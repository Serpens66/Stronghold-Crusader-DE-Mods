using System;

namespace BugfixesAndQoL
{
    internal static class KeepFlagRotationTests
    {
        internal static void Run(Action<bool, string> outerCheck)
        {
            int checks = 0;
            Action<bool, string> check = (condition, message) =>
            {
                checks++;
                outerCheck(condition, message);
            };
            CheckPositions(check, 10, 20, 7, 0, 135, 160, 135, 160, false);
            CheckPositions(check, 10, 20, 7, 2, 135, 160, 80, 160, true);
            CheckPositions(check, 10, 20, 7, 4, 135, 160, 80, 215, true);
            CheckPositions(check, 10, 20, 7, 6, 135, 160, 135, 215, true);
            CheckPositions(check, 10, 20, 7, 15, 135, 160, 135, 160, false);
            CheckPositions(check, 10, 20, 11, 0, 167, 160, 167, 160, false);
            CheckPositions(check, 10, 20, 11, 2, 167, 160, 80, 160, true);
            CheckPositions(check, 10, 20, 11, 4, 167, 160, 80, 247, true);
            CheckPositions(check, 10, 20, 11, 6, 167, 160, 167, 247, true);
            CheckPositions(check, 10, 20, 11, 15, 167, 160, 167, 160, false);

            CheckPositions(check, 532, 281, 7, 4, 4311, 2248, 4256, 2303, true);
            CheckPositions(check, 248, 274, 7, 6, 2039, 2192, 2039, 2247, true);
            CheckPositions(check, 254, 587, 7, 2, 2087, 4696, 2032, 4696, true);
            CheckPositions(check, 654, 455, 7, 2, 5287, 3640, 5232, 3640, true);
            CheckPositions(check, 423, 667, 7, 0, 3439, 5336, 3439, 5336, false);
            CheckPositions(check, 254, 580, 7, 15, 2087, 4640, 2087, 4640, false);

            CheckObserved(check, 10, 20, 7, 2, 135, 160, 80, 160);
            CheckObserved(check, 10, 20, 7, 2, 135, 167, 87, 160);
            CheckObserved(check, 10, 20, 7, 2, 128, 167, 87, 167);
            CheckObserved(check, 10, 20, 7, 2, 128, 160, 80, 167);
            CheckObserved(check, 10, 20, 7, 6, 135, 160, 135, 215);
            CheckObserved(check, 10, 20, 7, 6, 135, 167, 128, 215);
            CheckObserved(check, 10, 20, 7, 6, 128, 167, 128, 208);
            CheckObserved(check, 10, 20, 7, 6, 128, 160, 135, 208);
            CheckObserved(check, 10, 20, 11, 2, 160, 167, 87, 167);
            CheckObserved(check, 10, 20, 11, 6, 160, 167, 160, 240);
            CheckObserved(check, 280, 407, 7, 6, 2288, 3263, 2288, 3304);
            CheckObserved(check, 522, 398, 7, 2, 4224, 3191, 4183, 3191);

            check(!KeepFlagRotationPolicy.TryGetCorrectedPosition(
                10, 20, 7, 2, new KeepFlagPosition(134, 160),
                out _, out _, out _, out _), "keep flag rejects unsupported Vanilla micro anchors");
            check(!KeepFlagRotationPolicy.TryResolveObservedPosition(
                10, 20, 7, 2, new KeepFlagPosition(120, 160),
                out _, out _), "keep flag rejects positions outside Vanilla and corrected corners");

            check(IsMainFlag(), "keep main flag accepts a stationary source/target pair");
            check(!IsMainFlag(targetX: 4312), "keep main flag rejects a moving target");
            check(!IsMainFlag(targetElevation: 109), "keep main flag rejects different elevations");
            check(!IsMainFlag(playerSourceId: 3), "keep main flag rejects mismatched players");
            check(!IsMainFlag(unitPlayerSourceId: 9), "keep main flag rejects invalid players");
            check(!IsMainFlag(sourceUnitId: 42), "keep main flag rejects unit-owned Flag3 projectiles");
            check(!IsMainFlag(attackedUnitId: 42), "keep main flag rejects attacking Flag3 projectiles");

            var vanilla = new KeepFlagPosition(4311, 2248);
            check(KeepFlagRotationPolicy.MatchesVanillaPosition(4311, 2248, 4311, 2248, vanilla),
                "keep main flag accepts the exact Vanilla source and target");
            check(!KeepFlagRotationPolicy.MatchesVanillaPosition(4310, 2248, 4310, 2248, vanilla),
                "keep main flag rejects a deviating source");
            check(!KeepFlagRotationPolicy.MatchesVanillaPosition(4311, 2248, 4310, 2248, vanilla),
                "keep main flag rejects a deviating target");

            check(KeepFlagRotationPolicy.ExpectedScale(1) == 7, "Keep1 scale is seven");
            check(KeepFlagRotationPolicy.ExpectedScale(2) == 7, "Keep2 scale is seven");
            check(KeepFlagRotationPolicy.ExpectedScale(3) == 11, "Keep3 scale is eleven");
            check(KeepFlagRotationPolicy.ExpectedScale(4) == 0, "unknown Keep scale is rejected");
            check(!KeepFlagRotationPolicy.TryGetPositions(10, 20, 7, 1, out _, out _),
                "odd Keep orientation is rejected");
            check(!KeepFlagRotationPolicy.TryGetPositions(10, 20, 0, 0, out _, out _),
                "zero Keep scale is rejected");
            check(KeepFlagRotationPolicy.TryNormalizeOrientation(15, out int normalized) && normalized == 0,
                "Keep sentinel 15 normalizes to South");
            check(KeepFlagRotationPolicy.DescribeDirection(0) == "South", "orientation 0 is South");
            check(KeepFlagRotationPolicy.DescribeDirection(2) == "East", "orientation 2 is East");
            check(KeepFlagRotationPolicy.DescribeDirection(4) == "North", "orientation 4 is North");
            check(KeepFlagRotationPolicy.DescribeDirection(6) == "West", "orientation 6 is West");
            check(KeepFlagRotationPolicy.DescribeCorner(0) == "NorthEast", "orientation 0 targets NorthEast");
            check(KeepFlagRotationPolicy.DescribeCorner(2) == "NorthWest", "orientation 2 targets NorthWest");
            check(KeepFlagRotationPolicy.DescribeCorner(4) == "SouthWest", "orientation 4 targets SouthWest");
            check(KeepFlagRotationPolicy.DescribeCorner(6) == "SouthEast", "orientation 6 targets SouthEast");

            KeepFlagPosition tile = KeepFlagRotationPolicy.ToTilePosition(new KeepFlagPosition(2039, 2247));
            check(tile.X == 254 && tile.Y == 280, "keep flag converts micro coordinates to tiles");
            check(KeepFlagRotationPolicy.TryGetProjectileIdFromSpanIndex(364, out int projectileId) &&
                  projectileId == 364, "keep flag projectile ID equals its span index");
            check(!KeepFlagRotationPolicy.TryGetProjectileIdFromSpanIndex(0, out projectileId) &&
                  projectileId == 0, "keep flag skips reserved projectile slot zero");
            check(KeepFlagRotationPolicy.TryResolveUniqueProjectileId(1, 364, out projectileId) &&
                  projectileId == 364, "keep flag accepts a unique projectile match");
            check(!KeepFlagRotationPolicy.TryResolveUniqueProjectileId(0, 0, out projectileId),
                "keep flag rejects a missing projectile match");
            check(!KeepFlagRotationPolicy.TryResolveUniqueProjectileId(2, 365, out projectileId) &&
                  projectileId == 0, "keep flag rejects ambiguous projectile matches");
            outerCheck(checks == 169, $"keep-flag regression suite retains all 169 checks (actual {checks})");
        }

        private static bool IsMainFlag(
            int sourceUnitId = 0,
            int playerSourceId = 2,
            int unitPlayerSourceId = 2,
            int sourceX = 4311,
            int sourceY = 2248,
            int sourceElevation = 108,
            int targetX = 4311,
            int targetY = 2248,
            int targetElevation = 108,
            int attackedUnitId = 0) =>
            KeepFlagRotationPolicy.IsStationaryMainFlag(
                sourceUnitId,
                playerSourceId,
                unitPlayerSourceId,
                sourceX,
                sourceY,
                sourceElevation,
                targetX,
                targetY,
                targetElevation,
                attackedUnitId);

        private static void CheckPositions(
            Action<bool, string> check,
            int originX,
            int originY,
            int scale,
            int orientation,
            int vanillaX,
            int vanillaY,
            int correctedX,
            int correctedY,
            bool expectsMutation)
        {
            bool resolved = KeepFlagRotationPolicy.TryGetPositions(
                originX,
                originY,
                scale,
                orientation,
                out KeepFlagPosition vanilla,
                out KeepFlagPosition corrected);
            check(resolved, $"keep flag resolves orientation {orientation}, scale {scale}");
            check(vanilla.X == vanillaX && vanilla.Y == vanillaY,
                $"keep flag Vanilla position for orientation {orientation}, scale {scale}");
            check(corrected.X == correctedX && corrected.Y == correctedY,
                $"keep flag corrected position for orientation {orientation}, scale {scale}");
            check(((vanilla.X != corrected.X || vanilla.Y != corrected.Y) == expectsMutation),
                $"keep flag mutation expectation for orientation {orientation}, scale {scale}");
        }

        private static void CheckObserved(
            Action<bool, string> check,
            int originX,
            int originY,
            int scale,
            int orientation,
            int vanillaX,
            int vanillaY,
            int correctedX,
            int correctedY)
        {
            var observed = new KeepFlagPosition(vanillaX, vanillaY);
            bool resolved = KeepFlagRotationPolicy.TryGetCorrectedPosition(
                originX,
                originY,
                scale,
                orientation,
                observed,
                out KeepFlagPosition vanilla,
                out KeepFlagPosition corrected,
                out int microX,
                out int microY);
            check(resolved, $"keep flag accepts Vanilla view variant {observed}");
            check(vanilla.X == vanillaX && vanilla.Y == vanillaY,
                $"keep flag preserves resolved Vanilla variant {observed}");
            check(corrected.X == correctedX && corrected.Y == correctedY,
                $"keep flag rotates Vanilla view variant {observed}");
            check((microX == 0 || microX == 7) && (microY == 0 || microY == 7),
                $"keep flag recognizes native micro anchor ({microX},{microY})");
            bool delayedResolved = KeepFlagRotationPolicy.TryResolveObservedPosition(
                originX,
                originY,
                scale,
                orientation,
                corrected,
                out KeepFlagPosition delayedVanilla,
                out KeepFlagPosition delayedCorrected);
            check(delayedResolved, $"keep flag delayed scan resolves corrected position {corrected}");
            check(delayedVanilla.X == vanillaX && delayedVanilla.Y == vanillaY &&
                  delayedCorrected.X == correctedX && delayedCorrected.Y == correctedY,
                $"keep flag delayed scan retains pair for {observed}");
        }
    }
}
