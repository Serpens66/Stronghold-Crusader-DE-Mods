using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Xml;

// Executes real production predicates from the built DLL. Unity/Noesis constructors and
// hook installation are external boundaries and are intentionally not invoked here.
internal static class Program
{
    private static Assembly mod;
    private static Type modelType;
    private static int checks;
    private const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    private static void Check(bool value,string label) { checks++;if(!value)throw new Exception(label); }
    private static object Model(bool h,bool c,string option,bool value)
    {
        object vm=FormatterServices.GetUninitializedObject(modelType);
        Set(vm,"EnableMod",h);Set(vm,"EnableClientFeatures",c);if(option!=null)Set(vm,option,value);return vm;
    }
    private static void Set(object vm,string property,object value)
    {
        string field=char.ToLowerInvariant(property[0])+property.Substring(1);
        FieldInfo f=modelType.GetField(field,All);Check(f!=null,"production backing field exists: "+property);
        if(f.FieldType==typeof(bool)||f.FieldType==typeof(int))f.SetValue(vm,value);
        else f.SetValue(vm,Activator.CreateInstance(f.FieldType,All,null,new object[]{value,null},null));
    }
    private static object Feature(string name,object vm)
    {
        Type type=mod.GetType("BugfixesAndQoL."+name,true);
        object feature=FormatterServices.GetUninitializedObject(type);
        type.GetField("settings",All).SetValue(feature,vm);return feature;
    }
    private static bool Bool(object feature,string member)
    {
        PropertyInfo p=feature.GetType().GetProperty(member,All);
        return p!=null?(bool)p.GetValue(feature,null):(bool)feature.GetType().GetMethod(member,All).Invoke(feature,null);
    }
    private static void Matrix(string feature,string member,string option,bool host)
    {
        // Explicit product truth tables, ordered H/C/O. They expose the original H=false,C=true defect.
        bool[] client={false,false,false,true,false,false,false,true};
        bool[] server={false,false,false,false,false,true,false,true};
        int index=0;
        foreach(bool h in new[]{false,true})foreach(bool c in new[]{false,true})foreach(bool o in new[]{false,true})
        {
            object vm=Model(h,c,option,o);
            Check(Bool(Feature(feature,vm),member)==(host?server:client)[index++],feature+" "+member+" H="+h+",C="+c+",O="+o);
        }
    }
    public static int Main(string[] args)
    {
        try
        {
            string workspace=Path.GetFullPath(args[0]), dll=Path.GetFullPath(args[1]);
            string game=@"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
            string api=args.Length>2?args[2]:Environment.GetEnvironmentVariable("API_SHARED_DIR");
            string[] dirs={Path.GetDirectoryName(dll),api,Path.Combine(workspace,"APIShared/BepInEx/plugins/APIShared_Serp"),Path.Combine(game,"BepInEx/plugins/000shcdese"),Path.Combine(game,"BepInEx/core"),Path.Combine(game,"Stronghold Crusader Definitive Edition_Data/Managed")};
            AppDomain.CurrentDomain.AssemblyResolve+=(_,e)=>
            {
                string name=new AssemblyName(e.Name).Name+".dll";
                foreach(string dir in dirs){if(string.IsNullOrWhiteSpace(dir))continue;string path=Path.Combine(dir,name);if(File.Exists(path))return Assembly.LoadFrom(path);}return null;
            };
            mod=Assembly.LoadFrom(dll);modelType=mod.GetType("BugfixesAndQoL.BugfixesAndQoLViewModel",true);
            string manifest=File.ReadAllText(Path.Combine(workspace,"BugfixesAndQoL/info.json"));
            string version=System.Text.RegularExpressions.Regex.Match(manifest,"\"Version\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value;
            Check(mod.GetName().Version.ToString()==version+".0","built assembly version matches manifest");
            var versionInfo=System.Diagnostics.FileVersionInfo.GetVersionInfo(dll);
            Check(versionInfo.FileVersion==version+".0" && versionInfo.ProductVersion==version,"built file/informational versions match manifest");
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve+=(_,e)=>
            {
                string name=new AssemblyName(e.Name).Name+".dll";
                foreach(string dir in dirs){if(string.IsNullOrWhiteSpace(dir))continue;string path=Path.Combine(dir,name);if(File.Exists(path))return Assembly.ReflectionOnlyLoad(File.ReadAllBytes(path));}
                return Assembly.ReflectionOnlyLoad(e.Name);
            };
            // Read plugin metadata without loading its Unity/BepInEx base class for execution.
            var metadata=Assembly.ReflectionOnlyLoad(File.ReadAllBytes(dll));
            Check((string)metadata.GetType("BugfixesAndQoL.BugfixesAndQoLPlugin").GetField("PluginVersion",All).GetRawConstantValue()==version,"plugin version matches package manifest");
            foreach(var test in new[]{
                new[]{"LobbyYellowContrastFeature","IsActive","ImproveYellowLobbyContrast"},
                new[]{"DamagedHealthBarsRuntime","FeatureEnabled","EnableDamagedHealthBars"},
                new[]{"NotificationSkipFeature","Enabled","EnableCompleteNotificationSkipOnClick"},
                new[]{"NotificationLastFrameFeature","Enabled","EnableNotificationLastFrame"},
                new[]{"VanillaMapEditorHook","FeatureEnabled","ShowLoadSaveDialogControls"},
                new[]{"TrailDeletionViewModel","Enabled","ShowLoadSaveDialogControls"},
                new[]{"CustomLordListEnhancementHook","IsActive","EnableLordAndMapListEnhancements"},
                new[]{"MapOriginSortHook","IsActive","EnableLordAndMapListEnhancements"},
                new[]{"AiCastleSettingsListEnhancementHook","PresentationEnabled","EnableLordAndMapListEnhancements"},
                new[]{"BugfixesAndQoLRuntime","MissingMapRecoveryEnabled","EnableMissingMapSaveRecovery"}})
                Matrix(test[0],test[1],test[2],false);
            foreach(var test in new[]{
                new[]{"AiCastleSettingsListEnhancementHook","HostEnhancementsEnabled","EnableCustomLordListEnhancements"},
                new[]{"CoopCustomLordSelectionFeature","EnhancementsEnabled","EnableCustomLordListEnhancements"},
                new[]{"MultiplayerAivSyncRuntime","IsFeatureActive","EnableCustomLordListEnhancements"},
                new[]{"SkirmishGameOptionsRuntime","Enabled","ShowMpAdvancedSettingsInSingleplayer"},
                new[]{"NoDogsNativePatch","DesiredEnabled","ShowMpAdvancedSettingsInSingleplayer"},
                new[]{"VanillaPeaceTimeGameplayPatch","DesiredEnabled","EnablePeaceTimeGameplayFixes"},
                new[]{"AbruptHostMigrationFix","StartupStateFixEnabled","EnableMultiplayerStartupStateFix"}})
                Matrix(test[0],test[1],test[2],true);
            Matrix("SkirmishAiSelectionMemoryHook","RandomEnhancementsEnabled","EnableRandomOpponentSelectionEnhancements",true);
            foreach(bool h in new[]{false,true})foreach(bool c in new[]{false,true})foreach(bool option in new[]{false,true})
            {
                object runtime=Feature("SkirmishGameOptionsRuntime",Model(h,c,"EnablePeaceTimeGameplayFixes",option));
                runtime.GetType().GetField("peaceTimeNativeAvailable",All).SetValue(runtime,true);
                Check(Bool(runtime,"PeaceAvailable")== (h&&option),"actual dialog capability follows independent peace option");
                runtime.GetType().GetField("peaceTimeNativeAvailable",All).SetValue(runtime,false);
                Check(!Bool(runtime,"PeaceAvailable"),"failed native capability never exposes a usable peace setting");
            }
            foreach(bool h in new[]{false,true})foreach(bool c in new[]{false,true})
                Check(Bool(Feature("SurrenderFeature",Model(h,c,null,false)),"StatisticsTeamBadgesEnabled")==c,"real independent badge snapshot gate");
            foreach(bool h in new[]{false,true})foreach(bool c in new[]{false,true})foreach(bool selection in new[]{false,true})foreach(bool presentation in new[]{false,true})
            {
                object vm=Model(h,c,"EnableCustomLordListEnhancements",selection);Set(vm,"EnableLordAndMapListEnhancements",presentation);
                object dialog=Feature("AiCastleSettingsListEnhancementHook",vm);
                Check(Bool(dialog,"IsActive")==((h&&selection)||(c&&presentation)),"both independent AI-settings consumers remain operable");
                if(h&&selection)
                    Check((int)dialog.GetType().GetMethod("GetActiveAivLimit",All).Invoke(dialog,new object[]{null})==50,"personal display off preserves the allowed host candidate capacity");
            }
            Policies();
            object protectedMap=Feature("VanillaMapEditorHook",Model(false,false,"ShowLoadSaveDialogControls",false));
            Check(!Bool(protectedMap,"OriginalMapProtectionEnabled"),"unused map protection has no hidden feature activation");
            protectedMap.GetType().GetField("preserveOriginalMapProtection",All).SetValue(protectedMap,true);
            Check(Bool(protectedMap,"OriginalMapProtectionEnabled") && !Bool(protectedMap,"FeatureEnabled"),"already exposed original stays protected while optional dialogs are off");
            Metadata(workspace);
            CallerGates();
            Traversal();
            Console.WriteLine("PASS: built production DLL settings matrices, independent AI-list capacity, callers, classifications/defaults/XAML: "+checks+" checks.");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    private static void Traversal()
    {
        bool[] enabled={false,true,false,true,false,true,true,true};
        int index=0;
        foreach(bool h in new[]{false,true})foreach(bool own in new[]{false,true})foreach(bool provider in new[]{false,true})
        {
            foreach(bool required in new[]{false,true})
            {
                BugfixesAndQoL.UnitCommands.UnitCommandPathAPI.Traversal=new BugfixesAndQoL.UnitCommands.UnitCommandTraversalProvider{Enabled=provider,RequiredOnly=required};
                var snapshot=BugfixesAndQoL.UnitCommands.MovementOptionsSnapshot.Capture(new BugfixesAndQoL.UnitCommands.TraversalSettings{EnableMod=h,EnableImprovedManualUnitCommands=own});
                Check(snapshot.Enabled==enabled[index],"production movement demand: own H/option and independent provider");
                Check(snapshot.TraversalEnabled==provider,"production provider demand is independent of own Host");
                Check(snapshot.RequiredOnly==required,"provider route mode preserved");
            }
            index++;
        }
        BugfixesAndQoL.UnitCommands.UnitCommandPathAPI.Traversal=null;
        var absent=BugfixesAndQoL.UnitCommands.MovementOptionsSnapshot.Capture(new BugfixesAndQoL.UnitCommands.TraversalSettings());
        Check(!absent.Enabled && !absent.TraversalEnabled,"no hidden command feature without own or foreign demand");
    }
    private static void Policies()
    {
        foreach(string name in new[]{"LobbyYellowContrastPolicy","BriefingNoStartingGoldFixPolicy"})
        {
            MethodInfo m=mod.GetType("BugfixesAndQoL."+name).GetMethod("IsEnabled",All);
            Check(m.GetParameters().Length==2,"local production policy has no Host input: "+name);
            Check((bool)m.Invoke(null,new object[]{true,true}),"local policy active independently of Host");
            Check(!(bool)m.Invoke(null,new object[]{false,true})&&!(bool)m.Invoke(null,new object[]{true,false}),"both local disabled paths");
        }
        Type click=mod.GetType("BugfixesAndQoL.PlacementCancelRightClickPolicy");
        MethodInfo down=click.GetMethod("BeginRightClickGesture",All),up=click.GetMethod("CompleteRightClickGesture",All);
        object[] parameters={true,true,5,false,false};
        Check(!(bool)down.Invoke(null,parameters),"placement Down suppressed");
        object[] pending={parameters[4]};Check(!(bool)up.Invoke(null,pending),"matching Up suppressed even if settings change between events");
        Check((bool)up.Invoke(null,pending),"only one Up suppressed");
    }
    private static void Metadata(string workspace)
    {
        var scopes=new Dictionary<string,bool>{{"EnableLordAndMapListEnhancements",false},{"EnableMissingMapSaveRecovery",false},{"EnableRandomOpponentSelectionEnhancements",true},{"EnableMultiplayerStartupStateFix",true},{"EnablePeaceTimeGameplayFixes",true}};
        MethodBase ctor=modelType.GetConstructors(All).Single();
        List<Tuple<OpCode,object>> body=Instructions(ctor);
        var xaml=new XmlDocument();xaml.Load(Path.Combine(workspace,"BugfixesAndQoL/Override/ScriptExtenderUI/BugfixesAndQoLSettings.xaml"));
        foreach(var pair in scopes)
        {
            PropertyInfo property=modelType.GetProperty(pair.Key);
            string attr=pair.Value?"SyncHostOnlyAttribute":"PresetLocalAttribute";
            Check(property.GetCustomAttributesData().Any(a=>a.AttributeType.Name==attr),"new setting category "+pair.Key);
            FieldInfo field=modelType.GetField(char.ToLowerInvariant(pair.Key[0])+pair.Key.Substring(1),All);
            Check(body.Select((v,i)=>new{v,i}).Any(x=>x.v.Item1==OpCodes.Stfld && Equals(x.v.Item2,field) && x.i>0 && body[x.i-1].Item1==OpCodes.Ldc_I4_1),"new setting constructor default true: "+pair.Key);
            XmlNode node=xaml.SelectSingleNode("//*[local-name()='CheckBox' and contains(@IsChecked,'"+pair.Key+",')]");
            Check(node!=null,"new setting has a UI editor");
            Check(node.Attributes["ToolTipService.ShowDuration"]?.Value=="60000" && node.Attributes["ToolTip"]!=null,"new setting tooltip contract");
            string role=pair.Value?"CanEditHostSettings":"CanEditClientSettings";
            Check(Ancestors(node).Any(a=>a.Attributes?["IsEnabled"]?.Value=="{Binding "+role+"}"),"new editor inherits its own role authority");
        }
        XmlNode workshop=xaml.SelectSingleNode("//*[local-name()='CheckBox' and contains(@IsChecked,'EnableWorkshopUploadLordSelectionFix,')]");
        Check(Ancestors(workshop).Any(a=>a.Attributes?["IsEnabled"]?.Value=="{Binding CanEditClientSettings}"),"Workshop is locally editable by a multiplayer client");
        Check(!Ancestors(workshop).Any(a=>a.Attributes?["IsEnabled"]?.Value=="{Binding CanEditHostSettings}"),"Workshop no longer inherits Host authority");
    }
    private static IEnumerable<XmlNode> Ancestors(XmlNode n){for(;n!=null;n=n.ParentNode)yield return n;}
    private static void CallerGates()
    {
        foreach(var pair in new[]{new[]{"LobbySaveLoadingFeature","ResolveSoloLoad","get_EnableSoloMultiplayerSaveLoading"},new[]{"LobbySaveLoadingFeature","AfterSetup","get_FixSkirmishLoadButtonVisibility"},new[]{"WorkshopUploadLordSelectionFix","ButtonClickedHook","get_EnableWorkshopUploadLordSelectionFix"}})
        {
            MethodInfo method=mod.GetType("BugfixesAndQoL."+pair[0]).GetMethod(pair[1],All);
            string[] calls=Instructions(method).Select(i=>i.Item2 as MethodBase).Where(m=>m!=null).Select(m=>m.Name).ToArray();
            Check(calls.Contains("get_EnableClientFeatures") && calls.Contains(pair[2]) && !calls.Contains("get_EnableMod"),"real compiled callback gates: "+pair[0]+"."+pair[1]);
        }
    }
    private static List<Tuple<OpCode,object>> Instructions(MethodBase method)
    {
        var ops=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static).Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)).ToDictionary(o=>(ushort)o.Value);
        byte[] il=method.GetMethodBody().GetILAsByteArray();var list=new List<Tuple<OpCode,object>>();
        for(int n=0;n<il.Length;)
        {
            ushort code=il[n++];if(code==0xfe)code=(ushort)(0xfe00|il[n++]);OpCode op=ops[code];object value=null;int size=0;
            switch(op.OperandType)
            {
                case OperandType.InlineField:value=method.Module.ResolveField(BitConverter.ToInt32(il,n));size=4;break;
                case OperandType.InlineMethod:value=method.Module.ResolveMethod(BitConverter.ToInt32(il,n));size=4;break;
                case OperandType.InlineNone:break;
                case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineI:case OperandType.ShortInlineVar:size=1;break;
                case OperandType.InlineVar:size=2;break;
                case OperandType.InlineI8:case OperandType.InlineR:size=8;break;
                case OperandType.InlineSwitch:size=4+4*BitConverter.ToInt32(il,n);break;
                default:size=4;break;
            }
            n+=size;list.Add(Tuple.Create(op,value));
        }
        return list;
    }
}
