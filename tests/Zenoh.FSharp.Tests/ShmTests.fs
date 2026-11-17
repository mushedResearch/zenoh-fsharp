module ShmTests

open System
open System.Text
open Expecto
open Zenoh
open TestUtil

[<Tests>]
let tests =
    testSequenced
    <| testList
        "shared memory"
        [
            test "a buffer is written, published and received" {
                use provider = new ShmProvider(1 <<< 20)
                let a, b = connectedPair ()
                use a = a
                use b = b
                let received = signal ()
                use _sub = a.DeclareSubscriber("test/shm", fun s -> received.TrySetResult s |> ignore)
                use publisher = b.DeclarePublisher "test/shm"
                eventually "matching" (fun () -> publisher.MatchingStatus)

                let buffer = provider.Alloc(ReadOnlySpan(Encoding.UTF8.GetBytes "shared"))
                Expect.equal buffer.Length 6 "buffer length"
                publisher.Put buffer

                let s = await received.Task
                Expect.equal s.PayloadString "shared" "payload"
                Expect.throwsT<ObjectDisposedException> (fun () -> buffer.Span.Length |> ignore) "buffer moved"
            }

            test "a query reply carries a buffer" {
                use provider = new ShmProvider(4096)
                use session = Session.Open(isolated ())
                use _q = session.DeclareQueryable("test/shmq", fun q -> q.Reply("test/shmq", provider.Alloc(ReadOnlySpan "r"B)))

                match await (session.Get("test/shmq", timeout = timeout)) with
                | [ ReplyOk s ] -> Expect.equal s.PayloadString "r" "reply payload"
                | other -> failtestf "unexpected replies %A" other
            }

            test "a disposed buffer returns its memory" {
                use provider = new ShmProvider(4096)
                let before = provider.Available
                let buffer = provider.Alloc 1024
                buffer.Span.Fill 7uy
                (buffer :> IDisposable).Dispose()
                provider.GarbageCollect() |> ignore
                Expect.equal provider.Available before "memory back in the pool"
            }

            test "an allocation larger than the pool raises ShmAllocException" {
                use provider = new ShmProvider(4096)
                Expect.throwsT<ShmAllocException> (fun () -> provider.Alloc(1 <<< 20) |> ignore) "too large"
            }
        ]
