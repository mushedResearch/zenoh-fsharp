module TestUtil

// Helpers shared by the tests that open sessions.

open System
open System.Net
open System.Net.Sockets
open System.Threading.Tasks
open Expecto
open Zenoh

let timeout = TimeSpan.FromSeconds 10.0

/// A peer that only talks to the sessions it is told about.
let isolated () =
    Config.defaults
    |> Config.mode Peer
    |> Config.multicastScouting false
    |> Config.listen [ "tcp/127.0.0.1:0" ]

let freePort () =
    let l = new TcpListener(IPAddress.Loopback, 0)
    l.Start()
    let port = (l.LocalEndpoint :?> IPEndPoint).Port
    l.Stop()
    port

let await (t: Task<'T>) =
    if not (t.Wait timeout) then failtest "timed out"
    t.Result

let signal<'T> () =
    TaskCompletionSource<'T>(TaskCreationOptions.RunContinuationsAsynchronously)

/// Wait until condition holds, up to the test timeout.
let eventually (what: string) (condition: unit -> bool) =
    let deadline = DateTime.UtcNow + timeout

    while not (condition ()) do
        if DateTime.UtcNow > deadline then failtest $"timed out waiting for {what}"
        Threading.Thread.Sleep 50

/// Read count samples from a stream.
let samples (stream: SampleStream) count =
    task {
        let result = ResizeArray()

        while result.Count < count do
            let! s = stream.Reader.ReadAsync()
            result.Add s

        return List.ofSeq result
    }

/// A peer listening on endpoint, that only talks to the sessions it is told about.
let listening (endpoint: string) =
    Config.defaults |> Config.mode Peer |> Config.multicastScouting false |> Config.listen [ endpoint ]

/// A peer connected to endpoint, that does not listen.
let connectedTo (endpoint: string) =
    Config.defaults
    |> Config.mode Peer
    |> Config.multicastScouting false
    |> Config.listen []
    |> Config.connect [ endpoint ]

/// Two sessions connected over TCP on 127.0.0.1; configure changes both configurations.
let connectedPairWith (configure: Config -> Config) =
    let endpoint = $"tcp/127.0.0.1:{freePort ()}"
    let a = Session.Open(configure (listening endpoint))
    let b = Session.Open(configure (connectedTo endpoint))
    eventually "peer connection" (fun () -> b.Peers |> List.contains a.Id)
    a, b

let connectedPair () = connectedPairWith id
