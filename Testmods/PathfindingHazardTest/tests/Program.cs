using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using PathfindingHazardTest;

string root = Path.GetFullPath(args.Length == 0 ? "." : args[0]);
int checks = 0;
void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
string fixture = Path.Combine(Path.GetTempPath(), "pf-hazard-" + Guid.NewGuid().ToString("N") + ".sav");
try
{
    // Parser boundary only; this placeholder is deliberately not presented as a loadable game save.
    File.WriteAllText(fixture, "offline configuration validation");
    foreach (string name in new[] { "fire-escape", "plague-escape", "fire-wait-bounded", "plague-wait-bounded", "fire-wait-unlimited", "plague-wait-unlimited" })
    {
        Check(Scenario.Owns(name), "Missing owned scenario: " + name);
        var scenario = new Scenario { Name = name, SavePath = fixture, ActorId = 1, TargetX = 20, TargetY = 20, HazardX = 15, HazardY = 20, WaitSeconds = 2, MaximumTicks = 1000 };
        scenario.Validate(); checks++;
        Check(scenario.Plague == name.StartsWith("plague-", StringComparison.Ordinal), "Hazard type mismatch");
        Check(scenario.Escape == name.EndsWith("-escape", StringComparison.Ordinal), "Operation mismatch");
        Check(scenario.Unlimited == name.EndsWith("-unlimited", StringComparison.Ordinal), "Wait mode mismatch");
        scenario.MaximumTicks = 1;
        try { scenario.Validate(); throw new Exception("Too-short observation accepted"); } catch (ArgumentException) { checks++; }
        scenario.MaximumTicks = 1000; scenario.ActorId = 0;
        try { scenario.Validate(); throw new Exception("Missing actor accepted"); } catch (ArgumentException) { checks++; }
        scenario.ActorId = 1; scenario.HazardX = 800;
        try { scenario.Validate(); throw new Exception("Invalid witness accepted"); } catch (ArgumentException) { checks++; }
        scenario.HazardX = 15; scenario.SavePath = "relative.sav";
        try { scenario.Validate(); throw new Exception("Relative fixture path accepted"); } catch (ArgumentException) { checks++; }
        scenario.SavePath = "\\fixture.sav";
        try { scenario.Validate(); throw new Exception("Drive-relative fixture path accepted"); } catch (ArgumentException) { checks++; }
    }
    foreach (string foreign in new[] { "", "SP-pathfinding", "fire", "plague-wait", "fire-escape-extra" })
        Check(!Scenario.Owns(foreign), "Foreign scenario must remain unowned: " + foreign);
    // These private names are the deliberate testmod reflection contract, verified against the real
    // compiled consumer. A future refactor must update the driver rather than silently observing nothing.
    string pathfinding = Path.Combine(root, "Pathfinding/BepInEx/plugins/Pathfinding_Serp/Pathfinding.dll");
    using var stream = File.OpenRead(pathfinding);
    using var pe = new PEReader(stream);
    MetadataReader metadata = pe.GetMetadataReader();
    TypeDefinition Type(string name) => metadata.TypeDefinitions.Select(metadata.GetTypeDefinition)
        .Single(type => metadata.GetString(type.Name) == name);
    var provider = Type("FriendlyMoatTraversalProvider");
    var fields = provider.GetFields().Select(handle => metadata.GetString(metadata.GetFieldDefinition(handle).Name)).ToHashSet(StringComparer.Ordinal);
    foreach (string name in new[] { "settings", "anchorAvailable", "fireEscapes", "replanWaits", "replanSolos", "fireTiles", "plagueTiles", "replanAborted", "tribeOrderSequence", "fireRulesActive", "plagueRulesActive" })
        Check(fields.Contains(name), "Missing compiled reflection field: " + name);
    var methods = provider.GetMethods().Select(metadata.GetMethodDefinition).ToArray();
    foreach (var expected in new[] { ("ApplyMissionOptions", 1), ("CaptureMovementCases", 1), ("RestoreMovementCases", 1), ("ResetReplanWatch", 0), ("ResetFireEscapes", 0) })
        Check(methods.Any(method => metadata.GetString(method.Name) == expected.Item1 && method.GetParameters().Count == expected.Item2), "Missing compiled reflection method: " + expected.Item1);
    Check(Type("EngineHost").GetFields().Any(handle => metadata.GetString(metadata.GetFieldDefinition(handle).Name) == "runtime"), "Missing process-root provider field");
    Check(Type("HazardTileSet").GetMethods().Any(handle => metadata.GetString(metadata.GetMethodDefinition(handle).Name) == "Contains" && metadata.GetMethodDefinition(handle).GetParameters().Count == 1), "Missing production hazard lookup");
    foreach (var expected in new[] { ("FireEscapeEntry", "Safe"), ("FireEscapeEntry", "Returning"), ("ReplanWait", "Until") })
        Check(Type(expected.Item1).GetFields().Any(handle => metadata.GetString(metadata.GetFieldDefinition(handle).Name) == expected.Item2), "Missing observed case field: " + expected);
    var properties = Type("EngineOptions").GetProperties().Select(handle => metadata.GetString(metadata.GetPropertyDefinition(handle).Name)).ToHashSet(StringComparer.Ordinal);
    foreach (string name in new[] { "AnchorSingleMoves", "EnablePathfindingCore", "AvoidFire", "AvoidPlague", "EscapeFire", "EscapePlague", "EscapePlagueRaised", "FireWaitSeconds", "PlagueWaitSeconds", "FireWaitIndefinitely", "PlagueWaitIndefinitely" })
        Check(properties.Contains(name), "Missing compiled reflection option: " + name);
    Console.WriteLine("PASS: " + checks + " prepared-scenario/configuration and compiled reflection boundary checks. No game acceptance.");
}
finally { File.Delete(fixture); }
