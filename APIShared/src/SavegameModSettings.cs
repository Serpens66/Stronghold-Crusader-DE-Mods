using BepInEx.Logging;
using CrusaderDE;
using MessagePack;
using SHCDESE.API;
using SHCDESE.API.Components.Archive;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.SaveData;
using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace APIShared
{
    /// <summary>Versioned savegame metadata for host settings and mission permission mode.</summary>
    [MessagePackObject]
    public sealed class SavegameModSettingsRecord
    {
        /// <summary>MessagePack schema version.</summary>
        [Key(0)] public int Version { get; set; }
        /// <summary>Saved mission kind.</summary>
        [Key(1)] public int Kind { get; set; }
        /// <summary>Saved Customize state.</summary>
        [Key(2)] public int Variant { get; set; }
        /// <summary>Persistent host properties keyed by plugin GUID and property name.</summary>
        [Key(3)] public Dictionary<string, Dictionary<string, byte[]>> Mods { get; set; }
        /// <summary>Whether the mission was locked by conflicting launch evidence when saved.</summary>
        [Key(4)] public bool LockedByConflict { get; set; }
    }

    /// <summary>Persists compatible host settings and the mission permission context in saves.</summary>
    public static class SavegameModSettings
    {
        private const string Identifier = "APIShared-SavegameModSettings";
        private const string EntryName = "_SE_ModData_" + Identifier + ".msgpack";
        private const int SchemaVersion = 2;
        private const int MaximumBytes = 4 * 1024 * 1024;
        private const int MaximumMods = 1024;
        private const int MaximumPropertiesPerMod = 2048;
        private const int MaximumPropertyBytes = 1024 * 1024;
        private static readonly object Sync = new object();
        private static ManualLogSource log;
        private static SavegameModSettingsRecord loaded;
        private static Dictionary<string, Dictionary<string, byte[]>> chosenCurrent;
        private static string chosenPath;
        private static bool restoreFailed;
        private static bool initialized;

        internal static void Initialize(ManualLogSource logger)
        {
            lock (Sync)
            {
                if (initialized) return;
                if (!ModSaveDataAPI.Instance.RegisterModDataHandler(Identifier, Save, Load))
                    throw new InvalidOperationException("Savegame ModSettings handler already exists.");
                log = logger;
                initialized = true;
            }
            NativeApiLog.Info(logger, "Savegame ModSettings handler registered after Script Extender startup cleanup.");
        }

        /// <summary>Reads only the selected save's own appended metadata for the load checkbox.</summary>
        public static bool CanUseCurrentSettings(string savePath)
        {
            if (!TryReadRecord(savePath, out SavegameModSettingsRecord record)) return false;
            return IsCurrentChoiceAllowed(record);
        }

        internal static bool IsCurrentChoiceAllowed(SavegameModSettingsRecord record)
        {
            if (record == null || record.LockedByConflict) return false;
            GameModeKind kind = (GameModeKind)record.Kind;
            GameModeLaunchVariant variant = (GameModeLaunchVariant)record.Variant;
            return kind == GameModeKind.CustomGame ||
                (variant != GameModeLaunchVariant.Standard &&
                 (kind == GameModeKind.CustomTrail || kind == GameModeKind.CoopTrail ||
                  kind == GameModeKind.VanillaTrail || kind == GameModeKind.SandsOfTime));
        }

        /// <summary>Returns compatible settings endpoints keyed by the owning plugin GUID.</summary>
        public static IReadOnlyDictionary<string, IModSettingsPresetEndpoint> GetCompatibleParticipants() =>
            FindParticipants().ToDictionary(item => item.Key, item => item.Value.Endpoint, StringComparer.Ordinal);

        /// <summary>Records the confirmed host choice for exactly one selected save.</summary>
        public static void PrepareLoad(string savePath, bool useCurrentSettings)
        {
            string path = Normalize(savePath);
            bool permitted = CanUseCurrentSettings(path);
            Dictionary<string, Dictionary<string, byte[]>> current =
                useCurrentSettings && permitted ? CaptureCompatibleHostSettings() : null;
            lock (Sync)
            {
                chosenPath = path.Length == 0 ? null : path;
                chosenCurrent = current;
            }
        }

        /// <summary>Discards an unconsumed load-dialog choice after cancellation or failure.</summary>
        public static void CancelLoadChoice()
        {
            lock (Sync)
            {
                chosenPath = null;
                chosenCurrent = null;
            }
        }

        internal static void BeginLoad()
        {
            lock (Sync)
            {
                loaded = null;
                restoreFailed = false;
            }
        }

        internal static bool TryGetRestoredMode(out GameModeKind kind,
            out GameModeLaunchVariant variant, out bool lockedByConflict)
        {
            lock (Sync)
            {
                kind = loaded == null ? GameModeKind.Unknown : (GameModeKind)loaded.Kind;
                variant = loaded == null ? GameModeLaunchVariant.Standard : (GameModeLaunchVariant)loaded.Variant;
                lockedByConflict = loaded == null || loaded.LockedByConflict || restoreFailed;
                return loaded != null;
            }
        }

        internal static void MarkRestoreFailed()
        {
            lock (Sync) restoreFailed = true;
        }

        internal static void ApplyLoadedSettings(MissionContext context)
        {
            if (context == null || !context.IsSave) return;
            SavegameModSettingsRecord record;
            Dictionary<string, Dictionary<string, byte[]>> current;
            lock (Sync)
            {
                record = loaded;
                current = chosenCurrent;
            }
            if (record == null) return;
            // Only the host publishes host-owned values. The Extender's existing
            // SyncHostOnly route sends property changes to clients.
            if (context.Mode.IsRealMultiplayer && context.IsHost != true) return;
            Dictionary<string, Dictionary<string, byte[]>> values =
                current != null && IsCurrentChoiceAllowed(record) ? current : record.Mods;
            Dictionary<string, CompatibleMod> participants;
            try { participants = FindParticipants(); }
            catch (Exception error)
            {
                MarkRestoreFailed();
                NativeApiLog.Error(log, "Could not enumerate savegame settings participants: " + error);
                return;
            }
            foreach (KeyValuePair<string, CompatibleMod> participant in participants)
            {
                try
                {
                    Dictionary<string, byte[]> snapshot =
                        participant.Value.Endpoint.System_CreateDisabledMissionPresetSnapshot();
                    if (snapshot == null)
                    {
                        MarkRestoreFailed();
                        continue;
                    }
                    // Player-owned and local values are outside the savegame host contract.
                    foreach (PropertyInfo property in participant.Value.PersonalProperties)
                    {
                        try
                        {
                            object value = property.GetValue(participant.Value.Endpoint);
                            if (value != null)
                                snapshot[property.Name] = MessagePackSerializer.Serialize(property.PropertyType, value);
                        }
                        catch (Exception error)
                        {
                            MarkRestoreFailed();
                            NativeApiLog.Error(log, "Could not preserve local setting " +
                                participant.Key + "." + property.Name + ": " + error.Message);
                        }
                    }
                    if (values != null && values.TryGetValue(participant.Key, out Dictionary<string, byte[]> stored))
                        OverlaySavedHostProperties(snapshot, participant.Value.Properties, stored, participant.Key);
                    participant.Value.Endpoint.System_ExitMissionPreset();
                    participant.Value.Endpoint.System_EnterMissionPreset(snapshot, "Savegame", false);
                }
                catch (Exception error)
                {
                    MarkRestoreFailed();
                    NativeApiLog.Error(log, "Could not restore saved settings for " + participant.Key + ": " + error);
                }
            }
        }

        internal static void FinishLoad()
        {
            lock (Sync)
            {
                loaded = null;
                chosenCurrent = null;
                chosenPath = null;
                restoreFailed = false;
            }
        }

        private static byte[] Save(SaveContext context)
        {
            if (context == null || !context.IsSaveFile || context.IsMapEditorSave) return null;
            MissionContext mission = MissionLifecycleService.ActiveContext;
            if (mission == null || (mission.Mode.IsRealMultiplayer && mission.IsHost != true)) return null;
            var record = new SavegameModSettingsRecord
            {
                Version = SchemaVersion,
                Kind = (int)mission.Mode.Kind,
                Variant = (int)mission.Mode.LaunchVariant,
                LockedByConflict = mission.Mode.HasConflictingCustomizedOrigin,
                Mods = CaptureCompatibleHostSettings(),
            };
            return SerializeWithinLimit(record);
        }

        private static void Load(byte[] bytes, LoadContext context)
        {
            if (context == null || !context.IsSaveFile || !TryDeserialize(bytes, out SavegameModSettingsRecord record)) return;
            lock (Sync)
            {
                if (chosenPath != null &&
                    !string.Equals(chosenPath, Normalize(context.FilePath), StringComparison.OrdinalIgnoreCase))
                    return;
                loaded = record;
            }
        }

        private static bool TryReadRecord(string path, out SavegameModSettingsRecord record)
        {
            record = null;
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) ||
                    !MapArchive.TryLoad(path, out MapArchive archive)) return false;
                using (archive)
                    return TryDeserialize(archive.TryReadBinaryFile(EntryName, true), out record);
            }
            catch (Exception error)
            {
                NativeApiLog.Error(log, "Could not inspect savegame ModSettings: " + error.Message);
                return false;
            }
        }

        internal static bool TryDeserialize(byte[] bytes, out SavegameModSettingsRecord record)
        {
            record = null;
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaximumBytes) return false;
            try
            {
                SavegameModSettingsRecord candidate = MessagePackSerializer.Deserialize<SavegameModSettingsRecord>(bytes);
                if (candidate == null || candidate.Version != SchemaVersion || candidate.Mods == null ||
                    !Enum.IsDefined(typeof(GameModeKind), candidate.Kind) ||
                    !Enum.IsDefined(typeof(GameModeLaunchVariant), candidate.Variant)) return false;
                if (candidate.Kind == (int)GameModeKind.Unknown || candidate.Kind == (int)GameModeKind.MapEditor ||
                    candidate.Mods.Count > MaximumMods) return false;
                foreach (KeyValuePair<string, Dictionary<string, byte[]>> mod in candidate.Mods)
                {
                    if (string.IsNullOrWhiteSpace(mod.Key) || mod.Key.Length > 256 ||
                        mod.Value == null || mod.Value.Count > MaximumPropertiesPerMod) return false;
                    foreach (KeyValuePair<string, byte[]> property in mod.Value)
                        if (string.IsNullOrWhiteSpace(property.Key) || property.Key.Length > 256 ||
                            property.Value == null || property.Value.Length > MaximumPropertyBytes) return false;
                }
                record = candidate;
                return true;
            }
            catch (Exception error)
            {
                NativeApiLog.Error(log, "Invalid savegame ModSettings: " + error.Message);
                return false;
            }
        }

        private static Dictionary<string, Dictionary<string, byte[]>> CaptureCompatibleHostSettings()
        {
            var result = new Dictionary<string, Dictionary<string, byte[]>>(StringComparer.Ordinal);
            Dictionary<string, CompatibleMod> participants;
            try { participants = FindParticipants(); }
            catch (Exception error)
            {
                NativeApiLog.Error(log, "Could not enumerate savegame settings participants: " + error);
                return result;
            }
            foreach (KeyValuePair<string, CompatibleMod> participant in participants
                .OrderBy(item => item.Key, StringComparer.Ordinal).Take(MaximumMods))
            {
                if (participant.Key.Length > 256) continue;
                result[participant.Key] = CaptureHostProperties(participant.Value.Endpoint,
                    participant.Value.Properties.Take(MaximumPropertiesPerMod), participant.Key);
            }
            return result;
        }

        internal static Dictionary<string, byte[]> CaptureHostProperties(
            object endpoint, IEnumerable<PropertyInfo> hostProperties, string modId)
        {
            var properties = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (PropertyInfo property in hostProperties)
            {
                if (property.Name.Length > 256) continue;
                try
                {
                    byte[] bytes = MessagePackSerializer.Serialize(
                        property.PropertyType, property.GetValue(endpoint));
                    if (bytes != null && bytes.Length <= MaximumPropertyBytes)
                        properties[property.Name] = bytes;
                    else
                        NativeApiLog.Error(log, "Savegame setting exceeds the property limit: " +
                            modId + "." + property.Name);
                }
                catch (Exception error)
                {
                    NativeApiLog.Error(log, "Could not capture savegame setting " +
                        modId + "." + property.Name + ": " + error.Message);
                }
            }
            return properties;
        }

        internal static void OverlaySavedHostProperties(Dictionary<string, byte[]> snapshot,
            IEnumerable<PropertyInfo> hostProperties, Dictionary<string, byte[]> stored, string modId)
        {
            if (snapshot == null || stored == null) return;
            foreach (PropertyInfo property in hostProperties)
            {
                if (!stored.TryGetValue(property.Name, out byte[] bytes) || bytes == null) continue;
                try
                {
                    object value = MessagePackSerializer.Deserialize(property.PropertyType, bytes);
                    if (value != null) snapshot[property.Name] = (byte[])bytes.Clone();
                }
                catch (Exception error)
                {
                    NativeApiLog.Error(log, "Invalid saved setting " + modId + "." +
                        property.Name + ": " + error.Message);
                }
            }
        }

        internal static byte[] SerializeWithinLimit(SavegameModSettingsRecord record)
        {
            byte[] bytes = MessagePackSerializer.Serialize(record);
            if (bytes.Length <= MaximumBytes) return bytes;
            foreach (string modId in record.Mods.Keys.OrderByDescending(value => value, StringComparer.Ordinal).ToArray())
            {
                Dictionary<string, byte[]> properties = record.Mods[modId];
                foreach (string propertyName in properties.Keys.OrderByDescending(value => value, StringComparer.Ordinal).ToArray())
                {
                    properties.Remove(propertyName);
                    NativeApiLog.Error(log, "Omitted savegame setting to stay within the payload limit: " +
                        modId + "." + propertyName);
                    bytes = MessagePackSerializer.Serialize(record);
                    if (bytes.Length <= MaximumBytes) return bytes;
                }
                record.Mods.Remove(modId);
                bytes = MessagePackSerializer.Serialize(record);
                if (bytes.Length <= MaximumBytes) return bytes;
            }
            throw new InvalidDataException("Savegame mode metadata alone exceeds the payload limit.");
        }

        private sealed class CompatibleMod
        {
            internal IModSettingsPresetEndpoint Endpoint;
            internal PropertyInfo[] Properties;
            internal PropertyInfo[] PersonalProperties;
        }

        private static Dictionary<string, CompatibleMod> FindParticipants()
        {
            var result = new Dictionary<string, CompatibleMod>(StringComparer.Ordinal);
            foreach (IGrouping<string, LobbyModSettingsEntry> group in GameXAMLManagerAPI.Instance.RegisteredModSettings
                .Where(entry => entry?.Plugin?.Info?.Metadata?.GUID != null)
                .GroupBy(entry => entry.Plugin.Info.Metadata.GUID, StringComparer.Ordinal))
            {
                if (string.Equals(group.Key, "ExtendedData_Serp", StringComparison.Ordinal)) continue;
                if (group.Count() != 1 || group.Any(entry => IsOptedOut(entry.Plugin))) continue;
                LobbyModSettingsEntry registration = group.First();
                if (!(registration.ViewModel is IModSettingsPresetEndpoint endpoint)) continue;
                PropertyInfo[] properties = registration.ViewModel.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(property => property.CanRead && property.CanWrite &&
                        property.GetCustomAttributes(false).Any(attribute => attribute.GetType().Name == "SyncHostOnlyAttribute") &&
                        !property.GetCustomAttributes(false).Any(attribute => attribute.GetType().Name == "DoNotPersistAttribute"))
                    .OrderBy(property => property.Name, StringComparer.Ordinal).ToArray();
                PropertyInfo[] personalProperties = registration.ViewModel.GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(property => property.CanRead && property.CanWrite &&
                        !property.GetCustomAttributes(false).Any(attribute => attribute.GetType().Name == "DoNotPersistAttribute") &&
                        property.GetCustomAttributes(false).Any(attribute =>
                            attribute.GetType().Name == "SyncPerPlayerAttribute" ||
                            attribute.GetType().Name == "PresetLocalAttribute"))
                    .ToArray();
                if (properties.Length == 0 || properties.Any(property =>
                    property.Name == "EnableMod" && property.PropertyType != typeof(bool)) ||
                    properties.GroupBy(property => property.Name, StringComparer.Ordinal).Any(items => items.Skip(1).Any()))
                    continue;
                try
                {
                    Dictionary<string, byte[]> disabled = endpoint.System_CreateDisabledMissionPresetSnapshot();
                    if (disabled == null) continue;
                    bool valid = true;
                    foreach (PropertyInfo property in properties)
                    {
                        if (!disabled.TryGetValue(property.Name, out byte[] value) || value == null)
                        {
                            valid = false;
                            break;
                        }
                        object disabledValue = MessagePackSerializer.Deserialize(property.PropertyType, value);
                        if (property.Name == "EnableMod" && disabledValue is bool enabled && enabled)
                        {
                            valid = false;
                            break;
                        }
                    }
                    if (!valid) continue;
                    result[group.Key] = new CompatibleMod
                    {
                        Endpoint = endpoint,
                        Properties = properties,
                        PersonalProperties = personalProperties,
                    };
                }
                catch (Exception error)
                {
                    NativeApiLog.Error(log, "Incompatible savegame settings for " + group.Key + ": " + error.Message);
                }
            }
            return result;
        }

        private static bool IsOptedOut(object plugin)
        {
            FieldInfo marker = plugin?.GetType().GetField("ExtendedDataModSettingsOptOut",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            return marker != null && marker.FieldType == typeof(bool) && marker.IsLiteral &&
                marker.GetRawConstantValue() is bool value && value;
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            try { return Path.GetFullPath(path ?? string.Empty); }
            catch { return string.Empty; }
        }
    }
}
