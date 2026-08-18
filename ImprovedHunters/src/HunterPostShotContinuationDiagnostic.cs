using BepInEx.Logging;
using Iced.Intel;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace ImprovedHunters
{
    internal delegate bool TryValidateHunterPostShotContinuation(
        int hunterUnitId,
        int preyUnitId,
        uint preyGlobalId,
        eChimps preyType,
        long timestamp,
        out string validation);

    internal readonly struct HunterPostShotContinuationCandidate
    {
        public readonly int HunterUnitId;
        public readonly uint HunterGlobalId;
        public readonly int PreyUnitId;
        public readonly uint PreyGlobalId;
        public readonly eChimps PreyType;
        public readonly string AttackSource;

        public HunterPostShotContinuationCandidate(
            int hunterUnitId,
            uint hunterGlobalId,
            int preyUnitId,
            uint preyGlobalId,
            eChimps preyType,
            string attackSource)
        {
            HunterUnitId = hunterUnitId;
            HunterGlobalId = hunterGlobalId;
            PreyUnitId = preyUnitId;
            PreyGlobalId = preyGlobalId;
            PreyType = preyType;
            AttackSource = attackSource ?? "unknown";
        }

        public bool IsValid =>
            HunterUnitId > 0 &&
            HunterGlobalId != 0 &&
            PreyUnitId > 0 &&
            PreyGlobalId != 0;
    }

    /// <summary>
    /// Correlates an accepted Hunter attack with State 10's two audited target
    /// queries. If the same live, own-reserved and reachable prey survives, the
    /// query result only selects Vanilla's existing State-0 requery path. The
    /// regular State-0 fallback then hands the identity to Vanilla MoveHere.
    /// </summary>
    internal sealed unsafe class HunterPostShotContinuationDiagnostic : IDisposable
    {
        private const string ReferenceDllSha256 =
            "33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469";
        private const int HunterUpdateStartRva = 0x12FC20;
        private const int HunterUpdateEndRva = 0x1313D2;
        private const int HunterQueryFunctionRva = 0x18AF00;
        private const int StateSixWriterRva = 0x12FF58;
        private const int StateZeroContinuationRva = 0x12FF3E;
        private const int StateTenPrimaryQuerySequenceRva = 0x1304C6;
        private const int StateTenSecondaryQuerySequenceRva = 0x13056C;
        private const int StateNineCompletionWriterRva = 0x13023C;
        private const int FailedDirectAttackWriterRva = 0x130171;
        private const int RecoveryWriterHookLength = 0x17;
        // Relative-target resolution starts after the E8 opcode, while logs name the call itself.
        private const int QueryCallInstructionOffset = 0x0B;
        private const int QueryCallDisplacementOffset = 0x0C;
        private const int QueryReturnHookOffset = 0x10;
        private const int QueryResultHookLength = 0x0F;
        private const int HunterCurrentUnitIdRva = 0x92F2C4;
        private const int HunterAiStateOffset = 0x2BC;
        private const int HunterTargetUnitIdOffset = 0x39A;
        private const int HunterTargetGlobalIdOffset = 0x39C;
        private const int PreyCorpseFlagOffset = 0x29C;
        private const int PreyReservationOffset = 0x448;
        private const ushort StateZero = 0;
        private const ushort StateOne = 1;
        private const ushort StateNine = 9;
        private const ushort StateTen = 10;
        private const int MaximumRecoveryAttempts = 3;
        private const int MaxDiagnosticLogs = 160;

        private const string StateTenPrimaryQuerySequencePattern =
            "8B 1D ? ? ? ? 8B D3 49 8B CD E8 ? ? ? ? " +
            "85 C0 48 63 05 ? ? ? ? 0F 85 ? ? ? ? E9 ? ? ? ?";
        private const string StateTenSecondaryQuerySequencePattern =
            "8B 1D ? ? ? ? 8B D3 49 8B CD E8 ? ? ? ? " +
            "85 C0 48 63 05 ? ? ? ? 0F 84 ? ? ? ? E9 ? ? ? ?";
        private const string StateNineCompletionWriterPattern =
            "48 63 05 ? ? ? ? 48 69 C8 90 04 00 00 " +
            "66 42 89 B4 29 18 09 00 00";
        private const string FailedDirectAttackWriterPattern =
            "48 69 CA 90 04 00 00 41 BF 14 00 00 00 " +
            "B8 06 00 00 00 BE 01 00 00 00";

        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly Func<bool> canRun;
        private readonly TryValidateHunterPostShotContinuation tryValidateContinuation;
        private readonly Action<int, uint, long> registerRejectedMove;
        private readonly object stateLock = new object();
        private readonly Dictionary<int, ShotObservation> activeShots =
            new Dictionary<int, ShotObservation>();
        private readonly Dictionary<int, PendingStateZeroHandoff> pendingStateZeroHandoffs =
            new Dictionary<int, PendingStateZeroHandoff>();
        private readonly Dictionary<int, FailedAttackObservation> failedAttacks =
            new Dictionary<int, FailedAttackObservation>();
        private readonly Dictionary<int, RecoveryAttemptBudget> recoveryAttempts =
            new Dictionary<int, RecoveryAttemptBudget>();
        private HookTransaction transaction;
        private HookRef<X64InlineHook> primaryQueryResultHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> secondaryQueryResultHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> stateNineCompletionHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> failedDirectAttackWriterHook = new HookRef<X64InlineHook>();
        private int* currentHunterUnitId;
        private bool featureAvailable;
        private bool primaryHookConfirmed;
        private bool secondaryHookConfirmed;
        private bool stateNineHookConfirmed;
        private bool failedAttackHookConfirmed;
        private int diagnosticLogs;
        private bool disposed;

        public HunterPostShotContinuationDiagnostic(
            ManualLogSource log,
            ImprovedHuntersViewModel settings,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches,
            Func<bool> canRun,
            TryValidateHunterPostShotContinuation tryValidateContinuation,
            Action<int, uint, long> registerRejectedMove)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.canRun = canRun ?? throw new ArgumentNullException(nameof(canRun));
            this.tryValidateContinuation = tryValidateContinuation ??
                throw new ArgumentNullException(nameof(tryValidateContinuation));
            this.registerRejectedMove = registerRejectedMove ??
                throw new ArgumentNullException(nameof(registerRejectedMove));

            if (!referenceHashMatches)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Improved Hunters post-shot continuation unavailable: " +
                    $"DLL hash differs from audited SHA-256 {ReferenceDllSha256}; behavior remains unchanged.");
                return;
            }
            if (memory.Length == 0 || libraryBase == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            ValidatePatternByteCount(
                StateNineCompletionWriterPattern,
                RecoveryWriterHookLength,
                "State-9 completion");
            ValidatePatternByteCount(
                FailedDirectAttackWriterPattern,
                RecoveryWriterHookLength,
                "failed direct attack");

            int primarySequenceRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                StateTenPrimaryQuerySequencePattern,
                StateTenPrimaryQuerySequenceRva,
                referenceHashMatches,
                "Hunter State-10 primary post-shot target query",
                log).Rva;
            int secondarySequenceRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                StateTenSecondaryQuerySequencePattern,
                StateTenSecondaryQuerySequenceRva,
                referenceHashMatches,
                "Hunter State-10 secondary post-shot target query",
                log).Rva;
            int stateNineCompletionWriterRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                StateNineCompletionWriterPattern,
                StateNineCompletionWriterRva,
                referenceHashMatches,
                "Hunter State-9 completion State-10 writer",
                log).Rva;
            int failedDirectAttackWriterRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                FailedDirectAttackWriterPattern,
                FailedDirectAttackWriterRva,
                referenceHashMatches,
                "Hunter failed direct-attack State-6 writer",
                log).Rva;

            int primaryQueryFunctionRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                primarySequenceRva + QueryCallDisplacementOffset,
                primarySequenceRva + QueryReturnHookOffset);
            int secondaryQueryFunctionRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                secondarySequenceRva + QueryCallDisplacementOffset,
                secondarySequenceRva + QueryReturnHookOffset);
            if (primaryQueryFunctionRva != HunterQueryFunctionRva ||
                secondaryQueryFunctionRva != HunterQueryFunctionRva)
            {
                throw new InvalidOperationException(
                    $"Hunter State-10 query targets changed: primary=0x{primaryQueryFunctionRva:X}, " +
                    $"secondary=0x{secondaryQueryFunctionRva:X}.");
            }

            int primaryResultRva = checked(primarySequenceRva + QueryReturnHookOffset);
            int secondaryResultRva = checked(secondarySequenceRva + QueryReturnHookOffset);
            ulong primaryHunterIdAddress = ValidateQueryResultHookSpan(
                memory,
                libraryBase,
                primaryResultRva,
                Mnemonic.Jne,
                StateZeroContinuationRva,
                "primary");
            ulong secondaryHunterIdAddress = ValidateQueryResultHookSpan(
                memory,
                libraryBase,
                secondaryResultRva,
                Mnemonic.Je,
                StateSixWriterRva,
                "secondary");
            if (primaryHunterIdAddress != secondaryHunterIdAddress ||
                primaryHunterIdAddress != libraryBase + unchecked((ulong)HunterCurrentUnitIdRva))
            {
                throw new InvalidOperationException(
                    "Hunter State-10 query hooks no longer share the audited current-Hunter global.");
            }
            currentHunterUnitId = (int*)primaryHunterIdAddress;
            ulong stateNineHunterIdAddress = ValidateStateNineCompletionHookSpan(
                memory,
                libraryBase,
                stateNineCompletionWriterRva);
            ValidateFailedDirectAttackHookSpan(memory, libraryBase, failedDirectAttackWriterRva);
            if (stateNineHunterIdAddress != primaryHunterIdAddress)
                throw new InvalidOperationException("State-9 completion no longer uses the audited current-Hunter global.");
            ValidateHookSpansDoNotOverlap(
                primaryResultRva,
                secondaryResultRva,
                stateNineCompletionWriterRva,
                failedDirectAttackWriterRva);

            try
            {
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddContextHook(
                    ref primaryQueryResultHook,
                    libraryBase + unchecked((ulong)primaryResultRva),
                    ObservePrimaryStateTenQueryResult,
                    regs: X64SmartCPUContextRegs.Volatile,
                    hookSize: QueryResultHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref secondaryQueryResultHook,
                    libraryBase + unchecked((ulong)secondaryResultRva),
                    ObserveSecondaryStateTenQueryResult,
                    regs: X64SmartCPUContextRegs.Volatile,
                    hookSize: QueryResultHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref stateNineCompletionHook,
                    libraryBase + unchecked((ulong)stateNineCompletionWriterRva),
                    TrySkipStateTenSitTransition,
                    regs: X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RSI,
                    hookSize: RecoveryWriterHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref failedDirectAttackWriterHook,
                    libraryBase + unchecked((ulong)failedDirectAttackWriterRva),
                    TryRerouteFailedDirectAttack,
                    regs: X64SmartCPUContextRegs.Volatile |
                        X64SmartCPUContextRegs.RSI |
                        X64SmartCPUContextRegs.R15,
                    hookSize: RecoveryWriterHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.BeforeCallback);
                transaction.Commit();

                if (!primaryQueryResultHook.Success ||
                    !secondaryQueryResultHook.Success ||
                    !stateNineCompletionHook.Success ||
                    !failedDirectAttackWriterHook.Success)
                    throw new InvalidOperationException("One or more Hunter post-shot query hooks were not installed.");

                featureAvailable = true;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Improved Hunters post-shot continuation diagnostic initialized: " +
                    $"primaryQueryRva=0x{primarySequenceRva + QueryCallInstructionOffset:X}, " +
                    $"primaryResultHookSpan=[0x{primaryResultRva:X},0x{primaryResultRva + QueryResultHookLength:X}), " +
                    $"secondaryQueryRva=0x{secondarySequenceRva + QueryCallInstructionOffset:X}, " +
                    $"secondaryResultHookSpan=[0x{secondaryResultRva:X},0x{secondaryResultRva + QueryResultHookLength:X}), " +
                    $"stateNineCompletionHookSpan=[0x{stateNineCompletionWriterRva:X}," +
                    $"0x{stateNineCompletionWriterRva + RecoveryWriterHookLength:X}), " +
                    $"failedAttackHookSpan=[0x{failedDirectAttackWriterRva:X}," +
                    $"0x{failedDirectAttackWriterRva + RecoveryWriterHookLength:X}), " +
                    $"semanticPatternBytes={RecoveryWriterHookLength}/{RecoveryWriterHookLength}, " +
                    "firstAfterHookValidated=True, " +
                    $"queryFunctionRva=0x{HunterQueryFunctionRva:X}, stateSixWriterRva=0x{StateSixWriterRva:X}, " +
                    $"maximumRecoveryAttempts={MaximumRecoveryAttempts}, totalDurationLimit=None, " +
                    "ownReservationRequired=2-or-released-during-State0, liveIdentityRequired=True, PclZeroRejects=True, " +
                    "handoff=State10-query-result-to-Vanilla-state0-query-and-MoveHere, " +
                    "earlyHandoff=State9-completion-skips-State10-sit, failedAttackHandoff=State6-writer-to-State0, " +
                    "registerOverride=RAX/RSI/R15-only, ownMovement=False, ownAiState=False, " +
                    "ownOrderWrite=False, speedWrite=False, animationWrite=False.");
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
            primaryQueryResultHook.Success &&
            secondaryQueryResultHook.Success &&
            stateNineCompletionHook.Success &&
            failedDirectAttackWriterHook.Success;

        public void RecordAcceptedAttack(
            HunterPostShotContinuationCandidate candidate,
            long timestamp)
        {
            if (!IsAvailable || !canRun() || !candidate.IsValid)
                return;

            if (!TryValidateCandidate(
                    candidate,
                    StateOne,
                    allowReleasedStateZeroTransition: false,
                    out GameUnit* hunter,
                    out GameUnit* prey,
                    out string rejection))
            {
                LogDiagnostic(
                    "Improved Hunters post-shot observation rejected accepted attack: " +
                    $"hunter={candidate.HunterUnitId}/{candidate.HunterGlobalId}, " +
                    $"target={candidate.PreyUnitId}/{candidate.PreyGlobalId}/{candidate.PreyType}, " +
                    $"reason={rejection}, behaviorMutation=False.",
                    warning: true);
                return;
            }

            int recoveryAttempt = IncrementRecoveryAttempt(candidate);
            ShotObservation observation = new ShotObservation(candidate, recoveryAttempt);
            lock (stateLock)
            {
                activeShots[candidate.HunterUnitId] = observation;
                failedAttacks.Remove(candidate.HunterUnitId);
                pendingStateZeroHandoffs.Remove(candidate.HunterUnitId);
            }

            LogDiagnostic(
                "Improved Hunters post-shot observation queued: " +
                $"hunter={candidate.HunterUnitId}/{candidate.HunterGlobalId}, " +
                $"target={candidate.PreyUnitId}/{candidate.PreyGlobalId}/{candidate.PreyType}, " +
                $"attackSource={candidate.AttackSource}, targetHealth={prey->r_CurrentHealth}, " +
                $"recoveryAttempt={recoveryAttempt}/{MaximumRecoveryAttempts}, " +
                $"reservation={*(ushort*)((byte*)prey + PreyReservationOffset)}, corpseFlag=" +
                $"{*(ushort*)((byte*)prey + PreyCorpseFlagOffset)}, {TryFormatMovementSnapshot(hunter)}, " +
                "behaviorMutation=False.");
        }

        public void RecordFailedDirectAttack(
            HunterPostShotContinuationCandidate candidate,
            long timestamp)
        {
            if (!IsAvailable || !canRun() || !candidate.IsValid)
                return;

            if (!TryValidateCandidate(
                    candidate,
                    StateOne,
                    allowReleasedStateZeroTransition: false,
                    out _,
                    out _,
                    out string rejection))
            {
                LogDiagnostic(
                    "Improved Hunters failed direct-attack recovery was not staged: " +
                    $"hunter={candidate.HunterUnitId}/{candidate.HunterGlobalId}, " +
                    $"target={candidate.PreyUnitId}/{candidate.PreyGlobalId}/{candidate.PreyType}, " +
                    $"reason={rejection}, behaviorMutation=False.",
                    warning: true);
                return;
            }

            int recoveryAttempt = IncrementRecoveryAttempt(candidate);
            lock (stateLock)
            {
                failedAttacks[candidate.HunterUnitId] =
                    new FailedAttackObservation(candidate, recoveryAttempt, timestamp);
                activeShots.Remove(candidate.HunterUnitId);
                pendingStateZeroHandoffs.Remove(candidate.HunterUnitId);
            }

            LogDiagnostic(
                "Improved Hunters failed direct-attack recovery staged: " +
                $"hunter={candidate.HunterUnitId}/{candidate.HunterGlobalId}, " +
                $"target={candidate.PreyUnitId}/{candidate.PreyGlobalId}/{candidate.PreyType}, " +
                $"recoveryAttempt={recoveryAttempt}/{MaximumRecoveryAttempts}, " +
                "expectedVanillaWriter=state6, behaviorMutation=False.");
        }

        public void ResetAttemptBudgetForIndependentMove(
            int hunterUnitId,
            int preyUnitId,
            uint preyGlobalId)
        {
            if (hunterUnitId <= 0)
                return;

            RecoveryAttemptBudget previous = default;
            lock (stateLock)
            {
                recoveryAttempts.TryGetValue(hunterUnitId, out previous);
                recoveryAttempts.Remove(hunterUnitId);
                activeShots.Remove(hunterUnitId);
                failedAttacks.Remove(hunterUnitId);
                pendingStateZeroHandoffs.Remove(hunterUnitId);
            }

            if (previous.Attempts > 0)
            {
                LogDiagnostic(
                    "Improved Hunters recovery-attempt budget reset by independent Vanilla MoveHere: " +
                    $"hunter={hunterUnitId}, previousTarget={previous.PreyUnitId}/" +
                    $"{previous.PreyGlobalId}, newTarget={preyUnitId}/{preyGlobalId}, " +
                    $"previousAttempts={previous.Attempts}/{MaximumRecoveryAttempts}.");
            }
        }

        public void RecordProjectileSpawn(
            int hunterUnitId,
            uint hunterGlobalId,
            int targetUnitId,
            uint targetGlobalId,
            long projectileId,
            uint projectileGlobalId)
        {
            if (!IsAvailable || hunterUnitId <= 0 || targetUnitId <= 0)
                return;

            ShotObservation observation;
            lock (stateLock)
            {
                if (!activeShots.TryGetValue(hunterUnitId, out observation) ||
                    observation.Candidate.HunterGlobalId != hunterGlobalId ||
                    observation.Candidate.PreyUnitId != targetUnitId ||
                    observation.Candidate.PreyGlobalId != targetGlobalId)
                {
                    return;
                }

                observation = observation.WithProjectile(projectileId, projectileGlobalId);
                activeShots[hunterUnitId] = observation;
            }

            LogDiagnostic(
                "Improved Hunters post-shot projectile correlated: " +
                $"hunter={hunterUnitId}/{hunterGlobalId}, target={targetUnitId}/{targetGlobalId}, " +
                $"projectile={projectileId}/{projectileGlobalId}, behaviorMutation=False.");
        }

        public void RecordProjectileDelete(long projectileId)
        {
            if (!IsAvailable || projectileId <= 0)
                return;

            ShotObservation match = default;
            lock (stateLock)
            {
                foreach (KeyValuePair<int, ShotObservation> pair in activeShots)
                {
                    if (pair.Value.ProjectileId != projectileId)
                        continue;

                    match = pair.Value;
                    break;
                }
            }

            if (match.Candidate.IsValid)
            {
                LogDiagnostic(
                    "Improved Hunters post-shot projectile delete observed: " +
                    $"hunter={match.Candidate.HunterUnitId}/{match.Candidate.HunterGlobalId}, " +
                    $"target={match.Candidate.PreyUnitId}/{match.Candidate.PreyGlobalId}, " +
                    $"projectile={projectileId}/{match.ProjectileGlobalId}, behaviorMutation=False.");
            }
        }

        public bool TryPrepareStateZeroContinuation(
            int hunterUnitId,
            long timestamp,
            out HunterPostShotContinuationCandidate candidate)
        {
            candidate = default;
            if (!IsAvailable || !canRun() || hunterUnitId <= 0)
                return false;

            PendingStateZeroHandoff handoff;
            lock (stateLock)
            {
                if (!pendingStateZeroHandoffs.TryGetValue(hunterUnitId, out handoff))
                    return false;
            }

            GameUnit* hunter = null;
            GameUnit* prey = null;
            string validation = null;
            if (!TryValidateCandidate(
                    handoff.Candidate,
                    StateZero,
                    allowReleasedStateZeroTransition: true,
                    out hunter,
                    out prey,
                    out string identityValidation) ||
                !tryValidateContinuation(
                    handoff.Candidate.HunterUnitId,
                    handoff.Candidate.PreyUnitId,
                    handoff.Candidate.PreyGlobalId,
                    handoff.Candidate.PreyType,
                    timestamp,
                    out validation))
            {
                lock (stateLock)
                    pendingStateZeroHandoffs.Remove(hunterUnitId);
                LogDiagnostic(
                    "Improved Hunters post-attack State-0 handoff failed revalidation: " +
                    $"hunter={hunterUnitId}, target={handoff.Candidate.PreyUnitId}/" +
                    $"{handoff.Candidate.PreyGlobalId}, identityValidation={identityValidation}, " +
                    $"runtimeValidation={validation ?? "not-run"}, " +
                    "behaviorMutation=False.",
                    warning: true);
                return false;
            }

            candidate = handoff.Candidate;
            LogDiagnostic(
                "Improved Hunters post-shot State-0 continuation prepared: " +
                $"hunter={candidate.HunterUnitId}/{candidate.HunterGlobalId}, " +
                $"target={candidate.PreyUnitId}/{candidate.PreyGlobalId}/{candidate.PreyType}, " +
                $"targetHealth={prey->r_CurrentHealth}, validation={validation}, " +
                $"{TryFormatMovementSnapshot(hunter)}, behaviorMutation=False.");
            return true;
        }

        public void RecordStateZeroHandoff(
            HunterPostShotContinuationCandidate candidate,
            int vanillaTargetUnitId)
        {
            lock (stateLock)
                pendingStateZeroHandoffs.Remove(candidate.HunterUnitId);

            LogDiagnostic(
                "Improved Hunters post-shot target supplied to Vanilla State-0 handoff: " +
                $"hunter={candidate.HunterUnitId}/{candidate.HunterGlobalId}, " +
                $"target={candidate.PreyUnitId}/{candidate.PreyGlobalId}/{candidate.PreyType}, " +
                $"vanillaTarget={vanillaTargetUnitId}, next=Vanilla-MoveHere, " +
                "registerOverride=RAX-query-result-only, ownMovement=False, ownAiState=False, ownOrderWrite=False.");
        }

        public void RecordMoveHereResult(
            HunterPostShotContinuationCandidate candidate,
            int moveHereResult)
        {
            lock (stateLock)
            {
                activeShots.Remove(candidate.HunterUnitId);
                failedAttacks.Remove(candidate.HunterUnitId);
                pendingStateZeroHandoffs.Remove(candidate.HunterUnitId);
            }

            LogDiagnostic(
                "Improved Hunters post-shot Vanilla MoveHere result: " +
                $"hunter={candidate.HunterUnitId}/{candidate.HunterGlobalId}, " +
                $"target={candidate.PreyUnitId}/{candidate.PreyGlobalId}/{candidate.PreyType}, " +
                $"moveHereResult={moveHereResult}, followup=" +
                $"{(moveHereResult != 0 ? "Vanilla-state1" : "Vanilla-rejection-cleanup")}, " +
                "ownMovement=False, ownAiState=False, ownOrderWrite=False.",
                warning: moveHereResult == 0);
        }

        public void ResetForMap()
        {
            lock (stateLock)
            {
                activeShots.Clear();
                failedAttacks.Clear();
                recoveryAttempts.Clear();
                pendingStateZeroHandoffs.Clear();
            }

            diagnosticLogs = 0;
            primaryHookConfirmed = false;
            secondaryHookConfirmed = false;
            stateNineHookConfirmed = false;
            failedAttackHookConfirmed = false;
        }

        private void ObservePrimaryStateTenQueryResult(NativePointer<X64SmartCPUContext> context)
        {
            ObserveStateTenQueryResult(context, "primary-visibility-or-target-guard", ref primaryHookConfirmed);
        }

        private void ObserveSecondaryStateTenQueryResult(NativePointer<X64SmartCPUContext> context)
        {
            ObserveStateTenQueryResult(context, "secondary-target-refresh", ref secondaryHookConfirmed);
        }

        private void TrySkipStateTenSitTransition(NativePointer<X64SmartCPUContext> context)
        {
            if (!IsAvailable || !canRun() || currentHunterUnitId == null)
                return;

            int hunterUnitId = *currentHunterUnitId;
            if (!stateNineHookConfirmed)
            {
                stateNineHookConfirmed = true;
                LogDiagnostic(
                    "Improved Hunters State-9 completion hook confirmed: " +
                    $"hunter={hunterUnitId}, requestedState={(ushort)context.Pointer->RSI}, " +
                    "transition=shot-to-State10-sit, behaviorMutation=False.");
            }

            ShotObservation observation;
            lock (stateLock)
            {
                if (!activeShots.TryGetValue(hunterUnitId, out observation))
                    return;
            }

            long timestamp = Stopwatch.GetTimestamp();
            HunterPostShotContinuationCandidate candidate = observation.Candidate;
            if (observation.RecoveryAttempt >= MaximumRecoveryAttempts)
            {
                ExhaustRecoveryBudget(candidate, timestamp);
                LogDiagnostic(
                    "Improved Hunters allowed Vanilla State-10 sit transition after shot: " +
                    $"hunter={hunterUnitId}, target={candidate.PreyUnitId}/{candidate.PreyGlobalId}, " +
                    $"recoveryAttempt={observation.RecoveryAttempt}/{MaximumRecoveryAttempts}, " +
                    "reason=recovery-attempt-budget-exhausted, behaviorMutation=False.",
                    warning: true);
                return;
            }

            if (!TryValidateRecovery(
                    candidate,
                    StateNine,
                    allowReleasedStateZeroTransition: false,
                    timestamp,
                    out GameUnit* hunter,
                    out GameUnit* prey,
                    out string validation))
            {
                lock (stateLock)
                    activeShots.Remove(hunterUnitId);
                LogDiagnostic(
                    "Improved Hunters allowed Vanilla State-10 sit transition after shot: " +
                    $"hunter={hunterUnitId}, target={candidate.PreyUnitId}/{candidate.PreyGlobalId}, " +
                    $"reason={validation}, recoveryAttempt={observation.RecoveryAttempt}/" +
                    $"{MaximumRecoveryAttempts}, behaviorMutation=False.",
                    warning: true);
                return;
            }

            lock (stateLock)
            {
                activeShots.Remove(hunterUnitId);
                pendingStateZeroHandoffs[hunterUnitId] =
                    new PendingStateZeroHandoff(candidate);
            }

            // ESI is Vanilla's pending AI-state value. The relocated writer
            // remains native and writes State 0 instead of State 10.
            context.Pointer->RSI = StateZero;
            LogDiagnostic(
                "Improved Hunters skipped post-shot State-10 sit transition: " +
                $"hunter={candidate.HunterUnitId}/{candidate.HunterGlobalId}, " +
                $"target={candidate.PreyUnitId}/{candidate.PreyGlobalId}/{candidate.PreyType}, " +
                $"recoveryAttempt={observation.RecoveryAttempt}/{MaximumRecoveryAttempts}, " +
                $"targetHealth={prey->r_CurrentHealth}, validation={validation}, " +
                $"{TryFormatMovementSnapshot(hunter)}, transition=State9-to-State0, " +
                "State10SitPrevented=True, projectileEndWait=False, registerOverride=RSI-state-only, " +
                "behaviorMutation=True.");
        }

        private void TryRerouteFailedDirectAttack(NativePointer<X64SmartCPUContext> context)
        {
            if (!IsAvailable || !canRun() || currentHunterUnitId == null)
                return;

            int hunterUnitId = *currentHunterUnitId;
            if (!failedAttackHookConfirmed)
            {
                failedAttackHookConfirmed = true;
                LogDiagnostic(
                    "Improved Hunters failed direct-attack writer hook confirmed: " +
                    $"hunter={hunterUnitId}, writerInputs=state{(ushort)context.Pointer->RAX}/" +
                    $"timer{(ushort)context.Pointer->R15}/control{(ushort)context.Pointer->RSI}, " +
                    "behaviorMutation=False.");
            }

            FailedAttackObservation observation;
            lock (stateLock)
            {
                if (!failedAttacks.TryGetValue(hunterUnitId, out observation))
                    return;
                failedAttacks.Remove(hunterUnitId);
            }

            HunterPostShotContinuationCandidate candidate = observation.Candidate;
            if (observation.RecoveryAttempt >= MaximumRecoveryAttempts)
            {
                ExhaustRecoveryBudget(candidate, observation.ObservedAt);
                LogDiagnostic(
                    "Improved Hunters allowed Vanilla State-6 abandonment after failed direct attack: " +
                    $"hunter={hunterUnitId}, target={candidate.PreyUnitId}/{candidate.PreyGlobalId}, " +
                    $"recoveryAttempt={observation.RecoveryAttempt}/{MaximumRecoveryAttempts}, " +
                    "reason=recovery-attempt-budget-exhausted, behaviorMutation=False.",
                    warning: true);
                return;
            }

            if (!TryValidateRecovery(
                    candidate,
                    StateOne,
                    allowReleasedStateZeroTransition: false,
                    observation.ObservedAt,
                    out GameUnit* hunter,
                    out GameUnit* prey,
                    out string validation))
            {
                LogDiagnostic(
                    "Improved Hunters allowed Vanilla State-6 abandonment after failed direct attack: " +
                    $"hunter={hunterUnitId}, target={candidate.PreyUnitId}/{candidate.PreyGlobalId}, " +
                    $"reason={validation}, recoveryAttempt={observation.RecoveryAttempt}/" +
                    $"{MaximumRecoveryAttempts}, behaviorMutation=False.",
                    warning: true);
                return;
            }

            lock (stateLock)
            {
                pendingStateZeroHandoffs[hunterUnitId] =
                    new PendingStateZeroHandoff(candidate);
            }

            // These are exactly the three values prepared by Vanilla before
            // its timer/state/control writers; the relocated writers stay native.
            context.Pointer->RAX = StateZero;
            context.Pointer->R15 = 0;
            context.Pointer->RSI = 0;
            LogDiagnostic(
                "Improved Hunters rerouted failed direct attack from State 6 to State 0: " +
                $"hunter={candidate.HunterUnitId}/{candidate.HunterGlobalId}, " +
                $"target={candidate.PreyUnitId}/{candidate.PreyGlobalId}/{candidate.PreyType}, " +
                $"recoveryAttempt={observation.RecoveryAttempt}/{MaximumRecoveryAttempts}, " +
                $"targetHealth={prey->r_CurrentHealth}, validation={validation}, " +
                $"{TryFormatMovementSnapshot(hunter)}, writerInputs=6/20/1->0/0/0, " +
                "next=Vanilla-State0-requery, behaviorMutation=True.");
        }

        private void ObserveStateTenQueryResult(
            NativePointer<X64SmartCPUContext> context,
            string queryPath,
            ref bool hookConfirmed)
        {
            if (!IsAvailable || !canRun() || currentHunterUnitId == null)
                return;

            int hunterUnitId = *currentHunterUnitId;
            int vanillaTargetUnitId = unchecked((int)(uint)context.Pointer->RAX);
            if (!hookConfirmed)
            {
                hookConfirmed = true;
                LogDiagnostic(
                    "Improved Hunters post-shot State-10 query hook confirmed: " +
                    $"path={queryPath}, hunter={hunterUnitId}, vanillaTarget={vanillaTargetUnitId}, " +
                    "behaviorMutation=False.");
            }

            long timestamp = Stopwatch.GetTimestamp();
            ShotObservation observation;
            lock (stateLock)
            {
                if (!activeShots.TryGetValue(hunterUnitId, out observation))
                    return;
            }

            HunterPostShotContinuationCandidate candidate = observation.Candidate;
            string rejection = null;
            GameUnit* hunter = null;
            GameUnit* prey = null;
            string validation = null;
            if (observation.RecoveryAttempt >= MaximumRecoveryAttempts)
                rejection = "recovery-attempt-budget-exhausted";
            else if (!TryValidateCandidate(
                    candidate,
                    StateTen,
                    allowReleasedStateZeroTransition: false,
                    out hunter,
                    out prey,
                    out string identityValidation))
                rejection = identityValidation;
            else if (!tryValidateContinuation(
                candidate.HunterUnitId,
                candidate.PreyUnitId,
                candidate.PreyGlobalId,
                candidate.PreyType,
                timestamp,
                out validation))
            {
                rejection = validation ?? "runtime-policy-rejected";
            }

            if (rejection != null)
            {
                lock (stateLock)
                {
                    activeShots.Remove(hunterUnitId);
                    pendingStateZeroHandoffs.Remove(hunterUnitId);
                }
                LogDiagnostic(
                    "Improved Hunters post-shot continuation left Vanilla unchanged: " +
                    $"path={queryPath}, hunter={hunterUnitId}, target={candidate.PreyUnitId}/" +
                    $"{candidate.PreyGlobalId}/{candidate.PreyType}, vanillaTarget={vanillaTargetUnitId}, " +
                    $"reason={rejection}, zeroResultFollowup=" +
                    $"{(vanillaTargetUnitId == 0 ? $"state6-writer-0x{StateSixWriterRva:X}" : "Vanilla-state0")}, " +
                    "behaviorMutation=False.",
                    warning: true);
                return;
            }

            PendingStateZeroHandoff handoff = new PendingStateZeroHandoff(candidate);
            lock (stateLock)
                pendingStateZeroHandoffs[hunterUnitId] = handoff;

            // The relocated TEST consumes only this result. Its nonzero branch
            // writes State 0; target, reservation and path remain Vanilla-owned.
            context.Pointer->RAX = unchecked((ulong)(uint)candidate.PreyUnitId);
            LogDiagnostic(
                "Improved Hunters post-shot continuation selected Vanilla State-0 recovery: " +
                $"path={queryPath}, hunter={candidate.HunterUnitId}/{candidate.HunterGlobalId}, " +
                $"target={candidate.PreyUnitId}/{candidate.PreyGlobalId}/{candidate.PreyType}, " +
                $"vanillaTarget={vanillaTargetUnitId}, targetHealth={prey->r_CurrentHealth}, " +
                $"reservation={*(ushort*)((byte*)prey + PreyReservationOffset)}, " +
                $"corpseFlag={*(ushort*)((byte*)prey + PreyCorpseFlagOffset)}, " +
                $"projectile={observation.ProjectileId}/{observation.ProjectileGlobalId}, " +
                $"validation={validation}, {TryFormatMovementSnapshot(hunter)}, " +
                $"preventedStateSixWriter=0x{StateSixWriterRva:X}, " +
                "registerOverride=RAX-query-result-only, behaviorMutation=True.");
        }

        private bool TryValidateCandidate(
            HunterPostShotContinuationCandidate candidate,
            ushort requiredHunterState,
            bool allowReleasedStateZeroTransition,
            out GameUnit* hunter,
            out GameUnit* prey,
            out string validation)
        {
            hunter = null;
            prey = null;
            validation = "unknown";
            if (!candidate.IsValid)
                return Reject("invalid-candidate", out validation);
            if (!settings.IsHuntingEnabled(candidate.PreyType))
                return Reject("prey-type-disabled", out validation);
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(candidate.HunterUnitId, out hunter) ||
                hunter == null)
                return Reject("hunter-not-found", out validation);
            if (hunter->r_AliveState != AliveState.IsAlive || hunter->r_CurrentHealth == 0)
                return Reject("hunter-not-live", out validation);
            if (hunter->r_GlobalId != candidate.HunterGlobalId ||
                hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER)
                return Reject("hunter-identity-changed", out validation);

            ushort actualState = *(ushort*)((byte*)hunter + HunterAiStateOffset);
            if (actualState != requiredHunterState)
                return Reject($"hunter-state-{actualState}-expected-{requiredHunterState}", out validation);
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(candidate.PreyUnitId, out prey) || prey == null)
                return Reject("prey-not-found", out validation);
            if (prey->r_AliveState != AliveState.IsAlive || prey->r_CurrentHealth == 0)
                return Reject("prey-not-live", out validation);
            if (prey->r_GlobalId != candidate.PreyGlobalId || prey->r_UnitChimp != candidate.PreyType)
                return Reject("prey-identity-changed", out validation);

            byte* hunterBytes = (byte*)hunter;
            byte* preyBytes = (byte*)prey;
            ushort targetUnitId = *(ushort*)(hunterBytes + HunterTargetUnitIdOffset);
            uint targetGlobalId = *(uint*)(hunterBytes + HunterTargetGlobalIdOffset);
            bool targetMatches =
                targetUnitId == candidate.PreyUnitId && targetGlobalId == candidate.PreyGlobalId;
            bool targetWasCleared = targetUnitId == 0 && targetGlobalId == 0;
            if (!targetMatches && !(allowReleasedStateZeroTransition && targetWasCleared))
                return Reject($"hunter-target-{targetUnitId}-{targetGlobalId}", out validation);
            if (*(ushort*)(preyBytes + PreyCorpseFlagOffset) != 0)
                return Reject("prey-corpse-flag-set", out validation);

            ushort reservation = *(ushort*)(preyBytes + PreyReservationOffset);
            if (reservation != 2 && !(allowReleasedStateZeroTransition && reservation == 0))
                return Reject($"prey-reservation-{reservation}", out validation);
            if (IsTargetedByOtherLiveHunter(candidate))
                return Reject("prey-targeted-by-other-hunter", out validation);

            validation = targetMatches && reservation == 2
                ? "identity-live-own-reservation"
                : $"identity-live-State0-transition-target-{targetUnitId}-{targetGlobalId}-reservation-{reservation}";
            return true;
        }

        private bool TryValidateRecovery(
            HunterPostShotContinuationCandidate candidate,
            ushort requiredHunterState,
            bool allowReleasedStateZeroTransition,
            long timestamp,
            out GameUnit* hunter,
            out GameUnit* prey,
            out string validation)
        {
            if (!TryValidateCandidate(
                    candidate,
                    requiredHunterState,
                    allowReleasedStateZeroTransition,
                    out hunter,
                    out prey,
                    out string identityValidation))
            {
                validation = identityValidation;
                return false;
            }

            if (!tryValidateContinuation(
                    candidate.HunterUnitId,
                    candidate.PreyUnitId,
                    candidate.PreyGlobalId,
                    candidate.PreyType,
                    timestamp,
                    out string runtimeValidation))
            {
                validation = runtimeValidation ?? "runtime-policy-rejected";
                return false;
            }

            validation = $"{identityValidation}+{runtimeValidation}";
            return true;
        }

        private int IncrementRecoveryAttempt(HunterPostShotContinuationCandidate candidate)
        {
            lock (stateLock)
            {
                int attempts = 1;
                if (recoveryAttempts.TryGetValue(candidate.HunterUnitId, out RecoveryAttemptBudget current) &&
                    current.PreyUnitId == candidate.PreyUnitId &&
                    current.PreyGlobalId == candidate.PreyGlobalId)
                {
                    attempts = current.Attempts + 1;
                }

                recoveryAttempts[candidate.HunterUnitId] = new RecoveryAttemptBudget(
                    candidate.PreyUnitId,
                    candidate.PreyGlobalId,
                    attempts);
                return attempts;
            }
        }

        private void ExhaustRecoveryBudget(
            HunterPostShotContinuationCandidate candidate,
            long timestamp)
        {
            lock (stateLock)
            {
                activeShots.Remove(candidate.HunterUnitId);
                failedAttacks.Remove(candidate.HunterUnitId);
                pendingStateZeroHandoffs.Remove(candidate.HunterUnitId);
            }

            registerRejectedMove(candidate.HunterUnitId, candidate.PreyGlobalId, timestamp);
        }

        private static bool Reject(string reason, out string validation)
        {
            validation = reason;
            return false;
        }

        private static bool IsTargetedByOtherLiveHunter(
            HunterPostShotContinuationCandidate candidate)
        {
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            if (units._array == null || units.Length == 0)
                return false;

            for (int index = 0; index < units.Length; index++)
            {
                int unitId = index + 1;
                if (unitId == candidate.HunterUnitId)
                    continue;

                GameUnit* otherHunter = units.GetValuePointer(index);
                if (otherHunter->r_AliveState != AliveState.IsAlive ||
                    otherHunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER)
                {
                    continue;
                }

                byte* hunterBytes = (byte*)otherHunter;
                if (*(ushort*)(hunterBytes + HunterTargetUnitIdOffset) == candidate.PreyUnitId &&
                    *(uint*)(hunterBytes + HunterTargetGlobalIdOffset) == candidate.PreyGlobalId)
                {
                    return true;
                }
            }

            return false;
        }

        private static ulong ValidateStateNineCompletionHookSpan(
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            int hookRva)
        {
            const int decodeLookahead = 40;
            if (hookRva < 0 || hookRva > memory.Length - decodeLookahead)
                throw new InvalidOperationException("State-9 completion hook lies outside the module image.");

            ulong hookAddress = libraryBase + unchecked((ulong)hookRva);
            Decoder decoder = Decoder.Create(
                64,
                new ByteArrayCodeReader(memory.Slice(hookRva, decodeLookahead).ToArray()),
                hookAddress);
            Instruction hunterLoad = decoder.Decode();
            Instruction unitOffset = decoder.Decode();
            Instruction stateWriter = decoder.Decode();
            int decodedLength = checked((int)(decoder.IP - hookAddress));
            Instruction firstAfterHook = decoder.Decode();
            if (hunterLoad.IsInvalid ||
                unitOffset.IsInvalid ||
                stateWriter.IsInvalid ||
                firstAfterHook.IsInvalid ||
                hunterLoad.Mnemonic != Mnemonic.Movsxd || hunterLoad.Length != 7 ||
                hunterLoad.Op0Register != Register.RAX ||
                unitOffset.Mnemonic != Mnemonic.Imul || unitOffset.Length != 7 ||
                unitOffset.Op0Register != Register.RCX ||
                unitOffset.Op1Register != Register.RAX ||
                unitOffset.Op2Kind != OpKind.Immediate32to64 || unitOffset.Immediate32 != 0x490 ||
                stateWriter.Mnemonic != Mnemonic.Mov || stateWriter.Length != 9 ||
                stateWriter.Op0Kind != OpKind.Memory ||
                stateWriter.Op1Register != Register.SI ||
                stateWriter.MemoryBase != Register.RCX ||
                stateWriter.MemoryIndex != Register.R13 ||
                stateWriter.MemoryDisplacement64 != 0x918 ||
                decodedLength != RecoveryWriterHookLength ||
                firstAfterHook.IP != hookAddress + RecoveryWriterHookLength ||
                firstAfterHook.Mnemonic != Mnemonic.Mov || firstAfterHook.Length != 8 ||
                firstAfterHook.Op0Kind != OpKind.Memory ||
                firstAfterHook.Op1Register != Register.EBP ||
                firstAfterHook.MemoryBase != Register.RCX ||
                firstAfterHook.MemoryIndex != Register.R13 ||
                firstAfterHook.MemoryDisplacement64 != 0x908 ||
                !hunterLoad.IsIPRelativeMemoryOperand ||
                hunterLoad.IPRelativeMemoryAddress !=
                    libraryBase + unchecked((ulong)HunterCurrentUnitIdRva))
            {
                throw new InvalidOperationException(
                    "State-9 completion hook does not decode as the audited 7+7+9-byte span.");
            }

            ValidateNoExternalDirectBranchTargetsInsideHook(
                memory,
                libraryBase,
                hookAddress,
                hookAddress + RecoveryWriterHookLength,
                "State-9-completion");
            return hunterLoad.IPRelativeMemoryAddress;
        }

        private static void ValidatePatternByteCount(
            string pattern,
            int expectedBytes,
            string name)
        {
            int actualBytes = pattern.Split(
                new[] { ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries).Length;
            if (actualBytes != expectedBytes)
            {
                throw new InvalidOperationException(
                    $"Hunter {name} pattern length changed: expected={expectedBytes}, actual={actualBytes}.");
            }
        }

        private static void ValidateHookSpansDoNotOverlap(
            int primaryResultRva,
            int secondaryResultRva,
            int stateNineCompletionWriterRva,
            int failedDirectAttackWriterRva)
        {
            (int Start, int End, string Name)[] spans =
            {
                (primaryResultRva, primaryResultRva + QueryResultHookLength, "primary-State10"),
                (secondaryResultRva, secondaryResultRva + QueryResultHookLength, "secondary-State10"),
                (stateNineCompletionWriterRva,
                    stateNineCompletionWriterRva + RecoveryWriterHookLength,
                    "State9-completion"),
                (failedDirectAttackWriterRva,
                    failedDirectAttackWriterRva + RecoveryWriterHookLength,
                    "failed-direct-attack")
            };

            for (int left = 0; left < spans.Length; left++)
            {
                for (int right = left + 1; right < spans.Length; right++)
                {
                    if (spans[left].Start < spans[right].End &&
                        spans[right].Start < spans[left].End)
                    {
                        throw new InvalidOperationException(
                            $"Hunter recovery hook spans overlap: {spans[left].Name} and {spans[right].Name}.");
                    }
                }
            }
        }

        private static void ValidateFailedDirectAttackHookSpan(
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            int hookRva)
        {
            const int decodeLookahead = 40;
            if (hookRva < 0 || hookRva > memory.Length - decodeLookahead)
                throw new InvalidOperationException("Failed direct-attack writer hook lies outside the module image.");

            ulong hookAddress = libraryBase + unchecked((ulong)hookRva);
            Decoder decoder = Decoder.Create(
                64,
                new ByteArrayCodeReader(memory.Slice(hookRva, decodeLookahead).ToArray()),
                hookAddress);
            Instruction unitOffset = decoder.Decode();
            Instruction timerValue = decoder.Decode();
            Instruction stateValue = decoder.Decode();
            Instruction controlValue = decoder.Decode();
            int decodedLength = checked((int)(decoder.IP - hookAddress));
            Instruction firstAfterHook = decoder.Decode();
            if (unitOffset.IsInvalid ||
                timerValue.IsInvalid ||
                stateValue.IsInvalid ||
                controlValue.IsInvalid ||
                firstAfterHook.IsInvalid ||
                unitOffset.Mnemonic != Mnemonic.Imul || unitOffset.Length != 7 ||
                unitOffset.Op0Register != Register.RCX ||
                unitOffset.Op1Register != Register.RDX ||
                unitOffset.Op2Kind != OpKind.Immediate32to64 || unitOffset.Immediate32 != 0x490 ||
                timerValue.Mnemonic != Mnemonic.Mov || timerValue.Length != 6 ||
                timerValue.Op0Register != Register.R15D ||
                timerValue.Op1Kind != OpKind.Immediate32 || timerValue.Immediate32 != 20 ||
                stateValue.Mnemonic != Mnemonic.Mov || stateValue.Length != 5 ||
                stateValue.Op0Register != Register.EAX ||
                stateValue.Op1Kind != OpKind.Immediate32 || stateValue.Immediate32 != 6 ||
                controlValue.Mnemonic != Mnemonic.Mov || controlValue.Length != 5 ||
                controlValue.Op0Register != Register.ESI ||
                controlValue.Op1Kind != OpKind.Immediate32 || controlValue.Immediate32 != 1 ||
                decodedLength != RecoveryWriterHookLength ||
                firstAfterHook.IP != hookAddress + RecoveryWriterHookLength ||
                firstAfterHook.Mnemonic != Mnemonic.Mov || firstAfterHook.Length != 9 ||
                firstAfterHook.Op0Kind != OpKind.Memory ||
                firstAfterHook.Op1Register != Register.R15W ||
                firstAfterHook.MemoryBase != Register.RCX ||
                firstAfterHook.MemoryIndex != Register.R13 ||
                firstAfterHook.MemoryDisplacement64 != 0x920)
            {
                throw new InvalidOperationException(
                    "Failed direct-attack hook does not decode as the audited 7+6+5+5-byte span.");
            }

            ValidateNoExternalDirectBranchTargetsInsideHook(
                memory,
                libraryBase,
                hookAddress,
                hookAddress + RecoveryWriterHookLength,
                "failed-direct-attack");
        }

        private static ulong ValidateQueryResultHookSpan(
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            int hookRva,
            Mnemonic expectedBranchMnemonic,
            int expectedBranchTargetRva,
            string name)
        {
            const int decodeLookahead = 32;
            if (hookRva < 0 || hookRva > memory.Length - decodeLookahead)
                throw new InvalidOperationException($"State-10 {name} query-result hook lies outside the module image.");

            ulong hookAddress = libraryBase + unchecked((ulong)hookRva);
            Decoder decoder = Decoder.Create(
                64,
                new ByteArrayCodeReader(memory.Slice(hookRva, decodeLookahead).ToArray()),
                hookAddress);
            Instruction test = decoder.Decode();
            Instruction hunterLoad = decoder.Decode();
            Instruction branch = decoder.Decode();
            int decodedHookLength = checked((int)(decoder.IP - hookAddress));
            if (test.IsInvalid ||
                hunterLoad.IsInvalid ||
                branch.IsInvalid ||
                test.Mnemonic != Mnemonic.Test ||
                test.Op0Register != Register.EAX ||
                test.Op1Register != Register.EAX ||
                test.Length != 2 ||
                hunterLoad.Mnemonic != Mnemonic.Movsxd ||
                hunterLoad.Length != 7 ||
                branch.Mnemonic != expectedBranchMnemonic ||
                branch.FlowControl != FlowControl.ConditionalBranch ||
                branch.Length != 6 ||
                decodedHookLength != QueryResultHookLength)
            {
                throw new InvalidOperationException(
                    $"State-10 {name} query-result hook does not decode as the audited 2+7+6-byte span.");
            }

            ulong hookEndAddress = hookAddress + QueryResultHookLength;
            ulong expectedBranchTarget = libraryBase + unchecked((ulong)expectedBranchTargetRva);
            if (!hunterLoad.IsIPRelativeMemoryOperand ||
                hunterLoad.IPRelativeMemoryAddress !=
                    libraryBase + unchecked((ulong)HunterCurrentUnitIdRva) ||
                branch.NearBranchTarget != expectedBranchTarget ||
                (branch.NearBranchTarget > hookAddress && branch.NearBranchTarget < hookEndAddress))
            {
                throw new InvalidOperationException(
                    $"State-10 {name} query-result operands changed: branchTarget=0x{branch.NearBranchTarget:X}, " +
                    $"hookSpan=[0x{hookAddress:X},0x{hookEndAddress:X}).");
            }

            ValidateNoExternalDirectBranchTargetsInsideHook(
                memory,
                libraryBase,
                hookAddress,
                hookEndAddress,
                name);
            return hunterLoad.IPRelativeMemoryAddress;
        }

        private static void ValidateNoExternalDirectBranchTargetsInsideHook(
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            ulong hookAddress,
            ulong hookEndAddress,
            string name)
        {
            int functionLength = HunterUpdateEndRva - HunterUpdateStartRva;
            if (functionLength <= 0 || HunterUpdateStartRva > memory.Length - functionLength)
                throw new InvalidOperationException("HunterUpdate audit range lies outside the module image.");

            ulong functionAddress = libraryBase + unchecked((ulong)HunterUpdateStartRva);
            ulong functionEndAddress = libraryBase + unchecked((ulong)HunterUpdateEndRva);
            Decoder decoder = Decoder.Create(
                64,
                new ByteArrayCodeReader(memory.Slice(HunterUpdateStartRva, functionLength).ToArray()),
                functionAddress);
            while (decoder.IP < functionEndAddress)
            {
                Instruction instruction = decoder.Decode();
                if (instruction.IsInvalid || decoder.LastError != DecoderError.None)
                {
                    throw new InvalidOperationException(
                        $"HunterUpdate State-10 branch audit failed to decode RVA 0x" +
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
                bool sourceOutsideHook = instruction.IP < hookAddress || instruction.IP >= hookEndAddress;
                if (sourceOutsideHook && target > hookAddress && target < hookEndAddress)
                {
                    throw new InvalidOperationException(
                        $"Unsafe inbound branch into State-10 {name} query-result hook span: " +
                        $"sourceRva=0x{instruction.IP - libraryBase:X}, " +
                        $"targetRva=0x{target - libraryBase:X}, " +
                        $"span=[0x{hookAddress - libraryBase:X},0x{hookEndAddress - libraryBase:X}).");
                }
            }

            if (decoder.IP != functionEndAddress)
            {
                throw new InvalidOperationException(
                    $"HunterUpdate State-10 branch audit ended at unexpected RVA 0x" +
                    $"{decoder.IP - libraryBase:X}.");
            }
        }

        private void LogDiagnostic(string message, bool warning = false)
        {
            if (diagnosticLogs >= MaxDiagnosticLogs)
                return;

            diagnosticLogs++;
            string countedMessage = $"{message} ({diagnosticLogs}/{MaxDiagnosticLogs}).";
            if (warning)
                Shared.DebugLogHelper.LogWarning(log, countedMessage);
            else
                Shared.DebugLogHelper.LogInfo(log, countedMessage);
        }

        private static string TryFormatMovementSnapshot(GameUnit* hunter)
        {
            try
            {
                return hunter == null ? "snapshot=unavailable" : HunterMovementSnapshot.TryFormat(hunter);
            }
            catch
            {
                return "snapshot=failed";
            }
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
            lock (stateLock)
            {
                activeShots.Clear();
                failedAttacks.Clear();
                recoveryAttempts.Clear();
                pendingStateZeroHandoffs.Clear();
            }
        }

        private readonly struct ShotObservation
        {
            public readonly HunterPostShotContinuationCandidate Candidate;
            public readonly int RecoveryAttempt;
            public readonly long ProjectileId;
            public readonly uint ProjectileGlobalId;

            public ShotObservation(
                HunterPostShotContinuationCandidate candidate,
                int recoveryAttempt,
                long projectileId = 0,
                uint projectileGlobalId = 0)
            {
                Candidate = candidate;
                RecoveryAttempt = recoveryAttempt;
                ProjectileId = projectileId;
                ProjectileGlobalId = projectileGlobalId;
            }

            public ShotObservation WithProjectile(long projectileId, uint projectileGlobalId) =>
                new ShotObservation(Candidate, RecoveryAttempt, projectileId, projectileGlobalId);
        }

        private readonly struct PendingStateZeroHandoff
        {
            public readonly HunterPostShotContinuationCandidate Candidate;

            public PendingStateZeroHandoff(HunterPostShotContinuationCandidate candidate)
            {
                Candidate = candidate;
            }
        }

        private readonly struct FailedAttackObservation
        {
            public readonly HunterPostShotContinuationCandidate Candidate;
            public readonly int RecoveryAttempt;
            public readonly long ObservedAt;

            public FailedAttackObservation(
                HunterPostShotContinuationCandidate candidate,
                int recoveryAttempt,
                long observedAt)
            {
                Candidate = candidate;
                RecoveryAttempt = recoveryAttempt;
                ObservedAt = observedAt;
            }
        }

        private readonly struct RecoveryAttemptBudget
        {
            public readonly int PreyUnitId;
            public readonly uint PreyGlobalId;
            public readonly int Attempts;

            public RecoveryAttemptBudget(int preyUnitId, uint preyGlobalId, int attempts)
            {
                PreyUnitId = preyUnitId;
                PreyGlobalId = preyGlobalId;
                Attempts = attempts;
            }
        }
    }
}
