using BepInEx.Bootstrap;
using BepInEx.Logging;
using Noesis;
using Shared;
using SHCDESE.API;
using System;
using System.IO;
using Path = System.IO.Path;
using System.Linq;
using System.Diagnostics;

namespace SerpsModsHost
{
    // Optional integration: no Tweaker assembly reference and no second host transport.
    internal static class StatsTweakerPresetAdapter
    {
        internal const string TargetGuid = "CrusaderDETweaker";
        private static readonly OptionalSettingsIntegration<StatsTweakerPresetViewModel> integration = new OptionalSettingsIntegration<StatsTweakerPresetViewModel>();
        private static Type observedApi;

        internal static void TryAttach(string storageAssembly, ManualLogSource log)
        {
            bool installed = Chainloader.PluginInfos.TryGetValue(TargetGuid, out var plugin);
            Type api = plugin?.Instance?.GetType().Assembly.GetType("CrusaderDETweaker.Configuration.ConfigurationApi", false);
            integration.Discover(installed, api, () =>
            {
                var phase = Stopwatch.StartNew();
                var provider = new StatsTweakerConfigurationProvider(api, message => log.LogInfo(message));
                if (observedApi != api)
                {
                    var changed = api.GetEvent("Changed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (changed?.EventHandlerType != typeof(Action)) throw new NotSupportedException("Tweaker readiness notification is unavailable.");
                    changed.AddEventHandler(null, (Action)SerpsModsHostPlugin.QueueModSettingsSort);
                    observedApi = api;
                }

                if (!provider.IsReady) return null;
                var candidate = new StatsTweakerPresetViewModel(provider);
                var registration = LobbyModSettingsPresetRegistration.PrepareExternalWorkingCopy(log, storageAssembly,
                    TargetGuid, TargetGuid, plugin.Metadata.Version, candidate);
                registration.Activate(provider.EnableRestartManagedSynchronization,
                    error => error.GetBaseException() is InvalidOperationException);
                log.LogInfo("[PresetPerf] provider activation: options=" + provider.GetSettings().Count + ", ms=" +
                    phase.Elapsed.TotalMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ", " + provider.PerformanceCounts);
                return candidate;
            }, error => log.LogWarning("[TweakerPresets] Optional integration unavailable: " + error.GetBaseException().Message));
            integration.Attach(viewModel =>
            {
                var entry = GameXAMLManagerAPI.Instance.RegisteredModSettings.FirstOrDefault(item =>
                    item.ViewModel?.GetType().Assembly == api.Assembly);
                if (!(entry?.View is Grid root)) return false;
                var scroll = root.Children.OfType<ScrollViewer>().SingleOrDefault();
                if (!(scroll?.Content is StackPanel parent)) return false;
                string xaml = Path.Combine(Path.GetDirectoryName(storageAssembly), "Override", "ScriptExtenderUI", "StatsTweakerPresets.xaml");
                var uiWatch = Stopwatch.StartNew();
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
                panel.DataContext = viewModel;
                LobbyModSettingsPresetRegistration.AttachExternalView(panel, log, TargetGuid);
                scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                parent.Children.Insert(0, panel);
                log.LogInfo("[PresetPerf] preset panel: ms=" + uiWatch.Elapsed.TotalMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                log.LogInfo("[TweakerPresets] Optional configuration integration attached.");
                return true;
            }, error => log.LogWarning("[TweakerPresets] Preset page unavailable; configuration guards remain active: " + error.GetBaseException().Message));
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

}
