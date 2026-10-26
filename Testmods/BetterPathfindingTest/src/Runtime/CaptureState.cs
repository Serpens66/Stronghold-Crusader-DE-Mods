using System;
using APIShared;

namespace BetterPathfindingTest
{
    internal sealed class CaptureState
    {
        private readonly Func<double> seconds;
        internal long Session { get; private set; }
        internal int Captures { get; private set; }
        internal bool Stopped { get; private set; }
        private double due;
        internal string WaitingReason = "engine-lock";
        internal CaptureState(Func<double> seconds) { this.seconds = seconds; }
        internal bool Start(long id)
        {
            if (id <= Session) return false; // Session IDs are process-local and monotonic.
            Session = id; Captures = 0; Stopped = false; due = seconds(); WaitingReason = "engine-lock"; return true;
        }
        internal void Retire(long id) { if (Session == id) Stopped = true; }
        internal bool Pending => !Stopped && Session != 0 && Captures < 2 && seconds() >= due;
        internal double WaitMilliseconds => Math.Max(0, seconds() - due) * 1000;
        internal bool Expired => Pending && seconds() - due >= 30;
        internal void Captured() { Captures++; due = seconds() + 10; WaitingReason = "engine-lock"; }
        internal void Fail() { Stopped = true; }
    }
    internal sealed class CaptureNotReadyException : Exception
    { internal CaptureNotReadyException(string message) : base(message) { } }
    internal sealed class CaptureContractException : NotSupportedException
    { internal CaptureContractException(string message) : base(message) { } }
    internal static class GateCapture
    {
        internal static IEnemyGateRoutePolicySnapshot[] Read(IEnemyGatePathPolicy policy)
        {
            var snapshots = new IEnemyGateRoutePolicySnapshot[9];
            if (policy == null || !policy.HasPublishedMask) return snapshots;
            var provider = policy as IEnemyGateRoutePolicyProvider;
            if (provider == null) throw new NotSupportedException("Published gate policy has no immutable snapshot provider.");
            for (int player = 1; player <= 8; player++)
            {
                if (!provider.TryCaptureRoutePolicy(player, out snapshots[player]) || snapshots[player] == null) throw new CaptureNotReadyException("gate-publication-unavailable player=" + player);
                if (snapshots[player].PlayerId != player) throw new InvalidOperationException("Gate snapshot has the wrong player: " + player);
            }
            Validate(snapshots);
            return snapshots;
        }
        internal static void Validate(IEnemyGateRoutePolicySnapshot[] snapshots)
        { for (int player = 1; player <= 8; player++) if (snapshots[player] != null && !snapshots[player].IsCurrent) throw new CaptureNotReadyException("gate-publication-stale player=" + player); }
    }
}
