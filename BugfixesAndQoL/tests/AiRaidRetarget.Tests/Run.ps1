$ErrorActionPreference = 'Stop'
$testDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = (Resolve-Path -LiteralPath (Join-Path $testDir '..\..\..')).Path
$source = Join-Path $workspace 'BugfixesAndQoL\src\RaidAttackFieldEvaluation.cs'
$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
$executable = Join-Path $testDir 'RaidRetargetEvaluationTests.exe'
$env:RAID_TEST_GAME_DIR = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$extender = Join-Path $env:RAID_TEST_GAME_DIR 'BepInEx\plugins\000shcdese'
$references = @('Iced','RedBird.X64','RedBird.Core','RedBird.Abstractions','SHCDESE','R3','System.Memory','Microsoft.Extensions.Logging.Abstractions') |
    ForEach-Object { '/reference:' + (Join-Path $extender ($_ + '.dll')) }
$references += '/reference:' + (Join-Path $env:RAID_TEST_GAME_DIR 'BepInEx\core\BepInEx.dll')
$references += '/reference:' + (Join-Path $env:RAID_TEST_GAME_DIR 'Stronghold Crusader Definitive Edition_Data\Managed\UnityEngine.dll')
$references += '/reference:' + (Join-Path $env:RAID_TEST_GAME_DIR 'Stronghold Crusader Definitive Edition_Data\Managed\UnityEngine.CoreModule.dll')
$references += '/reference:' + (Join-Path $env:RAID_TEST_GAME_DIR 'Stronghold Crusader Definitive Edition_Data\Managed\Assembly-CSharp.dll')
$references += '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8.1\Facades\netstandard.dll'
$testSources = @($source,(Join-Path $testDir 'Program.cs'),(Join-Path $testDir 'NativeSearchTests.cs'),
    (Join-Path $workspace 'BugfixesAndQoL\src\RaidSearchEvidence.cs'),
    (Join-Path $workspace 'BugfixesAndQoL\src\RaidSearchObserver.cs'),
    (Join-Path $workspace 'Shared\DebugLogHelper.cs'),
    (Join-Path $workspace 'APIShared\src\Units\UnitAccess.cs'),
    (Join-Path $workspace 'BugfixesAndQoL\src\AiRaidRetargetFixRuntime.cs'),
    # TEMP_GATE_ROUTE_ACCEPTANCE: the isolated runtime fixture does not reference APIShared.
    (Join-Path $workspace 'APIShared\src\Pathfinding\TemporaryGateRouteAcceptanceBridge.cs'),
    (Join-Path $workspace 'APIShared\src\Pathfinding\AssassinGateTransitionPolicy.cs'),
    (Join-Path $workspace 'BugfixesAndQoL\src\RaidActivationState.cs'),
    (Join-Path $testDir 'RuntimeTestStubs.cs'), (Join-Path $testDir 'ActivationTests.cs'), (Join-Path $testDir 'HardeningTests.cs'))
& $compiler /nologo /unsafe /langversion:latest /target:exe "/out:$executable" @references @testSources
if ($LASTEXITCODE -ne 0) { throw 'Attack-field regression test compilation failed.' }
& $executable
if ($LASTEXITCODE -ne 0) { throw 'Attack-field regression test failed.' }
