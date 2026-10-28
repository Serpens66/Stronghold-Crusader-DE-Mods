using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using Spectator;
using SHCDESE.API.Components.Noesis.XAML;
namespace SHCDESE.Logging
{
    internal static class LogHelper
    {
        internal static readonly List<string> Failures = new List<string>();
        internal static void Information(string text) { }
        internal static void Warning(string text) { Failures.Add(text); }
        internal static void Error(Exception error, string text) { Failures.Add(text + error); }
    }
}
internal static class AlliesTests
{
    sealed class TestCommand : System.Windows.Input.ICommand
    {
        readonly Action run; internal TestCommand(Action action){run=action;}
        public event EventHandler CanExecuteChanged {add{} remove{}}
        public bool CanExecute(object value)=>true;
        public void Execute(object value)=>run();
    }
    static int checks;
    static void Check(bool condition, string text) { checks++; if(!condition) throw new Exception(text); }
    static void Main(string[] args)
    {
        PanelInputLeaseTests.Run(Check);
        foreach(int start in new[]{0, 10, int.MaxValue-2, int.MaxValue})
        foreach(int delta in new[]{1,5,10,25,100,500,2500})
        foreach(bool bfq in new[]{false,true})
        {
            int value=start, calls=0, refreshed=0;
            var originalCommand=new TestCommand(()=> { calls++; value=bfq?(int)Math.Min(int.MaxValue,(long)value+delta):unchecked(value+delta); });
            var adapter=new CheckedAllyAmountCommand(originalCommand,()=>value,v=>value=v,()=>refreshed++);
            // Vanilla's largest button is 500; larger effective steps are handled by BFQ itself.
            if(!bfq && delta>500) continue;
            adapter.Execute("X500");
            Check(calls==1 && refreshed==1,"exactly one original amount action");
            Check(value==(int)Math.Min(int.MaxValue,(long)start+delta),"modifier result preserved and Vanilla overflow saturated");
        }
        var state = new AllyTestState();
        Check(!state.Enabled && state.Recipient==0,"session starts disabled");
        state.Toggle(3); Check(state.Enabled && state.Recipient==3,"initial viewed player");
        state.Select(8); state.Open("Orders"); state.SelectTarget(2);
        Check(state.Recipient==8 && state.Target==2,"actor and target independent");
        state.Open("Protect"); Check(state.Page=="Protect" && state.Recipient==8,"protection picker preserves ordered AI");
        state.Open("Goods"); state.Open(""); Check(state.Recipient==8 && state.Target==2,"navigation preserves identities");
        state.Toggle(1); state.Toggle(4); Check(state.Recipient==8,"perspective changes do not change saved recipient");
        state.Select(0); state.Select(9); Check(state.Recipient==8,"reject invalid IDs");
        state.Reset(); state.Toggle(-1); Check(state.Enabled && state.Recipient==0,"invalid initial perspective never picks unrelated player");
        state.Reset(); Check(!state.Enabled && state.Recipient==0 && state.Target==0,"new session clears state");
        var pending = new PanelRefreshState(); var panel = new object(); var snapshot = new object();
        pending.Begin(1,panel,snapshot); long generation=pending.Generation;
        Check(pending.IsCurrent(1,panel,true) && !pending.HasFreshState(snapshot),"wait for fresh snapshot");
        Check(!pending.IsCurrent(1,panel,false),"closing wins before state refresh");
        Check(!pending.IsCurrent(2,panel,true),"session replacement invalidates refresh");
        Check(!pending.IsCurrent(1,new object(),true),"HUD replacement invalidates refresh");
        pending.Cancel(); Check(!pending.Pending && !pending.HasFreshState(new object()),"delayed completion after close is inert");
        pending.Begin(1,panel,new object()); Check(pending.Generation>generation,"reopen gets new generation");
        pending.Begin(1,panel,snapshot); Check(!pending.HasFreshState(snapshot) && pending.HasFreshState(new object()),"last rapid switch wins");
        var original=File.ReadAllText(args[0]); var before=XDocument.Parse(original); var patch=XDocument.Load(args[1]);
        var manager=XamlPatcher.CreateNamespaceManager(before); var ops=new List<XmlPatchOperation>();
        foreach(var op in patch.Root.Elements("Operation"))
        {
            var type=(XmlPatchType)Enum.Parse(typeof(XmlPatchType),(string)op.Attribute("Type"));
            if(type!=XmlPatchType.AddNamespace) Check(before.XPathSelectElements((string)op.Attribute("XPath"),manager).Count()==1,"unique Vanilla patch target: "+op.Attribute("XPath"));
            if(type==XmlPatchType.SetAttribute) Check((string)op.Attribute("AttributeName")=="x:Name","Vanilla modification is naming only");
            ops.Add(new XmlPatchOperation {Type=type,XPath=(string)op.Attribute("XPath"),AttributeName=(string)op.Attribute("AttributeName"),Value=(string)op.Attribute("Value"),Content=op.Element("Content")?.Elements().Single().ToString()});
        }
        var after=XDocument.Parse(XamlPatcher.ApplyPatches(original,ops));
        Check(SHCDESE.Logging.LogHelper.Failures.Count==0,string.Join("\n",SHCDESE.Logging.LogHelper.Failures));
        XNamespace x="http://schemas.microsoft.com/winfx/2006/xaml";
        var host=after.Descendants().Single(e=>(string)e.Attribute(x+"Name")=="SpectatorAllyTest");
        Check((string)host.Attribute("Visibility")=="Collapsed","no initial orphan UI");
        for(int i=1;i<=8;i++) Check(host.Descendants().Count(e=>(string)e.Attribute("CommandParameter")=="Player"+i)==1,"all eight fixed IDs");
        Check(!host.Descendants().Any(e=>e.Name.LocalName=="TextBox"),"Vanilla quantity buttons only");
        Check(!host.Descendants().Any(e=>(string)e.Attribute("CommandParameter")=="ConfirmGoods"),"test confirmation cannot call BFQ or Vanilla transfer");
        foreach(string action in new[]{"Debit","Protect","Cancel","Back","Exit"})
            Check(after.Descendants().Any(e=>(string)e.Attribute("CommandParameter")==action),"missing action "+action);
        Check(!host.Descendants().Any(e=>(string)e.Attribute(x+"Name")=="AllyTestGoodsFooter"),"no duplicate goods footer");
        var extras=after.Descendants().Single(e=>(string)e.Attribute(x+"Name")=="AllyTestFooterExtras");
        Check(extras.Parent.Elements().Count(e=>(string)e.Attribute("CommandParameter")=="ConfirmGoods")==1,"reuse original confirmation");
        Check(extras.Parent.Elements().Count(e=>(string)e.Attribute("CommandParameter")=="CancelGoods")==1,"preserve original reset");
        Check(extras.Descendants().Count(e=>(string)e.Attribute("CommandParameter")=="Debit")==1,"one extra debit command");
        Check(extras.Descendants().Single(e=>(string)e.Attribute("CommandParameter")=="Debit").Attribute("Width").Value=="35","Vanilla-sized debit hit area");
        extras.Remove();
        foreach(var pair in new[]{new[]{"AllyTestOrders","Orders"},new[]{"AllyTestGoods","Send"},new[]{"AllyTestCancel","Cancel"},new[]{"AllyTestDefend","Defend"},new[]{"AllyTestClose","Exit"},new[]{"AllyTestBack","Back"}})
        {
            var source=before.Descendants().First(e=>(string)e.Attribute("CommandParameter")==pair[1]);
            var actual=host.Descendants().Single(e=>(string)e.Attribute(x+"Name")==pair[0]);
            foreach(var attr in source.Attributes().Where(a=>a.Name.LocalName=="Width" || a.Name.LocalName=="Height" || a.Name.LocalName.StartsWith("PropEx.Sprite")))
                Check((string)actual.Attribute(attr.Name)==attr.Value,"Vanilla control size and sprite states: "+pair[0]+" "+attr.Name);
        }
        // Remove owned additions/names: all existing commands, BFQ targets and bindings must remain identical.
        host.Remove();
        foreach(var e in after.Descendants().Where(e=>((string)e.Attribute(x+"Name"))?.StartsWith("AllyVanilla")==true || ((string)e.Attribute(x+"Name"))?.StartsWith("AllyStock")==true)) e.Attribute(x+"Name").Remove();
        foreach(var attribute in after.Root.Attributes().Where(a=>a.IsNamespaceDeclaration && (a.Name.LocalName=="specui" || a.Name.LocalName=="seui")).ToArray()) attribute.Remove();
        Check(XNode.DeepEquals(before,after),"Vanilla subtree, amount buttons and original bindings preserved");
        Console.WriteLine("PASS: "+checks+" Allies production controller and real-patcher checks.");
    }
}
