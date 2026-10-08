from pathlib import Path
exec(Path('_inspect/FormationRows/implement.py').read_text().split("p='APIShared/src/UnitCommands/FormationModel.cs'")[0])
p='APIShared/src/UnitCommands/FormationModel.cs';s=read(p).replace('Begin(int unitCount, RangedPlacementMode placementMode)','Begin(int unitCount, RangedPlacementMode placementMode, int rows)').replace('AddInt32(ref hash, unitCount);','AddInt32(ref hash, unitCount);\n            AddInt32(ref hash, rows);');write(p,s)
p='APIShared/src/UnitCommands/FormationRuntime.cs';s=read(p)
s=s.replace('FormationModel.NormalizePlacementMode(packet.PlacementMode));\n                    if (actualPlanHash','FormationModel.NormalizePlacementMode(packet.PlacementMode), packet.Rows);\n                    if (actualPlanHash')
s=s.replace('units, destinations, state.PlacementMode);','units, destinations, state.PlacementMode, state.Rows);')
start=s.index('        private static ulong ComputePlanHash(');end=s.index('        private ',start+20)
chunk=s[start:end].replace('RangedPlacementMode placementMode)','RangedPlacementMode placementMode, int rows)').replace('FormationPlanHash.Begin(count, placementMode)','FormationPlanHash.Begin(count, placementMode, rows)');s=s[:start]+chunk+s[end:];write(p,s)
p='BugfixesAndQoL/tests/Formations.Tests/Program.cs';s=read(p)
s=re.sub(r'FormationPlanHash.Begin\(2, RangedPlacementMode\.(Rear|Center)\)',r'FormationPlanHash.Begin(2, RangedPlacementMode.\1, 2)',s)
s=s.replace('    private static void TestPlanHash()\n    {','    private static void TestPlanHash()\n    {\n        Check(FormationPlanHash.Begin(31, RangedPlacementMode.Off, 16) !=\n              FormationPlanHash.Begin(31, RangedPlacementMode.Off, 21), "row metadata contributes to the plan hash");')
s=s.replace('int width = FormationModel.ResolveAutomaticWidth(kind, 31);','int rows = FormationModel.ResolveAutomaticRows(kind, 31);').replace('kind, 31, width,','kind, 31, rows,')
idx=s.index('        TestSelectionMigration();',s.index('    private static void TestSourceSafetyContracts()'))
s=s[:idx]+'''        string wheel = ExtractMethodBody(runtime, "private void UpdateGesture(");
        Check(wheel.Contains("state.Geometry.ApplyWheel(wheel, frame)") &&
              !wheel.Contains("state.Density =") && wheel.Contains("lastWheelFrame != frame"),
            "runtime samples wheel once without changing density");
        string direction = ExtractMethodBody(runtime, "private static void ResolveDirectionAndWidth(");
        Check(!direction.Contains("dragDistance") && direction.Contains("state.Rows"),
            "mouse distance cannot resize the formation");
        string reachable = ExtractMethodBody(runtime, "private List<NativeDestination> CaptureReachableCandidates(");
        Check(reachable.Contains("pending.Count != 0") && reachable.Contains("pending.Remove(current.TileId)") &&
              reachable.Contains("components[tile] == component") && reachable.Contains("0x10000100"),
            "reachability search covers desired extent and preserves component/Assassin gates");
        Check(runtime.Contains("width does not match rows") && runtime.Contains("packet.Rows == 0"),
            "malformed row/width packets are rejected");
        string menu = File.ReadAllText(Path.Combine(main, "src", "FormationMenuViewModel.cs"));
        string xaml = File.ReadAllText(Path.Combine(main, "Patches", "Assets", "GUI", "XAMLResources", "HUD_Troops.xaml"));
        foreach (string kind in new[] { "Vanilla", "Block", "Line", "Column", "Wedge", "Circle" })
            Check(menu.Contains("public bool Is" + kind) && menu.Contains("OnChanged(nameof(Is" + kind + "))") &&
                  CountOccurrences(xaml, "{StaticResource BugfixesFormationIcon" + kind + "}") == 2 &&
                  xaml.Contains("{Binding Is" + kind + ","), "selected icon template and Config notification " + kind);
'''+s[idx:];write(p,s)
