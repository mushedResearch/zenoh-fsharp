module QuerierTests

open System
open Expecto
open Zenoh
open TestUtil

[<Tests>]
let tests =
    testSequenced
    <| testList
        "querier and matching"
        [
            test "a querier gets the replies of a queryable" {
                use session = Session.Open(isolated ())
                use _q = session.DeclareQueryable("test/querier/**", fun q -> q.Reply("test/querier/a", $"p={q.Parameters}"))
                use querier = session.DeclareQuerier("test/querier/**", timeout = timeout)
                Expect.equal querier.KeyExpr "test/querier/**" "querier key"

                for i in 1..3 do
                    match await (querier.Get(parameters = $"n={i}")) with
                    | [ ReplyOk s ] -> Expect.equal s.PayloadString $"p=n={i}" $"reply {i}"
                    | other -> failtestf "unexpected replies %A" other
            }

            test "querier matching status follows queryables" {
                use session = Session.Open(isolated ())
                use querier = session.DeclareQuerier "test/qm"
                Expect.isFalse querier.MatchingStatus "no queryable yet"
                let changes = Collections.Concurrent.ConcurrentQueue<bool>()
                use _listener = querier.DeclareMatchingListener changes.Enqueue

                let queryable = session.DeclareQueryable("test/qm", fun q -> q.Reply("test/qm", "x"))
                eventually "matching" (fun () -> querier.MatchingStatus)
                (queryable :> IDisposable).Dispose()
                eventually "not matching" (fun () -> not querier.MatchingStatus)
                eventually "listener calls" (fun () -> List.ofSeq changes = [ true; false ])
            }

            test "publisher matching status follows subscribers" {
                use session = Session.Open(isolated ())
                use publisher = session.DeclarePublisher "test/pm"
                Expect.isFalse publisher.MatchingStatus "no subscriber yet"
                let matched = signal ()
                use _listener = publisher.DeclareMatchingListener(fun m -> if m then matched.TrySetResult true |> ignore)
                use _sub = session.DeclareSubscriber("test/pm", ignore)
                Expect.isTrue (await matched.Task) "listener called"
                Expect.isTrue publisher.MatchingStatus "matching"
            }
        ]
