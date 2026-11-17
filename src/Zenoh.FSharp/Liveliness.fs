namespace Zenoh

// Liveliness: tokens that say "this entity is alive", and subscribers and gets that see them
// appear (Put samples) and disappear (Delete samples).

open System
open System.Threading
open System.Threading.Channels
open System.Threading.Tasks
open Zenoh.Native

#nowarn "9"
#nowarn "51"

/// A liveliness token: alive until disposed, or until its session closes or loses connectivity.
[<Sealed>]
type LivelinessToken internal (owned: nativeptr<z_owned_liveliness_token_t>, keyExpr: string) =
    let handle =
        Owned("LivelinessToken", owned, fun p -> Interop.z_liveliness_undeclare_token p |> ignore)

    member _.KeyExpr = keyExpr

    interface IDisposable with
        member _.Dispose() = handle.Dispose()

[<AutoOpen>]
module LivelinessExtensions =
    let private declareSubscriber (session: Session) keyExpr target (history: bool option) =
        let loaned = session.Loan()

        Handlers.declareOwned (fun owned ->
            Ffi.withKeyExpr keyExpr (fun ke ->
                Handlers.withClosure Interop.z_closure_sample target (fun closure ->
                    let mutable o = Unchecked.defaultof<z_liveliness_subscriber_options_t>
                    Interop.z_liveliness_subscriber_options_default &&o

                    match history with
                    | Some h -> o.history <- Handlers.cbool h
                    | None -> ()

                    Interop.z_liveliness_declare_subscriber (loaned, owned, ke, closure, &&o)))
            |> Ffi.check "z_liveliness_declare_subscriber")

    type Session with
        /// Declare a liveliness token on keyExpr.
        member this.DeclareLivelinessToken(keyExpr: string) =
            let loaned = this.Loan()

            let owned =
                Handlers.declareOwned (fun owned ->
                    Ffi.withKeyExpr keyExpr (fun ke ->
                        Ffi.check "z_liveliness_declare_token" (Interop.z_liveliness_declare_token (loaned, owned, ke, 0n))))

            new LivelinessToken(owned, keyExpr)

        /// Call handler when a liveliness token matching keyExpr appears (SampleKind.Put) or
        /// disappears (SampleKind.Delete). history: also report the tokens alive now.
        member this.DeclareLivelinessSubscriber(keyExpr: string, handler: Sample -> unit, ?history: bool) =
            new Subscriber(declareSubscriber this keyExpr (Handlers.sampleCallback handler) history, keyExpr)

        /// Liveliness changes on keyExpr, read from a channel (see DeclareLivelinessSubscriber).
        member this.SubscribeLiveliness(keyExpr: string, ?history: bool, ?capacity: int) =
            let target, reader = Handlers.sampleChannel capacity
            let subscriber = new Subscriber(declareSubscriber this keyExpr target history, keyExpr)
            new SampleStream(subscriber, keyExpr, reader)

        /// Query the liveliness tokens alive on keyExpr: one ReplyOk per token.
        /// Cancelling cancellationToken cancels the query.
        member this.LivelinessGetStream
            (keyExpr: string, ?timeout: TimeSpan, ?cancellationToken: CancellationToken)
            : ChannelReader<Reply> =
            let loaned = this.Loan()

            Handlers.get cancellationToken (fun target cancellation ->
                Ffi.withKeyExpr keyExpr (fun ke ->
                    let mutable o = Unchecked.defaultof<z_liveliness_get_options_t>
                    let mutable token = Unchecked.defaultof<z_owned_cancellation_token_t>
                    Interop.z_liveliness_get_options_default &&o

                    match timeout with
                    | Some t -> o.timeout_ms <- Handlers.timeoutMs t
                    | None -> ()

                    o.cancellation_token <- Handlers.cancelToken cancellation &&token

                    Handlers.withClosure Interop.z_closure_reply target (fun closure ->
                        Interop.z_liveliness_get (loaned, ke, closure, &&o))
                    |> Ffi.check "z_liveliness_get"))

        /// Collect the liveliness tokens alive on keyExpr.
        member this.LivelinessGet(keyExpr: string, ?timeout: TimeSpan, ?cancellationToken: CancellationToken) : Task<Reply list> =
            let reader =
                this.LivelinessGetStream(keyExpr, ?timeout = timeout, ?cancellationToken = cancellationToken)

            Handlers.collect reader (defaultArg cancellationToken CancellationToken.None)
