﻿namespace Interlude.UI

open OpenTK
open System
open Prelude.Common
open Prelude.Data.Themes
open Prelude.Charts.Interlude
open Prelude.Gameplay.Score
open Interlude
open Interlude.Render
open Interlude.Options

//TODO LIST
//  ARROW NOTE ROTATION
//  SEEKING/REWINDING SUPPORT
//  COLUMN INDEPENDENT SV
//  MAYBE FIX HOLD TAIL CLIPPING

type NoteRenderer() as this =
    inherit Widget()
    //scale, column width, note provider should be options
    
    //functions to get bounding boxes for things. used to place other gameplay widgets on the playfield.

    //constants
    let (keys, notes, bpm, sv, mods) = Gameplay.coloredChart.Force()
    let columnPositions = Array.init keys (fun i -> float32 i * Themes.noteskinConfig.ColumnWidth)
    let columnWidths = Array.create keys (float32 Themes.noteskinConfig.ColumnWidth)
    let noteHeight = Themes.noteskinConfig.ColumnWidth
    let scale = float32(Options.profile.ScrollSpeed.Get()) / Gameplay.rate * 1.0f</ms>
    let hitposition = float32 <| Options.profile.HitPosition.Get()
    let holdnoteTrim = Themes.noteskinConfig.ColumnWidth * Themes.noteskinConfig.HoldNoteTrim

    let tailsprite = Themes.getTexture(if Themes.noteskinConfig.UseHoldTailTexture then "holdtail" else "holdhead")
    let animation = new Animation.AnimationCounter(200.0)

    //arrays of stuff that are reused/changed every frame. the data from the previous frame is not used, but making new arrays causes garbage collection
    let mutable note_seek = 0 //see comments for sv_seek and sv_peek. same role but for index of next row
    let mutable note_peek = note_seek
    let sv = Array.init (keys + 1) (fun i -> sv.GetChannelData(i - 1).Data)
    let sv_seek = Array.create (keys + 1) 0 //index of next appearing SV point for each channel; = number of SV points if there are no more
    let sv_peek = Array.create (keys + 1) 0 //same as sv_seek, but sv_seek stores the relevant point for t = now (according to music) and this stores t > now
    let sv_value = Array.create (keys + 1) 1.0f //value of the most recent sv point per channel, equal to the sv value at index (sv_peek - 1), or 1 if that point doesn't exist
    let sv_time = Array.zeroCreate (keys + 1)
    let column_pos = Array.zeroCreate keys //running position calculation of notes for sv
    let hold_presence = Array.create keys false
    let hold_pos = Array.create keys 0.0f
    let hold_colors = Array.create keys 0

    let scrollDirectionPos bottom = if Options.profile.Upscroll.Get() then id else fun (struct (l, t, r, b): Rect) -> struct (l, bottom - b, r, bottom - t)
    let scrollDirectionFlip = if (not Themes.noteskinConfig.FlipHoldTail) || Options.profile.Upscroll.Get() then id else Quad.flip

    do
        //todo: position differently for editor
        let width = Array.mapi (fun i n -> n + columnWidths.[i]) columnPositions |> Array.max
        let (screenAlign, columnAlign) = Themes.noteskinConfig.PlayfieldAlignment
        this.Reposition(-width * columnAlign, screenAlign, 0.0f, 0.0f, width * (1.0f - columnAlign), screenAlign, 0.0f, 1.0f)
        this.Animation.Add(animation)

    override this.Draw() =
        let struct (left, top, right, bottom) = this.Bounds
        let playfieldHeight = bottom - top
        let now = Audio.timeWithOffset()

        //seek to appropriate sv and note locations in data.
        //all of this stuff could be wrapped in an object handling seeking/peeking but would give slower performance because it's based on Seq and not ResizeArray
        //i therefore sadly had to make a mess here. see comments on the variables for more on whats going on
        while note_seek < notes.Data.Count && (offsetOf notes.Data.[note_seek]) < now do
            note_seek <- note_seek + 1
        note_peek <- note_seek
        for i in 0 .. keys do
            while sv_seek.[i] < sv.[i].Count && (offsetOf <| sv.[i].[sv_seek.[i]]) < now do
                sv_seek.[i] <- sv_seek.[i] + 1
            sv_peek.[i] <- sv_seek.[i]
            sv_value.[i] <- if sv_seek.[i] > 0 then snd sv.[i].[sv_seek.[i] - 1] else 1.0f

        for k in 0 .. (keys - 1) do
            Draw.rect(Rect.create (left + columnPositions.[k]) top (left + columnPositions.[k] + columnWidths.[k]) bottom) (Color.FromArgb(127, 0, 0, 0)) Sprite.Default
            sv_time.[k] <- now
            column_pos.[k] <- hitposition
            hold_pos.[k] <- hitposition
            hold_presence.[k] <-
                if note_seek > 0 then
                    let (_, struct (nd, c)) = notes.Data.[note_seek - 1] in
                    hold_colors.[k] <- int c.[k]
                    (testForNote k NoteType.HOLDHEAD nd || testForNote k NoteType.HOLDBODY nd)
                else false
            Draw.rect(Rect.create (left + columnPositions.[k]) hitposition (left + columnPositions.[k] + columnWidths.[k]) (hitposition + noteHeight) |> scrollDirectionPos bottom) Color.White (Themes.getTexture("receptor")) //animation for being pressed

        //main render loop - until the last note rendered in every column appears off screen
        let mutable min = hitposition
        while min < playfieldHeight && note_peek < notes.Data.Count do
            min <- playfieldHeight
            let (t, struct (nd, color)) = notes.Data.[note_peek]
            //until no sv adjustments needed...
            //update main sv
            while (sv_peek.[0] < sv.[0].Count && offsetOf sv.[0].[sv_peek.[0]] < t) do
                let (t2, v) = sv.[0].[sv_peek.[0]]
                for k in 0 .. (keys - 1) do
                    column_pos.[k] <- column_pos.[k] + scale * sv_value.[0] * (t2 - sv_time.[k])
                    sv_time.[k] <- t2
                sv_value.[0] <- v
                sv_peek.[0] <- sv_peek.[0] + 1
            //update column sv

            //render notes
            for k in 0 .. (keys - 1) do
                column_pos.[k] <- column_pos.[k] + scale * sv_value.[0] * (t - sv_time.[k])
                sv_time.[k] <- t
                min <- Math.Min(column_pos.[k], min)
                if testForNote k NoteType.NORMAL nd then
                    Draw.quad (Quad.ofRect (Rect.create(left + columnPositions.[k]) column_pos.[k] (left + columnPositions.[k] + columnWidths.[k]) (column_pos.[k] + noteHeight) |> scrollDirectionPos bottom)) (Quad.colorOf Color.White) (Sprite.uv(animation.Loops, int color.[k])(Themes.getTexture("note")))
                elif testForNote k NoteType.HOLDHEAD nd then
                    hold_pos.[k] <- column_pos.[k]
                    hold_colors.[k] <- int color.[k]
                    hold_presence.[k] <- true
                elif testForNote k NoteType.HOLDTAIL nd then
                    let headpos = hold_pos.[k]
                    let pos = column_pos.[k] - holdnoteTrim
                    if headpos < pos then
                        Draw.quad (Quad.ofRect (Rect.create(left + columnPositions.[k]) (headpos + noteHeight * 0.5f) (left + columnPositions.[k] + columnWidths.[k]) (pos + noteHeight * 0.5f) |> scrollDirectionPos bottom)) (Quad.colorOf Color.White) (Sprite.uv(animation.Loops, hold_colors.[k])(Themes.getTexture("holdbody")))
                    if headpos - pos < noteHeight * 0.5f then
                        Draw.quad
                            (Quad.ofRect (Rect.create(left + columnPositions.[k]) (Math.Max(pos, headpos + noteHeight * 0.5f)) (left + columnPositions.[k] + columnWidths.[k]) (pos + noteHeight) |> scrollDirectionPos bottom))
                            (Quad.colorOf Color.White)
                            (Sprite.uv(animation.Loops, int color.[k])(tailsprite) |> fun struct (x, y) -> struct (x, scrollDirectionFlip y))
                    Draw.quad (Quad.ofRect (Rect.create(left + columnPositions.[k]) headpos (left + columnPositions.[k] + columnWidths.[k]) (headpos + noteHeight) |> scrollDirectionPos bottom)) (Quad.colorOf Color.White) (Sprite.uv(animation.Loops, hold_colors.[k])(Themes.getTexture("holdhead")))
                    hold_presence.[k] <- false
                elif testForNote k NoteType.MINE nd then
                    Draw.quad (Quad.ofRect (Rect.create(left + columnPositions.[k]) column_pos.[k] (left + columnPositions.[k] + columnWidths.[k]) (column_pos.[k] + noteHeight) |> scrollDirectionPos bottom)) (Quad.colorOf Color.White) (Sprite.uv(animation.Loops, int color.[k])(Themes.getTexture("mine")))
                    
            note_peek <- note_peek + 1
        
        for k in 0 .. (keys - 1) do
            if hold_presence.[k] then
                let headpos = hold_pos.[k]
                Draw.quad (Quad.ofRect (Rect.create(left + columnPositions.[k]) (headpos + noteHeight * 0.5f) (left + columnPositions.[k] + columnWidths.[k]) bottom |> scrollDirectionPos bottom)) (Quad.colorOf Color.White) (Sprite.uv(animation.Loops, hold_colors.[k])(Themes.getTexture("holdbody")))
                Draw.quad (Quad.ofRect (Rect.create(left + columnPositions.[k]) headpos (left + columnPositions.[k] + columnWidths.[k]) (headpos + noteHeight) |> scrollDirectionPos bottom)) (Quad.colorOf Color.White) (Sprite.uv(animation.Loops, hold_colors.[k])(Themes.getTexture("holdhead")))
        base.Draw()

module GameplayWidgets = 
    type HitEvent = (struct(int * Time * Time))
    type Helper = {
        Scoring: AccuracySystem
        OnHit: IEvent<HitEvent>
    }

    type AccuracyMeter(conf: WidgetConfig.AccuracyMeter, helper) as this =
        inherit Widget()
        do
            this.Add(new Components.TextBox(helper.Scoring.Format, (fun () -> Color.White), 0.5f))
            //todo: optionally show name of scoring system
            //todo: optionally color accuracy by current grade

    type HitMeter(conf: WidgetConfig.HitMeter, helper) =
        inherit Widget()
        let hits = ResizeArray<struct (Time * float32 * int)>()
        let mutable w = 0.0f
        let listener =
            helper.OnHit.Subscribe(
                fun struct (_, delta, now) -> hits.Add(struct (now, delta/MISSWINDOW * w * 0.5f, helper.Scoring.JudgeFunc(Time.Abs delta) |> int)))

        override this.Update(elapsedTime, bounds) =
            base.Update(elapsedTime, bounds)
            if w = 0.0f then w <- Rect.width this.Bounds
            let now = Audio.timeWithOffset()
            while hits.Count > 0 && let struct (time, _, _) = (hits.[0]) in time + conf.AnimationTime * 1.0f<ms> < now do
                hits.RemoveAt(0)

        override this.Draw() =
            base.Draw()
            let struct (left, top, right, bottom) = this.Bounds
            let centre = (right + left) * 0.5f
            //todo: optional guide bar in centre
            let now = Audio.timeWithOffset()
            for struct (time, pos, j) in hits do
                Draw.rect(Rect.create (centre + pos - conf.Thickness) top (centre + pos + conf.Thickness) bottom)
                    (let c = Themes.themeConfig.JudgementColors.[j] in
                        Color.FromArgb(255 - int (254.0f * (now - time) / conf.AnimationTime), int c.R, int c.G, int c.B))
                    (Sprite.Default)
        interface IDisposable with
            member this.Dispose() =
                listener.Dispose()

open GameplayWidgets

type ScreenPlay() as this =
    inherit Screen()
    
    let (keys, notes, bpm, sv, mods) = Gameplay.coloredChart.Force()
    let scoreData = Gameplay.replayData.Force()
    let scoring = createAccuracyMetric(SCPlus 4)
    let onHit = new Event<HitEvent>()
    let widgetHelper: Helper = { Scoring = scoring; OnHit = onHit.Publish }
    let binds = Options.options.GameplayBinds.[keys - 3]
    let missWindow = MISSWINDOW * Gameplay.rate

    do
        let noteRenderer = new NoteRenderer()
        this.Add(noteRenderer)
        let inline f name (constructor : 'T -> Widget) = 
            let config: ^T = Themes.getGameplayConfig(name)
            let pos : WidgetConfig = (^T: (member Position: WidgetConfig) (config)) //wtaf
            if pos.Enabled then
                config
                |> constructor
                |> Components.positionWidget(pos.Left, pos.LeftA, pos.Top, pos.TopA, pos.Right, pos.RightA, pos.Bottom, pos.BottomA)
                |> if pos.Float then this.Add else noteRenderer.Add
        f "accuracyMeter" (fun c -> new AccuracyMeter(c, widgetHelper) :> Widget)
        f "hitMeter" (fun c -> new HitMeter(c, widgetHelper) :> Widget)
        //todo: rest of widgets

    override this.OnEnter(prev) =
        if (prev :? ScreenScore) then
            Screens.popScreen()
        else
            Screens.backgroundDim.SetTarget(Options.profile.BackgroundDim.Get() |> float32)
            //discord presence
            Screens.setToolbarCollapsed(true)
            //disable cursor
            Audio.changeRate(Gameplay.rate)
            Audio.playLeadIn()

    override this.OnExit(next) =
        Screens.setToolbarCollapsed(false)
        Screens.backgroundDim.SetTarget(0.7f)

    member private this.Hit(i, k, delta, bad, now) =
        let _, deltas, status = scoreData.[i]
        match status.[k] with
        | HitStatus.Hit
        | HitStatus.SpecialNG
        | HitStatus.SpecialOK -> ()
        | HitStatus.NotHit ->
            deltas.[k] <- delta
            status.[k] <- HitStatus.Hit
            scoring.HandleHit(k)(i)(scoreData)
            onHit.Trigger(struct(k, delta, now))
        | HitStatus.Special ->
            deltas.[k] <- delta
            status.[k] <- if bad then HitStatus.SpecialNG else HitStatus.SpecialOK
            scoring.HandleHit(k)(i)(scoreData)
        | HitStatus.Nothing
        | _ -> failwith "impossible"

    member private this.HandleHit(k, now, release) =
        let i, _ = notes.IndexAt(now - missWindow) //maybe optimise this with another seeker?
        let mutable i = i + 1 //next index
        let mutable delta = missWindow
        let mutable hitAt = -1
        let mutable noteType = enum -1
        while i < notes.Count && offsetOf notes.Data.[i] < now + missWindow do
            let (time, struct (nd, _)) = notes.Data.[i]
            let (_, deltas, status) = scoreData.[i]
            if (status.[k] = HitStatus.NotHit || status.[k] = HitStatus.Special || deltas.[k] < -missWindow * 0.5f) then
                let d = now - time
                if release then
                    if (testForNote k NoteType.HOLDTAIL nd) then
                        if (Time.Abs(delta) > Time.Abs(d)) || noteType = NoteType.HOLDBODY  then
                            delta <- d
                            hitAt <- i
                            noteType <- NoteType.HOLDTAIL
                    else if noteType <> NoteType.HOLDTAIL && (testForNote k NoteType.HOLDBODY nd) then
                        if (Time.Abs(delta) > Time.Abs(d)) then
                            delta <- d
                            hitAt <- i
                            noteType <- NoteType.HOLDBODY
                else 
                    if (testForNote k NoteType.HOLDHEAD nd) || (testForNote k NoteType.NORMAL nd) then
                        if (Time.Abs(delta) > Time.Abs(d)) || noteType = NoteType.MINE  then
                            delta <- d
                            hitAt <- i
                            noteType <- NoteType.NORMAL
                    else if noteType <> NoteType.NORMAL && (testForNote k NoteType.MINE nd) then
                        if (Time.Abs(delta) > Time.Abs(d)) then
                            delta <- d
                            hitAt <- i
                            noteType <- NoteType.MINE
            i <- i + 1
        if hitAt >= 0 then
            delta <- delta / Gameplay.rate * (if release then 0.5f else 1.0f)
            this.Hit(hitAt, k, delta, noteType = NoteType.MINE || noteType = NoteType.HOLDBODY, now)


    override this.Update(elapsedTime, bounds) =
        base.Update(elapsedTime, bounds)
        let now = Audio.timeWithOffset()
        for k in 0 .. (keys - 1) do
            if binds.[k].Tapped(true) then
                this.HandleHit(k, now, false)
            elif (binds.[k].Released()) then
                this.HandleHit(k, now, true)
        let i, _ = notes.IndexAt(now - missWindow * 0.5f)
        let mutable i = i + 1
        //todo: fix potential issues with this behaviour
        while i < notes.Count && offsetOf notes.Data.[i] < now + missWindow * 0.5f do
            let (_, struct (nd, _)) = notes.Data.[i]
            let (_, _, s) = scoreData.[i]
            for k in noteData NoteType.HOLDBODY nd |> getBits do
                if s.[k] = HitStatus.Special && not (binds.[k].Pressed(true)) then s.[k] <- HitStatus.SpecialNG
            for k in noteData NoteType.MINE nd |> getBits do
                if s.[k] = HitStatus.Special && binds.[k].Pressed(true) then s.[k] <- HitStatus.SpecialNG
            i <- i + 1
        //todo: handle in all watchers
        scoring.Update(now - missWindow)(scoreData)

    override this.Draw() =
        base.Draw()
        let (judgements, pts, maxpts, combo, maxcombo, cbs) = scoring.State
        Text.draw(Themes.font(), combo.ToString(), 30.0f, 10.0f, 70.0f, Color.White)
        Text.draw(Themes.font(), maxcombo.ToString(), 30.0f, 10.0f, 100.0f, Color.White)
        for i in 1..(judgements.Length - 1) do
            Text.draw(Themes.font(), ((enum i):JudgementType).ToString() + ": " + judgements.[i].ToString(), 30.0f, 20.0f, 130.0f + 30.0f * float32 i, Color.White)
