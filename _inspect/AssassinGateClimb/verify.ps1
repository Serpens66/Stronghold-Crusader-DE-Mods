[CmdletBinding()]
param([switch]$CheckTestApi)
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$roots = @('APIShared', 'BugfixesAndQoL', 'Testmods\AssassinGatehouseClimbTest')
foreach ($relative in $roots) {
    $root = Join-Path $workspace $relative
    $files = @(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
        $_.FullName -notmatch '\\(?:bin|obj|BepInEx|tests)\\' -and
        $_.Extension -in @('.cs','.csproj','.ps1','.bat','.json','.md','.xaml') -and $_.Name -ne 'README.md'
    })
    foreach ($file in $files) {
        $text = [IO.File]::ReadAllText($file.FullName)
        if ($text -match '(?<!\r)\n') { throw "Non-CRLF: $($file.FullName)" }
        if ($file.Extension -in @('.cs','.csproj') -and $text -match 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json') {
            throw "Forbidden runtime JSON dependency: $($file.FullName)"
        }
        if ($file.Extension -eq '.cs' -and $text -match '\b(OnDestroy|OnDisable|OnApplicationQuit|StartCoroutine|LateUpdate|FixedUpdate)\s*\(') {
            throw "Runtime lifecycle callback: $($file.FullName)"
        }
        if ($file.Name -like '*Plugin.cs' -and $text -match '\b(Update|OnApplicationPause)\s*\(') { throw "Plugin callback: $($file.FullName)" }
        if ($file.Extension -eq '.xaml') {
            [xml]$xml = $text
            foreach ($content in @($xml.SelectNodes("//*[local-name()='Content']"))) {
                if (@($content.ChildNodes | Where-Object NodeType -eq 'Element').Count -ne 1) { throw "XAML root: $($file.FullName)" }
            }
        }
    }
}
& (Join-Path $workspace 'Shared\Test-PermanentNativeRuntimePatches.ps1')
if (-not $?) { throw 'Workspace hook regression failed' }
$api = [IO.File]::ReadAllText((Join-Path $workspace 'APIShared\src\AssassinPathAPI.cs'))
if ($api -match '\b(transaction|endpointHooks|builderHook)\??\.Dispose\s*\(' -or
    $api -match '\.(Enable|Disable|Undo)\s*\(' -or $api -notmatch 'if \(builderHook == pendingBuilder\) throw;') {
    throw 'Shared published hook teardown or missing unpublished rollback guard'
}
$main = [IO.File]::ReadAllText((Join-Path $workspace 'BugfixesAndQoL\src\AssassinPathfindingRuntime.cs'))
$reconstruction = [IO.File]::ReadAllText((Join-Path $workspace 'BugfixesAndQoL\src\AssassinPathReconstructionPatch.cs'))
if ($main -match 'AddDetour|AddInline|new HookTransaction' -or $reconstruction -match 'AddDetour|AddInline|new HookTransaction' -or
    -not $main.Contains('if (sharedBuilderRegistered) throw;') -or -not $main.Contains('if (sharedBuilderRegistered) return;')) {
    throw 'Duplicate Assassin hook ownership or published consumer teardown'
}
$test = [IO.File]::ReadAllText((Join-Path $workspace 'Testmods\AssassinGatehouseClimbTest\src\AssassinGatehouseClimbTestPlugin.cs'))
if ($test -match 'CodePatch\.Write|VirtualProtect|Marshal\.Write|AddDetour|AddInline|\.Dispose\s*\(' -or
    -not $test.Contains('private static Runtime runtime;') -or -not $test.Contains('OnTick += OnTick')) { throw 'Testmod lifetime/mutation contract' }
# No new Assembly-CSharp members are accessed by these changes. Validate every new
# test observer member against the installed, genuine Script Extender metadata.
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $game 'BepInEx\core\Mono.Cecil.dll')))
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $game 'BepInEx\plugins\000shcdese\SHCDESE.dll'))
$unit = @($assembly.MainModule.Types | Where-Object FullName -eq 'SHCDESE.Interop.GameUnit')[0]
foreach ($name in @('r_AliveState','r_UnitChimp','r_CurrentPositionTileId','r_NextPositionTileId2','r_AIState','r_GlobalId','r_AI_LastIssuedTribeCommand','r_ControllableForPlayerId','r_TargetTilePositionX2','r_TargetTilePositionY2')) {
    $field = @($unit.Fields | Where-Object Name -ceq $name)
    if ($field.Count -ne 1 -or -not $field[0].IsPublic) { throw "Installed private/missing GameUnit.$name" }
    Write-Host ("Public installed member: GameUnit." + $name + ' : ' + $field[0].FieldType.FullName)
}
foreach ($contract in @(@('SHCDESE.API.GameUnitManagerAPI','GetUnitsAsSpan'), @('SHCDESE.API.GameTimeManagerAPI','add_OnTick'))) {
    $type = @($assembly.MainModule.Types | Where-Object FullName -ceq $contract[0])[0]
    $method = @($type.Methods | Where-Object Name -ceq $contract[1])
    if ($method.Count -ne 1 -or -not $method[0].IsPublic) { throw "Installed API contract: $contract" }
    Write-Host $method[0].FullName
}
Write-Host 'PASS: Assassin shared preflight, lifetime, JSON, XAML, CRLF and installed public-member contracts.'
if ($CheckTestApi) {
$apiAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $game 'BepInEx\plugins\APIShared_Serp\APIShared.dll'))
foreach ($contract in @(@('APIShared.ApiShared','WhenReady'), @('APIShared.IApiShared','TryGetMissionLifecycle'), @('APIShared.IMissionLifecycleCapability','TryRegisterObserver'))) {
    $type = @($apiAssembly.MainModule.Types | Where-Object FullName -ceq $contract[0])[0]
    $method = @($type.Methods | Where-Object Name -ceq $contract[1])
    if ($method.Count -ne 1 -or -not $method[0].IsPublic) { throw "APIShared test observer contract: $contract" }
    Write-Host $method[0].FullName
}
}
