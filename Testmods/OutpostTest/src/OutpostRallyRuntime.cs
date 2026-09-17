using System;
using System.Collections.Generic;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace OutpostTest
{
    internal sealed unsafe partial class OutpostRuntime
    {
        private readonly object rallyLock=new object();
        private OutpostRallyState rally=new OutpostRallyState();
        private byte[] loadedRally;
        private readonly List<IDisposable> rallySubscriptions=new List<IDisposable>();
        private OutpostRallyView rallyView;
        private bool rallyRegistered, dispatchingRally, frameFailed;
        private int dispatchTribe;
        private long? moveResult;
        internal void RegisterRally()
        {
            if(rallyRegistered) return;
            rallyView=new OutpostRallyView(this);
            // Runtime is statically rooted before any of these publishers receive it.
            rallySubscriptions.Add(InputR3EventHooks.OnKeyDown.Observable.Subscribe(args=>
            { try { if(active) rallyView.Input(args); } catch(Exception ex) { RallyFailure(ex); } }));
            rallySubscriptions.Add(TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable.Subscribe(args=>
            {
                lock(rallyLock) {
                    if(dispatchingRally && args.TribeId==dispatchTribe) {
                        if(args.Phase==EventHookPhase.Post) moveResult=args.ReturnValue;
                    } else if(args.Phase==EventHookPhase.Pre && args.IsNewOrder) rally.CancelTribe(args.TribeId);
                }
            }));
            rallySubscriptions.Add(TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable.Subscribe(args=>
            { if(args.Phase==EventHookPhase.Pre) lock(rallyLock) rally.CancelTribe(args.TribeId); }));
            rallySubscriptions.Add(TribeR3EventHooks.OnTribeAssignUnit.Observable.Subscribe(args=>
            {
                if(args.Phase!=EventHookPhase.Pre) return;
                lock(rallyLock) {
                    rally.Pending.RemoveAll(p=>p.Unit==args.UnitId && p.Tribe!=args.TribeId || p.Tribe==args.TribeId && p.Unit!=args.UnitId);
                }
            }));
            if(!ModSaveDataAPI.Instance.RegisterModDataHandler(OutpostTestPlugin.Guid,
                context=> { lock(rallyLock) return context.IsSaveFile ? rally.Encode() : null; },
                (bytes,context)=> { if(context.IsSaveFile) lock(rallyLock) loadedRally=(byte[])bytes.Clone(); },
                ()=> { lock(rallyLock) { loadedRally=null; rally=new OutpostRallyState(); rallyView?.Reset(); } }))
                throw new InvalidOperationException("Outpost rally save handler already registered.");
            rallyRegistered=true;
            Info("rally ready: middle click, per-outpost save data, Hold and one-shot native running orders");
        }
        internal void FailInitialization()
        { failed=true; Disable("initialization failure"); }
        private void RallyFailure(Exception ex)
        {
            failed=true; Disable("rally failure; Vanilla resumed");
            Shared.DebugLogHelper.LogError(log,"OutpostTest rally failure: "+ex);
        }
        private static bool Human(int owner) => owner>=1 && owner<=8 && !GamePlayerManagerAPI.Instance.IsAIPlayer(owner);
        private bool ValidBuilding(OutpostRallyState.Record r,out GameBuilding* b)
        {
            b=null;
            return GameBuildingManagerAPI.Instance.TryGetBuildingById(r.Building,out b) && b->r_AliveState==AliveState.IsAlive &&
                r.Matches(b->r_GlobalId,b->r_PlayerIdOwner,(int)b->r_BuildingType) && Human(r.Owner);
        }
        internal bool TrySelected(out int id,out uint global,out int owner,out int type)
        {
            id=0;global=0;owner=0;type=0;
            if(!active) return false;
            id=GamePlayerManagerAPI.Instance.GetSelectedBuildingId();
            if(!GameBuildingManagerAPI.Instance.TryGetBuildingById(id,out var b) || b->r_AliveState!=AliveState.IsAlive ||
                !OutpostSchedule.IsOutpost((int)b->r_BuildingType) || b->r_PlayerIdOwner!=GamePlayerManagerAPI.Instance.GetLocalPlayerId() || !Human(b->r_PlayerIdOwner)) return false;
            global=b->r_GlobalId;owner=b->r_PlayerIdOwner;type=(int)b->r_BuildingType;return true;
        }
        internal bool SetRally(int id,uint global,int owner,int type,int x,int y)
        {
            lock(rallyLock) {
                if(!TrySelected(out int selected,out uint g,out int o,out int t) || selected!=id ||
                    !OutpostSchedule.SameIdentity(global,owner,type,g,o,t) || !GameTileManagerAPI.Instance.IsTileInsideMapBounds(x,y)) return false;
                var r=rally.Get(id,global,owner,type); r.HasPoint=true;r.X=x;r.Y=y;
                Info($"rally-set building={id}/{global} owner={owner} target={x},{y}; future-spawns-only");return true;
            }
        }
        internal void PresentRally()
        {
            if(frameFailed || !rallyRegistered) return;
            try {
                lock(rallyLock) {
                    rallyView.AcceptInput();
                    OutpostRallyState.Record selected=null;
                    if(TrySelected(out int id,out uint global,out int owner,out int type) &&
                        rally.Records.TryGetValue(id,out var r) && r.Matches(global,owner,type) && r.HasPoint) selected=r;
                    rallyView.Present(selected);
                }
            } catch(Exception ex) { frameFailed=true; RallyFailure(ex); }
        }
        private void RestoreRally(bool isSave)
        {
            byte[] bytes=loadedRally; loadedRally=null;
            if(!active || !isSave || bytes==null) return;
            try {
                var restored=OutpostRallyState.Decode(bytes);
                var invalid=new List<int>();
                foreach(var r in restored.Records.Values)
                    if(!ValidBuilding(r,out var b) || r.HasPoint && !GameTileManagerAPI.Instance.IsTileInsideMapBounds(r.X,r.Y)) invalid.Add(r.Building);
                foreach(int id in invalid) restored.Remove(id);
                restored.Pending.RemoveAll(p=>!ValidPending(p,out var u,out var t));
                rally=restored;
                Info($"rally-load records={rally.Records.Count} pending={rally.Pending.Count} invalidBuildings={invalid.Count}");
            } catch(Exception ex) {
                // Do not silently discard saved targets or resume with changed cycle timing.
                RallyFailure(new InvalidOperationException("Rally save could not be restored.",ex));
            }
        }
        private bool ValidPending(OutpostRallyState.Order p,out GameUnit* u,out GameTribe* t)
        {
            u=null;t=null;
            return GameUnitManagerAPI.Instance.TryGetUnitById(p.Unit,out u) && u->r_GlobalId==p.UnitGlobal &&
                (u->r_AliveState==AliveState.NeedsInit || u->r_AliveState==AliveState.IsAlive) && u->r_CurrentHealth>0 &&
                GameUnitManagerAPI.Instance.GetOwner(p.Unit)==p.Owner && Human(p.Owner) &&
                TryGroup(p.Tribe,p.TribeGlobal,out t) && t->r_PlayerIdOwner==p.Owner && t->r_UnitsInGroup==1 && t->r_TribeStance==TribeStance.Hold && u->r_TribeId==p.Tribe;
        }
        private void ProcessRallyOrders()
        {
            var invalid=new List<int>();
            foreach(var r in rally.Records.Values) if(!ValidBuilding(r,out var b)) invalid.Add(r.Building);
            foreach(int id in invalid) rally.Remove(id);
            // Remove before dispatch: a reentrant native/event callback cannot execute it twice.
            for(int i=rally.Pending.Count-1;i>=0;i--)
            {
                var p=rally.Pending[i];
                if(!ValidPending(p,out var u,out var tribe)) { rally.Pending.RemoveAt(i); continue; }
                if(!OutpostRallyState.Ready(u->r_AliveState==AliveState.NeedsInit,u->r_AliveState==AliveState.IsAlive,tribe->r_UnitsInGroup,u->r_TribeId,p.Tribe))
                { if(++p.Wait>=80) { rally.Pending.RemoveAt(i);Info($"rally-cancel unit={p.Unit}/{p.UnitGlobal} reason=initialization-timeout"); } continue; }
                rally.Pending.RemoveAt(i); dispatchingRally=true;dispatchTribe=p.Tribe;moveResult=null;
                try {
                    native.RunTo(p.Tribe,p.X,p.Y);
                    Info($"rally-move unit={p.Unit}/{p.UnitGlobal} tribe={p.Tribe}/{p.TribeGlobal} target={p.X},{p.Y} stance={tribe->r_TribeStance} nativeResult={(moveResult.HasValue?moveResult.Value.ToString():"deferred-or-unobserved")} state={u->r_AIState}");
                } finally { dispatchingRally=false;dispatchTribe=0; }
                // Native handlers may cancel other pending records; restart safely.
                i=Math.Min(i,rally.Pending.Count);
            }
        }
        private void AdoptHuman(Entry e,GameBuilding* b)
        {
            var r=rally.Get(e.Id,e.Global,e.Owner,e.Type);
            int id=Read(b,0x302);uint global=GroupGlobal(b);
            if(TryGroup(id,global,out var tribe)) {
                if(r.Target==0 && Read(b,0x30C)==2 && Read(b,0x30A)>0) {
                    r.Target=Read(b,0x30A);r.Produced=Math.Min(r.Target,tribe->r_UnitsInGroup);
                }
                native.Finish(id,global);
                Info($"human-adopt building={e.Id}/{e.Global} previousTribe={id}/{global} existingUnitsUnchanged=True");
            }
            Link(b,0,0);e.Tribe=0;e.TribeGlobal=0;
        }
        private void ProduceHuman(Entry e,GameBuilding* b,int tick,int[] added)
        {
            var r=rally.Get(e.Id,e.Global,e.Owner,e.Type);
            if(!OutpostRallyState.PointValid(b->r_TilePositionXEnd,b->r_TilePositionYEnd)) throw new InvalidOperationException("Invalid outpost exit.");
            bool fast=native.ReadInt(0x3668E34)>3000 && native.ReadInt(0x3669048)<11;
            int size=Read(b,0x30E), interval=OutpostSchedule.SpawnWait(Read(b,0x318),size);
            int delay=Math.Max(0,Read(b,0x310)-1); Write(b,0x310,delay);
            int counter=Math.Min(30000,Read(b,0x308)+1);Write(b,0x308,counter);
            if(r.Target==0) {
                if(counter<OutpostSchedule.GroupWait(Read(b,0x316),native.ReadInt(0x8574B90),fast)) return;
                if(!native.HasCapacity(e.Owner,added[e.Owner])) { Limited(e,tick,"player-limit");return; }
                r.Target=OutpostSchedule.Target(size,OutpostSchedule.Roll(e.Global,tick,2,10));r.Produced=0;
                counter=2001;Write(b,0x308,counter);Write(b,0x30A,r.Target);Write(b,0x30C,2);
                Info($"human-cycle tick={tick} building={e.Id}/{e.Global} target={r.Target}");
            }
            if(counter<interval) return;
            Write(b,0x308,OutpostSchedule.Roll(e.Global,tick,40,40));
            if(r.Produced<r.Target) {
                if(OutpostSchedule.DelayBlocks(r.Produced,r.Target,delay)) return;
                if(!native.HasCapacity(e.Owner,added[e.Owner])) { Limited(e,tick,"player-limit");return; }
                if(!SpawnHuman(e,b,r,tick,added)) return;
            }
            if(OutpostSchedule.Complete(r.Produced,r.Target,delay)) {
                Info($"human-cycle-finish tick={tick} building={e.Id}/{e.Global} produced={r.Produced}/{r.Target}");
                r.Target=r.Produced=0;
                Write(b,0x316,Math.Min(2000,Read(b,0x316)+(fast?100:33)));Write(b,0x318,Math.Min(150,Read(b,0x318)+4));
            }
        }
        private bool SpawnHuman(Entry e,GameBuilding* b,OutpostRallyState.Record r,int tick,int[] added)
        {
            var tribes=GameTribeManagerAPI.Instance;
            int tribeId=checked((int)tribes.Create(e.Owner,false));
            if(tribeId<=0) { Limited(e,tick,"tribe-pool");return false; }
            if(!tribes.TryGetTribeById(tribeId,out var tribe)) throw new InvalidOperationException("Human tribe unresolved.");
            native.ValidateTribePointer(tribeId,tribe);uint global=tribe->r_GlobalId;
            if(tribe->r_PlayerIdOwner!=e.Owner || tribe->r_UnitsInGroup!=0 || tribe->r_AliveState!=AliveState.IsAlive)
                throw new InvalidOperationException("Human tribe allocator contract changed.");
            try {
                int id=checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(e.Owner,e.Owner,b->r_TilePositionXEnd,b->r_TilePositionYEnd,8,(eChimps)26));
                if(id==0) { Info($"human-spawn tick={tick} building={e.Id} created=0 reason=unit-pool-or-cancelled");return false; }
                added[e.Owner]++;
                if(!GameUnitManagerAPI.Instance.TryGetUnitById(id,out var u)) throw new InvalidOperationException("Human unit unresolved.");
                native.ValidateUnitPointer(id,u);
                if((int)u->r_UnitChimp!=26 || GameUnitManagerAPI.Instance.GetOwner(id)!=e.Owner ||
                    (u->r_AliveState!=AliveState.NeedsInit && u->r_AliveState!=AliveState.IsAlive)) throw new InvalidOperationException("Human spawn contract changed.");
                if(!tribes.AssignUnit(tribeId,id) || u->r_TribeId!=tribeId || tribe->r_UnitsInGroup!=1) throw new InvalidOperationException("Human assignment failed.");
                OutpostNative.InitializeUnit(u);OutpostNative.SetRole(tribe);
                if(!tribes.SetStance(tribeId,TribeStance.Hold)) throw new InvalidOperationException("Human Hold failed.");
                r.Produced++;rally.Queue(r,id,u->r_GlobalId,tribeId,global);
                Info($"human-spawn tick={tick} building={e.Id}/{e.Global} unit={id}/{u->r_GlobalId} tribe={tribeId}/{global} stance=Hold produced={r.Produced}/{r.Target} rally={(r.HasPoint?r.X+","+r.Y:"none")}");
                return true;
            } finally {
                if(tribes.TryGetTribeById(tribeId,out var remaining) && remaining->r_GlobalId==global && remaining->r_UnitsInGroup==0)
                    tribes.DeleteTribeSafe(tribeId);
            }
        }
    }
}
