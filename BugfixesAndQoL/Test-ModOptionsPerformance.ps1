[CmdletBinding()]
param([switch]$RunTests)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
function Read-Source([string]$name) { [IO.File]::ReadAllText((Join-Path $PSScriptRoot "src\$name.cs")) }
$plague = Read-Source 'PlaguePopularityFix'
if ($plague -match '\bOnTick\b|OnGameTick|Stopwatch|PruneInvalidProjectiles|RemoveProjectileSlot') {
    throw 'Permanent plague tick reconciliation or global deletion search was reintroduced.'
}
foreach ($required in @('herds.ReconcilePlayer(playerId, isLivingProjectile)',
    'herds.ReconcileDeletedSlot(args.ProjectileId, isLivingProjectile)',
    'herds.ReconcileAll(isLivingProjectile)', 'herds.ToSaveRecords()', 'herds.Load(state.Herds)',
    'AddDelayedAction(', 'CancelDiagnostic(playerId)', 'diagnosticGeneration != generation',
    'private volatile bool correctionEnabled', 'settings.SettingChanged += OnSettingChanged',
    'serp-plague-popularity-v1', '*popularityAccumulator = correctedPopularity;')) {
    if (-not $plague.Contains($required)) { throw "Missing plague performance/compatibility contract: $required" }
}
$callback = $plague.Substring($plague.IndexOf('private void CorrectPlaguePopularity'))
$callback = $callback.Substring(0, $callback.IndexOf('private byte[] SaveState'))
if ($callback.IndexOf('if (!correctionEnabled') -gt $callback.IndexOf('context.Pointer') -or
    $callback.Contains('PlaguePopularitySaveLimitPolicy.GetCurrent()')) {
    throw 'Disabled popularity path reads native context, or active path allocates save-limit snapshots.'
}
$foreign = Read-Source 'ForeignTroopHudRuntime'
if (-not $foreign.Contains('surfaces == 0 && Volatile.Read(ref resetPending) == 0') -or
    -not $foreign.Contains('Interlocked.Exchange(ref activeSurfaces, next)') -or
    -not $foreign.Contains('else if ((surfaces & 2) != 0) RefreshOwnHealth()') -or
    -not $foreign.Contains('nextRefreshAt = now + 0.1f')) { throw 'Foreign/health HUD idle gate or active cadence changed.' }
$health = Read-Source 'SelectedUnitHealthFeature'
$refresh = $health.Substring($health.IndexOf('internal void Refresh()'))
if ($refresh.IndexOf('if (!settings.EnableClientFeatures') -gt $refresh.IndexOf('MainViewModel.Instance')) {
    throw 'Disabled health display reaches game singletons.'
}
$countdown = Read-Source 'TimerCountdownFeature'
if (-not $countdown.Contains('if (!enabled && Volatile.Read(ref clearPending) == 0) return;') -or
    -not $countdown.Contains('private volatile bool enabled;')) { throw 'Countdown idle/atomic activation contract missing.' }
$surrender = Read-Source 'SurrenderFeature'
foreach ($required in @('Interlocked.Exchange(ref statisticsBadgeModeSnapshot, next)',
    'Interlocked.Exchange(ref statisticsBadgeClearPending, 0)',
    'lobbyReturnFeature.OnBeforeRender()',
    'if (spectatorPromotionActivated && !spectatorPromotionConfirmed) ConfirmSpectatorPromotion()')) {
    if (-not $surrender.Contains($required)) { throw "Missing independent surrender task gate: $required" }
}
if ($RunTests) {
    $msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
    & $msbuild (Join-Path $PSScriptRoot 'tests\ModOptions.Tests\ModOptions.Tests.csproj') /p:Configuration=Release /verbosity:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Mod-option regression compilation failed.' }
    & (Join-Path $PSScriptRoot 'tests\ModOptions.Tests\bin\ModOptions.Tests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Mod-option runtime regression failed.' }
}
Write-Output 'PASS: mod-option performance source contracts; active HUD cadence and pending lobby/spectator work preserved.'
