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

function Get-FreePort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        return ([System.Net.IPEndPoint] $listener.LocalEndpoint).Port
    } finally { $listener.Stop() }
}

# Validate the complete package set, including the dependency needed by the client.
$expectedIds = @('AzLocal.Core', 'AzLocal.Client', 'azlocal')
$packages = @(Get-ChildItem -LiteralPath $PackageDirectory -Filter '*.nupkg')
if ($packages.Count -ne $expectedIds.Count) { throw 'Expected exactly the Core, Client, and CLI packages.' }
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

$toolDirectory = Join-Path $scratch 'tool'
Invoke-Dotnet -Arguments @('tool', 'install', 'azlocal', '--version', $Version, '--tool-path', $toolDirectory, '--add-source', $PackageDirectory, '--ignore-failed-sources')
$tool = Join-Path $toolDirectory $(if ($IsWindows) { 'azlocal.exe' } else { 'azlocal' })

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

# A temporary certificate avoids changing the runner's certificate trust settings.
$rsa = [System.Security.Cryptography.RSA]::Create(2048)
$certificateRequest = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=localhost', $rsa, [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
$names = [System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
$names.AddDnsName('localhost')
$names.AddIpAddress([System.Net.IPAddress]::Loopback)
$certificateRequest.CertificateExtensions.Add($names.Build())
$certificate = $certificateRequest.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5), [DateTimeOffset]::UtcNow.AddHours(1))
$certificatePath = Join-Path $scratch 'localhost.pfx'
$certificatePassword = [guid]::NewGuid().ToString('N')
[System.IO.File]::WriteAllBytes($certificatePath, $certificate.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $certificatePassword))
$httpPort = Get-FreePort
$amqpPort = Get-FreePort
while ($httpPort -eq $amqpPort) { $amqpPort = Get-FreePort }
$endpoint = "https://127.0.0.1:$httpPort"

$start = [System.Diagnostics.ProcessStartInfo]::new($tool)
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.WorkingDirectory = $scratch
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
foreach ($argument in @('--internal-host', '--urls', $endpoint)) { $start.ArgumentList.Add($argument) }
$start.Environment['Kestrel__Certificates__Default__Path'] = $certificatePath
$start.Environment['Kestrel__Certificates__Default__Password'] = $certificatePassword
$start.Environment['AzLocal__AmqpPort'] = $amqpPort.ToString()
$start.Environment['AzLocal__StateMode'] = 'Sqlite'
$start.Environment['AzLocal__SqlitePath'] = Join-Path $scratch 'state'
$start.Environment['AzLocal__BlobStorePath'] = Join-Path $scratch 'blobs'
$hostProcess = [System.Diagnostics.Process]::Start($start)
$stdout = $hostProcess.StandardOutput.ReadToEndAsync()
$stderr = $hostProcess.StandardError.ReadToEndAsync()

try {
    $ready = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        if ($hostProcess.HasExited) { throw 'The installed CLI host exited before becoming ready.' }
        try {
            $null = Invoke-RestMethod "$endpoint/" -SkipCertificateCheck -TimeoutSec 2
            $ready = $true
            break
        } catch { Start-Sleep -Milliseconds 250 }
    }
    if (-not $ready) { throw 'The installed CLI host did not become ready.' }
    Invoke-Dotnet -Arguments @('run', '--project', $project, '--configuration', 'Release', '--no-build', '--', $endpoint, $amqpPort.ToString(), $certificate.Thumbprint)
    if (-not (Test-Path -LiteralPath (Join-Path $scratch 'state/state.db'))) {
        throw 'The installed host did not create its SQLite state database.'
    }
} catch {
    Write-Error $_ -ErrorAction Continue
    throw
} finally {
    if (-not $hostProcess.HasExited) { $hostProcess.Kill($true) }
    $hostProcess.WaitForExit()
    $stdout.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $scratch 'host.stdout.log')
    $stderr.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $scratch 'host.stderr.log')
    Write-Output "Package smoke-test logs: $scratch"
    $hostProcess.Dispose()
    $certificate.Dispose()
    $rsa.Dispose()
}
