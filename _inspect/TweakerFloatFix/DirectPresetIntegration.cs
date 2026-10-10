using System;
using System.IO;
using System.Linq;
using APIShared.ModSettings;
using CrusaderDETweaker.Configuration;
using CrusaderDETweaker.Config.Sync;
using Noesis;
using SHCDESE.API;
using Path = System.IO.Path;

namespace CrusaderDETweaker.Presets
{
    internal static class DirectPresetIntegration
    {
        private static DirectPresetViewModel model;
        private static bool queued;

        // Called once after configuration initialization. The existing dispatcher survives
        // startup cleanup; the static participant and Extender registry retain the commands.
        internal static void Initialize()
        {
            if (queued) return;
            queued = true;
            UnityMainThreadDispatcher.EnqueueStatic(Register);
        }

        private static void Register()
        {
            string location = typeof(DirectPresetIntegration).Assembly.Location;
            try
            {
                var candidate = new DirectPresetViewModel(new DirectPresetProvider());
                var registration = LobbyModSettingsPresetRegistration.PrepareExternalWorkingCopy(
                    Plugin.Logger, location, PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_GUID,
                    new Version(PluginInfo.PLUGIN_VERSION), candidate);
                registration.Activate(ConfigurationApi.EnableRestartManagedSynchronization,
                    error => error is InvalidOperationException);
                model = candidate;
            }
            catch (Exception error)
            {
                Plugin.Logger.LogError("[Presets] Registration failed: " + error);
                return;
            }

            // UI failures must not remove an activated participant or its launch guards.
            try
            {
                var entry = GameXAMLManagerAPI.Instance.RegisteredModSettings.Single(x => ReferenceEquals(x.ViewModel, ConfigSyncManager.Lobby));
                var root = entry.View as Grid ?? throw new InvalidDataException("Unexpected lobby view root.");
                var scroll = root.Children.OfType<ScrollViewer>().Single();
                var parent = scroll.Content as StackPanel ?? throw new InvalidDataException("Unexpected lobby content.");
                string path = Path.Combine(Path.GetDirectoryName(location), "Override", "ScriptExtenderUI", "TweakerPresets.xaml");
                FrameworkElement panel;
                using (var stream = File.OpenRead(path))
                    panel = GUI.LoadXaml(stream, path) as FrameworkElement;
                if (panel == null) throw new InvalidDataException("Preset panel could not be loaded.");
                panel.HorizontalAlignment = HorizontalAlignment.Left;
                panel.SetBinding(FrameworkElement.WidthProperty, new Binding("ViewportWidth")
                {
                    Source = scroll,
                    Mode = BindingMode.OneWay,
                    Converter = PresetViewportWidthConverter.Instance,
                    ConverterParameter = parent.Margin.Left + parent.Margin.Right
                });
                panel.DataContext = model;
                LobbyModSettingsPresetRegistration.AttachExternalView(panel, Plugin.Logger, PluginInfo.PLUGIN_GUID);
                parent.Children.Insert(0, panel);
                Plugin.Logger.LogInfo("[Presets] APIShared participant and preset panel registered.");
            }
            catch (Exception error)
            {
                Plugin.Logger.LogError("[Presets] Page unavailable; configuration guards remain active: " + error);
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
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            throw new NotSupportedException("Viewport width is a one-way binding.");
    }
}
