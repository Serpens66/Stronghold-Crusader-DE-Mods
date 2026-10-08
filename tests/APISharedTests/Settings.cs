using APIShared.ModSettings;
using APIShared;
using CrusaderDE;
using Iced.Intel;
using APIShared.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace APISharedTests
{
    public partial class RuntimeTests
    {
        [TestMethod]
        [TestCategory("Settings")]
        public void VerifyPublishedPresetJson() => TestPublishedPresetJson();

        private static void TestPublishedPresetJson()
        {
            var settings = new Dictionary<string, PublishedPresetSetting>(StringComparer.Ordinal)
            {
                ["Count"] = new PublishedPresetSetting { Mode = PublishedPresetValueMode.Fixed, Value = 12 },
                ["Mode"] = new PublishedPresetSetting { Mode = PublishedPresetValueMode.ModDefault },
                ["Local"] = new PublishedPresetSetting { Mode = PublishedPresetValueMode.Player },
            };
            string json = ModSettingsPresetJson.Serialize(
                "ThirdParty.Target",
                "balanced",
                "Balanced",
                "Round-trip",
                "1.0.0",
                "2.0.0",
                settings);
            PublishedModSettingsPreset parsed = ModSettingsPresetJson.Parse(
                json,
                "Provider.Guid",
                "Provider",
                "ThirdParty.Target",
                "preset_balanced.json");
            Assert(parsed.Id == "balanced" && parsed.Name == "Balanced" &&
                parsed.Settings.Count == 3 &&
                parsed.Settings["Count"].Mode == PublishedPresetValueMode.Fixed &&
                Convert.ToInt32(parsed.Settings["Count"].Value) == 12,
                "published preset JSON round-trip preserves identity, metadata, modes, and fixed values");

            string maximumDescription = "Line one\r\n" +
                new string('x', ModSettingsPresetJson.MaximumDescriptionLength - 10);
            string maximumDescriptionJson = ModSettingsPresetJson.Serialize(
                "ThirdParty.Target",
                "multiline",
                "Multiline",
                maximumDescription,
                string.Empty,
                string.Empty,
                settings);
            PublishedModSettingsPreset maximumDescriptionPreset = ModSettingsPresetJson.Parse(
                maximumDescriptionJson,
                "Provider.Guid",
                "Provider",
                "ThirdParty.Target",
                "preset_multiline.json");
            Assert(maximumDescriptionPreset.Description == maximumDescription &&
                    maximumDescriptionPreset.Description.Length == 8192,
                "preset descriptions must preserve line breaks and round-trip at the 8192-character limit");
            AssertThrows<InvalidDataException>(
                () => ModSettingsPresetJson.Serialize(
                    "ThirdParty.Target",
                    "too-long",
                    "Too long",
                    new string('x', ModSettingsPresetJson.MaximumDescriptionLength + 1),
                    string.Empty,
                    string.Empty,
                    settings),
                "preset serialization must reject descriptions longer than 8192 characters before writing");
            string oversizedDescriptionJson = maximumDescriptionJson.Replace(
                new string('x', ModSettingsPresetJson.MaximumDescriptionLength - 10),
                new string('x', ModSettingsPresetJson.MaximumDescriptionLength - 9));
            AssertThrows<InvalidDataException>(
                () => ModSettingsPresetJson.Parse(
                    oversizedDescriptionJson,
                    "Provider.Guid",
                    "Provider",
                    "ThirdParty.Target",
                    "preset_oversized-description.json"),
                "preset parsing must reject descriptions longer than 8192 characters");

            int[] converted = (int[])ModSettingsPresetJson.ConvertValue(
                new object[] { 1, 2, 3 },
                typeof(int[]));
            Assert(converted.SequenceEqual(new[] { 1, 2, 3 }),
                "published preset conversion supports one-dimensional primitive arrays");
            Assert((DayOfWeek)ModSettingsPresetJson.ConvertValue("Friday", typeof(DayOfWeek)) == DayOfWeek.Friday &&
                (DayOfWeek)ModSettingsPresetJson.ConvertValue(2, typeof(DayOfWeek)) == DayOfWeek.Tuesday,
                "published preset conversion supports enum names and integral legacy values");
            AssertThrows<InvalidDataException>(
                () => ModSettingsPresetJson.ConvertValue(1.0, typeof(DayOfWeek)),
                "floating-point enum values must fail closed");
            AssertThrows<InvalidDataException>(
                () => ModSettingsPresetJson.ConvertValue("1", typeof(DayOfWeek)),
                "numeric enum strings must fail closed");

            Guid expected = Guid.NewGuid();
            object encoded = ModSettingsPresetJson.ToJsonValue(typeof(Guid), expected);
            Assert(encoded is string text && text.StartsWith(ModSettingsPresetJson.EncodedMessagePackPrefix, StringComparison.Ordinal) &&
                (Guid)ModSettingsPresetJson.ConvertValue(encoded, typeof(Guid)) == expected,
                "published preset conversion round-trips MessagePack fallback values");

            AssertThrows<InvalidDataException>(
                () => ModSettingsPresetJson.Parse(
                    "{\"schemaVersion\":1,\"id\":\"x\",\"name\":\"X\",\"targetGuid\":\"ThirdParty.Target\",\"unknown\":true,\"settings\":{\"Count\":{\"mode\":\"fixed\",\"value\":1}}}",
                    "Provider.Guid", "Provider", "ThirdParty.Target", "unknown.json"),
                "unknown published preset members must fail closed");
            AssertThrows<InvalidDataException>(
                () => ModSettingsPresetJson.Parse(
                    "{\"schemaVersion\":1,\"id\":\"x\",\"name\":\"X\",\"targetGuid\":\"Wrong.Target\",\"settings\":{\"Count\":{\"mode\":\"fixed\",\"value\":1}}}",
                    "Provider.Guid", "Provider", "ThirdParty.Target", "wrong-target.json"),
                "misplaced published preset files must fail closed");
        }
        [TestMethod]
        [TestCategory("Settings")]
        public void VerifyPublishedPresetDiscovery() => TestPublishedPresetDiscovery();

        private static void TestPublishedPresetDiscovery()
        {
            string root = Path.Combine(Path.GetTempPath(), "APISharedPresetDiscovery-" + Guid.NewGuid().ToString("N"));
            string target = "APISharedTests.Target." + Guid.NewGuid().ToString("N");
            string directory = Path.Combine(root, "Override", target);
            string personalDirectory = Path.Combine(root, "LobbyModSettings", "Presets", "Override", target);
            Directory.CreateDirectory(directory);
            Directory.CreateDirectory(personalDirectory);
            try
            {
                Func<string, string, string, string> create = (id, name, minimum) =>
                    ModSettingsPresetJson.Serialize(
                        target,
                        id,
                        name,
                        string.Empty,
                        minimum,
                        string.Empty,
                        new Dictionary<string, PublishedPresetSetting>
                        {
                            ["Count"] = new PublishedPresetSetting
                            {
                                Mode = PublishedPresetValueMode.Fixed,
                                Value = 1,
                            },
                        });
                File.WriteAllText(Path.Combine(directory, "preset_valid.json"), create("valid", "Valid", "1.0.0"));
                File.WriteAllText(Path.Combine(directory, "preset_future.json"), create("future", "Future", "9.0.0"));
                File.WriteAllText(Path.Combine(directory, "preset_duplicate-a.json"), create("duplicate", "Duplicate A", string.Empty));
                File.WriteAllText(Path.Combine(directory, "preset_duplicate-b.json"), create("duplicate", "Duplicate B", string.Empty));
                File.WriteAllText(Path.Combine(personalDirectory, "preset_personal.json"), create("personal", "Valid", string.Empty));

                IReadOnlyList<PublishedModSettingsPreset> discovered = ModSettingsPresetCatalog.Discover(
                    target,
                    new Version(2, 0, 0),
                    root,
                    log: null);
                Assert(discovered.Count == 2 && discovered.Any(item => item.Id == "valid" &&
                        item.SourceKind == ModSettingsPresetSourceKind.Bundled && !item.CanOverwrite) &&
                        discovered.Any(item => item.Id == "personal" &&
                        item.SourceKind == ModSettingsPresetSourceKind.Personal && item.CanOverwrite),
                    "preset discovery did not separate compatible bundled and personal files or reject invalid duplicates");
                var entries = discovered.Select(item => new ModSettingsPresetListEntry { Preset = item }).ToArray();
                Assert(entries.Single(item => item.SourceKind == ModSettingsPresetSourceKind.Personal).CanDelete &&
                        !entries.Single(item => item.SourceKind == ModSettingsPresetSourceKind.Bundled).CanDelete,
                    "only personal preset list entries may expose deletion");
                Assert(discovered.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count() == 2 &&
                        discovered.All(item => item.Name == "Valid"),
                    "same-name source entries did not retain distinct stable identities");
                AssertThrows<InvalidDataException>(
                    () => ModSettingsPresetCatalog.Discover("..", new Version(1, 0), root, null),
                    "preset discovery must reject unsafe target GUID path segments");
                AssertThrows<InvalidDataException>(
                    () => ModSettingsPresetCatalog.ValidatePersonalWritePath(
                        root,
                        personalDirectory,
                        Path.Combine(personalDirectory, "..", "escaped.json")),
                    "personal preset writes must not escape their target directory");
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
        [TestMethod]
        [TestCategory("Settings")]
        public void VerifyPresetSaveUiModel() => TestPresetSaveUiModel();

        private static void TestPresetSaveUiModel()
        {
            var viewModel = new PresetSaveTestViewModel();
            Assert(viewModel.HostOptionsText == "HOST OPTIONS",
                "an unresolved settings localization key must use the APIShared fallback");
            Assert(viewModel.ClientOptionsText == "Translated client options",
                "a resolved settings localization value must win over the APIShared fallback");
            Assert(viewModel.System_PresetSaveBulkModeIndex == (int)PresetSaveBulkMode.HostFixed,
                "an empty preset-save list must report the Host Fixed default, not mixed");

            PresetSettingDescriptor CreateDescriptor(string name, PresetSettingScope scope)
            {
                var descriptor = new PresetSettingDescriptor();
                typeof(PresetSettingDescriptor).GetProperty(nameof(PresetSettingDescriptor.PropertyName))
                    .SetValue(descriptor, name);
                typeof(PresetSettingDescriptor).GetProperty(nameof(PresetSettingDescriptor.PropertyType))
                    .SetValue(descriptor, typeof(int));
                typeof(PresetSettingDescriptor).GetProperty(nameof(PresetSettingDescriptor.Scope))
                    .SetValue(descriptor, scope);
                return descriptor;
            }

            var first = new PresetSaveSettingViewModel(
                CreateDescriptor("First", PresetSettingScope.Host),
                "Host",
                new[] { "Default", "Player", "Fixed" });
            var second = new PresetSaveSettingViewModel(
                CreateDescriptor("Second", PresetSettingScope.Local),
                "Local",
                new[] { "Default", "Player", "Fixed" });
            MethodInfo changedMethod = typeof(PresetLobbyModSettingsViewModel).GetMethod(
                "OnPresetSaveSettingPropertyChanged",
                BindingFlags.Instance | BindingFlags.NonPublic);
            first.PropertyChanged += (PropertyChangedEventHandler)Delegate.CreateDelegate(
                typeof(PropertyChangedEventHandler), viewModel, changedMethod);
            second.PropertyChanged += (PropertyChangedEventHandler)Delegate.CreateDelegate(
                typeof(PropertyChangedEventHandler), viewModel, changedMethod);
            viewModel.System_PresetSaveSettings.Add(first);
            viewModel.System_PresetSaveSettings.Add(second);

            bool bulkChanged = false;
            viewModel.PropertyChanged += (_, args) =>
                bulkChanged |= args.PropertyName == nameof(viewModel.System_PresetSaveBulkModeIndex);
            viewModel.System_PresetSaveBulkModeIndex = (int)PublishedPresetValueMode.Player;
            Assert(first.SelectedModeIndex == (int)PublishedPresetValueMode.Player &&
                second.SelectedModeIndex == (int)PublishedPresetValueMode.Player,
                "the preset-save bulk mode must update every displayed row");
            Assert(viewModel.System_PresetSaveBulkModeIndex == (int)PublishedPresetValueMode.Player,
                "uniform preset-save rows must report their common bulk mode");

            viewModel.System_PresetSaveBulkModeIndex = (int)PresetSaveBulkMode.ModDefault;
            Assert(first.SelectedModeIndex == (int)PublishedPresetValueMode.ModDefault &&
                second.SelectedModeIndex == (int)PublishedPresetValueMode.ModDefault &&
                viewModel.System_PresetSaveBulkModeIndex == (int)PresetSaveBulkMode.ModDefault,
                "uniform default rows must report the default bulk state");

            viewModel.System_PresetSaveBulkModeIndex = (int)PresetSaveBulkMode.HostFixed;
            Assert(first.SelectedModeIndex == (int)PublishedPresetValueMode.Fixed &&
                second.SelectedModeIndex == (int)PublishedPresetValueMode.Player &&
                viewModel.System_PresetSaveBulkModeIndex == (int)PresetSaveBulkMode.HostFixed,
                "Host Fixed must fix host rows, preserve player/local rows, and report its aggregate state");

            bulkChanged = false;
            second.SelectedModeIndex = (int)PublishedPresetValueMode.Fixed;
            Assert(viewModel.System_PresetSaveBulkModeIndex == (int)PresetSaveBulkMode.Fixed && bulkChanged,
                "uniform fixed rows must report the fixed bulk state");
            second.SelectedModeIndex = (int)PublishedPresetValueMode.ModDefault;
            Assert(viewModel.System_PresetSaveBulkModeIndex == (int)PresetSaveBulkMode.Mixed,
                "an individual preset-save mode change must publish the mixed bulk state");

            var partial = new PublishedModSettingsPreset();
            typeof(PublishedModSettingsPreset).GetProperty(nameof(PublishedModSettingsPreset.Settings))
                .SetValue(partial, new Dictionary<string, PublishedPresetSetting>(StringComparer.Ordinal)
                {
                    ["First"] = new PublishedPresetSetting { Mode = PublishedPresetValueMode.ModDefault },
                });
            typeof(PresetLobbyModSettingsViewModel).GetMethod(
                    "ApplyPresetToSaveRows",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(viewModel, new object[] { partial });
            Assert(first.SelectedModeIndex == (int)PublishedPresetValueMode.ModDefault &&
                second.SelectedModeIndex == (int)PublishedPresetValueMode.Player,
                "editing a partial preset must initialize omitted properties as Player");
            var selections = (PresetSaveSelection[])typeof(PresetLobbyModSettingsViewModel).GetMethod(
                    "CreatePresetSaveSelections",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(viewModel, null);
            Assert(selections.Length == 2 &&
                selections.Any(item => item.PropertyName == "First") &&
                selections.Any(item => item.PropertyName == "Second"),
                "the standard save dialog must include every persistent property row");
            typeof(PresetLobbyModSettingsViewModel).GetMethod(
                    "ResetPresetSaveForm",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(viewModel, null);
            Assert(first.SelectedModeIndex == (int)PublishedPresetValueMode.Fixed &&
                    second.SelectedModeIndex == (int)PublishedPresetValueMode.Player &&
                    viewModel.System_PresetSaveBulkModeIndex == (int)PresetSaveBulkMode.HostFixed,
                "a new personal preset form must initialize Host rows as Fixed and Player/Local rows as Player");

            viewModel.System_PresetSaveName = "   ";
            Assert(!viewModel.System_CanConfirmPresetSave,
                "whitespace-only preset names must disable saving");
            viewModel.System_PresetSaveName = "Named preset";
            Assert(viewModel.System_CanConfirmPresetSave,
                "a nonempty preset name must enable saving");

            MethodInfo openLoad = typeof(PresetLobbyModSettingsViewModel).GetMethod(
                "OpenPresetLoad", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo openSave = typeof(PresetLobbyModSettingsViewModel).GetMethod(
                "OpenPresetSave", BindingFlags.Instance | BindingFlags.NonPublic);
            openLoad.Invoke(viewModel, null);
            Assert(viewModel.System_PresetLoadPanelVisibility == Noesis.Visibility.Visible &&
                viewModel.System_PresetSavePanelVisibility == Noesis.Visibility.Collapsed,
                "opening Load must show only the load panel");
            openLoad.Invoke(viewModel, null);
            Assert(viewModel.System_PresetLoadPanelVisibility == Noesis.Visibility.Collapsed,
                "pressing Load again must close its panel");
            openSave.Invoke(viewModel, null);
            Assert(viewModel.System_PresetSavePanelVisibility == Noesis.Visibility.Visible &&
                viewModel.System_PresetLoadPanelVisibility == Noesis.Visibility.Collapsed &&
                viewModel.System_PresetSaveName == string.Empty &&
                viewModel.System_PresetSaveDescription == string.Empty,
                "opening Save must show only the save panel and start a new preset with blank metadata");
            Assert(viewModel.System_SettingsSourceText == "Reset settings to" &&
                    viewModel.System_SettingsSourceLoadText == "Reset" &&
                    viewModel.System_SettingsSourceHelpText.StartsWith("Resets this mod's settings", StringComparison.Ordinal),
                "the common settings reset controls must use understandable fallback text without raw localization keys");

            MethodInfo setStatus = typeof(PresetLobbyModSettingsViewModel).GetMethod(
                "SetPresetStatus",
                BindingFlags.Instance | BindingFlags.NonPublic);
            setStatus.Invoke(viewModel, new object[] { "injected failure", true });
            Assert(viewModel.System_PresetOperationStatusVisibility == Noesis.Visibility.Visible &&
                    viewModel.System_PresetOperationErrorVisibility == Noesis.Visibility.Visible,
                "preset operation failures must remain visible");
            setStatus.Invoke(viewModel, new object[] { "obsolete success", false });
            Assert(viewModel.System_PresetOperationStatusVisibility == Noesis.Visibility.Collapsed &&
                    viewModel.System_PresetOperationSuccessVisibility == Noesis.Visibility.Collapsed,
                "successful preset operations must never expose a result banner");

            var existingPreset = new PublishedModSettingsPreset();
            typeof(PublishedModSettingsPreset).GetProperty(nameof(PublishedModSettingsPreset.Name))
                .SetValue(existingPreset, "Existing preset");
            typeof(PublishedModSettingsPreset).GetProperty(nameof(PublishedModSettingsPreset.Description))
                .SetValue(existingPreset, "Existing\r\ndescription");
            typeof(PublishedModSettingsPreset).GetProperty(nameof(PublishedModSettingsPreset.Settings))
                .SetValue(existingPreset, new Dictionary<string, PublishedPresetSetting>(StringComparer.Ordinal)
                {
                    ["First"] = new PublishedPresetSetting { Mode = PublishedPresetValueMode.Fixed, Value = 1 },
                });
            var existingTarget = new ModSettingsPresetSaveTarget();
            typeof(ModSettingsPresetSaveTarget).GetProperty("Preset", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(existingTarget, existingPreset);
            viewModel.System_SelectedPresetSaveTarget = existingTarget;
            Assert(viewModel.System_PresetSaveName == "Existing preset" &&
                    viewModel.System_PresetSaveDescription == "Existing\r\ndescription",
                "selecting an existing personal preset must fill its editable name and description");
            viewModel.System_SelectedPresetSaveTarget = new ModSettingsPresetSaveTarget();
            Assert(viewModel.System_PresetSaveName == string.Empty &&
                    viewModel.System_PresetSaveDescription == string.Empty,
                "returning to the new-personal-preset target must clear name and description");

            openLoad.Invoke(viewModel, null);
            Assert(viewModel.System_PresetLoadPanelVisibility == Noesis.Visibility.Visible &&
                viewModel.System_PresetSavePanelVisibility == Noesis.Visibility.Collapsed,
                "switching from Save to Load must never leave both panels open");
            Assert(Enum.GetValues(typeof(PresetSaveBulkMode)).Length == 5 &&
                    (int)PresetSaveBulkMode.HostFixed == 3 &&
                    (int)PresetSaveBulkMode.Mixed == 4,
                "the bulk selector contract must expose four actions plus the Mixed status");
        }
        [TestMethod]
        [TestCategory("Settings")]
        public void VerifyDynamicModDefaults() => TestDynamicModDefaults();

        private static void TestDynamicModDefaults()
        {
            string root = Path.Combine(Path.GetTempPath(), "api-shared-dynamic-defaults-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var viewModel = new DynamicDefaultTestViewModel
                {
                    DynamicValues = new[] { "working" },
                };
                viewModel.PreparePresets(
                    null,
                    Path.Combine(root, "DynamicDefaults.dll"),
                    "Dynamic defaults",
                    "Tests.DynamicDefaults",
                    new Version(1, 0, 0));

                viewModel.PublishDefault(new[] { "fixed-a", "fixed-b" });
                Dictionary<string, byte[]> snapshot = viewModel.System_CreateModDefaultSnapshot();
                string[] values = (string[])MessagePack.MessagePackSerializer.Deserialize(
                    typeof(string[]),
                    snapshot[nameof(DynamicDefaultTestViewModel.DynamicValues)]);
                Assert(values.SequenceEqual(new[] { "fixed-a", "fixed-b" }) &&
                        viewModel.DynamicValues.SequenceEqual(new[] { "working" }),
                    "updating a dynamic code default must affect snapshots without changing working values");

                viewModel.ActivatePresets();
                viewModel.System_LoadModDefaults();
                Assert(viewModel.DynamicValues.SequenceEqual(new[] { "fixed-a", "fixed-b" }),
                    "resetting to Mod defaults must apply the dynamically materialized default");

                viewModel.DynamicValues = new[] { "changed" };
                viewModel.System_SavePersonalPreset(
                    "dynamic-mod-default",
                    "Dynamic Mod Default",
                    string.Empty,
                    new[]
                    {
                        new PresetSaveSelection
                        {
                            PropertyName = nameof(DynamicDefaultTestViewModel.DynamicValues),
                            Mode = PublishedPresetValueMode.ModDefault,
                        },
                    },
                    overwrite: false);
                object controller = typeof(PresetLobbyModSettingsViewModel).GetField(
                        "presetController",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(viewModel);
                var presets = (IReadOnlyList<PublishedModSettingsPreset>)controller.GetType()
                    .GetProperty("PublishedPresets")
                    .GetValue(controller);
                PublishedModSettingsPreset preset = presets.Single(item => item.Id == "dynamic-mod-default");
                controller.GetType().GetMethod("LoadPreset").Invoke(controller, new object[] { preset });
                Assert(viewModel.DynamicValues.SequenceEqual(new[] { "fixed-a", "fixed-b" }),
                    "a published ModDefault value must resolve through the dynamically materialized default");
                AssertThrows<InvalidDataException>(
                    () => viewModel.PublishUnknownDefault(),
                    "dynamic defaults must reject unknown persistent properties");
                AssertThrows<InvalidDataException>(
                    () => viewModel.PublishWrongTypeDefault(),
                    "dynamic defaults must reject mismatched property types");
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
        [TestMethod]
        [TestCategory("Settings")]
        public void VerifyWorkingSourceRegistry() => TestWorkingSourceRegistry();

        private static void TestWorkingSourceRegistry()
        {
            var provider = new RecordingWorkingSourceProvider();
            var viewModel = new PresetSaveTestViewModel();
            viewModel.PreparePresets(
                null,
                typeof(RuntimeTests).Assembly.Location,
                "Working source selection",
                "Tests.WorkingSourceSelection",
                new Version(1, 0, 0));
            int changes = 0;
            Action changed = () => changes++;
            ModSettingsWorkingSourceRegistry.SourcesChanged += changed;
            try
            {
                ModSettingsWorkingSourceRegistry.Register(provider);
                IReadOnlyList<ModSettingsWorkingSource> sources = ModSettingsWorkingSourceRegistry.GetProviderSources("Target");
                Assert(sources.Count == 2 && sources[0].Kind == ModSettingsWorkingSourceKind.Trail &&
                    sources[0].IsPreferred && sources[1].Kind == ModSettingsWorkingSourceKind.Map,
                    "working-source registry must expose optional Trail and Map sources in provider order");
                Assert(viewModel.System_SelectedSettingsSource?.Id == ModSettingsWorkingSourceRegistry.TrailId,
                    "entering a Trail context must preselect the preferred Trail source");
                viewModel.System_SelectedSettingsSource = viewModel.System_SettingsSources.Single(item =>
                    item.Id == ModSettingsWorkingSourceRegistry.MapId);
                provider.RaiseChanged();
                Assert(viewModel.System_SelectedSettingsSource?.Id == ModSettingsWorkingSourceRegistry.MapId,
                    "a refresh of the same context must retain a manual source selection");
                provider.PreferenceContextId = "trail-b";
                provider.RaiseChanged();
                Assert(viewModel.System_SelectedSettingsSource?.Id == ModSettingsWorkingSourceRegistry.TrailId,
                    "a different mission with the same preferred source kind must still restore its contextual preference");
                provider.PreferredId = ModSettingsWorkingSourceRegistry.MapId;
                provider.RaiseChanged();
                Assert(viewModel.System_SelectedSettingsSource?.Id == ModSettingsWorkingSourceRegistry.MapId,
                    "a changed context must select its new preferred source");
                provider.PreferredId = string.Empty;
                provider.RaiseChanged();
                Assert(viewModel.System_SelectedSettingsSource?.Id == ModSettingsWorkingSourceRegistry.ModDefaultsId,
                    "leaving mission sources must return the selector to Mod defaults");
                ModSettingsWorkingSourceRegistry.Apply("Target", ModSettingsWorkingSourceRegistry.MapId);
                ModSettingsWorkingSourceRegistry.ApplyMany(new[] { "A", "B" }, ModSettingsWorkingSourceRegistry.TrailId);
                Assert(provider.Calls.SequenceEqual(new[] { "one:Target:map", "many:A,B:trail" }),
                    "working-source registry must forward single and atomic multi-target applications");
                provider.RaiseChanged();
                ModSettingsWorkingSourceRegistry.Unregister(provider);
                Assert(changes == 7 && !ModSettingsWorkingSourceRegistry.HasProvider,
                    "working-source registration, provider refresh, and failed-initialization rollback must notify consumers");
            }
            finally
            {
                ModSettingsWorkingSourceRegistry.Unregister(provider);
                ModSettingsWorkingSourceRegistry.SourcesChanged -= changed;
            }
        }

    }
}
