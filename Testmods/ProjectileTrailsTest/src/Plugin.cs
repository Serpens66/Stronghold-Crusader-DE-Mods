using System;
using System.Collections.Generic;
using System.Reflection;
using APIShared;
using APIShared.Presentation;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SHCDESE.Interop.Enums;
using UnityEngine;
using Position = System.Numerics.Vector3;

namespace ProjectileTrailsTest
{
    [BepInPlugin(Guid, "Extreme Troops Projectile Trails Test", "0.1.0")]
    [BepInDependency("000shcdese", "2.14.4")]
    [BepInDependency("APIShared_Serp", "0.62.0")]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        internal const string Guid = "ProjectileTrailsTest_Serp";
        // Native launch subtype 0x5B becomes an ArcherArrow, uses this render file,
        // and retains its kind-1 Vanilla trail (see EXTREME_TROOPS.md).
        private const int KindOneArrowSpriteFile = 0x91;
        private static ManualLogSource log;
        private static ConfigEntry<bool> enabledSetting;
        private static ModApiClient api;
        private static IMissionLifecycleCapability missions;
        private static IUiFrameWorkRegistration frame;
        private static TrailBuffer history;
        private static TrailMeshes meshes;
        private static GameMap map;
        private static Camera camera;
        private static Matrix4x4 cameraMatrix;
        private static Enums.Dircs rotation;
        private static long session;
        private static bool initialized, ready, renderingEnabled, dirty, firstVisible;
        private static volatile bool failed;

        internal static bool SessionExtreme { get; private set; }
        internal static int LastSegmentCount { get; private set; }
        internal static bool TrailRenderingEnabled
        {
            get => renderingEnabled;
            set
            {
                if (renderingEnabled == value) return;
                renderingEnabled = value;
                ResetView();
            }
        }

        private void Awake()
        {
            if (initialized) return;
            initialized = true;
            log = Logger;
            enabledSetting = Config.Bind("Display", "Enabled", true, "Draw local arrow and bolt trails in Extreme Troops.");
            api = ApiShared.ForMod(Guid);
            api.WhenReady(_ =>
            {
                if (!api.Ui.TryPost(Initialize)) Fail("Unity dispatch unavailable");
            });
        }

        private static void Initialize()
        {
            try
            {
                var add = typeof(GameMap).GetMethod(nameof(GameMap.addUpdateFly), new[] {
                    typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(float),
                    typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int),
                    typeof(int), typeof(int)
                });
                var remove = typeof(GameMap).GetMethod(nameof(GameMap.deleteFly), new[] { typeof(Fly), typeof(bool) });
                var flies = typeof(GameMap).GetField("flies", BindingFlags.Instance | BindingFlags.NonPublic);
                if (add == null || add.IsStatic || add.ReturnType != typeof(void) || remove == null ||
                    remove.IsStatic || remove.ReturnType != typeof(void) ||
                    flies?.FieldType != typeof(Dictionary<int, Fly>))
                    throw new InvalidOperationException("GameMap projectile presentation contract changed.");
                var arguments = add.GetParameters();
                if (arguments[0].Name != "_objectID" || arguments[6].Name != "file" ||
                    arguments[11].Name != "_gameObjectType")
                    throw new InvalidOperationException("GameMap projectile presentation contract changed.");

                if (!api.Ui.TryRegisterBeforeRender("flight-trails", BeforeRender, out frame, out string reason))
                    throw new InvalidOperationException(reason);
                if (!api.TryGetMissionLifecycle(out missions, out var diagnostic))
                    throw new InvalidOperationException(diagnostic.Reason);
                history = new TrailBuffer();
                meshes = new TrailMeshes();
                renderingEnabled = enabledSetting.Value;
                var harmony = new Harmony(Guid);
                // APIShared's IL edit for the popularity popup stays in the original body.
                harmony.Patch(add, postfix: new HarmonyMethod(typeof(Plugin), nameof(AfterAdd)));
                harmony.Patch(remove, postfix: new HarmonyMethod(typeof(Plugin), nameof(AfterRemove)));
                if (!missions.TryRegisterObserver("flight-trail-mission", MissionChanged, MissionChanged, null, out diagnostic))
                    throw new InvalidOperationException(diagnostic.Reason);
                enabledSetting.SettingChanged += (_, __) => api.Ui.TryPost(() => TrailRenderingEnabled = enabledSetting.Value);
                ready = true;
                WriteInfo("ready");
                InitializeAllocationCounters();
                StartAutomation(api);
            }
            catch (Exception ex) { Fail(ex.ToString()); }
        }

        private static void MissionChanged(MissionLifecycleNotification notification)
        {
            if (!api.Ui.TryPost(SynchronizeMission))
                Fail("Mission notification could not reach Unity");
        }

        private static void SynchronizeMission()
        {
            if (failed) return;
            var current = missions.Current;
            long next = current?.SessionId ?? 0;
            if (session == next) return;
            ResetView();
            session = next;
            SessionExtreme = false;
            firstVisible = false;
            if (current == null || current.IsEditor) return;
            try
            {
                SessionExtreme = EngineInterface.getMultiplayerStartingData().extreme_troops != 0;
                WriteInfo($"mission {session} extreme={SessionExtreme} enabled={renderingEnabled}");
            }
            catch (Exception ex) { Fail(ex.ToString()); }
        }

        private static void AfterAdd(GameMap __instance, int __0, int __6, int __11, Dictionary<int, Fly> ___flies)
        {
            if (!ready || failed || !SessionExtreme || !renderingEnabled || !TrailBuffer.IsBodySlot(__0)) return;
            try
            {
                if ((__11 != (int)ProjectileType.ArcherArrow && __11 != (int)ProjectileType.CrossbowBolt) ||
                    __6 == KindOneArrowSpriteFile)
                {
                    if (history.Remove(__0)) Invalidate();
                    return;
                }

                if (!PrepareView(__instance)) return;
                if (!___flies.TryGetValue(__0, out var fly) || fly.sprRenderer == null)
                {
                    if (history.Remove(__0)) Invalidate();
                    return;
                }
                var sprite = fly.sprRenderer;
                Vector3 world = sprite.transform.position;
                long sort = BatchBudget.Key(sprite.sortingLayerID, sprite.sortingOrder);
                bool changed = history.Observe(__0, fly, __11, sort, new Position(world.x, world.y, world.z));
                TraceObservation(__0, world, changed);
                if (changed)
                    Invalidate();
            }
            catch (Exception ex) { Fail(ex.ToString()); }
        }

        private static void AfterRemove(GameMap __instance, Fly __0)
        {
            if (!ready || failed || __0 == null || !ReferenceEquals(map, __instance)) return;
            if (history.Remove(__0.objectID)) Invalidate();
        }

        private static bool PrepareView(GameMap current)
        {
            if (current == null || current.pendingRotationtriggered)
            {
                if (map != null) ResetView();
                return false;
            }
            var currentRotation = current.CurrentRotation();
            if (!ReferenceEquals(map, current) || rotation != currentRotation)
            {
                ResetView();
                map = current;
                rotation = currentRotation;
            }
            return true;
        }

        private static void Invalidate()
        {
            if (dirty) return;
            dirty = true;
            frame.Request();
        }

        private static void BeforeRender()
        {
            if (failed)
            {
                ResetView();
                return;
            }
            if (!SessionExtreme || !renderingEnabled || map == null) return;
            try
            {
                if (!PrepareView(GameMap.instance)) return;
                Camera current = Camera.main;
                if (current == null)
                {
                    meshes.Hide();
                    camera = null;
                    LastSegmentCount = 0;
                    frame.SetContinuous(history.HasSegments);
                    return;
                }
                Matrix4x4 matrix = current.cullingMatrix;
                if (!dirty && camera == current && cameraMatrix.Equals(matrix)) return;
                dirty = false;
                camera = current;
                cameraMatrix = matrix;
                long allocatedBefore = 0;
                SampleMeshAllocation(ref allocatedBefore);
                LastSegmentCount = meshes.Draw(history, current);
                RecordMeshAllocation(allocatedBefore);
                TracePublish(current);
                // Continue checking camera movement only while the last publish
                // can contain trails. Mesh uploads depend on actual changes.
                frame.SetContinuous(history.HasSegments);
                if (!firstVisible && LastSegmentCount > 0)
                {
                    firstVisible = true;
                    WriteInfo($"rendered first trails in mission {session}");
                }
            }
            catch (Exception ex)
            {
                ResetView();
                Fail(ex.ToString());
            }
        }

        private static void ResetView()
        {
            history?.Clear();
            meshes?.Hide();
            frame?.SetContinuous(false);
            map = null;
            camera = null;
            dirty = false;
            LastSegmentCount = 0;
        }

        private static void Fail(string reason)
        {
            if (failed) return;
            failed = true;
            frame?.Request();
            log.LogError($"[{DateTime.Now:HH:mm:ss.fff}] ProjectileTrailsTest disabled. {reason}");
        }

        internal static void WriteInfo(string message) =>
            log.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] ProjectileTrailsTest {message}");

        static partial void StartAutomation(ModApiClient api);
        static partial void InitializeAllocationCounters();
        static partial void SampleMeshAllocation(ref long allocated);
        static partial void RecordMeshAllocation(long before);
        static partial void TraceObservation(int id, Vector3 position, bool changed);
        static partial void TracePublish(Camera current);
    }
}
