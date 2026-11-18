namespace Zenoh.Native

// P/Invoke declarations of the zenoh-c functions the F# API uses, written from zenoh_commons.h.
//
// Conventions of zenoh-c and how they map here:
// - `z_owned_X_t*` and `z_moved_X_t*` are both passed as `z_owned_X_t*`: a moved type is a
//   struct holding the owned type, so the pointers are the same. Passing it moves ownership.
// - `z_loaned_X_t*` is a borrow of an owned value, valid while the owner is alive.
// - C `bool` is returned as `byte` so that no marshalling (4-byte Win32 BOOL) applies.
// - `const char*` arguments that zenoh-c only reads during the call are marshalled as UTF-8;
//   strings that must outlive the call (key expression views) are passed as `nativeint`.

open System.Runtime.InteropServices
open Zenoh

#nowarn "9"

module Interop =
    [<Literal>]
    let Lib = "zenohc"

    // Logging
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t zc_init_log_from_env_or([<MarshalAs(UnmanagedType.LPUTF8Str)>] string fallback_filter)

    // Config
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_config_default(z_owned_config_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t zc_config_from_str(z_owned_config_t* this_, [<MarshalAs(UnmanagedType.LPUTF8Str)>] string s)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_config_t* z_config_loan_mut(z_owned_config_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_config_t* z_config_loan(z_owned_config_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t zc_config_insert_json5(
        z_loaned_config_t* this_,
        [<MarshalAs(UnmanagedType.LPUTF8Str)>] string key,
        [<MarshalAs(UnmanagedType.LPUTF8Str)>] string value)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t zc_config_to_string(z_loaned_config_t* config, z_owned_string_t* out_config_string)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_config_drop(z_owned_config_t* this_)

    // Session
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_open(z_owned_session_t* this_, z_owned_config_t* config, nativeint options)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_session_t* z_session_loan(z_owned_session_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_close(z_loaned_session_t* session, nativeint options)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern byte z_session_is_closed(z_loaned_session_t* session)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_session_drop(z_owned_session_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_id_t z_info_zid(z_loaned_session_t* session)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_id_to_string(z_id_t* zid, z_owned_string_t* dst)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_closure_zid(z_owned_closure_t* this_, nativeint call, nativeint drop, nativeint context)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_info_peers_zid(z_loaned_session_t* session, z_owned_closure_t* callback)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_info_routers_zid(z_loaned_session_t* session, z_owned_closure_t* callback)

    // Strings and slices
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_string_t* z_string_loan(z_owned_string_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_string_t* z_view_string_loan(z_view_string_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern nativeint z_string_data(z_loaned_string_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern unativeint z_string_len(z_loaned_string_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_string_drop(z_owned_string_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_slice_t* z_slice_loan(z_owned_slice_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern nativeint z_slice_data(z_loaned_slice_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern unativeint z_slice_len(z_loaned_slice_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_slice_drop(z_owned_slice_t* this_)

    // Bytes
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_bytes_copy_from_buf(z_owned_bytes_t* this_, byte* data, unativeint len)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_bytes_empty(z_owned_bytes_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_bytes_to_slice(z_loaned_bytes_t* this_, z_owned_slice_t* dst)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern unativeint z_bytes_len(z_loaned_bytes_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_bytes_t* z_bytes_loan(z_owned_bytes_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_bytes_drop(z_owned_bytes_t* this_)

    // Encoding
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_encoding_from_str(z_owned_encoding_t* this_, [<MarshalAs(UnmanagedType.LPUTF8Str)>] string s)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_encoding_to_string(z_loaned_encoding_t* this_, z_owned_string_t* out_str)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_encoding_drop(z_owned_encoding_t* this_)

    // Key expressions
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_view_keyexpr_from_str(z_view_keyexpr_t* this_, nativeint expr)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_keyexpr_t* z_view_keyexpr_loan(z_view_keyexpr_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_keyexpr_as_view_string(z_loaned_keyexpr_t* this_, z_view_string_t* out_string)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern byte z_keyexpr_intersects(z_loaned_keyexpr_t* left, z_loaned_keyexpr_t* right)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern byte z_keyexpr_includes(z_loaned_keyexpr_t* left, z_loaned_keyexpr_t* right)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_keyexpr_is_canon(byte* start, unativeint len)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_keyexpr_canonize(byte* start, unativeint* len)

    // Put / delete
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_put_options_default(z_put_options_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_put(
        z_loaned_session_t* session,
        z_loaned_keyexpr_t* key_expr,
        z_owned_bytes_t* payload,
        z_put_options_t* options)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_delete_options_default(z_delete_options_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_delete(z_loaned_session_t* session, z_loaned_keyexpr_t* key_expr, z_delete_options_t* options)

    // Publisher
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_publisher_options_default(z_publisher_options_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_declare_publisher(
        z_loaned_session_t* session,
        z_owned_publisher_t* publisher,
        z_loaned_keyexpr_t* key_expr,
        z_publisher_options_t* options)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_publisher_t* z_publisher_loan(z_owned_publisher_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_keyexpr_t* z_publisher_keyexpr(z_loaned_publisher_t* publisher)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_publisher_put_options_default(z_publisher_put_options_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_publisher_put(
        z_loaned_publisher_t* this_,
        z_owned_bytes_t* payload,
        z_publisher_put_options_t* options)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_publisher_delete(z_loaned_publisher_t* publisher, nativeint options)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_undeclare_publisher(z_owned_publisher_t* this_)

    // Subscriber
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_closure_sample(z_owned_closure_t* this_, nativeint call, nativeint drop, nativeint context)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_subscriber_options_default(z_subscriber_options_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_declare_subscriber(
        z_loaned_session_t* session,
        z_owned_subscriber_t* subscriber,
        z_loaned_keyexpr_t* key_expr,
        z_owned_closure_t* callback,
        z_subscriber_options_t* options)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_undeclare_subscriber(z_owned_subscriber_t* this_)

    // Samples
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_keyexpr_t* z_sample_keyexpr(z_loaned_sample_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_bytes_t* z_sample_payload(z_loaned_sample_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern SampleKind z_sample_kind(z_loaned_sample_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_encoding_t* z_sample_encoding(z_loaned_sample_t* this_)

    /// Returns null when the sample has no attachment.
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_bytes_t* z_sample_attachment(z_loaned_sample_t* this_)

    /// Returns null when the sample has no timestamp.
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_timestamp_t* z_sample_timestamp(z_loaned_sample_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern Priority z_sample_priority(z_loaned_sample_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern CongestionControl z_sample_congestion_control(z_loaned_sample_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern byte z_sample_express(z_loaned_sample_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern uint64 z_timestamp_ntp64_time(z_timestamp_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_id_t z_timestamp_id(z_timestamp_t* this_)

    // Queryable
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_closure_query(z_owned_closure_t* this_, nativeint call, nativeint drop, nativeint context)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_queryable_options_default(z_queryable_options_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_declare_queryable(
        z_loaned_session_t* session,
        z_owned_queryable_t* queryable,
        z_loaned_keyexpr_t* key_expr,
        z_owned_closure_t* callback,
        z_queryable_options_t* options)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_undeclare_queryable(z_owned_queryable_t* this_)

    // Queries
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_query_clone(z_owned_query_t* dst, z_loaned_query_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_query_t* z_query_loan(z_owned_query_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_query_drop(z_owned_query_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_keyexpr_t* z_query_keyexpr(z_loaned_query_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_query_parameters(z_loaned_query_t* this_, z_view_string_t* parameters)

    /// Returns null when the query has no payload.
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_bytes_t* z_query_payload(z_loaned_query_t* this_)

    /// Returns null when the query has no encoding.
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_encoding_t* z_query_encoding(z_loaned_query_t* this_)

    /// Returns null when the query has no attachment.
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_bytes_t* z_query_attachment(z_loaned_query_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_query_reply_options_default(z_query_reply_options_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_query_reply(
        z_loaned_query_t* this_,
        z_loaned_keyexpr_t* key_expr,
        z_owned_bytes_t* payload,
        z_query_reply_options_t* options)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_query_reply_err(
        z_loaned_query_t* this_,
        z_owned_bytes_t* payload,
        z_query_reply_err_options_t* options)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_query_reply_del(z_loaned_query_t* this_, z_loaned_keyexpr_t* key_expr, nativeint options)

    // Get
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_closure_reply(z_owned_closure_t* this_, nativeint call, nativeint drop, nativeint context)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_get_options_default(z_get_options_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_get(
        z_loaned_session_t* session,
        z_loaned_keyexpr_t* key_expr,
        [<MarshalAs(UnmanagedType.LPUTF8Str)>] string parameters,
        z_owned_closure_t* callback,
        z_get_options_t* options)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern byte z_reply_is_ok(z_loaned_reply_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_sample_t* z_reply_ok(z_loaned_reply_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_reply_err_t* z_reply_err(z_loaned_reply_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_bytes_t* z_reply_err_payload(z_loaned_reply_err_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_encoding_t* z_reply_err_encoding(z_loaned_reply_err_t* this_)

    // Entity ids
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_id_t z_entity_global_id_zid(z_entity_global_id_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern uint32 z_entity_global_id_eid(z_entity_global_id_t* this_)

    // String arrays
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_string_array_t* z_string_array_loan(z_owned_string_array_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern unativeint z_string_array_len(z_loaned_string_array_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_string_t* z_string_array_get(z_loaned_string_array_t* this_, unativeint index)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_string_array_drop(z_owned_string_array_t* this_)

    // Liveliness
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_liveliness_declare_token(
        z_loaned_session_t* session,
        z_owned_liveliness_token_t* token,
        z_loaned_keyexpr_t* key_expr,
        nativeint options)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_liveliness_undeclare_token(z_owned_liveliness_token_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_liveliness_subscriber_options_default(z_liveliness_subscriber_options_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_liveliness_declare_subscriber(
        z_loaned_session_t* session,
        z_owned_subscriber_t* subscriber,
        z_loaned_keyexpr_t* key_expr,
        z_owned_closure_t* callback,
        z_liveliness_subscriber_options_t* options)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_liveliness_get_options_default(z_liveliness_get_options_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_liveliness_get(
        z_loaned_session_t* session,
        z_loaned_keyexpr_t* key_expr,
        z_owned_closure_t* callback,
        z_liveliness_get_options_t* options)

    // Querier
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_querier_options_default(z_querier_options_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_declare_querier(
        z_loaned_session_t* session,
        z_owned_querier_t* querier,
        z_loaned_keyexpr_t* key_expr,
        z_querier_options_t* options)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_querier_t* z_querier_loan(z_owned_querier_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_keyexpr_t* z_querier_keyexpr(z_loaned_querier_t* querier)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_querier_get_options_default(z_querier_get_options_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_querier_get(
        z_loaned_querier_t* querier,
        [<MarshalAs(UnmanagedType.LPUTF8Str)>] string parameters,
        z_owned_closure_t* callback,
        z_querier_get_options_t* options)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_undeclare_querier(z_owned_querier_t* this_)

    // Matching
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_closure_matching_status(z_owned_closure_t* this_, nativeint call, nativeint drop, nativeint context)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_publisher_get_matching_status(z_loaned_publisher_t* this_, z_matching_status_t* matching_status)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_publisher_declare_matching_listener(
        z_loaned_publisher_t* publisher,
        z_owned_matching_listener_t* matching_listener,
        z_owned_closure_t* callback)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_querier_get_matching_status(z_loaned_querier_t* this_, z_matching_status_t* matching_status)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_querier_declare_matching_listener(
        z_loaned_querier_t* querier,
        z_owned_matching_listener_t* matching_listener,
        z_owned_closure_t* callback)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_undeclare_matching_listener(z_owned_matching_listener_t* this_)

    // Scouting
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_closure_hello(z_owned_closure_t* this_, nativeint call, nativeint drop, nativeint context)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_scout_options_default(z_scout_options_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_scout(z_owned_config_t* config, z_owned_closure_t* callback, z_scout_options_t* options)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_id_t z_hello_zid(z_loaned_hello_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern WhatAmI z_hello_whatami(z_loaned_hello_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_hello_locators(z_loaned_hello_t* this_, z_owned_string_array_t* locators_out)

    // Shared memory (unstable in zenoh-c)
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_shm_provider_default_new(z_owned_shm_provider_t* this_, unativeint size)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_shm_provider_t* z_shm_provider_loan(z_owned_shm_provider_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_shm_provider_drop(z_owned_shm_provider_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern unativeint z_shm_provider_available(z_loaned_shm_provider_t* provider)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern unativeint z_shm_provider_garbage_collect(z_loaned_shm_provider_t* provider)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern unativeint z_shm_provider_defragment(z_loaned_shm_provider_t* provider)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_shm_provider_alloc_gc_defrag(
        z_buf_layout_alloc_result_t* out_result,
        z_loaned_shm_provider_t* provider,
        unativeint size)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_shm_mut_t* z_shm_mut_loan_mut(z_owned_shm_mut_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern nativeint z_shm_mut_data_mut(z_loaned_shm_mut_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern unativeint z_shm_mut_len(z_loaned_shm_mut_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_shm_mut_drop(z_owned_shm_mut_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_bytes_from_shm_mut(z_owned_bytes_t* this_, z_owned_shm_mut_t* shm)

    // zenoh-ext: advanced publisher
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void ze_advanced_publisher_options_default(ze_advanced_publisher_options_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_declare_advanced_publisher(
        z_loaned_session_t* session,
        ze_owned_advanced_publisher_t* publisher,
        z_loaned_keyexpr_t* key_expr,
        ze_advanced_publisher_options_t* options)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern ze_loaned_advanced_publisher_t* ze_advanced_publisher_loan(ze_owned_advanced_publisher_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_keyexpr_t* ze_advanced_publisher_keyexpr(ze_loaned_advanced_publisher_t* publisher)

    /// options: ze_advanced_publisher_put_options_t, which only wraps z_publisher_put_options_t.
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_advanced_publisher_put(
        ze_loaned_advanced_publisher_t* this_,
        z_owned_bytes_t* payload,
        z_publisher_put_options_t* options)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_advanced_publisher_delete(ze_loaned_advanced_publisher_t* publisher, nativeint options)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_advanced_publisher_get_matching_status(
        ze_loaned_advanced_publisher_t* this_,
        z_matching_status_t* matching_status)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_advanced_publisher_declare_matching_listener(
        ze_loaned_advanced_publisher_t* publisher,
        z_owned_matching_listener_t* matching_listener,
        z_owned_closure_t* callback)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_undeclare_advanced_publisher(ze_owned_advanced_publisher_t* this_)

    // zenoh-ext: advanced subscriber
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void ze_advanced_subscriber_options_default(ze_advanced_subscriber_options_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_declare_advanced_subscriber(
        z_loaned_session_t* session,
        ze_owned_advanced_subscriber_t* subscriber,
        z_loaned_keyexpr_t* key_expr,
        z_owned_closure_t* callback,
        ze_advanced_subscriber_options_t* options)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern ze_loaned_advanced_subscriber_t* ze_advanced_subscriber_loan(ze_owned_advanced_subscriber_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_keyexpr_t* ze_advanced_subscriber_keyexpr(ze_loaned_advanced_subscriber_t* subscriber)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void ze_closure_miss(z_owned_closure_t* this_, nativeint call, nativeint drop, nativeint context)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_advanced_subscriber_declare_sample_miss_listener(
        ze_loaned_advanced_subscriber_t* subscriber,
        ze_owned_sample_miss_listener_t* sample_miss_listener,
        z_owned_closure_t* callback)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_undeclare_sample_miss_listener(ze_owned_sample_miss_listener_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_advanced_subscriber_detect_publishers(
        ze_loaned_advanced_subscriber_t* subscriber,
        z_owned_subscriber_t* liveliness_subscriber,
        z_owned_closure_t* callback,
        z_liveliness_subscriber_options_t* options)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_undeclare_advanced_subscriber(ze_owned_advanced_subscriber_t* this_)

    // zenoh-ext: serialization
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_serializer_empty(ze_owned_serializer_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern ze_loaned_serializer_t* ze_serializer_loan_mut(ze_owned_serializer_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void ze_serializer_finish(ze_owned_serializer_t* this_, z_owned_bytes_t* bytes)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void ze_serializer_drop(ze_owned_serializer_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_serializer_serialize_bool(ze_loaned_serializer_t* this_, byte value)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_serializer_serialize_int8(ze_loaned_serializer_t* this_, int8 value)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_serializer_serialize_int16(ze_loaned_serializer_t* this_, int16 value)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_serializer_serialize_int32(ze_loaned_serializer_t* this_, int value)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_serializer_serialize_int64(ze_loaned_serializer_t* this_, int64 value)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_serializer_serialize_uint8(ze_loaned_serializer_t* this_, byte value)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_serializer_serialize_uint16(ze_loaned_serializer_t* this_, uint16 value)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_serializer_serialize_uint32(ze_loaned_serializer_t* this_, uint32 value)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_serializer_serialize_uint64(ze_loaned_serializer_t* this_, uint64 value)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_serializer_serialize_float(ze_loaned_serializer_t* this_, float32 value)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_serializer_serialize_double(ze_loaned_serializer_t* this_, float value)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_serializer_serialize_substr(ze_loaned_serializer_t* this_, byte* start, unativeint len)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_serializer_serialize_buf(ze_loaned_serializer_t* this_, byte* data, unativeint len)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_serializer_serialize_sequence_length(ze_loaned_serializer_t* this_, unativeint len)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern ze_deserializer_t ze_deserializer_from_bytes(z_loaned_bytes_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern byte ze_deserializer_is_done(ze_deserializer_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_deserializer_deserialize_bool(ze_deserializer_t* this_, byte* dst)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_deserializer_deserialize_int8(ze_deserializer_t* this_, int8* dst)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_deserializer_deserialize_int16(ze_deserializer_t* this_, int16* dst)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_deserializer_deserialize_int32(ze_deserializer_t* this_, int* dst)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_deserializer_deserialize_int64(ze_deserializer_t* this_, int64* dst)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_deserializer_deserialize_uint8(ze_deserializer_t* this_, byte* dst)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_deserializer_deserialize_uint16(ze_deserializer_t* this_, uint16* dst)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_deserializer_deserialize_uint32(ze_deserializer_t* this_, uint32* dst)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_deserializer_deserialize_uint64(ze_deserializer_t* this_, uint64* dst)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_deserializer_deserialize_float(ze_deserializer_t* this_, float32* dst)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_deserializer_deserialize_double(ze_deserializer_t* this_, float* dst)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_deserializer_deserialize_string(ze_deserializer_t* this_, z_owned_string_t* str)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_deserializer_deserialize_slice(ze_deserializer_t* this_, z_owned_slice_t* slice)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t ze_deserializer_deserialize_sequence_length(ze_deserializer_t* this_, unativeint* len)

    // Entity ids of the declarations
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_entity_global_id_t z_session_id(z_loaned_session_t* session)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_entity_global_id_t z_publisher_id(z_loaned_publisher_t* publisher)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_subscriber_t* z_subscriber_loan(z_owned_subscriber_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_entity_global_id_t z_subscriber_id(z_loaned_subscriber_t* subscriber)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_queryable_t* z_queryable_loan(z_owned_queryable_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_entity_global_id_t z_queryable_id(z_loaned_queryable_t* queryable)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_entity_global_id_t z_querier_id(z_loaned_querier_t* querier)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_entity_global_id_t ze_advanced_publisher_id(ze_loaned_advanced_publisher_t* publisher)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_entity_global_id_t ze_advanced_subscriber_id(ze_loaned_advanced_subscriber_t* subscriber)

    // Source info (unstable)
    /// Returns null when the sample has no source info.
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_source_info_t* z_sample_source_info(z_loaned_sample_t* this_)

    /// Returns null when the query has no source info.
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_source_info_t* z_query_source_info(z_loaned_query_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_source_info_t z_source_info_new(z_entity_global_id_t* source_id, uint32 source_sn)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_entity_global_id_t z_source_info_id(z_source_info_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern uint32 z_source_info_sn(z_source_info_t* this_)

    // Cancellation tokens (unstable)
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_cancellation_token_new(z_owned_cancellation_token_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_cancellation_token_t* z_cancellation_token_loan_mut(z_owned_cancellation_token_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_cancellation_token_clone(z_owned_cancellation_token_t* dst, z_loaned_cancellation_token_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_cancellation_token_cancel(z_loaned_cancellation_token_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_cancellation_token_drop(z_owned_cancellation_token_t* this_)

    // Shared memory: alignment, layouts, custom providers and clients (unstable)
    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_shm_provider_alloc_gc_defrag_aligned(
        z_buf_layout_alloc_result_t* out_result,
        z_loaned_shm_provider_t* provider,
        unativeint size,
        z_alloc_alignment_t alignment)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_shm_provider_alloc_layout_aligned(
        z_owned_precomputed_layout_t* this_,
        z_loaned_shm_provider_t* provider,
        unativeint size,
        z_alloc_alignment_t alignment)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_precomputed_layout_t* z_precomputed_layout_loan(z_owned_precomputed_layout_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_precomputed_layout_alloc_gc_defrag(z_buf_alloc_result_t* out_result, z_loaned_precomputed_layout_t* layout)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_precomputed_layout_drop(z_owned_precomputed_layout_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_memory_layout_new(z_owned_memory_layout_t* this_, unativeint size, z_alloc_alignment_t alignment)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_memory_layout_t* z_memory_layout_loan(z_owned_memory_layout_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_memory_layout_get_data(
        z_loaned_memory_layout_t* this_,
        unativeint* out_size,
        z_alloc_alignment_t* out_alignment)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_memory_layout_drop(z_owned_memory_layout_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_posix_shm_provider_with_layout_new(z_owned_shm_provider_t* this_, z_loaned_memory_layout_t* layout)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_shm_provider_threadsafe_new(
        z_owned_shm_provider_t* this_,
        zc_threadsafe_context_t context,
        zc_shm_provider_backend_callbacks_t callbacks)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_ptr_in_segment_new(z_owned_ptr_in_segment_t* this_, nativeint ptr, zc_threadsafe_context_t segment)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_chunk_alloc_result_new_ok(z_owned_chunk_alloc_result_t* this_, z_allocated_chunk_t allocated_chunk)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_chunk_alloc_result_new_error(z_owned_chunk_alloc_result_t* this_, int alloc_error)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_shm_client_new(z_owned_shm_client_t* this_, zc_threadsafe_context_t context, zc_shm_client_callbacks_t callbacks)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void zc_shm_client_list_new(zc_owned_shm_client_list_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern zc_loaned_shm_client_list_t* zc_shm_client_list_loan_mut(zc_owned_shm_client_list_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t zc_shm_client_list_add_client(zc_loaned_shm_client_list_t* this_, z_owned_shm_client_t* client)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void zc_shm_client_list_drop(zc_owned_shm_client_list_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_shm_client_storage_new(
        z_owned_shm_client_storage_t* this_,
        zc_loaned_shm_client_list_t* clients,
        byte add_default_client_set)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_loaned_shm_client_storage_t* z_shm_client_storage_loan(z_owned_shm_client_storage_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void z_shm_client_storage_drop(z_owned_shm_client_storage_t* this_)

    [<DllImport(Lib, CallingConvention = CallingConvention.Cdecl)>]
    extern z_result_t z_open_with_custom_shm_clients(
        z_owned_session_t* this_,
        z_owned_config_t* config,
        z_loaned_shm_client_storage_t* shm_clients)
