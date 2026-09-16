from pathlib import Path
import re
root=Path.cwd()
def write(p,s):p.write_bytes(s.replace('\r\n','\n').replace('\n','\r\n').encode())
def replace_method(s,name,body):
    start=s.index('    private static void '+name+'(')
    end=s.find('\n    private static ',start+10)
    if end<0:end=s.index('\n}\n',start)
    return s[:start]+body+'\n'+s[end:]
p=root/'_inspect/HostClientPresetTests/Program.cs';s=p.read_text(encoding='utf-8-sig')
s=replace_method(s,'TestGameModeHelper', '''    private static void TestGameModeHelper()
    {
        APIShared.MissionLifecycleService.Snapshot = default;
        Check(GameModeHelper.Capture().Kind == GameModeKind.Unknown, "No mission must not inherit stale game flags");
        var cases = new[] {
            (Enums.eGameTypeModes.GAMETYPE_CAMPAIGN, -1, -1, 0, GameModeKind.Campaign),
            (Enums.eGameTypeModes.GAMETYPE_MAP, -1, -1, 0, GameModeKind.StandaloneMission),
            (Enums.eGameTypeModes.GAMETYPE_TUTORIAL, -1, -1, 0, GameModeKind.Tutorial),
            (Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER, 0, -1, 0, GameModeKind.CustomGame),
            (Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER, 1, 0, 0, GameModeKind.VanillaTrail),
            (Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER, 1, 11, 0, GameModeKind.SandsOfTime),
            (Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER, 2, -1, 0, GameModeKind.CustomTrail),
            (Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER, 0, -1, 2, GameModeKind.CoopTrail)
        };
        foreach (var item in cases)
        foreach (bool multiplayer in new[] { false, true })
        foreach (bool save in new[] { false, true })
        {
            var data = new EngineInterface.LoadMapReturnData {
                game_type = (int)item.Item1, skirmishGameType = item.Item2,
                skirmishTrail = item.Item3, coopTrailID = item.Item4 };
            var captured = GameModeHelper.CaptureMission(multiplayer, save, 0, -1, false, data, GameModeKind.Unknown);
            Check(captured.Kind == item.Item5 && captured.IsRealMultiplayer == multiplayer &&
                captured.MultiplayerSave == (multiplayer && save), "Central mode capture confused mode, network role or save origin");
            APIShared.MissionLifecycleService.Snapshot = captured;
            Check(GameModeHelper.Capture().Kind == item.Item5, "Consumer did not read the central snapshot");
        }
        var editor = CaptureModeFixture(editor: true);
        Check(editor.Kind == GameModeKind.MapEditor, "Explicit editor evidence was lost");
        var tutorial = GameModeHelper.CaptureMission(false, false, 0, -1, false,
            new EngineInterface.LoadMapReturnData { game_type = (int)Enums.eGameTypeModes.GAMETYPE_TUTORIAL }, GameModeKind.Tutorial);
        Check(!GameplayModModePolicy.IsAllowed(GameplayModModePolicy.GetProfile("ExtraFeatures_Serp", "Extra"), tutorial, out _),
            "Recognizing tutorials silently enabled regular gameplay mods");
        APIShared.MissionLifecycleService.Snapshot = default;
    }

    private static GameModeSnapshot CaptureModeFixture(bool multiplayer = false, bool editor = false) =>
        GameModeHelper.CaptureMission(multiplayer, false, 0, -1, editor,
            new EngineInterface.LoadMapReturnData { game_type = (int)Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER,
                skirmishGameType = (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM, skirmishTrail = -1 },
            editor ? GameModeKind.MapEditor : GameModeKind.CustomGame);
''')
s=s.replace('GameModeSnapshot customGame = GameModeHelper.Capture();','GameModeSnapshot customGame = CaptureModeFixture();')
s=s.replace('GameModeSnapshot realMultiplayerCustomGame = GameModeHelper.Capture();','GameModeSnapshot realMultiplayerCustomGame = CaptureModeFixture(multiplayer: true);')
s=s.replace('GameModeSnapshot editor = GameModeHelper.Capture();','GameModeSnapshot editor = CaptureModeFixture(editor: true);')
s=replace_method(s,'TestSharedGameplaySessionLifecycle','''    private static void TestSharedGameplaySessionLifecycle()
    {
        MissionEvents.ResetForTests();
        var state = new APIShared.MissionLifecycleState(_ => { });
        state.Register("adapter", "test", MissionEvents.PublishForTests);
        var order = new List<string>();
        MissionEvents.SetGate(e => order.Add("gate:" + e.Kind));
        var observed = new List<GameplaySessionStartedContext>();
        var start = GameplaySessionLifecycle.SubscribeStarted(null, e => {
            Check(order.Last() == "gate:Start", "Feature ran before its activation gate");
            observed.Add(e);
        });
        var seed = new APIShared.MissionContext(0, APIShared.MissionStartKind.LoadedSave,
            CaptureModeFixture(), "save.sav", "map", false);
        long id = state.Begin(seed);
        state.Checkpoint(id, seed, APIShared.MissionInitializationPhase.NativeLoaded);
        Check(observed.Count == 0, "Native load was exposed as a completed managed session");
        state.Ready(id, seed);
        Check(observed.Count == 1 && observed[0].IsLoadedSave, "Save load did not start exactly once");
        int replay = 0;
        var late = GameplaySessionLifecycle.SubscribeStarted(null, e => { if(e.IsReplay) replay++; });
        Check(replay == 1, "Late subscriber did not receive exactly one marked replay");
        state.End(APIShared.MissionEndReason.Unloaded);
        int stale = 0;
        var after = GameplaySessionLifecycle.SubscribeStarted(null, _ => stale++);
        Check(stale == 0, "Ended session leaked into a late registration");
        start.Dispose(); late.Dispose(); after.Dispose();
        MissionEvents.ResetForTests();
        TestSharedEditorSessionLifecycle();
    }
''')
s=replace_method(s,'TestSharedEditorSessionLifecycle','''    private static void TestSharedEditorSessionLifecycle()
    {
        var order = new List<string>();
        MissionEvents.SetGate(e => order.Add("gate:" + e.Kind));
        var state = new APIShared.MissionLifecycleState(_ => { });
        state.Register("adapter", "editor", MissionEvents.PublishForTests);
        var subscription = GameplaySessionLifecycle.SubscribeStarted(null, context => {
            Check(context.IsEditor && !context.IsLoadedSave && order.Last() == "gate:Start", "Editor contract/gate order changed");
            order.Add("feature:Start");
        }, () => order.Add("feature:End"));
        foreach (var kind in new[] { APIShared.MissionStartKind.EditorCreated, APIShared.MissionStartKind.EditorLoaded })
        {
            var seed = new APIShared.MissionContext(0, kind, CaptureModeFixture(editor:true), "test.map", "map", false);
            long id = state.Begin(seed);
            state.Checkpoint(id, seed, APIShared.MissionInitializationPhase.NativeLoaded);
            state.Ready(id, seed);
            state.End(APIShared.MissionEndReason.Unloaded);
            Check(order.Skip(order.Count-2).SequenceEqual(new[]{"gate:End","feature:End"}), "Editor cleanup preceded its gate reset");
        }
        subscription.Dispose();
        MissionEvents.ResetForTests();
    }
''')
s=s.replace('if (!source.Contains("MapLoaderR3EventHooks.OnStartMap.Observable"))','if (path.Contains(Path.DirectorySeparatorChar + "APIShared" + Path.DirectorySeparatorChar) || !source.Contains("MapLoaderR3EventHooks.OnStartMap.Observable"))')
s=s.replace('''Check(lifecycle.Contains("args.ReturnValue > 0") &&
              lifecycle.Contains("EventHookPhase.Post") &&''','''Check(lifecycle.Contains("TryGetMissionLifecycle") &&
              lifecycle.Contains("priority?.Invoke(notification)") &&''')
s=s.replace('Check(extraFeaturesRuntime.Contains("if (context.IsLoadedSave || context.IsEditor)") &&','Check(extraFeaturesRuntime.Contains("!context.IsReplay") &&')
s=s.replace('"Shared", "GameplayModModePolicy.cs"','"APIShared", "src", "GameplayModModePolicy.cs"').replace('"Shared", "GameplayFeatureModePolicy.cs"','"APIShared", "src", "GameplayFeatureModePolicy.cs"')
write(p,s)

for rel in ['_inspect/HostClientPresetTests/HostClientPresetTests.csproj','_inspect/LobbyModSettingsPresetTests/LobbyModSettingsPresetTests.csproj']:
    p=root/rel;s=p.read_text(encoding='utf-8-sig')
    extra='''    <Compile Include="..\\..\\APIShared\\src\\MissionLifecycleContracts.cs" />
    <Compile Include="..\\..\\APIShared\\src\\MissionLifecycleState.cs" />
    <Compile Include="..\\HostClientPresetTests\\MissionTestEnvironment.cs" />
    <Reference Include="R3"><HintPath>E:\\ProgrammeE\\Steam\\steamapps\\common\\Stronghold Crusader Definitive Edition\\BepInEx\\plugins\\000shcdese\\R3.dll</HintPath></Reference>
'''
    s=s.replace('  <ItemGroup>','  <ItemGroup>\n'+extra,1)
    write(p,s)
