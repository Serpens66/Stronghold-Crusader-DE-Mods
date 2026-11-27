using BugfixesAndQoL;
using BugfixesAndQoL.TrappedUnitRescue;
#if SERP_DEVDIAG
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using APIShared;
using APIShared.Simulation;
using APIShared.WorldMutations;
using CrusaderDE;
using R3;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        // Test-only existing-event observation. This is deliberately NOT a
        // passive performance profile or an exclusive native-damage attribution.
        private sealed class PerformanceHitTrace : IDisposable
        {
            private const int RecordLimit=256,PublicationLimit=10000;
            private readonly Fixture fixture;
            private readonly PerformanceProof proof;
            private readonly Dictionary<int,Actor> actors=new Dictionary<int,Actor>();
            private readonly HashSet<int> retired=new HashSet<int>();
            private readonly FieldInfo engineLockField;
            private readonly object engineLock;
            private IDisposable melee,projectile;
            private int active,disposed,publications,records,iteration=-1;
            private long session,world;
            private string phase="unbound",fault;

            internal PerformanceHitTrace(Fixture fixture,PerformanceProof proof)
            {
                this.fixture=fixture;this.proof=proof;
                Require(ReferenceEquals(proof.Fixture,fixture) && proof.Population==4000 && proof.Actors.Count==4000 &&
                    proof.IdleActors.Count==3000 && proof.Groups.Count==32 && !proof.ObserveArmyMoves,
                    "Health diagnostic requires its complete4000/1000-walker profile");
                foreach(var actor in proof.Actors)actors.Add(actor.Id,actor);
                // Real Assembly-CSharp declares this private. Read and validate
                // its existing object; never take, replace or release its lock.
                engineLockField=typeof(EngineInterface).GetField("threadLock",BindingFlags.Static|BindingFlags.NonPublic);
                Require(engineLockField!=null && engineLockField.FieldType==typeof(object),"Diagnostic engine monitor contract changed");
                engineLock=engineLockField.GetValue(null);Require(engineLock!=null,"Diagnostic engine monitor unavailable");
                try
                {
                    melee=UnitR3EventHooks.OnUnitTakeMeleeDamage.Observable.Subscribe(ObserveMelee);
                    projectile=UnitR3EventHooks.OnUnitTakeProjectileDamageEx.Observable.Subscribe(ObserveProjectile);
                    fixture.log("performance-health-diagnostic run="+proof.RunId+" profile="+fixture.scenario+
                        " population=4000 walkingPopulation=1000 diagnosticSubscriptions=true performanceAcceptanceClaim=false"+
                        " recordLimit="+RecordLimit+" publicationLimit="+PublicationLimit+" passiveComparison=false");
                }
                catch{Dispose();throw;}
            }
            internal void BindCurrentWorld(int currentIteration)
            {
                Require(Volatile.Read(ref disposed)==0 && ReferenceEquals(engineLockField.GetValue(null),engineLock) &&
                    Monitor.IsEntered(engineLock),"Diagnostic binding requires the unchanged held engine monitor");
                Require(WorldMutationEvents.TryGetCurrentWorld(out long sid,out long generation) &&
                    sid==fixture.session && sid==proof.LoadedSession && generation!=0,"Diagnostic actual loaded world unavailable");
                Require(fault==null,"Health diagnostic callback failed: "+fault);
                session=sid;world=generation;iteration=currentIteration;phase="loaded-boundary";retired.Clear();
                Volatile.Write(ref active,1);
            }
            internal void SetPhase(int currentIteration,string currentPhase)
            {iteration=currentIteration;phase=currentPhase;}
            internal void Suspend()=>Volatile.Write(ref active,0);
            internal void RequireHealthy()
            {
                Require(ReferenceEquals(engineLockField.GetValue(null),engineLock) && Monitor.IsEntered(engineLock),
                    "Diagnostic outer boundary lost the audited engine monitor");
                Require(fault==null,"Health diagnostic callback failed: "+fault);
            }
            private void ObserveMelee(UnitTakeDamageByMeleeEventArgs args)
            {
                if(Volatile.Read(ref active)==0)return;
                try
                {
                    Require(args!=null && (args.Phase==EventHookPhase.Pre || args.Phase==EventHookPhase.Post),
                        "Unknown melee diagnostic publication");
                    Observe("melee",args.Phase,args.DamagedUnitId,args.AttackingUnitId,-1,args.Damage,
                        " skipAtObservation="+args.SkipOriginalFunction+" postArgumentsMayBeOriginal=true");
                }
                catch(Exception error){Fail(error);}
            }
            private void ObserveProjectile(UnitTakeDamageByProjectileExEventArgs args)
            {
                if(Volatile.Read(ref active)==0)return;
                try
                {
                    Require(args!=null && args.Phase==EventHookPhase.Pre,"Unknown projectileEx diagnostic publication");
                    Observe("projectileEx",args.Phase,args.AttackedUnitId,args.AttackingUnitId,args.ProjectileId,args.Damage,
                        " preHpWriteSite=true laterDamageSubscribersUnobserved=true");
                }
                catch(Exception error){Fail(error);}
            }
            private unsafe void Observe(string kind,EventHookPhase hookPhase,int victimId,int attackerId,int projectileId,int damage,string semantics)
            {
                if(Volatile.Read(ref active)==0 || Volatile.Read(ref disposed)!=0 || !proof.Active || fixture.stopped)return;
                Require(Monitor.IsEntered(engineLock),"Damage event arrived outside the existing engine monitor");
                if(!WorldMutationEvents.TryGetCurrentWorld(out long sid,out long generation) || sid!=session || generation!=world)
                {Suspend();throw new InvalidOperationException("Active diagnostic crossed its bound native world");}
                Require(Interlocked.Increment(ref publications)<=PublicationLimit,"Health diagnostic publication budget exceeded");
                if(!actors.TryGetValue(victimId,out var actor) || retired.Contains(victimId))return;
                // Exact authority precedes records/fault labels for this life.
                if(!UnitAccess.TryGetById(victimId,out GameUnit* victim,out _) || victim==null || victim->r_GlobalId!=actor.Global ||
                    victim->r_UnitChimp!=actor.Type || victim->r_ControllableForPlayerId!=fixture.owner)
                {retired.Add(victimId);return;}
                Require(SimulationTickEvents.TryGetCurrentTick(out int tick),"Damage event has no admitted native tick");
                int sequence=Interlocked.Increment(ref records);Require(sequence<=RecordLimit,"Matched health diagnostic record budget exceeded");
                uint hp=victim->r_CurrentHealth,max=victim->r_MaxHealth;
                string victimFacts=" victim="+actor.Id+"/"+actor.Global+" type="+(int)victim->r_UnitChimp+
                    " owner="+victim->r_ControllableForPlayerId+" tribe="+victim->r_TribeId+" savedTribe="+actor.Tribe+
                    " currentHealth="+hp+" maxHealth="+max+" savedHealth="+actor.Health+" savedDelta="+((long)hp-actor.Health)+
                    " life="+(int)victim->r_AliveState+" ai="+victim->r_AIState+" flags="+victim->r_PathPlanStateBitFlags+
                    " ss="+victim->r_MovementSubstep+" current="+victim->r_CurrentTilePositionX+","+victim->r_CurrentTilePositionY+
                    " next="+victim->r_NextTilePositionX2+","+victim->r_NextTilePositionY2+
                    " packedCurrent="+victim->r_CurrentPositionTileId+" packedNextRaw="+victim->r_NextPositionTileId2+
                    " goal="+victim->r_TargetTilePositionX2+","+victim->r_TargetTilePositionY2+
                    " savedSource="+actor.SourceX+","+actor.SourceY;
                string attacker=" reportedAttackerId="+attackerId+" attackerCurrentRecord=unavailable";
                if(attackerId>0 && UnitAccess.TryGetById(attackerId,out GameUnit* source,out _) && source!=null)
                    attacker=" reportedAttackerId="+attackerId+" attackerCurrentGlobal="+source->r_GlobalId+
                        " attackerCurrentType="+(int)source->r_UnitChimp+" attackerCurrentOwner="+source->r_ControllableForPlayerId+
                        " attackerCurrentLife="+(int)source->r_AliveState+" attackerCurrentHealth="+source->r_CurrentHealth+
                        " attackerCurrentXY="+source->r_CurrentTilePositionX+","+source->r_CurrentTilePositionY;
                fixture.log("performance-health-event run="+proof.RunId+" sequence="+sequence+" session="+sid+" world="+generation+
                    " iteration="+iteration+" phase="+phase+" enabled="+proof.Enabled+" nativeTick="+tick+
                    " callbackThread="+Thread.CurrentThread.ManagedThreadId+" kind="+kind+" hookPhase="+hookPhase+
                    " reportedDamage="+damage+" reportedProjectileId="+projectileId+victimFacts+attacker+semantics+
                    " reportedArgumentsOnly=true exclusiveCauseUnproved=true shooterLifetimeUnproved=true"+
                    " diagnosticSubscriptions=true performanceAcceptanceClaim=false");
            }
            private void Fail(Exception error)
            {Interlocked.CompareExchange(ref fault,error.ToString(),null);Suspend();}
            public void Dispose()
            {
                Suspend();if(Interlocked.Exchange(ref disposed,1)!=0)return;
                try{melee?.Dispose();}finally{projectile?.Dispose();}
                melee=null;projectile=null;
            }
        }
    }
}
#endif
