﻿namespace Interlude

open System
open System.Collections.Generic
open System.IO
open OpenTK.Windowing.GraphicsLibraryFramework
open Prelude.Common
open Prelude.Scoring
open Prelude.Gameplay.Layout
open Prelude.Gameplay.NoteColors
open Prelude.Data.ScoreManager
open Interlude
open Interlude.Input
open Interlude.Input.Bind

module Options =

    type WindowType =
        | WINDOWED = 0
        | BORDERLESS = 1
        | FULLSCREEN = 2

    type WindowResolution =
        | Preset of index:int
        | Custom of width:int * height:int

    type GameConfig = {
        WorkingDirectory: string
        Locale: string
        WindowMode: Setting<WindowType>
        Resolution: Setting<WindowResolution>
        FrameLimiter: Setting<float>
    } with
        static member Default = {
            WorkingDirectory = ""
            Locale = "en_GB.txt"
            WindowMode = Setting.simple WindowType.BORDERLESS
            Resolution = Setting.simple (Custom (1024, 768))
            FrameLimiter = Setting.simple 0.0
        }

    type Hotkeys = {
        Exit: Setting<Bind>
        Select: Setting<Bind>
        Previous: Setting<Bind>
        Next: Setting<Bind>
        Up: Setting<Bind>
        Down: Setting<Bind>
        Start: Setting<Bind>
        End: Setting<Bind>

        Skip: Setting<Bind>
        Search: Setting<Bind>
        Toolbar: Setting<Bind>
        Tooltip: Setting<Bind>
        Delete: Setting<Bind>
        Screenshot: Setting<Bind>
        Volume: Setting<Bind>

        Collections: Setting<Bind>
        AddToCollection: Setting<Bind>
        RemoveFromCollection: Setting<Bind>
        Mods: Setting<Bind>
        Autoplay: Setting<Bind>
        ChartInfo: Setting<Bind>

        Import: Setting<Bind>
        Options: Setting<Bind>
        Help: Setting<Bind>
        Tasks: Setting<Bind>

        UpRate: Setting<Bind>
        DownRate: Setting<Bind>
        UpRateHalf: Setting<Bind>
        DownRateHalf: Setting<Bind>
        UpRateSmall: Setting<Bind>
        DownRateSmall: Setting<Bind>
    } with
        static member Default = {
            Exit = Setting.simple(mk Keys.Escape)
            Select = Setting.simple(mk Keys.Enter)
            Search = Setting.simple(mk Keys.Tab)
            Toolbar = Setting.simple(ctrl Keys.T)
            Tooltip = Setting.simple(mk Keys.Slash)
            Delete = Setting.simple(mk Keys.Delete)
            Screenshot = Setting.simple(mk Keys.F12)
            Volume = Setting.simple(mk Keys.LeftAlt)
            Previous = Setting.simple(mk Keys.Left)
            Next = Setting.simple(mk Keys.Right)
            Up = Setting.simple(mk Keys.Up)
            Down = Setting.simple(mk Keys.Down)
            Start = Setting.simple(mk Keys.Home)
            End = Setting.simple(mk Keys.End)
            Skip = Setting.simple(mk Keys.Space)

            Collections = Setting.simple(mk Keys.N)
            AddToCollection = Setting.simple(mk Keys.RightBracket)
            RemoveFromCollection = Setting.simple(mk Keys.LeftBracket)
            Mods = Setting.simple(mk Keys.M)
            Autoplay = Setting.simple(ctrl Keys.A)
            ChartInfo = Setting.simple(mk Keys.Period)

            Import = Setting.simple(ctrl Keys.I)
            Options = Setting.simple(ctrl Keys.O)
            Help = Setting.simple(ctrl Keys.H)
            Tasks = Setting.simple(mk Keys.F8)

            UpRate = Setting.simple(mk Keys.Equal)
            DownRate = Setting.simple(mk Keys.Minus)
            UpRateHalf = Setting.simple(ctrl Keys.Equal)
            DownRateHalf = Setting.simple(ctrl Keys.Minus)
            UpRateSmall = Setting.simple(shift Keys.Equal)
            DownRateSmall = Setting.simple(shift Keys.Minus)
        }

    type Keymode =
        | ``3K`` = 3
        | ``4K`` = 4
        | ``5K`` = 5
        | ``6K`` = 6
        | ``7K`` = 7
        | ``8K`` = 8
        | ``9K`` = 9
        | ``10K`` = 10

    type ScoreSaving =
        | Always = 0
        | Pacemaker = 1
        | PersonalBest = 2

    type Pacemaker =
        | Accuracy of float
        | Lamp of Lamp

    type FailType =
        | Instant = 0
        | EndOfSong = 1

    type WatcherSelection<'T> = 'T * 'T list
    module WatcherSelection =
        let cycleForward (main, alts) =
            match alts with
            | [] -> (main, alts)
            | x :: xs -> (x, xs @ [main])

        let rec cycleBackward (main, alts) =
            match alts with
            | [] -> (main, alts)
            | x :: xs -> let (m, a) = cycleBackward (x, xs) in (m, main :: a)

    type GameOptions = {
        AudioOffset: Setting.Bounded<float>
        AudioVolume: Setting.Bounded<float>
        CurrentChart: Setting<string>
        EnabledThemes: List<string>

        ScrollSpeed: Setting.Bounded<float>
        HitPosition: Setting.Bounded<int>
        HitLighting: Setting<bool>
        Upscroll: Setting<bool>
        BackgroundDim: Setting.Bounded<float>
        PerspectiveTilt: Setting.Bounded<float>
        ScreenCoverUp: Setting.Bounded<float>
        ScreenCoverDown: Setting.Bounded<float>
        ScreenCoverFadeLength: Setting.Bounded<int>
        ColorStyle: Setting<ColorConfig>
        KeymodePreference: Setting<Keymode>
        UseKeymodePreference: Setting<bool>
        NoteSkin: Setting<string>

        Playstyles: Layout array
        HPSystems: Setting<WatcherSelection<Metrics.HPSystemConfig>>
        AccSystems: Setting<WatcherSelection<Metrics.AccuracySystemConfig>>
        ScoreSaveCondition: Setting<ScoreSaving>
        FailCondition: Setting<FailType>
        Pacemaker: Setting<Pacemaker>
        GameplayBinds: (Bind array) array

        ChartSortMode: Setting<string>
        ChartGroupMode: Setting<string>
        //ChartColorMode: Setting<string>
        ScoreSortMode: Setting<int>

        Hotkeys: Hotkeys
    } with
        static member Default = {
            AudioOffset = Setting.bounded 0.0 -500.0 500.0 |> Setting.round 0
            AudioVolume = Setting.percent 0.1
            CurrentChart = Setting.simple ""
            EnabledThemes = new List<string>()

            ScrollSpeed = Setting.bounded 2.05 1.0 3.0 |> Setting.round 2
            HitPosition = Setting.bounded 0 -100 400
            HitLighting = Setting.simple false
            Upscroll = Setting.simple false
            BackgroundDim = Setting.percent 0.5
            PerspectiveTilt = Setting.bounded 0.0 -1.0 1.0 |> Setting.round 2
            ScreenCoverUp = Setting.percent 0.0
            ScreenCoverDown = Setting.percent 0.0
            ScreenCoverFadeLength = Setting.bounded 200 0 500
            NoteSkin = Setting.simple "default"
            //todo: move to noteskin
            ColorStyle = Setting.simple ColorConfig.Default
            KeymodePreference = Setting.simple Keymode.``4K``
            UseKeymodePreference = Setting.simple false

            Playstyles = [|Layout.OneHand; Layout.Spread; Layout.LeftOne; Layout.Spread; Layout.LeftOne; Layout.Spread; Layout.LeftOne; Layout.Spread|]
            HPSystems = Setting.simple (Metrics.VG, [])
            AccSystems = Setting.simple (Metrics.SCPlus (4, false), [])
            ScoreSaveCondition = Setting.simple ScoreSaving.Always
            FailCondition = Setting.simple FailType.EndOfSong
            Pacemaker = Setting.simple (Accuracy 0.95)

            ChartSortMode = Setting.simple "Title"
            ChartGroupMode = Setting.simple "Pack"
            ScoreSortMode = Setting.simple 0
            Hotkeys = Hotkeys.Default
            GameplayBinds = [|
                [|mk Keys.Left; mk Keys.Down; mk Keys.Right|];
                [|mk Keys.Z; mk Keys.X; mk Keys.Period; mk Keys.Slash|];
                [|mk Keys.Z; mk Keys.X; mk Keys.Space; mk Keys.Period; mk Keys.Slash|];
                [|mk Keys.Z; mk Keys.X; mk Keys.C; mk Keys.Comma; mk Keys.Period; mk Keys.Slash|];
                [|mk Keys.Z; mk Keys.X; mk Keys.C; mk Keys.Space; mk Keys.Comma; mk Keys.Period; mk Keys.Slash|];
                [|mk Keys.Z; mk Keys.X; mk Keys.C; mk Keys.V; mk Keys.Comma; mk Keys.Period; mk Keys.Slash; mk Keys.RightShift|];
                [|mk Keys.Z; mk Keys.X; mk Keys.C; mk Keys.V; mk Keys.Space; mk Keys.Comma; mk Keys.Period; mk Keys.Slash; mk Keys.RightShift|];
                [|mk Keys.CapsLock; mk Keys.Q; mk Keys.W; mk Keys.E; mk Keys.V; mk Keys.Space; mk Keys.K; mk Keys.L; mk Keys.Semicolon; mk Keys.Apostrophe|]
            |]
        }

    //forward ref for applying game config options. it is initialised in the constructor of Game
    let mutable applyOptions: unit -> unit = ignore

    let resolutions: (struct (int * int)) array =
        [|struct (800, 600); struct (1024, 768); struct (1280, 800); struct (1280, 1024); struct (1366, 768); struct (1600, 900);
            struct (1600, 1024); struct (1680, 1050); struct (1920, 1080); struct (2715, 1527)|]

    let getResolution res =
        match res with
        | Custom (w, h) -> (true, struct (w, h))
        | Preset i ->
            let i = System.Math.Clamp(i, 0, Array.length resolutions - 1)
            (false, resolutions.[i])

    let mutable internal config = GameConfig.Default
    let mutable internal options = GameOptions.Default
    let internal configPath = Path.GetFullPath("config.json")
    let firstLaunch = not (File.Exists configPath)

    let load() =
        config <- loadImportantJsonFile "Config" configPath config true
        Localisation.loadFile(config.Locale)
        if config.WorkingDirectory <> "" then Directory.SetCurrentDirectory(config.WorkingDirectory)
        options <- loadImportantJsonFile "Options" (Path.Combine(getDataPath("Data"), "options.json")) options true

        Themes.refreshAvailableThemes()
        Themes.loadThemes(options.EnabledThemes)
        Themes.changeNoteSkin(options.NoteSkin.Value)

    let save() =
        try
            JSON.ToFile(configPath, true) config
            JSON.ToFile(Path.Combine(getDataPath("Data"), "options.json"), true) options
        with err -> Logging.Critical("Failed to write options/config to file.", err)