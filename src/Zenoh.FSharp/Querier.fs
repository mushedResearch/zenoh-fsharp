namespace Zenoh

// Querier: a get declared once on a key expression, with its options, then sent many times.

open System
open System.Threading
open System.Threading.Channels
open System.Threading.Tasks
open Zenoh.Native

#nowarn "9"
#nowarn "51"

/// A querier declared on a key expression. Dispose undeclares it.
[<Sealed>]
type Querier internal (owned: nativeptr<z_owned_querier_t>) =
    let handle =
        Owned("Querier", owned, fun p -> Interop.z_undeclare_querier p |> ignore)

    let keyExpr =
        Ffi.keyExprToString (Interop.z_querier_keyexpr (Interop.z_querier_loan owned))

    let loan () = Interop.z_querier_loan handle.Ptr

    member _.KeyExpr = keyExpr

    /// Send a query; replies arrive on the returned channel, which completes when the query
    /// ends. Cancelling cancellationToken cancels the query.
    member _.GetStream
        (
            ?parameters: string,
            ?payload: byte[],
            ?encoding: string,
            ?attachment: byte[],
            ?sourceInfo: SourceInfo,
            ?cancellationToken: CancellationToken
        ) : ChannelReader<Reply> =
        let querier = loan ()

        Handlers.get cancellationToken (fun target cancellation ->
            Moved.call "z_querier_get" (Option.map Moved.bytes payload) encoding attachment (fun pl enc att ->
                SourceInfoNative.using sourceInfo (fun si ->
                    let mutable o = Unchecked.defaultof<z_querier_get_options_t>
                    let mutable token = Unchecked.defaultof<z_owned_cancellation_token_t>
                    Interop.z_querier_get_options_default &&o
                    o.payload <- pl
                    o.encoding <- enc
                    o.attachment <- att
                    o.source_info <- si
                    o.cancellation_token <- Handlers.cancelToken cancellation &&token

                    Handlers.withClosure Interop.z_closure_reply target (fun closure ->
                        Interop.z_querier_get (querier, defaultArg parameters "", closure, &&o)))))

    /// Send a query and collect all its replies. Cancelling cancellationToken cancels the query.
    member this.Get
        (
            ?parameters: string,
            ?payload: byte[],
            ?encoding: string,
            ?attachment: byte[],
            ?sourceInfo: SourceInfo,
            ?cancellationToken: CancellationToken
        ) : Task<Reply list> =
        let reader =
            this.GetStream(
                ?parameters = parameters,
                ?payload = payload,
                ?encoding = encoding,
                ?attachment = attachment,
                ?sourceInfo = sourceInfo,
                ?cancellationToken = cancellationToken
            )

        Handlers.collect reader (defaultArg cancellationToken CancellationToken.None)

    /// Global id of this querier.
    member _.EntityId = EntityId(Interop.z_querier_id (loan ()))

    /// Whether some queryable matches this querier.
    member _.MatchingStatus =
        let querier = loan ()
        Matching.status "z_querier_get_matching_status" (fun s -> Interop.z_querier_get_matching_status (querier, s))

    /// Call handler with the matching status each time it changes.
    member _.DeclareMatchingListener(handler: bool -> unit) =
        let querier = loan ()

        Matching.listen "z_querier_declare_matching_listener" handler (fun (listener, closure) ->
            Interop.z_querier_declare_matching_listener (querier, listener, closure))

    interface IDisposable with
        member _.Dispose() = handle.Dispose()

[<AutoOpen>]
module QuerierExtensions =
    type Session with
        /// Declare a querier on keyExpr; its options apply to every get it sends.
        member this.DeclareQuerier
            (
                keyExpr: string,
                ?target: QueryTarget,
                ?consolidation: ConsolidationMode,
                ?timeout: TimeSpan,
                ?acceptReplies: ReplyKeyExpr,
                ?priority: Priority,
                ?congestionControl: CongestionControl,
                ?express: bool,
                ?allowedDestination: Locality
            ) =
            let session = this.Loan()

            let owned =
                Handlers.declareOwned (fun owned ->
                    Ffi.withKeyExpr keyExpr (fun ke ->
                        let mutable o = Unchecked.defaultof<z_querier_options_t>
                        Interop.z_querier_options_default &&o

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

                        match allowedDestination with
                        | Some d -> o.allowed_destination <- d
                        | None -> ()

                        Ffi.check "z_declare_querier" (Interop.z_declare_querier (session, owned, ke, &&o))))

            new Querier(owned)
