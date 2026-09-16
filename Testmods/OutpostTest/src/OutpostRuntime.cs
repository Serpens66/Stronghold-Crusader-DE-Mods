using System;
using System.Collections.Generic;
using APIShared;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace OutpostTest
{
    internal sealed unsafe class OutpostRuntime
    {
        private readonly ManualLogSource log;
        private readonly OutpostNative native;
        private readonly OutpostSchedule schedule = new OutpostSchedule();
        private readonly List<Observation> observations = new List<Observation>();
        private bool active, failed, confirmed;
        private int lastTick = int.MinValue;
        private long initialBypasses;
        private sealed class Observation
        {
            internal int Building, Tribe, Born, Stage;
            internal uint TribeGlobal;
            internal readonly List<Tuple<int, uint>> Units = new List<Tuple<int, uint>>();
        }
        internal OutpostRuntime(ManualLogSource log, CrusaderLibraryLoadContext context)
        { this.log = log; native = new OutpostNative(log, context); }

        internal void Begin(MissionLifecycleNotification notification)
        {
            Disable("new mission");
            var mode = notification.Context.Mode;
            active = !failed && !mode.IsRealMultiplayer && !mode.IsMapEditor &&
                !mode.HasConflictingCustomizedOrigin && mode.Kind != Shared.GameModeKind.Unknown &&
                mode.Kind != Shared.GameModeKind.Tutorial;
            native.Enabled = active;
            initialBypasses = native.Bypasses;
            Info($"session={notification.Context.SessionId} save={notification.Context.IsSave} active={active}; {mode.ToDiagnosticString()}");
        }
        internal void End(MissionLifecycleNotification _) => Disable("mission ended");
        internal void Disable(string reason)
        {
            native.Enabled = false; active = false; schedule.Clear(); observations.Clear();
            lastTick = int.MinValue; confirmed = false;
            Info("inactive: " + reason);
        }
        internal void RollbackUnpublishedInitialization() => native.RollbackUnpublishedInitialization();

        internal void Tick(int tick)
        {
            if (!active || tick == lastTick) return;
            lastTick = tick;
            try
            {
                if (!native.ProductionAllowed) return;
                ObserveFollowups(tick);
                int[] added = new int[9];
                var buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                for (int spanIndex = 0; spanIndex < buildings.Length && spanIndex < 3999; spanIndex++)
                {
                    fixed (GameBuilding* b = &buildings[spanIndex])
                    {
                        if (b->r_AliveState != AliveState.IsAlive || !OutpostSchedule.IsOutpost((int)b->r_BuildingType)) continue;
                        int buildingId = spanIndex + 1, owner = b->r_PlayerIdOwner;
                        if (owner < 1 || owner > 8) continue;
                        native.ValidateBuildingPointer(buildingId, b);
                        var entry = schedule.Observe(buildingId, b->r_GlobalId, owner, (int)b->r_BuildingType, tick);
                        if (!entry.Adopted)
                        {
                            Adopt(b, buildingId);
                            entry.Adopted = true;
                            Info($"tracking building={buildingId}/{b->r_GlobalId} type={(int)b->r_BuildingType} owner={owner} exit={b->r_TilePositionXEnd},{b->r_TilePositionYEnd} firstTick={entry.NextTick}; offsets mask=300 tribe=302/304 unit=AC/426 role=652.");
                        }
                        if (!confirmed && native.Bypasses > initialBypasses)
                        { confirmed = true; Info($"hook confirmed building={buildingId}/{b->r_GlobalId} alive={b->r_AliveState} bypasses={native.Bypasses} tick={tick}"); }
                        if (OutpostSchedule.TakeWave(entry, tick)) SpawnWave(b, buildingId, tick, added);
                    }
                }
                schedule.Prune(tick);
            }
            catch (Exception ex)
            {
                failed = true; Disable("runtime contract failure");
                Shared.DebugLogHelper.LogError(log, "OutpostTest failed closed; no further custom spawns: " + ex);
            }
        }
        private void Adopt(GameBuilding* b, int buildingId)
        {
            byte* bytes = (byte*)b;
            int tribeId = *(short*)(bytes + 0x302);
            uint global = *(uint*)(bytes + 0x304);
            if (tribeId > 0 && GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out var t) &&
                t->r_GlobalId == global && t->r_AliveState == AliveState.IsAlive)
            {
                native.ValidateTribePointer(tribeId, t);
                native.Finish(tribeId, global);
                Info($"adopt building={buildingId} previousTribe={tribeId}/{global} members={t->r_UnitsInGroup} owner={t->r_PlayerIdOwner} handoff=once");
            }
            *(short*)(bytes + 0x302) = 0;
            *(uint*)(bytes + 0x304) = 0;
        }

        private void SpawnWave(GameBuilding* b, int buildingId, int tick, int[] added)
        {
            int owner = b->r_PlayerIdOwner, x = b->r_TilePositionXEnd, y = b->r_TilePositionYEnd;
            if (x >= 800 || y >= 800) throw new InvalidOperationException("Outpost exit outside native tile bounds.");
            if (!native.HasCapacity(owner, added[owner]))
            { Info($"wave tick={tick} building={buildingId}/{b->r_GlobalId} owner={owner} requested=5 created=0 reason=player-limit"); return; }
            int tribeId = native.Allocate(owner);
            if (tribeId == 0)
            { Info($"wave tick={tick} building={buildingId}/{b->r_GlobalId} requested=5 created=0 reason=tribe-pool"); return; }
            var tribeApi = GameTribeManagerAPI.Instance;
            var unitApi = GameUnitManagerAPI.Instance;
            if (!tribeApi.TryGetTribeById(tribeId, out var tribe)) throw new InvalidOperationException("Allocated tribe missing.");
            native.ValidateTribePointer(tribeId, tribe);
            uint tribeGlobal = tribe->r_GlobalId;
            var observation = new Observation { Building = buildingId, Tribe = tribeId, TribeGlobal = tribeGlobal, Born = tick };
            bool move = false;
            string reason = "complete";
            try
            {
                if (tribe->r_PlayerIdOwner != owner || tribe->r_AliveState != AliveState.IsAlive || tribe->r_UnitsInGroup != 0)
                    throw new InvalidOperationException("Outpost tribe allocator result differs.");
                tribe->r_TribeStance = TribeStance.Aggressive;
                OutpostNative.SetRole(tribe);
                for (int i = 0; i < OutpostSchedule.WaveSize; i++)
                {
                    if (!native.HasCapacity(owner, added[owner])) { reason = "player-limit"; break; }
                    int unitId = checked((int)unitApi.CreateUnitLocal(owner, owner, x, y, 8, (eChimps)26));
                    if (unitId == 0) { reason = "unit-pool-or-cancelled"; break; }
                    if (!unitApi.TryGetUnitById(unitId, out var unit)) throw new InvalidOperationException("Spawn returned invalid unit ID.");
                    native.ValidateUnitPointer(unitId, unit);
                    // Record successful allocation even if a subsequent contract check fails.
                    observation.Units.Add(Tuple.Create(unitId, unit->r_GlobalId));
                    added[owner]++;
                    if (unit->r_AliveState != AliveState.NeedsInit && unit->r_AliveState != AliveState.IsAlive)
                        throw new InvalidOperationException("Spawn did not return an initial/live unit.");
                    if ((int)unit->r_UnitChimp != 26 || unitApi.GetOwner(unitId) != owner)
                        throw new InvalidOperationException("Another spawn modifier changed the requested Maceman/owner.");
                    if (!tribeApi.AssignUnit(tribeId, unitId) || unit->r_TribeId != tribeId ||
                        tribe->r_UnitsInGroup != observation.Units.Count)
                        throw new InvalidOperationException("Tribe assignment/count differs.");
                    OutpostNative.InitializeUnit(unit);
                }
                if (tribe->r_UnitsInGroup > 0)
                    move = tribeApi.IssueMoveHereCommand(tribeId, x, y, false, 0, TribeMoveType.NoChange);
            }
            catch
            {
                reason = "contract-failure";
                throw;
            }
            finally
            {
                // Native handoff also retires an empty allocated tribe. It deliberately
                // leaves human groups outside the AI queue; do not invent an attack order.
                native.Finish(tribeId, tribeGlobal);
                Info($"wave tick={tick} building={buildingId}/{b->r_GlobalId} type={(int)b->r_BuildingType} owner={owner} tribe={tribeId}/{tribeGlobal} requested=5 created={observation.Units.Count} members={tribe->r_UnitsInGroup} stance={tribe->r_TribeStance} role={*(short*)((byte*)tribe+0x652)} moveResult={move} handoff=called reason={reason} units={string.Join(",", observation.Units)}");
                if (observation.Units.Count > 0) observations.Add(observation);
            }
        }
        private void ObserveFollowups(int tick)
        {
            for (int i = observations.Count - 1; i >= 0; i--)
            {
                var o = observations[i];
                int age = unchecked(tick - o.Born), due = o.Stage == 0 ? 1 : o.Stage == 1 ? 40 : 200;
                if (age < due) continue;
                var states = new List<string>();
                foreach (var identity in o.Units)
                {
                    if (GameUnitManagerAPI.Instance.TryGetUnitById(identity.Item1, out var u) && u->r_GlobalId == identity.Item2)
                        states.Add($"{identity.Item1}/{identity.Item2}:type={(int)u->r_UnitChimp},alive={(int)u->r_AliveState},tribe={u->r_TribeId},state={u->r_AIState},tile={u->r_CurrentWorldPositionX/8},{u->r_CurrentWorldPositionY/8}");
                    else states.Add($"{identity.Item1}/{identity.Item2}:retired");
                }
                Info($"followup tick={tick} age={age} building={o.Building} tribe={o.Tribe}/{o.TribeGlobal} units=[{string.Join(";", states)}]");
                if (++o.Stage == 3) observations.RemoveAt(i);
            }
        }
        private void Info(string message) => Shared.DebugLogHelper.LogInfo(log, "OutpostTest " + message);
    }
}
