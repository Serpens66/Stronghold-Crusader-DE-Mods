using BepInEx.Logging;
using System;
using System.Reflection;

namespace CustomCustomTrail
{
    internal sealed class BugfixesAndQoLTrailCustomizationBridge
    {
        private const string HostTypeName =
            "BugfixesAndQoL.TrailCustomizationProviderHostApi, BugfixesAndQoL";
        private readonly ManualLogSource log;
        private MethodInfo refreshMethod;

        public BugfixesAndQoLTrailCustomizationBridge(ManualLogSource log)
        {
            this.log = log;
        }

        public bool TryRegister(
            Func<bool> isEnabled,
            Func<bool> customizeCustomTrail,
            Func<bool> customizeCoopTrail)
        {
            try
            {
                Type host = Type.GetType(HostTypeName, throwOnError: false);
                MethodInfo register = host?.GetMethod(
                    "RegisterProvider",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(int), typeof(Func<bool>), typeof(Func<bool>), typeof(Func<bool>) },
                    null);
                refreshMethod = host?.GetMethod("Refresh", BindingFlags.Public | BindingFlags.Static);
                if (register == null || refreshMethod == null)
                    return false;
                bool registered = (bool)register.Invoke(
                    null,
                    new object[]
                    {
                        CustomCustomTrailPlugin.PluginGuid,
                        1,
                        isEnabled,
                        customizeCustomTrail,
                        customizeCoopTrail,
                    });
                if (registered)
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        "Registered CustomCustomTrail as the BugfixesAndQoL Trail customization provider.");
                }
                return registered;
            }
            catch (Exception exception)
            {
                refreshMethod = null;
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "BugfixesAndQoL Trail customization integration is unavailable; " +
                    "CustomCustomTrail keeps standalone button ownership: " + exception.Message);
                return false;
            }
        }

        public void Refresh()
        {
            try
            {
                refreshMethod?.Invoke(null, null);
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Could not refresh BugfixesAndQoL Trail button visibility: " + exception.Message);
            }
        }
    }
}
