using System.Collections.ObjectModel;

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
    }
}
