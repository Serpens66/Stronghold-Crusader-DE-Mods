using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;

namespace WorkshopLoadProbe
{
    // Lives in BepInEx/plugins. Proves load order across both folders (soft dependency on the
    // Workshop payload) and runs T4: late Script Extender asset registration of Workshop roots.
    // All work is synchronous in Awake; the only later callback is the OnStartMap subscription,
    // rooted by the extender's static R3 hook and therefore unaffected by the startup cleanup.
    [BepInPlugin(Guid, "Workshop Load Probe", "0.1.0")]
    [BepInDependency("000shcdese", "2.14.0")]
    [BepInDependency(PayloadGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class ObserverPlugin : BaseUnityPlugin
    {
        public const string Guid = "WorkshopLoadProbe_Serp";
        public const string PayloadGuid = "WorkshopLoadProbePayload_Serp";
        private const string PatchedXaml = "Assets/GUI/XAML/FrontendMenus.xaml";
        private static ManualLogSource log;
        private static bool subscribed;

        private void Awake()
        {
            log = Logger;
            Mark("OBSERVER_AWAKE", "location=" + Info.Location);
            try { Observe(); }
            catch (Exception ex) { log.LogError(Stamp() + " WLP OBSERVER_ERROR " + ex); }
        }

        private static void Observe()
        {
            if (Chainloader.PluginInfos.TryGetValue(PayloadGuid, out PluginInfo payload))
                Mark("ORDER", "payload loaded before observer=" + (payload.Instance != null) + " location=" + payload.Location);
            else Mark("ORDER", "payload not loaded");

            string[] roots = AppDomain.CurrentDomain.GetData("WorkshopLoadProbe.Roots") as string[];
            if (roots == null || roots.Length == 0) { Mark("T4_SKIPPED", "no Workshop roots loaded"); return; }

            var manager = GameAssetModManager.Instance;
            string hashBefore = ModHash();
            int patchesBefore = PatchCount();
            string localesBefore = Locales();
            foreach (string root in roots)
                foreach (string directory in Directory.GetDirectories(root).Where(d => File.Exists(Path.Combine(d, "info.json"))).Concat(
                    File.Exists(Path.Combine(root, "info.json")) ? new[] { root } : new string[0]))
                {
                    var watch = Stopwatch.StartNew();
                    manager.RegisterAssetMod(directory);
                    var entry = manager.GetRegisteredAssetDirectories().FirstOrDefault(pair =>
                        string.Equals(Path.GetFullPath(pair.Value), Path.GetFullPath(directory), StringComparison.OrdinalIgnoreCase));
                    Mark("T4_REGISTER", directory + " registered=" + (entry.Key != null) + " guid=" + entry.Key?.GUID + " ms=" + watch.ElapsedMilliseconds);
                }
            Mark("T4_MODHASH", "before=" + hashBefore + " after=" + ModHash());
            Mark("T4_PATCHES", PatchedXaml + " indexedBefore=" + patchesBefore + " indexedAfter=" + PatchCount());
            Mark("T4_LOCALES", "before=[" + localesBefore + "] after=[" + Locales() + "]");
            Mark("T4_LUA", "pending=" + string.Join(",", LuaGuids()));
            Mark("T4_MODLIST", "payloadListed=" + manager.GetRegisteredAssetDirectories().Any(pair => pair.Key.GUID == PayloadGuid));

            if (!subscribed)
            {
                subscribed = true;
                MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(OnStartMap);
            }
        }

        private static void OnStartMap(MapStartEventArgs args)
        {
            try
            {
                if (args.Phase != EventHookPhase.Post) return;
                Mark("MAP_START", "payloadRegistered=" + GameAssetModManager.Instance.IsRegistered(PayloadGuid) +
                    " luaPending=" + string.Join(",", LuaGuids()));
            }
            catch (Exception ex) { log?.LogError(Stamp() + " WLP MAP_START_ERROR " + ex.Message); }
        }

        // Internal extender members, read by reflection because this is test-only observation.
        private static string ModHash()
        {
            var method = typeof(GameNetworkAPI).GetMethod("ComputeActiveModHash", BindingFlags.Static | BindingFlags.NonPublic);
            return method == null ? "unavailable" : (string)method.Invoke(null, null);
        }

        private static int PatchCount()
        {
            var method = typeof(GameAssetManagerAPI).GetMethod("GetPatchesForPath", BindingFlags.Instance | BindingFlags.NonPublic);
            return method == null ? -1 : ((ICollection)method.Invoke(GameAssetManagerAPI.Instance, new object[] { PatchedXaml })).Count;
        }

        private static IEnumerable<string> LuaGuids()
        {
            var method = typeof(GameAssetModManager).GetMethod("GetPendingLuaMods", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) return new[] { "unavailable" };
            return ((IEnumerable)method.Invoke(GameAssetModManager.Instance, null)).Cast<object>()
                .Select(pair => (string)pair.GetType().GetProperty("Key").GetValue(pair, null)).ToArray();
        }

        private static string Locales()
        {
            var api = GameTranslateAPI.Instance;
            return string.Join(" ", api.GetLoadedLocales().OrderBy(l => l, StringComparer.Ordinal)
                .Select(locale => locale + "=" + api.GetLocaleTranslationCount(locale)).ToArray());
        }

        private static void Mark(string marker, string detail) => log?.LogInfo(Stamp() + " WLP " + marker + " " + detail);

        private static string Stamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }
}
