// Only the external API/plugin/lifecycle/viewmodel boundaries are doubled. The actual
// BFQoL contributor and policy are source-linked; no APIShared build or native state is touched.
using System;
using System.Collections.Generic;
namespace APIShared
{
    public enum NativeCapabilityState { Available, ValidationFailed }
    public sealed class NativeCapabilityDiagnostic
    {
        public NativeCapabilityState State = NativeCapabilityState.Available;
        public string Reason = "test source ready";
    }
    public sealed class MissionContext { public long SessionId; }
    public interface IMissionLifecycleCapability { MissionContext Current { get; } }
    public sealed class TestMissions : IMissionLifecycleCapability { public MissionContext Context; public MissionContext Current => Context; }
    public sealed class TestClient
    {
        public bool TryGetMissionLifecycle(out IMissionLifecycleCapability capability, out NativeCapabilityDiagnostic diagnostic)
        { capability = ApiShared.Missions; diagnostic = new NativeCapabilityDiagnostic(); return true; }
    }
    public static class ApiShared
    {
        public static readonly TestMissions Missions = new TestMissions();
        public static TestClient ForMod(string owner) => new TestClient();
    }
}
namespace APIShared.Buildings
{
    public enum EnemyProximityPhase { Override, Limit }
    public readonly struct EnemyProximityContext { }
    public readonly struct EnemyProximityDecision
    {
        public readonly int? CivilianRadius, MilitaryRadius;
        public readonly bool Block;
        public EnemyProximityDecision(int? civilianRadius = null, int? militaryRadius = null, bool block = false)
        { CivilianRadius = civilianRadius; MilitaryRadius = militaryRadius; Block = block; }
        public static EnemyProximityDecision None => default;
    }
    public sealed class EnemyProximityRegistration
    {
        public bool Enabled;
        public void SetEnabled(bool enabled) => Enabled = enabled;
    }
    public static class EnemyProximityPolicies
    {
        public static readonly APIShared.NativeCapabilityDiagnostic QueryDiagnostic = new APIShared.NativeCapabilityDiagnostic();
        public static EnemyProximityRegistration Registration;
        public static Func<EnemyProximityContext, EnemyProximityDecision> Callback;
        public static bool ExtraRegistered;
        public static bool HasRegistration(string owner, string id) => owner == "ExtraFeatures_Serp" && id == "enemy-proximity" && ExtraRegistered;
        public static bool TryRegister(string owner, string id, EnemyProximityPhase phase,
            Func<EnemyProximityContext, EnemyProximityDecision> callback, out EnemyProximityRegistration registration,
            out string error, int order = 0)
        {
            if (owner != "BugfixesAndQoL_Serp" || id != "peace-time-civilian-proximity" || phase != EnemyProximityPhase.Limit)
                throw new Exception("Wrong contributor identity or stage");
            Callback = callback; Registration = registration = new EnemyProximityRegistration(); error = null; return true;
        }
    }
}
namespace APIShared.Simulation
{
    public static class PeaceTimeState
    {
        public static readonly APIShared.NativeCapabilityDiagnostic Diagnostic = new APIShared.NativeCapabilityDiagnostic();
        public static bool Available, Active;
        public static int Reads;
        public static bool TryGetActive(out bool active) { Reads++; active = Active; return Available; }
    }
}
namespace BepInEx.Bootstrap
{
    public static class Chainloader { public static readonly Dictionary<string, object> PluginInfos = new Dictionary<string, object>(); }
}
namespace Shared
{
    internal static partial class DebugLogHelper
    {
        internal static int PeaceWarnings;
        internal static void LogWarning(object log, string text) { PeaceWarnings++; }
    }
    internal sealed class GameplaySessionStartedContext { internal long SessionId; }
    internal static class GameplaySessionLifecycle
    {
        internal static Action<GameplaySessionStartedContext> Started;
        internal static Action Ended;
        internal static IDisposable SubscribeStarted(BepInEx.Logging.ManualLogSource log,
            Action<GameplaySessionStartedContext> started, Action onEnded = null)
        { Started = started; Ended = onEnded; return new Token(); }
        private sealed class Token : IDisposable { public void Dispose() { } }
    }
}
namespace BugfixesAndQoL
{
    internal static class BugfixesAndQoLPlugin { public const string PluginGuid = "BugfixesAndQoL_Serp"; }
    internal sealed partial class BugfixesAndQoLViewModel
    {
        public bool EnableMod = true;
        public bool EnablePeaceTimeCivilianProximityReduction = true;
        public bool EnablePeaceTimeGameplayFixes;
        public bool Locked;
        public event Action<string> SettingChanged;
        internal void Change(string name) => SettingChanged?.Invoke(name);
        internal void SetPeaceTimeCivilianMissionLocked(bool locked) { Locked = locked; }
    }
}
