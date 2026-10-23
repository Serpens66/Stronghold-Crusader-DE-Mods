using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace AIBuildDiagnoseTest
{
    // Reflection avoids a circular mod dependency. Only the known private observer contract is used.
    internal sealed class SpectatorAvailabilityConnection
    {
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Static;
        private PropertyInfo ready, failures;
        internal readonly bool Registered;
        internal readonly string Detail;
        internal SpectatorAvailabilityConnection(Func<int, int, int, Action<int?, bool>> callback)
        {
            try
            {
                if (!Chainloader.PluginInfos.TryGetValue("SpectatorEditorBuildTest_Serp", out var owner))
                { Detail = "owner-not-installed; diagnostic owns native resource entry"; return; }
                Type type = owner.Instance.GetType().Assembly.GetType("SpectatorEditorBuildTest.AvailabilityObservation", false);
                var register = type?.GetMethod("Register", Flags, null,
                    new[] { typeof(Func<int, int, int, Action<int?, bool>>) }, null);
                ready = type?.GetProperty("Ready", Flags);
                failures = type?.GetProperty("CallbackFailures", Flags);
                if (register == null || ready?.PropertyType != typeof(bool) || failures?.PropertyType != typeof(long))
                { Detail = "owner-observation-contract-unavailable"; return; }
                Registered = (bool)register.Invoke(null, new object[] { callback });
                Detail = Registered ? "passive-owner-registered" : "owner-observer-already-occupied";
            }
            catch (Exception error) { Detail = "owner-connection-failed:" + error.GetType().Name; }
        }
        internal bool Ready { get { try { return Registered && (bool)ready.GetValue(null); } catch { return false; } } }
        internal long CallbackFailures { get { try { return failures == null ? -1 : (long)failures.GetValue(null); } catch { return -1; } } }
    }
}
