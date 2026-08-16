using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace ImprovedHunters
{
    /// <summary>
    /// Lets Vanilla's explicit AttackUnit path accept chickens for supported
    /// ranged attackers without bypassing its identity/alive-state checks or
    /// the subsequent range and line-of-sight checks. Automatic target
    /// candidates never execute this branch.
    /// </summary>
    internal sealed unsafe class ManualChickenAttackPatch : IDisposable
    {
        private const int ManualAttackDecisionSequenceRva = 0x18EB98;
        private const int CompatibilityCallOffset = 0x17;
        private const int CompatibilityCallDisplacementOffset = CompatibilityCallOffset + 1;
        private const int CompatibilityCallEndOffset = CompatibilityCallOffset + 5;
        private const int CompatibilityDecisionHookOffset = 0x2B;
        private const int CompatibilityDecisionHookRva =
            ManualAttackDecisionSequenceRva + CompatibilityDecisionHookOffset;
        private const int CompatibilityFunctionRva = 0x186750;
        private const int HunterBypassTargetRva = 0x18EBD2;
        private const int MaxDecisionLogs = 80;
        private const int MaxInvalidContextLogs = 10;

        private const string ManualAttackDecisionSequencePattern =
            "41 83 FC 06 74 ?? 48 8B 8C 24 D0 00 00 00 41 B1 01 " +
            "44 8B C3 41 8B D7 E8 ?? ?? ?? ?? 4C 8B 84 24 D0 00 00 00 " +
            "4C 8D 15 ?? ?? ?? ?? 85 C0 0F 85 ?? ?? ?? ??";
        private const string CompatibilityFunctionPattern =
            "48 89 5C 24 08 48 63 C2 48 8B D9 4C 69 D8 90 04 00 00 4C 03 D9";

        private readonly ManualLogSource log;
        private readonly Func<bool> canAllowManualChickenAttack;
        private HookTransaction transaction;
        private HookRef<X64InlineHook> compatibilityDecisionHook = new HookRef<X64InlineHook>();
        private bool featureAvailable = true;
        private bool hookConfirmed;
        private int decisionLogs;
        private int invalidContextLogs;
        private bool disposed;

        public ManualChickenAttackPatch(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches,
            Func<bool> canAllowManualChickenAttack)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.canAllowManualChickenAttack = canAllowManualChickenAttack ??
                throw new ArgumentNullException(nameof(canAllowManualChickenAttack));
            if (memory.Length == 0 || libraryBase == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            int sequenceRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                ManualAttackDecisionSequencePattern,
                ManualAttackDecisionSequenceRva,
                referenceHashMatches,
                "manual chicken AttackUnit compatibility decision",
                log).Rva;
            int compatibilityFunctionRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                sequenceRva + CompatibilityCallDisplacementOffset,
                sequenceRva + CompatibilityCallEndOffset);
            if (!Shared.NativePatternResolver.MatchesPatternAt(
                    memory,
                    compatibilityFunctionRva,
                    CompatibilityFunctionPattern))
            {
                throw new InvalidOperationException(
                    "The manual AttackUnit compatibility function failed byte validation.");
            }

            int hunterBypassTargetRva = ResolveShortBranchTarget(memory, sequenceRva + 4);
            int hookRva = checked(sequenceRva + CompatibilityDecisionHookOffset);
            if (referenceHashMatches &&
                (compatibilityFunctionRva != CompatibilityFunctionRva ||
                    hunterBypassTargetRva != HunterBypassTargetRva ||
                    hookRva != CompatibilityDecisionHookRva))
            {
                throw new InvalidOperationException(
                    $"The manual AttackUnit compatibility semantics changed: " +
                    $"compatibility=0x{compatibilityFunctionRva:X}, " +
                    $"hunterBypass=0x{hunterBypassTargetRva:X}, decision=0x{hookRva:X}.");
            }

            try
            {
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddContextHook(
                    ref compatibilityDecisionHook,
                    libraryBase + unchecked((ulong)hookRva),
                    AllowManualChickenTarget,
                    // Vanilla keeps its unit-manager pointer in R8 across this
                    // decision. Preserve every volatile register around the
                    // managed callback before returning to the native path.
                    regs: X64SmartCPUContextRegs.Volatile |
                        X64SmartCPUContextRegs.R14 |
                        X64SmartCPUContextRegs.R15,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.Commit();

                if (!compatibilityDecisionHook.Success)
                    throw new InvalidOperationException("The manual chicken AttackUnit hook was not installed.");

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters manual chicken AttackUnit patch initialized: " +
                    $"decisionRva=0x{hookRva:X}, compatibilityRva=0x{compatibilityFunctionRva:X}, " +
                    $"hunterBypassRva=0x{hunterBypassTargetRva:X}, " +
                    "volatileRegistersPreserved=true.");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public bool IsAvailable => featureAvailable && !disposed && compatibilityDecisionHook.Success;

        private void AllowManualChickenTarget(NativePointer<X64SmartCPUContext> context)
        {
            if (!featureAvailable || !canAllowManualChickenAttack())
                return;

            // Zero already means Vanilla accepted the target. Only replace the
            // compatibility function's well-known boolean rejection result.
            int compatibilityResult = unchecked((int)(uint)context.Pointer->RAX);
            if (compatibilityResult == 0)
                return;
            if (compatibilityResult != 1)
            {
                TryLogInvalidContext(
                    $"unexpected compatibility result {compatibilityResult}");
                return;
            }

            int targetUnitId = unchecked((int)(long)context.Pointer->R14);
            int attackerUnitId = unchecked((int)(uint)context.Pointer->R15);
            try
            {
                GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
                if (targetUnitId <= 0 ||
                    attackerUnitId <= 0 ||
                    !unitApi.TryGetUnitById(targetUnitId, out GameUnit* target) ||
                    target == null ||
                    !unitApi.TryGetUnitById(attackerUnitId, out GameUnit* attacker) ||
                    attacker == null)
                {
                    TryLogInvalidContext(
                        $"unresolved attacker/target identity attacker={attackerUnitId}, target={targetUnitId}");
                    return;
                }

                if (target->r_UnitChimp != eChimps.CHIMP_TYPE_CHICKEN ||
                    target->r_AliveState != AliveState.IsAlive ||
                    target->r_CurrentHealth == 0 ||
                    target->r_GlobalId == 0 ||
                    attacker->r_AliveState != AliveState.IsAlive ||
                    attacker->r_CurrentHealth == 0 ||
                    attacker->r_GlobalId == 0)
                {
                    return;
                }

                // Hunter type 6 bypasses the compatibility call in Vanilla and
                // therefore should never need this correction.
                if (attacker->r_UnitChimp == eChimps.CHIMP_TYPE_HUNTER)
                {
                    TryLogInvalidContext(
                        $"Hunter unexpectedly reached compatibility rejection attacker={attackerUnitId}");
                    return;
                }

                // Melee units otherwise accept the order and walk beside the
                // chicken, but Vanilla has no completing melee attack path for
                // this target type. Retain Vanilla's rejection for them.
                if (!ManualChickenAttackPolicy.CanOverrideCompatibilityRejection(
                        attacker->r_UnitChimp))
                {
                    return;
                }

                context.Pointer->RAX = 0;
                TryLogDecision(attackerUnitId, attacker, targetUnitId, target);
            }
            catch (Exception exception)
            {
                DisableFeature(exception);
            }
        }

        private void TryLogDecision(
            int attackerUnitId,
            GameUnit* attacker,
            int targetUnitId,
            GameUnit* target)
        {
            try
            {
                if (!hookConfirmed)
                {
                    hookConfirmed = true;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Improved Hunters manual chicken AttackUnit hook confirmed: " +
                        $"attacker={attackerUnitId}/{attacker->r_GlobalId}/{attacker->r_UnitChimp}, " +
                        $"target={targetUnitId}/{target->r_GlobalId}/{target->r_UnitChimp}, " +
                        "compatibility=1->0.");
                }

                if (decisionLogs >= MaxDecisionLogs)
                    return;

                decisionLogs++;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters allowed explicit chicken AttackUnit: " +
                    $"attacker={attackerUnitId}/{attacker->r_GlobalId}/{attacker->r_UnitChimp}, " +
                    $"target={targetUnitId}/{target->r_GlobalId}, compatibility=1->0 " +
                    $"({decisionLogs}/{MaxDecisionLogs}).");
            }
            catch
            {
                // Diagnostics must not undo a successfully prepared decision.
            }
        }

        private void TryLogInvalidContext(string reason)
        {
            try
            {
                if (invalidContextLogs >= MaxInvalidContextLogs)
                    return;

                invalidContextLogs++;
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Improved Hunters manual chicken AttackUnit hook left Vanilla unchanged: " +
                    $"reason={reason} ({invalidContextLogs}/{MaxInvalidContextLogs}).");
            }
            catch
            {
                // Invalid-context diagnostics are behavior-neutral.
            }
        }

        private void DisableFeature(Exception failure)
        {
            if (!featureAvailable)
                return;

            featureAvailable = false;
            Shared.DebugLogHelper.LogError(
                log,
                $"Improved Hunters disabled its manual chicken AttackUnit correction for this process; " +
                $"Vanilla command handling remains active: {failure}");
        }

        private static int ResolveShortBranchTarget(ReadOnlySpan<byte> memory, int instructionRva)
        {
            if (instructionRva < 0 || instructionRva > memory.Length - 2 || memory[instructionRva] != 0x74)
                throw new InvalidOperationException("The Hunter manual-attack bypass branch changed.");

            int displacement = unchecked((sbyte)memory[instructionRva + 1]);
            return checked(instructionRva + 2 + displacement);
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
        }
    }
}
