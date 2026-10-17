using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace SavegameMissingMapFixTest
{
    // Independent of Unity component lifetime; also exercised by the desktop tests.
    internal sealed class RuntimeStartup
    {
        private int attempted;
        internal void Subscribe(Action<Action> subscribe, Action initialize, Action<bool> activate,
            Action<string> log, Action<Exception> error)
        {
            log("WAITING: installed APIShared 0.9.2; awaiting game library.");
            subscribe(() =>
            {
                if (Interlocked.Exchange(ref attempted, 1) != 0) return;
                activate(false);
                try
                {
                    initialize();
                    activate(true);
                    log("READY: decoder, encoder, restart menu and isolated confirmation callback validated; permanent hooks active.");
                }
                catch (Exception ex)
                {
                    activate(false);
                    error(ex);
                }
            });
        }

        internal static string ValidateGameAssembly(string managedPath)
        {
            // Unity loads Assembly-CSharp from bytes, so Assembly.Location may be empty.
            if (string.IsNullOrWhiteSpace(managedPath) || !Path.IsPathRooted(managedPath))
                throw new InvalidOperationException("BepInEx ManagedPath is missing or not absolute.");
            string path = Path.Combine(managedPath, "Assembly-CSharp.dll");
            if (!File.Exists(path)) throw new FileNotFoundException("Installed game assembly missing: " + path, path);
            using (var sha = SHA256.Create())
            using (var input = File.OpenRead(path))
            {
                string hash = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
                if (hash != "BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789")
                    throw new InvalidOperationException("Unsupported game assembly at " + path + "; SHA256=" + hash);
            }
            return path;
        }
    }
}
