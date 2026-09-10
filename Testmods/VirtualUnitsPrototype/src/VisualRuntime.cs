using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.EventAPI.Units;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
using UnityEngine.Tilemaps;
using VirtualUnitsPrototype.API;

namespace VirtualUnitsPrototype
{
    internal sealed class VisualRuntime : IDisposable
    {
        private delegate void SetBodySpriteDelegate(SpriteRenderer renderer, int file, int image, int colour, bool altFrame, int chopFeet, int transparency);
        private delegate void SetBuildingGraphicDelegate(GameMapTile tile, int file, int image, int light);
        private delegate void SetTileColourDelegate(gameTile self, GameMapTile tile, Vector3Int location, int light);
        private readonly VirtualEntityRuntime runtime;
        private readonly ManualLogSource log;
        private readonly Dictionary<SpriteRenderer, int> unitByRenderer = new Dictionary<SpriteRenderer, int>(ReferenceComparer<SpriteRenderer>.Instance);
        private readonly Dictionary<SpriteRenderer, Color> vanillaUnitColours = new Dictionary<SpriteRenderer, Color>(ReferenceComparer<SpriteRenderer>.Instance);
        private readonly HashSet<string> warnings = new HashSet<string>(StringComparer.Ordinal);
        private Hook unitHook;
        private Hook buildingHook;
        private Hook tileColourHook;
        private SetBodySpriteDelegate unitTrampoline;
        private SetBuildingGraphicDelegate buildingTrampoline;
        private SetTileColourDelegate tileColourTrampoline;

        public VisualRuntime(VirtualEntityRuntime runtime, ManualLogSource log) { this.runtime = runtime; this.log = log; }

        public void Install()
        {
            MethodInfo unitMethod = typeof(SpriteMapping).GetMethod("SetBodySprite", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(SpriteRenderer), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(int), typeof(int) }, null)
                ?? throw new MissingMethodException("SpriteMapping.SetBodySprite");
            MethodInfo buildingMethod = typeof(SpriteMapping).GetMethod("setGenericBuildingTileGraphic", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(GameMapTile), typeof(int), typeof(int), typeof(int) }, null)
                ?? throw new MissingMethodException("SpriteMapping.setGenericBuildingTileGraphic");
            MethodInfo tileColourMethod = typeof(gameTile).GetMethod("setTileColour", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
                new[] { typeof(GameMapTile), typeof(Vector3Int), typeof(int) }, null)
                ?? throw new MissingMethodException("gameTile.setTileColour");
            try
            {
                unitHook = new Hook(unitMethod, (SetBodySpriteDelegate)SetBodySpriteHook);
                unitTrampoline = unitHook.GenerateTrampoline<SetBodySpriteDelegate>();
                buildingHook = new Hook(buildingMethod, (SetBuildingGraphicDelegate)SetBuildingGraphicHook);
                buildingTrampoline = buildingHook.GenerateTrampoline<SetBuildingGraphicDelegate>();
                tileColourHook = new Hook(tileColourMethod, (SetTileColourDelegate)SetTileColourHook);
                tileColourTrampoline = tileColourHook.GenerateTrampoline<SetTileColourDelegate>();
            }
            catch { Dispose(); throw; }
            Shared.DebugLogHelper.LogInfo(log, "Managed unit sprite, building sprite, and tile-colour detours installed.");
        }

        public void OnUnitVisualSpawn(UnitUnityVisualSpawnEventArgs args)
        {
            if (args?.SpriteRenderer == null || args.UnitId <= 0) return;
            if (!runtime.MayHaveUnitVisual(args.UnitId)) return;
            unitByRenderer[args.SpriteRenderer] = args.UnitId;
            runtime.RecordUnitRendererBinding(args.UnitId);
            LogOnce($"unit-renderer:{args.UnitId}", $"Unit renderer bound from spawn event: unitId={args.UnitId}, globalId={runtime.GetKnownVisualGlobalId(VirtualEntityKind.Unit, args.UnitId)}, unityThread={Thread.CurrentThread.ManagedThreadId}.");
        }

        public void OnUnitVisualInterpolate(UnitUnityVisualInterpolateEventArgs args)
        {
            Chimp chimp = args?.Chimp;
            if (chimp == null || chimp.sprRenderer == null || chimp.objectID <= 0 || !runtime.MayHaveUnitVisual(chimp.objectID)) return;
            bool newlyBound = !unitByRenderer.TryGetValue(chimp.sprRenderer, out int previousUnitId) || previousUnitId != chimp.objectID;
            unitByRenderer[chimp.sprRenderer] = chimp.objectID;
            runtime.RecordUnitRendererBinding(chimp.objectID);
            LogOnce($"unit-interpolate:{chimp.objectID}", $"Unit renderer bound from interpolate event: unitId={chimp.objectID}, globalId={runtime.GetKnownVisualGlobalId(VirtualEntityKind.Unit, chimp.objectID)}, unityThread={Thread.CurrentThread.ManagedThreadId}.");
            // Interpolate fires after addUpdateChimp's normal sprite update; repaint once so existing units do not wait for an animation change.
            if (newlyBound) SpriteMapping.SetBodySprite(chimp.sprRenderer, chimp.file1, chimp.image1, chimp.colour1, chimp.altFrame1Set, chimp.chopFeet, chimp.transparency);
        }

        public void OnUnitVisualRemove(UnitUnityVisualRemoveEventArgs args)
        {
            SpriteRenderer renderer = args?.Chimp?.sprRenderer;
            if (renderer != null) { unitByRenderer.Remove(renderer); vanillaUnitColours.Remove(renderer); }
        }

        public void RestoreUnitTint(int unitId)
        {
            foreach (KeyValuePair<SpriteRenderer, int> pair in new List<KeyValuePair<SpriteRenderer, int>>(unitByRenderer))
            {
                if (pair.Value != unitId || pair.Key == null) continue;
                if (vanillaUnitColours.TryGetValue(pair.Key, out Color vanilla)) pair.Key.color = vanilla;
            }
        }

        public void ClearBindings() { unitByRenderer.Clear(); vanillaUnitColours.Clear(); warnings.Clear(); }

        private void SetBodySpriteHook(SpriteRenderer renderer, int file, int image, int colour, bool altFrame, int chopFeet, int transparency)
        {
            int matchedUnitId = 0;
            VirtualUnitDefinition matchedDefinition = null;
            try
            {
                LogOnce("unit-hook-entry", $"SetBodySprite detour entered: unityThread={Thread.CurrentThread.ManagedThreadId}, firstGM={(Enums.GM)file}, image={image}, alt={altFrame}.");
                if (renderer != null && unitByRenderer.TryGetValue(renderer, out int unitId) && runtime.TryResolveUnitVisual(unitId, out VirtualUnitDefinition definition))
                {
                    matchedUnitId = unitId;
                    matchedDefinition = definition;
                }
            }
            catch (Exception ex) { WarnOnce("unit-hook-error", $"Unit visual hook failed closed; Vanilla remains active: {ex}"); }
            // Vanilla must choose the frame, material, team colour, foot clipping and transparency before tinting.
            unitTrampoline(renderer, file, image, colour, altFrame, chopFeet, transparency);
            if (matchedUnitId <= 0 || matchedDefinition == null || renderer == null) return;
            bool tintApplied = false;
            try
            {
                if (IsUsableSprite(renderer.sprite))
                {
                    Color vanilla = renderer.color;
                    vanillaUnitColours[renderer] = vanilla;
                    renderer.color = MultiplyRgb(vanilla, matchedDefinition.SpriteProfile);
                    tintApplied = true;
                    LogOnce($"unit-hook:{matchedUnitId}", $"Unit tint applied: unitId={matchedUnitId}, globalId={runtime.GetKnownVisualGlobalId(VirtualEntityKind.Unit, matchedUnitId)}, vanillaGM={(Enums.GM)file}, image={image}, alt={altFrame}, vanillaColour={DescribeColour(vanilla)}, tintedColour={DescribeColour(renderer.color)}, sprite={DescribeSprite(renderer.sprite)}.");
                }
            }
            catch (Exception ex) { WarnOnce("unit-tint-error", $"Unit tint failed closed after Vanilla: {ex}"); }
            runtime.RecordUnitVisualHook(matchedUnitId, tintApplied);
        }

        private void SetBuildingGraphicHook(GameMapTile tile, int file, int image, int light)
        {
            buildingTrampoline(tile, file, image, light);
            try
            {
                LogOnce("building-hook-entry", $"Building tile detour entered: unityThread={Thread.CurrentThread.ManagedThreadId}, firstGM={(Enums.GM)file}, image={image}, light={light}.");
                if (tile == null || spriteLoader.instance == null) return;
                int tileId = GameTileManagerAPI.Instance.GetTileId(tile.gameMapX, tile.gameMapY);
                int buildingId = GameTileManagerAPI.Instance.GetTileBuildingId(tileId);
                if (buildingId <= 0 || !runtime.TryResolveBuildingVisual(buildingId, out VirtualBuildingDefinition definition)) return;
                if (IsUsableSprite(tile.tileImage))
                {
                    runtime.RecordBuildingVisualHook(buildingId, true);
                    LogOnce($"building-hook:{buildingId}", $"Building Vanilla sprite confirmed: buildingId={buildingId}, globalId={runtime.GetKnownVisualGlobalId(VirtualEntityKind.Building, buildingId)}, tileId={tileId}, tile={tile.gameMapX},{tile.gameMapY}, vanillaGM={(Enums.GM)file}, image={image}, light={light}, sprite={DescribeSprite(tile.tileImage)}.");
                }
            }
            catch (Exception ex) { WarnOnce("building-hook-error", $"Building visual hook failed closed after Vanilla: {ex}"); }
        }

        private void SetTileColourHook(gameTile self, GameMapTile tile, Vector3Int location, int light)
        {
            // Tint Vanilla's final light/shadow colour instead of replacing any tile-composed building sprite.
            tileColourTrampoline(self, tile, location, light);
            try
            {
                LogOnce("tile-colour-hook-entry", $"Tile-colour detour entered: unityThread={Thread.CurrentThread.ManagedThreadId}, location={location}, light={light}.");
                if (tile == null || tile.tilemapRef == null || GameTileManagerAPI.Instance == null) return;
                int tileId = GameTileManagerAPI.Instance.GetTileId(tile.gameMapX, tile.gameMapY);
                int buildingId = GameTileManagerAPI.Instance.GetTileBuildingId(tileId);
                if (buildingId <= 0 || !runtime.TryResolveBuildingVisual(buildingId, out VirtualBuildingDefinition definition) || !IsUsableSprite(tile.tileImage)) return;
                Color vanilla = tile.tilemapRef.GetColor(location);
                Color tinted = MultiplyRgb(vanilla, definition.VisualProfile);
                tile.tilemapRef.SetColor(location, tinted);
                runtime.RecordBuildingTintHook(buildingId);
                LogOnce($"building-tint:{buildingId}", $"Building tint applied: buildingId={buildingId}, globalId={runtime.GetKnownVisualGlobalId(VirtualEntityKind.Building, buildingId)}, tileId={tileId}, tile={tile.gameMapX},{tile.gameMapY}, vanillaColour={DescribeColour(vanilla)}, tintedColour={DescribeColour(tinted)}, sprite={DescribeSprite(tile.tileImage)}.");
            }
            catch (Exception ex) { WarnOnce("building-tint-error", $"Building tint failed closed after Vanilla: {ex}"); }
        }

        private void WarnOnce(string key, string message) { if (warnings.Add(key)) Shared.DebugLogHelper.LogWarning(log, message); }
        private void LogOnce(string key, string message) { if (warnings.Add(key)) Shared.DebugLogHelper.LogInfo(log, message); }
        private static bool IsUsableSprite(Sprite sprite) => sprite != null && sprite.texture != null && sprite.rect.width > 0f && sprite.rect.height > 0f && sprite.bounds.size.x > 0f && sprite.bounds.size.y > 0f;
        private static Color MultiplyRgb(Color vanilla, VirtualSpriteTintProfile tint) => new Color(VirtualMath.ScaleTintChannel(vanilla.r, tint.Red), VirtualMath.ScaleTintChannel(vanilla.g, tint.Green), VirtualMath.ScaleTintChannel(vanilla.b, tint.Blue), vanilla.a);
        private static string DescribeColour(Color colour) => $"{colour.r:F3},{colour.g:F3},{colour.b:F3},{colour.a:F3}";
        private static string DescribeSprite(Sprite sprite) => sprite == null ? "<null>" : $"name={sprite.name},rect={sprite.rect.width}x{sprite.rect.height},bounds={sprite.bounds.size.x}x{sprite.bounds.size.y}";
        public void Dispose()
        {
            Release(ref tileColourHook); tileColourTrampoline = null;
            Release(ref buildingHook); buildingTrampoline = null;
            Release(ref unitHook); unitTrampoline = null;
            ClearBindings();
        }
        private static void Release(ref Hook hook) { Hook current = hook; hook = null; if (current == null) return; try { current.Undo(); } catch { } try { current.Dispose(); } catch { } }

        private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
