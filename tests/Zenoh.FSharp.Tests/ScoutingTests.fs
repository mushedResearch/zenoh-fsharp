module ScoutingTests

open System
open Expecto
open Zenoh
open TestUtil

[<Tests>]
let tests =
    testSequenced
    <| testList
        "scouting"
        [
            test "multicast scouting finds a peer" {
                // A peer with multicast scouting on (the default), listening on a known endpoint.
                let endpoint = $"tcp/127.0.0.1:{freePort ()}"
                use session = Session.Open(Config.defaults |> Config.mode Peer |> Config.listen [ endpoint ])

                let hellos =
                    await (Scouting.Scout(what = WhatAmI.Peer, timeout = TimeSpan.FromSeconds 3.0))

                match hellos |> List.tryFind (fun h -> h.Zid = session.Id) with
                | Some hello ->
                    Expect.equal hello.WhatAmI WhatAmI.Peer "kind"
                    Expect.contains hello.Locators endpoint "locator"
                | None -> failtestf "session %s not found in %A" session.Id hellos
            }
        ]
