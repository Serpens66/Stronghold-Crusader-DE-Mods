using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.IO;
using System.Threading;
using APIShared.Automation;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;

namespace UnitRelocationAcceptance
{
    internal static partial class Fixture
    {
        private static int fixtureUnityThread;
        private static bool OwnScenario(string scenario) => scenario == "unit-relocation-integration" ||
            scenario == "area-input-idle-before" || scenario == "area-input-idle-after";
        private static IEnumerable<AutomationStep> ScenarioSteps(string scenario)
        {
            if (scenario == "unit-relocation-integration") return Steps();
            bool expectedBypass = scenario == "area-input-idle-after";
            return new[] {
                new AutomationStep("area-fresh-world", TimeSpan.FromSeconds(180), run => Start(run, true)),
                new AutomationStep("area-actual-input-gate", TimeSpan.FromSeconds(90),
                    run => AreaInputChecks(run, expectedBypass), true)
            };
        }
        private static FieldInfo AreaField(Type type, string name) => type.GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingFieldException(type.FullName, name);
        private static MethodInfo AreaMethod(Type type, string name) => type.GetMethod(name,
            BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingMethodException(type.FullName, name);
        private static object AreaValue(object instance, string name) => AreaField(instance.GetType(), name).GetValue(instance);

        private static void RequireAreaIdle(object runtime, long session)
        {
            Require(!(bool)AreaValue(runtime,"captured") && !(bool)AreaValue(runtime,"dragging") &&
                !(bool)AreaValue(runtime,"faulted"), "area fixture is not an unfaulted idle input owner");
            Require(((ICollection)AreaValue(runtime,"toBuild")).Count == 0 && AreaValue(runtime,"storageBuild") == null &&
                AreaValue(runtime,"quarryOrder") == null, "area fixture has pending construction work");
            var mission = (APIShared.MissionContext)AreaValue(runtime,"mission");
            Require(mission != null && mission.SessionId == session, "area fixture has a stale mission");
            Require((int)AreaValue(runtime,"cancellation") == (int)AreaValue(runtime,"appliedCancellation"),
                "area fixture starts with unapplied cancellation");
        }
        private static IEnumerable<AutomationWait> AreaInputChecks(AutomationRun run, bool expectedBypass)
        {
            foreach (var wait in Ready(run)) yield return wait;
            Require(Thread.CurrentThread.ManagedThreadId == fixtureUnityThread && fixtureUnityThread != 0,
                "area fixture is not executing on APIShared's validated Unity context");
            var main = AppDomain.CurrentDomain.GetAssemblies();
            Type type = null;
            foreach (var assembly in main)
                if ((type = assembly.GetType("BugfixesAndQoL.AreaConstruction.PlacementInput", false)) != null) break;
            Require(type != null, "actual production placement input is absent");
            var instanceField = type.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            Require(instanceField != null, "production placement input owner contract changed");
            var runtime = instanceField.GetValue(null);
            Require(runtime != null && EditorDirector.instance != null, "production placement input/director unavailable");
            var gate = AreaValue(runtime,"engine");
            var engineField = typeof(EngineInterface).GetField("threadLock", BindingFlags.Static | BindingFlags.NonPublic);
            Require(engineField != null && ReferenceEquals(gate, engineField.GetValue(null)) && !Monitor.IsEntered(gate),
                "fixture must use the real engine monitor outside InSimulation");
            run.InSimulation(run.CurrentMission, () => {
                Require(GamePlayerManagerAPI.Instance.IsLocalPaused(), "area fixture requires confirmed native pause");
                RequireAreaIdle(runtime, run.CurrentMission.SessionId);
                return true;
            });
            string mainHash;
            using (var input = File.OpenRead(type.Assembly.Location)) using (var sha = SHA256.Create())
                mainHash = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
            Info("AREA_GATE sourceModeExpectedBypass=" + expectedBypass + " mainSha256=" + mainHash +
                " unityThread=" + fixtureUnityThread + " paused=True actualRuntime=True");

            var afterInput = type.GetMethod("AfterInput", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(EditorDirector) }, null);
            Require(afterInput != null && afterInput.ReturnType == typeof(void), "actual AfterInput contract changed");
            var director = EditorDirector.instance;
            var idle = ProbeAreaGate(gate, () => afterInput.Invoke(null, new object[] { director }));
            Require(idle.ReturnedWhileHeld == expectedBypass, "actual idle AfterInput engine-monitor behavior differs from explicit test mode");
            RequireAreaIdle(runtime, run.CurrentMission.SessionId);
            checks++;
            Info("CASE_PASS area-idle-gate expectedBypass=" + expectedBypass + " returnedWhileHeld=" + idle.ReturnedWhileHeld +
                " elapsedMs=" + idle.ElapsedMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));

            var primary = AreaMethod(type,"Primary");
            var allowCursor = AreaMethod(type,"AllowCursor");
            var focusChanged = AreaMethod(type,"FocusChanged");
            Require(primary.IsStatic && primary.ReturnType == typeof(bool) &&
                allowCursor.IsStatic && allowCursor.ReturnType == typeof(bool) &&
                !focusChanged.IsStatic && focusChanged.ReturnType == typeof(void), "area input callback contracts changed");
            // Cache the director and arguments on Unity. Workers invoke only production
            // passthrough methods; they never query Unity singletons or manufacture input.
            object[] primaryArgs = { director, 0 };
            object[] cursorArgs = { (int)eMappers.MAPPER_HOVEL, 2, true };
            ProbeAreaPassthrough("primary-idle", gate, () => (bool)primary.Invoke(null, primaryArgs), expectedBypass);
            RequireAreaIdle(runtime, run.CurrentMission.SessionId);
            ProbeAreaPassthrough("cursor-idle", gate, () => (bool)allowCursor.Invoke(null, cursorArgs), expectedBypass);
            RequireAreaIdle(runtime, run.CurrentMission.SessionId);
            var focus = ProbeAreaGate(gate, () => focusChanged.Invoke(runtime, new object[] { false }));
            Require(focus.ReturnedWhileHeld == expectedBypass, "idle focus callback engine-monitor behavior differs from explicit mode");
            RequireAreaIdle(runtime, run.CurrentMission.SessionId);
            checks++;
            Info("CASE_PASS area-focus-idle expectedBypass=" + expectedBypass + " returnedWhileHeld=" + focus.ReturnedWhileHeld);
            int primaryWorker = 0, cursorWorker = 0;
            ProbeAreaPassthrough("primary-nonui", gate,
                () => InvokeAreaWorker(() => (bool)primary.Invoke(null, primaryArgs), out primaryWorker), expectedBypass);
            Require(primaryWorker != 0 && primaryWorker != fixtureUnityThread, "primary passthrough did not execute on a separate worker");
            RequireAreaIdle(runtime, run.CurrentMission.SessionId);
            ProbeAreaPassthrough("cursor-nonui", gate,
                () => InvokeAreaWorker(() => (bool)allowCursor.Invoke(null, cursorArgs), out cursorWorker), expectedBypass);
            Require(cursorWorker != 0 && cursorWorker != fixtureUnityThread, "cursor passthrough did not execute on a separate worker");
            RequireAreaIdle(runtime, run.CurrentMission.SessionId);

            AreaMethod(type,"CancelLocalWork").Invoke(runtime, null);
            Require((int)AreaValue(runtime,"cancellation") != (int)AreaValue(runtime,"appliedCancellation"),
                "real cancellation did not create the required epoch mismatch");
            var cancellation = ProbeAreaGate(gate, () => afterInput.Invoke(null, new object[] { director }));
            Require(!cancellation.ReturnedWhileHeld, "pending cancellation bypassed the engine monitor");
            RequireAreaIdle(runtime, run.CurrentMission.SessionId);
            Require(ReferenceEquals(runtime, instanceField.GetValue(null)) && ReferenceEquals(director, EditorDirector.instance),
                "the tested production owner or director was replaced");
            checks++;
            Info("CASE_PASS area-cancellation-gate returnedWhileHeld=False applied=True elapsedMs=" +
                cancellation.ElapsedMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
        }
        private static void ProbeAreaPassthrough(string name, object gate, Func<bool> callback, bool expectedBypass)
        {
            bool vanilla = false;
            var probe = ProbeAreaGate(gate, () => vanilla = callback());
            Require(vanilla && probe.ReturnedWhileHeld == expectedBypass,
                name + " changed Vanilla passthrough or differs from explicit monitor expectation");
            checks++;
            Info("CASE_PASS area-" + name + " expectedBypass=" + expectedBypass +
                " returnedWhileHeld=" + probe.ReturnedWhileHeld + " vanilla=True elapsedMs=" +
                probe.ElapsedMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
        }
        private static bool InvokeAreaWorker(Func<bool> callback, out int threadId)
        {
            bool result = false;
            int actualThread = 0;
            Exception error = null;
            var worker = new Thread(() => {
                actualThread = Thread.CurrentThread.ManagedThreadId;
                try { result = callback(); } catch (Exception ex) { error = ex; }
            }) { IsBackground = true, Name = "AreaPassthroughAcceptance" };
            worker.Start();
            if (!worker.Join(2000)) throw new InvalidOperationException("area passthrough worker did not finish after bounded gate release");
            threadId = actualThread;
            if (error != null) throw new InvalidOperationException("actual non-UI passthrough failed", error);
            return result;
        }
        private sealed class AreaGateResult
        {
            internal bool ReturnedWhileHeld;
            internal double ElapsedMs;
        }
        private static AreaGateResult ProbeAreaGate(object gate, Action actualAfterInput)
        {
            Require(!Monitor.IsEntered(gate), "probe caller already holds the engine monitor");
            using (var ready = new ManualResetEvent(false)) using (var release = new ManualResetEvent(false))
            {
                int acquired = 0, expired = 0, exited = 0;
                Exception helperError = null, invocationError = null;
                var helper = new Thread(() => {
                    bool entered = false;
                    try
                    {
                        entered = Monitor.TryEnter(gate, 1000);
                        if (!entered) return;
                        Volatile.Write(ref acquired, 1);
                        ready.Set();
                        if (!release.WaitOne(750)) Volatile.Write(ref expired, 1);
                    }
                    catch (Exception ex) { helperError = ex; }
                    finally
                    {
                        if (entered) Monitor.Exit(gate);
                        Volatile.Write(ref exited, 1);
                        ready.Set();
                    }
                }) { IsBackground = true, Name = "AreaGateAcceptance" };
                bool returnedWhileHeld = false, setupReady = false;
                long started = 0, stopped = 0;
                helper.Start();
                try
                {
                    setupReady = ready.WaitOne(1500) && Volatile.Read(ref acquired) != 0 &&
                        Volatile.Read(ref expired) == 0 && Volatile.Read(ref exited) == 0;
                    if (setupReady)
                    {
                        // No APIShared actions, logging, assertions, or field mutation while
                        // the helper owns the gate: invoke only the real Unity callback.
                        started = Stopwatch.GetTimestamp();
                        try { actualAfterInput(); } catch (Exception ex) { invocationError = ex; }
                        stopped = Stopwatch.GetTimestamp();
                        returnedWhileHeld = Volatile.Read(ref expired) == 0 && Volatile.Read(ref exited) == 0;
                    }
                }
                finally
                {
                    release.Set();
                    if (!helper.Join(2000)) throw new InvalidOperationException("area gate helper did not stop after release");
                }
                Require(setupReady && helperError == null, "area gate helper failed to establish monitor ownership");
                if (invocationError != null) throw new InvalidOperationException("actual AfterInput invocation failed", invocationError);
                return new AreaGateResult { ReturnedWhileHeld = returnedWhileHeld,
                    ElapsedMs = (stopped - started) * 1000.0 / Stopwatch.Frequency };
            }
        }
    }
}
