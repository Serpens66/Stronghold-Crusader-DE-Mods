using BepInEx.Logging;
using System;

namespace ExtendedData
{
    // Permanent support messages: independent of optional diagnostics and lobby chat delivery.
    internal static class CoopLaunchBlockDiagnostics
    {
        internal const string ChatPrefix = "Extended Data: Start blocked. ";

        internal static bool IsBlockedStartChat(string message) =>
            message?.StartsWith(ChatPrefix, StringComparison.Ordinal) == true;

        internal static void ReportLocal(ManualLogSource log, string command, string reason,
            string context, string detail = null)
        {
            Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData.CoopLaunch.LocalBlock",
                "Blocked launch " + command + ": " + reason + "; " + context +
                (string.IsNullOrEmpty(detail) ? string.Empty : "; " + detail));
        }

        internal static void ReportRemote(ManualLogSource log, ulong sender, string lobby, string message)
        {
            if (!IsBlockedStartChat(message)) return;
            Shared.DebugLogHelper.LogWarningLimited(log, "ExtendedData.CoopLaunch.RemoteBlock",
                "Received blocked-start lobby chat: lobby=" + lobby + ", sender=" + sender +
                ", message=" + message);
        }
    }
}
