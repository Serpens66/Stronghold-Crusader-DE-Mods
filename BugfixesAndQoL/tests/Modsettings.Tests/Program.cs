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
internal static partial class Program
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

    private static void LocalAutosaveFileTests()
    {
        MethodInfo write = mod.GetType("BugfixesAndQoL.LocalAutosaveRuntime", true)
            .GetMethod("WriteSaveFile", All);
        string directory = Path.Combine(Path.GetTempPath(), "shcde-autosave-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string local = Path.Combine(directory, "Autosave - Singleplayer.sav");
        string multiplayer = Path.Combine(directory, "autosave.msv");

        byte[] SaveFrame(byte value, bool archive = false)
        {
            using (var bytes = new MemoryStream())
            using (var writer = new BinaryWriter(bytes))
            {
                writer.Write(0xFFFFFFFEu);
                for (int block = 0; block < 6; block++) writer.Write(0);
                writer.Write(4036);
                writer.Write(1); // Declared native payload length.
                writer.Write(1); // One section.
                writer.Write(new byte[4036 - 3 * sizeof(int)]);
                writer.Write(value);
                if (archive)
                {
                    writer.Write(0x06054B50u);
                    writer.Write(new byte[18]); // Valid empty ZIP, with no comment.
                }
                return bytes.ToArray();
            }
        }

        void Save(byte[] contents, bool complete)
        {
            write.Invoke(null, new object[] { directory, (Func<string, bool>)(path =>
            {
                Check(Path.GetDirectoryName(path) != directory && Path.GetExtension(path) == ".sav",
                    "game writes a staged save outside the visible save listing");
                File.WriteAllBytes(path, contents);
                return complete;
            }) });
        }

        try
        {
            File.WriteAllText(multiplayer, "online");
            byte[] first = SaveFrame(1);
            byte[] second = SaveFrame(2, archive: true);
            Save(first, true);
            Check(File.ReadAllBytes(local).SequenceEqual(first), "first autosave is published");
            bool published = (bool)write.Invoke(null, new object[] { directory, (Func<string, bool>)(_ => false) });
            Check(!published && File.ReadAllBytes(local).SequenceEqual(first),
                "a refusal before writing can be retried without changing the previous save");

            foreach (var failure in new[]
            {
                (Contents: new byte[] { 1 }, Complete: false),
                (Contents: Array.Empty<byte>(), Complete: true),
                (Contents: first.Take(first.Length - 1).ToArray(), Complete: true),
                (Contents: second.Where((_, index) => index != first.Length - 1).ToArray(), Complete: true),
                (Contents: second.Take(second.Length - 1).ToArray(), Complete: true),
            })
            {
                try
                {
                    Save(failure.Contents, failure.Complete);
                    throw new Exception("incomplete save was accepted");
                }
                catch (TargetInvocationException error) when (error.InnerException is IOException)
                {
                    Check(File.ReadAllBytes(local).SequenceEqual(first), "failed writer preserves previous autosave");
                }
            }

            Save(second, true);
            Check(File.ReadAllBytes(local).SequenceEqual(second), "successful writer replaces previous autosave");
            Check(File.ReadAllText(multiplayer) == "online", "separate multiplayer autosave is preserved");
            Check(Directory.GetFiles(directory, "*.sav", SearchOption.AllDirectories).Length == 1,
                "only the completed local autosave remains");
            Check(Directory.GetDirectories(directory).Length == 0, "staging directories are removed");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void PeaceTimeWildlifeRuleTests()
    {
        var type = mod.GetType("BugfixesAndQoL.VanillaPeaceTimeGameplayPatch", true);
        var rules = (Array)type.GetField("WildlifeRules", All).GetValue(null);
        var actual = new HashSet<(string Species, int Player)>();
        foreach (object rule in rules)
        {
            Type ruleType = rule.GetType();
            object attacker = ruleType.GetProperty("Attacker").GetValue(rule);
            object target = ruleType.GetProperty("Target").GetValue(rule);
            Type selectorType = attacker.GetType();
            PropertyInfo playerId = selectorType.GetProperty("PlayerId");
            PropertyInfo unitType = selectorType.GetProperty("UnitType");
            PropertyInfo globalId = selectorType.GetProperty("GlobalId");
            Check(!(bool)ruleType.GetProperty("Bidirectional").GetValue(rule) &&
                ruleType.GetProperty("Condition").GetValue(rule).ToString() == "WhilePeaceTimeActive" &&
                playerId.GetValue(attacker) == null && globalId.GetValue(attacker) == null &&
                unitType.GetValue(target) == null && globalId.GetValue(target) == null,
                "wildlife rule has the original direction, native condition and selector scope");
            Check(actual.Add((unitType.GetValue(attacker).ToString(), (int)playerId.GetValue(target))),
                "wildlife rule covers a unique species and player slot");
        }
        string[] predators = { "CHIMP_TYPE_LION", "CHIMP_TYPE_HYENA", "CHIMP_TYPE_CROCODILE" };
        Check(actual.SetEquals(predators.SelectMany(predator => Enumerable.Range(1, 8)
            .Select(player => (predator, player)))), "wildlife rule covers all and only the audited predators and slots 1..8");
    }

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--peace-policy-only")
            {
                PeaceTimeCivilianPolicyTests();
                PeaceTimeCivilianRuntimeTests();
                Console.WriteLine("PASS: peace-time civilian policy checks: " + checks);
                return 0;
            }
            string workspace=Path.GetFullPath(args[0]), dll=Path.GetFullPath(args[1]);
            string game=@"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
            string api=args.Length>2?args[2]:Environment.GetEnvironmentVariable("API_SHARED_DIR");
            string[] dirs={Path.GetDirectoryName(dll),api,Path.Combine(workspace,"APIShared/BepInEx/plugins/APIShared_Serp"),Path.Combine(game,"BepInEx/plugins/000shcdese"),Path.Combine(game,"BepInEx/core"),Path.Combine(game,"Stronghold Crusader Definitive Edition_Data/Managed")};
            var byteLoaded = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
            AppDomain.CurrentDomain.AssemblyResolve+=(_,e)=>
            {
                string name=new AssemblyName(e.Name).Name+".dll";
                // The explicit BepInEx reference can retain Windows download-zone metadata.
                // Keep game/Noesis load-context identity; do not change installed files.
                foreach (string dir in dirs)
                {
                    if (string.IsNullOrWhiteSpace(dir)) continue;
                    string path = Path.Combine(dir, name);
                    if (!File.Exists(path)) continue;
                    if (name != "BepInEx.dll" && !name.StartsWith("Mono.", StringComparison.Ordinal) &&
                        !name.StartsWith("MonoMod.", StringComparison.Ordinal)) return Assembly.LoadFrom(path);
                    // The test runner and production DLL must share the exact Cecil/MonoMod type identities.
                    if (!byteLoaded.TryGetValue(path, out Assembly loaded))
                        byteLoaded.Add(path, loaded = Assembly.Load(File.ReadAllBytes(path)));
                    return loaded;
                }
                return null;
            };
            mod=Assembly.LoadFrom(dll);modelType=mod.GetType("BugfixesAndQoL.BugfixesAndQoLViewModel",true);
            if (args.Length > 3 && args[3] == "--settings-metadata-only")
            {
                Metadata(workspace);
                SettingsPresentationTests(workspace);
                Console.WriteLine("PASS: production settings metadata, defaults, reset, report slots and UI: " + checks);
                return 0;
            }
            LocalAutosaveFileTests();
            if (args.Length > 3 && args[3] == "--local-autosave-only")
            {
                Console.WriteLine("PASS: local autosave file checks: " + checks);
                return 0;
            }
            FixesSuppressMessagesSyncTests(game);
            object presetModel = FormatterServices.GetUninitializedObject(modelType);
            var retired = modelType.GetMethod("IsRetiredPresetProperty", All);
            foreach (string key in new[] { "EnableSpectatorPerspective", "EnableEliminatedPlayersBecomeSpectators" })
                Check((bool)retired.Invoke(presetModel, new object[] { key }), "known removed spectator preference is retired: " + key);
            Check(!(bool)retired.Invoke(presetModel, new object[] { "UnknownSpectatorPreference" }), "unknown properties still fail preset validation");
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
                new[]{"CompactLobbyChatFeature","IsActive","EnableCompactLobbyChat"},
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
                new[]{"PeaceTimeCivilianProximityFeature","DesiredEnabled","EnablePeaceTimeCivilianProximityReduction"},
                new[]{"AbruptHostMigrationFix","StartupStateFixEnabled","EnableMultiplayerStartupStateFix"}})
                Matrix(test[0],test[1],test[2],true);
            PeaceTimeWildlifeRuleTests();
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
                Check(Bool(Feature("StatisticsTeamBadges",Model(h,c,null,false)),"StatisticsTeamBadgesEnabled")==c,"real independent badge snapshot gate");
            foreach(bool h in new[]{false,true})foreach(bool c in new[]{false,true})foreach(bool selection in new[]{false,true})foreach(bool presentation in new[]{false,true})
            {
                object vm=Model(h,c,"EnableCustomLordListEnhancements",selection);Set(vm,"EnableLordAndMapListEnhancements",presentation);
                object dialog=Feature("AiCastleSettingsListEnhancementHook",vm);
                Check(Bool(dialog,"IsActive")==((h&&selection)||(c&&presentation)),"both independent AI-settings consumers remain operable");
                if(h&&selection)
                    Check((int)dialog.GetType().GetMethod("GetActiveAivLimit",All).Invoke(dialog,new object[]{null})==50,"personal display off preserves the allowed host candidate capacity");
            }
            Type treeRuntime = mod.GetType("BugfixesAndQoL.TreeBlightDecay.FixRuntime", true);
            treeRuntime.GetMethod("ValidateLayout", All).Invoke(null, null);
            var treeGate = treeRuntime.GetMethod("IsEnabled", All);
            foreach(bool host in new[]{false,true}) foreach(bool option in new[]{false,true})
                Check((bool)treeGate.Invoke(null,new object[]{host,option}) == (host && option), "tree decay uses host activation and synchronized option");
            IntPtr treeState = System.Runtime.InteropServices.Marshal.AllocHGlobal(32);
            try
            {
                object tree = FormatterServices.GetUninitializedObject(treeRuntime);
                treeRuntime.GetField("state", All).SetValue(tree, treeState);
                foreach(bool ready in new[]{false,true}) foreach(bool host in new[]{false,true}) foreach(bool option in new[]{false,true})
                {
                    treeRuntime.GetField("ready", All).SetValue(tree, ready);
                    treeRuntime.GetMethod("SetEnabled", All).Invoke(tree, new object[]{host,option});
                    Check(System.Runtime.InteropServices.Marshal.ReadInt32(treeState) == (ready && host && option ? 1 : 0),
                        "tree decay native flag cannot activate after failed validation");
                }
            }
            finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(treeState); }
            Type lordRuntime = mod.GetType("BugfixesAndQoL.LordTeleportGuard.LordTeleportGuardRuntime", true);
            IntPtr lordFlag = System.Runtime.InteropServices.Marshal.AllocHGlobal(4);
            try
            {
                object lord = FormatterServices.GetUninitializedObject(lordRuntime);
                lordRuntime.GetField("enabled", All).SetValue(lord, lordFlag);
                foreach(bool ready in new[]{false,true}) foreach(bool main in new[]{false,true}) foreach(bool option in new[]{false,true})
                {
                    lordRuntime.GetField("ready", All).SetValue(lord, ready);
                    lordRuntime.GetMethod("SetEnabled", All).Invoke(lord,new object[]{main,option});
                    Check(System.Runtime.InteropServices.Marshal.ReadInt32(lordFlag) == (ready && main && option ? 1 : 0),
                        "integrated Lord native flag follows readiness/main/host option");
                }
            }
            finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(lordFlag); }
            Policies();
            object protectedMap=Feature("VanillaMapEditorHook",Model(false,false,"ShowLoadSaveDialogControls",false));
            Check(!Bool(protectedMap,"OriginalMapProtectionEnabled"),"unused map protection has no hidden feature activation");
            protectedMap.GetType().GetField("preserveOriginalMapProtection",All).SetValue(protectedMap,true);
            Check(Bool(protectedMap,"OriginalMapProtectionEnabled") && !Bool(protectedMap,"FeatureEnabled"),"already exposed original stays protected while optional dialogs are off");
            PeaceTimeCivilianPolicyTests();
            PeaceTimeCivilianRuntimeTests();
            PeaceTimeCivilianCompiledTests(workspace);
            Metadata(workspace);
            SettingsPresentationTests(workspace);
            CompactLobbyChatTests(workspace, game);
            CallerGates();
            Console.WriteLine("PASS: built production DLL settings matrices, independent AI-list capacity, callers, classifications/defaults/XAML: "+checks+" checks.");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }

    private sealed class UnrelatedSettings
    {
        public bool SuppressMessages { get; set; }
    }

    private static void FixesSuppressMessagesSyncTests(string game)
    {
        string path = Path.Combine(game, "BepInEx/plugins/fixes/fixes.dll");
        if (!File.Exists(path)) return;

        var feature = mod.GetType("BugfixesAndQoL.FixesSuppressMessagesSync", true);
        var findProperty = feature.GetMethod("FindLocalProperty", All);
        Check(findProperty.Invoke(null, new object[] { typeof(Program).Assembly }) == null,
            "a different mod cannot opt into the Fixes compatibility filter by using the same property name");
        var property = (PropertyInfo)findProperty.Invoke(null, new object[] { Assembly.LoadFrom(path) });
        if (property == null) return; // A newer Fixes build can correct the missing companion itself.

        var allowSend = (Func<PropertyInfo, bool, bool>)Delegate.CreateDelegate(typeof(Func<PropertyInfo, bool, bool>),
            feature.GetMethod("AllowSend", All));
        var isLocal = (Func<PropertyInfo, bool>)Delegate.CreateDelegate(typeof(Func<PropertyInfo, bool>),
            feature.GetMethod("IsLocal", All));
        var active = feature.GetField("localProperty", All);
        Check(allowSend(property, true) && !isLocal(property), "unpublished compatibility hooks pass through original routing");
        try
        {
            active.SetValue(null, property);
            var other = property.DeclaringType.GetProperty("PlayerKeepRotation");
            var namesake = typeof(UnrelatedSettings).GetProperty(nameof(UnrelatedSettings.SuppressMessages));
            Check(!allowSend(property, true) && isLocal(property), "Fixes suppression stays local in both directions");
            Check(other != null && allowSend(other, true) && !isLocal(other), "Fixes keep rotation still synchronizes");
            Check(allowSend(namesake, true) && !isLocal(namesake) && !allowSend(namesake, false),
                "other mods and Extender routing decisions are preserved");
        }
        finally { active.SetValue(null, null); }

        // Transform and compile the real installed Extender bodies, without patching the game or opening a lobby.
        var manager = Type.GetType("SHCDESE.API.GameXAMLManagerAPI, SHCDESE", true);
        var change = (MethodInfo)feature.GetMethod("FindChangeHandler", All).Invoke(null, new object[] { manager });
        var probe = feature.GetMethod("Probe", All);
        var patchSend = feature.GetMethod("PatchSend", All);
        MonoMod.Cil.ILContext.Manipulator changePatch = il => patchSend.Invoke(null, new object[] { il, "BroadcastSettingChange" });
        MonoMod.Cil.ILContext.Manipulator joinPatch = il => patchSend.Invoke(null, new object[] { il, "SendPacketToSteamId" });
        var receivePatch = (MonoMod.Cil.ILContext.Manipulator)Delegate.CreateDelegate(typeof(MonoMod.Cil.ILContext.Manipulator),
            feature.GetMethod("PatchReceive", All));
        var receive = manager.GetMethod("ApplyPerPlayerUpdate", All);
        probe.Invoke(null, new object[] { change, changePatch });
        probe.Invoke(null, new object[] { manager.GetMethod("SyncSettingsToNewPlayer", All), joinPatch });
        using (var definition = new MonoMod.Utils.DynamicMethodDefinition(receive))
        using (var context = new MonoMod.Cil.ILContext(definition.Definition))
        {
            context.ReferenceBag = MonoMod.Cil.RuntimeILReferenceBag.Instance;
            context.Invoke(il =>
            {
                var original = il.Body.Instructions[0];
                receivePatch(il);
                var guardReturn = original.Previous;
                Check(guardReturn.OpCode == Mono.Cecil.Cil.OpCodes.Ret &&
                    guardReturn.Previous.Operand is MonoMod.Cil.ILLabel continueOriginal && continueOriginal.Target == original,
                    "the legacy receive guard returns before the original body and other properties branch to it");
            });
            var patchedReceive = definition.Generate();
            try
            {
                active.SetValue(null, property);
                patchedReceive.Invoke(null, new object[] { null, property, null, null, null });
                Check(true, "the transformed Extender body executes for a legacy Fixes property");
            }
            finally { active.SetValue(null, null); }
        }
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
        var scopes=new Dictionary<string,bool>{{"EnableCompactLobbyChat",false},{"EnableLordAndMapListEnhancements",false},{"EnableMissingMapSaveRecovery",false},{"EnablePitchMoatLines",false},{"EnableRandomOpponentSelectionEnhancements",true},{"EnableMultiplayerStartupStateFix",true},{"EnablePeaceTimeGameplayFixes",true},{"EnablePeaceTimeCivilianProximityReduction",true},{"EnablePitchStairsFix",true},{"EnableSiegeCrewDuplicationFix",true},{"EnableSiegeCrewFireLossFix",true},{"EnableTreeBlightDecayFix",true},{"EnablePrebuiltAiStorageFix",true},{"EnableAiOutpostRetargetFix",true}};
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
            XmlNode popupHelp = node.SelectSingleNode("./*[local-name()='CheckBox.ToolTip']/*[local-name()='ToolTip']/@Content");
            Check(node.Attributes["ToolTipService.ShowDuration"]?.Value == "60000" &&
                !string.IsNullOrWhiteSpace(popupHelp?.Value), "new setting tooltip contract");
            string role=pair.Value?"CanEditHostSettings":"CanEditClientSettings";
            Check(Ancestors(node).Any(a=>a.Attributes?["IsEnabled"]?.Value=="{Binding "+role+"}"),"new editor inherits its own role authority");
        }
        var presetApplication = modelType.BaseType.Assembly.GetType("APIShared.ModSettings.ModSettingsApplication", true);
        var hostProperties = (PropertyInfo[])presetApplication.GetMethod("GetHostProperties")
            .Invoke(null, new[] { FormatterServices.GetUninitializedObject(modelType) });
        var reset = Instructions(modelType.GetMethod("ResetToDefault", All));
        foreach (string option in new[] { "EnableSiegeCrewDuplicationFix", "EnableSiegeCrewFireLossFix" })
        {
            Check(hostProperties.Any(property => property.Name == option), "preset discovery includes host option " + option);
            var setter = modelType.GetProperty(option).GetSetMethod();
            Check(reset.Select((instruction, index) => new { instruction, index }).Any(entry =>
                Equals(entry.instruction.Item2, setter) && entry.index > 0 &&
                reset[entry.index - 1].Item1 == OpCodes.Ldc_I4_1), "reset restores enabled default for " + option);
        }
        var pitchLines = modelType.GetProperty("EnablePitchMoatLines");
        Check(!hostProperties.Any(property => property == pitchLines), "pitch/moat preference is excluded from host snapshots");
        Check(!pitchLines.GetCustomAttributesData().Any(attribute => attribute.AttributeType.Name == "SyncPerPlayerAttribute"),
            "pitch/moat preference remains local instead of being broadcast");
        Check(reset.Select((instruction, index) => new { instruction, index }).Any(entry =>
            Equals(entry.instruction.Item2, pitchLines.GetSetMethod()) && entry.index > 0 &&
            reset[entry.index - 1].Item1 == OpCodes.Ldc_I4_1), "reset enables pitch/moat lines");

        var report = modelType.GetProperty("PitchMoatLineCompatibilityReport");
        var reportAttributes = report.GetCustomAttributesData().Select(attribute => attribute.AttributeType.Name).ToArray();
        Check(reportAttributes.Contains("SyncPerPlayerAttribute") && reportAttributes.Contains("DoNotPersistAttribute"),
            "pitch/moat compatibility is a nonpersistent per-player report");
        var reportValue = modelType.GetField("pitchMoatLineCompatibilityReport", All);
        Check(body.Select((instruction, index) => new { instruction, index }).Any(entry =>
            entry.instruction.Item1 == OpCodes.Stfld && Equals(entry.instruction.Item2, reportValue) && entry.index > 0 &&
            ((body[entry.index - 1].Item1 == OpCodes.Ldsfld && Equals(body[entry.index - 1].Item2, typeof(string).GetField("Empty"))) ||
             (body[entry.index - 1].Item1 == OpCodes.Ldstr && Equals(body[entry.index - 1].Item2, "")))),
            "pitch/moat compatibility starts unavailable until runtime validation succeeds");
        Check(modelType.GetProperty("PitchMoatLineCompatibilityReportData").PropertyType == typeof(string[]),
            "pitch/moat compatibility report has its player-slot companion");
        var reportData = modelType.GetField("pitchMoatLineCompatibilityReportData", All);
        Check(body.Select((instruction, index) => new { instruction, index }).Any(entry =>
            entry.instruction.Item1 == OpCodes.Stfld && Equals(entry.instruction.Item2, reportData) && entry.index >= 2 &&
            body[entry.index - 1].Item1 == OpCodes.Newarr && Equals(body[entry.index - 1].Item2, typeof(string)) &&
            (body[entry.index - 2].Item1 == OpCodes.Ldc_I4_S || body[entry.index - 2].Item1 == OpCodes.Ldc_I4) &&
            Convert.ToInt32(body[entry.index - 2].Item2) == 9),
            "pitch/moat compatibility report reserves exactly slots zero through eight");
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
                case OperandType.InlineType:value=method.Module.ResolveType(BitConverter.ToInt32(il,n));size=4;break;
                case OperandType.InlineString:value=method.Module.ResolveString(BitConverter.ToInt32(il,n));size=4;break;
                case OperandType.InlineI:value=BitConverter.ToInt32(il,n);size=4;break;
                case OperandType.ShortInlineI:value=unchecked((sbyte)il[n]);size=1;break;
                case OperandType.InlineNone:break;
                case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineVar:size=1;break;
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
