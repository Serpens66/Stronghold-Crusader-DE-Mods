using System;
using System.Collections.Generic;
using System.Linq;
using CrusaderDE;
using Noesis;

namespace LordOverviewTest {
 internal static partial class OverviewRuntime {
  private sealed class RowVisual {
   internal StackPanel Identity;
   internal Grid Portrait,Number;
   internal SolidColorBrush Foreground;
   internal readonly Dictionary<Metric,Grid> Cells=new Dictionary<Metric,Grid>();
  }
  private readonly static Dictionary<int,RowVisual> rowVisuals=new Dictionary<int,RowVisual>();
  private readonly static List<Border> dividers=new List<Border>();
  private readonly static List<Button> editHeaders=new List<Button>();
  private readonly static Dictionary<Button,Canvas> sortArrows=new Dictionary<Button,Canvas>();
  private static Button numberHeader;
  private static int editEpoch;
  private static bool headerEditing;
  private static bool Editing=>columnsPopup?.IsOpen==true;

  private static void Render(){
   if(headerGesture.Active || panel==null)return;
   var columns=selection.Columns;state.Reconcile(columns);var ordered=state.Sort(rows);
   var colors=ordered.ToDictionary(r=>r.Player,r=>ResolvePlayerColor(r.Player));
   string next=Metrics.Serialize(columns)+"/"+catalogRevision+"/"+string.Join(";",ordered.Select(r=>r.Player+"="+colors[r.Player]))+"/"+state.Column+"/"+state.Descending+"/"+string.Join(";",ordered.Select(r=>r.Player+":"+r.Alive+":"+r.ValueSignature));
   if(next==signature)return;signature=next;
   string layout=Metrics.Serialize(columns)+"/"+catalogRevision;
   if(tableHeader==null || headerLayout!=layout){
    ResetTable();headerLayout=layout;
    var background=new Border {Background=new SolidColorBrush(Color.FromRgb(96,96,96)){Opacity=.5f},IsHitTestVisible=false};panel.Children.Add(background);
    var table=new Grid {Margin=new Thickness(5,2,5,2)};
    // Canvas supplies unbounded horizontal measurement; every row shares these intrinsic columns.
    for(int i=0;i<=columns.Length;i++)table.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
    table.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
    var identity=new Grid {MinWidth=44};table.Children.Add(identity);
    var gear=ActionButton("",Text("ColumnsHelp"),()=>{if(Editing)CloseColumnsPopup();else OpenColumnsPopup();});
    gear.Content=GearIcon();gear.Width=24;gear.Height=24;gear.HorizontalAlignment=HorizontalAlignment.Left;gear.Focusable=false;
    // Native tooltip service would require an extra visual hit. The same passive presenter owns the gear tip.
    ToolTipService.SetToolTip(gear,null);identity.Children.Add(gear);gearButton=gear;
    RegisterHover("gear",gear,Text("ColumnsHelp"));
    numberHeader=CreateHeader(null,Text("Player"),Label("#"));numberHeader.Width=20;numberHeader.HorizontalAlignment=HorizontalAlignment.Right;identity.Children.Add(numberHeader);
    for(int i=0;i<columns.Length;i++){var button=CreateHeader(columns[i],MetricHelp(columns[i]),MetricIcon(columns[i]));Grid.SetColumn(button,i+1);table.Children.Add(button);}
    AttachHeaderMarker(table);panel.Children.Add(table);AddDivider(table,0,true);
    table.SizeChanged+=OnTableGeometryChanged;
   }
   var grid=tableHeader;
   foreach(int player in rowVisuals.Keys.Where(p=>!ordered.Any(r=>r.Player==p)).ToArray())RemoveRow(player);
   while(grid.RowDefinitions.Count<ordered.Count+1)grid.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
   while(grid.RowDefinitions.Count>ordered.Count+1)grid.RowDefinitions.RemoveAt(grid.RowDefinitions.Count-1);
   while(dividers.Count>ordered.Count+1){grid.Children.Remove(dividers[dividers.Count-1]);dividers.RemoveAt(dividers.Count-1);}
   while(dividers.Count<ordered.Count+1)AddDivider(grid,dividers.Count,false);
   int index=1;
   foreach(var row in ordered){
    if(!rowVisuals.TryGetValue(row.Player,out var view)){
     view=new RowVisual {Identity=new StackPanel {Orientation=Orientation.Horizontal,IsHitTestVisible=false,Margin=new Thickness(2,2,2,2)}};
     view.Portrait=OverviewAppearance.Portrait(null);view.Identity.Children.Add(view.Portrait);
     view.Number=OverviewAppearance.PlayerText(row.Player.ToString(),new SolidColorBrush(colors[row.Player]));view.Number.Margin=new Thickness(5,0,3,0);view.Identity.Children.Add(view.Number);
     grid.Children.Add(view.Identity);rowVisuals.Add(row.Player,view);
     RegisterHover("player/"+row.Player,view.Portrait,Platform_Multiplayer.Instance.getSkirmishName(row.Player));
     for(int i=0;i<columns.Length;i++){
      var cell=OverviewAppearance.PlayerText("",new SolidColorBrush(colors[row.Player]));cell.IsHitTestVisible=false;cell.HorizontalAlignment=HorizontalAlignment.Right;cell.Margin=new Thickness(4,2,4,2);
      Grid.SetColumn(cell,i+1);grid.Children.Add(cell);view.Cells.Add(columns[i],cell);
     }
    }
    var snapshot=GameData.Instance.lastGameState;
    var face=snapshot.is_skirmish_player(row.Player)?main.getAIFace(snapshot.computer_register[row.Player],true):Platform_Multiplayer.Instance.GetUserAvatar(Platform_Multiplayer.Instance.getPlayerSteamID(row.Player));
    var image=(Image)view.Portrait.Children[0];if(!ReferenceEquals(image.Source,face))image.Source=face;
    if(view.Foreground==null || !view.Foreground.Color.Equals(colors[row.Player]))view.Foreground=new SolidColorBrush(colors[row.Player]);
    var color=view.Foreground;OverviewAppearance.UpdatePlayerText(view.Number,row.Player.ToString(),color);
    Grid.SetRow(view.Identity,index);view.Identity.Opacity=row.Alive?1:.45f;
    foreach(var metric in columns){var cell=view.Cells[metric];OverviewAppearance.UpdatePlayerText(cell,Metrics.Get(metric).Format(row.Get(metric)),color);cell.Opacity=view.Identity.Opacity;Grid.SetRow(cell,index);}
    index++;
   }
   UpdateHeaderEditing();MarkHoverGeometryDirty();
  }
  private static void ResetTable(){
   ClearHoverTargets();hoverFaulted=false;rowVisuals.Clear();dividers.Clear();editHeaders.Clear();sortArrows.Clear();numberHeader=null;
   metricHeaders.Clear();headerHighlights.Clear();
   if(tableHeader!=null)tableHeader.SizeChanged-=OnTableGeometryChanged;
   tableHeader=null;insertionMarker=null;headerLayout="";
   if(panel!=null){CloseTips(panel);panel.Children.Clear();}
  }
  private static void RemoveRow(int player){
   var view=rowVisuals[player];RemoveHover("player/"+player);tableHeader.Children.Remove(view.Identity);
   foreach(var cell in view.Cells.Values)tableHeader.Children.Remove(cell);rowVisuals.Remove(player);
  }
  private static void AddDivider(Grid table,int row,bool header){
   var line=new Border {Height=1,VerticalAlignment=VerticalAlignment.Bottom,IsHitTestVisible=false,Background=new SolidColorBrush(Color.FromArgb((byte)(header?100:45),220,220,220))};
   Grid.SetRow(line,row);Grid.SetColumnSpan(line,table.ColumnDefinitions.Count);table.Children.Add(line);dividers.Add(line);
  }
  private static Button CreateHeader(Metric? metric,string help,FrameworkElement symbol){
   symbol.IsHitTestVisible=false;symbol.HorizontalAlignment=HorizontalAlignment.Center;symbol.VerticalAlignment=VerticalAlignment.Center;
   var content=new Grid {Margin=new Thickness(2,0,2,0)};content.Children.Add(symbol);
   var arrow=new Canvas {Width=4,Height=5,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false,Visibility=Visibility.Collapsed};
   var path=new Path {Fill=new SolidColorBrush(Color.FromRgb(255,240,191)),Data=Geometry.Parse("M0,0 L4,0 L2,3 Z")};arrow.Children.Add(path);content.Children.Add(arrow);
   var highlight=new Border {Child=content,Background=new SolidColorBrush(Color.FromArgb(0,0,0,0))};
   var button=new Button {Style=buttonStyle,Content=highlight,Padding=new Thickness(0),HorizontalAlignment=HorizontalAlignment.Stretch};
   sortArrows[button]=arrow;editHeaders.Add(button);
   if(metric.HasValue)BindMetricHeader(button,metric.Value,highlight);
   else button.Click+=(_,__)=>{long expected=System.Threading.Interlocked.Read(ref session);int epoch=editEpoch;
    UiAction(()=>{if(!TableInteraction.CanEdit(Editing,panel?.IsVisible==true,System.Threading.Interlocked.Read(ref session),expected,editEpoch,epoch))return;state.Select(null);signature="";Render();})();};
   RegisterHover(metric.HasValue?"metric/"+Metrics.Key(metric.Value):"identity",symbol,help);
   return button;
  }
  private static void UpdateHeaderEditing(){
   if(headerEditing!=Editing){headerEditing=Editing;editEpoch++;CancelHeaderGesture();ClosePassiveHover();}
   foreach(var button in editHeaders){button.IsHitTestVisible=Editing;button.Focusable=Editing;}
   foreach(var target in hoverTargets)if(target.Key.StartsWith("metric/",StringComparison.Ordinal) || target.Key=="identity")target.Value.Tip.Content=target.Value.Description+(Editing?" — "+Text("Sort")+" — "+Text("HeaderDragHint"):"");
   foreach(var pair in sortArrows){Metric? metric=ReferenceEquals(pair.Key,numberHeader)?(Metric?)null:metricHeaders.First(p=>ReferenceEquals(p.Value,pair.Key)).Key;
    pair.Value.Visibility=state.Column==metric?Visibility.Visible:Visibility.Collapsed;
    pair.Value.RenderTransform=state.Descending?null:new ScaleTransform {ScaleX=1,ScaleY=-1,CenterX=2,CenterY=2};
   }
  }
  private static void OnTableGeometryChanged(object sender,SizeChangedEventArgs args)=>MarkHoverGeometryDirty();
 }
}
