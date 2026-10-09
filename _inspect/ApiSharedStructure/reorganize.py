import json, pathlib, subprocess
root=pathlib.Path.cwd(); api=root/'APIShared'; audit=root/'_inspect/ApiSharedStructure'
tool=audit/'StructureTool/bin/Debug/net10.0/StructureTool.dll'
def write(path,text):
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_bytes(text.replace('\r\n','\n').replace('\n','\r\n').encode())
def run(*args): subprocess.run(['dotnet',str(tool),*map(str,args)],check=True)
moves={}
def move(old,new):
    source=api/'src'/old; target=api/'src'/new
    target.parent.mkdir(parents=True,exist_ok=True)
    source.rename(target); moves['src/'+old]='src/'+new
def group(area,directory,names):
    for name in names.split(): move(area+'/'+name+'.cs',area+'/'+directory+'/'+name+'.cs')
group('UnitCommands','Formation','FormationModel FormationOrderPacket FormationPresentation FormationPreviewMarkerModel FormationReleaseStateModel FormationRuntime LargeMoveTargetMarkerRenderer LargeMoveTargetOverflowModel NativeFormationSlots')
group('UnitCommands','Cursor','CursorConnectivity CursorRegionGraph GroundMovePreviewAuthorization')
move('UnitCommands/Internal/GroundMovePreviewEligibility.cs','UnitCommands/Cursor/GroundMovePreviewEligibility.cs')
group('UnitCommands','Moat','DirectMoatCommandScopes MoatCandidateField MoatModeFlagIntermediaryFactory MoatPlacement MoatPlacementSearch MoatWorkTargetSelection')
group('UnitCommands','Movement','FillWeightedRoutes MovementPathPublication MovementSearchContext TraversalProvider TraversalDispatch TemporaryGateRouteReporting WeightedMoatPublication WeightedMoatRoutePlanner WeightedGridSearchKernel UnitMovementContext NativeMovementRecovery NativeMovementCadenceResolver QueueNativeContract')
move('UnitCommands/Internal/TemporaryPackedRouteInspection.cs','UnitCommands/Movement/TemporaryPackedRouteInspection.cs')
group('UnitCommands','Runtime','UnitCommandPathAPI UnitCommandContracts MovementOptionsSnapshot ManualUnitCommands')
group('UnitCommands','Native','NativeDetourContracts PermanentCommandHooks')
group('UnitCommands','Attack','UnitCommandPathRuntime.LadderAttackFix ManualProbeRepairGuards')
group('UnitCommands','Integration','AssassinSelectionAdapters EnemyGatePolicyIntegration')
# Each moved declaration is preserved verbatim by Roslyn, including comments/attributes.
command=api/'src/UnitCommands/UnitCommandPathRuntime.cs'
mapping={}
def assign(dest,*ranges):
    for first,last in ranges:
        for index in range(first,last+1): mapping[str(index)]=str(api/'src/UnitCommands'/dest)
assign('Native/UnitCommandPathRuntime.NativeDelegates.cs',(1,23))
assign('Runtime/UnitCommandPathRuntime.Initialization.cs',(341,342))
assign('Native/UnitCommandPathRuntime.AttackHooks.cs',(343,346))
assign('Attack/UnitCommandPathRuntime.OrderEvents.cs',(347,348))
assign('Attack/UnitCommandPathRuntime.Tracking.cs',(349,356),(361,363),(380,384),(536,537),(540,540),(543,543))
assign('Movement/UnitCommandPathRuntime.QueueTracking.cs',(357,360),(562,563))
assign('Moat/UnitCommandPathRuntime.Tracking.cs',(364,366),(371,379),(544,544))
assign('Movement/UnitCommandPathRuntime.CadenceDiagnostics.cs',(367,370))
assign('Moat/UnitCommandPathRuntime.DiggerSelection.cs',(385,388))
assign('Movement/UnitCommandPathRuntime.Planning.cs',(389,390),(402,404),(558,558))
assign('Movement/UnitCommandPathRuntime.WeightedContext.cs',(391,401),(545,545))
assign('Attack/UnitCommandPathRuntime.ScopeDiagnostics.cs',(405,406))
assign('Movement/UnitCommandPathRuntime.RegionEligibility.cs',(407,413),(555,555))
assign('Attack/UnitCommandPathRuntime.ApproachFlood.cs',(414,417),(546,547))
assign('Attack/UnitCommandPathRuntime.BuildingCandidates.cs',(418,421),(428,429),(548,548))
assign('Attack/UnitCommandPathRuntime.BuildingFallback.cs',(422,426),(551,551))
assign('Attack/UnitCommandPathRuntime.RegionFallback.cs',(430,435))
assign('Attack/UnitCommandPathRuntime.ApproachPublication.cs',(436,446))
assign('Attack/UnitCommandPathRuntime.ApproachDiagnostics.cs',(447,452),(549,550),(552,552))
assign('Cursor/UnitCommandPathRuntime.MoatEligibility.cs',(453,457),(538,539),(564,565))
assign('Movement/UnitCommandPathRuntime.RequiredRoutes.cs',(458,462),(485,485),(541,542))
assign('Cursor/UnitCommandPathRuntime.Selection.cs',(463,470),(484,484),(568,568))
assign('Cursor/UnitCommandPathRuntime.TargetResolution.cs',(471,483))
assign('Movement/UnitCommandPathRuntime.ReachabilityMap.cs',(486,501))
assign('Runtime/UnitCommandPathRuntime.Diagnostics.cs',(502,525))
assign('Native/UnitCommandPathRuntime.NativeContracts.cs',(526,535))
assign('Runtime/UnitCommandPathRuntime.CommandScopes.cs',(553,554),(556,557),(559,561),(566,567))
# Keep every field initializer in original order in the state part.
members=json.loads((audit/'command-members.json').read_text(encoding='utf-8-sig'))
for member in members:
    if member['Kind']=='FieldDeclaration': mapping.pop(str(member['Index']),None)
for member in members:
    mapping.setdefault(str(member['Index']),str(api/'src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs'))
write(audit/'command-split.json',json.dumps(mapping,indent=2))
run('split',command,'UnitCommandPathRuntime',audit/'command-split.json'); command.unlink()
# Place each public service next to its implementation, grouped by its actual feature.
group('Buildings','Repair','BuildingRepairContracts BuildingRepairCapability')
group('Buildings','Gatehouse','GatehouseTimingCapability GatehousePermanentRuntimeState GatehouseDrawbridgeCoupling GatehouseDistanceOriginCapability GatehouseAutomationNativeState')
group('Presentation','UnitHud','UnitHudContracts UnitHudPresentationCapability')
group('Presentation','BriefingGold','BriefingGoldContracts BriefingGoldPresentationCapability')
group('ModSettings','Presets','DynamicPresetSettings LobbyModSettingsPresetRegistration ModSettingsPresetDocuments PresetLobbyModSettingsViewModel PresetLobbyModSettingsViewModel.Persistence PresetLobbyModSettingsViewModel.Sources')
group('ModSettings','Lobby','PerPlayerLobbySettings')
group('ModSettings','UI','ToolTipPresentation ModSettingsSearch')
move('ModSettings/Internal/ToolTipPresentation.cs','ModSettings/UI/Internal/ToolTipPresentation.cs')
group('Pathfinding','Assassin','AssassinAttackNativeContract AssassinAttackControlAPI AssassinEndpointEmitter AssassinGateTransitionPolicy AssassinPathAPI AssassinRouteHandoff AssassinPathNativeDefinition')
group('Pathfinding','GateRoutes','TemporaryGateRouteAcceptanceBridge EnemyGatePathPolicyBridge EnemyBridgeDiagnosticBridge')
group('Pathfinding','Moat','ElevatedMoatAiState')
# General native mechanisms are kept distinct from feature-specific hooks.
native=api/'src/Core/NativeInfrastructure.cs'
nativeMap={name:str(api/'src/Core/Internal/Native'/file) for names,file in [
 ('NativeReservationMode NativeInterval NativeOwnershipRegistry','NativeOwnershipRegistry.cs'),
 ('ProcessNativeMemory','ProcessNativeMemory.cs'),('INativeMemory','INativeMemory.cs'),('NativeResolutionException','NativeResolutionException.cs'),
 ('NativeSection NativePeImage','NativePeImage.cs'),('NativeApiLog','NativeApiLog.cs')] for name in names.split()}
write(audit/'native-split.json',json.dumps(nativeMap,indent=2)); run('namespace',native,'unused',audit/'native-split.json'); native.unlink()
move('Core/UniquePatternSearch.cs','Core/Internal/Native/UniquePatternSearch.cs')
move('Core/Internal/NativePatternResolver.cs','Core/Internal/Native/NativePatternResolver.cs')
# Separate the HUD's selection policy before extracting parts of its service.
hud=api/'src/Presentation/UnitHud/UnitHudPresentationCapability.cs'
hudMap={'UnitHudSelectionPolicy':str(hud.parent/'UnitHudSelectionPolicy.cs')}
write(audit/'hud-types.json',json.dumps(hudMap)); run('namespace',hud,'unused',audit/'hud-types.json')
hudMembers=json.loads((audit/'hud-members.json').read_text(encoding='utf-8-sig'))
hudSplit={str(m['Index']):str(hud.parent/'UnitHudPresentationService.cs') for m in hudMembers}
def hudassign(file,*ranges):
    for first,last in ranges:
        for index in range(first,last+1):
            if hudMembers[index]['Kind']!='FieldDeclaration': hudSplit[str(index)]=str(hud.parent/('UnitHudPresentationService.'+file+'.cs'))
hudassign('Hooks',(17,24),(117,120),(135,135),(143,145),(147,151),(158,158),(218,219))
hudassign('Registrations',(121,134),(220,220),(223,227))
hudassign('Categories',(136,142),(156,157),(161,166),(188,208),(213,213),(228,229))
hudassign('Recruitment',(152,155),(167,176))
hudassign('DetailsAndArmy',(177,187),(214,214),(221,222))
hudassign('Images',(159,160),(209,212),(215,216))
write(audit/'hud-split.json',json.dumps(hudSplit,indent=2)); run('split',hud,'UnitHudPresentationService',audit/'hud-split.json'); hud.unlink()
# Update project/test/tool/document references together; consumers still use namespaces unchanged.
eligible={'.csproj','.props','.targets','.ps1','.cs','.md','.json','.py','.bat'}
for base in [api/'tests',api/'tools',api/'docs',root/'Shared/Tools',root/'Testmods',root/'Helpers']:
    for path in base.rglob('*'):
        if not path.is_file() or path.suffix not in eligible or any(p in {'bin','obj','BepInEx','.git'} for p in path.parts): continue
        text=path.read_text(encoding='utf-8-sig'); updated=text
        for old,new in moves.items():
            # References usually use / or backslash paths; preserve the existing style.
            updated=updated.replace(old,new).replace(old.replace('/','\\'),new.replace('/','\\'))
        if updated!=text: write(path,updated)
write(audit/'moves.json',json.dumps(moves,indent=2))
