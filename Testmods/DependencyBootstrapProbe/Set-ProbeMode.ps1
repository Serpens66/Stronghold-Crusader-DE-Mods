[CmdletBinding(SupportsShouldProcess=$true)]
param(
    [ValidateSet('Observe','Active','Disabled','Remove')][string]$Mode = 'Disabled',
    [switch]$ConfirmMenuAndUnityLog,
    [string]$ObservedLogPath
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
    & (Join-Path $PSScriptRoot 'Check-Run.ps1') -Mode Observe -LogPath $ObservedLogPath
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