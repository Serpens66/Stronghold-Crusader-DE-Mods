using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using CrusaderDE;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace SavegameMissingMapFixTest
{
    internal static class RestartTests
    {
        private static readonly List<ILHook> roots = new List<ILHook>();
        private static int gameType, trailType, restarted, campaign, trail, customTrail, stopped, innerCalls;
        private static bool skirmish = true;
        private static void Require(bool condition, string reason) { if (!condition) throw new Exception(reason); }
        [MethodImpl(MethodImplOptions.NoInlining)] private static Action Outer() => Inner;
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Inner() { innerCalls++; }
        private static MethodInfo Method(string name) => typeof(RestartTests).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        private static GameData Data() => null;
        private static int GameType(GameData _) => gameType;
        private static int TrailType(GameData _) => trailType;
        private static int Level(GameData _) => 1;
        private static MainViewModel MainValue() => null;
        private static Director DirectorValue() => null;
        private static EditorDirector Editor() => null;
        private static bool Skirmish(Director _) => skirmish;
        private static FRONT_Multiplayer Front(MainViewModel _) => null;
        private static FrontendMenus FrontEnd(MainViewModel _) => null;
        private static void Stop(EditorDirector _, bool ignored) { stopped++; }
        private static void Campaign(MainViewModel _, int level) { campaign++; }
        private static void Trail(FrontendMenus _, int level, int type) { trail++; }
        private static void CustomTrail(MainViewModel _, string name, int level, int difficulty) { customTrail++; }
        private static void Restart(FRONT_Multiplayer _, HUD_IngameMenu.RestartSkirmishMapInfo info) { restarted++; }

        private static void Isolate(ILContext il)
        {
            var calls = new Dictionary<string, string>
            {
                ["GameData::get_Instance"] = nameof(Data), ["GameData::get_game_type"] = nameof(GameType),
                ["GameData::get_SkirmishTrailType"] = nameof(TrailType), ["GameData::get_SkirmishTrailLevel"] = nameof(Level),
                ["GameData::get_mission_level"] = nameof(Level), ["Director::get_SkirmishModeGame"] = nameof(Skirmish),
                ["CrusaderDE.MainViewModel::get_Instance"] = nameof(MainValue), ["EditorDirector::stopGameSim"] = nameof(Stop),
                ["CrusaderDE.MainViewModel::StartCampaignMission"] = nameof(Campaign),
                ["CrusaderDE.FrontendMenus::StartTrailMission"] = nameof(Trail),
                ["CrusaderDE.MainViewModel::StartCustomTrailMission"] = nameof(CustomTrail),
                ["CrusaderDE.FRONT_Multiplayer::RestartSkirmishGame"] = nameof(Restart)
            };
            foreach (var i in il.Body.Instructions)
            {
                string replacement = null;
                if (i.Operand is MethodReference m) calls.TryGetValue(m.DeclaringType.FullName + "::" + m.Name, out replacement);
                if (i.Operand is FieldReference f)
                {
                    if (f.DeclaringType.FullName == "Director" && f.Name == "instance") replacement = nameof(DirectorValue);
                    if (f.DeclaringType.FullName == "EditorDirector" && f.Name == "instance") replacement = nameof(Editor);
                    if (f.DeclaringType.FullName == "CrusaderDE.MainViewModel" && f.Name == "FRONTMultiplayer") replacement = nameof(Front);
                    if (f.DeclaringType.FullName == "CrusaderDE.MainViewModel" && f.Name == "FrontEndMenu") replacement = nameof(FrontEnd);
                }
                if (replacement == null) continue;
                i.OpCode = OpCodes.Call; i.Operand = il.Import(Method(replacement));
            }
        }
        internal static void Run(byte[] payload)
        {
            SavegameFix.ValidateMembers();
            CheckInvocationContracts();
            var targets = SavegameFix.HookTargets;
            Require(targets.Length == 4 && targets.Distinct().Count() == 4, "Unexpected hook target list.");
            Require(targets.All(m => m.Name != "ButtonIngameMenuFunction" && m.Name != "RestartSkirmishGame"), "Conflicting owner hooked.");
            Require(targets[3] == SavegameFix.RestartConfirmation && targets[3].DeclaringType == typeof(HUD_IngameMenu), "Exit callback substituted for restart.");
            int ownerRuns = 0;
            roots.Add(new ILHook(Method(nameof(Outer)), il => { ownerRuns++; new ILCursor(il).Emit(OpCodes.Nop); }));
            int before = ownerRuns;
            Require(before > 0, "Outer manipulator did not run.");
            roots.Add(new ILHook(Method(nameof(Inner)), il => new ILCursor(il).Emit(OpCodes.Nop)));
            Require(ownerRuns == before, "Callback hook reran the outer manipulator.");
            Outer()(); Require(innerCalls == 1, "Nested hooked callback did not run exactly once.");
            // Positive control: the harness detects the rebuild caused by a second owner hook.
            roots.Add(new ILHook(Method(nameof(Outer)), il => new ILCursor(il).Emit(OpCodes.Nop)));
            Require(ownerRuns > before, "Harness failed to detect owner rebuild.");
            Console.WriteLine("PASS: installed MonoMod isolated-callback hook does not rerun owner IL manipulator; positive control detects owner rehook.");

            using (var copy = new DynamicMethodDefinition(SavegameFix.RestartConfirmation))
            {
                using (var il = new ILContext(copy.Definition)) { il.Invoke(SavegameFix.ModifyRestartConfirmation); Isolate(il); }
                var run = (Action<HUD_IngameMenu>)copy.Generate().CreateDelegate(typeof(Action<HUD_IngameMenu>));
                var menu = (HUD_IngameMenu)FormatterServices.GetUninitializedObject(typeof(HUD_IngameMenu));
                var missing = (HUD_IngameMenu.RestartSkirmishMapInfo)FormatterServices.GetUninitializedObject(typeof(HUD_IngameMenu.RestartSkirmishMapInfo));
                SavegameFix.Enabled = true; SavegameFix.Remember(missing, payload);
                menu.restartSkirmishMapInfo = missing; gameType = 3; trailType = -1;
                run(menu); Require(restarted == 0 && stopped == 0, "Missing map restart was not blocked before side effects.");
                SavegameFix.Enabled = false;
                run(menu); Require(restarted == 1, "Disabled fix changed Vanilla restart path.");
                SavegameFix.Enabled = true;
                missing.selectedHeader = new FileHeader();
                run(menu); Require(restarted == 2, "Installed map did not restart exactly once.");
                missing.customTrail = true;
                run(menu); Require(customTrail == 1 && restarted == 2, "Available custom trail changed.");
                missing.selectedHeader = null;
                run(menu); Require(customTrail == 1, "Missing custom trail was not blocked.");
                gameType = 0;
                run(menu); Require(campaign == 1 && stopped == 1, "Campaign branch changed.");
                gameType = 3; trailType = 0;
                run(menu); Require(trail == 1 && stopped == 2, "Built-in trail branch changed.");
                skirmish = false;
                run(menu); Require(trail == 1 && stopped == 2, "Non-skirmish branch changed.");
            }
            using (var copy = new DynamicMethodDefinition(SavegameFix.RestartConfirmation))
            using (var il = new ILContext(copy.Definition))
            {
                var call = il.Body.Instructions.First(i => i.Operand is MethodReference m && m.Name == "RestartSkirmishGame");
                ((MethodReference)call.Operand).Name = "ChangedRestart";
                il.Invoke(context => {
                string beforeIL = Snapshot(il);
                bool rejected = false;
                try { SavegameFix.ModifyRestartConfirmation(il); } catch (InvalidOperationException) { rejected = true; }
                Require(rejected && beforeIL == Snapshot(il), "Changed callback was not rejected before mutation.");
                });
            }
            Console.WriteLine("PASS: actual restart callback executed: missing/installed/disabled, campaign, built-in/custom trails; changed contract rejected.");
        }

        internal static string Snapshot(ILContext il) => string.Join("\n", il.Body.Instructions.Select(i => i.OpCode.Code + "|" + Operand(il, i.Operand)));
        private static string Operand(ILContext il, object operand)
        {
            if (operand is ILLabel label) return "label:" + Operand(il, label.Target);
            if (operand is Instruction target) return "instruction:" + il.Body.Instructions.IndexOf(target) + ":" + RuntimeHelpers.GetHashCode(target);
            if (operand is MemberReference member) return member.FullName;
            return operand == null ? "null" : operand.ToString();
        }
        private static ILLabel Label(ILContext il, Instruction target) { var label = new ILCursor(il).DefineLabel(); label.Target = target; return label; }
        private static void CheckInvocationContracts()
        {
            foreach (bool longBranch in new[] { false, true })
            using (var copy = new DynamicMethodDefinition(SavegameFix.RestartConfirmation))
            using (var il = new ILContext(copy.Definition))
            {
                var branch = il.Body.Instructions.Single(i => i.OpCode == OpCodes.Blt_S);
                var original = branch.Operand as Instruction;
                Require(SavegameFix.ResolveBranchTarget(il, original) == original, "Direct instruction target rejected.");
                if (longBranch) branch.OpCode = OpCodes.Blt;
                il.Invoke(context =>
                {
                    Require(branch.Operand is ILLabel, "Regression must exercise MonoMod's label conversion.");
                    Require(SavegameFix.ResolveBranchTarget(context, branch.Operand) == original, "Label target changed.");
                    SavegameFix.ModifyRestartConfirmation(context);
                    Require(SavegameFix.ResolveBranchTarget(context, branch.Operand) == original, "Guard moved existing branch target.");
                });
                Require(ReferenceEquals(branch.Operand, original), "Invoke failed to restore branch instruction.");
                GC.KeepAlive(copy.Generate());
            }
            foreach (int variant in new[] { 0, 1, 2, 3 })
            using (var copy = new DynamicMethodDefinition(SavegameFix.RestartConfirmation))
            using (var il = new ILContext(copy.Definition))
                il.Invoke(context =>
                {
                    var branch = il.Body.Instructions.Single(i => i.OpCode == OpCodes.Blt_S);
                    var foreign = Instruction.Create(OpCodes.Nop);
                    branch.Operand = variant == 0 ? null : variant == 1 ? (object)"invalid" :
                        variant == 2 ? foreign : (object)Label(il, foreign);
                    string before = Snapshot(il);
                    bool rejected = false;
                    try { SavegameFix.ModifyRestartConfirmation(context); }
                    catch (InvalidOperationException ex) { rejected = ex.Message.Contains("branch target"); }
                    Require(rejected && before == Snapshot(il), "Invalid target was not rejected without mutation.");
                    // Restore a valid operand for Invoke's own postprocessing.
                    branch.Operand = Label(il, il.Body.Instructions.Last());
                });
            int published = 0, prepared = 0;
            var transforms = SavegameFix.HookTransforms;
            transforms[3] = il =>
            {
                var call = il.Body.Instructions.First(i => i.Operand is MethodReference m && m.Name == "RestartSkirmishGame");
                ((MethodReference)call.Operand).Name = "ChangedRestart";
                SavegameFix.ModifyRestartConfirmation(il);
            };
            bool failed = false;
            try { SavegameFix.RunHookPlan(SavegameFix.HookTargets, transforms, () => prepared++, (method, transform) => published++); }
            catch (InvalidOperationException ex) { failed = ex.Message.Contains("phase=preflight") && ex.Message.Contains(SavegameFix.RestartConfirmation.Name); }
            Require(failed && prepared == 0 && published == 0, "Failed fourth preflight reached registration or hook publication.");
            SavegameFix.RunHookPlan(SavegameFix.HookTargets, SavegameFix.HookTransforms, () => prepared++, (method, transform) => published++);
            Require(prepared == 1 && published == 4, "Valid hook plan did not publish all four targets.");
            Console.WriteLine("PASS: Invoke short/long labels, preserved targets, invalid/external operands, all-four preflight before publication.");
        }
    }
}
