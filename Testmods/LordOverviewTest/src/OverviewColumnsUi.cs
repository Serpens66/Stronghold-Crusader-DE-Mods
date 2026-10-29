using System;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;
using CrusaderDE;
using Noesis;
using SHCDESE.Interop;
namespace LordOverviewTest {
 internal static partial class OverviewRuntime {
  private static Popup columnsPopup;
  private static Border popupAnchor;
  private static UIElement inputRoot;
  private static FrameworkElement gearButton;
  private static readonly ColumnMenuState menuState=new ColumnMenuState();
  private static readonly Dictionary<Metric,CheckBox> metricChecks=new Dictionary<Metric,CheckBox>();
  private static readonly Dictionary<string,Grid> metricPages=new Dictionary<string,Grid>();
  private static readonly Dictionary<string,Border> categoryTabs=new Dictionary<string,Border>();
  private static ScrollViewer metricScroll;
  private static TextBlock columnCount;
  private static bool restoringScroll;
  private static Action UiAction(Action action){
   long expected=System.Threading.Interlocked.Read(ref session);int token=System.Threading.Volatile.Read(ref generation);var view=panel;
   return ()=>Post(()=>{if(expected>0 && expected==System.Threading.Interlocked.Read(ref session) && token==System.Threading.Volatile.Read(ref generation) &&
    ReferenceEquals(view,panel) && panel?.IsVisible==true && settings.EnableMod)action();});
  }
  private static void InvalidateInformation(){long id=System.Threading.Interlocked.Read(ref session);Post(()=>{
   if(id!=System.Threading.Interlocked.Read(ref session))return;
   CancelHeaderGesture();
   rows=rows.Select(row=>{var values=(long?[])row.Values.Clone();for(int i=2;i<values.Length;i++)values[i]=null;return new LordRow(row.Player,row.Alive,values);}).ToList();
   signature="";if(panel?.IsVisible==true)Render();
  });}
  private static void OnSettingsChanged(object sender,PropertyChangedEventArgs args){
   if(args.PropertyName!=nameof(OverviewSettings.Columns) && args.PropertyName!=nameof(OverviewSettings.EnableMod) &&
    args.PropertyName!=nameof(OverviewSettings.IsLocalSettingsHost) && args.PropertyName!=nameof(OverviewSettings.IsRealMultiplayerContext) &&
    args.PropertyName!=nameof(OverviewSettings.CanEditClientSettings))return;
   long id=System.Threading.Interlocked.Read(ref session);
   Post(()=>{
    settings.FlushLocalColumns();
    if(id!=System.Threading.Interlocked.Read(ref session))return;
    if(args.PropertyName==nameof(OverviewSettings.IsLocalSettingsHost) || args.PropertyName==nameof(OverviewSettings.IsRealMultiplayerContext))InvalidateInformation();
    if(args.PropertyName==nameof(OverviewSettings.EnableMod)){
     registration?.SetVisible(id>0 && settings.EnableMod);if(!settings.EnableMod)Close();
    }
    if(args.PropertyName==nameof(OverviewSettings.Columns)){
     selection.Load(settings.DisplayColumns);state.Reconcile(selection.Columns);signature="";
     CancelHeaderGesture();if(panel?.IsVisible==true)Render();UpdateColumnsPopup();
    }
   });
  }
  private static void AttachColumnsPopup(){
   popupAnchor=new Border {Width=380,Height=1,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,IsHitTestVisible=false};parent.Children.Add(popupAnchor);
   columnsPopup=new Popup {PlacementTarget=popupAnchor,Placement=PlacementMode.Top,StaysOpen=true};
   columnsPopup.Closed+=OnColumnsClosed;parent.Children.Add(columnsPopup);
   inputRoot=parent;while(VisualTreeHelper.GetParent(inputRoot) is UIElement ancestor)inputRoot=ancestor;
   inputRoot.PreviewMouseDown+=OnColumnsOutsideClick;
   inputRoot.PreviewKeyDown+=OnHeaderKeyDown;
  }
  private static bool ContainsClick(FrameworkElement element,MouseButtonEventArgs args){if(element==null || !element.IsVisible)return false;var point=args.GetPosition(element);return point.X>=0 && point.Y>=0 && point.X<element.ActualWidth && point.Y<element.ActualHeight;}
  private static void OnColumnsOutsideClick(object sender,MouseButtonEventArgs args){
   if(columnsPopup?.IsOpen==true && !ContainsClick(columnsPopup.Child as FrameworkElement,args) && !ContainsClick(gearButton,args))CloseColumnsPopup();
   // Never mark the preview event handled: the original map/HUD action must still receive it.
  }
  private static void OnColumnsClosed(object sender,Noesis.EventArgs args){if(metricScroll!=null && !restoringScroll)menuState.Remember(metricScroll.VerticalOffset);if(columnsPopup?.Child!=null)CloseTips(columnsPopup.Child);}
  private static void CloseColumnsPopup(){if(columnsPopup==null)return;if(columnsPopup.Child!=null)CloseTips(columnsPopup.Child);columnsPopup.IsOpen=false;}
  private static void DetachColumnsPopup(){if(inputRoot!=null){inputRoot.PreviewMouseDown-=OnColumnsOutsideClick;inputRoot.PreviewKeyDown-=OnHeaderKeyDown;}inputRoot=null;gearButton=null;metricChecks.Clear();metricPages.Clear();categoryTabs.Clear();metricScroll=null;columnCount=null;restoringScroll=false;if(popupAnchor!=null)parent?.Children.Remove(popupAnchor);popupAnchor=null;if(columnsPopup==null)return;columnsPopup.Closed-=OnColumnsClosed;parent?.Children.Remove(columnsPopup);columnsPopup.Child=null;columnsPopup.PlacementTarget=null;columnsPopup=null;}
  private static string MetricHelp(Metric metric)=>providers.TryGetValue(metric,out var provider)?provider.GetTooltip():((int)metric<0?Metrics.Key(metric)+" — "+Text("Unavailable"):Text(metric.ToString())+(Metrics.Get(metric).Historical?" — "+Text("SinceStart"):" — "+Text(metric==Metric.Fear?"FearHelp":"CurrentValue")));
  private static Button ActionButton(string title,string help,Action action,bool enabled=true){
   var button=new Button {Style=buttonStyle,Content=Label(title),IsEnabled=enabled,Opacity=enabled?1:0.4f,Padding=new Thickness(3)};
   var execute=UiAction(action);button.Click+=(_,__)=>execute();Tip(button,help);return button;
  }
  private static void ApplyColumns(){
   CancelHeaderGesture();state.Reconcile(selection.Columns);settings.SetDisplayColumns(Metrics.Serialize(selection.Columns));signature="";
   Render();UpdateColumnsPopup();
  }
  private static void BuildColumnsPopup(){
   if(columnsPopup==null || columnsPopup.Child!=null)return;
   var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
   layout.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
   var head=new DockPanel();var close=ActionButton("X",Text("Close"),CloseColumnsPopup);DockPanel.SetDock(close,Dock.Right);head.Children.Add(close);
   columnCount=Label("");columnCount.Margin=new Thickness(3,2,6,6);head.Children.Add(columnCount);layout.Children.Add(head);
   var tabs=new Grid {Margin=new Thickness(0,0,0,6)};Grid.SetRow(tabs,1);
   for(int i=0;i<Metrics.Groups.Length;i++){
    string group=Metrics.Groups[i];tabs.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});
    var button=ActionButton("",Text(group),()=>SelectCategory(group));var name=Label(Text(group));name.FontSize=13;name.TextWrapping=TextWrapping.Wrap;name.TextAlignment=TextAlignment.Center;
    var border=new Border {Child=name,Padding=new Thickness(3),MinHeight=36,BorderThickness=new Thickness(1),BorderBrush=new SolidColorBrush(Color.FromRgb(242,212,138))};
    categoryTabs[group]=border;button.Content=border;Grid.SetColumn(button,i);tabs.Children.Add(button);
   }
   layout.Children.Add(tabs);var pages=new Grid {Margin=new Thickness(0,0,22,0)}; // Explicit gutter: Vanilla's scrollbar skin may overlay its presenter.
   foreach(string group in Metrics.Groups){
    var page=new Grid();page.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});page.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});
    metricPages[group]=page;pages.Children.Add(page);
   }
   metricScroll=new ScrollViewer {Content=pages,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Height=240};
   metricScroll.ScrollChanged+=(sender,__)=>{if(ReferenceEquals(sender,metricScroll) && !restoringScroll && columnsPopup?.IsOpen==true)menuState.Remember(metricScroll.VerticalOffset);};Grid.SetRow(metricScroll,2);layout.Children.Add(metricScroll);
   var foot=new StackPanel {Margin=new Thickness(0,6,0,0)};
   foot.Children.Add(ActionButton(Text("ResetColumns"),Text("ResetColumnsHelp"),()=>{selection.Load(Metrics.Serialize(Metrics.Defaults));ApplyColumns();}));
   var hint=Label(Text("HeaderDragHint"));hint.FontSize=12;hint.MaxWidth=350;hint.TextWrapping=TextWrapping.Wrap;hint.Margin=new Thickness(3,5,3,0);foot.Children.Add(hint);Grid.SetRow(foot,3);layout.Children.Add(foot);
   columnsPopup.Child=new Border {Width=380,Background=new SolidColorBrush(Color.FromRgb(29,23,16)),BorderBrush=new SolidColorBrush(Color.FromRgb(242,212,138)),BorderThickness=new Thickness(1),Padding=new Thickness(6),Child=layout};
   SyncColumnChoices();UpdateColumnsPopup();
  }
  private static void UpdateColumnsPopup(){if(columnCount==null)return;var columns=selection.Columns;columnCount.Text=Text("ChooseColumns")+" · "+string.Format(Text("ColumnCount"),columns.Length);
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
     label.Children.Add(MetricIcon(metric));var name=Label(MetricName(metric));name.FontSize=14;name.MaxWidth=125;name.TextWrapping=TextWrapping.Wrap;name.Margin=new Thickness(5,0,0,0);Grid.SetColumn(name,1);label.Children.Add(name);checkbox.Content=label;
    }
    int index=0;page.RowDefinitions.Clear();
    foreach(var pair in metricChecks.Where(p=>VisualTreeHelper.GetParent(p.Value)==page)){
     bool visible=definitions.Any(d=>d.Id==pair.Key);pair.Value.Visibility=visible?Visibility.Visible:Visibility.Collapsed;if(!visible)continue;
     if(index%2==0)page.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});Grid.SetRow(pair.Value,index/2);Grid.SetColumn(pair.Value,index%2);index++;
    }
   }
   // Do not force layout on a popup outside an attached Noesis view.
   var scroll=metricScroll;long expected=System.Threading.Interlocked.Read(ref session);int token=System.Threading.Volatile.Read(ref generation);
   if(columnsPopup.IsOpen)Post(()=>{if(!ReferenceEquals(scroll,metricScroll) || expected!=System.Threading.Interlocked.Read(ref session) || token!=System.Threading.Volatile.Read(ref generation))return;scroll.ScrollToVerticalOffset((float)offset);restoringScroll=false;});
   else restoringScroll=false;
  }
  private static void SelectCategory(string group){if(group==menuState.Group)return;menuState.Select(group,metricScroll.VerticalOffset);ShowCategory();}
  private static void ShowCategory(){
   restoringScroll=true;foreach(var page in metricPages)page.Value.Visibility=page.Key==menuState.Group?Visibility.Visible:Visibility.Collapsed;
   foreach(var tab in categoryTabs){bool selected=tab.Key==menuState.Group;tab.Value.Background=new SolidColorBrush(selected?Color.FromArgb(110,145,98,45):Color.FromArgb(0,0,0,0));tab.Value.Opacity=selected?1:0.65f;}
   var scroll=metricScroll;string group=menuState.Group;double offset=menuState.Offset;long id=System.Threading.Interlocked.Read(ref session);int token=System.Threading.Volatile.Read(ref generation);
   Post(()=>{if(!ReferenceEquals(scroll,metricScroll) || group!=menuState.Group || id!=System.Threading.Interlocked.Read(ref session) || token!=System.Threading.Volatile.Read(ref generation))return;scroll.ScrollToVerticalOffset((float)offset);restoringScroll=false;});
  }
  private static void OpenColumnsPopup(){
   if(columnsPopup==null)return;BuildColumnsPopup();UpdateColumnsPopup();float height=parent.ActualHeight;
   bool above=height-panel.ActualHeight>=270;columnsPopup.Placement=above?PlacementMode.Top:PlacementMode.Right;
   popupAnchor.Width=above?380:1;popupAnchor.Margin=new Thickness(above?40:40+panel.ActualWidth,(float)0,0,panel.ActualHeight+2);
   metricScroll.Height=Math.Max(60,Math.Min(240,(above?height-panel.ActualHeight:height)-160));
   columnsPopup.IsOpen=true;ShowCategory();
  }
  private static FrameworkElement GearIcon(){
   string teeth="";for(int angle=0;angle<360;angle+=45)teeth+="<Rectangle Canvas.Left='8' Canvas.Top='0' Width='2' Height='4' Fill='#FFF2D48A'><Rectangle.RenderTransform><RotateTransform Angle='"+angle+"' CenterX='1' CenterY='9'/></Rectangle.RenderTransform></Rectangle>";
   return GUI.ParseXaml("<Canvas xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Width='18' Height='18'>"+teeth+"<Ellipse Width='12' Height='12' Canvas.Left='3' Canvas.Top='3' Stroke='#FFF2D48A' StrokeThickness='2'/><Ellipse Width='3' Height='3' Canvas.Left='7.5' Canvas.Top='7.5' Fill='#FFF2D48A'/></Canvas>") as FrameworkElement;
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
