using APIShared.ModSettings;
using MessagePack;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class PeaceTimeProximityPresetTests
{
    private const string Property = nameof(BugfixesAndQoL.BugfixesAndQoLViewModel.EnablePeaceTimeCivilianProximityReduction);
    private static int checks;

    // The setting/default/mission guard below come from the actual production partial.
    // Only unrelated ViewModel members and presentation dispatch are boundary doubles.
    internal static void Run()
    {
        checks = 0;
        bool wasHost = GameNetworkAPI.LocalHost, wasNetworked = GameNetworkAPI.Networked,
            wasMultiplayer = GameNetworkAPI.MultiplayerGame, wasThrowing = GameNetworkAPI.ThrowOnRoleQuery;
        try
        {
            GameNetworkAPI.ThrowOnRoleQuery = false;
            GameNetworkAPI.LocalHost = true; GameNetworkAPI.Networked = false; GameNetworkAPI.MultiplayerGame = false;
            var property = typeof(BugfixesAndQoL.BugfixesAndQoLViewModel).GetProperty(Property);
            Check(property.GetCustomAttribute<SyncHostOnlyAttribute>() != null, "actual property is host synchronized");
            Check(new BugfixesAndQoL.BugfixesAndQoLViewModel().EnablePeaceTimeCivilianProximityReduction,
                "actual production initializer enables the feature");

            string plugin = Path.Combine(Program.PresetFixtureInstallRoot, "PeaceTimeProximity.dll");
            const string guid = "PeaceTimeProximity";
            string file = new PersonalSettingsPaths(plugin, "Tests." + guid).SettingsFile;
            var settings = Prepare(plugin, guid);
            Check(settings.EnablePeaceTimeCivilianProximityReduction, "fresh preset retains true");
            settings.EnablePeaceTimeCivilianProximityReduction = false;
            Check(!ReadWorkingBool(file), "explicit false is committed through real atomic storage");
            settings = Prepare(plugin, guid);
            Check(!settings.EnablePeaceTimeCivilianProximityReduction, "explicit false survives restart");
            settings.System_LoadModDefaults();
            Check(settings.EnablePeaceTimeCivilianProximityReduction && ReadWorkingBool(file), "shared reset restores captured production default");

            string legacyGuid = guid + "Legacy";
            string legacyFile = new PersonalSettingsPaths(plugin, "Tests." + legacyGuid).SettingsFile;
            WriteOldSnapshot(legacyFile);
            var legacy = Prepare(plugin, legacyGuid);
            Check(legacy.EnablePeaceTimeCivilianProximityReduction, "old snapshot without property uses true default");

            settings.EnablePeaceTimeCivilianProximityReduction = false;
            settings.SetPeaceTimeCivilianMissionLocked(true);
            Check(!settings.CanConfigurePeaceTimeCivilianProximity, "actual mission guard disables UI editing");
            settings.EnablePeaceTimeCivilianProximityReduction = true;
            Check(!settings.EnablePeaceTimeCivilianProximityReduction, "actual setter rejects a local active-mission edit");
            var requested = settings.System_CreateCurrentWorkingSnapshot();
            requested[Property] = MessagePackSerializer.Serialize(true);
            settings.System_ApplyWorkingSnapshot(requested);
            Check(!settings.EnablePeaceTimeCivilianProximityReduction && !ReadWorkingBool(file),
                "local working snapshot cannot bypass the active-mission lock or change persisted value");
            settings.System_LoadModDefaults();
            Check(!settings.EnablePeaceTimeCivilianProximityReduction && !ReadWorkingBool(file),
                "local reset cannot bypass the active-mission lock or change persisted value");
            string presets = new PersonalSettingsPaths(plugin, "Tests." + guid).Presets;
            Directory.CreateDirectory(presets);
            File.WriteAllText(Path.Combine(presets, "preset_peace.json"), ModSettingsPresetJson.Serialize(
                "Tests." + guid, "peace", "Enable peace cap", "", "", "",
                new Dictionary<string, PublishedPresetSetting> {
                    [Property] = new PublishedPresetSetting { Mode = PublishedPresetValueMode.Fixed, Value = true }
                }));
            settings.System_TestRefreshPresetCatalog();
            settings.System_TestLoadPreset(settings.System_TestPublishedPresets.Single(p => p.Id == "peace").StableId);
            Check(!settings.EnablePeaceTimeCivilianProximityReduction && !ReadWorkingBool(file),
                "local published preset cannot bypass the active-mission lock or change persisted value");
            Check(!Prepare(plugin, guid).EnablePeaceTimeCivilianProximityReduction,
                "blocked local snapshot/reset/preset changes stay blocked after restart");
            settings.System_EnterMissionPreset(requested, "Authoritative save load", false);
            Check(settings.EnablePeaceTimeCivilianProximityReduction && !ReadWorkingBool(file),
                "actual mission entry restores its authoritative value while locked without personal persistence");
            settings.System_ExitMissionPreset();
            Check(!settings.EnablePeaceTimeCivilianProximityReduction && !ReadWorkingBool(file),
                "actual mission exit restores personal value while still locked");
            settings.SetPeaceTimeCivilianMissionLocked(false);
            settings.EnablePeaceTimeCivilianProximityReduction = true;
            settings.SetPeaceTimeCivilianMissionLocked(true);
            settings.EnablePeaceTimeCivilianProximityReduction = false;
            Check(settings.EnablePeaceTimeCivilianProximityReduction, "local edit remains blocked after snapshot completion");

            GameNetworkAPI.LocalHost = false; GameNetworkAPI.Networked = true; GameNetworkAPI.MultiplayerGame = true;
            settings.System_RefreshSettingsAccess();
            settings.EnablePeaceTimeCivilianProximityReduction = false;
            Check(settings.EnablePeaceTimeCivilianProximityReduction, "client local edit cannot change host value");
            byte[] before = File.ReadAllBytes(file);
            GameXAMLManagerAPI.Instance.ApplyNetworkSync(settings, () => settings.EnablePeaceTimeCivilianProximityReduction = false);
            Check(!settings.EnablePeaceTimeCivilianProximityReduction, "authorized host network update accepted despite active-mission lock");
            Check(before.SequenceEqual(File.ReadAllBytes(file)), "remote host value never replaces personal persisted setting");
            Check(MessagePackSerializer.Deserialize<bool>(settings.System_CreatePlayerMissionPresetSnapshot()[Property]),
                "client export retains personal true instead of received host false");

            settings.SetPeaceTimeCivilianMissionLocked(false);
            GameNetworkAPI.LocalHost = true; GameNetworkAPI.Networked = false; GameNetworkAPI.MultiplayerGame = false;
            settings.System_RefreshSettingsAccess();
            settings.EnablePeaceTimeCivilianProximityReduction = true;
            var trail = settings.System_CreateCurrentWorkingSnapshot();
            trail[Property] = MessagePackSerializer.Serialize(false);
            before = File.ReadAllBytes(file);
            settings.System_EnterMissionPreset(trail, "Peace Trail", false);
            settings.SetPeaceTimeCivilianMissionLocked(true);
            Check(!settings.EnablePeaceTimeCivilianProximityReduction && !settings.CanEditHostSettings,
                "read-only Trail supplies its own false setting");
            settings.EnablePeaceTimeCivilianProximityReduction = true;
            Check(!settings.EnablePeaceTimeCivilianProximityReduction, "Trail and mission locks reject local edits");
            settings.System_ExitMissionPreset();
            Check(settings.EnablePeaceTimeCivilianProximityReduction && !settings.CanConfigurePeaceTimeCivilianProximity,
                "Trail restoration works while the native mission edit guard is still locked");
            settings.SetPeaceTimeCivilianMissionLocked(false);
            Check(settings.EnablePeaceTimeCivilianProximityReduction && settings.CanConfigurePeaceTimeCivilianProximity,
                "Trail exit restores personal true and next-game editing");
            Check(before.SequenceEqual(File.ReadAllBytes(file)), "temporary Trail false does not overwrite personal storage");
            Check(Prepare(plugin, guid).EnablePeaceTimeCivilianProximityReduction, "personal true survives Trail and restart");
            Console.WriteLine("PASS: actual peace-time property, default/reset, old/missing and explicit values, mission/host/Trail guards and client persistence (" + checks + " checks).");
        }
        finally
        {
            GameNetworkAPI.LocalHost = wasHost; GameNetworkAPI.Networked = wasNetworked;
            GameNetworkAPI.MultiplayerGame = wasMultiplayer; GameNetworkAPI.ThrowOnRoleQuery = wasThrowing;
        }
    }

    private static BugfixesAndQoL.BugfixesAndQoLViewModel Prepare(string plugin, string guid)
    {
        var settings = new BugfixesAndQoL.BugfixesAndQoLViewModel();
        settings.PreparePresets(null, plugin, guid); settings.ActivatePresets(); return settings;
    }
    private static void WriteOldSnapshot(string file)
    {
        var current = new Dictionary<string, byte[]> { ["UnrelatedOldSetting"] = MessagePackSerializer.Serialize(false) };
        var root = new Dictionary<string, byte[]> {
            ["__SerpPresetSchemaVersion"] = MessagePackSerializer.Serialize(3),
            ["__SerpCurrentSettings"] = MessagePackSerializer.Serialize(current),
            ["__SerpPresetDirty"] = MessagePackSerializer.Serialize(false),
            ["__SerpLegacyPresetImportCompleted"] = MessagePackSerializer.Serialize(true)
        };
        Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllBytes(file, MessagePackSerializer.Serialize(root));
    }
    private static bool ReadWorkingBool(string file)
    {
        var root = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(File.ReadAllBytes(file));
        var current = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(root["__SerpCurrentSettings"]);
        return MessagePackSerializer.Deserialize<bool>(current[Property]);
    }
    private static void Check(bool condition, string detail)
    { checks++; if (!condition) throw new InvalidOperationException("Peace proximity preset: " + detail); }
}

namespace BugfixesAndQoL
{
    public sealed partial class BugfixesAndQoLViewModel : PresetLobbyModSettingsViewModel
    {
        // The real central setter's authority/event boundary, without unrelated BFQoL settings.
        public event Action<string> SettingChanged;
        private void SetSetting<T>(ref T field, T value, string propertyName)
        {
            if (!CanMutateSetting(propertyName) || Equals(field, value)) return;
            field = value; SettingChanged?.Invoke(propertyName); OnPropertyChanged(propertyName);
        }
    }
    internal static class SerpLocalization { internal static string Get(string key) => key; }
}

namespace Shared
{
    // UI dispatch is outside this storage fixture; no Noesis object is constructed.
    internal static class UnityMainThreadDispatch
    { internal static bool TryRunInlineOrEnqueue(Action action) { action(); return true; } }
}
