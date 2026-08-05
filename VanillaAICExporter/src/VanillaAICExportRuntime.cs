using BepInEx;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.AICDecoder;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AILordSlot = SHCDESE.Interop.AILords;

namespace VanillaAICExporter
{
    internal sealed class VanillaAICExportRuntime
    {
        private static readonly LordDefinition[] OfficialLordSlots =
        {
            new LordDefinition(AILordSlot.SK_RAT, "Rat"),
            new LordDefinition(AILordSlot.SK_SNAKE, "Snake"),
            new LordDefinition(AILordSlot.SK_PIG, "Pig"),
            new LordDefinition(AILordSlot.SK_WOLF, "Wolf"),
            new LordDefinition(AILordSlot.SK_SALADIN, "Saladin"),
            new LordDefinition(AILordSlot.SK_CALIPH, "Caliph"),
            new LordDefinition(AILordSlot.SK_SULTAN, "Sultan"),
            new LordDefinition(AILordSlot.SK_RICHARD, "Richard"),
            new LordDefinition(AILordSlot.SK_FREDERICK, "Frederick"),
            new LordDefinition(AILordSlot.SK_PHILLIP, "Philip"),
            new LordDefinition(AILordSlot.SK_WAZIR, "Wazir"),
            new LordDefinition(AILordSlot.SK_EMIR, "Emir"),
            new LordDefinition(AILordSlot.SK_NIZAR, "Nizar"),
            new LordDefinition(AILordSlot.SK_SHERIFF, "Sheriff"),
            new LordDefinition(AILordSlot.SK_MARSHAL, "Marshal"),
            new LordDefinition(AILordSlot.SK_ABBOT, "Abbot"),
            new LordDefinition(AILordSlot.SK_JEWEL, "Jewel"),
            new LordDefinition(AILordSlot.SK_SENTINEL, "Sentinel"),
            new LordDefinition(AILordSlot.SK_NOMAD, "Nomad"),
            new LordDefinition(AILordSlot.SK_KAHIN, "Kahinah"),
            new LordDefinition(AILordSlot.SK_CANARY, "Canary"),
            new LordDefinition(AILordSlot.SK_TRADER, "Trader"),
            new LordDefinition(AILordSlot.SK_SERGEANT, "Sergeant"),
            new LordDefinition(AILordSlot.SK_LIONESS, "Lioness"),
            new LordDefinition(AILordSlot.SK_CROCODILE, "Crocodile"),
            new LordDefinition(AILordSlot.SK_BALDWIN, "Baldwin"),
            new LordDefinition(AILordSlot.SK_BULLSEYE, "Bullseye"),
            // The current game names the two formerly reserved numeric slots Surgeon and Baibars;
            // the local Script Extender enum still exposes their stable values as DLC4A/DLC4B.
            new LordDefinition(AILordSlot.SK_DLC4A, "Surgeon"),
            new LordDefinition(AILordSlot.SK_DLC4B, "Baibars")
        };

        private readonly ManualLogSource log;

        public VanillaAICExportRuntime(ManualLogSource log)
        {
            this.log = log;
        }

        public void Export()
        {
            string pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string requestPath = Path.Combine(pluginDirectory, "export_requested.txt");
            if (!File.Exists(requestPath))
            {
                Shared.DebugLogHelper.LogInfo(log, "No one-shot export request found; exporter remains inactive.");
                return;
            }

            IntPtr arrayAddress = GameAIManagerAPI.Instance.GetAICArray().GetArrayAddress();
            if (arrayAddress == IntPtr.Zero)
                throw new InvalidOperationException("The Script Extender returned a null AIC array address.");

            string nativeLibraryPath = FindNativeLibrary();
            string steamBuildId = GetSteamBuildId();
            string version = GetSafeVersion(nativeLibraryPath, steamBuildId);
            string runName = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + "_game-" + version;
            string outputDirectory = Path.Combine(pluginDirectory, "Exports", runName);
            Directory.CreateDirectory(outputDirectory);

            int structSize = Marshal.SizeOf<InternalAIC>();
            var exportedLords = new List<string>();
            var skippedSlots = new List<string>();
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = null
            };

            foreach (LordDefinition definition in OfficialLordSlots)
            {
                IntPtr source = IntPtr.Add(arrayAddress, checked((int)definition.Slot * structSize));
                InternalAIC internalAic = Marshal.PtrToStructure<InternalAIC>(source);

                // Reserved DLC slots are present in the enum before their actual AIC data ships.
                if (!HasAicData(internalAic))
                {
                    skippedSlots.Add(definition.Name);
                    continue;
                }

                PublicAIC publicAic = PublicAIC.FromInternal(internalAic);
                string json = JsonSerializer.Serialize(new LordJsonRoot { lord = publicAic }, jsonOptions);
                string outputPath = Path.Combine(outputDirectory, definition.Name + ".lordjson");
                WriteUtf8CrLf(outputPath, json);
                exportedLords.Add(definition.Name);
                Shared.DebugLogHelper.LogInfo(log, $"Exported vanilla AIC slot {definition.Slot} to {outputPath}.");
            }

            var manifest = new ExportManifest
            {
                exporterVersion = VanillaAICExporterPlugin.PluginVersion,
                exportedAtLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture),
                steamBuildId = steamBuildId,
                gameExecutableVersion = GetExecutableVersion(),
                nativeLibraryPath = nativeLibraryPath,
                nativeLibraryFileVersion = FileVersionInfo.GetVersionInfo(nativeLibraryPath).FileVersion ?? string.Empty,
                nativeLibrarySha256 = ComputeSha256(nativeLibraryPath),
                exportedLords = exportedLords.ToArray(),
                skippedEmptyOfficialSlots = skippedSlots.ToArray(),
                note = "SK_X1 through SK_X8 and SK_TEMP are runtime/custom slots and are intentionally excluded."
            };

            WriteUtf8CrLf(
                Path.Combine(outputDirectory, "manifest.json"),
                JsonSerializer.Serialize(manifest, jsonOptions));

            // The launcher waits for this marker and then copies the completed directory into the workspace.
            WriteUtf8CrLf(Path.Combine(pluginDirectory, "last_export.txt"), outputDirectory);
            File.Delete(requestPath);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Vanilla AIC export complete: {exportedLords.Count} lordjson files in {outputDirectory}.");
        }

        private static bool HasAicData(InternalAIC data)
        {
            return data.opponent_type != 0 || data.lord_gfx_type != 0 || data.lord_hps_percent != 0;
        }

        private static string FindNativeLibrary()
        {
            string path = Path.Combine(
                Paths.GameRootPath,
                "Stronghold Crusader Definitive Edition_Data",
                "Plugins",
                "x86_64",
                "CrusaderDE.dll");

            if (!File.Exists(path))
                throw new FileNotFoundException("Could not locate the currently installed CrusaderDE.dll.", path);

            return path;
        }

        private static string GetSafeVersion(string nativeLibraryPath, string steamBuildId)
        {
            string version = FileVersionInfo.GetVersionInfo(nativeLibraryPath).FileVersion;
            if (string.IsNullOrWhiteSpace(version))
                version = string.IsNullOrWhiteSpace(steamBuildId) ? "unknown" : "steam-" + steamBuildId;

            foreach (char invalid in Path.GetInvalidFileNameChars())
                version = version.Replace(invalid, '_');

            return version.Replace(' ', '_');
        }

        private static string GetSteamBuildId()
        {
            DirectoryInfo gameDirectory = new DirectoryInfo(Paths.GameRootPath);
            string steamAppsDirectory = gameDirectory.Parent?.Parent?.FullName;
            if (string.IsNullOrEmpty(steamAppsDirectory))
                return string.Empty;

            string appManifestPath = Path.Combine(steamAppsDirectory, "appmanifest_3024040.acf");
            if (!File.Exists(appManifestPath))
                return string.Empty;

            Match match = Regex.Match(
                File.ReadAllText(appManifestPath),
                "\"buildid\"\\s+\"(?<id>\\d+)\"",
                RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["id"].Value : string.Empty;
        }

        private static string GetExecutableVersion()
        {
            string executablePath = Path.Combine(Paths.GameRootPath, "Stronghold Crusader Definitive Edition.exe");
            return File.Exists(executablePath)
                ? FileVersionInfo.GetVersionInfo(executablePath).FileVersion ?? string.Empty
                : string.Empty;
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static void WriteUtf8CrLf(string path, string content)
        {
            string normalized = (content ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace("\n", "\r\n");
            if (!normalized.EndsWith("\r\n", StringComparison.Ordinal))
                normalized += "\r\n";

            File.WriteAllText(path, normalized, new UTF8Encoding(false));
        }

        private sealed class LordJsonRoot
        {
            public PublicAIC lord { get; set; }
        }

        private sealed class ExportManifest
        {
            public string exporterVersion { get; set; }
            public string exportedAtLocal { get; set; }
            public string steamBuildId { get; set; }
            public string gameExecutableVersion { get; set; }
            public string nativeLibraryPath { get; set; }
            public string nativeLibraryFileVersion { get; set; }
            public string nativeLibrarySha256 { get; set; }
            public string[] exportedLords { get; set; }
            public string[] skippedEmptyOfficialSlots { get; set; }
            public string note { get; set; }
        }

        private readonly struct LordDefinition
        {
            public LordDefinition(AILordSlot slot, string name)
            {
                Slot = slot;
                Name = name;
            }

            public AILordSlot Slot { get; }
            public string Name { get; }
        }
    }
}
