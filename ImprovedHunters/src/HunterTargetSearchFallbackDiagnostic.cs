using BepInEx.Logging;
using Iced.Intel;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace ImprovedHunters
{
    internal enum HunterStateOneNearRefreshAction
    {
        None = 0,
        ContinueExistingPath = 1,
        ContinueExistingPathPendingVisibility = 2,
        HandoffToVanillaAttack = 3
    }

    internal delegate HunterStateOneNearRefreshAction TryPrepareHunterStateOneNearRefresh(
        int hunterUnitId,
        int preyUnitId,
        uint preyGlobalId,
        int nativeWorldDistance,
        out bool shouldLog);

    internal delegate bool TryPrepareHunterPostShotStateZeroContinuation(
        int hunterUnitId,
        long timestamp,
        out HunterPostShotContinuationCandidate candidate);

    /// <summary>
    /// Temporary, separately removable validation of the native target-search
    /// handoff, the state-1 near-target refresh and its direct-attack
    /// continuation. State 0 may receive a validated fallback target. State 1
    /// only selects Vanilla's existing continuation branch for an already
    /// validated path; Vanilla remains responsible for locomotion and orders.
    /// </summary>
    internal sealed unsafe partial class HunterTargetSearchFallbackDiagnostic : IDisposable
    {
        private const string ReferenceDllSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const int StateZeroQuerySequenceRva = 0x12FDB7;
        private const int StateZeroQueryReturnHookOffset = 0x22;
        private const int StateZeroMoveResultSequenceRva = 0x12FE63;
        private const int StateZeroMoveResultHookOffset = 0x17;
        private const int StateOneRefreshQuerySequenceRva = 0x12FF57;
        private const int StateOneRefreshQueryResultHookOffset = 0x27;
        private const int StateOneRefreshQueryResultHookLength = 0x0E;
        private const int StateOneRefreshFailureBranchTargetOffset = 0x25;
        private const int StateOneNearRefreshBranchSequenceRva = 0x130069;
        private const int StateOneNearRefreshHookLength = 0x0F;
        private const int StateOneNearRefreshFarBranchTargetOffset = 0x14;
        private const int StateOneNearRefreshQueryJumpOffset = 0x0F;
        private const int StateOneWorldDistanceScratchRva = 0x34A9F5C;
        private const int StateOneCurrentHunterUnitIdRva = 0x9302C4;
        private const int StateOneRefreshDistance = 20;
        private const int StateOneContinuationDistance = 28;
        private const int StateOneBypassDistance = StateOneRefreshDistance + 1;
        private const int StateOneDirectAttackSequenceRva = 0x13018D;
        private const int StateOneDirectAttackResultHookOffset = 0x0C;
        private const int HunterUpdateStartRva = 0x12FC70;
        private const int HunterUpdateEndRva = 0x131422;
        private const int HunterQueryFunctionRva = 0x18AF50;
        private const int MoveHereFunctionRva = 0x196280;
        private const int DirectAttackFunctionRva = 0x18E9A0;
        private const int HunterPathStateOffset = 0xF2;
        private const int HunterPathFieldF4Offset = 0xF4;
        private const int HunterPathProgressOffset = 0xF6;
        private const int HunterPathLengthOffset = 0xF8;
        private const int HunterAiStateOffset = 0x2BC;
        private const int HunterOrderTargetGlobalIdOffset = 0xA4;
        private const int HunterTargetUnitIdOffset = 0x39A;
        private const int HunterTargetGlobalIdOffset = 0x39C;
        private const int PreyCorpseFlagOffset = 0x29C;
        private const int PreyReservationOffset = 0x448;
        private const int MinimumMovingTargetAnchorDisplacement = 6;
        private const int MaximumMovingTargetReplans = 3;
        private const int MaxDiagnosticLogs = 160;
        private const int MaxStateOneDiagnosticLogs = 160;
        private const ulong ZeroFlagMask = 1UL << 6;

        private const string StateZeroQuerySequencePattern =
            "8B D3 49 8B CD E8 ? ? ? ? " +
            "49 0F BF 8C 3E FC 01 00 00 " +
            "48 69 D1 90 04 00 00 " +
            "41 8B 8C 3E 08 02 00 00 " +
            "4C 63 C0 42 39 8C 2A F0 06 00 00";
        private const string StateZeroMoveResultSequencePattern =
            "66 47 89 B4 28 A4 0A 00 00 " +
            "47 0F BF 84 28 1C 07 00 00 " +
            "E8 ? ? ? ? 85 C0 48 63 05 ? ? ? ? 0F 84 ? ? ? ?";
        private const string StateOneRefreshQuerySequencePattern =
            "4A 0F BF B4 2F F6 09 00 00 " +
            "42 8B 84 2F F8 09 00 00 " +
            "48 69 CE 90 04 00 00 " +
            "42 39 84 29 F0 06 00 00 74 61 " +
            "8B D3 49 8B CD E8 ? ? ? ? " +
            "85 C0 48 63 05 ? ? ? ? 74 15 " +
            "48 69 C8 90 04 00 00";
        private const string StateOneNearRefreshBranchSequencePattern =
            "83 3D ? ? ? ? 14 7F 0B " +
            "8B 15 ? ? ? ? E9 ? ? ? ? " +
            "48 63 1D ? ? ? ? 83 FF 1E 7E 23";
        private const string StateOneDirectAttackSequencePattern =
            "E8 ? ? ? ? 48 63 15 ? ? ? ? 85 C0 74 ? " +
            "4C 69 C2 90 04 00 00 43 38 AC 28 5A 0A 00 00 75 ? " +
            "B8 09 00 00 00";

        private static readonly long RejectedCandidateCooldown = Stopwatch.Frequency * 30;
        private static long nextGeneration;

        [ThreadStatic] private static long activeGeneration;
        [ThreadStatic] private static int activeHunterUnitId;
        [ThreadStatic] private static Candidate stagedCandidate;
        [ThreadStatic] private static Candidate pendingMoveCandidate;
        [ThreadStatic] private static Candidate stagedMovingTargetReplan;

        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly Func<bool> canRunTargetSelection;
        private readonly Func<bool> canRunPathfinding;
        private readonly TryPrepareHunterStateOneNearRefresh tryPrepareStateOneNearRefresh;
        private readonly TryPrepareHunterPostShotStateZeroContinuation tryPreparePostShotStateZeroContinuation;
        private readonly TryValidateHunterPostShotContinuation tryValidateContinuation;
        private readonly Action<HunterPostShotContinuationCandidate, long> recordAcceptedPostShotAttack;
        private readonly Action<HunterPostShotContinuationCandidate, long> recordFailedPostShotAttack;
        private readonly Action<HunterPostShotContinuationCandidate, int> recordPostShotStateZeroHandoff;
        private readonly Action<HunterPostShotContinuationCandidate, int> recordPostShotMoveHereResult;
        private readonly Action<int, int, uint> resetPostShotAttemptBudget;
        private readonly Action<int, uint, long> registerRejectedMove;
        private readonly Action<int, int, uint, eChimps, int, long> recordPclMoveHereResult;
        private readonly long generation;
        private int* stateOneWorldDistanceScratch;
        private int* stateOneCurrentHunterUnitId;
        private readonly object observationLock = new object();
        private readonly Dictionary<int, AcceptedMoveObservation> acceptedMoveObservations =
            new Dictionary<int, AcceptedMoveObservation>();
        private readonly Dictionary<int, Candidate> pendingMovingTargetContinuations =
            new Dictionary<int, Candidate>();
        private readonly HashSet<ulong> loggedNearRefreshBypasses = new HashSet<ulong>();
        private HookTransaction transaction;
        private HookRef<X64InlineHook> queryStartHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> queryReturnHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> moveResultHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> stateOneRefreshBranchContextHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> stateOneRefreshQueryResultHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> stateOneDirectAttackResultHook = new HookRef<X64InlineHook>();
        private bool featureAvailable;
        private bool hookConfirmed;
        private bool stateOneNearRefreshHookConfirmed;
        private bool stateOneHookConfirmed;
        private bool stateOneInvalidContextLogged;
        private long nextAcceptedPathGeneration;
        private int diagnosticLogs;
        private int stateOneDiagnosticLogs;
        private bool disposed;

        public HunterTargetSearchFallbackDiagnostic(
            ManualLogSource log,
            ImprovedHuntersViewModel settings,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches,
            Func<bool> canRunTargetSelection,
            Func<bool> canRunPathfinding,
            TryPrepareHunterStateOneNearRefresh tryPrepareStateOneNearRefresh,
            TryPrepareHunterPostShotStateZeroContinuation tryPreparePostShotStateZeroContinuation,
            TryValidateHunterPostShotContinuation tryValidateContinuation,
            Action<HunterPostShotContinuationCandidate, long> recordAcceptedPostShotAttack,
            Action<HunterPostShotContinuationCandidate, long> recordFailedPostShotAttack,
            Action<HunterPostShotContinuationCandidate, int> recordPostShotStateZeroHandoff,
            Action<HunterPostShotContinuationCandidate, int> recordPostShotMoveHereResult,
            Action<int, int, uint> resetPostShotAttemptBudget,
            Action<int, uint, long> registerRejectedMove,
            Action<int, int, uint, eChimps, int, long> recordPclMoveHereResult)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.canRunTargetSelection = canRunTargetSelection ??
                throw new ArgumentNullException(nameof(canRunTargetSelection));
            this.canRunPathfinding = canRunPathfinding ??
                throw new ArgumentNullException(nameof(canRunPathfinding));
            this.tryPrepareStateOneNearRefresh = tryPrepareStateOneNearRefresh ??
                throw new ArgumentNullException(nameof(tryPrepareStateOneNearRefresh));
            this.tryPreparePostShotStateZeroContinuation = tryPreparePostShotStateZeroContinuation ??
                throw new ArgumentNullException(nameof(tryPreparePostShotStateZeroContinuation));
            this.tryValidateContinuation = tryValidateContinuation ??
                throw new ArgumentNullException(nameof(tryValidateContinuation));
            this.recordAcceptedPostShotAttack = recordAcceptedPostShotAttack ??
                throw new ArgumentNullException(nameof(recordAcceptedPostShotAttack));
            this.recordFailedPostShotAttack = recordFailedPostShotAttack ??
                throw new ArgumentNullException(nameof(recordFailedPostShotAttack));
            this.recordPostShotStateZeroHandoff = recordPostShotStateZeroHandoff ??
                throw new ArgumentNullException(nameof(recordPostShotStateZeroHandoff));
            this.recordPostShotMoveHereResult = recordPostShotMoveHereResult ??
                throw new ArgumentNullException(nameof(recordPostShotMoveHereResult));
            this.resetPostShotAttemptBudget = resetPostShotAttemptBudget ??
                throw new ArgumentNullException(nameof(resetPostShotAttemptBudget));
            this.registerRejectedMove = registerRejectedMove ??
                throw new ArgumentNullException(nameof(registerRejectedMove));
            this.recordPclMoveHereResult = recordPclMoveHereResult ??
                throw new ArgumentNullException(nameof(recordPclMoveHereResult));
            generation = Interlocked.Increment(ref nextGeneration);

            if (!referenceHashMatches)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    "Improved Hunters target-search fallback unavailable: " +
                    $"DLL hash differs from audited SHA-256 {ReferenceDllSha256}; behavior remains unchanged.");
                return;
            }
            if (memory.Length == 0 || libraryBase == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            int querySequenceRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                StateZeroQuerySequencePattern,
                StateZeroQuerySequenceRva,
                referenceHashMatches,
                "Hunter state-0 target-query handoff",
                log).Rva;
            int moveResultSequenceRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                StateZeroMoveResultSequencePattern,
                StateZeroMoveResultSequenceRva,
                referenceHashMatches,
                "Hunter state-0 MoveHere result",
                log).Rva;
            int stateOneRefreshQuerySequenceRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                StateOneRefreshQuerySequencePattern,
                StateOneRefreshQuerySequenceRva,
                referenceHashMatches,
                "Hunter state-1 near-target refresh query",
                log).Rva;
            int stateOneNearRefreshBranchSequenceRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                StateOneNearRefreshBranchSequencePattern,
                StateOneNearRefreshBranchSequenceRva,
                referenceHashMatches,
                "Hunter state-1 near-target refresh branch",
                log).Rva;
            int stateOneDirectAttackSequenceRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                StateOneDirectAttackSequencePattern,
                StateOneDirectAttackSequenceRva,
                referenceHashMatches,
                "Hunter state-1 direct-attack result",
                log).Rva;

            int queryFunctionRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                querySequenceRva + 6,
                querySequenceRva + 10);
            int moveHereFunctionRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                moveResultSequenceRva + 0x13,
                moveResultSequenceRva + 0x17);
            int stateOneRefreshQueryFunctionRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                stateOneRefreshQuerySequenceRva + 0x28,
                stateOneRefreshQuerySequenceRva + 0x2C);
            int stateOneNearRefreshJumpTargetRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                stateOneNearRefreshBranchSequenceRva + 0x10,
                stateOneNearRefreshBranchSequenceRva + 0x14);
            int directAttackFunctionRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                stateOneDirectAttackSequenceRva + 1,
                stateOneDirectAttackSequenceRva + 5);
            if (queryFunctionRva != HunterQueryFunctionRva ||
                stateOneRefreshQueryFunctionRva != HunterQueryFunctionRva ||
                stateOneNearRefreshJumpTargetRva != stateOneRefreshQuerySequenceRva + 0x24 ||
                moveHereFunctionRva != MoveHereFunctionRva ||
                directAttackFunctionRva != DirectAttackFunctionRva)
            {
                throw new InvalidOperationException(
                    $"Hunter target-search call chain changed: query=0x{queryFunctionRva:X}, " +
                    $"stateOneRefreshQuery=0x{stateOneRefreshQueryFunctionRva:X}, " +
                    $"stateOneRefreshJumpTarget=0x{stateOneNearRefreshJumpTargetRva:X}, " +
                    $"MoveHere=0x{moveHereFunctionRva:X}, directAttack=0x{directAttackFunctionRva:X}.");
            }

            ValidateStateOneNearRefreshHookSpan(
                memory,
                libraryBase,
                stateOneNearRefreshBranchSequenceRva,
                stateOneRefreshQuerySequenceRva + 0x24,
                out ulong stateOneWorldDistanceScratchAddress,
                out ulong stateOneCurrentHunterUnitIdAddress);
            int stateOneRefreshQueryResultRva = checked(
                stateOneRefreshQuerySequenceRva + StateOneRefreshQueryResultHookOffset);
            ValidateStateOneRefreshResultHookSpan(
                memory,
                libraryBase,
                stateOneRefreshQueryResultRva,
                stateOneCurrentHunterUnitIdAddress);
            stateOneWorldDistanceScratch = (int*)stateOneWorldDistanceScratchAddress;
            stateOneCurrentHunterUnitId = (int*)stateOneCurrentHunterUnitIdAddress;

            int queryReturnRva = checked(querySequenceRva + StateZeroQueryReturnHookOffset);
            int moveResultRva = checked(moveResultSequenceRva + StateZeroMoveResultHookOffset);
            int stateOneRefreshBranchContextRva = stateOneNearRefreshBranchSequenceRva;
            int stateOneDirectAttackResultRva = checked(
                stateOneDirectAttackSequenceRva + StateOneDirectAttackResultHookOffset);
            try
            {
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddContextHook(
                    ref queryStartHook,
                    libraryBase + unchecked((ulong)querySequenceRva),
                    BeginStateZeroQuery,
                    regs: X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBX,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref queryReturnHook,
                    libraryBase + unchecked((ulong)queryReturnRva),
                    CompleteStateZeroQuery,
                    regs: X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBX,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref moveResultHook,
                    libraryBase + unchecked((ulong)moveResultRva),
                    ObserveStateZeroMoveResult,
                    regs: X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBX,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref stateOneRefreshBranchContextHook,
                    libraryBase + unchecked((ulong)stateOneRefreshBranchContextRva),
                    CaptureStateOneNearRefreshContext,
                    regs: X64SmartCPUContextRegs.Volatile,
                    hookSize: StateOneNearRefreshHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref stateOneRefreshQueryResultHook,
                    libraryBase + unchecked((ulong)stateOneRefreshQueryResultRva),
                    CompleteStateOneMovingTargetReplanQuery,
                    regs: X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.Flags,
                    hookSize: StateOneRefreshQueryResultHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    // Relocated CALL/TEST/load run first; the untouched JE consumes our final ZF.
                    placement: OverwrittenInstructionPlacement.BeforeCallback);
                transaction.AddContextHook(
                    ref stateOneDirectAttackResultHook,
                    libraryBase + unchecked((ulong)stateOneDirectAttackResultRva),
                    ObserveStateOneDirectAttackResult,
                    // EDI still contains HunterUpdate's exact native distance result.
                    regs: X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RDI,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.Commit();

                if (!queryStartHook.Success ||
                    !queryReturnHook.Success ||
                    !moveResultHook.Success ||
                    !stateOneRefreshBranchContextHook.Success ||
                    !stateOneRefreshQueryResultHook.Success ||
                    !stateOneDirectAttackResultHook.Success)
                {
                    throw new InvalidOperationException("One or more Hunter target-search fallback hooks were not installed.");
                }

                featureAvailable = true;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Improved Hunters target-search fallback diagnostic initialized: " +
                    $"queryStartRva=0x{querySequenceRva:X}, queryReturnRva=0x{queryReturnRva:X}, " +
                    $"moveResultRva=0x{moveResultRva:X}, queryFunctionRva=0x{queryFunctionRva:X}, " +
                    $"stateOneRefreshContextRva=0x{stateOneRefreshBranchContextRva:X}, " +
                    $"stateOneRefreshHookSpan=[0x{stateOneRefreshBranchContextRva:X}," +
                    $"0x{stateOneRefreshBranchContextRva + StateOneNearRefreshHookLength:X}), " +
                    $"stateOneRefreshResultSpan=[0x{stateOneRefreshQueryResultRva:X}," +
                    $"0x{stateOneRefreshQueryResultRva + StateOneRefreshQueryResultHookLength:X}), " +
                    $"MoveHereRva=0x{moveHereFunctionRva:X}, " +
                    $"stateOneAttackResultRva=0x{stateOneDirectAttackResultRva:X}, " +
                    $"directAttackRva=0x{directAttackFunctionRva:X}, cooldownSeconds=" +
                    $"{RejectedCandidateCooldown / Stopwatch.Frequency}, ownMovement=False, ownAiState=False, " +
                    $"worldDistanceScratchRva=0x{StateOneWorldDistanceScratchRva:X}, " +
                    $"currentHunterUnitIdRva=0x{StateOneCurrentHunterUnitIdRva:X}, " +
                    "stateOneRefreshOverrides=world-distance-scratch-20-to-21-or-stale-query-ZF-clear-only, " +
                    "nearRefreshDecision=bidirectional-native-visibility, " +
                    $"movingTargetReplanDisplacement={MinimumMovingTargetAnchorDisplacement}, " +
                    $"maximumMovingTargetReplans={MaximumMovingTargetReplans}, " +
                    "movingTargetReplan=one-per-accepted-path-generation, " +
                    "continuationTicketRequiredWhenBlocked=False-except-stale-moving-target, " +
                    "tileAttackDecision=active-visibility-snapshot, ownReservationRequired=2, " +
                    "foreignReservationAllowed=False, stateOneQueryResultMutation=guarded-ZF-clear-only, " +
                    "stateOneDirectAttackObservationOnly=True.");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public bool IsAvailable =>
            featureAvailable &&
            !disposed &&
            queryStartHook.Success &&
            queryReturnHook.Success &&
            moveResultHook.Success &&
            stateOneRefreshBranchContextHook.Success &&
            stateOneRefreshQueryResultHook.Success &&
            stateOneDirectAttackResultHook.Success;

        public void RecordCandidate(
            int hunterUnitId,
            int preyUnitId,
            uint preyGlobalId,
            eChimps preyType,
            bool preferred,
            long timestamp)
        {
            if (!IsAvailable ||
                !canRunTargetSelection() ||
                activeGeneration != generation ||
                activeHunterUnitId != hunterUnitId ||
                hunterUnitId <= 0 ||
                preyUnitId <= 0 ||
                preyUnitId > ushort.MaxValue ||
                preyGlobalId == 0 ||
                !settings.IsHuntingEnabled(preyType))
            {
                return;
            }

            // A forced continuation already owns this one query. Its final live
            // validation still happens after Vanilla returns.
            if (stagedCandidate.IsForcedContinuation)
                return;

            Candidate candidate = new Candidate(
                hunterUnitId,
                preyUnitId,
                preyGlobalId,
                preyType,
                preferred,
                suppliedByFallback: false);

            if (!stagedCandidate.IsValid ||
                (candidate.Preferred && !stagedCandidate.Preferred) ||
                (candidate.Preferred == stagedCandidate.Preferred &&
                    candidate.PreyUnitId < stagedCandidate.PreyUnitId))
            {
                stagedCandidate = candidate;
            }
        }

        public void ResetForMap()
        {
            lock (observationLock)
            {
                acceptedMoveObservations.Clear();
                pendingMovingTargetContinuations.Clear();
                loggedNearRefreshBypasses.Clear();
            }

            diagnosticLogs = 0;
            stateOneDiagnosticLogs = 0;
            hookConfirmed = false;
            stateOneNearRefreshHookConfirmed = false;
            stateOneHookConfirmed = false;
            stateOneInvalidContextLogged = false;
            nextAcceptedPathGeneration = 0;
            ClearThreadState();
        }

        private void BeginStateZeroQuery(NativePointer<X64SmartCPUContext> context)
        {
            ClearThreadState();
            if (!IsAvailable || (!canRunTargetSelection() && !canRunPathfinding()))
                return;

            int hunterUnitId = unchecked((int)(uint)context.Pointer->RBX);
            if (!TryValidateHunter(hunterUnitId, requiredAiState: 0, out _))
            {
                LogDiagnostic(
                    $"Improved Hunters target-search fallback rejected query start: hunter={hunterUnitId}, " +
                    "reason=invalid-live-state0-hunter.",
                    warning: true);
                return;
            }

            activeGeneration = generation;
            activeHunterUnitId = hunterUnitId;
            long timestamp = Stopwatch.GetTimestamp();
            if (canRunPathfinding())
            {
                try
                {
                    if (tryPreparePostShotStateZeroContinuation(
                            hunterUnitId,
                            timestamp,
                            out HunterPostShotContinuationCandidate postShotCandidate))
                    {
                        stagedCandidate = Candidate.FromPostShot(postShotCandidate);
                    }
                }
                catch (Exception exception)
                {
                    LogDiagnostic(
                        "Improved Hunters post-shot State-0 preparation failed independently; " +
                        $"hunter={hunterUnitId}, error={exception.Message}.",
                        warning: true);
                }

                if (stagedCandidate.IsValid)
                    return;

                try
                {
                    if (TryPrepareMovingTargetStateZeroContinuation(
                            hunterUnitId,
                            timestamp,
                            out Candidate movingTargetCandidate))
                    {
                        stagedCandidate = movingTargetCandidate;
                    }
                }
                catch (Exception exception)
                {
                    LogDiagnostic(
                        "Improved Hunters moving-target State-0 preparation failed independently; " +
                        $"hunter={hunterUnitId}, error={exception.Message}.",
                        warning: true);
                }
            }
        }

        private void CompleteStateZeroQuery(NativePointer<X64SmartCPUContext> context)
        {
            int hunterUnitId = unchecked((int)(uint)context.Pointer->RBX);
            int vanillaTargetUnitId = unchecked((int)(uint)context.Pointer->RAX);
            if (!hookConfirmed && hunterUnitId > 0)
            {
                hookConfirmed = true;
                LogDiagnostic(
                    "Improved Hunters target-search fallback hook confirmed: " +
                    $"hunter={hunterUnitId}, vanillaTarget={vanillaTargetUnitId}, " +
                    $"stagedTarget={stagedCandidate.PreyUnitId}.");
            }

            if (!IsAvailable ||
                (!canRunTargetSelection() && !canRunPathfinding()) ||
                activeGeneration != generation ||
                activeHunterUnitId != hunterUnitId)
            {
                ClearQueryState();
                return;
            }

            if (canRunPathfinding() && stagedCandidate.IsForcedContinuation)
            {
                Candidate forcedCandidate = stagedCandidate;
                bool forcedCandidateValid =
                    TryValidateOwnReservationCandidate(
                        forcedCandidate,
                        requiredAiState: 0,
                        allowReleasedStateZeroTransition: true);
                if (forcedCandidateValid)
                {
                    pendingMoveCandidate = forcedCandidate;
                    context.Pointer->RAX = unchecked((ulong)(uint)forcedCandidate.PreyUnitId);
                    if (forcedCandidate.IsPostShotContinuation)
                    {
                        try
                        {
                            recordPostShotStateZeroHandoff(
                                forcedCandidate.PostShotContinuation,
                                vanillaTargetUnitId);
                        }
                        catch (Exception exception)
                        {
                            LogDiagnostic(
                                "Improved Hunters post-shot State-0 handoff recording failed independently: " +
                                $"hunter={hunterUnitId}, target={forcedCandidate.PreyUnitId}/" +
                                $"{forcedCandidate.PreyGlobalId}, error={exception.Message}.",
                                warning: true);
                        }
                    }
                    else
                    {
                        lock (observationLock)
                            pendingMovingTargetContinuations.Remove(hunterUnitId);
                    }

                    LogDiagnostic(
                        "Improved Hunters target-search fallback supplied forced same-identity target: " +
                        $"hunter={hunterUnitId}, target={forcedCandidate.PreyUnitId}/" +
                        $"{forcedCandidate.PreyType}, globalId={forcedCandidate.PreyGlobalId}, " +
                        $"source={forcedCandidate.Source}, vanillaQueryResult={vanillaTargetUnitId}, " +
                        "handoff=Vanilla-MoveHere, registerOverride=RAX-query-result-only.");
                    ClearQueryState(keepPendingMove: true);
                    return;
                }

                if (forcedCandidate.IsMovingTargetReplan)
                {
                    lock (observationLock)
                        pendingMovingTargetContinuations.Remove(hunterUnitId);
                }
                stagedCandidate = default;
            }

            if (vanillaTargetUnitId != 0)
            {
                if (TryCreateVanillaCandidate(
                        hunterUnitId,
                        vanillaTargetUnitId,
                        out Candidate vanillaCandidate))
                {
                    pendingMoveCandidate = vanillaCandidate;
                    ClearQueryState(keepPendingMove: true);
                }
                else
                {
                    ClearQueryState();
                }

                return;
            }

            bool stagedCandidateValid = canRunTargetSelection() &&
                stagedCandidate.IsValid &&
                TryValidateCandidate(stagedCandidate);
            if (!stagedCandidateValid)
            {
                ClearQueryState();
                return;
            }

            Candidate fallbackCandidate = stagedCandidate.AsSuppliedFallback();
            pendingMoveCandidate = fallbackCandidate;
            context.Pointer->RAX = unchecked((ulong)(uint)fallbackCandidate.PreyUnitId);
            LogDiagnostic(
                "Improved Hunters target-search fallback supplied hidden candidate: " +
                $"hunter={hunterUnitId}, target={fallbackCandidate.PreyUnitId}/{fallbackCandidate.PreyType}, " +
                $"globalId={fallbackCandidate.PreyGlobalId}, preferred={fallbackCandidate.Preferred}, " +
                "vanillaQueryResult=0, handoff=Vanilla-MoveHere.");
            ClearQueryState(keepPendingMove: true);
        }

        private void ObserveStateZeroMoveResult(NativePointer<X64SmartCPUContext> context)
        {
            Candidate candidate = pendingMoveCandidate;
            pendingMoveCandidate = default;
            if (!candidate.IsValid || candidate.HunterUnitId != unchecked((int)(uint)context.Pointer->RBX))
                return;

            int moveResult = unchecked((int)(uint)context.Pointer->RAX);
            long timestamp = Stopwatch.GetTimestamp();
            string movementSnapshot = TryFormatMovementSnapshot(candidate.HunterUnitId);
            if (candidate.IsPostShotContinuation)
            {
                try
                {
                    recordPostShotMoveHereResult(candidate.PostShotContinuation, moveResult);
                }
                catch (Exception exception)
                {
                    LogDiagnostic(
                        "Improved Hunters post-shot MoveHere result recording failed independently: " +
                        $"hunter={candidate.HunterUnitId}, target={candidate.PreyUnitId}/" +
                        $"{candidate.PreyGlobalId}, moveResult={moveResult}, error={exception.Message}.",
                        warning: true);
                }
            }
            try
            {
                recordPclMoveHereResult(
                    candidate.HunterUnitId,
                    candidate.PreyUnitId,
                    candidate.PreyGlobalId,
                    candidate.PreyType,
                    moveResult,
                    timestamp);
            }
            catch (Exception exception)
            {
                LogDiagnostic(
                    "Improved Hunters PCL/MoveHere callback failed independently: " +
                    $"hunter={candidate.HunterUnitId}, target={candidate.PreyUnitId}, " +
                    $"globalId={candidate.PreyGlobalId}, moveResult={moveResult}, error={exception.Message}.",
                    warning: true);
            }

            if (moveResult != 0)
            {
                if (!candidate.IsForcedContinuation)
                {
                    try
                    {
                        resetPostShotAttemptBudget(
                            candidate.HunterUnitId,
                            candidate.PreyUnitId,
                            candidate.PreyGlobalId);
                    }
                    catch (Exception exception)
                    {
                        LogDiagnostic(
                            "Improved Hunters recovery-attempt reset failed independently: " +
                            $"hunter={candidate.HunterUnitId}, target={candidate.PreyUnitId}/" +
                            $"{candidate.PreyGlobalId}, error={exception.Message}.",
                            warning: true);
                    }
                }

                AcceptedMoveObservation acceptedObservation =
                    CaptureAcceptedMoveObservation(candidate, timestamp);
                lock (observationLock)
                {
                    acceptedMoveObservations[candidate.HunterUnitId] = acceptedObservation;
                    pendingMovingTargetContinuations.Remove(candidate.HunterUnitId);
                }

                LogDiagnostic(
                    "Improved Hunters target-search fallback accepted by Vanilla MoveHere: " +
                    $"hunter={candidate.HunterUnitId}, target={candidate.PreyUnitId}/{candidate.PreyType}, " +
                    $"globalId={candidate.PreyGlobalId}, source={candidate.Source}, " +
                    $"moveResult={moveResult}, followup=Vanilla-state1, " +
                    $"acceptedPathGeneration={acceptedObservation.PathGeneration}, " +
                    $"targetAnchor={acceptedObservation.AnchorDescription}, " +
                    $"movingTargetReplans={acceptedObservation.MovingTargetReplans}/" +
                    $"{MaximumMovingTargetReplans}, " +
                    $"transitionPhase=after-MoveHere, {movementSnapshot}.");
                return;
            }

            lock (observationLock)
            {
                acceptedMoveObservations.Remove(candidate.HunterUnitId);
                pendingMovingTargetContinuations.Remove(candidate.HunterUnitId);
            }

            try
            {
                registerRejectedMove(candidate.HunterUnitId, candidate.PreyGlobalId, timestamp);
            }
            catch (Exception exception)
            {
                LogDiagnostic(
                    "Improved Hunters target-search fallback failed to register rejected MoveHere cooldown: " +
                    $"hunter={candidate.HunterUnitId}, target={candidate.PreyUnitId}, " +
                    $"globalId={candidate.PreyGlobalId}, error={exception.Message}.",
                    warning: true);
            }

            try
            {
                CleanupRejectedMove(candidate);
            }
            catch (Exception exception)
            {
                LogDiagnostic(
                    "Improved Hunters target-search fallback failed to clean rejected MoveHere context: " +
                    $"hunter={candidate.HunterUnitId}, target={candidate.PreyUnitId}, " +
                    $"globalId={candidate.PreyGlobalId}, error={exception.Message}.",
                    warning: true);
            }
        }

        private bool TryCreateOwnReservationRefreshCandidate(
            int hunterUnitId,
            ushort requiredAiState,
            out Candidate candidate)
        {
            candidate = default;
            if (!TryValidateHunter(hunterUnitId, requiredAiState, out GameUnit* hunter))
                return false;

            byte* hunterBytes = (byte*)hunter;
            int preyUnitId = *(ushort*)(hunterBytes + HunterTargetUnitIdOffset);
            uint preyGlobalId = *(uint*)(hunterBytes + HunterTargetGlobalIdOffset);
            if (preyUnitId <= 0 ||
                preyGlobalId == 0 ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(preyUnitId, out GameUnit* prey) ||
                prey == null ||
                !settings.IsKnownAnimal(prey->r_UnitChimp))
            {
                return false;
            }

            candidate = new Candidate(
                hunterUnitId,
                preyUnitId,
                preyGlobalId,
                prey->r_UnitChimp,
                preferred: true,
                suppliedByFallback: false);
            return TryValidateOwnReservationCandidate(candidate, requiredAiState);
        }

        private bool TryValidateOwnReservationCandidate(
            Candidate candidate,
            ushort requiredAiState,
            bool allowReleasedStateZeroTransition = false)
        {
            if (!TryValidateHunter(candidate.HunterUnitId, requiredAiState, out GameUnit* hunter))
            {
                return false;
            }

            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (!unitApi.TryGetUnitById(candidate.PreyUnitId, out GameUnit* prey) ||
                prey == null ||
                prey->r_AliveState != AliveState.IsAlive ||
                prey->r_CurrentHealth == 0 ||
                prey->r_GlobalId != candidate.PreyGlobalId ||
                prey->r_UnitChimp != candidate.PreyType ||
                !settings.IsHuntingEnabled(prey->r_UnitChimp))
            {
                return false;
            }

            byte* hunterBytes = (byte*)hunter;
            byte* preyBytes = (byte*)prey;
            ushort targetUnitId = *(ushort*)(hunterBytes + HunterTargetUnitIdOffset);
            uint targetGlobalId = *(uint*)(hunterBytes + HunterTargetGlobalIdOffset);
            ushort reservation = *(ushort*)(preyBytes + PreyReservationOffset);
            bool targetMatches =
                targetUnitId == candidate.PreyUnitId &&
                targetGlobalId == candidate.PreyGlobalId;
            bool targetWasCleared = targetUnitId == 0 && targetGlobalId == 0;
            return (targetMatches || (allowReleasedStateZeroTransition && targetWasCleared)) &&
                *(ushort*)(preyBytes + PreyCorpseFlagOffset) == 0 &&
                (reservation == 2 || (allowReleasedStateZeroTransition && reservation == 0)) &&
                !IsTargetedByOtherLiveHunter(candidate);
        }

        private bool TryCreateVanillaCandidate(
            int hunterUnitId,
            int preyUnitId,
            out Candidate candidate)
        {
            candidate = default;
            if (preyUnitId <= 0 ||
                preyUnitId > ushort.MaxValue ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(preyUnitId, out GameUnit* prey) ||
                prey == null ||
                prey->r_GlobalId == 0 ||
                !settings.IsKnownAnimal(prey->r_UnitChimp) ||
                !settings.IsHuntingEnabled(prey->r_UnitChimp))
            {
                return false;
            }

            candidate = new Candidate(
                hunterUnitId,
                preyUnitId,
                prey->r_GlobalId,
                prey->r_UnitChimp,
                preferred: false,
                suppliedByFallback: false);
            return TryValidateCandidate(candidate);
        }

        private bool TryValidateCandidate(Candidate candidate)
        {
            if (!TryValidateHunter(candidate.HunterUnitId, requiredAiState: 0, out GameUnit* hunter))
                return false;

            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (!unitApi.TryGetUnitById(candidate.PreyUnitId, out GameUnit* prey) ||
                prey == null ||
                prey->r_AliveState != AliveState.IsAlive ||
                prey->r_CurrentHealth == 0 ||
                prey->r_GlobalId != candidate.PreyGlobalId ||
                prey->r_UnitChimp != candidate.PreyType ||
                !settings.IsHuntingEnabled(prey->r_UnitChimp))
            {
                return false;
            }

            byte* preyBytes = (byte*)prey;
            return *(ushort*)(preyBytes + PreyCorpseFlagOffset) == 0 &&
                *(ushort*)(preyBytes + PreyReservationOffset) == 0 &&
                hunter->r_GlobalId != 0;
        }

        private static bool TryValidateHunter(
            int hunterUnitId,
            ushort requiredAiState,
            out GameUnit* hunter)
        {
            hunter = null;
            if (hunterUnitId <= 0 ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(hunterUnitId, out hunter) ||
                hunter == null ||
                hunter->r_AliveState != AliveState.IsAlive ||
                hunter->r_CurrentHealth == 0 ||
                hunter->r_GlobalId == 0 ||
                hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER)
            {
                return false;
            }

            return *(ushort*)((byte*)hunter + HunterAiStateOffset) == requiredAiState;
        }

        private void CleanupRejectedMove(Candidate candidate)
        {
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (!unitApi.TryGetUnitById(candidate.HunterUnitId, out GameUnit* hunter) ||
                hunter == null ||
                !unitApi.TryGetUnitById(candidate.PreyUnitId, out GameUnit* prey) ||
                prey == null ||
                hunter->r_GlobalId == 0 ||
                prey->r_GlobalId != candidate.PreyGlobalId)
            {
                LogDiagnostic(
                    "Improved Hunters target-search fallback MoveHere rejection cleanup skipped: " +
                    $"hunter={candidate.HunterUnitId}, target={candidate.PreyUnitId}, " +
                    $"globalId={candidate.PreyGlobalId}, reason=identity-changed.",
                    warning: true);
                return;
            }

            byte* hunterBytes = (byte*)hunter;
            byte* preyBytes = (byte*)prey;
            ushort hunterTargetUnitId = *(ushort*)(hunterBytes + HunterTargetUnitIdOffset);
            uint hunterTargetGlobalId = *(uint*)(hunterBytes + HunterTargetGlobalIdOffset);
            ushort reservationBefore = *(ushort*)(preyBytes + PreyReservationOffset);
            bool targetMatches =
                hunterTargetUnitId == candidate.PreyUnitId &&
                hunterTargetGlobalId == candidate.PreyGlobalId;
            bool targetedByOtherHunter = IsTargetedByOtherLiveHunter(candidate);

            if (targetMatches)
            {
                *(ushort*)(hunterBytes + HunterTargetUnitIdOffset) = 0;
                *(uint*)(hunterBytes + HunterTargetGlobalIdOffset) = 0;
                if (*(uint*)(hunterBytes + HunterOrderTargetGlobalIdOffset) == candidate.PreyGlobalId)
                    *(uint*)(hunterBytes + HunterOrderTargetGlobalIdOffset) = 0;
            }

            bool reservationReleased = false;
            if (reservationBefore == 2 && !targetedByOtherHunter)
            {
                *(ushort*)(preyBytes + PreyReservationOffset) = 0;
                reservationReleased = *(ushort*)(preyBytes + PreyReservationOffset) == 0;
            }

            ushort targetAfter = *(ushort*)(hunterBytes + HunterTargetUnitIdOffset);
            uint targetGlobalAfter = *(uint*)(hunterBytes + HunterTargetGlobalIdOffset);
            ushort reservationAfter = *(ushort*)(preyBytes + PreyReservationOffset);
            bool cleanupValid =
                (!targetMatches || (targetAfter == 0 && targetGlobalAfter == 0)) &&
                (reservationBefore != 2 || targetedByOtherHunter || reservationReleased);
            LogDiagnostic(
                "Improved Hunters target-search fallback rejected by Vanilla MoveHere: " +
                $"hunter={candidate.HunterUnitId}, target={candidate.PreyUnitId}/{candidate.PreyType}, " +
                $"globalId={candidate.PreyGlobalId}, source={candidate.Source}, " +
                $"moveResult=0, targetMatched={targetMatches}, " +
                $"targetAfter={targetAfter}/{targetGlobalAfter}, reservation={reservationBefore}->{reservationAfter}, " +
                $"targetedByOtherHunter={targetedByOtherHunter}, cooldownSeconds=" +
                $"{RejectedCandidateCooldown / Stopwatch.Frequency}, cleanupValid={cleanupValid}, " +
                "followup=Vanilla-state7.",
                warning: !cleanupValid);
        }

        private static bool IsTargetedByOtherLiveHunter(Candidate candidate)
        {
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            if (units._array == null || units.Length == 0)
                return false;

            for (int index = 0; index < units.Length; index++)
            {
                int unitId = index + 1;
                if (unitId == candidate.HunterUnitId)
                    continue;

                GameUnit* hunter = units.GetValuePointer(index);
                if (hunter->r_AliveState != AliveState.IsAlive ||
                    hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER)
                {
                    continue;
                }

                byte* hunterBytes = (byte*)hunter;
                if (*(ushort*)(hunterBytes + HunterTargetUnitIdOffset) == candidate.PreyUnitId &&
                    *(uint*)(hunterBytes + HunterTargetGlobalIdOffset) == candidate.PreyGlobalId)
                {
                    return true;
                }
            }

            return false;
        }

        private void LogDiagnostic(string message, bool warning = false)
        {
            if (diagnosticLogs >= MaxDiagnosticLogs)
                return;

            diagnosticLogs++;
            string boundedMessage = $"{message} ({diagnosticLogs}/{MaxDiagnosticLogs}).";
            if (warning)
                Shared.DebugLogHelper.LogWarning(log, boundedMessage);
            else
                Shared.DebugLogHelper.LogInfo(log, boundedMessage);
        }

        private void LogStateOneDiagnostic(string message, bool warning = false)
        {
            if (stateOneDiagnosticLogs >= MaxStateOneDiagnosticLogs)
                return;

            stateOneDiagnosticLogs++;
            string boundedMessage =
                $"{message} ({stateOneDiagnosticLogs}/{MaxStateOneDiagnosticLogs}).";
            if (warning)
                Shared.DebugLogHelper.LogWarning(log, boundedMessage);
            else
                Shared.DebugLogHelper.LogInfo(log, boundedMessage);
        }

        private static string TryFormatMovementSnapshot(int hunterUnitId)
        {
            try
            {
                if (hunterUnitId <= 0 ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(hunterUnitId, out GameUnit* hunter) ||
                    hunter == null)
                {
                    return "snapshot=unavailable";
                }

                return HunterMovementSnapshot.TryFormat(hunter);
            }
            catch
            {
                // Snapshot lookup is diagnostic-only and must not change handoff behavior.
                return "snapshot=failed";
            }
        }

        private static void ClearQueryState(bool keepPendingMove = false)
        {
            activeGeneration = 0;
            activeHunterUnitId = 0;
            stagedCandidate = default;
            if (!keepPendingMove)
                pendingMoveCandidate = default;
        }

        private static void ClearThreadState()
        {
            ClearQueryState();
            stagedMovingTargetReplan = default;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            featureAvailable = false;
            transaction?.Unload();
            transaction?.Dispose();
            transaction = null;
            lock (observationLock)
            {
                acceptedMoveObservations.Clear();
                pendingMovingTargetContinuations.Clear();
                loggedNearRefreshBypasses.Clear();
            }
            ClearThreadState();
        }

        private readonly struct Candidate
        {
            public readonly int HunterUnitId;
            public readonly int PreyUnitId;
            public readonly uint PreyGlobalId;
            public readonly eChimps PreyType;
            public readonly bool Preferred;
            public readonly bool SuppliedByFallback;
            public readonly HunterPostShotContinuationCandidate PostShotContinuation;
            public readonly bool MovingTargetReplan;
            public readonly int MovingTargetReplanAttempt;
            public readonly long MovingTargetSourceGeneration;

            public Candidate(
                int hunterUnitId,
                int preyUnitId,
                uint preyGlobalId,
                eChimps preyType,
                bool preferred,
                bool suppliedByFallback,
                HunterPostShotContinuationCandidate postShotContinuation = default,
                bool movingTargetReplan = false,
                int movingTargetReplanAttempt = 0,
                long movingTargetSourceGeneration = 0)
            {
                HunterUnitId = hunterUnitId;
                PreyUnitId = preyUnitId;
                PreyGlobalId = preyGlobalId;
                PreyType = preyType;
                Preferred = preferred;
                SuppliedByFallback = suppliedByFallback;
                PostShotContinuation = postShotContinuation;
                MovingTargetReplan = movingTargetReplan;
                MovingTargetReplanAttempt = movingTargetReplanAttempt;
                MovingTargetSourceGeneration = movingTargetSourceGeneration;
            }

            public bool IsValid => HunterUnitId > 0 && PreyUnitId > 0 && PreyGlobalId != 0;

            public bool IsPostShotContinuation => PostShotContinuation.IsValid;

            public bool IsMovingTargetReplan => MovingTargetReplan;

            public bool IsForcedContinuation =>
                IsPostShotContinuation || IsMovingTargetReplan;

            public string Source
            {
                get
                {
                    if (IsPostShotContinuation)
                        return "PostShotContinuation";
                    if (IsMovingTargetReplan)
                        return "MovingTargetReplan";
                    return SuppliedByFallback ? "InjectedFallback" : "VanillaQuery";
                }
            }

            public Candidate AsSuppliedFallback() => new Candidate(
                HunterUnitId,
                PreyUnitId,
                PreyGlobalId,
                PreyType,
                Preferred,
                suppliedByFallback: true,
                postShotContinuation: PostShotContinuation,
                movingTargetReplan: MovingTargetReplan,
                movingTargetReplanAttempt: MovingTargetReplanAttempt,
                movingTargetSourceGeneration: MovingTargetSourceGeneration);

            public Candidate AsMovingTargetReplan(int attempt, long sourceGeneration) =>
                new Candidate(
                    HunterUnitId,
                    PreyUnitId,
                    PreyGlobalId,
                    PreyType,
                    preferred: true,
                    suppliedByFallback: true,
                    postShotContinuation: default,
                    movingTargetReplan: true,
                    movingTargetReplanAttempt: attempt,
                    movingTargetSourceGeneration: sourceGeneration);

            public static Candidate FromPostShot(HunterPostShotContinuationCandidate candidate) =>
                new Candidate(
                    candidate.HunterUnitId,
                    candidate.PreyUnitId,
                    candidate.PreyGlobalId,
                    candidate.PreyType,
                    preferred: true,
                    suppliedByFallback: true,
                    postShotContinuation: candidate);
        }

        private readonly struct AcceptedMoveObservation
        {
            public readonly Candidate Candidate;
            public readonly long AcceptedAt;
            public readonly long PathGeneration;
            public readonly int MovingTargetReplans;
            public readonly bool HasPathAnchor;
            public readonly int AnchorTileX;
            public readonly int AnchorTileY;
            public readonly uint PathLength;
            public readonly bool ReplanRequested;

            public AcceptedMoveObservation(
                Candidate candidate,
                long acceptedAt,
                long pathGeneration,
                int movingTargetReplans,
                bool hasPathAnchor = false,
                int anchorTileX = 0,
                int anchorTileY = 0,
                uint pathLength = 0,
                bool replanRequested = false)
            {
                Candidate = candidate;
                AcceptedAt = acceptedAt;
                PathGeneration = pathGeneration;
                MovingTargetReplans = movingTargetReplans;
                HasPathAnchor = hasPathAnchor;
                AnchorTileX = anchorTileX;
                AnchorTileY = anchorTileY;
                PathLength = pathLength;
                ReplanRequested = replanRequested;
            }

            public string AnchorDescription =>
                HasPathAnchor ? $"{AnchorTileX},{AnchorTileY}" : "unavailable";

            public bool Matches(Candidate candidate) =>
                Candidate.HunterUnitId == candidate.HunterUnitId &&
                Candidate.PreyUnitId == candidate.PreyUnitId &&
                Candidate.PreyGlobalId == candidate.PreyGlobalId &&
                Candidate.PreyType == candidate.PreyType;

            public AcceptedMoveObservation WithReplanRequested() =>
                new AcceptedMoveObservation(
                    Candidate,
                    AcceptedAt,
                    PathGeneration,
                    MovingTargetReplans,
                    HasPathAnchor,
                    AnchorTileX,
                    AnchorTileY,
                    PathLength,
                    replanRequested: true);
        }
    }
}
