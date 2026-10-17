using System;
using System.Collections.Generic;
using APIShared;
using BepInEx;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.API.LowLevel;

namespace SavegameMissingMapFixTest
{
    [BepInPlugin(Guid, "Savegame Missing Map Fix Test", "0.1.0")]
    [BepInDependency("000shcdese")]
    [BepInDependency("APIShared_Serp", "0.9.2")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "SavegameMissingMapFixTest_Serp";
        private static readonly List<ILHook> hooks = new List<ILHook>();
        private static ManualLogSource log;
        private static readonly RuntimeStartup startup = new RuntimeStartup();
        private static volatile bool tickPending;
        private static IMissionLifecycleCapability lifecycle;
        private void Awake()
        {
            log = Logger;
            SavegameFix.Log = Write;
            startup.Subscribe(callback => CrusaderLibrary.Instance.LibraryLoaded += context => callback(),
                Initialize, active => SavegameFix.Enabled = active, Write,
                ex => log.LogError("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] FIX_DISABLED: " + ex));
        }
        private static void Write(string message) => log.LogInfo("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + message);
        private static void Initialize()
        {
            Write("ASSEMBLY_VALIDATED: " + RuntimeStartup.ValidateGameAssembly(Paths.ManagedPath));
            SavegameFix.ValidateMembers();
            SavegameFix.RunHookPlan(SavegameFix.HookTargets, SavegameFix.HookTransforms, () =>
            {
                if (!ApiShared.Current.TryGetMissionLifecycle(Guid, out lifecycle, out var diagnostic) ||
                    !lifecycle.TryRegisterObserver("save-data-diagnostics", OnStart, null, null, out diagnostic))
                    throw new InvalidOperationException("Mission lifecycle unavailable: " + diagnostic?.Reason);
                GameTimeManagerAPI.Instance.OnTick += OnTick;
            }, (method, transform) =>
            {
                hooks.Add(new ILHook(method, transform));
                Write("HOOK_ACTIVE: " + method.DeclaringType.FullName + "::" + method.Name);
            });
        }
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
