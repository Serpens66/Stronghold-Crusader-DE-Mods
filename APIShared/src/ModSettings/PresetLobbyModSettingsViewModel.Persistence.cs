using APIShared.GameModes;
using APIShared.ModSettings;
using APIShared.SerpsMods;
using Shared;
#pragma warning disable 1591 // XAML and integration surface is documented by the APIShared preset guide.
using BepInEx;
using BepInEx.Logging;
using MessagePack;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.Network;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
#if !API_SHARED_PRESET_TESTS
using R3;
using SHCDESE.EventAPI;
using SHCDESE.NoesisUtil;
#endif
using ComboBoxItem = Noesis.ComboBoxItem;
using Visibility = Noesis.Visibility;
#if API_SHARED_LOBBY_OBSERVER && !API_SHARED_PRESET_TESTS
using APIShared;
#endif

namespace APIShared.ModSettings
{
    /// <summary>Personal preset persistence, defaults, working snapshots and stable storage schema.</summary>
    public abstract partial class PresetLobbyModSettingsViewModel
    {
        private sealed class PresetController
        {
            internal const string SchemaVersionKey = "__SerpPresetSchemaVersion";
            internal const string ActivePresetKey = "__SerpActivePreset";
            internal const string Preset1Key = "__SerpPreset1";
            internal const string Preset2Key = "__SerpPreset2";
            internal const string PublishedPresetKey = "__SerpPublishedPreset";
            internal const string CurrentSettingsKey = "__SerpCurrentSettings";
            internal const string BasedOnPresetKey = "__SerpBasedOnPreset";
            internal const string PresetDirtyKey = "__SerpPresetDirty";
            internal const string LegacyPresetImportCompletedKey = "__SerpLegacyPresetImportCompleted";

            private const int SchemaVersion = 3;

            private readonly PresetLobbyModSettingsViewModel owner;
            private readonly ManualLogSource log;
            private readonly string modName;
            private readonly bool routineLoggingEnabled;
            private readonly string filePath;
            private readonly string pluginDirectory;
            private readonly string personalPresetDirectory;
            private readonly string targetGuid;
            private readonly Version targetVersion;
            private readonly IDynamicPresetSettingsProvider dynamicProvider;
            private readonly PresetPropertyAccessor[] persistedProperties;
            private readonly PresetPropertyAccessor[] hostProperties;
            private readonly PresetPropertyAccessor[] clientProperties;
            private readonly PresetPropertyAccessor hostSettingsActivationProperty;
            private readonly PresetPropertyAccessor clientSettingsActivationProperty;
            private readonly Dictionary<string, PresetPropertyAccessor> persistedPropertiesByName;
            private readonly List<PublishedModSettingsPreset> publishedPresets =
                new List<PublishedModSettingsPreset>();

            private Dictionary<string, byte[]> defaults;
            private Dictionary<string, byte[]> preset1;
            private Dictionary<string, byte[]> missionPreset;
            private bool active;
            private bool applying;
            private PublishedModSettingsPreset activePublishedPreset;
            private PublishedModSettingsPreset suspendedPublishedPreset;
            private string basedOnStableId = string.Empty;
            private string basedOnName = string.Empty;
            private string basedOnSource = string.Empty;
            private bool presetDirty;
            private bool legacyPresetImportCompleted;
            private bool legacyMigrationPending;
            private string suspendedBasedOnStableId = string.Empty;
            private string suspendedBasedOnName = string.Empty;
            private string suspendedBasedOnSource = string.Empty;
            private bool suspendedPresetDirty;
            private string missionPresetLabel = string.Empty;
            private PublishedModSettingsPreset missionBasedOnPreset;
            private bool missionPresetDirty;
#if API_SHARED_PRESET_TESTS
            internal int TestWriteCount { get; private set; }
#endif

            public PresetController(
                PresetLobbyModSettingsViewModel owner,
                ManualLogSource log,
                string pluginAssemblyLocation,
                string modName,
                string targetGuid,
                Version targetVersion,
                bool logRoutineActivity)
            {
                this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
                this.log = log;
                this.modName = modName ?? throw new ArgumentNullException(nameof(modName));
                routineLoggingEnabled = logRoutineActivity;

                pluginDirectory = Path.GetDirectoryName(pluginAssemblyLocation)
                    ?? throw new ArgumentException(
                        $"Cannot determine the plugin directory for [{pluginAssemblyLocation}].",
                        nameof(pluginAssemblyLocation));
                this.targetGuid = ModSettingsPresetCatalog.ValidateTargetGuid(
                    targetGuid ?? throw new ArgumentNullException(nameof(targetGuid)));
                this.targetVersion = targetVersion;
                string safeFileName = string.Concat(modName.Split(Path.GetInvalidFileNameChars()));
                filePath = Path.Combine(
                    pluginDirectory,
                    LobbyModSettingsStorage.STORAGE_FOLDER_NAME,
                    safeFileName + LobbyModSettingsStorage.FILE_EXTENSION);
                personalPresetDirectory = Path.Combine(
                    pluginDirectory,
                    LobbyModSettingsStorage.STORAGE_FOLDER_NAME,
                    "Presets",
                    "Override",
                    this.targetGuid);

                dynamicProvider = owner.DynamicSettingsProvider;
                persistedProperties = dynamicProvider != null
                    ? dynamicProvider.GetSettings().Select(item => new PresetPropertyAccessor(item, dynamicProvider)).ToArray()
                    : owner.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Select(item => new PresetPropertyAccessor(item)).Where(IsPersistedProperty).ToArray();
                if (persistedProperties.Length > ModSettingsPresetJson.MaximumSettings)
                    throw new InvalidDataException("Too many preset settings.");
                if (persistedProperties.Any(x => x.RequiresRestart) && owner.SettingsApplicationBackend == null)
                    throw new InvalidOperationException("RequiresRestart settings need a configuration application backend.");
                persistedPropertiesByName = persistedProperties
                    .ToDictionary(property => property.Name, StringComparer.Ordinal);
                hostProperties = persistedProperties.Where(IsHostProperty).ToArray();
                clientProperties = persistedProperties.Where(IsClientProperty).ToArray();
                hostSettingsActivationProperty = FindSettingsActivationProperty(hostProperties, "EnableMod");
                clientSettingsActivationProperty = FindSettingsActivationProperty(clientProperties, "EnableClientFeatures", "EnableMod");
                if (persistedProperties.Length != 0)
                    RefreshCatalog();
            }

            internal void RestorePreparationLabel(string label)
            {
                missionBasedOnPreset = null;
                missionPresetLabel = label;
            }

            public IReadOnlyList<PublishedModSettingsPreset> PublishedPresets => publishedPresets;
            public string TargetGuid => targetGuid;
            public bool HasPersistentSettings => persistedProperties.Length != 0;

            public string GetStatusText(
                string basedOnText,
                string modifiedText,
                string personalSourceText,
                string bundledSourceText,
                string externalSourceText)
            {
                bool missionSelected = owner.IsMissionPresetSelected;
                if (missionSelected && missionBasedOnPreset == null)
                    return missionPresetLabel;
                string sourceName = missionSelected ? missionBasedOnPreset.Name : basedOnName;
                if (string.IsNullOrWhiteSpace(sourceName))
                    return string.Empty;
                string status = (basedOnText ?? "Based on") + ": " + sourceName;
                PublishedModSettingsPreset sourcePreset = missionSelected ? missionBasedOnPreset : activePublishedPreset;
                string localizedSource = sourcePreset == null
                    ? basedOnSource
                    : DescribeSource(
                        sourcePreset,
                        personalSourceText,
                        bundledSourceText,
                        externalSourceText);
                if (!string.IsNullOrWhiteSpace(localizedSource))
                    status += " · " + localizedSource;
                if (missionSelected ? missionPresetDirty : presetDirty)
                    status += " (" + (modifiedText ?? "modified") + ")";
                return status;
            }

            public void RefreshCatalog()
            {
                if (persistedProperties.Length == 0)
                {
                    publishedPresets.Clear();
                    return;
                }

                Directory.CreateDirectory(personalPresetDirectory);
                var refreshed = new List<PublishedModSettingsPreset>();
                foreach (PublishedModSettingsPreset preset in ModSettingsPresetCatalog.Discover(
                    targetGuid,
                    targetVersion,
                    pluginDirectory,
                    personalPresetDirectory,
                    log))
                {
                    try
                    {
                        ValidatePublishedPreset(preset);
                        refreshed.Add(preset);
                    }
                    catch (Exception exception)
                    {
                        DebugLogHelper.LogError(log, $"Published preset [{preset.SourcePath}] is incompatible with [{modName}]: {exception.Message}");
                    }
                }
                publishedPresets.Clear();
                publishedPresets.AddRange(refreshed);
                activePublishedPreset = publishedPresets.FirstOrDefault(item =>
                    string.Equals(item.StableId, basedOnStableId, StringComparison.OrdinalIgnoreCase));
                if (activePublishedPreset != null)
                {
                    basedOnName = activePublishedPreset.Name;
                    basedOnSource = DescribeSource(activePublishedPreset);
                }
                bool clearedMissingSource = active && basedOnStableId.Length != 0 && activePublishedPreset == null;
                if (clearedMissingSource)
                {
                    LogRoutine($"[{modName}] Preset source [{SanitizeLogValue(basedOnStableId)}] is unavailable; retained the materialized working settings.");
                    basedOnStableId = string.Empty;
                    basedOnName = string.Empty;
                    basedOnSource = string.Empty;
                    presetDirty = false;
                }
                if (clearedMissingSource && active)
                    WriteCombinedPayload();
            }

            public string CreateUniquePersonalPresetId(string name)
            {
                string baseId = CreatePersonalPresetId(name);
                string candidate = baseId;
                int suffix = 2;
                var occupied = new HashSet<string>(
                    publishedPresets.Where(item => item.SourceKind == ModSettingsPresetSourceKind.Personal)
                        .Select(item => item.Id),
                    StringComparer.OrdinalIgnoreCase);
                while (occupied.Contains(candidate) || File.Exists(Path.Combine(personalPresetDirectory, "preset_" + candidate + ".json")))
                    candidate = baseId + "-" + suffix++;
                return candidate;
            }

            private static string CreatePersonalPresetId(string name)
            {
                string trimmed = (name ?? string.Empty).Trim();
                if (trimmed.Length == 0)
                    throw new InvalidDataException("A preset name is required.");
                var result = new System.Text.StringBuilder(trimmed.Length);
                bool separator = false;
                foreach (char character in trimmed.ToLowerInvariant())
                {
                    if (char.IsLetterOrDigit(character) || character == '-' || character == '_')
                    {
                        result.Append(character);
                        separator = false;
                    }
                    else if (!separator)
                    {
                        result.Append('-');
                        separator = true;
                    }
                }
                string id = result.ToString().Trim('-');
                if (id.Length == 0)
                    throw new InvalidDataException("The preset name does not contain a usable id.");
                return id.Length <= 128 ? id : id.Substring(0, 128).TrimEnd('-');
            }

            private bool MigrateStagedExports()
            {
                string staged = Path.Combine(
                    pluginDirectory,
                    LobbyModSettingsStorage.STORAGE_FOLDER_NAME,
                    "PresetExports",
                    "Override",
                    targetGuid);
                if (!Directory.Exists(staged))
                    return true;
                Directory.CreateDirectory(personalPresetDirectory);
                bool succeeded = true;
                foreach (string source in Directory.GetFiles(staged, "preset_*.json", SearchOption.TopDirectoryOnly))
                {
                    string destination = Path.Combine(personalPresetDirectory, Path.GetFileName(source));
                    if (File.Exists(destination))
                        continue;
                    try
                    {
                        PublishPresetJson(destination, File.ReadAllText(source), overwrite: false);
                        LogRoutine($"[{modName}] Imported staged personal preset [{source}] to [{destination}].");
                    }
                    catch (Exception exception)
                    {
                        succeeded = false;
                        DebugLogHelper.LogError(
                            log,
                            $"[{modName}] Could not import staged personal preset [{source}]: {exception.Message}");
                    }
                }
                return succeeded;
            }

            public int MissionPresetIndex => 1;

            public bool HasHostSettings => hostProperties.Length != 0;

            public bool HasClientSettings => clientProperties.Length != 0;

            public bool HasHostSettingsActivation => hostSettingsActivationProperty != null;

            public bool HasClientSettingsActivation => clientSettingsActivationProperty != null;

            public bool HostSettingsEnabled => ReadSettingsActivation(hostSettingsActivationProperty);

            public bool ClientSettingsEnabled => ReadSettingsActivation(clientSettingsActivationProperty);

            public void SetHostSettingsEnabled(bool value) =>
                WriteSettingsActivation(hostSettingsActivationProperty, value);

            public void SetClientSettingsEnabled(bool value) =>
                WriteSettingsActivation(clientSettingsActivationProperty, value);

            public bool IsHostSettingsActivationProperty(string propertyName) =>
                IsSettingsActivationProperty(hostSettingsActivationProperty, propertyName);

            public bool IsClientSettingsActivationProperty(string propertyName) =>
                IsSettingsActivationProperty(clientSettingsActivationProperty, propertyName);

            public bool IsApplyingSnapshot => applying;

            public bool IsHostPropertyName(string propertyName) =>
                !string.IsNullOrEmpty(propertyName) &&
                persistedPropertiesByName.TryGetValue(propertyName, out PresetPropertyAccessor property) &&
                IsHostProperty(property);

#if API_SHARED_PRESET_TESTS
            public int NormalizeSelection(int selected, bool missionContext)
            {
                if (missionContext && selected == MissionPresetIndex)
                    return selected;
                return 0;
            }
#endif

            public void CaptureDefaults()
            {
                defaults = dynamicProvider == null ? CaptureCurrentSettings() : persistedProperties.ToDictionary(
                    item => item.Name, item => MessagePackSerializer.Serialize(item.PropertyType, item.DefaultValue), StringComparer.Ordinal);
            }

            public void SetDefaultValue<T>(string propertyName, T value)
            {
                if (string.IsNullOrWhiteSpace(propertyName) ||
                    !persistedPropertiesByName.TryGetValue(propertyName, out PresetPropertyAccessor property))
                {
                    throw new InvalidDataException($"Unknown persistent setting [{propertyName}].");
                }
                if (property.PropertyType != typeof(T))
                {
                    throw new InvalidDataException(
                        $"Default setting [{propertyName}] expects [{property.PropertyType.FullName}], not [{typeof(T).FullName}].");
                }
                if (value == null && property.PropertyType.IsValueType &&
                    Nullable.GetUnderlyingType(property.PropertyType) == null)
                {
                    throw new InvalidDataException($"Default setting [{propertyName}] cannot be null.");
                }

                byte[] serialized = MessagePackSerializer.Serialize(property.PropertyType, value);
                MessagePackSerializer.Deserialize(property.PropertyType, serialized);
                defaults[property.Name] = serialized;
            }

            public void Activate()
            {
                if (active)
                    return;

                if (persistedProperties.Length == 0)
                {
                    active = true;
                    LogRoutine($"[{modName}] Preset storage skipped because the ViewModel has no persistent settings.");
                    return;
                }

                Dictionary<string, byte[]> payload = null;
                bool fileExists = File.Exists(filePath);
                if (fileExists && !TryReadPayload(out payload))
                {
                    BackupCorruptFile();
                    payload = null;
                }

                bool needsRewrite = false;
                PublishedModSettingsPreset legacyPublishedToMaterialize = null;
                Dictionary<string, byte[]> legacyPreset1 = null;
                Dictionary<string, byte[]> legacyPreset2 = null;
                int legacySelected = 0;
                int loadedSchemaVersion = 0;
                string oldPublishedId = string.Empty;
                if (payload != null && payload.ContainsKey(SchemaVersionKey))
                {
                    try
                    {
                        int schemaVersion = MessagePackSerializer.Deserialize<int>(payload[SchemaVersionKey]);
                        loadedSchemaVersion = schemaVersion;
                        if (schemaVersion < 1 || schemaVersion > SchemaVersion)
                            throw new InvalidDataException($"Unsupported preset schema version [{schemaVersion}].");
                        if (schemaVersion == SchemaVersion)
                        {
                            if (payload.TryGetValue(LegacyPresetImportCompletedKey, out byte[] importCompletedBytes))
                                legacyPresetImportCompleted = MessagePackSerializer.Deserialize<bool>(importCompletedBytes);
                            preset1 = ReadSnapshot(payload, CurrentSettingsKey) ?? CaptureCurrentSettings();
                            if (payload.TryGetValue(BasedOnPresetKey, out byte[] basedOnBytes))
                                basedOnStableId = MessagePackSerializer.Deserialize<string>(basedOnBytes) ?? string.Empty;
                            if (payload.TryGetValue(PresetDirtyKey, out byte[] dirtyBytes))
                                presetDirty = MessagePackSerializer.Deserialize<bool>(dirtyBytes);
                        }
                        else
                        {
                            legacySelected = payload.TryGetValue(ActivePresetKey, out byte[] selectedBytes)
                                ? NormalizePreset(MessagePackSerializer.Deserialize<int>(selectedBytes))
                                : 0;
                            legacyPreset1 = ReadSnapshot(payload, Preset1Key) ?? CaptureCurrentSettings();
                            legacyPreset2 = ReadSnapshot(payload, Preset2Key);
                            if (schemaVersion >= 2 && payload.TryGetValue(PublishedPresetKey, out byte[] publishedBytes))
                                oldPublishedId = MessagePackSerializer.Deserialize<string>(publishedBytes) ?? string.Empty;
                        }
                    }
                    catch (Exception exception)
                    {
                        DebugLogHelper.LogError(
                            log,
                            $"[{modName}] Preset metadata is invalid: {exception}");
                        BackupCorruptFile();
                        payload = null;
                    }
                }

                if (payload != null && loadedSchemaVersion > 0 && loadedSchemaVersion < SchemaVersion)
                {
                    preset1 = Clone(legacySelected == 1 && legacyPreset2 != null ? legacyPreset2 : legacyPreset1);
                    legacyPublishedToMaterialize = FindLegacyPublishedPreset(oldPublishedId);
                    try
                    {
                        SaveLegacySnapshotAsPersonalPreset("legacy-preset-1", "Preset 1 (migrated)", legacyPreset1);
                        if (legacyPreset2 != null)
                            SaveLegacySnapshotAsPersonalPreset("legacy-preset-2", "Preset 2 (migrated)", legacyPreset2);
                        RefreshCatalog();
                        if (legacyPublishedToMaterialize == null)
                        {
                            PublishedModSettingsPreset migrated = publishedPresets.FirstOrDefault(item =>
                                item.SourceKind == ModSettingsPresetSourceKind.Personal &&
                                string.Equals(item.Id, legacySelected == 1 ? "legacy-preset-2" : "legacy-preset-1", StringComparison.OrdinalIgnoreCase));
                            SetBasedOn(migrated, modified: false);
                        }
                        needsRewrite = true;
                        LogRoutine($"[{modName}] Migrated legacy Preset 1/2 storage to personal JSON presets.");
                    }
                    catch (Exception exception)
                    {
                        legacyMigrationPending = true;
                        DebugLogHelper.LogError(
                            log,
                            $"[{modName}] Could not publish legacy presets; the valid MessagePack data was retained for retry: {exception}");
                    }
                }

                if (payload == null || !payload.ContainsKey(SchemaVersionKey))
                {
                    // The compatibility load before registration restored a legacy file here.
                    // Capturing the ViewModel preserves those values and supplies defaults
                    // for settings introduced after that file was written.
                    preset1 = CaptureCurrentSettings();
                    if (fileExists)
                    {
                        try
                        {
                            SaveLegacySnapshotAsPersonalPreset("legacy-preset-1", "Preset 1 (migrated)", preset1);
                            RefreshCatalog();
                            SetBasedOn(publishedPresets.FirstOrDefault(item =>
                                item.SourceKind == ModSettingsPresetSourceKind.Personal &&
                                string.Equals(item.Id, "legacy-preset-1", StringComparison.OrdinalIgnoreCase)), modified: false);
                            needsRewrite = true;
                        }
                        catch (Exception exception)
                        {
                            legacyMigrationPending = true;
                            DebugLogHelper.LogError(
                                log,
                                $"[{modName}] Could not publish the legacy lobby-settings preset; the original file was retained for retry: {exception}");
                        }
                    }
                    LogRoutine(
                        fileExists && !legacyMigrationPending
                            ? $"[{modName}] Migrated legacy lobby settings to a personal preset."
                            : fileExists
                                ? $"[{modName}] Deferred legacy lobby-settings migration after a publication failure."
                            : $"[{modName}] Initialized editable settings from code defaults.");
                }

                if (!legacyMigrationPending && !legacyPresetImportCompleted)
                {
                    legacyPresetImportCompleted = MigrateStagedExports();
                    RefreshCatalog();
                    needsRewrite = true;
                }

                string storedBasedOnStableId = basedOnStableId;
                RestoreBasedOnMetadata();
                if (storedBasedOnStableId.Length != 0 && basedOnStableId.Length == 0)
                    needsRewrite = true;
                LogRoutine($"[{modName}] Loaded editable lobby-settings working state; basedOn={SanitizeLogValue(basedOnStableId)}, modified={presetDirty}.");

                active = true;
                if (dynamicProvider != null)
                {
                    // The external provider owns startup files; never revive a stale pre-restart working copy.
                    preset1 = CaptureCurrentSettings();
                    SetBasedOn(null, false);
                }
                ApplySnapshot(preset1, 0, writeLocalStorage: false);
                if (legacyPublishedToMaterialize != null)
                    ApplyPublishedPreset(legacyPublishedToMaterialize, writeLocalStorage: false);

                // Persist the envelope immediately after reading a legacy top-level payload.
                // Otherwise an unchanged legacy file would be re-imported on every startup and
                // could never retain the stable identity of a subsequently selected public preset.
                if (needsRewrite && !legacyMigrationPending)
                    WriteCombinedPayload();
            }

#if API_SHARED_PRESET_TESTS
            public void SwitchTo(int selected)
            {
                selected = NormalizeSelection(selected, owner.missionPresetContext);
                if (!active || owner.selectedPreset == selected)
                    return;
                if (owner.missionPresetContext && !owner.missionPresetEditable)
                    return;

                if (owner.missionPresetContext && selected == MissionPresetIndex)
                {
                    activePublishedPreset = null;
                    ApplySnapshot(missionPreset, MissionPresetIndex, writeLocalStorage: false);
                    LogRoutine($"[{modName}] Restored the active mission preset.");
                    return;
                }

                ApplySnapshot(preset1, 0, writeLocalStorage: true);
            }
#endif

            public void LoadPreset(PublishedModSettingsPreset preset)
            {
                if (preset == null || !publishedPresets.Contains(preset))
                    throw new InvalidDataException("The selected preset is unavailable.");
                if (owner.IsMissionPresetSelected && !owner.missionPresetEditable)
                    throw new InvalidOperationException("The active mission preset is read-only.");
                ApplyPublishedPreset(preset, writeLocalStorage: !owner.IsMissionPresetSelected);
            }

            public void ApplyDefaultsAsWorkingCopy()
            {
                if (owner.IsMissionPresetSelected)
                    throw new InvalidOperationException("Mission defaults must be materialized by the registered mission source provider.");
                var prepared = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                foreach (PresetPropertyAccessor property in persistedProperties)
                {
                    if (IsHostProperty(property) && !owner.isLocalHost) continue;
                    if (defaults.TryGetValue(property.Name, out byte[] value))
                        prepared[property.Name] = value == null ? null : (byte[])value.Clone();
                }
                ApplySnapshot(prepared, 0, writeLocalStorage: false);
                foreach (KeyValuePair<string, byte[]> entry in prepared) preset1[entry.Key] = entry.Value;
                SetBasedOn(null, false);
                WriteCombinedPayload();
                LogRoutine($"[{modName}] Loaded Mod defaults as editable working settings.");
            }

            public void ApplyWorkingSnapshot(Dictionary<string, byte[]> snapshot)
            {
                if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
                if (owner.IsMissionPresetSelected)
                {
                    ApplyMissionWorkingSnapshot(snapshot, missionPresetLabel);
                    return;
                }
                Dictionary<string, byte[]> clone = Clone(snapshot);
                ApplySnapshot(clone, 0, writeLocalStorage: false);
                preset1 = clone;
                WriteCombinedPayload();
            }

            private void RestoreBasedOnMetadata()
            {
                activePublishedPreset = publishedPresets.FirstOrDefault(item =>
                    string.Equals(item.StableId, basedOnStableId, StringComparison.OrdinalIgnoreCase));
                if (activePublishedPreset == null)
                {
                    if (basedOnStableId.Length != 0)
                        LogRoutine($"[{modName}] Preset source [{SanitizeLogValue(basedOnStableId)}] is unavailable; retained the materialized working settings.");
                    basedOnStableId = string.Empty;
                    basedOnName = string.Empty;
                    basedOnSource = string.Empty;
                    return;
                }
                basedOnName = activePublishedPreset.Name;
                basedOnSource = DescribeSource(activePublishedPreset);
            }

            private PublishedModSettingsPreset FindLegacyPublishedPreset(string legacyStableId)
            {
                if (string.IsNullOrWhiteSpace(legacyStableId))
                    return null;
                return publishedPresets.FirstOrDefault(item =>
                    string.Equals(item.ProviderGuid + "\n" + item.TargetGuid + "\n" + item.Id,
                        legacyStableId,
                        StringComparison.OrdinalIgnoreCase));
            }

            private void SetBasedOn(PublishedModSettingsPreset preset, bool modified)
            {
                activePublishedPreset = preset;
                basedOnStableId = preset?.StableId ?? string.Empty;
                basedOnName = preset?.Name ?? string.Empty;
                basedOnSource = preset == null ? string.Empty : DescribeSource(preset);
                presetDirty = preset != null && modified;
            }

            private static string DescribeSource(PublishedModSettingsPreset preset)
            {
                return DescribeSource(preset, "Personal presets", "Bundled with this mod", "External presets");
            }

            private static string DescribeSource(
                PublishedModSettingsPreset preset,
                string personalSourceText,
                string bundledSourceText,
                string externalSourceText)
            {
                switch (preset.SourceKind)
                {
                    case ModSettingsPresetSourceKind.Personal: return personalSourceText;
                    case ModSettingsPresetSourceKind.Bundled: return bundledSourceText;
                    case ModSettingsPresetSourceKind.External: return externalSourceText + ": " + preset.ProviderName;
                    default: return preset.ProviderName;
                }
            }

            private void SaveLegacySnapshotAsPersonalPreset(
                string id,
                string name,
                Dictionary<string, byte[]> snapshot)
            {
                Directory.CreateDirectory(personalPresetDirectory);
                string path = Path.Combine(personalPresetDirectory, "preset_" + id + ".json");
                var settings = new Dictionary<string, PublishedPresetSetting>(StringComparer.Ordinal);
                foreach (PresetPropertyAccessor property in persistedProperties)
                {
                    if (snapshot == null || !snapshot.TryGetValue(property.Name, out byte[] bytes) || bytes == null)
                        continue;
                    object value = MessagePackSerializer.Deserialize(property.PropertyType, bytes);
                    settings[property.Name] = new PublishedPresetSetting
                    {
                        Mode = PublishedPresetValueMode.Fixed,
                        Value = ModSettingsPresetJson.ToJsonValue(property.PropertyType, value),
                    };
                }
                string json = ModSettingsPresetJson.Serialize(
                    targetGuid,
                    id,
                    name,
                    "Migrated from the previous local Preset 1/2 storage.",
                    string.Empty,
                    string.Empty,
                    settings);
                if (File.Exists(path))
                {
                    string existing = File.ReadAllText(path);
                    if (string.Equals(existing, json, StringComparison.Ordinal))
                        return;
                    throw new InvalidDataException(
                        $"The migration target [{path}] already exists with different contents.");
                }
                PublishPresetJson(path, json, overwrite: false);
            }

            private PresetPropertyAccessor[] descriptorOrder;
            public IReadOnlyList<PresetSettingDescriptor> GetSettingDescriptors()
            {
                if (descriptorOrder == null)
                    descriptorOrder = persistedProperties.OrderBy(p => IsHostProperty(p) ? PresetSettingScope.Host :
                        p.GetCustomAttribute<SyncPerPlayerAttribute>() != null ? PresetSettingScope.Player : PresetSettingScope.Local)
                        .ThenBy(p => p.Name, StringComparer.Ordinal).ToArray();
                // Return fresh descriptors; capabilities remain live and callers cannot mutate the cache.
                return
                descriptorOrder.Select(property => new PresetSettingDescriptor
                {
                    PropertyName = property.Name,
                    PropertyType = property.PropertyType,
                    Group = property.Group,
                    DisplayName = property.DisplayName,
                    RequiresRestart = property.RequiresRestart,
                    Scope = IsHostProperty(property)
                        ? PresetSettingScope.Host
                        : property.GetCustomAttribute<SyncPerPlayerAttribute>() != null
                            ? PresetSettingScope.Player
                            : PresetSettingScope.Local,
                }).ToArray();
            }

            public string SavePersonalPreset(
                string id,
                string name,
                string description,
                IEnumerable<PresetSaveSelection> selections,
                bool overwrite)
            {
                PresetSaveSelection[] selectedSettings = (selections ?? Enumerable.Empty<PresetSaveSelection>()).ToArray();
                if (selectedSettings.Length == 0)
                    throw new InvalidDataException("At least one setting must be selected for saving.");
                IGrouping<string, PresetSaveSelection> duplicate = selectedSettings
                    .GroupBy(item => item?.PropertyName ?? string.Empty, StringComparer.Ordinal)
                    .FirstOrDefault(group => group.Count() > 1);
                if (duplicate != null)
                    throw new InvalidDataException($"Setting [{duplicate.Key}] was selected more than once.");

                var exported = new Dictionary<string, PublishedPresetSetting>(StringComparer.Ordinal);
                foreach (PresetSaveSelection selection in selectedSettings)
                {
                    if (selection == null || !persistedPropertiesByName.TryGetValue(selection.PropertyName, out PresetPropertyAccessor property))
                        throw new InvalidDataException($"Unknown persistent setting [{selection?.PropertyName}].");
                    object value = null;
                    if (selection.Mode == PublishedPresetValueMode.Fixed)
                    {
                        object currentValue = property.GetValue(owner);
                        if (IsHostProperty(property) && !owner.isLocalHost)
                        {
                            Dictionary<string, byte[]> ownedPreset = preset1 ?? defaults;
                            if (!ownedPreset.TryGetValue(property.Name, out byte[] ownedBytes) || ownedBytes == null)
                                throw new InvalidDataException($"Locally owned value for [{property.Name}] is unavailable.");
                            currentValue = MessagePackSerializer.Deserialize(property.PropertyType, ownedBytes);
                        }
                        value = ModSettingsPresetJson.ToJsonValue(property.PropertyType, currentValue);
                    }
                    exported.Add(property.Name, new PublishedPresetSetting { Mode = selection.Mode, Value = value });
                }

                string json = ModSettingsPresetJson.Serialize(targetGuid, id, name, description, string.Empty, string.Empty, exported);
                string safeId = string.Concat(id.Split(Path.GetInvalidFileNameChars()));
                if (string.IsNullOrWhiteSpace(safeId))
                    throw new InvalidDataException("Preset id has no safe filename characters.");
                string path = Path.Combine(
                    personalPresetDirectory,
                    "preset_" + safeId + ".json");
                PublishPresetJson(path, json, overwrite);
                RefreshCatalog();
                LogRoutine(
                    $"[{modName}] {(overwrite ? "Overwrote" : "Saved")} personal preset [{id}] at [{path}].");
                return path;
            }

            public string ImportPresetJson(string json)
            {
                PublishedModSettingsPreset imported = ModSettingsPresetJson.Parse(json, targetGuid, modName, targetGuid, "");
                ValidatePublishedPreset(imported);
                string safeId = string.Concat(imported.Id.Split(Path.GetInvalidFileNameChars()));
                if (string.IsNullOrWhiteSpace(safeId)) throw new InvalidDataException("Invalid preset id.");
                string path = Path.Combine(personalPresetDirectory, "preset_" + safeId + ".json");
                PublishPresetJson(path, ModSettingsPresetJson.Serialize(targetGuid, imported.Id, imported.Name,
                    imported.Description, imported.MinimumTargetVersion, imported.MaximumTargetVersion, imported.Settings), false);
                RefreshCatalog();
                return path;
            }

            public void LogPresetOperationCancelled(string operation, string id)
            {
                LogRoutine(
                    $"[{modName}] Personal preset operation [{operation}] for [{id ?? string.Empty}] was cancelled.");
            }

            public void LogPresetOperationFailure(string operation, string id, Exception exception)
            {
                DebugLogHelper.LogError(
                    log,
                    $"[{modName}] Personal preset operation [{operation}] for [{id ?? string.Empty}] failed: {exception}");
            }

            public void DeletePersonalPreset(PublishedModSettingsPreset preset)
            {
                if (preset == null ||
                    preset.SourceKind != ModSettingsPresetSourceKind.Personal ||
                    !publishedPresets.Contains(preset))
                {
                    throw new InvalidDataException("Only an available personal preset can be deleted.");
                }

                string path = Path.GetFullPath(preset.SourcePath ?? string.Empty);
                ModSettingsPresetCatalog.ValidatePersonalWritePath(
                    pluginDirectory,
                    personalPresetDirectory,
                    path);
                if (!File.Exists(path))
                    throw new FileNotFoundException("The selected personal preset no longer exists.", path);

                string stableId = preset.StableId;
                File.Delete(path);

                bool metadataChanged = false;
                if (string.Equals(basedOnStableId, stableId, StringComparison.OrdinalIgnoreCase))
                {
                    activePublishedPreset = null;
                    basedOnStableId = string.Empty;
                    basedOnName = string.Empty;
                    basedOnSource = string.Empty;
                    presetDirty = false;
                    metadataChanged = true;
                }
                if (string.Equals(suspendedBasedOnStableId, stableId, StringComparison.OrdinalIgnoreCase))
                {
                    suspendedPublishedPreset = null;
                    suspendedBasedOnStableId = string.Empty;
                    suspendedBasedOnName = string.Empty;
                    suspendedBasedOnSource = string.Empty;
                    suspendedPresetDirty = false;
                    metadataChanged = true;
                }

                RefreshCatalog();
                if (metadataChanged && active)
                    WriteCombinedPayload();
                LogRoutine($"[{modName}] Deleted personal preset [{SanitizeLogValue(stableId)}].");
            }

            private void PublishPresetJson(string path, string json, bool overwrite)
            {
                string directory = Path.GetDirectoryName(path);
                ModSettingsPresetCatalog.ValidatePersonalWritePath(
                    pluginDirectory,
                    personalPresetDirectory,
                    path);
                Directory.CreateDirectory(directory);
                ModSettingsPresetCatalog.ValidatePersonalWritePath(
                    pluginDirectory,
                    personalPresetDirectory,
                    path);
                string temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
                try
                {
                    File.WriteAllText(temporaryPath, json, new System.Text.UTF8Encoding(false));
                    if (!overwrite)
                    {
                        try
                        {
                            File.Move(temporaryPath, path);
                        }
                        catch (IOException)
                        {
                            if (File.Exists(path))
                                throw new PresetSaveFileExistsException(path);
                            throw;
                        }
                    }
                    else
                    {
                        PresetAtomicPublishResult publish = PresetAtomicFilePublisher.Publish(temporaryPath, path);
                        if (!publish.Succeeded) throw publish.Error;
                    }
                }
                finally
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
            }

            public Dictionary<string, byte[]> CreateDisabledSnapshot()
            {
                Dictionary<string, byte[]> snapshot = CopyProperties(defaults, hostProperties);
                if (persistedPropertiesByName.TryGetValue("EnableMod", out PresetPropertyAccessor enableProperty) &&
                    enableProperty.PropertyType == typeof(bool))
                {
                    snapshot[enableProperty.Name] = MessagePackSerializer.Serialize(false);
                }
                return snapshot;
            }

            public Dictionary<string, byte[]> CreateDefaultSnapshot() => Clone(defaults);

            public Dictionary<string, byte[]> CreateCurrentMissionSnapshot() =>
                Clone(owner.IsMissionPresetSelected ? missionPreset : preset1 ?? defaults);

            public Dictionary<string, byte[]> CreatePlayerMissionSnapshot() =>
                Clone(owner.IsMissionPresetSelected ? preset1 ?? defaults : CaptureCurrentSettings());

            public void ApplyMissionWorkingSnapshot(Dictionary<string, byte[]> snapshot, string label)
            {
                if (!owner.IsMissionPresetSelected || !owner.missionPresetEditable)
                    throw new InvalidOperationException("Mission settings are not currently editable.");
                if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
                Dictionary<string, byte[]> merged = Clone(missionPreset ?? defaults);
                foreach (KeyValuePair<string, byte[]> entry in snapshot)
                {
                    if (!persistedPropertiesByName.TryGetValue(entry.Key, out PresetPropertyAccessor property))
                        continue;
                    if (IsHostProperty(property) && !owner.isLocalHost)
                        continue;
                    merged[entry.Key] = entry.Value == null ? null : (byte[])entry.Value.Clone();
                }
                missionPreset = merged;
                missionPresetLabel = label ?? string.Empty;
                missionBasedOnPreset = null;
                missionPresetDirty = false;
                ApplySnapshot(missionPreset, MissionPresetIndex, writeLocalStorage: false);
                LogRoutine($"[{modName}] Loaded mission source [{SanitizeLogValue(missionPresetLabel)}] into the editable working copy.");
            }

            public void EnterMissionPreset(Dictionary<string, byte[]> snapshot, string label, bool editable)
            {
                suspendedPublishedPreset = activePublishedPreset;
                suspendedBasedOnStableId = basedOnStableId;
                suspendedBasedOnName = basedOnName;
                suspendedBasedOnSource = basedOnSource;
                suspendedPresetDirty = presetDirty;
                activePublishedPreset = null;
                missionPresetLabel = label ?? string.Empty;
                missionBasedOnPreset = null;
                missionPresetDirty = false;
                missionPreset = Clone(preset1 ?? defaults);
                Dictionary<string, byte[]> supplied = snapshot ?? CreateDisabledSnapshot();
                foreach (KeyValuePair<string, byte[]> entry in supplied)
                    missionPreset[entry.Key] = entry.Value == null ? null : (byte[])entry.Value.Clone();
                ApplySnapshot(missionPreset, MissionPresetIndex, writeLocalStorage: false);
                LogRoutine($"[{modName}] Entered {(editable ? "editable" : "read-only")} mission preset.");
            }

            public void ExitMissionPreset()
            {
                missionPreset = null;
                missionBasedOnPreset = null;
                missionPresetDirty = false;
                basedOnStableId = suspendedBasedOnStableId;
                basedOnName = suspendedBasedOnName;
                basedOnSource = suspendedBasedOnSource;
                presetDirty = suspendedPresetDirty;
                activePublishedPreset = suspendedPublishedPreset != null && publishedPresets.Contains(suspendedPublishedPreset)
                    ? suspendedPublishedPreset
                    : null;
                ApplySnapshot(preset1, 0, writeLocalStorage: true);
                suspendedPublishedPreset = null;
                missionPresetLabel = string.Empty;
                LogRoutine($"[{modName}] Left mission preset and restored the previous normal preset.");
            }

            public void AfterPropertyChanged(string propertyName)
            {
                if (!active || applying || string.IsNullOrEmpty(propertyName))
                    return;

                persistedPropertiesByName.TryGetValue(
                    propertyName,
                    out PresetPropertyAccessor property);

                // Keep verified host state in the transient Trail snapshot as well.
                // Otherwise switching to a local preset and back would restore the
                // client's stale local Trail value. Never write this branch to disk.
                if (IsNetworkSyncInProgress())
                {
                    if (property != null &&
                        owner.IsMissionPresetSelected &&
                        IsHostProperty(property))
                    {
                        StoreProperty(missionPreset, property);
                    }
                    return;
                }

                if (property == null)
                    return;

                if (owner.IsMissionPresetSelected)
                {
                    if (owner.missionPresetEditable &&
                        (owner.isLocalHost || IsClientProperty(property)))
                    {
                        StoreProperty(missionPreset, property);
                        if (missionBasedOnPreset != null)
                            missionPresetDirty = true;
                    }
                    // Mission-owned values remain in memory until the normal preset is restored.
                    return;
                }

                // Incoming host values are runtime-only on clients.
                if (IsHostProperty(property) && !owner.isLocalHost)
                {
                    // A local client edit cannot replace the locally owned host preset.
                    return;
                }

                if (owner.isLocalHost || IsClientProperty(property))
                {
                    StoreProperty(preset1, property);
                    if (basedOnStableId.Length != 0)
                        presetDirty = true;
                    WriteCombinedPayload();
                }
            }

            private void ApplyPublishedPreset(PublishedModSettingsPreset preset, bool writeLocalStorage)
            {
                if (preset == null)
                    throw new ArgumentNullException(nameof(preset));

                Dictionary<string, byte[]> playerPreset = owner.IsMissionPresetSelected
                    ? Clone(preset1 ?? defaults)
                    : CaptureCurrentSettings();
                var prepared = new Dictionary<PresetPropertyAccessor, byte[]>();
                foreach (KeyValuePair<string, PublishedPresetSetting> entry in preset.Settings)
                {
                    if (owner.IsRetiredPresetProperty(entry.Key)) continue;
                    PresetPropertyAccessor property = persistedPropertiesByName[entry.Key];
                    if (IsHostProperty(property) && !owner.isLocalHost)
                        continue;

                    byte[] bytes;
                    switch (entry.Value.Mode)
                    {
                        case PublishedPresetValueMode.ModDefault:
                            if (!defaults.TryGetValue(property.Name, out bytes))
                                throw new InvalidDataException($"Code default for [{property.Name}] is unavailable.");
                            break;
                        case PublishedPresetValueMode.Player:
                            if (!playerPreset.TryGetValue(property.Name, out bytes) &&
                                !defaults.TryGetValue(property.Name, out bytes))
                            {
                                throw new InvalidDataException($"Player value for [{property.Name}] is unavailable.");
                            }
                            break;
                        case PublishedPresetValueMode.Fixed:
                            object converted = ModSettingsPresetJson.ConvertValue(entry.Value.Value, property.PropertyType);
                            bytes = MessagePackSerializer.Serialize(property.PropertyType, converted);
                            break;
                        default:
                            throw new InvalidDataException($"Unsupported published preset mode for [{property.Name}].");
                    }
                    prepared[property] = (byte[])bytes.Clone();
                }

                applying = true;
                try
                {
                    if (dynamicProvider != null || owner.SettingsApplicationBackend != null) ApplyDynamicValues(prepared);
                    else foreach (KeyValuePair<PresetPropertyAccessor, byte[]> entry in prepared)
                    {
                        if (!TryApplyProperty(entry.Key, entry.Value))
                            throw new InvalidDataException($"Published value for [{entry.Key.Name}] could not be applied.");
                    }
                    if (owner.IsMissionPresetSelected)
                    {
                        foreach (PresetPropertyAccessor property in prepared.Keys)
                            StoreProperty(missionPreset, property);
                        // Mission attribution is transient; the normal working preset stays suspended.
                        missionBasedOnPreset = preset;
                        missionPresetDirty = false;
                        owner.SetSelectedPresetCore(MissionPresetIndex);
                    }
                    else
                    {
                        foreach (PresetPropertyAccessor property in prepared.Keys)
                            StoreProperty(preset1, property);
                        SetBasedOn(preset, modified: false);
                        owner.SetSelectedPresetCore(0);
                    }
                }
                finally
                {
                    applying = false;
                }
                owner.OnSettingsSnapshotApplied();
                if (writeLocalStorage) WriteCombinedPayload();
                LogRoutine($"[{modName}] Loaded editable preset [{preset.Name}] from [{preset.ProviderName}].");
            }

            private static string SanitizeLogValue(string value) =>
                (value ?? string.Empty).Replace("\r", "\\r").Replace("\n", "\\n");

            private void ValidatePublishedPreset(PublishedModSettingsPreset preset)
            {
                foreach (KeyValuePair<string, PublishedPresetSetting> entry in preset.Settings)
                {
                    if (owner.IsRetiredPresetProperty(entry.Key)) continue;
                    if (!persistedPropertiesByName.TryGetValue(entry.Key, out PresetPropertyAccessor property))
                        throw new InvalidDataException($"Unknown persistent property [{entry.Key}].");
                    if (entry.Value == null)
                        throw new InvalidDataException($"Setting [{entry.Key}] is null.");
                    if (entry.Value.Mode == PublishedPresetValueMode.Fixed)
                    {
                        object converted = ModSettingsPresetJson.ConvertValue(entry.Value.Value, property.PropertyType);
                        MessagePackSerializer.Serialize(property.PropertyType, converted);
                    }
                }
            }

            private void ApplySnapshot(
                Dictionary<string, byte[]> stored,
                int selected,
                bool writeLocalStorage)
            {
                applying = true;
                try
                {
                    if (dynamicProvider != null || owner.SettingsApplicationBackend != null)
                    {
                        var prepared = new Dictionary<PresetPropertyAccessor, byte[]>();
                        foreach (PresetPropertyAccessor property in persistedProperties)
                        {
                            if (selected != MissionPresetIndex && !owner.isLocalHost && !IsClientProperty(property)) continue;
                            byte[] bytes = null;
                            if (stored != null) stored.TryGetValue(property.Name, out bytes);
                            if (bytes == null) defaults.TryGetValue(property.Name, out bytes);
                            if (bytes == null) throw new InvalidDataException("Missing dynamic setting: " + property.Name);
                            prepared.Add(property, bytes);
                        }
                        ApplyDynamicValues(prepared);
                    }
                    else foreach (PresetPropertyAccessor property in persistedProperties)
                    {
                        bool include = selected == MissionPresetIndex || owner.isLocalHost || IsClientProperty(property);
                        if (!include)
                            continue;

                        byte[] bytes = null;
                        if (stored != null)
                            stored.TryGetValue(property.Name, out bytes);
                        if (bytes == null)
                            defaults.TryGetValue(property.Name, out bytes);
                        if (bytes == null || !property.CanWrite)
                            continue;

                        if (!TryApplyProperty(property, bytes) &&
                            defaults.TryGetValue(property.Name, out byte[] defaultBytes) &&
                            !ReferenceEquals(bytes, defaultBytes))
                        {
                            TryApplyProperty(property, defaultBytes);
                        }
                    }

                    owner.SetSelectedPresetCore(selected);
                }
                finally
                {
                    applying = false;
                }

                owner.OnSettingsSnapshotApplied();

                if (writeLocalStorage)
                    WriteCombinedPayload();
            }

            private void ApplyDynamicValues(Dictionary<PresetPropertyAccessor, byte[]> prepared)
            {
                var values = owner.SettingsApplicationBackend?.ReadDesiredValues() ?? dynamicProvider.ReadValues();
                foreach (var entry in prepared)
                    values[entry.Key.Name] = MessagePackSerializer.Deserialize(entry.Key.PropertyType, entry.Value);
                if (owner.SettingsApplicationBackend != null) owner.SettingsApplicationBackend.ReplaceDesiredValues(values);
                else { dynamicProvider.ValidateValues(values); dynamicProvider.ReplaceValues(values); }
            }

            private bool TryApplyProperty(PresetPropertyAccessor property, byte[] bytes)
            {
                try
                {
                    object value = MessagePackSerializer.Deserialize(property.PropertyType, bytes);
                    if (value == null)
                        return false;

                    property.SetValue(owner, value);
                    return true;
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogWarning(
                        log,
                        $"[{modName}] Could not restore [{property.Name}] from the current settings snapshot: {exception.Message}");
                    return false;
                }
            }

            private Dictionary<string, byte[]> CaptureCurrentSettings()
            {
                Dictionary<string, byte[]> snapshot =
                    new Dictionary<string, byte[]>(StringComparer.Ordinal);
                foreach (PresetPropertyAccessor property in persistedProperties)
                    StoreProperty(snapshot, property);
                return snapshot;
            }

            private void StoreProperty(
                Dictionary<string, byte[]> snapshot,
                PresetPropertyAccessor property)
            {
                if (!property.CanRead)
                    return;

                try
                {
                    object value = dynamicProvider != null ? dynamicProvider.ReadValue(property.Name) : owner.SettingsApplicationBackend != null ? owner.SettingsApplicationBackend.ReadDesiredValues()[property.Name] : property.GetValue(owner);
                    if (value == null)
                    {
                        snapshot.Remove(property.Name);
                        return;
                    }

                    snapshot[property.Name] =
                        MessagePackSerializer.Serialize(property.PropertyType, value);
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogWarning(
                        log,
                        $"[{modName}] Could not capture [{property.Name}] for the current settings snapshot: {exception.Message}");
                }
            }

            private void WriteCombinedPayload()
            {
                if (persistedProperties.Length == 0 || legacyMigrationPending)
                    return;

                Dictionary<string, byte[]> payload = ComposeSafeTopLevelSnapshot();
                payload[SchemaVersionKey] = MessagePackSerializer.Serialize(SchemaVersion);
                payload[CurrentSettingsKey] = MessagePackSerializer.Serialize(preset1 ?? Clone(defaults));
                if (!string.IsNullOrEmpty(basedOnStableId))
                    payload[BasedOnPresetKey] = MessagePackSerializer.Serialize(basedOnStableId);
                payload[PresetDirtyKey] = MessagePackSerializer.Serialize(presetDirty);
                payload[LegacyPresetImportCompletedKey] = MessagePackSerializer.Serialize(legacyPresetImportCompleted);

                string directory = Path.GetDirectoryName(filePath);
                string temporaryPath = filePath + ".tmp-" + Guid.NewGuid().ToString("N");
                int publishAttempts = 0;
                try
                {
                    Directory.CreateDirectory(directory);
                    File.WriteAllBytes(temporaryPath, MessagePackSerializer.Serialize(payload));
                    PresetAtomicPublishResult result =
                        PresetAtomicFilePublisher.Publish(temporaryPath, filePath);
                    publishAttempts = result.Attempts;
                    if (!result.Succeeded)
                    {
                        DebugLogHelper.LogError(
                            log,
                            $"[{modName}] Could not atomically publish lobby-settings presets to [{filePath}] " +
                            $"after {result.Attempts} attempts; hresult=0x{result.Error.HResult:X8}: {result.Error}");
                    }
#if API_SHARED_PRESET_TESTS
                    else
                        TestWriteCount++;
#endif
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(
                        log,
                        $"[{modName}] Could not save lobby-settings presets to [{filePath}]; " +
                        $"publishAttempts={publishAttempts}, hresult=0x{exception.HResult:X8}: {exception}");
                }
                finally
                {
                    try
                    {
                        if (File.Exists(temporaryPath))
                            File.Delete(temporaryPath);
                    }
                    catch (Exception exception)
                    {
                        DebugLogHelper.LogWarning(
                            log,
                            $"[{modName}] Could not remove temporary preset file [{temporaryPath}]: {exception.Message}");
                    }
                }
            }

            private Dictionary<string, byte[]> ComposeSafeTopLevelSnapshot()
            {
                Dictionary<string, byte[]> snapshot =
                    new Dictionary<string, byte[]>(StringComparer.Ordinal);
                Dictionary<string, byte[]> ownedPreset = preset1 ?? defaults;

                foreach (PresetPropertyAccessor property in persistedProperties)
                {
                    bool mayCaptureLive = !owner.IsMissionPresetSelected &&
                        (IsClientProperty(property) || owner.isLocalHost);
                    if (mayCaptureLive)
                    {
                        StoreProperty(snapshot, property);
                        continue;
                    }

                    // Preserve the user's own host preset instead of serializing a
                    // remote host value or an externally owned Trail value.
                    if (ownedPreset.TryGetValue(property.Name, out byte[] bytes))
                        snapshot[property.Name] = bytes == null ? null : (byte[])bytes.Clone();
                    else if (defaults.TryGetValue(property.Name, out bytes))
                        snapshot[property.Name] = bytes == null ? null : (byte[])bytes.Clone();
                }

                return snapshot;
            }

            private bool TryReadPayload(out Dictionary<string, byte[]> payload)
            {
                payload = null;
                if (!File.Exists(filePath))
                    return false;

                try
                {
                    payload = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(
                        File.ReadAllBytes(filePath));
                    return payload != null;
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(
                        log,
                        $"[{modName}] Could not read lobby-settings presets from [{filePath}]: {exception}");
                    return false;
                }
            }

            private void BackupCorruptFile()
            {
                if (!File.Exists(filePath))
                    return;

                string backupPath = filePath + ".corrupt-" +
                    DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
                try
                {
                    File.Copy(filePath, backupPath, false);
                    DebugLogHelper.LogWarning(
                        log,
                        $"[{modName}] Preserved invalid preset data at [{backupPath}].");
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(
                        log,
                        $"[{modName}] Could not preserve invalid preset data: {exception}");
                }
            }

            private Dictionary<string, byte[]> ReadSnapshot(
                Dictionary<string, byte[]> payload,
                string key)
            {
                if (!payload.TryGetValue(key, out byte[] bytes))
                    return null;

                return MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(bytes);
            }

            private static int NormalizePreset(int selected)
            {
                return selected == 1 ? 1 : 0;
            }

            private static bool IsPersistedProperty(PresetPropertyAccessor property)
            {
                return property.GetCustomAttribute<DoNotPersistAttribute>() == null &&
                    (property.GetCustomAttribute<SyncPerPlayerAttribute>() != null ||
                    property.GetCustomAttribute<SyncHostOnlyAttribute>() != null ||
                    property.GetCustomAttribute<PresetLocalAttribute>() != null);
            }

            private static bool IsHostProperty(PresetPropertyAccessor property) =>
                property.GetCustomAttribute<SyncHostOnlyAttribute>() != null;

            private static bool IsClientProperty(PresetPropertyAccessor property) =>
                property.GetCustomAttribute<SyncHostOnlyAttribute>() == null &&
                (property.GetCustomAttribute<SyncPerPlayerAttribute>() != null ||
                    property.GetCustomAttribute<PresetLocalAttribute>() != null);

            private static PresetPropertyAccessor FindSettingsActivationProperty(
                IEnumerable<PresetPropertyAccessor> properties,
                params string[] preferredNames)
            {
                foreach (string name in preferredNames)
                {
                    PresetPropertyAccessor property = properties.FirstOrDefault(item =>
                        item.Name == name &&
                        item.PropertyType == typeof(bool) &&
                        item.CanRead &&
                        item.CanWrite);
                    if (property != null)
                        return property;
                }

                return null;
            }

            private bool ReadSettingsActivation(PresetPropertyAccessor property) =>
                property != null && (bool)property.GetValue(owner);

            private void WriteSettingsActivation(PresetPropertyAccessor property, bool value)
            {
                if (property == null || ReadSettingsActivation(property) == value)
                    return;

                property.SetValue(owner, value);
            }

            private static bool IsSettingsActivationProperty(
                PresetPropertyAccessor property,
                string propertyName) =>
                property != null && string.Equals(property.Name, propertyName, StringComparison.Ordinal);

            public static bool IsNetworkSyncInProgress()
            {
                return GameXAMLManagerAPI.Instance != null &&
                    GameXAMLManagerAPI.Instance.CurrentLobbyModSettingsChangeOrigin ==
                    LobbyModSettingsChangeOrigin.IncomingNetwork;
            }

            private static Dictionary<string, byte[]> CopyProperties(
                Dictionary<string, byte[]> source,
                IEnumerable<PresetPropertyAccessor> properties)
            {
                Dictionary<string, byte[]> result =
                    new Dictionary<string, byte[]>(StringComparer.Ordinal);
                if (source == null)
                    return result;

                foreach (PresetPropertyAccessor property in properties)
                {
                    if (source.TryGetValue(property.Name, out byte[] bytes))
                        result[property.Name] = bytes == null ? null : (byte[])bytes.Clone();
                }
                return result;
            }

            private void LogRoutine(string message)
            {
                if (routineLoggingEnabled)
                    DebugLogHelper.LogDebug(log, message);
            }

            private static Dictionary<string, byte[]> Clone(
                Dictionary<string, byte[]> source)
            {
                Dictionary<string, byte[]> clone =
                    new Dictionary<string, byte[]>(StringComparer.Ordinal);
                if (source == null)
                    return clone;

                foreach (KeyValuePair<string, byte[]> entry in source)
                    clone[entry.Key] = entry.Value == null ? null : (byte[])entry.Value.Clone();
                return clone;
            }
        }
    }
}
