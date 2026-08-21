// Feature: Tick-synchronized multiplayer game-speed controls.
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
using System;
using System.Diagnostics;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed class MultiplayerGameSpeedRuntime : IDisposable
    {
        private delegate void KeyManagerUpdateDelegate(KeyManager self);
        private delegate bool IsActionPressedDelegate(KeyManager self, Enums.KeyFunctions function);
        private delegate void OptionsInitDelegate(HUD_Options self);
        private delegate void GameSpeedSliderChangedDelegate(
            HUD_Options self,
            object sender,
            RoutedPropertyChangedEventArgs<float> args);

        private const long TransportErrorLogIntervalMilliseconds = 5000;

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly MultiplayerFeatureGate multiplayerFeatureGate;
        private readonly FieldInfo functionMapField;
        private readonly GameSpeedRepeatScheduler increaseRepeat = new GameSpeedRepeatScheduler();
        private readonly GameSpeedRepeatScheduler decreaseRepeat = new GameSpeedRepeatScheduler();

        private Hook keyManagerUpdateHook;
        private Hook isActionPressedHook;
        private Hook optionsInitHook;
        private Hook sliderChangedHook;
        private KeyManagerUpdateDelegate keyManagerUpdateTrampoline;
        private IsActionPressedDelegate isActionPressedTrampoline;
        private OptionsInitDelegate optionsInitTrampoline;
        private GameSpeedSliderChangedDelegate sliderChangedTrampoline;
        private R3PacketEventHook<MultiplayerGameSpeedChangePacket> packetHook;
        private IDisposable packetSubscription;
        private long lastTransportErrorTimestamp;
        private int lastSliderBucket = -1;
        private bool suppressSliderEvent;
        private bool suppressVanillaSpeedKeybinds;
        private bool networkInitialized;
        private bool hooksInstalled;
        private bool disposed;

        public MultiplayerGameSpeedRuntime(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            MultiplayerFeatureGate multiplayerFeatureGate)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.multiplayerFeatureGate = multiplayerFeatureGate ?? throw new ArgumentNullException(nameof(multiplayerFeatureGate));
            functionMapField = typeof(KeyManager).GetField(
                "functionMap",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
                throw new MissingFieldException(typeof(KeyManager).FullName, "functionMap");
        }

        public void InitializeNetwork()
        {
            if (networkInitialized)
                return;

            packetHook = GameNetworkAPI.Instance.GetPacketEventFor<MultiplayerGameSpeedChangePacket>();
            packetSubscription = packetHook.GetBaseHook().Observable.Subscribe(OnPacketReceived);
            networkInitialized = true;
            LogInfo($"Chore packet registered eagerly: packetId={packetHook.GetPacketId()}, protocolVersion={MultiplayerGameSpeedPolicy.ProtocolVersion}.");
        }

        public void InstallHooks()
        {
            if (hooksInstalled)
                return;

            Hook installedKeyHook = null;
            Hook installedActionPressedHook = null;
            Hook installedInitHook = null;
            Hook installedSliderHook = null;
            try
            {
                installedActionPressedHook = new Hook(
                    FindInstanceMethod(
                        typeof(KeyManager),
                        "IsActionPressed",
                        new[] { typeof(Enums.KeyFunctions) },
                        typeof(bool)),
                    (IsActionPressedDelegate)IsActionPressedHook);
                IsActionPressedDelegate installedActionPressedTrampoline =
                    installedActionPressedHook.GenerateTrampoline<IsActionPressedDelegate>();

                installedKeyHook = new Hook(
                    FindInstanceMethod(typeof(KeyManager), "Update", Type.EmptyTypes),
                    (KeyManagerUpdateDelegate)KeyManagerUpdateHook);
                KeyManagerUpdateDelegate installedKeyTrampoline =
                    installedKeyHook.GenerateTrampoline<KeyManagerUpdateDelegate>();

                installedInitHook = new Hook(
                    FindInstanceMethod(typeof(HUD_Options), "Init", Type.EmptyTypes),
                    (OptionsInitDelegate)OptionsInitHook);
                OptionsInitDelegate installedInitTrampoline =
                    installedInitHook.GenerateTrampoline<OptionsInitDelegate>();

                Type[] sliderParameters =
                {
                    typeof(object),
                    typeof(RoutedPropertyChangedEventArgs<float>)
                };
                installedSliderHook = new Hook(
                    FindInstanceMethod(typeof(HUD_Options), "GameSpeedSlider_ValueChanged", sliderParameters),
                    (GameSpeedSliderChangedDelegate)GameSpeedSliderChangedHook);
                GameSpeedSliderChangedDelegate installedSliderTrampoline =
                    installedSliderHook.GenerateTrampoline<GameSpeedSliderChangedDelegate>();

                keyManagerUpdateHook = installedKeyHook;
                keyManagerUpdateTrampoline = installedKeyTrampoline;
                isActionPressedHook = installedActionPressedHook;
                isActionPressedTrampoline = installedActionPressedTrampoline;
                optionsInitHook = installedInitHook;
                optionsInitTrampoline = installedInitTrampoline;
                sliderChangedHook = installedSliderHook;
                sliderChangedTrampoline = installedSliderTrampoline;
                hooksInstalled = true;
                LogInfo("managed keybind and in-game options hooks installed.");
            }
            catch
            {
                installedSliderHook?.Dispose();
                installedInitHook?.Dispose();
                installedKeyHook?.Dispose();
                installedActionPressedHook?.Dispose();
                throw;
            }
        }

        public void ApplySetting()
        {
            ResetRepeatState();

            try
            {
                RefreshOpenOptionsUi();
            }
            catch (Exception ex)
            {
                LogError($"could not refresh the in-game game-speed controls after a setting change: {ex}");
            }
        }

        public void ResetMapState()
        {
            lastSliderBucket = -1;
            suppressSliderEvent = false;
            suppressVanillaSpeedKeybinds = false;
            ResetRepeatState();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            sliderChangedHook?.Undo();
            sliderChangedHook?.Dispose();
            optionsInitHook?.Undo();
            optionsInitHook?.Dispose();
            keyManagerUpdateHook?.Undo();
            keyManagerUpdateHook?.Dispose();
            isActionPressedHook?.Undo();
            isActionPressedHook?.Dispose();
            packetSubscription?.Dispose();
            hooksInstalled = false;
            networkInitialized = false;
        }

        private void KeyManagerUpdateHook(KeyManager self)
        {
            bool repeatEnabled = CanUseKeyRepeat(self);
            suppressVanillaSpeedKeybinds = repeatEnabled;
            try
            {
                keyManagerUpdateTrampoline(self);
            }
            finally
            {
                suppressVanillaSpeedKeybinds = false;
            }

            if (!repeatEnabled)
            {
                ResetRepeatState();
                QueueLegacyMultiplayerPresses(self);
                return;
            }

            // Read the states after Vanilla updated KeyManager so release cannot emit a late repeat.
            bool increasePressed = IsConfiguredActionState(
                self, Enums.KeyFunctions.IncreaseEngineSpeed, held: false);
            bool decreasePressed = IsConfiguredActionState(
                self, Enums.KeyFunctions.DecreaseEngineSpeed, held: false);
            bool increaseHeld = IsConfiguredActionState(
                self, Enums.KeyFunctions.IncreaseEngineSpeed, held: true);
            bool decreaseHeld = IsConfiguredActionState(
                self, Enums.KeyFunctions.DecreaseEngineSpeed, held: true);
            bool shiftHeld = self.isShiftDown();

            if (increasePressed)
                ApplyOrQueueChange(
                    shiftHeld
                        ? MultiplayerGameSpeedPolicy.FastIncreaseAction
                        : MultiplayerGameSpeedPolicy.IncreaseAction,
                    shiftHeld ? "shift-keybind-increase" : "keybind-increase");
            if (decreasePressed)
                ApplyOrQueueChange(
                    shiftHeld
                        ? MultiplayerGameSpeedPolicy.FastDecreaseAction
                        : MultiplayerGameSpeedPolicy.DecreaseAction,
                    shiftHeld ? "shift-keybind-decrease" : "keybind-decrease");

            int currentSpeed = GetCurrentSpeed();
            int maximumSpeed = GetMaximumSpeed();
            long now = Stopwatch.GetTimestamp();
            bool blocked = increaseHeld && decreaseHeld;
            bool repeatIncrease = increaseRepeat.Update(
                increaseHeld,
                increasePressed,
                blocked,
                currentSpeed >= maximumSpeed,
                now,
                Stopwatch.Frequency);
            bool repeatDecrease = decreaseRepeat.Update(
                decreaseHeld,
                decreasePressed,
                blocked,
                currentSpeed <= MultiplayerGameSpeedPolicy.MinimumSpeed,
                now,
                Stopwatch.Frequency);

            if (repeatIncrease)
                ApplyOrQueueChange(
                    self.isShiftDown()
                        ? MultiplayerGameSpeedPolicy.FastIncreaseAction
                        : MultiplayerGameSpeedPolicy.IncreaseAction,
                    "held-keybind-increase");
            if (repeatDecrease)
                ApplyOrQueueChange(
                    self.isShiftDown()
                        ? MultiplayerGameSpeedPolicy.FastDecreaseAction
                        : MultiplayerGameSpeedPolicy.DecreaseAction,
                    "held-keybind-decrease");
        }

        private bool IsActionPressedHook(KeyManager self, Enums.KeyFunctions function)
        {
            if (suppressVanillaSpeedKeybinds && IsSpeedFunction(function))
                return false;

            return isActionPressedTrampoline(self, function);
        }

        private bool IsConfiguredActionState(
            KeyManager self,
            Enums.KeyFunctions function,
            bool held)
        {
            if (self == null || !IsSpeedFunction(function))
                return false;

            int[,] functionMap = functionMapField.GetValue(self) as int[,];
            if (functionMap == null || (int)function >= functionMap.GetLength(0))
                return false;

            for (int slot = 0; slot < functionMap.GetLength(1); slot++)
            {
                int mappedCode = functionMap[(int)function, slot];
                if (mappedCode < 0)
                    continue;

                bool requiresControl = (mappedCode & 0x20000) != 0;
                bool requiresAlt = (mappedCode & 0x40000) != 0;
                bool requiresShift = (mappedCode & 0x10000) != 0;
                if (requiresControl != self.isCtrlDown() || requiresAlt != self.isAltDown())
                    continue;
                if (!self.isShiftDown() && requiresShift)
                    continue;

                // Shift is intentionally additive; retain every other configured modifier.
                bool active = held
                    ? self.IsKeyHeldDown((UnityEngine.KeyCode)mappedCode, ignoreModifiers: true)
                    : self.IsKeyPressed((UnityEngine.KeyCode)mappedCode, ignoreModifiers: true);
                if (active)
                    return true;
            }

            return false;
        }

        private void ApplyOrQueueChange(int action, string source)
        {
            Director director = Director.instance;
            if (director == null || !director.SimRunning)
                return;

            if (director.MultiplayerGame)
            {
                if (CanRequestMultiplayerChange())
                    TryQueueChange(action, 0, source);
                return;
            }

            int previousSpeed = GetCurrentSpeed();
            if (!MultiplayerGameSpeedPolicy.TryResolve(
                    previousSpeed,
                    action,
                    0,
                    GetMaximumSpeed(),
                    out int resolvedSpeed) ||
                resolvedSpeed == previousSpeed)
                return;

            director.SetEngineFrameRate(resolvedSpeed);
            OnScreenText.Instance?.addOSTEntry(Enums.eOnScreenText.OST_GAME_SPEED, resolvedSpeed);
            ConfigSettings.Settings_GameSpeed = resolvedSpeed;
            ConfigSettings.SaveSettings();
            RefreshOpenOptionsUi(resolvedSpeed);
            LogInfo($"singleplayer Shift game-speed change executed: action={action}, previousSpeed={previousSpeed}, resolvedSpeed={resolvedSpeed}.");
        }

        private static bool IsSpeedFunction(Enums.KeyFunctions function) =>
            function == Enums.KeyFunctions.IncreaseEngineSpeed ||
            function == Enums.KeyFunctions.DecreaseEngineSpeed;

        private bool CanUseKeyRepeat(KeyManager self)
        {
            if (self == null || !settings.EnableMod || !settings.EnableShiftGameSpeedSteps)
                return false;

            Director director = Director.instance;
            if (director == null || !director.SimRunning)
                return false;

            return !director.MultiplayerGame || CanRequestMultiplayerChange();
        }

        private void QueueLegacyMultiplayerPresses(KeyManager self)
        {
            if (!CanRequestMultiplayerChange() || self == null)
                return;

            if (self.IsActionPressed(Enums.KeyFunctions.IncreaseEngineSpeed))
                TryQueueChange(MultiplayerGameSpeedPolicy.IncreaseAction, 0, "keybind-increase");
            if (self.IsActionPressed(Enums.KeyFunctions.DecreaseEngineSpeed))
                TryQueueChange(MultiplayerGameSpeedPolicy.DecreaseAction, 0, "keybind-decrease");
        }

        private void ResetRepeatState()
        {
            increaseRepeat.Reset();
            decreaseRepeat.Reset();
        }

        private void OptionsInitHook(HUD_Options self)
        {
            optionsInitTrampoline(self);
            try
            {
                RefreshOptionsUi(self);
            }
            catch (Exception ex)
            {
                // Vanilla has already completed. A mod UI refresh must never break opening options.
                LogError($"could not refresh the multiplayer game-speed controls after Vanilla initialized the options UI: {ex}");
            }
        }

        private void GameSpeedSliderChangedHook(
            HUD_Options self,
            object sender,
            RoutedPropertyChangedEventArgs<float> args)
        {
            sliderChangedTrampoline(self, sender, args);
            if (suppressSliderEvent || !CanRequestMultiplayerChange())
                return;

            Slider slider = sender as Slider ?? self?.FindName("GameSpeedSlider") as Slider;
            if (slider == null)
                return;

            int targetSpeed = MultiplayerGameSpeedPolicy.NormalizeObservedSpeed(
                (int)Math.Round(slider.Value * MultiplayerGameSpeedPolicy.SpeedStep),
                GetMaximumSpeed());
            if (targetSpeed == lastSliderBucket)
                return;

            if (TryQueueChange(MultiplayerGameSpeedPolicy.SetAction, targetSpeed, "options-slider"))
            {
                lastSliderBucket = targetSpeed;
                return;
            }

            // A failed request must not leave the local slider displaying an unapplied value.
            RefreshOpenOptionsUi(GetCurrentSpeed());
        }

        private bool CanRequestMultiplayerChange()
        {
            try
            {
                return settings.EnableMod &&
                    settings.EnableMultiplayerGameSpeedChanges &&
                    Director.instance != null &&
                    Director.instance.MultiplayerGame &&
                    Director.instance.SimRunning &&
                    multiplayerFeatureGate.BlocksLocalStateChanges;
            }
            catch (Exception ex)
            {
                LogTransportFailureThrottled($"game-mode detection failed: {ex.Message}");
                return false;
            }
        }

        private bool TryQueueChange(int action, int targetSpeed, string source)
        {
            if (!IsChoreTransportReady())
            {
                LogTransportFailureThrottled($"{source} was refused because the Chore transport is unavailable");
                return false;
            }

            var packet = new MultiplayerGameSpeedChangePacket
            {
                ProtocolVersion = MultiplayerGameSpeedPolicy.ProtocolVersion,
                Action = action,
                TargetSpeed = targetSpeed
            };
            byte[] body = GameNetworkAPI.Serialize(packet);
            byte[] blob = new byte[sizeof(short) + body.Length];
            BitConverter.GetBytes(packetHook.GetPacketId()).CopyTo(blob, 0);
            Buffer.BlockCopy(body, 0, blob, sizeof(short), body.Length);

            Func<byte[], bool> sendRawBlob = ChoreNetworkTransport.SendRawBlob;
            bool queued = sendRawBlob != null && sendRawBlob(blob);
            if (!queued)
            {
                LogTransportFailureThrottled($"{source} was not queued; no local speed change was applied");
                return false;
            }

            LogInfo($"game-speed Chore queued: source={source}, action={action}, targetSpeed={targetSpeed}, payloadBytes={blob.Length}.");
            return true;
        }

        private void OnPacketReceived(ReceiveCustomPacketEventArgs<MultiplayerGameSpeedChangePacket> args)
        {
            MultiplayerGameSpeedChangePacket packet = args?.Packet;
            if (packet == null ||
                packet.ProtocolVersion != MultiplayerGameSpeedPolicy.ProtocolVersion ||
                !MultiplayerGameSpeedPolicy.TryResolvePacket(
                    GetCurrentSpeed(),
                    packet.ProtocolVersion,
                    packet.Action,
                    packet.TargetSpeed,
                    GetMaximumSpeed(),
                    out int resolvedSpeed))
            {
                LogError("rejected a multiplayer game-speed Chore with an invalid payload.");
                return;
            }

            try
            {
                Director director = Director.instance;
                if (director == null || !director.MultiplayerGame || !director.SimRunning)
                {
                    LogError("could not execute a multiplayer game-speed Chore because no multiplayer simulation is running.");
                    return;
                }

                int previousSpeed = GetCurrentSpeed();
                if (resolvedSpeed != previousSpeed)
                {
                    director.SetEngineFrameRate(resolvedSpeed);
                    OnScreenText.Instance?.addOSTEntry(Enums.eOnScreenText.OST_GAME_SPEED, resolvedSpeed);
                }

                RefreshOpenOptionsUi(resolvedSpeed);
                LogInfo($"game-speed Chore executed: action={packet.Action}, requestedTarget={packet.TargetSpeed}, previousSpeed={previousSpeed}, resolvedSpeed={resolvedSpeed}.");
            }
            catch (Exception ex)
            {
                LogError($"multiplayer game-speed Chore execution failed: {ex}");
            }
        }

        private void RefreshOpenOptionsUi()
        {
            if (!TryGetLoadedMainViewModel(out MainViewModel main))
                return;

            HUD_Options options = main.HUDOptions;
            if (options != null)
                RefreshOptionsUi(options);
            else if (!CanRequestMultiplayerChange())
                main.OptionsGameSpeedVis = Visibility.Collapsed;
        }

        private void RefreshOpenOptionsUi(int speed)
        {
            if (!TryGetLoadedMainViewModel(out MainViewModel main))
                return;

            main.GameSpeedValue = speed.ToString();
            HUD_Options options = main.HUDOptions;
            Slider slider = options?.FindName("GameSpeedSlider") as Slider;
            if (slider == null)
                return;

            suppressSliderEvent = true;
            try
            {
                slider.Minimum = MultiplayerGameSpeedPolicy.MinimumSpeed / MultiplayerGameSpeedPolicy.SpeedStep;
                slider.Maximum = (float)GetMaximumSpeed() / MultiplayerGameSpeedPolicy.SpeedStep;
                slider.TickFrequency = 1;
                slider.IsSnapToTickEnabled = true;
                slider.Value = speed / MultiplayerGameSpeedPolicy.SpeedStep;
                lastSliderBucket = speed;
            }
            finally
            {
                suppressSliderEvent = false;
            }
        }

        private void RefreshOptionsUi(HUD_Options options)
        {
            if (!TryGetLoadedMainViewModel(out MainViewModel main))
                return;

            // Outside a running multiplayer match Vanilla owns this visibility completely.
            if (Director.instance == null || !Director.instance.MultiplayerGame)
                return;

            bool show = CanRequestMultiplayerChange() &&
                IsChoreTransportReady() &&
                main.Show_HUD_Options &&
                !main.Show_HUD_OptionsMP;
            main.OptionsGameSpeedVis = show ? Visibility.Visible : Visibility.Collapsed;
            if (show)
                RefreshOpenOptionsUi(GetCurrentSpeed());
        }

        private static int GetCurrentSpeed()
        {
            Director director = Director.instance;
            if (director == null || director.EngineFrameTime <= 0f)
                return 40;

            int observed = (int)Math.Round(1.0 / director.EngineFrameTime);
            return MultiplayerGameSpeedPolicy.NormalizeObservedSpeed(observed, GetMaximumSpeed());
        }

        private static int GetMaximumSpeed()
        {
            // Match the same live configuration used by Script Extender's Director IL hooks.
            SHCDESE.BepInEx.Bootstrap.Plugin plugin = SHCDESE.BepInEx.Bootstrap.Plugin.Instance;
            return plugin?.MaxGameSpeed != null
                ? Math.Max(MultiplayerGameSpeedPolicy.MinimumSpeed, (int)plugin.MaxGameSpeed.Value)
                : MultiplayerGameSpeedPolicy.MaximumSpeed;
        }

        private static bool TryGetLoadedMainViewModel(out MainViewModel main)
        {
            main = null;

            // MainViewModel.Instance constructs the complete Vanilla view model when it is null.
            // Settings are applied before that constructor is safe, so never touch the getter early.
            if (!MainViewModel.viewModelLoaded)
                return false;

            main = MainViewModel.Instance;
            return main != null;
        }

        private bool IsChoreTransportReady() =>
            networkInitialized && packetHook != null && ChoreNetworkTransport.IsAvailable;

        private static MethodInfo FindInstanceMethod(
            Type type,
            string name,
            Type[] parameterTypes,
            Type returnType = null)
        {
            MethodInfo method = type.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);
            if (method == null || method.ReturnType != (returnType ?? typeof(void)))
                throw new MissingMethodException(type.FullName, name);
            return method;
        }

        private void LogTransportFailureThrottled(string message)
        {
            long now = Stopwatch.GetTimestamp();
            long elapsedMilliseconds = lastTransportErrorTimestamp == 0
                ? long.MaxValue
                : (now - lastTransportErrorTimestamp) * 1000 / Stopwatch.Frequency;
            if (elapsedMilliseconds < TransportErrorLogIntervalMilliseconds)
                return;

            lastTransportErrorTimestamp = now;
            LogError(message + ".");
        }

        private void LogInfo(string message) =>
            Shared.DebugLogHelper.LogInfo(log, $"Bugfixes and QoL multiplayer game speed: {message}");

        private void LogError(string message) =>
            Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL multiplayer game speed: {message}");
    }
}
