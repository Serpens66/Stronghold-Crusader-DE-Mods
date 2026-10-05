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
        private static StatsTweakerPresetViewModel viewModel;
        private static bool failed, attached;

        internal static void TryAttach(string storageAssembly, ManualLogSource log)
        {
            if (attached || failed || !Chainloader.PluginInfos.TryGetValue(TargetGuid, out var plugin)) return;
            Type api = plugin.Instance.GetType().Assembly.GetType("CrusaderDETweaker.Configuration.ConfigurationApi", false);
            if (api == null) return; // An older Tweaker remains fully independent.
            try
            {
                if (viewModel == null)
                {
                    var phase = Stopwatch.StartNew();
                    var provider = new StatsTweakerConfigurationProvider(api, message => log.LogInfo(message));
                    if (!provider.IsReady) return;
                    log.LogInfo("[PresetPerf] provider ready: options=" + provider.GetSettings().Count + ", ms=" + phase.Elapsed.TotalMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                    phase.Restart();
                    var candidate = new StatsTweakerPresetViewModel(provider);
                    LobbyModSettingsPresetRegistration.RegisterExternalWorkingCopy(log, storageAssembly, TargetGuid,
                        TargetGuid, plugin.Metadata.Version, candidate);
                    log.LogInfo("[PresetPerf] preset activation: ms=" + phase.Elapsed.TotalMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ", " + provider.PerformanceCounts);
                    viewModel = candidate;
                }
                var entry = GameXAMLManagerAPI.Instance.RegisteredModSettings.FirstOrDefault(item =>
                    item.ViewModel?.GetType().Assembly == api.Assembly);
                if (!(entry?.View is Grid root)) return;
                var scroll = root.Children.OfType<ScrollViewer>().SingleOrDefault();
                if (!(scroll?.Content is StackPanel parent)) return;
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
                attached = true; // The participant remains rooted even when its page is unavailable.
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

}
