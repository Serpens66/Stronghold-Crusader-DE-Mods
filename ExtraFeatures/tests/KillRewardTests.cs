using APIShared.ModSettings;
using ExtraFeatures;
using MessagePack;
using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ComponentModel;
using System.Reflection;
using SHCDESE.API.Components.Network;

internal static class KillRewardTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (int total in new[] { 0, 3, 103, 1999, 2000, 10000, 400000, int.MaxValue })
        foreach (int percent in new[] { 0, 1, 25, 100 })
        {
            check(NoKillRewardPolicy.Goods(total, percent) == (int)((long)total * percent / 100), "Goods exact whole percentage without overflow");
            check(NoKillRewardPolicy.Gold(total, percent, int.MaxValue) == (int)((long)total * percent / 100), "Custom gold has no minimum");
        }
        check(NoKillRewardPolicy.Gold(0, -1, -1) == 500 && NoKillRewardPolicy.Gold(1000000, -1, -1) == 100000, "Vanilla minimum and maximum");
        check(NoKillRewardPolicy.Gold(1000000, -1, 500000) == 250000 && NoKillRewardPolicy.Gold(0, -1, 10) == 10, "Independent cap overrides both Vanilla bounds");
        check(NoKillRewardPolicy.Gold(1000, 0, 100) == 0 && NoKillRewardPolicy.Gold(999, 1, -1) == 9, "Zero gold and rounding");
        check(NoKillRewardPolicy.LimitCredit(100, int.MaxValue - 7) == 7 && NoKillRewardPolicy.LimitCredit(int.MaxValue, -500) == int.MaxValue, "Credit uses signed 64-bit headroom");
        var roles = new KillRewardConfiguration(-1, -1, -1, 0, 100, 10);
        check(roles.IsVanilla(false) && !roles.IsVanilla(true), "Winner roles independently preserve Vanilla");

        string fixture = TestWorkDirectory.New("KillRewardPreset");
        var previousDocuments = PersonalSettingsPaths.TestDocumentsDirectory;
        PersonalSettingsPaths.TestDocumentsDirectory = _ => fixture;
        try
        {
            string plugin = Path.Combine(fixture, "ExtraFeatures.dll");
            Func<ExtraFeaturesViewModel> prepare = () => {
                var result = new ExtraFeaturesViewModel(false);
                result.PreparePresets(null, plugin, "KillReward"); result.ActivatePresets();
                result.CompleteKillRewardRegistration(); return result;
            };
            var settings = prepare();
            check(settings.KillRewardGoldHuman == -1 && settings.KillRewardGoldAI == -1 &&
                settings.KillRewardGoodsHuman == -1 && settings.KillRewardGoodsAI == -1 &&
                settings.KillRewardGoldMaximumHuman == -1 && settings.KillRewardGoldMaximumAI == -1, "Six Vanilla defaults");
            for (int percent = -1; percent <= 100; percent++)
            {
                settings.KillRewardGoldHuman = percent; settings.KillRewardGoodsAI = percent;
                check(settings.KillRewardGoldHuman == percent && settings.KillRewardGoodsAI == percent, "Every slider step is representable");
            }
            settings.KillRewardGoldMaximumHumanText = "123456";
            foreach (string invalid in new[] { "0", "-2", "1.5", "", "gold", "2147483648" })
            {
                settings.KillRewardGoldMaximumHumanText = invalid;
                check(settings.KillRewardGoldMaximumHuman == 123456, "Invalid cap rejected: " + invalid);
            }
            settings.KillRewardGoldMaximumAI = int.MaxValue;
            check(prepare().KillRewardGoldMaximumAI == int.MaxValue, "Int32 cap persists");
            // Observe the production notification boundary, including snapshot publication.
            // A whole-model application used to emit every unrelated host property here.
            var hostChanges = new List<string>(); var runtimeChanges = new List<string>();
            var textChanges = new List<string>();
            GameNetworkAPI.Networked = true;
            GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(null, "Reward regression", settings, "", false);
            PropertyChangedEventHandler changed = (sender, args) => {
                if (!args.PropertyName.StartsWith("System_", StringComparison.Ordinal) && args.PropertyName.EndsWith("Text", StringComparison.Ordinal)) textChanges.Add(args.PropertyName);
                if (settings.GetType().GetProperty(args.PropertyName)?.GetCustomAttribute<SyncHostOnlyAttribute>() != null)
                    hostChanges.Add(args.PropertyName);
            };
            ((INotifyPropertyChanged)settings).PropertyChanged += changed;
            settings.SettingChanged += runtimeChanges.Add;
            foreach (string name in new[] { "KillRewardGoldHuman", "KillRewardGoldAI", "KillRewardGoodsHuman", "KillRewardGoodsAI", "KillRewardGoldMaximumHuman", "KillRewardGoldMaximumAI", "CampfirePeasantsLimit" })
            {
                hostChanges.Clear(); runtimeChanges.Clear(); textChanges.Clear();
                GameXAMLManagerAPI.Instance.ResetRoutingCounts();
                PropertyInfo property = settings.GetType().GetProperty(name);
                int value = (int)property.GetValue(settings) == 47 ? 48 : 47;
                property.SetValue(settings, value);
                check(hostChanges.SequenceEqual(new[] { name }) && runtimeChanges.SequenceEqual(new[] { name }), "Single-value slider path: " + name);
                check(textChanges.SequenceEqual(new[] { name + "Text" }) && GameXAMLManagerAPI.Instance.BroadcastCount == 1,
                    "One display and one host publication: " + name + " (" + string.Join(",", textChanges) + "; broadcasts=" + GameXAMLManagerAPI.Instance.BroadcastCount + ")");
                hostChanges.Clear(); runtimeChanges.Clear(); textChanges.Clear();
                GameXAMLManagerAPI.Instance.ResetRoutingCounts(); property.SetValue(settings, value);
                check(hostChanges.Count == 0 && runtimeChanges.Count == 0 && textChanges.Count == 0 &&
                    GameXAMLManagerAPI.Instance.BroadcastCount == 0, "Unchanged slider does no work: " + name);
            }
            ((INotifyPropertyChanged)settings).PropertyChanged -= changed;
            settings.SettingChanged -= runtimeChanges.Add;
            GameNetworkAPI.Networked = false;
            GameXAMLManagerAPI.Instance.ResetRoutingProbe();
            settings.System_LoadModDefaults();
            // Actual legacy file, without any new properties: local-load migration must not
            // depend on property enumeration order or the presence of an active game.
            string storage = Directory.GetFiles(fixture, "settings.msgpack", SearchOption.AllDirectories).Single();
            var legacy = new Dictionary<string, byte[]> {
                ["NoKillRewardHuman"] = MessagePackSerializer.Serialize(true),
                ["NoKillRewardAI"] = MessagePackSerializer.Serialize(false)
            };
            File.WriteAllBytes(storage, MessagePackSerializer.Serialize(legacy));
            settings = prepare();
            check(settings.KillRewardGoldHuman == 0 && settings.KillRewardGoodsHuman == 0 &&
                settings.KillRewardGoldAI == -1 && settings.KillRewardGoodsAI == -1, "Legacy Human-only profile migrates");
            var migrated = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(File.ReadAllBytes(storage));
            check(!MessagePackSerializer.Deserialize<bool>(migrated["NoKillRewardHuman"]) &&
                MessagePackSerializer.Deserialize<int>(migrated["KillRewardGoodsHuman"]) == 0, "Personal migration is persisted before the first slider edit");
            IModSettingsWorkingCopyEndpoint endpoint = settings;
            var exported = endpoint.System_CreatePlayerMissionPresetSnapshot();
            check(!MessagePackSerializer.Deserialize<bool>(exported["NoKillRewardHuman"]) &&
                MessagePackSerializer.Deserialize<int>(exported["KillRewardGoldHuman"]) == 0 &&
                MessagePackSerializer.Deserialize<int>(exported["KillRewardGoodsHuman"]) == 0, "Untouched legacy owned profile exports canonical values through interface");
            settings.KillRewardGoldHuman = 37;
            var restored = prepare();
            check(restored.KillRewardGoldHuman == 37 && restored.KillRewardGoodsHuman == 0, "Editing one migrated value preserves other zero after restart");

            // Named presets use the production reflection/capture path.
            restored.System_SavePersonalPreset("reward", "Reward", "", new[] {
                new PresetSaveSelection { PropertyName = "KillRewardGoldHuman", Mode = PublishedPresetValueMode.Fixed },
                new PresetSaveSelection { PropertyName = "KillRewardGoodsHuman", Mode = PublishedPresetValueMode.Fixed },
                new PresetSaveSelection { PropertyName = "NoKillRewardHuman", Mode = PublishedPresetValueMode.Fixed }
            }, false);
            var preset = restored.System_TestPublishedPresets.Single(p => p.Id == "reward");
            restored.System_LoadModDefaults(); restored.System_TestLoadPreset(preset.StableId);
            check(restored.KillRewardGoldHuman == 37 && restored.KillRewardGoodsHuman == 0, "Named preset cannot revive old suppression");

            var trail = restored.System_CreateDisabledMissionPresetSnapshot();
            trail["NoKillRewardAI"] = MessagePackSerializer.Serialize(true);
            trail["EnableMod"] = MessagePackSerializer.Serialize(true);
            restored.System_EnterMissionPreset(trail, "Old Trail", false);
            check(restored.KillRewardGoldAI == 0 && restored.KillRewardGoodsAI == 0, "Legacy AI Trail migrates after complete snapshot");
            restored.KillRewardGoldAI = 70;
            check(restored.KillRewardGoldAI == 0, "Read-only Trail rejects slider change");
            var trailExport = ((IModSettingsWorkingCopyEndpoint)restored).System_CreateCurrentWorkingSnapshot();
            check(!MessagePackSerializer.Deserialize<bool>(trailExport["NoKillRewardAI"]) &&
                MessagePackSerializer.Deserialize<int>(trailExport["KillRewardGoodsAI"]) == 0, "Trail export canonicalizes without rewriting source");
            restored.System_ExitMissionPreset();
            check(restored.KillRewardGoldHuman == 37 && restored.KillRewardGoldAI == -1, "Leaving Trail restores own values");
            endpoint = restored;
            endpoint.System_EnterMissionPreset(null, "Unconfigured Trail", false);
            check(restored.KillRewardGoldHuman == -1 && restored.KillRewardGoodsHuman == -1, "Null mission snapshot retains the default-source contract");
            endpoint.System_ExitMissionPreset();

            // Legacy named presets can contain only the retired property. Capture that
            // real file shape through the same confirmed-selection override as the UI.
            restored.System_SavePersonalPreset("old", "Old", "", new[] {
                new PresetSaveSelection { PropertyName = "NoKillRewardAI", Mode = PublishedPresetValueMode.Fixed }
            }, false);
            var oldPreset = restored.System_TestPublishedPresets.Single(p => p.Id == "old");
            oldPreset.Settings["NoKillRewardAI"].Value = true;
            restored.System_TestLoadPreset(oldPreset.StableId);
            check(prepare().KillRewardGoodsAI == 0, "Old named preset is migrated and saved immediately");
            restored.KillRewardGoldAI = 61;
            check(prepare().KillRewardGoldAI == 61 && prepare().KillRewardGoodsAI == 0, "Named-preset migration survives independent edits and restart");
            endpoint = restored;
            endpoint.System_EnterMissionPreset(trail, "Editable old Trail", true);
            check(MessagePackSerializer.Deserialize<bool>(trail["NoKillRewardAI"]), "Loading leaves caller-owned Trail data unchanged");
            restored.System_TestLoadPreset(oldPreset.StableId);
            restored.KillRewardGoldAI = 62;
            var editable = endpoint.System_CreateCurrentWorkingSnapshot();
            check(!MessagePackSerializer.Deserialize<bool>(editable["NoKillRewardAI"]) &&
                MessagePackSerializer.Deserialize<int>(editable["KillRewardGoldAI"]) == 62 &&
                MessagePackSerializer.Deserialize<int>(editable["KillRewardGoodsAI"]) == 0, "Named legacy preset inside editable Trail retires aliases before edits");
            endpoint.System_ExitMissionPreset();
            restored.KillRewardGoldAI = restored.KillRewardGoodsAI = -1;

            restored.SetKillRewardMissionLocked(true); restored.KillRewardGoldHuman = 50; restored.KillRewardGoldMaximumAIText = "10";
            check(restored.KillRewardGoldHuman == 37 && restored.KillRewardGoldMaximumAI == -1 && !restored.CanConfigureKillReward, "Mission locks every input");
            restored.SetKillRewardMissionLocked(false);
            GameNetworkAPI.LocalHost = false; GameNetworkAPI.Networked = true; GameNetworkAPI.MultiplayerGame = true;
            restored.KillRewardGoldHuman = 50;
            check(restored.KillRewardGoldHuman == 37 && !restored.CanConfigureKillReward, "Client rejects own host-setting mutation");
            GameXAMLManagerAPI.Instance.ApplyNetworkSync(restored, () => { restored.KillRewardGoldHuman = 75; restored.KillRewardGoodsAI = 5; });
            check(restored.KillRewardGoldHuman == 75 && restored.KillRewardGoodsAI == 5, "Host sync reaches runtime values");
            var ownExport = ((IModSettingsWorkingCopyEndpoint)restored).System_CreatePlayerMissionPresetSnapshot();
            check(MessagePackSerializer.Deserialize<int>(ownExport["KillRewardGoldHuman"]) == 37, "Export of owned profile never captures live remote gold");
            GameNetworkAPI.LocalHost = true; GameNetworkAPI.Networked = false; GameNetworkAPI.MultiplayerGame = false;
            check(prepare().KillRewardGoldHuman == 37, "Remote host does not overwrite own file");
            restored.System_RefreshSettingsAccess();
            restored.ResetToDefaultCommand.Execute(null);
            check(restored.KillRewardGoldHuman == -1 && restored.KillRewardGoodsAI == -1 && restored.KillRewardGoldMaximumHuman == -1, "Reset restores Vanilla");

            legacy["NoKillRewardAI"] = MessagePackSerializer.Serialize(true);
            string clientFixture = TestWorkDirectory.New("KillRewardClient");
            PersonalSettingsPaths.TestDocumentsDirectory = _ => clientFixture;
            prepare();
            storage = Directory.GetFiles(clientFixture, "settings.msgpack", SearchOption.AllDirectories).Single();
            File.WriteAllBytes(storage, MessagePackSerializer.Serialize(legacy));
            GameNetworkAPI.LocalHost = false; GameNetworkAPI.Networked = true; GameNetworkAPI.MultiplayerGame = true;
            var client = prepare();
            // APIShared may upgrade the storage envelope during registration. Afterwards,
            // remote and Trail values must leave those owned bytes completely unchanged.
            byte[] clientProfile = File.ReadAllBytes(storage);
            GameXAMLManagerAPI.Instance.ApplyNetworkSync(client, () => { client.KillRewardGoldHuman = 83; client.KillRewardGoodsAI = 7; });
            IModSettingsWorkingCopyEndpoint clientEndpoint = client;
            clientEndpoint.System_EnterMissionPreset(trail, "Client Trail", false);
            clientEndpoint.System_ExitMissionPreset();
            check(File.ReadAllBytes(storage).SequenceEqual(clientProfile), "Client sync and Trail return leave owned personal bytes untouched");
            GameNetworkAPI.LocalHost = true; GameNetworkAPI.Networked = false; GameNetworkAPI.MultiplayerGame = false;
            client.System_RefreshSettingsAccess();
            var takeover = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(File.ReadAllBytes(storage));
            check(!MessagePackSerializer.Deserialize<bool>(takeover["NoKillRewardHuman"]) &&
                !MessagePackSerializer.Deserialize<bool>(takeover["NoKillRewardAI"]), "Host takeover persists retired legacy switches immediately");
            check(client.KillRewardGoldHuman == 0 && client.KillRewardGoodsHuman == 0 && client.KillRewardGoldAI == 0 && client.KillRewardGoodsAI == 0,
                "Later host takeover restores and migrates the owned profile for both roles");
            client.KillRewardGoldHuman = 19; client.KillRewardGoodsAI = 23;
            var hostRestart = prepare();
            check(hostRestart.KillRewardGoldHuman == 19 && hostRestart.KillRewardGoodsHuman == 0 &&
                hostRestart.KillRewardGoldAI == 0 && hostRestart.KillRewardGoodsAI == 23, "Host takeover migration persists independent changes without foreign values: " +
                string.Join(",", hostRestart.KillRewardGoldHuman, hostRestart.KillRewardGoodsHuman, hostRestart.KillRewardGoldAI, hostRestart.KillRewardGoodsAI));
        }
        finally
        {
            PersonalSettingsPaths.TestDocumentsDirectory = previousDocuments;
            GameNetworkAPI.LocalHost = true; GameNetworkAPI.Networked = false; GameNetworkAPI.MultiplayerGame = false;
        }
    }
}
