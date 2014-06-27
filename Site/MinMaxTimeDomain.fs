namespace Site

open IntelliFactory.WebSharper
open IntelliFactory.WebSharper.JQuery
open IntelliFactory.WebSharper.Html
open IntelliFactory.WebSharper.Html5
open IntelliFactory.WebSharper.JavaScript

[<JavaScript>]
module MinMaxTimeDomain =
    let context = new AudioContext ()
    let javascriptNode = context.CreateScriptProcessor (1024u, 1u, 1u)

    let ToList (from : Uint8Array) =
        let rec helper n res =
            if n <= -1 then res
            else helper (n - 1) (from.Get(n)::res)

        helper (from.Length - 1) []

    let Canvas = HTML5.Tags.Canvas [ Width "800"; Height "256"; Attr.Style "background-color: black;" ]

    let mutable column = 0.

    let DrawTimeDomain (ctx : CanvasRenderingContext2D) (array : Uint8Array) =
        let c = JQuery.Of(Canvas.Dom)
        let width = float <| c.Width()
        let height = float <| c.Height()

        let minValue = ref 9999999.
        let maxValue = ref 0.

        ToList array
        |> List.iteri (fun i a -> 
                            let value = (float a) / 256.
                            if value > !maxValue then
                                maxValue := value
                            if value < !minValue then
                                minValue := value
                      )

        let max = height - (height * !maxValue) - 1.
        let min = height - (height * !minValue) - 1.

        ctx.FillStyle <- "#ffffff"                                            
        ctx.FillRect(column, min, 1. ,max - min)

        column <- column + 1.
        if column >= width then
            column <- 0.
            ctx.ClearRect(0., 0., width, height);

    let Analyser (stream : MediaStream) =
        let analyser = context.CreateAnalyser ()
        analyser.SmoothingTimeConstant <- 0.
        analyser.FftSize <- 1024u

        //Workaroud for a bug in Chrome which makes the GC destroy 
        //the ScriptProcessorNode if it's not in global scope
        JavaScript.Global?sourceNode <- javascriptNode

        javascriptNode.Connect(context.Destination)
        javascriptNode.Onaudioprocess <- fun e ->
                                            let array = new Uint8Array(int(analyser.FrequencyBinCount))
                                            analyser.GetByteTimeDomainData array
                                            let ctx = (As<CanvasElement> Canvas.Dom).GetContext("2d")
                                            DrawTimeDomain ctx array

        let streamNode = context.CreateMediaStreamSource(stream)
        streamNode.Connect(analyser)
        streamNode.Connect(context.Destination)

        AudioHolder.SetCurrent streamNode javascriptNode

        analyser.Connect(javascriptNode)

    let um = new UserMedia()

    let LoadSound (onNotSupported : MediaStreamError -> unit) =
        um.GetUserMedia(MediaStreamConstraints(false, true), 
                               (fun stream ->
                                    Analyser stream
                               ), onNotSupported)

    let Main (elem : Dom.Element) =
        AudioHolder.StopCurrent ()

        let error = Div []
        if not (As um.GetUserMedia) then
            error.Text <- "Your browser does not support this feature!"                                                           
            JQuery.Of(elem).Append(error.Dom) |> ignore
        else
            JQuery.Of(elem).Append(Canvas.Dom) |> ignore
            LoadSound <| fun e ->
                            Canvas.SetCss("display", "none")
                            error.Text <- "No microphone was found!"
                            JQuery.Of(elem).Append(error.Dom) |> ignore

    let Sample =
        Samples.Build()
            .Id("MinMaxTimeDomain")
            .FileName(__SOURCE_FILE__)
            .Keywords(["webrtc"; "visualizer"; "time domain"; "microphone"])
            .Render(Main)
            .Create()
 