using APIShared.ModSettings;
using BugfixesAndQoL;
using MessagePack;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal static class CrenelOwnerPresetTests
{
    private const string Human = nameof(BugfixesAndQoLViewModel.EnableCrenelCheckerboard);
    private const string AI = nameof(BugfixesAndQoLViewModel.EnableCrenelCheckerboardAI);

    internal static void Run()
    {
        bool wasHost = GameNetworkAPI.LocalHost, wasNetworked = GameNetworkAPI.Networked,
            wasMultiplayer = GameNetworkAPI.MultiplayerGame, wasThrowing = GameNetworkAPI.ThrowOnRoleQuery;
        try
        {
            GameNetworkAPI.ThrowOnRoleQuery = false;
            GameNetworkAPI.LocalHost = true;
            GameNetworkAPI.Networked = false;
            GameNetworkAPI.MultiplayerGame = false;

            const string modName = "CrenelOwners";
            const string guid = "Tests." + modName;
            string plugin = Path.Combine(Program.PresetFixtureInstallRoot, "CrenelOwners.dll");
            var settings = Open(plugin, modName);
            Check(HasValues(settings, human: true, ai: false), "fresh preset enables Human only");
            int ownerChanges = 0;
            settings.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(BugfixesAndQoLViewModel.HasCrenelCheckerboardOwners))
                    ownerChanges++;
            };
            settings.EnableCrenelCheckerboard = false;
            Check(HasValues(settings, human: false, ai: false) && ownerChanges == 1,
                "disabling the last group disables the align controls");
            settings.EnableCrenelCheckerboardAI = true;
            Check(HasValues(settings, human: false, ai: true) && ownerChanges == 2,
                "AI can be enabled independently and re-enables the align controls");
            settings = Open(plugin, modName);
            Check(HasValues(settings, human: false, ai: true), "both choices persist independently");
            settings.System_LoadModDefaults();
            Check(HasValues(settings, human: true, ai: false), "reset restores the Human-only defaults");

            const string legacyName = modName + ".Legacy";
            string legacyFile = new PersonalSettingsPaths(plugin, "Tests." + legacyName).SettingsFile;
            var root = new Dictionary<string, byte[]>
            {
                ["__SerpPresetSchemaVersion"] = MessagePackSerializer.Serialize(3),
                ["__SerpCurrentSettings"] = MessagePackSerializer.Serialize(new Dictionary<string, byte[]>
                {
                    ["EnableCrenelCheckerboard"] = MessagePackSerializer.Serialize(false)
                }),
                ["__SerpLegacyPresetImportCompleted"] = MessagePackSerializer.Serialize(true)
            };
            Directory.CreateDirectory(Path.GetDirectoryName(legacyFile));
            File.WriteAllBytes(legacyFile, MessagePackSerializer.Serialize(root));
            Check(HasValues(Open(plugin, legacyName), human: false, ai: false),
                "old preset preserves the explicit off value and defaults AI to off");

            foreach (string name in new[] { Human, AI })
                Check(Attribute.IsDefined(typeof(BugfixesAndQoLViewModel).GetProperty(name), typeof(SyncHostOnlyAttribute)),
                    name + " participates in host synchronization");
            string personalFile = new PersonalSettingsPaths(plugin, guid).SettingsFile;
            byte[] personal = File.ReadAllBytes(personalFile);
            GameNetworkAPI.LocalHost = false;
            GameNetworkAPI.Networked = true;
            GameNetworkAPI.MultiplayerGame = true;
            settings.System_RefreshSettingsAccess();
            settings.EnableCrenelCheckerboard = false;
            settings.EnableCrenelCheckerboardAI = true;
            Check(HasValues(settings, human: true, ai: false), "client local edits are rejected");
            GameXAMLManagerAPI.Instance.ApplyNetworkSync(settings, () =>
            {
                settings.EnableCrenelCheckerboard = false;
                settings.EnableCrenelCheckerboardAI = true;
            });
            Check(HasValues(settings, human: false, ai: true) && personal.SequenceEqual(File.ReadAllBytes(personalFile)),
                "host changes reach the client without replacing its personal preset");
            Console.WriteLine("PASS: crenel owner defaults, legacy preset, independent persistence, align bindings and host sync.");
        }
        finally
        {
            GameNetworkAPI.LocalHost = wasHost;
            GameNetworkAPI.Networked = wasNetworked;
            GameNetworkAPI.MultiplayerGame = wasMultiplayer;
            GameNetworkAPI.ThrowOnRoleQuery = wasThrowing;
        }
    }

    private static BugfixesAndQoLViewModel Open(string plugin, string modName)
    {
        var settings = new BugfixesAndQoLViewModel();
        settings.PreparePresets(null, plugin, modName);
        settings.ActivatePresets();
        return settings;
    }

    private static bool HasValues(BugfixesAndQoLViewModel settings, bool human, bool ai) =>
        settings.EnableCrenelCheckerboard == human && settings.EnableCrenelCheckerboardAI == ai &&
        settings.HasCrenelCheckerboardOwners == (human || ai);

    private static void Check(bool condition, string detail)
    {
        if (!condition) throw new InvalidOperationException("Crenel owner preset: " + detail);
    }
}
