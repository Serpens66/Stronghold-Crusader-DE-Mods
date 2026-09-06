// Feature: Restore Vanilla's intended wall-defense replenishment before outer patrol growth.
using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Diagnostics;

namespace BugfixesAndQoL
{
    internal sealed unsafe class AiDefensePatrolFix : IDisposable
    {
        // Script Extender 2.2.0 does not name these two confirmed Vanilla roles.
        private const short CastleDefenseRole = 1;
        private const short OuterPatrolRole = 4;
        private const int SummaryIntervalSeconds = 60;
        private static readonly long SummaryIntervalStopwatchTicks =
            Stopwatch.Frequency * SummaryIntervalSeconds;

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly object stateLock = new object();
        private readonly bool[] fixEffectLoggedByOwner = new bool[byte.MaxValue + 1];
        private readonly HookHandle<X64InlineHook> assignmentDecisionHook =
            new HookHandle<X64InlineHook>();
        private readonly ulong hookAddress;
        private readonly byte[] originalHookBytes;
        private HookTransaction transaction;
        private bool correctionAvailable = true;
        private bool firstDecisionLogged;
        private bool callbackFailureLogged;
        private bool disposed;
        private long nextSummaryTimestamp;
        private long intervalDecisionCount;
        private long intervalCastleDecisionCount;
        private long intervalPatrolDecisionCount;
        private long intervalFixEffectDecisionCount;
        private int ownersWithObservedFixEffect;

        public AiDefensePatrolFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (!referenceHashMatches)
            {
                throw new InvalidOperationException(
                    "The AI defense patrol fix requires the audited CrusaderDE.dll hash.");
            }

            AiDefensePatrolNativeDefinition.ValidateManagedLayout();
            AiDefensePatrolNativeDefinition.Validate(memory);

            try
            {
                hookAddress = checked(
                    libraryBase + unchecked((ulong)AiDefensePatrolNativeDefinition.DecisionHookRva));
                originalHookBytes = memory
                    .Slice(
                        AiDefensePatrolNativeDefinition.DecisionHookRva,
                        AiDefensePatrolNativeDefinition.DecisionHookLength)
                    .ToArray();
                transaction = BugfixesHookInfrastructure.CreateOwnedTransaction(region);
                BugfixesHookInfrastructure.AddContextHook(
                    transaction,
                    assignmentDecisionHook,
                    hookAddress,
                    CorrectAssignmentDecision,
                    registers: X64SmartCPUContextRegs.All,
                    hookSize: AiDefensePatrolNativeDefinition.DecisionHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.BeforeCallback);
                CommitResult commitResult = transaction.Commit();
                if (!commitResult.IsCompleteSuccess || !assignmentDecisionHook.Success)
                {
                    throw new InvalidOperationException(
                        $"The AI defense patrol assignment hook was not installed: {commitResult}.");
                }

                ApplySetting();
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Bugfixes and QoL AI defense patrol hook installed: " +
                    $"hookRva=0x{AiDefensePatrolNativeDefinition.DecisionHookRva:X}, " +
                    $"hookLength={AiDefensePatrolNativeDefinition.DecisionHookLength}, " +
                    $"nativeHookActive={assignmentDecisionHook.IsInstalled}, enabled={IsEnabled}, " +
                    "unitIdsAreOneBased=true, scanIndicesAreZeroBased=true, existingPatrolMigration=false.");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void ApplySetting()
        {
            lock (stateLock)
            {
                if (disposed || !assignmentDecisionHook.Success)
                    return;

                if (!correctionAvailable || !IsEnabled)
                {
                    DisableNativeHookAndVerify();
                    return;
                }

                if (assignmentDecisionHook.IsInstalled)
                    return;

                if (!HookBytesMatchOriginal())
                {
                    correctionAvailable = false;
                    Shared.DebugLogHelper.LogError(
                        log,
                        "Bugfixes and QoL AI defense patrol hook was not re-enabled because its native target no longer contains the verified Vanilla bytes.");
                    return;
                }

                assignmentDecisionHook.Hook.Enable();
                if (!assignmentDecisionHook.IsInstalled)
                    throw new InvalidOperationException("The AI defense patrol hook did not become active.");

                Shared.DebugLogHelper.LogDebug(
                    log,
                    "Bugfixes and QoL AI defense patrol hook enabled by the synchronized host setting.");
            }
        }

        public void Dispose()
        {
            lock (stateLock)
            {
                if (disposed)
                    return;

                correctionAvailable = false;
                DisableNativeHookAndVerify();
                disposed = true;
                transaction?.Dispose();
                transaction = null;
            }
        }

        private void CorrectAssignmentDecision(NativePointer<X64SmartCPUContext> context)
        {
            lock (stateLock)
            {
                X64SmartCPUContext* registers = context.Pointer;
                if (registers == null || !correctionAvailable || !IsEnabled)
                    return;

                ulong originalRax = registers->RAX;
                try
                {
                    int defensiveTriggerLevel = unchecked((int)(uint)originalRax);
                    int unitId = unchecked((int)(uint)registers->RBX);
                    if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* recruitedUnit) ||
                        recruitedUnit == null ||
                        recruitedUnit->r_GlobalId == 0 ||
                        (recruitedUnit->r_AliveState != AliveState.NeedsInit &&
                         recruitedUnit->r_AliveState != AliveState.IsAlive))
                    {
                        throw new InvalidOperationException(
                            $"Fresh defensive recruit is invalid: unitId={unitId}.");
                    }

                    byte ownerId = recruitedUnit->r_ControllableForPlayerId;
                    if (ownerId == 0)
                    {
                        throw new InvalidOperationException(
                            $"Fresh defensive recruit has no controllable owner: unitId={unitId}.");
                    }

                    CountDefenseRoles(ownerId, out int role1Count, out int role4Count);
                    bool needsCastleDefender = AiDefensePatrolPolicy.NeedsCastleDefender(
                        role1Count,
                        defensiveTriggerLevel);
                    registers->RAX = AiDefensePatrolPolicy.SelectComparisonValue(needsCastleDefender);
                    RecordDecision(
                        unitId,
                        recruitedUnit->r_UnitChimp,
                        ownerId,
                        role1Count,
                        role4Count,
                        defensiveTriggerLevel,
                        needsCastleDefender);
                }
                catch (Exception exception)
                {
                    // Preserve Vanilla's original DefWalls value whenever inspection is uncertain.
                    registers->RAX = originalRax;
                    if (callbackFailureLogged)
                        return;

                    callbackFailureLogged = true;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"AI_DEFENSE_PATROL_CALLBACK_FALLBACK: Vanilla assignment retained; exception={exception}");
                }
            }
        }

        private void RecordDecision(
            int unitId,
            eChimps unitType,
            byte ownerId,
            int role1Count,
            int role4Count,
            int defensiveTriggerLevel,
            bool needsCastleDefender)
        {
            intervalDecisionCount++;
            if (needsCastleDefender)
                intervalCastleDecisionCount++;
            else
                intervalPatrolDecisionCount++;

            bool fixEffectObserved = needsCastleDefender && role4Count > 0;
            if (fixEffectObserved)
                intervalFixEffectDecisionCount++;

            long now = Stopwatch.GetTimestamp();
            if (nextSummaryTimestamp == 0)
                nextSummaryTimestamp = now + SummaryIntervalStopwatchTicks;

            string decision = needsCastleDefender ? "castleDefenseRole1" : "outerPatrolRole4";
            if (!firstDecisionLogged)
            {
                firstDecisionLogged = true;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "AI_DEFENSE_PATROL_FIRST_DECISION: " +
                    DescribeDecision(
                        unitId,
                        unitType,
                        ownerId,
                        role1Count,
                        role4Count,
                        defensiveTriggerLevel,
                        decision));
            }

            if (fixEffectObserved && !fixEffectLoggedByOwner[ownerId])
            {
                fixEffectLoggedByOwner[ownerId] = true;
                ownersWithObservedFixEffect++;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "AI_DEFENSE_PATROL_FIX_EFFECT_OBSERVED: patrolSurvivedWhileCastleDefenseWasBelowTarget=true, " +
                    DescribeDecision(
                        unitId,
                        unitType,
                        ownerId,
                        role1Count,
                        role4Count,
                        defensiveTriggerLevel,
                        decision));
            }

            if (now < nextSummaryTimestamp)
                return;

            Shared.DebugLogHelper.LogInfo(
                log,
                $"AI_DEFENSE_PATROL_SUMMARY: intervalSeconds={SummaryIntervalSeconds}, " +
                $"decisions={intervalDecisionCount}, castleDecisions={intervalCastleDecisionCount}, " +
                $"patrolDecisions={intervalPatrolDecisionCount}, " +
                $"fixEffectDecisions={intervalFixEffectDecisionCount}, " +
                $"ownersWithObservedFixEffect={ownersWithObservedFixEffect}.");
            intervalDecisionCount = 0;
            intervalCastleDecisionCount = 0;
            intervalPatrolDecisionCount = 0;
            intervalFixEffectDecisionCount = 0;
            nextSummaryTimestamp = now + SummaryIntervalStopwatchTicks;
        }

        private static string DescribeDecision(
            int unitId,
            eChimps unitType,
            byte ownerId,
            int role1Count,
            int role4Count,
            int defensiveTriggerLevel,
            string decision) =>
            $"unitId={unitId}, unitType={unitType}, ownerId={ownerId}, " +
            $"role1Count={role1Count}, role4Count={role4Count}, " +
            $"defWalls={defensiveTriggerLevel}, decision={decision}.";

        private static void CountDefenseRoles(byte ownerId, out int role1Count, out int role4Count)
        {
            role1Count = 0;
            role4Count = 0;
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            if (units._array == null)
                throw new InvalidOperationException("The game unit array is unavailable.");

            for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
            {
                GameUnit* unit = units.GetValuePointer(spanIndex);
                if (unit == null ||
                    unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_ControllableForPlayerId != ownerId)
                {
                    continue;
                }

                short role = (short)unit->r_AITribeRole;
                if (role == CastleDefenseRole)
                    role1Count++;
                else if (role == OuterPatrolRole)
                    role4Count++;
            }
        }

        private bool DisableNativeHookAndVerify()
        {
            if (!assignmentDecisionHook.Success)
                return true;

            bool disableCallSucceeded = true;
            if (assignmentDecisionHook.IsInstalled)
            {
                try
                {
                    assignmentDecisionHook.Hook.Disable();
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        "Bugfixes and QoL AI defense patrol hook disabled; Vanilla code restoration requested.");
                }
                catch (Exception exception)
                {
                    disableCallSucceeded = false;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL AI defense patrol hook disable failed; exact Vanilla-byte restoration will be attempted: {exception}");
                }
            }

            bool restorationSucceeded = true;
            if (!HookBytesMatchOriginal())
            {
                try
                {
                    CodePatch.Write(hookAddress, originalHookBytes);
                    restorationSucceeded = HookBytesMatchOriginal();
                }
                catch (Exception exception)
                {
                    restorationSucceeded = false;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL AI defense patrol Vanilla-byte restoration failed: {exception}");
                }
            }

            bool bytesRestored = restorationSucceeded && HookBytesMatchOriginal();
            bool hookStateConsistent = disableCallSucceeded && !assignmentDecisionHook.IsInstalled;
            if (!bytesRestored || !hookStateConsistent)
            {
                correctionAvailable = false;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL AI defense patrol hook disable verification failed: " +
                    $"vanillaBytesRestored={bytesRestored}, hookStateConsistent={hookStateConsistent}.");
            }

            return bytesRestored;
        }

        private bool HookBytesMatchOriginal()
        {
            if (originalHookBytes == null || originalHookBytes.Length == 0 || hookAddress == 0)
                return false;

            byte* current = (byte*)hookAddress;
            for (int index = 0; index < originalHookBytes.Length; index++)
            {
                if (current[index] != originalHookBytes[index])
                    return false;
            }

            return true;
        }

        private bool IsEnabled =>
            settings.EnableMod &&
            settings.EnableAiFixes &&
            settings.EnableAiDefensePatrolFix;
    }
}
