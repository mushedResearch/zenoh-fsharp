module ConfigTests

open Expecto
open Zenoh

[<Tests>]
let tests =
    testList
        "config"
        [
            test "settings are applied over the defaults" {
                let json =
                    Config.defaults
                    |> Config.mode Client
                    |> Config.connect [ "tcp/127.0.0.1:7447" ]
                    |> Config.toJson5

                Expect.stringContains json "\"mode\":\"client\"" "mode"
                Expect.stringContains json "127.0.0.1:7447" "connect endpoint"
            }

            test "later settings override earlier ones" {
                let json = Config.defaults |> Config.mode Client |> Config.mode Router |> Config.toJson5
                Expect.stringContains json "\"mode\":\"router\"" "last mode wins"
            }

            test "a JSON5 base is parsed" {
                let json = Config.ofJson5 "{ mode: 'client', }" |> Config.toJson5
                Expect.stringContains json "\"mode\":\"client\"" "mode from JSON5"
            }

            test "an unknown key raises ZenohException" {
                Expect.throwsT<ZenohException> (fun () -> Config.defaults |> Config.set "no/such/key" "1" |> Config.toJson5 |> ignore) "unknown key"
            }
        ]
