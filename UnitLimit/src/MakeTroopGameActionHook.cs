using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using SHCDESE.Interop;
using System;
using System.Reflection;

namespace UnitLimit
{
    internal struct MakeTroopGameActionDecision
    {
        public readonly bool Block;
        public readonly bool ReplaceAmount;
        public readonly int AmountToForward;
        public readonly int PendingPlayerId;
        public readonly eChimps PendingUnitType;
        public readonly int PendingAmount;

        private MakeTroopGameActionDecision(
            bool block,
            bool replaceAmount,
            int amountToForward,
            int pendingPlayerId,
            eChimps pendingUnitType,
            int pendingAmount)
        {
            Block = block;
            ReplaceAmount = replaceAmount;
            AmountToForward = amountToForward;
            PendingPlayerId = pendingPlayerId;
            PendingUnitType = pendingUnitType;
            PendingAmount = pendingAmount;
        }

        public static MakeTroopGameActionDecision AllowOriginal()
        {
            return new MakeTroopGameActionDecision(false, false, 0, 0, eChimps.CHIMP_TYPE_NULL, 0);
        }

        public static MakeTroopGameActionDecision AllowOriginalWithPending(
            int playerId,
            eChimps unitType,
            int pendingAmount)
        {
            return new MakeTroopGameActionDecision(false, false, 0, playerId, unitType, pendingAmount);
        }

        public static MakeTroopGameActionDecision ForwardAmount(
            int amount,
            int playerId,
            eChimps unitType,
            int pendingAmount)
        {
            return new MakeTroopGameActionDecision(false, true, amount, playerId, unitType, pendingAmount);
        }

        public static MakeTroopGameActionDecision BlockAction()
        {
            return new MakeTroopGameActionDecision(true, false, 0, 0, eChimps.CHIMP_TYPE_NULL, 0);
        }
    }

    internal sealed class MakeTroopGameActionHook : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly Func<int, eChimps, int, bool, MakeTroopGameActionDecision> decideMakeTroop;
        private readonly Action<MakeTroopGameActionDecision, int, bool> completeMakeTroop;
        private readonly Hook hook;
        private readonly EngineInterfaceGameActionDelegate trampoline;
        private bool disposed;

        private delegate int EngineInterfaceGameActionDelegate(Enums.GameActionCommand command, int structureID, int state, int value2);

        public MakeTroopGameActionHook(
            ManualLogSource log,
            Func<int, eChimps, int, bool, MakeTroopGameActionDecision> decideMakeTroop,
            Action<MakeTroopGameActionDecision, int, bool> completeMakeTroop)
        {
            this.log = log;
            this.decideMakeTroop = decideMakeTroop;
            this.completeMakeTroop = completeMakeTroop;

            MethodInfo gameActionMethod = typeof(EngineInterface).GetMethod(
                nameof(EngineInterface.GameAction),
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Enums.GameActionCommand), typeof(int), typeof(int), typeof(int) },
                null);

            if (gameActionMethod == null)
                throw new MissingMethodException(typeof(EngineInterface).FullName, nameof(EngineInterface.GameAction));

            hook = new Hook(gameActionMethod, (EngineInterfaceGameActionDelegate)EngineInterfaceGameActionHook);
            trampoline = hook.GenerateTrampoline<EngineInterfaceGameActionDelegate>();
            Shared.DebugLogHelper.LogDebug(log, "UnitLimit MakeTroop GameAction hook installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            hook?.Undo();
            hook?.Dispose();
            Shared.DebugLogHelper.LogDebug(log, "UnitLimit MakeTroop GameAction hook disposed.");
        }

        private int EngineInterfaceGameActionHook(Enums.GameActionCommand command, int structureID, int state, int value2)
        {
            if (command != Enums.GameActionCommand.MakeTroop)
                return trampoline(command, structureID, state, value2);

            int amount = NormalizeMakeTroopAmount(structureID, state, value2);
            using (Shared.CrashBreadcrumbScope diagnostic =
                Shared.CrashBreadcrumbDiagnostics.Enter(
                    "MakeTroopGameAction",
                    amount,
                    state,
                    value2))
            using (Shared.RecruitmentHookContext.Scope scope = Shared.RecruitmentHookContext.Enter(amount))
            {
                bool interpretCtrlSentinel = Shared.RecruitmentHookContext.ShouldInterpretCtrlSentinel(amount);
                MakeTroopGameActionDecision decision = MakeTroopGameActionDecision.AllowOriginal();
                try
                {
                    decision = decideMakeTroop(amount, (eChimps)state, state, interpretCtrlSentinel);
                }
                catch (Exception)
                {
                    Shared.CrashBreadcrumbDiagnostics.Record(
                        "MakeTroopDecisionFailure",
                        amount,
                        state,
                        value2,
                        outcome: -1);
                    decision = MakeTroopGameActionDecision.AllowOriginal();
                }

                int forwardedAmount = decision.ReplaceAmount ? decision.AmountToForward : structureID;
                if (decision.Block)
                {
                    Shared.RecruitmentHookContext.RecordBlocked();
                    diagnostic.Complete(1);
                    return 0;
                }

                if (decision.ReplaceAmount)
                {
                    Shared.RecruitmentHookContext.RecordForwardedAmount(decision.AmountToForward);
                }

                int result = CallTrampoline(command, forwardedAmount, state, value2);
                CompleteDecision(decision);
                diagnostic.Complete(decision.ReplaceAmount ? 2 : 0);
                return result;
            }
        }

        private int CallTrampoline(
            Enums.GameActionCommand command,
            int forwardedAmount,
            int state,
            int value2)
        {
            int result = trampoline(command, forwardedAmount, state, value2);
            return result;
        }

        private void CompleteDecision(MakeTroopGameActionDecision decision)
        {
            if (completeMakeTroop == null || decision.PendingAmount <= 0)
                return;

            try
            {
                Shared.RecruitmentHookContext.Result chainResult = Shared.RecruitmentHookContext.GetResult();
                completeMakeTroop(decision, chainResult.FinalAmount, chainResult.HasConcreteAmount);
            }
            catch (Exception ex)
            {
                Shared.CrashBreadcrumbDiagnostics.Record("RecruitmentCompletionFailure", outcome: -1);
                if (Shared.CrashBreadcrumbDiagnostics.ShouldLogUnexpected(
                    "RecruitmentCompletion:" + ex.GetType().FullName))
                {
                    Shared.DebugLogHelper.LogDebug(log, "UnitLimit recruitment completion failed:", ex.Message);
                }
            }
        }

        private int NormalizeMakeTroopAmount(int structureID, int state, int value2)
        {
            // For MakeTroop the generic structureID GameAction parameter is the requested amount.
            // Vanilla passes 1, 5 with Shift, or 1000 with Ctrl. Other hooks can forward exact amounts.
            if (structureID > 0)
                return structureID;

            Shared.CrashBreadcrumbDiagnostics.Record(
                "UnexpectedRecruitmentAmount",
                structureID,
                state,
                value2,
                outcome: -1);
            if (Shared.CrashBreadcrumbDiagnostics.ShouldLogUnexpected("UnexpectedRecruitmentAmount"))
            {
                Shared.DebugLogHelper.LogWarning(log, "UnitLimit MakeTroop received unexpected amount parameter: " +
                    "structureID=" + structureID +
                    " state=" + state +
                    " value2=" + value2 +
                    "; falling back to amount=1. Further occurrences are aggregated by crash diagnostics.");
            }
            return 1;
        }
    }
}
