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
using System.IO;
using System.Linq;
using System.Reflection;
using ComboBoxItem = Noesis.ComboBoxItem;
using Visibility = Noesis.Visibility;

namespace Shared
{
    /// <summary>
    /// Persists a setting in the shared local preset file without exposing it to
    /// the Script Extender's multiplayer synchronization layer.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class PresetLocalAttribute : Attribute
    {
    }

    /// <summary>
    /// Adds two local presets to a Script Extender lobby-settings ViewModel while
    /// keeping the outer MessagePack dictionary readable by the Script Extender.
    /// </summary>
    public abstract class PresetLobbyModSettingsViewModel : LobbyModSettingsBaseViewModel
    {
        private ComboBoxItem[] presetOptions = Array.Empty<ComboBoxItem>();
        private PresetController presetController;
        private int selectedPreset;
        private bool missionPresetContext;
        private bool missionPresetEditable;
        private bool isLocalHost = true;

        public ComboBoxItem[] PresetOptions => presetOptions;

        public bool HasHostSettings => presetController?.HasHostSettings ?? false;

        public bool HasClientSettings => presetController?.HasClientSettings ?? false;

        public bool IsLocalSettingsHost => isLocalHost;

        public bool MissionPresetEditable => missionPresetEditable;

        public bool CanEditHostSettings =>
            isLocalHost && (!missionPresetContext || missionPresetEditable);

        public bool CanEditClientSettings => true;

        public bool CanChangePreset =>
            !missionPresetContext && (isLocalHost || HasClientSettings);

        public bool CanResetSettings => CanEditHostSettings || HasClientSettings;

        public Visibility PresetVisibility =>
            missionPresetContext || isLocalHost || HasClientSettings
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility HostReadOnlyNoticeVisibility =>
            HasHostSettings && !CanEditHostSettings
                ? Visibility.Visible
                : Visibility.Collapsed;

        public string HostOptionsText =>
            ResolveSettingsUiText("Common.HostOptions", "HOST OPTIONS");

        public string ClientOptionsText =>
            ResolveSettingsUiText("Common.ClientOptions", "LOCAL CLIENT OPTIONS");

        public string HostReadOnlyNoticeText =>
            ResolveSettingsUiText("Common.HostReadOnly", "Values from host - read-only");

        public string ResetToDefaultHelpText =>
            ResolveSettingsUiText("Common.ResetToDefaultHelp", "Resets the settings you can control in the current context.");

        public string EnableModHelpText =>
            ResolveSettingsUiText("Common.EnableModHelp", "Enables or disables this mod for the match.");

        public string PresetHelpText =>
            ResolveSettingsUiText("Common.PresetHelp", "Selects a saved preset. Clients change only their personal settings.");

        // Compatibility alias for older views. New XAML binds host and client
        // sections separately so multiplayer and Trail locks remain independent.
        public bool AreSettingsEditable => CanEditHostSettings;

        public bool IsMissionPresetActive => missionPresetContext;

        protected virtual string ResolveSettingsUiText(string key, string fallback) => fallback;

        // Zero-based because Noesis binds this value directly to ComboBox.SelectedIndex.
        public int SelectedPreset
        {
            get => selectedPreset;
            set
            {
                int normalized = missionPresetContext ? 2 : (value == 1 ? 1 : 0);
                if (selectedPreset == normalized)
                    return;

                if (presetController == null)
                {
                    selectedPreset = normalized;
                    base.OnPropertyChanged(nameof(SelectedPreset));
                    return;
                }

                presetController.SwitchTo(normalized);
            }
        }

        internal void PreparePresets(
            ManualLogSource log,
            string pluginAssemblyLocation,
            string modName)
        {
            if (presetController != null)
                throw new InvalidOperationException($"Preset storage for [{modName}] was already prepared.");

            presetOptions = new[]
            {
                new ComboBoxItem { Content = GetVanillaText(log, "TEXT_NEW_TEXT2_210", "Preset 1") },
                new ComboBoxItem { Content = GetVanillaText(log, "TEXT_NEW_TEXT2_211", "Preset 2") },
                new ComboBoxItem { Content = string.Empty, Visibility = Visibility.Collapsed },
            };

            presetController = new PresetController(
                this,
                log,
                pluginAssemblyLocation,
                modName);
            presetController.CaptureDefaults();
            PropertyChanged += (_, __) => System_RefreshSettingsAccess();
            System_RefreshSettingsAccess();
        }

        internal void ActivatePresets()
        {
            if (presetController == null)
                throw new InvalidOperationException("Preset storage must be prepared before it is activated.");

            presetController.Activate();
        }

        // Neutral reflection boundary used by optional mission coordinators.
        public Dictionary<string, byte[]> System_CreateDisabledMissionPresetSnapshot() =>
            presetController?.CreateDisabledSnapshot() ?? new Dictionary<string, byte[]>(StringComparer.Ordinal);

        public void System_EnterMissionPreset(Dictionary<string, byte[]> snapshot, string label, bool editable)
        {
            if (presetController == null)
                return;

            missionPresetContext = true;
            missionPresetEditable = editable;
            // The items exist when Noesis first materializes the binding. Only the third
            // container's visibility changes, avoiding unsupported ItemsSource refreshes.
            presetOptions[2].Content = label ?? string.Empty;
            presetOptions[2].Visibility = Visibility.Visible;
            presetController.EnterMissionPreset(snapshot, editable);
            RaiseAccessProperties();
        }

        public void System_ExitMissionPreset()
        {
            if (!missionPresetContext || presetController == null)
                return;

            missionPresetContext = false;
            missionPresetEditable = false;
            presetController.ExitMissionPreset();
            presetOptions[2].Visibility = Visibility.Collapsed;
            presetOptions[2].Content = string.Empty;
            RaiseAccessProperties();
        }

        public void System_RefreshSettingsAccess()
        {
            bool currentIsHost;
            try
            {
                currentIsHost = GameNetworkAPI.IsLocalHost();
            }
            catch
            {
                // Registration happens before the network singleton is always available.
                currentIsHost = true;
            }

            if (isLocalHost == currentIsHost)
                return;

            isLocalHost = currentIsHost;
            RaiseAccessProperties();
        }

        // The Script Extender's event handler runs synchronously inside the base call.
        // Reattach our reserved keys only after its normal persistence has completed.
        protected new void OnPropertyChanged(string name)
        {
            try
            {
                base.OnPropertyChanged(name);
            }
            finally
            {
                presetController?.AfterPropertyChanged(name);
                System_RefreshSettingsAccess();
            }
        }

        private void SetSelectedPresetCore(int value)
        {
            if (selectedPreset == value)
                return;

            selectedPreset = value;
            OnPropertyChanged(nameof(SelectedPreset));
            RaiseAccessProperties();
        }

        private void RaiseAccessProperties()
        {
            base.OnPropertyChanged(nameof(IsLocalSettingsHost));
            base.OnPropertyChanged(nameof(HasHostSettings));
            base.OnPropertyChanged(nameof(HasClientSettings));
            base.OnPropertyChanged(nameof(MissionPresetEditable));
            base.OnPropertyChanged(nameof(CanEditHostSettings));
            base.OnPropertyChanged(nameof(CanEditClientSettings));
            base.OnPropertyChanged(nameof(CanChangePreset));
            base.OnPropertyChanged(nameof(CanResetSettings));
            base.OnPropertyChanged(nameof(PresetVisibility));
            base.OnPropertyChanged(nameof(HostReadOnlyNoticeVisibility));
            base.OnPropertyChanged(nameof(AreSettingsEditable));
            base.OnPropertyChanged(nameof(IsMissionPresetActive));
            // The Extender persists on every PropertyChanged event, including UI-only
            // access properties. Re-sanitize so remote/Trail host values never remain.
            presetController?.SanitizeStorage();
        }

        private static string GetVanillaText(
            ManualLogSource log,
            string key,
            string fallback)
        {
            try
            {
                if (CrusaderDE.Translate.Instance.GameTexts.TryGetValue(key, out string value) &&
                    !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
            catch (Exception exception)
            {
                DebugLogHelper.LogWarning(
                    log,
                    $"Could not read Vanilla preset text [{key}]: {exception.Message}");
            }

            DebugLogHelper.LogWarning(
                log,
                $"Vanilla preset text [{key}] is unavailable; using [{fallback}].");
            return fallback;
        }

        private sealed class PresetController
        {
            internal const string SchemaVersionKey = "__SerpPresetSchemaVersion";
            internal const string ActivePresetKey = "__SerpActivePreset";
            internal const string Preset1Key = "__SerpPreset1";
            internal const string Preset2Key = "__SerpPreset2";

            private const int SchemaVersion = 1;

            private readonly PresetLobbyModSettingsViewModel owner;
            private readonly ManualLogSource log;
            private readonly string modName;
            private readonly string filePath;
            private readonly PropertyInfo[] persistedProperties;
            private readonly PropertyInfo[] hostProperties;
            private readonly PropertyInfo[] clientProperties;
            private readonly Dictionary<string, PropertyInfo> persistedPropertiesByName;

            private Dictionary<string, byte[]> defaults;
            private Dictionary<string, byte[]> preset1;
            private Dictionary<string, byte[]> preset2;
            private Dictionary<string, byte[]> missionPreset;
            private bool active;
            private bool applying;
            private int localSelectedPreset;

            public PresetController(
                PresetLobbyModSettingsViewModel owner,
                ManualLogSource log,
                string pluginAssemblyLocation,
                string modName)
            {
                this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
                this.log = log;
                this.modName = modName ?? throw new ArgumentNullException(nameof(modName));

                string pluginDirectory = Path.GetDirectoryName(pluginAssemblyLocation)
                    ?? throw new ArgumentException(
                        $"Cannot determine the plugin directory for [{pluginAssemblyLocation}].",
                        nameof(pluginAssemblyLocation));
                string safeFileName = string.Concat(modName.Split(Path.GetInvalidFileNameChars()));
                filePath = Path.Combine(
                    pluginDirectory,
                    LobbyModSettingsStorage.STORAGE_FOLDER_NAME,
                    safeFileName + LobbyModSettingsStorage.FILE_EXTENSION);

                persistedProperties = owner.GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(IsPersistedProperty)
                    .ToArray();
                persistedPropertiesByName = persistedProperties
                    .ToDictionary(property => property.Name, StringComparer.Ordinal);
                hostProperties = persistedProperties.Where(IsHostProperty).ToArray();
                clientProperties = persistedProperties.Where(IsClientProperty).ToArray();
            }

            public bool HasHostSettings => hostProperties.Length != 0;

            public bool HasClientSettings => clientProperties.Length != 0;

            public void SanitizeStorage()
            {
                if (active && !applying)
                    WriteCombinedPayload();
            }

            public void CaptureDefaults()
            {
                defaults = CaptureCurrentSettings();
            }

            public void Activate()
            {
                if (active)
                    return;

                Dictionary<string, byte[]> payload = null;
                bool fileExists = File.Exists(filePath);
                if (fileExists && !TryReadPayload(out payload))
                {
                    BackupCorruptFile();
                    payload = null;
                }

                int selected = 0;
                if (payload != null && payload.ContainsKey(SchemaVersionKey))
                {
                    try
                    {
                        int schemaVersion = MessagePackSerializer.Deserialize<int>(payload[SchemaVersionKey]);
                        if (schemaVersion != SchemaVersion)
                            throw new InvalidDataException($"Unsupported preset schema version [{schemaVersion}].");

                        selected = NormalizePreset(
                            MessagePackSerializer.Deserialize<int>(payload[ActivePresetKey]));
                        preset1 = ReadSnapshot(payload, Preset1Key) ?? Clone(defaults);
                        preset2 = ReadSnapshot(payload, Preset2Key);
                        DebugLogHelper.LogInfo(
                            log,
                            $"[{modName}] Loaded lobby-settings presets; active preset={selected + 1}, preset2Saved={preset2 != null}.");
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

                if (payload == null || !payload.ContainsKey(SchemaVersionKey))
                {
                    // RegisterLobbyModSettings has already restored a legacy file here.
                    // Capturing the ViewModel preserves those values and supplies defaults
                    // for settings introduced after that file was written.
                    preset1 = CaptureCurrentSettings();
                    preset2 = null;
                    selected = 0;
                    DebugLogHelper.LogInfo(
                        log,
                        fileExists
                            ? $"[{modName}] Migrated legacy lobby settings to preset 1."
                            : $"[{modName}] Initialized preset 1 from code defaults.");
                }

                active = true;
                localSelectedPreset = selected;
                ApplyPreset(selected);
            }

            public void SwitchTo(int selected)
            {
                selected = owner.missionPresetContext ? 2 : NormalizePreset(selected);
                if (!active || owner.selectedPreset == selected)
                    return;

                if (owner.missionPresetContext)
                    return;

                localSelectedPreset = selected;
                ApplyPreset(selected);
                DebugLogHelper.LogInfo(
                    log,
                    $"[{modName}] Switched to preset {selected + 1}; saved={GetPreset(selected) != null}.");
            }

            public Dictionary<string, byte[]> CreateDisabledSnapshot()
            {
                Dictionary<string, byte[]> snapshot = CopyProperties(defaults, hostProperties);
                if (persistedPropertiesByName.TryGetValue("EnableMod", out PropertyInfo enableProperty) &&
                    enableProperty.PropertyType == typeof(bool))
                {
                    snapshot[enableProperty.Name] = MessagePackSerializer.Serialize(false);
                }
                return snapshot;
            }

            public void EnterMissionPreset(Dictionary<string, byte[]> snapshot, bool editable)
            {
                missionPreset = snapshot == null ? CreateDisabledSnapshot() : Clone(snapshot);
                ApplySnapshot(missionPreset, 2, writeLocalStorage: false);
                // Property setters invoked by the Trail can make the Extender write its
                // normal storage file. Replace that transient file with locally owned data.
                WriteCombinedPayload();
                DebugLogHelper.LogInfo(log, $"[{modName}] Entered {(editable ? "editable" : "read-only")} mission preset.");
            }

            public void ExitMissionPreset()
            {
                missionPreset = null;
                ApplyPreset(localSelectedPreset);
                DebugLogHelper.LogInfo(log, $"[{modName}] Left mission preset and restored preset {localSelectedPreset + 1}.");
            }

            public void AfterPropertyChanged(string propertyName)
            {
                if (!active || applying || string.IsNullOrEmpty(propertyName))
                    return;

                persistedPropertiesByName.TryGetValue(
                    propertyName,
                    out PropertyInfo property);

                if (property == null)
                {
                    // The Extender also saves for UI-only PropertyChanged notifications.
                    // Restore preset metadata and the safe host/client composition.
                    WriteCombinedPayload();
                    return;
                }

                if (owner.missionPresetContext)
                {
                    if (IsHostProperty(property))
                    {
                        if (owner.missionPresetEditable && owner.isLocalHost)
                            StoreProperty(missionPreset, property);
                        // Never leave an externally owned Trail value in local msgpack.
                        WriteCombinedPayload();
                        return;
                    }

                    // Trail owns only host settings. Personal settings remain editable
                    // and persistent without copying Trail values into local msgpack.
                    if (IsClientProperty(property))
                    {
                        StoreProperty(EnsurePreset(localSelectedPreset), property);
                        WriteCombinedPayload();
                    }
                    return;
                }

                // Incoming host values are runtime-only on clients.
                if (IsHostProperty(property) && !owner.isLocalHost)
                {
                    // The Script Extender suppresses its own storage handler while it
                    // applies network data. Do not turn that receive into a local write.
                    return;
                }

                if (owner.isLocalHost || IsClientProperty(property))
                {
                    StoreProperty(EnsurePreset(localSelectedPreset), property);
                    WriteCombinedPayload();
                }
            }

            private Dictionary<string, byte[]> EnsurePreset(int selected)
            {
                Dictionary<string, byte[]> preset = GetPreset(selected);
                if (preset == null)
                {
                    preset = Clone(defaults);
                    SetPreset(selected, preset);
                }
                return preset;
            }

            private void ApplyPreset(int selected)
            {
                Dictionary<string, byte[]> stored = GetPreset(selected);
                ApplySnapshot(stored, selected, writeLocalStorage: true);
            }

            private void ApplySnapshot(
                Dictionary<string, byte[]> stored,
                int selected,
                bool writeLocalStorage)
            {
                applying = true;
                try
                {
                    foreach (PropertyInfo property in persistedProperties)
                    {
                        bool include = selected == 2
                            ? IsHostProperty(property)
                            : owner.isLocalHost || IsClientProperty(property);
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

                if (writeLocalStorage)
                    WriteCombinedPayload();
            }

            private bool TryApplyProperty(PropertyInfo property, byte[] bytes)
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
                        $"[{modName}] Could not restore [{property.Name}] from preset {owner.selectedPreset + 1}: {exception.Message}");
                    return false;
                }
            }

            private Dictionary<string, byte[]> CaptureCurrentSettings()
            {
                Dictionary<string, byte[]> snapshot =
                    new Dictionary<string, byte[]>(StringComparer.Ordinal);
                foreach (PropertyInfo property in persistedProperties)
                    StoreProperty(snapshot, property);
                return snapshot;
            }

            private void StoreProperty(
                Dictionary<string, byte[]> snapshot,
                PropertyInfo property)
            {
                if (!property.CanRead)
                    return;

                try
                {
                    object value = property.GetValue(owner);
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
                        $"[{modName}] Could not capture [{property.Name}] for preset {owner.selectedPreset + 1}: {exception.Message}");
                }
            }

            private void WriteCombinedPayload()
            {
                Dictionary<string, byte[]> payload = ComposeSafeTopLevelSnapshot();
                payload[SchemaVersionKey] = MessagePackSerializer.Serialize(SchemaVersion);
                payload[ActivePresetKey] = MessagePackSerializer.Serialize(localSelectedPreset);
                payload[Preset1Key] = MessagePackSerializer.Serialize(preset1 ?? Clone(defaults));
                if (preset2 != null)
                    payload[Preset2Key] = MessagePackSerializer.Serialize(preset2);

                string directory = Path.GetDirectoryName(filePath);
                string temporaryPath = filePath + ".tmp-" + Guid.NewGuid().ToString("N");
                try
                {
                    Directory.CreateDirectory(directory);
                    File.WriteAllBytes(temporaryPath, MessagePackSerializer.Serialize(payload));
                    if (File.Exists(filePath))
                        File.Replace(temporaryPath, filePath, null);
                    else
                        File.Move(temporaryPath, filePath);
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(
                        log,
                        $"[{modName}] Could not save lobby-settings presets to [{filePath}]: {exception}");
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
                Dictionary<string, byte[]> ownedPreset = GetPreset(localSelectedPreset) ?? defaults;

                foreach (PropertyInfo property in persistedProperties)
                {
                    bool mayCaptureLive = IsClientProperty(property) ||
                        (owner.isLocalHost && !owner.missionPresetContext);
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

            private Dictionary<string, byte[]> GetPreset(int selected)
            {
                return selected == 1 ? preset2 : preset1;
            }

            private void SetPreset(int selected, Dictionary<string, byte[]> snapshot)
            {
                if (selected == 1)
                    preset2 = snapshot;
                else
                    preset1 = snapshot;
            }

            private static int NormalizePreset(int selected)
            {
                return selected == 1 ? 1 : 0;
            }

            private static bool IsPersistedProperty(PropertyInfo property)
            {
                return property.GetCustomAttribute<SyncPerPlayerAttribute>() != null ||
                    property.GetCustomAttribute<SyncHostOnlyAttribute>() != null ||
                    property.GetCustomAttribute<PresetLocalAttribute>() != null;
            }

            private static bool IsHostProperty(PropertyInfo property) =>
                property.GetCustomAttribute<SyncHostOnlyAttribute>() != null;

            private static bool IsClientProperty(PropertyInfo property) =>
                property.GetCustomAttribute<SyncPerPlayerAttribute>() != null ||
                property.GetCustomAttribute<PresetLocalAttribute>() != null;

            private static Dictionary<string, byte[]> CopyProperties(
                Dictionary<string, byte[]> source,
                IEnumerable<PropertyInfo> properties)
            {
                Dictionary<string, byte[]> result =
                    new Dictionary<string, byte[]>(StringComparer.Ordinal);
                if (source == null)
                    return result;

                foreach (PropertyInfo property in properties)
                {
                    if (source.TryGetValue(property.Name, out byte[] bytes))
                        result[property.Name] = bytes == null ? null : (byte[])bytes.Clone();
                }
                return result;
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

    public static class LobbyModSettingsPresetRegistration
    {
        public static void Register(
            BaseUnityPlugin plugin,
            ManualLogSource log,
            string modName,
            PresetLobbyModSettingsViewModel viewModel,
            string xamlSourceFile)
        {
            if (plugin == null)
                throw new ArgumentNullException(nameof(plugin));
            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));

            viewModel.PreparePresets(log, plugin.Info.Location, modName);
            GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(
                plugin,
                modName,
                viewModel,
                xamlSourceFile);

            bool registered = GameXAMLManagerAPI.Instance.RegisteredModSettings
                .Any(entry => ReferenceEquals(entry.ViewModel, viewModel));
            if (!registered)
            {
                DebugLogHelper.LogError(
                    log,
                    $"[{modName}] Presets were not activated because lobby-settings registration failed.");
                return;
            }

            viewModel.ActivatePresets();

            // Views are created before a lobby exists. Refresh the cached role whenever
            // the persistent settings hub opens or changes its selected tab.
            Plugin.ModSettingsHubViewModel.PropertyChanged += (_, __) =>
                viewModel.System_RefreshSettingsAccess();
        }
    }
}
