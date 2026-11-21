using APIShared.Maps;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BugfixesAndQoL
{
    internal static class LongMapFileNames
    {
        private const int WindowsPathLimit = 259;
        private const int WindowsFileNameLimit = 255;

        internal static MapLoadDecision Decide(string originalPath, bool canAlias)
        {
            string fileName = Path.GetFileName(originalPath);
            // Game and Workshop headers carry absolute paths. Check their raw length before
            // normalization, which can itself reject an overlong path on legacy runtimes.
            if (originalPath.Length > WindowsPathLimit)
                return Reject(fileName, "full path", originalPath.Length, WindowsPathLimit, originalPath);
            if (fileName.Length > WindowsFileNameLimit)
                return Reject(fileName, "Windows filename", fileName.Length, WindowsFileNameLimit, originalPath);

            string fullPath = Path.GetFullPath(originalPath);
            if (fullPath.Length > WindowsPathLimit)
                return Reject(fileName, "full path", fullPath.Length, WindowsPathLimit, fullPath);
            if (fileName.Length <= MapLoadPolicy.NativeFileNameLimit)
                return MapLoadDecision.Allow();
            if (!canAlias)
                return Reject(fileName, "native filename (short alias unavailable)", fileName.Length,
                    MapLoadPolicy.NativeFileNameLimit, fullPath);

            using (var sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(Encoding.Unicode.GetBytes(fileName));
                string alias = "Map-" + BitConverter.ToString(digest, 0, 16).Replace("-", "") + ".map";
                return MapLoadDecision.UseNativeFileName(alias);
            }
        }

        private static MapLoadDecision Reject(string name, string subject, int length, int maximum, string path) =>
            MapLoadDecision.Reject($"Cannot load map '{name}': {subject} length {length} exceeds {maximum} UTF-16 code units. Path: {path}");
    }
}
