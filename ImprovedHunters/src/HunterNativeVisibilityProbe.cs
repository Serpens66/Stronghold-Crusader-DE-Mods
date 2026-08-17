using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Zhuqiaomon.Memory;

namespace ImprovedHunters
{
    /// <summary>
    /// Temporary, behavior-neutral runtime validation of Vanilla's native Hunter
    /// visibility helper. It installs no hook and mutates neither units nor orders.
    /// Keep this file separate so the complete probe can be removed after sign-off.
    /// </summary>
    internal sealed unsafe class HunterNativeVisibilityProbe : IDisposable
    {
        private const string ReferenceDllSha256 =
            "33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469";
        private const int VisibilityWrapperRva = 0xA06F0;
        private const int VisibilityCoreRva = 0x9E350;
        private const int HunterQueryCallRva = 0x18B052;
        private const int HunterDirectOrderCallRva = 0x18ED1A;
        private const int FirstCoreCallDisplacementRva = VisibilityWrapperRva + 0x54;
        private const int FirstCoreCallNextInstructionRva = VisibilityWrapperRva + 0x58;
        private const int ReverseCoreCallDisplacementRva = VisibilityWrapperRva + 0x79;
        private const int ReverseCoreCallNextInstructionRva = VisibilityWrapperRva + 0x7D;
        private const int HunterAiStateOffset = 0x2BC;
        private const int HunterTargetUnitIdOffset = 0x39A;
        private const int HunterTargetGlobalIdOffset = 0x39C;
        private const int MaxProbeLogs = 120;
        private const int MaxProbesPerScan = 4;

        private const string WrapperEntryPattern =
            "48 8B C4 48 89 58 08 48 89 68 10 48 89 70 18 48 89 78 20 41 54 41 56 41 57 48 83 EC 40";
        private const string FirstCoreCallPattern =
            "C7 40 E0 00 00 00 00 48 8B E9 44 89 70 D8 44 89 78 D0 44 89 60 C8 E8 ? ? ? ? 85 C0 75 21";
        private const string ReverseCoreCallPattern =
            "89 44 24 38 45 8B CE 89 5C 24 30 45 8B C7 89 7C 24 28 41 8B D4 48 8B CD 89 74 24 20 E8 ? ? ? ?";
        private const string HunterQueryCallPattern =
            "E8 ? ? ? ? FF C8 3D AF 01 00 00 77 1F";
        private const string HunterDirectOrderCallPattern =
            "E8 ? ? ? ? 8B D0 85 C0 0F 8E EB 00 00 00";

        private static readonly long QueryBurstGap = Stopwatch.Frequency / 4;
        private static readonly long QueryQuietPeriod = Stopwatch.Frequency / 20;
        private static readonly long QueryLifetime = Stopwatch.Frequency * 2;
        private static readonly long RepeatInterval = Stopwatch.Frequency * 2;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NativeVisibilityDelegate(
            IntPtr context,
            int startX,
            int startY,
            int startHeight,
            int endX,
            int endY,
            int endHeight);

        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly object stateLock = new object();
        private readonly Dictionary<int, QueryBatch> queryBatches = new Dictionary<int, QueryBatch>();
        private readonly Dictionary<ProbeIdentity, ProbeObservation> observations =
            new Dictionary<ProbeIdentity, ProbeObservation>();
        private NativeVisibilityDelegate visibility;
        private int probeInProgress;
        private int probeLogs;
        private bool invocationConfirmed;
        private bool reentrancyLogged;
        private bool threadMismatchLogged;
        private bool captureFailureLogged;
        private bool invocationFailureLogged;
        private bool disabled;
        private bool disposed;

        public HunterNativeVisibilityProbe(
            ManualLogSource log,
            ImprovedHuntersViewModel settings,
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            if (!referenceHashMatches)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Improved Hunters native visibility probe unavailable: " +
                    $"DLL hash differs from audited SHA-256 {ReferenceDllSha256}; behavior remains unchanged.");
                return;
            }

            ValidateNativeCallChain(memory);
            if (imageBase == 0)
                throw new InvalidOperationException("native image base is zero.");
            if (IntPtr.Size != sizeof(long))
                throw new PlatformNotSupportedException("native visibility probe requires the 64-bit game process.");

            ulong wrapperAddress = checked(imageBase + (ulong)VisibilityWrapperRva);
            visibility = Marshal.GetDelegateForFunctionPointer<NativeVisibilityDelegate>(
                new IntPtr(unchecked((long)wrapperAddress)));

            Shared.DebugLogHelper.LogInfo(
                log,
                "Improved Hunters native visibility probe initialized: " +
                $"mode=reference-rva, wrapperRva=0x{VisibilityWrapperRva:X}, coreRva=0x{VisibilityCoreRva:X}, " +
                "privateContextBytes=16, contextGuards=True, nativeHooks=False, behaviorNeutral=True.");
        }

        public bool IsAvailable => visibility != null && !disabled && !disposed;

        public bool TryEvaluateDirectVisibility(
            int hunterUnitId,
            uint hunterGlobalId,
            int preyUnitId,
            uint preyGlobalId,
            eChimps preyType,
            out int result)
        {
            result = 0;
            if (!IsAvailable ||
                !settings.EnableMod ||
                !settings.ImprovedPathfinding ||
                !settings.IsHuntingEnabled(preyType) ||
                hunterUnitId <= 0 ||
                preyUnitId <= 0 ||
                hunterGlobalId == 0 ||
                preyGlobalId == 0)
            {
                return false;
            }

            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (!unitApi.TryGetUnitById(hunterUnitId, out GameUnit* hunter) ||
                !unitApi.TryGetUnitById(preyUnitId, out GameUnit* prey) ||
                hunter == null ||
                prey == null ||
                hunter->r_GlobalId != hunterGlobalId ||
                prey->r_GlobalId != preyGlobalId ||
                hunter->r_AliveState != AliveState.IsAlive ||
                prey->r_AliveState != AliveState.IsAlive ||
                hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER ||
                prey->r_UnitChimp != preyType)
            {
                return false;
            }

            return TryInvokeVisibility(hunter, prey, out result, out _);
        }

        public void RecordQueryCandidate(
            int hunterUnitId,
            int preyUnitId,
            eChimps preyType,
            uint preyGlobalId,
            long timestamp)
        {
            if (!IsAvailable ||
                !settings.EnableMod ||
                !settings.ImprovedPathfinding ||
                !settings.IsHuntingEnabled(preyType) ||
                hunterUnitId <= 0 ||
                preyUnitId <= 0 ||
                preyGlobalId == 0)
            {
                return;
            }

            try
            {
                GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
                if (!unitApi.TryGetUnitById(hunterUnitId, out GameUnit* hunter) ||
                    !unitApi.TryGetUnitById(preyUnitId, out GameUnit* prey) ||
                    hunter == null ||
                    prey == null ||
                    hunter->r_AliveState != AliveState.IsAlive ||
                    hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER ||
                    prey->r_AliveState != AliveState.IsAlive ||
                    prey->r_UnitChimp != preyType ||
                    prey->r_GlobalId != preyGlobalId ||
                    hunter->r_GlobalId == 0)
                {
                    return;
                }

                int manhattanDistance = Math.Abs(
                    (int)prey->r_CurrentTilePositionX - hunter->r_CurrentTilePositionX) +
                    Math.Abs((int)prey->r_CurrentTilePositionY - hunter->r_CurrentTilePositionY);
                QueryPhase phase = ClassifyQueryPhase(manhattanDistance);
                if (phase == QueryPhase.NotQueried)
                    return;

                QueryCandidate candidate = new QueryCandidate(
                    hunterUnitId,
                    hunter->r_GlobalId,
                    preyUnitId,
                    preyGlobalId,
                    preyType,
                    manhattanDistance,
                    phase,
                    Thread.CurrentThread.ManagedThreadId);

                lock (stateLock)
                {
                    if (!queryBatches.TryGetValue(hunterUnitId, out QueryBatch batch) ||
                        timestamp - batch.LastCandidateTimestamp > QueryBurstGap ||
                        batch.HunterGlobalId != hunter->r_GlobalId)
                    {
                        batch = new QueryBatch(hunter->r_GlobalId);
                        queryBatches[hunterUnitId] = batch;
                    }

                    batch.LastCandidateTimestamp = timestamp;
                    batch.SetNearest(candidate);
                }
            }
            catch (Exception exception)
            {
                LogFailureOnce("candidate capture", exception, disableProbe: false);
            }
        }

        public void ProcessNativeScan(SimpleNativeArray<GameUnit> units, long timestamp)
        {
            if (!IsAvailable ||
                !settings.EnableMod ||
                !settings.ImprovedPathfinding ||
                units._array == null ||
                units.Length == 0 ||
                probeLogs >= MaxProbeLogs)
            {
                return;
            }

            List<QueryCandidate> requests = new List<QueryCandidate>(MaxProbesPerScan);
            lock (stateLock)
            {
                List<int> completedHunters = new List<int>();
                foreach (KeyValuePair<int, QueryBatch> pair in queryBatches)
                {
                    QueryBatch batch = pair.Value;
                    long age = timestamp - batch.LastCandidateTimestamp;
                    if (age < QueryQuietPeriod)
                        continue;

                    completedHunters.Add(pair.Key);
                    if (age > QueryLifetime)
                        continue;

                    AddRequestIfDue(batch.FirstPass, timestamp, requests);
                    AddRequestIfDue(batch.SecondPass, timestamp, requests);
                    AddRequestIfDue(batch.FarPass, timestamp, requests);
                    if (requests.Count >= MaxProbesPerScan)
                        break;
                }

                foreach (int hunterUnitId in completedHunters)
                    queryBatches.Remove(hunterUnitId);
            }

            foreach (QueryCandidate request in requests)
            {
                if (!IsAvailable || probeLogs >= MaxProbeLogs)
                    break;

                Probe(units, request, timestamp);
            }
        }

        public void ResetForMap()
        {
            lock (stateLock)
            {
                queryBatches.Clear();
                observations.Clear();
            }

            probeLogs = 0;
            invocationConfirmed = false;
            reentrancyLogged = false;
            threadMismatchLogged = false;
            captureFailureLogged = false;
            invocationFailureLogged = false;
            probeInProgress = 0;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            visibility = null;
            lock (stateLock)
            {
                queryBatches.Clear();
                observations.Clear();
            }
        }

        private static void ValidateNativeCallChain(ReadOnlySpan<byte> memory)
        {
            RequirePattern(memory, VisibilityWrapperRva, WrapperEntryPattern, "visibility wrapper entry");
            RequirePattern(
                memory,
                VisibilityWrapperRva + 0x3D,
                FirstCoreCallPattern,
                "visibility wrapper first core call and private-context write setup");
            RequirePattern(
                memory,
                VisibilityWrapperRva + 0x5C,
                ReverseCoreCallPattern,
                "visibility wrapper reverse core call");
            RequirePattern(memory, HunterQueryCallRva, HunterQueryCallPattern, "Hunter query visibility call");
            RequirePattern(
                memory,
                HunterDirectOrderCallRva,
                HunterDirectOrderCallPattern,
                "Hunter direct-order visibility call");

            RequireRelativeTarget(
                memory,
                FirstCoreCallDisplacementRva,
                FirstCoreCallNextInstructionRva,
                VisibilityCoreRva,
                "visibility wrapper first core target");
            RequireRelativeTarget(
                memory,
                ReverseCoreCallDisplacementRva,
                ReverseCoreCallNextInstructionRva,
                VisibilityCoreRva,
                "visibility wrapper reverse core target");
            RequireRelativeTarget(
                memory,
                HunterQueryCallRva + 1,
                HunterQueryCallRva + 5,
                VisibilityWrapperRva,
                "Hunter query wrapper target");
            RequireRelativeTarget(
                memory,
                HunterDirectOrderCallRva + 1,
                HunterDirectOrderCallRva + 5,
                VisibilityWrapperRva,
                "Hunter direct-order wrapper target");
        }

        private static void RequirePattern(
            ReadOnlySpan<byte> memory,
            int rva,
            string pattern,
            string name)
        {
            if (!Shared.NativePatternResolver.MatchesPatternAt(memory, rva, pattern))
                throw new InvalidOperationException($"{name} failed byte validation at RVA 0x{rva:X}.");
        }

        private static void RequireRelativeTarget(
            ReadOnlySpan<byte> memory,
            int displacementRva,
            int nextInstructionRva,
            int expectedTargetRva,
            string name)
        {
            int targetRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                displacementRva,
                nextInstructionRva);
            if (targetRva != expectedTargetRva)
            {
                throw new InvalidOperationException(
                    $"{name} resolved RVA 0x{targetRva:X}, expected 0x{expectedTargetRva:X}.");
            }
        }

        private void AddRequestIfDue(
            QueryCandidate? candidate,
            long timestamp,
            List<QueryCandidate> requests)
        {
            if (!candidate.HasValue || requests.Count >= MaxProbesPerScan)
                return;

            QueryCandidate value = candidate.Value;
            ProbeIdentity identity = new ProbeIdentity(
                value.HunterUnitId,
                value.HunterGlobalId,
                value.PreyUnitId,
                value.PreyGlobalId);
            if (observations.TryGetValue(identity, out ProbeObservation previous) &&
                timestamp - previous.Timestamp < RepeatInterval)
            {
                return;
            }

            requests.Add(value);
            observations[identity] = new ProbeObservation(timestamp);
        }

        private void Probe(
            SimpleNativeArray<GameUnit> units,
            QueryCandidate request,
            long timestamp)
        {
            if (request.HunterUnitId > units.Length || request.PreyUnitId > units.Length)
                return;

            GameUnit* hunter = units.GetValuePointer(request.HunterUnitId - 1);
            GameUnit* prey = units.GetValuePointer(request.PreyUnitId - 1);
            if (hunter == null ||
                prey == null ||
                hunter->r_GlobalId != request.HunterGlobalId ||
                prey->r_GlobalId != request.PreyGlobalId ||
                hunter->r_AliveState != AliveState.IsAlive ||
                prey->r_AliveState != AliveState.IsAlive ||
                hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER ||
                prey->r_UnitChimp != request.PreyType ||
                !settings.IsHuntingEnabled(request.PreyType))
            {
                return;
            }

            int currentThreadId = Thread.CurrentThread.ManagedThreadId;
            if (currentThreadId != request.QueryThreadId)
            {
                if (!threadMismatchLogged)
                {
                    threadMismatchLogged = true;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "Improved Hunters native visibility probe skipped a non-query-thread scan: " +
                        $"queryThread={request.QueryThreadId}, scanThread={currentThreadId}; behavior remains unchanged.");
                }
                return;
            }

            if (!TryInvokeVisibility(hunter, prey, out int result, out int contextScratch))
                return;

            int hunterX = hunter->r_CurrentWorldPositionX;
            int hunterY = hunter->r_CurrentWorldPositionY;
            int hunterHeight = hunter->r_HeightElevation + hunter->N0000006A + 30;
            int preyX = prey->r_CurrentWorldPositionX;
            int preyY = prey->r_CurrentWorldPositionY;
            int preyHeight = prey->r_HeightElevation + prey->N0000006A + 26;

            if (!invocationConfirmed)
            {
                invocationConfirmed = true;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Improved Hunters native visibility probe invocation confirmed: " +
                    $"hunter={request.HunterUnitId}/{request.HunterGlobalId}, " +
                    $"prey={request.PreyUnitId}/{request.PreyGlobalId}/{request.PreyType}, " +
                    $"managedThread={currentThreadId}, privateContext=True.");
            }

            byte* hunterBytes = (byte*)hunter;
            int currentManhattanDistance = Math.Abs(
                (int)prey->r_CurrentTilePositionX - hunter->r_CurrentTilePositionX) +
                Math.Abs((int)prey->r_CurrentTilePositionY - hunter->r_CurrentTilePositionY);
            probeLogs++;
            Shared.DebugLogHelper.LogInfo(
                log,
                "Improved Hunters native visibility probe result: " +
                $"hunter={request.HunterUnitId}/{request.HunterGlobalId}, " +
                $"prey={request.PreyUnitId}/{request.PreyGlobalId}/{request.PreyType}, " +
                $"queryPhase={DescribePhase(request.Phase)}, capturedManhattan={request.ManhattanDistance}, " +
                $"currentManhattan={currentManhattanDistance}, nativeResult={result}, " +
                $"queryVisibilityPass={result >= 1 && result <= 432}, directOrderPass={result > 0}, " +
                $"contextScratch={contextScratch}, contextGuardsIntact=True, " +
                $"nativeTarget={*(ushort*)(hunterBytes + HunterTargetUnitIdOffset)}/" +
                $"{*(uint*)(hunterBytes + HunterTargetGlobalIdOffset)}, " +
                $"hunterAiState={*(ushort*)(hunterBytes + HunterAiStateOffset)}, " +
                $"hunterWorld={hunterX},{hunterY},{hunterHeight}, preyWorld={preyX},{preyY},{preyHeight}, " +
                $"physicalArrowPath=not-probed, behaviorMutation=False ({probeLogs}/{MaxProbeLogs}).");
        }

        private bool TryInvokeVisibility(
            GameUnit* hunter,
            GameUnit* prey,
            out int result,
            out int contextScratch)
        {
            result = 0;
            contextScratch = 0;
            if (Interlocked.CompareExchange(ref probeInProgress, 1, 0) != 0)
            {
                if (!reentrancyLogged)
                {
                    reentrancyLogged = true;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "Improved Hunters native visibility probe skipped a reentrant invocation; behavior remains unchanged.");
                }
                return false;
            }

            try
            {
                int* guardedBuffer = stackalloc int[8];
                guardedBuffer[0] = unchecked((int)0x13579BDF);
                guardedBuffer[1] = unchecked((int)0x2468ACE0);
                guardedBuffer[2] = 0;
                guardedBuffer[3] = 0;
                guardedBuffer[4] = 0;
                guardedBuffer[5] = 0;
                guardedBuffer[6] = unchecked((int)0x55AA33CC);
                guardedBuffer[7] = unchecked((int)0xAA55CC33);

                result = visibility(
                    (IntPtr)(guardedBuffer + 2),
                    hunter->r_CurrentWorldPositionX,
                    hunter->r_CurrentWorldPositionY,
                    hunter->r_HeightElevation + hunter->N0000006A + 30,
                    prey->r_CurrentWorldPositionX,
                    prey->r_CurrentWorldPositionY,
                    prey->r_HeightElevation + prey->N0000006A + 26);

                bool contextGuardsIntact =
                    guardedBuffer[0] == unchecked((int)0x13579BDF) &&
                    guardedBuffer[1] == unchecked((int)0x2468ACE0) &&
                    guardedBuffer[2] == 0 &&
                    guardedBuffer[3] == 0 &&
                    guardedBuffer[4] == 0 &&
                    guardedBuffer[6] == unchecked((int)0x55AA33CC) &&
                    guardedBuffer[7] == unchecked((int)0xAA55CC33);
                contextScratch = guardedBuffer[5];
                if (contextGuardsIntact)
                    return true;

                disabled = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    "Improved Hunters native visibility probe disabled: the private context guard changed outside context+0xC; " +
                    "no Hunter state or order was modified.");
                return false;
            }
            catch (Exception exception)
            {
                LogFailureOnce("native invocation", exception, disableProbe: true);
                return false;
            }
            finally
            {
                Volatile.Write(ref probeInProgress, 0);
            }
        }

        private void LogFailureOnce(string operation, Exception exception, bool disableProbe)
        {
            if (disableProbe)
            {
                disabled = true;
                if (invocationFailureLogged)
                    return;
                invocationFailureLogged = true;
            }
            else
            {
                if (captureFailureLogged)
                    return;
                captureFailureLogged = true;
            }

            Shared.DebugLogHelper.LogError(
                log,
                $"Improved Hunters native visibility probe {operation} failed; " +
                $"disabled={disabled}, Hunter behavior remains unchanged: {exception}");
        }

        private static QueryPhase ClassifyQueryPhase(int manhattanDistance)
        {
            if (manhattanDistance >= 54)
                return QueryPhase.FarQueryBypass;
            if (manhattanDistance > 20)
                return QueryPhase.FirstPassLineOfSight;
            if (manhattanDistance > 5)
                return QueryPhase.SecondPassLineOfSight;
            return QueryPhase.NotQueried;
        }

        private static string DescribePhase(QueryPhase phase)
        {
            switch (phase)
            {
                case QueryPhase.FirstPassLineOfSight:
                    return "first-pass-los";
                case QueryPhase.SecondPassLineOfSight:
                    return "second-pass-los";
                case QueryPhase.FarQueryBypass:
                    return "far-query-bypass";
                default:
                    return "not-queried";
            }
        }

        private enum QueryPhase
        {
            NotQueried,
            FirstPassLineOfSight,
            SecondPassLineOfSight,
            FarQueryBypass
        }

        private sealed class QueryBatch
        {
            public QueryBatch(uint hunterGlobalId)
            {
                HunterGlobalId = hunterGlobalId;
            }

            public uint HunterGlobalId { get; }
            public long LastCandidateTimestamp { get; set; }
            public QueryCandidate? FirstPass { get; private set; }
            public QueryCandidate? SecondPass { get; private set; }
            public QueryCandidate? FarPass { get; private set; }

            public void SetNearest(QueryCandidate candidate)
            {
                switch (candidate.Phase)
                {
                    case QueryPhase.FirstPassLineOfSight:
                        FirstPass = ChooseNearest(FirstPass, candidate);
                        break;
                    case QueryPhase.SecondPassLineOfSight:
                        SecondPass = ChooseNearest(SecondPass, candidate);
                        break;
                    case QueryPhase.FarQueryBypass:
                        FarPass = ChooseNearest(FarPass, candidate);
                        break;
                }
            }

            private static QueryCandidate ChooseNearest(
                QueryCandidate? current,
                QueryCandidate candidate)
            {
                if (!current.HasValue ||
                    candidate.ManhattanDistance < current.Value.ManhattanDistance ||
                    (candidate.ManhattanDistance == current.Value.ManhattanDistance &&
                        candidate.PreyUnitId < current.Value.PreyUnitId))
                {
                    return candidate;
                }

                return current.Value;
            }
        }

        private readonly struct QueryCandidate
        {
            public QueryCandidate(
                int hunterUnitId,
                uint hunterGlobalId,
                int preyUnitId,
                uint preyGlobalId,
                eChimps preyType,
                int manhattanDistance,
                QueryPhase phase,
                int queryThreadId)
            {
                HunterUnitId = hunterUnitId;
                HunterGlobalId = hunterGlobalId;
                PreyUnitId = preyUnitId;
                PreyGlobalId = preyGlobalId;
                PreyType = preyType;
                ManhattanDistance = manhattanDistance;
                Phase = phase;
                QueryThreadId = queryThreadId;
            }

            public int HunterUnitId { get; }
            public uint HunterGlobalId { get; }
            public int PreyUnitId { get; }
            public uint PreyGlobalId { get; }
            public eChimps PreyType { get; }
            public int ManhattanDistance { get; }
            public QueryPhase Phase { get; }
            public int QueryThreadId { get; }
        }

        private readonly struct ProbeIdentity : IEquatable<ProbeIdentity>
        {
            public ProbeIdentity(
                int hunterUnitId,
                uint hunterGlobalId,
                int preyUnitId,
                uint preyGlobalId)
            {
                HunterUnitId = hunterUnitId;
                HunterGlobalId = hunterGlobalId;
                PreyUnitId = preyUnitId;
                PreyGlobalId = preyGlobalId;
            }

            public int HunterUnitId { get; }
            public uint HunterGlobalId { get; }
            public int PreyUnitId { get; }
            public uint PreyGlobalId { get; }

            public bool Equals(ProbeIdentity other) =>
                HunterUnitId == other.HunterUnitId &&
                HunterGlobalId == other.HunterGlobalId &&
                PreyUnitId == other.PreyUnitId &&
                PreyGlobalId == other.PreyGlobalId;

            public override bool Equals(object obj) => obj is ProbeIdentity other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = HunterUnitId;
                    hash = hash * 397 ^ (int)HunterGlobalId;
                    hash = hash * 397 ^ PreyUnitId;
                    hash = hash * 397 ^ (int)PreyGlobalId;
                    return hash;
                }
            }
        }

        private readonly struct ProbeObservation
        {
            public ProbeObservation(long timestamp)
            {
                Timestamp = timestamp;
            }

            public long Timestamp { get; }
        }
    }
}
