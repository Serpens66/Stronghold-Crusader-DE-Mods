using APIShared;
using APIShared.Units;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;

namespace BugfixesAndQoL.LordTeleportGuard
{
    internal sealed unsafe class LordTeleportGuardRuntime
    {
        // The SDK registration roots this callback for the process lifetime after Unity startup cleanup.
        private readonly UnitRelocationPolicyRegistration policy;
        private readonly GamePlayerManagerAPI players;

        internal LordTeleportGuardRuntime(ManualLogSource logger, CrusaderLibraryLoadContext context, bool modEnabled, bool optionEnabled)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (Shared.DebugLogHelper.CurrentNativeSha256 != LordTeleportPolicy.NativeHash)
                throw new InvalidOperationException("Lord policy native identity contract mismatch.");
            LordTeleportPolicy.ValidateLayouts();
            players = GamePlayerManagerAPI.Instance;
            LordTeleportPolicy.ValidateAiLineUpAnchor(context.Memory, unchecked((ulong)context.ModuleHandle.ToInt64()),
                GameGlobalsManager.Instance.AILineUpVA);
            if (!UnitRelocationService.EnsureAvailable(BugfixesAndQoLPlugin.PluginGuid, out string reason))
                throw new InvalidOperationException("Shared unit relocation is unavailable: " + reason);
            if (!UnitRelocationService.TryRegisterPolicy(BugfixesAndQoLPlugin.PluginGuid, "Lord.HomeTeleportPolicy",
                eChimps.CHIMP_TYPE_LORD, ShouldCancel, out policy, out reason))
                throw new InvalidOperationException("Lord relocation policy registration failed: " + reason);
            SetEnabled(modEnabled, optionEnabled);
        }

        internal void SetEnabled(bool modEnabled, bool optionEnabled) =>
            policy?.SetEnabled(LordTeleportPolicy.ShouldEnable(true, modEnabled, optionEnabled));

        private bool ShouldCancel(UnitRelocationContext context)
        {
            // Native callers may run during type/identity changes. An uncertain record never receives a veto.
            if (!UnitAccess.TryGetById(context.UnitId, out var unit, out _) ||
                !LordTeleportPolicy.MatchesIdentity(unit, context.UnitId, context.GlobalId,
                    context.UnitType, context.ControllingPlayerId)) return false;
            int owner = context.ControllingPlayerId;
            if (owner < 1 || owner > GamePlayerManagerAPI.MAX_PLAYERS) return false;
            return LordTeleportPolicy.ShouldCancel(context.Origin, context.UnitType, true, owner,
                players.IsAIPlayer(owner), unit->r_LinkedProductionBuildingTileEndX,
                unit->r_LinkedProductionBuildingTileEndY, context.TargetX, context.TargetY);
        }
    }
}
