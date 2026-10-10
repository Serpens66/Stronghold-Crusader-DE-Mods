param([string]$Baseline = '38d9ccc888301018a83273c133aaf08d14252392')
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$files = @(
    'BugfixesAndQoL/src/AssassinClimbRuntime.cs',
    'BugfixesAndQoL/src/MultiplayerGameSpeedRuntime.cs',
    'BugfixesAndQoL/src/QuarryPileRelocationRuntime.cs',
    'BugfixesAndQoL/src/SiegeAmmoRestockFeature.cs',
    'BugfixesAndQoL/src/SingleBuildingPauseHook.cs',
    'BugfixesAndQoL/src/SurrenderFeature.cs',
    'ExtraFeatures/src/GatehouseAutomationRuntime.cs',
    'ExtraFeatures/src/KnightDismountRuntime.cs',
    'RandomEvents/src/RandomEventsRuntime.cs',
    'ExtremePowers/api/Networking/ExtremePowerNetworkRuntime.cs'
)
$rows = @(foreach ($file in $files) {
    $old = (@(git -C $workspace show ($Baseline + ':' + $file)) -join "`n") + "`n"
    if ($LASTEXITCODE -ne 0) { throw ('Baseline extraction failed: ' + $file) }
    $expected = [regex]::Replace($old, '(?:BugfixesAndQoL|ExtraFeatures|RandomEvents|ExtremePower)ChoreSender\.', 'APIShared.Networking.ChoreTransport.')
    $expected = [regex]::Replace($expected, ',\n[ \t]*\(\) => (?:SHCDESE\.GameGlobals\.)?GameGlobalsManager\.Instance\.ChoreManagerVA\)', ')')
    $expected = [regex]::Replace($expected, '(?m)^[ \t]*(?:value => GameNetworkAPI\.Serialize\(value\),|\(\) => (?:SHCDESE\.GameGlobals\.)?GameGlobalsManager\.Instance\.ChoreManagerVA,|\(value, id\) => GameNetworkAPI\.SendPacketToAllEx2\(value, id, viaChore: true\),)\n', '')
    $current = [IO.File]::ReadAllText((Join-Path $workspace $file)).Replace("`r`n", "`n")
    $current = [regex]::Replace($current, '(?m)^[ \t]*if \(!APIShared\.Networking\.ChoreTransport\.IsChoreDelivery\(args\)\)\n[ \t]*return;\n\n', '')
    [pscustomobject]@{ File = $file; MatchesOldExceptTransport = [string]::Equals($expected, $current, [StringComparison]::Ordinal) }
})
$rows | Format-Table -AutoSize
$json = [regex]::Replace(($rows | ConvertTo-Json), '\r?\n', "`r`n") + "`r`n"
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'runtime-equivalence.json'), $json, [Text.UTF8Encoding]::new($false))
if ($rows.MatchesOldExceptTransport -contains $false) { throw 'Unexpected runtime difference from the working Git baseline.' }
Write-Host 'PASS: all ten runtime files match the working Git baseline exactly except shared transport calls and dedicated-Chore receive guards.'
