using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace LinuxModding.DetourProbe
{
    internal static class Program
    {
        private const string BridgeTypeName = "LinuxModding.LinuxWorkshopUpdaterBridge";

        private static string gameDirectory;
        private static string pluginDirectory;

        private static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("Usage: LinuxModding.DetourProbe.exe <game-directory> <plugin-dll>");
                return 2;
            }

            gameDirectory = Path.GetFullPath(args[0]);
            string pluginPath = Path.GetFullPath(args[1]);
            pluginDirectory = Path.GetDirectoryName(pluginPath);

            AppDomain.CurrentDomain.AssemblyResolve += ResolveDependency;

            try
            {
                Assembly bepinEx = Assembly.LoadFrom(Path.Combine(gameDirectory, "BepInEx", "core", "BepInEx.dll"));
                Type loggerType = bepinEx.GetType("BepInEx.Logging.ManualLogSource", true);
                object logger = Activator.CreateInstance(loggerType, "LinuxModding.DetourProbe");

                Assembly plugin = Assembly.LoadFrom(pluginPath);
                Type bridgeType = plugin.GetType(BridgeTypeName, true);
                object bridge = Activator.CreateInstance(
                    bridgeType,
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { logger },
                    null);

                MethodInfo install = bridgeType.GetMethod("Install", BindingFlags.Instance | BindingFlags.NonPublic);
                if (install == null)
                    throw new MissingMethodException(BridgeTypeName, "Install");

                install.Invoke(bridge, null);

                var fakeManager = new SHCDESE.API.Components.Archive.MapModManager();
                fakeManager.InvokeUpdater();
                if (fakeManager.OriginalCalled)
                    throw new InvalidOperationException("The original updater ran instead of the Linux detour.");

                Console.WriteLine("PASS: private updater method was intercepted with the runtime reflection signature.");
                GC.KeepAlive(bridge);
                return 0;
            }
            catch (TargetInvocationException ex)
            {
                Console.Error.WriteLine(ex.InnerException ?? ex);
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static Assembly ResolveDependency(object sender, ResolveEventArgs args)
        {
            string fileName = new AssemblyName(args.Name).Name + ".dll";
            string[] searchDirectories =
            {
                Path.Combine(gameDirectory, "BepInEx", "core"),
                Path.Combine(gameDirectory, "Stronghold Crusader Definitive Edition_Data", "Managed"),
                pluginDirectory
            };

            foreach (string directory in searchDirectories)
            {
                string candidate = Path.Combine(directory, fileName);
                if (File.Exists(candidate))
                    return Assembly.LoadFrom(candidate);
            }

            return null;
        }
    }
}

namespace SHCDESE.API.Components.Archive
{
    // The production type is internal as well. This controlled twin verifies that
    // MonoMod accepts the object-based reflection detour used by the compatibility plugin.
    internal sealed class MapModManager
    {
        internal bool OriginalCalled { get; private set; }

        internal void InvokeUpdater()
        {
            LaunchUpdaterAndExit();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void LaunchUpdaterAndExit()
        {
            OriginalCalled = true;
        }
    }
}
