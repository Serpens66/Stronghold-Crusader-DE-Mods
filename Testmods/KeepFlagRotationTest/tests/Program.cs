using KeepFlagRotationTest;
using System;

namespace KeepFlagRotationTest.Tests
{
    internal static class Program
    {
        private static int checks;

        private static int Main()
        {
            try
            {
                CheckScaleSevenPositions();
                CheckScaleElevenPositions();
                CheckConcreteGameRegression();
                CheckMainFlagGuards();
                CheckPolicyGuards();
                CheckCoordinateAndProjectileContracts();
                Console.WriteLine($"KeepFlagRotationTest tests passed: {checks} checks.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void CheckScaleSevenPositions()
        {
            CheckPosition(10, 20, 7, 0, 135, 160, 135, 160, false);
            CheckPosition(10, 20, 7, 2, 135, 160, 80, 160, true);
            CheckPosition(10, 20, 7, 4, 135, 160, 80, 215, true);
            CheckPosition(10, 20, 7, 6, 135, 160, 135, 215, true);
            CheckPosition(10, 20, 7, 15, 135, 160, 135, 160, false);
        }

        private static void CheckScaleElevenPositions()
        {
            CheckPosition(10, 20, 11, 0, 167, 160, 167, 160, false);
            CheckPosition(10, 20, 11, 2, 167, 160, 80, 160, true);
            CheckPosition(10, 20, 11, 4, 167, 160, 80, 247, true);
            CheckPosition(10, 20, 11, 6, 167, 160, 167, 247, true);
            CheckPosition(10, 20, 11, 15, 167, 160, 167, 160, false);
        }

        private static void CheckConcreteGameRegression()
        {
            CheckPosition(532, 281, 7, 4, 4311, 2248, 4256, 2303, true);
            CheckPosition(248, 274, 7, 6, 2039, 2192, 2039, 2247, true);
            CheckPosition(254, 587, 7, 2, 2087, 4696, 2032, 4696, true);
            CheckPosition(654, 455, 7, 2, 5287, 3640, 5232, 3640, true);
            CheckPosition(423, 667, 7, 0, 3439, 5336, 3439, 5336, false);
            CheckPosition(254, 580, 7, 15, 2087, 4640, 2087, 4640, false);
        }

        private static void CheckMainFlagGuards()
        {
            Check(IsMainFlag(), "stationary target==source main flag rejected");
            Check(!IsMainFlag(targetX: 4312), "moving target accepted");
            Check(!IsMainFlag(targetElevation: 109), "different target elevation accepted");
            Check(!IsMainFlag(playerSourceId: 3), "mismatched player accepted");
            Check(!IsMainFlag(unitPlayerSourceId: 9), "invalid player accepted");
            Check(!IsMainFlag(sourceUnitId: 42), "unit-owned Flag3 accepted");
            Check(!IsMainFlag(attackedUnitId: 42), "attacking Flag3 accepted");

            var vanilla = new FlagPosition(4311, 2248);
            Check(KeepFlagRotationPolicy.MatchesVanillaPosition(4311, 2248, 4311, 2248, vanilla),
                "exact vanilla source/target rejected");
            Check(!KeepFlagRotationPolicy.MatchesVanillaPosition(4310, 2248, 4310, 2248, vanilla),
                "deviating source/target accepted");
            Check(!KeepFlagRotationPolicy.MatchesVanillaPosition(4311, 2248, 4310, 2248, vanilla),
                "deviating target accepted");
        }

        private static void CheckPolicyGuards()
        {
            Check(KeepFlagRotationPolicy.ExpectedScale(1) == 7, "Keep1 scale");
            Check(KeepFlagRotationPolicy.ExpectedScale(2) == 7, "Keep2 scale");
            Check(KeepFlagRotationPolicy.ExpectedScale(3) == 11, "Keep3 scale");
            Check(KeepFlagRotationPolicy.ExpectedScale(4) == 0, "unknown keep scale");
            Check(!KeepFlagRotationPolicy.TryGetPositions(10, 20, 7, 1, out _, out _), "odd orientation accepted");
            Check(!KeepFlagRotationPolicy.TryGetPositions(10, 20, 0, 0, out _, out _), "zero scale accepted");
            Check(KeepFlagRotationPolicy.TryNormalizeOrientation(15, out int normalized) && normalized == 0,
                "sentinel 15 was not normalized to zero");
            Check(KeepFlagRotationPolicy.DescribeDirection(0) == "South", "orientation 0 direction");
            Check(KeepFlagRotationPolicy.DescribeDirection(2) == "East", "orientation 2 direction");
            Check(KeepFlagRotationPolicy.DescribeDirection(4) == "North", "orientation 4 direction");
            Check(KeepFlagRotationPolicy.DescribeDirection(6) == "West", "orientation 6 direction");
            Check(KeepFlagRotationPolicy.DescribeCorner(0) == "NorthEast", "orientation 0 corner");
            Check(KeepFlagRotationPolicy.DescribeCorner(2) == "NorthWest", "orientation 2 corner");
            Check(KeepFlagRotationPolicy.DescribeCorner(4) == "SouthWest", "orientation 4 corner");
            Check(KeepFlagRotationPolicy.DescribeCorner(6) == "SouthEast", "orientation 6 corner");
        }

        private static void CheckCoordinateAndProjectileContracts()
        {
            FlagPosition southEastMicro = new FlagPosition(2039, 2247);
            FlagPosition southEastTile = KeepFlagRotationPolicy.ToTilePosition(southEastMicro);
            Check(southEastTile.X == 254 && southEastTile.Y == 280,
                $"micro-to-tile conversion was {southEastTile}");

            Check(KeepFlagRotationPolicy.TryGetProjectileIdFromSpanIndex(364, out int projectileId) &&
                projectileId == 364, "projectile span index was offset");
            Check(!KeepFlagRotationPolicy.TryGetProjectileIdFromSpanIndex(0, out projectileId) &&
                projectileId == 0, "reserved projectile slot zero accepted");
            Check(KeepFlagRotationPolicy.TryResolveUniqueProjectileId(1, 364, out projectileId) &&
                projectileId == 364, "unique projectile match rejected");
            Check(!KeepFlagRotationPolicy.TryResolveUniqueProjectileId(0, 0, out projectileId),
                "missing projectile match accepted");
            Check(!KeepFlagRotationPolicy.TryResolveUniqueProjectileId(2, 365, out projectileId) &&
                projectileId == 0, "ambiguous projectile match accepted");
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
            int attackedUnitId = 0)
        {
            return KeepFlagRotationPolicy.IsStationaryMainFlag(
                sourceUnitId, playerSourceId, unitPlayerSourceId,
                sourceX, sourceY, sourceElevation,
                targetX, targetY, targetElevation, attackedUnitId);
        }

        private static void CheckPosition(
            int originX, int originY, int scale, int orientation,
            int vanillaX, int vanillaY, int correctedX, int correctedY,
            bool expectsMutation)
        {
            Check(KeepFlagRotationPolicy.TryGetPositions(
                originX, originY, scale, orientation,
                out FlagPosition vanilla, out FlagPosition corrected),
                $"orientation {orientation}, scale {scale} rejected");
            Check(vanilla.X == vanillaX && vanilla.Y == vanillaY,
                $"orientation {orientation}, scale {scale} vanilla was {vanilla}");
            Check(corrected.X == correctedX && corrected.Y == correctedY,
                $"orientation {orientation}, scale {scale} corrected was {corrected}");
            bool mutates = vanilla.X != corrected.X || vanilla.Y != corrected.Y;
            Check(mutates == expectsMutation,
                $"orientation {orientation}, scale {scale} mutation expectation was {mutates}");
        }

        private static void Check(bool condition, string message)
        {
            checks++;
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
