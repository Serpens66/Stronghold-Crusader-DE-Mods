using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shared
{
    internal static class TroopActionButtonLayoutPolicy
    {
        public const string HostNamePrefix = "SerpTroopAction_";

        public static bool TryParseHostName(string name, out int priority, out string actionId)
        {
            priority = 0;
            actionId = null;
            if (string.IsNullOrEmpty(name) || !name.StartsWith(HostNamePrefix, StringComparison.Ordinal))
                return false;

            int priorityStart = HostNamePrefix.Length;
            int separator = name.IndexOf('_', priorityStart);
            if (separator <= priorityStart || separator >= name.Length - 1 ||
                !int.TryParse(name.Substring(priorityStart, separator - priorityStart), NumberStyles.None, CultureInfo.InvariantCulture, out priority))
            {
                return false;
            }

            actionId = name.Substring(separator + 1);
            return actionId.Length != 0;
        }

        public static IReadOnlyList<string> OrderActionIds(IEnumerable<string> hostNames)
        {
            var actions = new List<ActionIdentity>();
            if (hostNames != null)
            {
                foreach (string hostName in hostNames)
                {
                    if (TryParseHostName(hostName, out int priority, out string actionId))
                        actions.Add(new ActionIdentity(priority, actionId));
                }
            }

            actions.Sort((left, right) =>
            {
                int priority = left.Priority.CompareTo(right.Priority);
                return priority != 0
                    ? priority
                    : string.Compare(left.ActionId, right.ActionId, StringComparison.Ordinal);
            });
            var result = new string[actions.Count];
            for (int index = 0; index < actions.Count; index++)
                result[index] = actions[index].ActionId;
            return result;
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
            return isVisible && isHitTestVisible;
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
    }
}
