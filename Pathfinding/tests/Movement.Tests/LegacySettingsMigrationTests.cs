using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MessagePack;
using Microsoft.CSharp;

namespace Pathfinding.Movement.Tests
{
    internal static class LegacySettingsMigrationTests
    {
        private static readonly string Key = LegacyBfqolSettings.Keys[0];
        private static string SettingsPath(string root, string guid) => Path.Combine(root, "SHCDE", "APIShared", guid, "settings.msgpack");
        private static string JournalPath(string root) => Path.Combine(root, "SHCDE", "Pathfinding", "legacy-settings-migration.json");
        private static Dictionary<string, bool> Defaults() => LegacyBfqolSettings.Keys.ToDictionary(key => key, _ => true, StringComparer.Ordinal);
        private static Dictionary<string, byte[]> Value(bool value) => new Dictionary<string, byte[]> { { Key, MessagePackSerializer.Serialize(value) } };
        private static LegacyBfqolSettings Create(string root) => new LegacyBfqolSettings(root, null, null);
        private static void Write(string path, Dictionary<string, byte[]> values, int schema = 3)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var envelope = values;
            if (schema > 0)
            {
                envelope = new Dictionary<string, byte[]> { { "__SerpPresetSchemaVersion", MessagePackSerializer.Serialize(schema) } };
                if (schema == 3) envelope.Add("__SerpCurrentSettings", MessagePackSerializer.Serialize(values));
                else
                {
                    envelope.Add("__SerpActivePreset", MessagePackSerializer.Serialize(1));
                    envelope.Add("__SerpPreset1", MessagePackSerializer.Serialize(Value(true)));
                    envelope.Add("__SerpPreset2", MessagePackSerializer.Serialize(values));
                }
            }
            File.WriteAllBytes(path, MessagePackSerializer.Serialize(envelope));
        }
        private static bool Read(string path) => MessagePackSerializer.Deserialize<bool>(LegacyBfqolSettings.ReadSettings(path, null)[Key]);
        private static bool Reject(Action action) { try { action(); return false; } catch (IOException) { return true; } catch (InvalidDataException) { return true; } catch (UnauthorizedAccessException) { return true; } catch (InvalidOperationException) { return true; } }

        // The settings writer is an external boundary here: it merges supplied keys into a detached
        // working snapshot and persists that snapshot. The importer and its file/journal I/O are real.
        private static void Apply(LegacyBfqolSettings plan, string root, Dictionary<string, byte[]> current, bool host = true, bool persist = true)
        {
            plan.Apply(host, () => current, changes =>
            {
                foreach (var entry in changes) current[entry.Key] = entry.Value;
                if (persist) Write(SettingsPath(root, "Pathfinding_Serp"), current);
            });
        }

        internal static void Run(Action<bool, string> check, string project)
        {
            string suite = TestWorkDirectory.New("PathfindingMigration");
            int caseNumber = 0;
            Func<string> next = () => { string path = Path.Combine(suite, "case-" + ++caseNumber); Directory.CreateDirectory(path); return path; };
            foreach (int schema in new[] { 0, 1, 2, 3 })
            {
                string root = next(), source = SettingsPath(root, "BugfixesAndQoL_Serp"), target = SettingsPath(root, "Pathfinding_Serp");
                Write(source, Value(false), schema); byte[] before = File.ReadAllBytes(source);
                var plan = Create(root); plan.Prepare(Defaults());
                Apply(plan, root, Value(true));
                check(!Read(target) && before.SequenceEqual(File.ReadAllBytes(source)), "legacy false imports from schema " + schema + " without modifying BFQoL");
                Write(target, Value(true)); var restart = Create(root); restart.Prepare(Defaults()); Apply(restart, root, Value(true));
                check(Read(target), "completed migration cannot override a later saved choice on restart");
            }
            foreach (bool targetValue in new[] { false, true })
            {
                string root = next(); Write(SettingsPath(root, "BugfixesAndQoL_Serp"), Value(!targetValue)); Write(SettingsPath(root, "Pathfinding_Serp"), Value(targetValue));
                var plan = Create(root); plan.Prepare(Defaults()); Apply(plan, root, Value(targetValue));
                check(Read(SettingsPath(root, "Pathfinding_Serp")) == targetValue, "existing target value wins, including saved false");
            }
            {
                string root = next(); Write(SettingsPath(root, "BugfixesAndQoL_Serp"), Value(false));
                var plan = Create(root); plan.Prepare(Defaults()); var working = Value(true); Apply(plan, root, working, host: false);
                plan.PreserveEdit(Key); Apply(plan, root, working);
                check(!File.Exists(SettingsPath(root, "Pathfinding_Serp")), "explicit edit during host-role deferral cancels the pending legacy import");
            }
            {
                string root = next(), source = SettingsPath(root, "BugfixesAndQoL_Serp"); Write(source, Value(false));
                var plan = Create(root); plan.Prepare(Defaults()); Apply(plan, root, Value(true), host: false);
                var restart = Create(root); restart.Prepare(Defaults()); Apply(restart, root, Value(true));
                check(!Read(SettingsPath(root, "Pathfinding_Serp")), "deferred import resumes after restart and becoming host");
            }
            {
                string root = next(); Write(SettingsPath(root, "BugfixesAndQoL_Serp"), Value(false));
                var plan = Create(root); plan.Prepare(Defaults());
                check(Reject(() => Apply(plan, root, Value(true), persist: false)), "unconfirmed destination persistence is a failure");
                var retry = Create(root); retry.Prepare(Defaults()); Apply(retry, root, Value(true));
                check(!Read(SettingsPath(root, "Pathfinding_Serp")), "failed persistence leaves the journal pending for restart");
            }
            {
                string root = next(), source = SettingsPath(root, "BugfixesAndQoL_Serp"); Directory.CreateDirectory(Path.GetDirectoryName(source)); File.WriteAllBytes(source, new byte[] { 0xc1 });
                var plan = Create(root); bool failed = false; try { plan.Prepare(Defaults()); } catch { failed = true; }
                check(failed && plan.RegistrationSafe, "bad BFQoL input fails after initial eligibility is safely journaled");
                Write(source, Value(false)); var retry = Create(root); retry.Prepare(Defaults()); Apply(retry, root, Value(true));
                check(!Read(SettingsPath(root, "Pathfinding_Serp")), "corrupt source is retried after repair without recording false completion");
            }
            {
                string root = next(), journal = JournalPath(root); Directory.CreateDirectory(Path.GetDirectoryName(journal)); File.WriteAllText(journal, "{}");
                var plan = Create(root); check(Reject(() => plan.Prepare(Defaults())) && !plan.RegistrationSafe, "invalid journal cannot authorize registration");
            }
            {
                string root = next(), source = SettingsPath(root, "BugfixesAndQoL_Serp"), target = SettingsPath(root, "Pathfinding_Serp");
                Directory.CreateDirectory(Path.GetDirectoryName(source)); File.WriteAllBytes(source, new byte[] { 0xc1 });
                var plan = Create(root); try { plan.Prepare(Defaults()); } catch { }
                check(plan.RegistrationSafe, "source-read failure retains safe registration eligibility");
                Write(target, Value(true)); // Registration materializes defaults; an explicit edit returns to true.
                plan.PreserveEdit(Key);
                Write(source, Value(false)); var retry = Create(root); retry.Prepare(Defaults()); Apply(retry, root, Value(true));
                check(Read(target), "explicit edit while source-read preparation is deferred survives repaired restart, even at the original default");
            }
            {
                string root = next(), source = SettingsPath(root, "BugfixesAndQoL_Serp"), target = SettingsPath(root, "Pathfinding_Serp");
                var old = LegacyBfqolSettings.Keys.ToDictionary(key => key, _ => MessagePackSerializer.Serialize(false));
                old[LegacyBfqolSettings.Keys.Last()] = new byte[] { 1 }; // Valid envelope, invalid primitive boolean after earlier entries.
                Write(source, old); byte[] original = File.ReadAllBytes(source); var plan = Create(root);
                check(Reject(() => plan.Prepare(Defaults())) && plan.RegistrationSafe, "partial source validation fails with a durable eligibility journal");
                plan.PreserveEdit(Key);
                check(original.SequenceEqual(File.ReadAllBytes(source)), "preserving an edit after partial source validation never modifies BFQoL");
                old[LegacyBfqolSettings.Keys.Last()] = MessagePackSerializer.Serialize(false); Write(source, old);
                var retry = Create(root); retry.Prepare(Defaults());
                var working = LegacyBfqolSettings.Keys.ToDictionary(key => key, _ => MessagePackSerializer.Serialize(true));
                Apply(retry, root, working);
                var stored = LegacyBfqolSettings.ReadSettings(target, null);
                check(MessagePackSerializer.Deserialize<bool>(stored[Key]) && stored.Where(entry => entry.Key != Key).All(entry => !MessagePackSerializer.Deserialize<bool>(entry.Value)),
                    "partial Pending state is discarded while unedited eligibility resumes after source repair");
            }
            {
                string root = next(), source = SettingsPath(root, "BugfixesAndQoL_Serp"), journal = JournalPath(root);
                Write(source, Value(false)); var plan = Create(root); plan.Prepare(Defaults()); byte[] original = File.ReadAllBytes(journal);
                using (File.Open(journal, FileMode.Open, FileAccess.Read, FileShare.Read))
                    check(Reject(() => plan.PreserveEdit(Key)), "a failed cancellation-journal replacement reports failure");
                check(original.SequenceEqual(File.ReadAllBytes(journal)), "failed cancellation write preserves the previous valid journal bytes");
            }
            TestBootstrapGate(check, project, next);
            Console.WriteLine("Legacy settings migration fixtures: " + suite);
        }

        private static void TestBootstrapGate(Action<bool, string> check, string project, Func<string> next)
        {
            // Compile the actual production initializer. Only external registration/native services
            // are doubled: a new guard in a copied test implementation would prove nothing.
            string source = File.ReadAllText(Path.Combine(project, "src", "PathfindingPlugin.cs"));
            int start = source.IndexOf("private bool InitializeLibraryRuntime(", StringComparison.Ordinal);
            if (start < 0) throw new Exception("Production bootstrap entry was not found.");
            int body = source.IndexOf('{', start), depth = 1, end = body + 1;
            while (depth != 0 && end < source.Length) { char item = source[end++]; if (item == '{') depth++; else if (item == '}') depth--; }
            if (depth != 0) throw new Exception("Production bootstrap body was not complete.");
            string method = source.Substring(start, end - start);
            string boundary = @"
using System;
namespace APIShared.ModSettings { public static class LobbyModSettingsPresetRegistration {
 public static Action OnRegister; public static void Register(object plugin, object log, string guid, object settings, string xaml, bool logRoutineActivity) { OnRegister(); }
} }
namespace Shared { public static class DebugLogHelper { public static bool DiagnosticsEnabled; public static object ForExternalCode(object log) { return log; } public static bool IsCurrentNativeLibraryVersion() { return true; } public static void LogError(object log,string error) {} } }
namespace Pathfinding {
public class CrusaderLibraryLoadContext {}
public static class FormationFeature { public static void Initialize(object context) {} }
public static class PathfindingNativeCache { public static void Save() {} }
public class RuntimeBoundary { public void InitializeNetwork() {} public void InitializeNative(object context,bool compatible) {} public void InstallEngine(object context) {} public void ApplySettings() {} }
public class MigrationBoundary { public Func<bool> Safe; public bool RegistrationSafe { get { return Safe(); } } }
public class PathfindingPlugin {
 private MigrationBoundary legacySettings; private RuntimeBoundary runtime = new RuntimeBoundary(); private object Logger,Settings; private bool settingsRegistered; private const string PluginGuid = ""Pathfinding_Serp"";
 private Action prepare; private void PrepareLegacySettings() { prepare(); } private void LogMigrationError(Exception error) {} private void TryMigrateLegacySettings() {}
 public bool Run(Action prepare, Func<bool> safe, Action register, bool exists) { this.prepare=prepare; legacySettings=exists?new MigrationBoundary{Safe=safe}:null; APIShared.ModSettings.LobbyModSettingsPresetRegistration.OnRegister=register; return InitializeLibraryRuntime(new CrusaderLibraryLoadContext()); }
" + method + " } }";
            using (var compiler = new CSharpCodeProvider())
            {
                var options = new CompilerParameters(new[] { "System.dll", "System.Core.dll" }) { GenerateInMemory = true, GenerateExecutable = false };
                CompilerResults compiled = compiler.CompileAssemblyFromSource(options, boundary);
                if (compiled.Errors.HasErrors) throw new Exception(string.Join("\n", compiled.Errors.Cast<CompilerError>().Where(e => !e.IsWarning).Select(e => e.ToString())));
                Type type = compiled.CompiledAssembly.GetType("Pathfinding.PathfindingPlugin"); MethodInfo run = type.GetMethod("Run");
                string root = next(), blocked = Path.Combine(root, "SHCDE", "Pathfinding"); Directory.CreateDirectory(Path.GetDirectoryName(blocked)); File.WriteAllText(blocked, "blocks journal directory");
                Write(SettingsPath(root, "BugfixesAndQoL_Serp"), Value(false)); var plan = Create(root); int registrations = 0;
                Action register = () => { registrations++; Write(SettingsPath(root, "Pathfinding_Serp"), Value(true)); };
                bool success = (bool)run.Invoke(Activator.CreateInstance(type), new object[] { (Action)(() => plan.Prepare(Defaults())), (Func<bool>)(() => plan.RegistrationSafe), register, true });
                check(!success && registrations == 0 && !File.Exists(SettingsPath(root, "Pathfinding_Serp")), "actual bootstrap blocks default materialization when initial journal cannot be saved");
                File.Delete(blocked); var retry = Create(root);
                success = (bool)run.Invoke(Activator.CreateInstance(type), new object[] { (Action)(() => retry.Prepare(Defaults())), (Func<bool>)(() => retry.RegistrationSafe), register, true });
                Apply(retry, root, Value(true));
                check(success && registrations == 1 && !Read(SettingsPath(root, "Pathfinding_Serp")), "repaired restart registers once and imports original false despite default materialization");
                success = (bool)run.Invoke(Activator.CreateInstance(type), new object[] { (Action)(() => { throw new IOException("before candidate creation"); }), (Func<bool>)(() => true), register, false });
                check(!success && registrations == 1, "actual bootstrap also fails closed when migration candidate creation fails");
                string corruptRoot = next(), corruptTarget = SettingsPath(corruptRoot, "Pathfinding_Serp");
                Directory.CreateDirectory(Path.GetDirectoryName(corruptTarget)); byte[] corrupt = { 0xc1 }; File.WriteAllBytes(corruptTarget, corrupt);
                var corruptPlan = Create(corruptRoot);
                success = (bool)run.Invoke(Activator.CreateInstance(type), new object[] { (Action)(() => corruptPlan.Prepare(Defaults())), (Func<bool>)(() => corruptPlan.RegistrationSafe), register, true });
                check(!success && registrations == 1 && corrupt.SequenceEqual(File.ReadAllBytes(corruptTarget)), "actual bootstrap preserves unreadable own settings without registering defaults");
            }
        }
    }
}
