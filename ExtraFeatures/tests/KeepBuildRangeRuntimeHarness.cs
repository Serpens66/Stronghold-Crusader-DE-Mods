// Test-only publishers and settings; production runtime is compiled unchanged beside these.
namespace APIShared
{
    public sealed class MissionLifecycleNotification { public string Phase; }
}
namespace BepInEx.Logging { public sealed class ManualLogSource { } }
namespace R3
{
    public sealed class TestEvent
    {
        private event System.Action<APIShared.MissionLifecycleNotification> handlers;
        public System.IDisposable Subscribe(System.Action<APIShared.MissionLifecycleNotification> callback)
        { handlers += callback; return new Registration(); }
        public void Publish(string phase) { if (handlers != null) handlers(new APIShared.MissionLifecycleNotification { Phase = phase }); }
        private sealed class Registration : System.IDisposable { public void Dispose() { } }
    }
}
namespace Shared
{
    public static class MissionEvents
    {
        public static readonly R3.TestEvent Initialization = new R3.TestEvent();
        public static readonly R3.TestEvent Ended = new R3.TestEvent();
        public static void SetOwner(string owner) { }
    }
    public static class GameplayModActivationGate
    {
        public static bool Allowed;
        public static bool IsEnabled(bool enabled) { return Allowed && enabled; }
    }
    public static class DebugLogHelper
    {
        public static int DebugCalls, ErrorCalls;
        public static bool ThrowDebug, ThrowError;
        public static void LogDebug(BepInEx.Logging.ManualLogSource log, string text)
        {
            DebugCalls++;
            if (ThrowDebug) throw new System.Exception("Injected debug logger failure");
        }
        public static void LogError(BepInEx.Logging.ManualLogSource log, string text)
        {
            ErrorCalls++;
            if (ThrowError) throw new System.Exception("Injected error logger failure");
        }
    }
}
namespace SHCDESE.API
{
    public sealed class GameBuildingManagerAPI
    {
        public static readonly GameBuildingManagerAPI Instance = new GameBuildingManagerAPI();
        public readonly FaultInjectingValue KeepProximityOverride = new FaultInjectingValue();
    }
    public sealed class FaultInjectingValue
    {
        private readonly RedBird.Core.Memory.Managed.ManagedValue<int> value = new RedBird.Core.Memory.Managed.ManagedValue<int>(-1);
        public bool FailRead, FailWrite;
        public int ReadAttempts, WriteAttempts;
        public int GetValue()
        {
            ReadAttempts++;
            if (FailRead) throw new System.Exception("Injected read failure");
            return value.GetValue();
        }
        public void SetValue(int next)
        {
            WriteAttempts++;
            if (FailWrite) throw new System.Exception("Injected write failure");
            value.SetValue(next);
        }
        public void ClearOverrides() { value.ClearOverrides(); }
    }
}
namespace ExtraFeatures
{
    public sealed class ExtraFeaturesViewModel { public bool EnableMod = true; public int KeepBuildRange = -1; }
    public static class ExtraFeaturesPlugin { public const string PluginGuid = "KeepRangeTest"; }
    public static class KeepBuildRangeRuntimeTests
    {
        private static void Expect(int expected, string context)
        {
            if (SHCDESE.API.GameBuildingManagerAPI.Instance.KeepProximityOverride.GetValue() != expected)
                throw new System.Exception(context);
        }
        public static void Run()
        {
            var settings = new ExtraFeaturesViewModel();
            var runtime = new KeepBuildRangeRuntime(new BepInEx.Logging.ManualLogSource(), settings);
            var value = SHCDESE.API.GameBuildingManagerAPI.Instance.KeepProximityOverride;
            runtime.Initialize();
            runtime.Initialize(); // Idempotent registration.
            Expect(-1, "default in menu");
            settings.KeepBuildRange = 100;
            runtime.Refresh();
            Expect(-1, "blocked mode");
            Shared.GameplayModActivationGate.Allowed = true; // Publisher updates gate first.
            Shared.DebugLogHelper.ThrowDebug = true;
            Shared.MissionEvents.Initialization.Publish("BeforeNativeStart");
            Shared.DebugLogHelper.ThrowDebug = false;
            Expect(100, "range must be ready before prebuilt castles");
            settings.KeepBuildRange = 150;
            runtime.Refresh();
            Expect(150, "synchronized preset/setting refresh");
            settings.EnableMod = false;
            runtime.Refresh();
            Expect(-1, "mod disable");
            settings.EnableMod = true;
            Shared.MissionEvents.Initialization.Publish("NativeLoaded");
            Expect(150, "save restoration without new-game phase");
            Shared.GameplayModActivationGate.Allowed = false;
            Shared.MissionEvents.Ended.Publish("End");
            Expect(-1, "mission end releases override");
            value.ClearOverrides();
            Shared.GameplayModActivationGate.Allowed = true;
            Shared.MissionEvents.Initialization.Publish("BeforeLoad");
            Expect(150, "next map reacquires override");
            settings.KeepBuildRange = 0;
            runtime.Refresh();
            Expect(-1, "zero releases during mission");
            Check(Shared.DebugLogHelper.ErrorCalls == 0, "normal settings must not log errors");
            Check(Shared.DebugLogHelper.DebugCalls == 1, "lifecycle marker attempted once even if logger throws");

            settings.KeepBuildRange = 100;
            value.FailRead = true;
            Shared.DebugLogHelper.ThrowError = true;
            int reads = value.ReadAttempts;
            runtime.Refresh();
            Shared.MissionEvents.Initialization.Publish("NativeLoaded");
            Check(value.ReadAttempts == reads + 2, "failed reads keep retrying");
            Check(Shared.DebugLogHelper.ErrorCalls == 1, "one error attempt despite throwing logger");
            value.FailRead = false;
            Shared.DebugLogHelper.ThrowError = false;
            runtime.Refresh();
            Expect(100, "read recovery applies override");
            Check(Shared.DebugLogHelper.ErrorCalls == 1, "recovery is silent");

            settings.KeepBuildRange = 200;
            value.FailWrite = true;
            int writes = value.WriteAttempts;
            runtime.Refresh();
            runtime.Refresh();
            Check(value.WriteAttempts == writes + 2, "failed positive writes keep retrying");
            Check(Shared.DebugLogHelper.ErrorCalls == 2, "new failure episode reported once");
            Expect(100, "failed write preserves current override");
            value.FailWrite = false;
            runtime.Refresh();
            Expect(200, "positive write recovery");

            settings.KeepBuildRange = -1;
            value.FailWrite = true;
            writes = value.WriteAttempts;
            runtime.Refresh();
            Shared.MissionEvents.Ended.Publish("End");
            Check(value.WriteAttempts == writes + 2, "restoration retries after failure");
            Check(Shared.DebugLogHelper.ErrorCalls == 3, "restoration failure reported once");
            Expect(200, "failed restoration preserves ownership");
            value.FailWrite = false;
            runtime.Refresh();
            Expect(-1, "retry restores original predecessor");
            runtime.Refresh();
            Check(Shared.DebugLogHelper.ErrorCalls == 3 && Shared.DebugLogHelper.DebugCalls == 1,
                "successful retries and settings remain silent");
            System.Console.WriteLine("PASS: Production range runtime: lifecycle, settings, bounded errors, recovery, restoration retries and throwing loggers.");
        }
        private static void Check(bool condition, string context)
        {
            if (!condition) throw new System.Exception(context);
        }
    }
}
