// Optional client-only badges on both Vanilla results and spectator previews.
using BepInEx.Logging;
using CrusaderDE;
using Noesis;
using R3;
using System;
using System.Reflection;
using System.Threading;


namespace BugfixesAndQoL
{
    internal sealed class StatisticsTeamBadges : IDisposable
    {
        private int statisticsBadgeModeSnapshot;
        private int statisticsBadgeClearPending;
        private bool statisticsTeamBadgesReady;
        private bool statisticsTeamBadgeErrorLogged;
        private HUD_MissionOver statisticsTeamBadgeView;
        private EngineInterface.MPScoreData statisticsTeamBadgeSnapshot;
        private Grid[,] statisticsTeamBadgeHosts;
        private Image[,] statisticsTeamBadgeVanillaIcons;
        private Grid[,] statisticsTeamBadgeEasyReadIcons;
        private Path[,] statisticsTeamBadgeShields;
        private TextBlock[,] statisticsTeamBadgeNumbers;
        private readonly SolidColorBrush[] statisticsTeamBadgeFillBrushes = new SolidColorBrush[5];
        private SolidColorBrush statisticsTeamBadgeLightTextBrush;
        private SolidColorBrush statisticsTeamBadgeDarkTextBrush;
        private int statisticsTeamBadgeSortType = int.MinValue;
        private bool statisticsTeamBadgeSortReversed;
        private int statisticsTeamBadgeMode = int.MinValue;
        private readonly int[] statisticsTeamBadgeRowPlayerIds = new int[8];
        private FieldInfo lastStatsField;
        private FieldInfo mpSortTypeField;
        private FieldInfo sortReversedField;
        private FieldInfo rankingField;
        private FieldInfo individualRankingField;
        private FieldInfo missionOverInstance1Field;
        private FieldInfo missionOverInstance2Field;
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private IDisposable ended;
        private int lastFrame = -1;
        private bool disposed;
        internal StatisticsTeamBadges(ManualLogSource log, BugfixesAndQoLViewModel settings) { this.log = log; this.settings = settings; }
        internal void Initialize()
        {
            InitializeStatisticsTeamBadges();
            statisticsTeamBadgesReady = true;
            settings.SettingChanged += OnPresentationSettingChanged;
            PublishStatisticsBadgeMode();
            ended = Shared.MissionEvents.Ended.Subscribe(_ => Shared.UnityMainThreadDispatch.TryEnqueue(ClearStatisticsTeamBadges));
            UnityEngine.Application.onBeforeRender += Render;
        }
        private bool StatisticsTeamBadgesEnabled => settings.EnableClientFeatures;
        private void Render()
        {
            if (disposed || !MainViewModel.viewModelLoaded || lastFrame == UnityEngine.Time.frameCount) return;
            lastFrame = UnityEngine.Time.frameCount;
            try
            {
                if (Volatile.Read(ref statisticsBadgeClearPending) != 0 && Interlocked.Exchange(ref statisticsBadgeClearPending, 0) != 0) ClearStatisticsTeamBadges();
                if (statisticsTeamBadgesReady && Volatile.Read(ref statisticsBadgeModeSnapshot) != SurrenderPolicy.StatisticsTeamBadgesOff) UpdateStatisticsTeamBadges();
            }
            catch (Exception error)
            {
                ClearStatisticsTeamBadges(); statisticsTeamBadgesReady = false;
                if (!statisticsTeamBadgeErrorLogged) { statisticsTeamBadgeErrorLogged = true;
                    Shared.DebugLogHelper.LogErrorLimited(log, "statistics-badges", "Statistics team badges unavailable: " + error); }
            }
        }
        public void Dispose()
        {
            disposed = true; ended?.Dispose(); settings.SettingChanged -= OnPresentationSettingChanged;
            UnityEngine.Application.onBeforeRender -= Render; ClearStatisticsTeamBadges();
        }
        private void OnPresentationSettingChanged(string propertyName)
        {
            if (propertyName == nameof(BugfixesAndQoLViewModel.EnableClientFeatures) ||
                propertyName == nameof(BugfixesAndQoLViewModel.StatisticsTeamBadgeMode)) PublishStatisticsBadgeMode();
        }

        private void PublishStatisticsBadgeMode()
        {
            int next = StatisticsTeamBadgesEnabled
                ? SurrenderPolicy.NormalizeStatisticsTeamBadgeMode(settings.StatisticsTeamBadgeMode)
                : SurrenderPolicy.StatisticsTeamBadgesOff;
            int previous = Interlocked.Exchange(ref statisticsBadgeModeSnapshot, next);
            if (previous != next && next == SurrenderPolicy.StatisticsTeamBadgesOff)
                Interlocked.Exchange(ref statisticsBadgeClearPending, 1);
        }

        private void InitializeStatisticsTeamBadges()
        {
            lastStatsField = FindRequiredField(typeof(HUD_MissionOver), "last_stats", BindingFlags.Instance | BindingFlags.NonPublic);
            mpSortTypeField = FindRequiredField(typeof(HUD_MissionOver), "mp_sortType", BindingFlags.Instance | BindingFlags.NonPublic);
            sortReversedField = FindRequiredField(typeof(HUD_MissionOver), "sortReversed", BindingFlags.Instance | BindingFlags.NonPublic);
            rankingField = FindRequiredField(typeof(HUD_MissionOver), "ranking", BindingFlags.Instance | BindingFlags.NonPublic);
            individualRankingField = FindRequiredField(typeof(HUD_MissionOver), "individual_ranking", BindingFlags.Instance | BindingFlags.NonPublic);
            missionOverInstance1Field = FindRequiredField(typeof(HUD_MissionOver), "instance1", BindingFlags.Static | BindingFlags.NonPublic);
            missionOverInstance2Field = FindRequiredField(typeof(HUD_MissionOver), "instance2", BindingFlags.Static | BindingFlags.NonPublic);

            for (int teamId = 1; teamId <= 4; teamId++)
            {
                if (!SurrenderPolicy.TryResolveStatisticsTeamBadgeStyle(
                        teamId,
                        out StatisticsTeamBadgeStyle style))
                {
                    throw new InvalidOperationException($"Statistics team-badge style {teamId} is unavailable.");
                }

                statisticsTeamBadgeFillBrushes[teamId] = new SolidColorBrush(
                    Noesis.Color.FromArgb(byte.MaxValue, style.Red, style.Green, style.Blue));
            }

            statisticsTeamBadgeLightTextBrush = new SolidColorBrush(
                Noesis.Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
            statisticsTeamBadgeDarkTextBrush = new SolidColorBrush(
                Noesis.Color.FromArgb(byte.MaxValue, 20, 16, 10));
        }

        private HUD_MissionOver ResolveVisibleMissionOverView()
        {
            HUD_MissionOver first = missionOverInstance1Field.GetValue(null) as HUD_MissionOver;
            HUD_MissionOver second = missionOverInstance2Field.GetValue(null) as HUD_MissionOver;
            HUD_MissionOver resolved = null;
            int visibleCount = 0;

            if (first != null && first.IsVisible)
            {
                resolved = first;
                visibleCount++;
            }
            if (second != null && !ReferenceEquals(second, first) && second.IsVisible)
            {
                resolved = second;
                visibleCount++;
            }

            return visibleCount == 1 ? resolved : null;
        }

        private void UpdateStatisticsTeamBadges()
        {
            int badgeMode = Volatile.Read(ref statisticsBadgeModeSnapshot);
            if (badgeMode == SurrenderPolicy.StatisticsTeamBadgesOff) return;
            MainViewModel viewModel = MainViewModel.Instance;
            if (!statisticsTeamBadgesReady ||
                !StatisticsTeamBadgesEnabled ||
                badgeMode == SurrenderPolicy.StatisticsTeamBadgesOff ||
                viewModel == null ||
                !viewModel.Show_HUD_MissionOver ||
                viewModel.MO_MP_Score != Visibility.Visible)
            {
                ClearStatisticsTeamBadges();
                return;
            }

            HUD_MissionOver view = ResolveVisibleMissionOverView();
            EngineInterface.MPScoreData snapshot =
                view == null ? null : lastStatsField.GetValue(view) as EngineInterface.MPScoreData;
            if (view == null || !ValidateStatisticsSnapshot(snapshot))
            {
                ClearStatisticsTeamBadges();
                return;
            }

            int sortType = (int)mpSortTypeField.GetValue(view);
            bool sortReversed = (bool)sortReversedField.GetValue(view);
            if (ReferenceEquals(view, statisticsTeamBadgeView) &&
                ReferenceEquals(snapshot, statisticsTeamBadgeSnapshot) &&
                sortType == statisticsTeamBadgeSortType &&
                sortReversed == statisticsTeamBadgeSortReversed &&
                badgeMode == statisticsTeamBadgeMode)
            {
                return;
            }

            EnsureStatisticsTeamBadgeElements(view);
            int[] ranking = rankingField.GetValue(view) as int[];
            int[][] individualRanking = individualRankingField.GetValue(view) as int[][];
            if (!SurrenderPolicy.TryBuildStatisticsRowPlayerIds(
                    snapshot.valid,
                    ranking,
                    individualRanking,
                    sortType,
                    sortReversed,
                    statisticsTeamBadgeRowPlayerIds))
            {
                ClearStatisticsTeamBadgeElements();
            }
            else
            {
                for (int row = 0; row < 8; row++)
                {
                    int teamId = SurrenderPolicy.ResolveStatisticsTeamShield(
                        statisticsTeamBadgeRowPlayerIds[row],
                        snapshot.team_shield);
                    ApplyStatisticsTeamBadge(0, row, teamId, badgeMode, viewModel);
                    ApplyStatisticsTeamBadge(1, row, teamId, badgeMode, viewModel);
                }
            }

            statisticsTeamBadgeSnapshot = snapshot;
            statisticsTeamBadgeSortType = sortType;
            statisticsTeamBadgeSortReversed = sortReversed;
            statisticsTeamBadgeMode = badgeMode;
            statisticsTeamBadgeErrorLogged = false;
        }

        private void EnsureStatisticsTeamBadgeElements(HUD_MissionOver view)
        {
            if (ReferenceEquals(view, statisticsTeamBadgeView) &&
                statisticsTeamBadgeHosts != null)
            {
                return;
            }

            ClearStatisticsTeamBadges();
            var hosts = new Grid[2, 8];
            var vanillaIcons = new Image[2, 8];
            var easyReadIcons = new Grid[2, 8];
            var shields = new Path[2, 8];
            var numbers = new TextBlock[2, 8];
            for (int page = 0; page < 2; page++)
            {
                for (int row = 0; row < 8; row++)
                {
                    string prefix = $"BugfixesAndQoLTeamBadgePage{page + 1}Row{row + 1}";
                    hosts[page, row] = view.FindName(prefix) as Grid;
                    vanillaIcons[page, row] = view.FindName(prefix + "VanillaIcon") as Image;
                    easyReadIcons[page, row] = view.FindName(prefix + "EasyReadIcon") as Grid;
                    shields[page, row] = view.FindName(prefix + "Shield") as Path;
                    numbers[page, row] = view.FindName(prefix + "Number") as TextBlock;
                    if (hosts[page, row] == null ||
                        vanillaIcons[page, row] == null ||
                        easyReadIcons[page, row] == null ||
                        shields[page, row] == null ||
                        numbers[page, row] == null)
                    {
                        throw new InvalidOperationException(
                            $"HUD_MissionOver team-badge elements for '{prefix}' were not found.");
                    }
                }
            }

            statisticsTeamBadgeView = view;
            statisticsTeamBadgeHosts = hosts;
            statisticsTeamBadgeVanillaIcons = vanillaIcons;
            statisticsTeamBadgeEasyReadIcons = easyReadIcons;
            statisticsTeamBadgeShields = shields;
            statisticsTeamBadgeNumbers = numbers;
        }

        private void ApplyStatisticsTeamBadge(
            int page,
            int row,
            int teamId,
            int badgeMode,
            MainViewModel viewModel)
        {
            Grid host = statisticsTeamBadgeHosts[page, row];
            if (!SurrenderPolicy.TryResolveStatisticsTeamBadgeStyle(
                    teamId,
                    out StatisticsTeamBadgeStyle style))
            {
                statisticsTeamBadgeVanillaIcons[page, row].Source = null;
                statisticsTeamBadgeVanillaIcons[page, row].Visibility = Visibility.Collapsed;
                statisticsTeamBadgeEasyReadIcons[page, row].Visibility = Visibility.Collapsed;
                host.Visibility = Visibility.Collapsed;
                return;
            }

            if (badgeMode == SurrenderPolicy.StatisticsTeamBadgesVanillaIcons)
            {
                Image vanillaIcon = statisticsTeamBadgeVanillaIcons[page, row];
                vanillaIcon.Source = viewModel.getTeamAlliesShield(teamId, large: false);
                vanillaIcon.Visibility = vanillaIcon.Source == null
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                statisticsTeamBadgeEasyReadIcons[page, row].Visibility = Visibility.Collapsed;
                host.Visibility = vanillaIcon.Source == null
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                return;
            }

            statisticsTeamBadgeVanillaIcons[page, row].Source = null;
            statisticsTeamBadgeVanillaIcons[page, row].Visibility = Visibility.Collapsed;
            statisticsTeamBadgeShields[page, row].Fill = statisticsTeamBadgeFillBrushes[teamId];
            TextBlock number = statisticsTeamBadgeNumbers[page, row];
            number.Text = teamId.ToString();
            number.Foreground = style.UseDarkText
                ? statisticsTeamBadgeDarkTextBrush
                : statisticsTeamBadgeLightTextBrush;
            statisticsTeamBadgeEasyReadIcons[page, row].Visibility = Visibility.Visible;
            host.Visibility = Visibility.Visible;
        }

        private void ClearStatisticsTeamBadgeElements()
        {
            if (statisticsTeamBadgeHosts == null)
                return;

            for (int page = 0; page < 2; page++)
            {
                for (int row = 0; row < 8; row++)
                {
                    try
                    {
                        statisticsTeamBadgeHosts[page, row].Visibility = Visibility.Collapsed;
                        statisticsTeamBadgeVanillaIcons[page, row].Source = null;
                        statisticsTeamBadgeVanillaIcons[page, row].Visibility = Visibility.Collapsed;
                        statisticsTeamBadgeEasyReadIcons[page, row].Visibility = Visibility.Collapsed;
                        statisticsTeamBadgeShields[page, row].Fill = null;
                        statisticsTeamBadgeNumbers[page, row].Text = string.Empty;
                    }
                    catch
                    {
                        // Badge cleanup must never interfere with the Vanilla statistics view.
                    }
                }
            }
        }

        private void ClearStatisticsTeamBadges()
        {
            ClearStatisticsTeamBadgeElements();
            statisticsTeamBadgeView = null;
            statisticsTeamBadgeHosts = null;
            statisticsTeamBadgeVanillaIcons = null;
            statisticsTeamBadgeEasyReadIcons = null;
            statisticsTeamBadgeShields = null;
            statisticsTeamBadgeNumbers = null;
            statisticsTeamBadgeSnapshot = null;
            statisticsTeamBadgeSortType = int.MinValue;
            statisticsTeamBadgeSortReversed = false;
            statisticsTeamBadgeMode = int.MinValue;
        }

        private static bool ValidateStatisticsSnapshot(EngineInterface.MPScoreData snapshot)
        {
            if (snapshot == null ||
                !HasLength(snapshot.valid, 9) ||
                !HasLength(snapshot.gold_acquired, 9) ||
                !HasLength(snapshot.max_population, 9) ||
                !HasLength(snapshot.fearfactor, 9) ||
                !HasLength(snapshot.time_deceased, 9) ||
                !HasLength(snapshot.who_killed_who, 81) ||
                !HasLength(snapshot.enemy_buildings_destroyed, 9) ||
                !HasLength(snapshot.food_produced, 9) ||
                !HasLength(snapshot.iron_produced, 9) ||
                !HasLength(snapshot.stone_produced, 9) ||
                !HasLength(snapshot.wood_produced, 9) ||
                !HasLength(snapshot.pitch_produced, 9) ||
                !HasLength(snapshot.minfearfactor, 9) ||
                !HasLength(snapshot.winners, 9) ||
                !HasLength(snapshot.troop_points_killed, 9) ||
                !HasLength(snapshot.enemy_buildings_razed_points, 9) ||
                !HasLength(snapshot.troops_produced, 9) ||
                !HasLength(snapshot.goods_received, 9) ||
                !HasLength(snapshot.goods_sent, 9) ||
                !HasLength(snapshot.notable_victories, 9) ||
                !HasLength(snapshot.notable_defeats, 9) ||
                !HasLength(snapshot.time_lord_killed, 9) ||
                !HasLength(snapshot.blank2, 9) ||
                !HasLength(snapshot.blank3, 9) ||
                !HasLength(snapshot.blank4, 9) ||
                !HasLength(snapshot.weapons_produced, 9) ||
                !HasLength(snapshot.buildings_lost, 9) ||
                !HasLength(snapshot.lords_killed, 9) ||
                !HasLength(snapshot.team_shield, 9) ||
                !HasLength(snapshot.teams, 9) ||
                !HasLength(snapshot.computer_register, 9) ||
                !HasLength(snapshot.playerName, 9) ||
                !HasLength(snapshot.colourMap1, 9) ||
                !HasLength(snapshot.colourMap2, 9))
            {
                return false;
            }

            return true;
        }

        private static bool HasLength(Array array, int minimumLength) =>
            array != null && array.Length >= minimumLength;

        private static FieldInfo FindRequiredField(Type type, string fieldName, BindingFlags bindingFlags)
        {
            FieldInfo field = type.GetField(fieldName, bindingFlags);
            if (field == null)
                throw new MissingFieldException(type.FullName, fieldName);
            return field;
        }

    }
}
