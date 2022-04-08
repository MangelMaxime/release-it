module Release.Nuget

open Argu
open Ionide.KeepAChangelog
open SimpleExec
open System.IO
open System.Text.RegularExpressions
open FsToolkit.ErrorHandling
open System.Linq

let private tryGetProjectFile (args : ParseResults<NugetArgs>) (cwd : string) =
    let projectFileOpt = args.TryGetResult(Project)

    match projectFileOpt with
    | Some file ->
        Ok (FileInfo (Path.Join(cwd, file)))

    | None ->
        let projectFiles =
            [
                yield! Directory.GetFiles(cwd, "*.fsproj")
                yield! Directory.GetFiles(cwd, "*.csproj")
            ]

        match projectFiles with
        | [] ->
            Log.error $"No project files found in %s{cwd}. Please, specify the project file using --project option or by invoking the command in the project file directory."
            Error ProjectFileNotFound

        | projectFile :: [] ->
            Ok (FileInfo projectFile)

        | _ ->
            Log.error $"Multiple project files found in %s{cwd}. Please, specify the project file using --project option."
            Error ProjectFileNotFound

let private tryGetChangelog
    (args : ParseResults<NugetArgs>)
    (projectFile : FileInfo) =

    let changelogPathOpt = args.TryGetResult(Changelog)
    let projectDirectory = projectFile.Directory.FullName

    let changelogPath =
        match changelogPathOpt with
        | Some changelogPath ->
            Path.Join(projectDirectory, changelogPath)

        | None ->
            let projectFileContent = File.ReadAllText(projectFile.FullName)

            let m = Regex.Match(projectFileContent, "<ChangelogFile>(.*)<\/ChangelogFile>")

            if m.Success then
                Path.Join(projectDirectory, m.Groups[1].Value)
            else
                Path.Join(projectDirectory, "CHANGELOG.md")

    // Exit if the changelog file does not exist
    if not (File.Exists(changelogPath)) then
        Log.error $"Changelog file not found: %s{changelogPath}"

        Error ChangelogFileNotFound
    else

        Ok (FileInfo changelogPath)


let private tryGetCurrentVersion
    (changelogFile : FileInfo) =

    match Parser.parseChangeLog changelogFile with
    | Ok changelog ->

        let sortedReleases =
            // have to use LINQ here because List.sortBy* require IComparable, which
            // semver doesn't implement
            changelog.Releases.OrderByDescending(fun (v, _, _) -> v)

        match Seq.tryHead sortedReleases with
        | Some (currentVersion, _, _) ->
            Ok currentVersion

        | None ->
            Log.error $"No version found in changelog file: %s{changelogFile.FullName}"
            Error NoVersionFound


    | Error (formatted, msg) ->
        Log.error $"Error parsing Changelog at {changelogFile.FullName}. The error occurred at {msg.Position}.{System.Environment.NewLine}{formatted}"
        Error ChangelogParsingFailed

let private buildPackage
    (projectFile : FileInfo)
    (args : ParseResults<NugetArgs>) =

    let configuration =
        args.GetResult(Configuration, defaultValue = "Release")


    task {
        let mutable exitCode = 0
        let! (standardOutput, errorOutput) =
            Command.ReadAsync(
                "dotnet",
                $"pack --configuration %s{configuration}",
                workingDirectory = projectFile.Directory.FullName,
                handleExitCode =
                    fun code ->
                        exitCode <- code
                        true
            )

        if exitCode = 0 then
            Log.log $"{System.Environment.NewLine}{standardOutput}{System.Environment.NewLine}Packages built successfully."
        else

            Log.error standardOutput

            // Dotnet doesn't log errors to stderr,
            // but just in case we check if there are some errors in the output
            if errorOutput.Length > 0 then
                Log.error errorOutput

            Log.error "Packages failed to build."
    }

let private pushPackage
    (projectFile : FileInfo)
    (currentVersion : SemVersion.SemanticVersion)
    (args : ParseResults<NugetArgs>) =

    let apiKey = args.GetResult(ApiKey)
    let sourceOpt = args.TryGetResult(Source)

    let projectName =
        projectFile.Name.Replace(projectFile.Extension, "")

    let commandArgs =
        [
            "nuget push"

            $"bin/Release/{projectName}.{currentVersion}.nupkg"

            $"--api-key %s{apiKey}"

            match sourceOpt with
            | Some source ->
                $"--source %s{source}"
            | None ->
                ()
        ]
        |> String.concat " "

    task {
        let mutable exitCode = 0
        let! (standardOutput, errorOutput) =
            Command.ReadAsync(
                "dotnet",
                commandArgs,
                workingDirectory = projectFile.Directory.FullName,
                handleExitCode =
                    fun code ->
                        exitCode <- code
                        true
            )

        if exitCode = 0 then
            Log.log $"{System.Environment.NewLine}{standardOutput}{System.Environment.NewLine}Packages pushed successfully."
        else
            Log.error standardOutput

            // Dotnet doesn't log errors to stderr,
            // but just in case we check if there are some errors in the output
            if errorOutput.Length > 0 then
                Log.error errorOutput

            Log.error "Packages failed to push."
    }

let release (args : ParseResults<NugetArgs>) =
    result {
        let cwd = Directory.GetCurrentDirectory()

        Log.info $"Current directory: %s{cwd}"

        let! projectFile = tryGetProjectFile args cwd

        Log.log $"Project directory: %s{cwd}"

        let! changelogFile = tryGetChangelog args projectFile

        Log.log $"Changelog file found at: %s{changelogFile.FullName}"

        let! currentVersion = tryGetCurrentVersion changelogFile

        (buildPackage projectFile args).Wait()
        (pushPackage projectFile currentVersion args).Wait()
    }
