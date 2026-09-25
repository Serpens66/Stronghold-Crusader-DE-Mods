param(
    [Parameter(Mandatory = $true)][string]$TestExecutable,
    [Parameter(Mandatory = $true)][string]$ExtenderDir
)

$ErrorActionPreference = 'Stop'
$packages = Join-Path $env:USERPROFILE '.nuget\packages'
$probeRoot = Join-Path (Split-Path -Parent $TestExecutable) 'RedBirdVersionProbes'

foreach ($version in @('1.3.2', '1.5.0')) {
    $probeDir = Join-Path $probeRoot $version
    if (Test-Path -LiteralPath $probeDir) {
        Remove-Item -LiteralPath $probeDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $probeDir -Force | Out-Null
    Copy-Item -Path (Join-Path $ExtenderDir '*.dll') -Destination $probeDir -Force
    foreach ($package in @('redbird.abstractions', 'redbird.core', 'redbird.backends.nativex64')) {
        $source = Join-Path $packages "$package\$version\lib\net48\*.dll"
        Copy-Item -Path $source -Destination $probeDir -Force
    }
    & $TestExecutable redbird-probe $probeDir $version
    if ($LASTEXITCODE -ne 0) {
        throw "RedBird $version selector probe failed."
    }
}

Write-Host 'BugfixesAndQoL Waterboy RedBird 1.3.2 and 1.5.0 selector probes passed.'
