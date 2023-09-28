﻿namespace Interlude.Features.Multiplayer

open System.Linq
open Percyqaz.Common
open Percyqaz.Flux.UI
open Percyqaz.Flux.Graphics
open Prelude.Common
open Prelude.Data.Charts
open Prelude.Data.Charts.Caching
open Prelude.Gameplay.Mods
open Interlude.Web.Shared
open Interlude.Utils
open Interlude.UI
open Interlude.UI.Menu
open Interlude.UI.Components
open Interlude.Features.Gameplay
open Interlude.Features.LevelSelect
open Interlude.Features.Online
open Interlude.Features.Play

type ContextMenu(cc: CachedChart) as this =
    inherit Page()

    do
        let content = 
            FlowContainer.Vertical(PRETTYHEIGHT, Position = Position.Margin(100.0f, 200.0f))

            |+ PageButton(
                "chart.add_to_collection",
                (fun () -> 
                    SelectCollectionPage(
                        fun (name, collection) ->
                            if CollectionManager.add_to(name, collection, cc) then Menu.Back()
                    ).Show()
                ),
                Icon = Icons.add_to_collection
            )
        this.Content content

    override this.Title = cc.Title
    override this.OnClose() = ()

module SelectedChart =

    let mutable chart = None
    let selected() =
        match Chart.CACHE_DATA, chart with
        | Some cc, Some chart -> cc.Hash = chart.Hash
        | _ -> false
    let loaded() =
        selected() && Chart.WITH_MODS.IsSome

    let ensure_selected() =
        match chart with
        | None -> ()
        | Some chart ->
            if selected() then
                rate.Set chart.Rate
                selectedMods.Set (chart.Mods |> Map.ofArray |> Map.filter (fun id _ -> modList.ContainsKey id))
            else
                match Cache.by_hash chart.Hash Library.cache with
                | None -> 
                    Logging.Info(sprintf "Chart not found locally: %s [%s]" chart.Title chart.Hash)
                    Lobby.missing_chart()
                | Some cc -> 
                    Chart.change(cc, Collections.LibraryContext.None)
                    rate.Set chart.Rate
                    selectedMods.Set (chart.Mods |> Map.ofArray |> Map.filter (fun id _ -> modList.ContainsKey id))

    let switch(c: LobbyChart option) =
        
        chart <- c
        ensure_selected()

type SelectedChart() =
    inherit StaticContainer(NodeType.None)

    override this.Init(parent: Widget) =
        
        this
        |+ Text((fun () -> match SelectedChart.chart with Some c -> c.Title | None -> L"lobby.no_song_selected"), Align = Alignment.LEFT, Position = Position.SliceTop(40.0f).Margin(10.0f, 0.0f))
        |+ Text((fun () -> match SelectedChart.chart with Some c -> c.Artist + "  •  " + c.Creator | None -> ""), Color = K Colors.text_subheading, Align = Alignment.LEFT, Position = Position.TrimTop(40.0f).SliceTop(30.0f).Margin(10.0f, 0.0f))
        |+ Text((fun () -> if SelectedChart.loaded() then Chart.CACHE_DATA.Value.DifficultyName else "???"), Color = K Colors.text_subheading, Align = Alignment.LEFT, Position = Position.TrimTop(70.0f).SliceTop(30.0f).Margin(10.0f, 0.0f))

        |+ Text((fun () -> if SelectedChart.loaded() then sprintf "%s %.2f" Icons.star Chart.RATING.Value.Physical else ""), Align = Alignment.LEFT, Position = Position.TrimTop(100.0f).SliceTop(60.0f))
        |+ Text((fun () -> if SelectedChart.loaded() then Chart.FMT_DURATION else ""), Align = Alignment.CENTER, Position = Position.TrimTop(100.0f).SliceTop(60.0f))
        |+ Text((fun () -> if SelectedChart.loaded() then Chart.FMT_BPM else ""), Align = Alignment.RIGHT, Position = Position.TrimTop(100.0f).SliceTop(60.0f))
        |+ Text((fun () -> if SelectedChart.loaded() then getModString(rate.Value, selectedMods.Value, false) else ""), Align = Alignment.LEFT, Position = Position.TrimTop(160.0f).SliceTop(40.0f))
        |+ Text((fun () -> if SelectedChart.loaded() then Chart.FMT_NOTECOUNTS.Value else ""), Align = Alignment.RIGHT, Position = Position.TrimTop(160.0f).SliceTop(40.0f))
        |+ Text((fun () -> if SelectedChart.loaded() || SelectedChart.chart.IsNone then "" else L"lobby.missing_chart"), Align = Alignment.CENTER, Position = Position.TrimTop(100.0f).SliceTop(60.0f))

        |+ Clickable(
            fun () -> 
                if 
                    Network.lobby.IsSome
                    && Network.lobby.Value.YouAreHost 
                then Screen.change Screen.Type.LevelSelect Transitions.Flags.Default
            , Position = Position.SliceTop(100.0f)
        )
        
        |+ Conditional(
            (fun () -> 
                Network.lobby.IsSome
                && SelectedChart.loaded()
                && not Network.lobby.Value.GameInProgress
                && Network.lobby.Value.ReadyStatus = ReadyFlag.NotReady),

            StylishButton(
                (fun () -> Network.lobby.Value.Spectate <- not Network.lobby.Value.Spectate),
                (fun () -> 
                    if Network.lobby.Value.Spectate then sprintf "%s %s" Icons.preview (L"lobby.spectator")
                    else sprintf "%s %s" Icons.play (L"lobby.player")),
                !%Palette.MAIN_100,
                Position = { Position.SliceBottom(50.0f) with Right = 0.5f %- 25.0f })
            )

        |+ Conditional(
            (fun () -> Network.lobby.IsSome && SelectedChart.loaded() && Network.lobby.Value.GameInProgress),
                StylishButton(
                (fun () ->
                    let username = Network.lobby.Value.Players.Keys.First(fun p -> Network.lobby.Value.Players.[p].Status = LobbyPlayerStatus.Playing)
                    Screen.changeNew 
                        (fun () -> SpectateScreen.spectate_screen username)
                        Screen.Type.Replay
                        Transitions.Flags.Default
                ),
                K (sprintf "%s %s" Icons.preview (L"lobby.spectate")),
                !%Palette.DARK_100,
                TiltRight = false,
                Position = { Position.SliceBottom(50.0f) with Left = 0.5f %- 0.0f })
            )
        
        |+ Conditional(
            (fun () -> Network.lobby.IsSome && SelectedChart.loaded() && not Network.lobby.Value.GameInProgress),

            StylishButton(
                (fun () -> 
                    Network.lobby.Value.ReadyStatus <-
                        match Network.lobby.Value.ReadyStatus with
                        | ReadyFlag.NotReady -> if Network.lobby.Value.Spectate then ReadyFlag.Spectate else ReadyFlag.Play
                        | _ -> ReadyFlag.NotReady
                    Lobby.set_ready Network.lobby.Value.ReadyStatus
                ),
                (fun () -> 
                    match Network.lobby with 
                    | Some l -> 
                        match l.ReadyStatus with
                        | ReadyFlag.NotReady -> 
                            if Network.lobby.Value.Spectate then 
                                sprintf "%s %s" Icons.preview (L"lobby.ready") 
                            else 
                                sprintf "%s %s" Icons.ready (L"lobby.ready")
                        | _ -> sprintf "%s %s" Icons.not_ready (L"lobby.not_ready")
                    | None -> "!"),
                !%Palette.DARK_100,
                TiltRight = false,
                Position = { Position.SliceBottom(50.0f) with Left = 0.5f %- 0.0f })
            )

        |* Conditional(
            (fun () -> 
                Network.lobby.IsSome
                && Network.lobby.Value.YouAreHost
                && Network.lobby.Value.ReadyStatus <> ReadyFlag.NotReady
                && not Network.lobby.Value.GameInProgress),

            StylishButton(
                (fun () -> if Network.lobby.Value.Countdown then Lobby.cancel_round() else Lobby.start_round()),
                (fun () -> 
                    if Network.lobby.Value.Countdown then sprintf "%s %s" Icons.connection_failed (L"lobby.cancel_game")
                    else sprintf "%s %s" Icons.play (L"lobby.start_game")),
                !%Palette.MAIN_100,
                Position = { Position.SliceBottom(50.0f) with Right = 0.5f %- 25.0f }
            )
        )

        SelectedChart.switch Network.lobby.Value.Chart
        Network.Events.join_lobby.Add(fun () -> SelectedChart.switch None)
        Network.Events.change_chart.Add(fun () -> if Screen.currentType = Screen.Type.Lobby then SelectedChart.switch Network.lobby.Value.Chart) // todo: not always

        base.Init parent

    override this.Draw() =
        Draw.rect (this.Bounds.SliceTop(70.0f)) (if SelectedChart.loaded() then (!*Palette.DARK).O4a 180 else Color.FromArgb(180, 100, 100, 100))
        Draw.rect (this.Bounds.SliceTop(100.0f).SliceBottom(30.0f)) (if SelectedChart.loaded() then (!*Palette.DARKER).O4a 180 else Color.FromArgb(180, 50, 50, 50))
        Draw.rect (this.Bounds.SliceTop(100.0f).SliceLeft(5.0f)) (if SelectedChart.loaded() then !*Palette.MAIN else Colors.white)

        base.Draw()