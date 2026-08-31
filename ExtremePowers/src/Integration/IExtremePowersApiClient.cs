using System;

namespace ExtremePowers.Integration
{
    internal interface IExtremePowersApiClient
    {
        string Status { get; }
        void Apply(Settings.ExtremePowersSettings settings);
        void RestoreVanilla();
        IDisposable InstallGoldDemo(Settings.ExtremePowersSettings settings);
    }
}
