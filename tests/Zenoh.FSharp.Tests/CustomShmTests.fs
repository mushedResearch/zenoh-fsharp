module CustomShmTests

open System
open System.Text
open System.Threading
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Expecto
open Zenoh
open TestUtil

#nowarn "9"

/// A backend with 16 slots of 256 bytes in one segment; chunk id = slot index.
type SlotBackend() =
    let slotSize = 256
    let slots = 16
    let memory = NativeMemory.AlignedAlloc(unativeint (slotSize * slots), 4096un) |> NativePtr.ofVoidPtr<byte>
    let used = Array.zeroCreate<bool> slots
    let mutable allocs = 0
    let mutable frees = 0

    member _.Base = NativePtr.toNativeInt memory
    member _.SlotSize = slotSize
    member _.Allocs = Volatile.Read &allocs
    member _.Frees = Volatile.Read &frees
    member _.Release() = NativeMemory.AlignedFree(NativePtr.toVoidPtr memory)

    static member Protocol = 0x0F5_0001u
    static member Segment = 7u

    interface IShmProviderBackend with
        member _.ProtocolId = SlotBackend.Protocol

        member this.Alloc layout =
            lock used (fun () ->
                match Array.tryFindIndex not used with
                | Some i when layout.Size <= slotSize ->
                    used[i] <- true
                    Interlocked.Increment &allocs |> ignore

                    Some
                        {
                            Segment = SlotBackend.Segment
                            Chunk = uint32 i
                            Length = layout.Size
                            Address = this.Base + nativeint (i * slotSize)
                        }
                | _ -> None)

        member _.Free(_, chunk, _) =
            lock used (fun () -> used[int chunk] <- false)
            Interlocked.Increment &frees |> ignore

        member _.Defragment() = 0L

        member _.Available =
            lock used (fun () -> int64 (used |> Array.filter not |> Array.length) * int64 slotSize)

        member _.LayoutFor layout =
            if layout.Size <= slotSize && layout.Alignment <= slotSize then Some layout else None

/// The client of SlotBackend: maps chunk ids back to the memory of the backend.
type SlotClient(backend: SlotBackend) =
    let mutable attached = 0
    let mutable mapped = 0
    member _.Attached = Volatile.Read &attached
    member _.Mapped = Volatile.Read &mapped

    interface IShmClient with
        member _.ProtocolId = SlotBackend.Protocol

        member _.Attach segment =
            if segment <> SlotBackend.Segment then
                None
            else
                Interlocked.Increment &attached |> ignore

                Some
                    { new IShmSegment with
                        member _.Map chunk =
                            Interlocked.Increment &mapped |> ignore
                            backend.Base + nativeint (int chunk * backend.SlotSize)
                    }

let private address (buffer: ShmBuffer) =
    use p = fixed buffer.Span
    NativePtr.toNativeInt p

[<Tests>]
let tests =
    testSequenced
    <| testList
        "shared memory layouts and custom providers"
        [
            test "aligned allocations" {
                use provider = ShmProvider.Posix { Size = 1 <<< 16; Alignment = 64 }
                use buffer = provider.Alloc(128, alignment = 64)
                Expect.equal buffer.Length 128 "length"
                Expect.equal (address buffer % 64n) 0n "aligned on 64 bytes"
                Expect.throwsT<ArgumentException> (fun () -> provider.Alloc(9, alignment = 3) |> ignore) "not a power of two"
                Expect.throwsT<ArgumentException> (fun () -> provider.Alloc(100, alignment = 64) |> ignore) "size not a multiple"
            }

            test "an alignment above the one of the provider is refused" {
                use provider = new ShmProvider(1 <<< 16)
                Expect.throwsT<ShmAllocException> (fun () -> provider.Alloc(4096, alignment = 4096) |> ignore) "too aligned"
            }

            test "a precomputed layout allocates buffers of its size" {
                use provider = ShmProvider.Posix { Size = 1 <<< 16; Alignment = 16 }
                use layout = provider.Layout(128, alignment = 16)
                use a = layout.Alloc()
                use b = layout.Alloc()
                Expect.equal (a.Length, b.Length) (128, 128) "lengths"
                Expect.equal (address b % 16n) 0n "aligned"
            }

            test "a POSIX provider with a layout" {
                use provider = ShmProvider.Posix { Size = 4096; Alignment = 64 }
                use buffer = provider.Alloc 256
                Expect.equal (address buffer % 64n) 0n "aligned like the pool"
            }

            test "a custom backend allocates, and frees after use" {
                let backend = SlotBackend()

                try
                    use provider = ShmProvider.Custom backend
                    use session = Session.Open(isolated ())
                    let received = signal ()
                    use _sub = session.DeclareSubscriber("test/custom", fun s -> received.TrySetResult s.PayloadString |> ignore)

                    session.Put("test/custom", provider.Alloc(ReadOnlySpan(Encoding.UTF8.GetBytes "slot")))
                    Expect.equal (await received.Task) "slot" "payload"
                    Expect.equal backend.Allocs 1 "one allocation"
                    eventually "chunk freed" (fun () -> provider.GarbageCollect() |> ignore; backend.Frees = 1)
                    Expect.equal provider.Available (16L * 256L) "all slots free"
                finally
                    backend.Release()
            }

            test "a custom backend refuses a layout it cannot serve" {
                let backend = SlotBackend()

                try
                    use provider = ShmProvider.Custom backend
                    Expect.throwsT<ShmAllocException> (fun () -> provider.Alloc 1000 |> ignore) "too large for a slot"
                    Expect.equal backend.Allocs 0 "no allocation"
                finally
                    backend.Release()
            }

            test "a session with the custom client reads custom buffers from another session" {
                let backend = SlotBackend()
                let client = SlotClient backend

                try
                    use provider = ShmProvider.Custom backend
                    let endpoint = $"tcp/127.0.0.1:{freePort ()}"
                    use a = Session.Open(listening endpoint, shmClients = [ client ])
                    use b = Session.Open(connectedTo endpoint)
                    eventually "peer connection" (fun () -> b.Peers |> List.contains a.Id)

                    let received = signal ()
                    use _sub = a.DeclareSubscriber("test/custom/remote", fun s -> received.TrySetResult s.PayloadString |> ignore)
                    use publisher = b.DeclarePublisher "test/custom/remote"
                    eventually "matching" (fun () -> publisher.MatchingStatus)

                    publisher.Put(provider.Alloc(ReadOnlySpan(Encoding.UTF8.GetBytes "remote slot")))
                    Expect.equal (await received.Task) "remote slot" "payload"
                    Expect.isGreaterThan client.Attached 0 "segment attached by the client"
                    Expect.isGreaterThan client.Mapped 0 "chunk mapped by the client"
                finally
                    backend.Release()
            }
        ]
