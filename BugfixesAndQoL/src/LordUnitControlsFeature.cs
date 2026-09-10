// Feature: Lord-specific action handling; shared troop presentation lives in APIShared.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed unsafe class LordUnitControlsFeature
    {
        private delegate void ButtonUnitDisbandDelegate(MainViewModel self, object parameter);
        private delegate void ButtonTroopPanelMouseEnterDelegate(MainViewModel self, object parameter);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly SurrenderFeature surrenderFeature;
        private readonly Func<bool> isMixedDisbandContractValidated;
        private Hook disbandHook;
        private ButtonUnitDisbandDelegate disbandOriginal;
        private Hook tooltipHook;
        private ButtonTroopPanelMouseEnterDelegate tooltipOriginal;
        private HUD_Troops activePanel;
        private UIElement disbandElement;
        private UIElement attackHereElement;
        private int lastFrame = -1;
        private bool lordModeActive;
        private bool callbackErrorLogged;

        internal LordUnitControlsFeature(ManualLogSource log, BugfixesAndQoLViewModel settings, SurrenderFeature surrenderFeature, Func<bool> isMixedDisbandContractValidated)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.surrenderFeature = surrenderFeature ?? throw new ArgumentNullException(nameof(surrenderFeature));
            this.isMixedDisbandContractValidated = isMixedDisbandContractValidated ?? throw new ArgumentNullException(nameof(isMixedDisbandContractValidated));
            MethodInfo disband = RequireMethod(typeof(MainViewModel), "ButtonUnitDisband", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo tooltip = RequireMethod(typeof(MainViewModel), "ButtonTroopPanelMouseEnter", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                disbandHook = new Hook(disband, (ButtonUnitDisbandDelegate)ButtonUnitDisbandHook);
                disbandOriginal = disbandHook.GenerateTrampoline<ButtonUnitDisbandDelegate>();
                tooltipHook = new Hook(tooltip, (ButtonTroopPanelMouseEnterDelegate)ButtonTroopPanelMouseEnterHook);
                tooltipOriginal = tooltipHook.GenerateTrampoline<ButtonTroopPanelMouseEnterDelegate>();
            }
            catch { DisposeHooks(); throw; }
            UnityEngine.Application.onBeforeRender += OnBeforeRender;
        }

        internal void RefreshSetting()
        {
            if (!settings.EnableMod || !settings.EnableLordUnitControls)
                DeactivateLordOnlyMode(true);
        }

        private void OnBeforeRender()
        {
            if (lastFrame == UnityEngine.Time.frameCount) return;
            lastFrame = UnityEngine.Time.frameCount;
            try { RefreshLordOnlyHud(); }
            catch (Exception ex)
            {
                DeactivateLordOnlyMode(false);
                if (callbackErrorLogged) return;
                callbackErrorLogged = true;
                Shared.DebugLogHelper.LogError(log, $"Lord action HUD failed closed; Vanilla presentation remains active: {ex}");
            }
        }

        private void RefreshLordOnlyHud()
        {
            MainViewModel main = MainViewModel.Instance;
            bool active = TryGetSoleControlledLord(out _);
            if (!active || main == null)
            {
                bool returnToDefaultHud = lordModeActive && main?.Show_HUD_Troops == true && !SelectionContainsNormalUnit();
                DeactivateLordOnlyMode(!returnToDefaultHud);
                if (returnToDefaultHud)
                {
                    if (Shared.GameModeHelper.IsMapEditor()) main.DefaultMapEditorUIGameAction();
                    else main.DefaultGameUIGameAction();
                }
                return;
            }
            if (!main.Show_HUD_Troops) main.TroopsSelectedGameAction(true);
            activePanel = main.HUDTroopPanel ?? throw new InvalidOperationException("HUD_Troops is unavailable.");
            attackHereElement = RequireElement<UIElement>(activePanel, "UnitAttackHere");
            disbandElement = RequireElement<UIElement>(activePanel, "UnitDisband");
            attackHereElement.Visibility = Visibility.Visible;
            disbandElement.Visibility = LordUnitControlsPolicy.CanShowDisband(true, settings.EnableMod && settings.EnableSurrenderAndStatistics, Shared.GameModeHelper.IsMapEditor()) ? Visibility.Visible : Visibility.Collapsed;
            lordModeActive = true;
        }

        private void ButtonUnitDisbandHook(MainViewModel self, object parameter)
        {
            bool sole = TryGetSoleControlledLord(out _);
            bool contains = ShouldIncludeControlledLord(out _);
            LordDisbandAction action = LordUnitControlsPolicy.GetDisbandAction(settings.EnableMod && settings.EnableLordUnitControls, sole, contains, contains && SelectionContainsOtherThanControlledLord(), isMixedDisbandContractValidated());
            if (action == LordDisbandAction.UseVanilla) { disbandOriginal(self, parameter); return; }
            if (action == LordDisbandAction.RejectUnsafeMixedSelection)
            {
                Shared.DebugLogHelper.LogWarning(log, "Mixed Lord disband rejected because its native ignore contract is unavailable.");
                return;
            }
            if (!surrenderFeature.TryRequestSurrenderFromLordHud())
                Shared.DebugLogHelper.LogWarning(log, "Lord surrender request was rejected; Vanilla disband was not called.");
        }

        private void ButtonTroopPanelMouseEnterHook(MainViewModel self, object parameter)
        {
            string name = parameter as string;
            LordStanceTooltipAction action = LordUnitControlsPolicy.GetStanceTooltipAction(lordModeActive && TryGetSoleControlledLord(out _), name);
            if (action == LordStanceTooltipAction.UseVanillaStandGround) { tooltipOriginal(self, "GuardStanceButton"); return; }
            tooltipOriginal(self, parameter);
            if (action == LordStanceTooltipAction.ShowVanillaBehavior)
            {
                self.TroopsPanelRollover = SerpLocalization.Get("BugfixesAndQoL.LordStanceVanillaBehavior");
                self.TroopsPanelRollover_AmountGot1 = string.Empty;
            }
        }

        private bool TryGetSoleControlledLord(out SurrenderLordSnapshot lord)
        {
            EngineInterface.PlayState state = GameData.Instance?.lastGameState;
            bool editor = Shared.GameModeHelper.IsMapEditor();
            int count = state?.numSelectedChimps ?? 0;
            int selectedId = count == 1 && state.selectedChimps != null && state.selectedChimps.Length > 0 ? state.selectedChimps[0] : -1;
            int player = GetControlledPlayerId(editor);
            lord = CaptureLord(player);
            return LordUnitControlsPolicy.CanActivate(settings.EnableMod, settings.EnableLordUnitControls, IsActiveMatch(), editor, state != null && state.spectatorMode != 0, count, selectedId, player, lord);
        }

        private bool ShouldIncludeControlledLord(out SurrenderLordSnapshot lord)
        {
            lord = CaptureLord(GetControlledPlayerId(Shared.GameModeHelper.IsMapEditor()));
            if (!settings.EnableMod || !settings.EnableLordUnitControls || !SurrenderPolicy.IsValidLord(lord)) return false;
            EngineInterface.PlayState state = GameData.Instance?.lastGameState;
            if (state?.selectedChimps == null) return false;
            int count = Math.Min(state.numSelectedChimps, state.selectedChimps.Length);
            for (int i = 0; i < count; i++) if (state.selectedChimps[i] == lord.UnitId) return true;
            return false;
        }

        private bool SelectionContainsOtherThanControlledLord()
        {
            if (!ShouldIncludeControlledLord(out SurrenderLordSnapshot lord)) return false;
            EngineInterface.PlayState state = GameData.Instance.lastGameState;
            int count = Math.Min(state.numSelectedChimps, state.selectedChimps.Length);
            for (int i = 0; i < count; i++) if (state.selectedChimps[i] > 0 && state.selectedChimps[i] != lord.UnitId) return true;
            return false;
        }

        private static bool SelectionContainsNormalUnit()
        {
            EngineInterface.PlayState state = GameData.Instance?.lastGameState;
            if (state?.selectedChimps == null || GameUnitManagerAPI.Instance == null) return false;
            int count = Math.Min(state.numSelectedChimps, state.selectedChimps.Length);
            for (int i = 0; i < count; i++)
                if (state.selectedChimps[i] > 0 && GameUnitManagerAPI.Instance.TryGetUnitById(state.selectedChimps[i], out GameUnit* unit) && unit != null && unit->r_AliveState == AliveState.IsAlive && unit->r_UnitChimp != eChimps.CHIMP_TYPE_LORD) return true;
            return false;
        }

        private void DeactivateLordOnlyMode(bool refresh)
        {
            if (!lordModeActive) return;
            lordModeActive = false;
            if (refresh && MainViewModel.Instance?.Show_HUD_Troops == true) activePanel?.SelectedTroops();
        }

        private static SurrenderLordSnapshot CaptureLord(int playerId)
        {
            if (playerId < 1 || playerId > 8 || GamePlayerManagerAPI.Instance == null || GameUnitManagerAPI.Instance == null) return default(SurrenderLordSnapshot);
            int unitId = GamePlayerManagerAPI.Instance.GetLordUnitId(playerId);
            if (unitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) || unit == null) return new SurrenderLordSnapshot(playerId, unitId, -1, -1, false);
            return new SurrenderLordSnapshot(playerId, unitId, (int)unit->r_GlobalId, unit->r_ControllableForPlayerId, unit->r_AliveState == AliveState.IsAlive && unit->r_UnitChimp == eChimps.CHIMP_TYPE_LORD && unit->r_CurrentHealth > 0);
        }

        private static bool IsActiveMatch() => FatControler.currentScene == Enums.SceneIDS.ActualMainGame && Director.instance != null && Director.instance.SimRunning && GameData.Instance?.lastGameState != null;
        private static int GetControlledPlayerId(bool editor) => editor ? (EditorDirector.instance?.ActivePlayerID ?? -1) : (GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? -1);
        private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags) => type.GetMethod(name, flags, null, new[] { typeof(object) }, null) ?? throw new MissingMethodException(type.FullName, name);
        private static T RequireElement<T>(HUD_Troops panel, string name) where T : class => panel.FindName(name) as T ?? throw new InvalidOperationException($"HUD_Troops element '{name}' was not found.");
        private void DisposeHooks() { Undo(ref tooltipHook); tooltipOriginal = null; Undo(ref disbandHook); disbandOriginal = null; }
        private static void Undo(ref Hook hook) { if (hook == null) return; try { hook.Undo(); } finally { hook.Dispose(); hook = null; } }
    }
}
