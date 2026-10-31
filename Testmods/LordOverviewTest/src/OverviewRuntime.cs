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
  private static int started,shown,generation;
  private static double[] widths;
  private static OverviewSettings settings;
  private static GameModeSnapshot? mode;
  private static readonly ColumnSelection selection=new ColumnSelection();
  private static long session,lastSampleSession;
  private static readonly HybridRefreshGate sampling=new HybridRefreshGate();
  private static OverviewState state=new OverviewState();
  private static List<LordRow> rows=new List<LordRow>();
  private static IHudExtrasButtonRegistration registration;
  private static Grid panel,parent;
  private static Canvas overlay;
  private static FrameworkElement sourceHost;
  private static MainViewModel main;
  private static Style buttonStyle,tipStyle;
  private static int errors,informationGeneration;
  private static readonly UiRefreshQueue requestedRefresh=new UiRefreshQueue(CurrentUiContext,Post,Refresh);
  private static string Text(string key)=>SerpLocalization.Get(key);
  internal static void Start(ManualLogSource logger,OverviewSettings preferences) {
   if(Interlocked.Exchange(ref started,1)!=0)return;log=logger;settings=preferences;
   selection.Load(settings.DisplayColumns);settings.PropertyChanged+=OnSettingsChanged;settings.InformationRightsInvalidated+=InvalidateInformation;
   UnityEngine.Application.focusChanged+=OnOverviewFocusChanged;
   TooltipProbeStartup.Start(logger);Shared.DevDiag.Attach(logger);
   ApiShared.ForMod("LordOverviewTest_Serp").WhenReady(client=>{
    OverviewData.EnableReadbacks();StartExtensions();StartPassiveHover();
    if(!client.TryGetLobbyState(out var lobby,out var lobbyReason) || !lobby.TryRegisterObserver("overview-permission",settings.ObserveLobby,out lobbyReason))throw new InvalidOperationException(lobbyReason.Reason);
    if(!client.TryGetMissionLifecycle(out var lifecycle,out var reason)){Error(new Exception(reason.Reason));return;}
    if(!lifecycle.TryRegisterObserver("overview",n=>{
     long id=n.Context.SessionId;Interlocked.Exchange(ref session,id);
     Post(()=>{if(id!=Interlocked.Read(ref session))return;Close();OverviewData.Reset();sampling.Reset(Now);menuState.Reset();settingsPosition.Reset();mode=n.Context.Mode;selection.Load(settings.DisplayColumns);state=new OverviewState();rows.Clear();registration?.RequestContentRefresh();registration?.SetVisible(settings.EnableMod);Message("SESSION_START");});
    },n=>{Interlocked.Exchange(ref session,0);Post(()=>{if(Interlocked.Read(ref session)!=0)return;Close();Detach();OverviewData.Reset();sampling.Reset(Now);mode=null;rows.Clear();registration?.SetVisible(false);settings.FlushLocalPreferences();});},null,out reason)){Error(new Exception(reason.Reason));return;}
    if(!client.TryGetHudExtrasButtons(out var buttons,out reason)){Error(new Exception(reason.Reason));return;}
    var definition=new HudExtrasButtonDefinition("overview",new Command(Toggle),CrownTooltip,context=>{
     Attach(context.Hud);
     var button=OverviewAppearance.CreateButton(context.Hud);BindCrownButton(button);return button;
    },tooltipFactory:context=>CreateTip(CrownTooltip,context.Hud));
    if(!buttons.TryRegisterButton(definition,out registration,out reason)){Error(new Exception(reason.Reason));return;}
    registration.SetVisible(Interlocked.Read(ref session)>0 && settings.EnableMod);
    GameTimeManagerAPI.Instance.OnTick+=OnTick;
    Message("READY");
   });
  }
  private static void Post(Action action)=>UnityMainThreadDispatcher.EnqueueStatic(()=>{try{action();}catch(Exception e){Close();Error(e);}});
  private static double Now=>System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
  private static void OnTick(int tick){
   long elapsed=0;Shared.DevDiag.Start(ref elapsed);
   sampling.Tick(Volatile.Read(ref shown)!=0);
   Shared.DevDiag.Stop(ref elapsed,"overview.tick");
  }
  private static void OnOverviewGui(APIShared.Presentation.PresentationPostEventArgs args){
   DiagOverviewGui();
   if(Volatile.Read(ref shown)==0)return;
   OnHoverGui(args);
   if(args.OriginalCompleted)try{Refresh();}catch(Exception error){Close();Error(error);}
  }
  static partial void DiagOverviewGui();
  private static void Attach(FrameworkElement host) {
   if(ReferenceEquals(sourceHost,host))return;
   Detach();sourceHost=host;
   var extras=VisualTreeHelper.GetParent(host) as Grid;
   parent=extras==null?null:VisualTreeHelper.GetParent(extras) as Grid;
   if(parent==null || extras.Name!="HUD_ObjectivesPanel")throw new InvalidOperationException("Full viewport side-HUD contract unavailable.");
   main=MainViewModel.Instance;main.PropertyChanged+=OnChanged;
   overlay=new Canvas {ClipToBounds=false,Tag=OverviewPresentationPolicy.ContainerTag};
   Grid.SetRowSpan(overlay,Math.Max(1,parent.RowDefinitions.Count));Grid.SetColumnSpan(overlay,Math.Max(1,parent.ColumnDefinitions.Count));
   overlay.SetBinding(UIElement.VisibilityProperty,new Binding("Visibility"){Source=extras});
   // Same layer as the original extras; later Vanilla dialogs retain their normal precedence.
   Panel.SetZIndex(overlay,Panel.GetZIndex(extras));parent.Children.Insert(parent.Children.IndexOf(extras)+1,overlay);
   panel=new Grid {Visibility=Visibility.Collapsed,Tag=OverviewPresentationPolicy.PassiveTag};Canvas.SetLeft(panel,40);Canvas.SetBottom(panel,0);overlay.Children.Add(panel);
   panel.IsVisibleChanged+=OnPanelVisibility;parent.SizeChanged+=OnViewportSizeChanged;
   buttonStyle=GUI.ParseXaml("<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='{x:Type Button}' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Button}'><Border Background='Transparent' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border></ControlTemplate></Setter.Value></Setter></Style>") as Style;
   tipStyle=GUI.ParseXaml(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ToolTip}'>
    <Setter Property='Background' Value='#FF1D1710'/><Setter Property='Foreground' Value='White'/><Setter Property='BorderBrush' Value='#FFF2D48A'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ToolTip}'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}'><TextBlock Text='{TemplateBinding Content}' FontSize='{TemplateBinding FontSize}' Foreground='{TemplateBinding Foreground}' MaxWidth='{TemplateBinding MaxWidth}' TextWrapping='Wrap'/></Border></ControlTemplate></Setter.Value></Setter>
   </Style>") as Style;
   AttachSettingsMenu();

  }
  private static void OnPanelVisibility(object sender,DependencyPropertyChangedEventArgs e){bool visible=panel?.IsVisible==true;int previous=Interlocked.Exchange(ref shown,visible?1:0);if(!visible){sampling.Cancel();if(previous!=0)Interlocked.Increment(ref generation);CancelHeaderGesture();CloseSettingsMenu();ClosePassiveHover();}}
  private static void OnChanged(object sender,PropertyChangedEventArgs args) {
   if(args.PropertyName==nameof(MainViewModel.Show_HUD_Goods) && main.Show_HUD_Goods || args.PropertyName==nameof(MainViewModel.Show_HUD_Objectives) && main.Show_HUD_Objectives)Close();
   if(args.PropertyName==nameof(MainViewModel.Show_HUD_Extras) && !main.Show_HUD_Extras || args.PropertyName==nameof(MainViewModel.Show_InGameUI) && !main.Show_InGameUI)Close();
  }
  private static void Toggle() {
   if(Interlocked.Read(ref session)<=0 || !settings.EnableMod || panel==null || !panel.IsLoaded || !main.Show_InGameUI)return;
   if(Volatile.Read(ref shown)!=0){Close();return;}
   OpenOverview();
  }
  private static void OpenOverview(){
   if(Interlocked.Read(ref session)<=0 || !settings.EnableMod || panel==null || !panel.IsLoaded || main?.Show_InGameUI!=true)return;
   main.SetGoodsPopupState(false);main.SetObjectivePopupState(false);
   panel.Visibility=Visibility.Visible;Volatile.Write(ref shown,1);hoverFaulted=false;MarkHoverGeometryDirty();RequestRefresh();
  }
  private static void Close(){CancelCrownPress();CancelHeaderGesture();CloseSettingsMenu();ClosePassiveHover();Interlocked.Increment(ref generation);Volatile.Write(ref shown,0);sampling.Cancel();OverviewData.CancelPending();if(panel!=null){CloseTips(panel);panel.Visibility=Visibility.Collapsed;}}
  private static void CloseTips(DependencyObject root){if(ToolTipService.GetToolTip(root) is ToolTip tip)tip.IsOpen=false;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)CloseTips(VisualTreeHelper.GetChild(root,i));}
  private static void Detach(){Close();DetachCrownButton();DetachSettingsMenu();ResetTable();ClearHoverTargets();ResetMetricIcons();requestedRefresh.Cancel();
   if(hoverBubble!=null)overlay?.Children.Remove(hoverBubble);hoverBubble=null;hoverBubbleLabel=null;bubbleText=null;
   if(main!=null)main.PropertyChanged-=OnChanged;
   if(parent!=null)parent.SizeChanged-=OnViewportSizeChanged;
   if(panel!=null)panel.IsVisibleChanged-=OnPanelVisibility;
   if(overlay!=null){overlay.ClearValue(UIElement.VisibilityProperty);parent?.Children.Remove(overlay);}
   panel=null;overlay=null;parent=null;sourceHost=null;main=null;
  }

  private static UiActionContext CurrentUiContext()=>new UiActionContext(Interlocked.Read(ref session),Volatile.Read(ref generation),editEpoch,panel,panel?.IsVisible==true,settings.EnableMod,Editing);
  private static void RequestRefresh(){
   if(panel?.IsVisible!=true)return;
   var stamp=CurrentUiContext();int rights=Volatile.Read(ref informationGeneration);
   var cached=OverviewData.FromCache(selection.Columns,settings,mode,rows,stamp.Session,rights);
   if(cached!=null && stamp.Allows(CurrentUiContext(),false) && rights==Volatile.Read(ref informationGeneration)){rows=cached;Render();}
   sampling.Request();requestedRefresh.Request();
  }
  private static void Refresh(){
   if(panel?.IsVisible!=true || !panel.IsLoaded || GameData.Instance?.lastGameState==null)return;
   double now=Now;
   if(OverviewData.HasPending){if(!OverviewData.PollPending(now))return;}
   else if(!sampling.TryBegin(now))return;
   CaptureAndPublish(now);
  }
  private static void CaptureAndPublish(double now){
   bool success=false;
   try {
    success=UiSamples.TryRead(CurrentUiContext,()=>Volatile.Read(ref informationGeneration),
     ()=>OverviewData.Capture(selection.Columns,settings,mode,rows,Interlocked.Read(ref session),Volatile.Read(ref informationGeneration),Volatile.Read(ref generation),now),ReadExtensions,sample=>{
      rows=sample;long id=Interlocked.Read(ref session);if(lastSampleSession!=id){lastSampleSession=id;Message("FIRST_SAMPLE session="+id+" players="+rows.Count);}Render();
     });
   }finally{sampling.End(Now,success);}
  }
  private static Noesis.Color ResolvePlayerColor(int player) {
   var mapping=SpriteMapping.remapColours;
   int index=OverviewLayout.ColorIndex(player,SpriteMapping.RemapMPLoadedColour,mapping);
   if(mapping==null || index<=0 || index>=mapping.Length || spriteLoader.instance==null)return Noesis.Color.FromRgb(255,255,255);
   UnityEngine.Color color;
   spriteLoader.instance.GetGMMaterial(global::Enums.GM.GM_BODY_ARCHER,index,0,out color,0);
   return Noesis.Color.FromRgb((byte)(color.r*255f),(byte)(color.g*255f),(byte)(color.b*255f));
  }
  private static TextBlock Label(string text)=>new TextBlock {Text=text,FontSize=16,Foreground=new SolidColorBrush(Noesis.Color.FromArgb(255,255,240,191)),VerticalAlignment=VerticalAlignment.Center,Effect=new DropShadowEffect {ShadowDepth=1,BlurRadius=1,Color=Noesis.Color.FromArgb(255,0,0,0)}};
  private static void Tip(DependencyObject element,string text){ToolTipService.SetToolTip(element,CreateTip(text,element as FrameworkElement??sourceHost));ToolTipService.SetShowDuration(element,60000);ToolTipService.SetShowOnDisabled(element,true);ToolTipService.SetIsEnabled(element,true);}
  private static void Message(string text)=>log.LogInfo("["+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")+"] LORD_OVERVIEW_"+text);
  private static void Error(Exception e){if(Interlocked.Increment(ref errors)>3)return;log.LogError("["+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")+"] LORD_OVERVIEW: "+e);}
  private sealed class Command:ICommand {private readonly Action run;internal Command(Action action){run=action;}public bool CanExecute(object value)=>Interlocked.Read(ref session)>0;public void Execute(object value)=>run();public event System.EventHandler CanExecuteChanged {add{} remove{}}}
 }
}
