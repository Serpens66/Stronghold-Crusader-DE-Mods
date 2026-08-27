using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using System;
using System.Reflection;

namespace CastlePlanner
{
    internal static class ExtraFeaturesFlagBridge
    {
        private const string PluginGuid = "ExtraFeatures_Serp";
        private const string RegistrationMethodName = "TryRegisterVanillaFlagDisease";
        private static MethodInfo registrationMethod;
        private static object pluginInstance;
        private static bool resolutionAttempted;
        private static bool failureLogged;

        public static bool TryRegisterDiseaseFlag(
            int projectileId,
            ManualLogSource log)
        {
            try
            {
                if (!TryResolve())
                    return false;

                object result = registrationMethod.Invoke(
                    pluginInstance,
                    new object[] { projectileId });
                if (result is bool registered && registered)
                    return true;

                LogFailureOnce(
                    log,
                    $"ExtraFeatures declined Disease-flag registration for projectile {projectileId}; Vanilla spawning remains active.");
            }
            catch (Exception ex)
            {
                LogFailureOnce(
                    log,
                    $"Optional ExtraFeatures Disease-flag registration failed; Vanilla spawning remains active: {ex.GetBaseException().Message}.");
            }
            return false;
        }

        private static bool TryResolve()
        {
            if (resolutionAttempted)
                return registrationMethod != null && pluginInstance != null;

            resolutionAttempted = true;
            if (!Chainloader.PluginInfos.TryGetValue(PluginGuid, out PluginInfo pluginInfo) ||
                pluginInfo?.Instance == null)
            {
                return false;
            }

            MethodInfo method = pluginInfo.Instance.GetType().GetMethod(
                RegistrationMethodName,
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(int) },
                modifiers: null);
            if (method == null || method.ReturnType != typeof(bool))
                return false;

            pluginInstance = pluginInfo.Instance;
            registrationMethod = method;
            return true;
        }

        private static void LogFailureOnce(ManualLogSource log, string message)
        {
            if (failureLogged)
                return;
            failureLogged = true;
            Shared.DebugLogHelper.LogWarning(log, message);
        }
    }
}
