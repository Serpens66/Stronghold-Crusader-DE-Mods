using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shared;
using SHCDESE.API;

internal static class RestartSettingsTests
{
    internal static void Run()
    {
        GameNetworkAPI.LocalHost = true;
        var vm = new Model();
        string root = Path.Combine(Path.GetTempPath(), "RestartSettingsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        vm.PreparePresets(null, Path.Combine(root, "Probe.dll"), "RestartProbe", "RestartProbe", new Version(1, 0));
        vm.ActivatePresets();
        Check(!vm.System_ApplyConfiguration(""), "unchanged configuration requested restart");
        vm.Source.Working["live"] = 2L;
        Check(!vm.System_ApplyConfiguration("") && vm.Source.Applied == 1, "live change was not applied");
        vm.Source.Working["startup"] = 3L;
        vm.Source.Working["live"] = 4L;
        Check(vm.System_ApplyConfiguration("mission") && vm.Source.Staged == 1, "restart change not staged");
        Check((long)vm.Source.Active["live"] == 2 && (long)vm.Source.Pending["live"] == 4, "mixed configuration partly applied");
        vm.Source.FailStage = true;
        vm.Source.Working["startup"] = 9L;
        try { vm.System_ApplyConfiguration("mission"); throw new Exception("failure not propagated"); }
        catch (IOException) { }
        Check((long)vm.Source.Pending["startup"] == 3, "failed replacement lost prior pending values");
        vm.Source.FailStage = false;
        vm.Source.Working["startup"] = 3L;
        vm.Source.Restart();
        Check(!vm.System_ApplyConfiguration("mission"), "loaded preparation requested another restart");
        Check((long)vm.Source.Own["startup"] == 1, "temporary configuration changed personal values");
        vm.System_ReturnToOwnConfiguration();
        Check(vm.Source.Pending != null && (long)vm.Source.Pending["startup"] == 1, "return to personal values was not prepared");
        vm.Source.Options[0].RequiresRestart = false;
        vm.Source.Working["startup"] = 7L;
        Check(!vm.System_ApplyConfiguration("") && (long)vm.Source.Active["startup"] == 7 && vm.Source.Pending == null,
            "runtime-capable upgrade retained restart machinery");
        Check(typeof(Attributed).GetProperty("Startup").IsDefined(typeof(RequiresRestartAttribute), true), "attribute missing");
        Console.WriteLine("PASS: unchanged/live/restart/mixed settings, atomic rejection, personal preservation and runtime-capability upgrade");
        string journal = Path.Combine(root, "restart.json");
        ModSettingsApplication.ResetForTests(journal);
        ModSettingsApplication.Register("RestartProbe", vm, Path.Combine(root, "Probe.dll"));
        vm.Source.Options[0].RequiresRestart = true;
        vm.Source.Working["startup"] = 13L;
        ModSettingsApplication.EnterContext("mission|original-content");
        Check(ModSettingsApplication.Commit("RestartProbe"), "context change did not prepare restart");
        byte[] validJournal = File.ReadAllBytes(journal);
        vm.Source.FailStage = true;
        vm.Source.Working["startup"] = 14L;
        try { ModSettingsApplication.Commit("RestartProbe"); throw new Exception("failed stage accepted"); }
        catch (IOException) { }
        Check(validJournal.SequenceEqual(File.ReadAllBytes(journal)), "failed replacement changed durable preparation");
        try { ModSettingsApplication.PrepareLaunch(); throw new Exception("failed launch stage accepted"); }
        catch (IOException) { }
        Check(validJournal.SequenceEqual(File.ReadAllBytes(journal)), "failed launch stage changed durable preparation");
        vm.Source.FailStage = false;
        vm.Source.Restart();
        ModSettingsApplication.ResetForTests(journal);
        ModSettingsApplication.Register("RestartProbe", vm, Path.Combine(root, "Probe.dll"));
        ModSettingsApplication.EnterContext("mission|original-content");
        Check(ModSettingsApplication.ResumeSnapshot("RestartProbe") != null, "process restart lost preparation");
        Check(MessagePack.MessagePackSerializer.Deserialize<long>(ModSettingsApplication.ResumeSnapshot("RestartProbe")["startup"]) == 13L, "restored snapshot did not contain actual desired provider values");
        try { ModSettingsApplication.EnterContext("mission|changed-content"); throw new Exception("changed content accepted"); }
        catch (InvalidDataException) { }
        vm.Source.Own["startup"] = 99L;
        try { ModSettingsApplication.EnterContext("mission|original-content"); throw new Exception("changed personal baseline accepted"); }
        catch (InvalidDataException) { }
        vm.Source.Own["startup"] = 1L;
        ModSettingsApplication.ResetForTests(journal);
        try { ModSettingsApplication.EnterContext("mission|original-content"); ModSettingsApplication.PrepareLaunch(); throw new Exception("missing provider accepted"); }
        catch (InvalidDataException) { }
        ModSettingsApplication.EnterContext("unrelated|other-content");
        Check(ModSettingsApplication.PrepareLaunch(), "missing provider blocked unrelated context");
        ModSettingsApplication.ConfirmStarted();
        Check(File.Exists(journal), "unrelated mission consumed another preparation");
        string[] unavailable = ModSettingsApplication.DiscardPreparationWithReport();
        Check(unavailable.Contains("RestartProbe") && !File.Exists(journal), "missing provider cleanup misreported or inaccessible");
        File.WriteAllText(journal, "{}");
        ModSettingsApplication.ResetForTests(journal);
        try { ModSettingsApplication.EnterContext("mission|original-content"); throw new Exception("corrupt preparation accepted"); }
        catch (InvalidDataException) { }
        ModSettingsApplication.ResetForTests(Path.Combine(root, "unused.json"));
        Console.WriteLine("PASS: durable restart recovery, failed replacement rollback, changed content, missing provider and corrupt preparation");
        vm.Source.Working["startup"] = 13L;
        Check(MessagePack.MessagePackSerializer.Deserialize<long>(vm.System_CreatePlayerMissionPresetSnapshot()["startup"]) == 1L,
            "player source used prior working or mission values instead of personal values");
        Check(MessagePack.MessagePackSerializer.Deserialize<long>(vm.System_CreateCurrentWorkingSnapshot()["startup"]) == 13L,
            "current source did not use desired provider values");
        Check(!vm.System_ApplyConfiguration("", true) && (long)vm.Source.Pending["startup"] == 13L,
            "already active personal choice was lost or unnecessarily required restart");
        int persistedChoiceCount = vm.Source.Staged;
        Check(!vm.System_ApplyConfiguration("") && vm.Source.Pending != null && vm.Source.Staged == persistedChoiceCount,
            "launch check discarded or republished an already confirmed personal choice");
        vm.Source.DiscardPendingConfiguration();
        Check(!vm.System_ApplyConfiguration("") && vm.Source.Pending == null,
            "launch check recreated a discarded personal choice");
        vm.Source.NetworkClient = true;
        vm.Source.NetworkReady = false;
        vm.Source.Working["startup"] = 100L;
        int stageCount = vm.Source.Staged;
        Check(vm.System_ApplyConfiguration("client-mission") && vm.Source.Staged == stageCount,
            "client admission staged working host values as personal values");
        vm.Source.NetworkReady = true;
        Check(!vm.System_ApplyConfiguration("client-mission") && vm.Source.Staged == stageCount,
            "verified network provider was replaced by local configuration application");
        vm.Source.ApplyValues(new Dictionary<string, object> { ["startup"] = 55L, ["live"] = 2L }, "network:host");
        vm.DiscardApplicationPackage();
        Check(vm.Source.Pending != null && (long)vm.Source.Pending["startup"] == 1L && vm.System_ApplicationNotice.Length != 0,
            "discarding a loaded client context did not prepare and explain personal restoration");
        vm.Source.Options[0].RequiresRestart = false;
        vm.DiscardApplicationPackage();
        Check(vm.Source.Pending == null && (long)vm.Source.Active["startup"] == 1L && vm.System_ApplicationNotice.Length == 0,
            "runtime upgrade still staged restoration instead of applying personal values");
        vm.Source.NetworkClient = false;
        Console.WriteLine("PASS: personal versus desired sources, unchanged personal persistence and network-owned client admission");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private sealed class Attributed { [RequiresRestart] public long Startup { get; set; } }
    private sealed class Model : PresetLobbyModSettingsViewModel
    {
        internal readonly Backend Source = new Backend();
        protected override IDynamicPresetSettingsProvider DynamicSettingsProvider => Source;
    }
    private sealed class Backend : IDynamicPresetSettingsProvider, IModSettingsApplicationBackend, INetworkModSettingsApplicationBackend
    {
        internal readonly DynamicPresetSetting[] Options = {
            new DynamicPresetSetting { Key = "startup", ValueType = typeof(long), DefaultValue = 1L, Scope = PresetSettingScope.Host, RequiresRestart = true },
            new DynamicPresetSetting { Key = "live", ValueType = typeof(long), DefaultValue = 1L, Scope = PresetSettingScope.Local }
        };
        internal Dictionary<string, object> Own = new Dictionary<string, object> { ["startup"] = 1L, ["live"] = 1L };
        internal Dictionary<string, object> Working, Active, Pending;
        internal bool FailStage, NetworkClient, NetworkReady;
        public bool IsNetworkConfigurationClient => NetworkClient;
        public bool PrepareNetworkConfiguration() => NetworkReady;
        internal int Staged, Applied;
        internal string PendingContext;
        public string ActiveContextId { get; private set; } = "";
        internal Backend() { Working = Copy(Own); Active = Copy(Own); }
        private static Dictionary<string, object> Copy(Dictionary<string, object> source) => source == null ? null : new Dictionary<string, object>(source);
        public IReadOnlyList<DynamicPresetSetting> GetSettings() => Options;
        public Dictionary<string, object> ReadValues() => Copy(Working);
        public object ReadValue(string key) => Working[key];
        public void ValidateValues(Dictionary<string, object> values) { if (values.Count != 2 || Options.Any(x => !values.ContainsKey(x.Key) || !(values[x.Key] is long))) throw new InvalidDataException(); }
        public void ReplaceValues(Dictionary<string, object> values) { ValidateValues(values); Working = Copy(values); }
        public Dictionary<string, object> ReadDesiredValues() => ReadValues();
        public void ReplaceDesiredValues(Dictionary<string, object> values) => ReplaceValues(values);
        public Dictionary<string, object> ReadActiveValues() => Copy(Active);
        public Dictionary<string, object> ReadOwnValues() => Copy(Own);
        public Dictionary<string, object> ReadPendingValues() => Copy(Pending);
        public void StageValues(Dictionary<string, object> values, string contextId) { if (FailStage) throw new IOException("simulated write failure"); Pending = Copy(values); PendingContext = contextId; Staged++; }
        public void ApplyValues(Dictionary<string, object> values, string contextId) { Active = Copy(values); ActiveContextId = contextId; Applied++; }
        public void ReturnToOwnConfiguration() => StageValues(Own, "");
        public void DiscardPendingConfiguration() { Pending = null; }
        internal void Restart() { Active = Copy(Pending); ActiveContextId = PendingContext; Pending = null; }
    }
}
