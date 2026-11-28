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
            FixesLordPreferencesBridge fixes, Candidate source)
        {
            LordPackageFileState package = LordPackageFingerprint.Capture(source.Config.path, source.Config.name);
            if (!package.GameplayPaths.Any(path => SameName(path, source.Config.name + ".lordjson")))
                throw new FileNotFoundException("The selected Lord configuration is no longer in the package.",
                    Path.Combine(source.Config.path, source.Config.name + ".lordjson"));
            string sidecar = package.SynchronizedPaths.FirstOrDefault(path =>
                SameName(path, source.Config.name + ".modlord.json"));
            string preferences = package.SynchronizedPaths.FirstOrDefault(path =>
                SameName(path, "Override/Fixes/preferences.json"));
            string fixesJson = preferences == null ? null :
                File.ReadAllText(Path.Combine(source.Config.path, preferences), new UTF8Encoding(false, true));
            if (fixes.Installed) fixesJson = fixes.Normalize(fixesJson);
            else if (fixesJson != null && !(Shared.DependencyFreeJson.Parse(fixesJson) is Dictionary<string, object>))
                throw new InvalidDataException("Expected a Fixes preference object: " + preferences);
            var slot = SavedIdentity(info, playerId);
            slot.Presentation = TrailLordMediaStore.Capture(source.Name, source.Config.path);
            slot.RequiresInstalledPackage = package.HasUnsupportedGameplayFiles;
            slot.PackageDigest = package.HasUnsupportedGameplayFiles ? package.Digest : null;
            slot.ModLordJson = sidecar == null ? null :
                LordDataSnapshot.ReadModLordFile(Path.Combine(source.Config.path, sidecar));
            LordDataSnapshot.ValidateModLord(slot.ModLordJson);
            slot.FixesJson = fixesJson;
            slot.FixesSources = fixes.CapturePackageSources(source.Name, source.Config.lordType,
                info.lordConfig.lordData.opponent_type, fixesJson);
            return slot;
        }

        internal static Candidate FindMakerSource(FRONT_Multiplayer.MPAIVInfo info)
        {
            string name = TrailLordSourceSelector.ResolveLordName(info.lordName,
                info.lordType, ConfigSettings.extendedLordPaths);
            Candidate[] candidates = MakerCandidates(info, name);
            if (!string.IsNullOrWhiteSpace(info.lordConfig.path))
            {
                string selected = Path.GetFullPath(info.lordConfig.path).TrimEnd('\\', '/');
                candidates = candidates.Where(candidate => SameName(
                    Path.GetFullPath(candidate.Config.path).TrimEnd('\\', '/'), selected)).ToArray();
            }
            return candidates.Length == 1 ? candidates[0] : null;
        }

        internal static void RefreshMakerSelection(HUD_IngameMenu.RestartSkirmishMapInfo restart)
        {
            if (restart?.aivs == null) return;
            var manager = CustomisationFileManager.Instance;
            foreach (var info in restart.aivs)
            {
                if (info == null) continue;
                bool named = !string.IsNullOrWhiteSpace(info.lordName);
                if (!named && (info.lordType < 0 || info.lordType >= ConfigSettings.extendedLordPaths.Length))
                    continue;
                string name = named ? info.lordName : ConfigSettings.extendedLordPaths[info.lordType];
                if (!info.builtInLord && info.lordConfig != null)
                {
                    Candidate[] candidates = MakerCandidates(info, name);
                    if (candidates.Length > 1) continue;
                    if (candidates.Length == 1) info.lordConfig = candidates[0].Config;
                }

                if (named)
                {
                    byte[] portrait = null;
                    var image = manager.GetCustomLordImage(name, ref portrait);
                    if (portrait != null)
                    {
                        info.imageData = portrait;
                        info.image = image;
                    }
                }
                var available = named
                    ? manager.GetCustomLords().Where(lord => SameName(lord.lordName, name))
                        .SelectMany(lord => lord.aivs ?? new List<CustomisationFileManager.CustomAIV>())
                    : manager.getLordAIVList(info.lordType) ?? Enumerable.Empty<CustomisationFileManager.CustomAIV>();
                if (info.aivs == null) continue;
                for (int index = 0; index < info.aivs.Count; index++)
                {
                    var saved = info.aivs[index];
                    if (saved == null || saved.builtIn || string.IsNullOrWhiteSpace(saved.AIVName))
                        continue;
                    var matches = available.Where(aiv => aiv != null && !aiv.builtIn &&
                        SameName(aiv.AIVName, saved.AIVName)).Take(2).ToArray();
                    if (matches.Length == 1) info.aivs[index] = matches[0];
                }
            }
        }

        private static Candidate[] MakerCandidates(FRONT_Multiplayer.MPAIVInfo info, string name)
        {
            var manager = CustomisationFileManager.Instance;
            bool named = !string.IsNullOrWhiteSpace(info.lordName);
            var configs = named
                ? manager.GetCustomLords().Where(lord => SameName(lord.lordName, name))
                    .SelectMany(lord => lord.configs ?? new List<CustomisationFileManager.CustomLordConfig>())
                : manager.getLordLordList(info.lordType) ?? Enumerable.Empty<CustomisationFileManager.CustomLordConfig>();
            return configs.Where(config => config != null && !string.IsNullOrWhiteSpace(config.path) &&
                SameName(config.name, info.lordConfig.name) &&
                (named || config.lordType == info.lordType))
                .GroupBy(config => Path.GetFullPath(config.path).TrimEnd('\\', '/'), StringComparer.OrdinalIgnoreCase)
                .Select(group => new Candidate { Name = name, Config = group.First() }).ToArray();
        }

        private static bool SameName(string left, string right) =>
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        internal static bool TryResolve(TrailLordSlot slot, out string internalName,
            out string requiredLordPath, out string reason)
        {
            internalName = null;
            requiredLordPath = null;
            reason = string.Empty;
            Candidate[] candidates = FindCandidates(slot.ConfigChecksum,
                    slot.RequiresInstalledPackage ? Array.Empty<string>() : slot.AivChecksums)
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
            if (configs == null) return;
            if (requiredAivs.Count != 0 && (aivs == null ||
                requiredAivs.Any(value => !aivs.Any(aiv => aiv.checksum.ToString() == value)))) return;
            foreach (var config in configs.Where(item => item != null &&
                string.Equals(item.checksum.ToString(), checksum, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(item.path)))
                result.Add(new Candidate { Name = name, Config = config });
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
