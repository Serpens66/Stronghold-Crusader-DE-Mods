import json,pathlib,subprocess
root=pathlib.Path.cwd(); audit=root/'_inspect/ApiSharedStructure'; tool=audit/'StructureTool/bin/Debug/net10.0/StructureTool.dll'
def split(file,classname,inventory,parts):
    members=json.loads((audit/inventory).read_text(encoding='utf-8-sig'))
    mapping={str(m['Index']):str(file.parent/(classname+'.State.cs')) for m in members}
    for name,ranges in parts.items():
        for first,last in ranges:
            for index in range(first,last+1):
                if members[index]['Kind']!='FieldDeclaration': mapping[str(index)]=str(file.parent/name)
    config=audit/(inventory+'.split.json');config.write_bytes(json.dumps(mapping,indent=2).replace('\n','\r\n').encode())
    subprocess.run(['dotnet',str(tool),'split',str(file),classname,str(config)],check=True);file.unlink()
split(root/'APIShared/src/UnitCommands/Formation/FormationRuntime.cs','FormationRuntime','formation-members.json',{
 'FormationRuntime.Initialization.cs':[(80,81)],
 'FormationRuntime.NativeDelegates.cs':[(0,5)],
 'FormationRuntime.Input.cs':[(82,84),(89,90),(142,147)],
 'FormationRuntime.EngineAndCameraHooks.cs':[(85,88),(148,149),(161,161)],
 'FormationRuntime.NetworkCommands.cs':[(91,95),(150,150)],
 'FormationRuntime.OrderEvents.cs':[(96,97),(162,163),(166,168)],
 'FormationRuntime.SlotHooks.cs':[(98,106),(113,113),(165,165)],
 'FormationRuntime.Diagnostics.cs':[(107,110),(136,136)],
 'FormationRuntime.Destinations.cs':[(111,112),(114,125),(171,172)],
 'FormationRuntime.Preview.cs':[(126,128),(138,138),(159,160)],
 'FormationRuntime.SelectionAndTargets.cs':[(129,137),(141,141),(169,170)],
 'FormationRuntime.Activation.cs':[(139,140),(151,154)],
 'FormationRuntime.NativeContracts.cs':[(155,158),(164,164)],
})
split(root/'APIShared/src/UnitCommands/Moat/MoatWorkTargetSelection.cs','UnitCommandPathRuntime','moat-target-members.json',{
 'UnitCommandPathRuntime.WorkTargetHooks.cs':[(0,2),(45,46),(71,71)],
 'UnitCommandPathRuntime.WorkTargetSelection.cs':[(47,53),(73,74)],
 'UnitCommandPathRuntime.WorkTargetFillApproach.cs':[(54,55),(57,58),(75,75)],
 'UnitCommandPathRuntime.WorkTargetReachability.cs':[(59,63)],
 'UnitCommandPathRuntime.WorkTargetDiagnostics.cs':[(64,66)],
 'UnitCommandPathRuntime.WorkTargetValidation.cs':[(56,56),(67,70),(72,72),(76,76)],
})
# Avoid colliding the moat work state part with the main runtime state.
file=root/'APIShared/src/UnitCommands/Moat/UnitCommandPathRuntime.State.cs'
file.rename(file.with_name('UnitCommandPathRuntime.WorkTargetState.cs'))
