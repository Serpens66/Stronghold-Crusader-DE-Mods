using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace EnemyBridgePathTest
{
    internal static unsafe class RouteTraceTests
    {
        private static int checks;
        private static void Check(bool value,string reason) {checks++;if(!value)throw new Exception(reason);}
        internal static int Run()
        {
            var output=new List<string>();long captures=0;
            var bridge=new BridgeRouteTrace.Bridge {Id=703,Global=2432893,Parent=600,ParentGlobal=2432853,ParentLink="unique-footprint-adjacency-candidate"};
            for(int x=601;x<=603;x++)bridge.Deck.Add(BridgeRouteTrace.Bridge.XY(x,474));
            var trace=new BridgeRouteTrace((kind,detail)=>output.Add(kind+","+detail),()=>new[]{bridge},()=>++captures);
            trace.Begin(1);
            var track=new BridgeRouteTrace.Track {Unit=1145,Global=2416993,Player=8,Tribe=4364,Command=1320346,ParentEvent=1287021,PlanningRoot=1320345,CandidatePlan=1287038,Phase=6,Session=1};
            var h=new BridgeRouteTrace.Header(track.Global,600,474,600,474,4,0);
            byte[] bytes={0x22,0x22};
            trace.ObservePlan(track,h,bytes,0);
            Check(output.Single().Contains("703/2432893/0/2/600/474/604/474/deck-without-parent-footprint"),"sideways route intersects exact deck even when endpoints are outside");
            Check(output[0].Contains("packedHex=2222")&&output[0].Contains("planningRoot=1320345"),"stored route reconstructible and bound to actual root");
            for(int i=0;i<10000;i++)trace.ObservePlan(track,h,bytes,0);
            Check(output.Count==1&&captures==1,"unchanged full paths do not capture or format again");
            trace.ObserveMovement(track,h,false,0);
            trace.ObserveMovement(track,new BridgeRouteTrace.Header(track.Global,601,474,600,474,4,1),true,1);
            trace.ObserveMovement(track,new BridgeRouteTrace.Header(track.Global,604,474,600,474,4,4),true,2);
            Check(output.Count(x=>x.StartsWith("bridge-movement"))==2&&output.Any(x=>x.Contains("stage=enter"))&&output.Any(x=>x.Contains("stage=exit")),"tile transitions separately establish deck entry/exit");
            trace.ObserveMovement(track,new BridgeRouteTrace.Header(track.Global,604,474,600,474,4,4),true,6*Stopwatch.Frequency);
            int gaps=output.Count(x=>x.StartsWith("route-progress-gap"));
            trace.ObserveMovement(track,new BridgeRouteTrace.Header(track.Global,604,474,600,474,4,4),true,12*Stopwatch.Frequency);
            Check(gaps==1&&output.Count(x=>x.StartsWith("route-progress-gap"))==1,"progress gap emitted once until movement resumes, cause unproven");
            bridge.Gate.Add(BridgeRouteTrace.Bridge.XY(602,474));track.HasPlan=false;trace.ObservePlan(track,h,bytes,0);
            Check(output.Any(x=>x.Contains("gate-footprint-observed")),"gate passage differs from sideways deck intersection");
            trace.ObservePlan(track,h,new byte[]{0x44,0x44},0);
            Check(output.Last().Contains("bridges=[]"),"same-header replacement reads actual changed bytes and alternative route remains allowed");
            track.HasPlan=false;trace.ObservePlan(track,h,new byte[]{0x2F,0x22},0);
            Check(output.Any(x=>x.Contains("complete=False"))&&output.Last().Contains("invalid-direction-or-coordinate"),"unknown native nibble explicitly unresolved, never guessed as movement");
            track.HasPlan=false;trace.ObservePlan(track,new BridgeRouteTrace.Header(track.Global,1,1,1,1,2001,0),new byte[1000],0);
            Check(output.Last().Contains("invalid-length-cursor-or-origin"),"physical buffer limit enforced without out-of-range read");
            // Odd unused high nibble is inactive; it must not create a changed plan.
            var odd=new BridgeRouteTrace.Track {Global=1};var one=new BridgeRouteTrace.Header(1,1,1,1,1,1,0);
            trace.ObservePlan(odd,one,new byte[]{0xF2},0);long before=captures;
            trace.ObservePlan(odd,one,new byte[]{0xA2},0);Check(captures==before,"inactive high nibble ignored");
            for(int raw=0;raw<8;raw++) {int x=10,y=10;Check(BridgeRouteTrace.Step(raw,ref x,ref y)&&Math.Abs(x-10)<=1&&Math.Abs(y-10)<=1,"audited compass direction");}
            int bx=0,by=0;Check(!BridgeRouteTrace.Step(7,ref bx,ref by),"coordinate bounds fail closed");
            ProductionView(output,bridge);
            Completion();
            CompactReferences();
            LatestPopulation();
            Console.WriteLine("PASS route observation: stored plans, deck/gate distinction, actual transitions, replacement/reuse and bounded completion.");
            return checks;
        }
        private static void ProductionView(List<string> output,BridgeRouteTrace.Bridge bridge)
        {
            IntPtr unitMemory=Marshal.AllocHGlobal(sizeof(GameUnit)),pathMemory=Marshal.AllocHGlobal(1000);
            try
            {
                GameUnit* unit=(GameUnit*)unitMemory;*unit=default;
                unit->r_GlobalId=99;unit->r_CurrentTilePositionX=600;unit->r_CurrentTilePositionY=474;
                unit->r_TargetTilePositionX=604;unit->r_TargetTilePositionY=474;
                unit->r_PreviousTilePositionX=600;unit->r_PreviousTilePositionY=474;unit->r_PathPlanLength=4;unit->r_PathPlanStateBitFlags=2;
                for(int i=0;i<1000;i++)((byte*)pathMemory)[i]=0;((byte*)pathMemory)[0]=0x22;((byte*)pathMemory)[1]=0x22;
                var ctor=typeof(GameUnitPathPlanView).GetConstructors(BindingFlags.NonPublic|BindingFlags.Instance).Single();
                var parameters=ctor.GetParameters();
                var view=(GameUnitPathPlanView)ctor.Invoke(new object[]{Pointer.Box(unit,parameters[0].ParameterType),Pointer.Box((byte*)pathMemory,parameters[1].ParameterType)});
                int lookups=0;bool fail=false;
                var trace=new BridgeRouteTrace((kind,detail)=>output.Add(kind+","+detail),()=>new[]{bridge},()=>1,id=> {lookups++;if(fail&&id==1)throw new InvalidOperationException("fixture");return unitMemory;},id=>view);
                trace.Begin(1);trace.Bind(0,99,8,4,1,0,2,3,4,6);trace.Observe(0,true);Check(lookups==0,"ID zero never looked up");
                for(int id=1;id<=38;id++) {trace.Bind(id,99,8,4364,100+id,20,30,40,50,6);trace.Observe(id,true,true);}
                Check(lookups==76&&trace.Summary().Contains("tracked=38"),"38 following-command chains use actual public packed views and production header getter");
                Check(output.Any(x=>x.Contains("pathChangedSinceCommandPre=False")),"unchanged old route is distinguished from a newly built path");
                trace.Bind(1,99,8,4364,500,20,30,40,50,6);trace.Observe(1,true,true,1,499);
                Check(output.Last().Contains("intervening-command"),"outer return cannot overwrite a newer nested command");
                ((byte*)pathMemory)[0]=0x44;trace.Observe(1,true,true,1,500);
                Check(output.Any(x=>x.Contains("commandOp=500")&&x.Contains("pathChangedSinceCommandPre=True")),"same-header command replacement compared to actual pre bytes");
                ((byte*)pathMemory)[0]=0x22;
                byte[] retained=new byte[1000];Marshal.Copy(pathMemory,retained,0,1000);
                unit->r_CurrentTilePositionX=601;unit->r_CurrentPathPlanIndex=1;trace.Observe(1,true);
                Check(unit->r_CurrentTilePositionX==601&&view.PackedBytes.SequenceEqual(retained),"observation preserves all native path bytes and counters");
                fail=true;trace.Observe(1,true);trace.Observe(2,true);Check(output.Any(x=>x.StartsWith("route-capture-error"))&&trace.IsTracked(2),"one unit's failure does not disable remaining units");
                fail=false;unit->r_GlobalId=100;trace.Observe(1,true);Check(!trace.IsTracked(1)&&output.Last().Contains("missing-or-reused-unit"),"reused slot cannot use retained pointer view");
                trace.End();trace.Begin(2);Check(!trace.IsTracked(2),"map reload clears old identities and plans");
            }
            finally {Marshal.FreeHGlobal(pathMemory);Marshal.FreeHGlobal(unitMemory);}
        }
        private static void CompactReferences()
        {
            var trace=new BridgeDecisionTrace(null,_=>default,()=>1,r=>0);trace.StartSession(60);
            int start=Shared.DebugLogHelper.Recent.Count;
            var lower=BridgeNativeDefinition.Sites.Single(x=>x.Rva==0x64460);
            for(int i=0;i<10;i++) {var call=trace.Enter(lower,IntPtr.Zero,703);trace.Exit(call,true,null,IntPtr.Zero);}
            while(trace.Pending>0)trace.Drain();
            var lines=Shared.DebugLogHelper.Recent.Skip(start).ToArray();
            Check(lines.Count(x=>x.Contains("kind=text-definition,"))==2&&lines.Count(x=>x.Contains("kind=native-enter,"))==10&&lines.Where(x=>x.Contains("kind=native-enter,")).All(x=>x.Contains("contextDefinition=")),"shared contexts preserve every changed operation using two definitions");
            var first=new BridgeDecisionTrace.TableImage {Rva=0x2EB6B64,Slot=8,Count=1,Available=1,Rows=new[]{214724,214725,40,0}};
            var second=new BridgeDecisionTrace.TableImage {Rva=0x2EB6B64,Slot=7,Count=1,Available=1,Rows=new[]{214724,214725,40,0}};
            trace.PublishTable(first,"pre");trace.PublishTable(second,"pre");
            Check(first.Definition==second.Definition&&first.Rows[0]==214724&&second.Slot==7,"equal candidate content reuses definition without altering slot or data");
            trace.FlushRegions();while(trace.Pending>0)trace.Drain();
            Check(Shared.DebugLogHelper.Recent.Any(x=>x.Contains("candidate-table-reference-batch")&&x.Contains("/7/"+first.Definition+"/")),"reused candidate table retains actual slot reference");
        }
        private static void LatestPopulation()
        {
            var trace=new BridgeDecisionTrace(null,_=>new BridgeDecisionTrace.AttackStamp(1,8,1,6,0,1,1),()=>1,r=>r==0x60AD6CC?1:r==0x37ED4CC?2:0);
            trace.StartSession(90);long bytes=Shared.DebugLogHelper.Bytes;
            var attack=BridgeNativeDefinition.Sites.Single(x=>x.Rva==0x11A980);
            var topology=BridgeNativeDefinition.Sites.Single(x=>x.Rva==0xE49D0);
            for(int i=0;i<1571980;i++)
            {
                var call=trace.Enter(attack,IntPtr.Zero,4364);trace.Exit(call,true,null,IntPtr.Zero);
                if(i<9806) {bool rebuild=i<88;call=trace.Enter(topology,IntPtr.Zero,rebuild?1:0);trace.Exit(call,true,rebuild?1:0,IntPtr.Zero);}
                if(i<32784) {trace.CountCommand(true);trace.CountCommand(false);trace.CountEvent(BridgeDecisionTrace.CommandCountData(3,i%8+1,3,1,false,true));}
                if(i%1000==0)trace.Drain();
            }
            trace.FlushRegions();while(trace.Pending>0)trace.Drain();
            Check(trace.Captures==178&&trace.Entered==1581786,"latest ordinary/topology population:88 actual rebuilds, bounded full reads");
            Check(Shared.DebugLogHelper.Bytes-bytes<100000,"unchanged latest hot population fits1MB/min including95-byte prefix allowance");
            int[,] rare={{0x64460,3},{0x645C0,4},{0xD95E0,23},{0xD9190,23},{0x10DF60,16},{0x115B10,16},{0x122B40,12},{0xE7F60,4},{0x110EC0,28},{0x111330,41},{0x111D90,31},{0x3C2E0,343},{0x2D250,23},{0x2C480,16},{0x2C5A0,16},{0x3BD50,7},{0xCF360,1167},{0xCF400,28}};
            for(int row=0;row<rare.GetLength(0);row++)for(int i=0;i<rare[row,1];i++)
            {var site=BridgeNativeDefinition.Sites.Single(x=>x.Rva==rare[row,0]);var call=trace.Enter(site,IntPtr.Zero,8,1);trace.Exit(call,true,1,IntPtr.Zero);if(i%32==0)trace.Drain();}
            trace.FlushRegions();while(trace.Pending>0)trace.Drain();
            Check(trace.Entered==1583587&&trace.Entered==trace.Exited,"latest full1583587-call population exact and balanced");
            Check(trace.Summary().Contains("commandPre=32784,commandPost=32784")&&trace.Failures==0&&trace.Summary().Contains("overflow=0,backgroundOverflow=0"),"latest command counters and capture completeness");
            Console.WriteLine("Latest full population: calls="+trace.Entered+",commands=32784,bytesWithPrefixAllowance="+(Shared.DebugLogHelper.Bytes-bytes)+" (fixture reads; changed bursts separate)");
        }
        private static void Completion()
        {
            var trace=new BridgeDecisionTrace(null,_=>default,()=>1);trace.StartSession(55);
            for(int i=0;i<130;i++)trace.Observe("fixture","i="+i);
            trace.End();Check(trace.Pending>64,"end does not synchronously empty queue");
            int frames=0;
            do {long count=trace.OutputRecords;trace.Drain();Check(trace.OutputRecords-count<=64,"render budget includes delivery marker");}while(trace.Pending>0&&++frames<1000);
            trace.Drain();
            Check(Shared.DebugLogHelper.Recent.Any(x=>x.Contains("session=55,")&&x.Contains("kind=session-delivered,")&&x.Contains("deliveryComplete=True")),"delivery marker follows all queued definitions and decisions");
        }
    }
}
