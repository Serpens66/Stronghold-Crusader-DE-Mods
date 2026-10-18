using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SerpsModsHostDuplicateTests
{
    internal static class BootstrapProbe
    {
        internal static int Run(string hostPath, string game, string manifestPath, string extenderDirectory)
        {
            int apiRequests = 0;
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
                string name = new AssemblyName(args.Name).Name;
                if (name == "APIShared") { apiRequests++; throw new FileNotFoundException("APIShared deliberately absent in bootstrap probe."); }
                foreach (string directory in new[] {
                    Path.Combine(game, "BepInEx", "core"), extenderDirectory,
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
            var info = (Dictionary<string, object>)Shared.DependencyFreeJson.Parse(File.ReadAllText(manifestPath));
            var identity = plugin.GetCustomAttributesData().Single(a => a.AttributeType.FullName == "BepInEx.BepInPlugin");
            if (!Equals(identity.ConstructorArguments[0].Value, info["GUID"]) ||
                !Equals(identity.ConstructorArguments[2].Value, info["Version"]))
                throw new Exception("Compiled host identity/version differs from its manifest.");
            var dependencies = plugin.GetCustomAttributesData().Where(a => a.AttributeType.FullName == "BepInEx.BepInDependency").ToArray();
            var api = dependencies.Single(a => Equals(a.ConstructorArguments[0].Value, "APIShared_Serp"));
            var flag = api.ConstructorArguments[1];
            if (!flag.ArgumentType.IsEnum || Enum.GetName(flag.ArgumentType, flag.Value) != "SoftDependency")
                throw new Exception("Host bootstrap must load with APIShared absent (soft BepInEx dependency).");
            var extender = dependencies.Single(a => Equals(a.ConstructorArguments[0].Value, "000shcdese"));
            if (!Equals(extender.ConstructorArguments[1].Value, info["MinimumScriptExtenderVersion"]))
                throw new Exception("Compiled extender minimum differs from the manifest.");
            var apiRequirements = ((IList)info["Dependencies"]).Cast<Dictionary<string, object>>()
                .Where(d => Equals(d["GUID"], "APIShared_Serp")).ToArray();
            if (apiRequirements.Length != 1 || !Version.TryParse(apiRequirements[0]["MinimumVersion"] as string, out _))
                throw new Exception("Manifest must require exactly one valid APIShared minimum.");
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
