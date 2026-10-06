[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$projectPath = Join-Path $PSScriptRoot 'EnemyBridgePathTest.csproj'
[xml]$project = [IO.File]::ReadAllText($projectPath)
$sources = @($project.SelectNodes("//*[local-name()='Compile']") | ForEach-Object {
    (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot $_.Include)).Path
})
$forbidden = 'JavaScriptSerializer|System\.Web\.Extensions|System\.Text\.Json|Newtonsoft|DataContractJsonSerializer|JsonUtility'
foreach ($path in @($projectPath) + $sources) {
    $text = [IO.File]::ReadAllText($path)
    if ($text -match $forbidden) { throw "Forbidden runtime JSON dependency: $path" }
    if ($text -match '\b(?:void|IEnumerator)\s+(?:OnDestroy|OnDisable|OnApplicationQuit|Update|LateUpdate|FixedUpdate|Start|OnEnable)\s*\(' -or
        $text -match '\bStartCoroutine\s*\(') { throw "Runtime lifecycle callback requires audit: $path" }
    if ($text -match '\b(?:VirtualProtect|FlushInstructionCache)\s*\(|\bCodePatch\.Write\s*\(|\bMarshal\.Write\w*\s*\(|\.(?:Disable|Undo)\s*\(') {
        throw "Runtime executable mutation or published hook teardown requires audit: $path"
    }
}
foreach ($path in $sources) {
    if ($path -like '*EnemyBridgePathTest\src\*' -and [IO.File]::ReadAllText($path) -match '\.(?:Dispose|Apply)\s*\(') { throw "Published diagnostic runtime teardown: $path" }
}
# Shared lifecycle Pair.Dispose only releases observer tokens; the static plugin retains its Pair.
$hookSource = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/BridgeNativeHooks.cs'))
if ([regex]::Matches($hookSource, '\.Enable\s*\(').Count -ne 1 -or
    $hookSource -notmatch 'foreach \(var hook in permanent\) hook\.Enable\(\);' -or
    $hookSource -notmatch 'AllowedSchemes\s*=\s*DetourScheme\.Absolute,') { throw 'Native hook publication contract changed; re-audit required.' }
$traceSource = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/BridgeDecisionTrace.cs'))
$indexSource = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/BridgeBuildingIndex.cs'))
if ($traceSource -match 'params\s+int\[\]|Drain\(true\)|DescribeLiveParent' -or
    $indexSource -match 'TryGetBuildingById' -or
    $traceSource -notmatch 'count<64' -or $traceSource -notmatch 'backgroundQueued\)>4096') { throw 'Lean trace allocation/lookup/drain contract regression' }
if ($traceSource -match '667E8E00|SplitLiveState|Regex|PlanCell' -or
    $traceSource -notmatch 'ReadAssignedTask\(GameUnit\* unit\) => unit->r_AI_ContextTargetBuildingTileId' -or
    $traceSource -notmatch 'unit->r_AIState==0x65\|\|unit->r_AIState==0x67' -or
    $traceSource -notmatch 'WriteRecord\(StampRecord\(new Record \{Kind="session-end"') { throw 'Numeric capture/task-state/end-delivery contract regression' }
$installed = [Reflection.Assembly]::LoadFrom('E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese\SHCDESE.dll')
$unitType = $installed.GetType('SHCDESE.Interop.GameUnit',$true)
foreach ($member in @(@('r_AI_ContextTargetBuildingTileId',0x3A4,'UInt32'),@('r_AIState',0x2BC,'UInt16'),@('r_AI_LastIssuedTribeCommand',0x398,'UInt16'))) {
    $field = $unitType.GetField($member[0])
    if (!$field -or !$field.IsPublic -or $field.FieldType.Name -ne $member[2] -or [Runtime.InteropServices.Marshal]::OffsetOf($unitType,$member[0]).ToInt32() -ne $member[1]) { throw ('Installed assignment member mismatch: '+$member[0]) }
}
# Public path contracts must match the installed Extender, not a publicized game DLL.
foreach ($member in @(@('r_CurrentTilePositionX',0xC0,'UInt16'),@('r_CurrentTilePositionY',0xC2,'UInt16'),@('r_TargetTilePositionX',0xC4,'UInt16'),@('r_TargetTilePositionY',0xC6,'UInt16'),@('r_PreviousTilePositionX',0xC8,'UInt16'),@('r_PreviousTilePositionY',0xCA,'UInt16'),@('r_NextTilePositionX2',0xDC,'UInt16'),@('r_NextTilePositionY2',0xDE,'UInt16'),@('r_PathPlanStateBitFlags',0xF2,'UInt16'),@('r_MovementSubstep',0xF4,'UInt16'),@('r_CurrentPathPlanIndex',0xF6,'UInt16'),@('r_PathPlanLength',0xF8,'UInt16'),@('r_ContextTargetTileX',0x3E4,'UInt16'),@('r_ContextTargetTileY',0x3E6,'UInt16'))) {
    $field = $unitType.GetField($member[0])
    if (!$field -or !$field.IsPublic -or $field.FieldType.Name -ne $member[2] -or [Runtime.InteropServices.Marshal]::OffsetOf($unitType,$member[0]).ToInt32() -ne $member[1]) { throw ('Installed path member mismatch: '+$member[0]) }
}
$pathApi = $installed.GetType('SHCDESE.API.GamePathingManagerAPI',$true)
$pathView = $installed.GetType('SHCDESE.Interop.GameUnitPathPlanView',$true)
$viewMethod = $pathApi.GetMethod('TryGetUnitPathPlanView')
if (!$viewMethod -or !$viewMethod.IsPublic -or $viewMethod.ReturnType -ne [bool] -or $viewMethod.GetParameters()[0].ParameterType -ne [int] -or $viewMethod.GetParameters()[1].ParameterType.GetElementType() -ne $pathView) { throw 'Installed public path view signature mismatch' }
if (!$pathView.GetProperty('PackedBytes').GetMethod.IsPublic -or $pathView.GetProperty('PackedBytes').PropertyType.ToString() -ne 'System.Span`1[System.Byte]') { throw 'Installed public packed buffer mismatch' }
$movementEvent = $installed.GetType('SHCDESE.EventAPI.UnitR3EventHooks',$true).GetField('OnUnitMovement')
if (!$movementEvent -or !$movementEvent.IsPublic -or !$movementEvent.IsStatic -or $movementEvent.FieldType.GetGenericArguments()[0].FullName -ne 'SHCDESE.EventAPI.Units.UnitMovementEventArgs') { throw 'Installed movement publisher mismatch' }
$routeSource = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/BridgeRouteTrace.cs'))
if ($routeSource -match 'TrySet|TryReplace|ClearUnitPath|SetPacked|FindNext|Marshal.Write') { throw 'Passive route observation became a writer/search' }
$textFiles = @(Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File | Where-Object {
    $_.FullName -notmatch '\\(?:bin|obj|BepInEx)\\' -and $_.Extension -in @('.cs','.csproj','.ps1','.bat','.md','.json','.xaml')
})
foreach ($file in $textFiles) {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -match '(?<!\r)\n|\r(?!\n)') { throw "Non-CRLF file: $($file.FullName)" }
}
foreach ($file in @(Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File -Filter '*.xaml')) {
    [xml]$xaml = [IO.File]::ReadAllText($file.FullName)
    foreach ($content in $xaml.SelectNodes("//*[local-name()='Content']")) {
        $elements = @($content.ChildNodes | Where-Object NodeType -eq Element)
        if ($elements.Count -ne 1) { throw "XAML Content requires exactly one root: $($file.FullName)" }
    }
}
& (Join-Path $workspace 'Shared/Test-PermanentNativeRuntimePatches.ps1')
Write-Host "PASS: EnemyBridge runtime JSON/lifecycle, permanent hooks, XAML and CRLF ($($sources.Count) runtime sources)."

# Virtual shadow uses installed public views; raw 107160 reads stay hash-bound.
foreach ($methodSpec in @(@('SHCDESE.API.GameUnitManagerAPI','GetUnitsAsSpan','System.Span`1[SHCDESE.Interop.GameUnit]'),@('SHCDESE.API.GamePathingManagerAPI','GetPathComponentGrid','System.Span`1[System.UInt16]'),@('SHCDESE.API.GamePathingManagerAPI','GetPathEdgeMaskGrid','System.Span`1[System.Byte]'),@('SHCDESE.API.GamePathingManagerAPI','GetPathConnectionRecords','System.Span`1[SHCDESE.Interop.PathConnectionRecord]'),@('SHCDESE.API.GameTileManagerAPI','GetLogicLayer','System.Span`1[System.Int32]'),@('SHCDESE.API.GameTileManagerAPI','GetOrganismLayer','System.Span`1[System.UInt16]'))) {
    $method=$installed.GetType($methodSpec[0],$true).GetMethod($methodSpec[1])
    if (!$method -or !$method.IsPublic -or $method.GetParameters().Count -ne 0 -or $method.ReturnType.ToString() -ne $methodSpec[2]) { throw ('Virtual copy method mismatch: '+$methodSpec[1]) }
}
$recordType=$installed.GetType('SHCDESE.Interop.PathConnectionRecord',$true)
if ($recordType.StructLayoutAttribute.Size -ne 0x204) { throw 'Native connection record size mismatch' }
foreach ($member in @(@('r_IsActive',0,'Int32'),@('r_ConnectionClass',4,'PathConnectionClass'),@('r_RecordGlobalId',8,'UInt32'),@('r_BuildingId',12,'Int32'),@('r_UnitId',16,'Int32'),@('r_SubjectGlobalId',20,'UInt32'),@('r_IsEnabledOrOpen',24,'Int32'),@('r_EntryTileId',36,'Int32'),@('r_ExitTileId',48,'Int32'),@('r_PathComponentA',52,'Int32'),@('r_PathComponentB',56,'Int32'),@('r_OwnerOrAccessPlayerId',0x1E4,'Int32'),@('r_PathComponentC',0x1E8,'Int32'))) {
    $field=$recordType.GetField($member[0])
    if (!$field -or !$field.IsPublic -or $field.FieldType.Name -ne $member[2] -or [Runtime.InteropServices.Marshal]::OffsetOf($recordType,$member[0]).ToInt32() -ne $member[1]) { throw ('Virtual connection member mismatch: '+$member[0]) }
}
foreach ($member in @(@('MapRowLookupTable','System.Int32*'),@('MapColumnLookupTable','System.UInt16*'))) {
    $field=$installed.GetType('SHCDESE.API.GameTileManagerAPI',$true).GetField($member[0])
    if (!$field -or !$field.IsPublic -or $field.FieldType.ToString() -ne $member[1]) { throw ('Packed coordinate member mismatch: '+$member[0]) }
}
$shadowSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/BridgeVirtualShadow.cs'))
$coreSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/VirtualBridgeGraph.cs'))
if ($shadowSource -match 'TryGetBuildingById|TryGetUnitById|FindNext|FindPath|Marshal.Write|TrySet|TryReplace' -or $coreSource -match 'SHCDESE|APIShared|Marshal|IntPtr') { throw 'Virtual shadow is no longer a pure copied-input observer' }
if ($shadowSource -notmatch 'pending.Count>=32' -or $shadowSource -notmatch 'request.Authorization=authorized&&boundary&&deck.Count==0' -or $shadowSource -notmatch 'read\(0x60AD6CC\)!=0\|\|read\(0x60AD6D4\)!=nativeRevision') { throw 'Virtual coherence/negative-policy gate regression' }
Write-Host 'PASS: virtual public copy/member contracts, bounded shadow queue and unvalidated policy fail-open.'
