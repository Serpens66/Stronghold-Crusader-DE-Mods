using APIShared;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BugfixesAndQoL
{
    internal sealed unsafe class CorruptLordDataSpawnRuntime
    {
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly CorruptLordDataSpawnSessionState sessionState = new CorruptLordDataSpawnSessionState();
        private bool mapActive;
        private bool sessionEligible;
        private int observationTick;

        internal CorruptLordDataSpawnRuntime(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        internal void Install()
        {
            Shared.MissionEvents.SetOwner(BugfixesAndQoLPlugin.PluginGuid);
            subscriptions.Add(Shared.MissionEvents.Initialization.Subscribe(OnInitialization));
            subscriptions.Add(Shared.MissionEvents.Ended.Subscribe(OnMissionEnded));
            GameTimeManagerAPI.Instance.OnTick += OnGameTick;
        }

        private bool Enabled => settings.EnableMod && settings.EnableCorruptLordDataSpawnFix;

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

                // Mode classification is diagnostic-only. The corrupt reference was proven in
                // a CustomGame, but neither the exact tombstone nor Vanilla's Lord creation
                // depends on that label. NewGame is the safety boundary: saves restore
                // authoritative identities without guaranteeing another Lord spawn, while the
                // editor has no equivalent gameplay-spawn contract.
                sessionEligible = notification.Context.StartKind == MissionStartKind.NewGame;
                mapActive = true;
                log.LogDebug(
                    $"Corrupt Lord-data spawn fix session armed: session={notification.Context.SessionId}, " +
                    $"eligible={sessionEligible}, phase={notification.Phase}, " +
                    $"startKind={notification.Context.StartKind}, mode={notification.Context.Mode.Kind}.");
            }
            catch (Exception ex)
            {
                mapActive = false;
                sessionEligible = false;
                log.LogError($"Corrupt Lord-data spawn fix could not arm the session: {ex}");
            }
        }

        private void OnMissionEnded(MissionLifecycleNotification notification)
        {
            mapActive = false;
            sessionEligible = false;
        }

        private void OnGameTick(int simulationTick)
        {
            if (!mapActive)
                return;

            observationTick++;
            try
            {
                if (CorruptLordDataSpawnObservationPolicy.IsCorrectionWindow(observationTick))
                    ScanCorrectionCandidates(simulationTick);
                else
                    ObserveOutstandingPlayers(simulationTick);

                if (CorruptLordDataSpawnObservationPolicy.ShouldTimeoutAfterConfirmation(
                        observationTick,
                        sessionState.HasOutstandingConfirmations))
                {
                    LogOutstandingConfirmations(simulationTick);
                    mapActive = false;
                    return;
                }

                if (CorruptLordDataSpawnObservationPolicy.ShouldStopAfterTick(
                        observationTick,
                        sessionState.HasOutstandingConfirmations))
                {
                    mapActive = false;
                }
            }
            catch (Exception ex)
            {
                mapActive = false;
                log.LogError(
                    $"Corrupt Lord-data spawn fix failed during observation: session={sessionState.SessionId}, " +
                    $"tick={observationTick}, simulationTick={simulationTick}, error={ex}");
            }
        }

        private void ScanCorrectionCandidates(int simulationTick)
        {
            HashSet<int> kickedPlayerIds = CaptureKickedPlayerIds();

            // gameMembers is populated by only some launch paths. Iterating all native
            // one-based records makes detection mode-independent; a live owned Keep and
            // start marker establish actual participation. When a roster exists, kicked is
            // retained as an additional veto, never as an allowlist.
            for (int playerId = 1; playerId <= 8; playerId++)
                ObserveCorrectionCandidate(playerId, kickedPlayerIds.Contains(playerId), simulationTick);
        }

        private void ObserveCorrectionCandidate(int playerId, bool kicked, int simulationTick)
        {
            GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
            bool hasResources = players.TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources) &&
                resources != null;
            PlayerSnapshot snapshot = hasResources
                ? CaptureSnapshot(playerId, resources)
                : PlayerSnapshot.Missing(playerId);

            if (sessionState.WasAttempted(playerId))
            {
                TryConfirmVanillaLord(playerId, snapshot, simulationTick);
                return;
            }

            var guard = new CorruptLordDataSpawnGuardInput(
                Enabled,
                sessionEligible,
                CorruptLordDataSpawnObservationPolicy.IsCorrectionWindow(observationTick),
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
            CorruptLordDataSpawnDecision decision = CorruptLordDataSpawnPolicy.Evaluate(in guard);
            if (decision != CorruptLordDataSpawnDecision.ClearStaleLordReference)
                return;

            sessionState.MarkAttempted(playerId);

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
                    $"Corrupt Lord-data spawn fix failed to clear player {playerId}'s stale reference " +
                    $"in session {sessionState.SessionId}; no retry will be attempted.");
                return;
            }

            log.LogWarning(
                $"Cleared corrupt Lord data for player {playerId} in session {sessionState.SessionId} " +
                $"(stale unit/global reference {snapshot.LordUnitId}/{snapshot.LordGlobalId}); " +
                "Lord creation remains with Vanilla.");
        }

        private void ObserveOutstandingPlayers(int simulationTick)
        {
            for (int playerId = 1; playerId <= 8; playerId++)
            {
                if (!sessionState.IsOutstanding(playerId))
                    continue;

                if (!TryCaptureValidOwnedLord(
                        playerId,
                        out int lordUnitId,
                        out int lordGlobalId))
                {
                    continue;
                }

                TryConfirmVanillaLord(
                    playerId,
                    lordUnitId,
                    lordGlobalId,
                    validOwnedLord: true,
                    simulationTick: simulationTick);
            }
        }

        private void TryConfirmVanillaLord(int playerId, PlayerSnapshot snapshot, int simulationTick)
        {
            TryConfirmVanillaLord(
                playerId,
                snapshot.LordUnitId,
                snapshot.LordGlobalId,
                snapshot.ValidOwnedLord,
                simulationTick);
        }

        private void TryConfirmVanillaLord(
            int playerId,
            int lordUnitId,
            int lordGlobalId,
            bool validOwnedLord,
            int simulationTick)
        {
            if (sessionState.IsConfirmed(playerId) || !validOwnedLord)
                return;

            sessionState.MarkConfirmed(playerId);
            log.LogInfo(
                $"Vanilla Lord spawn confirmed after corrupt-data repair: session={sessionState.SessionId}, " +
                $"player={playerId}, unit/global={lordUnitId}/{lordGlobalId}, " +
                $"observationTick={observationTick}, simulationTick={simulationTick}.");
        }

        private void LogOutstandingConfirmations(int simulationTick)
        {
            for (int playerId = 1; playerId <= 8; playerId++)
            {
                if (sessionState.WasAttempted(playerId) && !sessionState.IsConfirmed(playerId))
                    log.LogError(
                        $"No Vanilla Lord was observed after corrupt-data repair: " +
                        $"session={sessionState.SessionId}, player={playerId}, " +
                        $"observationTick={observationTick}, simulationTick={simulationTick}.");
            }
        }

        private static bool TryCaptureValidOwnedLord(
            int playerId,
            out int lordUnitId,
            out int lordGlobalId)
        {
            lordUnitId = 0;
            lordGlobalId = 0;
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(
                    playerId,
                    out GamePlayerResources* resources) ||
                resources == null)
            {
                return false;
            }

            lordUnitId = (int)resources->r_LordUnitId;
            lordGlobalId = (int)resources->r_LordUnitGlobalId;
            if (lordUnitId <= 0 || lordGlobalId <= 0 ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(lordUnitId, out GameUnit* lord) ||
                lord == null)
            {
                return false;
            }

            return lord->r_AliveState == AliveState.IsAlive &&
                lord->r_ControllableForPlayerId == playerId &&
                lord->r_UnitChimp == eChimps.CHIMP_TYPE_LORD &&
                lord->r_GlobalId != 0 &&
                lord->r_GlobalId == lordGlobalId &&
                lord->r_CurrentHealth > 0;
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
                    true, lord->r_ControllableForPlayerId, lord->r_UnitChimp,
                    lord->r_AliveState, (int)lord->r_GlobalId, (int)lord->r_CurrentHealth);
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
                resources->r_WinLossState, lordUnitId, lordGlobalId, lordIdentity,
                validOwnedKeep, validOwnedKeepDoorReference, validOwnedLord);
        }

        private static BuildingIdentity CaptureBuilding(int buildingId)
        {
            if (buildingId <= 0 ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                building == null)
            {
                return BuildingIdentity.Missing;
            }

            bool alive = building->r_AliveState == AliveState.NeedsInit ||
                building->r_AliveState == AliveState.IsAlive;
            return new BuildingIdentity(
                true, building->r_PlayerIdOwner, building->r_BuildingType, alive);
        }

        private static bool IsKeep(eStructs type) =>
            type == eStructs.STRUCT_KEEP_ONE ||
            type == eStructs.STRUCT_KEEP_TWO ||
            type == eStructs.STRUCT_KEEP_THREE ||
            type == eStructs.STRUCT_KEEP_FOUR ||
            type == eStructs.STRUCT_KEEP_FIVE;

        private readonly struct BuildingIdentity
        {
            internal BuildingIdentity(bool exists, int ownerPlayerId, eStructs type, bool isAlive)
            {
                Exists = exists;
                OwnerPlayerId = ownerPlayerId;
                Type = type;
                IsAlive = isAlive;
            }

            internal bool Exists { get; }
            internal int OwnerPlayerId { get; }
            internal eStructs Type { get; }
            internal bool IsAlive { get; }

            internal static BuildingIdentity Missing =>
                new BuildingIdentity(false, 0, eStructs.STRUCT_NULL, false);
        }

        private readonly struct UnitIdentity
        {
            internal UnitIdentity(
                bool exists, int ownerPlayerId, eChimps type,
                AliveState aliveState, int globalId, int currentHealth)
            {
                Exists = exists;
                OwnerPlayerId = ownerPlayerId;
                Type = type;
                AliveState = aliveState;
                GlobalId = globalId;
                CurrentHealth = currentHealth;
            }

            internal bool Exists { get; }
            internal int OwnerPlayerId { get; }
            internal eChimps Type { get; }
            internal AliveState AliveState { get; }
            internal int GlobalId { get; }
            internal int CurrentHealth { get; }

            internal static UnitIdentity Missing(int unitId) =>
                new UnitIdentity(false, 0, default, AliveState.None, 0, 0);
        }

        private readonly struct PlayerSnapshot
        {
            internal PlayerSnapshot(
                WinLossState winLossState, int lordUnitId, int lordGlobalId,
                UnitIdentity lordIdentity, bool validOwnedKeep,
                bool validOwnedKeepDoorReference, bool validOwnedLord)
            {
                WinLossState = winLossState;
                LordUnitId = lordUnitId;
                LordGlobalId = lordGlobalId;
                LordIdentity = lordIdentity;
                ValidOwnedKeep = validOwnedKeep;
                ValidOwnedKeepDoorReference = validOwnedKeepDoorReference;
                ValidOwnedLord = validOwnedLord;
            }

            internal WinLossState WinLossState { get; }
            internal int LordUnitId { get; }
            internal int LordGlobalId { get; }
            internal UnitIdentity LordIdentity { get; }
            internal bool ValidOwnedKeep { get; }
            internal bool ValidOwnedKeepDoorReference { get; }
            internal bool ValidOwnedLord { get; }

            internal static PlayerSnapshot Missing(int playerId) =>
                new PlayerSnapshot(
                    WinLossState.None, 0, 0, UnitIdentity.Missing(0),
                    false, false, false);
        }
    }
}
