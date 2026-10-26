using BepInEx.Logging;
using System.Collections.Generic;

namespace ExtendedData
{
    // Permanent local support messages for checks that are not lobby announcements (map and package checks).
    // Lobby chat, deduplication and launch status logging belong to APIShared.Lobby.LobbyLaunchStatus.
    internal static class CoopLaunchBlockDiagnostics
    {
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
                int count = System.Math.Min(3000, text.Length - offset);
                if (offset + count < text.Length && char.IsHighSurrogate(text[offset + count - 1])) count--;
                parts.Add(text.Substring(offset, count)); offset += count;
            }
            for (int index = 0; index < parts.Count; index++)
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData.CoopLaunch.LocalBlock",
                    (parts.Count == 1 ? string.Empty : "[" + (index + 1) + "/" + parts.Count + "] ") + parts[index]);
        }
    }
}
