// Feature: Plugin bootstrap of the Pathfinding mod.
using BepInEx;
using SHCDESE.API.LowLevel;
using System;

namespace Pathfinding
{
    [BepInDependency(ScriptExtenderGuid, "2.14.3")]
    [BepInDependency(ApiSharedGuid, "0.46.0")]
    [BepInDependency("ExtraFeatures_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    // BugfixesAndQoL loads first, as its hooks did when these features still lived there.
    [BepInDependency("BugfixesAndQoL_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    // MoatMove is a retired provider of the old command runtime and cannot run against this mod.
    [BepInIncompatibility("MoatMove_Serp")]
    [BepInIncompatibility("TroopMovementFix_Serp")]
    [BepInIncompatibility("TroopMovementFix2_Serp")]
    [BepInIncompatibility("TroopMovementFix3_Serp")]
    [BepInIncompatibility("MoveMoatTest_Serp")]
    [BepInIncompatibility("QueueTest_Serp")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed partial class PathfindingPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string ApiSharedGuid = "APIShared_Serp";

        public const string PluginGuid = "Pathfinding_Serp";
        public const string PluginName = "Pathfinding";
        public const string PluginVersion = "0.1.2";

        // Static roots: the plugin component itself is destroyed by the SHCDE startup cleanup.
        private static PathfindingPlugin processRoot;
        private static PathfindingRuntime runtime;
        private static bool initialized;

        public PathfindingViewModel Settings { get; private set; }

        private void Awake()
        {
            if (initialized) return;
            initialized = true;
            processRoot = this;
            Shared.DebugLogHelper.InitializeDiagnostics(Config, Logger);
            Shared.DevDiag.Attach(Logger);
            Shared.UnityMainThreadDispatch.InitializeForCurrentThread();
            Shared.CrashBreadcrumbDiagnostics.Initialize(Logger, PluginGuid, PluginName, PluginVersion);
            Settings = new PathfindingViewModel();
            ImportLegacyFormationConfig();
            FormationFeature.Configure(Logger, Config, Settings);
            GateRouting.GateRoutingFeature.Configure(Logger, Config, Settings);
            runtime = new PathfindingRuntime(Logger, Settings, System.IO.Path.GetDirectoryName(Info.Location));
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
            Shared.DebugLogHelper.LogInfo(Logger, $"{PluginName} {PluginVersion} loaded; awaiting native library.");
            ReportOutdatedBugfixesAndQoL();
        }

        // BugfixesAndQoL up to 1.0.214 still contains every feature of this mod. Both copies would then
        // install the same hooks and HUD buttons; only an error is reported (SerpsModsHost shows it in game).
        private static readonly Version FirstBugfixesWithoutPathfinding = new Version(1, 0, 215);

        private void ReportOutdatedBugfixesAndQoL()
        {
            if (!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("BugfixesAndQoL_Serp", out PluginInfo bugfixes) ||
                bugfixes?.Metadata == null || bugfixes.Metadata.Version >= FirstBugfixesWithoutPathfinding)
                return;
            Shared.DebugLogHelper.LogError(Logger,
                $"PATHFINDING_OUTDATED_BUGFIXES: BugfixesAndQoL {bugfixes.Metadata.Version} is installed. " +
                $"Its unit commands, gate routing, Assassin climbing, formations, command queue and troop movement now " +
                $"belong to Pathfinding and run twice with this version. Update BugfixesAndQoL to " +
                $"{FirstBugfixesWithoutPathfinding} or newer.");
        }

        private void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            using (Shared.CrashBreadcrumbScope diagnostic = Shared.CrashBreadcrumbDiagnostics.Enter("LibraryLoaded"))
            {
                // Initial native installation window (see PathfindingHookInfrastructure.DetourBackend).
                HookInstallPhase.InitialLibraryLoad = true;
                bool completed;
                try { completed = InitializeLibraryRuntime(context); }
                finally { HookInstallPhase.InitialLibraryLoad = false; }
                if (completed) diagnostic.Complete();
            }
        }

        // Runs inside the initial native installation window; false = stopped fail-closed.
        private bool InitializeLibraryRuntime(CrusaderLibraryLoadContext context)
        {
            // Packet types are registered before settings can vary.
            try { runtime.InitializeNetwork(); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(Logger, "PATHFINDING_NETWORK_INIT_FAILED: " + ex); }
            try { PrepareLegacySettings(); }
            catch (Exception ex) { LogMigrationError(ex); }
            try
            {
                // Registration can persist defaults. Preserve the pre-registration migration intent first.
                if (legacySettings == null || !legacySettings.RegistrationSafe)
                    throw new InvalidOperationException("Settings registration deferred: the initial settings/migration journal could not be read or saved safely. Retry after fixing the logged file error.");
                APIShared.ModSettings.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Shared.DebugLogHelper.ForExternalCode(Logger),
                    PluginGuid,
                    Settings,
                    "ScriptExtenderUI/PathfindingSettings.xaml",
                    logRoutineActivity: Shared.DebugLogHelper.DiagnosticsEnabled);
                settingsRegistered = true;
                TryMigrateLegacySettings();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, "PATHFINDING_SETTINGS_REGISTRATION_FAILED: gameplay runtime stopped fail-closed; " + ex);
                return false;
            }
            try { runtime.InitializeNative(context, Shared.DebugLogHelper.IsCurrentNativeLibraryVersion()); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(Logger, "PATHFINDING_NATIVE_INIT_FAILED: unaffected features may continue; " + ex); }
            FormationFeature.Initialize(context);
            try { runtime.InstallEngine(context); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(Logger, "PATHFINDING_ENGINE_INIT_FAILED: Vanilla routes stay active; " + ex); }
            try { runtime.ApplySettings(); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(Logger, "PATHFINDING_SETTINGS_APPLY_FAILED: " + ex); }
            // Results of a one-time native fallback (unknown game build) are reused on the next start.
            PathfindingNativeCache.Save();
            return true;
        }
    }
}
