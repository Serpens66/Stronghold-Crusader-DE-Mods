using System.Collections.Generic;

namespace ExtendedData
{
    internal enum CoopWorkshopNotice { None, UpdatePending, Restart }

    internal sealed class CoopWorkshopInstallation
    {
        internal string Root { get; set; }
        internal HashSet<string> PackageIds { get; set; } = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        internal uint InstalledAt { get; set; }
        internal ulong? CompletedManifest { get; private set; }

        internal static bool TryObserveCompletion(Dictionary<ulong, CoopWorkshopInstallation> installations,
            ulong workshopId, ulong manifest) => installations.TryGetValue(workshopId, out var source) && source.ObserveCompletion(manifest);

        internal static bool TryMatch(Dictionary<ulong, CoopWorkshopInstallation> installations, string packageId,
            out KeyValuePair<ulong, CoopWorkshopInstallation> match)
        {
            match = default;
            foreach (var entry in installations)
            {
                if (!entry.Value.PackageIds.Contains(packageId)) continue;
                if (match.Value != null) return false;
                match = entry;
            }
            return match.Value != null;
        }

        // Instances exist only for subscriptions with a previously observed installation.
        private bool ObserveCompletion(ulong manifest)
        {
            if (CompletedManifest == manifest) return false;
            CompletedManifest = manifest;
            return true;
        }

        internal CoopWorkshopNotice Notice(bool subscribed, bool installed, bool pending)
        {
            if (!subscribed) return CoopWorkshopNotice.None;
            if (pending) return CoopWorkshopNotice.UpdatePending;
            return installed && CompletedManifest.HasValue ? CoopWorkshopNotice.Restart : CoopWorkshopNotice.None;
        }
    }
}
