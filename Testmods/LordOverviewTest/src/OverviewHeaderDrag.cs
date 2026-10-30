using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Noesis;
namespace LordOverviewTest {
 internal static partial class OverviewRuntime {
  private sealed class HeaderSurface {
   internal Grid Grid;
   internal Border Marker;
   internal int Skip;
   internal string Prefix;
  }
  private sealed class HeaderBinding {
   internal HeaderSurface Surface;
   internal Metric? Metric;
   internal Border Highlight;
   internal Canvas Arrow;
   internal bool Menu;
  }
  private static readonly HeaderGesture headerGesture=new HeaderGesture();
  private static readonly Dictionary<Button,HeaderBinding> headers=new Dictionary<Button,HeaderBinding>();
  private static readonly List<HeaderSurface> headerSurfaces=new List<HeaderSurface>();
  private static HeaderSurface activeHeader;
  private static Button pressedHeader;
  private static long pointerSession;
  private static int pointerGeneration,pointerEditEpoch;
  private static HeaderSurface NewHeaderSurface(Grid grid,int skip,string prefix){
   var surface=new HeaderSurface {Grid=grid,Skip=skip,Prefix=prefix};
   surface.Marker=new Border {Width=2,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Height=24,
    Background=new SolidColorBrush(Color.FromRgb(242,212,138)),Visibility=Visibility.Collapsed,IsHitTestVisible=false};
   Grid.SetColumnSpan(surface.Marker,grid.ColumnDefinitions.Count);Panel.SetZIndex(surface.Marker,10);grid.Children.Add(surface.Marker);
   headerSurfaces.Add(surface);return surface;
  }
  private static void BindMetricHeader(Button button,HeaderBinding binding){
   headers.Add(button,binding);
   button.PreviewMouseDown+=OnHeaderDown;button.MouseMove+=OnHeaderMove;button.PreviewMouseUp+=OnHeaderUp;
   button.LostMouseCapture+=(_,__)=>{if(ReferenceEquals(pressedHeader,button))CancelHeaderGesture();};
   button.LostKeyboardFocus+=(_,__)=>{if(ReferenceEquals(pressedHeader,button))CancelHeaderGesture();};
  }
  private static void OnHeaderDown(object sender,MouseButtonEventArgs args){
   if(args.ChangedButton!=MouseButton.Left || !(sender is Button button) || !headers.TryGetValue(button,out var binding) || !binding.Metric.HasValue ||
    Interlocked.Read(ref session)<=0 || panel?.IsVisible!=true || !settings.EnableMod || !Editing)return;
   CancelHeaderGesture();activeHeader=binding.Surface;button.Focus();var point=args.GetPosition(activeHeader.Grid);
   pointerSession=Interlocked.Read(ref session);pointerGeneration=Volatile.Read(ref generation);pointerEditEpoch=editEpoch;
   widths=activeHeader.Grid.ColumnDefinitions.Skip(activeHeader.Skip).Select(c=>(double)c.ActualWidth).ToArray();
   headerGesture.Begin(binding.Metric.Value,point.X,point.Y,pointerSession,pointerGeneration);pressedHeader=button;
   if(!button.CaptureMouse()){CancelHeaderGesture();return;}args.Handled=true;
  }
  private static void TrackHeader(MouseEventArgs args){
   if(!headerGesture.Active || activeHeader==null || !Editing)return;
   var grid=activeHeader.Grid;var point=args.GetPosition(grid);var columns=selection.Columns;
   bool inside=HeaderDropGeometry.TryTarget(point.X,point.Y,grid.RowDefinitions[0].ActualHeight,
    grid.ColumnDefinitions.Take(activeHeader.Skip).Sum(c=>(double)c.ActualWidth),widths,Array.IndexOf(columns,headerGesture.Metric),out int target,out double boundary);
   headerGesture.Update(point.X,point.Y,target);
   if(pressedHeader!=null && headers.TryGetValue(pressedHeader,out var binding))binding.Highlight.Background=new SolidColorBrush(headerGesture.Dragging?Color.FromArgb(100,200,150,50):Color.FromArgb(0,0,0,0));
   activeHeader.Marker.Visibility=headerGesture.Dragging && inside?Visibility.Visible:Visibility.Collapsed;
   activeHeader.Marker.RenderTransform=new TranslateTransform((float)boundary-1,0);
  }
  private static void OnHeaderMove(object sender,MouseEventArgs args){
   if(!ReferenceEquals(sender,pressedHeader) || !headerGesture.Active)return;
   if(args.LeftButton!=MouseButtonState.Pressed){CancelHeaderGesture();return;}TrackHeader(args);args.Handled=true;
  }
  private static void OnHeaderUp(object sender,MouseButtonEventArgs args){
   if(args.ChangedButton!=MouseButton.Left || !ReferenceEquals(sender,pressedHeader) || !headerGesture.Active)return;
   TrackHeader(args);bool original=ContainsClick(pressedHeader,args);long expected=pointerSession;int token=pointerGeneration;int editToken=pointerEditEpoch;var view=panel;
   var result=headerGesture.End(original,Interlocked.Read(ref session),Volatile.Read(ref generation),out var metric,out int target);
   string originalColumns=Metrics.Serialize(selection.Columns);
   ReleaseHeaderCapture();args.Handled=true;
   Post(()=>{if(expected<=0 || expected!=Interlocked.Read(ref session) || token!=Volatile.Read(ref generation) || !ReferenceEquals(view,panel) ||
    !TableInteraction.CanEdit(Editing,panel?.IsVisible==true,Interlocked.Read(ref session),expected,editEpoch,editToken) || originalColumns!=Metrics.Serialize(selection.Columns))return;
    if(result==HeaderGestureResult.Sort){state.Select(metric);signature="";}
    else if(result==HeaderGestureResult.Reorder && selection.MoveTo(metric,target)){ApplyColumns();return;}
    Render();UpdateColumnsPopup();
   });
  }
  private static void ReleaseHeaderCapture(){var old=pressedHeader;pressedHeader=null;if(old?.IsMouseCaptured==true)old.ReleaseMouseCapture();
   foreach(var surface in headerSurfaces)surface.Marker.Visibility=Visibility.Collapsed;
   foreach(var binding in headers.Values)binding.Highlight.Background=new SolidColorBrush(Color.FromArgb(0,0,0,0));activeHeader=null;
  }
  private static void CancelHeaderGesture(){if(!headerGesture.Active && pressedHeader==null)return;headerGesture.Cancel();ReleaseHeaderCapture();
   var view=panel;long id=Interlocked.Read(ref session);int token=Volatile.Read(ref generation);
   Post(()=>{if(id==Interlocked.Read(ref session) && token==Volatile.Read(ref generation) && ReferenceEquals(view,panel) && panel?.IsVisible==true)Render();});
  }
  private static void RemoveHeaders(bool menu){
   foreach(var pair in headers.Where(p=>p.Value.Menu==menu).ToArray())headers.Remove(pair.Key);
   headerSurfaces.RemoveAll(s=>s.Prefix.StartsWith(menu?"menu/":"view/",StringComparison.Ordinal));
   foreach(var key in hoverTargets.Keys.Where(k=>k.StartsWith(menu?"menu/":"view/",StringComparison.Ordinal)).ToArray())RemoveHover(key);
  }
  private static bool ContainsHeaderClick(MouseButtonEventArgs args)=>Editing && headerSurfaces.Any(s=>{
   if(s.Prefix.StartsWith("menu/",StringComparison.Ordinal))return false;
   var point=args.GetPosition(s.Grid);return point.X>=0 && point.X<s.Grid.ActualWidth && point.Y>=0 && point.Y<s.Grid.RowDefinitions[0].ActualHeight;
  });
  private static void OnHeaderKeyDown(object sender,KeyEventArgs args){if(args.Key!=Key.Escape)return;
   if(settingsPosition.Dragging){CancelMenuDrag();args.Handled=true;return;}
   if(headerGesture.Active)CancelHeaderGesture();else if(Editing)CloseSettingsMenu();else return;args.Handled=true;
  }
  private static void OnOverviewFocusChanged(bool focused){if(!focused)Post(()=>{CancelCrownPress();CancelMenuDrag();CancelHeaderGesture();CloseSettingsMenu();ClosePassiveHover();});}
 }
}
