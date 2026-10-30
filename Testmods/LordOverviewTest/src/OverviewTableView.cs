using System;
using System.Collections.Generic;
using System.Linq;
using CrusaderDE;
using Noesis;

namespace LordOverviewTest {
 internal static partial class OverviewRuntime {
  private sealed class RowVisual {
   internal FrameworkElement Root;
   internal Grid Portrait,Number;
   internal TextBlock Name;
   internal SolidColorBrush Foreground;
   internal string FullName;
   internal ImageSource Face;
   internal readonly Dictionary<Metric,Grid> Cells=new Dictionary<Metric,Grid>();
  }
  private static readonly Dictionary<int,RowVisual> rowVisuals=new Dictionary<int,RowVisual>();
  private static readonly List<Grid> tables=new List<Grid>();
  private static Grid cards;
  private static int editEpoch;
  private static bool headerEditing;
  private static bool Editing=>settingsWindow?.Visibility==Visibility.Visible && panel?.IsVisible==true;

  private static ImageSource PlayerFace(int player){
   var snapshot=GameData.Instance.lastGameState;
   return snapshot.is_skirmish_player(player)?main.getAIFace(snapshot.computer_register[player],true):
    Platform_Multiplayer.Instance.GetUserAvatar(Platform_Multiplayer.Instance.getPlayerSteamID(player));
  }
  private static void Render(){
   if(headerGesture.Active || panel==null || GameData.Instance?.lastGameState==null)return;
   var columns=selection.Columns;state.Reconcile(columns);var ordered=state.Sort(rows);NormalizeGridPreferences(ordered.Count,columns.Length);
   var colors=ordered.ToDictionary(r=>r.Player,r=>ResolvePlayerColor(r.Player));
   var names=ordered.ToDictionary(r=>r.Player,r=>Platform_Multiplayer.Instance.getSkirmishName(r.Player)??"");
   var faces=ordered.ToDictionary(r=>r.Player,r=>PlayerFace(r.Player));
   string layout=Metrics.Serialize(columns)+"/"+catalogRevision+"/"+settings.DisplayDesign+"/"+settings.DisplayLordGrid+"/"+settings.DisplayValueGrid+"/"+string.Join(",",ordered.Select(r=>r.Player).OrderBy(p=>p));
   string next=layout+"/"+string.Join(";",ordered.Select(r=>r.Player+"="+colors[r.Player]))+"/"+state.Column+"/"+state.Descending+"/"+string.Join(";",ordered.Select(r=>r.Player+":"+r.Alive+":"+r.ValueSignature));
   bool identityChanged=ordered.Any(r=>!rowVisuals.TryGetValue(r.Player,out var view) || view.FullName!=names[r.Player] || !ReferenceEquals(view.Face,faces[r.Player]));
   if(next==signature && !identityChanged)return;
   if(headerLayout!=layout){
    ResetTable();headerLayout=layout;
    if(settings.DisplayDesign==OverviewDesign.Cards){
     cards=new Grid();var flow=settings.DisplayLordGrid;
     for(int i=0;i<flow.Columns(ordered.Count);i++)cards.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
     for(int i=0;i<flow.Rows(ordered.Count);i++)cards.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});panel.Children.Add(cards);
    }else{
     var container=new StackPanel {Orientation=Orientation.Horizontal};panel.Children.Add(container);
     int panes=settings.DisplayDesign==OverviewDesign.SplitList?2:1;
     for(int i=0;i<panes;i++){
      var table=new Grid {Margin=new Thickness(5,2,5,2)};tables.Add(table);
      for(int col=0;col<=columns.Length;col++)table.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
      int count=panes==1?ordered.Count:(ordered.Count+1)/2;
      for(int row=0;row<=count;row++)table.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
      var surface=NewHeaderSurface(table,1,"view/"+i+"/");
      var identity=CreateHeader(surface,null,Text("Player"),Label("#"));identity.MinWidth=44;table.Children.Add(identity);
      for(int col=0;col<columns.Length;col++){var button=CreateHeader(surface,columns[col],MetricHelp(columns[col]),MetricIcon(columns[col]));Grid.SetColumn(button,col+1);table.Children.Add(button);}
      for(int row=0;row<=count;row++)AddDivider(table,row,row==0);
      table.SizeChanged+=OnTableGeometryChanged;
      var backing=new Grid {Margin=new Thickness(i==0?0:8,0,0,0),VerticalAlignment=VerticalAlignment.Bottom};
      backing.Children.Add(new Border {Background=StatisticsBackground(),IsHitTestVisible=false});backing.Children.Add(table);container.Children.Add(backing);
     }
    }
    foreach(var row in ordered){var view=CreateRow(row.Player,columns);rowVisuals.Add(row.Player,view);}
   }
   for(int index=0;index<ordered.Count;index++){
    var row=ordered[index];var view=rowVisuals[row.Player];
    if(view.Foreground==null || !view.Foreground.Color.Equals(colors[row.Player]))view.Foreground=new SolidColorBrush(colors[row.Player]);
    var color=view.Foreground;view.FullName=names[row.Player];view.Face=faces[row.Player];
    ((Image)view.Portrait.Children[0]).Source=view.Face;
    UpdateHoverText("view/player/"+row.Player,view.FullName);
    OverviewAppearance.UpdatePlayerText(view.Number,row.Player.ToString(),color);
    if(view.Name!=null){view.Name.Text=view.FullName;view.Name.Foreground=color;UpdateHoverText("view/name/"+row.Player,view.FullName);}
    foreach(var metric in columns)OverviewAppearance.UpdatePlayerText(view.Cells[metric],Metrics.Get(metric).Format(row.Get(metric)),color);
    if(cards!=null){view.Root.Opacity=row.Alive?1:.45f;settings.DisplayLordGrid.Position(index,ordered.Count,out int cardRow,out int cardColumn);Grid.SetRow(view.Root,cardRow);Grid.SetColumn(view.Root,cardColumn);
     int lastOccupiedRow=(ordered.Count-1)/settings.DisplayLordGrid.Columns(ordered.Count);view.Root.Margin=new Thickness(0,0,8,cardRow==lastOccupiedRow?0:6);}
    else{
     int pane=tables.Count==1?0:OverviewLayout.Pane(index,ordered.Count),position=(tables.Count==1?index:OverviewLayout.PaneRow(index,ordered.Count))+1;
     MoveCell(view.Root,tables[pane],position,0);view.Root.Opacity=row.Alive?1:.45f;
     for(int col=0;col<columns.Length;col++){var cell=view.Cells[columns[col]];MoveCell(cell,tables[pane],position,col+1);cell.Opacity=view.Root.Opacity;}
    }
   }
   UpdateHeaderEditing();UpdateGridReadouts();MarkHoverGeometryDirty();signature=next;
  }
  private static Brush StatisticsBackground()=>new SolidColorBrush(Color.FromRgb(96,96,96)){Opacity=.5f};
  private static void MoveCell(FrameworkElement cell,Grid target,int row,int column){
   if(VisualTreeHelper.GetParent(cell) is Grid old && !ReferenceEquals(old,target))old.Children.Remove(cell);
   if(!target.Children.Contains(cell))target.Children.Add(cell);Grid.SetRow(cell,row);Grid.SetColumn(cell,column);
  }
  private static RowVisual CreateRow(int player,Metric[] columns){
   var view=new RowVisual {Portrait=OverviewAppearance.Portrait(null),Number=OverviewAppearance.PlayerText(player.ToString(),null)};
   view.Number.Margin=new Thickness(5,0,3,0);
   RegisterHover("view/player/"+player,view.Portrait,"");
   if(cards==null){
    var identity=new StackPanel {Orientation=Orientation.Horizontal,IsHitTestVisible=false,Margin=new Thickness(2)};
    identity.Children.Add(view.Portrait);identity.Children.Add(view.Number);view.Root=identity;
    foreach(var metric in columns){var cell=OverviewAppearance.PlayerText("",null);cell.HorizontalAlignment=HorizontalAlignment.Right;cell.Margin=new Thickness(4,2,4,2);view.Cells.Add(metric,cell);}
    return view;
   }
   var content=new Grid {Margin=new Thickness(5,2,5,2),IsHitTestVisible=false};content.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});content.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
   var header=new Grid {Margin=new Thickness(0,0,0,4),IsHitTestVisible=false};header.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});header.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});header.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});
   header.Children.Add(view.Portrait);Grid.SetColumn(view.Number,1);header.Children.Add(view.Number);
   // A Canvas contributes no name width to the natural card size. Only the values and portrait/ID set it.
   var nameHost=new Canvas {Height=28,IsHitTestVisible=false,ClipToBounds=true};Grid.SetColumn(nameHost,2);header.Children.Add(nameHost);
   view.Name=Label("");view.Name.TextTrimming=TextTrimming.CharacterEllipsis;view.Name.IsHitTestVisible=false;Canvas.SetTop(view.Name,4);nameHost.Children.Add(view.Name);
   nameHost.SizeChanged+=(_,__)=>{view.Name.Width=Math.Max(0,nameHost.ActualWidth);MarkHoverGeometryDirty();};
   RegisterHover("view/name/"+player,nameHost,"");content.Children.Add(header);
   var values=new Grid {IsHitTestVisible=false};Grid.SetRow(values,1);var flow=settings.DisplayValueGrid;int cols=flow.Columns(columns.Length);
   for(int i=0;i<cols;i++)values.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
   for(int i=0;i<flow.Rows(columns.Length);i++)values.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
   for(int i=0;i<columns.Length;i++){
    var metric=columns[i];var pair=new StackPanel {Orientation=Orientation.Horizontal,IsHitTestVisible=false,Margin=new Thickness(4,2,6,2)};
    var icon=MetricIcon(metric);icon.IsHitTestVisible=false;icon.VerticalAlignment=VerticalAlignment.Center;pair.Children.Add(icon);
    var cell=OverviewAppearance.PlayerText("",null);cell.Margin=new Thickness(5,0,0,0);view.Cells.Add(metric,cell);pair.Children.Add(cell);
    RegisterHover("view/value/"+player+"/"+Metrics.Key(metric),icon,MetricHelp(metric));
    flow.Position(i,columns.Length,out int valueRow,out int valueColumn);Grid.SetRow(pair,valueRow);Grid.SetColumn(pair,valueColumn);values.Children.Add(pair);
   }
   content.Children.Add(values);
   var box=new Border {Background=StatisticsBackground(),Child=content,Margin=new Thickness(0,0,8,6),HorizontalAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Bottom,IsHitTestVisible=false};
   box.SizeChanged+=OnTableGeometryChanged;view.Root=box;cards.Children.Add(box);return view;
  }
  private static void UpdateHoverText(string key,string text){if(hoverTargets.TryGetValue(key,out var target)){target.Description=text;target.Tip.Content=text;}}
  private static void ResetTable(){
   CancelHeaderGesture();RemoveHeaders(false);rowVisuals.Clear();
   foreach(var table in tables)table.SizeChanged-=OnTableGeometryChanged;tables.Clear();cards=null;headerLayout="";
   if(panel!=null){CloseTips(panel);panel.Children.Clear();}
  }
  private static void AddDivider(Grid table,int row,bool header){
   var line=new Border {Height=1,VerticalAlignment=VerticalAlignment.Bottom,IsHitTestVisible=false,Background=new SolidColorBrush(Color.FromArgb((byte)(header?100:45),220,220,220))};
   Grid.SetRow(line,row);Grid.SetColumnSpan(line,table.ColumnDefinitions.Count);table.Children.Add(line);
  }
  private static Button CreateHeader(HeaderSurface surface,Metric? metric,string help,FrameworkElement symbol){
   symbol.IsHitTestVisible=false;symbol.HorizontalAlignment=HorizontalAlignment.Center;symbol.VerticalAlignment=VerticalAlignment.Center;
   var content=new Grid {Margin=new Thickness(2,0,2,0)};content.Children.Add(symbol);
   var arrow=new Canvas {Width=4,Height=5,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false,Visibility=Visibility.Collapsed};
   arrow.Children.Add(new Path {Fill=new SolidColorBrush(Color.FromRgb(255,240,191)),Data=Geometry.Parse("M0,0 L4,0 L2,3 Z")});content.Children.Add(arrow);
   var highlight=new Border {Child=content,Background=new SolidColorBrush(Color.FromArgb(0,0,0,0))};
   var button=new Button {Style=buttonStyle,Content=highlight,Padding=new Thickness(2),HorizontalAlignment=HorizontalAlignment.Stretch};
   var binding=new HeaderBinding {Surface=surface,Metric=metric,Highlight=highlight,Arrow=arrow,Menu=surface.Prefix.StartsWith("menu/",StringComparison.Ordinal)};
   if(binding.Menu)Tip(button,help+" — "+Text("HeaderDragHint"));
   if(metric.HasValue)BindMetricHeader(button,binding);
   else {headers.Add(button,binding);var action=UiAction(()=>{state.Select(null);signature="";Render();UpdateColumnsPopup();});button.Click+=(_,__)=>action();}
   if(!binding.Menu)RegisterHover(surface.Prefix+(metric.HasValue?Metrics.Key(metric.Value):"identity"),symbol,help);return button;
  }
  private static void UpdateHeaderEditing(){
   if(headerEditing!=Editing){headerEditing=Editing;editEpoch++;CancelHeaderGesture();ClosePassiveHover();}
   foreach(var pair in headers){
    pair.Key.IsHitTestVisible=Editing;pair.Key.Focusable=Editing;
    pair.Value.Arrow.Visibility=state.Column==pair.Value.Metric?Visibility.Visible:Visibility.Collapsed;
    pair.Value.Arrow.RenderTransform=state.Descending?null:new ScaleTransform {ScaleX=1,ScaleY=-1,CenterX=2,CenterY=2};
   }
   foreach(var surface in headerSurfaces.Where(s=>s.Prefix.StartsWith("view/",StringComparison.Ordinal)))
    foreach(var target in hoverTargets.Where(p=>p.Key.StartsWith(surface.Prefix,StringComparison.Ordinal)))
     target.Value.Tip.Content=target.Value.Description+(Editing?" — "+Text("Sort")+" — "+Text("HeaderDragHint"):"");
  }
  private static void OnTableGeometryChanged(object sender,SizeChangedEventArgs args){CancelHeaderGesture();MarkHoverGeometryDirty();if(initialMenuPlacement)PositionSettingsMenu();}
 }
}
