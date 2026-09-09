using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.EventAPI.Units;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using VirtualUnitsPrototype.API;

namespace VirtualUnitsPrototype
{
    internal sealed class VisualRuntime : IDisposable
    {
        private delegate void SetBodySpriteDelegate(SpriteRenderer renderer, int file, int image, int colour, bool altFrame, int chopFeet, int transparency);
        private delegate void SetBuildingGraphicDelegate(GameMapTile tile, int file, int image, int light);
        private readonly VirtualEntityRuntime runtime;
        private readonly ManualLogSource log;
        private readonly Dictionary<SpriteRenderer, int> unitByRenderer = new Dictionary<SpriteRenderer, int>(ReferenceComparer<SpriteRenderer>.Instance);
        private readonly HashSet<string> warnings = new HashSet<string>(StringComparer.Ordinal);
        private Hook unitHook;
        private Hook buildingHook;
        private SetBodySpriteDelegate unitTrampoline;
        private SetBuildingGraphicDelegate buildingTrampoline;

        public VisualRuntime(VirtualEntityRuntime runtime, ManualLogSource log) { this.runtime = runtime; this.log = log; }

        public void Install()
        {
            MethodInfo unitMethod = typeof(SpriteMapping).GetMethod("SetBodySprite", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(SpriteRenderer), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(int), typeof(int) }, null)
                ?? throw new MissingMethodException("SpriteMapping.SetBodySprite");
            MethodInfo buildingMethod = typeof(SpriteMapping).GetMethod("setGenericBuildingTileGraphic", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(GameMapTile), typeof(int), typeof(int), typeof(int) }, null)
                ?? throw new MissingMethodException("SpriteMapping.setGenericBuildingTileGraphic");
            try
            {
                unitHook = new Hook(unitMethod, (SetBodySpriteDelegate)SetBodySpriteHook);
                unitTrampoline = unitHook.GenerateTrampoline<SetBodySpriteDelegate>();
                buildingHook = new Hook(buildingMethod, (SetBuildingGraphicDelegate)SetBuildingGraphicHook);
                buildingTrampoline = buildingHook.GenerateTrampoline<SetBuildingGraphicDelegate>();
            }
            catch { Dispose(); throw; }
            Shared.DebugLogHelper.LogInfo(log, "Managed unit and building visual detours installed.");
        }

        public void OnUnitVisualSpawn(UnitUnityVisualSpawnEventArgs args)
        {
            if (args?.SpriteRenderer == null || args.UnitId <= 0) return;
            unitByRenderer[args.SpriteRenderer] = args.UnitId;
            runtime.RecordUnitRendererBinding(args.UnitId);
        }

        public void OnUnitVisualRemove(UnitUnityVisualRemoveEventArgs args)
        {
            SpriteRenderer renderer = args?.Chimp?.sprRenderer;
            if (renderer != null) unitByRenderer.Remove(renderer);
        }

        public void ClearBindings() { unitByRenderer.Clear(); warnings.Clear(); }

        public bool HasRendererBinding(int unitId)
        {
            if (unitId <= 0) return false;
            foreach (KeyValuePair<SpriteRenderer, int> pair in unitByRenderer)
                if (pair.Value == unitId && pair.Key != null) return true;
            return false;
        }

        private void SetBodySpriteHook(SpriteRenderer renderer, int file, int image, int colour, bool altFrame, int chopFeet, int transparency)
        {
            int effectiveFile = file;
            try
            {
                if (renderer != null && unitByRenderer.TryGetValue(renderer, out int unitId) && runtime.TryResolveUnitVisual(unitId, out VirtualUnitDefinition definition))
                {
                    int targetFile = (int)definition.SpriteProfile.TargetGm;
                    Sprite target = SpriteMapping.getBodyImage(targetFile, image, altFrame);
                    if (IsUsableSprite(target))
                    {
                        effectiveFile = targetFile;
                        LogOnce($"unit-hook:{unitId}", $"Unit visual hook matched: unitId={unitId}, globalId={GameUnitManagerAPI.Instance.GetGlobalId(unitId)}, vanillaGM={(Enums.GM)file}, targetGM={definition.SpriteProfile.TargetGm}, image={image}, alt={altFrame}, sprite={DescribeSprite(target)}.");
                    }
                    else WarnOnce($"unit:{definition.TypeId}:{image}:{altFrame}", $"Missing target unit frame; using Vanilla. type={definition.TypeId}, targetGM={definition.SpriteProfile.TargetGm}, image={image}, alt={altFrame}.");
                }
            }
            catch (Exception ex) { WarnOnce("unit-hook-error", $"Unit visual hook failed closed; Vanilla remains active: {ex}"); }
            unitTrampoline(renderer, effectiveFile, image, colour, altFrame, chopFeet, transparency);
        }

        private void SetBuildingGraphicHook(GameMapTile tile, int file, int image, int light)
        {
            buildingTrampoline(tile, file, image, light);
            try
            {
                if (tile == null || spriteLoader.instance == null) return;
                int tileId = GameTileManagerAPI.Instance.GetTileId(tile.gameMapX, tile.gameMapY);
                int buildingId = GameTileManagerAPI.Instance.GetTileBuildingId(tileId);
                if (buildingId <= 0 || !runtime.TryResolveBuildingVisual(buildingId, out VirtualBuildingDefinition definition)) return;
                if (!definition.VisualProfile.TryMap((Enums.GM)file, out Enums.GM targetGm)) return;
                Sprite target = spriteLoader.instance.GetGMSprite(targetGm, image);
                if (IsUsableSprite(target))
                {
                    tile.tileImage = target;
                    LogOnce($"building-hook:{buildingId}", $"Building visual hook matched: buildingId={buildingId}, globalId={GameBuildingManagerAPI.Instance.GetGlobalId(buildingId)}, tileId={tileId}, tile={tile.gameMapX},{tile.gameMapY}, vanillaGM={(Enums.GM)file}, targetGM={targetGm}, image={image}, sprite={DescribeSprite(target)}.");
                }
                else WarnOnce($"building:{definition.TypeId}:{targetGm}:{image}", $"Missing target building frame; using Vanilla. type={definition.TypeId}, targetGM={targetGm}, image={image}.");
            }
            catch (Exception ex) { WarnOnce("building-hook-error", $"Building visual hook failed closed after Vanilla: {ex}"); }
        }

        private void WarnOnce(string key, string message) { if (warnings.Add(key)) Shared.DebugLogHelper.LogWarning(log, message); }
        private void LogOnce(string key, string message) { if (warnings.Add(key)) Shared.DebugLogHelper.LogInfo(log, message); }
        private static bool IsUsableSprite(Sprite sprite) => sprite != null && sprite.texture != null && sprite.rect.width > 0f && sprite.rect.height > 0f && sprite.bounds.size.x > 0f && sprite.bounds.size.y > 0f;
        private static string DescribeSprite(Sprite sprite) => sprite == null ? "<null>" : $"name={sprite.name},rect={sprite.rect.width}x{sprite.rect.height},bounds={sprite.bounds.size.x}x{sprite.bounds.size.y}";
        public void Dispose()
        {
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
