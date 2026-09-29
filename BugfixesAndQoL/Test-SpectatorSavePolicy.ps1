$ErrorActionPreference = 'Stop'
$policyPath = Join-Path $PSScriptRoot 'src\SpectatorSavePolicy.cs'
$markerPath = Join-Path $PSScriptRoot 'src\SpectatorSaveMarker.cs'
$types = @(Add-Type -Path @($policyPath, $markerPath) -PassThru -WarningAction SilentlyContinue)
$policyType = $types |
    Where-Object FullName -eq 'BugfixesAndQoL.SpectatorSavePolicy'
$markerType = $types |
    Where-Object FullName -eq 'BugfixesAndQoL.SpectatorSaveMarker'
$method = $policyType.GetMethod('TryRecognize',
    [Reflection.BindingFlags]::NonPublic -bor [Reflection.BindingFlags]::Static)
$markedMethod = $policyType.GetMethod('TryRecognizeMarked',
    [Reflection.BindingFlags]::NonPublic -bor [Reflection.BindingFlags]::Static)
$encodeMethod = $markerType.GetMethod('Encode',
    [Reflection.BindingFlags]::NonPublic -bor [Reflection.BindingFlags]::Static)
$decodeMethod = $markerType.GetMethod('TryDecode',
    [Reflection.BindingFlags]::NonPublic -bor [Reflection.BindingFlags]::Static)
if ($null -eq $method -or $null -eq $markedMethod -or
    $null -eq $encodeMethod -or $null -eq $decodeMethod) {
    throw 'Save policy or marker method unavailable.'
}

function New-Roster {
    $humans = New-Object 'System.Int16[]' 9
    $computers = New-Object 'System.Int16[]' 9
    for ($slot = 0; $slot -le 8; $slot++) { $humans[$slot] = -1; $computers[$slot] = -1 }
    return ,@($humans, $computers)
}

function Assert-Recognition($name, [System.Int16[]]$humans, [System.Int16[]]$computers,
    [bool]$network, [int]$raw,
    [bool]$expected, [int]$expectedView) {
    $values = New-Object 'System.Object[]' 6
    $values[0] = $humans
    $values[1] = $computers
    $values[2] = $network
    $values[3] = $raw
    $values[4] = 0
    $values[5] = $null
    $actual = [bool]$method.Invoke($null, $values)
    if ($actual -ne $expected -or [int]$values[4] -ne $expectedView) {
        throw "$name failed: accepted=$actual view=$($values[4]) reason=$($values[5])"
    }
}

function Assert-MarkedRecognition($name, [System.Int16[]]$humans, [System.Int16[]]$computers,
    [bool]$network, [int]$raw, [int]$marked,
    [bool]$expected, [int]$expectedView) {
    $values = New-Object 'System.Object[]' 7
    $values[0] = $humans
    $values[1] = $computers
    $values[2] = $network
    $values[3] = $raw
    $values[4] = $marked
    $values[5] = 0
    $values[6] = $null
    $actual = [bool]$markedMethod.Invoke($null, $values)
    if ($actual -ne $expected -or [int]$values[5] -ne $expectedView) {
        throw "$name failed: accepted=$actual view=$($values[5]) reason=$($values[6])"
    }
}

function Assert-Marker($name, [byte[]]$bytes, [bool]$expected, [int]$expectedView) {
    $values = New-Object 'System.Object[]' 2
    $values[0] = $bytes
    $values[1] = 0
    $actual = [bool]$decodeMethod.Invoke($null, $values)
    if ($actual -ne $expected -or [int]$values[1] -ne $expectedView) {
        throw "$name failed: decoded=$actual view=$($values[1])"
    }
}

$roster = New-Roster
1..8 | ForEach-Object { $roster[1][$_] = 2 }
Assert-Recognition 'Vanilla all CPU' $roster[0] $roster[1] $false 1 $true 1
Assert-Recognition 'Mod selected player' $roster[0] $roster[1] $false 5 $true 5
Assert-Recognition 'Zero view falls back to CPU' $roster[0] $roster[1] $false 0 $true 1
Assert-Recognition 'Eliminated network player' $roster[0] $roster[1] $true 1 $false 0
Assert-Recognition 'Invalid native view' $roster[0] $roster[1] $false -1 $false 0
$roster[0][1] = 1
Assert-Recognition 'Real human player' $roster[0] $roster[1] $false 1 $false 0

$synthetic = New-Roster
2..8 | ForEach-Object { $synthetic[1][$_] = 2 }
$synthetic[0][1] = 1
Assert-Recognition 'Ambiguous synthetic human' $synthetic[0] $synthetic[1] $false 1 $false 0
$synthetic[0][1] = 2
Assert-Recognition 'Wrong synthetic value' $synthetic[0] $synthetic[1] $false 1 $false 0
$synthetic[0][1] = 1
$synthetic[0][3] = 3
Assert-Recognition 'Additional human registration' $synthetic[0] $synthetic[1] $false 1 $false 0
$empty = New-Roster
Assert-Recognition 'No CPU players' $empty[0] $empty[1] $false 0 $false 0

$spectatorBytes = [byte[]]$encodeMethod.Invoke($null, @(5))
$normalBytes = [byte[]]$encodeMethod.Invoke($null, @(0))
Assert-Marker 'Spectator marker' $spectatorBytes $true 5
Assert-Marker 'Explicit normal-save marker' $normalBytes $true 0
$badVersion = [byte[]]$spectatorBytes.Clone()
$badVersion[4] = 2
Assert-Marker 'Unsupported marker version' $badVersion $false 0
$badFlag = [byte[]]$spectatorBytes.Clone()
$badFlag[5] = 0
Assert-Marker 'Inconsistent marker flag' $badFlag $false 0
Assert-Marker 'Truncated marker' ([byte[]]@(1, 2, 3)) $false 0
Assert-Marker 'Absent marker' $null $false 0

$allCpu = New-Roster
1..8 | ForEach-Object { $allCpu[1][$_] = 2 }
Assert-MarkedRecognition 'Marked all-CPU save' $allCpu[0] $allCpu[1] $false 1 5 $true 5
Assert-MarkedRecognition 'Marked save in network game' $allCpu[0] $allCpu[1] $true 1 5 $false 0
Assert-MarkedRecognition 'Marked CPU missing' $allCpu[0] $allCpu[1] $false 1 0 $false 0
Assert-MarkedRecognition 'Invalid marked native view' $allCpu[0] $allCpu[1] $false 9 5 $false 0
$allCpu[1][5] = -1
Assert-MarkedRecognition 'Marked selected slot not CPU' $allCpu[0] $allCpu[1] $false 1 5 $false 0

$syntheticMarked = New-Roster
2..8 | ForEach-Object { $syntheticMarked[1][$_] = 2 }
$syntheticMarked[0][1] = 1
Assert-MarkedRecognition 'Marked synthetic human' $syntheticMarked[0] $syntheticMarked[1] $false 1 5 $true 5
$syntheticMarked[0][3] = 1
Assert-MarkedRecognition 'Marked additional human' $syntheticMarked[0] $syntheticMarked[1] $false 1 5 $false 0
$syntheticMarked[0][3] = -1
$syntheticMarked[0][1] = 2
Assert-MarkedRecognition 'Marked non-synthetic human value' $syntheticMarked[0] $syntheticMarked[1] $false 1 5 $false 0

Write-Output 'PASS: spectator-save marker and recognition fixtures.'
