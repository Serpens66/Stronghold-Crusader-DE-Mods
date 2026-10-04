using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BepInEx.Configuration;
using CrusaderDETweaker.Configuration;
using CrusaderDETweaker.Config.Toml;

internal static class ContextApiTests
{
    internal static void Run(string root)
    {
        ConfigPaths.OwnConfigDir = Path.Combine(root, "context-api");
        Directory.CreateDirectory(ConfigPaths.OwnConfigDir);
        ConfigGenerator.GenerateDefaultConfigGlobals(); ConfigGenerator.GenerateDefaultConfigUnits(); ConfigGenerator.GenerateDefaultConfigStructures();
        CrusaderDETweaker.Config.DamageMatrix.DamageMatrixManager.GenerateDefaults();
        File.WriteAllText(Path.Combine(ConfigPaths.OwnConfigDir, ConfigPaths.UnitsFileName), "# personal comment\r\n[Archer]\r\nHealth = 10\r\n");
        Start();
        var own = ConfigurationApi.ReadOwnConfiguration();
        var paths = Directory.GetFiles(ConfigPaths.OwnConfigDir, "*", SearchOption.AllDirectories);
        var original = paths.ToDictionary(x => x, File.ReadAllBytes);
        var values = new Dictionary<string, object>(own.Values);
        values["units/Archer/Health"] = 45L;
        ConfigurationApi.StageContextConfiguration(values, "mission|content-hash", own.Revision);
        Check((long)ConfigurationApi.GetLoadedConfiguration().Values["units/Archer/Health"] == 10, "stage changed loaded values");
        Start();
        Check(ConfigurationApi.GetLoadedConfiguration().ContextId == "mission|content-hash", "context selection not applied");
        Check((long)ConfigurationApi.GetLoadedConfiguration().Values["units/Archer/Health"] == 45, "temporary values not loaded");
        Check((long)ConfigurationApi.ReadOwnConfiguration().Values["units/Archer/Health"] == 10, "personal values polluted");
        Check(original.All(x => x.Value.SequenceEqual(File.ReadAllBytes(x.Key))), "temporary startup modified personal bytes");
        ConfigurationApi.StageReturnToOwnConfiguration();
        Start();
        Check(ConfigurationApi.GetLoadedConfiguration().ContextId == "", "return to personal files failed");
        Check(original.All(x => x.Value.SequenceEqual(File.ReadAllBytes(x.Key))), "restoration changed personal formatting");
        Check(ConfigurationApi.GetPendingConfiguration() == null, "completed preparation remained pending");
        own = ConfigurationApi.ReadOwnConfiguration();
        ConfigurationApi.StageContextConfiguration(values, "second|hash", own.Revision);
        string units = Path.Combine(ConfigPaths.OwnConfigDir, ConfigPaths.UnitsFileName);
        File.WriteAllText(units, "[Archer]\r\nHealth = 20\r\n");
        Start();
        Check((long)ConfigurationApi.ReadOwnConfiguration().Values["units/Archer/Health"] == 20 && ConfigurationApi.GetPendingConfiguration() != null,
            "external edit was overwritten or conflicted package lost");
        ConfigurationApi.DiscardPendingConfiguration();
        Console.WriteLine("PASS: production configuration facade, temporary startup, exact personal preservation, restoration and external conflict");
        var hostFiles = Directory.GetFiles(ConfigPaths.OwnConfigDir, "*.toml")
            .Concat(Directory.GetFiles(Path.Combine(ConfigPaths.OwnConfigDir, CrusaderDETweaker.Config.DamageMatrix.Core.MatrixPaths.MatrixDirName), "*.csv"))
            .ToDictionary(x => x.Substring(ConfigPaths.OwnConfigDir.Length + 1), File.ReadAllBytes, StringComparer.Ordinal);
        hostFiles[ConfigPaths.UnitsFileName] = System.Text.Encoding.UTF8.GetBytes("[Archer]\r\nHealth = 55\r\n".Replace("\\r\\n", "\r\n"));
        var multipliers = new Dictionary<string, float>();
        Check(!ConfigurationApi.PrepareReceivedConfiguration(hostFiles, multipliers, "network:host:hash", false), "unloaded host values reported active");
        Check(ConfigurationApi.GetPendingConfiguration() == null, "receiving host values staged without confirmation");
        Check(!ConfigurationApi.PrepareReceivedConfiguration(hostFiles, multipliers, "network:host:hash", true), "host change failed to request restart");
        Check((long)ConfigurationApi.ReadOwnConfiguration().Values["units/Archer/Health"] == 20, "host preparation overwrote personal files");
        Start();
        Check(ConfigurationApi.PrepareReceivedConfiguration(hostFiles, multipliers, "network:host:hash", false), "rejoining with loaded host values not accepted");
        hostFiles[ConfigPaths.UnitsFileName] = System.Text.Encoding.UTF8.GetBytes("[Archer]\r\nHealth = 56\r\n".Replace("\\r\\n", "\r\n"));
        Check(!ConfigurationApi.PrepareReceivedConfiguration(hostFiles, multipliers, "network:host:changed", false), "changed host values not detected");
        ConfigurationApi.StageReturnToOwnConfiguration(); Start();
        Check((long)ConfigurationApi.GetLoadedConfiguration().Values["units/Archer/Health"] == 20, "network return lost personal values");
        Console.WriteLine("PASS: host receive versus confirmation, isolated client preparation, rejoin comparison, changed host and personal restoration");
    }
    private static void Start()
    {
        ConfigPaths.StartupContextId = "";
        ConfigurationApi.ApplyPendingBeforeLoading();
        ConfigurationApi.CaptureGeneratedDefaults();
        var cfg = new ConfigFile(Path.Combine(ConfigPaths.StartupConfigDir, "CrusaderDETweaker_GlobalMultipliers.cfg"), true);
        cfg.Bind("Debug", "Enabled", false, "Local"); cfg.Save();
        ConfigurationApi.CompleteInitialization(cfg);
    }
    private static void Check(bool result, string message) { if (!result) throw new Exception(message); }
}
namespace CrusaderDETweaker { internal static class PluginInfo { internal const string PLUGIN_VERSION = "test"; } }
namespace CrusaderDETweaker.Configuration { internal static class ConfigurationPropertyValidation { internal static void Bind(ConfigurationDocuments documents) { } } }
namespace CrusaderDETweaker.Config.Toml
{
    internal static class ConfigPaths
    {
        internal static string OwnConfigDir, StartupContextId = "";
        internal static string StartupConfigDir => StartupContextId.Length == 0 ? OwnConfigDir : Path.Combine(OwnConfigDir, "Session");
        internal const string UnitsFileName = "units.toml", StructuresFileName = "structures.toml", GlobalsFileName = "gameplay.toml";
    }
    internal static class ConfigGenerator
    {
        internal static void GenerateDefaultConfigUnits() => Write(ConfigPaths.UnitsFileName, "[Archer]\nHealth = -1\n");
        internal static void GenerateDefaultConfigStructures() => Write(ConfigPaths.StructuresFileName, "[Tower]\nHealth = -1\n");
        internal static void GenerateDefaultConfigGlobals() => Write(ConfigPaths.GlobalsFileName, "[Flags]\nEnabled = false\n");
        internal static void Write(string name, string text)
        {
            string path = Path.Combine(ConfigPaths.StartupConfigDir, name);
            if (GeneratedConfigurationCapture.TryWrite(path, text)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, text);
        }
    }
}
namespace CrusaderDETweaker.Config.DamageMatrix
{
    internal static class DamageMatrixManager
    {
        internal static void GenerateDefaults() { foreach (string name in new[] { "melee.csv", "ranged.csv", "aoe.csv", "ballista.csv", "fire.csv", "heal.csv", "building.csv" }) ConfigGenerator.Write(Path.Combine("DamageMatrices", name), ",Archer\nArcher,10\n"); }
    }
}
namespace CrusaderDETweaker.Config.DamageMatrix.Core
{
    internal static class MatrixPaths
    {
        internal const string MatrixDirName = "DamageMatrices", MeleeDamageFileName = "melee.csv", RangedDamageFileName = "ranged.csv", EunuchAoeDamageFileName = "aoe.csv", BallistaDamageFileName = "ballista.csv", UnitFireDamageFileName = "fire.csv", BedouinHealFileName = "heal.csv", BuildingFireDamageFileName = "building.csv";
    }
}
