using CrusaderDE;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace BugfixesAndQoL
{
    public enum AivCandidateStatus
    {
        Pending = 0,
        Complete = 1,
        Partial = 2,
        Impossible = 3,
        NotEvaluable = 4
    }

    public sealed class AivCandidateStatusInfo
    {
        internal AivCandidateStatusInfo(AivCandidateStatus status, string toolTip)
        {
            Status = status;
            ToolTip = toolTip ?? string.Empty;
        }

        public AivCandidateStatus Status { get; }
        public string ToolTip { get; }
    }

    // This deliberately exposes only Vanilla types, checksums and neutral status values.
    // Optional providers can publish through reflection without becoming a dependency of this mod.
    public static class AivCandidateStatusApi
    {
        private static readonly Dictionary<FRONT_Multiplayer.MPAIVInfo, Dictionary<ulong, AivCandidateStatusInfo>> Statuses =
            new Dictionary<FRONT_Multiplayer.MPAIVInfo, Dictionary<ulong, AivCandidateStatusInfo>>(
                ReferenceEqualityComparer<FRONT_Multiplayer.MPAIVInfo>.Instance);

        public static event Action<FRONT_Multiplayer.MPAIVInfo> StatusChanged;

        public static void SetStatus(
            FRONT_Multiplayer.MPAIVInfo info,
            ulong aivChecksum,
            int status,
            string toolTip)
        {
            if (info == null || !Enum.IsDefined(typeof(AivCandidateStatus), status))
                return;

            if (!Statuses.TryGetValue(info, out Dictionary<ulong, AivCandidateStatusInfo> byChecksum))
            {
                byChecksum = new Dictionary<ulong, AivCandidateStatusInfo>();
                Statuses[info] = byChecksum;
            }

            var next = new AivCandidateStatusInfo((AivCandidateStatus)status, toolTip);
            if (byChecksum.TryGetValue(aivChecksum, out AivCandidateStatusInfo previous) &&
                previous.Status == next.Status &&
                string.Equals(previous.ToolTip, next.ToolTip, StringComparison.Ordinal))
            {
                return;
            }

            byChecksum[aivChecksum] = next;
            StatusChanged?.Invoke(info);
        }

        public static void ClearStatuses(FRONT_Multiplayer.MPAIVInfo info)
        {
            if (info != null && Statuses.Remove(info))
                StatusChanged?.Invoke(info);
        }

        public static void ClearAll()
        {
            if (Statuses.Count == 0)
                return;
            FRONT_Multiplayer.MPAIVInfo[] changed = new FRONT_Multiplayer.MPAIVInfo[Statuses.Count];
            Statuses.Keys.CopyTo(changed, 0);
            Statuses.Clear();
            foreach (FRONT_Multiplayer.MPAIVInfo info in changed)
                StatusChanged?.Invoke(info);
        }

        public static bool TryGetStatus(
            FRONT_Multiplayer.MPAIVInfo info,
            ulong aivChecksum,
            out AivCandidateStatusInfo status)
        {
            status = null;
            return info != null &&
                Statuses.TryGetValue(info, out Dictionary<ulong, AivCandidateStatusInfo> byChecksum) &&
                byChecksum.TryGetValue(aivChecksum, out status);
        }

        private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
        {
            internal static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
