using System;

namespace SerpsModsHost
{
    internal static class PackPluginDiagnosticMessages
    {
        internal const string ApiSharedGuid = "APIShared_Serp";

        public static string MissingInfrastructure(PackModRecord dependency)
        {
            if (IsApiShared(dependency))
            {
                return $"APIShared is missing or was not loaded: expected {dependency.Guid} v{dependency.Version}. " +
                    "Mods that require APIShared cannot start. Reinstall the complete Serps Mods package and " +
                    "check the earlier BepInEx dependency errors.";
            }

            return $"Required package infrastructure was not loaded: {DisplayName(dependency)} " +
                $"({dependency.Guid}) v{dependency.Version}.";
        }

        public static string InfrastructureVersionMismatch(
            PackModRecord dependency,
            string actualVersion)
        {
            if (IsApiShared(dependency))
            {
                return $"The loaded APIShared version is incompatible with this package: expected " +
                    $"{dependency.Version}, actual {actualVersion}. Reinstall the complete Serps Mods package.";
            }

            return $"Loaded infrastructure version mismatch: {dependency.Guid}, expected " +
                $"{dependency.Version}, actual {actualVersion}.";
        }

        public static string MissingChild(PackModRecord mod, bool apiSharedAvailable)
        {
            string identity = $"{DisplayName(mod)} ({mod.Guid}) v{mod.Version}";
            if (!apiSharedAvailable)
            {
                return $"Packed mod was not loaded by BepInEx: {identity}. APIShared is missing or " +
                    "incompatible and may have caused this mod to be skipped.";
            }

            return $"Packed mod was not loaded by BepInEx: {identity}. APIShared is loaded; check " +
                "the earlier BepInEx messages for another missing dependency or a plugin startup error.";
        }

        public static string MissingChildForScriptExtender(
            PackModRecord mod,
            string installedVersion,
            string minimumVersion,
            string maximumVersion)
        {
            string identity = $"{DisplayName(mod)} ({mod.Guid}) v{mod.Version}";
            if (string.IsNullOrWhiteSpace(maximumVersion))
            {
                return $"Packed mod was not loaded by BepInEx: {identity}. The installed Script Extender " +
                    $"{installedVersion} is too old; this mod requires version {minimumVersion} or newer. " +
                    "Update the SHCDE Script Extender.";
            }

            return $"Packed mod was not loaded by BepInEx: {identity}. The installed Script Extender " +
                $"{installedVersion} is outside this mod's supported range {minimumVersion} to " +
                $"{maximumVersion}. Install a supported SHCDE Script Extender version.";
        }

        public static bool IsApiShared(PackModRecord record)
        {
            return record != null &&
                string.Equals(record.Guid, ApiSharedGuid, StringComparison.OrdinalIgnoreCase);
        }

        private static string DisplayName(PackModRecord record)
        {
            return string.IsNullOrWhiteSpace(record?.Name) ? "Unnamed plugin" : record.Name;
        }
    }
}
