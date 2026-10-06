$ErrorActionPreference = 'Stop'
$review = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\Fremde Mods\crusader-de-tweaker-api-review'))
function Save([string]$relative, [string]$content) {
    $content = $content.Replace("`r`n", "`n").Replace("`n", "`r`n")
    $path = Join-Path $review $relative
    [IO.File]::WriteAllText($path, $content, [Text.UTF8Encoding]::new($false))
    if ([IO.File]::ReadAllText($path) -cne $content) { throw "Write mismatch: $relative" }
}
function Read([string]$relative) { [IO.File]::ReadAllText((Join-Path $review $relative)).Replace("`r`n", "`n") }
$s = Read 'Config/PublicApi/ConfigurationApi.cs'
$start = $s.IndexOf('                RequireReady();', $s.IndexOf('private static void StageCore'))
$end = $s.IndexOf("`n        }", $start)
$body = $s.Substring($start, $end - $start) -replace '(?m)^    ', ''
$body = $body.Replace('            var problems = documents.Validate(values);' + "`n" + '            if (problems.Length != 0) throw new InvalidDataException(string.Join("; ", problems.Select(x => x.ToString())));', @'
            if (preserveOwn)
            {
                var problems = documents.Validate(values);
                if (problems.Length != 0) throw new InvalidDataException(string.Join("; ", problems.Select(x => x.ToString())));
            }
'@)
$s = $s.Substring(0,$start) + $body + $s.Substring($end)
$s = $s.Replace('public static bool PrepareNetworkConfiguration() => NetworkPreparation?.Invoke() ?? true;', @'
public static bool PrepareNetworkConfiguration()
        {
            lock (Gate) RequireReady();
            if (NetworkPreparation == null) throw new InvalidOperationException("Network synchronization is not initialized.");
            return NetworkPreparation();
        }
'@)
Save 'Config/PublicApi/ConfigurationApi.cs' $s
$s = Read 'Config/PublicApi/RestartSynchronizationPolicy.cs'
$s = $s.Replace('internal void BeforeHostApplication()', 'internal bool BeforeHostApplication()').Replace('            lock (gate)' + "`n" + '                if (!enabled) legacyApplicationStarted = true;', @'
            lock (gate)
            {
                if (!enabled) legacyApplicationStarted = true;
                return enabled;
            }
'@)
Save 'Config/PublicApi/RestartSynchronizationPolicy.cs' $s
$s = Read 'Config/Sync/ConfigSyncManager.cs'
$s = $s.Replace('if (RestartPolicy.IsEnabled && !CheckNetworkRestart(package, false))', 'if (RestartPolicy.BeforeHostApplication() && !CheckNetworkRestart(package, false))')
$s = $s.Replace("                // Record before any legacy side effects: even a failed partial application invalidates startup evidence.`n                RestartPolicy.BeforeHostApplication();`n", '')
Save 'Config/Sync/ConfigSyncManager.cs' $s
$s = Read 'Tests/ConfigurationApi/ContextApiTests.cs'
$s = $s.Replace('        Start();' + "`n" + '        var own =', @'
        Start();
        bool callbackBlocked = false;
        int notifications = 0;
        Action listener = () =>
        {
            notifications++;
            var read = System.Threading.Tasks.Task.Run(() => ConfigurationApi.GetCapabilities());
            if (!read.Wait(TimeSpan.FromSeconds(3))) callbackBlocked = true;
        };
        ConfigurationApi.Changed += listener;
        var own =
'@)
$s = $s.Replace('        ConfigurationApi.StageReturnToOwnConfiguration();' + "`n" + '        Start();', @'
        int beforeReturn = notifications;
        ConfigurationApi.StageReturnToOwnConfiguration();
        Check(notifications == beforeReturn + 1 && !callbackBlocked, "return notification was missing or ran under the API lock");
        Start();
'@)
$s = $s.Replace('        Console.WriteLine("PASS: host receive', @'
        Check(!callbackBlocked, "network notification ran under the API lock");
        ConfigurationApi.Changed -= listener;
        own = ConfigurationApi.ReadOwnConfiguration();
        ConfigurationApi.StageContextConfiguration(values, "keep-pending", own.Revision);
        string pendingRevision = ConfigurationApi.GetPendingConfiguration().Revision;
        File.WriteAllText(units, "[Archer]\r\nHealth = 2147483648\r\n".Replace("\\r\\n", "\r\n"));
        bool invalidReturnRejected = false;
        try { ConfigurationApi.StageReturnToOwnConfiguration(); } catch (InvalidDataException) { invalidReturnRejected = true; }
        Check(invalidReturnRejected && ConfigurationApi.GetPendingConfiguration().Revision == pendingRevision,
            "invalid personal return replaced the previous valid preparation");
        ConfigurationApi.DiscardPendingConfiguration();
        File.WriteAllText(units, "[Archer]\r\nHealth = 20\r\n".Replace("\\r\\n", "\r\n"));
        // Upgrade an original personal-only package using its production identity and inventory.
        var legacyNames = Directory.GetFiles(ConfigPaths.OwnConfigDir, "*.toml")
            .Concat(Directory.GetFiles(Path.Combine(ConfigPaths.OwnConfigDir, "DamageMatrices"), "*.csv"))
            .Concat(new[] { Path.Combine(ConfigPaths.OwnConfigDir, "CrusaderDETweaker_GlobalMultipliers.cfg") })
            .Select(x => x.Substring(ConfigPaths.OwnConfigDir.Length + 1)).ToArray();
        var legacy = new ConfigurationFileTransaction(ConfigPaths.OwnConfigDir, legacyNames, "configuration-v1/test");
        var old = legacy.ReadOwn();
        var upgraded = new Dictionary<string, byte[]>(old);
        upgraded[ConfigPaths.UnitsFileName] = System.Text.Encoding.UTF8.GetBytes("[Archer]\r\nHealth = 77\r\n".Replace("\\r\\n", "\r\n"));
        legacy.Stage(upgraded, ConfigurationFileTransaction.Revision(old));
        Start();
        Check((long)ConfigurationApi.GetLoadedConfiguration().Values["units/Archer/Health"] == 77 && ConfigurationApi.GetPendingConfiguration() == null,
            "original package migration failed");
        Console.WriteLine("PASS: unlocked notifications, invalid return preserves pending package, original package migration");
        Console.WriteLine("PASS: host receive
'@)
Save 'Tests/ConfigurationApi/ContextApiTests.cs' $s
$s = Read 'Tests/ConfigurationApi/Program.cs'
$s = $s.Replace('private static void Main()', 'private static void Main(string[] args)')
$s = $s.Replace('        Directory.CreateDirectory(root);', "        Directory.CreateDirectory(root);`n        if (args.Length != 0)`n        {`n            if (args.Length != 4 || args[0] != `"--runtime`") throw new ArgumentException(`"Usage: --runtime <Tweaker DLL> <game directory> <configuration directory>`");`n            RuntimeCatalogTests.Run(args[1], args[2], args[3], root);`n        }")
Save 'Tests/ConfigurationApi/Program.cs' $s
$s = Read 'Tests/ConfigurationApi/ConfigurationApiTests.csproj'
$s = $s.Replace('<Compile Include="DocumentTests.cs" />', '<Compile Include="DocumentTests.cs" />' + "`n    " + '<Compile Include="RuntimeCatalogTests.cs" />')
Save 'Tests/ConfigurationApi/ConfigurationApiTests.csproj' $s
Save 'Tests/ConfigurationApi/RuntimeCatalogTests.cs' @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using BepInEx.Logging;

// Optional integration test: load the built production assembly but never create a plugin,
// call a native getter/setter, or modify installed configuration files.
internal static class RuntimeCatalogTests
{
    internal static void Run(string assemblyPath, string gameDirectory, string configurationDirectory, string output)
    {
        string[] dependencies = Directory.GetFiles(Path.Combine(gameDirectory, "BepInEx"), "*.dll", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(gameDirectory, "Stronghold Crusader Definitive Edition_Data", "Managed"), "*.dll")).ToArray();
        ResolveEventHandler resolver = (sender, args) =>
        {
            string name = new AssemblyName(args.Name).Name + ".dll";
            string path = dependencies.FirstOrDefault(x => string.Equals(Path.GetFileName(x), name, StringComparison.OrdinalIgnoreCase));
            return path == null ? null : Assembly.LoadFrom(path);
        };
        AppDomain.CurrentDomain.AssemblyResolve += resolver;
        try
        {
            var assembly = Assembly.LoadFrom(Path.GetFullPath(assemblyPath));
            assembly.GetType("CrusaderDETweaker.Plugin", true).GetField("Logger", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, new ManualLogSource("ConfigurationTests"));
            int handlers = 0;
            foreach (string kind in new[] { "Unit", "Structure" })
            {
                var registry = assembly.GetType("CrusaderDETweaker.Config.Toml." + kind + "s." + kind + "PropertyRegistry", true)
                    .GetProperty("Instance").GetValue(null);
                foreach (object handler in (IEnumerable)registry.GetType().GetMethod("GetAll").Invoke(registry, null))
                {
                    handlers++;
                    Type wrapper = handler.GetType();
                    Type valueType = (Type)wrapper.GetProperty("ValueType").GetValue(handler);
                    var validate = wrapper.GetMethod("ValidateWithoutApplying");
                    if (valueType != typeof(string) && valueType != typeof(bool))
                        Check((bool)validate.Invoke(handler, new object[] { -1L }), "numeric no-override sentinel rejected");
                    if (((string)wrapper.GetProperty("Name").GetValue(handler)).StartsWith("ResourceType", StringComparison.Ordinal))
                    {
                        Check((bool)validate.Invoke(handler, new object[] { "NONE" }), "NONE rejected");
                        Check(!(bool)validate.Invoke(handler, new object[] { "invalid-resource-name" }), "invalid resource accepted");
                    }
                }
            }
            Check(handlers > 0, "No production registrations tested");
            var defaults = Directory.GetFiles(configurationDirectory, "*.toml")
                .Concat(Directory.GetFiles(Path.Combine(configurationDirectory, "DamageMatrices"), "*.csv"))
                .ToDictionary(x => x.Substring(configurationDirectory.TrimEnd('\\').Length + 1), File.ReadAllText);
            const string cfgName = "CrusaderDETweaker_GlobalMultipliers.cfg";
            string cfgText = File.ReadAllText(Path.Combine(configurationDirectory, cfgName));
            var config = new ConfigFile(Path.Combine(output, "catalog-copy.cfg"), true);
            string section = "", type = "";
            foreach (string line in cfgText.Split('\n'))
            {
                string text = line.Trim();
                if (text.StartsWith("[")) section = text.Trim('[', ']');
                else if (text.StartsWith("# Setting type: ")) type = text.Substring("# Setting type: ".Length);
                else if (text.Length != 0 && !text.StartsWith("#") && text.Contains("="))
                {
                    int equals = text.IndexOf('=');
                    string key = text.Substring(0, equals).Trim(), value = text.Substring(equals + 1).Trim();
                    if (type == "Boolean") config.Bind(section, key, bool.Parse(value), "Read-only integration fixture");
                    else if (type == "Single") config.Bind(section, key, float.Parse(value, CultureInfo.InvariantCulture), "Read-only integration fixture");
                    else throw new InvalidDataException("Unsupported fixture type: " + type);
                }
            }
            Type documentType = assembly.GetType("CrusaderDETweaker.Configuration.ConfigurationDocuments", true);
            object documents = Activator.CreateInstance(documentType, BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { defaults, cfgName, config }, CultureInfo.InvariantCulture);
            assembly.GetType("CrusaderDETweaker.Configuration.ConfigurationPropertyValidation", true)
                .GetMethod("Bind", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { documents });
            var files = defaults.ToDictionary(x => x.Key, x => Encoding.UTF8.GetBytes(x.Value));
            files.Add(cfgName, Encoding.UTF8.GetBytes(cfgText));
            var read = documentType.GetMethod("Read", BindingFlags.Instance | BindingFlags.NonPublic);
            var values = (Dictionary<string, object>)read.Invoke(documents, new object[] { files });
            var problems = (Array)documentType.GetMethod("Validate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(documents, new object[] { values });
            Check(problems.Length == 0, "Production validation rejected fixture: " + string.Join("; ", problems.Cast<object>()));
            var rendered = documentType.GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(documents, new object[] { files, values });
            var roundtrip = (Dictionary<string, object>)read.Invoke(documents, new[] { rendered });
            Check(values.All(x => Equals(x.Value, roundtrip[x.Key])), "Production roundtrip changed values");
            Console.WriteLine("PASS: " + handlers + " real handlers; " + values.Count + " real options, production validator binding and document roundtrip");
        }
        finally { AppDomain.CurrentDomain.AssemblyResolve -= resolver; }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
'@
