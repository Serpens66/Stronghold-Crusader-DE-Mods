using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

// TODO
// script extender update: GetSelectedChimps in DisbandGameActionHook , dadurch aktuell keine auflösung von mehreren units supported.
// aber falls OnUnitTransition korrekt soldier->peasant erkennt, kann man das dann stattdessen nutzen.
// Eventuell dann ActiveUnmitCache nochmal überarbeiten, dass es effizienter läuft und auch keinen Timer mehr braucht usw und eigentlich auch kein resync mehr, wobei man das evlt doch alle 60 sek oderso machen sollte.
// sobald im extender ein weg eingebaut wurde die Rekrutierung zu verhinden, dann MakeTroopGameActionHooks dadurch ersetzen

namespace UnitLimit
{
    public sealed partial class UnitLimitRuntime : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly UnitLimitLobbyViewModel settings;
        private readonly HashSet<eChimps> loggedUnitLimitCooldownSuppressions = new HashSet<eChimps>();
        private readonly HashSet<eChimps> locallyDisabledUnitRecruitment = new HashSet<eChimps>();
        private readonly Dictionary<eChimps, DateTime> unitLimitMessageCooldowns = new Dictionary<eChimps, DateTime>();
        private readonly Dictionary<eChimps, bool> originalUnitRecruitableStates = new Dictionary<eChimps, bool>();
        private readonly Dictionary<eChimps, int> activeUnitLimits = new Dictionary<eChimps, int>();
        private readonly Dictionary<PendingRecruitmentKey, List<DateTime>> pendingRecruitments = new Dictionary<PendingRecruitmentKey, List<DateTime>>();
        // private readonly List<int> matchingUnitIds = new List<int>();
        private readonly ActiveUnitCache activeUnitCache;
        private MakeTroopGameActionHook makeTroopGameActionHook;
        private DisbandGameActionHook disbandGameActionHook;
        private bool settingsPropertyChangedSubscribed;
        private bool hooksSubscribed;
        private bool libraryInitialized;
        private const int LimitMessageDurationMilliseconds = 3000;
        private const int UnitLimitRecruitableRefreshMilliseconds = 5000;
        private static readonly TimeSpan UnitLimitMessageCooldown = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan PendingRecruitmentLifetime = TimeSpan.FromSeconds(3);
        private string limitMessageTimerHandle;
        private string unitLimitRecruitableRefreshTimerHandle;

        public LimitNotificationViewModel LimitNotification { get; } = new LimitNotificationViewModel();

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

        public UnitLimitRuntime(ManualLogSource log, UnitLimitLobbyViewModel settings)
        {
            this.log = log;
            this.settings = settings;
            activeUnitCache = new ActiveUnitCache(log);
        }

        public void SubscribeHooks()
        {
            if (hooksSubscribed)
                return;

            LogInfo("Subscribing unit limit runtime hooks");
            activeUnitCache.SubscribeHooks();
            activeUnitCache.OnActiveUnitChanged += OnActiveUnitChanged;
            makeTroopGameActionHook = new MakeTroopGameActionHook(log, ShouldBlockMakeTroopGameAction);
            disbandGameActionHook = new DisbandGameActionHook(log, activeUnitCache.NotifyNativeSnapshotChanged);

            MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnStartMap);

            MapLoaderR3EventHooks.OnLoadSave.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnLoadSave);

            MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnUnloadMap);

            LogInfo("Unit limit runtime hooks subscribed");
            hooksSubscribed = true;
        }

        public void InitializeAfterLibraryLoaded()
        {
            if (libraryInitialized)
                return;

            ApplyUnitLimits();
            SubscribeSettingsChanges();
            LogInfo("Applied unit limit settings");
            libraryInitialized = true;
        }

        public void Dispose()
        {
            if (settingsPropertyChangedSubscribed)
            {
                settings.SettingChanged -= OnSettingChanged;
                settingsPropertyChangedSubscribed = false;
            }

            CancelUnitLimitRecruitableRefresh();
            RestoreOriginalUnitRecruitableStates();
            HideLimitMessage();
            ClearPendingRecruitments("Dispose");
            disbandGameActionHook?.Dispose();
            disbandGameActionHook = null;
            makeTroopGameActionHook?.Dispose();
            makeTroopGameActionHook = null;
            activeUnitCache.OnActiveUnitChanged -= OnActiveUnitChanged;
            activeUnitCache.Dispose();

            loggedUnitLimitCooldownSuppressions.Clear();
            locallyDisabledUnitRecruitment.Clear();
            unitLimitMessageCooldowns.Clear();
            originalUnitRecruitableStates.Clear();
            activeUnitLimits.Clear();
        }

        private void OnStartMap(MapStartEventArgs args)
        {
            LogInfo("OnStartMap");
            ResetUnitRecruitableTracking();
            ApplyUnitLimits(false);
            StartUnitLimitRecruitableRefresh();
        }

        private void OnLoadSave(LoadSaveGameEventArgs args)
        {
            LogInfo("OnLoadSave");
            ResetUnitRecruitableTracking();
            ApplyUnitLimits(false);
            StartUnitLimitRecruitableRefresh();
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            LogInfo("OnUnloadMap");
            ClearPendingRecruitments("OnUnloadMap");
            CancelUnitLimitRecruitableRefresh();
            RestoreOriginalUnitRecruitableStates();
            HideLimitMessage();
            unitLimitMessageCooldowns.Clear();
            loggedUnitLimitCooldownSuppressions.Clear();
            locallyDisabledUnitRecruitment.Clear();
            originalUnitRecruitableStates.Clear();
            // matchingUnitIds.Clear();
        }

        private void LogInfo(params object[] parts)
        {
            log.LogInfo(string.Join(" ", parts));
        }
    }
}
