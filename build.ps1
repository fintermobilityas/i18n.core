param(
    [ValidateSet("dotnet-tool", "nupkg")]
    [string]$Type = "dotnet-tool",
    [string]$PackageVersion = "0.1.0",
    [string]$Target = "Rebuild",
    [string]$Verbosity = "Minimal"
)

$RootDirectory = Split-Path -parent $script:MyInvocation.MyCommand.Path
$BuildDirectory = Join-Path $RootDirectory build
$ToolsBuildDirectory = Join-Path $BuildDirectory tools
$NupkgsDir = Join-Path $RootDirectory nupkgs

$PotToolSrcDirectory = Join-Path $RootDirectory src\pot
$PotToolCsproj = Join-Path $PotToolSrcDirectory pot.csproj
$PotToolBuildDirectory = Join-Path $ToolsBuildDirectory pot\$PackageVersion

function Build-Pot {
    . dotnet tool uninstall -g pot 
    . dotnet pack $PotToolCsproj --output $PotToolBuildDirectory -c Release /p:Version=$PackageVersion $PotCsproj 
    . dotnet tool install --add-source $PotToolBuildDirectory --global pot
}

switch($type)
{
    "dotnet-tool" {
        Build-Pot 
    }
}