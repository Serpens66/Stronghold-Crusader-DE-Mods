using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace WorkshopLoadProbe
{
    // P2: helper DLLs next to a Workshop plugin. Registered after BepInEx's own handler, so it
    // only runs when that one failed. With resolver=off it logs what it would have resolved and
    // returns null, which shows whether BepInEx resolves such DLLs on its own.
    internal static class Resolver
    {
        private static readonly object gate = new object();
        private static readonly Dictionary<string, string> index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Assembly> loaded = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool active;
        private static bool registered;

        internal static void Register(List<string> roots, bool resolve)
        {
            lock (gate)
            {
                active = resolve;
                foreach (string root in roots)
                    foreach (string file in Directory.GetFiles(root, "*.dll", SearchOption.AllDirectories))
                    {
                        string name = Path.GetFileNameWithoutExtension(file);
                        if (index.ContainsKey(name)) ProbeLog.Warning("RESOLVER_DUPLICATE", name + " " + file);
                        else index[name] = file;
                    }
                if (!registered) { AppDomain.CurrentDomain.AssemblyResolve += Resolve; registered = true; }
            }
            ProbeLog.Info("RESOLVER", "active=" + resolve + " assemblies=" + index.Count);
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            try
            {
                string name = new AssemblyName(args.Name).Name;
                lock (gate)
                {
                    if (!index.TryGetValue(name, out string path)) return null;
                    if (loaded.TryGetValue(name, out Assembly existing)) return existing;
                    if (reported.Add(name))
                        ProbeLog.Info("RESOLVE", (active ? "resolved " : "would resolve ") + name + " -> " + path +
                            " requester=" + (args.RequestingAssembly?.GetName().Name ?? "?"));
                    if (!active) return null;
                    var assembly = Assembly.LoadFile(path);
                    loaded[name] = assembly;
                    return assembly;
                }
            }
            catch (Exception ex) { ProbeLog.Error("RESOLVE_FAILED", args.Name + " " + ex.Message); return null; }
        }
    }
}
