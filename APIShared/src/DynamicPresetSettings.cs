#pragma warning disable 1591
using System;
using System.Collections.Generic;
using System.Reflection;
using SHCDESE.API.Components.Network;

namespace Shared
{
    /// <summary>Options supplied by an optional external configuration API.</summary>
    public sealed class DynamicPresetSetting
    {
        public string Key { get; set; }
        public Type ValueType { get; set; }
        public object DefaultValue { get; set; }
        public PresetSettingScope Scope { get; set; }
        public string Group { get; set; }
        public string DisplayName { get; set; }
        public bool RequiresRestart { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public sealed class RequiresRestartAttribute : Attribute { }

    /// <summary>Applies a complete configuration, separately from its editable working copy.</summary>
    public interface IModSettingsApplicationBackend
    {
        Dictionary<string, object> ReadDesiredValues();
        void ReplaceDesiredValues(Dictionary<string, object> values);
        Dictionary<string, object> ReadActiveValues();
        Dictionary<string, object> ReadOwnValues();
        Dictionary<string, object> ReadPendingValues();
        string ActiveContextId { get; }
        void StageValues(Dictionary<string, object> values, string contextId);
        void ApplyValues(Dictionary<string, object> values, string contextId);
        void ReturnToOwnConfiguration();
        void DiscardPendingConfiguration();
    }

    /// <summary>Optional admission check owned by a mod's existing authenticated host transport.</summary>
    public interface INetworkModSettingsApplicationBackend
    {
        bool IsNetworkConfigurationClient { get; }
        bool PrepareNetworkConfiguration();
    }

    /// <summary>Whole-snapshot operations on a local working copy; never a second network transport.</summary>
    public interface IDynamicPresetSettingsProvider
    {
        IReadOnlyList<DynamicPresetSetting> GetSettings();
        Dictionary<string, object> ReadValues();
        object ReadValue(string key);
        void ValidateValues(Dictionary<string, object> values);
        void ReplaceValues(Dictionary<string, object> values);
    }

    internal sealed class PresetPropertyAccessor
    {
        private readonly PropertyInfo reflected;
        private readonly DynamicPresetSetting dynamic;
        private readonly IDynamicPresetSettingsProvider provider;
        internal PresetPropertyAccessor(PropertyInfo property) { reflected = property; }
        internal PresetPropertyAccessor(DynamicPresetSetting setting, IDynamicPresetSettingsProvider source)
        {
            dynamic = setting;
            provider = source;
            if (string.IsNullOrWhiteSpace(setting.Key) || setting.Key.Length > 256 || setting.ValueType == null ||
                setting.DefaultValue == null || setting.DefaultValue.GetType() != setting.ValueType ||
                setting.Scope == PresetSettingScope.Player)
                throw new ArgumentException("Invalid dynamic setting descriptor: " + setting.Key);
        }
        internal string Group => dynamic?.Group ?? string.Empty;
        internal string DisplayName => dynamic?.DisplayName ?? Name;
        internal string Name => reflected?.Name ?? dynamic.Key;
        internal Type PropertyType => reflected?.PropertyType ?? dynamic.ValueType;
        internal bool CanRead => reflected?.CanRead ?? true;
        internal bool CanWrite => reflected?.CanWrite ?? true;
        internal bool IsDynamic => dynamic != null;
        internal bool RequiresRestart => dynamic?.RequiresRestart ?? reflected.IsDefined(typeof(RequiresRestartAttribute), true);
        internal object DefaultValue => dynamic?.DefaultValue;
        internal object GetValue(object owner) => reflected != null ? reflected.GetValue(owner) : provider.ReadValue(Name);
        internal void SetValue(object owner, object value)
        {
            if (reflected == null) throw new InvalidOperationException("Dynamic settings require a complete snapshot.");
            reflected.SetValue(owner, value);
            // Dynamic values are validated and replaced together by the preset controller.
        }
        internal T GetCustomAttribute<T>() where T : Attribute
        {
            if (reflected != null) return reflected.GetCustomAttribute<T>();
            if (typeof(T) == typeof(SyncHostOnlyAttribute) && dynamic.Scope == PresetSettingScope.Host)
                return (T)(Attribute)new SyncHostOnlyAttribute();
            if (typeof(T) == typeof(PresetLocalAttribute) && dynamic.Scope == PresetSettingScope.Local)
                return (T)(Attribute)new PresetLocalAttribute();
            return null;
        }
    }
}
