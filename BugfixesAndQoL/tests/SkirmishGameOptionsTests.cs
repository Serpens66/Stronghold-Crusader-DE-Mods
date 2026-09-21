using Iced.Intel;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace BugfixesAndQoL
{
    internal static class SkirmishGameOptionsTests
    {
        internal static void Run(Action<bool, string> check)
        {
            TestTransaction(check);
            TestAdvancedState(check);
            TestGuards(check);
            TestNoDogsContract(check);
            TestInstalledNoDogsBytes(check);
            TestXamlAndOwnership(check);
        }

        private static void TestTransaction(Action<bool, string> check)
        {
            var committed = new Model { Peace = 4, StrongWalls = 0, Buildings = new[] { 1, 1 } };
            var transaction = new WorkingCopyTransaction<Model>(Clone);
            Model working = transaction.Begin(committed);
            working.Peace = 20;
            working.StrongWalls = 1;
            working.Buildings[1] = 0;
            transaction.Cancel();
            check(committed.Peace == 4 && committed.StrongWalls == 0 && committed.Buildings[1] == 1,
                "Skirmish Game Options Cancel leaves every committed value unchanged");

            working = transaction.Begin(committed);
            working.Peace = 18;
            working.StrongWalls = 1;
            working.Buildings[0] = 0;
            transaction.ApplyTo(committed, Copy);
            check(committed.Peace == 18 && committed.StrongWalls == 1 && committed.Buildings[0] == 0,
                "Skirmish Game Options Apply commits general and availability values");
            check(transaction.Working == null,
                "Skirmish Game Options Apply closes the transaction");
        }

        private static void TestAdvancedState(Action<bool, string> check)
        {
            SkirmishGameOptionsPolicy.AdvancedState state = Defaults();
            check(SkirmishGameOptionsPolicy.ToSkirmishAdvancedFlag(true, state) == 0,
                "empty advanced settings normalize to disabled");
            state.Goods[1] = 0;
            check(SkirmishGameOptionsPolicy.ToSkirmishAdvancedFlag(true, state) == 1,
                "a goods-only restriction keeps Skirmish advanced settings enabled");
            check(SkirmishGameOptionsPolicy.ToSharedAdvancedFlag(0, 1, state) == 1,
                "shared presets accept the Skirmish advanced flag");
            state = Defaults();
            state.EnemyHitPoints = 2;
            check(SkirmishGameOptionsPolicy.ShouldShowAdvancedIndicator(1, state),
                "gameplay-only changes update the advanced indicator");
        }

        private static void TestGuards(Action<bool, string> check)
        {
            check(SkirmishGameOptionsPolicy.ShouldBlockCow(true, true),
                "No Cows blocks local Skirmish cow throwing");
            check(!SkirmishGameOptionsPolicy.ShouldBlockCow(false, true),
                "No Cows guard does not alter multiplayer");
            check(SkirmishGameOptionsPolicy.ShouldBlockAutoTrading(true, false),
                "disabled Auto-Trading blocks local Skirmish actions");
            check(!SkirmishGameOptionsPolicy.ShouldAllowOutpostToggle(true, false) &&
                  SkirmishGameOptionsPolicy.NormalizeOutpostsForApply(true, false, 1) == 0,
                "unsupported Skirmish outposts are disabled and normalized");
            check(SkirmishGameOptionsPolicy.IsPresetLoadCommand("UsePrevious") &&
                  SkirmishGameOptionsPolicy.IsPresetLoadCommand("UsePresets1") &&
                  SkirmishGameOptionsPolicy.IsPresetSaveCommand("SavePresets2"),
                "shared Previous and persistent preset commands use the working copy");
        }

        private static void TestNoDogsContract(Action<bool, string> check)
        {
            byte[] image = new byte[NoDogsNativeContract.PatchRva + 16];
            Array.Copy(
                NoDogsNativeContract.ExpectedBytes,
                0,
                image,
                NoDogsNativeContract.PatchRva,
                NoDogsNativeContract.ExpectedBytes.Length);
            NoDogsNativeContract.Validate(
                image,
                NoDogsNativeContract.PreferredImageBase,
                true);

            var assembler = new Assembler(64);
            NoDogsNativeContract.EmitReplacement(assembler);
            byte[] assembled;
            using (var stream = new MemoryStream())
            {
                var writer = new StreamCodeWriter(stream);
                bool success = assembler.TryAssemble(
                    writer,
                    NoDogsNativeContract.PreferredImageBase + NoDogsNativeContract.PatchRva,
                    out string error,
                    out _,
                    BlockEncoderOptions.None);
                check(success, "installed Iced assembles the No Dogs replacement: " + error);
                assembled = stream.ToArray();
            }
            check(assembled.SequenceEqual(NoDogsNativeContract.ReplacementBytes),
                "No Dogs production emitter produces exactly 90 90");

            var decoder = Decoder.Create(64, new ByteArrayCodeReader(assembled));
            decoder.IP = NoDogsNativeContract.PreferredImageBase + NoDogsNativeContract.PatchRva;
            check(decoder.Decode().Mnemonic == Mnemonic.Nop &&
                  decoder.Decode().Mnemonic == Mnemonic.Nop,
                "No Dogs replacement decodes completely as two one-byte NOPs");
        }

        private static void TestInstalledNoDogsBytes(Action<bool, string> check)
        {
            string gameDirectory = Environment.GetEnvironmentVariable("SHCDE_GAME_DIR") ??
                @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
            string path = Path.Combine(
                gameDirectory,
                @"Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll");
            check(File.Exists(path), "installed native DLL exists for Skirmish regression");
            byte[] file = File.ReadAllBytes(path);
            using (SHA256 sha = SHA256.Create())
            {
                string hash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", string.Empty);
                check(hash == NoDogsNativeContract.ReferenceSha256,
                    "installed native DLL matches the audited Skirmish hash");
            }
            int offset = RvaToFileOffset(file, NoDogsNativeContract.PatchRva);
            check(file[offset] == 0x74 && file[offset + 1] == 0x31,
                "installed No Dogs byte window is the audited JE rel8");
        }

        private static void TestXamlAndOwnership(Action<bool, string> check)
        {
            string project = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                @"..\.."));
            string frontPath = Path.Combine(
                project,
                @"Patches\Assets\GUI\XAMLResources\FRONT_Multiplayer.xaml");
            XDocument front = XDocument.Load(frontPath);
            XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
            XElement host = front.Descendants().Single(element =>
                (string)element.Attribute(xaml + "Name") ==
                    "BugfixesAndQoLSkirmishGameOptionsButtonHost");
            XElement button = host.Descendants().Single(element =>
                element.Name.LocalName == "Button");
            check((string)host.Attribute("Margin") == "52,5,0,0" &&
                  (string)button.Attribute("CommandParameter") == "Setup",
                "Skirmish Settings button keeps its audited position and Vanilla command");
            check((string)host.Attribute("Visibility") == "{Binding ButtonVisibility}" &&
                  (string)button.Attribute(xaml + "Name") ==
                      "BugfixesAndQoLSkirmishGameOptionsButton" &&
                  (string)button.Attribute(xaml + "Name") != (string)host.Attribute(xaml + "Name") &&
                  (string)button.Attribute("Command") == "{Binding MultiplayerMenuCommand}" &&
                  (string)button.Attribute("Visibility") ==
                      "{Binding SkirmishSetupMode, Converter={StaticResource booleanToVisibilityConverter}}" &&
                  (string)button.Attribute(XName.Get("PropEx.TextCentre", "clr-namespace:CrusaderDE")) ==
                      "{Binding Source={x:Static local:Translate.Instance}, Path=GameTexts[TEXT_NEW_TEXT2_058]}",
                "registered host owns only mod visibility while the named button retains the working Vanilla command and stable localized text");
            check(front.Root.Elements("Operation").Any(operation =>
                    (string)operation.Attribute("AttributeName") == "Panel.ZIndex" &&
                    (string)operation.Attribute("Value") == "1000"),
                "Game Options host retains foreground Z-index");

            string settingsPath = Path.Combine(
                project,
                @"Override\ScriptExtenderUI\BugfixesAndQoLSettings.xaml");
            XDocument settingsXaml = XDocument.Load(settingsPath);
            XElement hostInterface = settingsXaml.Descendants().Single(element =>
                element.Attributes().Any(attribute =>
                    attribute.Name.LocalName == "ModSettingsSearch.SectionKey" &&
                    (string)attribute == "bugfixes.section.host-interface"));
            string[] interfaceSettingKeys =
            {
                "bugfixes.show-mp-advanced-settings-in-sp",
                "bugfixes.enable-custom-lord-list-enhancements",
                "bugfixes.enable-trail-customization-buttons"
            };
            foreach (string settingKey in interfaceSettingKeys)
            {
                check(hostInterface.Descendants().Any(element =>
                          element.Attributes().Any(attribute =>
                              attribute.Name.LocalName == "ModSettingsSearch.Key" &&
                              (string)attribute == settingKey)) &&
                      settingsXaml.Descendants().Count(element =>
                          element.Attributes().Any(attribute =>
                              attribute.Name.LocalName == "ModSettingsSearch.Key" &&
                              (string)attribute == settingKey)) == 1,
                    settingKey + " exists exactly once in the Host Interface section");
            }
            check(settingsXaml.ToString().Contains("{Binding InterfaceTitleText}") &&
                  !settingsXaml.ToString().Contains("ClientInterfaceTitleText"),
                "client and host sections share the generalized Interface title binding");

            string[] localePaths = Directory.GetFiles(
                Path.Combine(project, "Locales"),
                "*.txt");
            check(localePaths.Length > 0 && localePaths.All(localePath =>
                    File.ReadAllText(localePath).Contains("BugfixesAndQoL.InterfaceTitle=") &&
                    !File.ReadAllText(localePath).Contains("BugfixesAndQoL.ClientInterfaceTitle=")),
                "every supported locale exposes only the generalized Interface title key");
            string localizationFallbacks = File.ReadAllText(Path.Combine(
                project,
                @"..\Shared\SerpLocalization.cs"));
            check(localizationFallbacks.Contains("{ \"BugfixesAndQoL.InterfaceTitle\",") &&
                  !localizationFallbacks.Contains("{ \"BugfixesAndQoL.ClientInterfaceTitle\","),
                "the Shared fallback uses only the generalized Interface title key");

            string setupPath = Path.Combine(
                project,
                @"Patches\Assets\GUI\XAMLResources\FRONT_Multiplayer_Setup.xaml");
            string setup = File.ReadAllText(setupPath);
            check(setup.Contains("Show_MPNotSkirmishIsHost") && setup.Contains("Show_MPIsHost"),
                "Vanilla preset strip is enabled for local Skirmish hosts");

            string runtime = File.ReadAllText(Path.Combine(project, @"src\SkirmishGameOptionsRuntime.cs"));
            check(runtime.Contains("viewModel.Show_MPPeacetime = peaceTimeNativeAvailable;") &&
                  !runtime.Contains("ExtraFeatures") &&
                  runtime.Contains("working.peacetime = committedBeforeApply.peacetime;") &&
                  runtime.Contains("committed.advanced_options = 0;") &&
                  runtime.Contains("self.FindName(\"UnitFireCow\") as Grid") &&
                  !runtime.Contains("self.RefUnitFireCow"),
                "integrated dialog owns Vanilla Peace Time, fails closed and normalizes Skirmish advanced state");
            string access = File.ReadAllText(Path.Combine(
                project,
                @"src\SkirmishGameOptionsAccessViewModel.cs"));
            int bindingGuardStart = access.IndexOf(
                "if (button == null || vanillaViewModel == null ||",
                StringComparison.Ordinal);
            int bindingGuardEnd = access.IndexOf(
                "{",
                bindingGuardStart >= 0 ? bindingGuardStart : 0,
                StringComparison.Ordinal);
            int bindingAssignment = access.IndexOf(
                "button.DataContext = vanillaViewModel;",
                bindingGuardStart >= 0 ? bindingGuardStart : 0,
                StringComparison.Ordinal);
            string bindingGuard = bindingGuardStart >= 0 && bindingGuardEnd > bindingGuardStart
                ? access.Substring(bindingGuardStart, bindingGuardEnd - bindingGuardStart)
                : string.Empty;
            check(access.Contains("INoesisElementBindingAware") &&
                  access.Contains("element?.FindName(SettingsButtonName) as Button") &&
                  bindingGuard.Contains("vanillaViewModel.MultiplayerMenuCommand == null") &&
                  !bindingGuard.Contains("MP_Settings_Button") &&
                  bindingAssignment >= 0 &&
                  !access.Contains("textReady=") &&
                  !access.Contains("textAvailable=") &&
                  access.Contains("BUGFIXES_AND_QOL_SKIRMISH_GAME_OPTIONS_BUTTON_BIND_FAILED") &&
                  access.Contains("BUGFIXES_AND_QOL_SKIRMISH_GAME_OPTIONS_BUTTON_BOUND"),
                "button binding callback restores MainViewModel and logs only structural failures");

            string testModFront = File.ReadAllText(Path.Combine(
                project,
                @"..\Testmods\SkirmishGameOptionsTest\Patches\Assets\GUI\XAMLResources\FRONT_Multiplayer.xaml"));
            string productionFront = File.ReadAllText(frontPath);
            string stableSettingsTextBinding =
                "{Binding Source={x:Static local:Translate.Instance}, Path=GameTexts[TEXT_NEW_TEXT2_058]}";
            check(productionFront.Contains(stableSettingsTextBinding) &&
                  testModFront.Contains(stableSettingsTextBinding) &&
                  !productionFront.Contains("{Binding MP_Settings_Button}") &&
                  !testModFront.Contains("{Binding MP_Settings_Button}"),
                "injected Settings buttons never depend on Vanilla's initially empty MP_Settings_Button property");
            string noDogsPatch = File.ReadAllText(Path.Combine(
                project,
                @"src\NoDogsNativePatch.cs"));
            string peaceTimePatch = File.ReadAllText(Path.Combine(
                project,
                @"src\VanillaPeaceTimeGameplayPatch.cs"));
            check(!runtime.Contains("DebugLogHelper.LogInfo(") &&
                  !access.Contains("DebugLogHelper.LogInfo(") &&
                  !noDogsPatch.Contains("DebugLogHelper.LogInfo(") &&
                  !peaceTimePatch.Contains("DebugLogHelper.LogInfo(") &&
                  runtime.Contains("DebugLogHelper.LogDebug(") &&
                  access.Contains("DebugLogHelper.LogDebug(") &&
                  noDogsPatch.Contains("DebugLogHelper.LogDebug(") &&
                  peaceTimePatch.Contains("DebugLogHelper.LogDebug("),
                "successful initialization and routine Skirmish actions are debug-only");
            check(runtime.Contains("DebugLogHelper.LogWarning(") &&
                  runtime.Contains("SKIRMISH_GAME_OPTIONS_NO_DOGS_DISABLED") &&
                  runtime.Contains("SKIRMISH_GAME_OPTIONS_PEACE_TIME_DISABLED") &&
                  access.Contains("DebugLogHelper.LogError(") &&
                  access.Contains("SKIRMISH_GAME_OPTIONS_BUTTON_BIND_FAILED"),
                "feature degradation and structural binding failures remain visible");
            string extraRuntime = File.ReadAllText(Path.Combine(
                project,
                @"..\ExtraFeatures\src\ExtraFeaturesRuntime.cs"));
            check(!extraRuntime.Contains("VanillaPeaceTimeRuntime") &&
                  !extraRuntime.Contains("VanillaPeaceTimeGameplayPatch"),
                "ExtraFeatures no longer initializes Peace Time runtime or native patches");

            string bugfixSettings = File.ReadAllText(Path.Combine(
                project,
                @"src\BugfixesAndQoLViewModel.cs"));
            check(bugfixSettings.Contains("private bool showMpAdvancedSettingsInSingleplayer = true;") &&
                  bugfixSettings.Contains("public bool ShowMpAdvancedSettingsInSingleplayer") &&
                  bugfixSettings.Contains("public string InterfaceTitleText") &&
                  !bugfixSettings.Contains("ClientInterfaceTitleText"),
                "Singleplayer Game Options host setting exists and defaults to enabled");
            string extraSettings = File.ReadAllText(Path.Combine(
                project,
                @"..\ExtraFeatures\src\ExtraFeaturesViewModel.cs"));
            int peaceProperty = extraSettings.IndexOf("public int VanillaPeaceTimeMinutes", StringComparison.Ordinal);
            int peaceAttributes = peaceProperty < 0
                ? -1
                : extraSettings.LastIndexOf("[Obsolete", peaceProperty, StringComparison.Ordinal);
            string peacePrefix = peaceAttributes < 0
                ? string.Empty
                : extraSettings.Substring(peaceAttributes, peaceProperty - peaceAttributes);
            check(peaceProperty >= 0 && peaceAttributes >= 0 &&
                  peacePrefix.Contains("DoNotPersist") &&
                  !peacePrefix.Contains("SyncHostOnly"),
                "ExtraFeatures Peace Time compatibility shim is hidden, unsynchronized and unpersisted");
        }

        private static SkirmishGameOptionsPolicy.AdvancedState Defaults() =>
            new SkirmishGameOptionsPolicy.AdvancedState
            {
                Buildings = new[] { 1, 1, 1 },
                Goods = new[] { 1, 1, 1 },
                Troops = new[] { 1, 1, 1 },
                EnemyHitPoints = 1
            };

        private static Model Clone(Model source) =>
            new Model
            {
                Peace = source.Peace,
                StrongWalls = source.StrongWalls,
                Buildings = (int[])source.Buildings.Clone()
            };

        private static void Copy(Model source, Model target)
        {
            Model clone = Clone(source);
            target.Peace = clone.Peace;
            target.StrongWalls = clone.StrongWalls;
            target.Buildings = clone.Buildings;
        }

        private static int RvaToFileOffset(byte[] image, int rva)
        {
            int pe = BitConverter.ToInt32(image, 0x3C);
            int sectionCount = BitConverter.ToUInt16(image, pe + 6);
            int optionalSize = BitConverter.ToUInt16(image, pe + 20);
            int section = pe + 24 + optionalSize;
            for (int index = 0; index < sectionCount; index++, section += 40)
            {
                int virtualSize = BitConverter.ToInt32(image, section + 8);
                int virtualAddress = BitConverter.ToInt32(image, section + 12);
                int rawSize = BitConverter.ToInt32(image, section + 16);
                int rawAddress = BitConverter.ToInt32(image, section + 20);
                int span = Math.Max(virtualSize, rawSize);
                if (rva >= virtualAddress && rva < virtualAddress + span)
                    return rawAddress + rva - virtualAddress;
            }
            throw new InvalidOperationException($"RVA 0x{rva:X} is not mapped by the PE sections.");
        }

        private sealed class Model
        {
            internal int Peace;
            internal int StrongWalls;
            internal int[] Buildings;
        }
    }
}
