using System;

namespace SerpsModsHost
{
    internal static class ModHashCompatibility
    {
        internal static bool TryCreateMismatchMessage(
            string localHash,
            string hostHash,
            string localPlayerName,
            string hostPlayerName,
            string messageTemplate,
            out string message)
        {
            message = null;
            if (string.IsNullOrWhiteSpace(localHash) || string.IsNullOrWhiteSpace(hostHash))
                return false;
            if (string.Equals(localHash, hostHash, StringComparison.Ordinal))
                return false;
            if (string.IsNullOrWhiteSpace(messageTemplate))
                return false;

            message = messageTemplate
                .Replace("{Player}", NormalizeName(localPlayerName))
                .Replace("{Host}", NormalizeName(hostPlayerName))
                .Replace("{PlayerHash}", localHash)
                .Replace("{HostHash}", hostHash);
            return true;
        }

        private static string NormalizeName(string name) =>
            string.IsNullOrWhiteSpace(name) ? "unknown" : name.Trim();
    }
}
