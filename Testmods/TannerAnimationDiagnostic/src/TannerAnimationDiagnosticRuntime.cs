using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace TannerAnimationDiagnostic
{
    internal sealed class TannerAnimationDiagnosticRuntime
    {
        private delegate void AddBuildingAnimDelegate(GameMap map, int objectId, int x, int y,
            int tileX, int tileY, int animLayer, int file, int image, int colour,
            int transparency, int layerDelay, bool subSpecial, int halfPixelX, int halfPixelY);
        private delegate void DeleteBuildingAnimDelegate(GameMap map, BuildingAnim visual, bool removeFromDictionary);

        private readonly ManualLogSource log;
        private readonly Dictionary<int, VisualTimeline> units = new Dictionary<int, VisualTimeline>();
        private readonly Dictionary<int, VisualTimeline> buildingAnimations = new Dictionary<int, VisualTimeline>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly FieldInfo buildingAnimationsField;
        private Hook addBuildingHook;
        private Hook deleteBuildingHook;
        private NativeTannerFade nativeFade;
        private AddBuildingAnimDelegate addBuildingOriginal;
        private DeleteBuildingAnimDelegate deleteBuildingOriginal;
        private bool postCleanupMarkerLogged;
        private bool callbackErrorLogged;

        internal TannerAnimationDiagnosticRuntime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            buildingAnimationsField = typeof(GameMap).GetField("buildingAnims", BindingFlags.Instance | BindingFlags.NonPublic);
            if (buildingAnimationsField == null || buildingAnimationsField.FieldType != typeof(Dictionary<int, BuildingAnim>))
                throw new MissingFieldException("The installed GameMap.buildingAnims contract changed.");
        }

        internal void Initialize(CrusaderLibraryLoadContext context)
        {
            try
            {
                MethodInfo addMethod = typeof(GameMap).GetMethod(nameof(GameMap.addUpdateBuildingAnim),
                    BindingFlags.Instance | BindingFlags.Public, null,
                    new[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(int),
                        typeof(int), typeof(int), typeof(int), typeof(int), typeof(int),
                        typeof(int), typeof(bool), typeof(int), typeof(int) }, null);
                MethodInfo deleteMethod = typeof(GameMap).GetMethod(nameof(GameMap.deleteBuildingAnim),
                    BindingFlags.Instance | BindingFlags.Public, null,
                    new[] { typeof(BuildingAnim), typeof(bool) }, null);
                if (addMethod == null || deleteMethod == null)
                    throw new MissingMethodException("The installed GameMap building animation methods changed.");

                // Install once. Published hooks remain active and rooted until process exit.
                addBuildingHook = new Hook(addMethod, (AddBuildingAnimDelegate)AddBuildingAnimation);
                addBuildingOriginal = addBuildingHook.GenerateTrampoline<AddBuildingAnimDelegate>();
                deleteBuildingHook = new Hook(deleteMethod, (DeleteBuildingAnimDelegate)DeleteBuildingAnimation);
                deleteBuildingOriginal = deleteBuildingHook.GenerateTrampoline<DeleteBuildingAnimDelegate>();
                subscriptions.Add(UnitR3EventHooks.OnUnitUnityVisualInterpolate.Observable.Subscribe(OnUnitVisualInterpolate));
                subscriptions.Add(UnitR3EventHooks.OnUnitUnityVisualRemove.Observable.Subscribe(OnUnitVisualRemove));
                nativeFade = new NativeTannerFade(log, context);
            }
            catch
            {
                // Only an initialization candidate that was never published may be rolled back.
                foreach (IDisposable subscription in subscriptions)
                    subscription.Dispose();
                deleteBuildingHook?.Undo();
                deleteBuildingHook?.Dispose();
                addBuildingHook?.Undo();
                addBuildingHook?.Dispose();
                throw;
            }
        }

        private void OnUnitVisualInterpolate(UnitUnityVisualInterpolateEventArgs args)
        {
            MarkPostCleanupCallback();
            try
            {
                Chimp chimp = args?.Chimp;
                if (chimp == null || chimp.objectID <= 0)
                    return;
                int id = chimp.objectID;
                bool tracked = units.TryGetValue(id, out VisualTimeline timeline);
                if (chimp.file1 != (int)GM.GM_BODY_TANNER)
                {
                    if (tracked)
                    {
                        timeline.Close(Stopwatch.GetTimestamp(), "file_changed");
                        units.Remove(id);
                    }
                    return;
                }

                int aiState = -1;
                uint group = 0;
                uint nativeFrame = 0;
                unsafe
                {
                    if (GameUnitManagerAPI.Instance != null &&
                        GameUnitManagerAPI.Instance.TryGetUnitById(id, out GameUnit* unit))
                    {
                        if (unit->r_UnitChimp != eChimps.CHIMP_TYPE_TANNER && !tracked)
                            return;
                        aiState = unit->r_AIState;
                        group = unit->r_SpriteAnimationGroup;
                        nativeFrame = unit->r_AnimationFrame;
                    }
                }

                if (!tracked)
                {
                    timeline = new VisualTimeline($"UNIT id={id}", Write);
                    units.Add(id, timeline);
                }
                SpriteRenderer renderer = chimp.sprRenderer;
                float actualAlpha = renderer == null ? float.NaN : renderer.color.a;
                bool visible = renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy && actualAlpha > 0f;
                timeline.Observe(Stopwatch.GetTimestamp(),
                    new VisualSample(chimp.image1, chimp.transparency, actualAlpha, visible, aiState, group, nativeFrame));
            }
            catch (Exception ex)
            {
                LogCallbackError(ex);
            }
        }

        private void OnUnitVisualRemove(UnitUnityVisualRemoveEventArgs args)
        {
            try
            {
                int id = args?.Chimp?.objectID ?? 0;
                if (id > 0 && units.TryGetValue(id, out VisualTimeline timeline))
                {
                    timeline.Close(Stopwatch.GetTimestamp(), "visual_removed");
                    units.Remove(id);
                }
            }
            catch (Exception ex)
            {
                LogCallbackError(ex);
            }
        }

        private void AddBuildingAnimation(GameMap map, int objectId, int x, int y,
            int tileX, int tileY, int animLayer, int file, int image, int colour,
            int transparency, int layerDelay, bool subSpecial, int halfPixelX, int halfPixelY)
        {
            addBuildingOriginal(map, objectId, x, y, tileX, tileY, animLayer, file,
                image, colour, transparency, layerDelay, subSpecial, halfPixelX, halfPixelY);
            MarkPostCleanupCallback();
            try
            {
                if (file != (int)GM.GM_WORKSHOP_TANNER_ANIMS)
                {
                    if (buildingAnimations.TryGetValue(objectId, out VisualTimeline old))
                    {
                        old.Close(Stopwatch.GetTimestamp(), "file_changed");
                        buildingAnimations.Remove(objectId);
                    }
                    return;
                }
                var sprites = (Dictionary<int, BuildingAnim>)buildingAnimationsField.GetValue(map);
                SpriteRenderer renderer = sprites.TryGetValue(objectId, out BuildingAnim visual) ? visual.sprRenderer : null;
                float actualAlpha = renderer == null ? float.NaN : renderer.color.a;
                bool visible = renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy && actualAlpha > 0f;
                if (!buildingAnimations.TryGetValue(objectId, out VisualTimeline timeline))
                {
                    timeline = new VisualTimeline($"BUILDING_ANIM object={objectId} x={x} y={y} layer={animLayer}", Write);
                    buildingAnimations.Add(objectId, timeline);
                }
                timeline.Observe(Stopwatch.GetTimestamp(),
                    new VisualSample(image, transparency, actualAlpha, visible, -1, 0, 0));
            }
            catch (Exception ex)
            {
                LogCallbackError(ex);
            }
        }

        private void DeleteBuildingAnimation(GameMap map, BuildingAnim visual, bool removeFromDictionary)
        {
            try
            {
                if (visual != null && buildingAnimations.TryGetValue(visual.objectID, out VisualTimeline timeline))
                {
                    timeline.Close(Stopwatch.GetTimestamp(), "visual_removed");
                    buildingAnimations.Remove(visual.objectID);
                }
            }
            catch (Exception ex)
            {
                LogCallbackError(ex);
            }
            finally
            {
                deleteBuildingOriginal(map, visual, removeFromDictionary);
            }
        }

        private void MarkPostCleanupCallback()
        {
            if (postCleanupMarkerLogged)
                return;
            postCleanupMarkerLogged = true;
            Write("TANNER_DIAG_POST_CLEANUP_CALLBACK publisher=GameMap");
        }

        private void Write(string message) =>
            log.LogInfo($"[{DateTimeOffset.Now:yyyy-MM-ddTHH:mm:ss.fffzzz}] TANNER_DIAG {message}");

        private void LogCallbackError(Exception ex)
        {
            if (callbackErrorLogged)
                return;
            callbackErrorLogged = true;
            log.LogError($"[{DateTimeOffset.Now:yyyy-MM-ddTHH:mm:ss.fffzzz}] TANNER_DIAG_CALLBACK_FAILED {ex}");
        }
    }
}
