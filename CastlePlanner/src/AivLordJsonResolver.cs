using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CastlePlanner
{
    internal static class AivLordJsonResolver
    {
        // Provenance: VanillaAICExporter export for game build steam-24651686.
        // The official Castle & CPU Lord Editor ships the AIVJSON files without
        // companion LordJSON files, although the matching Lord data exists in-game.
        private static readonly IReadOnlyDictionary<string, ushort> VanillaFlagTypes =
            new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
            {
                ["Rat"] = 12,
                ["Snake"] = 12,
                ["Pig"] = 12,
                ["Wolf"] = 13,
                ["Saladin"] = 10,
                ["Caliph"] = 10,
                ["Sultan"] = 10,
                ["Richard"] = 13,
                ["Frederick"] = 13,
                ["Philip"] = 13,
                ["Wazir"] = 10,
                ["Emir"] = 10,
                ["Nizar"] = 10,
                ["Sheriff"] = 13,
                ["Marshal"] = 13,
                ["Abbot"] = 13,
                ["Jewel"] = 13,
                ["Sentinel"] = 13,
                ["Nomad"] = 10,
                ["Kahinah"] = 10,
                ["Canary"] = 12,
                ["Trader"] = 10,
                ["Sergeant"] = 13,
                ["Lioness"] = 10,
                ["Crocodile"] = 12,
                ["Baldwin"] = 12,
                ["Bullseye"] = 12,
                ["Surgeon"] = 10,
                ["Baibars"] = 10
            };

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
                    if (lordFiles.Length == 0 &&
                        TryResolveBundledVanillaFlagType(aivPath, out ushort vanillaFlagType))
                    {
                        return vanillaFlagType;
                    }

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

        private static bool TryResolveBundledVanillaFlagType(
            string aivPath,
            out ushort flagType)
        {
            flagType = 0;
            string directory = Path.GetDirectoryName(aivPath) ?? string.Empty;
            string directoryName = Path.GetFileName(directory) ?? string.Empty;
            bool bundledVanilla = string.Equals(
                directoryName,
                "VanillaAIV",
                StringComparison.OrdinalIgnoreCase);
            bool officialEditorVillages = string.Equals(
                    directoryName,
                    "Villages",
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    Path.GetFileName(Path.GetDirectoryName(directory) ?? string.Empty),
                    "StreamingAssets",
                    StringComparison.OrdinalIgnoreCase);
            if (!bundledVanilla && !officialEditorVillages)
                return false;

            string name = Path.GetFileNameWithoutExtension(aivPath) ?? string.Empty;
            const string communityPrefix = "Community_";
            if (name.StartsWith(communityPrefix, StringComparison.OrdinalIgnoreCase))
                name = name.Substring(communityPrefix.Length);
            int nameLength = name.Length;
            while (nameLength > 0 && char.IsDigit(name[nameLength - 1]))
                nameLength--;
            name = name.Substring(0, nameLength).Trim();
            return VanillaFlagTypes.TryGetValue(name, out flagType);
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
