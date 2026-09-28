$ErrorActionPreference = 'Stop'
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$flags = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static'
$api = [Reflection.Assembly]::LoadFrom((Join-Path $game 'BepInEx\plugins\APIShared_Serp\APIShared.dll'))
[Reflection.Assembly]::LoadFrom((Join-Path $game 'BepInEx\plugins\000shcdese\SHCDESE.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $game 'BepInEx\core\BepInEx.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $game 'BepInEx\plugins\000shcdese\R3.dll')) | Out-Null
$test = [Reflection.Assembly]::LoadFrom((Join-Path $game 'BepInEx\plugins\AIBuildDiagnoseTest_Serp\AIBuildDiagnoseTest.dll'))
$runtime = $test.GetType('AIBuildDiagnoseTest.AIBuildDiagnoseRuntime', $true)
$attemptType = $runtime.GetNestedType('Attempt', [Reflection.BindingFlags]'NonPublic')
$sampleType = $api.GetType('APIShared.AiPathTileSample', $true)
$snapshotType = $api.GetType('APIShared.AiNearbyPathEvidence', $true)
$sampleCtor = $sampleType.GetConstructors($flags)[0]
$snapshotCtor = $snapshotType.GetConstructors($flags)[0]
$attemptCtor = $attemptType.GetConstructors($flags)[0]
$classify = $runtime.GetMethod('AnalyzeNearby', $flags)

function New-Sample([int]$x, [int]$y, [int]$native, [int]$view) {
    return $sampleCtor.Invoke(@($x, $y, 221520, $native, $view, 'ok'))
}

function New-Snapshot([int]$targetNative, [int]$targetView, [bool]$after) {
    $anchors = [Array]::CreateInstance($sampleType, 25)
    $index = 0
    for ($dx = -2; $dx -le 2; $dx++) {
        for ($dy = -2; $dy -le 2; $dy++) {
            $x = (69 + $dx) * 5
            $y = (98 + $dy) * 5
            $native = if ($x -eq 345 -and $y -eq 485) { $targetNative } else { 3 }
            $view = if ($x -eq 345 -and $y -eq 485) { $targetView } else { 3 }
            $anchors.SetValue((New-Sample $x $y $native $view), $index)
            $index++
        }
    }
    $footprint = [Array]::CreateInstance($sampleType, 0)
    $resultX = if ($after) { 69 } else { -1 }
    $resultY = if ($after) { 97 } else { -1 }
    return $snapshotCtor.Invoke(@('ok', 69, 98, $resultX, $resultY, $anchors, $footprint))
}

function New-Attempt([int]$beforeNative, [int]$beforeView,
        [int]$afterNative, [int]$afterView, [int]$regionCalls = 1,
        [bool]$rejectedZero = $false, [bool]$overridden = $false) {
    $attempt = $attemptCtor.Invoke(@(6))
    $attemptType.GetField('NearbyBefore', $flags).SetValue($attempt,
        (New-Snapshot $beforeNative $beforeView $false))
    $attemptType.GetField('NearbyAfter', $flags).SetValue($attempt,
        (New-Snapshot $afterNative $afterView $true))
    $attemptType.GetField('NearbyRegionCallCount', $flags).SetValue($attempt, $regionCalls)
    $attemptType.GetField('AnyRejectedZeroTarget', $flags).SetValue($attempt, $rejectedZero)
    $attemptType.GetField('AnyRegionOverride', $flags).SetValue($attempt, $overridden)
    return $attempt
}

function Assert-Case([string]$name, $attempt, [string]$expected) {
    $actual = [string]$classify.Invoke($null, @($attempt))
    if ($actual -cne $expected) { throw "$name expected $expected but got $actual" }
    Write-Output "PASS ${name}: $actual"
}

Assert-Case 'zero-component rejected by nearby route' `
    (New-Attempt 0 0 0 0 1 $true) 'nearby-returned-after-rejected-zero-region-call'
Assert-Case 'valid component remains stable' `
    (New-Attempt 3 3 3 3) 'nearby-selection-unexplained-by-observed-region-calls'
Assert-Case 'component changed during search' `
    (New-Attempt 3 3 0 0) 'component-changed-during-nearby-search'
Assert-Case 'native and extender views differ' `
    (New-Attempt 3 0 3 0) 'native-api-component-view-divergence'
Assert-Case 'existing hook changes result' `
    (New-Attempt 3 3 3 3 1 $false $true) 'existing-region-hook-changed-result'
Assert-Case 'region call unavailable' `
    (New-Attempt 3 3 3 3 0) 'nearby-region-call-unobserved'
$missing = $attemptCtor.Invoke(@(6))
Assert-Case 'map change or missing snapshot' $missing 'nearby-snapshot-unobserved'
