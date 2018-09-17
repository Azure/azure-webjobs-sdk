namespace FSharpFunctions

open FSharp.Control.Tasks.ContextInsensitive
open Microsoft.Azure.WebJobs

module TestFunction =
    [<NoAutomaticTrigger>]
    let TaskTest() = task {
        printf "hello"
    }