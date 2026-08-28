using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using SHCDESE.Interop;
using System;
using System.Reflection;

namespace UnitCosts
{
    internal struct MakeTroopGameActionDecision
    {
        public readonly bool Block;
        public readonly bool ReplaceAmount;
        public readonly int AmountToForward;

        private MakeTroopGameActionDecision(bool block, bool replaceAmount, int amountToForward)
        {
            Block = block;
            ReplaceAmount = replaceAmount;
            AmountToForward = amountToForward;
        }

        public static MakeTroopGameActionDecision AllowOriginal()
        {
            return new MakeTroopGameActionDecision(false, false, 0);
        }

        public static MakeTroopGameActionDecision ForwardAmount(int amount)
        {
            return new MakeTroopGameActionDecision(false, true, amount);
        }

        public static MakeTroopGameActionDecision BlockAction()
        {
            return new MakeTroopGameActionDecision(true, false, 0);
        }
    }

    internal sealed class MakeTroopGameActionHook : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly Func<int, eChimps, int, bool, MakeTroopGameActionDecision> decideMakeTroop;
        private readonly Hook hook;
        private readonly EngineInterfaceGameActionDelegate trampoline;
        private bool disposed;

        private delegate int EngineInterfaceGameActionDelegate(Enums.GameActionCommand command, int structureID, int state, int value2);

        public MakeTroopGameActionHook(ManualLogSource log, Func<int, eChimps, int, bool, MakeTroopGameActionDecision> decideMakeTroop)
        {
            this.log = log;
            this.decideMakeTroop = decideMakeTroop;

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
            Shared.DebugLogHelper.LogDebug(log, "UnitCosts MakeTroop GameAction hook installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            hook?.Undo();
            hook?.Dispose();
            Shared.DebugLogHelper.LogDebug(log, "UnitCosts MakeTroop GameAction hook disposed.");
        }

        private int EngineInterfaceGameActionHook(Enums.GameActionCommand command, int structureID, int state, int value2)
        {
            if (command != Enums.GameActionCommand.MakeTroop)
                return trampoline(command, structureID, state, value2);

            int amount = NormalizeMakeTroopAmount(structureID, state, value2);
            using (Shared.RecruitmentHookContext.Scope scope = Shared.RecruitmentHookContext.Enter(amount))
            {
                bool interpretCtrlSentinel = Shared.RecruitmentHookContext.ShouldInterpretCtrlSentinel(amount);
                MakeTroopGameActionDecision decision = MakeTroopGameActionDecision.AllowOriginal();
                try
                {
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        "UnitCosts MakeTroop hook enter:",
                        "incomingAmount", amount,
                        "interpretCtrlSentinel", interpretCtrlSentinel,
                        "state", state,
                        "value2", value2);

                    decision = decideMakeTroop(amount, (eChimps)state, state, interpretCtrlSentinel);
                }
                catch (Exception ex)
                {
                    Shared.DebugLogHelper.LogDebug(log, "UnitCosts game action decision failed:", ex.Message);
                    decision = MakeTroopGameActionDecision.AllowOriginal();
                }

                int forwardedAmount = decision.ReplaceAmount ? decision.AmountToForward : structureID;
                Shared.DebugLogHelper.LogDebug(
                    log,
                    "UnitCosts MakeTroop hook decision:",
                    "incomingAmount", amount,
                    "interpretCtrlSentinel", interpretCtrlSentinel,
                    "state", state,
                    "value2", value2,
                    "decision", GetDecisionName(decision),
                    "forwardedAmount", decision.Block ? 0 : forwardedAmount);

                if (decision.Block)
                {
                    Shared.RecruitmentHookContext.RecordBlocked();
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        "UnitCosts MakeTroop hook blocked original action:",
                        "originalAmount", amount,
                        "state", state,
                        "value2", value2);
                    return 0;
                }

                if (decision.ReplaceAmount)
                {
                    Shared.RecruitmentHookContext.RecordForwardedAmount(decision.AmountToForward);
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        "UnitCosts MakeTroop hook replaced original action:",
                        "originalAmount", amount,
                        "forwardedAmount", decision.AmountToForward,
                        "state", state,
                        "value2", value2);
                }

                return CallTrampoline(command, forwardedAmount, state, value2, decision, amount);
            }
        }

        private int CallTrampoline(
            Enums.GameActionCommand command,
            int forwardedAmount,
            int state,
            int value2,
            MakeTroopGameActionDecision decision,
            int incomingAmount)
        {
            Shared.DebugLogHelper.LogDebug(
                log,
                "UnitCosts MakeTroop hook trampoline enter:",
                "incomingAmount", incomingAmount,
                "state", state,
                "value2", value2,
                "decision", GetDecisionName(decision),
                "forwardedAmount", forwardedAmount);
            int result = trampoline(command, forwardedAmount, state, value2);
            Shared.DebugLogHelper.LogDebug(
                log,
                "UnitCosts MakeTroop hook trampoline returned:",
                "incomingAmount", incomingAmount,
                "state", state,
                "value2", value2,
                "decision", GetDecisionName(decision),
                "forwardedAmount", forwardedAmount,
                "result", result);
            return result;
        }

        private static string GetDecisionName(MakeTroopGameActionDecision decision)
        {
            if (decision.Block)
                return "BlockAction";

            if (decision.ReplaceAmount)
                return "ForwardAmount";

            return "AllowOriginal";
        }

        private int NormalizeMakeTroopAmount(int structureID, int state, int value2)
        {
            // For MakeTroop the generic structureID GameAction parameter is the requested amount.
            // Vanilla passes 1, 5 with Shift, or 1000 with Ctrl. Other hooks can forward exact amounts.
            if (structureID > 0)
                return structureID;

            Shared.DebugLogHelper.LogWarning(log, "UnitCosts MakeTroop received unexpected amount parameter: " +
                "structureID=" + structureID +
                " state=" + state +
                " value2=" + value2 +
                "; falling back to amount=1.");
            return 1;
        }
    }
}
