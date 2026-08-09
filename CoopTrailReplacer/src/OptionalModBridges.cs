using BepInEx.Bootstrap;
using CoopTrailReplacer.Core;
using System;
using System.Linq;
using System.Reflection;

namespace CoopTrailReplacer
{
    /// <summary>
    /// Neutral reflection boundary: CoopTrailReplacer does not depend on any one
    /// settings mod and talks only to the elected Shared runtime.
    /// </summary>
    internal sealed class TrailModSettingsBridge
    {
        private readonly MethodInfo enterMethod;
        private readonly MethodInfo exitMethod;

        public TrailModSettingsBridge()
        {
            string leaderId = ModSettingsDefinition.TargetModIds
                .Where(id => Chainloader.PluginInfos.ContainsKey(id))
                .OrderBy(id => id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (leaderId == null)
                return;

            Assembly assembly = Chainloader.PluginInfos[leaderId].Instance.GetType().Assembly;
            Type runtime = assembly.GetType("Shared.TrailModSettingsRuntime", false);
            enterMethod = runtime?.GetMethod("System_EnterCoopTrailJson", BindingFlags.Public | BindingFlags.Static);
            exitMethod = runtime?.GetMethod("System_ExitTrailContext", BindingFlags.Public | BindingFlags.Static);
        }

        public bool IsAvailable => enterMethod != null;

        public string[] Enter(ModSettingsDefinition settings, bool editable)
        {
            if (enterMethod == null)
                return Array.Empty<string>();
            return (string[])enterMethod.Invoke(
                null,
                new object[] { MissionLoader.SerializeModSettings(settings), editable });
        }

        public void Exit()
        {
            exitMethod?.Invoke(null, null);
        }

    }
}
