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
    internal sealed unsafe partial class HunterTargetSearchFallbackDiagnostic
    {
        // State-1 visibility handoff and moving-target path regeneration remain
        // diagnostic code, but are isolated from State-0 target selection.
        private void CaptureStateOneNearRefreshContext(NativePointer<X64SmartCPUContext> context)
        {
            ClearThreadState();
            if (!IsAvailable ||
                !canRunPathfinding() ||
                stateOneWorldDistanceScratch == null ||
                stateOneCurrentHunterUnitId == null)
            {
                return;
            }

            // Vanilla itself loads this actor ID for the immediately following
            // near-target query. RBX is not used as an undocumented substitute.
            int hunterUnitId = *stateOneCurrentHunterUnitId;
            int nativeWorldDistance = *stateOneWorldDistanceScratch;
            if ((uint)nativeWorldDistance > StateOneContinuationDistance)
                return;

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

            if (action == HunterStateOneNearRefreshAction.ContinueExistingPath &&
                TryPrepareMovingTargetNearReplan(
                    refreshCandidate,
                    nativeWorldDistance,
                    out Candidate movingTargetCandidate,
                    out string movingTargetDetail))
            {
                stagedMovingTargetReplan = movingTargetCandidate;
                LogStateOneDiagnostic(
                    "Improved Hunters detected stale moving-target path and allowed one Vanilla refresh: " +
                    $"hunter={hunterUnitId}, target={movingTargetCandidate.PreyUnitId}/" +
                    $"{movingTargetCandidate.PreyGlobalId}/{movingTargetCandidate.PreyType}, " +
                    $"nativeWorldDistance={nativeWorldDistance}, {movingTargetDetail}, " +
                    $"replanAttempt={movingTargetCandidate.MovingTargetReplanAttempt}/" +
                    $"{MaximumMovingTargetReplans}, querySkipped=False, " +
                    "ownMovement=False, ownAiState=False, ownOrderWrite=False, " +
                    "speedWrite=False, animationWrite=False, compareScratchWrite=False.");
                return;
            }

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

        private void CompleteStateOneMovingTargetReplanQuery(
            NativePointer<X64SmartCPUContext> context)
        {
            Candidate candidate = stagedMovingTargetReplan;
            stagedMovingTargetReplan = default;
            if (!candidate.IsMovingTargetReplan ||
                !IsAvailable ||
                !canRunPathfinding() ||
                stateOneCurrentHunterUnitId == null)
            {
                return;
            }

            int currentHunterUnitId = unchecked((int)(uint)context.Pointer->RAX);
            int expectedHunterUnitId = *stateOneCurrentHunterUnitId;
            bool vanillaQueryReturnedZero =
                (context.Pointer->Rflags & ZeroFlagMask) != 0;
            if (currentHunterUnitId != candidate.HunterUnitId ||
                expectedHunterUnitId != candidate.HunterUnitId ||
                !TryValidateOwnReservationCandidate(candidate, requiredAiState: 1))
            {
                LogStateOneDiagnostic(
                    "Improved Hunters discarded stale moving-target refresh result: " +
                    $"hunter={expectedHunterUnitId}, reloadedHunter={currentHunterUnitId}, " +
                    $"target={candidate.PreyUnitId}/{candidate.PreyGlobalId}, " +
                    "reason=identity-or-reservation-changed, behaviorMutation=False.",
                    warning: true);
                return;
            }

            lock (observationLock)
                pendingMovingTargetContinuations[candidate.HunterUnitId] = candidate;

            // The untouched JE consumes ZF; no synthetic query result or state is written.
            if (vanillaQueryReturnedZero)
                context.Pointer->Rflags &= ~ZeroFlagMask;

            LogStateOneDiagnostic(
                "Improved Hunters moving-target refresh selected Vanilla State-0 replan: " +
                $"hunter={candidate.HunterUnitId}, target={candidate.PreyUnitId}/" +
                $"{candidate.PreyGlobalId}/{candidate.PreyType}, " +
                $"vanillaQueryReturnedZero={vanillaQueryReturnedZero}, " +
                $"acceptedPathGeneration={candidate.MovingTargetSourceGeneration}, " +
                $"replanAttempt={candidate.MovingTargetReplanAttempt}/" +
                $"{MaximumMovingTargetReplans}, " +
                $"registerOverride={(vanillaQueryReturnedZero ? "ZF-clear-only" : "none")}, " +
                "next=Vanilla-State0-query-and-MoveHere, behaviorMutation=True.");
        }

        private void ObserveStateOneDirectAttackResult(NativePointer<X64SmartCPUContext> context)
        {
            if (!IsAvailable || !canRunPathfinding())
                return;

            int hunterUnitId = unchecked((int)(uint)context.Pointer->RDX);
            int attackResult = unchecked((int)(uint)context.Pointer->RAX);
            long timestamp = Stopwatch.GetTimestamp();
            AcceptedMoveObservation observation;
            lock (observationLock)
            {
                if (!acceptedMoveObservations.TryGetValue(hunterUnitId, out observation))
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

            if (TryValidateOwnReservationCandidate(candidate, requiredAiState: 1))
            {
                HunterPostShotContinuationCandidate postShotCandidate =
                    new HunterPostShotContinuationCandidate(
                        candidate.HunterUnitId,
                        hunter->r_GlobalId,
                        candidate.PreyUnitId,
                        candidate.PreyGlobalId,
                        candidate.PreyType,
                        candidate.Source);
                try
                {
                    if (attackResult != 0)
                        recordAcceptedPostShotAttack(postShotCandidate, timestamp);
                    else
                        recordFailedPostShotAttack(postShotCandidate, timestamp);
                }
                catch (Exception exception)
                {
                    LogStateOneDiagnostic(
                        "Improved Hunters post-attack recovery recording failed independently: " +
                        $"hunter={candidate.HunterUnitId}, target={candidate.PreyUnitId}/" +
                        $"{candidate.PreyGlobalId}, error={exception.Message}.",
                        warning: true);
                }
            }
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

        private AcceptedMoveObservation CaptureAcceptedMoveObservation(
            Candidate candidate,
            long timestamp)
        {
            long pathGeneration = Interlocked.Increment(ref nextAcceptedPathGeneration);
            int movingTargetReplans = candidate.IsMovingTargetReplan
                ? candidate.MovingTargetReplanAttempt
                : 0;
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(
                    candidate.HunterUnitId,
                    out GameUnit* hunter) ||
                hunter == null ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(
                    candidate.PreyUnitId,
                    out GameUnit* prey) ||
                prey == null ||
                prey->r_GlobalId != candidate.PreyGlobalId)
            {
                return new AcceptedMoveObservation(
                    candidate,
                    timestamp,
                    pathGeneration,
                    movingTargetReplans);
            }

            byte* hunterBytes = (byte*)hunter;
            ushort pathState = *(ushort*)(hunterBytes + HunterPathStateOffset);
            uint pathLength = *(uint*)(hunterBytes + HunterPathLengthOffset);
            bool hasPathAnchor =
                pathState == 2 && pathLength > 1 && pathLength <= 2000;
            return new AcceptedMoveObservation(
                candidate,
                timestamp,
                pathGeneration,
                movingTargetReplans,
                hasPathAnchor,
                prey->r_CurrentTilePositionX,
                prey->r_CurrentTilePositionY,
                pathLength,
                replanRequested: false);
        }

        private bool TryPrepareMovingTargetNearReplan(
            Candidate refreshCandidate,
            int nativeWorldDistance,
            out Candidate candidate,
            out string detail)
        {
            candidate = default;
            detail = "moving-target-context-unavailable";
            AcceptedMoveObservation observation;
            lock (observationLock)
            {
                if (!acceptedMoveObservations.TryGetValue(
                        refreshCandidate.HunterUnitId,
                        out observation))
                {
                    return false;
                }
            }

            if (!observation.Matches(refreshCandidate) ||
                !observation.HasPathAnchor ||
                observation.ReplanRequested ||
                observation.MovingTargetReplans >= MaximumMovingTargetReplans ||
                nativeWorldDistance < 0 ||
                nativeWorldDistance > StateOneRefreshDistance ||
                !TryValidateOwnReservationCandidate(refreshCandidate, requiredAiState: 1))
            {
                return false;
            }

            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (!unitApi.TryGetUnitById(refreshCandidate.HunterUnitId, out GameUnit* hunter) ||
                hunter == null ||
                !unitApi.TryGetUnitById(refreshCandidate.PreyUnitId, out GameUnit* prey) ||
                prey == null)
            {
                return false;
            }

            byte* hunterBytes = (byte*)hunter;
            ushort pathState = *(ushort*)(hunterBytes + HunterPathStateOffset);
            ushort pathProgress = *(ushort*)(hunterBytes + HunterPathProgressOffset);
            uint pathLength = *(uint*)(hunterBytes + HunterPathLengthOffset);
            if (pathState != 2 ||
                pathLength != observation.PathLength ||
                pathProgress >= pathLength)
            {
                return false;
            }

            int hunterX = hunter->r_CurrentTilePositionX;
            int hunterY = hunter->r_CurrentTilePositionY;
            int preyX = prey->r_CurrentTilePositionX;
            int preyY = prey->r_CurrentTilePositionY;
            int anchorDx = Math.Abs(preyX - observation.AnchorTileX);
            int anchorDy = Math.Abs(preyY - observation.AnchorTileY);
            int anchorDisplacement = Math.Max(anchorDx, anchorDy);
            int targetDistance = Math.Abs(preyX - hunterX) + Math.Abs(preyY - hunterY);
            int oldAnchorDistance =
                Math.Abs(observation.AnchorTileX - hunterX) +
                Math.Abs(observation.AnchorTileY - hunterY);
            long directionDot =
                (long)(preyX - hunterX) * (observation.AnchorTileX - hunterX) +
                (long)(preyY - hunterY) * (observation.AnchorTileY - hunterY);
            // Displacement alone would churn routes for harmless herd motion. The
            // opposing vectors identify the actual crossed-past-the-Hunter case.
            if (anchorDisplacement < MinimumMovingTargetAnchorDisplacement ||
                directionDot >= 0 ||
                targetDistance > oldAnchorDistance)
            {
                return false;
            }

            long timestamp = Stopwatch.GetTimestamp();
            if (!tryValidateContinuation(
                    refreshCandidate.HunterUnitId,
                    refreshCandidate.PreyUnitId,
                    refreshCandidate.PreyGlobalId,
                    refreshCandidate.PreyType,
                    timestamp,
                    out string validation))
            {
                detail = $"runtimeValidation={validation}";
                return false;
            }

            int replanAttempt = observation.MovingTargetReplans + 1;
            lock (observationLock)
            {
                if (!acceptedMoveObservations.TryGetValue(
                        refreshCandidate.HunterUnitId,
                        out AcceptedMoveObservation current) ||
                    current.PathGeneration != observation.PathGeneration ||
                    current.ReplanRequested)
                {
                    return false;
                }

                acceptedMoveObservations[refreshCandidate.HunterUnitId] =
                    current.WithReplanRequested();
            }

            candidate = observation.Candidate.AsMovingTargetReplan(
                replanAttempt,
                observation.PathGeneration);
            detail =
                $"acceptedPathGeneration={observation.PathGeneration}, " +
                $"anchor={observation.AnchorTileX},{observation.AnchorTileY}, " +
                $"hunterTile={hunterX},{hunterY}, preyTile={preyX},{preyY}, " +
                $"anchorChebyshevDisplacement={anchorDisplacement}, " +
                $"targetManhattanDistance={targetDistance}, " +
                $"oldAnchorManhattanDistance={oldAnchorDistance}, " +
                $"directionDot={directionDot}, path={pathState}/{pathProgress}/{pathLength}, " +
                $"validation={validation}";
            return true;
        }

        private bool TryPrepareMovingTargetStateZeroContinuation(
            int hunterUnitId,
            long timestamp,
            out Candidate candidate)
        {
            candidate = default;
            Candidate pending;
            lock (observationLock)
                pendingMovingTargetContinuations.TryGetValue(hunterUnitId, out pending);

            if (pending.IsMovingTargetReplan)
            {
                if (TryValidateOwnReservationCandidate(
                        pending,
                        requiredAiState: 0,
                        allowReleasedStateZeroTransition: true) &&
                    tryValidateContinuation(
                        pending.HunterUnitId,
                        pending.PreyUnitId,
                        pending.PreyGlobalId,
                        pending.PreyType,
                        timestamp,
                        out string pendingValidation))
                {
                    candidate = pending;
                    LogDiagnostic(
                        "Improved Hunters moving-target State-0 continuation prepared: " +
                        $"hunter={pending.HunterUnitId}, target={pending.PreyUnitId}/" +
                        $"{pending.PreyGlobalId}/{pending.PreyType}, " +
                        $"acceptedPathGeneration={pending.MovingTargetSourceGeneration}, " +
                        $"replanAttempt={pending.MovingTargetReplanAttempt}/" +
                        $"{MaximumMovingTargetReplans}, validation={pendingValidation}.");
                    return true;
                }

                lock (observationLock)
                    pendingMovingTargetContinuations.Remove(hunterUnitId);
                return false;
            }

            AcceptedMoveObservation observation;
            lock (observationLock)
            {
                if (!acceptedMoveObservations.TryGetValue(hunterUnitId, out observation) ||
                    !observation.HasPathAnchor ||
                    observation.ReplanRequested ||
                    observation.MovingTargetReplans >= MaximumMovingTargetReplans)
                {
                    return false;
                }
            }

            if (!TryValidateHunter(hunterUnitId, requiredAiState: 0, out GameUnit* hunter) ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(
                    observation.Candidate.PreyUnitId,
                    out GameUnit* prey) ||
                prey == null ||
                prey->r_GlobalId != observation.Candidate.PreyGlobalId)
            {
                return false;
            }

            byte* hunterBytes = (byte*)hunter;
            ushort pathProgress = *(ushort*)(hunterBytes + HunterPathProgressOffset);
            uint pathLength = *(uint*)(hunterBytes + HunterPathLengthOffset);
            int anchorDisplacement = Math.Max(
                Math.Abs(prey->r_CurrentTilePositionX - observation.AnchorTileX),
                Math.Abs(prey->r_CurrentTilePositionY - observation.AnchorTileY));
            if (pathLength != observation.PathLength ||
                pathProgress < pathLength ||
                anchorDisplacement < MinimumMovingTargetAnchorDisplacement)
            {
                return false;
            }

            Candidate fallbackCandidate = observation.Candidate.AsMovingTargetReplan(
                observation.MovingTargetReplans + 1,
                observation.PathGeneration);
            if (!TryValidateOwnReservationCandidate(
                    fallbackCandidate,
                    requiredAiState: 0,
                    allowReleasedStateZeroTransition: true) ||
                !tryValidateContinuation(
                    fallbackCandidate.HunterUnitId,
                    fallbackCandidate.PreyUnitId,
                    fallbackCandidate.PreyGlobalId,
                    fallbackCandidate.PreyType,
                    timestamp,
                    out string validation))
            {
                return false;
            }

            lock (observationLock)
            {
                if (!acceptedMoveObservations.TryGetValue(
                        hunterUnitId,
                        out AcceptedMoveObservation current) ||
                    current.PathGeneration != observation.PathGeneration ||
                    current.ReplanRequested)
                {
                    return false;
                }

                acceptedMoveObservations[hunterUnitId] = current.WithReplanRequested();
                pendingMovingTargetContinuations[hunterUnitId] = fallbackCandidate;
            }

            candidate = fallbackCandidate;
            LogDiagnostic(
                "Improved Hunters prepared path-complete moving-target fallback: " +
                $"hunter={hunterUnitId}, target={candidate.PreyUnitId}/" +
                $"{candidate.PreyGlobalId}/{candidate.PreyType}, " +
                $"acceptedPathGeneration={candidate.MovingTargetSourceGeneration}, " +
                $"path={pathProgress}/{pathLength}, " +
                $"anchor={observation.AnchorTileX},{observation.AnchorTileY}, " +
                $"preyTile={prey->r_CurrentTilePositionX},{prey->r_CurrentTilePositionY}, " +
                $"anchorChebyshevDisplacement={anchorDisplacement}, " +
                $"replanAttempt={candidate.MovingTargetReplanAttempt}/" +
                $"{MaximumMovingTargetReplans}, validation={validation}, " +
                "next=Vanilla-State0-query-and-MoveHere.");
            return true;
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

        private static void ValidateStateOneRefreshResultHookSpan(
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            int hookRva,
            ulong expectedCurrentHunterUnitIdAddress)
        {
            const int decodeLookahead = 32;
            if (hookRva < 0 || hookRva > memory.Length - decodeLookahead)
                throw new InvalidOperationException("State-1 refresh result hook lies outside the module image.");

            ulong hookAddress = libraryBase + unchecked((ulong)hookRva);
            Decoder decoder = Decoder.Create(
                64,
                new ByteArrayCodeReader(memory.Slice(hookRva, decodeLookahead).ToArray()),
                hookAddress);
            Instruction queryCall = decoder.Decode();
            Instruction resultTest = decoder.Decode();
            Instruction hunterIdLoad = decoder.Decode();
            int decodedHookLength = checked((int)(decoder.IP - hookAddress));
            if (queryCall.IsInvalid ||
                resultTest.IsInvalid ||
                hunterIdLoad.IsInvalid ||
                queryCall.Mnemonic != Mnemonic.Call ||
                queryCall.FlowControl != FlowControl.Call ||
                queryCall.Length != 5 ||
                queryCall.NearBranchTarget !=
                    libraryBase + unchecked((ulong)HunterQueryFunctionRva) ||
                resultTest.Mnemonic != Mnemonic.Test ||
                resultTest.Length != 2 ||
                resultTest.Op0Register != Register.EAX ||
                resultTest.Op1Register != Register.EAX ||
                hunterIdLoad.Mnemonic != Mnemonic.Movsxd ||
                hunterIdLoad.Length != 7 ||
                hunterIdLoad.Op0Register != Register.RAX ||
                !hunterIdLoad.IsIPRelativeMemoryOperand ||
                hunterIdLoad.IPRelativeMemoryAddress != expectedCurrentHunterUnitIdAddress ||
                decodedHookLength != StateOneRefreshQueryResultHookLength)
            {
                throw new InvalidOperationException(
                    "State-1 refresh result hook does not decode as the audited 5+2+7-byte span.");
            }

            ulong hookEndAddress = hookAddress + StateOneRefreshQueryResultHookLength;
            Instruction failureBranch = decoder.Decode();
            ulong expectedFailureTarget =
                hookAddress + StateOneRefreshFailureBranchTargetOffset;
            if (failureBranch.IsInvalid ||
                failureBranch.IP != hookEndAddress ||
                failureBranch.Mnemonic != Mnemonic.Je ||
                failureBranch.Length != 2 ||
                failureBranch.FlowControl != FlowControl.ConditionalBranch ||
                failureBranch.NearBranchTarget != expectedFailureTarget ||
                failureBranch.NearBranchTarget <= hookEndAddress)
            {
                throw new InvalidOperationException(
                    $"State-1 refresh result span changed or is unsafe: " +
                    $"failureTarget=0x{failureBranch.NearBranchTarget:X}, " +
                    $"hookSpan=[0x{hookAddress:X},0x{hookEndAddress:X}).");
            }

            ValidateNoExternalDirectBranchTargetsInsideHook(
                memory,
                libraryBase,
                hookAddress,
                hookEndAddress);
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

    }
}
