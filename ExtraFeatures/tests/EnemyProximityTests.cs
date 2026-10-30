using SHCDESE.API.Components.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using APIShared.ModSettings;
using ExtraFeatures;
using MessagePack;
using SHCDESE.API;

internal static class EnemyProximityTests
{
    internal static void Run(Action<bool, string> check)
    {
        string[] oldKeys = { "HumanEnemyProximitySingleplayer", "HumanEnemyProximityMultiplayer", "AIEnemyProximitySingleplayer", "AIEnemyProximityMultiplayer" };
        var keys = (from role in new[] { "Human", "AI" }
                    from category in new[] { "Civilian", "Military" }
                    from mode in new[] { "Singleplayer", "Multiplayer" }
                    select role + category + "EnemyProximity" + mode).ToArray();
        var old = oldKeys.Select((name, index) => new { name, value = index * 13 }).ToDictionary(x => x.name, x => MessagePackSerializer.Serialize(x.value));
        old["HumanCivilianEnemyProximitySingleplayer"] = MessagePackSerializer.Serialize(-1);
        var canonical = ExtraFeaturesViewModel.CanonicalEnemyProximitySnapshot(old);
        check(MessagePackSerializer.Deserialize<int>(old[oldKeys[1]]) == 13, "Migration does not mutate caller-owned data");
        check(MessagePackSerializer.Deserialize<int>(canonical["HumanCivilianEnemyProximitySingleplayer"]) == -1, "Explicit Vanilla has precedence");
        foreach (string key in keys.Where(k => k != "HumanCivilianEnemyProximitySingleplayer"))
        {
            string legacy = key.Replace("Civilian", "").Replace("Military", "");
            check(MessagePackSerializer.Deserialize<int>(canonical[key]) == MessagePackSerializer.Deserialize<int>(old[legacy]), "Each missing category migrates independently: " + key);
        }
        var twice = ExtraFeaturesViewModel.CanonicalEnemyProximitySnapshot(canonical);
        check(canonical.All(p => twice[p.Key].SequenceEqual(p.Value)), "Migration is idempotent");

        string fixture = TestWorkDirectory.New("EnemyProximityPreset"); Directory.CreateDirectory(fixture);
        var previous = PersonalSettingsPaths.TestDocumentsDirectory;
        PersonalSettingsPaths.TestDocumentsDirectory = _ => fixture;
        bool wasHost = GameNetworkAPI.LocalHost, wasNetworked = GameNetworkAPI.Networked, wasMultiplayer = GameNetworkAPI.MultiplayerGame;
        GameNetworkAPI.LocalHost = true; GameNetworkAPI.Networked = false; GameNetworkAPI.MultiplayerGame = false;
        try
        {
            string plugin = Path.Combine(fixture, "ExtraFeatures.dll");
            Func<ExtraFeaturesViewModel> prepare = () => {
                var model = new ExtraFeaturesViewModel(false);
                model.CaptureEnemyProximityStartupFile(new PersonalSettingsPaths(plugin, "Tests.EnemyProximity").SettingsFile);
                model.PreparePresets(null, plugin, "EnemyProximity");
                model.ActivatePresets(); model.CompleteKillRewardRegistration(); return model;
            };
            var settings = prepare();
            foreach (string key in keys)
            {
                PropertyInfo p = typeof(ExtraFeaturesViewModel).GetProperty(key);
                check(p.GetCustomAttribute<SyncHostOnlyAttribute>() != null && (int)p.GetValue(settings) == -1, "Host-only Vanilla default: " + key);
                p.SetValue(settings, -2); check((int)p.GetValue(settings) == -1, "Lower bound: " + key);
                p.SetValue(settings, 101); check((int)p.GetValue(settings) == 100, "Upper bound: " + key);
                p.SetValue(settings, 0); check((int)p.GetValue(prepare()) == 0, "Zero persists: " + key);
            }
            settings.System_LoadModDefaults();
            foreach (string key in keys) check((int)typeof(ExtraFeaturesViewModel).GetProperty(key).GetValue(settings) == -1, "Reset restores Vanilla: " + key);
            // A real pre-upgrade disk payload has only the four common radii.
            string storage = Directory.GetFiles(fixture, "settings.msgpack", SearchOption.AllDirectories).Single();
            File.WriteAllBytes(storage, MessagePackSerializer.Serialize(old));
            settings = prepare();
            check(settings.HumanCivilianEnemyProximitySingleplayer == -1 && settings.HumanMilitaryEnemyProximitySingleplayer == 0 &&
                settings.HumanCivilianEnemyProximityMultiplayer == 13 && settings.HumanMilitaryEnemyProximityMultiplayer == 13 &&
                settings.AICivilianEnemyProximitySingleplayer == 26 && settings.AIMilitaryEnemyProximityMultiplayer == 39, "Old disk profile migrates on registration");
            settings.HumanCivilianEnemyProximityMultiplayer = 2;
            check(prepare().HumanCivilianEnemyProximityMultiplayer == 2 && prepare().HumanMilitaryEnemyProximityMultiplayer == 13, "Independent edit survives restart");
            var owned = ((IModSettingsWorkingCopyEndpoint)settings).System_CreatePlayerMissionPresetSnapshot();
            check(keys.All(owned.ContainsKey) && oldKeys.All(k => MessagePackSerializer.Deserialize<int>(owned[k]) == -1), "Canonical export retires common values");
            IModSettingsWorkingCopyEndpoint endpoint = settings;
            endpoint.System_EnterMissionPreset(old, "Legacy Trail", false);
            check(settings.AIMilitaryEnemyProximitySingleplayer == 26 && settings.HumanCivilianEnemyProximitySingleplayer == -1, "Read-only Trail migration preserves explicit new Vanilla");
            settings.AIMilitaryEnemyProximitySingleplayer = 70;
            check(settings.AIMilitaryEnemyProximitySingleplayer == 26, "Read-only Trail blocks edits");
            endpoint.System_ExitMissionPreset();
            check(settings.HumanCivilianEnemyProximityMultiplayer == 2, "Leaving Trail restores own values");
            settings.SetKillRewardMissionLocked(true); settings.HumanCivilianEnemyProximityMultiplayer = 80;
            check(settings.HumanCivilianEnemyProximityMultiplayer == 2, "Active mission blocks distance changes");
            settings.SetKillRewardMissionLocked(false);
            byte[] before = File.ReadAllBytes(storage);
            GameNetworkAPI.LocalHost = false; GameNetworkAPI.Networked = true; GameNetworkAPI.MultiplayerGame = true;
            settings.HumanCivilianEnemyProximityMultiplayer = 80;
            check(settings.HumanCivilianEnemyProximityMultiplayer == 2, "Client cannot change host distance");
            GameXAMLManagerAPI.Instance.ApplyNetworkSync(settings, () => { settings.HumanCivilianEnemyProximityMultiplayer = 4; settings.HumanMilitaryEnemyProximityMultiplayer = 15; });
            check(settings.HumanCivilianEnemyProximityMultiplayer == 4 && settings.HumanMilitaryEnemyProximityMultiplayer == 15, "Host categories synchronize independently");
            check(File.ReadAllBytes(storage).SequenceEqual(before), "Remote values never enter local storage");
            var exported = endpoint.System_CreatePlayerMissionPresetSnapshot();
            check(MessagePackSerializer.Deserialize<int>(exported["HumanCivilianEnemyProximityMultiplayer"]) == 2, "Client export retains owned radius");
            GameNetworkAPI.LocalHost = true; GameNetworkAPI.Networked = false; GameNetworkAPI.MultiplayerGame = false;
            settings.System_LoadModDefaults();
            settings.HumanEnemyProximitySingleplayer = 27;
            settings.System_SavePersonalPreset("old-proximity", "Old proximity", "", new[] {
                new PresetSaveSelection { PropertyName = "HumanEnemyProximitySingleplayer", Mode = PublishedPresetValueMode.Fixed },
                new PresetSaveSelection { PropertyName = "HumanCivilianEnemyProximitySingleplayer", Mode = PublishedPresetValueMode.Fixed }
            }, false);
            var named = settings.System_TestPublishedPresets.Single(p => p.Id == "old-proximity");
            settings.System_LoadModDefaults(); settings.System_TestLoadPreset(named.StableId);
            check(settings.HumanCivilianEnemyProximitySingleplayer == -1 && settings.HumanMilitaryEnemyProximitySingleplayer == 27,
                "Named legacy JSON preset preserves explicit Vanilla and migrates only missing category");
            check(prepare().HumanMilitaryEnemyProximitySingleplayer == 27, "Named migration persists before a later edit");
            settings.HumanCivilianEnemyProximitySingleplayer = 3;
            settings.HumanEnemyProximitySingleplayer = 27;
            settings.System_SavePersonalPreset("old-player-choice", "Old player choice", "", new[] {
                new PresetSaveSelection { PropertyName = "HumanEnemyProximitySingleplayer", Mode = PublishedPresetValueMode.Fixed },
                new PresetSaveSelection { PropertyName = "HumanCivilianEnemyProximitySingleplayer", Mode = PublishedPresetValueMode.Player }
            }, false);
            var playerChoice = settings.System_TestPublishedPresets.Single(p => p.Id == "old-player-choice");
            settings.System_TestLoadPreset(playerChoice.StableId);
            check(settings.HumanCivilianEnemyProximitySingleplayer == 3 && settings.HumanMilitaryEnemyProximitySingleplayer == 27,
                "Explicit player-owned category beats fixed common legacy radius");
            // Current owned snapshot wins over stale top-level values in the envelope.
            var envelope = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(File.ReadAllBytes(storage));
            var current = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(envelope["__SerpCurrentSettings"]);
            foreach (string key in keys) current.Remove(key);
            current["HumanEnemyProximityMultiplayer"] = MessagePackSerializer.Serialize(91);
            current["HumanCivilianEnemyProximityMultiplayer"] = MessagePackSerializer.Serialize(-1);
            envelope["__SerpCurrentSettings"] = MessagePackSerializer.Serialize(current);
            File.WriteAllBytes(storage, MessagePackSerializer.Serialize(envelope));
            settings = prepare();
            check(settings.HumanCivilianEnemyProximityMultiplayer == -1 && settings.HumanMilitaryEnemyProximityMultiplayer == 91,
                "Early migration uses authoritative nested current snapshot and explicit new keys");

        }
        finally
        {
            GameNetworkAPI.LocalHost = wasHost; GameNetworkAPI.Networked = wasNetworked; GameNetworkAPI.MultiplayerGame = wasMultiplayer;
            PersonalSettingsPaths.TestDocumentsDirectory = previous;
        }
    }
}
