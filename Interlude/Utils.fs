﻿namespace Interlude

open System.Reflection
open System.Diagnostics
open System.IO

module Utils =
    let version =
        let v = Assembly.GetExecutingAssembly().GetName()
        let v2 = Assembly.GetExecutingAssembly().Location |> FileVersionInfo.GetVersionInfo
        sprintf "%s %s (%s)" v.Name (v.Version.ToString(3)) v2.ProductVersion

    let K x _ = x

    let getResourceStream name =
        Assembly.GetExecutingAssembly().GetManifestResourceStream("Interlude.Resources." + name)

    let randomSplash(name) =
        let r = new System.Random()
        use s = getResourceStream(name)
        use tr = new StreamReader(s)
        let lines = tr.ReadToEnd().Split("\n")

        fun () -> lines.[r.Next(lines.Length)]

    let idPrint x = printfn "%A" x; x

    module AutoUpdate =
        open System.IO.Compression
        open Percyqaz.Json
        open Prelude.Common
        open Prelude.Web

        let rec copyFolder source dest =
            Directory.EnumerateFiles source
            |> Seq.iter
                (fun s ->
                    let fDest = Path.Combine(dest, Path.GetFileName(s))
                    try
                        File.Copy(s, fDest, true)
                    with
                    | :? IOException as err ->
                        File.Move(fDest, s + ".old", true)
                        File.Copy(s, fDest, true)
                )
            Directory.EnumerateDirectories source
            |> Seq.iter (fun d -> copyFolder d (Path.Combine(dest, Path.GetFileName(d))))

        [<Json.AllRequired>]
        type GithubAsset = {
            name: string
            browser_download_url: string
        } with static member Default = { name = ""; browser_download_url = "" }
        
        [<Json.AllRequired>]
        type GithubRelease = {
            url: string
            tag_name: string
            name: string
            published_at: string
            body: string
            assets: GithubAsset list
        } with static member Default = { name = ""; url = ""; tag_name = ""; published_at = ""; body = ""; assets = [] }

        let mutable latestRelease = None
        let mutable updateAvailable = false

        let handleUpdate(release: GithubRelease) =
            latestRelease <- Some release

            let current = Assembly.GetExecutingAssembly().GetName().Version.ToString(3)
            let incoming = release.tag_name.Substring(1)

            if incoming > current then Logging.Info(sprintf "Update available (%s)!" incoming)""; updateAvailable <- true
            elif incoming < current then Logging.Debug(sprintf "Current build (%s) is ahead of update stream (%s)." current incoming)""
            else Logging.Info("Game is up to date.")""

        let checkForUpdates() =
            BackgroundTask.Create TaskFlags.HIDDEN "Checking for updates"
                (fun output -> downloadJson("https://api.github.com/repos/percyqaz/YAVSRG/releases/latest", fun (d: GithubRelease) -> handleUpdate(d)))
            |> ignore

            let path = Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName
            let folderPath = Path.Combine(path, "update")
            if Directory.Exists(folderPath) then Directory.Delete(folderPath, true)

        //call this only if updateAvailable = true
        let applyUpdate(callback) =
            let download_url = latestRelease.Value.assets.Head.browser_download_url
            let path = Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName
            let zipPath = Path.Combine(path, "update.zip")
            let folderPath = Path.Combine(path, "update")
            File.Delete(zipPath)
            if Directory.Exists(folderPath) then Directory.Delete(folderPath, true)
            BackgroundTask.Create TaskFlags.NONE ("Downloading update " + latestRelease.Value.tag_name)
                ((downloadFile(download_url, zipPath))
                |> BackgroundTask.Callback
                    (fun b ->
                        if b then
                            ZipFile.ExtractToDirectory(zipPath, folderPath)
                            File.Delete(zipPath)
                            copyFolder folderPath path
                            callback()
                    ))
            |> ignore