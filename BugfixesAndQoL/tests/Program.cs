using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Xml;
using CrusaderDE;
using Iced.Intel;
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
            TestSurrenderGameOverPolicy();
            TestCoopCustomLordSelectionPolicy();
            TestTrailCustomizationOwnership();
            TestTunnelPlacementDistancePolicy();
            TestTunnelPlacementDistanceIntegration();
            TestAicDropdownPolicy();
            TestAicDropdownIntegration();
            TestFriendlyMoatMovementPolicy();
            TestFriendlyMoatMovementIntegration();
            TestReachableEnemyGatehouseUnitIdContract();
            TestSynchronizedGatehouseReachabilityPolicy();
            TestMovementFastPathParity();
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
            TestResolutionAwareZoomPolicy();
            TestResolutionAwareZoomIntegration();
            TestNotificationSkipPolicy();
            TestNotificationManagedContracts();
            TestNotificationQueueNearCallResolver();
            TestNotificationSkipIntegration();
            TestAllyGoodsTransferPolicy();
            TestAllyGoodsTransferIntegration();
            TestMinimapInputIntegration();
            TestPlacementCancelMoveSuppressionPolicy();
            TestPlacementCancelMoveSuppressionIntegration();
            TestSpriteAnimationGroup26Contract();
            TestMapFileManagerContract();
            TestMultiplayerLobbyReturnIntegration();
            TestClassicMapSizeReader();
            TestLobbyMapSelectionMemory();
            TestNativePatternSearch();
            TestNativeContracts();
            if (failures == 0)
            {
                Console.WriteLine("BugfixesAndQoL policy and native-contract tests passed.");
                return 0;
            }
            Console.Error.WriteLine($"BugfixesAndQoL policy and native-contract tests failed: {failures}.");
            return 1;
        }

        private static void TestSurrenderGameOverPolicy()
        {
            Check(SurrenderPolicy.ResolvePresentedGameOverState(1, true) == 2,
                "eliminated-player spectator promotion presents Vanilla victory as defeat");
            Check(SurrenderPolicy.ResolvePresentedGameOverState(2, true) == 2,
                "eliminated-player spectator promotion preserves an existing defeat");
            Check(SurrenderPolicy.ResolvePresentedGameOverState(0, true) == 0 &&
                    SurrenderPolicy.ResolvePresentedGameOverState(7, true) == 7,
                "spectator promotion preserves running and unknown game-over states");
            Check(SurrenderPolicy.ResolvePresentedGameOverState(1, false) == 1,
                "ordinary victory and initial spectators retain Vanilla presentation");

            string featureSource = File.ReadAllText(Path.Combine("src", "SurrenderFeature.cs"));
            const string correctedOriginalCall =
                "setGameOverStateOriginal(self, presentedState, screen, skirmishDate);";
            int correctedOriginalCallCount = featureSource.Split(
                new[] { correctedOriginalCall },
                StringSplitOptions.None).Length - 1;
            Check(featureSource.Contains(
                        "lobbyReturnFeature.OnGameOverState(presentedState);") &&
                    correctedOriginalCallCount == 1 &&
                    !featureSource.Contains(
                        "setGameOverStateOriginal(self, state, screen, skirmishDate);"),
                "game-over hook forwards the corrected state once and preserves screen and date");

            const string correctedOstOriginalCall =
                "addOnScreenTextEntryOriginal(self, ostID, presentedData1, data2, data3, data4, data5);";
            int correctedOstOriginalCallCount = featureSource.Split(
                new[] { correctedOstOriginalCall },
                StringSplitOptions.None).Length - 1;
            Check(featureSource.Contains(
                        "int presentedData1 = ostID == Enums.eOnScreenText.OST_MP_GAME_OVER") &&
                    featureSource.Contains(
                        "SurrenderPolicy.ResolvePresentedGameOverState(data1, spectatorPromotionRequested)") &&
                    correctedOstOriginalCallCount == 1 &&
                    !featureSource.Contains(
                        "addOnScreenTextEntryOriginal(self, ostID, data1, data2, data3, data4, data5);"),
                "OST hook corrects only the game-over result and forwards all auxiliary data once");

            Check(featureSource.Contains(
                        "LogGameOverStateCorrectionOnce(data1, presentedData1, \"OST_MP_GAME_OVER\");") &&
                    featureSource.Contains(
                        "LogGameOverStateCorrectionOnce(state, presentedState, \"setGameOverState\");") &&
                    featureSource.Contains(
                        "if (presentedState == originalState || gameOverStateCorrectionLogged)"),
                "both presentation boundaries share the one-shot correction diagnostic");

            TestStatisticsTeamBadgePolicy();
            TestStatisticsTeamBadgeIntegration(featureSource);
        }

        private static void TestStatisticsTeamBadgePolicy()
        {
            Check(SurrenderPolicy.StatisticsTeamBadgesOff == 0 &&
                    SurrenderPolicy.StatisticsTeamBadgesVanillaIcons == 1 &&
                    SurrenderPolicy.StatisticsTeamBadgesEasyReadIcons == 2 &&
                    SurrenderPolicy.DefaultStatisticsTeamBadgeMode == 1,
                "statistics team-badge modes use stable slider values and default to Vanilla icons");
            Check(SurrenderPolicy.NormalizeStatisticsTeamBadgeMode(0) == 0 &&
                    SurrenderPolicy.NormalizeStatisticsTeamBadgeMode(1) == 1 &&
                    SurrenderPolicy.NormalizeStatisticsTeamBadgeMode(2) == 2 &&
                    SurrenderPolicy.NormalizeStatisticsTeamBadgeMode(-1) == 1 &&
                    SurrenderPolicy.NormalizeStatisticsTeamBadgeMode(3) == 1,
                "statistics team-badge mode normalization preserves valid values and restores the default");

            var localMode = new LocalPerPlayerSetting<int>(
                SurrenderPolicy.DefaultStatisticsTeamBadgeMode);
            bool localSlotsWork = localMode.Value == 1 &&
                localMode.TrySetLocalPlayerId(3) &&
                localMode.SetValue(2) &&
                localMode.Data[3] == 2 &&
                localMode.Data[4] == 1 &&
                localMode.TrySetLocalPlayerId(5) &&
                localMode.SetValue(0) &&
                localMode.Data[3] == 2 &&
                localMode.Data[5] == 0;
            Check(localSlotsWork,
                "statistics team-badge preference keeps independent per-player values");

            int[] valid = new int[9];
            int[] ranking = new int[9];
            int[][] individualRanking = new int[16][];
            for (int playerId = 1; playerId <= 8; playerId++)
            {
                valid[playerId] = 1;
                ranking[playerId] = playerId;
            }
            for (int rankingIndex = 0; rankingIndex < individualRanking.Length; rankingIndex++)
            {
                individualRanking[rankingIndex] = new int[9];
                for (int ordinal = 1; ordinal <= 8; ordinal++)
                    individualRanking[rankingIndex][ordinal] = (ordinal + rankingIndex) % 8 + 1;
            }

            int[] rows = new int[8];
            bool standardOrder = SurrenderPolicy.TryBuildStatisticsRowPlayerIds(
                valid, ranking, individualRanking, 0, false, rows);
            Check(standardOrder && RowsEqual(rows, 1, 2, 3, 4, 5, 6, 7, 8),
                "statistics team badges follow Vanilla's standard ranking order");

            bool reversedOrder = SurrenderPolicy.TryBuildStatisticsRowPlayerIds(
                valid, ranking, individualRanking, 0, true, rows);
            Check(reversedOrder && RowsEqual(rows, 8, 7, 6, 5, 4, 3, 2, 1),
                "statistics team badges follow Vanilla's reversed ranking order");

            int[] expectedRankingIndices =
                { -1, 0, 1, 10, 3, 2, 5, 6, 7, 8, 4, 11, 12, 13, 14, 15 };
            bool allSortTypesMatch = true;
            for (int sortType = 0; sortType <= 15; sortType++)
            {
                allSortTypesMatch &= SurrenderPolicy.TryBuildStatisticsRowPlayerIds(
                    valid, ranking, individualRanking, sortType, false, rows);
                int rankingIndex = expectedRankingIndices[sortType];
                int[] expected = rankingIndex < 0 ? ranking : individualRanking[rankingIndex];
                for (int row = 0; row < 8; row++)
                    allSortTypesMatch &= rows[row] == expected[row + 1];

                allSortTypesMatch &= SurrenderPolicy.TryBuildStatisticsRowPlayerIds(
                    valid, ranking, individualRanking, sortType, true, rows);
                for (int row = 0; row < 8; row++)
                    allSortTypesMatch &= rows[row] == expected[8 - row];
            }
            Check(allSortTypesMatch,
                "statistics team badges mirror all 16 Vanilla sort-to-ranking mappings");

            int[] sparseValid = new int[9];
            sparseValid[1] = 1;
            sparseValid[3] = 1;
            sparseValid[7] = 1;
            bool sparseOrder = SurrenderPolicy.TryBuildStatisticsRowPlayerIds(
                sparseValid, ranking, individualRanking, 0, false, rows);
            Check(sparseOrder && RowsEqual(rows, 1, 3, 7, 0, 0, 0, 0, 0),
                "statistics team badges compact fewer than eight valid participants like Vanilla");

            int[] invalidRanking = (int[])ranking.Clone();
            invalidRanking[4] = 9;
            bool invalidRejected = !SurrenderPolicy.TryBuildStatisticsRowPlayerIds(
                valid, invalidRanking, individualRanking, 0, false, rows);
            Check(invalidRejected && RowsEqual(rows, 0, 0, 0, 0, 0, 0, 0, 0),
                "statistics team badges fail closed for invalid ranked player IDs");

            int[] teamShields = new int[9];
            teamShields[1] = 1;
            teamShields[2] = 1;
            teamShields[3] = 2;
            teamShields[4] = 3;
            teamShields[5] = 4;
            teamShields[6] = 0;
            teamShields[7] = 5;
            Check(SurrenderPolicy.ResolveStatisticsTeamShield(1, teamShields) == 1 &&
                    SurrenderPolicy.ResolveStatisticsTeamShield(2, teamShields) == 1 &&
                    SurrenderPolicy.ResolveStatisticsTeamShield(3, teamShields) == 2 &&
                    SurrenderPolicy.ResolveStatisticsTeamShield(4, teamShields) == 3 &&
                    SurrenderPolicy.ResolveStatisticsTeamShield(5, teamShields) == 4,
                "statistics team members resolve to the shared Vanilla badge for teams 1 through 4");
            Check(SurrenderPolicy.ResolveStatisticsTeamShield(6, teamShields) == 0 &&
                    SurrenderPolicy.ResolveStatisticsTeamShield(7, teamShields) == 0 &&
                    SurrenderPolicy.ResolveStatisticsTeamShield(0, teamShields) == 0,
                "statistics solo and invalid team values remain badge-free");

            bool stylesMatchLobby =
                SurrenderPolicy.TryResolveStatisticsTeamBadgeStyle(1, out StatisticsTeamBadgeStyle team1) &&
                team1.Red == 204 && team1.Green == 80 && team1.Blue == 80 && !team1.UseDarkText &&
                SurrenderPolicy.TryResolveStatisticsTeamBadgeStyle(2, out StatisticsTeamBadgeStyle team2) &&
                team2.Red == 204 && team2.Green == 204 && team2.Blue == 80 && team2.UseDarkText &&
                SurrenderPolicy.TryResolveStatisticsTeamBadgeStyle(3, out StatisticsTeamBadgeStyle team3) &&
                team3.Red == 80 && team3.Green == 140 && team3.Blue == 204 && !team3.UseDarkText &&
                SurrenderPolicy.TryResolveStatisticsTeamBadgeStyle(4, out StatisticsTeamBadgeStyle team4) &&
                team4.Red == 80 && team4.Green == 204 && team4.Blue == 80 && !team4.UseDarkText;
            Check(stylesMatchLobby,
                "statistics team badges use opaque Vanilla lobby colours and a contrasting number colour");
            Check(!SurrenderPolicy.TryResolveStatisticsTeamBadgeStyle(0, out _) &&
                    !SurrenderPolicy.TryResolveStatisticsTeamBadgeStyle(5, out _),
                "statistics team-badge styles reject solo and invalid team IDs");
        }

        private static void TestStatisticsTeamBadgeIntegration(string featureSource)
        {
            string patchPath = Path.Combine(
                "Patches", "Assets", "GUI", "XAMLResources", "HUD_MissionOver.xaml");
            var patch = new XmlDocument();
            patch.Load(patchPath);
            XmlNodeList operations = patch.SelectNodes(
                "/Patch/Operation[contains(@XPath, 'MO_MP_PlayersShields')]");
            bool completeBadgeGrid = operations != null && operations.Count == 16;
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (operations != null)
            {
                foreach (XmlElement operation in operations)
                {
                    XmlElement host = operation.SelectSingleNode("Content/Grid") as XmlElement;
                    XmlElement vanillaIcon = host?.SelectSingleNode("Image") as XmlElement;
                    XmlElement easyReadIcon = host?.SelectSingleNode("Grid") as XmlElement;
                    XmlElement shield = easyReadIcon?.SelectSingleNode("Path") as XmlElement;
                    XmlElement number = easyReadIcon?.SelectSingleNode("TextBlock") as XmlElement;
                    string name = host?.GetAttribute(
                        "Name", "http://schemas.microsoft.com/winfx/2006/xaml") ?? string.Empty;
                    completeBadgeGrid &= host != null &&
                        vanillaIcon != null &&
                        easyReadIcon != null &&
                        shield != null &&
                        number != null &&
                        names.Add(name) &&
                        host.GetAttribute("Grid.Column") == "0" &&
                        host.GetAttribute("Panel.ZIndex") == "20" &&
                        host.GetAttribute("Visibility") == "Collapsed" &&
                        vanillaIcon.GetAttribute("Width") == "20" &&
                        vanillaIcon.GetAttribute("Height") == "20" &&
                        vanillaIcon.GetAttribute("Margin") == "-22,22,0,0" &&
                        vanillaIcon.GetAttribute("Visibility") == "Collapsed" &&
                        easyReadIcon.GetAttribute("Width") == "24" &&
                        easyReadIcon.GetAttribute("Height") == "28" &&
                        easyReadIcon.GetAttribute("Margin") == "-26,30,0,0" &&
                        easyReadIcon.GetAttribute("Visibility") == "Collapsed" &&
                        shield.GetAttribute("Stroke") == "#FF21190F" &&
                        shield.GetAttribute("StrokeThickness") == "1.5" &&
                        !string.IsNullOrEmpty(shield.GetAttribute("Data")) &&
                        number.GetAttribute("FontSize") == "15" &&
                        number.GetAttribute("FontWeight") == "Bold" &&
                        vanillaIcon.GetAttribute(
                            "Name", "http://schemas.microsoft.com/winfx/2006/xaml").Equals(name + "VanillaIcon") &&
                        easyReadIcon.GetAttribute(
                            "Name", "http://schemas.microsoft.com/winfx/2006/xaml").Equals(name + "EasyReadIcon") &&
                        shield.GetAttribute(
                            "Name", "http://schemas.microsoft.com/winfx/2006/xaml").Equals(name + "Shield") &&
                        number.GetAttribute(
                            "Name", "http://schemas.microsoft.com/winfx/2006/xaml").Equals(name + "Number");
                }
            }
            for (int page = 1; page <= 2; page++)
            {
                for (int row = 1; row <= 8; row++)
                    completeBadgeGrid &= names.Contains($"BugfixesAndQoLTeamBadgePage{page}Row{row}");
            }
            Check(completeBadgeGrid,
                "HUD_MissionOver patch adds lower-left Vanilla and Easy Read icons to every row on both pages");

            string patchSource = File.ReadAllText(patchPath);
            Check(featureSource.Contains("getTeamAlliesShield(teamId, large: false)") &&
                    featureSource.Contains("snapshot.team_shield") &&
                    featureSource.Contains("statisticsTeamBadgeShields[page, row].Fill") &&
                    featureSource.Contains("number.Text = teamId.ToString();") &&
                    featureSource.Contains("host.Visibility = Visibility.Visible;") &&
                    featureSource.Contains("ReferenceEquals(snapshot, statisticsTeamBadgeSnapshot)") &&
                    featureSource.Contains("badgeMode == statisticsTeamBadgeMode"),
                "statistics team badges select Vanilla or Easy Read visuals and cache the chosen mode");
            Check(featureSource.Contains(
                        "private bool StatisticsTeamBadgesEnabled => settings.EnableMod && settings.EnableClientFeatures;") &&
                    featureSource.Contains("!StatisticsTeamBadgesEnabled") &&
                    featureSource.Contains("badgeMode == SurrenderPolicy.StatisticsTeamBadgesOff"),
                "statistics team badges use the independent client gate and support Off mode");
            Check(!patchSource.Contains("Background=") &&
                    !patchSource.Contains("MO_MP_PlayersVisible") &&
                    !patchSource.Contains("MO_MP_PlayersShields0}\" />"),
                "team-badge patch leaves Vanilla row backgrounds, visibility, and personal shield bindings unchanged");

            string viewModel = File.ReadAllText(Path.Combine("src", "BugfixesAndQoLViewModel.cs"));
            string settingsXaml = File.ReadAllText(Path.Combine(
                "Override", "ScriptExtenderUI", "BugfixesAndQoLSettings.xaml"));
            string plugin = File.ReadAllText(Path.Combine("src", "BugfixesAndQoLPlugin.cs"));
            Check(viewModel.Contains("new LocalPerPlayerSetting<int>(SurrenderPolicy.DefaultStatisticsTeamBadgeMode)") &&
                    viewModel.Contains("public int[] StatisticsTeamBadgeModeData") &&
                    viewModel.Contains("statisticsTeamBadgeMode.TrySetLocalPlayerId(playerId)") &&
                    viewModel.Contains("StatisticsTeamBadgeMode = SurrenderPolicy.DefaultStatisticsTeamBadgeMode;"),
                "statistics team-badge mode is stored and reset as a per-player client preference");
            Check(settingsXaml.Contains("Value=\"{Binding StatisticsTeamBadgeMode, Mode=TwoWay}\"") &&
                    settingsXaml.Contains("Minimum=\"0\" Maximum=\"2\"") &&
                    settingsXaml.Contains("StatisticsTeamBadgeVanillaPreviewVisibility") &&
                    settingsXaml.Contains("StatisticsTeamBadgeEasyReadPreviewVisibility") &&
                    settingsXaml.Contains("Fill=\"#FF508CCC\"") &&
                    settingsXaml.Contains("Text=\"3\""),
                "client slider exposes all three modes and previews Team 3 in both icon styles");
            Check(viewModel.Contains("getTeamAlliesShield(3, large: false)") &&
                    plugin.Contains("Settings.RefreshStatisticsTeamBadgePreviewVisual();"),
                "Team 3 Vanilla preview resolves at the existing safe ModSettings visual refresh point");

            bool allLocalesComplete = true;
            foreach (string localePath in Directory.GetFiles("Locales", "*.txt"))
            {
                string locale = File.ReadAllText(localePath);
                allLocalesComplete &= locale.Contains("BugfixesAndQoL.StatisticsTeamBadgeMode=") &&
                    locale.Contains("BugfixesAndQoL.StatisticsTeamBadgeModeHelp=") &&
                    locale.Contains("BugfixesAndQoL.StatisticsTeamBadgeModeOff=") &&
                    locale.Contains("BugfixesAndQoL.StatisticsTeamBadgeModeVanillaIcons=") &&
                    locale.Contains("BugfixesAndQoL.StatisticsTeamBadgeModeEasyReadIcons=");
            }
            Check(allLocalesComplete,
                "all locales define the statistics team-badge slider and its three values");
        }

        private static bool RowsEqual(int[] rows, params int[] expected)
        {
            if (rows == null || expected == null || rows.Length != expected.Length)
                return false;
            for (int index = 0; index < rows.Length; index++)
            {
                if (rows[index] != expected[index])
                    return false;
            }
            return true;
        }

        private static void TestResolutionAwareZoomPolicy()
        {
            Check(AlmostEqual(ResolutionAwareZoomPolicy.GetResolutionScale(720), 1f),
                "resolution-aware zoom leaves sub-1080p scale unchanged");
            Check(AlmostEqual(ResolutionAwareZoomPolicy.GetResolutionScale(1080), 1f),
                "resolution-aware zoom uses 1080p as its reference");
            Check(AlmostEqual(ResolutionAwareZoomPolicy.GetResolutionScale(1440), 4f / 3f),
                "resolution-aware zoom scales 1440p proportionally");
            Check(AlmostEqual(ResolutionAwareZoomPolicy.GetResolutionScale(2160), 2f),
                "resolution-aware zoom doubles the effective 4K scale");
            Check(AlmostEqual(ResolutionAwareZoomPolicy.GetEffectiveZoom(1f, 2160), 2f),
                "4K normal zoom matches the 1080p world height");

            float worldHeight1080 = 1080f / (1f * 64f);
            float worldHeight4K = 2160f / (
                ResolutionAwareZoomPolicy.GetEffectiveZoom(1f, 2160) * 64f);
            Check(AlmostEqual(worldHeight1080, worldHeight4K),
                "resolution normalization preserves visible vertical world size");
            float[] extendedFarZoomValues = { 0.175f };
            bool matchingExtendedFarWorldHeights = true;
            foreach (float farZoom in extendedFarZoomValues)
            {
                float farWorldHeight1080 = 1080f / (farZoom * 64f);
                float farWorldHeight4K = 2160f / (
                    ResolutionAwareZoomPolicy.GetEffectiveZoom(farZoom, 2160) * 64f);
                matchingExtendedFarWorldHeights &=
                    AlmostEqual(farWorldHeight1080, farWorldHeight4K);
            }
            Check(matchingExtendedFarWorldHeights,
                "the retained distant position preserves world height between 1080p and 4K");

            float retainedFarTileAreaFactor = (0.25f / 0.175f) * (0.25f / 0.175f);
            float removedExtremeTileAreaFactor = (0.25f / 0.125f) * (0.25f / 0.125f);
            Check(retainedFarTileAreaFactor < 2.1f &&
                    AlmostEqual(removedExtremeTileAreaFactor, 4f),
                "retained far zoom limits tile-area growth to about 2x instead of 4x");

            int[] tilemapSizes = { 160, 200, 300, 400, 500, 600, 700, 800, 1000 };
            bool matchingFullHdAnd4KPolicies = true;
            foreach (int tilemapSize in tilemapSizes)
            {
                matchingFullHdAnd4KPolicies &=
                    ResolutionAwareZoomPolicy.CanUserExtraZoom(1920, 1080, tilemapSize) ==
                    ResolutionAwareZoomPolicy.CanUserExtraZoom(3840, 2160, tilemapSize);
            }
            Check(matchingFullHdAnd4KPolicies,
                "1080p and 4K expose identical zoom positions for every Vanilla map size");
            Check(ResolutionAwareZoomPolicy.CanUserExtraZoom(1920, 1080, 400) &&
                    ResolutionAwareZoomPolicy.CanUserExtraZoom(3840, 2160, 400),
                "standard maps keep the distant 1080p zoom positions at 4K");
            Check(ResolutionAwareZoomPolicy.CanUserExtraZoom(1920, 1080, 300) &&
                    !ResolutionAwareZoomPolicy.CanUserExtraZoom(1920, 1080, 200) &&
                    !ResolutionAwareZoomPolicy.CanUserExtraZoom(1920, 1080, 160),
                "normalized policy retains Vanilla's small-map limits");
            Check(!ResolutionAwareZoomPolicy.CanUserExtraZoom(3440, 1440, 400),
                "normalized policy retains Vanilla's horizontal ultrawide limit");
            Check(ResolutionAwareZoomPolicy.CanUserExtraZoom(1280, 720, 160),
                "sub-1080p resolutions retain their unscaled Vanilla policy");

            Check(AlmostEqual(
                    ResolutionAwareZoomPolicy.GetLockedMinimumPosition(true, false, true, true),
                    0.5f) &&
                AlmostEqual(
                    ResolutionAwareZoomPolicy.GetLockedMinimumPosition(false, false, true, true),
                    2f),
                "extended far zoom only relaxes maps allowed by Vanilla's extra-zoom policy");
            Check(AlmostEqual(
                    ResolutionAwareZoomPolicy.GetLockedMinimumPosition(true, false, true, false),
                    1f) &&
                AlmostEqual(
                    ResolutionAwareZoomPolicy.GetLockedMinimumPosition(true, false, false, true),
                    1f) &&
                AlmostEqual(
                    ResolutionAwareZoomPolicy.GetLockedMinimumPosition(false, false, false, true),
                    2f) &&
                AlmostEqual(
                    ResolutionAwareZoomPolicy.GetLockedMinimumPosition(true, true, false, true),
                    0f),
                "whole steps, disabled feature and editor restore their Vanilla minimum positions");

            Check(AlmostEqual(
                    ResolutionAwareZoomPolicy.ResolvePosition(1f, -0.5f, true, true, true, false, false),
                    0.5f) &&
                AlmostEqual(
                    ResolutionAwareZoomPolicy.ResolvePosition(0.5f, -0.5f, true, true, true, false, false),
                    0.5f),
                "extra-zoom steps reach but do not pass the retained distant position");
            Check(AlmostEqual(
                    ResolutionAwareZoomPolicy.ResolvePosition(1f, -1f, true, true, false, false, false),
                    1f) &&
                AlmostEqual(
                    ResolutionAwareZoomPolicy.NormalizeLockedPosition(0.5f, true, false, true, false),
                    1f),
                "whole-step zoom retains Vanilla's far limit and normalizes a live half step");
            Check(AlmostEqual(
                    ResolutionAwareZoomPolicy.ResolvePosition(0.5f, -0.5f, true, true, true, false, true),
                    ResolutionAwareZoomPolicy.ExtendedLockedMaximumPosition) &&
                AlmostEqual(
                    ResolutionAwareZoomPolicy.ResolvePosition(4f, 0.5f, true, true, true, false, true),
                    0.5f),
                "cyclic zoom wraps between both extended limits");
            Check(AlmostEqual(
                    ResolutionAwareZoomPolicy.ResolvePosition(2f, -0.5f, true, false, true, false, false),
                    2f),
                "Vanilla-limited maps do not receive the additional distant positions");

            Check(AlmostEqual(
                    ResolutionAwareZoomPolicy.ResolvePosition(3f, 0.5f, true, true, true, false, false),
                    3.5f) &&
                AlmostEqual(
                    ResolutionAwareZoomPolicy.ResolvePosition(3.5f, 0.5f, true, true, true, false, false),
                    4f),
                "extra zoom reaches the added 1.5x and 2x close positions");
            Check(AlmostEqual(
                    ResolutionAwareZoomPolicy.ResolvePosition(3f, 1f, true, true, false, false, false),
                    4f),
                "whole-step zoom reaches the extended maximum directly");
            Check(AlmostEqual(
                    ResolutionAwareZoomPolicy.ResolvePosition(4f, 0.5f, true, false, true, false, true),
                    2f),
                "locked cyclic zoom wraps to Vanilla's resolution-limited minimum");
            Check(AlmostEqual(
                    ResolutionAwareZoomPolicy.ResolvePosition(0f, -1f, true, true, false, true, false),
                    0f) &&
                AlmostEqual(
                    ResolutionAwareZoomPolicy.ResolvePosition(5f, 1f, false, true, false, false, false),
                    5f),
                "editor minimum and unlocked-camera limits retain Vanilla behavior");
        }

        private static void TestNotificationSkipPolicy()
        {
            Check(NotificationSkipPolicy.ShouldArmVideo(true, true, true, true, true, false),
                "active visible queued video notification arms complete skip");
            Check(!NotificationSkipPolicy.ShouldArmVideo(false, true, true, true, true, false) &&
                  !NotificationSkipPolicy.ShouldArmVideo(true, false, true, true, true, false) &&
                  !NotificationSkipPolicy.ShouldArmVideo(true, true, false, true, true, false) &&
                  !NotificationSkipPolicy.ShouldArmVideo(true, true, true, false, true, false) &&
                  !NotificationSkipPolicy.ShouldArmVideo(true, true, true, true, false, false) &&
                  !NotificationSkipPolicy.ShouldArmVideo(true, true, true, true, true, true),
                "disabled, idle, videoless, hidden, stopped, and briefing video notifications remain untouched");
            Check(NotificationSkipPolicy.ShouldArmMinimap(true, true, false, false),
                "active videoless notification arms complete skip from the minimap");
            Check(!NotificationSkipPolicy.ShouldArmMinimap(false, true, false, false) &&
                  !NotificationSkipPolicy.ShouldArmMinimap(true, false, false, false) &&
                  !NotificationSkipPolicy.ShouldArmMinimap(true, true, true, false) &&
                  !NotificationSkipPolicy.ShouldArmMinimap(true, true, false, true),
                "disabled, idle, video, and briefing notifications do not arm minimap skip");
            Check(NotificationSkipPolicy.ShouldCompleteOnRightClick(true, true, true),
                "single right-click event on an armed notification surface completes it");
            Check(!NotificationSkipPolicy.ShouldCompleteOnRightClick(false, true, true) &&
                  !NotificationSkipPolicy.ShouldCompleteOnRightClick(true, false, true) &&
                  !NotificationSkipPolicy.ShouldCompleteOnRightClick(true, true, false),
                "unarmed, left-click, and non-single-click events do not complete notifications");
            Check(NotificationSkipPolicy.AudioPathMatches(
                      "fx\\speech\\Random_Events14.wav", "random_events14.wav") &&
                  NotificationSkipPolicy.AudioPathMatches(
                      "folder/Random_Events14.wav", "C:\\audio\\random_events14.wav") &&
                  !NotificationSkipPolicy.AudioPathMatches(
                      "Random_Events14.wav", "unrelated.wav") &&
                  !NotificationSkipPolicy.AudioPathMatches(string.Empty, "Random_Events14.wav"),
                "notification audio loads are matched by portable case-insensitive filename");
        }

        private static void TestNotificationQueueNearCallResolver()
        {
            byte[] validCall = { 0x90, 0xE8, 0x04, 0x00, 0x00, 0x00, 0x90, 0x90, 0x90, 0x90, 0xC3 };
            Check(NotificationQueueNativeContract.ResolveNearCallTarget(validCall, 1) == 10,
                "production near-call resolver reads rel32 after E8 and uses the five-byte return address");

            bool wrongOpcodeRejected = false;
            try
            {
                NotificationQueueNativeContract.ResolveNearCallTarget(new byte[] { 0xE9, 0, 0, 0, 0 }, 0);
            }
            catch (InvalidOperationException)
            {
                wrongOpcodeRejected = true;
            }

            bool truncatedCallRejected = false;
            try
            {
                NotificationQueueNativeContract.ResolveNearCallTarget(new byte[] { 0xE8, 0, 0, 0 }, 0);
            }
            catch (InvalidOperationException)
            {
                truncatedCallRejected = true;
            }

            Check(wrongOpcodeRejected && truncatedCallRejected,
                "production near-call resolver rejects wrong opcodes and truncated calls");
        }

        private static void TestAllyGoodsTransferPolicy()
        {
            Check(AllyGoodsTransferPolicy.ShouldForceConfirmVisible(
                    true, true, true, 2, 25),
                "ally goods confirm remains visible for a valid send selection");
            Check(!AllyGoodsTransferPolicy.ShouldForceConfirmVisible(
                    false, true, true, 2, 25) &&
                  !AllyGoodsTransferPolicy.ShouldForceConfirmVisible(
                    true, false, true, 2, 25),
                "disabled client features or ally amount setting preserve Vanilla visibility");
            Check(!AllyGoodsTransferPolicy.ShouldForceConfirmVisible(
                    true, true, false, 2, 25),
                "ally goods requests preserve Vanilla visibility");
            Check(!AllyGoodsTransferPolicy.ShouldForceConfirmVisible(
                    true, true, true, 0, 25) &&
                  !AllyGoodsTransferPolicy.ShouldForceConfirmVisible(
                    true, true, true, 25, 25) &&
                  !AllyGoodsTransferPolicy.ShouldForceConfirmVisible(
                    true, true, true, 2, 0),
                "invalid goods or non-positive amounts do not expose ally goods confirm");
            Check(AllyGoodsTransferPolicy.IsSendSuccessful(0) &&
                  !AllyGoodsTransferPolicy.IsSendSuccessful(1) &&
                  !AllyGoodsTransferPolicy.IsSendSuccessful(-1) &&
                  !AllyGoodsTransferPolicy.IsSendRejected(0) &&
                  !AllyGoodsTransferPolicy.IsSendRejected(-1) &&
                  AllyGoodsTransferPolicy.IsSendRejected(1) &&
                  AllyGoodsTransferPolicy.IsSendRejected(int.MaxValue),
                "only ally goods result zero succeeds and positive results reject");
        }

        private static void TestAllyGoodsTransferIntegration()
        {
            string hook = File.ReadAllText(Path.Combine("src", "AllyGoodsAmountModifierHook.cs"));
            string runtime = File.ReadAllText(Path.Combine("src", "BugfixesAndQoLRuntime.cs"));
            int confirmationStart = hook.IndexOf(
                "private void HandleSendConfirmation",
                StringComparison.Ordinal);
            int confirmationEnd = hook.IndexOf(
                "private void UpdateGoodsHook",
                confirmationStart,
                StringComparison.Ordinal);
            string confirmationHandler = hook.Substring(
                confirmationStart,
                confirmationEnd - confirmationStart);

            Check(hook.Split(new[] { "GameActionCommand.Ally_SendGoods" },
                      StringSplitOptions.None).Length == 2 &&
                  hook.Contains("int result = EngineInterface.GameAction(") &&
                  hook.Contains("IsSendSuccessful(result)") &&
                  hook.Contains("IsSendRejected(result)") &&
                  !confirmationHandler.Contains("buttonClickedTrampoline") &&
                  !confirmationHandler.Contains("SetValue("),
                "ally goods confirmation submits exactly one Vanilla action and preserves the open panel and selection on success");
            Check(hook.Contains("Space_Warning7.wav") &&
                  hook.IndexOf("IsSendRejected(result)", StringComparison.Ordinal) <
                  hook.IndexOf("Space_Warning7.wav", StringComparison.Ordinal) &&
                  hook.Contains("the panel remains open and the action is not retried"),
                "rejected ally goods sends warn, remain open and are never retried");
            Check(hook.Contains("updateGoodsTrampoline(self);") &&
                  hook.IndexOf(
                      "ShouldForceConfirmVisible(",
                      hook.IndexOf("updateGoodsTrampoline(self);", StringComparison.Ordinal),
                      StringComparison.Ordinal) >
                  hook.IndexOf("updateGoodsTrampoline(self);", StringComparison.Ordinal) &&
                  hook.Contains("Allies_GoodConfirmVis = true") &&
                  !hook.Contains("OnTick") &&
                  !hook.Contains("onBeforeRender"),
                "ally goods visibility extends Vanilla UpdateGoods without polling");
            Check(hook.Contains("Allies_SendGoodsViewVis") &&
                  !hook.Contains("GameActionCommand.Ally_RequestGoods"),
                "ally request confirmation remains owned by Vanilla");
            int refreshStart = hook.IndexOf(
                "internal void RefreshSetting()",
                StringComparison.Ordinal);
            int refreshEnd = hook.IndexOf(
                "internal static int CalculateAmount",
                refreshStart,
                StringComparison.Ordinal);
            string refreshSetting = hook.Substring(refreshStart, refreshEnd - refreshStart);
            Check(refreshSetting.Contains("RefreshDisplayedAmounts();") &&
                  !refreshSetting.Contains("MainViewModel.Instance") &&
                  !refreshSetting.Contains("MainViewModel.viewModelLoaded") &&
                  !refreshSetting.Contains("updateGoodsMethod.Invoke") &&
                  !refreshSetting.Contains("OnTick") &&
                  !refreshSetting.Contains("onBeforeRender"),
                "ally goods setting refresh updates bindings without touching the MainViewModel factory or polling");
            Check(runtime.Contains(
                    "private static AllyGoodsAmountModifierHook processAllyGoodsAmountModifierHook;") &&
                  runtime.Contains("processAllyGoodsAmountModifierHook = candidate;") &&
                  !runtime.Contains("processAllyGoodsAmountModifierHook?.Dispose") &&
                  !hook.Contains("public void Dispose()"),
                "ally goods hook is process-rooted and never torn down normally");
        }

        private static void TestPlacementCancelMoveSuppressionPolicy()
        {
            Check(!PlacementCancelRightClickPolicy.ShouldForwardRightDown(
                    true, true, true, 5, false),
                "DE right-click placement cancellation is not forwarded to the engine");
            Check(PlacementCancelRightClickPolicy.ShouldForwardRightDown(
                    true, true, true, 0, false) &&
                  PlacementCancelRightClickPolicy.ShouldForwardRightDown(
                    true, true, true, 3, false),
                "ordinary and non-building right-clicks retain Vanilla forwarding");
            Check(PlacementCancelRightClickPolicy.ShouldForwardRightDown(
                    true, true, true, 5, true),
                "SH1 controls retain Vanilla placement-cancel forwarding");
            Check(PlacementCancelRightClickPolicy.ShouldForwardRightDown(
                    false, true, true, 5, false) &&
                  PlacementCancelRightClickPolicy.ShouldForwardRightDown(
                    true, false, true, 5, false) &&
                  PlacementCancelRightClickPolicy.ShouldForwardRightDown(
                    true, true, false, 5, false),
                "disabled mod, client features, or local option retain Vanilla forwarding");

            bool suppressRightUp = false;
            bool placementDown = PlacementCancelRightClickPolicy.BeginRightClickGesture(
                true, true, true, 5, false, ref suppressRightUp);
            bool placementUp = PlacementCancelRightClickPolicy.CompleteRightClickGesture(
                ref suppressRightUp);
            bool followingUp = PlacementCancelRightClickPolicy.CompleteRightClickGesture(
                ref suppressRightUp);
            Check(!placementDown && !placementUp && followingUp && !suppressRightUp,
                "DE placement cancellation suppresses Down and exactly one matching Up");

            suppressRightUp = true;
            bool ordinaryDown = PlacementCancelRightClickPolicy.BeginRightClickGesture(
                true, true, true, 0, false, ref suppressRightUp);
            bool ordinaryUp = PlacementCancelRightClickPolicy.CompleteRightClickGesture(
                ref suppressRightUp);
            Check(ordinaryDown && ordinaryUp && !suppressRightUp,
                "a later ordinary Down replaces a stale placement-cancel Up marker");

            suppressRightUp = false;
            bool disabledDown = PlacementCancelRightClickPolicy.BeginRightClickGesture(
                true, true, false, 5, false, ref suppressRightUp);
            Check(disabledDown &&
                  PlacementCancelRightClickPolicy.CompleteRightClickGesture(ref suppressRightUp),
                "disabled placement suppression forwards the complete right-click gesture");
        }

        private static void TestPlacementCancelMoveSuppressionIntegration()
        {
            string feature = File.ReadAllText(
                Path.Combine("src", "PlacementCancelMoveSuppressionFeature.cs"));
            string runtime = File.ReadAllText(Path.Combine("src", "BugfixesAndQoLRuntime.cs"));
            string viewModel = File.ReadAllText(Path.Combine("src", "BugfixesAndQoLViewModel.cs"));
            string project = File.ReadAllText("BugfixesAndQoL.csproj");
            string settingsXaml = File.ReadAllText(Path.Combine(
                "Override", "ScriptExtenderUI", "BugfixesAndQoLSettings.xaml"));

            Check(feature.Contains("new ILHook(updateMethod, PatchRightClickBranch)") &&
                  feature.Contains("cursor.RemoveRange(4)") &&
                  feature.Contains("CancelPlacementAndGetRightDown") &&
                  feature.Contains("GetRightUpForEngine") &&
                  feature.Contains("rightUpCursor.EmitDelegate<Func<bool>>") &&
                  feature.Contains("controls.StopAllPlacement();") &&
                  feature.Contains("return forwardRightDown;") &&
                  feature.Contains("rightDownMatches.Count != 1 || rightUpMatches.Count != 1"),
                "placement cancellation replaces both unique Vanilla right-click gesture IL blocks before engine input");
            Check(!feature.Contains("OnTribeIssueOrderMoveHere") &&
                  !feature.Contains("GetSelectedChimps") &&
                  !feature.Contains("PlacementCancelMoveSuppressionState") &&
                  !feature.Contains("Input.GetMouseButtonDown") &&
                  !feature.Contains("new Hook(updateMethod") &&
                  !feature.Contains("EngineRun") &&
                  !feature.Contains("GameTimeManagerAPI") &&
                  !feature.Contains("OnTick") &&
                  !feature.Contains("InputR3EventHooks"),
                "placement-cancel suppression has no MoveHere, selection, frame-detour, held-key, engine-run, or tick callback");
            Check(feature.Contains("bool forwardRightDown = true;") &&
                  feature.Contains("suppressNextRightUp = false;") &&
                  feature.Contains("catch (Exception ex)") &&
                  feature.Split(new[] { "controls.StopAllPlacement();" }, StringSplitOptions.None).Length == 2,
                "placement-cancel classification fails open and invokes Vanilla cleanup exactly once");
            Check(!feature.Contains("LogDebug(") &&
                  feature.Split(new[] { "LogError(" }, StringSplitOptions.None).Length == 2 &&
                  feature.Contains("if (!classificationFailureLogged)") &&
                  feature.Contains("classificationFailureLogged = true;"),
                "placement-cancel suppression logs only its once-guarded fail-open classification error");
            Check(runtime.Contains("private static PlacementCancelMoveSuppressionFeature processPlacementCancelMoveSuppressionFeature;") &&
                  runtime.Contains("EnsurePlacementCancelMoveSuppressionFeature);") &&
                  !runtime.Contains("processPlacementCancelMoveSuppressionFeature?.Dispose"),
                "placement-cancel runtime is process-rooted and never torn down normally");
            Check(project.Contains("PlacementCancelMoveSuppressionFeature.cs") &&
                  project.Contains("PlacementCancelRightClickPolicy.cs") &&
                  project.Contains("MonoMod.Utils") &&
                  project.Contains("Mono.Cecil"),
                "placement-cancel IL implementation and dependencies are compiled into the runtime project");
            Check(viewModel.Contains("new LocalPerPlayerSetting<bool>(true)") &&
                  viewModel.Contains("public bool PreventMoveOrderOnPlacementCancel") &&
                  viewModel.Contains("preventMoveOrderOnPlacementCancel.TrySetLocalPlayerId(playerId)") &&
                  settingsXaml.Contains("PreventMoveOrderOnPlacementCancel, Mode=TwoWay"),
                "placement-cancel suppression is an enabled-by-default per-player client setting");

            Check(!File.ReadAllText(Path.Combine("src", "AssassinPathfindingRuntime.cs"))
                    .Contains("if (args.SkipOriginalFunction)" +
                        Environment.NewLine + "                return;") &&
                  !File.ReadAllText(Path.Combine("src", "ExtendedShiftCommandQueueRuntime.cs"))
                    .Contains("if (!installed || args.SkipOriginalFunction)") &&
                  !File.ReadAllText(Path.Combine("src", "FastRecruitRallyMovementRuntime.cs"))
                    .Contains("if (!args.SkipOriginalFunction &&") &&
                  !File.ReadAllText(Path.Combine("src", "FriendlyMoatMovementRuntime.cs"))
                    .Contains("if (disposed || args.SkipOriginalFunction)") &&
                  !File.ReadAllText(Path.Combine("src", "TroopMovementFix3Runtime.cs"))
                    .Contains("if (!IsFeatureEnabled || args.SkipOriginalFunction"),
                "placement-cancel handling no longer patches individual MoveHere consumers");

            MethodInfo update = typeof(EditorDirector).GetMethod(
                "Update",
                BindingFlags.Instance | BindingFlags.NonPublic);
            byte[] il = update?.GetMethodBody()?.GetILAsByteArray();
            int contractMatches = 0;
            int rightUpContractMatches = 0;
            if (il != null)
            {
                for (int offset = 0; offset <= il.Length - 17; offset++)
                {
                    if (il[offset] != 0x7e || il[offset + 5] != 0x6f ||
                        il[offset + 10] != 0x02 || il[offset + 11] != 0x17 ||
                        il[offset + 12] != 0x7d)
                    {
                        continue;
                    }

                    try
                    {
                        FieldInfo controlsInstance = update.Module.ResolveField(
                            BitConverter.ToInt32(il, offset + 1)) as FieldInfo;
                        MethodInfo stopPlacement = update.Module.ResolveMethod(
                            BitConverter.ToInt32(il, offset + 6)) as MethodInfo;
                        FieldInfo rightDown = update.Module.ResolveField(
                            BitConverter.ToInt32(il, offset + 13)) as FieldInfo;
                        if (controlsInstance?.DeclaringType == typeof(MainControls) &&
                            controlsInstance.Name == "instance" &&
                            stopPlacement?.DeclaringType == typeof(MainControls) &&
                            stopPlacement.Name == nameof(MainControls.StopAllPlacement) &&
                            stopPlacement.GetParameters().Length == 0 &&
                            rightDown?.DeclaringType == typeof(EditorDirector) &&
                            rightDown.Name == "rightDownForEngine")
                        {
                            contractMatches++;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Operand bytes can resemble opcodes but cannot resolve as this contract.
                    }
                }

                for (int offset = 0; offset <= il.Length - 15; offset++)
                {
                    if (il[offset] != 0x17 || il[offset + 1] != 0x28 ||
                        il[offset + 6] != 0x2c || il[offset + 8] != 0x02 ||
                        il[offset + 9] != 0x17 || il[offset + 10] != 0x7d)
                    {
                        continue;
                    }

                    try
                    {
                        MethodInfo getMouseButtonUp = update.Module.ResolveMethod(
                            BitConverter.ToInt32(il, offset + 2)) as MethodInfo;
                        FieldInfo rightUp = update.Module.ResolveField(
                            BitConverter.ToInt32(il, offset + 11)) as FieldInfo;
                        if (getMouseButtonUp?.DeclaringType?.FullName == "UnityEngine.Input" &&
                            getMouseButtonUp.Name == "GetMouseButtonUp" &&
                            getMouseButtonUp.GetParameters().Length == 1 &&
                            rightUp?.DeclaringType == typeof(EditorDirector) &&
                            rightUp.Name == "rightUpForEngine")
                        {
                            rightUpContractMatches++;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Operand bytes can resemble opcodes but cannot resolve as this contract.
                    }
                }
            }
            Check(contractMatches == 1 && rightUpContractMatches == 1,
                "installed Assembly-CSharp contains exactly one expected right-click Down and Up IL block");

            bool allLocalesComplete = true;
            foreach (string locale in Directory.GetFiles("Locales", "*.txt"))
            {
                string text = File.ReadAllText(locale);
                allLocalesComplete &=
                    text.Contains("BugfixesAndQoL.PreventMoveOrderOnPlacementCancel=") &&
                    text.Contains("BugfixesAndQoL.PreventMoveOrderOnPlacementCancelHelp=");
            }
            Check(allLocalesComplete,
                "all locales contain placement-cancel setting labels and help text");
        }

        private static void TestNotificationManagedContracts()
        {
            Type ostType = typeof(OnScreenText.OST);
            Check(ostType.GetField("active")?.FieldType == typeof(bool) &&
                  ostType.GetField("activeThisFrame")?.FieldType == typeof(bool) &&
                  ostType.GetField("wasTurnedOnOrChanged")?.FieldType == typeof(bool) &&
                  ostType.GetField("wasTurnedOff")?.FieldType == typeof(bool) &&
                  ostType.GetField("timedEnd")?.FieldType == typeof(DateTime),
                "message-bar OST state fields retain their managed types");
            MethodInfo getOst = typeof(OnScreenText).GetMethod(
                "getOST",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(Enums.eOnScreenText),
                    typeof(bool).MakeByRefType(),
                    typeof(bool).MakeByRefType(),
                    typeof(bool)
                },
                null);
            PropertyInfo visibility = typeof(MainViewModel).GetProperty(
                "OST_Message_Bar_Vis",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Check(getOst?.ReturnType == ostType &&
                  visibility?.PropertyType == typeof(bool) &&
                  visibility.CanWrite,
                "message-bar lookup and visibility contracts remain writable");
        }

        private static void TestNotificationSkipIntegration()
        {
            string feature = File.ReadAllText(Path.Combine("src", "NotificationSkipFeature.cs"));
            string behavior = File.ReadAllText(Path.Combine("src", "NotificationSkipBehavior.cs"));
            string viewModel = File.ReadAllText(Path.Combine("src", "BugfixesAndQoLViewModel.cs"));
            string runtime = File.ReadAllText(Path.Combine("src", "BugfixesAndQoLRuntime.cs"));
            string xaml = File.ReadAllText(Path.Combine(
                "Override", "ScriptExtenderUI", "BugfixesAndQoLSettings.xaml"));
            string mainHudPatchPath = Path.Combine(
                "Patches", "Assets", "GUI", "XAML", "MainHUD.xaml");
            string mainHudPatch = File.ReadAllText(mainHudPatchPath);

            Check(feature.Contains("MainViewModel.Instance.HUDRoot.RadarME_Ended();") &&
                  feature.Contains("StopSpeechChannel1(MyAudioManager.Instance);") &&
                  feature.Contains("Enums.eOnScreenText.OST_MESSAGE_BAR") &&
                  feature.Contains("MainViewModel.Instance.OST_Message_Bar_Vis = false;") &&
                  feature.Contains("ImmediateCommandIdOffset") &&
                  !feature.Contains("StopAllGameSounds"),
                "notification right-click closes video, stops only channel 1, hides only the message bar, and marks queue state");
            Check(behavior.Contains("element.PreviewMouseDown += OnPreviewMouseDown") &&
                  behavior.Contains("using NoesisApp;") &&
                  behavior.Contains("sender is MediaElement") &&
                  behavior.Contains("sender is Image") &&
                  behavior.Contains("args.ChangedButton != MouseButton.Right") &&
                  feature.Contains("args.Handled = true;") &&
                  !feature.Contains("RadarScrollMap") &&
                  !feature.Contains("Input.GetMouseButtonDown") &&
                  !feature.Contains("OnLoadRadarGrid") &&
                  !behavior.Contains("Input.GetMouseButtonDown") &&
                  !behavior.Contains(".MouseDown") &&
                  !behavior.Contains("MouseLeftButton") &&
                  !behavior.Contains("void Update("),
                "complete notification skip uses only notification-surface right-click UI events and no polling or HUD hook");
            Check(behavior.IndexOf("args.ChangedButton != MouseButton.Right", StringComparison.Ordinal) <
                      behavior.IndexOf("CompleteFromNotificationSurfaceRightClick(surface, args)", StringComparison.Ordinal) &&
                  behavior.Contains("private static NotificationSkipFeature feature;") &&
                  feature.Contains("NotificationSkipBehavior.Configure(this);") &&
                  feature.Contains("source?.Stop();"),
                "left-click exits in the static behavior before feature inspection and paused channel 1 stops");
            Check(feature.Contains("surface == NotificationSkipSurface.Minimap") &&
                  feature.Contains("NotificationSkipPolicy.ShouldArmMinimap") &&
                  feature.Contains("if (!hasVideo)") &&
                  feature.Contains("if (hasVideo)") &&
                  feature.Contains("if (hasAudio)") &&
                  feature.Contains("ImmediateAudioPathOffset") &&
                  feature.Contains("NotificationSkipPolicy.AudioPathMatches") &&
                  feature.Contains("The immediate notification audio path is not null-terminated"),
                "video and videoless minimap clicks are classified separately, including a transparent RadarME overlay, and only matching notification audio is stopped");
            Check(feature.Contains("initializationStage = \"channel-1 speech hook\"") &&
                  feature.Contains("initializationStage = \"native notification-update detour\"") &&
                  feature.Contains("RollbackFailedInitialization(pendingLoadHook, pendingNativeTransaction)") &&
                  feature.IndexOf("speechSource1Field = FindField", StringComparison.Ordinal) <
                      feature.IndexOf("pendingLoadHook = new Hook", StringComparison.Ordinal) &&
                  feature.IndexOf("pendingLoadHook = new Hook", StringComparison.Ordinal) <
                      feature.IndexOf("pendingNativeTransaction.Commit()", StringComparison.Ordinal) &&
                  feature.IndexOf("NotificationSkipBehavior.Configure(this);", StringComparison.Ordinal) <
                      feature.IndexOf("catch (Exception ex)", StringComparison.Ordinal),
                "audio and native hook failures have distinct context and share fail-closed publication");
            int updateCallback = feature.IndexOf(
                "private void UpdateNotificationQueue(IntPtr manager)", StringComparison.Ordinal);
            int messageBarHelper = feature.IndexOf(
                "private static void HideMessageBar()", StringComparison.Ordinal);
            Check(updateCallback >= 0 && messageBarHelper > updateCallback &&
                  feature.IndexOf("finalizeNotification(manager)", StringComparison.Ordinal) > updateCallback &&
                  feature.IndexOf("finalizeNotification(manager)", StringComparison.Ordinal) < messageBarHelper &&
                  feature.IndexOf("finalizeNotification(manager)",
                      feature.IndexOf("finalizeNotification(manager)", StringComparison.Ordinal) + 1,
                      StringComparison.Ordinal) < 0 &&
                  feature.Contains("notificationUpdateHook.Original(manager);") &&
                  !feature.Contains("0x102C30"),
                "central single-message finalizer is called only from the validated native update callback");
            Check(feature.Contains("PendingSkipRequest") &&
                  feature.Contains("expectedPresentationId") &&
                  feature.Contains("complete notification-skip hooks installed for the process lifetime") &&
                  feature.Contains("discarded notification completion for an unexpected manager") &&
                  feature.Contains("discarded stale notification completion") &&
                  feature.Contains("complete notification skip failed after activation") &&
                  feature.Contains("native notification completion failed") &&
                  !feature.Contains("requested right-click notification completion") &&
                  !feature.Contains("completed the right-clicked notification centrally") &&
                  !feature.Contains("queuedAtClick") &&
                  !feature.Contains("elapsedMs=") &&
                  !feature.Contains("skipRequestGeneration") &&
                  !feature.Contains("RequestedTimestamp"),
                "notification requests remain identity-bound while routine click logs and diagnostic-only state are omitted");
            Check(feature.Contains("speechChannel1Generation") &&
                  feature.Contains("loadedClip?.UnloadAudioData();") &&
                  feature.Contains("generation != Volatile.Read"),
                "superseded channel-1 loads cannot replay skipped notification audio");
            Check(feature.Contains("RollbackFailedInitialization") &&
                  !feature.Contains("public void Dispose()"),
                "notification hooks are process-lifetime rooted with initialization-only rollback");
            Check(runtime.Contains("private NotificationSkipFeature notificationSkipFeature;") &&
                  runtime.Contains("new NotificationSkipFeature(") &&
                  runtime.Contains("nativeRegion,") &&
                  runtime.Contains("newLibraryHandle)"),
                "notification feature is retained by the process-lifetime runtime");
            Check(!feature.Contains("GameSoundManagerAPI") &&
                  !feature.Contains("SetSuppressMessages") &&
                  !feature.Contains("0x1031B0"),
                "notification completion does not interfere with Script Extender or fixes-mod message suppression at enqueue time");
            Check(viewModel.Contains("new LocalPerPlayerSetting<bool>(true)") &&
                  viewModel.Contains("public bool EnableCompleteNotificationSkipOnClick") &&
                  viewModel.Contains("EnableCompleteNotificationSkipOnClick = true;") &&
                  viewModel.Contains("enableCompleteNotificationSkipOnClick.TrySetLocalPlayerId(playerId)"),
                "complete notification skip is a default-on per-player preference");
            Check(xaml.Contains("bugfixes.enable-complete-notification-skip-on-click") &&
                  xaml.Contains("IsChecked=\"{Binding EnableCompleteNotificationSkipOnClick, Mode=TwoWay}\""),
                "complete notification skip is exposed in searchable client UI");
            var patchDocument = new XmlDocument();
            patchDocument.LoadXml(mainHudPatch);
            XmlNodeList patchOperations = patchDocument.SelectNodes("/Patch/Operation");
            Check(patchOperations.Count == 5 &&
                  mainHudPatch.Contains(
                      "xmlns:bugfixes=\"clr-namespace:BugfixesAndQoL;assembly=BugfixesAndQoL\"") &&
                  patchOperations[0].Attributes?["Type"]?.Value == "AddNamespace" &&
                  patchOperations[0].Attributes?["AttributeName"]?.Value == "bugfixes" &&
                  patchOperations[0].Attributes?["Value"]?.Value ==
                      "clr-namespace:BugfixesAndQoL;assembly=BugfixesAndQoL" &&
                  patchOperations[1].Attributes?["Type"]?.Value == "SetAttribute" &&
                  patchOperations[1].Attributes?["XPath"]?.Value ==
                      "//n:MediaElement[@Name='RadarME']" &&
                  patchOperations[1].Attributes?["AttributeName"]?.Value ==
                      "bugfixes:NotificationSkipBehavior.IsEnabled" &&
                  patchOperations[1].Attributes?["Value"]?.Value == "True" &&
                  patchOperations[2].Attributes?["Type"]?.Value == "SetAttribute" &&
                  patchOperations[2].Attributes?["XPath"]?.Value ==
                      "//n:MediaElement[@Name='RadarME']" &&
                  patchOperations[2].Attributes?["AttributeName"]?.Value ==
                      "bugfixes:MinimapInputBehavior.IsEnabled" &&
                  patchOperations[2].Attributes?["Value"]?.Value == "True" &&
                  patchOperations[3].Attributes?["Type"]?.Value == "SetAttribute" &&
                  patchOperations[3].Attributes?["XPath"]?.Value ==
                      "//n:Image[@Name='RadarMapImage']" &&
                  patchOperations[3].Attributes?["AttributeName"]?.Value ==
                      "bugfixes:NotificationSkipBehavior.IsEnabled" &&
                  patchOperations[3].Attributes?["Value"]?.Value == "True" &&
                  patchOperations[4].Attributes?["Type"]?.Value == "SetAttribute" &&
                  patchOperations[4].Attributes?["XPath"]?.Value ==
                      "//n:Image[@Name='RadarMapImage']" &&
                  patchOperations[4].Attributes?["AttributeName"]?.Value ==
                      "bugfixes:MinimapInputBehavior.IsEnabled" &&
                  patchOperations[4].Attributes?["Value"]?.Value == "True" &&
                  !mainHudPatch.Contains("RadarMapGrid"),
                "MainHUD patch targets only RadarME and RadarMapImage with both click behaviors");

            string projectDirectory = FindProjectDirectory();
            string baselineRoot = Path.Combine(
                Directory.GetParent(projectDirectory).FullName,
                "_inspect",
                "CrusaderDE-Native-Baseline");
            var currentBaseline = (IDictionary<string, object>)Shared.DependencyFreeJson.Parse(
                File.ReadAllText(Path.Combine(baselineRoot, "CURRENT.json")));
            string semanticDirectory = (string)currentBaseline["semanticDirectory"];
            string canonicalMainHudPath = Path.Combine(
                baselineRoot,
                semanticDirectory.Replace('/', Path.DirectorySeparatorChar),
                "resources",
                "xaml",
                "Assets",
                "GUI",
                "XAML",
                "MainHUD.xaml");
            var canonicalMainHud = new XmlDocument();
            canonicalMainHud.Load(canonicalMainHudPath);
            var canonicalNamespaces = new XmlNamespaceManager(canonicalMainHud.NameTable);
            canonicalNamespaces.AddNamespace("n", canonicalMainHud.DocumentElement.NamespaceURI);
            Check(canonicalMainHud.SelectNodes(
                      "//n:MediaElement[@Name='RadarME']",
                      canonicalNamespaces)?.Count == 1,
                "current canonical MainHUD contains exactly one RadarME target for the patch XPath");
            Check(canonicalMainHud.SelectNodes(
                      "//n:Image[@Name='RadarMapImage']",
                      canonicalNamespaces)?.Count == 1,
                "current canonical MainHUD contains exactly one RadarMapImage target for the patch XPath");
            foreach (string locale in Directory.GetFiles("Locales", "*.txt"))
            {
                string text = File.ReadAllText(locale);
                Check(text.Contains("BugfixesAndQoL.EnableCompleteNotificationSkipOnClick=") &&
                      text.Contains("BugfixesAndQoL.EnableCompleteNotificationSkipOnClickHelp="),
                    "complete notification skip localization exists in " + Path.GetFileName(locale));
            }

            Type audioType = typeof(MyAudioManager);
            BindingFlags members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type mainHudType = audioType.Assembly.GetType("CrusaderDE.MainHUD");
            Type radarMediaType = mainHudType?.GetField("RefRadarME", members)?.FieldType;
            Check(radarMediaType?.GetEvent("PreviewMouseDown")?.EventHandlerType?.FullName ==
                      "Noesis.MouseButtonEventHandler",
                "installed Vanilla RadarME exposes the Noesis mouse-event contract used by the behavior");
            Check(audioType.GetField("speechSource1", members)?.FieldType == typeof(UnityEngine.AudioSource) &&
                  audioType.GetField("speechClip1", members)?.FieldType == typeof(UnityEngine.AudioClip) &&
                  audioType.GetField("speechMode1", members)?.FieldType == typeof(int) &&
                  audioType.GetField("speechPaused", members)?.FieldType == typeof(bool) &&
                  audioType.GetField("ignoreSpeechMuting", members)?.FieldType == typeof(bool) &&
                  audioType.GetMethod(
                      "LoadClip",
                      members,
                      null,
                      new[] { typeof(int), typeof(string), typeof(string), typeof(bool), typeof(bool) },
                      null)?.ReturnType == typeof(void) &&
                  audioType.GetMethod(
                      "LoadClip",
                      members,
                      null,
                      new[] { typeof(string) },
                      null)?.ReturnType == typeof(System.Threading.Tasks.Task<UnityEngine.AudioClip>),
                "installed Vanilla channel-1 reflection contract matches the notification feature");
        }

        private static void TestMinimapInputIntegration()
        {
            string input = File.ReadAllText(Path.Combine("src", "MinimapInputFeature.cs"));
            string runtime = File.ReadAllText(Path.Combine("src", "BugfixesAndQoLRuntime.cs"));
            string project = File.ReadAllText("BugfixesAndQoL.csproj");

            Check(input.Contains("element.PreviewMouseDown += OnPreviewMouseDown") &&
                  input.Contains("captureElement.PreviewMouseMove += OnPreviewMouseMove") &&
                  input.Contains("captureElement.PreviewMouseUp += OnPreviewMouseUp") &&
                  input.Contains("captureElement.LostMouseCapture += OnLostMouseCapture") &&
                  input.Contains("captureElement.CaptureMouse()") &&
                  input.Contains("element.ReleaseMouseCapture()"),
                "minimap input attaches movement and release events only for a captured gesture");
            Check(input.IndexOf("TryBeginGesture(sender, args", StringComparison.Ordinal) <
                      input.IndexOf("captureElement.PreviewMouseMove +=", StringComparison.Ordinal) &&
                  input.IndexOf("gestureElement = null;", StringComparison.Ordinal) <
                      input.IndexOf("element.PreviewMouseMove -=", StringComparison.Ordinal),
                "minimap gesture callbacks are dynamically armed and fail-safe detached");
            Check(!input.Contains("RadarScrollMap") &&
                  !input.Contains("void Update(") &&
                  !input.Contains("Application.onBeforeRender") &&
                  !input.Contains("Input.GetMouseButton") &&
                  !input.Contains("InputR3EventHooks"),
                "minimap improvements install no frame, render, or held-input polling callback");
            Check(!input.Contains("DebugLogHelper.LogDebug") &&
                  !input.Contains("DebugLogHelper.LogInfo") &&
                  !input.Contains("DebugLogHelper.LogWarning") &&
                  input.Contains("DebugLogHelper.LogError") &&
                  input.Contains("if (failureLogged)"),
                "minimap input logs only its one-shot unexpected failure diagnostic");
            Check(input.Contains("args.GetPosition(currentRadarImage)") &&
                  input.Contains("args.GetPosition(radarImage)") &&
                  input.Contains("Enums.editorActions.placingBuilding") &&
                  input.Contains("Enums.editorActions.troopSelection") &&
                  input.Contains("Enums.editorActions.troopSelectionEnding") &&
                  input.Contains("MoveCameraToRadarPoint(controller, point)") &&
                  input.Contains("ApplyVanillaStyleDrag(point)"),
                "minimap events preserve placement clicks, cursor following, and Vanilla action exclusions");
            Check(input.Contains("requestBinkPlayState != 0") &&
                  input.Contains("sfxManager.binkIsPlaying") &&
                  input.Contains("radarMedia.Opacity = 0f") &&
                  input.Contains("RadarHeldX = 0f") &&
                  input.Contains("RadarHeldY = 0f"),
                "minimap gesture normalization preserves active video and clears held movement");
            Check(input.Contains("RadarOverlayState overlayState = InspectAndNormalizeRadarOverlay(main);") &&
                  input.Contains("overlayState == RadarOverlayState.ActiveVideoOrUnavailable") &&
                  input.Contains("bool replacedStaleClick = overlayState == RadarOverlayState.NormalizedStale;") &&
                  input.Contains("if (replacedStaleClick)") &&
                  input.Contains("FatControler.MouseIsDownStroke = false;") &&
                  input.Contains("if (placementGesture || replacedStaleClick)") &&
                  !input.Contains("mediaSurface && !TryNormalizeIdleRadarOverlay"),
                "stale radar overlay replacement is sender-independent and forwards exactly the swallowed click");
            Check(input.IndexOf("overlayState == RadarOverlayState.ActiveVideoOrUnavailable", StringComparison.Ordinal) <
                      input.IndexOf("bool replacedStaleClick", StringComparison.Ordinal) &&
                  input.IndexOf("FatControler.MouseIsDownStroke = false;", StringComparison.Ordinal) <
                      input.IndexOf("if (placementGesture || replacedStaleClick)", StringComparison.Ordinal),
                "active videos exit before replacement and the Vanilla stroke is cleared before forwarding");
            Check(runtime.Contains("private MinimapInputFeature minimapInputFeature;") &&
                  runtime.Contains("new MinimapInputFeature(log, settings)") &&
                  runtime.Contains("DeactivateMinimapInputFeature") &&
                  project.Contains("src\\MinimapInputFeature.cs") &&
                  !project.Contains("MinimapPlacementClickHook.cs") &&
                  !project.Contains("MinimapBuildingPlacementFeature.cs") &&
                  !project.Contains("MinimapCursorFollowFeature.cs"),
                "runtime and project use only the event-driven minimap implementation");

            BindingFlags members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type mainHudType = typeof(MyAudioManager).Assembly.GetType("CrusaderDE.MainHUD");
            Type radarImageType = mainHudType?.GetField("RefRadarMapImage", members)?.FieldType;
            Check(radarImageType?.GetEvent("PreviewMouseDown")?.EventHandlerType?.FullName ==
                      "Noesis.MouseButtonEventHandler" &&
                  radarImageType.GetEvent("PreviewMouseMove")?.EventHandlerType?.FullName ==
                      "Noesis.MouseEventHandler" &&
                  radarImageType.GetEvent("PreviewMouseUp")?.EventHandlerType?.FullName ==
                      "Noesis.MouseButtonEventHandler" &&
                  radarImageType.GetEvent("LostMouseCapture")?.EventHandlerType?.FullName ==
                      "Noesis.MouseEventHandler" &&
                  radarImageType.GetMethod("CaptureMouse", Type.EmptyTypes)?.ReturnType == typeof(bool),
                "installed Vanilla radar image exposes the required Noesis gesture contract");
            Check(typeof(FatControler).GetField("radarClickDelay", members)?.FieldType == typeof(bool) &&
                  typeof(FatControler).GetField("radarClickDelayTime", members)?.FieldType == typeof(DateTime) &&
                  typeof(FatControler).GetField("radarScrollTrigged", members)?.FieldType == typeof(bool),
                "installed Vanilla minimap delay and drag fields retain their managed contracts");
        }

        private static void TestResolutionAwareZoomIntegration()
        {
            string hook = File.ReadAllText(Path.Combine("src", "ResolutionAwareZoomHook.cs"));
            string plugin = File.ReadAllText(Path.Combine("src", "BugfixesAndQoLPlugin.cs"));
            string viewModel = File.ReadAllText(Path.Combine("src", "BugfixesAndQoLViewModel.cs"));
            string xaml = File.ReadAllText(Path.Combine(
                "Override", "ScriptExtenderUI", "BugfixesAndQoLSettings.xaml"));

            Check(plugin.Contains("private static ResolutionAwareZoomHook resolutionAwareZoomHook;") &&
                    plugin.Contains("new ResolutionAwareZoomHook(Logger, Settings)"),
                "resolution-aware zoom hook is rooted for the process lifetime");
            Check(hook.Contains("RollbackFailedInitialization()") &&
                    hook.Contains("settings.EnableClientFeatures") &&
                    hook.Contains("settings.EnableResolutionAwareExtendedZoom") &&
                    hook.Contains("CanUserExtraZoomDelegate") &&
                    hook.Contains("ResolutionAwareZoomPolicy.CanUserExtraZoom") &&
                    hook.Contains("canUserExtraZoomOriginal(self)") &&
                    hook.Contains("ConfigSettings.Settings_ExtraZoom") &&
                    hook.Contains("ResolutionAwareZoomPolicy.GetLockedMinimumPosition") &&
                    hook.Contains("ResolutionAwareZoomPolicy.ResolvePosition"),
                "zoom hook is fail-closed and dynamically setting-gated");
            Type zoomType = typeof(PerfectPixelWithZoom);
            string[] floatFields =
                { "zoomPos", "pixelsPerUnitScale", "zoomCurrentValue", "zoomNextValue" };
            bool managedContractMatches = true;
            foreach (string fieldName in floatFields)
            {
                FieldInfo field = zoomType.GetField(
                    fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                managedContractMatches &= field != null && field.FieldType == typeof(float);
            }
            managedContractMatches &=
                zoomType.GetMethod("adjustZoom", new[] { typeof(float), typeof(bool) })?.ReturnType == typeof(void) &&
                zoomType.GetMethod(nameof(PerfectPixelWithZoom.CanUserExtraZoom), Type.EmptyTypes)?.ReturnType == typeof(bool) &&
                zoomType.GetMethod("SetZoomImmediate", new[] { typeof(float) })?.ReturnType == typeof(void) &&
                zoomType.GetMethod("Zoom", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(float) }, null)?.ReturnType == typeof(void) &&
                zoomType.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null)?.ReturnType == typeof(void);
            Check(managedContractMatches,
                "resolution-aware zoom reflection targets match the installed Vanilla managed contract");
            Check(typeof(SHCDESE.API.GamePlayerManagerAPI).Assembly.GetName().Version ==
                    new Version(2, 6, 0, 0),
                "resolution-aware zoom is tested against installed Script Extender 2.6.0");
            Check(viewModel.Contains("public bool EnableResolutionAwareExtendedZoom") &&
                    viewModel.Contains("new LocalPerPlayerSetting<bool>(true)"),
                "resolution-aware zoom is a default-on per-player setting");
            Check(xaml.Contains("bugfixes.resolution-aware-extended-zoom") &&
                    xaml.Contains("IsChecked=\"{Binding EnableResolutionAwareExtendedZoom, Mode=TwoWay}\""),
                "resolution-aware zoom setting is exposed in searchable client UI");

            foreach (string locale in Directory.GetFiles("Locales", "*.txt"))
            {
                string text = File.ReadAllText(locale);
                Check(text.Contains("BugfixesAndQoL.EnableResolutionAwareExtendedZoom=") &&
                        text.Contains("BugfixesAndQoL.EnableResolutionAwareExtendedZoomHelp="),
                    "resolution-aware zoom localization exists in " + Path.GetFileName(locale));
            }
        }

        private static unsafe void TestSpriteAnimationGroup26Contract()
        {
            Check(Marshal.OffsetOf(
                    typeof(GameUnit),
                    nameof(GameUnit.r_SpriteAnimationGroup)).ToInt32() == 0x04,
                "SE 2.6 sprite-animation group keeps the audited GameUnit offset");

            GameUnit unit = default;
            unit.r_SpriteAnimationGroup = 0x12345678u;
            Check(unit.r_SpriteAnimationGroup == 0x12345678u,
                "SE 2.6 sprite-animation group reads and writes the installed layout");

            string projectDirectory = FindProjectDirectory();
            string cadence = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "TroopMovementFix3SynchronizedMovementCadencePatch.cs"));
            Check(cadence.Contains("UnitAnimationStateManagerOffset = 0x660") &&
                    cadence.Contains("UnitAnimationStateManagerOffset") &&
                    !cadence.Contains("N000000F4"),
                "native movement fastpath uses the audited SE 2.6 sprite-animation group offset");
        }

        private static bool AlmostEqual(float left, float right) =>
            Math.Abs(left - right) < 0.0001f;

        private static void TestNativePatternSearch()
        {
            var random = new Random(0x5E7A);
            string[] patterns = { "AA", "AA BB CC", "? BB CC", "AA ? CC", "AA BB ?", "? ?", "10 ? ? 40" };
            foreach (string pattern in patterns)
            {
                int length = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
                for (int iteration = 0; iteration < 100; iteration++)
                {
                    var data = new byte[random.Next(0, 160)];
                    random.NextBytes(data);
                    if (iteration % 4 == 0 && data.Length >= length)
                        StampNativePattern(data, random.Next(0, data.Length - length + 1), pattern, random);
                    if (iteration % 11 == 0 && data.Length >= length * 2)
                    {
                        StampNativePattern(data, 0, pattern, random);
                        StampNativePattern(data, data.Length - length, pattern, random);
                    }

                    string expected = FindNativePatternReference(data, pattern);
                    string actual;
                    try
                    {
                        actual = Shared.NativePatternResolver.FindUniquePattern(
                            data, pattern, "randomized", Shared.NativePatternSearchScope.EntireImage).ToString();
                    }
                    catch (InvalidOperationException ex)
                    {
                        actual = ex.Message.EndsWith("matched more than once.", StringComparison.Ordinal)
                            ? "ambiguous"
                            : "missing";
                    }
                    Check(actual == expected, "anchored native-pattern search retains reference semantics for " + pattern);
                }
            }
        }

        private static string FindNativePatternReference(byte[] data, string pattern)
        {
            string[] tokens = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int found = -1;
            for (int offset = 0; offset <= data.Length - tokens.Length; offset++)
            {
                bool matches = true;
                for (int index = 0; index < tokens.Length; index++)
                {
                    if (tokens[index] != "?" && tokens[index] != "??" &&
                        data[offset + index] != Convert.ToByte(tokens[index], 16))
                    {
                        matches = false;
                        break;
                    }
                }
                if (!matches)
                    continue;
                if (found >= 0)
                    return "ambiguous";
                found = offset;
            }
            return found < 0 ? "missing" : found.ToString();
        }

        private static void StampNativePattern(byte[] data, int offset, string pattern, Random random)
        {
            string[] tokens = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < tokens.Length; index++)
                data[offset + index] = tokens[index] == "?" || tokens[index] == "??"
                    ? (byte)random.Next(0, 256)
                    : Convert.ToByte(tokens[index], 16);
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

            MethodInfo exactMultiplayerHeader = typeof(MapFileManager).GetMethod(
                nameof(MapFileManager.GetHeaderFromFileNameMP),
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(int) },
                null);
            MethodInfo populateMapList = typeof(FRONT_Multiplayer).GetMethod(
                "populateMapList",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(FileHeader), typeof(bool) },
                null);

            Check(
                exactMultiplayerHeader?.ReturnType == typeof(FileHeader) &&
                    populateMapList?.ReturnType == typeof(void),
                "post-game lobby map restoration targets retain their installed Vanilla contracts");
        }

        private static void TestMultiplayerLobbyReturnIntegration()
        {
            string source = File.ReadAllText(Path.Combine(
                "src", "MultiplayerLobbyReturnFeature.cs"));

            Check(
                source.Contains("LobbySnapshot transitionSnapshot = snapshot;") &&
                    source.Contains("bool transitionAsHost = transitionHostLobby != null && transitionHostLobby.isHost;") &&
                    source.Contains("OpenHostLobby(multiplayer, transitionHostLobby, transitionSnapshot);") &&
                    source.Contains("RestoreHostMapPresentation(front, transitionSnapshot);") &&
                    source.Contains("RestoreHostMapPresentation(\r\n            FRONT_Multiplayer front,\r\n            LobbySnapshot transitionSnapshot)") &&
                    source.Contains("if (transitionSnapshot == null)") &&
                    !source.Contains("RestoreHostMapPresentation(FRONT_Multiplayer front)"),
                "post-game host transition preserves map metadata and role across the synchronous mission-end reset");
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
                    "ExtendedData_Serp",
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
                    "ExtendedData_Serp", 2, () => true, () => true, () => true),
                "Trail customization rejects an incompatible provider API");

            bool enabled = true;
            int customCalls = 0;
            int coopCalls = 0;
            Check(TrailCustomizationProviderHostApi.RegisterProvider(
                    "ExtendedData_Serp",
                    TrailCustomizationProviderHostApi.ApiVersion,
                    () => enabled,
                    () => { customCalls++; return true; },
                    () => { coopCalls++; return true; }),
                "Trail customization accepts the compatible ExtendedData provider");
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
                    "ExtendedData_Serp",
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
                Directory.GetParent(projectDirectory).FullName, "APIShared", "src", "MissionModePolicy.cs"));
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
            Check(TunnelPlacementDistancePolicy.IsSpecialValidationMode(-2) &&
                    TunnelPlacementDistancePolicy.IsSpecialValidationMode(0) &&
                    !TunnelPlacementDistancePolicy.IsSpecialValidationMode(2) &&
                    !TunnelPlacementDistancePolicy.IsSpecialValidationMode(3),
                "placement clearance distinguishes Vanilla sentinel modes from positive footprints");

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
            int sentinelGuard = feature.IndexOf(
                "IsSpecialValidationMode(args.Unknown1)", StringComparison.Ordinal);
            int mismatchDiagnostic = feature.IndexOf(
                "if (args.Unknown1 != footprintSize)", StringComparison.Ordinal);
            Check(sentinelGuard >= 0 && mismatchDiagnostic >= 0 && sentinelGuard < mismatchDiagnostic,
                "placement clearance silently ignores non-positive Vanilla validator modes before mismatch diagnostics");
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
            Check(xaml.Contains(
                    "<Grid Style=\"{StaticResource ModSettingsSearchTargetGrid}\" shared:ModSettingsSearch.Key=\"bugfixes.category.client-qol\"") &&
                    !xaml.Contains(
                    "<TextBlock Text=\"{Binding QolTitleText}\" Style=\"{StaticResource CategoryHeader}\" Margin=\"0,8,0,4\" shared:ModSettingsSearch.Key=\"bugfixes.category.client-qol\""),
                "client QoL category uses the neutral Grid search target wrapper");
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
            string friendlyRuntime = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "FriendlyMoatMovementRuntime.cs"));
            string selectionAdapters = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "AssassinSelectionAdapters.cs"));
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
            Check(runtime.Contains("processFriendlyMoatMovementRuntime") &&
                    runtime.Contains("new FriendlyMoatMovementRuntime(") &&
                    !runtime.Contains("friendlyMoatMovementRuntime?.Dispose()"),
                "integrated native runtime remains process-rooted and is not disposed by plugin teardown");
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
            Check(friendlyRuntime.Contains("selectionCallAdapters=6") &&
                    friendlyRuntime.Contains("ValidateSelectionCallAdapters(libraryBase)") &&
                    !friendlyRuntime.Contains("CursorTilePairFallbackResultHook") &&
                    !friendlyRuntime.Contains("ObserveCursorTilePairFallbackSelectionContext"),
                "friendly moat selection uses six call-site adapters and removes the unsafe post-call hooks");
            Check(selectionAdapters.Contains("SelectionCallRvas") &&
                    selectionAdapters.Contains("0xB7321") &&
                    selectionAdapters.Contains("E84AF50D0085C0757E488BF333DB") &&
                    selectionAdapters.Contains("original[0].NearBranch64 != libraryBase + 0x196870") &&
                    selectionAdapters.Contains("handle.Require().DisplacedByteCount"),
                "selection adapters validate the SE call, exact complete spans, and installed displacement");
            Check(selectionAdapters.Contains("asm.sub(rsp, 0xE0)") &&
                    selectionAdapters.Contains("asm.movdqu(__xmmword_ptr[rsp + 0xB0], xmm5)") &&
                    selectionAdapters.Contains("asm.pushfq()") &&
                    selectionAdapters.Contains("asm.popfq()") &&
                    selectionAdapters.Contains("for (int i = 1; i < original.Length; i++)"),
                "selection adapters preserve stack, volatile SIMD state, flags, and relocated instructions");
            Check(friendlyRuntime.Contains("private long ObserveCursorTilePairFallbackSelection(") &&
                    friendlyRuntime.Contains("if (vanillaResult != 0)") &&
                    friendlyRuntime.Contains("if (vanillaResult == 0 && functionalArmed)"),
                "friendly moat selection preserves full-width nonzero SE results and only lifts rejection");
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
            string cadencePatch = File.ReadAllText(Path.Combine(
                projectDirectory,
                "src",
                "TroopMovementFix3SynchronizedMovementCadencePatch.cs"));
            string integration = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "MovementCadenceIntegration.cs"));
            string bridge = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "FastRecruitMovementBridge.cs"));
            string parityHarness = File.ReadAllText(Path.Combine(
                projectDirectory, "tests", "MovementFastPathParityHarness.cs"));
            string english = File.ReadAllText(Path.Combine(
                projectDirectory, "Locales", "en-US.txt"));
            string german = File.ReadAllText(Path.Combine(
                projectDirectory, "Locales", "de-DE.txt"));

            Check(fastRecruit.Contains("!players.IsPlayerIdValid(ownerPlayerId)") &&
                    fastRecruit.Contains("players.IsAIPlayer(ownerPlayerId)") &&
                    fastRecruit.Contains("UnitTransitionSource.MercenaryOutpost") &&
                    fastRecruit.Contains("UnitTransitionSource.EuropeanBarracks") &&
                    fastRecruit.Contains("OnUnitDelete") &&
                    fastRecruit.Contains("OnUnloadMap") &&
                    fastRecruit.Contains("movementPatch.SetRallyTracking(") &&
                    cadencePatch.Contains("UnitOwnerManagerOffset") &&
                    cadencePatch.Contains("RallyGenerationGlobalIdOffset") &&
                    cadencePatch.Contains("RallyUnitTypeOffset"),
                "fast recruit rally tracks only valid human owners and preserves owner identity");
            Check(!fastRecruit.Contains("FastRecruitRallyMovementModLog.Debug") &&
                    !fastRecruit.Contains("Fast recruit rally tracking added") &&
                    !fastRecruit.Contains("Fast recruit rally movement started"),
                "fast recruit rally omits routine per-unit lifecycle logging");
            Check(fastRecruit.Contains("if (enabled &&" +
                    Environment.NewLine +
                    "                args.Phase == EventHookPhase.Pre") &&
                    fastRecruit.Contains("if (enabled && args.Phase == EventHookPhase.Pre)") &&
                    fastRecruit.Contains("args.AICommand == TribeAICommand.UnitStop") &&
                    fastRecruit.IndexOf("if (enabled &&", StringComparison.Ordinal) <
                        fastRecruit.IndexOf("RemoveTrackingForTribe(args.TribeId)", StringComparison.Ordinal),
                "disabled fast recruit handlers exit before production tribe queries; only the explicitly marked UnitStop diagnostic samples the disabled control run");

            int tribeLoop = troopMovement.IndexOf("foreach (int unitId in unitIds)",
                StringComparison.Ordinal);
            int idValidation = troopMovement.IndexOf(
                "unitId <= 0",
                tribeLoop,
                StringComparison.Ordinal);
            int unitLookup = troopMovement.IndexOf(
                "GameUnit* unit = unitArray + unitId - 1;",
                tribeLoop,
                StringComparison.Ordinal);
            Check(tribeLoop >= 0 && idValidation > tribeLoop &&
                    unitLookup > idValidation,
                "tribe synchronization validates one-based IDs and performs one direct array conversion");
            Check(!cadencePatch.Contains("AddContextHook") &&
                    !cadencePatch.Contains("NativePointer<X64SmartCPUContext>") &&
                    !cadencePatch.Contains("TryGetCadenceDelegate") &&
                    !integration.Contains("Func<IntPtr, bool>") &&
                    !integration.Contains("Action<IntPtr>") &&
                    cadencePatch.Contains("transaction.AddInline(") &&
                    cadencePatch.Contains("GeneratePreTerrainSpeedFastPath") &&
                    cadencePatch.Contains("GenerateCadenceFastPath"),
                "movement hotpaths use native inline tables without managed callbacks");
            Check(cadencePatch.Contains("rallyEnabledFlag") &&
                    cadencePatch.Contains("synchronizationEnabledFlag") &&
                    cadencePatch.Contains("assembler.je(trySynchronization);") &&
                    cadencePatch.Contains("assembler.je(replayVanilla);") &&
                    integration.Contains("SetRallyEnabled(bool enabled)") &&
                    !integration.Contains("TryGetNativeRunningState") &&
                    !integration.Contains("TryGetNativeRunningSpeedBonus") &&
                    !bridge.Contains("TryGetNativeRunningState") &&
                    !bridge.Contains("TryGetNativeRunningSpeedBonus"),
                "independent native flags bypass disabled kernels and obsolete managed animation queries are removed");
            Check(cadencePatch.Contains("MaximumTrackedUnitId = 10000") &&
                    cadencePatch.Contains("MaximumTrackedTribeId = 4500") &&
                    cadencePatch.Contains("RallyObservedOffset") &&
                    cadencePatch.Contains("RallyMovingOffset") &&
                    cadencePatch.Contains("UnitInitializationAiState = 109") &&
                    cadencePatch.Contains("clearRallyAndTrySynchronization") &&
                    cadencePatch.Contains("applyRallyProfile") &&
                    cadencePatch.IndexOf("applyRallyProfile", StringComparison.Ordinal) <
                        cadencePatch.IndexOf("applyRunningProfile", StringComparison.Ordinal),
                "native tables preserve rally identity, interrupted-path state and rally precedence");
            const string rallyDiagnosticsTag =
                "RALLY_ANIMATION_DIAGNOSTICS";
            Check(cadencePatch.Contains(
                      rallyDiagnosticsTag + "_BEGIN") &&
                    cadencePatch.Contains(
                      rallyDiagnosticsTag + "_END") &&
                    cadencePatch.Contains(
                      "RallyDiagnosticsRegistered") &&
                    cadencePatch.Contains(
                      "RallyDiagnosticsIdentityConfirmed") &&
                    cadencePatch.Contains(
                      "RallyDiagnosticsPathObserved") &&
                    cadencePatch.Contains(
                      "RallyDiagnosticsProfileResolved") &&
                    cadencePatch.Contains(
                      "RallyDiagnosticsCadenceWritten") &&
                    cadencePatch.Contains(
                      "RallyDiagnosticsSnapshotCaptured") &&
                    cadencePatch.Contains(
                      "EmitRallyDiagnosticsSnapshot") &&
                    cadencePatch.Contains(
                      "LogRallyAnimationDiagnostics") &&
                    fastRecruit.Contains(
                      rallyDiagnosticsTag + "_BEGIN") &&
                    fastRecruit.Contains(
                      rallyDiagnosticsTag + "_END") &&
                    fastRecruit.Contains(
                      "LogRallyAnimationDiagnosticsUnitStop") &&
                    fastRecruit.Contains(
                      "args.AICommand == TribeAICommand.UnitStop") &&
                    !integration.Contains(rallyDiagnosticsTag) &&
                    !bridge.Contains(rallyDiagnosticsTag),
                "temporary rally animation diagnostics are explicitly marked and confined to the cadence and UnitStop owners");
            Check(cadencePatch.Contains(
                      "SetAuditedProfile(eChimps.CHIMP_TYPE_ARAB_BOW, 1, 0x81);") &&
                    parityHarness.Contains(
                      "arab-bow-vanilla-running-pair") &&
                    parityHarness.Contains(
                      "tableUnit.Animation != 0x81 || tableUnit.SpeedBonus != 1"),
                "Arab bow rally cadence preserves Vanilla Animation 0x81 and SpeedBonus 1");
            Check(cadencePatch.Contains("UnitCurrentSpeed2ManagerOffset = 0x9A2") &&
                    cadencePatch.Contains("UnitCurrentSpeedManagerOffset = 0x9A4") &&
                    cadencePatch.Contains("assembler.Label(ref trySynchronization);") &&
                    cadencePatch.Contains("__word_ptr[r8 + UnitAliveStateManagerOffset]") &&
                    cadencePatch.Contains("ValidateMovementCadenceHook") &&
                    cadencePatch.Contains("assembler.pushfq()") &&
                    cadencePatch.Contains("assembler.popfq()") &&
                    cadencePatch.Contains("foreach (Instruction instruction in overwrittenInstructions)"),
                "native movement hooks validate exact fields and replay Vanilla with preserved flags");
            Check(Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_SpriteAnimationGroup)).ToInt32() == 0x004 &&
                    Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_AliveState)).ToInt32() == 0x088 &&
                    Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_ControllableForPlayerId)).ToInt32() == 0x092 &&
                    Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_GlobalId)).ToInt32() == 0x094 &&
                    Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_PathPlanStateBitFlags)).ToInt32() == 0x0F2 &&
                    Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_SpeedBonus)).ToInt32() == 0x2BA &&
                    Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_AIState)).ToInt32() == 0x2BC &&
                    Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_TransformIntoUnitOfType)).ToInt32() == 0x2C6 &&
                    Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_TribeId)).ToInt32() == 0x2D4 &&
                    Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_CurrentSpeed2)).ToInt32() == 0x346 &&
                    Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_CurrentSpeed)).ToInt32() == 0x348,
                "native fastpath offsets match the installed Script Extender GameUnit layout");
            Check(english.Contains("human-player units") &&
                    english.Contains("AI units remain unchanged") &&
                    german.Contains("Einheiten menschlicher Spieler") &&
                    german.Contains("KI-Einheiten bleiben unverändert"),
                "fast recruit rally help text documents the human-only behavior");
        }

        private static void TestMovementFastPathParity()
        {
            List<string> parityFailures = MovementFastPathParityHarness.Run();
            foreach (string failure in parityFailures)
            {
                Check(false, failure);
            }

            Check(parityFailures.Count == 0,
                "managed reference and fixed-table movement kernels have identical golden-matrix results");
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

        private static void TestReachableEnemyGatehouseUnitIdContract()
        {
            const int unitSpanLength = 10000;
            Check(Shared.GatehouseQueryUnitIdPolicy.TryValidateGameId(
                    1, unitSpanLength, out int firstUnitId) &&
                firstUnitId == 1 && firstUnitId - 1 == 0,
                "gatehouse event ID 1 remains game ID 1 and converts to span index 0 only at a direct-span boundary");
            Check(Shared.GatehouseQueryUnitIdPolicy.TryValidateGameId(
                    unitSpanLength, unitSpanLength, out int lastUnitId) &&
                lastUnitId == unitSpanLength,
                "gatehouse event accepts the highest valid one-based Unit game ID unchanged");
            Check(!Shared.GatehouseQueryUnitIdPolicy.TryValidateGameId(
                    0, unitSpanLength, out _) &&
                !Shared.GatehouseQueryUnitIdPolicy.TryValidateGameId(
                    -1, unitSpanLength, out _) &&
                !Shared.GatehouseQueryUnitIdPolicy.TryValidateGameId(
                    unitSpanLength + 1, unitSpanLength, out _),
                "gatehouse event rejects zero, negative and above-span Unit IDs");

            string projectDirectory = FindProjectDirectory();
            string source = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "ReachableEnemyGatehouseRuntime.cs"));
            Check(!source.Contains("zero-based span index") &&
                    !source.Contains("rawUnitSpanIndex") &&
                    source.Contains("one-based Unit game ID") &&
                    source.Contains("validates it without conversion") &&
                    source.Contains("eventUnitId={args.UnitId}"),
                "gatehouse source documents and diagnoses the current one-based event contract");

            int handlerStart = source.IndexOf(
                "private void OnGatehouseQuery(", StringComparison.Ordinal);
            int helperStart = handlerStart < 0
                ? -1
                : source.IndexOf(
                    "private bool TryIsUnitReachableToGate(",
                    handlerStart,
                    StringComparison.Ordinal);
            Check(handlerStart >= 0 && helperStart > handlerStart,
                "gatehouse source exposes the expected event-handler boundary");
            if (handlerStart < 0 || helperStart <= handlerStart)
                return;

            string handler = source.Substring(handlerStart, helperStart - handlerStart);
            int eventRead = handler.IndexOf(
                "int candidateUnitId = args.UnitId;", StringComparison.Ordinal);
            int validation = handler.IndexOf(
                "GatehouseQueryUnitIdPolicy.TryValidateGameId(", StringComparison.Ordinal);
            int lookup = handler.IndexOf(
                "TryGetUnitById(unitId,", StringComparison.Ordinal);
            int pathing = handler.IndexOf(
                "TryIsUnitReachableToGate(", StringComparison.Ordinal);
            Check(eventRead >= 0 && validation > eventRead && lookup > validation && pathing > lookup,
                "gatehouse handler passes the validated one-based Unit ID unchanged to lookup and pathing");
            Check(!handler.Contains("args.UnitId + 1") &&
                    !handler.Contains("args.UnitId+1") &&
                    !handler.Contains("candidateUnitId + 1") &&
                    !handler.Contains("candidateUnitId+1") &&
                    !handler.Contains("unitId + 1") &&
                    !handler.Contains("unitId+1"),
                "gatehouse handler applies no obsolete +1 conversion to the event Unit ID");

            Check(source.Contains("STRUCT_DRAWBRIDGE") &&
                    source.Contains("MaximumSynchronizedDrawbridges") &&
                    source.Contains("BuildOrderedFootprintCandidates(") &&
                    source.Contains("CollectFirstDistinctBuildingIds(") &&
                    source.Contains("CanReachAnyExteriorApproach(") &&
                    source.Contains("GetTileBuildingId(") &&
                    !source.Contains("GATE_REACH_DIAG") &&
                    !source.Contains("GatehouseReachabilityDiagnosticThrottle") &&
                    !source.Contains("missing-path-record") &&
                    !source.Contains("CanUnitTypeUsePathConnectionClass(") &&
                    !source.Contains("r_IsEnabledOrOpen =") &&
                    !source.Contains("SetPath") &&
                    !source.Contains("RecalculateTile"),
                "gatehouse runtime uses Vanilla's building grid and exterior approaches without mutating live pathing state");
        }

        private static void TestSynchronizedGatehouseReachabilityPolicy()
        {
            List<VanillaFootprintCandidate> candidates =
                SynchronizedGatehouseReachabilityPolicy.BuildOrderedFootprintCandidates(
                    0, 0, 5);
            string actualOrder = string.Join(
                ";",
                candidates.Select(candidate =>
                    $"({candidate.X},{candidate.Y},{candidate.OutwardX},{candidate.OutwardY})"));
            const string ExpectedOrder =
                "(2,-1,0,-1);(3,-1,0,-1);(4,-1,0,-1);" +
                "(5,0,1,0);(5,1,1,0);(5,2,1,0);(5,3,1,0);(5,4,1,0);" +
                "(4,5,0,1);(3,5,0,1);(2,5,0,1);(1,5,0,1);(0,5,0,1);" +
                "(-1,4,-1,0);(-1,3,-1,0);(-1,2,-1,0);(-1,1,-1,0);(-1,0,-1,0);" +
                "(0,-1,0,-1);(1,-1,0,-1)";
            Check(candidates.Count == 20 && actualOrder == ExpectedOrder,
                "gatehouse footprint size 5 yields Vanilla's exact 20 edge candidates in order");

            var candidateBuildings = new Dictionary<string, int>
            {
                ["2,-1"] = 90,
                ["3,-1"] = 10,
                ["4,-1"] = 10,
                ["5,0"] = 11,
                ["5,1"] = 12,
                ["5,2"] = 13
            };
            var eligible = new HashSet<int> { 10, 11, 12, 13 };
            List<int> selected =
                SynchronizedGatehouseReachabilityPolicy.CollectFirstDistinctBuildingIds(
                    candidates,
                    (x, y) => candidateBuildings.TryGetValue($"{x},{y}", out int id) ? id : 0,
                    id => eligible.Contains(id));
            Check(selected.SequenceEqual(new[] { 10, 11 }),
                "dead or non-drawbridge candidates are skipped and only Vanilla's first two distinct bridges are coupled");
            Check(selected.Contains(11),
                "a live foreign-owner drawbridge remains eligible because Vanilla's lookup has no owner filter");

            int HorizontalBuildingAt(int x, int y) => y == 2 && x >= 5 && x <= 9 ? 7 : 0;
            bool horizontalOpen = SynchronizedGatehouseReachabilityPolicy.TryTraceExteriorApproach(
                new VanillaFootprintCandidate(5, 2, 1, 0),
                7,
                (x, y) => x >= 0 && x < 20 && y >= 0 && y < 20,
                HorizontalBuildingAt,
                (x, y) => y * 20 + x,
                tileId => tileId == 50 ? 88 : 0,
                out DrawbridgeApproachSnapshot openApproach);
            bool horizontalRaised = SynchronizedGatehouseReachabilityPolicy.TryTraceExteriorApproach(
                new VanillaFootprintCandidate(5, 2, 1, 0),
                7,
                (x, y) => x >= 0 && x < 20 && y >= 0 && y < 20,
                HorizontalBuildingAt,
                (x, y) => y * 20 + x,
                tileId => tileId == 50 ? 88 : 0,
                out DrawbridgeApproachSnapshot raisedApproach);
            Check(horizontalOpen && horizontalRaised &&
                    openApproach.ExteriorX == 10 && openApproach.ExteriorY == 2 &&
                    openApproach.ExteriorPcl == 88 &&
                    raisedApproach.ExteriorTileId == openApproach.ExteriorTileId,
                "open and raised drawbridge states resolve to the same stable exterior approach without a path record");

            int VerticalBuildingAt(int x, int y) => x == 3 && y >= 4 && y <= 8 ? 8 : 0;
            Check(SynchronizedGatehouseReachabilityPolicy.TryTraceExteriorApproach(
                    new VanillaFootprintCandidate(3, 4, 0, -1),
                    8,
                    (x, y) => x >= 0 && x < 20 && y >= 0 && y < 20,
                    VerticalBuildingAt,
                    (x, y) => y * 20 + x,
                    tileId => tileId == 63 ? 77 : 0,
                    out DrawbridgeApproachSnapshot rotatedApproach) &&
                  rotatedApproach.ExteriorX == 3 && rotatedApproach.ExteriorY == 3 &&
                  rotatedApproach.ExteriorPcl == 77,
                "rotated drawbridges follow the candidate's outward direction");
            Check(!SynchronizedGatehouseReachabilityPolicy.TryTraceExteriorApproach(
                    new VanillaFootprintCandidate(0, 2, -1, 0),
                    9,
                    (x, y) => x >= 0 && x < 20 && y >= 0 && y < 20,
                    (x, y) => x == 0 && y == 2 ? 9 : 0,
                    (x, y) => y * 20 + x,
                    tileId => 22,
                    out _) &&
                  !SynchronizedGatehouseReachabilityPolicy.TryTraceExteriorApproach(
                    new VanillaFootprintCandidate(5, 2, 1, 0),
                    7,
                    (x, y) => x >= 0 && x < 20 && y >= 0 && y < 20,
                    HorizontalBuildingAt,
                    (x, y) => y * 20 + x,
                    tileId => 0,
                    out _),
                "out-of-map geometry and exterior PCL zero fail closed");

            var firstBridge = new SynchronizedDrawbridgeSnapshot(7, 700);
            firstBridge.Approaches.Add(openApproach);
            var secondBridge = new SynchronizedDrawbridgeSnapshot(8, 800);
            secondBridge.Approaches.Add(rotatedApproach);
            Check(SynchronizedGatehouseReachabilityPolicy.CanReachAnyExteriorApproach(
                    new[] { firstBridge, secondBridge },
                    component => component == 77) &&
                  !SynchronizedGatehouseReachabilityPolicy.CanReachAnyExteriorApproach(
                    new[] { firstBridge, secondBridge },
                    component => component == 99),
                "either of two coupled drawbridge exteriors can establish reachability without unrelated components");
            Check(!SynchronizedGatehouseReachabilityPolicy.CanReachAnyExteriorApproach(
                    new[] { firstBridge, secondBridge, firstBridge },
                    component => true),
                "more than two supplied drawbridges fail closed even though discovery selects only two");

            int signature =
                SynchronizedGatehouseReachabilityPolicy.ComputeDrawbridgeSignature(
                    new[] { firstBridge });
            var replacedBridge = new SynchronizedDrawbridgeSnapshot(7, 701);
            replacedBridge.Approaches.Add(openApproach);
            var changedExterior = new SynchronizedDrawbridgeSnapshot(7, 700);
            changedExterior.Approaches.Add(new DrawbridgeApproachSnapshot(
                5, 2, 10, 2, 50, 89));
            Check(signature != SynchronizedGatehouseReachabilityPolicy.ComputeDrawbridgeSignature(
                      new[] { replacedBridge }) &&
                  signature != SynchronizedGatehouseReachabilityPolicy.ComputeDrawbridgeSignature(
                      new[] { changedExterior }),
                "drawbridge cache signature changes with Global ID and exterior PCL");

            string projectDirectory = FindProjectDirectory();
            string runtime = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "ReachableEnemyGatehouseRuntime.cs"));
            Check(!runtime.Contains("OpenState") &&
                    !runtime.Contains("previouslyOpen") &&
                    runtime.Contains("tick != lastCacheTick") &&
                    runtime.Contains("reachabilityCache.Clear()"),
                "already-closed gates need no remembered open state and cache only within one tick");
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
            try
            {
                NotificationQueueNativeResolution notification =
                    NotificationQueueNativeContract.Validate(mappedImage);
                Check(notification.RunTickRva == 0x86680 &&
                      notification.UpdateRva == 0xFE570 &&
                      notification.FinalizerRva == 0x102A70 &&
                      notification.StartRva == 0xFEE50 &&
                      notification.PendingFlagRva == 0x8EBA90 &&
                      NotificationQueueNativeContract.ImmediateCommandIdOffset == 0x04 &&
                      NotificationQueueNativeContract.ImmediatePresentationIdOffset == 0x08 &&
                      NotificationQueueNativeContract.ImmediateVideoPathOffset == 0x0C &&
                      NotificationQueueNativeContract.ImmediateAudioPathOffset == 0x70 &&
                      NotificationQueueNativeContract.ImmediateAudioPathCapacity == 100 &&
                      NotificationQueueNativeContract.QueuedCountOffset == 0x94C,
                    "notification DLL_RunTick update, single finalizer, presentation start, and manager offsets match the audited contract");
            }
            catch (Exception exception)
            {
                Check(false, "notification queue native contract: " + exception.Message);
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
            int[] fallbackCalls = { 0x8D724, 0x8E2B8, 0x8E550, 0x8F325, 0xB7161, 0xB7321 };
            int[] fallbackHooks = { 0x8D729, 0x8E2BD, 0x8E555, 0x8F32A, 0xB7166, 0xB7326 };
            byte[][] fallbackSpans =
            {
                new byte[] { 0x85, 0xC0, 0x74, 0x23, 0x46, 0x8B, 0x84, 0x26, 0x2C, 0x07, 0x00, 0x00, 0x48, 0x8D, 0x0D, 0x24, 0xFF, 0x01, 0x06 },
                new byte[] { 0x48, 0x8D, 0x15, 0x3C, 0x1D, 0xF7, 0xFF, 0x85, 0xC0, 0x74, 0x63, 0x48, 0x63, 0xC7 },
                new byte[] { 0x85, 0xC0, 0x74, 0x23, 0x45, 0x8B, 0x84, 0x2C, 0x2C, 0x07, 0x00, 0x00, 0x48, 0x8D, 0x0D, 0xF8, 0xF0, 0x01, 0x06 },
                new byte[] { 0x49, 0x8B, 0xFE, 0x85, 0xC0, 0x74, 0x34, 0x8B, 0x15, 0xF1, 0x2A, 0x98, 0x03, 0x48, 0x8D, 0x0D, 0x22, 0xE3, 0x01, 0x06 },
                new byte[] { 0x45, 0x33, 0xF6, 0xB9, 0x01, 0x00, 0x00, 0x00, 0x85, 0xC0, 0x44, 0x0F, 0x45, 0xF1 },
                new byte[] { 0x85, 0xC0, 0x75, 0x7E, 0x48, 0x8B, 0xF3, 0x33, 0xDB, 0x90, 0x42, 0x0F, 0xB6, 0x4C, 0x3D, 0x00 }
            };
            for (int index = 0; index < fallbackCalls.Length; index++)
            {
                Check(image.CountNearCalls(fallbackCalls[index], 5, 0x196870) == 1,
                    $"cursor fallback call {index + 1} still targets the SE-owned selector");
                CheckBytes(image, fallbackHooks[index], fallbackSpans[index],
                    $"cursor fallback result hook {index + 1} exact displaced bytes");
                CheckInstructionSpan(fallbackSpans[index], fallbackHooks[index],
                    $"cursor fallback result hook {index + 1} instruction boundaries");
            }
            CheckShortBranch(image, 0x8D729, 2, 0x8D750, "primary move rejection branch");
            CheckShortBranch(image, 0x8E2BD, 9, 0x8E32B, "building attack rejection branch");
            CheckShortBranch(image, 0x8E555, 2, 0x8E57C, "alternative attack rejection branch");
            CheckShortBranch(image, 0x8F32A, 5, 0x8F365, "wall attack rejection branch");
            CheckShortBranch(image, 0xB7326, 2, 0xB73A8, "approach scan acceptance branch");
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

        private static void CheckInstructionSpan(byte[] bytes, int rva, string name)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = unchecked((ulong)rva);
            ulong expectedEnd = unchecked((ulong)(rva + bytes.Length));
            bool valid = true;
            while (decoder.IP < expectedEnd)
            {
                decoder.Decode(out Instruction instruction);
                valid &= instruction.Code != Code.INVALID && decoder.IP <= expectedEnd;
            }
            Check(valid && decoder.IP == expectedEnd, name);
        }

        private static void CheckShortBranch(
            PeImage image, int blockRva, int branchOffset, int expectedTargetRva, string name)
        {
            byte[] bytes = image.ReadRva(blockRva + branchOffset, 2);
            int target = blockRva + branchOffset + 2 + unchecked((sbyte)bytes[1]);
            Check((bytes[0] == 0x74 || bytes[0] == 0x75) && target == expectedTargetRva, name);
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
