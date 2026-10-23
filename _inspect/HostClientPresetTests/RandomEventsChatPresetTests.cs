using APIShared.ModSettings;
using MessagePack;
using RandomEvents;
using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

internal static class RandomEventsChatPresetTests
{
    internal static void Run()
    {
        GameNetworkAPI.ThrowOnRoleQuery = false;
        GameNetworkAPI.Networked = true;
        GameNetworkAPI.MultiplayerGame = true;
        GameNetworkAPI.LocalHost = true;
        string plugin = Path.Combine(Program.PresetFixtureInstallRoot, "RandomEvents.dll");
        var host = Prepare(plugin, "RandomEventsChatHost");
        Check(host.ShowEventChatMessages, "new settings must enable chat");
        Check(typeof(RandomEventsSettingsViewModel).GetProperty(nameof(host.ShowEventChatMessages))
            .GetCustomAttribute<PresetLocalAttribute>() != null, "chat must be a local preset property");
        host.ShowEventChatMessages = false;
        var restored = Prepare(plugin, "RandomEventsChatHost");
        Check(!restored.ShowEventChatMessages, "local chat preference did not survive persistence/restart");
        restored.System_LoadModDefaults();
        Check(restored.ShowEventChatMessages, "mod-default reset did not restore chat");

        // An old working snapshot has no chat property: preserve the production default.
        var legacy = Prepare(plugin, "RandomEventsChatLegacy");
        var legacySnapshot = legacy.System_CreateCurrentWorkingSnapshot();
        legacySnapshot.Remove(nameof(legacy.ShowEventChatMessages));
        legacy.System_ApplyWorkingSnapshot(legacySnapshot);
        Check(legacy.ShowEventChatMessages, "missing legacy value disabled chat");

        GameNetworkAPI.LocalHost = false;
        var client = Prepare(plugin, "RandomEventsChatClient");
        Check(client.CanEditClientSettings && !client.CanEditHostSettings, "client role gates are incorrect");
        int interval = client.IntervalMonths;
        client.ShowEventChatMessages = false;
        client.IntervalMonths = interval + 1;
        Check(!client.ShowEventChatMessages && client.IntervalMonths == interval,
            "client cannot edit local chat independently of protected host settings");
        GameXAMLManagerAPI.Instance.ApplyNetworkSync(client, () => client.IncludeAIPlayers = true);
        Check(!client.ShowEventChatMessages && client.IncludeAIPlayers,
            "incoming host settings overwrote the local chat preference");
        client.ResetToDefaultCommand.Execute(null);
        Check(client.ShowEventChatMessages && client.IntervalMonths == interval,
            "client reset failed to restore chat or modified host settings");

        client.ShowEventChatMessages = false;
        client.System_EnterMissionPreset(new Dictionary<string, byte[]>
        {
            [nameof(client.IncludeAIPlayers)] = MessagePackSerializer.Serialize(false)
        }, "Legacy mission", false);
        Check(!client.ShowEventChatMessages, "host-only mission snapshot changed local chat");
        client.System_ExitMissionPreset();
        Check(!client.ShowEventChatMessages, "mission exit lost the personal chat preference");
        Check(!host.ShowEventChatMessages, "another player's reset changed the host preference");

        GameNetworkAPI.LocalHost = true;
        GameNetworkAPI.Networked = false;
        GameNetworkAPI.MultiplayerGame = false;
        Console.WriteLine("PASS: RandomEvents local chat default, persistence, reset, legacy snapshots and host/client independence.");
    }

    private static RandomEventsSettingsViewModel Prepare(string plugin, string id)
    {
        var settings = new RandomEventsSettingsViewModel();
        settings.PreparePresets(null, plugin, id);
        settings.ActivatePresets();
        return settings;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("RandomEvents chat: " + message);
    }
}

namespace RandomEvents
{
    // Translation is a presentation boundary; use the real production settings/controller.
    internal static class SerpLocalization
    {
        internal static string Get(string key) => key;
    }
}
