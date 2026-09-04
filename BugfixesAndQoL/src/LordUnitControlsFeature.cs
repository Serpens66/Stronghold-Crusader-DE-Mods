// Feature: Reuse Vanilla's troop commands for the selected controlled Lord in a compact HUD.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal sealed unsafe class LordUnitControlsFeature : IDisposable
    {
        private const float CompactFrameWidth = 240.0f;
        private const float MinimapFrameStart = 560.0f;
        private const float FullFrameWidth = 800.0f;
        private const float FrameHeight = 155.0f;

        private delegate void ButtonUnitDisbandDelegate(MainViewModel self, object parameter);
        private delegate void ButtonTroopPanelMouseEnterDelegate(MainViewModel self, object parameter);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly SurrenderFeature surrenderFeature;
        private readonly Dictionary<UIElement, Visibility> savedVisibility =
            new Dictionary<UIElement, Visibility>();

        private Hook disbandHook;
        private ButtonUnitDisbandDelegate disbandOriginal;
        private Hook troopPanelMouseEnterHook;
        private ButtonTroopPanelMouseEnterDelegate troopPanelMouseEnterOriginal;
        private HUD_Troops activePanel;
        private FrameworkElement frame;
        private UIElement disbandElement;
        private UIElement attackHereElement;
        private FrameworkElement controlGroupsElement;
        private UIElement lordSelectionElement;
        private Thickness savedControlGroupsMargin;
        private Geometry savedFrameClip;
        private Geometry compactFrameClip;
        private int lastFrame = -1;
        private int activeLordUnitId = -1;
        private int activeLordPlayerId = -1;
        private bool lordModeActive;
        private bool callbackErrorLogged;
        private bool disposed;

        internal LordUnitControlsFeature(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            SurrenderFeature surrenderFeature)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.surrenderFeature = surrenderFeature ?? throw new ArgumentNullException(nameof(surrenderFeature));

            MethodInfo disbandMethod = typeof(MainViewModel).GetMethod(
                "ButtonUnitDisband",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(object) },
                null);
            if (disbandMethod == null || disbandMethod.ReturnType != typeof(void))
                throw new MissingMethodException(typeof(MainViewModel).FullName, "ButtonUnitDisband");

            MethodInfo troopPanelMouseEnterMethod = typeof(MainViewModel).GetMethod(
                "ButtonTroopPanelMouseEnter",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(object) },
                null);
            if (troopPanelMouseEnterMethod == null || troopPanelMouseEnterMethod.ReturnType != typeof(void))
                throw new MissingMethodException(typeof(MainViewModel).FullName, "ButtonTroopPanelMouseEnter");

            // Resolve every target before installing either hook so construction fails cleanly.
            disbandHook = new Hook(disbandMethod, (ButtonUnitDisbandDelegate)ButtonUnitDisbandHook);
            disbandOriginal = disbandHook.GenerateTrampoline<ButtonUnitDisbandDelegate>();
            troopPanelMouseEnterHook = new Hook(
                troopPanelMouseEnterMethod,
                (ButtonTroopPanelMouseEnterDelegate)ButtonTroopPanelMouseEnterHook);
            troopPanelMouseEnterOriginal =
                troopPanelMouseEnterHook.GenerateTrampoline<ButtonTroopPanelMouseEnterDelegate>();

            // The plugin component is short-lived; the static render callback persists for the match.
            Application.onBeforeRender += OnBeforeRender;
        }

        internal bool IsLordModeActive => lordModeActive;
        internal int ActiveLordPlayerId => activeLordPlayerId;

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Application.onBeforeRender -= OnBeforeRender;
            RestoreVanillaHud(refreshPanel: true);
            disbandHook?.Undo();
            disbandHook?.Dispose();
            disbandHook = null;
            disbandOriginal = null;
            troopPanelMouseEnterHook?.Undo();
            troopPanelMouseEnterHook?.Dispose();
            troopPanelMouseEnterHook = null;
            troopPanelMouseEnterOriginal = null;
        }

        private void OnBeforeRender()
        {
            if (disposed || lastFrame == Time.frameCount)
                return;
            lastFrame = Time.frameCount;

            try
            {
                Refresh();
            }
            catch (Exception ex)
            {
                RestoreVanillaHud(refreshPanel: false);
                if (!callbackErrorLogged)
                {
                    callbackErrorLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL compact Lord HUD failed closed and restored Vanilla presentation: {ex}");
                }
            }
        }

        private void Refresh()
        {
            MainViewModel main = MainViewModel.Instance;
            EngineInterface.PlayState state = GameData.Instance?.lastGameState;
            bool mapEditor = Shared.GameModeHelper.IsMapEditor();
            int selectedCount = state?.numSelectedChimps ?? 0;
            int selectedUnitId = selectedCount == 1 &&
                state.selectedChimps != null &&
                state.selectedChimps.Length > 0
                ? state.selectedChimps[0]
                : -1;
            int controlledPlayerId = GetControlledPlayerId(mapEditor);
            SurrenderLordSnapshot lord = CaptureLord(controlledPlayerId);
            bool active = LordUnitControlsPolicy.CanActivate(
                settings.EnableMod,
                settings.EnableLordUnitControls,
                IsActiveMatch(),
                mapEditor,
                state != null && state.spectatorMode != 0,
                selectedCount,
                selectedUnitId,
                controlledPlayerId,
                lord);

            if (!active || main == null || main.HUDTroopPanel == null)
            {
                bool returnToDefaultHud = LordUnitControlsPolicy.ShouldReturnToDefaultHud(
                    lordModeActive,
                    main?.Show_HUD_Troops == true,
                    selectedCount);
                RestoreVanillaHud(refreshPanel: !returnToDefaultHud);
                if (returnToDefaultHud)
                {
                    if (mapEditor)
                        main.DefaultMapEditorUIGameAction();
                    else
                        main.DefaultGameUIGameAction();
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        $"Empty Lord selection returned to Vanilla's {(mapEditor ? "map-editor" : "game")} HUD.");
                }
                return;
            }

            HUD_Troops panel = main.HUDTroopPanel;
            if (!ReferenceEquals(activePanel, panel))
            {
                RestoreVanillaHud(refreshPanel: false);
                ResolvePanelElements(panel);
            }

            if (!main.Show_HUD_Troops)
                main.TroopsSelectedGameAction(fromInitialOpening: true);

            ApplyCompactHud(mapEditor);
            if (!lordModeActive ||
                activeLordUnitId != lord.UnitId ||
                activeLordPlayerId != lord.PlayerId)
            {
                lordModeActive = true;
                activeLordUnitId = lord.UnitId;
                activeLordPlayerId = lord.PlayerId;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Compact Lord troop HUD activated: playerId={lord.PlayerId}, unitId={lord.UnitId}, globalId={lord.GlobalId}.");
            }
        }

        private void ResolvePanelElements(HUD_Troops panel)
        {
            activePanel = panel ?? throw new ArgumentNullException(nameof(panel));
            frame = RequireElement<FrameworkElement>(panel, "MainFrameTroops");
            savedFrameClip = frame.Clip;
            compactFrameClip = CreateCompactFrameClip();
            SaveVisibility(RequireElement<UIElement>(panel, "TroopSelectionControls"));
            SaveVisibility(RequireElement<UIElement>(panel, "TroopSelectionNumbers"));
            SaveVisibility(RequireElement<UIElement>(panel, "ButtonTroopPanelPage1"));
            SaveVisibility(RequireElement<UIElement>(panel, "ButtonTroopPanelPage2"));
            controlGroupsElement = RequireElement<FrameworkElement>(panel, "ToggleControlGroups");
            savedControlGroupsMargin = controlGroupsElement.Margin;
            SaveVisibility(controlGroupsElement);
            lordSelectionElement = RequireElement<UIElement>(panel, "BugfixesAndQoLLordSelectionHost");
            SaveVisibility(lordSelectionElement);

            // These are the two lower action slots reserved for the compact Lord HP display.
            SaveVisibility(RequireElement<UIElement>(panel, "UnitBuild"));
            SaveVisibility(RequireElement<UIElement>(panel, "UnitReload"));
            SaveVisibility(RequireElement<UIElement>(panel, "UnitbuildMantlet"));
            SaveVisibility(RequireElement<UIElement>(panel, "UnitFireCow"));
            SaveVisibility(RequireElement<UIElement>(panel, "UnitbuildArabBallista"));
            attackHereElement = RequireElement<UIElement>(panel, "UnitAttackHere");
            SaveVisibility(attackHereElement);
            disbandElement = RequireElement<UIElement>(panel, "UnitDisband");
            SaveVisibility(disbandElement);
        }

        private void ApplyCompactHud(bool mapEditor)
        {
            frame.Clip = compactFrameClip;
            foreach (UIElement element in savedVisibility.Keys)
                element.Visibility = Visibility.Collapsed;

            bool surrenderEnabled = settings.EnableMod && settings.EnableSurrenderAndStatistics;
            // Reuse Vanilla's normal melee/ranged attack-here command for the Lord.
            attackHereElement.Visibility = Visibility.Visible;
            disbandElement.Visibility = LordUnitControlsPolicy.CanShowDisband(
                true,
                surrenderEnabled,
                mapEditor)
                ? Visibility.Visible
                : Visibility.Collapsed;
            // Keep Vanilla's button and command, but move them beside the compact Lord panel.
            controlGroupsElement.Margin = new Thickness(195f, 11f, 0f, 0f);
            controlGroupsElement.Visibility = Visibility.Visible;
            lordSelectionElement.Visibility = Visibility.Visible;
        }

        private void RestoreVanillaHud(bool refreshPanel)
        {
            if (activePanel == null)
            {
                lordModeActive = false;
                activeLordUnitId = -1;
                activeLordPlayerId = -1;
                return;
            }

            if (frame != null)
                frame.Clip = savedFrameClip;
            foreach (KeyValuePair<UIElement, Visibility> entry in savedVisibility)
                entry.Key.Visibility = entry.Value;
            if (controlGroupsElement != null)
                controlGroupsElement.Margin = savedControlGroupsMargin;

            HUD_Troops panel = activePanel;
            bool wasActive = lordModeActive;
            activePanel = null;
            frame = null;
            disbandElement = null;
            attackHereElement = null;
            controlGroupsElement = null;
            lordSelectionElement = null;
            savedFrameClip = null;
            compactFrameClip = null;
            savedVisibility.Clear();
            lordModeActive = false;
            activeLordUnitId = -1;
            activeLordPlayerId = -1;

            if (refreshPanel && MainViewModel.Instance?.Show_HUD_Troops == true)
                panel.SelectedTroops();
            if (wasActive)
                Shared.DebugLogHelper.LogDebug(log, "Compact Lord troop HUD deactivated and Vanilla presentation restored.");
        }

        private void ButtonUnitDisbandHook(MainViewModel self, object parameter)
        {
            if (!lordModeActive)
            {
                disbandOriginal(self, parameter);
                return;
            }

            if (!IsCurrentLordSelectionEligible())
            {
                RestoreVanillaHud(refreshPanel: true);
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Lord disband was rejected because the selected Lord or match state changed; Vanilla disband was not called.");
                return;
            }

            if (!surrenderFeature.TryRequestSurrenderFromLordHud())
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Lord disband was rejected because the shared surrender action is unavailable; Vanilla disband was not called.");
            }
        }

        private void ButtonTroopPanelMouseEnterHook(MainViewModel self, object parameter)
        {
            string buttonName = parameter as string;
            bool activeLordSelection = lordModeActive && IsCurrentLordSelectionEligible();
            LordStanceTooltipAction action = LordUnitControlsPolicy.GetStanceTooltipAction(
                activeLordSelection,
                buttonName);

            if (action == LordStanceTooltipAction.UseVanillaStandGround)
            {
                // Both non-zero stances suppress the Lord's automatic movement, so reuse
                // Vanilla's already localized stand-ground rollover for either button.
                troopPanelMouseEnterOriginal(self, "GuardStanceButton");
                return;
            }

            troopPanelMouseEnterOriginal(self, parameter);
            if (action == LordStanceTooltipAction.ShowVanillaBehavior)
            {
                self.TroopsPanelRollover = SerpLocalization.Get(
                    "BugfixesAndQoL.LordStanceVanillaBehavior");
                self.TroopsPanelRollover_AmountGot1 = string.Empty;
            }
        }

        private bool IsCurrentLordSelectionEligible()
        {
            MainViewModel main = MainViewModel.Instance;
            EngineInterface.PlayState state = GameData.Instance?.lastGameState;
            bool mapEditor = Shared.GameModeHelper.IsMapEditor();
            int selectedCount = state?.numSelectedChimps ?? 0;
            int selectedUnitId = selectedCount == 1 &&
                state.selectedChimps != null &&
                state.selectedChimps.Length > 0
                ? state.selectedChimps[0]
                : -1;
            int controlledPlayerId = GetControlledPlayerId(mapEditor);
            return LordUnitControlsPolicy.CanActivate(
                settings.EnableMod,
                settings.EnableLordUnitControls,
                IsActiveMatch(),
                mapEditor,
                state != null && state.spectatorMode != 0,
                selectedCount,
                selectedUnitId,
                controlledPlayerId,
                CaptureLord(controlledPlayerId));
        }

        private static SurrenderLordSnapshot CaptureLord(int playerId)
        {
            if (playerId < 1 ||
                playerId > 8 ||
                GamePlayerManagerAPI.Instance == null ||
                GameUnitManagerAPI.Instance == null)
                return default(SurrenderLordSnapshot);

            int unitId = GamePlayerManagerAPI.Instance.GetLordUnitId(playerId);
            if (unitId <= 0 ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                unit == null)
            {
                return new SurrenderLordSnapshot(playerId, unitId, -1, -1, false);
            }

            return new SurrenderLordSnapshot(
                playerId,
                unitId,
                (int)unit->r_GlobalId,
                unit->r_ControllableForPlayerId,
                unit->r_AliveState == AliveState.IsAlive &&
                    unit->r_UnitChimp == eChimps.CHIMP_TYPE_LORD &&
                    unit->r_CurrentHealth > 0);
        }

        private static bool IsActiveMatch() =>
            FatControler.currentScene == Enums.SceneIDS.ActualMainGame &&
            Director.instance != null &&
            Director.instance.SimRunning &&
            GameData.Instance?.lastGameState != null;

        private static int GetControlledPlayerId(bool mapEditor)
        {
            if (mapEditor)
                return EditorDirector.instance?.ActivePlayerID ?? -1;
            return GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? -1;
        }

        private static Geometry CreateCompactFrameClip()
        {
            // UI-HUD 006 also contains the Minimap surround. Preserve its right-hand segment
            // while removing only the empty troop-list frame between both HUD sections.
            var actionFrame = new RectangleGeometry(
                new Noesis.Rect(0.0f, 0.0f, CompactFrameWidth, FrameHeight));
            var minimapFrame = new RectangleGeometry(
                new Noesis.Rect(
                    MinimapFrameStart,
                    0.0f,
                    FullFrameWidth - MinimapFrameStart,
                    FrameHeight));
            return new CombinedGeometry(actionFrame, minimapFrame, GeometryCombineMode.Union);
        }

        private void SaveVisibility(UIElement element)
        {
            if (element == null)
                throw new InvalidOperationException("A required compact Lord HUD element is unavailable.");
            savedVisibility[element] = element.Visibility;
        }

        private static T RequireElement<T>(HUD_Troops panel, string name) where T : class
        {
            T element = panel.FindName(name) as T;
            if (element == null)
                throw new InvalidOperationException($"HUD_Troops element '{name}' was not found.");
            return element;
        }
    }
}
