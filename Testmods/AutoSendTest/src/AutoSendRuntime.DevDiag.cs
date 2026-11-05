using System;
using System.Collections.Concurrent;
using APIShared.Economy;
using APIShared.Presentation;
using Noesis;
using Shared;

namespace AutoSendTest
{
    // Compiled only by /devdiag. All observations stay local and never enter the
    // synchronized rules, save record, transport or allocation decisions.
    internal sealed partial class AutoSendRuntime
    {
        private readonly AutoSendProbe probe = new AutoSendProbe();
        private readonly ConcurrentQueue<string> probeMessages = new ConcurrentQueue<string>();
        private readonly int[] probeMin = new int[9], probeMax = new int[9], probeStock = new int[9], probeFree = new int[9], probeCredits = new int[9];
        private readonly bool[] probeEligible = new bool[9];
        private bool probeRequested, probeRound, probePaused, probeReadinessReported;
        private long probeSession;
        private int probeSender, probeGood, probeSourceBefore, probeRecipientBefore, probeObserved, probeRounds, probeDropped, probeDetailLines;
        private int probeRoundSource;
        private long probeRoundDebit;
        private string probeStatus = "ProbeIdle", probeSummary = string.Empty;

        partial void InitializeDiagnostics() => DevDiag.Attach(log);
        internal void ToggleProbe(AllyGoodsPanelContext context)
        {
            var display = ReadDisplay(context);
            lock (sync)
            {
                if (probe.Active) FinishProbe("user-stop");
                else if (probeRequested) { probeRequested = false; probeStatus = "ProbeIdle"; }
                else if (display.Ready && !display.Pending && context.Good.HasValue && mission != null)
                {
                    probeSession = mission.SessionId; probeSender = context.LocalPlayerId; probeGood = (int)context.Good.Value;
                    probeRequested = true; probeStatus = "ProbeWaiting"; probeSummary = string.Empty;
                }
            }
            RequestPresentation();
        }
        internal string ProbeStatus(out bool running)
        {
            lock (sync)
            {
                running = probe.Active || probeRequested;
                return AutoSendText.Get(probeStatus).Replace("{Count}", probeObserved.ToString()) +
                    (string.IsNullOrEmpty(probeSummary) ? "" : " " + probeSummary);
            }
        }
        partial void EndDiagnostics()
        {
            probeRequested = false;
            probeReadinessReported = false;
            if (probe.Active) FinishProbe("mission-ended");
        }
        partial void TickDiagnostics(int tick)
        {
            if (!probeReadinessReported && initializedMap && !identityMismatch && epochs.IsReady(expectedMask))
            {
                probeReadinessReported = true;
                ProbeLog("test:session-ready sender=" + localPlayer + " roster=" + expectedMask + " tick=" + tick);
            }
            if (probeRequested)
            {
                probeRequested = false;
                if (mission == null || mission.SessionId != probeSession || identityMismatch || !epochs.IsReady(expectedMask))
                { probeStatus = "ProbeUnavailable"; return; }
                probe.Start(probeSender, probeGood, tick);
                probeObserved = probeRounds = probeDropped = probeDetailLines = 0; probeStatus = "ProbeRunning";
                ProbeLog("test:start sender=" + probeSender + " good=" + probeGood + " tick=" + tick + " durationTicks=2400 source=actual-automatic-transfers");
                RequestPresentation();
            }
            if (probe.Active && probe.HasElapsed(tick)) FinishProbe("duration");
        }
        partial void BeforeDiagnosticRound(int tick)
        {
            probeRound = false;
            if (!probe.Active) return;
            int sender = probe.Sender, good = probe.Good;
            probeRoundSource = GetStock(sender, good); probeRoundDebit = 0;
            probePaused = state.IsPaused(sender);
            for (int recipient = 1; recipient <= 8; recipient++)
            {
                var rule = state.GetRule(sender, recipient, good);
                probeMin[recipient] = rule.Minimum; probeMax[recipient] = rule.Maximum;
                probeEligible[recipient] = !probePaused && rule.Enabled && IsAlliedLiving(sender, recipient);
                probeStock[recipient] = probeEligible[recipient] ? GetStock(recipient, good) : 0;
                probeFree[recipient] = probeEligible[recipient] ? GetFreeCapacity(recipient, good) : 0;
                probeCredits[recipient] = 0;
            }
            probeRound = probe.BeginRound(tick, state.GetReserve(sender, good), probeRoundSource,
                probeMin, probeMax, probeStock, probeFree, probeEligible, state.GetBatchLimit(good));
        }
        partial void BeforeDiagnosticTransfer(int sender, int recipient, int good, int amount)
        {
            if (!probe.Active || !probeRound || sender != probe.Sender || good != probe.Good) return;
            probeSourceBefore = GetStock(sender, good); probeRecipientBefore = GetStock(recipient, good);
        }
        partial void AfterDiagnosticTransfer(int sender, int recipient, int good, int amount, AllyTransferResult result)
        {
            if (!probe.Active || !probeRound || sender != probe.Sender || good != probe.Good) return;
            bool called = result?.OriginalWasCalled == true;
            bool known = !called || result.Status == AllyTransferStatus.Completed;
            probe.ObserveTransfer(recipient, amount, called, known, result?.SenderDebit, result?.RecipientCredit,
                probeSourceBefore, probeRecipientBefore);
            if (called && result.SenderDebit.HasValue) probeRoundDebit += result.SenderDebit.Value;
            if (called && result.RecipientCredit.GetValueOrDefault() > 0 && result.RecipientCredit <= int.MaxValue)
            { probeObserved++; probeCredits[recipient] += (int)result.RecipientCredit.Value; }
            if (probeDetailLines++ < 16) ProbeLog("test:transfer sender=" + sender + " recipient=" + recipient + " good=" + good +
                " requested=" + amount + " called=" + called + " status=" + result?.Status +
                " debit=" + result?.SenderDebit + " credit=" + result?.RecipientCredit +
                " sourceBefore=" + probeSourceBefore + " recipientBefore=" + probeRecipientBefore);
        }
        partial void AfterDiagnosticRound()
        {
            if (!probe.Active || !probeRound) return;
            // Other automated donors run in the same planner pass. Their credits must
            // not be mistaken for unfair allocation by the watched sender.
            bool externalChange = (long)GetStock(probe.Sender, probe.Good) != (long)probeRoundSource - probeRoundDebit;
            for (int recipient = 1; recipient <= 8; recipient++)
                if (probeEligible[recipient] && (long)GetStock(recipient, probe.Good) != (long)probeStock[recipient] + probeCredits[recipient]) externalChange = true;
            if (externalChange) probe.InvalidateRound();
            probe.EndRound(); probeRound = false; probeRounds++;
            ProbeLog("test:case round=" + probeRounds + " sender=" + probe.Sender + " good=" + probe.Good +
                " paused=" + probePaused + " externalChange=" + externalChange + " creditsByPlayer=" + string.Join(",", probeCredits));
            RequestPresentation();
        }
        private void FinishProbe(string reason)
        {
            var summary = probe.Finish(reason);
            probeRound = false; probeRequested = false; probeStatus = "ProbeFinished";
            probeSummary = summary.ToString() + " droppedLogs=" + probeDropped;
            ProbeLog("test:summary sender=" + probeSender + " good=" + probeGood + " " + probeSummary);
            RequestPresentation();
        }
        private void ProbeLog(string text)
        {
            if (probeMessages.Count < 96) probeMessages.Enqueue(text); else probeDropped++;
            RequestPresentation();
        }
        partial void PresentDiagnostics()
        {
            while (probeMessages.TryDequeue(out string record))
            { string immutable = record; DevDiag.Log(() => immutable); }
        }
    }

    internal sealed partial class AutoSendView
    {
        private Button probeButton;
        private TextBlock probeText;
        partial void AddDiagnostics(StackPanel body)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            probeButton = Action(row, "ProbeHelp", () => {
                if (AllyGoodsPanelExtensions.TryGetContext(out var context)) runtime.ToggleProbe(context);
            });
            probeText = Text(14); probeText.Margin = new Thickness(0, 5, 0, 0);
            body.Children.Add(row); body.Children.Add(probeText);
        }
        partial void RefreshDiagnostics()
        {
            if (probeButton == null) return;
            string status = runtime.ProbeStatus(out bool running);
            bool canStart = false;
            if (AllyGoodsPanelExtensions.TryGetContext(out var context) && context.IsSendMode && context.Good.HasValue)
            { var display = runtime.ReadDisplay(context); canStart = display.Ready && !display.Pending; }
            probeButton.IsEnabled = running || canStart;
            Content(probeButton, AutoSendText.Get(running ? "ProbeStop" : "ProbeStart"));
            Set(probeText, status);
        }
    }
}
