using System;
using System.Collections.Generic;
using System.Reflection;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest.Tests
{
    internal static class RescueGeometryWitnessFixture
    {
        private static int checks;
        private static void Require(bool value,string why){checks++;if(!value)throw new Exception("Geometry witness: "+why);}
        private static UnitSnapshot Original(bool step=true)
        {
            var u=new UnitSnapshot{Id=11,GlobalId=901,Type=(int)eChimps.CHIMP_TYPE_FARMER_APPLE,Owner=2,
                X=20,Y=30,TileId=1000,NextX=step?21:20,NextY=30,NextTileId=step?1001:1000,
                Flags=step?2:0,Substep=step?3:8,OrdinaryStep=step,AffectedCurrent=true,AffectedNext=step,
                CurrentWasOrdinary=true,NextWasOrdinary=true,GroundFlags=8,NextGroundFlags=2,
                AIState=7,Tribe=88,TribeGlobalId=42,RequestedX=70,RequestedY=75,Health=50,
                HasCapturedVegetation=true,CapturedVegetationGlobal=101,Job=Job(7,19),CrewIdentity=new uint[]{77}};
            u.Candidates.Add(new RescueCandidate{X=20,Y=30,Tile=1000,Distance=0});
            u.Candidates.Add(new RescueCandidate{X=21,Y=30,Tile=1001,Distance=1});
            u.Candidates.Add(new RescueCandidate{X=12,Y=22,Tile=700,Distance=12});return u;
        }
        private static WorkerJourneySnapshot Job(int state,ushort purpose)
        {
            WorkerTravelPolicy.TryDescribe(eChimps.CHIMP_TYPE_FARMER_APPLE,state,out var rule);
            Require(WorkerJourneySnapshot.TryCapture(rule,offset=>offset==0x6E6?(ushort)rule.UnitType:
                offset==0x918?(ushort)state:offset==0x9D0?purpose:(ushort)0,_=>0,out var job),"real immutable typed purpose fixture");return job;
        }
        private static UnitSnapshot Fresh(bool atNext=false,bool step=false)
        {
            var u=new UnitSnapshot();
            // Seed every scalar, including future added fields. A forgotten copy in
            // the join must not pass just because its fixture default was zero.
            foreach(var f in Fields())
            {
                if(f.FieldType==typeof(int))f.SetValue(u,37);
                else if(f.FieldType==typeof(uint))f.SetValue(u,99u);
                else if(f.FieldType==typeof(bool))f.SetValue(u,true);
                else if(f.FieldType==typeof(byte))f.SetValue(u,(byte)5);
                else if(f.FieldType==typeof(string))f.SetValue(u,"fresh-boundary-marker");
            }
            u.Id=11;u.GlobalId=901;u.Type=(int)eChimps.CHIMP_TYPE_FARMER_APPLE;u.Owner=2;
            u.X=atNext?21:20;u.Y=30;u.TileId=atNext?1001:1000;
            u.NextX=step?21:u.X;u.NextY=30;u.NextTileId=step?1001:u.TileId;
            u.Flags=4;u.Substep=step?5:8;u.OrdinaryStep=step;
            u.AIState=2;u.RequestedX=70;u.RequestedY=75;u.Job=Job(2,83);u.CrewIdentity=new uint[]{1,2,3};
            u.Candidates.Add(new RescueCandidate{X=90,Y=90,Tile=9000,Distance=3});return u;
        }
        private static FieldInfo[] Fields()=>typeof(UnitSnapshot).GetFields(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
        private static void PreservesFresh(UnitSnapshot fresh,UnitSnapshot joined)
        {
            var historical=new HashSet<string>{"AffectedCurrent","AffectedNext","CurrentWasOrdinary","NextWasOrdinary","GroundFlags","NextGroundFlags","Candidates","CrewIdentity","GeometryWitness"};
            foreach(var f in Fields())if(!historical.Contains(f.Name))Require(Equals(f.GetValue(fresh),f.GetValue(joined)),"fresh field preserved: "+f.Name);
            Require(!ReferenceEquals(fresh.CrewIdentity,joined.CrewIdentity) && fresh.CrewIdentity.Length==joined.CrewIdentity.Length,"crew array not aliased");
            for(int i=0;i<fresh.CrewIdentity.Length;i++)Require(fresh.CrewIdentity[i]==joined.CrewIdentity[i],"fresh crew identity preserved "+i);
        }
        internal static void Run()
        {
            checks=0;
            foreach(var field in typeof(RescueGeometryWitness).GetFields(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public))
                Require(field.IsInitOnly && (field.FieldType.IsValueType || field.FieldType==typeof(RescueCandidate[])),"witness retains immutable geometry/lifetime data only: "+field.Name);
            var original=Original();var fresh=Fresh();
            Require(RescueGeometryWitness.TryCapture(original,out var witness),"causal geometry captured");
            Require(RescueGeometryWitness.TryJoin(witness,fresh,out var joined),"fresh active route at original Current");PreservesFresh(fresh,joined);
            Require(joined.AffectedCurrent && joined.CurrentWasOrdinary && joined.GroundFlags==8,"historical Current evidence joined");
            Require(ReferenceEquals(joined.GeometryWitness,witness),"joined persistence retains explicit original spatial anchor");
            Require(!joined.OrdinaryStep && !joined.AffectedNext && joined.NextGroundFlags==8,"stationary fresh frame creates no accepted step");
            Require(ReferenceEquals(joined.Job,fresh.Job) && !ReferenceEquals(joined.Job,original.Job) && joined.AIState==2 && joined.Tribe==fresh.Tribe,"same-goal new purpose wins over original job/AI/group");
            Require(joined.HasCapturedVegetation==fresh.HasCapturedVegetation && joined.CapturedVegetationGlobal==99 && joined.CapturedVegetationGlobal!=original.CapturedVegetationGlobal,"only fresh external vegetation evidence retained");
            Require(joined.Candidates.Count==3 && joined.Candidates[2].Tile==700 && fresh.Candidates.Count==1,"only original bank list copied, inputs unchanged");
            joined.CrewIdentity[0]=500;Require(fresh.CrewIdentity[0]==1 && original.CrewIdentity[0]==77,"joined crew mutation cannot modify either input");
            joined.Candidates[0]=new RescueCandidate{Tile=111};Require(witness.CopyCandidates()[0].Tile==1000 && original.Candidates[0].Tile==1000,"joined candidate mutation cannot alter witness/source");
            original.Candidates.Clear();original.AIState=111;original.RequestedX=499;original.CapturedVegetationGlobal=9999;
            Require(witness.CandidateCount==3 && RescueGeometryWitness.TryJoin(witness,fresh,out joined) && joined.AIState==2 && joined.RequestedX==70,"no old snapshot reference retained");
            var copied=witness.CopyCandidates();copied[0].X=600;Require(witness.CopyCandidates()[0].X==20,"candidate getter returns independent scalar copy");
            fresh=Fresh(true);Require(RescueGeometryWitness.TryJoin(witness,fresh,out joined),"fresh Current at originally accepted Next");PreservesFresh(fresh,joined);
            Require(joined.GroundFlags==2 && joined.CurrentWasOrdinary && joined.AffectedCurrent && !joined.OrdinaryStep && !joined.AffectedNext,"old Next maps to fresh Current only");
            Require(joined.Candidates[0].Distance==0 && joined.Candidates[0].X==20 && Math.Abs(joined.Candidates[2].X-joined.X)==9,"historical bank coordinates/order/distance remain in original frame");
            fresh=Fresh(false,true);Require(RescueGeometryWitness.TryJoin(witness,fresh,out joined),"same originally accepted edge remains admissible");PreservesFresh(fresh,joined);
            Require(joined.GroundFlags==8 && joined.NextGroundFlags==2 && joined.AffectedNext && joined.Substep==5 && joined.Flags==4,"fresh movement phase and both old endpoints kept separately");
            foreach(var alter in new Action<UnitSnapshot>[]
            {
                u=>u.Id++,u=>u.GlobalId++,u=>u.Type++,u=>u.Owner++,u=>u.TileId++,u=>u.X++,
                u=>u.Flags=0,u=>u.Flags=5,u=>u.Substep=9,u=>u.Substep=-1,
                u=>u.NextTileId++,u=>u.NextX++,u=>u.OrdinaryStep=true,
                u=>{u.Substep=3;u.OrdinaryStep=false;},
                u=>{u.Substep=3;u.OrdinaryStep=true;u.NextX=22;u.NextTileId=1002;}
            })
            {fresh=Fresh();alter(fresh);Require(!RescueGeometryWitness.TryJoin(witness,fresh,out joined) && joined==null,"invalid lifetime/pose cannot obtain historical authority");}
            fresh=Fresh(true);fresh.Substep=2;fresh.OrdinaryStep=true;fresh.NextX=22;fresh.NextTileId=1002;
            Require(!RescueGeometryWitness.TryJoin(witness,fresh,out _),"second accepted step from original Next is unproved");
            fresh=Fresh(false,true);fresh.NextX=19;fresh.NextTileId=999;
            Require(!RescueGeometryWitness.TryJoin(witness,fresh,out _),"new edge from original Current is unproved");
            var idle=Original(false);Require(RescueGeometryWitness.TryCapture(idle,out var idleWitness),"stationary original records only Current");
            Require(RescueGeometryWitness.TryJoin(idleWitness,Fresh(),out _),"new stationary fresh pose at old Current");
            Require(!RescueGeometryWitness.TryJoin(idleWitness,Fresh(false,true),out _),"fresh route cannot invent accepted Next from stationary original");
            original=Original();original.AffectedCurrent=false;original.CurrentWasOrdinary=false;
            Require(RescueGeometryWitness.TryCapture(original,out var nextOnly),"affected old Next supplies causal evidence");
            Require(RescueGeometryWitness.TryJoin(nextOnly,Fresh(true),out joined) && joined.AffectedCurrent && joined.CurrentWasOrdinary,"next-only proof rebases correctly");
            Require(RescueGeometryWitness.TryJoin(nextOnly,Fresh(),out joined) && !joined.AffectedCurrent && !joined.CurrentWasOrdinary,"next-only proof never strengthens original Current");
            var soldier=Original(false);soldier.Type=(int)eChimps.CHIMP_TYPE_SPEARMAN;soldier.Job=null;
            Require(RescueGeometryWitness.TryCapture(soldier,out var soldierWitness),"command family uses the same geometry-only proof");
            var commandPre=Fresh();commandPre.Type=soldier.Type;commandPre.AIState=101;commandPre.Job=null;commandPre.Tribe=0;commandPre.TribeGlobalId=0;
            commandPre.HasCapturedVegetation=false;commandPre.CapturedVegetationGlobal=0;
            Require(CommandJourneySnapshot.TryCapture(eChimps.CHIMP_TYPE_SPEARMAN,101,offset=>offset==0x6E4?(ushort)AliveState.IsAlive:
                offset==0x6E6?(ushort)eChimps.CHIMP_TYPE_SPEARMAN:offset==0x918?(ushort)101:offset==0x6EE?(ushort)2:
                offset==0xA9E?(ushort)1:offset==0x958?(ushort)9:(ushort)0,offset=>offset==0x6F0?901u:0u,out commandPre.Command),"real immutable fresh command fixture");
            Require(RescueGeometryWitness.TryJoin(soldierWitness,commandPre,out joined) && ReferenceEquals(joined.Command,commandPre.Command) &&
                joined.Job==null && joined.AIState==101 && joined.RequestedX==commandPre.RequestedX,"fresh command purpose/goal survives geometry transfer");
            InvalidCaptures();
            Console.WriteLine("RescueGeometryWitnessFixture: "+checks+" checks passed");
        }
        private static void InvalidCaptures()
        {
            Require(!RescueGeometryWitness.TryCapture(null,out _),"null source rejected");
            foreach(var alter in new Action<UnitSnapshot>[]
            {
                u=>u.Id=0,u=>u.GlobalId=0,u=>u.X=-1,u=>u.Y=800,u=>u.TileId=NativeRelocation.TileCapacity,
                u=>u.Flags=0,u=>u.Substep=8,u=>u.Substep=-1,u=>u.NextTileId=u.TileId,u=>u.NextX=22,
                u=>{u.NextX=u.X;u.NextY=u.Y;},u=>{u.AffectedCurrent=false;u.AffectedNext=false;},
                u=>{u.CurrentWasOrdinary=false;u.NextWasOrdinary=false;},
                u=>u.Candidates.Add(u.Candidates[0]),u=>u.Candidates[1]=new RescueCandidate{X=29,Y=30,Tile=1100,Distance=1},
                u=>u.Candidates[1]=new RescueCandidate{X=21,Y=30,Tile=1001,Distance=0},
                u=>u.Candidates[1]=new RescueCandidate{X=21,Y=30,Tile=1001,Distance=-1}
            })
            {var original=Original();alter(original);Require(!RescueGeometryWitness.TryCapture(original,out _),"invalid original geometry rejected");}
            var crowded=Original();crowded.Candidates.Clear();
            for(int i=0;i<=RescueGeometryWitness.MaximumCandidates;i++)crowded.Candidates.Add(new RescueCandidate{X=20,Y=30,Tile=i,Distance=1});
            Require(!RescueGeometryWitness.TryCapture(crowded,out _),"candidate bound fails before unbounded copy");
            Require(!RescueGeometryWitness.TryJoin(null,Fresh(),out _) && !RescueGeometryWitness.TryJoin(null,null,out _),"missing geometry supplies no authority");
        }
    }
}
