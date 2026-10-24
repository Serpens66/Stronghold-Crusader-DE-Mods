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
                // Mod-owned persisted values are host-controlled; independent overrides
                // are opt-in. Discover typed settings without prescribing companion names
                // or the inherited preset engine's property layout.
                var modSettings = typeof(PopularitySettings).GetProperties(BindingFlags.Public |
                    BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(p => p.CanRead && p.CanWrite && (p.PropertyType == typeof(bool) ||
                        p.PropertyType == typeof(int) || p.PropertyType == typeof(double))).ToArray();
                Check(modSettings.Length > 0, "Mod-owned settings are present");
                foreach (var property in modSettings)
                    Check(property.IsDefined(typeof(SHCDESE.API.Components.Network.SyncHostOnlyAttribute), true),
                        property.Name + " is host-controlled");
                var enabled = modSettings.Where(p => p.PropertyType == typeof(bool) &&
                    p.Name != nameof(PopularitySettings.EnableMod)).ToArray();
                Check(enabled.Length > 0, "Opt-in overrides are present");
                foreach (var toggle in enabled)
                    Check(!(bool)toggle.GetValue(settings), toggle.Name + " defaults disabled");
                Check(settings.CaptureFearContributions().Count == 0, "Fear groups default inactive");
                settings.NegativeFearEnabled = true;
                var negative = settings.CaptureFearContributions();
                Check(negative.Keys.OrderBy(v => v).SequenceEqual(Enumerable.Range(-5,5)), "Negative group contains exactly its supported levels");
                foreach (var entry in negative) Check(entry.Value == entry.Key, "Negative level has its Vanilla default");
                settings.PositiveFearEnabled = true;
                var both = settings.CaptureFearContributions();
                foreach (int level in Enumerable.Range(-5,11).Where(level => level != 0))
                    Check(both.TryGetValue(level,out int points) && points == level, "Every Vanilla fear level maps to its own default");
                Check(!both.ContainsKey(0), "Neutral fear is never overridden");
                settings.FearNegative3 = -12; settings.FearPositive4 = 0; settings.FearPositive2 = -7;
                var savedFear = Prepare(plugin);
                Check(savedFear.NegativeFearEnabled && savedFear.PositiveFearEnabled &&
                    savedFear.CaptureFearContributions().OrderBy(v => v.Key).SequenceEqual(settings.CaptureFearContributions().OrderBy(v => v.Key)),
                    "Both groups and all level contributions persist across restart");
                settings.NegativeFearEnabled = false;
                Check(settings.FearNegative3 == -12 && !settings.FearNegative3Editable &&
                    settings.CaptureFearContributions().Keys.All(level => level > 0), "Disabling one side retains values and leaves the other active");
                settings.NegativeFearEnabled = true; settings.PositiveFearEnabled = false;
                Check(settings.CaptureFearContributions().Keys.All(level => level < 0) && !settings.FearPositive4Editable,
                    "Positive and negative activation are independent");
                settings.FearNegative1ValueText = " +25 ";
                Check(settings.FearNegative1 == 25, "Signed whole-point text accepted");
                settings.FearNegative1ValueText = "1.5"; settings.FearNegative1ValueText = "not a number";
                Check(settings.FearNegative1 == 25, "Invalid or fractional text preserves whole-point value");
                settings.FearNegative1 = -999; Check(settings.FearNegative1 == -25, "Fear lower bound");
                settings.FearNegative1 = 999; Check(settings.FearNegative1 == 25, "Fear upper bound");
                for (int value=-25; value<=25; value++)
                {
                    settings.FearNegative1 = value;
                    Check(settings.CaptureFearContributions()[-1] == value, "Every declared whole-point contribution is representable");
                }
                settings.EnableMod = false;
                Check(settings.CaptureFearContributions().Count == 0, "Host activation disables both fear groups");
                settings.EnableMod = true;
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
                var missionFear = restored.CaptureFearContributions();
                restored.FearNegative3 = 20; restored.NegativeFearEnabled = false;
                Check(restored.CaptureFearContributions().OrderBy(v => v.Key).SequenceEqual(missionFear.OrderBy(v => v.Key)),
                    "Mission locks fear contributions and side switches");
                Check(restored.ReligionPopModifierTierOne == -9 && restored.EnableMod && !restored.CanConfigure, "Mission locks setters");
                restored.SetMissionLocked(false);
                GameNetworkAPI.LocalHost = false; GameNetworkAPI.Networked = true; GameNetworkAPI.MultiplayerGame = true;
                restored.ReligionPopModifierTierOne = 12;
                int hostFear = restored.FearNegative3;
                restored.FearNegative3 = 23; restored.NegativeFearEnabled = false;
                Check(restored.FearNegative3 == hostFear && restored.NegativeFearEnabled, "Client cannot alter fear groups or values");
                GameXAMLManagerAPI.Instance.ApplyNetworkSync(restored, () => restored.FearNegative3 = 8);
                Check(restored.FearNegative3 == 8, "Authoritative fear value accepted");
                Check(restored.ReligionPopModifierTierOne == -9, "Client cannot change host value");
                GameXAMLManagerAPI.Instance.ApplyNetworkSync(restored, () => restored.ReligionPopModifierTierOne = 11);
                Check(restored.ReligionPopModifierTierOne == 11, "Authoritative host update accepted");
                GameNetworkAPI.LocalHost = true; GameNetworkAPI.Networked = false; GameNetworkAPI.MultiplayerGame = false;
                var own = Prepare(plugin);
                Check(own.ReligionPopModifierTierOne == -9 && own.FearNegative3 == hostFear, "Received host values do not overwrite personal presets");
                own.System_LoadModDefaults();
                Check(own.ReligionPopModifierTierOne == 2 && own.AlePopModifierTierFour == 8 && own.TaxPopModifierCruellest == -24, "Vanilla defaults restored");
                foreach (var toggle in enabled) Check(!(bool)toggle.GetValue(own), "Shared defaults reset disables every override");
                own.NegativeFearEnabled = true; own.PositiveFearEnabled = true;
                foreach (var entry in own.CaptureFearContributions()) Check(entry.Value == entry.Key, "Shared defaults restores every fear contribution");
                own.SetMissionLocked(true); own.SetMissionLocked(false);
                Check(own.FearNegative3Editable && own.FearPositive4Editable, "Next mission setup unlocks both fear groups");
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
                bool independent = false;
                IndependentFeatureRegistration.Register(() => { throw new InvalidOperationException("adapter unavailable"); },
                    () => independent = true, ex => { throw new Exception("broken log listener"); });
                Check(independent, "Unavailable fear adapter and failing diagnostics do not block independent registration");
                bool published = false;
                try {
                    IndependentFeatureRegistration.Register(() => { }, () => { throw new InvalidOperationException("lifecycle unavailable"); }, ex => { });
                    published = true;
                } catch (InvalidOperationException) { }
                Check(!published, "Required registration failure cannot publish a ready runtime");
                batch.Apply(new[] { active }); values.Pop(); values.Push(999);
                Check(!batch.Release() && values.Value == 999, "Ownership warning listener failure cannot change a foreign value or escape cleanup");
                Console.WriteLine("PASS: " + checks + " PopularityTest settings, restart/authority, mission locking and override ownership checks.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
    }
}
