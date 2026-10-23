// Connect Extra Features purchase guards to the optional Bugfixes and QoL market owner.
using BepInEx.Bootstrap;
using BepInEx.Logging;
using SHCDESE.Interop;
using System;
using System.Reflection;

namespace ExtraFeatures
{
    internal sealed class MarketTradeGuardBridge : IDisposable
    {
        private const string BugfixPluginGuid = "BugfixesAndQoL_Serp";
        private const string IntegrationTypeName = "BugfixesAndQoL.MarketTradeIntegration";

        private delegate void RegisterDelegate(Action<int, int> begin, Action<int, int> end);
        private delegate void UnregisterDelegate(Action<int, int> begin);

        private readonly ManualLogSource log;
        private readonly Action<int, int> beginCallback;
        private readonly Action<int, int> endCallback;
        private UnregisterDelegate unregister;
        private bool registered;
        private bool connectAttempted;

        public MarketTradeGuardBridge(ManualLogSource log, ExtraFeaturesRuntime runtime)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));

            beginCallback = (playerId, good) => runtime.BeginSingleMarketBuyGuard(playerId, (eGoods)good);
            endCallback = (playerId, good) => runtime.EndSingleMarketBuyGuard(playerId, (eGoods)good);
        }

        /// <summary>
        /// Must run after the Chainloader finished: Bugfixes and QoL soft-depends on this mod and
        /// therefore loads after Extra Features' Awake, so an Awake-time lookup never finds it.
        /// </summary>
        public void TryConnect()
        {
            if (connectAttempted)
                return;
            connectAttempted = true;

            if (!Chainloader.PluginInfos.TryGetValue(BugfixPluginGuid, out var pluginInfo))
                return;

            try
            {
                if (pluginInfo?.Instance == null)
                    throw new InvalidOperationException("Bugfixes and QoL is listed but has no plugin instance.");

                Type integrationType = pluginInfo.Instance.GetType().Assembly.GetType(IntegrationTypeName, true);
                RegisterDelegate register = CreateDelegate<RegisterDelegate>(integrationType, "RegisterSingleBuyGuards");
                unregister = CreateDelegate<UnregisterDelegate>(integrationType, "UnregisterSingleBuyGuards");
                register(beginCallback, endCallback);
                registered = true;
                Shared.DebugLogHelper.LogDebug(log, "Extra Features registered market-purchase guards with Bugfixes and QoL.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtraFeatures/src/MarketTradeGuardBridge.cs:62", $"Extra Features market-purchase guard integration failed; purchased goods may be treated as gained goods: {ex}");
            }
        }

        public void Dispose()
        {
            if (registered)
                unregister(beginCallback);
            registered = false;
        }

        private static T CreateDelegate<T>(Type type, string methodName) where T : Delegate
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (method == null)
                throw new MissingMethodException(type.FullName, methodName);
            return (T)method.CreateDelegate(typeof(T));
        }
    }
}
