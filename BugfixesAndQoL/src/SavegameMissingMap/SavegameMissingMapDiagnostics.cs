#if SERP_DEVDIAG
using System;
using APIShared;
using BepInEx.Logging;
using SHCDESE.API;

namespace BugfixesAndQoL.SavegameMissingMap
{
    // Temporary acceptance instrumentation. No functional decision depends on this file.
    internal static class SavegameMissingMapDiagnostics
    {
        private static ManualLogSource log;
        private static IMissionLifecycleCapability lifecycle;
        private static volatile bool tickPending;
        internal static void Register(ManualLogSource logger)
        {
            if (!Shared.DebugLogHelper.DiagnosticsEnabled) return;
            log = logger;
            SavegameFix.Log = Write;
            SavegameFix.HookDiagnostic = method => Write("HOOK_ACTIVE: " + method.DeclaringType.FullName + "::" + method.Name);
            SavegameFix.RecoveryDiagnostic = (name, slots) => Write("RECOVERED map=" + name + "; slots=" + slots + "; originalMapAvailable=false");
            SavegameFix.RestartDiagnostic = () => Write("RESTART_BLOCKED: original map is not installed; current save remains active.");
            if (!ApiShared.Current.TryGetMissionLifecycle("BugfixesAndQoL_Serp", out lifecycle, out var diagnostic) ||
                !lifecycle.TryRegisterObserver("save-data-diagnostics", OnStart, null, null, out diagnostic))
            {
                Write("DIAGNOSTICS_UNAVAILABLE: " + diagnostic?.Reason);
                return;
            }
            GameTimeManagerAPI.Instance.OnTick += OnTick;
        }
        private static void Write(string message) => RuntimeStartup.Report<string>(text =>
            Shared.DebugLogHelper.LogInfo(log, "SavegameMissingMap: " + text), message);
        private static void OnStart(MissionLifecycleNotification notification)
        {
            Write("MISSION_STARTED: session=" + notification.Context.SessionId + "; kind=" + notification.Context.StartKind);
            tickPending = true;
        }
        private static void OnTick(int tick)
        {
            if (!tickPending) return;
            tickPending = false;
            Write("RUNTIME_TICK: tick=" + tick + "; active=" + SavegameFix.Enabled);
        }
    }
}
#endif
