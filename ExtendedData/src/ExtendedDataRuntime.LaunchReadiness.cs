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
        private readonly CoopLaunchAnnouncementState blockAnnouncements = new CoopLaunchAnnouncementState();
        private readonly HashSet<INotifyPropertyChanged> configurationObservers = new HashSet<INotifyPropertyChanged>();
        private int readinessQueued;
        private long readinessInputRevision;
        private long lastAttemptRevision;
        private string publishedMapIdentity = string.Empty;
        private string mapLookupFailure = string.Empty;
        private string lastAttemptFailure = string.Empty;
        private string lastAttemptScope = string.Empty;
        private bool sharedMapLookupRegistered;
        private bool readinessInMission;
        private bool launchReadinessActive;
        private long blockTransitionSequence;

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
            if (!LobbyChatPresentation.IsAvailable ||
                !LobbyChatPresentation.TryRegisterSystemMessageRule(ExtendedDataPlugin.PluginGuid, "Launch.Blocks",
                    message => launchReadinessActive && CoopLaunchBlockDiagnostics.IsBlockedStartChat(message.Message), out diagnostic))
                LogError("APIShared lobby system-message presentation is unavailable.");
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

        private void ReadinessLobbyChanged(PerPlayerLobbySnapshot snapshot) => ReadinessInputChanged();

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
        private void QueueLaunchReadiness()
        {
            if (!launchReadinessActive) return;
            if (Interlocked.Exchange(ref readinessQueued, 1) != 0) return;
            if (!Shared.UnityMainThreadDispatch.TryEnqueue(() =>
            {
                Interlocked.Exchange(ref readinessQueued, 0);
                if (lastAttemptRevision != Interlocked.Read(ref readinessInputRevision)) lastAttemptFailure = string.Empty;
                try { ObserveLaunchReadiness(); }
                catch (Exception error) { LogError("Could not inspect lobby launch readiness: " + error); }
            }))
            {
                Interlocked.Exchange(ref readinessQueued, 0);
                LogError("Could not dispatch lobby readiness observation.");
            }
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

        private void ObserveLaunchReadiness()
        {
            if (!launchReadinessActive) return;
            FRONT_Multiplayer self = GetExistingMainViewModel()?.FRONTMultiplayer;
            if (readinessInMission || !IsCurrentSteamLobby(self)) { blockAnnouncements.Reset(); return; }
            WatchConfigurationReadiness();
            var reasons = new List<string>();
            string packageSyncDetail = string.Empty;
            if (ModSettingsApplication.HasActivationFailures)
                reasons.Add(SerpLocalization.Get("ExtendedData.SettingsProviderFailed"));
            foreach (var endpoint in ModSettingsApplication.Endpoints)
                if (endpoint.Value.System_ConfigurationNeedsRestart())
                    reasons.Add(SerpLocalization.Get("ExtendedData.SettingsRestartRequired") + " [" + endpoint.Key + "]");
            if (activation.CoopPackages && CurrentSlotOutsidePackage(self)) reasons.Add(CustomCoopTrailEndNotice(self));
            if (HasActiveCoopPackage() && !IsLocalPackageReady() && !string.IsNullOrWhiteSpace(localPackageError))
                reasons.Add(localPackageError);
            if (activation.CoopPackages && CurrentSlotRequiresPackage(self))
            {
                string feature = SelectedFeatureDependencyError();
                if (feature != null) reasons.Add(feature);
                if (!IsLocalPackageReady()) reasons.Add(GetLocalBlockReason());
                if (selected == null) reasons.Add(SerpLocalization.Get("ExtendedData.ErrorPackageNotReady"));
                if (selected?.Loaded.Definition.Map.Source == "bundled" && !sharedMapLookupRegistered)
                    reasons.Add(SerpLocalization.Get("ExtendedData.MapServiceUnavailable"));
                if (mapLookupFailure.Length != 0) reasons.Add(mapLookupFailure);
                if (self.currentLobby.isHost)
                {
                    List<HumanPackageState> participants = GetHumanPackageStates(self);
                    if (!settings.System_ArePerPlayerSettingsReady(participants.Select(item => item.PlayerId), out string syncError))
                    {
                        reasons.Add(SerpLocalization.Get("ExtendedData.ErrorPackageReportsPending"));
                        packageSyncDetail = "; Shared personal settings incomplete: " + syncError;
                    }
                    if (!AreAllHumanPlayersPackageReady(participants)) reasons.Add(GetParticipantPackageBlockReason(participants));
                    if (selected != null && IsLocalPackageReady())
                    {
                        string aiError = CoopAiStartBlockReason(self);
                        if (aiError != null) reasons.Add(aiError);
                    }
                }
            }
            if (activation.LordDataIntegration && lordDataCoordinator?.NeedsLordStartGate == true)
            {
                if (!lordDataCoordinator.IsLocalSelectionReady(CurrentLordInfoMap(self), out string reason,
                    verifyEffectiveData: false)) reasons.Add(reason);
                if (self.currentLobby.isHost && selected?.Loaded.LordRequirements != null &&
                    !lordDataCoordinator.IsReadyToLaunch(self, out reason, verifyEffectiveData: false)) reasons.Add(reason);
            }
            if (lastAttemptScope == LaunchScope(self) && lastAttemptFailure.Length != 0) reasons.Add(lastAttemptFailure);
            string[] current = reasons.Where(reason => !string.IsNullOrWhiteSpace(reason)).Distinct(StringComparer.Ordinal).ToArray();
            if (!blockAnnouncements.ShouldSend(LaunchScope(self), "Lobby", current, true, DateTime.UtcNow)) return;
            blockTransitionSequence++;
            string detail = "transition=" + blockTransitionSequence + packageSyncDetail;
            if (self.currentLobby.isHost && activation.CoopPackages && CurrentSlotRequiresPackage(self))
                detail += "; " + DescribeHumanPackageStates(self, GetHumanPackageStates(self));
            foreach (string reason in current)
                CoopLaunchBlockDiagnostics.ReportLocal(log, "Readiness", reason, DescribeLaunchBlockContext(self),
                    detail);
            SendLaunchBlockChat(self, SerpLocalization.Get("ExtendedData.ReadyOrStart"), current);
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

        private void SendLaunchBlockChat(FRONT_Multiplayer self, string operation, IEnumerable<string> reasons)
        {
            if (!IsCurrentSteamLobby(self)) return;
            if (operation == "Ready" || operation == "ReadyLock") operation = SerpLocalization.Get("ExtendedData.ReadyAction");
            else if (operation == "Play" || operation == "COOP_START") operation = SerpLocalization.Get("ExtendedData.StartAction");
            try
            {
                foreach (string message in CoopLaunchBlockDiagnostics.ChatMessages(operation, reasons))
                    Platform_Multiplayer.Instance.SendLobbyChatMessage(message);
            }
            catch (Exception error)
            {
                blockAnnouncements.Reset();
                LogError("Could not announce lobby launch block: " + error);
            }
        }

        private void ReportUnacceptedReady(FRONT_Multiplayer self, string command)
        {
            if (command != "Ready" || ReadLobbyFlag(self, LocalReadyField) || !HasActiveCoopPackage()) return;
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
