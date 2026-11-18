namespace Zenoh

// Shared memory: providers that allocate buffers (the default POSIX one, or a custom backend
// written in F#), buffers, layouts, and the clients that let a session read the buffers of a
// custom backend. Unstable in zenoh-c: the API may change in a later release.

open System
open System.Numerics
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Zenoh.Native

#nowarn "9"
#nowarn "51"

/// Size and alignment of a shared-memory allocation. Alignment is a power of two, in bytes,
/// and Size a multiple of it.
[<Struct>]
type ShmLayout = { Size: int; Alignment: int }

/// Memory a custom backend allocated: its address in this process, and the ids by which the
/// IShmClient of a receiving session finds the same memory (Segment, then Chunk in the segment).
[<Struct>]
type ShmChunk =
    {
        Segment: uint32
        Chunk: uint32
        Length: int
        Address: nativeint
    }

/// A shared-memory allocator written in .NET, for ShmProvider.Custom. zenoh calls it from its
/// own threads, possibly at the same time: implementations must be thread-safe. Exceptions are
/// reported on ZenohRuntime.CallbackError and count as a failed call.
type IShmProviderBackend =
    /// Id of the protocol, shared with the IShmClient of the receivers. Incompatible backends
    /// must never use the same id.
    abstract ProtocolId: uint32
    /// Allocate memory for layout; None when there is no room.
    abstract Alloc: layout: ShmLayout -> ShmChunk option
    /// Free a chunk returned by Alloc; zenoh calls it once nobody uses the chunk any more.
    abstract Free: segment: uint32 * chunk: uint32 * length: int -> unit
    /// Defragment; returns the size of the largest free block.
    abstract Defragment: unit -> int64
    /// Free bytes.
    abstract Available: int64
    /// The layout the backend uses to serve a request (for example rounded up to its block
    /// size), or None when it cannot serve it.
    abstract LayoutFor: layout: ShmLayout -> ShmLayout option

/// A segment attached by an IShmClient.
type IShmSegment =
    /// Address in this process of a chunk of the segment.
    abstract Map: chunk: uint32 -> nativeint

/// Reads the buffers of a custom shared-memory protocol in a receiving session: pass it to
/// Session.Open. Must be thread-safe, like IShmProviderBackend.
type IShmClient =
    abstract ProtocolId: uint32
    /// Attach to a segment of the protocol; None when it is not available.
    abstract Attach: segment: uint32 -> IShmSegment option

/// A shared-memory buffer from a ShmProvider. Write it through Span, then publish it
/// (Session.Put, Publisher.Put, Query.Reply): publishing moves the buffer into zenoh and makes it
/// unusable here. Dispose frees a buffer that was not published.
[<Sealed>]
type ShmBuffer internal (owned: nativeptr<z_owned_shm_mut_t>) =
    let handle = Owned("ShmBuffer", owned, Interop.z_shm_mut_drop)

    member _.Length = int (Interop.z_shm_mut_len (Interop.z_shm_mut_loan_mut handle.Ptr))

    /// The memory of the buffer, valid until the buffer is published or disposed.
    member this.Span: Span<byte> =
        let loaned = Interop.z_shm_mut_loan_mut handle.Ptr
        Span<byte>(NativePtr.toVoidPtr (NativePtr.ofNativeInt<byte> (Interop.z_shm_mut_data_mut loaned)), this.Length)

    /// Payload writer that moves the buffer into the bytes of a message.
    member internal _.WritePayload(dst: nativeptr<z_owned_bytes_t>) =
        handle.Move(fun p -> Ffi.check "z_bytes_from_shm_mut" (Interop.z_bytes_from_shm_mut (dst, p)))

    interface IDisposable with
        member _.Dispose() = handle.Dispose()

/// Raised when a ShmProvider cannot allocate a buffer.
type ShmAllocException(size: int, reason: string) =
    inherit Exception($"Shared-memory allocation of {size} bytes failed: {reason}")
    member _.Size = size
    member _.Reason = reason

module internal ShmNative =
    let alignment (bytes: int) =
        if bytes <= 0 || not (BitOperations.IsPow2 bytes) then
            invalidArg "alignment" $"{bytes} is not a power of two"

        let mutable a = Unchecked.defaultof<z_alloc_alignment_t>
        a.pow <- byte (BitOperations.Log2(uint32 bytes))
        a

    /// The alignment of an allocation of size bytes; zenoh needs the size to be a multiple of it.
    let layoutAlignment (size: int) (bytes: int) =
        let a = alignment bytes

        if size % bytes <> 0 then
            invalidArg "alignment" $"the size {size} is not a multiple of the alignment {bytes}"

        a

    let readLayout (layout: nativeptr<z_loaned_memory_layout_t>) =
        let mutable size = 0un
        let mutable align = Unchecked.defaultof<z_alloc_alignment_t>
        Interop.z_memory_layout_get_data (layout, &&size, &&align)
        { Size = int size; Alignment = 1 <<< int align.pow }

    let allocError (code: int) =
        match code with
        | 0 -> "the pool needs defragmentation"
        | 1 -> "out of memory"
        | _ -> "allocation error"

    /// Move an allocated buffer into native memory owned by a ShmBuffer.
    let buffer (buf: z_owned_shm_mut_t) =
        let owned = Ffi.alloc<z_owned_shm_mut_t> ()
        NativePtr.write owned buf
        new ShmBuffer(owned)

    let fromLayoutResult (length: int) (result: z_buf_layout_alloc_result_t) =
        match result.status with
        | zc_buf_layout_alloc_status_t.Ok -> buffer result.buf
        | zc_buf_layout_alloc_status_t.AllocError -> raise (ShmAllocException(length, allocError result.alloc_error))
        | _ -> raise (ShmAllocException(length, "the provider cannot serve this size and alignment"))

    // Callbacks of custom backends and clients. Their context is a GCHandle of the .NET object,
    // freed by the delete function zenoh-c calls once, after the last callback.

    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type AllocFn = delegate of nativeint * nativeint * nativeint -> unit

    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type FreeFn = delegate of nativeint * nativeint -> unit

    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type SizeFn = delegate of nativeint -> unativeint

    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type LayoutForFn = delegate of nativeint * nativeint -> unit

    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type IdFn = delegate of nativeint -> uint32

    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type DeleteFn = delegate of nativeint -> unit

    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type AttachFn = delegate of nativeint * uint32 * nativeint -> byte

    [<UnmanagedFunctionPointer(CallingConvention.Cdecl)>]
    type MapFn = delegate of uint32 * nativeint -> nativeint

    let private target<'T> (context: nativeint) =
        GCHandle.FromIntPtr(context).Target :?> 'T

    /// Run a callback body; on an exception, report it and return fallback.
    let private guard fallback (f: unit -> 'a) =
        try
            f ()
        with ex ->
            ZenohRuntime.ReportCallbackError ex
            fallback

    let private onDelete (context: nativeint) =
        if context <> 0n then GCHandle.FromIntPtr(context).Free()

    let private onNoopDelete (_: nativeint) = ()

    let private deleteFn = DeleteFn onDelete
    let private noopDeleteFn = DeleteFn onNoopDelete

    /// A thread-safe context holding a GCHandle of value.
    let gcContext (value: obj) =
        let mutable c = Unchecked.defaultof<zc_threadsafe_context_t>
        c.ptr <- GCHandle.ToIntPtr(GCHandle.Alloc value)
        c.delete_fn <- Marshal.GetFunctionPointerForDelegate deleteFn
        c

    // Provider backend

    let private onAlloc (result: nativeint) (layout: nativeint) (context: nativeint) =
        let result = NativePtr.ofNativeInt<z_owned_chunk_alloc_result_t> result

        let ok =
            guard false (fun () ->
                let backend = target<IShmProviderBackend> context

                match backend.Alloc(readLayout (NativePtr.ofNativeInt layout)) with
                | Some c ->
                    // The backend keeps the memory valid until Free: the segment context has
                    // nothing to release.
                    let mutable segment = Unchecked.defaultof<zc_threadsafe_context_t>
                    segment.delete_fn <- Marshal.GetFunctionPointerForDelegate noopDeleteFn
                    let mutable ptr = Unchecked.defaultof<z_owned_ptr_in_segment_t>
                    let pPtr = &&ptr
                    Interop.z_ptr_in_segment_new (pPtr, c.Address, segment)

                    let mutable chunk = Unchecked.defaultof<z_allocated_chunk_t>
                    chunk.descriptor.segment <- c.Segment
                    chunk.descriptor.chunk <- c.Chunk
                    chunk.descriptor.len <- unativeint c.Length
                    chunk.ptr <- pPtr
                    Interop.z_chunk_alloc_result_new_ok (result, chunk) = Result.Z_OK
                | None ->
                    Interop.z_chunk_alloc_result_new_error (result, 1) // Z_ALLOC_ERROR_OUT_OF_MEMORY
                    true)

        if not ok then Interop.z_chunk_alloc_result_new_error (result, 2) // Z_ALLOC_ERROR_OTHER

    let private onFree (chunk: nativeint) (context: nativeint) =
        guard () (fun () ->
            let c: z_chunk_descriptor_t = NativePtr.read (NativePtr.ofNativeInt chunk)
            (target<IShmProviderBackend> context).Free(c.segment, c.chunk, int c.len))

    let private onDefragment (context: nativeint) =
        guard 0un (fun () -> unativeint (max 0L ((target<IShmProviderBackend> context).Defragment())))

    let private onAvailable (context: nativeint) =
        guard 0un (fun () -> unativeint (max 0L (target<IShmProviderBackend> context).Available))

    /// The layout is in/out: keep it to accept the request, replace it to adjust it, drop it to
    /// refuse it.
    let private onLayoutFor (layout: nativeint) (context: nativeint) =
        let owned = NativePtr.ofNativeInt<z_owned_memory_layout_t> layout
        let requested = readLayout (Interop.z_memory_layout_loan owned)

        match guard None (fun () -> (target<IShmProviderBackend> context).LayoutFor requested) with
        | Some l when l = requested -> ()
        | Some l ->
            Interop.z_memory_layout_drop owned

            guard () (fun () ->
                Interop.z_memory_layout_new (owned, unativeint l.Size, alignment l.Alignment) |> ignore)
        | None -> Interop.z_memory_layout_drop owned

    let private onProviderId (context: nativeint) =
        guard 0u (fun () -> (target<IShmProviderBackend> context).ProtocolId)

    let private allocFn = AllocFn onAlloc
    let private freeFn = FreeFn onFree
    let private defragmentFn = SizeFn onDefragment
    let private availableFn = SizeFn onAvailable
    let private layoutForFn = LayoutForFn onLayoutFor
    let private providerIdFn = IdFn onProviderId

    let backendCallbacks () =
        let mutable c = Unchecked.defaultof<zc_shm_provider_backend_callbacks_t>
        c.alloc_fn <- Marshal.GetFunctionPointerForDelegate allocFn
        c.free_fn <- Marshal.GetFunctionPointerForDelegate freeFn
        c.defragment_fn <- Marshal.GetFunctionPointerForDelegate defragmentFn
        c.available_fn <- Marshal.GetFunctionPointerForDelegate availableFn
        c.layout_for_fn <- Marshal.GetFunctionPointerForDelegate layoutForFn
        c.id_fn <- Marshal.GetFunctionPointerForDelegate providerIdFn
        c

    // Client

    let private onMap (chunk: uint32) (context: nativeint) =
        guard 0n (fun () -> (target<IShmSegment> context).Map chunk)

    let private mapFn = MapFn onMap

    let private onAttach (segment: nativeint) (segmentId: uint32) (context: nativeint) =
        guard 0uy (fun () ->
            match (target<IShmClient> context).Attach segmentId with
            | Some s ->
                let out = NativePtr.ofNativeInt<z_shm_segment_t> segment
                let mutable value = Unchecked.defaultof<z_shm_segment_t>
                value.context <- gcContext s
                value.map_fn <- Marshal.GetFunctionPointerForDelegate mapFn
                NativePtr.write out value
                1uy
            | None -> 0uy)

    let private onClientId (context: nativeint) =
        guard 0u (fun () -> (target<IShmClient> context).ProtocolId)

    let private attachFn = AttachFn onAttach
    let private clientIdFn = IdFn onClientId

    /// Call f with a client storage holding the default clients and these.
    let withClientStorage (clients: IShmClient seq) (f: nativeptr<z_loaned_shm_client_storage_t> -> 'a) : 'a =
        let mutable list = Unchecked.defaultof<zc_owned_shm_client_list_t>
        let pList = &&list
        Interop.zc_shm_client_list_new pList

        try
            for client in clients do
                let mutable callbacks = Unchecked.defaultof<zc_shm_client_callbacks_t>
                callbacks.attach_fn <- Marshal.GetFunctionPointerForDelegate attachFn
                callbacks.id_fn <- Marshal.GetFunctionPointerForDelegate clientIdFn
                let mutable owned = Unchecked.defaultof<z_owned_shm_client_t>
                let pOwned = &&owned
                Interop.z_shm_client_new (pOwned, gcContext client, callbacks)

                Ffi.check
                    "zc_shm_client_list_add_client"
                    (Interop.zc_shm_client_list_add_client (Interop.zc_shm_client_list_loan_mut pList, pOwned))

            let mutable storage = Unchecked.defaultof<z_owned_shm_client_storage_t>
            let pStorage = &&storage

            Ffi.check
                "z_shm_client_storage_new"
                (Interop.z_shm_client_storage_new (pStorage, Interop.zc_shm_client_list_loan_mut pList, 1uy))

            try
                f (Interop.z_shm_client_storage_loan pStorage)
            finally
                Interop.z_shm_client_storage_drop pStorage
        finally
            Interop.zc_shm_client_list_drop pList

/// A precomputed allocation layout of a ShmProvider: allocations of one size and alignment,
/// without computing the layout each time. Dispose it before its provider.
[<Sealed>]
type ShmAllocLayout internal (owned: nativeptr<z_owned_precomputed_layout_t>, size: int, checkProvider: unit -> unit) =
    let handle = Owned("ShmAllocLayout", owned, Interop.z_precomputed_layout_drop)

    member _.Size = size

    /// Allocate a buffer, after garbage collection and defragmentation if the pool is full.
    /// Throws ShmAllocException when the pool has no room.
    member _.Alloc() =
        checkProvider ()
        let mutable result = Unchecked.defaultof<z_buf_alloc_result_t>
        Interop.z_precomputed_layout_alloc_gc_defrag (&&result, Interop.z_precomputed_layout_loan handle.Ptr)

        match result.status with
        | zc_buf_alloc_status_t.Ok -> ShmNative.buffer result.buf
        | _ -> raise (ShmAllocException(size, ShmNative.allocError result.error))

    interface IDisposable with
        member _.Dispose() = handle.Dispose()

/// A shared-memory provider: allocates the buffers that sessions publish without copies.
/// `new ShmProvider(size)` is the default (POSIX) provider of zenoh-c with a pool of `size`
/// bytes; ShmProvider.Posix sets the alignment of the pool; ShmProvider.Custom uses an allocator
/// written in .NET. Receiving sessions on the same machine read POSIX buffers by default; they
/// need an IShmClient (Session.Open) for the buffers of a custom backend.
[<Sealed>]
type ShmProvider private (owned: nativeptr<z_owned_shm_provider_t>) =
    let handle = Owned("ShmProvider", owned, Interop.z_shm_provider_drop)

    let loan () = Interop.z_shm_provider_loan handle.Ptr

    new(size: int) =
        new ShmProvider(
            Handlers.declareOwned (fun p ->
                Ffi.check "z_shm_provider_default_new" (Interop.z_shm_provider_default_new (p, unativeint size)))
        )

    /// A POSIX provider whose pool has the size and alignment of layout.
    static member Posix(layout: ShmLayout) =
        let mutable memoryLayout = Unchecked.defaultof<z_owned_memory_layout_t>
        let pLayout = &&memoryLayout

        Ffi.check
            "z_memory_layout_new"
            (Interop.z_memory_layout_new (pLayout, unativeint layout.Size, ShmNative.layoutAlignment layout.Size layout.Alignment))

        try
            new ShmProvider(
                Handlers.declareOwned (fun p ->
                    Ffi.check
                        "z_posix_shm_provider_with_layout_new"
                        (Interop.z_posix_shm_provider_with_layout_new (p, Interop.z_memory_layout_loan pLayout)))
            )
        finally
            Interop.z_memory_layout_drop pLayout

    /// A provider that allocates with backend.
    static member Custom(backend: IShmProviderBackend) =
        new ShmProvider(
            Handlers.declareOwned (fun p ->
                Interop.z_shm_provider_threadsafe_new (p, ShmNative.gcContext backend, ShmNative.backendCallbacks ()))
        )

    /// Free bytes in the pool.
    member _.Available = int64 (Interop.z_shm_provider_available (loan ()))

    /// Allocate a buffer of `length` bytes aligned on `alignment` bytes (a power of two that
    /// divides length, at most the alignment of the provider: see ShmProvider.Posix), after
    /// garbage collection and
    /// defragmentation if the pool is full. Throws ShmAllocException when the pool has no room
    /// or cannot serve the alignment.
    member _.Alloc(length: int, ?alignment: int) =
        let mutable result = Unchecked.defaultof<z_buf_layout_alloc_result_t>

        match alignment with
        | Some a ->
            let alignment = ShmNative.layoutAlignment length a
            Interop.z_shm_provider_alloc_gc_defrag_aligned (&&result, loan (), unativeint length, alignment)
        | None -> Interop.z_shm_provider_alloc_gc_defrag (&&result, loan (), unativeint length)

        ShmNative.fromLayoutResult length result

    /// Allocate a buffer holding a copy of data.
    member this.Alloc(data: ReadOnlySpan<byte>, ?alignment: int) =
        let buffer = this.Alloc(data.Length, ?alignment = alignment)
        data.CopyTo buffer.Span
        buffer

    /// A precomputed layout for allocations of `size` bytes aligned on `alignment` bytes (at
    /// most the alignment of the provider). Throws ZenohException when the provider cannot
    /// serve it.
    member _.Layout(size: int, ?alignment: int) =
        let provider = loan ()
        let alignment = ShmNative.layoutAlignment size (defaultArg alignment 1)

        let owned =
            Handlers.declareOwned (fun p ->
                Ffi.check
                    "z_shm_provider_alloc_layout_aligned"
                    (Interop.z_shm_provider_alloc_layout_aligned (
                        p,
                        provider,
                        unativeint size,
                        alignment
                    )))

        new ShmAllocLayout(owned, size, (fun () -> handle.Ptr |> ignore))

    /// Reclaim the buffers no longer used by anyone; returns the bytes reclaimed.
    member _.GarbageCollect() = int64 (Interop.z_shm_provider_garbage_collect (loan ()))

    /// Defragment the pool; returns the size of the largest free block.
    member _.Defragment() = int64 (Interop.z_shm_provider_defragment (loan ()))

    interface IDisposable with
        member _.Dispose() = handle.Dispose()
