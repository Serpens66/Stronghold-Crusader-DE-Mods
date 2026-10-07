from pathlib import Path
root = Path.cwd()
def edit(name, old, new):
    p=root/name
    t=p.read_text(encoding='utf-8-sig')
    if old not in t: raise RuntimeError('missing '+name+' '+old[:60])
    t=t.replace(old,new)
    p.write_bytes(t.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))
edit('BugfixesAndQoL/BugfixesAndQoL.csproj','<Compile Include="src\\TemporaryGateRouteReporting.cs" />',
     '<Compile Include="src\\TemporaryGateRouteReporting.cs" /><Compile Include="..\\Shared\\TemporaryPackedRouteInspection.cs" />')
edit('Testmods/EnemyGatePathfindingTest/EnemyGatePathfindingTest.PolicyTests.csproj','<Compile Include="tests\\Program.cs" />',
     '<Compile Include="tests\\Program.cs" /><Compile Include="tests\\TemporaryGateAcceptanceTests.cs" /><Compile Include="src\\TemporaryGateAcceptanceAggregate.cs" /><Compile Include="..\\..\\Shared\\TemporaryPackedRouteInspection.cs" />')
edit('Testmods/EnemyGatePathfindingTest/tests/Program.cs','assertions += GateRoutePolicyTests.Run();',
     'assertions += GateRoutePolicyTests.Run();\n                assertions += TemporaryGateAcceptanceTests.Run();')
edit('_inspect/EnemyBridgeCompileTests/Program.cs','var compilation = CSharpCompilation.Create(',
     '''if (Assembly.LoadFrom(api).GetType("APIShared.TemporaryGateRouteAcceptanceBridge",false)==null)
        trees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"APIShared","src","TemporaryGateRouteAcceptanceBridge.cs"))));
    var compilation = CSharpCompilation.Create(''')
edit('BugfixesAndQoL/tests/FriendlyMoatMovement.Tests/Program.cs','using var output = new MemoryStream();',
     '''if (Assembly.LoadFrom(apiSharedPath).GetType("APIShared.TemporaryGateRouteAcceptanceBridge", false) == null)
    compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"APIShared","src","TemporaryGateRouteAcceptanceBridge.cs"))));
using var output = new MemoryStream();''')
edit('BugfixesAndQoL/tests/FriendlyMoatMovement.Tests/Program.cs','var check=CSharpCompilation.Create("FriendlyMoatMovementSourceContract",sources,',
     '''if (Assembly.LoadFrom(apiSharedPath).GetType("APIShared.TemporaryGateRouteAcceptanceBridge", false) == null)
        sources = sources.Concat(new[]{CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"APIShared","src","TemporaryGateRouteAcceptanceBridge.cs")))}).ToArray();
    sources = sources.Concat(new[]{CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"Shared","TemporaryPackedRouteInspection.cs")))}).ToArray();
    var check=CSharpCompilation.Create("FriendlyMoatMovementSourceContract",sources,''')
for name in ['APIShared/src/TemporaryGateRouteAcceptanceBridge.cs','Shared/TemporaryPackedRouteInspection.cs',
 'Shared/PathDecisionAggregate.cs','BugfixesAndQoL/src/TemporaryGateRouteReporting.cs','BugfixesAndQoL/src/AiRaidRetargetFixRuntime.cs',
 'BugfixesAndQoL/src/MovementPathPublication.cs','BugfixesAndQoL/src/MovementSearchContext.cs',
 'Testmods/EnemyGatePathfindingTest/src/TemporaryGateAcceptanceAggregate.cs','Testmods/EnemyGatePathfindingTest/src/TemporaryGateRouteAcceptance.cs',
 'Testmods/EnemyGatePathfindingTest/src/EnemyGatePathfindingRuntime.cs','Testmods/EnemyGatePathfindingTest/src/SamePclGateRouteRuntime.cs',
 'Testmods/EnemyGatePathfindingTest/src/AttackOrderCorrelationDiagnostics.cs','Testmods/EnemyGatePathfindingTest/tests/TemporaryGateAcceptanceTests.cs',
 '_inspect/EnemyGateBuildingContextAudit/temporary-acceptance-edits.py']:
    p=root/name; t=p.read_text(encoding='utf-8-sig').replace('\r\n','\n').replace('\n','\r\n'); p.write_bytes(t.encode('utf-8'))
    assert p.read_bytes().decode('utf-8') == t
