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

namespace BugfixesAndQoL
{
    internal sealed unsafe class AiDefensePatrolFix : IDisposable
    {
        // The current Script Extender does not name these two confirmed Vanilla roles.
        private const short CastleDefenseRole = 1;
        private const short OuterPatrolRole = 4;
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly object stateLock = new object();
        private readonly HookHandle<X64InlineHook> assignmentDecisionHook =
            new HookHandle<X64InlineHook>();
        private HookTransaction transaction;
        private volatile bool correctionAvailable = true;
        private volatile bool logicallyEnabled;
        private bool callbackFailureLogged;
        private bool disposed;

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
                ulong hookAddress = checked(
                    libraryBase + unchecked((ulong)AiDefensePatrolNativeDefinition.DecisionHookRva));
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
                correctionAvailable = false;
                transaction?.Dispose();
                transaction = null;
                disposed = true;
                throw;
            }
        }

        public void ApplySetting()
        {
            lock (stateLock)
            {
                if (disposed || !assignmentDecisionHook.Success)
                    return;

                if (!assignmentDecisionHook.IsInstalled)
                {
                    correctionAvailable = false;
                    logicallyEnabled = false;
                    Shared.DebugLogHelper.LogError(
                        log,
                        "Bugfixes and QoL AI defense patrol fix was disabled because its permanent native hook is no longer installed.");
                    return;
                }

                logicallyEnabled = settings.EnableMod && settings.EnableAiDefensePatrolFix;
            }
        }

        public void Dispose()
        {
            lock (stateLock)
            {
                if (disposed)
                    return;

                correctionAvailable = false;
                logicallyEnabled = false;
                disposed = true;
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

        private bool IsEnabled => logicallyEnabled;
    }
}
