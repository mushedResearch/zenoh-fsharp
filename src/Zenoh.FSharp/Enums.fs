namespace Zenoh

open System

// Public enums. Their values are those of the zenoh-c enums, so the native layer uses them directly.

/// Which sessions a declaration or a message may reach (z_locality_t).
type Locality =
    | Any = 0
    | SessionLocal = 1
    | Remote = 2

/// What to do when a message cannot be sent at once (z_congestion_control_t).
type CongestionControl =
    | Block = 0
    | Drop = 1
    | BlockFirst = 2

/// Priority of a message (z_priority_t). Default: Data.
type Priority =
    | RealTime = 1
    | InteractiveHigh = 2
    | InteractiveLow = 3
    | DataHigh = 4
    | Data = 5
    | DataLow = 6
    | Background = 7

/// Reliability of a message (z_reliability_t, unstable in zenoh-c).
type Reliability =
    | BestEffort = 0
    | Reliable = 1

/// Which queryables a get reaches (z_query_target_t).
type QueryTarget =
    | BestMatching = 0
    | All = 1
    | AllComplete = 2

/// How replies of a get are consolidated (z_consolidation_mode_t).
type ConsolidationMode =
    | Auto = -1
    | None = 0
    | Monotonic = 1
    | Latest = 2

/// Which key expressions a get accepts in its replies (z_reply_keyexpr_t).
type ReplyKeyExpr =
    | Any = 0
    | MatchingQuery = 1

/// Kind of a sample (z_sample_kind_t).
type SampleKind =
    | Put = 0
    | Delete = 1

/// Kinds of zenoh nodes (z_whatami_t); combine them to scout for several kinds (z_what_t).
[<Flags>]
type WhatAmI =
    | Router = 1
    | Peer = 2
    | Client = 4
