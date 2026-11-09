using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using APIShared.Presentation;

namespace UiAcceptance
{
    // Waits yielded by step iterators. The runner evaluates them once per rendered frame
    // (including while paused) on the Unity thread.
    internal abstract class Wait { }
    internal sealed class NextFrame : Wait { internal static readonly NextFrame Instance = new NextFrame(); }
    internal sealed class Delay : Wait { internal readonly double Ms; internal Delay(double ms) { Ms = ms; } }
    internal sealed class Until : Wait
    {
        internal readonly Func<bool> Condition; internal readonly string What;
        internal Until(string what, Func<bool> condition) { What = what; Condition = condition; }
    }

    internal sealed class StepFailure : Exception { internal StepFailure(string message) : base(message) { } }

    internal static class Runner
    {
        internal delegate IEnumerable<Wait> Handler(Step step);
        private static readonly Dictionary<string, Handler> handlers = new Dictionary<string, Handler>(StringComparer.OrdinalIgnoreCase);
        private static Scenario scenario;
        private static int index = -1, passed, failed;
        private static IEnumerator<Wait> current;
        private static Step step;
        private static long stepLog;
        private static Stopwatch stepClock, waitClock;
        private static Wait waiting;
        private static IUiFrameWorkRegistration frame;
        private static bool finished;
        private static int lastFrame = -1;
        internal static Scenario Active => scenario;

        internal static void Register(string name, Handler handler) => handlers.Add(name, handler);

        internal static void Start(Scenario value, IUiFrameWorkRegistration registration)
        {
            foreach (Step s in value.Steps)
                if (!handlers.ContainsKey(s.Do)) throw new InvalidOperationException("Unknown step action '" + s.Do + "' (" + s.Id + ").");
            scenario = value; frame = registration;
            AcceptLog.Info("Scenario " + value.Series + " role=" + value.Role + " steps=" + value.Steps.Count + " armed.");
            frame.SetContinuous(true);
        }

        internal static void Tick()
        {
            if (scenario == null || lastFrame == UnityEngine.Time.frameCount) return;
            lastFrame = UnityEngine.Time.frameCount;
            if (finished) { DriveQuit(); return; }
            try
            {
                for (int guard = 0; guard < 8; guard++)
                {
                    if (finished || current == null && !BeginNext()) return;
                    if (waiting != null && !Satisfied()) return;
                    waiting = null;
                    if (!current.MoveNext()) { CompleteStep(null); continue; }
                    waiting = current.Current; waitClock = Stopwatch.StartNew();
                    if (waiting is NextFrame) return;
                }
            }
            catch (Exception error) { CompleteStep(error is StepFailure ? error.Message : error.ToString()); }
        }

        private static bool Satisfied()
        {
            if (stepClock.ElapsedMilliseconds > step.TimeoutMs)
                throw new StepFailure("timeout after " + step.TimeoutMs + " ms" + (waiting is Until u ? " waiting for " + u.What : ""));
            switch (waiting)
            {
                case NextFrame _: return true;
                case Delay d: return waitClock.Elapsed.TotalMilliseconds >= d.Ms;
                case Until u: return u.Condition();
                default: return true;
            }
        }

        private static bool BeginNext()
        {
            if (++index >= scenario.Steps.Count) { Finish(); return false; }
            step = scenario.Steps[index];
            stepLog = AcceptLog.Position;
            stepClock = Stopwatch.StartNew();
            AcceptLog.Info("STEP " + step.Id + " " + step.Do + " " + string.Join(" ", step.Args));
            current = handlers[step.Do](step).GetEnumerator();
            waiting = null;
            return true;
        }

        private static void CompleteStep(string failure)
        {
            var problems = new List<string>();
            if (failure != null) problems.Add(failure);
            foreach (Expectation e in step.Expect)
            {
                int count = AcceptLog.Count(stepLog, e.Marker);
                if (count < e.Min || count > e.Max)
                    problems.Add("marker /" + e.Text + "/ count=" + count + " expected " + e.Min + ".." + (e.Max == int.MaxValue ? "inf" : e.Max.ToString()));
            }
            List<string> errors = AcceptLog.Errors(stepLog);
            if (!step.AllowErrors && errors.Count > 0)
                problems.Add("errors=" + errors.Count + " first=" + errors[0].Replace('\n', ' '));
            bool pass = problems.Count == 0;
            if (pass) passed++; else failed++;
            AcceptLog.Result(step.Id, pass, step.Do + " ms=" + stepClock.ElapsedMilliseconds + (pass ? "" : " :: " + string.Join(" | ", problems)));
            current = null; waiting = null;
            if (!pass && step.StopOnFail) { AcceptLog.Warn("stopOnFail: remaining steps skipped."); Finish(); }
        }

        private static void Finish()
        {
            if (finished) return;
            finished = true;
            AcceptLog.Result("SUMMARY", failed == 0, "series=" + scenario.Series + " passed=" + passed + " failed=" + failed +
                " skipped=" + Math.Max(0, scenario.Steps.Count - passed - failed));
            current = null; waiting = null;
            if (scenario.QuitWhenDone) quitter = GameSteps.QuitSequence(scenario.QuitDelayMs).GetEnumerator();
            else frame?.SetContinuous(false);
        }

        // After the summary only the quit sequence runs (Delay waits; no step timeout).
        private static IEnumerator<Wait> quitter;
        private static void DriveQuit()
        {
            try
            {
                if (quitter == null) return;
                if (waiting is Delay d && waitClock.Elapsed.TotalMilliseconds < d.Ms) return;
                if (quitter.MoveNext()) { waiting = quitter.Current; waitClock = Stopwatch.StartNew(); }
                else { quitter = null; waiting = null; frame?.SetContinuous(false); }
            }
            catch (Exception error) { quitter = null; AcceptLog.Warn("quit failed: " + error.Message); }
        }

        // Shared assertion helpers for handlers.
        internal static void Require(bool condition, string message) { if (!condition) throw new StepFailure(message); }
        internal static long StepLog => stepLog;
    }
}
