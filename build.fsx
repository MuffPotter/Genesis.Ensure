﻿#I "Src/packages/FAKE/tools"
#r "FakeLib.dll"

open Fake
open Fake.AssemblyInfoFile
open Fake.EnvironmentHelper
open Fake.MSBuildHelper
open Fake.NuGetHelper
open Fake.Testing.XUnit2

// properties
let semanticVersion = "1.0.0-alpha"
let version = (>=>) @"(?<major>\d*)\.(?<minor>\d*)\.(?<build>\d*).*?" "${major}.${minor}.${build}.0" semanticVersion
let configuration = getBuildParamOrDefault "configuration" "Release"
// can be set by passing: -ev deployToNuGet true
let deployToNuGet = getBuildParamOrDefault "deployToNuGet" "false"
let genDir = "Gen/"
let srcDir = "Src/"
let packagesDir = "Src/packages/"
let testDir = genDir @@ "Test"
let nugetDir = genDir @@ "NuGet"

Target "Clean" (fun _ ->
    CleanDirs[genDir; testDir; nugetDir]

    build (fun p ->
        { p with
            Verbosity = Some(Quiet)
            Targets = ["Clean"]
            Properties = ["Configuration", configuration]
        })
        (srcDir @@ "Genesis.Ensure.sln")
)

// would prefer to use the built-in RestorePackages function, but it restores packages in the root dir (not in Src), which causes build problems
Target "RestorePackages" (fun _ ->
    !! "./**/packages.config"
    |> Seq.iter (
        RestorePackage (fun p ->
            { p with
                OutputPath = (srcDir @@ "packages")
            })
        )
)

Target "Build" (fun _ ->
    // generate the shared assembly info
    CreateCSharpAssemblyInfoWithConfig (srcDir @@ "AssemblyInfoCommon.cs")
        [
            Attribute.Version version
            Attribute.FileVersion version
            Attribute.Configuration configuration
            Attribute.Company "Kent Boogaart"
            Attribute.Product "Genesis.Ensure"
            Attribute.Copyright "© Copyright. Kent Boogaart."
            Attribute.Trademark ""
            Attribute.Culture ""
            Attribute.StringAttribute("NeutralResourcesLanguage", "en-US", "System.Resources")
            Attribute.StringAttribute("AssemblyInformationalVersion", semanticVersion, "System.Reflection")
        ]
        (AssemblyInfoFileConfig(false))

    build (fun p ->
        { p with
            Verbosity = Some(Quiet)
            Targets = ["Build"]
            Properties =
                [
                    "Optimize", "True"
                    "DebugSymbols", "True"
                    "Configuration", configuration
                ]
        })
        (srcDir @@ "Genesis.Ensure.sln")
)

Target "ExecuteUnitTests" (fun _ ->
    xUnit2 (fun p ->
        { p with
            ShadowCopy = false;
        })
        [srcDir @@ "Genesis.Ensure.UnitTests/bin" @@ configuration @@ "Genesis.Ensure.UnitTests.dll"]
)

Target "CreateArchives" (fun _ ->
    // source archive
    !! "**/*.*"
        -- ".git/**"
        -- (genDir @@ "**")
        -- (srcDir @@ "**/.vs/**")
        -- (srcDir @@ "packages/**/*")
        -- (srcDir @@ "**/*.suo")
        -- (srcDir @@ "**/*.csproj.user")
        -- (srcDir @@ "**/*.gpState")
        -- (srcDir @@ "**/bin/**")
        -- (srcDir @@ "**/obj/**")
        |> Zip "." (genDir @@ "Genesis.Ensure-" + semanticVersion + "-src.zip")

    // binary archive
    let workingDir = srcDir @@ "Genesis.Ensure/bin" @@ configuration

    !! (workingDir @@ "Genesis.Ensure.*")
        |> Zip workingDir (genDir @@ "Genesis.Ensure-" + semanticVersion + "-bin.zip")
)

Target "CreateNuGetPackages" (fun _ ->
    // copy files required in the NuGet
    !! (srcDir @@ "Genesis.Ensure/bin" @@ configuration @@ "Genesis.Ensure.*")
        |> CopyFiles (nugetDir @@ "Genesis.Ensure/lib/portable-win+net45+wp81+win8+MonoAndroid+Xamarin.iOS10+MonoTouch")

    // copy source
    let sourceFiles =
        [!! (srcDir @@ "**/*.*")
            -- ".git/**"
            -- (srcDir @@ "**/.vs/**")
            -- (srcDir @@ "packages/**/*")
            -- (srcDir @@ "**/*.suo")
            -- (srcDir @@ "**/*.csproj.user")
            -- (srcDir @@ "**/*.gpState")
            -- (srcDir @@ "**/bin/**")
            -- (srcDir @@ "**/obj/**")]
    sourceFiles
        |> CopyWithSubfoldersTo (nugetDir @@ "Genesis.Ensure")

    // create the NuGets
    NuGet (fun p ->
        {p with
            Project = "Genesis.Ensure"
            Version = semanticVersion
            OutputPath = nugetDir
            WorkingDir = nugetDir @@ "Genesis.Ensure"
            SymbolPackage = NugetSymbolPackage.Nuspec
            Publish = System.Convert.ToBoolean(deployToNuGet)
        })
        (srcDir @@ "Genesis.Ensure.nuspec")
)

// build order
"Clean"
    ==> "RestorePackages"
    ==> "Build"
    ==> "ExecuteUnitTests"
    ==> "CreateArchives"
    ==> "CreateNuGetPackages"

RunTargetOrDefault "CreateNuGetPackages"