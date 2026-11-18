namespace Zenoh

open System
open System.Text
open Microsoft.FSharp.NativeInterop
open Zenoh.Native

#nowarn "9"
#nowarn "51"

/// Global id of a zenoh entity (session, publisher, subscriber, ...): the zenoh id of its
/// session and the id of the entity in that session.
[<Sealed>]
type EntityId internal (native: z_entity_global_id_t) =
    let zid, eid =
        let mutable id = native
        Ffi.zidToString (Interop.z_entity_global_id_zid &&id), Interop.z_entity_global_id_eid &&id

    member _.Zid = zid
    member _.Eid = eid
    member internal _.Native = native

    override _.Equals other =
        match other with
        | :? EntityId as o -> o.Zid = zid && o.Eid = eid
        | _ -> false

    override _.GetHashCode() = HashCode.Combine(zid, eid)
    override _.ToString() = $"{zid}:{eid}"

/// Where a sample or query comes from: the entity that sent it and its sequence number.
/// Set it when publishing (sourceInfo argument) to tell receivers; unstable in zenoh-c.
type SourceInfo =
    {
        Source: EntityId
        SequenceNumber: uint32
    }

module internal SourceInfoNative =
    let read (p: nativeptr<z_source_info_t>) =
        if NativePtr.isNullPtr p then
            None
        else
            Some
                {
                    Source = EntityId(Interop.z_source_info_id p)
                    SequenceNumber = Interop.z_source_info_sn p
                }

    /// Call f with a pointer to the zenoh-c source info (0 for None), valid during the call.
    let using (sourceInfo: SourceInfo option) (f: nativeint -> 'a) : 'a =
        match sourceInfo with
        | None -> f 0n
        | Some si ->
            let mutable id = si.Source.Native
            let mutable native = Interop.z_source_info_new (&&id, si.SequenceNumber)
            f (NativePtr.toNativeInt &&native)

/// Hybrid logical clock timestamp of a sample: an NTP64 time and the id of the clock.
type Timestamp =
    {
        /// Upper 32 bits: seconds since the UNIX epoch; lower 32 bits: fraction of a second.
        Ntp64: uint64
        /// Zenoh id of the clock that made the timestamp.
        Id: string
    }

    member this.ToDateTimeOffset() =
        let seconds = int64 (this.Ntp64 >>> 32)
        let fraction = this.Ntp64 &&& 0xFFFF_FFFFUL
        let ticks = int64 ((fraction * uint64 TimeSpan.TicksPerSecond) >>> 32)
        DateTimeOffset.UnixEpoch.AddSeconds(float seconds).AddTicks ticks

/// A value published on a key expression, copied out of zenoh-c.
type Sample =
    {
        KeyExpr: string
        Payload: byte[]
        Kind: SampleKind
        /// Encoding as zenoh writes it, such as "zenoh/bytes" or "text/plain;charset".
        Encoding: string
        Timestamp: Timestamp option
        Attachment: byte[] option
        Priority: Priority
        CongestionControl: CongestionControl
        Express: bool
        SourceInfo: SourceInfo option
    }

    /// The payload decoded as UTF-8.
    member this.PayloadString = Encoding.UTF8.GetString this.Payload

/// The error a queryable replied with.
type ReplyError =
    {
        Payload: byte[]
        Encoding: string
    }

    member this.PayloadString = Encoding.UTF8.GetString this.Payload

/// One reply to a get.
type Reply =
    | ReplyOk of Sample
    | ReplyErr of ReplyError

module internal SampleReader =
    let private timestamp (ts: nativeptr<z_timestamp_t>) =
        if NativePtr.isNullPtr ts then
            None
        else
            Some
                {
                    Ntp64 = Interop.z_timestamp_ntp64_time ts
                    Id = Ffi.zidToString (Interop.z_timestamp_id ts)
                }

    let read (s: nativeptr<z_loaned_sample_t>) : Sample =
        {
            KeyExpr = Ffi.keyExprToString (Interop.z_sample_keyexpr s)
            Payload = Ffi.bytesToArray (Interop.z_sample_payload s)
            Kind = Interop.z_sample_kind s
            Encoding = Ffi.encodingToString (Interop.z_sample_encoding s)
            Timestamp = timestamp (Interop.z_sample_timestamp s)
            Attachment = Ffi.optionalBytes (Interop.z_sample_attachment s)
            Priority = Interop.z_sample_priority s
            CongestionControl = Interop.z_sample_congestion_control s
            Express = Interop.z_sample_express s <> 0uy
            SourceInfo = SourceInfoNative.read (Interop.z_sample_source_info s)
        }

    let readReply (r: nativeptr<z_loaned_reply_t>) : Reply =
        if Interop.z_reply_is_ok r <> 0uy then
            ReplyOk(read (Interop.z_reply_ok r))
        else
            let err = Interop.z_reply_err r
            ReplyErr
                {
                    Payload = Ffi.bytesToArray (Interop.z_reply_err_payload err)
                    Encoding = Ffi.encodingToString (Interop.z_reply_err_encoding err)
                }
