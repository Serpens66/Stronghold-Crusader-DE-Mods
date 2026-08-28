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
// sobald im extender ein weg eingebaut wurde die Rekrutierung zu verhinden, dann MakeTroopGameActionHooks dadurch ersetzen
// anstelle von AliveCount kann für den lokalen Spieler auch das verwendet werden: GameUnitManagerAPI.Instance.GetUnitArmyCount(eChimps chimp) liefert selbe ergebnisse wie alive (also kein pending und keine siege tents)

namespace UnitLimit
{
    public sealed partial class UnitLimitRuntime : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly UnitLimitLobbyViewModel settings;
        private readonly Dictionary<eChimps, int> activeUnitLimits = new Dictionary<eChimps, int>();
        private readonly Dictionary<PendingRecruitmentKey, PendingRecruitmentQueue> pendingRecruitments = new Dictionary<PendingRecruitmentKey, PendingRecruitmentQueue>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly ActiveUnitCache activeUnitCache;
        private readonly ActiveSiegeTentCache activeSiegeTentCache;
        private bool activeUnitCacheAvailable;
        private bool activeSiegeTentCacheAvailable;
        private MakeTroopGameActionHook makeTroopGameActionHook;
        private CreateTroopHoverHook createTroopHoverHook;
        private SiegeBuildHoverHook siegeBuildHoverHook;
        private RecruitmentAvailabilityUiHook recruitmentAvailabilityUiHook;
        private bool settingsPropertyChangedSubscribed;
        private bool hooksSubscribed;
        private bool libraryInitialized;
        private const int LimitMessageDurationMilliseconds = 3000;
        private static readonly TimeSpan PendingRecruitmentLifetime = TimeSpan.FromSeconds(3);
        private string limitMessageTimerHandle;

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
            eChimps.CHIMP_TYPE_ARAB_BALLISTA,
            eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER,
            eChimps.CHIMP_TYPE_BEDOUIN_HEALER,
            eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH,
            eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER,
            eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER,
            eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL,
            eChimps.CHIMP_TYPE_BEDOUIN_SAPPER,
            eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER,
        };

        public UnitLimitRuntime(ManualLogSource log, UnitLimitLobbyViewModel settings, bool verboseUnitEventLogging = false)
        {
            this.log = log;
            this.settings = settings;
            activeUnitCache = new ActiveUnitCache(log, verboseUnitEventLogging);
            activeSiegeTentCache = new ActiveSiegeTentCache(log);
        }

        public void SubscribeHooks()
        {
            if (!settings.EnableMod)
                return;

            if (hooksSubscribed)
                return;

            LogDebug("Subscribing unit limit runtime hooks");
            activeUnitCacheAvailable = TryInitializeFeature("active-unit cache", () =>
            {
                activeUnitCache.SubscribeHooks();
                activeUnitCache.OnActiveUnitChanged += OnActiveUnitChanged;
            });
            activeSiegeTentCacheAvailable = TryInitializeFeature("active-siege-tent cache", () =>
            {
                activeSiegeTentCache.SubscribeHooks();
                activeSiegeTentCache.OnActiveSiegeTentChanged += OnActiveSiegeTentChanged;
            });
            if (!activeUnitCacheAvailable)
            {
                activeUnitCache.OnActiveUnitChanged -= OnActiveUnitChanged;
                TryDisposeFeature("active-unit cache rollback", activeUnitCache);
            }
            if (!activeSiegeTentCacheAvailable)
            {
                activeSiegeTentCache.OnActiveSiegeTentChanged -= OnActiveSiegeTentChanged;
                TryDisposeFeature("active-siege-tent cache rollback", activeSiegeTentCache);
            }
            if (activeUnitCacheAvailable)
            {
                TryInitializeFeature("recruitment enforcement", () => makeTroopGameActionHook = new MakeTroopGameActionHook(log, DecideMakeTroopGameAction));
                TryInitializeFeature("recruitment tooltip", () => createTroopHoverHook = new CreateTroopHoverHook(log, UpdateRecruitmentLimitTooltip, ClearUnitLimitTooltip));
                TryInitializeFeature("recruitment availability UI", () => recruitmentAvailabilityUiHook = new RecruitmentAvailabilityUiHook(log, RefreshRecruitmentButtonAvailability));
            }
            if (activeUnitCacheAvailable && activeSiegeTentCacheAvailable)
                TryInitializeFeature("siege tooltip", () => siegeBuildHoverHook = new SiegeBuildHoverHook(log, UpdateSiegeBuildLimitTooltip, ClearUnitLimitTooltip));

            TrySubscribeFeature("map start", () => MapLoaderR3EventHooks.OnStartMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnStartMap));

            TrySubscribeFeature("save load", () => MapLoaderR3EventHooks.OnLoadSave.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnLoadSave));

            TrySubscribeFeature("map unload", () => MapLoaderR3EventHooks.OnUnloadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnUnloadMap));

            if (activeUnitCacheAvailable && activeSiegeTentCacheAvailable)
            {
                TrySubscribeFeature("placement validation", () => BuildingR3EventHooks.OnPlacementValidation.Observable
                        .Where(args => args.Phase == EventHookPhase.Pre)
                        .Subscribe(OnBuildingPlacementValidation));
            }

            LogDebug("Unit limit runtime hooks subscribed");
            hooksSubscribed = true;
        }

        public void InitializeAfterLibraryLoaded()
        {
            if (libraryInitialized)
                return;

            SubscribeSettingsChanges();
            if (!settings.EnableMod)
            {
                LogDebug("Unit limit disabled; runtime hooks not subscribed");
                libraryInitialized = true;
                return;
            }

            SubscribeHooks();
            ApplyUnitLimits();
            LogDebug("Applied unit limit settings");
            libraryInitialized = true;
        }

        public void Dispose()
        {
            UnsubscribeHooks();
            if (settingsPropertyChangedSubscribed)
            {
                settings.SettingChanged -= OnSettingChanged;
                settingsPropertyChangedSubscribed = false;
            }
        }

        private void UnsubscribeHooks()
        {
            foreach (IDisposable subscription in subscriptions)
            {
                try { subscription.Dispose(); }
                catch (Exception ex) { LogDebug("Unit limit subscription cleanup failed:", ex); }
            }

            subscriptions.Clear();
            hooksSubscribed = false;
            HideLimitMessage();
            ClearPendingRecruitments("Dispose");
            TryDisposeFeature("recruitment enforcement", makeTroopGameActionHook);
            makeTroopGameActionHook = null;
            TryDisposeFeature("recruitment tooltip", createTroopHoverHook);
            createTroopHoverHook = null;
            TryDisposeFeature("siege tooltip", siegeBuildHoverHook);
            siegeBuildHoverHook = null;
            TryDisposeFeature("recruitment availability UI", recruitmentAvailabilityUiHook);
            recruitmentAvailabilityUiHook = null;
            ClearUnitLimitTooltip();
            activeUnitCache.OnActiveUnitChanged -= OnActiveUnitChanged;
            activeSiegeTentCache.OnActiveSiegeTentChanged -= OnActiveSiegeTentChanged;
            TryDisposeFeature("active-unit cache", activeUnitCache);
            TryDisposeFeature("active-siege-tent cache", activeSiegeTentCache);
            activeUnitCacheAvailable = false;
            activeSiegeTentCacheAvailable = false;

            activeUnitLimits.Clear();
        }

        private void OnStartMap(MapStartEventArgs args)
        {
            LogDebug("OnStartMap");
            ResetUnitRecruitableTracking();
            ApplyUnitLimits();
        }

        private void OnLoadSave(LoadSaveGameEventArgs args)
        {
            LogDebug("OnLoadSave");
            ResetUnitRecruitableTracking();
            ApplyUnitLimits();
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            LogDebug("OnUnloadMap");
            ClearPendingRecruitments("OnUnloadMap");
            HideLimitMessage();
            ClearUnitLimitTooltip();
        }

        private void LogDebug(params object[] parts)
        {
            Shared.DebugLogHelper.LogDebug(log, parts);
        }

        private bool TryInitializeFeature(string featureName, Action initialize)
        {
            try
            {
                initialize();
                return true;
            }
            catch (Exception ex)
            {
                LogDebug("Unit limit feature failed; independent features continue:", featureName, ex);
                return false;
            }
        }

        private void TrySubscribeFeature(string featureName, Func<IDisposable> subscribe)
        {
            try
            {
                IDisposable subscription = subscribe();
                if (subscription != null)
                    subscriptions.Add(subscription);
            }
            catch (Exception ex) { LogDebug("Unit limit subscription failed; independent features continue:", featureName, ex); }
        }

        private void TryDisposeFeature(string featureName, IDisposable feature)
        {
            if (feature == null)
                return;
            try { feature.Dispose(); }
            catch (Exception ex) { LogDebug("Unit limit feature cleanup failed; independent features continue:", featureName, ex); }
        }
    }
}
