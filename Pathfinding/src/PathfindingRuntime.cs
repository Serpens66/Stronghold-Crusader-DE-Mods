// Feature: Lifecycle orchestration of the Pathfinding features. The relative installation order
// follows the source mods (BugfixesAndQoL runtime, then the movement engine provider).
using BepInEx.Logging;
using Pathfinding.Engine;
using RedBird.Core.Memory;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace Pathfinding
{
    public sealed class PathfindingRuntime
    {
        private readonly ManualLogSource log;
        private readonly PathfindingViewModel settings;
        private readonly TroopMovementFixRuntime troopMovementFixRuntime;
        private readonly MultiplayerFeatureGate multiplayerFeatureGate;
        private readonly AssassinClimbRuntime assassinClimbRuntime;
        private readonly AssassinClimbCancellationRuntime assassinClimbCancellationRuntime;
        private readonly AssassinPathfindingRuntime assassinPathfindingRuntime;
        private readonly AssassinCombatResumeRuntime assassinCombatResumeRuntime;
        private readonly ReachableEnemyGatehouseRuntime reachableEnemyGatehouseRuntime;
        private readonly string pluginDirectory;
        private ExtendedShiftCommandQueueRuntime extendedShiftCommandQueueRuntime;
        private static ExtendedShiftCommandQueueRuntime processExtendedShiftCommandQueueRuntime;
        private UnitCommandPathRuntime commandRuntime;
        private static UnitCommandPathRuntime processUnitCommandPathRuntime;
        private AssemblyPointPlacementPatch assemblyPointPlacementPatch;
        private MountedStockpileMovementPatch mountedStockpileMovementPatch;
        private FastRecruitMovementBridge fastRecruitMovementBridge;
        private IDisposable mapStartSubscription, mapUnloadSubscription;
        private IntPtr libraryHandle;
        private ScanRegion nativeRegion;
        private bool nativeLibraryAvailable, fixedLayoutHashValidated;
        private bool assemblyPointPlacementPatchUnavailable, mountedStockpileMovementPatchUnavailable, fastRecruitInitializationAttempted;
        private ulong LibraryBase => unchecked((ulong)libraryHandle.ToInt64());

        public PathfindingRuntime(ManualLogSource log, PathfindingViewModel settings, string pluginDirectory)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.pluginDirectory = pluginDirectory;
            troopMovementFixRuntime = new TroopMovementFixRuntime(log, settings);
            multiplayerFeatureGate = new MultiplayerFeatureGate(log);
            assassinClimbRuntime = new AssassinClimbRuntime(log, settings, multiplayerFeatureGate);
            assassinClimbCancellationRuntime = new AssassinClimbCancellationRuntime(log, settings);
            assassinPathfindingRuntime = new AssassinPathfindingRuntime(log, settings, assassinClimbRuntime);
            // Rooted by the static field for the process; inactive until the runtime is installed.
            UnitCommands.UnitCommandPathRuntime.AssassinGroupRouteStager = assassinPathfindingRuntime.TryStageGroupRoute;
            AssassinPathfindingRuntime.MapTickSource = () => GameTimeManagerAPI.Instance.GetElapsedMapTicks();
            assassinCombatResumeRuntime = new AssassinCombatResumeRuntime(log, settings);
            reachableEnemyGatehouseRuntime = new ReachableEnemyGatehouseRuntime(log, settings);
            settings.SettingChanged += OnSettingChanged;
        }

        public void InitializeNetwork()
        {
            // This order is a multiplayer protocol boundary and must not depend on settings.
            TryPersistent("automatic gate closing", reachableEnemyGatehouseRuntime.Initialize);
            TryPersistent("Assassin climb-state synchronization", assassinClimbRuntime.InitializeNetwork);
            TryPersistent("Assassin climb-state save data", assassinClimbRuntime.InitializeSaveData);
            TryPersistent("troop movement synchronization save data", troopMovementFixRuntime.InitializeSaveData);
            TryPersistent("Assassin climb cancellation event", assassinClimbCancellationRuntime.Initialize);
            // APIShared owns the button, overflow placement and the single troop-action setup hook.
            TryPersistent("troop action HUD registration", () =>
            {
                assassinClimbRuntime.RegisterActionButton();
                if (!APIShared.Presentation.TroopActionHud.TryRegister(PathfindingPlugin.PluginGuid, "AssassinClimb",
                    assassinClimbRuntime.RefreshButtonVisibility, out string reason))
                    throw new InvalidOperationException(reason);
            });
            if (mapStartSubscription == null)
                TryPersistent("gameplay-session start subscription", () => mapStartSubscription =
                    Shared.GameplaySessionLifecycle.SubscribeStarted(log, BeginGameplaySession));
            if (mapUnloadSubscription == null)
                TryPersistent("gameplay-session end subscription", () => mapUnloadSubscription =
                    Shared.MissionEvents.Ended.Subscribe(_ => EndGameplaySession()));
        }

        private void EndGameplaySession()
        {
            assassinClimbRuntime.EndMap();
            assassinPathfindingRuntime.EndMap();
            multiplayerFeatureGate.Reset();
        }

        private void BeginGameplaySession(Shared.GameplaySessionStartedContext context)
        {
            multiplayerFeatureGate.CaptureMapMode(
                context.Notification != null && context.Notification.Context.IsSave && context.Mode.IsRealMultiplayer);
            assassinPathfindingRuntime.BeginMap();
            assassinClimbRuntime.BeginMap(context);
            TryApply("troop movement save restore", () => troopMovementFixRuntime.RestoreSessionState(context));
            APIShared.Presentation.TroopActionHud.RefreshCurrentPanel();
        }

        public void InitializeNative(CrusaderLibraryLoadContext context, bool isFixedLayoutHashValidated)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (nativeLibraryAvailable) return;
            IntPtr newLibraryHandle = context.ModuleHandle;
            ReadOnlySpan<byte> memory = context.Memory;
            if (newLibraryHandle == IntPtr.Zero || memory.Length == 0)
                throw new ArgumentException("The Crusader library is unavailable.");
            libraryHandle = newLibraryHandle;
            nativeRegion = context.Region;
            fixedLayoutHashValidated = isFixedLayoutHashValidated;
            nativeLibraryAvailable = true;
            assassinClimbCancellationRuntime.SetFixedUnitLayoutValidated(isFixedLayoutHashValidated);
            if (isFixedLayoutHashValidated)
            {
                try { assassinPathfindingRuntime.InitializeNative(newLibraryHandle, nativeRegion, memory, fixedLayoutHashValidated: true); }
                catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "PATHFINDING_ASSASSIN_SEARCH_INACTIVE: " + ex); }
                try
                {
                    // Independent of the weighted builder: with that option off, Vanilla's own
                    // Assassin builder receives the restored post-combat request.
                    assassinCombatResumeRuntime.InitializeNative(newLibraryHandle, nativeRegion, memory, fixedLayoutHashValidated: true);
                }
                catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "PATHFINDING_ASSASSIN_RESUME_INACTIVE: " + ex); }
            }
            else if (settings.EnableMod && (settings.EnableImprovedAssassinPathfinding || settings.EnableImprovedAssassinPathfindingAI || settings.EnableAssassinCombatResumeFix))
                Shared.DebugLogHelper.LogError(log, "PATHFINDING_ASSASSIN_INACTIVE: the fixed native layout is not validated for this CrusaderDE.dll; Vanilla stays active.");

            try { troopMovementFixRuntime.InitializeNative(newLibraryHandle, nativeRegion, memory, isFixedLayoutHashValidated); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "PATHFINDING_TROOP_MOVEMENT_INACTIVE: " + ex); }
            reachableEnemyGatehouseRuntime.SetNativeCompatibility(isFixedLayoutHashValidated);
            TryInitialize("unit commands", () => InitializeCommands(context, isFixedLayoutHashValidated));
            TryPersistent("Assassin gatehouse descent", () =>
                GateRouting.AssassinGatehouseDescentFeature.Initialize(context, log, settings, isFixedLayoutHashValidated));
            TryPersistent("Assassin gatehouse climb", ApplyAssassinGatehouseClimbFix);
            TryPersistent("enemy gate routing", () => GateRouting.GateRoutingFeature.Initialize(context, isFixedLayoutHashValidated));
            TryInitialize("Extended Shift command queue", () => InitializeExtendedShiftCommandQueue(context, isFixedLayoutHashValidated));
            TryInitialize("assembly-point placement fix", ApplyAssemblyPointPlacementPatchSetting);
            TryInitialize("mounted-stockpile movement fix", ApplyMountedStockpileMovementPatchSetting);
        }

        // Runs last, as the separate provider mod did: its hooks build on the command and queue owners.
        public void InstallEngine(CrusaderLibraryLoadContext context) => EngineHost.Install(context, log, settings, pluginDirectory);

        private void ApplyAssassinGatehouseClimbFix()
        {
            if (!nativeLibraryAvailable) return;
            APIShared.AssassinPathAPI.SetDirectGatehouseClimbing(
                PathfindingPlugin.PluginGuid, settings.EnableMod && settings.EnableAssassinGatehouseClimbFix);
            GateRouting.AssassinGatehouseDescentFeature.Refresh();
        }

        public void ApplySettings()
        {
            TryApply("Assassin gatehouse climb", ApplyAssassinGatehouseClimbFix);
            TryApply("fast recruit movement", ApplyFastRecruitRallyMovementSetting);
            TryApply("troop movement fix", troopMovementFixRuntime.ApplySetting);
            TryApply("Extended Shift command queue", () => extendedShiftCommandQueueRuntime?.ApplySetting());
            TryApply("assembly-point placement fix", ApplyAssemblyPointPlacementPatchSetting);
            TryApply("mounted-stockpile movement fix", ApplyMountedStockpileMovementPatchSetting);
            TryApply("Assassin path reconstruction", assassinPathfindingRuntime.ApplySetting);
            if (settings.EnableMod && settings.EnableImprovedAssassinPathfinding && assassinPathfindingRuntime.IsInstalled)
                TryApply("Assassin climb button", assassinClimbRuntime.Initialize);
            else
                TryApply("Assassin climb button cleanup", assassinClimbRuntime.Dispose);
        }

        private void InitializeCommands(CrusaderLibraryLoadContext context, bool referenceHashMatches)
        {
            if (!nativeLibraryAvailable || commandRuntime != null) return;
            if (processUnitCommandPathRuntime == null)
            {
#if SERP_DEVDIAG
                UnitCommandPathAPI.AssassinReconstructionRelaxation = () => AssassinPathfindingRuntime.TemporaryReconstructionRelaxation;
#endif
                processUnitCommandPathRuntime = UnitCommandPathAPI.RegisterCommands(
                    log, settings, context, referenceHashMatches, PathfindingPlugin.PluginGuid);
            }
            commandRuntime = processUnitCommandPathRuntime;
        }

        private void InitializeExtendedShiftCommandQueue(CrusaderLibraryLoadContext context, bool referenceHashMatches)
        {
            if (!nativeLibraryAvailable || extendedShiftCommandQueueRuntime != null) return;
            // Root the runtime before installing process-lifetime detours so a partial native
            // transaction can never leave callbacks whose managed owner became collectible.
            if (processExtendedShiftCommandQueueRuntime == null)
            {
                processExtendedShiftCommandQueueRuntime = new ExtendedShiftCommandQueueRuntime(log, settings, commandRuntime);
                processExtendedShiftCommandQueueRuntime.Install(context, referenceHashMatches);
            }
            extendedShiftCommandQueueRuntime = processExtendedShiftCommandQueueRuntime;
        }

        private void ApplyFastRecruitRallyMovementSetting()
        {
            if (!nativeLibraryAvailable) return;
            if (!settings.EnableMod || !settings.EnableFastRecruitRallyMovement)
            {
                fastRecruitMovementBridge?.SetEnabled(false);
                return;
            }
            if (fastRecruitMovementBridge != null) { fastRecruitMovementBridge.SetEnabled(true); return; }
            if (fastRecruitInitializationAttempted) return;
            fastRecruitInitializationAttempted = true;
            var bridge = new FastRecruitMovementBridge(log);
            if (bridge.IsActive) fastRecruitMovementBridge = bridge;
            else bridge.Dispose();
        }

        private void ApplyAssemblyPointPlacementPatchSetting()
        {
            if (!nativeLibraryAvailable) return;
            bool wanted = settings.EnableMod && settings.EnableAssemblyPointPlacementFix;
            if (assemblyPointPlacementPatch != null) { assemblyPointPlacementPatch.SetEnabled(wanted); return; }
            if (!wanted || assemblyPointPlacementPatchUnavailable) return;
            try { assemblyPointPlacementPatch = new AssemblyPointPlacementPatch(log, nativeRegion, nativeRegion.Span, LibraryBase, fixedLayoutHashValidated); }
            catch (Exception ex) { assemblyPointPlacementPatchUnavailable = true; Shared.DebugLogHelper.LogError(log, "PATHFINDING_ASSEMBLY_POINT_INACTIVE: " + ex); }
        }

        private void ApplyMountedStockpileMovementPatchSetting()
        {
            if (!nativeLibraryAvailable) return;
            bool wanted = settings.EnableMod && settings.EnableMountedStockpileMovementFix;
            if (mountedStockpileMovementPatch != null) { mountedStockpileMovementPatch.SetEnabled(wanted); return; }
            if (!wanted || mountedStockpileMovementPatchUnavailable) return;
            try { mountedStockpileMovementPatch = new MountedStockpileMovementPatch(log, nativeRegion, nativeRegion.Span, LibraryBase, fixedLayoutHashValidated); }
            catch (Exception ex) { mountedStockpileMovementPatchUnavailable = true; Shared.DebugLogHelper.LogError(log, "PATHFINDING_MOUNTED_INACTIVE: " + ex); }
        }

        private void OnSettingChanged(string propertyName)
        {
            if (propertyName == nameof(PathfindingViewModel.EnableTroopMovementFix))
            {
                TryApply("troop movement fix", troopMovementFixRuntime.ApplySetting);
                return;
            }
            ApplySettings();
        }

        private void TryPersistent(string name, Action action) => RunIsolated("PersistentInitialize", name, action,
            $"Pathfinding persistent feature '{name}' could not be initialized; independent features continue");
        private void TryInitialize(string name, Action action) => RunIsolated("FeatureInitialize", name, action,
            $"Pathfinding feature '{name}' could not be initialized and remains inactive");
        private void TryApply(string name, Action action) => RunIsolated("FeatureApply", name, action,
            $"Pathfinding feature '{name}' could not apply its setting; independent features continue");

        // One feature's failure must never stop the features after it.
        private void RunIsolated(string category, string name, Action action, string failure)
        {
            using (Shared.CrashBreadcrumbScope diagnostic = Shared.CrashBreadcrumbDiagnostics.IsEnabled
                ? Shared.CrashBreadcrumbDiagnostics.Enter(category, name.GetHashCode()) : default(Shared.CrashBreadcrumbScope))
            {
                try { action(); diagnostic.Complete(); }
                catch (Exception ex) { diagnostic.Complete(-1); Shared.RuntimeHealth.Failure(log, name, true, $"{failure}: {ex}"); }
            }
        }
    }
}
