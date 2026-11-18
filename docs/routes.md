# Three routes to Zenoh from F#

| | 1. Bind the C headers | 2. Generated C# from Rust (csbindgen) | 3. Implement the protocol |
|---|---|---|---|
| status | **done** (this repository) | not started | not started |
| needs | prebuilt zenoh-c | Rust toolchain, a csbindgen fix | only .NET |
| native dependency | libzenohc | libzenohc | none |
| features | all of zenoh-c (bound on demand) | all of zenoh-c | what we implement |
| upgrade cost | regenerate sizes, check changed structs | regenerate | follow protocol changes |
| main risk | struct layouts written by hand | macro-generated Rust types | size of the work, interop |

## 1. Bind the C headers (implemented)

The F# code calls zenoh-c through `DllImport`:

- **Opaque types** (`z_owned_session_t`, `z_owned_bytes_t`, ...) are byte blobs of fixed size and
  alignment in `zenoh_opaque.h` (and two in `zenoh_commons.h`). `tools/GenOpaque.fsx` generates them as F# structs of the same
  size and alignment, so they can live on the stack (`let mutable x = ...` and `&&x`).
- **Concrete structs** (option structs, closures) and the function declarations are written by hand
  from `zenoh_commons.h`, only for the functions the F# API uses. `LayoutTests.fs` checks every
  size and offset against the C compiler (`tools/layout-check.c`).
- **Callbacks**: one pair of static C function pointers serves all closures; the closure context is
  a `GCHandle` of the F# handler, freed when zenoh-c calls the drop function.
- **Ownership**: values that outlive a call (session, publisher, subscriber, queryable, detached
  query) are in native memory, released once by `Dispose`. Values that a call moves (payload,
  encoding, attachment, config) are built on the stack just before the call.

Bound: sessions, put/delete, publishers, subscribers, queryables, gets, queriers, matching
listeners, liveliness, scouting, entity ids and source info, cancellation of gets, shared memory
(default, POSIX with a layout, and custom providers and clients written in .NET; alignment and
precomputed layouts), the advanced pub/sub of zenoh-ext and its serialization. Not bound: the
deprecated publication cache and querying subscriber, the asynchronous SHM allocations, and the
FIFO/ring channel handlers of zenoh-c (the F# API uses .NET channels instead).

## 2. C# generated from Rust with csbindgen

[csbindgen](https://github.com/Cysharp/csbindgen) reads Rust `extern "C"` functions and writes C#
`DllImport` declarations; F# then references the generated C# project.

What it needs:

- A Rust toolchain (not installed on this machine) and a checkout of zenoh-c at the release tag.
- A `build.rs` (or a small crate) that runs csbindgen over the zenoh-c sources.
- The fix of the csbindgen bug seen before. Its details are not recorded here: describe it (or the
  error) to continue this route.

Expected difficulty: zenoh-c does not declare its C types as plain Rust structs. The owned, loaned
and moved types come from macros (`decl_c_type!`), and the opaque sizes are computed by its
`build.rs` for each target. csbindgen does not expand macros, so these types are likely missing or
wrong in its output. If that is the bug, two ways out:

- run csbindgen on macro-expanded sources (`cargo expand`), or
- generate C# from the C headers instead of the Rust sources, with ClangSharpPInvokeGenerator.
  The headers are what cbindgen produced from the same Rust code, with the sizes already resolved:
  this is route 1 with a generator in place of the hand-written declarations.

What it would give over route 1: every function of zenoh-c without writing declarations. The F#
API of this repository could then sit on the generated layer instead of `Native/Interop.fs`.

## 3. Implement the Zenoh protocol in F#

A managed implementation that speaks the zenoh 1.x wire protocol to routers and peers, with no
native library.

Minimal client, in order:

1. Codec: variable-length integers (LEB128), message headers and extensions, zero-copy buffers.
2. Unicast transport over TCP: frames prefixed by a 16-bit little-endian length; the session
   handshake (InitSyn, InitAck, OpenSyn, OpenAck), keep-alive, lease, close, fragmentation.
3. Network messages: Declare (key expressions, subscribers, queryables), Push (Put, Del),
   Request / Response / ResponseFinal (Query, Reply, Err), Interest.
4. Client mode first (connect to a zenohd router), then peer mode (scouting, gossip, routing).

Route 1 is the test oracle: an F# protocol client can be tested in-process against a libzenohc
session that listens on a local port, message by message.

Cost: the largest of the three by far, and it has to follow changes of the protocol. It pays off
where a native library is not possible (WebAssembly, restricted platforms) or for a client-only
subset that must be small.

## Recommendation

Keep route 1 as the base. Take route 2 only if the bound subset of zenoh-c grows large; prefer the
header-based generator variant, which avoids the Rust macros. Consider route 3 for a client-mode
subset when a native dependency is not acceptable.
