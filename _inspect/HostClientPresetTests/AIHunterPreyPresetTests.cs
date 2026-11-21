using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ImprovedHunters;
using APIShared.ModSettings;
using MessagePack;
using SHCDESE.API;
using SHCDESE.API.Components.Network;

internal static class AIHunterPreyPresetTests
{
    // The complete production ImprovedHunters model and APIShared preset controller/storage are compiled into this fixture.
    // Only existing presentation/network boundaries are test doubles; no replacement settings model is used.
    private static readonly string[] Names = {
        nameof(ImprovedHuntersViewModel.EnableMod), nameof(ImprovedHuntersViewModel.ControlAivHunters),
        nameof(ImprovedHuntersViewModel.AutoBuildHunters), nameof(ImprovedHuntersViewModel.RetireEmptyHunters)
    };
    private static readonly Func<ImprovedHuntersViewModel, bool>[] Getters = {
        model => model.EnableMod, model => model.ControlAivHunters,
        model => model.AutoBuildHunters, model => model.RetireEmptyHunters
    };
    private static readonly Action<ImprovedHuntersViewModel, bool>[] Setters = {
        (model, value) => model.EnableMod = value, (model, value) => model.ControlAivHunters = value,
        (model, value) => model.AutoBuildHunters = value, (model, value) => model.RetireEmptyHunters = value
    };
    private static int checks;

    internal static void Run()
    {
        checks = 0;
        bool wasHost = GameNetworkAPI.LocalHost, wasNetworked = GameNetworkAPI.Networked,
            wasMultiplayer = GameNetworkAPI.MultiplayerGame, wasThrowing = GameNetworkAPI.ThrowOnRoleQuery;
        try
        {
            SetRole(true, false);
            var initial = new ImprovedHuntersViewModel();
            for (int index = 0; index < Names.Length; index++)
            {
                var property = typeof(ImprovedHuntersViewModel).GetProperty(Names[index]);
                Check(Getters[index](initial), Names[index] + " production default");
                Check(property.GetCustomAttribute<SyncHostOnlyAttribute>() != null &&
                    property.GetCustomAttribute<SyncPerPlayerAttribute>() == null &&
                    property.GetCustomAttribute<PresetLocalAttribute>() == null,
                    Names[index] + " is exclusively host synchronized");
            }

            string plugin = Path.Combine(Program.PresetFixtureInstallRoot, "ImprovedHunters.dll");
            const string id = "ImprovedHuntersAI";
            string file = new PersonalSettingsPaths(plugin, "Tests." + id).SettingsFile;
            var model = Prepare(plugin, id);
            Check(model.CanEditHostSettings, "singleplayer can edit");
            for (int changed = 0; changed < Names.Length; changed++)
            {
                model.ResetToDefaultCommand.Execute(null);
                Setters[changed](model, false);
                for (int index = 0; index < Names.Length; index++)
                    Check(Getters[index](model) == (index != changed), Names[changed] + " independent of " + Names[index]);
            }
            model.ResetToDefaultCommand.Execute(null);
            Expect(model, new[] { true, true, true, true }, "actual reset restores all defaults");

            bool[] personal = { false, true, false, true };
            Apply(model, personal);
            ExpectStored(file, personal, "host working values atomically persisted");
            model = Prepare(plugin, id);
            Expect(model, personal, "restart restores every separate switch");

            SetRole(false, true);
            model.System_RefreshSettingsAccess();
            Check(!model.CanEditHostSettings, "client cannot edit host switches");
            for (int index = 0; index < Names.Length; index++) Setters[index](model, !personal[index]);
            Expect(model, personal, "client local setters blocked");
            model.ResetToDefaultCommand.Execute(null);
            Expect(model, personal, "client reset cannot bypass host ownership");
            byte[] before = File.ReadAllBytes(file);
            bool[] received = { true, false, true, false };
            GameXAMLManagerAPI.Instance.ApplyNetworkSync(model, () => Apply(model, received));
            Expect(model, received, "authorized incoming host values accepted");
            Check(before.SequenceEqual(File.ReadAllBytes(file)), "remote values do not overwrite personal storage");
            var playerExport = model.System_CreatePlayerMissionPresetSnapshot();
            for (int index = 0; index < Names.Length; index++)
                Check(MessagePackSerializer.Deserialize<bool>(playerExport[Names[index]]) == personal[index],
                    Names[index] + " client export retains personal value");

            SetRole(true, false);
            model.System_RefreshSettingsAccess();
            Apply(model, personal);
            var trail = model.System_CreateCurrentWorkingSnapshot();
            for (int index = 0; index < Names.Length; index++) trail[Names[index]] = MessagePackSerializer.Serialize(received[index]);
            before = File.ReadAllBytes(file);
            model.System_EnterMissionPreset(trail, "AI hunter Trail", false);
            Expect(model, received, "Trail applies its own switch vector");
            Check(!model.CanEditHostSettings, "read-only Trail locks host settings");
            for (int index = 0; index < Names.Length; index++) Setters[index](model, personal[index]);
            model.ResetToDefaultCommand.Execute(null);
            Expect(model, received, "Trail blocks setters and actual reset");
            Check(before.SequenceEqual(File.ReadAllBytes(file)), "Trail never persists temporary values");
            model.System_ExitMissionPreset();
            Expect(model, personal, "Trail exit restores personal switches");
            Check(model.CanEditHostSettings, "Trail exit restores editing");
            Check(before.SequenceEqual(File.ReadAllBytes(file)), "Trail exit preserves personal bytes");
            Expect(Prepare(plugin, id), personal, "restart after Trail preserves personal vector");

            SetRole(true, true);
            model.System_RefreshSettingsAccess();
            Check(model.CanEditHostSettings, "multiplayer host can edit switches");
            model.ResetToDefaultCommand.Execute(null);
            Expect(model, new[] { true, true, true, true }, "host reset restores all defaults");
            ExpectStored(file, new[] { true, true, true, true }, "host reset persists defaults");

            string legacyId = id + "Legacy";
            string legacyFile = new PersonalSettingsPaths(plugin, "Tests." + legacyId).SettingsFile;
            WriteLegacy(legacyFile);
            Expect(Prepare(plugin, legacyId), new[] { true, true, true, true }, "old snapshot missing switches retains defaults");
            Console.WriteLine("PASS: AI hunter actual settings defaults, separate switches, host/client authority, network nonpersistence, reset, restart and Trail (" + checks + " checks).");
        }
        finally
        {
            GameNetworkAPI.LocalHost = wasHost; GameNetworkAPI.Networked = wasNetworked;
            GameNetworkAPI.MultiplayerGame = wasMultiplayer; GameNetworkAPI.ThrowOnRoleQuery = wasThrowing;
        }
    }

    private static void SetRole(bool host, bool networked)
    {
        GameNetworkAPI.ThrowOnRoleQuery = false; GameNetworkAPI.LocalHost = host;
        GameNetworkAPI.Networked = networked; GameNetworkAPI.MultiplayerGame = networked;
    }
    private static ImprovedHuntersViewModel Prepare(string plugin, string id)
    {
        var model = new ImprovedHuntersViewModel(); model.PreparePresets(null, plugin, id); model.ActivatePresets(); return model;
    }
    private static void Apply(ImprovedHuntersViewModel model, bool[] values)
    { for (int index = 0; index < Names.Length; index++) Setters[index](model, values[index]); }
    private static void Expect(ImprovedHuntersViewModel model, bool[] values, string detail)
    { for (int index = 0; index < Names.Length; index++) Check(Getters[index](model) == values[index], detail + ": " + Names[index]); }
    private static void ExpectStored(string file, bool[] values, string detail)
    {
        var payload = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(File.ReadAllBytes(file));
        var working = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(payload["__SerpCurrentSettings"]);
        for (int index = 0; index < Names.Length; index++)
            Check(MessagePackSerializer.Deserialize<bool>(working[Names[index]]) == values[index], detail + ": " + Names[index]);
    }
    private static void WriteLegacy(string file)
    {
        var current = new Dictionary<string, byte[]> { ["RetiredSetting"] = MessagePackSerializer.Serialize(false) };
        var payload = new Dictionary<string, byte[]> {
            ["__SerpPresetSchemaVersion"] = MessagePackSerializer.Serialize(3),
            ["__SerpCurrentSettings"] = MessagePackSerializer.Serialize(current),
            ["__SerpPresetDirty"] = MessagePackSerializer.Serialize(false),
            ["__SerpLegacyPresetImportCompleted"] = MessagePackSerializer.Serialize(true)
        };
        Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllBytes(file, MessagePackSerializer.Serialize(payload));
    }
    private static void Check(bool condition, string detail)
    { checks++; if (!condition) throw new InvalidOperationException("AI hunter preset: " + detail); }
}

namespace ImprovedHunters
{
    // Presentation-only boundary; the complete production model owns settings and reset behavior.
    internal static class SerpLocalization { internal static string Get(string key) => key; }
}
