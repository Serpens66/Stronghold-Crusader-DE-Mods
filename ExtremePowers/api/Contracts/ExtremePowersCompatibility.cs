using System;

namespace ExtremePowers.API
{
    public static class ExtremePowersCompatibility
    {
        public static string CreateToken(string protocol, string dllHash, bool nativeBackendAvailable, int packetId)
        {
            return (protocol ?? string.Empty) + "|" + (dllHash ?? string.Empty) + "|" + (nativeBackendAvailable ? "native" : "vanilla") + "|" + packetId;
        }

        public static ExtremePowersReadiness EvaluateSession(bool realMultiplayer, string expectedToken, string[] reports, int[] players)
        {
            if (!realMultiplayer) return ExtremePowersReadiness.Available;
            if (string.IsNullOrWhiteSpace(expectedToken)) return ExtremePowersReadiness.Unavailable("local compatibility token is unavailable");
            if (players == null || players.Length == 0) return ExtremePowersReadiness.Unavailable("real multiplayer participant roster is unresolved");
            if (reports == null) return ExtremePowersReadiness.Unavailable("participant compatibility reports are unavailable");
            foreach (int player in players)
            {
                if (player < 1 || player >= reports.Length) return ExtremePowersReadiness.Unavailable("participant id " + player + " is outside the report table");
                if (string.IsNullOrWhiteSpace(reports[player])) return ExtremePowersReadiness.Unavailable("participant " + player + " has not reported compatibility");
                if (!string.Equals(reports[player], expectedToken, StringComparison.Ordinal)) return ExtremePowersReadiness.Unavailable("participant " + player + " reported an incompatible API/DLL/backend/packet token");
            }
            return ExtremePowersReadiness.Available;
        }
    }
}
