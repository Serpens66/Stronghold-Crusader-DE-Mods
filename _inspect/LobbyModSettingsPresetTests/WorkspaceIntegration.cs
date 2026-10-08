using APIShared.ModSettings;
using MessagePack;
using SHCDESE.API.Components.Network;
using APIShared.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace LobbyModSettingsPresetTests
{
    internal static partial class Program
    {
        static partial void TestWorkspacePublishedReleases()
        {
            string workspace = FindWorkspaceRoot();
            var releases = new[]
            {
                new { Mod = "BugfixesAndQoL", Version = "1.0.154", Commit = "f674e9aee" },
                new { Mod = "BuildingCosts", Version = "1.0.106", Commit = "4185561af" },
                new { Mod = "BuildingLimit", Version = "1.0.24", Commit = "9f580bbc7" },
                new { Mod = "CastlePlanner", Version = "0.8.29", Commit = "1a8523d6c" },
                new { Mod = "ExtraFeatures", Version = "1.0.98", Commit = "cf337ec23" },
                new { Mod = "RandomEvents", Version = "1.0.42", Commit = "3729ba298" },
                new { Mod = "StartConditions", Version = "1.0.27", Commit = "b30c09299" },
                new { Mod = "UnitCosts", Version = "1.0.29", Commit = "597b3dbd1" },
                new { Mod = "UnitLimit", Version = "1.0.99", Commit = "4e5980a39" },
            };

            foreach (var release in releases)
            {
                string provenance = Path.Combine(
                    workspace,
                    ".release-output",
                    release.Mod,
                    "v" + release.Version,
                    release.Mod + "-v" + release.Version + ".provenance.json");
                Assert(File.Exists(provenance), $"Published provenance is missing for {release.Mod} {release.Version}.");
                string stagePluginDirectory = Path.Combine(
                    workspace,
                    ".release-output",
                    release.Mod,
                    "v" + release.Version,
                    "stage",
                    release.Mod + "_Serp");
                Assert(
                    File.Exists(Path.Combine(stagePluginDirectory, release.Mod + ".dll")) &&
                    File.Exists(Path.Combine(stagePluginDirectory, "info.json")),
                    $"Published stage baseline is incomplete for {release.Mod} {release.Version}.");
                string provenanceText = File.ReadAllText(provenance);
                Assert(provenanceText.Contains(release.Commit),
                    $"Published provenance commit changed for {release.Mod} {release.Version}.");

                string source = ReadGitFile(
                    workspace,
                    release.Commit,
                    "Shared/PresetLobbyModSettingsViewModel.cs");
                Assert(source.Contains("private const int SchemaVersion = 1;") &&
                        source.Contains("__SerpActivePreset") &&
                        source.Contains("__SerpPreset1") &&
                        source.Contains("__SerpPreset2"),
                    $"Published preset contract changed for {release.Mod} {release.Version} ({release.Commit}).");
            }

            Console.WriteLine($"Validated {releases.Length} published schema-1 preset contracts from release stage provenance.");
        }

        private static string FindWorkspaceRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ".git")) &&
                    Directory.Exists(Path.Combine(directory.FullName, ".release-output")))
                    return directory.FullName;
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("Could not locate the repository root and .release-output baseline.");
        }

        private static string ReadGitFile(string workspace, string commit, string path)
        {
            var start = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "show " + commit + ":" + path,
                WorkingDirectory = workspace,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using (Process process = Process.Start(start))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException($"Could not read release source {commit}:{path}: {error}");
                return output;
            }
        }

    }
}
