using System;
using BepInEx.Configuration;

namespace BugfixesAndQoL.GateRouting
{
    internal static class GateDiagnosticConfiguration
    {
        internal static bool Read(ConfigFile config, Action<Exception> reportFailure, Action save = null)
        {
            if (save == null) save = config.Save;
            bool autoSave = config.SaveOnConfigSet;
            ConfigEntry<int> migration = null;
            try
            {
                // Persist the disabled switch before acknowledging migration. A failed save
                // must never preserve the old default in this process or skip the next retry.
                config.SaveOnConfigSet = false;
                var detailed = config.Bind("GateMigrationDiagnostics", "DetailedDiagnostics", false,
                    "Detailed gate diagnosis (expensive); restart required. Enable only when investigating a problem.");
                migration = config.Bind("GateMigrationDiagnostics", "DetailedDiagnosticsDefaultsVersion", 0,
                    "Local migration marker for the disabled diagnostic default; do not edit.");
                if (migration.Value >= 1) return detailed.Value;
                detailed.Value = false;
                save();
                migration.Value = 1;
                save();
                return false;
            }
            catch (Exception ex)
            {
                if (migration != null) migration.Value = 0;
                reportFailure(ex);
                return false;
            }
            finally { config.SaveOnConfigSet = autoSave; }
        }
    }
}
