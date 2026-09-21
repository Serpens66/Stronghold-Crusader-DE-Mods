using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Player;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace StartConditions
{
    public sealed partial class StartConditionsRuntime : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly IStartConditionsSettings settings;
        private readonly VanillaPeaceTimeState vanillaPeaceTimeState;
        private readonly VanillaStartTroopSpawnState vanillaStartTroopSpawnState;
        private IStartConditionsSettings activeSettings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly StartConditionsMapSessionState mapSessionState =
            new StartConditionsMapSessionState();
        private static StartConditionsBriefingGoldRegistration processBriefingGoldRegistration;
        private bool settingsChangedSubscribed;
        private bool hooksSubscribed;
        private bool libraryInitialized;
        internal const int DelayedStartTroopCountMilliseconds = 20000;
        internal const int DelayedStartTroopCountSeconds = DelayedStartTroopCountMilliseconds / 1000;
        private static readonly TimeSpan KeepReadinessTimeout = TimeSpan.FromSeconds(30);
        private const int IncomingGoodClearAmount = 100000;
        private string pendingStartTroopTimerHandle;
        private StartTroopPlan pendingStartTroopPlan;
        private bool waitingForPeaceTimeEnd;
        private bool waitingForVanillaStartTroopCompletion;
        private readonly StartTroopCompletionWaitState startTroopCompletionWaitState =
            new StartTroopCompletionWaitState();
        private Shared.ActivePlayerKeepWaitHandle pendingKeepReadiness;
        private int[] activePlayerIds = Array.Empty<int>();

        private static readonly HashSet<eChimps> SoldierChimps = new HashSet<eChimps>
        {
            eChimps.CHIMP_TYPE_ARCHER,
            eChimps.CHIMP_TYPE_SPEARMAN,
            eChimps.CHIMP_TYPE_MACEMAN,
            eChimps.CHIMP_TYPE_XBOWMAN,
            eChimps.CHIMP_TYPE_PIKEMAN,
            eChimps.CHIMP_TYPE_SWORDSMAN,
            eChimps.CHIMP_TYPE_KNIGHT,
            eChimps.CHIMP_TYPE_ENGINEER,
            eChimps.CHIMP_TYPE_MONK,
            eChimps.CHIMP_TYPE_LADDERMAN,
            eChimps.CHIMP_TYPE_TUNNELER,
            eChimps.CHIMP_TYPE_ARAB_BOW,
            eChimps.CHIMP_TYPE_ARAB_SLAVE,
            eChimps.CHIMP_TYPE_ARAB_SLINGER,
            eChimps.CHIMP_TYPE_ARAB_ASSASIN,
            eChimps.CHIMP_TYPE_ARAB_HORSEMAN,
            eChimps.CHIMP_TYPE_ARAB_SWORDSMAN,
            eChimps.CHIMP_TYPE_ARAB_GRENADIER,
            eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER,
            eChimps.CHIMP_TYPE_BEDOUIN_HEALER,
            eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH,
            eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER,
            eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER,
            eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL,
            eChimps.CHIMP_TYPE_BEDOUIN_SAPPER,
            eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER,
        };

        public StartConditionsRuntime(ManualLogSource log, IStartConditionsSettings settings)
        {
            this.log = log;
            this.settings = settings;
            vanillaPeaceTimeState = new VanillaPeaceTimeState(log);
            vanillaStartTroopSpawnState = new VanillaStartTroopSpawnState(log);
            activeSettings = settings;
            Shared.GameplayModActivationGate.Initialize(
                log,
                StartConditionsPlugin.PluginGuid,
                StartConditionsPlugin.PluginName,
                () => settings.EnableMod,
                logRoutineActivity: false);
            Shared.GameplayModActivationGate.StateChanged += OnModeAllowedChanged;
        }

        private IStartConditionsSettings EffectiveSettings => activeSettings ?? settings;
        private bool EffectsEnabled =>
            Shared.GameplayModActivationGate.IsAllowed &&
            (settings.EnableMod || StartConditionsIntegration.HasMissionOverride);

        public void SubscribeHooks()
        {
            if (!EffectsEnabled)
                return;

            if (hooksSubscribed)
                return;

            subscriptions.Add(Shared.GameplaySessionLifecycle.SubscribeStarted(
                log,
                context =>
                {
                    if (context.IsEditor)
                        ResetMapSession(); // An editor session must never apply gameplay start resources/troops.
                    else if (context.IsLoadedSave)
                        OnLoadSave(context.Notification);
                    else
                        OnStartMap(context.Notification);
                }));

            subscriptions.Add(Shared.MissionEvents.Ended
                .Subscribe(OnUnloadMap));

            hooksSubscribed = true;
        }

        public void InitializeAfterLibraryLoaded()
        {
            InitializeAfterLibraryLoaded(null, false);
        }

        internal void InitializeAfterLibraryLoaded(
            CrusaderLibraryLoadContext context,
            bool currentNativeVersion)
        {
            if (libraryInitialized)
                return;

            vanillaPeaceTimeState.Initialize(context, currentNativeVersion);
            vanillaStartTroopSpawnState.Initialize(context, currentNativeVersion);
            InitializeAIStartTroopIsolation();
            EnsureBriefingGoldRegistration();
            SubscribeSettingsChanges();
            SubscribeHooks();
            libraryInitialized = true;
        }

        private void EnsureBriefingGoldRegistration()
        {
            if (processBriefingGoldRegistration != null)
                return;

            processBriefingGoldRegistration =
                new StartConditionsBriefingGoldRegistration(log, AdjustBriefingGold);
        }

        private int AdjustBriefingGold(APIShared.BriefingGoldContext context)
        {
            if (!EffectsEnabled || !mapSessionState.IsNewGame)
                return context.CurrentGold;
            if (context.IsHuman && !context.HasNoStartingGoldState)
                return context.CurrentGold;

            IStartConditionsSettings current = EffectiveSettings;
            int setGold = context.IsHuman
                ? current.SetStartGoldHuman
                : current.SetStartGoldAI;
            int addGold = context.IsHuman
                ? current.AddStartGoldHuman
                : current.AddStartGoldAI;
            return StartGoldPolicy.CalculateGold(
                context.EffectiveVanillaGold,
                setGold,
                addGold);
        }

        public void Dispose()
        {
            Shared.GameplayModActivationGate.StateChanged -= OnModeAllowedChanged;
            UnsubscribeHooks();
            if (settingsChangedSubscribed)
            {
                settings.SettingChanged -= OnSettingChanged;
                StartConditionsIntegration.MissionOverrideChanged -= OnMissionOverrideChanged;
                settingsChangedSubscribed = false;
            }
        }

        private void SubscribeSettingsChanges()
        {
            if (settingsChangedSubscribed)
                return;

            settings.SettingChanged += OnSettingChanged;
            StartConditionsIntegration.MissionOverrideChanged += OnMissionOverrideChanged;
            settingsChangedSubscribed = true;
        }

        private void OnSettingChanged(string propertyName)
        {
            if (propertyName != nameof(IStartConditionsSettings.EnableMod))
                return;

            if (EffectsEnabled)
                SubscribeHooks();
            else
                UnsubscribeHooks();
        }

        private void OnMissionOverrideChanged()
        {
            // A mission must work even when the user's persistent StartConditions toggle is off.
            if (EffectsEnabled)
                SubscribeHooks();
            else
                UnsubscribeHooks();
        }

        private void OnModeAllowedChanged(bool allowed)
        {
            if (!libraryInitialized)
                return;

            if (EffectsEnabled)
                SubscribeHooks();
            else
                UnsubscribeHooks();
        }

        private void UnsubscribeHooks()
        {
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();

            subscriptions.Clear();
            hooksSubscribed = false;
            ResetMapSession();
        }

        private void ResetMapSession()
        {
            CancelPendingKeepReadiness();
            CancelPendingStartTroopProcessing();
            mapSessionState.Reset();
            activeSettings = settings;
            activePlayerIds = Array.Empty<int>();
        }

        private void LogWarning(params object[] parts)
        {
            Shared.DebugLogHelper.LogWarning(log, string.Join(" ", parts));
        }

        private void LogError(params object[] parts)
        {
            Shared.DebugLogHelper.LogError(log, string.Join(" ", parts));
        }
    }
}
