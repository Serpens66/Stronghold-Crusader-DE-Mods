using System;
using System.Collections.Generic;
using System.Linq;

namespace SerpsModsHost
{
    internal static class LobbyChatMessageFormatter
    {
        internal const int MaximumMessageLength = 280;
        internal const int MaximumShownDifferences = 4;

        internal static IReadOnlyList<string> BuildMessages(
            string summary,
            ModInventoryDifference difference,
            string hostOnlyLabel,
            string clientOnlyLabel,
            string versionLabel,
            string moreDifferencesTemplate,
            string inventoryUnavailable,
            string folderHint)
        {
            var messages = new List<string>();
            AddMessage(messages, summary);

            if (difference == null)
            {
                AddCombinedMessage(messages, inventoryUnavailable, folderHint);
                return messages;
            }

            int remaining = MaximumShownDifferences;
            AddSection(
                messages,
                hostOnlyLabel,
                difference.HostOnly.Take(remaining).Select(FormatEntry),
                ref remaining);
            AddSection(
                messages,
                clientOnlyLabel,
                difference.ClientOnly.Take(remaining).Select(FormatEntry),
                ref remaining);
            AddSection(
                messages,
                versionLabel,
                difference.VersionMismatches.Take(remaining).Select(FormatVersionMismatch),
                ref remaining);

            int shown = MaximumShownDifferences - remaining;
            int omitted = difference.Count - shown;
            string omittedText = omitted > 0
                ? (moreDifferencesTemplate ?? string.Empty)
                    .Replace("{Count}", omitted.ToString())
                : string.Empty;
            AddCombinedMessage(messages, omittedText, folderHint);
            return messages;
        }

        private static string FormatEntry(ModInventoryEntry entry) =>
            Clean(entry?.Name) + " v" + Clean(entry?.Version);

        private static string FormatVersionMismatch(ModVersionMismatch mismatch)
        {
            if (mismatch == null)
                return string.Empty;
            string name = string.IsNullOrWhiteSpace(mismatch.Host.Name)
                ? mismatch.Client.Name
                : mismatch.Host.Name;
            return Clean(name) + ": " + Clean(mismatch.Client.Version) + " / " +
                Clean(mismatch.Host.Version);
        }

        private static void AddSection(
            ICollection<string> messages,
            string label,
            IEnumerable<string> values,
            ref int remaining)
        {
            if (remaining <= 0)
                return;

            string prefix = Clean(label) + ": ";
            prefix = Truncate(prefix, MaximumMessageLength - 2);
            string current = prefix;
            bool hasValue = false;

            foreach (string rawValue in values)
            {
                if (remaining <= 0)
                    break;

                string value = Truncate(
                    Clean(rawValue),
                    MaximumMessageLength - prefix.Length - 1);
                if (value.Length == 0)
                    continue;

                string separator = hasValue ? ", " : string.Empty;
                if (current.Length + separator.Length + value.Length + 1 > MaximumMessageLength)
                {
                    messages.Add(current + ".");
                    current = prefix;
                    hasValue = false;
                    separator = string.Empty;
                }

                current += separator + value;
                hasValue = true;
                remaining--;
            }

            if (hasValue)
                messages.Add(current + ".");
        }

        private static void AddCombinedMessage(
            ICollection<string> messages,
            string first,
            string second)
        {
            string combined = string.Join(
                " ",
                new[] { Clean(first), Clean(second) }.Where(value => value.Length > 0));
            AddMessage(messages, combined);
        }

        private static void AddMessage(ICollection<string> messages, string message)
        {
            string cleaned = Clean(message);
            if (cleaned.Length > 0)
                messages.Add(Truncate(cleaned, MaximumMessageLength));
        }

        private static string Clean(string value) =>
            (value ?? string.Empty)
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();

        private static string Truncate(string value, int maximumLength)
        {
            if (string.IsNullOrEmpty(value) || maximumLength <= 0)
                return string.Empty;
            if (value.Length <= maximumLength)
                return value;
            if (maximumLength == 1)
                return "…";

            int contentLength = maximumLength - 1;
            if (contentLength > 0 && char.IsHighSurrogate(value[contentLength - 1]))
                contentLength--;
            return value.Substring(0, contentLength).TrimEnd() + "…";
        }
    }
}
