using System;
using System.Collections.Generic;
using System.IO;
using MessagePack;
using SHCDESE.API;
using APIShared.ModSettings;
using Spectator;

internal static class LegacySpectatorSettingsTests
{
    private const string Perspective = "EnableSpectatorPerspective", Defeat = "EnableEliminatedPlayersBecomeSpectators";
    private static void Check(bool ok, string reason) { if (!ok) throw new Exception("Legacy migration: " + reason); }
    private static Dictionary<string, byte[]> Values(bool perspective, bool defeat) => new Dictionary<string, byte[]> {
        { Perspective, MessagePackSerializer.Serialize(perspective) }, { Defeat, MessagePackSerializer.Serialize(defeat) }
    };
    private static Dictionary<string, bool> Defaults() => new Dictionary<string, bool> { { Perspective, true }, { Defeat, true } };
    private static string Settings(string root, string guid) => Path.Combine(root, "SHCDE", "APIShared", guid, "settings.msgpack");
    private static void Write(string file, Dictionary<string, byte[]> values, int version = 3, int selected = 0)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        var payload = values;
        if (version > 0)
        {
            payload = new Dictionary<string, byte[]> {
                { "__SerpPresetSchemaVersion", MessagePackSerializer.Serialize(version) },
                { Perspective, MessagePackSerializer.Serialize(true) } // stale top-level value must not win
            };
            if (version == 3) payload.Add("__SerpCurrentSettings", MessagePackSerializer.Serialize(values));
            else
            {
                payload.Add("__SerpActivePreset", MessagePackSerializer.Serialize(selected));
                payload.Add("__SerpPreset1", MessagePackSerializer.Serialize(selected == 0 ? values : Values(true, true)));
                payload.Add("__SerpPreset2", MessagePackSerializer.Serialize(selected == 1 ? values : Values(true, true)));
            }
        }
        File.WriteAllBytes(file, MessagePackSerializer.Serialize(payload));
    }
    private static string NewRoot()
    {
        string root = TestWorkDirectory.New("SpectatorMigration");
        PersonalSettingsPaths.TestDocumentsDirectory = _ => root;
        GameNetworkAPI.Networked = GameNetworkAPI.MultiplayerGame = false; GameNetworkAPI.LocalHost = true;
        return root;
    }
    private static SpectatorViewModel Register(string root)
    {
        var vm = new SpectatorViewModel();
        vm.PreparePresets(null, Path.Combine(root, "plugin", "Spectator.dll"), "Spectator_Serp", "Spectator_Serp", new Version(0,1));
        vm.ActivatePresets(); return vm;
    }
    private static LegacySpectatorSettings Plan(string root, string targetLegacy = null, string sourceLegacy = null)
    {
        var migration = new LegacySpectatorSettings(root, targetLegacy, sourceLegacy);
        migration.Prepare(Defaults()); return migration;
    }
    private static void Apply(LegacySpectatorSettings plan, SpectatorViewModel vm, bool host = true) =>
        plan.Apply(key => key == Perspective || host, vm.System_CreateCurrentWorkingSnapshot, vm.System_ApplyWorkingSnapshot);

    internal static void Run()
    {
        foreach (int format in new[] {0,1,2,3})
        {
            string root = NewRoot(), source = Settings(root, "BugfixesAndQoL_Serp");
            var old = Values(false, false); old.Add("EnableSurrenderAndStatistics", MessagePackSerializer.Serialize(false));
            Write(source, old, format, format == 1 ? 1 : 0);
            byte[] original = File.ReadAllBytes(source);
            var plan = Plan(root); var vm = Register(root); Apply(plan, vm);
            var after = Register(root);
            Check(!after.EnableSpectatorPerspective && !after.EnableEliminatedPlayersBecomeSpectators && after.EnableStatistics,
                "only the two matching flags import from format " + format);
            Check(Convert.ToBase64String(original) == Convert.ToBase64String(File.ReadAllBytes(source)), "source is read-only");
            vm.ResetToDefaultCommand.Execute(null); Apply(Plan(root), vm);
            Check(vm.EnableSpectatorPerspective && vm.EnableEliminatedPlayersBecomeSpectators && Register(root).EnableSpectatorPerspective,
                "completion survives reset and restart");
        }
        {
            string root = NewRoot();
            Write(Settings(root, "Spectator_Serp"), new Dictionary<string, byte[]> { { Perspective, MessagePackSerializer.Serialize(false) } });
            Write(Settings(root, "BugfixesAndQoL_Serp"), Values(true, false));
            var plan = Plan(root); var vm = Register(root); Apply(plan, vm);
            Check(!vm.EnableSpectatorPerspective && !vm.EnableEliminatedPlayersBecomeSpectators, "saved false wins; missing property imports");
        }
        {
            string root = NewRoot(); Write(Settings(root, "BugfixesAndQoL_Serp"), Values(false, false));
            Plan(root); Register(root); // interruption after registration materializes defaults
            var retry = Plan(root); var vm = Register(root);
            Apply(retry, vm, false); Check(!vm.EnableSpectatorPerspective && vm.EnableEliminatedPlayersBecomeSpectators, "client migrates its local perspective while host promotion stays deferred");
            Apply(retry, vm); Check(!vm.EnableSpectatorPerspective, "pending intent survives default materialization and role deferral");
        }
        {
            string root = NewRoot(); Write(Settings(root, "BugfixesAndQoL_Serp"), Values(false, false));
            var plan = Plan(root); var vm = Register(root);
            plan.PreserveEdit(Perspective); Apply(plan, vm);
            Check(vm.EnableSpectatorPerspective && !vm.EnableEliminatedPlayersBecomeSpectators, "later local edit cancels only its own pending import");
        }
        {
            string root = NewRoot(); Write(Settings(root, "BugfixesAndQoL_Serp"), Values(false, false));
            var plan = Plan(root); var vm = Register(root);
            string target = Settings(root, "Spectator_Serp"); bool refused = false;
            using (File.Open(target, FileMode.Open, FileAccess.Read, FileShare.Read))
                try { Apply(plan, vm); } catch (IOException) { refused = true; }
            Check(refused, "failed atomic settings persistence cannot complete migration");
            var retry = Plan(root); vm = Register(root); Apply(retry, vm);
            Check(!Register(root).EnableSpectatorPerspective, "failed persistence is retried on restart");
        }
        {
            string root = NewRoot(), legacy = Path.Combine(root, "old", "BFQ.msgpack");
            Write(legacy, Values(false, false), 0);
            var plan = Plan(root, sourceLegacy: legacy); var vm = Register(root); Apply(plan, vm);
            Check(!vm.EnableSpectatorPerspective, "documented plugin-local legacy file is read when canonical is absent");
        }
        {
            string root = NewRoot(), canonical = Settings(root, "BugfixesAndQoL_Serp"), legacy = Path.Combine(root, "old", "BFQ.msgpack");
            Write(legacy, Values(false, false)); Write(canonical, Values(true, true));
            var plan = Plan(root, sourceLegacy: legacy); var vm = Register(root); Apply(plan, vm);
            Check(vm.EnableSpectatorPerspective && vm.EnableEliminatedPlayersBecomeSpectators, "canonical snapshot wins over stale plugin-local file");
            File.WriteAllBytes(canonical, new byte[] { 0xc1 });
            bool rejected = false;
            try { LegacySpectatorSettings.ReadSettings(canonical, legacy); } catch { rejected = true; }
            Check(rejected, "corrupt canonical never revives stale legacy values");
        }
        {
            string root = NewRoot(), canonical = Settings(root, "BugfixesAndQoL_Serp");
            Write(canonical, Values(false, false)); bool rejected = false;
            using (File.Open(canonical, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                try { Plan(root); } catch (IOException) { rejected = true; }
            Check(rejected && File.Exists(Path.Combine(root, "SHCDE", "Spectator", "legacy-settings-migration.json")),
                "initial target absence is journaled before a source read fails");
            Register(root); // defaults may be materialized while BFQ remains temporarily unavailable
            var plan = Plan(root); var vm = Register(root); Apply(plan, vm); Check(!vm.EnableSpectatorPerspective, "source retry succeeds");
        }
        {
            string root = NewRoot(); var plan = Plan(root); var vm = Register(root); Apply(plan, vm);
            Check(vm.EnableSpectatorPerspective && !vm.EnableSingleplayerTestMode, "no legacy file keeps code defaults");
        }
        Console.WriteLine("PASS: production Spectator legacy migration precedence, formats, role deferral, crash recovery and atomic-write failure/retry.");
    }
}
