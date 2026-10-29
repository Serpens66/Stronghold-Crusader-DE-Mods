using SHCDESE.API.Components.Network;
using APIShared.ModSettings;
using MessagePack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExtraFeatures
{
    public sealed partial class ExtraFeaturesViewModel
    {
        public string CivilianEnemyProximityText => SerpLocalization.Get("SomeSettings.CivilianEnemyProximity");
        public string MilitaryEnemyProximityText => SerpLocalization.Get("SomeSettings.MilitaryEnemyProximity");
        private int humanCivilianEnemyProximitySingleplayer = -1;
        [SyncHostOnly] public int HumanCivilianEnemyProximitySingleplayer { get => humanCivilianEnemyProximitySingleplayer; set => SetEnemyProximity(ref humanCivilianEnemyProximitySingleplayer, value, -1, 100, nameof(HumanCivilianEnemyProximitySingleplayer), nameof(HumanCivilianEnemyProximitySingleplayerValueText)); }
        public string HumanCivilianEnemyProximitySingleplayerValueText { get => FormatWholeTiles(HumanCivilianEnemyProximitySingleplayer); set => SetIntValueText(value, parsed => HumanCivilianEnemyProximitySingleplayer = parsed, nameof(HumanCivilianEnemyProximitySingleplayerValueText)); }
        public string HumanCivilianEnemyProximitySingleplayerSearchTitle => EnemyProximityTitleText + " - " + HumanEnemyProximitySingleplayerText + " - " + CivilianEnemyProximityText;
        private int humanCivilianEnemyProximityMultiplayer = -1;
        [SyncHostOnly] public int HumanCivilianEnemyProximityMultiplayer { get => humanCivilianEnemyProximityMultiplayer; set => SetEnemyProximity(ref humanCivilianEnemyProximityMultiplayer, value, -1, 100, nameof(HumanCivilianEnemyProximityMultiplayer), nameof(HumanCivilianEnemyProximityMultiplayerValueText)); }
        public string HumanCivilianEnemyProximityMultiplayerValueText { get => FormatWholeTiles(HumanCivilianEnemyProximityMultiplayer); set => SetIntValueText(value, parsed => HumanCivilianEnemyProximityMultiplayer = parsed, nameof(HumanCivilianEnemyProximityMultiplayerValueText)); }
        public string HumanCivilianEnemyProximityMultiplayerSearchTitle => EnemyProximityTitleText + " - " + HumanEnemyProximityMultiplayerText + " - " + CivilianEnemyProximityText;
        private int humanMilitaryEnemyProximitySingleplayer = -1;
        [SyncHostOnly] public int HumanMilitaryEnemyProximitySingleplayer { get => humanMilitaryEnemyProximitySingleplayer; set => SetEnemyProximity(ref humanMilitaryEnemyProximitySingleplayer, value, -1, 100, nameof(HumanMilitaryEnemyProximitySingleplayer), nameof(HumanMilitaryEnemyProximitySingleplayerValueText)); }
        public string HumanMilitaryEnemyProximitySingleplayerValueText { get => FormatWholeTiles(HumanMilitaryEnemyProximitySingleplayer); set => SetIntValueText(value, parsed => HumanMilitaryEnemyProximitySingleplayer = parsed, nameof(HumanMilitaryEnemyProximitySingleplayerValueText)); }
        public string HumanMilitaryEnemyProximitySingleplayerSearchTitle => EnemyProximityTitleText + " - " + HumanEnemyProximitySingleplayerText + " - " + MilitaryEnemyProximityText;
        private int humanMilitaryEnemyProximityMultiplayer = -1;
        [SyncHostOnly] public int HumanMilitaryEnemyProximityMultiplayer { get => humanMilitaryEnemyProximityMultiplayer; set => SetEnemyProximity(ref humanMilitaryEnemyProximityMultiplayer, value, -1, 100, nameof(HumanMilitaryEnemyProximityMultiplayer), nameof(HumanMilitaryEnemyProximityMultiplayerValueText)); }
        public string HumanMilitaryEnemyProximityMultiplayerValueText { get => FormatWholeTiles(HumanMilitaryEnemyProximityMultiplayer); set => SetIntValueText(value, parsed => HumanMilitaryEnemyProximityMultiplayer = parsed, nameof(HumanMilitaryEnemyProximityMultiplayerValueText)); }
        public string HumanMilitaryEnemyProximityMultiplayerSearchTitle => EnemyProximityTitleText + " - " + HumanEnemyProximityMultiplayerText + " - " + MilitaryEnemyProximityText;
        private int aiCivilianEnemyProximitySingleplayer = -1;
        [SyncHostOnly] public int AICivilianEnemyProximitySingleplayer { get => aiCivilianEnemyProximitySingleplayer; set => SetEnemyProximity(ref aiCivilianEnemyProximitySingleplayer, value, -1, 100, nameof(AICivilianEnemyProximitySingleplayer), nameof(AICivilianEnemyProximitySingleplayerValueText)); }
        public string AICivilianEnemyProximitySingleplayerValueText { get => FormatWholeTiles(AICivilianEnemyProximitySingleplayer); set => SetIntValueText(value, parsed => AICivilianEnemyProximitySingleplayer = parsed, nameof(AICivilianEnemyProximitySingleplayerValueText)); }
        public string AICivilianEnemyProximitySingleplayerSearchTitle => EnemyProximityTitleText + " - " + AIEnemyProximitySingleplayerText + " - " + CivilianEnemyProximityText;
        private int aiCivilianEnemyProximityMultiplayer = -1;
        [SyncHostOnly] public int AICivilianEnemyProximityMultiplayer { get => aiCivilianEnemyProximityMultiplayer; set => SetEnemyProximity(ref aiCivilianEnemyProximityMultiplayer, value, -1, 100, nameof(AICivilianEnemyProximityMultiplayer), nameof(AICivilianEnemyProximityMultiplayerValueText)); }
        public string AICivilianEnemyProximityMultiplayerValueText { get => FormatWholeTiles(AICivilianEnemyProximityMultiplayer); set => SetIntValueText(value, parsed => AICivilianEnemyProximityMultiplayer = parsed, nameof(AICivilianEnemyProximityMultiplayerValueText)); }
        public string AICivilianEnemyProximityMultiplayerSearchTitle => EnemyProximityTitleText + " - " + AIEnemyProximityMultiplayerText + " - " + CivilianEnemyProximityText;
        private int aiMilitaryEnemyProximitySingleplayer = -1;
        [SyncHostOnly] public int AIMilitaryEnemyProximitySingleplayer { get => aiMilitaryEnemyProximitySingleplayer; set => SetEnemyProximity(ref aiMilitaryEnemyProximitySingleplayer, value, -1, 100, nameof(AIMilitaryEnemyProximitySingleplayer), nameof(AIMilitaryEnemyProximitySingleplayerValueText)); }
        public string AIMilitaryEnemyProximitySingleplayerValueText { get => FormatWholeTiles(AIMilitaryEnemyProximitySingleplayer); set => SetIntValueText(value, parsed => AIMilitaryEnemyProximitySingleplayer = parsed, nameof(AIMilitaryEnemyProximitySingleplayerValueText)); }
        public string AIMilitaryEnemyProximitySingleplayerSearchTitle => EnemyProximityTitleText + " - " + AIEnemyProximitySingleplayerText + " - " + MilitaryEnemyProximityText;
        private int aiMilitaryEnemyProximityMultiplayer = -1;
        [SyncHostOnly] public int AIMilitaryEnemyProximityMultiplayer { get => aiMilitaryEnemyProximityMultiplayer; set => SetEnemyProximity(ref aiMilitaryEnemyProximityMultiplayer, value, -1, 100, nameof(AIMilitaryEnemyProximityMultiplayer), nameof(AIMilitaryEnemyProximityMultiplayerValueText)); }
        public string AIMilitaryEnemyProximityMultiplayerValueText { get => FormatWholeTiles(AIMilitaryEnemyProximityMultiplayer); set => SetIntValueText(value, parsed => AIMilitaryEnemyProximityMultiplayer = parsed, nameof(AIMilitaryEnemyProximityMultiplayerValueText)); }
        public string AIMilitaryEnemyProximityMultiplayerSearchTitle => EnemyProximityTitleText + " - " + AIEnemyProximityMultiplayerText + " - " + MilitaryEnemyProximityText;

        public string CivilianSingleplayerText => SingleplayerText + " - " + CivilianEnemyProximityText;

        public string MilitarySingleplayerText => SingleplayerText + " - " + MilitaryEnemyProximityText;

        public string CivilianMultiplayerText => MultiplayerText + " - " + CivilianEnemyProximityText;

        public string MilitaryMultiplayerText => MultiplayerText + " - " + MilitaryEnemyProximityText;

        public bool CanConfigureEnemyProximity => CanEditHostSettings && !killRewardMissionLocked;
        private void SetEnemyProximity(ref int field, int value, int minimum, int maximum, string name, string text)
        {
            if (killRewardMissionLocked && !IsApplyingSettingsSnapshot) return;
            SetIntSetting(ref field, value, minimum, maximum, name, text);
        }

        private void ResetEnemyProximityDefaults()
        {
            HumanCivilianEnemyProximitySingleplayer = -1;
            HumanCivilianEnemyProximityMultiplayer = -1;
            HumanMilitaryEnemyProximitySingleplayer = -1;
            HumanMilitaryEnemyProximityMultiplayer = -1;
            AICivilianEnemyProximitySingleplayer = -1;
            AICivilianEnemyProximityMultiplayer = -1;
            AIMilitaryEnemyProximitySingleplayer = -1;
            AIMilitaryEnemyProximityMultiplayer = -1;
        }

        private Dictionary<string, byte[]> startupEnemyProximity;

        // APIShared currently materializes missing reflected settings before its post-load
        // callback. Capture presence before registration; never write or relocate its files.
        // These candidates mirror the inspected PersonalSettingsPaths contract; no game path
        // is chosen when a personal file already exists, even if that file is unreadable.
        internal void CaptureEnemyProximityStartup(string pluginLocation, string guid, string modName)
        {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string documents = new[] { Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                string.IsNullOrWhiteSpace(profile) ? null : Path.Combine(profile, "Documents"),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) }
                .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && Path.IsPathRooted(p));
            if (documents == null) throw new IOException("Cannot resolve personal proximity migration source.");
            string current = Path.Combine(documents, "SHCDE", "APIShared", guid, "settings.msgpack");
            string legacy = Path.Combine(Path.GetDirectoryName(pluginLocation), "LobbyModSettings",
                string.Concat(modName.Split(Path.GetInvalidFileNameChars())) + ".msgpack");
            CaptureEnemyProximityStartupFile(EnemyProximityFileExists(current) ? current : legacy);
        }

        private static bool EnemyProximityFileExists(string path)
        {
            try
            {
                if ((File.GetAttributes(path) & FileAttributes.Directory) != 0) throw new IOException("Settings path is a directory: " + path);
                return true;
            }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
        }

        internal void CaptureEnemyProximityStartupFile(string path)
        {
            startupEnemyProximity = null;
            if (!EnemyProximityFileExists(path)) return;
            var payload = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(File.ReadAllBytes(path));
            if (payload == null) return;
            if (payload.TryGetValue("__SerpPresetSchemaVersion", out byte[] versionBytes))
            {
                int version = MessagePackSerializer.Deserialize<int>(versionBytes);
                if (version < 1 || version > 3) return; // the owner reports unsupported envelopes
                string key = "__SerpCurrentSettings";
                if (version < 3)
                {
                    int selected = payload.TryGetValue("__SerpActivePreset", out byte[] selectedBytes)
                        ? MessagePackSerializer.Deserialize<int>(selectedBytes) : 0;
                    key = selected == 1 ? "__SerpPreset2" : "__SerpPreset1";
                }
                if (!payload.TryGetValue(key, out byte[] nested) || nested == null) return;
                payload = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(nested);
            }
            if (payload != null && HasLegacyEnemyProximity(payload))
                startupEnemyProximity = CanonicalEnemyProximitySnapshot(payload)
                    .Where(p => p.Key.Contains("EnemyProximity") && !p.Key.EndsWith("Text", StringComparison.Ordinal))
                    .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        }

        private bool ApplyStartupEnemyProximity()
        {
            if (startupEnemyProximity == null || IsMissionPresetSelected) return false;
            var patch = startupEnemyProximity;
            startupEnemyProximity = null;
            try { base.System_ApplyWorkingSnapshot(patch); }
            catch { startupEnemyProximity = patch; throw; }
            return true;
        }

        private Dictionary<string, byte[]> EnemyProximityPresetPatch(PublishedModSettingsPreset preset)
        {
            var raw = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var applied = base.System_CreateCurrentWorkingSnapshot();
            foreach (var pair in preset.Settings)
            {
                if (!pair.Key.Contains("EnemyProximity")) continue;
                if (pair.Value.Mode == PublishedPresetValueMode.Fixed && (pair.Value.Value is int || pair.Value.Value is long))
                    raw[pair.Key] = MessagePackSerializer.Serialize(checked(Convert.ToInt32(pair.Value.Value)));
                else if ((pair.Key.Contains("Civilian") || pair.Key.Contains("Military")) && applied.TryGetValue(pair.Key, out byte[] bytes))
                    raw[pair.Key] = bytes; // Player/ModDefault is an explicit category choice too.
            }
            return HasLegacyEnemyProximity(raw) ? CanonicalEnemyProximitySnapshot(raw) : null;
        }

        private static bool HasLegacyEnemyProximity(Dictionary<string, byte[]> snapshot)
        {
            foreach (string role in new[] { "Human", "AI" })
            foreach (string mode in new[] { "Singleplayer", "Multiplayer" })
            {
                string old = role + "EnemyProximity" + mode;
                if (snapshot.TryGetValue(old, out byte[] bytes) && bytes != null &&
                    (!snapshot.ContainsKey(role + "CivilianEnemyProximity" + mode) ||
                     !snapshot.ContainsKey(role + "MilitaryEnemyProximity" + mode))) return true;
            }
            return false;
        }

        // Presence, not value, selects precedence: explicit -1 must beat an old radius.
        internal static Dictionary<string, byte[]> CanonicalEnemyProximitySnapshot(Dictionary<string, byte[]> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var result = new Dictionary<string, byte[]>(source, StringComparer.Ordinal);
            foreach (string role in new[] { "Human", "AI" })
            foreach (string mode in new[] { "Singleplayer", "Multiplayer" })
            {
                string old = role + "EnemyProximity" + mode;
                int radius = -1;
                if (source.TryGetValue(old, out byte[] bytes) && bytes != null)
                    try { radius = Math.Max(-1, Math.Min(100, MessagePackSerializer.Deserialize<int>(bytes))); }
                    catch (MessagePackSerializationException) { }
                foreach (string category in new[] { "Civilian", "Military" })
                {
                    string key = role + category + "EnemyProximity" + mode;
                    if (!result.ContainsKey(key)) result[key] = MessagePackSerializer.Serialize(radius);
                }
                result[old] = MessagePackSerializer.Serialize(-1);
            }
            return result;
        }
    }
}
