﻿namespace Interlude.Features.Play

open OpenTK
open Percyqaz.Flux.Audio
open Percyqaz.Flux.Input
open Percyqaz.Flux.UI
open Prelude.Common
open Prelude.Charts.Formats.Interlude
open Prelude.Scoring
open Prelude.Scoring.Metrics
open Prelude.Data.Themes
open Prelude.Data.Scores
open Interlude
open Interlude.Options
open Interlude.UI
open Interlude.Features
open Interlude.Features.Play.GameplayWidgets
open Interlude.Features.Score

[<RequireQualifiedAccess>]
type PacemakerMode =
    | None
    | Score of rate: float32 * ReplayData
    | Setting

type PlayScreen(pacemakerMode: PacemakerMode) as this =
    inherit Screen()
    
    let chart = Gameplay.Chart.withMods.Value
    let firstNote = offsetOf chart.Notes.First.Value

    let liveplay = LiveReplayProvider firstNote
    let scoringConfig = Gameplay.ruleset
    let scoring = createScoreMetric scoringConfig chart.Keys liveplay chart.Notes Gameplay.rate.Value
    let onHit = new Event<HitEvent<HitEventGuts>>()

    let pacemakerInfo =
        match pacemakerMode with
        | PacemakerMode.None -> PacemakerInfo.None
        | PacemakerMode.Score (rate, replay) ->
            let replayData = StoredReplayProvider(replay) :> IReplayProvider
            let scoring = createScoreMetric scoringConfig chart.Keys replayData chart.Notes rate
            PacemakerInfo.Replay scoring
        | PacemakerMode.Setting ->
            let setting = if options.Pacemakers.ContainsKey Gameplay.rulesetId then options.Pacemakers.[Gameplay.rulesetId] else Pacemaker.Default
            match setting with
            | Pacemaker.Accuracy acc -> PacemakerInfo.Accuracy acc
            | Pacemaker.Lamp lamp ->
                let l = Gameplay.ruleset.Grading.Lamps.[lamp]
                PacemakerInfo.Judgement(l.Judgement, l.JudgementThreshold)

    let widgetHelper: Helper =
        {
            ScoringConfig = scoringConfig
            Scoring = scoring
            HP = scoring.HP
            OnHit = onHit.Publish
            CurrentChartTime = fun () -> Song.timeWithOffset() - firstNote
            Pacemaker = pacemakerInfo
        }
    let binds = options.GameplayBinds.[chart.Keys - 3]

    let mutable inputKeyState = 0us

    do
        let noteRenderer = NoteRenderer scoring
        this.Add noteRenderer

        if Content.noteskinConfig().EnableColumnLight then
            noteRenderer.Add(new ColumnLighting(chart.Keys, Content.noteskinConfig().ColumnLightTime, widgetHelper))

        if Content.noteskinConfig().Explosions.FadeTime >= 0.0f then
            noteRenderer.Add(new Explosions(chart.Keys, Content.noteskinConfig().Explosions, widgetHelper))

        noteRenderer.Add(LaneCover())

        let inline add_widget (constructor: 'T -> Widget) = 
            let config: ^T = Content.getGameplayConfig<'T>()
            let pos: WidgetConfig = (^T: (member Position: WidgetConfig) config)
            if pos.Enabled then
                let w = constructor config
                w.Position <- { Left = pos.LeftA %+ pos.Left; Top = pos.TopA %+ pos.Top; Right = pos.RightA %+ pos.Right; Bottom = pos.BottomA %+ pos.Bottom }
                if pos.Float then this.Add w else noteRenderer.Add w

        add_widget (fun c -> new AccuracyMeter(c, widgetHelper))
        add_widget (fun c -> new HitMeter(c, widgetHelper))
        add_widget (fun c -> new LifeMeter(c, widgetHelper))
        add_widget (fun c -> new ComboMeter(c, widgetHelper))
        add_widget (fun c -> new SkipButton(c, widgetHelper))
        add_widget (fun c -> new ProgressMeter(c, widgetHelper))
        add_widget (fun c -> new Pacemaker(c, widgetHelper))

        scoring.SetHitCallback onHit.Trigger

    override this.OnEnter(prev) =
        Background.dim (float32 options.BackgroundDim.Value)
        Screen.Toolbar.hide()
        Song.changeRate Gameplay.rate.Value
        Song.changeGlobalOffset (toTime options.AudioOffset.Value)
        Song.onFinish <- SongFinishAction.Wait
        Song.playLeadIn()
        Input.finish_frame_events()

    override this.OnExit next =
        Background.dim 0.7f
        if next <> Screen.Type.Score then Screen.Toolbar.show()

    override this.Update(elapsedTime, bounds) =
        base.Update(elapsedTime, bounds)
        let now = Song.timeWithOffset()
        let chartTime = now - firstNote

        if not (liveplay :> IReplayProvider).Finished then
            // feed keyboard input into the replay provider
            Input.consumeGameplay(binds, fun column time isRelease ->
                if isRelease then inputKeyState <- Bitmap.unsetBit column inputKeyState
                else inputKeyState <- Bitmap.setBit column inputKeyState
                liveplay.Add(time, inputKeyState) )
            scoring.Update chartTime

        if (!|"options").Tapped() then
            Song.pause()
            inputKeyState <- 0us
            liveplay.Add(now, inputKeyState)
            QuickOptions.show(scoring, fun () -> Screen.changeNew (fun () -> PlayScreen(pacemakerMode) :> Screen.T) Screen.Type.Play Transitions.Flags.Default)

        if (!|"retry").Pressed() then
            Screen.changeNew (fun () -> PlayScreen(pacemakerMode) :> Screen.T) Screen.Type.Play Transitions.Flags.Default
        
        if scoring.Finished && not (liveplay :> IReplayProvider).Finished then
            liveplay.Finish()
            Screen.changeNew
                ( fun () ->
                    let sd =
                        ScoreInfoProvider (
                            Gameplay.makeScore((liveplay :> IReplayProvider).GetFullReplay(), chart.Keys),
                            Gameplay.Chart.current.Value,
                            scoringConfig,
                            ModChart = Gameplay.Chart.withMods.Value,
                            Difficulty = Gameplay.Chart.rating.Value
                        )
                    // todo: replace true with flag if pacemaker met
                    (sd, Gameplay.setScore true sd)
                    |> ScoreScreen
                )
                Screen.Type.Score
                Transitions.Flags.Default