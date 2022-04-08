﻿namespace Interlude.UI.Screens.LevelSelect

open System
open Prelude.Common
open Prelude.Data.Scores
open Prelude.Data.Charts.Caching
open Prelude.Scoring
open Prelude.ChartFormats.Interlude
open Interlude.UI
open Interlude.Utils
open Interlude.Graphics
open Interlude.Input
open Interlude.Gameplay
open Interlude.Options
open Interlude.UI.Animation
open Interlude.UI.Components
open Interlude.UI.Components.Selection
open Interlude.UI.Components.Selection.Containers

module Scoreboard =

    type Sort =
    | Time = 0
    | Performance = 1
    | Accuracy = 2

    type Filter =
    | All = 0
    | CurrentRate = 1
    | CurrentPlaystyle = 2
    | CurrentMods = 3

    type ScoreCard(data: ScoreInfoProvider) as this =
        inherit Widget()

        let fade = AnimationFade 0.0f

        do
            data.Physical |> ignore
            data.Lamp |> ignore

            let colfun = fun () -> let a = int (255.0f * fade.Value) in (Color.FromArgb(a, Color.White), Color.FromArgb(a, Color.Black))
            
            TextBox((fun() -> data.Scoring.FormatAccuracy()), colfun, 0.0f)
            |> positionWidget(5.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.5f, 0.0f, 0.6f)
            |> this.Add

            TextBox((fun () -> sprintf "%s  •  %ix  •  %.2f" (data.Ruleset.LampName data.Lamp) data.Scoring.State.BestCombo data.Physical), colfun, 0.0f)
            |> positionWidget(5.0f, 0.0f, 0.0f, 0.6f, 0.0f, 0.5f, 0.0f, 1.0f)
            |> this.Add

            TextBox(K (formatTimeOffset(DateTime.Now - data.ScoreInfo.time)), colfun, 1.0f)
            |> positionWidget(0.0f, 0.5f, 0.0f, 0.6f, -5.0f, 1.0f, 0.0f, 1.0f)
            |> this.Add

            TextBox(K data.Mods, colfun, 1.0f)
            |> positionWidget(0.0f, 0.5f, 0.0f, 0.0f, -5.0f, 1.0f, 0.0f, 0.6f)
            |> this.Add

            Clickable((fun () -> Screen.changeNew (fun () -> new Screens.Score.Screen(data, BestFlags.Default) :> Screen.T) Screen.Type.Score Screen.TransitionFlag.Default), ignore)
            |> this.Add

            this.Animation.Add fade
            Animation.Serial(AnimationTimer 150.0, AnimationAction (fun () -> let (l, t, r, b) = this.Anchors in l.Snap(); t.Snap(); r.Snap(); b.Snap(); fade.Target <- 1.0f))
            |> this.Animation.Add

            this.Reposition(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 75.0f, 0.0f)

        override this.Draw() =
            Draw.rect this.Bounds (Style.accentShade(int (100.0f * fade.Value), 0.5f, 0.0f)) Sprite.Default
            base.Draw()
        member this.Data = data

        override this.Update(elapsedTime, bounds) =
            base.Update(elapsedTime, bounds)
            if Mouse.Hover this.Bounds && options.Hotkeys.Delete.Value.Tapped() then
                let scoreName = sprintf "%s | %s" (data.Scoring.FormatAccuracy()) (data.Lamp.ToString())
                Tooltip.callback (
                    options.Hotkeys.Delete.Value,
                    Localisation.localiseWith [scoreName] "misc.delete",
                    Warning,
                    fun () ->
                        Chart.saveData.Value.Scores.Remove data.ScoreInfo |> ignore
                        LevelSelect.refresh <- true
                        Notification.add (Localisation.localiseWith [scoreName] "notification.deleted", Info)
                )

    module Loader =

        let reload (container: FlowContainer) =
            let mutable rsid = ""
            let mutable rs = Unchecked.defaultof<_>
            let mutable calculateBests : Bests option = None
            let future =
                BackgroundTask.futureSeq<ScoreInfoProvider> "Scoreboard loader"
                    ( fun score ->
                        calculateBests <- Some (
                            match calculateBests with
                            | None -> Bests.create score
                            | Some b -> fst(Bests.update score b))
                        let sc = ScoreCard score
                        container.Synchronized(fun () -> container.Add sc)
                    )
                    ( fun () -> 
                        match calculateBests with
                        | None -> ()
                        | Some b ->
                            container.Synchronized( fun () -> 
                                if not (Chart.saveData.Value.Bests.ContainsKey rsid) || b <> Chart.saveData.Value.Bests[rsid] then
                                    Globals.colorVersionGlobal <- Globals.colorVersionGlobal + 1
                                Chart.saveData.Value.Bests[rsid] <- b
                            )
                    )
            fun () ->
                future
                    ( fun () ->
                        // capture current ruleset, avoids race conditions
                        rs <- ruleset
                        rsid <- rulesetId
                        calculateBests <- None
                        container.Synchronized(container.Clear)
                        match Chart.saveData with
                        | None -> Seq.empty
                        | Some d ->
                            seq { 
                                for score in d.Scores do
                                    yield ScoreInfoProvider(score, Chart.current.Value, rs)
                            }
                    )

open Scoreboard

type Scoreboard() as this =
    inherit Selectable()

    let mutable count = -1

    let mutable chart = ""
    let mutable scoring = ""
    let ls = new ListSelectable(true)

    let filter = Setting.simple Filter.All
    let sort = Setting.map enum int options.ScoreSortMode

    let sorter() : Comparison<Widget> =
        match sort.Value with
        | Sort.Accuracy -> Comparison(fun b a -> (a :?> ScoreCard).Data.Scoring.Value.CompareTo((b :?> ScoreCard).Data.Scoring.Value))
        | Sort.Performance -> Comparison(fun b a -> (a :?> ScoreCard).Data.Physical.CompareTo((b :?> ScoreCard).Data.Physical))
        | Sort.Time
        | _ -> Comparison(fun b a -> (a :?> ScoreCard).Data.ScoreInfo.time.CompareTo((b :?> ScoreCard).Data.ScoreInfo.time))

    let filterer() : Widget -> bool =
        match filter.Value with
        | Filter.CurrentRate -> (fun a -> (a :?> ScoreCard).Data.ScoreInfo.rate = rate.Value)
        | Filter.CurrentPlaystyle -> (fun a -> (a :?> ScoreCard).Data.ScoreInfo.layout = options.Playstyles.[(a :?> ScoreCard).Data.ScoreInfo.keycount - 3])
        | Filter.CurrentMods -> (fun a -> (a :?> ScoreCard).Data.ScoreInfo.selectedMods = selectedMods.Value)
        | _ -> K true


    let flowContainer = new FlowContainer(Sort = sorter(), Filter = filterer())

    let loader = Loader.reload flowContainer

    let scoreLoader =
        let future =
            BackgroundTask.futureSeq<ScoreCard> "Scoreboard loader"
                (fun item -> flowContainer.Synchronized(fun () -> flowContainer.Add item))
                ignore
        fun () ->
            future
                ( fun () ->
                    flowContainer.Synchronized(flowContainer.Clear)
                    match Chart.saveData with
                    | None -> Seq.empty
                    | Some d ->
                        seq { 
                            for score in d.Scores do
                                yield ScoreInfoProvider(score, Chart.current.Value, getCurrentRuleset())
                                |> ScoreCard
                        }
                )

    do
        flowContainer
        |> positionWidgetA(0.0f, 10.0f, 0.0f, -50.0f)
        |> this.Add

        StylishButton.FromEnum("Sort",
            sort |> Setting.trigger (fun _ -> flowContainer.Sort <- sorter()),
            Style.main 100, TiltLeft = false )
        |> TooltipRegion.Create (L"levelselect.scoreboard.sort.tooltip")
        |> positionWidget(0.0f, 0.0f, -45.0f, 1.0f, -15.0f, 0.25f, -5.0f, 1.0f)
        |> ls.Add

        StylishButton.FromEnum("Filter",
            filter |> Setting.trigger (fun _ -> this.Refresh()),
            Style.main 90 )
        |> TooltipRegion.Create (L"levelselect.scoreboard.filter.tooltip")
        |> positionWidget(10.0f, 0.25f, -45.0f, 1.0f, -15.0f, 0.5f, -5.0f, 1.0f)
        |> ls.Add

        StylishButton(
            (fun () -> Setting.app WatcherSelection.cycleForward options.Rulesets; LevelSelect.refresh <- true),
            (fun () -> ruleset.Name),
            Style.main 80 )
        |> TooltipRegion.Create (L"levelselect.scoreboard.ruleset.tooltip")
        |> positionWidget(10.0f, 0.5f, -45.0f, 1.0f, -15.0f, 0.75f, -5.0f, 1.0f)
        |> ls.Add

        StylishButton(
            this.Refresh,
            K <| Localisation.localise "levelselect.scoreboard.storage.local",
            Style.main 70, TiltRight = false ) //nyi
        |> TooltipRegion.Create (L"levelselect.scoreboard.storage.tooltip")
        |> positionWidget(10.0f, 0.75f, -45.0f, 1.0f, -15.0f, 1.0f, -5.0f, 1.0f)
        |> ls.Add

        ls |> this.Add

        let noLocalScores = Localisation.localise "levelselect.scoreboard.empty"
        TextBox((fun () -> if count = 0 then noLocalScores else ""), K (Color.White, Color.Black), 0.5f)
        |> positionWidget(50.0f, 0.0f, 0.0f, 0.3f, -50.0f, 1.0f, 0.0f, 0.5f)
        |> this.Add

    member this.Refresh() =
        let h = match Chart.cacheInfo with Some c -> c.Hash | None -> ""
        if (match Chart.saveData with None -> false | Some d -> let v = d.Scores.Count <> count in count <- d.Scores.Count; v) || h <> chart then
            chart <- h
            loader() |> ignore
        elif scoring <> rulesetId then
            let s = getCurrentRuleset()
            for c in flowContainer.Children do (c :?> ScoreCard).Data.Ruleset <- s
            scoring <- rulesetId
        flowContainer.Filter <- filterer()

    override this.Update(elapsedTime, bounds) =
        base.Update(elapsedTime, bounds)
        this.HoverChild <- None