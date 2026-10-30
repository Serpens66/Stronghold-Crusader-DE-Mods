using System;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;
using CrusaderDE;
using Noesis;
using SHCDESE.Interop;
namespace LordOverviewTest {
 internal static partial class OverviewRuntime {
  private static Border settingsWindow;
  private static UIElement inputRoot;
  private static readonly ColumnMenuState menuState=new ColumnMenuState();
  private static readonly Dictionary<Metric,CheckBox> metricChecks=new Dictionary<Metric,CheckBox>();
  private static readonly Dictionary<string,Grid> metricPages=new Dictionary<string,Grid>();
  private static readonly Dictionary<string,Border> categoryTabs=new Dictionary<string,Border>();
  private static ScrollViewer metricScroll;
  private static TextBlock columnCount;
  private static bool restoringScroll;
  private static Action UiAction(Action action,bool needsMenu=true)=>UiActions.Bind(action,CurrentUiContext,Post,needsMenu);
  private static void InvalidateInformation(){System.Threading.Interlocked.Increment(ref informationGeneration);long id=System.Threading.Interlocked.Read(ref session);Post(()=>{
   if(id!=System.Threading.Interlocked.Read(ref session))return;
   CancelHeaderGesture();OverviewData.Reset();
   rows=rows.Select(row=>{var values=(long?[])row.Values.Clone();for(int i=2;i<values.Length;i++)values[i]=null;return new LordRow(row.Player,row.Alive,values);}).ToList();
   if(panel?.IsVisible==true)Render();
  });}
  private static void OnSettingsChanged(object sender,PropertyChangedEventArgs args){
   bool layoutChanged=args.PropertyName==nameof(OverviewSettings.Design) || args.PropertyName==nameof(OverviewSettings.LordGrid) || args.PropertyName==nameof(OverviewSettings.ValueGrid);
   if(!layoutChanged && args.PropertyName!=nameof(OverviewSettings.Columns) && args.PropertyName!=nameof(OverviewSettings.EnableMod) &&
    args.PropertyName!=nameof(OverviewSettings.IsLocalSettingsHost) && args.PropertyName!=nameof(OverviewSettings.IsRealMultiplayerContext) &&
    args.PropertyName!=nameof(OverviewSettings.CanEditClientSettings))return;
   long id=System.Threading.Interlocked.Read(ref session);
   Post(()=>{
    settings.FlushLocalPreferences();
    if(id!=System.Threading.Interlocked.Read(ref session))return;
    if(args.PropertyName==nameof(OverviewSettings.IsLocalSettingsHost) || args.PropertyName==nameof(OverviewSettings.IsRealMultiplayerContext))InvalidateInformation();
    if(args.PropertyName==nameof(OverviewSettings.EnableMod)){
     registration?.SetVisible(id>0 && settings.EnableMod);if(!settings.EnableMod)Close();
    }
    if(args.PropertyName==nameof(OverviewSettings.Columns)){
     selection.Load(settings.DisplayColumns);state.Reconcile(selection.Columns);
     CancelHeaderGesture();if(panel?.IsVisible==true){Render();RequestRefresh();}UpdateColumnsPopup();
    }
    if(layoutChanged)ApplyLayout();
   });
  }
  private static void AttachSettingsMenu(){
   settingsWindow=new Border {Visibility=Visibility.Collapsed,Background=new SolidColorBrush(Color.FromRgb(29,23,16)),BorderBrush=new SolidColorBrush(Color.FromRgb(242,212,138)),BorderThickness=new Thickness(1),Padding=new Thickness(6),ClipToBounds=true};
   Panel.SetZIndex(settingsWindow,20);overlay.Children.Add(settingsWindow);settingsWindow.SizeChanged+=OnSettingsSizeChanged;
   inputRoot=parent;while(VisualTreeHelper.GetParent(inputRoot) is UIElement ancestor)inputRoot=ancestor;
   inputRoot.PreviewMouseDown+=OnColumnsOutsideClick;inputRoot.PreviewKeyDown+=OnHeaderKeyDown;
  }
  private static bool ContainsClick(FrameworkElement element,MouseButtonEventArgs args){if(element==null || !element.IsVisible)return false;var point=args.GetPosition(element);return point.X>=0 && point.Y>=0 && point.X<element.ActualWidth && point.Y<element.ActualHeight;}
  private static void OnColumnsOutsideClick(object sender,MouseButtonEventArgs args){
   if(Editing && !settingsPosition.Dragging && !LayoutDropDownOpen && !ContainsClick(settingsWindow,args) && !ContainsClick(crownButton,args) && !ContainsHeaderClick(args))CloseSettingsMenu();
   // Leave the original map/HUD action unhandled.
  }
  private static void CloseSettingsMenu(){
   CancelMenuDrag();initialMenuPlacement=false;if(settingsWindow==null)return;
   CloseLayoutDropDowns();if(metricScroll!=null && !restoringScroll)menuState.Remember(metricScroll.VerticalOffset);
   CloseTips(settingsWindow);settingsWindow.Visibility=Visibility.Collapsed;UpdateHeaderEditing();ClosePassiveHover();
  }
  private static void DetachSettingsMenu(){
   if(inputRoot!=null){inputRoot.PreviewMouseDown-=OnColumnsOutsideClick;inputRoot.PreviewKeyDown-=OnHeaderKeyDown;}inputRoot=null;
   DetachMenuDrag();metricChecks.Clear();metricPages.Clear();categoryTabs.Clear();metricScroll=null;columnCount=null;restoringScroll=false;
   RemoveHeaders(true);ResetLayoutMenu();if(settingsWindow!=null){settingsWindow.SizeChanged-=OnSettingsSizeChanged;overlay?.Children.Remove(settingsWindow);settingsWindow.Child=null;}settingsWindow=null;
  }
  private static string MetricHelp(Metric metric)=>providers.TryGetValue(metric,out var provider)?provider.GetTooltip():((int)metric<0?Metrics.Key(metric)+" — "+Text("Unavailable"):Text(metric.ToString())+(Metrics.Get(metric).Historical?" — "+Text("SinceStart"):" — "+Text(metric==Metric.Fear?"FearHelp":"CurrentValue")));
  private static Button ActionButton(string title,string help,Action action,bool enabled=true,bool needsMenu=true){
   var button=new Button {Style=buttonStyle,Content=Label(title),IsEnabled=enabled,Opacity=enabled?1:0.4f,Padding=new Thickness(3)};
   var execute=UiAction(action,needsMenu);button.Click+=(_,__)=>execute();Tip(button,help);return button;
  }
  private static void ApplyColumns(){
   CancelHeaderGesture();state.Reconcile(selection.Columns);settings.SetDisplayColumns(Metrics.Serialize(selection.Columns));
   Render();RequestRefresh();UpdateColumnsPopup();
  }
  private static void BuildSettingsMenu(){
   if(settingsWindow==null || settingsWindow.Child!=null)return;
   var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition {Height=new GridLength(1,GridUnitType.Star)});
   var head=new DockPanel {Margin=new Thickness(0,0,0,6)};
   var close=ActionButton("X",Text("Close"),CloseSettingsMenu);DockPanel.SetDock(close,Dock.Right);head.Children.Add(close);
   menuDragHandle=new Border {Background=new SolidColorBrush(Color.FromArgb(1,0,0,0)),Padding=new Thickness(4),Child=Label(Text("SettingsTitle"))};Tip(menuDragHandle,Text("MoveSettings"));head.Children.Add(menuDragHandle);AttachMenuDrag();layout.Children.Add(head);
   var body=new Grid {MinWidth=480};body.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(200)});body.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});
   var preferences=new ScrollViewer {Content=BuildLayoutMenu(),HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,0,10,0)};body.Children.Add(preferences);
   var choices=new Grid();Grid.SetColumn(choices,1);body.Children.Add(choices);
   choices.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});choices.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});choices.RowDefinitions.Add(new RowDefinition {Height=new GridLength(1,GridUnitType.Star)});choices.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
   columnCount=Label("");columnCount.FontSize=14;columnCount.Margin=new Thickness(3,2,3,6);choices.Children.Add(columnCount);
   var tabs=new Grid {Margin=new Thickness(0,0,0,6)};Grid.SetRow(tabs,1);tabs.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});tabs.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});
   tabs.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});tabs.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
   for(int i=0;i<Metrics.Groups.Length;i++){
    string group=Metrics.Groups[i];var button=ActionButton("",Text(group),()=>SelectCategory(group));var name=Label(Text(group));name.FontSize=12;name.TextWrapping=TextWrapping.Wrap;name.TextAlignment=TextAlignment.Center;
    var border=new Border {Child=name,Padding=new Thickness(2),MinHeight=24,BorderThickness=new Thickness(1),BorderBrush=new SolidColorBrush(Color.FromRgb(242,212,138))};
    categoryTabs[group]=border;button.Content=border;Grid.SetColumn(button,i%2);Grid.SetRow(button,i/2);tabs.Children.Add(button);
   }
   choices.Children.Add(tabs);var pages=new Grid {Margin=new Thickness(0,0,22,0)};
   foreach(string group in Metrics.Groups){var page=new Grid();page.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});page.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});metricPages[group]=page;pages.Children.Add(page);}
   metricScroll=new ScrollViewer {Content=pages,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
   metricScroll.ScrollChanged+=(sender,__)=>{if(ReferenceEquals(sender,metricScroll) && !restoringScroll && Editing)menuState.Remember(metricScroll.VerticalOffset);};Grid.SetRow(metricScroll,2);choices.Children.Add(metricScroll);
   var reset=ActionButton(Text("ResetColumns"),Text("ResetColumnsHelp"),()=>{selection.Load(Metrics.Serialize(Metrics.Defaults));ApplyColumns();});reset.Margin=new Thickness(0,4,0,0);Grid.SetRow(reset,3);choices.Children.Add(reset);
   menuScroll=new ScrollViewer {Content=body,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetRow(menuScroll,1);layout.Children.Add(menuScroll);settingsWindow.Child=layout;
   SyncColumnChoices();UpdateColumnsPopup();
  }
  private static void UpdateColumnsPopup(){if(columnCount==null)return;UpdateLayoutMenu();var columns=selection.Columns;columnCount.Text=Text("ChooseColumns")+" · "+string.Format(Text("ColumnCount"),columns.Length);
   foreach(var pair in metricChecks){bool selected=columns.Contains(pair.Key),enabled=selected?columns.Length>1:Available(pair.Key);var checkbox=pair.Value;checkbox.IsChecked=selected;checkbox.IsEnabled=enabled;checkbox.Opacity=enabled?1:0.45f;
    string help=MetricHelp(pair.Key)+(enabled?"":" — "+Text(selected?"MinimumColumns":"Unavailable"));
    if(ToolTipService.GetToolTip(checkbox) is ToolTip tip)tip.Content=help;else Tip(checkbox,help);
   }
  }
  private static void SyncColumnChoices(){
   if(metricScroll==null)return;
   double offset=metricScroll.VerticalOffset;restoringScroll=true;
   var chosen=selection.Columns;
   foreach(var group in Metrics.Groups){
    var page=metricPages[group];var definitions=Metrics.All.Where(d=>d.Group==group && ((int)d.Id>=0 || Available(d.Id) || chosen.Contains(d.Id))).ToArray();
    // Retain controls and the ScrollViewer. Reflow only cells whose catalog placement changed.
    foreach(var definition in definitions){var metric=definition.Id;
     if(!metricChecks.TryGetValue(metric,out var checkbox)){
      checkbox=new CheckBox {MinHeight=36,HorizontalContentAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(3),Foreground=new SolidColorBrush(Color.FromRgb(255,255,255))};
      var toggle=UiAction(()=>{if(selection.Toggle(metric))ApplyColumns();});checkbox.Click+=(_,__)=>toggle();metricChecks.Add(metric,checkbox);
     }
     if(VisualTreeHelper.GetParent(checkbox) is Grid previous && previous!=page)previous.Children.Remove(checkbox);
     if(!page.Children.Contains(checkbox))page.Children.Add(checkbox);
     var label=new Grid();label.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(18)});label.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});
     label.Children.Add(MetricIcon(metric));var name=Label(MetricName(metric));name.FontSize=13;name.MaxWidth=96;name.TextWrapping=TextWrapping.Wrap;name.Margin=new Thickness(5,0,0,0);Grid.SetColumn(name,1);label.Children.Add(name);checkbox.Content=label;
    }
    int index=0;page.RowDefinitions.Clear();
    foreach(var pair in metricChecks.Where(p=>VisualTreeHelper.GetParent(p.Value)==page)){
     bool visible=definitions.Any(d=>d.Id==pair.Key);pair.Value.Visibility=visible?Visibility.Visible:Visibility.Collapsed;if(!visible)continue;
     if(index%2==0)page.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});Grid.SetRow(pair.Value,index/2);Grid.SetColumn(pair.Value,index%2);index++;
    }
   }
   // Do not force layout on a popup outside an attached Noesis view.
   if(Editing)RestoreColumnScroll(offset,menuState.Group);
   else restoringScroll=false;
  }
  private static void SelectCategory(string group){if(group==menuState.Group)return;menuState.Select(group,metricScroll.VerticalOffset);ShowCategory();}
  private static void ShowCategory(){
   restoringScroll=true;foreach(var page in metricPages)page.Value.Visibility=page.Key==menuState.Group?Visibility.Visible:Visibility.Collapsed;
   foreach(var tab in categoryTabs){bool selected=tab.Key==menuState.Group;tab.Value.Background=new SolidColorBrush(selected?Color.FromArgb(110,145,98,45):Color.FromArgb(0,0,0,0));tab.Value.Opacity=selected?1:0.65f;}
   RestoreColumnScroll(menuState.Offset,menuState.Group);
  }
  private static void RestoreColumnScroll(double offset,string group){
   var scroll=metricScroll;long id=System.Threading.Interlocked.Read(ref session);int epoch=editEpoch;restoringScroll=true;
   Post(()=>{if(!ReferenceEquals(scroll,metricScroll))return;
    try {if(Editing && group==menuState.Group && id==System.Threading.Interlocked.Read(ref session) && epoch==editEpoch){restoringScroll=true;scroll.ScrollToVerticalOffset((float)offset);}}
    finally {restoringScroll=false;}
   });
  }
  private static FrameworkElement MetricIcon(Metric metric){
   if((int)metric<0){var image=providers.TryGetValue(metric,out var provider)?provider.GetIcon():null;return image!=null?(FrameworkElement)new Image {Source=image,Width=18,Height=18}:Label("?");}
   ImageSource source=null;
   switch(metric){
    case Metric.Gold:case Metric.GoldAcquired:source=main.getSmallGoodsIcon((int)eGoods.STORED_GOLD);break;
    case Metric.FoodProduced:source=main.getSmallGoodsIcon((int)eGoods.STORED_FOOD_BREAD);break;
    case Metric.WoodProduced:source=main.getSmallGoodsIcon((int)eGoods.STORED_WOOD_PLANKS);break;
    case Metric.StoneProduced:case Metric.BuildingsDestroyed:case Metric.BuildingsLost:source=main.getSmallGoodsIcon((int)eGoods.STORED_STONE_BLOCKS);break;
    case Metric.IronProduced:source=main.getSmallGoodsIcon((int)eGoods.STORED_IRON_INGOTS);break;
    case Metric.PitchProduced:source=main.getSmallGoodsIcon((int)eGoods.STORED_PITCH_REFINED);break;
    case Metric.WeaponsProduced:source=main.getSmallGoodsIcon((int)eGoods.STORED_SWORDS);break;
    case Metric.GoodsReceived:case Metric.GoodsSent:source=main.getAlliesGoodsIcon(-1);break;
    case Metric.Fear:source=main.GameSprites[115];break; // Vanilla GetPlayerFear(0): neutral historical fear icon, used only as a column symbol.
    case Metric.Popularity:case Metric.MaxPopulation:return GUI.ParseXaml("<Canvas xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Width='18' Height='18'><Ellipse Width='16' Height='16' Canvas.Left='1' Canvas.Top='1' Stroke='#FFF2D48A' StrokeThickness='1.5'/><Path Data='M5,6 L6,6 M12,6 L13,6 M5,10 Q9,16 13,10' Stroke='#FFF2D48A' StrokeThickness='1.5'/></Canvas>") as FrameworkElement;
    default:source=main.getAlliesGoodsIcon(-3);break; // Vanilla attack-order scroll.
   }
   var grid=new Grid {Width=18,Height=18};grid.Children.Add(new Image {Source=source});
   string badge=Metrics.Get(metric).Historical?"Σ":"";
   if(metric==Metric.TroopsLost || metric==Metric.BuildingsLost)badge="−";
   if(metric==Metric.GoodsReceived)badge="←";if(metric==Metric.GoodsSent)badge="→";
   if(badge!=""){var text=Label(badge);text.FontSize=9;text.HorizontalAlignment=HorizontalAlignment.Right;text.VerticalAlignment=VerticalAlignment.Bottom;grid.Children.Add(text);}
   return grid;
  }
 }
}
