$ErrorActionPreference = 'Stop'
$testDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = (Resolve-Path -LiteralPath (Join-Path $testDir '..\..\..')).Path
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
$work = New-ShortWorkDirectory -Root $workspace -Kind t -Name 'raid-retarget'
$source = Join-Path $workspace 'BugfixesAndQoL\src\AI\Raid\RaidAttackFieldEvaluation.cs'
$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
$executable = Join-Path $work 'RaidRetargetEvaluationTests.exe'
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
    (Join-Path $workspace 'BugfixesAndQoL\src\AI\Raid\RaidSearchEvidence.cs'),
    (Join-Path $workspace 'BugfixesAndQoL\src\AI\Raid\RaidSearchObserver.cs'),
    (Join-Path $workspace 'Shared\Runtime\Diagnostics\DebugLogHelper.cs'),
    (Join-Path $workspace 'APIShared\src\Units\UnitAccess.cs'),
    (Join-Path $workspace 'BugfixesAndQoL\src\AI\Raid\AiRaidRetargetFixRuntime.cs'),
    # TEMP_GATE_ROUTE_ACCEPTANCE: the isolated runtime fixture does not reference APIShared.
    (Join-Path $workspace 'APIShared\src\Pathfinding\GateRoutes\TemporaryGateRouteAcceptanceBridge.cs'),
    (Join-Path $workspace 'APIShared\src\Pathfinding\Assassin\AssassinGateTransitionPolicy.cs'),
    (Join-Path $workspace 'BugfixesAndQoL\src\AI\Raid\RaidActivationState.cs'),
    (Join-Path $testDir 'RuntimeTestStubs.cs'), (Join-Path $testDir 'ActivationTests.cs'), (Join-Path $testDir 'HardeningTests.cs'))
& $compiler /nologo /unsafe /langversion:latest /target:exe "/out:$executable" @references @testSources
if ($LASTEXITCODE -ne 0) { throw 'Attack-field regression test compilation failed.' }
Copy-Item -LiteralPath (Join-Path $testDir 'RaidRetargetEvaluationTests.exe.config') -Destination ($executable + '.config')
& $executable
if ($LASTEXITCODE -ne 0) { throw 'Attack-field regression test failed.' }
