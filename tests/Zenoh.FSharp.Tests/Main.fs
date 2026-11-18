module Main

open Expecto

[<EntryPoint>]
let main argv =
    Zenoh.ZenohRuntime.InitLogging "error"
    Tests.runTestsInAssemblyWithCLIArgs [] argv
