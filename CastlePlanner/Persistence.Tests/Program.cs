using CastlePlanner;
using MessagePack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

internal static class Program
{
    private const string RuntimeName = "CastlePlanner_Serp.Runtime.msgpack";
    private static int checks;
    private static string fixture;

    private static int Main(string[] args)
    {
        fixture = TestWorkDirectory.New("castle-storage");
        try
        {
            HudMigration();
            ExistingTarget();
            BrokenPayload(false);
            BrokenPayload(true);
            FailedMigration();
            NoResurrection();
            if (args.Length == 2 && args[0] == "--assembly")
            {
                string assemblyPath = Path.GetFullPath(args[1]);
                string game = Environment.GetEnvironmentVariable("CASTLE_PLANNER_GAME_DIR") ??
                    @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
                string[] roots = { Path.GetDirectoryName(assemblyPath), Path.Combine(game, "BepInEx", "core"),
                    Path.Combine(game, "BepInEx", "plugins", "000shcdese"), Path.Combine(game, "BepInEx", "plugins", "APIShared_Serp"),
                    Path.Combine(game, "Stronghold Crusader Definitive Edition_Data", "Managed") };
                ResolveEventHandler resolve = (sender, request) =>
                {
                    string name = new AssemblyName(request.Name).Name + ".dll";
                    string path = roots.Select(root => Path.Combine(root, name)).FirstOrDefault(File.Exists);
                    return path == null ? null : Assembly.LoadFrom(path);
                };
                AppDomain.CurrentDomain.AssemblyResolve += resolve;
                try { Calibration(Assembly.LoadFrom(assemblyPath)); }
                finally { AppDomain.CurrentDomain.AssemblyResolve -= resolve; }
            }
            else if (args.Length != 0) throw new ArgumentException("Use --assembly <fresh CastlePlanner.dll> to include production calibration tests.");
            Console.WriteLine("PASS: CastlePlanner persistence " + checks + " assertions; fixture=" + fixture);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { Shared.PersonalDataFile.TestDocumentsDirectory = null; }
    }

    private static string Case(string name)
    {
        string root = Path.Combine(fixture, name);
        Directory.CreateDirectory(root);
        Shared.PersonalDataFile.TestDocumentsDirectory = () => Path.Combine(root, "documents");
        return root;
    }

    private static string Legacy(string root)
    {
        string directory = Path.Combine(root, "plugin", "LobbyModSettings");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, RuntimeName);
    }

    private static string Target() => Shared.PersonalDataFile.GetPath("CastlePlanner", RuntimeName);
    private static CastlePlannerRuntimeStorage Open(string root) => new CastlePlannerRuntimeStorage(Path.Combine(root, "plugin", "CastlePlanner.dll"), _ => { });
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static byte[] Payload(double x, double y)
    {
        return MessagePackSerializer.Serialize(new Dictionary<string, byte[]>
        {
            ["HasBlueprintHudPosition"] = MessagePackSerializer.Serialize(true),
            ["BlueprintHudPositionX"] = MessagePackSerializer.Serialize(x),
            ["BlueprintHudPositionY"] = MessagePackSerializer.Serialize(y),
            ["FutureOptionalValue"] = MessagePackSerializer.Serialize(42),
        });
    }

    private static void HudMigration()
    {
        string root = Case("hud"); string old = Legacy(root); byte[] bytes = Payload(0.21, 0.87); File.WriteAllBytes(old, bytes);
        var state = new State(); var storage = Open(root); storage.Load(state);
        Check(state.HasBlueprintHudPosition && state.BlueprintHudPositionX == 0.21 && state.BlueprintHudPositionY == 0.87, "Legacy MessagePack HUD fields must survive migration.");
        Check(!File.Exists(old) && File.ReadAllBytes(Target()).SequenceEqual(bytes), "Migration must publish identical bytes before deleting the old HUD file.");
        state.BlueprintHudPositionX = 0.43; storage.Save(state);
        var reloaded = new State(); Open(root).Load(reloaded);
        Check(reloaded.BlueprintHudPositionX == 0.43 && reloaded.BlueprintHudPositionY == 0.87, "HUD writes must persist across storage instances.");
        var payload = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(File.ReadAllBytes(Target()));
        Check(MessagePackSerializer.Deserialize<int>(payload["FutureOptionalValue"]) == 42, "Unrecognized optional fields must survive saves.");
    }

    private static void ExistingTarget()
    {
        string root = Case("target"); string old = Legacy(root); byte[] oldBytes = Payload(0.1, 0.2); File.WriteAllBytes(old, oldBytes);
        Directory.CreateDirectory(Path.GetDirectoryName(Target())); File.WriteAllBytes(Target(), Payload(0.8, 0.9));
        var state = new State(); Open(root).Load(state);
        Check(state.BlueprintHudPositionX == 0.8 && state.BlueprintHudPositionY == 0.9, "Existing Documents target must win over a different old file.");
        Check(File.ReadAllBytes(old).SequenceEqual(oldBytes), "Conflicting unimported source must not be deleted.");
    }

    private static void BrokenPayload(bool partial)
    {
        string root = Case(partial ? "partial" : "broken"); string old = Legacy(root);
        byte[] bytes = new byte[] { 0xC1 };
        if (partial)
        {
            var values = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(Payload(0.2, 0.6));
            values["BlueprintHudPositionX"] = bytes;
            bytes = MessagePackSerializer.Serialize(values);
        }
        File.WriteAllBytes(old, bytes);
        var state = new State(); var storage = Open(root); storage.Load(state);
        Check(!partial || state.HasBlueprintHudPosition && state.BlueprintHudPositionY == 0.6 && state.BlueprintHudPositionX == 0, "Invalid HUD field must not discard other valid fields.");
        state.HasBlueprintHudPosition = true; state.BlueprintHudPositionX = 0.4; state.BlueprintHudPositionY = 0.5; storage.Save(state);
        Check(Directory.GetFiles(Path.GetDirectoryName(Target())).Any(path => File.ReadAllBytes(path).SequenceEqual(bytes)), "A malformed/partially rejected original must be retained before rewriting.");
        var reloaded = new State(); Open(root).Load(reloaded);
        Check(reloaded.BlueprintHudPositionX == 0.4 && reloaded.BlueprintHudPositionY == 0.5, "A new explicit HUD position must remain writable after invalid input is preserved.");
    }

    private static void FailedMigration()
    {
        string root = Case("blocked"); string old = Legacy(root); byte[] bytes = Payload(0.3, 0.7); File.WriteAllBytes(old, bytes);
        Directory.CreateDirectory(Path.Combine(root, "documents", "SHCDE"));
        File.WriteAllText(Path.Combine(root, "documents", "SHCDE", "CastlePlanner"), "blocked directory");
        var state = new State(); var storage = Open(root); storage.Load(state);
        Check(state.BlueprintHudPositionX == 0.3 && state.BlueprintHudPositionY == 0.7, "Readable legacy HUD values must remain available when Documents publication fails.");
        storage.Save(new State { HasBlueprintHudPosition = true });
        Check(File.ReadAllBytes(old).SequenceEqual(bytes), "Failed migration must not replace the source with session defaults.");
    }

    private static void NoResurrection()
    {
        string root = Case("receipt"); string old = Legacy(root); File.WriteAllBytes(old, Payload(0.6, 0.8)); Open(root).Load(new State());
        File.Delete(Target()); File.WriteAllBytes(old, Payload(0.1, 0.2));
        var state = new State(); Open(root).Load(state);
        Check(!state.HasBlueprintHudPosition && !File.Exists(Target()), "A completed migration must never revive a later legacy file after target deletion.");
    }

    private static void Calibration(Assembly assembly)
    {
        string root = Case("calibration"); string documents = Path.Combine(root, "documents");
        Type fileType = assembly.GetType("Shared.PersonalDataFile", true);
        FieldInfo seam = fileType.GetField("TestDocumentsDirectory", BindingFlags.NonPublic | BindingFlags.Static);
        seam.SetValue(null, (Func<string>)(() => documents));
        try
        {
            Type type = assembly.GetType("CastlePlanner.BlueprintBuildingSizeCalibration", true);
            Type logType = Type.GetType("BepInEx.Logging.ManualLogSource, BepInEx", true);
            object log = Activator.CreateInstance(logType, "CastlePlanner.Persistence.Tests");
            string sourceRoot = Path.Combine(root, "plugin"); Directory.CreateDirectory(Path.Combine(sourceRoot, "RuntimeData"));
            string source = Path.Combine(sourceRoot, "RuntimeData", "BlueprintBuildingSizes.tsv");
            // Synthetic mapper keys exercise persisted rows without querying the game.
            string original = "# fixture\r\n1\tfixture-one\t2.5\t3.5\t4\t2\r\n" +
                "2\tfixture-two\t4.5\t5.5\t6\t1\t1\t0.25\t-0.5\t7\tGeneric\r\ninvalid-row\r\n";
            File.WriteAllText(source, original, new UTF8Encoding(false));
            object calibration = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { log, sourceRoot }, null);
            var measurements = (IDictionary)type.GetField("measurements", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(calibration);
            Check(measurements.Count == 2, "Production TSV parser must keep both legacy and extended valid rows while ignoring malformed input.");
            Check((float)measurements["1:Generic"].GetType().GetProperty("WorldWidth").GetValue(measurements["1:Generic"]) == 2.5f, "Production calibration width must survive migration.");
            string target = Path.Combine(documents, "SHCDE", "CastlePlanner", "BlueprintBuildingSizes.tsv");
            Check(!File.Exists(source) && File.ReadAllText(target) == original, "Calibration migration must preserve original bytes and remove its old source.");
            type.GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(calibration, null);
            Check(Directory.GetFiles(Path.GetDirectoryName(target)).Any(path => File.ReadAllBytes(path).SequenceEqual(new UTF8Encoding(false).GetBytes(original))), "Rejected TSV rows must remain preserved before normalization overwrites the new store.");
            object reloaded = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { log, sourceRoot }, null);
            var second = (IDictionary)type.GetField("measurements", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(reloaded);
            Check(second.Count == 2, "Production calibration writes must round-trip accepted rows.");
        }
        finally { seam.SetValue(null, null); }
    }

    private sealed class State : ICastlePlannerRuntimeState
    {
        public bool HasBlueprintHudPosition { get; set; }
        public double BlueprintHudPositionX { get; set; }
        public double BlueprintHudPositionY { get; set; }
    }
}
