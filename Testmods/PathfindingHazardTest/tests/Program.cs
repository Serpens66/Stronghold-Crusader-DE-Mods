using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using PathfindingHazardTest;

string root = Path.GetFullPath(args.Length == 0 ? "." : args[0]);
int checks = 0;
void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }

// Request ownership: the matrix and each single case, nothing else (foreign requests stay untouched).
Check(Scenario.Owns(Scenario.Matrix) && Scenario.Select(Scenario.Matrix).Length == Scenario.Cases.Length, "Matrix does not select every case");
Check(Scenario.Cases.Select(c => c.Name).Distinct().Count() == Scenario.Cases.Length, "Case names must be unique (step names)");
foreach (HazardCase item in Scenario.Cases)
{
    Check(Scenario.Owns(item.Name) && Scenario.Select(item.Name).Single() == item, "Single case not selectable: " + item.Name);
    Check(item.Plague == item.Name.StartsWith("plague-", StringComparison.Ordinal), "Hazard type mismatch: " + item.Name);
    Check(item.Escape == (item.Kind == CaseKind.Escape) && item.Unlimited == (item.Kind == CaseKind.WaitUnlimited), "Kind mismatch: " + item.Name);
}
foreach (bool plague in new[] { false, true })
    foreach (CaseKind kind in new[] { CaseKind.Escape, CaseKind.WaitBounded, CaseKind.WaitUnlimited })
        Check(Scenario.Cases.Any(c => c.Plague == plague && c.Kind == kind), "Matrix lacks " + (plague ? "plague " : "fire ") + kind);
Check(Scenario.Cases.Any(c => c.Plague && c.Kind == CaseKind.ImmuneCrossing), "Matrix lacks the plague-immune crossing");
foreach (string foreign in new[] { "", "SP-pathfinding", "fire", "plague-wait", "fire-escape-extra", "HAZARD-MATRIX" })
    Check(!Scenario.Owns(foreign), "Foreign scenario must remain unowned: " + foreign);
foreach (int seconds in new[] { 1, 2, 15 }) { Scenario.ValidateWaitSeconds(seconds); checks++; }
foreach (int seconds in new[] { 0, 16, -1 })
    try { Scenario.ValidateWaitSeconds(seconds); throw new Exception("Invalid WaitSeconds accepted: " + seconds); } catch (ArgumentException) { checks++; }

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
foreach (string name in new[] { "settings", "anchorAvailable", "anchorMaskNext", "fireRules", "fireEscapes", "replanWaits", "replanSolos", "fireTiles",
    "plagueTiles", "replanSettled", "tribeOrderSequence", "fireRulesActive", "plagueRulesActive" })
    Check(fields.Contains(name), "Missing compiled reflection field: " + name);
var methods = provider.GetMethods().Select(metadata.GetMethodDefinition).ToArray();
foreach (var expected in new[] { ("ApplyMissionOptions", 1), ("CaptureMovementCases", 1), ("RestoreMovementCases", 1), ("ResetReplanWatch", 0), ("ResetFireEscapes", 0) })
    Check(methods.Any(method => metadata.GetString(method.Name) == expected.Item1 && method.GetParameters().Count == expected.Item2), "Missing compiled reflection method: " + expected.Item1);
Check(Type("EngineHost").GetFields().Any(handle => metadata.GetString(metadata.GetFieldDefinition(handle).Name) == "runtime"), "Missing process-root provider field");
Check(Type("HazardTileSet").GetMethods().Any(handle => metadata.GetString(metadata.GetMethodDefinition(handle).Name) == "Contains" && metadata.GetMethodDefinition(handle).GetParameters().Count == 1), "Missing production hazard lookup");
Check(Type("FireRouteRules").GetProperties().Any(handle => metadata.GetString(metadata.GetPropertyDefinition(handle).Name) == "Effects"), "Missing bound effect manager property");
foreach (var expected in new[] { ("FireEscapeEntry", "Safe"), ("FireEscapeEntry", "Returning"), ("ReplanWait", "Until") })
    Check(Type(expected.Item1).GetFields().Any(handle => metadata.GetString(metadata.GetFieldDefinition(handle).Name) == expected.Item2), "Missing observed case field: " + expected);
var properties = Type("EngineOptions").GetProperties().Select(handle => metadata.GetString(metadata.GetPropertyDefinition(handle).Name)).ToHashSet(StringComparer.Ordinal);
foreach (string name in new[] { "AnchorSingleMoves", "EnablePathfindingCore", "AvoidFire", "AvoidPlague", "EscapeFire", "EscapePlague", "EscapePlagueRaised",
    "FireWaitSeconds", "PlagueWaitSeconds", "FireWaitIndefinitely", "PlagueWaitIndefinitely", "FireReturnSeconds", "PlagueReturnSeconds",
    "FireDetourPercent", "FireDetourMinTiles" })
    Check(properties.Contains(name), "Missing compiled reflection option: " + name);
Console.WriteLine("PASS: " + checks + " scenario-selection and compiled reflection boundary checks. No game acceptance.");
