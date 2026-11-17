module SessionTests

open System
open System.Text
open System.Threading.Tasks
open Expecto
open Zenoh
open TestUtil

[<Tests>]
let tests =
    testSequenced
    <| testList
        "session"
        [
            test "open, id, close" {
                use session = Session.Open(isolated ())
                Expect.isNotEmpty session.Id "session id"
                Expect.isFalse session.IsClosed "open"
                session.Close()
                Expect.isTrue session.IsClosed "closed"
            }

            test "a disposed session raises ObjectDisposedException" {
                let session = Session.Open(isolated ())
                (session :> IDisposable).Dispose()
                Expect.throwsT<ObjectDisposedException> (fun () -> session.Put("demo/a", "x")) "put after dispose"
            }

            test "a disposed publisher raises ObjectDisposedException" {
                use session = Session.Open(isolated ())
                let publisher = session.DeclarePublisher "test/disposed"
                (publisher :> IDisposable).Dispose()
                Expect.throwsT<ObjectDisposedException> (fun () -> publisher.Put("x", encoding = "text/plain")) "put"
            }

            test "put reaches a subscriber of the same session" {
                use session = Session.Open(isolated ())
                let received = signal ()

                use _sub = session.DeclareSubscriber("test/put/**", fun s -> received.TrySetResult s |> ignore)

                session.Put(
                    "test/put/one",
                    "hello",
                    encoding = "text/plain",
                    attachment = Encoding.UTF8.GetBytes "meta"
                )

                let s = await received.Task
                Expect.equal s.KeyExpr "test/put/one" "key"
                Expect.equal s.PayloadString "hello" "payload"
                Expect.equal s.Kind SampleKind.Put "kind"
                Expect.stringStarts s.Encoding "text/plain" "encoding"
                Expect.equal (s.Attachment |> Option.map Encoding.UTF8.GetString) (Some "meta") "attachment"
            }

            test "publisher put and delete arrive on a sample stream" {
                use session = Session.Open(isolated ())
                use stream = session.Subscribe "test/pub/*"
                use publisher = session.DeclarePublisher("test/pub/a", encoding = "application/json")
                Expect.equal publisher.KeyExpr "test/pub/a" "publisher key"

                publisher.Put "{\"x\":1}"
                publisher.Delete()

                match await (samples stream 2) with
                | [ put; delete ] ->
                    Expect.equal put.PayloadString "{\"x\":1}" "payload"
                    Expect.equal put.Encoding "application/json" "publisher encoding"
                    Expect.equal delete.Kind SampleKind.Delete "delete kind"
                | other -> failtestf "unexpected samples %A" other
            }

            test "disposing a sample stream completes its reader" {
                use session = Session.Open(isolated ())
                let stream = session.Subscribe "test/stream/**"
                (stream :> IDisposable).Dispose()
                Expect.isTrue (stream.Reader.Completion.Wait timeout) "reader completed"
            }

            test "get collects replies of a queryable" {
                use session = Session.Open(isolated ())

                use _q =
                    session.DeclareQueryable(
                        "test/q/**",
                        fun q ->
                            q.Reply("test/q/a", $"params={q.Parameters}")
                            q.Reply("test/q/b", "second")
                    )

                let replies = await (session.Get("test/q/**", parameters = "x=1", timeout = timeout))

                let payloads =
                    replies
                    |> List.map (function
                        | ReplyOk s -> s.PayloadString
                        | ReplyErr e -> failtestf "error reply %s" e.PayloadString)
                    |> List.sort

                Expect.equal payloads [ "params=x=1"; "second" ] "replies"
            }

            test "query payload and error replies" {
                use session = Session.Open(isolated ())

                use _q =
                    session.DeclareQueryable(
                        "test/err",
                        fun q ->
                            let body = q.Payload |> Option.map Encoding.UTF8.GetString |> Option.defaultValue "<none>"
                            q.ReplyErr($"rejected {body}", encoding = "text/plain")
                    )

                let replies =
                    await (session.Get("test/err", payload = Encoding.UTF8.GetBytes "req", timeout = timeout))

                match replies with
                | [ ReplyErr e ] ->
                    Expect.equal e.PayloadString "rejected req" "error payload"
                    Expect.stringStarts e.Encoding "text/plain" "error encoding"
                | other -> failtestf "unexpected replies %A" other
            }

            test "a detached query replies after the handler returns" {
                use session = Session.Open(isolated ())

                use _q =
                    session.DeclareQueryable(
                        "test/async",
                        fun q ->
                            let detached = q.Detach()

                            Task.Run(fun () ->
                                use d = detached
                                Threading.Thread.Sleep 100
                                d.Reply("test/async", "late"))
                            |> ignore
                    )

                match await (session.Get("test/async", timeout = timeout)) with
                | [ ReplyOk s ] -> Expect.equal s.PayloadString "late" "late reply"
                | other -> failtestf "unexpected replies %A" other
            }

            test "a borrowed query is invalid after the handler returns" {
                use session = Session.Open(isolated ())
                let kept = signal<Query> ()
                use _q = session.DeclareQueryable("test/kept", fun q -> kept.TrySetResult q |> ignore)
                session.Get("test/kept", timeout = TimeSpan.FromMilliseconds 500.0) |> ignore
                let q = await kept.Task
                eventually "handler return" (fun () ->
                    try
                        q.Reply("test/kept", "x")
                        false
                    with :? ObjectDisposedException ->
                        true)
            }

            test "two sessions talk over TCP" {
                let a, b = connectedPair ()
                use a = a
                use b = b
                Expect.contains a.Peers b.Id "a sees b"

                let received = signal ()
                use _sub = a.DeclareSubscriber("test/tcp", fun s -> received.TrySetResult s.PayloadString |> ignore)

                // Declarations propagate asynchronously: publish until the subscriber gets one.
                eventually "remote sample" (fun () ->
                    b.Put("test/tcp", "over the wire")
                    received.Task.Wait 200)

                Expect.equal received.Task.Result "over the wire" "payload from b"
            }

            test "timestamps are present when timestamping is enabled" {
                use session = Session.Open(isolated () |> Config.timestamping true)
                let received = signal ()
                use _sub = session.DeclareSubscriber("test/ts", fun s -> received.TrySetResult s |> ignore)
                session.Put("test/ts", "t")
                let s = await received.Task

                match s.Timestamp with
                | Some ts ->
                    let age = DateTimeOffset.UtcNow - ts.ToDateTimeOffset()
                    Expect.isLessThan (abs age.TotalSeconds) 60.0 "timestamp is close to now"
                | None -> failtest "no timestamp"
            }

            test "handler exceptions go to ZenohRuntime.CallbackError" {
                use session = Session.Open(isolated ())
                let reported = signal ()
                use _handler = ZenohRuntime.CallbackError.Subscribe(fun ex -> reported.TrySetResult ex.Message |> ignore)
                use _sub = session.DeclareSubscriber("test/throw", fun _ -> failwith "boom")
                session.Put("test/throw", "x")
                Expect.equal (await reported.Task) "boom" "reported exception"
            }

            test "invalid key expressions raise ArgumentException" {
                use session = Session.Open(isolated ())
                Expect.throwsT<ArgumentException> (fun () -> session.Put("bad//key", "x")) "put"
            }
        ]
