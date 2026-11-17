namespace Zenoh

// zenoh-ext serialization: the binary format of z_serialize / z_deserialize in Rust and of the
// other bindings. Integers and floats are little-endian; strings, byte arrays and sequences are
// prefixed with their length (a LEB128 integer); tuples are their fields one after the other.

open System
open System.Collections
open System.Collections.Generic
open System.Text
open Microsoft.FSharp.NativeInterop
open Microsoft.FSharp.Reflection
open Zenoh.Native

#nowarn "9"
#nowarn "51"
// ZSerde walks values by reflection, whose APIs are typed as nullable; the values it reads and
// builds are never null.
#nowarn "3261"
#nowarn "3264"

/// Writes values in the zenoh serialization format. Finish returns the bytes.
[<Sealed>]
type ZSerializer() =
    let handle =
        Owned(
            "ZSerializer",
            Handlers.declareOwned (fun p -> Ffi.check "ze_serializer_empty" (Interop.ze_serializer_empty p)),
            Interop.ze_serializer_drop
        )

    let s () = Interop.ze_serializer_loan_mut handle.Ptr

    /// zenoh-c reads the data during the call; a 1-byte array stands in for an empty one, so that
    /// the pointer is never null.
    let withData (data: byte[]) (f: nativeptr<byte> -> unativeint -> z_result_t) =
        let pinned = if data.Length = 0 then [| 0uy |] else data
        use p = fixed pinned
        f p (unativeint data.Length)

    member _.WriteBool(v: bool) =
        Ffi.check "ze_serializer_serialize_bool" (Interop.ze_serializer_serialize_bool (s (), Handlers.cbool v))

    member _.WriteInt8(v: sbyte) =
        Ffi.check "ze_serializer_serialize_int8" (Interop.ze_serializer_serialize_int8 (s (), v))

    member _.WriteInt16(v: int16) =
        Ffi.check "ze_serializer_serialize_int16" (Interop.ze_serializer_serialize_int16 (s (), v))

    member _.WriteInt32(v: int) =
        Ffi.check "ze_serializer_serialize_int32" (Interop.ze_serializer_serialize_int32 (s (), v))

    member _.WriteInt64(v: int64) =
        Ffi.check "ze_serializer_serialize_int64" (Interop.ze_serializer_serialize_int64 (s (), v))

    member _.WriteUInt8(v: byte) =
        Ffi.check "ze_serializer_serialize_uint8" (Interop.ze_serializer_serialize_uint8 (s (), v))

    member _.WriteUInt16(v: uint16) =
        Ffi.check "ze_serializer_serialize_uint16" (Interop.ze_serializer_serialize_uint16 (s (), v))

    member _.WriteUInt32(v: uint32) =
        Ffi.check "ze_serializer_serialize_uint32" (Interop.ze_serializer_serialize_uint32 (s (), v))

    member _.WriteUInt64(v: uint64) =
        Ffi.check "ze_serializer_serialize_uint64" (Interop.ze_serializer_serialize_uint64 (s (), v))

    member _.WriteFloat32(v: float32) =
        Ffi.check "ze_serializer_serialize_float" (Interop.ze_serializer_serialize_float (s (), v))

    member _.WriteFloat64(v: float) =
        Ffi.check "ze_serializer_serialize_double" (Interop.ze_serializer_serialize_double (s (), v))

    /// A UTF-8 string, prefixed with its length in bytes.
    member _.WriteString(v: string) =
        let serializer = s ()

        withData (Encoding.UTF8.GetBytes v) (fun p len -> Interop.ze_serializer_serialize_substr (serializer, p, len))
        |> Ffi.check "ze_serializer_serialize_substr"

    /// Bytes, prefixed with their length.
    member _.WriteBytes(v: byte[]) =
        let serializer = s ()

        withData v (fun p len -> Interop.ze_serializer_serialize_buf (serializer, p, len))
        |> Ffi.check "ze_serializer_serialize_buf"

    /// The length of a sequence; write its elements after it.
    member _.WriteSequenceLength(length: int) =
        Ffi.check
            "ze_serializer_serialize_sequence_length"
            (Interop.ze_serializer_serialize_sequence_length (s (), unativeint length))

    /// The bytes written. The serializer cannot be used after.
    member _.Finish() : byte[] =
        handle.Move(fun p ->
            let mutable bytes = Unchecked.defaultof<z_owned_bytes_t>
            let pBytes = &&bytes
            Interop.ze_serializer_finish (p, pBytes)

            try
                Ffi.bytesToArray (Interop.z_bytes_loan pBytes)
            finally
                Interop.z_bytes_drop pBytes)

    interface IDisposable with
        member _.Dispose() = handle.Dispose()

/// Reads values in the zenoh serialization format from bytes. A read that does not match the
/// data raises ZenohException.
[<Sealed>]
type ZDeserializer(data: byte[]) =
    let bytes =
        Owned("ZDeserializer", Handlers.declareOwned (Moved.bytes data), Interop.z_bytes_drop)

    // The deserializer state refers to the bytes, so both live in native memory.
    let state =
        let p = Ffi.alloc<ze_deserializer_t> ()
        NativePtr.write p (Interop.ze_deserializer_from_bytes (Interop.z_bytes_loan bytes.Ptr))
        p

    let d () =
        bytes.Ptr |> ignore // throws ObjectDisposedException after Dispose
        state

    let read operation (deserialize: nativeptr<ze_deserializer_t> -> nativeptr<'T> -> z_result_t) : 'T =
        let mutable v = Unchecked.defaultof<'T>
        let p = &&v
        Ffi.check operation (deserialize (d ()) p)
        v

    member _.ReadBool() =
        read "ze_deserializer_deserialize_bool" (fun d p -> Interop.ze_deserializer_deserialize_bool (d, p)) <> 0uy

    member _.ReadInt8() =
        read "ze_deserializer_deserialize_int8" (fun d p -> Interop.ze_deserializer_deserialize_int8 (d, p))

    member _.ReadInt16() =
        read "ze_deserializer_deserialize_int16" (fun d p -> Interop.ze_deserializer_deserialize_int16 (d, p))

    member _.ReadInt32() =
        read "ze_deserializer_deserialize_int32" (fun d p -> Interop.ze_deserializer_deserialize_int32 (d, p))

    member _.ReadInt64() =
        read "ze_deserializer_deserialize_int64" (fun d p -> Interop.ze_deserializer_deserialize_int64 (d, p))

    member _.ReadUInt8() =
        read "ze_deserializer_deserialize_uint8" (fun d p -> Interop.ze_deserializer_deserialize_uint8 (d, p))

    member _.ReadUInt16() =
        read "ze_deserializer_deserialize_uint16" (fun d p -> Interop.ze_deserializer_deserialize_uint16 (d, p))

    member _.ReadUInt32() =
        read "ze_deserializer_deserialize_uint32" (fun d p -> Interop.ze_deserializer_deserialize_uint32 (d, p))

    member _.ReadUInt64() =
        read "ze_deserializer_deserialize_uint64" (fun d p -> Interop.ze_deserializer_deserialize_uint64 (d, p))

    member _.ReadFloat32() =
        read "ze_deserializer_deserialize_float" (fun d p -> Interop.ze_deserializer_deserialize_float (d, p))

    member _.ReadFloat64() =
        read "ze_deserializer_deserialize_double" (fun d p -> Interop.ze_deserializer_deserialize_double (d, p))

    member _.ReadString() =
        let mutable s = Unchecked.defaultof<z_owned_string_t>
        let p = &&s
        Ffi.check "ze_deserializer_deserialize_string" (Interop.ze_deserializer_deserialize_string (d (), p))

        try
            Ffi.loanedString (Interop.z_string_loan p)
        finally
            Interop.z_string_drop p

    member _.ReadBytes() : byte[] =
        let mutable slice = Unchecked.defaultof<z_owned_slice_t>
        let p = &&slice
        Ffi.check "ze_deserializer_deserialize_slice" (Interop.ze_deserializer_deserialize_slice (d (), p))

        try
            let loaned = Interop.z_slice_loan p
            let result = Array.zeroCreate<byte> (int (Interop.z_slice_len loaned))

            if result.Length > 0 then
                Runtime.InteropServices.Marshal.Copy(Interop.z_slice_data loaned, result, 0, result.Length)

            result
        finally
            Interop.z_slice_drop p

    member _.ReadSequenceLength() =
        int (
            read "ze_deserializer_deserialize_sequence_length" (fun d p ->
                Interop.ze_deserializer_deserialize_sequence_length (d, p))
        )

    /// Whether all the data was read.
    member _.IsDone = Interop.ze_deserializer_is_done (d ()) <> 0uy

    interface IDisposable with
        member _.Dispose() =
            if not bytes.IsDisposed then
                bytes.Dispose()
                Ffi.free state

/// Serialize F# values in the zenoh format. Supported: bool, integers, float32, float, string,
/// byte[] (as bytes), arrays, lists, ResizeArray and other IList<'T> (as sequences), tuples,
/// Map and Dictionary (as sequences of key-value pairs), and any combination of them.
module ZSerde =
    let private isGeneric (def: Type) (t: Type) =
        t.IsGenericType && t.GetGenericTypeDefinition() = def

    let private elementType (t: Type) =
        if t.IsArray then t.GetElementType() else t.GetGenericArguments()[0]

    let rec private write (s: ZSerializer) (t: Type) (v: obj) =
        match v with
        | :? bool as x -> s.WriteBool x
        | :? sbyte as x -> s.WriteInt8 x
        | :? int16 as x -> s.WriteInt16 x
        | :? int as x -> s.WriteInt32 x
        | :? int64 as x -> s.WriteInt64 x
        | :? byte as x -> s.WriteUInt8 x
        | :? uint16 as x -> s.WriteUInt16 x
        | :? uint32 as x -> s.WriteUInt32 x
        | :? uint64 as x -> s.WriteUInt64 x
        | :? float32 as x -> s.WriteFloat32 x
        | :? float as x -> s.WriteFloat64 x
        | :? string as x -> s.WriteString x
        | :? (byte[]) as x -> s.WriteBytes x
        | _ when FSharpType.IsTuple t ->
            Array.iter2 (write s) (FSharpType.GetTupleElements t) (FSharpValue.GetTupleFields v)
        | _ when isGeneric typedefof<Map<_, _>> t || isGeneric typedefof<Dictionary<_, _>> t ->
            let args = t.GetGenericArguments()
            let items = (v :?> IEnumerable) |> Seq.cast<obj> |> Seq.toArray
            s.WriteSequenceLength items.Length

            for kv in items do
                let kvType = kv.GetType()
                write s args[0] (kvType.GetProperty("Key").GetValue kv)
                write s args[1] (kvType.GetProperty("Value").GetValue kv)
        | _ when t.IsArray || isGeneric typedefof<list<_>> t || isGeneric typedefof<List<_>> t ->
            let element = elementType t
            let items = (v :?> IEnumerable) |> Seq.cast<obj> |> Seq.toArray
            s.WriteSequenceLength items.Length

            for item in items do
                write s element item
        | _ -> raise (NotSupportedException $"zenoh serialization does not support {t}")

    let rec private read (d: ZDeserializer) (t: Type) : obj =
        if t = typeof<bool> then box (d.ReadBool())
        elif t = typeof<sbyte> then box (d.ReadInt8())
        elif t = typeof<int16> then box (d.ReadInt16())
        elif t = typeof<int> then box (d.ReadInt32())
        elif t = typeof<int64> then box (d.ReadInt64())
        elif t = typeof<byte> then box (d.ReadUInt8())
        elif t = typeof<uint16> then box (d.ReadUInt16())
        elif t = typeof<uint32> then box (d.ReadUInt32())
        elif t = typeof<uint64> then box (d.ReadUInt64())
        elif t = typeof<float32> then box (d.ReadFloat32())
        elif t = typeof<float> then box (d.ReadFloat64())
        elif t = typeof<string> then box (d.ReadString())
        elif t = typeof<byte[]> then box (d.ReadBytes())
        elif FSharpType.IsTuple t then
            FSharpValue.MakeTuple(FSharpType.GetTupleElements t |> Array.map (read d), t)
        elif isGeneric typedefof<Map<_, _>> t || isGeneric typedefof<Dictionary<_, _>> t then
            let args = t.GetGenericArguments()
            let n = d.ReadSequenceLength()
            let pairs = Array.init n (fun _ -> let k = read d args[0] in k, read d args[1])

            if isGeneric typedefof<Dictionary<_, _>> t then
                let dict = Activator.CreateInstance t :?> IDictionary
                for k, v in pairs do
                    dict[k] <- v
                box dict
            else
                let tupleType = FSharpType.MakeTupleType args
                let tuples = Array.CreateInstance(tupleType, n)

                pairs |> Array.iteri (fun i (k, v) -> tuples.SetValue(FSharpValue.MakeTuple([| k; v |], tupleType), i))

                Activator.CreateInstance(t, [| box tuples |])
        elif t.IsArray || isGeneric typedefof<list<_>> t || isGeneric typedefof<List<_>> t then
            let element = elementType t
            let n = d.ReadSequenceLength()
            let items = Array.CreateInstance(element, n)

            for i in 0 .. n - 1 do
                items.SetValue(read d element, i)

            if t.IsArray then
                box items
            elif isGeneric typedefof<List<_>> t then
                Activator.CreateInstance(t, [| box items |])
            else
                // F# list: build from the end with Cons.
                let empty = t.GetProperty("Empty").GetValue null
                let cons = t.GetMethod "Cons"

                Seq.foldBack (fun item acc -> cons.Invoke(null, [| item; acc |])) (items |> Seq.cast<obj>) empty
        else
            raise (NotSupportedException $"zenoh serialization does not support {t}")

    /// The zenoh serialization of value.
    let serialize<'T> (value: 'T) : byte[] =
        use s = new ZSerializer()
        write s typeof<'T> (box value)
        s.Finish()

    /// Deserialize data as a 'T; raises ZenohException when data is not a whole 'T.
    let deserialize<'T> (data: byte[]) : 'T =
        use d = new ZDeserializer(data)
        let value = read d typeof<'T> :?> 'T

        if not d.IsDone then
            raise (ZenohException("ZSerde.deserialize: data left after the value", int Result.Z_EDESERIALIZE))

        value
