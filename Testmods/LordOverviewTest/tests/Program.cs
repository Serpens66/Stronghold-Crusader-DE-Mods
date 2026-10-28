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
  state.Select(0);Check(string.Join(",",state.Sort(rows).Select(r=>r.Player))=="2,1,4,3","descending, stable ties, unknown last");
  state.Select(0);Check(string.Join(",",state.Sort(rows).Select(r=>r.Player))=="1,4,2,3","ascending, unknown still last");
  state.Select(1);Check(string.Join(",",state.Sort(rows).Select(r=>r.Player))=="4,1,2,3","new metric descending");
  var updated=new[]{new LordRow(1,true,20,50),b,c,d};Check(state.Sort(updated)[0].Player==1 && state.Descending && state.Column==1,"live data preserves selected sort");
  Check(ReferenceEquals(state.Sort(rows).Single(r=>r.Player==2),b),"sorting preserves complete player identity");
  state.Select(-1);state.Select(-1);Check(state.Sort(rows)[0].Player==1,"ID ascending restored");
  var values=new long?[]{1,2,3,4,5};var five=new LordRow(8,true,values);values[4]=0;
  state.Select(4);Check(five.Values[4]==5 && state.Sort(new[]{five})[0].Player==8,"five immutable metric slots");
  var gate=new RefreshGate();for(int i=0;i<39;i++)Check(!gate.Tick(true),"no query on intermediate ticks");Check(gate.Tick(true),"query at tick forty");
  for(int i=0;i<120;i++)Check(!gate.Tick(false),"hidden never samples");
  for(int i=0;i<39;i++)Check(!gate.Tick(true),"hidden resets interval");Check(gate.Tick(true),"one full interval after hidden");
  for(int i=0;i<400;i++)Check(gate.Tick(true)==(i%40==39),"exact cadence without drift");
  Console.WriteLine("PASS: "+checks+" sorting, identity and 40-tick cadence checks.");
 }
}
