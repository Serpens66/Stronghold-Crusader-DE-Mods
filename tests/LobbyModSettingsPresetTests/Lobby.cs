using APIShared.ModSettings;
using MessagePack;
using SHCDESE.API.Components.Network;
using APIShared.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LobbyModSettingsPresetTests
{
    public partial class PresetTests
    {
        [TestMethod]
        public void DirectLaunchNotices() => TestDirectLaunchNotices(testRoot);

        private static void TestDirectLaunchNotices(string root)
        {
            string folder = Path.Combine(root, "DirectLaunchNotices");
            string path = Path.Combine(folder, "LobbyModSettings", ModName + ".msgpack");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            FakeSettings settings = Start(Path.Combine(folder, "PresetTest.dll"), path, () => true);
            settings.System_ConfigureDirectLaunchNotice("BuildingCosts_Serp");

            settings.System_TestSetSettingsMenuContext(false, true, false);
            Assert(settings.System_DirectLaunchNoticeVisibility == Noesis.Visibility.Visible,
                "Direct campaign does not explain why gameplay settings are inactive.");
            settings.System_TestSetSettingsMenuContext(false, false, true);
            Assert(settings.System_DirectLaunchNoticeVisibility == Noesis.Visibility.Visible,
                "Direct Trail without its own settings does not show the inactive notice.");
            settings.System_TestSetSettingsMenuContext(true, false, false, true);
            Assert(settings.System_DirectLaunchNoticeVisibility == Noesis.Visibility.Visible &&
                   settings.System_TrailSourceNoticeVisibility == Noesis.Visibility.Collapsed,
                "Direct Vanilla Coop Trail was mistaken for Customize.");

            settings.System_SetExplicitMissionSettings(true);
            settings.System_EnterMissionPreset(new Dictionary<string, byte[]>(), "Trail", false);
            settings.System_TestSetSettingsMenuContext(false, false, true);
            Assert(settings.System_TrailSourceNoticeVisibility == Noesis.Visibility.Visible &&
                   settings.System_DirectLaunchNoticeVisibility == Noesis.Visibility.Collapsed &&
                   !settings.CanEditHostSettings && !settings.CanChangePreset,
                "Trail-owned settings are not shown as read-only.");
            settings.System_TestSetSettingsMenuContext(true, false, false, true);
            Assert(settings.System_TrailSourceNoticeVisibility == Noesis.Visibility.Visible &&
                   settings.System_DirectLaunchNoticeVisibility == Noesis.Visibility.Collapsed &&
                   !settings.CanEditHostSettings && !settings.CanChangePreset,
                "Direct Custom Coop Trail did not show its read-only mission settings.");
            settings.System_ExitMissionPreset();

            settings.System_SetExplicitMissionSettings(true);
            settings.System_EnterMissionPreset(new Dictionary<string, byte[]>(), "Trail", true);
            settings.System_TestSetSettingsMenuContext(true, false, true);
            Assert(settings.System_TrailSourceNoticeVisibility == Noesis.Visibility.Collapsed &&
                   settings.System_DirectLaunchNoticeVisibility == Noesis.Visibility.Collapsed &&
                   settings.CanEditHostSettings,
                "Customize did not restore editable Trail settings.");
            settings.System_TestSetSettingsMenuContext(true, false, false, false);
            Assert(settings.System_TrailSourceNoticeVisibility == Noesis.Visibility.Collapsed &&
                   settings.System_DirectLaunchNoticeVisibility == Noesis.Visibility.Collapsed &&
                   settings.CanEditHostSettings,
                "Coop Customize setup retained the direct Trail notice or lock.");
            settings.System_ExitMissionPreset();

            settings.System_TestSetSettingsMenuContext(false, true, true);
            Assert(settings.System_DirectLaunchNoticeVisibility == Noesis.Visibility.Collapsed,
                "Ambiguous front-end state incorrectly claims a direct launch.");
        }
        [TestMethod]
        public void SavegameReturnToLobby() => TestSavegameReturnToLobby(testRoot);

        private static void TestSavegameReturnToLobby(string root)
        {
            string folder = Path.Combine(root, "SavegameReturn");
            string path = Path.Combine(folder, "LobbyModSettings", ModName + ".msgpack");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            WriteLegacy(path, true, 27);
            FakeSettings settings = Start(Path.Combine(folder, "PresetTest.dll"), path, () => true);
            bool previousEnabled = settings.EnableMod;
            int previousNumber = settings.Number;
            settings.System_EnterMissionPreset(new Dictionary<string, byte[]>
            {
                [nameof(FakeSettings.EnableMod)] = MessagePackSerializer.Serialize(false),
                [nameof(FakeSettings.Number)] = MessagePackSerializer.Serialize(91),
            }, "Savegame", false);
            Assert(settings.IsMissionPresetSelected &&
                !settings.CanChangePreset && !settings.CanEditHostSettings && !settings.CanResetSettings,
                "loaded savegame did not enter the read-only preset context");
            settings.System_ExitMissionPreset();
            Assert(!settings.IsMissionPresetActive &&
                settings.CanChangePreset && settings.CanEditHostSettings && settings.CanResetSettings &&
                settings.EnableMod == previousEnabled && settings.Number == previousNumber,
                "returning to the lobby did not restore the previous editable preset");
            settings.Number = 34;
            Assert(settings.Number == 34, "returning to the lobby did not permit a value edit");
            settings.System_LoadModDefaults();
            Assert(settings.CanEditHostSettings && settings.CanChangePreset,
                "returning to the lobby did not permit a default reset");
            PublishedModSettingsPreset personal = settings.System_TestPublishedPresets.First(item =>
                item.SourceKind == ModSettingsPresetSourceKind.Personal);
            settings.System_TestLoadPreset(personal.StableId);
            Assert(settings.CanEditHostSettings && settings.CanChangePreset,
                "returning to the lobby did not permit loading a preset");
        }

    }
}
