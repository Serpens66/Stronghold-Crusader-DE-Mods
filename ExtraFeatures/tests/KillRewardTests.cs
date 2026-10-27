using APIShared.ModSettings;
using ExtraFeatures;
using MessagePack;
using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
                result.PreparePresets(null, plugin, "KillReward"); result.ActivatePresets(); return result;
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
        }
        finally
        {
            PersonalSettingsPaths.TestDocumentsDirectory = previousDocuments;
            GameNetworkAPI.LocalHost = true; GameNetworkAPI.Networked = false; GameNetworkAPI.MultiplayerGame = false;
        }
    }
}
