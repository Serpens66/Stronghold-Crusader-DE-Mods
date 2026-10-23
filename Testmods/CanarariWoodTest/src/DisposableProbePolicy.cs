using System;
using System.IO;
using System.Security.Cryptography;
namespace CanarariWoodTest
{
    internal static class DisposableProbePolicy
    {
        internal static bool IsNamedCopy(bool loadedSave, bool editor, bool replay, string path, string expectedName) =>
            loadedSave && !editor && !replay && !string.IsNullOrEmpty(path) &&
            string.Equals(Path.GetFileName(path), expectedName, StringComparison.OrdinalIgnoreCase);
        internal static bool HasExpectedHash(string path, string expected)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""),
                    expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
