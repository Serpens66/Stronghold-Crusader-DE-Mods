exec((__import__('pathlib').Path(__file__).parent/'integrate.py').read_text().split("for name in ['FormationModel'")[0])

p=shared/'UnitCommandPathRuntime.cs';s=read(p)
s=s.replace('                MovementOptionsSnapshot currentOptions,\n                bool hasFormationSpacing = false,\n                int formationSpacing = MoveFormationSpacingPolicy.Default)','                MovementOptionsSnapshot currentOptions)')
write(p,s)
p=root/'APIShared/APIShared.csproj';s=read(p)
s=s.replace('    <Compile Include="..\\Shared\\DebugLogHelper.cs">','    <Compile Include="..\\Shared\\UnityMainThreadDispatch.cs"><Link>Shared\\UnityMainThreadDispatch.cs</Link></Compile>\n    <Compile Include="..\\Shared\\GroundMovePreviewEligibility.cs"><Link>Shared\\GroundMovePreviewEligibility.cs</Link></Compile>\n    <Compile Include="..\\Shared\\DebugLogHelper.cs">')
write(p,s)

# HUD patches share the main mod's existing operation list instead of replacing files.
for p in (old/'Patches').rglob('*.xaml'):
    target=root/'BugfixesAndQoL/Patches'/p.relative_to(old/'Patches')
    s=read(p).replace('FormationTest','BugfixesAndQoLFormation')
    if target.exists():
        operations=s[s.index('<Operation'):s.rindex('</Patch>')]
        current=read(target)
        s=replace(current,'</Patch>',operations+'</Patch>')
    write(target,s)

tests=root/'BugfixesAndQoL/tests/Formations.Tests'
s=read(old/'tests/Program.cs').replace('using FormationTest;', 'using APIShared.UnitCommands;\nusing BugfixesAndQoL;').replace('FormationTest','Formations')
start=s.index('    private static void TestSourceSafetyContracts()');end=s.index('    private static string FindProjectRoot()',start)
s=s[:start]+'''    private static void TestSourceSafetyContracts()
    {
        string main = FindProjectRoot();
        string shared = Path.Combine(Directory.GetParent(main).FullName, "APIShared", "src", "UnitCommands");
        string runtime = File.ReadAllText(Path.Combine(shared, "FormationRuntime.cs"));
        string slots = File.ReadAllText(Path.Combine(shared, "NativeFormationSlots.cs"));
        string common = File.ReadAllText(Path.Combine(shared, "MoatPlacement.cs"));
        string markers = File.ReadAllText(Path.Combine(shared, "LargeMoveTargetMarkerRenderer.cs"));
        string feature = File.ReadAllText(Path.Combine(main, "src", "FormationFeature.cs"));
        string source = runtime + slots + common + markers + feature;
        foreach (string forbidden in new[] {
            "System.Text.Json", "Newtonsoft.Json", "JavaScriptSerializer", "JsonUtility",
            "OnDestroy(", "OnDisable(", "OnApplicationQuit(", "StartCoroutine(",
            "MainViewModel.instance", "CodePatch.Write(", "VirtualProtect(" })
            Check(!source.Contains(forbidden), "forbidden runtime pattern: " + forbidden);
        Check(!runtime.Contains("AddDetour(") && !runtime.Contains("AddContextHook("),
            "formation runtime cannot install competing native hooks");
        Check(slots.Contains("formationRuntime.TryChooseStandardFormationSlot") &&
              slots.Contains("formationRuntime.TryChooseAssassinFormationSlot") &&
              common.Contains("formationRuntime.CommonGroupMoveHook"),
            "all placement providers use shared native dispatch");
        Check(runtime.Contains("settings.EnableMod && settings.EnableMoveFormationEnhancements") &&
              runtime.Contains("if (!Enabled)"), "checkbox and runtime fault gate input and received commands");
        Check(runtime.Contains("TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable") &&
              runtime.Contains("UnitR3EventHooks.OnUnitMoveHere.Observable"), "existing Extender order and terminal events used");
        Check(runtime.Contains("InputR3EventHooks.OnKeyDown.Observable.Subscribe(OnKeyDown)") &&
              runtime.Contains("InputR3EventHooks.OnKey.Observable.Subscribe(OnKeyHeld)") &&
              runtime.Contains("InputR3EventHooks.OnKeyUp.Observable.Subscribe(OnKeyUp)"), "input uses persistent events");
        Check(runtime.Contains("FORMATION_RUNTIME_AFTER_STARTUP_CLEANUP") &&
              feature.Contains("private static FormationRuntime runtime;"), "runtime survives startup component cleanup");
        string engine = ExtractMethodBody(runtime, "private int EngineRunHook(");
        Check(CountOccurrences(engine, "TryDispatch(packet,") == 1 &&
              engine.Contains("RunOriginalAfterReleaseConsumed(") && !engine.Contains("UpdateGesture("),
            "release has a single dispatch and no render/Unity polling");
        string consume = ExtractMethodBody(runtime, "private int RunOriginalAfterReleaseConsumed(");
        Check(consume.Contains("originalState.ConsumeCommandRelease") && !consume.Contains("originalState.Restore"),
            "accepted release is permanently consumed");
        Check(runtime.Contains("ProtocolVersion = 5") && runtime.Contains("actualPlanHash != packet.PlanHash") &&
              runtime.Contains("result.Sort((left, right) => left.UnitId.CompareTo(right.UnitId))"),
            "deterministic unit order and synchronized plan validation retained");
        Check(runtime.Contains("APIShared.UnitAccess.IsReallyAlive(unit)") && runtime.Contains("unit->r_GlobalId == globalId"),
            "native death marker and identity validation retained");
        Check(runtime.Contains("IsShiftHeld()") && runtime.Contains("EvaluateFixedGroundTarget(state.Target)"),
            "Shift, objects, selection changes and invalid ground retain Vanilla");
        Check(runtime.Contains("state.Authorization.Observe(groundFeedbackReader.Read())") &&
              markers.Contains("previewCheckedThisPass"), "preview authorization is shared and evaluated per native pass");
        Check(markers.Contains("ExpectedVisibleTileDisplacedBytes = 17") &&
              markers.Contains("Placement = OverwrittenInstructionPlacement.AfterCallback"), "shared marker displacement contract");
        Check(!Directory.GetFiles(Path.Combine(main, "src"), "MoveFormationDrag*.cs").Any(), "obsolete drag implementation removed");
        Check(!File.Exists(Path.Combine(shared, "MoveFormationSpacingPolicy.cs")), "obsolete spacing implementation removed");
        Check(feature.IndexOf("runtime = new FormationRuntime", StringComparison.Ordinal) <
              feature.IndexOf("runtime.Initialize();", StringComparison.Ordinal), "runtime rooted before hook publication");
        TestSelectionMigration();
    }

    private static void TestSelectionMigration()
    {
        var empty = FormationSelectionMigration.ReadFormationSection("");
        var legacy = FormationSelectionMigration.ReadFormationSection(
            "[Other]\\r\\nDensity=4\\r\\n[Formation]\\r\\nKind=Wedge\\r\\nDensity=3\\r\\nRangedPlacement=Rear\\r\\nShowRoleMarkers=false\\r\\n");
        Check(FormationSelectionMigration.ResolveEnum(empty, legacy, "Kind", FormationKind.Block) == FormationKind.Wedge &&
              FormationSelectionMigration.ResolveDensity(empty, legacy) == 3 &&
              FormationSelectionMigration.ResolveEnum(empty, legacy, "RangedPlacement", RangedPlacementMode.Off) == RangedPlacementMode.Rear &&
              !FormationSelectionMigration.ResolveRoleMarkers(empty, legacy), "missing selections migrate from legacy config");
        var existing = FormationSelectionMigration.ReadFormationSection("[Formation]\\nKind=Circle\\nDensity=1\\nRangedPlacement=Center\\nShowRoleMarkers=true");
        Check(FormationSelectionMigration.ResolveEnum(existing, legacy, "Kind", FormationKind.Block) == FormationKind.Block &&
              FormationSelectionMigration.ResolveDensity(existing, legacy) == 2 &&
              FormationSelectionMigration.ResolveRoleMarkers(existing, legacy), "existing main entries are never overridden by migration defaults");
        var invalid = FormationSelectionMigration.ReadFormationSection("[Formation]\\nKind=255\\nDensity=9\\nRangedPlacement=garbage\\nShowRoleMarkers=invalid");
        Check(FormationSelectionMigration.ResolveEnum(empty, invalid, "Kind", FormationKind.Block) == FormationKind.Block &&
              FormationSelectionMigration.ResolveDensity(empty, invalid) == 2 &&
              FormationSelectionMigration.ResolveEnum(empty, invalid, "RangedPlacement", RangedPlacementMode.Off) == RangedPlacementMode.Off &&
              FormationSelectionMigration.ResolveRoleMarkers(empty, invalid), "invalid legacy values use testmod defaults");
    }

'''+s[end:]
s=s.replace('"Formations.csproj"', '"BugfixesAndQoL.csproj"')
# The permanent consumption assertion uses the actual model method's name.
s=s.replace('originalState.ConsumeCommandRelease','originalState.ConsumeRelease')
write(tests/'Program.cs',s)
write(tests/'NativeDetourEntryContract.cs',read(old/'src/NativeDetourEntryContract.cs').replace('namespace FormationTest','namespace APIShared.UnitCommands'))
s=read(old/'tests/FormationTest.Tests.csproj').replace('FormationTest.Tests','Formations.Tests')
s=s.replace('..\\src\\','..\\..\\..\\APIShared\\src\\UnitCommands\\')
s=s.replace('    <Compile Include="..\\..\\..\\APIShared\\src\\UnitCommands\\NativeDetourEntryContract.cs"><Link>NativeDetourEntryContract.cs</Link></Compile>',
'''    <Compile Include="NativeDetourEntryContract.cs" />
    <Compile Include="..\\..\\src\\FormationSelectionMigration.cs"><Link>FormationSelectionMigration.cs</Link></Compile>''')
write(tests/'Formations.Tests.csproj',s)

# Keep queue tests unrelated to the retired density feature.
q=root/'BugfixesAndQoL/tests/ExtendedShiftCommandQueue.Tests'
p=q/'Program.cs';s=read(p)
for name in ['CheckDeferredFormationChoreExecution','CheckMoveFormationSpacing','CheckMoveFormationPlanner','CheckMoveFormationGesture']:
    s=s.replace(f'        {name}();\n','')
    s=remove_method(s,f'private static void {name}()')
start=s.index('        foreach (string mode in new[]',s.index('private static void CheckChoreMarkers()'));end=s.index('        int[] producerMoveTypes',start)
s=s[:start]+s[end:]
start=s.index('        foreach (int moveType in producerMoveTypes)\n        foreach (int spacing');end=s.index('        Check(!QueueNativeContract.TryMarkMoveTypeForQueue(2',start)
s=s[:start]+s[end:]
start=s.index('    private static void CheckMigrationSourceContracts()');end=s.index('    private static string FindWorkspace()',start)
oldmethod=s[start:end]
# Extract the queue/marker checks that do not concern the replaced drag implementation.
keep=[]
pos=0
while True:
    pos=oldmethod.find('        Check(',pos)
    if pos<0: break
    tail=oldmethod.find(';\n',pos)+2
    statement=oldmethod[pos:tail]
    pos=tail
    if any(x in statement for x in ['moveFormation','MoveFormation','nativeFormationSlots','NativeFormationSlots','FORMATION_SPACING']): continue
    keep.append(statement)
newmethod='''    private static void CheckMigrationSourceContracts()
    {
        string workspace = FindWorkspace();
        string bugfixesPlugin = Read(workspace, "BugfixesAndQoL", "src", "BugfixesAndQoLPlugin.cs");
        string bugfixesMinimum = ReadManifestMinimum(workspace, "BugfixesAndQoL");
        string queueRuntime = Read(workspace, "BugfixesAndQoL", "src", "ExtendedShiftCommandQueueRuntime.cs");
        string largeMoveRuntime = Read(workspace, "BugfixesAndQoL", "src", "LargeMoveTargetMarkerRuntime.cs");
        string largeMoveRenderer = Read(workspace, "APIShared", "src", "UnitCommands", "LargeMoveTargetMarkerRenderer.cs");
        string sharedProject = Read(workspace, "APIShared", "APIShared.csproj");
        string viewModel = Read(workspace, "BugfixesAndQoL", "src", "BugfixesAndQoLViewModel.cs");
        string settingsXaml = Read(workspace, "BugfixesAndQoL", "Override", "ScriptExtenderUI", "BugfixesAndQoLSettings.xaml");
        string bugfixesRuntime = string.Join("\\n", Directory.GetFiles(Path.Combine(workspace, "BugfixesAndQoL", "src"), "*.cs").Select(File.ReadAllText));
        string bugfixesProject = Read(workspace, "BugfixesAndQoL", "BugfixesAndQoL.csproj");
'''+''.join(keep)+'''        Check(!queueRuntime.Contains("MoveFormationCommandContext") && !queueRuntime.Contains("ShouldMarkOutgoingFormationOrder"),
            "queue no longer transports obsolete density bits");
    }

'''
s=s[:start]+newmethod+s[end:];write(p,s)
p=q/'ExtendedShiftCommandQueue.Tests.csproj';s=read(p)
s=s.replace('..\\..\\src\\GroundMovePreviewAuthorization.cs','..\\..\\..\\APIShared\\src\\UnitCommands\\GroundMovePreviewAuthorization.cs').replace('..\\..\\src\\LargeMoveTargetOverflowModel.cs','..\\..\\..\\APIShared\\src\\UnitCommands\\LargeMoveTargetOverflowModel.cs')
s='\n'.join(line for line in s.split('\n') if not any(n in line for n in ['MoveFormationSpacingPolicy.cs','MoveFormationDragGesture.cs','MoveFormationPreviewPlanner.cs']))
write(p,s)
(q/'MoveFormationPlannerStubs.cs').unlink()

# Formations regression harness runs as part of the approved main-mod build driver.
p=root/'BugfixesAndQoL/build.bat';s=read(p)
needle='"%MSBUILD%" tests\\WaterboyTargetReservation.Tests\\WaterboyTargetReservation.Tests.csproj'
index=s.index(needle)
s=s[:index]+'''"%MSBUILD%" tests\\Formations.Tests\\Formations.Tests.csproj /p:Configuration=Release /p:ExtenderDir="%EXTENDER_DIR%"
if errorlevel 1 goto build_failed_popd
"%PROJECT_DIR%tests\\Formations.Tests\\bin\\Formations.Tests.exe"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
'''+s[index:];write(p,s)
# Include native/interop coverage in both drivers before compilation.
for mod in ['APIShared','BugfixesAndQoL']:
    p=root/mod/'build.bat';s=read(p)
    s=s.replace('@echo off','@echo off\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\\_inspect\\FormationIntegration\\Verify-Interop.ps1"\nif errorlevel 1 exit /b 1',1)
    write(p,s)
write(root/'_inspect/FormationIntegration/changed-tests.txt','\n'.join(changed)+'\n')
