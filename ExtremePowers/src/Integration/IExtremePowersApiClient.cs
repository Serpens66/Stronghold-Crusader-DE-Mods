using System;

namespace ExtremePowers.Integration
{
    internal readonly struct ApiReadiness
    {
        internal ApiReadiness(bool ready, string reason) { Ready = ready; Reason = reason ?? string.Empty; }
        internal bool Ready { get; }
        internal string Reason { get; }
        internal static ApiReadiness Available => new ApiReadiness(true, string.Empty);
        internal static ApiReadiness Unavailable(string reason) => new ApiReadiness(false, reason);
    }

    internal interface IExtremePowersApiClient
    {
        string Status { get; }
        string CompatibilityToken { get; }
        ApiReadiness EvaluateSession(bool realMultiplayer, string[] reports, int[] players);
        void Apply(Settings.ExtremePowersSettings settings);
        void RestoreVanilla();
        IDisposable InstallGoldDemo(Settings.ExtremePowersSettings settings);
    }
}
