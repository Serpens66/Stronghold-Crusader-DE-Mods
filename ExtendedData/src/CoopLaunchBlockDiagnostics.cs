using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtendedData
{
    // Permanent support messages: independent of optional diagnostics and lobby chat delivery.
    internal static class CoopLaunchBlockDiagnostics
    {
        internal const string ChatPrefix = "Extended Data: Start blocked. ";

        internal static bool IsBlockedStartChat(string message) =>
            message?.StartsWith(ChatPrefix, StringComparison.Ordinal) == true;

        internal static string[] ChatMessages(string operation, IEnumerable<string> reasons)
        {
            string body = operation + ": " + string.Join("; ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))
                .Distinct(StringComparer.Ordinal)).Replace('\r', ' ').Replace('\n', ' ');
            // Reserve enough space for numbering even for very large reports. Never discard a reason.
            int limit = 280 - ChatPrefix.Length - 24;
            var parts = new List<string>();
            for (int offset = 0; offset < body.Length;)
            {
                int length = Math.Min(limit, body.Length - offset);
                if (offset + length < body.Length && char.IsHighSurrogate(body[offset + length - 1])) length--;
                parts.Add(body.Substring(offset, length));
                offset += length;
            }
            return parts.Select((part, index) => ChatPrefix +
                (parts.Count == 1 ? string.Empty : "[" + (index + 1) + "/" + parts.Count + "] ") + part).ToArray();
        }

        internal static void ReportLocal(ManualLogSource log, string command, string reason,
            string context, string detail = null)
        {
            string text = "Blocked launch " + command + ": " + reason + "; " + context +
                (string.IsNullOrEmpty(detail) ? string.Empty : "; " + detail);
            // The shared health ledger bounds a single detail to 4096 characters.
            // Split before that boundary so a long reason or exception remains available in full.
            var parts = new List<string>();
            for (int offset = 0; offset < text.Length;)
            {
                int count = Math.Min(3000, text.Length - offset);
                if (offset + count < text.Length && char.IsHighSurrogate(text[offset + count - 1])) count--;
                parts.Add(text.Substring(offset, count)); offset += count;
            }
            for (int index = 0; index < parts.Count; index++)
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData.CoopLaunch.LocalBlock",
                    (parts.Count == 1 ? string.Empty : "[" + (index + 1) + "/" + parts.Count + "] ") + parts[index]);
        }

        internal static void ReportRemote(ManualLogSource log, ulong sender, string lobby, string message)
        {
            if (!IsBlockedStartChat(message)) return;
            Shared.DebugLogHelper.LogWarningLimited(log, "ExtendedData.CoopLaunch.RemoteBlock",
                "Received blocked-start lobby chat: lobby=" + lobby + ", sender=" + sender +
                ", message=" + message);
        }
    }

    // One instance per runtime. Scope changes retire both automatic state and click throttles.
    internal sealed class CoopLaunchAnnouncementState
    {
        private string scope = string.Empty;
        private readonly Dictionary<string, string> observed = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTime> sent = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        internal bool ShouldSend(string currentScope, string operation, IEnumerable<string> reasons,
            bool automatic, DateTime now)
        {
            if (!string.Equals(scope, currentScope, StringComparison.Ordinal))
            { scope = currentScope; observed.Clear(); sent.Clear(); }
            string signature = string.Join("\n", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))
                .Distinct(StringComparer.Ordinal).OrderBy(reason => reason, StringComparer.Ordinal));
            if (automatic)
            {
                if (observed.TryGetValue(operation, out string previous) && previous == signature) return false;
                observed[operation] = signature;
                if (signature.Length == 0) { sent.Clear(); return false; }
            }
            string key = signature;
            if (sent.TryGetValue(key, out DateTime last) && (now - last).TotalSeconds < 2) return false;
            sent[key] = now;
            // Bound manual-attempt history without suppressing new reasons.
            if (sent.Count > 128) { sent.Clear(); sent[key] = now; }
            return signature.Length != 0;
        }
        internal void Reset() { scope = string.Empty; observed.Clear(); sent.Clear(); }
    }
}
