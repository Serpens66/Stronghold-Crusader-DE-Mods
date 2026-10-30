using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;

namespace LordOverviewTest {
 internal enum OverviewDesign { List, SplitList, Cards }

 // A paired grid may contain empty cells; capacity grows by rows, never by hiding data.
 // Zero on one axis represents a saved pre-combination preference until its item count is known.
 internal sealed class GridFlow {
  private readonly int rowCount,columnCount;
  internal GridFlow(int rows,int columns){rowCount=Math.Max(0,rows);columnCount=Math.Max(0,columns);if(rowCount==0 && columnCount==0)columnCount=1;}
  internal int Columns(int items)=>Math.Min(Math.Max(1,items),columnCount>0?columnCount:Divide(items,Math.Max(1,rowCount)));
  internal int Rows(int items)=>Divide(items,Columns(items));
  internal bool IsNormalized(int items)=>rowCount>0 && columnCount>0 && rowCount==Rows(items) && columnCount==Columns(items);
  internal GridFlow Normalize(int items)=>new GridFlow(Rows(items),Columns(items));
  internal void Position(int index,int items,out int row,out int column){
   if(items<1 || index<0 || index>=items)throw new ArgumentOutOfRangeException(nameof(index));
   int columns=Columns(items);row=index/columns;column=index%columns;
  }
  private static int Divide(int items,int count)=>Math.Max(1,(int)(((long)Math.Max(0,items)+count-1)/count));
  public override string ToString()=>rowCount==0?"Columns:"+columnCount.ToString(CultureInfo.InvariantCulture):columnCount==0?"Rows:"+rowCount.ToString(CultureInfo.InvariantCulture):rowCount.ToString(CultureInfo.InvariantCulture)+"x"+columnCount.ToString(CultureInfo.InvariantCulture);
  internal string Label=>rowCount+" × "+columnCount;
  internal static GridFlow Parse(string value,int fallback){
   var parts=(value??"").Split(':');int count,columns;
   if(parts.Length==2 && (parts[0]=="Rows" || parts[0]=="Columns") && int.TryParse(parts[1],NumberStyles.None,CultureInfo.InvariantCulture,out count) && count>0)
    return new GridFlow(parts[0]=="Rows"?count:0,parts[0]=="Columns"?count:0);
   parts=(value??"").Split('x');
   if(parts.Length==2 && int.TryParse(parts[0],NumberStyles.None,CultureInfo.InvariantCulture,out count) && count>0 && int.TryParse(parts[1],NumberStyles.None,CultureInfo.InvariantCulture,out columns) && columns>0)return new GridFlow(count,columns);
   return new GridFlow(0,fallback);
  }
  internal static GridFlow[] Choices(int items,GridFlow selected){
   items=Math.Max(1,items);var choices=new Dictionary<string,GridFlow>();
   for(int row=1;row<=items;row++){int cols=Divide(items,row);if(Divide(items,cols)!=row)continue;var grid=new GridFlow(row,cols);choices[grid.ToString()]=grid;}
   var current=selected.Normalize(items);choices[current.ToString()]=current;
   return choices.Values.OrderBy(g=>g.rowCount).ThenByDescending(g=>g.columnCount).ToArray();
  }
 }

 internal static class OverviewLayout {
  internal static OverviewDesign ParseDesign(string value)=>Enum.TryParse(value,out OverviewDesign design) && Enum.IsDefined(typeof(OverviewDesign),design)?design:OverviewDesign.List;
  internal static int Pane(int index,int count)=>index/Math.Max(1,(count+1)/2);
  internal static int PaneRow(int index,int count)=>index%Math.Max(1,(count+1)/2);
  internal static int ColorIndex(int player,Func<int,int> remap,int[] mapping){
   if(player<1 || player>8 || mapping==null)return 0;
   int slot=remap(player);return slot>0 && slot<mapping.Length && mapping[slot]>0 && mapping[slot]<mapping.Length?mapping[slot]:0;
  }
 }

 // Shared by the real UI and tests. A reusable handler captures its stamp on each invocation.
 internal sealed class UiActionContext {
  internal readonly long Session;
  internal readonly int Generation,MenuEpoch;
  internal readonly object View;
  internal readonly bool Visible,Enabled,MenuOpen;
  internal UiActionContext(long session,int generation,int menuEpoch,object view,bool visible,bool enabled,bool menuOpen){
   Session=session;Generation=generation;MenuEpoch=menuEpoch;View=view;Visible=visible;Enabled=enabled;MenuOpen=menuOpen;
  }
  internal bool Allows(UiActionContext next,bool needsMenu)=>Session>0 && Session==next.Session && Generation==next.Generation && View!=null && ReferenceEquals(View,next.View)
   && Visible && Enabled && next.Visible && next.Enabled && (!needsMenu || MenuOpen && next.MenuOpen && MenuEpoch==next.MenuEpoch);
 }
 internal static class UiActions {
  internal static Action Bind(Action action,Func<UiActionContext> read,Action<Action> post,bool needsMenu){
   return ()=>{var stamp=read();post(()=>{if(stamp.Allows(read(),needsMenu))action();});};
  }
 }
 // Main-thread requests are coalesced independently of simulation ticks, including while paused.
 internal sealed class UiRefreshQueue {
  private readonly Func<UiActionContext> read;
  private readonly Action<Action> post;
  private readonly Action refresh;
  private UiActionContext latest;
  private bool queued;
  internal UiRefreshQueue(Func<UiActionContext> read,Action<Action> post,Action refresh){this.read=read;this.post=post;this.refresh=refresh;}
  internal void Request(){latest=read();if(queued)return;queued=true;post(()=>{
   var stamp=latest;latest=null;queued=false;if(stamp!=null && stamp.Allows(read(),false))refresh();
  });}
  internal void Cancel(){latest=null;}
 }
 internal static class UiSamples {
  // No snapshot may cross a mission/HUD change or an information-rights revocation.
  internal static bool TryRead<T>(Func<UiActionContext> read,Func<int> rights,Func<T> capture,Func<T,UiActionContext,int,bool> extend,Action<T> publish){
   var stamp=read();int version=rights();if(!stamp.Allows(stamp,false))return false;
   var sample=capture();if(ReferenceEquals(sample,null) || !stamp.Allows(read(),false) || version!=rights())return false;
   if(!extend(sample,stamp,version) || !stamp.Allows(read(),false) || version!=rights())return false;
   publish(sample);return true;
  }
 }
}
