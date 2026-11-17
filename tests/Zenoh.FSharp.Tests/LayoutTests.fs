module LayoutTests

// The F# mirrors of the zenoh-c structs must have the size and field offsets that the C compiler
// gives them. Expected values: output of tools/layout-check.c for zenoh-c 1.10.2 on x86_64 Linux.

open System.Runtime.InteropServices
open Expecto
open Zenoh.Native

#nowarn "9"

let private size<'T when 'T: unmanaged> () = sizeof<'T>
let private offset<'T> (field: string) = int (Marshal.OffsetOf<'T> field)

let private expected =
    [
        "z_id_t", 16, size<z_id_t> ()
        "z_owned_closure_sample_t", 24, size<z_owned_closure_t> ()
        "z_put_options_t", 56, size<z_put_options_t> ()
        "z_put_options_t.is_express", 16, offset<z_put_options_t> "is_express"
        "z_put_options_t.timestamp", 24, offset<z_put_options_t> "timestamp"
        "z_put_options_t.reliability", 32, offset<z_put_options_t> "reliability"
        "z_put_options_t.allowed_destination", 36, offset<z_put_options_t> "allowed_destination"
        "z_put_options_t.source_info", 40, offset<z_put_options_t> "source_info"
        "z_put_options_t.attachment", 48, offset<z_put_options_t> "attachment"
        "z_delete_options_t", 32, size<z_delete_options_t> ()
        "z_delete_options_t.timestamp", 16, offset<z_delete_options_t> "timestamp"
        "z_delete_options_t.allowed_destination", 28, offset<z_delete_options_t> "allowed_destination"
        "z_publisher_options_t", 32, size<z_publisher_options_t> ()
        "z_publisher_options_t.is_express", 16, offset<z_publisher_options_t> "is_express"
        "z_publisher_options_t.allowed_destination", 24, offset<z_publisher_options_t> "allowed_destination"
        "z_publisher_put_options_t", 32, size<z_publisher_put_options_t> ()
        "z_publisher_put_options_t.attachment", 24, offset<z_publisher_put_options_t> "attachment"
        "z_subscriber_options_t", 4, size<z_subscriber_options_t> ()
        "z_queryable_options_t", 8, size<z_queryable_options_t> ()
        "z_queryable_options_t.allowed_origin", 4, offset<z_queryable_options_t> "allowed_origin"
        "z_query_reply_options_t", 48, size<z_query_reply_options_t> ()
        "z_query_reply_options_t.timestamp", 24, offset<z_query_reply_options_t> "timestamp"
        "z_query_reply_options_t.attachment", 40, offset<z_query_reply_options_t> "attachment"
        "z_query_reply_err_options_t", 8, size<z_query_reply_err_options_t> ()
        "z_get_options_t", 80, size<z_get_options_t> ()
        "z_get_options_t.payload", 8, offset<z_get_options_t> "payload"
        "z_get_options_t.congestion_control", 24, offset<z_get_options_t> "congestion_control"
        "z_get_options_t.is_express", 28, offset<z_get_options_t> "is_express"
        "z_get_options_t.allowed_destination", 32, offset<z_get_options_t> "allowed_destination"
        "z_get_options_t.priority", 40, offset<z_get_options_t> "priority"
        "z_get_options_t.attachment", 56, offset<z_get_options_t> "attachment"
        "z_get_options_t.timeout_ms", 64, offset<z_get_options_t> "timeout_ms"
        "z_get_options_t.cancellation_token", 72, offset<z_get_options_t> "cancellation_token"
        "z_liveliness_subscriber_options_t", 1, size<z_liveliness_subscriber_options_t> ()
        "z_liveliness_get_options_t", 16, size<z_liveliness_get_options_t> ()
        "z_liveliness_get_options_t.cancellation_token", 8, offset<z_liveliness_get_options_t> "cancellation_token"
        "z_querier_options_t", 40, size<z_querier_options_t> ()
        "z_querier_options_t.congestion_control", 8, offset<z_querier_options_t> "congestion_control"
        "z_querier_options_t.is_express", 12, offset<z_querier_options_t> "is_express"
        "z_querier_options_t.allowed_destination", 16, offset<z_querier_options_t> "allowed_destination"
        "z_querier_options_t.priority", 24, offset<z_querier_options_t> "priority"
        "z_querier_options_t.timeout_ms", 32, offset<z_querier_options_t> "timeout_ms"
        "z_querier_get_options_t", 40, size<z_querier_get_options_t> ()
        "z_querier_get_options_t.attachment", 24, offset<z_querier_get_options_t> "attachment"
        "z_querier_get_options_t.cancellation_token", 32, offset<z_querier_get_options_t> "cancellation_token"
        "z_matching_status_t", 1, size<z_matching_status_t> ()
        "z_scout_options_t", 16, size<z_scout_options_t> ()
        "z_scout_options_t.what", 8, offset<z_scout_options_t> "what"
        "z_buf_layout_alloc_result_t", 96, size<z_buf_layout_alloc_result_t> ()
        "z_buf_layout_alloc_result_t.buf", 8, offset<z_buf_layout_alloc_result_t> "buf"
        "z_buf_layout_alloc_result_t.alloc_error", 88, offset<z_buf_layout_alloc_result_t> "alloc_error"
        "z_buf_layout_alloc_result_t.layout_error", 92, offset<z_buf_layout_alloc_result_t> "layout_error"
        "ze_advanced_publisher_cache_options_t", 32, size<ze_advanced_publisher_cache_options_t> ()
        "ze_advanced_publisher_cache_options_t.max_samples", 8, offset<ze_advanced_publisher_cache_options_t> "max_samples"
        "ze_advanced_publisher_cache_options_t.is_express", 24, offset<ze_advanced_publisher_cache_options_t> "is_express"
        "ze_advanced_publisher_sample_miss_detection_options_t", 16, size<ze_advanced_publisher_sample_miss_detection_options_t> ()
        "ze_advanced_publisher_sample_miss_detection_options_t.heartbeat_period_ms", 8, offset<ze_advanced_publisher_sample_miss_detection_options_t> "heartbeat_period_ms"
        "ze_advanced_publisher_options_t", 96, size<ze_advanced_publisher_options_t> ()
        "ze_advanced_publisher_options_t.cache", 32, offset<ze_advanced_publisher_options_t> "cache"
        "ze_advanced_publisher_options_t.sample_miss_detection", 64, offset<ze_advanced_publisher_options_t> "sample_miss_detection"
        "ze_advanced_publisher_options_t.publisher_detection", 80, offset<ze_advanced_publisher_options_t> "publisher_detection"
        "ze_advanced_publisher_options_t.publisher_detection_metadata", 88, offset<ze_advanced_publisher_options_t> "publisher_detection_metadata"
        "ze_advanced_subscriber_history_options_t", 24, size<ze_advanced_subscriber_history_options_t> ()
        "ze_advanced_subscriber_history_options_t.max_samples", 8, offset<ze_advanced_subscriber_history_options_t> "max_samples"
        "ze_advanced_subscriber_history_options_t.max_age_ms", 16, offset<ze_advanced_subscriber_history_options_t> "max_age_ms"
        "ze_advanced_subscriber_recovery_options_t", 24, size<ze_advanced_subscriber_recovery_options_t> ()
        "ze_advanced_subscriber_recovery_options_t.last_sample_miss_detection", 8, offset<ze_advanced_subscriber_recovery_options_t> "last_sample_miss_detection"
        "ze_advanced_subscriber_options_t", 80, size<ze_advanced_subscriber_options_t> ()
        "ze_advanced_subscriber_options_t.history", 8, offset<ze_advanced_subscriber_options_t> "history"
        "ze_advanced_subscriber_options_t.recovery", 32, offset<ze_advanced_subscriber_options_t> "recovery"
        "ze_advanced_subscriber_options_t.query_timeout_ms", 56, offset<ze_advanced_subscriber_options_t> "query_timeout_ms"
        "ze_advanced_subscriber_options_t.subscriber_detection", 64, offset<ze_advanced_subscriber_options_t> "subscriber_detection"
        "ze_advanced_subscriber_options_t.subscriber_detection_metadata", 72, offset<ze_advanced_subscriber_options_t> "subscriber_detection_metadata"
        "ze_miss_t", 24, size<ze_miss_t> ()
        "ze_miss_t.nb", 20, offset<ze_miss_t> "nb"
        "ze_deserializer_t", 24, size<ze_deserializer_t> ()
        "ze_owned_serializer_t", 64, size<ze_owned_serializer_t> ()
        "z_owned_shm_mut_t", 80, size<z_owned_shm_mut_t> ()
        "ze_owned_sample_miss_listener_t", 24, size<ze_owned_sample_miss_listener_t> ()
        "z_alloc_alignment_t", 1, size<z_alloc_alignment_t> ()
        "z_chunk_descriptor_t", 16, size<z_chunk_descriptor_t> ()
        "z_chunk_descriptor_t.len", 8, offset<z_chunk_descriptor_t> "len"
        "z_allocated_chunk_t", 24, size<z_allocated_chunk_t> ()
        "z_allocated_chunk_t.ptr", 16, offset<z_allocated_chunk_t> "ptr"
        "zc_threadsafe_context_t", 16, size<zc_threadsafe_context_t> ()
        "zc_threadsafe_context_t.delete_fn", 8, offset<zc_threadsafe_context_t> "delete_fn"
        "zc_shm_provider_backend_callbacks_t", 48, size<zc_shm_provider_backend_callbacks_t> ()
        "zc_shm_provider_backend_callbacks_t.id_fn", 40, offset<zc_shm_provider_backend_callbacks_t> "id_fn"
        "zc_shm_client_callbacks_t", 16, size<zc_shm_client_callbacks_t> ()
        "z_shm_segment_t", 24, size<z_shm_segment_t> ()
        "z_shm_segment_t.callbacks", 16, offset<z_shm_segment_t> "map_fn"
        "z_buf_alloc_result_t", 96, size<z_buf_alloc_result_t> ()
        "z_buf_alloc_result_t.buf", 8, offset<z_buf_alloc_result_t> "buf"
        "z_buf_alloc_result_t.error", 88, offset<z_buf_alloc_result_t> "error"
        "z_source_info_t", 24, size<z_source_info_t> ()
        "z_entity_global_id_t", 20, size<z_entity_global_id_t> ()
        "z_owned_cancellation_token_t", 24, size<z_owned_cancellation_token_t> ()
        "z_owned_session_t", 8, size<z_owned_session_t> ()
        "z_owned_config_t", 2064, size<z_owned_config_t> ()
        "z_view_keyexpr_t", 32, size<z_view_keyexpr_t> ()
        "z_owned_bytes_t", 40, size<z_owned_bytes_t> ()
        "z_owned_query_t", 144, size<z_owned_query_t> ()
    ]

[<Tests>]
let tests =
    testList
        "layout"
        [
            for name, c, fsharp in expected ->
                test name { Expect.equal fsharp c $"{name}: F# layout differs from C" }
        ]
