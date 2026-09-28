using BepInEx.Logging;
using CrusaderDE;
using MessagePack;
using MessagePack.Formatters;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.Archive;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.SaveData;
using SHCDESE.IO;
using Shared;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace APIShared
{
    /// <summary>Versioned savegame metadata for host settings and mission permission mode.</summary>
    [MessagePackObject]
    [MessagePackFormatter(typeof(SavegameModSettingsRecordFormatter))]
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
        /// <summary>Trail author rules keyed by plugin GUID and host property.</summary>
        [Key(5)] public Dictionary<string, Dictionary<string, TrailCreatorRule>> CreatorRules { get; set; }
        /// <summary>Whether the player actually launched through Customize.</summary>
        [Key(6)] public bool TrailCustomizeAllowed { get; set; }
    }

    /// <summary>One Trail author's host-property rule. Mode: default=0, player=1, fixed=2.</summary>
    public sealed class TrailCreatorRule
    {
        /// <summary>Author-selected mode.</summary>
        public int Mode { get; set; }
        /// <summary>MessagePack value for a fixed rule.</summary>
        public byte[] FixedValue { get; set; }
    }

    /// <summary>Availability and permission state of one selected savegame.</summary>
    public enum SavegameLoadChoiceState
    {
        /// <summary>The save or its present metadata cannot be trusted.</summary>
        Invalid = 0,
        /// <summary>No APIShared record exists; current host settings are mandatory.</summary>
        Legacy = 1,
        /// <summary>Saved host settings are mandatory.</summary>
        SavedOnly = 2,
        /// <summary>The host may choose saved or current settings.</summary>
        Selectable = 3,
    }

    /// <summary>Explicit wire format; avoids MessagePack's unavailable dynamic formatter dependency.</summary>
    public sealed class SavegameModSettingsRecordFormatter : IMessagePackFormatter<SavegameModSettingsRecord>
    {
        /// <summary>Writes the bounded record without dynamic object formatters.</summary>
        public void Serialize(ref MessagePackWriter writer, SavegameModSettingsRecord value,
            MessagePackSerializerOptions options)
        {
            if (value == null) { writer.WriteNil(); return; }
            writer.WriteArrayHeader(7);
            writer.Write(value.Version);
            writer.Write(value.Kind);
            writer.Write(value.Variant);
            WriteValues(ref writer, value.Mods);
            writer.Write(value.LockedByConflict);
            WriteRules(ref writer, value.CreatorRules);
            writer.Write(value.TrailCustomizeAllowed);
        }

        /// <summary>Reads the versioned record with explicit bounds before allocating entries.</summary>
        public SavegameModSettingsRecord Deserialize(ref MessagePackReader reader,
            MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;
            if (reader.ReadArrayHeader() != 7) return null;
            var value = new SavegameModSettingsRecord
            {
                Version = reader.ReadInt32(), Kind = reader.ReadInt32(),
                Variant = reader.ReadInt32(), Mods = ReadValues(ref reader),
                LockedByConflict = reader.ReadBoolean(), CreatorRules = ReadRules(ref reader),
                TrailCustomizeAllowed = reader.ReadBoolean(),
            };
            return value;
        }

        private static void WriteValues(ref MessagePackWriter writer,
            Dictionary<string, Dictionary<string, byte[]>> values)
        {
            writer.WriteMapHeader(values?.Count ?? 0);
            if (values == null) return;
            foreach (var mod in values.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                writer.Write(mod.Key);
                writer.WriteMapHeader(mod.Value?.Count ?? 0);
                if (mod.Value == null) continue;
                foreach (var property in mod.Value.OrderBy(item => item.Key, StringComparer.Ordinal))
                { writer.Write(property.Key); writer.Write(property.Value); }
            }
        }

        private static Dictionary<string, Dictionary<string, byte[]>> ReadValues(ref MessagePackReader reader)
        {
            int count = reader.ReadMapHeader();
            if (count > 1024) throw new InvalidDataException("Too many savegame mods.");
            var values = new Dictionary<string, Dictionary<string, byte[]>>(StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
            {
                string mod = reader.ReadString();
                int properties = reader.ReadMapHeader();
                if (properties > 2048) throw new InvalidDataException("Too many savegame properties.");
                var stored = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                for (int j = 0; j < properties; j++)
                {
                    string name = reader.ReadString();
                    byte[] data = reader.ReadBytes()?.ToArray();
                    if (name == null || data == null || data.Length > 1024 * 1024 || stored.ContainsKey(name))
                        throw new InvalidDataException("Invalid savegame property.");
                    stored.Add(name, data);
                }
                if (mod == null || values.ContainsKey(mod)) throw new InvalidDataException("Duplicate savegame mod.");
                values.Add(mod, stored);
            }
            return values;
        }

        private static void WriteRules(ref MessagePackWriter writer,
            Dictionary<string, Dictionary<string, TrailCreatorRule>> rules)
        {
            writer.WriteMapHeader(rules?.Count ?? 0);
            if (rules == null) return;
            foreach (var mod in rules.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                writer.Write(mod.Key);
                writer.WriteMapHeader(mod.Value?.Count ?? 0);
                if (mod.Value == null) continue;
                foreach (var property in mod.Value.OrderBy(item => item.Key, StringComparer.Ordinal))
                {
                    writer.Write(property.Key);
                    writer.WriteArrayHeader(2);
                    writer.Write(property.Value.Mode);
                    writer.Write(property.Value.FixedValue);
                }
            }
        }

        private static Dictionary<string, Dictionary<string, TrailCreatorRule>> ReadRules(ref MessagePackReader reader)
        {
            int count = reader.ReadMapHeader();
            if (count > 1024) throw new InvalidDataException("Too many Trail rule mods.");
            var rules = new Dictionary<string, Dictionary<string, TrailCreatorRule>>(StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
            {
                string mod = reader.ReadString();
                int properties = reader.ReadMapHeader();
                if (properties > 2048) throw new InvalidDataException("Too many Trail rules.");
                var stored = new Dictionary<string, TrailCreatorRule>(StringComparer.Ordinal);
                for (int j = 0; j < properties; j++)
                {
                    string name = reader.ReadString();
                    if (reader.ReadArrayHeader() != 2) throw new InvalidDataException("Invalid Trail rule.");
                    var rule = new TrailCreatorRule { Mode = reader.ReadInt32(), FixedValue = reader.ReadBytes()?.ToArray() };
                    if (name == null || stored.ContainsKey(name)) throw new InvalidDataException("Duplicate Trail rule.");
                    stored.Add(name, rule);
                }
                if (mod == null || rules.ContainsKey(mod)) throw new InvalidDataException("Duplicate Trail rule mod.");
                rules.Add(mod, stored);
            }
            return rules;
        }
    }

    /// <summary>Persists compatible host settings and the mission permission context in saves.</summary>
    public static class SavegameModSettings
    {
        private const string Identifier = "APIShared-SavegameModSettings";
        private const string EntryName = "_SE_ModData_" + Identifier + ".msgpack";
        private const int SchemaVersion = 3;
        private const int MaximumBytes = 4 * 1024 * 1024;
        private const int MaximumMods = 1024;
        private const int MaximumPropertiesPerMod = 2048;
        private const int MaximumPropertyBytes = 1024 * 1024;
        private static readonly object Sync = new object();
        private static ManualLogSource log;
        private static SavegameModSettingsRecord loaded;
        private static Dictionary<string, Dictionary<string, byte[]>> chosenCurrent;
        private static bool useChosenCurrent;
        private static bool legacyChoice;
        private static Dictionary<string, Dictionary<string, TrailCreatorRule>> activeCreatorRules;
        private static Func<Dictionary<string, Dictionary<string, TrailCreatorRule>>> creatorRulesProvider;
        private static Func<bool> trailCustomizeProvider;
        private static bool activeTrailCustomizeAllowed;
        private static string chosenPath;
        private static bool restoreFailed;
        private static bool initialized;
        private static readonly SavegameMissionPresetLease ActivePreset = new SavegameMissionPresetLease();
        private static IDisposable missionEndSubscription;

        internal static void Initialize(ManualLogSource logger)
        {
            lock (Sync)
            {
                if (initialized) return;
                Shared.MissionEvents.SetOwner("APIShared_Serp");
                missionEndSubscription = Shared.MissionEvents.Ended.Subscribe(OnMissionEnded);
                try
                {
                    if (!ModSaveDataAPI.Instance.RegisterModDataHandler(Identifier, Save, Load))
                        throw new InvalidOperationException("Savegame ModSettings handler already exists.");
                }
                catch
                {
                    missionEndSubscription.Dispose();
                    missionEndSubscription = null;
                    throw;
                }
                GameModeHelper.ReadSavedModeEvidence = () =>
                {
                    bool present = TryGetRestoredMode(out GameModeKind kind,
                        out GameModeLaunchVariant variant, out bool locked);
                    return new SavedModeEvidence(present, kind, variant, locked);
                };
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

        /// <summary>Describes the selected save's modsettings choice.</summary>
        public static SavegameLoadChoiceState GetLoadChoiceState(string savePath)
        {
            RecordReadState state = ReadRecord(savePath, out SavegameModSettingsRecord record);
            return state == RecordReadState.Missing ? SavegameLoadChoiceState.Legacy :
                state == RecordReadState.Invalid ? SavegameLoadChoiceState.Invalid :
                IsCurrentChoiceAllowed(record) ? SavegameLoadChoiceState.Selectable :
                SavegameLoadChoiceState.SavedOnly;
        }

        /// <summary>Registers the ExtendedData source of author rules for a newly launched Trail.</summary>
        public static void RegisterTrailCreatorRulesProvider(
            Func<Dictionary<string, Dictionary<string, TrailCreatorRule>>> provider,
            Func<bool> customizeProvider)
        {
            lock (Sync)
            {
                creatorRulesProvider = provider;
                trailCustomizeProvider = customizeProvider;
            }
        }

        internal static bool IsCurrentChoiceAllowed(SavegameModSettingsRecord record)
        {
            if (record == null || record.LockedByConflict) return false;
            GameModeKind kind = (GameModeKind)record.Kind;
            GameModeLaunchVariant variant = (GameModeLaunchVariant)record.Variant;
            if (kind == GameModeKind.CustomTrail || kind == GameModeKind.CoopTrail)
                return record.TrailCustomizeAllowed;
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
            SavegameLoadChoiceState state = GetLoadChoiceState(path);
            Dictionary<string, Dictionary<string, byte[]>> current = CaptureCompatibleHostSettings();
            lock (Sync)
            {
                chosenPath = path.Length == 0 ? null : path;
                chosenCurrent = current;
                useChosenCurrent = state == SavegameLoadChoiceState.Legacy ||
                    state == SavegameLoadChoiceState.Selectable && useCurrentSettings;
                legacyChoice = state == SavegameLoadChoiceState.Legacy;
            }
        }

        /// <summary>Discards an unconsumed load-dialog choice after cancellation or failure.</summary>
        public static void CancelLoadChoice()
        {
            lock (Sync)
            {
                chosenPath = null;
                chosenCurrent = null;
                useChosenCurrent = false;
                legacyChoice = false;
            }
        }

        internal static void BeginLoad()
        {
            Dictionary<string, Dictionary<string, byte[]>> fallback = null;
            lock (Sync) { if (chosenCurrent == null) fallback = CaptureCompatibleHostSettings(); }
            lock (Sync)
            {
                loaded = null;
                restoreFailed = false;
                if (chosenCurrent == null) chosenCurrent = fallback;
            }
        }

        internal static bool TryGetRestoredMode(out GameModeKind kind,
            out GameModeLaunchVariant variant, out bool lockedByConflict)
        {
            lock (Sync)
            {
                kind = loaded == null ? GameModeKind.Unknown : (GameModeKind)loaded.Kind;
                variant = loaded == null ? GameModeLaunchVariant.Standard : (GameModeLaunchVariant)loaded.Variant;
                lockedByConflict = loaded == null ? !legacyChoice || restoreFailed :
                    loaded.LockedByConflict || restoreFailed;
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
            bool useCurrent;
            bool legacy;
            lock (Sync)
            {
                record = loaded;
                current = chosenCurrent;
                useCurrent = useChosenCurrent;
                legacy = legacyChoice;
            }
            if (record == null)
            {
                lock (Sync)
                {
                    activeCreatorRules = null;
                    activeTrailCustomizeAllowed = false;
                }
                if (!legacy) return;
            }
            else
            {
                lock (Sync)
                {
                    activeCreatorRules = record.CreatorRules;
                    activeTrailCustomizeAllowed = record.TrailCustomizeAllowed;
                }
            }
            // Only the host publishes host-owned values. The Extender's existing
            // SyncHostOnly route sends property changes to clients.
            if (context.Mode.IsRealMultiplayer && context.IsHost != true) return;
            Dictionary<string, Dictionary<string, byte[]>> values =
                legacy || useCurrent && IsCurrentChoiceAllowed(record) ? current : record.Mods;
            bool locked = record?.LockedByConflict == true;
            if (locked) values = null;
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
                    bool strictTrail = record != null && !record.TrailCustomizeAllowed &&
                        (record.Kind == (int)GameModeKind.CustomTrail || record.Kind == (int)GameModeKind.CoopTrail);
                    if (strictTrail || locked)
                    {
                        Dictionary<string, byte[]> baseline = participant.Value.Endpoint.System_CreateDisabledMissionPresetSnapshot();
                        foreach (PropertyInfo property in participant.Value.Properties)
                            if (baseline.TryGetValue(property.Name, out byte[] disabled))
                                snapshot[property.Name] = disabled;
                    }
                    if (!locked && record?.CreatorRules != null &&
                        record.CreatorRules.TryGetValue(participant.Key, out var rules))
                    {
                        ApplyCreatorRules(snapshot, participant.Value.Properties, rules,
                            current != null && current.TryGetValue(participant.Key, out var currentMod) ? currentMod : null,
                            strictTrail);
                    }
                    participant.Value.Endpoint.System_ExitMissionPreset();
                    participant.Value.Endpoint.System_EnterMissionPreset(snapshot, "Savegame", false);
                    if (!participant.Value.Endpoint.IsMissionPresetActive)
                        throw new InvalidOperationException("The savegame preset did not become active.");
                    ActivePreset.Track(context.SessionId, participant.Key, participant.Value.Endpoint);
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
                useChosenCurrent = false;
                legacyChoice = false;
                restoreFailed = false;
            }
        }

        private static void OnMissionEnded(MissionLifecycleNotification notification)
        {
            if (notification?.Context?.IsSave != true) return;
            ActivePreset.ReleaseOnEnd(notification.Context.SessionId,
                (modId, error) => NativeApiLog.Error(log,
                    "Could not leave savegame settings for " + modId + ": " + error));
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
                CreatorRules = CaptureCreatorRules(mission),
                TrailCustomizeAllowed = CaptureTrailCustomizeAllowed(mission),
            };
            return SerializeWithinLimit(record);
        }

        private static Dictionary<string, Dictionary<string, TrailCreatorRule>> CaptureCreatorRules(MissionContext mission)
        {
            if (mission.Mode.Kind != GameModeKind.CustomTrail && mission.Mode.Kind != GameModeKind.CoopTrail)
                return new Dictionary<string, Dictionary<string, TrailCreatorRule>>(StringComparer.Ordinal);
            try
            {
                Dictionary<string, Dictionary<string, TrailCreatorRule>> rules;
                lock (Sync)
                {
                    rules = mission.IsSave ? activeCreatorRules : creatorRulesProvider?.Invoke();
                }
                return rules ?? new Dictionary<string, Dictionary<string, TrailCreatorRule>>(StringComparer.Ordinal);
            }
            catch (Exception error)
            {
                NativeApiLog.Error(log, "Could not capture Trail creator rules: " + error);
                return new Dictionary<string, Dictionary<string, TrailCreatorRule>>(StringComparer.Ordinal);
            }
        }

        private static bool CaptureTrailCustomizeAllowed(MissionContext mission)
        {
            if (mission.Mode.Kind != GameModeKind.CustomTrail && mission.Mode.Kind != GameModeKind.CoopTrail)
                return false;
            try
            {
                lock (Sync) return mission.IsSave ? activeTrailCustomizeAllowed : trailCustomizeProvider?.Invoke() == true;
            }
            catch (Exception error)
            {
                NativeApiLog.Error(log, "Could not capture Trail Customize origin: " + error.Message);
                return false;
            }
        }

        internal static void ApplyCreatorRules(Dictionary<string, byte[]> snapshot,
            IEnumerable<PropertyInfo> properties, Dictionary<string, TrailCreatorRule> rules,
            Dictionary<string, byte[]> current, bool strictTrail)
        {
            foreach (PropertyInfo property in properties)
            {
                if (!rules.TryGetValue(property.Name, out TrailCreatorRule rule)) continue;
                byte[] bytes = rule.Mode == 1 ?
                    current != null && current.TryGetValue(property.Name, out byte[] player) ? player : null :
                    strictTrail && rule.Mode == 2 ? rule.FixedValue : null;
                if (bytes == null) continue;
                try
                {
                    if (MessagePackSerializer.Deserialize(property.PropertyType, bytes) != null)
                        snapshot[property.Name] = (byte[])bytes.Clone();
                }
                catch (Exception error)
                {
                    NativeApiLog.Error(log, "Invalid Trail creator rule " + property.Name + ": " + error.Message);
                }
            }
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

        private enum RecordReadState { Invalid, Missing, Valid }

        private static bool TryReadRecord(string path, out SavegameModSettingsRecord record) =>
            ReadRecord(path, out record) == RecordReadState.Valid;

        private static RecordReadState ReadRecord(string path, out SavegameModSettingsRecord record)
        {
            record = null;
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return RecordReadState.Invalid;
                if (!MapArchive.TryLoad(path, out MapArchive archive))
                {
                    // Vanilla saves may have no appended ZIP at all. An identifiable
                    // damaged ZIP is different from a genuinely missing mod entry.
                    return ZipUtil.FindEocdHeaderIndex(path) < 0 &&
                        ZipUtil.FindFirstZipHeaderIndex(path) < 0
                        ? RecordReadState.Missing : RecordReadState.Invalid;
                }
                using (archive)
                {
                    byte[] bytes = archive.TryReadBinaryFile(EntryName, true);
                    if (bytes == null) return RecordReadState.Missing;
                    return TryDeserialize(bytes, out record) ? RecordReadState.Valid : RecordReadState.Invalid;
                }
            }
            catch (Exception error)
            {
                NativeApiLog.Error(log, "Could not inspect savegame ModSettings: " + error.Message);
                return RecordReadState.Invalid;
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
                    candidate.CreatorRules == null ||
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
                if (candidate.CreatorRules.Count > MaximumMods) return false;
                foreach (var mod in candidate.CreatorRules)
                {
                    if (string.IsNullOrWhiteSpace(mod.Key) || mod.Key.Length > 256 ||
                        mod.Value == null || mod.Value.Count > MaximumPropertiesPerMod) return false;
                    foreach (var property in mod.Value)
                    {
                        TrailCreatorRule rule = property.Value;
                        if (string.IsNullOrWhiteSpace(property.Key) || property.Key.Length > 256 ||
                            rule == null || rule.Mode < 0 || rule.Mode > 2 ||
                            (rule.Mode == 2 && (rule.FixedValue == null || rule.FixedValue.Length > MaximumPropertyBytes)) ||
                            (rule.Mode != 2 && rule.FixedValue != null)) return false;
                    }
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
            // Rules cannot be truncated selectively: doing so could turn a fixed
            // Trail restriction into an unrestricted current value on restore.
            record.LockedByConflict = true;
            record.CreatorRules?.Clear();
            bytes = MessagePackSerializer.Serialize(record);
            if (bytes.Length <= MaximumBytes) return bytes;
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

    internal sealed class SavegameMissionPresetLease
    {
        private readonly object sync = new object();
        private readonly Dictionary<long, Dictionary<string, IModSettingsPresetEndpoint>> sessions =
            new Dictionary<long, Dictionary<string, IModSettingsPresetEndpoint>>();

        internal void Track(long sessionId, string modId, IModSettingsPresetEndpoint endpoint)
        {
            if (sessionId <= 0 || string.IsNullOrEmpty(modId) || endpoint == null)
                throw new ArgumentException("A savegame preset requires a valid session and endpoint.");
            lock (sync)
            {
                if (!sessions.TryGetValue(sessionId, out var participants))
                {
                    participants = new Dictionary<string, IModSettingsPresetEndpoint>(StringComparer.Ordinal);
                    sessions.Add(sessionId, participants);
                }
                participants[modId] = endpoint;
            }
        }

        internal void ReleaseOnEnd(long sessionId, Action<string, Exception> reportError)
        {
            Dictionary<string, IModSettingsPresetEndpoint> participants;
            lock (sync)
            {
                if (!sessions.TryGetValue(sessionId, out participants)) return;
                sessions.Remove(sessionId);
            }
            foreach (KeyValuePair<string, IModSettingsPresetEndpoint> participant in participants)
            {
                try { participant.Value.System_ExitMissionPreset(); }
                catch (Exception error)
                {
                    try { reportError?.Invoke(participant.Key, error); }
                    catch { }
                }
            }
        }
    }
}
