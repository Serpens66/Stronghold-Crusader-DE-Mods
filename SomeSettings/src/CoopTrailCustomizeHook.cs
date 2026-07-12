using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;

namespace SomeSettings
{
    internal sealed class CoopTrailCustomizeHook : IDisposable
    {
        private delegate void MultiplayerButtonClickedDelegate(FRONT_Multiplayer self, string param);

        private const string CustomizeCoopTrailCommand = "SomeSettingsCustomizeCoopTrail";
        private static readonly MethodInfo MultiplayerShowSetupScreenMethod = FindMethod(typeof(FRONT_Multiplayer), "ShowSetupScreen");
        private static readonly MethodInfo MultiplayerSetupSkirmishModeSettingsMethod = FindMethod(typeof(FRONT_Multiplayer), "SetupSkirmishModeSettings");
        private static readonly MethodInfo MultiplayerUpdateSteamIdMappingsMethod = FindMethod(typeof(FRONT_Multiplayer), "updateSteamIDMappings");
        private static readonly MethodInfo MultiplayerUpdateRadarShieldPositionsMethod = FindMethod(typeof(FRONT_Multiplayer), "UpdateRadarShieldPositions");

        private readonly ManualLogSource log;
        private readonly Hook buttonClickedHook;
        private readonly MultiplayerButtonClickedDelegate buttonClickedTrampoline;
        private bool disposed;

        public CoopTrailCustomizeHook(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            buttonClickedHook = new Hook(FindMethod(typeof(FRONT_Multiplayer), "ButtonClicked", typeof(string)), (MultiplayerButtonClickedDelegate)ButtonClickedHook);
            buttonClickedTrampoline = buttonClickedHook.GenerateTrampoline<MultiplayerButtonClickedDelegate>();
            Shared.DebugLogHelper.LogDebug(log, "SomeSettings coop trail customize hook installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            buttonClickedHook?.Undo();
            buttonClickedHook?.Dispose();
            Shared.DebugLogHelper.LogDebug(log, "SomeSettings coop trail customize hook disposed.");
        }

        private void ButtonClickedHook(FRONT_Multiplayer self, string param)
        {
            if (!string.Equals(param, CustomizeCoopTrailCommand, StringComparison.Ordinal))
            {
                buttonClickedTrampoline(self, param);
                return;
            }

            try
            {
                CustomizeCurrentCoopTrail(self);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"SomeSettings failed to open coop trail customize lobby: {ex}");
            }
        }

        private void CustomizeCurrentCoopTrail(FRONT_Multiplayer self)
        {
            int currentTrail = FrontendMenus.CurrentSelectedTrail;
            int currentMission = GetCurrentCoopMission(currentTrail);
            if (currentMission <= 0)
            {
                Shared.DebugLogHelper.LogWarning(log, $"SomeSettings ignored coop trail customize command: unsupportedTrail={currentTrail}, mission={currentMission}.");
                return;
            }

            if (self.currentLobby == null || (!MainViewModel.Instance.Show_CoopHostJoinedPane && !MainViewModel.Instance.Show_CoopClientPane))
            {
                Shared.DebugLogHelper.LogWarning(log, $"SomeSettings ignored coop trail customize command outside joined coop lobby: trail={currentTrail}, mission={currentMission}.");
                return;
            }

            self.CoopMissionChanged(GetCoopTrailId(currentTrail), currentMission);
            if (self.singlePlayerCoop)
            {
                SwitchAiCoopToSinglePlayerSkirmishSetup(self, currentTrail);
                Shared.DebugLogHelper.LogDebug(log, () => $"SomeSettings switched AI coop trail to singleplayer skirmish setup: trail={currentTrail}, mission={currentMission}.");
                return;
            }

            SwitchHumanCoopToMultiplayerSetup(self, currentTrail);
            Shared.DebugLogHelper.LogDebug(log, () => $"SomeSettings switched coop trail to multiplayer setup screen: trail={currentTrail}, mission={currentMission}, isHost={self.currentLobby.isHost}.");
        }

        private static int GetCurrentCoopMission(int currentTrail)
        {
            switch (currentTrail)
            {
                case 21:
                    return FrontendMenus.CurrentSelectedTrailCoop1Mission;
                case 22:
                    return FrontendMenus.CurrentSelectedTrailCoop2Mission;
                case 23:
                    return FrontendMenus.CurrentSelectedTrailCoop3Mission;
                default:
                    return -1;
            }
        }

        private static int GetCoopTrailId(int currentTrail)
        {
            switch (currentTrail)
            {
                case 21:
                    return 0;
                case 22:
                    return 1;
                case 23:
                    return 2;
                default:
                    return -1;
            }
        }

        private static void SwitchAiCoopToSinglePlayerSkirmishSetup(FRONT_Multiplayer self, int currentTrail)
        {
            FRONT_Multiplayer.skirmishGame = true;
            FRONT_Multiplayer.coopGame = true;
            FRONT_Multiplayer.coopGame_IsHost = true;
            FRONT_Multiplayer.customCoopGame = false;
            MainViewModel.Instance.SkirmishSetupMode = true;
            MainViewModel.Instance.MultiplayerSetupMode = false;
            MainViewModel.Instance.Show_SkirmishRandomAI = true;
            MainViewModel.Instance.Show_SkirmishTeams = true;
            MainViewModel.Instance.Show_MPIsHost = true;
            MainViewModel.Instance.Show_MPSteamIdentity = false;
            MultiplayerShowSetupScreenMethod.Invoke(self, null);
            MultiplayerSetupSkirmishModeSettingsMethod.Invoke(self, null);
            MultiplayerUpdateSteamIdMappingsMethod.Invoke(self, null);
            MultiplayerUpdateRadarShieldPositionsMethod.Invoke(self, null);
            ShowSetupOverCoopTrail(currentTrail);
        }

        private static void SwitchHumanCoopToMultiplayerSetup(FRONT_Multiplayer self, int currentTrail)
        {
            MultiplayerShowSetupScreenMethod.Invoke(self, null);
            ShowSetupOverCoopTrail(currentTrail);
        }

        private static void ShowSetupOverCoopTrail(int currentTrail)
        {
            MainViewModel.Instance.Show_CoopHostInvitePane = false;
            MainViewModel.Instance.Show_CoopHostJoinedPane = false;
            MainViewModel.Instance.Show_CoopClientPane = false;
            MainViewModel.Instance.Show_CoopMapIcons = false;
            MainViewModel.Instance.Show_CoopAIAllyPanel = false;
            MainViewModel.Instance.Show_CoopOptions = false;
            MainViewModel.Instance.Show_CoopWaiting = false;
            MainViewModel.Instance.Show_MPSharing = false;
            MainViewModel.Instance.Show_MultiplayerSetup = true;

            switch (currentTrail)
            {
                case 21:
                    MainViewModel.Instance.Show_CoopTrail1 = false;
                    break;
                case 22:
                    MainViewModel.Instance.Show_CoopTrail2 = false;
                    break;
                case 23:
                    MainViewModel.Instance.Show_CoopTrail3 = false;
                    break;
            }
        }

        private static MethodInfo FindMethod(Type type, string name, params Type[] parameterTypes)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, parameterTypes, null);
            if (method == null)
                throw new MissingMethodException(type.FullName, name);

            return method;
        }
    }
}
