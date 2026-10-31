using System;
using System.Collections.Generic;
using APIShared.Presentation;
using Noesis;

namespace LordOverviewTest {
 internal static partial class OverviewRuntime {
  private sealed class HoverTarget {
   internal FrameworkElement Element;
   internal string Description,DisplayText;
   internal Rect Bounds;
   internal bool ValidBounds,Immediate;
  }
  private static readonly Dictionary<string,HoverTarget> hoverTargets=new Dictionary<string,HoverTarget>();
  private static readonly OverviewHoverState hoverState=new OverviewHoverState();
  private static HoverTarget visibleHover;
  private static Border hoverBubble;
  private static TextBlock hoverBubbleLabel;
  private static string bubbleText;
  private static int hoverRevision,bubbleRevision=-1;
  private static bool hoverGeometryDirty=true,hoverFaulted;
  private static int hoverFrame=-1,hoverScreenWidth,hoverScreenHeight;
  private static void StartPassiveHover(){
   if(!PresentationEvents.TryRegister(PresentationOperation.GuiChecks,"LordOverviewTest_Serp","passive-hover",null,OnOverviewGui,out string reason))throw new InvalidOperationException(reason);
  }
  private static HoverTarget RegisterHover(string key,FrameworkElement element,string text,bool immediate=false){
   RemoveHover(key);
   var target=new HoverTarget {Element=element,Description=text,DisplayText=text,Immediate=immediate};hoverTargets.Add(key,target);MarkHoverGeometryDirty();return target;
  }
  private static void RemoveHover(string key){if(!hoverTargets.TryGetValue(key,out var target))return;if(ReferenceEquals(target,visibleHover))ClosePassiveHover();hoverTargets.Remove(key);}
  private static void ClearHoverTargets(){ClosePassiveHover();hoverTargets.Clear();MarkHoverGeometryDirty();}
  private static void MarkHoverGeometryDirty(){hoverGeometryDirty=true;hoverRevision++;}
  private static void HidePassiveBubble(){if(hoverBubble?.Visibility==Visibility.Visible)hoverBubble.Visibility=Visibility.Collapsed;bubbleRevision=-1;}
  private static void ClosePassiveHover(){HidePassiveBubble();visibleHover=null;hoverState.Reset();}
  private static Border CreatePassiveBubble(){
   // A view-owned passive visual: manual ToolTip.IsOpen did not attach a popup in the game's Noesis build.
   hoverBubbleLabel=new TextBlock {IsHitTestVisible=false,FontSize=16,Foreground=new SolidColorBrush(Color.FromRgb(255,255,255)),TextWrapping=TextWrapping.Wrap};
   return new Border {IsHitTestVisible=false,Tag=OverviewPresentationPolicy.PassiveTag,Visibility=Visibility.Collapsed,Background=new SolidColorBrush(Color.FromArgb(255,29,23,16)),BorderBrush=new SolidColorBrush(Color.FromRgb(242,212,138)),BorderThickness=new Thickness(1.5f),Padding=new Thickness(10,8,10,8),Child=hoverBubbleLabel};
  }
  private static void ShowPassiveBubble(HoverTarget target){
   if(overlay==null || parent==null || !target.ValidBounds)return;
   if(hoverBubble==null){hoverBubble=CreatePassiveBubble();Panel.SetZIndex(hoverBubble,30);overlay.Children.Add(hoverBubble);}
   if(hoverBubble.Visibility==Visibility.Visible && bubbleRevision==hoverRevision && bubbleText==target.DisplayText)return;
   if(bubbleText!=target.DisplayText){bubbleText=target.DisplayText;hoverBubbleLabel.Text=bubbleText;}
   hoverBubble.MaxWidth=Math.Max(1,Math.Min(420,parent.ActualWidth));hoverBubble.Visibility=Visibility.Visible;
   hoverBubble.Measure(new Size(hoverBubble.MaxWidth,float.PositiveInfinity));
   var a=overlay.PointFromScreen(new Point(target.Bounds.X,target.Bounds.Y));var b=overlay.PointFromScreen(new Point(target.Bounds.Right,target.Bounds.Bottom));
   var position=PassiveBubblePlacement.Place(new MenuRect(a.X,a.Y,b.X-a.X,b.Y-a.Y),hoverBubble.DesiredSize.Width,hoverBubble.DesiredSize.Height,parent.ActualWidth,parent.ActualHeight);
   Canvas.SetLeft(hoverBubble,(float)position.X);Canvas.SetTop(hoverBubble,(float)position.Y);bubbleRevision=hoverRevision;
  }
  private static bool ScreenContains(FrameworkElement element,Point point){
   if(element==null || !element.IsLoaded || !element.IsVisible)return false;
   var local=element.PointFromScreen(point);return local.X>=0 && local.Y>=0 && local.X<element.ActualWidth && local.Y<element.ActualHeight;
  }
  private static bool OverHeader(Point screen){
   if(!Editing)return false;
   foreach(var surface in headerSurfaces){if(surface.Prefix.StartsWith("menu/",StringComparison.Ordinal))continue;var grid=surface.Grid;var local=grid.PointFromScreen(screen);
    if(local.X>=0 && local.X<grid.ActualWidth && local.Y>=0 && local.Y<grid.RowDefinitions[0].ActualHeight)return true;}
   return false;
  }
  private static readonly Func<Visual,Visual> hoverParent=v=>VisualTreeHelper.GetParent(v) as Visual;
  private static readonly Func<Visual,int> hoverZ=v=>v is UIElement element?Panel.GetZIndex(element):0;
  private static readonly Func<Visual,Visual,int> hoverIndex=(p,c)=>p is Panel owner && c is UIElement child?owner.Children.IndexOf(child):-1;
  private static void OnHoverGui(PresentationPostEventArgs args){
   long elapsed=0;Shared.DevDiag.Start(ref elapsed);
   try {HoverGuiCore(args);}finally{Shared.DevDiag.StopSpike(ref elapsed,"overview.hover",1000);}
  }
  private static void HoverGuiCore(PresentationPostEventArgs args){
   // Existing Unity-thread publisher; no game reads, dispatcher closures or allocations on the dormant path.
   if(System.Threading.Volatile.Read(ref shown)==0 || hoverFaulted)return;
   int frame=UnityEngine.Time.frameCount;if(frame==hoverFrame)return;hoverFrame=frame;
   try {
    if(!UnityEngine.Application.isFocused || !args.OriginalCompleted || panel?.IsVisible!=true || !panel.IsLoaded || parent==null || !sourceHost.IsLoaded || main.Show_HUD_IngameMenu){ClosePassiveHover();return;}
    int screenWidth=UnityEngine.Screen.width,screenHeight=UnityEngine.Screen.height;
    if(screenWidth!=hoverScreenWidth || screenHeight!=hoverScreenHeight){hoverScreenWidth=screenWidth;hoverScreenHeight=screenHeight;MarkHoverGeometryDirty();ClosePassiveHover();CancelHeaderGesture();CancelMenuDrag();PositionSettingsMenu();return;} // Let Noesis finish its viewport update before caching transforms.
    var mouse=UnityEngine.Input.mousePosition;var screen=new Point(mouse.x,screenHeight-mouse.y);
    DiagHoverPoint(ref screen);
    bool pressed=UnityEngine.Input.GetMouseButtonDown(0) || UnityEngine.Input.GetMouseButtonDown(1) || UnityEngine.Input.GetMouseButtonDown(2);
    if(Editing && !settingsPosition.Dragging && !LayoutDropDownOpen && pressed && !ScreenContains(settingsWindow,screen) && !ScreenContains(crownButton,screen) && !OverHeader(screen))CloseSettingsMenu();
    if(pressed || UnityEngine.Input.mouseScrollDelta.sqrMagnitude>0 || UnityEngine.Input.GetMouseButton(0) || UnityEngine.Input.GetMouseButton(1) || UnityEngine.Input.GetMouseButton(2) || headerGesture.Active || settingsPosition.Dragging || !ScreenContains(parent,screen)) {ClosePassiveHover();return;}
    if(Editing && (LayoutDropDownOpen || ScreenContains(settingsWindow,screen))){ClosePassiveHover();return;}
    if(hoverGeometryDirty){
     foreach(var target in hoverTargets.Values){
      var element=target.Element;
      target.ValidBounds=false;
      if(!element.IsLoaded || !element.IsVisible || element.ActualWidth<=0 || element.ActualHeight<=0)continue;
      var top=element.PointToScreen(new Point(0,0));var bottom=element.PointToScreen(new Point(element.ActualWidth,element.ActualHeight));
      target.Bounds=new Rect(top.X,top.Y,Math.Max(0,bottom.X-top.X),Math.Max(0,bottom.Y-top.Y));target.ValidBounds=target.Bounds.Width>0 && target.Bounds.Height>0;
     }hoverGeometryDirty=false;
    }
    string key=null;HoverTarget found=null;
    foreach(var pair in hoverTargets)if(pair.Value.ValidBounds && !string.IsNullOrEmpty(pair.Value.Description) && pair.Value.Bounds.Contains(screen)){key=pair.Key;found=pair.Value;break;}
    if(found!=null){
     // Passive content does not hit. A different foreground UI must still win over our hover areas.
     var hitRoot=inputRoot as Visual??parent;
     var hitObject=VisualTreeHelper.HitTest(hitRoot,hitRoot.PointFromScreen(screen)).VisualHit;var hit=hitObject as Visual;
     if(hitObject!=null && hit==null || HoverOcclusion.Blocks<Visual>(hit,overlay,hoverParent,hoverZ,hoverIndex)){key=null;found=null;}
    }
    if(!ReferenceEquals(found,visibleHover)){HidePassiveBubble();visibleHover=found;}
    double delay=found==null || found.Immediate?0:ToolTipService.GetInitialShowDelay(found.Element)/1000d;
    bool open=hoverState.Update(key,UnityEngine.Time.realtimeSinceStartup,delay,60);
    if(found!=null && open)ShowPassiveBubble(found);else HidePassiveBubble();
   } catch(Exception error){hoverFaulted=true;try{ClosePassiveHover();}catch{} Error(error);} // Retry on an explicit reopen/new HUD, never throw once per frame.
  }
  static partial void DiagHoverPoint(ref Point screen);
  private static void OnViewportSizeChanged(object sender,SizeChangedEventArgs args){CancelHeaderGesture();CancelMenuDrag();ClosePassiveHover();MarkHoverGeometryDirty();PositionSettingsMenu();}
 }
}
