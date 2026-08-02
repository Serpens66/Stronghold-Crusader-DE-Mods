using BepInEx.Logging;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using UnityEngine;

namespace SpawnCastle
{
    public enum SpawnCastleMode
    {
        Disabled,
        Blueprint,
        Spawn
    }

    public sealed class SpawnCastleSettingsViewModel : LobbyModSettingsBaseViewModel
    {
        private readonly ManualLogSource log;
        private readonly AivFileCatalog catalog = new AivFileCatalog();
        private readonly LobbyModSettingsStorage storage;
        private readonly PersistedSettings persistedSettings =
            new PersistedSettings();
        private SpawnCastleMode mode;
        private string selectedCastle;
        private KeyCode blueprintHotkey;
        private double blueprintIconScale;
        private double blueprintIconAlpha;
        private bool isCapturingHotkey;

        public SpawnCastleSettingsViewModel(
            ManualLogSource log,
            string pluginAssemblyLocation)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (string.IsNullOrWhiteSpace(pluginAssemblyLocation))
            {
                throw new ArgumentException(
                    "The plugin assembly location is required.",
                    nameof(pluginAssemblyLocation));
            }

            foreach (SpawnCastleMode option in Enum.GetValues(typeof(SpawnCastleMode)))
                ModeOptions.Add(option);
            foreach (string option in catalog.Discover())
                CastleOptions.Add(option);

            string defaultCastle = CastleOptions.Count > 0
                ? CastleOptions[0]
                : string.Empty;
            storage = new LobbyModSettingsStorage(
                pluginAssemblyLocation,
                SpawnCastlePlugin.PluginGuid);
            storage.Load(persistedSettings);

            mode = NormalizeMode(persistedSettings.Mode);
            selectedCastle = NormalizeCastle(
                persistedSettings.SelectedCastle,
                defaultCastle);
            blueprintHotkey = NormalizeKeyCode(
                persistedSettings.BlueprintHotkey);
            blueprintIconScale = NormalizeIconScale(
                persistedSettings.BlueprintIconScale);
            blueprintIconAlpha = NormalizeIconAlpha(
                persistedSettings.BlueprintIconAlpha);
            if (persistedSettings.HasBlueprintHudPosition)
            {
                persistedSettings.BlueprintHudPositionX = NormalizeUnitValue(
                    persistedSettings.BlueprintHudPositionX);
                persistedSettings.BlueprintHudPositionY = NormalizeUnitValue(
                    persistedSettings.BlueprintHudPositionY);
            }
            PersistCurrentValues();

            AssignHotkeyCommand = new RelayCommand(BeginHotkeyCapture);
            ClearHotkeyCommand = new RelayCommand(ClearHotkey);
            HotkeyInputCommand =
                new ParameterRelayCommand(CaptureNoesisHotkeyInput);
        }

        internal event Action SettingsChanged;
        internal event Action BlueprintVisualSettingsChanged;
        internal event Action HotkeyCaptureRequested;

        public ObservableCollection<SpawnCastleMode> ModeOptions { get; } =
            new ObservableCollection<SpawnCastleMode>();

        public ObservableCollection<string> CastleOptions { get; } =
            new ObservableCollection<string>();

        public ICommand AssignHotkeyCommand { get; }
        public ICommand ClearHotkeyCommand { get; }
        public ICommand HotkeyInputCommand { get; }

        public int AvailableFileCount => CastleOptions.Count;

        public string InventoryText =>
            $"{AvailableFileCount} local AIVJSON files found. " +
            "Blueprint settings stay local in multiplayer.";

        public SpawnCastleMode Mode
        {
            get => mode;
            set
            {
                SpawnCastleMode normalized = NormalizeMode(value);
                if (mode == normalized)
                    return;

                mode = normalized;
                PersistCurrentValues();
                OnPropertyChanged(nameof(Mode));
                OnPropertyChanged(nameof(IsBlueprintMode));
                OnPropertyChanged(nameof(IsSpawnMode));
                Shared.DebugLogHelper.LogInfo(log, $"SpawnCastle mode changed to '{mode}'.");
                SettingsChanged?.Invoke();
            }
        }

        public string SelectedCastle
        {
            get => selectedCastle;
            set
            {
                string normalized = NormalizeCastle(value, string.Empty);
                if (string.Equals(selectedCastle, normalized, StringComparison.Ordinal))
                    return;

                selectedCastle = normalized;
                PersistCurrentValues();
                OnPropertyChanged(nameof(SelectedCastle));
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"SpawnCastle local AIVJSON selection changed to '{selectedCastle}'.");
                SettingsChanged?.Invoke();
            }
        }

        public int BlueprintHotkey
        {
            get => (int)blueprintHotkey;
            set => SetHotkey(NormalizeKeyCode(value));
        }

        public double BlueprintIconScale
        {
            get => blueprintIconScale;
            set
            {
                double normalized = NormalizeIconScale(value);
                if (Math.Abs(blueprintIconScale - normalized) < 0.0001)
                    return;

                blueprintIconScale = normalized;
                PersistCurrentValues();
                OnPropertyChanged(nameof(BlueprintIconScale));
                OnPropertyChanged(nameof(BlueprintIconScaleText));
                BlueprintVisualSettingsChanged?.Invoke();
            }
        }

        public double BlueprintIconAlpha
        {
            get => blueprintIconAlpha;
            set
            {
                double normalized = NormalizeIconAlpha(value);
                if (Math.Abs(blueprintIconAlpha - normalized) < 0.0001)
                    return;

                blueprintIconAlpha = normalized;
                PersistCurrentValues();
                OnPropertyChanged(nameof(BlueprintIconAlpha));
                OnPropertyChanged(nameof(BlueprintIconAlphaText));
                BlueprintVisualSettingsChanged?.Invoke();
            }
        }

        public string BlueprintIconScaleText =>
            BlueprintIconScale.ToString("0.00");

        public string BlueprintIconAlphaText =>
            BlueprintIconAlpha.ToString("0.00");

        public string HotkeyDisplayText =>
            blueprintHotkey == KeyCode.None
                ? "Not assigned"
                : GetKeyDisplayName(blueprintHotkey);

        public string HotkeyCaptureButtonText =>
            isCapturingHotkey ? "Press any key..." : "Assign key";

        public bool IsCapturingHotkey => isCapturingHotkey;
        public bool IsBlueprintMode => mode == SpawnCastleMode.Blueprint;
        public bool IsSpawnMode => mode == SpawnCastleMode.Spawn;
        internal KeyCode BlueprintHotkeyCode => blueprintHotkey;
        internal float BlueprintIconScaleValue => (float)blueprintIconScale;
        internal float BlueprintIconAlphaValue => (float)blueprintIconAlpha;

        internal bool TryGetBlueprintHudPosition(
            out double normalizedX,
            out double normalizedY)
        {
            normalizedX = NormalizeUnitValue(
                persistedSettings.BlueprintHudPositionX);
            normalizedY = NormalizeUnitValue(
                persistedSettings.BlueprintHudPositionY);
            return persistedSettings.HasBlueprintHudPosition;
        }

        internal void SaveBlueprintHudPosition(
            double normalizedX,
            double normalizedY)
        {
            persistedSettings.HasBlueprintHudPosition = true;
            persistedSettings.BlueprintHudPositionX =
                NormalizeUnitValue(normalizedX);
            persistedSettings.BlueprintHudPositionY =
                NormalizeUnitValue(normalizedY);
            PersistCurrentValues();
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Blueprint HUD position saved: " +
                $"x={persistedSettings.BlueprintHudPositionX:0.000}, " +
                $"y={persistedSettings.BlueprintHudPositionY:0.000}.");
        }

        internal void LogBlueprintHudMessage(string message)
        {
            Shared.DebugLogHelper.LogInfo(log, message);
        }

        internal bool TryResolveSelectedFile(out string fullPath)
        {
            return catalog.TryResolve(selectedCastle, out fullPath);
        }

        internal void CompleteHotkeyCapture(KeyCode key)
        {
            SetCaptureState(false);
            SetHotkey(key);
        }

        private void CaptureNoesisHotkeyInput(object parameter)
        {
            if (!isCapturingHotkey)
                return;

            KeyCode key;
            Noesis.RoutedEventArgs routedArgs;
            if (parameter is Noesis.KeyEventArgs keyArgs &&
                TryMapNoesisKey(keyArgs.Key, out key))
            {
                routedArgs = keyArgs;
            }
            else if (parameter is Noesis.MouseButtonEventArgs mouseArgs &&
                     TryMapNoesisMouseButton(
                         mouseArgs.ChangedButton,
                         out key))
            {
                routedArgs = mouseArgs;
            }
            else
            {
                return;
            }

            routedArgs.Handled = true;
            if (KeyManager.instance != null)
                KeyManager.instance.HotKeySelectorMode = false;
            CompleteHotkeyCapture(key);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Blueprint hotkey captured directly from Noesis: " +
                $"key={key}, value={(int)key}.");
        }

        private void BeginHotkeyCapture()
        {
            SetCaptureState(true);
            HotkeyCaptureRequested?.Invoke();
        }

        private void ClearHotkey()
        {
            SetCaptureState(false);
            if (KeyManager.instance != null)
                KeyManager.instance.HotKeySelectorMode = false;
            SetHotkey(KeyCode.None);
        }

        private static bool TryMapNoesisMouseButton(
            Noesis.MouseButton button,
            out KeyCode key)
        {
            switch (button)
            {
                case Noesis.MouseButton.Left:
                    key = KeyCode.Mouse0;
                    return true;
                case Noesis.MouseButton.Right:
                    key = KeyCode.Mouse1;
                    return true;
                case Noesis.MouseButton.Middle:
                    key = KeyCode.Mouse2;
                    return true;
                case Noesis.MouseButton.XButton1:
                    key = KeyCode.Mouse3;
                    return true;
                case Noesis.MouseButton.XButton2:
                    key = KeyCode.Mouse4;
                    return true;
                default:
                    key = KeyCode.None;
                    return false;
            }
        }

        private static bool TryMapNoesisKey(
            Noesis.Key source,
            out KeyCode key)
        {
            if (source >= Noesis.Key.A && source <= Noesis.Key.Z)
            {
                key = (KeyCode)((int)KeyCode.A +
                    ((int)source - (int)Noesis.Key.A));
                return true;
            }
            if (source >= Noesis.Key.D0 && source <= Noesis.Key.D9)
            {
                key = (KeyCode)((int)KeyCode.Alpha0 +
                    ((int)source - (int)Noesis.Key.D0));
                return true;
            }
            if (source >= Noesis.Key.NumPad0 &&
                source <= Noesis.Key.NumPad9)
            {
                key = (KeyCode)((int)KeyCode.Keypad0 +
                    ((int)source - (int)Noesis.Key.NumPad0));
                return true;
            }
            if (source >= Noesis.Key.F1 && source <= Noesis.Key.F15)
            {
                key = (KeyCode)((int)KeyCode.F1 +
                    ((int)source - (int)Noesis.Key.F1));
                return true;
            }

            switch (source)
            {
                case Noesis.Key.Back: key = KeyCode.Backspace; return true;
                case Noesis.Key.Tab: key = KeyCode.Tab; return true;
                case Noesis.Key.Clear: key = KeyCode.Clear; return true;
                case Noesis.Key.Return: key = KeyCode.Return; return true;
                case Noesis.Key.Pause: key = KeyCode.Pause; return true;
                case Noesis.Key.Escape: key = KeyCode.Escape; return true;
                case Noesis.Key.Space: key = KeyCode.Space; return true;
                case Noesis.Key.PageUp: key = KeyCode.PageUp; return true;
                case Noesis.Key.PageDown: key = KeyCode.PageDown; return true;
                case Noesis.Key.End: key = KeyCode.End; return true;
                case Noesis.Key.Home: key = KeyCode.Home; return true;
                case Noesis.Key.Left: key = KeyCode.LeftArrow; return true;
                case Noesis.Key.Up: key = KeyCode.UpArrow; return true;
                case Noesis.Key.Right: key = KeyCode.RightArrow; return true;
                case Noesis.Key.Down: key = KeyCode.DownArrow; return true;
                case Noesis.Key.Print:
                    key = KeyCode.Print;
                    return true;
                case Noesis.Key.Insert: key = KeyCode.Insert; return true;
                case Noesis.Key.Delete: key = KeyCode.Delete; return true;
                case Noesis.Key.Help: key = KeyCode.Help; return true;
                case Noesis.Key.Multiply: key = KeyCode.KeypadMultiply; return true;
                case Noesis.Key.Add: key = KeyCode.KeypadPlus; return true;
                case Noesis.Key.Subtract: key = KeyCode.KeypadMinus; return true;
                case Noesis.Key.Decimal: key = KeyCode.KeypadPeriod; return true;
                case Noesis.Key.Divide: key = KeyCode.KeypadDivide; return true;
                case Noesis.Key.NumLock: key = KeyCode.Numlock; return true;
                case Noesis.Key.Scroll: key = KeyCode.ScrollLock; return true;
                case Noesis.Key.CapsLock: key = KeyCode.CapsLock; return true;
                case Noesis.Key.LeftShift: key = KeyCode.LeftShift; return true;
                case Noesis.Key.RightShift: key = KeyCode.RightShift; return true;
                case Noesis.Key.LeftCtrl: key = KeyCode.LeftControl; return true;
                case Noesis.Key.RightCtrl: key = KeyCode.RightControl; return true;
                case Noesis.Key.LeftAlt: key = KeyCode.LeftAlt; return true;
                case Noesis.Key.RightAlt: key = KeyCode.RightAlt; return true;
                case Noesis.Key.LWin: key = KeyCode.LeftWindows; return true;
                case Noesis.Key.RWin: key = KeyCode.RightWindows; return true;
                case Noesis.Key.Apps: key = KeyCode.Menu; return true;
                case Noesis.Key.OemSemicolon: key = KeyCode.Semicolon; return true;
                case Noesis.Key.OemPlus: key = KeyCode.Equals; return true;
                case Noesis.Key.OemComma: key = KeyCode.Comma; return true;
                case Noesis.Key.OemMinus: key = KeyCode.Minus; return true;
                case Noesis.Key.OemPeriod: key = KeyCode.Period; return true;
                case Noesis.Key.OemQuestion: key = KeyCode.Slash; return true;
                case Noesis.Key.OemTilde: key = KeyCode.BackQuote; return true;
                case Noesis.Key.OemOpenBrackets:
                    key = KeyCode.LeftBracket;
                    return true;
                case Noesis.Key.OemPipe: key = KeyCode.Backslash; return true;
                case Noesis.Key.OemCloseBrackets:
                    key = KeyCode.RightBracket;
                    return true;
                case Noesis.Key.OemQuotes: key = KeyCode.Quote; return true;
                case Noesis.Key.GamepadAccept:
                    key = KeyCode.JoystickButton0;
                    return true;
                case Noesis.Key.GamepadCancel:
                    key = KeyCode.JoystickButton1;
                    return true;
                case Noesis.Key.GamepadContext1:
                    key = KeyCode.JoystickButton2;
                    return true;
                case Noesis.Key.GamepadContext2:
                    key = KeyCode.JoystickButton3;
                    return true;
                case Noesis.Key.GamepadPageLeft:
                    key = KeyCode.JoystickButton4;
                    return true;
                case Noesis.Key.GamepadPageRight:
                    key = KeyCode.JoystickButton5;
                    return true;
                case Noesis.Key.GamepadView:
                    key = KeyCode.JoystickButton6;
                    return true;
                case Noesis.Key.GamepadMenu:
                    key = KeyCode.JoystickButton7;
                    return true;
                default:
                    key = KeyCode.None;
                    return false;
            }
        }

        private void SetCaptureState(bool value)
        {
            if (isCapturingHotkey == value)
                return;

            isCapturingHotkey = value;
            OnPropertyChanged(nameof(IsCapturingHotkey));
            OnPropertyChanged(nameof(HotkeyCaptureButtonText));
        }

        private void SetHotkey(KeyCode key)
        {
            if (blueprintHotkey == key)
                return;

            blueprintHotkey = key;
            PersistCurrentValues();
            OnPropertyChanged(nameof(BlueprintHotkey));
            OnPropertyChanged(nameof(HotkeyDisplayText));
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Blueprint toggle hotkey changed to '{HotkeyDisplayText}' ({(int)blueprintHotkey}).");
        }

        private string NormalizeCastle(string value, string fallback)
        {
            string candidate = value?.Trim() ?? string.Empty;
            foreach (string option in CastleOptions)
            {
                if (string.Equals(option, candidate, StringComparison.OrdinalIgnoreCase))
                    return option;
            }

            if (!string.IsNullOrEmpty(candidate))
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Stored AIVJSON is no longer available: '{candidate}'.");
            }

            return fallback;
        }

        private static SpawnCastleMode NormalizeMode(SpawnCastleMode value)
        {
            return Enum.IsDefined(typeof(SpawnCastleMode), value)
                ? value
                : SpawnCastleMode.Disabled;
        }

        private static KeyCode NormalizeKeyCode(int value)
        {
            return Enum.IsDefined(typeof(KeyCode), value)
                ? (KeyCode)value
                : KeyCode.None;
        }

        private static double NormalizeIconScale(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 1.0;

            // Keep malformed persisted values inside the range exposed by the HUD.
            return Math.Round(
                Math.Max(0.05, Math.Min(1.0, value)),
                2,
                MidpointRounding.AwayFromZero);
        }

        private static double NormalizeIconAlpha(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0.3;

            return Math.Round(
                Math.Max(0.0, Math.Min(1.0, value)),
                2,
                MidpointRounding.AwayFromZero);
        }

        private static double NormalizeUnitValue(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0.0;

            return Math.Max(0.0, Math.Min(1.0, value));
        }

        private void PersistCurrentValues()
        {
            persistedSettings.Mode = mode;
            persistedSettings.SelectedCastle = selectedCastle;
            persistedSettings.BlueprintHotkey = (int)blueprintHotkey;
            persistedSettings.BlueprintIconScale = blueprintIconScale;
            persistedSettings.BlueprintIconAlpha = blueprintIconAlpha;
            storage.Save(persistedSettings);
        }

        private static string GetKeyDisplayName(KeyCode key)
        {
            try
            {
                string display = CrusaderDE.HUD_Options.GetKeyCodeString(key);
                return string.IsNullOrWhiteSpace(display) ? key.ToString() : display;
            }
            catch
            {
                return key.ToString();
            }
        }

        private sealed class ParameterRelayCommand : ICommand
        {
            private readonly Action<object> execute;

            public ParameterRelayCommand(Action<object> execute)
            {
                this.execute =
                    execute ?? throw new ArgumentNullException(nameof(execute));
            }

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public void Execute(object parameter)
            {
                execute(parameter);
            }

            public event EventHandler CanExecuteChanged
            {
                add { }
                remove { }
            }
        }

        private sealed class PersistedSettings
        {
            // These attributes are storage markers only. This private model is
            // never registered for lobby sync, so all options remain local.
            [SyncPerPlayer]
            public SpawnCastleMode Mode { get; set; } =
                SpawnCastleMode.Disabled;

            [SyncPerPlayer]
            public string SelectedCastle { get; set; } = string.Empty;

            [SyncPerPlayer]
            public int BlueprintHotkey { get; set; } = (int)KeyCode.None;

            [SyncPerPlayer]
            public double BlueprintIconScale { get; set; } = 1.0;

            [SyncPerPlayer]
            public double BlueprintIconAlpha { get; set; } = 0.3;

            [SyncPerPlayer]
            public bool HasBlueprintHudPosition { get; set; }

            [SyncPerPlayer]
            public double BlueprintHudPositionX { get; set; }

            [SyncPerPlayer]
            public double BlueprintHudPositionY { get; set; }
        }
    }
}
