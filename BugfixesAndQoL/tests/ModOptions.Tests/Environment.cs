// These publishers/memory buffers exercise the actual runtime sources without installing hooks.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SHCDESE.EventAPI.Projectiles;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace BepInEx.Logging { public class ManualLogSource { } }
namespace R3
{
    public sealed class Subscription : IDisposable { private readonly Action action; public Subscription(Action a) { action = a; } public void Dispose() => action(); }
    public sealed class TestObservable<T>
    {
        private readonly List<Action<T>> callbacks;
        private readonly Func<T, bool> filter;
        public TestObservable() { callbacks = new List<Action<T>>(); filter = _ => true; }
        private TestObservable(List<Action<T>> c, Func<T, bool> f) { callbacks = c; filter = f; }
        public TestObservable<T> Observable => this;
        public TestObservable<T> Where(Func<T, bool> predicate) => new TestObservable<T>(callbacks, x => filter(x) && predicate(x));
        public IDisposable Subscribe(Action<T> action) { Action<T> wrapped = x => { if (filter(x)) action(x); }; callbacks.Add(wrapped); return new Subscription(() => callbacks.Remove(wrapped)); }
        public void Emit(T value) { foreach (var callback in callbacks.ToArray()) callback(value); }
    }
}
namespace SHCDESE.EventAPI { public enum EventHookPhase { Pre, Post } }
namespace SHCDESE.EventAPI.MapLoader { }
namespace SHCDESE.EventAPI.Projectiles
{
    public class ProjectileSpawnEventArgs { public SHCDESE.EventAPI.EventHookPhase Phase; public ProjectileType ProjectileType; public long ReturnValue; }
    public class ProjectileDeleteEventArgs { public SHCDESE.EventAPI.EventHookPhase Phase; public int ProjectileId; }
}
namespace SHCDESE.EventAPI
{
    public static class ProjectileR3EventHooks
    {
        public static readonly R3.TestObservable<ProjectileSpawnEventArgs> OnProjectileSpawn = new R3.TestObservable<ProjectileSpawnEventArgs>();
        public static readonly R3.TestObservable<ProjectileDeleteEventArgs> OnProjectileDelete = new R3.TestObservable<ProjectileDeleteEventArgs>();
    }
}
namespace SHCDESE.Interop.Enums { public enum AliveState { None, NeedsInit, IsAlive, ToDelete } public enum ProjectileType { None, Disease = 22 } }
namespace SHCDESE.Interop
{
    public struct GameProjectile { public AliveState r_AliveState; public ProjectileType r_ProjectileType; public uint r_GlobalId; }
    public struct GameBuilding { public int r_PlayerIdOwner; public uint r_GlobalId; public ushort r_TilePositionXBegin, r_TilePositionYBegin; }
    public unsafe struct NativePointer<T> where T : unmanaged { public T* Pointer; public NativePointer(T* p) { Pointer = p; } }
}
namespace SHCDESE.API.Components.SaveData
{
    public class SaveContext { public bool IsSaveFile = true; }
    public class LoadContext { public bool IsSaveFile = true; }
}
namespace SHCDESE.API
{
    public unsafe sealed class GameProjectileManagerAPI
    {
        public static readonly GameProjectileManagerAPI Instance = new GameProjectileManagerAPI();
        public readonly GameProjectile* Pool = (GameProjectile*)Marshal.AllocHGlobal(sizeof(GameProjectile) * 256);
        public int Reads;
        public bool TryGetProjectileById(int id, out GameProjectile* value) { Reads++; value = id > 0 && id < 256 ? Pool + id : null; return value != null; }
    }
    public unsafe sealed class GameBuildingManagerAPI
    {
        public static readonly GameBuildingManagerAPI Instance = new GameBuildingManagerAPI();
        public readonly GameBuilding* Building = (GameBuilding*)Marshal.AllocHGlobal(sizeof(GameBuilding));
        public bool TryGetBuildingById(int id, out GameBuilding* value) { value = Building; return true; }
    }
    public sealed class TestTimerEngine
    {
        public readonly Dictionary<string, Action> Pending = new Dictionary<string, Action>();
        public bool Fail;
        public string AddDelayedAction(int ms, Action action, string name) { if (Fail) throw new Exception("timer failure"); if (ms != 3000 || name != null) throw new Exception("watchdog contract"); string key = Guid.NewGuid().ToString(); Pending.Add(key, action); return key; }
        public void RemoveAction(string handle) => Pending.Remove(handle);
        public void Fire() { var actions = new List<Action>(Pending.Values); Pending.Clear(); foreach (var action in actions) action(); }
    }
    public sealed class GameTimeManagerAPI { public static readonly GameTimeManagerAPI Instance = new GameTimeManagerAPI(); public readonly TestTimerEngine Timers = new TestTimerEngine(); public TestTimerEngine GetTimerEngine() => Timers; }
    public sealed class ModSaveDataAPI
    {
        public static readonly ModSaveDataAPI Instance = new ModSaveDataAPI();
        public Func<Components.SaveData.SaveContext, byte[]> Save;
        public Action<byte[], Components.SaveData.LoadContext> Load;
        public Action Reset;
        public bool RegisterModDataHandler(string id, Func<Components.SaveData.SaveContext, byte[]> save, Action<byte[], Components.SaveData.LoadContext> load, Action reset) { if (id != "serp-plague-popularity-v1") throw new Exception("save identity"); Save = save; Load = load; Reset = reset; return true; }
        public void UnregisterModDataHandler(string id) { }
    }
    public sealed class GameXAMLManagerAPI { public static readonly GameXAMLManagerAPI Instance = new GameXAMLManagerAPI(); public void RegisterBinding(string key, object value) { } }
}
namespace RedBird.Core.Memory { public class ScanRegion { } }
namespace RedBird.Abstractions.Hooks
{
    public class DetourHandle<T> where T : Delegate { public T Original; public bool Success = true; }
    public class HookHandle<T> { public bool Success = true; }
    public class HookTarget { public static HookTarget FromAddress(ulong address) => new HookTarget(); }
    public enum CallbackErrorMode { LogAndContinue }
    public enum OverwrittenInstructionPlacement { AfterCallback }
}
namespace RedBird.Abstractions.Hooks.Transaction { public class CommitResult { public bool IsCompleteSuccess = true; } }
namespace RedBird.X64.Hooks { public class X64InlineHook { } }
namespace RedBird.X64.Assembly { public struct X64SmartCPUContext { public ulong RAX, RDX, R12, R14, RBP; } [Flags] public enum X64SmartCPUContextRegs { Volatile = 1, RBP = 2, R12 = 4, R14 = 8 } }
namespace RedBird.X64.Hooks.Transaction
{
    public class HookTransaction
    {
        public void AddDetour<T>(RedBird.Abstractions.Hooks.DetourHandle<T> handle, RedBird.Abstractions.Hooks.HookTarget target, T callback) where T : Delegate
        { handle.Original = (T)Delegate.CreateDelegate(typeof(T), typeof(BugfixesAndQoL.Program).GetMethod("Original")); }
        public RedBird.Abstractions.Hooks.Transaction.CommitResult Commit() => new RedBird.Abstractions.Hooks.Transaction.CommitResult();
    }
}
namespace Shared
{
    public class Mode { public string ToDiagnosticString() => "test"; }
    public class GameplaySessionStartedContext { public string Kind = "test"; public Mode Mode = new Mode(); }
    public static class GameplaySessionLifecycle
    {
        public static Action<GameplaySessionStartedContext> Started;
        public static Action Reset;
        public static IDisposable SubscribeStarted(BepInEx.Logging.ManualLogSource log, Action<GameplaySessionStartedContext> started, Action reset) { Started = started; Reset = reset; return new R3.Subscription(() => { }); }
    }
    public static class DebugLogHelper
    {
        public static int Warnings, Errors, Factories; public static bool FailDebug;
        public static void LogDebug(BepInEx.Logging.ManualLogSource log, Func<string> text) { if (FailDebug) { Factories++; throw new Exception("listener failure"); } }
        public static void LogWarning(BepInEx.Logging.ManualLogSource log, string text) { Warnings++; }
        public static void LogError(BepInEx.Logging.ManualLogSource log, string text) { Errors++; }
    }
}
namespace UnityEngine
{
    public static class Application { public static event Action onBeforeRender; public static void Render() { Time.frameCount++; onBeforeRender?.Invoke(); } }
    public static class Time { public static int frameCount; }
}
namespace CrusaderDE
{
    public class MainViewModel { public static bool viewModelLoaded = true; public static MainViewModel Instance = new MainViewModel(); public bool Show_InGame = true; }
    public class Director { public static Director instance = new Director(); public bool SimRunning = true; }
    public static class GameData { public static Scenario scenario = new Scenario(); public class Scenario { public int Reads; public object getWinTimer(ref int start, ref int now, ref int end) { Reads++; end = 80; return this; } } }
    public static class Enums { public enum eOnScreenText { OST_TIMETODEFEAT, OST_WIN_TIMER, OST_PEACETIMER } }
    public class OnScreenText { public static OnScreenText Instance = new OnScreenText(); public class OST { public int curValue; } public OST getOST(Enums.eOnScreenText id, ref bool off, ref bool changed, bool reset) => null; }
}
namespace BugfixesAndQoL
{
    internal static class BugfixesHookInfrastructure
    {
        public static RedBird.X64.Hooks.Transaction.HookTransaction CreateOwnedTransaction(RedBird.Core.Memory.ScanRegion region) => new RedBird.X64.Hooks.Transaction.HookTransaction();
        public static void AddContextHook(RedBird.X64.Hooks.Transaction.HookTransaction t, RedBird.Abstractions.Hooks.HookHandle<RedBird.X64.Hooks.X64InlineHook> h, ulong address, Action<NativePointer<RedBird.X64.Assembly.X64SmartCPUContext>> callback, RedBird.X64.Assembly.X64SmartCPUContextRegs registers, RedBird.Abstractions.Hooks.CallbackErrorMode errorMode, RedBird.Abstractions.Hooks.OverwrittenInstructionPlacement placement) { }
    }
    internal static class PlagueNativePatternValidator { public static int Resolve(BepInEx.Logging.ManualLogSource l, ReadOnlySpan<byte> m, string p, int r, bool match, string name) => r; }
    internal sealed class BugfixesAndQoLViewModel
    {
        public event Action<string> SettingChanged;
        private bool mod, fix, client, countdown;
        public bool EnableMod { get => mod; set { mod = value; SettingChanged?.Invoke(nameof(EnableMod)); } }
        public bool EnablePlaguePopularityFix { get => fix; set { fix = value; SettingChanged?.Invoke(nameof(EnablePlaguePopularityFix)); } }
        public bool EnableClientFeatures { get => client; set { client = value; SettingChanged?.Invoke(nameof(EnableClientFeatures)); } }
        public bool ShowCountdownTimers { get => countdown; set { countdown = value; SettingChanged?.Invoke(nameof(ShowCountdownTimers)); } }
    }
    internal sealed class TimerCountdownViewModel
    {
        public static int Writes;
        public bool TrySetRemaining(string a, string b, out Exception failure) { Writes++; failure = null; return true; }
    }
}

namespace APIShared.GameModes { public static class GameModeHelper { public static Shared.Mode Capture() => new Shared.Mode(); } }
