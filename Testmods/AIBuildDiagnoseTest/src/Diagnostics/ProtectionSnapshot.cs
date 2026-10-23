using System;
using System.Reflection;
using System.Text;
using BepInEx.Bootstrap;

namespace AIBuildDiagnoseTest
{
    internal static class ProtectionSnapshot
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static object Read(object value, string name, bool field, StringBuilder missing)
        {
            if (ReferenceEquals(value, null)) return null;
            try
            {
                MemberInfo member = field ? (MemberInfo)value.GetType().GetField(name, Flags) : value.GetType().GetProperty(name, Flags);
                if (member == null) { missing.Append(name).Append(":member-missing;"); return null; }
                return field ? ((FieldInfo)member).GetValue(value) : ((PropertyInfo)member).GetValue(value);
            }
            catch (Exception error)
            {
                missing.Append(name).Append(":read-failed-").Append(error.GetType().Name).Append(';');
                return null;
            }
        }
        internal static string Capture()
        {
            try
            {
                bool registered = Chainloader.PluginInfos.TryGetValue("BugfixesAndQoL_Serp", out var plugin);
                return Capture(registered, registered ? (object)plugin.Instance : null, registered ? plugin.Metadata.Version.ToString() : "unavailable");
            }
            catch (Exception error) { return "protection-settings=unavailable:" + error.GetType().Name; }
        }

        // The managed plugin survives Unity's startup destruction; never use Unity's overloaded null comparison.
        internal static string Capture(bool registered, object instance, string version)
        {
            if (!registered) return "protection-settings=unavailable; owner-not-registered";
            if (ReferenceEquals(instance, null)) return "protection-settings=unavailable; owner-managed-reference-missing; ownerVersion=" + version;
            var missing = new StringBuilder();
            object runtime = Read(instance, "runtime", true, missing);
            object hook = Read(runtime, "aiEconomyProtectionHook", true, missing);
            object hookSettings = Read(hook, "settings", true, missing);
            object settings = hookSettings ?? Read(instance, "Settings", false, missing);
            var text = new StringBuilder("ownerVersion=" + version + ", runtime=" +
                (ReferenceEquals(runtime, null) ? "unavailable" : "available") + ", protectionHook=" +
                (ReferenceEquals(hook, null) ? "unavailable" : "available") + ", settingsScope=" +
                (ReferenceEquals(hookSettings, null) ? "configuration-only" : "installed-hook-settings") + ";");
            foreach (string key in new[] { "EnableMod", "PreventEmergencyDemolition", "PreventHovelDeletion", "InaccessibleAIBuildingDemolitionProtection", "PreventAIPause", "FixAIPreplacedMapBuildings" })
                text.Append(key).Append('=').Append(Read(settings, key, false, missing) ?? "unavailable").Append(';');
            foreach (string key in new[] { "emergencyDemolitionHook", "aiHovelDemolitionHook", "inaccessibleBuildingDemolitionHook", "aiResourceShortageSleepHook", "sleepStateHook" })
                text.Append(key).Append(".Success=").Append(Read(Read(hook, key, true, missing), "Success", false, missing) ?? "unavailable").Append(';');
            foreach (string key in new[] { "aiResourceShortageSleepProtectionSupported", "inaccessibleBuildingProtectionSupported" })
                text.Append(key).Append('=').Append(Read(hook, key, true, missing) ?? "unavailable").Append(';');
            return text + "missingMembers=[" + missing + "];perDecisionProtectionObservation=unavailable; historicalHookEffect=unknown; statusIsNotAnAppliedDecision=true";
        }
    }
}
