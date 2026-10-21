using Shared;
using BepInEx.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

[TestClass]
public sealed class RuntimeHealthTests
{
    [TestMethod]
    public void IndependentFallbackLoggersReceiveTheirOwnFailures()
    {
        using (var first = new ManualLogSource("first"))
        using (var second = new ManualLogSource("second"))
        {
            int a = 0, b = 0;
            first.LogEvent += (_, args) => a++;
            second.LogEvent += (_, args) => b++;
            RuntimeHealth.Failure(first, "same-feature", true, "same failure");
            RuntimeHealth.Failure(second, "same-feature", true, "same failure");
            RuntimeHealth.Failure(first, "same-feature", true, "same failure");
            Assert.AreEqual(1, a); Assert.AreEqual(1, b);
        }
    }
    [TestMethod]
    public void ConstructionIsSilentAndFailuresRetainSeverityAndContext()
    {
        var messages = new List<(LogLevel, string)>();
        var health = new RuntimeHealthLedger("mod", (level, text) => messages.Add((level, text)));
        Assert.AreEqual(0, messages.Count);
        health.Report("apply", true, "setter failed; Vanilla retained");
        health.Report("optional", false, "requested capability unavailable; feature inactive");
        Assert.AreEqual(2, messages.Count);
        Assert.AreEqual(LogLevel.Error, messages[0].Item1); Assert.AreEqual(LogLevel.Warning, messages[1].Item1);
        Assert.IsTrue(messages.All(m => m.Item2.Contains("mod=mod; feature=")));
        Assert.IsTrue(messages.All(m => System.Text.RegularExpressions.Regex.IsMatch(m.Item2,
            @"^\[\d{4}-\d\d-\d\d \d\d:\d\d:\d\d\.\d{3}\] MOD_FAILURE")));
    }
    [TestMethod]
    public void NewCausesAndIndependentFunctionsHaveNoReportQuota()
    {
        int reports = 0;
        var health = new RuntimeHealthLedger("mod", (_, text) => reports++, () => 0, 1);
        for (int i = 0; i < 1000; i++) health.Report("same-feature", true, "different cause " + i);
        for (int i = 0; i < 300; i++) health.Report("different-feature-" + i, true, "same cause");
        Assert.AreEqual(1300, reports);
    }
    [TestMethod]
    public void OnlyIdenticalRepeatsAreSuppressedForThirtySeconds()
    {
        long now = 0;
        var messages = new List<string>();
        var health = new RuntimeHealthLedger("mod", (_, text) => messages.Add(text), () => now, 100);
        health.Report("apply", true, "failure");
        now = 1; health.Report("apply", true, "failure");
        now = 2999; health.Report("apply", true, "failure");
        Assert.AreEqual(1, messages.Count);
        now = 3000; health.Report("apply", true, "failure");
        Assert.AreEqual(2, messages.Count);
        Assert.IsTrue(messages[1].Contains("suppressed repeats=2"));
        health.Report("apply", false, "failure");
        health.Report("independent", true, "failure");
        health.Report("apply", true, "new failure");
        Assert.AreEqual(5, messages.Count);
        now = 6000; health.Report("apply", true, "failure");
        Assert.AreEqual(6, messages.Count);
        Assert.IsFalse(messages.Last().Contains("suppressed repeats="));
    }
    [TestMethod]
    public void FullCacheEvictsOldIdentitiesAndAdmitsNewFailures()
    {
        int reports = 0;
        var health = new RuntimeHealthLedger("mod", (_, text) => reports++, () => 0, 1);
        for (int i = 0; i < 5000; i++) health.Report("feature-" + i, true, "failure");
        Assert.AreEqual(5000, reports); Assert.AreEqual(4096, health.Count);
        health.Report("feature-0", true, "failure");
        Assert.AreEqual(5001, reports); Assert.AreEqual(4096, health.Count);
    }
    [TestMethod]
    public void LeastRecentlyUsedEvictionPreservesFrequentlyRepeatedFailure()
    {
        int reports = 0;
        var health = new RuntimeHealthLedger("mod", (_, text) => reports++, () => 0, 1, 2);
        health.Report("A", true, "failure"); health.Report("B", true, "failure");
        health.Report("A", true, "failure"); health.Report("C", true, "failure");
        health.Report("A", true, "failure");
        Assert.AreEqual(3, reports);
        health.Report("B", true, "failure"); Assert.AreEqual(4, reports);
    }
    [TestMethod]
    public void TimestampChangesAreRepeatsButTruncatedDifferentCausesRemainIndependent()
    {
        int reports = 0;
        var health = new RuntimeHealthLedger("mod", (_, text) => reports++, () => 0, 1);
        health.Report("apply", true, "[2026-10-18 20:00:00.001] failure");
        health.Report("apply", true, "[2026-10-18 20:00:01.002] failure");
        Assert.AreEqual(1, reports);
        string prefix = new string('a', 5000);
        health.Report("apply", true, prefix + "first"); health.Report("apply", true, prefix + "second");
        Assert.AreEqual(3, reports);
    }
    [TestMethod]
    public void ForwardedReportsAreNotThrottledAgain()
    {
        using (var source = new ManualLogSource("proxy-fixture"))
        {
            var messages = new List<string>();
            source.LogEvent += (_, args) => messages.Add(args.Data.ToString());
            var proxy = DebugLogHelper.ForExternalCode(source);
            long now = 0;
            var health = new RuntimeHealthLedger("mod", (level, text) => proxy.Log(level, text), () => now, 1);
            health.Report("A", true, "failure"); health.Report("A", true, "failure");
            now = 30; health.Report("A", true, "failure");
            health.Report("B", true, "failure"); health.Report("B", false, "failure");
            Assert.AreEqual(4, messages.Count);
            Assert.IsTrue(messages[1].Contains("suppressed repeats=1"));
            Assert.IsTrue(messages[2].Contains("feature=B;"));
        }
    }
    [TestMethod]
    public void ExistingLoggingApiSeparatesActualCallerFunctions()
    {
        using (var source = new ManualLogSource("caller-fixture"))
        {
            var messages = new List<string>(); source.LogEvent += (_, args) => messages.Add(args.Data.ToString());
            ReportA(source); ReportB(source); ReportA(source);
            Assert.AreEqual(2, messages.Count);
            Assert.IsTrue(messages[0].Contains(nameof(ReportA)));
            Assert.IsTrue(messages[1].Contains(nameof(ReportB)));
        }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ReportA(ManualLogSource log) { DebugLogHelper.LogError(log, "same failure"); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ReportB(ManualLogSource log) { DebugLogHelper.LogError(log, "same failure"); }
    [TestMethod]
    public void ThrowingListenerCannotInterruptIndependentFailures()
    {
        int attempts = 0;
        var health = new RuntimeHealthLedger("mod", (_, text) => { attempts++; throw new InvalidOperationException("listener"); });
        health.Report("apply", true, "required failure"); health.Report("apply", true, "required failure");
        health.Report("cleanup", true, "independent failure"); Assert.AreEqual(2, attempts);
    }

    [TestMethod]
    public void CauseCacheUsesCompleteNormalizedTextAndHasIndependentBounds()
    {
        var messages = new List<string>();
        var health = new RuntimeHealthLedger("cache", (_, text) => messages.Add(text), () => 0, 1);
        health.Report("A", true, "[2026-10-21 10:00:00.001] failure");
        health.Report("A", true, "[2026-10-21 10:00:01.002] failure");
        health.Report("B", false, "[2026-10-21 10:00:02.003] failure");
        Assert.AreEqual(1L, health.CauseHashComputations, "Features and severity share only the hash, never their suppression state.");
        Assert.AreEqual(2, messages.Count);
        for (int i = 0; i < 100; i++) health.Report("A", true, "cause-" + i);
        Assert.AreEqual(64, health.CachedCauseCount);
        long before = health.CauseHashComputations;
        health.Report("A", true, "[2026-10-21 10:00:03.004] failure");
        Assert.AreEqual(before + 1, health.CauseHashComputations, "Eviction permits rehashing without losing throttle state.");
        Assert.AreEqual(102, messages.Count, "Cache eviction does not emit a suppressed repeat.");
        string prefix = new string('x', 5000);
        before = health.CauseHashComputations;
        health.Report("long", true, prefix + "first");
        health.Report("long", true, prefix + "second");
        health.Report("long", true, prefix + "first");
        Assert.AreEqual(before + 3, health.CauseHashComputations, "Long causes are hashed completely and never retained in the small cache.");
        Assert.AreEqual(104, messages.Count);
        Assert.AreEqual(64, health.CachedCauseCount);
    }

    [TestMethod]
    public void ConcurrentCauseCacheKeepsExactSuppressionCounts()
    {
        long now = 0;
        var messages = new List<string>();
        var health = new RuntimeHealthLedger("parallel", (_, text) => { lock (messages) messages.Add(text); },
            () => System.Threading.Interlocked.Read(ref now), 1);
        System.Threading.Tasks.Parallel.For(0, 1000, _ => health.Report("same", true, "failure"));
        Assert.AreEqual(1, messages.Count);
        Assert.AreEqual(1, health.CachedCauseCount);
        now = 30;
        health.Report("same", true, "failure");
        Assert.AreEqual(2, messages.Count);
        StringAssert.Contains(messages[1], "suppressed repeats=999");
    }
}
