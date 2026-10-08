exec((__import__('pathlib').Path(__file__).parent/'integrate.py').read_text().split("for name in ['FormationModel'")[0])
p=shared/'FormationModel.cs';s=read(p)
start=s.index('    internal readonly struct FormationDefaultsMigration');end=s.index('    internal static class FormationModel',start)
s=s[:start]+s[end:];write(p,s)
p=root/'BugfixesAndQoL/tests/Formations.Tests/Program.cs';s=read(p)
s=s.replace('            TestDefaultsMigration();\n','');s=remove_method(s,'private static void TestDefaultsMigration()');write(p,s)
p=root/'BugfixesAndQoL/tests/ExtendedShiftCommandQueue.Tests/Program.cs';s=read(p).replace('largeMoveRenderer.Contains("BugfixesHookInfrastructure.AddContextHook(")','largeMoveRenderer.Contains("candidate.AddContextHook(")');write(p,s)
p=root/'BugfixesAndQoL/tests/FriendlyMoatMovement.Tests/FillFormationTests.cs';s=read(p)
start=s.index('                activeMoveCommand.HasFormationSpacing=true;');end=s.index('                tileFlags[1060]|=',start)
s=s[:start]+'''                activeMoveCommand=new MoveCommandScope{TribeId=1,TargetX=60,TargetY=10};
'''+s[end:];write(p,s)
p=root/'BugfixesAndQoL/tests/FriendlyMoatMovement.Tests/RuntimeHarness.cs';s=read(p)
start=s.index('    internal readonly struct MoveFormationDestination');end=s.index('    internal enum AliveState',start)
s=s[:start]+s[end:]
s=s.replace('            public bool HasFormationSpacing;\n            public int FormationSpacing = MoveFormationSpacingPolicy.Default;\n','').replace('  internal int MoveFormationSpacing=MoveFormationSpacingPolicy.Default;\n','')
write(p,s)
# All existing non-German translations used the same English fallback for this entry.
english='Choose formation, density and role placement in the troop HUD. Hold the configured Move mouse button over open ground and drag to set width and direction; use the mouse wheel for density 1-4. Release issues one synchronized Move. Requires at least two selected units. Shift waypoints retain their existing behavior.'
german='Waehle Formation, Dichte und Rollenplatzierung im Truppen-HUD. Halte die konfigurierte Move-Maustaste ueber freiem Boden gedrueckt und ziehe fuer Breite und Richtung; das Mausrad aendert die Dichte 1-4. Loslassen erteilt genau einen synchronisierten Move. Erfordert mindestens zwei ausgewaehlte Einheiten. Shift-Wegpunkte behalten ihr bisheriges Verhalten.'
for p in (root/'BugfixesAndQoL/Locales').glob('*.txt'):
    s=read(p);s=re.sub(r'^BugfixesAndQoL.EnableMoveFormationEnhancementsHelp=.*$', 'BugfixesAndQoL.EnableMoveFormationEnhancementsHelp='+(german if p.name=='de-DE.txt' else english), s,flags=re.M);write(p,s)
write(root/'_inspect/FormationIntegration/changed-finish.txt','\n'.join(changed)+'\n')
