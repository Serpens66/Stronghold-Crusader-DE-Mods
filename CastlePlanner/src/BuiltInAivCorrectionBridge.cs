using BepInEx.Bootstrap;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace CastlePlanner
{
    internal static class BuiltInAivCorrectionBridge
    {
        private const string BugfixesGuid = "BugfixesAndQoL_Serp";
        private static readonly object fileWrite = new object();
        private static Func<int, int, short[]> readCorrection;

        internal static string ResolveFile(string asset, string originalPath)
        {
            string key = Path.GetFileNameWithoutExtension(asset);
            int indexSeparator = key.LastIndexOf('_');
            if (indexSeparator < 0 || !int.TryParse(key.Substring(indexSeparator + 1),
                NumberStyles.None, CultureInfo.InvariantCulture, out int variant))
                return originalPath;
            string text = GetText(key.Substring(0, indexSeparator), variant);
            if (text == null) return originalPath;

            var utf8 = new UTF8Encoding(false);
            string identity;
            using (SHA256 sha = SHA256.Create())
                identity = BitConverter.ToString(sha.ComputeHash(utf8.GetBytes(text))).Replace("-", string.Empty);
            string directory = Path.Combine(Path.GetTempPath(), CastlePlannerPlugin.PluginGuid, "BuiltInAivCorrections");
            string path = Path.Combine(directory, $"{key}-{identity}.aivjson");
            lock (fileWrite)
            {
                Directory.CreateDirectory(directory);
                if (!File.Exists(path) || !string.Equals(File.ReadAllText(path), text, StringComparison.Ordinal))
                    File.WriteAllText(path, text, utf8);
            }
            return path;
        }

        internal static string GetText(string lordName, int variant)
        {
            if (!Enum.TryParse(lordName, true, out Enums.AILords lord) || !Enum.IsDefined(typeof(Enums.AILords), lord))
                return null;
            if (readCorrection == null)
            {
                if (!Chainloader.PluginInfos.TryGetValue(BugfixesGuid, out var plugin) ||
                    ReferenceEquals(plugin.Instance, null))
                    return null;
                MethodInfo method = plugin.Instance.GetType().GetMethod("GetBuiltInAivCorrection",
                    BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(int), typeof(int) }, null);
                if (method?.ReturnType != typeof(short[]))
                    return null;
                readCorrection = (Func<int, int, short[]>)method.CreateDelegate(typeof(Func<int, int, short[]>));
            }

            short[] raw = readCorrection((int)lord - 1, variant);
            if (raw == null) return null;
            var document = AivSpawnPlan.Decode(raw);
            return Shared.DependencyFreeJson.Serialize(new
            {
                document.pauseDelayAmount,
                frames = document.frames.Select(frame => new { frame.itemType, frame.tilePositionOfsets, frame.shouldPause }).ToArray(),
                miscItems = document.miscItems.Select(item => new { item.itemType, item.positionOfset, item.number }).ToArray()
            });
        }
    }
}
