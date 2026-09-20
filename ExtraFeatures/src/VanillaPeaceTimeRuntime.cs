using APIShared;
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using R3;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Reflection;

namespace ExtraFeatures
{
    internal sealed class VanillaPeaceTimeRuntime
    {
        private static readonly FieldInfo TemporarySetupDataField =
            typeof(FRONT_Multiplayer).GetField(
                "MPTEMPsetupData",
                BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new MissingFieldException(typeof(FRONT_Multiplayer).FullName, "MPTEMPsetupData");

        private delegate void ImportSettingsDelegate(
            FRONT_Multiplayer self,
            string serializedSettings,
            bool isHost);

        private delegate void PeaceTimeSliderChangedDelegate(
            FRONT_Multiplayer self,
            object sender,
            RoutedPropertyChangedEventArgs<float> args);

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;

        // These hooks and subscriptions intentionally remain rooted for the process lifetime.
        private Hook importSettingsHook;
        private Hook peaceTimeSliderHook;
        private ImportSettingsDelegate importSettingsOriginal;
        private PeaceTimeSliderChangedDelegate peaceTimeSliderOriginal;
        private ulong? lobbyId;
        private bool initializedForLobby;
        private bool synchronizingLobby;
        private bool missionOverridePending;
        private long overriddenSessionId;
        private int previousNativeMinutes;
        private bool initialized;

        internal VanillaPeaceTimeRuntime(ManualLogSource log, ExtraFeaturesViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        internal void Initialize()
        {
            if (initialized)
                return;

            InstallLobbyHooks();
            RegisterLobbyObserver();
            Shared.MissionEvents.Initialization.Subscribe(OnMissionInitialization);
            Shared.MissionEvents.Ended.Subscribe(_ => RestoreNativeOption("mission end"));
            settings.SettingChanged += OnSettingChanged;
            initialized = true;
            Shared.DebugLogHelper.LogDebug(log, "Vanilla peace-time runtime initialized.");
        }

        private void InstallLobbyHooks()
        {
            Hook newImportHook = null;
            Hook newSliderHook = null;
            try
            {
                MethodInfo importMethod = RequireMethod(
                    "ImportSettings",
                    typeof(string),
                    typeof(bool));
                newImportHook = new Hook(importMethod, (ImportSettingsDelegate)ImportSettingsHook);
                ImportSettingsDelegate newImportOriginal =
                    newImportHook.GenerateTrampoline<ImportSettingsDelegate>();

                MethodInfo sliderMethod = RequireMethod(
                    nameof(FRONT_Multiplayer.MP_Settings_Peacetime_Slider_ValueChanged),
                    typeof(object),
                    typeof(RoutedPropertyChangedEventArgs<float>));
                newSliderHook = new Hook(sliderMethod, (PeaceTimeSliderChangedDelegate)PeaceTimeSliderChangedHook);
                PeaceTimeSliderChangedDelegate newSliderOriginal =
                    newSliderHook.GenerateTrampoline<PeaceTimeSliderChangedDelegate>();

                importSettingsOriginal = newImportOriginal;
                peaceTimeSliderOriginal = newSliderOriginal;
                importSettingsHook = newImportHook;
                peaceTimeSliderHook = newSliderHook;
            }
            catch
            {
                newSliderHook?.Dispose();
                newImportHook?.Dispose();
                throw;
            }
        }

        private void RegisterLobbyObserver()
        {
            if (!ApiShared.Current.TryGetLobbyState(
                    ExtraFeaturesPlugin.PluginGuid,
                    out ILobbyStateCapability capability,
                    out NativeCapabilityDiagnostic diagnostic))
            {
                throw new InvalidOperationException(
                    "Lobby-state capability unavailable: " + diagnostic?.Reason);
            }

            if (!capability.TryRegisterObserver(
                    "vanilla-peace-time",
                    OnLobbyStateChanged,
                    out diagnostic))
            {
                throw new InvalidOperationException(
                    "Lobby-state observer registration failed: " + diagnostic?.Reason);
            }
        }

        private void OnLobbyStateChanged(LobbyStateSnapshot snapshot)
        {
            try
            {
                if (snapshot == null || !string.IsNullOrEmpty(snapshot.Error))
                    return;

                if (!snapshot.LobbyId.HasValue)
                {
                    if (!snapshot.PreserveForMapTransition)
                    {
                        lobbyId = null;
                        initializedForLobby = false;
                    }
                    return;
                }

                if (lobbyId != snapshot.LobbyId)
                {
                    lobbyId = snapshot.LobbyId;
                    initializedForLobby = false;
                }

                FRONT_Multiplayer front = MainViewModel.Instance?.FRONTMultiplayer;
                if (front != null && IsRealLobbyHost(front) && settings.EnableMod &&
                    TryPushSettingToVanilla(front))
                {
                    initializedForLobby = true;
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "Vanilla peace-time lobby observation failed: " + ex);
            }
        }

        private void ImportSettingsHook(
            FRONT_Multiplayer self,
            string serializedSettings,
            bool isHost)
        {
            importSettingsOriginal(self, serializedSettings, isHost);
            try
            {
                VanillaPeaceTimeLobbyDirection direction =
                    VanillaPeaceTimePolicy.ResolveLobbyDirection(
                        lobbyId.HasValue,
                        IsRealLobbyHost(self),
                        settings.EnableMod,
                        initializedForLobby);
                if (direction == VanillaPeaceTimeLobbyDirection.ModSettingToVanilla)
                {
                    if (TryPushSettingToVanilla(self))
                        initializedForLobby = true;
                }
                else if (direction == VanillaPeaceTimeLobbyDirection.VanillaToModSetting)
                {
                    PullVanillaIntoSetting(self);
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "Vanilla peace-time settings import synchronization failed: " + ex);
            }
        }

        private void PeaceTimeSliderChangedHook(
            FRONT_Multiplayer self,
            object sender,
            RoutedPropertyChangedEventArgs<float> args)
        {
            peaceTimeSliderOriginal(self, sender, args);
            try
            {
                if (!synchronizingLobby && initializedForLobby && settings.EnableMod &&
                    IsRealLobbyHost(self))
                {
                    PullVanillaIntoSetting(self);
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "Vanilla peace-time slider synchronization failed: " + ex);
            }
        }

        private void OnSettingChanged(string propertyName)
        {
            if (propertyName != nameof(ExtraFeaturesViewModel.VanillaPeaceTimeMinutes) &&
                propertyName != nameof(ExtraFeaturesViewModel.EnableMod))
            {
                return;
            }

            if (synchronizingLobby)
                return;

            try
            {
                FRONT_Multiplayer front = MainViewModel.Instance?.FRONTMultiplayer;
                if (lobbyId.HasValue && settings.EnableMod && IsRealLobbyHost(front) &&
                    TryPushSettingToVanilla(front))
                {
                    initializedForLobby = true;
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "Vanilla peace-time mod-setting synchronization failed: " + ex);
            }
        }

        private bool TryPushSettingToVanilla(FRONT_Multiplayer front)
        {
            EngineInterface.MultiplayerSetupData setupData = GetTemporarySetupData(front);
            if (setupData == null)
                return false;

            int minutes = VanillaPeaceTimePolicy.NormalizeMinutes(settings.VanillaPeaceTimeMinutes);
            synchronizingLobby = true;
            try
            {
                setupData.peacetime = minutes;
                Slider slider = FRONT_Multiplayer_Setup.Instance?.RefMP_Settings_Peacetime_Slider;
                if (slider != null)
                    slider.Value = minutes;
                UpdateVanillaLobbyText(minutes);
            }
            finally
            {
                synchronizingLobby = false;
            }
            return true;
        }

        private void PullVanillaIntoSetting(FRONT_Multiplayer front)
        {
            EngineInterface.MultiplayerSetupData setupData = GetTemporarySetupData(front);
            if (synchronizingLobby || setupData == null)
                return;

            int minutes = VanillaPeaceTimePolicy.NormalizeMinutes(setupData.peacetime);
            synchronizingLobby = true;
            try
            {
                settings.VanillaPeaceTimeMinutes = minutes;
            }
            finally
            {
                synchronizingLobby = false;
            }
        }

        private static void UpdateVanillaLobbyText(int minutes)
        {
            if (MainViewModel.Instance == null || Translate.Instance == null)
                return;

            string suffix = Translate.Instance.lookUpText(
                Enums.eTextSections.TEXT_NEW_TEXT2,
                168);
            MainViewModel.Instance.MP_Settings_Peacetime =
                FatControler.ukrainian
                    ? minutes + " " + suffix
                    : minutes + suffix;
        }

        private static bool IsRealLobbyHost(FRONT_Multiplayer front)
        {
            if (front == null || FRONT_Multiplayer.skirmishGame)
                return false;

            var lobby = front.currentLobby ?? Platform_Multiplayer.Instance?.activeLobby;
            return lobby?.isHost == true;
        }

        private void OnMissionInitialization(MissionLifecycleNotification notification)
        {
            try
            {
                if (notification?.Phase == MissionInitializationPhase.BeforeNativeStart)
                {
                    RestoreNativeOption("replacement start");
                    if (!VanillaPeaceTimePolicy.ShouldOverrideMission(
                            notification.Context,
                            settings.EnableMod,
                            VanillaPeaceTimePolicy.IsMissionModeAllowed(notification.Context.Mode)))
                    {
                        return;
                    }

                    ChoreManagerOptionsInternal options =
                        GamePlayerManagerAPI.Instance._choreManagerOptionsInternal;
                    if (!options.IsValid())
                    {
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            "Vanilla peace time was not applied because ChoreManager options are unavailable.");
                        return;
                    }

                    previousNativeMinutes = options.Peacetime;
                    overriddenSessionId = notification.Context.SessionId;
                    missionOverridePending = true;
                    options.Peacetime = VanillaPeaceTimePolicy.NormalizeMinutes(
                        settings.VanillaPeaceTimeMinutes);
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        $"Vanilla peace time prepared for session {overriddenSessionId}: " +
                        $"minutes={options.Peacetime}, previous={previousNativeMinutes}.");
                }
                else if (notification?.Phase == MissionInitializationPhase.AfterNativeStart &&
                         missionOverridePending &&
                         notification.Context?.SessionId == overriddenSessionId)
                {
                    RestoreNativeOption("native start completed");
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "Vanilla peace-time mission initialization failed: " + ex);
                RestoreNativeOption("initialization failure");
            }
        }

        private void RestoreNativeOption(string reason)
        {
            if (!missionOverridePending)
                return;

            try
            {
                ChoreManagerOptionsInternal options =
                    GamePlayerManagerAPI.Instance._choreManagerOptionsInternal;
                if (options.IsValid())
                    options.Peacetime = previousNativeMinutes;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "Vanilla peace-time option restoration failed: " + ex);
            }
            finally
            {
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Vanilla peace-time option restored after {reason}: previous={previousNativeMinutes}.");
                missionOverridePending = false;
                overriddenSessionId = 0;
                previousNativeMinutes = 0;
            }
        }

        private static MethodInfo RequireMethod(string name, params Type[] parameterTypes) =>
            typeof(FRONT_Multiplayer).GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null) ?? throw new MissingMethodException(typeof(FRONT_Multiplayer).FullName, name);

        private static EngineInterface.MultiplayerSetupData GetTemporarySetupData(
            FRONT_Multiplayer front) =>
            front == null
                ? null
                : TemporarySetupDataField.GetValue(front) as EngineInterface.MultiplayerSetupData;
    }
}
