using System;
using BepInEx.Logging;
using RedBird.Core.Memory;

namespace BugfixesAndQoL
{
    internal sealed class VanillaPeaceTimeGameplayPatch
    {
        private readonly BugfixesAndQoLViewModel settings;
        private readonly PermanentNativeSwitch native;

        internal VanillaPeaceTimeGameplayPatch(ManualLogSource log, IntPtr libraryHandle, ScanRegion region,
            ReadOnlySpan<byte> memory, bool referenceHashMatches, BugfixesAndQoLViewModel settings)
        {
            this.settings=settings ?? throw new ArgumentNullException(nameof(settings));
            if(log==null) throw new ArgumentNullException(nameof(log));
            if(libraryHandle==IntPtr.Zero || region==null || memory.Length==0)
                throw new ArgumentException("The Crusader native library is unavailable.");
            ulong imageBase=unchecked((ulong)libraryHandle.ToInt64());
            VanillaPeaceTimeNativeContract.Validate(memory,imageBase,referenceHashMatches);
            native=new PermanentNativeSwitch(log,imageBase,NativeSwitchCatalog.CreatePlans(memory,imageBase,false));
            settings.SettingChanged+=Refresh;
            Refresh(null);
        }

        private bool DesiredEnabled => settings.EnableMod && settings.EnablePeaceTimeGameplayFixes;

        private void Refresh(string propertyName) =>
            native.SetEnabled(DesiredEnabled);
    }
}
