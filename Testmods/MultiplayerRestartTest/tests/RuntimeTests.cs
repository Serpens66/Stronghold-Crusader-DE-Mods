using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using MessagePack;
using MultiplayerSaveLoadTest;

// A separate desktop fixture. No Unity startup, Steam initialization or game process.
internal static class SaveLoadTestBootstrap
{
    private static string[] searchDirectories;

    public static int Main(string[] args)
    {
        try
        {
            string gameAssembly = Path.GetFullPath(args.Length > 0 ? args[0] :
                @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition");
            if (Directory.Exists(gameAssembly)) gameAssembly = Path.Combine(gameAssembly,
                "Stronghold Crusader Definitive Edition_Data", "Managed", "Assembly-CSharp.dll");
            if (!File.Exists(gameAssembly)) throw new FileNotFoundException("Selected real game assembly is missing.", gameAssembly);
            string managed = Path.GetDirectoryName(gameAssembly);
            string game = Directory.GetParent(Directory.GetParent(managed).FullName).FullName;
            searchDirectories = new[] {
                AppDomain.CurrentDomain.BaseDirectory, managed, Path.Combine(game, "BepInEx", "core"),
                Path.Combine(game, "BepInEx", "plugins", "000shcdese"),
                args.Length > 1 ? Path.GetFullPath(args[1]) : Path.Combine(game, "BepInEx", "plugins", "APIShared_Serp")
            };
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            return Run(gameAssembly);
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { AppDomain.CurrentDomain.AssemblyResolve -= Resolve; }
    }

    private static Assembly Resolve(object sender, ResolveEventArgs args)
    {
        string name = new AssemblyName(args.Name).Name;
        // Never turn an assembly name into an arbitrary filesystem path.
        if (name.IndexOfAny(new[] { '/', '\\', ':' }) >= 0) return null;
        foreach (string directory in searchDirectories)
        {
            string file = Path.Combine(directory, name + ".dll");
            if (File.Exists(file)) return Assembly.LoadFrom(file);
        }
        return null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run(string gameAssembly)
    {
        if (BarrierTests.Main(new[] { gameAssembly }) != 0) return 1;
        LobbyBridgeTests.Run();
        LocaleTests.Run();
        LoadWireTests.Run();
        RuntimeReceiptTests.Run();
        Console.WriteLine("Save/load offline runtime fixtures passed.");
        return 0;
    }
}

// Fault injection stops at transport/logging. Apply and PumpReceipts are the real
// runtime methods; no replacement implementation of their retry decisions exists.
internal static class RuntimeReceiptTests
{
    private static int checks, sends;
    private static bool rejectSend;
    private static int readyCalls;
    private static bool readyThrows;
    private static LocalReadyResult readyResult;
    private static Action duringReady;
    private static bool hasModal, hasRequester;
    private static int popups;
    private static bool popupInMultiplayer, popupInMasters;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool value, string reason)
    { checks++; if (!value) throw new Exception(reason); }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
    private static void Invoke(object target, string method, params object[] args)
        => target.GetType().GetMethod(method, Private).Invoke(target, args);
    private static bool SendPrefix(LoadWire wire)
    {
        if (wire.Packet.Kind == LoadMessageKind.ReleaseAcknowledged)
            Check((int)typeof(LoadBarrier).GetField("launchAllowed", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null) != 0,
                "Final acknowledgement was sent before permitting the ordinary native loader.");
        sends++;
        if (rejectSend) throw new InvalidOperationException("fixture-send-rejected");
        return false;
    }
    private static bool ReportPrefix() => false;
    private static bool ReadyPrefix(ref LocalReadyResult __result)
    {
        readyCalls++;
        duringReady?.Invoke();
        if (readyThrows) throw new InvalidOperationException("fixture-ready-failed");
        __result = readyResult; return false;
    }
    private static bool ModalPrefix(ref bool __result) { __result = hasModal; return false; }
    private static bool RequesterPrefix(ref bool __result) { __result = hasRequester; return false; }
    private static bool PopupPrefix(bool MPConf, bool skirmishMasters)
    { popups++; popupInMultiplayer = MPConf; popupInMasters = skirmishMasters; return false; }
    private static bool TextPrefix(string key, ref string __result) { __result = key; return false; }

    private static APIShared.Savegames.SaveLoadDialogContext Request(Enums.RequesterTypes type)
        => (APIShared.Savegames.SaveLoadDialogContext)Activator.CreateInstance(typeof(APIShared.Savegames.SaveLoadDialogContext),
            BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { 1L, type }, null);

    private static APIShared.Lobby.LobbyActionNotification ReadyAction(ulong lobby, string command = "Ready",
        bool before = true, bool after = false, bool originalInvoked = true)
        => (APIShared.Lobby.LobbyActionNotification)Activator.CreateInstance(typeof(APIShared.Lobby.LobbyActionNotification),
            BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { lobby, command, before, after, originalInvoked }, null);

    private sealed class Frame : APIShared.Presentation.IUiFrameWorkRegistration
    {
        internal bool Continuous;
        internal readonly List<TimeSpan> Delays = new List<TimeSpan>();
        public void Request() { }
        public void RequestAfter(TimeSpan delay) { Delays.Add(delay); }
        public void CancelPending() { }
        public void SetContinuous(bool enabled) { Continuous = enabled; }
    }
    private sealed class Lifecycle : APIShared.IMissionLifecycleCapability
    {
        public APIShared.MissionContext Current => null;
        public bool TryRegisterObserver(string id, Action<APIShared.MissionLifecycleNotification> start,
            Action<APIShared.MissionLifecycleNotification> end, Action<APIShared.MissionLifecycleNotification> initialization,
            out APIShared.NativeCapabilityDiagnostic diagnostic)
        { diagnostic = default(APIShared.NativeCapabilityDiagnostic); throw new NotSupportedException(); }
    }

    private static LoadStep CompletedClient(out LoadProtocol protocol)
    {
        protocol = new LoadProtocol(2, 1, new ulong[] { 1, 2 }, 10, 1000);
        var packet = new LoadPacket { Session = 10, Operation = "59064f03768c4d5b99a19bec606e218d", Kind = LoadMessageKind.Prepare };
        Check(protocol.Receive(1, packet, 1).Accepted, "Client setup Prepare failed.");
        protocol.Prepared(2); packet.Kind = LoadMessageKind.Stop;
        protocol.Receive(1, packet, 3); protocol.Stopped(4);
        packet.Kind = LoadMessageKind.Stopped; protocol.Receive(1, packet, 5);
        packet.Kind = LoadMessageKind.Lobby; packet.LobbyId = 20; protocol.Receive(1, packet, 6);
        protocol.Joined(7); packet.Kind = LoadMessageKind.ReleaseLobby; packet.LobbyId = 0;
        LoadStep released = protocol.Receive(1, packet, 8);
        Check(protocol.IsCompleted && released.Outbound.Single().Kind == LoadMessageKind.ReleaseAcknowledged,
            "Client setup did not produce the actual final receipt.");
        return released;
    }

    internal static void Run()
    {
        var harmony = new HarmonyLib.Harmony("MultiplayerSaveLoadTest.RuntimeReceiptFixture");
        var send = typeof(SaveLoadRuntime).GetMethod("Send", Private);
        var report = typeof(SaveLoadRuntime).GetMethod("Report", Private | BindingFlags.Public);
        var ready = typeof(LobbyBridge).GetMethod("TryReady", BindingFlags.Static | BindingFlags.NonPublic);
        var modal = typeof(LobbyBridge).GetMethod("HasModal", BindingFlags.Static | BindingFlags.NonPublic);
        var requester = typeof(LobbyBridge).GetMethod("HasSaveRequester", BindingFlags.Static | BindingFlags.NonPublic);
        var popup = typeof(CrusaderDE.HUD_ConfirmationPopup).GetMethod("ShowConfirmation", BindingFlags.Static | BindingFlags.Public,
            null, new[] { typeof(string), typeof(Action), typeof(Action), typeof(bool), typeof(bool) }, null);
        var text = typeof(SerpLocalization).GetMethod("Get", new[] { typeof(string) });
        harmony.Patch(send, prefix: new HarmonyLib.HarmonyMethod(typeof(RuntimeReceiptTests), nameof(SendPrefix)));
        harmony.Patch(report, prefix: new HarmonyLib.HarmonyMethod(typeof(RuntimeReceiptTests), nameof(ReportPrefix)));
        harmony.Patch(ready, prefix: new HarmonyLib.HarmonyMethod(typeof(RuntimeReceiptTests), nameof(ReadyPrefix)));
        harmony.Patch(modal, prefix: new HarmonyLib.HarmonyMethod(typeof(RuntimeReceiptTests), nameof(ModalPrefix)));
        harmony.Patch(requester, prefix: new HarmonyLib.HarmonyMethod(typeof(RuntimeReceiptTests), nameof(RequesterPrefix)));
        harmony.Patch(popup, prefix: new HarmonyLib.HarmonyMethod(typeof(RuntimeReceiptTests), nameof(PopupPrefix)));
        harmony.Patch(text, prefix: new HarmonyLib.HarmonyMethod(typeof(RuntimeReceiptTests), nameof(TextPrefix)));
        var platform = Platform_Multiplayer.Instance;
        var previousLobby = platform.activeLobby;
        var previousDirector = Director.instance;
        try
        {
            var lobby = (Platform_Multiplayer.MPLobby)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Platform_Multiplayer.MPLobby));
            lobby.id = new Steamworks.CSteamID(20); platform.activeLobby = lobby;
            Director.instance = (Director)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Director));
            var runtime = new SaveLoadRuntime(null);
            var frame = new Frame();
            Set(runtime, "frame", frame); Set(runtime, "lifecycle", new Lifecycle()); Set(runtime, "host", 1UL);
            Set(runtime, "menu", Activator.CreateInstance(typeof(APIShared.Presentation.IngameMenuActionRegistration),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { (Action)(() => { }) }, null));
            LoadStep receipt = CompletedClient(out LoadProtocol protocol); Set(runtime, "protocol", protocol);
            sends = 0; rejectSend = true;
            Invoke(runtime, "Apply", receipt);
            Check(sends == 1 && Get<LoadPacket>(runtime, "acknowledgementRetry") != null && frame.Continuous,
                "Rejected final receipt was not cached with continuing frame work.");
            long readyDeadline = Get<long>(runtime, "readyDeadline");
            Check(Get<bool>(runtime, "readyPending") && frame.Delays.Last() == TimeSpan.FromSeconds(1),
                "Actual release did not schedule deferred Ready.");
            long deadline = Get<long>(runtime, "acknowledgementDeadline");
            Check(deadline > System.Diagnostics.Stopwatch.GetTimestamp(), "Receipt did not receive a bounded deadline.");
            var duplicateRelease = new LoadPacket { Kind = LoadMessageKind.ReleaseLobby, Session = protocol.Session,
                Operation = protocol.Operation };
            LoadStep duplicate = protocol.Receive(1, duplicateRelease, 9);
            Check(duplicate.Accepted && duplicate.Effects == LoadEffect.None, "Duplicate release repeated local effects.");
            Invoke(runtime, "Apply", duplicate);
            Check(Get<long>(runtime, "acknowledgementDeadline") == deadline, "Duplicate failed receipt extended its deadline.");
            Check(Get<long>(runtime, "readyDeadline") == readyDeadline, "Duplicate release reset the Ready deadline.");
            Set(runtime, "nextAcknowledgementRetry", 0L); Invoke(runtime, "PumpReceipts");
            Check(sends == 3 && Get<LoadPacket>(runtime, "acknowledgementRetry") != null &&
                Get<long>(runtime, "acknowledgementDeadline") == deadline, "Failed retry lost cache or renewed its deadline.");
            rejectSend = false; Set(runtime, "nextAcknowledgementRetry", 0L); Invoke(runtime, "PumpReceipts");
            Check(sends == 4 && Get<LoadPacket>(runtime, "acknowledgementRetry") == null, "Successful retry did not clear the receipt.");
            // Expired old receipt with a changed world/lobby must be discarded before
            // timeout handling; it must not request a barrier or failure cleanup.
            Set(runtime, "acknowledgementRetry", receipt.Outbound.Single());
            Set(runtime, "acknowledgementDeadline", 0L);
            typeof(Director).GetField("multiplayerGame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Director.instance, true);
            LoadBarrier.Release(); Invoke(runtime, "PumpReceipts");
            Check(Get<LoadPacket>(runtime, "acknowledgementRetry") == null && !Get<bool>(runtime, "failurePending") && sends == 4,
                "Retired receipt acted on a replacement world.");
            var requested = typeof(LoadBarrier).GetField("requested", BindingFlags.NonPublic | BindingFlags.Static);
            Check((int)requested.GetValue(null) == 0, "Retired receipt parked a replacement world.");
            AutoReady(runtime, frame, protocol);
            RetryAdmission(runtime, platform);
            LoadMenu(runtime, platform, lobby);
            Console.WriteLine("Runtime receipts: " + checks + " checks passed.");
        }
        finally
        {
            platform.activeLobby = previousLobby; Director.instance = previousDirector;
            LoadBarrier.Release();
            harmony.Unpatch(send, HarmonyLib.HarmonyPatchType.Prefix, harmony.Id);
            harmony.Unpatch(report, HarmonyLib.HarmonyPatchType.Prefix, harmony.Id);
            harmony.Unpatch(ready, HarmonyLib.HarmonyPatchType.Prefix, harmony.Id);
            harmony.Unpatch(modal, HarmonyLib.HarmonyPatchType.Prefix, harmony.Id);
            harmony.Unpatch(requester, HarmonyLib.HarmonyPatchType.Prefix, harmony.Id);
            harmony.Unpatch(popup, HarmonyLib.HarmonyPatchType.Prefix, harmony.Id);
            harmony.Unpatch(text, HarmonyLib.HarmonyPatchType.Prefix, harmony.Id);
        }
    }

    private static void RetryAdmission(SaveLoadRuntime runtime, Platform_Multiplayer platform)
    {
        var previousRuntime = SaveLoadPlugin.Runtime;
        var previousProtocol = Get<LoadProtocol>(runtime, "protocol");
        bool previousInitialized = Get<bool>(runtime, "initialized");
        bool previousReleased = Get<bool>(runtime, "released");
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        var requested = typeof(LoadBarrier).GetField("requested", flags);
        var admission = typeof(SaveLoadRuntime).GetMethod("BeforeStart", flags);
        try
        {
            SaveLoadPlugin.Runtime = runtime; Set(runtime, "initialized", true);
            // This is the state left after Managed Started/Adopt when the native
            // handshake never reaches startSimThread: the protocol is retired,
            // its released flag is false, and the worker is still requested.
            Set(runtime, "protocol", null); Set(runtime, "released", false);
            requested.SetValue(null, 1); LoadBarrier.AllowLaunch();
            admission.Invoke(null, new object[] { platform });
            Check(LoadBarrier.Requested && LoadBarrier.LaunchAllowed,
                "A later Vanilla start was blocked or prematurely released the parked worker.");
            Check(Get<ulong>(runtime, "nextLobby") == platform.activeLobby.identifier,
                "A later admitted start did not capture its actual lobby.");
            typeof(LoadBarrier).GetField("launchAllowed", flags).SetValue(null, 0);
            bool blocked = false;
            try { admission.Invoke(null, new object[] { platform }); }
            catch (TargetInvocationException error) { blocked = error.InnerException is InvalidOperationException; }
            Check(blocked, "An unfinished barrier admitted an ordinary start.");
            LoadBarrier.AllowLaunch(); Set(runtime, "failurePending", true);
            blocked = false;
            try { admission.Invoke(null, new object[] { platform }); }
            catch (TargetInvocationException error) { blocked = error.InnerException is InvalidOperationException; }
            Check(blocked, "A failed group return admitted a start merely because the worker had launch permission.");
        }
        finally
        {
            LoadBarrier.Release(); SaveLoadPlugin.Runtime = previousRuntime;
            Set(runtime, "initialized", previousInitialized); Set(runtime, "protocol", previousProtocol);
            Set(runtime, "released", previousReleased); Set(runtime, "failurePending", false);
        }
    }

    private static void LoadMenu(SaveLoadRuntime runtime, Platform_Multiplayer platform, Platform_Multiplayer.MPLobby lobby)
    {
        var host = new LoadProtocol(1, 1, new ulong[] { 1 }, 10, 1000);
        host.Begin("59064f03768c4d5b99a19bec606e218d", 1); host.Prepared(2); host.Stopped(3);
        host.PublishLobby(20, 4); LoadStep completed = host.Joined(5);
        Check(host.IsCompleted && (completed.Effects & LoadEffect.OpenLoadMenu) != 0,
            "Host setup did not produce the real deferred load-menu effect.");
        Set(runtime, "protocol", host); Set(runtime, "host", 1UL); Set(runtime, "self", 1UL);
        typeof(Director).GetField("multiplayerGame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Director.instance, false);
        platform.activeLobby = lobby; popups = 0; hasModal = hasRequester = false;
        Invoke(runtime, "Apply", completed);
        Check(Get<bool>(runtime, "loadMenuPending") && popups == 0, "Host completion opened the category popup synchronously.");
        Invoke(runtime, "PumpLoadMenu");
        Check(popups == 0 && Get<bool>(runtime, "loadMenuPending"), "Category popup preceded automatic Ready completion.");
        Set(runtime, "readyPending", false); hasModal = true; Invoke(runtime, "PumpLoadMenu");
        Check(popups == 0 && Get<bool>(runtime, "loadMenuPending"), "Category popup replaced a foreign modal.");
        hasModal = false; Invoke(runtime, "PumpLoadMenu"); Invoke(runtime, "PumpLoadMenu");
        Check(popups == 1 && !Get<bool>(runtime, "loadMenuPending"), "Unblocked category popup did not open exactly once.");
        Check(popupInMultiplayer && !popupInMasters,
            "Category question targeted the hidden in-mission popup instead of the multiplayer lobby popup.");

        Set(runtime, "loadMenuPending", true); hasRequester = true; Invoke(runtime, "PumpLoadMenu");
        Check(popups == 1 && !Get<bool>(runtime, "loadMenuPending"), "Manual native requester did not cancel deferred category.");
        hasRequester = false; Invoke(runtime, "PumpLoadMenu");
        Check(popups == 1, "Closing the manual requester reopened the deferred category.");

        // The opening and cancellation both happen between pumps; visibility alone
        // cannot retain that user choice. Exercise the actual registered callback.
        Set(runtime, "loadMenuPending", true);
        Invoke(runtime, "SaveRequesterOpened", Request(Enums.RequesterTypes.LoadMultiplayerGame));
        Check(!Get<bool>(runtime, "loadMenuPending"), "Short manual requester opening did not cancel category intent.");
        Invoke(runtime, "PumpLoadMenu");
        Check(popups == 1, "Short requester open/cancel was followed by another category popup.");
        Set(runtime, "loadMenuPending", true);
        Invoke(runtime, "SaveRequesterOpened", Request(Enums.RequesterTypes.LoadSinglePlayerGame));
        Check(Get<bool>(runtime, "loadMenuPending"), "Unrelated requester cancelled multiplayer category intent.");
        platform.activeLobby = null;
        Invoke(runtime, "SaveRequesterOpened", Request(Enums.RequesterTypes.LoadMultiplayerCoopGame));
        Check(Get<bool>(runtime, "loadMenuPending"), "Old lobby requester affected current category intent.");
        platform.activeLobby = lobby;
        Invoke(runtime, "SaveRequesterOpened", Request(Enums.RequesterTypes.LoadMultiplayerCoopGame));
        Check(!Get<bool>(runtime, "loadMenuPending"), "Cooperative requester opening did not cancel category intent.");
        Invoke(runtime, "SaveRequesterOpened", Request(Enums.RequesterTypes.LoadMultiplayerGame));
        Check(!Get<bool>(runtime, "loadMenuPending"), "Own already-consumed requester recreated category intent.");

        Set(runtime, "loadMenuPending", true);
        typeof(Director).GetField("multiplayerGame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Director.instance, true);
        Invoke(runtime, "PumpLoadMenu");
        Check(popups == 1 && !Get<bool>(runtime, "loadMenuPending"), "New mission received a stale category popup.");
        typeof(Director).GetField("multiplayerGame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Director.instance, false);
        Set(runtime, "loadMenuPending", true); platform.activeLobby = null; Invoke(runtime, "PumpLoadMenu");
        Check(popups == 1 && !Get<bool>(runtime, "loadMenuPending"), "Changed lobby received a stale category popup.");
    }

    private static void AutoReady(SaveLoadRuntime runtime, Frame frame, LoadProtocol completed)
    {
        readyCalls = 0; readyThrows = false; readyResult = LocalReadyResult.Waiting;
        Set(runtime, "released", true);
        Invoke(runtime, "ScheduleAutoReady");
        long deadline = Get<long>(runtime, "readyDeadline");
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        Check(deadline > now && deadline <= now + System.Diagnostics.Stopwatch.Frequency * 90L,
            "Ready waiting period is not bounded to ninety seconds.");
        Invoke(runtime, "PumpAutoReady");
        Check(readyCalls == 0 && Get<bool>(runtime, "readyPending"), "Ready command fired before the initial delay.");
        Set(runtime, "nextReady", 0L); Invoke(runtime, "PumpAutoReady");
        Check(readyCalls == 1 && Get<bool>(runtime, "readyPending") && Get<int>(runtime, "readyAttempts") == 0 &&
            Get<long>(runtime, "readyDeadline") == deadline && frame.Delays.Last() == TimeSpan.FromSeconds(1),
            "Map waiting consumed a rejection or extended the waiting deadline.");
        Set(runtime, "readyDeadline", 0L); Set(runtime, "nextReady", 0L); Invoke(runtime, "PumpAutoReady");
        Check(readyCalls == 1 && !Get<bool>(runtime, "readyPending"), "Expired map waiting invoked Ready or kept retrying.");

        Invoke(runtime, "ScheduleAutoReady"); readyResult = LocalReadyResult.Rejected;
        int before = readyCalls;
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            Set(runtime, "nextReady", 0L); Invoke(runtime, "PumpAutoReady");
            Check(Get<int>(runtime, "readyAttempts") == attempt && readyCalls == before + attempt,
                "Rejected Ready attempts were not counted exactly once.");
            if (attempt < 5)
                Check(Get<bool>(runtime, "readyPending") && frame.Delays.Last() == TimeSpan.FromSeconds(1 << attempt),
                    "Rejected Ready did not schedule exponential backoff.");
        }
        Check(!Get<bool>(runtime, "readyPending"), "Five rejections did not stop automatic Ready.");
        Invoke(runtime, "PumpAutoReady"); Check(readyCalls == before + 5, "Rejected command retried past the limit.");

        foreach (LocalReadyResult result in new[] { LocalReadyResult.Ready, LocalReadyResult.NotCurrent })
        {
            Invoke(runtime, "ScheduleAutoReady"); readyResult = result;
            Set(runtime, "nextReady", 0L); Invoke(runtime, "PumpAutoReady"); before = readyCalls;
            Check(!Get<bool>(runtime, "readyPending"), "Terminal Ready result kept the loop active.");
            readyResult = LocalReadyResult.Rejected; // A later native reset must remain under user control.
            Invoke(runtime, "PumpAutoReady"); Check(readyCalls == before, "Terminal Ready was reasserted after a later native reset.");
        }
        Invoke(runtime, "ScheduleAutoReady"); readyThrows = true;
        Set(runtime, "nextReady", 0L); Invoke(runtime, "PumpAutoReady"); readyThrows = false;
        Check(!Get<bool>(runtime, "readyPending"), "Ready exception did not stop retries.");
        Check(!Get<bool>(runtime, "ownReadyInvocation"), "Ready exception retained autonomous invocation ownership.");

        ManualReadyIntent(runtime);

        foreach (string guard in new[] { "active", "released", "failurePending", "recovering", "readyLobby" })
        {
            Invoke(runtime, "ScheduleAutoReady");
            if (guard == "active") Set(runtime, "protocol", new LoadProtocol(2, 1, new ulong[] { 1, 2 }, 10, 1000));
            else if (guard == "released") Set(runtime, guard, false);
            else if (guard == "readyLobby") Set(runtime, guard, completed.LobbyId + 1);
            else Set(runtime, guard, true);
            before = readyCalls; Set(runtime, "nextReady", 0L); Invoke(runtime, "PumpAutoReady");
            Check(readyCalls == before && !Get<bool>(runtime, "readyPending"), "Ready escaped its runtime guard: " + guard);
            Set(runtime, "protocol", completed); Set(runtime, "released", true);
            Set(runtime, "failurePending", false); Set(runtime, "recovering", false);
        }
    }

    private static void ManualReadyIntent(SaveLoadRuntime runtime)
    {
        Invoke(runtime, "ScheduleAutoReady");
        int before = readyCalls;
        // A manual Ready followed by Unready before the first pump must stay unready.
        Invoke(runtime, "LobbyActionObserved", ReadyAction(20, before: false, after: true));
        Check(Get<bool>(runtime, "readyPending"), "Manual Ready-on cancelled the pending action as an Unready.");
        Invoke(runtime, "LobbyActionObserved", ReadyAction(20));
        Set(runtime, "nextReady", 0L); Invoke(runtime, "PumpAutoReady");
        Check(!Get<bool>(runtime, "readyPending") && readyCalls == before, "Manual Unready before first pump was reasserted.");

        Invoke(runtime, "ScheduleAutoReady"); readyResult = LocalReadyResult.Rejected;
        Set(runtime, "nextReady", 0L); Invoke(runtime, "PumpAutoReady"); before = readyCalls;
        Invoke(runtime, "LobbyActionObserved", ReadyAction(20));
        Set(runtime, "nextReady", 0L); Invoke(runtime, "PumpAutoReady");
        Check(!Get<bool>(runtime, "readyPending") && readyCalls == before, "Manual Unready during retry backoff was reasserted.");

        Invoke(runtime, "ScheduleAutoReady");
        foreach (var notification in new[] { ReadyAction(21), ReadyAction(20, command: "Play"),
            ReadyAction(20, originalInvoked: false), ReadyAction(20, before: false), ReadyAction(20, after: true) })
        {
            Invoke(runtime, "LobbyActionObserved", notification);
            Check(Get<bool>(runtime, "readyPending"), "Nonmanual/old/unexecuted action cancelled Ready intent.");
        }
        readyResult = LocalReadyResult.Waiting;
        duringReady = () => Invoke(runtime, "LobbyActionObserved", ReadyAction(20));
        try { Set(runtime, "nextReady", 0L); Invoke(runtime, "PumpAutoReady"); }
        finally { duringReady = null; }
        Check(Get<bool>(runtime, "readyPending") && !Get<bool>(runtime, "ownReadyInvocation"),
            "Own Ready callback cancelled intent or retained invocation ownership.");
        // A settings-driven reset has no Ready command notification and cannot cancel intent.
        before = readyCalls; Set(runtime, "nextReady", 0L); Invoke(runtime, "PumpAutoReady");
        Check(readyCalls == before + 1 && Get<bool>(runtime, "readyPending"), "A state-only reset suppressed the pending command.");
        readyResult = LocalReadyResult.Ready;
        Set(runtime, "nextReady", 0L); Invoke(runtime, "PumpAutoReady"); before = readyCalls;
        Invoke(runtime, "LobbyActionObserved", ReadyAction(20));
        Invoke(runtime, "PumpAutoReady");
        Check(!Get<bool>(runtime, "readyPending") && readyCalls == before, "Manual Unready after success reactivated automatic Ready.");
    }
}

internal static class LoadWireTests
{
    private const short PacketId = 137;
    private const ulong Session = 109775241157299357;
    private const string Operation = "59064f03768c4d5b99a19bec606e218d";
    private static int checks;

    private static void Check(bool value, string reason)
    { checks++; if (!value) throw new Exception(reason); }

    private static void Reject(Action action, string reason)
    {
        bool rejected = false;
        try { action(); }
        catch (MessagePackSerializationException) { rejected = true; }
        Check(rejected, reason);
    }

    private static LoadWire Message(LoadMessageKind kind, int control = 0) => new LoadWire {
        Control = control, PacketId = PacketId,
        Packet = new LoadPacket { Kind = kind, Session = Session,
            Operation = control == 0 ? Operation : "", LobbyId = kind == LoadMessageKind.Lobby ? 7654321UL : 0 }
    };

    // Build malformed frames independently of the production formatter. MessagePack's
    // primitive object-array encoding exercises the actual formatter's reader path.
    private static byte[] Raw(int version = 1, int control = 0, int id = PacketId, int kind = 1,
        ulong session = Session, string operation = Operation, ulong lobby = 0)
        => MessagePackSerializer.Serialize(new object[] { version, control, id, kind, session, operation, lobby });

    private static LoadWire Decode(byte[] bytes) => MessagePackSerializer.Deserialize<LoadWire>(bytes);

    internal static void Run()
    {
        foreach (LoadMessageKind kind in Enum.GetValues(typeof(LoadMessageKind))) RoundTrip(Message(kind));
        RoundTrip(Message(LoadMessageKind.Prepare, 1));
        RoundTrip(Message(LoadMessageKind.Prepare, 2));
        InvalidReaderInput();
        InvalidWriterInput();
        Copies();
        Framing();
        Console.WriteLine("LoadWire: " + checks + " checks passed.");
    }

    private static void RoundTrip(LoadWire source)
    {
        byte[] bytes = MessagePackSerializer.Serialize(source);
        Check(bytes.Length > 0 && bytes.Length <= LoadWire.MaximumBytes, "Wire size exceeds the transport contract.");
        LoadWire result = Decode(bytes);
        Check(result.Control == source.Control && result.PacketId == source.PacketId &&
            result.Packet.Kind == source.Packet.Kind && result.Packet.Session == source.Packet.Session &&
            result.Packet.Operation == source.Packet.Operation && result.Packet.LobbyId == source.Packet.LobbyId,
            "Actual MessagePack round trip changed the load packet.");
        Check(!ReferenceEquals(source, result) && !ReferenceEquals(source.Packet, result.Packet), "Decoded packet aliases the source.");
    }

    private static void InvalidReaderInput()
    {
        var invalid = new List<byte[]> {
            new byte[0], new byte[] { 0xc0 }, new byte[] { 0xc1 },
            MessagePackSerializer.Serialize(new object[6]), MessagePackSerializer.Serialize(new object[8]),
            Raw(version: 0), Raw(version: 2), Raw(control: -1), Raw(control: 3),
            Raw(id: 0), Raw(id: -1), Raw(id: short.MaxValue + 1), Raw(kind: 0), Raw(kind: int.MaxValue),
            Raw(session: 0), Raw(operation: null), Raw(operation: ""), Raw(operation: new string('a', 33)),
            Raw(operation: new string('a', 4096)), Raw(operation: new string('z', 32)), Raw(lobby: 1),
            Raw(kind: (int)LoadMessageKind.Lobby), Raw(control: 1),
            Raw(control: 1, operation: "", kind: (int)LoadMessageKind.Stop),
            Raw(control: 2, operation: "", lobby: 1)
        };
        foreach (byte[] bytes in invalid) Reject(() => Decode(bytes), "Malformed wire input accepted.");
        byte[] valid = Raw();
        for (int length = 0; length < valid.Length; length++)
        {
            byte[] truncated = valid.Take(length).ToArray();
            Reject(() => Decode(truncated), "Truncated wire accepted at " + length);
        }
        foreach (byte[] suffix in new[] { new byte[] { 0xc0 }, valid, new byte[] { 0, 0 } })
            Reject(() => Decode(valid.Concat(suffix).ToArray()), "Trailing wire data accepted.");
    }

    private static void InvalidWriterInput()
    {
        Reject(() => MessagePackSerializer.Serialize<LoadWire>(null), "Null wire serialized.");
        var invalid = new List<LoadWire>();
        var value = Message(LoadMessageKind.Stop); value.Packet = null; invalid.Add(value);
        value = Message(LoadMessageKind.Stop); value.PacketId = 0; invalid.Add(value);
        value = Message(LoadMessageKind.Stop); value.Control = 3; invalid.Add(value);
        value = Message(LoadMessageKind.Stop); value.Packet.Session = 0; invalid.Add(value);
        value = Message(LoadMessageKind.Stop); value.Packet.Operation = null; invalid.Add(value);
        value = Message(LoadMessageKind.Stop); value.Packet.Operation = "invalid"; invalid.Add(value);
        value = Message(LoadMessageKind.Stop); value.Packet.Kind = (LoadMessageKind)99; invalid.Add(value);
        value = Message(LoadMessageKind.Stop); value.Packet.LobbyId = 1; invalid.Add(value);
        value = Message(LoadMessageKind.Lobby); value.Packet.LobbyId = 0; invalid.Add(value);
        value = Message(LoadMessageKind.Stop, 1); invalid.Add(value);
        foreach (LoadWire item in invalid)
            Reject(() => MessagePackSerializer.Serialize(item), "Invalid local envelope serialized.");
    }

    private static void Copies()
    {
        var source = Message(LoadMessageKind.Lobby);
        var copy = source.Copy();
        Check(!ReferenceEquals(copy, source) && !ReferenceEquals(copy.Packet, source.Packet), "Copy aliases mutable packet.");
        copy.Control = 2; copy.PacketId = 42; copy.Packet.Kind = LoadMessageKind.Abort;
        copy.Packet.Session = 1; copy.Packet.Operation = "changed"; copy.Packet.LobbyId = 1;
        Check(source.Control == 0 && source.PacketId == PacketId && source.Packet.Kind == LoadMessageKind.Lobby &&
            source.Packet.Session == Session && source.Packet.Operation == Operation && source.Packet.LobbyId == 7654321,
            "Mutating copied envelope changed source.");
        var empty = new LoadWire { Control = 1, PacketId = PacketId };
        Check(empty.Copy().Packet == null, "Null-packet copy invented payload.");
    }

    private static void Framing()
    {
        byte[] body = MessagePackSerializer.Serialize(Message(LoadMessageKind.Lobby));
        var frame = new byte[6 + body.Length];
        Buffer.BlockCopy(BitConverter.GetBytes(PacketId), 0, frame, 0, 2);
        Buffer.BlockCopy(BitConverter.GetBytes(body.Length), 0, frame, 2, 4);
        Buffer.BlockCopy(body, 0, frame, 6, body.Length);
        byte[] extracted = LobbyBridge.ExtractControl(frame, PacketId);
        Check(extracted != null && Decode(extracted).Packet.LobbyId == 7654321, "Real wire rejected by transport framing.");
        Check(LobbyBridge.ExtractControl(frame, (short)(PacketId + 1)) == null, "Foreign transport packet accepted.");
        Buffer.BlockCopy(BitConverter.GetBytes(body.Length - 1), 0, frame, 2, 4);
        Check(LobbyBridge.ExtractControl(frame, PacketId) == null, "Mismatching transport length accepted.");
    }
}
