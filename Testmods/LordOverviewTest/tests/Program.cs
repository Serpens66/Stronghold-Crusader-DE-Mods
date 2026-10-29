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
  TestColumns();TestInformation();TestValues();TestMenuAndGestures();
  var gate=new RefreshGate();for(int i=0;i<39;i++)Check(!gate.Tick(true),"no query on intermediate ticks");Check(gate.Tick(true),"query at tick forty");
  for(int i=0;i<120;i++)Check(!gate.Tick(false),"hidden never samples");
  for(int i=0;i<39;i++)Check(!gate.Tick(true),"hidden resets interval");Check(gate.Tick(true),"one full interval after hidden");
  for(int i=0;i<400;i++)Check(gate.Tick(true)==(i%40==39),"exact cadence without drift");
  Console.WriteLine("PASS: "+checks+" production sorting, selection, information policy, statistic arithmetic and 40-tick cadence checks.");
 }
 static void TestColumns(){
  var selection=new ColumnSelection();Check(selection.Columns.SequenceEqual(Metrics.Defaults),"default four columns");
  Check(selection.Toggle(Metric.LordsKilled) && selection.Columns.Length==5,"fifth column allowed");
  Check(!selection.Toggle(Metric.GoldAcquired) && selection.Columns.Length==5,"sixth column rejected");
  Check(selection.MoveTo(Metric.LordsKilled,3) && selection.Columns[3]==Metric.LordsKilled,"reorder selected value");
  var sort=new OverviewState();sort.Select(Metric.LordsKilled);sort.Reconcile(selection.Columns);Check(sort.Column==Metric.LordsKilled,"sort follows metric after reorder");
  selection.Toggle(Metric.LordsKilled);sort.Reconcile(selection.Columns);Check(sort.Column==null && !sort.Descending,"removed sort resets ID ascending");
  foreach(var metric in new[]{Metric.Fear,Metric.Popularity,Metric.Army})Check(selection.Toggle(metric),"deselect column");
  Check(!selection.Toggle(Metric.Gold) && selection.Columns.Length==1,"last metric cannot be removed");
  Check(!selection.MoveTo(Metric.Gold,-1) && !selection.MoveTo(Metric.Gold,1),"reorder bounds");
  foreach(var metric in Metrics.All){selection.Load(metric.Id.ToString());Check(selection.Columns.Single()==metric.Id,"every metric persists by identifier");}
  selection.Load("TroopsLost,Gold,Popularity");var saved=Metrics.Serialize(selection.Columns);selection.Load(saved);Check(Metrics.Serialize(selection.Columns)==saved,"order round trip");
  selection.Load("0,Unknown");Check(selection.Columns.SequenceEqual(Metrics.Defaults),"invalid persisted identifiers safe default");
  selection.Load("Gold,Gold,Army");Check(selection.Columns.SequenceEqual(new[]{Metric.Gold,Metric.Army}),"duplicate identifiers normalized");
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
  Check(Metrics.All.Where(d=>d.Group=="Military").Select(d=>d.Id).SequenceEqual(military),"current and cumulative military values grouped together");
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
  Check(!HeaderDropGeometry.TryTarget(45,10,25,widths,0,out destination,out boundary),"player-number header is not a drop target");
  Check(HeaderDropGeometry.TryTarget(46,10,25,widths,2,out destination,out boundary) && destination==0 && boundary==46,"last metric moves to first boundary");
  Check(HeaderDropGeometry.TryTarget(167,10,25,widths,0,out destination,out boundary) && destination==2 && boundary==168,"first metric moves to last boundary");
  Check(!HeaderDropGeometry.TryTarget(168,10,25,widths,0,out destination,out boundary),"gear area excluded from target");
  Check(!HeaderDropGeometry.TryTarget(80,25,25,widths,0,out destination,out boundary) && !HeaderDropGeometry.TryTarget(80,-1,25,widths,0,out destination,out boundary),"vertical header bounds enforced");
  Check(!HeaderDropGeometry.TryTarget(80,10,25,new double[]{0},0,out destination,out boundary) && !HeaderDropGeometry.TryTarget(double.NaN,10,25,widths,0,out destination,out boundary),"unarranged or invalid pointer geometry cancels");
 }
}
