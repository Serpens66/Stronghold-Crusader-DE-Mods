using BepInEx.Bootstrap;
using CoopTrailReplacer.Core;
using System;
using System.Reflection;

namespace CoopTrailReplacer
{
    internal sealed class StartConditionsBridge
    {
        public const string Owner = "CoopTrailReplacer_Serp";
        private readonly Type integrationType;
        private readonly Type overrideType;
        private readonly MethodInfo setMethod;
        private readonly MethodInfo clearMethod;
        private readonly StartConditions.StartConditionsOverrideSettings fallbackSettings;
        private readonly StartConditions.StartConditionsRuntime fallbackRuntime;

        public StartConditionsBridge(BepInEx.Logging.ManualLogSource log)
        {
            if (Chainloader.PluginInfos.TryGetValue("StartConditions_Serp", out var pluginInfo))
            {
                Assembly assembly = pluginInfo.Instance.GetType().Assembly;
                integrationType = assembly.GetType("StartConditions.StartConditionsIntegration", true);
                overrideType = assembly.GetType("StartConditions.StartConditionsOverrideSettings", true);
                setMethod = integrationType.GetMethod("SetMissionOverride", BindingFlags.Public | BindingFlags.Static);
                clearMethod = integrationType.GetMethod("ClearMissionOverride", BindingFlags.Public | BindingFlags.Static);
                return;
            }

            fallbackSettings = new StartConditions.StartConditionsOverrideSettings();
            fallbackRuntime = new StartConditions.StartConditionsRuntime(log, fallbackSettings);
            fallbackRuntime.InitializeAfterLibraryLoaded();
        }

        public bool UsesInstalledPlugin => integrationType != null;

        public void Apply(StartConditionsDefinition definition)
        {
            if (definition == null)
                definition = new StartConditionsDefinition();

            if (UsesInstalledPlugin)
            {
                object settings = Activator.CreateInstance(overrideType);
                Set(settings, "EnableMod", true);
                Set(settings, "SetStartGoldAI", definition.SetStartGoldAI);
                Set(settings, "SetStartGoldHuman", definition.SetStartGoldHuman);
                Set(settings, "AddStartGoldAI", definition.AddStartGoldAI);
                Set(settings, "AddStartGoldHuman", definition.AddStartGoldHuman);
                Set(settings, "MultiplyStartTroopsAI", definition.MultiplyStartTroopsAI);
                Set(settings, "MultiplyStartTroopsHuman", definition.MultiplyStartTroopsHuman);
                Set(settings, "StartGoodsAI", MissionLoader.SerializeAmounts(definition.StartGoodsAI));
                Set(settings, "StartGoodsHuman", MissionLoader.SerializeAmounts(definition.StartGoodsHuman));
                Set(settings, "AddStartTroopsAI", MissionLoader.SerializeAmounts(definition.AddStartTroopsAI));
                Set(settings, "AddStartTroopsHuman", MissionLoader.SerializeAmounts(definition.AddStartTroopsHuman));
                setMethod.Invoke(null, new[] { Owner, settings });
                return;
            }

            CopyToFallback(definition);
            StartConditions.StartConditionsIntegration.SetMissionOverride(Owner, fallbackSettings);
        }

        public void Clear()
        {
            if (UsesInstalledPlugin)
                clearMethod.Invoke(null, new object[] { Owner });
            else
                StartConditions.StartConditionsIntegration.ClearMissionOverride(Owner);
        }

        public void Dispose()
        {
            Clear();
            fallbackRuntime?.Dispose();
        }

        private static void Set(object target, string property, object value) =>
            target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public).SetValue(target, value);

        private void CopyToFallback(StartConditionsDefinition definition)
        {
            fallbackSettings.EnableMod = true;
            fallbackSettings.SetStartGoldAI = definition.SetStartGoldAI;
            fallbackSettings.SetStartGoldHuman = definition.SetStartGoldHuman;
            fallbackSettings.AddStartGoldAI = definition.AddStartGoldAI;
            fallbackSettings.AddStartGoldHuman = definition.AddStartGoldHuman;
            fallbackSettings.MultiplyStartTroopsAI = definition.MultiplyStartTroopsAI;
            fallbackSettings.MultiplyStartTroopsHuman = definition.MultiplyStartTroopsHuman;
            fallbackSettings.StartGoodsAI = MissionLoader.SerializeAmounts(definition.StartGoodsAI);
            fallbackSettings.StartGoodsHuman = MissionLoader.SerializeAmounts(definition.StartGoodsHuman);
            fallbackSettings.AddStartTroopsAI = MissionLoader.SerializeAmounts(definition.AddStartTroopsAI);
            fallbackSettings.AddStartTroopsHuman = MissionLoader.SerializeAmounts(definition.AddStartTroopsHuman);
        }
    }

    internal sealed class SomeSettingsBridge : IDisposable
    {
        private readonly Type contextType;
        private readonly EventInfo customizedEvent;
        private readonly Action customizedHandler;

        public SomeSettingsBridge(Action onCustomized)
        {
            if (!Chainloader.PluginInfos.TryGetValue("SomeSettings_Serp", out var pluginInfo))
                return;
            contextType = pluginInfo.Instance.GetType().Assembly.GetType("SomeSettings.CoopTrailLaunchContext", false);
            customizedEvent = contextType?.GetEvent("Customized", BindingFlags.Public | BindingFlags.Static);
            if (customizedEvent != null)
            {
                customizedHandler = onCustomized;
                customizedEvent.AddEventHandler(null, customizedHandler);
            }
        }

        public void Clear() => contextType?.GetMethod("Clear", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);

        public void Dispose()
        {
            if (customizedEvent != null && customizedHandler != null)
                customizedEvent.RemoveEventHandler(null, customizedHandler);
        }
    }
}
