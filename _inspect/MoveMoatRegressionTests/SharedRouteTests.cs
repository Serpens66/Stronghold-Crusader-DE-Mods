using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
namespace MoveMoatTest
{
    internal static class SharedRouteTests
    {
        public static void Run()
        {
            const int w=64;
            WeightedMovementCostProfile.TryCreate(1,1,0,0,0,0,false,out var profile,out _);
            var members=new List<GroupRouteSession.Member> {
                new GroupRouteSession.Member {Id=4,Start=26,Player=1,Profile=profile},
                new GroupRouteSession.Member {Id=1,Start=0,Player=1,Profile=profile},
                new GroupRouteSession.Member {Id=3,Start=24,Player=1,Profile=profile},
                new GroupRouteSession.Member {Id=2,Start=2,Player=1,Profile=profile},
                new GroupRouteSession.Member {Id=5,Start=0,Player=2,Profile=profile}
            };
            var clusters=new GroupRouteSession(true,true);clusters.Capture(members,w);
            if(clusters.Units[1].Reference.Id!=2||clusters.Units[2].Reference.Id!=2||clusters.Units[3].Reference.Id!=3||
                clusters.Units[4].Reference.Id!=3||clusters.Units[5].Count!=1)throw new Exception("Deterministic centroid/radius/player partition");

            int checks=0;
            var random=new Random(7741);
            for(int map=0;map<30;map++)
            {
                var cost=new Dictionary<long,long>();
                for(int y=0;y<w;y++) for(int x=0;x<w;x++)
                    for(int dy=-1;dy<=1;dy++) for(int dx=-1;dx<=1;dx++)
                    {
                        if((dx==0&&dy==0)||(uint)(x+dx)>=w||(uint)(y+dy)>=w)continue;
                        int a=x+y*w,b=a+dx+dy*w;
                        cost[((long)a<<32)|(uint)b]=random.Next(5)==0?-1:random.Next(1,9);
                    }
                int[] main=Enumerable.Range(15,34).Select(x=>x+32*w).ToArray();
                for(int i=1;i<main.Length;i++)cost[((long)main[i-1]<<32)|(uint)main[i]]=2;
                long Edge(int a,int b)=>cost.TryGetValue(((long)a<<32)|(uint)b,out long c)?c:-1;
                var field=new SharedRouteField(w,main,Edge);
                long[] Reference(bool reverse)
                {
                    var d=Enumerable.Repeat(long.MaxValue,w*w).ToArray();
                    int center=reverse?main[0]:main[main.Length-1];
                    bool Inside(int n)=>Math.Max(Math.Abs(n%w-center%w),Math.Abs(n/w-center/w))<=12;
                    for(int i=0;i<main.Length;i++)if(Inside(main[i]))d[main[i]]=2*(reverse?main.Length-1-i:i);
                    // Independent Bellman-Ford, no production heap or parent state.
                    bool changed=true;
                    while(changed)
                    {
                        changed=false;
                        foreach(var kv in cost)
                        {
                            int a=(int)(kv.Key>>32),b=(int)kv.Key;
                            if(reverse){int t=a;a=b;b=t;}
                            if(kv.Value<=0||!Inside(a)||!Inside(b)||d[a]==long.MaxValue)continue;
                            long v=d[a]+kv.Value;
                            if(v<d[b]){d[b]=v;changed=true;}
                        }
                    }
                    return d;
                }
                var entry=Reference(true);var exit=Reference(false);
                for(int i=0;i<300;i++)
                {
                    int start=main[0]+random.Next(-12,13)+random.Next(-12,13)*w;
                    int target=main[main.Length-1]+random.Next(-12,13)+random.Next(-12,13)*w;
                    if(!field.TryConnect(start,target,out int[] path))continue;
                    long actual=0;
                    if(path[0]!=start||path[path.Length-1]!=target||path.Distinct().Count()!=path.Length)throw new Exception("Shared endpoints/loop");
                    for(int j=1;j<path.Length;j++){long c=Edge(path[j-1],path[j]);if(c<=0)throw new Exception("Shared directed edge");actual+=c;}
                    if(actual!=entry[start]+exit[target]-2*(main.Length-1))throw new Exception("Shared connector reference costs");
                    checks++;
                }
            }
            foreach(int count in new[]{1,120,680,1000})
            {
                var main=Enumerable.Range(15,34).Select(x=>x+32*w).ToArray();
                long Edge(int a,int b)=>Math.Abs(a%w-b%w)<=1&&Math.Abs(a/w-b/w)<=1?1:-1;
                long allocated=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();
                var field=new SharedRouteField(w,main,Edge);
                for(int i=0;i<count;i++)if(!field.TryConnect(main[0]+i%7,main[main.Length-1]-i%5,out _))throw new Exception("Shared benchmark connection");
                Console.WriteLine($"SHARED CONNECTORS units={count} ms={watch.Elapsed.TotalMilliseconds:F3} nodes={field.Expanded} bytes={GC.GetAllocatedBytesForCurrentThread()-allocated}");
            }
            Console.WriteLine($"PASS: {checks} shared directed connector paths vs independent Bellman-Ford.");
        }
    }
    internal sealed unsafe partial class MoveMoatPathTest
    {
        private void TestSharedRoutePipeline(GameUnit* units)
        {
            bool previousCapture=captureWeighted;captureWeighted=true;
            MoveMoatTestPlugin.Settings.RouteMode=1;
            try
            {
                foreach(int count in new[]{1,120,680,1000})
                {
                    tick++;
                    activeMoveCommand=new MoveCommandScope { TribeId=1,TargetX=18,TargetY=10,
                        ActiveUnitIdsAtDispatch=Enumerable.Range(1,count).ToArray() };
                    var session=activeMoveCommand.Routes;
                    for(int id=1;id<=count;id++)
                    {
                        units[id].r_GlobalId=(uint)(10000+id);
                        units[id].r_CurrentTilePositionX=10;units[id].r_NextTilePositionX2=10;
                    }
                    long allocated=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();
                    for(int id=1;id<=count;id++)
                    {
                        var plan=new PlanScope(id,18,10){PlayerId=1};
                        TryCaptureWeightedMovementCostProfile(units+id,out var profile,out _);
                        bool found=TryBuildSharedGroupRoute(plan,units+id,10,10,18,10,profile,false,null,out var summary,out var route);
                        Check(found==(count>1),"shared production group/singleton");
                        if(found) Check(route.IsValid&&summary.MoatEdges>0,"shared production audited geometry");
                    }
                    Check(session.MainSearches==(count>1?1:0),"one shared main search");
                    if(count>1)
                    {
                        var p=new PlanScope(1,18,10){PlayerId=1};TryCaptureWeightedMovementCostProfile(units+1,out var profile,out _);
                        units[1].r_GlobalId++;
                        Check(!TryBuildSharedGroupRoute(p,units+1,10,10,18,10,profile,false,null,out _,out _),"shared reused ID rejected");
                        units[1].r_GlobalId--;
                        Check(!TryBuildSharedGroupRoute(p,units+1,10,10,18,10,profile,false,new[]{new MoatSearchLimit(1,1,0)},out _,out _),"shared profile bound rejected");
                        units[1].r_CurrentTilePositionX=11;
                        var other=new PlanScope(2,18,10){PlayerId=1};
                        Check(TryBuildSharedGroupRoute(other,units+2,10,10,18,10,profile,false,null,out _,out _),"reference movement preserves shared geometry");
                        units[1].r_CurrentTilePositionX=10;
                        long runs=session.MainSearches;InvalidateMovementSearchData();
                        Check(TryBuildSharedGroupRoute(p,units+1,10,10,18,10,profile,false,null,out _,out _)&&session.MainSearches==runs+1,"shared terrain revision refreshed");
                    }
                    Console.WriteLine($"SHARED PRODUCTION units={count} ms={watch.Elapsed.TotalMilliseconds:F3} main={session.MainSearches} connectorNodes={session.ConnectorNodes} reuse={session.Reused} fallback={session.Fallbacks} bytes={GC.GetAllocatedBytesForCurrentThread()-allocated}");
                    MoveMoatTestPlugin.Settings.EnableMod=false;
                    Check(ExtensionsEnabled,"active command keeps activation snapshot");
                    MoveMoatTestPlugin.Settings.EnableMod=true;
                }
            }
            finally { captureWeighted=previousCapture;activeMoveCommand=null;MoveMoatTestPlugin.Settings.RouteMode=0;MoveMoatTestPlugin.Settings.EnableMod=true; }
        }
    }
}
