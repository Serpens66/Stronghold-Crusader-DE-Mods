[CmdletBinding(SupportsShouldProcess=$true)]
param(
    [ValidateSet('Observe','Active','Disabled','Remove')][string]$Mode = 'Disabled',
    [switch]$ConfirmMenuAndUnityLog,
    [string]$ObservedLogPath,
    [string]$AcceptedStartupRoot
)
$ErrorActionPreference = 'Stop'
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { throw 'Close the game first.' }
$root = [IO.Path]::GetFullPath((Join-Path $game 'BepInEx'))
$active = Join-Path $root 'config\DependencyBootstrapProbe.active'
$disabled = Join-Path $root 'config\DependencyBootstrapProbe.disabled'
$ownFiles = @('patchers\DependencyBootstrapProbe.Patcher.dll','plugins\DependencyBootstrapProbe_Serp\DependencyBootstrapProbe.dll','plugins\DependencyBootstrapProbe_Serp\info.json','config\DependencyBootstrapProbe.active','config\DependencyBootstrapProbe.disabled')
foreach ($relative in $ownFiles) {
    $absolute = [IO.Path]::GetFullPath((Join-Path $root $relative))
    if (-not $absolute.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Target escapes BepInEx root.' }
}
if ($Mode -eq 'Active') {
    if (-not $ConfirmMenuAndUnityLog) { throw 'First confirm usable menu and no new Unity initialization errors with -ConfirmMenuAndUnityLog.' }
    if ($AcceptedStartupRoot) {
        # A previously accepted active startup may authorize the unchanged early entry for the shadow extension.
        & (Join-Path $PSScriptRoot 'Check-Run.ps1') -Mode Active -LogPath (Join-Path $AcceptedStartupRoot 'DependencyBootstrapProbe.trace.log') -ExpectedArtifactRoot $AcceptedStartupRoot -GameDir $AcceptedStartupRoot
        function HashFile([string]$p) {
            $s=[IO.File]::OpenRead($p); $h=[Security.Cryptography.SHA256]::Create()
            try { [BitConverter]::ToString($h.ComputeHash($s)).Replace('-','') } finally { $s.Dispose(); $h.Dispose() }
        }
        $early = Get-Content -Raw -LiteralPath (Join-Path $AcceptedStartupRoot 'early-source-hashes.json') | ConvertFrom-Json
        $expected = @('patcher\DependencyBootstrapPatcher.cs','patcher\ReadyGate.cs','ProbeTrace.cs','ProbeIdentity.cs')
        if ($early.Count -ne $expected.Count) { throw 'Incomplete early-entry evidence.' }
        foreach ($relative in $expected) {
            $entry = @($early | Where-Object Path -ceq $relative)
            if ($entry.Count -ne 1 -or (HashFile (Join-Path $PSScriptRoot $relative)) -cne $entry[0].Hash) { throw "Early entry changed: $relative. A new observation run is required." }
        }
        foreach ($relative in @('BepInEx\core\BepInEx.dll','BepInEx\plugins\000shcdese\SHCDESE.dll')) {
            if ((HashFile (Join-Path $game $relative)) -cne (HashFile (Join-Path $AcceptedStartupRoot $relative))) { throw 'Loader binaries changed since accepted run.' }
        }
        foreach ($relative in @('BepInEx\patchers\DependencyBootstrapProbe.Patcher.dll','BepInEx\plugins\DependencyBootstrapProbe_Serp\DependencyBootstrapProbe.dll')) {
            if ((HashFile (Join-Path $game $relative)) -cne (HashFile (Join-Path $PSScriptRoot $relative))) { throw 'Installed build differs from local artifacts.' }
        }
    } else {
        & (Join-Path $PSScriptRoot 'Check-Run.ps1') -Mode Observe -LogPath $ObservedLogPath
    }
}
if (-not $PSCmdlet.ShouldProcess($root,"Set probe mode: $Mode (only explicitly named probe files)")) { return }
if ($Mode -eq 'Remove') {
    foreach ($relative in $ownFiles) {
        $target = Join-Path $root $relative
        if (Test-Path -LiteralPath $target -PathType Leaf) { Remove-Item -LiteralPath $target -Force }
    }
    Write-Host 'Own probe DLLs, manifest and switches removed. Trace and other components preserved.'
    return
}
[IO.Directory]::CreateDirectory((Join-Path $root 'config')) | Out-Null
foreach ($file in @($active,$disabled)) { if (Test-Path -LiteralPath $file -PathType Leaf) { Remove-Item -LiteralPath $file -Force } }
if ($Mode -eq 'Disabled') { [IO.File]::WriteAllText($disabled,'disabled until next explicit mode change') }
if ($Mode -eq 'Active') {
    $stream=[IO.File]::OpenRead((Join-Path $root 'patchers\DependencyBootstrapProbe.Patcher.dll'))
    $sha=[Security.Cryptography.SHA256]::Create()
    try { $hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') } finally {$sha.Dispose();$stream.Dispose()}
    [IO.File]::WriteAllText($active,$hash)
}
Write-Host "Mode=$Mode; takes effect at next full game start."
