using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using MultiplayerRestartTest;

// Executes the actual production Apply/LaunchGuard methods. Only the external send,
// log and failure-UI cleanup boundaries are intercepted; protocol decisions and the
// production iteration/finally code run unchanged. No Unity/Steam methods execute.
internal static class RuntimeFailureTests
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly List<ulong> attempted = new List<ulong>();
    private static int checks, cleanupCalls;
    private static Harmony patches;
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    private static MethodInfo Method(Type type, string name) => type.GetMethod(name, Any)
        ?? throw new MissingMethodException(type.FullName, name);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Any).SetValue(target, value);
    private static object Call(object target, string name, params object[] args)
    {
        try { return Method(target.GetType(), name).Invoke(target, args); }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
    }
    private static void FailedSend(ulong peer)
    {
        attempted.Add(peer);
        throw new InvalidOperationException("injected-disconnected-peer");
    }
    private static bool CleanupBoundary() { cleanupCalls++; return false; }
    private static bool NoLog() => false;

    private static RestartRuntime Seed(RestartProtocol protocol)
    {
        var runtime = (RestartRuntime)FormatterServices.GetUninitializedObject(typeof(RestartRuntime));
        var snapshot = (LaunchSnapshot)FormatterServices.GetUninitializedObject(typeof(LaunchSnapshot));
        var memberType = typeof(LaunchSnapshot).GetNestedType("Member", BindingFlags.NonPublic);
        if (memberType == null) throw new MissingMemberException("LaunchSnapshot member fixture contract");
        var members = Array.CreateInstance(memberType, 3);
        for (int i = 0; i < 3; i++)
        {
            object member = Activator.CreateInstance(memberType, true);
            Set(member, "Id", 1001UL + (ulong)i);
            Set(member, "Ai", false);
            members.SetValue(member, i);
        }
        Set(snapshot, "members", members);
        Set(runtime, "protocol", protocol);
        Set(runtime, "snapshot", snapshot);
        Set(runtime, "self", 1001UL);
        return runtime;
    }

    private static RestartProtocol Host()
    {
        var protocol = new RestartProtocol(1001, 1001, new ulong[] { 1001, 1002, 1003 }, "fixture-session", "fixture-digest", 10);
        protocol.Begin(Guid.NewGuid().ToString("N"), 0);
        return protocol;
    }

    private static void FailureCleanup(bool incomingAbort, bool committed)
    {
        var protocol = Host();
        if (committed)
        {
            protocol.Prepared(0);
            foreach (ulong peer in new ulong[] { 1002, 1003 })
                protocol.Receive(peer, new RestartPacket { Kind = RestartMessageKind.Ready,
                    Session = "fixture-session", Digest = "fixture-digest", Operation = protocol.Operation }, 0);
            Check(protocol.IsCommitted, "Failure fixture did not cross the real commitment barrier.");
        }
        RestartStep step;
        if (incomingAbort)
            step = protocol.Receive(1002, new RestartPacket { Kind = RestartMessageKind.Abort,
                Session = "fixture-session", Digest = "fixture-digest", Operation = protocol.Operation }, 1);
        else step = protocol.Tick(10);
        Check(protocol.IsTerminal && (step.Effects & RestartEffect.Cancelled) != 0,
            "Fixture did not reach the real terminal timeout/Abort path.");
        attempted.Clear(); cleanupCalls = 0;
        Call(Seed(protocol), "Apply", step);
        Check(attempted.Count == 2 && attempted[0] == 1002 && attempted[1] == 1003,
            "Abort broadcast stopped at the first failed peer, or attempted to send to self.");
        Check(cleanupCalls == 1,
            "Terminal timeout/Abort did not reach its cleanup boundary exactly once after send failure.");
    }

    private static void FailedTransitionCannotLaunch()
    {
        var protocol = Host();
        protocol.Prepared(0);
        foreach (ulong peer in new ulong[] { 1002, 1003 })
            protocol.Receive(peer, new RestartPacket { Kind = RestartMessageKind.Ready,
                Session = "fixture-session", Digest = "fixture-digest", Operation = protocol.Operation }, 0);
        Check(protocol.IsCommitted, "Fixture did not cross the stopped-world commitment boundary.");
        protocol.Abort("fixture-transition-failure");
        Check(protocol.IsTerminal, "Fixture did not retire the failed transition.");
        FieldInfo requested = typeof(StopBarrier).GetField("requested", BindingFlags.Static | BindingFlags.NonPublic);
        if (requested == null) throw new MissingFieldException("StopBarrier.requested fixture contract");
        object originalRequested = requested.GetValue(null);
        try
        {
            requested.SetValue(null, 1);
            string blocked = (string)Call(Seed(protocol), "LaunchGuard", new object[] { null });
            Check(!string.IsNullOrWhiteSpace(blocked),
                "The real launch guard admitted an unrelated new game while the failed old world remained parked.");
            requested.SetValue(null, 0);
            Check(Call(Seed(null), "LaunchGuard", new object[] { null }) == null,
                "Dormant testmod blocked an ordinary lobby launch.");
        }
        finally { requested.SetValue(null, originalRequested); }
    }

    internal static void Run()
    {
        patches = new Harmony("MultiplayerRestartTest.RuntimeFailureFixture");
        patches.Patch(Method(typeof(RestartRuntime), "Send"),
            prefix: new HarmonyMethod(Method(typeof(RuntimeFailureTests), nameof(FailedSend))));
        patches.Patch(Method(typeof(RestartRuntime), "Cancelled"),
            prefix: new HarmonyMethod(Method(typeof(RuntimeFailureTests), nameof(CleanupBoundary))));
        patches.Patch(Method(typeof(RestartRuntime), "Report"),
            prefix: new HarmonyMethod(Method(typeof(RuntimeFailureTests), nameof(NoLog))));
        foreach (bool committed in new[] { false, true })
        {
            FailureCleanup(false, committed);
            FailureCleanup(true, committed);
        }
        FailedTransitionCannotLaunch();
        Console.WriteLine("Actual runtime failure paths passed: " + checks + " checks; send and UI boundaries isolated.");
    }
}
