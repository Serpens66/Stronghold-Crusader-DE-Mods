using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
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
// anstelle von AliveCount kann für den lokalen Spieler auch das verwendet werden: GameUnitManagerAPI.Instance.GetUnitArmyCount(eChimps chimp) liefert selbe ergebnisse wie alive (also kein pending und keine siege tents)

namespace UnitLimit
{
    public sealed partial class UnitLimitRuntime : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly UnitLimitLobbyViewModel settings;
        private readonly HashSet<eChimps> locallyDisabledUnitRecruitment = new HashSet<eChimps>();
        private readonly Dictionary<eChimps, bool> originalUnitRecruitableStates = new Dictionary<eChimps, bool>();
        private readonly Dictionary<eChimps, int> activeUnitLimits = new Dictionary<eChimps, int>();
        private readonly Dictionary<PendingRecruitmentKey, List<DateTime>> pendingRecruitments = new Dictionary<PendingRecruitmentKey, List<DateTime>>();
        // private readonly List<int> matchingUnitIds = new List<int>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly ActiveUnitCache activeUnitCache;
        private readonly ActiveSiegeTentCache activeSiegeTentCache;
        private MakeTroopGameActionHook makeTroopGameActionHook;
        private DisbandGameActionHook disbandGameActionHook;
        private CreateTroopHoverHook createTroopHoverHook;
        private SiegeBuildHoverHook siegeBuildHoverHook;
        private bool settingsPropertyChangedSubscribed;
        private bool hooksSubscribed;
        private bool libraryInitialized;
        private const int LimitMessageDurationMilliseconds = 3000;
        private const int UnitLimitRecruitableRefreshMilliseconds = 5000;
        private static readonly TimeSpan PendingRecruitmentLifetime = TimeSpan.FromSeconds(3);
        private string limitMessageTimerHandle;
        private string unitLimitRecruitableRefreshTimerHandle;

        public LimitNotificationViewModel LimitNotification { get; } = new LimitNotificationViewModel();
        public LimitNotificationViewModel SiegeLimitNotification { get; } = new LimitNotificationViewModel();
        public UnitLimitTooltipViewModel UnitLimitTooltip { get; } = new UnitLimitTooltipViewModel();

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
            eChimps.CHIMP_TYPE_CATAPULT,
            eChimps.CHIMP_TYPE_TREBUCHET,
            eChimps.CHIMP_TYPE_BATTERING_RAM,
            eChimps.CHIMP_TYPE_SIEGE_TOWER,
            eChimps.CHIMP_TYPE_PORTABLE_SHIELD,
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
            activeSiegeTentCache = new ActiveSiegeTentCache(log);
        }

        public void SubscribeHooks()
        {
            if (hooksSubscribed)
                return;

            LogDebug("Subscribing unit limit runtime hooks");
            activeUnitCache.SubscribeHooks();
            activeUnitCache.OnActiveUnitChanged += OnActiveUnitChanged;
            activeSiegeTentCache.SubscribeHooks();
            activeSiegeTentCache.OnActiveSiegeTentChanged += OnActiveSiegeTentChanged;
            makeTroopGameActionHook = new MakeTroopGameActionHook(log, DecideMakeTroopGameAction);
            disbandGameActionHook = new DisbandGameActionHook(log, activeUnitCache.NotifyNativeSnapshotChanged);
            createTroopHoverHook = new CreateTroopHoverHook(log, UpdateRecruitmentLimitTooltip, ClearUnitLimitTooltip);
            siegeBuildHoverHook = new SiegeBuildHoverHook(log, UpdateSiegeBuildLimitTooltip, ClearUnitLimitTooltip);

            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnStartMap));

            subscriptions.Add(MapLoaderR3EventHooks.OnLoadSave.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnLoadSave));

            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnUnloadMap));

            subscriptions.Add(BuildingR3EventHooks.OnPlacementValidation.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnBuildingPlacementValidation));

            LogDebug("Unit limit runtime hooks subscribed");
            hooksSubscribed = true;
        }

        public void InitializeAfterLibraryLoaded()
        {
            if (libraryInitialized)
                return;

            ApplyUnitLimits();
            SubscribeSettingsChanges();
            LogDebug("Applied unit limit settings");
            libraryInitialized = true;
        }

        public void Dispose()
        {
            if (settingsPropertyChangedSubscribed)
            {
                settings.SettingChanged -= OnSettingChanged;
                settingsPropertyChangedSubscribed = false;
            }

            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();

            subscriptions.Clear();
            hooksSubscribed = false;
            CancelUnitLimitRecruitableRefresh();
            RestoreOriginalUnitRecruitableStates();
            HideLimitMessage();
            ClearPendingRecruitments("Dispose");
            disbandGameActionHook?.Dispose();
            disbandGameActionHook = null;
            makeTroopGameActionHook?.Dispose();
            makeTroopGameActionHook = null;
            createTroopHoverHook?.Dispose();
            createTroopHoverHook = null;
            siegeBuildHoverHook?.Dispose();
            siegeBuildHoverHook = null;
            ClearUnitLimitTooltip();
            activeUnitCache.OnActiveUnitChanged -= OnActiveUnitChanged;
            activeSiegeTentCache.OnActiveSiegeTentChanged -= OnActiveSiegeTentChanged;
            activeUnitCache.Dispose();
            activeSiegeTentCache.Dispose();

            locallyDisabledUnitRecruitment.Clear();
            originalUnitRecruitableStates.Clear();
            activeUnitLimits.Clear();
        }

        private void OnStartMap(MapStartEventArgs args)
        {
            LogDebug("OnStartMap");
            ResetUnitRecruitableTracking();
            ApplyUnitLimits(false);
            StartUnitLimitRecruitableRefresh();
        }

        private void OnLoadSave(LoadSaveGameEventArgs args)
        {
            LogDebug("OnLoadSave");
            ResetUnitRecruitableTracking();
            ApplyUnitLimits(false);
            StartUnitLimitRecruitableRefresh();
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            LogDebug("OnUnloadMap");
            ClearPendingRecruitments("OnUnloadMap");
            CancelUnitLimitRecruitableRefresh();
            RestoreOriginalUnitRecruitableStates();
            HideLimitMessage();
            ClearUnitLimitTooltip();
            locallyDisabledUnitRecruitment.Clear();
            originalUnitRecruitableStates.Clear();
            // matchingUnitIds.Clear();
        }

        private void LogDebug(params object[] parts)
        {
            Shared.DebugLogHelper.LogDebug(log, parts);
        }
    }
}
