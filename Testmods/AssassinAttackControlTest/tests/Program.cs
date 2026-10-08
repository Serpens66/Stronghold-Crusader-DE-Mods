using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using APIShared;
using AssassinAttackControlTest;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
internal static class Program
{
    private static int assertions;
    private static void Check(bool value,string name) { assertions++; if(!value) throw new Exception(name); }
    private sealed class Grid : IAssassinTraversalView
    {
        internal readonly int Size;
        internal readonly Dictionary<long,int> Edges=new Dictionary<long,int>();
        internal bool Current=true,Topology=true;
        internal Grid(int size) { Size=size; }
        public bool IsCurrent=>Current;
        public bool ValidateTopology()=>Current && Topology;
        public bool TryGetEdgeCost(int x,int y,int nx,int ny,out int cost)
        { cost=0; return Current && x>=0 && y>=0 && nx>=0 && ny>=0 && x<Size && nx<Size && y<Size && ny<Size && Edges.TryGetValue(((long)(y*800+x)<<32)|(uint)(ny*800+nx),out cost); }
    }
    private static int Main()
    {
        try { SearchTests(); PolicyTests(); NativeTests(); Console.WriteLine("PASS Assassin attack control: "+assertions+" assertions"); return 0; }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void SearchTests()
    {
        var random=new Random(1949); var search=new AlternativeSearch();
        for(int trial=0;trial<100;trial++)
        {
            var grid=new Grid(8);
            for(int y=0;y<8;y++) for(int x=0;x<8;x++) for(int dy=-1;dy<=1;dy++) for(int dx=-1;dx<=1;dx++)
                if((dx!=0 || dy!=0) && x+dx>=0 && x+dx<8 && y+dy>=0 && y+dy<8 && random.Next(4)!=0)
                    grid.Edges[((long)(y*800+x)<<32)|(uint)((y+dy)*800+x+dx)]=random.Next(1,20);
            var gates=new Dictionary<int,int> { [7]=41,[5607]=42 };
            var inside=new HashSet<int> { 3204 };
            // Independent repeated-relaxation oracle (no heap, no production search).
            var distance=new Dictionary<int,long>(); for(int y=0;y<8;y++) for(int x=0;x<8;x++) distance[y*800+x]=long.MaxValue;
            distance[0]=0;
            for(int pass=0;pass<64;pass++) foreach(var edge in grid.Edges)
            { int from=(int)(edge.Key>>32),to=(int)edge.Key; if(distance[from]!=long.MaxValue && distance[from]+edge.Value<distance[to]) distance[to]=distance[from]+edge.Value; }
            SearchResult result=search.Find(grid,0,gates,inside);
            int expected=distance[7]<=distance[5607] ? 7 : 5607;
            if(distance[expected]==long.MaxValue) expected=distance[3204]!=long.MaxValue ? 3204 : -1;
            Check(expected<0 ? result.Outcome==SearchOutcome.NoAlternative : result.Outcome==SearchOutcome.Alternative && result.Goal==expected,"Independent weighted directed reachability oracle");
            if(result.Outcome==SearchOutcome.Alternative)
            {
                Check(AlternativeSearch.Validate(grid,result.Route),"Witness route validates");
                long cost=0; for(int i=1;i<result.Route.Length;i++) cost+=grid.Edges[((long)result.Route[i-1]<<32)|(uint)result.Route[i]];
                Check(cost==distance[result.Goal],"Exact weighted route cost");
                grid.Current=false; Check(!AlternativeSearch.Validate(grid,result.Route),"Stale policy rejected");
            }
        }
        var line=new Grid(8);
        for(int x=0;x<7;x++) line.Edges[((long)x<<32)|(uint)(x+1)]=1;
        search.Begin(line,0,new Dictionary<int,int> { [7]=1 },new HashSet<int>());
        Check(search.Step(1).Outcome==SearchOutcome.Unknown && search.HasPendingNodes,"Budget exhaustion is not no-route proof");
        SearchResult continued=null;
        for(int i=0;i<8;i++) { continued=search.Step(1); if(continued.Outcome!=SearchOutcome.Unknown) break; }
        Check(continued.Outcome==SearchOutcome.Alternative && continued.Goal==7,"Bounded continuation completes");
        search.Begin(line,0,new Dictionary<int,int> { [7]=1 },new HashSet<int>());
        search.Step(1); line.Topology=false;
        Check(search.Step(1).Outcome==SearchOutcome.Unknown && !search.HasPendingNodes,"Changed topology never proves no route");
        Check(search.Find(null,0,new Dictionary<int,int>(),new HashSet<int>()).Outcome==SearchOutcome.Unknown,"Missing provider/target never proves no route");
        var isolated=new Grid(8);
        Check(search.Find(isolated,0,new Dictionary<int,int> { [7]=1 },new HashSet<int>()).Outcome==SearchOutcome.NoAlternative,"Complete isolated flood permits fallback");
    }
    private sealed class Policy : IEnemyGatePathPolicy
    {
        internal bool Mask;
        public bool HasPublishedMask=>Mask;
        public bool IsDirectionAllowed(int playerId,int tileId,int direction)=>!Mask;
        public int ResolveTribePlayer(int tribeId)=>1;
        public int ResolveBuildingPlayer(int player,int tribeId)=>player;
        public int ResolveCursorPlayer(int tribeId)=>1;
        public object EnterNativeSearch(int player,EnemyGateSearchKind kind)=>null;
        public void ExitNativeSearch(object scope,EnemyGateSearchKind kind,bool completed,bool success) { }
    }
    private sealed class Snapshot : IEnemyGateRoutePolicySnapshot
    {
        internal bool Current=true;
        public int PlayerId=>1;
        public bool IsCurrent=>Current;
        public bool IsDirectionAllowed(int tile,int direction)=>true;
    }
    private static void PolicyTests()
    {
        var provider=new Policy(); var snapshot=new Snapshot();
        Check(BugfixesAndQoL.AssassinGateRoutePolicy.IsPublicationCurrent(null,null,null),"Absent provider stays current");
        Check(BugfixesAndQoL.AssassinGateRoutePolicy.IsPublicationCurrent(provider,null,provider),"Unmasked publication stays current");
        provider.Mask=true;
        Check(!BugfixesAndQoL.AssassinGateRoutePolicy.IsPublicationCurrent(provider,null,provider),"New masks invalidate null snapshot");
        Check(!BugfixesAndQoL.AssassinGateRoutePolicy.IsPublicationCurrent(null,null,provider),"New provider invalidates absent capture");
        Check(BugfixesAndQoL.AssassinGateRoutePolicy.IsPublicationCurrent(provider,snapshot,provider),"Live masked publication accepted");
        provider.Mask=false;
        Check(!BugfixesAndQoL.AssassinGateRoutePolicy.IsPublicationCurrent(provider,snapshot,provider),"Cleared mask invalidates old capture");
        provider.Mask=true; snapshot.Current=false;
        Check(!BugfixesAndQoL.AssassinGateRoutePolicy.IsPublicationCurrent(provider,snapshot,provider),"Replaced snapshot invalidated");
        Check(!BugfixesAndQoL.AssassinGateRoutePolicy.IsPublicationCurrent(provider,null,new Policy()),"Provider identity change invalidated");
    }
    [DllImport("kernel32.dll")] private static extern IntPtr VirtualAlloc(IntPtr address,UIntPtr size,uint kind,uint protect);
    [DllImport("kernel32.dll")] private static extern bool VirtualFree(IntPtr address,UIntPtr size,uint kind);
    private static void NativeTests()
    {
        IntPtr code=VirtualAlloc(IntPtr.Zero,(UIntPtr)4096,0x3000,0x40);
        NativeDetour<AssassinAttackNativeContract.UpdateDelegate> candidate=null;
        AssassinAttackNativeContract.UpdateDelegate original=null;
        int calls=0;
        AssassinAttackNativeContract.UpdateDelegate callback=()=> { calls++; original(); };
        try
        {
            Check(code!=IntPtr.Zero,"Native allocation");
            byte[] bytes={0x48,0x89,0x5C,0x24,0x08,0x48,0x89,0x6C,0x24,0x10,0xC3};
            Marshal.Copy(bytes,0,code,bytes.Length);
            var request=new DetourRequest<AssassinAttackNativeContract.UpdateDelegate> { Name="Assassin update exact backend fixture",TargetAddress=(ulong)code.ToInt64(),Callback=callback };
            candidate=AssassinAttackNativeContract.Backend.CreateDetour(in request) as NativeDetour<AssassinAttackNativeContract.UpdateDelegate>;
            Check(candidate!=null && candidate.Scheme==DetourScheme.Indirect && candidate.DisplacedByteCount==10,"Production backend exact displacement");
            original=candidate.Original; candidate.Enable();
            AssassinAttackNativeContract.Validate(candidate,code); assertions++;
            Marshal.GetDelegateForFunctionPointer<AssassinAttackNativeContract.UpdateDelegate>(code)();
            Check(calls==1,"No-argument native callback/original ABI");
            original(); Check(calls==1,"Original trampoline does not recurse");
        }
        finally { candidate?.Dispose(); VirtualFree(code,UIntPtr.Zero,0x8000); GC.KeepAlive(callback); }
    }
}
