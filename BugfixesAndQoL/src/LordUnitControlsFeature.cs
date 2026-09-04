// Feature: Integrate the selected controlled Lord into Vanilla's complete troop HUD.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Reflection;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal sealed unsafe class LordUnitControlsFeature : IDisposable
    {
        private delegate void SetupSelectedTroopsDelegate(HUD_Troops self);
        private delegate void ButtonUnitDisbandDelegate(MainViewModel self, object parameter);
        private delegate void ButtonTroopPanelMouseEnterDelegate(MainViewModel self, object parameter);

        private static readonly FieldInfo SelectedChimpArrayField = RequireField("SelectedChimpArray");
        private static readonly FieldInfo NoSelectedChimpTypesField = RequireField("NoSelectedChimpTypes");
        private static readonly FieldInfo CurrentPageField = RequireField("currentPage");
        private static readonly FieldInfo PagesField = RequireField("pages");
        private static readonly FieldInfo SelTroopPositionsField = RequireField("SelTroopPositions");

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly SurrenderFeature surrenderFeature;
        private readonly Func<bool> isMixedDisbandContractValidated;
        private Hook setupSelectedTroopsHook;
        private SetupSelectedTroopsDelegate setupSelectedTroopsOriginal;
        private Hook disbandHook;
        private ButtonUnitDisbandDelegate disbandOriginal;
        private Hook troopPanelMouseEnterHook;
        private ButtonTroopPanelMouseEnterDelegate troopPanelMouseEnterOriginal;
        private HUD_Troops activePanel;
        private Button lordSelectionButton;
        private UIElement disbandElement;
        private UIElement attackHereElement;
        private int lastFrame = -1;
        private int activeLordUnitId = -1;
        private int activeLordPlayerId = -1;
        private bool lordModeActive;
        private bool callbackErrorLogged;
        private bool layoutErrorLogged;
        private bool disposed;

        internal LordUnitControlsFeature(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            SurrenderFeature surrenderFeature,
            Func<bool> isMixedDisbandContractValidated)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.surrenderFeature = surrenderFeature ?? throw new ArgumentNullException(nameof(surrenderFeature));
            this.isMixedDisbandContractValidated = isMixedDisbandContractValidated ??
                throw new ArgumentNullException(nameof(isMixedDisbandContractValidated));

            MethodInfo setupMethod = typeof(HUD_Troops).GetMethod(
                "SetupSelectedTroops",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            MethodInfo disbandMethod = typeof(MainViewModel).GetMethod(
                "ButtonUnitDisband",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(object) },
                null);
            MethodInfo tooltipMethod = typeof(MainViewModel).GetMethod(
                "ButtonTroopPanelMouseEnter",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(object) },
                null);
            ValidateHookTarget(setupMethod, typeof(HUD_Troops), "SetupSelectedTroops");
            ValidateHookTarget(disbandMethod, typeof(MainViewModel), "ButtonUnitDisband");
            ValidateHookTarget(tooltipMethod, typeof(MainViewModel), "ButtonTroopPanelMouseEnter");

            try
            {
                setupSelectedTroopsHook = new Hook(
                    setupMethod,
                    (SetupSelectedTroopsDelegate)SetupSelectedTroopsHook);
                setupSelectedTroopsOriginal =
                    setupSelectedTroopsHook.GenerateTrampoline<SetupSelectedTroopsDelegate>();
                disbandHook = new Hook(disbandMethod, (ButtonUnitDisbandDelegate)ButtonUnitDisbandHook);
                disbandOriginal = disbandHook.GenerateTrampoline<ButtonUnitDisbandDelegate>();
                troopPanelMouseEnterHook = new Hook(
                    tooltipMethod,
                    (ButtonTroopPanelMouseEnterDelegate)ButtonTroopPanelMouseEnterHook);
                troopPanelMouseEnterOriginal =
                    troopPanelMouseEnterHook.GenerateTrampoline<ButtonTroopPanelMouseEnterDelegate>();
            }
            catch
            {
                DisposeHooks();
                throw;
            }

            // The startup plugin component is short-lived, but this static callback persists.
            Application.onBeforeRender += OnBeforeRender;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Application.onBeforeRender -= OnBeforeRender;
            HideLordButton();
            DisposeHooks();
            lordModeActive = false;
            activeLordUnitId = -1;
            activeLordPlayerId = -1;
        }

        internal void RefreshSetting()
        {
            if (disposed)
                return;
            bool activeGameUi = Shared.GameModeHelper.IsMapEditor() || IsActiveMatch();
            if (!activeGameUi)
                return;
            MainViewModel main = MainViewModel.Instance;
            HUD_Troops panel = main?.HUDTroopPanel;
            if (main?.Show_HUD_Troops == true && panel != null)
                panel.SetupSelectedTroops();
            else if (!settings.EnableMod || !settings.EnableLordUnitControls)
                HideLordButton(panel);
        }

        private void SetupSelectedTroopsHook(HUD_Troops self)
        {
            setupSelectedTroopsOriginal(self);
            try
            {
                if (!ShouldIncludeControlledLord(out _))
                {
                    HideLordButton(self);
                    return;
                }

                ApplyLordAwareLayout(self);
            }
            catch (Exception ex)
            {
                // A helper can fail after clearing slots. Re-run the trampoline to reconstruct
                // the complete Vanilla layout before removing our only custom element.
                setupSelectedTroopsOriginal(self);
                HideLordButton(self);
                if (!layoutErrorLogged)
                {
                    layoutErrorLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL Lord troop-list layout failed closed; Vanilla's full troop HUD remains active: {ex}");
                }
            }
        }

        private void ApplyLordAwareLayout(HUD_Troops panel)
        {
            ResolvePanelElements(panel);
            int[] selectedTypeCounts = SelectedChimpArrayField.GetValue(panel) as int[];
            if (selectedTypeCounts == null ||
                selectedTypeCounts.Length <= (int)eChimps.CHIMP_TYPE_LORD)
            {
                HideLordButton(panel);
                return;
            }

            // Direct Lord selection can leave Vanilla's type-55 aggregate at zero even
            // though selectedChimps contains the validated, unique controlled Lord.
            selectedTypeCounts[(int)eChimps.CHIMP_TYPE_LORD] = 1;

            int pages = SelectedUnitHealthPageLayout.GetPageCount(selectedTypeCounts);
            int currentPage = SelectedUnitHealthPageLayout.ClampPage(
                (int)CurrentPageField.GetValue(panel),
                selectedTypeCounts);
            NoSelectedChimpTypesField.SetValue(
                panel,
                SelectedUnitHealthPageLayout.CountVisibleTypes(selectedTypeCounts));
            PagesField.SetValue(panel, pages);
            CurrentPageField.SetValue(panel, currentPage);

            panel.HideAllSelectedTroops();
            panel.HideAllSelectedTroopsNumbers();
            lordSelectionButton.Visibility = Visibility.Collapsed;
            SetPageButtonVisibility(panel, currentPage, pages);

            int[] visibleTypes = SelectedUnitHealthPageLayout.GetVisibleTypes(
                selectedTypeCounts,
                currentPage);
            var positions = SelTroopPositionsField.GetValue(panel) as TranslateTransform[];
            if (positions == null || positions.Length < SelectedUnitHealthPageLayout.SlotCount)
                throw new InvalidOperationException("HUD_Troops.SelTroopPositions has an unexpected layout.");

            for (int slot = 0; slot < visibleTypes.Length; slot++)
            {
                int type = visibleTypes[slot];
                if (type < 0)
                    continue;

                if (type == (int)eChimps.CHIMP_TYPE_LORD)
                {
                    positions[slot].Y = 0f;
                    lordSelectionButton.RenderTransform = positions[slot];
                    lordSelectionButton.Visibility = Visibility.Visible;
                }
                else
                {
                    positions[slot].Y = panel.SetSelectedTroopVisible(type);
                    panel.SetSelectedTroopPosition(type, slot);
                }
                panel.ShowSelectedTroopsNumber(slot, selectedTypeCounts[type]);
            }
        }

        private void OnBeforeRender()
        {
            if (disposed || lastFrame == Time.frameCount)
                return;
            lastFrame = Time.frameCount;

            try
            {
                RefreshLordOnlyHud();
            }
            catch (Exception ex)
            {
                DeactivateLordOnlyMode(refreshVanillaPanel: false);
                if (!callbackErrorLogged)
                {
                    callbackErrorLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL Lord troop HUD failed closed; Vanilla presentation remains active: {ex}");
                }
            }
        }

        private void RefreshLordOnlyHud()
        {
            MainViewModel main = MainViewModel.Instance;
            bool active = TryGetSoleControlledLord(out SurrenderLordSnapshot lord);
            if (!active || main == null)
            {
                bool returnToDefaultHud = lordModeActive &&
                    main?.Show_HUD_Troops == true &&
                    !SelectionContainsNormalUnit();
                DeactivateLordOnlyMode(refreshVanillaPanel: !returnToDefaultHud);
                if (returnToDefaultHud)
                {
                    if (Shared.GameModeHelper.IsMapEditor())
                        main.DefaultMapEditorUIGameAction();
                    else
                        main.DefaultGameUIGameAction();
                }
                return;
            }

            if (!main.Show_HUD_Troops)
                main.TroopsSelectedGameAction(fromInitialOpening: true);
            HUD_Troops panel = main.HUDTroopPanel;
            if (panel == null)
                throw new InvalidOperationException("HUD_Troops is unavailable after opening the troop HUD.");
            ResolvePanelElements(panel);

            // Vanilla does not normally open this panel for a sole Lord. Keep the complete
            // action area and only expose the two general commands that apply to him.
            attackHereElement.Visibility = Visibility.Visible;
            disbandElement.Visibility = LordUnitControlsPolicy.CanShowDisband(
                true,
                settings.EnableMod && settings.EnableSurrenderAndStatistics,
                Shared.GameModeHelper.IsMapEditor())
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (!lordModeActive ||
                activeLordUnitId != lord.UnitId ||
                activeLordPlayerId != lord.PlayerId)
            {
                lordModeActive = true;
                activeLordUnitId = lord.UnitId;
                activeLordPlayerId = lord.PlayerId;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Full Lord troop HUD activated: playerId={lord.PlayerId}, unitId={lord.UnitId}, globalId={lord.GlobalId}.");
            }
        }

        private void ButtonUnitDisbandHook(MainViewModel self, object parameter)
        {
            bool soleControlledLord = TryGetSoleControlledLord(out _);
            bool containsControlledLord = ShouldIncludeControlledLord(out _);
            bool containsOtherUnits = containsControlledLord && SelectionContainsOtherThanControlledLord();
            LordDisbandAction action = LordUnitControlsPolicy.GetDisbandAction(
                settings.EnableMod && settings.EnableLordUnitControls,
                soleControlledLord,
                containsControlledLord,
                containsOtherUnits,
                isMixedDisbandContractValidated());

            if (action == LordDisbandAction.UseVanilla)
            {
                disbandOriginal(self, parameter);
                return;
            }
            if (action == LordDisbandAction.RejectUnsafeMixedSelection)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Mixed Lord disband was rejected because the native Lord-ignore contract is not validated; no selected unit was disbanded.");
                return;
            }

            if (!surrenderFeature.TryRequestSurrenderFromLordHud())
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Lord surrender was rejected because the selected Lord or shared surrender action is unavailable; Vanilla disband was not called.");
            }
        }

        private void ButtonTroopPanelMouseEnterHook(MainViewModel self, object parameter)
        {
            string buttonName = parameter as string;
            if (string.Equals(
                    buttonName,
                    "BugfixesAndQoLLordSelected",
                    StringComparison.Ordinal) &&
                ShouldIncludeControlledLord(out _))
            {
                // Let Vanilla reset and expose its rollover controls, then use the existing
                // fully localized Lord selection text which Vanilla never dispatches here.
                troopPanelMouseEnterOriginal(self, "ArchersSelected");
                self.TroopsPanelRollover = Translate.Instance.lookUpText(
                    Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT,
                    Enums.eTextValues.BHELP_TEXT_SELECT_LORD);
                self.TroopsPanelRollover_AmountGot1 = string.Empty;
                return;
            }

            LordStanceTooltipAction action = LordUnitControlsPolicy.GetStanceTooltipAction(
                lordModeActive && TryGetSoleControlledLord(out _),
                buttonName);
            if (action == LordStanceTooltipAction.UseVanillaStandGround)
            {
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

        private bool TryGetSoleControlledLord(out SurrenderLordSnapshot lord)
        {
            EngineInterface.PlayState state = GameData.Instance?.lastGameState;
            bool mapEditor = Shared.GameModeHelper.IsMapEditor();
            int selectedCount = state?.numSelectedChimps ?? 0;
            int selectedUnitId = selectedCount == 1 &&
                state.selectedChimps != null &&
                state.selectedChimps.Length > 0
                ? state.selectedChimps[0]
                : -1;
            int controlledPlayerId = GetControlledPlayerId(mapEditor);
            lord = CaptureLord(controlledPlayerId);
            return LordUnitControlsPolicy.CanActivate(
                settings.EnableMod,
                settings.EnableLordUnitControls,
                IsActiveMatch(),
                mapEditor,
                state != null && state.spectatorMode != 0,
                selectedCount,
                selectedUnitId,
                controlledPlayerId,
                lord);
        }

        private bool ShouldIncludeControlledLord(out SurrenderLordSnapshot lord)
        {
            lord = default(SurrenderLordSnapshot);
            if (!settings.EnableMod || !settings.EnableLordUnitControls)
                return false;

            EngineInterface.PlayState state = GameData.Instance?.lastGameState;
            bool mapEditor = Shared.GameModeHelper.IsMapEditor();
            if (state == null ||
                state.numSelectedChimps <= 0 ||
                state.selectedChimps == null ||
                (!mapEditor && (!IsActiveMatch() || state.spectatorMode != 0)))
                return false;

            lord = CaptureLord(GetControlledPlayerId(mapEditor));
            if (!SurrenderPolicy.IsValidLord(lord))
                return false;

            int count = Math.Min(state.numSelectedChimps, state.selectedChimps.Length);
            for (int i = 0; i < count; i++)
            {
                if (state.selectedChimps[i] == lord.UnitId)
                    return true;
            }
            return false;
        }

        private bool SelectionContainsOtherThanControlledLord()
        {
            if (!ShouldIncludeControlledLord(out SurrenderLordSnapshot lord))
                return false;
            EngineInterface.PlayState state = GameData.Instance.lastGameState;
            int count = Math.Min(state.numSelectedChimps, state.selectedChimps.Length);
            for (int i = 0; i < count; i++)
            {
                if (state.selectedChimps[i] > 0 && state.selectedChimps[i] != lord.UnitId)
                    return true;
            }
            return false;
        }

        private static bool SelectionContainsNormalUnit()
        {
            EngineInterface.PlayState state = GameData.Instance?.lastGameState;
            GameUnitManagerAPI units = GameUnitManagerAPI.Instance;
            if (state == null || units == null || state.selectedChimps == null)
                return false;
            int count = Math.Min(state.numSelectedChimps, state.selectedChimps.Length);
            for (int i = 0; i < count; i++)
            {
                int unitId = state.selectedChimps[i];
                if (unitId > 0 &&
                    units.TryGetUnitById(unitId, out GameUnit* unit) &&
                    unit != null &&
                    unit->r_AliveState == AliveState.IsAlive &&
                    unit->r_UnitChimp != eChimps.CHIMP_TYPE_LORD)
                    return true;
            }
            return false;
        }

        private void ResolvePanelElements(HUD_Troops panel)
        {
            if (ReferenceEquals(activePanel, panel) && lordSelectionButton != null)
                return;
            HideLordButton();
            activePanel = panel ?? throw new ArgumentNullException(nameof(panel));
            lordSelectionButton = RequireElement<Button>(panel, "BugfixesAndQoLLordSelected");
            attackHereElement = RequireElement<UIElement>(panel, "UnitAttackHere");
            disbandElement = RequireElement<UIElement>(panel, "UnitDisband");
        }

        private void DeactivateLordOnlyMode(bool refreshVanillaPanel)
        {
            if (!lordModeActive)
                return;
            HUD_Troops panel = activePanel;
            lordModeActive = false;
            activeLordUnitId = -1;
            activeLordPlayerId = -1;
            if (refreshVanillaPanel && MainViewModel.Instance?.Show_HUD_Troops == true)
                panel?.SelectedTroops();
            Shared.DebugLogHelper.LogDebug(log, "Full Lord troop HUD deactivated.");
        }

        private void HideLordButton(HUD_Troops panel = null)
        {
            Button button = ReferenceEquals(panel, activePanel)
                ? lordSelectionButton
                : panel?.FindName("BugfixesAndQoLLordSelected") as Button;
            if (button != null)
                button.Visibility = Visibility.Collapsed;
            if (panel == null && lordSelectionButton != null)
                lordSelectionButton.Visibility = Visibility.Collapsed;
        }

        private static void SetPageButtonVisibility(HUD_Troops panel, int currentPage, int pages)
        {
            // Vanilla names are historical: Page1 carries command "1" (next), Page2 "0" (previous).
            Button next = RequireElement<Button>(panel, "ButtonTroopPanelPage1");
            Button previous = RequireElement<Button>(panel, "ButtonTroopPanelPage2");
            PropEx.SetButtonVisibility(
                previous,
                pages > 1 && currentPage > 0 ? Visibility.Visible : Visibility.Hidden);
            PropEx.SetButtonVisibility(
                next,
                pages > 1 && currentPage < pages - 1 ? Visibility.Visible : Visibility.Hidden);
        }

        private void DisposeHooks()
        {
            UndoAndDispose(ref troopPanelMouseEnterHook);
            troopPanelMouseEnterOriginal = null;
            UndoAndDispose(ref disbandHook);
            disbandOriginal = null;
            UndoAndDispose(ref setupSelectedTroopsHook);
            setupSelectedTroopsOriginal = null;
        }

        private static void UndoAndDispose(ref Hook hook)
        {
            if (hook == null)
                return;
            try
            {
                hook.Undo();
            }
            finally
            {
                hook.Dispose();
                hook = null;
            }
        }

        private static SurrenderLordSnapshot CaptureLord(int playerId)
        {
            if (playerId < 1 || playerId > 8 ||
                GamePlayerManagerAPI.Instance == null ||
                GameUnitManagerAPI.Instance == null)
                return default(SurrenderLordSnapshot);

            int unitId = GamePlayerManagerAPI.Instance.GetLordUnitId(playerId);
            if (unitId <= 0 ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                unit == null)
                return new SurrenderLordSnapshot(playerId, unitId, -1, -1, false);

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

        private static int GetControlledPlayerId(bool mapEditor) =>
            mapEditor
                ? (EditorDirector.instance?.ActivePlayerID ?? -1)
                : (GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? -1);

        private static FieldInfo RequireField(string name) =>
            typeof(HUD_Troops).GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
            throw new MissingFieldException(typeof(HUD_Troops).FullName, name);

        private static void ValidateHookTarget(MethodInfo method, Type type, string name)
        {
            if (method == null || method.ReturnType != typeof(void))
                throw new MissingMethodException(type.FullName, name);
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
