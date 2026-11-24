using System;
using BugfixesAndQoL.AutoSend;

internal static class PaintTests
{
    private static int checks;
    private static void Check(bool value)
    { checks++; if (!value) throw new Exception("Paint regression " + checks + " failed."); }

    private static AutoSendDisplay Snapshot() => new AutoSendDisplay {
        SessionId = 7, SenderPlayerId = 1, Ready = true, SenderReady = true,
        HasDesiredValues = true, Reserve = 50, DesiredReserve = 60, Feedback = string.Empty,
        Rule = new AutoSendRule(10, 100, true, 30, true, 17),
        DesiredRule = new AutoSendRule(15, 120, true, 40, true, 23)
    };
    internal static int Run()
    {
        var baseline = Snapshot();
        Check(baseline.HasSamePaint(baseline));
        Check(baseline.HasSamePaint(Snapshot()));
        Check(!baseline.HasSamePaint(null));
        var telemetry = Snapshot();
        telemetry.SourceStock = 999; telemetry.RecipientStock = 888;
        telemetry.Capacity = 777; telemetry.ActiveRules = 12;
        telemetry.Rule = new AutoSendRule(10, 100, true, 30, true, int.MinValue);
        telemetry.DesiredRule = new AutoSendRule(15, 120, true, 40, false, 0);
        Check(baseline.HasSamePaint(telemetry) && telemetry.HasSamePaint(baseline));
        // Each displayed field independently invalidates paint. This exercises
        // the production comparison, rather than duplicating its expression.
        Action<AutoSendDisplay>[] changes = {
            d => d.SessionId++, d => d.SenderPlayerId++, d => d.Ready = false,
            d => d.SenderReady = false, d => d.Pending = true,
            d => d.IdentityMismatch = true, d => d.TransferUnavailable = true,
            d => d.ChatMessages = true, d => d.ChatSuppressionUnavailable = true,
            d => d.HasDesiredValues = false, d => d.Reserve++, d => d.DesiredReserve++,
            d => d.Feedback = "rejected",
            d => d.Rule = new AutoSendRule(11, 100, true, 30),
            d => d.Rule = new AutoSendRule(10, 101, true, 30),
            d => d.Rule = new AutoSendRule(10, 100, false, 30),
            d => d.Rule = new AutoSendRule(10, 100, true, 31),
            d => d.DesiredRule = new AutoSendRule(16, 120, true, 40),
            d => d.DesiredRule = new AutoSendRule(15, 121, true, 40),
            d => d.DesiredRule = new AutoSendRule(15, 120, false, 40),
            d => d.DesiredRule = new AutoSendRule(15, 120, true, 41)
        };
        foreach (var change in changes)
        {
            var changed = Snapshot(); change(changed);
            Check(!baseline.HasSamePaint(changed)); Check(!changed.HasSamePaint(baseline));
        }
        return checks;
    }
}
