using APIShared.GameModes;
using APIShared;
using MonoMod.RuntimeDetour;
using SHCDESE.EventAPI;
using System.Reflection;

internal static class Program
{
    internal static Action MessageBody;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly Type Service = typeof(MissionLifecycleService);
    static readonly Type Operation = Service.GetNestedType("Operation", BindingFlags.NonPublic)!;
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethods(Private)
        .Single(m => m.Name == name && !m.IsGenericMethod).Invoke(target, args)!;
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private)!.SetValue(target, value);
    static object NewOperation(MissionStartKind kind, bool multiplayer = false)
    {
        var op = Activator.CreateInstance(Operation, true)!;
        Set(op, "Kind", kind); Set(op, "Multiplayer", multiplayer);
        Set(op, "Intent", kind == MissionStartKind.EditorCreated || kind == MissionStartKind.EditorLoaded ? APIShared.GameModes.GameModeKind.MapEditor : APIShared.GameModes.GameModeKind.CustomGame);
        return op;
    }
    static void Native(MissionLifecycleService service, int error = 1) => Call(service, "CaptureNative",
        (Func<EngineInterface.LoadMapReturnData>)(() => { MapLoaderR3EventHooks.OnUnloadMap.Observable.Send(new() { Phase = EventHookPhase.Pre }); return new() { errorCode = error, playerID = 1, mapSize = 160 }; }));
    static void Main()
    {
        Hook.FailApplyAt = 3;
        Check(!MissionLifecycleService.TryCreate(null, out _, out _), "failed installation was published");
        Check(Hook.All.All(h => !h.Applied && h.Disposed), "installation rollback leaked hooks");
        Hook.All.Clear(); Hook.FailApplyAt = 0;
        NativeApiLog.Fail = true;
        Check(MissionLifecycleService.TryCreate(null, out var service, out _), "service installation failed");
        NativeApiLog.Fail = false;
        var capability = service.Bind("test");
        var events = new List<MissionLifecycleNotification>();
        capability.TryRegisterObserver("all", events.Add, events.Add, events.Add, out _);
        Action successful = () => { Native(service); Call(service, "MarkManaged"); };
        int vanillaCalls = 0;
        var editorLoad = Hook.All.Single(h => h.Callback.Method.ReturnType == typeof(bool) &&
            h.Callback.Method.GetParameters().Length == 3 &&
            h.Callback.Method.GetParameters()[0].ParameterType == typeof(EditorDirector)).Callback;
        EditorDirector.LoadBody = () => { vanillaCalls++; Native(service); return false; };
        Check((bool)editorLoad.DynamicInvoke(EditorDirector.instance, "bad.map", "bad") == false &&
            capability.Current == null && vanillaCalls == 1, "false editor result was published or Vanilla repeated");
        EditorDirector.LoadBody = () => { vanillaCalls++; Native(service); return true; };
        Check((bool)editorLoad.DynamicInvoke(EditorDirector.instance, "good.map", "good") &&
            capability.Current?.FilePath == "good.map" && vanillaCalls == 2, "successful full editor load did not publish");
        int phaseBegin = events.Count;
        Call(service, "Run", NewOperation(MissionStartKind.NewGame), (Action)(() =>
        {
            Call(service, "CaptureNative", (Func<EngineInterface.LoadMapReturnData>)(() =>
            {
                Check(events.Last().Phase == MissionInitializationPhase.BeforeLoad, "BeforeLoad followed the native call");
                MapLoaderR3EventHooks.OnStartMap.Observable.Send(new() { Phase = EventHookPhase.Pre });
                MapLoaderR3EventHooks.OnStartMap.Observable.Send(new() { Phase = EventHookPhase.Post, ReturnValue = -1 });
                return new() { errorCode = 1 };
            }));
            Call(service, "MarkManaged");
        }));
        Check(events.Skip(phaseBegin).Where(e => e.Kind != MissionLifecycleKind.End).Select(e => e.Phase).SequenceEqual(new[] {
            MissionInitializationPhase.BeforeLoad, MissionInitializationPhase.BeforeNativeStart,
            MissionInitializationPhase.AfterNativeStart, MissionInitializationPhase.NativeLoaded, MissionInitializationPhase.ManagedReady }),
            "initialization and completion callbacks were reordered");
        var multiplayerInit = Hook.All.Single(h => h.Callback.Method.ReturnType == typeof(EngineInterface.MultiplayerSetupData)).Callback;
        Call(service, "Run", NewOperation(MissionStartKind.NewGame), (Action)(() =>
        {
            multiplayerInit.DynamicInvoke(true, null, 4, 9, false, false, false);
            successful();
        }));
        Check(capability.Current?.Mode.Kind == APIShared.GameModes.GameModeKind.CoopTrail && capability.Current.MissionIndex == 9,
            "Managed MP pre-initializer lost local coop provenance or changed the zero-based mission index");
        foreach (MissionStartKind kind in Enum.GetValues<MissionStartKind>())
        {
            int start = events.Count(e => e.Kind == MissionLifecycleKind.Start);
            Call(service, "Run", NewOperation(kind), successful);
            Check(capability.Current?.StartKind == kind, "wrong start kind");
            Check(events.Count(e => e.Kind == MissionLifecycleKind.Start) == start + 1, "start duplicated/missing after nested unload");
        }
        var active = capability.Current!.SessionId;
        var screenHook = Hook.All.Single(h => h.Callback.Method.GetParameters().Length == 2 && h.Callback.Method.GetParameters()[0].ParameterType == typeof(CrusaderDE.MainViewModel)).Callback;
        var vm = new CrusaderDE.MainViewModel();
        screenHook.DynamicInvoke(vm, Enums.SceneIDS.Options);
        Check(capability.Current?.SessionId == active, "options ended the mission");
        screenHook.DynamicInvoke(vm, Enums.SceneIDS.FrontEnd);
        Check(capability.Current == null, "actual scene departure retained mission");
        Call(service, "Run", NewOperation(MissionStartKind.NewGame), successful);
        int ready = events.Count(e => e.Kind == MissionLifecycleKind.Start);
        Call(service, "Run", NewOperation(MissionStartKind.EditorLoaded), (Action)(() => Native(service, 0)));
        Check(capability.Current == null && events.Count(e => e.Kind == MissionLifecycleKind.Start) == ready, "failed native load published start");
        Check(events.Last().EndReason == MissionEndReason.Failed && !events.Last().HasStarted, "failed attempt missing End");
        int originals = 0;
        try { Call(service, "Run", NewOperation(MissionStartKind.NewGame), (Action)(() => { originals++; throw new Exception("vanilla"); })); } catch (TargetInvocationException) { }
        Check(originals == 1 && events.Last().EndReason == MissionEndReason.Exception, "throwing Vanilla repeated or exception not ended");
        Call(service, "Run", NewOperation(MissionStartKind.NewGame), (Action)(() =>
        {
            MapLoaderR3EventHooks.OnStartMap.Observable.Send(new() { Phase = EventHookPhase.Pre });
            MapLoaderR3EventHooks.OnStartMap.Observable.Send(new() { Phase = EventHookPhase.Post, ReturnValue = -11 });
            successful();
        }));
        Check(capability.Current == null, "outer native success masked inner campaign failure");
        Platform_Multiplayer.Instance.Host = false;
        Call(service, "Run", NewOperation(MissionStartKind.LoadedSave, true), (Action)(() => Native(service)));
        Check(capability.Current == null && events.Last().Kind == MissionLifecycleKind.Initialization, "client completed before delayed message");
        MessageBody = () => Call(service, "MarkManaged");
        Call(service, "ProcessMessage", Platform_Multiplayer.Instance, new Platform_Multiplayer.MPData(), new Platform_Multiplayer.MPGameMember(), false);
        Check(capability.Current?.IsSave == true, "delayed save client never completed");
        long completed = capability.Current.SessionId;
        Call(service, "ProcessMessage", Platform_Multiplayer.Instance, new Platform_Multiplayer.MPData(), new Platform_Multiplayer.MPGameMember(), false);
        Check(events.Count(e => e.Kind == MissionLifecycleKind.Start && e.Context.SessionId == completed) == 1, "duplicate client message restarted session");
        Call(service, "Run", NewOperation(MissionStartKind.LoadedSave, true), (Action)(() => Native(service)));
        Call(service, "Run", NewOperation(MissionStartKind.EditorCreated), successful);
        active = capability.Current!.SessionId;
        Call(service, "ProcessMessage", Platform_Multiplayer.Instance, new Platform_Multiplayer.MPData(), new Platform_Multiplayer.MPGameMember(), false);
        Check(capability.Current?.SessionId == active, "replaced waiting client reactivated");
        int survivors = 0;
        capability.TryRegisterObserver("throw", _ => throw new Exception("observer"), null, null, out _);
        capability.TryRegisterObserver("survivor", _ => survivors++, null, null, out _);
        NativeApiLog.Fail = true;
        Call(service, "Run", NewOperation(MissionStartKind.NewGame), successful);
        NativeApiLog.Fail = false;
        Check(survivors == 2 && capability.Current != null, "observer/logger failure blocked another subscriber or Vanilla");
        TestRestarts(service, events, successful);
        Console.WriteLine($"PASS: {checks} production service boundary assertions (no native hooks or runtime installation).");
    }

    static void TestRestarts(MissionLifecycleService service, List<MissionLifecycleNotification> events, Action successful)
    {
        Platform_Multiplayer.Instance.Host = true;
        var capability = service.Bind("restart-tests");
        var pairs = new List<(long Source, long Destination)>();
        capability.TryRegisterObserver("ownership", null, e =>
        {
            if (e.ReplacementContext?.RestartedFromSessionId == e.Context.SessionId)
            {
                Check(MissionRestart.IsInProgress, "restart preparation was not active before source End");
                pairs.Add((e.Context.SessionId, e.ReplacementContext.SessionId));
            }
        }, null, out _);
        foreach (var kind in new[] { APIShared.GameModes.GameModeKind.CustomGame,
            APIShared.GameModes.GameModeKind.CustomTrail, APIShared.GameModes.GameModeKind.CoopTrail,
            APIShared.GameModes.GameModeKind.Campaign, APIShared.GameModes.GameModeKind.StandaloneMission })
        {
            var seed = NewOperation(MissionStartKind.NewGame);
            Set(seed, "Intent", kind);
            Call(service, "Run", seed, successful);
            for (int i = 0; i < 2; i++)
            {
                long source = capability.Current.SessionId;
                int before = events.Count;
                MissionRestart.Run("arbitrary.mod", () => MissionRestart.Run("nested.mod", successful));
                Check(capability.Current?.IsRestart == true && !capability.Current.IsSave &&
                    capability.Current.RestartedFromSessionId == source && capability.Current.Mode.Kind == kind,
                    "restart lost identity, kind or regular new-game semantics");
                Check(events.Skip(before).Count(e => e.Kind == MissionLifecycleKind.End) == 1 &&
                    events.Skip(before).Count(e => e.Kind == MissionLifecycleKind.Start) == 1,
                    "nested restart duplicated lifecycle notifications");
                Check(!MissionRestart.IsInProgress && pairs.Last() == (source, capability.Current.SessionId), "restart ownership was not linked");
            }
        }
        // The actual menu and popup hooks: opening/No do not retire a session; Yes runs once.
        var button = Hook.All.Single(h => h.Callback.Method.GetParameters().FirstOrDefault()?.ParameterType == typeof(CrusaderDE.HUD_IngameMenu)).Callback;
        var popup = Hook.All.Single(h => h.Callback.Method.GetParameters().Length == 5 && h.Callback.Method.GetParameters()[0].ParameterType == typeof(string)).Callback;
        int actions = 0;
        CrusaderDE.HUD_IngameMenu.ButtonBody = _ => popup.DynamicInvoke("localized title", (Action)(() => { actions++; successful(); }), (Action)(() => { }), false, false);
        long unchanged = capability.Current.SessionId;
        button.DynamicInvoke(new CrusaderDE.HUD_IngameMenu(), 5);
        CrusaderDE.HUD_ConfirmationPopup.No();
        Check(capability.Current.SessionId == unchanged && actions == 0, "cancelled confirmation changed lifecycle");
        CrusaderDE.HUD_ConfirmationPopup.Yes();
        Check(actions == 1 && capability.Current.RestartedFromSessionId == unchanged, "confirmed menu restart was not wrapped");
        unchanged = capability.Current.SessionId;
        CrusaderDE.HUD_IngameMenu.ButtonBody = _ => popup.DynamicInvoke("other confirmation", (Action)(() => actions++), (Action)(() => { }), false, false);
        button.DynamicInvoke(new CrusaderDE.HUD_IngameMenu(), 6);
        CrusaderDE.HUD_ConfirmationPopup.Yes();
        Check(actions == 2 && capability.Current.SessionId == unchanged, "unrelated confirmation became a restart");
        CrusaderDE.HUD_IngameMenu.ButtonBody = _ => popup.DynamicInvoke("restart", (Action)(() => { actions++; successful(); }), (Action)(() => { }), false, false);
        button.DynamicInvoke(new CrusaderDE.HUD_IngameMenu(), 5);
        var staleYes = CrusaderDE.HUD_ConfirmationPopup.Yes;
        Call(service, "Run", NewOperation(MissionStartKind.NewGame), successful);
        unchanged = capability.Current.SessionId;
        staleYes();
        Check(actions == 2 && capability.Current.SessionId == unchanged, "stale confirmation restarted another mission");
        bool offThread = false;
        try { System.Threading.Tasks.Task.Run(() => MissionRestart.Run("worker", () => offThread = true)).GetAwaiter().GetResult(); }
        catch (InvalidOperationException) { }
        Check(!offThread && capability.Current.SessionId == unchanged, "worker thread was allowed to mutate restart state");
        var skirmish = Hook.All.Single(h => h.Target.Name == "RestartSkirmishGame").Callback;
        CrusaderDE.FRONT_Multiplayer.RestartBody = successful;
        skirmish.DynamicInvoke(new CrusaderDE.FRONT_Multiplayer(), new CrusaderDE.HUD_IngameMenu.RestartSkirmishMapInfo());
        Check(capability.Current.RestartedFromSessionId == unchanged, "dedicated Skirmish entry did not establish restart ownership");
        Call(service, "Run", NewOperation(MissionStartKind.LoadedSave), successful);
        unchanged = capability.Current.SessionId;
        MissionRestart.Run("save-restart", successful);
        Check(!capability.Current.IsSave && capability.Current.RestartedFromSessionId == unchanged,
            "restart of a loaded save was misclassified as loading another save");
        var error = new InvalidOperationException("Vanilla failure");
        try { MissionRestart.Run("test", () => throw error); Check(false, "restart exception swallowed"); }
        catch (InvalidOperationException caught) { Check(ReferenceEquals(caught, error), "restart replaced Vanilla exception"); }
        Check(capability.Current == null && events.Last().EndReason == MissionEndReason.Exception && !MissionRestart.IsInProgress,
            "failed restart retained ownership or scope");
        Call(service, "Run", NewOperation(MissionStartKind.NewGame), successful);
        MissionRestart.Run("test", () => Native(service, 0));
        Check(capability.Current == null && events.Last().EndReason == MissionEndReason.Failed, "native restart failure became ready");
        Platform_Multiplayer.Instance.Host = false;
        Call(service, "Run", NewOperation(MissionStartKind.NewGame, true), successful);
        MissionRestart.Run("client", () => Native(service));
        Check(capability.Current == null && !MissionRestart.IsInProgress, "delayed restart client completed early");
        MessageBody = () => Call(service, "MarkManaged");
        Call(service, "ProcessMessage", Platform_Multiplayer.Instance, new Platform_Multiplayer.MPData(), new Platform_Multiplayer.MPGameMember(), false);
        Check(capability.Current?.IsRestart == true && !capability.Current.IsSave, "delayed restart client lost restart attribution");
        long delayedSource = capability.Current.SessionId;
        MissionRestart.Run("aborted-client", () => Native(service));
        int beforeAbort = events.Count;
        Call(service, "End", MissionEndReason.Unloaded);
        Check(events.Skip(beforeAbort).Count(e => e.Kind == MissionLifecycleKind.End) == 1 &&
            !events.Last().HasStarted && events.Last().Context.RestartedFromSessionId == delayedSource,
            "abandoned client restart did not retire its exact pending session once");
        int afterAbort = events.Count;
        Call(service, "ProcessMessage", Platform_Multiplayer.Instance, new Platform_Multiplayer.MPData(), new Platform_Multiplayer.MPGameMember(), false);
        Check(capability.Current == null && events.Count == afterAbort,
            "a late handshake revived an abandoned client restart");
        Call(service, "Run", NewOperation(MissionStartKind.NewGame, true), successful);
        MissionRestart.Run("superseded-client", () => Native(service));
        Call(service, "Run", NewOperation(MissionStartKind.NewGame), successful);
        Check(!capability.Current.IsRestart && capability.Current.RestartedFromSessionId == null, "restart attribution leaked into a different launch");
    }
}
