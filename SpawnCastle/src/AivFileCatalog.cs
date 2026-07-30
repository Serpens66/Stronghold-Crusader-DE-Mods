using BepInEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SpawnCastle
{
    internal sealed class AivFileCatalog
    {
        private readonly Dictionary<string, string> pathByOption =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<string> Discover()
        {
            pathByOption.Clear();

            string pluginDirectory =
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ??
                string.Empty;

            AddRoot("Mod", Path.Combine(pluginDirectory, "AIV"));
            AddLocalLordRoots();
            AddRoot(
                "Editor",
                Path.Combine(
                    Paths.GameRootPath + " - Castle & CPU Lord Editor",
                    "CrusaderCastleEditorUnity_Data",
                    "StreamingAssets",
                    "Villages"));

            return pathByOption.Keys
                .OrderBy(option => option, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public bool TryResolve(string option, out string fullPath)
        {
            return pathByOption.TryGetValue(option ?? string.Empty, out fullPath) &&
                   File.Exists(fullPath);
        }

        private void AddLocalLordRoots()
        {
            string localAppData =
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            DirectoryInfo appData = Directory.GetParent(localAppData);
            if (appData == null)
                return;

            string gameDataRoot = Path.Combine(
                appData.FullName,
                "LocalLow",
                "Firefly Studios",
                "Stronghold Crusader Definitive Edition");

            AddRoot("CustomLords", Path.Combine(gameDataRoot, "CustomLords"));
            AddRoot("ExtendedLords", Path.Combine(gameDataRoot, "ExtendedLords"));
        }

        private void AddRoot(string sourceName, string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return;

            string[] files;
            try
            {
                files = Directory.GetFiles(root, "*.aivjson", SearchOption.AllDirectories);
            }
            catch
            {
                return;
            }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
            {
                string relativePath = MakeRelativePath(root, file);
                string option = $"[{sourceName}] {relativePath}";
                pathByOption[option] = file;
            }
        }

        private static string MakeRelativePath(string root, string file)
        {
            string normalizedRoot =
                Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            Uri rootUri = new Uri(normalizedRoot, UriKind.Absolute);
            Uri fileUri = new Uri(Path.GetFullPath(file), UriKind.Absolute);
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString())
                .Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
