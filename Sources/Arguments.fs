namespace Release

open Argu

type CliError =
    | ArgumentsNotSpecified
    | ProjectFileNotFound
    | ChangelogFileNotFound
    | NoVersionFound
    | ChangelogParsingFailed

type NugetArgs =
    | [<CustomCommandLine("--api-key");AltCommandLine("-k")>] ApiKey of string
    | [<AltCommandLine("-s")>] Source of string
    | [<AltCommandLine("-c")>] Configuration of string
    | [<AltCommandLine("-p")>] Project of string
    | Changelog of string

    interface IArgParserTemplate with

        member this.Usage =
            match this with
            | ApiKey _ -> "The API key for the server."
            | Source _ -> "Package source (URL, UNC/folder path or package source name) to use."
            | Configuration _ -> "The configuration to use for building the package. Defaults to Release."
            | Project _ -> "The project to operate on. If a file is not specified, the command will search the current directory for one."
            | Changelog _ -> "Relative path to the changelog file. Default, to ChangelogFile attributes if found in the project file or CHANGELOG.md in last resort."

type ReleaseArgs =
    | [<CliPrefix(CliPrefix.None)>] Nuget of ParseResults<NugetArgs>

    interface IArgParserTemplate with

        member this.Usage =
            match this with
            | Nuget _ -> "Commands related to nuget release."
