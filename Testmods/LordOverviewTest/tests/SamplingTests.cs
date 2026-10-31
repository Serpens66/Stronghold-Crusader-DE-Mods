using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using LordOverviewTest;

internal static partial class Program {
 sealed class VisualNode {
  internal VisualNode Parent;internal int Z;internal readonly List<VisualNode> Children=new List<VisualNode>();
  internal VisualNode Add(int z=0){var node=new VisualNode {Parent=this,Z=z};Children.Add(node);return node;}
 }
 static void TestSamplingAndPointer(){
  Check(OverviewPresentationPolicy.PopularitySprite(49,true)==2 && OverviewPresentationPolicy.PopularitySprite(50,true)==0 && OverviewPresentationPolicy.PopularitySprite(null,true)==1,"card popularity uses Vanilla 50 threshold and an unknown neutral mask");
  TestVanillaIconMappings();
  Check(OverviewPresentationPolicy.PopularitySprite(0,false)==0 && OverviewPresentationPolicy.PopularitySprite(null,false)==0,"list and menu popularity icons are always green");
  Check(OverviewPresentationPolicy.StatisticsTag(false)=="Ignore" && OverviewPresentationPolicy.StatisticsTag(true)=="IgnoreSelf","Vanilla world-input tags distinguish the passive statistic subtree and editing headers");
  foreach(var metric in Metrics.Builtins)Check(metric.Id==Metric.Popularity || !string.IsNullOrEmpty(OverviewPresentationPolicy.IconKey(metric.Id)),"each built-in has a verified Vanilla icon source");
  for(int count=0;count<=8;count++)for(int mask=0;mask<(1<<count);mask++){
   var source=new List<LordRow>();int living=0;for(int i=0;i<count;i++){bool alive=(mask&(1<<i))!=0;if(alive)living++;source.Add(new LordRow(i+1,alive,i*17));}
   var sorted=new List<LordRow>();var order=new OverviewState();
   foreach(bool show in new[]{true,false}){
    order.SortInto(source,sorted,show);Check(sorted.Count==(show?count:living) && sorted.Count==OverviewState.VisibleCount(source,show),"filter counts match displayed rows including empty roster");
    for(int i=0;i<sorted.Count;i++)Check((show || sorted[i].Alive) && (i==0 || sorted[i-1].Player<sorted[i].Player),"filtered rows retain global player sorting without duplicates");
    order.Select(Metric.Gold);order.SortInto(source,sorted,show);
    for(int i=1;i<sorted.Count;i++)Check(sorted[i-1].Get(Metric.Gold)>=sorted[i].Get(Metric.Gold),"filtered value sorting stays global");
    order=new OverviewState();
   }
   var preference=new GridFlow(3,3);var effective=preference.Normalize(living);Check(preference.ToString()=="3x3" && effective.Columns(living)>=1,"filter layout resolution never mutates saved grid preference");
  }
  int deferredCopies=0;var deferred=new DeferredRead(()=>deferredCopies++);
  deferred.Queue();deferred.Cancel();deferred.Run();Check(!deferred.Busy && deferredCopies==0,"queued read canceled before producer ownership");
  deferred.Queue();var copier=new Thread(deferred.Run);copier.Start();copier.Join();
  Check(deferred.Complete && deferred.Take(out var deferredError) && deferredError==null && deferredCopies==1,"producer publishes completed private storage to main thread");
  deferred.Queue(10);var pausedFallback=new PreparedRead(new object(),deferred.Run);
  Check(!deferred.Poll(11.999,pausedFallback) && deferred.Poll(12,pausedFallback) && deferredCopies==2,"pause immediately after a busy request recovers through one throttled main-thread fallback");
  Check(deferred.Take(out _),"paused fallback remains an ordinary completed job");
  using(var entered=new ManualResetEventSlim())using(var resume=new ManualResetEventSlim()){
   var inflight=new DeferredRead(()=>{entered.Set();resume.Wait();deferredCopies++;});inflight.Queue();
   var worker=new Thread(inflight.Run);worker.Start();entered.Wait();inflight.Cancel();
   Check(inflight.Busy && !inflight.Complete,"cancel cannot recycle storage owned by in-flight producer");resume.Set();worker.Join();
   Check(inflight.Complete && !inflight.Take(out _) && !inflight.Busy,"retired producer is discarded after safe completion");
  }
  var deferredFailure=new DeferredRead(()=>throw new InvalidOperationException());deferredFailure.Queue();deferredFailure.Run();
  Check(deferredFailure.Take(out var failedCopy) && failedCopy is InvalidOperationException && !deferredFailure.Busy,"failed producer frees ownership and reports outside lock");
  object gate=new object();int copies=0;var read=new PreparedRead(gate,()=>copies++);
  using(var ready=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()){
   var owner=new Thread(()=>{lock(gate){ready.Set();release.Wait(2000);}});owner.Start();ready.Wait();
   try{Check(!read.TryRead() && copies==0,"busy engine skips the copy rather than waiting for another thread");}finally{release.Set();owner.Join();}
  }
  Check(read.TryRead() && copies==1,"reader recovers after contention");
  var failure=new PreparedRead(gate,()=>throw new InvalidOperationException());
  for(int i=0;i<2;i++){try{failure.TryRead();Check(false,"expected fixture error");}catch(InvalidOperationException){Check(!Monitor.IsEntered(gate),"copy exception releases the engine monitor");}}
  PreparedRead recursive=null;bool rejected=false;recursive=new PreparedRead(gate,()=>rejected=!recursive.TryRead());Check(recursive.TryRead() && rejected,"reentrant copy cannot overwrite prepared buffers");
  for(int i=0;i<1000;i++)read.TryRead();
  long bytes=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();
  for(int i=0;i<10000;i++)read.TryRead();timer.Stop();
  // Stopwatch itself is outside the measured critical-section allocation delta below.
  bytes=GC.GetAllocatedBytesForCurrentThread();
  for(int i=0;i<10000;i++)read.TryRead();
  Check(GC.GetAllocatedBytesForCurrentThread()==bytes,"prepared nonblocking critical section allocates zero bytes after warm-up");
  Console.WriteLine("PreparedRead fixture: "+(timer.Elapsed.TotalMicroseconds/10000).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" us/call (managed copy boundary; not native/Noesis timing).");
  var front=new RawSnapshot(64);var back=new RawSnapshot(64);front.Merit[5]=1;front.History[1]=42;front.HasHistory=true;
  back.Merit[7]=999;back.Resources[1]=new ResourceSnapshot {Present=true,Defeated=true,Popularity=9000,Fear=4};back.Controlled=1;back.Eliminated=true;
  back.Prepare(false,front);Check(back.Merit[7]==0 && !back.Resources[1].Present && !back.Resources[1].Defeated && back.Controlled==0 && !back.Eliminated,"unused native slots cannot retain previous player data");
  Check(back.History[1]==42 && back.HasHistory,"not-due history uses an isolated working copy");back.History[1]=9;back.Merit[5]=2;
  Check(front.History[1]==42 && front.Merit[5]==1,"working buffer writes cannot alter the prior raw snapshot");
  back.Prepare(true,front);Check(back.History[1]==0,"due native export starts with a cleared buffer");front.Clear();Check(!front.HasHistory && front.Merit[5]==0 && front.History[1]==0,"rights/session reset erases cached sensitive raw data");
  var cadence=new HybridRefreshGate();cadence.Reset(0);cadence.Request();Check(cadence.TryBegin(0),"first explicit sample is immediate");Check(!cadence.TryBegin(0),"only one in-flight sample");cadence.End(0,true);
  for(int i=0;i<199;i++)cadence.Tick(true);Check(!cadence.TryBegin(2.1),"199 ticks alone do not trigger a sample");cadence.Tick(true);Check(cadence.TryBegin(2.1),"200 ticks plus time floor trigger a sample");cadence.End(2.1,true);
  cadence.Tick(true);Check(!cadence.TryBegin(7.0),"watchdog waits five seconds after last successful sample");Check(cadence.TryBegin(7.101),"slow running simulation gets the five-second watchdog attempt");cadence.End(7.101,true);
  for(int i=0;i<10000;i++)cadence.Tick(true);Check(!cadence.TryBegin(8),"high game speed cannot bypass two-second floor");Check(cadence.TryBegin(9.102),"high-speed demand runs once after cooldown");cadence.End(9.102,false);
  Check(!cadence.TryBegin(10),"busy engine cannot cause a GUI retry storm");Check(cadence.TryBegin(11.103),"failed attempt stays pending until a bounded retry");cadence.End(11.103,true);
  for(int i=0;i<1000;i++)cadence.Tick(false);Check(!cadence.TryBegin(100),"stable pause has no periodic sampling");
  cadence.Request();Check(cadence.TryBegin(100),"new selection can request data while paused");cadence.Request();cadence.End(100,true);Check(!cadence.TryBegin(101) && cadence.TryBegin(102.001),"requests during capture coalesce and retain the cooldown");cadence.End(102.001,true);
  cadence.Request();cadence.Cancel();Check(!cadence.TryBegin(200),"closing clears pending capture and tick demand");cadence.Reset(200);cadence.Request();Check(cadence.TryBegin(200),"new session does not inherit old capture timestamps");cadence.End(200,true);
  var root=new VisualNode();var background=root.Add();var overlay=root.Add();var foreground=root.Add();var portrait=overlay.Add();var nested=foreground.Add();
  Func<VisualNode,VisualNode> parent=n=>n.Parent;Func<VisualNode,int> z=n=>n.Z;Func<VisualNode,VisualNode,int> index=(p,c)=>p.Children.IndexOf(c);
  Check(!HoverOcclusion.Blocks<VisualNode>(null,overlay,parent,z,index),"world with no visual hit allows passive hover");
  Check(!HoverOcclusion.Blocks(root,overlay,parent,z,index) && !HoverOcclusion.Blocks(background,overlay,parent,z,index),"shared root and lower HUD branch are backgrounds");
  Check(!HoverOcclusion.Blocks(portrait,overlay,parent,z,index),"own visual branch permits hover");Check(HoverOcclusion.Blocks(nested,overlay,parent,z,index),"later foreground branch blocks hover");
  background.Z=1;Check(HoverOcclusion.Blocks(background,overlay,parent,z,index),"higher z-index wins over earlier sibling order");foreground.Z=-1;Check(!HoverOcclusion.Blocks(foreground,overlay,parent,z,index),"lower z-index remains behind a later sibling");
  Check(HoverOcclusion.Blocks(new VisualNode(),overlay,parent,z,index),"unrelated unknown visual tree stays blocked");
  var selected=new ColumnSelection();var identity=selected.Columns;selected.Load(Metrics.Serialize(identity));Check(ReferenceEquals(identity,selected.Columns),"equivalent setting delivery cannot rebuild the realized columns");
  Check(new GridFlow(4,2).SameShape(new GridFlow(4,2)) && !new GridFlow(4,2).SameShape(new GridFlow(2,4)),"grid structure compares shape rather than temporary object identity");
  var hover=new OverviewHoverState();Check(hover.Update("portrait/1",1,0,60) && hover.Update("portrait/2",1,0,60),"portrait first entry and direct switches have zero intentional delay");
  for(int x=-10;x<120;x+=10)for(int y=-10;y<120;y+=10){var bubble=PassiveBubblePlacement.Place(new MenuRect(x,y,28,28),60,30,120,120);Check(bubble.X>=0 && bubble.Y>=0 && bubble.Right<=120 && bubble.Bottom<=120,"passive tooltip stays in its attached viewport");}
  for(int count=1;count<=40;count++)for(int columns=1;columns<=8;columns++){
   int rows=(count+columns-1)/columns;var cells=new MenuRect[count];for(int i=0;i<count;i++)cells[i]=new MenuRect(i%columns*32,i/columns*32,32,32);
   for(int source=0;source<count;source++)for(int to=0;to<count;to++){
    Check(WrappedDropGeometry.TryTarget(cells[to].X+1,cells[to].Y+16,columns*32,rows*32,cells,source,out int target,out var marker),"all realized rows accept drops");
    Check(target==(to>source?to-1:to) && marker.Y==cells[to].Y && marker.Height==32,"wrapped insertion uses global sequence and row geometry");
   }
   Check(!WrappedDropGeometry.TryTarget(-1,0,columns*32,rows*32,cells,0,out _,out _),"drop outside wrapped host cancels");
   Check(WrappedDropGeometry.TryTarget(columns*32-1,rows*32-1,columns*32,rows*32,cells,0,out int last,out _) && last==count-1,"trailing space on last row appends");
  }
 }
}
