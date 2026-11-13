using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using CrusaderDE;
using HarmonyLib;
using SHCDESE.API;
using SHCDESE.Interop.Enums;

namespace UiAcceptance
{
    // Commands follow the same public UI paths as SpectatorSteps. Reflection only observes
    // the mod's owned selection; this test never writes its private state or native memory.
    internal static class SpectatorPowerRegressionSteps
    {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static Type runtime;
        private static FieldInfo pending, generation, context, owner, buttons;
        private static string executedText;
        private static bool inputPatched;
        private static PowerGesture pendingGesture, activeGesture;
        private static readonly Dictionary<string, Mark> marks = new Dictionary<string, Mark>(StringComparer.Ordinal);

        private sealed class PowerGesture
        {
            internal int X, Y, LeftState;
            internal bool RightDown, RightUp;
            internal int Consumed;
        }

        private sealed class Mark
        {
            internal long Generation, PendingGeneration, Session, Log;
            internal int Actor, GoldBefore;
            internal ExtremePower Power;
            internal object Selection;
        }

        private sealed class State
        {
            internal object Selection;
            internal long Generation, SelectionGeneration, Session;
            internal int Actor, Power, Mode, NativePower, Owner;
        }

        internal static void Register()
        {
            Runner.Register("Spectator.PowerCheck.Begin", Begin);
            Runner.Register("Spectator.PowerCheck.Select", Select);
            Runner.Register("Spectator.PowerCheck.Pending", Pending);
            Runner.Register("Spectator.PowerCheck.Confirm", Confirm);
            Runner.Register("Spectator.PowerCheck.Capture", Capture);
            Runner.Register("Spectator.PowerCheck.Cancelled", Cancelled);
            Runner.Register("Spectator.PowerCheck.Executed", Executed);
            Runner.Register("Spectator.PowerCheck.ReplacedMission", ReplacedMission);
        }

        private static IEnumerable<Wait> Begin(Step step)
        {
            foreach (var plugin in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
                if (plugin.Metadata.GUID == "Spectator_Serp")
                    runtime = plugin.Instance.GetType().Assembly.GetType("Spectator.SpectatorRuntime", true);
            Runner.Require(runtime != null, "Spectator runtime not installed");
            pending = RequiredStatic("pendingPower");
            generation = RequiredStatic("powerGeneration");
            context = RequiredStatic("powerContext");
            owner = RequiredStatic("humanSelectedOwner");
            buttons = RequiredStatic("vanillaPowerButtons");
            Runner.Require(generation.FieldType == typeof(long) && owner.FieldType == typeof(int), "power state field contract changed");
            var localizer = runtime.Assembly.GetType("SerpLocalization", true);
            var getText = localizer.GetMethod("Get", Static, null, new[] { typeof(string) }, null);
            Runner.Require(getText != null && getText.ReturnType == typeof(string), "Spectator locale reader unavailable");
            executedText = (string)getText.Invoke(null, new object[] { "Tools.PowerExecuted" });
            Runner.Require(!string.IsNullOrEmpty(executedText) && executedText != "Tools.PowerExecuted", "power execution locale missing");
            marks.Clear();
            yield return new Until("initialized native powers and available test tools", () => {
                var model = SpectatorSteps.Model();
                object native = context.GetValue(null);
                return MainViewModel.viewModelLoaded && UiAcceptancePlugin.StartedSession > 0 &&
                    model != null && Convert.ToBoolean(Member(model, "ToolsAvailable"), CultureInfo.InvariantCulture) &&
                    Convert.ToBoolean(Member(model, "PowersEnabled"), CultureInfo.InvariantCulture) && native != null &&
                    Convert.ToBoolean(Member(native, "Ready"), CultureInfo.InvariantCulture) && PowerButtonsReady();
            });
            State state = Read();
            Runner.Require(state.Selection == null && !Targeting(state.Mode), "fixture already has an active power selection");
            AcceptLog.Info("POWER_REGRESSION_READY session=" + UiAcceptancePlugin.StartedSession + " generation=" + state.Generation);
        }

        // args: mark, Extender ExtremePower enum name, actor. No copied numeric power IDs.
        private static IEnumerable<Wait> Select(Step step)
        {
            var power = (ExtremePower)Enum.Parse(typeof(ExtremePower), step.Arg(1), false);
            Runner.Require(Enum.IsDefined(typeof(ExtremePower), power), "unknown power enum " + step.Arg(1));
            int actor = step.Int(2);
            State before = Read();
            Runner.Require(before.Selection == null && !Targeting(before.Mode), "previous selection not retired");
            Runner.Require(before.Owner == actor, "toolbar recipient differs from expected actor " + actor);
            object descriptor = Descriptor(power);
            int targetKind = Convert.ToInt32(Member(descriptor, "TargetKind"), CultureInfo.InvariantCulture);
            if (power == ExtremePower.GrantGold)
                Runner.Require(targetKind == 0 && Member(descriptor, "Replacement") == null,
                    "immediate native Gold fixture requires the original non-targeted Gold power");
            else Runner.Require(targetKind == 1, "targeted fixture power changed its contract");
            marks[step.Arg(0)] = new Mark {
                Generation = before.Generation, Session = UiAcceptancePlugin.StartedSession,
                Log = AcceptLog.Position, Actor = actor, Power = power,
                GoldBefore = GamePlayerManagerAPI.Instance.GetPlayerGold(actor)
            };
            MainViewModel.Instance.ButtonExtremeCommand.Execute(((int)power).ToString(CultureInfo.InvariantCulture));
            yield return NextFrame.Instance;
        }

        // args: mark, optional hold milliseconds. Selection must survive real render frames.
        private static IEnumerable<Wait> Pending(Step step)
        {
            Mark mark = RequiredMark(step.Arg(0));
            yield return new Until("native targeted power " + mark.Power, () => IsPending(Read(), mark));
            State first = Read();
            mark.Selection = first.Selection;
            mark.PendingGeneration = first.SelectionGeneration;
            yield return new Delay(step.Int(1, 250));
            State after = Read();
            Runner.Require(IsPending(after, mark) && ReferenceEquals(mark.Selection, after.Selection) &&
                after.Generation == mark.PendingGeneration, "power selection changed during idle render frames");
            RequireNoActions(mark);
            AcceptLog.Info("POWER_REGRESSION_PENDING mark=" + step.Arg(0) + " actor=" + mark.Actor +
                " power=" + mark.Power + " generation=" + after.Generation + " mode=" + after.Mode);
        }

        private static IEnumerable<Wait> Capture(Step step)
        {
            State state = Read();
            Runner.Require(state.Selection == null && !Targeting(state.Mode), "idle cancellation fixture is not idle");
            marks[step.Arg(0)] = new Mark { Generation = state.Generation,
                Session = UiAcceptancePlugin.StartedSession, Log = AcceptLog.Position };
            yield break;
        }

        // args: mark, tile X, tile Y. Native power targeting accepts the command-button
        // DOWN edge, unlike ordinary right-button unit orders (which accept the UP edge).
        private static IEnumerable<Wait> Confirm(Step step)
        {
            Mark mark = RequiredMark(step.Arg(0));
            Runner.Require(IsPending(Read(), mark), "confirmation requires the marked native selection");
            PatchInput();
            bool left = ConfigSettings.Settings_SH1RTSControls;
            int x = step.Int(1), y = step.Int(2);
            var down = new PowerGesture { X = x, Y = y, LeftState = left ? 1 : 0, RightDown = !left };
            var up = new PowerGesture { X = x, Y = y, LeftState = left ? 3 : 0, RightUp = !left };
            foreach (var wait in PerformGesture(down)) yield return wait;
            foreach (var wait in PerformGesture(up)) yield return wait;
            AcceptLog.Info("POWER_REGRESSION_GESTURE mark=" + step.Arg(0) + " tile=" + x + "," + y +
                " button=" + (left ? "left" : "right") + " downConsumed=True upConsumed=True");
            // Executed separately requires exactly one successful native confirmation;
            // merely delivering this gesture is not accepted as proof of execution.
        }

        private static IEnumerable<Wait> PerformGesture(PowerGesture gesture)
        {
            Runner.Require(Volatile.Read(ref pendingGesture) == null && Volatile.Read(ref activeGesture) == null,
                "previous power gesture still in flight");
            Volatile.Write(ref pendingGesture, gesture);
            yield return new Until("native input pass consumed the power button edge", () => Volatile.Read(ref gesture.Consumed) != 0);
        }

        private static void PatchInput()
        {
            if (inputPatched) return;
            var caller = AccessTools.Method(typeof(EditorDirector), nameof(EditorDirector.preDLLCallActions),
                new[] { typeof(int).MakeByRefType(), typeof(int).MakeByRefType() });
            var mouse = AccessTools.Method(typeof(EditorDirector), "getMouseStateForEngine",
                new[] { typeof(bool).MakeByRefType(), typeof(bool).MakeByRefType() });
            Runner.Require(caller != null && caller.ReturnType == typeof(void) && !caller.IsStatic &&
                mouse != null && mouse.ReturnType == typeof(int) && !mouse.IsStatic, "native input boundary changed");
            var harmony = UiAcceptancePlugin.Harmony;
            harmony.Patch(caller,
                prefix: new HarmonyMethod(typeof(SpectatorPowerRegressionSteps), nameof(BeforePowerInput)) { priority = Priority.First },
                postfix: new HarmonyMethod(typeof(SpectatorPowerRegressionSteps), nameof(AfterPowerTile)) { priority = Priority.First });
            harmony.Patch(caller,
                postfix: new HarmonyMethod(typeof(SpectatorPowerRegressionSteps), nameof(AfterPowerInput)) { priority = Priority.Last });
            harmony.Patch(mouse,
                postfix: new HarmonyMethod(typeof(SpectatorPowerRegressionSteps), nameof(AfterPowerMouse)) { priority = Priority.First });
            inputPatched = true;
        }

        // Logically inactive outside one queued test gesture; published patches stay installed.
        private static void BeforePowerInput() => Volatile.Write(ref activeGesture, Interlocked.Exchange(ref pendingGesture, null));

        private static void AfterPowerMouse(ref int __result, ref bool rightDown, ref bool rightUp)
        {
            PowerGesture gesture = Volatile.Read(ref activeGesture);
            if (gesture == null) return;
            __result = gesture.LeftState;
            rightDown = gesture.RightDown;
            rightUp = gesture.RightUp;
        }

        private static void AfterPowerTile(ref int mouseOverX, ref int mouseOverY)
        {
            PowerGesture gesture = Volatile.Read(ref activeGesture);
            if (gesture == null) return;
            mouseOverX = gesture.X; mouseOverY = gesture.Y;
        }

        private static void AfterPowerInput()
        {
            PowerGesture gesture = Interlocked.Exchange(ref activeGesture, null);
            if (gesture != null) Volatile.Write(ref gesture.Consumed, 1);
        }

        private static IEnumerable<Wait> Cancelled(Step step)
        {
            Mark mark = RequiredMark(step.Arg(0));
            long cancelledGeneration = mark.Selection != null ? mark.PendingGeneration : mark.Generation;
            yield return new Until("selection retired and cancellation generation advanced", () => {
                State state = Read();
                return state.Selection == null && !Targeting(state.Mode) && state.Generation > cancelledGeneration;
            });
            yield return new Delay(200);
            State after = Read();
            Runner.Require(after.Selection == null && !Targeting(after.Mode), "cancelled selection reappeared");
            RequireNoActions(mark);
            AcceptLog.Info("POWER_REGRESSION_CANCELLED mark=" + step.Arg(0) + " old=" + cancelledGeneration +
                " generation=" + after.Generation + " mode=" + after.Mode);
        }

        private static IEnumerable<Wait> Executed(Step step)
        {
            Mark mark = RequiredMark(step.Arg(0));
            var actions = new Regex(@"TEST_ACTION session=" + mark.Session + @" operation=\d+ kind=Power actor=" +
                mark.Actor + @" .* power=" + (int)mark.Power + @" tile=-?\d+ building=.* result=" +
                Regex.Escape(executedText) + @"\s*$", RegexOptions.CultureInvariant);
            yield return new Until("one successful native power confirmation", () => AcceptLog.Count(mark.Log, actions) >= 1);
            yield return new Delay(400);
            Runner.Require(AcceptLog.Count(mark.Log, actions) == 1, "power executed more than once");
            Runner.Require(AcceptLog.Count(mark.Log, new Regex(@"TEST_ACTION .*kind=Power ", RegexOptions.CultureInvariant)) == 1,
                "additional or rejected power action accompanied the confirmation");
            State after = Read();
            Runner.Require(after.Selection == null && !Targeting(after.Mode), "confirmed native power left a pending selection");
            Runner.Require(after.Generation > mark.Generation, "selection did not acquire a new generation");
            // AI economy is intentionally not frozen: this is an observation, not an exact gold-delta assertion.
            int goldAfter = GamePlayerManagerAPI.Instance.GetPlayerGold(mark.Actor);
            AcceptLog.Info("POWER_REGRESSION_EXECUTED mark=" + step.Arg(0) + " actor=" + mark.Actor +
                " power=" + mark.Power + " exactlyOnce=True generation=" + after.Generation +
                " goldBefore=" + mark.GoldBefore + " goldAfter=" + goldAfter);
        }

        private static IEnumerable<Wait> ReplacedMission(Step step)
        {
            Runner.Require(marks.TryGetValue(step.Arg(0), out Mark mark) && mark.Selection != null,
                "map replacement needs a previously observed active selection");
            yield return new Until("new mission after unloading the active selection", () =>
                UiAcceptancePlugin.StartedSession > 0 && UiAcceptancePlugin.StartedSession != mark.Session &&
                UiAcceptancePlugin.EndedSession != UiAcceptancePlugin.StartedSession);
            State state = Read();
            Runner.Require(state.Selection == null && !Targeting(state.Mode), "old selection survived mission replacement");
            Runner.Require(state.Generation > mark.PendingGeneration, "map cleanup did not invalidate the old generation");
            RequireNoActions(mark);
            AcceptLog.Info("POWER_REGRESSION_REPLACED oldSession=" + mark.Session +
                " session=" + UiAcceptancePlugin.StartedSession + " oldGeneration=" + mark.PendingGeneration +
                " generation=" + state.Generation + " mode=" + state.Mode);
        }

        private static bool IsPending(State state, Mark mark) => state.Selection != null &&
            state.Actor == mark.Actor && state.Power == (int)mark.Power && state.NativePower == (int)mark.Power &&
            state.Session == mark.Session && state.SelectionGeneration == state.Generation &&
            state.Generation > mark.Generation && Targeting(state.Mode);

        // NativePowerContext's existing audited contract: modes 4 and 5 are active power targeting.
        private static bool Targeting(int mode) => mode == 4 || mode == 5;

        private static State Read()
        {
            Runner.Require(runtime != null && UiAcceptancePlugin.StartedSession > 0 &&
                UiAcceptancePlugin.EndedSession != UiAcceptancePlugin.StartedSession, "power check requires the live mission");
            object native = context.GetValue(null);
            Runner.Require(native != null && Convert.ToBoolean(Member(native, "Ready"), CultureInfo.InvariantCulture),
                "native power context not ready");
            var state = new State {
                Selection = pending.GetValue(null), Generation = (long)generation.GetValue(null),
                Owner = (int)owner.GetValue(null), Mode = Convert.ToInt32(Member(native, "Mode"), CultureInfo.InvariantCulture),
                NativePower = Convert.ToInt32(Member(native, "SelectedPower"), CultureInfo.InvariantCulture)
            };
            if (state.Selection != null)
            {
                state.Actor = Convert.ToInt32(Member(state.Selection, "Actor"), CultureInfo.InvariantCulture);
                state.Session = Convert.ToInt64(Member(state.Selection, "Session"), CultureInfo.InvariantCulture);
                state.SelectionGeneration = Convert.ToInt64(Member(state.Selection, "Generation"), CultureInfo.InvariantCulture);
                state.Power = Convert.ToInt32(Member(Member(state.Selection, "Power"), "Id"), CultureInfo.InvariantCulture);
            }
            return state;
        }

        private static object Descriptor(ExtremePower power)
        {
            object model = SpectatorSteps.Model() ?? throw new StepFailure("Spectator tools model unavailable");
            var powers = Member(model, "Powers") as IEnumerable;
            Runner.Require(powers != null, "power descriptors unavailable");
            foreach (object item in powers)
                if (Convert.ToInt32(Member(item, "Id"), CultureInfo.InvariantCulture) == (int)power) return item;
            throw new StepFailure("missing power descriptor " + power);
        }

        private static bool PowerButtonsReady()
        {
            var live = buttons.GetValue(null) as Array;
            if (live == null || live.Length == 0) return false;
            foreach (object button in live) if (button == null) return false;
            return Convert.ToBoolean(Member(Descriptor(ExtremePower.ArrowVolley), "Enabled"), CultureInfo.InvariantCulture) &&
                Convert.ToBoolean(Member(Descriptor(ExtremePower.GrantGold), "Enabled"), CultureInfo.InvariantCulture);
        }

        private static void RequireNoActions(Mark mark) => Runner.Require(AcceptLog.Count(mark.Log,
            new Regex(@"TEST_ACTION .*kind=Power ", RegexOptions.CultureInvariant)) == 0,
            "cancelled/unconfirmed selection executed a power action");

        private static Mark RequiredMark(string name)
        {
            Runner.Require(marks.TryGetValue(name, out Mark mark), "unknown power mark " + name);
            Runner.Require(mark.Session == UiAcceptancePlugin.StartedSession, "power mark belongs to a replaced mission");
            return mark;
        }

        private static FieldInfo RequiredStatic(string name) => runtime.GetField(name, Static) ??
            throw new StepFailure("Spectator state field missing: " + name);

        private static object Member(object value, string name)
        {
            if (value == null) throw new StepFailure("null object reading " + name);
            Type type = value.GetType();
            var field = type.GetField(name, Instance);
            if (field != null) return field.GetValue(value);
            var property = type.GetProperty(name, Instance);
            if (property != null && property.GetIndexParameters().Length == 0) return property.GetValue(value, null);
            throw new StepFailure("Spectator state member missing: " + type.FullName + "." + name);
        }
    }
}
