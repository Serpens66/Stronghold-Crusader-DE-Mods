using Noesis;
using SHCDESE.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Path = System.IO.Path;

namespace ExtendedData
{
    internal sealed partial class TrailMissionSettingsCoordinator
    {
        private const string MediaExportMarker = "lordmedia.enabled";
        private CheckBox lordMediaExportCheckbox;
        private static bool IsMakerMediaEnabled() => File.Exists(Path.Combine(ConfigSettings.GetUserTrailMakerPath(), MediaExportMarker));
        private static void SetMakerMediaEnabled(bool enabled) => SetMediaExportMarker(ConfigSettings.GetUserTrailMakerPath(), enabled);
        private static void SetMediaExportMarker(string root, bool enabled)
        {
            string path = Path.Combine(root, MediaExportMarker);
            if (enabled) { Directory.CreateDirectory(root); File.WriteAllText(path, "enabled\r\n", new UTF8Encoding(false)); }
            else if (File.Exists(path)) File.Delete(path);
        }
        private void AddLordMediaCheckbox(Panel host, StackPanel optionRow)
        {
            var row = new StackPanel { Orientation = Orientation.Vertical, Margin = optionRow.Margin,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
            host.Children.Remove(optionRow); optionRow.Margin = new Thickness(0); row.Children.Add(optionRow);
            var tip = new ToolTip { Background = new SolidColorBrush(Color.FromArgb(255,29,23,16)),
                Foreground = new SolidColorBrush(Colors.White), BorderBrush = new SolidColorBrush(Color.FromArgb(255,242,212,138)),
                BorderThickness = new Thickness(1), FontSize = Shared.ToolTipPresentation.FontSize,
                MaxWidth = Shared.ToolTipPresentation.MaximumWidth,
                Content = SerpLocalization.Get("ExtendedData.EmbedLordMediaHelp") };
            // The Maker is outside the modsettings resource tree. Explicitly replace Vanilla's popup template.
            tip.Template = (ControlTemplate)GUI.ParseXaml(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ToolTip}'>
                <Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}' MaxWidth='{TemplateBinding MaxWidth}' CornerRadius='2'>
                    <TextBlock Text='{TemplateBinding Content}' Foreground='{TemplateBinding Foreground}' FontSize='{TemplateBinding FontSize}' TextWrapping='Wrap'/>
                </Border></ControlTemplate>");
            ToolTipResolutionScale.SetEnabled(tip, true);
            lordMediaExportCheckbox = new CheckBox { Content = SerpLocalization.Get("ExtendedData.EmbedLordMedia"),
                IsChecked = IsMakerMediaEnabled(), Style = coopTrailExportCheckbox.Style,
                Foreground = new SolidColorBrush(Colors.Black), HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0,4,0,0), ToolTip = tip };
            ToolTipService.SetShowDuration(lordMediaExportCheckbox, 60000);
            lordMediaExportCheckbox.Click += (_, __) => {
                try { if (EditorEnabled) SetMakerMediaEnabled(lordMediaExportCheckbox.IsChecked == true); }
                catch (Exception error) { Shared.DebugLogHelper.LogErrorLimited(log, "lord-media-export-option", error.ToString()); }
                lordMediaExportCheckbox.IsChecked = IsMakerMediaEnabled();
            };
            row.Children.Add(lordMediaExportCheckbox); host.Children.Add(row);
        }
        private static void CopyLordPresentationSources(string source, string destination)
        {
            if (!Directory.Exists(source)) return;
            var values = new List<LordPresentationData>();
            foreach (string trail in Directory.GetFiles(source, "*.trail"))
            {
                var requirements = TrailLordRequirements.Read(trail);
                if (requirements == null) continue;
                var presentations = requirements.Slots.Select(x => x.Presentation).Where(x => x != null).ToArray();
                TrailLordMediaStore.BindAndVerify(presentations, source); values.AddRange(presentations);
            }
            TrailLordMediaStore.Stage(values, destination);
            string marker = Path.Combine(source, MediaExportMarker), target = Path.Combine(destination, MediaExportMarker);
            if (File.Exists(marker)) File.Copy(marker, target, true);
            else if (File.Exists(target)) File.Delete(target);
        }
        private string LordMediaUploadSummary(string source)
        {
            try
            {
                string missions = File.Exists(Path.Combine(source, "cooptrail.json")) ? Path.Combine(source, "CoopMissions") : source;
                var values = new List<LordPresentationData>();
                if (Directory.Exists(missions)) foreach (string path in Directory.GetFiles(missions))
                {
                    if (!path.EndsWith(".trail", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".coopmission.json", StringComparison.OrdinalIgnoreCase)) continue;
                    var requirements = TrailLordRequirements.Read(path);
                    if (requirements != null) values.AddRange(requirements.Slots.Select(x => x.Presentation).Where(x => x != null));
                }
                return SerpLocalization.Get(values.Any(x => x.MediaId != null && x.MediaHashes?.Count > 0)
                    ? "ExtendedData.LordMediaIncluded" : values.Any(x => x.MetadataJson != null)
                    ? "ExtendedData.LordMetadataOnly" : "ExtendedData.LordMetadataLegacy");
            }
            catch (Exception error)
            {
                Shared.DebugLogHelper.LogWarningLimited(log, "trail-upload-lord-summary", error.Message);
                return SerpLocalization.Get("ExtendedData.ErrorPackageInvalid");
            }
        }
    }
}
