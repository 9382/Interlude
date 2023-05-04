﻿namespace Interlude.Features.Play.HUD

open System
open Percyqaz.Flux.Audio
open Percyqaz.Flux.Graphics
open Percyqaz.Flux.Input
open Percyqaz.Flux.UI
open Prelude.Common
open Prelude.Charts.Formats.Interlude
open Prelude.Scoring
open Prelude.Scoring.Grading
open Prelude.Data.Content
open Interlude
open Interlude.UI
open Interlude.Content
open Interlude.Features
open Interlude.Features.Online
open Interlude.Features.Play
open Interlude.Features.Gameplay.Online
open Interlude.Utils

(*
    Handful of widgets that directly pertain to gameplay
    They can all be toggled/repositioned/configured using themes
*)

type AccuracyMeter(conf: HUD.AccuracyMeter, state) as this =
    inherit StaticContainer(NodeType.None)

    let grades = state.Ruleset.Grading.Grades
    let color = Animation.Color (if conf.GradeColors then Array.last(grades).Color else Color.White)

    do
        if conf.GradeColors then
            state.SubscribeToHits
                ( fun _ ->
                    color.Target <- Grade.calculate grades state.Scoring.State |> state.Ruleset.GradeColor
                )

        this
        |* Text(
            (fun () -> state.Scoring.FormatAccuracy()),
            Color = (fun () -> color.Value, Color.Transparent),
            Align = Alignment.CENTER,
            Position = { Position.Default with Bottom = 0.7f %+ 0.0f })
        if conf.ShowName then
            this
            |* Text((fun () -> state.Scoring.Name),
                Color = K (Color.White, Color.Transparent),
                Align = Alignment.CENTER,
                Position = { Position.Default with Top = 0.6f %+ 0.0f })

    override this.Update(elapsedTime, moved) =
        base.Update(elapsedTime, moved)
        color.Update elapsedTime

[<Struct>]
type private HitMeterHit = { Time: Time; Position: float32; IsRelease: bool; Judgement: JudgementId option }

type HitMeter(conf: HUD.HitMeter, state: PlayState) =
    inherit StaticWidget(NodeType.None)
    let hits = ResizeArray<HitMeterHit>()
    let mutable w = 0.0f

    let mutable last_seen_time = -Time.infinity

    do
        state.SubscribeToHits(fun ev ->
            match ev.Guts with
            | Hit e ->
                hits.Add { Time = ev.Time; Position = e.Delta / state.Scoring.MissWindow * w * 0.5f; IsRelease = false; Judgement = e.Judgement }
            | Release e ->
                hits.Add { Time = ev.Time; Position = e.Delta / state.Scoring.MissWindow * w * 0.5f; IsRelease = true; Judgement = e.Judgement }
        )

    override this.Update(elapsedTime, moved) =
        base.Update(elapsedTime, moved)
        if w = 0.0f then w <- this.Bounds.Width
        let now = state.CurrentChartTime()
        if now < last_seen_time then hits.Clear()
        last_seen_time <- now
        while hits.Count > 0 && hits.[0].Time + conf.AnimationTime * 1.0f<ms> < now do
            hits.RemoveAt(0)

    override this.Draw() =
        let centre = this.Bounds.CenterX
        if conf.ShowGuide then
            Draw.rect
                (Rect.Create(centre - conf.Thickness, this.Bounds.Top, centre + conf.Thickness, this.Bounds.Bottom))
                Color.White
        let now = state.CurrentChartTime()
        for hit in hits do
            let r = Rect.Create(centre + hit.Position - conf.Thickness, this.Bounds.Top, centre + hit.Position + conf.Thickness, this.Bounds.Bottom)
            let c = 
                match hit.Judgement with
                | None -> Color.FromArgb(Math.Clamp(127 - int (127.0f * (now - hit.Time) / conf.AnimationTime), 0, 127), Color.Silver)
                | Some j -> Color.FromArgb(Math.Clamp(255 - int (255.0f * (now - hit.Time) / conf.AnimationTime), 0, 255), state.Ruleset.JudgementColor j)
            if conf.ShowNonJudgements || hit.Judgement.IsSome then
                Draw.rect (if hit.IsRelease then r.Expand(0.0f, conf.ReleasesExtraHeight) else r) c

//type JudgementMeter(conf: WidgetConfig.JudgementMeter, helper) =
//    inherit StaticWidget(NodeType.None)
//    let atime = conf.AnimationTime * 1.0f<ms>
//    let mutable tier = 0
//    let mutable late = 0
//    let mutable time = -Time.infinity
//    let texture = Content.getTexture "judgement"

//    do
//        helper.OnHit.Add
//            ( fun ev ->
//                let (judge, delta) =
//                    match ev.Guts with
//                    | Hit e -> (e.Judgement, e.Delta)
//                    | Release e -> (e.Judgement, e.Delta)
//                if
//                    judge.IsSome && true
//                    //match judge.Value with
//                    //| _JType.RIDICULOUS
//                    //| _JType.MARVELLOUS -> conf.ShowRDMA
//                    //| _ -> true
//                then
//                    let j = int judge.Value in
//                    if j >= tier || ev.Time - atime > time then
//                        tier <- j
//                        time <- ev.Time
//                        late <- if delta > 0.0f<ms> then 1 else 0
//            )

//    override this.Draw() =
//        if time > -Time.infinity then
//            let a = 255 - Math.Clamp(255.0f * (helper.CurrentChartTime() - time) / atime |> int, 0, 255)
//            Draw.quad (Quad.ofRect this.Bounds) (Quad.colorOf (Color.FromArgb(a, Color.White))) (Sprite.gridUV (late, tier) texture)

type ComboMeter(conf: HUD.Combo, state: PlayState) =
    inherit StaticWidget(NodeType.None)
    let popAnimation = Animation.Fade(0.0f)
    let color = Animation.Color(Color.White)
    let mutable hits = 0

    do
        state.SubscribeToHits(
            fun _ ->
                hits <- hits + 1
                if (conf.LampColors && hits > 50) then
                    color.Target <-
                        Lamp.calculate state.Ruleset.Grading.Lamps state.Scoring.State
                        |> state.Ruleset.LampColor
                popAnimation.Value <- conf.Pop)

    override this.Update(elapsedTime, moved) =
        base.Update(elapsedTime, moved)
        color.Update elapsedTime
        popAnimation.Update elapsedTime

    override this.Draw() =
        let combo = state.Scoring.State.CurrentCombo
        let amt = popAnimation.Value + (((combo, 1000) |> Math.Min |> float32) * conf.Growth)
        Text.drawFill(Style.baseFont, combo.ToString(), this.Bounds.Expand amt, color.Value, 0.5f)

type ProgressMeter(conf: HUD.ProgressMeter, state) =
    inherit StaticWidget(NodeType.None)

    let duration = 
        let chart = Gameplay.Chart.colored()
        offsetOf chart.Notes.Last.Value - offsetOf chart.Notes.First.Value

    let pulse = Animation.Counter(1000.0)
        
    override this.Update(elapsedTime, moved) =
        base.Update(elapsedTime, moved)
        pulse.Update elapsedTime

    override this.Draw() =
        let height = this.Bounds.Height - conf.BarHeight
        let pc = state.CurrentChartTime() / duration

        let bar = Rect.Box(this.Bounds.Left, (this.Bounds.Top + height * pc), this.Bounds.Width, conf.BarHeight)
        let glowA = (float conf.GlowColor.A) * pulse.Time / 1000.0 |> int
        Draw.rect (bar.Expand(conf.GlowSize)) (Color.FromArgb(glowA, conf.GlowColor))
        Draw.rect bar conf.BarColor

type SkipButton(conf: HUD.SkipButton, state) =
    inherit StaticWidget(NodeType.None)

    let text = Localisation.localiseWith [(!|"skip").ToString()] "play.skiphint"
    let mutable active = true
        
    let firstNote = offsetOf (Gameplay.Chart.colored().Notes.First.Value)

    override this.Update(elapsedTime, bounds) =
        base.Update(elapsedTime, bounds)
        if active && state.CurrentChartTime() < -Song.LEADIN_TIME * 2.5f then
            if (!|"skip").Tapped() then
                Song.pause()
                Song.playFrom(firstNote - Song.LEADIN_TIME)
        else active <- false

    override this.Draw() =
        if active then Text.drawFillB(Style.baseFont, text, this.Bounds, Style.text(), Alignment.CENTER)

type LifeMeter(conf: HUD.LifeMeter, state: PlayState) =
    inherit StaticWidget(NodeType.None)

    let color = Animation.Color conf.FullColor
    let slider = Animation.Fade(float32 state.Scoring.HP.State.Health)

    override this.Update(elapsedTime, moved) =
        base.Update(elapsedTime, moved)
        slider.Target <- float32 state.Scoring.HP.State.Health
        color.Target <- (Color.FromArgb(
            Percyqaz.Flux.Utils.lerp (float32 state.Scoring.HP.State.Health) (float32 conf.EmptyColor.R) (float32 conf.FullColor.R) |> int,
            Percyqaz.Flux.Utils.lerp (float32 state.Scoring.HP.State.Health) (float32 conf.EmptyColor.G) (float32 conf.FullColor.G) |> int,
            Percyqaz.Flux.Utils.lerp (float32 state.Scoring.HP.State.Health) (float32 conf.EmptyColor.B) (float32 conf.FullColor.B) |> int
            ))
        color.Update elapsedTime
        slider.Update elapsedTime

    override this.Draw() =
        let w, h = this.Bounds.Width, this.Bounds.Height
        if conf.Horizontal then
            let b = this.Bounds.SliceLeft(w * slider.Value)
            Draw.rect b color.Value
            Draw.rect (b.SliceRight h) conf.TipColor
        else
            let b = this.Bounds.SliceBottom(h * slider.Value)
            Draw.rect b color.Value
            Draw.rect (b.SliceTop w) conf.TipColor

type Pacemaker(conf: HUD.Pacemaker, state: PlayState) =
    inherit StaticWidget(NodeType.None)

    let color = Animation.Color(Color.White)
    let flag_position = Animation.Fade(0.5f)
    let position_cooldown = Animation.Delay(3000.0)
    let mutable ahead_by = 0.0
    let mutable hearts = -1

    let update_flag_position() =
        if ahead_by >= 10.0 then flag_position.Target <- 1.0f
        elif ahead_by > -10.0 then 
            flag_position.Target <- (ahead_by + 10.0) / 20.0 |> float32
            if ahead_by > 0.0 then color.Target <- Color.FromHsv(140.0f/360.0f, ahead_by / 10.0 |> float32, 1.0f)
            else color.Target <- Color.FromHsv(340.0f/360.0f, ahead_by / -10.0 |> float32, 1.0f)
        else flag_position.Target <- 0.0f

    do
        match state.Pacemaker with
        | PacemakerInfo.None
        | PacemakerInfo.Accuracy _
        | PacemakerInfo.Replay _ -> ()
        | PacemakerInfo.Judgement (judgement, _) -> 
            color.Target <-
                if judgement = -1 then 
                    Rulesets.current.Judgements.[Rulesets.current.Judgements.Length - 1].Color
                else Rulesets.current.Judgements.[judgement].Color
            

    override this.Update(elapsedTime, moved) =
        base.Update(elapsedTime, moved)

        match state.Pacemaker with
        | PacemakerInfo.None -> ()
        | PacemakerInfo.Accuracy x ->
            if position_cooldown.Complete then
                ahead_by <- state.Scoring.State.PointsScored - state.Scoring.State.MaxPointsScored * x
                update_flag_position()
                position_cooldown.Reset()

            flag_position.Update elapsedTime
            position_cooldown.Update elapsedTime
        | PacemakerInfo.Replay score ->
            if position_cooldown.Complete then
                score.Update(state.CurrentChartTime())
                ahead_by <- state.Scoring.State.PointsScored - score.State.PointsScored
                update_flag_position()
                position_cooldown.Reset()

            flag_position.Update elapsedTime
            position_cooldown.Update elapsedTime
        | PacemakerInfo.Judgement (_, _) -> ()

        color.Update elapsedTime

    override this.Draw() =
        match state.Pacemaker with
        | PacemakerInfo.None -> ()
        | PacemakerInfo.Accuracy _
        | PacemakerInfo.Replay _ ->
            Text.drawFillB(Style.baseFont, Icons.goal, this.Bounds.SliceLeft(0.0f).Expand(this.Bounds.Height, 0.0f).Translate(this.Bounds.Width * flag_position.Value, 0.0f), (color.Value, Color.Black), Alignment.CENTER)
        | PacemakerInfo.Judgement (judgement, count) ->
            let actual = 
                if judgement = -1 then state.Scoring.State.ComboBreaks
                else
                    let mutable c = state.Scoring.State.Judgements.[judgement]
                    for j = judgement + 1 to state.Scoring.State.Judgements.Length - 1 do
                        if state.Scoring.State.Judgements.[j] > 0 then c <- 1000000
                    c
            let _hearts = 1 + count - actual
            if _hearts < hearts then color.Value <- Color.White
            hearts <- _hearts
            let display = 
                if hearts > 5 then sprintf "%s x%i" (String.replicate 5 Icons.heart2) hearts
                elif hearts > 0 then (String.replicate hearts Icons.heart2)
                else Icons.failure
            Text.drawFillB(Style.baseFont, display, this.Bounds, (color.Value, Color.Black), Alignment.CENTER)

type JudgementCounts(conf: HUD.JudgementCounts, state: PlayState) =
    inherit StaticWidget(NodeType.None)

    let judgementAnimations = Array.init state.Ruleset.Judgements.Length (fun _ -> Animation.Delay(conf.AnimationTime))

    do
        state.SubscribeToHits (
            fun h -> 
                match h.Guts with 
                | Hit x -> if x.Judgement.IsSome then judgementAnimations[x.Judgement.Value].Reset()
                | Release x -> if x.Judgement.IsSome then judgementAnimations[x.Judgement.Value].Reset()
            )

    override this.Update(elapsedTime, moved) =
        base.Update(elapsedTime, moved)
        for j in judgementAnimations do
            j.Update elapsedTime

    override this.Draw() =
        let h = this.Bounds.Height / float32 judgementAnimations.Length
        let mutable r = this.Bounds.SliceTop(h).Shrink(5.0f)
        for i = 0 to state.Ruleset.Judgements.Length - 1 do
            let j = state.Ruleset.Judgements.[i]
            Draw.rect (r.Expand(10.0f, 5.0f).SliceLeft(5.0f)) j.Color
            if not judgementAnimations.[i].Complete && state.Scoring.State.Judgements.[i] > 0 then
                Draw.rect (r.Expand 5.0f) (Color.FromArgb(127 - max 0 (int (127.0 * judgementAnimations.[i].Elapsed / conf.AnimationTime)), j.Color))
            Text.drawFillB(Style.baseFont, j.Name, r, (Color.White, Color.Black), Alignment.LEFT)
            Text.drawFillB(Style.baseFont, state.Scoring.State.Judgements.[i].ToString(), r, (Color.White, Color.Black), Alignment.RIGHT)
            r <- r.Translate(0.0f, h)

type MultiplayerScoreTracker(conf: HUD.Pacemaker, state: PlayState) =
    inherit StaticWidget(NodeType.None)

    override this.Draw() =
        let x = this.Bounds.Right + 100.0f
        let mutable y = this.Bounds.Top
        Multiplayer.replays
        |> Seq.map (|KeyValue|)
        |> Seq.sortByDescending (fun (_, s) -> s.Value)
        |> Seq.iter (fun (username, s) ->
            let c = if username = Network.username then Color.SkyBlue else Color.White
            Text.draw(Style.baseFont, username, 20.0f, x, y, c)
            Text.drawJust(Style.baseFont, s.FormatAccuracy(), 20.0f, x - 10.0f, y, c, 1.0f)
            y <- y + 25.0f
        )

    override this.Update(elapsedTime, moved) =
        base.Update(elapsedTime, moved)
        for s in Multiplayer.replays.Values do
            s.Update(state.CurrentChartTime() - Web.Shared.Packets.MULTIPLAYER_REPLAY_DELAY_MS * 2.0f<ms>)