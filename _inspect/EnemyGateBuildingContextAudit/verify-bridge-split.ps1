$ErrorActionPreference = 'Stop'
$workspace = (Get-Location).Path
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $game 'BepInEx/plugins/000shcdese/SHCDESE.dll'))
# Every GameBuilding/GameTribe/GameUnit member read by the new diagnostic sources.
$fields = @{
    GameBuilding = 'r_AliveState','r_BuildingType','r_GlobalId','r_PlayerIdOwner','r_CapturedByPlayerId','r_GatehouseId','r_GateState','r_AIWalkableState','r_SpriteVariationIndex','r_OccupyTileGridSize','r_OccupiedTileIdsArrayBegin'
    GameUnit = 'r_GlobalId','r_TribeId','r_AIState','r_AI_ContextTargetBuildingTileId','r_AI_LastIssuedTribeCommand','r_ContextTargetTileX','r_ContextTargetTileY','r_MoatWorkTaskIndex','r_CurrentTilePositionX','r_CurrentTilePositionY','r_UnitChimp','r_ControllableForPlayerId','N00000569'
    GameTribe = 'r_GlobalId','r_PlayerIdOwner','r_LeaderUnitId'
}
foreach ($typeName in $fields.Keys) {
    $type = $assembly.GetType('SHCDESE.Interop.' + $typeName,$true)
    foreach ($name in $fields[$typeName]) {
        $field = $type.GetField($name)
        if (!$field -or !$field.IsPublic) { throw "Private/missing installed member $typeName.$name" }
        Write-Host "$typeName.$name : public $($field.FieldType.Name) offset=$([Runtime.InteropServices.Marshal]::OffsetOf($type,$name))"
    }
}
$vectorType=$assembly.GetType('SHCDESE.Interop.UnmanagedVector2`1',$true).MakeGenericType([ushort])
foreach ($name in @('X','Y')) {
    $member=$vectorType.GetField($name)
    if (!$member -or !$member.IsPublic -or $member.FieldType -ne [ushort]) { throw "Installed coordinate member mismatch: $name" }
}
foreach ($row in @(@('GameUnit','r_AIState',0x2BC),@('GameUnit','r_AI_ContextTargetBuildingTileId',0x3A4),@('GameUnit','r_AI_LastIssuedTribeCommand',0x398),@('GameUnit','r_ContextTargetTileX',0x3E4),@('GameUnit','r_ContextTargetTileY',0x3E6),@('GameUnit','r_MoatWorkTaskIndex',0x3B4))) {
    if ([Runtime.InteropServices.Marshal]::OffsetOf($assembly.GetType('SHCDESE.Interop.' + $row[0]),$row[1]).ToInt32() -ne $row[2]) { throw 'Native work layout mismatch' }
}
foreach ($row in @(@('GameTribeManagerAPI','TryGetAITribeStorageRole'),@('GameTribeManagerAPI','TryResolveAITribeStorageRole'),@('GameTileManagerAPI','GetTileId'),@('GameTileManagerAPI','IsValidTileId'),@('GameTileManagerAPI','GetTilePropertyFlag'),@('GameTileManagerAPI','GetTileVectorFromId'),@('GameTileManagerAPI','GetMoatWorkTaskIndexLayer'),@('GamePathingManagerAPI','GetPathComponentGrid'),@('GameUnitManagerAPI','GetUnitsAsSpan'),@('GameBuildingManagerAPI','GetBuildingsAsSpan'))) {
    $methods = @($assembly.GetType('SHCDESE.API.' + $row[0],$true).GetMethods() | Where-Object Name -eq $row[1])
    if (!$methods.Count) { throw "Missing installed method $($row[1])" }
    foreach ($method in $methods) { Write-Host ($method.ToString()) }
}
$tickEvent = $assembly.GetType('SHCDESE.API.GameTimeManagerAPI',$true).GetEvent('OnTick')
if (!$tickEvent -or !$tickEvent.AddMethod.IsPublic -or $tickEvent.EventHandlerType.FullName -ne 'System.Action`1[[System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]') {
    if (!$tickEvent -or !$tickEvent.AddMethod.IsPublic -or $tickEvent.EventHandlerType.ToString() -ne 'System.Action`1[System.Int32]') { throw 'Installed OnTick signature mismatch' }
}
Write-Host ('Installed persistent publisher: ' + $tickEvent.ToString())
$buildingHooks = $assembly.GetType('SHCDESE.EventAPI.BuildingR3EventHooks',$true)
foreach ($eventName in @('OnBuildingSpawn','OnBuildingDelete')) {
    $eventField = $buildingHooks.GetField($eventName)
    if (!$eventField -or !$eventField.IsPublic) { throw "Missing installed building event $eventName" }
}
foreach ($apiName in @('GameBuildingManagerAPI','GameUnitManagerAPI','GameTribeManagerAPI')) {
    $idMethod = $assembly.GetType('SHCDESE.API.'+$apiName,$true).GetMethod('IsValidId',[type[]]@([int]))
    if (!$idMethod -or !$idMethod.IsPublic -or $idMethod.ReturnType -ne [bool]) { throw "Missing installed ID validator $apiName" }
}
foreach ($mod in @('APIShared','BugfixesAndQoL','Testmods/EnemyGatePathfindingTest','Testmods/EnemyBridgePathTest')) {
    $files = @(Get-ChildItem -LiteralPath (Join-Path $workspace $mod) -Recurse -File | Where-Object { $_.FullName -notmatch '\\(?:bin|obj|BepInEx)\\' -and $_.Extension -in '.cs','.csproj' })
    foreach ($file in $files) {
        $text = [IO.File]::ReadAllText($file.FullName)
        # Test fixtures outside runtime are permitted serializers; runtime files are always checked.
        if ($file.FullName -notmatch '\\tests\\' -and $text -match 'JavaScriptSerializer|System\.Web\.Extensions|System\.Text\.Json|Newtonsoft|DataContractJsonSerializer|JsonUtility') { throw "JSON dependency $($file.FullName)" }
        if ($text -match '\b(?:void|IEnumerator)\s+(?:OnDestroy|OnDisable|OnApplicationQuit|LateUpdate|FixedUpdate|OnEnable)\s*\(' -or $text -match '\bStartCoroutine\s*\(') { throw "Lifecycle $($file.FullName)" }
        if ($file.Name -match 'Plugin' -and $text -match '\bvoid\s+(?:Update|Start)\s*\(') { throw 'Plugin runner' }
    }
    foreach ($file in @(Get-ChildItem -LiteralPath (Join-Path $workspace $mod) -Recurse -File -Filter '*.xaml' | Where-Object FullName -NotMatch '\\(?:bin|obj|BepInEx)\\')) {
        [xml]$xml = [IO.File]::ReadAllText($file.FullName)
        foreach ($node in $xml.SelectNodes("//*[local-name()='Content']")) {
            if (@($node.ChildNodes | Where-Object NodeType -eq Element).Count -ne 1) { throw "XAML Content roots $($file.FullName)" }
        }
    }
}
& (Join-Path $workspace 'Shared/Test-PermanentNativeRuntimePatches.ps1')
Write-Host 'PASS: bridge split installed members, JSON/lifecycle, XAML and permanent hooks'
