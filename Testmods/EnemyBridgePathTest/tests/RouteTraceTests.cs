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
            Check(output[0].Contains("packedHex=2222")&&track.PlanningRoot==1320345,"stored route reconstructible; binding retains actual root separately");
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
            trace.Flush();Check(output.Any(x=>x.StartsWith("stored-route-background-batch,")&&x.Contains("bridges=[]")),"same-header replacement reads actual changed bytes and alternative route remains allowed");
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
            DecisionPersistence();
            CommandAgeAndCursor();
            PromotedCommandFrames();
            ObservedPopulation();
            Completion();
            CompactReferences();
            BackgroundTransport();
            LatestPopulation();
            Console.WriteLine("PASS route observation: stored plans, deck/gate distinction, actual transitions, replacement/reuse and bounded completion.");
            return checks;
        }
        private static IEnumerable<string[]> Rows(List<string> lines,string kind) => lines.Where(x=>x.StartsWith(kind+",")).SelectMany(x=>x.Substring(x.IndexOf("rows=[",StringComparison.Ordinal)+6).Split(']')[0].Split(new[]{';'},StringSplitOptions.RemoveEmptyEntries)).Select(row=>row.Split('/'));
        private static void BackgroundTransport()
        {
            var output=new List<string>();var trace=new BridgeRouteTrace((k,d)=>output.Add(k+","+d),()=>Array.Empty<BridgeRouteTrace.Bridge>(),()=>throw new Exception("background full capture"));trace.Begin(901);
            for(int unit=1;unit<=75;unit++)
            {
                var track=new BridgeRouteTrace.Track {Unit=unit,Global=(uint)unit,Player=8,Command=unit,Session=901};
                var header=new BridgeRouteTrace.Header((uint)unit,unit,1,unit,1,1,0);
                trace.ObservePlan(track,header,new byte[]{2},0);
                for(int repeat=0;repeat<1000;repeat++)trace.ObservePlan(track,header,new byte[]{2},repeat);
                // Simulate the same replacement/flush boundary as the production binding.
                typeof(BridgeRouteTrace).GetMethod("FlushRepeats",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(trace,new object[]{track});
            }
            trace.Flush();
            Check(Rows(output,"stored-route-background-batch").Count()==75,"all background path definitions retained numerically");
            Check(Rows(output,"route-background-repeat-batch").Sum(row=>long.Parse(row[1]))==75000,"background repeated observations counted exactly");
            Check(!output.Any(x=>x.StartsWith("route-repeat-batch,")),"unchanged no-deck repeats do not produce per-binding definitions");
            Check(output.Sum(line=>line.Length+95+2)<40000,"background path and repeat transport fits1MB/min with prefixes");
            trace.End();trace.Begin(902);trace.Flush();
            Check(Rows(output,"stored-route-background-batch").Count()==75,"reload does not reemit prior numeric batches");
        }
        private static void DecisionPersistence()
        {
            int phase=5,target=232127;
            var trace=new BridgeDecisionTrace(null,_=>default,()=>1,r=>
                r==0x379D974+8*0x583C?phase:r==0x379D9A8+8*0x583C?1:r==0x379D968+8*0x583C?target:
                r==0x12EC54+8*0x583C||r==0x12EC54+0x583C?1:
                r==0x12EDA0+8*0x583C?10:r==0x12EDA0+0x583C?11:
                r==0x50EC690+20?102:r==0x50EC690+22?1:0);
            trace.SetNative(IntPtr.Zero,int.MaxValue);trace.StartSession(777);
            var root=BridgeNativeDefinition.Sites.Single(x=>x.Rva==0x3C2E0);var cf=BridgeNativeDefinition.Sites.Single(x=>x.Rva==0xCF360);
            var call=trace.Enter(root,IntPtr.Zero,8);var access=trace.Enter(cf,IntPtr.Zero,8,1);trace.Region(8,1,102,0,102,102);trace.Exit(access,true,1,IntPtr.Zero);phase=6;trace.Exit(call,true,null,IntPtr.Zero);
            var plan=new BridgeDecisionTrace.Arguments(0,6,1,target,0,0);long definition=trace.DecisionFor(8,plan);
            Check(definition>0,"consumed5->6 access state retained across calls");
            call=trace.Enter(root,IntPtr.Zero,8);Check(trace.DecisionFor(8,call.PrePlan)==definition,"later quiet phase6 references completed decision");trace.Exit(call,true,null,IntPtr.Zero);
            target++;Check(trace.DecisionFor(8,new BridgeDecisionTrace.Arguments(0,6,1,target,0,0))==0,"target change ends previous permission provenance");
            trace.StartSession(778);Check(trace.DecisionFor(8,plan)==0,"reload cannot inherit earlier decision");
            trace.FlushRegions();while(trace.Pending>0)trace.Drain();
            Check(Shared.DebugLogHelper.Recent.Any(x=>x.Contains("kind=decision-state,")&&x.Contains("/102/102/1;")&&x.Contains("consumedPlan=[0/6/1/232127/0/0]")),"native/effective result and consumed phase are retained separately");
        }
        private static bool CommandPayload(long op,params string[] terms)
        {
            var lines=Shared.DebugLogHelper.Recent;
            foreach(var line in lines.Where(v=>v.Contains("kind=command-frame-batch,")))
            foreach(var row in line.Split(new[]{"rows=["},StringSplitOptions.None)[1].Split(']')[0].Split(';'))
            {var v=row.Split('/');if(v.Length!=12||v[8]!=op.ToString())continue;string definition=v[11];if(lines.Any(s=>s.Contains("kind=text-definition,")&&s.Contains("definition="+definition+",")&&s.Contains("category=command-payload,")&&terms.All(s.Contains)))return true;}
            return false;
        }
        private static void PromotedCommandFrames()
        {
            var trace=new BridgeDecisionTrace(null,_=>default,()=>1);trace.StartSession(779);
            var rootSite=BridgeNativeDefinition.Sites.Single(s=>s.Rva==0x3C2E0);var initial=trace.Enter(rootSite,IntPtr.Zero,8);trace.Exit(initial,true,null,IntPtr.Zero);
            var root=trace.Enter(rootSite,IntPtr.Zero,8);Check(!root.Detailed,"unchanged military root begins quiet");
            var diagnostics=new BridgeDiagnostics(null);var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            typeof(BridgeDiagnostics).GetField("Trace",flags).SetValue(diagnostics,trace);typeof(BridgeDiagnostics).GetField("running",flags).SetValue(diagnostics,true);typeof(BridgeDiagnostics).GetField("epoch",flags).SetValue(diagnostics,779L);
            var ft=typeof(BridgeDiagnostics).GetNestedType("Frame",BindingFlags.NonPublic);var list=(System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ft));
            object Frame(string kind,int id,long op,long parent)
            {
                object f=Activator.CreateInstance(ft,true);ft.GetField("Kind",flags).SetValue(f,kind);ft.GetField("Id",flags).SetValue(f,id);ft.GetField("TraceId",flags).SetValue(f,op);ft.GetField("ParentTrace",flags).SetValue(f,parent);ft.GetField("Epoch",flags).SetValue(f,779L);ft.GetField("Player",flags).SetValue(f,8);ft.GetField("Global",flags).SetValue(f,99u);ft.GetField("SourcePcl",flags).SetValue(f,1);ft.GetField("TargetPcl",flags).SetValue(f,1);list.Add(f);return f;
            }
            object group=Frame("move",4364,210,0),unit=Frame("unit",1143,211,210);
            typeof(BridgeDiagnostics).GetField("frames",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,list);
            typeof(BridgeDiagnostics).GetMethod("PromoteRouteFrames",flags).Invoke(diagnostics,new object[]{211L});
            Check((bool)ft.GetField("Detailed",flags).GetValue(group)&&(bool)ft.GetField("Detailed",flags).GetValue(unit)&&root.Detailed,"bridge route promotes frozen unit and group frames plus quiet native parent");
            var pop=typeof(BridgeDiagnostics).GetMethod("Pop",flags);pop.Invoke(diagnostics,new object[]{"unit",1143,1L});pop.Invoke(diagnostics,new object[]{"move",4364,1L});trace.Exit(root,true,null,IntPtr.Zero);
            trace.FlushRegions();while(trace.Pending>0)trace.Drain();
            Check(CommandPayload(210,"promotion=bridge-route-observed","entryData=retained-pre-fields"),"retained input is explicit, no fabricated entry snapshot");
            Check(CommandPayload(210,"stage=same-pcl-with-no-region-call"),"promoted group return distinguishes equality from a positive query");
        }
        private static void CommandAgeAndCursor()
        {
            var lines=new List<string>();var bridge=new BridgeRouteTrace.Bridge {Id=703,Global=1};bridge.Deck.Add(BridgeRouteTrace.Bridge.XY(2,1));
            var trace=new BridgeRouteTrace((kind,text)=>lines.Add(kind+","+text),()=>new[]{bridge},()=>1);trace.Begin(1);
            var t=new BridgeRouteTrace.Track {Unit=1,Global=1,Command=5,LastProgress=0,CommandStarted=10*Stopwatch.Frequency};
            var h=new BridgeRouteTrace.Header(1,1,1,1,1,3,0);trace.ObservePlan(t,h,new byte[]{0x22,0x02},0);trace.ObserveMovement(t,h,true,10*Stopwatch.Frequency);
            trace.ObserveMovement(t,h,true,14*Stopwatch.Frequency);Check(!lines.Any(x=>x.StartsWith("route-progress-gap")),"old global stillness cannot warn before new command's five seconds");
            trace.ObserveMovement(t,h,true,15*Stopwatch.Frequency);Check(lines.Any(x=>x.StartsWith("route-progress-gap")&&x.Contains("commandAgeSeconds=5.000")),"command age and no-position-change duration are separate");
            trace.ObserveMovement(t,new BridgeRouteTrace.Header(1,1,1,1,1,3,3),true,16*Stopwatch.Frequency);trace.Flush();
            Check(Rows(lines,"route-observations").Any(row=>row[12]=="4"),"cursor passed deck is explicit, not future crossing or executed movement");
            Check(!lines.Any(x=>x.StartsWith("bridge-movement")),"cursor alone cannot prove movement");
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
                trace.Flush();Check(Rows(output,"route-observations").Any(row=>row[10]=="0"),"unchanged old route is distinguished numerically from a newly built path");
                trace.Bind(1,99,8,4364,500,20,30,40,50,6);trace.Observe(1,true,true,1,499);
                Check(output.Last().Contains("intervening-command"),"outer return cannot overwrite a newer nested command");
                ((byte*)pathMemory)[0]=0x44;trace.Observe(1,true,true,1,500);
                trace.Flush();Check(Rows(output,"route-observations").Any(row=>row[10]=="1"),"same-header command replacement compared to actual pre bytes");
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
            Check(lines.Count(x=>x.Contains("kind=text-definition,"))==2&&lines.Where(x=>x.Contains("kind=native-frame-batch,")).Sum(x=>x.Split(';').Length-1)==20&&lines.Where(x=>x.Contains("kind=native-frame-batch,")).All(x=>x.Contains("contextDefinition/values26")),"fixed native frames preserve every changed operation using two definitions");
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
            for(int i=0;i<1683174;i++)
            {
                var call=trace.Enter(attack,IntPtr.Zero,4364);trace.Exit(call,true,null,IntPtr.Zero);
                if(i<10653) {bool rebuild=i<93;call=trace.Enter(topology,IntPtr.Zero,rebuild?1:0);trace.Exit(call,true,rebuild?1:0,IntPtr.Zero);}
                if(i<34661) {trace.CountCommand(true);trace.CountCommand(false);trace.CountEvent(BridgeDecisionTrace.CommandCountData(3,i%8+1,3,1,false,true));}
                if(i%1000==0)trace.Drain();
            }
            trace.FlushRegions();while(trace.Pending>0)trace.Drain();
            Check(trace.Captures==188&&trace.Entered==1693827,"latest ordinary/topology population:93 actual rebuilds, bounded full reads");
            Check(Shared.DebugLogHelper.Bytes-bytes<100000,"unchanged latest hot population fits1MB/min including95-byte prefix allowance");
            int[,] rare={{0x64460,2},{0x645C0,3},{0xD95E0,28},{0xD9190,28},{0x10DF60,21},{0x115B10,21},{0x122B40,18},{0xE7F60,4},{0x110EC0,26},{0x111330,29},{0x111D90,29},{0x3C2E0,371},{0x2D250,28},{0x2C480,21},{0x2C5A0,21},{0x3BD50,7},{0xCF360,1248},{0xCF400,34}};
            for(int row=0;row<rare.GetLength(0);row++)for(int i=0;i<rare[row,1];i++)
            {var site=BridgeNativeDefinition.Sites.Single(x=>x.Rva==rare[row,0]);var call=trace.Enter(site,IntPtr.Zero,8,1);trace.Exit(call,true,1,IntPtr.Zero);if(i%32==0)trace.Drain();}
            trace.FlushRegions();while(trace.Pending>0)trace.Drain();
            Check(trace.Entered==1695766&&trace.Entered==trace.Exited,"latest full1695766-call population exact and balanced");
            Check(trace.Summary().Contains("commandPre=34661,commandPost=34661")&&trace.Failures==0&&trace.Summary().Contains("overflow=0,backgroundOverflow=0"),"latest command counters and capture completeness");
            Console.WriteLine("Latest full population: calls="+trace.Entered+",commands=34661,bytesWithPrefixAllowance="+(Shared.DebugLogHelper.Bytes-bytes)+" (fixture reads; changed bursts separate)");
        }
        private static void ObservedPopulation()
        {
            // Frozen relevant routes from complete Log_243, not the mutable live LogOutput.
            string[] paths={
                "888|2416251|5|4383|198124|198123|198122|198122|0|6|566|465|137|0|588|384|588|384|333333333333333333232322222222222222010000000011000000000000000000000000000000000000000000000000000000000000007077770700007777777777770001",
                "1152|2417022|5|4375|198128|198127|198122|198122|0|6|554|453|149|0|588|384|588|384|333333333333333333333333333333232322222222222222010000000011000000000000000000000000000000000000000000000000000000000000007077770700007777777777770001",
                "1152|2417022|5|4375|198128|198127|198122|198122|0|6|554|453|149|14|588|384|588|384|333333333333333333333333333333232322222222222222010000000011000000000000000000000000000000000000000000000000000000000000007077770700007777777777770001",
                "888|2416251|5|4383|198124|198123|198122|198122|0|6|566|465|137|15|588|384|588|384|333333333333333333232322222222222222010000000011000000000000000000000000000000000000000000000000000000000000007077770700007777777777770001",
                "1265|2417364|8|4324|1294023|0|0|0|1267901|-1|538|421|151|0|547|414|547|414|45444444444444343333333333333333333333333333333333333333333333232322222222222222010000007777000000707777777777777777777777777777777777777777777777777707",
                "1018|2429022|8|4324|1309647|0|0|0|1267901|-1|539|424|150|0|546|412|546|412|444444444444443333333333333333333333333333333333333333333333322222222222222212000000707707000000707777777777777777777777777777777777777777777777777777",
                "1295|2417485|8|4324|1312444|0|0|0|1267901|-1|541|429|140|0|549|417|549|417|44444444443433333333333333333333333333333333333333333323232222222222222201000000777700000077777777777777777777777777777777777777777777777777",
                "1378|2417806|8|4324|1314419|0|0|0|1267901|-1|537|423|149|0|547|415|547|415|444444444444343333333333333333333333333333333333333333333333232322222222222222010000007777000000707777777777777777777777777777777777777777777777777706",
                "1369|2417771|8|4324|1318221|0|0|0|1267901|-1|534|417|158|0|545|411|545|411|44444444444444443333333333333333333333333333333333333333333333333323232222222222222201000000777700000000777777777777777777777777777777777777777777777777777777",
                "1256|2417334|8|4324|1320192|0|0|0|1267901|-1|537|420|155|0|545|411|545|411|444444444444444433333333333333333333333333333333333333333333333332222222222222221200000070770700000070777777777777777777777777777777777777777777777777777707",
                "1356|2417704|8|4324|1320860|0|0|0|1267901|-1|536|419|156|0|545|411|545|411|444444444444444433333333333333333333333333333333333333333333333323232222222222222201000000777700000000777777777777777777777777777777777777777777777777777777",
                "701|2415538|8|4324|1326599|0|0|0|1267901|-1|539|424|150|0|546|412|546|412|444444444444443333333333333333333333333333333333333333333333322222222222222212000000707707000000707777777777777777777777777777777777777777777777777777",
                "1422|2418046|8|4324|1329898|0|0|0|1267901|-1|534|416|162|0|543|408|543|408|444444444444444434333333333333333333333333333333333333333333333333333222222222222222120000007077070000000077777777777777777777777777777777777777777777777777777777",
                "1326|2417611|8|4324|1330064|0|0|0|1267901|-1|536|418|159|0|544|409|544|409|4444444444444444343333333333333333333333333333333333333333333333333222222222222222120000007077070000000077777777777777777777777777777777777777777777777777777707",
                "488|2432425|7|4389|1333726|1333725|1333724|1333724|1300752|6|473|364|256|0|588|384|588|384|4344444444444444444444444434333333333333333333333333333333333333332222222222222222223333333333333333333333333333333333333333333333333333333222222222222222120000000010010000000000000000000000000000000000000000000000000000000000000077777700007077777777770710",
                "560|2424041|7|4389|1333731|1333725|1333724|1333724|1300752|6|474|363|257|0|587|384|587|384|444444444444444444444444444433333333333333333333333333333333333333232222222222222222323333333333333333333333333333333333333333333333333333232322222222222222010000000011000000000000000000000000000000000000000000000000000000000000007077770700007777777777770000",
                "1118|2416891|7|4389|1333734|1333725|1333724|1333724|1300752|6|474|365|255|0|589|384|589|384|4444444444444444444444444433333333333333333333333333333333333333232222222222222222323333333333333333333333333333333333333333333333333333232322222222222222010000000011000000000000000000000000000000000000000000000000000000000000007077770700007777777777771001",
                "1427|2426946|7|4389|1333737|1333725|1333724|1333724|1300752|6|471|364|258|0|588|383|588|383|564444444444444444444434333333333333333333333333333333333333333333232222222222222222323333333333333333333333333333333333333333333333333333232322222222222222010000000011000000000000000000000000000000000000000000000000000000000000007077770700007777777777770010",
                "1476|2418205|7|4389|1333740|1333725|1333724|1333724|1300752|6|475|364|257|0|587|383|587|383|454444444444444444444444443433333333333333333333333333333333333333222222222222222222333333333333333333333333333333333333333333333333333333322222222222222212000000001001000000000000000000000000000000000000000000000000000000000000007777770000707777777777070000",
                "1605|2419374|7|4389|1333743|1333725|1333724|1333724|1300752|6|475|362|259|0|589|383|589|383|44454444444444444444444444443433333333333333333333333333333333333333222222222222222222333333333333333333333333333333333333333333333333333333322222222222222212000000001001000000000000000000000000000000000000000000000000000000000000007777770000707777777777071001",
                "1619|2419462|7|4389|1333746|1333725|1333724|1333724|1300752|6|474|368|251|0|588|385|588|385|444444444444444444444434333333333333333333333333333333333333332222222222222222223333333333333333333333333333333333333333333333333333333222222222222222120000000010010000000000000000000000000000000000000000000000000000000000000077777700007077777777770701",
                "246|2414271|7|4357|1333750|1333749|1333724|1333724|1300752|6|474|351|269|0|588|384|588|384|444444444444444444444444444444444444444433333333333333333333333333333333333333232222222222222222323333333333333333333333333333333333333333333333333333232322222222222222010000000011000000000000000000000000000000000000000000000000000000000000007077770700007777777777770001",
                "374|2429368|7|4357|1333753|1333749|1333724|1333724|1300752|6|473|350|270|0|587|384|587|384|444444443444444444444444444444444444444434333333333333333333333333333333333333332222222222222222223333333333333333333333333333333333333333333333333333333222222222222222120000000010010000000000000000000000000000000000000000000000000000000000000077777700007077777777770700",
                "821|2425520|7|4357|1333756|1333749|1333724|1333724|1300752|6|475|350|270|0|589|384|589|384|444444444444444544444444444444444444444434333333333333333333333333333333333333332222222222222222223333333333333333333333333333333333333333333333333333333222222222222222120000000010010000000000000000000000000000000000000000000000000000000000000077777700007077777777770711",
                "1009|2431474|7|4357|1333759|1333749|1333724|1333724|1300752|6|473|352|269|0|588|383|588|383|444444344444444444444444444444444444443433333333333333333333333333333333333333222222222222222222333333333333333333333333333333333333333333333333333333322222222222222212000000001001000000000000000000000000000000000000000000000000000000000000007777770000707777777777070001",
                "1600|2419369|7|4357|1333762|1333749|1333724|1333724|1300752|6|475|352|269|0|587|383|587|383|444444444444454444444444444444444444443433333333333333333333333333333333333333222222222222222222333333333333333333333333333333333333333333333333333333322222222222222212000000001001000000000000000000000000000000000000000000000000000000000000007777770000707777777777070000",
                "1614|2419434|7|4357|1333765|1333749|1333724|1333724|1300752|6|472|351|270|0|589|383|589|383|444444344344444444444444444444444444444433333333333333333333333333333333333333232222222222222222323333333333333333333333333333333333333333333333333333232322222222222222010000000011000000000000000000000000000000000000000000000000000000000000007077770700007777777777770011",
                "912|2424281|7|4381|1333769|1333768|1333724|1333724|1300752|6|523|435|180|0|588|384|588|384|222222222222323333333333333333333333333333333333333333333333333222222222222222120000000010010000000000000000000000000000000000000000000000000000000000000077777700007077777777770710",
                "1059|2427103|7|4381|1333772|1333768|1333724|1333724|1300752|6|525|437|179|0|587|384|588|385|222222222222323333333333333333333333333333333333333333333333232322222222222222010000000011000000000000000000000000000000000000000000000000000000000000007077770700007777777777770000",
                "1115|2424504|7|4381|1333775|1333768|1333724|1333724|1300752|6|527|439|177|0|589|384|590|385|2222222222223233333333333333333333333333333333333333333333232322222222222222010000000011000000000000000000000000000000000000000000000000000000000000007077770700007777777777771001",
                "1144|2424730|7|4381|1333778|1333768|1333724|1333724|1300752|6|528|440|176|0|588|383|588|383|22222222222232333333333333333333333333333333333333333333232322222222222222010000000011000000000000000000000000000000000000000000000000000000000000007077770700007777777777770010",
                "1148|2425992|7|4381|1333781|1333768|1333724|1333724|1300752|6|530|442|175|0|587|383|588|384|22222222222232333333333333333333333333333333333333333333322222222222222212000000001001000000000000000000000000000000000000000000000000000000000000007777770000707777777777070000",
                "238|2423916|7|4461|1333785|1333784|1333724|1333724|1300752|6|473|357|263|0|588|384|588|384|444344444444444444444444444444444433333333333333333333333333333333333333232222222222222222323333333333333333333333333333333333333333333333333333232322222222222222010000000011000000000000000000000000000000000000000000000000000000000000007077770700007777777777770001",
                "403|2427932|7|4461|1333788|1333784|1333724|1333724|1300752|6|472|356|264|0|587|384|587|384|443344444444444444444444444444444434333333333333333333333333333333333333332222222222222222223333333333333333333333333333333333333333333333333333333222222222222222120000000010010000000000000000000000000000000000000000000000000000000000000077777700007077777777770700",
                "739|2425916|7|4461|1333791|1333784|1333724|1333724|1300752|6|474|356|264|0|589|384|589|384|444444444444444444444444444444444434333333333333333333333333333333333333332222222222222222223333333333333333333333333333333333333333333333333333333222222222222222120000000010010000000000000000000000000000000000000000000000000000000000000077777700007077777777770711",
                "952|2429021|7|4461|1333794|1333784|1333724|1333724|1300752|6|472|358|263|0|588|383|588|383|334444444444444444444444444444443433333333333333333333333333333333333333222222222222222222333333333333333333333333333333333333333333333333333333322222222222222212000000001001000000000000000000000000000000000000000000000000000000000000007777770000707777777777070001",
                "1066|2432344|7|4461|1333797|1333784|1333724|1333724|1300752|6|474|358|263|0|587|383|587|383|444444444444444444444444444444443433333333333333333333333333333333333333222222222222222222333333333333333333333333333333333333333333333333333333322222222222222212000000001001000000000000000000000000000000000000000000000000000000000000007777770000707777777777070000",
                "1263|2432195|7|4461|1333800|1333784|1333724|1333724|1300752|6|471|357|264|0|589|383|589|383|544544444444444444444444444434333333333333333333333333333333333333333333232222222222222222323333333333333333333333333333333333333333333333333333232322222222222222010000000011000000000000000000000000000000000000000000000000000000000000007077770700007777777777770011",
                "1280|2426646|7|4461|1333803|1333784|1333724|1333724|1300752|6|475|357|262|0|588|385|588|385|4444445444444444444444444444444444333333333333333333333333333333333333332322222222222222223233333333333333333333333333333333333333333333333333332323222222222222220100000000110000000000000000000000000000000000000000000000000000000000000070777707000077777777777710",
                "1325|2427015|7|4461|1333806|1333784|1333724|1333724|1300752|6|473|355|264|0|587|385|587|385|444443444444444444444444444444444444333333333333333333333333333333333333332322222222222222223233333333333333333333333333333333333333333333333333332323222222222222220100000000110000000000000000000000000000000000000000000000000000000000000070777707000077777777777700",
                "1448|2418156|7|4461|1333809|1333784|1333724|1333724|1300752|6|471|355|264|0|589|385|589|385|445445444444444444444444444444343333333333333333333333333333333333333333332322222222222222223233333333333333333333333333333333333333333333333333332323222222222222220100000000110000000000000000000000000000000000000000000000000000000000000070777707000077777777777711",
                "1592|2419328|7|4461|1333812|1333784|1333724|1333724|1300752|6|475|355|265|0|586|384|586|384|44444444544444444444444444444444444433333333333333333333333333333333333333232222222222222222323333333333333333333333333333333333333333333333333333232322222222222222010000000011000000000000000000000000000000000000000000000000000000000000007077770700007777777777770007",
                "1143|2425970|8|4364|1333988|1333987|1333986|1333986|1267901|6|546|494|157|0|588|384|588|384|22222222222222222222222222222222222222222222221211111111010000000011000000000000000000000000000000000000000000000000000000000000007077770700007777777777770001",
                "1145|2416993|8|4364|1333993|1333987|1333986|1333986|1267901|6|545|493|158|0|587|384|587|384|22222222222222222222222222222222222222222222222212111111110000000010010000000000000000000000000000000000000000000000000000000000000077777700007077777777770700",
                "1254|2434080|8|4364|1333997|1333987|1333986|1333986|1267901|6|547|493|156|0|589|384|589|384|222222222222222222222222222222222222222222222212111111110000000010010000000000000000000000000000000000000000000000000000000000000077777700007077777777770711",
                "1335|2417641|8|4364|1334001|1333987|1333986|1333986|1267901|6|545|495|159|0|588|383|588|383|2222222222222222222222222222222222222222222222121111111111000000001001000000000000000000000000000000000000000000000000000000000000007777770000707777777777070001",
                "1466|2430626|8|4364|1334005|1333987|1333986|1333986|1267901|6|547|495|157|0|587|383|587|383|22222222222222222222222222222222222222222222121111111111000000001001000000000000000000000000000000000000000000000000000000000000007777770000707777777777070000",
                "125|2414070|8|4340|1334010|1334009|1333986|1333986|1267901|6|546|491|157|0|588|384|588|384|22222222222222222222222222222222222222222222222222111111010000000011000000000000000000000000000000000000000000000000000000000000007077770700007777777777770001",
                "891|2416254|8|4340|1334014|1334009|1333986|1333986|1267901|6|545|490|158|0|587|384|587|384|22222222222222222222222222222222222222222222222222221111110000000010010000000000000000000000000000000000000000000000000000000000000077777700007077777777770700",
                "1307|2417531|8|4340|1334018|1334009|1333986|1333986|1267901|6|547|490|156|0|589|384|589|384|222222222222222222222222222222222222222222222222221111110000000010010000000000000000000000000000000000000000000000000000000000000077777700007077777777770711",
                "1510|2418355|8|4340|1334022|1334009|1333986|1333986|1267901|6|545|492|159|0|588|383|588|383|2222222222222222222222222222222222222222222222222211111111000000001001000000000000000000000000000000000000000000000000000000000000007777770000707777777777070001",
                "1539|2429271|8|4340|1334026|1334009|1333986|1333986|1267901|6|547|492|157|0|587|383|587|383|22222222222222222222222222222222222222222222222211111111000000001001000000000000000000000000000000000000000000000000000000000000007777770000707777777777070000",
                "370|2427264|8|4332|1334031|1334030|1333986|1333986|1267901|6|530|503|173|0|588|384|588|384|222222222222222222222222222222222222222222222222222222111111111111111111010000000011000000000000000000000000000000000000000000000000000000000000007077770700007777777777770001",
                "532|2427935|8|4332|1334035|1334030|1333986|1333986|1267901|6|529|502|174|0|587|384|587|384|222222222222222222222222222222222222222222222222222222221111111111111111110000000010010000000000000000000000000000000000000000000000000000000000000077777700007077777777770700",
                "1248|2417320|8|4332|1334039|1334030|1333986|1333986|1267901|6|531|502|172|0|589|384|589|384|2222222222222222222222222222222222222222222222222222221111111111111111110000000010010000000000000000000000000000000000000000000000000000000000000077777700007077777777770711",
                "1277|2417403|8|4332|1334043|1334030|1333986|1333986|1267901|6|529|504|175|0|588|383|588|383|22222222222222222222222222222222222222222222222222222211111111111111111111000000001001000000000000000000000000000000000000000000000000000000000000007777770000707777777777070001",
                "1285|2417451|8|4332|1334047|1334030|1333986|1333986|1267901|6|531|504|173|0|587|383|587|383|222222222222222222222222222222222222222222222222222211111111111111111111000000001001000000000000000000000000000000000000000000000000000000000000007777770000707777777777070000",
                "701|2415538|8|4324|1334052|1334051|1333986|1333986|1267901|6|539|428|174|0|588|384|588|384|444444444433333333333333333333333333333333333333333333333222222222222222120000000010010000000000000000000000000000000000000000000000000000000000000077777700007077777777770710",
                "1018|2429022|8|4324|1334055|1334051|1333986|1333986|1267901|6|539|436|165|0|587|384|587|383|3433333333333333333333333333333333333333333333232322222222222222010000000011000000000000000000000000000000000000000000000000000000000000007077770700007777777777770000",
                "1256|2417334|8|4324|1334058|1334051|1333986|1333986|1267901|6|537|427|175|0|589|384|589|384|44444444343333333333333333333333333333333333333333333333232322222222222222010000000011000000000000000000000000000000000000000000000000000000000000007077770700007777777777771001",
                "1265|2417364|8|4324|1334061|1334051|1333986|1333986|1267901|6|542|441|161|0|588|383|587|382|333333333333333333333333333333333333333333322222222222222212000000001001000000000000000000000000000000000000000000000000000000000000007777770000707777777777070001",
                "1295|2417485|8|4324|1334064|1334051|1333986|1333986|1267901|6|541|440|163|0|587|383|587|383|33333333333333333333333333333333333333333333322222222222222212000000001001000000000000000000000000000000000000000000000000000000000000007777770000707777777777070000",
                "1326|2417611|8|4324|1334067|1334051|1333986|1333986|1267901|6|536|420|183|0|589|383|589|383|4444444444444434333333333333333333333333333333333333333333333333322222222222222212000000001001000000000000000000000000000000000000000000000000000000000000007777770000707777777777071001",
                "1356|2417704|8|4324|1334070|1334051|1333986|1333986|1267901|6|536|426|175|0|588|385|588|385|44444444343333333333333333333333333333333333333333333333333222222222222222120000000010010000000000000000000000000000000000000000000000000000000000000077777700007077777777770701",
                "1369|2417771|8|4324|1334073|1334051|1333986|1333986|1267901|6|534|425|176|0|587|385|587|385|44444444333333333333333333333333333333333333333333333333332323222222222222220100000000110000000000000000000000000000000000000000000000000000000000000070777707000077777777777700",
                "1378|2417806|8|4324|1334076|1334051|1333986|1333986|1267901|6|537|433|168|0|589|385|589|385|443433333333333333333333333333333333333333333333332323222222222222220100000000110000000000000000000000000000000000000000000000000000000000000070777707000077777777777711",
                "1422|2418046|8|4324|1334079|1334051|1333986|1333986|1267901|6|534|418|184|0|586|384|586|384|4444444444444434333333333333333333333333333333333333333333333333333222222222222222120000000010010000000000000000000000000000000000000000000000000000000000000077777700007077777777770770",
            };
            var output=new List<string>();var bridge=new BridgeRouteTrace.Bridge {Id=703,Global=2432893,Parent=600,ParentGlobal=2432853};
            for(int y=474;y<=476;y++)for(int x=600;x<=604;x++)bridge.Deck.Add(BridgeRouteTrace.Bridge.XY(x,y));
            var routes=new BridgeRouteTrace((kind,text)=>output.Add(kind+","+text),()=>new[]{bridge},()=>1);routes.Begin(1);
            foreach(string encoded in paths)
            {
                string[] v=encoded.Split('|');int I(int index)=>int.Parse(v[index]);
                var t=new BridgeRouteTrace.Track {Unit=I(0),Global=uint.Parse(v[1]),Player=I(2),Tribe=I(3),Command=long.Parse(v[4]),ParentEvent=long.Parse(v[5]),Consumer=long.Parse(v[6]),PlanningRoot=long.Parse(v[7]),CandidatePlan=long.Parse(v[8]),Phase=I(9),Session=1};
                var h=new BridgeRouteTrace.Header(t.Global,I(10),I(11),I(10),I(11),I(12),I(13),sx:I(14),sy:I(15));
                byte[] bytes=Enumerable.Range(0,v[18].Length/2).Select(i=>Convert.ToByte(v[18].Substring(i*2,2),16)).ToArray();
                routes.ObservePlan(t,h,bytes,0);Check(t.Relevant&&t.Content.Complete,"observed Log243 packed route independently intersects exact15-cell deck");
                int x=I(10),y=I(11);for(int step=0;step<I(12);step++)Check(BridgeRouteTrace.Step((bytes[step/2]>>(4*(step%2)))&15,ref x,ref y),"observed native directions valid");
                Check(x==I(16)&&y==I(17),"independent endpoint reconstruction matches recorded endpoint, not assumed target");
            }
            routes.Flush();Check(Rows(output,"route-observations").Count()==67&&Rows(output,"route-bindings").Count()==67,"all67 frozen crossings preserve bindings and observations");
            long routeBytes=output.Sum(line=>System.Text.Encoding.UTF8.GetByteCount(line)+95);Check(routeBytes<150000,"67 changed relevant route observations are bounded with prefixes");
            // Preserve all19 original assignment values through the real public getter.
            uint[] assigned={181065u,178810u,181065u,178810u,179564u,177298u,181812u,180315u,178809u,176538u,178054u,172710u,182557u,180314u,175776u,183301u,174247u,175013u,173479u};
            foreach(uint task in assigned) {GameUnit unit=default;unit.r_AI_ContextTargetBuildingTileId=task;unit.r_AIState=101;unit.r_AI_LastIssuedTribeCommand=3;Check(BridgeDecisionTrace.ReadAssignedTask(&unit)==task&&unit.r_AIState==101&&unit.r_AI_LastIssuedTribeCommand==3,"19 frozen task values use public production getter and separate state/command");}
            var trace=new BridgeDecisionTrace(null,_=>new BridgeDecisionTrace.AttackStamp(1,8,1,6,0,1,1),()=>1,r=>r==0x60AD6CC?1:r==0x37ED4CC?2:0);trace.StartSession(91);
            long before=Shared.DebugLogHelper.Bytes;var attack=BridgeNativeDefinition.Sites.Single(s=>s.Rva==0x11A980);var topology=BridgeNativeDefinition.Sites.Single(s=>s.Rva==0xE49D0);
            for(int i=0;i<1341577;i++) {var c=trace.Enter(attack,IntPtr.Zero,4364);trace.Exit(c,true,null,IntPtr.Zero);if(i<8531) {c=trace.Enter(topology,IntPtr.Zero,i<75?1:0);trace.Exit(c,true,i<75?1:0,IntPtr.Zero);}if(i<30018) {trace.CountCommand(true);trace.CountCommand(false);}if(i%1000==0)trace.Drain();}
            trace.FlushRegions();while(trace.Pending>0)trace.Drain();Check(Shared.DebugLogHelper.Bytes-before<100000,"unchanged current hot population fits1MB/min including prefix allowance");
            int[,] rare={{0x64460,3},{0x645C0,3},{0xD95E0,16},{0xD9190,16},{0x10DF60,11},{0x115B10,11},{0x122B40,8},{0xE7F60,2},{0x110EC0,19},{0x111330,21},{0x111D90,21},{0x3C2E0,294},{0x2D250,16},{0x2C480,11},{0x2C5A0,11},{0x3BD50,5},{0xCF360,751},{0xCF400,13}};
            for(int row=0;row<rare.GetLength(0);row++)for(int i=0;i<rare[row,1];i++) {var c=trace.Enter(BridgeNativeDefinition.Sites.Single(s=>s.Rva==rare[row,0]),IntPtr.Zero,8,1);trace.Exit(c,true,1,IntPtr.Zero);if(i%32==0)trace.Drain();}
            trace.FlushRegions();while(trace.Pending>0)trace.Drain();Check(trace.Entered==1351340&&trace.Entered==trace.Exited&&trace.Summary().Contains("commandPre=30018,commandPost=30018")&&trace.Failures==0,"complete current production population has exact counts and no capture failures");
            Console.WriteLine("Log243 regression: calls="+trace.Entered+",commands=30018,routeObservations=67,taskValues=19,routeBytesWithPrefix="+routeBytes+",nativeBytesWithPrefix="+(Shared.DebugLogHelper.Bytes-before));
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
