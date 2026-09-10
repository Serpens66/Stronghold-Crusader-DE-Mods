using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

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
            Check(VirtualMath.ScaleMovementSpeed(1, 3, 2, ushort.MaxValue) == 1, "unrepresentable unit speed must remain nonzero");
            Check(VirtualMath.ScaleHealth(150, 200, 100) == 75, "damage ratio restoration");
            Check(VirtualMath.ScaleHealth(1, 100, 1) == 1, "living minimum health");
            Check(VirtualMath.ScalePositive(short.MaxValue, 2, 1, short.MaxValue) == short.MaxValue, "building health clamp");
            Check(VirtualMath.SignedLow32(4294967262L) == -34, "building result signed low32");
            Check(VirtualMath.HexLow32(4294967262L) == "0xFFFFFFDE", "building result hexadecimal low32");
            Check(Math.Abs(VirtualMath.ScaleTintChannel(0.5f, byte.MaxValue) - 0.5f) < 0.0001f, "opaque white tint changed a colour channel");
            Check(Math.Abs(VirtualMath.ScaleTintChannel(1f, 180) - (180f / byte.MaxValue)) < 0.0001f, "tint channel multiplication");
        }
        private static void TestPendingPolicy()
        {
            Check(!SpawnInitializationPolicy.CanFinalize(true, false, true, false, false), "NeedsInit finalized");
            Check(!SpawnInitializationPolicy.CanFinalize(false, true, true, false, false), "identity mismatch finalized");
            Check(!SpawnInitializationPolicy.CanFinalize(true, true, false, false, false), "invalid placement finalized");
            Check(!SpawnInitializationPolicy.CanFinalize(true, true, true, true, false), "unit without renderer finalized");
            Check(SpawnInitializationPolicy.CanFinalize(true, true, true, true, true), "valid unit not finalized");
            Check(!SpawnInitializationPolicy.CanFinalize(true, true, true, true, false), "building without tile hook finalized");
            Check(SpawnInitializationPolicy.CanFinalize(true, true, true, true, true), "valid building not finalized");
            Check(!SpawnInitializationPolicy.HasTimedOut(99, 100) && SpawnInitializationPolicy.HasTimedOut(100, 100), "timeout boundary");
        }
        private static void TestSaveRoundTrip()
        {
            var source = new[] { new SaveRecord { Kind = 1, GameId = 7, GlobalId = 99, TypeId = "mod:type", DefinitionVersion = 2, OriginalMaxHealth = 100, OriginalSpeed = 40 } };
            var groupSource = new[] { new ControlGroupSaveRecord { Group = 4, GameId = 7, GlobalId = 99, TypeId = "mod:type" } };
            SavePayload payload = SaveCodec.DecodePayload(SaveCodec.Encode(source, groupSource));
            SaveRecord record = payload.Records.Single();
            Check(record.Kind == 1 && record.GameId == 7 && record.GlobalId == 99, "save identity roundtrip");
            Check(record.TypeId == "mod:type" && record.DefinitionVersion == 2, "save definition roundtrip");
            Check(record.OriginalMaxHealth == 100 && record.OriginalSpeed == 40, "save baseline roundtrip");
            ControlGroupSaveRecord group = payload.ControlGroups.Single();
            Check(group.Group == 4 && group.GameId == 7 && group.GlobalId == 99 && group.TypeId == "mod:type", "control-group shadow roundtrip");
            byte[] legacy;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(1); writer.Write(1); writer.Write((byte)1); writer.Write(7); writer.Write((uint)99);
                writer.Write("mod:type"); writer.Write(2); writer.Write(100); writer.Write(40); writer.Flush();
                legacy = stream.ToArray();
            }
            SavePayload legacyPayload = SaveCodec.DecodePayload(legacy);
            Check(legacyPayload.Records.Count == 1 && legacyPayload.ControlGroups.Count == 0, "legacy save without control groups is not readable");
        }
        private static void TestStaticContracts()
        {
            string root = Directory.GetCurrentDirectory();
            string visual = File.ReadAllText(Path.Combine(root, "src", "VisualRuntime.cs"));
            string runtime = File.ReadAllText(Path.Combine(root, "src", "VirtualEntityRuntime.cs"));
            string hud = File.ReadAllText(Path.Combine(root, "src", "VirtualSpawnHud.cs"));
            string api = File.ReadAllText(Path.Combine(root, "src", "ApiContracts.cs"));
            string project = File.ReadAllText(Path.Combine(root, "VirtualUnitsPrototype.csproj"));
            string plugin = File.ReadAllText(Path.Combine(root, "src", "VirtualUnitsPlugin.cs"));
            string sharedPresentation = File.ReadAllText(Path.Combine(root, "..", "APIShared", "src", "UnitHudPresentationCapability.cs"));
            string plan = File.ReadAllText(Path.Combine(root, "UnitOverrideSystemPlan.md"));
            Check(Count(visual, "unitTrampoline(renderer,") == 1, "unit trampoline is not exactly once");
            Check(Count(visual, "buildingTrampoline(tile,") == 1, "building trampoline is not exactly once");
            Check(Count(visual, "tileColourTrampoline(self,") == 1, "tile-colour trampoline is not exactly once");
            Check(visual.Contains("GetTileBuildingId") && !visual.Contains("StructureGrid"), "building visual does not use the public tile lookup");
            Check(runtime.Contains("IsSingleplayerSkirmish") && runtime.Contains("IsRealMultiplayer") && runtime.Contains("IsMapEditor"), "mode guard incomplete");
            Check(runtime.Contains("CreateUnitLocal") && runtime.Contains("CreatePrefab") && runtime.Contains("false);"), "spawn contracts missing");
            Check(runtime.Contains("InitializationPending") && runtime.Contains("ProcessPendingUnits") && runtime.Contains("ProcessPendingBuildings"), "pending initialization flow missing");
            Check(runtime.Contains("DeleteUnitSafe") && runtime.Contains("DeleteBuildingSafe") && runtime.Contains("PendingIdentityMatches"), "pending cleanup is not identity guarded");
            Check(visual.Contains("Unit tint applied") && visual.Contains("Building tint applied") && visual.Contains("Building Vanilla sprite confirmed") && visual.Contains("IsUsableSprite"), "visual diagnostics missing");
            Check(runtime.Contains("GameTimeManagerAPI.Instance.OnTick += OnSimulationTick") && runtime.Contains("operationQueue.TryDequeue"), "native mutations are not driven by the simulation tick");
            Check(!runtime.Contains("Time.frameCount") && runtime.Contains("DeadlineTick") && runtime.Contains("currentSimulationTick"), "pending lifecycle still uses Unity render frames");
            Check(!hud.Contains("CreateUnitLocal") && !hud.Contains("CreatePrefab") && !hud.Contains("runtime.Tick()") && hud.Contains("DrainMainThreadWork"), "HUD still performs simulation work");
            Check(hud.Contains("getMouseMapTilePosition") && hud.Contains("getMapTile(internalX, internalY)") && hud.Contains("mapTile.gameMapX") && hud.Contains("mapTile.gameMapY"), "HUD does not convert rotated coordinates through the Vanilla map tile");
            Check(hud.Contains("PreviewMouseDown") && hud.Contains("Time.frameCount <= candidate.Frame") && hud.Contains("Show_HUD_Main") && hud.Contains("Show_BlackOut"), "HUD click-through isolation is incomplete");
            Check(!hud.Contains("hudHost.PreviewMouseDown") && hud.Contains("AttachInteractiveSurface(hudToggle)") && hud.Contains("AttachInteractiveSurface(hudPanel)"), "full-screen HUD host still intercepts world clicks");
            Check(hud.Contains("World-click candidate rejected") && hud.Contains("Show_HUD_FrontEndBlackout"), "click rejection diagnostics or consolidated modal gate missing");
            Check(Count(hud, "QueueVirtualUnitSpawn(candidate.TypeId") == 1 && Count(hud, "QueueVirtualBuildingSpawn(candidate.TypeId") == 1, "a physical HUD candidate can enqueue more than one operation per kind");
            Check(runtime.Contains("completionQueue") && api.Contains("VirtualOperationTicket") && api.Contains("OperationCompleted"), "thread-separated operation completion API missing");
            Check(runtime.Contains("SaveCodec.Encode(records)") && runtime.Contains("legacy control-group shadow records"), "legacy control-group metadata is not ignored safely");
            Check(visual.Contains("OnUnitVisualInterpolate") && visual.Contains("unit-hook-entry") && visual.Contains("building-hook-entry") && visual.Contains("tile-colour-hook-entry"), "visual recovery or detour entry diagnostics missing");
            Check(runtime.Contains("pending.RendererSeen && pending.UnitHookSeen") && runtime.Contains("pending.BuildingHookSeen && pending.BuildingTintSeen"), "success does not require the complete visual path");
            Check(!visual.Contains("effectiveFile") && !visual.Contains("TargetGm") && !visual.Contains("TryMap("), "obsolete cross-GM frame replacement remains active");
            Check(visual.Contains("unitTrampoline(renderer, file, image, colour, altFrame, chopFeet, transparency)") && visual.Contains("vanilla.a"), "Vanilla unit frame or transparency is not preserved");
            Check(visual.Contains("tile.tilemapRef.GetColor(location)") && visual.Contains("tile.tilemapRef.SetColor(location, tinted)") && !visual.Contains("tile.tileImage = target"), "building tint does not preserve Vanilla tile sprites and lighting");
            Check(api.Contains("VirtualSpriteTintProfile") && api.Contains("public byte Alpha") && runtime.Contains("tint.Alpha == byte.MaxValue"), "immutable opaque tint profile contract missing");
            Check(api.Contains("VirtualUnitPresentationProfile") && api.Contains("VirtualUnitSelectionSnapshot") && api.Contains("GetSelectedVirtualUnits"), "public distinct-presentation contracts missing");
            Check(!runtime.Contains("VirtualUnitPresentationRuntime") && project.Contains("APIShared.dll") && plugin.Contains("APIShared_Serp"), "VUP does not exclusively consume APIShared presentation");
            Check(runtime.Contains("UIButtonsK023") && !runtime.Contains("UIButtonsK001"), "Desert Archer category does not use the Vanilla Archer HUD icon");
            Check(sharedPresentation.Contains("EngineInterface.TroopSelectionChanged") && sharedPresentation.Contains("unitId <= 0"), "central ID-exact 1-based selection path missing");
            Check(!Regex.IsMatch(sharedPresentation, @"r_UnitChimp\s*=(?!=)") &&
                    !Regex.IsMatch(sharedPresentation, @"selectedChimpTypes\s*\[[^\]]+\]\s*=(?!=)") &&
                    !Regex.IsMatch(sharedPresentation, @"troop_counts\s*\[[^\]]+\]\s*=(?!=)"),
                "presentation mutates a fixed Vanilla type representation");
            Check(sharedPresentation.Contains("GroupTroops0") && sharedPresentation.Contains("groupRecords") && sharedPresentation.Contains("GlobalId"), "central concrete control-group reconstruction missing");
            Check(!File.ReadAllText(Path.Combine(root, "src", "VirtualUnitsPlugin.cs")).Contains("GM_BODY_ARAB_BOW"), "built-in Archer still selects Arab Bow frames");
            Check(plan.Contains("SetBodySprite(SpriteRenderer,int,int,int,bool,int,int)") && plan.Contains("GetTileBuildingId"), "confirmed plan corrections missing");
            Check(plan.Contains("gameMapX/gameMapY") && plan.Contains("Frameindizes") && plan.Contains("Farb"), "coordinate or tint plan correction missing");
        }
        private static int Count(string text, string value) { int count = 0, offset = 0; while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0) { count++; offset += value.Length; } return count; }
        private static void Check(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
    }
}
