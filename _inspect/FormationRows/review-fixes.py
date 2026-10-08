from pathlib import Path
exec(Path('_inspect/FormationRows/implement.py').read_text().split("p='APIShared/src/UnitCommands/FormationModel.cs'")[0])
p='BugfixesAndQoL/tests/Formations.Tests/Program.cs';s=read(p)
baseline=(root/'_inspect/FormationRows/before'/p).read_text(encoding='utf-8-sig')
# Update positional geometry fixtures without replacing a coincidentally equal unit count.
bs=baseline.index('    private static void TestShapesAndDensity()');be=baseline.index('    private static void TestPreviewMarkerNormalization()',bs)
chunk=baseline[bs:be]
chunk=re.sub(r'(FormationModel.BuildRelativeSlots\(\s*FormationKind\.(?:Block|Line|Column), )(\d+)(, )(\d+)(,)',lambda m:m[1]+m[2]+m[3]+str((int(m[2])+int(m[4])-1)//int(m[4]))+m[5],chunk)
chunk=chunk.replace('FormationKind.Column, alreadyOrdered.Count, 1,','FormationKind.Column, alreadyOrdered.Count, alreadyOrdered.Count,').replace('FormationKind.Column, tiny.Count, 1,','FormationKind.Column, tiny.Count, tiny.Count,')
chunk=chunk.replace('int vanillaDragged = FormationModel.ResolveDraggedWidth(\n            FormationKind.Vanilla, 10, 40);','int vanillaDragged = FormationModel.ResolveWidthForRows(FormationKind.Vanilla, 40, 1);')
chunk=chunk.replace('FormationModel.ResolveDraggedWidth(FormationKind.Block, 1, 20) == 4','new FormationGestureState(FormationKind.Block, 20, 0).Rows == 5').replace('short drag retains automatic Block depth','gesture starts with automatic Block depth')
start=s.index('    private static void TestShapesAndDensity()');end=s.index('    private static void TestPreviewMarkerNormalization()',start)
s=s[:start]+chunk+s[end:];write(p,s)
p='APIShared/src/UnitCommands/FormationModel.cs';s=read(p).replace('case FormationKind.Circle:\n                    return ResolveCircleDiameter(count);','case FormationKind.Circle:\n                    return Math.Min(count, ResolveCircleDiameter(count));');write(p,s)
p='APIShared/src/UnitCommands/FormationRuntime.cs';s=read(p)
for name,typ in [('Kind','FormationKind'),('Density','int'),('PlacementMode','RangedPlacementMode')]:
    s=s.replace(f'internal {typ} {name} {{ get; set; }}',f'internal {typ} {name} {{ get; }}')
write(p,s)
# Give templates a concrete content value so each presenter instantiates its glyph.
p='BugfixesAndQoL/Patches/Assets/GUI/XAMLResources/HUD_Troops.xaml';s=read(p)
s=re.sub(r'<ContentControl(?=[^>]*BugfixesFormationIcon)', '<ContentControl Content="Formation"',s);write(p,s)
