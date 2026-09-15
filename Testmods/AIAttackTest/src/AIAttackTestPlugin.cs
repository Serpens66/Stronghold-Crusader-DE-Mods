using BepInEx;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using System;

namespace AIAttackTest
{
    [BepInDependency(ScriptExtenderGuid, "2.6.0")]
    [BepInDependency("BugfixesAndQoL_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class AIAttackTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        public const string PluginGuid = "AIAttackTest_Serp";
        public const string PluginName = "AI Attack Test";
        public const string PluginVersion = "0.1.0";

        private static ManualLogSource persistentLog;
        private static AIAttackTestSettings settings;
        private static AIAttackTestRuntime runtime;
        private static IDisposable sessionStartedSubscription;
        private static IDisposable mapUnloadSubscription;
        private static bool librarySubscriptionInstalled;
        private static bool tickSubscriptionInstalled;

        private void Awake()
        {
            persistentLog = Logger;
            settings ??= new AIAttackTestSettings();

            try
            {
                GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(
                    this,
                    PluginName,
                    settings,
                    "ScriptExtenderUI/AIAttackTestSettings.xaml");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    persistentLog,
                    $"{PluginName} settings registration failed; gameplay changes remain unavailable: {ex}");
                return;
            }

            Shared.DebugLogHelper.LogInfo(
                persistentLog,
                $"{PluginName} {PluginVersion} loaded; synchronized settings apply on the next gameplay session.");

            if (librarySubscriptionInstalled)
                return;

            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
            librarySubscriptionInstalled = true;
        }

        private static void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null)
                return;

            AIAttackTestRuntime candidate = null;
            IDisposable candidateSessionSubscription = null;
            IDisposable candidateUnloadSubscription = null;
            try
            {
                bool hashMatches = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                    persistentLog,
                    PluginName,
                    requireCurrentVersion: true);
                if (!hashMatches)
                    return;

                candidate = new AIAttackTestRuntime(persistentLog, settings, context, hashMatches);
                AIAttackTestRuntime installed = candidate;
                candidateSessionSubscription = Shared.GameplaySessionLifecycle.SubscribeStarted(
                    persistentLog,
                    installed.BeginMap);
                candidateUnloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(_ => installed.EndMap("OnUnloadMap(Pre)"));
                if (candidateSessionSubscription == null || candidateUnloadSubscription == null)
                    throw new InvalidOperationException("Persistent map subscriptions could not be created.");

                sessionStartedSubscription = candidateSessionSubscription;
                candidateSessionSubscription = null;
                mapUnloadSubscription = candidateUnloadSubscription;
                candidateUnloadSubscription = null;
                runtime = candidate;
                candidate = null;
                if (!tickSubscriptionInstalled)
                {
                    GameTimeManagerAPI.Instance.OnTick += OnGameTick;
                    tickSubscriptionInstalled = true;
                }
                Shared.DebugLogHelper.LogInfo(
                    persistentLog,
                    $"{PluginName} native contracts validated; map-scoped runtime is ready.");
            }
            catch (Exception ex)
            {
                try
                {
                    candidateSessionSubscription?.Dispose();
                    candidateUnloadSubscription?.Dispose();
                    candidate?.RollbackUnpublishedInitialization();
                }
                catch (Exception rollbackError)
                {
                    ex = new AggregateException(ex, rollbackError);
                }
                Shared.DebugLogHelper.LogError(
                    persistentLog,
                    $"{PluginName} initialization failed; Vanilla behavior remains active: {ex}");
            }
        }

        private static void OnGameTick(int tick) => runtime?.OnGameTick(tick);
    }
}
