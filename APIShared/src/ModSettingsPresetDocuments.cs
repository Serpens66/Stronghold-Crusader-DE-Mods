#pragma warning disable 1591 // Public schema members are documented by the APIShared preset guide.
using BepInEx.Logging;
using SHCDESE.API.Components.ModManager;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Shared
{
    /// <summary>Typed APIShared endpoint consumed by optional mission-setting providers such as ExtendedData.</summary>
    public interface IModSettingsPresetEndpoint
    {
        Dictionary<string, byte[]> System_CreateDisabledMissionPresetSnapshot();
        void System_EnterMissionPreset(Dictionary<string, byte[]> snapshot, string label, bool editable);
        void System_ExitMissionPreset();
        bool IsMissionPresetActive { get; }
    }

    /// <summary>Determines how a published preset resolves one selected setting.</summary>
    public enum PublishedPresetValueMode
    {
        ModDefault = 0,
        Player = 1,
        Fixed = 2,
    }

    /// <summary>Describes the ownership of a persistent lobby setting.</summary>
    public enum PresetSettingScope
    {
        Host = 0,
        Player = 1,
        Local = 2,
    }

    /// <summary>Identifies who owns a preset and whether it may be replaced by the player.</summary>
    public enum ModSettingsPresetSourceKind
    {
        Personal = 0,
        Bundled = 1,
        External = 2,
        Mission = 3,
    }

    /// <summary>One persistent setting exposed to preset authoring UI.</summary>
    public sealed class PresetSettingDescriptor
    {
        public string PropertyName { get; internal set; } = string.Empty;
        public Type PropertyType { get; internal set; }
        public PresetSettingScope Scope { get; internal set; }
    }

    /// <summary>One author-selected setting passed to personal-preset persistence.</summary>
    public sealed class PresetSaveSelection
    {
        public string PropertyName { get; set; } = string.Empty;
        public PublishedPresetValueMode Mode { get; set; } = PublishedPresetValueMode.Fixed;
    }

    /// <summary>Mutable row model used by the standard personal-preset save dialog.</summary>
    public sealed class PresetSaveSettingViewModel : INotifyPropertyChanged
    {
        private bool isSelected;
        private int selectedModeIndex = (int)PublishedPresetValueMode.Fixed;

        public PresetSaveSettingViewModel(PresetSettingDescriptor descriptor)
            : this(descriptor, descriptor?.Scope.ToString(), null)
        {
        }

        public PresetSaveSettingViewModel(
            PresetSettingDescriptor descriptor,
            string scopeText,
            string[] modeOptions)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            PropertyName = descriptor.PropertyName;
            Scope = descriptor.Scope;
            ScopeText = string.IsNullOrWhiteSpace(scopeText) ? descriptor.Scope.ToString() : scopeText;
            ModeOptions = modeOptions != null && modeOptions.Length == 3
                ? (string[])modeOptions.Clone()
                : new[] { "ModDefault", "Player", "Fixed" };
        }

        public string PropertyName { get; }
        public PresetSettingScope Scope { get; }
        public string ScopeText { get; }
        public string[] ModeOptions { get; }

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (isSelected == value) return;
                isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public int SelectedModeIndex
        {
            get => selectedModeIndex;
            set
            {
                if (value < 0 || value > 2) value = (int)PublishedPresetValueMode.Fixed;
                if (selectedModeIndex == value) return;
                selectedModeIndex = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedModeIndex)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public PresetSaveSelection ToSelection() => new PresetSaveSelection
        {
            PropertyName = PropertyName,
            Mode = (PublishedPresetValueMode)SelectedModeIndex,
        };
    }

    /// <summary>One setting selected by a published preset.</summary>
    public sealed class PublishedPresetSetting
    {
        public PublishedPresetValueMode Mode { get; set; }
        public object Value { get; set; }
    }

    /// <summary>A validated, provider-qualified preset offered by a target mod.</summary>
    public sealed class PublishedModSettingsPreset
    {
        public ModSettingsPresetSourceKind SourceKind { get; internal set; }
        public string ProviderGuid { get; internal set; } = string.Empty;
        public string ProviderName { get; internal set; } = string.Empty;
        public string TargetGuid { get; internal set; } = string.Empty;
        public string Id { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public string Description { get; internal set; } = string.Empty;
        public string MinimumTargetVersion { get; internal set; } = string.Empty;
        public string MaximumTargetVersion { get; internal set; } = string.Empty;
        public IReadOnlyDictionary<string, PublishedPresetSetting> Settings { get; internal set; } =
            new Dictionary<string, PublishedPresetSetting>(StringComparer.Ordinal);
        public string SourcePath { get; internal set; } = string.Empty;

        public bool CanOverwrite => SourceKind == ModSettingsPresetSourceKind.Personal;
        public string StableId => SourceKind + "\n" + ProviderGuid + "\n" + TargetGuid + "\n" + Id;
    }

    /// <summary>One source-labelled row shown by the standard preset load/save dialogs.</summary>
    public sealed class ModSettingsPresetListEntry
    {
        internal PublishedModSettingsPreset Preset { get; set; }
        public string StableId => Preset?.StableId ?? string.Empty;
        public string Name => Preset?.Name ?? string.Empty;
        public string Description => Preset?.Description ?? string.Empty;
        public string ProviderGuid => Preset?.ProviderGuid ?? string.Empty;
        public string ProviderName => Preset?.ProviderName ?? string.Empty;
        public ModSettingsPresetSourceKind SourceKind =>
            Preset?.SourceKind ?? ModSettingsPresetSourceKind.Personal;
        public bool CanOverwrite => Preset?.CanOverwrite == true;
        public string SourceLabel { get; internal set; } = string.Empty;
        public string DisplayText => SourceLabel + " · " + Name;
        public override string ToString() => DisplayText;
    }

    /// <summary>A deliberate destination offered by the personal-preset save dialog.</summary>
    public sealed class ModSettingsPresetSaveTarget
    {
        internal PublishedModSettingsPreset Preset { get; set; }
        public bool IsNew => Preset == null;
        public string Name => Preset?.Name ?? string.Empty;
        public string StableId => Preset?.StableId ?? string.Empty;
        public string DisplayText { get; internal set; } = string.Empty;
        public override string ToString() => DisplayText;
    }

    /// <summary>Shared JSON contract for loose <c>preset_*.json</c> files.</summary>
    public static class ModSettingsPresetJson
    {
        public const int SchemaVersion = 1;
        public const string EncodedMessagePackPrefix = "messagepack-base64:";
        public const int MaximumSettings = 1024;

        public static PublishedModSettingsPreset Parse(
            string json,
            string providerGuid,
            string providerName,
            string expectedTargetGuid,
            string sourcePath)
        {
            if (!(DependencyFreeJson.Parse(json) is Dictionary<string, object> root))
                throw new InvalidDataException("Preset JSON root must be an object.");
            RequireKnownKeys(
                root,
                "preset",
                "schemaVersion", "id", "name", "description", "targetGuid",
                "minimumTargetVersion", "maximumTargetVersion", "settings");
            RequireExactInt(root, "schemaVersion", SchemaVersion);

            string id = RequireIdentifier(root, "id", 128);
            string name = RequireText(root, "name", 256);
            string targetGuid = RequireIdentifier(root, "targetGuid", 200);
            if (!string.Equals(targetGuid, expectedTargetGuid, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Preset targetGuid does not match its Override directory.");

            string description = OptionalText(root, "description", 2048);
            string minimum = OptionalText(root, "minimumTargetVersion", 80);
            string maximum = OptionalText(root, "maximumTargetVersion", 80);
            if (!root.TryGetValue("settings", out object settingsValue) ||
                !(settingsValue is Dictionary<string, object> rawSettings))
            {
                throw new InvalidDataException("Preset JSON requires a settings object.");
            }
            if (rawSettings.Count == 0 || rawSettings.Count > MaximumSettings)
                throw new InvalidDataException("Preset settings count is outside the supported range.");

            var settings = new Dictionary<string, PublishedPresetSetting>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> entry in rawSettings)
            {
                string propertyName = ValidatePropertyName(entry.Key);
                if (!(entry.Value is Dictionary<string, object> rawSetting))
                    throw new InvalidDataException("Preset setting [" + propertyName + "] must be an object.");
                RequireKnownKeys(rawSetting, "setting " + propertyName, "mode", "value");
                string modeText = RequireText(rawSetting, "mode", 32);
                PublishedPresetValueMode mode;
                switch (modeText)
                {
                    case "modDefault": mode = PublishedPresetValueMode.ModDefault; break;
                    case "player": mode = PublishedPresetValueMode.Player; break;
                    case "fixed": mode = PublishedPresetValueMode.Fixed; break;
                    default: throw new InvalidDataException("Unknown preset mode [" + modeText + "] for [" + propertyName + "].");
                }

                bool hasValue = rawSetting.TryGetValue("value", out object fixedValue);
                if (mode == PublishedPresetValueMode.Fixed && (!hasValue || fixedValue == null))
                    throw new InvalidDataException("Fixed preset setting [" + propertyName + "] requires a non-null value.");
                if (mode != PublishedPresetValueMode.Fixed && hasValue)
                    throw new InvalidDataException("Only fixed preset settings may contain value.");
                settings.Add(propertyName, new PublishedPresetSetting { Mode = mode, Value = fixedValue });
            }

            return new PublishedModSettingsPreset
            {
                ProviderGuid = providerGuid ?? string.Empty,
                ProviderName = string.IsNullOrWhiteSpace(providerName) ? providerGuid ?? string.Empty : providerName,
                TargetGuid = targetGuid,
                Id = id,
                Name = name,
                Description = description,
                MinimumTargetVersion = minimum,
                MaximumTargetVersion = maximum,
                Settings = settings,
                SourcePath = sourcePath ?? string.Empty,
            };
        }

        public static string Serialize(
            string targetGuid,
            string id,
            string name,
            string description,
            string minimumTargetVersion,
            string maximumTargetVersion,
            IReadOnlyDictionary<string, PublishedPresetSetting> settings)
        {
            targetGuid = ValidateIdentifier(targetGuid, "targetGuid", 200);
            id = ValidateIdentifier(id, "id", 128);
            name = ValidateText(name, "name", 256);
            if (settings == null || settings.Count == 0 || settings.Count > MaximumSettings)
                throw new InvalidDataException("Preset settings count is outside the supported range.");

            var serializedSettings = new OrderedDictionary(StringComparer.Ordinal);
            foreach (KeyValuePair<string, PublishedPresetSetting> entry in settings.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                string propertyName = ValidatePropertyName(entry.Key);
                PublishedPresetSetting setting = entry.Value ?? throw new InvalidDataException("Preset setting is null.");
                var value = new OrderedDictionary(StringComparer.Ordinal)
                {
                    { "mode", ToJsonMode(setting.Mode) },
                };
                if (setting.Mode == PublishedPresetValueMode.Fixed)
                {
                    if (setting.Value == null)
                        throw new InvalidDataException("Fixed preset setting [" + propertyName + "] is null.");
                    value.Add("value", setting.Value);
                }
                serializedSettings.Add(propertyName, value);
            }

            var root = new OrderedDictionary(StringComparer.Ordinal)
            {
                { "schemaVersion", SchemaVersion },
                { "id", id },
                { "name", name },
            };
            if (!string.IsNullOrWhiteSpace(description)) root.Add("description", description.Trim());
            root.Add("targetGuid", targetGuid);
            if (!string.IsNullOrWhiteSpace(minimumTargetVersion)) root.Add("minimumTargetVersion", minimumTargetVersion.Trim());
            if (!string.IsNullOrWhiteSpace(maximumTargetVersion)) root.Add("maximumTargetVersion", maximumTargetVersion.Trim());
            root.Add("settings", serializedSettings);
            return DependencyFreeJson.Serialize(root);
        }

        public static object ConvertValue(object value, Type targetType)
        {
            if (value == null) throw new InvalidDataException("Null cannot be assigned to [" + targetType.FullName + "].");
            if (targetType != typeof(string) && value is string encoded && encoded.StartsWith(EncodedMessagePackPrefix, StringComparison.Ordinal))
            {
                byte[] bytes = Convert.FromBase64String(encoded.Substring(EncodedMessagePackPrefix.Length));
                return MessagePack.MessagePackSerializer.Deserialize(targetType, bytes);
            }

            Type effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (effectiveType.IsInstanceOfType(value)) return value;
            if (effectiveType.IsArray)
            {
                IEnumerable sequence = value as IEnumerable;
                if (effectiveType.GetArrayRank() != 1 || effectiveType.GetElementType().IsArray ||
                    sequence == null || value is string)
                {
                    throw new InvalidDataException("Only one-dimensional arrays of directly supported values may use JSON arrays.");
                }
                Type elementType = effectiveType.GetElementType();
                var converted = new List<object>();
                foreach (object item in sequence) converted.Add(ConvertValue(item, elementType));
                Array array = Array.CreateInstance(elementType, converted.Count);
                for (int index = 0; index < converted.Count; index++) array.SetValue(converted[index], index);
                return array;
            }
            if (effectiveType.IsEnum)
            {
                if (value is string enumName)
                {
                    if (long.TryParse(enumName, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                        throw new InvalidDataException("Numeric enum strings are not supported.");
                    try
                    {
                        return Enum.Parse(effectiveType, enumName, ignoreCase: false);
                    }
                    catch (ArgumentException exception)
                    {
                        throw new InvalidDataException("Unknown enum name [" + enumName + "] for [" + effectiveType.FullName + "].", exception);
                    }
                }
                Type sourceType = value.GetType();
                bool supportedPrimitive = sourceType == typeof(bool) || sourceType == typeof(byte) ||
                    sourceType == typeof(sbyte) || sourceType == typeof(short) || sourceType == typeof(ushort) ||
                    sourceType == typeof(int) || sourceType == typeof(uint) || sourceType == typeof(long) ||
                    sourceType == typeof(ulong);
                if (!supportedPrimitive)
                    throw new InvalidDataException("Enum values must use a declared name or an integral legacy representation.");
                byte[] primitive = MessagePack.MessagePackSerializer.Serialize(sourceType, value);
                return MessagePack.MessagePackSerializer.Deserialize(effectiveType, primitive);
            }
            return Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture);
        }

        public static object ToJsonValue(Type propertyType, object value)
        {
            if (value == null) throw new InvalidDataException("Preset values cannot be null.");
            Type type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            if (type.IsEnum) return value.ToString();
            if (IsJsonScalar(value)) return value;
            if (type.IsArray && type.GetArrayRank() == 1)
            {
                var items = new List<object>();
                foreach (object item in (IEnumerable)value)
                {
                    if (!IsJsonScalar(item) && !item.GetType().IsEnum)
                        return EncodeMessagePack(propertyType, value);
                    items.Add(item.GetType().IsEnum ? item.ToString() : item);
                }
                return items;
            }
            return EncodeMessagePack(propertyType, value);
        }

        private static string EncodeMessagePack(Type type, object value) =>
            EncodedMessagePackPrefix + Convert.ToBase64String(MessagePack.MessagePackSerializer.Serialize(type, value));
        private static bool IsJsonScalar(object value) => value is bool || value is string || value is byte || value is sbyte ||
            value is short || value is ushort || value is int || value is uint || value is long || value is ulong ||
            value is float || value is double || value is decimal;
        private static string ToJsonMode(PublishedPresetValueMode mode) => mode == PublishedPresetValueMode.ModDefault
            ? "modDefault" : mode == PublishedPresetValueMode.Player ? "player" : mode == PublishedPresetValueMode.Fixed
            ? "fixed" : throw new InvalidDataException("Unknown published preset mode.");
        private static void RequireExactInt(Dictionary<string, object> root, string key, int expected)
        {
            if (!root.TryGetValue(key, out object value) || !(value is int number) || number != expected)
                throw new InvalidDataException("Unsupported preset " + key + ".");
        }
        private static string RequireIdentifier(Dictionary<string, object> root, string key, int max) =>
            !root.TryGetValue(key, out object value) || !(value is string text)
                ? throw new InvalidDataException("Preset JSON requires string " + key + ".")
                : ValidateIdentifier(text, key, max);
        private static string RequireText(Dictionary<string, object> root, string key, int max) =>
            !root.TryGetValue(key, out object value) || !(value is string text)
                ? throw new InvalidDataException("Preset JSON requires string " + key + ".")
                : ValidateText(text, key, max);
        private static string OptionalText(Dictionary<string, object> root, string key, int max) =>
            !root.TryGetValue(key, out object value) ? string.Empty : value is string text
                ? ValidateOptionalText(text, key, max)
                : throw new InvalidDataException("Preset " + key + " must be a string.");
        private static string ValidateIdentifier(string value, string key, int max)
        {
            string text = ValidateText(value, key, max);
            if (text.Any(ch => char.IsControl(ch) || ch == '/' || ch == '\\'))
                throw new InvalidDataException("Preset " + key + " contains unsafe characters.");
            return text;
        }
        private static string ValidateText(string value, string key, int max)
        {
            string text = value?.Trim() ?? string.Empty;
            if (text.Length == 0 || text.Length > max) throw new InvalidDataException("Preset " + key + " is empty or too long.");
            return text;
        }
        private static string ValidateOptionalText(string value, string key, int max)
        {
            string text = value?.Trim() ?? string.Empty;
            if (text.Length > max) throw new InvalidDataException("Preset " + key + " is too long.");
            return text;
        }
        private static string ValidatePropertyName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 256 || !string.Equals(name, name.Trim(), StringComparison.Ordinal))
                throw new InvalidDataException("Preset contains an invalid property name.");
            return name;
        }

        private static void RequireKnownKeys(
            Dictionary<string, object> values,
            string context,
            params string[] allowedKeys)
        {
            var allowed = new HashSet<string>(allowedKeys, StringComparer.Ordinal);
            string unknown = values.Keys.FirstOrDefault(key => !allowed.Contains(key));
            if (unknown != null)
                throw new InvalidDataException("Unknown " + context + " member [" + unknown + "].");
        }
    }

    internal static class ModSettingsPresetCatalog
    {
        private const long MaximumFileBytes = 1024 * 1024;
        private const int MaximumFilesPerProvider = 512;
        private const int MaximumPresetsPerTarget = 2048;

        internal static string ValidateTargetGuid(string targetGuid) =>
            RequireSafePathSegment(targetGuid, "target plugin GUID");

        internal static void ValidatePersonalWritePath(
            string providerRoot,
            string personalDirectory,
            string path)
        {
            string root = Path.GetFullPath(providerRoot);
            string directory = Path.GetFullPath(personalDirectory);
            string fullPath = Path.GetFullPath(path);
            if (!IsAtOrBelow(root, directory) || !IsBelow(directory, fullPath))
                throw new InvalidDataException("Personal preset path leaves its target directory.");
            EnsureExistingPathSegmentsNoReparse(root, directory);
            if (File.Exists(fullPath) &&
                (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Personal preset paths may not use reparse points.");
            }
        }

        public static IReadOnlyList<PublishedModSettingsPreset> Discover(
            string targetGuid,
            Version targetVersion,
            string targetPluginDirectory,
            ManualLogSource log) => Discover(
                targetGuid,
                targetVersion,
                targetPluginDirectory,
                Path.Combine(targetPluginDirectory, "LobbyModSettings", "Presets", "Override", targetGuid),
                log);

        public static IReadOnlyList<PublishedModSettingsPreset> Discover(
            string targetGuid,
            Version targetVersion,
            string targetPluginDirectory,
            string personalPresetRoot,
            ManualLogSource log)
        {
            targetGuid = RequireSafePathSegment(targetGuid, "target plugin GUID");
            var providers = new List<PresetProvider>();
            var seenDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddProvider(
                providers,
                seenDirectories,
                "personal:" + targetGuid,
                "Personal",
                personalPresetRoot,
                ModSettingsPresetSourceKind.Personal,
                directoryIsTarget: true);
            AddProvider(
                providers,
                seenDirectories,
                targetGuid,
                targetGuid,
                targetPluginDirectory,
                ModSettingsPresetSourceKind.Bundled,
                directoryIsTarget: false);
            foreach (KeyValuePair<ModInfo, string> provider in GameAssetModManager.Instance.GetRegisteredAssetDirectories())
            {
                if (!Directory.Exists(provider.Value))
                {
                    if (string.Equals(Path.GetExtension(provider.Value), ".semod", StringComparison.OrdinalIgnoreCase))
                        DebugLogHelper.LogWarning(log, "Preset provider [" + provider.Key.Name + "] is a .semod archive and is skipped; published presets currently require a loose folder.");
                    continue;
                }
                AddProvider(
                    providers,
                    seenDirectories,
                    provider.Key.GUID,
                    provider.Key.Name,
                    provider.Value,
                    string.Equals(provider.Key.GUID, targetGuid, StringComparison.OrdinalIgnoreCase)
                        ? ModSettingsPresetSourceKind.Bundled
                        : ModSettingsPresetSourceKind.External,
                    directoryIsTarget: false);
            }

            var result = new List<PublishedModSettingsPreset>();
            var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PresetProvider provider in providers)
            {
                try
                {
                var providerPresets = new List<PublishedModSettingsPreset>();
                string root = Path.GetFullPath(provider.Root);
                string presetDirectory = provider.DirectoryIsTarget
                    ? root
                    : Path.GetFullPath(Path.Combine(root, "Override", targetGuid));
                if (!IsAtOrBelow(root, presetDirectory) || !Directory.Exists(presetDirectory)) continue;
                EnsureNoReparsePoints(root, presetDirectory);
                string[] files = Directory.GetFiles(presetDirectory, "preset_*.json", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                    .Take(MaximumFilesPerProvider + 1).ToArray();
                if (files.Length > MaximumFilesPerProvider)
                {
                    DebugLogHelper.LogError(log, "Preset provider [" + provider.Name + "] exceeds the per-target file limit and is ignored.");
                    continue;
                }
                foreach (string file in files)
                {
                    try
                    {
                        string fullPath = Path.GetFullPath(file);
                        if (!IsBelow(presetDirectory, fullPath)) throw new InvalidDataException("Preset path leaves its provider directory.");
                        EnsureNoReparsePoints(root, fullPath);
                        var info = new FileInfo(fullPath);
                        if (info.Length <= 0 || info.Length > MaximumFileBytes) throw new InvalidDataException("Preset file size is outside the supported range.");
                        PublishedModSettingsPreset preset = ModSettingsPresetJson.Parse(
                            ReadPresetText(fullPath), provider.Guid, provider.Name, targetGuid, fullPath);
                        preset.SourceKind = provider.SourceKind;
                        if (!MatchesVersion(targetVersion, preset.MinimumTargetVersion, preset.MaximumTargetVersion)) continue;
                        providerPresets.Add(preset);
                    }
                    catch (Exception exception)
                    {
                        DebugLogHelper.LogError(log, "Published preset [" + file + "] was rejected: " + exception.Message);
                    }
                }
                var duplicatedIds = new HashSet<string>(
                    providerPresets
                        .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                        .Where(group => group.Skip(1).Any())
                        .Select(group => group.Key),
                    StringComparer.OrdinalIgnoreCase);
                foreach (string duplicateId in duplicatedIds)
                {
                    DebugLogHelper.LogError(
                        log,
                        "Preset provider [" + provider.Name + "] contains duplicate id [" + duplicateId + "] for target [" + targetGuid + "]; all duplicates are ignored.");
                }
                foreach (PublishedModSettingsPreset preset in providerPresets.Where(item => !duplicatedIds.Contains(item.Id)))
                {
                    if (result.Count >= MaximumPresetsPerTarget)
                    {
                        DebugLogHelper.LogError(log, "Published presets for target [" + targetGuid + "] exceed the global limit; remaining presets are ignored.");
                        break;
                    }
                    if (!identities.Add(preset.StableId))
                    {
                        DebugLogHelper.LogError(log, "Duplicate provider/target/preset identity [" + preset.StableId + "] was ignored.");
                        continue;
                    }
                    result.Add(preset);
                }
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(
                        log,
                        "Preset provider [" + provider.Name + "] was rejected: " + exception.Message);
                }
            }

            return result.OrderBy(item => item.SourceKind)
                .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.ProviderName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }

        private static void AddProvider(
            List<PresetProvider> providers,
            HashSet<string> seen,
            string guid,
            string name,
            string path,
            ModSettingsPresetSourceKind sourceKind,
            bool directoryIsTarget)
        {
            if (string.IsNullOrWhiteSpace(guid) || guid.Any(char.IsControl) ||
                string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
            string full = Path.GetFullPath(path);
            if (seen.Add(full))
            {
                providers.Add(new PresetProvider
                {
                    Guid = guid,
                    Name = name ?? guid,
                    Root = full,
                    SourceKind = sourceKind,
                    DirectoryIsTarget = directoryIsTarget,
                });
            }
        }

        private sealed class PresetProvider
        {
            public string Guid;
            public string Name;
            public string Root;
            public ModSettingsPresetSourceKind SourceKind;
            public bool DirectoryIsTarget;
        }

        private static string ReadPresetText(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length <= 0 || stream.Length > MaximumFileBytes)
                    throw new InvalidDataException("Preset file size is outside the supported range.");
                using (var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true))
                {
                    string text = reader.ReadToEnd();
                    if (stream.Length > MaximumFileBytes || Encoding.UTF8.GetByteCount(text) > MaximumFileBytes)
                        throw new InvalidDataException("Preset file size is outside the supported range.");
                    return text;
                }
            }
        }
        private static bool IsBelow(string root, string path)
        {
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAtOrBelow(string root, string path)
        {
            string canonicalRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string canonicalPath = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(canonicalRoot, canonicalPath, StringComparison.OrdinalIgnoreCase) ||
                IsBelow(canonicalRoot, canonicalPath);
        }

        private static void EnsureNoReparsePoints(string root, string path)
        {
            string canonicalRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string canonicalPath = Path.GetFullPath(path);
            if (!IsAtOrBelow(canonicalRoot, canonicalPath))
                throw new InvalidDataException("Preset path leaves its provider directory.");
            string relative = canonicalPath.Substring(canonicalRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string current = canonicalRoot;
            foreach (string segment in relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Preset paths may not traverse reparse points.");
            }
        }

        private static void EnsureExistingPathSegmentsNoReparse(string root, string path)
        {
            string canonicalRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string canonicalPath = Path.GetFullPath(path);
            if (!IsAtOrBelow(canonicalRoot, canonicalPath))
                throw new InvalidDataException("Preset path leaves its provider directory.");
            string relative = canonicalPath.Substring(canonicalRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string current = canonicalRoot;
            foreach (string segment in relative.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                if (!Directory.Exists(current) && !File.Exists(current))
                    break;
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Preset paths may not traverse reparse points.");
            }
        }
        private static bool MatchesVersion(Version actual, string minimum, string maximum)
        {
            if (actual == null) return string.IsNullOrWhiteSpace(minimum) && string.IsNullOrWhiteSpace(maximum);
            Version parsedMinimum = null;
            Version parsedMaximum = null;
            if (!string.IsNullOrWhiteSpace(minimum))
            {
                if (!TryVersion(minimum, out parsedMinimum))
                    throw new InvalidDataException("Invalid minimumTargetVersion [" + minimum + "].");
            }
            if (!string.IsNullOrWhiteSpace(maximum))
            {
                if (!TryVersion(maximum, out parsedMaximum))
                    throw new InvalidDataException("Invalid maximumTargetVersion [" + maximum + "].");
            }
            if (parsedMinimum != null && parsedMaximum != null && parsedMinimum > parsedMaximum)
                throw new InvalidDataException("minimumTargetVersion exceeds maximumTargetVersion.");
            if (parsedMinimum != null && actual < parsedMinimum) return false;
            if (parsedMaximum != null && actual > parsedMaximum) return false;
            return true;
        }
        private static bool TryVersion(string text, out Version version)
        {
            string numeric = (text ?? string.Empty).Split('-', '+', ' ')[0];
            return Version.TryParse(numeric, out version);
        }

        private static string RequireSafePathSegment(string value, string label)
        {
            string segment = (value ?? string.Empty).Trim();
            if (segment.Length == 0 || segment == "." || segment == ".." ||
                segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                segment.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
                segment.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
            {
                throw new InvalidDataException("Invalid " + label + ".");
            }
            return segment;
        }
    }
}
