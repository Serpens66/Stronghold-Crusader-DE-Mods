using Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
namespace SerpsModsHost
{
    internal sealed class StatsTweakerConfigurationProvider : IDynamicPresetSettingsProvider, IModSettingsApplicationBackend, INetworkModSettingsApplicationBackend
    {
        private readonly Type api;
        private readonly Action<string> diagnostics;
        private int ownReads, validations;
        internal string PerformanceCounts => "ownReads=" + ownReads + ", validations=" + validations;
        private static readonly ConcurrentDictionary<Tuple<Type, string>, PropertyInfo> properties = new ConcurrentDictionary<Tuple<Type, string>, PropertyInfo>();
        private readonly Dictionary<string, MethodInfo> methods = new Dictionary<string, MethodInfo>();
        private readonly List<DynamicPresetSetting> options = new List<DynamicPresetSetting>();
        private Dictionary<string, object> working;
        private Exception synchronizationFailure;
        internal string OwnRevision { get; private set; }
        internal bool IsReady { get; }

        internal StatsTweakerConfigurationProvider(Type api, Action<string> diagnostics = null)
        {
            this.api = api; this.diagnostics = diagnostics;
            if (api.GetProperty("ApiVersion", BindingFlags.Public | BindingFlags.Static)?.PropertyType != typeof(int) ||
                (int)api.GetProperty("ApiVersion").GetValue(null) != 1)
                throw new NotSupportedException("Unsupported Tweaker configuration API version.");
            foreach (string name in new[] { "GetCapabilities", "GetOptions", "ReadOwnConfiguration", "GetLoadedConfiguration", "GetPendingConfiguration", "DiscardPendingConfiguration" })
                RequireMethod(name, Type.EmptyTypes);
            RequireMethod("ValidateConfiguration", new[] { typeof(IDictionary<string, object>) });
            RequireMethod("StageConfiguration", new[] { typeof(IDictionary<string, object>), typeof(string) });
            object capabilities = Call("GetCapabilities");
            ValidateContract();
            bool immediate = Read<bool>(capabilities, "CanApplyWithoutRestart");
            RequireMethod("StageContextConfiguration", new[] { typeof(IDictionary<string, object>), typeof(string), typeof(string) });
            RequireMethod("StageReturnToOwnConfiguration", Type.EmptyTypes);
            RequireMethod("IsNetworkConfigurationClient", Type.EmptyTypes);
            RequireMethod("PrepareNetworkConfiguration", Type.EmptyTypes);
            RequireMethod("EnableRestartManagedSynchronization", Type.EmptyTypes);
            if (immediate)
            {
                RequireMethod("ApplyConfiguration", new[] { typeof(IDictionary<string, object>), typeof(string) });
                RequireMethod("GetActiveConfiguration", Type.EmptyTypes);
            }
            IsReady = Read<bool>(capabilities, "IsReady") && (immediate || Read<bool>(capabilities, "CanStageForNextStart"));
            if (!IsReady) return;
            if (!Read<bool>(capabilities, "CanStageContextConfigurations"))
                throw new NotSupportedException("Tweaker does not support isolated context configurations.");
            var keys = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (object option in (IEnumerable)Call("GetOptions"))
            {
                string type = Read<string>(option, "ValueType");
                Type valueType = type == "Boolean" ? typeof(bool) : type == "Integer" ? typeof(long) :
                    type == "Number" ? typeof(double) : type == "String" ? typeof(string) : null;
                if (valueType == null) throw new InvalidDataException("Unknown configuration value type: " + type);
                bool supported = Read<bool>(option, "IsSupported");
                string key = Read<string>(option, "Key");
                if (string.IsNullOrWhiteSpace(key) || key.Length > 256 || !keys.Add(key) || keys.Count > 16384)
                    throw new InvalidDataException("Invalid or oversized Tweaker option catalog.");
                if (Read<object>(option, "DefaultValue")?.GetType() != valueType)
                    throw new InvalidDataException("Invalid Tweaker option default: " + key);
                options.Add(new DynamicPresetSetting
                {
                    Key = Read<string>(option, "Key"), ValueType = valueType,
                    DefaultValue = Read<object>(option, "DefaultValue"),
                    RequiresRestart = option.GetType().GetProperty("RequiresRestart") == null ? !immediate : Read<bool>(option, "RequiresRestart"),
                    Scope = Read<bool>(option, "IsLocal") ? PresetSettingScope.Local : PresetSettingScope.Host,
                    Group = Read<string>(option, "File") + " / " + Read<string>(option, "Group"),
                    DisplayName = Read<string>(option, "Group") + " / " + Read<string>(option, "Name") + (supported ? "" : " [" + Read<string>(option, "Notice") + "]")
                });
            }
            if (options.Count == 0) throw new InvalidDataException("Tweaker option catalog is empty.");
            if (!immediate && options.Any(x => !x.RequiresRestart))
                throw new InvalidDataException("Tweaker advertises live settings without an immediate application capability.");
            working = ReadOwn();
        }

        private void ValidateContract()
        {
            RequireProperties("ConfigurationCapabilities", typeof(bool), "IsReady", "CanApplyWithoutRestart", "CanStageForNextStart", "CanStageContextConfigurations");
            RequireProperties("ConfigurationSnapshot", typeof(string), "Revision", "ContextId", "Source");
            RequireProperties("ConfigurationSnapshot", typeof(Dictionary<string, object>), "Values");
            RequireProperties("ConfigurationOption", typeof(string), "Key", "Name", "Group", "File", "ValueType", "Notice");
            RequireProperties("ConfigurationOption", typeof(bool), "RequiresRestart", "IsSupported", "IsLocal");
            RequireProperties("ConfigurationOption", typeof(object), "DefaultValue");
            RequireProperties("ConfigurationProblem", typeof(string), "Key", "Message");
        }
        private void RequireProperties(string contract, Type expected, params string[] names)
        {
            Type type = api.Assembly.GetType(api.Namespace + "." + contract, true);
            foreach (string name in names)
            {
                var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (property?.GetGetMethod() == null || property.PropertyType != expected || property.GetIndexParameters().Length != 0)
                    throw new InvalidDataException("Incompatible Tweaker API property: " + contract + "." + name);
            }
        }

        private void RequireMethod(string name, Type[] parameters)
        {
            MethodInfo method = api.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, parameters, null);
            if (method == null) throw new MissingMethodException(api.FullName, name);
            Type expected;
            switch (name)
            {
                case "IsNetworkConfigurationClient":
                case "PrepareNetworkConfiguration": expected = typeof(bool); break;
                case "StageConfiguration":
                case "EnableRestartManagedSynchronization":
                case "StageContextConfiguration":
                case "StageReturnToOwnConfiguration":
                case "ApplyConfiguration":
                case "DiscardPendingConfiguration": expected = typeof(void); break;
                case "GetCapabilities": expected = api.Assembly.GetType(api.Namespace + ".ConfigurationCapabilities", true); break;
                case "GetOptions": expected = api.Assembly.GetType(api.Namespace + ".ConfigurationOption", true).MakeArrayType(); break;
                case "ValidateConfiguration": expected = api.Assembly.GetType(api.Namespace + ".ConfigurationProblem", true).MakeArrayType(); break;
                default: expected = api.Assembly.GetType(api.Namespace + ".ConfigurationSnapshot", true); break;
            }
            if (method.ReturnType != expected) throw new InvalidDataException("Incompatible Tweaker API return type: " + name);
            methods.Add(name, method);
        }
        internal object Call(string name, params object[] args) => methods[name].Invoke(null, args);
        internal static T Read<T>(object value, string property)
        {
            if (value == null) throw new InvalidDataException("Missing Tweaker API value: " + property);
            PropertyInfo info = properties.GetOrAdd(Tuple.Create(value.GetType(), property), key =>
                key.Item1.GetProperty(key.Item2, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidDataException("Missing Tweaker API property: " + key.Item2));
            if (info == null || !info.CanRead || !typeof(T).IsAssignableFrom(info.PropertyType))
                throw new InvalidDataException("Incompatible Tweaker API property: " + property);
            return (T)info.GetValue(value);
        }
        internal Dictionary<string, object> ReadOwn()
        {
            var watch = Stopwatch.StartNew();
            ownReads++;
            object snapshot = Call("ReadOwnConfiguration");
            var values = new Dictionary<string, object>(Read<Dictionary<string, object>>(snapshot, "Values"), StringComparer.Ordinal);
            ValidateValues(values);
            OwnRevision = Read<string>(snapshot, "Revision");
            diagnostics?.Invoke("[PresetPerf] read personal configuration: ms=" + watch.Elapsed.TotalMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ", " + PerformanceCounts);
            return values;
        }
        public IReadOnlyList<DynamicPresetSetting> GetSettings() => options;
        public object ReadValue(string key) => working[key];
        public Dictionary<string, object> ReadValues() => new Dictionary<string, object>(working, StringComparer.Ordinal);
        public void ValidateValues(Dictionary<string, object> values)
        {
            var watch = Stopwatch.StartNew();
            validations++;
            string[] errors = ((IEnumerable)Call("ValidateConfiguration", values)).Cast<object>()
                .Select(item => Read<string>(item, "Key") + ": " + Read<string>(item, "Message")).ToArray();
            diagnostics?.Invoke("[PresetPerf] validate configuration: options=" + values.Count + ", ms=" + watch.Elapsed.TotalMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            if (errors.Length != 0) throw new InvalidDataException(string.Join("\n", errors.Take(8)));
        }
        public void ReplaceValues(Dictionary<string, object> values)
        {
            // An equal copy of our already validated snapshot needs no second validation.
            if (working != null && values != null && values.Count == working.Count &&
                values.All(x => working.TryGetValue(x.Key, out var current) && Equals(current, x.Value))) return;
            ValidateValues(values);
            working = new Dictionary<string, object>(values, StringComparer.Ordinal);
        }
        internal void Discard() => Call("DiscardPendingConfiguration");
        public bool IsNetworkConfigurationClient => (bool)Call("IsNetworkConfigurationClient");
        internal void EnableRestartManagedSynchronization()
        {
            try { Call("EnableRestartManagedSynchronization"); }
            catch (Exception ex)
            {
                synchronizationFailure = ex.GetBaseException();
                throw;
            }
        }
        private void RequireValidStartupEvidence()
        {
            if (synchronizationFailure != null)
                throw new InvalidOperationException(synchronizationFailure.Message, synchronizationFailure);
        }
        public bool PrepareNetworkConfiguration()
        {
            RequireValidStartupEvidence();
            return (bool)Call("PrepareNetworkConfiguration");
        }
        public Dictionary<string, object> ReadDesiredValues() => ReadValues();
        public void ReplaceDesiredValues(Dictionary<string, object> values) => ReplaceValues(values);
        public Dictionary<string, object> ReadOwnValues() => ReadOwn();
        public Dictionary<string, object> ReadActiveValues()
        {
            RequireValidStartupEvidence();
            return Read<Dictionary<string, object>>(Call(methods.ContainsKey("GetActiveConfiguration") ? "GetActiveConfiguration" : "GetLoadedConfiguration"), "Values");
        }
        public Dictionary<string, object> ReadPendingValues()
        {
            object pending = Call("GetPendingConfiguration");
            return pending == null ? null : Read<Dictionary<string, object>>(pending, "Values");
        }
        public string ActiveContextId => Read<string>(Call(methods.ContainsKey("GetActiveConfiguration") ? "GetActiveConfiguration" : "GetLoadedConfiguration"), "ContextId");
        public void StageValues(Dictionary<string, object> values, string contextId)
        {
            ValidateValues(values);
            if (string.IsNullOrEmpty(contextId)) Call("StageConfiguration", values, OwnRevision);
            else Call("StageContextConfiguration", values, contextId, OwnRevision);
        }
        public void ApplyValues(Dictionary<string, object> values, string contextId)
        {
            if (!methods.ContainsKey("ApplyConfiguration")) throw new NotSupportedException("Tweaker cannot apply configuration without a restart.");
            Call("ApplyConfiguration", values, contextId);
        }
        public void ReturnToOwnConfiguration() => Call("StageReturnToOwnConfiguration");
        public void DiscardPendingConfiguration() => Discard();
    }
}
