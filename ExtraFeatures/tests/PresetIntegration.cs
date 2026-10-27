using APIShared.ModSettings;
using ExtraFeatures;
using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MessagePack;

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


namespace ExtraFeatures.Tests
{
    internal static class PresetEntry
    {
        private static int checks;
        private static void Check(bool condition, string name)
        { if (!condition) throw new Exception(name); checks++; }
        private static ExtraFeaturesViewModel Prepare(string path)
        {
            var settings = new ExtraFeaturesViewModel(false);
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
                string plugin = Path.Combine(fixture, "ExtraFeatures.dll");
                GameNetworkAPI.LocalHost = true; GameNetworkAPI.Networked = false; GameNetworkAPI.MultiplayerGame = false;
                var settings = Prepare(plugin);
                foreach (var parameter in PopularityCatalog.All)
                {
                    Check((int)typeof(ExtraFeaturesViewModel).GetProperty(parameter.Name+"Human").GetValue(settings)==parameter.Vanilla && !(bool)typeof(ExtraFeaturesViewModel).GetProperty(parameter.Group+"HumanEnabled").GetValue(settings),parameter.Name+" retains its disabled Vanilla default");
                    var property=typeof(ExtraFeaturesViewModel).GetProperty(parameter.Name+"Human");
                    property.SetValue(settings,int.MinValue);
                    Check((int)property.GetValue(settings)==-parameter.Limit,parameter.Name+" lower bound");
                    property.SetValue(settings,int.MaxValue);
                    Check((int)property.GetValue(settings)==parameter.Limit,parameter.Name+" upper bound");
                    property.SetValue(settings,parameter.Vanilla);
                }
                foreach (var group in PopularityCatalog.All.GroupBy(p => p.Group))
                {
                    settings.ResetPopularityDefaults();
                    foreach(var parameter in group)
                    foreach(string role in new[]{"Human","AI"}) {
                        var number=typeof(ExtraFeaturesViewModel).GetProperty(parameter.Name+role);
                        var flag=typeof(ExtraFeaturesViewModel).GetProperty(group.Key+role+"Enabled");
                        Check((int)number.GetValue(settings)==parameter.Vanilla && !(bool)flag.GetValue(settings),"Group defaults: "+parameter.Name+role);
                        number.SetValue(settings,int.MinValue);
                        Check((int)number.GetValue(settings)==-parameter.Limit,"Every role obeys the lower numeric bound");
                        number.SetValue(settings,int.MaxValue);
                        Check((int)number.GetValue(settings)==parameter.Limit,"Every role obeys the upper numeric bound");
                        number.SetValue(settings,role=="Human"?0:-parameter.Limit);
                        Check(typeof(ExtraFeaturesViewModel).GetProperty(parameter.Name+role+"Enabled")==null,"Unpublished individual activations are removed");
                    }
                    for(int combination=0;combination<4;combination++) {
                        bool human=(combination&1)!=0, ai=(combination&2)!=0;
                        typeof(ExtraFeaturesViewModel).GetProperty(group.Key+"HumanEnabled").SetValue(settings,human);
                        typeof(ExtraFeaturesViewModel).GetProperty(group.Key+"AIEnabled").SetValue(settings,ai);
                        var captured=settings.CaptureOrdinaryContributions();
                        var expected=group.Select(p=>(APIShared.Economy.PopularitySource)Enum.Parse(typeof(APIShared.Economy.PopularitySource),p.Name));
                        Check(captured.Keys.OrderBy(v=>v).SequenceEqual((human||ai?expected:Enumerable.Empty<APIShared.Economy.PopularitySource>()).OrderBy(v=>v)),"Only the selected group is published, including every member");
                        foreach(var parameter in group) {
                            var source=(APIShared.Economy.PopularitySource)Enum.Parse(typeof(APIShared.Economy.PopularitySource),parameter.Name);
                            if(human||ai) Check(captured[source].HumanPoints==(human?(int?)0:null) && captured[source].AIPoints==(ai?(int?)-parameter.Limit:null),"Independent role contributions, including zero");
                            foreach(string role in new[]{"Human","AI"}) {
                                Check((bool)typeof(ExtraFeaturesViewModel).GetProperty(parameter.Name+role+"Editable").GetValue(settings)==(role=="Human"?human:ai),"Every member follows its own group/role switch");
                                Check((int)typeof(ExtraFeaturesViewModel).GetProperty(parameter.Name+role).GetValue(settings)==(role=="Human"?0:-parameter.Limit),"Disabling a group retains every entered number");
                            }
                        }
                    }
                }
                settings.ResetPopularityDefaults();
                foreach(string group in new[]{"NegativeFear","PositiveFear"})
                foreach(int combination in new[]{0,1,2,3}) {
                    bool h=(combination&1)!=0,ai=(combination&2)!=0;
                    typeof(ExtraFeaturesViewModel).GetProperty(group+"HumanEnabled").SetValue(settings,h);
                    typeof(ExtraFeaturesViewModel).GetProperty(group+"AIEnabled").SetValue(settings,ai);
                    for(int level=1;level<=5;level++) {
                        string key="Fear"+(group=="NegativeFear"?"Negative":"Positive")+level;
                        typeof(ExtraFeaturesViewModel).GetProperty(key+"Human").SetValue(settings,0);
                        typeof(ExtraFeaturesViewModel).GetProperty(key+"AI").SetValue(settings,25-level);
                        var captured=settings.CaptureFearContributions();int signed=group=="NegativeFear"?-level:level;
                        Check(captured.ContainsKey(signed)==(h||ai),"Fear switches cover every signed level");
                        if(h||ai)Check(captured[signed].HumanPoints==(h?(int?)0:null)&&captured[signed].AIPoints==(ai?(int?)(25-level):null),"Fear role values independent, including zero and reversed signs");
                    }
                }
                settings.RationsHumanEnabled=true; settings.RationsAIEnabled=true;
                settings.RationsPopModifierNoneHuman=0; settings.RationsPopModifierNoneAI=-16;
                var bothRoles=Prepare(plugin);
                Check(bothRoles.FearNegative3Human==0 && bothRoles.FearNegative3AI==22 && bothRoles.NegativeFearHumanEnabled && bothRoles.NegativeFearAIEnabled,"Both fear role values persist");
                Check(bothRoles.RationsPopModifierNoneHuman==0 && bothRoles.RationsPopModifierNoneAI==-16 && bothRoles.RationsHumanEnabled && bothRoles.RationsAIEnabled,"Both ordinary role values persist");
                // A real persisted old snapshot has the same numeric property keys,
                // but only retired per-value flags. No activation migration is intended.
                string oldFile=Directory.GetFiles(fixture,"settings.msgpack",SearchOption.AllDirectories).Single();
                var oldPayload=MessagePackSerializer.Deserialize<Dictionary<string,byte[]>>(File.ReadAllBytes(oldFile));
                bool oldGroupSnapshot=false;
                foreach(string section in oldPayload.Keys.ToArray()) {
                    Dictionary<string,byte[]> snapshot;
                    try { snapshot=MessagePackSerializer.Deserialize<Dictionary<string,byte[]>>(oldPayload[section]); } catch { continue; }
                    if(snapshot==null || !snapshot.ContainsKey(nameof(settings.RationsPopModifierNoneHuman)))continue;
                    foreach(var parameter in PopularityCatalog.All)
                    foreach(string role in new[]{"Human","AI"}) {
                        snapshot.Remove(parameter.Group+role+"Enabled");
                        snapshot[parameter.Name+role+"Enabled"]=MessagePackSerializer.Serialize(true);
                    }
                    oldPayload[section]=MessagePackSerializer.Serialize(snapshot); oldGroupSnapshot=true;
                }
                Check(oldGroupSnapshot,"Old individual-switch payload contains the real persisted values");
                File.WriteAllBytes(oldFile,MessagePackSerializer.Serialize(oldPayload));
                settings=Prepare(plugin);
                foreach(var group in PopularityCatalog.All.Select(p=>p.Group).Distinct())
                foreach(string role in new[]{"Human","AI"}) Check(!(bool)typeof(ExtraFeaturesViewModel).GetProperty(group+role+"Enabled").GetValue(settings),"Old switches do not activate new groups");
                Check(settings.RationsPopModifierNoneHuman==0 && settings.RationsPopModifierNoneAI==-16 && settings.NegativeFearHumanEnabled && settings.NegativeFearAIEnabled,"Numbers and unchanged fear flags survive while new group flags stay off");
                settings.System_LoadModDefaults();
                // Evaluate production text properties through a localization boundary
                // returning their requested key, then check the delivered mod-owned values.
                // Common.* may use SerpLocalization's maintained built-in fallbacks.
                var textKeys = typeof(ExtraFeaturesViewModel).GetProperties()
                    .Where(p => p.PropertyType == typeof(string) && p.CanRead)
                    .Select(p => (string)p.GetValue(settings))
                    .Where(v => v != null && v.StartsWith("ExtraFeatures.Popularity.")).Distinct().ToArray();
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
                var modSettings = typeof(ExtraFeaturesViewModel).GetProperties(BindingFlags.Public |
                    BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(p => p.CanRead && p.CanWrite && (p.PropertyType == typeof(bool) ||
                        p.PropertyType == typeof(int) || p.PropertyType == typeof(double))).ToArray();
                var popularityNames = System.Text.RegularExpressions.Regex.Matches(
                    File.ReadAllText(Path.Combine(args[0], "src/ExtraFeaturesViewModel.Popularity.cs")),
                    @"\[SyncHostOnly\]\s+public (?:bool|int) (\w+)")
                    .Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value).ToArray();
                modSettings = modSettings.Where(p => popularityNames.Contains(p.Name)).ToArray();
                // Simulate a genuinely older ExtraFeatures payload: retain its real
                // envelope and existing values, remove every newly added property.
                settings.WoodRefundPercent = 77;
                settings.MarketBuyPriceMultiplier = 1.7;
                settings.EnableFearFactorNeutralization = true;
                string stored = Directory.GetFiles(fixture,"settings.msgpack",SearchOption.AllDirectories).Single();
                var payload = MessagePackSerializer.Deserialize<Dictionary<string,byte[]>>(File.ReadAllBytes(stored));
                bool legacySnapshot = false;
                foreach (string key in payload.Keys.ToArray())
                {
                    Dictionary<string,byte[]> snapshot;
                    try { snapshot = MessagePackSerializer.Deserialize<Dictionary<string,byte[]>>(payload[key]); }
                    catch { continue; }
                    if (snapshot == null || !snapshot.ContainsKey(nameof(settings.WoodRefundPercent))) continue;
                    foreach (string added in popularityNames) snapshot.Remove(added);
                    payload[key] = MessagePackSerializer.Serialize(snapshot);
                    legacySnapshot = true;
                }
                Check(legacySnapshot,"Old-format compatibility fixture contains the actual stored snapshot");
                File.WriteAllBytes(stored,MessagePackSerializer.Serialize(payload));
                settings = Prepare(plugin);
                Check(settings.WoodRefundPercent==77 && settings.MarketBuyPriceMultiplier==1.7 && settings.EnableFearFactorNeutralization,
                    "Existing ExtraFeatures values and independent fear neutralization survive old payload loading");
                Check(settings.FearNegative3Human==-3 && !settings.NegativeFearHumanEnabled && !settings.RationsHumanEnabled,
                    "Absent Popularity fields acquire inactive defaults without resetting existing values");
                Check(modSettings.Length > 0, "Mod-owned settings are present");
                foreach (var property in modSettings)
                    Check(property.IsDefined(typeof(SHCDESE.API.Components.Network.SyncHostOnlyAttribute), true),
                        property.Name + " is host-controlled");
                var enabled = modSettings.Where(p => p.PropertyType == typeof(bool) &&
                    p.Name != nameof(ExtraFeaturesViewModel.EnableMod)).ToArray();
                Check(enabled.Length > 0, "Opt-in overrides are present");
                foreach (var toggle in enabled)
                    Check(!(bool)toggle.GetValue(settings), toggle.Name + " defaults disabled");
                Check(settings.CaptureFearContributions().Count == 0, "Fear groups default inactive");
                settings.NegativeFearHumanEnabled = true;
                var negative = settings.CaptureFearContributions();
                Check(negative.Keys.OrderBy(v => v).SequenceEqual(Enumerable.Range(-5,5)), "Negative group contains exactly its supported levels");
                foreach (var entry in negative) Check(entry.Value.HumanPoints == entry.Key, "Negative level has its Vanilla default");
                settings.PositiveFearHumanEnabled = true;
                var both = settings.CaptureFearContributions();
                foreach (int level in Enumerable.Range(-5,11).Where(level => level != 0))
                    Check(both.TryGetValue(level,out APIShared.Economy.PopularityOverrideValues points) && points.HumanPoints == level, "Every Vanilla fear level maps to its own default");
                Check(!both.ContainsKey(0), "Neutral fear is never overridden");
                settings.FearNegative3Human = -12; settings.FearPositive4Human = 0; settings.FearPositive2Human = -7;
                var savedFear = Prepare(plugin);
                Check(savedFear.NegativeFearHumanEnabled && savedFear.PositiveFearHumanEnabled &&
                    savedFear.CaptureFearContributions().OrderBy(v => v.Key).SequenceEqual(settings.CaptureFearContributions().OrderBy(v => v.Key)),
                    "Both groups and all level contributions persist across restart");
                settings.NegativeFearHumanEnabled = false;
                Check(settings.FearNegative3Human == -12 && !settings.FearNegative3HumanEditable &&
                    settings.CaptureFearContributions().Keys.All(level => level > 0), "Disabling one side retains values and leaves the other active");
                settings.NegativeFearHumanEnabled = true; settings.PositiveFearHumanEnabled = false;
                Check(settings.CaptureFearContributions().Keys.All(level => level < 0) && !settings.FearPositive4HumanEditable,
                    "Positive and negative activation are independent");
                settings.FearNegative1HumanValueText = " +25 ";
                Check(settings.FearNegative1Human == 25, "Signed whole-point text accepted");
                settings.FearNegative1HumanValueText = "1.5"; settings.FearNegative1HumanValueText = "not a number";
                Check(settings.FearNegative1Human == 25, "Invalid or fractional text preserves whole-point value");
                settings.FearNegative1Human = -999; Check(settings.FearNegative1Human == -25, "Fear lower bound");
                settings.FearNegative1Human = 999; Check(settings.FearNegative1Human == 25, "Fear upper bound");
                for (int value=-25; value<=25; value++)
                {
                    settings.FearNegative1Human = value;
                    Check(settings.CaptureFearContributions()[-1].HumanPoints == value, "Every declared whole-point contribution is representable");
                }
                settings.EnableMod = false;
                Check(settings.CaptureFearContributions().Count == 0, "Host activation disables both fear groups");
                settings.EnableMod = true;
                settings.ReligionHumanEnabled = true; settings.ReligionPopModifierTierOneHuman = -9;
                settings.AleHumanEnabled = true; settings.AlePopModifierTierFourHuman = -7;
                settings.TaxPopModifierCruellestHuman = -48;
                var restored = Prepare(plugin);
                restored.ReligionAIEnabled=true; restored.ReligionPopModifierTierOneAI=10;
                restored.System_SavePersonalPreset("group-roles","Group roles","",modSettings.Select(p=>new PresetSaveSelection {PropertyName=p.Name,Mode=PublishedPresetValueMode.Fixed}).ToArray(),false);
                var preset=restored.System_TestPublishedPresets.Single(p=>p.Id=="group-roles");
                restored.System_LoadModDefaults();
                restored.System_TestLoadPreset(preset.StableId);
                Check(restored.ReligionHumanEnabled && restored.ReligionAIEnabled && restored.ReligionPopModifierTierOneHuman==-9 && restored.ReligionPopModifierTierOneAI==10,"Named preset restores independent group flags and values");
                Check(restored.ReligionPopModifierTierOneHuman == -9 && restored.ReligionHumanEnabled, "Negative religion and activation persisted across restart");
                Check(restored.AlePopModifierTierFourHuman == -7 && restored.TaxPopModifierCruellestHuman == -48, "Multiple source values persist");
                restored.AleHumanEnabled = false;
                Check(restored.AlePopModifierTierFourHuman == -7 && !restored.AlePopModifierTierFourHumanEditable, "Disabled retains value and locks editor");
                restored.TaxPopModifierCruellestHuman = -999; Check(restored.TaxPopModifierCruellestHuman == -48, "Numeric input bounds");
                restored.SetPopularityMissionLocked(true);
                foreach(var toggle in enabled) {
                    bool previous=(bool)toggle.GetValue(restored);toggle.SetValue(restored,!previous);
                    Check((bool)toggle.GetValue(restored)==previous,"Mission locks every group and role switch");
                }
                restored.ReligionPopModifierTierOneHuman = 12;
                var missionFear = restored.CaptureFearContributions();
                restored.FearNegative3Human = 20; restored.NegativeFearHumanEnabled = false;
                Check(restored.CaptureFearContributions().OrderBy(v => v.Key).SequenceEqual(missionFear.OrderBy(v => v.Key)),
                    "Mission locks fear contributions and side switches");
                Check(restored.ReligionPopModifierTierOneHuman == -9 && !restored.CanConfigurePopularity, "Mission locks setters");
                restored.SetPopularityMissionLocked(false);
                GameNetworkAPI.LocalHost = false; GameNetworkAPI.Networked = true; GameNetworkAPI.MultiplayerGame = true;
                foreach(var toggle in enabled) {
                    bool previous=(bool)toggle.GetValue(restored);toggle.SetValue(restored,!previous);
                    Check((bool)toggle.GetValue(restored)==previous,"Client cannot change any group or role switch");
                }
                GameXAMLManagerAPI.Instance.ApplyNetworkSync(restored,()=>restored.RationsAIEnabled=true);
                Check(restored.RationsAIEnabled && !restored.RationsPopModifierHalfAIEditable,"Received group activation applies without granting client editor rights");
                restored.ReligionPopModifierTierOneHuman = 12;
                int hostFear = restored.FearNegative3Human;
                restored.FearNegative3Human = 23; restored.NegativeFearHumanEnabled = false;
                Check(restored.FearNegative3Human == hostFear && restored.NegativeFearHumanEnabled, "Client cannot alter fear groups or values");
                GameXAMLManagerAPI.Instance.ApplyNetworkSync(restored, () => restored.FearNegative3Human = 8);
                Check(restored.FearNegative3Human == 8, "Authoritative fear value accepted");
                Check(restored.ReligionPopModifierTierOneHuman == -9, "Client cannot change host value");
                GameXAMLManagerAPI.Instance.ApplyNetworkSync(restored, () => restored.ReligionPopModifierTierOneHuman = 11);
                Check(restored.ReligionPopModifierTierOneHuman == 11, "Authoritative host update accepted");
                GameNetworkAPI.LocalHost = true; GameNetworkAPI.Networked = false; GameNetworkAPI.MultiplayerGame = false;
                var own = Prepare(plugin);
                Check(!own.RationsAIEnabled,"Received group switch does not overwrite personal values");
                Check(own.ReligionPopModifierTierOneHuman == -9 && own.FearNegative3Human == hostFear, "Received host values do not overwrite personal presets");
                own.System_LoadModDefaults();
                Check(own.ReligionPopModifierTierOneHuman == 2 && own.AlePopModifierTierFourHuman == 8 && own.TaxPopModifierCruellestHuman == -24, "Vanilla defaults restored");
                foreach (var toggle in enabled) Check(!(bool)toggle.GetValue(own), "Shared defaults reset disables every override");
                own.NegativeFearHumanEnabled = true; own.PositiveFearHumanEnabled = true;
                foreach (var entry in own.CaptureFearContributions()) Check(entry.Value.HumanPoints == entry.Key, "Shared defaults restores every fear contribution");
                own.SetPopularityMissionLocked(true); own.SetPopularityMissionLocked(false);
                Check(own.FearNegative3HumanEditable && own.FearPositive4HumanEditable, "Next mission setup unlocks both fear groups");
                own.FearNegative3Human = 19;
                own.ResetToDefaultCommand.Execute(null);
                Check(!own.NegativeFearHumanEnabled && own.FearNegative3Human == -3, "Legacy ExtraFeatures reset includes Popularity");
                KillRewardTests.Run(Check);
                PeriodicGoldTests.Run(Check);
                PopularityLifecycleTests.Run(Check);
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
                Console.WriteLine("PASS: " + checks + " ExtraFeatures settings, restart/authority, mission locking and targeted policy checks.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
    }
}
