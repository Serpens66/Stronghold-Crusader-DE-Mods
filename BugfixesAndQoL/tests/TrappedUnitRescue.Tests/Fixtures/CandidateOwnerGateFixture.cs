using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest.Tests
{
    // Real owner gate + real local BFS + real occupancy discovery. Inject only
    // terrain/record boundaries; no native singleton, mirrored gate or game call.
    internal static unsafe class CandidateOwnerGateFixture
    {
        private static int checks;
        private static void Check(bool value,string message){checks++;if(!value)throw new InvalidOperationException(message);}
        private static int Tile(int x,int y)=>x>=0 && x<25 && y>=0 && y<25?y*25+x:-1;
        private static readonly int[] Dx={0,1,1,1,0,-1,-1,-1},Dy={-1,-1,0,1,1,1,0,-1};
        private static bool Ground(int tile)=>tile%25!=15;
        private static bool Edge(int tile,int x,int y,int direction)
        {int next=Tile(x+Dx[direction],y+Dy[direction]);return next>=0 && Ground(tile) && Ground(next);}
        private static UnitSnapshot Unit(int owner)=>new UnitSnapshot{Id=1,GlobalId=81,Owner=owner,Type=(int)eChimps.CHIMP_TYPE_SPEARMAN,
            X=12,Y=12,TileId=Tile(12,12),RequestedX=8,RequestedY=8,Health=123,Flags=2,Substep=3};
        private static void SameField(IList<RescueCandidate> actual,IList<RescueCandidate> reference,string message)
        {
            Check(actual.Count==reference.Count,message+" count");
            for(int i=0;i<actual.Count;i++)Check(actual[i].X==reference[i].X && actual[i].Y==reference[i].Y &&
                actual[i].Tile==reference[i].Tile && actual[i].Distance==reference[i].Distance,message+" order/distance "+i);
        }
        internal static void Run()
        {
            checks=0;int calls=0;
            Action<UnitSnapshot> capture=value=>{calls++;value.Candidates.AddRange(
                RescueRuntime.FindLocalCandidates(value.X,value.Y,Tile,Ground,Edge));};
            var reference=RescueRuntime.FindLocalCandidates(12,12,Tile,Ground,Edge);
            Check(reference.Count>0,"actual production BFS must produce a nonempty reference field");
            // Exercise all real player owners, especially human1 and AI2. Zero
            // source means all; ownership is equality, never human/alliance policy.
            for(int sourceOwner=0;sourceOwner<=8;sourceOwner++)for(int owner=1;owner<=8;owner++)
            {
                var value=Unit(owner);int before=calls;
                RescueRuntime.CaptureCandidatesForOwner(value,sourceOwner,capture);
                if(sourceOwner==0 || sourceOwner==owner)
                {Check(calls==before+1,"matching/unfiltered owner invokes actual collector exactly once");SameField(value.Candidates,reference,"retained field");}
                else Check(calls==before && value.Candidates.Count==0,"foreign owner never invokes candidate BFS");
                Check(value.Id==1 && value.GlobalId==81 && value.Owner==owner && value.Health==123 && value.Flags==2 &&
                    value.Substep==3 && value.RequestedX==8 && value.RequestedY==8,"gate does not change identity/intent/pose/health");
            }
            var unknown=Unit(0);int prior=calls;
            RescueRuntime.CaptureCandidatesForOwner(unknown,2,capture);Check(calls==prior,"positive source also rejects a zero-owner candidate");
            RescueRuntime.CaptureCandidatesForOwner(unknown,0,capture);Check(calls==prior+1,"unfiltered source remains truly unfiltered");
            SameField(unknown.Candidates,reference,"unfiltered zero owner");

            var snapshot=typeof(RescueRuntime).GetMethod("SnapshotUnit",BindingFlags.Instance|BindingFlags.NonPublic,null,
                new[]{typeof(int),typeof(bool),typeof(bool),typeof(int)},null).GetParameters();
            Check(snapshot[2].IsOptional && Equals(snapshot[2].DefaultValue,true) && snapshot[3].IsOptional &&
                Equals(snapshot[3].DefaultValue,0),"existing default snapshots retain collection and unfiltered ownership");
            var mutation=typeof(RescueRuntime).GetMethod("TryCaptureMutation",BindingFlags.Instance|BindingFlags.NonPublic).GetParameters();
            Check(mutation[3].IsOptional && Equals(mutation[3].DefaultValue,0),"existing mutation callers remain unfiltered by default");
            DiscoveryFailures();EmptyCrew();
            Console.WriteLine("PASS: candidate owner capture gate "+checks+" checks; actual gate/BFS/discovery, owners0..8, order/distances, original failure budgets; no game execution.");
        }
        private static void DiscoveryFailures()
        {
            int center=Tile(12,12),heads=0,reads=0;
            var changed=new[]{new RescueTile(12,12,center)};
            var budget=new RescueDiscoveryBudget(4096,16384,512,9999,1000);
            RescueDiscoveryUnitReader never=(int id,out RescueDiscoveryUnit value)=>{reads++;value=default;return false;};
            Check(RescueDiscovery.TryCollect(changed,Tile,tile=>{heads++;return 0;},never,budget,out var units,out var reason) &&
                units.Count==0 && heads==9 && reads==0,"all nine empty halo heads are still read exactly once; no record read");
            heads=reads=0;
            Check(!RescueDiscovery.TryCollect(changed,Tile,tile=>{heads++;throw new Exception("head");},never,budget,out units,out reason) &&
                reason=="discovery-reader-failed" && units.Count==0 && heads==1 && reads==0,"head exception remains a whole-capture failure");
            Func<int,int> head=tile=>tile==center?1:0;
            RescueDiscoveryUnitReader chain=(int id,out RescueDiscoveryUnit value)=>{
                reads++;value=new RescueDiscoveryUnit(id,(uint)(100+id),center,center,id<3?id+1:0,false);return true;};
            reads=0;
            Check(RescueDiscovery.TryCollect(changed,Tile,head,chain,budget,out units,out reason) && units.Count==3 && reads==6,
                "nonempty chain captures and revalidates every record once per pass");
            reads=0;var twoLinks=new RescueDiscoveryBudget(4096,2,512,9999,1000);
            Check(!RescueDiscovery.TryCollect(changed,Tile,head,chain,twoLinks,out units,out reason) &&
                reason=="occupancy-walk-budget" && units.Count==0 && reads==2,"third link still trips budget before read, never partial success");
            RescueDiscoveryUnitReader zeroGlobal=(int id,out RescueDiscoveryUnit value)=>{
                value=new RescueDiscoveryUnit(id,0,center,center,0,false);return true;};
            Check(!RescueDiscovery.TryCollect(changed,Tile,head,zeroGlobal,budget,out units,out reason) &&
                reason=="invalid-occupant-record" && units.Count==0,"foreign eligibility cannot conceal an invalid discovered lifetime");
            RescueDiscoveryUnitReader cycle=(int id,out RescueDiscoveryUnit value)=>{
                value=new RescueDiscoveryUnit(id,99,center,center,id,false);return true;};
            Check(!RescueDiscovery.TryCollect(changed,Tile,head,cycle,budget,out units,out reason) &&
                reason=="invalid-or-cyclic-occupancy-chain" && units.Count==0,"foreign eligibility cannot conceal a bad chain");
            for(int count=512;count<=513;count++)
            {
                reads=0;
                RescueDiscoveryUnitReader many=(int id,out RescueDiscoveryUnit value)=>{
                    reads++;value=new RescueDiscoveryUnit(id,(uint)(100+id),center,center,id<count?id+1:0,false);return true;};
                bool accepted=RescueDiscovery.TryCollect(changed,Tile,head,many,budget,out units,out reason);
                Check(reads==2*count && (count==512?accepted && units.Count==512:
                    !accepted && units.Count==0 && reason=="occupant-snapshot-budget"),"512 selected-unit limit is applied before any owner gate");
            }
        }
        private static void EmptyCrew()
        {
            // Only the nonsiege branch is executed: it reads the supplied stack
            // unit's type and returns before touching the uninitialized native field.
            var runtime=(RescueRuntime)FormatterServices.GetUninitializedObject(typeof(RescueRuntime));
            var method=typeof(RescueRuntime).GetMethod("CaptureCrew",BindingFlags.Instance|BindingFlags.NonPublic);
            GameUnit unit=default;unit.r_UnitChimp=eChimps.CHIMP_TYPE_SPEARMAN;
            object pointer=Pointer.Box(&unit,typeof(GameUnit*));
            var first=(uint[])method.Invoke(runtime,new[]{pointer});var second=(uint[])method.Invoke(runtime,new[]{pointer});
            Check(first.Length==0 && ReferenceEquals(first,second) && ReferenceEquals(first,Array.Empty<uint>()),
                "actual nonsiege capture shares immutable empty crew representation");
            Check(RescueRuntime.HasCoherentCrewShape(first,0) && !RescueRuntime.HasCoherentCrewShape(first,1),
                "shared empty view preserves exact existing crew shape policy");
        }
    }
}
