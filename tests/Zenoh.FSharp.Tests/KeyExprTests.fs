module KeyExprTests

open System
open Expecto
open Zenoh

[<Tests>]
let tests =
    testList
        "keyexpr"
        [
            test "canonical expressions are recognised" {
                Expect.isTrue (KeyExpr.isCanon "demo/example/**") "canon"
                Expect.isFalse (KeyExpr.isCanon "demo//example") "empty chunk"
                Expect.isFalse (KeyExpr.isCanon "demo/**/**") "repeated **"
            }

            test "canonize removes redundant wildcards" {
                Expect.equal (KeyExpr.canonize "a/**/**/b") "a/**/b" "double **"
                Expect.equal (KeyExpr.canonize "a/b") "a/b" "already canon"
            }

            test "canonize rejects invalid expressions" {
                Expect.throwsT<ArgumentException> (fun () -> KeyExpr.canonize "a//b" |> ignore) "empty chunk"
            }

            test "intersects and includes" {
                Expect.isTrue (KeyExpr.intersects "demo/*" "demo/a") "* matches a chunk"
                Expect.isFalse (KeyExpr.intersects "demo/*" "other/a") "different prefix"
                Expect.isTrue (KeyExpr.includes "demo/**" "demo/a/b") "** includes deeper keys"
                Expect.isFalse (KeyExpr.includes "demo/a" "demo/**") "a key does not include a wildcard"
            }

            test "invalid key expressions raise ArgumentException" {
                Expect.throwsT<ArgumentException> (fun () -> KeyExpr.intersects "a//b" "a" |> ignore) "invalid"
            }
        ]
