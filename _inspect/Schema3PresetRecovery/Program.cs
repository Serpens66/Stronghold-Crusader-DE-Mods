using MessagePack;
using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Schema3PresetRecovery
{
    internal static class Program
    {
        private const string CurrentSettingsKey = "__SerpCurrentSettings";
        private const string SchemaVersionKey = "__SerpPresetSchemaVersion";
        private const string PresetId = "recovered-working-settings";
        private static readonly string GameDirectory =
            @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
        private static readonly Dictionary<string, string> ModAssemblies =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["BugfixesAndQoL_Serp"] = "BugfixesAndQoL.dll",
                ["BuildingCosts_Serp"] = "BuildingCosts.dll",
                ["BuildingLimit_Serp"] = "BuildingLimit.dll",
                ["CastlePlanner_Serp"] = "CastlePlanner.dll",
                ["ExtraFeatures_Serp"] = "ExtraFeatures.dll",
                ["RandomEvents_Serp"] = "RandomEvents.dll",
                ["StartConditions_Serp"] = "StartConditions.dll",
                ["UnitCosts_Serp"] = "UnitCosts.dll",
                ["UnitLimit_Serp"] = "UnitLimit.dll",
            };

        private static int Main(string[] args)
        {
            bool dryRun = args.Any(value => string.Equals(value, "--dry-run", StringComparison.Ordinal));
            string pluginDirectory = Path.Combine(GameDirectory, "BepInEx", "plugins");
            ConfigureAssemblyResolution(pluginDirectory);
            string backupSuffix = ".recovery-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
            var pending = new List<RecoveryOutput>();

            foreach (KeyValuePair<string, string> mod in ModAssemblies)
                pending.Add(Prepare(pluginDirectory, mod.Key, mod.Value, backupSuffix));

            foreach (RecoveryOutput output in pending)
            {
                Console.WriteLine((dryRun ? "DRY-RUN " : "WRITE ") + output.TargetPath +
                    " (" + output.PropertyCount + " settings)");
                if (dryRun)
                    continue;

                Directory.CreateDirectory(Path.GetDirectoryName(output.TargetPath));
                if (File.Exists(output.TargetPath))
                    throw new IOException("Recovery target already exists: " + output.TargetPath);
                File.Copy(output.SettingsPath, output.BackupPath, overwrite: false);
                if (!File.ReadAllBytes(output.SettingsPath).SequenceEqual(File.ReadAllBytes(output.BackupPath)))
                    throw new IOException("Recovery backup verification failed: " + output.BackupPath);

                string temporary = output.TargetPath + ".tmp-" + Guid.NewGuid().ToString("N");
                File.WriteAllText(temporary, output.Json, new UTF8Encoding(false));
                try
                {
                    File.Move(temporary, output.TargetPath);
                }
                finally
                {
                    if (File.Exists(temporary))
                        File.Delete(temporary);
                }
            }

            Console.WriteLine((dryRun ? "Validated" : "Recovered") + " all " + pending.Count + " working-state presets.");
            return 0;
        }

        private static RecoveryOutput Prepare(
            string pluginDirectory,
            string targetGuid,
            string assemblyName,
            string backupSuffix)
        {
            string modDirectory = Path.Combine(pluginDirectory, targetGuid);
            string settingsPath = Path.Combine(modDirectory, "LobbyModSettings", targetGuid + ".msgpack");
            string assemblyPath = Path.Combine(modDirectory, assemblyName);
            if (!File.Exists(settingsPath) || !File.Exists(assemblyPath))
                throw new FileNotFoundException("Installed recovery input is missing for " + targetGuid + ".");

            Dictionary<string, byte[]> root =
                MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(File.ReadAllBytes(settingsPath));
            if (!root.TryGetValue(SchemaVersionKey, out byte[] schemaBytes) ||
                MessagePackSerializer.Deserialize<int>(schemaBytes) != 3 ||
                !root.TryGetValue(CurrentSettingsKey, out byte[] snapshotBytes))
            {
                throw new InvalidDataException(targetGuid + " is not a recoverable schema-3 working snapshot.");
            }

            Dictionary<string, byte[]> snapshot =
                MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(snapshotBytes);
            if (snapshot.Count == 0)
                throw new InvalidDataException(targetGuid + " has no current working settings.");

            Assembly assembly = Assembly.LoadFrom(assemblyPath);
            Type viewModelType = GetLoadableTypes(assembly)
                .Where(IsPresetViewModel)
                .FirstOrDefault(type => snapshot.Keys.All(key =>
                    type.GetProperty(key, BindingFlags.Instance | BindingFlags.Public) != null));
            if (viewModelType == null)
                throw new InvalidDataException("No matching preset ViewModel was found in " + assemblyPath + ".");

            var settings = new Dictionary<string, PublishedPresetSetting>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, byte[]> item in snapshot.OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                PropertyInfo property = viewModelType.GetProperty(
                    item.Key,
                    BindingFlags.Instance | BindingFlags.Public);
                object value = MessagePackSerializer.Deserialize(property.PropertyType, item.Value);
                settings.Add(item.Key, new PublishedPresetSetting
                {
                    Mode = PublishedPresetValueMode.Fixed,
                    Value = ModSettingsPresetJson.ToJsonValue(property.PropertyType, value),
                });
            }

            string targetPath = Path.Combine(
                modDirectory,
                "LobbyModSettings",
                "Presets",
                "Override",
                targetGuid,
                "preset_" + PresetId + ".json");
            if (File.Exists(targetPath))
                throw new IOException("Recovery target already exists and will not be overwritten: " + targetPath);

            string json = ModSettingsPresetJson.Serialize(
                targetGuid,
                PresetId,
                "Wiederhergestellte Einstellungen",
                "Einmalig aus dem noch vorhandenen Schema-3-Arbeitsstand der Testinstallation wiederhergestellt.",
                string.Empty,
                string.Empty,
                settings);
            ModSettingsPresetJson.Parse(json, targetGuid, targetGuid, targetGuid, targetPath);
            return new RecoveryOutput
            {
                SettingsPath = settingsPath,
                BackupPath = settingsPath + backupSuffix,
                TargetPath = targetPath,
                Json = json,
                PropertyCount = settings.Count,
            };
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
        }

        private static bool IsPresetViewModel(Type type)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                if (string.Equals(
                    current.FullName,
                    "Shared.PresetLobbyModSettingsViewModel",
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static void ConfigureAssemblyResolution(string pluginDirectory)
        {
            string managed = Path.Combine(GameDirectory, "Stronghold Crusader Definitive Edition_Data", "Managed");
            string extender = Path.Combine(pluginDirectory, "000shcdese");
            string core = Path.Combine(GameDirectory, "BepInEx", "core");
            AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
            {
                string fileName = new AssemblyName(args.Name).Name + ".dll";
                foreach (string directory in new[] { extender, core, managed, Path.Combine(pluginDirectory, "APIShared_Serp") })
                {
                    string candidate = Path.Combine(directory, fileName);
                    if (File.Exists(candidate))
                        return Assembly.LoadFrom(candidate);
                }
                foreach (string directory in Directory.GetDirectories(pluginDirectory))
                {
                    string candidate = Path.Combine(directory, fileName);
                    if (File.Exists(candidate))
                        return Assembly.LoadFrom(candidate);
                }
                return null;
            };
        }

        private sealed class RecoveryOutput
        {
            internal string SettingsPath { get; set; }
            internal string BackupPath { get; set; }
            internal string TargetPath { get; set; }
            internal string Json { get; set; }
            internal int PropertyCount { get; set; }
        }
    }
}
