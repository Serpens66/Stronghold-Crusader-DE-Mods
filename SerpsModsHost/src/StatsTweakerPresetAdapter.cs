using BepInEx.Bootstrap;
using BepInEx.Logging;
using Noesis;
using Shared;
using SHCDESE.API;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using System.Linq;
using System.Reflection;

namespace SerpsModsHost
{
    // Optional integration: no Tweaker assembly reference and no second host transport.
    internal static class StatsTweakerPresetAdapter
    {
        internal const string TargetGuid = "CrusaderDETweaker";
        private static StatsTweakerPresetViewModel viewModel;
        private static bool failed;

        internal static void TryAttach(string storageAssembly, ManualLogSource log)
        {
            if (viewModel != null || failed || !Chainloader.PluginInfos.TryGetValue(TargetGuid, out var plugin)) return;
            Type api = plugin.Instance.GetType().Assembly.GetType("CrusaderDETweaker.Configuration.ConfigurationApi", false);
            if (api == null) return; // An older Tweaker remains fully independent.
            try
            {
                var provider = new StatsTweakerConfigurationProvider(api);
                if (!provider.IsReady) return;
                var entry = GameXAMLManagerAPI.Instance.RegisteredModSettings.FirstOrDefault(item =>
                    item.ViewModel?.GetType().Assembly == api.Assembly);
                if (!(entry?.View is Grid root)) return;
                var scroll = root.Children.OfType<ScrollViewer>().SingleOrDefault();
                if (!(scroll?.Content is StackPanel parent)) return;
                string xaml = Path.Combine(Path.GetDirectoryName(storageAssembly), "Override", "ScriptExtenderUI", "StatsTweakerPresets.xaml");
                FrameworkElement panel;
                using (var stream = File.OpenRead(xaml)) panel = GUI.LoadXaml(stream, xaml) as FrameworkElement;
                if (panel == null) throw new InvalidDataException("Preset panel could not be loaded.");
                // Horizontal scrolling measures the content with unlimited width. Constrain our
                // panel to the viewport instead, including the original page's content margins.
                panel.HorizontalAlignment = HorizontalAlignment.Left;
                panel.SetBinding(FrameworkElement.WidthProperty, new Binding("ViewportWidth")
                {
                    Source = scroll, Mode = BindingMode.OneWay,
                    Converter = PresetViewportWidthConverter.Instance,
                    ConverterParameter = parent.Margin.Left + parent.Margin.Right
                });
                var candidate = new StatsTweakerPresetViewModel(provider);
                panel.DataContext = candidate;
                LobbyModSettingsPresetRegistration.AttachExternalWorkingCopy(log, storageAssembly, TargetGuid,
                    TargetGuid, plugin.Metadata.Version, candidate, panel);
                candidate.ImportOwnFiles();
                candidate.InitializeSelection();
                scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                parent.Children.Insert(0, panel);
                viewModel = candidate; // Root commands and working copy for the process lifetime.
                log.LogInfo("[TweakerPresets] Configuration API v1 attached; presets require a game restart.");
            }
            catch (Exception ex)
            {
                failed = true;
                log.LogError("[TweakerPresets] Optional preset integration unavailable: " + ex.GetBaseException().Message);
            }
        }
    }

    internal sealed class PresetViewportWidthConverter : IValueConverter
    {
        internal static readonly PresetViewportWidthConverter Instance = new PresetViewportWidthConverter();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            double width = System.Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
            double margin = System.Convert.ToDouble(parameter, System.Globalization.CultureInfo.InvariantCulture);
            return double.IsNaN(width) || double.IsInfinity(width) ? 0f : (float)Math.Max(0, width - margin);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException("Viewport width is a one-way binding.");
    }

    internal sealed class StatsTweakerConfigurationProvider : IDynamicPresetSettingsProvider
    {
        private readonly Type api;
        private readonly Dictionary<string, MethodInfo> methods = new Dictionary<string, MethodInfo>();
        private readonly List<DynamicPresetSetting> options = new List<DynamicPresetSetting>();
        private Dictionary<string, object> working;
        internal string OwnRevision { get; private set; }
        internal bool IsReady { get; }

        internal StatsTweakerConfigurationProvider(Type api)
        {
            this.api = api;
            if (api.GetProperty("ApiVersion", BindingFlags.Public | BindingFlags.Static)?.PropertyType != typeof(int) ||
                (int)api.GetProperty("ApiVersion").GetValue(null) != 1)
                throw new NotSupportedException("Unsupported Tweaker configuration API version.");
            foreach (string name in new[] { "GetCapabilities", "GetOptions", "ReadOwnConfiguration", "GetLoadedConfiguration", "GetPendingConfiguration", "DiscardPendingConfiguration" })
                RequireMethod(name, Type.EmptyTypes);
            RequireMethod("ValidateConfiguration", new[] { typeof(IDictionary<string, object>) });
            RequireMethod("StageConfiguration", new[] { typeof(IDictionary<string, object>), typeof(string) });
            object capabilities = Call("GetCapabilities");
            IsReady = Read<bool>(capabilities, "IsReady") && Read<bool>(capabilities, "CanStageForNextStart");
            if (!IsReady) return;
            foreach (object option in (IEnumerable)Call("GetOptions"))
            {
                string type = Read<string>(option, "ValueType");
                Type valueType = type == "Boolean" ? typeof(bool) : type == "Integer" ? typeof(long) :
                    type == "Number" ? typeof(double) : type == "String" ? typeof(string) : null;
                if (valueType == null) throw new InvalidDataException("Unknown configuration value type: " + type);
                bool supported = Read<bool>(option, "IsSupported");
                options.Add(new DynamicPresetSetting
                {
                    Key = Read<string>(option, "Key"), ValueType = valueType,
                    DefaultValue = Read<object>(option, "DefaultValue"),
                    Scope = Read<bool>(option, "IsLocal") ? PresetSettingScope.Local : PresetSettingScope.Host,
                    Group = Read<string>(option, "File") + " / " + Read<string>(option, "Group"),
                    DisplayName = Read<string>(option, "Group") + " / " + Read<string>(option, "Name") + (supported ? "" : " [" + Read<string>(option, "Notice") + "]")
                });
            }
            var own = ReadOwn();
            ValidateValues(own);
            working = own;
        }

        private void RequireMethod(string name, Type[] parameters)
        {
            MethodInfo method = api.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, parameters, null);
            if (method == null) throw new MissingMethodException(api.FullName, name);
            Type expected;
            switch (name)
            {
                case "StageConfiguration":
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
            PropertyInfo info = value?.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
            if (info == null || !info.CanRead || !typeof(T).IsAssignableFrom(info.PropertyType))
                throw new InvalidDataException("Incompatible Tweaker API property: " + property);
            return (T)info.GetValue(value);
        }
        internal Dictionary<string, object> ReadOwn()
        {
            object snapshot = Call("ReadOwnConfiguration");
            var values = new Dictionary<string, object>(Read<Dictionary<string, object>>(snapshot, "Values"), StringComparer.Ordinal);
            ValidateValues(values);
            OwnRevision = Read<string>(snapshot, "Revision");
            return values;
        }
        public IReadOnlyList<DynamicPresetSetting> GetSettings() => options;
        public object ReadValue(string key) => working[key];
        public Dictionary<string, object> ReadValues() => new Dictionary<string, object>(working, StringComparer.Ordinal);
        public void ValidateValues(Dictionary<string, object> values)
        {
            string[] errors = ((IEnumerable)Call("ValidateConfiguration", values)).Cast<object>()
                .Select(item => Read<string>(item, "Key") + ": " + Read<string>(item, "Message")).ToArray();
            if (errors.Length != 0) throw new InvalidDataException(string.Join("\n", errors.Take(8)));
        }
        public void ReplaceValues(Dictionary<string, object> values)
        {
            ValidateValues(values);
            working = new Dictionary<string, object>(values, StringComparer.Ordinal);
        }
        internal string Describe(string method)
        {
            object snapshot = Call(method);
            return snapshot == null ? "—" : Read<string>(snapshot, "Revision").Substring(0, 12);
        }
        internal void Stage() => Call("StageConfiguration", ReadValues(), OwnRevision);
        internal void StageValues(Dictionary<string, object> values)
        {
            ValidateValues(values);
            Call("StageConfiguration", values, OwnRevision);
        }
        internal void Discard() => Call("DiscardPendingConfiguration");
    }
}
