using BepInEx;
using BepInEx.Logging;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using System;
using System.Linq;

namespace ExtenderFixesIssueRepros
{
    [BepInDependency("000shcdese", "2.9.0")]
    [BepInDependency("fixes")]
    [BepInPlugin("ExtenderFixesIssueRepros_Serp", "Extender and Fixes Issue Repros", "0.1.0")]
    public sealed unsafe class IssueReprosPlugin : BaseUnityPlugin
    {
        private static IssueReprosPlugin persistentInstance;
        private static IDisposable mapSubscription;
        private static AivSaveProbe aivProbe;
        private static bool librarySubscriptionInstalled;
        private static bool probesInitialized;
        private static bool selectionSnapshotSeen;
        private static int previousSelectionSignature;
        private static bool tickFailureLogged;
        private static readonly string[] previousLordNames = new string[9];
        private static readonly bool[] previousPreferencePresence = new bool[9];
        private static readonly bool[] previousEnabledFlags = new bool[9];
        private static int mapSequence;

        internal static ManualLogSource Log => persistentInstance.Logger;

        private void Awake()
        {
            persistentInstance = this;
            Log.LogInfo("Issue probes loaded. Selection, AIV save/load, and map-load AI flags are captured automatically.");
            if (librarySubscriptionInstalled)
                return;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            librarySubscriptionInstalled = true;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (probesInitialized)
                return;
            probesInitialized = true;
            try
            {
                aivProbe = new AivSaveProbe();
                aivProbe.Register();
                GameTimeManagerAPI.Instance.OnTick += OnGameTick;
                mapSubscription = MapLoaderR3EventHooks.OnPostLoad.Observable.Subscribe(OnMapPostLoad);
                Log.LogInfo("[REPRO] Event probes installed; all subscriptions remain rooted for process lifetime.");
            }
            catch (Exception ex)
            {
                Log.LogError("[REPRO] Initialization failed: " + ex);
            }
        }

        private static void OnGameTick(int tick)
        {
            try
            {
                aivProbe?.CompareOnceAfterLoad();
                GamePlayerManagerAPI api = GamePlayerManagerAPI.Instance;
                int localId = api.GetLocalPlayerId();
                int[] output = EngineInterface.selectedChimps;
                if (localId < 1 || localId > 8 || output == null)
                    return;
                int capacity = output.Length / 2;
                int signature = 17;
                for (int playerId = 1; playerId <= 8; playerId++)
                    signature = unchecked(signature * 31 + api.GetSelectedChimpsCount(playerId));
                int localCount = api.GetSelectedChimpsCount(localId);
                for (int i = 0; i < localCount && i < capacity; i++)
                {
                    signature = unchecked(signature * 31 + output[2 * i]);
                    signature = unchecked(signature * 31 + output[2 * i + 1]);
                }
                if (selectionSnapshotSeen && signature == previousSelectionSignature)
                    return;
                selectionSnapshotSeen = true;
                previousSelectionSignature = signature;
                SnapshotSelection(tick);
            }
            catch (Exception ex)
            {
                if (!tickFailureLogged)
                {
                    tickFailureLogged = true;
                    Log.LogError("[REPRO] Tick probe failed: " + ex);
                }
            }
        }

        private static void SnapshotSelection(int tick)
        {
            GamePlayerManagerAPI api = GamePlayerManagerAPI.Instance;
            int localId = api.GetLocalPlayerId();
            int[] output = EngineInterface.selectedChimps;
            if (output == null)
            {
                Log.LogWarning("[SE-SELECT] INCONCLUSIVE: local output array is null.");
                return;
            }

            int capacity = output.Length / 2;
            Log.LogInfo($"[SE-SELECT] utc={DateTime.UtcNow:O} tick={tick} localPlayerId={localId} localOutputCapacity={capacity}; compare both clients' snapshots with different selections.");
            for (int playerId = 1; playerId <= 8; playerId++)
            {
                int count = api.GetSelectedChimpsCount(playerId);
                if (count < 0 || count > capacity)
                {
                    Log.LogWarning($"[SE-SELECT] player={playerId} count={count} outside local output capacity; API call skipped.");
                    continue;
                }
                var units = api.GetSelectedChimps(playerId);
                string ids = string.Join(",", units.Select(u => u.UnitId.ToString()).ToArray());
                bool equalsLocalPrefix = units.Select((u, index) => u.UnitId == output[2 * index] && (int)u.UnitType == output[2 * index + 1]).All(equal => equal);
                Log.LogInfo($"[SE-SELECT] player={playerId} count={count} ids=[{ids}] equalsLocalOutputPrefix={equalsLocalPrefix}");
            }
            Log.LogInfo("[SE-SELECT] A defect is demonstrated only if a nonlocal player has a nonzero count and their returned IDs match this client's local output instead of that player's own snapshot.");
        }

        private static void OnMapPostLoad(MapPostLoadEventArgs e)
        {
            if (e == null || e.Phase != EventHookPhase.Post)
                return;
            selectionSnapshotSeen = false;
            mapSequence++;
            try
            {
                GameAIVManagerAPI aiv = GameAIVManagerAPI.Instance;
                GameAIManagerAPI ai = GameAIManagerAPI.Instance;
                for (int playerId = 1; playerId <= 8; playerId++)
                {
                    if (!aiv.TryGetVillageByPlayerId(playerId, out _))
                        continue;
                    string lordName = ai.GetCustomAILordNameByPlayerId(playerId);
                    if (!FixesInspection.TryGetFlags(playerId, out string flags, out string reason))
                    {
                        Log.LogWarning($"[FIXES-FLAGS] player={playerId} INCONCLUSIVE: {reason}");
                        continue;
                    }
                    if (!FixesInspection.TryGetPreferencePresence(lordName, out bool hasPreferences))
                    {
                        Log.LogWarning($"[FIXES-FLAGS] player={playerId} lord={lordName} flags={flags} preference lookup unavailable");
                        continue;
                    }
                    bool enabled = flags.Split(' ').Any(value => value.EndsWith("=True", StringComparison.Ordinal));
                    bool transitioned = previousPreferencePresence[playerId] && previousEnabledFlags[playerId] &&
                        !hasPreferences && enabled &&
                        !string.Equals(previousLordNames[playerId], lordName, StringComparison.Ordinal);
                    string result = transitioned ? "REPRODUCED: prior lord's enabled flag survived a different lord without preferences" :
                        !hasPreferences && enabled ? "SUSPECT: enabled flag without current preferences; prior map not observed" : "OBSERVED";
                    Log.LogInfo($"[FIXES-FLAGS] mapSequence={mapSequence} player={playerId} lord={lordName} hasPreferences={hasPreferences} previousLord={previousLordNames[playerId] ?? "<none>"} previousHasPreferences={previousPreferencePresence[playerId]} {flags} result={result}");
                    previousLordNames[playerId] = lordName;
                    previousPreferencePresence[playerId] = hasPreferences;
                    previousEnabledFlags[playerId] = enabled;
                }
            }
            catch (Exception ex)
            {
                Log.LogError("[FIXES-FLAGS] INCONCLUSIVE: " + ex);
            }
        }
    }
}
