#requires -Version 7.0
[CmdletBinding()]
param(
    [string] $Image = 'azlocal:local',
    [string] $Version = '0.1.0-alpha.0',
    [switch] $NoBuild
)
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$suffix = [guid]::NewGuid().ToString('N')
$container = "azlocal-check-$suffix"
$dataVolume = "$container-data"
$certVolume = "$container-certs"
$smokeImage = "azlocal-smoke:$suffix"

function Invoke-Docker {
    param([string[]] $Arguments)
    & docker @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Docker command failed: $($Arguments[0]) (exit $LASTEXITCODE)." }
}
function Wait-Healthy {
    for ($attempt = 0; $attempt -lt 80; $attempt++) {
        $state = & docker inspect --format '{{.State.Health.Status}}' $container
        if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the test container.' }
        if ($state -eq 'healthy') { return }
        if ($state -eq 'unhealthy') { throw 'The Docker health check failed.' }
        Start-Sleep -Milliseconds 500
    }
    throw 'Timed out waiting for the Docker health check.'
}
try {
    if (-not $NoBuild) {
        Invoke-Docker -Arguments @('build', '--target', 'runtime', '--build-arg', "VERSION=$Version", '-t', $Image, $repository)
    }
    Invoke-Docker -Arguments @('build', '--target', 'smoke', '--build-arg', "VERSION=$Version", '-t', $smokeImage, $repository)
    Invoke-Docker -Arguments @('volume', 'create', $dataVolume)
    Invoke-Docker -Arguments @('volume', 'create', $certVolume)
    Invoke-Docker -Arguments @('run', '--detach', '--name', $container, '--read-only', '--tmpfs', '/tmp', '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges:true', '--publish', '127.0.0.1::4566', '--publish', '127.0.0.1::5672', '--mount', "type=volume,source=$dataVolume,target=/data", '--mount', "type=volume,source=$certVolume,target=/certs", $Image)
    Wait-Healthy
    $port = (& docker port $container '4566/tcp').Split(':')[-1]
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the published HTTPS port.' }
    # Reachability diagnostic; SDK requests below pin the generated certificate.
    $response = Invoke-RestMethod "https://127.0.0.1:$port/" -SkipCertificateCheck -TimeoutSec 5
    if ($response.status -ne 'AzLocal emulator running') { throw 'Published host port did not reach the emulator.' }
    $smokeArguments = @('run', '--rm', '--network', "container:$container", '--read-only', '--tmpfs', '/tmp', '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges:true', '--mount', "type=volume,source=$certVolume,target=/certs,readonly", $smokeImage)
    Invoke-Docker -Arguments $smokeArguments
    Invoke-Docker -Arguments @('restart', $container)
    Wait-Healthy
    Invoke-Docker -Arguments ($smokeArguments + @('verify'))
    Write-Output 'Docker SDK operations, HTTPS/AMQP connectivity, and SQLite/blob restart persistence passed.'
} finally {
    & docker logs $container 2>&1 | Write-Output
    & docker rm --force $container 2>&1 | Write-Output
    & docker volume rm $dataVolume $certVolume 2>&1 | Write-Output
    & docker image rm $smokeImage 2>&1 | Write-Output
}
