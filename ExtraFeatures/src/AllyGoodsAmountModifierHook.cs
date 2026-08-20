// Feature: Local Shift/Ctrl amount modifiers in the allies goods-transfer panel.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace ExtraFeatures
{
    internal sealed class AllyGoodsAmountModifierHook : IDisposable, INotifyPropertyChanged
    {
        private delegate void ButtonClickedDelegate(HUD_AlliesPanel self, string parameter);

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly FieldInfo selectedGoodsAmountField;
        private readonly MethodInfo updateGoodsMethod;
        private readonly Hook buttonClickedHook;
        private readonly ButtonClickedDelegate buttonClickedTrampoline;
        private DisplayMode displayMode;
        private int lastDisplayFrame = -1;
        private bool failureLogged;
        private bool disposed;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Amount5Text => FormatDisplayedAmount(5);
        public string Amount10Text => FormatDisplayedAmount(10);
        public string Amount25Text => FormatDisplayedAmount(25);
        public string Amount100Text => FormatDisplayedAmount(100);
        public string Amount500Text => FormatDisplayedAmount(500);

        public AllyGoodsAmountModifierHook(ManualLogSource log, ExtraFeaturesViewModel settings)
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
            try
            {
                installedHook = new Hook(buttonClickedMethod, (ButtonClickedDelegate)ButtonClickedHook);
                buttonClickedTrampoline = installedHook.GenerateTrampoline<ButtonClickedDelegate>();
                buttonClickedHook = installedHook;
            }
            catch
            {
                installedHook?.Dispose();
                throw;
            }

            Application.onBeforeRender += RefreshDisplayedAmounts;
            Shared.DebugLogHelper.LogDebug(log, "Extra Features ally goods amount modifier hook installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Application.onBeforeRender -= RefreshDisplayedAmounts;
            buttonClickedHook?.Undo();
            buttonClickedHook?.Dispose();
        }

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
                        $"Extra Features ally goods amount modifier failed; this click uses Vanilla behavior: {ex}");
                }
                buttonClickedTrampoline(self, parameter);
            }
        }

        private static bool IsHeld(KeyCode left, KeyCode right) =>
            Input.GetKey(left) || Input.GetKey(right);

        private void RefreshDisplayedAmounts()
        {
            if (lastDisplayFrame == Time.frameCount)
                return;
            lastDisplayFrame = Time.frameCount;

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
