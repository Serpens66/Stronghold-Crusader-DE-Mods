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
                CheckPosition(10, 20, 7, 0, 135, 160, 135, 160);
                CheckPosition(10, 20, 7, 2, 135, 167, 135, 215);
                CheckPosition(10, 20, 7, 4, 128, 167, 80, 215);
                CheckPosition(10, 20, 7, 6, 128, 160, 80, 160);
                CheckPosition(10, 20, 11, 4, 160, 167, 80, 247);
                Check(KeepFlagRotationPolicy.ExpectedScale(1) == 7, "Keep1 scale");
                Check(KeepFlagRotationPolicy.ExpectedScale(2) == 7, "Keep2 scale");
                Check(KeepFlagRotationPolicy.ExpectedScale(3) == 11, "Keep3 scale");
                Check(KeepFlagRotationPolicy.ExpectedScale(4) == 0, "unknown keep scale");
                Check(!KeepFlagRotationPolicy.TryGetPositions(10, 20, 7, 1, out _, out _), "odd orientation accepted");
                Check(!KeepFlagRotationPolicy.TryGetPositions(10, 20, 0, 0, out _, out _), "zero scale accepted");
                Console.WriteLine($"KeepFlagRotationTest tests passed: {checks} checks.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void CheckPosition(
            int originX, int originY, int scale, int orientation,
            int vanillaX, int vanillaY, int correctedX, int correctedY)
        {
            Check(KeepFlagRotationPolicy.TryGetPositions(
                originX, originY, scale, orientation,
                out FlagPosition vanilla, out FlagPosition corrected),
                $"orientation {orientation} rejected");
            Check(vanilla.X == vanillaX && vanilla.Y == vanillaY,
                $"orientation {orientation} vanilla was {vanilla}");
            Check(corrected.X == correctedX && corrected.Y == correctedY,
                $"orientation {orientation} corrected was {corrected}");
        }

        private static void Check(bool condition, string message)
        {
            checks++;
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
