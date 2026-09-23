using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SerpsModsHost
{
    internal static class ModSettingsRegistrationOrder
    {
        public static bool PromoteToFront<T>(ObservableCollection<T> registrations, T registration)
            where T : class
        {
            if (registrations == null || registration == null)
                return false;

            int currentIndex = registrations.IndexOf(registration);
            if (currentIndex <= 0)
                return false;

            registrations.Move(currentIndex, 0);
            return true;
        }

        public static bool SortAlphabetically<T>(
            ObservableCollection<T> registrations,
            T firstRegistration,
            Func<T, string> getName)
            where T : class
        {
            if (registrations == null || getName == null)
                return false;

            // OrderBy is stable when two mods expose the same display name.
            List<T> ordered = registrations
                .OrderBy(entry => ReferenceEquals(entry, firstRegistration) ? 0 : 1)
                .ThenBy(entry => getName(entry) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();

            bool changed = false;
            for (int targetIndex = 0; targetIndex < ordered.Count; targetIndex++)
            {
                int currentIndex = registrations.IndexOf(ordered[targetIndex]);
                if (currentIndex == targetIndex)
                    continue;

                registrations.Move(currentIndex, targetIndex);
                changed = true;
            }

            return changed;
        }
    }
}
