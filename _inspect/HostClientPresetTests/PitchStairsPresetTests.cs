using APIShared.ModSettings;
using MessagePack;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class PitchStairsPresetTests
{
    private const string Property = nameof(BugfixesAndQoL.BugfixesAndQoLViewModel.EnablePitchStairsFix);
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
            Check(new BugfixesAndQoL.BugfixesAndQoLViewModel().EnablePitchStairsFix,
                "actual production initializer enables the feature");

            string plugin = Path.Combine(Program.PresetFixtureInstallRoot, "PitchStairs.dll");
            const string guid = "PitchStairs";
            string file = new PersonalSettingsPaths(plugin, "Tests." + guid).SettingsFile;
            var settings = Prepare(plugin, guid);
            Check(settings.EnablePitchStairsFix, "fresh preset retains true");
            settings.EnablePitchStairsFix = false;
            Check(!ReadWorkingBool(file), "explicit false is committed through real atomic storage");
            settings = Prepare(plugin, guid);
            Check(!settings.EnablePitchStairsFix, "explicit false survives restart");
            settings.System_LoadModDefaults();
            Check(settings.EnablePitchStairsFix && ReadWorkingBool(file), "shared reset restores captured production default");

            string legacyGuid = guid + "Legacy";
            string legacyFile = new PersonalSettingsPaths(plugin, "Tests." + legacyGuid).SettingsFile;
            WriteOldSnapshot(legacyFile);
            var legacy = Prepare(plugin, legacyGuid);
            Check(legacy.EnablePitchStairsFix, "old snapshot without property uses true default");

            settings.EnablePitchStairsFix = false;
            settings.SetPitchStairsMissionLocked(true);
            Check(!settings.CanConfigurePitchStairs, "actual mission guard disables UI editing");
            settings.EnablePitchStairsFix = true;
            Check(!settings.EnablePitchStairsFix, "actual setter rejects a local active-mission edit");
            var requested = settings.System_CreateCurrentWorkingSnapshot();
            requested[Property] = MessagePackSerializer.Serialize(true);
            settings.System_ApplyWorkingSnapshot(requested);
            Check(!settings.EnablePitchStairsFix && !ReadWorkingBool(file),
                "local working snapshot cannot bypass the active-mission lock or change persisted value");
            settings.System_LoadModDefaults();
            Check(!settings.EnablePitchStairsFix && !ReadWorkingBool(file),
                "local reset cannot bypass the active-mission lock or change persisted value");
            string presets = new PersonalSettingsPaths(plugin, "Tests." + guid).Presets;
            Directory.CreateDirectory(presets);
            File.WriteAllText(Path.Combine(presets, "preset_pitch.json"), ModSettingsPresetJson.Serialize(
                "Tests." + guid, "pitch", "Enable pitch stairs", "", "", "",
                new Dictionary<string, PublishedPresetSetting> {
                    [Property] = new PublishedPresetSetting { Mode = PublishedPresetValueMode.Fixed, Value = true }
                }));
            settings.System_TestRefreshPresetCatalog();
            settings.System_TestLoadPreset(settings.System_TestPublishedPresets.Single(p => p.Id == "pitch").StableId);
            Check(!settings.EnablePitchStairsFix && !ReadWorkingBool(file),
                "local published preset cannot bypass the active-mission lock or change persisted value");
            Check(!Prepare(plugin, guid).EnablePitchStairsFix,
                "blocked local snapshot/reset/preset changes stay blocked after restart");
            settings.System_EnterMissionPreset(requested, "Authoritative save load", false);
            Check(settings.EnablePitchStairsFix && !ReadWorkingBool(file),
                "actual mission entry restores its authoritative value while locked without personal persistence");
            settings.System_ExitMissionPreset();
            Check(!settings.EnablePitchStairsFix && !ReadWorkingBool(file),
                "actual mission exit restores personal value while still locked");
            settings.SetPitchStairsMissionLocked(false);
            settings.EnablePitchStairsFix = true;
            settings.SetPitchStairsMissionLocked(true);
            settings.EnablePitchStairsFix = false;
            Check(settings.EnablePitchStairsFix, "local edit remains blocked after snapshot completion");

            GameNetworkAPI.LocalHost = false; GameNetworkAPI.Networked = true; GameNetworkAPI.MultiplayerGame = true;
            settings.System_RefreshSettingsAccess();
            settings.EnablePitchStairsFix = false;
            Check(settings.EnablePitchStairsFix, "client local edit cannot change host value");
            byte[] before = File.ReadAllBytes(file);
            GameXAMLManagerAPI.Instance.ApplyNetworkSync(settings, () => settings.EnablePitchStairsFix = false);
            Check(!settings.EnablePitchStairsFix, "authorized host network update accepted despite active-mission lock");
            Check(before.SequenceEqual(File.ReadAllBytes(file)), "remote host value never replaces personal persisted setting");
            Check(MessagePackSerializer.Deserialize<bool>(settings.System_CreatePlayerMissionPresetSnapshot()[Property]),
                "client export retains personal true instead of received host false");

            settings.SetPitchStairsMissionLocked(false);
            GameNetworkAPI.LocalHost = true; GameNetworkAPI.Networked = false; GameNetworkAPI.MultiplayerGame = false;
            settings.System_RefreshSettingsAccess();
            settings.EnablePitchStairsFix = true;
            var trail = settings.System_CreateCurrentWorkingSnapshot();
            trail[Property] = MessagePackSerializer.Serialize(false);
            before = File.ReadAllBytes(file);
            settings.System_EnterMissionPreset(trail, "Pitch Trail", false);
            settings.SetPitchStairsMissionLocked(true);
            Check(!settings.EnablePitchStairsFix && !settings.CanEditHostSettings,
                "read-only Trail supplies its own false setting");
            settings.EnablePitchStairsFix = true;
            Check(!settings.EnablePitchStairsFix, "Trail and mission locks reject local edits");
            settings.System_ExitMissionPreset();
            Check(settings.EnablePitchStairsFix && !settings.CanConfigurePitchStairs,
                "Trail restoration works while the native mission edit guard is still locked");
            settings.SetPitchStairsMissionLocked(false);
            Check(settings.EnablePitchStairsFix && settings.CanConfigurePitchStairs,
                "Trail exit restores personal true and next-game editing");
            Check(before.SequenceEqual(File.ReadAllBytes(file)), "temporary Trail false does not overwrite personal storage");
            Check(Prepare(plugin, guid).EnablePitchStairsFix, "personal true survives Trail and restart");
            Console.WriteLine("PASS: actual pitch-stairs property, default/reset, old/missing and explicit values, mission/host/Trail guards and client persistence (" + checks + " checks).");
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
    { checks++; if (!condition) throw new InvalidOperationException("Pitch-stairs preset: " + detail); }
}

