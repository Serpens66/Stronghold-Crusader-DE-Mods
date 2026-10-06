using System;
using System.Linq;
namespace EnemyBridgePathTest
{
    internal static class LeanTraceTests
    {
        private static int checks;
        private static void Check(bool value,string text) {checks++;if(!value)throw new Exception(text);}
        internal static int Run()
        {
            var unavailable=new BridgeDecisionTrace(null);
            bool guarded=false;
            try {typeof(BridgeDecisionTrace).GetMethod("Read",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(unavailable,new object[]{0});}
            catch(System.Reflection.TargetInvocationException error) {guarded=error.InnerException is InvalidOperationException;}
            Check(guarded,"unavailable native module is rejected before a pointer read");
            Check(BridgeBuildingIndex.SpanIndex(0,3999)==-1&&BridgeBuildingIndex.SpanIndex(-1,3999)==-1,"unknown/negative IDs never become a slot or API lookup");
            Check(BridgeBuildingIndex.SpanIndex(4000,3999)==-1&&BridgeBuildingIndex.SpanIndex(1,3999)==0&&BridgeBuildingIndex.SpanIndex(3999,3999)==3998,"exact 1-based building boundary");
            var bridgeA=new BridgeBuildingIndex.Entry {Id=720,Global=2432353,Owner=1,NativeParent=0,Grid=5,Orientation=6};
            var bridgeB=new BridgeBuildingIndex.Entry {Id=721,Global=2432354,Owner=2,NativeParent=826,Grid=5,Orientation=0};
            Check(BridgeBuildingIndex.IsCurrent(bridgeA,2432353,1,0,5,6)&&BridgeBuildingIndex.IsCurrent(bridgeB,2432354,2,826,5,0),"independent bridges including opaque native connection field");
            Check(!BridgeBuildingIndex.IsCurrent(bridgeA,2432355,1,0,5,6),"reused building slot invalidates association");
            Check(!BridgeBuildingIndex.IsCurrent(bridgeA,2432353,2,0,5,6)&&!BridgeBuildingIndex.IsCurrent(bridgeA,2432353,1,827,5,6),"ownership and raw field change invalidate association");
            Check(!BridgeBuildingIndex.IsCurrent(bridgeA,2432353,1,0,0,6)&&!BridgeBuildingIndex.IsCurrent(bridgeA,2432353,1,0,5,0),"initializing footprint and orientation invalidate association");
            Check(!BridgeBuildingIndex.IsCurrent(bridgeA,2432353,1,0,5,6,1,0),"position change invalidates cached spatial coupling");
            var site=BridgeNativeDefinition.Sites.First(s=>s.Rva==0x11A980);
            int reads=0;uint global=7;int target=0;bool fail=false;
            var trace=new BridgeDecisionTrace(null,id=>new BridgeDecisionTrace.AttackStamp(global,2,3,0,0,target,12),()=>{reads++;if(fail)throw new Exception("fixture capture failure");return 1;});
            trace.StartSession(1);
            var timer=System.Diagnostics.Stopwatch.StartNew();
            for(int i=0;i<75117;i++) {var scope=trace.Enter(site,IntPtr.Zero,4);trace.Exit(scope,true,null,IntPtr.Zero);}
            timer.Stop();
            Check(trace.Entered==75117&&trace.Exited==75117,"exact observed load pairing");
            Check(trace.Captures==2&&reads==2,"unchanged attacks capture only initial definition");
            Check(trace.Coalesced==75116,"all repeats counted");
            Check(trace.Pending==4,"constant output for unchanged attacks includes two shared context definitions");
            long loggedBefore=Shared.DebugLogHelper.Bytes;
            while(trace.Pending>0) trace.Drain();
            Check(Shared.DebugLogHelper.Bytes-loggedBefore<4096,"less than 4KB steady-load trace for 75117 calls");
            Console.WriteLine("Lean steady load ms="+timer.Elapsed.TotalMilliseconds.ToString("F3")+",records="+trace.OutputRecords+",bytes="+(Shared.DebugLogHelper.Bytes-loggedBefore));
            target=5;var changed=trace.Enter(site,IntPtr.Zero,4);trace.Exit(changed,true,null,IntPtr.Zero);
            Check(trace.Captures==4,"target change retains entry and exit");
            global=8;var reused=trace.Enter(site,IntPtr.Zero,4);trace.Exit(reused,true,null,IntPtr.Zero);
            Check(trace.Captures==6,"tribe ID reuse distinguished by global identity");
            var parent=trace.Enter(site,IntPtr.Zero,4);trace.Command("command-pre","op=10");trace.Command("command-post","op=10");trace.Exit(parent,true,null,IntPtr.Zero);
            Check(trace.Captures==10,"command promotes quiet parent and captures actual command state");
            Check(trace.Entered==trace.Exited,"promoted parent still balanced");
            var outer=trace.Enter(site,IntPtr.Zero,4);var inner=trace.Enter(site,IntPtr.Zero,5);trace.Exit(inner,true,null,IntPtr.Zero);trace.Exit(outer,true,null,IntPtr.Zero);
            Check(!trace.InsideNative&&trace.Entered==trace.Exited,"pooled nesting restored");
            for(int i=0;i<10000;i++)trace.Region(2,1,2,0,0,1);
            long before=trace.Pending;trace.FlushRegions();Check(trace.Pending==before+1,"numeric region repeat aggregation");
            for(int i=0;i<100;i++)trace.Observe("fixture","i="+i);
            long records=trace.OutputRecords;trace.Drain();Check(trace.OutputRecords-records<=64&&trace.Pending>0,"bounded rendering drain");
            long pending=trace.Pending;long delivered=trace.OutputRecords;trace.End();Check(trace.Pending==pending+1&&trace.OutputRecords==delivered+1,"map end emits one immediate summary and never drains unbounded backlog");
            trace.StartSession(2);fail=true;var broken=trace.Enter(site,IntPtr.Zero,4);trace.Exit(broken,true,null,IntPtr.Zero);
            Check(trace.Failures==2&&trace.Entered==trace.Exited&&!trace.InsideNative,"capture exceptions isolated and scopes restored");
            fail=false;trace.StartSession(3);var recovery=trace.Enter(site,IntPtr.Zero,4);trace.StartSession(4);
            Check(!trace.DetailedNative,"old native scope is not a detailed parent in replacement session");
            var seed=BridgeNativeDefinition.Sites.First(s=>s.Rva==0xD95E0);
            var fresh=trace.Enter(seed,IntPtr.Zero,1,0,1);
            Check(fresh.PlanningPlayer==0,"new session does not inherit old planning player");
            trace.Exit(fresh,true,null,IntPtr.Zero);Check(!trace.DetailedNative,"return restores foreign scope only for unwinding");
            trace.Exit(recovery,true,null,IntPtr.Zero);
            Check(trace.Entered==trace.Exited,"session crossing preserves scope lifetime");
            Check(trace.ScopeAllocations<=2,"no scope allocation per repeated callback");
            var bounded=new BridgeDecisionTrace(null,_=>default,()=>1);bounded.StartSession(1);
            for(int i=0;i<5000;i++) bounded.Observe("fixture","same");
            Check(bounded.Pending==4096&&bounded.Summary().Contains("backgroundOverflow=904")&&bounded.Summary().Contains("traceComplete=False"),"overflow bounded and explicitly invalidates completeness");
            // Background saturation cannot consume the reserved decision queue.
            var important=bounded.Enter(site,IntPtr.Zero,5);bounded.Exit(important,true,null,IntPtr.Zero);
            Check(bounded.Pending==4100&&bounded.Summary().Contains("overflow=0,"),"critical reservation survives background saturation");
            MixedLoad(site);
            PlanningAndCommandFrames();
            FocusedDecisionEvidence();
            ProductionAssignmentAndCurrentLoad();
            ObservedPlayerEightAccess();
            StablePopulationAndTransport();
            RaisedPopulation();
            LatestShadowPopulation();
            Console.WriteLine("PASS lean trace: 75117 repeated attacks, two initial captures; exact counts, changes, nesting, exceptions and bounded drain.");
            return checks;
        }
        private static void RaisedPopulation()
        {
            var trace=new BridgeDecisionTrace(null,_=>new BridgeDecisionTrace.AttackStamp(1,8,125,5,0,1,1),()=>1,r=>r==0x60AD6CC?1:r==0x37ED4CC?2:0);
            trace.StartSession(604);long startBytes=Shared.DebugLogHelper.Bytes;
            int[,] calls={{0x11A980,2480079},{0xE49D0,16015},{0x64460,3},{0x645C0,3},{0xD95E0,80},{0xD9190,80},{0x10DF60,73},{0x115B10,73},{0x122B40,119},{0xE7F60,12},{0x110EC0,116},{0x111330,557},{0x111D90,557},{0x3C2E0,560},{0x2D250,80},{0x2C480,73},{0x2C5A0,73},{0x3BD50,7},{0xCF360,2047},{0xCF400,205}};
            for(int row=0;row<calls.GetLength(0);row++)
            {
                var site=BridgeNativeDefinition.Sites.Single(s=>s.Rva==calls[row,0]);
                for(int i=0;i<calls[row,1];i++)
                {
                    var scope=trace.Enter(site,IntPtr.Zero,site.Rva==0xE49D0?(i<131?1:0):8,1);trace.Exit(scope,true,site.Rva==0xE49D0&&i<131?1:0,IntPtr.Zero);
                    if(row==0&&i<40729) {trace.CountCommand(true);trace.CountCommand(false);trace.CountEvent(BridgeDecisionTrace.CommandCountData(3,i%8+1,3,1,false,true));}
                    if(i%(row==0?1000:16)==0)trace.Drain();
                }
            }
            trace.FlushRegions();while(trace.Pending>0)trace.Drain();
            Check(trace.Entered==2500812&&trace.Exited==2500812,"raised exact2500812 native calls");
            Check(trace.Summary().Contains("commandPre=40729,commandPost=40729")&&trace.Summary().Contains("invalidIds=0"),"raised command pairs and no invalid ID lookups");
            Check(trace.Failures==0&&trace.Summary().Contains("overflow=0,backgroundOverflow=0"),"raised production load complete without overflow");
            long bytes=Shared.DebugLogHelper.Bytes-startBytes;
            Check(bytes*60.0/123.428<1000000,"raised synthetic unchanged population below1MB/min including prefixes");
            Console.WriteLine("Raised population:2500812 calls,40729 command pairs,bytesWithPrefixAllowance="+bytes);
        }
        private static void LatestShadowPopulation()
        {
            var trace=new BridgeDecisionTrace(null,_=>new BridgeDecisionTrace.AttackStamp(1,8,125,6,0,1,1),()=>1,r=>r==0x60AD6CC?1:r==0x37ED4CC?2:0);
            trace.StartSession(605);long bytes=Shared.DebugLogHelper.Bytes;
            int[,] calls={{0x11A980,975674},{0xE49D0,6557},{0x64460,1},{0xD95E0,8},{0xD9190,8},{0x10DF60,3},{0x115B10,3},{0x3C2E0,224},{0x2D250,8},{0x2C480,3},{0x2C5A0,3},{0x3BD50,5},{0xCF360,370},{0xCF400,1}};
            for(int row=0;row<calls.GetLength(0);row++)
            {
                var site=BridgeNativeDefinition.Sites.Single(s=>s.Rva==calls[row,0]);
                for(int i=0;i<calls[row,1];i++)
                {
                    var scope=trace.Enter(site,IntPtr.Zero,site.Rva==0xE49D0?(i<57?1:0):8,1);trace.Exit(scope,true,site.Rva==0xE49D0&&i<57?1:0,IntPtr.Zero);
                    if(row==0&&i<21456) {trace.CountCommand(true);trace.CountCommand(false);trace.CountEvent(BridgeDecisionTrace.CommandCountData(3,8,3,1,false,true));}
                    if(i%(row==0?1000:16)==0)trace.Drain();
                }
            }
            trace.FlushRegions();while(trace.Pending>0)trace.Drain();
            Check(trace.Entered==982868&&trace.Exited==982868,"latest shadow run exact982868 calls");
            Check(trace.Summary().Contains("commandPre=21456,commandPost=21456")&&trace.Failures==0&&trace.Summary().Contains("overflow=0,backgroundOverflow=0"),"latest command population complete");
            long volume=Shared.DebugLogHelper.Bytes-bytes;Check(volume*60.0/61.824<1000000,"latest unchanged population under1MB/min including prefix allowance");
            Console.WriteLine("Latest shadow population:982868 calls,21456 commands,57 rebuilds,quietBytesWithPrefixes="+volume);
        }
        private static void StablePopulationAndTransport()
        {
            var attack=BridgeNativeDefinition.Sites.Single(s=>s.Rva==0x11A980);
            var topology=BridgeNativeDefinition.Sites.Single(s=>s.Rva==0xE49D0);
            var trace=new BridgeDecisionTrace(null,_=>new BridgeDecisionTrace.AttackStamp(1,8,125,6,0,1,1),()=>1,r=>r==0x60AD6CC?1:r==0x37ED4CC?2:0);
            trace.StartSession(601);long bytes=Shared.DebugLogHelper.Bytes;
            for(int i=0;i<2103209;i++)
            {
                var call=trace.Enter(attack,IntPtr.Zero,4340);trace.Exit(call,true,null,IntPtr.Zero);
                if(i<13747) {call=trace.Enter(topology,IntPtr.Zero,i<114?1:0);trace.Exit(call,true,i<114?1:0,IntPtr.Zero);}
                if(i<36923) {trace.CountCommand(true);trace.CountCommand(false);trace.CountEvent(BridgeDecisionTrace.CommandCountData(3,i%8+1,3,1,false,true));}
                if(i%1000==0)trace.Drain();
            }
            trace.FlushRegions();while(trace.Pending>0)trace.Drain();
            long quietBytes=Shared.DebugLogHelper.Bytes-bytes;
            Check(trace.Entered==2116956&&trace.Exited==2116956&&trace.Captures==230,"stable hot population:114 rebuilt topologies, exact pairing and bounded capture");
            // The frozen session lasts113.069s. Use its time-normalized acceptance
            // budget, not an unrelated absolute100KB threshold.
            Check(quietBytes*60.0/113.069<1000000,"stable hot population including logger prefix allowance below1MB/min; bytes="+quietBytes);
            int[,] rare={{0x64460,1},{0xD95E0,25},{0xD9190,25},{0x10DF60,18},{0x115B10,18},{0x3C2E0,476},{0x2D250,25},{0x2C480,18},{0x2C5A0,18},{0x3BD50,7},{0xCF360,1118},{0xCF400,1}};
            for(int row=0;row<rare.GetLength(0);row++)for(int i=0;i<rare[row,1];i++)
            {var site=BridgeNativeDefinition.Sites.Single(s=>s.Rva==rare[row,0]);var call=trace.Enter(site,IntPtr.Zero,8,1);trace.Exit(call,true,1,IntPtr.Zero);if(i%32==0)trace.Drain();}
            trace.FlushRegions();while(trace.Pending>0)trace.Drain();
            Check(trace.Entered==2118706&&trace.Exited==2118706&&trace.Summary().Contains("commandPre=36923,commandPost=36923"),"stable full production call population is exactly counted");
            Check(trace.Failures==0&&trace.Summary().Contains("overflow=0,backgroundOverflow=0"),"stable workload preserves completeness");
            var compact=new BridgeDecisionTrace(null,_=>new BridgeDecisionTrace.AttackStamp(uint.MaxValue,8,125,6,0,1,uint.MaxValue),()=>1);
            compact.StartSession(602);int start=Shared.DebugLogHelper.Recent.Count;
            var scope=compact.Enter(attack,IntPtr.Zero,4340);
            for(int i=0;i<4;i++)compact.Command(i%2==0?"command-pre":"command-post","op="+(800+i),8);
            compact.Exit(scope,true,0x100000001,IntPtr.Zero);while(compact.Pending>0)compact.Drain();
            var output=Shared.DebugLogHelper.Recent.Skip(start).ToArray();
            Check(output.Count(s=>s.Contains("kind=command-context,"))==1&&output.Count(s=>s.Contains("commandContext=1,"))==4,"equal numeric command contexts defined once and referenced without dropping commands");
            Check(output.Any(s=>s.Contains("kind=native-frame-batch,")&&s.Contains("/4294967297/")),"numeric native frame preserves full64-bit return");
            compact.StartSession(603);compact.Command("command-pre","op=900",8);while(compact.Pending>0)compact.Drain();
            Check(Shared.DebugLogHelper.Recent.Last().Contains("commandContext=2,"),"map reload redefines context with monotonic identity");
            Console.WriteLine("Stable production population:2118706 calls,36923 commands,quietBytesWithPrefixes="+quietBytes+"; native frames and numeric context references preserve exact results");
        }
        private static void MixedLoad(BridgeNativeDefinition.Site attack)
        {
            var topology=BridgeNativeDefinition.Sites.First(s=>s.Rva==0xE49D0);
            long fullReads=0;
            var trace=new BridgeDecisionTrace(null,_=>new BridgeDecisionTrace.AttackStamp(8,5,1051,3,0,0,0),()=>{fullReads++;return 1;},rva=>rva==0x60AD6CC?1:rva==0x37ED4CC?2:0);
            trace.StartSession(100);
            long bytesBefore=Shared.DebugLogHelper.Bytes;
            var timer=System.Diagnostics.Stopwatch.StartNew();
            for(int i=0;i<834632;i++)
            {
                var scope=trace.Enter(attack,IntPtr.Zero,4359);trace.Exit(scope,true,null,IntPtr.Zero);
                if(i<18835)
                {
                    trace.CountCommand(true);trace.CountCommand(false);
                    trace.CountEvent(new BridgeDecisionTrace.Arguments(3,i%8+1,0,0,0,1));
                    trace.CountEvent(new BridgeDecisionTrace.Arguments(3,i%8+1,0,1,1,1));
                    trace.Region(i%8+1,i%99+1,1,0,1,1);
                    Check(!trace.ShouldDetailCommand(false,false,true),"ordinary unscoped unit movement remains counted without full capture");
                }
                if(i<5082)
                {
                    bool rebuilt=i<47;
                    var call=trace.Enter(topology,IntPtr.Zero,rebuilt?1:0);
                    trace.Exit(call,true,rebuilt?1:0,IntPtr.Zero);
                }
                if(i%1000==0)trace.Drain();
            }
            trace.FlushRegions();trace.FlushCosts();while(trace.Pending>0)trace.Drain();timer.Stop();
            long bytes=Shared.DebugLogHelper.Bytes-bytesBefore;
            Check(trace.Entered==839714&&trace.Exited==839714,"mixed actual log population preserves exact native counts");
            Check(trace.Captures==96&&fullReads==96,"dirty pending topology is not rebuilt: only 47 actual pairs plus initial attack pair");
            Check(trace.Summary().Contains("commandPre=18835,commandPost=18835")&&trace.Summary().Contains("overflow=0,backgroundOverflow=0"),"mixed command population is exactly counted without loss");
            Check(bytes<700000,"mixed 42-second population fits below one MB/min and 95 percent volume reduction");
            Check(trace.ShouldDetailCommand(true,false,true)&&trace.ShouldDetailCommand(false,true,true)&&!trace.ShouldDetailCommand(false,true,false),"changed group and nearby bridge commands retain details; exact repeats coalesce");
            Check(BridgeDecisionTrace.TopologyWillRebuild(1,0,100)&&BridgeDecisionTrace.TopologyWillRebuild(0,1,1)&&!BridgeDecisionTrace.TopologyWillRebuild(0,1,2)&&!BridgeDecisionTrace.TopologyWillRebuild(0,0,0)&&!BridgeDecisionTrace.TopologyWillRebuild(0,1,int.MinValue),"native forced, countdown, clean and signed-wrap rebuild branches");
            Check(trace.CommandIsNew(3,100,8,new BridgeDecisionTrace.Arguments(1,5,3,602,477,0))&&!trace.CommandIsNew(3,100,8,new BridgeDecisionTrace.Arguments(1,5,3,602,477,0))&&trace.CommandIsNew(3,100,9,new BridgeDecisionTrace.Arguments(1,5,3,602,477,0)),"unit reuse and fixed numeric command deduplication");
            Check(!BridgeDecisionTrace.CommandCountData(3,5,3,1,false,true).Equals(BridgeDecisionTrace.CommandCountData(3,5,3,0x100000001,false,true)),"background command counters preserve full 64-bit returns");
            Console.WriteLine("Mixed population: nativeCalls="+trace.Entered+",commands=18835,actualRebuilds=47,captures="+trace.Captures+",bytes="+bytes+",ms="+timer.Elapsed.TotalMilliseconds.ToString("F3"));
        }
        private static void FocusedDecisionEvidence()
        {
            Check(BridgeDecisionTrace.AccessBranch(new BridgeDecisionTrace.Arguments(1,1,12,13,1,1))=="equal-nonzero-regions-accept","equal keep areas accept without claiming a query");
            foreach(int pcl in new[]{31,1,31})
                Check(BridgeDecisionTrace.AccessBranch(new BridgeDecisionTrace.Arguments(1,1,12,13,1,pcl))==(pcl==1?"equal-nonzero-regions-accept":"different-regions-query-required"),"player8 raised/down/raised region branch regression");
            Check(BridgeDecisionTrace.AccessBranch(new BridgeDecisionTrace.Arguments(0,1,0,1,0,1))=="attacker-inactive-accept"&&BridgeDecisionTrace.AccessBranch(new BridgeDecisionTrace.Arguments(1,0,1,0,1,0))=="target-inactive-accept","inactive player branches precede region checks");
            Check(BridgeDecisionTrace.AccessBranch(new BridgeDecisionTrace.Arguments(1,1,1,2,0,1))=="attacker-zero-region-reject"&&BridgeDecisionTrace.AccessBranch(new BridgeDecisionTrace.Arguments(1,1,1,2,1,0))=="target-zero-region-reject","zero regions are separate rejections");
            var trace=new BridgeDecisionTrace(null,_=>default,()=>1);trace.StartSession(70);
            var image=new BridgeDecisionTrace.TableImage {Rva=0x2EA70E4,Slot=8,Count=2,Available=2,Rows=new[]{214725,214724,40,0,215376,215375,41,0}};
            trace.PublishTable(image,"post");long first=image.Definition;
            var changed=new BridgeDecisionTrace.TableImage {Rva=image.Rva,Slot=8,Count=2,Available=1,Rows=new[]{214725,214724,40,12,215376,215375,41,0}};
            trace.PublishTable(changed,"selection-post");Check(changed.Definition!=first,"reservation creates a new candidate definition");
            Check(BridgeDecisionTrace.ReservationTransitions(image,changed,12,214725,out int selected)==1&&selected==0,"unique task and actual unit reservation transition");
            Check(BridgeDecisionTrace.ReservationTransitions(changed,changed,12,214725,out selected)==0,"already reserved row is not fabricated as a new transition");
            Check(BridgeDecisionTrace.ReservationTransitions(image,changed,13,214725,out selected)==0&&BridgeDecisionTrace.ReservationTransitions(image,changed,0,214725,out selected)==0,"other and zero unit cannot acquire candidate provenance");
            var ambiguous=new BridgeDecisionTrace.TableImage {Count=2,Rows=new[]{214725,214724,40,12,214725,215375,41,12}};
            Check(BridgeDecisionTrace.ReservationTransitions(image,ambiguous,12,214725,out selected)==2,"multiple matching reservations remain ambiguous");
            var repeat=new BridgeDecisionTrace.TableImage {Rva=image.Rva,Slot=8,Count=2,Available=1,Rows=(int[])changed.Rows.Clone()};trace.PublishTable(repeat,"pre");
            Check(repeat.Definition==changed.Definition,"pre/post share an identical numeric table definition");
            var trim=new BridgeDecisionTrace.TableImage {Rva=image.Rva,Slot=8,Count=1,Available=1,Rows=new[]{214725,214724,40,12}};trace.PublishTable(trim,"post");Check(trim.Definition!=repeat.Definition,"count truncation is recorded even with no changed retained rows");
            var physical=new BridgeDecisionTrace.NumericImage(22) {Link="fixture"};
            for(int i=0;i<17;i++)physical.Add(i==0?703:i==1?123:0);
            foreach(long value in new long[]{214725,1,1145,0,255})physical.Add(value);
            var planning=new BridgeDecisionTrace.NumericImage(7);
            foreach(long value in new long[]{703,123,8,214725,0,0,10})planning.Add(value);
            long pending=trace.Pending;trace.PublishBridgeImage(physical,planning,8,true);Check(trace.Pending==pending+2,"physical and planning numeric fragments defined once");
            typeof(BridgeDecisionTrace).GetMethod("MarkBridgePlanning",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(trace,new object[]{new BridgeDecisionTrace.Scope {PlanningPlayer=8,Id=123}});
            pending=trace.Pending;
            var numericTimer=System.Diagnostics.Stopwatch.StartNew();
            for(int i=0;i<10000;i++)trace.PublishBridgeImage(physical,planning,8,true);
            numericTimer.Stop();Check(trace.Pending==pending,"10000 unchanged numeric captures create no text or definitions");
            Check(trace.BridgePlanCurrent(703,123,8),"unrelated topology preserves per-bridge fresh planning");
            planning.Data[6]=11;trace.PublishBridgeImage(physical,planning,8,true);Check(trace.Pending==pending+1,"planning-only change reuses physical definition");
            physical.Data[1]=124;planning.Data[1]=124;trace.PublishBridgeImage(physical,planning,8,true);Check(trace.Pending==pending+3,"building reuse gets independent numeric identities");
            Check(!trace.BridgePlanCurrent(703,123,8)&&!trace.BridgePlanCurrent(703,124,8),"reused building cannot inherit plan");
            Console.WriteLine("Numeric unchanged capture 10000 ms="+numericTimer.Elapsed.TotalMilliseconds.ToString("F3"));
            long bytes=Shared.DebugLogHelper.Bytes;
            for(int i=0;i<1238;i++)trace.CountEvent(new BridgeDecisionTrace.Arguments(4,i%8+1,i,1,1,0));
            trace.FlushRegions();Check(trace.Pending<100,"1238 observed counter tuples batched into at most39 records");
            while(trace.Pending>0)trace.Drain();Check(Shared.DebugLogHelper.Bytes-bytes<80000,"counter and definition transport remains bounded without discarding tuples");
            var attack=BridgeNativeDefinition.Sites.First(s=>s.Rva==0x11A980);var topology=BridgeNativeDefinition.Sites.First(s=>s.Rva==0xE49D0);
            var mixed=new BridgeDecisionTrace(null,_=>default,()=>1,r=>r==0x60AD6CC?1:r==0x37ED4CC?2:0);mixed.StartSession(80);
            for(int i=0;i<1661352;i++)
            {
                var scope=mixed.Enter(attack,IntPtr.Zero,1);mixed.Exit(scope,true,null,IntPtr.Zero);
                if(i<10518) {var t=mixed.Enter(topology,IntPtr.Zero,i<95?1:0);mixed.Exit(t,true,i<95?1:0,IntPtr.Zero);}
                if(i<35588) {mixed.CountCommand(true);mixed.CountCommand(false);mixed.CountEvent(new BridgeDecisionTrace.Arguments(3,i%8+1,0,0,0,2));}
                if(i%1000==0)mixed.Drain();
            }
            mixed.FlushRegions();while(mixed.Pending>0)mixed.Drain();
            Check(mixed.Entered==1671870&&mixed.Entered==mixed.Exited&&mixed.Captures==192,"latest observed hot population keeps exact pairing and only actual rebuild captures");
            Check(mixed.Summary().Contains("commandPre=35588,commandPost=35588")&&mixed.Summary().Contains("overflow=0,backgroundOverflow=0"),"latest observed command population has no lost counts");
        }
        private static unsafe void ProductionAssignmentAndCurrentLoad()
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var unit=default(SHCDESE.Interop.GameUnit);
            unit.r_AI_ContextTargetBuildingTileId=181065;unit.r_AIState=0x65;unit.r_AI_LastIssuedTribeCommand=3;unit.r_GlobalId=90;
            Check(System.Runtime.InteropServices.Marshal.OffsetOf(typeof(SHCDESE.Interop.GameUnit),"r_AI_ContextTargetBuildingTileId").ToInt32()==0x3A4,"installed assigned-task offset is the audited native write");
            Check(System.Runtime.InteropServices.Marshal.OffsetOf(typeof(SHCDESE.Interop.GameUnit),"r_AIState").ToInt32()==0x2BC,"native assignment state maps to AIState");
            Check(BridgeDecisionTrace.ReadAssignedTask(&unit)==181065&&BridgeDecisionTrace.AssignmentMatches(&unit,181065),"production getter accepts AIState65 independently of other issued command");
            unit.r_AIState=3;unit.r_AI_LastIssuedTribeCommand=0x65;
            Check(!BridgeDecisionTrace.AssignmentMatches(&unit,181065),"issued command65 cannot substitute AIState");
            unit.r_AIState=0x67;Check(BridgeDecisionTrace.AssignmentMatches(&unit,181065)&&!BridgeDecisionTrace.AssignmentMatches(&unit,178810),"AIState67 and exact selected task");
            IntPtr buffer=System.Runtime.InteropServices.Marshal.AllocHGlobal(0x490);
            try
            {
                System.Runtime.InteropServices.Marshal.StructureToPtr(unit,buffer,false);
                var trace=new BridgeDecisionTrace(null,_=>default,()=>1,null,id=>id==1?throw new InvalidOperationException("fixture unit failure"):buffer);trace.StartSession(900);
                var scope=new BridgeDecisionTrace.Scope {Id=101};
                scope.Selections.Add(new BridgeDecisionTrace.SelectionEvidence {Op=1,Unit=1,Global=90,Task=181065});
                scope.Selections.Add(new BridgeDecisionTrace.SelectionEvidence {Op=2,Unit=2,Global=91,Task=181065});
                scope.Selections.Add(new BridgeDecisionTrace.SelectionEvidence {Op=3,Unit=3,Global=90,Task=181065});
                typeof(BridgeDecisionTrace).GetMethod("CompleteTaskAssignments",flags).Invoke(trace,new object[]{scope});
                Check(trace.Failures==1&&trace.Pending==4,"failure is isolated; reused identity and following valid unit still recorded");
                while(trace.Pending>0)trace.Drain();
                Check(Shared.DebugLogHelper.Recent.Any(s=>s.Contains("selection=3")&&s.Contains("aiState=103")&&s.Contains("command=101")&&s.Contains("nativeAssignedTaskRaw=181065")),"production assignment record preserves both distinct state and command plus actual task");
            }
            finally {System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);}
            var attack=BridgeNativeDefinition.Sites.First(s=>s.Rva==0x11A980);var topo=BridgeNativeDefinition.Sites.First(s=>s.Rva==0xE49D0);
            var mixed=new BridgeDecisionTrace(null,_=>default,()=>1,r=>r==0x60AD6CC?1:r==0x37ED4CC?2:0);mixed.StartSession(901);
            long bytes=Shared.DebugLogHelper.Bytes;
            for(int i=0;i<1548253;i++)
            {
                var call=mixed.Enter(attack,IntPtr.Zero,1);mixed.Exit(call,true,null,IntPtr.Zero);
                if(i<9704) {var t=mixed.Enter(topo,IntPtr.Zero,i<84?1:0);mixed.Exit(t,true,i<84?1:0,IntPtr.Zero);}
                if(i<32410) {mixed.CountCommand(true);mixed.CountCommand(false);mixed.CountEvent(new BridgeDecisionTrace.Arguments(3,i%8+1,3,1,0,2));}
                if(i%1000==0)mixed.Drain();
            }
            mixed.FlushRegions();while(mixed.Pending>0)mixed.Drain();
            Check(mixed.Entered==1557957&&mixed.Entered==mixed.Exited&&mixed.Captures==170,"current hot subset:1548253 attacks,9704 topology calls,84 actual rebuilds");
            Check(mixed.Summary().Contains("commandPre=32410,commandPost=32410")&&mixed.Failures==0,"current command counts and zero capture failures");
            Check(Shared.DebugLogHelper.Bytes-bytes<100000,"steady hot population leaves ample logger-prefix margin below1MB/min");
            int[,] rare={{0x64460,2},{0x645C0,3},{0xD95E0,20},{0xD9190,20},{0x10DF60,13},{0x115B10,13},{0x122B40,9},{0xE7F60,5},{0x110EC0,26},{0x111330,31},{0x111D90,31},{0x3C2E0,336},{0x2D250,20},{0x2C480,13},{0x2C5A0,13},{0x3BD50,7},{0xCF360,1016},{0xCF400,24}};
            for(int row=0;row<rare.GetLength(0);row++)
                for(int i=0;i<rare[row,1];i++)
                {
                    var site=BridgeNativeDefinition.Sites.First(s=>s.Rva==rare[row,0]);
                    var call=mixed.Enter(site,IntPtr.Zero,8,1);mixed.Exit(call,true,1,IntPtr.Zero);
                    if(i%32==0)mixed.Drain();
                }
            mixed.FlushRegions();while(mixed.Pending>0)mixed.Drain();
            Check(mixed.Entered==1559559&&mixed.Entered==mixed.Exited&&mixed.Failures==0,"full latest population includes all1602 less frequent callbacks with exact per-site counts");
            Console.WriteLine("Current full population: nativeCalls="+mixed.Entered+",commands=32410,bytesWithPrefixAllowance="+(Shared.DebugLogHelper.Bytes-bytes));
            var table=new BridgeDecisionTrace.TableImage {Rva=1,Slot=8,Count=1,Available=1,Rows=new[]{1,2,3,0}};
            mixed.PublishTable(table,"pre");long before=mixed.Pending;
            for(int i=0;i<75117;i++)mixed.PublishTable(new BridgeDecisionTrace.TableImage {Rva=1,Slot=8,Count=1,Available=1,Rows=table.Rows},"pre");
            Check(mixed.Pending==before,"75117 unchanged table references coalesce before queueing");
            mixed.FlushRegions();Check(mixed.Pending==before+1,"exact repeated-reference count fits one batch");
            Shared.DebugLogHelper.ThrowNext=true;mixed.End();
            Check(mixed.Failures==1&&mixed.Enter(attack,IntPtr.Zero,1)==null,"summary logger failure is isolated and cannot keep the ended session active");
        }
        private static void ObservedPlayerEightAccess()
        {
            int phase=5,targetPcl=31,attackerPcl=109;
            var trace=new BridgeDecisionTrace(null,_=>default,()=>1,r=>
                r==0x379D974+8*0x583C?phase:
                r==0x12EC54+8*0x583C||r==0x12EC54+0x583C?1:
                r==0x12EDA0+8*0x583C?10:r==0x12EDA0+0x583C?11:
                r==0x50EC690+20?attackerPcl:r==0x50EC690+22?targetPcl:0);
            trace.SetNative(IntPtr.Zero,int.MaxValue);trace.StartSession(902);
            var root=BridgeNativeDefinition.Sites.First(s=>s.Rva==0x3C2E0);
            foreach(int nextTargetPcl in new[]{31,1,31})
            {
                targetPcl=nextTargetPcl;if(targetPcl==31&&phase==6)attackerPcl=116;
                var parent=trace.Enter(root,IntPtr.Zero,8);
                int observed=targetPcl==1?109:0;
                var cf=BridgeNativeDefinition.Sites.First(s=>s.Rva==0xCF360);
                var child=trace.Enter(cf,IntPtr.Zero,8,1);trace.Region(8,targetPcl,attackerPcl,0,observed,observed);trace.Exit(child,true,observed==0?0:1,IntPtr.Zero);
                if(phase==6&&observed==0)
                {
                    cf=BridgeNativeDefinition.Sites.First(s=>s.Rva==0xCF400);
                    child=trace.Enter(cf,IntPtr.Zero,8,1);trace.Region(8,targetPcl,attackerPcl,1,0,0);trace.Exit(child,true,0,IntPtr.Zero);
                }
                // Replay the audited caller's observed phase write; no new Vanilla search.
                phase=observed!=0?6:5;trace.Exit(parent,true,null,IntPtr.Zero);
            }
            trace.FlushRegions();while(trace.Pending>0)trace.Drain();
            Check(trace.Entered==7&&trace.Entered==trace.Exited&&trace.Failures==0,"actual player8 access chain nesting and mode counts");
            Check(Shared.DebugLogHelper.Recent.Any(s=>s.Contains("planning-outcome")&&s.Contains("entryPhase=5,exitPhase=6"))&&Shared.DebugLogHelper.Recent.Any(s=>s.Contains("planning-outcome")&&s.Contains("entryPhase=6,exitPhase=5")),"consumed phase writes are separate from pre-consumption access snapshots");
        }
        private static void PlanningAndCommandFrames()
        {
            int phase=5,target=214724;
            var trace=new BridgeDecisionTrace(null,_=>default,()=>1,rva=>rva==0x379D974+5*0x583C?phase:rva==0x379D968+5*0x583C?target:0);
            trace.StartSession(200);
            var root=BridgeNativeDefinition.Sites.First(s=>s.Rva==0x3C2E0);
            var candidate=BridgeNativeDefinition.Sites.First(s=>s.Rva==0x2C480);
            var first=trace.Enter(root,IntPtr.Zero,5);trace.Exit(first,true,null,IntPtr.Zero);
            long captured=trace.Captures;
            for(int i=0;i<1000;i++){var quiet=trace.Enter(root,IntPtr.Zero,5);trace.Exit(quiet,true,null,IntPtr.Zero);}
            Check(trace.Captures==captured,"quiet attack phases do not capture groups/bridge repeatedly");
            var outer=trace.Enter(root,IntPtr.Zero,5);var build=trace.Enter(candidate,IntPtr.Zero,5);trace.Exit(build,true,null,IntPtr.Zero);
            Check(trace.DetailedNative&&trace.ShouldDetailCommand(false,false,false),"fresh candidate child promotes parent and preserves even unchanged following command");
            long prior=trace.Pending;for(int i=0;i<1000;i++)trace.Region(5,1,2,0,0,1);
            Check(trace.Pending==prior+1&&outer.Regions==1000,"detailed region repetition produces one branch record and exact counters");
            trace.Command("command-pre","fixture-follow",5);trace.Command("command-post","fixture-follow",5);trace.Exit(outer,true,null,IntPtr.Zero);
            phase=6;target=214725;var changed=trace.Enter(root,IntPtr.Zero,5);trace.Exit(changed,true,null,IntPtr.Zero);
            trace.FlushCosts();while(trace.Pending>0)trace.Drain();
            Check(trace.Entered==trace.Exited&&trace.Summary().Contains("overflow=0,backgroundOverflow=0"),"changed planning phases and child chain pair without loss");
            var diagnostics=new BridgeDiagnostics(null);
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(BridgeDiagnostics).GetField("Trace",flags).SetValue(diagnostics,trace);
            typeof(BridgeDiagnostics).GetField("running",flags).SetValue(diagnostics,true);
            typeof(BridgeDiagnostics).GetField("epoch",flags).SetValue(diagnostics,1L);
            var frameType=typeof(BridgeDiagnostics).GetNestedType("Frame",System.Reflection.BindingFlags.NonPublic);
            var push=typeof(BridgeDiagnostics).GetMethod("Push",flags);var pop=typeof(BridgeDiagnostics).GetMethod("Pop",flags);
            long before=trace.Captures;
            for(int i=0;i<18835;i++)
            {
                object frame=Activator.CreateInstance(frameType,true);
                frameType.GetField("Kind",flags).SetValue(frame,"unit");frameType.GetField("Id",flags).SetValue(frame,i%100+1);
                frameType.GetField("Global",flags).SetValue(frame,(uint)(i%100+1));frameType.GetField("Player",flags).SetValue(frame,i%8+1);
                frameType.GetField("X",flags).SetValue(frame,i%400);frameType.GetField("Y",flags).SetValue(frame,i%400);
                push.Invoke(diagnostics,new[]{frame});pop.Invoke(diagnostics,new object[]{"unit",i%100+1,1L});
            }
            Check(trace.Captures==before,"real diagnostic Push/Pop for 18835 unrelated unit commands adds no bridge captures");
            Check((long)typeof(BridgeDiagnostics).GetField("preCount",flags).GetValue(diagnostics)==18835&&(long)typeof(BridgeDiagnostics).GetField("postCount",flags).GetValue(diagnostics)==18835,"real command frames retain exact pre/post counts");
            Check((long)typeof(BridgeDiagnostics).GetField("errors",flags).GetValue(diagnostics)==0,"real command frames balance without callback errors");
            Console.WriteLine("PASS: actual diagnostic command frames and alternative planning parent promotion.");
        }
    }
}
