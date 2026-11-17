namespace Zenoh

// zenoh-ext advanced publisher and subscriber: a publisher cache that late subscribers can read
// (history), detection and recovery of lost samples, and detection of publishers/subscribers.
// Unstable in zenoh-c.

open System
open Microsoft.FSharp.NativeInterop
open Zenoh.Native

#nowarn "9"
#nowarn "51"

/// How an advanced publisher with sample miss detection sends heartbeats, so that subscribers
/// also detect the loss of its last samples.
type Heartbeat =
    | NoHeartbeat
    /// A heartbeat every period.
    | Periodic of period: TimeSpan
    /// A heartbeat every period, only when samples were published since the last one.
    | Sporadic of period: TimeSpan

/// Samples an advanced subscriber missed from one publisher.
type Miss =
    {
        /// The publisher.
        Source: EntityId
        Count: uint32
    }

/// A listener of missed samples. Dispose undeclares it.
[<Sealed>]
type SampleMissListener internal (owned: nativeptr<ze_owned_sample_miss_listener_t>) =
    let handle =
        Owned("SampleMissListener", owned, fun p -> Interop.ze_undeclare_sample_miss_listener p |> ignore)

    interface IDisposable with
        member _.Dispose() = handle.Dispose()

/// An advanced publisher. Dispose undeclares it.
[<Sealed>]
type AdvancedPublisher internal (owned: nativeptr<ze_owned_advanced_publisher_t>) =
    let handle =
        Owned("AdvancedPublisher", owned, fun p -> Interop.ze_undeclare_advanced_publisher p |> ignore)

    let keyExpr =
        Ffi.keyExprToString (Interop.ze_advanced_publisher_keyexpr (Interop.ze_advanced_publisher_loan owned))

    let loan () = Interop.ze_advanced_publisher_loan handle.Ptr

    let put payload encoding attachment sourceInfo =
        let publisher = loan ()

        Moved.call "ze_advanced_publisher_put" (Some payload) encoding attachment (fun pl enc att ->
            SourceInfoNative.using sourceInfo (fun si ->
                let mutable o = Unchecked.defaultof<z_publisher_put_options_t>
                Interop.z_publisher_put_options_default &&o
                o.encoding <- enc
                o.attachment <- att
                o.source_info <- si
                Interop.ze_advanced_publisher_put (publisher, pl, &&o)))

    member _.KeyExpr = keyExpr

    /// Global id of this publisher.
    member _.EntityId = EntityId(Interop.ze_advanced_publisher_id (loan ()))

    member _.Put(payload: byte[], ?encoding: string, ?attachment: byte[], ?sourceInfo: SourceInfo) =
        put (Moved.bytes payload) encoding attachment sourceInfo

    member _.Put(payload: string, ?encoding: string, ?attachment: byte[], ?sourceInfo: SourceInfo) =
        put (Moved.bytes (Ffi.utf8 payload)) encoding attachment sourceInfo

    /// Publish a shared-memory buffer, without copying it. The buffer is moved into zenoh.
    member _.Put(payload: ShmBuffer, ?encoding: string, ?attachment: byte[], ?sourceInfo: SourceInfo) =
        put payload.WritePayload encoding attachment sourceInfo

    member _.Delete() =
        Ffi.check "ze_advanced_publisher_delete" (Interop.ze_advanced_publisher_delete (loan (), 0n))

    /// Whether some subscriber matches this publisher.
    member _.MatchingStatus =
        let publisher = loan ()

        Matching.status "ze_advanced_publisher_get_matching_status" (fun s ->
            Interop.ze_advanced_publisher_get_matching_status (publisher, s))

    /// Call handler with the matching status each time it changes.
    member _.DeclareMatchingListener(handler: bool -> unit) =
        let publisher = loan ()

        Matching.listen "ze_advanced_publisher_declare_matching_listener" handler (fun (listener, closure) ->
            Interop.ze_advanced_publisher_declare_matching_listener (publisher, listener, closure))

    interface IDisposable with
        member _.Dispose() = handle.Dispose()

/// An advanced subscriber. Dispose undeclares it.
[<Sealed>]
type AdvancedSubscriber internal (owned: nativeptr<ze_owned_advanced_subscriber_t>) =
    let handle =
        Owned("AdvancedSubscriber", owned, fun p -> Interop.ze_undeclare_advanced_subscriber p |> ignore)

    let keyExpr =
        Ffi.keyExprToString (Interop.ze_advanced_subscriber_keyexpr (Interop.ze_advanced_subscriber_loan owned))

    let loan () = Interop.ze_advanced_subscriber_loan handle.Ptr

    member _.KeyExpr = keyExpr

    /// Global id of this subscriber.
    member _.EntityId = EntityId(Interop.ze_advanced_subscriber_id (loan ()))

    /// Call handler when samples from a publisher are detected as lost (they could not be
    /// recovered). Needs publishers with sample miss detection.
    member _.DeclareSampleMissListener(handler: Miss -> unit) =
        let subscriber = loan ()

        let target =
            Closures.Target(
                (fun p ->
                    let miss: ze_miss_t = NativePtr.read (NativePtr.ofNativeInt p)

                    handler
                        {
                            Source = EntityId miss.source
                            Count = miss.nb
                        }),
                ignore
            )

        let owned =
            Handlers.declareOwned (fun owned ->
                Handlers.withClosure Interop.ze_closure_miss target (fun closure ->
                    Interop.ze_advanced_subscriber_declare_sample_miss_listener (subscriber, owned, closure))
                |> Ffi.check "ze_advanced_subscriber_declare_sample_miss_listener")

        new SampleMissListener(owned)

    /// Call handler when an advanced publisher with publisher detection on the key expression of
    /// this subscriber appears (Put) or disappears (Delete). history: also report those alive now.
    member _.DetectPublishers(handler: Sample -> unit, ?history: bool) =
        let subscriber = loan ()

        let owned =
            Handlers.declareOwned (fun owned ->
                Handlers.withClosure Interop.z_closure_sample (Handlers.sampleCallback handler) (fun closure ->
                    let mutable o = Unchecked.defaultof<z_liveliness_subscriber_options_t>
                    Interop.z_liveliness_subscriber_options_default &&o

                    match history with
                    | Some h -> o.history <- Handlers.cbool h
                    | None -> ()

                    Interop.ze_advanced_subscriber_detect_publishers (subscriber, owned, closure, &&o))
                |> Ffi.check "ze_advanced_subscriber_detect_publishers")

        new Subscriber(owned, keyExpr)

    interface IDisposable with
        member _.Dispose() = handle.Dispose()

[<AutoOpen>]
module AdvancedExtensions =
    type Session with
        /// Declare an advanced publisher on keyExpr.
        /// - cache: keep the last `cache` samples, for subscribers with history or recovery.
        ///   Without sampleMissDetection, the cache orders samples by timestamp, so the session
        ///   needs timestamping (Config.timestamping true).
        /// - sampleMissDetection: let subscribers detect lost samples, with the given heartbeat.
        /// - publisherDetection: announce the publisher with a liveliness token, for
        ///   AdvancedSubscriber.DetectPublishers and late publisher detection.
        member this.DeclareAdvancedPublisher
            (
                keyExpr: string,
                ?cache: int,
                ?sampleMissDetection: Heartbeat,
                ?publisherDetection: bool,
                ?encoding: string,
                ?priority: Priority,
                ?congestionControl: CongestionControl,
                ?express: bool,
                ?reliability: Reliability,
                ?allowedDestination: Locality
            ) =
            let session = this.Loan()

            let owned =
                Handlers.declareOwned (fun owned ->
                    Ffi.withKeyExpr keyExpr (fun ke ->
                        Moved.call "ze_declare_advanced_publisher" None encoding None (fun _ enc _ ->
                            let mutable o = Unchecked.defaultof<ze_advanced_publisher_options_t>
                            Interop.ze_advanced_publisher_options_default &&o
                            o.publisher_options.encoding <- enc

                            match priority with
                            | Some p -> o.publisher_options.priority <- p
                            | None -> ()

                            match congestionControl with
                            | Some c -> o.publisher_options.congestion_control <- c
                            | None -> ()

                            match express with
                            | Some e -> o.publisher_options.is_express <- Handlers.cbool e
                            | None -> ()

                            match reliability with
                            | Some r -> o.publisher_options.reliability <- r
                            | None -> ()

                            match allowedDestination with
                            | Some d -> o.publisher_options.allowed_destination <- d
                            | None -> ()

                            match cache with
                            | Some n ->
                                o.cache.is_enabled <- 1uy
                                o.cache.max_samples <- unativeint n
                            | None -> ()

                            match sampleMissDetection with
                            | Some heartbeat ->
                                o.sample_miss_detection.is_enabled <- 1uy

                                match heartbeat with
                                | NoHeartbeat -> o.sample_miss_detection.heartbeat_mode <- 0
                                | Periodic period ->
                                    o.sample_miss_detection.heartbeat_mode <- 1
                                    o.sample_miss_detection.heartbeat_period_ms <- Handlers.timeoutMs period
                                | Sporadic period ->
                                    o.sample_miss_detection.heartbeat_mode <- 2
                                    o.sample_miss_detection.heartbeat_period_ms <- Handlers.timeoutMs period
                            | None -> ()

                            match publisherDetection with
                            | Some d -> o.publisher_detection <- Handlers.cbool d
                            | None -> ()

                            Interop.ze_declare_advanced_publisher (session, owned, ke, &&o))))

            new AdvancedPublisher(owned)

        /// Declare an advanced subscriber that calls handler for each sample on keyExpr.
        /// - history (or any history* setting): first read the samples cached by advanced
        ///   publishers, at most historyMaxSamples per publisher and no older than historyMaxAge.
        /// - detectLatePublishers: also read the history of publishers that appear later
        ///   (needs publisherDetection on the publishers).
        /// - recovery (or recoveryPeriodicQueries): ask publishers for lost samples (needs
        ///   publishers with a cache and sample miss detection); recoveryPeriodicQueries also
        ///   queries periodically, to detect the loss of the last samples without heartbeats.
        /// - subscriberDetection: announce the subscriber with a liveliness token.
        member this.DeclareAdvancedSubscriber
            (
                keyExpr: string,
                handler: Sample -> unit,
                ?history: bool,
                ?historyMaxSamples: int,
                ?historyMaxAge: TimeSpan,
                ?detectLatePublishers: bool,
                ?recovery: bool,
                ?recoveryPeriodicQueries: TimeSpan,
                ?queryTimeout: TimeSpan,
                ?subscriberDetection: bool,
                ?allowedOrigin: Locality
            ) =
            let loaned = this.Loan()

            let owned =
                Handlers.declareOwned (fun owned ->
                    Ffi.withKeyExpr keyExpr (fun ke ->
                        Handlers.withClosure Interop.z_closure_sample (Handlers.sampleCallback handler) (fun closure ->
                            let mutable o = Unchecked.defaultof<ze_advanced_subscriber_options_t>
                            Interop.ze_advanced_subscriber_options_default &&o

                            // Any history setting turns history on.
                            let historyOn =
                                defaultArg history (historyMaxSamples.IsSome || historyMaxAge.IsSome || detectLatePublishers = Some true)

                            o.history.is_enabled <- Handlers.cbool historyOn

                            match historyMaxSamples with
                            | Some n -> o.history.max_samples <- unativeint n
                            | None -> ()

                            match historyMaxAge with
                            | Some a -> o.history.max_age_ms <- Handlers.timeoutMs a
                            | None -> ()

                            match detectLatePublishers with
                            | Some d -> o.history.detect_late_publishers <- Handlers.cbool d
                            | None -> ()

                            let recoveryOn = defaultArg recovery recoveryPeriodicQueries.IsSome
                            o.recovery.is_enabled <- Handlers.cbool recoveryOn

                            match recoveryPeriodicQueries with
                            | Some period ->
                                o.recovery.last_sample_miss_detection.is_enabled <- 1uy
                                o.recovery.last_sample_miss_detection.periodic_queries_period_ms <- Handlers.timeoutMs period
                            | None -> ()

                            match queryTimeout with
                            | Some t -> o.query_timeout_ms <- Handlers.timeoutMs t
                            | None -> ()

                            match subscriberDetection with
                            | Some d -> o.subscriber_detection <- Handlers.cbool d
                            | None -> ()

                            match allowedOrigin with
                            | Some l -> o.subscriber_options.allowed_origin <- l
                            | None -> ()

                            Interop.ze_declare_advanced_subscriber (loaned, owned, ke, closure, &&o)))
                    |> Ffi.check "ze_declare_advanced_subscriber")

            new AdvancedSubscriber(owned)
