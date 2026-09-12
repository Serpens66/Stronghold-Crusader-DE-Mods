using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Xml;
using SHCDESE.Interop;

namespace BugfixesAndQoL
{
    internal static class Program
    {
        private const string ExpectedHash =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const int FindRva = 0x69D60;
        private const int ResolveRva = 0x6AF60;
        private const int DispatcherRva = 0x13F540;
        private const int DispatcherSize = 10069;
        private const int PlannerRva = 0x196280;
        private const int MovementPlannerLowFlagGateRva = 0x196464;
        private const int MovementPlannerStructureFlagGateRva = 0x19648D;
        private const int AiWallTargetingPatternRva = 0x10ECB7;
        private const int AiWallReservationRejectJumpRva = 0x10ECC3;
        private const int AiWallApproachTileGuardRva = 0x10ECE1;

        private static int failures;

        private static int Main()
        {
            TestCoopCustomLordSelectionPolicy();
            TestTrailCustomizationOwnership();
            TestTunnelPlacementDistancePolicy();
            TestTunnelPlacementDistanceIntegration();
            TestAicDropdownPolicy();
            TestAicDropdownIntegration();
            TestFriendlyMoatMovementPolicy();
            TestFriendlyMoatMovementIntegration();
            TestMovementSafetyIntegration();
            TestAiDefensePatrolPolicy();
            TestAiDefensePatrolIntegration();
            TestAiStoneReserveIntegration();
            TestAiWallTargetingIntegration();
            TestSingleBuildingPauseOverrideStore();
            TestAIResourceShortageSleepPolicy();
            TestAIResourceShortageSleepIntegration();
            TestTemporaryGateBlockagePolicy();
            TestTemporaryGateBlockageIntegration();
            TestMapFileManagerContract();
            TestClassicMapSizeReader();
            TestLobbyMapSelectionMemory();
            TestNativeContracts();
            if (failures == 0)
            {
                Console.WriteLine("BugfixesAndQoL policy and native-contract tests passed.");
                return 0;
            }
            Console.Error.WriteLine($"BugfixesAndQoL policy and native-contract tests failed: {failures}.");
            return 1;
        }

        private static void TestMapFileManagerContract()
        {
            Type[] parameterTypes =
            {
                typeof(string),
                typeof(string),
                typeof(int),
                typeof(bool)
            };
            MethodInfo method = typeof(MapFileManager).GetMethod(
                nameof(MapFileManager.GetFileInfoFromFileName),
                BindingFlags.Instance | BindingFlags.Public,
                null,
                parameterTypes,
                null);

            Check(
                method != null &&
                    method.IsPublic &&
                    !method.IsStatic &&
                    method.ReturnType == typeof(FileHeader),
                "MapFileManager exposes the public GetFileInfoFromFileName hook contract");
        }

        private static void TestCoopCustomLordSelectionPolicy()
        {
            int expectedLordType = checked((int)AILords.SK_X2 - 1);
            Check(
                CoopCustomLordSelectionPolicy.CustomPartnerLordType == expectedLordType &&
                CoopCustomLordSelectionPolicy.SharedProgressId == (ulong)expectedLordType + 1000UL,
                "Coop custom partner maps SK_X2 to the shared Vanilla progress ID");

            Check(
                CoopCustomLordSelectionPolicy.CanSelect(true, true, false, true, true, 1) &&
                !CoopCustomLordSelectionPolicy.CanSelect(false, true, false, true, true, 1) &&
                !CoopCustomLordSelectionPolicy.CanSelect(true, true, true, true, true, 1) &&
                !CoopCustomLordSelectionPolicy.CanSelect(true, true, false, true, true, 2),
                "Coop custom partner selection is gated to enabled singleplayer Coop");

            Check(
                CoopCustomLordSelectionPolicy.ShouldReplaceDefaultAiv(
                    true, false, true, true, expectedLordType, expectedLordType, 2) &&
                !CoopCustomLordSelectionPolicy.ShouldReplaceDefaultAiv(
                    true, false, true, true, expectedLordType, expectedLordType, 3) &&
                !CoopCustomLordSelectionPolicy.ShouldReplaceDefaultAiv(
                    true, false, true, false, expectedLordType, expectedLordType, 2) &&
                !CoopCustomLordSelectionPolicy.ShouldReplaceDefaultAiv(
                    false, false, true, true, expectedLordType, expectedLordType, 2) &&
                !CoopCustomLordSelectionPolicy.ShouldReplaceDefaultAiv(
                    true, true, true, true, expectedLordType, expectedLordType, 2) &&
                !CoopCustomLordSelectionPolicy.ShouldReplaceDefaultAiv(
                    true, false, false, true, expectedLordType, expectedLordType, 2) &&
                !CoopCustomLordSelectionPolicy.ShouldReplaceDefaultAiv(
                    true, false, true, true, expectedLordType - 1, expectedLordType, 2),
                "Coop custom AIV replacement is limited to the active player-2 partner");

            Check(
                CoopCustomLordSelectionPolicy.CalculatePortraitContentHeight(-1) == 450 &&
                CoopCustomLordSelectionPolicy.CalculatePortraitContentHeight(0) == 450 &&
                CoopCustomLordSelectionPolicy.CalculatePortraitContentHeight(7) == 450 &&
                CoopCustomLordSelectionPolicy.CalculatePortraitContentHeight(8) == 560,
                "Coop portrait scrolling starts with the eighth custom lord");

            Check(
                CoopCustomLordSelectionPolicy.AppendLordPower("Rat", 1) == "Rat (1)" &&
                CoopCustomLordSelectionPolicy.AppendLordPower("Rat (1)", 1) == "Rat (1)",
                "Coop lord power is appended exactly once");

            ulong sharedProgressId = CoopCustomLordSelectionPolicy.SharedProgressId;
            Check(
                CoopCustomLordSelectionPolicy.FormatHistoryName("Nox", sharedProgressId, true) ==
                    "Nox (Custom Lord)" &&
                CoopCustomLordSelectionPolicy.FormatHistoryName(
                    "Nox (Custom Lord)", sharedProgressId, true) == "Nox (Custom Lord)" &&
                CoopCustomLordSelectionPolicy.FormatHistoryName("Nox", sharedProgressId, false) == "Nox" &&
                CoopCustomLordSelectionPolicy.FormatHistoryName("Rat", 1001UL, true) == "Rat" &&
                CoopCustomLordSelectionPolicy.ShouldShowDeleteButton(true, 1001UL) &&
                !CoopCustomLordSelectionPolicy.ShouldShowDeleteButton(false, 1001UL) &&
                !CoopCustomLordSelectionPolicy.ShouldShowDeleteButton(true, 0UL),
                "Coop history marker and delete visibility are display-only and setting-gated");

            Check(
                CoopCustomLordSelectionPolicy.CanConfirmProgressDeletion(
                    1001UL, 1001UL, recordInDictionary: true, recordInOrderedList: true) &&
                !CoopCustomLordSelectionPolicy.CanConfirmProgressDeletion(
                    1001UL, 1002UL, recordInDictionary: true, recordInOrderedList: true) &&
                !CoopCustomLordSelectionPolicy.CanConfirmProgressDeletion(
                    1001UL, 1001UL, recordInDictionary: false, recordInOrderedList: true) &&
                !CoopCustomLordSelectionPolicy.CanConfirmProgressDeletion(
                    1001UL, 1001UL, recordInDictionary: true, recordInOrderedList: false),
                "Coop progress deletion rejects changed or inconsistent rows at confirmation");

            ulong[] sixOccupiedRows =
                { 1001UL, 76561198000000001UL, 1008UL, 1002UL, sharedProgressId, 1003UL, 0UL, 0UL };
            int visibleDeleteButtons = 0;
            for (int row = 0; row < sixOccupiedRows.Length; row++)
            {
                if (CoopCustomLordSelectionPolicy.ShouldShowDeleteButton(true, sixOccupiedRows[row]))
                    visibleDeleteButtons++;
            }
            Check(
                visibleDeleteButtons == 6,
                "six occupied Coop rows show exactly six delete buttons");

            const string targetXPath =
                "//n:Grid[@Width='1080' and @Height='640']/n:Grid[@Margin='25,0,0,0']";
            bool xamlContractsValid = true;
            for (int trail = 1; trail <= 4; trail++)
            {
                string patchPath = Path.Combine(
                    "Patches", "Assets", "GUI", "XAMLResources", $"FRONT_CoopTrail{trail}.xaml");
                string baselinePath = Path.Combine(
                    "..", "_inspect", "CrusaderDE-Native-Baseline", "sem", "FBCB9319",
                    "resources", "xaml", "Assets", "GUI", "XAMLResources",
                    $"FRONT_CoopTrail{trail}.xaml");
                string patchText = File.ReadAllText(patchPath);
                xamlContractsValid &= patchText.Contains("Value=\"CoopLordPortraitGrid\"") &&
                    patchText.Contains("x:Name=\"CoopCustomLordSelectionHost\"") &&
                    patchText.Contains("<Canvas />") &&
                    patchText.Contains("Property=\"Canvas.Left\"") &&
                    patchText.Contains("Property=\"Canvas.Top\"") &&
                    patchText.Contains("ItemsSource=\"{Binding Choices}\"") &&
                    patchText.Contains("Width=\"110\" Height=\"110\"") &&
                    patchText.Contains("Width=\"100\" Height=\"100\"") &&
                    patchText.Contains("Style=\"{StaticResource BTN_Image}\"") &&
                    !patchText.Contains("Type=\"Replace\"") &&
                    !patchText.Contains("EnterCommand") &&
                    !patchText.Contains("LeaveCommand") &&
                    !patchText.Contains("ToolTip") &&
                    !patchText.Contains("ToolTipService");
                xamlContractsValid &=
                    !patchText.Contains("CoopDeleteProgress") &&
                    !patchText.Contains("BugfixesAndQoL_CoopDelete") &&
                    !patchText.Contains("Command=\"{Binding MultiplayerMenuCommand}\"");

                var document = new XmlDocument();
                document.Load(baselinePath);
                var namespaces = new XmlNamespaceManager(document.NameTable);
                namespaces.AddNamespace("n", document.DocumentElement.NamespaceURI);
                namespaces.AddNamespace("x", "http://schemas.microsoft.com/winfx/2006/xaml");
                XmlNode portraitGrid = document.SelectSingleNode(targetXPath, namespaces);
                for (int row = 1; row <= 8; row++)
                {
                    xamlContractsValid &= document.SelectNodes(
                        $"//n:Grid[@Name='HostInvitePane']//n:Grid[@x:Name='Row{row}']",
                        namespaces)?.Count == 1;
                }
                int[] vanillaOrder =
                    { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 24, 20, 21, 22, 23, 25, 26, 27, 28 };
                XmlNodeList vanillaButtons = portraitGrid?.SelectNodes(
                    "./n:Button[@Command='{Binding SkirmishAIAddClickCommand}']", namespaces);
                bool vanillaOrderMatches = vanillaButtons?.Count == vanillaOrder.Length;
                for (int index = 0; vanillaOrderMatches && index < vanillaOrder.Length; index++)
                    vanillaOrderMatches &= vanillaButtons[index].Attributes?["CommandParameter"]?.Value == vanillaOrder[index].ToString();
                bool dlcVisibilityMatches =
                    portraitGrid?.SelectSingleNode("./n:Button[@CommandParameter='20' and @Visibility='{Binding NewAILordNotVisible[0]}']", namespaces) != null &&
                    portraitGrid.SelectSingleNode("./n:Button[@CommandParameter='21' and @Visibility='{Binding NewAILordNotVisible[1]}']", namespaces) != null &&
                    portraitGrid.SelectSingleNode("./n:Button[@CommandParameter='22' and @Visibility='{Binding NewAILordNotVisible[2]}']", namespaces) != null &&
                    portraitGrid.SelectSingleNode("./n:Button[@CommandParameter='23' and @Visibility='{Binding NewAILordNotVisible[3]}']", namespaces) != null &&
                    portraitGrid.SelectSingleNode("./n:Button[@CommandParameter='25' and @Visibility='{Binding NewAILordNotVisible[5]}']", namespaces) != null &&
                    portraitGrid.SelectSingleNode("./n:Button[@CommandParameter='26' and @Visibility='{Binding NewAILordNotVisible[6]}']", namespaces) != null &&
                    portraitGrid.SelectSingleNode("./n:Button[@CommandParameter='27' and @Visibility='{Binding NewAILordNotVisible[7]}']", namespaces) != null &&
                    portraitGrid.SelectSingleNode("./n:Button[@CommandParameter='28' and @Visibility='{Binding NewAILordNotVisible[8]}']", namespaces) != null;
                xamlContractsValid &= portraitGrid != null &&
                    portraitGrid.SelectNodes("./n:Image", namespaces)?.Count == 29 &&
                    vanillaOrderMatches && dlcVisibilityMatches &&
                    portraitGrid.SelectNodes(".//n:TextBlock", namespaces)?.Count == 0 &&
                    portraitGrid.SelectNodes(".//n:Button[@CommandParameter='CoopSinglePlayerBack']", namespaces)?.Count == 0 &&
                    document.SelectNodes("//n:Grid[@Width='1080' and @Height='640']/n:TextBlock[@Text='{Binding SkirmishLordRolloverName}']", namespaces)?.Count == 1 &&
                    document.SelectNodes("//n:Grid[@Width='1080' and @Height='640']/n:TextBlock[@Text='{Binding SkirmishLordRolloverDesc}']", namespaces)?.Count == 1 &&
                    document.SelectNodes("//n:Grid[@Width='1080' and @Height='640']/n:Button[@CommandParameter='CoopSinglePlayerBack']", namespaces)?.Count == 1;
            }
            Check(
                xamlContractsValid,
                "all four Coop Trail patches preserve Vanilla portraits, order, hover and DLC visibility while adding custom slots");

            string featureSource = File.ReadAllText(Path.Combine("src", "CoopCustomLordSelectionFeature.cs"));
            Check(
                featureSource.Contains("api.TryGetLordDetails(lord.lordName, out LordDetails details);") &&
                featureSource.Contains("viewModel.SkirmishLordRolloverDesc = details?.Description ?? string.Empty;") &&
                featureSource.Contains("button.MouseEnter += CustomButtonMouseEnter;") &&
                featureSource.Contains("button.MouseLeave += CustomButtonMouseLeave;") &&
                featureSource.Contains("VerticalScrollBarVisibility = ScrollBarVisibility.Auto") &&
                featureSource.Contains("int portraitIndex = VanillaLordCount + choices.Count;") &&
                featureSource.Contains("nameof(FRONT_Multiplayer.AILordEnter)") &&
                featureSource.Contains("GameAIManagerAPI.Instance.GetAICArray()") &&
                featureSource.Contains("AILords.SK_RAT") &&
                featureSource.Contains("AILords.SK_DLC4B") &&
                featureSource.Contains("lord.configs[0].lordData.lord_power_display_level") &&
                featureSource.Contains("HUD_ConfirmationPopup.ShowConfirmationMessage(") &&
                featureSource.Contains("MPConf: true") &&
                featureSource.Contains("ShowMultiplayerConfirmationOkMessage(") &&
                featureSource.Contains("MainViewModel.Instance.Show_HUD_ConfirmationMP = true;") &&
                featureSource.Contains("CoopInfoDictionaryField.GetValue(null) as IDictionary") &&
                featureSource.Contains("CoopInfoListField.GetValue(null) as IList") &&
                featureSource.Contains("ConfigSettings.SaveCoop();") &&
                featureSource.Contains("File.Delete(coopFilePath);") &&
                featureSource.Contains("FormatHistoryName(") &&
                featureSource.Contains("button.Click += DeleteProgressButtonClicked;") &&
                featureSource.Contains("button.Click -= DeleteProgressButtonClicked;") &&
                featureSource.Contains("SetCoopRowHook(") &&
                featureSource.Contains("page.TryFindResource(\"BTN_Building\") as Style") &&
                featureSource.Contains("page.TryFindResource(\"UI-Buttons L009\") as ImageSource") &&
                featureSource.Contains("page.TryFindResource(\"UI-Buttons L010\") as ImageSource") &&
                featureSource.Contains("Name = DeleteProgressButtonPrefix + (row + 1)") &&
                featureSource.Contains("Margin = new Thickness(0, 0, 130, 0)") &&
                featureSource.Contains("PropEx.SetButtonVisibility(button, Visibility.Collapsed);") &&
                featureSource.Contains("rowGrids[row].Children.Add(button);") &&
                featureSource.Contains("UpdateDeleteButtonRow(row, steamId);") &&
                featureSource.Contains("PropEx.SetButtonVisibility(button, visibility);") &&
                featureSource.Contains("RequestProgressDeletion(self, row);") &&
                !featureSource.Contains("CollectDeleteButtonsRecursive(") &&
                !featureSource.Contains("BugfixesAndQoL_CoopDelete") &&
                !featureSource.Contains("SelectVanilla") &&
                !featureSource.Contains("EnterVanilla") &&
                !featureSource.Contains("lordmeta.json") &&
                !featureSource.Contains("DependencyFreeJson"),
                "Coop hover power and progress deletion preserve Vanilla UI and use Script Extender metadata");

            string multiplayerHookSource = File.ReadAllText(
                Path.Combine("src", "SkirmishAiSelectionMemoryHook.cs"));
            Check(
                multiplayerHookSource.Contains(
                    "CoopCustomLordSelectionFeature.OnMultiplayerButtonStarting(self, param);") &&
                !multiplayerHookSource.Contains(
                    "if (CoopCustomLordSelectionFeature.OnMultiplayerButtonStarting(self, param))"),
                "Coop delete buttons do not consume or reroute Vanilla multiplayer commands");

            int initHookStart = featureSource.IndexOf(
                "private void InitCoopGameHook",
                StringComparison.Ordinal);
            int rowHookStart = featureSource.IndexOf(
                "private int[] GetCoopRowInfoHook",
                StringComparison.Ordinal);
            string initHookSource = initHookStart >= 0 && rowHookStart > initHookStart
                ? featureSource.Substring(initHookStart, rowHookStart - initHookStart)
                : string.Empty;
            Check(
                initHookSource.Contains("userName = selectedDisplayName;") &&
                initHookSource.Contains("initCoopGameOriginal(steamId, userName, coaString);") &&
                !initHookSource.Contains("FormatHistoryName("),
                "the Custom Lord history marker is not persisted to coop.cfg");
        }

        private static void TestClassicMapSizeReader()
        {
            int[] directoryTags = { 2036, 3036, 4036 };
            int[] mapSizes = { 160, 400, 800 };
            for (int index = 0; index < directoryTags.Length; index++)
            {
                using (var stream = new MemoryStream(BuildClassicMapFixture(
                    directoryTags[index], mapSizes[index])))
                {
                    Check(
                        ClassicMapSizeReader.TryRead(stream, out int actual) &&
                            actual == mapSizes[index],
                        $"classic map size reader supports directory tag {directoryTags[index]}");
                }
            }

            Check(
                ClassicMapSizeReader.ShouldPopulate(true, true, -1, "classic.map") &&
                    !ClassicMapSizeReader.ShouldPopulate(false, true, -1, "classic.map") &&
                    !ClassicMapSizeReader.ShouldPopulate(true, false, -1, "classic.map") &&
                    !ClassicMapSizeReader.ShouldPopulate(true, true, 400, "classic.map") &&
                    !ClassicMapSizeReader.ShouldPopulate(true, true, -1, "classic.sav"),
                "classic map size policy changes only missing enabled classic map metadata");

            CheckClassicMapFixtureRejected(
                BuildClassicMapFixture(3036, 400, sectionId: 1051),
                "classic map size reader rejects a missing map-size section");
            CheckClassicMapFixtureRejected(
                BuildClassicMapFixture(3036, 400, compressionFlag: 1),
                "classic map size reader rejects a compressed map-size section");
            CheckClassicMapFixtureRejected(
                BuildClassicMapFixture(3036, 400, unpackedSize: 8),
                "classic map size reader rejects a wrongly sized map-size section");
            CheckClassicMapFixtureRejected(
                BuildClassicMapFixture(3036, 400, relativeOffset: 4),
                "classic map size reader rejects a map-size offset outside the payload");
            CheckClassicMapFixtureRejected(
                BuildClassicMapFixture(3036, 0),
                "classic map size reader rejects a zero map size");
            CheckClassicMapFixtureRejected(
                BuildClassicMapFixture(3036, SHCDESE.API.GameTileManagerAPI.MAX_WIDTH + 2),
                "classic map size reader rejects a map size above the engine maximum");

            byte[] truncated = BuildClassicMapFixture(3036, 400);
            Array.Resize(ref truncated, truncated.Length - 1);
            CheckClassicMapFixtureRejected(
                truncated,
                "classic map size reader rejects a truncated payload");

            string samplePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData",
                "LocalLow",
                "Firefly Studios",
                "Stronghold Crusader Definitive Edition",
                "Maps",
                "Brother's Strife.map");
            if (File.Exists(samplePath))
            {
                Check(
                    ClassicMapSizeReader.TryRead(samplePath, out int sampleSize) && sampleSize == 400,
                    "Brother's Strife exposes its HD map size through section 1050");
            }
        }

        private static void TestLobbyMapSelectionMemory()
        {
            Check(
                LobbyMapSelectionPolicy.IsFeatureEnabled(true, true) &&
                !LobbyMapSelectionPolicy.IsFeatureEnabled(false, true) &&
                !LobbyMapSelectionPolicy.IsFeatureEnabled(true, false),
                "lobby map memory is gated by the existing enhancement setting");

            int[] validColumns = { 0, 1, 2, 3, 4, 5, 10, 11, 12, 13, 14, 15, 16 };
            foreach (int column in validColumns)
            {
                Check(
                    LobbyMapSelectionPolicy.IsValidSortColumn(column),
                    $"lobby map memory accepts sort column {column}");
            }
            Check(
                !LobbyMapSelectionPolicy.IsValidSortColumn(-1) &&
                !LobbyMapSelectionPolicy.IsValidSortColumn(6) &&
                !LobbyMapSelectionPolicy.IsValidSortColumn(9) &&
                !LobbyMapSelectionPolicy.IsValidSortColumn(17),
                "lobby map memory rejects unsupported sort columns");

            Check(
                LobbyMapSelectionPolicy.HasMapAuthority(true, false, false) &&
                LobbyMapSelectionPolicy.HasMapAuthority(false, true, true) &&
                !LobbyMapSelectionPolicy.HasMapAuthority(false, true, false) &&
                !LobbyMapSelectionPolicy.HasMapAuthority(false, false, false),
                "lobby map memory grants map authority only to local modes and multiplayer hosts");

            var remembered = new LobbyMapIdentity
            {
                Origin = LobbyMapSelectionPolicy.UserOrigin,
                FilePath = @"C:\Maps\Remembered.map",
                FileName = "Remembered",
            };
            object exact = new object();
            object sameNameElsewhere = new object();
            var candidates = new[]
            {
                new LobbyMapCandidate(
                    LobbyMapSelectionPolicy.UserOrigin,
                    @"C:\Other\Remembered.map",
                    "Remembered",
                    sameNameElsewhere),
                new LobbyMapCandidate(
                    LobbyMapSelectionPolicy.UserOrigin,
                    @"C:\Maps\Remembered.map",
                    "Remembered",
                    exact),
            };
            Check(
                ReferenceEquals(LobbyMapSelectionPolicy.FindMatch(remembered, candidates), exact),
                "lobby map memory prefers the exact origin and path match");

            remembered.FilePath = @"D:\Moved\Remembered.map";
            Check(
                LobbyMapSelectionPolicy.FindMatch(remembered, candidates) == null,
                "lobby map memory rejects an ambiguous origin and name fallback");
            Check(
                ReferenceEquals(
                    LobbyMapSelectionPolicy.FindMatch(remembered, new[] { candidates[0] }),
                    sameNameElsewhere),
                "lobby map memory accepts one unambiguous origin and name fallback");

            var snapshot = new LobbyMapSelectionSnapshot
            {
                SortColumn = 16,
                SortAscending = false,
                Map = new LobbyMapIdentity
                {
                    Origin = LobbyMapSelectionPolicy.WorkshopOrigin,
                    FilePath = @"C:\Workshop\Example.map",
                    FileName = "Example",
                },
            };
            string json = LobbyMapSelectionCodec.Serialize(snapshot);
            Check(
                LobbyMapSelectionCodec.TryDeserialize(
                    json,
                    out LobbyMapSelectionSnapshot decoded,
                    out _) &&
                decoded.SortColumn == 16 &&
                !decoded.SortAscending &&
                decoded.Map?.Origin == LobbyMapSelectionPolicy.WorkshopOrigin &&
                decoded.Map?.FileName == "Example",
                "lobby map memory JSON roundtrip preserves map and special player sort mode");
            Check(
                !LobbyMapSelectionCodec.TryDeserialize(
                    "{\"version\":1,\"sort\":{\"column\":7,\"ascending\":true}}",
                    out _,
                    out _),
                "lobby map memory rejects invalid persisted sort data");
            Check(
                !LobbyMapSelectionCodec.TryDeserialize(
                    "{\"version\":2,\"sort\":{\"column\":0,\"ascending\":true}}",
                    out _,
                    out _) &&
                !LobbyMapSelectionCodec.TryDeserialize(
                    "{\"version\":1.0,\"sort\":{\"column\":0,\"ascending\":true}}",
                    out _,
                    out _),
                "lobby map memory rejects unknown schemas and non-integral schema values");

            string directory = Path.Combine(
                Path.GetTempPath(),
                "BugfixesAndQoL-LobbyMapMemory-" + Guid.NewGuid().ToString("N"));
            string storePath = Path.Combine(directory, "LobbyMapSelectionMemory.json");
            try
            {
                var store = new LobbyMapSelectionStore(null, storePath);
                store.RememberSort(snapshot.SortColumn, snapshot.SortAscending);
                store.RememberMap(snapshot.Map);
                LobbyMapSelectionSnapshot persisted =
                    new LobbyMapSelectionStore(null, storePath).Current;
                Check(
                    persisted.SortColumn == snapshot.SortColumn &&
                    persisted.SortAscending == snapshot.SortAscending &&
                    persisted.Map?.FileName == snapshot.Map.FileName,
                    "lobby map memory store persists state atomically across instances");

                File.WriteAllText(storePath, "not json");
                LobbyMapSelectionSnapshot malformed =
                    new LobbyMapSelectionStore(null, storePath).Current;
                Check(
                    malformed.SortColumn == 0 && malformed.SortAscending && malformed.Map == null,
                    "lobby map memory store ignores malformed data");

                File.WriteAllBytes(storePath, new byte[64 * 1024 + 1]);
                LobbyMapSelectionSnapshot oversized =
                    new LobbyMapSelectionStore(null, storePath).Current;
                Check(
                    oversized.SortColumn == 0 && oversized.SortAscending && oversized.Map == null,
                    "lobby map memory store ignores oversized data");
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        private static void CheckClassicMapFixtureRejected(byte[] bytes, string name)
        {
            using (var stream = new MemoryStream(bytes))
                Check(!ClassicMapSizeReader.TryRead(stream, out _), name);
        }

        private static byte[] BuildClassicMapFixture(
            int directoryTag,
            int mapSize,
            int sectionId = 1050,
            int compressionFlag = 0,
            int unpackedSize = sizeof(int),
            int storedSize = sizeof(int),
            int relativeOffset = 0)
        {
            int capacity = (directoryTag - 36) / 20;
            byte[] directoryBody = new byte[directoryTag - sizeof(int)];
            WriteInt32(directoryBody, 0, sizeof(int));
            WriteInt32(directoryBody, 4, 1);
            WriteInt32(directoryBody, 8, 172);
            int arraysOffset = 28;
            WriteInt32(directoryBody, arraysOffset, unpackedSize);
            WriteInt32(directoryBody, arraysOffset + capacity * sizeof(int), storedSize);
            WriteInt32(directoryBody, arraysOffset + capacity * sizeof(int) * 2, sectionId);
            WriteInt32(directoryBody, arraysOffset + capacity * sizeof(int) * 3, compressionFlag);
            WriteInt32(directoryBody, arraysOffset + capacity * sizeof(int) * 4, relativeOffset);

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(-1);
                for (int blockIndex = 0; blockIndex < 5; blockIndex++)
                    writer.Write(0);
                writer.Write(80);
                writer.Write(new byte[80]);
                writer.Write(0);
                writer.Write(directoryTag);
                writer.Write(directoryBody);
                writer.Write(mapSize);
                return stream.ToArray();
            }
        }

        private static void WriteInt32(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        private static void TestTrailCustomizationOwnership()
        {
            int refreshCalls = 0;
            Check(!TrailCustomizationProviderHostApi.RegisterProvider(
                    "CustomCustomTrail_Serp",
                    TrailCustomizationProviderHostApi.ApiVersion,
                    () => true,
                    () => true,
                    () => true),
                "Trail customization keeps provider standalone ownership until the host is attached");
            TrailCustomizationProviderHostApi.Attach(() => refreshCalls++);
            Check(!TrailCustomizationProviderHostApi.RegisterProvider(
                    "", 1, () => true, () => true, () => true),
                "Trail customization rejects an invalid provider identity");
            Check(!TrailCustomizationProviderHostApi.RegisterProvider(
                    "CustomCustomTrail_Serp", 2, () => true, () => true, () => true),
                "Trail customization rejects an incompatible provider API");

            bool enabled = true;
            int customCalls = 0;
            int coopCalls = 0;
            Check(TrailCustomizationProviderHostApi.RegisterProvider(
                    "CustomCustomTrail_Serp",
                    TrailCustomizationProviderHostApi.ApiVersion,
                    () => enabled,
                    () => { customCalls++; return true; },
                    () => { coopCalls++; return true; }),
                "Trail customization accepts the compatible CustomCustomTrail provider");
            Check(refreshCalls == 2,
                "Trail customization refreshes visibility after attach and provider registration");
            Check(TrailCustomizationProviderHostApi.IsProviderEnabled() &&
                    TrailCustomizationProviderHostApi.TryCustomizeCustomTrail(out bool customActive) &&
                    customActive && customCalls == 1 &&
                    TrailCustomizationProviderHostApi.TryCustomizeCoopTrail(out bool coopActive) &&
                    coopActive && coopCalls == 1,
                "active provider owns each Customize transition exactly once");
            Check(!TrailCustomizationProviderHostApi.RegisterProvider(
                    "UnexpectedTrailProvider",
                    TrailCustomizationProviderHostApi.ApiVersion,
                    () => true,
                    () => true,
                    () => true),
                "Trail customization rejects an unexpected competing provider");
            int rejectedCalls = 0;
            Check(TrailCustomizationProviderHostApi.RegisterProvider(
                    "CustomCustomTrail_Serp",
                    TrailCustomizationProviderHostApi.ApiVersion,
                    () => enabled,
                    () => { rejectedCalls++; return false; },
                    () => { rejectedCalls++; return false; }) &&
                    !TrailCustomizationProviderHostApi.TryCustomizeCustomTrail(out bool rejectedActive) &&
                    rejectedActive && rejectedCalls == 1,
                "active provider rejection remains fail-closed without a second transition");
            enabled = false;
            Check(!TrailCustomizationProviderHostApi.TryCustomizeCustomTrail(out bool inactive) &&
                    !inactive && customCalls == 1,
                "disabled provider yields to the BugfixesAndQoL standalone fallback");

            string projectDirectory = FindProjectDirectory();
            string feature = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "TrailCustomizationFeature.cs"));
            string viewModel = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "BugfixesAndQoLViewModel.cs"));
            string xaml = File.ReadAllText(Path.Combine(
                projectDirectory, "Override", "ScriptExtenderUI", "BugfixesAndQoLSettings.xaml"));
            string runtime = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "BugfixesAndQoLRuntime.cs"));
            string sharedGameMode = File.ReadAllText(Path.Combine(
                Directory.GetParent(projectDirectory).FullName, "Shared", "GameModeHelper.cs"));
            Check(feature.Contains("Name = \"SharedTrailCustomize\"") &&
                    feature.Contains("foreach (UIElement child in host.Children)") &&
                    feature.Contains("TryCustomizeCustomTrail(out bool providerActive)") &&
                    feature.Contains("TryCustomizeCoopTrail(out bool providerActive)") &&
                    feature.Contains("args.SenderSteamId.Value != host.Value") &&
                    feature.Contains("TrailCustomizationLaunchOriginApi.Clear();") &&
                    feature.Contains("throw;"),
                "Trail customization owns idempotent buttons and authenticated provider-aware transitions");
            Check(viewModel.Contains("[SyncHostOnly]") &&
                    viewModel.Contains("public bool EnableTrailCustomizationButtons") &&
                    xaml.Contains("EnableTrailCustomizationButtons, Mode=TwoWay"),
                "Trail customization exposes the default-enabled host setting");
            Check(runtime.IndexOf("multiplayerFeatureGate.CaptureMapMode", StringComparison.Ordinal) <
                    runtime.IndexOf("TrailCustomizationLaunchOriginApi.MarkMapStarted", StringComparison.Ordinal) &&
                    sharedGameMode.Contains("bool hasLaunchPending = TryReadStaticBool") &&
                    sharedGameMode.Contains("if (hasActive)"),
                "launch origin is captured before cleanup and conflicting providers fail closed");
        }

        private static void TestTunnelPlacementDistancePolicy()
        {
            Check(TunnelPlacementDistancePolicy.IsTunnelMapper(eMappers.MAPPER_TUNNEL) &&
                    TunnelPlacementDistancePolicy.IsTunnelMapper(eMappers.MAPPER_TUNNEL_CONSTRUCTION) &&
                    !TunnelPlacementDistancePolicy.IsTunnelMapper(eMappers.MAPPER_IRON_MINE) &&
                    !TunnelPlacementDistancePolicy.IsTunnelMapper(eMappers.MAPPER_CATAPULT),
                "placement clearance retains the tunnel-only structure rule");
            Check(TunnelPlacementDistancePolicy.IsPlaceableBuildingMapper(eMappers.MAPPER_IRON_MINE, 4) &&
                    TunnelPlacementDistancePolicy.IsPlaceableBuildingMapper(eMappers.MAPPER_TUNNEL, 3) &&
                    TunnelPlacementDistancePolicy.IsPlaceableBuildingMapper(eMappers.MAPPER_TUNNEL_CONSTRUCTION, 3) &&
                    TunnelPlacementDistancePolicy.IsPlaceableBuildingMapper(eMappers.MAPPER_CATAPULT, 3) &&
                    TunnelPlacementDistancePolicy.IsPlaceableBuildingMapper(eMappers.MAPPER_TREBUCHET, 3) &&
                    TunnelPlacementDistancePolicy.IsPlaceableBuildingMapper(eMappers.MAPPER_SIEGE_TOWER, 3) &&
                    TunnelPlacementDistancePolicy.IsPlaceableBuildingMapper(eMappers.MAPPER_BATTERING_RAM, 3) &&
                    TunnelPlacementDistancePolicy.IsPlaceableBuildingMapper(eMappers.MAPPER_PORTABLE_SHIELD, 3) &&
                    TunnelPlacementDistancePolicy.IsPlaceableBuildingMapper(eMappers.MAPPER_ARAB_BALLISTA, 3),
                "placement clearance includes ordinary buildings, tunnels and every siege tent");
            Check(!TunnelPlacementDistancePolicy.IsPlaceableBuildingMapper(eMappers.MAPPER_WALL, 2) &&
                    !TunnelPlacementDistancePolicy.IsPlaceableBuildingMapper(eMappers.MAPPER_MOAT, 2) &&
                    !TunnelPlacementDistancePolicy.IsPlaceableBuildingMapper(eMappers.MAPPER_CAMP_FIRE, 1) &&
                    !TunnelPlacementDistancePolicy.IsPlaceableBuildingMapper(eMappers.MAPPER_PLACE_ASSEMBLY_POINT1, 0) &&
                    !TunnelPlacementDistancePolicy.IsPlaceableBuildingMapper(eMappers.MAPPER_PEOPLE_ARCHERS, 0),
                "placement clearance excludes walls, moats, map objects, markers and units");
            Check(TunnelPlacementDistancePolicy.ShouldApply(
                        true, true, false, false, eMappers.MAPPER_IRON_MINE, 4) &&
                    TunnelPlacementDistancePolicy.ShouldApply(
                        true, true, false, false, eMappers.MAPPER_TUNNEL, 3) &&
                    TunnelPlacementDistancePolicy.ShouldApply(
                        true, true, false, false, eMappers.MAPPER_CATAPULT, 3),
                "placement clearance accepts human ordinary, tunnel and siege buildings");
            Check(!TunnelPlacementDistancePolicy.ShouldApply(
                        false, true, false, false, eMappers.MAPPER_TUNNEL, 3) &&
                    !TunnelPlacementDistancePolicy.ShouldApply(
                        true, false, false, false, eMappers.MAPPER_TUNNEL, 3) &&
                    !TunnelPlacementDistancePolicy.ShouldApply(
                        true, true, true, false, eMappers.MAPPER_TUNNEL, 3) &&
                    !TunnelPlacementDistancePolicy.ShouldApply(
                        true, true, false, true, eMappers.MAPPER_TUNNEL, 3) &&
                    !TunnelPlacementDistancePolicy.ShouldApply(
                        true, true, false, false, eMappers.MAPPER_WALL, 2) &&
                    !TunnelPlacementDistancePolicy.ShouldApply(
                        true, true, false, false, eMappers.MAPPER_TUNNEL, 0),
                "placement clearance preserves disabled, editor, AI and non-building placement");

            var visited = new HashSet<string>();
            bool emptyRingBlocked = TunnelPlacementDistancePolicy.HasHostileOuterRingTile(
                10,
                20,
                3,
                (x, y) => true,
                (x, y) =>
                {
                    visited.Add(x + "," + y);
                    return false;
                });
            Check(!emptyRingBlocked && visited.Count == 16 &&
                    visited.Contains("9,19") && visited.Contains("13,19") &&
                    visited.Contains("9,23") && visited.Contains("13,23") &&
                    visited.Contains("11,19") && visited.Contains("9,21") &&
                    visited.Contains("13,21") && visited.Contains("11,23") &&
                    !visited.Contains("10,20") && !visited.Contains("12,22"),
                "tunnel distance policy scans the complete 16-tile outer ring only");

            bool everyRingPositionDetected = true;
            foreach (string hostileCoordinate in visited)
            {
                bool detected = TunnelPlacementDistancePolicy.HasHostileOuterRingTile(
                    10,
                    20,
                    3,
                    (x, y) => true,
                    (x, y) => x + "," + y == hostileCoordinate);
                everyRingPositionDetected &= detected;
            }
            Check(everyRingPositionDetected,
                "placement clearance detects hostile tiles on every side and corner");

            bool allFootprintSizesCorrect = true;
            foreach (int footprintSize in new[] { 1, 2, 3, 10 })
            {
                int inspected = 0;
                bool blocked = TunnelPlacementDistancePolicy.HasHostileOuterRingTile(
                    20, 30, footprintSize, (x, y) => true,
                    (x, y) => { inspected++; return false; });
                allFootprintSizesCorrect &= !blocked && inspected == 4 * footprintSize + 4;
            }
            Check(allFootprintSizesCorrect,
                "placement clearance scans the complete outer ring for varied building sizes");

            int inspectedInsideTiles = 0;
            bool outOfBoundsBlocked = TunnelPlacementDistancePolicy.HasHostileOuterRingTile(
                0,
                0,
                3,
                (x, y) => x >= 0 && y >= 0,
                (x, y) =>
                {
                    inspectedInsideTiles++;
                    return false;
                });
            Check(!outOfBoundsBlocked && inspectedInsideTiles == 7,
                "tunnel distance policy ignores ring coordinates outside the map");
            Check(TunnelPlacementDistancePolicy.WallOwnerToGamePlayerId(0) == 1 &&
                    TunnelPlacementDistancePolicy.WallOwnerToGamePlayerId(7) == 8,
                "wall ownership is converted exactly once from zero-based to game player ID");
            Check(!TunnelPlacementDistancePolicy.IsHostileOwner(2, 2, (a, b) => false) &&
                    !TunnelPlacementDistancePolicy.IsHostileOwner(2, 3, (a, b) => true) &&
                    TunnelPlacementDistancePolicy.IsHostileOwner(2, 3, (a, b) => false),
                "own and allied owners remain allowed while enemy owners block");

            Func<int, bool> validPlayer = id => id >= 1 && id <= 8;
            Check(TunnelPlacementDistancePolicy.TryResolveCompletedMoatOwner(
                    500, 12, 20, 500, 3, validPlayer, out int moatOwner) && moatOwner == 3,
                "completed moat ownership preserves the one-based record owner");
            Check(!TunnelPlacementDistancePolicy.TryResolveCompletedMoatOwner(
                        500, 0, 20, 500, 3, validPlayer, out _) &&
                    !TunnelPlacementDistancePolicy.TryResolveCompletedMoatOwner(
                        500, 20, 20, 500, 3, validPlayer, out _) &&
                    !TunnelPlacementDistancePolicy.TryResolveCompletedMoatOwner(
                        500, 12, 64001, 500, 3, validPlayer, out _) &&
                    !TunnelPlacementDistancePolicy.TryResolveCompletedMoatOwner(
                        500, 12, 20, 501, 3, validPlayer, out _) &&
                    !TunnelPlacementDistancePolicy.TryResolveCompletedMoatOwner(
                        500, 12, 20, 500, 9, validPlayer, out _),
                "completed moat ownership rejects invalid IDs, counts, tile links and owners");
        }

        private static void TestTunnelPlacementDistanceIntegration()
        {
            string projectDirectory = FindProjectDirectory();
            string feature = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "TunnelPlacementDistanceFeature.cs"));
            string runtime = File.ReadAllText(Path.Combine(projectDirectory, "src", "BugfixesAndQoLRuntime.cs"));
            string viewModel = File.ReadAllText(Path.Combine(projectDirectory, "src", "BugfixesAndQoLViewModel.cs"));
            string xaml = File.ReadAllText(Path.Combine(
                projectDirectory, "Override", "ScriptExtenderUI", "BugfixesAndQoLSettings.xaml"));

            Check(feature.Contains("BuildingR3EventHooks.OnPlacementValidation") &&
                    feature.Contains("EventHookPhase.Pre") &&
                    feature.Contains("GetTileBuildingId(tileId)") &&
                    feature.Contains("TryGetBuildingById(") &&
                    feature.Contains("building->r_AliveState == AliveState.IsAlive") &&
                    feature.Contains("building->r_PlayerIdOwner") &&
                    feature.Contains("TilePropertyFlag.IsWall") &&
                    feature.Contains("TilePropertyFlag.IsMoat") &&
                    feature.Contains("GetTilePlayerOwnerId(tileId)") &&
                    feature.Contains("MoatIdGridOffset = 0x1EA23F0") &&
                    feature.Contains("MoatRecordArrayOffset = 0x1F3EE30") &&
                    feature.Contains("MoatRecordCountOffset = 0x2038E30") &&
                    feature.Contains("MoatRecordSize = 0x10") &&
                    feature.Contains("MoatRecordOwnerOffset = 0x0C") &&
                    feature.Contains("recordTileId") &&
                    feature.Contains("fixedNativeLayoutValidated") &&
                    !feature.Contains("TilePropertyFlag.PlannedMoat") &&
                    feature.Contains("players.IsPlayerAlliedTo"),
                "placement clearance uses public placement APIs and the validated moat record contract");
            Check(feature.Contains("args.CustomValidationRules = true;") &&
                    feature.Contains("args.ForceBlockPlacementState = true;") &&
                    !feature.Contains("args.CustomValidationRules = false;") &&
                    !feature.Contains("args.ForceBlockPlacementState = false;"),
                "tunnel distance feature only adds a placement rejection");
            Check(feature.IndexOf("if (args.PlayerId == 0)", StringComparison.Ordinal) >= 0 &&
                    feature.IndexOf("if (args.PlayerId == 0)", StringComparison.Ordinal) <
                    feature.IndexOf("if (!players.IsPlayerIdValid(args.PlayerId))", StringComparison.Ordinal) &&
                    feature.Contains("Building placement-clearance validation ignored invalid player ID"),
                "placement clearance silently ignores Nature while retaining diagnostics for other invalid player IDs");
            Check(!feature.Contains("NativePatternResolver") &&
                    !feature.Contains("GetDelegateForFunctionPointer") &&
                    !feature.Contains("ResolveUnique") &&
                    !feature.Contains("buildingId - 1"),
                "placement clearance has no extra native hook and preserves one-based IDs");
            Check(runtime.Contains("new TunnelPlacementDistanceFeature(log, settings)") &&
                    runtime.Contains("tunnelPlacementDistanceFeature.Initialize") &&
                    runtime.Contains("tunnelPlacementDistanceFeature.SetFixedNativeLayoutValidated") &&
                    runtime.Contains("tunnelPlacementDistanceFeature.Dispose()"),
                "placement clearance is registered persistently and receives hash validation");
            Check(viewModel.Contains("[SyncHostOnly]") &&
                    viewModel.Contains("public bool EnableTunnelPlacementDistanceFix") &&
                    viewModel.Contains("enableTunnelPlacementDistanceFix = true"),
                "tunnel distance fix is an enabled-by-default synchronized host setting");
            Check(xaml.Contains("EnableTunnelPlacementDistanceFix, Mode=TwoWay") &&
                    xaml.Contains("bugfixes.enable-tunnel-placement-distance-fix"),
                "tunnel distance fix is exposed in the settings UI");
        }

        private static void TestFriendlyMoatMovementPolicy()
        {
            Check(FriendlyMoatMovementPolicy.DefaultMode == 0,
                "Off is the default mode while the feature remains experimental");
            Check(FriendlyMoatMovementPolicy.Normalize(0) == 0 &&
                    FriendlyMoatMovementPolicy.Normalize(1) == 1 &&
                    FriendlyMoatMovementPolicy.Normalize(2) == 2,
                "all three public mode values are preserved");
            Check(FriendlyMoatMovementPolicy.Normalize(-1) == 0 &&
                    FriendlyMoatMovementPolicy.Normalize(3) == 0 &&
                    FriendlyMoatMovementPolicy.Normalize(int.MaxValue) == 0,
                "invalid friendly-moat modes fail closed to Off");
            Check(FriendlyMoatMovementPolicy.ToSliderValue(0) == 0 &&
                    FriendlyMoatMovementPolicy.ToSliderValue(2) == 1 &&
                    FriendlyMoatMovementPolicy.ToSliderValue(1) == 2,
                "friendly-moat slider is ordered Off, Fast, Precise without changing persisted values");
            Check(FriendlyMoatMovementPolicy.FromSliderValue(0) == 0 &&
                    FriendlyMoatMovementPolicy.FromSliderValue(1) == 2 &&
                    FriendlyMoatMovementPolicy.FromSliderValue(2) == 1 &&
                    FriendlyMoatMovementPolicy.FromSliderValue(-1) == 0 &&
                    FriendlyMoatMovementPolicy.FromSliderValue(3) == 0,
                "friendly-moat slider maps back to stable modes and fails closed");
        }

        private static void TestAicDropdownPolicy()
        {
            Check(AicDropdownPolicy.Compare(
                    true, "Default", false, 0,
                    false, "Alpha", false, 1) < 0,
                "AIC dropdown pins Default before custom configurations");
            Check(AicDropdownPolicy.Compare(
                    false, "Alpha", true, 9,
                    false, "beta", false, 1) < 0,
                "AIC dropdown sorts custom configurations by name");
            Check(AicDropdownPolicy.Compare(
                    false, "Duplicate", false, 8,
                    false, "duplicate", true, 2) < 0 &&
                    AicDropdownPolicy.Compare(
                        false, "Duplicate", false, 2,
                        false, "duplicate", false, 8) < 0,
                "AIC dropdown uses origin and power as deterministic name tie-breakers");
            Check(AicDropdownPolicy.FormatDisplayName("Rat strong", 8, false, "Default") ==
                    "Rat strong (8)" &&
                    AicDropdownPolicy.FormatDisplayName(null, 0, true, "Default") == "Default",
                "AIC dropdown formats custom power and Default labels");
            Check(AicDropdownPolicy.Matches("Rat strong", 8, false, "Default", "rat") &&
                    AicDropdownPolicy.Matches("Rat strong", 8, false, "Default", "8") &&
                    AicDropdownPolicy.Matches(null, 0, true, "Default", "fault") &&
                    !AicDropdownPolicy.Matches("Rat strong", 8, false, "Default", "snake"),
                "AIC dropdown search matches names, power, and Default without changing case semantics");
            Check(AicDropdownPolicy.GetOrigin(true, true) == AicDropdownOrigin.Default &&
                    AicDropdownPolicy.GetOrigin(false, false) == AicDropdownOrigin.Local &&
                    AicDropdownPolicy.GetOrigin(false, true) == AicDropdownOrigin.Workshop,
                "AIC dropdown resolves Default, local, and Workshop icons explicitly");
        }

        private static void TestAicDropdownIntegration()
        {
            string projectDirectory = FindProjectDirectory();
            string hook = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "AiCastleSettingsListEnhancementHook.cs"));
            string xaml = File.ReadAllText(Path.Combine(
                projectDirectory, "Patches", "Assets", "GUI", "XAMLResources",
                "FRONT_Multiplayer_AISettings.xaml"));
            string english = File.ReadAllText(Path.Combine(projectDirectory, "Locales", "en-US.txt"));
            string german = File.ReadAllText(Path.Combine(projectDirectory, "Locales", "de-DE.txt"));

            string[] requiredControls =
            {
                "BugfixesAndQoLAicDropdownHost",
                "BugfixesAndQoLAicDropdownSelector",
                "BugfixesAndQoLAicDropdownOpenSurface",
                "BugfixesAndQoLAicDropdownPopup",
                "BugfixesAndQoLAicDropdownSearchBox",
                "BugfixesAndQoLAicDropdownResults"
            };
            foreach (string control in requiredControls)
            {
                Check(xaml.Contains("x:Name=\"" + control + "\""),
                    $"AIC dropdown control '{control}' is present");
            }

            Check(xaml.Contains("ItemsSource=\"{Binding AicDropdownEntries}\"") &&
                    xaml.Contains("MaxHeight=\"390\"") &&
                    xaml.Contains("Source=\"{Binding Icon}\"") &&
                    xaml.Contains("ToolTip=\"{Binding OriginHelp}\"") &&
                    xaml.Contains("Text=\"{Binding DisplayText}\""),
                "AIC dropdown exposes searchable, scrollable icon and power rows");
            Check(xaml.Contains("Margin=\"120,40,0,0\"") &&
                    xaml.Contains("Margin=\"240,40,0,0\"") &&
                    xaml.Contains("Margin=\"120,70,0,0\"") &&
                    xaml.Contains("Margin=\"240,70,0,0\""),
                "rotation controls use the compact two-row layout");
            Check(xaml.Contains("<Grid Width=\"340\" Height=\"34\" Margin=\"-40,289,-100,-100\"") &&
                    xaml.Contains("BugfixesAndQoLAivPresetButtonHost\" Width=\"180\"") &&
                    xaml.Contains("<Viewbox Width=\"150\" Height=\"34\" Margin=\"190,0,0,0\""),
                "Save/Load keeps its width and sits ten units left of Clear");
            Check(hook.Contains("info.builtInLord = true;") &&
                    hook.Contains("info.lordConfig = null;") &&
                    hook.Contains("info.builtInLord = false;") &&
                    hook.Contains("selectionLoaded?.Invoke(info);") &&
                    hook.Contains("aicListControl.SelectionChanged += AicListSelectionChanged;"),
                "AIC dropdown updates Vanilla state, selection memory, and the legacy list");
            Check(english.Contains("BugfixesAndQoL.AicDropdownLocal=Local") &&
                    german.Contains("BugfixesAndQoL.AicDropdownLocal=Lokal") &&
                    english.Contains("BugfixesAndQoL.AicDropdownSearchPlaceholder=Search AICs") &&
                    german.Contains("BugfixesAndQoL.AicDropdownSearchPlaceholder=AICs durchsuchen"),
                "AIC dropdown labels have English fallbacks and German translations");
        }

        private static void TestFriendlyMoatMovementIntegration()
        {
            string projectDirectory = FindProjectDirectory();
            string runtime = File.ReadAllText(Path.Combine(projectDirectory, "src", "BugfixesAndQoLRuntime.cs"));
            string moatWork = File.ReadAllText(Path.Combine(projectDirectory, "src", "MoatWorkTargetSelection.cs"));
            string viewModel = File.ReadAllText(Path.Combine(projectDirectory, "src", "BugfixesAndQoLViewModel.cs"));
            string xaml = File.ReadAllText(Path.Combine(
                projectDirectory, "Override", "ScriptExtenderUI", "BugfixesAndQoLSettings.xaml"));
            string english = File.ReadAllText(Path.Combine(projectDirectory, "Locales", "en-US.txt"));
            string german = File.ReadAllText(Path.Combine(projectDirectory, "Locales", "de-DE.txt"));
            string plugin = File.ReadAllText(Path.Combine(projectDirectory, "src", "BugfixesAndQoLPlugin.cs"));
            string aiSettingsPatch = File.ReadAllText(Path.Combine(
                projectDirectory, "Patches", "Assets", "GUI", "XAMLResources", "FRONT_Multiplayer_AISettings.xaml"));
            string troopPatch = File.ReadAllText(Path.Combine(
                projectDirectory, "Patches", "Assets", "GUI", "XAMLResources", "HUD_Troops.xaml"));
            Check(runtime.Contains("new FriendlyMoatMovementRuntime(") &&
                    runtime.Contains("friendlyMoatMovementRuntime?.Dispose()"),
                "integrated runtime participates in native initialization and final disposal");
            Check(!aiSettingsPatch.Contains("<Attribute Name=") &&
                    aiSettingsPatch.Contains("AttributeName=\"Width\" Value=\"100\"") &&
                    aiSettingsPatch.Contains("AttributeName=\"Margin\" Value=\"0,0,20,20\"") &&
                    aiSettingsPatch.Contains("AttributeName=\"HorizontalAlignment\" Value=\"Right\"") &&
                    troopPatch.Contains("AttributeName=\"bugfixes:TroopHudMiddleClickBehavior.IsEnabled\"") &&
                    !troopPatch.Contains("AttributeName=\"{clr-namespace:"),
                "XAML SetAttribute operations use the audited Script Extender patch contract");
            Check(moatWork.Contains("settings.EnableMod && settings.EnableImprovedMoatFilling") &&
                    moatWork.Contains("relationshipMode == 1 && !friendlyMovementEnabled") &&
                    moatWork.Contains("if (!ExtensionsEnabled)") &&
                    !moatWork.Contains("RegisterImprovedMoatFillingProvider"),
                "hostile filling remains independent while Off blocks every friendly moat work route");
            Check(viewModel.Contains("[SyncHostOnly]") &&
                    viewModel.Contains("public int FriendlyMoatMovementMode") &&
                    viewModel.Contains("FriendlyMoatMovementPolicy.DefaultMode"),
                "friendly moat movement is a default-required synchronized host setting");
            Check(viewModel.Contains("public int FriendlyMoatMovementSliderValue") &&
                    viewModel.Contains("FriendlyMoatMovementModeValueText") &&
                    !viewModel.Contains("FriendlyMoatMovementModeOptions") &&
                    !viewModel.Contains("FriendlyMoatMovementModeIndex"),
                "friendly moat movement exposes the ordered slider adapter and value label");
            Check(xaml.Contains("Value=\"{Binding FriendlyMoatMovementSliderValue, Mode=TwoWay}\"") &&
                    xaml.Contains("Text=\"{Binding FriendlyMoatMovementModeValueText}\"") &&
                    !xaml.Contains("ItemsSource=\"{Binding FriendlyMoatMovementModeOptions}\""),
                "friendly moat movement uses the standard three-position slider layout");
            Check(english.Contains("FriendlyMoatMovementModeHelp=Experimental:") &&
                    english.Contains("Precise (Exact) can cause noticeable lag when commanding large groups") &&
                    german.Contains("FriendlyMoatMovementModeHelp=Experiementell:") &&
                    german.Contains("kann aber beim Kommandieren großer Gruppen spürbare Lags verursachen"),
                "friendly moat tooltips start with the experimental warning and mention precise-mode group lag");
            Check(plugin.Contains("[BepInIncompatibility(LegacyMoveMoatGuid)]"),
                "legacy standalone plugin is explicitly incompatible");
        }

        private static void TestAiDefensePatrolPolicy()
        {
            Check(AiDefensePatrolPolicy.NeedsCastleDefender(19, 20),
                "AI defense patrol detects wall-defense underfill");
            Check(!AiDefensePatrolPolicy.NeedsCastleDefender(20, 20) &&
                    !AiDefensePatrolPolicy.NeedsCastleDefender(30, 20),
                "AI defense patrol preserves patrol assignment after the wall quota is met");
            Check(!AiDefensePatrolPolicy.NeedsCastleDefender(0, 0),
                "AI defense patrol handles zero DefWalls and DefTotal");
            Check(AiDefensePatrolPolicy.NeedsCastleDefender(0, int.MaxValue) &&
                    !AiDefensePatrolPolicy.NeedsCastleDefender(int.MaxValue, int.MaxValue),
                "AI defense patrol handles integer boundary quotas");
            Check(AiDefensePatrolPolicy.SelectComparisonValue(true) == unchecked((uint)int.MaxValue) &&
                    AiDefensePatrolPolicy.SelectComparisonValue(false) == unchecked((uint)int.MinValue),
                "AI defense patrol emits signed-jl comparison sentinels");
        }

        private static void TestMovementSafetyIntegration()
        {
            string projectDirectory = FindProjectDirectory();
            string fastRecruit = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "FastRecruitRallyMovementRuntime.cs"));
            string troopMovement = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "TroopMovementFix3Runtime.cs"));
            string english = File.ReadAllText(Path.Combine(
                projectDirectory, "Locales", "en-US.txt"));
            string german = File.ReadAllText(Path.Combine(
                projectDirectory, "Locales", "de-DE.txt"));

            Check(fastRecruit.Contains("!players.IsPlayerIdValid(ownerPlayerId)") &&
                    fastRecruit.Contains("players.IsAIPlayer(ownerPlayerId)") &&
                    fastRecruit.Contains("unit->r_ControllableForPlayerId == tracking.OwnerPlayerId") &&
                    fastRecruit.Contains("OwnerPlayerId = ownerPlayerId;"),
                "fast recruit rally tracks only valid human owners and preserves owner identity");
            Check(!fastRecruit.Contains("FastRecruitRallyMovementModLog.Debug") &&
                    !fastRecruit.Contains("Fast recruit rally tracking added") &&
                    !fastRecruit.Contains("Fast recruit rally movement started"),
                "fast recruit rally omits routine per-unit lifecycle logging");

            int tribeLoop = troopMovement.IndexOf("foreach (int unitId in unitIds)",
                StringComparison.Ordinal);
            int idValidation = troopMovement.IndexOf(
                "GameUnitManagerAPI.Instance.IsValidId(unitId)",
                tribeLoop,
                StringComparison.Ordinal);
            int unitLookup = troopMovement.IndexOf(
                "GameUnitManagerAPI.Instance.TryGetUnitById(",
                tribeLoop,
                StringComparison.Ordinal);
            Check(tribeLoop >= 0 && idValidation > tribeLoop &&
                    unitLookup > idValidation,
                "tribe synchronization rejects zero or out-of-range IDs before one-based unit lookup");
            Check(english.Contains("human-player units") &&
                    english.Contains("AI units remain unchanged") &&
                    german.Contains("Einheiten menschlicher Spieler") &&
                    german.Contains("KI-Einheiten bleiben unverändert"),
                "fast recruit rally help text documents the human-only behavior");
        }

        private static void TestAiDefensePatrolIntegration()
        {
            string projectDirectory = FindProjectDirectory();
            string runtime = File.ReadAllText(Path.Combine(projectDirectory, "src", "AiDefensePatrolFix.cs"));
            string orchestrator = File.ReadAllText(Path.Combine(projectDirectory, "src", "BugfixesAndQoLRuntime.cs"));
            string viewModel = File.ReadAllText(Path.Combine(projectDirectory, "src", "BugfixesAndQoLViewModel.cs"));
            string xaml = File.ReadAllText(Path.Combine(
                projectDirectory,
                "Override",
                "ScriptExtenderUI",
                "BugfixesAndQoLSettings.xaml"));

            Check(runtime.Contains("OverwrittenInstructionPlacement.BeforeCallback") &&
                    runtime.Contains("X64SmartCPUContextRegs.All") &&
                    runtime.Contains("BugfixesHookInfrastructure.CreateOwnedTransaction") &&
                    runtime.Contains("settings.EnableMod") &&
                    runtime.Contains("settings.EnableAiDefensePatrolFix"),
                "AI defense patrol runtime uses the owned before-callback hook and its specific setting gates");
            Check(runtime.Contains("registers->RAX = originalRax") &&
                    runtime.Contains("TryGetUnitById(unitId") &&
                    runtime.Contains("for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)"),
                "AI defense patrol retains Vanilla fallback and explicit ID/index contracts");
            Check(orchestrator.Contains("EnsureAiDefensePatrolFix") &&
                    orchestrator.Contains("aiDefensePatrolFix?.ApplySetting()") &&
                    orchestrator.Contains("aiDefensePatrolFix?.Dispose()"),
                "AI defense patrol participates in native initialization, setting reconciliation and final disposal");
            Check(viewModel.Contains("private bool enableAiDefensePatrolFix = true;") &&
                    viewModel.Contains("public bool EnableAiDefensePatrolFix") &&
                    viewModel.Contains("EnableAiDefensePatrolFix = true;"),
                "AI defense patrol host setting defaults and resets to enabled");
            Check(xaml.Contains("bugfixes.enable-ai-defense-patrol-fix") &&
                    xaml.Contains("IsChecked=\"{Binding EnableAiDefensePatrolFix, Mode=TwoWay}\""),
                "AI defense patrol setting is searchable and bound in XAML");
        }

        private static void TestAiStoneReserveIntegration()
        {
            string projectDirectory = FindProjectDirectory();
            string sourceDirectory = Path.Combine(projectDirectory, "src");
            string fix = File.ReadAllText(Path.Combine(sourceDirectory, "AiStoneReserveFix.cs"));
            string viewModel = File.ReadAllText(Path.Combine(sourceDirectory, "BugfixesAndQoLViewModel.cs"));
            string xaml = File.ReadAllText(Path.Combine(
                projectDirectory,
                "Override",
                "ScriptExtenderUI",
                "BugfixesAndQoLSettings.xaml"));
            string english = File.ReadAllText(Path.Combine(projectDirectory, "Locales", "en-US.txt"));
            string german = File.ReadAllText(Path.Combine(projectDirectory, "Locales", "de-DE.txt"));

            Check(fix.Contains("settings.EnableMod && settings.EnableAiStoneReserveFix"),
                "AI stone-reserve runtime uses the mod and specific host setting gates");
            Check(viewModel.Contains("private bool enableAiStoneReserveFix = true;") &&
                    viewModel.Contains("public bool EnableAiStoneReserveFix") &&
                    viewModel.Contains("EnableAiStoneReserveFix = true;"),
                "AI stone-reserve host setting defaults and resets to enabled");
            Check(xaml.Contains("bugfixes.enable-ai-stone-reserve-fix") &&
                    xaml.Contains("IsChecked=\"{Binding EnableAiStoneReserveFix, Mode=TwoWay}\"") &&
                    english.Contains("BugfixesAndQoL.EnableAiStoneReserveFix=") &&
                    german.Contains("BugfixesAndQoL.EnableAiStoneReserveFix="),
                "AI stone-reserve setting is searchable, bound, and localized");

            string removedSettingName = "EnableAi" + "Fixes";
            bool removedFromActiveSources = !viewModel.Contains(removedSettingName) &&
                !xaml.Contains(removedSettingName);
            foreach (string sourceFile in Directory.GetFiles(sourceDirectory, "*.cs"))
                removedFromActiveSources &= !File.ReadAllText(sourceFile).Contains(removedSettingName);
            foreach (string localeFile in Directory.GetFiles(
                Path.Combine(projectDirectory, "Locales"),
                "*.txt"))
            {
                removedFromActiveSources &= !File.ReadAllText(localeFile).Contains(removedSettingName);
            }
            string sharedLocalization = File.ReadAllText(Path.Combine(
                Directory.GetParent(projectDirectory).FullName,
                "Shared",
                "SerpLocalization.cs"));
            removedFromActiveSources &= !sharedLocalization.Contains(removedSettingName);
            Check(removedFromActiveSources,
                "the removed aggregate AI-fixes setting is absent from active sources, XAML, and localization");
        }

        private static void TestAiWallTargetingIntegration()
        {
            string projectDirectory = FindProjectDirectory();
            string patch = File.ReadAllText(Path.Combine(projectDirectory, "src", "AiWallTargetingFix.cs"));
            string runtime = File.ReadAllText(Path.Combine(projectDirectory, "src", "BugfixesAndQoLRuntime.cs"));
            string viewModel = File.ReadAllText(Path.Combine(projectDirectory, "src", "BugfixesAndQoLViewModel.cs"));
            string xaml = File.ReadAllText(Path.Combine(
                projectDirectory,
                "Override",
                "ScriptExtenderUI",
                "BugfixesAndQoLSettings.xaml"));
            string english = File.ReadAllText(Path.Combine(projectDirectory, "Locales", "en-US.txt"));
            string german = File.ReadAllText(Path.Combine(projectDirectory, "Locales", "de-DE.txt"));

            Check(patch.Contains("settings.EnableMod &&") &&
                    patch.Contains("settings.EnableAiWallTargetingFix") &&
                    patch.Contains("OriginalReservationRejectJump = { 0x75, 0x63 }") &&
                    patch.Contains("EnabledBytes = { 0x90, 0x90 }") &&
                    patch.Contains("TryRollback(targetState, currentState)"),
                "AI wall-targeting patch has its specific setting gates, audited states, and rollback");
            Check(runtime.Contains("EnsureAiWallTargetingFix") &&
                    runtime.Contains("aiWallTargetingFix?.Dispose()") &&
                    runtime.Contains("aiWallTargetingFix.ApplySetting()"),
                "AI wall-targeting fix participates in initialization, reconciliation, and final disposal");
            Check(viewModel.Contains("private bool enableAiWallTargetingFix = true;") &&
                    viewModel.Contains("[SyncHostOnly]") &&
                    viewModel.Contains("public bool EnableAiWallTargetingFix") &&
                    viewModel.Contains("EnableAiWallTargetingFix = true;"),
                "AI wall-targeting host setting defaults and resets to enabled");
            Check(xaml.Contains("bugfixes.enable-ai-wall-targeting-fix") &&
                    xaml.Contains("IsChecked=\"{Binding EnableAiWallTargetingFix, Mode=TwoWay}\""),
                "AI wall-targeting setting is searchable and bound in XAML");
            Check(english.Contains("Allows multiple AI attackers to target the same reachable wall segment") &&
                    german.Contains("dasselbe erreichbare Mauersegment gleichzeitig anzugreifen"),
                "AI wall-targeting help text documents shared reachable wall targets");
        }

        private static unsafe void TestAIResourceShortageSleepPolicy()
        {
            int allocationSize = AIResourceShortageSleepPolicy.SleepStateTableOffset +
                (AIResourceShortageSleepPolicy.MaximumPlayerId + 1) *
                AIResourceShortageSleepPolicy.PlayerStride + 128;
            IntPtr allocation = Marshal.AllocHGlobal(allocationSize);
            try
            {
                byte* playerManager = (byte*)allocation.ToPointer();
                for (int index = 0; index < allocationSize; index++)
                    playerManager[index] = 0x5A;

                const int targetPlayerId = 4;
                byte* targetStates = playerManager +
                    AIResourceShortageSleepPolicy.SleepStateTableOffset +
                    targetPlayerId * AIResourceShortageSleepPolicy.PlayerStride;
                byte* neighboringStates = targetStates + AIResourceShortageSleepPolicy.PlayerStride;
                for (int buildingType = 0; buildingType <= (int)eStructs.STRUCT_WATERPOT; buildingType++)
                {
                    targetStates[buildingType] = 1;
                    neighboringStates[buildingType] = 0x7B;
                }

                int* plannerEnabled = (int*)(playerManager +
                    targetPlayerId * AIResourceShortageSleepPolicy.PlayerStride +
                    AIResourceShortageSleepPolicy.PlannerEnabledOffset);
                *plannerEnabled = 0;
                Check(AIResourceShortageSleepPolicy.TryClearSleepRequests(
                        playerManager, targetPlayerId) && targetStates[(int)eStructs.STRUCT_MILL] == 1,
                    "AI resource-shortage policy leaves outputs untouched when the native planner exits early");
                *plannerEnabled = 1;

                Check(AIResourceShortageSleepPolicy.TryClearSleepRequests(
                        playerManager, targetPlayerId),
                    "AI resource-shortage policy accepts a valid one-based player id");

                var expected = new HashSet<int>();
                foreach (eStructs buildingType in AIResourceShortageSleepPolicy.AffectedBuildingTypes)
                    expected.Add((int)buildingType);

                bool targetRangeIsExact = true;
                bool neighboringPlayerUnchanged = true;
                for (int buildingType = 0; buildingType <= (int)eStructs.STRUCT_WATERPOT; buildingType++)
                {
                    byte expectedTarget = expected.Contains(buildingType) ? (byte)0 : (byte)1;
                    targetRangeIsExact &= targetStates[buildingType] == expectedTarget;
                    neighboringPlayerUnchanged &= neighboringStates[buildingType] == 0x7B;
                }

                Check(expected.Count == 21 && targetRangeIsExact,
                    "AI resource-shortage policy clears exactly the 21 audited building sleep requests");
                Check(neighboringPlayerUnchanged,
                    "AI resource-shortage policy leaves the neighboring player untouched");
                Check(!AIResourceShortageSleepPolicy.TryClearSleepRequests(playerManager, 0) &&
                        !AIResourceShortageSleepPolicy.TryClearSleepRequests(playerManager, 9) &&
                        !AIResourceShortageSleepPolicy.TryClearSleepRequests(null, targetPlayerId),
                    "AI resource-shortage policy rejects invalid player ids and null managers");
            }
            finally
            {
                Marshal.FreeHGlobal(allocation);
            }
        }

        private static void TestAIResourceShortageSleepIntegration()
        {
            string projectDirectory = FindProjectDirectory();
            string runtime = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "AIEconomyProtectionHook.cs"));
            string singlePause = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "SingleBuildingPauseHook.cs"));
            string movedFeatures = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "BugfixesAndQoLRuntime.MovedFeatures.cs"));
            string english = File.ReadAllText(Path.Combine(projectDirectory, "Locales", "en-US.txt"));
            string german = File.ReadAllText(Path.Combine(projectDirectory, "Locales", "de-DE.txt"));

            int originalCall = runtime.IndexOf(
                "aiResourceShortageSleepHook.Original(aiManager, playerId)", StringComparison.Ordinal);
            int clearCall = runtime.IndexOf(
                "AIResourceShortageSleepPolicy.TryClearSleepRequests", StringComparison.Ordinal);
            Check(runtime.Contains("DetourHandle<AIResourceShortageSleepDelegate>") &&
                    runtime.Contains("AIResourceShortageSleepNativeDefinition.FunctionPattern") &&
                    originalCall >= 0 && clearCall > originalCall,
                "AI resource-shortage hook runs Vanilla before clearing its sleep outputs");
            Check(runtime.Contains("ApplySingleBuildingSleepOverrideDuringSynchronization") &&
                    runtime.Contains("SleepStateComparisonDisplacedLength = 20") &&
                    runtime.Contains("sleepStateHook.Hook.DisplacedByteCount != SleepStateComparisonDisplacedLength") &&
                    runtime.Contains("sleepStateHook.Hook.Disable()") &&
                    runtime.Contains("SetSingleBuildingOverrideInterceptionEnabled") &&
                    !runtime.Contains("requestedState != SleepingState") &&
                    !runtime.Contains("PlayerOwnerDistanceFromSleeping"),
                "general sleep synchronization is limited to single-building overrides");
            Check(!singlePause.Contains("NoesisGUIUpdateChecksInGame") &&
                    singlePause.Contains("addChimpActions") &&
                    singlePause.Contains("addChimpActionsHook.Apply()") &&
                    singlePause.Contains("addChimpActionsHook.Undo()") &&
                    singlePause.Contains("buildingDeleteSubscription?.Dispose()") &&
                    movedFeatures.Contains("singleBuildingPauseHook?.Dispose()") &&
                    movedFeatures.Contains("singleBuildingPauseHook = null;"),
                "single-building pause keeps its render correction dormant without overrides");
            int nativeCommit = runtime.IndexOf("CommitResult commitResult = transaction.Commit()", StringComparison.Ordinal);
            int initialNativeDisable = runtime.IndexOf("sleepStateHook.Hook.Disable()", StringComparison.Ordinal);
            int activationBeforeStore = singlePause.IndexOf(
                "overrides.Count == 0 && !TryActivateOverrideHooks()", StringComparison.Ordinal);
            int activationMethod = singlePause.IndexOf(
                "private bool TryActivateOverrideHooks()", StringComparison.Ordinal);
            int uiActivationBeforeStore = singlePause.IndexOf(
                "addChimpActionsHook.Apply()", activationMethod, StringComparison.Ordinal);
            int nativeActivationBeforeStore = singlePause.IndexOf(
                "setSleepOverrideInterceptionEnabled(true)", activationMethod, StringComparison.Ordinal);
            int storeAfterActivation = singlePause.IndexOf(
                "overrides.Set(new SingleBuildingPauseOverride", StringComparison.Ordinal);
            Check(nativeCommit >= 0 && initialNativeDisable > nativeCommit &&
                    activationBeforeStore >= 0 &&
                    storeAfterActivation > activationBeforeStore &&
                    activationMethod > storeAfterActivation &&
                    uiActivationBeforeStore > activationMethod &&
                    nativeActivationBeforeStore > uiActivationBeforeStore &&
                    singlePause.Contains(
                        "!settings.EnableMod || !settings.EnableSingleBuildingPause"),
                "single-building native interception is dormant initially and activates before storing the first override");
            Check(english.Contains("AI sleep mode during resource shortages") &&
                    english.Contains("required input resource is unavailable") &&
                    english.Contains("production or transit") &&
                    !english.Contains("Prevents only") &&
                    !english.Contains("Other causes remain unchanged") &&
                    german.Contains("KI-Schlafmodus bei Rohstoffmangel") &&
                    german.Contains("benoetigter Rohstoff fehlt") &&
                    german.Contains("Produktion oder auf dem Transportweg") &&
                    !german.Contains("Verhindert nur") &&
                    !german.Contains("Andere Ursachen bleiben unveraendert"),
                "AI sleep title and help text describe the intended resource-shortage fix directly");
        }

        private static void TestSingleBuildingPauseOverrideStore()
        {
            var store = new SingleBuildingPauseOverrideStore();
            var first = new SingleBuildingPauseOverride(
                5, true, new IntPtr(0x1000), eStructs.STRUCT_WOODCUTTERS_HUT, 1, 101);
            var firstUpdated = new SingleBuildingPauseOverride(
                5, false, new IntPtr(0x1000), eStructs.STRUCT_WOODCUTTERS_HUT, 1, 101);
            var second = new SingleBuildingPauseOverride(
                9, true, new IntPtr(0x2000), eStructs.STRUCT_QUARRY, 1, 202);

            Check(store.Set(first) && store.Count == 1,
                "single-building override store reports the first active override");
            Check(!store.Set(firstUpdated) &&
                    store.TryGet(5, out SingleBuildingPauseOverride updated) &&
                    !updated.IsSleeping && store.Count == 1,
                "single-building override store updates without a second activation transition");
            Check(!store.Set(second) && store.Count == 2,
                "single-building override store adds further entries without reactivation");

            OverrideRemovalResult typeRemoval = store.RemoveForBuildingType(
                1,
                eStructs.STRUCT_WOODCUTTERS_HUT);
            Check(typeRemoval.Count == 1 && !typeRemoval.BecameEmpty && store.Count == 1,
                "single-building override type reset preserves unrelated entries");
            Check(store.Remove(9) && store.Count == 0,
                "single-building override deletion reports the last-entry transition");

            store.Set(first);
            OverrideRemovalResult lastTypeRemoval = store.RemoveForBuildingType(
                1,
                eStructs.STRUCT_WOODCUTTERS_HUT);
            Check(lastTypeRemoval.Count == 1 && lastTypeRemoval.BecameEmpty && store.Count == 0,
                "single-building override type reset reports the last-entry transition");

            store.Set(first);
            store.Set(second);
            Check(store.Clear() == 2 && store.Count == 0 &&
                    !store.TryGetBySleepingAddress(new IntPtr(0x1000), out _),
                "single-building override clear removes id and address indexes");
        }

        private static void TestTemporaryGateBlockagePolicy()
        {
            const int improved = TemporaryGateBlockagePolicy.ImprovedReachabilityMode;
            Check(TemporaryGateBlockagePolicy.ResolveAccessibilityResult(
                    TemporaryGateBlockagePolicy.VanillaMode, true, eStructs.STRUCT_STABLES,
                    0, true, true) == 0 &&
                TemporaryGateBlockagePolicy.ResolveAccessibilityResult(
                    improved, true, eStructs.STRUCT_WOODCUTTERS_HUT,
                    1, true, true) == 1,
                "AI accessibility policy preserves Vanilla mode and accessible results");
            Check(TemporaryGateBlockagePolicy.ResolveAccessibilityResult(
                    improved, true, eStructs.STRUCT_STABLES, 0, false, false) == 1 &&
                TemporaryGateBlockagePolicy.ResolveAccessibilityResult(
                    improved, true, eStructs.STRUCT_STABLES, 2, false, false) == 1,
                "AI accessibility policy exempts stables for both inaccessible results");
            Check(TemporaryGateBlockagePolicy.ResolveAccessibilityResult(
                    improved, true, eStructs.STRUCT_WOODCUTTERS_HUT, 2, true, true) == 1 &&
                TemporaryGateBlockagePolicy.ResolveAccessibilityResult(
                    improved, true, eStructs.STRUCT_WOODCUTTERS_HUT, 2, true, false) == 2 &&
                TemporaryGateBlockagePolicy.ResolveAccessibilityResult(
                    improved, true, eStructs.STRUCT_WOODCUTTERS_HUT, 2, false, true) == 2 &&
                TemporaryGateBlockagePolicy.ResolveAccessibilityResult(
                    improved, true, eStructs.STRUCT_WOODCUTTERS_HUT, 0, true, true) == 0,
                "improved AI accessibility only relaxes result 2 with a proven friendly portal route");
            Check(TemporaryGateBlockagePolicy.ResolveAccessibilityResult(
                    TemporaryGateBlockagePolicy.AlwaysPreventMode, true,
                    eStructs.STRUCT_WOODCUTTERS_HUT, 0, false, false) == 1 &&
                TemporaryGateBlockagePolicy.ResolveAccessibilityResult(
                    TemporaryGateBlockagePolicy.AlwaysPreventMode, true,
                    eStructs.STRUCT_WOODCUTTERS_HUT, 2, false, false) == 1 &&
                TemporaryGateBlockagePolicy.ResolveAccessibilityResult(
                    TemporaryGateBlockagePolicy.AlwaysPreventMode, false,
                    eStructs.STRUCT_STABLES, 2, true, true) == 2,
                "always-prevent remains scoped to living AI buildings and results 0/2");

            var portals = new List<PclPortalConnection>
            {
                new PclPortalConnection(10, 20, 30, ownerId: 2, buildingId: 100),
                new PclPortalConnection(30, 40, ownerId: 3, buildingId: 200)
            };
            GateBlockageEvaluation throughThirdAndMultiple =
                TemporaryGateBlockagePolicy.Evaluate(10, 40, portals);
            Check(throughThirdAndMultiple.Kind == GateBlockageEvaluationKind.ReachableViaFriendlyGate &&
                    throughThirdAndMultiple.UsedPortalIndices.Length == 2 &&
                    throughThirdAndMultiple.UsedPortalIndices[0] == 0 &&
                    throughThirdAndMultiple.UsedPortalIndices[1] == 1,
                "friendly portal graph follows a third PCL and a multiple-gate chain");
            Check(TemporaryGateBlockagePolicy.Evaluate(
                    10, 99, portals).Kind ==
                    GateBlockageEvaluationKind.UnreachableEvenWithFriendlyGates &&
                TemporaryGateBlockagePolicy.Evaluate(
                    10, 10, portals).Kind ==
                    GateBlockageEvaluationKind.ReachableWithoutFriendlyGate,
                "friendly portal graph distinguishes permanent separation and an existing PCL connection");

            Func<int, bool> validPlayer = id => id >= 1 && id <= 8;
            Func<int, int, bool> allied = (first, second) => first == 2 && second == 3;
            Check(TemporaryGateBlockagePolicy.IsFriendlyPortalOwner(2, 2, validPlayer, allied) &&
                    TemporaryGateBlockagePolicy.IsFriendlyPortalOwner(2, 3, validPlayer, allied) &&
                    !TemporaryGateBlockagePolicy.IsFriendlyPortalOwner(2, 4, validPlayer, allied),
                "portal ownership accepts own and allied gates but rejects enemy gates");
            Check(TemporaryGateBlockagePolicy.IsGateOrDrawbridge(eStructs.STRUCT_GATE_MAIN) &&
                    TemporaryGateBlockagePolicy.IsGateOrDrawbridge(eStructs.STRUCT_DRAWBRIDGE) &&
                    !TemporaryGateBlockagePolicy.IsGateOrDrawbridge(eStructs.STRUCT_SIEGE_TOWER),
                "portal type policy includes gates and drawbridges but excludes unrelated linkages");
        }

        private static void TestTemporaryGateBlockageIntegration()
        {
            string projectDirectory = FindProjectDirectory();
            string hook = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "AIEconomyProtectionHook.cs"));
            string classifier = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "AIBuildingTemporaryAccessClassifier.cs"));

            Check(hook.Contains("InaccessibleBuildingDecisionRva = 0xC8FD7") &&
                    hook.Contains("BuildingAccessibilityFunctionRva = 0xC90E0") &&
                    hook.Contains("InaccessibleBuildingDecisionLength = 15") &&
                    hook.Contains("OverwrittenInstructionPlacement.AfterCallback") &&
                    hook.Contains("DisplacedByteCount") &&
                    !hook.Contains("0x3B2FF") &&
                    !hook.Contains("InaccessibleCounterOffsetFromR8"),
                "AI accessibility hook replaces the woodcutter counter hook with the audited general sweep decision");
            Check(hook.Contains("registers->RSI") && hook.Contains("registers->R14") &&
                    hook.Contains("registers->RAX = unchecked((uint)effectiveResult)") &&
                    hook.Contains("nameof(GameBuilding.r_TilePositionXEnd), 0xFE") &&
                    hook.Contains("nameof(GamePlayerResources.r_KeepTileId), 0xA0"),
                "AI accessibility hook uses the audited register and managed-layout contracts");
            Check(classifier.Contains("r_TilePositionXEnd") &&
                    classifier.Contains("r_TilePositionYEnd") &&
                    classifier.Contains("r_KeepTileId") &&
                    classifier.Contains("PortalThirdPclOffsetDwords = 0x883") &&
                    classifier.Contains("IsPlayerAlliedTo") &&
                    !classifier.Contains("GetGatehouseArray") &&
                    !classifier.Contains("CollectAccessPcls"),
                "AI accessibility classifier uses Vanilla entrance/keep PCLs and native three-sided portals only");
        }

        private static void TestNativeContracts()
        {
            string root = Environment.GetEnvironmentVariable("SHCDE_GAME_DIR") ??
                @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
            string path = Path.Combine(root,
                "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
            Check(File.Exists(path), "canonical native DLL exists");
            if (!File.Exists(path))
                return;
            byte[] file = File.ReadAllBytes(path);
            using (SHA256 sha = SHA256.Create())
            {
                string hash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", string.Empty);
                Check(string.Equals(hash, ExpectedHash, StringComparison.OrdinalIgnoreCase),
                    "canonical native SHA-256 matches the audited baseline");
                Check(string.Equals(Shared.DebugLogHelper.CurrentNativeSha256, ExpectedHash,
                        StringComparison.OrdinalIgnoreCase),
                    "shared native SHA-256 matches the AI defense patrol baseline");
            }
            var image = new PeImage(file);
            byte[] mappedImage = MapPeImage(file);
            try
            {
                AiDefensePatrolNativeDefinition.ValidateManagedLayout();
                AiDefensePatrolNativeDefinition.Validate(MapPeImage(file));
                Check(true,
                    "AI defense patrol GameUnit layout, unique signature, instruction span, branches and call targets");
            }
            catch (Exception exception)
            {
                Check(false, "AI defense patrol native contract: " + exception.Message);
            }
            try
            {
                AIResourceShortageSleepNativeDefinition.Validate(mappedImage);
                Check(true,
                    "AI resource-shortage sleep planner signature, calls, and 21 output stores");
            }
            catch (Exception exception)
            {
                Check(false, "AI resource-shortage sleep native contract: " + exception.Message);
            }
            CheckBytes(image, FindRva, new byte[] { 0x44, 0x89, 0x44, 0x24, 0x18, 0x89, 0x54, 0x24,
                0x10, 0x55, 0x56, 0x57, 0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x48, 0x83, 0xEC, 0x68,
                0x48, 0x8B, 0xE9 }, "selector entry bytes");
            CheckBytes(image, ResolveRva, new byte[] { 0x44, 0x89, 0x4C, 0x24, 0x20, 0x53, 0x57, 0x41,
                0x57, 0x48, 0x83, 0xEC, 0x20, 0x48, 0x63, 0x44, 0x24, 0x60, 0x45, 0x8B, 0xD0, 0x49,
                0x63, 0xD9, 0x4C, 0x63, 0xDA }, "resolver entry bytes");
            CheckBytes(image, PlannerRva, new byte[] { 0x48, 0x89, 0x5C, 0x24, 0x20, 0x55, 0x56, 0x57,
                0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57, 0x48, 0x83, 0xEC, 0x30, 0x48, 0x63,
                0xF2 }, "movement planner entry bytes");
            CheckBytes(image, MovementPlannerLowFlagGateRva,
                new byte[] { 0xF6, 0x84, 0x8A, 0xB0, 0x71, 0x8F, 0x04, 0x30 },
                "movement low-flag gate bytes");
            CheckBytes(image, MovementPlannerStructureFlagGateRva,
                new byte[] { 0xF7, 0x84, 0x8A, 0xB0, 0x71, 0x8F, 0x04,
                0x00, 0x01, 0x00, 0x10 }, "movement structure-flag gate bytes");
            const string aiWallTargetingPattern =
                "8B D3 49 8B CC E8 ?? ?? ?? ?? 85 C0 75 63 8B 05 ?? ?? ?? ?? " +
                "4C 8D 3D ?? ?? ?? ?? 41 8D 04 C6 48 98 41 8B 14 87 03 D3 " +
                "48 63 C2 41 F7 84 87 00 84 89 00 00 01 00 10 75 1A";
            try
            {
                int patternRva = Shared.NativePatternResolver.FindUniquePattern(
                    mappedImage,
                    aiWallTargetingPattern,
                    "AI wall-targeting test context");
                Check(patternRva == AiWallTargetingPatternRva,
                    "AI wall-targeting context is unique at the audited RVA");
            }
            catch (Exception exception)
            {
                Check(false, "AI wall-targeting context: " + exception.Message);
            }
            CheckBytes(image, AiWallReservationRejectJumpRva,
                new byte[] { 0x75, 0x63 }, "AI wall reservation rejection branch bytes");
            CheckBytes(image, AiWallApproachTileGuardRva,
                new byte[] { 0x41, 0xF7, 0x84, 0x87, 0x00, 0x84, 0x89, 0x00,
                    0x00, 0x01, 0x00, 0x10, 0x75, 0x1A },
                "AI wall approach-tile validation remains separate and intact");
            CheckBytes(image, 0xC8F50, new byte[] {
                0x40, 0x56, 0x57, 0x41, 0x56, 0x48, 0x83, 0xEC,
                0x20, 0xBE, 0x01, 0x00, 0x00, 0x00, 0x44, 0x8B,
                0xF2, 0x48, 0x8B, 0xF9 }, "general AI accessibility sweep entry bytes");
            CheckBytes(image, 0xC8FD7, new byte[] {
                0x85, 0xC0, 0x75, 0x06, 0x66, 0x44, 0x89, 0x3B,
                0xEB, 0x11, 0x83, 0xF8, 0x02, 0x75, 0x06 },
                "general AI accessibility decision exact 15-byte hook span");
            Check(image.CountNearCalls(0xC8FD2, 5, 0xC90E0) == 1,
                "general AI sweep calls the audited building accessibility classifier immediately before the hook");
            Check(Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_AliveState)).ToInt32() == 0xD0 &&
                    Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_BuildingType)).ToInt32() == 0xD2 &&
                    Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_PlayerIdOwner)).ToInt32() == 0xD6 &&
                    Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_GlobalId)).ToInt32() == 0xD8 &&
                    Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_TilePositionXEnd)).ToInt32() == 0xFE &&
                    Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_TilePositionYEnd)).ToInt32() == 0x100 &&
                    Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.r_KeepTileId)).ToInt32() == 0xA0,
                "AI accessibility managed building and player-resource offsets match the native contract");
            Check(image.CountNearCalls(DispatcherRva, DispatcherSize, FindRva) >= 2 &&
                    image.CountNearCalls(DispatcherRva, DispatcherSize, ResolveRva) >= 3 &&
                    image.CountNearCalls(DispatcherRva, DispatcherSize, PlannerRva) >= 1,
                "dispatcher contains initial and follow-up moat-work call chain");
        }

        private static byte[] MapPeImage(byte[] file)
        {
            int peOffset = ReadInt32(file, 0x3C);
            if (ReadInt32(file, peOffset) != 0x00004550)
                throw new InvalidDataException("Invalid PE signature.");

            int sectionCount = ReadUInt16(file, peOffset + 6);
            int optionalHeaderSize = ReadUInt16(file, peOffset + 20);
            int optionalHeader = peOffset + 24;
            int sizeOfImage = ReadInt32(file, optionalHeader + 56);
            int sizeOfHeaders = ReadInt32(file, optionalHeader + 60);
            byte[] image = new byte[sizeOfImage];
            Buffer.BlockCopy(file, 0, image, 0, Math.Min(sizeOfHeaders, file.Length));

            int sectionTable = optionalHeader + optionalHeaderSize;
            for (int index = 0; index < sectionCount; index++)
            {
                int section = sectionTable + index * 40;
                int virtualAddress = ReadInt32(file, section + 12);
                int rawSize = ReadInt32(file, section + 16);
                int rawAddress = ReadInt32(file, section + 20);
                if (rawSize <= 0)
                    continue;
                if (rawAddress < 0 || rawAddress > file.Length - rawSize ||
                    virtualAddress < 0 || virtualAddress > image.Length - rawSize)
                {
                    throw new InvalidDataException("PE section lies outside the file or virtual image.");
                }

                Buffer.BlockCopy(file, rawAddress, image, virtualAddress, rawSize);
            }

            return image;
        }

        private static string FindProjectDirectory()
        {
            DirectoryInfo directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "BugfixesAndQoL.csproj")))
                    return directory.FullName;
                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("BugfixesAndQoL project directory was not found.");
        }

        private static int ReadUInt16(byte[] bytes, int offset) =>
            bytes[offset] | bytes[offset + 1] << 8;

        private static int ReadInt32(byte[] bytes, int offset) =>
            bytes[offset] |
            bytes[offset + 1] << 8 |
            bytes[offset + 2] << 16 |
            bytes[offset + 3] << 24;

        private static void CheckBytes(PeImage image, int rva, byte[] expected, string name)
        {
            byte[] actual = image.ReadRva(rva, expected.Length);
            bool equal = actual.Length == expected.Length;
            for (int i = 0; equal && i < expected.Length; i++)
                equal = actual[i] == expected[i];
            Check(equal, name);
        }

        private static void Check(bool condition, string name)
        {
            if (condition)
                Console.WriteLine("PASS " + name);
            else
            {
                failures++;
                Console.Error.WriteLine("FAIL " + name);
            }
        }

        private sealed class PeImage
        {
            private readonly byte[] file;
            private readonly List<Section> sections = new List<Section>();

            internal PeImage(byte[] file)
            {
                this.file = file;
                int pe = ReadInt32(0x3C);
                int count = ReadUInt16(pe + 6);
                int table = pe + 24 + ReadUInt16(pe + 20);
                for (int i = 0; i < count; i++)
                {
                    int entry = table + i * 40;
                    sections.Add(new Section(ReadInt32(entry + 12),
                        Math.Max(ReadInt32(entry + 8), ReadInt32(entry + 16)), ReadInt32(entry + 20)));
                }
            }

            internal byte[] ReadRva(int rva, int length)
            {
                int offset = RvaToOffset(rva);
                var result = new byte[length];
                Buffer.BlockCopy(file, offset, result, 0, length);
                return result;
            }

            internal int CountNearCalls(int startRva, int length, int targetRva)
            {
                byte[] bytes = ReadRva(startRva, length);
                int count = 0;
                for (int i = 0; i <= bytes.Length - 5; i++)
                {
                    if (bytes[i] != 0xE8)
                        continue;
                    int displacement = bytes[i + 1] | bytes[i + 2] << 8 |
                        bytes[i + 3] << 16 | bytes[i + 4] << 24;
                    if (startRva + i + 5 + displacement == targetRva)
                        count++;
                }
                return count;
            }

            private int RvaToOffset(int rva)
            {
                foreach (Section section in sections)
                {
                    if (rva >= section.VirtualAddress && rva < section.VirtualAddress + section.Size)
                        return checked(section.RawOffset + rva - section.VirtualAddress);
                }
                throw new InvalidOperationException($"RVA 0x{rva:X} is outside PE sections.");
            }

            private int ReadUInt16(int offset) => file[offset] | file[offset + 1] << 8;
            private int ReadInt32(int offset) => file[offset] | file[offset + 1] << 8 |
                file[offset + 2] << 16 | file[offset + 3] << 24;

            private readonly struct Section
            {
                internal Section(int virtualAddress, int size, int rawOffset)
                {
                    VirtualAddress = virtualAddress;
                    Size = size;
                    RawOffset = rawOffset;
                }
                internal int VirtualAddress { get; }
                internal int Size { get; }
                internal int RawOffset { get; }
            }
        }
    }
}
