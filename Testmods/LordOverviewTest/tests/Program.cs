using System;
using System.Linq;
using LordOverviewTest;
internal static class Program {
 static int checks;
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static void Main(){
  var a=new LordRow(1,true,20,8);var b=new LordRow(2,true,100,8);var c=new LordRow(3,false,null,2);var d=new LordRow(4,true,20,9);
  var rows=new[]{c,d,b,a};var state=new OverviewState();
  Check(string.Join(",",state.Sort(rows).Select(r=>r.Player))=="1,2,3,4","initial ID sort");
  state.Select(Metric.Gold);Check(string.Join(",",state.Sort(rows).Select(r=>r.Player))=="2,1,4,3","descending, stable ties, unknown last");
  state.Select(Metric.Gold);Check(string.Join(",",state.Sort(rows).Select(r=>r.Player))=="1,4,2,3","ascending, unknown still last");
  state.Select(Metric.Army);Check(string.Join(",",state.Sort(rows).Select(r=>r.Player))=="4,1,2,3","new metric descending");
  var updated=new[]{new LordRow(1,true,20,50),b,c,d};Check(state.Sort(updated)[0].Player==1 && state.Descending && state.Column==Metric.Army,"live data preserves selected sort");
  Check(ReferenceEquals(state.Sort(rows).Single(r=>r.Player==2),b),"sorting preserves complete player identity");
  state.Select(null);state.Select(null);Check(state.Sort(rows)[0].Player==1,"ID ascending restored");
  var values=new long?[]{1,2,3,4,5};var five=new LordRow(8,true,values);values[4]=0;
  state.Select(Metric.GoldAcquired);Check(five.Values[4]==5 && state.Sort(new[]{five})[0].Player==8,"immutable metric snapshot");
  TestColumns();TestExternalColumns();TestInformation();TestValues();TestMenuAndGestures();TestPassiveHover();TestLayoutsAndActions();TestMenuWindowAndCrown();
  var gate=new RefreshGate();for(int i=0;i<39;i++)Check(!gate.Tick(true),"no query on intermediate ticks");Check(gate.Tick(true),"query at tick forty");
  for(int i=0;i<120;i++)Check(!gate.Tick(false),"hidden never samples");
  for(int i=0;i<39;i++)Check(!gate.Tick(true),"hidden resets interval");Check(gate.Tick(true),"one full interval after hidden");
  for(int i=0;i<400;i++)Check(gate.Tick(true)==(i%40==39),"exact cadence without drift");
  Console.WriteLine("PASS: "+checks+" production layout, UI lifetime, sample isolation, sorting, information and cadence checks.");
 }
 static void TestColumns(){
  var selection=new ColumnSelection();Check(selection.Columns.SequenceEqual(Metrics.Defaults),"default four columns");
  Check(selection.Toggle(Metric.LordsKilled) && selection.Columns.Length==5,"fifth column allowed");
  Check(selection.Toggle(Metric.GoldAcquired) && selection.Columns.Length==6,"sixth column allowed");selection.Toggle(Metric.GoldAcquired);
  Check(selection.MoveTo(Metric.LordsKilled,3) && selection.Columns[3]==Metric.LordsKilled,"reorder selected value");
  var sort=new OverviewState();sort.Select(Metric.LordsKilled);sort.Reconcile(selection.Columns);Check(sort.Column==Metric.LordsKilled,"sort follows metric after reorder");
  selection.Toggle(Metric.LordsKilled);sort.Reconcile(selection.Columns);Check(sort.Column==null && !sort.Descending,"removed sort resets ID ascending");
  foreach(var metric in new[]{Metric.Fear,Metric.Popularity,Metric.Army})Check(selection.Toggle(metric),"deselect column");
  Check(!selection.Toggle(Metric.Gold) && selection.Columns.Length==1,"last metric cannot be removed");
  Check(!selection.MoveTo(Metric.Gold,-1) && !selection.MoveTo(Metric.Gold,1),"reorder bounds");
  foreach(var metric in Metrics.Builtins){selection.Load(metric.Id.ToString());Check(selection.Columns.Single()==metric.Id,"every metric persists by identifier");}
  selection.Load("TroopsLost,Gold,Popularity");var saved=Metrics.Serialize(selection.Columns);selection.Load(saved);Check(Metrics.Serialize(selection.Columns)==saved,"order round trip");
  selection.Load("0,Unknown");Check(selection.Columns.SequenceEqual(Metrics.Defaults),"invalid persisted identifiers safe default");
  selection.Load("Gold,Gold,Army");Check(selection.Columns.SequenceEqual(new[]{Metric.Gold,Metric.Army}),"duplicate identifiers normalized");
 }
 static void TestExternalColumns(){
  var selection=new ColumnSelection();selection.Load(Metrics.Serialize(Metrics.Builtins.Select(d=>d.Id)));Check(selection.Columns.Length==20,"all builtin columns permitted");
  selection.Load("Gold,foreign.mod/units,Army");var missing=selection.Columns[1];Check(Metrics.Key(missing)=="foreign.mod/units","missing provider selection retained");
  var id=Metrics.RegisterExternal("foreign.mod/units","Military",v=>v.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+" m");Check(id==missing,"late provider recovers the same identity");
  Check(Metrics.Parse(Metrics.Serialize(selection.Columns)).SequenceEqual(selection.Columns),"qualified keys and order round trip without provider");
  var a=new LordRow(1,true,1);var b=new LordRow(2,true,2);var c=new LordRow(3,true,3);a.SetExtra(id,1.25m);b.SetExtra(id,-3.1m);c.SetExtra(id,null);
  var sort=new OverviewState();sort.Select(id);Check(sort.Sort(new[]{a,b,c})[0].Player==1,"decimal descending sort uses numbers, not units");sort.Select(id);Check(sort.Sort(new[]{a,b,c}).Select(r=>r.Player).SequenceEqual(new[]{2,1,3}),"decimal ascending keeps unknown last");
  Check(Metrics.Get(id).Format(a.Get(id))=="1.25 m","decimal formatting retains optional units");
  var local=new ColumnSelection();local.Load("Gold,foreign.mod/units");local.MoveTo(id,0);Check(Metrics.Serialize(local.Columns)=="foreign.mod/units,Gold","external ID reorder is stable");
 }
 static void TestPassiveHover(){
  var hover=new OverviewHoverState();
  Check(!hover.Update("player/1",1,.4,60),"passive portrait respects initial hover delay");
  Check(!hover.Update("player/1",1.2,.4,60),"pointer staying within a portrait does not open too early");
  Check(hover.Update("player/1",1.5,.4,60),"passive portrait opens after delay");
  for(int i=0;i<40;i++)Check(hover.Update("player/1",2+i,.4,60),"data refresh with stable identity does not restart hover");
  Check(!hover.Update("metric/Fear",43,.4,60),"moving to a header starts a new delay");
  Check(hover.Update("metric/Fear",43.5,.4,60),"header tooltip becomes visible");
  Check(!hover.Update(null,44,.4,60),"values and empty table areas close hover");
  Check(!hover.Update("player/2",45,.4,60),"same screen coordinates with another row reset delay");
  Check(hover.Update("player/2",45.5,.4,60),"replacement hover target opens");
  Check(!hover.Update("player/2",106,.4,60) && !hover.Update("player/2",107,.4,60),"sixty-second duration does not immediately reopen");
  hover.Update(null,108,.4,60);hover.Update("player/2",109,.4,60);Check(hover.Update("player/2",109.5,.4,60),"leaving and reentering permits a new tooltip");
  hover.Reset();Check(!hover.Update("player/2",110,.4,60),"HUD/menu/focus cleanup invalidates the previous hover");
  Check(!hover.Update("player/2",.1,.4,60),"clock reset cannot reuse prior timer");
  Check(TableInteraction.CanEdit(true,true,5,5,2,2),"open menu permits header actions");
  Check(!TableInteraction.CanEdit(false,true,5,5,2,2),"closed menu cannot sort or reorder");
  Check(!TableInteraction.CanEdit(true,false,5,5,2,2) && !TableInteraction.CanEdit(true,true,6,5,2,2),"hidden or replaced HUD rejects pending action");
  Check(!TableInteraction.CanEdit(true,true,5,5,4,2),"closing and reopening menu rejects stale queued sort");
 }
 static void TestInformation(){
  foreach(var metric in Metrics.All){
   bool baseline=metric.Id==Metric.Gold || metric.Id==Metric.Army;
   Check(InformationPolicy.CanRead(metric.Id,false,false,false,false,false,false)==baseline,"enemy restrictions default");
   Check(InformationPolicy.CanRead(metric.Id,false,false,true,false,false,false),"eliminated viewer sees all with spectator flag off and host permission off");
   Check(InformationPolicy.CanRead(metric.Id,false,true,false,false,false,false),"confirmed spectator all");
   Check(InformationPolicy.CanRead(metric.Id,true,false,false,false,false,false),"editor all");
   Check(InformationPolicy.CanRead(metric.Id,false,false,false,true,false,false),"own all");
   Check(InformationPolicy.CanRead(metric.Id,false,false,false,false,true,false),"allied all");
   Check(InformationPolicy.CanRead(metric.Id,false,false,false,false,false,true),"confirmed host permission all");
  }
  Check(!InformationPolicy.IsEliminated(false,false,false),"unresolved lord does not imply elimination");
  Check(InformationPolicy.IsEliminated(true,false,false),"official defeat loaded from save");
  Check(InformationPolicy.IsEliminated(false,true,false),"Vanilla merit defeat");
  Check(InformationPolicy.IsEliminated(false,false,true),"identified dead local lord before spectator promotion");
  Check(!InformationPolicy.HasDeadLordEvidence(true,true,false,false),"uninitialized lord is not an eliminated viewer");
  Check(!InformationPolicy.HasDeadLordEvidence(false,false,false,false),"healthy paused lord does not grant information");
  Check(InformationPolicy.HasDeadLordEvidence(false,true,false,false) && InformationPolicy.HasDeadLordEvidence(false,false,true,false),"health and projectile deaths qualify");
 }
 static void TestValues(){
  Check(StatisticValues.Popularity(0)==0 && StatisticValues.Popularity(9999)==99 && StatisticValues.Popularity(10000)==100,"Vanilla popularity truncates hundredths");
  Check(!StatisticValues.Popularity(10001).HasValue,"invalid popularity unavailable");
  for(int fear=-5;fear<=5;fear++)Check(Metrics.Get(Metric.Fear).Format(StatisticValues.Fear(fear))==(fear<0?fear.ToString():"+"+fear),"signed two-character fear");
  Check(!StatisticValues.Fear(6).HasValue && !StatisticValues.Fear(-6).HasValue,"unknown fear not clamped or inverted");
  var matrix=new int[81];matrix[2*9+1]=12;matrix[1*9+2]=7;matrix[2*9+2]=99;matrix[2]=3;matrix[2*9]=44;
  Check(StatisticValues.Kills(matrix,2,false)==12,"kills use outgoing row excluding self and environment");
  Check(StatisticValues.Kills(matrix,2,true)==10,"losses use incoming column including environment");
  matrix[2*9+3]=int.MaxValue;Check(StatisticValues.Kills(matrix,2,false)==(long)int.MaxValue+12,"wide sum avoids overflow");
  Check(!StatisticValues.Kills(new int[80],2,true).HasValue && !StatisticValues.At(new int[2],8).HasValue,"incomplete native snapshot unavailable");
 }
 static void TestMenuAndGestures(){
  Check(Metrics.All.Where(d=>d.Group=="PopulationMood").Select(d=>d.Id).OrderBy(x=>x).SequenceEqual(new[]{Metric.Popularity,Metric.Fear,Metric.MaxPopulation}),"population and mood are thematic, not a default-values group");
  var military=new[]{Metric.Army,Metric.TroopsProduced,Metric.TroopsKilled,Metric.TroopsLost,Metric.BuildingsDestroyed,Metric.BuildingsLost,Metric.LordsKilled};
  Check(Metrics.Builtins.Where(d=>d.Group=="Military").Select(d=>d.Id).SequenceEqual(military),"current and cumulative military values grouped together");
  Check(Metrics.All.All(d=>Metrics.Groups.Contains(d.Group)) && Metrics.All.Count(d=>d.Group=="Economy")==10,"every metric belongs to one of three complete themes");
  var menu=new ColumnMenuState();menu.Select("Military",42);menu.Remember(17);var columns=new ColumnSelection();columns.Toggle(Metric.WoodProduced);
  Check(menu.Group=="Military" && menu.Offset==17,"checkbox changes preserve category and offset");
  menu.Select("Economy",17);Check(menu.Offset==42,"each category restores its offset");
  menu.Select("Military",42);Check(menu.Offset==17,"reopening military retains offset");
  Check(!menu.Select("Current",0) && menu.Group=="Military","removed category cannot become active");
  menu.Reset();Check(menu.Group=="Economy" && menu.Offset==0,"new session resets navigation only");
  var gesture=new HeaderGesture();Metric metric=Metric.Gold;int index=-1;
  gesture.Begin(Metric.Gold,20,5,10,2);gesture.Update(25,5,1);
  Check(!gesture.Dragging && gesture.End(true,10,2,out metric,out index)==HeaderGestureResult.Sort && metric==Metric.Gold,"short click sorts");
  gesture.Begin(Metric.Gold,20,5,10,2);gesture.Update(26,5,3);
  Check(gesture.Dragging && gesture.End(true,10,2,out metric,out index)==HeaderGestureResult.Reorder && index==3,"six logical units starts reorder and suppresses sort");
  Check(columns.MoveTo(metric,index) && columns.Columns[3]==Metric.Gold,"drop commits one metric by ID");
  Check(!columns.MoveTo(metric,index),"same destination produces no extra persistence change");
  gesture.Begin(Metric.Army,0,0,10,2);gesture.Update(4,5,-1);Check(gesture.Dragging && gesture.End(false,10,2,out metric,out index)==HeaderGestureResult.Cancel,"drop outside header cancels");
  gesture.Begin(Metric.Army,0,0,10,2);Check(gesture.End(false,10,2,out metric,out index)==HeaderGestureResult.Cancel,"release outside original short-click target does not sort");
  gesture.Begin(Metric.Army,0,0,10,2);gesture.Update(100,0,0);Check(gesture.End(true,11,2,out metric,out index)==HeaderGestureResult.Cancel,"stale mission rejects drop");
  gesture.Begin(Metric.Army,0,0,10,2);gesture.Update(100,0,0);Check(gesture.End(true,10,3,out metric,out index)==HeaderGestureResult.Cancel,"HUD generation rejects drop");
  gesture.Begin(Metric.Army,0,0,10,2);gesture.Update(100,0,0);gesture.Cancel();Check(!gesture.Active && gesture.End(true,10,2,out metric,out index)==HeaderGestureResult.Cancel,"escape/focus/capture cancellation cannot commit later");
  var sort=new OverviewState();sort.Select(Metric.Gold);sort.Reconcile(columns.Columns);Check(sort.Column==Metric.Gold,"header reorder preserves selected statistic sort");
  double boundary;int destination;var widths=new double[]{50,40,32};
  Check(!HeaderDropGeometry.TryTarget(45,10,25,46,widths,0,out destination,out boundary),"player-number header is not a drop target");
  Check(HeaderDropGeometry.TryTarget(46,10,25,46,widths,2,out destination,out boundary) && destination==0 && boundary==46,"last metric moves to first boundary");
  Check(HeaderDropGeometry.TryTarget(167,10,25,46,widths,0,out destination,out boundary) && destination==2 && boundary==168,"first metric moves to last boundary");
  Check(!HeaderDropGeometry.TryTarget(168,10,25,46,widths,0,out destination,out boundary),"outside realized value columns excluded from target");
  Check(!HeaderDropGeometry.TryTarget(80,25,25,46,widths,0,out destination,out boundary) && !HeaderDropGeometry.TryTarget(80,-1,25,46,widths,0,out destination,out boundary),"vertical header bounds enforced");
  foreach(double start in new[]{0d,11.25d,110d}){
   Check(HeaderDropGeometry.TryTarget(start+1,3,20,start,new[]{32d,51.75d,110d},2,out destination,out boundary) && destination==0 && boundary==start,"drag uses actual identity widths, including fractional layouts");
   Check(HeaderDropGeometry.TryTarget(start+190,3,20,start,new[]{32d,51.75d,110d},0,out destination,out boundary) && destination==2,"variable metric widths retain insertion target");
  }
  Check(!HeaderDropGeometry.TryTarget(80,10,25,46,new double[]{0},0,out destination,out boundary) && !HeaderDropGeometry.TryTarget(double.NaN,10,25,46,widths,0,out destination,out boundary),"unarranged or invalid pointer geometry cancels");
 }
 static void TestLayoutsAndActions(){
  foreach(int count in Enumerable.Range(1,45))foreach(bool byRows in new[]{false,true})foreach(int size in Enumerable.Range(1,count+2)){
   var flow=GridFlow.Parse((byRows?"Rows:":"Columns:")+size,2);var positions=new System.Collections.Generic.HashSet<string>();int lastRow=-1,lastColumn=-1;
   for(int i=0;i<count;i++){
    flow.Position(i,count,out int row,out int column);
    Check(row>=0 && row<flow.Rows(count) && column>=0 && column<flow.Columns(count),"grid stays inside its arrangement");
    Check(positions.Add(row+","+column),"every chosen item occupies its own cell");
    Check(row>lastRow || row==lastRow && column>lastColumn,"grid follows global row-major sort order");lastRow=row;lastColumn=column;
   }
   Check(positions.Count==count,"no item is omitted by a narrow grid");
   Check(GridFlow.Parse(flow.ToString(),2).ToString()==flow.ToString(),"axis preference round trip");
  }
  for(int count=1;count<=8;count++){
   var seen=new System.Collections.Generic.HashSet<string>();
   for(int i=0;i<count;i++){int pane=OverviewLayout.Pane(i,count),row=OverviewLayout.PaneRow(i,count);
    Check(pane>=0 && pane<=1 && row<(count+1)/2 && seen.Add(pane+","+row),"split lists preserve every globally sorted lord exactly once");}
  }
  Check(GridFlow.Parse("Rows:0",3).ToString()=="Columns:3" && GridFlow.Parse("Columns:bad",2).Columns(8)==2,"malformed grids use safe defaults");
  Check(new GridFlow(0,int.MaxValue).Columns(8)==8 && new GridFlow(int.MaxValue,0).Rows(8)==8,"large preferences do not allocate empty cells");
  Check(OverviewLayout.ParseDesign("Cards")==OverviewDesign.Cards && OverviewLayout.ParseDesign("bad")==OverviewDesign.List,"design parsing preserves the list default");
  var palette=Enumerable.Range(0,9).ToArray();
  foreach(int player in Enumerable.Range(1,8))Check(OverviewLayout.ColorIndex(player,p=>9-p,palette)==9-player,"loaded-player remapping is applied before the palette lookup");
  Check(OverviewLayout.ColorIndex(0,p=>p,palette)==0 && OverviewLayout.ColorIndex(1,p=>-1,palette)==0 && OverviewLayout.ColorIndex(8,p=>99,palette)==0,"invalid color mappings fail safely");
  var queue=new System.Collections.Generic.Queue<Action>();object view=new object();long session=1;int generation=1,epoch=1;bool visible=true,enabled=true,menu=true;
  Func<UiActionContext> read=()=>new UiActionContext(session,generation,epoch,view,visible,enabled,menu);
  Action drain=()=>{while(queue.Count>0)queue.Dequeue()();};int calls=0;
  var control=UiActions.Bind(()=>calls++,read,queue.Enqueue,false);var checkbox=UiActions.Bind(()=>calls++,read,queue.Enqueue,true);
  control();checkbox();drain();Check(calls==2,"initial real action delegates run");
  generation++;menu=false;epoch++;visible=false;visible=true;
  control();drain();Check(calls==3,"same reusable handler works after overview close and reopen");
  menu=true;epoch++;checkbox();drain();Check(calls==4,"same checkbox handler works after overview close and reopen");
  checkbox();menu=false;epoch++;drain();Check(calls==4,"closed menu rejects a queued checkbox change");
  menu=true;checkbox();menu=false;epoch++;menu=true;epoch++;drain();Check(calls==4,"reopened menu cannot revive an earlier action");
  control();generation++;drain();Check(calls==4,"obsolete overview generation rejects queued UI action");
  control();view=new object();drain();Check(calls==4,"HUD replacement rejects queued work");
  control();session++;drain();Check(calls==4,"mission replacement rejects queued work");
  control();visible=false;drain();Check(calls==4,"hidden view rejects queued work");
  control();visible=true;drain();Check(calls==4,"a click issued while hidden cannot revive on reopening");
  enabled=false;control();enabled=true;drain();Check(calls==4,"disabled click cannot revive after enabling");
  control();drain();Check(calls==5,"retained controls recover after all invalidations");
  int samples=0;var refresh=new UiRefreshQueue(read,queue.Enqueue,()=>samples++);
  for(int i=0;i<100;i++)refresh.Request();Check(queue.Count==1,"explicit refreshes coalesce without simulation ticks");drain();Check(samples==1,"paused selection refresh runs once");
  refresh.Request();generation++;refresh.Request();drain();Check(samples==2,"new generation refresh replaces old queued request");
  refresh.Request();refresh.Cancel();drain();Check(samples==2,"detaching cancels explicit refresh");
  refresh.Request();visible=false;drain();Check(samples==2,"hidden overview never samples from pending UI work");
  visible=true;refresh.Request();drain();Check(samples==3,"explicit refresh recovers after hidden state");
  int rights=1,published=0,extended=0;
  Func<int> rightsNow=()=>rights;Func<string> capture=()=>"snapshot";
  Func<string,UiActionContext,int,bool> extend=(sample,stamp,version)=>{extended++;return true;};
  Action<string> publish=sample=>{Check(sample=="snapshot","only the actual sample is published");published++;};
  Check(UiSamples.TryRead(read,rightsNow,capture,extend,publish) && published==1 && extended==1,"valid sample passes both stages");
  Check(!UiSamples.TryRead(read,rightsNow,()=>{session++;return "snapshot";},extend,publish) && extended==1 && published==1,"session replacement during native capture prevents provider calls and publication");
  Check(!UiSamples.TryRead(read,rightsNow,()=>{rights++;return "snapshot";},extend,publish) && extended==1 && published==1,"rights revoked during capture prevent provider requests");
  Check(!UiSamples.TryRead(read,rightsNow,capture,(sample,stamp,version)=>{rights++;return true;},publish) && published==1,"rights revoked by a provider prevent publication");
  Check(!UiSamples.TryRead(read,rightsNow,capture,(sample,stamp,version)=>{generation++;return true;},publish) && published==1,"provider-triggered HUD generation change prevents publication");
  Check(!UiSamples.TryRead(read,rightsNow,capture,(sample,stamp,version)=>false,publish) && published==1,"aborted extension batch is never published");
  visible=false;Check(!UiSamples.TryRead(read,rightsNow,()=>{throw new Exception("must not sample hidden view");},extend,publish),"hidden UI does not enter capture");
  visible=true;Check(UiSamples.TryRead(read,rightsNow,capture,extend,publish) && published==2,"fresh samples recover after every invalidation");
 }

 static void TestMenuWindowAndCrown(){
  var eight=GridFlow.Choices(8,new GridFlow(4,2)).Select(g=>g.ToString()).ToArray();
  Check(eight.SequenceEqual(new[]{"1x8","2x4","3x3","4x2","8x1"}),"eight-lord combinations include both orientations and a square");
  Check(GridFlow.Parse("Rows:3",2).Normalize(8).ToString()=="3x3","saved row preference migrates into a pair");
  Check(GridFlow.Parse("Columns:2",2).Normalize(8).ToString()=="4x2","saved column preference migrates into a pair");
  Check(new GridFlow(2,3).Normalize(10).ToString()=="4x3","adding values grows rows without losing the column preference");
  foreach(int count in Enumerable.Range(1,45))foreach(var grid in GridFlow.Choices(count,new GridFlow(2,3))){
   var cells=new System.Collections.Generic.HashSet<string>();int previousRow=-1,previousColumn=-1;
   for(int i=0;i<count;i++){grid.Position(i,count,out int row,out int col);Check(cells.Add(row+","+col) && row<grid.Rows(count) && col<grid.Columns(count),"combination shows every value exactly once");Check(row>previousRow || row==previousRow && col>previousColumn,"combination keeps global row-major order");previousRow=row;previousColumn=col;}
   Check(GridFlow.Parse(grid.ToString(),2).ToString()==grid.ToString(),"paired grid persistence round trip");
  }
  var position=new MenuWindowState();var crown=new MenuRect(0,900,32,32);var stats=new MenuRect(40,700,600,380);
  var initial=position.Arrange(1920,1080,crown,stats);Check(initial.Width==520 && initial.Height==420 && !initial.Overlaps(stats),"initial compact menu avoids statistics when space is available");
  position.BeginDrag(initial.X+10,initial.Y+10);position.MoveDrag(-10000,-10000,1920,1080);Check(position.Bounds.X==0 && position.Bounds.Y==0,"drag cannot escape top and left");
  position.MoveDrag(10000,10000,1920,1080);Check(position.Bounds.Right<=1920 && position.Bounds.Bottom<=1080,"drag cannot escape right and bottom");position.EndDrag();
  var saved=position.Bounds;var reopened=position.Arrange(1920,1080,new MenuRect(0,0,32,32),stats);Check(reopened.X==saved.X && reopened.Y==saved.Y,"same-session reopen preserves menu position");
  var small=position.Arrange(320,180,crown,stats);Check(small.Width==304 && small.Height==164 && small.Right<=320 && small.Bottom<=180,"resolution change bounds the entire window, including its header");
  position.BeginDrag(10,10);position.EndDrag();var stopped=position.Bounds;position.MoveDrag(99,99,320,180);Check(position.Bounds.X==stopped.X && position.Bounds.Y==stopped.Y,"capture loss or close cancels drag");
  position.Reset();Check(!position.HasPosition && !position.Dragging,"new mission clears position and capture state");
  foreach(int width in new[]{64,320,1280,1920,3840})foreach(int height in new[]{48,180,720,1080,2160}){
   var rect=position.Arrange(width,height,crown,stats);Check(rect.X>=0 && rect.Y>=0 && rect.Right<=width && rect.Bottom<=height,"menu stays in bounds at every supported viewport size");
  }
  object view=new object();var context=new UiActionContext(1,1,1,view,true,true,false);var press=new CrownPress();
  Check(!press.Begin(context,false) && !press.Active,"left crown input is not intercepted by the right-click gesture");
  Check(press.Begin(context,true) && press.End(context,true,true),"right press and release on the current crown opens settings");
  press.Begin(context,true);Check(!press.End(context,false,true),"releasing outside the crown cancels settings");
  press.Begin(context,true);Check(!press.End(new UiActionContext(2,1,1,view,true,true,false),true,true),"session change rejects a pending crown press");
  press.Begin(context,true);Check(!press.End(new UiActionContext(1,2,1,view,true,true,false),true,true),"HUD generation change rejects a pending crown press");
  press.Begin(context,true);press.Cancel();Check(!press.End(context,true,true),"focus or capture loss cancels crown action");
  Check(!press.Begin(new UiActionContext(1,1,1,view,false,true,false),true),"hidden or obsolete crown cannot open settings");
  bool overview=false,menu=false;int opens=0;
  Action show=()=>{overview=true;opens++;};Action open=()=>menu=true;Action close=()=>menu=false;
  CrownSettingsAction.Run(overview,menu,show,open,close);Check(overview && menu && opens==1,"right click opens both overview and settings from closed state");
  CrownSettingsAction.Run(overview,menu,show,open,close);Check(overview && !menu && opens==1,"second right click closes only settings");
  CrownSettingsAction.Run(overview,menu,show,open,close);Check(overview && menu && opens==1,"right click reopens menu without toggling statistics");
 }
}
