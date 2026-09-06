namespace BugfixesAndQoL
{
    internal static class MoveFormationSpacingPolicy
    {
        public const int Minimum = 1;
        public const int Maximum = 4;
        public const int Default = 2;

        public static int Normalize(int value) =>
            value < Minimum || value > Maximum ? Default : value;

        public static int ResolveEffectiveSpacing(
            int vanillaSpacing,
            int configuredSpacing,
            bool overrideEnabled) =>
            overrideEnabled && vanillaSpacing >= 2 && vanillaSpacing <= Maximum
                ? Normalize(configuredSpacing)
                : vanillaSpacing;
    }

    internal enum MoveFormationSelector
    {
        Standard,
        AssassinGround
    }

    internal readonly struct MoveFormationUnitIdentity
    {
        internal MoveFormationUnitIdentity(int unitId, uint globalId)
        {
            UnitId = unitId;
            GlobalId = globalId;
        }

        internal int UnitId { get; }
        internal uint GlobalId { get; }
    }

    internal readonly struct MoveFormationSpacingAudit
    {
        internal MoveFormationSpacingAudit(
            int configuredSpacing,
            int standardCalls,
            int assassinGroundCalls,
            int overriddenCalls,
            int preservedOneCalls,
            int[] vanillaCounts,
            int[] effectiveCounts)
        {
            ConfiguredSpacing = configuredSpacing;
            StandardCalls = standardCalls;
            AssassinGroundCalls = assassinGroundCalls;
            OverriddenCalls = overriddenCalls;
            PreservedOneCalls = preservedOneCalls;
            VanillaCounts = vanillaCounts;
            EffectiveCounts = effectiveCounts;
        }

        internal int ConfiguredSpacing { get; }
        internal int StandardCalls { get; }
        internal int AssassinGroundCalls { get; }
        internal int OverriddenCalls { get; }
        internal int PreservedOneCalls { get; }
        internal int[] VanillaCounts { get; }
        internal int[] EffectiveCounts { get; }

        internal string FormatCompact(int inferredAssassinStructureCalls = 0)
        {
            var transitions = new System.Collections.Generic.List<string>(5);
            for (int vanilla = 1; vanilla <= MoveFormationSpacingPolicy.Maximum; vanilla++)
            {
                int count = VanillaCounts[vanilla];
                if (vanilla == 1)
                    count += inferredAssassinStructureCalls;
                if (count == 0)
                    continue;
                int effective = vanilla == 1 ? 1 : ConfiguredSpacing;
                transitions.Add($"{vanilla}->{effective}:{count}");
            }
            if (VanillaCounts[0] != 0)
                transitions.Add($"other:{VanillaCounts[0]}");
            return $"cfg{ConfiguredSpacing};selectors=s{StandardCalls}/a{AssassinGroundCalls}/" +
                $"w{inferredAssassinStructureCalls};transitions={string.Join("|", transitions)}";
        }
    }

    internal sealed class MoveFormationCommandSnapshot
    {
        internal MoveFormationCommandSnapshot(
            object owner,
            int tribeId,
            int targetX,
            int targetY,
            int configuredSpacing,
            MoveFormationUnitIdentity[] units)
        {
            Owner = owner;
            TribeId = tribeId;
            TargetX = targetX;
            TargetY = targetY;
            ConfiguredSpacing = MoveFormationSpacingPolicy.Normalize(configuredSpacing);
            Units = units;
        }

        internal object Owner { get; }
        internal int TribeId { get; }
        internal int TargetX { get; }
        internal int TargetY { get; }
        internal int ConfiguredSpacing { get; }
        internal MoveFormationUnitIdentity[] Units { get; }
        internal int StandardCalls { get; set; }
        internal int AssassinGroundCalls { get; set; }
        internal int OverriddenCalls { get; set; }
        internal int PreservedOneCalls { get; set; }
        internal int[] VanillaCounts { get; } = new int[5];
        internal int[] EffectiveCounts { get; } = new int[5];

        internal MoveFormationSpacingAudit Audit => new MoveFormationSpacingAudit(
            ConfiguredSpacing,
            StandardCalls,
            AssassinGroundCalls,
            OverriddenCalls,
            PreservedOneCalls,
            VanillaCounts,
            EffectiveCounts);
    }

    internal static class MoveFormationCommandSnapshotStore
    {
        internal const int MinimumTrackedUnits = 200;


        [System.ThreadStatic]
        private static MoveFormationCommandSnapshot current;

        internal static void Begin(
            object owner,
            int tribeId,
            int targetX,
            int targetY,
            int configuredSpacing,
            MoveFormationUnitIdentity[] units)
        {
            current = owner != null && units != null &&
                units.Length >= MinimumTrackedUnits
                ? new MoveFormationCommandSnapshot(
                    owner, tribeId, targetX, targetY, configuredSpacing, units)
                : null;
        }

        internal static void Observe(
            object owner,
            MoveFormationSelector selector,
            int vanillaSpacing,
            int effectiveSpacing)
        {
            MoveFormationCommandSnapshot snapshot = current;
            if (owner == null || snapshot == null ||
                !object.ReferenceEquals(snapshot.Owner, owner))
                return;

            if (selector == MoveFormationSelector.AssassinGround)
                snapshot.AssassinGroundCalls++;
            else
                snapshot.StandardCalls++;
            snapshot.VanillaCounts[CountIndex(vanillaSpacing)]++;
            snapshot.EffectiveCounts[CountIndex(effectiveSpacing)]++;
            if (vanillaSpacing != effectiveSpacing)
                snapshot.OverriddenCalls++;
            if (vanillaSpacing == MoveFormationSpacingPolicy.Minimum &&
                effectiveSpacing == vanillaSpacing)
                snapshot.PreservedOneCalls++;
        }

        internal static bool TryConsume(
            int tribeId,
            int targetX,
            int targetY,
            out MoveFormationCommandSnapshot snapshot)
        {
            snapshot = current;
            current = null;
            if (snapshot == null || snapshot.TribeId != tribeId ||
                snapshot.TargetX != targetX || snapshot.TargetY != targetY)
            {
                snapshot = null;
                return false;
            }
            return true;
        }

        internal static void Clear() => current = null;

        private static int CountIndex(int spacing) =>
            spacing >= MoveFormationSpacingPolicy.Minimum &&
            spacing <= MoveFormationSpacingPolicy.Maximum ? spacing : 0;

    }
}
