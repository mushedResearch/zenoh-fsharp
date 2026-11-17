// Print the size and field offsets of the concrete zenoh-c structs that Native/Types.fs mirrors,
// in the format of tests/Zenoh.FSharp.Tests/LayoutTests.fs. Build and run:
//   gcc -I native/include tools/layout-check.c -o /tmp/layout-check && /tmp/layout-check
#include <stddef.h>
#include <stdio.h>
#include "zenoh.h"

#define S(t) printf("\"%s\", %zu\n", #t, sizeof(t))
#define F(t, f) printf("\"%s.%s\", %zu\n", #t, #f, offsetof(t, f))

int main(void) {
    S(z_id_t);
    S(z_owned_closure_sample_t); S(z_owned_closure_query_t); S(z_owned_closure_reply_t); S(z_owned_closure_zid_t);
    S(z_put_options_t); F(z_put_options_t, is_express); F(z_put_options_t, timestamp); F(z_put_options_t, reliability);
    F(z_put_options_t, allowed_destination); F(z_put_options_t, source_info); F(z_put_options_t, attachment);
    S(z_delete_options_t); F(z_delete_options_t, timestamp); F(z_delete_options_t, allowed_destination);
    S(z_publisher_options_t); F(z_publisher_options_t, is_express); F(z_publisher_options_t, allowed_destination);
    S(z_publisher_put_options_t); F(z_publisher_put_options_t, attachment);
    S(z_subscriber_options_t);
    S(z_queryable_options_t); F(z_queryable_options_t, allowed_origin);
    S(z_query_reply_options_t); F(z_query_reply_options_t, timestamp); F(z_query_reply_options_t, attachment);
    S(z_query_reply_err_options_t);
    S(z_get_options_t); F(z_get_options_t, payload); F(z_get_options_t, congestion_control); F(z_get_options_t, is_express);
    F(z_get_options_t, allowed_destination); F(z_get_options_t, priority); F(z_get_options_t, attachment);
    F(z_get_options_t, timeout_ms); F(z_get_options_t, cancellation_token);
    S(z_liveliness_subscriber_options_t);
    S(z_liveliness_get_options_t); F(z_liveliness_get_options_t, cancellation_token);
    S(z_querier_options_t); F(z_querier_options_t, congestion_control); F(z_querier_options_t, is_express);
    F(z_querier_options_t, allowed_destination); F(z_querier_options_t, priority); F(z_querier_options_t, timeout_ms);
    S(z_querier_get_options_t); F(z_querier_get_options_t, attachment); F(z_querier_get_options_t, cancellation_token);
    S(z_matching_status_t);
    S(z_scout_options_t); F(z_scout_options_t, what);
    S(z_buf_layout_alloc_result_t); F(z_buf_layout_alloc_result_t, buf); F(z_buf_layout_alloc_result_t, alloc_error);
    F(z_buf_layout_alloc_result_t, layout_error);
    S(ze_advanced_publisher_cache_options_t); F(ze_advanced_publisher_cache_options_t, max_samples);
    F(ze_advanced_publisher_cache_options_t, is_express);
    S(ze_advanced_publisher_sample_miss_detection_options_t);
    F(ze_advanced_publisher_sample_miss_detection_options_t, heartbeat_period_ms);
    S(ze_advanced_publisher_options_t); F(ze_advanced_publisher_options_t, cache);
    F(ze_advanced_publisher_options_t, sample_miss_detection); F(ze_advanced_publisher_options_t, publisher_detection);
    F(ze_advanced_publisher_options_t, publisher_detection_metadata);
    S(ze_advanced_subscriber_history_options_t); F(ze_advanced_subscriber_history_options_t, max_samples);
    F(ze_advanced_subscriber_history_options_t, max_age_ms);
    S(ze_advanced_subscriber_recovery_options_t); F(ze_advanced_subscriber_recovery_options_t, last_sample_miss_detection);
    S(ze_advanced_subscriber_options_t); F(ze_advanced_subscriber_options_t, history);
    F(ze_advanced_subscriber_options_t, recovery); F(ze_advanced_subscriber_options_t, query_timeout_ms);
    F(ze_advanced_subscriber_options_t, subscriber_detection); F(ze_advanced_subscriber_options_t, subscriber_detection_metadata);
    S(ze_miss_t); F(ze_miss_t, nb);
    S(ze_deserializer_t); S(ze_owned_serializer_t); S(z_owned_shm_mut_t); S(ze_owned_sample_miss_listener_t);
    S(z_alloc_alignment_t); S(z_chunk_descriptor_t); F(z_chunk_descriptor_t, len);
    S(z_allocated_chunk_t); F(z_allocated_chunk_t, ptr);
    S(zc_threadsafe_context_t); F(zc_threadsafe_context_t, delete_fn);
    S(zc_shm_provider_backend_callbacks_t); F(zc_shm_provider_backend_callbacks_t, id_fn);
    S(zc_shm_client_callbacks_t); S(z_shm_segment_t); F(z_shm_segment_t, callbacks);
    S(z_buf_alloc_result_t); F(z_buf_alloc_result_t, buf); F(z_buf_alloc_result_t, error);
    S(z_source_info_t); S(z_entity_global_id_t); S(z_owned_cancellation_token_t);
    S(z_owned_session_t); S(z_owned_config_t); S(z_view_keyexpr_t); S(z_owned_bytes_t); S(z_owned_query_t);
    return 0;
}
