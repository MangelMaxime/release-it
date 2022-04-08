module Log

open System

let error (msg : string) =
    Console.ForegroundColor <- ConsoleColor.Red
    Console.Error.Write msg
    Console.WriteLine()
    Console.ResetColor()

let info (msg : string) =
    Console.ForegroundColor <- ConsoleColor.Blue
    Console.Write msg
    Console.WriteLine()
    Console.ResetColor()

let success (msg : string) =
    Console.ForegroundColor <- ConsoleColor.Green
    Console.Write msg
    Console.WriteLine()
    Console.ResetColor()

let log (msg : string) =
    Console.Write msg
    Console.WriteLine()
