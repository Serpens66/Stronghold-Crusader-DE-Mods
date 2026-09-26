using CrusaderDE;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ExtendedData
{
    internal static class TrailLordPackageRuntime
    {
        private sealed class Candidate
        {
            internal string Name;
            internal CustomisationFileManager.CustomLordConfig Config;
            internal List<CustomisationFileManager.CustomAIV> Aivs;
        }

        internal static IReadOnlyList<TrailLordSlot> Capture(
            HUD_IngameMenu.RestartSkirmishMapInfo restart)
        {
            if (restart?.aivs == null)
                throw new InvalidDataException("The Trail has no saved Lord data.");
            var fixes = new FixesLordPreferencesBridge();
            var result = new List<TrailLordSlot>();
            for (int index = 0; index < Math.Min(8, restart.aivs.Length); index++)
            {
                if (restart.lordTypes != null && index < restart.lordTypes.Count &&
                    restart.lordTypes[index] < 0)
                    continue;
                FRONT_Multiplayer.MPAIVInfo info = restart.aivs[index];
                if (info == null || info.builtInLord || info.lordConfig == null)
                    continue;
                string checksum = info.lordConfig.checksum.ToString();
                Candidate[] sources = FindCandidates(checksum,
                    (info.aivs ?? new List<CustomisationFileManager.CustomAIV>())
                        .Select(aiv => aiv.checksum.ToString()).ToArray());
                Candidate[] matching = sources.Where(item =>
                    string.Equals(item.Name, info.lordName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(item.Config.name, info.lordConfig.name, StringComparison.OrdinalIgnoreCase)).ToArray();
                int sourceIndex = TrailLordSourceSelector.SelectIndex(
                    matching.Select(item => item.Config.path).ToArray(),
                    info.lordConfig.path, info.lordName);
                Candidate source = matching[sourceIndex];
                LordPackageFileState package = LordPackageFingerprint.Capture(source.Config.path, source.Config.name);
                string sidecar = Path.Combine(source.Config.path, source.Config.name + ".modlord.json");
                string modJson = null;
                if (File.Exists(sidecar))
                {
                    byte[] bytes = File.ReadAllBytes(sidecar);
                    if (bytes.Length > LordDataSnapshot.MaxSidecarBytes)
                        throw new InvalidDataException("Lord mod values exceed 64 KiB: " + sidecar);
                    modJson = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
                    LordDataSnapshot.ValidateModLord(modJson);
                }
                result.Add(new TrailLordSlot
                {
                    PlayerId = index + 1,
                    LordName = info.lordName,
                    ConfigName = info.lordConfig.name,
                    ConfigChecksum = checksum,
                    AivChecksums = (info.aivs ?? new List<CustomisationFileManager.CustomAIV>())
                        .Select(aiv => aiv.checksum.ToString()).ToArray(),
                    RequiresInstalledPackage = package.HasUnsupportedGameplayFiles,
                    PackageDigest = package.HasUnsupportedGameplayFiles ? package.Digest : null,
                    ModLordJson = modJson,
                    FixesJson = fixes.Capture(info.lordName),
                });
            }
            return result;
        }

        internal static bool TryResolve(TrailLordSlot slot, out string internalName,
            out string requiredLordPath, out string reason)
        {
            internalName = null;
            requiredLordPath = null;
            reason = string.Empty;
            Candidate[] candidates = FindCandidates(slot.ConfigChecksum, slot.AivChecksums);
            if (slot.RequiresInstalledPackage)
            {
                candidates = candidates.Where(item =>
                {
                    try
                    {
                        return HasRegisteredSePackage(item) &&
                            string.Equals(LordPackageFingerprint.Capture(item.Config.path,
                                item.Config.name).Digest, slot.PackageDigest, StringComparison.Ordinal);
                    }
                    catch { return false; }
                }).ToArray();
                if (candidates.Length == 0)
                {
                    reason = "Required Lord package is missing, differs, or was not loaded by Script Extender at startup: " + slot.LordName;
                    return false;
                }
                requiredLordPath = Path.Combine(candidates[0].Config.path,
                    candidates[0].Config.name + ".lordjson");
            }
            Candidate[] mediaDonors = candidates.Where(item =>
                HasRegisteredSeMedia(item) || HasVanillaMedia(item.Name)).ToArray();
            if (mediaDonors.Length == 1)
                internalName = mediaDonors[0].Name;
            return true;
        }

        private static Candidate[] FindCandidates(string configChecksum, IReadOnlyList<string> aivChecksums)
        {
            var result = new List<Candidate>();
            CustomisationFileManager manager = CustomisationFileManager.Instance;
            foreach (CustomisationFileManager.CustomLord lord in manager.GetCustomLords())
                AddCandidates(result, lord.lordName, lord.configs, lord.aivs,
                    configChecksum, aivChecksums);
            for (int type = 0; type < ConfigSettings.extendedLordPaths.Length; type++)
            {
                List<CustomisationFileManager.CustomLordConfig> configs = manager.getLordLordList(type);
                if (configs == null) continue;
                string name = ConfigSettings.extendedLordPaths[type];
                AddCandidates(result, name, configs, manager.getLordAIVList(type),
                    configChecksum, aivChecksums);
            }
            return result.GroupBy(item => item.Name + "|" + item.Config.path + "|" + item.Config.name,
                StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToArray();
        }

        private static void AddCandidates(List<Candidate> result, string name,
            IEnumerable<CustomisationFileManager.CustomLordConfig> configs,
            List<CustomisationFileManager.CustomAIV> aivs,
            string checksum, IReadOnlyList<string> requiredAivs)
        {
            if (configs == null || aivs == null) return;
            string[] available = aivs.Select(item => item.checksum.ToString()).ToArray();
            if (requiredAivs.Any(value => !available.Contains(value, StringComparer.Ordinal))) return;
            foreach (var config in configs.Where(item => item != null &&
                string.Equals(item.checksum.ToString(), checksum, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(item.path)))
                result.Add(new Candidate { Name = name, Config = config, Aivs = aivs });
        }

        private static bool HasVanillaMedia(string name)
        {
            string simple = name?.Split('\\').LastOrDefault();
            return !string.IsNullOrWhiteSpace(simple) &&
                Directory.Exists(Path.Combine(ConfigSettings.GetUserCustomMediaPath(), simple));
        }

        private static bool HasRegisteredSeMedia(Candidate donor)
        {
            if (string.IsNullOrWhiteSpace(donor.Name) ||
                !GameAIManagerAPI.Instance.IsSupportedCustomLord(donor.Name)) return false;
            return HasRegisteredSePackage(donor);
        }

        private static bool HasRegisteredSePackage(Candidate donor)
        {
            string infoPath = Path.Combine(donor.Config.path, "info.json");
            if (!File.Exists(infoPath)) return false;
            try
            {
                var info = Shared.DependencyFreeJson.Parse(File.ReadAllText(infoPath)) as Dictionary<string, object>;
                string guid = info != null && info.TryGetValue("GUID", out object value) ? value as string : null;
                return guid != null && GameAssetModManager.Instance.TryGetRegisteredDirectory(guid,
                    out string registered) && string.Equals(Path.GetFullPath(registered).TrimEnd('\\', '/'),
                        Path.GetFullPath(donor.Config.path).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
    }
}
