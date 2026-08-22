// Feature: Persist named AIV/AIC/rotation presets per AI lord.
using BepInEx.Logging;
using CrusaderDE;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace BugfixesAndQoL
{
    internal sealed class AivAicPresetStore
    {
        internal const int MaximumAivEntries = 50;
        internal const int MaximumPresetNameLength = 64;

        private const int SchemaVersion = 1;
        private const int MaximumPresetsPerLord = 200;
        private const long MaximumStoreBytes = 16L * 1024L * 1024L;
        private const string StoreFileName = "AivAicPresets.json";
        private readonly Dictionary<string, List<AivAicPresetDefinition>> presetsByLord =
            new Dictionary<string, List<AivAicPresetDefinition>>(StringComparer.OrdinalIgnoreCase);
        private readonly ManualLogSource log;
        private readonly string storePath;

        public AivAicPresetStore(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            storePath = Path.Combine(GetPluginDirectory(), "LobbyModSettings", StoreFileName);
            Load();
        }

        public IReadOnlyList<AivAicPresetDefinition> GetPresets(string lordKey)
        {
            return !string.IsNullOrEmpty(lordKey) && presetsByLord.TryGetValue(lordKey, out var presets)
                ? presets.AsReadOnly()
                : Array.Empty<AivAicPresetDefinition>();
        }

        public AivAicPresetDefinition Find(string lordKey, string name)
        {
            if (string.IsNullOrEmpty(lordKey) || string.IsNullOrWhiteSpace(name) ||
                !presetsByLord.TryGetValue(lordKey, out var presets))
                return null;

            string normalized = name.Trim();
            return presets.Find(preset =>
                string.Equals(preset.Name, normalized, StringComparison.OrdinalIgnoreCase));
        }

        public AivAicPresetDefinition Save(string lordKey, string name, FRONT_Multiplayer.MPAIVInfo info)
        {
            if (string.IsNullOrEmpty(lordKey))
                throw new InvalidOperationException("The AI lord has no stable preset identity.");
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            string normalizedName = (name ?? string.Empty).Trim();
            if (normalizedName.Length == 0 || normalizedName.Length > MaximumPresetNameLength)
                throw new InvalidDataException("The preset name is empty or too long.");
            if ((info.aivs?.Count ?? 0) > MaximumAivEntries)
                throw new InvalidDataException("The AIV selection exceeds the preset limit.");

            if (!presetsByLord.TryGetValue(lordKey, out var presets))
            {
                presets = new List<AivAicPresetDefinition>();
                presetsByLord.Add(lordKey, presets);
            }

            AivAicPresetDefinition preset = Find(lordKey, normalizedName);
            if (preset == null)
            {
                if (presets.Count >= MaximumPresetsPerLord)
                    throw new InvalidDataException("The preset limit for this lord has been reached.");
                preset = new AivAicPresetDefinition();
                presets.Add(preset);
            }

            preset.Name = normalizedName;
            preset.SavedUtc = DateTime.UtcNow;
            preset.Rotation = info.rotation;
            preset.UseVanillaAic = info.builtInLord || info.lordConfig == null;
            preset.Aic = preset.UseVanillaAic ? null : CaptureAic(info.lordConfig);
            preset.Aivs.Clear();
            if (info.aivs != null)
            {
                foreach (CustomisationFileManager.CustomAIV aiv in info.aivs)
                {
                    if (aiv != null)
                        preset.Aivs.Add(CaptureAiv(aiv));
                }
            }

            Write();
            return preset;
        }

        public bool Delete(string lordKey, string name)
        {
            if (string.IsNullOrEmpty(lordKey) || !presetsByLord.TryGetValue(lordKey, out var presets))
                return false;
            int removed = presets.RemoveAll(preset =>
                string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
                return false;
            if (presets.Count == 0)
                presetsByLord.Remove(lordKey);
            Write();
            return true;
        }

        public AivAicPresetApplyResult Apply(
            AivAicPresetDefinition preset,
            FRONT_Multiplayer.MPAIVInfo info,
            IList<CustomisationFileManager.CustomAIV> availableAivs,
            IList<CustomisationFileManager.CustomLordConfig> availableAics,
            int maximumAivs)
        {
            if (preset == null || info == null)
                throw new ArgumentNullException(preset == null ? nameof(preset) : nameof(info));

            int safeMaximum = Math.Max(0, Math.Min(maximumAivs, MaximumAivEntries));
            var resolvedAivs = new List<CustomisationFileManager.CustomAIV>();
            int missingAivs = 0;
            int truncatedAivs = 0;
            foreach (AivAicAssetReference reference in preset.Aivs)
            {
                CustomisationFileManager.CustomAIV resolved = ResolveAiv(reference, availableAivs);
                if (resolved == null)
                {
                    missingAivs++;
                    continue;
                }
                if (resolvedAivs.Count >= safeMaximum)
                {
                    truncatedAivs++;
                    continue;
                }
                // Vanilla disallows duplicate checksums, so loaded presets follow the same rule.
                if (!resolvedAivs.Exists(item => item.checksum == resolved.checksum))
                    resolvedAivs.Add(resolved);
            }

            info.builtIn = false;
            info.community = false;
            info.historical = false;
            info.aivs.Clear();
            info.aivs.AddRange(resolvedAivs);
            info.rotation = preset.Rotation;

            bool missingAic = false;
            if (preset.UseVanillaAic)
            {
                info.builtInLord = true;
                info.lordConfig = null;
            }
            else
            {
                CustomisationFileManager.CustomLordConfig resolvedAic = ResolveAic(preset.Aic, availableAics);
                missingAic = resolvedAic == null;
                info.builtInLord = missingAic;
                info.lordConfig = resolvedAic;
            }

            return new AivAicPresetApplyResult(resolvedAivs.Count, missingAivs, truncatedAivs, missingAic);
        }

        internal static string BuildLordKey(FRONT_Multiplayer.MPAIVInfo info)
        {
            if (info == null)
                return string.Empty;
            return !string.IsNullOrEmpty(info.lordName)
                ? "custom:" + info.lordName
                : "builtin:" + info.lordType.ToString(CultureInfo.InvariantCulture);
        }

        private static AivAicAssetReference CaptureAiv(CustomisationFileManager.CustomAIV aiv)
        {
            return new AivAicAssetReference
            {
                BuiltIn = aiv.builtIn,
                Workshop = aiv.workshop,
                LordType = aiv.lordType,
                Name = aiv.AIVName ?? string.Empty,
                Path = aiv.builtIn ? string.Empty : BuildAssetPath(aiv.path, aiv.AIVName, ".aivjson"),
                Checksum = aiv.checksum.ToString("X16", CultureInfo.InvariantCulture),
            };
        }

        private static AivAicAssetReference CaptureAic(CustomisationFileManager.CustomLordConfig aic)
        {
            return new AivAicAssetReference
            {
                BuiltIn = false,
                Workshop = aic.workshop,
                LordType = aic.lordType,
                Name = aic.name ?? string.Empty,
                Path = BuildAssetPath(aic.path, aic.name, ".lordjson"),
                Checksum = aic.checksum.ToString("X16", CultureInfo.InvariantCulture),
            };
        }

        private static CustomisationFileManager.CustomAIV ResolveAiv(
            AivAicAssetReference reference,
            IList<CustomisationFileManager.CustomAIV> available)
        {
            if (reference == null || available == null)
                return null;
            ulong checksum = ParseChecksum(reference.Checksum);
            foreach (CustomisationFileManager.CustomAIV candidate in available)
            {
                if (candidate == null || candidate.builtIn != reference.BuiltIn)
                    continue;
                if (reference.BuiltIn && candidate.lordType == reference.LordType && candidate.checksum == checksum)
                    return candidate;
                if (!reference.BuiltIn && PathsEqual(
                        BuildAssetPath(candidate.path, candidate.AIVName, ".aivjson"), reference.Path))
                    return candidate;
            }
            if (!reference.BuiltIn)
            {
                foreach (CustomisationFileManager.CustomAIV candidate in available)
                {
                    if (candidate != null && !candidate.builtIn && candidate.workshop == reference.Workshop &&
                        candidate.checksum == checksum && string.Equals(
                            candidate.AIVName, reference.Name, StringComparison.OrdinalIgnoreCase))
                        return candidate;
                }
            }
            return null;
        }

        private static CustomisationFileManager.CustomLordConfig ResolveAic(
            AivAicAssetReference reference,
            IList<CustomisationFileManager.CustomLordConfig> available)
        {
            if (reference == null || available == null)
                return null;
            ulong checksum = ParseChecksum(reference.Checksum);
            foreach (CustomisationFileManager.CustomLordConfig candidate in available)
            {
                if (candidate != null && PathsEqual(
                        BuildAssetPath(candidate.path, candidate.name, ".lordjson"), reference.Path))
                    return candidate;
            }
            foreach (CustomisationFileManager.CustomLordConfig candidate in available)
            {
                if (candidate != null && candidate.workshop == reference.Workshop &&
                    candidate.checksum == checksum && string.Equals(
                        candidate.name, reference.Name, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
            return null;
        }

        private void Load()
        {
            presetsByLord.Clear();
            if (!File.Exists(storePath))
                return;
            try
            {
                if (new FileInfo(storePath).Length > MaximumStoreBytes)
                    throw new InvalidDataException("The preset file is too large.");
                object rootValue = Shared.DependencyFreeJson.Parse(File.ReadAllText(storePath));
                if (!(rootValue is Dictionary<string, object> root) || ReadInt(root, "version") != SchemaVersion ||
                    !root.TryGetValue("lords", out object lordsValue) ||
                    !(lordsValue is Dictionary<string, object> lords))
                    throw new InvalidDataException("The AIV/AIC preset root is invalid.");

                foreach (KeyValuePair<string, object> lordEntry in lords)
                {
                    if (string.IsNullOrWhiteSpace(lordEntry.Key) || !(lordEntry.Value is List<object> values))
                        continue;
                    var parsed = new List<AivAicPresetDefinition>();
                    foreach (object value in values)
                    {
                        if (parsed.Count >= MaximumPresetsPerLord)
                            break;
                        try
                        {
                            AivAicPresetDefinition preset = ParsePreset(value);
                            if (preset != null && !parsed.Exists(item => string.Equals(
                                    item.Name, preset.Name, StringComparison.OrdinalIgnoreCase)))
                                parsed.Add(preset);
                        }
                        catch (Exception ex)
                        {
                            Shared.DebugLogHelper.LogWarning(log,
                                $"Bugfixes and QoL ignored an invalid AIV/AIC preset for {lordEntry.Key}: {ex.Message}");
                        }
                    }
                    if (parsed.Count > 0)
                        presetsByLord[lordEntry.Key] = parsed;
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarning(log,
                    $"Bugfixes and QoL could not load AIV/AIC presets from {storePath}: {ex.Message}");
            }
        }

        private void Write()
        {
            string directory = Path.GetDirectoryName(storePath);
            string temporaryPath = storePath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                var lords = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, List<AivAicPresetDefinition>> lord in presetsByLord)
                {
                    var values = new List<object>();
                    foreach (AivAicPresetDefinition preset in lord.Value)
                        values.Add(SerializePreset(preset));
                    lords[lord.Key] = values;
                }
                var root = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["version"] = SchemaVersion,
                    ["lords"] = lords,
                };
                Directory.CreateDirectory(directory);
                File.WriteAllText(temporaryPath, Shared.DependencyFreeJson.Serialize(root),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                if (File.Exists(storePath))
                    File.Replace(temporaryPath, storePath, null);
                else
                    File.Move(temporaryPath, storePath);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch (Exception ex)
                {
                    Shared.DebugLogHelper.LogWarning(log,
                        $"Bugfixes and QoL could not remove temporary AIV/AIC preset file: {ex.Message}");
                }
            }
        }

        private static Dictionary<string, object> SerializePreset(AivAicPresetDefinition preset)
        {
            var aivs = new List<object>();
            foreach (AivAicAssetReference reference in preset.Aivs)
                aivs.Add(SerializeReference(reference));
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["name"] = preset.Name,
                ["savedUtc"] = preset.SavedUtc.ToString("O", CultureInfo.InvariantCulture),
                ["rotation"] = preset.Rotation,
                ["useVanillaAic"] = preset.UseVanillaAic,
                ["aic"] = preset.Aic == null ? null : SerializeReference(preset.Aic),
                ["aivs"] = aivs,
            };
        }

        private static Dictionary<string, object> SerializeReference(AivAicAssetReference reference)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["builtIn"] = reference.BuiltIn,
                ["workshop"] = reference.Workshop,
                ["lordType"] = reference.LordType,
                ["name"] = reference.Name,
                ["path"] = reference.Path,
                ["checksum"] = reference.Checksum,
            };
        }

        private static AivAicPresetDefinition ParsePreset(object value)
        {
            if (!(value is Dictionary<string, object> map))
                throw new InvalidDataException("Preset must be an object.");
            string name = ReadString(map, "name").Trim();
            if (name.Length == 0 || name.Length > MaximumPresetNameLength)
                throw new InvalidDataException("Preset name is invalid.");
            if (!DateTime.TryParse(ReadString(map, "savedUtc"), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime savedUtc))
                throw new InvalidDataException("Preset timestamp is invalid.");
            if (!map.TryGetValue("aivs", out object aivsValue) || !(aivsValue is List<object> aivValues) ||
                aivValues.Count > 999)
                throw new InvalidDataException("Preset AIV list is invalid.");

            var preset = new AivAicPresetDefinition
            {
                Name = name,
                SavedUtc = savedUtc,
                Rotation = ReadInt(map, "rotation"),
                UseVanillaAic = ReadBool(map, "useVanillaAic"),
            };
            if (preset.Rotation < 0 || preset.Rotation > 4)
                throw new InvalidDataException("Preset rotation is invalid.");
            foreach (object aivValue in aivValues)
                preset.Aivs.Add(ParseReference(aivValue));
            if (!preset.UseVanillaAic)
            {
                if (!map.TryGetValue("aic", out object aicValue) || aicValue == null)
                    throw new InvalidDataException("Custom AIC reference is missing.");
                preset.Aic = ParseReference(aicValue);
            }
            return preset;
        }

        private static AivAicAssetReference ParseReference(object value)
        {
            if (!(value is Dictionary<string, object> map))
                throw new InvalidDataException("Asset reference must be an object.");
            string checksum = ReadString(map, "checksum");
            ParseChecksum(checksum);
            return new AivAicAssetReference
            {
                BuiltIn = ReadBool(map, "builtIn"),
                Workshop = ReadBool(map, "workshop"),
                LordType = ReadInt(map, "lordType"),
                Name = ReadString(map, "name"),
                Path = ReadString(map, "path"),
                Checksum = checksum,
            };
        }

        private static string ReadString(Dictionary<string, object> map, string key)
        {
            if (!map.TryGetValue(key, out object value) || !(value is string text))
                throw new InvalidDataException("Missing string field '" + key + "'.");
            return text;
        }

        private static int ReadInt(Dictionary<string, object> map, string key)
        {
            if (!map.TryGetValue(key, out object value))
                throw new InvalidDataException("Missing integer field '" + key + "'.");
            if (value is int integer32)
                return integer32;
            if (value is long integer && integer >= int.MinValue && integer <= int.MaxValue)
                return (int)integer;
            if (value is ulong unsigned && unsigned <= int.MaxValue)
                return (int)unsigned;
            throw new InvalidDataException("Field '" + key + "' must be an integer.");
        }

        private static bool ReadBool(Dictionary<string, object> map, string key)
        {
            if (map.TryGetValue(key, out object value) && value is bool boolean)
                return boolean;
            throw new InvalidDataException("Field '" + key + "' must be a boolean.");
        }

        private static ulong ParseChecksum(string value)
        {
            if (!ulong.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong checksum))
                throw new InvalidDataException("Asset checksum is invalid.");
            return checksum;
        }

        private static string BuildAssetPath(string directory, string name, string extension)
        {
            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(name))
                return string.Empty;
            try
            {
                return Path.GetFullPath(Path.Combine(directory, name + extension))
                    .Replace('/', '\\').TrimEnd('\\');
            }
            catch
            {
                return Path.Combine(directory, name + extension).Replace('/', '\\').TrimEnd('\\');
            }
        }

        private static bool PathsEqual(string left, string right)
        {
            return !string.IsNullOrEmpty(left) && !string.IsNullOrEmpty(right) &&
                string.Equals(left, right.Replace('/', '\\').TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetPluginDirectory()
        {
            try
            {
                string location = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(location))
                    return Path.GetDirectoryName(location);
            }
            catch
            {
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }

    internal sealed class AivAicPresetDefinition
    {
        public string Name { get; set; } = string.Empty;
        public DateTime SavedUtc { get; set; }
        public int Rotation { get; set; }
        public bool UseVanillaAic { get; set; }
        public AivAicAssetReference Aic { get; set; }
        public List<AivAicAssetReference> Aivs { get; } = new List<AivAicAssetReference>();
    }

    internal sealed class AivAicAssetReference
    {
        public bool BuiltIn { get; set; }
        public bool Workshop { get; set; }
        public int LordType { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Checksum { get; set; } = string.Empty;
    }

    internal readonly struct AivAicPresetApplyResult
    {
        public AivAicPresetApplyResult(int loadedAivs, int missingAivs, int truncatedAivs, bool missingAic)
        {
            LoadedAivs = loadedAivs;
            MissingAivs = missingAivs;
            TruncatedAivs = truncatedAivs;
            MissingAic = missingAic;
        }

        public int LoadedAivs { get; }
        public int MissingAivs { get; }
        public int TruncatedAivs { get; }
        public bool MissingAic { get; }
    }
}
