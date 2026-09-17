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
    internal sealed unsafe partial class OutpostRuntime
    {
        private readonly ManualLogSource log;
        private readonly OutpostNative native;
        private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
        private bool active, failed, confirmed;
        private int lastTick = int.MinValue;
        private long initialBypasses;
        private sealed class Entry
        {
            internal int Id, Owner, Type, Seen, Tribe;
            internal uint Global, TribeGlobal;
            internal bool Human;
        }
        internal OutpostRuntime(ManualLogSource log, CrusaderLibraryLoadContext context)
        { this.log = log; native = new OutpostNative(log, context); }
        internal void Begin(MissionLifecycleNotification n)
        {
            Disable("new mission");
            var m = n.Context.Mode;
            active = !failed && !m.IsRealMultiplayer && !m.IsMapEditor && !m.HasConflictingCustomizedOrigin &&
                m.Kind != Shared.GameModeKind.Unknown && m.Kind != Shared.GameModeKind.Tutorial;
            native.Enabled = active; initialBypasses = native.Bypasses;
            lock(rallyLock) RestoreRally(n.Context.IsSave);
            Info($"session={n.Context.SessionId} save={n.Context.IsSave} active={active} mode=incremental-macemen; {m.ToDiagnosticString()}");
        }
        internal void End(MissionLifecycleNotification _) => Disable("mission ended");
        internal void Disable(string reason)
        {
            native.Enabled = false; active = false; entries.Clear(); lastTick = int.MinValue; confirmed = false;
            lock(rallyLock) { rally=new OutpostRallyState(); rallyView?.Reset(); }
            // Native building fields retain unfinished groups for saves and Vanilla fallback.
            Info("inactive: " + reason);
        }
        internal void RollbackUnpublishedInitialization() => native.RollbackUnpublishedInitialization();
        private static short Read(GameBuilding* b, int offset) => *(short*)((byte*)b + offset);
        private static void Write(GameBuilding* b, int offset, int value) => *(short*)((byte*)b + offset) = checked((short)value);
        private static uint GroupGlobal(GameBuilding* b) => *(uint*)((byte*)b + 0x304);
        private static void Link(GameBuilding* b, int id, uint global)
        { Write(b, 0x302, id); *(uint*)((byte*)b+0x304) = global; }
        private bool TryGroup(int id, uint global, out GameTribe* tribe)
        {
            tribe = null;
            bool valid = id > 0 && GameTribeManagerAPI.Instance.TryGetTribeById(id, out tribe) &&
                tribe->r_GlobalId == global && tribe->r_AliveState == AliveState.IsAlive;
            if (valid) native.ValidateTribePointer(id,tribe);
            return valid;
        }
        internal void Tick(int tick)
        { lock(rallyLock) TickLocked(tick); }
        private void TickLocked(int tick)
        {
            if (!active || tick == lastTick) return;
            lastTick = tick;
            try
            {
                rallyView?.AcceptInput();
                if (!native.ProductionAllowed) return;
                ProcessRallyOrders();
                int[] added = new int[9];
                var buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                for (int spanIndex = 0; spanIndex < buildings.Length && spanIndex < 3999; spanIndex++)
                {
                    fixed (GameBuilding* b = &buildings[spanIndex])
                    {
                        if (b->r_AliveState != AliveState.IsAlive || !OutpostSchedule.IsOutpost((int)b->r_BuildingType) ||
                            b->r_PlayerIdOwner < 1 || b->r_PlayerIdOwner > 8) continue;
                        int id = spanIndex + 1;
                        native.ValidateBuildingPointer(id, b);
                        if (entries.TryGetValue(id, out var previous) &&
                            (!OutpostSchedule.SameIdentity(previous.Global,previous.Owner,previous.Type,b->r_GlobalId,b->r_PlayerIdOwner,(int)b->r_BuildingType) || previous.Human!=Human(b->r_PlayerIdOwner)))
                        {
                            Retire(previous, b, "identity-or-owner-change"); entries.Remove(id);
                        }
                        if (!entries.TryGetValue(id, out var e))
                        {
                            e = new Entry { Id=id, Global=b->r_GlobalId, Owner=b->r_PlayerIdOwner, Type=(int)b->r_BuildingType, Human=Human(b->r_PlayerIdOwner) };
                            entries.Add(id, e); if(e.Human) AdoptHuman(e,b); else Adopt(e, b);
                            Info($"tracking building={id}/{e.Global} type={e.Type} owner={e.Owner} size={Read(b,0x30E)} delay={Read(b,0x310)}");
                        }
                        e.Seen = tick;
                        if (!confirmed && native.Bypasses > initialBypasses)
                        { confirmed = true; Info($"hook confirmed building={id}/{e.Global} tick={tick} bypasses={native.Bypasses}"); }
                        if(e.Human) ProduceHuman(e,b,tick,added); else Produce(e, b, tick, added);
                    }
                }
                var removed = new List<int>();
                foreach (var pair in entries) if (pair.Value.Seen != tick)
                {
                    GameBuildingManagerAPI.Instance.TryGetBuildingById(pair.Key, out var b);
                    Retire(pair.Value, b, "building-ended"); removed.Add(pair.Key);
                }
                foreach (int id in removed) entries.Remove(id);
            }
            catch (Exception ex)
            {
                failed = true; Disable("contract failure; native group fields retained");
                Shared.DebugLogHelper.LogError(log, "OutpostTest failed closed; Vanilla resumed: " + ex);
            }
        }
        private void Adopt(Entry e, GameBuilding* b)
        {
            int id = Read(b, 0x302); uint global = GroupGlobal(b);
            if (TryGroup(id, global, out var t))
            {
                // Profile 2 is exclusively Macemen. Resume native save state only with matching owner/role.
                if (Read(b,0x30C) == 2 && t->r_PlayerIdOwner == e.Owner && *(short*)((byte*)t+0x652) == 184 && Read(b,0x30A) > 0)
                {
                    e.Tribe=id; e.TribeGlobal=global;
                    Info($"resume building={e.Id}/{e.Global} tribe={id}/{global} members={t->r_UnitsInGroup} target={Read(b,0x30A)}");
                    return;
                }
                native.Finish(id, global);
                Info($"adopt-finish building={e.Id} tribe={id}/{global} reason=previous-profile");
                Write(b,0x308,0);
            }
            Link(b,0,0);
        }
        private void Retire(Entry e, GameBuilding* current, string reason)
        {
            rally.Remove(e.Id);
            bool sameBuilding = current != null && current->r_GlobalId == e.Global;
            bool linked = sameBuilding && Read(current,0x302) == e.Tribe && GroupGlobal(current) == e.TribeGlobal;
            // Native deletion already hands off 106/107. Bedouin deletion does not.
            // Clearing a still matching link before native cleanup prevents double handoff.
            if (OutpostSchedule.FinishRetired(linked,sameBuilding,e.Type) && TryGroup(e.Tribe,e.TribeGlobal,out var t))
            {
                native.Finish(e.Tribe,e.TribeGlobal);
                Info($"retire building={e.Id}/{e.Global} tribe={e.Tribe}/{e.TribeGlobal} reason={reason} handoff=called");
            }
            if (linked) { Link(current,0,0); Write(current,0x308,0); }
            e.Tribe=0; e.TribeGlobal=0;
        }
        private void Produce(Entry e, GameBuilding* b, int tick, int[] added)
        {
            if (b->r_TilePositionXEnd >= 800 || b->r_TilePositionYEnd >= 800) throw new InvalidOperationException("Outpost exit outside native tile bounds.");
            int mode = native.ReadInt(0x8574B90);
            bool fast = native.ReadInt(0x3668E34) > 3000 && native.ReadInt(0x3669048) < 11;
            int size = Read(b,0x30E), delay = Math.Max(0,Read(b,0x310)-1);
            int interval = OutpostSchedule.SpawnWait(Read(b,0x318),size);
            Write(b,0x310,delay);
            int tribeId=Read(b,0x302); uint global=GroupGlobal(b);
            int counter=Read(b,0x308);
            if (!TryGroup(tribeId,global,out var tribe))
            {
                Link(b,0,0); e.Tribe=0; e.TribeGlobal=0;
                counter = Math.Min(30000,counter+1);
                Write(b,0x308,counter);
                if (counter < OutpostSchedule.GroupWait(Read(b,0x316),mode,fast)) return;
                if (!native.HasCapacity(e.Owner,added[e.Owner])) { Limited(e,tick,"player-limit"); return; }
                tribeId=native.Allocate(e.Owner);
                if (tribeId <= 0) { Limited(e,tick,"tribe-pool"); return; }
                if (!GameTribeManagerAPI.Instance.TryGetTribeById(tribeId,out tribe)) throw new InvalidOperationException("Allocated tribe unresolved.");
                native.ValidateTribePointer(tribeId,tribe);
                if (tribe->r_PlayerIdOwner != e.Owner || tribe->r_AliveState != AliveState.IsAlive || tribe->r_UnitsInGroup != 0)
                    throw new InvalidOperationException("Unexpected allocated tribe.");
                global=tribe->r_GlobalId; Link(b,tribeId,global); e.Tribe=tribeId; e.TribeGlobal=global;
                tribe->r_TribeStance=TribeStance.Aggressive; OutpostNative.SetRole(tribe);
                Write(b,0x30C,2);
                Write(b,0x30A,OutpostSchedule.Target(size,OutpostSchedule.Roll(e.Global,tick,2,10)));
                counter=2000; Write(b,0x308,counter);
                Info($"group-start tick={tick} building={e.Id}/{e.Global} owner={e.Owner} tribe={tribeId}/{global} target={Read(b,0x30A)} interval={interval} stance=Aggressive role=184");
            }
            e.Tribe=tribeId; e.TribeGlobal=global;
            if (tribe->r_PlayerIdOwner != e.Owner) throw new InvalidOperationException("Production tribe owner changed.");
            counter=Math.Min(30000,counter+1); Write(b,0x308,counter);
            if (counter < interval) return;
            Write(b,0x308,OutpostSchedule.Roll(e.Global,tick,40,40));
            int target=Read(b,0x30A), before=tribe->r_UnitsInGroup;
            if (OutpostSchedule.DelayBlocks(before,target,delay)) return;
            if (!native.HasCapacity(e.Owner,added[e.Owner])) { Limited(e,tick,"player-limit"); return; }
            int requested=OutpostSchedule.Batch(before,target,delay,native.ReadInt(0x379D0D0+e.Owner*0x583C) != 0);
            int created=0; bool move=false; string reason="complete";
            for (int i=0;i<requested;i++)
            {
                if (!native.HasCapacity(e.Owner,added[e.Owner])) { reason="player-limit"; break; }
                int unitId=checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(e.Owner,e.Owner,b->r_TilePositionXEnd,b->r_TilePositionYEnd,8,(eChimps)26));
                if (unitId == 0) { reason="unit-pool-or-cancelled"; break; }
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId,out var u)) throw new InvalidOperationException("Created unit unresolved.");
                native.ValidateUnitPointer(unitId,u); added[e.Owner]++; created++;
                if ((int)u->r_UnitChimp != 26 || GameUnitManagerAPI.Instance.GetOwner(unitId) != e.Owner ||
                    (u->r_AliveState != AliveState.NeedsInit && u->r_AliveState != AliveState.IsAlive))
                    throw new InvalidOperationException("Created unit contract changed.");
                int members=tribe->r_UnitsInGroup;
                if (!GameTribeManagerAPI.Instance.AssignUnit(tribeId,unitId) || u->r_TribeId != tribeId || tribe->r_UnitsInGroup != members+1)
                    throw new InvalidOperationException("Assignment/count mismatch.");
                OutpostNative.InitializeUnit(u);
                Info($"spawn tick={tick} building={e.Id}/{e.Global} tribe={tribeId}/{global} unit={unitId}/{u->r_GlobalId} type=26 members={tribe->r_UnitsInGroup}/{target} state={u->r_AIState}");
            }
            if (created > 0) move=GameTribeManagerAPI.Instance.IssueMoveHereCommand(tribeId,b->r_TilePositionXEnd,b->r_TilePositionYEnd,false,0,TribeMoveType.NoChange);
            bool complete=OutpostSchedule.Complete(tribe->r_UnitsInGroup,target,delay);
            if (complete || tribe->r_UnitsInGroup == 0)
            {
                native.Finish(tribeId,global); Link(b,0,0); e.Tribe=0; e.TribeGlobal=0;
                if (complete)
                { Write(b,0x316,Math.Min(2000,Read(b,0x316)+(fast?100:33))); Write(b,0x318,Math.Min(150,Read(b,0x318)+4)); }
                Info($"group-finish tick={tick} building={e.Id}/{e.Global} tribe={tribeId}/{global} members={tribe->r_UnitsInGroup}/{target} handoff=called reason={(complete?"target-reached":"empty-failed-spawn")}");
            }
            Info($"production tick={tick} building={e.Id}/{e.Global} requested={requested} created={created} members={tribe->r_UnitsInGroup}/{target} moveResult={move} handedOff={complete} reason={reason}");
        }
        private void Limited(Entry e,int tick,string reason)
        { if (tick % 200 == 0) Info($"limited tick={tick} building={e.Id}/{e.Global} reason={reason}"); }
        private void Info(string text) => Shared.DebugLogHelper.LogInfo(log,"OutpostTest "+text);
    }
}
