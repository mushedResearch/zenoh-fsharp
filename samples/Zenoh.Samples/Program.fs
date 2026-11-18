// Command-line examples, compatible with the z_pub / z_sub / z_get / z_queryable examples of
// the other zenoh bindings:
//
//   dotnet run --project samples/Zenoh.Samples -- sub  [key]              [-e endpoint] [-l endpoint]
//   dotnet run --project samples/Zenoh.Samples -- pub  [key] [value]      [-e endpoint] [-l endpoint]
//   dotnet run --project samples/Zenoh.Samples -- get  [selector]         [-e endpoint] [-l endpoint]
//   dotnet run --project samples/Zenoh.Samples -- queryable [key] [value] [-e endpoint] [-l endpoint]
//
// -e connects to an endpoint (such as tcp/127.0.0.1:7447), -l listens on one; with either,
// multicast scouting is off. With neither, sessions find each other by multicast scouting.
open System
open System.Threading
open Zenoh

let usage () =
    eprintfn "usage: (sub|pub|get|queryable) [key] [value] [-e endpoint] [-l endpoint]"
    2

type Endpoints = { Connect: string list; Listen: string list }

let config (eps: Endpoints) =
    match eps with
    | { Connect = []; Listen = [] } -> Config.defaults
    | _ ->
        Config.defaults
        |> Config.multicastScouting false
        |> Config.connect eps.Connect
        |> (if eps.Listen.IsEmpty then id else Config.listen eps.Listen)

let rec parse (positional: string list) (eps: Endpoints) =
    function
    | "-e" :: ep :: rest -> parse positional { eps with Connect = eps.Connect @ [ ep ] } rest
    | "-l" :: ep :: rest -> parse positional { eps with Listen = eps.Listen @ [ ep ] } rest
    | arg :: rest -> parse (arg :: positional) eps rest
    | [] -> List.rev positional, eps

let waitForCtrlC () =
    use stop = new ManualResetEventSlim()
    Console.CancelKeyPress.Add(fun e ->
        e.Cancel <- true
        stop.Set())
    stop.Wait()

[<EntryPoint>]
let main argv =
    ZenohRuntime.InitLogging "error"

    match parse [] { Connect = []; Listen = [] } (List.ofArray argv) with
    | "sub" :: rest, eps ->
        let key = List.tryHead rest |> Option.defaultValue "demo/example/**"
        use session = Session.Open(config eps)
        printfn "Subscribing to '%s' (Ctrl-C to quit)" key

        use _sub =
            session.DeclareSubscriber(key, fun s -> printfn ">> [%A] '%s': '%s'" s.Kind s.KeyExpr s.PayloadString)

        waitForCtrlC ()
        0
    | "pub" :: rest, eps ->
        let key = List.tryItem 0 rest |> Option.defaultValue "demo/example/zenoh-fs-pub"
        let value = List.tryItem 1 rest |> Option.defaultValue "Pub from F#!"
        use session = Session.Open(config eps)
        use publisher = session.DeclarePublisher key
        printfn "Publishing on '%s' (Ctrl-C to quit)" key
        use stop = new CancellationTokenSource()
        Console.CancelKeyPress.Add(fun e ->
            e.Cancel <- true
            stop.Cancel())

        let mutable i = 0

        while not stop.IsCancellationRequested do
            let payload = $"[{i, 4}] {value}"
            printfn "<< '%s': '%s'" key payload
            publisher.Put payload
            i <- i + 1
            stop.Token.WaitHandle.WaitOne 1000 |> ignore

        0
    | "get" :: rest, eps ->
        let selector = List.tryHead rest |> Option.defaultValue "demo/example/**"
        let key, parameters =
            match selector.IndexOf '?' with
            | -1 -> selector, ""
            | i -> selector.Substring(0, i), selector.Substring(i + 1)

        use session = Session.Open(config eps)
        printfn "Sending query '%s'" selector

        for reply in session.Get(key, parameters = parameters).Result do
            match reply with
            | ReplyOk s -> printfn ">> '%s': '%s'" s.KeyExpr s.PayloadString
            | ReplyErr e -> printfn ">> error: '%s'" e.PayloadString

        0
    | "queryable" :: rest, eps ->
        let key = List.tryItem 0 rest |> Option.defaultValue "demo/example/zenoh-fs-queryable"
        let value = List.tryItem 1 rest |> Option.defaultValue "Queryable from F#!"
        use session = Session.Open(config eps)
        printfn "Declaring queryable on '%s' (Ctrl-C to quit)" key

        use _q =
            session.DeclareQueryable(
                key,
                fun q ->
                    printfn ">> query '%s?%s'" q.KeyExpr q.Parameters
                    q.Reply(key, value)
            )

        waitForCtrlC ()
        0
    | _ -> usage ()
