using APIShared.ModSettings;
using PopularityTest;
using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

// UI command/localization stand-ins only. Production settings and the maintained
// preset/authority/storage engine are compiled unchanged into this test harness.
namespace SHCDESE.NoesisUtil
{
    public sealed class RelayCommand
    {
        private readonly Action action;
        public RelayCommand(Action action) { this.action = action; }
        public void Execute(object parameter) => action();
    }
}
public static class SerpLocalization { public static string Get(string key) => key; }

namespace PopularityTest.Tests
{
    internal static class PresetEntry
    {
        private static int checks;
        private static void Check(bool condition, string name)
        { if (!condition) throw new Exception(name); checks++; }
        private sealed class Values : IOverrideValue
        {
            internal readonly List<int> Stack = new List<int> { 137 };
            public int Value => Stack[Stack.Count - 1];
            public int Depth => Stack.Count;
            public void Push(int value) => Stack.Add(value);
            public void Pop() { if (Stack.Count > 1) Stack.RemoveAt(Stack.Count - 1); }
        }
        private static PopularitySettings Prepare(string path)
        {
            var settings = new PopularitySettings();
            settings.PreparePresets(null, path, "PopularityIntegration"); settings.ActivatePresets();
            return settings;
        }
        private static int Main(string[] args)
        {
            try
            {
                string fixture = TestWorkDirectory.New("PopularityPreset");
                Directory.CreateDirectory(fixture);
                PersonalSettingsPaths.TestDocumentsDirectory = _ => fixture;
                string plugin = Path.Combine(fixture, "PopularityTest.dll");
                GameNetworkAPI.LocalHost = true; GameNetworkAPI.Networked = false; GameNetworkAPI.MultiplayerGame = false;
                var settings = Prepare(plugin);
                // Evaluate production text properties through a localization boundary
                // returning their requested key, then check the delivered mod-owned values.
                // Common.* may use SerpLocalization's maintained built-in fallbacks.
                var textKeys = typeof(PopularitySettings).GetProperties()
                    .Where(p => p.PropertyType == typeof(string) && p.CanRead)
                    .Select(p => (string)p.GetValue(settings))
                    .Where(v => v != null && v.StartsWith("Popularity.")).Distinct().ToArray();
                foreach (var locale in Directory.GetFiles(Path.Combine(args[0],"Locales"),"*.txt"))
                {
                    var translations = File.ReadAllLines(locale).Where(l => l.Contains("="))
                        .ToDictionary(l => l.Substring(0,l.IndexOf('=')),l => l.Substring(l.IndexOf('=')+1));
                    foreach (var key in textKeys)
                        Check(translations.TryGetValue(key,out var text) && !string.IsNullOrWhiteSpace(text), "Used localization key: " + key);
                }
                var enabled = typeof(PopularitySettings).GetProperties().Where(p => p.Name.EndsWith("Enabled") &&
                    p.Name != "HostSettingsEnabled" && p.Name != "ClientSettingsEnabled").ToArray();
                Check(enabled.Length > 0, "Activation settings are present");
                foreach (var toggle in enabled)
                {
                    Check(!(bool)toggle.GetValue(settings), toggle.Name + " defaults disabled");
                    Check(toggle.GetCustomAttributes(false).Any(a => a.GetType().Name == "SyncHostOnlyAttribute"), "Toggle host classification");
                    var value = typeof(PopularitySettings).GetProperty(toggle.Name.Substring(0, toggle.Name.Length - 7));
                    Check(value != null && (value.PropertyType == typeof(int) || value.PropertyType == typeof(double)), "Numeric companion");
                    Check(value.GetCustomAttributes(false).Any(a => a.GetType().Name == "SyncHostOnlyAttribute"), "Value host classification");
                }
                settings.GoodBadThingPopModifierMultiplierEnabled = true;
                settings.GoodBadThingPopModifierMultiplier = 1.1;
                Check(Prepare(plugin).GoodBadThingPopModifierMultiplier == 1.1, "Fractional fear coefficient persists exactly");
                settings.GoodBadThingPopModifierMultiplierValueText = "-2,3";
                Check(settings.GoodBadThingPopModifierMultiplier == -2.3, "Decimal comma accepted");
                settings.GoodBadThingPopModifierMultiplier = 99;
                Check(settings.GoodBadThingPopModifierMultiplier == 5, "Fear maximum +5");
                settings.GoodBadThingPopModifierMultiplier = -99;
                Check(settings.GoodBadThingPopModifierMultiplier == -5, "Fear minimum -5");
                settings.GoodBadThingPopModifierMultiplier = 1.26;
                Check(settings.GoodBadThingPopModifierMultiplier == 1.3, "Fear input snaps to tenths");
                for (int tenths = -50; tenths <= 50; tenths++)
                {
                    settings.GoodBadThingPopModifierMultiplier = tenths / 10.0;
                    Check(settings.GoodBadThingPopModifierMultiplier == tenths / 10.0, "Every declared tenth is representable");
                }
                settings.GoodBadThingPopModifierMultiplier = 1.1;
                settings.GoodBadThingPopModifierMultiplier = double.NaN;
                settings.GoodBadThingPopModifierMultiplier = double.PositiveInfinity;
                Check(settings.GoodBadThingPopModifierMultiplier == 1.1, "Nonfinite inputs preserve current coefficient");
                settings.ReligionPopModifierTierOneEnabled = true; settings.ReligionPopModifierTierOne = -9;
                settings.AlePopModifierTierFourEnabled = true; settings.AlePopModifierTierFour = -7;
                settings.TaxPopModifierCruellest = -48;
                var restored = Prepare(plugin);
                Check(restored.ReligionPopModifierTierOne == -9 && restored.ReligionPopModifierTierOneEnabled, "Negative religion and activation persisted across restart");
                Check(restored.AlePopModifierTierFour == -7 && restored.TaxPopModifierCruellest == -48, "Multiple source values persist");
                restored.AlePopModifierTierFourEnabled = false;
                Check(restored.AlePopModifierTierFour == -7 && !restored.AlePopModifierTierFourEditable, "Disabled retains value and locks editor");
                restored.TaxPopModifierCruellest = -999; Check(restored.TaxPopModifierCruellest == -48, "Numeric input bounds");
                restored.SetMissionLocked(true); restored.ReligionPopModifierTierOne = 12; restored.EnableMod = false;
                restored.ResetToDefaultCommand.Execute(null);
                Check(restored.ReligionPopModifierTierOne == -9 && restored.EnableMod && !restored.CanConfigure, "Mission locks setters and reset");
                restored.SetMissionLocked(false);
                GameNetworkAPI.LocalHost = false; GameNetworkAPI.Networked = true; GameNetworkAPI.MultiplayerGame = true;
                restored.ReligionPopModifierTierOne = 12;
                Check(restored.ReligionPopModifierTierOne == -9, "Client cannot change host value");
                GameXAMLManagerAPI.Instance.ApplyNetworkSync(restored, () => restored.ReligionPopModifierTierOne = 11);
                Check(restored.ReligionPopModifierTierOne == 11, "Authoritative host update accepted");
                GameNetworkAPI.LocalHost = true; GameNetworkAPI.Networked = false; GameNetworkAPI.MultiplayerGame = false;
                var own = Prepare(plugin);
                Check(own.ReligionPopModifierTierOne == -9, "Received host value does not overwrite personal preset");
                own.ResetToDefaultCommand.Execute(null);
                Check(own.ReligionPopModifierTierOne == 2 && own.AlePopModifierTierFour == 8 && own.TaxPopModifierCruellest == -24, "Vanilla defaults restored");
                foreach (var toggle in enabled) Check(!(bool)toggle.GetValue(own), "Reset disables every override");
                var baseline = new Values(); var lease = new OwnedOverride(baseline, -400);
                Check(baseline.Value == -400, "Override installed");
                Check(lease.Release() && baseline.Depth == 1 && baseline.Value == 137, "Exact foreign baseline restored");
                lease = new OwnedOverride(baseline, 0); baseline.Push(777); baseline.Push(-15);
                Check(lease.Release() && baseline.Stack.SequenceEqual(new[] { 137,777,-15 }), "Foreign overlays retain order and values");
                lease = new OwnedOverride(baseline, 25); baseline.Pop(); baseline.Push(99);
                Check(!lease.Release() && baseline.Value == 99, "Replaced ownership never overwritten");
                var values = new Values(); int resolutions = 0;
                var batch = new OverrideBatch(message => { throw new Exception(message); });
                var inactive = new OverrideRequest("food", null, () => { resolutions++; return values; });
                batch.Apply(new[] { inactive });
                Check(resolutions == 0 && values.Depth == 1 && values.Value == 137, "Disabled source is not resolved or written");
                var active = new OverrideRequest("food", -225, () => { resolutions++; return values; });
                Check(batch.Apply(new[] { active }) && values.Value == -225 && values.Depth == 2, "Session override applied once");
                Check(!batch.Apply(new[] { active }) && resolutions == 1 && values.Depth == 2, "Repeated lifecycle checkpoints do not stack duplicate overrides");
                batch.Release(); Check(values.Depth == 1 && values.Value == 137, "Map unload restores original");
                batch.Apply(new[] { new OverrideRequest("food", 0, () => values) });
                Check(values.Value == 0 && batch.Count == 1, "Reload/save snapshot supports explicit zero");
                batch.Apply(new[] { inactive }); Check(values.Value == 137 && batch.Count == 0, "Next disabled map does not inherit an old override");
                bool failed = false;
                try { batch.Apply(new[] { active, new OverrideRequest("broken", 25, () => { throw new InvalidOperationException("fixture"); }) }); }
                catch (InvalidOperationException) { failed = true; }
                Check(failed && batch.Count == 0 && values.Depth == 1 && values.Value == 137, "Initialization failure rolls back own partial batch");
                Console.WriteLine("PASS: " + checks + " PopularityTest settings, restart/authority, mission locking and override ownership checks.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
    }
}
