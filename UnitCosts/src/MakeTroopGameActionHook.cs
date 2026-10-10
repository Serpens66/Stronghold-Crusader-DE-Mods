using BepInEx.Logging;
using CrusaderDE;
using APIShared.Commands;
using SHCDESE.Interop;
using System;


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

    // Policy adapter only: APIShared owns the single managed interception point.
    internal sealed class MakeTroopGameActionHook
    {
        public MakeTroopGameActionHook(ManualLogSource log, Func<bool> isActive,
            Func<int, eChimps, int, bool, MakeTroopGameActionDecision> decideMakeTroop)
        {
            if (!GameActionEvents.TryRegister(UnitCostsPlugin.PluginGuid, "RecruitmentCosts", args => {
                if (args.Command != Enums.GameActionCommand.MakeTroop || args.SkipOriginalFunction || !isActive()) return;
                var decision = decideMakeTroop(Math.Max(1, args.StructureId), (eChimps)args.ActionState,
                    args.ActionState, args.InterpretCtrlSentinel);
                if (decision.Block) args.SkipOriginalFunction = true;
                else if (decision.ReplaceAmount) args.StructureId = decision.AmountToForward;
            }, null, out string reason)) throw new InvalidOperationException(reason);
            Shared.DebugLogHelper.LogDebug(log, "Recruitment cost policy registered with APIShared.");
        }
    }
}
