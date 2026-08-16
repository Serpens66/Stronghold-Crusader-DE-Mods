using BepInEx;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Zhuqiaomon.Memory;

namespace HunterQueryTargetDiagnostic
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class HunterQueryTargetDiagnosticPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string PluginGuid = "HunterQueryTargetDiagnostic_Serp";
        private const string PluginName = "Hunter Query Target Diagnostic";
        private const string PluginVersion = "1.4.1";

        private const int BaselineDetailLimit = 12;
        private const int SuspiciousDetailLimit = 160;
        private const int CallbackErrorLimit = 5;
        private static readonly long SummaryInterval = Stopwatch.Frequency * 5;

        private static ManualLogSource diagnosticLog;
        private static IDisposable eventSubscription;
        private static HunterState7CauseDiagnostic state7CauseDiagnostic;
        private static bool libraryLoadedSubscriptionInstalled;
        private static bool hookConfirmed;
        private static bool conclusiveBugMarkerLogged;
        private static long nextSummaryTimestamp;
        private static long eventCount;
        private static long validLiveHunterCount;
        private static long suspiciousHunterCount;
        private static long singleState7CorrelationCount;
        private static long state7QueryCount;
        private static long state7EntryEpisodeCount;
        private static long state7ValidActorCount;
        private static long state7WrongActorCount;
        private static long state7UncorrelatedActorCount;
        private static long state7InvalidLinkedIdentityEpisodeCount;
        private static long state7MovementFailureCandidateEpisodeCount;
        private static long state7IndeterminateCauseEpisodeCount;
        private static int baselineDetailCount;
        private static int suspiciousDetailCount;
        private static int callbackErrorCount;
        private static bool state7ObservedEver;
        private static string previousState7Outcome = string.Empty;
        private static readonly HashSet<uint> previousState7HunterGlobalIds = new HashSet<uint>();

        private void Awake()
        {
            diagnosticLog = Logger;
            LogInfo($"{PluginName} {PluginVersion} loaded; observerOnly=true, eventMutation=false.");

            if (libraryLoadedSubscriptionInstalled)
                return;

            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
            libraryLoadedSubscriptionInstalled = true;
        }

        private static void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (eventSubscription != null || state7CauseDiagnostic != null)
                return;

            try
            {
                eventSubscription = UnitR3EventHooks.OnUnitHunterQueryTarget.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(OnHunterQueryTarget);

                LogInfo(
                    "Hunter query event diagnostic subscribed and always active; " +
                    "state7AtQueryBoundaryInspection=true, eventValuesAreObservedOnly=true, " +
                    "expectedProblemPath=unitType6/r_AIState7/RVA0x194164.");
            }
            catch (Exception exception)
            {
                LogInfo($"DIAGNOSTIC_INITIALIZATION_ERROR: {exception}");
            }

            try
            {
                bool referenceHashMatches = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                    diagnosticLog,
                    PluginName,
                    requireCurrentVersion: false);
                state7CauseDiagnostic = new HunterState7CauseDiagnostic(
                    diagnosticLog,
                    memory,
                    unchecked((ulong)libraryHandle.ToInt64()),
                    referenceHashMatches);
            }
            catch (Exception exception)
            {
                LogInfo($"STATE7_CAUSE_DIAGNOSTIC_INITIALIZATION_ERROR: {exception}");
            }
        }

        private static void OnHunterQueryTarget(UnitHunterQueryTargetEventArgs args)
        {
            try
            {
                InspectEvent(args);
            }
            catch (Exception exception)
            {
                callbackErrorCount++;
                if (callbackErrorCount <= CallbackErrorLimit)
                {
                    LogInfo(
                        $"CALLBACK_ERROR: count={callbackErrorCount}/{CallbackErrorLimit}, " +
                        $"event={eventCount + 1}, exception={exception}");
                }
            }
        }

        private static unsafe void InspectEvent(UnitHunterQueryTargetEventArgs args)
        {
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            SimpleNativeArray<GameUnit> units = unitApi.GetUnitArray();
            eventCount++;

            bool reportedHunterIsLive = TryGetLiveHunter(
                unitApi,
                args.HunterUnitId,
                out GameUnit* reportedHunter);
            bool reportedHunterIsState7 =
                reportedHunterIsLive && reportedHunter->r_AIState == 7;
            if (reportedHunterIsLive)
                validLiveHunterCount++;
            else
                suspiciousHunterCount++;

            List<UnitSnapshot> state7Hunters = CaptureState7Hunters(units);
            if (!reportedHunterIsLive && state7Hunters.Count == 1)
                singleState7CorrelationCount++;

            ulong unitManagerAddress = unchecked((ulong)unitApi.GetUnitManager().Pointer);
            uint unitManagerLow32 = unchecked((uint)unitManagerAddress);
            uint reportedHunterBits = unchecked((uint)args.HunterUnitId);
            bool matchesUnitManagerLow32 = reportedHunterBits == unitManagerLow32;

            string query = DescribeUnit(unitApi, args.QueryUnitId);
            string state7 = DescribeState7Hunters(state7Hunters);
            string classification = reportedHunterIsLive ? "VALID_LIVE_HUNTER" : "SUSPECTED_MISMATCH";

            TrackState7QueryOutcome(
                args,
                state7Hunters,
                reportedHunterIsLive,
                reportedHunterIsState7,
                reportedHunterBits,
                unitManagerAddress,
                unitManagerLow32,
                matchesUnitManagerLow32,
                query,
                state7);

            if (!hookConfirmed)
            {
                hookConfirmed = true;
                LogInfo(
                    $"HOOK_CONFIRMED: event={eventCount}, reportedHunterId={args.HunterUnitId}, " +
                    $"reportedHunterLive={reportedHunterIsLive}, query={query}, " +
                    $"state7Hunters={state7Hunters.Count}/{state7}.");
            }

            bool shouldLogDetail = reportedHunterIsLive
                ? baselineDetailCount < BaselineDetailLimit
                : suspiciousDetailCount < SuspiciousDetailLimit;

            if (shouldLogDetail)
            {
                if (reportedHunterIsLive)
                    baselineDetailCount++;
                else
                    suspiciousDetailCount++;

                LogInfo(
                    $"{classification}: event={eventCount}, " +
                    $"reportedHunter=(id={args.HunterUnitId}/hex=0x{reportedHunterBits:X8}/" +
                    $"liveHunter={reportedHunterIsLive}), query={query}, " +
                    $"unitManager=(address=0x{unitManagerAddress:X16}/low32=0x{unitManagerLow32:X8}/" +
                    $"matchesReported={matchesUnitManagerLow32}), " +
                    $"state7Hunters={state7Hunters.Count}/{state7}.");
            }

            if (!reportedHunterIsLive && matchesUnitManagerLow32 && !conclusiveBugMarkerLogged)
            {
                conclusiveBugMarkerLogged = true;
                LogInfo(
                    $"SCRIPT_EXTENDER_BUG_REPRODUCED: event={eventCount}, " +
                    $"reportedHunterId=0x{reportedHunterBits:X8} equals UnitManager.low32, " +
                    $"reportedHunterIsNotLive=true, state7Hunters={state7Hunters.Count}/{state7}, " +
                    $"query={query}.");
            }

            long now = Stopwatch.GetTimestamp();
            if (eventCount == 1 || now >= nextSummaryTimestamp || !reportedHunterIsLive)
            {
                nextSummaryTimestamp = now + SummaryInterval;
                bool classificationInvariant =
                    eventCount == validLiveHunterCount + suspiciousHunterCount;

                LogInfo(
                    $"SUMMARY: events={eventCount}, validLiveHunterIds={validLiveHunterCount}, " +
                    $"suspiciousHunterIds={suspiciousHunterCount}, " +
                    $"classificationInvariant={classificationInvariant}, " +
                    $"singleState7Correlations={singleState7CorrelationCount}, " +
                    $"state7ObservedEver={state7ObservedEver}, " +
                    $"state7Queries={state7QueryCount}, state7EntryEpisodes={state7EntryEpisodeCount}, " +
                    $"state7CauseEpisodes=(invalidLinkedIdentity={state7InvalidLinkedIdentityEpisodeCount}/" +
                    $"movementFailureCandidate={state7MovementFailureCandidateEpisodeCount}/" +
                    $"indeterminate={state7IndeterminateCauseEpisodeCount}), " +
                    $"state7Outcomes=(validActor={state7ValidActorCount}/" +
                    $"wrongActor={state7WrongActorCount}/" +
                    $"uncorrelatedActor={state7UncorrelatedActorCount}), " +
                    $"detailLogs={baselineDetailCount}+{suspiciousDetailCount}, " +
                    $"callbackErrors={callbackErrorCount}.");
            }
        }

        private static void TrackState7QueryOutcome(
            UnitHunterQueryTargetEventArgs args,
            List<UnitSnapshot> state7Hunters,
            bool reportedHunterIsLive,
            bool reportedHunterIsState7,
            uint reportedHunterBits,
            ulong unitManagerAddress,
            uint unitManagerLow32,
            bool matchesUnitManagerLow32,
            string query,
            string state7Description)
        {
            HashSet<uint> currentState7GlobalIds = new HashSet<uint>();
            List<UnitSnapshot> newEntries = new List<UnitSnapshot>();
            for (int index = 0; index < state7Hunters.Count; index++)
            {
                UnitSnapshot hunter = state7Hunters[index];
                currentState7GlobalIds.Add(hunter.GlobalId);
                if (!previousState7HunterGlobalIds.Contains(hunter.GlobalId))
                    newEntries.Add(hunter);
            }

            previousState7HunterGlobalIds.Clear();
            foreach (uint globalId in currentState7GlobalIds)
                previousState7HunterGlobalIds.Add(globalId);

            if (state7Hunters.Count == 0)
            {
                previousState7Outcome = string.Empty;
                return;
            }

            state7ObservedEver = true;
            state7QueryCount++;
            if (newEntries.Count > 0)
            {
                state7EntryEpisodeCount++;
                for (int index = 0; index < newEntries.Count; index++)
                {
                    UnitSnapshot entry = newEntries[index];
                    if (entry.NativeInvalidLinkedIdentityCondition)
                        state7InvalidLinkedIdentityEpisodeCount++;
                    else if (entry.MovementFailureCandidateCondition)
                        state7MovementFailureCandidateEpisodeCount++;
                    else
                        state7IndeterminateCauseEpisodeCount++;
                }
            }

            string outcome;
            if (reportedHunterIsState7)
            {
                outcome = "STATE7_WITH_VALID_HUNTER_ID";
                state7ValidActorCount++;
            }
            else if (!reportedHunterIsLive)
            {
                outcome = matchesUnitManagerLow32
                    ? "STATE7_WITH_WRONG_HUNTER_ID_UNIT_MANAGER_LOW32"
                    : "STATE7_WITH_WRONG_HUNTER_ID_OTHER";
                state7WrongActorCount++;
            }
            else
            {
                // Another Hunter can be in State 7 while the reported live actor
                // is using one of the five unaffected native caller paths.
                outcome = "STATE7_PRESENT_WITH_DIFFERENT_VALID_HUNTER";
                state7UncorrelatedActorCount++;
            }

            bool outcomeChanged = !string.Equals(
                previousState7Outcome,
                outcome,
                StringComparison.Ordinal);
            previousState7Outcome = outcome;

            if (newEntries.Count == 0 && !outcomeChanged)
                return;

            LogInfo(
                $"{outcome}: event={eventCount}, state7AtQueryBoundary=true, " +
                $"newState7Entries={newEntries.Count}/{DescribeState7Hunters(newEntries)}, " +
                $"reportedHunter=(id={args.HunterUnitId}/hex=0x{reportedHunterBits:X8}/" +
                $"liveHunter={reportedHunterIsLive}/state7={reportedHunterIsState7}), " +
                $"unitManager=(address=0x{unitManagerAddress:X16}/low32=0x{unitManagerLow32:X8}/" +
                $"matchesReported={matchesUnitManagerLow32}), " +
                $"allState7Hunters={state7Hunters.Count}/{state7Description}, query={query}.");
        }

        private static unsafe bool TryGetLiveHunter(
            GameUnitManagerAPI unitApi,
            int unitId,
            out GameUnit* hunter)
        {
            hunter = null;
            return unitApi.IsValidId(unitId) &&
                unitApi.TryGetUnitById(unitId, out hunter) &&
                hunter != null &&
                hunter->r_AliveState == AliveState.IsAlive &&
                hunter->r_UnitChimp == eChimps.CHIMP_TYPE_HUNTER;
        }

        private static unsafe List<UnitSnapshot> CaptureState7Hunters(
            SimpleNativeArray<GameUnit> units)
        {
            List<UnitSnapshot> snapshots = new List<UnitSnapshot>();
            if (units._array == null || units.Length <= 0)
                return snapshots;

            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit == null ||
                    unit->r_AliveState == AliveState.None ||
                    unit->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER ||
                    unit->r_AIState != 7)
                {
                    continue;
                }

                snapshots.Add(new UnitSnapshot(index + 1, unit, includeHunterDiagnostics: true));
            }

            return snapshots;
        }

        private static unsafe string DescribeUnit(GameUnitManagerAPI unitApi, int unitId)
        {
            if (!unitApi.IsValidId(unitId) ||
                !unitApi.TryGetUnitById(unitId, out GameUnit* unit) ||
                unit == null)
            {
                return $"id={unitId}/valid=false";
            }

            return new UnitSnapshot(unitId, unit).ToString();
        }

        private static string DescribeState7Hunters(List<UnitSnapshot> hunters)
        {
            if (hunters.Count == 0)
                return "[]";

            return "[" + string.Join(";", hunters) + "]";
        }

        private static void LogInfo(string message)
        {
            diagnosticLog?.LogInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
        }

        private readonly struct UnitSnapshot
        {
            // The audited Hunter path reads these two still-unnamed building fields as
            // a signed unit ID and its expected global identity.
            private const int LinkedBuildingWorkerUnitIdOffset = 0xA0;
            private const int LinkedBuildingWorkerGlobalIdOffset = 0xAC;

            private readonly int unitId;
            private readonly uint globalId;
            private readonly eChimps type;
            private readonly AliveState aliveState;
            private readonly ushort aiState;
            private readonly byte player;
            private readonly ushort tileX;
            private readonly ushort tileY;
            private readonly ushort targetTileX;
            private readonly ushort targetTileY;
            private readonly ushort previousTileX;
            private readonly ushort previousTileY;
            private readonly ushort pathFlags;
            private readonly ushort movingRelevant;
            private readonly ushort currentPathPosition;
            private readonly uint pathSize;
            private readonly ushort contextTargetUnitId;
            private readonly uint contextTargetGlobalId;
            private readonly short unknown2A2;
            private readonly bool hunterDiagnosticsIncluded;
            private readonly ushort linkedBuildingId;
            private readonly bool linkedBuildingIdValid;
            private readonly bool linkedBuildingSlotResolved;
            private readonly AliveState linkedBuildingAliveState;
            private readonly eStructs linkedBuildingType;
            private readonly uint linkedBuildingGlobalId;
            private readonly int linkedBuildingWorkerUnitId;
            private readonly uint linkedBuildingWorkerExpectedGlobalId;
            private readonly bool linkedBuildingWorkerUnitResolved;
            private readonly uint linkedBuildingWorkerActualGlobalId;
            private readonly bool linkedBuildingWorkerIdentityMatches;
            private readonly bool linkedBuildingPointsToThisHunter;
            private readonly bool nativeInvalidLinkedIdentityCondition;
            private readonly bool movementFailureCandidateCondition;

            public uint GlobalId => globalId;
            public bool NativeInvalidLinkedIdentityCondition => nativeInvalidLinkedIdentityCondition;
            public bool MovementFailureCandidateCondition => movementFailureCandidateCondition;

            public unsafe UnitSnapshot(
                int id,
                GameUnit* unit,
                bool includeHunterDiagnostics = false)
            {
                unitId = id;
                globalId = unit->r_GlobalId;
                type = unit->r_UnitChimp;
                aliveState = unit->r_AliveState;
                aiState = unit->r_AIState;
                player = unit->r_ControllableForPlayerId;
                tileX = unit->r_CurrentTilePositionX;
                tileY = unit->r_CurrentTilePositionY;
                targetTileX = unit->r_TargetTilePositionX;
                targetTileY = unit->r_TargetTilePositionY;
                previousTileX = unit->r_PreviousTilePositionX;
                previousTileY = unit->r_PreviousTilePositionY;
                pathFlags = unit->r_PathPlanStateBitFlags;
                movingRelevant = unit->r_MovingRelevant;
                currentPathPosition = unit->p_CurrentPathPlanPosition;
                pathSize = unit->p_PathPlanSize;
                contextTargetUnitId = unit->r_AI_ContextTargetUnitId;
                contextTargetGlobalId = unit->r_AI_ContextTargetUnitGlobalId;
                unknown2A2 = *(short*)((byte*)unit + 0x2A2);

                hunterDiagnosticsIncluded = includeHunterDiagnostics;
                linkedBuildingId = unit->r_LinkedProductionBuildingId;
                linkedBuildingIdValid = false;
                linkedBuildingSlotResolved = false;
                linkedBuildingAliveState = AliveState.None;
                linkedBuildingType = default;
                linkedBuildingGlobalId = 0;
                linkedBuildingWorkerUnitId = 0;
                linkedBuildingWorkerExpectedGlobalId = 0;
                linkedBuildingWorkerUnitResolved = false;
                linkedBuildingWorkerActualGlobalId = 0;
                linkedBuildingWorkerIdentityMatches = false;
                linkedBuildingPointsToThisHunter = false;
                nativeInvalidLinkedIdentityCondition = false;
                movementFailureCandidateCondition = false;

                if (!includeHunterDiagnostics)
                    return;

                GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
                linkedBuildingIdValid = buildingApi.IsValidId(linkedBuildingId);
                if (!linkedBuildingIdValid ||
                    !buildingApi.TryGetBuildingById(linkedBuildingId, out GameBuilding* building) ||
                    building == null)
                {
                    return;
                }

                linkedBuildingSlotResolved = true;
                linkedBuildingAliveState = building->r_AliveState;
                linkedBuildingType = building->r_BuildingType;
                linkedBuildingGlobalId = building->r_GlobalId;
                linkedBuildingWorkerUnitId = *(short*)((byte*)building + LinkedBuildingWorkerUnitIdOffset);
                linkedBuildingWorkerExpectedGlobalId =
                    *(uint*)((byte*)building + LinkedBuildingWorkerGlobalIdOffset);

                GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
                GameUnit* linkedWorker = null;
                linkedBuildingWorkerUnitResolved =
                    unitApi.IsValidId(linkedBuildingWorkerUnitId) &&
                    unitApi.TryGetUnitById(linkedBuildingWorkerUnitId, out linkedWorker) &&
                    linkedWorker != null;
                if (linkedBuildingWorkerUnitResolved)
                {
                    linkedBuildingWorkerActualGlobalId = linkedWorker->r_GlobalId;
                    linkedBuildingWorkerIdentityMatches =
                        linkedBuildingWorkerActualGlobalId == linkedBuildingWorkerExpectedGlobalId;
                    linkedBuildingPointsToThisHunter =
                        linkedBuildingWorkerUnitId == id &&
                        linkedBuildingWorkerExpectedGlobalId == globalId;
                }

                nativeInvalidLinkedIdentityCondition =
                    linkedBuildingAliveState == AliveState.None &&
                    !linkedBuildingWorkerIdentityMatches;
                movementFailureCandidateCondition =
                    linkedBuildingAliveState == AliveState.None &&
                    linkedBuildingWorkerIdentityMatches;
            }

            public override string ToString()
            {
                string basic =
                    $"id={unitId}/global={globalId}/type={(int)type}:{type}/" +
                    $"alive={(int)aliveState}:{aliveState}/aiState={aiState}/" +
                    $"player={player}/tile={tileX},{tileY}";

                if (!hunterDiagnosticsIncluded)
                    return basic;

                return
                    basic +
                    $"/targetTile={targetTileX},{targetTileY}" +
                    $"/previousTile={previousTileX},{previousTileY}" +
                    $"/path=(flags={pathFlags}/moving={movingRelevant}/" +
                    $"position={currentPathPosition}/size={pathSize})" +
                    $"/contextTarget={contextTargetUnitId}:{contextTargetGlobalId}" +
                    $"/unknown2A2={unknown2A2}" +
                    $"/hunterPost=(id={linkedBuildingId}/idValid={linkedBuildingIdValid}/" +
                    $"slotResolved={linkedBuildingSlotResolved}/" +
                    $"alive={(int)linkedBuildingAliveState}:{linkedBuildingAliveState}/" +
                    $"type={(int)linkedBuildingType}:{linkedBuildingType}/global={linkedBuildingGlobalId}/" +
                    $"storedWorker={linkedBuildingWorkerUnitId}:{linkedBuildingWorkerExpectedGlobalId}/" +
                    $"workerResolved={linkedBuildingWorkerUnitResolved}/" +
                    $"actualWorkerGlobal={linkedBuildingWorkerActualGlobalId}/" +
                    $"identityMatches={linkedBuildingWorkerIdentityMatches}/" +
                    $"pointsToThisHunter={linkedBuildingPointsToThisHunter}/" +
                    $"nativeInvalidIdentityCondition={nativeInvalidLinkedIdentityCondition}/" +
                    $"movementFailureCandidate={movementFailureCandidateCondition})";
            }
        }
    }
}
