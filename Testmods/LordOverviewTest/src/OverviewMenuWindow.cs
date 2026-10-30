using System;
using Noesis;

namespace LordOverviewTest {
 internal static partial class OverviewRuntime {
  private static readonly MenuWindowState settingsPosition=new MenuWindowState();
  private static Border menuDragHandle;
  private static UiActionContext menuDragStamp;
  private static bool positioningMenu,initialMenuPlacement;
  private static void OpenSettingsMenu(){
   if(settingsWindow==null || panel?.IsVisible!=true)return;
   BuildSettingsMenu();UpdateColumnsPopup();initialMenuPlacement=!settingsPosition.HasPosition;
   settingsWindow.Visibility=Visibility.Visible;UpdateHeaderEditing();PositionSettingsMenu();ShowCategory();ClosePassiveHover();
  }
  private static MenuRect ElementBounds(FrameworkElement element){
   if(element?.IsLoaded!=true || overlay?.IsLoaded!=true)return default(MenuRect);
   var a=overlay.PointFromScreen(element.PointToScreen(new Point(0,0)));
   var b=overlay.PointFromScreen(element.PointToScreen(new Point(element.ActualWidth,element.ActualHeight)));
   return new MenuRect(a.X,a.Y,Math.Max(0,b.X-a.X),Math.Max(0,b.Y-a.Y));
  }
  private static void PositionSettingsMenu(){
   if(!Editing || parent==null || positioningMenu || parent.ActualWidth<=0 || parent.ActualHeight<=0)return;
   positioningMenu=true;
   try {
    if(initialMenuPlacement)settingsPosition.Reset();
    var bounds=settingsPosition.Arrange(parent.ActualWidth,parent.ActualHeight,ElementBounds(crownButton),ElementBounds(panel));
    settingsWindow.Width=(float)bounds.Width;settingsWindow.Height=(float)bounds.Height;
    Canvas.SetLeft(settingsWindow,(float)bounds.X);Canvas.SetTop(settingsWindow,(float)bounds.Y);
    if(settingsWindow.ActualWidth>0 && panel.ActualWidth>0)initialMenuPlacement=false;
    MarkHoverGeometryDirty();
   } finally {positioningMenu=false;}
  }
  private static void OnSettingsSizeChanged(object sender,SizeChangedEventArgs args){PositionSettingsMenu();MarkHoverGeometryDirty();}
  private static void AttachMenuDrag(){
   menuDragHandle.MouseLeftButtonDown+=OnMenuDragDown;menuDragHandle.MouseMove+=OnMenuDragMove;
   menuDragHandle.MouseLeftButtonUp+=OnMenuDragUp;menuDragHandle.LostMouseCapture+=OnMenuLostCapture;
  }
  private static void DetachMenuDrag(){
   CancelMenuDrag();if(menuDragHandle==null)return;
   menuDragHandle.MouseLeftButtonDown-=OnMenuDragDown;menuDragHandle.MouseMove-=OnMenuDragMove;
   menuDragHandle.MouseLeftButtonUp-=OnMenuDragUp;menuDragHandle.LostMouseCapture-=OnMenuLostCapture;menuDragHandle=null;
  }
  private static void OnMenuDragDown(object sender,MouseButtonEventArgs args){
   if(args.ChangedButton!=MouseButton.Left || !Editing || overlay==null || !menuDragHandle.CaptureMouse())return;
   initialMenuPlacement=false;menuDragStamp=CurrentUiContext();var point=args.GetPosition(overlay);settingsPosition.BeginDrag(point.X,point.Y);ClosePassiveHover();args.Handled=true;
  }
  private static void OnMenuDragMove(object sender,MouseEventArgs args){
   if(!settingsPosition.Dragging)return;
   if(args.LeftButton!=MouseButtonState.Pressed || menuDragStamp==null || !menuDragStamp.Allows(CurrentUiContext(),true)){CancelMenuDrag();return;}
   var point=args.GetPosition(overlay);settingsPosition.MoveDrag(point.X,point.Y,parent.ActualWidth,parent.ActualHeight);PositionSettingsMenu();args.Handled=true;
  }
  private static void OnMenuDragUp(object sender,MouseButtonEventArgs args){if(args.ChangedButton!=MouseButton.Left || !settingsPosition.Dragging)return;CancelMenuDrag();args.Handled=true;}
  private static void OnMenuLostCapture(object sender,MouseEventArgs args){settingsPosition.EndDrag();menuDragStamp=null;}
  private static void CancelMenuDrag(){settingsPosition.EndDrag();menuDragStamp=null;if(menuDragHandle?.IsMouseCaptured==true)menuDragHandle.ReleaseMouseCapture();}
 }
}
