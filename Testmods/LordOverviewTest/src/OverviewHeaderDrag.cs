using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Noesis;
namespace LordOverviewTest {
 internal static partial class OverviewRuntime {
  private static readonly HeaderGesture headerGesture=new HeaderGesture();
  private static readonly Dictionary<Metric,Button> metricHeaders=new Dictionary<Metric,Button>();
  private static readonly Dictionary<Metric,Border> headerHighlights=new Dictionary<Metric,Border>();
  private static Grid tableHeader;
  private static Border insertionMarker;
  private static Button pressedHeader;
  private static long pointerSession;
  private static int pointerGeneration;
  private static void BindMetricHeader(Button button,Metric metric,Border highlight){
   metricHeaders[metric]=button;headerHighlights[metric]=highlight;
   button.PreviewMouseDown+=OnHeaderDown;button.MouseMove+=OnHeaderMove;button.PreviewMouseUp+=OnHeaderUp;
   button.LostMouseCapture+=(_,__)=>{if(ReferenceEquals(pressedHeader,button))CancelHeaderGesture();};
   button.LostKeyboardFocus+=(_,__)=>{if(ReferenceEquals(pressedHeader,button))CancelHeaderGesture();};
  }
  private static void AttachHeaderMarker(Grid header){tableHeader=header;
   insertionMarker=new Border {Width=2,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Stretch,
    Background=new SolidColorBrush(Color.FromRgb(242,212,138)),Visibility=Visibility.Collapsed,IsHitTestVisible=false};
   Grid.SetColumnSpan(insertionMarker,selection.Columns.Length+2);Panel.SetZIndex(insertionMarker,10);header.Children.Add(insertionMarker);
  }
  private static void OnHeaderDown(object sender,MouseButtonEventArgs args){
   if(args.ChangedButton!=MouseButton.Left || !(sender is Button button) || Interlocked.Read(ref session)<=0 || panel?.IsVisible!=true || !settings.EnableMod || tableHeader==null)return;
   var match=metricHeaders.FirstOrDefault(pair=>ReferenceEquals(pair.Value,button));if(match.Value==null)return;
   CancelHeaderGesture();button.Focus();var point=args.GetPosition(tableHeader);pointerSession=Interlocked.Read(ref session);pointerGeneration=Volatile.Read(ref generation);
   widths=tableHeader.ColumnDefinitions.Skip(2).Select(c=>(double)c.ActualWidth).ToArray();headerGesture.Begin(match.Key,point.X,point.Y,pointerSession,pointerGeneration);pressedHeader=button;
   if(!button.CaptureMouse()){CancelHeaderGesture();return;}args.Handled=true;
  }
  private static void TrackHeader(MouseEventArgs args){
   if(!headerGesture.Active || tableHeader==null)return;var point=args.GetPosition(tableHeader);var columns=selection.Columns;
   bool inside=HeaderDropGeometry.TryTarget(point.X,point.Y,tableHeader.RowDefinitions[0].ActualHeight,tableHeader.ColumnDefinitions.Take(2).Sum(c=>(double)c.ActualWidth),widths,Array.IndexOf(columns,headerGesture.Metric),out int target,out double boundary);
   headerGesture.Update(point.X,point.Y,target);
   if(headerHighlights.TryGetValue(headerGesture.Metric,out var highlight))highlight.Background=new SolidColorBrush(headerGesture.Dragging?Color.FromArgb(100,200,150,50):Color.FromArgb(0,0,0,0));
   if(insertionMarker!=null){insertionMarker.Visibility=headerGesture.Dragging && inside?Visibility.Visible:Visibility.Collapsed;insertionMarker.Margin=new Thickness((float)boundary-1,0,0,0);}
  }
  private static void OnHeaderMove(object sender,MouseEventArgs args){
   if(!ReferenceEquals(sender,pressedHeader) || !headerGesture.Active)return;
   if(args.LeftButton!=MouseButtonState.Pressed){CancelHeaderGesture();return;}TrackHeader(args);args.Handled=true;
  }
  private static void OnHeaderUp(object sender,MouseButtonEventArgs args){
   if(args.ChangedButton!=MouseButton.Left || !ReferenceEquals(sender,pressedHeader) || !headerGesture.Active)return;
   TrackHeader(args);bool original=ContainsClick(pressedHeader,args);long expected=pointerSession;int token=pointerGeneration;var view=panel;
   var result=headerGesture.End(original,Interlocked.Read(ref session),Volatile.Read(ref generation),out var metric,out int target);
   ReleaseHeaderCapture();args.Handled=true;
   Post(()=>{if(expected<=0 || expected!=Interlocked.Read(ref session) || token!=Volatile.Read(ref generation) || !ReferenceEquals(view,panel) || panel?.IsVisible!=true)return;
    if(result==HeaderGestureResult.Sort){state.Select(metric);signature="";}
    else if(result==HeaderGestureResult.Reorder && selection.MoveTo(metric,target)){ApplyColumns();return;}
    Render();
   });
  }
  private static void ReleaseHeaderCapture(){var old=pressedHeader;pressedHeader=null;if(old?.IsMouseCaptured==true)old.ReleaseMouseCapture();
   if(insertionMarker!=null)insertionMarker.Visibility=Visibility.Collapsed;
   foreach(var highlight in headerHighlights.Values)highlight.Background=new SolidColorBrush(Color.FromArgb(0,0,0,0));
  }
  private static void CancelHeaderGesture(){if(!headerGesture.Active && pressedHeader==null)return;headerGesture.Cancel();ReleaseHeaderCapture();
   var view=panel;long id=Interlocked.Read(ref session);int token=Volatile.Read(ref generation);
   Post(()=>{if(id==Interlocked.Read(ref session) && token==Volatile.Read(ref generation) && ReferenceEquals(view,panel) && panel?.IsVisible==true)Render();});
  }
  private static void OnHeaderKeyDown(object sender,KeyEventArgs args){if(headerGesture.Active && args.Key==Key.Escape){CancelHeaderGesture();args.Handled=true;}}
  private static void OnOverviewFocusChanged(bool focused){if(!focused)Post(()=>{CancelHeaderGesture();CloseColumnsPopup();});}
 }
}
