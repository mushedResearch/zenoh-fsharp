module LivelinessTests

open System
open Expecto
open Zenoh
open TestUtil

[<Tests>]
let tests =
    testSequenced
    <| testList
        "liveliness"
        [
            test "a token is seen by a liveliness get" {
                use session = Session.Open(isolated ())
                use _token = session.DeclareLivelinessToken "test/alive/a"

                match await (session.LivelinessGet("test/alive/**", timeout = timeout)) with
                | [ ReplyOk s ] -> Expect.equal s.KeyExpr "test/alive/a" "token key"
                | other -> failtestf "unexpected replies %A" other
            }

            test "a subscriber sees a token appear and disappear" {
                let a, b = connectedPair ()
                use a = a
                use b = b
                use stream = a.SubscribeLiveliness "test/live/**"

                let token = b.DeclareLivelinessToken "test/live/b"
                let appeared = await (stream.Reader.ReadAsync().AsTask())
                Expect.equal (appeared.KeyExpr, appeared.Kind) ("test/live/b", SampleKind.Put) "appeared"

                (token :> IDisposable).Dispose()
                let gone = await (stream.Reader.ReadAsync().AsTask())
                Expect.equal (gone.KeyExpr, gone.Kind) ("test/live/b", SampleKind.Delete) "disappeared"
            }

            test "history reports the tokens alive at declaration" {
                use session = Session.Open(isolated ())
                use _token = session.DeclareLivelinessToken "test/hist/x"
                let seen = signal ()

                use _sub =
                    session.DeclareLivelinessSubscriber("test/hist/**", (fun s -> seen.TrySetResult s.KeyExpr |> ignore), history = true)

                Expect.equal (await seen.Task) "test/hist/x" "existing token"
            }
        ]
