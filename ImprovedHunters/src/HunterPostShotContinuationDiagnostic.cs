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
        private const ushort StateTen = 10;
        private const int MaxDiagnosticLogs = 160;

        private const string StateTenPrimaryQuerySequencePattern =
            "8B 1D ? ? ? ? 8B D3 49 8B CD E8 ? ? ? ? " +
            "85 C0 48 63 05 ? ? ? ? 0F 85 ? ? ? ? E9 ? ? ? ?";
        private const string StateTenSecondaryQuerySequencePattern =
            "8B 1D ? ? ? ? 8B D3 49 8B CD E8 ? ? ? ? " +
            "85 C0 48 63 05 ? ? ? ? 0F 84 ? ? ? ? E9 ? ? ? ?";

        private static readonly long AttackObservationLifetime = Stopwatch.Frequency * 4;
        private static readonly long StateZeroHandoffLifetime = Stopwatch.Frequency * 2;

        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly Func<bool> canRun;
        private readonly TryValidateHunterPostShotContinuation tryValidateContinuation;
        private readonly object stateLock = new object();
        private readonly Dictionary<int, ShotObservation> activeShots =
            new Dictionary<int, ShotObservation>();
        private readonly Dictionary<int, PendingStateZeroHandoff> pendingStateZeroHandoffs =
            new Dictionary<int, PendingStateZeroHandoff>();
        private HookTransaction transaction;
        private HookRef<X64InlineHook> primaryQueryResultHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> secondaryQueryResultHook = new HookRef<X64InlineHook>();
        private int* currentHunterUnitId;
        private bool featureAvailable;
        private bool primaryHookConfirmed;
        private bool secondaryHookConfirmed;
        private int diagnosticLogs;
        private bool disposed;

        public HunterPostShotContinuationDiagnostic(
            ManualLogSource log,
            ImprovedHuntersViewModel settings,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches,
            Func<bool> canRun,
            TryValidateHunterPostShotContinuation tryValidateContinuation)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.canRun = canRun ?? throw new ArgumentNullException(nameof(canRun));
            this.tryValidateContinuation = tryValidateContinuation ??
                throw new ArgumentNullException(nameof(tryValidateContinuation));

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
                transaction.Commit();

                if (!primaryQueryResultHook.Success || !secondaryQueryResultHook.Success)
                    throw new InvalidOperationException("One or more Hunter post-shot query hooks were not installed.");

                featureAvailable = true;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Improved Hunters post-shot continuation diagnostic initialized: " +
                    $"primaryQueryRva=0x{primarySequenceRva + QueryCallInstructionOffset:X}, " +
                    $"primaryResultHookSpan=[0x{primaryResultRva:X},0x{primaryResultRva + QueryResultHookLength:X}), " +
                    $"secondaryQueryRva=0x{secondarySequenceRva + QueryCallInstructionOffset:X}, " +
                    $"secondaryResultHookSpan=[0x{secondaryResultRva:X},0x{secondaryResultRva + QueryResultHookLength:X}), " +
                    $"queryFunctionRva=0x{HunterQueryFunctionRva:X}, stateSixWriterRva=0x{StateSixWriterRva:X}, " +
                    "ownReservationRequired=2, liveIdentityRequired=True, PclZeroRejects=True, " +
                    "handoff=State10-query-result-to-Vanilla-state0-query-and-MoveHere, " +
                    "registerOverride=RAX-query-result-only, ownMovement=False, ownAiState=False, " +
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
            secondaryQueryResultHook.Success;

        public void RecordAcceptedAttack(
            HunterPostShotContinuationCandidate candidate,
            long timestamp)
        {
            if (!IsAvailable || !canRun() || !candidate.IsValid)
                return;

            if (!TryValidateCandidate(candidate, StateOne, out GameUnit* hunter, out GameUnit* prey))
            {
                LogDiagnostic(
                    "Improved Hunters post-shot observation rejected accepted attack: " +
                    $"hunter={candidate.HunterUnitId}/{candidate.HunterGlobalId}, " +
                    $"target={candidate.PreyUnitId}/{candidate.PreyGlobalId}/{candidate.PreyType}, " +
                    "reason=identity-state-or-own-reservation-validation-failed, behaviorMutation=False.",
                    warning: true);
                return;
            }

            ShotObservation observation = new ShotObservation(candidate, timestamp);
            lock (stateLock)
            {
                activeShots[candidate.HunterUnitId] = observation;
                pendingStateZeroHandoffs.Remove(candidate.HunterUnitId);
            }

            LogDiagnostic(
                "Improved Hunters post-shot observation queued: " +
                $"hunter={candidate.HunterUnitId}/{candidate.HunterGlobalId}, " +
                $"target={candidate.PreyUnitId}/{candidate.PreyGlobalId}/{candidate.PreyType}, " +
                $"attackSource={candidate.AttackSource}, targetHealth={prey->r_CurrentHealth}, " +
                $"reservation={*(ushort*)((byte*)prey + PreyReservationOffset)}, corpseFlag=" +
                $"{*(ushort*)((byte*)prey + PreyCorpseFlagOffset)}, {TryFormatMovementSnapshot(hunter)}, " +
                "behaviorMutation=False.");
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
            if (timestamp > handoff.ExpiresAt ||
                !TryValidateCandidate(handoff.Candidate, StateZero, out hunter, out prey) ||
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
                    "Improved Hunters post-shot State-0 handoff expired or failed revalidation: " +
                    $"hunter={hunterUnitId}, target={handoff.Candidate.PreyUnitId}/" +
                    $"{handoff.Candidate.PreyGlobalId}, validation={validation ?? "identity-or-expiry"}, " +
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
                pendingStateZeroHandoffs.Clear();
            }

            diagnosticLogs = 0;
            primaryHookConfirmed = false;
            secondaryHookConfirmed = false;
        }

        private void ObservePrimaryStateTenQueryResult(NativePointer<X64SmartCPUContext> context)
        {
            ObserveStateTenQueryResult(context, "primary-visibility-or-target-guard", ref primaryHookConfirmed);
        }

        private void ObserveSecondaryStateTenQueryResult(NativePointer<X64SmartCPUContext> context)
        {
            ObserveStateTenQueryResult(context, "secondary-target-refresh", ref secondaryHookConfirmed);
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
            if (timestamp - observation.AttackAcceptedAt > AttackObservationLifetime)
                rejection = "attack-observation-expired";
            else if (!TryValidateCandidate(candidate, StateTen, out hunter, out prey))
                rejection = "identity-state-or-own-reservation-validation-failed";
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

            PendingStateZeroHandoff handoff = new PendingStateZeroHandoff(
                candidate,
                timestamp + StateZeroHandoffLifetime);
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
            out GameUnit* hunter,
            out GameUnit* prey)
        {
            hunter = null;
            prey = null;
            if (!candidate.IsValid ||
                !settings.IsHuntingEnabled(candidate.PreyType) ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(candidate.HunterUnitId, out hunter) ||
                hunter == null ||
                hunter->r_AliveState != AliveState.IsAlive ||
                hunter->r_CurrentHealth == 0 ||
                hunter->r_GlobalId != candidate.HunterGlobalId ||
                hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER ||
                *(ushort*)((byte*)hunter + HunterAiStateOffset) != requiredHunterState ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(candidate.PreyUnitId, out prey) ||
                prey == null ||
                prey->r_AliveState != AliveState.IsAlive ||
                prey->r_CurrentHealth == 0 ||
                prey->r_GlobalId != candidate.PreyGlobalId ||
                prey->r_UnitChimp != candidate.PreyType)
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
                pendingStateZeroHandoffs.Clear();
            }
        }

        private readonly struct ShotObservation
        {
            public readonly HunterPostShotContinuationCandidate Candidate;
            public readonly long AttackAcceptedAt;
            public readonly long ProjectileId;
            public readonly uint ProjectileGlobalId;

            public ShotObservation(
                HunterPostShotContinuationCandidate candidate,
                long attackAcceptedAt,
                long projectileId = 0,
                uint projectileGlobalId = 0)
            {
                Candidate = candidate;
                AttackAcceptedAt = attackAcceptedAt;
                ProjectileId = projectileId;
                ProjectileGlobalId = projectileGlobalId;
            }

            public ShotObservation WithProjectile(long projectileId, uint projectileGlobalId) =>
                new ShotObservation(Candidate, AttackAcceptedAt, projectileId, projectileGlobalId);
        }

        private readonly struct PendingStateZeroHandoff
        {
            public readonly HunterPostShotContinuationCandidate Candidate;
            public readonly long ExpiresAt;

            public PendingStateZeroHandoff(
                HunterPostShotContinuationCandidate candidate,
                long expiresAt)
            {
                Candidate = candidate;
                ExpiresAt = expiresAt;
            }
        }
    }
}
