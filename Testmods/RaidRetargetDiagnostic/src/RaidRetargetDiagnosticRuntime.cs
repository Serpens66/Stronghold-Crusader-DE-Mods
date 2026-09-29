using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RaidRetargetDiagnostic
{
    internal sealed unsafe class RaidRetargetDiagnosticRuntime
    {
        private const int MaxPlayers = 8;
        private const int RaidGroupCount = 6;
        private const int RetargetCounterOffset = 0x2B78;
        private const int RemainingCounterOffset = 0x390C;
        private const int TribeStatusAOffset = 0x5EA;
        private const int TribeStatusBOffset = 0x5EC;
        private const int TribeStatusCOffset = 0x5F0;
        private const int TargetBuildingIdOffset = 0x622;
        private const int TargetGlobalIdOffset = 0x626;
        private const int CommandTargetBuildingIdOffset = 0x64C;
        private const int CommandTargetGlobalIdOffset = 0x650;
        private const int StationarySummaryInterval = 50;

        private readonly ManualLogSource log;
        private readonly bool fixesLoaded;
        private readonly string[,] previousGroups = new string[MaxPlayers + 1, RaidGroupCount];
        private readonly string[] previousCounters = new string[MaxPlayers + 1];
        private readonly string[,] previousTargetIdentities = new string[MaxPlayers + 1, RaidGroupCount];
        private readonly Dictionary<string, string> previousUnits = new Dictionary<string, string>();
        private readonly Dictionary<string, int> lastUnitChanges = new Dictionary<string, int>();
        private readonly Dictionary<int, Stack<DeleteCapture>> pendingDeletes =
            new Dictionary<int, Stack<DeleteCapture>>();
        private bool active;
        private bool firstTickLogged;
        private int lastTick = -1;
        private long sessionId;

        internal RaidRetargetDiagnosticRuntime(ManualLogSource log, bool fixesLoaded)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.fixesLoaded = fixesLoaded;
            ValidateLayout();
        }

        internal void OnSessionStarted(Shared.GameplaySessionStartedContext session)
        {
            ClearObservations();
            sessionId = session.SessionId;
            active = true;
            firstTickLogged = false;
            lastTick = -1;
            Info($"RAID_DIAG_SESSION: session={sessionId}, kind={session.Kind}, loadedSave={session.IsLoadedSave}, " +
                $"save={session.SaveFileName}, editor={session.IsEditor}, replay={session.IsReplay}, fixesLoaded={fixesLoaded}.");
        }

        internal void OnSessionEnded()
        {
            if (active) Info($"RAID_DIAG_END: session={sessionId}, tick={lastTick}.");
            active = false;
            ClearObservations();
        }

        internal void OnTick(int tick)
        {
            if (!active) return;
            try
            {
                if (!firstTickLogged)
                {
                    firstTickLogged = true;
                    Info($"RAID_DIAG_POST_STARTUP_TICK: session={sessionId}, tick={tick}, publisher=GameTimeManagerAPI.OnTick.");
                }
                if (lastTick >= 0 && tick < lastTick)
                {
                    int resetFrom = lastTick;
                    ClearObservations();
                    lastTick = -1;
                    Warn($"RAID_DIAG_TICK_RESET: session={sessionId}, previous={resetFrom}, current={tick}.");
                }
                int previousPreTick = lastTick;
                lastTick = tick;
                var players = GamePlayerManagerAPI.Instance;
                var tribes = GameTribeManagerAPI.Instance;
                for (int playerId = 1; playerId <= MaxPlayers; playerId++)
                {
                    if (!players.IsAIPlayer(playerId) ||
                        !players.TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
                        continue;

                    string counters = $"retarget={resources->N00003F66},remaining100={resources->N00005341}";
                    if (!String.Equals(previousCounters[playerId], counters, StringComparison.Ordinal))
                    {
                        string changeWindow = previousCounters[playerId] == null || previousPreTick < 0 ? "initialSample" :
                            $"afterPreTick:{previousPreTick}/beforePreTick:{tick}";
                        Info($"RAID_DIAG_COUNTER: session={sessionId}, tick={tick}, player={playerId}, " +
                            $"previous={previousCounters[playerId] ?? "unseen"}, current={counters}, " +
                            $"changeWindow={changeWindow}.");
                        previousCounters[playerId] = counters;
                    }

                    for (int group = 0; group < RaidGroupCount; group++)
                    {
                        AITribeStorageRole16 role = (AITribeStorageRole16)((int)AITribeStorageRole16.HarassmentCombat0 + group);
                        bool hasRole = tribes.TryGetAITribeStorageRole(playerId, role,
                            out ushort tribeId, out uint tribeGlobalId);
                        GameTribe* tribe = null;
                        bool resolved = hasRole && tribeId != 0 && tribes.IsValidId(tribeId) &&
                            tribes.TryResolveAITribeStorageRole(playerId, role,
                                out tribe);
                        string state = FormatGroup(tribeId, tribeGlobalId, resolved ? tribe : null);
                        if (resolved && tribe != null && tribe->r_UnitsInGroup != 0)
                        {
                            byte* bytes = (byte*)tribe;
                            ushort targetId = *(ushort*)(bytes + TargetBuildingIdOffset);
                            uint targetGlobalId = *(uint*)(bytes + TargetGlobalIdOffset);
                            string targetIdentity = $"{tribeId}/{tribeGlobalId}:{targetId}/{targetGlobalId}";
                            if (!String.Equals(previousTargetIdentities[playerId, group], targetIdentity,
                                    StringComparison.Ordinal))
                            {
                                previousTargetIdentities[playerId, group] = targetIdentity;
                                if (targetId != 0)
                                    LogTargetAccess(playerId, group, tribeId, tribeGlobalId,
                                        targetId, targetGlobalId);
                            }
                            if (targetId != 0)
                                LogGroupUnits(playerId, group, tribeId, tribeGlobalId);
                        }
                        if (String.Equals(previousGroups[playerId, group], state, StringComparison.Ordinal))
                            continue;
                        Info($"RAID_DIAG_GROUP: session={sessionId}, tick={tick}, player={playerId}, group={group}, " +
                            $"role={(int)role}, previous={previousGroups[playerId, group] ?? "unseen"}, current={state}, {counters}.");
                        previousGroups[playerId, group] = state;
                    }
                }
            }
            catch (Exception ex)
            {
                Warn($"RAID_DIAG_TICK_ERROR: session={sessionId}, tick={tick}, error={ex}.");
            }
        }

        internal void OnBuildingDamage(BuildingTileTakeDamageEventArgs args)
        {
            if (!active || args.Phase != EventHookPhase.Pre) return;
            try
            {
                var tiles = GameTileManagerAPI.Instance;
                if ((uint)args.TileId >= (uint)tiles.GetStructureLayer().Length) return;
                int buildingId = tiles.GetTileBuildingId(args.TileId);
                uint? buildingGlobalId = ReadBuildingGlobalId(buildingId);
                if (!buildingGlobalId.HasValue) return;
                string groups = MatchingTargetGroups(buildingId, buildingGlobalId.Value);
                if (groups == "none") return;
                Info($"RAID_DIAG_DAMAGE: session={sessionId}, tick=afterPreTick:{lastTick}, phase={args.Phase}, " +
                    $"buildingId={buildingId}, buildingGlobalId={buildingGlobalId.Value}, " +
                    $"building={FormatBuilding(buildingId, null)}, raidGroups={groups}, " +
                    $"tile={args.TileId}, xy={args.TileX}/{args.TileY}, damage={args.Damage}, sourcePlayer={args.PlayerIdSource}.");
            }
            catch (Exception ex)
            {
                Warn($"RAID_DIAG_DAMAGE_ERROR: session={sessionId}, tick={lastTick}, error={ex}.");
            }
        }

        internal void OnBuildingDelete(BuildingDeleteEventArgs args)
        {
            if (!active) return;
            try
            {
                if (args.Phase == EventHookPhase.Pre)
                {
                    uint? globalId = ReadBuildingGlobalId(args.BuildingId);
                    string groups = globalId.HasValue
                        ? MatchingTargetGroups(args.BuildingId, globalId.Value) : "none";
                    var capture = new DeleteCapture(globalId, FormatBuilding(args.BuildingId, null), groups);
                    if (!pendingDeletes.TryGetValue(args.BuildingId, out Stack<DeleteCapture> stack))
                    {
                        stack = new Stack<DeleteCapture>();
                        pendingDeletes.Add(args.BuildingId, stack);
                    }
                    stack.Push(capture);
                    // All building deletions are logged, regardless of building type or raid target.
                    Info($"RAID_DIAG_DELETE: session={sessionId}, tick=afterPreTick:{lastTick}, phase=Pre, " +
                        $"buildingId={args.BuildingId}, preGlobalId={globalId?.ToString() ?? "none"}, " +
                        $"preBuilding={capture.PreBuilding}, raidGroups={groups}.");
                }
                else if (args.Phase == EventHookPhase.Post)
                {
                    DeleteCapture capture = null;
                    if (pendingDeletes.TryGetValue(args.BuildingId, out Stack<DeleteCapture> stack) &&
                        stack.Count != 0)
                    {
                        capture = stack.Pop();
                        if (stack.Count == 0) pendingDeletes.Remove(args.BuildingId);
                    }
                    Info($"RAID_DIAG_DELETE: session={sessionId}, tick=afterPreTick:{lastTick}, phase=Post, " +
                        $"buildingId={args.BuildingId}, preGlobalId={capture?.GlobalId?.ToString() ?? "missing"}, " +
                        $"preBuilding={capture?.PreBuilding ?? "missing"}, " +
                        $"raidGroupsAtPre={capture?.RaidGroups ?? "missing"}, " +
                        $"postBuilding={FormatBuilding(args.BuildingId, null)}.");
                }
            }
            catch (Exception ex)
            {
                Warn($"RAID_DIAG_DELETE_ERROR: session={sessionId}, tick={lastTick}, error={ex}.");
            }
        }

        internal void OnTribeOrder(TribeIssueOrderWithTargetEventArgs args)
        {
            if (!active || !TryIdentifyRaidGroup(args.TribeId,
                    out int playerId, out int group, out uint tribeGlobalId)) return;
            try
            {
                string target = args.AICommand == TribeAICommand.AttackBuilding ||
                    args.AICommand == TribeAICommand.ForceAttackBuilding
                    ? FormatBuilding(args.TargetValue1, unchecked((uint)args.TargetValue2))
                    : "notBuildingCommand";
                Info($"RAID_DIAG_TRIBE_ORDER: session={sessionId}, tick=afterPreTick:{lastTick}, " +
                    $"phase={args.Phase}, player={playerId}, group={group}, " +
                    $"tribe={args.TribeId}/{tribeGlobalId}, command={args.AICommand}/{(int)args.AICommand}, " +
                    $"target1={args.TargetValue1}, target2={args.TargetValue2}, a6={args.a6}, " +
                    $"return={args.ReturnValue}, targetBuilding={target}.");
            }
            catch (Exception ex)
            {
                Warn($"RAID_DIAG_TRIBE_ORDER_ERROR: session={sessionId}, tick={lastTick}, error={ex}.");
            }
        }

        internal void OnTribeMove(TribeIssueOrderMoveHereEventArgs args)
        {
            if (!active || !TryIdentifyRaidGroup(args.TribeId,
                    out int playerId, out int group, out uint tribeGlobalId)) return;
            Info($"RAID_DIAG_TRIBE_MOVE: session={sessionId}, tick=afterPreTick:{lastTick}, " +
                $"phase={args.Phase}, player={playerId}, group={group}, " +
                $"tribe={args.TribeId}/{tribeGlobalId}, destination={args.TileX}/{args.TileY}, " +
                $"patrol={args.IsPatrolPath}, newOrder={args.IsNewOrder}, " +
                $"moveType={args.MoveType}, return={args.ReturnValue}.");
        }

        internal void OnUnitMove(UnitMoveHereEventArgs args)
        {
            if (!active || args.UnitId <= 0) return;
            try
            {
                var units = GameUnitManagerAPI.Instance;
                if (!units.IsValidId(args.UnitId) ||
                    !units.TryGetUnitById(args.UnitId, out GameUnit* unit) || unit == null ||
                    unit->r_AliveState != AliveState.IsAlive ||
                    !TryIdentifyRaidGroup(unit->r_TribeId,
                        out int playerId, out int group, out uint tribeGlobalId)) return;
                Info($"RAID_DIAG_UNIT_MOVE: session={sessionId}, tick=afterPreTick:{lastTick}, " +
                    $"phase={args.Phase}, player={playerId}, group={group}, " +
                    $"tribe={unit->r_TribeId}/{tribeGlobalId}, unit={args.UnitId}/{unit->r_GlobalId}, " +
                    $"destination={args.TileX}/{args.TileY}, unknown={args.Unknown}, " +
                    $"return={args.ReturnValue}, current={unit->r_CurrentTilePositionX}/{unit->r_CurrentTilePositionY}.");
            }
            catch (Exception ex)
            {
                Warn($"RAID_DIAG_UNIT_MOVE_ERROR: session={sessionId}, tick={lastTick}, error={ex}.");
            }
        }

        private static bool TryIdentifyRaidGroup(int tribeId, out int playerId,
            out int group, out uint tribeGlobalId)
        {
            playerId = 0;
            group = -1;
            tribeGlobalId = 0;
            var tribes = GameTribeManagerAPI.Instance;
            if (tribeId <= 0 || !tribes.IsValidId(tribeId)) return false;
            for (int candidatePlayer = 1; candidatePlayer <= MaxPlayers; candidatePlayer++)
            {
                if (!GamePlayerManagerAPI.Instance.IsAIPlayer(candidatePlayer)) continue;
                for (int candidateGroup = 0; candidateGroup < RaidGroupCount; candidateGroup++)
                {
                    AITribeStorageRole16 role = (AITribeStorageRole16)
                        ((int)AITribeStorageRole16.HarassmentCombat0 + candidateGroup);
                    if (!tribes.TryGetAITribeStorageRole(candidatePlayer, role,
                            out ushort storedId, out uint storedGlobalId) ||
                        storedId != tribeId ||
                        !tribes.TryResolveAITribeStorageRole(candidatePlayer, role,
                            out GameTribe* tribe) || tribe == null ||
                        tribe->r_GlobalId != storedGlobalId) continue;
                    playerId = candidatePlayer;
                    group = candidateGroup;
                    tribeGlobalId = storedGlobalId;
                    return true;
                }
            }
            return false;
        }

        private void LogGroupUnits(int playerId, int group, int tribeId, uint tribeGlobalId)
        {
            var unitIds = new List<int>();
            if (!GameTribeManagerAPI.Instance.GetUnits(tribeId, unitIds)) return;
            var units = GameUnitManagerAPI.Instance;
            foreach (int unitId in unitIds)
            {
                if (!units.IsValidId(unitId) ||
                    !units.TryGetUnitById(unitId, out GameUnit* unit) || unit == null ||
                    unit->r_AliveState != AliveState.IsAlive || unit->r_TribeId != tribeId)
                    continue;
                string key = $"{playerId}/{group}/{tribeGlobalId}/{unit->r_GlobalId}";
                string state = $"unit={unitId}/{unit->r_GlobalId},type={unit->r_UnitChimp}," +
                    $"position={unit->r_CurrentTilePositionX}/{unit->r_CurrentTilePositionY}," +
                    $"destination={unit->r_TargetTilePositionX}/{unit->r_TargetTilePositionY}," +
                    $"currentTile={unit->r_CurrentPositionTileId},targetTile={unit->r_TargetPositionTileId}," +
                    $"pathFlags={unit->r_PathPlanStateBitFlags},pathIndex={unit->r_CurrentPathPlanIndex}," +
                    $"pathLength={unit->r_PathPlanLength},aiState={unit->r_AIState}," +
                    $"attackMoveTarget={unit->r_AttackMoveToTargetTileX}/{unit->r_AttackMoveToTargetTileY}," +
                    $"contextUnit={unit->r_AI_ContextTargetUnitId}/{unit->r_AI_ContextTargetUnitGlobalId}," +
                    $"contextBuildingTile={unit->r_AI_ContextTargetBuildingTileId}";
                if (!previousUnits.TryGetValue(key, out string previous) ||
                    !String.Equals(previous, state, StringComparison.Ordinal))
                {
                    Info($"RAID_DIAG_UNIT_STATE: session={sessionId}, tick={lastTick}, " +
                        $"player={playerId}, group={group}, tribe={tribeId}/{tribeGlobalId}, " +
                        $"previous={previous ?? "unseen"}, current={state}.");
                    previousUnits[key] = state;
                    lastUnitChanges[key] = lastTick;
                }
                else if (lastTick % StationarySummaryInterval == 0 &&
                    lastUnitChanges.TryGetValue(key, out int changedAt) &&
                    lastTick - changedAt >= StationarySummaryInterval)
                {
                    Info($"RAID_DIAG_UNIT_STILL: session={sessionId}, tick={lastTick}, " +
                        $"player={playerId}, group={group}, tribe={tribeId}/{tribeGlobalId}, " +
                        $"unchangedSince={changedAt}, {state}.");
                }
            }
        }

        private static string FormatGroup(ushort tribeId, uint tribeGlobalId, GameTribe* tribe)
        {
            if (tribe == null)
                return $"tribe={tribeId}/{tribeGlobalId},resolved=false";

            byte* bytes = (byte*)tribe;
            ushort targetId = *(ushort*)(bytes + TargetBuildingIdOffset);
            uint targetGlobalId = *(uint*)(bytes + TargetGlobalIdOffset);
            short statusA = *(short*)(bytes + TribeStatusAOffset);
            short statusB = *(short*)(bytes + TribeStatusBOffset);
            short statusC = *(short*)(bytes + TribeStatusCOffset);
            return $"tribe={tribeId}/{tribeGlobalId},resolved=true,units={tribe->r_UnitsInGroup}," +
                $"stance={tribe->r_TribeStance},status={statusA}/{statusB}/{statusC}," +
                $"target={targetId}/{targetGlobalId},building={FormatBuilding(targetId, targetGlobalId)}";
        }

        private static string FormatBuilding(int buildingId, uint? expectedGlobalId)
        {
            if (buildingId <= 0) return "none";
            var buildings = GameBuildingManagerAPI.Instance;
            if (!buildings.IsValidId(buildingId) ||
                !buildings.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                building == null)
                return "slotInvalid";
            bool live = building->r_AliveState == AliveState.IsAlive;
            string slot = $"type={building->r_BuildingType},owner={building->r_PlayerIdOwner}," +
                $"global={building->r_GlobalId},alive={building->r_AliveState},slotAlive={live}";
            if (!expectedGlobalId.HasValue) return slot;
            bool matches = building->r_GlobalId == expectedGlobalId.Value;
            return $"{slot},globalMatchesTarget={matches},targetIdentityValid={matches && live}";
        }

        private static uint? ReadBuildingGlobalId(int buildingId)
        {
            var buildings = GameBuildingManagerAPI.Instance;
            if (!buildings.IsValidId(buildingId) ||
                !buildings.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                building == null || building->r_AliveState != AliveState.IsAlive)
                return null;
            return building->r_GlobalId;
        }

        private static string MatchingTargetGroups(int buildingId, uint buildingGlobalId)
        {
            var matches = new List<string>();
            var players = GamePlayerManagerAPI.Instance;
            var tribes = GameTribeManagerAPI.Instance;
            for (int playerId = 1; playerId <= MaxPlayers; playerId++)
            {
                if (!players.IsAIPlayer(playerId)) continue;
                for (int group = 0; group < RaidGroupCount; group++)
                {
                    AITribeStorageRole16 role = (AITribeStorageRole16)((int)AITribeStorageRole16.HarassmentCombat0 + group);
                    if (!tribes.TryGetAITribeStorageRole(playerId, role,
                            out ushort tribeId, out uint tribeGlobalId) ||
                        tribeId == 0 || !tribes.IsValidId(tribeId) ||
                        !tribes.TryResolveAITribeStorageRole(playerId, role,
                        out GameTribe* tribe)) continue;
                    byte* bytes = (byte*)tribe;
                    if (*(ushort*)(bytes + TargetBuildingIdOffset) == buildingId &&
                        *(uint*)(bytes + TargetGlobalIdOffset) == buildingGlobalId)
                        matches.Add($"p{playerId}g{group}t{tribeId}/{tribeGlobalId}");
                }
            }
            return matches.Count == 0 ? "none" : String.Join(",", matches);
        }

        private static void ValidateLayout()
        {
            if ((int)AITribeStorageRole16.HarassmentCombat0 != 180 ||
                (int)AITribeStorageRole16.HarassmentCombat5 != 185 ||
                Marshal.SizeOf(typeof(GameTribe)) != 0x688 ||
                Marshal.OffsetOf(typeof(GameTribe), nameof(GameTribe.r_UnitsInGroup)).ToInt32() != 0x32 ||
                Marshal.OffsetOf(typeof(GameTribe), nameof(GameTribe.r_TribeStance)).ToInt32() != 0x60A ||
                Marshal.OffsetOf(typeof(GameTribe), nameof(GameTribe.N00000580)).ToInt32() != 0x620 ||
                Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.N00003F66)).ToInt32() != RetargetCounterOffset ||
                Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.N00005341)).ToInt32() != RemainingCounterOffset)
                throw new InvalidOperationException("Installed Script Extender raid layout differs from audited native layout.");
        }

        private void ClearObservations()
        {
            Array.Clear(previousGroups, 0, previousGroups.Length);
            Array.Clear(previousCounters, 0, previousCounters.Length);
            pendingDeletes.Clear();
        }

        private void Info(string value) => Shared.DebugLogHelper.LogInfo(log, value);
        private void Warn(string value) => Shared.DebugLogHelper.LogWarning(log, value);

        private sealed class DeleteCapture
        {
            internal readonly uint? GlobalId;
            internal readonly string PreBuilding;
            internal readonly string RaidGroups;

            internal DeleteCapture(uint? globalId, string preBuilding, string raidGroups)
            {
                GlobalId = globalId;
                PreBuilding = preBuilding;
                RaidGroups = raidGroups;
            }
        }
    }
}
