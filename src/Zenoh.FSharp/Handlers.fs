namespace Zenoh

// Building blocks shared by the declarations: ownership of native values, values moved into
// zenoh-c calls, and the closures that turn zenoh-c callbacks into F# handlers and channels.

open System
open System.Threading
open System.Threading.Channels
open System.Threading.Tasks
open Microsoft.FSharp.NativeInterop
open Zenoh.Native

#nowarn "9"
#nowarn "51"

/// Owner of a zenoh-c value in native memory: release runs once, on the first Dispose.
type internal Owned<'T when 'T: unmanaged>(name: string, ptr: nativeptr<'T>, release: nativeptr<'T> -> unit) =
    let mutable disposed = 0

    member _.Ptr =
        if Volatile.Read &disposed = 1 then raise (ObjectDisposedException name)
        ptr

    member _.IsDisposed = Volatile.Read &disposed = 1

    /// Give the value to `move` (a zenoh-c call that takes ownership), then free the memory.
    member _.Move(move: nativeptr<'T> -> 'R) : 'R =
        if Interlocked.Exchange(&disposed, 1) = 1 then raise (ObjectDisposedException name)

        try
            move ptr
        finally
            Ffi.free ptr

    member _.Dispose() =
        if Interlocked.Exchange(&disposed, 1) = 0 then
            try
                release ptr
            finally
                Ffi.free ptr

/// Builds the owned values that a zenoh-c call moves (payload, encoding, attachment).
module internal Moved =
    /// A payload writer that copies data.
    let bytes (data: byte[]) =
        fun (dst: nativeptr<z_owned_bytes_t>) -> Ffi.writeBytes (ReadOnlySpan data) dst

    /// Create the values, then call `call payload encoding attachment`; an absent value is a null
    /// pointer. The zenoh-c call must be the last thing `call` does: it moves the values, and
    /// when anything throws before it, the values created are dropped here.
    let call
        (operation: string)
        (payload: (nativeptr<z_owned_bytes_t> -> unit) option)
        (encoding: string option)
        (attachment: byte[] option)
        (call: nativeptr<z_owned_bytes_t> -> nativeptr<z_owned_encoding_t> -> nativeptr<z_owned_bytes_t> -> z_result_t)
        =
        let mutable enc = Unchecked.defaultof<z_owned_encoding_t>
        let mutable att = Unchecked.defaultof<z_owned_bytes_t>
        let mutable pl = Unchecked.defaultof<z_owned_bytes_t>
        let pEnc = &&enc
        let pAtt = &&att
        let pPl = &&pl
        let mutable hasEnc = false
        let mutable hasAtt = false
        let mutable hasPl = false

        let rc =
            try
                match encoding with
                | Some e ->
                    Ffi.writeEncoding e pEnc
                    hasEnc <- true
                | None -> ()

                match attachment with
                | Some a ->
                    Ffi.writeBytes (ReadOnlySpan a) pAtt
                    hasAtt <- true
                | None -> ()

                match payload with
                | Some write ->
                    write pPl
                    hasPl <- true
                | None -> ()

                call
                    (if hasPl then pPl else NativePtr.nullPtr)
                    (if hasEnc then pEnc else NativePtr.nullPtr)
                    (if hasAtt then pAtt else NativePtr.nullPtr)
            with _ ->
                if hasEnc then Interop.z_encoding_drop pEnc
                if hasAtt then Interop.z_bytes_drop pAtt
                if hasPl then Interop.z_bytes_drop pPl
                reraise ()

        Ffi.check operation rc

/// A zenoh cancellation token cancelled by a .NET CancellationToken, for one get.
///
/// zenoh-c gets a clone of the token (CopyInto) and cancelling blocks while a reply callback
/// runs. The token is freed by whoever finishes last: the end of the get (Release, called from
/// the drop of the reply closure) or a cancellation in progress. Neither waits for the other,
/// so a cancellation and the end of the get cannot deadlock.
type internal GetCancellation(ct: CancellationToken) =
    let token =
        let p = Ffi.alloc<z_owned_cancellation_token_t> ()
        let rc = Interop.z_cancellation_token_new p

        if rc <> Result.Z_OK then
            Ffi.free p
            Ffi.check "z_cancellation_token_new" rc

        p

    let gate = obj ()
    let mutable cancelling = false
    let mutable released = false
    let mutable registration = Unchecked.defaultof<CancellationTokenRegistration>

    let free () =
        Interop.z_cancellation_token_drop token
        Ffi.free token

    let cancel () =
        let proceed =
            lock gate (fun () ->
                if released then
                    false
                else
                    cancelling <- true
                    true)

        if proceed then
            Interop.z_cancellation_token_cancel (Interop.z_cancellation_token_loan_mut token) |> ignore

            lock gate (fun () ->
                cancelling <- false
                if released then free ())

    /// Write a clone of the token into dst (a value the get options move into zenoh-c).
    member _.CopyInto(dst: nativeptr<z_owned_cancellation_token_t>) =
        Interop.z_cancellation_token_clone (dst, Interop.z_cancellation_token_loan_mut token)
        NativePtr.toNativeInt dst

    /// Cancel the token when ct is cancelled (at once if it already is).
    member _.Start() = registration <- ct.Register(fun () -> cancel ())

    /// The get ended (or never started): stop listening to ct and free the token.
    member _.Release() =
        lock gate (fun () ->
            if not released then
                released <- true
                registration.Unregister() |> ignore
                if not cancelling then free ())

module internal Handlers =
    let cbool (b: bool) = if b then 1uy else 0uy

    let timeoutMs (t: TimeSpan) = uint64 (max 1.0 t.TotalMilliseconds)

    /// Allocate native memory for an owned value and initialise it with declare; frees the
    /// memory when declare throws.
    let declareOwned<'T when 'T: unmanaged> (declare: nativeptr<'T> -> unit) : nativeptr<'T> =
        let owned = Ffi.alloc<'T> ()

        try
            declare owned
            owned
        with _ ->
            Ffi.free owned
            reraise ()

    /// Initialise a closure with a zenoh-c closure constructor and pass it to call, which moves
    /// it into zenoh-c (zenoh-c drops it, and so frees the target, also when the call fails).
    let withClosure
        (ctor: nativeptr<z_owned_closure_t> * nativeint * nativeint * nativeint -> unit)
        (target: Closures.Target)
        (call: nativeptr<z_owned_closure_t> -> z_result_t)
        =
        let mutable closure = Unchecked.defaultof<z_owned_closure_t>
        let p = &&closure
        Closures.create ctor target p
        call p

    let sampleCallback (handler: Sample -> unit) =
        Closures.Target((fun p -> handler (SampleReader.read (NativePtr.ofNativeInt p))), ignore)

    /// With a capacity, the channel keeps the most recent samples and drops the oldest.
    let sampleChannel (capacity: int option) =
        let channel =
            match capacity with
            | Some c ->
                Channel.CreateBounded<Sample>(
                    BoundedChannelOptions(c, FullMode = BoundedChannelFullMode.DropOldest, SingleWriter = false)
                )
            | None -> Channel.CreateUnbounded<Sample>()

        let target =
            Closures.Target(
                (fun p -> channel.Writer.TryWrite(SampleReader.read (NativePtr.ofNativeInt p)) |> ignore),
                (fun () -> channel.Writer.TryComplete() |> ignore)
            )

        target, channel.Reader

    /// Send a get. `send target cancellation` makes the zenoh-c call with the reply closure
    /// target; for its cancellation token option it calls `cancelToken cancellation storage`.
    /// Replies go to the returned channel, which completes when zenoh-c drops the closure (end
    /// of the get). A CancellationToken that can be cancelled cancels the get.
    let get
        (cancellationToken: CancellationToken option)
        (send: Closures.Target -> GetCancellation option -> unit)
        : ChannelReader<Reply> =
        let channel = Channel.CreateUnbounded<Reply>()
        let isCancelled () = cancellationToken |> Option.exists (fun ct -> ct.IsCancellationRequested)

        // An already cancelled get is not sent: zenoh-c would refuse it (logging an error).
        if isCancelled () then
            channel.Writer.TryComplete() |> ignore
        else
            let cancellation =
                cancellationToken
                |> Option.filter (fun ct -> ct.CanBeCanceled)
                |> Option.map (fun ct -> new GetCancellation(ct))

            let target =
                Closures.Target(
                    (fun p -> channel.Writer.TryWrite(SampleReader.readReply (NativePtr.ofNativeInt p)) |> ignore),
                    (fun () ->
                        cancellation |> Option.iter (fun c -> c.Release())
                        channel.Writer.TryComplete() |> ignore)
                )

            cancellation |> Option.iter (fun c -> c.Start())

            try
                send target cancellation
            with _ ->
                // Release is idempotent: also right when zenoh-c already dropped the closure.
                cancellation |> Option.iter (fun c -> c.Release())

                // Cancelled while it was sent: zenoh-c refused it; the get is cancelled, not failed.
                if isCancelled () then channel.Writer.TryComplete() |> ignore else reraise ()

        channel.Reader

    /// The value of a cancellation_token option: a clone of the token in storage, or 0.
    let cancelToken (cancellation: GetCancellation option) (storage: nativeptr<z_owned_cancellation_token_t>) =
        match cancellation with
        | Some c -> c.CopyInto storage
        | None -> 0n

    let matchingCallback (handler: bool -> unit) =
        Closures.Target(
            (fun p ->
                let status: z_matching_status_t = NativePtr.read (NativePtr.ofNativeInt p)
                handler (status.matching <> 0uy)),
            ignore
        )

    /// Read a channel to its end.
    let collect (reader: ChannelReader<'T>) (ct: CancellationToken) : Task<'T list> =
        task {
            let items = ResizeArray()
            let e = reader.ReadAllAsync(ct).GetAsyncEnumerator(ct)
            let mutable more = true

            while more do
                let! next = e.MoveNextAsync()

                if next then items.Add e.Current else more <- false

            return List.ofSeq items
        }
