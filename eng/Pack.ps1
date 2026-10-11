#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidatePattern('^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$')]
    [string] $Version,
    [string] $OutputDirectory,
    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$calculatedVersion = & dotnet msbuild (Join-Path $repositoryRoot 'src/AzLocal.Client') -target:MinVer -getProperty:Version -nologo
if ($LASTEXITCODE -ne 0) { throw 'Could not calculate the package version from Git.' }
$calculatedVersion = "$calculatedVersion".Trim()
if ($Version -and $Version -cne $calculatedVersion) {
    throw "Expected $Version but Git determines $calculatedVersion. Tag the release commit before packing."
}
$Version = $calculatedVersion
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repositoryRoot "artifacts/packages/$Version"
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

foreach ($project in @('AzLocal.Core', 'AzLocal.Client', 'AzLocal.Cli')) {
    $projectPath = Join-Path $repositoryRoot "src/$project/$project.csproj"
    $packArguments = @('pack', $projectPath, '--configuration', 'Release', '--no-restore', '--output', $OutputDirectory)
    if ($NoBuild) { $packArguments += '--no-build' }
    & dotnet @packArguments
    if ($LASTEXITCODE -ne 0) { throw "Packing $project failed with exit code $LASTEXITCODE." }
}

Write-Output "Packages prepared in $OutputDirectory"
