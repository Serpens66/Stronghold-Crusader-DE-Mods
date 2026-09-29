using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using System;

namespace RaidRetargetDiagnostic
{
    [BepInDependency("000shcdese", "2.11.0")]
    [BepInDependency("APIShared_Serp", "0.4.2")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, Name, Version)]
    public sealed class RaidRetargetDiagnosticPlugin : BaseUnityPlugin
    {
        public const string Guid = "RaidRetargetDiagnostic_Serp";
        public const string Name = "Raid Retarget Diagnostic";
        public const string Version = "0.1.0";

        private static ManualLogSource log;
        private static RaidRetargetDiagnosticRuntime runtime;
        private static IDisposable sessionSubscription;
        private static IDisposable damageSubscription;
        private static IDisposable deleteSubscription;
        private static IDisposable tribeOrderSubscription;
        private static IDisposable tribeMoveSubscription;
        private static IDisposable unitMoveSubscription;
        private static bool libraryRegistered;

        private void Awake()
        {
            log = Logger;
            Shared.DebugLogHelper.LogInfo(log, $"{Name} {Version} loaded; waiting for native library.");
            if (libraryRegistered) return;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            libraryRegistered = true;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            if (!Shared.DebugLogHelper.ReportNativeLibraryVersion(log, Name, requireCurrentVersion: true))
                return;

            IDisposable candidateSession = null;
            IDisposable candidateDamage = null;
            IDisposable candidateDelete = null;
            IDisposable candidateTribeOrder = null;
            IDisposable candidateTribeMove = null;
            IDisposable candidateUnitMove = null;
            bool candidateTick = false;
            try
            {
                var candidate = new RaidRetargetDiagnosticRuntime(log,
                    Chainloader.PluginInfos.ContainsKey("fixes"));
                candidateSession = Shared.GameplaySessionLifecycle.SubscribeStarted(
                    log, candidate.OnSessionStarted, candidate.OnSessionEnded);
                candidateDamage = BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable
                    .Subscribe(candidate.OnBuildingDamage);
                candidateDelete = BuildingR3EventHooks.OnBuildingDelete.Observable
                    .Subscribe(candidate.OnBuildingDelete);
                candidateTribeOrder = TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable
                    .Subscribe(candidate.OnTribeOrder);
                candidateTribeMove = TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable
                    .Subscribe(candidate.OnTribeMove);
                candidateUnitMove = UnitR3EventHooks.OnUnitMoveHere.Observable
                    .Subscribe(candidate.OnUnitMove);
                GameTimeManagerAPI.Instance.OnTick += OnTick;
                candidateTick = true;

                Shared.DebugLogHelper.LogInfo(log,
                    "RAID_DIAG_READY: raidMeleeRetarget=true, publisher=GameTimeManagerAPI.OnTick, " +
                    "events=buildingDamage+delete+tribeOrder+tribeMove+unitMove.");
                // The extender publishers and these static fields survive startup cleanup.
                sessionSubscription = candidateSession;
                damageSubscription = candidateDamage;
                deleteSubscription = candidateDelete;
                tribeOrderSubscription = candidateTribeOrder;
                tribeMoveSubscription = candidateTribeMove;
                unitMoveSubscription = candidateUnitMove;
                runtime = candidate;
            }
            catch (Exception ex)
            {
                if (candidateTick) GameTimeManagerAPI.Instance.OnTick -= OnTick;
                candidateUnitMove?.Dispose();
                candidateTribeMove?.Dispose();
                candidateTribeOrder?.Dispose();
                candidateDelete?.Dispose();
                candidateDamage?.Dispose();
                candidateSession?.Dispose(); // Rollback only before runtime publication.
                Shared.DebugLogHelper.LogError(log, "Raid diagnostic initialization failed: " + ex);
            }
        }

        private static void OnTick(int tick) => runtime?.OnTick(tick);
    }
}
