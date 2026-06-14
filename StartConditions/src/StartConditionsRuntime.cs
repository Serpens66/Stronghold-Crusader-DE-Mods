using BepInEx.Logging;
using R3;
using SHCDESE.API;
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
        private readonly StartConditionsLobbyViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private bool handledCurrentMap;
        private bool hooksSubscribed;
        private bool libraryInitialized;
        internal const int DelayedStartTroopCountMilliseconds = 20000;
        internal const int DelayedStartTroopCountSeconds = DelayedStartTroopCountMilliseconds / 1000;
        private const int IncomingGoodClearAmount = 100000;
        private string pendingStartTroopTimerHandle;
        private StartTroopPlan pendingStartTroopPlan;

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

        public StartConditionsRuntime(ManualLogSource log, StartConditionsLobbyViewModel settings)
        {
            this.log = log;
            this.settings = settings;
        }

        public void SubscribeHooks()
        {
            if (hooksSubscribed)
                return;

            LogDebug("Subscribing start conditions runtime hooks");

            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnStartMap));

            subscriptions.Add(MapLoaderR3EventHooks.OnLoadSave.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnLoadSave));

            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnUnloadMap));

            LogDebug("Start conditions runtime hooks subscribed");
            hooksSubscribed = true;
        }

        public void InitializeAfterLibraryLoaded()
        {
            if (libraryInitialized)
                return;

            LogDebug("Start conditions initialized");
            libraryInitialized = true;
        }

        public void Dispose()
        {
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();

            subscriptions.Clear();
            hooksSubscribed = false;
            CancelPendingStartTroopProcessing();
        }

        private void LogDebug(params object[] parts)
        {
            Shared.DebugLogHelper.LogDebug(log, parts);
        }

        private void LogInfo(params object[] parts)
        {
            log?.LogInfo(string.Join(" ", parts));
        }
    }
}
