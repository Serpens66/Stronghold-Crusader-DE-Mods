using APIShared.ModSettings;
using ExtraFeatures;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.Interop;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class HealthMultiplierTests
{
    internal static void Run(Action<bool, string> check)
    {
        check(HealthMultiplierPolicy.NormalizePercent(-1) == 10 && HealthMultiplierPolicy.NormalizePercent(900) == 500,
            "Health percentage keeps the 10..500 input contract");
        foreach (eChimps type in new[] { eChimps.CHIMP_TYPE_ENGINEER, eChimps.CHIMP_TYPE_TUNNELER,
            eChimps.CHIMP_TYPE_LADDERMAN, eChimps.CHIMP_TYPE_SIEGE_TOWER, eChimps.CHIMP_TYPE_BEDOUIN_HEALER,
            eChimps.CHIMP_TYPE_MONK, eChimps.CHIMP_TYPE_ARAB_SLAVE, eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL })
            check(HealthMultiplierPolicy.ClassifyUnit(type) == HealthCategory.Soldier, "Military support and mobile siege use soldier HP: " + type);
        foreach (eChimps type in new[] { eChimps.CHIMP_TYPE_PEASANT, eChimps.CHIMP_TYPE_WOODCUTTER,
            eChimps.CHIMP_TYPE_MINER1, eChimps.CHIMP_TYPE_PRIEST, eChimps.CHIMP_TYPE_HEALER,
            eChimps.CHIMP_TYPE_FIREMAN, eChimps.CHIMP_TYPE_TRADER })
            check(HealthMultiplierPolicy.ClassifyUnit(type) == HealthCategory.Civilian, "Functional civilian uses civilian HP: " + type);
        foreach (eChimps type in new[] { eChimps.CHIMP_TYPE_QUARRY_OX, eChimps.CHIMP_TYPE_TRADER_HORSE,
            eChimps.CHIMP_TYPE_WAR_DOG, eChimps.CHIMP_TYPE_DEER, eChimps.CHIMP_TYPE_LADY,
            eChimps.CHIMP_TYPE_JESTER, eChimps.CHIMP_TYPE_MOTHER, eChimps.CHIMP_TYPE_CHILD,
            eChimps.CHIMP_TYPE_JUGGLER, eChimps.CHIMP_TYPE_FIREEATER, eChimps.CHIMP_TYPE_NULL })
            check(HealthMultiplierPolicy.ClassifyUnit(type) == HealthCategory.None, "Animals and passive decorations remain unchanged: " + type);
        check(HealthMultiplierPolicy.ClassifyUnit(eChimps.CHIMP_TYPE_LORD) == HealthCategory.Lord,
            "Lord multiplier remains separate from soldiers");
        check(HealthMultiplierPolicy.ClassifyBuilding(eStructs.STRUCT_SIEGE_TOWER) == HealthCategory.None &&
            HealthMultiplierPolicy.ClassifyBuilding(eStructs.STRUCT_GATE_MAIN) == HealthCategory.Building &&
            HealthMultiplierPolicy.ClassifyBuilding(eStructs.STRUCT_TOWER1) == HealthCategory.Building,
            "Ordinary towers and gates use building HP; mobile tower proxy is not multiplied twice");
        foreach (eStructs type in new[] { eStructs.STRUCT_WAS_WALL, eStructs.STRUCT_WOOD_WALL,
            eStructs.STRUCT_STONE_WALL, eStructs.STRUCT_CRENAL_WALL })
            check(HealthMultiplierPolicy.ClassifyBuilding(type) == HealthCategory.None,
                "Wall-typed building contexts retain native HP: " + type);

        string[] names = (from category in new[] { "Lord", "Soldier", "Civilian", "Building" }
                          from role in new[] { "Human", "AI" }
                          select role + category + "HealthPercent").ToArray();
        string fixture = TestWorkDirectory.New("HealthPreset"); Directory.CreateDirectory(fixture);
        var previous = PersonalSettingsPaths.TestDocumentsDirectory;
        bool host = GameNetworkAPI.LocalHost, networked = GameNetworkAPI.Networked, multiplayer = GameNetworkAPI.MultiplayerGame;
        PersonalSettingsPaths.TestDocumentsDirectory = _ => fixture;
        GameNetworkAPI.LocalHost = true; GameNetworkAPI.Networked = false; GameNetworkAPI.MultiplayerGame = false;
        try
        {
            string plugin = Path.Combine(fixture, "ExtraFeatures.dll");
            Func<ExtraFeaturesViewModel> prepare = () => {
                var model = new ExtraFeaturesViewModel(false);
                model.PreparePresets(null, plugin, "Health"); model.ActivatePresets(); return model;
            };
            ExtraFeaturesViewModel settings = prepare();
            for (int index = 0; index < names.Length; index++)
            {
                string name = names[index];
                PropertyInfo property = typeof(ExtraFeaturesViewModel).GetProperty(name);
                PropertyInfo text = typeof(ExtraFeaturesViewModel).GetProperty(name + "Text");
                check(property.GetCustomAttribute<SyncHostOnlyAttribute>() != null && (int)property.GetValue(settings) == 100,
                    "Each health setting is host controlled with neutral default: " + name);
                property.SetValue(settings, int.MinValue); check((int)property.GetValue(settings) == 10, "Health lower input bound: " + name);
                text.SetValue(settings, "900%"); check((int)property.GetValue(settings) == 500, "Health text upper input bound: " + name);
                text.SetValue(settings, "invalid"); check((int)property.GetValue(settings) == 500, "Invalid health input retains value: " + name);
                property.SetValue(settings, 110 + index * 10);
            }
            var captured = new HealthSettingsSnapshot(settings);
            check(captured.GetPercent(HealthCategory.Lord, false) == 110 && captured.GetPercent(HealthCategory.Lord, true) == 120 &&
                captured.GetPercent(HealthCategory.Soldier, false) == 130 && captured.GetPercent(HealthCategory.Soldier, true) == 140 &&
                captured.GetPercent(HealthCategory.Civilian, false) == 150 && captured.GetPercent(HealthCategory.Civilian, true) == 160 &&
                captured.GetPercent(HealthCategory.Building, false) == 170 && captured.GetPercent(HealthCategory.Building, true) == 180,
                "Settings snapshot retains every independent role/category value");
            settings.HumanSoldierHealthPercent = 500;
            check(captured.GetPercent(HealthCategory.Soldier, false) == 130, "Mission health snapshot does not read live settings");
            settings.HumanSoldierHealthPercent = 130;
            ExtraFeaturesViewModel restarted = prepare();
            for (int index = 0; index < names.Length; index++)
                check((int)typeof(ExtraFeaturesViewModel).GetProperty(names[index]).GetValue(restarted) == 110 + index * 10,
                    "Health setting survives personal storage restart: " + names[index]);
            GameNetworkAPI.LocalHost = false; GameNetworkAPI.Networked = true; GameNetworkAPI.MultiplayerGame = true;
            foreach (string name in names)
            {
                PropertyInfo property = typeof(ExtraFeaturesViewModel).GetProperty(name);
                int before = (int)property.GetValue(restarted); property.SetValue(restarted, 400);
                check((int)property.GetValue(restarted) == before, "Client cannot change host health setting: " + name);
                GameXAMLManagerAPI.Instance.ApplyNetworkSync(restarted, () => property.SetValue(restarted, 220));
                check((int)property.GetValue(restarted) == 220, "Authoritative health setting is accepted: " + name);
            }
            GameNetworkAPI.LocalHost = true; GameNetworkAPI.Networked = false; GameNetworkAPI.MultiplayerGame = false;
            ExtraFeaturesViewModel own = prepare();
            for (int index = 0; index < names.Length; index++)
                check((int)typeof(ExtraFeaturesViewModel).GetProperty(names[index]).GetValue(own) == 110 + index * 10,
                    "Received health setting does not overwrite personal preset: " + names[index]);
            own.System_LoadModDefaults();
            foreach (string name in names)
                check((int)typeof(ExtraFeaturesViewModel).GetProperty(name).GetValue(own) == 100, "Reset restores neutral health setting: " + name);
        }
        finally
        {
            PersonalSettingsPaths.TestDocumentsDirectory = previous;
            GameNetworkAPI.LocalHost = host; GameNetworkAPI.Networked = networked; GameNetworkAPI.MultiplayerGame = multiplayer;
        }
    }
}
