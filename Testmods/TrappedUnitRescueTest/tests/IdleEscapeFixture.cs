using System;
using System.Collections.Generic;
using System.Reflection;
using APIShared.Units;

namespace TrappedUnitRescueTest.Tests
{
    internal static class IdleEscapeFixture
    {
        private static int checks;
        private static void Require(bool value,string why){checks++;if(!value)throw new Exception("Idle escape: "+why);}
        private static IdleEscapeRequest Request(int id=1,long session=1,long generation=1,long order=1)=>
            new IdleEscapeRequest(session,generation,order,new UnitSnapshot{Id=id,GlobalId=(uint)(100+id),X=10,Y=10,TileId=50},
                new RescueCandidate{X=11,Y=10,Tile=51},false);
        internal static void Run()
        {
            checks=0;
            var world=new World();var owner=new IdleEscapeOwner(world,2,1,8);
            world.DuringMove=t=>{Require(owner.Owns(t),"reservation already owns native call");Require(!owner.TryStart(Request(2),1,out _),"nested instruction rejected");};
            Require(owner.TryStart(Request(),1,out _) && owner.LastState==IdleEscapeState.InProgress && owner.Count==1,"positive publication remains in progress");
            Require(world.Moves==1 && world.Relocations==0,"publication is not teleported");
            Require(!owner.TryStart(Request(),1,out _) && world.Moves==1,"duplicate does not move twice");
            world.DuringMove=null;
            Require(owner.TryStart(Request(2),1,out _),"second independent receipt");
            Require(!owner.TryStart(Request(3),1,out var reason) && reason=="idle-escape-capacity" && world.Moves==2,"capacity rejects before native");
            Require(owner.Advance(2)==1 && owner.Count==2,"fixed inspection budget");
            world.States[2]=IdleEscapeInspection.Arrived;
            Require(owner.Advance(2)==0 && owner.Count==2,"duplicate tick cannot spend inspection budget twice");
            Require(owner.Advance(3)==1 && owner.Count==1 && owner.LastState==IdleEscapeState.Completed && owner.CompletedCount==1,"fair rotation observes actual second arrival");
            world.States[1]=IdleEscapeInspection.Changed;
            Require(owner.Advance(4)==1 && owner.Count==0 && owner.LastState==IdleEscapeState.Superseded && owner.CompletedCount==1,"foreign intent releases without restoring or counting success");
            owner.Clear();Require(owner.CompletedCount==1,"completion metric survives Clear");
            Require(world.Moves==2 && world.Relocations==0,"monitor never retries or restores");
            foreach(var actual in new[]{IdleEscapeMove.Other,IdleEscapeMove.NativeSucceeded,IdleEscapeMove.NativeRefused})
            {
                world=new World{Actual=actual,Published=false};owner=new IdleEscapeOwner(world);
                bool accepted=owner.TryStart(Request(),0,out _);
                Require(accepted==(actual==IdleEscapeMove.NativeRefused),"only exact actual zero permits fallback "+actual);
                Require(world.Relocations==(actual==IdleEscapeMove.NativeRefused?1:0),"veto/redirect/failed publication never relocates "+actual);
                Require(owner.Count==0,"terminal instruction releases receipt "+actual);
                Require(owner.CompletedCount==(accepted?1:0),"only actual physical arrival increments metric "+actual);
            }
            world=new World{Actual=IdleEscapeMove.NativeRefused,Fallback=false};owner=new IdleEscapeOwner(world);
            Require(!owner.TryStart(Request(),0,out _) && world.Relocations==0,"changed AI/context/source forbids refusal fallback");
            world=new World{Actual=IdleEscapeMove.NativeRefused,Relocated=false};owner=new IdleEscapeOwner(world);
            Require(!owner.TryStart(Request(),0,out _) && world.Relocations==1 && owner.Count==0,"failed relocation never reported rescued");
            world=new World{Actual=IdleEscapeMove.NativeRefused,ArrivalAfterRelocate=false};owner=new IdleEscapeOwner(world);
            Require(!owner.TryStart(Request(),0,out _) && owner.Count==0,"physical postcondition required after relocation");
            world=new World{ThrowMove=true};owner=new IdleEscapeOwner(world);
            Require(!owner.TryStart(Request(),0,out _) && world.Relocations==0 && owner.Count==0,"throw does not masquerade as native refusal");
            world=new World{ThrowCapture=true};owner=new IdleEscapeOwner(world);
            Require(!owner.TryStart(Request(),0,out _) && world.Moves==0 && owner.Count==0,"capture exception isolated before reservation");
            world=new World();owner=new IdleEscapeOwner(world);
            world.DuringMove=t=>owner.Release(t.UnitId);
            Require(!owner.TryStart(Request(),0,out _) && owner.LastState==IdleEscapeState.Superseded && world.Relocations==0,"nested foreign authority wins");
            world=new World{Actual=IdleEscapeMove.NativeRefused};owner=new IdleEscapeOwner(world);
            world.DuringFallback=t=>owner.Clear();
            Require(!owner.TryStart(Request(),0,out _) && world.Relocations==0,"fallback revalidates reservation after observer");
            world=new World();owner=new IdleEscapeOwner(world,1,1,8);
            Require(owner.TryStart(Request(),uint.MaxValue-3,out _),"deadline starts before native wrap");
            Require(owner.Advance(2)==1 && owner.Count==1,"wrap age six is not expired");
            Require(owner.Advance(4)==1 && owner.Count==0 && owner.LastState==IdleEscapeState.Expired,"wrap age eight expires without replay");
            world=new World();owner=new IdleEscapeOwner(world,1,1,8);
            for(int i=0;i<1000;i++)
            {Require(owner.TryStart(Request(),0,out _),"replacement can reserve after release "+i);owner.Release(1);}
            Require(owner.Advance(0)==0 && owner.Count==0 && world.Inspections==1000,"released receipts leave no stale unbounded scan");
            foreach(bool changedSession in new[]{true,false})
            {
                world=new World();owner=new IdleEscapeOwner(world);
                Require(owner.TryStart(Request(),0,out _),"identity watch admission");
                if(changedSession)world.Session=2;else world.Order=2;
                Require(owner.Advance(1)==1 && owner.Count==0 && owner.LastState==IdleEscapeState.Superseded,"session/new order cancels owned escape");
                Require(world.Moves==1 && world.Relocations==0,"identity invalidation has no native side effect");
            }
            world=new World{Accept=false};owner=new IdleEscapeOwner(world);
            Require(!owner.TryStart(Request(),0,out _) && world.Moves==0,"admission rejection cannot issue goal");
            world=new World();owner=new IdleEscapeOwner(world,2,2);
            Require(owner.TryStart(Request(),0,out _) && owner.TryStart(Request(2),0,out _),"two receipts before simultaneous invalidation");
            world.DuringInspect=t=>owner.Clear();
            Require(owner.Advance(1)==1 && owner.Count==0 && owner.CompletedCount==0,"world invalidation during inspection cannot enqueue stale receipts or overrun empty schedule");
            world=new World();world.States[1]=IdleEscapeInspection.Arrived;owner=new IdleEscapeOwner(world);
            Require(owner.TryStart(Request(),0,out _) && owner.Count==0 && owner.CompletedCount==1 && world.Relocations==0,"synchronous native physical arrival counted once");
            NativeResults();
            NativePhase();
            PhysicalExit();
            Console.WriteLine("IdleEscapeFixture: "+checks+" checks passed");
        }
        private static void NativeResults()
        {
            // Construct SDK immutable facts through reflection, never invoking a
            // native entry or using the mutable legacy Post result as evidence.
            foreach(UnitMoveRunStatus status in Enum.GetValues(typeof(UnitMoveRunStatus)))
            {
                var result=Result(status,status==UnitMoveRunStatus.NativeSucceeded?1:0);
                var expected=status==UnitMoveRunStatus.NativeSucceeded?IdleEscapeMove.NativeSucceeded:
                    status==UnitMoveRunStatus.NativeRefused?IdleEscapeMove.NativeRefused:IdleEscapeMove.Other;
                Require(NativeIdleEscapeWorld.ClassifyMove(result,true)==expected,"all SDK statuses distinguish actual result "+status);
                Require(NativeIdleEscapeWorld.ClassifyMove(result,false)==IdleEscapeMove.Other,"foreign source always wins "+status);
            }
            foreach(string fact in new[]{"NativeInvoked","NativeReturned","WrapperReturned","IsCurrentWorld"})
            {
                var result=Result(UnitMoveRunStatus.NativeRefused,0);Set(result,fact,false);
                Require(NativeIdleEscapeWorld.ClassifyMove(result,true)==IdleEscapeMove.Other,"refusal requires "+fact);
            }
            foreach(string failure in new[]{"Failure","WrapperFailure"})
            {
                var result=Result(UnitMoveRunStatus.NativeRefused,0);Set(result,failure,new InvalidOperationException());
                Require(NativeIdleEscapeWorld.ClassifyMove(result,true)==IdleEscapeMove.Other,"exceptions forbid fallback "+failure);
            }
            Require(NativeIdleEscapeWorld.ClassifyMove(Result(UnitMoveRunStatus.NativeRefused,1),true)==IdleEscapeMove.Other,"classification alone cannot forge zero");
            Require(NativeIdleEscapeWorld.ClassifyMove(Result(UnitMoveRunStatus.NativeSucceeded,0),true)==IdleEscapeMove.Other,"classification alone cannot forge success");
            Require(NativeIdleEscapeWorld.ClassifyMove(Result(UnitMoveRunStatus.NativeRefused,-1),true)==IdleEscapeMove.Other,"unsupported native negative is not zero");
            var overridden=Result(UnitMoveRunStatus.NativeRefused,0);Set(overridden,"WrapperResult",1L);
            Require(NativeIdleEscapeWorld.ClassifyMove(overridden,true)==IdleEscapeMove.NativeRefused,"legacy Post success cannot conceal native refusal");
            overridden=Result(UnitMoveRunStatus.NativeSucceeded,1);Set(overridden,"WrapperResult",0L);
            Require(NativeIdleEscapeWorld.ClassifyMove(overridden,true)==IdleEscapeMove.NativeSucceeded,"legacy Post zero cannot hide native publication");
            var postThrew=Result(UnitMoveRunStatus.NativeSucceeded,1);Set(postThrew,"WrapperFailure",new Exception());Set(postThrew,"WrapperReturned",false);
            var actual=NativeIdleEscapeWorld.ClassifyMove(postThrew,true);
            Require(actual==IdleEscapeMove.NativeSucceeded,"native one survives a legacy Post exception as read-only monitoring evidence");
            foreach(bool published in new[]{true,false})
            {
                var world=new World{Actual=actual,Published=published};var owner=new IdleEscapeOwner(world);
                Require(owner.TryStart(Request(),0,out _)==published,"Postthrow actual one still requires coherent live publication "+published);
                Require(world.Relocations==0 && owner.CompletedCount==0,"Postthrow cannot gain a relocation or unobserved completion "+published);
            }
            Set(postThrew,"NativeResult",0L);Set(postThrew,"Status",UnitMoveRunStatus.NativeRefused);
            Require(NativeIdleEscapeWorld.ClassifyMove(postThrew,true)==IdleEscapeMove.Other,"Postthrow actual zero never permits fallback");
        }
        private static UnitMoveRunResult Result(UnitMoveRunStatus status,long actual)
        {
            var result=(UnitMoveRunResult)Activator.CreateInstance(typeof(UnitMoveRunResult),true);
            Set(result,"Status",status);Set(result,"NativeResult",actual);
            foreach(string fact in new[]{"NativeInvoked","NativeReturned","WrapperReturned","IsCurrentWorld"})Set(result,fact,true);
            return result;
        }
        private static void Set(UnitMoveRunResult result,string property,object value)=>
            typeof(UnitMoveRunResult).GetProperty(property,BindingFlags.Public|BindingFlags.Instance).GetSetMethod(true).Invoke(result,new[]{value});
        private static UnitMoveContext Context(long session=1,long generation=1,int unit=1,int x=11,int y=10,int extra=0,string owner="ours")=>
            (UnitMoveContext)Activator.CreateInstance(typeof(UnitMoveContext),BindingFlags.Instance|BindingFlags.NonPublic,null,
                new object[]{1L,0L,session,generation,unit,x,y,extra,owner},null);
        private static UnitMoveCompletion Completion(UnitMoveContext context)=>
            (UnitMoveCompletion)Activator.CreateInstance(typeof(UnitMoveCompletion),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{context},null);
        private static void NativePhase()
        {
            foreach(var wrong in new[]{Context(session:2),Context(generation:2),Context(unit:2),Context(x:12),Context(y:11),Context(extra:1),Context(owner:null),Context(owner:"foreign")})
            {
                var phase=new IdleEscapeSourcePhase("ours",Request());
                Require(phase.TryBegin(wrong)==null && !phase.Finished,"wrong/nested invocation cannot relax source guard");
            }
            var exact=Context();var current=new IdleEscapeSourcePhase("ours",Request());
            var receipt=current.TryBegin(exact);
            Require(receipt!=null && !current.Finished,"own begin retains source guard through final observer check");
            receipt.Complete(Completion(Context()));
            Require(!current.Finished,"different immutable completion cannot finish source phase");
            receipt.Complete(null);Require(!current.Finished,"null completion cannot finish source phase");
            Require(current.TryBegin(Context())==null,"one phase cannot bind a second original");
            receipt.Complete(Completion(exact));Require(current.Finished,"matching completion relaxes only source phase for final identity check");
            // Even a noninvoked/failed completion ends this pre-original phase;
            // NativeResults independently rejects its missing actual-zero facts.
            receipt.Complete(Completion(exact));Require(current.Finished,"completion is idempotent");
        }
        private static void PhysicalExit()
        {
            var c=new RescueCandidate{X=10,Y=10,Tile=50};
            Func<int,int,int> northOnly=(x,y)=>x==10 && y==9?91:-1;
            Require(!NativeIdleEscapeWorld.HasPhysicalExit(c,northOnly,t=>false,t=>255),"stale connection bits into blocked terrain are not a physical exit");
            Require(!NativeIdleEscapeWorld.HasPhysicalExit(c,(x,y)=>-1,t=>true,t=>255),"inactive map boundary is not an exit");
            Require(!NativeIdleEscapeWorld.HasPhysicalExit(c,northOnly,t=>true,t=>0),"ordinary neighbour without a connection is not an exit");
            Require(NativeIdleEscapeWorld.HasPhysicalExit(c,northOnly,t=>true,t=>(byte)(t==50?1:0)),"source north edge reaches actual ordinary neighbour");
            Require(NativeIdleEscapeWorld.HasPhysicalExit(c,northOnly,t=>true,t=>(byte)(t==91?16:0)),"reverse neighbour south edge matches native symmetric edge policy");
            Require(!NativeIdleEscapeWorld.HasPhysicalExit(c,northOnly,t=>true,t=>(byte)(t==50?2:0)),"unrelated diagonal connection does not prove northern exit");
        }
        private sealed class World : IIdleEscapeWorld
        {
            internal IdleEscapeMove Actual=IdleEscapeMove.NativeSucceeded;
            internal bool Accept=true,Published=true,Fallback=true,Relocated=true,ArrivalAfterRelocate=true,ThrowMove,ThrowCapture;
            internal int Moves,Relocations,Inspections;
            internal long Session=1,Order=1;
            internal Action<IdleEscapeTicket> DuringMove,DuringFallback,DuringInspect;
            internal readonly Dictionary<int,IdleEscapeInspection> States=new Dictionary<int,IdleEscapeInspection>();
            public bool TryCapture(IdleEscapeRequest r,out IdleEscapeTicket t,out string reason)
            {if(ThrowCapture)throw new Exception();t=Accept?new IdleEscapeTicket(r,new uint[]{7}):null;reason=null;return Accept;}
            public IdleEscapeMove Move(IdleEscapeTicket t,Func<bool> current)
            {Moves++;if(ThrowMove)throw new Exception();DuringMove?.Invoke(t);return current()?Actual:IdleEscapeMove.Other;}
            public bool HasPublishedRoute(IdleEscapeTicket t)=>Published;
            public bool CanFallback(IdleEscapeTicket t){DuringFallback?.Invoke(t);return Fallback;}
            public bool RelocateAndNormalize(IdleEscapeTicket t)
            {Relocations++;if(Relocated && ArrivalAfterRelocate)States[t.UnitId]=IdleEscapeInspection.Arrived;return Relocated;}
            public IdleEscapeInspection Inspect(IdleEscapeTicket t)
            {Inspections++;DuringInspect?.Invoke(t);return t.Request.Session!=Session || t.Request.OrderSerial!=Order?IdleEscapeInspection.Changed:
                States.TryGetValue(t.UnitId,out var state)?state:IdleEscapeInspection.Waiting;}
        }
    }
}
