using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;

namespace BugfixesAndQoL.SavegameMissingMap
{
    internal static class SavegameMissingMapFeature
    {
        internal const string LegacyGuid = "SavegameMissingMapFixTest_Serp";
        private static readonly RuntimeStartup startup = new RuntimeStartup();
        private static readonly List<ILHook> hooks = new List<ILHook>();
        private static bool attempted;
        private static ManualLogSource log;

        // Called by the existing runtime LibraryLoaded path, never by a second plugin.
        internal static void Initialize(ManualLogSource logger, Func<bool> recoveryEnabled)
        {
            if (attempted) return;
            attempted = true;
            log = logger;
            InitializeUnlessLegacy(Chainloader.PluginInfos.ContainsKey(LegacyGuid), () =>
            {
                SavegameFix.RecoveryEnabled = recoveryEnabled;
#if SERP_DEVDIAG
                // Acceptance observation belongs exclusively to the dedicated diagnostic build.
                RuntimeStartup.Report<Action>(register => register(), () => SavegameMissingMapDiagnostics.Register(log));
#endif
                startup.Subscribe(callback => callback(), Install, active => SavegameFix.Enabled = active,
                    message => RuntimeStartup.Report(SavegameFix.Log, message),
                    WriteError);
            }, Write);
        }

        internal static void InitializeUnlessLegacy(bool legacyPresent, Action initialize, Action<string> report)
        {
            if (legacyPresent)
            {
                RuntimeStartup.Report(report, "SKIPPED: " + LegacyGuid + " is loaded; remove it before using the integrated savegame fix.");
                return;
            }
            initialize();
        }

        private static void Install()
        {
            // The code contract of the loaded methods is the gate (members, IL preflight and a compiled copy),
            // not a hash of Assembly-CSharp.dll, so an update that keeps these methods keeps the fix.
            SavegameFix.ValidateMembers();
            SavegameFix.RunHookPlan(SavegameFix.HookTargets, SavegameFix.HookTransforms, () => { }, (method, transform) =>
            {
                hooks.Add(new ILHook(method, transform));
                RuntimeStartup.Report(SavegameFix.HookDiagnostic, method);
            });
        }

        private static void WriteError(Exception error) => RuntimeStartup.Report<Exception>(ex =>
            log.LogError("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] SavegameMissingMap FIX_DISABLED: " + ex), error);

        private static void Write(string message) => RuntimeStartup.Report<string>(text =>
            log.LogWarning("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] SavegameMissingMap: " + text), message);
    }
}
