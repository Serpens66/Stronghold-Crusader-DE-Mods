using APIShared.ModSettings;
using BugfixesAndQoL;
using MessagePack;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

// Uses the production setting partial and shared preset controller. The small partial below
// exposes only the existing player-slot binding boundary of the full main-mod model.
internal static class AutoSendSettingsPresetTests
{
    private const string Property = nameof(BugfixesAndQoLViewModel.EnableAutoSend);
    private static int checks;
    private static void Check(bool value, string reason)
    { checks++; if (!value) throw new InvalidOperationException("AutoSend settings: " + reason); }

    internal static void Run()
    {
        checks = 0;
        bool host = GameNetworkAPI.LocalHost, networked = GameNetworkAPI.Networked,
            multiplayer = GameNetworkAPI.MultiplayerGame, throwing = GameNetworkAPI.ThrowOnRoleQuery;
        try
        {
            GameNetworkAPI.ThrowOnRoleQuery = false;
            GameNetworkAPI.LocalHost = true;
            GameNetworkAPI.Networked = false;
            GameNetworkAPI.MultiplayerGame = false;
            var property = typeof(BugfixesAndQoLViewModel).GetProperty(Property);
            Check(property.GetCustomAttributes(typeof(SyncPerPlayerAttribute), true).Length == 1 &&
                property.GetCustomAttributes(typeof(SyncHostOnlyAttribute), true).Length == 0,
                "actual property belongs to the individual player rather than the host");
            var fresh = new BugfixesAndQoLViewModel();
            Check(fresh.EnableAutoSend && fresh.EnableAutoSendData.Skip(1).All(value => value),
                "actual production initializer enables the personal preference");
            Check(fresh.EnableAutoSendData.Length == 9 &&
                ReferenceEquals(fresh.EnableAutoSendData, fresh.EnableAutoSendData),
                "companion array is stable and covers all player slots");

            const string id = "Tests.AutoSendPreference";
            string plugin = Path.Combine(Program.PresetFixtureInstallRoot, "AutoSendPreference.dll");
            string file = new PersonalSettingsPaths(plugin, id).SettingsFile;
            var settings = Prepare(plugin, id);
            Check(settings.CanConfigureAutoSend, "pre-match personal preference can be edited");
            Check(settings.TestBindAutoSendPlayer(2), "valid local player slot is accepted");
            settings.EnableAutoSend = false;
            Check(!settings.EnableAutoSend && !settings.EnableAutoSendData[2] && settings.EnableAutoSendData[1],
                "changing one's preference updates only the bound sender slot");
            Check(!ReadWorkingBool(file), "explicit false is stored through the real preset backend");
            settings = Prepare(plugin, id);
            Check(!settings.EnableAutoSend, "explicit false survives reconstruction of the settings model");

            var other = Prepare(Path.Combine(Program.PresetFixtureInstallRoot, "AutoSendOtherPlayer.dll"), id + ".Other");
            Check(other.EnableAutoSend, "another player's storage keeps the default independently");
            settings.TestBindAutoSendPlayer(2);
            settings.EnableAutoSendData[1] = true;
            settings.System_TriggerUpdate(nameof(settings.EnableAutoSendData));
            Check(!settings.EnableAutoSend && !ReadWorkingBool(file),
                "a remote sender's companion value does not replace the personal preference or storage");

            GameNetworkAPI.LocalHost = false;
            GameNetworkAPI.Networked = GameNetworkAPI.MultiplayerGame = true;
            settings.System_RefreshSettingsAccess();
            Check(settings.CanConfigureAutoSend, "a multiplayer client retains personal editing rights");
            settings.EnableAutoSend = true;
            Check(settings.EnableAutoSend && settings.EnableAutoSendData[2], "client can enable its own deliveries");
            settings.EnableAutoSend = false;

            int accessNotifications = 0;
            ((INotifyPropertyChanged)settings).PropertyChanged += (_, args) =>
            { if (args.PropertyName == nameof(settings.CanConfigureAutoSend)) accessNotifications++; };
            settings.SetAutoSendMissionLocked(true);
            settings.SetAutoSendMissionLocked(true);
            Check(!settings.CanConfigureAutoSend && accessNotifications == 1,
                "mission lock disables editing and identical lock state does not notify again");
            settings.EnableAutoSend = true;
            Check(!settings.EnableAutoSend && !ReadWorkingBool(file), "active-mission edit cannot change preference or storage");
            settings.System_LoadModDefaults();
            Check(!settings.EnableAutoSend && !ReadWorkingBool(file), "defaults cannot bypass an active-mission lock");
            var requested = settings.System_CreateCurrentWorkingSnapshot();
            requested[Property] = MessagePackSerializer.Serialize(true);
            settings.System_ApplyWorkingSnapshot(requested);
            Check(!settings.EnableAutoSend && !ReadWorkingBool(file),
                "working-snapshot edits cannot bypass the active-mission lock");
            settings.SetAutoSendMissionLocked(false);
            Check(settings.CanConfigureAutoSend && accessNotifications == 2, "mission exit restores editing");
            settings.System_LoadModDefaults();
            Check(settings.EnableAutoSend && ReadWorkingBool(file), "reset restores the actual enabled default after mission exit");
            Check(!other.System_CreateCurrentWorkingSnapshot().ContainsKey("EnableAutoSendData"),
                "network companion slots are not persisted as independent user preferences");

            var oldSnapshot = new Dictionary<string, byte[]>();
            var legacy = Prepare(Path.Combine(Program.PresetFixtureInstallRoot, "AutoSendOldPreset.dll"), id + ".Old");
            legacy.System_ApplyWorkingSnapshot(oldSnapshot);
            Check(legacy.EnableAutoSend, "an older preset without AutoSend keeps its enabled default");
            Console.WriteLine("PASS: " + checks + " AutoSend personal setting, player separation, storage, reset and mission-lock checks.");
        }
        finally
        {
            GameNetworkAPI.LocalHost = host; GameNetworkAPI.Networked = networked;
            GameNetworkAPI.MultiplayerGame = multiplayer; GameNetworkAPI.ThrowOnRoleQuery = throwing;
        }
    }

    private static BugfixesAndQoLViewModel Prepare(string plugin, string id)
    {
        var settings = new BugfixesAndQoLViewModel();
        settings.PreparePresets(null, plugin, id); settings.ActivatePresets(); return settings;
    }

    private static bool ReadWorkingBool(string file)
    {
        var root = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(File.ReadAllBytes(file));
        var values = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(root["__SerpCurrentSettings"]);
        return MessagePackSerializer.Deserialize<bool>(values[Property]);
    }
}

namespace BugfixesAndQoL
{
    public sealed partial class BugfixesAndQoLViewModel
    {
        internal bool TestBindAutoSendPlayer(int player) => enableAutoSend.TrySetLocalPlayerId(player);
    }
}
