[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$roots = @('.')
$excludedSegments = @(
    '\.git\', '\bin\', '\obj\', '\tests\', '\.inspect\', '\_inspect\',
    '\.release-output\', '\BepInEx\plugins\', '\packages\', '\shcde-script-extender\'
)
$files = foreach ($relativeRoot in $roots) {
    $root = Join-Path $workspace $relativeRoot
    if (-not (Test-Path -LiteralPath $root)) { continue }
    Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.cs' -ErrorAction SilentlyContinue | Where-Object {
        $path = $_.FullName
        -not ($excludedSegments | Where-Object { $path.Contains($_) })
    }
}

$errors = [System.Collections.Generic.List[string]]::new()
foreach ($file in $files) {
    $lines = [System.IO.File]::ReadAllLines($file.FullName)
    for ($index = 0; $index -lt $lines.Length; $index++) {
        $line = $lines[$index]
        if ($line -match '\.Hook\.(Enable|Disable)\s*\(' -or
            $line -match '\bCodePatch\.Write\s*\(' -or
            $line -match '\bGatehouseNativeMutation\b' -or
            $line -match '\bVirtualProtect\s*\(' -or
            $line -match '\bFlushInstructionCache\s*\(') {
            $errors.Add("$($file.FullName):$($index + 1): runtime executable-memory mutation: $($line.Trim())")
        }

        if ($line -notmatch '(classifierTransaction|transaction)\??\.Dispose\s*\(') { continue }
        $start = [Math]::Max(0, $index - 10)
        $context = [string]::Join("`n", $lines[$start..$index])
        $isInitializationRollback =
            $context -match '\bcatch\b' -or
            $context -match '!published' -or
            $context -match '!nativeInitialized' -or
            $context -match 'RollbackUnpublished' -or
            $context -match 'DisplacedByteCount' -or
            $context -match 'could not be installed'
        if (-not $isInitializationRollback) {
            $errors.Add("$($file.FullName):$($index + 1): published transaction teardown is not an initialization rollback")
        }
    }
}

$permanentManagedContracts = @(
    @{
        Path = 'BugfixesAndQoL\src\BugfixesAndQoLRuntime.cs'
        Required = @('ReconcilePermanentClientHook(')
        Forbidden = @(
            'DisposeFeature("market autotrade sell threshold"',
            'DisposeFeature("enemy-proximity bulldoze cursor"',
            'DisposeFeature("HD market view"',
            'DisposeFeature("camera movement modifier"',
            'DisposeFeature("Custom Trail starting-gold fix"'
        )
    },
    @{
        Path = 'BugfixesAndQoL\src\SingleBuildingPauseHook.cs'
        Required = @('!localHooksInstalled || !IsFeatureActive()')
        Forbidden = @('addChimpActionsHook.Apply()', 'addChimpActionsHook.Undo()')
    },
    @{
        Path = 'BugfixesAndQoL\src\QuarryPileRelocationRuntime.cs'
        Required = @('Keep the published', 'MonoMod hook')
        Forbidden = @('setUpInbuildingHook?.Undo()', 'setUpInbuildingHook?.Dispose()')
    }
)

foreach ($contract in $permanentManagedContracts) {
    $path = Join-Path $workspace $contract.Path
    if (-not (Test-Path -LiteralPath $path)) {
        $errors.Add("$path`: permanent managed-hook contract source is missing")
        continue
    }

    $source = [System.IO.File]::ReadAllText($path)
    foreach ($required in $contract.Required) {
        if (-not $source.Contains($required)) {
            $errors.Add("$path`: permanent managed-hook contract is missing '$required'")
        }
    }
    foreach ($forbidden in $contract.Forbidden) {
        if ($source.Contains($forbidden)) {
            $errors.Add("$path`: published managed hook can be repatched through '$forbidden'")
        }
    }
}

if ($errors.Count -ne 0) {
    $errors | ForEach-Object { Write-Error $_ }
    throw "Permanent native runtime regression check failed with $($errors.Count) finding(s)."
}

Write-Host "PASS: workspace runtime sources contain no unsafe executable-memory toggles, and audited live-setting MonoMod hooks remain permanently published."
