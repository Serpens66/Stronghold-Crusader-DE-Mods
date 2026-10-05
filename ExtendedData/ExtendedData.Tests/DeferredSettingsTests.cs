using System;
using System.Collections.Generic;
using System.Linq;
using ExtendedData.Core;

internal static class DeferredSettingsTests
{
    internal static void Run()
    {
        foreach (int count in new[] { 3, 4233, 12000, 16384 })
        {
            int created = 0;
            var page = new DeferredSettingsPage<int, object>(Enumerable.Range(0, count),
                (id, term) => id.ToString() == term, id => { created++; return new object(); });
            Require(page.Rows.Length == 0 && created == 0, "collapsed rows allocated");
            page.Expanded = true;
            Require(page.Rows.Length == Math.Min(64, count), "page size");
            var first = page.Rows;
            Require(ReferenceEquals(first, page.Rows) && created == first.Length, "binding recreated rows");
            page.Filter = (count - 1).ToString();
            Require(page.Rows.Length == 1 && page.Definitions.Count == count, "filter lost hidden definitions");
            page.Filter = ""; page.Page = int.MaxValue;
            Require(page.Rows.Length <= 64 && page.Page == page.PageCount - 1, "last page invalid");
            page.Expanded = false;
            Require(page.Rows.Length == 0 && !page.MaterializedRows.Any(), "collapsed rows retained");

            var modes = new Dictionary<string, TrailSettingMode>();
            var definitions = Enumerable.Range(0, count).Select(i => new ExtendedData.TrailSettingGroupDefinition(
                "key" + i, "Option " + i, new[] { "key" + i })).ToArray();
            int uiObjects = Noesis.ComboBoxItem.Created;
            var model = new ExtendedData.TrailModSelectionItem("probe", "Probe", definitions,
                (_, key) => modes.TryGetValue(key, out var mode) ? mode : TrailSettingMode.ModDefault,
                (_, keys, mode) => { foreach (string key in keys) modes[key] = mode; }, "help");
            Require(model.Settings.Length == 0 && Noesis.ComboBoxItem.Created - uiObjects == 4, "collapsed model built detail controls");
            Require(model.SearchText.Contains("Option " + (count - 1)), "search lost hidden option");
            model.SelectedModeIndex = (int)TrailSettingMode.Player;
            Require(modes.Count == count && model.SelectedModeIndex == (int)TrailSettingMode.Player, "bulk changed visible subset only");
            model.IsExpanded = true; model.Filter = "key" + (count - 1);
            Require(model.Settings.Length == 1, "model filter failed");
            model.Settings[0].SelectedModeIndex = (int)TrailSettingMode.Fixed;
            Require(model.SelectedModeIndex == 3, "hidden values missing from mixed state");
            model.SelectedModeIndex = (int)TrailSettingMode.ModDefault;
            Require(modes.Values.All(x => x == TrailSettingMode.ModDefault), "filtered bulk did not cover all settings");
            model.IsExpanded = false; uiObjects = Noesis.ComboBoxItem.Created; model.RefreshState();
            Require(Noesis.ComboBoxItem.Created == uiObjects, "state refresh allocated collapsed detail controls");
        }
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}

// Managed stand-ins for testing the actual production selection model without Noesis native UI.
namespace Noesis
{
    public enum Visibility { Visible, Collapsed }
    public sealed class ComboBoxItem
    {
        public static int Created;
        public ComboBoxItem() { Created++; }
        public object Content { get; set; }
        public bool IsEnabled { get; set; } = true;
    }
}
namespace SHCDESE.NoesisUtil { internal sealed class NamespaceMarker { } }
internal static class SerpLocalization { public static string Get(string key) => key; }
