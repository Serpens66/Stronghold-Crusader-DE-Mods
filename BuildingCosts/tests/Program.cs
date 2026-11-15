using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
    private static int Main(string[] args)
    {
        try
        {
            string workspace = Path.GetFullPath(args.Length == 0 ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..") : args[0]);
            string game = args.Length > 1 ? args[1] : @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
            string[] paths = { Path.Combine(workspace, "APIShared", "BepInEx", "plugins", "APIShared_Serp"),
                Path.Combine(game, "BepInEx", "plugins", "000shcdese"), Path.Combine(game, "BepInEx", "core"),
                Path.Combine(game, "Stronghold Crusader Definitive Edition_Data", "Managed") };
            AppDomain.CurrentDomain.AssemblyResolve += (_, e) => {
                string file = new AssemblyName(e.Name).Name + ".dll";
                foreach (var path in paths) if (File.Exists(Path.Combine(path, file))) return Assembly.LoadFrom(Path.Combine(path, file));
                return null;
            };
            var assembly = Assembly.LoadFrom(Path.Combine(workspace, "BuildingCosts", "BepInEx", "plugins", "BuildingCosts_Serp", "BuildingCosts.dll"));
            var type = assembly.GetType("BuildingCosts.BuildingCostsLobbyViewModel", true);
            var parse = type.GetMethod("ParseHumanCosts", BindingFlags.Static | BindingFlags.NonPublic);
            var serialize = type.GetMethod("SerializeHumanCosts", BindingFlags.Static | BindingFlags.NonPublic);
            string input = "MAPPER_HOVEL=STORED_GOLD:-12345,STORED_WOOD_PLANKS:1000,STORED_STONE_BLOCKS:-10,STORED_NULL:1\nMAPPER_UNKNOWN=STORED_GOLD:100\nMAPPER_BRAZIER=STORED_GOLD:57";
            object parsed = parse.Invoke(null, new object[] { input });
            string normalized = (string)serialize.Invoke(null, new[] { parsed });
            Check(normalized.Contains("MAPPER_HOVEL=STORED_GOLD:-10000,STORED_WOOD_PLANKS:100"), "production codec clamps gold and material domains");
            Check(!normalized.Contains("STORED_NULL") && !normalized.Contains("MAPPER_UNKNOWN") && !normalized.Contains("STORED_STONE_BLOCKS:-"), "unknown targets, goods and negative material costs rejected");
            Check(normalized.Contains("MAPPER_BRAZIER=STORED_GOLD:57"), "Human-only target survives serialization");
            Check((string)serialize.Invoke(null, new[] { parse.Invoke(null, new object[] { normalized }) }) == normalized, "Human settings round trip preserves normalized values");
            // These tests bypass UI construction and permissions. They test the actual absent-property
            // migration getter used by old persisted settings, without starting Unity/Noesis/native APIs.
            object model = FormatterServices.GetUninitializedObject(type);
            type.GetField("buildingCosts", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(model,
                "MAPPER_BRAZIER=-1,12,7,4,123\nMAPPER_HOVEL=7,-1,-1,-1,-1");
            var property = type.GetProperty("HumanExtraBuildingCosts");
            string migrated = (string)property.GetValue(model);
            Check(migrated.Contains("MAPPER_BRAZIER=STORED_GOLD:123,STORED_STONE_BLOCKS:12,STORED_IRON_INGOTS:7,STORED_PITCH_RAW:4"), "old Brazier costs migrate with -1 becoming zero");
            Check(migrated.Contains("MAPPER_HOVEL=\r\n"), "ordinary base costs do not migrate to extras");
            string explicitValues = (string)serialize.Invoke(null, new[] { parse.Invoke(null, new object[] { "MAPPER_BRAZIER=STORED_GOLD:9" }) });
            type.GetField("humanExtraBuildingCosts", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(model, explicitValues);
            Check((string)property.GetValue(model) == explicitValues, "explicit new Human values win over old Brazier values");
            var baseCosts = (IDictionary)type.GetMethod("ParseBuildingCosts").Invoke(model, null);
            Check(baseCosts.Count == 1, "old Brazier row is never applied to native costs");
            Check(type.GetProperty("CostEntries") != null && type.GetProperty("BuildingCosts") != null, "existing published settings surface retained");
            Console.WriteLine("Human cost settings: " + checks + " checks passed.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
