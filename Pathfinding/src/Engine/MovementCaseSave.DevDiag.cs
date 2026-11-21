#if SERP_DEVDIAG
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Pathfinding.Engine
{
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        private static Action<string, byte[]> TraceMovementCaseCapture(Action<string, byte[]> write, string phase)
        {
            return (name, data) =>
            {
                write(name, data);
                LogMovementCaseEntry(phase, CaseRecordVersion, name, data);
            };
        }

        partial void DiagMovementCaseLoad(int version, IReadOnlyDictionary<string, byte[]> entries)
        {
            if (entries == null) return;
            foreach (string name in CaseEntries)
                if (entries.TryGetValue(name, out byte[] data))
                    LogMovementCaseEntry("staged", version, name, data);
        }

        private static void LogMovementCaseEntry(string phase, int version, string name, byte[] data)
        {
            Shared.DevDiag.Log(() =>
            {
                bool orders = name == CaseOrders;
                int countOffset = orders ? sizeof(int) : 0;
                string count = data.Length >= countOffset + sizeof(int) ? BitConverter.ToInt32(data, countOffset).ToString() : "unreadable";
                string sequence = orders && data.Length >= sizeof(int) ? $" orderSequence={BitConverter.ToInt32(data, 0)}" : string.Empty;
                using (var sha = SHA256.Create())
                {
                    string hash = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty);
                    return $"PATHFINDING_MOVEMENT_CASES phase={phase} version={version} entry={name} bytes={data.Length} count={count} sha256={hash}{sequence}";
                }
            });
        }
    }
}
#endif
