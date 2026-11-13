using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using APIShared;
using APIShared.Automation;
using APIShared.Pathfinding;
using BepInEx.Bootstrap;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace PathfindingHazardTest
{
    // Unity drives the public automation API. Only the real native-map-tick callback touches
    // the provider, cases, effects or actors. Immutable observation strings cross back to Unity.
    internal sealed unsafe class Fixture
    {
        private readonly Scenario scenario;
        private readonly IMissionLifecycleCapability missions;
        private readonly Action<string> log;
        private readonly Action<Action> postUnity;
        private readonly ConcurrentQueue<string> messages = new ConcurrentQueue<string>();
        private volatile string error, cleanup = "not-active";
        private volatile bool done;
        private int request, phase, started, waitStarted, waitUntil;
        private long session, abortedBefore;
        private uint actorGlobal, tribeGlobal;
        private int owner, originalTribe, originX, originY;
        private bool sawHazard, sawSafe, sawReturning, waitedPastDeadline;
        private Bridge bridge;
        internal Fixture(Scenario scenario, IMissionLifecycleCapability missions, Action<string> log, Action<Action> postUnity)
        { this.scenario = scenario; this.missions = missions; this.log = log; this.postUnity = postUnity; }
        internal string CleanupStatus => cleanup;
        internal IEnumerable<AutomationStep> Steps()
        {
            yield return new AutomationStep("prepared-save", TimeSpan.FromSeconds(180), run => run.LoadSave(scenario.SavePath));
            yield return new AutomationStep("observe-real-hazard", TimeSpan.FromSeconds(600), Observe, missionBound: true);
        }
        private IEnumerable<AutomationWait> Observe(AutomationRun run)
        {
            yield return run.WaitForPlayState(run.CurrentMission);
            if (run.CurrentMission.Mode.IsRealMultiplayer) throw new InvalidOperationException("Prepared driver is SP only; use a separate authenticated two-peer acceptance.");
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            session = run.CurrentMission.SessionId;
            Interlocked.Exchange(ref request, 1);
            try
            {
                yield return AutomationWait.Until("genuine state transitions and confirmed restoration", () => {
                    Drain();
                    if (error != null) throw new InvalidOperationException(error);
                    return done && cleanup == "restored";
                });
                Drain();
                log("scenario=" + scenario.Name + " gameplay transitions observed; original options/cases restored");
            }
            finally { RequestCleanup(); }
        }
        private void Drain() { while (messages.TryDequeue(out string text)) log(text); }
        private void Emit(string text) { messages.Enqueue(text); postUnity(Drain); }
        internal void RequestCleanup()
        {
            if (cleanup == "restored") return;
            Interlocked.Exchange(ref request, 2);
        }
        // Public APIShared SimulationTickEvents callback, order 700, after Pathfinding's order 600 scheduler.
        internal void Tick(int tick)
        {
            int action = Interlocked.Exchange(ref request, 0);
            try
            {
                if (action == 2) { Restore(); return; }
                if (action == 1) Activate(tick);
                if (bridge == null || done || error != null || cleanup == "restored") return;
                Require(missions.Current?.SessionId == session, "Test mission was replaced; no actor/case action in a different mission.");
                Require(bridge.OwnsLease, "Mission options were replaced; test lease is no longer current.");
                GameUnit* actor = Actor();
                int x = actor->r_CurrentTilePositionX, y = actor->r_CurrentTilePositionY;
                bool witness = bridge.Hazard(scenario.Plague, scenario.HazardX, scenario.HazardY);
                bool here = bridge.Hazard(scenario.Plague, x, y);
                sawHazard |= witness;
                Require(unchecked(tick - started) <= scenario.MaximumTicks, "Native observation deadline reached without completing the required transitions.");
                if (scenario.Escape) ObserveEscape(actor, tick, here);
                else ObserveWait(actor, tick, witness);
            }
            catch (Exception exception)
            {
                error = "tick=" + tick + " " + Unwrap(exception);
                Emit("FAILED " + error);
                try { Restore(); }
                catch (Exception failure) { cleanup = "failed"; error += "; restoration failed: " + Unwrap(failure); }
            }
        }
        private void Activate(int tick)
        {
            Require(missions.Current?.SessionId == session, "Activation mission mismatch.");
            GameUnit* actor;
            Require(UnitAccess.TryGetById(scenario.ActorId, out actor, out _) && actor != null && UnitAccess.IsReallyAlive(actor), "Prepared actor missing/dead.");
            Require(actor->r_UnitChimp == eChimps.CHIMP_TYPE_SPEARMAN, "Prepared driver currently accepts a spearman; other types remain separate matrix cases.");
            Require(!Walking(actor), "Prepared actor must stand still before test activation.");
            owner = actor->r_ControllableForPlayerId;
            Require(GamePlayerManagerAPI.Instance.IsPlayerIdValid(owner) && !GamePlayerManagerAPI.Instance.IsAIPlayer(owner), "Actor must be human-controlled; AI interventions are not enabled by this fixture.");
            originalTribe = actor->r_TribeId;
            Require(GameTribeManagerAPI.Instance.TryGetTribeById(originalTribe, out GameTribe* tribe) && tribe != null &&
                tribe->r_AliveState == AliveState.IsAlive && tribe->r_PlayerIdOwner == owner && tribe->r_UnitsInGroup == 1,
                "Prepared actor needs a live one-man group of the same owner.");
            actorGlobal = actor->r_GlobalId; tribeGlobal = tribe->r_GlobalId;
            originX = actor->r_CurrentTilePositionX; originY = actor->r_CurrentTilePositionY;
            bridge = new Bridge();
            cleanup = "activation-pending"; // A partial ApplyMissionOptions failure must also restore its lease.
            bridge.Activate(scenario);
            cleanup = "pending"; started = tick; phase = 0;
            abortedBefore = bridge.Counter("replanAborted");
            Emit("activated actual-simulation-callback tick=" + tick + " actor=" + scenario.ActorId + ":" + actorGlobal +
                " tribe=" + originalTribe + ":" + tribeGlobal + " scenario=" + scenario.Name + "; setup is not a gameplay PASS");
        }
        private GameUnit* Actor()
        {
            Require(UnitAccess.TryGetById(scenario.ActorId, out GameUnit* actor, out _) && actor != null &&
                UnitAccess.IsReallyAlive(actor) && actor->r_GlobalId == actorGlobal && actor->r_ControllableForPlayerId == owner,
                "Actor died, changed identity or changed owner.");
            return actor;
        }
        private void ObserveEscape(GameUnit* actor, int tick, bool here)
        {
            object current = bridge.Case("fireEscapes", scenario.ActorId);
            if (phase == 0 && current != null)
            {
                Require(sawHazard, "No genuine scanner witness before escape.");
                phase = 1; Emit("escape-start observed tick=" + tick + " current-tribe=" + actor->r_TribeId);
            }
            if (current != null)
            {
                if (Bridge.Value<bool>(current, "Safe")) sawSafe = true;
                if (Bridge.Value<bool>(current, "Returning")) sawReturning = true;
            }
            if (phase != 1 || current != null) return;
            Require(sawSafe && sawReturning && !here, "Escape ended without observed safe arrival and issued return; fixture may have expired or been reordered.");
            Require(Math.Abs(actor->r_CurrentTilePositionX - originX) <= 1 && Math.Abs(actor->r_CurrentTilePositionY - originY) <= 1,
                "Return order was not confirmed by actual arrival at/next to the original position.");
            RequireOriginalGroup(actor);
            Finish(tick, "escape-safe-return-arrival");
        }
        private void ObserveWait(GameUnit* actor, int tick, bool witness)
        {
            object current = bridge.Case("replanWaits", scenario.ActorId);
            if (phase == 0)
            {
                if (!witness) return;
                Require(!bridge.Hazard(scenario.Plague, originX, originY), "Wait actor starts in danger; use an approach corridor fixture.");
                Require(GameTribeManagerAPI.Instance.IssueMoveHereCommand(originalTribe, scenario.TargetX, scenario.TargetY, false, 1, TribeMoveType.DefaultInSync),
                    "Move command rejected; setup did not establish a wait case.");
                phase = 1; Emit("approach issued tick=" + tick + "; awaiting actual replan wait");
                return;
            }
            if (phase == 1 && current != null)
            { phase = 2; waitStarted = tick; waitUntil = Bridge.Value<int>(current, "Until"); Emit("wait-start observed tick=" + tick + " until=" + waitUntil); }
            if (phase != 2) return;
            int elapsed = unchecked(tick - waitStarted);
            if (!scenario.Unlimited)
            {
                if (bridge.Counter("replanAborted") <= abortedBefore) return;
                Require(unchecked(tick - waitUntil) >= 0, "Abort counter changed before this actor's own saved deadline.");
                Require(witness && current == null && !Walking(actor), "Bounded wait did not abort while the witnessed danger remained.");
                Require(actor->r_TargetTilePositionX2 != scenario.TargetX || actor->r_TargetTilePositionY2 != scenario.TargetY,
                    "Bounded abort retained the destination as an active movement target.");
                RequireOriginalGroup(actor);
                Finish(tick, "bounded-abort-with-danger");
                return;
            }
            if (!waitedPastDeadline)
            {
                if (elapsed < scenario.WaitSeconds * 40 + 16) return;
                Require(current != null && witness && bridge.Counter("replanAborted") == abortedBefore,
                    "Unlimited wait did not persist beyond finite deadline with real danger (or fixture cleared too early).");
                waitedPastDeadline = true; Emit("unlimited wait survived finite deadline tick=" + tick);
            }
            if (current == null && !Walking(actor) && actor->r_CurrentTilePositionX == scenario.TargetX && actor->r_CurrentTilePositionY == scenario.TargetY)
            { RequireOriginalGroup(actor); Finish(tick, "unlimited-safe-resume-arrival"); }
        }
        private void RequireOriginalGroup(GameUnit* actor)
        {
            Require(actor->r_TribeId == originalTribe && GameTribeManagerAPI.Instance.TryGetTribeById(originalTribe, out GameTribe* tribe) &&
                tribe != null && tribe->r_AliveState == AliveState.IsAlive && tribe->r_GlobalId == tribeGlobal && tribe->r_PlayerIdOwner == owner,
                "Actor did not retain/rejoin the live original group identity and owner.");
        }
        private static bool Walking(GameUnit* actor) => actor->r_PathPlanStateBitFlags != 0 &&
            (actor->r_CurrentPathPlanIndex < actor->r_PathPlanLength || actor->r_NextTilePositionX2 != actor->r_CurrentTilePositionX || actor->r_NextTilePositionY2 != actor->r_CurrentTilePositionY);
        private void Finish(int tick, string result)
        {
            Restore();
            Require(cleanup == "restored", "Gameplay completed but restoration not confirmed.");
            done = true; Emit("observed-result=" + result + " tick=" + tick + " cleanup=restored");
        }
        private void Restore()
        {
            if (bridge == null) { cleanup = "not-active"; return; }
            if (!bridge.OwnsLease) { cleanup = "superseded-options"; Emit("cleanup=" + cleanup + "; newer configuration retained"); return; }
            if (missions.Current?.SessionId != session) { cleanup = "superseded-mission"; Emit("cleanup=" + cleanup + "; old cases not applied to new mission"); return; }
            bridge.Restore(); cleanup = "restored";
            Emit("restored original options reference and saved case records on simulation callback");
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static string Unwrap(Exception error) => (error is TargetInvocationException invocation && invocation.InnerException != null ? invocation.InnerException : error).ToString();

        private sealed class Bridge
        {
            private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            private readonly object provider, original;
            private readonly FieldInfo settings;
            private readonly MethodInfo apply, capture, restore, resetWatch, resetEscapes;
            private readonly Dictionary<string, byte[]> originalCases;
            private readonly int originalOrderSequence;
            private object lease;
            internal bool OwnsLease => lease != null && ReferenceEquals(settings.GetValue(provider), lease);
            internal Bridge()
            {
                var assembly = Chainloader.PluginInfos["Pathfinding_Serp"].Instance.GetType().Assembly;
                var host = assembly.GetType("Pathfinding.Engine.EngineHost", throwOnError: true);
                provider = host.GetField("runtime", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) ?? throw new InvalidOperationException("Pathfinding provider unavailable.");
                Type type = provider.GetType();
                settings = type.GetField("settings", Instance) ?? throw new MissingFieldException(type.FullName, "settings");
                original = settings.GetValue(provider) ?? throw new InvalidOperationException("No engine options.");
                apply = Method(type, "ApplyMissionOptions", original.GetType());
                capture = Method(type, "CaptureMovementCases", typeof(Action<string, byte[]>));
                restore = Method(type, "RestoreMovementCases", typeof(IReadOnlyDictionary<string, byte[]>));
                resetWatch = Method(type, "ResetReplanWatch"); resetEscapes = Method(type, "ResetFireEscapes");
                foreach (string name in new[] { "fireEscapes", "replanWaits", "replanSolos" })
                    Require(Count(name) == 0, "Prepared save contains active movement cases; driver requires a clean isolated fixture.");
                Require((bool)Property(original, "EnablePathfindingCore").GetValue(original) && Value<bool>(provider, "anchorAvailable"), "Enable the base engine and provide a ready anchor core.");
                originalCases = Capture();
                originalOrderSequence = Value<int>(provider, "tribeOrderSequence");
            }
            internal void Activate(Scenario scenario)
            {
                lease = Clone(original);
                Set(lease, "AnchorSingleMoves", true); Set(lease, "AvoidFire", !scenario.Plague); Set(lease, "AvoidPlague", scenario.Plague);
                Set(lease, "EscapeFire", scenario.Escape && !scenario.Plague); Set(lease, "EscapePlague", scenario.Escape && scenario.Plague);
                Set(lease, "EscapePlagueRaised", false);
                Set(lease, "FireWaitIndefinitely", scenario.Unlimited && !scenario.Plague); Set(lease, "PlagueWaitIndefinitely", scenario.Unlimited && scenario.Plague);
                Set(lease, "FireWaitSeconds", scenario.WaitSeconds); Set(lease, "PlagueWaitSeconds", scenario.WaitSeconds);
                apply.Invoke(provider, new[] { lease });
                Require(OwnsLease, "Temporary option snapshot was not installed.");
                Require(Value<bool>(provider, scenario.Plague ? "plagueRulesActive" : "fireRulesActive"),
                    "Selected danger scanner/rule is not bound and active; inspect the mainmod binding diagnostics.");
            }
            internal long Counter(string name) => Value<long>(provider, name);
            internal object Case(string name, int id)
            {
                object dictionary = Field(name).GetValue(provider);
                return (bool)dictionary.GetType().GetMethod("ContainsKey").Invoke(dictionary, new object[] { id })
                    ? dictionary.GetType().GetProperty("Item").GetValue(dictionary, new object[] { id }) : null;
            }
            internal bool Hazard(bool plague, int x, int y)
            {
                if ((uint)x >= RouteTileRules.MapSize || (uint)y >= RouteTileRules.MapSize) return false;
                object tiles = Field(plague ? "plagueTiles" : "fireTiles").GetValue(provider);
                return (bool)tiles.GetType().GetMethod("Contains", Instance).Invoke(tiles, new object[] { y * RouteTileRules.MapSize + x });
            }
            internal void Restore()
            {
                if (!OwnsLease) return;
                Dictionary<string, byte[]> current = Capture();
                object finishing = Clone(lease);
                Set(finishing, "AnchorSingleMoves", false); Set(finishing, "AvoidFire", false); Set(finishing, "AvoidPlague", false);
                try
                {
                    apply.Invoke(provider, new[] { finishing });
                    resetWatch.Invoke(provider, null); resetEscapes.Invoke(provider, null);
                    restore.Invoke(provider, new object[] { current }); // Disabled cases end/rejoin rather than orphaning temporary groups.
                    resetWatch.Invoke(provider, null); resetEscapes.Invoke(provider, null);
                    Field("tribeOrderSequence").SetValue(provider, originalOrderSequence);
                }
                finally { apply.Invoke(provider, new[] { original }); }
                restore.Invoke(provider, new object[] { originalCases });
                Require(ReferenceEquals(settings.GetValue(provider), original), "Original option reference was not restored.");
                Dictionary<string, byte[]> after = Capture();
                Require(Equal(originalCases, after), "Original movement case bytes were not restored.");
                lease = null;
            }
            private int Count(string name)
            { object dictionary = Field(name).GetValue(provider); return (int)dictionary.GetType().GetProperty("Count").GetValue(dictionary); }
            private FieldInfo Field(string name) => provider.GetType().GetField(name, Instance) ?? throw new MissingFieldException(provider.GetType().FullName, name);
            private Dictionary<string, byte[]> Capture()
            {
                var values = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                capture.Invoke(provider, new object[] { new Action<string, byte[]>((name, bytes) => values.Add(name, (byte[])bytes.Clone())) });
                return values;
            }
            private static bool Equal(Dictionary<string, byte[]> a, Dictionary<string, byte[]> b)
            {
                if (a.Count != b.Count) return false;
                foreach (var item in a)
                {
                    if (!b.TryGetValue(item.Key, out byte[] bytes) || bytes.Length != item.Value.Length) return false;
                    for (int i = 0; i < bytes.Length; i++) if (bytes[i] != item.Value[i]) return false;
                }
                return true;
            }
            internal static T Value<T>(object target, string name) => (T)(target.GetType().GetField(name, Instance) ?? throw new MissingFieldException(target.GetType().FullName, name)).GetValue(target);
            private static PropertyInfo Property(object target, string name) => target.GetType().GetProperty(name, Instance) ?? throw new MissingMemberException(target.GetType().FullName, name);
            private static void Set(object target, string name, object value) => Property(target, name).SetValue(target, value);
            private static object Clone(object value) => typeof(object).GetMethod("MemberwiseClone", Instance).Invoke(value, null);
            private static MethodInfo Method(Type type, string name, params Type[] arguments) => type.GetMethod(name, Instance, null, arguments, null) ?? throw new MissingMethodException(type.FullName, name);
        }
    }
}
