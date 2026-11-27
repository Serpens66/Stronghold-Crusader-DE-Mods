using System;
using System.Collections.Generic;
using System.Reflection;
using APIShared.Units;
using APIShared.WorldMutations;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest.Tests
{
    // Real coordinator, queue, join, typed receipts and codecs. Only the engine and
    // admitted intent boundary are fake; native validation has its own928 checks.
    internal static class ReplanCoordinatorFixture
    {
        private static int checks;
        private static void Check(bool value,string why){checks++;if(!value)throw new Exception("Replan coordinator: "+why);}
        private class Engine : IRescueMutationFootprintWorld, IRescueMutationEnvelopeWorld
        {
            internal long Session=9,Generation=3;
            internal int Now=100,RescueCalls,WorldReads,TickReads,CaptureCalls,EnvelopeCalls;
            internal bool RescueSuccess=true;
            internal RescueStatus RescueFailureStatus=RescueStatus.NoCandidate;
            internal IReadOnlyList<RescueTile> PlannedWrites=Array.Empty<RescueTile>();
            internal bool CaptureSupported=true,EnvelopeSupported=true,ThrowEnvelope;
            internal Action<UnitSnapshot> DuringRescue;
            internal readonly List<UnitSnapshot> Captured=new List<UnitSnapshot>();
            internal readonly List<UnitSnapshot> Rescued=new List<UnitSnapshot>();
            public bool TryCapture(WorldMutationContext c,out List<UnitSnapshot> before,out string reason)
            {CaptureCalls++;before=new List<UnitSnapshot>(Captured);reason=CaptureSupported?null:"fixture capture failure";return CaptureSupported;}
            public bool TryCapture(WorldMutationContext c,out List<UnitSnapshot> before,out IReadOnlyList<RescueTile> plannedWrites,out string reason)
            {plannedWrites=PlannedWrites;return TryCapture(c,out before,out reason);}
            public bool TryPlan(WorldMutationContext c,out IReadOnlyList<RescueTile> plannedWrites,out string reason)
            {EnvelopeCalls++;if(ThrowEnvelope)throw new Exception("fixture envelope failure");plannedWrites=PlannedWrites;reason=null;return EnvelopeSupported;}
            public bool HasChangedTrapCandidate(UnitSnapshot before)=>true;
            public virtual bool TryRescue(UnitSnapshot before,string cause,bool resume,out RescueResult result)
            {RescueCalls++;Rescued.Add(before);DuringRescue?.Invoke(before);result=new RescueResult{Status=RescueSuccess?RescueStatus.Rescued:RescueFailureStatus};return RescueSuccess;}
            public bool TryGetWorld(out long session,out long generation){WorldReads++;session=Session;generation=Generation;return true;}
            public bool TryGetTick(out int tick){TickReads++;tick=Now;return true;}
        }
        private sealed class IdleEngine : Engine,IRescuePassiveIdleWorld
        {
            internal readonly Dictionary<int,UnitSnapshot> Idle=new Dictionary<int,UnitSnapshot>();
            internal int IdleCaptures,IdleChecks;
            internal readonly HashSet<int> ReadIds=new HashSet<int>();
            internal bool IdleAvailable=true,CurrentIdle=true,ThrowIdle,Dead,Projectile;
            internal readonly HashSet<int> Settling=new HashSet<int>();
            public bool IsKnownIdleTransition(RescueGeometryWitness witness)=>witness!=null && Settling.Contains(witness.UnitId);
            internal Action DuringIdleCapture;
            public bool TryCaptureIdle(int id,out UnitSnapshot idle)
            {
                IdleCaptures++;ReadIds.Add(id);idle=null;if(ThrowIdle)throw new InvalidOperationException("idle fixture failure");
                DuringIdleCapture?.Invoke();
                return IdleAvailable && !Dead && !Projectile && Idle.TryGetValue(id,out idle) &&
                    RescueGeometryWitness.IsIndependentIdleSnapshot(idle) && IsCurrentIdle(idle);
            }
            public unsafe bool IsCurrentIdle(UnitSnapshot idle)
            {
                IdleChecks++;if(idle!=null)ReadIds.Add(idle.Id);
                if(idle==null || !Idle.TryGetValue(idle.Id,out var live))return false;
                GameUnit record=default;record.r_AliveState=Dead?AliveState.MarkedForDeletion:AliveState.IsAlive;
                record.r_IsKilledByProjectile=(ushort)(Projectile?1:0);record.r_CurrentHealth=live.Health;record.r_GlobalId=live.GlobalId;
                record.r_UnitChimp=(eChimps)live.Type;record.r_ControllableForPlayerId=(ushort)live.Owner;record.r_AIState=(ushort)live.AIState;
                return CurrentIdle && NativeRescueMutationWorld.IsLivingIdleIdentity(idle,&record) &&
                    RescueGeometryWitness.IsIndependentIdleSnapshot(live) && idle.Tribe==live.Tribe && idle.TribeGlobalId==live.TribeGlobalId &&
                    idle.X==live.X && idle.Y==live.Y && idle.TileId==live.TileId && idle.AIState==live.AIState &&
                    idle.Command.Matches((eChimps)live.Type,live.AIState,
                        at=>(ushort)(live.Command.TryGetValue(at,out uint wordValue)?wordValue:0),
                        at=>live.Command.TryGetValue(at,out uint dwordValue)?dwordValue:0);
            }
            public override bool TryRescue(UnitSnapshot before,string cause,bool resume,out RescueResult result)
            {
                // Exercise the existing production transaction-health clone at
                // the fake engine boundary; no simulated health write occurs.
                if(Idle.TryGetValue(before.Id,out var live))before=before.WithCurrentHealth(live.Health);
                return base.TryRescue(before,cause,resume,out result);
            }
        }
        private sealed class IntentBoundary : IRescueReplanIntentWorld
        {
            internal int Captures,Refreshes;
            internal readonly Dictionary<int,UnitSnapshot> Live=new Dictionary<int,UnitSnapshot>();
            internal readonly HashSet<int> Rejected=new HashSet<int>();
            internal bool CaptureAllowed=true,ThrowCapture,ThrowRefresh;
            public bool TryCapture(int id,int x,int y,int extra,out RescueReplanIntent intent)
            {
                Captures++;intent=null;if(ThrowCapture)throw new InvalidOperationException("fixture capture failure");
                if(!CaptureAllowed || extra!=0 || !Live.TryGetValue(id,out var u) || u.RequestedX!=x || u.RequestedY!=y || u.Command==null)return false;
                var receipt=WorkerResumeTicket.ForCommand(u.Id,u.GlobalId,(eChimps)u.Type,u.AIState,(ushort)u.Owner,u.Tribe,u.TribeGlobalId,
                    (ushort)u.RequestedX,(ushort)u.RequestedY,u.Command);
                if(!receipt.IsValid)return false;intent=new RescueReplanIntent(u,receipt);return true;
            }
            public bool IsCurrent(RescueReplanIntent intent)=>intent!=null && !Rejected.Contains(intent.UnitId);
            public bool TryGetSnapshot(RescueReplanIntent intent,out UnitSnapshot exact)
            {
                Refreshes++;exact=null;
                if(ThrowRefresh)throw new InvalidOperationException("fixture validation failure");
                if(!IsCurrent(intent))return false;
                exact=intent.CopyBefore();return true;
            }
        }
        private sealed class Harness
        {
            internal readonly Engine Engine;
            internal readonly IntentBoundary Intents=new IntentBoundary();
            internal readonly GeneralRescueObserver Observer;
            private long batch,move;
            internal Harness(bool active=true,Engine engine=null)
            {Engine=engine??new Engine();Observer=new GeneralRescueObserver(Engine,null,null){ReplanIntents=Intents,ReplanOwnerGuid="ours",OwnReplanScope=()=>false};if(active)Observer.SetSession(Engine.Session);}
            internal void Enqueue(params UnitSnapshot[] units)
            {
                Engine.Captured.Clear();Engine.Captured.AddRange(units);
                var context=Mutation(++batch,Engine.Session,Engine.Generation);var receipt=Observer.TryBegin(context);
                Check(receipt!=null,"observed mutation captured");receipt.Complete(MutationEnd(context));Observer.OnBatchCompleted(BatchEnd(batch,Engine.Session,Engine.Generation));
            }
            internal IUnitMoveInvocation Begin(UnitSnapshot fresh,out UnitMoveContext context,int extra=0)
            {
                Intents.Live[fresh.Id]=fresh;
                // Production observer priority: canonical effective order invalidation
                // precedes P7's capture of the final-original immutable context.
                Observer.ObserveOrder(fresh.Id);
                context=Move(++move,Engine.Session,Engine.Generation,fresh.Id,fresh.RequestedX,fresh.RequestedY,extra);
                return Observer.TryBegin(context);
            }
            internal void Fail(UnitSnapshot fresh,int extra=0)
            {var receipt=Begin(fresh,out var context,extra);Check(receipt!=null,"retained causal geometry interests actual original");receipt.Complete(MoveEnd(context));}
            internal void Tick(int tick){Engine.Now=tick;if(Engine is IdleEngine idle)idle.ReadIds.Clear();Observer.Tick(tick);}
        }
        private static UnitSnapshot Old(int id=1,bool step=false)
        {
            int x=20+id%20,y=30+id/20,tile=1000+id*3;
            var u=new UnitSnapshot{Id=id,GlobalId=(uint)(1000+id),Type=(int)eChimps.CHIMP_TYPE_SPEARMAN,Owner=1,
                X=x,Y=y,TileId=tile,NextX=step?x+1:x,NextY=y,NextTileId=step?tile+1:tile,
                Flags=step?2:0,Substep=step?3:8,OrdinaryStep=step,AIState=step?101:0,Health=100,
                RequestedX=70,RequestedY=80,AffectedCurrent=true,AffectedNext=step,CurrentWasOrdinary=true,NextWasOrdinary=true,
                CrewIdentity=Array.Empty<uint>()};
            u.Candidates.Add(new RescueCandidate{X=x,Y=y,Tile=tile,Distance=0});
            u.Candidates.Add(new RescueCandidate{X=x+1,Y=y,Tile=tile+1,Distance=1});
            u.Candidates.Add(new RescueCandidate{X=x-8,Y=y-8,Tile=tile+2,Distance=8});
            u.Command=Command(u,1);return u;
        }
        private static UnitSnapshot Fresh(UnitSnapshot old,bool atNext=false,ushort purpose=9)
        {
            var u=RescueReplanIntent.CopySnapshot(old);u.GeometryWitness=null;u.Candidates.Clear();
            if(atNext){u.X=old.NextX;u.Y=old.NextY;u.TileId=old.NextTileId;}
            u.NextX=u.X;u.NextY=u.Y;u.NextTileId=u.TileId;u.Flags=2;u.Substep=8;u.OrdinaryStep=false;
            u.AIState=101;u.AffectedCurrent=false;u.AffectedNext=false;u.CurrentWasOrdinary=false;u.NextWasOrdinary=false;
            u.GroundFlags=0x40000000u;u.NextGroundFlags=u.GroundFlags;u.Command=Command(u,purpose);return u;
        }
        private static CommandJourneySnapshot Command(UnitSnapshot u,ushort purpose,int target=0)
        {
            Func<int,ushort> word=offset=>offset==target?(ushort)1:offset==0x6E4?(ushort)AliveState.IsAlive:offset==0x6E6?(ushort)u.Type:
                offset==0x6EE?(ushort)u.Owner:offset==0x918?(ushort)u.AIState:offset==0x930?(ushort)u.Tribe:
                offset==0x958?purpose:offset==0xA9E?(ushort)1:(ushort)0;
            Func<int,uint> dword=offset=>offset==target?1u:offset==0x6F0?u.GlobalId:offset==0x93C?u.TribeGlobalId:0u;
            CommandJourneySnapshot command;
            bool valid=u.Flags==0?CommandJourneySnapshot.TryCaptureStationary((eChimps)u.Type,u.AIState,word,dword,out command):
                CommandJourneySnapshot.TryCapture((eChimps)u.Type,u.AIState,word,dword,out command);
            Check(valid,"real typed command fixture");return command;
        }
        private static WorldMutationContext Mutation(long batch,long session,long generation)=>(WorldMutationContext)Activator.CreateInstance(typeof(WorldMutationContext),
            BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{WorldMutationSource.BuildStructure,batch,0L,batch,session,generation,new long[]{123,1,2,3,4,1,0,0}},null);
        private static WorldMutationCompletion MutationEnd(WorldMutationContext context)
        {
            var c=(WorldMutationCompletion)Activator.CreateInstance(typeof(WorldMutationCompletion),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{context},null);
            typeof(WorldMutationCompletion).GetMethod("Set",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{true,true,0L,null});return c;
        }
        private static WorldMutationBatchCompletion BatchEnd(long batch,long session,long generation)
        {
            var c=(WorldMutationBatchCompletion)Activator.CreateInstance(typeof(WorldMutationBatchCompletion),BindingFlags.Instance|BindingFlags.NonPublic,null,
                new object[]{batch,session,generation,WorldMutationSource.BuildStructure},null);
            typeof(WorldMutationBatchCompletion).GetMethod("Set",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{true,false,true});return c;
        }
        private static UnitMoveContext Move(long invocation,long session,long generation,int id,int x,int y,int extra=0,string owner=null)=>
            (UnitMoveContext)Activator.CreateInstance(typeof(UnitMoveContext),BindingFlags.Instance|BindingFlags.NonPublic,null,
                new object[]{invocation,0L,session,generation,id,x,y,extra,owner},null);
        private static UnitMoveCompletion MoveEnd(UnitMoveContext context,long actual=0,bool invoked=true,bool returned=true,bool current=true,Exception failure=null)
        {
            var c=(UnitMoveCompletion)Activator.CreateInstance(typeof(UnitMoveCompletion),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{context},null);
            typeof(UnitMoveCompletion).GetMethod("Set",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{invoked,returned,actual,failure,current});return c;
        }
        private static void SpearmanOneIndependentIdle()
        {
            Check(CommandJourneySnapshot.IsIndependentIdlePhase(eChimps.CHIMP_TYPE_SPEARMAN,1),"exact SpearmanAI1 is an independently audited idle animation");
            foreach(var type in new[]{eChimps.CHIMP_TYPE_ARCHER,eChimps.CHIMP_TYPE_XBOWMAN,eChimps.CHIMP_TYPE_PIKEMAN,eChimps.CHIMP_TYPE_ENGINEER})
                Check(!CommandJourneySnapshot.IsIndependentIdlePhase(type,1),"SpearmanAI1 does not grant any other type'sAI1 independent authority");
            var e=new IdleEngine();var h=new Harness(engine:e);var old=Fresh(Old(44));old.CurrentWasOrdinary=true;old.AffectedCurrent=true;
            old.Candidates.Add(new RescueCandidate{X=old.X+1,Y=old.Y,Tile=old.TileId+1,Distance=1});h.Enqueue(old);
            var phase1=Idle(old);phase1.AIState=1;phase1.Command=Command(phase1,0);e.Idle[phase1.Id]=phase1;h.Tick(100);
            Check(h.Observer.TryCapturePending(out var pending) && e.RescueCalls==0 && pending.Length==1 &&
                pending[0].Snapshot.AIState==1 && pending[0].Snapshot.RequestedX==phase1.X && pending[0].Attempts==0 && pending[0].ActiveAge==0,
                "fresh Spearman1 joins original geometry without replaying the old101 goal");
            var saved=new RescuePendingCandidate<UnitSnapshot>(pending[0].Source,pending[0].Snapshot,200,2,0,RescueQueueLifetimeMode.ActiveWindow,7,false);
            Check(RescuePendingCodec.TryEncode(saved,out var bytes),"Spearman1 encodes using the existing production codec");
            Check(RescuePendingCodec.TryDecode(bytes,out var decoded) && decoded.Snapshot.AIState==1 && decoded.Age==200 && decoded.ActiveAge==7 && decoded.Attempts==2 &&
                RescueGeometryCodec.CanonicalJoin(decoded.Snapshot),"existing production codec preserves Spearman1 authority and old source/window/attempts without a schema change");
            var loadedEngine=new IdleEngine();var loaded=new Harness(engine:loadedEngine);loadedEngine.Idle[phase1.Id]=phase1;loaded.Observer.StagePending(new[]{decoded});
            loaded.Tick(500);
            Check(loadedEngine.RescueCalls==1 && loadedEngine.Rescued[0].AIState==1 && loadedEngine.Rescued[0].RequestedX==phase1.X &&
                old.AIState==101 && old.RequestedX!=phase1.X && loaded.Observer.PendingCount==0,
                "loaded current Spearman1 performs only fresh stationary rescue, preserving its current AI and goal");
            foreach(int target in new[]{0x996,0x99C,0xA00})
            {
                var blocked=new IdleEngine();var b=new Harness(engine:blocked);b.Observer.StagePending(new[]{decoded});
                var aimed=RescueReplanIntent.CopySnapshot(phase1);aimed.Command=Command(aimed,0,target);blocked.Idle[aimed.Id]=aimed;
                b.Tick(500);b.Tick(501);Check(blocked.RescueCalls==0,"Spearman1 with a concrete target never acquires independent idle authority");
            }
        }
        private static void PassiveIdleHealthAndAnimation()
        {
            foreach(uint health in new uint[]{60,120})
            {
                var e=new IdleEngine();var h=new Harness(engine:e);var old=Fresh(Old(41));old.CurrentWasOrdinary=true;old.AffectedCurrent=true;
                old.Candidates.Add(new RescueCandidate{X=old.X+1,Y=old.Y,Tile=old.TileId+1,Distance=1});h.Enqueue(old);
                var idle=Idle(old);e.Idle[idle.Id]=idle;h.Tick(100);
                Check(h.Observer.TryCapturePending(out var saved) && saved.Length==1,"queued fresh idle health fixture captured");
                e.Idle[idle.Id]=idle.WithCurrentHealth(health);
                Check(h.Observer.TryCapturePending(out var changedHealth) && changedHealth.Length==1 &&
                    changedHealth[0].Age==saved[0].Age && changedHealth[0].ActiveAge==saved[0].ActiveAge && changedHealth[0].Attempts==saved[0].Attempts,
                    "positive health change neither invalidates save authority nor renews proof time");
                h.Tick(101);
                Check(e.RescueCalls==1 && e.Rescued[0].Health==health && saved[0].Snapshot.Health==100 &&
                    ReferenceEquals(e.Rescued[0].GeometryWitness,saved[0].Snapshot.GeometryWitness),
                    "real current-health clone at the engine boundary preserves damage/heal and old spatial witness");
            }
            foreach(int invalid in new[]{0,1,2,3,4})
            {
                var e=new IdleEngine();var h=new Harness(engine:e);var old=Fresh(Old(42));old.CurrentWasOrdinary=true;old.AffectedCurrent=true;
                old.Candidates.Add(new RescueCandidate{X=old.X+1,Y=old.Y,Tile=old.TileId+1,Distance=1});h.Enqueue(old);var idle=Idle(old);e.Idle[idle.Id]=idle;h.Tick(100);
                if(invalid==0)e.Idle[idle.Id]=idle.WithCurrentHealth(0);
                if(invalid==1)e.Projectile=true;
                if(invalid==2)e.Dead=true;
                if(invalid==3){var reused=RescueReplanIntent.CopySnapshot(idle);reused.GlobalId++;e.Idle[idle.Id]=reused;}
                if(invalid==4)h.Observer.ObserveOrder(idle.Id);
                h.Tick(101);h.Tick(102);
                Check(e.RescueCalls==0,"zero/projectile/dead/reuse/new order cannot inherit idle health permission");
            }
            var animation=new IdleEngine();var moving=new Harness(engine:animation);var source=Fresh(Old(43));source.Type=(int)eChimps.CHIMP_TYPE_ARCHER;
            source.Command=Command(source,0);source.CurrentWasOrdinary=true;source.AffectedCurrent=true;
            source.Candidates.Add(new RescueCandidate{X=source.X+1,Y=source.Y,Tile=source.TileId+1,Distance=1});moving.Enqueue(source);
            var facing=Idle(source);facing.AIState=7;facing.Command=Command(facing,0);animation.Idle[facing.Id]=facing;moving.Tick(100);
            Check(moving.Observer.TryCapturePending(out var oldAnimation) && oldAnimation.Length==1,"fresh Archer7 authority captured");
            var now=RescueReplanIntent.CopySnapshot(facing);now.AIState=0;now.Command=Command(now,0);animation.Idle[now.Id]=now;
            moving.Tick(101);
            Check(animation.RescueCalls==1 && animation.Rescued[0].AIState==0 && ReferenceEquals(animation.Rescued[0].Command,now.Command) &&
                animation.Rescued[0].RequestedX==now.X && oldAnimation[0].Snapshot.AIState==7,
                "Archer7-to0 uses wholly fresh command semantics, not tolerant matching or old AI replay");

            foreach(int variant in new[]{0,1,2,3})
            {
                var e=new IdleEngine();var h=new Harness(engine:e);var oldSaved=oldAnimation[0];
                var aged=new RescuePendingCandidate<UnitSnapshot>(oldSaved.Source,oldSaved.Snapshot,200,2,0,RescueQueueLifetimeMode.ActiveWindow,7,false);
                h.Observer.StagePending(new[]{aged});var current=RescueReplanIntent.CopySnapshot(now);
                if(variant==1)current.AIState=1;
                if(variant==2)current.Command=Command(current,0,0xA00);
                if(variant==3){current.Flags=2;current.AIState=101;current.Command=Command(current,0);}
                e.Idle[current.Id]=current;h.Tick(500);
                if(variant==0)
                {
                    Check(e.RescueCalls==0 && h.Observer.TryCapturePending(out var refreshed) && refreshed.Length==1 &&
                        refreshed[0].Snapshot.AIState==0 && refreshed[0].Snapshot.RequestedX==now.X && refreshed[0].Age==200 &&
                        refreshed[0].ActiveAge==7 && refreshed[0].Attempts==2,"stale queued flags0 is freshly rebound without renewing source/window/attempts");
                    h.Tick(501);Check(e.RescueCalls==1 && e.Rescued[0].AIState==0,"new current idle phase reaches the actual coordinator action");
                }
                else {h.Tick(501);Check(e.RescueCalls==0,"combat/targeted/active state never gains fresh stationary authority");}
            }
            var manyEngine=new IdleEngine();var many=new Harness(engine:manyEngine);var records=new List<RescuePendingCandidate<UnitSnapshot>>();
            for(int id=100;id<700;id++)
            {
                var old=Fresh(Old(id));old.Type=(int)eChimps.CHIMP_TYPE_ARCHER;old.Command=Command(old,0);old.CurrentWasOrdinary=true;old.AffectedCurrent=true;
                old.Candidates.Add(new RescueCandidate{X=old.X+1,Y=old.Y,Tile=old.TileId+1,Distance=1});
                Check(RescueGeometryWitness.TryCapture(old,out var w),"animation wave has old causal geometry");
                var phase7=Idle(old);phase7.AIState=7;phase7.Command=Command(phase7,0);
                Check(RescueGeometryWitness.TryJoinStationary(w,phase7,out var joined),"animation wave stores a canonical fresh idle receipt");
                records.Add(new RescuePendingCandidate<UnitSnapshot>("replan/BuildStructure",joined,200,0,0,RescueQueueLifetimeMode.ActiveWindow,-1,false));
                var phase0=RescueReplanIntent.CopySnapshot(phase7);phase0.AIState=0;phase0.Command=Command(phase0,0);manyEngine.Idle[id]=phase0;
            }
            many.Observer.StagePending(records.ToArray());int previous=0;
            for(int tick=100;tick<240;tick++)
            {many.Tick(tick);Check(manyEngine.ReadIds.Count<=32 && manyEngine.RescueCalls-previous<=8,
                "600 changed animation receipts retain32 known-ID visits and8 actions with constant bounded revalidation");previous=manyEngine.RescueCalls;}
            Check(manyEngine.RescueCalls==600 && many.Observer.PendingCount==0,"animation wave fully drains without old phase replay or deadline refresh");
        }
        private static UnitSnapshot Idle(UnitSnapshot old,bool atNext=false)
        {
            var u=Fresh(old,atNext);u.Flags=0;u.AIState=0;u.RequestedX=u.X;u.RequestedY=u.Y;u.NextTileId=0;
            u.Command=Command(u,0);return u;
        }
        private static void PassiveIdleAfterCompletedIntent()
        {
            var engine=new IdleEngine();var h=new Harness(engine:engine);var old=Fresh(Old());old.CurrentWasOrdinary=true;old.AffectedCurrent=true;
            old.GroundFlags=0;old.Candidates.Add(new RescueCandidate{X=old.X+2,Y=old.Y,Tile=old.TileId+2,Distance=2});
            h.Enqueue(old);var idle=Idle(old);engine.Idle[idle.Id]=idle;
            h.Tick(100);
            Check(engine.RescueCalls==0 && engine.IdleCaptures==1 && h.Observer.PendingCount==1 && h.Observer.AttemptCount==0,
                "completed old command never reaches native recovery before fresh idle admission");
            Check(h.Observer.TryCapturePending(out var pending) && pending.Length==1 && pending[0].Snapshot.Flags==0 &&
                pending[0].Snapshot.AIState==0 && pending[0].Snapshot.RequestedX==idle.X && pending[0].Attempts==0 &&
                pending[0].ActiveAge==0 && pending[0].Age==0 && RescueGeometryCodec.CanonicalJoin(pending[0].Snapshot),
                "save exports exactly one fresh stationary authority with original source/window");
            Check(h.Observer.TryCaptureGeometry(out var geometry) && geometry.Length==0,"paired save excludes fresh idle geometry duplicate");
            Check(RescuePendingCodec.TryEncode(pending[0],out var bytes) && RescuePendingCodec.TryDecode(bytes,out var restored) &&
                restored.Snapshot.Flags==0 && restored.Snapshot.RequestedX==idle.X && restored.ActiveAge==0 && restored.Age==0,
                "production codec roundtrips joined fresh idle authority");
            h.Tick(101);
            Check(engine.RescueCalls==1 && engine.Rescued[0].AIState==0 && engine.Rescued[0].RequestedX==idle.X &&
                engine.Rescued[0].GeometryWitness!=null && ReferenceEquals(engine.Rescued[0].Command,idle.Command) &&
                h.Observer.PendingCount==0,"fresh idle uses same action queue without old goal or AI replay");
            for(int tick=102;tick<120;tick++)h.Tick(tick);
            Check(engine.IdleCaptures==1 && engine.RescueCalls==1,"no background idle polling after one transition");

            var clock=new IdleEngine();var aged=new Harness(engine:clock);var agedOld=Fresh(Old(2));agedOld.CurrentWasOrdinary=true;agedOld.AffectedCurrent=true;
            agedOld.Candidates.Add(new RescueCandidate{X=agedOld.X+1,Y=agedOld.Y,Tile=agedOld.TileId+1,Distance=1});
            aged.Observer.StagePending(new[]{new RescuePendingCandidate<UnitSnapshot>("BuildStructure",agedOld,200,2,0,
                RescueQueueLifetimeMode.ActiveWindow,7,false)});clock.Idle[agedOld.Id]=Idle(agedOld);aged.Tick(300);
            Check(aged.Observer.TryCapturePending(out var kept) && kept.Length==1 && kept[0].Age==200 && kept[0].ActiveAge==7 && kept[0].Attempts==2,
                "passive phase change never renews old source age, active window or attempts");

            foreach(int negative in new[]{0,1,2,3,4})
            {
                var e=new IdleEngine();var n=new Harness(engine:e);var original=Fresh(Old(10+negative));original.CurrentWasOrdinary=true;original.AffectedCurrent=true;
                if(negative==0){original.Type=(int)eChimps.CHIMP_TYPE_ARCHER;original.Command=Command(original,0);}
                original.Candidates.Add(new RescueCandidate{X=original.X+1,Y=original.Y,Tile=original.TileId+1,Distance=1});n.Enqueue(original);
                var current=Idle(original);
                if(negative==0)current.AIState=1;
                if(negative==1)current.GlobalId++;
                if(negative==2)current.Owner++;
                if(negative==3)current.RequestedX++;
                if(negative==4){current.X++;current.NextX++;current.RequestedX++;current.TileId++;}
                e.Idle[current.Id]=current;e.RescueSuccess=false;e.RescueFailureStatus=RescueStatus.Unsupported;n.Tick(100);
                Check(n.Observer.PendingCount==0,"combat/new life/owner/goal/unproved position cannot become fresh idle");
            }
            var changedEngine=new IdleEngine();var changed=new Harness(engine:changedEngine);var b=Fresh(Old(20));b.CurrentWasOrdinary=true;b.AffectedCurrent=true;
            b.Candidates.Add(new RescueCandidate{X=b.X+1,Y=b.Y,Tile=b.TileId+1,Distance=1});changed.Enqueue(b);changedEngine.Idle[b.Id]=Idle(b);changed.Tick(100);
            changedEngine.CurrentIdle=false;changed.Tick(101);
            Check(changedEngine.RescueCalls==0 && changed.Observer.PendingCount==0,"changed idle authority retires before action");

            var reentryEngine=new IdleEngine();var reentry=new Harness(engine:reentryEngine);var reentryOld=Fresh(Old(21));reentryOld.CurrentWasOrdinary=true;reentryOld.AffectedCurrent=true;
            reentryOld.Candidates.Add(new RescueCandidate{X=reentryOld.X+1,Y=reentryOld.Y,Tile=reentryOld.TileId+1,Distance=1});reentry.Enqueue(reentryOld);
            reentryEngine.Idle[reentryOld.Id]=Idle(reentryOld);reentryEngine.DuringIdleCapture=()=>reentryEngine.Generation++;
            reentry.Tick(100);reentry.Tick(101);
            Check(reentryEngine.RescueCalls==0 && reentry.Observer.PendingCount==0,"world loss inside readonly idle boundary cannot publish old geometry");

            var waitEngine=new IdleEngine();var wait=new Harness(engine:waitEngine);var waiting=Fresh(Old(23));waiting.CurrentWasOrdinary=true;waiting.AffectedCurrent=true;
            waiting.Candidates.Add(new RescueCandidate{X=waiting.X+1,Y=waiting.Y,Tile=waiting.TileId+1,Distance=1});wait.Enqueue(waiting);
            waitEngine.Settling.Add(waiting.Id);wait.Tick(100);
            Check(waitEngine.RescueCalls==0 && wait.Observer.PendingCount==1,"known AI101 stationary caller tail retains geometry without action");
            for(int tick=101;tick<=105;tick++)wait.Tick(tick);
            Check(waitEngine.RescueCalls==0 && wait.Observer.PendingCount==1,"delayed native type-handler transition remains bounded and never replays AI101");
            waitEngine.Settling.Clear();waitEngine.Idle[waiting.Id]=Idle(waiting);wait.Tick(106);
            Check(waitEngine.RescueCalls==1 && waitEngine.Rescued[0].AIState==0 && waitEngine.Rescued[0].RequestedX==waiting.X,
                "later genuine idle state grants only its new stationary semantics");
            var expiryEngine=new IdleEngine();var expiry=new Harness(engine:expiryEngine);var expiring=Fresh(Old(24));expiring.CurrentWasOrdinary=true;expiring.AffectedCurrent=true;
            expiring.Candidates.Add(new RescueCandidate{X=expiring.X+1,Y=expiring.Y,Tile=expiring.TileId+1,Distance=1});expiry.Enqueue(expiring);
            expiryEngine.Settling.Add(expiring.Id);for(int tick=100;tick<=181;tick++)expiry.Tick(tick);
            Check(expiry.Observer.PendingCount==0 && expiryEngine.RescueCalls==0 && expiryEngine.IdleCaptures<=80,
                "known transition cannot renew its active deadline or poll forever");

            var budgetEngine=new IdleEngine();var budget=new Harness(engine:budgetEngine);var many=new List<UnitSnapshot>();
            for(int id=100;id<900;id++)
            {var u=Fresh(Old(id));u.CurrentWasOrdinary=true;u.AffectedCurrent=true;u.Candidates.Add(new RescueCandidate{X=u.X+1,Y=u.Y,Tile=u.TileId+1,Distance=1});
                many.Add(u);if(id<700)budgetEngine.Idle[id]=Idle(u);}
            budget.Enqueue(many.ToArray());int previousCaptures=0,previousActions=0,previousIdleChecks=0;
            for(int tick=100;tick<260;tick++)
            {budget.Tick(tick);Check(budgetEngine.ReadIds.Count<=32 && budgetEngine.IdleCaptures-previousCaptures+budgetEngine.IdleChecks-previousIdleChecks<=96 &&
                budgetEngine.RescueCalls-previousActions<=8,
                "passive burst bounds32 known-ID visits,96 capture/validation calls and8 actual actions");previousCaptures=budgetEngine.IdleCaptures;previousActions=budgetEngine.RescueCalls;previousIdleChecks=budgetEngine.IdleChecks;}
            Check(budget.Observer.PendingCount==0 && budgetEngine.RescueCalls==800,"600 passive and200 active originals drain without starvation or duplicate action");

            var waveEngine=new IdleEngine();var wave=new Harness(engine:waveEngine);var ready=Fresh(Old(1));ready.CurrentWasOrdinary=true;ready.AffectedCurrent=true;
            ready.Candidates.Add(new RescueCandidate{X=ready.X+1,Y=ready.Y,Tile=ready.TileId+1,Distance=1});wave.Enqueue(ready);waveEngine.Idle[ready.Id]=Idle(ready);wave.Tick(100);
            var waveRecords=new List<RescuePendingGeometry>();
            for(int id=100;id<800;id++)
            {var u=Old(id);Check(RescueGeometryWitness.TryCapture(u,out var w),"geometry-wave original causal witness");
                waveRecords.Add(new RescuePendingGeometry("BuildStructure",w,200,0,0,RescueQueueLifetimeMode.ActiveWindow,-1,false));waveEngine.Idle[id]=Idle(u);}
            wave.Observer.StageGeometry(waveRecords.ToArray());wave.Tick(101);
            Check(waveEngine.Rescued.Count>0 && waveEngine.Rescued[0].Id==ready.Id,"older already-active idle authority precedes a large newer geometry wave");
            Check(wave.Observer.TryCaptureGeometry(out var unvisitedGeometry) && unvisitedGeometry.Length>512,
                "large geometry wave remains bounded rather than synchronously drained");
            foreach(var row in unvisitedGeometry)Check(row.ActiveAge==-1 && row.Age==200,"metadata alone neither starts the active clock nor renews original source age");
            int waveCaptures=waveEngine.IdleCaptures,waveChecks=waveEngine.IdleChecks,waveActions=waveEngine.RescueCalls;
            for(int tick=102;tick<220;tick++)
            {wave.Tick(tick);Check(waveEngine.ReadIds.Count<=32 && waveEngine.IdleCaptures-waveCaptures+waveEngine.IdleChecks-waveChecks<=96 &&
                waveEngine.RescueCalls-waveActions<=8,
                "mixed wave bounds32 known-ID visits,96 capture/validation calls and8 actual actions");waveCaptures=waveEngine.IdleCaptures;waveChecks=waveEngine.IdleChecks;waveActions=waveEngine.RescueCalls;}
            Check(wave.Observer.PendingCount==0 && waveEngine.RescueCalls==701,"large new geometry wave cannot starve an existing ready receipt or itself");

            var loadEngine=new IdleEngine();var loaded=new Harness(engine:loadEngine);var history=Old(31);
            Check(RescueGeometryWitness.TryCapture(history,out var savedWitness),"restored geometry has original causal source");
            loaded.Observer.StageGeometry(new[]{new RescuePendingGeometry("BuildStructure",savedWitness,200,1,0,
                RescueQueueLifetimeMode.ActiveWindow,5,false)});loadEngine.Idle[history.Id]=Idle(history);loaded.Tick(500);
            Check(loadEngine.IdleCaptures==1 && loadEngine.RescueCalls==1 && loadEngine.Rescued[0].AIState==0 &&
                loadEngine.Rescued[0].RequestedX==history.X,"new-world geometry restore grants only a freshly observed independent idle action");
            var failedEngine=new IdleEngine();var failed=new Harness(engine:failedEngine);var oldFailure=Old(30);failed.Enqueue(oldFailure);
            failed.Intents.CaptureAllowed=false;var currentIdle=Idle(oldFailure);failedEngine.Idle[oldFailure.Id]=currentIdle;failed.Fail(currentIdle);
            Check(failedEngine.IdleCaptures==0,"failed original never captures idle within caller tail");failed.Tick(100);
            Check(failedEngine.IdleCaptures==1 && failedEngine.RescueCalls==1 && failedEngine.Rescued[0].Flags==0,
                "failed original schedules one bounded post-tail fresh idle admission");
        }
        private static void InactivePackedNextNativeRefusal()
        {
            var source=new RescueReplanIntentFixture.Source(eChimps.CHIMP_TYPE_SPEARMAN,101){PackedNext=0};
            var original=source.Snapshot(7);original.AffectedCurrent=true;original.CurrentWasOrdinary=true;
            original.Candidates.Add(new RescueCandidate{X=3,Y=5,Tile=4003,Distance=1});
            var h=new Harness();h.Observer.ReplanIntents=source.Boundary;h.Enqueue(original);
            var call=h.Begin(source.Snapshot(7),out var context);
            Check(call!=null,"actual intent boundary admits stationary stale Next cache before original");
            call.Complete(MoveEnd(context,0));h.Tick(100);
            Check(h.Engine.RescueCalls==1 && h.Engine.Rescued[0].NextTileId==0 && h.Engine.Rescued[0].GeometryWitness!=null &&
                h.Engine.Rescued[0].RequestedX==20 && h.Engine.Rescued[0].RequestedY==30,
                "actual Native0 plus production intent boundary joins retained geometry and reaches recovery with raw cache0");
        }
        internal static void Run()
        {
            checks=0;SpearmanOneIndependentIdle();PassiveIdleHealthAndAnimation();PassiveIdleAfterCompletedIntent();InactivePackedNextNativeRefusal();RecoveryReentryRetirement();EnvelopeOverlapRetirement();FreshIntentAndSaveBoundary();OriginalAcceptedNextRoundTrip();NestedPublication();FirstBoundaryAndNewOrder();RejectedCallsAndInvalidation();
            OriginalLifetimeAndAttempts();CapacityAndBudgets();GeometryOnlyLoadAndDisable();DormantBurstAndAtomicSave();
            Console.WriteLine("ReplanCoordinatorFixture: "+checks+" checks passed; actual General coordinator/queue/join/codecs, no native calls");
        }
        private static void FreshIntentAndSaveBoundary()
        {
            var h=new Harness();var old=Old();h.Enqueue(old);var fresh=Fresh(old);
            h.Engine.DuringRescue=snapshot=>Check(!h.Observer.TryCapturePending(out _) && !h.Observer.TryCaptureGeometry(out _),"atomic save also waits until synchronous recovery finishes");
            var receipt=h.Begin(fresh,out var context);
            Check(receipt!=null && h.Observer.GetOrderSerial(old.Id)==1 && h.Observer.PendingCount==1,"external Pre revokes old queue intent and retains geometry");
            Check(h.Engine.RescueCalls==0 && h.Intents.Refreshes==0,"Begin never validates late intent or mutates");
            Check(h.Observer.HasUnsettledReplans && !h.Observer.TryCapturePending(out _) && !h.Observer.TryCaptureGeometry(out _),"save refused from Begin until admitted Tick");
            receipt.Complete(MoveEnd(context));receipt.Complete(MoveEnd(context));
            Check(h.Observer.HasUnsettledReplans && h.Engine.RescueCalls==0,"actual zero Completion is single-use and not recovery");
            Check(!h.Observer.TryCapturePending(out _) && !h.Observer.TryCaptureGeometry(out _),"original completion is still before caller/legacy Post boundary");
            h.Tick(100);
            Check(h.Engine.RescueCalls==1 && h.Observer.PendingCount==0 && !h.Observer.HasUnsettledReplans,"first admitted Tick joins and rescues once");
            var joined=h.Engine.Rescued[0];
            Check(ReferenceEquals(joined.Command,fresh.Command) && !ReferenceEquals(joined.Command,old.Command) && joined.AIState==101,"same-goal fresh command purpose replaces old idle purpose");
            Check(joined.Flags==2 && joined.Substep==8 && !joined.OrdinaryStep && joined.GroundFlags==old.GroundFlags && joined.CurrentWasOrdinary,"fresh pose and original geometry stay separate");
            Check(joined.GeometryWitness!=null && joined.Candidates.Count==old.Candidates.Count && joined.Candidates[2].Tile==old.Candidates[2].Tile,"old reachable bank remains original evidence");
            h.Tick(100);Check(h.Engine.RescueCalls==1,"duplicate simulation callback cannot rescue twice");
            Check(h.Observer.TryCapturePending(out var pending) && pending.Length==0 && h.Observer.TryCaptureGeometry(out var geometry) && geometry.Length==0,"settled successful Tick restores atomic-save admission");
        }
        private static void RecoveryReentryRetirement()
        {
            // Actual production Drain callback A reenters the real WorldMutation
            // observer before a foreign wall original; B's endpoints are untouched.
            for(int mode=0;mode<9;mode++)
            {
                var h=new Harness();var a=Old();var b=Old(2);Shift(b,40,40);h.Enqueue(a,b);
                int captures=h.Engine.CaptureCalls;
                h.Engine.DuringRescue=current=>
                {
                    if(current.Id!=a.Id)return;
                    Check(h.Observer.IsRecovering,"foreign wall runs within actual rescue callback");
                    Check(!h.Observer.TryCapturePending(out _),"reentry cannot authorize a partial save");
                    h.Engine.PlannedWrites=mode==2?Array.Empty<RescueTile>():mode==3?null:
                        new[]{new RescueTile(mode==1?300:mode==7?a.X+3:b.X+3,mode==1?300:mode==7?a.Y:b.Y,20000)};
                    h.Engine.ThrowEnvelope=mode==4;h.Engine.EnvelopeSupported=mode!=5;
                    var c=Mutation(2000+mode,mode==6?8:9,mode==8?4:3);
                    Check(h.Observer.TryBegin(c)==null,"reentry publishes no capture receipt");
                    h.Observer.OnBatchCompleted(BatchEnd(2000+mode,mode==6?8:9,mode==8?4:3));
                    Check(h.Engine.CaptureCalls==captures,"reentry performs no unit capture or candidate BFS");
                    // The first operation is already running. Overlap can retire its
                    // ownership, but must neither undo it nor re-add its queue node.
                    if(mode==7)Check(!h.Observer.IsInterestedInOrder(a.Id),"active A ownership retired before wall original");
                };
                h.Tick(100);h.Tick(101);
                bool bRuns=mode==1 || mode==2 || mode==6 || mode==7 || mode==8;
                Check(h.Engine.RescueCalls==(bRuns?2:1),"overlap/unknown drops B, unrelated/empty/wrong-world preserves B mode="+mode);
                Check(h.Engine.EnvelopeCalls==(mode==6 || mode==8?0:1),"world guard precedes planning mode="+mode);
                Check(h.Observer.PendingCount==0 && !h.Observer.IsRecovering,"reentrant invalidation leaves no retry/readded active node mode="+mode);
            }
            for(int kind=0;kind<6;kind++)
            {
                var h=new Harness();var a=Old();var b=Old(2,true);Shift(b,40,40);h.Enqueue(a);
                if(kind<=1 || kind==5)
                {
                    h.Enqueue(b);
                    if(kind==1)h.Observer.ObserveOrder(b.Id);
                    if(kind==5)h.Fail(Fresh(b,true));
                }
                else if(kind==2)h.Observer.StagePending(new[]{new RescuePendingCandidate<UnitSnapshot>("BuildStructure",b,0,0,0)});
                else
                {
                    Check(RescueGeometryWitness.TryCapture(b,out var witness),"reentry retained storage witness");
                    if(kind==3)
                    {
                        Check(RescueGeometryWitness.TryJoin(witness,Fresh(b,true),out var joined),"reentry joined saved witness");
                        h.Observer.StagePending(new[]{new RescuePendingCandidate<UnitSnapshot>("replan/BuildStructure",joined,0,0,0)});
                    }
                    else h.Observer.StageGeometry(new[]{new RescuePendingGeometry("BuildStructure",witness,0,0,0)});
                }
                int captures=h.Engine.CaptureCalls;
                h.Engine.DuringRescue=current=>
                {
                    if(current.Id!=a.Id)return;
                    // Original left boundary is distance9 from joined acceptedNext.
                    h.Engine.PlannedWrites=new[]{new RescueTile(b.X-8,b.Y,20000)};
                    Check(h.Observer.TryBegin(Mutation(2100+kind,9,3))==null,"reentry storage creates no new capture");
                    Check(!h.Observer.IsInterestedInOrder(b.Id),"reentry retires overlapping historical form kind="+kind);
                };
                h.Tick(100);
                Check(h.Engine.RescueCalls==1 && h.Engine.CaptureCalls==captures && h.Observer.PendingCount==0,
                    "all six historical forms retire during recovery without new BFS kind="+kind);
            }
        }
        private static void EnvelopeOverlapRetirement()
        {
            for(int kind=0;kind<6;kind++)
            {
                var h=new Harness();var old=Old(step:kind==2 || kind==4);
                if(kind<=2)
                {
                    h.Enqueue(old);
                    if(kind==0)h.Observer.ObserveOrder(old.Id);
                    if(kind==2){h.Engine.RescueSuccess=false;h.Fail(Fresh(old,true));h.Tick(100);}
                }
                else if(kind==3)h.Observer.StagePending(new[]{new RescuePendingCandidate<UnitSnapshot>("BuildStructure",old,0,0,0)});
                else
                {
                    Check(RescueGeometryWitness.TryCapture(old,out var witness),"historical storage witness fixture");
                    if(kind==4)
                    {Check(RescueGeometryWitness.TryJoin(witness,Fresh(old,true),out var joined),"joined staged fixture");h.Observer.StagePending(new[]{new RescuePendingCandidate<UnitSnapshot>("replan/BuildStructure",joined,0,0,0)});}
                    else h.Observer.StageGeometry(new[]{new RescuePendingGeometry("BuildStructure",witness,0,0,0)});
                }
                int calls=h.Engine.RescueCalls;Check(h.Observer.PendingCount==1,"historical storage admitted kind"+kind);
                h.Engine.Captured.Clear();
                // Joined cases use the original left radius8 border: distance9 from
                // fresh acceptedNext, not the incorrectly shifted fresh square.
                h.Engine.PlannedWrites=new[]{new RescueTile(old.X-(kind==2 || kind==4?8:-3),old.Y+3,20000)};
                Check(h.Observer.TryBegin(Mutation(900,h.Engine.Session,h.Engine.Generation))==null,"later writer has zero local actors kind"+kind);
                Check(h.Observer.PendingCount==0,"envelope overlap retires historical proof before original kind"+kind);
                h.Observer.OnBatchCompleted(BatchEnd(900,h.Engine.Session,h.Engine.Generation));h.Tick(101);
                Check(h.Engine.RescueCalls==calls,"retired proof cannot rescue later kind"+kind);
            }
            var separate=new Harness();var near=Old();var far=Old(2);Shift(far,200,200);separate.Enqueue(near,far);
            separate.Observer.ObserveOrder(near.Id);separate.Observer.ObserveOrder(far.Id);separate.Engine.Captured.Clear();
            separate.Engine.PlannedWrites=new[]{new RescueTile(near.X+3,near.Y+3,20000)};separate.Engine.CaptureSupported=false;
            Check(separate.Observer.TryBegin(Mutation(901,9,3))==null,"unit discovery failure publishes no new proof");
            Check(separate.Observer.PendingCount==1 && separate.Observer.IsInterestedInOrder(far.Id) && !separate.Observer.IsInterestedInOrder(near.Id),
                "complete plan survives failed unit capture and preserves unrelated historical bank");
            separate.Engine.PlannedWrites=null;
            Check(separate.Observer.TryBegin(Mutation(902,9,3))==null && separate.Observer.PendingCount==0,"unknown plan retires all unknown affected histories");
            var sparse=new Harness();near=Old();sparse.Enqueue(near);sparse.Observer.ObserveOrder(near.Id);sparse.Engine.Captured.Clear();
            sparse.Engine.PlannedWrites=new[]{new RescueTile(0,0,20000),new RescueTile(100,100,20001)};
            sparse.Observer.TryBegin(Mutation(903,9,3));Check(sparse.Observer.PendingCount==1,"bounding-box overlap alone does not discard unrelated sparse writes");
            var active=new Harness();near=Old();active.Enqueue(near);var waiting=active.Begin(Fresh(near),out var moveContext);
            active.Engine.Captured.Clear();active.Engine.PlannedWrites=new[]{new RescueTile(near.X+3,near.Y+3,20000)};
            active.Observer.TryBegin(Mutation(904,9,3));waiting.Complete(MoveEnd(moveContext));active.Tick(101);
            Check(active.Observer.PendingCount==0 && active.Engine.RescueCalls==0 && !active.Observer.HasUnsettledReplans,"conflict cancels unsettled receipt and late zero cannot revive it");
            CompoundEnvelopeCases();
        }
        private static void Shift(UnitSnapshot u,int dx,int dy)
        {u.X+=dx;u.Y+=dy;u.NextX+=dx;u.NextY+=dy;for(int i=0;i<u.Candidates.Count;i++){var c=u.Candidates[i];c.X+=dx;c.Y+=dy;u.Candidates[i]=c;}}
        private static void CompoundEnvelopeCases()
        {
            foreach(bool failedFirst in new[]{true,false})
            {
                var h=new Harness();var old=Old();var outer=Mutation(910,9,3);
                h.Engine.Captured.Add(old);h.Engine.CaptureSupported=!failedFirst;h.Engine.PlannedWrites=failedFirst?null:Array.Empty<RescueTile>();
                var first=h.Observer.TryBegin(outer);h.Engine.CaptureSupported=failedFirst;h.Engine.PlannedWrites=failedFirst?Array.Empty<RescueTile>():null;
                var child=Mutation(910,9,3);var next=h.Observer.TryBegin(child);
                first?.Complete(MutationEnd(outer));next?.Complete(MutationEnd(child));h.Observer.OnBatchCompleted(BatchEnd(910,9,3));h.Tick(100);
                Check(h.Observer.PendingCount==0 && h.Engine.RescueCalls==0,"failed first/child prevents partial samebatch evidence first="+failedFirst);
            }
            var complete=new Harness();var source=Old();complete.Engine.Captured.Add(source);var parent=Mutation(911,9,3);
            var parentReceipt=complete.Observer.TryBegin(parent);var nested=Mutation(911,9,3);
            complete.Engine.PlannedWrites=new[]{new RescueTile(source.X+3,source.Y+3,20000)};
            var nestedReceipt=complete.Observer.TryBegin(nested);nestedReceipt.Complete(MutationEnd(nested));parentReceipt.Complete(MutationEnd(parent));
            complete.Observer.OnBatchCompleted(BatchEnd(911,9,3));complete.Tick(100);
            Check(complete.Engine.RescueCalls==1 && ReferenceEquals(source,complete.Engine.Rescued[0]),"successful complete samebatch nested captures preserve earliest fresh event proof");
            var conflict=new Harness();source=Old();conflict.Enqueue(source);conflict.Engine.Captured.Clear();
            conflict.Engine.PlannedWrites=new[]{new RescueTile(source.X+3,source.Y+3,20000)};var firstWriter=Mutation(912,9,3);
            Check(conflict.Observer.TryBegin(firstWriter)==null && conflict.Observer.PendingCount==0,"zero-actor first writer retires prior normal queue");
            conflict.Engine.Captured.Add(Old());var secondWriter=Mutation(912,9,3);var second=conflict.Observer.TryBegin(secondWriter);
            second?.Complete(MutationEnd(secondWriter));conflict.Observer.OnBatchCompleted(BatchEnd(912,9,3));conflict.Tick(100);
            Check(conflict.Engine.RescueCalls==0,"samecompound child cannot re-admit a retired conflicting actor");
            var overflow=new Harness();overflow.Engine.CaptureSupported=false;overflow.Engine.PlannedWrites=null;
            for(int i=0;i<65;i++)overflow.Observer.TryBegin(Mutation(1000+i,9,3));
            overflow.Engine.CaptureSupported=true;overflow.Engine.PlannedWrites=Array.Empty<RescueTile>();overflow.Engine.Captured.Add(Old());
            Check(overflow.Observer.TryBegin(Mutation(1100,9,3))==null,"failed-batch tombstone overflow remains fail-closed without unbounded metadata");
            overflow.Engine.Generation++;var resumed=overflow.Observer.TryBegin(Mutation(1101,9,overflow.Engine.Generation));
            Check(resumed!=null,"new world resets bounded failed-batch overflow latch");
        }
        private static void OriginalAcceptedNextRoundTrip()
        {
            var h=new Harness();h.Engine.RescueSuccess=false;var old=Old(step:true);h.Enqueue(old);h.Fail(Fresh(old,true));h.Tick(100);
            Check(h.Observer.TryCapturePending(out var pending) && pending.Length==1,"failed local recovery remains bounded pending work");
            var saved=pending[0];var u=saved.Snapshot;
            Check(u.X==old.NextX && u.NextX==u.X && !u.OrdinaryStep && u.GeometryWitness.HasAcceptedNext,"fresh Current rebases onto original accepted Next without invented successor");
            Check(Math.Abs(u.Candidates[2].X-u.X)==9 && u.Candidates[0].Distance==0 && u.Candidates[0].X==old.X && u.Candidates[0].Tile==old.TileId,"old radius and zero-distance anchor do not shift/truncate");
            RescuePendingCandidate<UnitSnapshot> loaded=default;
            Check(RescuePendingCodec.TryEncode(saved,out var bytes) && RescuePendingCodec.TryDecode(bytes,out loaded),"current joined pending proof round trips");
            Check(loaded.Snapshot.GeometryWitness.Current.X==old.X && loaded.Snapshot.Candidates[2].X==old.Candidates[2].X &&
                loaded.Snapshot.Candidates[0].Distance==0 && RescueGeometryCodec.CanonicalJoin(loaded.Snapshot),"loaded joined proof validates in original coordinate frame");
            var corrupt=RescueReplanIntent.CopySnapshot(loaded.Snapshot);corrupt.Candidates[0]=new RescueCandidate{X=u.X,Y=u.Y,Tile=u.TileId,Distance=0};
            Check(!RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>(saved.Source,corrupt,saved.Age,saved.Attempts,saved.RetryIn),out _),"shifted historical zero-anchor rejected by codec");
            var restored=new Harness();restored.Engine.Now=200;restored.Observer.StagePending(new[]{loaded});restored.Tick(200);
            Check(restored.Engine.RescueCalls==0,"saved retry delay is preserved on load");restored.Tick(201);
            Check(restored.Engine.RescueCalls==1 && restored.Engine.Rescued[0].GeometryWitness.Current.Tile==old.TileId,"loaded retry uses fresh intent and original source bank");
        }
        private static void NestedPublication()
        {
            var h=new Harness();var old=Old();h.Enqueue(old);var outer=h.Begin(Fresh(old),out var outerContext);
            var inner=h.Begin(Fresh(old,purpose:11),out var innerContext);inner.Complete(MoveEnd(innerContext));outer.Complete(MoveEnd(outerContext,1));
            h.Tick(100);Check(h.Engine.RescueCalls==0 && h.Observer.PendingCount==0,"older outer positive original revokes newer nested zero per unit");
            h=new Harness();var a=Old(1);var b=Old(2);h.Enqueue(a,b);
            var first=h.Begin(Fresh(a),out var ca);var other=h.Begin(Fresh(b),out var cb);
            first.Complete(MoveEnd(ca));other.Complete(MoveEnd(cb,1));h.Tick(100);
            Check(h.Engine.RescueCalls==1 && h.Engine.Rescued[0].Id==a.Id,"another unit's positive original does not revoke matching zero");
            h=new Harness();old=Old();h.Enqueue(old);outer=h.Begin(Fresh(old),out outerContext);
            inner=h.Begin(Fresh(old,purpose:12),out innerContext);inner.Complete(MoveEnd(innerContext));outer.Complete(MoveEnd(outerContext));h.Tick(100);
            Check(h.Engine.RescueCalls==1 && h.Engine.Rescued[0].Command.TryGetValue(0x958,out uint purpose) && purpose==12,"older outer zero cannot overwrite newer nested fresh purpose");
        }
        private static void RejectedCallsAndInvalidation()
        {
            foreach(int mode in new[]{0,1,2,3,4,5})
            {
                var h=new Harness();var old=Old();h.Enqueue(old);var receipt=h.Begin(Fresh(old),out var context,mode==5?1:0);
                Check(receipt!=null,"interested unsuccessful invocation has private completion");
                receipt.Complete(MoveEnd(context,mode==4?2:0,invoked:mode!=0,returned:mode!=1,current:mode!=2,
                    failure:mode==3?new InvalidOperationException():null));h.Tick(100);
                Check(h.Engine.RescueCalls==0 && !h.Observer.HasUnsettledReplans,"veto/throw/staleworld/failure/unsupportedresult/selector cannot rescue mode"+mode);
            }
            foreach(string invalid in new[]{"new-goal","AI","job-or-role","pose","unit-lifetime","group","external-target"})
            {
                var h=new Harness();var old=Old();h.Enqueue(old);h.Fail(Fresh(old));
                // The fake denies the exact read-only boundary. Its native implementation
                // separately tests each reason; this tests coordinator propagation.
                h.Intents.Rejected.Add(old.Id);h.Tick(100);
                Check(h.Engine.RescueCalls==0 && !h.Observer.HasUnsettledReplans && h.Observer.PendingCount==0,"Tick-time boundary rejection retires both trigger and geometry: "+invalid);
            }
            foreach(bool beginThrows in new[]{true,false})
            {
                var h=new Harness();var old=Old();h.Enqueue(old);h.Intents.ThrowCapture=beginThrows;h.Intents.ThrowRefresh=!beginThrows;
                h.Fail(Fresh(old));h.Tick(100);Check(h.Engine.RescueCalls==0 && !h.Observer.HasUnsettledReplans,"isolated boundary exception does not authorize recovery");
                Check(h.Observer.PendingCount==(beginThrows?1:0),"capture exception retains only unknown geometry; Tick validation exception retires qualified proof");
            }
            var changed=new Harness();var source=Old(step:true);changed.Enqueue(source);var escaped=Fresh(source,true);escaped.X++;escaped.NextX=escaped.X;escaped.TileId+=50;escaped.NextTileId=escaped.TileId;
            Check(changed.Begin(escaped,out _)==null,"qualified departure is rejected before creating a replan receipt");
            changed.Tick(100);Check(changed.Engine.RescueCalls==0,"fresh unproved second pose never joins original bank");
            Check(changed.Observer.PendingCount==0,"observed departure retires spatial witness rather than retaining it for a later return");
        }
        private static void FirstBoundaryAndNewOrder()
        {
            var h=new Harness();var old=Old();h.Enqueue(old);var receipt=h.Begin(Fresh(old),out var context);
            h.Tick(100);receipt.Complete(MoveEnd(context));h.Tick(101);
            Check(h.Engine.RescueCalls==0 && h.Observer.PendingCount==1 && !h.Observer.HasUnsettledReplans,"late zero completion cannot arm a later arbitrary caller boundary");
            h=new Harness();old=Old();h.Enqueue(old);receipt=h.Begin(Fresh(old),out context);
            h.Tick(100);receipt.Complete(MoveEnd(context,1));
            Check(h.Observer.PendingCount==0,"late positive completion still retires the retained witness");
            h=new Harness();old=Old();h.Enqueue(old);h.Fail(Fresh(old));
            h.Observer.ObserveOrder(old.Id);h.Tick(100);
            Check(h.Engine.RescueCalls==0 && h.Observer.TryCaptureGeometry(out var geometry) && geometry.Length==1,"new order vetoed before final-original Begin cancels older trigger without replay");
            h=new Harness();old=Old();h.Enqueue(old);h.Fail(Fresh(old));var newer=Fresh(old,purpose:13);newer.RequestedX++;
            h.Fail(newer);h.Tick(100);
            Check(h.Engine.RescueCalls==1 && h.Engine.Rescued[0].RequestedX==newer.RequestedX &&
                ReferenceEquals(h.Engine.Rescued[0].Command,newer.Command),"new goal and purpose captured by newer zero are authoritative");
        }
        private static void OriginalLifetimeAndAttempts()
        {
            var h=new Harness();h.Engine.RescueSuccess=false;var old=Old();h.Enqueue(old);
            for(int attempt=0;attempt<4;attempt++)
            {
                h.Engine.Now=100+attempt;h.Fail(Fresh(old,purpose:(ushort)(20+attempt)));h.Tick(100+attempt);
                Check(h.Engine.RescueCalls==attempt+1,"replan shares original cumulative attempt budget "+attempt);
                if(attempt<3)
                {Check(h.Observer.TryCapturePending(out var records) && records.Length==1 && records[0].Attempts==attempt+1 && records[0].Age==attempt,"new order never resets original age/attempts");}
            }
            Check(h.Observer.PendingCount==0,"fourth unsuccessful attempt exhausts original allowance");
            var fifth=h.Begin(Fresh(old),out _);Check(fifth==null,"fifth order cannot invent fresh geometry after exhausted proof");
            h=new Harness();old=Old();h.Enqueue(old);h.Intents.CaptureAllowed=false;
            foreach(int tick in new[]{100,140,179})
            {h.Engine.Now=tick;h.Fail(Fresh(old));h.Tick(tick);Check(h.Observer.TryCaptureGeometry(out var records) && records.Length==1 && records[0].Age==tick-100,"unavailable fresh intent retains original source age without starting work window");}
            h.Engine.Now=180;h.Intents.CaptureAllowed=true;h.Fail(Fresh(old));h.Tick(180);
            Check(h.Engine.RescueCalls==1 && h.Observer.PendingCount==0,"first qualified work after80 source ticks receives its original unstarted active window");
            var delayed=new Harness();old=Old();Check(RescueGeometryWitness.TryCapture(old,out var witness),"loaded retry geometry fixture");
            delayed.Engine.Now=400;delayed.Observer.StageGeometry(new[]{new RescuePendingGeometry("BuildStructure",witness,17,2,1)});
            delayed.Fail(Fresh(old,purpose:31));delayed.Fail(Fresh(old,purpose:32));delayed.Tick(400);
            Check(delayed.Engine.RescueCalls==0 && delayed.Observer.TryCapturePending(out var waiting) && waiting.Length==1 &&
                waiting[0].Age==17 && waiting[0].Attempts==2 && waiting[0].RetryIn==1,"repeated replans preserve original absolute retry due tick and counters");
            delayed.Tick(401);Check(delayed.Engine.RescueCalls==1 && delayed.Engine.Rescued[0].Command.TryGetValue(0x958,out uint selected) && selected==32,
                "saved retry becomes due once with newest fresh purpose");
        }
        private static void CapacityAndBudgets()
        {
            var h=new Harness();var units=new UnitSnapshot[GeneralRescueObserver.MaximumPending];for(int i=0;i<units.Length;i++)units[i]=Old(i+1);
            for(int offset=0;offset<units.Length;offset+=500){int count=Math.Min(500,units.Length-offset);var chunk=new UnitSnapshot[count];Array.Copy(units,offset,chunk,0,count);h.Enqueue(chunk);}
            Check(h.Observer.PendingCount==GeneralRescueObserver.MaximumPending,"normal queue reaches native-ID bounded capacity");h.Observer.ObserveOrder(1);
            RescuePendingGeometry[] geometry=null;RescuePendingCandidate<UnitSnapshot>[] pending=null;
            Check(h.Observer.PendingCount==GeneralRescueObserver.MaximumPending && h.Observer.TryCaptureGeometry(out geometry) && geometry.Length==1 &&
                h.Observer.TryCapturePending(out pending) && pending.Length==GeneralRescueObserver.MaximumPending-1,"geometry transfer shares capacity instead of allocating a parallel allowance");
            var loaded=new Harness();loaded.Observer.StagePending(pending);loaded.Observer.StageGeometry(geometry);
            Check(loaded.Observer.PendingCount==GeneralRescueObserver.MaximumPending,"mixed staged pending/geometry shares native-ID capacity");
            h=new Harness();units=new UnitSnapshot[47];for(int i=0;i<40;i++)units[i]=Old(i+1);
            for(int i=40;i<47;i++)units[i]=Old(101+i-40);h.Enqueue(units);
            for(int i=0;i<40;i++)h.Fail(Fresh(units[i]));h.Tick(100);
            Check(h.Intents.Refreshes==32 && h.Engine.RescueCalls==8,"first Tick bounds expensive joins32 and shared recovery attempts8");
            int normal=0,replans=0;foreach(var rescued in h.Engine.Rescued)if(rescued.GeometryWitness==null)normal++;else replans++;
            Check(normal==7 && replans==1,"normal and replan candidates consume one shared8-attempt allowance");
            Check(!h.Observer.HasUnsettledReplans,"first safe boundary settles caller tails while unvisited fresh intents remain owned");
            h.Tick(100);Check(h.Intents.Refreshes==32 && h.Engine.RescueCalls==8,"duplicate Tick cannot spend join/attempt budgets twice");
            h.Tick(101);Check(h.Intents.Refreshes==40 && h.Engine.RescueCalls==16,"unvisited fresh intents survive and transfer on the next bounded tick");
        }
        private static void DormantBurstAndAtomicSave()
        {
            var dormant=new Harness();var old=Old();dormant.Enqueue(old);dormant.Observer.ObserveOrder(old.Id);
            int refreshes=dormant.Intents.Refreshes;
            for(int tick=101;tick<=300;tick++)dormant.Tick(tick);
            Check(dormant.Intents.Refreshes==refreshes && dormant.Observer.PendingCount==1,
                "dormant geometry without fresh intent has no per-tick intent scan or source-age expiry");
            Check(dormant.Observer.TryCaptureGeometry(out var dormantSave) && dormantSave.Length==1 && dormantSave[0].Age==200 &&
                dormantSave[0].ActiveAge==-1 && dormantSave[0].LifetimeMode==RescueQueueLifetimeMode.ActiveWindow,"dormant save preserves long original source age");

            var h=new Harness();int total=GeneralRescueObserver.MaximumPending;
            var sources=new UnitSnapshot[total];
            for(int offset=0;offset<total;offset+=500)
            {
                int count=Math.Min(500,total-offset);var chunk=new UnitSnapshot[count];
                for(int i=0;i<count;i++){chunk[i]=Old(offset+i+1);sources[offset+i]=chunk[i];}
                h.Enqueue(chunk);
            }
            for(int id=1;id<=total;id++)h.Fail(Fresh(sources[id-1]));
            h.Tick(100);
            Check(h.Intents.Refreshes==32 && h.Engine.RescueCalls==8 && !h.Observer.HasUnsettledReplans,"native-size burst first safe boundary transfers32 and attempts8");
            // Pending export must include every validated unprocessed Fresh. Its paired
            // geometry export excludes exactly those IDs, not a duplicate copy of them.
            Check(h.Observer.TryCapturePending(out var pending),"burst has a pending save boundary");
            Check(h.Observer.TryCaptureGeometry(out var geometry),"burst has a paired geometry save boundary");
            Check(pending.Length==total-8 && geometry.Length==0,"paired save retains every fresh intent exactly once across queue and retained bank");
            var ids=new HashSet<int>();foreach(var record in pending)
                Check(ids.Add(record.Snapshot.Id) && record.Age==0 && record.ActiveAge==-1 && record.Attempts==0,
                    "save never starts or refreshes unprocessed fresh work");
            Check(h.Observer.TryCaptureGeometry(out var standalone) && standalone.Length==total-32,"standalone geometry export does not reuse prior pending exclusion cache");
            int saveRefreshes=h.Intents.Refreshes;
            h.Tick(101);Check(h.Intents.Refreshes-saveRefreshes==32 && h.Engine.RescueCalls==16,"post-save runtime remains bounded and retains next32 fresh intents");
            // Prove the tail survives beyond the old80-tick absolute expiry, using
            // production queue processing rather than merely counting retained data.
            for(int tick=102;tick<430;tick++)h.Tick(tick);
            Check(h.Intents.Refreshes-saveRefreshes==total-32 && h.Engine.RescueCalls>640,
                "all9999 fresh intents eventually reach bounded validation beyond80 source ticks");

            // Settle a safe boundary without visiting this fresh record by placing
            //32 other fresh records first in the actual coordinator schedule.
            var delayed=new Harness();for(int id=1;id<=33;id++)delayed.Enqueue(Old(id));
            for(int id=1;id<=33;id++)delayed.Fail(Fresh(Old(id)));delayed.Tick(100);
            delayed.Intents.Rejected.Add(33);
            Check(delayed.Observer.TryCapturePending(out pending) && delayed.Observer.TryCaptureGeometry(out geometry) &&
                geometry.Length==1 && geometry[0].Witness.UnitId==33,"invalid unvisited fresh intent saves geometry only, never stale command");
            delayed.Observer.ObserveOrder(33);
            Check(delayed.Observer.TryCaptureGeometry(out geometry) && geometry.Length==1,"invalid fresh retains geometry independently of later order");
            var changed=new Harness();for(int id=1;id<=33;id++)changed.Enqueue(Old(id));
            for(int id=1;id<=33;id++)changed.Fail(Fresh(Old(id)));changed.Tick(100);
            changed.Engine.Now=250;
            Check(changed.Observer.TryCapturePending(out pending),"long-waiting unprocessed fresh intent can be saved");
            bool originalAge=false;foreach(var record in pending)if(record.Snapshot.Id==33)
                originalAge=record.Age==150 && record.ActiveAge==-1 && record.Attempts==0;
            Check(originalAge,"save preserves original source age beyond80 without starting a fresh work window");
            changed.Observer.ObserveOrder(33);
            Check(changed.Observer.TryCaptureGeometry(out geometry) && geometry.Length==1 && geometry[0].Witness.UnitId==33,
                "new order invalidates previously prepared fresh-pending exclusion before paired geometry export");
        }
        private static void GeometryOnlyLoadAndDisable()
        {
            var h=new Harness();var old=Old();h.Enqueue(old);h.Engine.Now=120;h.Observer.ObserveOrder(old.Id);
            Check(h.Observer.TryCaptureGeometry(out var geometry) && geometry.Length==1 && geometry[0].Age==20 && geometry[0].Attempts==0,"settled retained geometry exports original metadata only");
            RescuePendingGeometry record=default;
            Check(RescueGeometryCodec.TryEncode(geometry[0],out var bytes) && RescueGeometryCodec.TryDecode(bytes,out record),"geometry-only record round trips independently of invalidated old purpose");
            var disabled=new Harness(false);disabled.Observer.StageGeometry(new[]{record});int reads=disabled.Engine.WorldReads;
            Check(disabled.Observer.TryCaptureGeometry(out var paused) && paused.Length==1 && disabled.Engine.WorldReads==reads,"disabled paused load can re-save managed geometry without unit/world reads");
            disabled.Tick(300);Check(disabled.Observer.PendingCount==0 && disabled.Engine.RescueCalls==0,"disabled simulation progress expires staged geometry without action");
            var active=new Harness();active.Engine.Now=200;active.Observer.StageGeometry(new[]{record});
            active.Fail(Fresh(old));active.Tick(200);
            Check(active.Engine.RescueCalls==1 && active.Engine.Rescued[0].GeometryWitness.Current.Tile==old.TileId,"first new failed order can rebind loaded original geometry to fresh intent");
            var expired=new Harness();expired.Engine.Now=500;
            expired.Observer.StageGeometry(new[]{new RescuePendingGeometry(record.Source,record.Witness,79,3,0)});
            expired.Intents.CaptureAllowed=false;expired.Fail(Fresh(old));expired.Tick(500);
            expired.Engine.Now=501;expired.Intents.CaptureAllowed=true;expired.Fail(Fresh(old));expired.Tick(501);
            Check(expired.Engine.RescueCalls==0 && expired.Observer.PendingCount==0,"loaded remaining lifetime is not refreshed by new orders");
        }
    }
}
