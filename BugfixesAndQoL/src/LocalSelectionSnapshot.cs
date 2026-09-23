using System;
using SHCDESE.API;
using SHCDESE.Interop;

namespace BugfixesAndQoL
{
    internal static class LocalSelectionSnapshot
    {
        internal static bool TryCapture(int localPlayerId, out SelectedUnitInfo[] selected)
        {
            selected = Array.Empty<SelectedUnitInfo>();
            if (localPlayerId < 1 || localPlayerId > 8)
                return false;

            try
            {
                GamePlayerManagerAPI api = GamePlayerManagerAPI.Instance;
                if (api == null)
                    return false;
                int count = api.GetSelectedChimpsCount(localPlayerId);
                if (!Shared.SelectedChimpsSnapshotPolicy.IsPlausibleCount(count))
                    return false;
                SelectedUnitInfo[] snapshot = api.GetSelectedChimps(localPlayerId);
                if (snapshot == null || snapshot.Length != count)
                    return false;
                selected = snapshot;
                return true;
            }
            catch (ArgumentException) { return false; }
            catch (OverflowException) { return false; }
            catch (IndexOutOfRangeException) { return false; }
        }
    }
}
