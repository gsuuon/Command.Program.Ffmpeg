module Command.Program.Ffmpeg

open System.Drawing
open Gsuuon.Command
open Gsuuon.Console.Log
open Gsuuon.Console.Style


type Dshow =
    | Audio of deviceName: string
    // | Video of deviceName: string

type GdigrabRegion = {
    width : int
    height : int
    offsetX : int
    offsetY : int
}

type GdigrabFrame =
    | Region of GdigrabRegion
    | Window of windowName : string
    | Desktop

type Gdigrab = {
    framerate : int
    frame : GdigrabFrame
}

type FfmpegOption =
    | Dshow of Dshow
    | Gdigrab of Gdigrab

let dshowOptionToArg =
    function
    | Audio name -> $"""-f dshow -i audio='{name}'"""

let gdigrabOptionToArg  gdigrab =
    match gdigrab.frame with
    | Desktop -> $"-f gdigrab -framerate {gdigrab.framerate} -i desktop"
    | Region region ->
        $"-f gdigrab -framerate {gdigrab.framerate} "
        + $"-offset_x {region.offsetX} -offset_y {region.offsetY} "
        + $"-video_size {region.width}x{region.height} -show_region 1 "
        + "-i desktop"
    | Window name ->
        $"-f gdigrab -framerate {gdigrab.framerate} -i title={name}"

let toArg =
    function
    | Dshow dshow -> dshowOptionToArg dshow
    | Gdigrab gdigrab -> gdigrabOptionToArg gdigrab

module Dshow =
    open System.Text.RegularExpressions

    let listDevices () =
        proc "ffmpeg" "-list_devices true -f dshow -i dummy" |> wait <!> Stderr
        |> readBlock
        |> fun s -> s.Split "\n"
        |> Seq.choose (fun l ->
            let m = Regex.Match(l, """dshow.+] "(?<name>.+)" \((?<type>.+)\)""")

            if m.Success then
                let name = m.Groups["name"].Value
                let typ = m.Groups["type"].Value

                Some {|
                    name = name
                    ``type`` = typ
                |}

            else
                None
        )
        |> Seq.toList

module Preset =
    module Gdigrab =
        let small () = """-filter_complex "scale=-2:800:flags=lanczos" -c:v libx264 -preset veryfast -crf 23 -pix_fmt yuv420p -movflags +faststart -y """

        let twitch ingestUrl twitchKey =
            $"-c:v libx264 -preset veryfast -maxrate 3000k -bufsize 6000k -pix_fmt yuv420p -g 60 -c:a aac -b:a 128k -f flv {ingestUrl}/{twitchKey}"

let ffmpeg (options: FfmpegOption list) =
    let args = options |> List.map toArg |> String.concat " "

    let p = proc "ffmpeg" args

    p <!> Stderr |> consume (slogn [fg Color.Red])
    p <!> Stdout |> consume logn
    p |> wait
