using System;
using System.Linq;

namespace PathfindingHazardTest
{
    internal enum CaseKind { Escape, WaitBounded, WaitUnlimited, ImmuneCrossing }

    // One self-generated hazard case: the fixture spawns its own actors and real fire/plague records.
    internal sealed class HazardCase
    {
        internal HazardCase(string name, bool plague, CaseKind kind) { Name = name; Plague = plague; Kind = kind; }
        internal string Name { get; }
        internal bool Plague { get; }
        internal CaseKind Kind { get; }
        internal bool Escape => Kind == CaseKind.Escape;
        internal bool Unlimited => Kind == CaseKind.WaitUnlimited;
    }

    internal static class Scenario
    {
        // The whole matrix runs in one game start; single case names run one case.
        internal const string Matrix = "hazard-matrix";
        internal static readonly HazardCase[] Cases =
        {
            new HazardCase("fire-escape", false, CaseKind.Escape),
            new HazardCase("plague-escape", true, CaseKind.Escape),
            new HazardCase("fire-wait-bounded", false, CaseKind.WaitBounded),
            new HazardCase("fire-wait-unlimited", false, CaseKind.WaitUnlimited),
            new HazardCase("plague-wait-bounded", true, CaseKind.WaitBounded),
            new HazardCase("plague-wait-unlimited", true, CaseKind.WaitUnlimited),
            new HazardCase("plague-immune-crossing", true, CaseKind.ImmuneCrossing),
        };
        internal static bool Owns(string name) => name == Matrix || Cases.Any(c => c.Name == name);
        internal static HazardCase[] Select(string name) =>
            name == Matrix ? Cases.ToArray() : Cases.Where(c => c.Name == name).ToArray();
        // Finite route wait of the bounded cases and the comparison deadline of the unlimited ones; the hazards
        // must outlast it (a pitch-free fire burns about 880..1090 ticks), so at most 15 game seconds.
        internal static void ValidateWaitSeconds(int seconds)
        {
            if (seconds < 1 || seconds > 15) throw new ArgumentException("WaitSeconds must be 1..15 game seconds.");
        }
    }
}
