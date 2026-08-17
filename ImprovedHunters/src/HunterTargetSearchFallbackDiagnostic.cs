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
        HandoffToVanillaAttack = 2
    }

    internal delegate HunterStateOneNearRefreshAction TryPrepareHunterStateOneNearRefresh(
        int hunterUnitId,
        int preyUnitId,
        uint preyGlobalId,
        int nativeWorldDistance,
        out bool shouldLog);

    /// <summary>
    /// Temporary, separately removable validation of the native target-search
    /// handoff, the state-1 near-target refresh and its direct-attack
    /// continuation. State 0 may receive a validated fallback target. State 1
    /// only selects Vanilla's existing continuation branch for an already
    /// validated path; Vanilla remains responsible for locomotion and orders.
    /// </summary>
    internal sealed unsafe class HunterTargetSearchFallbackDiagnostic : IDisposable
    {
        private const string ReferenceDllSha256 =
            "33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469";
        private const int StateZeroQuerySequenceRva = 0x12FD67;
        private const int StateZeroQueryReturnHookOffset = 0x22;
        private const int StateZeroMoveResultSequenceRva = 0x12FE13;
        private const int StateZeroMoveResultHookOffset = 0x17;
        private const int StateOneRefreshQuerySequenceRva = 0x12FF07;
        private const int StateOneNearRefreshBranchSequenceRva = 0x130019;
        private const int StateOneNearRefreshHookLength = 0x0F;
        private const int StateOneNearRefreshFarBranchTargetOffset = 0x14;
        private const int StateOneNearRefreshQueryJumpOffset = 0x0F;
        private const int StateOneWorldDistanceScratchRva = 0x34A8F5C;
        private const int StateOneCurrentHunterUnitIdRva = 0x92F2C4;
        private const int StateOneRefreshDistance = 20;
        private const int StateOneContinuationDistance = 28;
        private const int StateOneBypassDistance = StateOneRefreshDistance + 1;
        private const int StateOneDirectAttackSequenceRva = 0x13013D;
        private const int StateOneDirectAttackResultHookOffset = 0x0C;
        private const int HunterUpdateStartRva = 0x12FC20;
        private const int HunterUpdateEndRva = 0x1313D2;
        private const int HunterQueryFunctionRva = 0x18AF00;
        private const int MoveHereFunctionRva = 0x196230;
        private const int DirectAttackFunctionRva = 0x18E950;
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
        private const int MaxDiagnosticLogs = 160;
        private const int MaxStateOneDiagnosticLogs = 160;

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
        private static readonly long AcceptedMoveObservationLifetime = Stopwatch.Frequency * 60;
        private static long nextGeneration;

        [ThreadStatic] private static long activeGeneration;
        [ThreadStatic] private static int activeHunterUnitId;
        [ThreadStatic] private static Candidate stagedCandidate;
        [ThreadStatic] private static Candidate pendingMoveCandidate;

        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly Func<bool> canRun;
        private readonly TryPrepareHunterStateOneNearRefresh tryPrepareStateOneNearRefresh;
        private readonly Action<int, uint, long> registerRejectedMove;
        private readonly Action<int, int, uint, eChimps, int, long> recordPclMoveHereResult;
        private readonly long generation;
        private int* stateOneWorldDistanceScratch;
        private int* stateOneCurrentHunterUnitId;
        private readonly object observationLock = new object();
        private readonly Dictionary<int, AcceptedMoveObservation> acceptedMoveObservations =
            new Dictionary<int, AcceptedMoveObservation>();
        private readonly HashSet<ulong> loggedNearRefreshBypasses = new HashSet<ulong>();
        private HookTransaction transaction;
        private HookRef<X64InlineHook> queryStartHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> queryReturnHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> moveResultHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> stateOneRefreshBranchContextHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> stateOneDirectAttackResultHook = new HookRef<X64InlineHook>();
        private bool featureAvailable;
        private bool hookConfirmed;
        private bool stateOneNearRefreshHookConfirmed;
        private bool stateOneHookConfirmed;
        private bool stateOneInvalidContextLogged;
        private int diagnosticLogs;
        private int stateOneDiagnosticLogs;
        private bool disposed;

        public HunterTargetSearchFallbackDiagnostic(
            ManualLogSource log,
            ImprovedHuntersViewModel settings,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches,
            Func<bool> canRun,
            TryPrepareHunterStateOneNearRefresh tryPrepareStateOneNearRefresh,
            Action<int, uint, long> registerRejectedMove,
            Action<int, int, uint, eChimps, int, long> recordPclMoveHereResult)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.canRun = canRun ?? throw new ArgumentNullException(nameof(canRun));
            this.tryPrepareStateOneNearRefresh = tryPrepareStateOneNearRefresh ??
                throw new ArgumentNullException(nameof(tryPrepareStateOneNearRefresh));
            this.registerRejectedMove = registerRejectedMove ??
                throw new ArgumentNullException(nameof(registerRejectedMove));
            this.recordPclMoveHereResult = recordPclMoveHereResult ??
                throw new ArgumentNullException(nameof(recordPclMoveHereResult));
            generation = Interlocked.Increment(ref nextGeneration);

            if (!referenceHashMatches)
            {
                Shared.DebugLogHelper.LogWarning(
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
                    $"MoveHereRva=0x{moveHereFunctionRva:X}, " +
                    $"stateOneAttackResultRva=0x{stateOneDirectAttackResultRva:X}, " +
                    $"directAttackRva=0x{directAttackFunctionRva:X}, cooldownSeconds=" +
                    $"{RejectedCandidateCooldown / Stopwatch.Frequency}, ownMovement=False, ownAiState=False, " +
                    $"worldDistanceScratchRva=0x{StateOneWorldDistanceScratchRva:X}, " +
                    $"currentHunterUnitIdRva=0x{StateOneCurrentHunterUnitIdRva:X}, " +
                    "stateOneRefreshOverride=world-distance-scratch-20-to-21-only, " +
                    "nearRefreshDecision=bidirectional-native-visibility, " +
                    "continuationTicketRequiredWhenBlocked=False, " +
                    "tileAttackDecision=active-visibility-snapshot, ownReservationRequired=2, " +
                    "foreignReservationAllowed=False, stateOneQueryResultMutation=False, " +
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
                loggedNearRefreshBypasses.Clear();
            }

            diagnosticLogs = 0;
            stateOneDiagnosticLogs = 0;
            hookConfirmed = false;
            stateOneNearRefreshHookConfirmed = false;
            stateOneHookConfirmed = false;
            stateOneInvalidContextLogged = false;
            ClearThreadState();
        }

        private void BeginStateZeroQuery(NativePointer<X64SmartCPUContext> context)
        {
            ClearThreadState();
            if (!IsAvailable || !canRun())
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
                !canRun() ||
                activeGeneration != generation ||
                activeHunterUnitId != hunterUnitId)
            {
                ClearQueryState();
                return;
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

            bool stagedCandidateValid = stagedCandidate.IsValid &&
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
                lock (observationLock)
                {
                    acceptedMoveObservations[candidate.HunterUnitId] =
                        new AcceptedMoveObservation(candidate, timestamp);
                }

                LogDiagnostic(
                    "Improved Hunters target-search fallback accepted by Vanilla MoveHere: " +
                    $"hunter={candidate.HunterUnitId}, target={candidate.PreyUnitId}/{candidate.PreyType}, " +
                    $"globalId={candidate.PreyGlobalId}, source={candidate.Source}, " +
                    $"moveResult={moveResult}, followup=Vanilla-state1, " +
                    $"transitionPhase=after-MoveHere, {movementSnapshot}.");
                return;
            }

            lock (observationLock)
                acceptedMoveObservations.Remove(candidate.HunterUnitId);

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

        private void CaptureStateOneNearRefreshContext(NativePointer<X64SmartCPUContext> context)
        {
            ClearThreadState();
            if (!IsAvailable ||
                !canRun() ||
                stateOneWorldDistanceScratch == null ||
                stateOneCurrentHunterUnitId == null)
            {
                return;
            }

            int nativeWorldDistance = *stateOneWorldDistanceScratch;
            if ((uint)nativeWorldDistance > StateOneContinuationDistance)
                return;

            // Vanilla itself loads this actor ID for the immediately following
            // near-target query. RBX is not used as an undocumented substitute.
            int hunterUnitId = *stateOneCurrentHunterUnitId;
            if (!TryCreateOwnReservationRefreshCandidate(
                    hunterUnitId,
                    requiredAiState: 1,
                    out Candidate refreshCandidate))
            {
                return;
            }

            if (!stateOneNearRefreshHookConfirmed)
            {
                stateOneNearRefreshHookConfirmed = true;
                LogDiagnostic(
                    "Improved Hunters state-1 near-distance hook confirmed: " +
                    $"hunter={hunterUnitId}, target={refreshCandidate.PreyUnitId}/" +
                    $"{refreshCandidate.PreyGlobalId}, nativeWorldDistance={nativeWorldDistance}.");
            }

            HunterStateOneNearRefreshAction action = tryPrepareStateOneNearRefresh(
                    hunterUnitId,
                    refreshCandidate.PreyUnitId,
                    refreshCandidate.PreyGlobalId,
                    nativeWorldDistance,
                    out bool shouldLog);
            if (action == HunterStateOneNearRefreshAction.None)
                return;

            if (nativeWorldDistance > StateOneRefreshDistance)
                return;

            // This scratch value is the operand of the immediately relocated
            // Vanilla CMP. The next distance helper invocation overwrites it.
            *stateOneWorldDistanceScratch = StateOneBypassDistance;
            ulong bypassIdentity =
                (unchecked((ulong)(uint)hunterUnitId) << 32) | refreshCandidate.PreyGlobalId;
            lock (observationLock)
                shouldLog |= loggedNearRefreshBypasses.Add(bypassIdentity);
            if (shouldLog)
            {
                LogStateOneDiagnostic(
                    "Improved Hunters bypassed Hunter state-1 near-target refresh: " +
                    $"hunter={hunterUnitId}, target={refreshCandidate.PreyUnitId}/" +
                    $"{refreshCandidate.PreyGlobalId}/{refreshCandidate.PreyType}, " +
                    $"nativeWorldDistance={nativeWorldDistance}->{StateOneBypassDistance}, " +
                    $"branch=Vanilla-greater-than-20, querySkipped=True, action={action}, " +
                    "continuationTicket=False, " +
                    "ownMovement=False, ownAiState=False, ownOrderWrite=False, " +
                    "speedWrite=False, animationWrite=False, compareScratchOnly=True, " +
                    $"{TryFormatMovementSnapshot(hunterUnitId)}.");
            }
        }

        private void ObserveStateOneDirectAttackResult(NativePointer<X64SmartCPUContext> context)
        {
            if (!IsAvailable || !canRun())
                return;

            int hunterUnitId = unchecked((int)(uint)context.Pointer->RDX);
            int attackResult = unchecked((int)(uint)context.Pointer->RAX);
            long timestamp = Stopwatch.GetTimestamp();
            AcceptedMoveObservation observation;
            lock (observationLock)
            {
                if (!acceptedMoveObservations.TryGetValue(hunterUnitId, out observation) ||
                    timestamp - observation.AcceptedAt > AcceptedMoveObservationLifetime)
                {
                    acceptedMoveObservations.Remove(hunterUnitId);
                    LogInvalidStateOneContextOnce(
                        hunterUnitId,
                        attackResult,
                        "no-recent-correlated-state0-move");
                    return;
                }

                if (attackResult != 0)
                    acceptedMoveObservations.Remove(hunterUnitId);
            }

            Candidate candidate = observation.Candidate;
            if (!TryValidateHunter(hunterUnitId, requiredAiState: 1, out GameUnit* hunter) ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(candidate.PreyUnitId, out GameUnit* prey) ||
                prey == null ||
                prey->r_AliveState != AliveState.IsAlive ||
                prey->r_GlobalId != candidate.PreyGlobalId)
            {
                LogInvalidStateOneContextOnce(
                    hunterUnitId,
                    attackResult,
                    "hunter-or-prey-identity-invalid");
                return;
            }

            byte* hunterBytes = (byte*)hunter;
            ushort targetUnitId = *(ushort*)(hunterBytes + HunterTargetUnitIdOffset);
            uint targetGlobalId = *(uint*)(hunterBytes + HunterTargetGlobalIdOffset);
            bool targetMatches =
                targetUnitId == candidate.PreyUnitId &&
                targetGlobalId == candidate.PreyGlobalId;
            if (!targetMatches)
            {
                LogInvalidStateOneContextOnce(
                    hunterUnitId,
                    attackResult,
                    $"target-mismatch-{targetUnitId}-{targetGlobalId}");
                return;
            }

            int nativeDistance = unchecked((int)(uint)context.Pointer->RDI);
            if (!stateOneHookConfirmed)
            {
                stateOneHookConfirmed = true;
                LogStateOneDiagnostic(
                    "Improved Hunters state-1 direct-attack result hook confirmed: " +
                    $"hunter={hunterUnitId}/{hunter->r_GlobalId}, target={candidate.PreyUnitId}/{candidate.PreyGlobalId}.");
            }

            LogStateOneDiagnostic(
                "Improved Hunters state-1 direct-attack observation: " +
                $"hunter={hunterUnitId}/{hunter->r_GlobalId}, target={candidate.PreyUnitId}/{candidate.PreyType}, " +
                $"globalId={candidate.PreyGlobalId}, source={candidate.Source}, attackResult={attackResult}, " +
                $"nativeDistance={nativeDistance}, pathState={*(ushort*)(hunterBytes + HunterPathStateOffset)}, " +
                $"pathFieldF4={*(ushort*)(hunterBytes + HunterPathFieldF4Offset)}, " +
                $"pathProgress={*(ushort*)(hunterBytes + HunterPathProgressOffset)}, " +
                    $"pathLength={*(uint*)(hunterBytes + HunterPathLengthOffset)}, " +
                $"acceptedAgeMs={(timestamp - observation.AcceptedAt) * 1000 / Stopwatch.Frequency}, " +
                $"hunterTile={hunter->r_CurrentTilePositionX},{hunter->r_CurrentTilePositionY}, " +
                $"preyTile={prey->r_CurrentTilePositionX},{prey->r_CurrentTilePositionY}, " +
                "behaviorMutation=False.");
        }

        private void LogInvalidStateOneContextOnce(
            int hunterUnitId,
            int attackResult,
            string reason)
        {
            if (stateOneInvalidContextLogged)
                return;

            stateOneInvalidContextLogged = true;
            LogStateOneDiagnostic(
                "Improved Hunters state-1 direct-attack observation skipped invalid context: " +
                $"hunter={hunterUnitId}, attackResult={attackResult}, reason={reason}, behaviorMutation=False.",
                warning: true);
        }

        private static void ValidateStateOneNearRefreshHookSpan(
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            int hookRva,
            int expectedQueryEntryRva,
            out ulong worldDistanceScratchAddress,
            out ulong currentHunterUnitIdAddress)
        {
            const int decodeLookahead = 32;
            if (hookRva < 0 || hookRva > memory.Length - decodeLookahead)
                throw new InvalidOperationException("State-1 near-refresh hook lies outside the module image.");

            ulong hookAddress = libraryBase + unchecked((ulong)hookRva);
            Decoder decoder = Decoder.Create(
                64,
                new ByteArrayCodeReader(memory.Slice(hookRva, decodeLookahead).ToArray()),
                hookAddress);
            Instruction compare = decoder.Decode();
            Instruction farBranch = decoder.Decode();
            Instruction nearPathLoad = decoder.Decode();
            int decodedHookLength = checked((int)(decoder.IP - hookAddress));
            if (compare.IsInvalid ||
                farBranch.IsInvalid ||
                nearPathLoad.IsInvalid ||
                compare.Mnemonic != Mnemonic.Cmp ||
                compare.Length != 7 ||
                farBranch.Mnemonic != Mnemonic.Jg ||
                farBranch.Length != 2 ||
                nearPathLoad.Mnemonic != Mnemonic.Mov ||
                nearPathLoad.Length != 6 ||
                decodedHookLength != StateOneNearRefreshHookLength)
            {
                throw new InvalidOperationException(
                    "State-1 near-refresh hook does not decode as the audited 7+2+6-byte span.");
            }

            ulong hookEndAddress = hookAddress + StateOneNearRefreshHookLength;
            ulong expectedFarBranchTarget =
                hookAddress + StateOneNearRefreshFarBranchTargetOffset;
            if (farBranch.FlowControl != FlowControl.ConditionalBranch ||
                farBranch.NearBranchTarget != expectedFarBranchTarget ||
                farBranch.NearBranchTarget < hookEndAddress)
            {
                throw new InvalidOperationException(
                    $"State-1 near-refresh far branch is unsafe: target=0x{farBranch.NearBranchTarget:X}, " +
                    $"hookSpan=[0x{hookAddress:X},0x{hookEndAddress:X}).");
            }

            if (!compare.IsIPRelativeMemoryOperand ||
                compare.IPRelativeMemoryAddress !=
                    libraryBase + unchecked((ulong)StateOneWorldDistanceScratchRva) ||
                !nearPathLoad.IsIPRelativeMemoryOperand ||
                nearPathLoad.IPRelativeMemoryAddress !=
                    libraryBase + unchecked((ulong)StateOneCurrentHunterUnitIdRva))
            {
                throw new InvalidOperationException(
                    "State-1 near-refresh scratch or current-Hunter address changed.");
            }

            Instruction queryJump = decoder.Decode();
            ulong expectedQueryEntryAddress =
                libraryBase + unchecked((ulong)expectedQueryEntryRva);
            if (queryJump.IsInvalid ||
                queryJump.IP != hookAddress + StateOneNearRefreshQueryJumpOffset ||
                queryJump.Mnemonic != Mnemonic.Jmp ||
                queryJump.FlowControl != FlowControl.UnconditionalBranch ||
                queryJump.NearBranchTarget != expectedQueryEntryAddress)
            {
                throw new InvalidOperationException(
                    $"State-1 near-refresh query jump changed: address=0x{queryJump.IP:X}, " +
                    $"target=0x{queryJump.NearBranchTarget:X}.");
            }

            ValidateNoExternalDirectBranchTargetsInsideHook(
                memory,
                libraryBase,
                hookAddress,
                hookEndAddress);
            worldDistanceScratchAddress = compare.IPRelativeMemoryAddress;
            currentHunterUnitIdAddress = nearPathLoad.IPRelativeMemoryAddress;
        }

        private static void ValidateNoExternalDirectBranchTargetsInsideHook(
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            ulong hookAddress,
            ulong hookEndAddress)
        {
            int functionLength = HunterUpdateEndRva - HunterUpdateStartRva;
            if (functionLength <= 0 ||
                HunterUpdateStartRva > memory.Length - functionLength)
            {
                throw new InvalidOperationException("HunterUpdate audit range lies outside the module image.");
            }

            ulong functionAddress = libraryBase + unchecked((ulong)HunterUpdateStartRva);
            ulong functionEndAddress = libraryBase + unchecked((ulong)HunterUpdateEndRva);
            Decoder decoder = Decoder.Create(
                64,
                new ByteArrayCodeReader(
                    memory.Slice(HunterUpdateStartRva, functionLength).ToArray()),
                functionAddress);
            while (decoder.IP < functionEndAddress)
            {
                Instruction instruction = decoder.Decode();
                if (instruction.IsInvalid || decoder.LastError != DecoderError.None)
                {
                    throw new InvalidOperationException(
                        $"HunterUpdate branch audit failed to decode RVA 0x" +
                        $"{instruction.IP - libraryBase:X}.");
                }

                bool hasDirectTarget =
                    instruction.Op0Kind == OpKind.NearBranch16 ||
                    instruction.Op0Kind == OpKind.NearBranch32 ||
                    instruction.Op0Kind == OpKind.NearBranch64;
                bool isAuditedFlowControl =
                    instruction.FlowControl == FlowControl.ConditionalBranch ||
                    instruction.FlowControl == FlowControl.UnconditionalBranch ||
                    instruction.FlowControl == FlowControl.Call;
                if (!hasDirectTarget || !isAuditedFlowControl)
                    continue;

                ulong target = instruction.NearBranchTarget;
                bool sourceOutsideHook =
                    instruction.IP < hookAddress || instruction.IP >= hookEndAddress;
                if (sourceOutsideHook && target > hookAddress && target < hookEndAddress)
                {
                    throw new InvalidOperationException(
                        $"Unsafe inbound branch into state-1 near-refresh hook span: " +
                        $"sourceRva=0x{instruction.IP - libraryBase:X}, " +
                        $"targetRva=0x{target - libraryBase:X}, " +
                        $"span=[0x{hookAddress - libraryBase:X}," +
                        $"0x{hookEndAddress - libraryBase:X}).");
                }
            }

            if (decoder.IP != functionEndAddress)
            {
                throw new InvalidOperationException(
                    $"HunterUpdate branch audit ended at unexpected RVA 0x" +
                    $"{decoder.IP - libraryBase:X}.");
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
            ushort requiredAiState)
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
            return *(ushort*)(hunterBytes + HunterTargetUnitIdOffset) == candidate.PreyUnitId &&
                *(uint*)(hunterBytes + HunterTargetGlobalIdOffset) == candidate.PreyGlobalId &&
                *(ushort*)(preyBytes + PreyCorpseFlagOffset) == 0 &&
                *(ushort*)(preyBytes + PreyReservationOffset) == 2 &&
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

            public Candidate(
                int hunterUnitId,
                int preyUnitId,
                uint preyGlobalId,
                eChimps preyType,
                bool preferred,
                bool suppliedByFallback)
            {
                HunterUnitId = hunterUnitId;
                PreyUnitId = preyUnitId;
                PreyGlobalId = preyGlobalId;
                PreyType = preyType;
                Preferred = preferred;
                SuppliedByFallback = suppliedByFallback;
            }

            public bool IsValid => HunterUnitId > 0 && PreyUnitId > 0 && PreyGlobalId != 0;

            public string Source
            {
                get
                {
                    return SuppliedByFallback ? "InjectedFallback" : "VanillaQuery";
                }
            }

            public Candidate AsSuppliedFallback() => new Candidate(
                HunterUnitId,
                PreyUnitId,
                PreyGlobalId,
                PreyType,
                Preferred,
                suppliedByFallback: true);
        }

        private readonly struct AcceptedMoveObservation
        {
            public readonly Candidate Candidate;
            public readonly long AcceptedAt;

            public AcceptedMoveObservation(Candidate candidate, long acceptedAt)
            {
                Candidate = candidate;
                AcceptedAt = acceptedAt;
            }
        }
    }
}
