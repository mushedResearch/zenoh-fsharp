namespace Zenoh

// Scouting: discover the routers and peers reachable by multicast (or gossip), without a session.

open System
open System.Threading
open System.Threading.Channels
open System.Threading.Tasks
open Microsoft.FSharp.NativeInterop
open Zenoh.Native

#nowarn "9"
#nowarn "51"

/// A zenoh node found by scouting.
type Hello =
    {
        Zid: string
        WhatAmI: WhatAmI
        /// Endpoints where the node listens, such as "tcp/192.168.1.2:7447".
        Locators: string list
    }

[<AbstractClass; Sealed>]
type Scouting private () =
    static let readHello (h: nativeptr<z_loaned_hello_t>) =
        let mutable locators = Unchecked.defaultof<z_owned_string_array_t>
        let pLocators = &&locators
        Interop.z_hello_locators (h, pLocators)

        let list =
            try
                let loaned = Interop.z_string_array_loan pLocators

                [
                    for i in 0 .. int (Interop.z_string_array_len loaned) - 1 do
                        Ffi.loanedString (Interop.z_string_array_get (loaned, unativeint i))
                ]
            finally
                Interop.z_string_array_drop pLocators

        {
            Zid = Ffi.zidToString (Interop.z_hello_zid h)
            WhatAmI = Interop.z_hello_whatami h
            Locators = list
        }

    /// Scout for the kinds of nodes in `what` (default: routers and peers) during `timeout`
    /// (default: 1 s). Each node found arrives on the channel, which completes at the timeout.
    /// The config sets how to scout (multicast address and interface, ...).
    static member ScoutStream(?config: Config, ?what: WhatAmI, ?timeout: TimeSpan) : ChannelReader<Hello> =
        let channel = Channel.CreateUnbounded<Hello>()

        let target =
            Closures.Target(
                (fun p -> channel.Writer.TryWrite(readHello (NativePtr.ofNativeInt p)) |> ignore),
                (fun () -> channel.Writer.TryComplete() |> ignore)
            )

        let mutable cfg = Unchecked.defaultof<z_owned_config_t>
        let pCfg = &&cfg
        Config.write (defaultArg config Config.defaults) pCfg

        let mutable o = Unchecked.defaultof<z_scout_options_t>
        let pOptions = &&o
        Interop.z_scout_options_default pOptions

        match what with
        | Some w -> o.what <- w
        | None -> ()

        match timeout with
        | Some t -> o.timeout_ms <- Handlers.timeoutMs t
        | None -> ()

        // z_scout moves the config and the closure.
        Handlers.withClosure Interop.z_closure_hello target (fun closure -> Interop.z_scout (pCfg, closure, pOptions))
        |> Ffi.check "z_scout"

        channel.Reader

    /// Scout and collect the nodes found (see ScoutStream).
    static member Scout(?config: Config, ?what: WhatAmI, ?timeout: TimeSpan, ?cancellationToken: CancellationToken) : Task<Hello list> =
        let reader = Scouting.ScoutStream(?config = config, ?what = what, ?timeout = timeout)
        Handlers.collect reader (defaultArg cancellationToken CancellationToken.None)
