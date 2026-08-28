// Feature: Remember AI selections and filter random opponents by lord source.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;

namespace BugfixesAndQoL
{
    internal sealed class SkirmishAiSelectionMemoryHook : INotifyPropertyChanged, IDisposable
    {
        internal const int MaxStoredAivEntriesPerLord = 50;

        private delegate void MultiplayerButtonClickedDelegate(FRONT_Multiplayer self, string param);
        private delegate void MultiplayerUpdateRandomAiButtonsDelegate(FRONT_Multiplayer self);
        private delegate void ShowSkirmishRandomAiSetterDelegate(MainViewModel self, bool value);
        private delegate void AiSettingsButtonClickedDelegate(FRONT_Multiplayer_AISettings self, string param);
        private delegate void AiSettingsAddSelectedDelegate(FRONT_Multiplayer_AISettings self);

        // The store normally stays tiny; reject implausibly large files before building a JSON DOM.
        private const long MaxStoreFileBytes = 16L * 1024L * 1024L;
        private const string BuiltInPrefix = "builtin:";
        private const string CustomPrefix = "custom:";
        private const string StoreFileName = "AiAivSelectionMemory.json";

        private static readonly FieldInfo AiSettingsAivInfoField =
            FindField(typeof(FRONT_Multiplayer_AISettings), "AIVInfo");
        private static readonly FieldInfo MultiplayerPlayerCapField =
            FindField(typeof(FRONT_Multiplayer), "PlayerCap");
        private static readonly FieldInfo MultiplayerSelectedMpHeaderField =
            FindField(typeof(FRONT_Multiplayer), "selectedMPHeader");
        private static readonly FieldInfo MultiplayerPlayKickSpeechField =
            FindField(typeof(FRONT_Multiplayer), "playKickSpeech");
        private static readonly FieldInfo FileHeaderMaxPlayersField =
            FindField(typeof(FileHeader), "maxPlayers");
        private static readonly FieldInfo[] MultiplayerRandomAiButtonFields =
        {
            FindField(typeof(FRONT_Multiplayer), "RefRandomAI1"),
            FindField(typeof(FRONT_Multiplayer), "RefRandomAI2"),
            FindField(typeof(FRONT_Multiplayer), "RefRandomAI3"),
            FindField(typeof(FRONT_Multiplayer), "RefRandomAI4"),
            FindField(typeof(FRONT_Multiplayer), "RefRandomAI5"),
            FindField(typeof(FRONT_Multiplayer), "RefRandomAI6"),
            FindField(typeof(FRONT_Multiplayer), "RefRandomAI7"),
        };
        private static readonly MethodInfo MultiplayerUpdateHostInfoMethod =
            FindMethod(typeof(FRONT_Multiplayer), "UpdateHostInfo", typeof(bool));
        private static readonly MethodInfo MultiplayerUpdateSteamMappingsMethod =
            FindMethod(typeof(FRONT_Multiplayer), "updateSteamIDMappings");
        private static readonly MethodInfo MultiplayerReSortTeamInfoMethod =
            FindMethod(typeof(FRONT_Multiplayer), "ReSortTeamInfo");
        private static readonly MethodInfo MultiplayerCreateTeamShieldsMethod =
            FindMethod(typeof(FRONT_Multiplayer), "CreateTeamShields");
        private static readonly MethodInfo MultiplayerUpdateRadarShieldPositionsMethod =
            FindMethod(typeof(FRONT_Multiplayer), "UpdateRadarShieldPositions");
        private static readonly MethodInfo MultiplayerUpdateRandomAiButtonsMethod =
            FindMethod(typeof(FRONT_Multiplayer), "UpdateRandomAIButtons");

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly Dictionary<string, string> storedSelections =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly string storePath;

        private readonly Hook multiplayerButtonClickedHook;
        private readonly Hook skirmishAiAddClickHook;
        private readonly Hook multiplayerUpdateRandomAiButtonsHook;
        private readonly Hook showSkirmishRandomAiSetterHook;
        private readonly Hook aiSettingsButtonClickedHook;
        private readonly Hook aiSettingsAddSelectedHook;
        private readonly MultiplayerButtonClickedDelegate multiplayerButtonClickedTrampoline;
        private readonly MultiplayerButtonClickedDelegate skirmishAiAddClickTrampoline;
        private readonly MultiplayerUpdateRandomAiButtonsDelegate multiplayerUpdateRandomAiButtonsTrampoline;
        private readonly ShowSkirmishRandomAiSetterDelegate showSkirmishRandomAiSetterTrampoline;
        private readonly AiSettingsButtonClickedDelegate aiSettingsButtonClickedTrampoline;
        private readonly AiSettingsAddSelectedDelegate aiSettingsAddSelectedTrampoline;
        private bool includeBuiltInLords = true;
        private bool includeLocalCustomLords = true;
        private bool includeWorkshopCustomLords = true;
        private bool emptyRandomLordSelectionLogged;
        private bool disposed;

        public SkirmishAiSelectionMemoryHook(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            storePath = Path.Combine(GetPluginDirectory(), "LobbyModSettings", StoreFileName);
            LoadStore();

            // Both random-lord panels share these properties and therefore stay synchronized.
            GameXAMLManagerAPI.Instance.RegisterBinding("RandomLordFiltersSimple", this);
            GameXAMLManagerAPI.Instance.RegisterBinding("RandomLordFiltersAdvanced", this);

            MethodInfo multiplayerButtonClickedMethod =
                FindMethod(typeof(FRONT_Multiplayer), "ButtonClicked", typeof(string));
            MethodInfo skirmishAiAddClickMethod =
                FindMethod(typeof(FRONT_Multiplayer), "SkirmishAIAddClick", typeof(string));
            MethodInfo updateRandomAiButtonsMethod =
                FindMethod(typeof(FRONT_Multiplayer), "UpdateRandomAIButtons");
            MethodInfo showSkirmishRandomAiSetterMethod =
                typeof(MainViewModel).GetProperty(nameof(MainViewModel.Show_SkirmishRandomAI))?.GetSetMethod();
            if (showSkirmishRandomAiSetterMethod == null)
                throw new MissingMethodException(typeof(MainViewModel).FullName, "set_Show_SkirmishRandomAI");
            MethodInfo aiSettingsButtonClickedMethod =
                FindMethod(typeof(FRONT_Multiplayer_AISettings), "ButtonClicked", typeof(string));
            MethodInfo aiSettingsAddSelectedMethod =
                FindMethod(typeof(FRONT_Multiplayer_AISettings), "AddSelected");

            Hook multiplayerButtonClicked = null;
            Hook skirmishAiAddClick = null;
            Hook updateRandomAiButtons = null;
            Hook showSkirmishRandomAiSetter = null;
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

                updateRandomAiButtons =
                    new Hook(updateRandomAiButtonsMethod,
                        (MultiplayerUpdateRandomAiButtonsDelegate)UpdateRandomAiButtonsHook);
                MultiplayerUpdateRandomAiButtonsDelegate updateRandomAiButtonsOriginal =
                    updateRandomAiButtons.GenerateTrampoline<MultiplayerUpdateRandomAiButtonsDelegate>();

                showSkirmishRandomAiSetter =
                    new Hook(showSkirmishRandomAiSetterMethod,
                        (ShowSkirmishRandomAiSetterDelegate)ShowSkirmishRandomAiSetterHook);
                ShowSkirmishRandomAiSetterDelegate showSkirmishRandomAiSetterOriginal =
                    showSkirmishRandomAiSetter.GenerateTrampoline<ShowSkirmishRandomAiSetterDelegate>();

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
                multiplayerUpdateRandomAiButtonsHook = updateRandomAiButtons;
                multiplayerUpdateRandomAiButtonsTrampoline = updateRandomAiButtonsOriginal;
                showSkirmishRandomAiSetterHook = showSkirmishRandomAiSetter;
                showSkirmishRandomAiSetterTrampoline = showSkirmishRandomAiSetterOriginal;
                aiSettingsButtonClickedHook = aiSettingsButtonClicked;
                aiSettingsButtonClickedTrampoline = aiSettingsButtonClickedOriginal;
                aiSettingsAddSelectedHook = aiSettingsAddSelected;
                aiSettingsAddSelectedTrampoline = aiSettingsAddSelectedOriginal;
            }
            catch
            {
                aiSettingsAddSelected?.Dispose();
                aiSettingsButtonClicked?.Dispose();
                showSkirmishRandomAiSetter?.Dispose();
                updateRandomAiButtons?.Dispose();
                skirmishAiAddClick?.Dispose();
                multiplayerButtonClicked?.Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                () =>
                    $"Bugfixes and QoL AI selection memory hooks installed. memoryEnabled={IsMemoryActive()}, " +
                    $"storePath={storePath}");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public Noesis.Visibility RandomLordFiltersVisibility =>
            settings.EnableMod ? Noesis.Visibility.Visible : Noesis.Visibility.Collapsed;

        public string BuiltInLordFilterHelp =>
            SerpLocalization.Get("BugfixesAndQoL.RandomLordBuiltInHelp");

        public string LocalCustomLordFilterHelp =>
            SerpLocalization.Get("BugfixesAndQoL.RandomLordLocalHelp");

        public string WorkshopCustomLordFilterHelp =>
            SerpLocalization.Get("BugfixesAndQoL.RandomLordWorkshopHelp");

        public bool IncludeBuiltInLords
        {
            get => includeBuiltInLords;
            set => SetFilter(ref includeBuiltInLords, value, nameof(IncludeBuiltInLords));
        }

        public bool IncludeLocalCustomLords
        {
            get => includeLocalCustomLords;
            set => SetFilter(ref includeLocalCustomLords, value, nameof(IncludeLocalCustomLords));
        }

        public bool IncludeWorkshopCustomLords
        {
            get => includeWorkshopCustomLords;
            set => SetFilter(ref includeWorkshopCustomLords, value, nameof(IncludeWorkshopCustomLords));
        }

        public void ApplySetting()
        {
            OnPropertyChanged(nameof(RandomLordFiltersVisibility));
            if (settings.EnableMod)
                EnsureRandomAiButtonVisibleInEditableLobby();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            multiplayerButtonClickedHook?.Undo();
            skirmishAiAddClickHook?.Undo();
            multiplayerUpdateRandomAiButtonsHook?.Undo();
            showSkirmishRandomAiSetterHook?.Undo();
            aiSettingsButtonClickedHook?.Undo();
            aiSettingsAddSelectedHook?.Undo();
            multiplayerButtonClickedHook?.Dispose();
            skirmishAiAddClickHook?.Dispose();
            multiplayerUpdateRandomAiButtonsHook?.Dispose();
            showSkirmishRandomAiSetterHook?.Dispose();
            aiSettingsButtonClickedHook?.Dispose();
            aiSettingsAddSelectedHook?.Dispose();
            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL AI selection memory hooks disposed.");
        }

        private void MultiplayerButtonClickedHook(FRONT_Multiplayer self, string param)
        {
            bool memoryActiveBefore = IsMemoryActive();
            bool mayAddAi = string.Equals(param, "AddCustomLord", StringComparison.Ordinal);
            Dictionary<int, string> before =
                memoryActiveBefore && mayAddAi ? CaptureAiSlotKeys(self) : null;

            if (memoryActiveBefore && string.Equals(param, "CancelAISettings", StringComparison.Ordinal))
                SaveActiveAiSettings();

            if (IsMemoryActive() && string.Equals(param, "Play", StringComparison.Ordinal))
                SaveAllAiSettings(self);

            if (mayAddAi)
            {
                InvokeWithFinalAiSeatReleased(
                    self,
                    "ButtonClicked AddCustomLord",
                    () => multiplayerButtonClickedTrampoline(self, param));
            }
            else
            {
                multiplayerButtonClickedTrampoline(self, param);
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
                    $"Bugfixes and QoL AI selection apply after ButtonClicked({param}) failed: {ex}");
            }
        }

        private void ShowSkirmishRandomAiSetterHook(MainViewModel self, bool value)
        {
            if (!value && settings.EnableMod && IsEditableHostLobby(self))
                value = true;

            showSkirmishRandomAiSetterTrampoline(self, value);
        }

        private void UpdateRandomAiButtonsHook(FRONT_Multiplayer self)
        {
            multiplayerUpdateRandomAiButtonsTrampoline(self);
            if (!settings.EnableMod || self?.currentLobby == null)
                return;

            int maximumAiCount = GetMaximumAiCount(self);
            for (int index = 0; index < MultiplayerRandomAiButtonFields.Length; index++)
            {
                if (MultiplayerRandomAiButtonFields[index].GetValue(self) is Noesis.Button button)
                    button.IsEnabled = maximumAiCount >= index + 1;
            }
        }

        private void EnsureRandomAiButtonVisibleInEditableLobby()
        {
            // MainViewModel.Instance constructs the view model on first access and is not
            // safe during early plugin initialization before Vanilla finishes loading it.
            if (!MainViewModel.viewModelLoaded)
                return;

            MainViewModel viewModel = MainViewModel.Instance;
            if (IsEditableHostLobby(viewModel))
                viewModel.Show_SkirmishRandomAI = true;
        }

        private static bool IsEditableHostLobby(MainViewModel viewModel)
        {
            FRONT_Multiplayer frontend = viewModel?.FRONTMultiplayer;
            return viewModel != null &&
                viewModel.Show_SkirmishAIADD &&
                frontend?.currentLobby != null &&
                frontend.currentLobby.isHost;
        }

        private static int GetMaximumAiCount(FRONT_Multiplayer self)
        {
            int playerCap = self == null ? 0 : (int)MultiplayerPlayerCapField.GetValue(self);
            int lobbyMaxPlayers = self?.currentLobby?.iMaxPlayers ?? 0;
            FileHeader selectedHeader = self == null
                ? null
                : MultiplayerSelectedMpHeaderField.GetValue(self) as FileHeader;
            int selectedMapMaxPlayers = selectedHeader == null
                ? 0
                : (int)FileHeaderMaxPlayersField.GetValue(selectedHeader);
            int humanCount = self?.currentLobby?.CountHumanPlayers() ?? 0;
            return RandomOpponentLobbyPolicy.GetMaximumAiCount(
                playerCap,
                lobbyMaxPlayers,
                selectedMapMaxPlayers,
                FRONT_Multiplayer.customCoopGame,
                FRONT_Multiplayer.skirmishGame,
                humanCount);
        }

        private void SkirmishAiAddClickHook(FRONT_Multiplayer self, string param)
        {
            bool memoryActiveBefore = IsMemoryActive();
            Dictionary<int, string> before = memoryActiveBefore ? CaptureAiSlotKeys(self) : null;
            bool protectedRandomCommand = settings.EnableMod && IsRandomOpponentCount(param, out _);

            bool handled = false;
            try
            {
                handled = TryCreateFilteredRandomAi(self, param);
            }
            catch (Exception ex)
            {
                // Never hand a protected random command to Vanilla after a partial mutation.
                Shared.DebugLogHelper.LogError(
                    log,
                    protectedRandomCommand
                        ? $"Bugfixes and QoL protected random-lord selection failed; the command was stopped without calling Vanilla: {ex}"
                        : $"Bugfixes and QoL filtered random-lord selection failed; falling back to Vanilla: {ex}");
                handled = protectedRandomCommand;
            }

            if (!handled)
            {
                if (IsIndividualAiSelection(param))
                {
                    InvokeWithFinalAiSeatReleased(
                        self,
                        "SkirmishAIAddClick " + param,
                        () => skirmishAiAddClickTrampoline(self, param));
                }
                else
                {
                    skirmishAiAddClickTrampoline(self, param);
                }
            }

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
                    $"Bugfixes and QoL AI selection apply after SkirmishAIAddClick({param}) failed: {ex}");
            }
        }

        private bool TryCreateFilteredRandomAi(FRONT_Multiplayer self, string param)
        {
            if (!IsRandomOpponentCount(param, out int requestedValue) || !settings.EnableMod)
                return false;

            if (self?.currentLobby == null)
                return true;

            if (!FRONT_Multiplayer.skirmishGame && !self.currentLobby.isHost)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Bugfixes and QoL rejected a random-opponent command from a multiplayer client.");
                return true;
            }

            List<RandomLordCandidate> candidates = BuildRandomLordCandidates();
            if (candidates.Count == 0)
            {
                if (!emptyRandomLordSelectionLogged)
                {
                    emptyRandomLordSelectionLogged = true;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "Bugfixes and QoL did not create random opponents because no enabled lord source contains an available lord.");
                }
                return true;
            }

            emptyRandomLordSelectionLogged = false;
            List<HumanLobbyMemberSnapshot> humans = CaptureHumanMembers(self);
            int removedCount = RemoveExistingRandomOpponents(self);
            if (!VerifyHumanMembers(self, humans, "after removing old random opponents"))
            {
                RefreshRandomOpponentUi(self);
                return true;
            }

            var random = new Random();
            int requestedCount = -requestedValue;
            int maximumAiCount = GetMaximumAiCount(self);
            int targetCount = Math.Min(requestedCount, maximumAiCount);
            int addedCount = 0;
            for (int i = 0; i < targetCount; i++)
            {
                RandomLordCandidate candidate = candidates[random.Next(candidates.Count)];
                Platform_Multiplayer.MPLobbyMember member = candidate.CustomLord == null
                    ? Platform_Multiplayer.Instance.AddSkirmishPlayerLocal(candidate.BuiltInLordType)
                    : Platform_Multiplayer.Instance.AddCustomSkirmishPlayerLocal(candidate.CustomLord);
                UpdateSteamMappings(self);
                if (member == null)
                    continue;

                if (candidate.CustomLord != null)
                    FinalizeCustomLordIdentity(self, member);

                int playerId = self.currentLobby.getThisPlayerFromSteamID(member.GetSteamID());
                if (playerId < 1 || playerId > self.AIVs.Length)
                    throw new InvalidOperationException("The newly added random lord has no valid lobby player slot.");

                if (candidate.CustomLord == null)
                {
                    self.AIVs[playerId - 1].Init(candidate.BuiltInLordType, string.Empty);
                }
                else
                {
                    InitializeCustomLord(self, member, playerId, candidate.CustomLord);
                }
                addedCount++;
            }

            UpdateSteamMappings(self);
            RefreshRandomOpponentUi(self);
            bool humansUnchanged = VerifyHumanMembers(self, humans, "after adding new random opponents");
            Shared.DebugLogHelper.LogDebug(
                log,
                () =>
                    $"Bugfixes and QoL created protected random opponents. lobby={GetLobbyKind(self)}, " +
                    $"requested={requestedCount}, maximum={maximumAiCount}, removed={removedCount}, added={addedCount}, " +
                    $"humans={humans.Count}, humansUnchanged={humansUnchanged}, " +
                    $"candidates={candidates.Count}, builtIn={includeBuiltInLords}, local={includeLocalCustomLords}, " +
                    $"workshop={includeWorkshopCustomLords}");
            return true;
        }

        private static bool IsRandomOpponentCount(string param, out int requestedValue) =>
            int.TryParse(param, out requestedValue) && requestedValue < 0 && requestedValue >= -8;

        private static bool IsIndividualAiSelection(string param)
        {
            return int.TryParse(param, out int value) &&
                   ((value >= 0 && value <= 26) || value == 99);
        }

        private void InvokeWithFinalAiSeatReleased(
            FRONT_Multiplayer self,
            string source,
            Action invokeVanilla)
        {
            Platform_Multiplayer.MPLobby lobby = self?.currentLobby;
            int playerCap = self == null ? 0 : (int)MultiplayerPlayerCapField.GetValue(self);
            int lobbyMaxPlayers = lobby?.iMaxPlayers ?? 0;
            int memberCountBefore = lobby?.members?.Count ?? 0;
            int humanCountBefore = lobby?.CountHumanPlayers() ?? 0;
            int aiCountBefore = lobby?.CountAIPlayers() ?? 0;
            bool releaseSeat = RandomOpponentLobbyPolicy.ShouldReleaseFinalAiSeat(
                settings.EnableMod,
                lobby != null && lobby.isHost,
                FRONT_Multiplayer.skirmishGame,
                FRONT_Multiplayer.coopGame,
                FRONT_Multiplayer.customCoopGame,
                playerCap,
                lobbyMaxPlayers,
                memberCountBefore,
                humanCountBefore,
                aiCountBefore);

            if (!releaseSeat)
            {
                invokeVanilla();
                return;
            }

            bool originalSkirmishGame = FRONT_Multiplayer.skirmishGame;
            try
            {
                // Vanilla's only extra restriction here is guarded by !skirmishGame.
                // Restore the real mode before publishing any resulting lobby update.
                FRONT_Multiplayer.skirmishGame = true;
                invokeVanilla();
            }
            finally
            {
                FRONT_Multiplayer.skirmishGame = originalSkirmishGame;
            }

            int memberCountAfter = lobby.members.Count;
            int humanCountAfter = lobby.CountHumanPlayers();
            int aiCountAfter = lobby.CountAIPlayers();
            if (memberCountAfter > memberCountBefore)
                UpdateHostInfo(self);

            Shared.DebugLogHelper.LogDebug(
                log,
                $"Bugfixes and QoL final multiplayer AI seat release: source={source}, " +
                $"members={memberCountBefore}->{memberCountAfter}, humans={humanCountBefore}->{humanCountAfter}, " +
                $"ai={aiCountBefore}->{aiCountAfter}, playerCap={playerCap}, lobbyMaxPlayers={lobbyMaxPlayers}, " +
                $"added={memberCountAfter > memberCountBefore}.");
        }

        private List<RandomLordCandidate> BuildRandomLordCandidates()
        {
            var candidates = new List<RandomLordCandidate>();
            if (includeBuiltInLords)
            {
                for (int lordType = 0; lordType < 29; lordType++)
                {
                    if (IsBuiltInLordAvailable(lordType))
                        candidates.Add(RandomLordCandidate.ForBuiltIn(lordType));
                }
            }

            if (includeLocalCustomLords || includeWorkshopCustomLords)
            {
                foreach (CustomisationFileManager.CustomLord lord in
                    CustomisationFileManager.Instance.GetCustomLords())
                {
                    if (lord == null ||
                        string.IsNullOrEmpty(lord.lordName) ||
                        lord.configs == null || lord.configs.Count == 0 ||
                        lord.aivs == null || lord.aivs.Count == 0)
                    {
                        continue;
                    }

                    bool sourceEnabled = lord.workshop
                        ? includeWorkshopCustomLords
                        : includeLocalCustomLords;
                    if (sourceEnabled)
                        candidates.Add(RandomLordCandidate.ForCustom(lord));
                }
            }

            return candidates;
        }

        private static bool IsBuiltInLordAvailable(int lordType)
        {
            // Frontend lord indices start at zero and therefore do not match AILords,
            // whose zero value is SK_NULL.
            switch (lordType)
            {
                case 20:
                case 21:
                    return FrontendMenus.DLC1Owned;
                case 22:
                case 23:
                    return FrontendMenus.DLC2Owned;
                case 25:
                case 26:
                    return FrontendMenus.DLC3Owned;
                case 27:
                case 28:
                    return FrontendMenus.DLC4Owned;
                default:
                    return true;
            }
        }

        private static int RemoveExistingRandomOpponents(FRONT_Multiplayer self)
        {
            var aiMembers = new List<Platform_Multiplayer.MPLobbyMember>();
            foreach (Platform_Multiplayer.MPLobbyMember member in self.currentLobby.members)
            {
                if (member != null &&
                    RandomOpponentLobbyPolicy.IsRemovableAi(member.SkirmishMember, member.SkirmishHumanMember))
                {
                    aiMembers.Add(member);
                }
            }

            int removedCount = 0;
            MultiplayerPlayKickSpeechField.SetValue(self, false);
            try
            {
                foreach (Platform_Multiplayer.MPLobbyMember member in aiMembers)
                {
                    Platform_Multiplayer.Instance.kickSkirmishPlayer(member.GetSteamID());
                    removedCount++;
                    self.currentLobby.validateTeams();
                    UpdateSteamMappings(self);
                    MultiplayerReSortTeamInfoMethod.Invoke(self, null);
                    UpdateHostInfo(self);
                    MultiplayerUpdateRadarShieldPositionsMethod.Invoke(self, null);
                    MultiplayerUpdateRandomAiButtonsMethod.Invoke(self, null);
                }
            }
            finally
            {
                MultiplayerPlayKickSpeechField.SetValue(self, true);
            }
            self.currentLobby.validateTeams();
            return removedCount;
        }

        private static List<HumanLobbyMemberSnapshot> CaptureHumanMembers(FRONT_Multiplayer self)
        {
            var snapshots = new List<HumanLobbyMemberSnapshot>();
            if (self?.currentLobby?.members == null)
                return snapshots;

            foreach (Platform_Multiplayer.MPLobbyMember member in self.currentLobby.members)
            {
                if (member == null || !member.SkirmishHumanMember)
                    continue;

                snapshots.Add(new HumanLobbyMemberSnapshot(
                    member.GetSteamID(),
                    self.currentLobby.getThisPlayerFromSteamID(member.GetSteamID()),
                    member.colourID,
                    self.currentLobby.getTeam(member)));
            }

            return snapshots;
        }

        private bool VerifyHumanMembers(
            FRONT_Multiplayer self,
            List<HumanLobbyMemberSnapshot> snapshots,
            string phase)
        {
            if (self?.currentLobby?.members == null || snapshots == null)
                return false;

            foreach (HumanLobbyMemberSnapshot snapshot in snapshots)
            {
                Platform_Multiplayer.MPLobbyMember current = null;
                foreach (Platform_Multiplayer.MPLobbyMember member in self.currentLobby.members)
                {
                    if (member != null && member.GetSteamID() == snapshot.SteamId)
                    {
                        current = member;
                        break;
                    }
                }

                int currentPlayerId = current == null
                    ? 0
                    : self.currentLobby.getThisPlayerFromSteamID(current.GetSteamID());
                int currentTeam = current == null ? -1 : self.currentLobby.getTeam(current);
                int currentColour = current == null ? -1 : current.colourID;
                if (current == null ||
                    !current.SkirmishHumanMember ||
                    currentPlayerId != snapshot.PlayerId ||
                    currentColour != snapshot.ColourId ||
                    currentTeam != snapshot.Team)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL detected a changed human lobby member {phase}; " +
                        $"steamId={snapshot.SteamId}, player={snapshot.PlayerId}->{currentPlayerId}, " +
                        $"colour={snapshot.ColourId}->{currentColour}, team={snapshot.Team}->{currentTeam}. " +
                        "Random-opponent processing was stopped.");
                    return false;
                }
            }

            return true;
        }

        private static string GetLobbyKind(FRONT_Multiplayer self)
        {
            if (FRONT_Multiplayer.skirmishGame)
                return FRONT_Multiplayer.coopGame ? "singleplayer-customized-coop-trail" : "singleplayer-skirmish";
            if (self?.currentLobby?.coopTrailGame == true)
                return "multiplayer-customized-coop-trail";
            if (FRONT_Multiplayer.customCoopGame)
                return "multiplayer-custom-coop";
            return "multiplayer-skirmish";
        }

        private static void FinalizeCustomLordIdentity(
            FRONT_Multiplayer self,
            Platform_Multiplayer.MPLobbyMember member)
        {
            for (int slot = 0; slot < 8; slot++)
            {
                if (self.currentLobby.this_player_to_SteamID_mapping[slot] != member.GetSteamID())
                    continue;

                ulong previousSteamId = member.GetSteamID();
                member.SetValidCustomLordType(slot, member.GetLordSubType());
                self.currentLobby.this_player_to_SteamID_mapping[slot] = member.GetSteamID();
                self.currentLobby.switchTeamID(previousSteamId, member.GetSteamID());
                break;
            }
        }

        private static void InitializeCustomLord(
            FRONT_Multiplayer self,
            Platform_Multiplayer.MPLobbyMember member,
            int playerId,
            CustomisationFileManager.CustomLord lord)
        {
            FRONT_Multiplayer.MPAIVInfo info = self.AIVs[playerId - 1];
            info.Init(member.GetLordType(), lord.lordName);
            info.lordConfig = lord.configs[0];
            info.aivs.Add(lord.aivs[0]);
            info.imageData = lord.imageData;
            info.image = lord.image;
        }

        private static void RefreshRandomOpponentUi(FRONT_Multiplayer self)
        {
            MultiplayerReSortTeamInfoMethod.Invoke(self, null);
            UpdateHostInfo(self);
            MultiplayerCreateTeamShieldsMethod.Invoke(self, null);
            MultiplayerUpdateRadarShieldPositionsMethod.Invoke(self, null);
            MultiplayerUpdateRandomAiButtonsMethod.Invoke(self, null);
        }

        private static void UpdateSteamMappings(FRONT_Multiplayer self)
        {
            MultiplayerUpdateSteamMappingsMethod.Invoke(self, null);
        }

        private void SetFilter(ref bool field, bool value, string propertyName)
        {
            if (field == value)
                return;

            field = value;
            OnPropertyChanged(propertyName);
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private sealed class RandomLordCandidate
        {
            private RandomLordCandidate(int builtInLordType, CustomisationFileManager.CustomLord customLord)
            {
                BuiltInLordType = builtInLordType;
                CustomLord = customLord;
            }

            public int BuiltInLordType { get; }
            public CustomisationFileManager.CustomLord CustomLord { get; }

            public static RandomLordCandidate ForBuiltIn(int lordType)
            {
                return new RandomLordCandidate(lordType, null);
            }

            public static RandomLordCandidate ForCustom(CustomisationFileManager.CustomLord lord)
            {
                return new RandomLordCandidate(-1, lord);
            }
        }

        private sealed class HumanLobbyMemberSnapshot
        {
            public HumanLobbyMemberSnapshot(ulong steamId, int playerId, int colourId, int team)
            {
                SteamId = steamId;
                PlayerId = playerId;
                ColourId = colourId;
                Team = team;
            }

            public ulong SteamId { get; }
            public int PlayerId { get; }
            public int ColourId { get; }
            public int Team { get; }
        }

        private void AiSettingsButtonClickedHook(FRONT_Multiplayer_AISettings self, string param)
        {
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
                    $"Bugfixes and QoL AI selection save failed after ButtonClicked({param}): {ex}");
            }
        }

        private void AiSettingsAddSelectedHook(FRONT_Multiplayer_AISettings self)
        {
            aiSettingsAddSelectedTrampoline(self);
            SaveAiSettings(GetAivInfo(self));
        }

        private bool IsMemoryActive()
        {
            return settings.EnableMod && settings.RememberAiAivSettings;
        }

        internal void RecordSelection(FRONT_Multiplayer.MPAIVInfo info)
        {
            try
            {
                SaveAiSettings(info);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL AI selection save failed after loading a named preset: {ex}");
            }
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
                    $"Bugfixes and QoL AI selection save failed while closing settings: {ex}");
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

            string encoded = AiAivSelectionCodec.Encode(info);
            bool hadExisting = storedSelections.TryGetValue(key, out string existing);
            if (hadExisting && string.Equals(existing, encoded, StringComparison.Ordinal))
                return;

            storedSelections[key] = encoded;
            SaveStore();
            Shared.DebugLogHelper.LogDebug(
                log,
                () =>
                    $"Bugfixes and QoL saved AI AIV/AIC selection: key={key}, hadExisting={hadExisting}, " +
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
                        $"Bugfixes and QoL ignored invalid stored AI selection for {key}: {decodeError}");
                    continue;
                }
                if (!string.IsNullOrEmpty(decodeError))
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Bugfixes and QoL migrated stored AI selection for {key}: {decodeError}");
                }
                if (!string.Equals(BuildLordKey(decoded), key, StringComparison.OrdinalIgnoreCase))
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Bugfixes and QoL ignored stored AI selection for {key}: decoded lord is {BuildLordKey(decoded)}.");
                    continue;
                }

                AiAivSelectionCodec.CopyInto(decoded, parent.AIVs[playerId - 1]);
                applied = true;
                Shared.DebugLogHelper.LogDebug(
                    log,
                    () =>
                        $"Bugfixes and QoL restored shared AI AIV/AIC selection: reason={reason}, key={key}, " +
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

        private static void UpdateHostInfo(FRONT_Multiplayer parent)
        {
            if (parent != null)
                MultiplayerUpdateHostInfoMethod.Invoke(parent, new object[] { false });
        }

        private static string BuildLordKey(FRONT_Multiplayer.MPAIVInfo info)
        {
            return AivAicPresetStore.BuildLordKey(info);
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
                    () => $"Bugfixes and QoL shared AI AIV/AIC selection file not found: {storePath}");
                return;
            }

            try
            {
                FileInfo fileInfo = new FileInfo(storePath);
                if (fileInfo.Length > MaxStoreFileBytes)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Bugfixes and QoL ignored oversized AI selection store: path={storePath}, " +
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
                            $"Bugfixes and QoL ignored unsupported AI selection entry: key={entry.Key}.");
                        continue;
                    }

                    storedSelections[entry.Key] = entry.Value;
                }

                Shared.DebugLogHelper.LogDebug(
                    log,
                    () =>
                        $"Bugfixes and QoL loaded {storedSelections.Count} shared AI AIV/AIC selections from {storePath}.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Bugfixes and QoL could not load AI selection store from {storePath}: {ex.Message}");
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
                    $"Bugfixes and QoL could not save AI selection store to {storePath}: {ex}");
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
            var jsonValues = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> entry in values)
                jsonValues.Add(entry.Key, entry.Value);
            return Shared.DependencyFreeJson.Serialize(jsonValues);
        }

        private static Dictionary<string, string> ParseJsonObject(string json)
        {
            if (!(Shared.DependencyFreeJson.Parse(json) is Dictionary<string, object> values))
                throw new InvalidDataException("AI selection store root must be a JSON object.");

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, object> entry in values)
            {
                if (!(entry.Value is string text))
                    throw new InvalidDataException("AI selection store value for '" + entry.Key + "' must be a string.");
                result[entry.Key] = text;
            }
            return result;
        }

    }
}
