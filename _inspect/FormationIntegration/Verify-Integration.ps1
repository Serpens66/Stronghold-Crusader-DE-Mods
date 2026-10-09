param()
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
& (Join-Path $PSScriptRoot 'Verify-Interop.ps1')
if (-not $?) { throw 'Installed interop audit failed' }
& (Join-Path $workspace 'Shared\Tools\Validation\Test-UnitCommandSplit.ps1')
if (-not $?) { throw 'Runtime preflight failed' }
$shared = Join-Path $workspace 'BugfixesAndQoL\src\UnitCommands'
$runtime = (@(Get-ChildItem -LiteralPath (Join-Path $workspace 'BugfixesAndQoL\src\Formations') -File -Filter 'FormationRuntime.*.cs' | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n")
$renderer = [IO.File]::ReadAllText((Join-Path $workspace 'BugfixesAndQoL\src\Formations\Markers\LargeMoveTargetMarkerRenderer.cs'))
$feature = [IO.File]::ReadAllText((Join-Path $workspace 'BugfixesAndQoL\src\FormationFeature.cs'))
if ($runtime -match '\bAddDetour\(|\bAddContextHook\(|\.Undo\(|engineRunHook\??\.Dispose\(|cameraUpdateHook\??\.Dispose\(') { throw 'Competing native installation or published hook teardown' }
if ($runtime -notmatch 'HookConfig \{ ManualApply = true' -or
    $runtime -notmatch 'commandRuntime\.formationRuntime = this' -or
    $feature -notmatch 'private static FormationRuntime runtime' -or
    $renderer -notmatch 'if \(transaction == null\) candidate\.Dispose\(\)') { throw 'Permanent publication roots/rollback guard missing' }
foreach ($name in @('MoveFormationCommandContext','MoveFormationPreviewPlanner','MoveFormationSpacingPolicy')) {
    if (Test-Path -LiteralPath (Join-Path $shared ($name + '.cs'))) { throw "Obsolete density file $name" }
}
foreach ($mod in @('APIShared','BugfixesAndQoL')) {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $workspace $mod) -Recurse -File -Include '*.cs','*.csproj','*.xaml' | Where-Object { $_.FullName -notmatch '\\(BepInEx|bin|obj)\\' }) {
        if ([IO.File]::ReadAllText($file.FullName) -match '(?<!\r)\n') { throw "Bare LF $($file.FullName)" }
    }
}
Write-Output 'PASS: integrated formation hooks, rooting, JSON/lifecycle, native/real-assembly, XAML and CRLF.'
