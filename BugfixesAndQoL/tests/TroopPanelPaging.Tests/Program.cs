using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using APIShared.Patching;
using CrusaderDE;
using HarmonyLib;

namespace BugfixesAndQoL
{
    internal static class Program
    {
        private static int checks;
        private static int foreignCalls;
        private static int[] selection;
        private static int firstDrawn;
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
        private static Type Fix => typeof(TroopPanelPageCountFix);

        private static int Main()
        {
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            try { Run(); Console.WriteLine("PASS: troop-panel paging (" + checks + " checks; actual HUD IL and Harmony rebuild)."); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name + ".dll";
            string game = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
            foreach (string directory in new[] { Path.Combine(game, "BepInEx", "core"), Path.Combine(game, "BepInEx", "plugins", "000shcdese"), Path.Combine(game, "Stronghold Crusader Definitive Edition_Data", "Managed") })
            {
                string candidate = Path.Combine(directory, name);
                if (File.Exists(candidate)) return Assembly.LoadFrom(candidate);
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Run()
        {
            List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(typeof(HUD_Troops), "SetupSelectedTroops")).ToList();
            Set("enabled", 1);
            List<CodeInstruction> edited = ((IEnumerable<CodeInstruction>)Invoke("TranspilePageMarkers", original)).ToList();
            Check(edited.Count == original.Count, "only operands are replaced");
            MethodInfo third = Fix.GetMethod("ThirdPageStart", PrivateStatic), fourth = Fix.GetMethod("FourthPageStart", PrivateStatic);
            Check(edited.Count(x => x.Calls(third)) == 1 && edited.Count(x => x.Calls(fourth)) == 1, "actual HUD contains exactly the two corrected page markers");
            Check(!original.Any(x => x.Calls(third) || x.Calls(fourth)), "actual original instruction objects remain untouched");
            for (int i = 0; i < original.Count; i++)
            {
                if (edited[i].Calls(third)) Check(original[i].LoadsConstant(18), "third-page call replaces Vanilla 18");
                else if (edited[i].Calls(fourth)) Check(original[i].LoadsConstant(27), "fourth-page call replaces Vanilla 27");
                else Check(Same(original[i], edited[i]), "all unrelated HUD IL and metadata are preserved");
            }
            int marker = edited.FindIndex(x => x.Calls(third));
            RejectMalformed(original, "missing marker", list => list.RemoveAt(marker));
            RejectMalformed(original, "changed marker", list => list[marker].operand = (sbyte)19);
            RejectMalformed(original, "changed branch", list => list[marker + 1].opcode = OpCodes.Beq);
            RejectMalformed(original, "changed store context", list => {
                int store = list.FindIndex(marker, x => x.opcode == OpCodes.Stfld && ((FieldInfo)x.operand).Name == "pages");
                list[store].operand = AccessTools.DeclaredField(typeof(HUD_Troops), "currentPage");
            });
            RejectMalformed(original, "duplicate marker context", list => {
                int store = list.FindIndex(marker, x => x.opcode == OpCodes.Stfld && ((FieldInfo)x.operand).Name == "pages");
                list.AddRange(list.Skip(marker - 1).Take(store - marker + 2).Select(x => new CodeInstruction(x)).ToArray());
            });
            RejectMalformed(original, "different counter local", list => list[marker - 1] = new CodeInstruction(OpCodes.Ldloc, 200));
            Set("enabled", 1);
            Check((int)Invoke("ThirdPageStart") == 17 && (int)Invoke("FourthPageStart") == 25, "enabled markers cover every eight-slot page");
            Set("enabled", 0);
            Check((int)Invoke("ThirdPageStart") == 18 && (int)Invoke("FourthPageStart") == 27, "disabled markers preserve Vanilla");
            TestPrefixAndFailureState();
            TestHarmonyRebuild();
            TestActualInstallation();
        }

        private static void RejectMalformed(List<CodeInstruction> original, string name, Action<List<CodeInstruction>> mutate)
        {
            List<CodeInstruction> input = original.Select(x => new CodeInstruction(x)).ToList();
            mutate(input);
            List<CodeInstruction> snapshot = input.Select(x => new CodeInstruction(x)).ToList();
            bool? outcome = null;
            List<CodeInstruction> result = SafeTranspiler.Run("TroopPanelPaging.Tests", name, input,
                instructions => Transform(instructions), null, success => outcome = success).ToList();
            Check(outcome == false, name + " fails closed through actual SafeTranspiler");
            Check(result.Count == snapshot.Count && result.Zip(snapshot, Same).All(x => x), name + " preserves fallback IL");
            Check(input.Zip(snapshot, Same).All(x => x), name + " does not mutate input");
        }

        private static void TestPrefixAndFailureState()
        {
            // An uninitialized managed HUD tests its integer field only: no Noesis constructor/native UI.
            var hud = (HUD_Troops)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(HUD_Troops));
            GC.SuppressFinalize(hud);
            FieldInfo pages = AccessTools.DeclaredField(typeof(HUD_Troops), "pages");
            Set("pagesField", AccessTools.FieldRefAccess<HUD_Troops, int>(pages));
            Set("settings", new BugfixesAndQoLViewModel());
            Set("installed", 1); Set("transpilerReady", 1); Set("faulted", 0);
            Invoke("RefreshEnabled");
            pages.SetValue(hud, 4); Invoke("BeforeSetupSelectedTroops", hud);
            Check((int)pages.GetValue(hud) == 1, "enabled prefix clears stale count");
            Set("enabled", 0); pages.SetValue(hud, 4); Invoke("BeforeSetupSelectedTroops", hud);
            Check((int)pages.GetValue(hud) == 4, "disabled prefix preserves count");
            Invoke("OnTranspilerOutcome", false); Invoke("OnTranspilerOutcome", true); Invoke("RefreshEnabled");
            Check((int)Get("enabled") == 0 && (int)Get("faulted") != 0, "failed rebuild remains disabled after success and settings refresh");
            pages.SetValue(hud, 4); Invoke("BeforeSetupSelectedTroops", hud);
            Check((int)pages.GetValue(hud) == 4, "failed rebuild cannot leave count-only patch active");
        }

        private static void TestHarmonyRebuild()
        {
            // This private fixture is never shared with game mods and exits with this process.
            MethodInfo target = Method("Fixture");
            MethodInfo productionTranspiler = Fix.GetMethods(PrivateStatic).Single(x => x.Name != "TransformPageMarkers" &&
                typeof(IEnumerable<CodeInstruction>).IsAssignableFrom(x.ReturnType) &&
                x.GetParameters().Any(p => p.ParameterType == typeof(IEnumerable<CodeInstruction>)));
            var first = new Harmony("TroopPanelPaging.Tests.production");
            first.Patch(target, transpiler: new HarmonyMethod(productionTranspiler));
            var foreign = new Harmony("TroopPanelPaging.Tests.foreign");
            foreign.Patch(target, prefix: new HarmonyMethod(typeof(Program), nameof(ForeignPrefix)), postfix: new HarmonyMethod(typeof(Program), nameof(ForeignPostfix)));
            Check(Fixture() == 13 && foreignCalls == 1, "foreign prefix and postfix survive rejected production transpiler during rebuild");
            var another = new Harmony("TroopPanelPaging.Tests.rebuild");
            another.Patch(target, prefix: new HarmonyMethod(typeof(Program), nameof(OtherPrefix)));
            Check(Fixture() == 13 && foreignCalls == 2, "later rebuild retains all preceding foreign patches");
        }

        private static void TestActualInstallation()
        {
            Set("attempted", 0); Set("installed", 0); Set("transpilerReady", 0); Set("faulted", 0); Set("enabled", 0);
            var settings = new BugfixesAndQoLViewModel();
            TroopPanelPageCountFix.Install(null, settings);
            Check((int)Get("installed") == 1 && (int)Get("transpilerReady") == 1 && (int)Get("faulted") == 0 &&
                (int)Get("enabled") == 1, "real HUD method accepts production preflight and permanent Harmony installation");
            settings.EnableTroopPanelPageCountFix = false;
            settings.Notify(nameof(settings.EnableTroopPanelPageCountFix));
            Check((int)Get("enabled") == 0, "real settings event disables both paging edits");
            settings.EnableTroopPanelPageCountFix = true;
            settings.Notify(nameof(settings.EnableTroopPanelPageCountFix));
            Check((int)Get("enabled") == 1, "valid installed patch can be reenabled logically");
            TestActualPageControlFlow(settings);
        }

        private static void TestActualPageControlFlow(BugfixesAndQoLViewModel settings)
        {
            var boundary = new Harmony("TroopPanelPaging.Tests.gui-boundaries");
            foreach (string method in new[] { "HideAllSelectedTroops", "HideAllSelectedTroopsNumbers" })
                boundary.Patch(AccessTools.DeclaredMethod(typeof(HUD_Troops), method), prefix: new HarmonyMethod(typeof(Program), nameof(SkipGui)));
            boundary.Patch(AccessTools.DeclaredMethod(typeof(HUD_Troops), "CountSelectedChimpTypes"), prefix: new HarmonyMethod(typeof(Program), nameof(ProvideSelection)));
            boundary.Patch(AccessTools.DeclaredMethod(typeof(HUD_Troops), "SetSelectedTroopVisible"), prefix: new HarmonyMethod(typeof(Program), nameof(RecordVisibleType)));
            boundary.Patch(AccessTools.DeclaredMethod(typeof(HUD_Troops), "SetupSelectedTroops"), transpiler: new HarmonyMethod(typeof(Program), nameof(ReplaceButtonDrawing)));
            var hud = (HUD_Troops)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(HUD_Troops));
            GC.SuppressFinalize(hud);
            FieldInfo pages = AccessTools.DeclaredField(typeof(HUD_Troops), "pages");
            FieldInfo page = AccessTools.DeclaredField(typeof(HUD_Troops), "currentPage");
            AccessTools.DeclaredField(typeof(HUD_Troops), "SelTroopPositions").SetValue(hud, new Noesis.TranslateTransform[8]);
            foreach (bool sparse in new[] { false, true })
            for (int count = 0; count <= 32; count++)
            foreach (bool enabled in new[] { false, true })
            {
                int[] ids = Enumerable.Range(1, 88).Where(x => x != 55 && (!sparse || x % 2 == 1)).Take(count).ToArray();
                selection = new int[89]; foreach (int id in ids) selection[id] = 1;
                settings.EnableTroopPanelPageCountFix = enabled;
                settings.Notify(nameof(settings.EnableTroopPanelPageCountFix));
                int expectedPages = enabled ? Math.Max(1, (count + 7) / 8) : count >= 27 ? 4 : count >= 18 ? 3 : count >= 9 ? 2 : 1;
                for (int requested = 0; requested <= 4; requested++)
                {
                    pages.SetValue(hud, enabled ? 4 : 1); page.SetValue(hud, requested); firstDrawn = -1;
                    hud.SetupSelectedTroops();
                    int effectivePage = Math.Min(requested, expectedPages - 1);
                    int start = effectivePage == 0 ? 0 : enabled ? effectivePage * 8 : effectivePage == 1 ? 8 : effectivePage == 2 ? 17 : 26;
                    Check((int)pages.GetValue(hud) == expectedPages && (int)page.GetValue(hud) == effectivePage,
                        "actual HUD count/clamp: count=" + count + ", sparse=" + sparse + ", enabled=" + enabled + ", requested=" + requested);
                    Check(firstDrawn == (count == 0 ? -1 : ids[start]), "actual HUD starts each page at its selected type");
                }
            }
            // Rendering ends deliberately at the first null transform after recording its type. The real
            // method catches that managed exception; this covers its count/clamp/start IL, not drawing.
        }

        private static bool SkipGui() => false;
        private static bool ProvideSelection(HUD_Troops __instance)
        {
            AccessTools.DeclaredField(typeof(HUD_Troops), "SelectedChimpArray").SetValue(__instance, (int[])selection.Clone());
            return false;
        }
        private static bool RecordVisibleType(int type, ref int __result) { firstDrawn = type; __result = -13; return false; }
        private static void IgnoreButtonVisibility(Noesis.UIElement element, Noesis.Visibility value) { }
        private static IEnumerable<CodeInstruction> ReplaceButtonDrawing(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.operand is MethodInfo method && method.DeclaringType == typeof(PropEx) && method.Name == "SetButtonVisibility")
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = Method(nameof(IgnoreButtonVisibility));
                }
                yield return instruction;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)] private static int Fixture() => 6;
        private static void ForeignPrefix() { foreignCalls++; }
        private static void OtherPrefix() { }
        private static void ForeignPostfix(ref int __result) { __result += 7; }
        private static List<CodeInstruction> Transform(IEnumerable<CodeInstruction> source) => ((IEnumerable<CodeInstruction>)Invoke("TransformPageMarkers", source)).ToList();
        private static bool Same(CodeInstruction a, CodeInstruction b) => a.opcode == b.opcode &&
            (Equals(a.operand, b.operand) || a.operand is Label[] left && b.operand is Label[] right && left.SequenceEqual(right)) &&
            a.labels.SequenceEqual(b.labels) && a.blocks.Count == b.blocks.Count &&
            a.blocks.Zip(b.blocks, (x, y) => x == null ? y == null : y != null && x.blockType == y.blockType && x.catchType == y.catchType).All(x => x);
        private static MethodInfo Method(string name) => typeof(Program).GetMethod(name, PrivateStatic);
        private static object Invoke(string name, params object[] arguments)
        {
            try { return Fix.GetMethod(name, PrivateStatic).Invoke(null, arguments); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }
        private static void Set(string name, object value) => Fix.GetField(name, PrivateStatic).SetValue(null, value);
        private static object Get(string name) => Fix.GetField(name, PrivateStatic).GetValue(null);
        private static void Check(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
    }
}
