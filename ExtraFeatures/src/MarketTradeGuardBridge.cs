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

        public MarketTradeGuardBridge(ManualLogSource log, ExtraFeaturesRuntime runtime)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));

            beginCallback = (playerId, good) => runtime.BeginSingleMarketBuyGuard(playerId, (eGoods)good);
            endCallback = (playerId, good) => runtime.EndSingleMarketBuyGuard(playerId, (eGoods)good);
            if (!Chainloader.PluginInfos.TryGetValue(BugfixPluginGuid, out var pluginInfo))
                return;

            try
            {
                Type integrationType = pluginInfo.Instance.GetType().Assembly.GetType(IntegrationTypeName, true);
                RegisterDelegate register = CreateDelegate<RegisterDelegate>(integrationType, "RegisterSingleBuyGuards");
                unregister = CreateDelegate<UnregisterDelegate>(integrationType, "UnregisterSingleBuyGuards");
                register(beginCallback, endCallback);
                registered = true;
                Shared.DebugLogHelper.LogDebug(log, "Extra Features registered market-purchase guards with Bugfixes and QoL.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Extra Features market-purchase guard integration failed; purchased goods may be treated as gained goods: {ex}");
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
