using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SerpsModsHostDuplicateTests
{
    internal static class BootstrapProbe
    {
        internal static int Run(string hostPath, string game)
        {
            int apiRequests = 0;
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
                string name = new AssemblyName(args.Name).Name;
                if (name == "APIShared") { apiRequests++; throw new FileNotFoundException("APIShared deliberately absent in bootstrap probe."); }
                foreach (string directory in new[] {
                    Path.Combine(game, "BepInEx", "core"), Path.Combine(game, "BepInEx", "plugins", "000shcdese"),
                    Path.Combine(game, "Stronghold Crusader Definitive Edition_Data", "Managed") })
                {
                    string path = Path.Combine(directory, name + ".dll");
                    if (File.Exists(path)) return Assembly.Load(File.ReadAllBytes(path));
                }
                return null;
            };
            var host = Assembly.LoadFile(Path.GetFullPath(hostPath));
            Type plugin = host.GetType("SerpsModsHost.SerpsModsHostPlugin", true);
            if (!plugin.IsPublic || plugin.BaseType.FullName != "BepInEx.BaseUnityPlugin") throw new Exception("Plugin identity changed.");
            RuntimeHelpers.PrepareMethod(plugin.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).MethodHandle);
            var viewModel = host.GetType("SerpsModsHost.MissingApiViewModel", true);
            object instance = Activator.CreateInstance(viewModel, new object[] { "APIShared separately subscribe / separat abonnieren" });
            if ((string)viewModel.GetProperty("Message").GetValue(instance) != "APIShared separately subscribe / separat abonnieren" ||
                viewModel.GetProperty("OpenWorkshopCommand").GetValue(instance) == null || apiRequests != 0)
                throw new Exception("Missing-APIShared diagnostic requires APIShared.");
            Console.WriteLine("PASS: compiled host entry JIT and diagnostic view model load with APIShared absent; installer and game entry are not executed.");
            return 0;
        }
    }
}
