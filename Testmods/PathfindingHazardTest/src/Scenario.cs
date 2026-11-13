using System;
using System.IO;

namespace PathfindingHazardTest
{
    internal sealed class Scenario
    {
        internal string Name, SavePath;
        internal int ActorId, TargetX, TargetY, HazardX, HazardY, WaitSeconds, MaximumTicks;
        internal bool Plague => Name.StartsWith("plague-", StringComparison.Ordinal);
        internal bool Escape => Name.EndsWith("-escape", StringComparison.Ordinal);
        internal bool Unlimited => Name.EndsWith("-wait-unlimited", StringComparison.Ordinal);
        internal static bool Owns(string name) => name == "fire-escape" || name == "plague-escape" ||
            name == "fire-wait-bounded" || name == "plague-wait-bounded" ||
            name == "fire-wait-unlimited" || name == "plague-wait-unlimited";
        internal void Validate()
        {
            if (!Owns(Name)) throw new ArgumentException("Unowned scenario.");
            if (string.IsNullOrWhiteSpace(SavePath) || !Path.IsPathRooted(SavePath) ||
                !string.Equals(Path.GetPathRoot(SavePath), Path.GetPathRoot(Path.GetFullPath(SavePath)), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetExtension(SavePath), ".sav", StringComparison.OrdinalIgnoreCase) || !File.Exists(SavePath))
                throw new ArgumentException("Supply an existing absolute prepared .sav path.");
            // Capacity is a live manager contract, checked by UnitAccess at activation, not a fixed ID limit.
            if (ActorId < 1 || WaitSeconds < 1 || WaitSeconds > 1800 ||
                MaximumTicks < WaitSeconds * 40 + 80 || MaximumTicks > 72000)
                throw new ArgumentException("ActorId/WaitSeconds/MaximumTicks invalid or observation shorter than the finite deadline.");
            foreach (int coordinate in new[] { TargetX, TargetY, HazardX, HazardY })
                if (coordinate < 0 || coordinate >= 800) throw new ArgumentException("Explicit target/hazard coordinates 0..799 required.");
        }
    }
}
