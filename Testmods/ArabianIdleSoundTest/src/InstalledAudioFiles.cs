using System;
using System.IO;

namespace ArabianIdleSoundTest
{
    // Unity can load Assembly-CSharp from bytes, leaving Assembly.Location empty.
    // Resolve the installed files exclusively from BepInEx's game root.
    internal sealed class InstalledAudioFiles
    {
        internal readonly string Native, Managed, Backend;
        internal InstalledAudioFiles(string gameRoot)
        {
            if (string.IsNullOrWhiteSpace(gameRoot)) throw new ArgumentException("BepInEx game root is unavailable.", nameof(gameRoot));
            string root = Path.GetFullPath(gameRoot);
            Native = Path.Combine(root, "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
            Managed = Path.Combine(root, "Stronghold Crusader Definitive Edition_Data", "Managed", "Assembly-CSharp.dll");
            Backend = Path.Combine(root, "BepInEx", "plugins", "000shcdese", "RedBird.Backends.NativeX64.dll");
        }
        internal void Validate()
        {
            ValidateFile(Native, NativeContract.NativeHash);
            ValidateFile(Managed, NativeContract.ManagedHash);
            ValidateFile(Backend, NativeContract.BackendHash);
        }
        internal static void ValidateFile(string path, string expectedHash)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Required installed audio binary is missing.", path);
            if (NativeContract.HashFile(path) != expectedHash)
                throw new InvalidOperationException("Binary identity changed; re-audit native layout, IL and backend: " + path);
        }
    }
}
