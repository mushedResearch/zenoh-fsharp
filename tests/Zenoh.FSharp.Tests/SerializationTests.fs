module SerializationTests

open System.Collections.Generic
open Expecto
open Zenoh

let private roundTrip<'T when 'T: equality> (value: 'T) =
    Expect.equal (ZSerde.deserialize<'T> (ZSerde.serialize value)) value $"round trip of %A{value}"

[<Tests>]
let tests =
    testList
        "serialization"
        [
            // Expected bytes: the zenoh-ext format (little-endian numbers, LEB128 lengths).
            test "primitives use the zenoh format" {
                Expect.equal (ZSerde.serialize 1) [| 1uy; 0uy; 0uy; 0uy |] "int32"
                Expect.equal (ZSerde.serialize 258us) [| 2uy; 1uy |] "uint16"
                Expect.equal (ZSerde.serialize true) [| 1uy |] "bool"
                Expect.equal (ZSerde.serialize "ab") [| 2uy; 97uy; 98uy |] "string"
                Expect.equal (ZSerde.serialize [| 5uy; 6uy |]) [| 2uy; 5uy; 6uy |] "bytes"
                Expect.equal (ZSerde.serialize 1.0) (System.BitConverter.GetBytes 1.0) "float"
            }

            test "sequences and tuples" {
                Expect.equal (ZSerde.serialize [ 1s; 2s ]) [| 2uy; 1uy; 0uy; 2uy; 0uy |] "list of int16"
                Expect.equal (ZSerde.serialize (7uy, "a")) [| 7uy; 1uy; 97uy |] "tuple"
                Expect.equal (ZSerde.serialize (Array.zeroCreate<int64> 200)).[0..1] [| 200uy; 1uy |] "LEB128 length"
            }

            test "round trips" {
                roundTrip 42L
                roundTrip -3y
                roundTrip 3.5f
                roundTrip "zenoh ✓"
                roundTrip [| 1; 2; 3 |]
                roundTrip [ "a"; "b" ]
                roundTrip (1, "two", 3.0)
                roundTrip (Map.ofList [ "x", 1; "y", 2 ])
                roundTrip [ (1uy, [ "nested" ]) ]
                roundTrip ([||]: byte[])
            }

            test "Dictionary and ResizeArray" {
                let dict = Dictionary<string, int>()
                dict["k"] <- 9
                let back = ZSerde.deserialize<Dictionary<string, int>> (ZSerde.serialize dict)
                Expect.equal (List.ofSeq back.Keys, back["k"]) ([ "k" ], 9) "dictionary"
                let list = ResizeArray [ 1u; 2u ]
                Expect.sequenceEqual (ZSerde.deserialize<ResizeArray<uint32>> (ZSerde.serialize list)) list "ResizeArray"
            }

            test "serializer and deserializer by hand" {
                use s = new ZSerializer()
                s.WriteSequenceLength 2
                s.WriteString "a"
                s.WriteString "b"
                s.WriteFloat64 2.5
                let data = s.Finish()

                use d = new ZDeserializer(data)
                let n = d.ReadSequenceLength()
                let items = [ for _ in 1..n -> d.ReadString() ]
                Expect.equal (items, d.ReadFloat64()) ([ "a"; "b" ], 2.5) "values"
                Expect.isTrue d.IsDone "all read"
                Expect.equal (ZSerde.deserialize<string list * float> data) ([ "a"; "b" ], 2.5) "same as ZSerde"
            }

            test "malformed data raises ZenohException" {
                Expect.throwsT<ZenohException> (fun () -> ZSerde.deserialize<int64> [| 1uy |] |> ignore) "too short"
                Expect.throwsT<ZenohException> (fun () -> ZSerde.deserialize<byte> [| 1uy; 2uy |] |> ignore) "data left"
            }

            test "unsupported types raise NotSupportedException" {
                Expect.throwsT<System.NotSupportedException> (fun () -> ZSerde.serialize (Some 1) |> ignore) "option"
            }
        ]
