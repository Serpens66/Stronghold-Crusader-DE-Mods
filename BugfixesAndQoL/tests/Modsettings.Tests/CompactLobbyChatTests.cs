using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using BugfixesAndQoL;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;

internal static partial class Program
{
    private static void CompactLobbyChatTests(string workspace, string game)
    {
        var type = typeof(CompactLobbyChatFeature);
        object feature = FormatterServices.GetUninitializedObject(type);
        var states = type.GetField("views", All);
        states.SetValue(feature, Array.CreateInstance(states.FieldType.GetElementType(), 5));
        Action<int, Noesis.TextBlock, bool, bool> update = (slot, view, rebuilt, enabled) =>
            type.GetMethod("UpdateView", All).Invoke(feature, new object[] { slot, view, rebuilt, enabled });
        Action<Noesis.TextBlock, string, string> append = (view, name, message) =>
        {
            view.Inlines.Add(new Noesis.InlineUIContainer { Child = new Noesis.Image { Width = 14, Height = 14 } });
            view.Inlines.Add(new Noesis.InlineUIContainer { Child = new Noesis.TextBlock { Text = " " + name + " :", Width = 600, Height = 14, FontSize = 14 } });
            view.Inlines.Add(new Noesis.LineBreak());
            view.Inlines.Add(new Noesis.InlineUIContainer { Child = new Noesis.TextBlock { Text = message, FontSize = 12,
                Margin = new Noesis.Thickness(40, 0, 5, 0), TextWrapping = Noesis.TextWrapping.WrapWithOverflow } });
            view.Inlines.Add(new Noesis.Run(Environment.NewLine));
        };
        foreach (string message in new[] { "Hi!", "", "Grüße 中文 🛡 <not markup>", new string('W',300) })
        {
            var view = new Noesis.TextBlock(); append(view,"Serp 🐍",message);
            var original = view.Inlines.ToArray();
            for (int cycle = 0; cycle < 3; cycle++)
            {
                update(0,view,false,true);
                Check(view.Inlines.Count==4 && ReferenceEquals(view.Inlines[0],original[0]) && ReferenceEquals(view.Inlines[3],original[4]), "compact keeps shield and exactly one message terminator");
                Check(((Noesis.Run)view.Inlines[1]).Text==" Serp 🐍 : " && view.Inlines[1].FontSize==14 &&
                    ((Noesis.Run)view.Inlines[2]).Text==message && view.Inlines[2].FontSize==12, "production creates literal inline text with original fonts");
                var compact=view.Inlines.ToArray(); update(0,view,false,true);
                Check(CompactLobbyChatLayout.Matches(view.Inlines,compact),"reapplying does not duplicate messages");
                update(0,view,false,false);
                Check(CompactLobbyChatLayout.Matches(view.Inlines,original),"disable restores the exact original history");
            }
        }
        for(int slot=0;slot<5;slot++)
        {
            var view=new Noesis.TextBlock();
            var system=new Noesis.InlineUIContainer { Child=new Noesis.TextBlock {Text="System",Width=600,Height=16,FontSize=14} };
            view.Inlines.Add(system);view.Inlines.Add(new Noesis.LineBreak());append(view,"Other","Message");
            update(slot,view,true,true);
            Check(view.Inlines.Count==6 && ReferenceEquals(view.Inlines[0],system),"each lobby slot retains system entries");
            view.Inlines.Clear();update(slot,view,false,false);
            Check(view.Inlines.Count==0,"lobby reset never resurrects saved messages");
            append(view,"New lobby","fresh");update(slot,view,true,true);
            Check(view.Inlines.Count==4 && ((Noesis.Run)view.Inlines[2]).Text=="fresh","rebuild replaces the snapshot");
            var foreign=new Noesis.Run("foreign");view.Inlines[1]=foreign;
            update(slot,view,false,false);
            Check(ReferenceEquals(view.Inlines[1],foreign),"foreign inline sequence is never overwritten");
        }
        var invalid=new Noesis.TextBlock();append(invalid,"A","valid");invalid.Inlines.Add(new Noesis.Run("unknown"));
        var before=invalid.Inlines.ToArray();
        try { update(0,invalid,true,true);Check(false,"unknown structure must reject"); }
        catch(TargetInvocationException error) { Check(error.InnerException is InvalidOperationException,"unknown structure rejected before mutation"); }
        Check(CompactLobbyChatLayout.Matches(invalid.Inlines,before),"unknown trailing structure leaves all earlier messages untouched");
        var failure=new Noesis.TextBlock();append(failure,"A","rollback");before=failure.Inlines.ToArray();failure.Inlines.FailNextAdd=true;
        try { update(0,failure,true,true);Check(false,"injected failure must reject"); }
        catch(TargetInvocationException) { }
        Check(CompactLobbyChatLayout.Matches(failure.Inlines,before),"external add failure restores the original sequence");
        var empty=new Noesis.TextBlock();update(0,empty,true,true);Check(empty.Inlines.Count==0,"empty history stays empty");

        var options=new BugfixesAndQoL.BugfixesAndQoLViewModel();
        type.GetField("settings",All).SetValue(feature,options);
        type.GetField("mainThread",All).SetValue(feature,System.Threading.Thread.CurrentThread.ManagedThreadId);
        ((CompactLobbyChatFeature)feature).ApplySetting(); // Before MainViewModel exists.
        var toggle=new Noesis.TextBlock();append(toggle,"toggle","history");
        var lobby=new CrusaderDE.FRONT_Multiplayer { Chat=toggle };
        CrusaderDE.MainViewModel.Instance=new CrusaderDE.MainViewModel { FRONTMultiplayer=lobby };
        CrusaderDE.MainViewModel.viewModelLoaded=true;
        type.GetMethod("OnRebuilt",All).Invoke(feature,new object[]{lobby});
        Check(toggle.Inlines.Count==4,"first redraw applies the saved default after startup readiness");
        options.EnableCompactLobbyChat=false;((CompactLobbyChatFeature)feature).ApplySetting();
        Check(toggle.Inlines.Count==5,"settings event immediately restores existing history");
        options.EnableClientFeatures=false;options.EnableCompactLobbyChat=true;((CompactLobbyChatFeature)feature).ApplySetting();
        Check(toggle.Inlines.Count==5,"client main switch independently suppresses compact rendering");
        options.EnableClientFeatures=true;((CompactLobbyChatFeature)feature).ApplySetting();
        Check(toggle.Inlines.Count==4,"client main switch immediately recompacts the current view");
        toggle.Inlines.Clear();toggle.Inlines.Add(new Noesis.Run("unexpected"));
        Shared.DebugLogHelper.FailLogging=true;
        type.GetMethod("OnRebuilt",All).Invoke(feature,new object[]{lobby});
        Shared.DebugLogHelper.FailLogging=false;
        Check((bool)type.GetField("failed",All).GetValue(feature) && toggle.Inlines.Count==1,
            "unknown layout and failed logging cannot escape the injected callback or erase a foreign view");

        // Execute the actual transform against installed Vanilla IL, including the
        // leave/finally edge that would otherwise skip a naive insertion.
        using(var assembly=AssemblyDefinition.ReadAssembly(Path.Combine(game,"Stronghold Crusader Definitive Edition_Data/Managed/Assembly-CSharp.dll")))
        {
            var method=assembly.MainModule.Types.Single(t=>t.FullName=="CrusaderDE.FRONT_Multiplayer").Methods.Single(m=>m.Name=="refreshLobbyChat");
            using(var context=new ILContext(method))
            {
                context.ReferenceBag = RuntimeILReferenceBag.Instance;
                context.Invoke(il=>
            {
                int index=CompactLobbyChatIl.FindInsertion(il);
                var start=il.Body.Instructions[index];
                var leave=il.Body.Instructions.Single(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Leave_S && i.Operand is ILLabel l && l.Target==start);
                var handler=il.Body.ExceptionHandlers.Single(h=>h.HandlerEnd==start);
                var earlyReturns=il.Body.Instructions.Take(index).Where(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Ret).ToArray();
                Check(earlyReturns.Length>0,"Vanilla throttling returns precede layout insertion");
                CompactLobbyChatIl.Patch(il, _=>{});
                var added=il.Body.Instructions[index];
                Check(added!=start && ((ILLabel)leave.Operand).Target==added && handler.HandlerEnd==added,"foreach exit executes callback outside finally");
                Check(il.Body.Instructions.Count(i=>i.Operand is MethodReference m && m.Name=="ScrollToBottom")==5,"all five original scroll calls retained");
                Check(earlyReturns.All(i=>il.Body.Instructions.IndexOf(i)<index),"no callback added to early return paths");
                start.OpCode=Mono.Cecil.Cil.OpCodes.Ldnull;
                try { CompactLobbyChatIl.FindInsertion(il);Check(false,"changed contract must reject"); }
                catch(InvalidOperationException) { Check(true,"changed IL contract rejected"); }
            });
            }
        }
        var actualRefresh=Type.GetType("CrusaderDE.FRONT_Multiplayer, Assembly-CSharp",true)
            .GetMethod("refreshLobbyChat",BindingFlags.Instance|BindingFlags.NonPublic);
        using(var definition=new MonoMod.Utils.DynamicMethodDefinition(actualRefresh))
        using(var context=new ILContext(definition.Definition))
        {
            context.ReferenceBag=RuntimeILReferenceBag.Instance;
            context.Invoke(il=>CompactLobbyChatIl.Patch(il,_=>{}));
            Check(definition.Generate()!=null,"complete transformed Vanilla body compiles before publication");
        }
        MethodInfo reset=modelType.GetMethod("ResetToDefault",All);
        var resetIl=Instructions(reset);
        Check(resetIl.Select((v,i)=>new{v,i}).Any(x=>x.v.Item2 is MethodInfo m && m.Name=="set_EnableCompactLobbyChat" && x.i>0 && resetIl[x.i-1].Item1==System.Reflection.Emit.OpCodes.Ldc_I4_1),"production reset restores enabled default");
    }
}
