// Feature: Deterministic shared-pool calculation for siege-ammunition restocking.
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal enum SiegeAmmoRestockModifier
    {
        Normal = 0,
        Shift = 1,
        Control = 2,
        ShiftAndControl = 3
    }

    internal readonly struct SiegeAmmoRestockTarget
    {
        internal SiegeAmmoRestockTarget(int globalUnitId, ushort ammunition)
        {
            GlobalUnitId = globalUnitId;
            Ammunition = ammunition;
        }

        internal int GlobalUnitId { get; }
        internal ushort Ammunition { get; }
    }

    internal sealed class SiegeAmmoRestockPlan
    {
        internal SiegeAmmoRestockPlan(int stoneCost, int ammunitionAdded, SiegeAmmoRestockTarget[] targets)
        {
            StoneCost = stoneCost;
            AmmunitionAdded = ammunitionAdded;
            Targets = targets ?? Array.Empty<SiegeAmmoRestockTarget>();
        }

        internal int StoneCost { get; }
        internal int AmmunitionAdded { get; }
        internal SiegeAmmoRestockTarget[] Targets { get; }
    }

    internal static class SiegeAmmoRestockPolicy
    {
        internal const int MaximumTargetCount = 128;

        internal static bool TryCalculateRequestedPackage(
            int baseStoneCost,
            int baseAmmunitionAmount,
            SiegeAmmoRestockModifier modifier,
            out int stoneCost,
            out int ammunitionAmount)
        {
            stoneCost = 0;
            ammunitionAmount = 0;
            if (baseStoneCost <= 0 || baseAmmunitionAmount <= 0 ||
                !Enum.IsDefined(typeof(SiegeAmmoRestockModifier), modifier))
            {
                return false;
            }

            long desiredAmmunition = ScaleAmmunition(baseAmmunitionAmount, modifier);
            if (desiredAmmunition <= 0 || desiredAmmunition > int.MaxValue)
                return false;

            long desiredStone = MultiplyDivideCeiling(
                desiredAmmunition,
                baseStoneCost,
                baseAmmunitionAmount);
            if (desiredStone <= 0 || desiredStone > int.MaxValue)
                return false;

            stoneCost = (int)desiredStone;
            ammunitionAmount = (int)desiredAmmunition;
            return true;
        }

        internal static string ReplaceFirstTwoNumbers(string template, int first, int second)
        {
            if (string.IsNullOrEmpty(template))
                return template;

            int firstStart = FindNumberStart(template, 0);
            if (firstStart < 0)
                return template;
            int firstEnd = FindNumberEnd(template, firstStart);
            int secondStart = FindNumberStart(template, firstEnd);
            if (secondStart < 0)
                return template;
            int secondEnd = FindNumberEnd(template, secondStart);
            return template.Substring(0, firstStart) + first +
                template.Substring(firstEnd, secondStart - firstEnd) + second +
                template.Substring(secondEnd);
        }

        internal static bool TryCreatePlan(
            int baseStoneCost,
            int baseAmmunitionAmount,
            SiegeAmmoRestockModifier modifier,
            int availableStone,
            IReadOnlyList<SiegeAmmoRestockTarget> targets,
            out SiegeAmmoRestockPlan plan)
        {
            plan = null;
            if (baseStoneCost <= 0 || baseAmmunitionAmount <= 0 || availableStone <= 0 ||
                targets == null || targets.Count == 0 || targets.Count > MaximumTargetCount ||
                !Enum.IsDefined(typeof(SiegeAmmoRestockModifier), modifier))
            {
                return false;
            }

            var ordered = new List<MutableTarget>(targets.Count);
            var ids = new HashSet<int>();
            long totalCapacity = 0;
            for (int index = 0; index < targets.Count; index++)
            {
                SiegeAmmoRestockTarget target = targets[index];
                if (target.GlobalUnitId <= 0 || !ids.Add(target.GlobalUnitId))
                    return false;

                ordered.Add(new MutableTarget(target.GlobalUnitId, target.Ammunition));
                totalCapacity += ushort.MaxValue - target.Ammunition;
            }

            if (!TryCalculateRequestedPackage(
                    baseStoneCost,
                    baseAmmunitionAmount,
                    modifier,
                    out _,
                    out int requestedAmmunition) ||
                totalCapacity <= 0)
            {
                return false;
            }

            long desiredAmmunition = requestedAmmunition;

            long affordableAmmunition = MultiplyDivideFloor(availableStone, baseAmmunitionAmount, baseStoneCost);
            long ammunitionToAdd = Math.Min(desiredAmmunition, Math.Min(affordableAmmunition, totalCapacity));
            if (ammunitionToAdd <= 0)
                return false;

            long stoneCost = MultiplyDivideCeiling(ammunitionToAdd, baseStoneCost, baseAmmunitionAmount);
            if (stoneCost <= 0 || stoneCost > availableStone || stoneCost > int.MaxValue)
                return false;

            ordered.Sort((left, right) =>
            {
                int byAmmunition = left.Ammunition.CompareTo(right.Ammunition);
                return byAmmunition != 0 ? byAmmunition : left.GlobalUnitId.CompareTo(right.GlobalUnitId);
            });

            DistributeWaterFilled(ordered, ammunitionToAdd);
            var results = new SiegeAmmoRestockTarget[ordered.Count];
            long verifiedAdded = 0;
            for (int index = 0; index < ordered.Count; index++)
            {
                MutableTarget target = ordered[index];
                verifiedAdded += target.Ammunition - target.OriginalAmmunition;
                results[index] = new SiegeAmmoRestockTarget(target.GlobalUnitId, (ushort)target.Ammunition);
            }

            if (verifiedAdded != ammunitionToAdd || verifiedAdded > int.MaxValue)
                return false;

            plan = new SiegeAmmoRestockPlan((int)stoneCost, (int)verifiedAdded, results);
            return true;
        }

        private static long ScaleAmmunition(int amount, SiegeAmmoRestockModifier modifier)
        {
            switch (modifier)
            {
                case SiegeAmmoRestockModifier.Shift:
                    return SaturatingMultiply(amount, 5);
                case SiegeAmmoRestockModifier.Control:
                    return amount / 5;
                default:
                    return amount;
            }
        }

        private static int FindNumberStart(string text, int start)
        {
            for (int index = Math.Max(0, start); index < text.Length; index++)
                if (text[index] >= '0' && text[index] <= '9') return index;
            return -1;
        }

        private static int FindNumberEnd(string text, int start)
        {
            int index = start;
            while (index < text.Length && text[index] >= '0' && text[index] <= '9') index++;
            return index;
        }

        private static long MultiplyDivideFloor(long value, long multiplier, long divisor)
        {
            if (value <= 0 || multiplier <= 0 || divisor <= 0)
                return 0;
            if (value > long.MaxValue / multiplier)
                return long.MaxValue / divisor;
            return value * multiplier / divisor;
        }

        private static long MultiplyDivideCeiling(long value, long multiplier, long divisor)
        {
            if (value <= 0 || multiplier <= 0 || divisor <= 0)
                return 0;
            long product = SaturatingMultiply(value, multiplier);
            return product == long.MaxValue ? long.MaxValue : (product + divisor - 1) / divisor;
        }

        private static long SaturatingMultiply(long left, long right) =>
            left > 0 && right > 0 && left > long.MaxValue / right ? long.MaxValue : left * right;

        private static void DistributeWaterFilled(List<MutableTarget> targets, long ammunition)
        {
            int groupSize = 1;
            while (ammunition > 0)
            {
                int currentLevel = targets[0].Ammunition;
                while (groupSize < targets.Count && targets[groupSize].Ammunition == currentLevel)
                    groupSize++;

                int nextLevel = groupSize < targets.Count ? targets[groupSize].Ammunition : ushort.MaxValue;
                long required = (long)(nextLevel - currentLevel) * groupSize;
                if (required > 0 && ammunition >= required)
                {
                    for (int index = 0; index < groupSize; index++)
                        targets[index].Ammunition = nextLevel;
                    ammunition -= required;
                    if (nextLevel == ushort.MaxValue)
                        return;
                    continue;
                }

                long equalShare = ammunition / groupSize;
                int remainder = (int)(ammunition % groupSize);
                for (int index = 0; index < groupSize; index++)
                    targets[index].Ammunition += (int)equalShare;

                // Selection order must not affect the unavoidable one-projectile remainder.
                targets.Sort(0, groupSize, GlobalIdComparer.Instance);
                for (int index = 0; index < remainder; index++)
                    targets[index].Ammunition++;
                return;
            }
        }

        private sealed class MutableTarget
        {
            internal MutableTarget(int globalUnitId, ushort ammunition)
            {
                GlobalUnitId = globalUnitId;
                OriginalAmmunition = ammunition;
                Ammunition = ammunition;
            }

            internal int GlobalUnitId { get; }
            internal int OriginalAmmunition { get; }
            internal int Ammunition { get; set; }
        }

        private sealed class GlobalIdComparer : IComparer<MutableTarget>
        {
            internal static readonly GlobalIdComparer Instance = new GlobalIdComparer();
            public int Compare(MutableTarget left, MutableTarget right) => left.GlobalUnitId.CompareTo(right.GlobalUnitId);
        }
    }
}
