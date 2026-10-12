#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $PackageDirectory,
    [Parameter(Mandatory)]
    [ValidatePattern('^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$')]
    [string] $Version
)

$ErrorActionPreference = 'Stop'
$PackageDirectory = [System.IO.Path]::GetFullPath($PackageDirectory)
$scratch = Join-Path ([System.IO.Path]::GetTempPath()) ("azlocal-package-test-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null

function Invoke-Dotnet {
    param([string[]] $Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet command failed with exit code $LASTEXITCODE." }
}

# Validate the complete package set, including the dependency needed by the client.
$expectedIds = @('AzLocal.Core', 'AzLocal.Client')
$packages = @(Get-ChildItem -LiteralPath $PackageDirectory -Filter '*.nupkg')
if ($packages.Count -ne $expectedIds.Count) { throw 'Expected exactly the Core and Client packages.' }
foreach ($id in $expectedIds) {
    $package = Join-Path $PackageDirectory "$id.$Version.nupkg"
    if (-not (Test-Path -LiteralPath $package)) { throw "Missing package: $id.$Version" }
    $archive = [System.IO.Compression.ZipFile]::OpenRead($package)
    try {
        $entry = $archive.Entries | Where-Object { $_.FullName.EndsWith('.nuspec') } | Select-Object -First 1
        $reader = [System.IO.StreamReader]::new($entry.Open())
        try { [xml] $manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if ($manifest.package.metadata.id -cne $id -or $manifest.package.metadata.version -cne $Version) {
            throw "Package identity/version mismatch in $package"
        }
        if ($manifest.package.metadata.license.'#text' -ne 'MIT' -or $manifest.package.metadata.readme -ne 'NUGET_README.md' -or $manifest.package.metadata.icon -ne 'icon.png') {
            throw "Missing release metadata in $package"
        }
        if (-not $archive.GetEntry('NUGET_README.md') -or -not $archive.GetEntry('LICENSE') -or -not $archive.GetEntry('icon.png')) {
            throw "Missing package README, icon, or license in $package"
        }
    } finally { $archive.Dispose() }
}

# Create a fresh app outside the checkout, with NuGet references instead of project references.
$application = Join-Path $scratch 'consumer'
New-Item -ItemType Directory -Path $application | Out-Null
$project = Join-Path $application 'Consumer.csproj'
$escapedVersion = [System.Security.SecurityElement]::Escape($Version)
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="AzLocal.Client" Version="$escapedVersion" />
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $project -Encoding utf8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PackageSmoke.cs') -Destination (Join-Path $application 'Program.cs')
$nugetConfig = Join-Path $scratch 'NuGet.Config'
$escapedSource = [System.Security.SecurityElement]::Escape($PackageDirectory)
@"
<configuration>
  <packageSources>
    <clear />
    <add key="local-release" value="$escapedSource" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@ | Set-Content -LiteralPath $nugetConfig -Encoding utf8
Invoke-Dotnet -Arguments @('restore', $project, '--configfile', $nugetConfig)
Invoke-Dotnet -Arguments @('build', $project, '--configuration', 'Release', '--no-restore')


Write-Output "NuGet metadata and fresh client application build passed. Live SDK checks run against Docker."
