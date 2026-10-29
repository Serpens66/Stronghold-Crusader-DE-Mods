using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using APIShared;
using APIShared.GameModes;
using BepInEx.Logging;
using CrusaderDE;
using Noesis;
using SHCDESE.API;
using SHCDESE.Interop;
namespace LordOverviewTest {
 internal static partial class OverviewRuntime {
  private static ManualLogSource log;
  private static int started,pending,shown,generation;
  private static double[] widths;
  private static OverviewSettings settings;
  private static GameModeSnapshot? mode;
  private static readonly ColumnSelection selection=new ColumnSelection();
  private static long session,lastSampleSession;
  private static readonly RefreshGate cadence=new RefreshGate();
  private static OverviewState state=new OverviewState();
  private static List<LordRow> rows=new List<LordRow>();
  private static IHudExtrasButtonRegistration registration;
  private static Grid panel,parent;
  private static FrameworkElement sourceHost;
  private static MainViewModel main;
  private static Style buttonStyle,tipStyle;
  private static string signature="";
  private static string headerLayout="";
  private static int errors;
  private static string Text(string key)=>SerpLocalization.Get(key);
  internal static void Start(ManualLogSource logger,OverviewSettings preferences) {
   if(Interlocked.Exchange(ref started,1)!=0)return;log=logger;settings=preferences;
   selection.Load(settings.DisplayColumns);settings.PropertyChanged+=OnSettingsChanged;settings.InformationRightsInvalidated+=InvalidateInformation;
   UnityEngine.Application.focusChanged+=OnOverviewFocusChanged;
   TooltipProbeStartup.Start(logger);
   ApiShared.ForMod("LordOverviewTest_Serp").WhenReady(client=>{
    if(!client.TryGetLobbyState(out var lobby,out var lobbyReason) || !lobby.TryRegisterObserver("overview-permission",settings.ObserveLobby,out lobbyReason))throw new InvalidOperationException(lobbyReason.Reason);
    if(!client.TryGetMissionLifecycle(out var lifecycle,out var reason)){Error(new Exception(reason.Reason));return;}
    if(!lifecycle.TryRegisterObserver("overview",n=>{
     long id=n.Context.SessionId;Interlocked.Exchange(ref session,id);
     Post(()=>{if(id!=Interlocked.Read(ref session))return;Close();menuState.Reset();mode=n.Context.Mode;selection.Load(settings.DisplayColumns);state=new OverviewState();rows.Clear();registration?.RequestContentRefresh();registration?.SetVisible(settings.EnableMod);Message("SESSION_START");});
    },n=>{Interlocked.Exchange(ref session,0);Post(()=>{if(Interlocked.Read(ref session)!=0)return;Close();Detach();mode=null;rows.Clear();registration?.SetVisible(false);settings.FlushLocalColumns();});},null,out reason)){Error(new Exception(reason.Reason));return;}
    if(!client.TryGetHudExtrasButtons(out var buttons,out reason)){Error(new Exception(reason.Reason));return;}
    var definition=new HudExtrasButtonDefinition("overview",new Command(Toggle),Text("Title"),context=>{
     Attach(context.Hud);
     return OverviewAppearance.CreateButton(context.Hud);
    });
    if(!buttons.TryRegisterButton(definition,out registration,out reason)){Error(new Exception(reason.Reason));return;}
    registration.SetVisible(Interlocked.Read(ref session)>0 && settings.EnableMod);
    GameTimeManagerAPI.Instance.OnTick+=OnTick;
    Message("READY");
   });
  }
  private static void Post(Action action)=>UnityMainThreadDispatcher.EnqueueStatic(()=>{try{action();}catch(Exception e){Close();Error(e);}});
  private static void OnTick(int tick) {
   // This simulation callback only counts; all native reads and UI work run on the main thread.
   if(!cadence.Tick(Volatile.Read(ref shown)!=0))return;
   if(Interlocked.Exchange(ref pending,1)!=0)return;
   long id=Interlocked.Read(ref session);int token=Volatile.Read(ref generation);
   Post(()=>{try{if(id>0 && id==Interlocked.Read(ref session) && token==Volatile.Read(ref generation) && Volatile.Read(ref shown)!=0)Refresh();}finally{Interlocked.Exchange(ref pending,0);}});
  }
  private static void Attach(FrameworkElement host) {
   if(ReferenceEquals(sourceHost,host))return;
   Detach();sourceHost=host;parent=VisualTreeHelper.GetParent(host) as Grid;
   if(parent==null)throw new InvalidOperationException("Side-HUD parent missing.");
   main=MainViewModel.Instance;main.PropertyChanged+=OnChanged;
   panel=new Grid {HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(40,0,0,0),Visibility=Visibility.Collapsed,Background=new SolidColorBrush(Noesis.Color.FromRgb(96,96,96)){Opacity=0.5f}};
   Panel.SetZIndex(panel,15);parent.Children.Add(panel);panel.IsVisibleChanged+=OnPanelVisibility;
   buttonStyle=GUI.ParseXaml("<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='{x:Type Button}' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Button}'><Border Background='Transparent' Padding='3'><ContentPresenter/></Border></ControlTemplate></Setter.Value></Setter></Style>") as Style;
   tipStyle=host.TryFindResource("APISharedHudExtrasTooltipStyle") as Style;
   if(tipStyle==null)throw new InvalidOperationException("Shared tooltip style unavailable.");
   TooltipProbeStartup.Observe(ref tipStyle,"LordOverviewTest.HUD");
   AttachColumnsPopup();
  }
  private static void OnPanelVisibility(object sender,DependencyPropertyChangedEventArgs e){bool visible=panel?.IsVisible==true;Volatile.Write(ref shown,visible?1:0);if(!visible){CancelHeaderGesture();CloseColumnsPopup();}}
  private static void OnChanged(object sender,PropertyChangedEventArgs args) {
   if(args.PropertyName==nameof(MainViewModel.Show_HUD_Goods) && main.Show_HUD_Goods || args.PropertyName==nameof(MainViewModel.Show_HUD_Objectives) && main.Show_HUD_Objectives)Close();
   if(args.PropertyName==nameof(MainViewModel.Show_HUD_Extras) && !main.Show_HUD_Extras)Close();
  }
  private static void Toggle() {
   if(Interlocked.Read(ref session)<=0 || !settings.EnableMod || panel==null || !panel.IsLoaded)return;
   if(Volatile.Read(ref shown)!=0){Close();return;}
   main.SetGoodsPopupState(false);main.SetObjectivePopupState(false);
   panel.Visibility=Visibility.Visible;Volatile.Write(ref shown,1);signature="";Refresh();
  }
  private static void Close(){CancelHeaderGesture();CloseColumnsPopup();Interlocked.Increment(ref generation);Volatile.Write(ref shown,0);if(panel!=null){CloseTips(panel);panel.Visibility=Visibility.Collapsed;}}
  private static void CloseTips(DependencyObject root){if(ToolTipService.GetToolTip(root) is ToolTip tip)tip.IsOpen=false;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)CloseTips(VisualTreeHelper.GetChild(root,i));}
  private static void Detach(){Close();DetachColumnsPopup();metricHeaders.Clear();headerHighlights.Clear();tableHeader=null;insertionMarker=null;headerLayout="";if(main!=null)main.PropertyChanged-=OnChanged;if(panel!=null){panel.IsVisibleChanged-=OnPanelVisibility;parent?.Children.Remove(panel);}panel=null;parent=null;sourceHost=null;main=null;signature="";}
  private static void Refresh() {
   if(panel==null || !panel.IsLoaded || !panel.IsVisible || GameData.Instance?.lastGameState==null)return;
   rows=OverviewData.Capture(selection.Columns,settings,mode);
   long id=Interlocked.Read(ref session);if(lastSampleSession!=id){lastSampleSession=id;Message("FIRST_SAMPLE session="+id+" players="+rows.Count);}
   Render();
  }
  private static void Render() {
   if(headerGesture.Active)return;
   var columns=selection.Columns;state.Reconcile(columns);widths=new double[columns.Length];
   var ordered=state.Sort(rows);
   for(int i=0;i<columns.Length;i++){var metric=Metrics.Get(columns[i]);widths[i]=Math.Max(metric.MinimumWidth,ordered.Count==0?metric.MinimumWidth:ordered.Max(r=>metric.Format(r.Get(metric.Id)).Length)*8.5+14);}
   var colors = ordered.ToDictionary(r=>r.Player, r=>ResolvePlayerColor(r.Player));
   string next=Metrics.Serialize(columns)+"/"+string.Join(";",ordered.Select(r=>r.Player+"="+colors[r.Player].ToString()))+"/"+state.Column+"/"+state.Descending+"/"+string.Join(";",ordered.Select(r=>r.Player+":"+r.Alive+":"+string.Join(",",r.Values)));
   if(next==signature)return;signature=next;
   string layout=Metrics.Serialize(columns)+"/"+state.Column+"/"+state.Descending+"/"+string.Join(",",widths);
   if(tableHeader!=null && headerLayout==layout && panel.Children.Count>0){
    // Keep hovered/clicked header controls alive across routine statistic updates.
    while(panel.Children.Count>1){int last=panel.Children.Count-1;CloseTips(panel.Children[last]);panel.Children.RemoveAt(last);}
    while(panel.RowDefinitions.Count>1)panel.RowDefinitions.RemoveAt(panel.RowDefinitions.Count-1);
   }else{
    CloseTips(panel);metricHeaders.Clear();headerHighlights.Clear();panel.Children.Clear();panel.RowDefinitions.Clear();
    panel.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
    var header=Line();AddHeader(header,0,null,Text("Player"),null);
    for(int col=0;col<columns.Length;col++)AddHeader(header,col+1,columns[col],MetricHelp(columns[col]),MetricIcon(columns[col]));
    var gear=ActionButton("",Text("ColumnsHelp"),()=>{if(columnsPopup.IsOpen)CloseColumnsPopup();else OpenColumnsPopup();});gear.Content=GearIcon();gearButton=gear;
    Grid.SetColumn(gear,columns.Length+1);header.Children.Add(gear);AttachHeaderMarker(header);AddRow(header,0,true);headerLayout=layout;
   }
   int index=1;
   foreach(var row in ordered){panel.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});var line=Line();line.Opacity=row.Alive?1:0.45f;Grid.SetRow(line,index++);
    var identity=new StackPanel {Orientation=Orientation.Horizontal};var snapshot=GameData.Instance.lastGameState;
    var face=snapshot.is_skirmish_player(row.Player)?main.getAIFace(snapshot.computer_register[row.Player],true):Platform_Multiplayer.Instance.GetUserAvatar(Platform_Multiplayer.Instance.getPlayerSteamID(row.Player));
    identity.Children.Add(OverviewAppearance.Portrait(face));
    var color=new SolidColorBrush(colors[row.Player]);
    var number=OverviewAppearance.PlayerText(row.Player.ToString(),color);number.Margin=new Thickness(5,0,3,0);identity.Children.Add(number);
    Tip(identity,Platform_Multiplayer.Instance.getSkirmishName(row.Player));line.Children.Add(identity);
    for(int col=0;col<columns.Length;col++){var metric=Metrics.Get(columns[col]);long? numberValue=row.Get(metric.Id);var value=OverviewAppearance.PlayerText(metric.Format(numberValue),color);value.Background=new SolidColorBrush(Color.FromArgb(0,0,0,0));value.HorizontalAlignment=HorizontalAlignment.Right;value.Margin=new Thickness(8,0,6,0);Tip(value,MetricHelp(metric.Id)+(numberValue.HasValue?"": " — "+Text("Unavailable")));Grid.SetColumn(value,col+1);line.Children.Add(value);}AddRow(line,index-1,false);
   }
  }
  private static Noesis.Color ResolvePlayerColor(int player) {
   var mapping=SpriteMapping.remapColours;
   int index=mapping!=null && player>0 && player<mapping.Length?mapping[player]:0;
   if(mapping==null || index<=0 || index>=mapping.Length || spriteLoader.instance==null)return Noesis.Color.FromRgb(255,255,255);
   UnityEngine.Color color;
   spriteLoader.instance.GetGMMaterial(global::Enums.GM.GM_BODY_ARCHER,index,0,out color,0);
   return Noesis.Color.FromRgb((byte)(color.r*255f),(byte)(color.g*255f),(byte)(color.b*255f));
  }
  private static void AddRow(Grid content,int row,bool header){
   Grid.SetRow(content,0);var border=new Border {Child=content,BorderThickness=new Thickness(0,0,0,1),BorderBrush=new SolidColorBrush(Noesis.Color.FromArgb((byte)(header?100:45),220,220,220))};Grid.SetRow(border,row);panel.Children.Add(border);
  }
  private static Grid Line(){var g=new Grid {Margin=new Thickness(5,2,5,2)};g.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(46)});foreach(var width in widths)g.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength((float)width)});g.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(24)});return g;}
  private static TextBlock Label(string text)=>new TextBlock {Text=text,FontSize=16,Foreground=new SolidColorBrush(Noesis.Color.FromArgb(255,255,240,191)),VerticalAlignment=VerticalAlignment.Center,Effect=new DropShadowEffect {ShadowDepth=1,BlurRadius=1,Color=Noesis.Color.FromArgb(255,0,0,0)}};
  private static void AddHeader(Grid line,int column,Metric? sort,string text,FrameworkElement icon){var content=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};if(icon!=null)content.Children.Add(icon);else content.Children.Add(Label("#"));if(state.Column==sort){var arrow=Label(state.Descending?"▼":"▲");arrow.FontSize=9;content.Children.Add(arrow);}var highlight=new Border {Child=content,Background=new SolidColorBrush(Color.FromArgb(0,0,0,0))};var button=new Button {Style=buttonStyle,Content=highlight,HorizontalAlignment=HorizontalAlignment.Stretch};
   if(sort.HasValue)BindMetricHeader(button,sort.Value,highlight);else{var execute=UiAction(()=>{state.Select(null);signature="";Render();});button.Click+=(_,__)=>execute();}
   Tip(button,text+" — "+Text("Sort")+(sort.HasValue?" — "+Text("HeaderDragHint"):""));Grid.SetColumn(button,column);line.Children.Add(button);}
  private static void Tip(DependencyObject element,string text){ToolTipService.SetToolTip(element,new ToolTip {Style=tipStyle,Content=text});ToolTipService.SetShowDuration(element,60000);ToolTipService.SetShowOnDisabled(element,true);}
  private static void Message(string text)=>log.LogInfo("["+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")+"] LORD_OVERVIEW_"+text);
  private static void Error(Exception e){if(Interlocked.Increment(ref errors)>3)return;log.LogError("["+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")+"] LORD_OVERVIEW: "+e);}
  private sealed class Command:ICommand {private readonly Action run;internal Command(Action action){run=action;}public bool CanExecute(object value)=>Interlocked.Read(ref session)>0;public void Execute(object value)=>run();public event System.EventHandler CanExecuteChanged {add{} remove{}}}
 }
}
