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
    $runtimeText=[IO.File]::ReadAllText($path)
    if ($path -like '*EnemyBridgePathTest\src\BridgeNativeHooks.cs') {
        if ($runtimeText -notmatch 'if\(publicationStarted\)throw' -or $runtimeText -notmatch 'if\(hook.IsInstalled\)throw' -or
            $runtimeText -notmatch 'catch \{RollbackUnpublished\(\);throw;\}' -or
            [regex]::Matches($runtimeText,'\(\(IDisposable\)permanent\[i\]\)\.Dispose\(\);').Count -ne 1) { throw 'Unpublished native rollback guard changed' }
        $runtimeText=$runtimeText.Replace('((IDisposable)permanent[i]).Dispose();','/* guarded unpublished candidate release */')
    }
    if ($path -like '*EnemyBridgePathTest\src\*' -and $runtimeText -match '\.(?:Dispose|Apply)\s*\(') { throw "Published diagnostic runtime teardown: $path" }
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
$memoryProperty=$installed.GetType('SHCDESE.API.LowLevel.CrusaderLibraryLoadContext',$true).GetProperty('Memory')
if (!$memoryProperty -or !$memoryProperty.GetMethod.IsPublic -or $memoryProperty.PropertyType.ToString() -ne 'System.ReadOnlySpan`1[System.Byte]') { throw 'Installed immutable load-time snapshot contract changed' }
$unitType = $installed.GetType('SHCDESE.Interop.GameUnit',$true)
$ownerField=$unitType.GetField('r_ControllableForPlayerId')
if (!$ownerField -or !$ownerField.IsPublic -or $ownerField.FieldType.Name -ne 'UInt16' -or [Runtime.InteropServices.Marshal]::OffsetOf($unitType,'r_ControllableForPlayerId').ToInt32() -ne 0x92) {throw 'Installed complete UInt16 unit owner mismatch'}

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
& (Join-Path $workspace 'Shared/Tools/Validation/Test-PermanentNativeRuntimePatches.ps1')
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
if ($shadowSource -notmatch 'pending.Count>=32' -or $shadowSource -notmatch 'request.Authorization=prepared.Authorized&&prepared.Boundary&&prepared.Deck.Length==0' -or $shadowSource -notmatch 'read\(0x60AD6CC\)!=dirty\|\|read\(0x60AD6D4\)!=nativeRevision') { throw 'Virtual coherence/negative-policy gate regression' }
if ($shadowSource -notmatch '!decisionOnly&&dirty!=0' -or $shadowSource -notmatch 'if\(decisionOnly\)\{result.Token=new object\(\);decisionCaptured=result;return;' -or $shadowSource -notmatch 'request.Input.Dirty!=0') { throw 'Decision copy must not publish dirty negative topology' }
Write-Host 'PASS: virtual public copy/member contracts, bounded shadow queue and unvalidated policy fail-open.'
if ($hookSource -notmatch 'site.Rva==0x3C2E0\?site.Bytes.Length:site.Size' -or
    $hookSource -notmatch 'ValidateAttackBody\(context.Memory.Slice' -or
    $hookSource -notmatch 'Tuple.Create\(0x2F,18\),Tuple.Create\(0xF9,36\)' -or
    $hookSource -notmatch 'Unknown attack-body change') { throw 'Audited patched attack-body scanner contract changed' }
Write-Host 'PASS: exact bounded attack entry and whole-body patch whitelist; failed unpublished candidates only can roll back.'

$artifactSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/BridgeInputArtifact.cs'))
$componentSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/VirtualComponentControl.cs'))
if ($shadowSource -match 'nextCapture|request.Input.Revision==revision|request.Input.Identity==identity|third=input.Anchors' -or
    $shadowSource -notmatch 'PolicyValidAtDecision=policyValid' -or
    $shadowSource -notmatch 'request.Variant%3==1&&bridge.Id!=703' -or
    $shadowSource -notmatch 'if\(\+\+active.Variant<6\)' -or
    $shadowSource -notmatch 'sameCoordinates' -or $shadowSource -notmatch 'takeOwnership:true') { throw 'Historical input, exact coordinate reuse or six-control comparison regression' }
if ($componentSource -match 'SHCDESE|APIShared|Marshal|IntPtr' -or
    $artifactSource -match 'SHCDESE|FindNext|FindPath|Marshal|ThreadPool|Task.Run' -or
    $artifactSource -notmatch 'BlockLimit=65536' -or $artifactSource -notmatch 'count>=2' -or
    $artifactSource -notmatch 'BRGEND01' -or $artifactSource -notmatch 'File.Move\(active.Partial,active.Final\)' -or
    $traceSource -notmatch 'ArtifactPending\(ended\)' -or
    [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/BridgeDiagnostics.cs')) -notmatch 'PumpArtifacts\(\);Trace.Drain\(\)') { throw 'Copied-input control, bounded artifact writer or durable completion regression' }
Write-Host 'PASS: historical copied inputs, no guessed third endpoint, six controls, max two artifacts and one 64KiB render block.'

# Partial macro knowledge must never manufacture C or discard verified A/B witnesses.
if ($coreSource -notmatch 'if\(connection.ThirdEndpointUnknown\)uncertain=true' -or
    $shadowSource -match 'PathComponentC>0\) \{known=false' -or
    $shadowSource -notmatch 'private static bool SameContent' -or $shadowSource -notmatch 'ReferenceEquals\(content,b.content\)' -or
    $shadowSource -notmatch 'if\(stage.StartsWith\("group-formation"\)\)' -or $shadowSource -notmatch 'if\(!Promote\(request\)\)return' -or
    $shadowSource -notmatch 'preparations.Count>=8' -or $shadowSource -notmatch 'lastContent.Token:new object\(\)' -or
    $coreSource -notmatch 'Workspace capacity mismatch') { throw 'Partial endpoint, bounded content cache, deferred promotion or workspace contract regression' }
Write-Host 'PASS: independently known A/B endpoints, conservative C, exact content reuse and selected group promotion.'

# Reconstructed gate C uses copied coordinates and exact native class layouts.
if ($shadowSource -notmatch 'VirtualGateEndpoint.TryResolve' -or
    $shadowSource -match 'SpanIndex\(b.r_GatehouseId' -or $indexSource -match 'ParentId=b.r_GatehouseId|native-building-id' -or
    $indexSource -notmatch 'CollectFirstDistinctBuildingIds' -or $indexSource -notmatch 'native-ordered-footprint-coupling') { throw 'Native C reconstruction or spatial coupling regression' }
$buildingType=$installed.GetType('SHCDESE.Interop.GameBuilding',$true)
foreach ($spec in @(@('r_TilePositionXBegin',238,'UInt16'),@('r_TilePositionYBegin',240,'UInt16'),@('r_OccupyTileGridSize',248,'UInt32'))) {
    $field=$buildingType.GetField($spec[0]);if (!$field -or !$field.IsPublic -or $field.FieldType.Name -ne $spec[2] -or [Runtime.InteropServices.Marshal]::OffsetOf($buildingType,$spec[0]).ToInt32() -ne $spec[1]) { throw ('Coupling member mismatch: '+$spec[0]) }
}
$tileType=$installed.GetType('SHCDESE.API.GameTileManagerAPI',$true)
foreach ($spec in @(@('GetTileBuildingId',[UInt16],[int]),@('GetTileId',[int],[int],[int]),@('IsTileInsideMapBounds',[bool],[int],[int]),@('IsValidTileId',[bool],[int]))) {
    $args=@($spec | Select-Object -Skip 2);$method=$tileType.GetMethod($spec[0],[type[]]$args)
    if (!$method -or !$method.IsPublic -or $method.ReturnType -ne $spec[1]) { throw ('Coupling API mismatch: '+$spec[0]) }
}
Write-Host 'PASS: installed coupling views/signatures, copied class3/4 C endpoints and opaque connection-record field.'

# New planning/table preparations are pure, dormant copied-input kernels.
foreach ($name in @('VirtualBridgePlanning.cs','NativePathfindingTableCopy.cs','VirtualTopologyRebuild.cs','VirtualPlanningCaller.cs','VirtualRaisedPlanning.cs','VirtualPlanningBuildings.cs','CopiedPlanningBundle.cs','BridgePlanningImporter.cs','CopiedPlanningRegions.cs','CopiedPlanningReplay.cs','VirtualCandidateBuilder.cs','CopiedCandidateInputs.cs')) {
    $pure=[IO.File]::ReadAllText((Join-Path $PSScriptRoot ('src/'+$name)))
    if ($pure -match 'SHCDESE|APIShared|Marshal|IntPtr|DllImport|FindNext|FindPath|UnityEngine|ThreadPool|Task.Run') { throw ('Planning/table preparation is not pure: '+$name) }
}
foreach ($path in $sources | Where-Object { $_ -notmatch 'VirtualBridgePlanning.cs|NativePathfindingTableCopy.cs|VirtualPlanningCaller.cs|VirtualRaisedPlanning.cs|BridgePlanningCapture.cs|BridgePlanningImporter.cs|CopiedPlanningReplay.cs' }) {
    if ([IO.File]::ReadAllText($path) -match 'VirtualBridgePlanning\.|NativePathfindingTableCopy\.') { throw 'Offline preparations unexpectedly activated in a live callback' }
}
Write-Host 'PASS: dormant copied planning/permission kernels; no live integration, searches or new game-member accesses.'

$collector=[IO.File]::ReadAllText((Join-Path $PSScriptRoot "src/BridgePlanningCapture.cs"))
if ($collector -match "Marshal.Write|FindNext|FindPath|Game.*API|VirtualBridgePlanning\.") { throw "Passive planning capture contains an active access" }
Write-Host "PASS: hash-bound passive planning copy, no live planning execution or native writes."

# Additive consumer capture is confined to existing observers and one bounded bundle.
if ($collector -notmatch 'ObserveConsumerChild' -or $collector -notmatch 'parent!=consumerOp' -or $collector -notmatch 'consumer/pre' -or $collector -notmatch 'limit>10000' -or $collector -notmatch '4500\*0x688' -or $collector -notmatch '0x8574B90' -or $collector -match '0x88574B90') { throw 'Bounded consumer capture/address contract regression' }
$importer=[IO.File]::ReadAllText((Join-Path $PSScriptRoot "src/BridgePlanningImporter.cs"))
if ($importer -notmatch 'consumer-pre-post-binding' -or $importer -notmatch 'missing-versioned-consumer') { throw 'Consumer artifact identity/version validation missing' }
Write-Host 'PASS: bounded own-frame consumer capture, native unit/tribe capacities, additive schema2 validation.'

# Extended immutable input provenance; all paths remain bounded/passive.
foreach ($field in @('buildingUpdaterControls','buildingUpdaterSlots','packedValidity','combatClassMask','workQueueX','gameModeValues')) {
    if ($collector -notmatch $field -or $importer -notmatch $field) { throw ('Consumer v2 provenance/capacity check missing: '+$field) }
}
if ($importer -notmatch 'weight-mode-changed-during-call') { throw 'Weight mode stability contract missing' }
Write-Host 'PASS: consumer v2 records actual weight modes, full updater input and bounded work fields.'
foreach ($field in @('nativeWorkDirections64','nativeDirectionMasks8','nativeWorkBuildingClasses336')) {
    if ($collector -notmatch $field -or $importer -notmatch $field) { throw ('Own consumer native table provenance missing: '+$field) }
}
if ($collector -notmatch 'consumer-native-table-changed' -or $importer -notmatch 'consumer-native-table-changed') { throw 'Consumer native table pre/post stability missing' }
$candidateInputs=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/CopiedCandidateInputs.cs'))
if ($candidateInputs -notmatch 'missing-own-consumer-table' -or $candidateInputs -notmatch '!calculated.Complete' -or $candidateInputs -match 'ConstantBytes|CrusaderLibraryLoadContext|File.ReadAllBytes') { throw 'Copied candidate inputs may not substitute unrecorded tables' }
foreach ($path in $sources | Where-Object { $_ -notmatch 'VirtualCandidateBuilder.cs|CopiedCandidateInputs.cs' }) {
    if ([IO.File]::ReadAllText($path) -match 'new VirtualCandidateBuilder|CopiedCandidateInputs\.TryCreate') { throw 'Dormant candidate continuation unexpectedly activated in a live callback' }
}
Write-Host 'PASS: bounded own consumer table capture, optional legacy import and dormant copied candidate continuation.'

$definitionSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/BridgeNativeDefinition.cs'))
if ($definitionSource -notmatch 'site.Rva != 0xE49D0 && site.Rva != 0x111C00' -or [regex]::Matches($hookSource,'if \(!BridgeNativeDefinition.OwnsEntry\(').Count -ne 2) {throw 'External topology/ladder owners must be excluded from validation and detour preparation'}
if ($traceSource -notmatch 'effective-consumer-pre-post' -or $traceSource -notmatch 'selectorReturn=not-observed' -or $traceSource -notmatch 'unit->r_ControllableForPlayerId==evidence.Player' -or $traceSource -notmatch 'nativeResult.HasValue' -or $traceSource -match 'N00000569') {throw 'Effective consumer or shared topology contract regression'}
Write-Host 'PASS: thirty private detours, shared topology, conservative effective ladder consumer and UInt16 ownership.'

$routeText=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/BridgeRouteTrace.cs'))
$commandText=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/BridgeDecisionTrace.cs'))
if ($routeText -notmatch 'stored-route-definition-batch' -or $routeText -notmatch 'relevantPathCount==32' -or $routeText -notmatch 'physicalAtCapture=not-recorded' -or $commandText -notmatch 'SameCommandInputs' -or $commandText -notmatch 'commandPreRows.Count>=4096' -or $commandText -notmatch 'commandPreRows.Clear\(\)') { throw 'Bounded exact command/path transport contract missing' }
Write-Host 'PASS: bounded exact command Pre references and numeric route definitions; batch timing is not capture timing.'

$entryCapture = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/BridgePlanningCapture.cs'))
$entryImporter = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/BridgePlanningImporter.cs'))
foreach ($entryField in @('entryLeaderIds9','entryLeaderGlobals9','entryUpdateClasses11','entryMoveClasses11','entryConfig5','entryIdentity4','callerHeight','callerBaseHeight')) {
    if (!$entryCapture.Contains($entryField) -or !$entryImporter.Contains($entryField)) {throw ('Own military/height input or import validation missing: '+$entryField)}
}
if (!$entryCapture.Contains('checked(0x366C210+lord*0x5e4)') -or !$entryImporter.Contains('military-entry-binding') -or !$entryImporter.Contains('military-entry-config-lord')) {throw 'Own military configuration/frame contract missing'}
Write-Host 'PASS: own military entry tables/configuration and caller heights, additive legacy-safe import; full entry/formation replay remains unproven.'
