using System;
using System.Collections.Generic;
using TrappedUnitRescueTest;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest.Tests
{
    internal static class RescuePolicyFixture
    {
        private static readonly int[] Dx={0,1,1,1,0,-1,-1,-1}, Dy={-1,-1,0,1,1,1,0,-1};
        private static int checks;
        private static void Require(bool condition,string message)
        { checks++;if(!condition)throw new InvalidOperationException("RescuePolicyFixture: "+message); }
        internal static void Run()
        {
            // The test drives the actual production BFS and selector. Ground/edges are external boundaries.
            Func<int,int,int> tile=(x,y)=>x>=0 && x<25 && y>=0 && y<25?y*25+x:-1;
            Func<int,bool> ground=id=>id%25!=13; // Existing continuous wall cannot be crossed or used as a destination.
            Func<int,int,int,int,bool> edge=(id,x,y,d)=>ground(id) && tile(x+Dx[d],y+Dy[d])>=0 && ground(tile(x+Dx[d],y+Dy[d]));
            var found=RescueRuntime.FindLocalCandidates(12,12,tile,ground,edge);
            Require(found.Count>0,"reachable side remains available");
            var seen=new HashSet<int>();int lastDistance=0;
            foreach(var candidate in found)
            {
                Require(candidate.X<13,"unchanged wall never crossed");
                Require(Math.Max(Math.Abs(candidate.X-12),Math.Abs(candidate.Y-12))<=8,"radius is bounded");
                Require(candidate.Distance>=lastDistance,"shortest old local distance first");lastDistance=candidate.Distance;
                Require(seen.Add(candidate.Tile),"each old reachable tile appears once");
            }
            Require(!seen.Contains(tile(12,12)),"original cell never a relocation candidate");
            Require(!RescueRuntime.ChooseCandidate(found,c=>false).HasValue,"all blocked after construction produces no target");
            Require(RescueRuntime.ChooseCandidate(found,c=>true).Value.Tile==found[0].Tile,"deterministic first shortest target");
            int blocked=found[0].Tile;
            Require(RescueRuntime.ChooseCandidate(found,c=>c.Tile!=blocked).Value.Tile==found[1].Tile,"newly occupied first target is skipped");
            Require(RescueRuntime.FindLocalCandidates(13,12,tile,ground,edge).Count==0,"unpassable source cannot establish old reachability");
            Require(RescueRuntime.FindLocalCandidates(-1,12,tile,ground,edge).Count==0,"invalid source remains empty");
            Require(RescueRuntime.FindLocalCandidates(12,12,tile,ground,(id,x,y,d)=>false).Count==0,"old enclosure creates no rescue candidates");
            // A one-way old passage must follow its supplied edge relation; no reverse edge is invented by BFS.
            var directed=RescueRuntime.FindLocalCandidates(4,4,tile,id=>true,(id,x,y,d)=>d==2);
            Require(directed.Count==8,"one-way corridor limited to eight steps");
            foreach(var candidate in directed)Require(candidate.Y==4 && candidate.X>4,"directed field has no invented exits");
            Require(!RescueRuntime.ChooseCandidate(new List<RescueCandidate>(),c=>true).HasValue,"empty old field cannot authorize movement");
            StateClassification();
            CivilianResumePolicy();
            CrewShape();
            Console.WriteLine("PASS RescuePolicyFixture "+checks+" checks");
        }
        private static GameUnit OrdinaryUnit()=>new GameUnit { r_UnitChimp=eChimps.CHIMP_TYPE_SPEARMAN,
            r_CurrentHealth=100,r_MovementSubstep=8,N000000DB=1,r_AliveState=AliveState.IsAlive };
        private static void CivilianResumePolicy()
        {
            foreach(eChimps type in new[]{eChimps.CHIMP_TYPE_FARMER_APPLE,eChimps.CHIMP_TYPE_QUARRY_OX})
                foreach(int flags in new[]{2,4})
                {
                    Require(RescueRuntime.NeedsExplicitCivilianResume((int)type,flags,false),
                        "active civilian route without explicit continuation fails before native relocation");
                    Require(!RescueRuntime.NeedsExplicitCivilianResume((int)type,flags,true),
                        "explicit continuation retains the audited civilian travel experiment");
                }
            foreach(eChimps type in new[]{eChimps.CHIMP_TYPE_SPEARMAN,eChimps.CHIMP_TYPE_ARAB_ASSASIN,eChimps.CHIMP_TYPE_CATAPULT})
                foreach(int flags in new[]{0,2,4})
                    Require(!RescueRuntime.NeedsExplicitCivilianResume((int)type,flags,false),
                        "civilian continuation veto does not change the separate military/root policy");
        }
        private static void CrewShape()
        {
            Require(!RescueRuntime.HasCoherentCrewShape(null,0),"missing crew view rejected");
            Require(RescueRuntime.HasCoherentCrewShape(new uint[0],0),"non-siege empty crew view accepted");
            Require(!RescueRuntime.HasCoherentCrewShape(new uint[0],1),"empty view cannot claim mounted crew");
            Require(!RescueRuntime.HasCoherentCrewShape(new uint[6],0),"incomplete four-slot crew view rejected");
            Require(!RescueRuntime.HasCoherentCrewShape(new uint[8],-1),"negative crew count rejected");
            Require(!RescueRuntime.HasCoherentCrewShape(new uint[8],5),"crew count beyond capacity rejected");
            for(int count=0;count<=4;count++)
            {
                var valid=new uint[8];
                for(int slot=0;slot<4;slot++)
                { valid[slot*2]=slot<count?(uint)(slot+1):0;valid[slot*2+1]=(uint)(101+slot); }
                Require(RescueRuntime.HasCoherentCrewShape(valid,count),"native contiguous prefix accepted despite stale globals in empty slots");
                for(int slot=0;slot<4;slot++)
                {
                    var changed=(uint[])valid.Clone();
                    changed[slot*2]=slot<count?0u:999u;
                    Require(!RescueRuntime.HasCoherentCrewShape(changed,count),"first-count holes and trailing live IDs rejected");
                }
            }
            // Equal population is insufficient: native refresh reads A,0 and misses B.
            Require(!RescueRuntime.HasCoherentCrewShape(new uint[]{1,101,0,0,2,102,0,0},2),
                "matching nonzero population cannot conceal a hole in the refreshed prefix");
        }
        private static unsafe void StateClassification()
        {
            Require(RescueRuntime.ClassifyState(null)=="missing-unit","null state fails closed");
            GameUnit unit=OrdinaryUnit();
            foreach(ushort state in new ushort[]{0,1,101})
                foreach(ushort flags in new ushort[]{0,2,4})
                {
                    unit=OrdinaryUnit();unit.r_AIState=state;unit.r_PathPlanStateBitFlags=flags;
                    Require(RescueRuntime.ClassifyState(&unit)==null,"normal soldier segment accepted");
                }
            for(ushort state=126;state<=129;state++)
            {
                unit=OrdinaryUnit();unit.r_UnitChimp=eChimps.CHIMP_TYPE_ARAB_ASSASIN;unit.r_AIState=state;
                Require(RescueRuntime.ClassifyState(&unit)=="assassin-transition","active climb state deferred");
            }
            for(ushort step=0;step<8;step++)
            {
                unit=OrdinaryUnit();unit.r_MovementSubstep=step;
                Require(RescueRuntime.ClassifyState(&unit)=="step-in-progress","already accepted mid-step deferred");
            }
            foreach(int field in new[]{0x8EC,0x8F6})
            {
                unit=OrdinaryUnit();*(ushort*)((byte*)&unit+field-0x65C)=1;
                Require(RescueRuntime.ClassifyState(&unit)=="portal-or-transport","connection/transport state deferred");
            }
            unit=OrdinaryUnit();*(ushort*)((byte*)&unit+0x9B0-0x65C)=1;
            Require(RescueRuntime.ClassifyState(&unit)=="mounted-or-hidden","mounted engineer cannot become independent rescue root");
            unit=OrdinaryUnit();unit.r_CurrentHealth=0;
            Require(RescueRuntime.ClassifyState(&unit)=="dying","zero health deferred");
            unit=OrdinaryUnit();unit.r_IsKilledByProjectile=1;
            Require(RescueRuntime.ClassifyState(&unit)=="dying","native death marker deferred");
            foreach(ushort mode in new ushort[]{1,2,ushort.MaxValue})
            {
                unit=OrdinaryUnit();unit.N00000573=mode;
                Require(RescueRuntime.ClassifyState(&unit)=="nonordinary-movement-class","tunnel/special movement mode cannot use ordinary exit proof");
            }
            foreach(eChimps type in new[]{eChimps.CHIMP_TYPE_SPEARMAN,eChimps.CHIMP_TYPE_ARAB_ASSASIN})
                foreach(ushort flags in new ushort[]{0,2,4})
                    foreach(ushort permission in new ushort[]{1,ushort.MaxValue})
                    {
                        unit=OrdinaryUnit();unit.r_UnitChimp=type;unit.r_PathPlanStateBitFlags=flags;
                        *(ushort*)((byte*)&unit+0x9C8-0x65C)=permission;
                        Require(RescueRuntime.ClassifyState(&unit)=="stored-moat-route-needs-mode-aware-exit-proof",
                            "stored moat route cannot be mistaken for zero ordinary exits");
                        *(ushort*)((byte*)&unit+0x9C8-0x65C)=0;
                        Require(RescueRuntime.ClassifyState(&unit)==null,"ordinary zero-moat-permission state remains supported");
                    }
            foreach(ushort flags in new ushort[]{1,3,5,6,ushort.MaxValue})
            {
                unit=OrdinaryUnit();unit.r_PathPlanStateBitFlags=flags;
                Require(RescueRuntime.ClassifyState(&unit)=="special-path-state","unknown path state deferred");
            }
            unit=OrdinaryUnit();unit.N000000DB=0;
            Require(RescueRuntime.ClassifyState(&unit)=="unclassified-zero-group-order-field","unclassified zero group-order field remains unsupported without a false immobility claim");
            foreach(eChimps type in new[]{eChimps.CHIMP_TYPE_FARMER_APPLE,eChimps.CHIMP_TYPE_QUARRY_OX})
                for(ushort state=0;state<108;state++)
                    foreach(ushort flags in new ushort[]{0,2,4})
                    {
                        unit=OrdinaryUnit();unit.r_UnitChimp=type;unit.N000000DB=0;
                        unit.r_AIState=state;unit.r_PathPlanStateBitFlags=flags;
                        bool audited=flags!=0 && (type==eChimps.CHIMP_TYPE_FARMER_APPLE
                            ? state==2 || state==5 || state==7 || state==8 : state==1);
                        Require((RescueRuntime.ClassifyState(&unit)==null)==audited,
                            "only the explicitly audited civilian active travel states are authorized");
                    }
            unit=OrdinaryUnit();unit.r_UnitChimp=eChimps.CHIMP_TYPE_FARMER_APPLE;unit.r_AIState=3;
            unit.r_TargetVegetationId=227;unit.N000000C5=2;
            Require(RescueRuntime.ClassifyState(&unit)=="apple-worker-needs-audited-active-travel",
                "stationary picker cannot retain state3/counter/tree while being moved beyond the picking approach");
            foreach(eChimps type in new[]{eChimps.CHIMP_TYPE_SPEARMAN,eChimps.CHIMP_TYPE_ARAB_ASSASIN})
                foreach(ushort state in new ushort[]{2,3,4,5,6,7,8,100,102,103,104,105,106,107})
                    foreach(ushort flags in new ushort[]{0,2,4})
                    {
                        unit=OrdinaryUnit();unit.r_UnitChimp=type;unit.r_AIState=state;unit.r_PathPlanStateBitFlags=flags;
                        bool stationarySpearmanIdle=type==eChimps.CHIMP_TYPE_SPEARMAN && state==2 && flags==0;
                        Require(stationarySpearmanIdle?RescueRuntime.ClassifyState(&unit)==null:RescueRuntime.ClassifyState(&unit)=="unproven-order-state",
                            "unproven military work/combat orders cannot inherit an arbitrary relocated position");
                    }
            unit=OrdinaryUnit();unit.r_AIState=2;
            Require(RescueRuntime.ClassifyState(&unit)==null,"native Spearman idle animation2 is an audited stationary root");
            unit.r_PathPlanStateBitFlags=2;
            Require(RescueRuntime.ClassifyState(&unit)=="unproven-order-state","idle-animation exception cannot authorize a stale active route");
            unit=OrdinaryUnit();unit.r_UnitChimp=eChimps.CHIMP_TYPE_ARAB_ASSASIN;unit.r_AIState=2;
            Require(RescueRuntime.ClassifyState(&unit)=="unproven-order-state","same small state number does not establish another type's idle semantics");
            foreach(eChimps type in new[]{eChimps.CHIMP_TYPE_TREBUCHET,eChimps.CHIMP_TYPE_MANGONEL,eChimps.CHIMP_TYPE_BALLISTA})
            {
                unit=OrdinaryUnit();unit.r_UnitChimp=type;unit.N000000DB=0;
                Require(RescueRuntime.ClassifyState(&unit)=="unclassified-zero-group-order-field",
                    "stationary zero-table types are not authorized by the civilian exception");
            }
            unit=OrdinaryUnit();unit.r_UnitChimp=eChimps.CHIMP_TYPE_CATAPULT;unit.r_AIState=8;
            Require(RescueRuntime.ClassifyState(&unit)=="siege-special-state","active siege work/deployment deferred");
            unit=OrdinaryUnit();unit.r_UnitChimp=eChimps.CHIMP_TYPE_CATAPULT;unit.r_AIState=101;
            Require(RescueRuntime.ClassifyState(&unit)==null,"ordinary moving device root accepted");
        }
    }
}
