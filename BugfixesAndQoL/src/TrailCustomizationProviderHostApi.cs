using System;

namespace BugfixesAndQoL
{
    /// <summary>Optional reflection surface for mods that extend Trail customization.</summary>
    public static class TrailCustomizationProviderHostApi
    {
        public const int ApiVersion = 1;
        private const string SupportedProviderId = "CustomCustomTrail_Serp";

        private static readonly object Sync = new object();
        private static string providerId;
        private static Func<bool> isEnabled;
        private static Func<bool> customizeCustomTrail;
        private static Func<bool> customizeCoopTrail;
        private static Action refreshVisibility;

        public static bool RegisterProvider(
            string id,
            int apiVersion,
            Func<bool> enabled,
            Func<bool> customTrailHandler,
            Func<bool> coopTrailHandler)
        {
            if (!string.Equals(id, SupportedProviderId, StringComparison.Ordinal) ||
                apiVersion != ApiVersion || enabled == null ||
                customTrailHandler == null || coopTrailHandler == null)
            {
                return false;
            }

            Action refresh;
            lock (Sync)
            {
                // A provider may suppress its standalone buttons only after the host has
                // successfully installed the physical UI and transition hooks.
                if (refreshVisibility == null)
                    return false;
                if (providerId != null && !string.Equals(providerId, id, StringComparison.Ordinal))
                    return false;
                providerId = id;
                isEnabled = enabled;
                customizeCustomTrail = customTrailHandler;
                customizeCoopTrail = coopTrailHandler;
                refresh = refreshVisibility;
            }
            // Registration is already committed. A presentation refresh must not turn a
            // successful handshake into a false result and make both mods own the buttons.
            try { refresh?.Invoke(); }
            catch { }
            return true;
        }

        public static void Refresh()
        {
            Action refresh;
            lock (Sync)
                refresh = refreshVisibility;
            refresh?.Invoke();
        }

        internal static void Attach(Action refresh)
        {
            lock (Sync)
                refreshVisibility = refresh;
            refresh?.Invoke();
        }

        internal static void Detach(Action refresh)
        {
            lock (Sync)
            {
                if (refreshVisibility == refresh)
                    refreshVisibility = null;
            }
        }

        internal static bool IsProviderEnabled()
        {
            Func<bool> callback;
            lock (Sync)
                callback = isEnabled;
            return callback != null && callback();
        }

        internal static bool TryCustomizeCustomTrail(out bool providerActive) =>
            TryInvoke(customizeCustomTrail, out providerActive);

        internal static bool TryCustomizeCoopTrail(out bool providerActive) =>
            TryInvoke(customizeCoopTrail, out providerActive);

        private static bool TryInvoke(Func<bool> handler, out bool providerActive)
        {
            Func<bool> enabled;
            lock (Sync)
                enabled = isEnabled;
            providerActive = enabled != null && enabled();
            return providerActive && handler != null && handler();
        }
    }
}
