namespace Zenoh

open System
open System.Runtime.InteropServices
open System.Text
open Microsoft.FSharp.NativeInterop
open Zenoh.Native

#nowarn "9"  // native pointers
#nowarn "51" // address-of on locals

/// Raised when a zenoh-c call fails. Code is the z_result_t of the call (Z_EINVAL = -1, ...).
type ZenohException(operation: string, code: int) =
    inherit Exception($"{operation} failed with zenoh-c error {code}")
    member _.Operation = operation
    member _.Code = code

/// Process-wide settings of the zenoh-c runtime.
[<AbstractClass; Sealed>]
type ZenohRuntime private () =
    static let callbackError = Event<exn>()

    /// Raised when an F# handler called by zenoh (subscriber, queryable, get) throws.
    /// The exception cannot unwind into zenoh-c, so it is reported here instead.
    [<CLIEvent>]
    static member CallbackError = callbackError.Publish

    static member internal ReportCallbackError(ex: exn) =
        try
            callbackError.Trigger ex
        with _ ->
            ()

    /// Enable the logs of zenoh-c, filtered by the RUST_LOG environment variable,
    /// else by fallbackFilter (for example "info" or "zenoh=debug").
    static member InitLogging(?fallbackFilter: string) =
        Interop.zc_init_log_from_env_or (defaultArg fallbackFilter "error") |> ignore

module internal Ffi =
    let check (operation: string) (rc: z_result_t) =
        if rc <> Result.Z_OK then raise (ZenohException(operation, int rc))

    /// Native memory for an owned zenoh-c value that outlives a call (session, publisher, ...).
    let alloc<'T when 'T: unmanaged> () : nativeptr<'T> =
        let size = unativeint sizeof<'T>
        let p = NativeMemory.AlignedAlloc(size, 8un)
        NativeMemory.Clear(p, size)
        NativePtr.ofVoidPtr p

    let free (p: nativeptr<'T>) =
        NativeMemory.AlignedFree(NativePtr.toVoidPtr p)

    /// A null-terminated UTF-8 copy of a string in native memory.
    type CString(s: string) =
        let ptr = Marshal.StringToCoTaskMemUTF8 s
        member _.Ptr = ptr

        interface IDisposable with
            member _.Dispose() = Marshal.FreeCoTaskMem ptr

    let loanedString (s: nativeptr<z_loaned_string_t>) =
        let len = Interop.z_string_len s
        if len = 0un then "" else Marshal.PtrToStringUTF8(Interop.z_string_data s, int len)

    /// Read a string that zenoh-c writes into an owned string, then drop it.
    let readOwnedString (write: nativeptr<z_owned_string_t> -> unit) =
        let mutable s = Unchecked.defaultof<z_owned_string_t>
        let ps = &&s
        write ps
        try
            loanedString (Interop.z_string_loan ps)
        finally
            Interop.z_string_drop ps

    let keyExprToString (ke: nativeptr<z_loaned_keyexpr_t>) =
        let mutable view = Unchecked.defaultof<z_view_string_t>
        Interop.z_keyexpr_as_view_string (ke, &&view)
        loanedString (Interop.z_view_string_loan &&view)

    let encodingToString (e: nativeptr<z_loaned_encoding_t>) =
        readOwnedString (fun s -> Interop.z_encoding_to_string (e, s))

    let zidToString (id: z_id_t) =
        let mutable id = id
        readOwnedString (fun s -> Interop.z_id_to_string (&&id, s))

    let bytesToArray (b: nativeptr<z_loaned_bytes_t>) : byte[] =
        let mutable slice = Unchecked.defaultof<z_owned_slice_t>
        let ps = &&slice
        check "z_bytes_to_slice" (Interop.z_bytes_to_slice (b, ps))
        try
            let loaned = Interop.z_slice_loan ps
            let len = int (Interop.z_slice_len loaned)
            let result = Array.zeroCreate<byte> len
            if len > 0 then Marshal.Copy(Interop.z_slice_data loaned, result, 0, len)
            result
        finally
            Interop.z_slice_drop ps

    let optionalBytes (b: nativeptr<z_loaned_bytes_t>) =
        if NativePtr.isNullPtr b then None else Some(bytesToArray b)

    /// Fill an owned bytes with a copy of data.
    let writeBytes (data: ReadOnlySpan<byte>) (dst: nativeptr<z_owned_bytes_t>) =
        if data.IsEmpty then
            Interop.z_bytes_empty dst
        else
            use p = fixed data
            check "z_bytes_copy_from_buf" (Interop.z_bytes_copy_from_buf (dst, p, unativeint data.Length))

    let writeEncoding (encoding: string) (dst: nativeptr<z_owned_encoding_t>) =
        check $"z_encoding_from_str(\"{encoding}\")" (Interop.z_encoding_from_str (dst, encoding))

    let utf8 (s: string) = Encoding.UTF8.GetBytes s

    /// Call f with a key expression borrowed from a string; throws ArgumentException when the
    /// string is not a canonical key expression.
    let withKeyExpr (expr: string) (f: nativeptr<z_loaned_keyexpr_t> -> 'a) : 'a =
        if obj.ReferenceEquals(expr, null) then nullArg "keyExpr"
        use cs = new CString(expr)
        let mutable view = Unchecked.defaultof<z_view_keyexpr_t>
        if Interop.z_view_keyexpr_from_str (&&view, cs.Ptr) <> Result.Z_OK then
            invalidArg "keyExpr" $"'{expr}' is not a valid canonical key expression"
        f (Interop.z_view_keyexpr_loan &&view)

/// Bridge from zenoh-c closures (context pointer + call/drop C functions) to F# functions.
/// The context is a GCHandle of a ClosureTarget; zenoh-c calls drop exactly once, which frees it.
module internal Closures =
    type CallDelegate = delegate of nativeint * nativeint -> unit
    type DropDelegate = delegate of nativeint -> unit

    /// call receives the loaned argument of the callback (sample, query, reply or id) as a pointer.
    type Target(call: nativeint -> unit, drop: unit -> unit) =
        member _.Call arg = call arg
        member _.Drop() = drop ()

    let private onCall (arg: nativeint) (context: nativeint) =
        try
            match GCHandle.FromIntPtr(context).Target with
            | :? Target as t -> t.Call arg
            | _ -> ()
        with ex ->
            ZenohRuntime.ReportCallbackError ex

    let private onDrop (context: nativeint) =
        let handle = GCHandle.FromIntPtr context
        try
            try
                match handle.Target with
                | :? Target as t -> t.Drop()
                | _ -> ()
            with ex ->
                ZenohRuntime.ReportCallbackError ex
        finally
            handle.Free()

    // Kept in static fields so the function pointers stay valid for the life of the process.
    let private callDelegate = CallDelegate onCall
    let private dropDelegate = DropDelegate onDrop
    let private callPtr = Marshal.GetFunctionPointerForDelegate callDelegate
    let private dropPtr = Marshal.GetFunctionPointerForDelegate dropDelegate

    /// Initialise dst with a closure constructor of zenoh-c (z_closure_sample, z_closure_query, ...).
    let create
        (ctor: nativeptr<z_owned_closure_t> * nativeint * nativeint * nativeint -> unit)
        (target: Target)
        (dst: nativeptr<z_owned_closure_t>)
        =
        let handle = GCHandle.Alloc target
        ctor (dst, callPtr, dropPtr, GCHandle.ToIntPtr handle)
