using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CastlePlanner
{
    internal static class AivLordJsonResolver
    {
        internal static ushort ResolveFlagProjectileType(
            string aivPath,
            out string lordPath,
            out string warning)
        {
            const ushort fallback = (ushort)ProjectileType.CrusaderFlag;
            lordPath = string.Empty;
            warning = string.Empty;

            try
            {
                string directory = Path.GetDirectoryName(aivPath) ?? string.Empty;
                string aivStem = Path.GetFileNameWithoutExtension(aivPath) ?? string.Empty;
                string[] lordFiles = Directory.Exists(directory)
                    ? Directory.GetFiles(directory, "*.lordjson", SearchOption.TopDirectoryOnly)
                    : Array.Empty<string>();
                Array.Sort(lordFiles, StringComparer.OrdinalIgnoreCase);

                string[] prefixMatches = lordFiles
                    .Where(path => aivStem.StartsWith(
                        Path.GetFileNameWithoutExtension(path) ?? string.Empty,
                        StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(path =>
                        (Path.GetFileNameWithoutExtension(path) ?? string.Empty).Length)
                    .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (prefixMatches.Length > 0)
                {
                    int longestLength = (Path.GetFileNameWithoutExtension(prefixMatches[0]) ?? string.Empty).Length;
                    string[] longest = prefixMatches
                        .Where(path => (Path.GetFileNameWithoutExtension(path) ?? string.Empty).Length == longestLength)
                        .ToArray();
                    if (longest.Length != 1)
                    {
                        warning = $"AIVJSON '{aivPath}' has multiple equally specific LordJSON prefix matches; using CrusaderFlag ({fallback}).";
                        return fallback;
                    }
                    lordPath = longest[0];
                }
                else if (lordFiles.Length == 1)
                {
                    lordPath = lordFiles[0];
                }
                else
                {
                    warning = lordFiles.Length == 0
                        ? $"AIVJSON '{aivPath}' has no LordJSON companion; using CrusaderFlag ({fallback})."
                        : $"AIVJSON '{aivPath}' has no name match and multiple LordJSON companions; using CrusaderFlag ({fallback}).";
                    return fallback;
                }

                object parsed = Shared.DependencyFreeJson.Parse(
                    File.ReadAllText(lordPath),
                    allowTrailingCommas: true);
                if (!(parsed is Dictionary<string, object> root) ||
                    !root.TryGetValue("lord", out object lordValue) ||
                    !(lordValue is Dictionary<string, object> lord) ||
                    !lord.TryGetValue("flag_type", out object flagValue) ||
                    !TryReadUInt16(flagValue, out ushort flagType))
                {
                    warning = $"LordJSON '{lordPath}' has no UInt16 lord.flag_type; using CrusaderFlag ({fallback}).";
                    return fallback;
                }

                return flagType;
            }
            catch (Exception exception)
            {
                warning = $"Could not resolve LordJSON for AIVJSON '{aivPath}': {exception.GetBaseException().Message}; using CrusaderFlag ({fallback}).";
                return fallback;
            }
        }

        private static bool TryReadUInt16(object value, out ushort result)
        {
            result = 0;
            if (value is int integer && integer >= ushort.MinValue && integer <= ushort.MaxValue)
            {
                result = (ushort)integer;
                return true;
            }
            if (value is long longInteger && longInteger >= ushort.MinValue && longInteger <= ushort.MaxValue)
            {
                result = (ushort)longInteger;
                return true;
            }
            if (value is uint unsignedInteger && unsignedInteger <= ushort.MaxValue)
            {
                result = (ushort)unsignedInteger;
                return true;
            }
            if (value is ulong unsignedLong && unsignedLong <= ushort.MaxValue)
            {
                result = (ushort)unsignedLong;
                return true;
            }
            return false;
        }
    }
}
