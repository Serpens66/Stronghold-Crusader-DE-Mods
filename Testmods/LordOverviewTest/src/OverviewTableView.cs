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
   internal Image PopularityIcon;
   internal int PopularityBand=-1;
   internal HoverTarget PortraitHover,NameHover;
   internal string PlayerLabel;
   internal bool? Alive;
   internal int Pane=-1,Position=-1;
   internal readonly Dictionary<Metric,decimal?> LastValues=new Dictionary<Metric,decimal?>();
   internal readonly Dictionary<Metric,Grid> Cells=new Dictionary<Metric,Grid>();
  }
  private static readonly Dictionary<int,RowVisual> rowVisuals=new Dictionary<int,RowVisual>();
  private static readonly List<Grid> tables=new List<Grid>();
  private static Grid cards;
  private static readonly List<LordRow> orderedRows=new List<LordRow>(8);
  private static Metric[] realizedColumns;
  private static OverviewDesign realizedDesign;
  private static GridFlow realizedLordGrid,realizedValueGrid;
  private static long realizedCatalog;
  private static int realizedRoster;
  private static int editEpoch;
  private static bool headerEditing,paintedDescending;
  private static Metric? paintedSort;
  private static int headerRevision,paintedHeaderRevision=-1;
  private static bool Editing=>settingsWindow?.Visibility==Visibility.Visible && panel?.IsVisible==true;

  private static ImageSource PlayerFace(int player){
   var snapshot=GameData.Instance.lastGameState;
   return snapshot.is_skirmish_player(player)?main.getAIFace(snapshot.computer_register[player],true):
    Platform_Multiplayer.Instance.GetUserAvatar(Platform_Multiplayer.Instance.getPlayerSteamID(player));
  }
  private static void Render(){
   if(headerGesture.Active || panel?.IsVisible!=true || GameData.Instance?.lastGameState==null)return;
   var columns=selection.Columns;state.Reconcile(columns);state.SortInto(rows,orderedRows,settings.DisplayShowEliminated);var ordered=orderedRows;
   NormalizeGridPreferences(rows.Count,columns.Length); // Filtering must not rewrite the saved grid.
   var design=settings.DisplayDesign;var lordFlow=settings.DisplayLordGrid;var valueFlow=settings.DisplayValueGrid;
   int roster=0;for(int i=0;i<ordered.Count;i++)roster|=1<<ordered[i].Player;
   bool rebuild=!OverviewLayout.SameColumns(realizedColumns,columns) || realizedDesign!=design || !lordFlow.SameShape(realizedLordGrid) || !valueFlow.SameShape(realizedValueGrid) || realizedCatalog!=catalogRevision || realizedRoster!=roster;
   bool geometryChanged=rebuild;long elapsed=0;Shared.DevDiag.Start(ref elapsed);
   try {
    if(rebuild){
     ResetTable();
    if(ordered.Count==0){var empty=Label(Text("NoLordsToShow"));empty.Tag=OverviewPresentationPolicy.PassiveTag;empty.IsHitTestVisible=false;panel.Children.Add(empty);}
    else if(settings.DisplayDesign==OverviewDesign.Cards){
     cards=new Grid();var flow=settings.DisplayLordGrid;
     for(int i=0;i<flow.Columns(ordered.Count);i++)cards.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
     for(int i=0;i<flow.Rows(ordered.Count);i++)cards.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});panel.Children.Add(cards);
    }else{
     var container=new StackPanel {Orientation=Orientation.Horizontal,Tag=OverviewPresentationPolicy.ContainerTag};panel.Children.Add(container);
     int panes=settings.DisplayDesign==OverviewDesign.SplitList?2:1;
     for(int i=0;i<panes;i++){
      var table=new Grid {Margin=new Thickness(5,2,5,2),Tag=OverviewPresentationPolicy.ContainerTag};tables.Add(table);
      for(int col=0;col<=columns.Length;col++)table.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
      int count=panes==1?ordered.Count:(ordered.Count+1)/2;
      for(int row=0;row<=count;row++)table.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
      var surface=NewHeaderSurface(table,1,"view/"+i+"/");
      var identity=CreateHeader(surface,null,Text("Player"),Label("#"));identity.MinWidth=44;table.Children.Add(identity);
      for(int col=0;col<columns.Length;col++){var button=CreateHeader(surface,columns[col],MetricHelp(columns[col]),MetricIcon(columns[col]));Grid.SetColumn(button,col+1);table.Children.Add(button);}
      for(int row=0;row<=count;row++)AddDivider(table,row,row==0);
      table.SizeChanged+=OnTableGeometryChanged;
      var backing=new Grid {Margin=new Thickness(i==0?0:8,0,0,0),VerticalAlignment=VerticalAlignment.Bottom,Tag=OverviewPresentationPolicy.ContainerTag};
      backing.Children.Add(new Border {Background=StatisticsBackground(),IsHitTestVisible=false,Tag=OverviewPresentationPolicy.PassiveTag});backing.Children.Add(table);container.Children.Add(backing);
     }
    }
    foreach(var row in ordered){var view=CreateRow(row.Player,columns);rowVisuals.Add(row.Player,view);}

     realizedColumns=columns;realizedDesign=design;realizedLordGrid=lordFlow;realizedValueGrid=valueFlow;realizedCatalog=catalogRevision;realizedRoster=roster;
    }
    for(int index=0;index<ordered.Count;index++){
     var row=ordered[index];var view=rowVisuals[row.Player];var playerColor=ResolvePlayerColor(row.Player);
     bool colorChanged=view.Foreground==null || !view.Foreground.Color.Equals(playerColor);
     if(colorChanged){view.Foreground=new SolidColorBrush(playerColor);OverviewAppearance.UpdatePlayerText(view.Number,view.PlayerLabel,view.Foreground);}
     string name=Platform_Multiplayer.Instance.getSkirmishName(row.Player)??"";var face=PlayerFace(row.Player);
     bool nameChanged=view.FullName!=name;
     if(!ReferenceEquals(view.Face,face)){view.Face=face;((Image)view.Portrait.Children[0]).Source=face;}
     if(nameChanged){view.FullName=name;UpdateHoverText(view.PortraitHover,name);UpdateHoverText(view.NameHover,name);}
     if(view.Name!=null){if(nameChanged)view.Name.Text=name;if(colorChanged)view.Name.Foreground=view.Foreground;}
     bool aliveChanged=view.Alive!=row.Alive;if(aliveChanged){view.Alive=row.Alive;view.Root.Opacity=row.Alive?1:.45f;}
     if(view.PopularityIcon!=null){int band=OverviewPresentationPolicy.PopularitySprite(row.Get(Metric.Popularity),true);if(view.PopularityBand!=band){view.PopularityBand=band;view.PopularityIcon.Source=popularityMasks[band];Shared.DevDiag.Count("overview.icon.popularity-source");}}
     foreach(var metric in columns){
      var value=row.Get(metric);var cell=view.Cells[metric];
      if(colorChanged || !view.LastValues.TryGetValue(metric,out var previous) || value!=previous){
       OverviewAppearance.UpdatePlayerText(cell,Metrics.Get(metric).Format(value),view.Foreground);view.LastValues[metric]=value;geometryChanged=true;
      }
      if(aliveChanged && cards==null)cell.Opacity=view.Root.Opacity;
     }
     if(cards!=null){
      lordFlow.Position(index,ordered.Count,out int cardRow,out int cardColumn);
      if(view.Pane!=cardColumn || view.Position!=cardRow){view.Pane=cardColumn;view.Position=cardRow;Grid.SetRow(view.Root,cardRow);Grid.SetColumn(view.Root,cardColumn);
       int lastRow=(ordered.Count-1)/lordFlow.Columns(ordered.Count);view.Root.Margin=new Thickness(0,0,8,cardRow==lastRow?0:6);geometryChanged=true;}
     }else{
      int pane=tables.Count==1?0:OverviewLayout.Pane(index,ordered.Count),position=(tables.Count==1?index:OverviewLayout.PaneRow(index,ordered.Count))+1;
      if(view.Pane!=pane || view.Position!=position){view.Pane=pane;view.Position=position;MoveCell(view.Root,tables[pane],position,0);
       for(int col=0;col<columns.Length;col++)MoveCell(view.Cells[columns[col]],tables[pane],position,col+1);geometryChanged=true;}
     }
    }
    UpdateHeaderEditing();if(rebuild)UpdateGridReadouts();if(geometryChanged)MarkHoverGeometryDirty();
   }finally{if(rebuild)Shared.DevDiag.StopSpike(ref elapsed,"overview.layout",1000);else Shared.DevDiag.StopSpike(ref elapsed,"overview.render",1000);}
  }
  private static Brush StatisticsBackground()=>new SolidColorBrush(Color.FromRgb(96,96,96)){Opacity=.5f};
  private static void MoveCell(FrameworkElement cell,Grid target,int row,int column){
   if(VisualTreeHelper.GetParent(cell) is Grid old && !ReferenceEquals(old,target))old.Children.Remove(cell);
   if(!target.Children.Contains(cell))target.Children.Add(cell);Grid.SetRow(cell,row);Grid.SetColumn(cell,column);
  }
  private static RowVisual CreateRow(int player,Metric[] columns){
   var view=new RowVisual {Portrait=OverviewAppearance.Portrait(null),Number=OverviewAppearance.PlayerText(player.ToString(),null),PlayerLabel=player.ToString()};
   view.Number.Margin=new Thickness(5,0,3,0);
   view.PortraitHover=RegisterHover("view/player/"+player,view.Portrait,"",true);
   if(cards==null){
    var identity=new StackPanel {Orientation=Orientation.Horizontal,IsHitTestVisible=false,Tag=OverviewPresentationPolicy.PassiveTag,Margin=new Thickness(2)};
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
   view.NameHover=RegisterHover("view/name/"+player,nameHost,"",true);content.Children.Add(header);
   var values=new Grid {IsHitTestVisible=false};Grid.SetRow(values,1);var flow=settings.DisplayValueGrid;int cols=flow.Columns(columns.Length);
   for(int i=0;i<cols;i++)values.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
   for(int i=0;i<flow.Rows(columns.Length);i++)values.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
   for(int i=0;i<columns.Length;i++){
    var metric=columns[i];var pair=new StackPanel {Orientation=Orientation.Horizontal,IsHitTestVisible=false,Margin=new Thickness(4,2,6,2)};
    var icon=MetricIcon(metric);if(metric==Metric.Popularity)view.PopularityIcon=(Image)icon;
    icon.IsHitTestVisible=false;icon.VerticalAlignment=VerticalAlignment.Center;pair.Children.Add(icon);
    var cell=OverviewAppearance.PlayerText("",null);cell.Margin=new Thickness(5,0,0,0);view.Cells.Add(metric,cell);pair.Children.Add(cell);
    RegisterHover("view/value/"+player+"/"+Metrics.Key(metric),icon,MetricHelp(metric));
    flow.Position(i,columns.Length,out int valueRow,out int valueColumn);Grid.SetRow(pair,valueRow);Grid.SetColumn(pair,valueColumn);values.Children.Add(pair);
   }
   content.Children.Add(values);
   var box=new Border {Background=StatisticsBackground(),Child=content,Margin=new Thickness(0,0,8,6),HorizontalAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Bottom,IsHitTestVisible=false,Tag=OverviewPresentationPolicy.PassiveTag};
   box.SizeChanged+=OnTableGeometryChanged;view.Root=box;cards.Children.Add(box);return view;
  }
  private static void UpdateHoverText(HoverTarget target,string text){if(target==null || target.Description==text)return;target.Description=target.DisplayText=text;}
  private static void ResetTable(){
   CancelHeaderGesture();RemoveHeaders(false);rowVisuals.Clear();realizedColumns=null;realizedLordGrid=realizedValueGrid=null;
   foreach(var table in tables)table.SizeChanged-=OnTableGeometryChanged;tables.Clear();cards=null;
   if(panel!=null){CloseTips(panel);panel.Children.Clear();}
  }
  private static void AddDivider(Grid table,int row,bool header){
   var line=new Border {Height=1,VerticalAlignment=VerticalAlignment.Bottom,IsHitTestVisible=false,Tag=OverviewPresentationPolicy.PassiveTag,Background=new SolidColorBrush(Color.FromArgb((byte)(header?100:45),220,220,220))};
   Grid.SetRow(line,row);Grid.SetColumnSpan(line,table.ColumnDefinitions.Count);table.Children.Add(line);
  }
  private static Button CreateHeader(HeaderSurface surface,Metric? metric,string help,FrameworkElement symbol){
   headerRevision++;symbol.IsHitTestVisible=false;symbol.HorizontalAlignment=HorizontalAlignment.Center;symbol.VerticalAlignment=VerticalAlignment.Center;
   var content=new Grid {Margin=new Thickness(2,0,2,0)};content.Children.Add(symbol);
   var arrow=new Canvas {Width=4,Height=5,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false,Visibility=Visibility.Collapsed};
   arrow.Children.Add(new Path {Fill=new SolidColorBrush(Color.FromRgb(255,240,191)),Data=Geometry.Parse("M0,0 L4,0 L2,3 Z")});content.Children.Add(arrow);
   var highlight=new Border {Child=content,Background=new SolidColorBrush(Color.FromArgb(0,0,0,0))};
   var button=new Button {Style=buttonStyle,Content=highlight,Padding=new Thickness(2),HorizontalAlignment=HorizontalAlignment.Stretch};
   var binding=new HeaderBinding {Surface=surface,Metric=metric,Highlight=highlight,Arrow=arrow,Menu=surface.Prefix.StartsWith("menu/",StringComparison.Ordinal)};
   if(surface.Wrap!=null)surface.Items.Add(button);
   if(binding.Menu)Tip(button,help+" — "+Text("HeaderDragHint"));
   if(metric.HasValue)BindMetricHeader(button,binding);
   else {headers.Add(button,binding);var action=UiAction(()=>{state.Select(null);Render();UpdateColumnsPopup();});button.Click+=(_,__)=>action();}
   if(!binding.Menu)RegisterHover(surface.Prefix+(metric.HasValue?Metrics.Key(metric.Value):"identity"),symbol,help);return button;
  }
  private static void UpdateHeaderEditing(){
   if(headerEditing==Editing && paintedHeaderRevision==headerRevision && paintedSort==state.Column && paintedDescending==state.Descending)return;
   paintedHeaderRevision=headerRevision;paintedSort=state.Column;paintedDescending=state.Descending;
   if(panel!=null)panel.Tag=OverviewPresentationPolicy.StatisticsTag(Editing);
   if(headerEditing!=Editing){headerEditing=Editing;editEpoch++;CancelHeaderGesture();ClosePassiveHover();}
   foreach(var pair in headers){
    pair.Key.IsHitTestVisible=Editing;pair.Key.Focusable=Editing;
    pair.Value.Arrow.Visibility=state.Column==pair.Value.Metric?Visibility.Visible:Visibility.Collapsed;
    pair.Value.Arrow.RenderTransform=state.Descending?null:new ScaleTransform {ScaleX=1,ScaleY=-1,CenterX=2,CenterY=2};
   }
   foreach(var surface in headerSurfaces.Where(s=>s.Prefix.StartsWith("view/",StringComparison.Ordinal)))
    foreach(var target in hoverTargets.Where(p=>p.Key.StartsWith(surface.Prefix,StringComparison.Ordinal)))
     target.Value.DisplayText=target.Value.Description+(Editing?" — "+Text("Sort")+" — "+Text("HeaderDragHint"):"");
  }
  private static void OnTableGeometryChanged(object sender,SizeChangedEventArgs args){CancelHeaderGesture();MarkHoverGeometryDirty();if(initialMenuPlacement)PositionSettingsMenu();}
 }
}
