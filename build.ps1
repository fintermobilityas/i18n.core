param(
    [Parameter(Position = 0, ValueFromPipeline)]
    [ValidateSet("Build", "Test-Pot", "Nupkg-Push")]
    [string] $Target = "Build",
    [Parameter(Position = 0, ValueFromPipeline)]
    [string] $Version = "0.0.0",
    [Parameter(Position = 1, ValueFromPipeline)]
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",
    [Parameter(Position = 2, ValueFromPipeline)]
    [switch] $Nupkg,
    [Parameter(Position = 3, ValueFromPipeline)]
    [switch] $CI,
    [Parameter(Position = 4, ValueFromPipeline)]
    [string]$NugetApiKey = $null
)

$WorkingDirectory = Split-Path -parent $MyInvocation.MyCommand.Definition
. $WorkingDirectory\common.ps1

$BuildOutputDirectory = Join-Path $WorkingDirectory build\$Version
$NupkgsDirectory = Join-Path $WorkingDirectory nupkgs

Resolve-Shell-Dependency dotnet

switch($Target) {
    "Build" {
        Invoke-Command-Colored dotnet @(
            ("build {0}" -f (Join-Path $WorkingDirectory i18n.core.sln))
            "/p:Version=$Version",
            "/p:GeneratePackageOnBuild=$Nupkg"
            "/p:IsCIBuild=$CI"
            "--output $BuildOutputDirectory"
            "--configuration $Configuration"
        )
    }
    "Test-Pot" {

        $PotFileName = Join-Path $WorkingDirectory src\i18n.Demo\locale\messages.pot
        $WebConfigFilename = Join-Path $WorkingDirectory src\i18n.Demo\Web.config

        Invoke-Command-Colored dotnet @(
            "tool update pot"
            "--global"
            "--version $Version"
            "--add-source ""$NupkgsDirectory"""
        )

        Invoke-Command-Colored pot @("--web-config-path $WebConfigFilename")

        $LocaleExists = Test-Path $PotFileName -PathType Leaf
        if($LocaleExists -eq $false) {
            exit 1
        }

        $PotCreationDate = Get-Content $PotFileName | Select-Object -First 4 `
            | Where-Object { [string]::IsNullOrWhiteSpace($_) -eq $false } `
            | Select-Object -Last 1

        $IsValidPotStr = ($PotCreationDate -replace "`"", "").StartsWith("POT-Creation-Date");
        if($IsValidPotStr -eq $false) {
            exit 1
        }

        Write-Output-Colored "Success"
        exit 0
          
    }
    "Nupkg-Push" {

        $NupkgVersionStr = $Version + ".nupkg"

        $Nupkgs = Get-ChildItem $NupkgsDirectory -Filter *.nupkg `
            | Where-Object { $_.Name.EndsWith($NupkgVersionStr) } `
            | Select-Object -ExpandProperty Fullname

        if($Nupkgs.Length -eq 0) {
            Write-Error "Unable to find any nupkgs in $NupkgsDirectory matching filter '*$NupkgVersionStr'"
            exit 1
        } 

        Write-Output "Nupkgs found: $($Nupkgs.Length)"

        foreach($NupkgPath in $Nupkgs) {
            Write-Output "Uploading $NupkgPath"
            Invoke-Command-Colored dotnet @("nuget push $NupkgPath --source nuget.org --api-key $NugetApiKey")
            Write-Output "Finished uploading $NupkgPath"
        }

        exit 0
    }
}