using System;
using System.Threading;
using Noesis;

namespace LordOverviewTest {
 internal static partial class OverviewRuntime {
  private static Button crownButton;
  private static readonly CrownPress crownPress=new CrownPress();
  private static string CrownTooltip=>Text("Title")+" · "+Text("RightClickSettings");
  private static UiActionContext CrownContext(Button button)=>new UiActionContext(Interlocked.Read(ref session),Volatile.Read(ref generation),editEpoch,panel,
   ReferenceEquals(button,crownButton) && button.IsLoaded && button.IsVisible && button.IsEnabled && button.IsHitTestVisible && sourceHost?.IsLoaded==true && main?.Show_InGameUI==true,settings.EnableMod,false);
  private static void BindCrownButton(Button button){
   DetachCrownButton();crownButton=button;
   button.PreviewMouseDown+=OnCrownDown;button.PreviewMouseUp+=OnCrownUp;button.LostMouseCapture+=OnCrownLostCapture;
  }
  private static void DetachCrownButton(){
   CancelCrownPress();if(crownButton==null)return;
   crownButton.PreviewMouseDown-=OnCrownDown;crownButton.PreviewMouseUp-=OnCrownUp;crownButton.LostMouseCapture-=OnCrownLostCapture;crownButton=null;
  }
  private static void CancelCrownPress(){bool active=crownPress.Active;crownPress.Cancel();if(active && crownButton?.IsMouseCaptured==true)crownButton.ReleaseMouseCapture();}
  private static void OnCrownLostCapture(object sender,MouseEventArgs args)=>crownPress.Cancel();
  private static void OnCrownDown(object sender,MouseButtonEventArgs args){
   if(args.ChangedButton!=MouseButton.Right || !(sender is Button button))return;
   if(!crownPress.Begin(CrownContext(button),true))return;
   if(!button.CaptureMouse()){crownPress.Cancel();return;}
   if(ToolTipService.GetToolTip(button) is ToolTip tip)tip.IsOpen=false;ClosePassiveHover();args.Handled=true;
  }
  private static void OnCrownUp(object sender,MouseButtonEventArgs args){
   if(args.ChangedButton!=MouseButton.Right || !(sender is Button button) || !crownPress.Active)return;
   bool run=crownPress.End(CrownContext(button),ContainsClick(button,args),true);
   if(button.IsMouseCaptured)button.ReleaseMouseCapture();args.Handled=true;if(!run)return;
   UiActions.Bind(()=>CrownSettingsAction.Run(panel?.IsVisible==true,Editing,OpenOverview,OpenSettingsMenu,CloseSettingsMenu),()=>CrownContext(button),Post,false)();
  }
 }
}
