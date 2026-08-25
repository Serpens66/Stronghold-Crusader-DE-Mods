using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shared
{
    internal enum TroopActionSlot
    {
        BottomRight = 0,
        BottomMiddle = 1
    }

    internal sealed class TroopActionRequest
    {
        public TroopActionRequest(string hostName, string actionId, int priority, bool wantsVisibility)
        {
            HostName = hostName;
            ActionId = actionId;
            Priority = priority;
            WantsVisibility = wantsVisibility;
        }

        public string HostName { get; }
        public string ActionId { get; }
        public int Priority { get; }
        public bool WantsVisibility { get; }
    }

    internal sealed class TroopActionSlotAssignment
    {
        public TroopActionSlotAssignment(string actionId, TroopActionSlot slot)
        {
            ActionId = actionId;
            Slot = slot;
        }

        public string ActionId { get; }
        public TroopActionSlot Slot { get; }
    }

    internal sealed class TroopActionLayoutDecision
    {
        public TroopActionLayoutDecision(
            IReadOnlyList<TroopActionSlotAssignment> assignments,
            IReadOnlyList<string> duplicateActionIds,
            IReadOnlyList<string> overflowActionIds)
        {
            Assignments = assignments;
            DuplicateActionIds = duplicateActionIds;
            OverflowActionIds = overflowActionIds;
        }

        public IReadOnlyList<TroopActionSlotAssignment> Assignments { get; }
        public IReadOnlyList<string> DuplicateActionIds { get; }
        public IReadOnlyList<string> OverflowActionIds { get; }
    }

    internal static class TroopActionButtonLayoutPolicy
    {
        public const int CurrentLayoutVersion = 1;
        public const string HostNamePrefix = "SerpTroopAction_";

        public static bool IsStandardHostName(string name)
        {
            return !string.IsNullOrEmpty(name) &&
                name.StartsWith(HostNamePrefix, StringComparison.Ordinal);
        }

        public static bool TryResolveIdentity(
            string hostName,
            int? layoutVersion,
            string actionId,
            int? priority,
            out int resolvedPriority,
            out string resolvedActionId)
        {
            resolvedPriority = 0;
            resolvedActionId = null;
            bool hasAnyExplicitMetadata = layoutVersion.HasValue || actionId != null || priority.HasValue;
            if (hasAnyExplicitMetadata)
            {
                if (layoutVersion != CurrentLayoutVersion ||
                    string.IsNullOrWhiteSpace(actionId) ||
                    !priority.HasValue || priority.Value < 0)
                {
                    return false;
                }

                resolvedPriority = priority.Value;
                resolvedActionId = actionId;
                return true;
            }

            return TryParseLegacyHostName(hostName, out resolvedPriority, out resolvedActionId);
        }

        public static bool TryParseHostName(string name, out int priority, out string actionId)
        {
            return TryParseLegacyHostName(name, out priority, out actionId);
        }

        public static IReadOnlyList<string> OrderActionIds(IEnumerable<string> hostNames)
        {
            var actions = new List<ActionIdentity>();
            if (hostNames != null)
            {
                foreach (string hostName in hostNames)
                {
                    if (TryParseLegacyHostName(hostName, out int priority, out string actionId))
                        actions.Add(new ActionIdentity(priority, actionId));
                }
            }

            actions.Sort(ActionIdentityComparer.Instance);
            var result = new string[actions.Count];
            for (int index = 0; index < actions.Count; index++)
                result[index] = actions[index].ActionId;
            return result;
        }

        public static TroopActionLayoutDecision CreateDecision(
            IEnumerable<TroopActionRequest> requests,
            bool bottomRightOccupied,
            bool bottomMiddleOccupied)
        {
            var requested = new List<ActionIdentity>();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            if (requests != null)
            {
                foreach (TroopActionRequest request in requests)
                {
                    if (request == null ||
                        string.IsNullOrWhiteSpace(request.ActionId) || request.Priority < 0)
                    {
                        continue;
                    }

                    counts.TryGetValue(request.ActionId, out int count);
                    counts[request.ActionId] = count + 1;
                    if (request.WantsVisibility)
                        requested.Add(new ActionIdentity(request.Priority, request.ActionId));
                }
            }

            var duplicates = new List<string>();
            foreach (KeyValuePair<string, int> pair in counts)
            {
                if (pair.Value > 1)
                    duplicates.Add(pair.Key);
            }
            duplicates.Sort(StringComparer.Ordinal);

            requested.RemoveAll(action => counts[action.ActionId] > 1);
            requested.Sort(ActionIdentityComparer.Instance);

            var freeSlots = new List<TroopActionSlot>(2);
            if (!bottomRightOccupied)
                freeSlots.Add(TroopActionSlot.BottomRight);
            if (!bottomMiddleOccupied)
                freeSlots.Add(TroopActionSlot.BottomMiddle);

            var assignments = new List<TroopActionSlotAssignment>(freeSlots.Count);
            var overflow = new List<string>();
            for (int index = 0; index < requested.Count; index++)
            {
                ActionIdentity action = requested[index];
                if (index < freeSlots.Count)
                    assignments.Add(new TroopActionSlotAssignment(action.ActionId, freeSlots[index]));
                else
                    overflow.Add(action.ActionId);
            }

            return new TroopActionLayoutDecision(assignments, duplicates, overflow);
        }

        public static int FindFirstAvailableSlot(IReadOnlyList<bool> occupied)
        {
            if (occupied == null)
                return -1;
            for (int index = 0; index < occupied.Count; index++)
            {
                if (!occupied[index])
                    return index;
            }
            return -1;
        }

        public static bool IsEffectivelyOccupied(bool isVisible, bool isHitTestVisible)
        {
            return isVisible;
        }

        private static bool TryParseLegacyHostName(string name, out int priority, out string actionId)
        {
            priority = 0;
            actionId = null;
            if (!IsStandardHostName(name))
                return false;

            int priorityStart = HostNamePrefix.Length;
            int separator = name.IndexOf('_', priorityStart);
            if (separator <= priorityStart || separator >= name.Length - 1 ||
                !int.TryParse(name.Substring(priorityStart, separator - priorityStart), NumberStyles.None, CultureInfo.InvariantCulture, out priority) ||
                priority < 0)
            {
                return false;
            }

            actionId = name.Substring(separator + 1);
            return actionId.Length != 0;
        }

        private struct ActionIdentity
        {
            public ActionIdentity(int priority, string actionId)
            {
                Priority = priority;
                ActionId = actionId;
            }

            public int Priority;
            public string ActionId;
        }

        private sealed class ActionIdentityComparer : IComparer<ActionIdentity>
        {
            public static readonly ActionIdentityComparer Instance = new ActionIdentityComparer();

            public int Compare(ActionIdentity left, ActionIdentity right)
            {
                int priority = left.Priority.CompareTo(right.Priority);
                return priority != 0
                    ? priority
                    : string.Compare(left.ActionId, right.ActionId, StringComparison.Ordinal);
            }
        }
    }
}
