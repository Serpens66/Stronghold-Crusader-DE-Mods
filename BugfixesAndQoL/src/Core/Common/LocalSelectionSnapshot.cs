using APIShared;

namespace BugfixesAndQoL
{
    internal static class LocalSelectionSnapshot
    {
        internal static bool TryCapture(int localPlayerId, out APIShared.LocalSelectionSnapshot selected) =>
            LocalSelectionAPI.TryCapture(localPlayerId, out selected);
    }
}
