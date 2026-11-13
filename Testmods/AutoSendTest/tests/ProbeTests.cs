using System;
using AutoSendTest;

internal static class ProbeTests
{
    private static int checks;
    internal static int Run()
    {
        checks = 0;
        NoTransfersAreUntested(); BaseInvariants(); FairExamples(); UnknownVetoAndPartial();
        EmergencyExamples(); IncompleteAndInvalid(); SnapshotIsolationAndChanges(); ExternalRoundChanges(); DurationWrap();
        return checks;
    }
    private static AutoSendProbe Begin(int own, int reserve, int[] minima, int[] maxima, int[] stocks, int[] capacities,
        bool[] eligible, int batch = 100)
    {
        var probe = new AutoSendProbe(); probe.Start(1, 41, 0);
        Check(probe.BeginRound(40, reserve, own, minima, maxima, stocks, capacities, eligible, batch));
        return probe;
    }
    private static int[] Values(int value = 0)
    { var values = new int[9]; for (int index = 0; index < values.Length; index++) values[index] = value; return values; }
    private static bool[] Targets(params int[] recipients)
    { var values = new bool[9]; foreach (int recipient in recipients) values[recipient] = true; return values; }
    private static AutoSendProbeSummary End(AutoSendProbe probe)
    { probe.EndRound(); return probe.Finish("test"); }
    private static void NoTransfersAreUntested()
    {
        var probe = new AutoSendProbe(); probe.Start(1, 41, 0);
        var result = probe.Finish("no round"); Check(result.Status == "UNTESTED"); Check(result.Transfers == 0);
        Check(result.FairStatus == "UNTESTED" && result.EmergencyStatus == "UNTESTED");
        Check(result.ToString().Contains("fair=UNTESTED(cases=0"));
        probe = Begin(0, 100, Values(), Values(100), Values(), Values(100), Targets(2, 3));
        result = End(probe); Check(result.Status == "UNTESTED"); Check(result.Rounds == 1);
    }
    private static void BaseInvariants()
    {
        var stocks = Values(); stocks[2] = 10;
        var probe = Begin(150, 100, Values(), Values(100), stocks, Values(100), Targets(2));
        probe.ObserveTransfer(2, 50, true, true, 50, 50, 150, 10);
        var result = End(probe); Check(result.Status == "PASS"); Check(result.Transfers == 1 && result.Units == 50);
        Check(result.BalanceViolations + result.ReserveViolations + result.CeilingViolations + result.BatchViolations == 0);
        Check(result.FairStatus == "UNTESTED" && result.EmergencyStatus == "UNTESTED");

        stocks = Values(); stocks[2] = 4;
        probe = Begin(100, 90, Values(), Values(5), stocks, Values(100), Targets(2), 3);
        probe.ObserveTransfer(2, 4, true, true, 12, 12, 100, 4);
        result = End(probe); Check(result.Status == "FAIL");
        Check(result.ReserveViolations == 1 && result.CeilingViolations == 1 && result.BatchViolations == 1);
        probe = Begin(100, 0, Values(), Values(100), Values(), Values(100), Targets(2));
        probe.ObserveTransfer(2, 50, true, true, 50, 40, 100, 0);
        result = End(probe); Check(result.BalanceViolations == 1 && result.Status == "FAIL");
        probe = Begin(100, 0, Values(), Values(100), Values(), Values(100), Targets());
        probe.ObserveTransfer(2, 25, true, true, 25, 25, 100, 0);
        result = End(probe); Check(result.EligibilityViolations == 1 && result.Status == "FAIL");
        probe = Begin(300, 0, Values(), Values(500), Values(), Values(500), Targets(2));
        probe.ObserveTransfer(2, 60, true, true, 60, 60, 300, 0);
        probe.ObserveTransfer(2, 60, true, true, 60, 60, 240, 60);
        result = End(probe); Check(result.BatchViolations == 1 && result.Status == "FAIL");
    }
    private static void FairExamples()
    {
        var probe = Begin(101, 0, Values(), Values(200), Values(), Values(200), Targets(2, 3, 4));
        probe.ObserveTransfer(2, 34, true, true, 34, 34, 101, 0);
        probe.ObserveTransfer(3, 34, true, true, 34, 34, 67, 0);
        probe.ObserveTransfer(4, 33, true, true, 33, 33, 33, 0);
        var result = End(probe); Check(result.FairCases == 1 && result.FairViolations == 0 && result.FairStatus == "PASS");
        probe = Begin(90, 0, Values(), Values(200), Values(), Values(200), Targets(2, 3, 4));
        probe.ObserveTransfer(2, 60, true, true, 60, 60, 90, 0);
        probe.ObserveTransfer(3, 30, true, true, 30, 30, 30, 0);
        result = End(probe); Check(result.Status == "FAIL" && result.FairViolations == 1 && result.FairCases == 1);
        Check(result.BaseStatus == "PASS");
        var capacity = Values(200); capacity[4] = 0;
        probe = Begin(101, 0, Values(), Values(200), Values(), capacity, Targets(2, 3, 4));
        probe.ObserveTransfer(2, 50, true, true, 50, 50, 101, 0);
        probe.ObserveTransfer(3, 51, true, true, 51, 51, 51, 0);
        result = End(probe); Check(result.FairCases == 1 && result.FairViolations == 0);
        capacity = Values(200); capacity[2] = 10;
        probe = Begin(101, 0, Values(), Values(200), Values(), capacity, Targets(2, 3));
        probe.ObserveTransfer(2, 10, true, true, 10, 10, 101, 0);
        probe.ObserveTransfer(3, 91, true, true, 91, 91, 91, 0);
        result = End(probe); Check(result.FairCases == 0 && result.FairStatus == "UNTESTED");
    }
    private static void UnknownVetoAndPartial()
    {
        var probe = Begin(100, 0, Values(), Values(200), Values(), Values(200), Targets(2, 3));
        probe.ObserveTransfer(2, 50, true, false, null, null, 100, 0);
        var result = End(probe); Check(result.Unknown == 1 && result.Status == "INCONCLUSIVE");
        Check(result.FairInconclusive == 1 && result.FairViolations == 0 && result.FairCases == 0);
        probe = Begin(100, 0, Values(), Values(200), Values(), Values(200), Targets(2, 3));
        probe.ObserveTransfer(2, 50, false, true, 0, 0, 100, 0);
        probe.ObserveTransfer(3, 50, true, true, 50, 50, 100, 0);
        result = End(probe); Check(result.Vetoed == 1 && result.Transfers == 1 && result.Status == "PASS");
        Check(result.FairInconclusive == 1 && result.FairViolations == 0);
        probe = Begin(100, 0, Values(), Values(200), Values(), Values(200), Targets(2, 3));
        probe.ObserveTransfer(2, 50, true, true, 25, 25, 100, 0);
        probe.ObserveTransfer(3, 50, true, true, 50, 50, 75, 0);
        result = End(probe); Check(result.Status == "PASS" && result.Transfers == 2 && result.Units == 75);
        Check(result.FairInconclusive == 1 && result.FairViolations == 0 && result.BalanceViolations == 0);
        probe = Begin(100, 0, Values(), Values(200), Values(), Values(200), Targets(2));
        probe.ObserveTransfer(2, 50, true, true, 0, 0, 100, 0);
        result = End(probe); Check(result.Status == "UNTESTED" && result.BalanceViolations == 0);
        probe = Begin(100, 0, Values(), Values(200), Values(), Values(200), Targets(2));
        probe.ObserveTransfer(2, 101, true, false, null, null, 100, 0);
        result = End(probe); Check(result.Status == "FAIL" && result.Unknown == 1 && result.BatchViolations == 1);
    }
    private static void EmergencyExamples()
    {
        var minimums = Values(); minimums[2] = 20;
        var probe = Begin(20, 0, minimums, Values(100), Values(), Values(100), Targets(2, 3));
        probe.ObserveTransfer(3, 20, true, true, 20, 20, 20, 0);
        var result = End(probe); Check(result.EmergencyCases == 1 && result.EmergencyViolations == 1 && result.Status == "FAIL");
        probe = Begin(30, 0, minimums, Values(100), Values(), Values(100), Targets(2, 3));
        probe.ObserveTransfer(2, 20, true, true, 20, 20, 30, 0);
        probe.ObserveTransfer(3, 10, true, true, 10, 10, 10, 0);
        result = End(probe); Check(result.EmergencyStatus == "PASS" && result.EmergencyCases == 1);
        minimums[2] = 100; var capacity = Values(100); capacity[2] = 10;
        probe = Begin(60, 0, minimums, Values(200), Values(), capacity, Targets(2, 3));
        probe.ObserveTransfer(2, 10, true, true, 10, 10, 60, 0);
        probe.ObserveTransfer(3, 50, true, true, 50, 50, 50, 0);
        result = End(probe); Check(result.EmergencyStatus == "PASS" && result.EmergencyViolations == 0);
        probe = Begin(60, 0, minimums, Values(200), Values(), capacity, Targets(2, 3));
        probe.ObserveTransfer(2, 10, false, true, 0, 0, 60, 0);
        probe.ObserveTransfer(3, 50, true, true, 50, 50, 60, 0);
        result = End(probe); Check(result.EmergencyStatus == "INCONCLUSIVE" && result.EmergencyViolations == 0);
    }
    private static void IncompleteAndInvalid()
    {
        var probe = Begin(100, 0, Values(), Values(200), Values(), Values(200), Targets(2, 3));
        var result = probe.Finish("interrupted");
        Check(result.IncompleteRounds == 1 && result.FairInconclusive == 1 && result.Status == "INCONCLUSIVE");
        probe = new AutoSendProbe(); probe.Start(1, 41, 0);
        Check(!probe.BeginRound(40, 0, 100, new int[8], Values(100), Values(), Values(100), Targets(2), 100));
        result = probe.Finish("invalid"); Check(result.InvalidRounds == 1 && result.Status == "INCONCLUSIVE");
        probe = Begin(100, 0, Values(), Values(100), Values(), Values(100), Targets(2));
        probe.ObserveTransfer(9, 10, true, true, 10, 10, 100, 0);
        result = End(probe); Check(result.InvalidRounds == 1 && result.Status == "INCONCLUSIVE");
        bool rejected = false;
        try { new AutoSendProbe().Start(0, 41, 0); } catch (ArgumentException) { rejected = true; }
        Check(rejected);
    }
    private static void SnapshotIsolationAndChanges()
    {
        var maxima = Values(200); var eligible = Targets(2);
        var probe = Begin(100, 0, Values(), maxima, Values(), Values(200), eligible);
        maxima[2] = 0; eligible[2] = false;
        probe.ObserveTransfer(2, 50, true, true, 50, 50, 100, 0);
        var result = End(probe); Check(result.Status == "PASS" && result.EligibilityViolations == 0 && result.CeilingViolations == 0);
        probe = Begin(100, 0, Values(), Values(200), Values(), Values(200), Targets(2, 3));
        probe.ObserveTransfer(2, 50, true, true, 50, 50, 120, 0);
        probe.ObserveTransfer(3, 50, true, true, 50, 50, 70, 0);
        result = End(probe); Check(result.FairStatus == "INCONCLUSIVE" && result.FairViolations == 0);
    }
    private static void DurationWrap()
    {
        var probe = new AutoSendProbe(); int start = int.MaxValue - 100; probe.Start(1, 41, start);
        Check(!probe.HasElapsed(unchecked(start + 2399))); Check(probe.HasElapsed(unchecked(start + 2400)));
        Check(!probe.HasElapsed(start - 1)); Check(!probe.HasElapsed(start, 0));
        probe.Finish("stop"); Check(!probe.HasElapsed(unchecked(start + 2400)));
    }
    private static void ExternalRoundChanges()
    {
        var minimums = Values(); minimums[2] = 20;
        var probe = Begin(20, 0, minimums, Values(100), Values(), Values(100), Targets(2, 3));
        probe.ObserveTransfer(3, 20, true, true, 20, 20, 20, 0);
        // Another donor filled player two. The runtime detects that post-round
        // drift in the actual stock, including recipients we did not touch.
        probe.InvalidateRound();
        var result = End(probe);
        Check(result.EmergencyViolations == 0 && result.EmergencyCases == 0 && result.EmergencyInconclusive == 1);
        Check(result.BaseStatus == "PASS");
        probe = Begin(90, 0, Values(), Values(200), Values(), Values(200), Targets(2, 3, 4));
        probe.ObserveTransfer(2, 60, true, true, 60, 60, 90, 0);
        probe.ObserveTransfer(3, 30, true, true, 30, 30, 30, 0);
        probe.InvalidateRound(); result = End(probe);
        Check(result.FairViolations == 0 && result.FairCases == 0 && result.FairInconclusive == 1);
        probe = Begin(100, 0, Values(), Values(200), Values(), Values(200), Targets(2, 3));
        probe.ObserveTransfer(2, 50, true, true, 50, 40, 100, 0);
        probe.InvalidateRound(); result = End(probe);
        Check(result.Status == "FAIL" && result.BalanceViolations == 1);
    }
    private static void Check(bool condition)
    { checks++; if (!condition) throw new InvalidOperationException("Probe regression " + checks + " failed."); }
}
