using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using APIShared.Maps;
using APIShared.Lobby;
using APIShared.ModSettings;
using CrusaderDE;

namespace ExtendedData
{
    internal sealed partial class ExtendedDataRuntime
    {
        private static readonly FieldInfo MapCheckedField = RequirePrivateLobbyFlag("MPMapChecked");
        private static readonly FieldInfo MapValidField = RequirePrivateLobbyFlag("MPMapValid");
        private readonly HashSet<INotifyPropertyChanged> configurationObservers = new HashSet<INotifyPropertyChanged>();
        private long readinessInputRevision;
        private long lastAttemptRevision;
        private string publishedMapIdentity = string.Empty;
        private string mapLookupFailure = string.Empty;
        private string lastAttemptFailure = string.Empty;
        private string lastAttemptScope = string.Empty;
        private bool sharedMapLookupRegistered;
        private bool readinessInMission;
        private bool launchReadinessActive;

        private void InitializeLaunchReadiness()
        {
            launchReadinessActive = true;
            string diagnostic;
            sharedMapLookupRegistered = MultiplayerMapLookup.IsAvailable &&
                MultiplayerMapLookup.TryRegisterProvider(ExtendedDataPlugin.PluginGuid, "Coop.PackageMap",
                    ProvidePackageMap, out diagnostic);
            if (!sharedMapLookupRegistered)
                LogError("APIShared multiplayer map lookup is unavailable; packaged Coop maps remain blocked.");
            if (!MultiplayerMapLookup.TryRegisterObserver(ExtendedDataPlugin.PluginGuid, "Coop.MapResult",
                ObservePackageMapLookup, out diagnostic)) LogError("Could not observe map lookup: " + diagnostic);
            // APIShared announces, deduplicates and logs; this provider only reports current reasons.
            if (!LobbyLaunchStatus.TryRegisterProvider(ExtendedDataPlugin.PluginGuid, "Coop.LaunchStatus",
                ProvideLaunchStatus, out diagnostic))
                LogError("Could not register the lobby launch status provider: " + diagnostic);
            settings.PropertyChanged += ReadinessInputChanged;
            settings.CoopPackageRemoteStatusChanged += ReadinessInputChanged;
            settings.LordDataRemoteStatusChanged += ReadinessInputChanged;
            settings.LordDataLobbyChanged += ReadinessLobbyChanged;
            ModSettingsApplication.PreparationChanged += ReadinessInputChanged;
            missionSettingsCoordinator.LaunchBlocked = BlockLaunch;
            WatchConfigurationReadiness();
            QueueLaunchReadiness();
        }

        private void StopLaunchReadiness()
        {
            launchReadinessActive = false;
            settings.PropertyChanged -= ReadinessInputChanged;
            settings.CoopPackageRemoteStatusChanged -= ReadinessInputChanged;
            settings.LordDataRemoteStatusChanged -= ReadinessInputChanged;
            settings.LordDataLobbyChanged -= ReadinessLobbyChanged;
            ModSettingsApplication.PreparationChanged -= ReadinessInputChanged;
            foreach (var endpoint in configurationObservers) endpoint.PropertyChanged -= ReadinessInputChanged;
            configurationObservers.Clear();
            if (missionSettingsCoordinator != null) missionSettingsCoordinator.LaunchBlocked = null;
        }

        private void ReadinessLobbyChanged(PerPlayerLobbySnapshot snapshot)
        {
            ReadinessInputChanged();
            // A joining partner makes the host lobby eligible for the Trail Lord publication.
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
            {
                try
                {
                    FRONT_Multiplayer self = GetExistingMainViewModel()?.FRONTMultiplayer;
                    if (self?.currentLobby?.isHost != true || selected?.Loaded.LordRequirements == null ||
                        !CurrentSlotRequiresPackage(self)) return;
                    // Inside Vanilla's mission switch the slots still hold the previous mission's Lords.
                    if (missionChangeDepth > 0)
                    {
                        hostLordRefreshDeferred = true;
                        return;
                    }
                    RefreshSelectedLordRequirementStatus(self);
                    PrepareHostTrailLordData(self);
                }
                catch (Exception exception) { LogError("Could not publish the Trail Lord data after a lobby change: " + exception); }
            });
        }

        private void WatchConfigurationReadiness()
        {
            foreach (var endpoint in ModSettingsApplication.Endpoints.Values)
                if (configurationObservers.Add(endpoint)) endpoint.PropertyChanged += ReadinessInputChanged;
        }
        private void ReadinessInputChanged(object sender, PropertyChangedEventArgs args) => ReadinessInputChanged();
        private void ReadinessInputChanged()
        {
            // Notifications may originate on a transport thread; all reads and mutation happen after dispatch.
            Interlocked.Increment(ref readinessInputRevision);
            QueueLaunchReadiness();
        }
        // APIShared coalesces requests and calls ProvideLaunchStatus on the Unity thread.
        private void QueueLaunchReadiness()
        {
            if (launchReadinessActive) LobbyLaunchStatus.RequestRefresh();
        }

        private bool HasCurrentPackageMap(FRONT_Multiplayer self) =>
            activation.CoopPackages && selected != null && activePackage != null &&
            self?.currentLobby?.coopTrailGame == true && CurrentSlotRequiresPackage(self) &&
            selected.Loaded.TrailNumber == self.currentLobby.coopTrailID + 1 &&
            selected.Loaded.MissionNumber == self.currentLobby.coopSelectedMission &&
            string.Equals(activePackage.Manifest.PackageId, settings.ActiveCoopPackageId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(activePackage.Manifest.ContentFingerprint, settings.ActiveCoopPackageFingerprint,
                StringComparison.OrdinalIgnoreCase);

        private FileHeader ProvidePackageMap(MultiplayerMapLookupRequest request)
        {
            FRONT_Multiplayer self = GetExistingMainViewModel()?.FRONTMultiplayer;
            if (readinessInMission || !HasCurrentPackageMap(self) || selected.Loaded.Definition.Map.Source != "bundled" ||
                !IsCurrentSteamLobby(self) || !string.Equals(request.FileName, self.currentLobby.mapFileName,
                    StringComparison.OrdinalIgnoreCase)) return null;
            FileHeader header = selected.Header;
            return string.Equals(request.FileName, header.fileName, StringComparison.OrdinalIgnoreCase) &&
                header.crc == request.Crc ? header : null;
        }

        private void ObservePackageMapLookup(MultiplayerMapLookupResult result)
        {
            FRONT_Multiplayer self = GetExistingMainViewModel()?.FRONTMultiplayer;
            if (readinessInMission || !HasCurrentPackageMap(self) || !IsCurrentSteamLobby(self) ||
                !string.Equals(result.Request.FileName, self.currentLobby.mapFileName, StringComparison.OrdinalIgnoreCase)) return;
            // Not a verification result yet: the host's CRC is still unknown, or the local selection still follows a
            // host package switch (client log 2026-10-26 20:43:35: map=Close Encounters, hostCRC=0, localCRC=0).
            // The lookup after the selection update decides.
            if (result.Request.Crc == 0 ||
                !string.Equals(result.Request.FileName, selected.Header.fileName, StringComparison.OrdinalIgnoreCase)) return;
            bool matchesPackage = result.Header != null &&
                string.Equals(result.Header.filePath, selected.Header.filePath, StringComparison.OrdinalIgnoreCase) &&
                result.Header.crc == selected.Header.crc;
            string next = matchesPackage ? string.Empty :
                SerpLocalization.Get(result.Status == MultiplayerMapLookupStatus.Conflict
                    ? "ExtendedData.MapProvidersConflict" : "ExtendedData.MapNotVerified") +
                " [map=" + result.Request.FileName + ", hostCRC=" + result.Request.Crc +
                ", localCRC=" + selected.Header.crc + "]";
            if (next == mapLookupFailure) return;
            mapLookupFailure = next;
            if (next.Length != 0) CoopLaunchBlockDiagnostics.ReportLocal(log, "MapCheck", next,
                DescribeLaunchBlockContext(self), result.Diagnostic);
            ReadinessInputChanged();
        }

        private void PublishPackageMap(FRONT_Multiplayer self)
        {
            string identity = HasCurrentPackageMap(self) ? LaunchScope(self) + "|" + selected.Header.filePath +
                "|" + selected.Header.fileName + "|" + selected.Header.crc : string.Empty;
            if (identity != publishedMapIdentity)
            {
                publishedMapIdentity = identity;
                mapLookupFailure = string.Empty;
                // Vanilla alone sets MPMapValid after the fresh lookup. Never force acceptance.
                if (self != null) MapCheckedField.SetValue(self, false);
            }
            QueueLaunchReadiness();
        }

        // Current reasons for the active lobby, called by APIShared on the Unity thread.
        // Loading: expected while a player joins and applies the host selection (see the 2026-10-26 lobby logs);
        // APIShared escalates it to a block if it persists. Clients leave their own loading states to the host,
        // which reports them per player. Waiting: the Coop lobby still lacks its second human player.
        // Lobby scope: local client problems the host cannot detect; APIShared sends them to everyone.
        private IEnumerable<LobbyLaunchStatusItem> ProvideLaunchStatus(LobbyLaunchStatusQuery query)
        {
            var items = new List<LobbyLaunchStatusItem>();
            FRONT_Multiplayer self = query.Lobby;
            if (!launchReadinessActive || readinessInMission || !IsCurrentSteamLobby(self)) return items;
            if (lastAttemptRevision != Interlocked.Read(ref readinessInputRevision)) lastAttemptFailure = string.Empty;
            WatchConfigurationReadiness();
            bool host = self.currentLobby.isHost;
            int humans = self.currentLobby.CountHumanPlayers();
            string context = DescribeLaunchBlockContext(self);
            void Add(string text, LobbyLaunchStatusKind kind, string detail = null,
                LobbyLaunchStatusScope scope = LobbyLaunchStatusScope.Local)
            {
                // Without a partner nobody can start yet; the host's own short loading states would only be noise.
                if (string.IsNullOrWhiteSpace(text) || kind == LobbyLaunchStatusKind.Loading && (!host || humans < 2)) return;
                items.Add(new LobbyLaunchStatusItem(text, kind, string.IsNullOrEmpty(detail) ? context : context + "; " + detail, scope));
            }

            if (ModSettingsApplication.HasActivationFailures)
                Add(SerpLocalization.Get("ExtendedData.SettingsProviderFailed"), LobbyLaunchStatusKind.Blocking,
                    scope: LobbyLaunchStatusScope.Lobby);
            foreach (var endpoint in ModSettingsApplication.Endpoints)
                if (endpoint.Value.System_ConfigurationNeedsRestart())
                    Add(SerpLocalization.Get("ExtendedData.SettingsRestartRequired") + " [" + endpoint.Key + "]", LobbyLaunchStatusKind.Blocking,
                        scope: LobbyLaunchStatusScope.Lobby);
            // Mission 0 is the empty selection before Vanilla or the host sync chooses a mission, not a slot after the package end.
            if (activation.CoopPackages && self.currentLobby.coopSelectedMission > 0 && CurrentSlotOutsidePackage(self))
                Add(CustomCoopTrailEndNotice(self), LobbyLaunchStatusKind.Blocking);
            bool localError = HasActiveCoopPackage() && !IsLocalPackageReady() && !string.IsNullOrWhiteSpace(localPackageError);
            if (localError) Add(localPackageError, LobbyLaunchStatusKind.Blocking, scope: LobbyLaunchStatusScope.Lobby);
            if (activation.CoopPackages && CurrentSlotRequiresPackage(self))
            {
                Add(SelectedFeatureDependencyError(), LobbyLaunchStatusKind.Blocking, scope: LobbyLaunchStatusScope.Lobby);
                if (!localError && (!IsLocalPackageReady() || selected == null))
                    Add(GetLocalBlockReason(), LobbyLaunchStatusKind.Loading);
                if (selected?.Loaded.Definition.Map.Source == "bundled" && !sharedMapLookupRegistered)
                    Add(SerpLocalization.Get("ExtendedData.MapServiceUnavailable"), LobbyLaunchStatusKind.Blocking,
                        scope: LobbyLaunchStatusScope.Lobby);
                Add(mapLookupFailure, LobbyLaunchStatusKind.Blocking, scope: LobbyLaunchStatusScope.Lobby);
                if (host && humans < 2)
                    Add(SerpLocalization.Get("ExtendedData.WaitingForPartner"), LobbyLaunchStatusKind.Waiting);
                else if (host)
                {
                    // Partner checks only apply while a partner is present; Vanilla keeps Start disabled otherwise.
                    List<HumanPackageState> participants = GetHumanPackageStates(self);
                    string roster = DescribeHumanPackageStates(self, participants);
                    CoopPackageBlockMessages.ParticipantStatus(participants.Select(state => new KeyValuePair<string, string>(
                        state.Name, state.PlayerId > 0 ? state.Status : null)), ExpectedReadyStatus(),
                        out string blocking, out string loading);
                    Add(blocking, LobbyLaunchStatusKind.Blocking, roster);
                    Add(loading, LobbyLaunchStatusKind.Loading, roster);
                    if (!settings.System_ArePerPlayerSettingsReady(participants.Select(item => item.PlayerId), out string syncError) &&
                        loading.Length == 0)
                        Add(SerpLocalization.Get("ExtendedData.ErrorPackageReportsPending"), LobbyLaunchStatusKind.Loading,
                            "Shared personal settings incomplete: " + syncError + "; " + roster);
                    if (selected != null && IsLocalPackageReady() && AreAllHumanPlayersPackageReady(participants))
                        Add(CoopAiStartBlockReason(self), LobbyLaunchStatusKind.Blocking);
                }
            }
            if (activation.LordDataIntegration && lordDataCoordinator?.NeedsLordStartGate == true)
            {
                // Lord data synchronizes after the package selection; a persistent failure is escalated by APIShared.
                if (!lordDataCoordinator.IsLocalSelectionReady(CurrentLordInfoMap(self), out string reason,
                    verifyEffectiveData: false)) Add(reason, LobbyLaunchStatusKind.Loading);
                // Same gate as the Start click (NeedsLordStartGate covers Trail and lobby selections);
                // the effective-data verification stays with the click because it reads game data.
                if (host && humans >= 2 &&
                    !lordDataCoordinator.IsReadyToLaunch(self, out reason, verifyEffectiveData: false))
                    Add(reason, LobbyLaunchStatusKind.Loading, lordDataCoordinator.LastSlotSelectionDiagnostic);
            }
            if (lastAttemptScope == LaunchScope(self)) Add(lastAttemptFailure, LobbyLaunchStatusKind.Blocking);
            return items;
        }

        private static bool IsCurrentSteamLobby(FRONT_Multiplayer self) =>
            self?.currentLobby != null && !self.singlePlayerCoop && !FRONT_Multiplayer.skirmishGame &&
            self.currentLobby.id.m_SteamID != 0 &&
            SHCDESE.API.GameNetworkAPI.IsNetworkedEnvironment() &&
            Platform_Multiplayer.Instance?.activeLobby?.id.m_SteamID == self.currentLobby.id.m_SteamID;

        private string LaunchScope(FRONT_Multiplayer self) => self?.currentLobby?.id.m_SteamID + "|" +
            settings.ActiveCoopPackageId + "|" + settings.ActiveCoopPackageFingerprint + "|" +
            self?.currentLobby?.coopTrailID + "/" + self?.currentLobby?.coopSelectedMission;

        private string DescribeLaunchBlockContext(FRONT_Multiplayer self) =>
            "role=" + (self?.singlePlayerCoop == true ? "single-player" : self?.currentLobby == null ? "unknown" :
                self.currentLobby.isHost ? "host" : "client") + ", lobby=" + self?.currentLobby?.id.m_SteamID +
            ", package=" + settings.ActiveCoopPackageId + ", fingerprint=" + settings.ActiveCoopPackageFingerprint +
            ", mission=Trail" + (self?.currentLobby?.coopTrailID + 1) + "/" + self?.currentLobby?.coopSelectedMission +
            ", localStatus=" + settings.CoopPackageStatus;

        private void ReportUnacceptedReady(FRONT_Multiplayer self, string command)
        {
            // Only the custom Coop mission map is ours to explain; in other lobbies (e.g. a skirmish whose map the
            // client still lacks) Vanilla's own map check and transfer apply.
            if (command != "Ready" || ReadLobbyFlag(self, LocalReadyField) || !HasActiveCoopPackage() ||
                self?.currentLobby?.coopTrailGame != true || !CurrentSlotRequiresPackage(self)) return;
            string reason = ReadLobbyFlag(self, MapValidField)
                ? SerpLocalization.Get("ExtendedData.ReadyNotAccepted") : SerpLocalization.Get("ExtendedData.MapNotVerified");
            BlockLaunch(command, reason, "Vanilla result: mapChecked=" + ReadLobbyFlag(self, MapCheckedField) +
                ", mapValid=" + ReadLobbyFlag(self, MapValidField) + ", map=" + self.currentLobby?.mapFileName +
                ", hostCRC=" + self.currentLobby?.crc + ", localCRC=" + selected?.Header.crc);
        }

        private bool PrepareLordSaveWithDiagnostics(FRONT_Multiplayer self, FileHeader header)
        {
            bool ready = lordDataCoordinator.PrepareSave(self, header);
            if (!ready)
                // The shared requester already displays its rejection dialog.
                ReportLaunchFailure("Play", string.IsNullOrEmpty(lordDataCoordinator.LastSaveBlockReason)
                    ? lordDataCoordinator.CurrentBlockReason : lordDataCoordinator.LastSaveBlockReason, null, showPopup: false);
            return ready;
        }
    }
}
