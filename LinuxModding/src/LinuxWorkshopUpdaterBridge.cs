using BepInEx;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace LinuxModding
{
    internal sealed class LinuxWorkshopUpdaterBridge
    {
        internal const string LauncherEnvironmentVariable = "SHCDE_LINUX_COMPAT_LAUNCHER";
        internal const string UpdateRequestFileName = ".linux-compat-update-request";

        private const string MapModManagerTypeName = "SHCDESE.API.Components.Archive.MapModManager";
        private const string UpdaterMethodName = "LaunchUpdaterAndExit";

        private delegate void LaunchUpdaterAndExitDelegate(object instance);

        private readonly ManualLogSource log;
        private Hook updaterHook;

        internal LinuxWorkshopUpdaterBridge(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        internal static bool WasStartedByCompatibilityLauncher()
        {
            return string.Equals(
                Environment.GetEnvironmentVariable(LauncherEnvironmentVariable),
                "1",
                StringComparison.Ordinal);
        }

        internal void Install()
        {
            Assembly scriptExtenderAssembly = FindScriptExtenderAssembly();
            Type mapModManagerType = scriptExtenderAssembly.GetType(MapModManagerTypeName, true);
            MethodInfo updaterMethod = mapModManagerType.GetMethod(
                UpdaterMethodName,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);

            if (updaterMethod == null)
                throw new MissingMethodException(MapModManagerTypeName, UpdaterMethodName);

            updaterHook = new Hook(
                updaterMethod,
                (LaunchUpdaterAndExitDelegate)RequestLinuxUpdateAndExit);

            LogInfo("Intercepted Script Extender MapModManager.LaunchUpdaterAndExit().");
        }

        private void RequestLinuxUpdateAndExit(object instance)
        {
            try
            {
                string seDirectory = Path.Combine(Paths.GameRootPath, "_SE");
                string stagingDirectory = Path.Combine(seDirectory, ".staging");
                string requestPath = Path.Combine(seDirectory, UpdateRequestFileName);
                string temporaryPath = requestPath + ".tmp";

                if (!Directory.Exists(stagingDirectory))
                    throw new DirectoryNotFoundException("Script Extender staging directory not found: " + stagingDirectory);

                Directory.CreateDirectory(seDirectory);

                string request =
                    "protocol=1" + Environment.NewLine +
                    "createdUtc=" + DateTime.UtcNow.ToString("O") + Environment.NewLine +
                    "gamePid=" + Process.GetCurrentProcess().Id + Environment.NewLine;

                byte[] bytes = Encoding.UTF8.GetBytes(request);
                using (FileStream stream = new FileStream(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                if (File.Exists(requestPath))
                    File.Delete(requestPath);
                File.Move(temporaryPath, requestPath);

                LogInfo("Workshop update staged; Linux launcher request written. Terminating game for deployment.");
                Process.GetCurrentProcess().Kill();
            }
            catch (Exception ex)
            {
                LogError("Could not hand the staged Workshop update to the Linux launcher: " + ex);
            }
        }

        private static Assembly FindScriptExtenderAssembly()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetType(MapModManagerTypeName, false) != null)
                    return assembly;
            }

            throw new TypeLoadException("Could not locate the loaded SHCDE Script Extender assembly.");
        }

        private void LogInfo(string message)
        {
            log.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }

        private void LogError(string message)
        {
            log.LogError($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }
    }
}
