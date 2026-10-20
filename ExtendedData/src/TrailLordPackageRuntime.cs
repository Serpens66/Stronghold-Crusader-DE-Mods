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
        internal sealed class Candidate
        {
            internal string Name;
            internal CustomisationFileManager.CustomLordConfig Config;
            internal List<CustomisationFileManager.CustomAIV> Aivs;
        }

        internal static IReadOnlyList<TrailLordSlot> Capture(
            HUD_IngameMenu.RestartSkirmishMapInfo restart,
            IDictionary<int, Candidate> resolvedSources = null,
            ISet<int> includedPlayerIds = null)
        {
            var fixes = new FixesLordPreferencesBridge();
            var result = new List<TrailLordSlot>();
            foreach (int playerId in CustomLordPlayerIds(restart, includedPlayerIds))
            {
                FRONT_Multiplayer.MPAIVInfo info = restart.aivs[playerId - 1];
                result.Add(CaptureSlot(info, playerId, fixes, out Candidate source));
                if (resolvedSources != null) resolvedSources[playerId] = source;
            }
            return result;
        }

        // Saved Custom-AIC slots in original 1-based player IDs. Built-in AICs carry no
        // installation-dependent values and are never part of the requirements.
        internal static IReadOnlyList<int> CustomLordPlayerIds(
            HUD_IngameMenu.RestartSkirmishMapInfo restart, ISet<int> includedPlayerIds)
        {
            if (restart?.aivs == null)
                throw new InvalidDataException("The Trail has no saved Lord data.");
            var result = new List<int>();
            for (int index = 0; index < Math.Min(8, restart.aivs.Length); index++)
            {
                if (includedPlayerIds != null && !includedPlayerIds.Contains(index + 1))
                    continue;
                if (restart.lordTypes != null && index < restart.lordTypes.Count &&
                    restart.lordTypes[index] < 0)
                    continue;
                FRONT_Multiplayer.MPAIVInfo info = restart.aivs[index];
                if (info == null || info.builtInLord || info.lordConfig == null)
                    continue;
                result.Add(index + 1);
            }
            return result;
        }

        // The identity stored in the Trail itself; needs no installation.
        internal static TrailLordSlot SavedIdentity(FRONT_Multiplayer.MPAIVInfo info, int playerId)
        {
            return new TrailLordSlot
            {
                PlayerId = playerId,
                LordType = info.lordConfig.lordType,
                LordName = TrailLordSourceSelector.ResolveLordName(info.lordName,
                    info.lordType, ConfigSettings.extendedLordPaths),
                ConfigName = info.lordConfig.name,
                ConfigChecksum = info.lordConfig.checksum.ToString(),
                AivChecksums = (info.aivs ?? new List<CustomisationFileManager.CustomAIV>())
                    .Select(aiv => aiv.checksum.ToString()).ToArray(),
            };
        }

        internal static TrailLordSlot CaptureSlot(FRONT_Multiplayer.MPAIVInfo info, int playerId,
            FixesLordPreferencesBridge fixes, out Candidate source)
        {
            string checksum = info.lordConfig.checksum.ToString();
            source = ResolveAuthorSource(info, playerId);
            LordPackageFileState package = LordPackageFingerprint.Capture(source.Config.path, source.Config.name);
            string sidecar = Path.Combine(source.Config.path, source.Config.name + ".modlord.json");
            string modJson = File.Exists(sidecar) ? LordDataSnapshot.ReadModLordFile(sidecar) : null;
            return new TrailLordSlot
            {
                PlayerId = playerId,
                LordType = source.Config.lordType,
                LordName = source.Name,
                ConfigName = info.lordConfig.name,
                ConfigChecksum = checksum,
                AivChecksums = (info.aivs ?? new List<CustomisationFileManager.CustomAIV>())
                    .Select(aiv => aiv.checksum.ToString()).ToArray(),
                RequiresInstalledPackage = package.HasUnsupportedGameplayFiles,
                PackageDigest = package.HasUnsupportedGameplayFiles ? package.Digest : null,
                ModLordJson = modJson,
                FixesJson = fixes.Capture(source.Name, source.Config.lordType),
            };
        }

        internal static Candidate ResolveAuthorSource(FRONT_Multiplayer.MPAIVInfo info, int playerId)
        {
            try
            {
                if (info?.lordConfig == null)
                    throw new InvalidDataException("The author Lord has no saved configuration.");
                string name = TrailLordSourceSelector.ResolveLordName(info.lordName,
                    info.lordType, ConfigSettings.extendedLordPaths);
                Candidate[] matching = FindCandidates(info.lordConfig.checksum.ToString(),
                    (info.aivs ?? new List<CustomisationFileManager.CustomAIV>())
                        .Select(aiv => aiv.checksum.ToString()).ToArray())
                    .Where(item => TrailLordSourceSelector.MatchesIdentity(item.Name,
                        item.Config.lordType, item.Config.name, name, info.lordType,
                        info.lordConfig.name, string.IsNullOrWhiteSpace(info.lordName))).ToArray();
                int sourceIndex = TrailLordSourceSelector.SelectIndex(
                    matching.Select(item => item.Config.path).ToArray(), info.lordConfig.path, name,
                    info.lordConfig.name);
                return matching[sourceIndex];
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("Author Lord resolution failed: player=" + playerId +
                    ", lordType=" + info?.lordType + ", lordName=[" + info?.lordName +
                    "], config=[" + info?.lordConfig?.name + "], checksum=" + info?.lordConfig?.checksum +
                    ", source=[" + info?.lordConfig?.path + "]. " + exception.Message, exception);
            }
        }
        internal static bool TryResolve(TrailLordSlot slot, out string internalName,
            out string requiredLordPath, out string reason)
        {
            internalName = null;
            requiredLordPath = null;
            reason = string.Empty;
            Candidate[] candidates = FindCandidates(slot.ConfigChecksum, slot.AivChecksums)
                .Where(item => !slot.LordType.HasValue || item.Config.lordType == slot.LordType.Value).ToArray();
            if (slot.RequiresInstalledPackage)
            {
                if (candidates.Select(item => item.Config.lordType).Distinct().Count() != 1)
                { reason = "Lord type is missing or ambiguous: " + slot.LordName; return false; }
                candidates = candidates.Where(item =>
                {
                    try
                    {
                        return HasRegisteredSePackage(item) &&
                            string.Equals(LordPackageFingerprint.Capture(item.Config.path,
                                item.Config.name).Digest, slot.PackageDigest, StringComparison.Ordinal);
                    }
                    catch (Exception ex) {
                        Shared.RuntimeHealth.Report("required-lord-package-fingerprint", false,
                            "Required package could not be verified; candidate rejected: " + ex);
                        return false;
                    }
                }).ToArray();
                if (candidates.Length == 0)
                {
                    reason = "Required Lord package is missing, differs, or was not loaded by Script Extender at startup: " + slot.LordName;
                    return false;
                }
                requiredLordPath = Path.Combine(candidates[0].Config.path,
                    candidates[0].Config.name + ".lordjson");
            }
            // Embedded gameplay values need no local installation. Legacy payloads with
            // several possible installed types must not pick an arbitrary media provider.
            if (candidates.Select(item => item.Config.lordType).Distinct().Skip(1).Any())
            {
                reason = "Optional Lord media has ambiguous installed Lord types: " + slot.LordName;
                return true;
            }
            Candidate[] mediaDonors = candidates.Where(item =>
                HasRegisteredSeMedia(item) || HasVanillaMedia(item.Name)).ToArray();
            if (mediaDonors.Length == 1)
                internalName = mediaDonors[0].Name;
            else
                reason = mediaDonors.Length == 0
                    ? "No installed Lord with matching configuration and AIV checksums has registered media: " +
                        slot.LordName
                    : "Several installed Lords with matching configuration and AIV checksums have media: " +
                        slot.LordName;
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
            catch (Exception ex) {
                Shared.RuntimeHealth.Report("lord-package-manifest", false,
                    "Existing package metadata could not be read; candidate rejected: " + ex);
                return false;
            }
        }
    }
}
