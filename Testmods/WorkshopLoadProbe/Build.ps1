[CmdletBinding()]
param([switch]$nopause, [switch]$noinstall)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'tools\Common.ps1')
$workspace = Get-ProbeWorkspace
$game = Get-ProbeGame

& (Join-Path $workspace 'Shared\Tools\Validation\Test-SharedBoundaries.ps1') -ModDirectory 'Testmods\WorkshopLoadProbe' -IndependentRuntime
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw 'Shared boundary check failed.' }

foreach ($project in @('patcher\WorkshopLoadProbe.Patcher.csproj', 'observer\WorkshopLoadProbe.csproj', 'payload\WorkshopLoadProbe.Payload.csproj')) {
    & dotnet msbuild (Join-Path $PSScriptRoot $project) /t:Build /p:Configuration=Release /verbosity:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $project" }
}

# Payload package (info.json + DLL); test items add variant files and the keep list.
# Never installed into BepInEx/plugins by this build: only the probe's safe swap installs it.
$package = Join-Path $PSScriptRoot ('payload\package\' + $ProbePayloadGuid)
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'payload\package')) { Remove-Item -LiteralPath (Join-Path $PSScriptRoot 'payload\package') -Recurse -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'payload\content') -Destination $package -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'payload\out\WorkshopLoadProbe.Payload.dll') -Destination $package

$installed = @(
    @{ Source = 'BepInEx\patchers\WorkshopLoadProbe.Patcher.dll'; Target = 'BepInEx\patchers\WorkshopLoadProbe.Patcher.dll' },
    @{ Source = 'BepInEx\plugins\WorkshopLoadProbe_Serp\WorkshopLoadProbe.dll'; Target = 'BepInEx\plugins\WorkshopLoadProbe_Serp\WorkshopLoadProbe.dll' },
    @{ Source = 'BepInEx\plugins\WorkshopLoadProbe_Serp\info.json'; Target = 'BepInEx\plugins\WorkshopLoadProbe_Serp\info.json' })
foreach ($file in $installed) {
    if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot $file.Source) -PathType Leaf)) { throw "Missing output: $($file.Source)" }
}
if (-not $noinstall) {
    Assert-ProbeGameClosed
    foreach ($file in $installed) {
        $source = Join-Path $PSScriptRoot $file.Source
        $target = Join-Path $game $file.Target
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
        Copy-Item -LiteralPath $source -Destination $target -Force
        if ((Get-ProbeSha256 $source) -ne (Get-ProbeSha256 $target)) { throw "Copy verification failed: $($file.Target)" }
    }
}
Write-Output ("PASS: built. Installed=" + (-not $noinstall) + ". The probe stays inert until tools\Set-ProbeConfig.ps1 writes a configuration.")
