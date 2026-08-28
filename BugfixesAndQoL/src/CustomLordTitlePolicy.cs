using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal sealed class CustomLordResolvedTitle
    {
        internal CustomLordResolvedTitle(string rawSuffix, string columnTitle, bool usesCustomTitle)
        {
            RawSuffix = rawSuffix ?? string.Empty;
            ColumnTitle = columnTitle ?? string.Empty;
            UsesCustomTitle = usesCustomTitle;
        }

        internal string RawSuffix { get; }
        internal string ColumnTitle { get; }
        internal bool UsesCustomTitle { get; }
    }

    internal static class CustomLordTitlePolicy
    {
        internal const int TitleSlotCount = 8;

        internal static CustomLordResolvedTitle[] ResolveAll(
            IReadOnlyList<string> rawTitles,
            IReadOnlyList<string> ordinalTitles)
        {
            if (rawTitles == null)
                throw new ArgumentNullException(nameof(rawTitles));
            if (ordinalTitles == null)
                throw new ArgumentNullException(nameof(ordinalTitles));
            if (rawTitles.Count < TitleSlotCount || ordinalTitles.Count < TitleSlotCount)
                throw new ArgumentException("Custom Lord title resolution requires eight title slots.");

            CustomLordResolvedTitle[] resolved = new CustomLordResolvedTitle[TitleSlotCount];
            var usedTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int slot = 0; slot < TitleSlotCount; slot++)
            {
                string raw = rawTitles[slot] ?? string.Empty;
                string column = ToColumnTitle(raw);
                string normalized = NormalizeForComparison(column);
                if (normalized.Length > 0 && usedTitles.Add(normalized))
                {
                    resolved[slot] = new CustomLordResolvedTitle(raw, column, usesCustomTitle: true);
                    continue;
                }

                string ordinal = (ordinalTitles[slot] ?? string.Empty).Trim();
                resolved[slot] = new CustomLordResolvedTitle(ordinal, ordinal, usesCustomTitle: false);
                if (ordinal.Length > 0)
                    usedTitles.Add(NormalizeForComparison(ordinal));
            }

            return resolved;
        }

        internal static bool TryRewriteFullName(
            string upstreamName,
            string upstreamRawSuffix,
            string correctRawSuffix,
            CustomLordResolvedTitle resolved,
            out string rewritten)
        {
            rewritten = upstreamName ?? string.Empty;
            if (resolved == null || string.IsNullOrEmpty(upstreamName))
                return false;

            string baseName;
            if (TryStripSuffix(upstreamName, correctRawSuffix, out baseName) ||
                TryStripSuffix(upstreamName, upstreamRawSuffix, out baseName))
            {
                rewritten = JoinNameAndTitle(baseName, resolved);
                return true;
            }

            // With no Script Extender title, its result is already the localized display name.
            if (string.IsNullOrEmpty(correctRawSuffix) && string.IsNullOrEmpty(upstreamRawSuffix))
            {
                rewritten = JoinNameAndTitle(upstreamName, resolved);
                return true;
            }

            return false;
        }

        internal static bool HasExactSuffix(string value, string suffix)
        {
            return !string.IsNullOrEmpty(value) &&
                   !string.IsNullOrEmpty(suffix) &&
                   value.EndsWith(suffix, StringComparison.Ordinal);
        }

        internal static string ToColumnTitle(string rawTitle)
        {
            string value = (rawTitle ?? string.Empty).TrimStart();
            int index = 0;
            while (index < value.Length && IsLeadingSeparator(value[index]))
                index++;
            return value.Substring(index).Trim();
        }

        private static string NormalizeForComparison(string title)
        {
            return (title ?? string.Empty).Trim();
        }

        private static bool TryStripSuffix(string value, string suffix, out string baseName)
        {
            baseName = value;
            if (!HasExactSuffix(value, suffix))
                return false;

            baseName = value.Substring(0, value.Length - suffix.Length).TrimEnd();
            return baseName.Length > 0;
        }

        private static string JoinNameAndTitle(string baseName, CustomLordResolvedTitle resolved)
        {
            string name = (baseName ?? string.Empty).TrimEnd();
            if (resolved.UsesCustomTitle)
            {
                string suffix = resolved.RawSuffix ?? string.Empty;
                if (suffix.Length == 0)
                    return name;
                return StartsWithSeparator(suffix) ? name + suffix : name + " " + suffix;
            }

            return string.IsNullOrEmpty(resolved.ColumnTitle)
                ? name
                : name + " " + resolved.ColumnTitle;
        }

        private static bool StartsWithSeparator(string value)
        {
            return value.Length > 0 && (char.IsWhiteSpace(value[0]) || IsLeadingSeparator(value[0]));
        }

        private static bool IsLeadingSeparator(char value)
        {
            return value == ',' || value == ':' || value == ';' || value == '-' ||
                   value == '\u2013' || value == '\u2014';
        }
    }
}
