namespace Zenoh.Native

// Concrete (non-opaque) types of zenoh_commons.h: enums, closures and option structs.
// The option structs mirror the official prebuilt zenoh-c, which is built with
// Z_FEATURE_UNSTABLE_API, so the fields guarded by that feature are present.
// The C enums are the public enums of Enums.fs (C enums are 4-byte ints).

open System.Runtime.InteropServices
open Zenoh

#nowarn "9"

type z_result_t = int8

/// `typedef struct ALIGN(1) z_id_t { uint8_t id[16]; }`, returned by value from z_info_zid.
/// Declared as two 8-byte integers: same size, and the System V x64 ABI classifies both as
/// two INTEGER eightbytes, so the return value comes back in RAX:RDX as C expects.
[<Struct; StructLayout(LayoutKind.Sequential)>]
type z_id_t =
    val mutable lo: uint64
    val mutable hi: uint64

module Result =
    [<Literal>]
    let Z_OK = 0y

    [<Literal>]
    let Z_EDESERIALIZE = -7y

    [<Literal>]
    let Z_ESESSION_CLOSED = -8y

/// Shape shared by z_owned_closure_sample_t, z_owned_closure_query_t and z_owned_closure_reply_t:
/// a context pointer and two C function pointers.
[<Struct; StructLayout(LayoutKind.Sequential)>]
type z_owned_closure_t =
    val mutable _context: nativeint
    val mutable _call: nativeint
    val mutable _drop: nativeint

[<Struct; StructLayout(LayoutKind.Sequential)>]
type z_query_consolidation_t =
    val mutable mode: ConsolidationMode

[<Struct; StructLayout(LayoutKind.Sequential)>]
type z_put_options_t =
    val mutable encoding: nativeptr<z_owned_encoding_t>
    val mutable congestion_control: CongestionControl
    val mutable priority: Priority
    val mutable is_express: byte // C bool
    val mutable timestamp: nativeptr<z_timestamp_t>
    val mutable reliability: Reliability
    val mutable allowed_destination: Locality
    val mutable source_info: nativeint
    val mutable attachment: nativeptr<z_owned_bytes_t>

[<Struct; StructLayout(LayoutKind.Sequential)>]
type z_delete_options_t =
    val mutable congestion_control: CongestionControl
    val mutable priority: Priority
    val mutable is_express: byte // C bool
    val mutable timestamp: nativeptr<z_timestamp_t>
    val mutable reliability: Reliability
    val mutable allowed_destination: Locality

[<Struct; StructLayout(LayoutKind.Sequential)>]
type z_publisher_options_t =
    val mutable encoding: nativeptr<z_owned_encoding_t>
    val mutable congestion_control: CongestionControl
    val mutable priority: Priority
    val mutable is_express: byte // C bool
    val mutable reliability: Reliability
    val mutable allowed_destination: Locality

[<Struct; StructLayout(LayoutKind.Sequential)>]
type z_publisher_put_options_t =
    val mutable encoding: nativeptr<z_owned_encoding_t>
    val mutable timestamp: nativeptr<z_timestamp_t>
    val mutable source_info: nativeint
    val mutable attachment: nativeptr<z_owned_bytes_t>

[<Struct; StructLayout(LayoutKind.Sequential)>]
type z_subscriber_options_t =
    val mutable allowed_origin: Locality

[<Struct; StructLayout(LayoutKind.Sequential)>]
type z_queryable_options_t =
    val mutable complete: byte // C bool
    val mutable allowed_origin: Locality

[<Struct; StructLayout(LayoutKind.Sequential)>]
type z_query_reply_options_t =
    val mutable encoding: nativeptr<z_owned_encoding_t>
    val mutable congestion_control: CongestionControl
    val mutable priority: Priority
    val mutable is_express: byte // C bool
    val mutable timestamp: nativeptr<z_timestamp_t>
    val mutable source_info: nativeint
    val mutable attachment: nativeptr<z_owned_bytes_t>

[<Struct; StructLayout(LayoutKind.Sequential)>]
type z_query_reply_err_options_t =
    val mutable encoding: nativeptr<z_owned_encoding_t>

[<Struct; StructLayout(LayoutKind.Sequential)>]
type z_get_options_t =
    val mutable target: QueryTarget
    val mutable consolidation: z_query_consolidation_t
    val mutable payload: nativeptr<z_owned_bytes_t>
    val mutable encoding: nativeptr<z_owned_encoding_t>
    val mutable congestion_control: CongestionControl
    val mutable is_express: byte // C bool
    val mutable allowed_destination: Locality
    val mutable accept_replies: ReplyKeyExpr
    val mutable priority: Priority
    val mutable source_info: nativeint
    val mutable attachment: nativeptr<z_owned_bytes_t>
    val mutable timeout_ms: uint64
    val mutable cancellation_token: nativeint

// Liveliness

[<Struct; StructLayout(LayoutKind.Sequential)>]
type z_liveliness_subscriber_options_t =
    val mutable history: byte // C bool

[<Struct; StructLayout(LayoutKind.Sequential)>]
type z_liveliness_get_options_t =
    val mutable timeout_ms: uint64
    val mutable cancellation_token: nativeint

// Querier and matching

[<Struct; StructLayout(LayoutKind.Sequential)>]
type z_querier_options_t =
    val mutable target: QueryTarget
    val mutable consolidation: z_query_consolidation_t
    val mutable congestion_control: CongestionControl
    val mutable is_express: byte // C bool
    val mutable allowed_destination: Locality
    val mutable accept_replies: ReplyKeyExpr
    val mutable priority: Priority
    val mutable timeout_ms: uint64

[<Struct; StructLayout(LayoutKind.Sequential)>]
type z_querier_get_options_t =
    val mutable payload: nativeptr<z_owned_bytes_t>
    val mutable encoding: nativeptr<z_owned_encoding_t>
    val mutable source_info: nativeint
    val mutable attachment: nativeptr<z_owned_bytes_t>
    val mutable cancellation_token: nativeint

[<Struct; StructLayout(LayoutKind.Sequential)>]
type z_matching_status_t =
    val mutable matching: byte // C bool

// Scouting

[<Struct; StructLayout(LayoutKind.Sequential)>]
type z_scout_options_t =
    val mutable timeout_ms: uint64
    val mutable what: WhatAmI

// Shared memory

type zc_buf_layout_alloc_status_t =
    | Ok = 0
    | AllocError = 1
    | LayoutError = 2

[<Struct; StructLayout(LayoutKind.Sequential)>]
type z_buf_layout_alloc_result_t =
    val mutable status: zc_buf_layout_alloc_status_t
    val mutable buf: z_owned_shm_mut_t
    val mutable alloc_error: int
    val mutable layout_error: int

// zenoh-ext: advanced publisher and subscriber

[<Struct; StructLayout(LayoutKind.Sequential)>]
type ze_advanced_publisher_cache_options_t =
    val mutable is_enabled: byte // C bool
    val mutable max_samples: unativeint
    val mutable congestion_control: CongestionControl
    val mutable priority: Priority
    val mutable is_express: byte // C bool

[<Struct; StructLayout(LayoutKind.Sequential)>]
type ze_advanced_publisher_sample_miss_detection_options_t =
    val mutable is_enabled: byte // C bool
    val mutable heartbeat_mode: int // ze_advanced_publisher_heartbeat_mode_t
    val mutable heartbeat_period_ms: uint64

[<Struct; StructLayout(LayoutKind.Sequential)>]
type ze_advanced_publisher_options_t =
    val mutable publisher_options: z_publisher_options_t
    val mutable cache: ze_advanced_publisher_cache_options_t
    val mutable sample_miss_detection: ze_advanced_publisher_sample_miss_detection_options_t
    val mutable publisher_detection: byte // C bool
    val mutable publisher_detection_metadata: nativeptr<z_loaned_keyexpr_t>

[<Struct; StructLayout(LayoutKind.Sequential)>]
type ze_advanced_subscriber_history_options_t =
    val mutable is_enabled: byte // C bool
    val mutable detect_late_publishers: byte // C bool
    val mutable max_samples: unativeint
    val mutable max_age_ms: uint64

[<Struct; StructLayout(LayoutKind.Sequential)>]
type ze_advanced_subscriber_last_sample_miss_detection_options_t =
    val mutable is_enabled: byte // C bool
    val mutable periodic_queries_period_ms: uint64

[<Struct; StructLayout(LayoutKind.Sequential)>]
type ze_advanced_subscriber_recovery_options_t =
    val mutable is_enabled: byte // C bool
    val mutable last_sample_miss_detection: ze_advanced_subscriber_last_sample_miss_detection_options_t

[<Struct; StructLayout(LayoutKind.Sequential)>]
type ze_advanced_subscriber_options_t =
    val mutable subscriber_options: z_subscriber_options_t
    val mutable history: ze_advanced_subscriber_history_options_t
    val mutable recovery: ze_advanced_subscriber_recovery_options_t
    val mutable query_timeout_ms: uint64
    val mutable subscriber_detection: byte // C bool
    val mutable subscriber_detection_metadata: nativeptr<z_loaned_keyexpr_t>

/// `struct ze_miss_t { z_entity_global_id_t source; uint32_t nb; }`
[<Struct; StructLayout(LayoutKind.Sequential)>]
type ze_miss_t =
    val mutable source: z_entity_global_id_t
    val mutable nb: uint32

// Custom shared-memory providers and clients

[<Struct; StructLayout(LayoutKind.Sequential)>]
type z_alloc_alignment_t =
    /// The alignment is 2^pow bytes.
    val mutable pow: byte

[<Struct; StructLayout(LayoutKind.Sequential)>]
type z_chunk_descriptor_t =
    val mutable segment: uint32
    val mutable chunk: uint32
    val mutable len: unativeint

[<Struct; StructLayout(LayoutKind.Sequential)>]
type z_allocated_chunk_t =
    val mutable descriptor: z_chunk_descriptor_t
    val mutable ptr: nativeptr<z_owned_ptr_in_segment_t>

/// A context pointer and the function zenoh-c calls once to delete it.
[<Struct; StructLayout(LayoutKind.Sequential)>]
type zc_threadsafe_context_t =
    val mutable ptr: nativeint
    val mutable delete_fn: nativeint

[<Struct; StructLayout(LayoutKind.Sequential)>]
type zc_shm_provider_backend_callbacks_t =
    val mutable alloc_fn: nativeint
    val mutable free_fn: nativeint
    val mutable defragment_fn: nativeint
    val mutable available_fn: nativeint
    val mutable layout_for_fn: nativeint
    val mutable id_fn: nativeint

[<Struct; StructLayout(LayoutKind.Sequential)>]
type zc_shm_client_callbacks_t =
    val mutable attach_fn: nativeint
    val mutable id_fn: nativeint

[<Struct; StructLayout(LayoutKind.Sequential)>]
type z_shm_segment_t =
    val mutable context: zc_threadsafe_context_t
    val mutable map_fn: nativeint

type zc_buf_alloc_status_t =
    | Ok = 0
    | AllocError = 1

[<Struct; StructLayout(LayoutKind.Sequential)>]
type z_buf_alloc_result_t =
    val mutable status: zc_buf_alloc_status_t
    val mutable buf: z_owned_shm_mut_t
    val mutable error: int
