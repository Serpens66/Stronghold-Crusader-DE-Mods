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
        public readonly int AmountToForward;

        private MakeTroopGameActionDecision(bool block, int amountToForward)
        {
            Block = block;
            AmountToForward = amountToForward;
        }

        public static MakeTroopGameActionDecision AllowOriginal()
        {
            return new MakeTroopGameActionDecision(false, 0);
        }

        public static MakeTroopGameActionDecision ForwardAmount(int amount)
        {
            return new MakeTroopGameActionDecision(false, amount);
        }

        public static MakeTroopGameActionDecision BlockAction()
        {
            return new MakeTroopGameActionDecision(true, 0);
        }
    }

    internal sealed class MakeTroopGameActionHook : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly Func<int, eChimps, int, MakeTroopGameActionDecision> decideMakeTroop;
        private readonly Hook hook;
        private readonly EngineInterfaceGameActionDelegate trampoline;
        private bool disposed;

        private delegate int EngineInterfaceGameActionDelegate(Enums.GameActionCommand command, int structureID, int state, int value2);

        public MakeTroopGameActionHook(ManualLogSource log, Func<int, eChimps, int, MakeTroopGameActionDecision> decideMakeTroop)
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
            log.LogDebug("UnitLimit MakeTroop GameAction hook installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            hook?.Undo();
            hook?.Dispose();
            log.LogDebug("UnitLimit MakeTroop GameAction hook disposed.");
        }

        private int EngineInterfaceGameActionHook(Enums.GameActionCommand command, int structureID, int state, int value2)
        {
            if (command != Enums.GameActionCommand.MakeTroop)
                return trampoline(command, structureID, state, value2);

            int amount = NormalizeMakeTroopAmount(structureID, state, value2);
            MakeTroopGameActionDecision decision = MakeTroopGameActionDecision.AllowOriginal();
            try
            {
                Shared.DebugLogHelper.LogDebug(
                    log,
                    "UnitLimit MakeTroop hook enter:",
                    "incomingAmount", amount,
                    "state", state,
                    "value2", value2);

                decision = decideMakeTroop(amount, (eChimps)state, state);
                Shared.DebugLogHelper.LogDebug(
                    log,
                    "UnitLimit MakeTroop hook decision:",
                    "incomingAmount", amount,
                    "state", state,
                    "value2", value2,
                    "decision", GetDecisionName(decision, amount),
                    "forwardedAmount", GetForwardedAmount(decision, amount, structureID));

                if (decision.Block)
                {
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        "UnitLimit MakeTroop hook blocked original action:",
                        "originalAmount", amount,
                        "state", state,
                        "value2", value2);
                    return 0;
                }

                if (decision.AmountToForward > 0 && decision.AmountToForward != amount)
                {
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        "UnitLimit MakeTroop hook replaced original action:",
                        "originalAmount", amount,
                        "forwardedAmount", decision.AmountToForward,
                        "state", state,
                        "value2", value2);
                    return CallTrampoline(command, decision.AmountToForward, state, value2, decision, amount);
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogDebug(log, "Unit limit game action hook failed:", ex.Message);
            }

            return CallTrampoline(command, structureID, state, value2, decision, amount);
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
                "UnitLimit MakeTroop hook trampoline enter:",
                "incomingAmount", incomingAmount,
                "state", state,
                "value2", value2,
                "decision", GetDecisionName(decision, incomingAmount),
                "forwardedAmount", forwardedAmount);
            int result = trampoline(command, forwardedAmount, state, value2);
            Shared.DebugLogHelper.LogDebug(
                log,
                "UnitLimit MakeTroop hook trampoline returned:",
                "incomingAmount", incomingAmount,
                "state", state,
                "value2", value2,
                "decision", GetDecisionName(decision, incomingAmount),
                "forwardedAmount", forwardedAmount,
                "result", result);
            return result;
        }

        private static string GetDecisionName(MakeTroopGameActionDecision decision, int incomingAmount)
        {
            if (decision.Block)
                return "BlockAction";

            if (decision.AmountToForward > 0 && decision.AmountToForward != incomingAmount)
                return "ForwardAmount";

            return "AllowOriginal";
        }

        private static int GetForwardedAmount(MakeTroopGameActionDecision decision, int incomingAmount, int originalAmount)
        {
            if (decision.Block)
                return 0;

            if (decision.AmountToForward > 0 && decision.AmountToForward != incomingAmount)
                return decision.AmountToForward;

            return originalAmount;
        }

        private int NormalizeMakeTroopAmount(int structureID, int state, int value2)
        {
            // For MakeTroop the generic structureID GameAction parameter is the requested amount.
            // Vanilla passes 1, 5 with Shift, or 1000 with Ctrl. Other hooks can forward exact amounts.
            if (structureID > 0)
                return structureID;

            log.LogWarning("UnitLimit MakeTroop received unexpected amount parameter: " +
                "structureID=" + structureID +
                " state=" + state +
                " value2=" + value2 +
                "; falling back to amount=1.");
            return 1;
        }
    }
}
