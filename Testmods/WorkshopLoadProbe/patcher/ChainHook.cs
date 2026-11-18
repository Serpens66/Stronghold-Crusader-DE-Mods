using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace WorkshopLoadProbe
{
    // Redirects the single plugin discovery call in Chainloader.Start
    // (TypeLoader.FindPluginTypes<PluginInfo>(Paths.PluginPath, ...)) to Discover, which returns
    // the unchanged local result plus the selected Workshop roots.
    //
    // APIShared's SafeIlHook is not available in the preloader. The same protection is applied
    // here: the edit is validated on a copy first, and the live manipulator never throws
    // (MonoMod reruns all manipulators on every later patch change of Chainloader.Start, and a
    // throwing one would drop every IL edit including APIShared.DependencyBootstrap's).
    internal static class ChainHook
    {
        private static ILHook hook;
        private static MethodInfo target;

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Install()
        {
            if (hook != null) return;
            try
            {
                var bepinex = typeof(Chainloader).Assembly.GetName();
                ProbeLog.Info("BEPINEX", bepinex.Name + " " + bepinex.Version + " " + typeof(Chainloader).Assembly.Location);
                var loaded = typeof(Chainloader).GetField("_loaded", BindingFlags.NonPublic | BindingFlags.Static);
                if (loaded == null || (bool)loaded.GetValue(null)) { ProbeLog.Error("HOOK_REFUSED", "Chainloader already ran or contract changed"); return; }
                target = typeof(ChainHook).GetMethod(nameof(Discover), BindingFlags.Static | BindingFlags.NonPublic);
                var method = typeof(Chainloader).GetMethod("Start", BindingFlags.Public | BindingFlags.Static);
                using (var copy = new DynamicMethodDefinition(method))
                using (var context = new ILContext(copy.Definition))
                {
                    Rewrite(context);
                    var compiled = copy.Generate().CreateDelegate(typeof(Action));
                    RuntimeHelpers.PrepareDelegate(compiled);
                    GC.KeepAlive(compiled);
                }
                hook = new ILHook(method, Manipulate);
                ProbeLog.Info("HOOK_INSTALLED", "Chainloader.Start discovery redirected");
            }
            catch (Exception ex) { ProbeLog.Error("HOOK_REFUSED", ex.ToString()); }
        }

        private static void Manipulate(ILContext context)
        {
            try { Rewrite(context); }
            catch (Exception ex) { ProbeLog.Error("HOOK_SKIPPED", "manipulator left Chainloader.Start unchanged: " + ex.Message); }
        }

        // Validates completely before the single operand change, so a failure leaves the body untouched.
        private static void Rewrite(ILContext context)
        {
            var calls = context.Body.Instructions.Where(IsDiscovery).ToArray();
            if (calls.Length != 1) throw new InvalidOperationException("expected one FindPluginTypes<PluginInfo> call, found " + calls.Length);
            var imported = context.Import(target);
            calls[0].Operand = imported;
        }

        private static bool IsDiscovery(Instruction instruction)
        {
            if (instruction.OpCode != OpCodes.Call || !(instruction.Operand is GenericInstanceMethod method)) return false;
            return method.Name == "FindPluginTypes" &&
                method.DeclaringType.FullName == "BepInEx.Bootstrap.TypeLoader" &&
                method.GenericArguments.Count == 1 &&
                method.GenericArguments[0].FullName == "BepInEx.PluginInfo" &&
                method.Parameters.Count == 4;
        }

        // Signature mirrors FindPluginTypes<PluginInfo>; the IL stack is unchanged by the redirect.
        private static Dictionary<string, List<PluginInfo>> Discover(string directory, Func<TypeDefinition, PluginInfo> selector,
            Func<AssemblyDefinition, bool> filter, string cacheName)
        {
            var local = TypeLoader.FindPluginTypes(directory, selector, filter, cacheName);
            try { WorkshopLoader.Merge(local, selector, filter); }
            catch (Exception ex) { ProbeLog.Error("ERROR", "Merge failed, local plugins only: " + ex); }
            return local;
        }
    }
}
