using BepInEx.Configuration;
using CrusaderDETweaker.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

internal static class DocumentTests
{
    internal static void RunInstalledFiles(string root)
    {
        string source = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\config\CrusaderDETweaker";
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
        var defaults = Directory.GetFiles(source, "*.toml").Concat(Directory.GetFiles(Path.Combine(source, "DamageMatrices"), "*.csv"))
            .ToDictionary(path => path.Substring(source.Length + 1), File.ReadAllText);
        string cfgName = "CrusaderDETweaker_GlobalMultipliers.cfg";
        string cfgText = File.ReadAllText(Path.Combine(source, cfgName));
        var config = new ConfigFile(Path.Combine(root, "installed-copy.cfg"), true);
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
                if (type == "Boolean") config.Bind(section, key, bool.Parse(value), "Fixture from installed CFG metadata");
                else if (type == "Single") config.Bind(section, key, float.Parse(value, System.Globalization.CultureInfo.InvariantCulture), "Fixture from installed CFG metadata");
                else throw new InvalidDataException("Unknown installed CFG type: " + type);
            }
        }
        var documents = new ConfigurationDocuments(defaults, cfgName, config);
        var files = defaults.ToDictionary(x => x.Key, x => Encoding.UTF8.GetBytes(x.Value));
        files.Add(cfgName, Encoding.UTF8.GetBytes(cfgText));
        var original = documents.Read(files);
        var reloaded = documents.Read(documents.Render(files, original));
        Check(original.All(x => Equals(x.Value, reloaded[x.Key])), "installed configuration roundtrip changed a value");
        Console.WriteLine("PASS installed 11-file configuration: " + original.Count + " unique options/cells roundtrip (read-only source)");
    }

    internal static void Run(string root)
    {
        var defaults = new Dictionary<string, string>
        {
            ["units.toml"] = "[Archer]\nHealth = -1\nSpeed = -1\nMaxCount = -1\n",
            ["structures.toml"] = "[Tower]\nHealth = -1\n",
            ["gameplay.toml"] = "[Gatehouse]\nDistance = 200\n[Flags]\nEnabled = false\n"
        };
        for (int i = 0; i < 7; i++) defaults.Add("Matrices/m" + i + ".csv", ", Archer, Knight\nArcher, 10, -1\nKnight, 20, 30\n");
        string cfgName = "multipliers.cfg";
        var config = new ConfigFile(Path.Combine(root, cfgName), true);
        config.Bind("Multipliers", "Health", 1f, new ConfigDescription("Multiplier", new AcceptableValueRange<float>(0.1f, 100f)));
        config.Bind("Debug", "DebugLogging", false, "Local diagnostic");
        config.Save();
        var documents = new ConfigurationDocuments(defaults, cfgName, config);
        documents.BindValidator("units.toml", "Archer", "Speed", typeof(float), value => Convert.ToDouble(value) == -1 || Convert.ToDouble(value) > 0);
        var files = defaults.ToDictionary(x => x.Key, x => Encoding.UTF8.GetBytes(x.Value));
        files.Add(cfgName, File.ReadAllBytes(Path.Combine(root, cfgName)));
        var values = documents.Read(files);
        Check(documents.Options.Length == 36 && values.Count == 36, "catalog cells missing or duplicated");
        Check(documents.Options.Single(x => x.Key == "multipliers/Debug/DebugLogging").IsLocal, "debug option is host managed");
        Check(!documents.Options.Single(x => x.Key == "gameplay/Gatehouse/Distance").IsSupported, "gatehouse option incorrectly reported effective");
        values["units/Archer/MaxCount"] = 0L;
        values["units/Archer/Speed"] = 1.5d;
        values["gameplay/Flags/Enabled"] = true;
        values["m0/Archer/Knight"] = 99L;
        Check(documents.Validate(values).Length == 0, "valid mixed values rejected");
        var rendered = documents.Render(files, values);
        var roundtrip = documents.Read(rendered);
        Check(values.All(x => Equals(x.Value, roundtrip[x.Key])), "roundtrip changed values");
        Check(Encoding.UTF8.GetString(rendered["units.toml"]).Contains("-1 = unlimited"), "MaxCount migration marker lost");
        Check(Encoding.UTF8.GetString(files["units.toml"]).Contains("MaxCount = -1"), "render mutated source");
        values["units/Archer/Speed"] = double.NaN;
        Check(documents.Validate(values).Any(x => x.Key == "units/Archer/Speed"), "nonfinite number accepted");
        values["units/Archer/Speed"] = 1.5d;
        values.Remove("units/Archer/Health");
        Check(documents.Validate(values).Any(x => x.Key == "units/Archer/Health"), "incomplete snapshot accepted");
        files["Matrices/m0.csv"] = Encoding.UTF8.GetBytes(", Knight, Archer\nKnight, 30, 20\nArcher, -1, 10\n");
        Check((long)documents.Read(files)["m0/Archer/Archer"] == 10, "matrix key depends on row order");
        files["Matrices/m0.csv"] = Encoding.UTF8.GetBytes(", Archer, Archer\nKnight, 1, 2\nArcher, 3, 4\n");
        bool rejected = false;
        try { documents.Read(files); } catch (FormatException) { rejected = true; }
        Check(rejected, "duplicate matrix header accepted");
        Console.WriteLine("PASS document catalog, stable coordinates, exact roundtrip, sentinels, diagnostics and validation");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
namespace CrusaderDETweaker
{
    internal static class Plugin { internal static readonly BepInEx.Logging.ManualLogSource Logger = new BepInEx.Logging.ManualLogSource("Test"); }
}
namespace CrusaderDETweaker.Data { internal class NamespaceMarker { } }
namespace SHCDESE.Interop { internal class NamespaceMarker { } }
