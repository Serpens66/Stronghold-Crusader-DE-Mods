// Feature: Local Shift/Ctrl amount modifiers in the allies goods-transfer panel.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Input;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal sealed class AllyGoodsAmountModifierHook : IDisposable, INotifyPropertyChanged
    {
        private delegate void ButtonClickedDelegate(HUD_AlliesPanel self, string parameter);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly FieldInfo selectedGoodsAmountField;
        private readonly MethodInfo updateGoodsMethod;
        private readonly Hook buttonClickedHook;
        private readonly ButtonClickedDelegate buttonClickedTrampoline;
        private readonly IDisposable keyDownSubscription;
        private readonly IDisposable keyUpSubscription;
        private DisplayMode displayMode;
        private bool failureLogged;
        private bool disposed;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Amount5Text => FormatDisplayedAmount(5);
        public string Amount10Text => FormatDisplayedAmount(10);
        public string Amount25Text => FormatDisplayedAmount(25);
        public string Amount100Text => FormatDisplayedAmount(100);
        public string Amount500Text => FormatDisplayedAmount(500);

        public AllyGoodsAmountModifierHook(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            Type panelType = typeof(HUD_AlliesPanel);
            MethodInfo buttonClickedMethod = FindMethod(panelType, "ButtonClicked", new[] { typeof(string) });
            updateGoodsMethod = FindMethod(panelType, "UpdateGoods", Type.EmptyTypes);
            selectedGoodsAmountField = panelType.GetField(
                "selectedGoodsAmount",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (selectedGoodsAmountField == null || selectedGoodsAmountField.FieldType != typeof(int))
                throw new MissingFieldException(panelType.FullName, "selectedGoodsAmount");

            Hook installedHook = null;
            IDisposable installedKeyDownSubscription = null;
            IDisposable installedKeyUpSubscription = null;
            bool focusChangedSubscribed = false;
            try
            {
                installedHook = new Hook(buttonClickedMethod, (ButtonClickedDelegate)ButtonClickedHook);
                buttonClickedTrampoline = installedHook.GenerateTrampoline<ButtonClickedDelegate>();
                buttonClickedHook = installedHook;

                installedKeyDownSubscription = InputR3EventHooks.OnKeyDown.Observable
                    .Subscribe(OnModifierKeyChanged);
                installedKeyUpSubscription = InputR3EventHooks.OnKeyUp.Observable
                    .Subscribe(OnModifierKeyChanged);
                keyDownSubscription = installedKeyDownSubscription;
                keyUpSubscription = installedKeyUpSubscription;
                Application.focusChanged += OnFocusChanged;
                focusChangedSubscribed = true;
                RefreshDisplayedAmounts();
            }
            catch
            {
                if (focusChangedSubscribed)
                    Application.focusChanged -= OnFocusChanged;
                installedKeyUpSubscription?.Dispose();
                installedKeyDownSubscription?.Dispose();
                installedHook?.Undo();
                installedHook?.Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL ally goods amount modifier hook installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Application.focusChanged -= OnFocusChanged;
            keyUpSubscription?.Dispose();
            keyDownSubscription?.Dispose();
            buttonClickedHook?.Undo();
            buttonClickedHook?.Dispose();
        }

        internal void RefreshSetting() => RefreshDisplayedAmounts();

        internal static int CalculateAmount(int currentAmount, int buttonAmount, bool subtract, bool shift, bool control)
        {
            int delta = buttonAmount;
            if (shift)
                delta = checked(delta * 5);
            if (control)
                delta /= 5;

            long normalizedCurrent = Math.Max(0, currentAmount);
            long result = subtract ? normalizedCurrent - delta : normalizedCurrent + delta;
            return (int)Math.Max(0L, Math.Min(int.MaxValue, result));
        }

        private void ButtonClickedHook(HUD_AlliesPanel self, string parameter)
        {
            if (!settings.EnableClientFeatures ||
                !settings.EnableAllyGoodsAmountModifiers ||
                !TryGetKnownAmountButton(parameter, out int buttonAmount, out bool subtract))
            {
                buttonClickedTrampoline(self, parameter);
                return;
            }

            bool shift = IsHeld(KeyCode.LeftShift, KeyCode.RightShift);
            bool control = IsHeld(KeyCode.LeftControl, KeyCode.RightControl);
            if (!shift && !control)
            {
                buttonClickedTrampoline(self, parameter);
                return;
            }

            int originalAmount = (int)selectedGoodsAmountField.GetValue(self);
            try
            {
                // Applying both modifiers intentionally composes 5x and 0.2x to Vanilla's 1x.
                int modifiedAmount = CalculateAmount(originalAmount, buttonAmount, subtract, shift, control);
                selectedGoodsAmountField.SetValue(self, modifiedAmount);
                updateGoodsMethod.Invoke(self, null);
            }
            catch (Exception ex)
            {
                // Restore before falling back so a failed UI refresh cannot apply two changes.
                selectedGoodsAmountField.SetValue(self, originalAmount);
                if (!failureLogged)
                {
                    failureLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL ally goods amount modifier failed; this click uses Vanilla behavior: {ex}");
                }
                buttonClickedTrampoline(self, parameter);
            }
        }

        private static bool IsHeld(KeyCode left, KeyCode right) =>
            Input.GetKey(left) || Input.GetKey(right);

        private void OnModifierKeyChanged(UnityInputEventArgs args)
        {
            if (args == null || args.Phase != EventHookPhase.Post || !IsModifierKey(args.Key))
                return;

            RefreshDisplayedAmounts();
        }

        private void OnFocusChanged(bool _) => RefreshDisplayedAmounts();

        private static bool IsModifierKey(KeyCode key) =>
            key == KeyCode.LeftShift || key == KeyCode.RightShift ||
            key == KeyCode.LeftControl || key == KeyCode.RightControl;

        private void RefreshDisplayedAmounts()
        {
            if (disposed)
                return;

            DisplayMode newMode = DisplayMode.Normal;
            if (settings.EnableClientFeatures && settings.EnableAllyGoodsAmountModifiers)
            {
                bool shift = IsHeld(KeyCode.LeftShift, KeyCode.RightShift);
                bool control = IsHeld(KeyCode.LeftControl, KeyCode.RightControl);
                if (shift && !control)
                    newMode = DisplayMode.Shift;
                else if (control && !shift)
                    newMode = DisplayMode.Control;
            }

            if (displayMode == newMode)
                return;

            displayMode = newMode;
            NotifyAmountPropertiesChanged();
        }

        private string FormatDisplayedAmount(int vanillaAmount)
        {
            int displayedAmount;
            switch (displayMode)
            {
                case DisplayMode.Shift:
                    displayedAmount = vanillaAmount * 5;
                    break;
                case DisplayMode.Control:
                    displayedAmount = vanillaAmount / 5;
                    break;
                default:
                    displayedAmount = vanillaAmount;
                    break;
            }
            return displayedAmount.ToString(CultureInfo.InvariantCulture);
        }

        private void NotifyAmountPropertiesChanged()
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler == null)
                return;

            handler(this, new PropertyChangedEventArgs(nameof(Amount5Text)));
            handler(this, new PropertyChangedEventArgs(nameof(Amount10Text)));
            handler(this, new PropertyChangedEventArgs(nameof(Amount25Text)));
            handler(this, new PropertyChangedEventArgs(nameof(Amount100Text)));
            handler(this, new PropertyChangedEventArgs(nameof(Amount500Text)));
        }

        private static bool TryGetKnownAmountButton(string parameter, out int amount, out bool subtract)
        {
            subtract = parameter != null && parameter.EndsWith("-", StringComparison.Ordinal);
            switch (subtract ? parameter.Substring(0, parameter.Length - 1) : parameter)
            {
                case "X5": amount = 5; return true;
                case "X10": amount = 10; return true;
                case "X25": amount = 25; return true;
                case "X100": amount = 100; return true;
                case "X500": amount = 500; return true;
                default: amount = 0; return false;
            }
        }

        private static MethodInfo FindMethod(Type type, string name, Type[] parameters)
        {
            MethodInfo method = type.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                parameters,
                null);
            if (method == null || method.ReturnType != typeof(void))
                throw new MissingMethodException(type.FullName, name);
            return method;
        }

        private enum DisplayMode
        {
            Normal,
            Shift,
            Control
        }
    }
}
