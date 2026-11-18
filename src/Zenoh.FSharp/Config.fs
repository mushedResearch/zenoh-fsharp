namespace Zenoh

open System.IO
open Zenoh.Native

#nowarn "9"
#nowarn "51"

/// Role of a session in the zenoh network.
type Mode =
    | Peer
    | Client
    | Router

    member this.Json5 =
        match this with
        | Peer -> "\"peer\""
        | Client -> "\"client\""
        | Router -> "\"router\""

/// An immutable zenoh configuration: a base (the zenoh defaults or a JSON5 document) and
/// settings applied over it in order. It becomes a zenoh-c config only when a session opens.
type Config =
    private
        {
            BaseJson5: string option
            /// (key path, JSON5 value), most recent first.
            Settings: (string * string) list
        }

module Config =
    /// The zenoh defaults (peer mode, multicast scouting on).
    let defaults = { BaseJson5 = None; Settings = [] }

    /// A configuration from a JSON5 document in the format of the zenoh configuration file.
    let ofJson5 (json5: string) = { BaseJson5 = Some json5; Settings = [] }

    let ofFile (path: string) = ofJson5 (File.ReadAllText path)

    /// Set the JSON5 value at a key path, such as "scouting/multicast/enabled" and "false".
    let set (key: string) (json5Value: string) (config: Config) =
        { config with Settings = (key, json5Value) :: config.Settings }

    let private jsonList (items: string seq) =
        items
        |> Seq.map (fun s -> "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"")
        |> String.concat ","
        |> sprintf "[%s]"

    let mode (m: Mode) = set "mode" m.Json5

    /// Endpoints to connect to, such as "tcp/127.0.0.1:7447".
    let connect (endpoints: string seq) = set "connect/endpoints" (jsonList endpoints)

    /// Endpoints to listen on, such as "tcp/127.0.0.1:7447".
    let listen (endpoints: string seq) = set "listen/endpoints" (jsonList endpoints)

    let multicastScouting (enabled: bool) =
        set "scouting/multicast/enabled" (if enabled then "true" else "false")

    let timestamping (enabled: bool) =
        set "timestamping/enabled" (if enabled then "true" else "false")

    /// Build the zenoh-c config into dst. On error, dst holds nothing to drop.
    let internal write (config: Config) (dst: nativeptr<z_owned_config_t>) =
        match config.BaseJson5 with
        | None -> Ffi.check "z_config_default" (Interop.z_config_default dst)
        | Some json5 -> Ffi.check "zc_config_from_str" (Interop.zc_config_from_str (dst, json5))

        try
            for key, value in List.rev config.Settings do
                Ffi.check
                    $"zc_config_insert_json5(\"{key}\")"
                    (Interop.zc_config_insert_json5 (Interop.z_config_loan_mut dst, key, value))
        with _ ->
            Interop.z_config_drop dst
            reraise ()

    /// The complete configuration as zenoh-c resolves it (validates every setting).
    let toJson5 (config: Config) =
        let mutable native = Unchecked.defaultof<z_owned_config_t>
        let p = &&native
        write config p

        try
            Ffi.readOwnedString (fun s ->
                Ffi.check "zc_config_to_string" (Interop.zc_config_to_string (Interop.z_config_loan p, s)))
        finally
            Interop.z_config_drop p
