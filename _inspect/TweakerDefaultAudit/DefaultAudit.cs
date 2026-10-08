using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using R3;
using SHCDESE.EventAPI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

[BepInPlugin("TweakerDefaultAudit_Serp", "Tweaker Default Audit (read-only test)", "1.0.0")]
[BepInDependency("CrusaderDETweaker")]
public sealed class DefaultAudit : BaseUnityPlugin
{
    private static ManualLogSource log;
    private static Type api;
    private static IDisposable mapSubscription;
    private static string previousReport;
    private static bool mapObserved;
    private static readonly BindingFlags InternalStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly BindingFlags InternalInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private void Awake()
    {
        log = Logger;
        try
        {
            var assembly = Chainloader.PluginInfos["CrusaderDETweaker"].Instance.GetType().Assembly;
            api = assembly.GetType("CrusaderDETweaker.Configuration.ConfigurationApi", true);
            api.GetEvent("Changed").AddEventHandler(null, (Action)OnConfigurationChanged);
            mapSubscription = MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(_ =>
            {
                if (mapObserved) return;
                mapObserved = true;
                log.LogInfo("[DefaultAudit] Persistent map event reached after startup cleanup.");
                Audit("map-start");
            });
            OnConfigurationChanged();
        }
        catch (Exception exception) { log.LogError("[DefaultAudit] Initialization failed: " + exception.GetBaseException()); }
    }

    private static void OnConfigurationChanged() => Audit("configuration-event");
    private static object Call(string method) => api.GetMethod(method).Invoke(null, null);
    private static T Get<T>(object instance, string property) => (T)instance.GetType().GetProperty(property).GetValue(instance);

    private static void Audit(string phase)
    {
        try
        {
            if (!Get<bool>(Call("GetCapabilities"), "IsReady")) return;
            // This is the actual output captured before Tweaker applies its configuration.
            // Do not rerun native generators after overrides have already been applied.
            var generated = (Dictionary<string, string>)api.GetField("generated", InternalStatic).GetValue(null);
            object documents = api.GetField("documents", InternalStatic).GetValue(null);
            var files = generated.ToDictionary(pair => pair.Key, pair => Encoding.UTF8.GetBytes(pair.Value));
            Type manager = api.Assembly.GetType("CrusaderDETweaker.Config.BepInEx.BepInExConfigManager", true);
            var diagnostic = (ConfigEntryBase)manager.GetProperty("DebugLogging", InternalStatic).GetValue(null);
            ConfigFile config = diagnostic.ConfigFile;
            var cfg = new StringBuilder();
            foreach (var section in config.GroupBy(pair => pair.Key.Section))
            {
                cfg.AppendLine("[" + section.Key + "]");
                foreach (var pair in section)
                    cfg.AppendLine(pair.Key.Key + " = " + Format(pair.Value.DefaultValue));
            }
            string cfgName = (string)api.GetField("CfgName", InternalStatic).GetRawConstantValue();
            files.Add(cfgName, Encoding.UTF8.GetBytes(cfg.ToString()));
            var read = documents.GetType().GetMethod("Read", InternalInstance);
            var freshValues = (Dictionary<string, object>)read.Invoke(documents, new object[] { files });
            var defaults = ((IEnumerable)Call("GetOptions")).Cast<object>().ToDictionary(
                option => Get<string>(option, "Key"), option => Get<object>(option, "DefaultValue"));
            RequireEqual(freshValues, defaults, "startup generator output and CFG registrations");
            var validation = (Array)api.GetMethod("ValidateConfiguration").Invoke(null, new object[] { defaults });
            if (validation.Length != 0) throw new InvalidDataException("Default candidate validation failed: " + string.Join("; ", validation.Cast<object>()));
            var rendered = documents.GetType().GetMethod("Render", InternalInstance).Invoke(documents, new object[] { files, defaults });
            RequireEqual(defaults, (Dictionary<string, object>)read.Invoke(documents, new[] { rendered }), "default roundtrip");

            string fingerprint = Fingerprint(defaults);
            var own = Get<Dictionary<string, object>>(Call("ReadOwnConfiguration"), "Values");
            var loaded = Get<Dictionary<string, object>>(Call("GetLoadedConfiguration"), "Values");
            object pending = Call("GetPendingConfiguration");
            string report = "PASS options=" + defaults.Count + " files=" + files.Count + " defaults=" + fingerprint +
                " ownDifferences=" + Differences(defaults, own) + " loadedDifferences=" + Differences(defaults, loaded) +
                " pendingDifferences=" + (pending == null ? "none" : Differences(defaults, Get<Dictionary<string, object>>(pending, "Values")).ToString(CultureInfo.InvariantCulture));
            if (report != previousReport || phase == "map-start")
            {
                log.LogInfo("[DefaultAudit] " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + phase + " " + report);
                previousReport = report;
            }
        }
        catch (Exception exception) { log.LogError("[DefaultAudit] " + phase + " FAIL: " + exception.GetBaseException()); }
    }

    private static int Differences(Dictionary<string, object> expected, Dictionary<string, object> actual) =>
        expected.Count(pair => !actual.TryGetValue(pair.Key, out var value) || !Equals(pair.Value, value)) + actual.Keys.Count(key => !expected.ContainsKey(key));
    private static void RequireEqual(Dictionary<string, object> expected, Dictionary<string, object> actual, string context)
    {
        if (Differences(expected, actual) != 0) throw new InvalidDataException("Default mismatch: " + context);
    }
    private static string Format(object value) => value is bool flag ? (flag ? "true" : "false") : Convert.ToString(value, CultureInfo.InvariantCulture);
    private static string Fingerprint(Dictionary<string, object> values)
    {
        string text = string.Join("\n", values.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair =>
            pair.Key.Length + ":" + pair.Key + "|" + pair.Value.GetType().FullName + "|" + Format(pair.Value)));
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
    }
}
