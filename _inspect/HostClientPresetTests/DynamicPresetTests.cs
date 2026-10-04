using Shared;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal static class DynamicPresetTests
{
    internal static void Run()
    {
        GameNetworkAPI.LocalHost = true;
        var vm = new Model();
        string root = Path.Combine(Path.GetTempPath(), "DynamicPresetTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        vm.PreparePresets(null, Path.Combine(root, "Probe.dll"), "DynamicProbe", "CrusaderDETweaker", new Version(2, 8));
        vm.ActivatePresets();
        Check(vm.System_GetPresetSettingDescriptors().Count == 12000, "large catalog incomplete");
        var mixed = new Dictionary<string, PublishedPresetSetting>
        {
            ["matrix/row0/column"] = new PublishedPresetSetting { Mode = PublishedPresetValueMode.Fixed, Value = 123 },
            ["matrix/row1/column"] = new PublishedPresetSetting { Mode = PublishedPresetValueMode.ModDefault },
            ["matrix/row2/column"] = new PublishedPresetSetting { Mode = PublishedPresetValueMode.Player }
        };
        vm.System_ImportPresetJson(ModSettingsPresetJson.Serialize("CrusaderDETweaker", "mixed", "Mixed", "", "", "", mixed));
        vm.System_TestLoadPreset(vm.System_TestPublishedPresets.Single(x => x.Id == "mixed").StableId);
        Check((long)vm.Source.ReadValue("matrix/row0/column") == 123, "fixed value lost");
        Check((long)vm.Source.ReadValue("matrix/row1/column") == -1, "default used current value instead of provider default");
        Check((long)vm.Source.ReadValue("matrix/row2/column") == 12, "player value changed");
        Check(vm.Source.Replacements == 2, "snapshot was not applied as a whole");
        var all = vm.System_GetPresetSettingDescriptors().Select(x => new PresetSaveSelection { PropertyName = x.PropertyName, Mode = PublishedPresetValueMode.Fixed });
        string saved = vm.System_SavePersonalPreset("all", "All", "", all, false);
        Check(ModSettingsPresetJson.Parse(File.ReadAllText(saved), "test", "test", "CrusaderDETweaker", saved).Settings.Count == 12000, "large preset roundtrip incomplete");
        var bad = vm.System_CreateCurrentWorkingSnapshot();
        bad["matrix/row0/column"] = MessagePack.MessagePackSerializer.Serialize(typeof(long), 555L);
        bad["matrix/row2/column"] = MessagePack.MessagePackSerializer.Serialize(typeof(long), -2L);
        int replacements = vm.Source.Replacements;
        Reject(() => vm.System_ApplyWorkingSnapshot(bad));
        Check((long)vm.Source.ReadValue("matrix/row0/column") == 123 && vm.Source.Replacements == replacements, "invalid snapshot partially applied");
        mixed["matrix/row0/column"].Mode = PublishedPresetValueMode.Player;
        vm.System_ImportPresetJson(ModSettingsPresetJson.Serialize("CrusaderDETweaker", "second", "Second", "", "", "", mixed));
        vm.System_TestLoadPreset(vm.System_TestPublishedPresets.Single(x => x.Id == "second").StableId);
        Check((long)vm.Source.ReadValue("matrix/row0/column") == 123, "successive preset did not preserve working copy");
        Check(vm.ConfirmedSelections == 2, "confirmed selection hook was bypassed");
        vm.FailConfirmation = true;
        replacements = vm.Source.Replacements;
        Reject(() => vm.System_TestLoadPreset(vm.System_TestPublishedPresets.Single(x => x.Id == "mixed").StableId));
        Check(vm.Source.Replacements == replacements, "rejected confirmation applied a snapshot");
        Reject(() => ModSettingsPresetJson.Parse(new string(' ', 8 * 1024 * 1024 + 1), "test", "test", "CrusaderDETweaker", ""));
        var oversized = Enumerable.Range(0, 16385).ToDictionary(i => "key" + i, i => new PublishedPresetSetting { Mode = PublishedPresetValueMode.Player });
        Reject(() => ModSettingsPresetJson.Serialize("CrusaderDETweaker", "large", "Large", "", "", "", oversized));
        Console.WriteLine("PASS: dynamic 12000-option catalog, shared JSON, mixed modes, successive presets and atomic rejection");
    }
    private static void Check(bool result, string message) { if (!result) throw new Exception(message); }
    private static void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Expected rejection"); }
    private sealed class Model : PresetLobbyModSettingsViewModel
    {
        internal readonly Provider Source = new Provider();
        internal int ConfirmedSelections;
        internal bool FailConfirmation;
        protected override IDynamicPresetSettingsProvider DynamicSettingsProvider => Source;
        protected override void ApplyConfirmedPresetSelection(PublishedModSettingsPreset preset)
        {
            if (FailConfirmation) throw new InvalidDataException("Confirmation rejected");
            base.ApplyConfirmedPresetSelection(preset);
            ConfirmedSelections++;
        }
    }
    private sealed class Provider : IDynamicPresetSettingsProvider
    {
        private readonly DynamicPresetSetting[] options = Enumerable.Range(0, 12000).Select(i => new DynamicPresetSetting
        {
            Key = "matrix/row" + i + "/column", ValueType = typeof(long), DefaultValue = -1L, Scope = PresetSettingScope.Host
        }).ToArray();
        private Dictionary<string, object> values;
        internal int Replacements;
        internal Provider() { values = options.Select((item, i) => new { item.Key, Value = (object)(long)(i + 10) }).ToDictionary(x => x.Key, x => x.Value); }
        public IReadOnlyList<DynamicPresetSetting> GetSettings() => options;
        public object ReadValue(string key) => values[key];
        public Dictionary<string, object> ReadValues() => new Dictionary<string, object>(values);
        public void ValidateValues(Dictionary<string, object> candidate)
        {
            if (candidate.Count != options.Length || options.Any(x => !candidate.ContainsKey(x.Key)) || candidate.Values.Any(x => !(x is long number) || number < -1))
                throw new InvalidDataException("Invalid candidate");
        }
        public void ReplaceValues(Dictionary<string, object> candidate) { ValidateValues(candidate); values = new Dictionary<string, object>(candidate); Replacements++; }
    }
}
