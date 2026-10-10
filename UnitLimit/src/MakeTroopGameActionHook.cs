using BepInEx.Logging;
using CrusaderDE;
using APIShared.Commands;
using SHCDESE.Interop;
using System;


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

    // Pending reservations belong to this policy, while APIShared supplies final accepted inputs.
    internal sealed class MakeTroopGameActionHook
    {
        public MakeTroopGameActionHook(ManualLogSource log,
            Func<int, eChimps, int, bool, MakeTroopGameActionDecision> decideMakeTroop,
            Action<MakeTroopGameActionDecision, int, bool> completeMakeTroop, Func<bool> isActive)
        {
            if (!GameActionEvents.TryRegister(UnitLimitPlugin.PluginGuid, "RecruitmentLimits", args => {
                if (args.Command != Enums.GameActionCommand.MakeTroop || args.SkipOriginalFunction || !isActive()) return;
                var decision = decideMakeTroop(Math.Max(1, args.StructureId), (eChimps)args.ActionState,
                    args.ActionState, args.InterpretCtrlSentinel);
                args.State = decision;
                if (decision.Block) args.SkipOriginalFunction = true;
                else if (decision.ReplaceAmount) args.StructureId = decision.AmountToForward;
            }, args => {
                if (args.WasSkipped || !(args.State is MakeTroopGameActionDecision decision) || decision.PendingAmount <= 0 || args.ActionState != (int)decision.PendingUnitType) return;
                completeMakeTroop?.Invoke(decision, args.StructureId, args.HasConcreteRecruitmentAmount);
            }, out string reason)) throw new InvalidOperationException(reason);
            Shared.DebugLogHelper.LogDebug(log, "Recruitment limit policy registered with APIShared.");
        }
    }
}
