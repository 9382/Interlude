﻿namespace Interlude.UI

open System
open System.Drawing
open System.Collections.Generic
open OpenTK
open Prelude.Common
open Interlude.Options
open Interlude.Render
open Interlude
open Interlude.Utils
open Interlude.Input
open OpenTK.Windowing.GraphicsLibraryFramework
open Interlude.Options
open Interlude.UI.Animation
open Interlude.UI.Components

module OptionsMenu =
    
    (*
        Fancy selection framework
    *)

    type Selectable() =
        inherit Widget()

        (*
            - There is only one item hovered per widget
            - This one item can be marked as selected
            Invariants:
                - (1) Only at most 1 leaf can be hovered
                - (2) Selected leaves are a subset, so at most 1 leaf can be selected
                - (3) The leaf that is hovered, if it exists, implies all its ancestors are selected
                - (4) The leaf that is hovered, if it exists, implies all its non-ancestors are not selected
        *) 

        let mutable hoverChild: Selectable option = None
        let mutable hoverSelected: bool = false

        member this.SelectedChild
            with get() = if hoverSelected then hoverChild else None
            and set(value) =
                match value with
                | Some v ->
                    match this.SelectedChild with
                    | Some c ->
                        if v <> c then
                            c.OnDeselect()
                            c.OnDehover()
                            hoverChild <- value
                            v.OnSelect()
                    | None ->
                        hoverChild <- value
                        hoverSelected <- true
                        match this.Parent with
                        | Some p ->
                            match p with
                            | :? Selectable as p -> p.SelectedChild <- Some this
                            | _ -> ()
                        | None -> ()
                        v.OnSelect()
                | None -> this.HoverChild <- None

        member this.HoverChild
            with get() = hoverChild
            and set(value) =
                match this.HoverChild with
                | Some c ->
                    if hoverSelected then c.OnDeselect()
                    if Some c <> value then c.OnDehover()
                | None -> ()
                hoverChild <- value
                hoverSelected <- false
                if value.IsSome then
                    match this.Parent with
                    | Some p ->
                        match p with
                        | :? Selectable as p -> p.SelectedChild <- Some this
                        | _ -> ()
                    | None -> ()

        member this.Selected
            with get() =
                match this.Parent with
                | Some p ->
                    match p with
                    | :? Selectable as p -> p.SelectedChild = Some this
                    | _ -> true
                | None -> true
            and set(value) =
                match this.Parent with
                | Some p ->
                    match p with
                    | :? Selectable as p ->
                        if value then p.SelectedChild <- Some this elif this.Hover then p.HoverChild <- Some this
                    | _ -> ()
                | None -> ()

        member this.Hover
            with get() =
                match this.Parent with
                | Some p ->
                    match p with
                    | :? Selectable as p -> p.HoverChild = Some this
                    | _ -> true
                | None -> true
            and set(value) =
                match this.Parent with
                | Some p ->
                    match p with
                    | :? Selectable as p ->
                        if value then p.HoverChild <- Some this elif this.Hover then p.HoverChild <- None
                    | _ -> ()
                | None -> ()

        abstract member OnSelect: unit -> unit
        default this.OnSelect() = ()

        abstract member OnDeselect: unit -> unit
        default this.OnDeselect() = ()

        abstract member OnDehover: unit -> unit
        default this.OnDehover() = this.HoverChild <- None

    type NavigateSelectable() =
        inherit Selectable()

        let mutable disposed = false

        abstract member Left: unit -> unit
        default this.Left() = ()

        abstract member Up: unit -> unit
        default this.Up() = ()

        abstract member Right: unit -> unit
        default this.Right() = ()

        abstract member Down: unit -> unit
        default this.Down() = ()

        override this.Update(elapsedTime, bounds) =
            base.Update(elapsedTime, bounds)
            if not disposed && this.Selected && this.SelectedChild.IsNone then
                if options.Hotkeys.Previous.Get().Tapped() then this.Left()
                elif options.Hotkeys.Up.Get().Tapped() then this.Up()
                elif options.Hotkeys.Next.Get().Tapped() then this.Right()
                elif options.Hotkeys.Down.Get().Tapped() then this.Down()
                elif options.Hotkeys.Select.Get().Tapped() then this.SelectedChild <- this.HoverChild
                elif options.Hotkeys.Exit.Get().Tapped() then this.Selected <- false

        override this.Dispose() =
            base.Dispose()
            disposed <- true

    type ListSelectable(horizontal) =
        inherit NavigateSelectable()

        let items = ResizeArray<Selectable>()
        let mutable lastHover = None

        member this.Previous() =
            match this.HoverChild with
            | None -> failwith "impossible"
            | Some w -> let i = (items.IndexOf(w) - 1 + items.Count) % items.Count in this.HoverChild <- Some items.[i]

        member this.Next() =
            match this.HoverChild with
            | None -> failwith "impossible"
            | Some w -> let i = (items.IndexOf(w) + 1) % items.Count in this.HoverChild <- Some items.[i]

        override this.Add(c) =
            base.Add(c)
            match c with
            | :? Selectable as c -> items.Add(c)
            | _ -> ()

        override this.OnSelect() = base.OnSelect(); if lastHover.IsNone then this.HoverChild <- Some items.[0] else this.HoverChild <- lastHover
        override this.OnDeselect() = lastHover <- this.HoverChild; this.HoverChild <- None
        override this.OnDehover() = base.OnDehover(); for i in items do i.OnDehover()

        override this.Left() = if horizontal then this.Previous()
        override this.Right() = if horizontal then this.Next()
        override this.Up() = if not horizontal then this.Previous()
        override this.Down() = if not horizontal then this.Next()


    (*
        Specific widgets to actually build options screen
    *)

    type BigButton(label, onClick) as this =
        inherit Selectable()

        do
            this.Add(Frame((fun () -> Screens.accentShade(180, 0.9f, 0.0f)), (fun () -> if this.Hover then Color.White else Color.Transparent)))
            this.Add(TextBox(K label, K (Color.White, Color.Black), 0.5f) |> positionWidget(0.0f, 0.0f, 0.0f, 0.6f, 0.0f, 1.0f, 0.0f, 0.8f))
            this.Add(Clickable((fun () -> this.Selected <- true), fun b -> if b then this.Hover <- true))

        override this.OnSelect() =
            this.Selected <- false
            onClick()

    type Selector(items: string array, index, setter) as this =
        inherit NavigateSelectable()
        let mutable index = index
        let fd() =
            index <- ((index + 1) % items.Length)
            setter(index, items.[index])
        let bk() =
            index <- ((index + items.Length - 1) % items.Length)
            setter(index, items.[index])
        do
            this.Add(new TextBox((fun () -> items.[index]), K (Color.White, Color.Black), 0.0f))
            this.Reposition(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 100.0f, 0.0f)
            this.Add(new Clickable((fun () -> (if not this.Selected then this.Selected <- true); fd()), fun b -> if b then this.Hover <- true))

        override this.Left() = bk()
        override this.Down() = bk()
        override this.Up() = fd()
        override this.Right() = fd()

        static member FromEnum<'U, 'T when 'T: enum<'U>>(setting: ISettable<'T>, onDeselect) =
            let names = Enum.GetNames(typeof<'T>)
            let values = Enum.GetValues(typeof<'T>) :?> 'T array
            { new Selector(names, Array.IndexOf(values, setting.Get()), (fun (i, _) -> setting.Set(values.[i])))
                with override this.OnDeselect() = base.OnDeselect(); onDeselect() }

        static member FromBool(setting: ISettable<bool>) =
            new Selector([|"NO" ; "YES"|], (if setting.Get() then 1 else 0), (fun (i, _) -> setting.Set(i > 0)))

        static member FromKeymode(setting: ISettable<int>) =
            new Selector([|"3K"; "4K"; "5K"; "6K"; "7K"; "8K"; "9K"; "10K"|], setting.Get(), (fun (i, _) -> setting.Set(i)))

    type Slider<'T when 'T : comparison>(setting: NumSetting<'T>, incr: float32) as this =
        inherit NavigateSelectable()
        let TEXTWIDTH = 130.0f
        let color = AnimationFade(0.5f)
        let mutable dragging = false
        let chPercent(v) = setting.SetPercent(setting.GetPercent() + v)
        do
            this.Animation.Add(color)
            this.Add(new TextBox((fun () -> setting.Get().ToString()), (fun () -> Color.White, Color.Black), 0.0f) |> positionWidget(0.0f, 0.0f, 0.0f, 0.0f, TEXTWIDTH, 0.0f, 0.0f, 1.0f))
            this.Reposition(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 100.0f, 0.0f)
            this.Add(new Clickable((fun () -> this.Selected <- true; dragging <- true), fun b -> color.Target <- if b then this.Hover <- true; 0.8f else 0.5f))

        override this.Update(elapsedTime, bounds) =
            base.Update(elapsedTime, bounds)
            let struct (l, t, r, b) = this.Bounds |> Rect.trimLeft(TEXTWIDTH)
            if this.Selected then
                if (Mouse.Held(MouseButton.Left) && dragging) then
                    let amt = (Mouse.X() - l) / (r - l)
                    setting.SetPercent(amt)
                else dragging <- false

        override this.Left() = chPercent(-incr)
        override this.Up() = chPercent(incr * 10.0f)
        override this.Right() = chPercent(incr)
        override this.Down() = chPercent(-incr * 10.0f)

        override this.Draw() =
            let v = setting.GetPercent()
            let struct (l, t, r, b) = this.Bounds |> Rect.trimLeft(TEXTWIDTH)
            let cursor = Rect.create (l + (r - l) * v) t (l + (r - l) * v) b |> Rect.expand(10.0f, -10.0f)
            let m = (b + t) * 0.5f
            Draw.rect (Rect.create l (m - 10.0f) r (m + 10.0f)) (Screens.accentShade(255, 1.0f, 0.0f)) Sprite.Default
            Draw.rect cursor (Screens.accentShade(255, 1.0f, color.Value)) Sprite.Default
            base.Draw()

    let localise s = Localisation.localise("options.name." + s)
    let row xs =
        let r = ListSelectable(true)
        List.iter r.Add xs; r
    let column xs =
        let c = ListSelectable(false)
        List.iter c.Add xs; c
    let wrapper main =
        let mutable disposed = false
        let w = { new Selectable() with
            override this.Update(elapsedTime, bounds) =
                if disposed then this.HoverChild <- None
                base.Update(elapsedTime, bounds)
                if not disposed then
                    Input.absorbAll()
            override this.Dispose() = (base.Dispose(); disposed <- true) }
        w.Add(main)
        w.SelectedChild <- Some main
        w

open OptionsMenu

module OptionsMenuTabs =

    let PRETTYTEXTWIDTH = 500.0f
    let PRETTYHEIGHT = 80.0f
    let PRETTYWIDTH = 1200.0f

    type PrettySetting(name, widget: Selectable) as this =
        inherit Selectable()
        do
            this.Add(widget |> positionWidgetA(PRETTYTEXTWIDTH, 0.0f, 0.0f, 0.0f))
            this.Add(TextBox(K (localise name + ":"), (fun () -> ((if this.Selected then Screens.accentShade(255, 1.0f, 0.2f) else Color.White), Color.Black)), 0.0f))
    
        member this.Position(y, width, height) =
            this |> positionWidget(100.0f, 0.0f, y, 0.0f, 100.0f + width, 0.0f, y + height, 0.0f)
    
        member this.Position(y, width) = this.Position(y, width, PRETTYHEIGHT)
        member this.Position(y) = this.Position(y, PRETTYWIDTH)

        override this.Draw() =
            if this.Selected then Draw.rect this.Bounds (Color.FromArgb(180, 0, 0, 0)) Sprite.Default
            elif this.Hover then Draw.rect this.Bounds (Color.FromArgb(80, 0, 0, 0)) Sprite.Default
            base.Draw()
    
        override this.Update(elapsedTime, bounds) =
            base.Update(elapsedTime, bounds)
            if widget.Hover && not widget.Selected && this.Selected then this.HoverChild <- None; this.Hover <- true
        
        override this.OnSelect() = if not widget.Hover then widget.Selected <- true
        override this.OnDehover() = base.OnDehover(); widget.OnDehover()

    type PrettyButton(name, action) as this =
        inherit Selectable()
        do
            this.Add(TextBox(K (localise name + " >"), (fun () -> ((if this.Hover then Screens.accentShade(255, 1.0f, 0.5f) else Color.White), Color.Black)), 0.0f))
            this.Add(Clickable((fun () -> this.Selected <- true), (fun b -> if b then this.Hover <- true)))
        override this.OnSelect() = action(); this.Selected <- false
        member this.Position(y) = this |> positionWidget(100.0f, 0.0f, y, 0.0f, 100.0f + PRETTYWIDTH, 0.0f, y + PRETTYHEIGHT, 0.0f)
    
    let system() =
        column [
            PrettySetting(
                "AudioOffset",
                { new Slider<float>(options.AudioOffset, 0.01f)
                    with override this.OnDeselect() = Audio.globalOffset <- float32 (options.AudioOffset.Get()) * 1.0f<ms> }
            ).Position(200.0f)
            
            PrettySetting(
                "AudioVolume",
                { new Slider<float>(options.AudioVolume, 0.01f)
                    with override this.OnDeselect() = Audio.changeVolume(options.AudioVolume.Get()) }
            ).Position(300.0f)

            PrettySetting(
                "WindowMode",
                Selector.FromEnum(config.WindowMode, Options.applyOptions)
            ).Position(400.0f)
            //todo: way to edit resolution settings?

            PrettySetting(
                "FrameLimiter",
                { new Selector(
                    [|"UNLIMITED"; "30"; "60"; "90"; "120"; "240"|],
                    int(config.FrameLimiter.Get() / 30.0) |> min(5),
                    (let e = [|0.0; 30.0; 60.0; 90.0; 120.0; 240.0|] in fun (i, _) -> config.FrameLimiter.Set(e.[i])) )
                    with override this.OnDeselect() = base.OnDeselect(); Options.applyOptions() }
            ).Position(500.0f)
        ]

    let themes() =
        //theme selection
        //noteskin selection
        //note color selection
        failwith "nyi"

    let gameplay(add) =
        column [
            PrettySetting("ScrollSpeed",
                Slider(options.ScrollSpeed :?> FloatSetting, 0.005f)
            ).Position(200.0f)
            
            PrettySetting("HitPosition",
                Slider(options.HitPosition :?> IntSetting, 0.005f)
            ).Position(300.0f)

            PrettySetting("Upscroll",
                Selector.FromBool(options.Upscroll)
            ).Position(400.0f)

            PrettySetting("BackgroundDim",
                Slider(options.BackgroundDim :?> FloatSetting, 0.01f)
            ).Position(500.0f)

            PrettyButton("ScreenCover", 
                fun() ->
                    //todo: preview of what screencover looks like
                    add("ScreenCover",
                        column [
                            PrettySetting("ScreenCoverUp",
                                Slider(options.ScreenCoverUp :?> FloatSetting, 0.01f)
                            ).Position(200.0f)

                            PrettySetting("ScreenCoverDown",
                                Slider(options.ScreenCoverDown :?> FloatSetting, 0.01f)
                            ).Position(300.0f)
                            
                            PrettySetting("ScreenCoverFadeLength",
                                Slider(options.ScreenCoverFadeLength :?> IntSetting, 0.01f)
                            ).Position(400.0f)
                        ] )
            ).Position(600.0f)
            //pacemaker
            //accuracy and hp systems
        ]

open OptionsMenuTabs

type OptionsMenu() as this =
    inherit Dialog()

    let stack: Selectable option array = Array.create 10 None
    let mutable namestack = []
    let mutable name = ""
    let body = Widget()

    let add(label, widget) =
        let n = (List.length namestack)
        namestack <- localise label :: namestack
        name <- String.Join(" > ", List.rev namestack)
        let w = wrapper widget
        match stack.[n] with
        | None -> ()
        | Some x -> x.Destroy()
        stack.[n] <- Some w
        body.Add(w)
        let n = float32 n + 1.0f
        w.Reposition(0.0f, Render.vheight * n, 0.0f, Render.vheight * n)
        body.Move(0.0f, -Render.vheight * n, 0.0f, -Render.vheight * n)

    let back() =
        namestack <- List.tail namestack
        name <- String.Join(" > ", List.rev namestack)
        let n = List.length namestack
        stack.[n].Value.Dispose()
        let n = float32 n
        body.Move(0.0f, -Render.vheight * n, 0.0f, -Render.vheight * n)

    let main =
        row [
            BigButton(localise "System", fun () -> add("System", system())) |> positionWidget(-790.0f, 0.5f, -150.0f, 0.5f, -490.0f, 0.5f, 150.0f, 0.5f);
            BigButton(localise "Themes", ignore) |> positionWidget(-470.0f, 0.5f, -150.0f, 0.5f, -170.0f, 0.5f, 150.0f, 0.5f);
            BigButton(localise "Gameplay", fun () -> add("Gameplay", gameplay(add))) |> positionWidget(-150.0f, 0.5f, -150.0f, 0.5f, 150.0f, 0.5f, 150.0f, 0.5f);
            BigButton(localise "Keybinds", ignore) |> positionWidget(170.0f, 0.5f, -150.0f, 0.5f, 470.0f, 0.5f, 150.0f, 0.5f);
            BigButton(localise "Debug", ignore) |> positionWidget(490.0f, 0.5f, -150.0f, 0.5f, 790.0f, 0.5f, 150.0f, 0.5f);
        ]

    do
        this.Add(body)
        this.Add(
            TextBox((fun () -> name), K (Color.White, Color.Black), 0.0f)
            |> positionWidget(20.0f, 0.0f, 20.0f, 0.0f, 0.0f, 1.0f, 100.0f, 0.0f))
        add("Options", main)

    override this.Update(elapsedTime, bounds) =
        base.Update(elapsedTime, bounds)
        match List.length namestack with
        | 0 -> this.Close()
        | n -> if stack.[n - 1].Value.SelectedChild.IsNone then back()

    override this.OnClose() = ()

