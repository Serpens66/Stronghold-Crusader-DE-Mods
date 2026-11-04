using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PlayerControlRelayTest
{
    internal sealed class RelayTribe
    {
        internal RelayTribe(int slot, uint globalId, int owner, RelayUnit[] members)
        {
            if (members == null) throw new ArgumentNullException(nameof(members));
            Slot = slot; GlobalId = globalId; Owner = owner;
            Members = Array.AsReadOnly((RelayUnit[])members.Clone());
        }
        internal int Slot { get; }
        internal uint GlobalId { get; }
        internal int Owner { get; }
        internal ReadOnlyCollection<RelayUnit> Members { get; }
    }
    internal sealed class RelayGroupPlan
    {
        internal RelayGroupPlan(List<RelayTribe> groups) { Groups = Array.AsReadOnly(groups.ToArray()); }
        internal ReadOnlyCollection<RelayTribe> Groups { get; }
    }
    internal delegate bool RelayUnitResolver(RelayUnit captured, out RelayUnit live, out int tribeSlot);
    internal delegate bool RelayTribeResolver(int tribeSlot, out RelayTribe tribe);

    // Resolvers must inspect one world under the caller's engine serialization scope.
    // Admission never expands a subset into a whole tribe and never mutates selection.
    internal static class RelayValidation
    {
        internal static bool TryValidate(RelayCommand command, int mapWidth, int mapHeight, out string reason)
        {
            reason = null;
            if (command == null) { reason = "No relay command."; return false; }
            if (mapWidth < 1 || mapHeight < 1) { reason = "Map bounds are unavailable."; return false; }
            if (command.Action == RelayAction.Move &&
                (command.TargetX < 0 || command.TargetY < 0 || command.TargetX >= mapWidth || command.TargetY >= mapHeight))
            { reason = "Move target is outside the current map."; return false; }
            return true;
        }
        internal static bool TryPlan(RelayCommand command, int mapWidth, int mapHeight,
            RelayUnitResolver resolveUnit, RelayTribeResolver resolveTribe, out RelayGroupPlan plan, out string reason)
        {
            plan = null;
            if (!TryValidate(command, mapWidth, mapHeight, out reason)) return false;
            if (resolveUnit == null || resolveTribe == null) { reason = "Live object resolution is unavailable."; return false; }
            var selected = new Dictionary<int, RelayUnit>();
            var assigned = new Dictionary<int, int>();
            var tribeSlots = new SortedSet<int>();
            foreach (var captured in command.Units)
            {
                if (!resolveUnit(captured, out var live, out int tribeSlot) || !live.Equals(captured) || tribeSlot <= 0)
                { reason = "A selected unit changed identity, ownership or tribe."; return false; }
                selected.Add(captured.Slot, captured); assigned.Add(captured.Slot, tribeSlot); tribeSlots.Add(tribeSlot);
            }
            var groups = new List<RelayTribe>();
            var covered = new HashSet<int>();
            foreach (int slot in tribeSlots)
            {
                if (!resolveTribe(slot, out var tribe) || tribe == null || tribe.Slot != slot || tribe.GlobalId == 0 ||
                    tribe.Owner != command.Owner || tribe.Members.Count < 1 || tribe.Members.Count > command.Units.Count)
                { reason = "A selected tribe is missing or changed identity/ownership."; return false; }
                foreach (var member in tribe.Members)
                {
                    if (!selected.TryGetValue(member.Slot, out var requested) || !requested.Equals(member) ||
                        assigned[member.Slot] != slot || !covered.Add(member.Slot))
                    { reason = "Selection must contain every member of each existing tribe exactly once."; return false; }
                }
                groups.Add(tribe);
            }
            if (covered.Count != selected.Count)
            { reason = "A selected unit is absent from its current tribe."; return false; }
            plan = new RelayGroupPlan(groups); return true;
        }
    }
}
