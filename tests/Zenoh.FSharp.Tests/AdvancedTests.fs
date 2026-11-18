module AdvancedTests

open System
open Expecto
open Zenoh
open TestUtil

[<Tests>]
let tests =
    testSequenced
    <| testList
        "zenoh-ext advanced pub/sub"
        [
            test "a late subscriber reads the history of a publisher cache" {
                // A cache without sample miss detection orders samples by timestamp.
                let a, b = connectedPairWith (Config.timestamping true)
                use a = a
                use b = b

                use publisher =
                    b.DeclareAdvancedPublisher("test/adv/a", cache = 10, publisherDetection = true)

                Expect.equal publisher.KeyExpr "test/adv/a" "publisher key"

                for i in 1..3 do
                    publisher.Put $"v{i}"

                let received = Collections.Concurrent.ConcurrentQueue<string>()

                use _sub =
                    a.DeclareAdvancedSubscriber(
                        "test/adv/**",
                        (fun s -> received.Enqueue s.PayloadString),
                        history = true,
                        detectLatePublishers = true
                    )

                eventually "history" (fun () -> received.Count >= 3)
                Expect.equal (List.ofSeq received) [ "v1"; "v2"; "v3" ] "cached samples in order"

                publisher.Put "v4"
                eventually "live sample" (fun () -> received.Count = 4)
            }

            test "publisher detection and sample miss listener" {
                use session = Session.Open(isolated ())
                use subscriber = session.DeclareAdvancedSubscriber("test/det/**", ignore, recovery = true)
                use _misses = subscriber.DeclareSampleMissListener(fun m -> failtestf "unexpected miss %A" m)
                let detected = signal ()

                use _detect =
                    subscriber.DetectPublishers(fun s -> if s.Kind = SampleKind.Put then detected.TrySetResult s.KeyExpr |> ignore)

                use publisher =
                    session.DeclareAdvancedPublisher(
                        "test/det/p",
                        cache = 1,
                        sampleMissDetection = Periodic(TimeSpan.FromMilliseconds 100.0),
                        publisherDetection = true
                    )

                Expect.stringStarts (await detected.Task) "test/det/p" "publisher detected"
                publisher.Put "x"
            }
        ]
