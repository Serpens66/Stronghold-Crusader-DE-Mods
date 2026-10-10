using System;

namespace BugfixesAndQoL
{
    public static class GatehouseTargetMarkerHeightTests
    {
        public static int Run()
        {
            int cases = 0;
            // Native builder uses 6-(terrain+20), managed negates it. After correction it must
            // equal the unit roof (terrain+40) minus the intentional six-unit marker offset.
            foreach (int terrain in new[] { 0, 90, 100, 255 })
                foreach (int type in new[] { 45, 46 })
                    for (int image = 82; image <= 89; image++)
                    {
                        int y = terrain + 20 - 6;
                        Require(GatehouseTargetMarkerHeightPolicy.TryAdjust(true, 107, image, 12, false,
                            true, false, true, 0x100, type, ref y), "gatehouse marker was rejected");
                        Require(y == terrain + 40 - 6, "wrong height sign or doubled correction");
                        // Repeated cached updates start from the native input again, not last frame's height.
                        y = terrain + 20 - 6;
                        GatehouseTargetMarkerHeightPolicy.TryAdjust(true, 107, image, 12, false,
                            true, false, true, 0x100, type, ref y);
                        Require(y == terrain + 40 - 6, "cached marker height drift");
                        cases++;
                    }
            // Every other native building type, including all keeps and destroyed towers, stays intact.
            for (int type = 0; type <= 109; type++)
                if (type != 45 && type != 46)
                {
                    ExpectUnchanged(true, 107, 83, 12, false, true, false, true, 0x100, type, 104);
                    cases++;
                }
            // Elevated wins over wall in Vanilla's marker branch; even a mixed-flag gate must stay intact.
            foreach (uint flags in new uint[] { 0, 0x400, 0x10000000, 0x10000100 })
            { ExpectUnchanged(true, 107, 83, 12, false, true, false, true, flags, 45, 104); cases++; }
            ExpectUnchanged(false, 107, 83, 12, false, true, false, true, 0x100, 45, 104);
            ExpectUnchanged(true, 106, 83, 12, false, true, false, true, 0x100, 45, 104);
            ExpectUnchanged(true, 107, 81, 12, false, true, false, true, 0x100, 45, 104);
            ExpectUnchanged(true, 107, 90, 12, false, true, false, true, 0x100, 45, 104);
            ExpectUnchanged(true, 107, 83, 10, false, true, false, true, 0x100, 45, 104);
            ExpectUnchanged(true, 107, 83, 12, true, true, false, true, 0x100, 45, 104);
            ExpectUnchanged(true, 107, 83, 12, false, false, false, true, 0x100, 45, 104);
            ExpectUnchanged(true, 107, 83, 12, false, true, true, true, 0x100, 45, 104);
            ExpectUnchanged(true, 107, 83, 12, false, true, false, true, 0x100, 45, int.MaxValue - 19);
            cases += 9;
            for (int a = 0; a < 2; a++)
                for (int b = 0; b < 2; b++)
                {
                    int y = 104;
                    bool adjusted = GatehouseTargetMarkerHeightPolicy.TryAdjust(true, 107, 83, 12,
                        false, true, a != 0, b != 0, 0x100, 45, ref y);
                    Require(adjusted == (a == 0 || b == 0), "incorrect flattened-view branch");
                    Require(y == (adjusted ? 124 : 104), "flattened-view output mismatch");
                    cases++;
                }
            // The click producer uses 106-countdown, countdown 16..1, horizontal offset 10.
            for (int countdown = 16; countdown >= 1; countdown--)
            {
                int image = 106 - countdown;
                foreach (int terrain in new[] { 0, 90, 100, 255 })
                    foreach (int type in new[] { 45, 46 })
                    {
                        int y = terrain + 20 - 6;
                        Require(GatehouseTargetMarkerHeightPolicy.TryAdjust(true, 107, image, 10, false,
                            true, false, true, 0x100, type, ref y) && y == terrain + 40 - 6,
                            "click height/sign mismatch");
                        cases++;
                    }
                for (int type = 0; type <= 109; type++)
                    if (type != 45 && type != 46)
                    { ExpectUnchanged(true, 107, image, 10, false, true, false, true, 0x100, type, 104); cases++; }
                foreach (uint flags in new uint[] { 0, 0x400, 0x10000000, 0x10000100 })
                { ExpectUnchanged(true, 107, image, 10, false, true, false, true, flags, 45, 104); cases++; }
                ExpectUnchanged(false, 107, image, 10, false, true, false, true, 0x100, 45, 104);
                ExpectUnchanged(true, 106, image, 10, false, true, false, true, 0x100, 45, 104);
                ExpectUnchanged(true, 107, image, 12, false, true, false, true, 0x100, 45, 104);
                ExpectUnchanged(true, 107, image, 10, true, true, false, true, 0x100, 45, 104);
                ExpectUnchanged(true, 107, image, 10, false, true, false, true, 0x100, 45, int.MaxValue - 19);
                cases += 5;
                for (int detailed = 0; detailed < 2; detailed++)
                    for (int flattened = 0; flattened < 2; flattened++)
                        for (int allowed = 0; allowed < 2; allowed++)
                        {
                            int y = 104;
                            bool expected = detailed == 1 && !(flattened == 1 && allowed == 1);
                            bool adjusted = GatehouseTargetMarkerHeightPolicy.TryAdjust(true, 107, image, 10,
                                false, detailed != 0, flattened != 0, allowed != 0, 0x100, 45, ref y);
                            Require(adjusted == expected && y == (expected ? 124 : 104), "click render-state mismatch");
                            cases++;
                        }
            }
            foreach (int image in new[] { 82, 89, 106 })
            { ExpectUnchanged(true, 107, image, 10, false, true, false, true, 0x100, 45, 104); cases++; }
            return cases;
        }

        private static void ExpectUnchanged(bool enabled, int file, int image, int horizontalOffset, bool hiMode,
            bool detailed, bool flattened, bool flattenAllowed, uint flags, int type, int original)
        {
            int y = original;
            Require(!GatehouseTargetMarkerHeightPolicy.TryAdjust(enabled, file, image, horizontalOffset, hiMode,
                detailed, flattened, flattenAllowed, flags, type, ref y) && y == original,
                "excluded rendering input changed");
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
