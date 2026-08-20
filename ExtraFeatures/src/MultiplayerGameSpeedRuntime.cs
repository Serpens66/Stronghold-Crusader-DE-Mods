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

namespace ExtraFeatures
{
    internal sealed class MultiplayerGameSpeedRuntime : IDisposable
    {
        private delegate void KeyManagerUpdateDelegate(KeyManager self);
        private delegate void OptionsInitDelegate(HUD_Options self);
        private delegate void GameSpeedSliderChangedDelegate(
            HUD_Options self,
            object sender,
            RoutedPropertyChangedEventArgs<float> args);

        private const long TransportErrorLogIntervalMilliseconds = 5000;

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly MultiplayerFeatureGate multiplayerFeatureGate;

        private Hook keyManagerUpdateHook;
        private Hook optionsInitHook;
        private Hook sliderChangedHook;
        private KeyManagerUpdateDelegate keyManagerUpdateTrampoline;
        private OptionsInitDelegate optionsInitTrampoline;
        private GameSpeedSliderChangedDelegate sliderChangedTrampoline;
        private R3PacketEventHook<MultiplayerGameSpeedChangePacket> packetHook;
        private IDisposable packetSubscription;
        private long lastTransportErrorTimestamp;
        private int lastSliderBucket = -1;
        private bool suppressSliderEvent;
        private bool networkInitialized;
        private bool hooksInstalled;
        private bool disposed;

        public MultiplayerGameSpeedRuntime(
            ManualLogSource log,
            ExtraFeaturesViewModel settings,
            MultiplayerFeatureGate multiplayerFeatureGate)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.multiplayerFeatureGate = multiplayerFeatureGate ?? throw new ArgumentNullException(nameof(multiplayerFeatureGate));
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
            Hook installedInitHook = null;
            Hook installedSliderHook = null;
            try
            {
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
                throw;
            }
        }

        public void ApplySetting()
        {
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
            packetSubscription?.Dispose();
            hooksInstalled = false;
            networkInitialized = false;
        }

        private void KeyManagerUpdateHook(KeyManager self)
        {
            keyManagerUpdateTrampoline(self);
            if (!CanRequestMultiplayerChange() || self == null)
                return;

            if (self.IsActionPressed(Enums.KeyFunctions.IncreaseEngineSpeed))
                TryQueueChange(MultiplayerGameSpeedPolicy.IncreaseAction, 0, "keybind-increase");
            if (self.IsActionPressed(Enums.KeyFunctions.DecreaseEngineSpeed))
                TryQueueChange(MultiplayerGameSpeedPolicy.DecreaseAction, 0, "keybind-decrease");
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
                (int)Math.Round(slider.Value * MultiplayerGameSpeedPolicy.SpeedStep));
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
                slider.Maximum = MultiplayerGameSpeedPolicy.MaximumSpeed / MultiplayerGameSpeedPolicy.SpeedStep;
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
            return MultiplayerGameSpeedPolicy.NormalizeObservedSpeed(observed);
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

        private static MethodInfo FindInstanceMethod(Type type, string name, Type[] parameterTypes)
        {
            MethodInfo method = type.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);
            if (method == null || method.ReturnType != typeof(void))
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
            Shared.DebugLogHelper.LogInfo(log, $"Extra Features multiplayer game speed: {message}");

        private void LogError(string message) =>
            Shared.DebugLogHelper.LogError(log, $"Extra Features multiplayer game speed: {message}");
    }
}
