// Feature: Allow market auto-sell thresholds greater than zero without reserving one good.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using System;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed class AutoTradeSellZeroHook : IDisposable
    {
        private delegate void AutoTradeSellSliderValueChangedDelegate(HUD_Buildings self, object sender, RoutedPropertyChangedEventArgs<float> e);

        private static readonly FieldInfo SliderSetupField = FindField("sliderSetup");
        private static readonly FieldInfo CurrentAutoTradeOnField = FindField("currentAutoTradeOn");
        private static readonly FieldInfo InsideValueChangedField = FindField("insideValueChanged");
        private static readonly MethodInfo ToggleAutoTradeMethod = FindMethod("toggleAutoTrade");

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly Hook hook;
        private readonly AutoTradeSellSliderValueChangedDelegate trampoline;
        private bool disposed;

        public AutoTradeSellZeroHook(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            hook = new Hook(FindSellSliderValueChangedMethod(), (AutoTradeSellSliderValueChangedDelegate)SellSliderValueChangedHook);
            trampoline = hook.GenerateTrampoline<AutoTradeSellSliderValueChangedDelegate>();
            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL auto-trade sell zero hook installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            hook?.Undo();
            hook?.Dispose();
            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL auto-trade sell zero hook disposed.");
        }

        private static MethodInfo FindSellSliderValueChangedMethod()
        {
            MethodInfo method = typeof(HUD_Buildings).GetMethod(
                "Autotrade_Sell_Slider_ValueChanged",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (method == null)
                throw new MissingMethodException(typeof(HUD_Buildings).FullName, "Autotrade_Sell_Slider_ValueChanged");

            return method;
        }

        private static FieldInfo FindField(string fieldName)
        {
            FieldInfo field = typeof(HUD_Buildings).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field == null)
                throw new MissingFieldException(typeof(HUD_Buildings).FullName, fieldName);

            return field;
        }

        private static MethodInfo FindMethod(string methodName)
        {
            MethodInfo method = typeof(HUD_Buildings).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);

            if (method == null)
                throw new MissingMethodException(typeof(HUD_Buildings).FullName, methodName);

            return method;
        }

        private void SellSliderValueChangedHook(HUD_Buildings self, object sender, RoutedPropertyChangedEventArgs<float> e)
        {
            if (!settings.EnableClientFeatures ||
                !settings.EnableAutoTradeSellZeroFix ||
                self == null ||
                MainViewModel.Instance == null ||
                Translate.Instance == null ||
                !IsSelectedTradepostControlled())
            {
                trampoline(self, sender, e);
                return;
            }

            RangeBase slider = self?.RefAutotrade_Sell_Slider ?? sender as RangeBase;
            int sliderValue = slider == null ? 0 : (int)slider.Value;

            if (sliderValue != 0)
            {
                trampoline(self, sender, e);
                return;
            }

            try
            {
                HandleSellZero(self);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL auto-trade sell zero hook failed: {ex}");
                trampoline(self, sender, e);
            }
        }

        private static void HandleSellZero(HUD_Buildings self)
        {
            if (FatControler.currentScene == (Enums.SceneIDS)0)
                return;

            if (!GetBool(self, SliderSetupField))
                EnsureBuySliderAtMinimum(self);

            MainViewModel.Instance.TradeAutoSell =
                Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 144) + " > 0";

            EngineInterface.GameAction(Enums.GameActionCommand.Autotrade_SetSell, 0, 0);

            if (!GetBool(self, SliderSetupField) && !GetBool(self, CurrentAutoTradeOnField))
                ToggleAutoTradeMethod.Invoke(self, null);
        }

        private static bool GetBool(HUD_Buildings self, FieldInfo field)
        {
            return (bool)field.GetValue(self);
        }

        private static void SetBool(HUD_Buildings self, FieldInfo field, bool value)
        {
            field.SetValue(self, value);
        }

        private static void EnsureBuySliderAtMinimum(HUD_Buildings self)
        {
            if (self.RefAutotrade_Buy_Slider == null || self.RefAutotrade_Buy_Slider.Value <= 0f)
                return;

            bool previousInsideValueChanged = GetBool(self, InsideValueChangedField);
            SetBool(self, InsideValueChangedField, true);

            try
            {
                self.RefAutotrade_Buy_Slider.Value = 0f;
            }
            finally
            {
                SetBool(self, InsideValueChangedField, previousInsideValueChanged);
            }
        }

        private static bool IsMapEditor() => Shared.GameModeHelper.IsMapEditor();

        private static unsafe bool IsSelectedTradepostControlled()
        {
            if (!IsMapEditor())
                return true;

            int activePlayerId = EditorDirector.instance?.ActivePlayerID ?? -1;
            int buildingId = SHCDESE.API.GamePlayerManagerAPI.Instance.GetSelectedBuildingId();
            return activePlayerId > 0 && buildingId > 0 &&
                SHCDESE.API.GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out SHCDESE.Interop.GameBuilding* building) &&
                building != null &&
                building->r_AliveState == SHCDESE.Interop.Enums.AliveState.IsAlive &&
                building->r_BuildingType == SHCDESE.Interop.eStructs.STRUCT_TRADEPOST &&
                building->r_PlayerIdOwner == activePlayerId;
        }
    }
}
