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
        private bool rosterUnavailableLogged;
        private int observationTick;

        internal LordSpawnSlotFixTestRuntime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        internal void Install()
        {
            Shared.MissionEvents.SetOwner(LordSpawnSlotFixTestPluginGuid);
            subscriptions.Add(Shared.MissionEvents.NativeStart.Subscribe(OnNativeStart));
            subscriptions.Add(Shared.MissionEvents.Ended.Subscribe(OnMissionEnded));
            GameTimeManagerAPI.Instance.OnTick += OnGameTick;
            log.LogInfo(
                "LSS_TEST_INSTALL: correctionThread=GameTimeManagerAPI.OnTick; nativeHooks=0; " +
                "eligibleMode=new CustomGame only; saves=false; trails=false; editor=false.");
        }

        private const string LordSpawnSlotFixTestPluginGuid = "LordSpawnSlotFixTest_Serp";

        private void OnNativeStart(MissionLifecycleNotification notification)
        {
            try
            {
                if (notification.Phase == MissionInitializationPhase.BeforeNativeStart)
                {
                    sessionState.Reset(notification.Context.SessionId);
                    mapActive = false;
                    sessionEligible = false;
                    observationTick = 0;
                    rosterUnavailableLogged = false;
                    lastSnapshots.Clear();
                    log.LogInfo(
                        $"LSS_TEST_SESSION_RESET: session={notification.Context.SessionId}; " +
                        $"startKind={notification.Context.StartKind}; mode={notification.Context.Mode.Kind}; " +
                        $"isSave={notification.Context.IsSave}; isEditor={notification.Context.IsEditor}.");
                    return;
                }

                if (notification.Phase != MissionInitializationPhase.AfterNativeStart)
                    return;

                sessionEligible =
                    notification.Context.StartKind == MissionStartKind.NewGame &&
                    notification.Context.Mode.Kind == Shared.GameModeKind.CustomGame &&
                    !notification.Context.IsSave &&
                    !notification.Context.IsEditor;
                mapActive = true;
                log.LogInfo(
                    $"LSS_TEST_SESSION_ARMED: session={notification.Context.SessionId}; eligible={sessionEligible}; " +
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
                RosterPlayer[] roster = CaptureRawRoster();
                if (roster.Length == 0)
                {
                    if (!rosterUnavailableLogged)
                    {
                        rosterUnavailableLogged = true;
                        log.LogWarning(
                            $"LSS_TEST_ROSTER_UNAVAILABLE: session={sessionState.SessionId}; " +
                            $"observationTick={observationTick}; simulationTick={simulationTick}; no mutation performed.");
                    }
                    return;
                }

                rosterUnavailableLogged = false;
                foreach (RosterPlayer member in roster)
                    ObservePlayer(member, simulationTick);

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

        private void ObservePlayer(RosterPlayer member, int simulationTick)
        {
            GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
            bool hasResources = players.TryGetPlayerResourcesById(member.PlayerId, out GamePlayerResources* resources) &&
                resources != null;
            PlayerSnapshot snapshot = hasResources
                ? CaptureSnapshot(member.PlayerId, resources)
                : PlayerSnapshot.Missing(member.PlayerId);

            bool logDetailed = observationTick <= DetailedSnapshotTicks ||
                (observationTick >= VanillaLordWindowStart && observationTick <= VanillaLordWindowEnd);
            string fingerprint = snapshot.Fingerprint;
            if (logDetailed || !lastSnapshots.TryGetValue(member.PlayerId, out string previous) || previous != fingerprint)
            {
                log.LogInfo(
                    $"LSS_TEST_SNAPSHOT: session={sessionState.SessionId}; observationTick={observationTick}; " +
                    $"simulationTick={simulationTick}; rosterPlayer={member.PlayerId}; kicked={member.Kicked}; " +
                    $"eligibleSession={sessionEligible}; {snapshot.Describe()}.");
                lastSnapshots[member.PlayerId] = fingerprint;
            }

            if (sessionState.WasAttempted(member.PlayerId))
            {
                TryConfirmVanillaLord(member.PlayerId, snapshot, simulationTick);
                return;
            }

            var guard = new LordSpawnSlotGuardInput(
                sessionEligible,
                hasResources,
                inRoster: true,
                member.Kicked,
                alreadyAttempted: false,
                snapshot.WinLossState == WinLossState.Loss,
                snapshot.LordUnitId,
                snapshot.ValidOwnedKeep,
                snapshot.ValidOwnedKeepDoor);
            LordSpawnSlotDecision decision = LordSpawnSlotFixPolicy.Evaluate(in guard);
            if (decision != LordSpawnSlotDecision.Correct)
            {
                if (observationTick == 1)
                    log.LogInfo(
                        $"LSS_TEST_DECISION: session={sessionState.SessionId}; player={member.PlayerId}; " +
                        $"decision={decision}; noMutation=true.");
                return;
            }

            sessionState.MarkAttempted(member.PlayerId);
            log.LogWarning(
                $"LSS_TEST_CAUSE_CONFIRMED: session={sessionState.SessionId}; player={member.PlayerId}; " +
                $"reason=active-roster-player-has-valid-owned-keep-and-door-but-started-Loss-without-Lord; " +
                $"before={snapshot.Describe()}.");
            players.SetWinLossState(member.PlayerId, WinLossState.None);

            if (!players.TryGetPlayerResourcesById(member.PlayerId, out GamePlayerResources* updated) ||
                updated == null || updated->r_WinLossState != WinLossState.None)
            {
                log.LogError(
                    $"LSS_TEST_CORRECTION_FAILED: session={sessionState.SessionId}; player={member.PlayerId}; " +
                    "SetWinLossState did not produce WinLossState.None; no retry will be attempted this session.");
                return;
            }

            PlayerSnapshot corrected = CaptureSnapshot(member.PlayerId, updated);
            lastSnapshots[member.PlayerId] = corrected.Fingerprint;
            log.LogWarning(
                $"LSS_TEST_CORRECTED: session={sessionState.SessionId}; player={member.PlayerId}; " +
                $"method=GamePlayerManagerAPI.SetWinLossState(None); after={corrected.Describe()}; " +
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
                "creationPath=Vanilla-after-WinLoss-correction.");
        }

        private void LogOutstandingConfirmations(int simulationTick)
        {
            foreach (RosterPlayer member in CaptureRawRoster())
            {
                if (sessionState.WasAttempted(member.PlayerId) && !sessionState.IsConfirmed(member.PlayerId))
                    log.LogError(
                        $"LSS_TEST_LORD_TIMEOUT: session={sessionState.SessionId}; player={member.PlayerId}; " +
                        $"observationTick={observationTick}; simulationTick={simulationTick}; " +
                        "the guarded WinLoss correction was applied but no valid Vanilla Lord was observed.");
            }
        }

        private static RosterPlayer[] CaptureRawRoster()
        {
            Platform_Multiplayer.MPGameMember[] members =
                Platform_Multiplayer.Instance?.gameMembers?.ToArray();
            if (members == null || members.Length == 0)
                return Array.Empty<RosterPlayer>();

            var byPlayer = new Dictionary<int, RosterPlayer>();
            foreach (Platform_Multiplayer.MPGameMember member in members)
            {
                if (member == null || member.playerID < 1 || member.playerID > 8)
                    continue;
                byPlayer[member.playerID] = new RosterPlayer(member.playerID, member.kicked);
            }

            RosterPlayer[] result = byPlayer.Values.OrderBy(member => member.PlayerId).ToArray();
            return result;
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
            bool validOwnedKeepDoor = keepDoor.Exists && keepDoor.OwnerPlayerId == playerId &&
                keepDoor.IsAlive && IsKeepDoor(keepDoor.Type);
            bool validOwnedLord = false;
            if (lordUnitId > 0 &&
                GameUnitManagerAPI.Instance.TryGetUnitById(lordUnitId, out GameUnit* lord) &&
                lord != null)
            {
                validOwnedLord = lord->r_AliveState == AliveState.IsAlive &&
                    lord->r_ControllableForPlayerId == playerId &&
                    lord->r_UnitChimp == eChimps.CHIMP_TYPE_LORD &&
                    lord->r_CurrentHealth > 0 &&
                    lord->r_GlobalId != 0 &&
                    (lordGlobalId == 0 || lord->r_GlobalId == lordGlobalId);
            }

            return new PlayerSnapshot(
                playerId, true, resources->r_WinLossState, keepId, keepDoorId,
                lordUnitId, lordGlobalId, keep, keepDoor,
                validOwnedKeep, validOwnedKeepDoor, validOwnedLord);
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

        private static bool IsKeepDoor(eStructs type) =>
            type == eStructs.STRUCT_KEEPDOOR_LEFT ||
            type == eStructs.STRUCT_KEEPDOOR_RIGHT ||
            type == eStructs.STRUCT_KEEPDOOR;

        private readonly struct RosterPlayer
        {
            internal RosterPlayer(int playerId, bool kicked)
            {
                PlayerId = playerId;
                Kicked = kicked;
            }

            internal int PlayerId { get; }
            internal bool Kicked { get; }
        }

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

        private readonly struct PlayerSnapshot
        {
            internal PlayerSnapshot(
                int playerId, bool hasResources, WinLossState winLossState,
                int keepId, int keepDoorId, int lordUnitId, int lordGlobalId,
                BuildingIdentity keep, BuildingIdentity keepDoor,
                bool validOwnedKeep, bool validOwnedKeepDoor, bool validOwnedLord)
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
                ValidOwnedKeep = validOwnedKeep;
                ValidOwnedKeepDoor = validOwnedKeepDoor;
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
            internal bool ValidOwnedKeep { get; }
            internal bool ValidOwnedKeepDoor { get; }
            internal bool ValidOwnedLord { get; }

            internal string Fingerprint =>
                $"{HasResources}|{(int)WinLossState}|{KeepId}|{KeepDoorId}|{LordUnitId}|{LordGlobalId}|" +
                $"{ValidOwnedKeep}|{ValidOwnedKeepDoor}|{ValidOwnedLord}|{Keep.OwnerPlayerId}|{KeepDoor.OwnerPlayerId}";

            internal string Describe() =>
                $"player={PlayerId}; resources={HasResources}; winLoss={WinLossState}; " +
                $"keep=[{Keep}]; validOwnedKeep={ValidOwnedKeep}; " +
                $"keepDoor=[{KeepDoor}]; validOwnedKeepDoor={ValidOwnedKeepDoor}; " +
                $"lordUnitId={LordUnitId}; lordGlobalId={LordGlobalId}; validOwnedLord={ValidOwnedLord}";

            internal static PlayerSnapshot Missing(int playerId) =>
                new PlayerSnapshot(
                    playerId, false, WinLossState.None, 0, 0, 0, 0,
                    BuildingIdentity.Missing(0), BuildingIdentity.Missing(0),
                    false, false, false);
        }
    }
}
