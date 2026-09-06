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

        internal string Format(int inferredAssassinStructureCalls = 0) =>
            $"configuredSpacing={ConfiguredSpacing}, " +
            $"selectors=standard:{StandardCalls}|assassin-ground:{AssassinGroundCalls}|" +
            $"assassin-structure-inferred:{inferredAssassinStructureCalls}, " +
            $"vanillaSpacing={FormatCounts(VanillaCounts, inferredAssassinStructureCalls)}, " +
            $"effectiveSpacing={FormatCounts(EffectiveCounts, inferredAssassinStructureCalls)}, " +
            $"spacingOverrides={OverriddenCalls}, " +
            $"preservedVanillaOne={PreservedOneCalls + inferredAssassinStructureCalls}";

        private static string FormatCounts(int[] counts, int additionalOne) =>
            $"1:{counts[1] + additionalOne}|2:{counts[2]}|3:{counts[3]}|4:{counts[4]}|other:{counts[0]}";
    }

    internal static class MoveFormationSpacingAuditStore
    {
        private sealed class Accumulator
        {
            internal object Owner;
            internal int TribeId;
            internal int TargetX;
            internal int TargetY;
            internal int ConfiguredSpacing;
            internal int StandardCalls;
            internal int AssassinGroundCalls;
            internal int OverriddenCalls;
            internal int PreservedOneCalls;
            internal readonly int[] VanillaCounts = new int[5];
            internal readonly int[] EffectiveCounts = new int[5];
        }

        [System.ThreadStatic]
        private static Accumulator current;

        internal static void Observe(
            object owner,
            int tribeId,
            int targetX,
            int targetY,
            int configuredSpacing,
            MoveFormationSelector selector,
            int vanillaSpacing,
            int effectiveSpacing)
        {
            if (owner == null)
                return;
            if (current == null || !object.ReferenceEquals(current.Owner, owner))
            {
                current = new Accumulator
                {
                    Owner = owner,
                    TribeId = tribeId,
                    TargetX = targetX,
                    TargetY = targetY,
                    ConfiguredSpacing = NormalizeConfigured(configuredSpacing)
                };
            }

            if (selector == MoveFormationSelector.AssassinGround)
                current.AssassinGroundCalls++;
            else
                current.StandardCalls++;
            current.VanillaCounts[CountIndex(vanillaSpacing)]++;
            current.EffectiveCounts[CountIndex(effectiveSpacing)]++;
            if (vanillaSpacing != effectiveSpacing)
                current.OverriddenCalls++;
            if (vanillaSpacing == MoveFormationSpacingPolicy.Minimum &&
                effectiveSpacing == vanillaSpacing)
                current.PreservedOneCalls++;
        }

        internal static bool TryConsume(
            int tribeId,
            int targetX,
            int targetY,
            out MoveFormationSpacingAudit audit)
        {
            Accumulator accumulator = current;
            if (accumulator == null || accumulator.TribeId != tribeId ||
                accumulator.TargetX != targetX || accumulator.TargetY != targetY)
            {
                audit = default;
                return false;
            }

            audit = new MoveFormationSpacingAudit(
                accumulator.ConfiguredSpacing,
                accumulator.StandardCalls,
                accumulator.AssassinGroundCalls,
                accumulator.OverriddenCalls,
                accumulator.PreservedOneCalls,
                (int[])accumulator.VanillaCounts.Clone(),
                (int[])accumulator.EffectiveCounts.Clone());
            current = null;
            return true;
        }

        internal static void Clear() => current = null;

        private static int CountIndex(int spacing) =>
            spacing >= MoveFormationSpacingPolicy.Minimum &&
            spacing <= MoveFormationSpacingPolicy.Maximum ? spacing : 0;

        private static int NormalizeConfigured(int spacing) =>
            MoveFormationSpacingPolicy.Normalize(spacing);
    }
}
