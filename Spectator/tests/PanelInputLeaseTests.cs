using System;
using System.Collections.Generic;
using Noesis;
using Spectator;
// A dependency-property fixture: setters remove bindings exactly as in the failing Vanilla path.
// Exercises the actual lease, not a separate restoration algorithm; rendering is tested in-game.
namespace Noesis
{
    internal sealed class DependencyProperty { internal static readonly object UnsetValue = new object(); }
    internal class BindingBase { }
    internal class UIElement
    {
        internal static readonly DependencyProperty OpacityProperty = new DependencyProperty(), IsHitTestVisibleProperty = new DependencyProperty(), VisibilityProperty = new DependencyProperty();
        internal readonly Dictionary<DependencyProperty,object> Values = new Dictionary<DependencyProperty,object>();
        internal readonly Dictionary<DependencyProperty,BindingBase> Bindings = new Dictionary<DependencyProperty,BindingBase>();
        internal float Opacity {set=>SetValue(OpacityProperty,value);}
        internal bool IsHitTestVisible {set=>SetValue(IsHitTestVisibleProperty,value);}
        internal object ReadLocalValue(DependencyProperty p)=>Values.TryGetValue(p,out var value)?value:DependencyProperty.UnsetValue;
        internal void SetValue(DependencyProperty p,object value){Bindings.Remove(p);Values[p]=value;}
        internal void ClearValue(DependencyProperty p){Bindings.Remove(p);Values.Remove(p);}
    }
    internal static class BindingOperations
    {
        internal static BindingBase GetBindingBase(UIElement element,DependencyProperty p)=>element.Bindings.TryGetValue(p,out var value)?value:null;
        internal static void SetBinding(UIElement element,DependencyProperty p,BindingBase value){element.ClearValue(p);element.Bindings[p]=value;}
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
        var lease=new PanelInputLease(panel,true);
        check(ReferenceEquals(BindingOperations.GetBindingBase(panel,UIElement.VisibilityProperty),visible),"refresh preserves live close binding");
        check(Equals(panel.ReadLocalValue(UIElement.OpacityProperty),0f),"old report obscured without changing visibility");
        lease.Restore();
        check(ReferenceEquals(BindingOperations.GetBindingBase(panel,UIElement.OpacityProperty),opacity),"restore original opacity binding");
        check(ReferenceEquals(BindingOperations.GetBindingBase(panel,UIElement.IsHitTestVisibleProperty),input),"restore original input binding");
        check(ReferenceEquals(BindingOperations.GetBindingBase(panel,UIElement.VisibilityProperty),visible),"closed/reopened visibility remains authoritative");
        lease=new PanelInputLease(panel,true); var newer=new BindingBase();
        BindingOperations.SetBinding(panel,UIElement.OpacityProperty,newer); panel.IsHitTestVisible=true;
        lease.Restore();
        check(ReferenceEquals(BindingOperations.GetBindingBase(panel,UIElement.OpacityProperty),newer),"newer view binding wins");
        check(Equals(panel.ReadLocalValue(UIElement.IsHitTestVisibleProperty),true),"newer view local input state wins");
        panel=new UIElement(); lease=new PanelInputLease(panel,true); lease.Restore();
        check(panel.ReadLocalValue(UIElement.OpacityProperty)==DependencyProperty.UnsetValue && panel.ReadLocalValue(UIElement.IsHitTestVisibleProperty)==DependencyProperty.UnsetValue,"no leftover override when original value inherited");
    }
}
