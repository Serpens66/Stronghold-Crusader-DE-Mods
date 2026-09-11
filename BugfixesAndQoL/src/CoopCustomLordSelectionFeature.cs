// Feature: Scrollable custom-lord partner selection for Vanilla's singleplayer Coop Trails.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using SHCDESE.API;
using SHCDESE.API.Components.AI;
using SHCDESE.Interop;
using SHCDESE.NoesisUtil;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed class CoopCustomLordSelectionFeature : INotifyPropertyChanged
    {
        private delegate void CoopMissionChangedDelegate(
            FRONT_Multiplayer self, int trailId, int missionId, bool resetOrderSwapped);
        private delegate void UploadDefaultAivDelegate(
            int lordType, int playerId, bool everySkirmishSet, bool everyHistoricalSet);
        private delegate void InitCoopGameDelegate(ulong steamId, string userName, string coaString);
        private delegate int[] GetCoopRowInfoDelegate(
            int row, int trailId, out ulong steamId, out string userName,
            out bool hidden, bool countHidden, out string coaString);
        private delegate bool GetCoopRowHiddenInfoDelegate(ulong steamId, out string userName);
        private delegate void CoopPopulateFriendsListDelegate(FRONT_Multiplayer self);
        private delegate void AiLordEnterDelegate(FRONT_Multiplayer self, string parameter);

        private static readonly BindingFlags AllStatic =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly BindingFlags AllInstance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const int PortraitColumns = 9;
        private const float PortraitLeft = 20f;
        private const float PortraitTop = 20f;
        private const float PortraitStep = 110f;
        private const float PortraitViewportHeight = 450f;
        private const float PortraitContentWidth = 1020f;
        private const string DeleteProgressCommandPrefix = "BugfixesAndQoL_CoopDelete";
        private static int VanillaLordCount =>
            CoopCustomLordSelectionPolicy.CustomPartnerLordType - 1;
        private static readonly FieldInfo CoopInfoDictionaryField =
            RequireField(typeof(ConfigSettings), "coopInfoDict", AllStatic);
        private static readonly Type CoopRecordType =
            CoopInfoDictionaryField.FieldType.GetGenericArguments()[1];
        private static readonly FieldInfo CoopRecordUserNameField =
            RequireField(CoopRecordType, "userName", AllInstance);
        private static readonly FieldInfo CoopInfoListField =
            RequireField(typeof(ConfigSettings), "coopInfoList", AllStatic);
        private static readonly MethodInfo GetCoopFileNameMethod =
            RequireMethod(typeof(ConfigSettings), "GetCoopFileName", AllStatic);
        private static readonly FieldInfo CoopFriendSteamIdsField =
            RequireField(typeof(FRONT_Multiplayer), "coopFriendsSteamIDs", AllInstance);
        private static readonly FieldInfo CoopFriendHiddenField =
            RequireField(typeof(FRONT_Multiplayer), "coopFriendsRowHidden", AllInstance);
        private static readonly FieldInfo CoopFriendsPageField =
            RequireField(typeof(FRONT_Multiplayer), "coopFriendsPage", AllInstance);
        private static readonly FieldInfo CoopShowHiddenFriendsField =
            RequireField(typeof(FRONT_Multiplayer), "coopShowHiddenFriends", AllInstance);
        private static readonly FieldInfo SinglePlayerCoopAllyField =
            RequireField(typeof(FRONT_Multiplayer), "singlePlayerCoopAlly", AllInstance);
        private static readonly MethodInfo SetCoopRowMethod =
            RequireMethod(
                typeof(FRONT_Multiplayer),
                "SetCoopRow",
                AllInstance,
                typeof(int), typeof(string), typeof(ulong), typeof(ImageSource), typeof(bool));

        private static CoopCustomLordSelectionFeature current;

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly ObservableCollection<CoopLordChoice> choices =
            new ObservableCollection<CoopLordChoice>();
        private readonly HashSet<Button> hookedCustomButtons = new HashSet<Button>();

        // Published hooks are process-lifetime objects. These fields keep every delegate rooted.
        private Hook coopMissionChangedHook;
        private Hook uploadDefaultAivHook;
        private Hook initCoopGameHook;
        private Hook getCoopRowInfoHook;
        private Hook getCoopRowHiddenInfoHook;
        private Hook coopPopulateFriendsListHook;
        private Hook aiLordEnterHook;
        private CoopMissionChangedDelegate coopMissionChangedOriginal;
        private UploadDefaultAivDelegate uploadDefaultAivOriginal;
        private InitCoopGameDelegate initCoopGameOriginal;
        private GetCoopRowInfoDelegate getCoopRowInfoOriginal;
        private GetCoopRowHiddenInfoDelegate getCoopRowHiddenInfoOriginal;
        private CoopPopulateFriendsListDelegate coopPopulateFriendsListOriginal;
        private AiLordEnterDelegate aiLordEnterOriginal;

        private FRONT_Multiplayer owner;
        private string selectedLordName = string.Empty;
        private string selectedDisplayName = string.Empty;
        private TextureSource selectedPortrait;
        private bool customSelectionActive;
        private bool initialized;

        internal CoopCustomLordSelectionFeature(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public ObservableCollection<CoopLordChoice> Choices => choices;

        internal void Initialize()
        {
            if (initialized)
                return;

            Hook pendingMission = null;
            Hook pendingUpload = null;
            Hook pendingInit = null;
            Hook pendingRow = null;
            Hook pendingHidden = null;
            Hook pendingPopulate = null;
            Hook pendingEnter = null;
            try
            {
                pendingMission = InstallHook(
                    RequireMethod(typeof(FRONT_Multiplayer), nameof(FRONT_Multiplayer.CoopMissionChanged),
                        AllInstance, typeof(int), typeof(int), typeof(bool)),
                    (CoopMissionChangedDelegate)CoopMissionChangedHook,
                    out coopMissionChangedOriginal);
                pendingUpload = InstallHook(
                    RequireMethod(typeof(AIVLoader), nameof(AIVLoader.UploadDefaultAIV), AllStatic,
                        typeof(int), typeof(int), typeof(bool), typeof(bool)),
                    (UploadDefaultAivDelegate)UploadDefaultAivHook,
                    out uploadDefaultAivOriginal);
                pendingInit = InstallHook(
                    RequireMethod(typeof(ConfigSettings), nameof(ConfigSettings.InitCoopGame), AllStatic,
                        typeof(ulong), typeof(string), typeof(string)),
                    (InitCoopGameDelegate)InitCoopGameHook,
                    out initCoopGameOriginal);
                pendingRow = InstallHook(
                    RequireMethod(typeof(ConfigSettings), nameof(ConfigSettings.getCoopRowInfo), AllStatic,
                        typeof(int), typeof(int), typeof(ulong).MakeByRefType(),
                        typeof(string).MakeByRefType(), typeof(bool).MakeByRefType(),
                        typeof(bool), typeof(string).MakeByRefType()),
                    (GetCoopRowInfoDelegate)GetCoopRowInfoHook,
                    out getCoopRowInfoOriginal);
                pendingHidden = InstallHook(
                    RequireMethod(typeof(ConfigSettings), nameof(ConfigSettings.getCoopRowHiddenInfo), AllStatic,
                        typeof(ulong), typeof(string).MakeByRefType()),
                    (GetCoopRowHiddenInfoDelegate)GetCoopRowHiddenInfoHook,
                    out getCoopRowHiddenInfoOriginal);
                pendingPopulate = InstallHook(
                    RequireMethod(typeof(FRONT_Multiplayer), "CoopPopulateFriendsList", AllInstance),
                    (CoopPopulateFriendsListDelegate)CoopPopulateFriendsListHook,
                    out coopPopulateFriendsListOriginal);
                pendingEnter = InstallHook(
                    RequireMethod(typeof(FRONT_Multiplayer), nameof(FRONT_Multiplayer.AILordEnter),
                        AllInstance, typeof(string)),
                    (AiLordEnterDelegate)AiLordEnterHook,
                    out aiLordEnterOriginal);

                coopMissionChangedHook = pendingMission;
                uploadDefaultAivHook = pendingUpload;
                initCoopGameHook = pendingInit;
                getCoopRowInfoHook = pendingRow;
                getCoopRowHiddenInfoHook = pendingHidden;
                coopPopulateFriendsListHook = pendingPopulate;
                aiLordEnterHook = pendingEnter;
                GameXAMLManagerAPI.Instance.RegisterBinding("CoopCustomLordSelectionHost", this);
                current = this;
                initialized = true;
            }
            catch
            {
                // Only a not-yet-published installation candidate may be rolled back.
                pendingEnter?.Dispose();
                pendingPopulate?.Dispose();
                pendingHidden?.Dispose();
                pendingRow?.Dispose();
                pendingInit?.Dispose();
                pendingUpload?.Dispose();
                pendingMission?.Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogInfo(log, "Bugfixes and QoL Coop custom-lord selection initialized.");
        }

        internal void ApplySetting()
        {
            if (owner == null || !MainViewModel.viewModelLoaded)
                return;

            if (MainViewModel.Instance.Show_CoopAIAllyPanel)
                RefreshChoices(owner);
            else if (FRONT_Multiplayer.coopGame)
                CoopPopulateFriendsListHook(owner);
        }

        internal static bool OnMultiplayerButtonStarting(FRONT_Multiplayer self, string command)
        {
            CoopCustomLordSelectionFeature feature = current;
            if (feature == null)
                return false;

            if (feature.TryRequestProgressDeletion(self, command))
                return true;

            if (string.Equals(command, "CoopSinglePlayer", StringComparison.Ordinal))
                feature.ClearSelection("new Coop run");
            else if (string.Equals(command, "CoopKick", StringComparison.Ordinal) ||
                     string.Equals(command, "CoopLeave", StringComparison.Ordinal))
                feature.ClearSelection(command);
            return false;
        }

        internal static void OnMultiplayerButtonCompleted(FRONT_Multiplayer self, string command)
        {
            if (string.Equals(command, "CoopSinglePlayer", StringComparison.Ordinal))
                current?.RefreshChoices(self);
        }

        internal static void OnMultiplayerUpdated(FRONT_Multiplayer self)
        {
            current?.UpdateCoopSelectionUi(self);
        }

        private bool TryRequestProgressDeletion(FRONT_Multiplayer self, string command)
        {
            if (string.IsNullOrEmpty(command) ||
                !command.StartsWith(DeleteProgressCommandPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            // Always consume our private command, even if a stale hidden button fires after disabling the setting.
            if (!EnhancementsEnabled || self == null || !FRONT_Multiplayer.coopGame ||
                FRONT_Multiplayer.customCoopGame ||
                !int.TryParse(command.Substring(DeleteProgressCommandPrefix.Length), out int rowNumber))
            {
                return true;
            }

            int rowIndex = rowNumber - 1;
            ulong[] steamIds = CoopFriendSteamIdsField.GetValue(self) as ulong[];
            if (steamIds == null || rowIndex < 0 || rowIndex >= steamIds.Length || steamIds[rowIndex] == 0UL)
                return true;

            ulong partnerId = steamIds[rowIndex];
            string rawName;
            if (partnerId == CoopCustomLordSelectionPolicy.SharedProgressId)
                rawName = GetStoredCustomDisplayName();
            else
                getCoopRowHiddenInfoOriginal(partnerId, out rawName);
            string partnerName = CoopCustomLordSelectionPolicy.FormatHistoryName(
                rawName,
                partnerId,
                enhancementsEnabled: true);
            HUD_ConfirmationPopup.ShowConfirmationMessage(
                SerpLocalization.Get("BugfixesAndQoL.CoopDeleteProgressTitle"),
                () => DeleteProgressConfirmed(self, rowIndex, partnerId),
                () => { },
                SerpLocalization.Get(
                    "BugfixesAndQoL.CoopDeleteProgressMessage",
                    "PartnerName",
                    partnerName));
            return true;
        }

        private void DeleteProgressConfirmed(FRONT_Multiplayer self, int rowIndex, ulong partnerId)
        {
            IDictionary dictionary = null;
            IList orderedRecords = null;
            object record = null;
            int orderedIndex = -1;
            string coopFilePath = null;
            byte[] savedFile = null;
            bool fileExisted = false;
            try
            {
                ulong[] steamIds = CoopFriendSteamIdsField.GetValue(self) as ulong[];
                if (steamIds == null || rowIndex < 0 || rowIndex >= steamIds.Length)
                {
                    throw new InvalidOperationException("The selected Coop history row changed before confirmation.");
                }

                dictionary = CoopInfoDictionaryField.GetValue(null) as IDictionary;
                orderedRecords = CoopInfoListField.GetValue(null) as IList;
                record = dictionary?[partnerId];
                orderedIndex = orderedRecords?.IndexOf(record) ?? -1;
                if (!CoopCustomLordSelectionPolicy.CanConfirmProgressDeletion(
                    partnerId,
                    steamIds[rowIndex],
                    record != null,
                    orderedIndex >= 0))
                {
                    throw new InvalidOperationException(
                        "The selected Coop progress record changed before confirmation.");
                }

                coopFilePath = GetCoopFileNameMethod.Invoke(null, null) as string;
                if (string.IsNullOrWhiteSpace(coopFilePath))
                    throw new InvalidOperationException("Vanilla did not provide a Coop progress file path.");
                fileExisted = File.Exists(coopFilePath);
                if (fileExisted)
                    savedFile = File.ReadAllBytes(coopFilePath);

                dictionary.Remove(partnerId);
                orderedRecords.RemoveAt(orderedIndex);
                if (dictionary.Count == 0)
                {
                    if (File.Exists(coopFilePath))
                        File.Delete(coopFilePath);
                    if (File.Exists(coopFilePath))
                        throw new IOException("The empty Coop progress file could not be removed.");
                }
                else
                {
                    ConfigSettings.SaveCoop();
                    if (!File.Exists(coopFilePath) || CoopFileContainsPartner(coopFilePath, partnerId))
                        throw new IOException("Vanilla did not persist the Coop progress deletion.");
                }

                ResetHistoryNavigation(self);
                if (partnerId == CoopCustomLordSelectionPolicy.SharedProgressId)
                    ClearSelection("shared Coop progress deleted");
                CoopPopulateFriendsListHook(self);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Bugfixes and QoL deleted Coop progress for partner ID {partnerId}.");
            }
            catch (Exception ex)
            {
                try
                {
                    if (dictionary != null && record != null && !dictionary.Contains(partnerId))
                        dictionary.Add(partnerId, record);
                    if (orderedRecords != null && record != null && !orderedRecords.Contains(record))
                    {
                        int restoreIndex = Math.Max(0, Math.Min(orderedIndex, orderedRecords.Count));
                        orderedRecords.Insert(restoreIndex, record);
                    }
                    if (!string.IsNullOrWhiteSpace(coopFilePath))
                    {
                        if (fileExisted && savedFile != null)
                            File.WriteAllBytes(coopFilePath, savedFile);
                        else if (!fileExisted && File.Exists(coopFilePath))
                            File.Delete(coopFilePath);
                    }
                }
                catch (Exception rollbackException)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        "Could not roll back the failed Coop progress deletion: " + rollbackException);
                }

                Shared.DebugLogHelper.LogError(log, "Could not delete Coop progress: " + ex);
                HUD_ConfirmationPopup.ShowConfirmationOKMessage(
                    SerpLocalization.Get("BugfixesAndQoL.CoopDeleteProgressErrorTitle"),
                    () => { },
                    SerpLocalization.Get("BugfixesAndQoL.CoopDeleteProgressErrorMessage"));
            }
        }

        private static bool CoopFileContainsPartner(string path, ulong partnerId)
        {
            string prefix = partnerId + ":";
            foreach (string line in File.ReadLines(path))
            {
                if (line.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private void ResetHistoryNavigation(FRONT_Multiplayer self)
        {
            CoopFriendsPageField.SetValue(self, 0);
            CoopShowHiddenFriendsField.SetValue(self, false);
            bool hasHidden = ConfigSettings.getCoopTrailCount(countHidden: true) !=
                ConfigSettings.getCoopTrailCount(countHidden: false);
            UpdateShowHiddenControl(FRONT_CoopTrail1.Instance, hasHidden);
            UpdateShowHiddenControl(FRONT_CoopTrail2.Instance, hasHidden);
            UpdateShowHiddenControl(FRONT_CoopTrail3.Instance, hasHidden);
            UpdateShowHiddenControl(FRONT_CoopTrail4.Instance, hasHidden);
        }

        private static void UpdateShowHiddenControl(FrameworkElement trailView, bool hasHidden)
        {
            if (!(trailView?.FindName("ShowHidden") is ToggleButton toggle))
                return;
            toggle.IsChecked = false;
            toggle.Visibility = hasHidden ? Visibility.Visible : Visibility.Hidden;
        }

        internal static bool TryHandleSkirmishAiAddClick(FRONT_Multiplayer self, string parameter)
        {
            CoopCustomLordSelectionFeature feature = current;
            if (feature == null || !int.TryParse(parameter, out int lordType))
                return false;

            if (lordType == CoopCustomLordSelectionPolicy.CustomPartnerLordType)
                return feature.TryRestoreFromContinuation(self);

            if (lordType >= 0 && lordType < CoopCustomLordSelectionPolicy.CustomPartnerLordType &&
                FRONT_Multiplayer.coopGame)
            {
                feature.ClearSelection("Vanilla Coop partner selected");
            }
            return false;
        }

        private void RefreshChoices(FRONT_Multiplayer self)
        {
            owner = self;
            if (CustomisationFileManager.Instance.filesChanged)
                CustomisationFileManager.Instance.BuildFileLists();

            UnhookCustomButtons();
            choices.Clear();
            if (settings.EnableMod && settings.EnableCustomLordListEnhancements)
            {
                foreach (CustomisationFileManager.CustomLord lord in CustomisationFileManager.Instance.GetCustomLords())
                {
                    if (!CustomLordLobbyUtility.IsValid(lord))
                        continue;
                    CustomisationFileManager.CustomLord captured = lord;
                    int portraitIndex = VanillaLordCount + choices.Count;
                    choices.Add(new CoopLordChoice(
                        captured,
                        lord.image,
                        lord.lordDisplayName ?? lord.lordName,
                        PortraitLeft + portraitIndex % PortraitColumns * PortraitStep,
                        PortraitTop + portraitIndex / PortraitColumns * PortraitStep,
                        () => SelectCustom(captured)));
                }
            }

            OnPropertyChanged(nameof(Choices));
            UpdateCoopSelectionUi(self);
            Shared.DebugLogHelper.LogDebug(
                log,
                () => $"Bugfixes and QoL refreshed Coop partner choices: count={choices.Count}, customEnabled={settings.EnableMod && settings.EnableCustomLordListEnhancements}.");
        }

        private void SelectCustom(CustomisationFileManager.CustomLord lord) =>
            SelectCustom(lord, requireOpenPanel: true);

        private void SelectCustom(
            CustomisationFileManager.CustomLord lord,
            bool requireOpenPanel)
        {
            FRONT_Multiplayer self = owner;
            bool canSelect = requireOpenPanel
                ? CoopCustomLordSelectionPolicy.CanSelect(
                    settings.EnableMod && settings.EnableCustomLordListEnhancements,
                    FRONT_Multiplayer.coopGame,
                    FRONT_Multiplayer.customCoopGame,
                    MainViewModel.Instance.Show_CoopAIAllyPanel,
                    self?.currentLobby?.isHost == true,
                    self?.currentLobby?.CountHumanPlayers() ?? 0)
                : FRONT_Multiplayer.coopGame && !FRONT_Multiplayer.customCoopGame &&
                  self?.currentLobby?.isHost == true && self.currentLobby.CountHumanPlayers() == 1;
            if (!canSelect || !CustomLordLobbyUtility.IsValid(lord))
                return;

            try
            {
                // Vanilla team IDs are zero-based; its Coop ally is explicitly assigned team 1.
                Platform_Multiplayer.MPLobbyMember member = CustomLordLobbyUtility.AddAndInitialize(
                    self, lord, forcedTeam: 1, insertionIndex: null, colourId: null, out int playerId);
                if (playerId != 2)
                {
                    Platform_Multiplayer.Instance.kickSkirmishPlayer(member.GetSteamID());
                    CustomLordLobbyUtility.UpdateSteamMappings(self);
                    throw new InvalidOperationException("The Coop custom lord did not occupy player slot 2.");
                }

                selectedLordName = lord.lordName;
                selectedDisplayName = string.IsNullOrWhiteSpace(lord.lordDisplayName)
                    ? lord.lordName
                    : lord.lordDisplayName;
                selectedPortrait = lord.image;
                customSelectionActive = true;
                self.singlePlayerCoop = true;
                SinglePlayerCoopAllyField.SetValue(
                    self,
                    (ulong)CoopCustomLordSelectionPolicy.CustomPartnerLordType);
                MainViewModel.Instance.Show_CoopAIAllyPanel = false;
                MainViewModel.Instance.Show_CoopMapIcons = true;
                MainViewModel.Instance.Show_CoopHostJoinedPane = true;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Bugfixes and QoL selected Coop custom lord '{selectedLordName}' in player slot 2.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "Could not select the Coop custom lord: " + ex);
                ShowMissingLordMessage();
            }
        }

        private bool TryRestoreFromContinuation(FRONT_Multiplayer self)
        {
            if (!FRONT_Multiplayer.coopGame || FRONT_Multiplayer.customCoopGame || self?.currentLobby == null)
                return false;

            if (!TryResolveSelectedLord(out CustomisationFileManager.CustomLord lord))
            {
                ShowMissingLordMessage();
                return true;
            }

            RefreshChoices(self);
            SelectCustom(lord, requireOpenPanel: false);
            return true;
        }

        private void CoopMissionChangedHook(
            FRONT_Multiplayer self,
            int trailId,
            int missionId,
            bool resetOrderSwapped)
        {
            if (!IsActiveCustomContext(self))
            {
                coopMissionChangedOriginal(self, trailId, missionId, resetOrderSwapped);
                return;
            }

            if (!TryResolveSelectedLord(out CustomisationFileManager.CustomLord lord))
            {
                ShowMissingLordMessage();
                return;
            }

            coopMissionChangedOriginal(self, trailId, missionId, resetOrderSwapped);
            try
            {
                RestorePartnerAfterMissionChange(self, lord);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "Could not restore the Coop custom lord after mission change: " + ex);
                ShowMissingLordMessage();
            }
        }

        private void RestorePartnerAfterMissionChange(
            FRONT_Multiplayer self,
            CustomisationFileManager.CustomLord lord)
        {
            Platform_Multiplayer.MPLobbyMember oldPartner =
                self.currentLobby.GetLobbyMemberFromThis_PlayerID(2);
            if (oldPartner == null)
                throw new InvalidOperationException("Vanilla did not rebuild the Coop partner in player slot 2.");

            int insertionIndex = self.currentLobby.members.IndexOf(oldPartner);
            int colourId = oldPartner.colourID;
            Platform_Multiplayer.Instance.kickSkirmishPlayer(oldPartner.GetSteamID());
            Platform_Multiplayer.MPLobbyMember replacement = CustomLordLobbyUtility.AddAndInitialize(
                self, lord, forcedTeam: 1, insertionIndex, colourId, out int playerId);
            if (replacement == null || playerId != 2)
                throw new InvalidOperationException("The restored Coop custom lord did not occupy player slot 2.");

            self.currentLobby.validateTeams();
            self.currentLobby.forceCoopTeams();
            Shared.DebugLogHelper.LogDebug(
                log,
                () => $"Bugfixes and QoL restored Coop custom lord '{lord.lordName}' after mission change.");
        }

        private void UploadDefaultAivHook(
            int lordType,
            int playerId,
            bool everySkirmishSet,
            bool everyHistoricalSet)
        {
            FRONT_Multiplayer self = MainViewModel.viewModelLoaded
                ? MainViewModel.Instance.FRONTMultiplayer
                : null;
            int selectedAllyLordType = self == null
                ? -1
                : checked((int)(ulong)SinglePlayerCoopAllyField.GetValue(self));
            if (!CoopCustomLordSelectionPolicy.ShouldReplaceDefaultAiv(
                    FRONT_Multiplayer.coopGame,
                    FRONT_Multiplayer.customCoopGame,
                    self?.singlePlayerCoop == true,
                    customSelectionActive,
                    selectedAllyLordType,
                    lordType,
                    playerId) ||
                self?.AIVs == null || self.AIVs.Length < playerId)
            {
                uploadDefaultAivOriginal(lordType, playerId, everySkirmishSet, everyHistoricalSet);
                return;
            }

            FRONT_Multiplayer.MPAIVInfo info = self.AIVs[playerId - 1];
            if (info == null || !string.Equals(info.lordName, selectedLordName, StringComparison.OrdinalIgnoreCase) ||
                info.aivs == null || info.aivs.Count == 0)
            {
                uploadDefaultAivOriginal(lordType, playerId, everySkirmishSet, everyHistoricalSet);
                return;
            }

            for (int index = 0; index < info.aivs.Count; index++)
                EngineInterface.ImportAIV(playerId - 1, index, info.aivs[index].data, 1);
            if (!info.builtInLord && info.lordConfig != null)
                EngineInterface.setCustomLordConfig(ref info.lordConfig.lordData, playerId);
        }

        private void InitCoopGameHook(ulong steamId, string userName, string coaString)
        {
            if (steamId == CoopCustomLordSelectionPolicy.SharedProgressId &&
                customSelectionActive && !string.IsNullOrWhiteSpace(selectedDisplayName))
            {
                userName = selectedDisplayName;
            }
            initCoopGameOriginal(steamId, userName, coaString);
        }

        private int[] GetCoopRowInfoHook(
            int row,
            int trailId,
            out ulong steamId,
            out string userName,
            out bool hidden,
            bool countHidden,
            out string coaString)
        {
            int[] result = getCoopRowInfoOriginal(
                row, trailId, out steamId, out userName, out hidden, countHidden, out coaString);
            if (result != null && steamId == CoopCustomLordSelectionPolicy.SharedProgressId)
            {
                userName = CoopCustomLordSelectionPolicy.FormatHistoryName(
                    GetStoredCustomDisplayName(),
                    steamId,
                    EnhancementsEnabled);
            }
            return result;
        }

        private bool GetCoopRowHiddenInfoHook(ulong steamId, out string userName)
        {
            bool result = getCoopRowHiddenInfoOriginal(steamId, out userName);
            if (steamId == CoopCustomLordSelectionPolicy.SharedProgressId)
                userName = GetStoredCustomDisplayName();
            return result;
        }

        private void CoopPopulateFriendsListHook(FRONT_Multiplayer self)
        {
            owner = self;
            coopPopulateFriendsListOriginal(self);
            ulong[] steamIds = CoopFriendSteamIdsField.GetValue(self) as ulong[];
            bool[] hiddenRows = CoopFriendHiddenField.GetValue(self) as bool[];
            if (steamIds == null || hiddenRows == null)
            {
                UpdateDeleteButtonVisibility(null);
                return;
            }

            string storedDisplayName = GetStoredCustomDisplayName();
            string displayName = CoopCustomLordSelectionPolicy.FormatHistoryName(
                storedDisplayName,
                CoopCustomLordSelectionPolicy.SharedProgressId,
                EnhancementsEnabled);
            ImageSource portrait = ResolveHistoryPortrait(storedDisplayName);
            for (int row = 0; row < steamIds.Length && row < hiddenRows.Length; row++)
            {
                if (steamIds[row] != CoopCustomLordSelectionPolicy.SharedProgressId)
                    continue;
                SetCoopRowMethod.Invoke(
                    self,
                    new object[] { row, displayName, steamIds[row], portrait, hiddenRows[row] });
            }
            UpdateDeleteButtonVisibility(steamIds);
        }

        private void AiLordEnterHook(FRONT_Multiplayer self, string parameter)
        {
            aiLordEnterOriginal(self, parameter);
            try
            {
                if (!EnhancementsEnabled || !ReferenceEquals(owner, self) ||
                    !FRONT_Multiplayer.coopGame || FRONT_Multiplayer.customCoopGame ||
                    !MainViewModel.viewModelLoaded || !MainViewModel.Instance.Show_CoopAIAllyPanel ||
                    !int.TryParse(parameter, out int zeroBasedLordType) ||
                    !TryGetVanillaLordPower(zeroBasedLordType, out int lordPower))
                {
                    return;
                }

                MainViewModel.Instance.SkirmishLordRolloverName =
                    CoopCustomLordSelectionPolicy.AppendLordPower(
                        MainViewModel.Instance.SkirmishLordRolloverName,
                        lordPower);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Could not append the Vanilla Coop lord power: " + ex.Message);
            }
        }

        private static bool TryGetVanillaLordPower(int zeroBasedLordType, out int lordPower)
        {
            lordPower = 0;
            int aicIndex = zeroBasedLordType + 1;
            int firstVanillaIndex = checked((int)AILords.SK_RAT);
            int lastVanillaIndex = checked((int)AILords.SK_DLC4B);
            if (aicIndex < firstVanillaIndex || aicIndex > lastVanillaIndex)
                return false;

            var aics = GameAIManagerAPI.Instance.GetAICArray();
            if (aicIndex < 0 || aicIndex >= aics.Length)
                return false;

            lordPower = aics[aicIndex].lord_power_display_level;
            return true;
        }

        private void UpdateDeleteButtonVisibility(ulong[] steamIds)
        {
            UpdateDeleteButtons(FRONT_CoopTrail1.Instance, steamIds);
            UpdateDeleteButtons(FRONT_CoopTrail2.Instance, steamIds);
            UpdateDeleteButtons(FRONT_CoopTrail3.Instance, steamIds);
            UpdateDeleteButtons(FRONT_CoopTrail4.Instance, steamIds);
        }

        private void UpdateDeleteButtons(FrameworkElement trailView, ulong[] steamIds)
        {
            if (trailView == null)
                return;
            for (int row = 0; row < 8; row++)
            {
                Button button = trailView.FindName("CoopDeleteProgress" + (row + 1)) as Button;
                if (button == null)
                    continue;
                ulong partnerId = steamIds != null && row < steamIds.Length ? steamIds[row] : 0UL;
                button.Visibility = CoopCustomLordSelectionPolicy.ShouldShowDeleteButton(
                    EnhancementsEnabled,
                    partnerId)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private ImageSource ResolveHistoryPortrait(string displayName)
        {
            if (customSelectionActive && selectedPortrait != null)
                return selectedPortrait;

            CustomisationFileManager.CustomLord match = null;
            foreach (CustomisationFileManager.CustomLord lord in CustomisationFileManager.Instance.GetCustomLords())
            {
                if (!string.Equals(lord?.lordDisplayName, displayName, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (match != null)
                    return null; // Ambiguous display names must not select an unrelated portrait.
                match = lord;
            }
            return match?.image;
        }

        private bool TryResolveSelectedLord(out CustomisationFileManager.CustomLord resolved)
        {
            resolved = null;
            if (CustomisationFileManager.Instance.filesChanged)
                CustomisationFileManager.Instance.BuildFileLists();

            string storedDisplayName = GetStoredCustomDisplayName();
            foreach (CustomisationFileManager.CustomLord lord in CustomisationFileManager.Instance.GetCustomLords())
            {
                if (!CustomLordLobbyUtility.IsValid(lord))
                    continue;
                if (!string.IsNullOrWhiteSpace(selectedLordName) &&
                    string.Equals(lord.lordName, selectedLordName, StringComparison.OrdinalIgnoreCase))
                {
                    resolved = lord;
                    return true;
                }
                if (string.IsNullOrWhiteSpace(selectedLordName) &&
                    string.Equals(lord.lordDisplayName, storedDisplayName, StringComparison.OrdinalIgnoreCase))
                {
                    if (resolved != null)
                        return false;
                    resolved = lord;
                }
            }

            if (resolved == null)
                return false;
            selectedLordName = resolved.lordName;
            selectedDisplayName = resolved.lordDisplayName;
            selectedPortrait = resolved.image;
            customSelectionActive = true;
            return true;
        }

        private string GetStoredCustomDisplayName()
        {
            try
            {
                IDictionary dictionary = CoopInfoDictionaryField.GetValue(null) as IDictionary;
                object record = dictionary?[CoopCustomLordSelectionPolicy.SharedProgressId];
                string stored = record == null ? null : CoopRecordUserNameField.GetValue(record) as string;
                if (!string.IsNullOrWhiteSpace(stored))
                    return stored;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarning(log, "Could not read the shared custom-lord Coop name: " + ex.Message);
            }
            return !string.IsNullOrWhiteSpace(selectedDisplayName)
                ? selectedDisplayName
                : SerpLocalization.Get("BugfixesAndQoL.CoopCustomLordGenericName");
        }

        private bool IsActiveCustomContext(FRONT_Multiplayer self) =>
            customSelectionActive && self != null && self.singlePlayerCoop &&
            FRONT_Multiplayer.coopGame && !FRONT_Multiplayer.customCoopGame &&
            (ulong)SinglePlayerCoopAllyField.GetValue(self) ==
                (ulong)CoopCustomLordSelectionPolicy.CustomPartnerLordType;

        private bool EnhancementsEnabled =>
            settings.EnableMod && settings.EnableCustomLordListEnhancements;

        private void UpdateCoopSelectionUi(FRONT_Multiplayer self)
        {
            if (!ReferenceEquals(owner, self) || !MainViewModel.viewModelLoaded ||
                !MainViewModel.Instance.Show_CoopAIAllyPanel)
            {
                return;
            }

            EnsureScrollablePortraitGrid(FRONT_CoopTrail1.Instance);
            EnsureScrollablePortraitGrid(FRONT_CoopTrail2.Instance);
            EnsureScrollablePortraitGrid(FRONT_CoopTrail3.Instance);
            EnsureScrollablePortraitGrid(FRONT_CoopTrail4.Instance);
            RefreshCustomButtonHandlers();
        }

        private void EnsureScrollablePortraitGrid(FrameworkElement trailView)
        {
            Grid portraitGrid = trailView?.FindName("CoopLordPortraitGrid") as Grid;
            if (portraitGrid == null)
                return;

            portraitGrid.Width = PortraitContentWidth;
            portraitGrid.Height = CalculatePortraitContentHeight();
            if (VisualTreeHelper.GetParent(portraitGrid) is ScrollViewer existingScroller)
            {
                existingScroller.Height = PortraitViewportHeight;
                return;
            }

            Panel parent = VisualTreeHelper.GetParent(portraitGrid) as Panel;
            if (parent == null)
                return;

            // Keep every original Vanilla child and binding intact; only reparent its Grid.
            parent.Children.Remove(portraitGrid);
            portraitGrid.Margin = new Thickness(0f);
            var scroller = new ScrollViewer
            {
                Width = 1055f,
                Height = PortraitViewportHeight,
                Margin = new Thickness(25f, 0f, 0f, 0f),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = portraitGrid
            };
            parent.Children.Add(scroller);
        }

        private float CalculatePortraitContentHeight()
        {
            return CoopCustomLordSelectionPolicy.CalculatePortraitContentHeight(choices.Count);
        }

        private void RefreshCustomButtonHandlers()
        {
            var currentButtons = new HashSet<Button>();
            CollectCustomButtons(FRONT_CoopTrail1.Instance, currentButtons);
            CollectCustomButtons(FRONT_CoopTrail2.Instance, currentButtons);
            CollectCustomButtons(FRONT_CoopTrail3.Instance, currentButtons);
            CollectCustomButtons(FRONT_CoopTrail4.Instance, currentButtons);

            var staleButtons = new List<Button>();
            foreach (Button button in hookedCustomButtons)
            {
                if (!currentButtons.Contains(button))
                    staleButtons.Add(button);
            }
            foreach (Button button in staleButtons)
            {
                button.MouseEnter -= CustomButtonMouseEnter;
                button.MouseLeave -= CustomButtonMouseLeave;
                hookedCustomButtons.Remove(button);
            }

            foreach (Button button in currentButtons)
            {
                if (!hookedCustomButtons.Add(button))
                    continue;
                button.MouseEnter += CustomButtonMouseEnter;
                button.MouseLeave += CustomButtonMouseLeave;
            }
        }

        private static void CollectCustomButtons(
            FrameworkElement trailView,
            HashSet<Button> result)
        {
            DependencyObject host = trailView?.FindName("CoopCustomLordSelectionHost") as DependencyObject;
            if (host != null)
                CollectCustomButtonsRecursive(host, result);
        }

        private static void CollectCustomButtonsRecursive(
            DependencyObject element,
            HashSet<Button> result)
        {
            if (element is Button button && button.DataContext is CoopLordChoice)
                result.Add(button);

            int childCount = VisualTreeHelper.GetChildrenCount(element);
            for (int index = 0; index < childCount; index++)
                CollectCustomButtonsRecursive(VisualTreeHelper.GetChild(element, index), result);
        }

        private void UnhookCustomButtons()
        {
            foreach (Button button in hookedCustomButtons)
            {
                button.MouseEnter -= CustomButtonMouseEnter;
                button.MouseLeave -= CustomButtonMouseLeave;
            }
            hookedCustomButtons.Clear();
        }

        private void CustomButtonMouseEnter(object sender, MouseEventArgs args)
        {
            if (sender is Button button && button.DataContext is CoopLordChoice choice)
                EnterCustom(choice.Lord);
        }

        private void CustomButtonMouseLeave(object sender, MouseEventArgs args)
        {
            LeaveRollover();
        }

        private void EnterCustom(CustomisationFileManager.CustomLord lord)
        {
            if (owner == null)
                return;

            owner.AILordEnter("98");
            MainViewModel viewModel = MainViewModel.Instance;
            GameAIManagerAPI api = GameAIManagerAPI.Instance;
            api.TryGetLordDetails(lord.lordName, out LordDetails details);
            string name = lord.lordDisplayName ?? lord.lordName;
            int lordPower = lord.configs != null && lord.configs.Count > 0
                ? lord.configs[0].lordData.lord_power_display_level
                : 0;
            viewModel.SkirmishLordRolloverName =
                CoopCustomLordSelectionPolicy.AppendLordPower(name, lordPower);
            viewModel.SkirmishLordRolloverName2 = string.Empty;
            viewModel.SkirmishLordRolloverDesc = details?.Description ?? string.Empty;
            viewModel.SkirmishLordRolloverRating = details?.DifficultyRating ?? string.Empty;
            viewModel.SkirmishLordRolloverTroops = details?.FavouriteTroops ?? string.Empty;
            viewModel.SkirmishLordRolloverCastle = details?.Castles ?? string.Empty;
            viewModel.SkirmishLordRolloverStyle = details?.PlayStyle ?? string.Empty;
            viewModel.SkirmishLordRolloverSaying = details?.FavouriteSaying ?? string.Empty;
            viewModel.SkirmishLordRolloverSayingOpacity =
                string.IsNullOrWhiteSpace(details?.FavouriteSaying) ? 0f : 1f;
            if (lord.image != null)
                viewModel.SkirmishLordRolloverFace = lord.image;
            viewModel.Show_AddAIPanel_Rollover = true;
        }

        private void LeaveRollover()
        {
            owner?.AILordLeave("-1");
        }

        private void ClearSelection(string reason)
        {
            if (customSelectionActive)
                Shared.DebugLogHelper.LogDebug(log, () => "Bugfixes and QoL cleared Coop custom-lord state: " + reason + ".");
            selectedLordName = string.Empty;
            selectedDisplayName = string.Empty;
            selectedPortrait = null;
            customSelectionActive = false;
        }

        private void ShowMissingLordMessage()
        {
            HUD_ConfirmationPopup.ShowConfirmationOKMessage(
                SerpLocalization.Get("BugfixesAndQoL.CoopCustomLordMissingTitle"),
                () => { },
                SerpLocalization.Get("BugfixesAndQoL.CoopCustomLordMissingMessage"));
        }

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private static Hook InstallHook<TDelegate>(MethodInfo method, TDelegate detour, out TDelegate trampoline)
            where TDelegate : Delegate
        {
            Hook hook = new Hook(method, detour);
            trampoline = hook.GenerateTrampoline<TDelegate>();
            return hook;
        }

        private static FieldInfo RequireField(Type type, string name, BindingFlags flags) =>
            type.GetField(name, flags) ?? throw new MissingFieldException(type.FullName, name);

        private static MethodInfo RequireMethod(
            Type type, string name, BindingFlags flags, params Type[] parameters) =>
            type.GetMethod(name, flags, null, parameters, null) ??
            throw new MissingMethodException(type.FullName, name);

        public sealed class CoopLordChoice
        {
            internal CoopLordChoice(
                CustomisationFileManager.CustomLord lord,
                ImageSource portrait,
                string displayName,
                float left,
                float top,
                Action select)
            {
                Lord = lord ?? throw new ArgumentNullException(nameof(lord));
                Portrait = portrait;
                DisplayName = displayName ?? string.Empty;
                Left = left;
                Top = top;
                SelectCommand = new RelayCommand(select);
            }

            public CustomisationFileManager.CustomLord Lord { get; }
            public ImageSource Portrait { get; }
            public string DisplayName { get; }
            public float Left { get; }
            public float Top { get; }
            public RelayCommand SelectCommand { get; }
        }
    }
}
