using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Diagnostics;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace AIDefensePatrolTest
{
    internal sealed unsafe class AIDefensePatrolTestRuntime
    {
        // Script Extender 1.42.0 does not name these two confirmed Vanilla roles.
        private const short CastleDefenseRole = 1;
        private const short OuterPatrolRole = 4;
        private const int SummaryIntervalSeconds = 60;
        private static readonly long SummaryIntervalStopwatchTicks =
            Stopwatch.Frequency * SummaryIntervalSeconds;

        private readonly ManualLogSource log;
        private readonly bool[] fixEffectLoggedByOwner = new bool[byte.MaxValue + 1];
        private HookRef<X64InlineHook> assignmentDecisionHook = new HookRef<X64InlineHook>();
        private HookTransaction hookTransaction;
        private bool applied;
        private bool firstDecisionLogged;
        private bool callbackFailureLogged;
        private long nextSummaryTimestamp;
        private long intervalDecisionCount;
        private long intervalCastleDecisionCount;
        private long intervalPatrolDecisionCount;
        private long intervalFixEffectDecisionCount;
        private int ownersWithObservedFixEffect;

        internal AIDefensePatrolTestRuntime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        internal void Apply(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (applied)
                return;
            if (libraryHandle == IntPtr.Zero || memory.Length == 0)
                throw new ArgumentException("The Crusader native library is unavailable.");

            AIDefensePatrolNativeDefinition.ValidateManagedLayout();
            AIDefensePatrolNativeDefinition.Validate(memory);

            ulong libraryBase = unchecked((ulong)libraryHandle.ToInt64());
            hookTransaction = new HookTransaction(
                memory,
                libraryBase,
                Plugin.Instance.LoggerFactory,
                TransactionFailureMode.RollbackAndThrow);
            hookTransaction.AddContextHook(
                ref assignmentDecisionHook,
                libraryBase + unchecked((uint)AIDefensePatrolNativeDefinition.DecisionHookRva),
                CorrectAssignmentDecision,
                regs: X64SmartCPUContextRegs.All,
                hookSize: AIDefensePatrolNativeDefinition.DecisionHookLength,
                errorMode: CallbackErrorMode.LogAndContinue,
                placement: OverwrittenInstructionPlacement.BeforeCallback);
            hookTransaction.Commit();

            if (!assignmentDecisionHook.Success || !assignmentDecisionHook.Value.IsActive)
                throw new InvalidOperationException("The AI defense patrol assignment hook was not installed.");

            applied = true;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"AI_DEFENSE_PATROL_FIX_READY: hookRva=0x{AIDefensePatrolNativeDefinition.DecisionHookRva:X}, " +
                $"hookLength={AIDefensePatrolNativeDefinition.DecisionHookLength}, " +
                "scriptExtender=1.42.0, unitIdsAreOneBased=true, scanIndicesAreZeroBased=true, " +
                "existingPatrolMigration=false.");
        }

        private void CorrectAssignmentDecision(NativePointer<X64SmartCPUContext> context)
        {
            X64SmartCPUContext* registers = context.Pointer;
            if (registers == null)
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
                    throw new InvalidOperationException($"Fresh defensive recruit is invalid: unitId={unitId}.");
                }

                byte ownerId = recruitedUnit->r_ControllableForPlayerId;
                if (ownerId == 0)
                    throw new InvalidOperationException($"Fresh defensive recruit has no controllable owner: unitId={unitId}.");

                CountDefenseRoles(ownerId, out int role1Count, out int role4Count);
                bool needsCastleDefender = DefenseAssignmentPolicy.NeedsCastleDefender(
                    role1Count,
                    defensiveTriggerLevel);
                registers->RAX = DefenseAssignmentPolicy.SelectComparisonValue(needsCastleDefender);
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
                // Preserve Vanilla's original DefWalls value when managed inspection is uncertain.
                registers->RAX = originalRax;
                if (callbackFailureLogged)
                    return;

                callbackFailureLogged = true;
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"AI_DEFENSE_PATROL_CALLBACK_FALLBACK: Vanilla assignment retained; exception={exception}");
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
    }
}
