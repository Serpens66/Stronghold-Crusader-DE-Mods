using System;
using APIShared;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;

namespace BugfixesAndQoL
{
    // APIShared roots BeforeUpdate for the process lifetime. Session publishers only toggle active;
    // this consumer owns no hook, search worker or Unity callback.
    internal sealed unsafe class AssassinCapturedGateProtectionRuntime
    {
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private ushort* buildings;
        private bool registered, active, confirmed;

        internal AssassinCapturedGateProtectionRuntime(ManualLogSource log, BugfixesAndQoLViewModel settings)
        { this.log = log; this.settings = settings; }

        internal void InitializeNative(IntPtr module)
        {
            if (registered) return;
            if (module == IntPtr.Zero) throw new ArgumentException("Native module required.", nameof(module));
            // Audited native tile-to-building map: 320800 ushort slots, one-based building IDs.
            buildings = (ushort*)((byte*)module + 0x4B6AA50);
            AssassinAttackControlAPI.RegisterGuard(BugfixesAndQoLPlugin.PluginGuid, BeforeUpdate,
                AssassinObstacleCompletionMode.NativeRetarget);
            registered = true;
        }

        internal void BeginMap() { active = true; }
        internal void EndMap() { active = false; }

        internal bool BeforeUpdate(int unitId)
        {
            if (!active || !settings.EnableMod || !settings.EnableAssassinCapturedGateProtectionFix) return false;
            if (!confirmed)
            {
                confirmed = true;
                log.LogDebug($"{DateTime.Now:HH:mm:ss.fff} Assassin captured-gate protection: persistent callback active after startup cleanup.");
            }
            if (!UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                !AssassinCapturedGateProtectionPolicy.IsEligibleUnit(in *unit)) return false;
            int player = unit->r_ControllableForPlayerId;
            var players = GamePlayerManagerAPI.Instance;
            if (!players.IsAIPlayer(player)) return false;
            int buildingId = buildings[unit->r_AI_ContextTargetBuildingTileId];
            if (buildingId == 0 ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                !AssassinCapturedGateProtectionPolicy.IsProtectedGate(in *building, true)) return false;
            int capturer = building->r_CapturedByPlayerId;
            return capturer == player || players.IsPlayerAlliedTo(player, capturer);
        }
    }
}
