namespace Zenoh

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Channels
open System.Threading.Tasks
open Microsoft.FSharp.NativeInterop
open Zenoh.Native

#nowarn "9"
#nowarn "51"

/// A listener of the matching status of a publisher or querier (whether some subscriber or
/// queryable matches it). Dispose undeclares it.
[<Sealed>]
type MatchingListener internal (owned: nativeptr<z_owned_matching_listener_t>) =
    let handle =
        Owned("MatchingListener", owned, fun p -> Interop.z_undeclare_matching_listener p |> ignore)

    interface IDisposable with
        member _.Dispose() = handle.Dispose()

module internal Matching =
    let status (operation: string) (get: nativeptr<z_matching_status_t> -> z_result_t) =
        let mutable status = Unchecked.defaultof<z_matching_status_t>
        let p = &&status
        Ffi.check operation (get p)
        status.matching <> 0uy

    /// Declare a matching listener with a declare function of zenoh-c.
    let listen
        (operation: string)
        (handler: bool -> unit)
        (declare: nativeptr<z_owned_matching_listener_t> * nativeptr<z_owned_closure_t> -> z_result_t)
        =
        let owned =
            Handlers.declareOwned (fun owned ->
                Handlers.withClosure Interop.z_closure_matching_status (Handlers.matchingCallback handler) (fun closure ->
                    declare (owned, closure))
                |> Ffi.check operation)

        new MatchingListener(owned)

/// A publisher declared on a key expression. Dispose undeclares it.
[<Sealed>]
type Publisher internal (owned: nativeptr<z_owned_publisher_t>) =
    let handle =
        Owned("Publisher", owned, fun p -> Interop.z_undeclare_publisher p |> ignore)

    let keyExpr =
        Ffi.keyExprToString (Interop.z_publisher_keyexpr (Interop.z_publisher_loan owned))

    let loan () = Interop.z_publisher_loan handle.Ptr

    let put payload encoding attachment sourceInfo =
        let publisher = loan ()

        Moved.call "z_publisher_put" (Some payload) encoding attachment (fun pl enc att ->
            SourceInfoNative.using sourceInfo (fun si ->
                let mutable o = Unchecked.defaultof<z_publisher_put_options_t>
                Interop.z_publisher_put_options_default &&o
                o.encoding <- enc
                o.attachment <- att
                o.source_info <- si
                Interop.z_publisher_put (publisher, pl, &&o)))

    member _.KeyExpr = keyExpr

    /// Global id of this publisher (for example the Source of a SourceInfo).
    member _.EntityId = EntityId(Interop.z_publisher_id (loan ()))

    /// Publish payload. encoding overrides the encoding of the publisher for this sample.
    member _.Put(payload: byte[], ?encoding: string, ?attachment: byte[], ?sourceInfo: SourceInfo) =
        put (Moved.bytes payload) encoding attachment sourceInfo

    /// Publish a UTF-8 string.
    member _.Put(payload: string, ?encoding: string, ?attachment: byte[], ?sourceInfo: SourceInfo) =
        put (Moved.bytes (Ffi.utf8 payload)) encoding attachment sourceInfo

    /// Publish a shared-memory buffer, without copying it. The buffer is moved into zenoh.
    member _.Put(payload: ShmBuffer, ?encoding: string, ?attachment: byte[], ?sourceInfo: SourceInfo) =
        put payload.WritePayload encoding attachment sourceInfo

    /// Publish a deletion of the key expression of the publisher.
    member _.Delete() =
        Ffi.check "z_publisher_delete" (Interop.z_publisher_delete (loan (), 0n))

    /// Whether some subscriber matches this publisher.
    member _.MatchingStatus =
        let publisher = loan ()
        Matching.status "z_publisher_get_matching_status" (fun s -> Interop.z_publisher_get_matching_status (publisher, s))

    /// Call handler with the matching status each time it changes.
    member _.DeclareMatchingListener(handler: bool -> unit) =
        let publisher = loan ()

        Matching.listen "z_publisher_declare_matching_listener" handler (fun (listener, closure) ->
            Interop.z_publisher_declare_matching_listener (publisher, listener, closure))

    interface IDisposable with
        member _.Dispose() = handle.Dispose()

/// A subscriber that calls a handler for each sample. Dispose undeclares it.
[<Sealed>]
type Subscriber internal (owned: nativeptr<z_owned_subscriber_t>, keyExpr: string) =
    let handle =
        Owned("Subscriber", owned, fun p -> Interop.z_undeclare_subscriber p |> ignore)

    member _.KeyExpr = keyExpr

    /// Global id of this subscriber.
    member _.EntityId = EntityId(Interop.z_subscriber_id (Interop.z_subscriber_loan handle.Ptr))

    interface IDisposable with
        member _.Dispose() = handle.Dispose()

/// A subscriber whose samples are read from a channel. Undeclaring it (Dispose) completes the channel.
[<Sealed>]
type SampleStream internal (subscription: IDisposable, keyExpr: string, reader: ChannelReader<Sample>) =
    member _.KeyExpr = keyExpr
    member _.Reader = reader

    interface IAsyncEnumerable<Sample> with
        member _.GetAsyncEnumerator(ct) =
            reader.ReadAllAsync(ct).GetAsyncEnumerator(ct)

    interface IDisposable with
        member _.Dispose() = subscription.Dispose()

/// A query received by a queryable.
///
/// The query passed to a queryable handler is valid until the handler returns. To reply after
/// that (for example from a task), call Detach in the handler and dispose the detached query
/// once all replies are sent: the get completes when every copy of the query is gone.
[<Sealed>]
type Query
    internal
    (
        loaned: nativeptr<z_loaned_query_t>,
        owned: nativeptr<z_owned_query_t>,
        keyExpr: string,
        parameters: string,
        payload: byte[] option,
        encoding: string option,
        attachment: byte[] option,
        sourceInfo: SourceInfo option
    ) =
    let mutable valid = true
    let mutable disposed = 0

    static member internal Read(loaned: nativeptr<z_loaned_query_t>) =
        let mutable view = Unchecked.defaultof<z_view_string_t>
        Interop.z_query_parameters (loaned, &&view)
        let enc = Interop.z_query_encoding loaned

        new Query(
            loaned,
            NativePtr.nullPtr,
            Ffi.keyExprToString (Interop.z_query_keyexpr loaned),
            Ffi.loanedString (Interop.z_view_string_loan &&view),
            Ffi.optionalBytes (Interop.z_query_payload loaned),
            (if NativePtr.isNullPtr enc then None else Some(Ffi.encodingToString enc)),
            Ffi.optionalBytes (Interop.z_query_attachment loaned),
            SourceInfoNative.read (Interop.z_query_source_info loaned)
        )

    member private _.Ptr =
        if not (Volatile.Read &valid) then
            raise (ObjectDisposedException("Query", "The query is no longer valid: call Detach to reply after the handler returns."))

        loaned

    member internal _.Invalidate() = Volatile.Write(&valid, false)

    member private this.ReplyWith
        (
            keyExpr: string,
            payload: nativeptr<z_owned_bytes_t> -> unit,
            encoding: string option,
            attachment: byte[] option,
            priority: Priority option,
            congestionControl: CongestionControl option,
            express: bool option,
            replySourceInfo: SourceInfo option
        ) =
        let query = this.Ptr

        Ffi.withKeyExpr keyExpr (fun ke ->
            Moved.call "z_query_reply" (Some payload) encoding attachment (fun pl enc att ->
                SourceInfoNative.using replySourceInfo (fun si ->
                    let mutable o = Unchecked.defaultof<z_query_reply_options_t>
                    Interop.z_query_reply_options_default &&o
                    o.encoding <- enc
                    o.attachment <- att
                    o.source_info <- si

                    match priority with
                    | Some p -> o.priority <- p
                    | None -> ()

                    match congestionControl with
                    | Some c -> o.congestion_control <- c
                    | None -> ()

                    match express with
                    | Some e -> o.is_express <- Handlers.cbool e
                    | None -> ()

                    Interop.z_query_reply (query, ke, pl, &&o))))

    /// Key expression of the query (may contain wildcards).
    member _.KeyExpr = keyExpr

    /// Selector parameters, such as "a=1;b=2" for the selector "key?a=1;b=2".
    member _.Parameters = parameters
    member _.Payload = payload
    member _.Encoding = encoding
    member _.Attachment = attachment

    /// Who sent the query, when the querier set it.
    member _.SourceInfo = sourceInfo

    /// Reply with a sample on keyExpr, which must intersect the key expression of the query.
    member this.Reply
        (
            keyExpr: string,
            payload: byte[],
            ?encoding: string,
            ?attachment: byte[],
            ?priority: Priority,
            ?congestionControl: CongestionControl,
            ?express: bool,
            ?sourceInfo: SourceInfo
        ) =
        this.ReplyWith(keyExpr, Moved.bytes payload, encoding, attachment, priority, congestionControl, express, sourceInfo)

    /// Reply with a UTF-8 string.
    member this.Reply(keyExpr: string, payload: string, ?encoding: string, ?attachment: byte[]) =
        this.ReplyWith(keyExpr, Moved.bytes (Ffi.utf8 payload), encoding, attachment, None, None, None, None)

    /// Reply with a shared-memory buffer, without copying it. The buffer is moved into zenoh.
    member this.Reply(keyExpr: string, payload: ShmBuffer, ?encoding: string, ?attachment: byte[]) =
        this.ReplyWith(keyExpr, payload.WritePayload, encoding, attachment, None, None, None, None)

    /// Reply with an error.
    member this.ReplyErr(payload: byte[], ?encoding: string) =
        let query = this.Ptr

        Moved.call "z_query_reply_err" (Some(Moved.bytes payload)) encoding None (fun pl enc _ ->
            let mutable o = Unchecked.defaultof<z_query_reply_err_options_t>
            o.encoding <- enc
            Interop.z_query_reply_err (query, pl, &&o))

    /// Reply with an error message in UTF-8.
    member this.ReplyErr(message: string, ?encoding: string) =
        this.ReplyErr(Ffi.utf8 message, ?encoding = encoding)

    /// Reply with a deletion of keyExpr.
    member this.ReplyDelete(keyExpr: string) =
        let query = this.Ptr

        Ffi.withKeyExpr keyExpr (fun ke ->
            Ffi.check "z_query_reply_del" (Interop.z_query_reply_del (query, ke, 0n)))

    /// A copy of the query that stays valid after the handler returns. Dispose it when done.
    member this.Detach() =
        let copy = Ffi.alloc<z_owned_query_t> ()
        Interop.z_query_clone (copy, this.Ptr)
        new Query(Interop.z_query_loan copy, copy, keyExpr, parameters, payload, encoding, attachment, sourceInfo)

    interface IDisposable with
        member _.Dispose() =
            if not (NativePtr.isNullPtr owned) && Interlocked.Exchange(&disposed, 1) = 0 then
                Volatile.Write(&valid, false)
                Interop.z_query_drop owned
                Ffi.free owned

/// A queryable that answers gets with a handler. Dispose undeclares it.
[<Sealed>]
type Queryable internal (owned: nativeptr<z_owned_queryable_t>, keyExpr: string) =
    let handle =
        Owned("Queryable", owned, fun p -> Interop.z_undeclare_queryable p |> ignore)

    member _.KeyExpr = keyExpr

    /// Global id of this queryable.
    member _.EntityId = EntityId(Interop.z_queryable_id (Interop.z_queryable_loan handle.Ptr))

    interface IDisposable with
        member _.Dispose() = handle.Dispose()

/// A zenoh session. Dispose closes it.
[<Sealed>]
type Session private (owned: nativeptr<z_owned_session_t>) =
    let handle =
        Owned(
            "Session",
            owned,
            fun p ->
                Interop.z_close (Interop.z_session_loan p, 0n) |> ignore
                Interop.z_session_drop p
        )

    let loan () = Interop.z_session_loan handle.Ptr

    let collectIds (info: nativeptr<z_loaned_session_t> * nativeptr<z_owned_closure_t> -> z_result_t) operation =
        let session = loan ()
        let ids = ResizeArray()

        let target =
            Closures.Target(
                (fun p ->
                    let id: z_id_t = NativePtr.read (NativePtr.ofNativeInt p)
                    lock ids (fun () -> ids.Add(Ffi.zidToString id))),
                ignore
            )

        Ffi.check operation (Handlers.withClosure Interop.z_closure_zid target (fun closure -> info (session, closure)))
        lock ids (fun () -> List.ofSeq ids)

    let declareSubscriber keyExpr target allowedOrigin =
        let session = loan ()

        Handlers.declareOwned (fun owned ->
            Ffi.withKeyExpr keyExpr (fun ke ->
                Handlers.withClosure Interop.z_closure_sample target (fun closure ->
                    let mutable o = Unchecked.defaultof<z_subscriber_options_t>
                    Interop.z_subscriber_options_default &&o

                    match allowedOrigin with
                    | Some l -> o.allowed_origin <- l
                    | None -> ()

                    Interop.z_declare_subscriber (session, owned, ke, closure, &&o)))
            |> Ffi.check "z_declare_subscriber")

    let put
        keyExpr
        payload
        encoding
        attachment
        (priority: Priority option)
        (congestionControl: CongestionControl option)
        (express: bool option)
        (reliability: Reliability option)
        (allowedDestination: Locality option)
        (sourceInfo: SourceInfo option)
        =
        let session = loan ()

        Ffi.withKeyExpr keyExpr (fun ke ->
            Moved.call "z_put" (Some payload) encoding attachment (fun pl enc att ->
                SourceInfoNative.using sourceInfo (fun si ->
                    let mutable o = Unchecked.defaultof<z_put_options_t>
                    Interop.z_put_options_default &&o
                    o.encoding <- enc
                    o.attachment <- att
                    o.source_info <- si

                    match priority with
                    | Some p -> o.priority <- p
                    | None -> ()

                    match congestionControl with
                    | Some c -> o.congestion_control <- c
                    | None -> ()

                    match express with
                    | Some e -> o.is_express <- Handlers.cbool e
                    | None -> ()

                    match reliability with
                    | Some r -> o.reliability <- r
                    | None -> ()

                    match allowedDestination with
                    | Some d -> o.allowed_destination <- d
                    | None -> ()

                    Interop.z_put (session, ke, pl, &&o))))

    /// Open a session; with no config, the zenoh defaults (peer mode, multicast scouting).
    /// shmClients: clients of custom shared-memory protocols (see IShmProviderBackend), in
    /// addition to the default ones, so that the session reads their buffers.
    static member Open(?config: Config, ?shmClients: IShmClient seq) =
        let openWith (owned: nativeptr<z_owned_session_t>) (open': nativeptr<z_owned_config_t> -> z_result_t) =
            let mutable cfg = Unchecked.defaultof<z_owned_config_t>
            let pCfg = &&cfg
            Config.write (defaultArg config Config.defaults) pCfg
            // The open functions move the config, also when they fail.
            Ffi.check "z_open" (open' pCfg)

        let owned =
            Handlers.declareOwned (fun owned ->
                match shmClients with
                | None -> openWith owned (fun cfg -> Interop.z_open (owned, cfg, 0n))
                | Some clients ->
                    ShmNative.withClientStorage clients (fun storage ->
                        openWith owned (fun cfg -> Interop.z_open_with_custom_shm_clients (owned, cfg, storage))))

        new Session(owned)

    /// The loaned session, for the declarations of the other files (liveliness, zenoh-ext, ...).
    member internal _.Loan() = loan ()

    /// Global id of this session.
    member _.EntityId = EntityId(Interop.z_session_id (loan ()))

    /// Zenoh id of this session, in hexadecimal.
    member _.Id = Ffi.zidToString (Interop.z_info_zid (loan ()))

    /// Zenoh ids of the peers this session is connected to.
    member _.Peers = collectIds Interop.z_info_peers_zid "z_info_peers_zid"

    /// Zenoh ids of the routers this session is connected to.
    member _.Routers = collectIds Interop.z_info_routers_zid "z_info_routers_zid"

    member _.IsClosed = handle.IsDisposed || Interop.z_session_is_closed (loan ()) <> 0uy

    /// Close the session: undeclare everything and disconnect. Dispose still frees it.
    member _.Close() =
        Ffi.check "z_close" (Interop.z_close (loan (), 0n))

    /// Publish payload on keyExpr.
    member _.Put
        (
            keyExpr: string,
            payload: byte[],
            ?encoding: string,
            ?attachment: byte[],
            ?priority: Priority,
            ?congestionControl: CongestionControl,
            ?express: bool,
            ?reliability: Reliability,
            ?allowedDestination: Locality,
            ?sourceInfo: SourceInfo
        ) =
        put
            keyExpr
            (Moved.bytes payload)
            encoding
            attachment
            priority
            congestionControl
            express
            reliability
            allowedDestination
            sourceInfo

    /// Publish a UTF-8 string on keyExpr.
    member _.Put(keyExpr: string, payload: string, ?encoding: string, ?attachment: byte[], ?priority: Priority) =
        put keyExpr (Moved.bytes (Ffi.utf8 payload)) encoding attachment priority None None None None None

    /// Publish a shared-memory buffer on keyExpr, without copying it. The buffer is moved into zenoh.
    member _.Put(keyExpr: string, payload: ShmBuffer, ?encoding: string, ?attachment: byte[], ?priority: Priority) =
        put keyExpr payload.WritePayload encoding attachment priority None None None None None

    /// Publish a deletion of keyExpr.
    member _.Delete(keyExpr: string, ?priority: Priority, ?congestionControl: CongestionControl) =
        let session = loan ()

        Ffi.withKeyExpr keyExpr (fun ke ->
            let mutable o = Unchecked.defaultof<z_delete_options_t>
            Interop.z_delete_options_default &&o

            match priority with
            | Some p -> o.priority <- p
            | None -> ()

            match congestionControl with
            | Some c -> o.congestion_control <- c
            | None -> ()

            Ffi.check "z_delete" (Interop.z_delete (session, ke, &&o)))

    /// Declare a publisher on keyExpr.
    member _.DeclarePublisher
        (
            keyExpr: string,
            ?encoding: string,
            ?priority: Priority,
            ?congestionControl: CongestionControl,
            ?express: bool,
            ?reliability: Reliability,
            ?allowedDestination: Locality
        ) =
        let session = loan ()

        let owned =
            Handlers.declareOwned (fun owned ->
                Ffi.withKeyExpr keyExpr (fun ke ->
                    Moved.call "z_declare_publisher" None encoding None (fun _ enc _ ->
                        let mutable o = Unchecked.defaultof<z_publisher_options_t>
                        Interop.z_publisher_options_default &&o
                        o.encoding <- enc

                        match priority with
                        | Some p -> o.priority <- p
                        | None -> ()

                        match congestionControl with
                        | Some c -> o.congestion_control <- c
                        | None -> ()

                        match express with
                        | Some e -> o.is_express <- Handlers.cbool e
                        | None -> ()

                        match reliability with
                        | Some r -> o.reliability <- r
                        | None -> ()

                        match allowedDestination with
                        | Some d -> o.allowed_destination <- d
                        | None -> ()

                        Interop.z_declare_publisher (session, owned, ke, &&o))))

        new Publisher(owned)

    /// Declare a subscriber that calls handler for each sample on keyExpr.
    /// The handler runs on a zenoh thread: keep it short, and do not dispose the subscriber
    /// or the session from it. Exceptions it throws go to ZenohRuntime.CallbackError.
    member _.DeclareSubscriber(keyExpr: string, handler: Sample -> unit, ?allowedOrigin: Locality) =
        new Subscriber(declareSubscriber keyExpr (Handlers.sampleCallback handler) allowedOrigin, keyExpr)

    /// Declare a subscriber whose samples are read from a channel. With a capacity, the channel
    /// keeps the most recent samples and drops the oldest; without, it is unbounded.
    member _.Subscribe(keyExpr: string, ?capacity: int, ?allowedOrigin: Locality) =
        let target, reader = Handlers.sampleChannel capacity
        let subscriber = new Subscriber(declareSubscriber keyExpr target allowedOrigin, keyExpr)
        new SampleStream(subscriber, keyExpr, reader)

    /// Declare a queryable that calls handler for each query on keyExpr. The query is valid
    /// until the handler returns (see Query.Detach). complete: the queryable answers every key
    /// of keyExpr (for QueryTarget.AllComplete).
    member _.DeclareQueryable(keyExpr: string, handler: Query -> unit, ?complete: bool, ?allowedOrigin: Locality) =
        let session = loan ()

        let target =
            Closures.Target(
                (fun p ->
                    let query = Query.Read(NativePtr.ofNativeInt p)

                    try
                        handler query
                    finally
                        query.Invalidate()),
                ignore
            )

        let owned =
            Handlers.declareOwned (fun owned ->
                Ffi.withKeyExpr keyExpr (fun ke ->
                    Handlers.withClosure Interop.z_closure_query target (fun closure ->
                        let mutable o = Unchecked.defaultof<z_queryable_options_t>
                        Interop.z_queryable_options_default &&o

                        match complete with
                        | Some c -> o.complete <- Handlers.cbool c
                        | None -> ()

                        match allowedOrigin with
                        | Some l -> o.allowed_origin <- l
                        | None -> ()

                        Interop.z_declare_queryable (session, owned, ke, closure, &&o)))
                |> Ffi.check "z_declare_queryable")

        new Queryable(owned, keyExpr)

    /// Send a query; replies arrive on the returned channel, which completes when the query
    /// ends (all queryables answered, the timeout passed, or cancellationToken was cancelled).
    member _.GetStream
        (
            keyExpr: string,
            ?parameters: string,
            ?payload: byte[],
            ?encoding: string,
            ?attachment: byte[],
            ?target: QueryTarget,
            ?consolidation: ConsolidationMode,
            ?timeout: TimeSpan,
            ?acceptReplies: ReplyKeyExpr,
            ?priority: Priority,
            ?congestionControl: CongestionControl,
            ?express: bool,
            ?sourceInfo: SourceInfo,
            ?cancellationToken: CancellationToken
        ) : ChannelReader<Reply> =
        let session = loan ()

        Handlers.get cancellationToken (fun closureTarget cancellation ->
            Ffi.withKeyExpr keyExpr (fun ke ->
                Moved.call "z_get" (Option.map Moved.bytes payload) encoding attachment (fun pl enc att ->
                    SourceInfoNative.using sourceInfo (fun si ->
                        let mutable o = Unchecked.defaultof<z_get_options_t>
                        let mutable token = Unchecked.defaultof<z_owned_cancellation_token_t>
                        Interop.z_get_options_default &&o
                        o.payload <- pl
                        o.encoding <- enc
                        o.attachment <- att
                        o.source_info <- si

                        match target with
                        | Some t -> o.target <- t
                        | None -> ()

                        match consolidation with
                        | Some c -> o.consolidation.mode <- c
                        | None -> ()

                        match timeout with
                        | Some t -> o.timeout_ms <- Handlers.timeoutMs t
                        | None -> ()

                        match acceptReplies with
                        | Some a -> o.accept_replies <- a
                        | None -> ()

                        match priority with
                        | Some p -> o.priority <- p
                        | None -> ()

                        match congestionControl with
                        | Some c -> o.congestion_control <- c
                        | None -> ()

                        match express with
                        | Some e -> o.is_express <- Handlers.cbool e
                        | None -> ()

                        o.cancellation_token <- Handlers.cancelToken cancellation &&token

                        Handlers.withClosure Interop.z_closure_reply closureTarget (fun closure ->
                            Interop.z_get (session, ke, defaultArg parameters "", closure, &&o))))))

    /// Send a query and collect all its replies. Cancelling cancellationToken cancels the query.
    member this.Get
        (
            keyExpr: string,
            ?parameters: string,
            ?payload: byte[],
            ?encoding: string,
            ?attachment: byte[],
            ?target: QueryTarget,
            ?consolidation: ConsolidationMode,
            ?timeout: TimeSpan,
            ?sourceInfo: SourceInfo,
            ?cancellationToken: CancellationToken
        ) : Task<Reply list> =
        let reader =
            this.GetStream(
                keyExpr,
                ?parameters = parameters,
                ?payload = payload,
                ?encoding = encoding,
                ?attachment = attachment,
                ?target = target,
                ?consolidation = consolidation,
                ?timeout = timeout,
                ?sourceInfo = sourceInfo,
                ?cancellationToken = cancellationToken
            )

        Handlers.collect reader (defaultArg cancellationToken CancellationToken.None)

    interface IDisposable with
        member _.Dispose() = handle.Dispose()
