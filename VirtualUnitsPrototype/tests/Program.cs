using System;
using System.IO;
using System.Linq;

namespace VirtualUnitsPrototype.Tests
{
    internal static class Program
    {
        private static int checks;
        private static int Main()
        {
            try
            {
                TestIdentityRegistry(); TestHealthMath(); TestPendingPolicy(); TestSaveRoundTrip(); TestStaticContracts();
                Console.WriteLine($"VirtualUnitsPrototype tests passed: {checks} checks."); return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        private static void TestIdentityRegistry()
        {
            var registry = new IdentityRegistry<string>();
            registry.Set(1, 1, 10, "first");
            Check(registry.TryGet(1, 1, 10, out string first) && first == "first", "valid identity lookup");
            Check(!registry.TryGet(1, 1, 11, out _), "global-ID mismatch accepted");
            registry.Set(1, 1, 11, "replacement");
            Check(!registry.TryGet(1, 1, 10, out _), "reused slot retained old identity");
            Check(registry.TryGet(1, 1, 11, out string replacement) && replacement == "replacement", "replacement identity missing");
            Check(registry.Remove(1, 1) && registry.Count == 0, "registry removal");
        }
        private static void TestHealthMath()
        {
            Check(VirtualMath.ScalePositive(100, 2, 1, int.MaxValue) == 200, "health factor");
            Check(VirtualMath.ScaleMovementSpeed(100, 3, 2, ushort.MaxValue) == 67, "faster movement uses a smaller encoded speed");
            Check(VirtualMath.ScaleHealth(150, 200, 100) == 75, "damage ratio restoration");
            Check(VirtualMath.ScaleHealth(1, 100, 1) == 1, "living minimum health");
            Check(VirtualMath.ScalePositive(short.MaxValue, 2, 1, short.MaxValue) == short.MaxValue, "building health clamp");
            Check(VirtualMath.SignedLow32(4294967262L) == -34, "building result signed low32");
            Check(VirtualMath.HexLow32(4294967262L) == "0xFFFFFFDE", "building result hexadecimal low32");
        }
        private static void TestPendingPolicy()
        {
            Check(!SpawnInitializationPolicy.CanFinalize(true, false, true, false, false), "NeedsInit finalized");
            Check(!SpawnInitializationPolicy.CanFinalize(false, true, true, false, false), "identity mismatch finalized");
            Check(!SpawnInitializationPolicy.CanFinalize(true, true, false, false, false), "invalid placement finalized");
            Check(!SpawnInitializationPolicy.CanFinalize(true, true, true, true, false), "unit without renderer finalized");
            Check(SpawnInitializationPolicy.CanFinalize(true, true, true, true, true), "valid unit not finalized");
            Check(SpawnInitializationPolicy.CanFinalize(true, true, true, false, false), "valid building not finalized");
            Check(!SpawnInitializationPolicy.HasTimedOut(99, 100) && SpawnInitializationPolicy.HasTimedOut(100, 100), "timeout boundary");
        }
        private static void TestSaveRoundTrip()
        {
            var source = new[] { new SaveRecord { Kind = 1, GameId = 7, GlobalId = 99, TypeId = "mod:type", DefinitionVersion = 2, OriginalMaxHealth = 100, OriginalSpeed = 40 } };
            SaveRecord record = SaveCodec.Decode(SaveCodec.Encode(source)).Single();
            Check(record.Kind == 1 && record.GameId == 7 && record.GlobalId == 99, "save identity roundtrip");
            Check(record.TypeId == "mod:type" && record.DefinitionVersion == 2, "save definition roundtrip");
            Check(record.OriginalMaxHealth == 100 && record.OriginalSpeed == 40, "save baseline roundtrip");
        }
        private static void TestStaticContracts()
        {
            string root = Directory.GetCurrentDirectory();
            string visual = File.ReadAllText(Path.Combine(root, "src", "VisualRuntime.cs"));
            string runtime = File.ReadAllText(Path.Combine(root, "src", "VirtualEntityRuntime.cs"));
            string plan = File.ReadAllText(Path.Combine(root, "UnitOverrideSystemPlan.md"));
            Check(Count(visual, "unitTrampoline(renderer,") == 1, "unit trampoline is not exactly once");
            Check(Count(visual, "buildingTrampoline(tile,") == 1, "building trampoline is not exactly once");
            Check(visual.Contains("GetTileBuildingId") && !visual.Contains("StructureGrid"), "building visual reads native grid directly");
            Check(runtime.Contains("IsSingleplayerSkirmish") && runtime.Contains("IsRealMultiplayer") && runtime.Contains("IsMapEditor"), "mode guard incomplete");
            Check(runtime.Contains("CreateUnitLocal") && runtime.Contains("CreatePrefab") && runtime.Contains("false);"), "spawn contracts missing");
            Check(runtime.Contains("InitializationPending") && runtime.Contains("ProcessPendingUnits") && runtime.Contains("ProcessPendingBuildings"), "pending initialization flow missing");
            Check(runtime.Contains("DeleteUnitSafe") && runtime.Contains("DeleteBuildingSafe") && runtime.Contains("PendingIdentityMatches"), "pending cleanup is not identity guarded");
            Check(visual.Contains("Unit visual hook matched") && visual.Contains("Building visual hook matched") && visual.Contains("IsUsableSprite"), "visual diagnostics missing");
            Check(plan.Contains("SetBodySprite(SpriteRenderer,int,int,int,bool,int,int)") && plan.Contains("GetTileBuildingId"), "confirmed plan corrections missing");
        }
        private static int Count(string text, string value) { int count = 0, offset = 0; while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0) { count++; offset += value.Length; } return count; }
        private static void Check(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
    }
}
