using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using CrusaderDE;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace BugfixesAndQoL.SavegameMissingMap
{
    internal static class MakerTests
    {
        private static readonly List<FileHeader> files = new List<FileHeader>();
        private static readonly List<Noesis.ImageSource> sprites = new List<Noesis.ImageSource>();
        private static Noesis.ImageSource marker;
        private static bool[] marked;
        private static int buttonIndex, selectedIndex;
        private static string label;
        private static Action<FRONT_ManageTrail, int> select;
        private static void Require(bool value, string reason) { if (!value) throw new Exception(reason); }
        private static MapFileManager Manager() => null;
        private static MainViewModel MainModel() => null;
        private static Translate Translator() => null;
        private static string Text(Translate self, Enums.eTextSections section, int value) => "text";
        private static string TextEnum(Translate self, Enums.eTextSections section, Enums.eTextValues value) => "text";
        private static int Count(MapFileManager self) => 1;
        private static List<FileHeader> Files(MapFileManager self, int sort, bool ascending) => files;
        private static List<Noesis.ImageSource> Sprites(MainViewModel self) => sprites;
        private static Noesis.ImageSource Image(List<Noesis.ImageSource> self, int index) => marker;
        private static FileHeader Header(MapFileManager self, string name) => files.FirstOrDefault(item => item.fileName == name);
        private static FileHeader Detailed(MapFileManager self, string path, string realPath, int folder, bool restart) => files.First(item => item.filePath == path);
        private static Noesis.RadioButton Button(FRONT_ManageTrail self, int index) { buttonIndex = index; return null; }
        private static bool? Checked(object self) => buttonIndex == selectedIndex;
        private static bool Different(object left, object right) => !ReferenceEquals(left, right);
        private static void Bool(object self, bool value) { }
        private static void Float(object self, float value) { }
        private static void String(object self, string value) { }
        private static void Label(object self, string value) { label = value; }
        private static void Sprite(object self, object value) { marked[buttonIndex] = value != null; }
        private static void Select(FRONT_ManageTrail self, int index) => select(self, index);
        private static void Buttons(FRONT_ManageTrail self) { }

        // Execute the installed Init/SelectMission bodies. Only catalog, localization and
        // native Noesis presentation boundaries are substituted; the loops/state and fix run.
        private static void Isolate(ILContext il)
        {
            foreach (var i in il.Body.Instructions)
            {
                string replacement = null;
                if (i.OpCode == OpCodes.Ldfld && i.Operand is FieldReference f && f.Name == "GameSprites") replacement = nameof(Sprites);
                if (i.Operand is MethodReference m)
                {
                    string type = m.DeclaringType.FullName;
                    if (m.Name == "get_Item" && m.DeclaringType is GenericInstanceType generic &&
                        generic.ElementType.FullName == "System.Collections.Generic.List`1" &&
                        generic.GenericArguments.Single().FullName == "Noesis.ImageSource") replacement = nameof(Image);
                    if (type == "MapFileManager")
                        replacement = m.Name == "get_Instance" ? nameof(Manager) :
                            m.Name == "GetCustomTrailsCount" ? nameof(Count) :
                            m.Name == "GetTrailMakerFiles" ? nameof(Files) :
                            m.Name == "GetHeaderFromTrailMaker" ? nameof(Header) :
                            m.Name == "GetFileInfoFromFileName" ? nameof(Detailed) : null;
                    if (type == "CrusaderDE.MainViewModel")
                        replacement = m.Name == "get_Instance" ? nameof(MainModel) :
                            m.Name == "set_TM_SelectedMap" ? nameof(Label) :
                            m.Name.StartsWith("set_TM_") ? nameof(String) : m.Name.StartsWith("set_Show_") ? nameof(Bool) : null;
                    if (type == "CrusaderDE.FRONT_ManageTrail")
                        replacement = m.Name == "GetButton" ? nameof(Button) : m.Name == "SelectMission" ? nameof(Select) :
                            m.Name == "UpdateMainButtons" ? nameof(Buttons) : null;
                    if (type == "CrusaderDE.PropEx" && m.Name == "SetSprite1") replacement = nameof(Sprite);
                    if (type == "Noesis.UIElement") replacement = m.Name == "set_IsEnabled" ? nameof(Bool) : m.Name == "set_Opacity" ? nameof(Float) : null;
                    if (type == "Noesis.ToggleButton" && m.Name == "get_IsChecked") replacement = nameof(Checked);
                    if (type == "Noesis.BaseComponent" && m.Name == "op_Inequality") replacement = nameof(Different);
                    if (type == "CrusaderDE.Translate") replacement = m.Name == "get_Instance" ? nameof(Translator) :
                        m.Name == "lookUpText" ? (m.Parameters[1].ParameterType.FullName == "System.Int32" ? nameof(Text) : nameof(TextEnum)) : null;
                }
                if (replacement == null) continue;
                i.OpCode = OpCodes.Call;
                i.Operand = il.Import(typeof(MakerTests).GetMethod(replacement, BindingFlags.NonPublic | BindingFlags.Static));
            }
        }

        internal static void Run(byte[] payload)
        {
            SavegameFix.Enabled = true;
            SavegameFix.RecoveryEnabled = () => true;
            marker = (Noesis.TextureSource)FormatterServices.GetUninitializedObject(typeof(Noesis.TextureSource));
            // Reproduce the installed page's own state allocation without invoking Noesis.
            // Neither the sprite index nor the mission capacity belongs to this fixture.
            using (var constructor = new DynamicMethodDefinition(typeof(FRONT_ManageTrail).GetConstructor(Type.EmptyTypes)))
            {
                var store = constructor.Definition.Body.Instructions.Single(i => i.OpCode == OpCodes.Stfld &&
                    i.Operand is FieldReference field && field.Name == "exists");
                Require(store.Previous.OpCode == OpCodes.Newarr, "Maker state is no longer an array allocation.");
                var size = store.Previous.Previous;
                Require(size.OpCode == OpCodes.Ldc_I4 || size.OpCode == OpCodes.Ldc_I4_S,
                    "Maker capacity is no longer a direct integer allocation; extend the state adapter.");
                marked = new bool[Convert.ToInt32(size.Operand)];
            }
            using (var selection = new DynamicMethodDefinition(SavegameFix.MakerSelection))
            using (var init = new DynamicMethodDefinition(typeof(FRONT_ManageTrail).GetMethod("Init", BindingFlags.Instance | BindingFlags.NonPublic)))
            {
                using (var il = new ILContext(selection.Definition)) { il.Invoke(SavegameFix.ModifyMakerSelection); Isolate(il); }
                using (var il = new ILContext(init.Definition)) Isolate(il);
                select = (Action<FRONT_ManageTrail, int>)selection.Generate().CreateDelegate(typeof(Action<FRONT_ManageTrail, int>));
                var initialize = (Action<FRONT_ManageTrail, bool>)init.Generate().CreateDelegate(typeof(Action<FRONT_ManageTrail, bool>));
                var page = (FRONT_ManageTrail)FormatterServices.GetUninitializedObject(typeof(FRONT_ManageTrail));
                typeof(FRONT_ManageTrail).GetField("exists", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(page, new bool[marked.Length]);
                foreach (int selected in new[] { 0, 4 })
                {
                    selectedIndex = selected;
                    foreach (int count in new[] { 5, 2, 5, 0, 2 })
                    {
                        files.Clear();
                        for (int index = 0; index < count; index++)
                        {
                            var restart = new HUD_IngameMenu.RestartSkirmishMapInfo();
                            restart.lordTypes.Add(-1); // No AI rows are needed to test map presentation.
                            SavegameFix.Remember(restart, payload);
                            files.Add(new FileHeader { fileName = FRONT_ManageTrail.GetMakerFileName(index), filePath = "mission" + index,
                                hasRestartSkirmishInfo = true, restartSkirmishInfo = restart });
                        }
                        initialize(page, false);
                        Require(marked.Take(count).All(value => value) && marked.Skip(count).All(value => !value),
                            "Installed Maker Init retained stale mission markers after count=" + count + ", selected=" + selected);
                        if (selected < count) Require(!string.IsNullOrEmpty(label), "Recovered map identity was not displayed.");
                    }
                }
                var first = files[0].restartSkirmishInfo;
                first.selectedHeader = new FileHeader { display_filename = "Installed map" };
                SavegameFix.Enabled = false;
                selectedIndex = 0;
                initialize(page, false);
                Require(label == "Installed map", "Disabled fix changed installed-map presentation.");
                SavegameFix.Enabled = true;
                first.selectedHeader = null;
                initialize(page, false);
                Require(!string.IsNullOrEmpty(label) && first.selectedHeader == null, "Presentation fabricated a loadable header.");
            }
            using (var copy = new DynamicMethodDefinition(SavegameFix.MakerSelection))
            using (var il = new ILContext(copy.Definition))
            {
                var field = il.Body.Instructions.First(i => i.Operand is FieldReference f && f.Name == "display_filename");
                ((FieldReference)field.Operand).Name = "changed";
                il.Invoke(context => {
                    string before = RestartTests.Snapshot(il);
                    bool rejected = false;
                    try { SavegameFix.ModifyMakerSelection(il); } catch (InvalidOperationException) { rejected = true; }
                    Require(rejected && before == RestartTests.Snapshot(il), "Changed Maker IL contract was not rejected before mutation.");
                });
            }
            Console.WriteLine("PASS: installed Maker Init/SelectMission: selected slots 1/5, 5 -> 2 -> 5, clear/reopen, retained map labels, inactive fix and changed IL.");
        }
    }
}
