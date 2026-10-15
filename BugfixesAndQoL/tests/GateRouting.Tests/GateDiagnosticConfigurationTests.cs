using System;
using System.IO;
using BepInEx.Configuration;

namespace BugfixesAndQoL.GateRouting
{
    internal static class GateDiagnosticConfigurationTests
    {
        internal static int Run()
        {
            int checks = 0;
            Action<bool, string> check = (ok, message) => { checks++; if (!ok) throw new Exception(message); };
            string directory = Path.Combine(Path.GetTempPath(), "gate-config-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            foreach (bool legacy in new[] { false, true })
            {
                string path = Path.Combine(directory, legacy ? "legacy.cfg" : "new.cfg");
                if (legacy) File.WriteAllText(path, "[GateMigrationDiagnostics]\r\nDetailedDiagnostics = true\r\n");
                var config = new ConfigFile(path, false);
                check(!GateDiagnosticConfiguration.Read(config, ex => { throw new Exception("Unexpected save failure", ex); }), "First startup disables diagnosis");
                check(config.SaveOnConfigSet, "Autosave restored");
                config = new ConfigFile(path, false);
                check(!GateDiagnosticConfiguration.Read(config, ex => { throw ex; }), "Repeated startup stays disabled");
                config.Bind("GateMigrationDiagnostics", "DetailedDiagnostics", false, "").Value = true;
                config = new ConfigFile(path, false);
                check(GateDiagnosticConfiguration.Read(config, ex => { throw ex; }), "Later explicit opt-in is preserved");
            }
            string blocked = Path.Combine(directory, "blocked.cfg");
            File.WriteAllText(blocked, "[GateMigrationDiagnostics]\r\nDetailedDiagnostics = true\r\n");
            var failed = new ConfigFile(blocked, false);
            int failures = 0;
            using (var locked = File.Open(blocked, FileMode.Open, FileAccess.Read, FileShare.Read))
                check(!GateDiagnosticConfiguration.Read(failed, ex => failures++), "Save failure keeps runtime diagnosis off");
            check(failures == 1 && failed.SaveOnConfigSet, "Save failure reported and autosave restored");
            check(failed.Bind("GateMigrationDiagnostics", "DetailedDiagnosticsDefaultsVersion", 0, "").Value == 0, "Failed migration remains pending");
            failed = new ConfigFile(blocked, false);
            check(!GateDiagnosticConfiguration.Read(failed, ex => { throw ex; }), "Next process retries migration");
            check(failed.Bind("GateMigrationDiagnostics", "DetailedDiagnosticsDefaultsVersion", 0, "").Value == 1, "Retry persisted marker");
            string secondPath = Path.Combine(directory, "second-save.cfg");
            var second = new ConfigFile(secondPath, false);
            int saves = 0;
            check(!GateDiagnosticConfiguration.Read(second, ex => failures++, () => {
                if (++saves == 2) throw new IOException("Injected marker save failure");
                second.Save();
            }), "Marker save failure keeps diagnosis off");
            check(saves == 2 && second.Bind("GateMigrationDiagnostics", "DetailedDiagnosticsDefaultsVersion", 0, "").Value == 0, "Marker is reset after second save failure");
            var retry = new ConfigFile(secondPath, false);
            check(!GateDiagnosticConfiguration.Read(retry, ex => { throw ex; }) && retry.Bind("GateMigrationDiagnostics", "DetailedDiagnosticsDefaultsVersion", 0, "").Value == 1, "Second-save failure retries from disk");
            Console.WriteLine("Config fixtures retained: " + directory);
            return checks;
        }
    }
}
