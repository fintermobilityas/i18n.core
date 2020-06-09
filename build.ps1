param(
    [Parameter(Position = 0, ValueFromPipeline)]
    [string] $Version = "0.0.0",
    [Parameter(Position = 1, ValueFromPipeline)]
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",
    [Parameter(Position = 2, ValueFromPipeline)]
    [switch] $Nupkg,
    [Parameter(Position = 3, ValueFromPipeline)]
    [switch] $CI
)

$WorkingDirectory = Split-Path -parent $MyInvocation.MyCommand.Definition
. $WorkingDirectory\common.ps1

$BuildOutputDirectory = Join-Path $WorkingDirectory build\$Version

Resolve-Shell-Dependency dotnet

Invoke-Command-Colored dotnet @(
    ("build {0}" -f (Join-Path $WorkingDirectory i18n.core.sln))
    "/p:Version=$Version",
    "/p:GeneratePackageOnBuild=$Nupkg"
    "/p:IsCIBuild=$CI"
    "--output $BuildOutputDirectory"
    "--configuration $Configuration"
)