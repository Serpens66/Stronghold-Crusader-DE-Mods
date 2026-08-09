using BepInEx;
using BepInEx.Logging;
using MessagePack;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.Network;
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
    /// Adds two local presets to a Script Extender lobby-settings ViewModel while
    /// keeping the outer MessagePack dictionary readable by the Script Extender.
    /// </summary>
    public abstract class PresetLobbyModSettingsViewModel : LobbyModSettingsBaseViewModel
    {
        private ComboBoxItem[] presetOptions = Array.Empty<ComboBoxItem>();
        private PresetController presetController;
        private int selectedPreset;
        private bool trailContext;
        private bool trailEditable;

        public ComboBoxItem[] PresetOptions => presetOptions;

        public bool AreSettingsEditable => !trailContext || selectedPreset != 2 || trailEditable;

        public bool IsTrailPresetActive => trailContext && selectedPreset == 2;

        // Zero-based because Noesis binds this value directly to ComboBox.SelectedIndex.
        public int SelectedPreset
        {
            get => selectedPreset;
            set
            {
                int normalized = trailContext && value == 2 ? 2 : (value == 1 ? 1 : 0);
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
                new ComboBoxItem { Content = "Trail", Visibility = Visibility.Collapsed },
            };

            presetController = new PresetController(
                this,
                log,
                pluginAssemblyLocation,
                modName);
            presetController.CaptureDefaults();
        }

        internal void ActivatePresets()
        {
            if (presetController == null)
                throw new InvalidOperationException("Preset storage must be prepared before it is activated.");

            presetController.Activate();
        }

        // Public reflection boundary used by the elected Shared runtime in another mod assembly.
        public Dictionary<string, byte[]> System_CaptureTrailSnapshot() =>
            presetController?.CaptureCurrent() ?? new Dictionary<string, byte[]>(StringComparer.Ordinal);

        public Dictionary<string, byte[]> System_CreateDisabledTrailSnapshot() =>
            presetController?.CreateDisabledSnapshot() ?? new Dictionary<string, byte[]>(StringComparer.Ordinal);

        public void System_EnterTrailPreset(Dictionary<string, byte[]> snapshot, bool editable)
        {
            if (presetController == null)
                return;

            trailContext = true;
            trailEditable = editable;
            // The items exist when Noesis first materializes the binding. Only the third
            // container's visibility changes, avoiding unsupported ItemsSource refreshes.
            presetOptions[2].Visibility = Visibility.Visible;
            presetController.EnterTrail(snapshot, editable);
            base.OnPropertyChanged(nameof(AreSettingsEditable));
            base.OnPropertyChanged(nameof(IsTrailPresetActive));
        }

        public void System_ExitTrailPreset()
        {
            if (!trailContext || presetController == null)
                return;

            trailContext = false;
            trailEditable = false;
            presetController.ExitTrail();
            presetOptions[2].Visibility = Visibility.Collapsed;
            base.OnPropertyChanged(nameof(AreSettingsEditable));
            base.OnPropertyChanged(nameof(IsTrailPresetActive));
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
            }
        }

        private void SetSelectedPresetCore(int value)
        {
            if (selectedPreset == value)
                return;

            selectedPreset = value;
            OnPropertyChanged(nameof(SelectedPreset));
            base.OnPropertyChanged(nameof(AreSettingsEditable));
            base.OnPropertyChanged(nameof(IsTrailPresetActive));
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
            private readonly Dictionary<string, PropertyInfo> persistedPropertiesByName;

            private Dictionary<string, byte[]> defaults;
            private Dictionary<string, byte[]> preset1;
            private Dictionary<string, byte[]> preset2;
            private Dictionary<string, byte[]> trail;
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
                selected = owner.trailContext && selected == 2 ? 2 : NormalizePreset(selected);
                if (!active || owner.selectedPreset == selected)
                    return;

                if (owner.trailContext)
                {
                    if (selected == 2)
                    {
                        // Trail is mission-owned and must never enter the local preset file.
                        ApplySnapshot(trail ?? CreateDisabledSnapshot(), selected, writeLocalStorage: false);
                        return;
                    }

                    // Presets 1/2 keep their normal persistence semantics even while a
                    // mission-specific Trail preset is available in the same dialog.
                    localSelectedPreset = selected;
                    ApplyPreset(selected);
                    return;
                }

                localSelectedPreset = selected;
                ApplyPreset(selected);
                DebugLogHelper.LogInfo(
                    log,
                    $"[{modName}] Switched to preset {selected + 1}; saved={GetPreset(selected) != null}.");
            }

            public Dictionary<string, byte[]> CaptureCurrent() => CaptureCurrentSettings();

            public Dictionary<string, byte[]> CreateDisabledSnapshot()
            {
                Dictionary<string, byte[]> snapshot = Clone(defaults);
                if (persistedPropertiesByName.TryGetValue("EnableMod", out PropertyInfo enableProperty) &&
                    enableProperty.PropertyType == typeof(bool))
                {
                    snapshot[enableProperty.Name] = MessagePackSerializer.Serialize(false);
                }
                return snapshot;
            }

            public void EnterTrail(Dictionary<string, byte[]> snapshot, bool editable)
            {
                trail = snapshot == null ? CreateDisabledSnapshot() : Clone(snapshot);
                ApplySnapshot(trail, 2, writeLocalStorage: false);
                DebugLogHelper.LogInfo(log, $"[{modName}] Entered {(editable ? "editable" : "read-only")} Trail preset.");
            }

            public void ExitTrail()
            {
                trail = null;
                ApplyPreset(localSelectedPreset);
                DebugLogHelper.LogInfo(log, $"[{modName}] Left Trail preset and restored preset {localSelectedPreset + 1}.");
            }

            public void AfterPropertyChanged(string propertyName)
            {
                if (!active || applying || string.IsNullOrEmpty(propertyName))
                    return;

                if (owner.trailContext && owner.selectedPreset == 2)
                {
                    if (owner.trailEditable && persistedPropertiesByName.TryGetValue(propertyName, out PropertyInfo trailProperty))
                        StoreProperty(trail, trailProperty);
                    return;
                }

                Dictionary<string, byte[]> diskPayload;
                if (TryReadPayload(out diskPayload) &&
                    diskPayload.ContainsKey(SchemaVersionKey))
                {
                    // Incoming network updates do not invoke the Extender's storage.Save.
                    // The marker therefore remains present and the local preset must stay untouched.
                    return;
                }

                if (persistedPropertiesByName.TryGetValue(propertyName, out PropertyInfo property))
                {
                    Dictionary<string, byte[]> currentPreset = GetPreset(owner.selectedPreset);
                    if (currentPreset == null)
                    {
                        currentPreset = Clone(defaults);
                        SetPreset(owner.selectedPreset, currentPreset);
                    }

                    StoreProperty(currentPreset, property);
                }

                WriteCombinedPayload();
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
                Dictionary<string, byte[]> payload = CaptureCurrentSettings();
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
                    property.GetCustomAttribute<SyncHostOnlyAttribute>() != null;
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
            TrailModSettingsRuntime.RegisterParticipant(plugin, log, modName);
        }
    }
}
