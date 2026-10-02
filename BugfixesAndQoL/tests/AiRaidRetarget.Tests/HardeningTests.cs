using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using BepInEx.Logging;
using BugfixesAndQoL;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop.Enums;
using static BugfixesAndQoL.RaidAttackFieldEvaluation;

internal static class HardeningTests
{
    private const BindingFlags Instance = BindingFlags.NonPublic | BindingFlags.Instance;
    private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;
    private static readonly Type Runtime = typeof(AiRaidRetargetFixRuntime);
    private static int assertions;
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); assertions++; }
    private static object Get(object runtime, string name) => Runtime.GetField(name, Instance).GetValue(runtime);
    private static void Set(object runtime, string name, object value) => Runtime.GetField(name, Instance).SetValue(runtime, value);
    private static object Call(object runtime, string name, params object[] args) =>
        Runtime.GetMethod(name, Instance).Invoke(runtime, args);
    private static object CallStatic(string name, object stack) => Runtime.GetMethod(name, Static).Invoke(null, new[] { stack });
    private static AiRaidRetargetFixRuntime Create(out List<string> lines)
    {
        var logger = new ManualLogSource("Raid hardening tests");
        var received = new List<string>(); logger.LogEvent += (sender, args) => received.Add(args.Data.ToString());
        lines = received;
        var runtime = new AiRaidRetargetFixRuntime(logger);
        ((RaidActivationState)Get(runtime, "activation")).Available = true;
        runtime.SetEnabled(true);
        return runtime;
    }
    private static void Start(AiRaidRetargetFixRuntime runtime) =>
        runtime.OnSessionStarted(new Shared.GameplaySessionStartedContext { SessionId = 100, IsReplay = true });
    private static void Complete(AiRaidRetargetFixRuntime runtime) => Call(runtime, "CompleteInitialization", null, null);
    private static TribeIssueOrderWithTargetEventArgs Pre(int tribe = 1, int target = 119) =>
        new TribeIssueOrderWithTargetEventArgs(EventHookPhase.Pre, tribe, TribeAICommand.AttackBuilding, target, 6450, 0);
    private static void Push(AiRaidRetargetFixRuntime runtime, TribeIssueOrderWithTargetEventArgs args)
    {
        bool neutral = args.TribeId <= 0;
        Call(runtime, "PushCommandFrame", 10, args, neutral ? 0u : 7u,
            new AttackCandidateSnapshot(!neutral, neutral ? -1 : 0, Array.Empty<CandidateRecord>()),
            neutral ? IntPtr.Zero : new IntPtr(123), neutral ? 0 : 1, neutral ? -1 : 0);
    }
    private static object Stack(AiRaidRetargetFixRuntime runtime) => ((IDictionary)Get(runtime, "pendingAttackCandidates"))[10];
    private static object Peek(AiRaidRetargetFixRuntime runtime) => CallStatic("PeekSearchFrame", Stack(runtime));
    private static object Pop(AiRaidRetargetFixRuntime runtime) => CallStatic("TakePostFrame", Stack(runtime));
    private static object Member(object frame, string field) => frame.GetType().GetField(field, Instance).GetValue(frame);
    private static int Depth(AiRaidRetargetFixRuntime runtime) => (int)Stack(runtime).GetType().GetProperty("Count").GetValue(Stack(runtime));
    private static object GroupKey(int player, int role, uint global = 7) =>
        Activator.CreateInstance(Runtime.GetNestedType("RaidGroupKey", BindingFlags.NonPublic), Instance, null,
            new object[] { player, role, player * 10 + role, global }, null);

    internal static void Run()
    {
        TestInitialization();
        TestFrames();
        TestExpiry();
        TestWaitingCallbacks();
        Console.WriteLine("PASS: " + assertions + " raid hardening assertions (publication, actual frame helpers, expiration and waiting callbacks).");
    }
    private static void TestInitialization()
    {
        var runtime = Create(out _); Start(runtime);
        Check(!(bool)Get(runtime, "initialized") && !(bool)Get(runtime, "active"), "Cached start must not activate unpublished runtime");
        Check(((RaidActivationState)Get(runtime, "activation")).SessionAllowed, "Cached start retains session permission for commit");
        Call(runtime, "FailInitialization"); Start(runtime);
        Check(!(bool)Get(runtime, "active") && !((RaidActivationState)Get(runtime, "activation")).Available, "Failed registration stays inactive on later start");
        ((RaidActivationState)Get(runtime, "activation")).Available = true;
        Complete(runtime);
        Check((bool)Get(runtime, "initialized") && (bool)Get(runtime, "active"), "Successful commit activates cached session");
        var disabled = Create(out _); Start(disabled); disabled.SetEnabled(false); Complete(disabled);
        Check(!(bool)Get(disabled, "active"), "Disable during registration respected at commit");
        disabled.SetEnabled(true); Check((bool)Get(disabled, "active"), "Reenable after commit works");
        disabled.OnSessionEnded(); Check(!(bool)Get(disabled, "active"), "End gates published runtime");
    }
    private static void TestFrames()
    {
        var runtime = Create(out _); Start(runtime); Complete(runtime); Set(runtime, "lastTick", 10);
        var outer = Pre(); Push(runtime, outer);
        var inner = Pre(target: 808); Push(runtime, inner);
        inner.SkipOriginalFunction = true; // Set by a subscriber AFTER this mod's Pre capture.
        Check(Object.ReferenceEquals(Member(Peek(runtime), "PreEvent"), outer) && Depth(runtime) == 1, "Skipped inner frame cannot steal outer search");
        Check(Object.ReferenceEquals(Member(Pop(runtime), "PreEvent"), outer) && Depth(runtime) == 0, "Outer Post consumes outer frame after skipped inner");
        var sameOuter = Pre(); Push(runtime, sameOuter); var sameInner = Pre(); Push(runtime, sameInner); sameInner.SkipOriginalFunction = true;
        Check(Object.ReferenceEquals(Member(Pop(runtime), "PreEvent"), sameOuter), "Identical canceled inner inputs still preserve outer sequence");
        var temporaryOuter = Pre(); temporaryOuter.SkipOriginalFunction = true; Push(runtime, temporaryOuter);
        var child = Pre(target: 5); Push(runtime, child);
        Check(Depth(runtime) == 2, "New Pre cannot discard temporarily skipped outer subscriber frame");
        Check(Object.ReferenceEquals(Member(Pop(runtime), "PreEvent"), child), "Child Post leaves outer publisher frame in place");
        temporaryOuter.SkipOriginalFunction = false;
        Check(Object.ReferenceEquals(Member(Peek(runtime), "PreEvent"), temporaryOuter), "Later subscriber can restore outer original execution");
        Pop(runtime);
        var invalidOuter = Pre(); Push(runtime, invalidOuter); var invalid = Pre(tribe: 0); Push(runtime, invalid);
        var invalidFrame = Pop(runtime);
        Check(Object.ReferenceEquals(Member(invalidFrame, "PreEvent"), invalid) && !(bool)((AttackCandidateSnapshot)Member(invalidFrame, "Snapshot")).Available,
            "Invalid tribe gets neutral frame and does not consume outer Post");
        Check(((RaidSearchEvidence)Member(invalidFrame, "Evidence")).Context == IntPtr.Zero, "Neutral frame has no native context");
        Check(Object.ReferenceEquals(Member(Pop(runtime), "PreEvent"), invalidOuter), "Outer frame remains after invalid nested command");
        var missingPost = Pre(); Push(runtime, missingPost); Set(runtime, "lastTick", 11); var next = Pre(target: 44); Push(runtime, next);
        Check(Depth(runtime) == 1 && Object.ReferenceEquals(Member(Pop(runtime), "PreEvent"), next), "Non-skipped missing Post invalidated at next tick");
        var skipped = Pre(); Push(runtime, skipped); skipped.SkipOriginalFunction = true;
        Check(Peek(runtime) == null && Pop(runtime) == null && Depth(runtime) == 0, "All skipped frames remain unproven, never guessed");
        var nestedOuter = Pre(); Push(runtime, nestedOuter); var nestedInner = Pre(target: 808); Push(runtime, nestedInner);
        var first = (RaidSearchEvidence)Member(Peek(runtime), "Evidence"); Pop(runtime);
        var second = (RaidSearchEvidence)Member(Peek(runtime), "Evidence"); Pop(runtime);
        Check(first.Sequence != second.Sequence && first.BuildingId == 808 && second.BuildingId == 119, "Normal nested calls retain distinct sequences and targets");
    }
    private static void TestExpiry()
    {
        var runtime = Create(out _); Start(runtime); Complete(runtime); var key = GroupKey(1, 0);
        Set(runtime, "lastTick", 0); Call(runtime, "RememberRejectedTarget", key, 119, 6450u);
        Set(runtime, "lastTick", 299);
        Check((bool)Call(runtime, "IsRejectedTarget", key, 119, 6450u), "Reject active until tick 299");
        Check(!(bool)Call(runtime, "IsRejectedTarget", key, 119, 6451u), "Recycled building ID not rejected");
        Check(!(bool)Call(runtime, "IsRejectedTarget", GroupKey(1, 0, 8), 119, 6450u), "Recycled group ID not rejected");
        Set(runtime, "lastTick", 300);
        Check(!(bool)Call(runtime, "IsRejectedTarget", key, 119, 6450u), "Tick 300 expiry exact");
        Call(runtime, "PruneExpiredRejectedTargets");
        Check(((IDictionary)Get(runtime, "rejectedRaidTargets")).Count == 0, "Expired last entry removes group map");
        Set(runtime, "lastTick", 301); Call(runtime, "RememberRejectedTarget", key, 119, 6450u);
        Call(runtime, "PruneExpiredRejectedTargets");
        Check((long)Get(runtime, "nextRejectionCleanupTick") == 500, "Cleanup not repeated before 200-tick interval");
        int maximum = 0;
        for (int tick = 1000; tick <= 21000; tick += 50)
        {
            Set(runtime, "lastTick", tick);
            for (int player = 1; player <= 8; player++) for (int role = 0; role < 6; role++)
                Call(runtime, "RememberRejectedTarget", GroupKey(player, role), tick / 50 % 100 + 1, (uint)tick);
            Call(runtime, "PruneExpiredRejectedTargets");
            int retained = 0; foreach (IDictionary values in ((IDictionary)Get(runtime, "rejectedRaidTargets")).Values) retained += values.Count;
            maximum = Math.Max(maximum, retained);
        }
        Check(maximum <= 480, "Long stream with eight players / six roles bounds retained expired identities");
        runtime.OnTick(5); // Backward tick clears maps before native role lookups, so this is native-memory-free.
        Check(((IDictionary)Get(runtime, "rejectedRaidTargets")).Count == 0 && (int)Get(runtime, "lastTick") == 5, "Backward tick clears identities and restarts cleanup");
        Check((long)Get(runtime, "nextRejectionCleanupTick") == 205, "Cleanup schedule reset after tick rollback");
        Check(!GroupKey(1, 0).Equals(GroupKey(1, 1)) && !GroupKey(1, 0).Equals(GroupKey(2, 0)), "Player/role independently identify groups");
    }
    private static void TestWaitingCallbacks()
    {
        foreach (bool search in new[] { true, false })
        {
            var runtime = Create(out var lines); Start(runtime); Complete(runtime);
            object gate = Get(runtime, "attackCaptureLock"); Exception error = null;
            var thread = new Thread(() => { try { if (search) runtime.OnSearchObserved(0, 0, IntPtr.Zero); else runtime.OnTribeOrder(null); } catch (Exception ex) { error = ex; } });
            Monitor.Enter(gate);
            try
            {
                thread.Start();
                Check(SpinWait.SpinUntil(() => (thread.ThreadState & ThreadState.WaitSleepJoin) != 0, 2000), "Callback waits at runtime lock");
                runtime.SetEnabled(false);
            }
            finally { Monitor.Exit(gate); }
            Check(thread.Join(2000) && error == null && lines.Count == 0, "Disabled waiting callback neither evaluates native data nor logs stale warning");
            Check(((IDictionary)Get(runtime, "pendingAttackCandidates")).Count == 0, "Disabled callback cannot recreate a command frame");
        }
    }
}
