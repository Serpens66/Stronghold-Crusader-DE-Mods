using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using APIShared;
using APIShared.Presentation;
using CrusaderDE;

namespace UiAcceptance
{
    // Holds real buffer presentation after a synchronous local save load. The independent release
    // registration keeps working if LoadSave or the scenario driver fails while awaiting ticks.
    internal static class MissionReadinessSteps
    {
        private sealed class HoldRun
        {
            internal string Path;
            internal object PreviousState;
            internal long Session, PreviousSession;
            internal int DurationMs, Receipts;
            internal bool Armed, Released, Checked16, Checked30;
            internal double ReleasedAfterMs;
            internal Stopwatch Clock;
            internal volatile string Failure;
        }

        private static IMissionPlayStateReadinessCapability readiness;
        private static IUiFrameWorkRegistration frame;
        private static HoldRun run;
        private static int unityThread;
        private static bool registered;
        private static MethodInfo spectatorActive;

        internal static void Register()
        {
            Runner.Register("MissionReadiness.Arm", Arm);
            Runner.Register("MissionReadiness.ExpectHold", ExpectHold);
            Runner.Register("MissionReadiness.Reports", Reports);
            Runner.Register("MissionReadiness.ExpectRetired", ExpectRetired);
        }

        private static void EnsureRegistered()
        {
            if (registered) return;
            unityThread = Thread.CurrentThread.ManagedThreadId;
            var api = ApiShared.ForMod(UiAcceptancePlugin.Guid);
            if (!api.TryGetMissionPlayStateReadiness(out readiness, out var diagnostic))
                throw new StepFailure("Mission readiness unavailable: " + diagnostic?.Reason);
            if (!api.Ui.TryRegisterBeforeRender("mission-readiness-acceptance", HoldFrame,
                out frame, out string reason, 950)) throw new StepFailure(reason);
            if (UiAcceptancePlugin.Missions == null ||
                !UiAcceptancePlugin.Missions.TryRegisterObserver("mission-readiness-hold", Started, Ended, null, out diagnostic) ||
                !readiness.TryRegisterObserver("mission-readiness-acceptance", Ready, out diagnostic))
                throw new StepFailure("Mission readiness acceptance registration failed: " + diagnostic?.Reason);
            registered = true;
        }

        private static IEnumerable<Wait> Arm(Step step)
        {
            EnsureRegistered();
            RequireUnity();
            Runner.Require(GameSteps.MenuReady(), "arm the delayed-readiness load from the main menu");
            Runner.Require(run == null || run.Released, "a previous readiness hold is still active");
            Runner.Require(readiness.Current == null, "the previous mission readiness was not retired");
            int duration = step.Int(1, 35000);
            Runner.Require(duration >= 35000 && duration <= 60000, "readiness hold must last 35000..60000 ms");
            string path = step.Arg(0);
            if (!Path.IsPathRooted(path)) path = Path.Combine(ConfigSettings.GetSavesPath(), path);
            path = Path.GetFullPath(path);
            Runner.Require(File.Exists(path), "readiness save not found: " + path);
            run = new HoldRun { Path = path, PreviousState = GameData.Instance?.lastGameState,
                PreviousSession = run?.Session ?? 0, DurationMs = duration, Armed = true };
            AcceptLog.Info("MISSION_READINESS_ARM holdMs=" + duration + " save=" + Path.GetFileName(path));
            yield break;
        }

        private static void Started(MissionLifecycleNotification notification)
        {
            HoldRun current = run;
            if (current == null || !current.Armed || notification.IsReplay || !notification.Context.IsSave ||
                !string.Equals(Path.GetFullPath(notification.Context.FilePath), current.Path, StringComparison.OrdinalIgnoreCase)) return;
            current.Armed = false;
            try
            {
                RequireUnity();
                Runner.Require(!notification.Context.Mode.IsRealMultiplayer && !notification.Context.IsEditor,
                    "the presentation hold only supports a local save load");
                Runner.Require(notification.Context.SessionId != current.PreviousSession,
                    "the repeated load reused the previous mission identity");
                current.Session = notification.Context.SessionId;
                object holdState = GameData.Instance?.lastGameState;
                // Vanilla postLoading clears the previous world's state before starting the
                // simulation. Null is not a published state and must not fail a repeated load.
                Runner.Require(holdState == null || ReferenceEquals(holdState, current.PreviousState),
                    "a new PlayState was already presented before the synchronous start hold");
                bool oldStateCleared = current.PreviousState != null && holdState == null;
                current.PreviousState = holdState;
                Runner.Require(readiness.Current == null, "mission readiness arrived before the initial hold");
                current.Clock = Stopwatch.StartNew();
                Director.instance.SetPausedState(true);
                frame.RequestAfter(TimeSpan.FromSeconds(16));
                AcceptLog.Info("MISSION_READINESS_HOLD session=" + current.Session + " holdMs=" + current.DurationMs +
                    " oldStateCleared=" + oldStateCleared);
            }
            catch (Exception error)
            {
                current.Failure = error.Message;
                // If pausing succeeded but scheduling/logging failed, release on this same Unity
                // callback when possible; an unrelated or non-Unity world is never touched.
                if (Thread.CurrentThread.ManagedThreadId == unityThread) Release(current, "start-failed");
                else frame?.Request();
            }
        }

        private static void Ended(MissionLifecycleNotification notification)
        {
            HoldRun current = run;
            if (current == null || current.Released || current.Session != notification.Context?.SessionId) return;
            current.Failure = "the held mission ended before release";
            frame?.Request();
        }

        private static void Ready(MissionPlayStateReadyNotification notification)
        {
            HoldRun current = run;
            if (current == null || current.Session == 0 || notification.Context.SessionId != current.Session) return;
            current.Receipts++;
            if (Thread.CurrentThread.ManagedThreadId != unityThread || !current.Released ||
                current.Receipts != 1 || readiness.Current?.SessionId != current.Session ||
                ReferenceEquals(GameData.Instance?.lastGameState, current.PreviousState))
            {
                current.Failure = "readiness was premature, duplicated, stale or delivered outside Unity";
                frame?.Request();
            }
            AcceptLog.Info("MISSION_READINESS_RECEIPT session=" + current.Session + " count=" + current.Receipts +
                " released=" + current.Released + " replay=" + notification.IsReplay);
        }

        private static void HoldFrame()
        {
            HoldRun current = run;
            if (current == null || current.Released || current.Armed) return;
            try
            {
                RequireUnity();
                if (current.Failure != null) { Release(current, "failed"); return; }
                Runner.Require(UiAcceptancePlugin.Missions.Current?.SessionId == current.Session,
                    "the held mission was replaced before release");
                Runner.Require(readiness.Current == null && current.Receipts == 0 &&
                    ReferenceEquals(GameData.Instance?.lastGameState, current.PreviousState),
                    "the first new PlayState/readiness was published while presentation was paused");
                double elapsed = current.Clock.Elapsed.TotalMilliseconds;
                if (!current.Checked16 && elapsed >= 16000)
                {
                    current.Checked16 = true;
                    AcceptLog.Info("MISSION_READINESS_STILL_PENDING session=" + current.Session + " checkpoint=16 elapsedMs=" + (long)elapsed);
                }
                if (!current.Checked30 && elapsed >= 30000)
                {
                    current.Checked30 = true;
                    AcceptLog.Info("MISSION_READINESS_STILL_PENDING session=" + current.Session + " checkpoint=30 elapsedMs=" + (long)elapsed);
                }
                if (elapsed >= current.DurationMs) { Release(current, "elapsed"); return; }
                double next = !current.Checked16 ? 16000 : !current.Checked30 ? 30000 : current.DurationMs;
                frame.RequestAfter(TimeSpan.FromMilliseconds(Math.Max(1, next - elapsed)));
            }
            catch (Exception error) { current.Failure = error.Message; Release(current, "check-failed"); }
        }

        private static void Release(HoldRun current, string reason)
        {
            RequireUnity();
            if (current.Released) return;
            current.ReleasedAfterMs = current.Clock?.Elapsed.TotalMilliseconds ?? 0;
            // Never unpause a new mission after an ended/replaced hold.
            if (current.Session > 0 && UiAcceptancePlugin.Missions.Current?.SessionId == current.Session)
                Director.instance.SetPausedState(false);
            current.Released = true;
            frame.CancelPending();
            AcceptLog.Info("MISSION_READINESS_RELEASE session=" + current.Session + " elapsedMs=" +
                (long)current.ReleasedAfterMs + " reason=" + reason);
        }

        private static IEnumerable<Wait> ExpectHold(Step step)
        {
            Runner.Require(run != null, "no readiness hold was armed");
            yield return new Until("released mission readiness receipt", () =>
            {
                Runner.Require(run.Failure == null, run.Failure);
                return run.Released && run.Receipts == 1;
            });
            Runner.Require(run.Session > 0 && !run.Armed && run.Checked16 && run.Checked30 &&
                run.ReleasedAfterMs >= run.DurationMs && readiness.Current?.SessionId == run.Session,
                "the requested real hold/checkpoints or final mission receipt were not verified");
            AcceptLog.Info("MISSION_READINESS_VERIFIED session=" + run.Session + " holdMs=" + (long)run.ReleasedAfterMs);
        }

        private static IEnumerable<Wait> Reports(Step step)
        {
            if (spectatorActive == null)
            {
                Type type = GameSteps.FindType("Spectator.SpectatorPerspectiveRuntime");
                spectatorActive = type?.GetMethod("IsActiveSpectator", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Runner.Require(spectatorActive != null && spectatorActive.ReturnType == typeof(bool) &&
                    spectatorActive.GetParameters().Length == 0, "Spectator active-view observation is unavailable");
            }
            yield return new Until("Spectator active after delayed first PlayState", () => (bool)spectatorActive.Invoke(null, null));
            foreach (var page in new[] { new[] { 0, 71 }, new[] { 4, 75 }, new[] { 0, 71 }, new[] { 5, 76 }, new[] { 0, 71 } })
            {
                MainViewModel.Instance.ButtonReportsCommand.Execute(page[0].ToString(System.Globalization.CultureInfo.InvariantCulture));
                int expected = page[1];
                yield return new Until("Vanilla report page " + expected, () =>
                    GameData.Instance?.lastGameState?.app_mode == 16 && GameData.Instance.lastGameState.app_sub_mode == expected);
                AcceptLog.Info("MISSION_READINESS_REPORT session=" + run.Session + " submode=" + expected);
            }
        }

        private static IEnumerable<Wait> ExpectRetired(Step step)
        {
            Runner.Require(run != null && run.Released && run.Receipts == 1 && run.Failure == null,
                "previous readiness hold did not complete cleanly");
            Runner.Require(readiness.Current == null && UiAcceptancePlugin.Missions.Current == null,
                "mission end retained an old readiness or lifecycle context");
            AcceptLog.Info("MISSION_READINESS_RETIRED session=" + run.Session);
            yield break;
        }

        private static void RequireUnity() => Runner.Require(Thread.CurrentThread.ManagedThreadId == unityThread,
            "mission readiness acceptance requires the captured Unity thread");
    }
}
