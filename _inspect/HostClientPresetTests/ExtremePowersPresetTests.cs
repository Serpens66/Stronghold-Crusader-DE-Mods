using ExtremePowers.Settings;
using MessagePack;
using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

internal static class ExtremePowersPresetTests
{
    internal static void Run()
    {
        GameNetworkAPI.ThrowOnRoleQuery = false;
        GameNetworkAPI.Networked = true;
        GameNetworkAPI.LocalHost = true;
        var settings = new ExtremePowersSettings();
        settings.PreparePresets(null,
            Path.Combine(Program.PresetFixtureInstallRoot, "ExtremePowers.dll"), "ExtremePowersPresetTests");
        settings.ActivatePresets();
        int ownerNotifications = 0, rejectedWrites = 0;
        settings.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(settings.DemoOwner)) ownerNotifications++;
            if (args.PropertyName == nameof(settings.DemoOwner) && settings.IsSuppressingSync) rejectedWrites++;
        };

        CompleteSnapshot(settings);
        Check(ownerNotifications == 0, "unchanged host owner raised a notification");

        GameNetworkAPI.LocalHost = false;
        settings.System_RefreshSettingsAccess();
        settings.System_EnterMissionPreset(Values(24, 3), "Save", false);
        Check(settings.DemoOwner == 3, "client rejected an authorised mission snapshot");
        Check(rejectedWrites == 0, "client snapshot completion attempted a host write");
        settings.System_ExitMissionPreset();
        Check(settings.DemoOwner == 3 && rejectedWrites == 0,
            "client restoration attempted to change an authoritative host value");
        int before = ownerNotifications;
        CompleteSnapshot(settings);
        Check(ownerNotifications == before && rejectedWrites == 0,
            "unchanged client owner triggered a UI revert");
        settings.DemoOwner = 4;
        Check(settings.DemoOwner == 3 && rejectedWrites == 1,
            "real client edit was not rejected with a UI revert");
        GameXAMLManagerAPI.Instance.ApplyNetworkSync(settings, () => settings.DemoOwner = 5);
        Check(settings.DemoOwner == 5 && rejectedWrites == 1,
            "authorised host update was rejected");

        GameNetworkAPI.LocalHost = true;
        settings.System_RefreshSettingsAccess();
        settings.System_EnterMissionPreset(Values(2, 6), "Read-only legacy save", false);
        Check(settings.DemoUnitType == 2 && settings.SpearmenType == 2 && settings.DemoSprite == "legacy sprite",
            "snapshot completion migrated a read-only mission");
        before = rejectedWrites;
        CompleteSnapshot(settings);
        Check(rejectedWrites == before && settings.DemoOwner == 6,
            "read-only snapshot completion attempted a write");
        settings.System_ExitMissionPreset();

        settings.System_EnterMissionPreset(Values(2, 7), "Editable legacy preset", true);
        Check(settings.SpearmenType == 24 && settings.EngineersType == 30 && settings.MacemenType == 26 &&
            settings.KnightsType == 28 && settings.DemoUnitType == 24 && settings.DemoSprite == "extreme power 3",
            "editable host snapshot no longer migrates invalid choices");
        Check(settings.DemoOwner == 7, "valid explicit owner was not retained during migration");
        settings.DemoUnitType = 44;
        Check(settings.DemoOwner == 0 && settings.ResolveDemoOwner(3) == 0,
            "deer no longer use the nature owner");
        settings.DemoOwner = 8;
        Check(settings.DemoOwner == 0, "explicit owner bypassed the deer owner rule");
        settings.System_ExitMissionPreset();
        Console.WriteLine("PASS: ExtremePowers production settings snapshot completion, migration and host authority.");
    }

    private static Dictionary<string, byte[]> Values(int unitType, int owner) => new Dictionary<string, byte[]>
    {
        ["SpearmenType"] = MessagePackSerializer.Serialize(unitType),
        ["EngineersType"] = MessagePackSerializer.Serialize(unitType),
        ["MacemenType"] = MessagePackSerializer.Serialize(unitType),
        ["KnightsType"] = MessagePackSerializer.Serialize(unitType),
        ["DemoUnitType"] = MessagePackSerializer.Serialize(unitType),
        ["DemoOwner"] = MessagePackSerializer.Serialize(owner),
        ["DemoSprite"] = MessagePackSerializer.Serialize(unitType == 2 ? "legacy sprite" : "extreme power 3")
    };

    // Invoke the production completion hook independently to detect extra work after a snapshot.
    private static void CompleteSnapshot(ExtremePowersSettings settings) =>
        typeof(ExtremePowersSettings).GetMethod("OnSettingsSnapshotApplied", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(settings, null);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("ExtremePowers: " + message);
    }
}

namespace ExtremePowers.Settings
{
    // Localisation is an external presentation boundary; no Unity/translation runtime is needed here.
    internal static class SerpLocalization
    {
        internal static string Get(string key) => key;
    }
}
