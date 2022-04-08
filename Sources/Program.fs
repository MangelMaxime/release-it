module Release.Main

open System
open Argu

let getExitCode result =
    match result with
    | Ok () -> 0
    | Error err ->
        match err with
        | ArgumentsNotSpecified -> 1
        | ProjectFileNotFound -> 2
        | ChangelogFileNotFound -> 3
        | NoVersionFound -> 4
        | ChangelogParsingFailed -> 5

[<EntryPoint>]
let main argv =
    let errorHandler =
        ProcessExiter(
            colorizer =
                function
                | ErrorCode.HelpText -> None
                | _ -> Some ConsoleColor.Red
        )

    let parser =
        ArgumentParser.Create<ReleaseArgs>(programName = "release", errorHandler = errorHandler)

    match parser.ParseCommandLine argv with
    | nuget when nuget.Contains(Nuget) ->
        let result = nuget.GetResult(Nuget)

        Nuget.release result

    | _ ->
        Log.log (parser.PrintUsage())
        Error ArgumentsNotSpecified
    |> getExitCode
