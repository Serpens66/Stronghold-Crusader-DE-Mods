using APIShared.GameModes;
// Reversible statistics preview; no surrender or mission-end action.
using APIShared;
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Network;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Steamworks;
using HarmonyLib;

namespace Spectator
{
    internal sealed class SpectatorStatistics
    {
        private static SpectatorStatistics instance;
        private readonly ManualLogSource log;
        private readonly SpectatorViewModel settings;
        private readonly Harmony hooks = new Harmony("Spectator_Serp.Statistics");
        private MethodInfo prepMpScoresMethod, showMpScoreMethod, updateHelpTextMethod, clearWeaselTextMethod;
        private FieldInfo lastStatsField, mpSortTypeField, sortReversedField, missionOverInstance1Field, missionOverInstance2Field;
        private readonly StatisticsPreviewController preview = new StatisticsPreviewController();
        private HUD_MissionOver statisticsPreviewView => preview.View as HUD_MissionOver;
        private bool statisticsPreviewActive => preview.Active;
        private bool ready;
        private long session;
        internal bool Active => statisticsPreviewActive;
        internal bool Available => ready && !preview.VanillaEnded && session > 0 && settings.Activation.Statistics &&
            MainViewModel.viewModelLoaded && GameData.Instance?.lastGameState != null && GameData.Instance.lastGameState.spectatorMode != 0 &&
            !GameModeHelper.IsMapEditor() && (GameModeHelper.IsRealMultiplayer() || GameModeHelper.IsSingleplayerSkirmishMode());
        internal SpectatorStatistics(ManualLogSource log, SpectatorViewModel settings) { this.log = log; this.settings = settings; }
        internal void Initialize()
        {
            instance = this;
            InitializeStatisticsPreview();
            hooks.Patch(FindRequiredMethod(typeof(HUD_MissionOver), "ButtonClicked", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, typeof(string)),
                prefix: new HarmonyMethod(typeof(SpectatorStatistics), nameof(BeforeButton)));
            hooks.Patch(FindRequiredMethod(typeof(GameData.Scenarios), "setGameOverState", BindingFlags.Instance | BindingFlags.Public, typeof(int), typeof(int), typeof(int)),
                prefix: new HarmonyMethod(typeof(SpectatorStatistics), nameof(BeforeGameOver)));
            if (!ApiShared.Current.TryGetMissionLifecycle(SpectatorPlugin.PluginGuid, out var lifecycle, out var diagnostic) ||
                !lifecycle.TryRegisterObserver("statistics", n => { session = n.Context.SessionId; long expected = session; SpectatorRuntime.PostPresentation(() => { if (session == expected) { preview.Start(expected); SpectatorRuntime.InvalidateToolbar(); } }); },
                    n => { session = 0; long generation = preview.Generation; SpectatorRuntime.PostPresentation(() => { if (generation == preview.Generation) CloseStatisticsPreview("session-ended"); }); }, null, out diagnostic))
                throw new InvalidOperationException("Statistics lifecycle unavailable: " + diagnostic?.Reason);
            ready = true;
        }
        private static bool BeforeButton(HUD_MissionOver __instance, string __0)
        {
            if (instance?.statisticsPreviewActive == true && ReferenceEquals(instance.statisticsPreviewView, __instance) && __0 == "Exit")
            { instance.CloseStatisticsPreview("exit"); return false; }
            return true;
        }
        private static void BeforeGameOver(int __0)
        {
            if (__0 <= 0 || instance == null) return;
            instance.preview.EndVanilla(); // Vanilla owns all subsequent visibility changes.
            SpectatorRuntime.PostPresentation(SpectatorRuntime.InvalidateToolbar);
        }
        internal void Open() { if (Available) TryOpenStatisticsPreview(); }
        internal void Close() => CloseStatisticsPreview("toolbar");
        internal void Refresh()
        {
            if (!Active) return;
            if (!Available || !ReferenceEquals(statisticsPreviewView, ResolveVisibleMissionOverView())) { Close(); return; }
            var snapshot = EngineInterface.GetMPScoreData();
            if (ValidateStatisticsSnapshot(snapshot)) TryApplyStatisticsSnapshot(statisticsPreviewView, snapshot, false);
        }
        private bool CanUseStatisticsPreview() => Available;
        private static bool HasLength(Array array, int minimumLength) => array != null && array.Length >= minimumLength;
        private void InitializeStatisticsPreview()
        {
            prepMpScoresMethod = FindRequiredMethod(
                typeof(HUD_MissionOver),
                "prepMPScores",
                BindingFlags.Instance | BindingFlags.NonPublic,
                typeof(EngineInterface.MPScoreData));
            showMpScoreMethod = FindRequiredMethod(
                typeof(HUD_MissionOver),
                "ShowMPScore",
                BindingFlags.Instance | BindingFlags.NonPublic,
                typeof(EngineInterface.MPScoreData),
                typeof(bool),
                typeof(bool));
            updateHelpTextMethod = FindRequiredMethod(
                typeof(HUD_MissionOver),
                "UpdateHelpText",
                BindingFlags.Instance | BindingFlags.NonPublic,
                typeof(int));
            clearWeaselTextMethod = FindRequiredMethod(
                typeof(HUD_MissionOver),
                "ClearWeaselText",
                BindingFlags.Instance | BindingFlags.NonPublic);
            lastStatsField = FindRequiredField(typeof(HUD_MissionOver), "last_stats", BindingFlags.Instance | BindingFlags.NonPublic);
            mpSortTypeField = FindRequiredField(typeof(HUD_MissionOver), "mp_sortType", BindingFlags.Instance | BindingFlags.NonPublic);
            sortReversedField = FindRequiredField(typeof(HUD_MissionOver), "sortReversed", BindingFlags.Instance | BindingFlags.NonPublic);
            missionOverInstance1Field = FindRequiredField(typeof(HUD_MissionOver), "instance1", BindingFlags.Static | BindingFlags.NonPublic);
            missionOverInstance2Field = FindRequiredField(typeof(HUD_MissionOver), "instance2", BindingFlags.Static | BindingFlags.NonPublic);

        }

        private bool TryOpenStatisticsPreview()
        {
            if (!CanUseStatisticsPreview())
                return false;

            EngineInterface.MPScoreData snapshot = EngineInterface.GetMPScoreData();
            if (!ValidateStatisticsSnapshot(snapshot))
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "Spectator/src/SpectatorStatistics.cs:1078", "Spectator rejected an invalid spectator-statistics snapshot.");
                return false;
            }

            MainViewModel viewModel = MainViewModel.Instance;
            viewModel.Show_HUD_MissionOver_Video = false;
            viewModel.Show_HUD_MissionOver_SandsBackground = false;
            viewModel.Show_HUD_MissionOver = true;

            HUD_MissionOver view = ResolveVisibleMissionOverView();
            if (view == null)
            {
                viewModel.Show_HUD_MissionOver = false;
                Shared.DebugLogHelper.LogErrorLimited(log, "Spectator/src/SpectatorStatistics.cs:1091", "Spectator could not resolve exactly one visible in-game statistics view.");
                return false;
            }

            viewModel.HUDMissionOver = view;
            if (!TryApplyStatisticsSnapshot(view, snapshot, initializeView: true))
            {
                viewModel.Show_HUD_MissionOver = false;
                return false;
            }

            if (!preview.Acquire(view)) return false;
            SpectatorRuntime.InvalidateToolbar();
            Shared.DebugLogHelper.LogDebugLimited(log, "Spectator/src/SpectatorStatistics.cs:1105", "Opened spectator statistics from the current local simulation snapshot without entering Vanilla game over.");
            return true;
        }

        private bool TryApplyStatisticsSnapshot(
            HUD_MissionOver view,
            EngineInterface.MPScoreData snapshot,
            bool initializeView)
        {
            EngineInterface.MPScoreData previousSnapshot =
                lastStatsField.GetValue(view) as EngineInterface.MPScoreData;

            try
            {
                ApplyStatisticsSnapshot(view, snapshot, initializeView);
                return true;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "Spectator/src/SpectatorStatistics.cs:1124", $"Spectator could not apply the spectator-statistics snapshot: {ex}");
                if (!initializeView && ValidateStatisticsSnapshot(previousSnapshot))
                {
                    try
                    {
                        ApplyStatisticsSnapshot(view, previousSnapshot, initializeView: false);
                    }
                    catch (Exception rollbackEx)
                    {
                        Shared.DebugLogHelper.LogErrorLimited(log, "Spectator/src/SpectatorStatistics.cs:1133", $"Spectator could not restore the previous spectator-statistics snapshot: {rollbackEx}");
                    }
                }
                return false;
            }
        }

        private void ApplyStatisticsSnapshot(
            HUD_MissionOver view,
            EngineInterface.MPScoreData snapshot,
            bool initializeView)
        {
            prepMpScoresMethod.Invoke(view, new object[] { snapshot });
            lastStatsField.SetValue(view, snapshot);

            MainViewModel viewModel = MainViewModel.Instance;
            if (initializeView)
            {
                // Start spectators never pass through Vanilla's victory/defeat loaders, so
                // dismiss the default Coop "Congratulations" overlay before showing scores.
                clearWeaselTextMethod.Invoke(view, null);
                mpSortTypeField.SetValue(view, 0);
                sortReversedField.SetValue(view, false);
                viewModel.MO_ShowPage1 = Visibility.Visible;
                viewModel.MO_ShowPage2 = Visibility.Collapsed;
                if (view.refRankButton1 == null || view.refRankButton2 == null)
                    throw new InvalidOperationException("Vanilla statistics page selectors are unavailable.");
                view.refRankButton1.IsChecked = true;
                view.refRankButton2.IsChecked = false;
            }

            showMpScoreMethod.Invoke(view, new object[] { snapshot, false, false });
            updateHelpTextMethod.Invoke(view, new object[] { (int)mpSortTypeField.GetValue(view) });

            viewModel.MO_SP_Score = false;
            viewModel.MO_SandsOutro1 = Visibility.Hidden;
            viewModel.MO_MP_Score = Visibility.Visible;
            viewModel.MO_MP_Victory = Visibility.Collapsed;
            viewModel.MO_MP_Defeat = Visibility.Collapsed;
            viewModel.Show_HUD_MissionOver_Video = false;
            viewModel.Show_HUD_MissionOver_SandsBackground = false;
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

        private void CloseStatisticsPreview(string reason)
        {
            HUD_MissionOver view = statisticsPreviewView;
            if (!statisticsPreviewActive && view == null)
            {
                return;
            }

            bool ownsVisible = preview.Release(preview.Generation, ResolveVisibleMissionOverView());
            SpectatorRuntime.InvalidateToolbar();

            try
            {
                if (ownsVisible) view?.PlayBackgroundVideo(false);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "Spectator/src/SpectatorStatistics.cs:1414", $"Spectator spectator-statistics video cleanup failed: {ex}");
            }

            MainViewModel viewModel = MainViewModel.Instance;
            if (viewModel != null && ownsVisible)
            {
                viewModel.Show_HUD_MissionOver_Video = false;
                viewModel.Show_HUD_MissionOver_SandsBackground = false;
                viewModel.Show_HUD_MissionOver = false;
            }

            Shared.DebugLogHelper.LogDebugLimited(log, "Spectator/src/SpectatorStatistics.cs:1425", $"Closed spectator statistics preview: reason={reason}.");
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

        private static MethodInfo FindRequiredMethod(
            Type type,
            string methodName,
            BindingFlags bindingFlags,
            params Type[] parameterTypes)
        {
            MethodInfo method = type.GetMethod(
                methodName,
                bindingFlags,
                null,
                parameterTypes,
                null);
            if (method == null || method.ReturnType != typeof(void))
                throw new MissingMethodException(type.FullName, methodName);
            return method;
        }

        private static FieldInfo FindRequiredField(Type type, string fieldName, BindingFlags bindingFlags)
        {
            FieldInfo field = type.GetField(fieldName, bindingFlags);
            if (field == null)
                throw new MissingFieldException(type.FullName, fieldName);
            return field;
        }
    }
}
