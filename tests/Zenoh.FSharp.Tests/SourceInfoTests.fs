module SourceInfoTests

open System
open System.Threading
open Expecto
open Zenoh
open TestUtil

[<Tests>]
let tests =
    testSequenced
    <| testList
        "entity ids, source info and cancellation"
        [
            test "entity ids name the session and its declarations" {
                use session = Session.Open(isolated ())
                use publisher = session.DeclarePublisher "test/ids/p"
                use subscriber = session.DeclareSubscriber("test/ids/s", ignore)
                use queryable = session.DeclareQueryable("test/ids/q", ignore)
                use querier = session.DeclareQuerier "test/ids/q"

                let ids = [ publisher.EntityId; subscriber.EntityId; queryable.EntityId; querier.EntityId ]
                Expect.equal session.EntityId.Zid session.Id "session zid"
                Expect.allEqual (ids |> List.map (fun i -> i.Zid)) session.Id "same session"
                Expect.equal (ids |> List.distinctBy (fun i -> i.Eid) |> List.length) 4 "distinct entity ids"
                Expect.equal publisher.EntityId publisher.EntityId "structural equality"
            }

            test "source info set by a publisher reaches the subscriber" {
                use session = Session.Open(isolated ())
                let received = signal ()
                use _sub = session.DeclareSubscriber("test/si", fun s -> received.TrySetResult s |> ignore)
                use publisher = session.DeclarePublisher "test/si"
                let info = { Source = publisher.EntityId; SequenceNumber = 7u }
                publisher.Put("x", sourceInfo = info)
                Expect.equal (await received.Task).SourceInfo (Some info) "source info"
            }

            test "samples without source info have None" {
                use session = Session.Open(isolated ())
                let received = signal ()
                use _sub = session.DeclareSubscriber("test/nosi", fun s -> received.TrySetResult s |> ignore)
                session.Put("test/nosi", "x")
                Expect.isNone (await received.Task).SourceInfo "no source info"
            }

            test "source info of queries and replies" {
                use session = Session.Open(isolated ())

                use queryable =
                    session.DeclareQueryable(
                        "test/siq",
                        fun q ->
                            let replyInfo = { Source = session.EntityId; SequenceNumber = 2u }
                            let seen = q.SourceInfo |> Option.map (fun i -> string i.SequenceNumber) |> Option.defaultValue "none"
                            q.Reply("test/siq", Text.Encoding.UTF8.GetBytes seen, sourceInfo = replyInfo)
                    )

                let queryInfo = { Source = session.EntityId; SequenceNumber = 1u }

                match await (session.Get("test/siq", sourceInfo = queryInfo, timeout = timeout)) with
                | [ ReplyOk s ] ->
                    Expect.equal s.PayloadString "1" "query source info seen by the queryable"
                    Expect.equal (s.SourceInfo |> Option.map (fun i -> i.SequenceNumber)) (Some 2u) "reply source info"
                | other -> failtestf "unexpected replies %A" other
            }

            test "cancelling a token cancels a pending get" {
                use session = Session.Open(isolated ())
                let kept = Collections.Concurrent.ConcurrentBag<IDisposable>()
                // The queryable never answers: the get would wait for its timeout.
                use _q = session.DeclareQueryable("test/cancel", fun q -> kept.Add(q.Detach() :> IDisposable))
                use cts = new CancellationTokenSource()

                let reader =
                    session.GetStream("test/cancel", timeout = TimeSpan.FromSeconds 60.0, cancellationToken = cts.Token)

                eventually "query received" (fun () -> not kept.IsEmpty)
                cts.Cancel()
                Expect.isTrue (reader.Completion.Wait timeout) "zenoh ended the get"

                for q in kept do
                    q.Dispose()
            }

            test "Get with a cancelled token is cancelled" {
                use session = Session.Open(isolated ())
                use querier = session.DeclareQuerier("test/cancel2", timeout = TimeSpan.FromSeconds 60.0)
                use cts = new CancellationTokenSource()
                cts.Cancel()
                let t = querier.Get(cancellationToken = cts.Token)
                Expect.throws (fun () -> t.Wait timeout |> ignore) "cancelled"
                Expect.isTrue t.IsCanceled "task cancelled"
            }

            test "a liveliness get can be cancelled" {
                use session = Session.Open(isolated ())
                use cts = new CancellationTokenSource()

                let reader =
                    session.LivelinessGetStream("test/cancel3/**", timeout = TimeSpan.FromSeconds 60.0, cancellationToken = cts.Token)

                cts.Cancel()
                Expect.isTrue (reader.Completion.Wait timeout) "ended"
            }
        ]
