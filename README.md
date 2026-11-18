# Zenoh.FSharp

F# bindings for [Eclipse Zenoh](https://zenoh.io), over the C API of
[zenoh-c](https://github.com/eclipse-zenoh/zenoh-c) 1.10.2.

```fsharp
open Zenoh

use session = Session.Open(Config.defaults |> Config.mode Peer)

use sub = session.DeclareSubscriber("demo/**", fun s -> printfn "%s: %s" s.KeyExpr s.PayloadString)
session.Put("demo/hello", "world", encoding = "text/plain")

use q = session.DeclareQueryable("demo/answer", fun q -> q.Reply("demo/answer", "42"))
let replies = session.Get("demo/answer").Result   // Reply list: ReplyOk sample | ReplyErr error
```

## Setup

The native library is not in the repository. Fetch the prebuilt zenoh-c release first:

```sh
scripts/fetch-zenohc.sh            # version from Directory.Build.props, linux-x64
scripts/fetch-zenohc.sh 1.10.2 osx-arm64
dotnet build
dotnet run --project tests/Zenoh.FSharp.Tests
```

The script puts the library in `native/<rid>/` and the headers in `native/include/`. The build
copies the library of the build machine next to the assembly, and `dotnet pack` puts every fetched
library under `runtimes/<rid>/native/`.

## API

| zenoh concept | F# |
|---|---|
| config | `Config` (immutable): `Config.defaults`, `ofJson5`, `ofFile`, `mode`, `connect`, `listen`, `multicastScouting`, `timestamping`, `set key json5`, `toJson5` |
| session | `Session.Open(?config, ?shmClients)`: `Id`, `EntityId`, `Peers`, `Routers`, `Close`, `Dispose` |
| put / delete | `session.Put(key, payload, ?encoding, ?attachment, ?priority, ...)`, `session.Delete(key)` |
| publisher | `session.DeclarePublisher(key, ?encoding, ...)`: `Put`, `Delete`, `MatchingStatus`, `DeclareMatchingListener` |
| subscriber | `session.DeclareSubscriber(key, handler)`, or `session.Subscribe(key, ?capacity)`: a `SampleStream` that is a `ChannelReader<Sample>` and an `IAsyncEnumerable<Sample>` |
| queryable | `session.DeclareQueryable(key, handler)`: the handler gets a `Query` with `Reply`, `ReplyErr`, `ReplyDelete`, `Detach` |
| get | `session.Get(key, ?parameters, ?payload, ?timeout, ?cancellationToken, ...)`: `Task<Reply list>`; `session.GetStream(...)`: `ChannelReader<Reply>`. Cancelling the `CancellationToken` cancels the query in zenoh (also for queriers and liveliness gets) |
| entity ids, source info | `EntityId` (`Zid`, `Eid`) on sessions and every declaration; `Sample.SourceInfo` and `Query.SourceInfo`; a `?sourceInfo` argument on puts, replies and gets |
| querier | `session.DeclareQuerier(key, ?target, ?timeout, ...)`: `Get`, `GetStream`, `MatchingStatus`, `DeclareMatchingListener` |
| liveliness | `session.DeclareLivelinessToken(key)`; `DeclareLivelinessSubscriber(key, handler, ?history)` or `SubscribeLiveliness`: Put when a token appears, Delete when it goes; `LivelinessGet(key)` |
| scouting | `Scouting.Scout(?config, ?what, ?timeout)`: `Task<Hello list>` (zid, `WhatAmI`, locators); `Scouting.ScoutStream` |
| shared memory | `new ShmProvider(poolSize)` or `ShmProvider.Posix { Size; Alignment }`: `Alloc(length, ?alignment)` or `Alloc(span)` gives a `ShmBuffer` (write its `Span`); `Layout(size, ?alignment)` precomputes a layout; `Put`/`Reply` overloads take a `ShmBuffer` and move it into zenoh without a copy |
| custom shared memory | `ShmProvider.Custom(backend)` with an `IShmProviderBackend` allocator written in .NET; receivers open their session with an `IShmClient` of the same protocol (`Session.Open(config, shmClients = [ client ])`) |
| zenoh-ext pub/sub | `session.DeclareAdvancedPublisher(key, ?cache, ?sampleMissDetection, ?publisherDetection, ...)`; `session.DeclareAdvancedSubscriber(key, handler, ?history, ?detectLatePublishers, ?recovery, ...)`: `DeclareSampleMissListener`, `DetectPublishers` |
| serialization | `ZSerde.serialize value` / `ZSerde.deserialize<'T> bytes` for numbers, strings, `byte[]`, arrays, lists, tuples, `Map`, `Dictionary`; `ZSerializer` / `ZDeserializer` value by value. The format of zenoh-ext, read by the other bindings |
| key expressions | `KeyExpr.isCanon`, `canonize`, `intersects`, `includes` |

Rules that come from zenoh-c:

- Handlers run on zenoh threads. Keep them short, and do not dispose the subscriber, queryable
  or session from inside a handler. An exception that a handler throws cannot go back into
  zenoh-c: it is raised on `ZenohRuntime.CallbackError`.
- A `Query` is valid until the queryable handler returns. To reply later, call `query.Detach()` in
  the handler and dispose the detached query after the last reply: the get completes when every
  copy of the query is gone.
- Samples, replies and query fields are copied into F# values, so they stay valid after the handler.
- Dispose sessions, publishers, subscribers and queryables (`use`). There are no finalizers.
- `ZenohRuntime.InitLogging "info"` turns on the logs of zenoh-c (`RUST_LOG` overrides the filter).
- Shared memory, source info, cancellation and the zenoh-ext advanced pub/sub are unstable in
  zenoh-c: their API may change.
- A shared-memory allocation size must be a multiple of its alignment, and the alignment at
  most that of the provider (`ShmProvider.Posix` sets it; the default provider has 1).
- `IShmProviderBackend` and `IShmClient` run on zenoh threads, possibly at the same time: they
  must be thread-safe. A backend keeps the memory of a chunk valid until zenoh calls `Free`.
- An advanced publisher with a cache but no sample miss detection needs timestamping
  (`Config.timestamping true`).

## Layout

| path | content |
|---|---|
| `src/Zenoh.FSharp/Native/Opaque.g.fs` | sizes of the zenoh-c opaque types, generated from the zenoh-c headers by `tools/GenOpaque.fsx` |
| `src/Zenoh.FSharp/Native/Types.fs`, `Interop.fs` | concrete structs and `DllImport` declarations, written from `zenoh_commons.h` |
| `src/Zenoh.FSharp/*.fs` | the F# API: `Session.fs` (core), `Liveliness.fs`, `Querier.fs`, `Scouting.fs`, `Shm.fs`, `Advanced.fs` (zenoh-ext pub/sub), `Serialization.fs`; `Handlers.fs` holds the ownership and closure helpers they share |
| `tests/Zenoh.FSharp.Tests` | Expecto tests: struct layouts against C, and behaviour against libzenohc |
| `samples/Zenoh.Samples` | `sub`, `pub`, `get`, `queryable` command-line examples |
| `tools/layout-check.c` | prints the C layouts that `LayoutTests.fs` expects |
| `docs/routes.md` | the three ways to bind zenoh from F#, and why this one first |

## Updating zenoh-c

1. Change `ZenohCVersion` in `Directory.Build.props` and run `scripts/fetch-zenohc.sh`.
2. Run `dotnet fsi tools/GenOpaque.fsx` to regenerate the opaque type sizes.
3. Run `gcc -I native/include tools/layout-check.c -o /tmp/layout-check && /tmp/layout-check`, compare
   with `tests/Zenoh.FSharp.Tests/LayoutTests.fs`, and update `Native/Types.fs` where a struct changed.
4. Check the changed signatures of the functions in `Native/Interop.fs`, then run the tests.

The option structs mirror the official prebuilt zenoh-c, which is built with
`Z_FEATURE_UNSTABLE_API`. A zenoh-c built without it has other layouts.

## License

zenoh-c is licensed under EPL-2.0 OR Apache-2.0.
