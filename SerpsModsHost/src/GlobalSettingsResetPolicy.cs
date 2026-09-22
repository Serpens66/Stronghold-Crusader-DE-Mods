using System;
using System.Collections.Generic;
using System.Linq;

namespace SerpsModsHost
{
    internal static class GlobalSettingsResetPolicy
    {
        internal static bool CanReset(
            bool confirmationVisible,
            bool isLocalHost,
            bool hasValidSelection,
            int targetCount,
            bool hasReadOnlyMissionTarget) =>
            !confirmationVisible &&
            isLocalHost &&
            hasValidSelection &&
            targetCount > 0 &&
            !hasReadOnlyMissionTarget;

        internal static bool CanSelect(
            bool confirmationVisible,
            bool isLocalHost,
            int targetCount,
            bool hasReadOnlyMissionTarget) =>
            !confirmationVisible &&
            isLocalHost &&
            targetCount > 0 &&
            !hasReadOnlyMissionTarget;

        internal static void ApplyAtomically<TEndpoint, TSnapshot>(
            IEnumerable<TEndpoint> endpoints,
            Func<TEndpoint, TSnapshot> capture,
            Action<TEndpoint> apply,
            Action<TEndpoint, TSnapshot> restore)
        {
            if (endpoints == null)
                throw new ArgumentNullException(nameof(endpoints));
            if (capture == null)
                throw new ArgumentNullException(nameof(capture));
            if (apply == null)
                throw new ArgumentNullException(nameof(apply));
            if (restore == null)
                throw new ArgumentNullException(nameof(restore));

            TEndpoint[] targets = endpoints.ToArray();
            var snapshots = new List<KeyValuePair<TEndpoint, TSnapshot>>(targets.Length);
            foreach (TEndpoint endpoint in targets)
                snapshots.Add(new KeyValuePair<TEndpoint, TSnapshot>(endpoint, capture(endpoint)));

            try
            {
                foreach (TEndpoint endpoint in targets)
                    apply(endpoint);
            }
            catch (Exception applyException)
            {
                var rollbackExceptions = new List<Exception>();
                foreach (KeyValuePair<TEndpoint, TSnapshot> snapshot in snapshots)
                {
                    try
                    {
                        restore(snapshot.Key, snapshot.Value);
                    }
                    catch (Exception rollbackException)
                    {
                        rollbackExceptions.Add(rollbackException);
                    }
                }

                if (rollbackExceptions.Count != 0)
                {
                    rollbackExceptions.Insert(0, applyException);
                    throw new AggregateException(
                        "Resetting ModSettings failed and at least one rollback also failed.",
                        rollbackExceptions);
                }

                throw;
            }
        }
    }
}
