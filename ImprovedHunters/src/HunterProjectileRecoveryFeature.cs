using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Projectiles;
using SHCDESE.EventAPI.Units;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Zhuqiaomon.Assembly.Stateful;
using Zhuqiaomon.Memory;

namespace ImprovedHunters
{
    internal sealed partial class ImprovedHuntersRuntime
    {
        // Correlates live Hunter arrows and applies the optional validated Vanilla ranged-damage recovery.
        private unsafe void OnProjectileSpawn(ProjectileSpawnEventArgs args)
        {
            if (!TryGetCompensableProjectileTarget(args, out _, out PreyEligibility eligibility))
                return;

            // Hunter arrows do not always report the hunter as SourceUnitId. Use
            // several weak signals and fall back to the target intent if needed.
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

            if (settings.ReliableHunterProjectiles)
            {
                QueuePendingHunterShotIntent(
                    hunterUnitId,
                    hunterGlobalId,
                    args.AttackedUnitId,
                    eligibility.GlobalId,
                    eligibility.Type,
                    hunterSource,
                    args.ReturnValue,
                    projectileGlobalId);
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
                TryApplyHunterProjectileDamageOnDelete(args.ProjectileId);
            }
            catch (Exception exception)
            {
                LogHunterProjectileDiagnostic(
                    $"Improved Hunters projectile-delete compensation failed; Vanilla deletion continues: {exception}");
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
                ushort hunterTargetUnitId = *(ushort*)(hunterBytes + 0x39A);
                uint hunterTargetGlobalId = *(uint*)(hunterBytes + 0x39C);
                if (hunterTargetUnitId != targetUnitId ||
                    hunterTargetGlobalId != targetGlobalId)
                {
                    continue;
                }

                hunterUnitId = index + 1;
                hunterGlobalId = unit->r_GlobalId;
                return true;
            }

            return false;
        }

        private unsafe bool TryGetCompensableProjectileTarget(
            ProjectileSpawnEventArgs args,
            out GameUnit* target,
            out PreyEligibility eligibility)
        {
            target = null;
            eligibility = default;

            if (!settings.EnableMod ||
                args.ProjectileType != ProjectileType.ArcherArrow ||
                !IsValidUnitId(args.AttackedUnitId) ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(args.AttackedUnitId, out target) ||
                target == null)
            {
                return false;
            }

            return IsCompensableHunterPrey(args.AttackedUnitId, target, out eligibility);
        }

        private unsafe void QueuePendingHunterShotIntent(
            int hunterUnitId,
            uint hunterGlobalId,
            int targetUnitId,
            uint targetGlobalId,
            eChimps targetType,
            string hunterSource,
            long spawnReturnValue,
            uint projectileGlobalId)
        {
            long timestamp = Stopwatch.GetTimestamp();
            ushort projectileX = 0;
            ushort projectileY = 0;
            if (spawnReturnValue > 0 &&
                spawnReturnValue <= int.MaxValue &&
                GameProjectileManagerAPI.Instance.TryGetProjectileById((int)spawnReturnValue, out GameProjectile* projectile) &&
                projectile != null &&
                projectile->r_GlobalId == projectileGlobalId)
            {
                projectileX = projectile->r_CurrentTileX;
                projectileY = projectile->r_CurrentTileY;
            }

            HunterShotIntentKey key = new HunterShotIntentKey(
                targetUnitId,
                targetGlobalId,
                projectileGlobalId,
                spawnReturnValue);
            PendingHunterShotIntent intent = new PendingHunterShotIntent(
                hunterUnitId,
                hunterGlobalId,
                targetUnitId,
                targetGlobalId,
                targetType,
                timestamp,
                timestamp + HunterProjectileIntentLifetime,
                hunterSource,
                spawnReturnValue,
                projectileGlobalId,
                projectileX,
                projectileY,
                timestamp);

            bool updatedExisting = pendingHunterShotIntents.ContainsKey(key);
            pendingHunterShotIntents[key] = intent;

            LogHunterProjectileDiagnostic(
                $"Improved Hunters hunter shot intent queued: hunter={hunterUnitId}, target={targetUnitId}/{targetType}, " +
                $"targetGlobalId={targetGlobalId}, lifetimeSeconds={HunterProjectileIntentLifetime / Stopwatch.Frequency}, " +
                $"hunterSource={hunterSource}, projectile={spawnReturnValue}/{projectileGlobalId}, updated={updatedExisting}.");
        }

        private unsafe void TryApplyHunterProjectileDamageDuringFlight(long timestamp)
        {
            if (!settings.EnableMod ||
                !settings.ReliableHunterProjectiles ||
                pendingHunterShotIntents.Count == 0)
                return;

            List<HunterShotIntentKey> keys = new List<HunterShotIntentKey>();
            foreach (KeyValuePair<HunterShotIntentKey, PendingHunterShotIntent> pair in pendingHunterShotIntents)
                keys.Add(pair.Key);

            for (int index = 0; index < keys.Count; index++)
            {
                HunterShotIntentKey key = keys[index];
                if (!pendingHunterShotIntents.TryGetValue(key, out PendingHunterShotIntent intent))
                    continue;

                if (!TryGetMatchingProjectile(intent, out GameProjectile* projectile) ||
                    projectile->r_AliveState != AliveState.IsAlive)
                {
                    continue;
                }

                if (projectile->r_CurrentTileX != intent.LastProjectileX ||
                    projectile->r_CurrentTileY != intent.LastProjectileY)
                {
                    intent = intent.WithProjectileObservation(
                        projectile->r_CurrentTileX,
                        projectile->r_CurrentTileY,
                        timestamp);
                    pendingHunterShotIntents[key] = intent;
                }

                if (intent.ActiveDamageAttempts >= MaxHunterProjectileDamageAttempts ||
                    timestamp < intent.NextDamageAttemptAt ||
                    timestamp - intent.CreatedAt < HunterProjectileMinimumFlightTime)
                {
                    continue;
                }

                GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
                if (!unitApi.TryGetUnitById(intent.TargetUnitId, out GameUnit* target) ||
                    target == null ||
                    target->r_GlobalId != intent.TargetGlobalId ||
                    target->r_CurrentHealth == 0)
                {
                    pendingHunterShotIntents.Remove(key);
                    continue;
                }

                int distanceToTarget = Math.Max(
                    Math.Abs((int)projectile->r_CurrentTileX - target->r_CurrentWorldPositionX),
                    Math.Abs((int)projectile->r_CurrentTileY - target->r_CurrentWorldPositionY));
                bool nearTarget = distanceToTarget <= HunterProjectileNearTargetDistance;
                bool stalled = timestamp - intent.LastProjectileMovementAt >= HunterProjectileStallInterval;
                if (!nearTarget && !stalled)
                    continue;

                TryApplyHunterProjectileDamage(
                    key,
                    intent,
                    nearTarget ? "active-near-target" : "active-stalled",
                    timestamp,
                    allowRetry: true);
            }
        }

        private void RunHunterProjectileCompensation(long timestamp)
        {
            try
            {
                TryApplyHunterProjectileDamageDuringFlight(timestamp);
            }
            catch (Exception exception)
            {
                if (!hunterProjectileCompensationFailureLogged)
                {
                    hunterProjectileCompensationFailureLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Improved Hunters active-flight ranged compensation failed safely; " +
                        $"the native scan and Vanilla continue: {exception}");
                }
            }

            try
            {
                ResolvePendingHunterShotIntents(timestamp);
            }
            catch (Exception exception)
            {
                if (!hunterProjectileCleanupFailureLogged)
                {
                    hunterProjectileCleanupFailureLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Improved Hunters projectile-intent cleanup failed safely; " +
                        $"the native scan and Vanilla continue: {exception}");
                }
            }
        }

        private unsafe void TryApplyHunterProjectileDamageOnDelete(long projectileId)
        {
            if (!settings.EnableMod ||
                !settings.ReliableHunterProjectiles ||
                projectileId <= 0 ||
                pendingHunterShotIntents.Count == 0)
                return;

            HunterShotIntentKey matchedKey = default;
            PendingHunterShotIntent matchedIntent = default;
            bool found = false;
            foreach (KeyValuePair<HunterShotIntentKey, PendingHunterShotIntent> pair in pendingHunterShotIntents)
            {
                if (pair.Value.SpawnReturnValue != projectileId)
                    continue;

                matchedKey = pair.Key;
                matchedIntent = pair.Value;
                found = true;
                break;
            }

            if (!found)
                return;

            TryApplyHunterProjectileDamage(
                matchedKey,
                matchedIntent,
                "projectile-delete",
                Stopwatch.GetTimestamp(),
                allowRetry: false);
        }

        private unsafe void TryApplyHunterProjectileDamage(
            HunterShotIntentKey key,
            PendingHunterShotIntent intent,
            string trigger,
            long timestamp,
            bool allowRetry)
        {
            pendingHunterShotIntents.Remove(key);
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (!unitApi.TryGetUnitById(intent.TargetUnitId, out GameUnit* target) ||
                target == null ||
                target->r_GlobalId != intent.TargetGlobalId ||
                target->r_AliveState != AliveState.IsAlive ||
                target->r_CurrentHealth == 0)
            {
                // The native hit already completed, or the slot was reused.
                return;
            }

            if (intent.HunterUnitId <= 0 ||
                !unitApi.TryGetUnitById(intent.HunterUnitId, out GameUnit* hunter) ||
                hunter == null ||
                hunter->r_GlobalId != intent.HunterGlobalId ||
                hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER ||
                hunter->r_AliveState != AliveState.IsAlive ||
                !IsCompensableHunterPrey(intent.TargetUnitId, target, out PreyEligibility eligibility) ||
                !TryGetMatchingProjectile(intent, out GameProjectile* projectile) ||
                projectile->r_PlayerSourceId != hunter->r_ControllableForPlayerId)
            {
                LogHunterProjectileDiagnostic(
                    $"Improved Hunters ranged compensation skipped: trigger={trigger}, hunter={intent.HunterUnitId}/{intent.HunterGlobalId}, " +
                    $"target={intent.TargetUnitId}/{intent.TargetGlobalId}/{intent.TargetType}, " +
                    $"projectile={intent.SpawnReturnValue}/{intent.ProjectileGlobalId}, reason=identity-or-state-validation-failed.");
                return;
            }

            // Remove before entering native code: ranged damage may synchronously
            // dispatch projectile deletion and must never re-enter this intent.
            int attempt = intent.ActiveDamageAttempts + 1;
            short projectileAliveState = (short)projectile->r_AliveState;
            ushort projectileX = projectile->r_CurrentTileX;
            ushort projectileY = projectile->r_CurrentTileY;
            bool damageApplied = unitApi.DamageUnitRanged(
                intent.TargetUnitId,
                (int)intent.SpawnReturnValue);
            bool targetIdentityValidAfter =
                unitApi.TryGetUnitById(intent.TargetUnitId, out GameUnit* targetAfter) &&
                targetAfter != null &&
                targetAfter->r_GlobalId == intent.TargetGlobalId;
            uint currentHealth = targetIdentityValidAfter ? targetAfter->r_CurrentHealth : 0;
            ushort aiState = targetIdentityValidAfter ? *(ushort*)((byte*)targetAfter + 0x2BC) : (ushort)0;
            ushort corpseFlag = targetIdentityValidAfter ? *(ushort*)((byte*)targetAfter + 0x29C) : (ushort)0;
            ushort reservation = targetIdentityValidAfter ? *(ushort*)((byte*)targetAfter + 0x448) : (ushort)0;
            bool targetKilled = targetIdentityValidAfter && currentHealth == 0;

            LogHunterProjectileDiagnostic(
                $"Improved Hunters Vanilla ranged compensation: trigger={trigger}, " +
                $"hunter={intent.HunterUnitId}/{intent.HunterGlobalId}, " +
                $"target={intent.TargetUnitId}/{intent.TargetGlobalId}/{eligibility.Type}, " +
                $"projectile={intent.SpawnReturnValue}/{intent.ProjectileGlobalId}, " +
                $"projectileAliveState={projectileAliveState}, projectilePosition={projectileX},{projectileY}, " +
                $"attempt={attempt}/{MaxHunterProjectileDamageAttempts}, damageApplied={damageApplied}, " +
                $"targetIdentityValidAfter={targetIdentityValidAfter}, targetKilled={targetKilled}, " +
                $"currentHealth={currentHealth}, aiState=0x{aiState:X}, corpseFlag={corpseFlag}, reservation={reservation}.");

            if (!targetKilled &&
                allowRetry &&
                attempt < MaxHunterProjectileDamageAttempts &&
                timestamp < intent.ExpiresAt &&
                TryGetMatchingProjectile(intent, out projectile) &&
                projectile->r_AliveState == AliveState.IsAlive)
            {
                pendingHunterShotIntents[key] = intent.WithDamageAttempt(
                    attempt,
                    timestamp + HunterProjectileRetryInterval);
            }
        }

        private unsafe bool TryGetMatchingProjectile(
            PendingHunterShotIntent intent,
            out GameProjectile* projectile)
        {
            projectile = null;
            if (intent.SpawnReturnValue <= 0 ||
                intent.SpawnReturnValue > int.MaxValue ||
                intent.ProjectileGlobalId == 0 ||
                intent.HunterUnitId <= 0)
            {
                return false;
            }

            return GameProjectileManagerAPI.Instance.TryGetProjectileById(
                    (int)intent.SpawnReturnValue,
                    out projectile) &&
                projectile != null &&
                projectile->r_AliveState != AliveState.None &&
                projectile->r_GlobalId == intent.ProjectileGlobalId &&
                projectile->r_ProjectileType == ProjectileType.ArcherArrow &&
                projectile->r_SourceUnitId == intent.HunterUnitId &&
                projectile->r_TargetUnidId == intent.TargetUnitId;
        }

        private unsafe void ResolvePendingHunterShotIntents(long timestamp)
        {
            if (pendingHunterShotIntents.Count == 0)
                return;

            List<HunterShotIntentKey> expiredKeys = null;
            foreach (KeyValuePair<HunterShotIntentKey, PendingHunterShotIntent> pair in pendingHunterShotIntents)
            {
                if (timestamp < pair.Value.ExpiresAt)
                    continue;

                if (expiredKeys == null)
                    expiredKeys = new List<HunterShotIntentKey>();

                expiredKeys.Add(pair.Key);
            }

            if (expiredKeys == null)
                return;

            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            for (int index = 0; index < expiredKeys.Count; index++)
            {
                HunterShotIntentKey key = expiredKeys[index];
                if (!pendingHunterShotIntents.TryGetValue(key, out PendingHunterShotIntent intent))
                    continue;

                pendingHunterShotIntents.Remove(key);
                bool targetStillAlive =
                    unitApi.TryGetUnitById(intent.TargetUnitId, out GameUnit* target) &&
                    target != null &&
                    target->r_GlobalId == intent.TargetGlobalId &&
                    target->r_AliveState == AliveState.IsAlive &&
                    target->r_CurrentHealth > 0;
                LogHunterProjectileDiagnostic(
                    $"Improved Hunters projectile intent expired without synthetic KillUnit: " +
                    $"hunter={intent.HunterUnitId}/{intent.HunterGlobalId}, " +
                    $"target={intent.TargetUnitId}/{intent.TargetGlobalId}/{intent.TargetType}, " +
                    $"projectile={intent.SpawnReturnValue}/{intent.ProjectileGlobalId}, " +
                    $"attempts={intent.ActiveDamageAttempts}, targetStillAlive={targetStillAlive}.");
            }
        }

        private void LogHunterProjectileDiagnostic(string message)
        {
            if (hunterProjectileDiagnosticLogs >= MaxHunterProjectileDiagnosticLogs)
                return;

            hunterProjectileDiagnosticLogs++;
            log.LogInfo($"{message} ({hunterProjectileDiagnosticLogs}/{MaxHunterProjectileDiagnosticLogs}).");

            if (hunterProjectileDiagnosticLogs == MaxHunterProjectileDiagnosticLogs)
                log.LogInfo("Improved Hunters hunter projectile diagnostic limit reached.");
        }

    }
}
