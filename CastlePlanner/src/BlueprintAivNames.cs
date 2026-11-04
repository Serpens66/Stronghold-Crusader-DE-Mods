using System;
using System.Collections.Generic;
using System.IO;
using SHCDESE.Interop;

namespace CastlePlanner
{
    internal enum BlueprintAivSet { Default, Community, Historical }

    // Names are indexed by the imported bank's variant, never by village slot.
    internal static class BlueprintAivNames
    {
        internal static string FileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            string file = name.Replace('\\', '/');
            file = file.Substring(file.LastIndexOf('/') + 1);
            if (string.IsNullOrWhiteSpace(file)) return null;
            return file.EndsWith(".aivjson", StringComparison.OrdinalIgnoreCase)
                ? file : file + ".aivjson";
        }

        internal static string BuiltIn(int lordType, int variant, BlueprintAivSet set,
            string[,] defaults, Func<int, int, string> findOverride)
        {
            if (defaults == null || lordType < 0 || lordType >= defaults.GetLength(0) ||
                variant < 0 || variant >= defaults.GetLength(1) ||
                (set == BlueprintAivSet.Historical && variant != 0)) return null;
            string replacement = findOverride?.Invoke(lordType, variant);
            if (replacement != null) return FileName(replacement);
            string file = FileName(defaults[lordType, variant]);
            if (file == null) return null;
            // Vanilla only has alternate sets for Rat through Abbot. Later lords
            // keep their default arrays even when the alternate-set flag is set.
            if (lordType >= (int)AILords.SK_JEWEL - 1 || set == BlueprintAivSet.Default)
                return file;
            return set == BlueprintAivSet.Community ? "Community_" + file
                : "Community_Historical_" + Path.GetFileNameWithoutExtension(file).TrimEnd('1','2','3','4','5','6','7','8') + ".aivjson";
        }

        internal static string Candidate(string name, bool builtIn, ulong checksum,
            int lordType, string[,] defaults, Func<int, int, string> findOverride)
        {
            if (!builtIn) return FileName(name);
            // Vanilla CustomisationFileManager serializes these built-in source IDs.
            if (checksum >= 1 && checksum <= 8)
                return BuiltIn(lordType, (int)checksum - 1, BlueprintAivSet.Default, defaults, findOverride);
            if (checksum >= 51 && checksum <= 58)
                return BuiltIn(lordType, (int)checksum - 51, BlueprintAivSet.Community, defaults, findOverride);
            return checksum == 61
                ? BuiltIn(lordType, 0, BlueprintAivSet.Historical, defaults, findOverride) : null;
        }

        internal static string At(IReadOnlyList<string> names, int variant) =>
            names != null && variant >= 0 && variant < names.Count ? names[variant] : null;
    }
}
