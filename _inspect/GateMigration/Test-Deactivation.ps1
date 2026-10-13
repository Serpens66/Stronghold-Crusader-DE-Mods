$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$driver=Join-Path $root 'BugfixesAndQoL/Disable-LegacyGateTest.ps1'
$fixture=Join-Path $PSScriptRoot ('deactivation-fixture-'+[Guid]::NewGuid().ToString('N'))
$legacy=Join-Path $fixture 'BepInEx/plugins/EnemyGatePathfindingTest_Serp'
$null=New-Item -ItemType Directory -Path $legacy -Force
[IO.File]::WriteAllBytes((Join-Path $legacy 'EnemyGatePathfindingTest.dll'),[byte[]](1,2,3,4))
[IO.File]::WriteAllText((Join-Path $legacy 'info.json'),'{"GUID":"EnemyGatePathfindingTest_Serp"}')
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $driver -GameDirectory $fixture
if($LASTEXITCODE-ne 0){throw 'Initial deactivation failed'}
$backup=@(Get-ChildItem -LiteralPath (Join-Path $fixture 'BepInEx/disabledmods') -Recurse -File)
if($backup.Count-ne 1 -or [Convert]::ToBase64String([IO.File]::ReadAllBytes($backup[0].FullName))-ne 'AQIDBA=='){throw 'Backup corrupted'}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $driver -GameDirectory $fixture
if($LASTEXITCODE-ne 0){throw 'Repeated deactivation failed'}
[IO.File]::WriteAllBytes((Join-Path $legacy 'EnemyGatePathfindingTest.dll'),[byte[]](1,2,3,4))
$ErrorActionPreference='Continue'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $driver -GameDirectory $fixture 2>&1 | Out-String | Write-Host
$ErrorActionPreference='Stop'
if($LASTEXITCODE-eq 0 -or -not(Test-Path -LiteralPath (Join-Path $legacy 'EnemyGatePathfindingTest.dll'))){throw 'Backup conflict did not preserve input'}
Write-Host 'PASS: real PS5.1 helper, hash-preserving move, repeat and conflict refusal; fixture retained.'
