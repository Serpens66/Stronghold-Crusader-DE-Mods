using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Button = Noesis.Button;
using ListView = Noesis.ListView;
using Visibility = Noesis.Visibility;

namespace SomeSettings
{
    internal sealed class SkirmishAiSelectionMemoryHook : IDisposable
    {
        private delegate void MultiplayerButtonClickedDelegate(FRONT_Multiplayer self, string param);
        private delegate void AiSettingsInitDelegate(
            FRONT_Multiplayer_AISettings self,
            FRONT_Multiplayer.MPAIVInfo aivInfo,
            bool mpMode);
        private delegate void AiSettingsPopulateListDelegate(
            FRONT_Multiplayer_AISettings self,
            FRONT_Multiplayer.MPAIVInfo aivInfo,
            bool doPopulate);
        private delegate void AiSettingsButtonClickedDelegate(FRONT_Multiplayer_AISettings self, string param);
        private delegate void AiSettingsAddSelectedDelegate(FRONT_Multiplayer_AISettings self);

        public const int MaxCustomAivsPerLord = 999;

        private const long MaxStoreFileBytes = 256L * 1024L * 1024L;
        private const string BuiltInPrefix = "builtin:";
        private const string CustomPrefix = "custom:";
        private const string StoreFileName = "AiAivSelectionMemory.json";

        private static readonly FieldInfo AiSettingsAivInfoField =
            FindField(typeof(FRONT_Multiplayer_AISettings), "AIVInfo");
        private static readonly FieldInfo AiSettingsAivListField =
            FindField(typeof(FRONT_Multiplayer_AISettings), "aivList");
        private static readonly FieldInfo AiSettingsFileListField =
            FindField(typeof(FRONT_Multiplayer_AISettings), "RefFileLists");
        private static readonly FieldInfo AiSettingsMpModeField =
            FindField(typeof(FRONT_Multiplayer_AISettings), "MPMode");
        private static readonly MethodInfo AiSettingsPopulateListMethod =
            FindMethod(
                typeof(FRONT_Multiplayer_AISettings),
                "populateList",
                typeof(FRONT_Multiplayer.MPAIVInfo),
                typeof(bool));
        private static readonly MethodInfo MultiplayerUpdateHostInfoMethod =
            FindMethod(typeof(FRONT_Multiplayer), "UpdateHostInfo", typeof(bool));

        private readonly ManualLogSource log;
        private readonly SomeSettingsViewModel settings;
        private readonly AiAivSelectionListViewModel selectionList;
        private readonly Dictionary<string, string> storedSelections =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<FRONT_Multiplayer_AISettings, bool> actualDialogMpModes =
            new Dictionary<FRONT_Multiplayer_AISettings, bool>();
        private readonly Random random = new Random();
        private readonly string storePath;

        private readonly Hook multiplayerButtonClickedHook;
        private readonly Hook skirmishAiAddClickHook;
        private readonly Hook aiSettingsInitHook;
        private readonly Hook aiSettingsPopulateListHook;
        private readonly Hook aiSettingsButtonClickedHook;
        private readonly Hook aiSettingsAddSelectedHook;
        private readonly MultiplayerButtonClickedDelegate multiplayerButtonClickedTrampoline;
        private readonly MultiplayerButtonClickedDelegate skirmishAiAddClickTrampoline;
        private readonly AiSettingsInitDelegate aiSettingsInitTrampoline;
        private readonly AiSettingsPopulateListDelegate aiSettingsPopulateListTrampoline;
        private readonly AiSettingsButtonClickedDelegate aiSettingsButtonClickedTrampoline;
        private readonly AiSettingsAddSelectedDelegate aiSettingsAddSelectedTrampoline;
        private bool disposed;

        public SkirmishAiSelectionMemoryHook(
            ManualLogSource log,
            SomeSettingsViewModel settings,
            AiAivSelectionListViewModel selectionList)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.selectionList = selectionList ?? throw new ArgumentNullException(nameof(selectionList));
            storePath = Path.Combine(GetPluginDirectory(), "LobbyModSettings", StoreFileName);
            LoadStore();

            MethodInfo multiplayerButtonClickedMethod =
                FindMethod(typeof(FRONT_Multiplayer), "ButtonClicked", typeof(string));
            MethodInfo skirmishAiAddClickMethod =
                FindMethod(typeof(FRONT_Multiplayer), "SkirmishAIAddClick", typeof(string));
            MethodInfo aiSettingsInitMethod =
                FindMethod(
                    typeof(FRONT_Multiplayer_AISettings),
                    "Init",
                    typeof(FRONT_Multiplayer.MPAIVInfo),
                    typeof(bool));
            MethodInfo aiSettingsButtonClickedMethod =
                FindMethod(typeof(FRONT_Multiplayer_AISettings), "ButtonClicked", typeof(string));
            MethodInfo aiSettingsAddSelectedMethod =
                FindMethod(typeof(FRONT_Multiplayer_AISettings), "AddSelected");

            Hook multiplayerButtonClicked = null;
            Hook skirmishAiAddClick = null;
            Hook aiSettingsInit = null;
            Hook aiSettingsPopulateList = null;
            Hook aiSettingsButtonClicked = null;
            Hook aiSettingsAddSelected = null;
            try
            {
                multiplayerButtonClicked =
                    new Hook(multiplayerButtonClickedMethod, (MultiplayerButtonClickedDelegate)MultiplayerButtonClickedHook);
                MultiplayerButtonClickedDelegate multiplayerButtonClickedOriginal =
                    multiplayerButtonClicked.GenerateTrampoline<MultiplayerButtonClickedDelegate>();

                skirmishAiAddClick =
                    new Hook(skirmishAiAddClickMethod, (MultiplayerButtonClickedDelegate)SkirmishAiAddClickHook);
                MultiplayerButtonClickedDelegate skirmishAiAddClickOriginal =
                    skirmishAiAddClick.GenerateTrampoline<MultiplayerButtonClickedDelegate>();

                aiSettingsInit = new Hook(aiSettingsInitMethod, (AiSettingsInitDelegate)AiSettingsInitHook);
                AiSettingsInitDelegate aiSettingsInitOriginal =
                    aiSettingsInit.GenerateTrampoline<AiSettingsInitDelegate>();

                aiSettingsPopulateList =
                    new Hook(AiSettingsPopulateListMethod, (AiSettingsPopulateListDelegate)AiSettingsPopulateListHook);
                AiSettingsPopulateListDelegate aiSettingsPopulateListOriginal =
                    aiSettingsPopulateList.GenerateTrampoline<AiSettingsPopulateListDelegate>();

                aiSettingsButtonClicked =
                    new Hook(aiSettingsButtonClickedMethod, (AiSettingsButtonClickedDelegate)AiSettingsButtonClickedHook);
                AiSettingsButtonClickedDelegate aiSettingsButtonClickedOriginal =
                    aiSettingsButtonClicked.GenerateTrampoline<AiSettingsButtonClickedDelegate>();

                aiSettingsAddSelected =
                    new Hook(aiSettingsAddSelectedMethod, (AiSettingsAddSelectedDelegate)AiSettingsAddSelectedHook);
                AiSettingsAddSelectedDelegate aiSettingsAddSelectedOriginal =
                    aiSettingsAddSelected.GenerateTrampoline<AiSettingsAddSelectedDelegate>();

                multiplayerButtonClickedHook = multiplayerButtonClicked;
                multiplayerButtonClickedTrampoline = multiplayerButtonClickedOriginal;
                skirmishAiAddClickHook = skirmishAiAddClick;
                skirmishAiAddClickTrampoline = skirmishAiAddClickOriginal;
                aiSettingsInitHook = aiSettingsInit;
                aiSettingsInitTrampoline = aiSettingsInitOriginal;
                aiSettingsPopulateListHook = aiSettingsPopulateList;
                aiSettingsPopulateListTrampoline = aiSettingsPopulateListOriginal;
                aiSettingsButtonClickedHook = aiSettingsButtonClicked;
                aiSettingsButtonClickedTrampoline = aiSettingsButtonClickedOriginal;
                aiSettingsAddSelectedHook = aiSettingsAddSelected;
                aiSettingsAddSelectedTrampoline = aiSettingsAddSelectedOriginal;
                selectionList.RemoveRequested += OnRemoveRequested;
            }
            catch
            {
                aiSettingsAddSelected?.Dispose();
                aiSettingsButtonClicked?.Dispose();
                aiSettingsPopulateList?.Dispose();
                aiSettingsInit?.Dispose();
                skirmishAiAddClick?.Dispose();
                multiplayerButtonClicked?.Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                () =>
                    $"SomeSettings AI selection hooks installed. extensionEnabled={IsDialogExtensionActive()}, " +
                    $"memoryEnabled={IsMemoryActive()}, maximumAivs={MaxCustomAivsPerLord}, storePath={storePath}");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            selectionList.RemoveRequested -= OnRemoveRequested;
            multiplayerButtonClickedHook?.Undo();
            skirmishAiAddClickHook?.Undo();
            aiSettingsInitHook?.Undo();
            aiSettingsPopulateListHook?.Undo();
            aiSettingsButtonClickedHook?.Undo();
            aiSettingsAddSelectedHook?.Undo();
            multiplayerButtonClickedHook?.Dispose();
            skirmishAiAddClickHook?.Dispose();
            aiSettingsInitHook?.Dispose();
            aiSettingsPopulateListHook?.Dispose();
            aiSettingsButtonClickedHook?.Dispose();
            aiSettingsAddSelectedHook?.Dispose();
            actualDialogMpModes.Clear();
            Shared.DebugLogHelper.LogDebug(log, "SomeSettings AI selection hooks disposed.");
        }

        private void MultiplayerButtonClickedHook(FRONT_Multiplayer self, string param)
        {
            bool memoryActiveBefore = IsMemoryActive();
            bool mayAddAi = string.Equals(param, "AddCustomLord", StringComparison.Ordinal);
            Dictionary<int, string> before =
                memoryActiveBefore && mayAddAi ? CaptureAiSlotKeys(self) : null;

            if (memoryActiveBefore && string.Equals(param, "CancelAISettings", StringComparison.Ordinal))
                SaveActiveAiSettings();

            List<NetworkAivSnapshot> networkSnapshots = null;
            if (IsDialogExtensionActive() && string.Equals(param, "Play", StringComparison.Ordinal))
            {
                EnforceAllRuntimeLimits(self);
                if (IsMemoryActive())
                    SaveAllAiSettings(self);

                if (IsNetworkHost(self))
                    networkSnapshots = SelectNetworkStartAivs(self);
            }

            try
            {
                multiplayerButtonClickedTrampoline(self, param);
            }
            finally
            {
                RestoreNetworkStartAivs(networkSnapshots);
            }

            bool memoryActiveAfter = IsMemoryActive();
            if (!memoryActiveAfter || !mayAddAi)
                return;

            try
            {
                if (ApplyStoredSelectionsToNewAiSlots(self, before, "after ButtonClicked " + param))
                    UpdateHostInfo(self);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"SomeSettings AI selection apply after ButtonClicked({param}) failed: {ex}");
            }
        }

        private void SkirmishAiAddClickHook(FRONT_Multiplayer self, string param)
        {
            bool memoryActiveBefore = IsMemoryActive();
            Dictionary<int, string> before = memoryActiveBefore ? CaptureAiSlotKeys(self) : null;

            skirmishAiAddClickTrampoline(self, param);

            bool memoryActiveAfter = IsMemoryActive();
            if (!memoryActiveAfter)
                return;

            try
            {
                if (ApplyStoredSelectionsToNewAiSlots(self, before, "after SkirmishAIAddClick " + param))
                    UpdateHostInfo(self);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"SomeSettings AI selection apply after SkirmishAIAddClick({param}) failed: {ex}");
            }
        }

        private void AiSettingsInitHook(
            FRONT_Multiplayer_AISettings self,
            FRONT_Multiplayer.MPAIVInfo aivInfo,
            bool mpMode)
        {
            actualDialogMpModes[self] = mpMode;
            bool extensionEnabled = IsDialogExtensionActive();
            if (extensionEnabled)
                EnforceRuntimeLimit(aivInfo, "dialog initialization");

            aiSettingsInitTrampoline(self, aivInfo, extensionEnabled ? false : mpMode);
            bool addButtonVisible = UpdateAddButtonVisibility(self);
            Shared.DebugLogHelper.LogDebug(
                log,
                () =>
                    $"SomeSettings AI settings dialog opened: extensionEnabled={extensionEnabled}, " +
                    $"memoryEnabled={IsMemoryActive()}, actualMultiplayerMode={mpMode}, " +
                    $"aivCount={aivInfo?.aivs?.Count ?? 0}, maximumAivs={MaxCustomAivsPerLord}, " +
                    $"addButtonVisible={addButtonVisible}.");
        }

        private void AiSettingsPopulateListHook(
            FRONT_Multiplayer_AISettings self,
            FRONT_Multiplayer.MPAIVInfo aivInfo,
            bool doPopulate)
        {
            SetEffectiveDialogMode(self);
            aiSettingsPopulateListTrampoline(self, aivInfo, doPopulate);
            RefreshSelectionList(self);
        }

        private bool UpdateAddButtonVisibility(FRONT_Multiplayer_AISettings instance)
        {
            Button addButton = instance?.FindName("MP_Add") as Button;
            if (addButton == null)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "SomeSettings could not find the AIV Add button named MP_Add.");
                return false;
            }

            bool visible = IsDialogExtensionActive() || !GetActualDialogMpMode(instance);
            addButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            return visible;
        }

        private void AiSettingsButtonClickedHook(FRONT_Multiplayer_AISettings self, string param)
        {
            SetEffectiveDialogMode(self);
            aiSettingsButtonClickedTrampoline(self, param);

            if (!IsMemoryActive() || !IsAiSettingsMutation(param))
                return;

            try
            {
                SaveAiSettings(GetAivInfo(self));
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"SomeSettings AI selection save failed after ButtonClicked({param}): {ex}");
            }
        }

        private void AiSettingsAddSelectedHook(FRONT_Multiplayer_AISettings self)
        {
            SetEffectiveDialogMode(self);
            if (!IsDialogExtensionActive())
            {
                aiSettingsAddSelectedTrampoline(self);
                return;
            }

            try
            {
                AddSelectedAivs(self);
                RefreshSelectionList(self);
                SaveAiSettings(GetAivInfo(self));
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"SomeSettings extended AIV add failed: {ex}");
            }
        }

        private void AddSelectedAivs(FRONT_Multiplayer_AISettings self)
        {
            FRONT_Multiplayer.MPAIVInfo info = GetAivInfo(self);
            List<CustomisationFileManager.CustomAIV> availableAivs = GetAvailableAivs(self);
            ListView fileList = GetFileList(self);
            if (info?.aivs == null || availableAivs == null || fileList == null || fileList.SelectedItem == null)
                return;

            List<int> selectedIndexes = new List<int>();
            if (fileList.SelectedItems != null && fileList.SelectedItems.Count > 1)
            {
                foreach (object selectedItem in (IEnumerable)fileList.SelectedItems)
                {
                    int selectedIndex = fileList.Items.IndexOf(selectedItem);
                    if (selectedIndex >= 0)
                        selectedIndexes.Add(selectedIndex);
                }
            }
            else if (fileList.SelectedIndex >= 0)
            {
                selectedIndexes.Add(fileList.SelectedIndex);
            }

            bool limitReached = false;
            HashSet<ulong> checksums = new HashSet<ulong>();
            foreach (CustomisationFileManager.CustomAIV existing in info.aivs)
            {
                if (existing != null)
                    checksums.Add(existing.checksum);
            }

            foreach (int selectedIndex in selectedIndexes)
            {
                if (selectedIndex < 0 || selectedIndex >= availableAivs.Count)
                    continue;

                CustomisationFileManager.CustomAIV candidate = availableAivs[selectedIndex];
                if (candidate == null || checksums.Contains(candidate.checksum))
                    continue;

                if (info.aivs.Count >= MaxCustomAivsPerLord)
                {
                    limitReached = true;
                    break;
                }

                info.aivs.Add(candidate);
                checksums.Add(candidate.checksum);
            }

            if (limitReached)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"SomeSettings AIV list limit reached for {BuildLordKey(info)}: " +
                    $"{info.aivs.Count}/{MaxCustomAivsPerLord}. Additional selections were ignored.");
            }
        }

        private void OnRemoveRequested(CustomisationFileManager.CustomAIV requestedAiv)
        {
            if (requestedAiv == null)
                return;

            try
            {
                FRONT_Multiplayer_AISettings instance = FRONT_Multiplayer_AISettings.Instance;
                if (!IsDialogExtensionActive() && GetActualDialogMpMode(instance))
                    return;

                FRONT_Multiplayer.MPAIVInfo info = GetAivInfo(instance);
                if (info?.aivs == null || !IsCustomAivMode(info))
                    return;

                int index = info.aivs.IndexOf(requestedAiv);
                if (index < 0)
                    index = info.aivs.FindIndex(aiv => aiv != null && aiv.checksum == requestedAiv.checksum);
                if (index < 0)
                    return;

                info.aivs.RemoveAt(index);
                RefreshSelectionList(instance);
                SaveAiSettings(info);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"SomeSettings AIV row removal failed: {ex}");
            }
        }

        private void RefreshSelectionList(FRONT_Multiplayer_AISettings instance)
        {
            FRONT_Multiplayer.MPAIVInfo info = GetAivInfo(instance);
            bool actualMpMode = GetActualDialogMpMode(instance);
            bool allowRemoval =
                IsCustomAivMode(info) && (IsDialogExtensionActive() || !actualMpMode);
            selectionList.Refresh(info, allowRemoval);
        }

        private void SetEffectiveDialogMode(FRONT_Multiplayer_AISettings instance)
        {
            if (instance == null)
                return;

            bool effectiveMpMode =
                IsDialogExtensionActive() ? false : GetActualDialogMpMode(instance);
            AiSettingsMpModeField.SetValue(instance, effectiveMpMode);
        }

        private bool GetActualDialogMpMode(FRONT_Multiplayer_AISettings instance)
        {
            return instance != null &&
                   actualDialogMpModes.TryGetValue(instance, out bool mpMode) &&
                   mpMode;
        }

        private bool IsDialogExtensionActive()
        {
            return settings.EnableMod;
        }

        private bool IsMemoryActive()
        {
            return settings.EnableMod && settings.RememberAiAivSettings;
        }

        private static bool IsCustomAivMode(FRONT_Multiplayer.MPAIVInfo info)
        {
            return info != null && !info.builtIn && !info.community && !info.historical;
        }

        private static bool IsNetworkHost(FRONT_Multiplayer parent)
        {
            return !FRONT_Multiplayer.skirmishGame &&
                   parent?.currentLobby != null &&
                   parent.currentLobby.isHost;
        }

        private List<NetworkAivSnapshot> SelectNetworkStartAivs(FRONT_Multiplayer parent)
        {
            List<NetworkAivSnapshot> snapshots = new List<NetworkAivSnapshot>();
            if (parent?.currentLobby?.members == null || parent.AIVs == null)
                return snapshots;

            foreach (Platform_Multiplayer.MPLobbyMember member in parent.currentLobby.members)
            {
                if (!TryGetAiSlotInfo(parent, member, out int playerId, out string key))
                    continue;
                if (playerId < 1 || playerId > parent.AIVs.Length)
                    continue;

                FRONT_Multiplayer.MPAIVInfo info = parent.AIVs[playerId - 1];
                if (!IsCustomAivMode(info) || info.aivs == null || info.aivs.Count <= 1)
                    continue;

                List<CustomisationFileManager.CustomAIV> fullList =
                    new List<CustomisationFileManager.CustomAIV>(info.aivs);
                int selectedIndex = random.Next(fullList.Count);
                CustomisationFileManager.CustomAIV selected = fullList[selectedIndex];
                snapshots.Add(new NetworkAivSnapshot(info, fullList));
                info.aivs.Clear();
                info.aivs.Add(selected);

                Shared.DebugLogHelper.LogDebug(
                    log,
                    () =>
                        $"SomeSettings selected network-start AIV: lord={key}, player={playerId}, " +
                        $"candidateCount={fullList.Count}, selectedIndex={selectedIndex}, " +
                        $"selectedName={selected.AIVName}, selectedChecksum={selected.checksum}.");
            }

            return snapshots;
        }

        private static void RestoreNetworkStartAivs(List<NetworkAivSnapshot> snapshots)
        {
            if (snapshots == null)
                return;

            foreach (NetworkAivSnapshot snapshot in snapshots)
            {
                snapshot.Info.aivs.Clear();
                snapshot.Info.aivs.AddRange(snapshot.FullList);
            }
        }

        private void EnforceAllRuntimeLimits(FRONT_Multiplayer parent)
        {
            if (parent?.AIVs == null)
                return;

            for (int i = 0; i < parent.AIVs.Length; i++)
                EnforceRuntimeLimit(parent.AIVs[i], $"player slot {i + 1} before game start");
        }

        private void EnforceRuntimeLimit(FRONT_Multiplayer.MPAIVInfo info, string reason)
        {
            if (info?.aivs == null || info.aivs.Count <= MaxCustomAivsPerLord)
                return;

            int removed = info.aivs.Count - MaxCustomAivsPerLord;
            info.aivs.RemoveRange(MaxCustomAivsPerLord, removed);
            Shared.DebugLogHelper.LogWarning(
                log,
                $"SomeSettings trimmed an unsafe AIV list: lord={BuildLordKey(info)}, reason={reason}, " +
                $"removed={removed}, maximum={MaxCustomAivsPerLord}.");
        }

        private void SaveActiveAiSettings()
        {
            try
            {
                FRONT_Multiplayer_AISettings instance = FRONT_Multiplayer_AISettings.Instance;
                SaveAiSettings(GetAivInfo(instance));
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"SomeSettings AI selection save failed while closing settings: {ex}");
            }
        }

        private void SaveAllAiSettings(FRONT_Multiplayer parent)
        {
            if (parent?.currentLobby?.members == null || parent.AIVs == null)
                return;

            foreach (Platform_Multiplayer.MPLobbyMember member in parent.currentLobby.members)
            {
                if (!TryGetAiSlotInfo(parent, member, out int playerId, out _))
                    continue;
                if (playerId < 1 || playerId > parent.AIVs.Length)
                    continue;

                SaveAiSettings(parent.AIVs[playerId - 1]);
            }
        }

        private void SaveAiSettings(FRONT_Multiplayer.MPAIVInfo info)
        {
            if (!IsMemoryActive() || info == null)
                return;

            string key = BuildLordKey(info);
            if (string.IsNullOrEmpty(key))
                return;

            EnforceRuntimeLimit(info, "saving selection");
            string encoded = AiAivSelectionCodec.Encode(info);
            bool hadExisting = storedSelections.TryGetValue(key, out string existing);
            if (hadExisting && string.Equals(existing, encoded, StringComparison.Ordinal))
                return;

            storedSelections[key] = encoded;
            SaveStore();
            Shared.DebugLogHelper.LogDebug(
                log,
                () =>
                    $"SomeSettings saved AI AIV/AIC selection: key={key}, hadExisting={hadExisting}, " +
                    $"{BuildInfoSummary(info)}, encodedLength={encoded.Length}.");
        }

        private bool ApplyStoredSelectionsToNewAiSlots(
            FRONT_Multiplayer parent,
            Dictionary<int, string> before,
            string reason)
        {
            if (storedSelections.Count == 0 ||
                parent?.AIVs == null ||
                parent.currentLobby?.members == null ||
                before == null)
            {
                return false;
            }

            bool applied = false;
            foreach (Platform_Multiplayer.MPLobbyMember member in parent.currentLobby.members)
            {
                if (!TryGetAiSlotInfo(parent, member, out int playerId, out string key))
                    continue;
                if (playerId < 1 || playerId > parent.AIVs.Length)
                    continue;
                if (before.TryGetValue(playerId, out string previousKey) &&
                    string.Equals(previousKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!storedSelections.TryGetValue(key, out string encoded))
                    continue;
                if (!AiAivSelectionCodec.TryDecode(
                        encoded,
                        out FRONT_Multiplayer.MPAIVInfo decoded,
                        out string decodeError))
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"SomeSettings ignored invalid stored AI selection for {key}: {decodeError}");
                    continue;
                }
                if (!string.Equals(BuildLordKey(decoded), key, StringComparison.OrdinalIgnoreCase))
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"SomeSettings ignored stored AI selection for {key}: decoded lord is {BuildLordKey(decoded)}.");
                    continue;
                }

                AiAivSelectionCodec.CopyInto(decoded, parent.AIVs[playerId - 1]);
                applied = true;
                Shared.DebugLogHelper.LogDebug(
                    log,
                    () =>
                        $"SomeSettings restored shared AI AIV/AIC selection: reason={reason}, key={key}, " +
                        $"player={playerId}, {BuildInfoSummary(decoded)}.");
            }

            return applied;
        }

        private Dictionary<int, string> CaptureAiSlotKeys(FRONT_Multiplayer parent)
        {
            Dictionary<int, string> result = new Dictionary<int, string>();
            if (parent?.currentLobby?.members == null)
                return result;

            foreach (Platform_Multiplayer.MPLobbyMember member in parent.currentLobby.members)
            {
                if (TryGetAiSlotInfo(parent, member, out int playerId, out string key))
                    result[playerId] = key;
            }

            return result;
        }

        private static bool IsAiSettingsMutation(string param)
        {
            if (string.IsNullOrEmpty(param))
                return false;
            if (param.StartsWith("Kick_", StringComparison.Ordinal))
                return true;

            switch (param)
            {
                case "Default":
                case "Community":
                case "Historical":
                case "NoRot":
                case "North":
                case "East":
                case "South":
                case "West":
                case "User":
                case "Clear_Selected":
                case "LordDefault":
                case "LordUser":
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryGetAiSlotInfo(
            FRONT_Multiplayer parent,
            Platform_Multiplayer.MPLobbyMember member,
            out int playerId,
            out string lordKey)
        {
            playerId = 0;
            lordKey = string.Empty;
            if (parent?.currentLobby == null ||
                member == null ||
                !member.SkirmishMember ||
                member.SkirmishHumanMember)
            {
                return false;
            }

            playerId = parent.currentLobby.getThisPlayerFromSteamID(member.GetSteamID());
            if (playerId < 1 || playerId > 8)
                return false;

            lordKey = !string.IsNullOrEmpty(member.customLordName)
                ? CustomPrefix + member.customLordName
                : BuiltInPrefix + member.GetLordType();
            return true;
        }

        private static FRONT_Multiplayer.MPAIVInfo GetAivInfo(FRONT_Multiplayer_AISettings instance)
        {
            return instance == null
                ? null
                : AiSettingsAivInfoField.GetValue(instance) as FRONT_Multiplayer.MPAIVInfo;
        }

        private static List<CustomisationFileManager.CustomAIV> GetAvailableAivs(
            FRONT_Multiplayer_AISettings instance)
        {
            return instance == null
                ? null
                : AiSettingsAivListField.GetValue(instance) as List<CustomisationFileManager.CustomAIV>;
        }

        private static ListView GetFileList(FRONT_Multiplayer_AISettings instance)
        {
            return instance == null ? null : AiSettingsFileListField.GetValue(instance) as ListView;
        }

        private static void UpdateHostInfo(FRONT_Multiplayer parent)
        {
            if (parent != null)
                MultiplayerUpdateHostInfoMethod.Invoke(parent, new object[] { false });
        }

        private static string BuildLordKey(FRONT_Multiplayer.MPAIVInfo info)
        {
            if (info == null)
                return string.Empty;

            return !string.IsNullOrEmpty(info.lordName)
                ? CustomPrefix + info.lordName
                : BuiltInPrefix + info.lordType;
        }

        private static string BuildInfoSummary(FRONT_Multiplayer.MPAIVInfo info)
        {
            if (info == null)
                return "info=null";

            string mode =
                info.builtIn ? "default" :
                info.community ? "community" :
                info.historical ? "historical" :
                "custom";
            string lordMode = info.builtInLord ? "defaultLord" : "customLordConfig";
            string firstAivName =
                info.aivs == null || info.aivs.Count == 0 ? string.Empty : info.aivs[0].AIVName;

            return
                $"lordType={info.lordType}, lordName={info.lordName ?? string.Empty}, mode={mode}, " +
                $"rotation={info.rotation}, aivCount={info.aivs?.Count ?? 0}, firstAiv={firstAivName}, " +
                $"lordMode={lordMode}, lordConfig={info.lordConfig?.name ?? string.Empty}";
        }

        private void LoadStore()
        {
            storedSelections.Clear();
            if (!File.Exists(storePath))
            {
                Shared.DebugLogHelper.LogDebug(
                    log,
                    () => $"SomeSettings shared AI AIV/AIC selection file not found: {storePath}");
                return;
            }

            try
            {
                FileInfo fileInfo = new FileInfo(storePath);
                if (fileInfo.Length > MaxStoreFileBytes)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"SomeSettings ignored oversized AI selection store: path={storePath}, " +
                        $"size={fileInfo.Length}, maximum={MaxStoreFileBytes}.");
                    return;
                }

                Dictionary<string, string> parsed = ParseJsonObject(File.ReadAllText(storePath));
                foreach (KeyValuePair<string, string> entry in parsed)
                {
                    if (!entry.Value.StartsWith("v2:", StringComparison.Ordinal))
                    {
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            $"SomeSettings ignored unsupported AI selection entry: key={entry.Key}.");
                        continue;
                    }

                    storedSelections[entry.Key] = entry.Value;
                }

                Shared.DebugLogHelper.LogDebug(
                    log,
                    () =>
                        $"SomeSettings loaded {storedSelections.Count} shared AI AIV/AIC selections from {storePath}.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"SomeSettings could not load AI selection store from {storePath}: {ex.Message}");
            }
        }

        private void SaveStore()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(storePath));
                File.WriteAllText(
                    storePath,
                    SerializeJsonObject(storedSelections),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"SomeSettings could not save AI selection store to {storePath}: {ex}");
            }
        }

        private static string GetPluginDirectory()
        {
            try
            {
                string assemblyLocation = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(assemblyLocation))
                    return Path.GetDirectoryName(assemblyLocation);
            }
            catch
            {
            }

            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static MethodInfo FindMethod(Type type, string methodName, params Type[] parameterTypes)
        {
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);
            if (method == null)
                throw new MissingMethodException(type.FullName, methodName);

            return method;
        }

        private static FieldInfo FindField(Type type, string fieldName)
        {
            FieldInfo field = type.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
                throw new MissingFieldException(type.FullName, fieldName);

            return field;
        }

        private static string SerializeJsonObject(Dictionary<string, string> values)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("{");
            bool first = true;
            foreach (KeyValuePair<string, string> entry in values)
            {
                if (!first)
                    builder.AppendLine(",");

                first = false;
                builder.Append("  \"");
                AppendEscapedJsonString(builder, entry.Key);
                builder.Append("\": \"");
                AppendEscapedJsonString(builder, entry.Value);
                builder.Append('"');
            }

            if (!first)
                builder.AppendLine();

            builder.AppendLine("}");
            return builder.ToString();
        }

        private static Dictionary<string, string> ParseJsonObject(string json)
        {
            Dictionary<string, string> result =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int index = 0;
            SkipWhitespace(json, ref index);
            Expect(json, ref index, '{');
            SkipWhitespace(json, ref index);
            if (TryConsume(json, ref index, '}'))
                return result;

            while (index < json.Length)
            {
                string key = ReadJsonString(json, ref index);
                SkipWhitespace(json, ref index);
                Expect(json, ref index, ':');
                SkipWhitespace(json, ref index);
                result[key] = ReadJsonString(json, ref index);
                SkipWhitespace(json, ref index);
                if (TryConsume(json, ref index, '}'))
                    return result;

                Expect(json, ref index, ',');
                SkipWhitespace(json, ref index);
            }

            throw new FormatException("Unterminated JSON object.");
        }

        private static void AppendEscapedJsonString(StringBuilder builder, string value)
        {
            foreach (char c in value ?? string.Empty)
            {
                switch (c)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '"': builder.Append("\\\""); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < 32)
                            builder.Append("\\u" + ((int)c).ToString("x4"));
                        else
                            builder.Append(c);
                        break;
                }
            }
        }

        private static string ReadJsonString(string text, ref int index)
        {
            Expect(text, ref index, '"');
            StringBuilder builder = new StringBuilder();
            while (index < text.Length)
            {
                char c = text[index++];
                if (c == '"')
                    return builder.ToString();
                if (c != '\\')
                {
                    builder.Append(c);
                    continue;
                }
                if (index >= text.Length)
                    throw new FormatException("Unterminated JSON escape.");

                char escaped = text[index++];
                switch (escaped)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (index + 4 > text.Length)
                            throw new FormatException("Invalid JSON unicode escape.");
                        builder.Append((char)Convert.ToInt32(text.Substring(index, 4), 16));
                        index += 4;
                        break;
                    default:
                        throw new FormatException($"Invalid JSON escape '\\{escaped}'.");
                }
            }

            throw new FormatException("Unterminated JSON string.");
        }

        private static void SkipWhitespace(string text, ref int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;
        }

        private static bool TryConsume(string text, ref int index, char expected)
        {
            if (index >= text.Length || text[index] != expected)
                return false;

            index++;
            return true;
        }

        private static void Expect(string text, ref int index, char expected)
        {
            if (index >= text.Length || text[index] != expected)
                throw new FormatException($"Expected '{expected}' at position {index}.");

            index++;
        }

        private sealed class NetworkAivSnapshot
        {
            public NetworkAivSnapshot(
                FRONT_Multiplayer.MPAIVInfo info,
                List<CustomisationFileManager.CustomAIV> fullList)
            {
                Info = info;
                FullList = fullList;
            }

            public FRONT_Multiplayer.MPAIVInfo Info { get; }
            public List<CustomisationFileManager.CustomAIV> FullList { get; }
        }
    }
}
