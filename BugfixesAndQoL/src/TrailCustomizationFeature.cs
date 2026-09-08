using BepInEx.Logging;
using CrusaderDE;
using MessagePack;
using MonoMod.RuntimeDetour;
using Noesis;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Network;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed class TrailCustomizationFeature : IDisposable
    {
        private delegate void FrontendOpenCustomTrailDelegate(FrontendMenus self, string trailName, int level);
        private delegate void FrontendButtonDelegate(FrontendMenus self, string command);
        private delegate void MultiplayerButtonDelegate(FRONT_Multiplayer self, string command);
        private delegate void StartSkirmishGameDelegate(
            FRONT_Multiplayer self,
            HUD_IngameMenu.RestartSkirmishMapInfo customTrailRestartInfo);

        private static readonly FieldInfo MpLocalReadyField = typeof(FRONT_Multiplayer).GetField(
            "MPLocalReady", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo MpLocalReadyLockedField = typeof(FRONT_Multiplayer).GetField(
            "MPLocalReadyLocked", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo ShowSetupScreenMethod = typeof(FRONT_Multiplayer).GetMethod(
            "ShowSetupScreen", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo SetupSkirmishModeSettingsMethod = typeof(FRONT_Multiplayer).GetMethod(
            "SetupSkirmishModeSettings", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo UpdateSteamIdMappingsMethod = typeof(FRONT_Multiplayer).GetMethod(
            "updateSteamIDMappings", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo UpdateRadarShieldPositionsMethod = typeof(FRONT_Multiplayer).GetMethod(
            "UpdateRadarShieldPositions", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo MpSetupDataField = typeof(FRONT_Multiplayer).GetField(
            "MPsetupData", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly List<Button> injectedButtons = new List<Button>();
        private readonly HashSet<UserControl> injectedPages = new HashSet<UserControl>();
        private Hook frontendOpenCustomTrailHook;
        private Hook frontendButtonHook;
        private Hook multiplayerButtonHook;
        private Hook startSkirmishGameHook;
        private FrontendOpenCustomTrailDelegate frontendOpenCustomTrailOriginal;
        private FrontendButtonDelegate frontendButtonOriginal;
        private MultiplayerButtonDelegate multiplayerButtonOriginal;
        private StartSkirmishGameDelegate startSkirmishGameOriginal;
        private HUD_IngameMenu.RestartSkirmishMapInfo customTrailSetupRestartInfo;
        private FileHeader customTrailSetupHeader;
        private short packetId;
        private bool initialized;
        private bool providerStateFailureLogged;

        public TrailCustomizationFeature(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Initialize()
        {
            if (initialized)
                return;

            TrailCustomizationLaunchOriginApi.Initialize(log);
            R3PacketEventHook<TrailCustomizationPacket> packetHook =
                GameNetworkAPI.Instance.GetPacketEventFor<TrailCustomizationPacket>();
            packetId = packetHook.GetPacketId();
            subscriptions.Add(packetHook.GetBaseHook().Observable.Subscribe(OnPacket));

            frontendOpenCustomTrailHook = InstallHook(
                typeof(FrontendMenus).GetMethod(
                    nameof(FrontendMenus.OpenCustomTrail),
                    new[] { typeof(string), typeof(int) }),
                (FrontendOpenCustomTrailDelegate)FrontendOpenCustomTrailHook,
                out frontendOpenCustomTrailOriginal);
            frontendButtonHook = InstallHook(
                typeof(FrontendMenus).GetMethod("ButtonClicked", new[] { typeof(string) }),
                (FrontendButtonDelegate)FrontendButtonHook,
                out frontendButtonOriginal);
            multiplayerButtonHook = InstallHook(
                typeof(FRONT_Multiplayer).GetMethod("ButtonClicked", new[] { typeof(string) }),
                (MultiplayerButtonDelegate)MultiplayerButtonHook,
                out multiplayerButtonOriginal);
            startSkirmishGameHook = InstallHook(
                typeof(FRONT_Multiplayer).GetMethod(
                    "StartSkirmishGame",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(HUD_IngameMenu.RestartSkirmishMapInfo) },
                    null),
                (StartSkirmishGameDelegate)StartSkirmishGameHook,
                out startSkirmishGameOriginal);

            TrailCustomizationProviderHostApi.Attach(RefreshVisibility);
            EnsureCoopButtons();
            initialized = true;
            Shared.DebugLogHelper.LogInfo(log, "Trail Customize button feature initialized.");
        }

        public void RefreshVisibility()
        {
            bool providerEnabled = TryIsProviderEnabled();
            bool visible = (settings.EnableMod && settings.EnableTrailCustomizationButtons) || providerEnabled;
            if (providerEnabled)
            {
                // Only one origin provider may be active. An enabled external provider owns the
                // complete transition, while this component remains the physical button owner.
                TrailCustomizationLaunchOriginApi.Clear();
                customTrailSetupRestartInfo = null;
                customTrailSetupHeader = null;
            }

            foreach (Button button in injectedButtons)
                button.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (MainViewModel.viewModelLoaded && MainViewModel.Instance != null)
                MainViewModel.Instance.Show_TrailCustomisationButtons = visible;
        }

        private bool IsEffectiveEnabled() =>
            (settings.EnableMod && settings.EnableTrailCustomizationButtons) ||
            TryIsProviderEnabled();

        private bool TryIsProviderEnabled()
        {
            try
            {
                bool result = TrailCustomizationProviderHostApi.IsProviderEnabled();
                providerStateFailureLogged = false;
                return result;
            }
            catch (Exception exception)
            {
                if (!providerStateFailureLogged)
                {
                    providerStateFailureLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        "Trail customization provider state failed; using the BugfixesAndQoL setting only: " + exception);
                }
                return false;
            }
        }

        private void FrontendOpenCustomTrailHook(FrontendMenus self, string trailName, int level)
        {
            frontendOpenCustomTrailOriginal(self, trailName, level);
            TrailCustomizationLaunchOriginApi.Clear();
            RefreshVisibility();
        }

        private void FrontendButtonHook(FrontendMenus self, string command)
        {
            if (string.Equals(command, "Customize", StringComparison.Ordinal) &&
                FrontendMenus.CurrentSelectedTrail >= 90 && FrontendMenus.CurrentSelectedTrail <= 92 &&
                IsEffectiveEnabled())
            {
                try
                {
                    if (TrailCustomizationProviderHostApi.TryCustomizeCustomTrail(out bool providerActive))
                        return;
                    if (providerActive)
                    {
                        Shared.DebugLogHelper.LogError(log, "The active Trail customization provider rejected the Custom Trail transition.");
                        return;
                    }
                    OpenSelectedCustomTrailSetup(self);
                }
                catch (Exception exception)
                {
                    customTrailSetupRestartInfo = null;
                    customTrailSetupHeader = null;
                    TrailCustomizationLaunchOriginApi.Clear();
                    Shared.DebugLogHelper.LogError(log, "Could not open Custom Trail setup: " + exception);
                }
                return;
            }

            frontendButtonOriginal(self, command);
            if (IsContextChangeCommand(command))
                TrailCustomizationLaunchOriginApi.Clear();
            if (IsCoopTrailOpenCommand(command) || string.Equals(command, "Coops", StringComparison.Ordinal))
                EnsureCoopButtons();
        }

        private void MultiplayerButtonHook(FRONT_Multiplayer self, string command)
        {
            if (IsStartCommand(command) &&
                TrailCustomizationLaunchOriginApi.Origin == TrailCustomizationLaunchOriginKind.CustomizedCoopTrail &&
                !self.singlePlayerCoop && self.currentLobby != null && self.currentLobby.isHost)
            {
                Broadcast(
                    TrailCustomizationLaunchOriginApi.TrailId,
                    TrailCustomizationLaunchOriginApi.MissionId,
                    launch: true);
            }
            multiplayerButtonOriginal(self, command);
        }

        private void StartSkirmishGameHook(
            FRONT_Multiplayer self,
            HUD_IngameMenu.RestartSkirmishMapInfo customTrailRestartInfo)
        {
            if (customTrailRestartInfo == null && customTrailSetupRestartInfo != null)
            {
                customTrailRestartInfo = customTrailSetupRestartInfo;
                customTrailRestartInfo.MPsetupData = (EngineInterface.MultiplayerSetupData)
                    (MpSetupDataField ?? throw new MissingFieldException(
                        typeof(FRONT_Multiplayer).FullName,
                        "MPsetupData")).GetValue(self);
                customTrailRestartInfo.importMembers(self.currentLobby);
                customTrailRestartInfo.importAIVs(self.AIVs);
                customTrailRestartInfo.selectedHeader = customTrailSetupHeader;
            }
            if (customTrailRestartInfo != null &&
                TrailCustomizationLaunchOriginApi.Origin != TrailCustomizationLaunchOriginKind.None)
                TrailCustomizationLaunchOriginApi.MarkRestartPending();
            startSkirmishGameOriginal(self, customTrailRestartInfo);
            customTrailSetupRestartInfo = null;
            customTrailSetupHeader = null;
        }

        private void OpenSelectedCustomTrailSetup(FrontendMenus menus)
        {
            int missionId = FrontendMenus.CurrentSelectedCustomTrailMission;
            int trailId = FrontendMenus.CurrentSelectedTrail;
            if (missionId <= 0 || trailId < 90 || trailId > 92 || string.IsNullOrWhiteSpace(menus.CustomTrailName))
                throw new InvalidDataException("The selected Custom Trail mission is invalid.");

            FileHeader header = MapFileManager.Instance.GetHeaderFromCustomTrail(
                menus.CustomTrailName,
                FRONT_ManageTrail.GetMakerFileName(missionId - 1));
            if (header == null || !header.hasRestartSkirmishInfo)
                throw new InvalidDataException("The selected Custom Trail mission has no skirmish setup data.");

            FileHeader fullHeader = MapFileManager.Instance.GetFileInfoFromFileName(
                header.filePath,
                header.filePath,
                4,
                loadRestartInfo: true);
            HUD_IngameMenu.RestartSkirmishMapInfo restartInfo = fullHeader?.restartSkirmishInfo;
            if (restartInfo?.selectedHeader == null)
                throw new InvalidDataException("The selected Custom Trail mission setup could not be decoded.");

            int difficulty = (int)typeof(FrontendMenus)
                .GetField("currentDifficultySetting", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(menus);
            restartInfo.customTrail = true;
            restartInfo.customTrailName = menus.CustomTrailName;
            restartInfo.customTrailLevel = missionId;
            restartInfo.customTrailDifficulty = difficulty;
            customTrailSetupRestartInfo = restartInfo;
            customTrailSetupHeader = header;
            TrailCustomizationLaunchOriginApi.SetCustomizedCustomTrail(trailId, missionId);

            FrontendMenus.ClearUIPanels(frontEndState: true, logo: false);
            MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
            FRONT_Multiplayer.Open(
                skirmishSetup: true,
                restartInfo: restartInfo,
                coopSetup: false,
                trailMaker: false,
                customiseTrailType: -1,
                customiseTrailID: -1);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Opened standalone Custom Trail setup [{menus.CustomTrailName}] mission {missionId}.");
        }

        private void EnsureCoopButtons()
        {
            UserControl[] pages =
            {
                FRONT_CoopTrail1.Instance,
                FRONT_CoopTrail2.Instance,
                FRONT_CoopTrail3.Instance,
                FRONT_CoopTrail4.Instance,
            };
            foreach (UserControl page in pages)
                InjectCoopButton(page);
            RefreshVisibility();
        }

        private void InjectCoopButton(UserControl page)
        {
            if (page == null || injectedPages.Contains(page))
                return;
            Button anchor = page.FindName("CoopKick") as Button;
            Grid host = anchor == null ? null : VisualTreeHelper.GetParent(anchor) as Grid;
            if (anchor == null || host == null)
                return;
            foreach (UIElement child in host.Children)
            {
                if (child is Button existing && string.Equals(existing.Name, "SharedTrailCustomize", StringComparison.Ordinal))
                {
                    injectedPages.Add(page);
                    return;
                }
            }

            var button = new Button
            {
                Name = "SharedTrailCustomize",
                Width = 200,
                Margin = new Thickness(0, 0, 0, -30),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Style = anchor.Style,
            };
            PropEx.SetTextCentre(
                button,
                Translate.Instance.GameTexts.TryGetValue("TEXT_CUSTOMISATION_071", out string text)
                    ? text
                    : "Customize");
            PropEx.SetTextLeft(button, string.Empty);
            PropEx.SetTextRight(button, string.Empty);
            PropEx.SetGlowButtonTextHeight(button, 28);
            button.Click += OnCoopCustomizeClicked;
            host.Children.Add(button);
            injectedButtons.Add(button);
            injectedPages.Add(page);
        }

        private void OnCoopCustomizeClicked(object sender, RoutedEventArgs args)
        {
            if (!IsEffectiveEnabled())
                return;
            try
            {
                if (TrailCustomizationProviderHostApi.TryCustomizeCoopTrail(out bool providerActive))
                    return;
                if (providerActive)
                {
                    Shared.DebugLogHelper.LogError(log, "The active Trail customization provider rejected the Coop Trail transition.");
                    return;
                }
                CustomizeCurrentCoopTrail();
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(log, "Could not open Coop Trail setup: " + exception);
            }
        }

        private void CustomizeCurrentCoopTrail()
        {
            FRONT_Multiplayer self = MainViewModel.Instance.FRONTMultiplayer;
            int currentTrail = FrontendMenus.CurrentSelectedTrail;
            int mission = currentTrail == 21 ? FrontendMenus.CurrentSelectedTrailCoop1Mission :
                currentTrail == 22 ? FrontendMenus.CurrentSelectedTrailCoop2Mission :
                currentTrail == 23 ? FrontendMenus.CurrentSelectedTrailCoop3Mission :
                currentTrail == 24 ? FrontendMenus.CurrentSelectedTrailCoop4Mission : -1;
            int trailId = currentTrail - 21;
            if (mission <= 0 || trailId < 0 || trailId > 3 || self.currentLobby == null)
                return;
            if (!self.singlePlayerCoop && !self.currentLobby.isHost)
            {
                Shared.DebugLogHelper.LogWarning(log, "Ignored Coop Trail Customize click from a non-host client.");
                return;
            }
            OpenCoopTrailSetup(self, trailId, mission, !self.singlePlayerCoop, "local host");
        }

        private void OpenCoopTrailSetup(
            FRONT_Multiplayer self,
            int trailId,
            int missionId,
            bool notifyClients,
            string source)
        {
            if (self?.currentLobby == null || trailId < 0 || trailId > 3 || missionId < 1 || missionId > 10)
                throw new InvalidDataException("The Coop Trail setup transition is invalid.");

            SetSelectedCoopMission(trailId, missionId);
            TrailCustomizationLaunchOriginApi.SetCustomizedCoopTrail(trailId, missionId);
            try
            {
                self.CoopMissionChanged(trailId, missionId);
                if (notifyClients)
                    Broadcast(trailId, missionId, launch: false);

                if (self.singlePlayerCoop)
                {
                    FRONT_Multiplayer.skirmishGame = true;
                    FRONT_Multiplayer.coopGame = true;
                    FRONT_Multiplayer.coopGame_IsHost = true;
                    FRONT_Multiplayer.customCoopGame = false;
                    (MpLocalReadyField ?? throw new MissingFieldException(typeof(FRONT_Multiplayer).FullName, "MPLocalReady"))
                        .SetValue(self, false);
                    (MpLocalReadyLockedField ?? throw new MissingFieldException(typeof(FRONT_Multiplayer).FullName, "MPLocalReadyLocked"))
                        .SetValue(self, false);
                    MainViewModel.Instance.SkirmishSetupMode = true;
                    MainViewModel.Instance.MultiplayerSetupMode = false;
                    MainViewModel.Instance.Show_SkirmishRandomAI = true;
                    MainViewModel.Instance.Show_SkirmishTeams = true;
                    MainViewModel.Instance.Show_MPIsHost = true;
                    MainViewModel.Instance.Show_MPSteamIdentity = false;
                    RequireMethod(ShowSetupScreenMethod, "ShowSetupScreen").Invoke(self, null);
                    RequireMethod(SetupSkirmishModeSettingsMethod, "SetupSkirmishModeSettings").Invoke(self, null);
                    RequireMethod(UpdateSteamIdMappingsMethod, "updateSteamIDMappings").Invoke(self, null);
                    RequireMethod(UpdateRadarShieldPositionsMethod, "UpdateRadarShieldPositions").Invoke(self, null);
                }
                else
                {
                    RequireMethod(ShowSetupScreenMethod, "ShowSetupScreen").Invoke(self, null);
                }
            }
            catch
            {
                TrailCustomizationLaunchOriginApi.Clear();
                throw;
            }

            MainViewModel.Instance.Show_CoopHostInvitePane = false;
            MainViewModel.Instance.Show_CoopHostJoinedPane = false;
            MainViewModel.Instance.Show_CoopClientPane = false;
            MainViewModel.Instance.Show_CoopMapIcons = false;
            MainViewModel.Instance.Show_CoopAIAllyPanel = false;
            MainViewModel.Instance.Show_CoopOptions = false;
            MainViewModel.Instance.Show_CoopWaiting = false;
            MainViewModel.Instance.Show_MPSharing = false;
            MainViewModel.Instance.Show_MultiplayerSetup = true;
            if (trailId == 0) MainViewModel.Instance.Show_CoopTrail1 = false;
            if (trailId == 1) MainViewModel.Instance.Show_CoopTrail2 = false;
            if (trailId == 2) MainViewModel.Instance.Show_CoopTrail3 = false;
            if (trailId == 3) MainViewModel.Instance.Show_CoopTrail4 = false;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Opened standalone Coop Trail setup trail={trailId + 1}, mission={missionId}, source={source}.");
        }

        private void Broadcast(int trailId, int missionId, bool launch)
        {
            var packet = new TrailCustomizationPacket
            {
                ProtocolVersion = TrailCustomizationPacket.CurrentProtocolVersion,
                TrailId = trailId,
                MissionId = missionId,
                Launch = launch,
            };
            byte[] bytes = MessagePackSerializer.Serialize(packet);
            GameNetworkAPI.SendPacketToAllLobby(new Platform_Multiplayer.MPData
            {
                packetType = packetId,
                data = bytes,
                dataLength = bytes.Length,
                dataOffset = 0,
            });
        }

        private void OnPacket(ReceiveCustomPacketEventArgs<TrailCustomizationPacket> args)
        {
            try
            {
                CSteamID? host = GameNetworkAPI.GetHostSteamId();
                if (!args.SenderSteamId.HasValue || !host.HasValue || args.SenderSteamId.Value != host.Value)
                {
                    Shared.DebugLogHelper.LogError(log, "Rejected Trail customization packet from a sender that is not the lobby host.");
                    return;
                }
                TrailCustomizationPacket packet = args.Packet;
                if (packet == null || packet.ProtocolVersion != TrailCustomizationPacket.CurrentProtocolVersion ||
                    packet.TrailId < 0 || packet.TrailId > 3 || packet.MissionId < 1 || packet.MissionId > 10)
                {
                    Shared.DebugLogHelper.LogError(log, "Rejected invalid Trail customization packet.");
                    return;
                }
                FRONT_Multiplayer self = MainViewModel.Instance?.FRONTMultiplayer;
                if (!IsEffectiveEnabled() || self?.currentLobby == null || self.currentLobby.isHost ||
                    self.singlePlayerCoop || !self.currentLobby.coopTrailGame)
                {
                    Shared.DebugLogHelper.LogWarning(log, "Ignored Trail customization packet outside an active client Coop lobby.");
                    return;
                }
                if (packet.Launch)
                {
                    TrailCustomizationLaunchOriginApi.SetCustomizedCoopTrail(packet.TrailId, packet.MissionId);
                    return;
                }
                OpenCoopTrailSetup(self, packet.TrailId, packet.MissionId, false, "authenticated host packet");
            }
            catch (Exception exception)
            {
                TrailCustomizationLaunchOriginApi.Clear();
                Shared.DebugLogHelper.LogError(log, "Could not apply Trail customization packet: " + exception);
            }
        }

        private static void SetSelectedCoopMission(int trailId, int missionId)
        {
            FrontendMenus.CurrentSelectedTrail = trailId + 21;
            if (trailId == 0) FrontendMenus.CurrentSelectedTrailCoop1Mission = missionId;
            if (trailId == 1) FrontendMenus.CurrentSelectedTrailCoop2Mission = missionId;
            if (trailId == 2) FrontendMenus.CurrentSelectedTrailCoop3Mission = missionId;
            if (trailId == 3) FrontendMenus.CurrentSelectedTrailCoop4Mission = missionId;
        }

        private static bool IsCoopTrailOpenCommand(string command) =>
            string.Equals(command, "Coop", StringComparison.Ordinal) ||
            string.Equals(command, "Coop2", StringComparison.Ordinal) ||
            string.Equals(command, "Coop3", StringComparison.Ordinal) ||
            string.Equals(command, "Coop4", StringComparison.Ordinal);

        private static bool IsStartCommand(string command) =>
            string.Equals(command, "Play", StringComparison.Ordinal) ||
            string.Equals(command, "COOP_START", StringComparison.Ordinal);

        private static bool IsContextChangeCommand(string command) =>
            string.Equals(command, "Skirmish", StringComparison.Ordinal) ||
            string.Equals(command, "MapEditor", StringComparison.Ordinal) ||
            string.Equals(command, "BackMain", StringComparison.Ordinal) ||
            string.Equals(command, "Coops", StringComparison.Ordinal) ||
            IsCoopTrailOpenCommand(command) ||
            string.Equals(command, "Trail", StringComparison.Ordinal) ||
            string.Equals(command, "Trail2", StringComparison.Ordinal) ||
            string.Equals(command, "Trail3", StringComparison.Ordinal) ||
            (command != null && command.StartsWith("Sands", StringComparison.Ordinal));

        private static MethodInfo RequireMethod(MethodInfo method, string name) =>
            method ?? throw new MissingMethodException(typeof(FRONT_Multiplayer).FullName, name);

        private static Hook InstallHook<TDelegate>(MethodBase target, TDelegate detour, out TDelegate original)
            where TDelegate : Delegate
        {
            if (target == null)
                throw new MissingMethodException("A required Trail customization method is unavailable.");
            var hook = new Hook(target, detour);
            original = hook.GenerateTrampoline<TDelegate>();
            return hook;
        }

        public void Dispose()
        {
            TrailCustomizationProviderHostApi.Detach(RefreshVisibility);
            foreach (Button button in injectedButtons)
            {
                button.Click -= OnCoopCustomizeClicked;
                button.Visibility = Visibility.Collapsed;
            }
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            frontendOpenCustomTrailHook?.Dispose();
            frontendButtonHook?.Dispose();
            multiplayerButtonHook?.Dispose();
            startSkirmishGameHook?.Dispose();
        }
    }
}
