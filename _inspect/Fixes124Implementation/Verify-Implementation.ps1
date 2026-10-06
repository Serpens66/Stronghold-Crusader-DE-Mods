[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$workspace=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$mods=@('ExtendedData','APIShared','Testmods\AIAttackTest','Testmods\FixesSiegeTentPlacementTest','Testmods\FixesBadThingPopularityTest')
$forbidden='System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json'
foreach($relative in $mods) {
    $root=Join-Path $workspace $relative
    $sources=@(Get-ChildItem -LiteralPath (Join-Path $root 'src') -File -Filter '*.cs')
    $projects=@(Get-ChildItem -LiteralPath $root -File -Filter '*.csproj')
    foreach($file in @($sources)+@($projects)) {
        $text=[IO.File]::ReadAllText($file.FullName)
        if($text -match $forbidden) { throw "Runtime JSON dependency: $($file.FullName)" }
        if($file.Name -like '*Plugin.cs' -and $text -match '\b(Update|LateUpdate|FixedUpdate|StartCoroutine|OnDisable|OnApplicationQuit|OnApplicationPause)\s*\(') { throw "Plugin lifetime callback: $($file.FullName)" }
        if($text -match '\b(OnDestroy|OnDisable|OnApplicationQuit)\s*\(' -and $file.Name -ne 'ExtendedDataPlugin.cs') { throw "Unexpected runtime teardown: $($file.FullName)" }
    }
    $texts=@(Get-ChildItem -LiteralPath $root -File -Recurse | Where-Object { $_.FullName -notmatch '\\(?:bin|obj|BepInEx)\\' -and $_.Extension -in @('.cs','.csproj','.ps1','.bat','.json','.md','.xaml','.lordjson','.aivjson') -and $_.Name -ne 'README.md' })
    foreach($file in $texts) {
        $text=[IO.File]::ReadAllText($file.FullName)
        if($text -match '(?<!\r)\n') { throw "Non-CRLF text: $($file.FullName)" }
    }
    foreach($file in @(Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.xaml' | Where-Object { $_.FullName -notmatch '\\BepInEx\\' })) {
        [xml]$xml=Get-Content -LiteralPath $file.FullName -Raw
        foreach($content in @($xml.SelectNodes("//*[local-name()='Content']"))) {
            $elements=@($content.ChildNodes | Where-Object NodeType -eq 'Element')
            if($elements.Count -ne 1) { throw "XAML patch Content must have one root: $($file.FullName)" }
        }
    }
}
& (Join-Path $workspace 'Shared\Test-PermanentNativeRuntimePatches.ps1')
if(-not $?) { throw 'Workspace permanent hook regression failed.' }
$overrides=[IO.File]::ReadAllText((Join-Path $workspace 'Testmods\AIAttackTest\src\AIAttackPermanentNativeOverrides.cs'))
if($overrides -notmatch 'if \(published\) return;' -or $overrides -notmatch 'recruitTransaction' -or $overrides -match '\b(CodePatch\.Write|VirtualProtect|Undo|Disable|Enable)\s*\(') { throw 'AIAttackTest permanent capability contract failed.' }
$se='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese'
$game='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$null=[Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $game 'BepInEx\core\Mono.Cecil.dll')))
$assembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $se 'SHCDESE.dll'))
try {
    $contracts=@{
        GameUnit=@('r_AliveState','r_ControllableForPlayerId','r_UnitChimp','N00000569','N0000019A','r_AITribeRole','r_TribeId','r_AIState','r_GlobalId','r_CurrentTilePositionX','r_CurrentTilePositionY','r_TargetTilePositionX','r_TargetTilePositionY','r_AI_LastIssuedTribeCommand','r_PathPlanStateBitFlags','r_CurrentHealth','r_AssignedEngineer1','r_AssignedEngineer2')
        GamePlayerResources=@('r_CurrentPopularity','r_BadThingBuildAttemptCounter','r_AISiegeState','r_SiegeAttackTargetPlayerId','r_SiegeRallyTileX','r_SiegeRallyTileY','r_AICurrentSiegeEngineerCount','r_KeepDoorTilePositionX','r_KeepDoorTilePositionY')
        GameBuilding=@('r_AliveState','r_GlobalId','r_BuildingType','r_PlayerIdOwner','r_TilePositionXBegin','r_TilePositionYBegin')
        GameUnitManager=@('r_TotalUnits'); GameTribe=@('r_GlobalId','r_PlayerIdOwner','r_LeaderUnitId','r_UnitsInGroup','r_AliveState')
        GameVegetation=@('r_AliveState'); GameVegetationManager=@('VegetationArray')
        AivVillageState=@('SelectedVariantIndex','UnlockedBuildStep','MaximumBuildStep','BuildRate','LowGoldBuildDelayElapsed')
        AivBuildStep=@('BuildingType','State','TileCount','MapTileIdOrBufferIndex','RebuildDelay')
        InternalAIC=@('siege_machines1','siege_machines2','siege_machines3','siege_machines4','siege_machines5','siege_machines6','siege_machines7','siege_machines8','siege_trigger_variance','percent_chance_waiting_for_joint_attack','siege_eng_amount')
    }
    foreach($name in $contracts.Keys) {
        $type=@($assembly.MainModule.Types | Where-Object Name -ceq $name)
        if($type.Count -ne 1 -or -not $type[0].IsPublic) { throw "Interop type missing/private: $name" }
        foreach($member in $contracts[$name]) {
            $field=@($type[0].Fields | Where-Object Name -ceq $member)
            if($field.Count -ne 1 -or -not $field[0].IsPublic) { throw "Interop field missing/private: $name.$member" }
        }
    }
    $mapType=@($assembly.MainModule.Types | Where-Object Name -ceq 'GameMapArchiveManagerAPI')
    foreach($methodName in @('GetCurrentFilePath','IsCurrentFileSaveFile')) {
        $method=@($mapType[0].Methods | Where-Object { $_.Name -ceq $methodName -and $_.Parameters.Count -eq 0 })
        if($method.Count -ne 1 -or -not $method[0].IsPublic) { throw "Public active-map identity method changed: $methodName" }
    }
    $loadContext=@($assembly.MainModule.Types | Where-Object Name -ceq 'CrusaderLibraryLoadContext')
    $moduleProperty=@($loadContext[0].Properties | Where-Object Name -ceq 'ModuleHandle')
    if($loadContext.Count -ne 1 -or -not $loadContext[0].IsPublic -or $moduleProperty.Count -ne 1 -or -not $moduleProperty[0].GetMethod.IsPublic -or $moduleProperty[0].PropertyType.FullName -ne 'System.IntPtr') { throw 'Public native module context changed.' }
} finally { $assembly.Dispose() }
& (Join-Path $workspace 'ExtendedData\Test-RuntimePreflight.ps1')
if(-not $?) { throw 'Real managed assembly contract verification failed.' }
foreach($name in @('FixesSiegeTentPlacementTest','FixesBadThingPopularityTest')) { & (Join-Path $workspace "Testmods\$name\verify.ps1"); if(-not $?) { throw "Observer verification failed: $name" } }
& 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe' (Join-Path $PSScriptRoot 'verify-offline.py')
if($LASTEXITCODE -ne 0) { throw 'Offline provenance verification failed.' }
Write-Host 'PASS: Fixes 1.24 implementation preflight; no runtime build or installation performed.'
