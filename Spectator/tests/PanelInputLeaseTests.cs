using System;
using System.Collections.Generic;
using Noesis;
using Spectator;
// A dependency-property fixture: setters remove bindings exactly as in the failing Vanilla path.
// Exercises the actual lease, not a separate restoration algorithm; rendering is tested in-game.
namespace Noesis
{
    internal sealed class DependencyProperty { internal static readonly object UnsetValue = new object(); }
    internal class BindingBase { internal object Value; }
    internal class DependencyObject
    {
        internal bool FailBindingRead;
        internal readonly Dictionary<DependencyProperty,object> Values = new Dictionary<DependencyProperty,object>();
        internal readonly Dictionary<DependencyProperty,BindingBase> Bindings = new Dictionary<DependencyProperty,BindingBase>();
        internal object GetValue(DependencyProperty p)=>Bindings.TryGetValue(p,out var binding)?binding.Value:Values.TryGetValue(p,out var value)?value:null;
        internal object ReadLocalValue(DependencyProperty p)=>Values.TryGetValue(p,out var value)?value:DependencyProperty.UnsetValue;
        internal void SetValue(DependencyProperty p,object value){Bindings.Remove(p);Values[p]=value;}
        internal void ClearValue(DependencyProperty p){Bindings.Remove(p);Values.Remove(p);}
    }
    internal class UIElement : DependencyObject
    {
        internal static readonly DependencyProperty OpacityProperty = new DependencyProperty(), IsHitTestVisibleProperty = new DependencyProperty(), VisibilityProperty = new DependencyProperty();
        internal float Opacity {set=>SetValue(OpacityProperty,value);}
        internal bool IsHitTestVisible {set=>SetValue(IsHitTestVisibleProperty,value);}
    }
    internal static class BindingOperations
    {
        internal static BindingBase GetBindingBase(DependencyObject element,DependencyProperty p)=>element.FailBindingRead?throw new InvalidOperationException("disposed fixture element"):element.Bindings.TryGetValue(p,out var value)?value:null;
        internal static void SetBinding(DependencyObject element,DependencyProperty p,BindingBase value){element.ClearValue(p);element.Bindings[p]=value;}
    }
}
internal static class PanelInputLeaseTests
{
    internal static void Run(Action<bool,string> check)
    {
        var panel=new UIElement(); var visible=new BindingBase(); var opacity=new BindingBase(); var input=new BindingBase();
        BindingOperations.SetBinding(panel,UIElement.VisibilityProperty,visible);
        BindingOperations.SetBinding(panel,UIElement.OpacityProperty,opacity);
        BindingOperations.SetBinding(panel,UIElement.IsHitTestVisibleProperty,input);
        var lease=new PanelInputLease(panel);
        check(ReferenceEquals(BindingOperations.GetBindingBase(panel,UIElement.VisibilityProperty),visible),"refresh preserves live close binding");
        check(ReferenceEquals(BindingOperations.GetBindingBase(panel,UIElement.OpacityProperty),opacity),"report frame remains visible with its original opacity binding");
        lease.Restore();
        check(ReferenceEquals(BindingOperations.GetBindingBase(panel,UIElement.OpacityProperty),opacity),"restore original opacity binding");
        check(ReferenceEquals(BindingOperations.GetBindingBase(panel,UIElement.IsHitTestVisibleProperty),input),"restore original input binding");
        check(ReferenceEquals(BindingOperations.GetBindingBase(panel,UIElement.VisibilityProperty),visible),"closed/reopened visibility remains authoritative");
        lease=new PanelInputLease(panel); var newer=new BindingBase();
        BindingOperations.SetBinding(panel,UIElement.OpacityProperty,newer); panel.IsHitTestVisible=true;
        lease.Restore();
        check(ReferenceEquals(BindingOperations.GetBindingBase(panel,UIElement.OpacityProperty),newer),"newer view binding wins");
        check(Equals(panel.ReadLocalValue(UIElement.IsHitTestVisibleProperty),true),"newer view local input state wins");
        panel=new UIElement(); lease=new PanelInputLease(panel); lease.Restore();
        check(panel.ReadLocalValue(UIElement.OpacityProperty)==DependencyProperty.UnsetValue && panel.ReadLocalValue(UIElement.IsHitTestVisibleProperty)==DependencyProperty.UnsetValue,"no leftover override when original value inherited");
        var textProperty=new DependencyProperty(); var text=new DependencyObject(); var live=new BindingBase { Value="Lord A" };
        BindingOperations.SetBinding(text,textProperty,live);
        var display=new ReportDisplayLease(); display.Add(text,textProperty);
        live.Value="Lord B";
        check(Equals(text.GetValue(textProperty),"Lord A"),"pending reports hold displayed name/value while the original binding source updates");
        display.Hold(); display.Restore();
        check(ReferenceEquals(BindingOperations.GetBindingBase(text,textProperty),live) && Equals(text.GetValue(textProperty),"Lord B"),"completed GUI refresh exposes the new live report binding");
        text.SetValue(textProperty,"old widget head"); display=new ReportDisplayLease(); display.Add(text,textProperty);
        text.SetValue(textProperty,"new widget head"); display.Hold();
        check(Equals(text.GetValue(textProperty),"old widget head"),"imperative report widgets stay visually stable while waiting");
        display.Restore(); display.Restore();
        check(Equals(text.GetValue(textProperty),"new widget head"),"release preserves the latest imperative GUI value and is idempotent");
        display=new ReportDisplayLease(); display.Add(text,textProperty); var replacement=new BindingBase {Value="new HUD"};
        BindingOperations.SetBinding(text,textProperty,replacement); display.Hold(); display.Restore();
        check(ReferenceEquals(BindingOperations.GetBindingBase(text,textProperty),replacement),"a newer HUD binding remains authoritative during cancellation");
        var disposed=new DependencyObject(); var survivor=new DependencyObject(); var survivorBinding=new BindingBase {Value="live"};
        BindingOperations.SetBinding(survivor,textProperty,survivorBinding);
        display=new ReportDisplayLease(); display.Add(disposed,textProperty); display.Add(survivor,textProperty);
        disposed.FailBindingRead=true;
        check(display.Restore()!=null && ReferenceEquals(BindingOperations.GetBindingBase(survivor,textProperty),survivorBinding),
            "a replaced/disposed report element cannot prevent remaining bindings from being restored");
        check(display.Restore()==null,"failed restoration also releases retained references idempotently");
    }
}
