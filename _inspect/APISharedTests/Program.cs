using APIShared;
using Iced.Intel;
using Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

namespace APISharedTests
{
    internal static class Program
    {
        private const long ModuleBase = 0x10000000;
        private const int FunctionRva = 0x1100;
        private const int FunctionSize = 0x300;
        private const int DistanceRva = 0x1180;
        private const int DecisionRva = 0x1200;
        private const int HumanDelayRva = 0x1280;
        private static readonly byte[] VanillaDistanceBytes = Hex("01 02 03 04");
        private static readonly byte[] CenteredDistanceBytes = Hex("05 06 07 08");
        private static readonly byte[] SupportedCenteredDistanceBytes = Hex(
            "44 0F BF 84 2A 0E 8B 7E 06 0F BF 8C 2A 10 8B 7E 06 0F BF 84 2B 0A CD 4C 06 " +
            "44 01 F8 C1 E0 02 44 29 C0 99 31 D0 29 D0 41 89 C0 0F BF 84 2B 0C CD 4C 06 " +
            "44 01 E0 C1 E0 02 29 C8 99 31 D0 29 D0 41 39 C0 44 0F 4C C0 90 90 90 90 90");
        private static readonly byte[] DecisionBytes = Hex(
            "40 84 F6 75 10 41 81 F8 C8 00 00 00 7D 10 B8 B0 04 00 00 EB 69 41 81 F8 8C 00 00 00 7C 5B");
        private static readonly byte[] HumanDelayBytes = Hex("EB 50 B8 64 00 00 00 48 8D 2D C0 83 F4 FF");
        private static int failures;

        private static int Main()
        {
            // The publicized fixture keeps its file name but has Assembly-CSharp as its identity.
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                new AssemblyName(args.Name).Name == "Assembly-CSharp"
                    ? Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assembly-CSharp-publicized.dll"))
                    : null;
            PlayerDefeatTests.Run();
            TestPublicSurface();
            TestElevatedMoatAiState();
            TestPublishedPresetJson();
            TestPublishedPresetDiscovery();
            TestPresetSaveUiModel();
            TestDynamicModDefaults();
            TestWorkingSourceRegistry();
            TestCompiledPatternSearch();
            TestUnitHudSnapshotImmutability();
            TestLobbyStateCapability();
            TestBriefingGoldPresentation();
            MissionLifecycleTests.Run(Assert);
            TestUnitHudVariantContracts();
            TestUnitHudLiveSelectionCounts();
            TestUnitHudSelectionIdentity();
            TestUnitHudActivation();
            TestPeValidation();
            TestFixedCatalogValidation();
            TestReadinessAndIndependentCapabilities();
            TestOwnership();
            TestCenteredDistanceSemantics();
            TestGatehouseDistanceOriginTransaction();
            TestGatehouseTransactionAndRounding();
            TestGatehouseRollbackAndPageCleanup();
            TestGatehouseConcurrentPublication();
            TestGatehouseAssemblerContracts();
            TestAivBuildStepBroker();
            TestMigrationContracts();
            if (failures == 0)
            {
                Console.WriteLine("PASS: APIShared baseline-hardened tests passed.");
                return 0;
            }
            Console.Error.WriteLine($"FAIL: APIShared tests reported {failures} failure(s).");
            return 1;
        }

        private static void TestElevatedMoatAiState()
        {
            int changes = 0;
            Action<ElevatedMoatAiState> observer = _ => changes++;
            ElevatedMoatAiCapability.Changed += observer;
            Assert(ElevatedMoatAiCapability.Current == ElevatedMoatAiState.Unknown,
                "elevated AI construction starts unknown");
            ElevatedMoatAiCapability.Publish(ElevatedMoatAiState.Enabled);
            ElevatedMoatAiCapability.Publish(ElevatedMoatAiState.Enabled);
            Assert(ElevatedMoatAiCapability.Current == ElevatedMoatAiState.Enabled && changes == 1,
                "effective enabled state is published once");
            ElevatedMoatAiCapability.Publish(ElevatedMoatAiState.Disabled);
            Assert(ElevatedMoatAiCapability.Current == ElevatedMoatAiState.Disabled && changes == 2,
                "effective disabled state reaches consumers");
            ElevatedMoatAiCapability.Changed -= observer;
            ElevatedMoatAiCapability.Publish(ElevatedMoatAiState.Unknown);
        }

        private static void TestUnitHudActivation()
        {
            Type type = typeof(UnitHudPresentationService);
            object service = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single()
                .Invoke(new object[] { "test", null, null, false });
            Func<string, object> field = name => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service);
            Action<string, object> set = (name, value) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(service, value);
            Func<string, IUnitHudPresentationCapability> bind = owner => ((UnitHudPresentationService)service).Bind(owner);
            var passive = bind("read-only");
            passive.RequestRefresh();
            Assert(!(bool)field("pendingPresentation") && !(bool)field("refreshRequested"), "capability lookup and passive refresh do not enable presentation");
            // Render entry order is checked below against source: standalone CLR cannot JIT Unity ECalls.
            Assert((UnitHudSurface)field("activeSurfaces") == UnitHudSurface.None && field("recruitmentLease") == null,
                "passive owner satisfies the render idle guard");

            var a = bind("a"); var b = bind("b");
            var aa = (IUnitHudActivationCapability)a; var ba = (IUnitHudActivationCapability)b;
            aa.SetOwnerActive(false);
            int calls = 0;
            Assert(a.TryRegisterCategory(new UnitHudCategoryDefinition("lord", "Lord", 55, UnitHudSurface.All), u => { calls++; return true; }, out _), "inactive registration succeeds");
            Assert((UnitHudSurface)field("activeSurfaces") == UnitHudSurface.None && !(bool)field("pendingPresentation"), "inactive registration does not wake HUD");
            a.RequestRefresh();
            Assert(!(bool)field("refreshRequested") && a.GetSelectedCategories().Count == 0 && calls == 0, "inactive refresh and queries perform no classification");
            aa.SetOwnerActive(true);
            Assert(((UnitHudSurface)field("activeSurfaces") & UnitHudSurface.ArmyReport) != 0 &&
                ((UnitHudSurface)field("activeSurfaces") & UnitHudSurface.Recruitment) == 0, "All category does not enable recruitment without handler");
            Assert(b.TryRegisterCategory(new UnitHudCategoryDefinition("archer", "Archer", 22, UnitHudSurface.TroopSelection | UnitHudSurface.Recruitment), u => false, out _), "legacy registrations remain active");
            Assert(b.TryRegisterRecruitment("archer", ticket => true, out _), "recruitment attaches to owner category");
            Assert((bool)field("activeRecruitmentHandlers"), "active handler enables recruitment");
            aa.SetOwnerActive(false);
            Assert(((UnitHudSurface)field("activeSurfaces") & UnitHudSurface.ArmyReport) == 0 && (bool)field("activeRecruitmentHandlers"), "disabling one owner preserves another");
            Assert(!aa.SetCategoryActive("archer", false) && ba.SetCategoryActive("archer", false), "activation is owner-bound");
            Assert(!(bool)field("activeRecruitmentHandlers") && (UnitHudSurface)field("activeSurfaces") == UnitHudSurface.None, "category disable also disables recruitment");
            Assert((UnitHudSurface)field("restoreSurfaces") != UnitHudSurface.None && (bool)field("pendingPresentation"), "last disable retains restoration work");
            Assert(ba.SetCategoryActive("archer", true) && (bool)field("activeRecruitmentHandlers"), "category reactivation works without re-registration");
            ba.SetOwnerActive(false);
            var image = bind("image");
            Assert(image.TryRegisterImageOverride(new UnitHudImageOverrideDefinition("skin", UnitHudImageSlot.UIBuildingsO011), ctx => null, out _), "image-only registration succeeds");
            Assert((bool)field("activeImages") && (UnitHudSurface)field("activeSurfaces") == UnitHudSurface.None, "image-only owner enables no unit surfaces");
            // Simulate completed restoration/refresh; the image hook will wake only for real sprite changes.
            set("pendingPresentation", false); set("refreshRequested", false);
            Assert((UnitHudSurface)field("activeSurfaces") == UnitHudSurface.None && field("recruitmentLease") == null,
                "image-only owner satisfies the render idle guard after refresh");
            Assert(((IUnitHudActivationCapability)image).SetImageOverrideActive("skin", false) && !(bool)field("activeImages"), "individual image override disables");
            Assert((bool)field("restoreImages"), "image disable requests Vanilla restoration");
        }

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

            string presetSource = File.ReadAllText(Path.Combine(
                FindWorkspaceRoot(),
                "APIShared",
                "src",
                "PresetLobbyModSettingsViewModel.cs"));
            Assert(presetSource.Contains("Common.PresetModeMixed") &&
                presetSource.Contains("IsEnabled = false"),
                "the mixed preset-save option must be visible but not selectable");
            Assert(Enum.GetValues(typeof(PresetSaveBulkMode)).Length == 5 &&
                    (int)PresetSaveBulkMode.HostFixed == 3 &&
                    (int)PresetSaveBulkMode.Mixed == 4,
                "the bulk selector contract must expose four actions plus the Mixed status");
            Assert(presetSource.Contains("Common.PresetModeHostFixed") &&
                    presetSource.Contains("System_PresetSaveBulkModeHelpText") &&
                    !presetSource.Contains("System_SelectAllPresetSaveSettingsCommand") &&
                    !presetSource.Contains("System_SelectHostPresetSaveSettingsCommand"),
                "the standard save UI must expose Host Fixed, explain modes, and save every row without inclusion controls");
            Assert(presetSource.Contains("System_DeletePresetCommand") &&
                    presetSource.Contains("Common.PresetDeleteConfirm") &&
                    presetSource.Contains("preset.SourceKind != ModSettingsPresetSourceKind.Personal"),
                "personal preset deletion must be confirmed and fail closed by source kind");
            Assert(presetSource.Contains("System_PresetInlineConfirmationVisibility") &&
                    presetSource.Contains("ConfirmPresetInlineAction") &&
                    presetSource.Contains("System_PresetOperationStatusVisibility") &&
                    !presetSource.Contains("LobbyPresetDialogSession") &&
                    !presetSource.Contains("ShowLobbyPresetConfirmation") &&
                    !presetSource.Contains("ModSettingsHubViewModel.WindowVisibility = Visibility.Collapsed"),
                "preset confirmations and result messages must stay inline without hiding the ModSettings hub");
            Assert(!presetSource.Contains("Common.SettingsSourceLoaded") &&
                    !presetSource.Contains("Common.PresetDeleteCompleted") &&
                    !presetSource.Contains("Common.PresetSaveCompletedTitle") &&
                    presetSource.Contains("DismissPresetStatus();") &&
                    presetSource.Contains("Could not reset settings"),
                "successful preset and settings-reset actions must stay silent while failures remain explicit");
            Assert(presetSource.Contains("ResolveSettingsUiTextSafe(\"Common.PresetLoadFailedTitle\", \"Preset load failed\")"),
                "preset-load failures must not be reported as save failures");
            Assert(presetSource.Contains("RaiseAccessProperties();\r\n                DismissPresetStatus();") ||
                    presetSource.Contains("RaiseAccessProperties();\n                DismissPresetStatus();"),
                "a successful preset load must clear a stale error status");
            Assert(!presetSource.Contains("SuggestedSaveName"),
                "the new-personal-preset form must not inherit the active preset name");
        }

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

        private static void TestWorkingSourceRegistry()
        {
            var provider = new RecordingWorkingSourceProvider();
            var viewModel = new PresetSaveTestViewModel();
            viewModel.PreparePresets(
                null,
                typeof(Program).Assembly.Location,
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

        private static void TestCompiledPatternSearch()
        {
            const string aivPattern =
                "40 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 78 4C 63 F2";
            const string hudPattern =
                "48 8D 1D ? ? ? ? 48 8B F8 48 8B E9 48 8D 05 ? ? ? ? BE 0A 00 00 00 45 33 F6";

            byte[] installedImage = MapPeImage(File.ReadAllBytes(Path.Combine(
                @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition",
                "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll")));
            Assert(CompiledBytePattern.Parse(aivPattern).FindUnique(installedImage) == 0x51790,
                "compiled AIV pattern must retain its canonical RVA");
            Assert(CompiledBytePattern.Parse(hudPattern).FindUnique(installedImage) == 0x186338,
                "compiled HUD pattern must retain its canonical RVA");

            var random = new Random(0x53E2);
            string[] patterns =
            {
                "AA", "AA BB CC", "? BB CC", "AA ? CC", "AA BB ?", "? ?", "10 ? ? 40"
            };
            foreach (string pattern in patterns)
            {
                CompiledBytePattern compiled = CompiledBytePattern.Parse(pattern);
                for (int iteration = 0; iteration < 100; iteration++)
                {
                    var data = new byte[random.Next(0, 160)];
                    random.NextBytes(data);
                    if (iteration % 4 == 0 && data.Length >= compiled.Length)
                        StampPattern(data, random.Next(0, data.Length - compiled.Length + 1), pattern, random);
                    if (iteration % 11 == 0 && data.Length >= compiled.Length * 2)
                    {
                        StampPattern(data, 0, pattern, random);
                        StampPattern(data, data.Length - compiled.Length, pattern, random);
                    }
                    Assert(compiled.FindUnique(data) == FindUniqueReference(data, pattern),
                        "compiled pattern search differs from reference semantics for " + pattern);
                }
            }
        }

        private static int FindUniqueReference(byte[] data, string pattern)
        {
            string[] tokens = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int found = -1;
            for (int offset = 0; offset <= data.Length - tokens.Length; offset++)
            {
                bool matches = true;
                for (int index = 0; index < tokens.Length; index++)
                {
                    if (tokens[index] != "?" && tokens[index] != "??" &&
                        data[offset + index] != Convert.ToByte(tokens[index], 16))
                    {
                        matches = false;
                        break;
                    }
                }
                if (!matches)
                    continue;
                if (found >= 0)
                    return -2;
                found = offset;
            }
            return found;
        }

        private static void StampPattern(byte[] data, int offset, string pattern, Random random)
        {
            string[] tokens = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < tokens.Length; index++)
                data[offset + index] = tokens[index] == "?" || tokens[index] == "??"
                    ? (byte)random.Next(0, 256)
                    : Convert.ToByte(tokens[index], 16);
        }

        private static byte[] MapPeImage(byte[] file)
        {
            int peOffset = BitConverter.ToInt32(file, 0x3C);
            int sectionCount = BitConverter.ToUInt16(file, peOffset + 6);
            int optionalHeaderSize = BitConverter.ToUInt16(file, peOffset + 20);
            int optionalHeader = peOffset + 24;
            int imageSize = BitConverter.ToInt32(file, optionalHeader + 56);
            int headersSize = BitConverter.ToInt32(file, optionalHeader + 60);
            var mapped = new byte[imageSize];
            Buffer.BlockCopy(file, 0, mapped, 0, Math.Min(headersSize, file.Length));
            int sectionTable = optionalHeader + optionalHeaderSize;
            for (int index = 0; index < sectionCount; index++)
            {
                int header = sectionTable + index * 40;
                int virtualAddress = BitConverter.ToInt32(file, header + 12);
                int rawSize = BitConverter.ToInt32(file, header + 16);
                int rawOffset = BitConverter.ToInt32(file, header + 20);
                if (rawSize > 0)
                    Buffer.BlockCopy(file, rawOffset, mapped, virtualAddress, rawSize);
            }
            return mapped;
        }

        private static void TestUnitHudSnapshotImmutability()
        {
            var original = new List<UnitHudUnitSnapshot>
            {
                new UnitHudUnitSnapshot(1, 10, 2, 1, true)
            };
            var category = new UnitHudCategorySnapshot("owner", "category", "Category", original);
            var group = new UnitHudControlGroupSnapshot(0, original);
            original.Clear();
            Assert(category.Units.Count == 1 && group.Units.Count == 1,
                "unit-HUD snapshots defensively copy caller-owned membership lists");
            Assert(!(category.Units is UnitHudUnitSnapshot[]) && !(group.Units is UnitHudUnitSnapshot[]),
                "unit-HUD snapshot membership is not exposed as a mutable array");
        }

        private static void TestLobbyStateCapability()
        {
            var source = new Dictionary<int, ulong> { [1] = 1001UL };
            var first = new LobbyStateSnapshot(
                42UL, source, false, 1, false, string.Empty, string.Empty);
            source[1] = 9001UL;
            Assert(first.Players[1] == 1001UL,
                "lobby snapshots must copy their player map");
            AssertThrows<NotSupportedException>(
                () => ((IDictionary<int, ulong>)first.Players)[2] = 1002UL,
                "lobby snapshot player maps must be immutable");

            var equal = new LobbyStateSnapshot(
                42UL,
                new Dictionary<int, ulong> { [1] = 1001UL },
                false,
                1,
                false,
                string.Empty,
                string.Empty);
            var changed = new LobbyStateSnapshot(
                42UL,
                new Dictionary<int, ulong> { [1] = 1001UL, [2] = 1002UL },
                false,
                1,
                false,
                string.Empty,
                string.Empty);
            Assert(LobbyStateService.ValueEquals(first, equal) &&
                !LobbyStateService.ValueEquals(first, changed),
                "lobby snapshot equality must compare every published value");
            Assert(LobbyStateService.ShouldObserve(false, true, 10, 11) &&
                !LobbyStateService.ShouldObserve(false, false, 10, 24) &&
                LobbyStateService.ShouldObserve(false, false, 10, 25) &&
                !LobbyStateService.ShouldObserve(true, true, 10, 25) &&
                LobbyStateService.ShouldObserve(false, true, 25, 26),
                "lobby polling must honor dirty, 15-frame fallback, map suppression and dirty resume");

            var service = new LobbyStateService(null);
            var calls = new List<string>();
            ILobbyStateCapability zOwner = service.Bind("z.owner");
            ILobbyStateCapability aOwner = service.Bind("a.owner");
            Assert(zOwner.TryRegisterObserver("one", _ => calls.Add("z"), out _),
                "first lobby observer registration should succeed");
            Assert(aOwner.TryRegisterObserver("two", snapshot =>
            {
                calls.Add("a2:" + snapshot.Players.Count);
                if (snapshot.Players.Count == 1)
                    service.System_TestPublish(changed);
            }, out _), "second lobby observer registration should succeed");
            Assert(aOwner.TryRegisterObserver("one", _ => calls.Add("a1"), out _),
                "third lobby observer registration should succeed");
            Assert(!aOwner.TryRegisterObserver("one", _ => { }, out NativeCapabilityDiagnostic duplicate) &&
                duplicate.State == NativeCapabilityState.ValidationFailed,
                "duplicate lobby observer IDs must fail closed");

            service.System_TestPublish(first);
            Assert(string.Join(",", calls) == "a1,a2:1,z,a1,a2:2,z",
                "lobby observers must be ordered and nested publication must unwind deterministically");
            calls.Clear();
            service.System_TestPublish(new LobbyStateSnapshot(
                42UL,
                new Dictionary<int, ulong> { [1] = 1001UL, [2] = 1002UL },
                false,
                1,
                false,
                string.Empty,
                string.Empty));
            Assert(calls.Count == 0,
                "equal lobby snapshots must not be republished");
            Assert(aOwner.TryRegisterObserver("late", _ => calls.Add("late"), out _ ) &&
                string.Join(",", calls) == "late",
                "late lobby observers must receive the current snapshot synchronously");

            var isolated = new LobbyStateService(null);
            var isolatedCalls = new List<string>();
            isolated.Bind("a").TryRegisterObserver("throws", _ =>
                throw new InvalidOperationException("expected"), out _);
            isolated.Bind("b").TryRegisterObserver("continues", _ =>
                isolatedCalls.Add("continues"), out _);
            isolated.System_TestPublish(first);
            Assert(isolatedCalls.Count == 1,
                "one failing lobby observer must not stop later observers");

            var lifecycle = new LobbyStateService(null);
            var lifecycleStates = new List<string>();
            lifecycle.Bind("owner").TryRegisterObserver("lifecycle", snapshot =>
                lifecycleStates.Add(
                    snapshot.Error.Length != 0 ? "error" :
                    !snapshot.LobbyId.HasValue ? "left" :
                    snapshot.HasUnresolvedPlayers ? "unresolved" :
                    "lobby:" + snapshot.Players.Count), out _);
            lifecycle.System_TestPublish(new LobbyStateSnapshot(
                null, null, false, 0, false, string.Empty, string.Empty));
            lifecycle.System_TestPublish(first);
            lifecycle.System_TestPublish(new LobbyStateSnapshot(
                42UL,
                new Dictionary<int, ulong> { [1] = 1001UL, [2] = 1002UL },
                true,
                1,
                false,
                string.Empty,
                string.Empty));
            lifecycle.System_TestPublish(new LobbyStateSnapshot(
                null, null, true, 0, false, "capture failed", "details"));
            lifecycle.System_TestPublish(changed);
            lifecycle.System_TestPublish(new LobbyStateSnapshot(
                null, null, false, 0, false, string.Empty, string.Empty));
            Assert(string.Join(",", lifecycleStates) ==
                "left,lobby:1,unresolved,error,lobby:2,left",
                "lobby snapshots must publish join, membership, unresolved, error, recovery and leave transitions");
        }

        private static void TestUnitHudVariantContracts()
        {
            var oldCategory = new UnitHudCategoryDefinition("old", "Old", 1, UnitHudSurface.TroopSelection);
            Assert(oldCategory.TextProfile.DisplayNameFallback == "Old" && oldCategory.TextProfile.DescriptionFallback == string.Empty,
                "legacy category constructor must retain deterministic text fallbacks");
            var text = new UnitHudTextProfile("Desert Archer", "DA", "Description", kind => kind == UnitHudTextKind.DisplayName ? "Localized" : null);
            var category = new UnitHudCategoryDefinition("desert", "Desert Archer", 1,
                UnitHudSurface.All, null, new UnitHudTint(220, 240, 255, 255), 0, text);
            Assert(category.TextProfile == text && (category.Surfaces & UnitHudSurface.Recruitment) != 0 &&
                (category.Surfaces & UnitHudSurface.UnitDetails) != 0,
                "variant category does not expose recruitment and unit-detail presentation");
            var ticket = new UnitHudRecruitmentTicket(7, "owner", "desert", 1, 2, 5);
            Assert(ticket.TicketId == 7 && ticket.PlayerId == 1 && ticket.BaseUnitType == 2 && ticket.RequestedAmount == 5,
                "recruitment ticket does not preserve immutable Vanilla request data");
        }

        private static void TestUnitHudLiveSelectionCounts()
        {
            var lord = new UnitHudUnitSnapshot(100, 1000, 55, 1, true);
            var spearman = new UnitHudUnitSnapshot(200, 2000, 24, 1, true);
            var secondSpearman = new UnitHudUnitSnapshot(201, 2001, 24, 1, true);
            var archer = new UnitHudUnitSnapshot(300, 3000, 22, 1, true);
            var unsupported = new UnitHudUnitSnapshot(400, 4000, 1, 1, true);
            var claimed = new HashSet<int> { lord.GameId };
            int[] emptyVanilla = new int[89];

            int[] lordFirst = UnitHudSelectionPolicy.ResolveVanillaTroopCounts(
                emptyVanilla, new[] { lord, spearman, secondSpearman, archer, unsupported }, claimed, true);
            int[] troopsFirst = UnitHudSelectionPolicy.ResolveVanillaTroopCounts(
                emptyVanilla, new[] { spearman, secondSpearman, archer, lord, unsupported }, claimed, true);
            Assert(ArraysEqual(lordFirst, troopsFirst) && lordFirst[24] == 2 && lordFirst[22] == 1,
                "live troop counts must be independent of Lord selection order and preserve multiplicity");
            Assert(lordFirst[55] == 0 && lordFirst[1] == 0,
                "claimed and unsupported unit types must not become Vanilla troop slots");

            int visibleTypes = 0;
            var manyTypes = new List<UnitHudUnitSnapshot>();
            int[] types = { 5, 22, 23, 24, 25, 26, 27, 28, 30 };
            for (int i = 0; i < types.Length; i++)
                manyTypes.Add(new UnitHudUnitSnapshot(500 + i, (uint)(5000 + i), types[i], 1, true));
            int[] manyCounts = UnitHudSelectionPolicy.ResolveVanillaTroopCounts(
                emptyVanilla, manyTypes, new HashSet<int>(), true);
            for (int i = 0; i < manyCounts.Length; i++) if (manyCounts[i] > 0) visibleTypes++;
            Assert(visibleTypes == 9 && (visibleTypes + 7) / 8 == 2,
                "live troop counts must retain enough distinct types for Vanilla-compatible paging");

            int[] vanillaFallback = new int[89];
            vanillaFallback[24] = 3;
            int[] incomplete = UnitHudSelectionPolicy.ResolveVanillaTroopCounts(
                vanillaFallback, new[] { lord }, claimed, false);
            Assert(incomplete[24] == 3 && incomplete[55] == 0,
                "an incomplete live selection must preserve Vanilla troop counts");

            int[] afterRemoval = UnitHudSelectionPolicy.ResolveVanillaTroopCounts(
                emptyVanilla, new[] { archer }, new HashSet<int>(), true);
            Assert(afterRemoval[22] == 1 && afterRemoval[24] == 0 && afterRemoval[55] == 0,
                "removed Lord and troop members must disappear from live counts");
        }

        private static void TestUnitHudSelectionIdentity()
        {
            int[] ids = { 10, 20 };
            int[] types = { 55, 24 };
            Assert(UnitHudSelectionPolicy.SelectionIdentityEquals(ids, types, new[] { 10, 20, 0 }, new[] { 55, 24, 0 }, 2),
                "an unchanged troop selection must retain the same identity");
            Assert(!UnitHudSelectionPolicy.SelectionIdentityEquals(ids, types, new[] { 10, 20, 30 }, new[] { 55, 24, 22 }, 3) &&
                !UnitHudSelectionPolicy.SelectionIdentityEquals(ids, types, new[] { 10 }, new[] { 55 }, 1) &&
                !UnitHudSelectionPolicy.SelectionIdentityEquals(ids, types, new[] { 10, 20 }, new[] { 55, 22 }, 2) &&
                !UnitHudSelectionPolicy.SelectionIdentityEquals(ids, types, new[] { 20, 10 }, new[] { 24, 55 }, 2),
                "added, removed, retyped, or reordered units must invalidate the rendered selection identity");
        }

        private static bool ArraysEqual(int[] left, int[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }

        private static void TestMigrationContracts()
        {
            string workspace = FindWorkspaceRoot();
            string plugin = File.ReadAllText(Path.Combine(workspace, "APIShared", "src", "APISharedPlugin.cs"));
            string project = File.ReadAllText(Path.Combine(workspace, "APIShared", "APIShared.csproj"));
            string unitHud = File.ReadAllText(Path.Combine(workspace, "APIShared", "src", "UnitHudPresentationCapability.cs"));
            string lobbyState = File.ReadAllText(Path.Combine(workspace, "APIShared", "src", "LobbyStateCapability.cs"));
            string sharedPreset = File.ReadAllText(Path.Combine(workspace, "APIShared", "src", "PresetLobbyModSettingsViewModel.cs"));
            string virtualRuntime = File.ReadAllText(Path.Combine(workspace, "Testmods", "VirtualUnitsPrototype", "src", "VirtualEntityRuntime.cs"));
            string bugfixLord = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL", "src", "LordUnitHudRegistration.cs"));
            string bugfixGatehouse = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL", "src", "GatehouseDistanceOriginRegistration.cs"));
            string extraGatehouse = File.ReadAllText(Path.Combine(workspace, "ExtraFeatures", "src", "GatehouseAutomationRuntime.cs"));
            string extraProject = File.ReadAllText(Path.Combine(workspace, "ExtraFeatures", "ExtraFeatures.csproj"));
            string extraAiv = File.ReadAllText(Path.Combine(workspace, "ExtraFeatures", "src", "AIDefenseRepairRuntime.cs"));
            string activeAiv = File.ReadAllText(Path.Combine(workspace, "Helpers", "ActiveAIVDetector", "src", "AivPlacementOracle.cs"));
            string activeRuntime = File.ReadAllText(Path.Combine(workspace, "Helpers", "ActiveAIVDetector", "src", "ActiveAIVDetectionRuntime.cs"));
            string activePlugin = File.ReadAllText(Path.Combine(workspace, "Helpers", "ActiveAIVDetector", "src", "ActiveAIVDetectorPlugin.cs"));
            string activeProject = File.ReadAllText(Path.Combine(workspace, "Helpers", "ActiveAIVDetector", "ActiveAIVDetector.csproj"));
            string bugfixControlGroups = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL", "src", "ControlGroupDisbandCleanupRuntime.cs"));
            string bugfixNativeDefinition = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL", "src", "ControlGroupNativeDefinition.cs"));
            string bugfixPlugin = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL", "src", "BugfixesAndQoLPlugin.cs"));
            string bugfixProject = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL", "BugfixesAndQoL.csproj"));
            string castlePlanner = File.ReadAllText(Path.Combine(workspace, "CastlePlanner", "src", "CastlePlannerRuntime.cs"));
            string castlePlugin = File.ReadAllText(Path.Combine(workspace, "CastlePlanner", "src", "CastlePlannerPlugin.cs"));
            string castleProject = File.ReadAllText(Path.Combine(workspace, "CastlePlanner", "CastlePlanner.csproj"));
            string customPlugin = File.ReadAllText(Path.Combine(workspace, "ExtendedData", "src", "ExtendedDataPlugin.cs"));
            string customProject = File.ReadAllText(Path.Combine(workspace, "ExtendedData", "ExtendedData.csproj"));
            string extremePlugin = File.ReadAllText(Path.Combine(workspace, "ExtremePowers", "src", "ExtremePowersPlugin.cs"));
            string extremeProject = File.ReadAllText(Path.Combine(workspace, "ExtremePowers", "ExtremePowers.csproj"));
            string releaseConfig = File.ReadAllText(Path.Combine(workspace, "Shared", "Release", "release-projects.json"));
            Func<string, string, bool> apiDependencyMatchesRelease = (source, consumer) =>
            {
                Match dependency = Regex.Match(source,
                    @"BepInDependency\((?:ApiSharedGuid|""APIShared_Serp"")\s*,\s*""(?<version>[^""]+)""\)");
                Match declared = Regex.Match(releaseConfig,
                    @"""" + Regex.Escape(consumer) + @"""\s*:\s*""(?<version>[^""]+)""");
                return dependency.Success && declared.Success &&
                    dependency.Groups["version"].Value == declared.Groups["version"].Value;
            };
            string releaseScript = File.ReadAllText(Path.Combine(workspace, "Shared", "Release", "Release-Mod.ps1"));
            string nexusScript = File.ReadAllText(Path.Combine(workspace, "Shared", "Release", "NexusRelease.Common.ps1"));
            string steamScript = File.ReadAllText(Path.Combine(workspace, "Shared", "Steam", "Create-SteamModPack.ps1"));
            string hostPlugin = File.ReadAllText(Path.Combine(workspace, "SerpsModsHost", "src", "SerpsModsHostPlugin.cs"));
            string randomRuntime = File.ReadAllText(Path.Combine(workspace, "RandomEvents", "src", "RandomEventsRuntime.cs"));
            string randomRegistry = File.ReadAllText(Path.Combine(workspace, "RandomEvents", "src", "ScenarioSignpostRegistry.cs"));
            string randomPlacement = File.ReadAllText(Path.Combine(workspace, "RandomEvents", "src", "SignpostPlacementService.cs"));
            string randomPlugin = File.ReadAllText(Path.Combine(workspace, "RandomEvents", "src", "RandomEventsPlugin.cs"));
            string randomManifest = File.ReadAllText(Path.Combine(workspace, "RandomEvents", "info.json"));
            string hunterRoutes =
                File.ReadAllText(Path.Combine(workspace, "ImprovedHunters", "src", "HunterPclReachability.cs")) + "\n" +
                File.ReadAllText(Path.Combine(workspace, "ImprovedHunters", "src", "HunterActiveTargetReachability.cs")) + "\n" +
                File.ReadAllText(Path.Combine(workspace, "ImprovedHunters", "src", "HunterPclReachabilityDiagnostic.cs"));
            string sourceManifest = File.ReadAllText(Path.Combine(workspace, "APIShared", "info.json"));
            string packageManifest = File.ReadAllText(Path.Combine(workspace, "APIShared", "BepInEx", "plugins", "APIShared_Serp", "info.json"));
            string buildingPatchPath = Path.Combine(workspace, "APIShared", "Patches", "Assets", "GUI", "XAMLResources", "HUD_Buildings.xaml");
            string packagedBuildingPatchPath = Path.Combine(workspace, "APIShared", "BepInEx", "plugins", "APIShared_Serp", "Patches", "Assets", "GUI", "XAMLResources", "HUD_Buildings.xaml");
            string buildingPatch = File.ReadAllText(buildingPatchPath);
            string troopPatch = File.ReadAllText(Path.Combine(workspace, "APIShared", "Patches", "Assets", "GUI", "XAMLResources", "HUD_Troops.xaml"));
            Match minimumMatch = Regex.Match(sourceManifest,
                @"""MinimumScriptExtenderVersion""\s*:\s*""([^""]*)""");
            string minimumExtenderVersion = minimumMatch.Success ? minimumMatch.Groups[1].Value : string.Empty;
            Match versionMatch = Regex.Match(sourceManifest, @"""Version""\s*:\s*""([^""]+)""");
            string modVersion = versionMatch.Success ? versionMatch.Groups[1].Value : string.Empty;

            Assert(string.IsNullOrEmpty(minimumExtenderVersion) ||
                plugin.Contains($"[BepInDependency(ScriptExtenderGuid, \"{minimumExtenderVersion}\")]"),
                "plugin dependency matches the source manifest minimum");
            Assert(plugin.Contains("OnLibraryLoaded(CrusaderLibraryLoadContext context)"),
                "plugin consumes CrusaderLibraryLoadContext");
            Assert(plugin.Contains("context.ModuleHandle.ToInt64()") && plugin.Contains("context.Memory"),
                "plugin passes the Script Extender module and memory view");
            Assert(!plugin.Contains("IntPtr libraryHandle") && !plugin.Contains("ReadOnlySpan<byte> memory"),
                "old LibraryLoaded callback is absent");
            Assert(!project.Contains("Zhuqiaomon") && !project.Contains("PolyHook"),
                "project has no obsolete native dependency");
            Assert(!project.Contains("SelectedUnitCommandCapability") &&
                typeof(IApiShared).GetMethod("TryGetSelectedUnitCommand", BindingFlags.Public | BindingFlags.Instance) == null,
                "the redundant selected-unit broker must not remain in APIShared");
            int menuContextStart = sharedPreset.IndexOf("private static SettingsMenuContext CaptureSettingsMenuContext()", StringComparison.Ordinal);
            int menuContextEnd = menuContextStart >= 0
                ? sharedPreset.IndexOf("private static SettingsMenuContext ResolveSettingsMenuContext(", menuContextStart, StringComparison.Ordinal)
                : -1;
            string menuContext = menuContextStart >= 0 && menuContextEnd > menuContextStart
                ? sharedPreset.Substring(menuContextStart, menuContextEnd - menuContextStart)
                : string.Empty;
            int menuReadyGuard = menuContext.IndexOf("if (!CrusaderDE.MainViewModel.viewModelLoaded)", StringComparison.Ordinal);
            int menuSingletonRead = menuContext.IndexOf("CrusaderDE.MainViewModel.Instance", StringComparison.Ordinal);
            int menuNeutralReturn = menuReadyGuard >= 0
                ? menuContext.IndexOf("return SettingsMenuContext.Other;", menuReadyGuard, StringComparison.Ordinal)
                : -1;
            Assert(menuReadyGuard >= 0 && menuReadyGuard < menuSingletonRead &&
                menuNeutralReturn > menuReadyGuard && menuNeutralReturn < menuSingletonRead &&
                sharedPreset.Contains("Plugin.ModSettingsHubViewModel.PropertyChanged += (_, __) =>") &&
                sharedPreset.Contains("viewModel.System_RefreshSettingsAccess();"),
                "settings menu context must not construct the Vanilla ViewModel before readiness and must refresh when the hub changes");
            Assert(!project.Contains("LocalScriptExtenderBuildOutput") &&
                !project.Contains("LocalScriptExtenderModOutput"),
                "APIShared must default to the installed Script Extender without dead local fallbacks");
            Assert(Count(unitHud, "setupTroopsOriginal(panel)") == 1 &&
                Count(unitHud, "populateGroupsOriginal(panel)") == 1 &&
                Count(unitHud, "gameActionOriginal(command, value1, value2, value3)") == 1 &&
                Count(unitHud, "updateSpritesOriginal(self, colour, arabic)") == 1 &&
                Count(unitHud, "updateSpritesOriginal(main, lastSpriteColour, lastSpriteArabic)") == 2,
                "central HUD sprite handling retains the hook, explicit refresh and activation-restoration paths");
            int beforeRenderStart = unitHud.IndexOf("private void OnBeforeRender()", StringComparison.Ordinal);
            int applyFrameStart = unitHud.IndexOf("private void TryApplyFrameArea(", StringComparison.Ordinal);
            string beforeRenderMethod = beforeRenderStart >= 0 && applyFrameStart > beforeRenderStart
                ? unitHud.Substring(beforeRenderStart, applyFrameStart - beforeRenderStart)
                : string.Empty;
            int idleGuard = beforeRenderMethod.IndexOf("if (activeSurfaces == UnitHudSurface.None && !pendingPresentation && !refreshRequested && recruitmentLease == null) return;", StringComparison.Ordinal);
            Assert(idleGuard >= 0 && idleGuard < beforeRenderMethod.IndexOf("Time.frameCount", StringComparison.Ordinal),
                "idle guard returns before Unity access");
            Assert(beforeRenderMethod.IndexOf("ExpireRecruitment();", StringComparison.Ordinal) < beforeRenderMethod.IndexOf("MainViewModel.viewModelLoaded", StringComparison.Ordinal),
                "pending tickets expire even when the HUD is unavailable");
            int selectionRefreshStart = beforeRenderMethod.IndexOf("if ((refresh && HasCategories(UnitHudSurface.TroopSelection)) || troopSelectionChanged)", StringComparison.Ordinal);
            int explicitRefreshStart = beforeRenderMethod.IndexOf("if (refresh)", selectionRefreshStart + 1, StringComparison.Ordinal);
            string selectionRefreshBlock = selectionRefreshStart >= 0 && explicitRefreshStart > selectionRefreshStart
                ? beforeRenderMethod.Substring(selectionRefreshStart, explicitRefreshStart - selectionRefreshStart)
                : string.Empty;
            string explicitRefreshBlock = explicitRefreshStart >= 0
                ? beforeRenderMethod.Substring(explicitRefreshStart)
                : string.Empty;
            Assert(selectionRefreshBlock.Contains("SetupSelectedTroops()") &&
                !selectionRefreshBlock.Contains("HUDControlGroups") &&
                !selectionRefreshBlock.Contains("updateSpritesOriginal") &&
                !selectionRefreshBlock.Contains("ApplyImageOverrides"),
                "selection-only refreshes must update the troop HUD without touching control groups or global images");
            Assert(explicitRefreshBlock.Contains("HUDControlGroups?.Update()") &&
                explicitRefreshBlock.Contains("updateSpritesOriginal(main, lastSpriteColour, lastSpriteArabic)") &&
                explicitRefreshBlock.Contains("ApplyImageOverrides(main, lastSpriteColour, lastSpriteArabic)"),
                "explicit refreshes must retain control-group and legitimate global image updates");
            int ensureButtonsStart = unitHud.IndexOf("private void EnsureCategoryButtons(", StringComparison.Ordinal);
            int hideButtonsStart = unitHud.IndexOf("private void HideCategoryButtons()", StringComparison.Ordinal);
            int mouseDownStart = unitHud.IndexOf("private void OnCategoryMouseDown(", StringComparison.Ordinal);
            string ensureButtonsMethod = ensureButtonsStart >= 0 && hideButtonsStart > ensureButtonsStart
                ? unitHud.Substring(ensureButtonsStart, hideButtonsStart - ensureButtonsStart)
                : string.Empty;
            string hideButtonsMethod = hideButtonsStart >= 0 && mouseDownStart > hideButtonsStart
                ? unitHud.Substring(hideButtonsStart, mouseDownStart - hideButtonsStart)
                : string.Empty;
            Assert(ensureButtonsMethod.Contains("var resolvedHosts = new Grid[TroopSlotCount]") &&
                ensureButtonsMethod.Contains("var resolvedButtons = new Button[TroopSlotCount]") &&
                ensureButtonsMethod.Contains("var resolvedTints = new Border[TroopSlotCount]") &&
                ensureButtonsMethod.IndexOf("categoryHosts = resolvedHosts", StringComparison.Ordinal) >
                ensureButtonsMethod.IndexOf("foreach (Button button in resolvedButtons)", StringComparison.Ordinal),
                "troop category hosts, buttons, and tints must be resolved completely before the cache is published");
            Assert(hideButtonsMethod.Contains("if (host != null)") &&
                unitHud.Contains("lock (sync) visibleSlots.Clear();"),
                "troop HUD failure cleanup must tolerate incomplete caches and clear stale slot snapshots");
            int armyStart = unitHud.IndexOf("private void ApplyArmyReport(", StringComparison.Ordinal);
            int armyEntriesStart = unitHud.IndexOf("private readonly Dictionary<string, Noesis.Grid> armyEntries", StringComparison.Ordinal);
            string armyMethod = armyStart >= 0 && armyEntriesStart > armyStart
                ? unitHud.Substring(armyStart, armyEntriesStart - armyStart)
                : string.Empty;
            Assert(armyMethod.IndexOf("APISharedArmyCategoriesHost", StringComparison.Ordinal) >= 0 &&
                armyMethod.IndexOf("APISharedArmyCategoriesHost", StringComparison.Ordinal) < armyMethod.IndexOf("main.AllTroops[", StringComparison.Ordinal) &&
                armyMethod.IndexOf("RenderArmyHosts(host, custom);", StringComparison.Ordinal) < armyMethod.IndexOf("main.AllTroops[desired.Key]", StringComparison.Ordinal),
                "army HUD host must be resolved before Vanilla troop counts are changed");
            Assert(unitHud.Contains("ManualApply = true") &&
                unitHud.Contains("updateSpritesOriginal = updateSpritesHook.GenerateTrampoline") &&
                unitHud.Contains("updateSpritesHook.Apply();") &&
                unitHud.IndexOf("updateSpritesOriginal = updateSpritesHook.GenerateTrampoline", StringComparison.Ordinal) <
                    unitHud.IndexOf("updateSpritesHook.Apply();", StringComparison.Ordinal),
                "HUD hooks must not become callable before their trampolines are published");
            Assert(unitHud.Contains("ButtonCreateTroop") && unitHud.Contains("Enums.GameActionCommand.MakeTroop") &&
                unitHud.Contains("recruitmentGameActionOriginal(command, structureId, state, value2)") &&
                !virtualRuntime.Contains("GameAction(Enums.GameActionCommand.MakeTroop"),
                "recruitment variants must observe Vanilla's one MakeTroop action instead of issuing a second action");
            Assert(unitHud.Contains("Shared.MissionEvents.Ended") && unitHud.Contains("activeRecruitment.Clear()") &&
                unitHud.Contains("APISharedUnitDetailHost"),
                "recruitment map reset or unit-detail host is missing");
            Assert(unitHud.Contains("if (updateSpritesActive)") &&
                unitHud.Contains("IsImageOverrideContextReady()") &&
                !unitHud.Contains("main.UpdateUITroopSprites(lastSpriteColour"),
                "image overrides lack startup, reentrancy, or refresh-loop protection");
            int renderStart = unitHud.IndexOf("private void OnBeforeRender()", StringComparison.Ordinal);
            int hoverStart = renderStart >= 0
                ? unitHud.IndexOf("private void ApplyHover(", renderStart, StringComparison.Ordinal)
                : -1;
            string renderMethod = renderStart >= 0 && hoverStart > renderStart
                ? unitHud.Substring(renderStart, hoverStart - renderStart)
                : string.Empty;
            int loadedGuard = renderMethod.IndexOf("if (!MainViewModel.viewModelLoaded) return;", StringComparison.Ordinal);
            int singletonRead = renderMethod.IndexOf("MainViewModel main = MainViewModel.Instance;", StringComparison.Ordinal);
            int hudGuard = renderMethod.IndexOf("if (main?.HUDmain == null) return;", StringComparison.Ordinal);
            int refreshConsume = renderMethod.IndexOf("refreshRequested = false;", StringComparison.Ordinal);
            Assert(loadedGuard >= 0 && loadedGuard < singletonRead &&
                singletonRead < hudGuard && hudGuard < refreshConsume,
                "render HUD readiness guards must precede the lazy singleton getter and refresh consumption");
            Assert(renderMethod.Contains("TryApplyFrameArea(\"hover presentation\", () => ApplyHover(main));") &&
                renderMethod.Contains("TryApplyFrameArea(\"army-report presentation\", () => ApplyArmyReport(main), HideArmyHosts);") &&
                renderMethod.Contains("TryApplyFrameArea(\"recruitment presentation\", () => ApplyRecruitmentPresentation(main), HideRecruitmentControls);") &&
                renderMethod.Contains("TryApplyFrameArea(\"unit-detail presentation\", () => ApplyUnitDetails(main), HideUnitDetailControls);") &&
                unitHud.Contains("private void ApplyHover(MainViewModel main)") &&
                unitHud.Contains("private void ApplyArmyReport(MainViewModel main)"),
                "independent render HUD areas must reuse the readiness-checked view model and fail closed separately");
            Assert(unitHud.Contains("private void HideArmyHosts()") &&
                unitHud.Contains("private void HideRecruitmentControls()") &&
                unitHud.Contains("private void HideUnitDetailControls()") &&
                unitHud.Contains("if (archerVariantHost != null)") &&
                unitHud.Contains("if (unitDetailHost != null)"),
                "HUD area cleanup must tolerate missing XAML controls");
            Assert(unitHud.Contains("loggedCallbackFailures.Add(area)") && !unitHud.Contains("callbackErrorLogged"),
                "HUD callback failures must be deduplicated independently by area");

            var patchDocument = new XmlDocument();
            patchDocument.LoadXml(buildingPatch);
            var recruitmentRoots = new HashSet<string>(StringComparer.Ordinal);
            int structuralOperations = 0;
            foreach (XmlNode operation in patchDocument.DocumentElement.ChildNodes)
            {
                if (operation.NodeType != XmlNodeType.Element || operation.LocalName != "Operation") continue;
                string operationType = operation.Attributes?["Type"]?.Value;
                if (operationType != "Add" && operationType != "InsertBefore" && operationType != "InsertAfter" && operationType != "Replace") continue;
                structuralOperations++;
                var contentNodes = new List<XmlNode>();
                foreach (XmlNode child in operation.ChildNodes)
                    if (child.NodeType == XmlNodeType.Element && child.LocalName == "Content") contentNodes.Add(child);
                var contentRoots = new List<XmlNode>();
                if (contentNodes.Count == 1)
                    foreach (XmlNode child in contentNodes[0].ChildNodes)
                        if (child.NodeType == XmlNodeType.Element) contentRoots.Add(child);
                Assert(contentNodes.Count == 1 && contentRoots.Count == 1,
                    "every structural XAML patch operation must expose exactly one Content root");
                if (operation.Attributes?["XPath"]?.Value == "//n:Grid[@Name='BarracksPanel']" && contentRoots.Count == 1)
                {
                    // Simulate Script Extender 2.5.0: only the first direct Content element survives.
                    string xamlName = contentRoots[0].Attributes?["x:Name"]?.Value;
                    if (!string.IsNullOrEmpty(xamlName)) recruitmentRoots.Add(xamlName);
                }
            }
            Assert(structuralOperations > 0 && recruitmentRoots.SetEquals(new[]
            {
                "APISharedArcherVariantHost"
            }), "Script Extender merge simulation must retain the single recruitment host");
            Assert(buildingPatch.Contains("APISharedArcherVariantPrevious") &&
                buildingPatch.Contains("APISharedArcherVariantNext") &&
                buildingPatch.Contains("Margin=\"4,0,0,91\"") &&
                buildingPatch.Contains("Margin=\"64,0,0,91\"") &&
                buildingPatch.Contains("Opacity=\"0.58\"") &&
                buildingPatch.Contains("Value=\"0.78\"") &&
                buildingPatch.Contains("Value=\"0.90\""),
                "recruitment arrows lack the confirmed names, positions, or translucent states");
            Assert(buildingPatch == File.ReadAllText(packagedBuildingPatchPath),
                "source and packaged APIShared building XAML patches must match");
            for (int slot = 1; slot <= 8; slot++)
            {
                Assert(troopPatch.Contains("APISharedUnitHudSlotHost" + slot) &&
                    troopPatch.Contains("APISharedUnitHudSlot" + slot) &&
                    troopPatch.Contains("APISharedUnitHudSlotTint" + slot),
                    "troop category slot " + slot + " lacks a colocated button and non-interactive tint overlay");
            }
            Assert(unitHud.Contains("UnitHudImageSlot.UIBuildingsO011") &&
                unitHud.Contains("UnitHudImageSlot.UIBuildingsO012") &&
                unitHud.Contains("UnitHudImageSlot.UIButtonsK007") &&
                unitHud.Contains("UnitHudImageSlot.UIButtonsK008") &&
                unitHud.Contains("UnitHudImageSlot.UIButtonsO016") &&
                unitHud.Contains("UnitHudImageSlot.UIButtonsO017") &&
                unitHud.Contains("UnitHudImageSlot.UIButtonsO018") &&
                Enum.GetValues(typeof(UnitHudImageSlot)).Length == 7,
                "typed image-override allowlist is incomplete");
            Assert(unitHud.Contains("cache.Mask = source == null ? null : new ImageBrush(source)") &&
                unitHud.Contains("ConditionalWeakTable<Border, TintCache>") &&
                unitHud.Contains(": (float)tint.Alpha / byte.MaxValue") &&
                unitHud.Contains("troopPanel?.FindName(\"ArchersSelected\")") &&
                unitHud.Contains("source ?? main?.UIButtonsK023") &&
                unitHud.Contains("main?.HUDBuildingPanel?.RefRecruitArcherButton") &&
                unitHud.Contains("source ?? main?.UIButtonsO001") &&
                unitHud.Contains("PropEx.GetSprite2(vanilla)") &&
                unitHud.Contains("parent.Children.Insert(imageIndex + 1, tint)") &&
                !unitHud.Contains("button.Content = CreateTint") &&
                virtualRuntime.Contains("new UnitHudTint(64, 128, byte.MaxValue, 115)") &&
                !virtualRuntime.Contains("() => MainViewModel.Instance?.UIButtonsK023"),
                "surface-specific Vanilla Archer icons or the 45-percent overlay tint are incorrect");
            Assert(virtualRuntime.Contains("TryRegisterCategory") && bugfixLord.Contains("TryRegisterCategory") &&
                !virtualRuntime.Contains("new Hook"),
                "consumer mods do not exclusively register with the central HUD API");
            Assert(bugfixGatehouse.Contains("TryGetGatehouseDistanceOrigin") &&
                bugfixGatehouse.Contains("GatehouseDistanceOrigin.BuildingBoundsCenter") &&
                bugfixGatehouse.Contains("GatehouseDistanceOrigin.VanillaBuildingBegin"),
                "BugfixesAndQoL must exclusively select the gatehouse distance origin through APIShared");
            Assert(extraGatehouse.Contains("TryGetGatehouseTiming") &&
                extraGatehouse.Contains("new GatehouseTimingSettings") &&
                !extraProject.Contains("GatehouseTimingPatch.cs"),
                "ExtraFeatures must exclusively apply gatehouse timing through APIShared");
            Assert(extraAiv.Contains("TryGetAivBuildStep") && extraAiv.Contains("TryRegisterObserver") &&
                !extraAiv.Contains("ExecuteBuildStepDelegate") && !extraAiv.Contains("executeBuildStepHook"),
                "ExtraFeatures must consume the shared AIV build-step broker without a local fallback detour");
            Assert(activeRuntime.Contains("TryGetAivBuildStep") && activeRuntime.Contains("TryRegisterObserver") &&
                !activeAiv.Contains("ExecuteBuildStepDelegate") && !activeAiv.Contains("executeBuildStepHook") &&
                activeProject.Contains("<Reference Include=\"APIShared\">") && activeProject.Contains("<Private>false</Private>") &&
                apiDependencyMatchesRelease(activePlugin, "ActiveAIVDetector"),
                "ActiveAIVDetector prebuild tracing must use APIShared as a thin hard dependency");
            Assert(bugfixControlGroups.Contains("TryRemoveUnitFromControlGroups") &&
                !bugfixControlGroups.Contains("ControlGroupStorage") &&
                !bugfixNativeDefinition.Contains("ControlGroupStorage") &&
                apiDependencyMatchesRelease(bugfixPlugin, "BugfixesAndQoL"),
                "native control-group storage must only be resolved and mutated inside APIShared");
            int bindStart = castlePlanner.IndexOf("private void BindNativeFunctions(", StringComparison.Ordinal);
            int hookStart = castlePlanner.IndexOf("private void InstallHumanStartPreparationHook(", StringComparison.Ordinal);
            string castleBindings = bindStart >= 0 && hookStart > bindStart
                ? castlePlanner.Substring(bindStart, hookStart - bindStart)
                : string.Empty;
            Assert(castleBindings.Contains("setPlacement = Bind<SetPlacementDelegate>") &&
                castleBindings.Contains("testSpecificCandidate = Bind<TestSpecificCandidateDelegate>") &&
                castleBindings.Contains("prepareLayout = Bind<PrepareLayoutDelegate>") &&
                castleBindings.Contains("executeToPercentage = Bind<ExecuteToPercentageDelegate>") &&
                !castleBindings.Contains("AddDetour") && !castleBindings.Contains("AddContextHook"),
                "CastlePlanner AIV targets 0x54EC0, 0x54DE0, 0x53D00, and 0x55F50 must remain bind-only");
            Assert(Count(lobbyState, "Application.onBeforeRender += OnBeforeRender") == 1 &&
                lobbyState.Contains("ObserveDirtyNow();") &&
                Count(lobbyState, "getActiveLobbyMembersOriginal(self, coopGame)") == 1 &&
                Count(lobbyState, "leaveLobbyOriginal(self, startGame)") == 1 &&
                lobbyState.Contains("private const int FallbackFrames = 15") &&
                lobbyState.Contains("Shared.MissionEvents.Initialization") &&
                lobbyState.Contains("Shared.MissionEvents.Ended"),
                "APIShared must own exactly one managed lobby observer with one-call detours and map-aware fallback polling");
            Assert(sharedPreset.Contains("TryGetLobbyState") &&
                sharedPreset.Contains("API_SHARED_LOBBY_OBSERVER") &&
                !sharedPreset.Contains("Application.onBeforeRender"),
                "the APIShared-owned preset coordinator must consume the lobby capability without a local render poller");
            string[] lobbyObserverProjects = FindRuntimeProjectFiles(workspace)
                .Where(path => File.ReadAllText(path).Contains("API_SHARED_LOBBY_OBSERVER"))
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Assert(lobbyObserverProjects.SequenceEqual(new[] { "APIShared" }, StringComparer.Ordinal),
                "only APIShared may compile the shared lobby bridge");
            Assert(bugfixProject.Contains("<Reference Include=\"APIShared\">") && bugfixProject.Contains("<Private>false</Private>") &&
                castleProject.Contains("<Reference Include=\"APIShared\">") && castleProject.Contains("<Private>false</Private>") &&
                customProject.Contains("<Reference Include=\"APIShared\">") && customProject.Contains("<Private>false</Private>") &&
                extremeProject.Contains("<Reference Include=\"APIShared\">") && extremeProject.Contains("<Private>false</Private>") &&
                apiDependencyMatchesRelease(bugfixPlugin, "BugfixesAndQoL") &&
                apiDependencyMatchesRelease(castlePlugin, "CastlePlanner") &&
                apiDependencyMatchesRelease(customPlugin, "ExtendedData") &&
                apiDependencyMatchesRelease(extremePlugin, "ExtremePowers"),
                "all known active preset consumers must compile against and hard-depend on APIShared");
            Assert(apiDependencyMatchesRelease(activePlugin, "ActiveAIVDetector") &&
                apiDependencyMatchesRelease(bugfixPlugin, "BugfixesAndQoL") &&
                apiDependencyMatchesRelease(castlePlugin, "CastlePlanner") &&
                apiDependencyMatchesRelease(customPlugin, "ExtendedData") &&
                apiDependencyMatchesRelease(extremePlugin, "ExtremePowers") &&
                apiDependencyMatchesRelease(File.ReadAllText(Path.Combine(workspace, "ExtraFeatures", "src", "ExtraFeaturesPlugin.cs")), "ExtraFeatures"),
                "release inventory must declare each consumer's actual APIShared minimum");
            Assert(releaseScript.Contains("Profile = 'Thin'") &&
                !releaseScript.Contains("Profile = 'Bundle'") &&
                !releaseScript.Contains("with-APIShared") &&
                releaseScript.Contains("Get-PublishedApiSharedRelease") &&
                releaseScript.Contains("APIShared.dll") && releaseScript.Contains("SHCDESE.dll") &&
                releaseScript.Contains("RedBird"),
                "release staging must emit only thin artifacts, validate the published APIShared release, and reject private runtime copies");
            Assert(!nexusScript.Contains("[ValidateSet('Thin','Bundle')]") &&
                nexusScript.Contains("Nexus akzeptiert nur Thin-Artefakte") &&
                nexusScript.Contains("Assert-NexusRetiredFileChainsInactive"),
                "Nexus validation must accept only thin artifacts and reject active retired bundle chains");
            Assert(steamScript.Contains("Infrastructure") && steamScript.Contains("releaseConfig.ApiShared.Guid") &&
                steamScript.Contains("APIShared.dll") && releaseConfig.Contains("\"Guid\": \"APIShared_Serp\""),
                "Steam staging must model APIShared as one separately validated infrastructure dependency");
            Assert(hostPlugin.Contains("List<PackModRecord> assetMods = ScriptExtenderCompatibility.SelectRuntimePackRecords(manifest);") &&
                hostPlugin.Contains("foreach (PackModRecord mod in assetMods)") &&
                hostPlugin.Contains("expected={assetMods.Count}"),
                "Steam host must register infrastructure assets before active child assets and include them in diagnostics");
            string randomPathing = randomRuntime + "\n" + randomRegistry + "\n" + randomPlacement;
            Assert(Count(randomPathing, "GetPathComponentGrid()") > 0 &&
                !randomPathing.Contains("TileManager.PathConnectionGrid") &&
                Count(randomPathing, "pathConnections[") == Count(randomPathing, "pathConnections.Length"),
                "RandomEvents must use the public path-component grid and guard every indexed access by span length");
            Match randomMinimumMatch = Regex.Match(randomManifest,
                @"""MinimumScriptExtenderVersion""\s*:\s*""([^""]*)""");
            string randomMinimum = randomMinimumMatch.Success ? randomMinimumMatch.Groups[1].Value : string.Empty;
            Assert(string.IsNullOrEmpty(randomMinimum) ||
                randomPlugin.Contains($"[BepInDependency(ScriptExtenderGuid, \"{randomMinimum}\")]"),
                "RandomEvents source dependency must match its manifest minimum");
            MatchCollection orderedRouteCalls = Regex.Matches(
                hunterRoutes,
                @"FindNextComponentTowardDestination\s*\(\s*(?<root>inputs|context)\.PlayerId\s*,\s*\k<root>\.(?:SourcePcl)\s*,\s*\k<root>\.(?:TargetPcl)\s*,");
            int routeCallCount = Count(hunterRoutes, "FindNextComponentTowardDestination(");
            Assert(routeCallCount > 0 && orderedRouteCalls.Count == routeCallCount,
                "route queries must retain player, current component, destination component, mode argument order");
            Assert(modVersion.Length > 0 && sourceManifest.Contains("\"NetworkMode\": 1"),
                "source manifest declares a version and gameplay mode");
            Assert(packageManifest.Contains($"\"Version\": \"{modVersion}\"") && packageManifest.Contains("\"NetworkMode\": 1"),
                "package manifest matches the source version and gameplay mode");
        }

        private static string FindWorkspaceRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "APIShared")) &&
                    File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                {
                    return directory.FullName;
                }
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("Workspace root was not found.");
        }

        private static IEnumerable<string> FindRuntimeProjectFiles(string workspace)
        {
            foreach (string directory in Directory.GetDirectories(workspace))
            {
                foreach (string project in Directory.GetFiles(
                    directory,
                    "*.csproj",
                    SearchOption.TopDirectoryOnly))
                {
                    yield return project;
                }

                string name = Path.GetFileName(directory);
                if (!string.Equals(name, "Testmods", StringComparison.Ordinal) &&
                    !string.Equals(name, "Helpers", StringComparison.Ordinal))
                {
                    continue;
                }
                foreach (string child in Directory.GetDirectories(directory))
                    foreach (string project in Directory.GetFiles(
                        child,
                        "*.csproj",
                        SearchOption.TopDirectoryOnly))
                    {
                        yield return project;
                    }
            }
        }

        private static int Count(string text, string value)
        {
            int count = 0;
            int offset = 0;
            while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += value.Length;
            }
            return count;
        }

        private static void TestPublicSurface()
        {
            var imageSlots = (UnitHudImageSlot[])Enum.GetValues(typeof(UnitHudImageSlot));
            Assert(imageSlots.Length == 7 &&
                imageSlots[0] == UnitHudImageSlot.UIBuildingsO011 &&
                imageSlots[1] == UnitHudImageSlot.UIBuildingsO012 &&
                imageSlots[2] == UnitHudImageSlot.UIButtonsK007 &&
                imageSlots[3] == UnitHudImageSlot.UIButtonsK008 &&
                imageSlots[4] == UnitHudImageSlot.UIButtonsO016 &&
                imageSlots[5] == UnitHudImageSlot.UIButtonsO017 &&
                imageSlots[6] == UnitHudImageSlot.UIButtonsO018,
                "unit-HUD image slots must retain the four-slot prefix and deterministic seven-slot order");
            var expected = new HashSet<string>(StringComparer.Ordinal)
            {
                "APIShared.MissionStartKind",
                "APIShared.MissionMapType",
                "APIShared.MissionLifecycleKind",
                "APIShared.MissionInitializationPhase",
                "APIShared.MissionEndReason",
                "APIShared.MissionContext",
                "APIShared.MissionLifecycleNotification",
                "APIShared.IMissionLifecycleCapability",
                "Shared.GameModeKind",
                "Shared.GameModeLaunchVariant",
                "Shared.GameTrailType",
                "Shared.GameModeSnapshot",
                "Shared.GameModeHelper",
                "Shared.GameplayModAllowedContext",
                "Shared.GameplayModActivationProfile",
                "Shared.GameplayModModePolicy",
                "Shared.GameplayFeatureId",
                "Shared.GameplayFeatureActivationProfile",
                "Shared.GameplayFeatureModePolicy",
                "Shared.ModSettingsSearchMatcher",
                "Shared.ModSettingsSearch",
                "Shared.ModSettingsSearchVisibilityConverter",
                "Shared.ModSettingsSearchEntry",
                "Shared.PerPlayerLobbySettingsBuilder",
                "Shared.PerPlayerLobbySnapshot",
                "Shared.PresetLocalAttribute",
                "Shared.PresetLobbyModSettingsViewModel",
                "Shared.LobbyModSettingsPresetRegistration",
                "Shared.IModSettingsPresetEndpoint",
                "Shared.IModSettingsMissionSourceEndpoint",
                "Shared.IModSettingsWorkingCopyEndpoint",
                "Shared.ModSettingsWorkingSourceKind",
                "Shared.ModSettingsWorkingSource",
                "Shared.IModSettingsWorkingSourceProvider",
                "Shared.ModSettingsWorkingSourceRegistry",
                "Shared.PublishedPresetValueMode",
                "Shared.PresetSaveBulkMode",
                "Shared.PresetSettingScope",
                "Shared.PresetSettingDescriptor",
                "Shared.PresetSaveSelection",
                "Shared.PresetSaveSettingViewModel",
                "Shared.ModSettingsPresetSourceKind",
                "Shared.ModSettingsPresetListEntry",
                "Shared.ModSettingsPresetSaveTarget",
                "Shared.PublishedPresetSetting",
                "Shared.PublishedModSettingsPreset",
                "Shared.ModSettingsPresetJson",
                "APIShared.GatehouseDistanceOrigin",
                "APIShared.GatehouseTimingSettings",
                "APIShared.GatehouseTimingValues",
                "APIShared.IGatehouseDistanceOriginCapability",
                "APIShared.IGatehouseTimingCapability",
                "APIShared.IUnitHudPresentationCapability",
                "APIShared.IUnitHudActivationCapability",
                "APIShared.IAivBuildStepCapability",
                "APIShared.ILobbyStateCapability",
                "APIShared.LobbyStateSnapshot",
                "APIShared.IPlayerDefeatCapability",
                "APIShared.IBriefingGoldPresentationCapability",
                "APIShared.BriefingGoldAdjustmentStage",
                "APIShared.BriefingGoldContext",
                "APIShared.BriefingGoldAdjuster",
                "APIShared.PlayerLordDeathNotification",
                "APIShared.PlayerDefeatNotification",
                "APIShared.IAivBuildStepObserver",
                "APIShared.IAivBuildStepInvocation",
                "APIShared.AivBuildStepContext",
                "APIShared.AivBuildStepCompletion",
                "APIShared.IApiShared",
                "APIShared.NativeApiState",
                "APIShared.LobbyPreparationOverride",
                "APIShared.ElevatedMoatAiState",
                "APIShared.ElevatedMoatAiCapability",
                "APIShared.NativeCapabilityDiagnostic",
                "APIShared.NativeCapabilityIds",
                "APIShared.NativeCapabilityState",
                "APIShared.UnitHudSurface",
                "APIShared.UnitHudMouseButton",
                "APIShared.UnitHudImageSlot",
                "APIShared.UnitHudTint",
                "APIShared.UnitHudUnitSnapshot",
                "APIShared.UnitHudCategoryMatcher",
                "APIShared.UnitHudCategoryImageResolver",
                "APIShared.UnitHudTextKind",
                "APIShared.UnitHudTextResolver",
                "APIShared.UnitHudTextProfile",
                "APIShared.UnitHudCategoryDefinition",
                "APIShared.UnitHudRecruitmentTicket",
                "APIShared.UnitHudRecruitmentHandler",
                "APIShared.UnitHudCategorySnapshot",
                "APIShared.UnitHudSlotSnapshot",
                "APIShared.UnitHudControlGroupSnapshot",
                "APIShared.UnitHudInteractionContext",
                "APIShared.UnitHudInteractionHandler",
                "APIShared.UnitHudImageOverrideContext",
                "APIShared.UnitHudImageOverrideResolver",
                "APIShared.UnitHudImageOverrideDefinition",
                "APIShared.ApiShared",
                // BepInEx discovers the plugin type; it is public but is not a consumer service.
                "APIShared.APISharedPlugin"
            };

            Type[] exported = typeof(IApiShared).Assembly.GetExportedTypes();
            foreach (Type type in exported)
            {
                Assert(expected.Remove(type.FullName), $"unexpected exported API type: {type.FullName}");
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    AssertSafePublicType(method.ReturnType, $"{type.FullName}.{method.Name} return type");
                    foreach (ParameterInfo parameter in method.GetParameters())
                        AssertSafePublicType(parameter.ParameterType, $"{type.FullName}.{method.Name} parameter {parameter.Name}");
                }
                if (!typeof(Delegate).IsAssignableFrom(type))
                    foreach (ConstructorInfo constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                        foreach (ParameterInfo parameter in constructor.GetParameters())
                            AssertSafePublicType(parameter.ParameterType, $"{type.FullName} constructor parameter {parameter.Name}");
                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    AssertSafePublicType(property.PropertyType, $"{type.FullName}.{property.Name} property type");
                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    AssertSafePublicType(field.FieldType, $"{type.FullName}.{field.Name} field type");
            }

            foreach (string missing in expected)
                Assert(false, $"expected exported API type is missing: {missing}");

            var expectedAcquisitionMethods = new HashSet<string>(StringComparer.Ordinal)
            {
                "TryGetGatehouseDistanceOrigin",
                "TryGetGatehouseTiming",
                "TryGetUnitHudPresentation",
                "TryGetAivBuildStep",
                "TryGetLobbyState",
                "TryGetMissionLifecycle",
                "TryGetPlayerDefeat",
                "TryGetBriefingGoldPresentation"
            };
            foreach (MethodInfo method in typeof(IApiShared).GetMethods())
                expectedAcquisitionMethods.Remove(method.Name);
            foreach (string missing in expectedAcquisitionMethods)
                Assert(false, $"expected capability acquisition method is missing: IApiShared.{missing}");

            Assert(NativeCapabilityIds.GatehouseDistanceOrigin == "gatehouse-distance-origin",
                "distance-origin capability ID must remain stable");
            Assert(NativeCapabilityIds.GatehouseTiming == "gatehouse-timing",
                "gatehouse-timing capability ID must remain stable");
            Assert(NativeCapabilityIds.UnitHudPresentation == "unit-hud-presentation",
                "unit-HUD capability ID must remain stable");
            Assert(NativeCapabilityIds.AivBuildStep == "aiv-build-step",
                "AIV build-step capability ID must remain stable");
            Assert(NativeCapabilityIds.LobbyState == "lobby-state",
                "lobby-state capability ID must remain stable");
            Assert(NativeCapabilityIds.PlayerDefeat == "player-defeat",
                "player-defeat capability ID must remain stable");
            Assert(NativeCapabilityIds.BriefingGoldPresentation == "briefing-gold-presentation",
                "briefing-gold capability ID must remain stable");
        }

        private static void TestBriefingGoldPresentation()
        {
            MethodInfo briefing = typeof(CrusaderDE.MainViewModel).GetMethod(
                "ButtonGotoBriefing",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(object) },
                null);
            Assert(briefing != null && briefing.ReturnType == typeof(void),
                "installed managed baseline retains ButtonGotoBriefing(object)");

            var service = new BriefingGoldPresentationService(null);
            var calls = new List<string>();
            IBriefingGoldPresentationCapability zOwner = service.Bind("z.owner");
            IBriefingGoldPresentationCapability aOwner = service.Bind("a.owner");

            Assert(zOwner.TryRegisterAdjustment(
                "mod",
                BriefingGoldAdjustmentStage.ModAdjustment,
                context => { calls.Add("mod"); return context.CurrentGold + 100; },
                out _), "briefing-gold mod adjustment registers");
            Assert(aOwner.TryRegisterAdjustment(
                "vanilla-b",
                BriefingGoldAdjustmentStage.VanillaCorrection,
                context => { calls.Add("vanilla-b"); return context.EffectiveVanillaGold; },
                out _), "briefing-gold Vanilla correction registers");
            Assert(aOwner.TryRegisterAdjustment(
                "vanilla-a",
                BriefingGoldAdjustmentStage.VanillaCorrection,
                context => { calls.Add("vanilla-a"); return context.CurrentGold + 1; },
                out _), "briefing-gold same-owner correction registers");
            Assert(aOwner.TryRegisterAdjustment(
                "failure",
                BriefingGoldAdjustmentStage.ModAdjustment,
                context => throw new InvalidOperationException("expected"),
                out _), "briefing-gold throwing adjustment registers");
            Assert(aOwner.TryRegisterAdjustment(
                "negative",
                BriefingGoldAdjustmentStage.ModAdjustment,
                context => -1,
                out _), "briefing-gold invalid-result adjustment registers");
            Assert(!aOwner.TryRegisterAdjustment(
                "negative",
                BriefingGoldAdjustmentStage.ModAdjustment,
                context => 0,
                out NativeCapabilityDiagnostic duplicate) &&
                duplicate.State == NativeCapabilityState.Conflict,
                "briefing-gold duplicate owner-local IDs fail closed");

            int human = service.EvaluateForTests(2, true, 2000, true, true);
            Assert(human == 100 && calls.SequenceEqual(new[]
            {
                "vanilla-a", "vanilla-b", "mod"
            }), "briefing-gold adjustments use stage, owner and ID order with failure isolation");

            calls.Clear();
            int ai = service.EvaluateForTests(3, false, 2000, true, true);
            Assert(ai == 2100,
                "No Starting Gold affects the effective human base but leaves AI gold unchanged");
            calls.Clear();
            int unresolved = service.EvaluateForTests(0, true, 4000, false, true);
            Assert(unresolved == 4100,
                "unresolved No Starting Gold state retains Vanilla's displayed base");

            var context = new BriefingGoldContext(4, true, 8000, 0, 0, true, true);
            Assert(context.SlotIndex == 4 && context.IsHuman &&
                context.VanillaDisplayedGold == 8000 && context.EffectiveVanillaGold == 0 &&
                context.CurrentGold == 0 && context.HasNoStartingGoldState &&
                context.NoStartingGoldEnabled,
                "briefing-gold context exposes immutable audited inputs");

            string projectDirectory = Path.Combine(FindWorkspaceRoot(), "APIShared");
            string source = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "BriefingGoldPresentationCapability.cs"));
            int originalCall = source.IndexOf(
                "briefingOriginal(self, parameter);",
                StringComparison.Ordinal);
            int visibleSlotPass = source.IndexOf(
                "ApplyToVisibleSlots(self);",
                StringComparison.Ordinal);
            Assert(originalCall >= 0 && visibleSlotPass > originalCall &&
                Count(source, "briefingOriginal(self, parameter);") == 1,
                "briefing hook invokes Vanilla exactly once before presentation adjustments");
            Assert(source.Contains("self.SkirmishBriefingAlly[slotIndex]") &&
                source.Contains("self.AlliesHumanFaceVis[slotIndex]") &&
                source.Contains("AdvOpt_NoGold"),
                "briefing hook filters visible slots and resolves human/AI plus No Starting Gold state");
        }

        private static void TestAivBuildStepBroker()
        {
            var service = new AivBuildStepService(ApiSharedRuntime.SupportedHash, null);
            var calls = new List<string>();
            IAivBuildStepCapability zOwner = service.Bind("z.owner");
            IAivBuildStepCapability aOwner = service.Bind("a.owner");
            var zObserver = new RecordingAivObserver("z", calls);
            var aSecondObserver = new RecordingAivObserver("a2", calls);
            var aFirstObserver = new RecordingAivObserver("a1", calls);

            Assert(zOwner.TryRegisterObserver("one", zObserver, out _), "AIV observer registration should succeed");
            Assert(aOwner.TryRegisterObserver("two", aSecondObserver, out _), "AIV observer registration should succeed");
            Assert(aOwner.TryRegisterObserver("one", aFirstObserver, out _), "AIV observer registration should succeed");
            Assert(aOwner.TryRegisterObserver("one", aFirstObserver, out _), "identical AIV registration should be idempotent");
            Assert(!aOwner.TryRegisterObserver("one", new RecordingAivObserver("conflict", calls), out NativeCapabilityDiagnostic conflict) &&
                conflict.State == NativeCapabilityState.Conflict,
                "a different observer under the same AIV registration identity must conflict");

            int originals = 0;
            var context = new AivBuildStepContext(0x1234, 4, 9, 2, 1);
            int result = service.DispatchForTest(context, () => { calls.Add("vanilla"); originals++; return 77; });
            Assert(result == 77 && originals == 1,
                "AIV broker must return the unchanged result from exactly one Vanilla call");
            AssertSequenceEqual(calls.ToArray(), new[]
            {
                "begin:a1", "begin:a2", "begin:z", "vanilla",
                "complete:z:True:77", "complete:a2:True:77", "complete:a1:True:77"
            }, "AIV observers must begin deterministically and unwind in reverse order");

            calls.Clear();
            var isolated = new AivBuildStepService(ApiSharedRuntime.SupportedHash, null);
            isolated.Bind("a").TryRegisterObserver("begin-failure", new RecordingAivObserver("bad-begin", calls, true, false), out _);
            isolated.Bind("b").TryRegisterObserver("complete-failure", new RecordingAivObserver("bad-complete", calls, false, true), out _);
            isolated.Bind("c").TryRegisterObserver("healthy", new RecordingAivObserver("healthy", calls), out _);
            originals = 0;
            result = isolated.DispatchForTest(context, () => { calls.Add("vanilla"); originals++; return 12; });
            Assert(result == 12 && originals == 1 && calls.Contains("complete:healthy:True:12"),
                "observer exceptions must not suppress Vanilla or healthy AIV observers");

            calls.Clear();
            var reentrant = new AivBuildStepService(ApiSharedRuntime.SupportedHash, null);
            reentrant.Bind("owner").TryRegisterObserver("observer", new RecordingAivObserver("observer", calls), out _);
            originals = 0;
            int outer = reentrant.DispatchForTest(context, () =>
            {
                originals++;
                int inner = reentrant.DispatchForTest(context, () => { originals++; return 5; });
                calls.Add("inner-result:" + inner);
                return 6;
            });
            Assert(outer == 6 && originals == 2 && Count(string.Join("|", calls), "begin:observer") == 2,
                "nested native calls must be dispatched reentrantly with one Original call per invocation");

            calls.Clear();
            var exceptional = new AivBuildStepService(ApiSharedRuntime.SupportedHash, null);
            exceptional.Bind("owner").TryRegisterObserver("observer", new RecordingAivObserver("observer", calls), out _);
            AssertThrows<InvalidOperationException>(
                () => exceptional.DispatchForTest(context, () => throw new InvalidOperationException("injected Vanilla failure")),
                "Vanilla exceptions must propagate through the AIV broker");
            Assert(calls.Contains("complete:observer:False:0"),
                "AIV completion must identify an Original call that did not complete");

            Assert(!AivBuildStepService.TryCreate(
                    "UNKNOWN", 0, ReadOnlySpan<byte>.Empty, null, null,
                    out _, out NativeCapabilityDiagnostic unknown) &&
                unknown.State == NativeCapabilityState.UnsupportedBuild,
                "unknown builds must fail closed before resolving the AIV target");
            Assert(!AivBuildStepService.TryCreate(
                    ApiSharedRuntime.SupportedHash, 0, ReadOnlySpan<byte>.Empty, null, null,
                    out _, out NativeCapabilityDiagnostic resolverFailure) &&
                resolverFailure.State == NativeCapabilityState.ValidationFailed,
                "missing native AIV resolver inputs must fail closed");

            string source = File.ReadAllText(Path.Combine(FindWorkspaceRoot(), "APIShared", "src", "AivBuildStepCapability.cs"));
            Assert(source.Contains("pending?.Dispose();") && source.Contains("candidate.transaction = pending;") &&
                source.Contains("OwnsHooks = false"),
                "AIV hook setup must roll back only unpublished candidates and retain the published transaction");
        }

        private static void AssertSafePublicType(Type type, string location)
        {
            while (type.IsByRef || type.IsArray)
                type = type.GetElementType();
            if (type.IsGenericType)
            {
                foreach (Type argument in type.GetGenericArguments())
                    AssertSafePublicType(argument, location + " generic argument");
            }
            string typeName = type.Name;
            bool forbidden = type.IsPointer || type == typeof(IntPtr) || type == typeof(UIntPtr) ||
                typeName.IndexOf("NativeDetour", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("MemoryWriter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("Pattern", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.StartsWith("Rva", StringComparison.OrdinalIgnoreCase) ||
                typeName.EndsWith("Rva", StringComparison.OrdinalIgnoreCase);
            Assert(!forbidden, $"forbidden native implementation type at {location}: {type.FullName}");
        }

        private static void TestPeValidation()
        {
            byte[] image = CreatePeImage(0x4000, true);
            NativePeImage pe = NativePeImage.Parse(image);
            Assert(pe.ImageSize == image.Length, "PE image size should parse");
            AssertThrowsState(NativeCapabilityState.ValidationFailed,
                () => pe.RequireExecutableRange(0x200, 1, "header"), "headers are not executable targets");

            byte[] nonExecutable = CreatePeImage(0x4000, false);
            GatehouseBuildTarget catalog = InstallTestGatehouse(nonExecutable);
            var runtime = InitializeRuntime(nonExecutable, catalog, SeedRuntimeMemory(nonExecutable, catalog));
            Assert(!runtime.TryGetGatehouseTiming("owner", out _, out NativeCapabilityDiagnostic diagnostic) &&
                diagnostic.State == NativeCapabilityState.ValidationFailed, "gatehouse function must be executable");
            Assert(!runtime.TryGetGatehouseDistanceOrigin("owner", out _, out diagnostic) &&
                diagnostic.State == NativeCapabilityState.ValidationFailed, "distance origin also requires an executable gatehouse function");
        }

        private static void TestFixedCatalogValidation()
        {
            byte[] image = CreatePeImage(0x4000, true);
            GatehouseBuildTarget catalog = InstallTestGatehouse(image);
            FakeMemory memory = SeedRuntimeMemory(image, catalog);
            ApiSharedRuntime runtime = InitializeRuntime(image, catalog, memory);
            Assert(runtime.TryGetGatehouseTiming("owner", out _, out NativeCapabilityDiagnostic available) &&
                available.State == NativeCapabilityState.Available && available.Reason.Contains("function SHA-256"),
                "matching fixed catalog should validate with provenance");
            Assert(runtime.TryGetGatehouseDistanceOrigin("origin-owner", out _, out NativeCapabilityDiagnostic originAvailable) &&
                originAvailable.State == NativeCapabilityState.Available && originAvailable.Reason.Contains("function SHA-256"),
                "matching distance-origin catalog should validate with provenance");
            Assert(memory.WriteCount == 0,
                "initialization and capability acquisition must not activate either gatehouse gameplay change");

            FakeMemory preHookedMemory = SeedRuntimeMemory(image, catalog);
            preHookedMemory.SetByte(ModuleBase + catalog.HumanReopenDelayRva + 4, 0xFF);
            runtime = InitializeRuntime(image, catalog, preHookedMemory);
            AssertTimingValidationFailure(runtime,
                "an adjacent hook present before APIShared initialization must fail the one-time live layout validation");
            Assert(runtime.TryGetGatehouseDistanceOrigin("owner", out _, out _),
                "a timing-only live layout mismatch must not disable distance origin");

            byte[] wrongHashImage = (byte[])image.Clone();
            var wrongHashCatalog = CloneCatalog(catalog, functionHash: new string('0', 64));
            runtime = InitializeRuntime(wrongHashImage, wrongHashCatalog, SeedRuntimeMemory(wrongHashImage, wrongHashCatalog));
            AssertBothGateValidationFailures(runtime, "wrong function hash must fail both gatehouse capabilities");

            byte[] wrongOpcode = (byte[])image.Clone();
            wrongOpcode[DecisionRva] ^= 1;
            Copy(wrongOpcode, 0x1500, DecisionBytes); // A decoy must never be used as a fallback.
            GatehouseBuildTarget wrongOpcodeCatalog = CloneCatalog(catalog, functionHash: ApiSharedRuntime.ComputeSha256(
                new ReadOnlySpan<byte>(wrongOpcode, FunctionRva, FunctionSize)));
            runtime = InitializeRuntime(wrongOpcode, wrongOpcodeCatalog, SeedRuntimeMemory(wrongOpcode, wrongOpcodeCatalog));
            AssertTimingValidationFailure(runtime, "wrong timing opcode must fail without accepting a decoy");
            Assert(runtime.TryGetGatehouseDistanceOrigin("owner", out _, out _),
                "a timing-only opcode mismatch must not disable distance-origin capability");

            byte[] wrongImmediate = (byte[])image.Clone();
            WriteInt32(wrongImmediate, DecisionRva + 8, 201);
            GatehouseBuildTarget wrongImmediateCatalog = CloneCatalog(catalog, functionHash: ApiSharedRuntime.ComputeSha256(
                new ReadOnlySpan<byte>(wrongImmediate, FunctionRva, FunctionSize)));
            runtime = InitializeRuntime(wrongImmediate, wrongImmediateCatalog, SeedRuntimeMemory(wrongImmediate, wrongImmediateCatalog));
            AssertTimingValidationFailure(runtime, "wrong Vanilla immediate must fail timing");
            Assert(runtime.TryGetGatehouseDistanceOrigin("owner", out _, out _),
                "a timing immediate mismatch must not disable distance-origin capability");

            byte[] wrongDistance = (byte[])image.Clone();
            wrongDistance[DistanceRva] ^= 1;
            GatehouseBuildTarget wrongDistanceCatalog = CloneCatalog(catalog, functionHash: ApiSharedRuntime.ComputeSha256(
                new ReadOnlySpan<byte>(wrongDistance, FunctionRva, FunctionSize)));
            runtime = InitializeRuntime(wrongDistance, wrongDistanceCatalog, SeedRuntimeMemory(wrongDistance, wrongDistanceCatalog));
            AssertOriginValidationFailure(runtime, "wrong Vanilla distance block must fail distance origin");
            Assert(runtime.TryGetGatehouseTiming("owner", out _, out _),
                "a distance-origin mismatch must not disable gatehouse timing");

            GatehouseBuildTarget outside = CloneCatalog(catalog, aiCloseDistanceRva: FunctionRva - 4);
            runtime = InitializeRuntime(image, outside, SeedRuntimeMemory(image, catalog));
            AssertTimingValidationFailure(runtime, "catalogued immediate outside function must fail timing");
            Assert(runtime.TryGetGatehouseDistanceOrigin("owner", out _, out _),
                "an invalid timing address must not disable distance origin");
        }

        private static void TestReadinessAndIndependentCapabilities()
        {
            byte[] image = CreatePeImage(0x4000, true);
            var runtime = new ApiSharedRuntime();
            Assert(!runtime.TryGetGatehouseDistanceOrigin("owner", out _, out NativeCapabilityDiagnostic originPending) &&
                originPending.State == NativeCapabilityState.Pending, "pre-initialization origin query should be Pending");
            Assert(!runtime.TryGetGatehouseTiming("owner", out _, out NativeCapabilityDiagnostic pending) &&
                pending.State == NativeCapabilityState.Pending, "pre-initialization query should be Pending");
            int readyBefore = 0;
            runtime.WhenReady(_ => readyBefore++);
            var memory = new FakeMemory();
            runtime.Initialize(ModuleBase, image, "UNKNOWN", memory, null, null, false);
            Assert(runtime.State == NativeApiState.Ready && readyBefore == 1, "unknown build should still publish Ready");
            Assert(!runtime.TryGetGatehouseTiming("owner", out _, out NativeCapabilityDiagnostic gate) &&
                gate.State == NativeCapabilityState.UnsupportedBuild, "unknown build should disable only gatehouse");
            Assert(!runtime.TryGetGatehouseDistanceOrigin("owner", out _, out NativeCapabilityDiagnostic origin) &&
                origin.State == NativeCapabilityState.UnsupportedBuild, "unknown build should disable distance origin without mutation");
            Assert(memory.OperationCount == 0, "unknown build performs no native operation");
            int readyAfter = 0;
            runtime.WhenReady(_ => readyAfter++);
            Assert(readyAfter == 1, "post-initialization readiness callback should be synchronous");

            runtime = new ApiSharedRuntime();
            runtime.Initialize(0, ReadOnlySpan<byte>.Empty, string.Empty, new FakeMemory(), null, null, false);
            Assert(runtime.State == NativeApiState.Ready, "missing native module is a gate capability error, not a global failure");
            Assert(!runtime.TryGetGatehouseTiming("owner", out _, out NativeCapabilityDiagnostic missingHash) &&
                missingHash.State == NativeCapabilityState.UnsupportedBuild, "missing hash is unsupported for gatehouse");
            Assert(!runtime.TryGetGatehouseDistanceOrigin("owner", out _, out NativeCapabilityDiagnostic missingOriginHash) &&
                missingOriginHash.State == NativeCapabilityState.UnsupportedBuild, "missing hash is unsupported for distance origin");
        }

        private static void TestOwnership()
        {
            var registry = new NativeOwnershipRegistry();
            var first = new[] { new NativeInterval(100, 110) };
            Assert(registry.TryReserve("A", "cap", NativeReservationMode.Exclusive, first, out _), "first reservation");
            Assert(registry.TryReserve("A", "cap", NativeReservationMode.Exclusive, first, out _), "same reservation is idempotent");
            Assert(registry.TryReserve("B", "other", NativeReservationMode.Exclusive,
                new[] { new NativeInterval(110, 120) }, out _), "adjacent half-open intervals do not overlap");
            Assert(!registry.TryReserve("C", "third", NativeReservationMode.Exclusive,
                new[] { new NativeInterval(109, 111) }, out string conflict) && conflict == "A",
                "exclusive overlap identifies the first owner");
        }

        private static void TestCenteredDistanceSemantics()
        {
            GatehouseBuildTarget supported = GatehouseBuildTarget.Supported;
            Assert(supported.DistanceBlockRva == 0xB7B70 &&
                supported.DistanceBlockRva + supported.VanillaDistanceBlockBytes.Length == 0xB7BBB,
                "distance patch must occupy exactly the post-hook Vanilla arithmetic block");
            Assert(supported.VanillaDistanceBlockBytes.Length == 75 &&
                supported.CenteredDistanceBlockBytes.Length == supported.VanillaDistanceBlockBytes.Length,
                "centered distance patch must preserve the 75-byte block size");
            AssertSequenceEqual(supported.CenteredDistanceBlockBytes, SupportedCenteredDistanceBytes,
                "supported centered distance bytes must match the reviewed crash-safe sequence");

            var originTarget = new GatehouseDistanceOriginTarget(
                ModuleBase + supported.DistanceBlockRva,
                supported.VanillaDistanceBlockBytes,
                supported.CenteredDistanceBlockBytes);
            var timingTarget = new GatehouseTimingTarget(
                ModuleBase + supported.AiCloseDistanceRva,
                ModuleBase + supported.AiReopenDelayRva,
                ModuleBase + supported.HumanCloseDistanceRva,
                ModuleBase + supported.HumanReopenDelayRva);
            Assert(originTarget.Intervals.Count == 1 &&
                originTarget.Intervals[0].Start == ModuleBase + 0xB7B70 &&
                originTarget.Intervals[0].End == ModuleBase + 0xB7BBB,
                "distance-origin capability must own exactly [0xB7B70, 0xB7BBB)");
            Assert(timingTarget.Intervals.Count == 4 &&
                timingTarget.Intervals[0].Start == ModuleBase + 0xB7BC3 && timingTarget.Intervals[0].End == ModuleBase + 0xB7BC7 &&
                timingTarget.Intervals[1].Start == ModuleBase + 0xB7BCA && timingTarget.Intervals[1].End == ModuleBase + 0xB7BCE &&
                timingTarget.Intervals[2].Start == ModuleBase + 0xB7BD3 && timingTarget.Intervals[2].End == ModuleBase + 0xB7BD7 &&
                timingTarget.Intervals[3].Start == ModuleBase + 0xB7C35 && timingTarget.Intervals[3].End == ModuleBase + 0xB7C39,
                "timing capability must own exactly the four immediate intervals");
            foreach (NativeInterval timingInterval in timingTarget.Intervals)
                Assert(!originTarget.Intervals[0].Overlaps(timingInterval),
                    "distance-origin and timing intervals must be disjoint");

            byte[] unitYLoad = Hex("0F BF 8C 2A 10 8B 7E 06");
            int unitYLoadOffset = IndexOfSequence(supported.CenteredDistanceBlockBytes, unitYLoad);
            int firstCdqOffset = Array.IndexOf(supported.CenteredDistanceBlockBytes, (byte)0x99);
            Assert(unitYLoadOffset == 9 && unitYLoadOffset + unitYLoad.Length <= firstCdqOffset,
                "unit Y must be loaded through the live RDX unit offset before CDQ overwrites RDX");

            Assert(GatehouseDistanceOriginService.ComputeCenteredDistanceNative(10, 20, 12, 24, 88, 176) == 0,
                "integer midpoint should map to zero native distance");
            Assert(GatehouseDistanceOriginService.ComputeCenteredDistanceNative(10, 20, 11, 23, 84, 172) == 0,
                "half-tile midpoint should remain exact in native coordinates");
            Assert(GatehouseDistanceOriginService.ComputeCenteredDistanceNative(12, 24, 10, 20, 88, 176) == 0,
                "reversed bounds should produce the same midpoint");
            Assert(GatehouseDistanceOriginService.ComputeCenteredDistanceNative(10, 20, 12, 24, 80, 176) == 8 &&
                GatehouseDistanceOriginService.ComputeCenteredDistanceNative(10, 20, 12, 24, 96, 176) == 8,
                "opposite horizontal approaches should have equal distance");
            Assert(GatehouseDistanceOriginService.ComputeCenteredDistanceNative(10, 20, 12, 24, 80, 168) == 8 &&
                GatehouseDistanceOriginService.ComputeCenteredDistanceNative(10, 20, 12, 24, 96, 184) == 8,
                "diagonal approaches should retain Vanilla Chebyshev distance");
        }

        private static void TestGatehouseDistanceOriginTransaction()
        {
            var ownership = new NativeOwnershipRegistry();
            var mutationSync = new object();
            GatehousePermanentRuntimeState state = GatehousePermanentRuntimeState.CreateTestState();
            IGatehouseDistanceOriginCapability origin =
                CreateOriginService(state, ownership, mutationSync).Bind("BugfixesAndQoL_Serp");
            IGatehouseTimingCapability timing =
                CreateGateService(state, ownership, mutationSync).Bind("ExtraFeatures_Serp");

            Assert(!origin.TryApply((GatehouseDistanceOrigin)99, out NativeCapabilityDiagnostic invalid) &&
                invalid.State == NativeCapabilityState.ValidationFailed,
                "unknown distance-origin values fail before logical publication");
            Assert(origin.TryApply(GatehouseDistanceOrigin.BuildingBoundsCenter, out NativeCapabilityDiagnostic centered) &&
                centered.CapabilityId == NativeCapabilityIds.GatehouseDistanceOrigin &&
                state.Origin == GatehouseDistanceOrigin.BuildingBoundsCenter,
                "Bugfixes owner should apply the centered distance origin");
            state.ReadTiming(out int aiDistance, out int aiDelay, out int humanDistance, out int humanDelay);
            Assert(aiDistance == 200 && aiDelay == 1200 && humanDistance == 140 && humanDelay == 100,
                "distance-origin publication leaves all timing values Vanilla");
            Assert(origin.TryApply(GatehouseDistanceOrigin.BuildingBoundsCenter, out _) &&
                state.Origin == GatehouseDistanceOrigin.BuildingBoundsCenter,
                "identical distance-origin apply is idempotent");

            Assert(timing.TryApply(new GatehouseTimingSettings(true, 1, 5, 10, 15), out NativeCapabilityDiagnostic timingApplied) &&
                timingApplied.CapabilityId == NativeCapabilityIds.GatehouseTiming,
                "different owners can reserve the adjacent timing and origin intervals");
            Assert(state.Origin == GatehouseDistanceOrigin.BuildingBoundsCenter,
                "timing publication leaves the independently selected origin unchanged");

            Assert(origin.TryApply(GatehouseDistanceOrigin.VanillaBuildingBegin, out _),
                "distance-origin capability restores Vanilla on explicit request");
            Assert(state.Origin == GatehouseDistanceOrigin.VanillaBuildingBegin,
                "Vanilla distance-origin request changes only the logical gate");
            state.ReadTiming(out aiDistance, out aiDelay, out humanDistance, out humanDelay);
            Assert(aiDistance == 120 && aiDelay == 200 && humanDistance == 80 && humanDelay == 40,
                "restoring the origin does not change customized timing values");

            var conflictRegistry = new NativeOwnershipRegistry();
            GatehouseDistanceOriginService originService =
                CreateOriginService(GatehousePermanentRuntimeState.CreateTestState(), conflictRegistry, new object());
            Assert(originService.Bind("A").TryApply(GatehouseDistanceOrigin.BuildingBoundsCenter, out _),
                "first distance-origin owner applies");
            Assert(!originService.Bind("B").TryApply(GatehouseDistanceOrigin.VanillaBuildingBegin, out NativeCapabilityDiagnostic conflict) &&
                conflict.State == NativeCapabilityState.Conflict && conflict.ConflictOwnerGuid == "A",
                "second distance-origin owner receives conflict diagnostics");
        }

        private static void TestGatehouseTransactionAndRounding()
        {
            GatehousePermanentRuntimeState state = GatehousePermanentRuntimeState.CreateTestState();
            IGatehouseTimingCapability capability = CreateGateService(
                state, new NativeOwnershipRegistry(), new object()).Bind("owner");
            Assert(!capability.TryApply(new GatehouseTimingSettings(true, double.NaN, 0, 5, 5), out NativeCapabilityDiagnostic invalid) &&
                invalid.State == NativeCapabilityState.ValidationFailed, "non-finite gatehouse input should fail");
            AssertThrows<ArgumentOutOfRangeException>(
                () => GatehouseTimingService.ConvertNativeUInt16(8192, 8, "value"), "native UInt16 overflow should fail");

            var rounded = new GatehouseTimingSettings(true, 0.0125, 0.0125, 5.0625, 5.0625);
            Assert(capability.TryApply(rounded, out NativeCapabilityDiagnostic applied) &&
                applied.Reason.Contains("41units") && applied.Reason.Contains("1ticks"),
                "AwayFromZero values and verified native units should be diagnosed");
            state.ReadTiming(out int aiDistance, out int aiDelay, out int humanDistance, out int humanDelay);
            Assert(aiDistance == 41 && aiDelay == 1 && humanDistance == 41 && humanDelay == 1,
                "all four rounded values should be atomically published");
            Assert(capability.TryApply(rounded, out _), "identical apply should be idempotent");
            Assert(capability.TryApply(new GatehouseTimingSettings(false, double.NaN, double.NaN, double.NaN, double.NaN), out _),
                "disabled settings restore Vanilla without validating unused values");
            state.ReadTiming(out aiDistance, out aiDelay, out humanDistance, out humanDelay);
            Assert(aiDistance == 200 && aiDelay == 1200 && humanDistance == 140 && humanDelay == 100,
                "disabled settings atomically publish all Vanilla values");
        }

        private static void TestGatehouseRollbackAndPageCleanup()
        {
            var registry = new NativeOwnershipRegistry();
            GatehousePermanentRuntimeState state = GatehousePermanentRuntimeState.CreateTestState();
            GatehouseTimingService service = CreateGateService(state, registry, new object());
            Assert(service.Bind("A").TryApply(new GatehouseTimingSettings(true, 1, 5, 10, 15), out _), "first owner applies");
            Assert(!service.Bind("B").TryApply(new GatehouseTimingSettings(true, 2, 6, 11, 16), out NativeCapabilityDiagnostic conflict) &&
                conflict.State == NativeCapabilityState.Conflict && conflict.ConflictOwnerGuid == "A",
                "second owner receives conflict diagnostics");
            state.ReadTiming(out int aiDistance, out int aiDelay, out int humanDistance, out int humanDelay);
            Assert(aiDistance == 120 && aiDelay == 200 && humanDistance == 80 && humanDelay == 40,
                "conflicting owner cannot alter the atomically published timing snapshot");
        }

        private static void TestGatehouseConcurrentPublication()
        {
            GatehousePermanentRuntimeState state = GatehousePermanentRuntimeState.CreateTestState();
            Exception writerFailure = null;
            var writer = new Thread(() =>
            {
                try
                {
                    for (int index = 0; index < 10000; index++)
                    {
                        if ((index & 1) == 0)
                            state.PublishTiming(11, 22, 33, 44);
                        else
                            state.PublishTiming(101, 202, 303, 404);
                    }
                }
                catch (Exception ex)
                {
                    writerFailure = ex;
                }
            });
            writer.Start();
            for (int index = 0; index < 10000; index++)
            {
                state.ReadTiming(out int aiDistance, out int aiDelay, out int humanDistance, out int humanDelay);
                bool vanilla = aiDistance == 200 && aiDelay == 1200 && humanDistance == 140 && humanDelay == 100;
                bool first = aiDistance == 11 && aiDelay == 22 && humanDistance == 33 && humanDelay == 44;
                bool second = aiDistance == 101 && aiDelay == 202 && humanDistance == 303 && humanDelay == 404;
                if (!vanilla && !first && !second)
                {
                    Assert(false, "parallel gatehouse readers must observe one complete immutable timing snapshot");
                    break;
                }
            }
            writer.Join();
            Assert(writerFailure == null, "parallel gatehouse publication must not fail");
        }

        private static void TestGatehouseAssemblerContracts()
        {
            const ulong moduleBase = 0x00007FF940F90000;
            const ulong stubAddress = 0x00007FF8D1144000;
            Instruction[] displaced = DecodeInstructions(
                GatehouseBuildTarget.Supported.VanillaDistanceBlockBytes,
                moduleBase + GatehousePermanentRuntimeState.DistanceHookRva);
            var distanceAssembler = new Assembler(64);
            GatehousePermanentRuntimeState.GenerateDistanceOrigin(
                distanceAssembler,
                displaced,
                0x000001A000001000);
            byte[] distanceStub = AssembleAndDecode(distanceAssembler, stubAddress);
            Assert(distanceStub.Length > displaced.Length,
                "gatehouse distance generator assembles its logical gate and relocated Vanilla fallback");

            var decisionAssembler = new Assembler(64);
            GatehousePermanentRuntimeState.GenerateDecision(
                decisionAssembler,
                new[] { Instruction.Create(Code.Nopd) },
                0x000001A000002000,
                moduleBase,
                moduleBase + GatehousePermanentRuntimeState.DecisionReturnRva,
                moduleBase + GatehousePermanentRuntimeState.ClosePathRva);
            byte[] decisionStub = AssembleAndDecode(decisionAssembler, stubAddress + 0x1000);
            Assert(decisionStub.Length > 0,
                "gatehouse timing generator assembles with one label per emitted instruction");
        }

        private static Instruction[] DecodeInstructions(byte[] bytes, ulong address)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = address;
            var result = new List<Instruction>();
            ulong end = address + unchecked((ulong)bytes.Length);
            while (decoder.IP < end)
            {
                Instruction instruction = decoder.Decode();
                Assert(!instruction.IsInvalid && instruction.NextIP <= end,
                    "gatehouse displaced Vanilla bytes decode without invalid instructions");
                result.Add(instruction);
            }
            return result.ToArray();
        }

        private static byte[] AssembleAndDecode(Assembler assembler, ulong address)
        {
            using (var stream = new MemoryStream())
            {
                assembler.Assemble(new StreamCodeWriter(stream), address);
                byte[] bytes = stream.ToArray();
                int offset = 0;
                while (offset < bytes.Length)
                {
                    if (offset <= bytes.Length - 14 &&
                        bytes[offset] == 0xFF && bytes[offset + 1] == 0x25 &&
                        bytes[offset + 2] == 0 && bytes[offset + 3] == 0 &&
                        bytes[offset + 4] == 0 && bytes[offset + 5] == 0)
                    {
                        offset += 14;
                        continue;
                    }
                    var remaining = new byte[bytes.Length - offset];
                    Buffer.BlockCopy(bytes, offset, remaining, 0, remaining.Length);
                    var decoder = Decoder.Create(64, new ByteArrayCodeReader(remaining));
                    decoder.IP = address + unchecked((ulong)offset);
                    Instruction instruction = decoder.Decode();
                    Assert(!instruction.IsInvalid && instruction.Length <= remaining.Length,
                        "generated gatehouse stub decodes completely");
                    offset += instruction.Length;
                }
                Assert(offset == bytes.Length, "generated gatehouse stub consumes its complete encoded span");
                return bytes;
            }
        }

        private static ApiSharedRuntime InitializeRuntime(
            byte[] image,
            GatehouseBuildTarget catalog,
            FakeMemory memory)
        {
            var runtime = new ApiSharedRuntime();
            runtime.Initialize(ModuleBase, image, catalog.BuildHash, memory, null, catalog, false);
            return runtime;
        }

        private static void AssertTimingValidationFailure(ApiSharedRuntime runtime, string message) =>
            Assert(!runtime.TryGetGatehouseTiming("owner", out _, out NativeCapabilityDiagnostic diagnostic) &&
                diagnostic.State == NativeCapabilityState.ValidationFailed, message);

        private static void AssertOriginValidationFailure(ApiSharedRuntime runtime, string message) =>
            Assert(!runtime.TryGetGatehouseDistanceOrigin("owner", out _, out NativeCapabilityDiagnostic diagnostic) &&
                diagnostic.State == NativeCapabilityState.ValidationFailed, message);

        private static void AssertBothGateValidationFailures(ApiSharedRuntime runtime, string message)
        {
            AssertTimingValidationFailure(runtime, message + " (timing)");
            AssertOriginValidationFailure(runtime, message + " (origin)");
        }

        private static GatehouseBuildTarget InstallTestGatehouse(byte[] image)
        {
            Copy(image, DistanceRva, VanillaDistanceBytes);
            Copy(image, DecisionRva, DecisionBytes);
            Copy(image, HumanDelayRva, HumanDelayBytes);
            return new GatehouseBuildTarget(
                "TESTHASH", FunctionRva, FunctionSize,
                ApiSharedRuntime.ComputeSha256(new ReadOnlySpan<byte>(image, FunctionRva, FunctionSize)),
                DistanceRva, VanillaDistanceBytes, CenteredDistanceBytes,
                DecisionRva, DecisionBytes, HumanDelayRva, HumanDelayBytes,
                DecisionRva + 8, DecisionRva + 15, DecisionRva + 24, HumanDelayRva + 3);
        }

        private static GatehouseBuildTarget CloneCatalog(
            GatehouseBuildTarget source,
            string functionHash = null,
            int? aiCloseDistanceRva = null) =>
            new GatehouseBuildTarget(
                source.BuildHash, source.FunctionRva, source.FunctionSize, functionHash ?? source.FunctionHash,
                source.DistanceBlockRva, source.VanillaDistanceBlockBytes, source.CenteredDistanceBlockBytes,
                source.DecisionBlockRva, source.DecisionBlockBytes, source.HumanDelayBlockRva, source.HumanDelayBlockBytes,
                aiCloseDistanceRva ?? source.AiCloseDistanceRva, source.AiReopenDelayRva,
                source.HumanCloseDistanceRva, source.HumanReopenDelayRva);

        private static FakeMemory SeedRuntimeMemory(byte[] image, GatehouseBuildTarget target)
        {
            var memory = new FakeMemory();
            for (int index = 0; index < target.VanillaDistanceBlockBytes.Length; index++)
                memory.SetByte(ModuleBase + target.DistanceBlockRva + index, image[target.DistanceBlockRva + index]);
            for (int index = 0; index < target.DecisionBlockBytes.Length; index++)
                memory.SetByte(ModuleBase + target.DecisionBlockRva + index, image[target.DecisionBlockRva + index]);
            for (int index = 0; index < target.HumanDelayBlockBytes.Length; index++)
                memory.SetByte(ModuleBase + target.HumanDelayBlockRva + index, image[target.HumanDelayBlockRva + index]);
            memory.Set(ModuleBase + target.AiCloseDistanceRva, 200);
            memory.Set(ModuleBase + target.AiReopenDelayRva, 1200);
            memory.Set(ModuleBase + target.HumanCloseDistanceRva, 140);
            memory.Set(ModuleBase + target.HumanReopenDelayRva, 100);
            return memory;
        }

        private static GatehouseDistanceOriginService CreateOriginService(
            GatehousePermanentRuntimeState state,
            NativeOwnershipRegistry ownership,
            object mutationSync)
        {
            var target = new GatehouseDistanceOriginTarget(
                ModuleBase + 0xB7B70,
                VanillaDistanceBytes,
                CenteredDistanceBytes);
            return new GatehouseDistanceOriginService("hash", target, state, ownership, mutationSync, null);
        }

        private static GatehouseTimingService CreateGateService(
            GatehousePermanentRuntimeState state,
            NativeOwnershipRegistry ownership,
            object mutationSync)
        {
            var invariants = new[]
            {
                new NativeByteInvariant(ModuleBase + 0xB7BC0, 0x41),
                new NativeByteInvariant(ModuleBase + 0xB7BC1, 0x81),
                new NativeByteInvariant(ModuleBase + 0xB7BC2, 0xF8),
                new NativeByteInvariant(ModuleBase + 0xB7C34, 0xB8)
            };
            var target = new GatehouseTimingTarget(
                ModuleBase + 0xB7BC3, ModuleBase + 0xB7BCA,
                ModuleBase + 0xB7BD3, ModuleBase + 0xB7C35, invariants);
            return new GatehouseTimingService("hash", target, state, ownership, mutationSync, null);
        }

        private static FakeMemory SeedDirectGateMemory()
        {
            var memory = new FakeMemory();
            memory.SetByte(ModuleBase + 0xB7BC0, 0x41);
            memory.SetByte(ModuleBase + 0xB7BC1, 0x81);
            memory.SetByte(ModuleBase + 0xB7BC2, 0xF8);
            memory.SetByte(ModuleBase + 0xB7C34, 0xB8);
            for (int index = 0; index < VanillaDistanceBytes.Length; index++)
                memory.SetByte(ModuleBase + 0xB7B70 + index, VanillaDistanceBytes[index]);
            memory.Set(ModuleBase + 0xB7BC3, 200);
            memory.Set(ModuleBase + 0xB7BCA, 1200);
            memory.Set(ModuleBase + 0xB7BD3, 140);
            memory.Set(ModuleBase + 0xB7C35, 100);
            return memory;
        }

        private static GatehouseTimingService CreateCrossPageGateService(FakeMemory memory)
        {
            var target = new GatehouseTimingTarget(
                ModuleBase + 0x1FF0, ModuleBase + 0x1FF4,
                ModuleBase + 0x1FF8, ModuleBase + 0x2004);
            return new GatehouseTimingService(
                "hash", target, GatehousePermanentRuntimeState.CreateTestState(),
                new NativeOwnershipRegistry(), new object(), null);
        }

        private static FakeMemory SeedCrossPageGateMemory()
        {
            var memory = new FakeMemory();
            for (int index = 0; index < VanillaDistanceBytes.Length; index++)
                memory.SetByte(ModuleBase + 0x1FE0 + index, VanillaDistanceBytes[index]);
            memory.Set(ModuleBase + 0x1FF0, 200);
            memory.Set(ModuleBase + 0x1FF4, 1200);
            memory.Set(ModuleBase + 0x1FF8, 140);
            memory.Set(ModuleBase + 0x2004, 100);
            return memory;
        }

        private static byte[] CreatePeImage(int size, bool executable)
        {
            var image = new byte[size];
            image[0] = 0x4D; image[1] = 0x5A;
            WriteInt32(image, 0x3C, 0x80);
            WriteInt32(image, 0x80, 0x4550);
            WriteUInt16(image, 0x86, 1);
            WriteUInt16(image, 0x94, 0xF0);
            WriteUInt16(image, 0x98, 0x20B);
            WriteInt32(image, 0x80 + 24 + 56, size);
            int section = 0x80 + 24 + 0xF0;
            WriteInt32(image, section + 8, size - 0x1000);
            WriteInt32(image, section + 12, 0x1000);
            WriteInt32(image, section + 16, size - 0x1000);
            WriteInt32(image, section + 36, unchecked((int)(executable ? 0x60000020 : 0x40000040)));
            return image;
        }

        private static byte[] Hex(string text)
        {
            string[] tokens = text.Split(' ');
            var bytes = new byte[tokens.Length];
            for (int index = 0; index < tokens.Length; index++)
                bytes[index] = Convert.ToByte(tokens[index], 16);
            return bytes;
        }

        private static void Copy(byte[] target, int offset, byte[] source) => Array.Copy(source, 0, target, offset, source.Length);
        private static void AssertBytes(FakeMemory memory, long address, byte[] expected, string message)
        {
            for (int index = 0; index < expected.Length; index++)
                if (memory.ReadByte(address + index) != expected[index])
                {
                    Assert(false, message + $" (mismatch at +0x{index:X})");
                    return;
                }
        }
        private static void AssertSequenceEqual(byte[] actual, byte[] expected, string message)
        {
            if (actual.Length != expected.Length)
            {
                Assert(false, message + $" (length {actual.Length}, expected {expected.Length})");
                return;
            }
            for (int index = 0; index < expected.Length; index++)
                if (actual[index] != expected[index])
                {
                    Assert(false, message + $" (mismatch at +0x{index:X})");
                    return;
                }
        }
        private static int IndexOfSequence(byte[] source, byte[] sequence)
        {
            for (int start = 0; start <= source.Length - sequence.Length; start++)
            {
                int index = 0;
                while (index < sequence.Length && source[start + index] == sequence[index])
                    index++;
                if (index == sequence.Length)
                    return start;
            }
            return -1;
        }
        private static void WriteUInt16(byte[] data, int offset, int value) { data[offset] = (byte)value; data[offset + 1] = (byte)(value >> 8); }
        private static void WriteInt32(byte[] data, int offset, int value)
        {
            data[offset] = (byte)value; data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16); data[offset + 3] = (byte)(value >> 24);
        }

        private static void AssertThrowsState(NativeCapabilityState expected, Action action, string message)
        {
            try { action(); Assert(false, message + " did not throw"); }
            catch (NativeResolutionException ex) { Assert(ex.State == expected, message + $": expected {expected}, got {ex.State}"); }
            catch (Exception ex) { Assert(false, message + $" threw {ex.GetType().Name}"); }
        }

        private static void AssertThrows<T>(Action action, string message) where T : Exception
        {
            try { action(); Assert(false, message + " did not throw"); }
            catch (T) { }
            catch (Exception ex) { Assert(false, message + $" threw {ex.GetType().Name}"); }
        }

        private static void Assert(bool condition, string message)
        {
            if (condition) return;
            failures++;
            Console.Error.WriteLine("FAIL: " + message);
        }

        private sealed class RecordingAivObserver : IAivBuildStepObserver
        {
            private readonly string name;
            private readonly List<string> calls;
            private readonly bool failBegin;
            private readonly bool failComplete;

            internal RecordingAivObserver(
                string name,
                List<string> calls,
                bool failBegin = false,
                bool failComplete = false)
            {
                this.name = name;
                this.calls = calls;
                this.failBegin = failBegin;
                this.failComplete = failComplete;
            }

            public IAivBuildStepInvocation TryBegin(AivBuildStepContext context)
            {
                calls.Add("begin:" + name);
                if (failBegin)
                    throw new InvalidOperationException("injected begin failure");
                return new RecordingAivInvocation(name, calls, failComplete);
            }
        }
        private static void AssertSequenceEqual(string[] actual, string[] expected, string message)
        {
            if (actual.Length != expected.Length)
            {
                Assert(false, message + $" (length {actual.Length}, expected {expected.Length})");
                return;
            }
            for (int index = 0; index < expected.Length; index++)
                if (!string.Equals(actual[index], expected[index], StringComparison.Ordinal))
                {
                    Assert(false, message + $" (mismatch at index {index}: '{actual[index]}', expected '{expected[index]}')");
                    return;
                }
        }

        private sealed class RecordingAivInvocation : IAivBuildStepInvocation
        {
            private readonly string name;
            private readonly List<string> calls;
            private readonly bool fail;

            internal RecordingAivInvocation(string name, List<string> calls, bool fail)
            {
                this.name = name;
                this.calls = calls;
                this.fail = fail;
            }

            public void Complete(AivBuildStepCompletion completion)
            {
                calls.Add($"complete:{name}:{completion.VanillaCompleted}:{completion.VanillaResult}");
                if (fail)
                    throw new InvalidOperationException("injected completion failure");
            }
        }

        private sealed class RecordingWorkingSourceProvider : IModSettingsWorkingSourceProvider
        {
            public event Action SourcesChanged;
            public List<string> Calls { get; } = new List<string>();
            public string PreferredId { get; set; } = ModSettingsWorkingSourceRegistry.TrailId;
            public string PreferenceContextId { get; set; } = "trail-a";
            public IReadOnlyList<ModSettingsWorkingSource> GetSources(string targetGuid) => new[]
            {
                new ModSettingsWorkingSource { Id = ModSettingsWorkingSourceRegistry.TrailId, Kind = ModSettingsWorkingSourceKind.Trail, DisplayName = "Trail", IsPreferred = PreferredId == ModSettingsWorkingSourceRegistry.TrailId, PreferenceContextId = PreferenceContextId },
                new ModSettingsWorkingSource { Id = ModSettingsWorkingSourceRegistry.MapId, Kind = ModSettingsWorkingSourceKind.Map, DisplayName = "Map", IsPreferred = PreferredId == ModSettingsWorkingSourceRegistry.MapId, PreferenceContextId = PreferenceContextId },
            };
            public void Apply(string targetGuid, string sourceId) => Calls.Add("one:" + targetGuid + ":" + sourceId);
            public void ApplyMany(IEnumerable<string> targetGuids, string sourceId) => Calls.Add("many:" + string.Join(",", targetGuids) + ":" + sourceId);
            public void RaiseChanged() => SourcesChanged?.Invoke();
        }

        private sealed class PresetSaveTestViewModel : PresetLobbyModSettingsViewModel
        {
            protected override string ResolveSettingsUiText(string key, string fallback) =>
                key == "Common.ClientOptions" ? "Translated client options" : key;
        }

        private sealed class DynamicDefaultTestViewModel : PresetLobbyModSettingsViewModel
        {
            [PresetLocal]
            public string[] DynamicValues { get; set; } = Array.Empty<string>();

            public void PublishDefault(string[] value) =>
                SetModDefaultValue(nameof(DynamicValues), value);

            public void PublishUnknownDefault() =>
                SetModDefaultValue("Missing", Array.Empty<string>());

            public void PublishWrongTypeDefault() =>
                SetModDefaultValue(nameof(DynamicValues), 42);
        }

        private sealed class FakeMemory : INativeMemory
        {
            private readonly Dictionary<long, int> values = new Dictionary<long, int>();
            private readonly Dictionary<long, byte> bytes = new Dictionary<long, byte>();
            public int PageSize => 0x1000;
            public long? FailNextWriteAddress { get; set; }
            public long? FailNextWriteByteAddress { get; set; }
            public bool FailRestore { get; set; }
            public bool FailFlush { get; set; }
            public long? MutateOnMakeWritableAddress { get; set; }
            public int MutateOnMakeWritableValue { get; set; }
            public int OperationCount { get; private set; }
            public int WriteCount { get; private set; }
            public List<long> WritablePages { get; } = new List<long>();
            public List<RestoredProtection> RestoredProtections { get; } = new List<RestoredProtection>();
            public void Set(long address, int value) => values[address] = value;
            public void SetByte(long address, byte value) => bytes[address] = value;
            public int ReadRaw(long address) => values[address];
            public byte ReadByte(long address) { OperationCount++; return bytes[address]; }
            public int ReadInt32(long address) { OperationCount++; return values[address]; }
            public void WriteByte(long address, byte value)
            {
                OperationCount++; WriteCount++;
                if (FailNextWriteByteAddress == address) { FailNextWriteByteAddress = null; throw new InvalidOperationException("injected byte write failure"); }
                bytes[address] = value;
            }
            public void WriteInt32(long address, int value)
            {
                OperationCount++; WriteCount++;
                if (FailNextWriteAddress == address) { FailNextWriteAddress = null; throw new InvalidOperationException("injected write failure"); }
                values[address] = value;
            }
            public uint MakeWritable(long address, int length)
            {
                OperationCount++;
                WritablePages.Add(address);
                if (MutateOnMakeWritableAddress.HasValue)
                {
                    values[MutateOnMakeWritableAddress.Value] = MutateOnMakeWritableValue;
                    MutateOnMakeWritableAddress = null;
                }
                return (uint)(0x20 + WritablePages.Count);
            }
            public void RestoreProtection(long address, int length, uint protection)
            {
                OperationCount++;
                RestoredProtections.Add(new RestoredProtection(address, protection));
                if (FailRestore) { FailRestore = false; throw new InvalidOperationException("injected restore failure"); }
            }
            public void Flush(long address, int length)
            {
                OperationCount++;
                if (FailFlush) { FailFlush = false; throw new InvalidOperationException("injected flush failure"); }
            }
        }

        private readonly struct RestoredProtection
        {
            public RestoredProtection(long address, uint protection) { Address = address; Protection = protection; }
            public long Address { get; }
            public uint Protection { get; }
        }

    }
}
