namespace Zenoh

open System.Text
open Zenoh.Native

#nowarn "9"
#nowarn "51"

/// Key expressions: paths such as "demo/example/**" that name and select resources.
module KeyExpr =
    /// Whether s is a key expression in canonical form (the only form zenoh accepts).
    let isCanon (s: string) =
        let bytes = Encoding.UTF8.GetBytes s
        use p = fixed bytes
        Interop.z_keyexpr_is_canon (p, unativeint bytes.Length) = Result.Z_OK

    /// The canonical form of s, such as "a/**/**/b" -> "a/**/b".
    /// Throws ArgumentException when s cannot be canonized.
    let canonize (s: string) =
        let bytes = Encoding.UTF8.GetBytes s
        let mutable len = unativeint bytes.Length

        let rc =
            if bytes.Length = 0 then
                Result.Z_OK
            else
                use p = fixed bytes
                Interop.z_keyexpr_canonize (p, &&len)

        if rc <> Result.Z_OK then
            invalidArg "s" $"'{s}' is not a valid key expression"

        Encoding.UTF8.GetString(bytes, 0, int len)

    /// Whether some key matches both a and b.
    let intersects (a: string) (b: string) =
        Ffi.withKeyExpr a (fun ka -> Ffi.withKeyExpr b (fun kb -> Interop.z_keyexpr_intersects (ka, kb) <> 0uy))

    /// Whether every key that matches b also matches a.
    let includes (a: string) (b: string) =
        Ffi.withKeyExpr a (fun ka -> Ffi.withKeyExpr b (fun kb -> Interop.z_keyexpr_includes (ka, kb) <> 0uy))
