using SHCDESE.API;
using SHCDESE.EventAPI.Projectiles;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Zhuqiaomon.Memory;

namespace ImprovedHunters
{
    internal sealed partial class ImprovedHuntersRuntime
    {
        // Projectile events remain observation-only because the unfinished
        // post-shot path continuation still correlates them with Hunter state.
        private unsafe void OnProjectileSpawn(ProjectileSpawnEventArgs args)
        {
            if (!TryGetObservableHunterProjectileTarget(args, out PreyEligibility eligibility))
                return;

            bool hasHunterContext = TryResolveHunterForProjectile(
                args.SourceUnitId,
                args.AttackedUnitId,
                eligibility.GlobalId,
                out int hunterUnitId,
                out uint hunterGlobalId,
                out string hunterSource);
            if (!hasHunterContext)
                hunterSource = "animal-arrow-fallback";
            else if (eligibility.Type == eChimps.CHIMP_TYPE_CHICKEN)
            {
                long timestamp = Stopwatch.GetTimestamp();
                hunterLineOfSightRecovery?.RecordProjectileSpawn(hunterUnitId, timestamp);
                hunterVisibilityDiagnostic?.RecordProjectileSpawn(
                    hunterUnitId,
                    args.AttackedUnitId,
                    eligibility.GlobalId,
                    args.ReturnValue,
                    hunterSource);
            }

            uint projectileGlobalId = 0;
            if (args.ReturnValue > 0 &&
                args.ReturnValue <= int.MaxValue &&
                GameProjectileManagerAPI.Instance.TryGetProjectileById((int)args.ReturnValue, out GameProjectile* projectile) &&
                projectile != null &&
                projectile->r_AliveState != AliveState.None &&
                projectile->r_ProjectileType == ProjectileType.ArcherArrow &&
                projectile->r_TargetUnidId == args.AttackedUnitId)
            {
                projectileGlobalId = projectile->r_GlobalId;
            }

            hunterPostShotContinuationDiagnostic?.RecordProjectileSpawn(
                hunterUnitId,
                hunterGlobalId,
                args.AttackedUnitId,
                eligibility.GlobalId,
                args.ReturnValue,
                projectileGlobalId);
        }

        private void OnProjectileDelete(ProjectileDeleteEventArgs args)
        {
            try
            {
                hunterPostShotContinuationDiagnostic?.RecordProjectileDelete(args.ProjectileId);
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters projectile-delete observation failed safely; Vanilla deletion continues: {exception}");
            }
        }

        private unsafe bool TryResolveHunterForProjectile(
            int sourceUnitId,
            int targetUnitId,
            uint targetGlobalId,
            out int hunterUnitId,
            out uint hunterGlobalId,
            out string source)
        {
            hunterUnitId = 0;
            hunterGlobalId = 0;
            source = null;

            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (IsValidUnitId(sourceUnitId) &&
                unitApi.TryGetUnitById(sourceUnitId, out GameUnit* sourceUnit) &&
                sourceUnit != null &&
                sourceUnit->r_UnitChimp == eChimps.CHIMP_TYPE_HUNTER)
            {
                hunterUnitId = sourceUnitId;
                hunterGlobalId = sourceUnit->r_GlobalId;
                source = "projectile-source";
                return true;
            }

            if (TryFindHunterTargetingPrey(targetUnitId, targetGlobalId, out hunterUnitId, out hunterGlobalId))
            {
                source = "live-hunter-target";
                return true;
            }

            foreach (KeyValuePair<int, HunterTargetSnapshot> pair in activeHunterTargets)
            {
                if (pair.Value.UnitId != targetUnitId ||
                    pair.Value.GlobalId != targetGlobalId ||
                    !unitApi.TryGetUnitById(pair.Key, out GameUnit* hunter) ||
                    hunter == null ||
                    hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER)
                {
                    continue;
                }

                hunterUnitId = pair.Key;
                hunterGlobalId = hunter->r_GlobalId;
                source = "cached-hunter-target";
                return true;
            }

            return false;
        }

        private unsafe bool TryFindHunterTargetingPrey(
            int targetUnitId,
            uint targetGlobalId,
            out int hunterUnitId,
            out uint hunterGlobalId)
        {
            hunterUnitId = 0;
            hunterGlobalId = 0;

            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            if (units._array == null || units.Length == 0)
                return false;

            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER)
                {
                    continue;
                }

                byte* hunterBytes = (byte*)unit;
                if (*(ushort*)(hunterBytes + 0x39A) != targetUnitId ||
                    *(uint*)(hunterBytes + 0x39C) != targetGlobalId)
                {
                    continue;
                }

                hunterUnitId = index + 1;
                hunterGlobalId = unit->r_GlobalId;
                return true;
            }

            return false;
        }

        private unsafe bool TryGetObservableHunterProjectileTarget(
            ProjectileSpawnEventArgs args,
            out PreyEligibility eligibility)
        {
            eligibility = default;
            if (!settings.EnableMod ||
                args.ProjectileType != ProjectileType.ArcherArrow ||
                !IsValidUnitId(args.AttackedUnitId) ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(args.AttackedUnitId, out GameUnit* target) ||
                target == null ||
                !TryGetPreyEligibility(args.AttackedUnitId, target, out eligibility))
            {
                return false;
            }

            return eligibility.KnownAnimal &&
                eligibility.RuntimeHuntingEnabled &&
                eligibility.OwnerAllowed &&
                eligibility.AliveState == (short)AliveState.IsAlive &&
                eligibility.FlagsAllowed &&
                (eligibility.Reservation == 0 || eligibility.Reservation == 2) &&
                eligibility.CorpseFlag == 0;
        }
    }
}
