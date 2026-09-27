using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
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

        private readonly ManualLogSource log;
        private readonly bool fixesLoaded;
        private readonly string[,] previousGroups = new string[MaxPlayers + 1, RaidGroupCount];
        private readonly string[] previousCounters = new string[MaxPlayers + 1];
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
