using APIShared;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LordSpawnSlotFixTest
{
    internal sealed unsafe class LordSpawnSlotFixTestRuntime
    {
        private const int DetailedSnapshotTicks = 3;
        private const int VanillaLordWindowStart = 94;
        private const int VanillaLordWindowEnd = 96;
        private const int ConfirmationTimeoutTicks = 180;
        private const int ObservationStopTicks = 240;

        private readonly ManualLogSource log;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly LordSpawnSlotSessionState sessionState = new LordSpawnSlotSessionState();
        private readonly Dictionary<int, string> lastSnapshots = new Dictionary<int, string>();
        private bool mapActive;
        private bool sessionEligible;
        private int observationTick;

        internal LordSpawnSlotFixTestRuntime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        internal void Install()
        {
            Shared.MissionEvents.SetOwner(LordSpawnSlotFixTestPluginGuid);
            subscriptions.Add(Shared.MissionEvents.Initialization.Subscribe(OnInitialization));
            subscriptions.Add(Shared.MissionEvents.Ended.Subscribe(OnMissionEnded));
            GameTimeManagerAPI.Instance.OnTick += OnGameTick;
            log.LogInfo(
                "LSS_TEST_INSTALL: correctionThread=GameTimeManagerAPI.OnTick; nativeHooks=0; " +
                "modeGate=none; eligibleLifecycle=NewGame; saves=false; editor=false; " +
                "playerScan=native records 1-8; gameMembers=optional kicked veto only.");
        }

        private const string LordSpawnSlotFixTestPluginGuid = "LordSpawnSlotFixTest_Serp";

        private void OnInitialization(MissionLifecycleNotification notification)
        {
            try
            {
                if (sessionState.SessionId != notification.Context.SessionId)
                {
                    sessionState.Reset(notification.Context.SessionId);
                    mapActive = false;
                    sessionEligible = false;
                    observationTick = 0;
                    lastSnapshots.Clear();
                    log.LogInfo(
                        $"LSS_TEST_SESSION_RESET: session={notification.Context.SessionId}; " +
                        $"startKind={notification.Context.StartKind}; mode={notification.Context.Mode.Kind}; " +
                        $"isSave={notification.Context.IsSave}; isEditor={notification.Context.IsEditor}.");
                }

                if (notification.Phase == MissionInitializationPhase.BeforeLoad ||
                    notification.Phase == MissionInitializationPhase.BeforeNativeStart)
                {
                    mapActive = false;
                    sessionEligible = false;
                    return;
                }

                if (notification.Phase != MissionInitializationPhase.AfterNativeStart &&
                    notification.Phase != MissionInitializationPhase.NativeLoaded)
                    return;

                if (mapActive)
                    return;

                // Mode classification is diagnostic-only. The faulty remap was proven in a
                // CustomGame, but neither its exact tombstone nor Vanilla's later Lord creation
                // depends on APIShared assigning that mode label. NewGame is the safety boundary:
                // saves restore authoritative identities without promising another Lord spawn,
                // while editor sessions do not have the normal gameplay Lord-spawn contract.
                sessionEligible = notification.Context.StartKind == MissionStartKind.NewGame;
                mapActive = true;
                log.LogInfo(
                    $"LSS_TEST_SESSION_ARMED: session={notification.Context.SessionId}; eligible={sessionEligible}; " +
                    $"phase={notification.Phase}; " +
                    $"startKind={notification.Context.StartKind}; mode={notification.Context.Mode.Kind}; " +
                    $"isSave={notification.Context.IsSave}; isEditor={notification.Context.IsEditor}.");
            }
            catch (Exception ex)
            {
                mapActive = false;
                sessionEligible = false;
                log.LogError($"LSS_TEST_SESSION_FAILED: {ex}");
            }
        }

        private void OnMissionEnded(MissionLifecycleNotification notification)
        {
            mapActive = false;
            sessionEligible = false;
            log.LogInfo(
                $"LSS_TEST_SESSION_ENDED: session={sessionState.SessionId}; observationTick={observationTick}; " +
                $"reason={notification.EndReason}.");
        }

        private void OnGameTick(int simulationTick)
        {
            if (!mapActive || observationTick >= ObservationStopTicks)
                return;

            observationTick++;
            try
            {
                HashSet<int> kickedPlayerIds = CaptureKickedPlayerIds();

                // gameMembers is populated by only some launch paths. Iterating the native
                // one-based records makes detection mode-independent; a live owned Keep and
                // start marker establish actual participation. When a roster exists, kicked is
                // retained as an additional veto, never as an allowlist.
                for (int playerId = 1; playerId <= 8; playerId++)
                    ObservePlayer(playerId, kickedPlayerIds.Contains(playerId), simulationTick);

                if (observationTick == ConfirmationTimeoutTicks)
                    LogOutstandingConfirmations(simulationTick);
                if (observationTick == ObservationStopTicks)
                    log.LogInfo(
                        $"LSS_TEST_OBSERVATION_COMPLETE: session={sessionState.SessionId}; " +
                        $"observationTick={observationTick}; simulationTick={simulationTick}.");
            }
            catch (Exception ex)
            {
                log.LogError(
                    $"LSS_TEST_TICK_FAILED: session={sessionState.SessionId}; observationTick={observationTick}; " +
                    $"simulationTick={simulationTick}; error={ex}");
            }
        }

        private void ObservePlayer(int playerId, bool kicked, int simulationTick)
        {
            GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
            bool hasResources = players.TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources) &&
                resources != null;
            PlayerSnapshot snapshot = hasResources
                ? CaptureSnapshot(playerId, resources)
                : PlayerSnapshot.Missing(playerId);

            bool logDetailed = observationTick <= DetailedSnapshotTicks ||
                (observationTick >= VanillaLordWindowStart && observationTick <= VanillaLordWindowEnd);
            string fingerprint = snapshot.Fingerprint;
            if (logDetailed || !lastSnapshots.TryGetValue(playerId, out string previous) || previous != fingerprint)
            {
                log.LogInfo(
                    $"LSS_TEST_SNAPSHOT: session={sessionState.SessionId}; observationTick={observationTick}; " +
                    $"simulationTick={simulationTick}; player={playerId}; kickedVeto={kicked}; " +
                    $"eligibleSession={sessionEligible}; {snapshot.Describe()}.");
                lastSnapshots[playerId] = fingerprint;
            }

            if (sessionState.WasAttempted(playerId))
            {
                TryConfirmVanillaLord(playerId, snapshot, simulationTick);
                return;
            }

            var guard = new LordSpawnSlotGuardInput(
                isNewGameSession: sessionEligible,
                correctionWindowOpen: observationTick <= DetailedSnapshotTicks,
                hasResources,
                kicked,
                alreadyAttempted: false,
                snapshot.WinLossState == WinLossState.Loss,
                snapshot.LordUnitId,
                snapshot.LordGlobalId,
                snapshot.LordIdentity.Exists,
                snapshot.LordIdentity.OwnerPlayerId,
                snapshot.LordIdentity.Type == eChimps.CHIMP_TYPE_NULL,
                snapshot.LordIdentity.AliveState == AliveState.None,
                snapshot.LordIdentity.GlobalId,
                snapshot.LordIdentity.CurrentHealth,
                snapshot.ValidOwnedKeep,
                snapshot.ValidOwnedKeepDoorReference);
            LordSpawnSlotDecision decision = LordSpawnSlotFixPolicy.Evaluate(in guard);
            if (decision != LordSpawnSlotDecision.ClearStaleLordReference)
            {
                if (observationTick == 1)
                    log.LogInfo(
                        $"LSS_TEST_DECISION: session={sessionState.SessionId}; player={playerId}; " +
                        $"decision={decision}; noMutation=true.");
                return;
            }

            sessionState.MarkAttempted(playerId);
            log.LogWarning(
                $"LSS_TEST_CAUSE_CONFIRMED: session={sessionState.SessionId}; player={playerId}; " +
                $"reason=active-player-has-valid-owned-keep-and-door-reference-and-exact-zeroed-unit-tombstone-blocks-Vanilla-spawn; " +
                $"before={snapshot.Describe()}.");

            // Native audit (FBCB9319): 0xC6810 transfers Keep-related fields but omits both
            // Lord identity fields. 0xC23C0 then treats any nonzero LordUnitId as present and
            // skips the authoritative 0x17FEF0 creation path. A hook at either native site
            // would be closer to the defect, but would add version-sensitive displaced-code
            // and detour-conflict risk. At ticks 1-3 the remap has finished and Vanilla's Lord
            // window has not: clearing only the proven tombstone, global ID first, lets Vanilla
            // create and initialize the complete Lord through its own path.
            players.SetLordUnitGlobalId(playerId, 0);
            players.SetLordUnitId(playerId, 0);

            if (!players.TryGetPlayerResourcesById(playerId, out GamePlayerResources* updated) ||
                updated == null || updated->r_LordUnitId != 0 || updated->r_LordUnitGlobalId != 0)
            {
                log.LogError(
                    $"LSS_TEST_CORRECTION_FAILED: session={sessionState.SessionId}; player={playerId}; " +
                    "the public Lord identity setters did not produce 0/0; no retry will be attempted this session.");
                return;
            }

            PlayerSnapshot corrected = CaptureSnapshot(playerId, updated);
            lastSnapshots[playerId] = corrected.Fingerprint;
            log.LogWarning(
                $"LSS_TEST_CORRECTED: session={sessionState.SessionId}; player={playerId}; " +
                $"method=GamePlayerManagerAPI.SetLordUnitGlobalId(0)+SetLordUnitId(0); after={corrected.Describe()}; " +
                "lordCreation=left-to-Vanilla.");
        }

        private void TryConfirmVanillaLord(int playerId, PlayerSnapshot snapshot, int simulationTick)
        {
            if (sessionState.IsConfirmed(playerId) || !snapshot.ValidOwnedLord)
                return;

            sessionState.MarkConfirmed(playerId);
            log.LogWarning(
                $"LSS_TEST_VANILLA_LORD_CONFIRMED: session={sessionState.SessionId}; player={playerId}; " +
                $"observationTick={observationTick}; simulationTick={simulationTick}; " +
                $"lordUnitId={snapshot.LordUnitId}; lordGlobalId={snapshot.LordGlobalId}; " +
                "creationPath=Vanilla-after-stale-identity-clear.");
        }

        private void LogOutstandingConfirmations(int simulationTick)
        {
            for (int playerId = 1; playerId <= 8; playerId++)
            {
                if (sessionState.WasAttempted(playerId) && !sessionState.IsConfirmed(playerId))
                    log.LogError(
                        $"LSS_TEST_LORD_TIMEOUT: session={sessionState.SessionId}; player={playerId}; " +
                        $"observationTick={observationTick}; simulationTick={simulationTick}; " +
                        "the guarded stale Lord identity clear was applied but no valid Vanilla Lord was observed.");
            }
        }

        private static HashSet<int> CaptureKickedPlayerIds()
        {
            Platform_Multiplayer.MPGameMember[] members =
                Platform_Multiplayer.Instance?.gameMembers?.ToArray();
            if (members == null || members.Length == 0)
                return new HashSet<int>();

            var kickedPlayerIds = new HashSet<int>();
            foreach (Platform_Multiplayer.MPGameMember member in members)
            {
                if (member == null || !member.kicked || member.playerID < 1 || member.playerID > 8)
                    continue;
                kickedPlayerIds.Add(member.playerID);
            }

            return kickedPlayerIds;
        }

        private static PlayerSnapshot CaptureSnapshot(int playerId, GamePlayerResources* resources)
        {
            int keepId = (int)resources->r_KeepId;
            int keepDoorId = (int)resources->r_KeepDoorId;
            int lordUnitId = (int)resources->r_LordUnitId;
            int lordGlobalId = (int)resources->r_LordUnitGlobalId;
            BuildingIdentity keep = CaptureBuilding(keepId);
            BuildingIdentity keepDoor = CaptureBuilding(keepDoorId);
            bool validOwnedKeep = keep.Exists && keep.OwnerPlayerId == playerId && keep.IsAlive && IsKeep(keep.Type);
            bool validOwnedKeepDoorReference = keepDoor.Exists && keepDoor.OwnerPlayerId == playerId &&
                keepDoor.IsAlive;
            UnitIdentity lordIdentity = UnitIdentity.Missing(lordUnitId);
            bool validLordReference = false;
            if (lordUnitId > 0 &&
                GameUnitManagerAPI.Instance.TryGetUnitById(lordUnitId, out GameUnit* lord) &&
                lord != null)
            {
                lordIdentity = new UnitIdentity(
                    lordUnitId, true, lord->r_ControllableForPlayerId,
                    lord->r_UnitChimp, lord->r_AliveState,
                    (int)lord->r_GlobalId, (int)lord->r_CurrentHealth);
                validLordReference =
                    (lord->r_AliveState == AliveState.NeedsInit || lord->r_AliveState == AliveState.IsAlive) &&
                    lord->r_ControllableForPlayerId == playerId &&
                    lord->r_UnitChimp == eChimps.CHIMP_TYPE_LORD &&
                    lord->r_GlobalId != 0 &&
                    lordGlobalId != 0 &&
                    lord->r_GlobalId == lordGlobalId;
            }

            bool validOwnedLord = validLordReference &&
                lordIdentity.AliveState == AliveState.IsAlive &&
                lordIdentity.CurrentHealth > 0;

            return new PlayerSnapshot(
                playerId, true, resources->r_WinLossState, keepId, keepDoorId,
                lordUnitId, lordGlobalId, keep, keepDoor, lordIdentity,
                validOwnedKeep, validOwnedKeepDoorReference,
                validLordReference, validOwnedLord);
        }

        private static BuildingIdentity CaptureBuilding(int buildingId)
        {
            if (buildingId <= 0 ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                building == null)
            {
                return BuildingIdentity.Missing(buildingId);
            }

            bool alive = building->r_AliveState == AliveState.NeedsInit ||
                building->r_AliveState == AliveState.IsAlive;
            return new BuildingIdentity(
                buildingId, true, building->r_PlayerIdOwner,
                building->r_BuildingType, building->r_AliveState, alive);
        }

        private static bool IsKeep(eStructs type) =>
            type == eStructs.STRUCT_KEEP_ONE ||
            type == eStructs.STRUCT_KEEP_TWO ||
            type == eStructs.STRUCT_KEEP_THREE ||
            type == eStructs.STRUCT_KEEP_FOUR ||
            type == eStructs.STRUCT_KEEP_FIVE;

        private readonly struct BuildingIdentity
        {
            internal BuildingIdentity(
                int id, bool exists, int ownerPlayerId, eStructs type,
                AliveState aliveState, bool isAlive)
            {
                Id = id;
                Exists = exists;
                OwnerPlayerId = ownerPlayerId;
                Type = type;
                AliveState = aliveState;
                IsAlive = isAlive;
            }

            internal int Id { get; }
            internal bool Exists { get; }
            internal int OwnerPlayerId { get; }
            internal eStructs Type { get; }
            internal AliveState AliveState { get; }
            internal bool IsAlive { get; }

            internal static BuildingIdentity Missing(int id) =>
                new BuildingIdentity(id, false, 0, eStructs.STRUCT_NULL, AliveState.None, false);

            public override string ToString() =>
                $"id={Id},exists={Exists},owner={OwnerPlayerId},type={Type},alive={AliveState}";
        }

        private readonly struct UnitIdentity
        {
            internal UnitIdentity(
                int id, bool exists, int ownerPlayerId, eChimps type,
                AliveState aliveState, int globalId, int currentHealth)
            {
                Id = id;
                Exists = exists;
                OwnerPlayerId = ownerPlayerId;
                Type = type;
                AliveState = aliveState;
                GlobalId = globalId;
                CurrentHealth = currentHealth;
            }

            internal int Id { get; }
            internal bool Exists { get; }
            internal int OwnerPlayerId { get; }
            internal eChimps Type { get; }
            internal AliveState AliveState { get; }
            internal int GlobalId { get; }
            internal int CurrentHealth { get; }

            internal static UnitIdentity Missing(int id) =>
                new UnitIdentity(id, false, 0, default, AliveState.None, 0, 0);

            public override string ToString() =>
                $"id={Id},exists={Exists},owner={OwnerPlayerId},type={Type},alive={AliveState}," +
                $"globalId={GlobalId},health={CurrentHealth}";
        }

        private readonly struct PlayerSnapshot
        {
            internal PlayerSnapshot(
                int playerId, bool hasResources, WinLossState winLossState,
                int keepId, int keepDoorId, int lordUnitId, int lordGlobalId,
                BuildingIdentity keep, BuildingIdentity keepDoor, UnitIdentity lordIdentity,
                bool validOwnedKeep, bool validOwnedKeepDoorReference,
                bool validLordReference, bool validOwnedLord)
            {
                PlayerId = playerId;
                HasResources = hasResources;
                WinLossState = winLossState;
                KeepId = keepId;
                KeepDoorId = keepDoorId;
                LordUnitId = lordUnitId;
                LordGlobalId = lordGlobalId;
                Keep = keep;
                KeepDoor = keepDoor;
                LordIdentity = lordIdentity;
                ValidOwnedKeep = validOwnedKeep;
                ValidOwnedKeepDoorReference = validOwnedKeepDoorReference;
                ValidLordReference = validLordReference;
                ValidOwnedLord = validOwnedLord;
            }

            internal int PlayerId { get; }
            internal bool HasResources { get; }
            internal WinLossState WinLossState { get; }
            internal int KeepId { get; }
            internal int KeepDoorId { get; }
            internal int LordUnitId { get; }
            internal int LordGlobalId { get; }
            internal BuildingIdentity Keep { get; }
            internal BuildingIdentity KeepDoor { get; }
            internal UnitIdentity LordIdentity { get; }
            internal bool ValidOwnedKeep { get; }
            internal bool ValidOwnedKeepDoorReference { get; }
            internal bool ValidLordReference { get; }
            internal bool ValidOwnedLord { get; }

            internal string Fingerprint =>
                $"{HasResources}|{(int)WinLossState}|{KeepId}|{KeepDoorId}|{LordUnitId}|{LordGlobalId}|" +
                $"{ValidOwnedKeep}|{ValidOwnedKeepDoorReference}|{ValidLordReference}|{ValidOwnedLord}|" +
                $"{Keep.OwnerPlayerId}|{KeepDoor.OwnerPlayerId}|{LordIdentity.Exists}|{LordIdentity.OwnerPlayerId}|" +
                $"{LordIdentity.Type}|{LordIdentity.AliveState}|{LordIdentity.GlobalId}|{LordIdentity.CurrentHealth}";

            internal string Describe() =>
                $"player={PlayerId}; resources={HasResources}; winLoss={WinLossState}; " +
                $"keep=[{Keep}]; validOwnedKeep={ValidOwnedKeep}; " +
                $"keepDoor=[{KeepDoor}]; validOwnedKeepDoorReference={ValidOwnedKeepDoorReference}; " +
                $"lordReference=[storedUnitId={LordUnitId},storedGlobalId={LordGlobalId},unit=[{LordIdentity}]]; " +
                $"validLordReference={ValidLordReference}; validOwnedLord={ValidOwnedLord}";

            internal static PlayerSnapshot Missing(int playerId) =>
                new PlayerSnapshot(
                    playerId, false, WinLossState.None, 0, 0, 0, 0,
                    BuildingIdentity.Missing(0), BuildingIdentity.Missing(0), UnitIdentity.Missing(0),
                    false, false, false, false);
        }
    }
}
