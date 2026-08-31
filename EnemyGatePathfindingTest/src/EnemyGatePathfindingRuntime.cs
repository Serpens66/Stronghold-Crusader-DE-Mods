using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.EventAPI.Units;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace EnemyGatePathfindingTest
{
    internal sealed unsafe class EnemyGatePathfindingRuntime
    {
        // UPDATE REVIEW (CrusaderDE.dll): verify calling convention, parameter order and
        // Int64 return type against the native function after every game-DLL update.
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate long GetNextReachablePclDelegate(
            IntPtr pathfindingContext,
            int playerId,
            int targetPcl,
            int sourcePcl,
            int mode);

        private const ulong ZeroFlagMask = 1UL << 6;
        private const int MaximumCallbackWarningsPerMap = 8;

        private readonly ManualLogSource log;
        private SamePclBridgeDiagnostics samePclDiagnostics;
        private HookTransaction transaction;
        private HookRef<X64ManagedFunctionDetourAOB<GetNextReachablePclDelegate>> reachabilityHook =
            new HookRef<X64ManagedFunctionDetourAOB<GetNextReachablePclDelegate>>();
        private HookRef<X64InlineHook> capturedByFilterHook = new HookRef<X64InlineHook>();
        private ulong libraryBase;
        private int mapActive;
        private int callbackWarnings;

        private long totalQueries;
        private long localHumanQueries;
        private long aiQueries;
        private long unknownRoleQueries;
        private long enemyRecordQueries;
        private long enemyQueriesReturningZero;
        private long enemyQueriesReturningPositive;
        private long uncapturedEnemyRecordsExcludedByVanilla;
        private long alliedCapturedEnemyRecordsAllowed;
        private long foreignCapturedEnemyRecordsRejected;
        private long filterFailOpenRecords;

        [ThreadStatic]
        private static int traceDepth;
        [ThreadStatic]
        private static int traceUncaptured;
        [ThreadStatic]
        private static int traceAlliedCaptured;
        [ThreadStatic]
        private static int traceForeignRejected;
        [ThreadStatic]
        private static int traceFailOpen;
        [ThreadStatic]
        private static GateRecordTrace traceFirstRecord;

        internal EnemyGatePathfindingRuntime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        internal void InitializeNative(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            if (!referenceHashMatches)
                throw new InvalidOperationException("fixed native layout hash does not match the supported CrusaderDE.dll");
            if (libraryHandle == IntPtr.Zero)
                throw new InvalidOperationException("native library handle is null");

            Shared.NativeResolution functionResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                EnemyGatePathfindingNativeDefinition.GetNextReachablePclPattern,
                EnemyGatePathfindingNativeDefinition.GetNextReachablePclRva,
                referenceHashMatches: true,
                "player-aware PCL reachability",
                log);
            Shared.NativeResolution compareSequenceResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                EnemyGatePathfindingNativeDefinition.CapturedByComparePattern,
                EnemyGatePathfindingNativeDefinition.CapturedByCompareRva -
                    EnemyGatePathfindingNativeDefinition.CapturedByCompareOffsetInPattern,
                referenceHashMatches: true,
                "hostile-gate captured-player comparison",
                log);
            Shared.NativeResolution moveHereResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                EnemyGatePathfindingNativeDefinition.MoveHerePattern,
                EnemyGatePathfindingNativeDefinition.MoveHereRva,
                referenceHashMatches: true,
                "Script-Extender-owned common MoveHere entry",
                log);
            Shared.NativeResolution cursorResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                EnemyGatePathfindingNativeDefinition.CursorTargetPattern,
                EnemyGatePathfindingNativeDefinition.CursorTargetSignatureRva,
                referenceHashMatches: true,
                "human cursor target coordinate loads",
                log);
            int compareRva = compareSequenceResolution.Rva +
                EnemyGatePathfindingNativeDefinition.CapturedByCompareOffsetInPattern;
            int cursorXRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                cursorResolution.Rva + EnemyGatePathfindingNativeDefinition.CursorTargetXDisplacementOffset,
                cursorResolution.Rva + EnemyGatePathfindingNativeDefinition.CursorTargetXNextInstructionOffset);
            int cursorYRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                cursorResolution.Rva + EnemyGatePathfindingNativeDefinition.CursorTargetYDisplacementOffset,
                cursorResolution.Rva + EnemyGatePathfindingNativeDefinition.CursorTargetYNextInstructionOffset);
            if (functionResolution.Rva != EnemyGatePathfindingNativeDefinition.GetNextReachablePclRva ||
                compareRva != EnemyGatePathfindingNativeDefinition.CapturedByCompareRva ||
                moveHereResolution.Rva != EnemyGatePathfindingNativeDefinition.MoveHereRva ||
                cursorResolution.Rva != EnemyGatePathfindingNativeDefinition.CursorTargetSignatureRva ||
                cursorXRva != EnemyGatePathfindingNativeDefinition.CursorTargetXRva ||
                cursorYRva != EnemyGatePathfindingNativeDefinition.CursorTargetYRva)
            {
                throw new InvalidOperationException("native PCL signatures resolved outside their audited RVAs");
            }

            libraryBase = unchecked((ulong)libraryHandle.ToInt64());
            samePclDiagnostics = new SamePclBridgeDiagnostics(
                log,
                (int*)(libraryBase + unchecked((ulong)cursorXRva)),
                (int*)(libraryBase + unchecked((ulong)cursorYRva)));
            // UPDATE REVIEW (Script Extender): revalidate HookTransaction context-save
            // semantics and that BeforeCallback executes relocated instructions first.
            transaction = new HookTransaction(
                memory,
                libraryBase,
                loggerFactory: null,
                failureMode: TransactionFailureMode.RollbackAndThrow);
            transaction.AddContextHook(
                ref capturedByFilterHook,
                libraryBase + unchecked((ulong)compareRva),
                FilterUnrelatedCapturedEnemyGate,
                regs: X64SmartCPUContextRegs.All,
                hookSize: EnemyGatePathfindingNativeDefinition.CapturedByCompareHookLength,
                errorMode: CallbackErrorMode.LogAndContinue,
                placement: OverwrittenInstructionPlacement.BeforeCallback);
            transaction.AddDetour(
                ref reachabilityHook,
                libraryBase + unchecked((ulong)functionResolution.Rva),
                ObserveGetNextReachablePclForPlayer);
            transaction.Commit();
            if (!capturedByFilterHook.Success || !reachabilityHook.Success)
                throw new InvalidOperationException("PCL filter and diagnostic detour were not installed atomically");

            ulong functionAddress = libraryBase + unchecked((ulong)functionResolution.Rva);
            ulong filterAddress = libraryBase + unchecked((ulong)compareRva);
            Shared.DebugLogHelper.LogInfo(
                log,
                "Enemy-gate PCL hooks installed: " +
                $"functionMethod={functionResolution.Method}, functionRva=0x{functionResolution.Rva:X}, " +
                $"functionAddress=0x{functionAddress:X}, filterMethod={compareSequenceResolution.Method}+" +
                $"0x{EnemyGatePathfindingNativeDefinition.CapturedByCompareOffsetInPattern:X}, " +
                $"filterRva=0x{compareRva:X}, filterAddress=0x{filterAddress:X}, " +
                $"dllSha256={EnemyGatePathfindingNativeDefinition.ReferenceSha256}.");
            Shared.DebugLogHelper.LogInfo(
                log,
                "Diagnostic native contract validated: " +
                $"MoveHereRva=0x{moveHereResolution.Rva:X} (observed through Script Extender 1.42.0 Pre/Post event; no overlapping hook), " +
                $"cursorSignatureRva=0x{cursorResolution.Rva:X}, cursorX=0x{cursorXRva:X}, cursorY=0x{cursorYRva:X}. " +
                "Diagnostics are active immediately, auto-start an epoch on the first valid query, " +
                "and correlate prior cursor/PCL checks with MoveHere over a deferred 1500-ms window.");
            Shared.DebugLogHelper.LogInfo(
                log,
                "Native audit contract: " +
                $"directCallers={EnemyGatePathfindingNativeDefinition.AuditedDirectCallerCount}, " +
                $"humanCursorCommandRange=[0x{EnemyGatePathfindingNativeDefinition.HumanCursorCommandStartRva:X}," +
                $"0x{EnemyGatePathfindingNativeDefinition.HumanCursorCommandEndRva:X}), " +
                $"commonPathBuilderRange=[0x{EnemyGatePathfindingNativeDefinition.CommonPathBuilderStartRva:X}," +
                $"0x{EnemyGatePathfindingNativeDefinition.CommonPathBuilderEndRva:X}), " +
                $"recordStride=0x{EnemyGatePathfindingNativeDefinition.NativeRecordStride:X}, " +
                $"pclOffsets=[{EnemyGatePathfindingNativeDefinition.RecordFirstPclOffset:+#;-#;0}," +
                $"{EnemyGatePathfindingNativeDefinition.RecordSecondPclOffset:+#;-#;0}," +
                $"{EnemyGatePathfindingNativeDefinition.RecordThirdPclOffset:+#;-#;0}]. " +
                "The third native PCL remains in Vanilla's graph and covers gate-associated drawbridge routing.");
        }

        internal void BeginMap()
        {
            if (Interlocked.CompareExchange(ref mapActive, 1, 0) != 0)
                return;
            ResetMapCounters();
            samePclDiagnostics?.BeginExplicitEpoch("OnStartMap(Post)");
            Shared.DebugLogHelper.LogInfo(
                log,
                "Enemy-gate PCL test map started. The native hostile-owner filter is active for AI, " +
                "human cursor validation and command validation; bounded query diagnostics are enabled.");
        }

        internal void EndMap()
        {
            bool hadActiveEpoch = Interlocked.CompareExchange(ref mapActive, 0, 1) == 1;
            samePclDiagnostics?.EndEpoch("OnUnloadMap(Post)");
            if (!hadActiveEpoch)
                return;
            Shared.DebugLogHelper.LogInfo(
                log,
                "Enemy-gate PCL map summary: " +
                $"queries={Read(ref totalQueries)}, human={Read(ref localHumanQueries)}, " +
                $"ai={Read(ref aiQueries)}, unknownRole={Read(ref unknownRoleQueries)}, " +
                $"enemyRecordQueries={Read(ref enemyRecordQueries)}, " +
                $"enemyQueryResultZero={Read(ref enemyQueriesReturningZero)}, " +
                $"enemyQueryResultPositive={Read(ref enemyQueriesReturningPositive)}, " +
                $"uncapturedExcludedByVanilla={Read(ref uncapturedEnemyRecordsExcludedByVanilla)}, " +
                $"alliedCapturedAllowed={Read(ref alliedCapturedEnemyRecordsAllowed)}, " +
                $"foreignCapturedRejected={Read(ref foreignCapturedEnemyRecordsRejected)}, " +
                $"filterFailOpen={Read(ref filterFailOpenRecords)}. " +
                "See the Same-PCL final summary for cursor, MoveHere and topology diagnostics.");
        }

        internal void ObserveMoveHere(UnitMoveHereEventArgs args)
        {
            if (args == null)
                return;
            try
            {
                samePclDiagnostics?.OnMoveHere(args);
            }
            catch
            {
                samePclDiagnostics?.RecordHotPathFailure();
            }
        }

        internal void ProcessDeferredDiagnostics()
        {
            try
            {
                samePclDiagnostics?.ProcessDeferred();
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure(ex);
            }
        }

        private void FilterUnrelatedCapturedEnemyGate(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                X64SmartCPUContext* registers = context.Pointer;
                if (registers == null)
                {
                    RecordFilterDecision(FilterTraceKind.FailOpen, default);
                    return;
                }
                // The relocated CMP already ran. This callback is reached only after
                // Vanilla established that the record owner is hostile to queryPlayer.
                int queryPlayerId = unchecked((int)(uint)registers->R14);
                int buildingIdFromRegister = unchecked((int)(uint)registers->RCX);
                byte* record = (byte*)registers->R9;
                if (record == null)
                {
                    RecordFilterDecision(FilterTraceKind.FailOpen, default);
                    return;
                }

                int recordBuildingId = *(int*)(record +
                    EnemyGatePathfindingNativeDefinition.RecordBuildingIdOffset);
                int ownerPlayerId = *(int*)(record +
                    EnemyGatePathfindingNativeDefinition.RecordOwnerPlayerIdOffset);
                int firstPcl = *(int*)(record + EnemyGatePathfindingNativeDefinition.RecordFirstPclOffset);
                int secondPcl = *(int*)(record + EnemyGatePathfindingNativeDefinition.RecordSecondPclOffset);
                int thirdPcl = *(int*)(record + EnemyGatePathfindingNativeDefinition.RecordThirdPclOffset);

                // UPDATE REVIEW (Script Extender): re-audit GameBuilding size/packing,
                // captured/owner/alive fields and 1-based building-ID lookup semantics.
                GameBuildingManagerAPI buildings = GameBuildingManagerAPI.Instance;
                GameBuilding* building = null;
                if (buildingIdFromRegister <= 0 || buildingIdFromRegister != recordBuildingId ||
                    !buildings.IsValidId(buildingIdFromRegister) ||
                    !buildings.TryGetBuildingById(buildingIdFromRegister, out building) ||
                    building == null || building->r_AliveState != AliveState.IsAlive ||
                    building->r_GlobalId == 0 || building->r_PlayerIdOwner != ownerPlayerId)
                {
                    RecordFilterDecision(
                        FilterTraceKind.FailOpen,
                        new GateRecordTrace(
                            "fail-open-inconsistent-record",
                            3,
                            recordBuildingId,
                            building == null ? 0u : building->r_GlobalId,
                            ownerPlayerId,
                            building == null ? -1 : building->r_CapturedByPlayerId,
                            firstPcl,
                            secondPcl,
                            thirdPcl));
                    return;
                }

                int capturedByPlayerId = building->r_CapturedByPlayerId;
                bool vanillaSawUncaptured = (registers->Rflags & ZeroFlagMask) != 0;
                if ((capturedByPlayerId == 0) != vanillaSawUncaptured)
                {
                    RecordFilterDecision(
                        FilterTraceKind.FailOpen,
                        new GateRecordTrace(
                            "fail-open-capture-race",
                            3,
                            buildingIdFromRegister,
                            building->r_GlobalId,
                            ownerPlayerId,
                            capturedByPlayerId,
                            firstPcl,
                            secondPcl,
                            thirdPcl));
                    return;
                }
                var recordTrace = new GateRecordTrace(
                    capturedByPlayerId == 0 ? "uncaptured-hostile" : "captured-hostile",
                    1,
                    buildingIdFromRegister,
                    building->r_GlobalId,
                    ownerPlayerId,
                    capturedByPlayerId,
                    firstPcl,
                    secondPcl,
                    thirdPcl);
                CapturedGateFilterDecision decision = EnemyGatePathfindingPolicy.EvaluateGateAccess(
                    queryPlayerId,
                    ownerPlayerId,
                    capturedByPlayerId,
                    IsValidPlayer,
                    AreAllied);
                switch (decision)
                {
                    case CapturedGateFilterDecision.PreserveVanilla:
                        if (capturedByPlayerId == 0)
                        {
                            RecordFilterDecision(FilterTraceKind.Uncaptured, recordTrace);
                        }
                        else
                        {
                            recordTrace = recordTrace.With("allied-capture-allowed", 2);
                            RecordFilterDecision(FilterTraceKind.AlliedCaptured, recordTrace);
                        }
                        return;

                    case CapturedGateFilterDecision.ExcludeForeignCapture:
                        // The untouched JE now takes the same exclusion branch as for
                        // r_CapturedByPlayerId == 0. No PCL or path result is synthesized.
                        registers->Rflags |= ZeroFlagMask;
                        recordTrace = recordTrace.With("foreign-capture-rejected", 4);
                        RecordFilterDecision(FilterTraceKind.ForeignRejected, recordTrace);
                        return;

                    default:
                        recordTrace = recordTrace.With("fail-open-player-or-alliance", 3);
                        RecordFilterDecision(FilterTraceKind.FailOpen, recordTrace);
                        return;
                }
            }
            catch
            {
                // Preserve the relocated CMP flags. Diagnostics are never allowed to
                // turn a fail-open record into either an accepted or rejected record.
                RecordFilterDecision(FilterTraceKind.FailOpen, default);
                Interlocked.Increment(ref callbackWarnings);
            }
        }

        private long ObserveGetNextReachablePclForPlayer(
            IntPtr pathfindingContext,
            int playerId,
            int targetPcl,
            int sourcePcl,
            int mode)
        {
            // Hot-path safety: no game API, stack walk, string, logging or topology
            // access is permitted before calling Vanilla.

            bool outerTrace = traceDepth++ == 0;
            if (outerTrace)
                ResetThreadTrace();

            long result;
            try
            {
                result = reachabilityHook.Value.Hook.Trampoline(
                    pathfindingContext, playerId, targetPcl, sourcePcl, mode);
            }
            finally
            {
                traceDepth--;
            }

            if (!outerTrace)
                return result;

            QueryTrace trace = CaptureThreadTrace();
            // Everything below is observational and isolated from the native result.
            try
            {
                ObserveCompletedQuery(playerId, sourcePcl, targetPcl, mode, result, trace);
            }
            catch
            {
                samePclDiagnostics?.RecordHotPathFailure();
            }
            return result;
        }

        private void ObserveCompletedQuery(
            int playerId,
            int sourcePcl,
            int targetPcl,
            int mode,
            long result,
            QueryTrace trace)
        {
            Interlocked.Increment(ref totalQueries);
            bool hasEnemyRecord = trace.Total != 0;
            if (hasEnemyRecord)
            {
                Interlocked.Increment(ref enemyRecordQueries);
                if (result == 0)
                    Interlocked.Increment(ref enemyQueriesReturningZero);
                else
                    Interlocked.Increment(ref enemyQueriesReturningPositive);
            }

            samePclDiagnostics?.ObserveQuery(
                playerId,
                sourcePcl,
                targetPcl,
                mode,
                result,
                trace.Total);

        }

        private void RecordFilterDecision(FilterTraceKind kind, GateRecordTrace record)
        {
            bool active = Volatile.Read(ref mapActive) != 0;
            switch (kind)
            {
                case FilterTraceKind.Uncaptured:
                    if (active)
                        Interlocked.Increment(ref uncapturedEnemyRecordsExcludedByVanilla);
                    if (traceDepth > 0)
                        traceUncaptured++;
                    break;
                case FilterTraceKind.AlliedCaptured:
                    if (active)
                        Interlocked.Increment(ref alliedCapturedEnemyRecordsAllowed);
                    if (traceDepth > 0)
                        traceAlliedCaptured++;
                    break;
                case FilterTraceKind.ForeignRejected:
                    if (active)
                        Interlocked.Increment(ref foreignCapturedEnemyRecordsRejected);
                    if (traceDepth > 0)
                        traceForeignRejected++;
                    break;
                default:
                    if (active)
                        Interlocked.Increment(ref filterFailOpenRecords);
                    if (traceDepth > 0)
                        traceFailOpen++;
                    break;
            }

            if (traceDepth > 0 && record.Priority > traceFirstRecord.Priority)
                traceFirstRecord = record;
        }

        // UPDATE REVIEW (Script Extender): verify player validity, local-player, AI and
        // alliance semantics, particularly neutral and multiplayer slots.
        private static bool IsValidPlayer(int playerId) =>
            GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId);

        private static bool AreAllied(int firstPlayerId, int secondPlayerId) =>
            firstPlayerId == secondPlayerId ||
            GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(firstPlayerId, secondPlayerId);

        private void TryLogDiagnosticFailure(Exception ex)
        {
            try
            {
                if (Interlocked.Increment(ref callbackWarnings) <= MaximumCallbackWarningsPerMap)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "Enemy-gate query diagnostics failed without changing the native result: " +
                        $"{ex.GetType().Name}: {ex.Message}");
                }
            }
            catch
            {
                // Logging must never change native policy.
            }
        }

        private static void ResetThreadTrace()
        {
            traceUncaptured = 0;
            traceAlliedCaptured = 0;
            traceForeignRejected = 0;
            traceFailOpen = 0;
            traceFirstRecord = default;
        }

        private static QueryTrace CaptureThreadTrace() => new QueryTrace(
            traceUncaptured,
            traceAlliedCaptured,
            traceForeignRejected,
            traceFailOpen,
            traceFirstRecord);

        private void ResetMapCounters()
        {
            Interlocked.Exchange(ref totalQueries, 0);
            Interlocked.Exchange(ref localHumanQueries, 0);
            Interlocked.Exchange(ref aiQueries, 0);
            Interlocked.Exchange(ref unknownRoleQueries, 0);
            Interlocked.Exchange(ref enemyRecordQueries, 0);
            Interlocked.Exchange(ref enemyQueriesReturningZero, 0);
            Interlocked.Exchange(ref enemyQueriesReturningPositive, 0);
            Interlocked.Exchange(ref uncapturedEnemyRecordsExcludedByVanilla, 0);
            Interlocked.Exchange(ref alliedCapturedEnemyRecordsAllowed, 0);
            Interlocked.Exchange(ref foreignCapturedEnemyRecordsRejected, 0);
            Interlocked.Exchange(ref filterFailOpenRecords, 0);
            Interlocked.Exchange(ref callbackWarnings, 0);
        }

        private static long Read(ref long value) => Interlocked.Read(ref value);

        private static string FormatRole(QueryPlayerRole role)
        {
            switch (role)
            {
                case QueryPlayerRole.LocalHuman:
                    return "local-human";
                case QueryPlayerRole.OtherHuman:
                    return "other-human";
                case QueryPlayerRole.Ai:
                    return "ai";
                default:
                    return "unknown";
            }
        }

        private enum FilterTraceKind
        {
            Uncaptured,
            AlliedCaptured,
            ForeignRejected,
            FailOpen
        }

        private enum QueryPlayerRole
        {
            Unknown,
            LocalHuman,
            OtherHuman,
            Ai
        }

        private readonly struct QueryTrace
        {
            internal QueryTrace(
                int uncaptured,
                int alliedCaptured,
                int foreignRejected,
                int failOpen,
                GateRecordTrace firstRecord)
            {
                Uncaptured = uncaptured;
                AlliedCaptured = alliedCaptured;
                ForeignRejected = foreignRejected;
                FailOpen = failOpen;
                FirstRecord = firstRecord;
            }

            internal int Uncaptured { get; }
            internal int AlliedCaptured { get; }
            internal int ForeignRejected { get; }
            internal int FailOpen { get; }
            internal GateRecordTrace FirstRecord { get; }
            internal int Total => Uncaptured + AlliedCaptured + ForeignRejected + FailOpen;
        }

        private readonly struct GateRecordTrace
        {
            internal GateRecordTrace(
                string kind,
                int priority,
                int buildingId,
                uint globalId,
                int ownerPlayerId,
                int capturedByPlayerId,
                int firstPcl,
                int secondPcl,
                int thirdPcl)
            {
                Kind = kind;
                Priority = priority;
                BuildingId = buildingId;
                GlobalId = globalId;
                OwnerPlayerId = ownerPlayerId;
                CapturedByPlayerId = capturedByPlayerId;
                FirstPcl = firstPcl;
                SecondPcl = secondPcl;
                ThirdPcl = thirdPcl;
            }

            internal string Kind { get; }
            internal int Priority { get; }
            internal int BuildingId { get; }
            internal uint GlobalId { get; }
            internal int OwnerPlayerId { get; }
            internal int CapturedByPlayerId { get; }
            internal int FirstPcl { get; }
            internal int SecondPcl { get; }
            internal int ThirdPcl { get; }

            internal GateRecordTrace With(string kind, int priority) => new GateRecordTrace(
                kind,
                priority,
                BuildingId,
                GlobalId,
                OwnerPlayerId,
                CapturedByPlayerId,
                FirstPcl,
                SecondPcl,
                ThirdPcl);
        }
    }
}
