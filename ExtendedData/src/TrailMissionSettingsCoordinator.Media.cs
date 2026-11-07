using Noesis;
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
        private CheckBox CreateLordMediaCheckbox(CheckBox coopCheckbox)
        {
            var checkbox = new CheckBox
            {
                Content = new TextBlock
                {
                    Text = SerpLocalization.Get("ExtendedData.EmbedLordMedia"),
                    TextWrapping = TextWrapping.Wrap,
                },
                IsChecked = IsMakerMediaEnabled(),
                Style = coopCheckbox.Style,
                FontSize = coopCheckbox.FontSize,
                MinHeight = coopCheckbox.Height,
                Foreground = new SolidColorBrush(Colors.Black),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0),
                ToolTip = Shared.MenuToolTip.Create(SerpLocalization.Get("ExtendedData.EmbedLordMediaHelp")),
                Visibility = coopCheckbox.Visibility,
            };
            ToolTipService.SetShowDuration(checkbox, Shared.MenuToolTip.Duration);
            checkbox.Click += (_, __) => {
                try { if (EditorEnabled) SetMakerMediaEnabled(checkbox.IsChecked == true); }
                catch (Exception error) { Shared.DebugLogHelper.LogErrorLimited(log, "lord-media-export-option", error.ToString()); }
                checkbox.IsChecked = IsMakerMediaEnabled();
            };
            return checkbox;
        }
        private static void CopyLordPresentationSources(string source, string destination)
        {
            if (!Directory.Exists(source)) return;
            var values = new List<LordPresentationData>();
            foreach (string trail in Directory.GetFiles(source, "*.trail"))
            {
                // Media hashes are verified below; mission binding is irrelevant for copying them.
                var requirements = TrailLordRequirements.ReadUnbound(trail, out _);
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
