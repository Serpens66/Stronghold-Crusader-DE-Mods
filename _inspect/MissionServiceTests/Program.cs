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
        Set(op, "Intent", kind == MissionStartKind.EditorCreated || kind == MissionStartKind.EditorLoaded ? Shared.GameModeKind.MapEditor : Shared.GameModeKind.CustomGame);
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
        Check(capability.Current?.Mode.Kind == Shared.GameModeKind.CoopTrail && capability.Current.MissionIndex == 9,
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
        Console.WriteLine($"PASS: {checks} production service boundary assertions (no native hooks or runtime installation).");
    }
}
