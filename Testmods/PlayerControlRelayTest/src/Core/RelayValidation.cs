using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PlayerControlRelayTest
{
    // Decides what one command does in the current simulation state. Identical on every peer.
    internal sealed class RelayOrderPlan
    {
        internal RelayOrderPlan(int[] slots, int[] sourceTribes, int existingTribe, int stance, int skipped)
        {
            Slots = Array.AsReadOnly(slots); SourceTribes = Array.AsReadOnly(sourceTribes);
            ExistingTribe = existingTribe; Stance = stance; Skipped = skipped;
        }
        internal ReadOnlyCollection<int> Slots { get; }
        internal ReadOnlyCollection<int> SourceTribes { get; }
        // Positive when the units are exactly one whole native group, so no regrouping is needed.
        internal int ExistingTribe { get; }
        internal int Stance { get; }
        internal int Skipped { get; }
    }
    internal delegate bool RelayUnitResolver(int slot, int owner, uint maxGlobalId, out int tribe, out int stance);
    internal delegate int RelayTribeMemberCount(int tribe);

    internal static class RelayValidation
    {
        internal const int StanceCount = 3;

        internal static bool UsesTile(RelayAction action) =>
            action == RelayAction.Move || action == RelayAction.Patrol || action == RelayAction.AttackHere;

        internal static bool TryValidate(RelayCommand command, int mapWidth, int mapHeight, out string reason)
        {
            reason = null;
            if (command == null) { reason = "No relay command."; return false; }
            if (mapWidth < 1 || mapHeight < 1) { reason = "Map bounds are unavailable."; return false; }
            if (UsesTile(command.Action) &&
                (command.TargetX < 0 || command.TargetY < 0 || command.TargetX >= mapWidth || command.TargetY >= mapHeight))
            { reason = "Order target is outside the current map."; return false; }
            if (command.Action == RelayAction.Stance && (command.TargetX < 0 || command.TargetX >= StanceCount))
            { reason = "Unknown stance."; return false; }
            return true;
        }

        internal static bool TryPlan(RelayCommand command, int mapWidth, int mapHeight, RelayUnitResolver resolveUnit,
            RelayTribeMemberCount memberCount, out RelayOrderPlan plan, out string reason)
        {
            plan = null;
            if (!TryValidate(command, mapWidth, mapHeight, out reason)) return false;
            if (resolveUnit == null || memberCount == null) { reason = "Live object resolution is unavailable."; return false; }
            var slots = new List<int>(command.Slots.Count);
            var tribes = new List<int>(command.Slots.Count);
            var stances = new int[StanceCount];
            foreach (int slot in command.Slots)
            {
                // Units that died or may not be ordered right now drop out; the others still obey.
                if (!resolveUnit(slot, command.Owner, command.MaxGlobalId, out int tribe, out int stance) ||
                    stance < 0 || stance >= StanceCount) continue;
                slots.Add(slot); tribes.Add(tribe); stances[stance]++;
            }
            if (slots.Count == 0) { reason = "None of the requested units can be ordered any more."; return false; }
            int shared = tribes[0];
            foreach (int tribe in tribes) if (tribe != shared) { shared = 0; break; }
            if (shared > 0 && memberCount(shared) != slots.Count) shared = 0;
            plan = new RelayOrderPlan(slots.ToArray(), tribes.ToArray(), shared, MajorityStance(stances), command.Slots.Count - slots.Count);
            return true;
        }

        // Same rule and tie order as Vanilla's selection (0x11B150).
        internal static int MajorityStance(int[] counts)
        {
            if (counts[1] <= counts[2] && counts[0] <= counts[2]) return 2;
            return counts[0] <= counts[1] ? 1 : 0;
        }
    }
}
