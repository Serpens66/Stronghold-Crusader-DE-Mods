using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using APIShared.ModSettings;
using CrusaderDETweaker.Configuration;

namespace CrusaderDETweaker.Presets
{
    // The working copy is UI state only. APIShared owns preset modes and mission preparation;
    // ConfigurationApi owns validation, persistence, actual application and host transport.
    internal sealed class DirectPresetProvider : IDynamicPresetSettingsProvider,
        IModSettingsApplicationBackend, INetworkModSettingsApplicationBackend
    {
        private readonly DynamicPresetSetting[] settings;
        private Dictionary<string, object> working;
        private string ownRevision;

        internal DirectPresetProvider()
        {
            settings = ConfigurationApi.GetOptions().Select(option => new DynamicPresetSetting
            {
                Key = option.Key,
                ValueType = option.ValueType == "Boolean" ? typeof(bool) : option.ValueType == "Integer" ? typeof(long) :
                    option.ValueType == "Number" ? typeof(double) : typeof(string),
                DefaultValue = option.DefaultValue,
                RequiresRestart = option.RequiresRestart,
                Scope = option.IsLocal ? PresetSettingScope.Local : PresetSettingScope.Host,
                Group = option.File + " / " + option.Group,
                DisplayName = option.Group + " / " + option.Name + (option.IsSupported ? "" : " [" + option.Notice + "]")
            }).ToArray();
            working = ReadOwnValues();
        }

        public IReadOnlyList<DynamicPresetSetting> GetSettings() => settings;
        public object ReadValue(string key) => working[key];
        public Dictionary<string, object> ReadValues() => new Dictionary<string, object>(working, StringComparer.Ordinal);
        public void ValidateValues(Dictionary<string, object> values)
        {
            var errors = ConfigurationApi.ValidateConfiguration(values);
            if (errors.Length != 0) throw new InvalidDataException(string.Join("\n", errors.Take(8).Select(x => x.ToString())));
        }
        public void ReplaceValues(Dictionary<string, object> values)
        {
            if (working != null && values != null && values.Count == working.Count &&
                values.All(x => working.TryGetValue(x.Key, out var current) && Equals(current, x.Value))) return;
            ValidateValues(values);
            working = new Dictionary<string, object>(values, StringComparer.Ordinal);
        }

        public Dictionary<string, object> ReadDesiredValues() => ReadValues();
        public void ReplaceDesiredValues(Dictionary<string, object> values) => ReplaceValues(values);
        public Dictionary<string, object> ReadOwnValues()
        {
            var own = ConfigurationApi.ReadOwnConfiguration();
            ValidateValues(own.Values);
            ownRevision = own.Revision;
            return own.Values;
        }
        public Dictionary<string, object> ReadActiveValues() => ConfigurationApi.GetActiveConfiguration().Values;
        public Dictionary<string, object> ReadPendingValues() => ConfigurationApi.GetPendingConfiguration()?.Values;
        public string ActiveContextId => ConfigurationApi.GetActiveConfiguration().ContextId;
        public void StageValues(Dictionary<string, object> values, string contextId)
        {
            if (string.IsNullOrEmpty(contextId)) ConfigurationApi.StageConfiguration(values, ownRevision);
            else ConfigurationApi.StageContextConfiguration(values, contextId, ownRevision);
        }
        public void ApplyValues(Dictionary<string, object> values, string contextId) =>
            ConfigurationApi.ApplyLiveConfiguration(values, contextId, ownRevision);
        public void ReturnToOwnConfiguration()
        {
            var own = ReadOwnValues();
            var active = ReadActiveValues();
            if (settings.Where(x => x.RequiresRestart).All(x => Equals(own[x.Key], active[x.Key])))
                ApplyValues(own, "");
            else ConfigurationApi.StageReturnToOwnConfiguration();
        }
        public void DiscardPendingConfiguration() => ConfigurationApi.DiscardPendingConfiguration();
        public bool IsNetworkConfigurationClient => ConfigurationApi.IsNetworkConfigurationClient();
        public bool PrepareNetworkConfiguration() => ConfigurationApi.PrepareNetworkConfiguration();
    }
}
