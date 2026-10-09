[CmdletBinding()]
param([switch]$CheckTestApi)
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$roots = @('APIShared', 'BugfixesAndQoL')
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
& (Join-Path $workspace 'Shared\Tools\Validation\Test-PermanentNativeRuntimePatches.ps1')
if (-not $?) { throw 'Workspace hook regression failed' }
$api = [IO.File]::ReadAllText((Join-Path $workspace 'APIShared\src\Pathfinding\Assassin\AssassinPathAPI.cs'))
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
$runtime = [IO.File]::ReadAllText((Join-Path $workspace 'BugfixesAndQoL\src\BugfixesAndQoLRuntime.cs'))
$view = [IO.File]::ReadAllText((Join-Path $workspace 'BugfixesAndQoL\src\BugfixesAndQoLViewModel.cs'))
foreach ($required in @('TryInitializePersistentFeature("Assassin gatehouse climb", ApplyAssassinGatehouseClimbFix);', 'TryApplyFeature("Assassin gatehouse climb", ApplyAssassinGatehouseClimbFix);', 'BugfixesAndQoLPlugin.PluginGuid, settings.EnableMod && settings.EnableAssassinGatehouseClimbFix')) {
    if (-not $runtime.Contains($required)) { throw "Missing integrated climb contract: $required" }
}
foreach ($required in @('private bool enableAssassinGatehouseClimbFix = true;', 'EnableAssassinGatehouseClimbFix = true;', 'SetSetting(ref enableAssassinGatehouseClimbFix, value, nameof(EnableAssassinGatehouseClimbFix))')) {
    if (-not $view.Contains($required)) { throw "Missing host climb contract: $required" }
}
if ($view -notmatch '\[SyncHostOnly\]\s+public bool EnableAssassinGatehouseClimbFix') { throw 'Climb setting must be host-only' }
$xaml = [IO.File]::ReadAllText((Join-Path $workspace 'BugfixesAndQoL\Override\ScriptExtenderUI\BugfixesAndQoLSettings.xaml'))
if ($xaml.IndexOf('Binding EnableAssassinGatehouseClimbFix,') -lt $xaml.IndexOf('Binding FixesTitleText')) { throw 'Climb setting outside Fixes section' }
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $workspace 'BugfixesAndQoL\Locales') -Filter '*.txt') {
    $locale = [IO.File]::ReadAllText($file.FullName)
    foreach ($key in @('EnableAssassinGatehouseClimbFix','EnableAssassinGatehouseClimbFixHelp')) {
        if ([regex]::Matches($locale, '(?m)^BugfixesAndQoL\.' + $key + '=').Count -ne 1) { throw "Climb locale key mismatch: $file/$key" }
    }
}
# No new Assembly-CSharp members are accessed by these changes. Validate every new
# test observer member against the installed, genuine Script Extender metadata.
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $game 'BepInEx\core\Mono.Cecil.dll')))
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $game 'BepInEx\plugins\000shcdese\SHCDESE.dll'))
$unit = @($assembly.MainModule.Types | Where-Object FullName -eq 'SHCDESE.Interop.GameUnit')[0]
foreach ($name in @('r_AliveState','r_UnitChimp','r_CurrentPositionTileId','r_NextPositionTileId2','r_AIState','r_GlobalId','r_AI_LastIssuedTribeCommand','r_ControllableForPlayerId','r_TargetTilePositionX2','r_TargetTilePositionY2','r_PathPlanStateBitFlags','r_MovementSubstep','r_CurrentTilePositionX','r_CurrentTilePositionY','r_NextTilePositionX2','r_NextTilePositionY2','r_IsKilledByProjectile','r_CurrentSpeed')) {
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
$installedShared = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $game 'BepInEx\plugins\APIShared_Serp\APIShared.dll'))
$pathApi = @($installedShared.MainModule.Types | Where-Object FullName -eq 'APIShared.AssassinPathAPI')[0]
$settingMethod = @($pathApi.Methods | Where-Object Name -eq 'SetDirectGatehouseClimbing')
if ($settingMethod.Count -ne 1 -or -not $settingMethod[0].IsPublic -or -not $settingMethod[0].IsStatic -or
    $settingMethod[0].ReturnType.FullName -ne 'System.Void' -or $settingMethod[0].Parameters.Count -ne 2 -or
    $settingMethod[0].Parameters[0].ParameterType.FullName -ne 'System.String' -or
    $settingMethod[0].Parameters[1].ParameterType.FullName -ne 'System.Boolean') { throw 'Installed shared climb setting contract changed' }
Write-Host 'PASS: installed public static AssassinPathAPI.SetDirectGatehouseClimbing(string,bool).'
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
