using System;
using System.Collections.Generic;
using System.Reflection;
using APIShared;
using APIShared.Presentation;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop.Enums;
using Shared;
using UnityEngine;
using Position = System.Numerics.Vector3;
using Axis = System.Numerics.Vector2;

namespace ProjectileTrailsTest
{
    [BepInPlugin(Guid, "Extreme Troops Projectile Trails Test", "0.1.3")]
    [BepInDependency("000shcdese", "2.14.4")]
    [BepInDependency("APIShared_Serp", "0.62.0")]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        internal const string Guid = "ProjectileTrailsTest_Serp";
        private const int WideProjectileLimit = TrailBuffer.WideProjectileLimit;
        private const int ProjectileLayerDelay = 1;
        private const float NativeHeightScale = 32f;
        private static ManualLogSource log;
        private static ConfigEntry<bool> enabledSetting;
        private static ModApiClient api;
        private static IMissionLifecycleCapability missions;
        private static IUiFrameWorkRegistration frame;
        private static NativeFlightSamples native;
        private static TrailMeshes meshes;
        private static readonly TrailBuffer history = new TrailBuffer();
        private static bool clockKnown;
        private static GameMap map;
        private static Camera camera;
        private static Matrix4x4 cameraMatrix;
        private static Enums.Dircs rotation;
        private static long session, lastHead;
        private static uint lastTick;
        private static int sortLayer, objectLayer;
        private static Axis axisX, axisY;
        private static bool initialized, ready, renderingEnabled, styleKnown, firstVisible;
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
                RefreshCapture();
            }
        }

        private void Awake()
        {
            if (initialized) return;
            initialized = true;
            log = Logger;
            enabledSetting = Config.Bind("Display", "Enabled", true, "Draw local fading arrow and bolt afterimages in Extreme Troops.");
            api = ApiShared.ForMod(Guid);
            CrusaderLibrary.Instance.LibraryLoaded += LibraryLoaded;
            api.WhenReady(_ =>
            {
                if (!api.Ui.TryPost(Initialize)) Fail("Unity dispatch unavailable");
            });
        }

        private static void LibraryLoaded(CrusaderLibraryLoadContext context)
        {
            try
            {
                // Root the buffer and hook before publication, even if the backend later reports failure.
                native = new NativeFlightSamples(context, log);
                native.Install(context);
            }
            catch (Exception ex) { Fail(ex.ToString()); }
        }

        private static void Initialize()
        {
            if (failed) return;
            try
            {
                var add = typeof(GameMap).GetMethod(nameof(GameMap.addUpdateFly), new[] {
                    typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(float),
                    typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int),
                    typeof(int), typeof(int)
                });
                var flies = typeof(GameMap).GetField("flies", BindingFlags.Instance | BindingFlags.NonPublic);
                if (add == null || add.IsStatic || add.ReturnType != typeof(void) ||
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
                meshes = new TrailMeshes();
                renderingEnabled = enabledSetting.Value;
                // APIShared's popularity-popup IL edit stays in the original body.
                new Harmony(Guid).Patch(add, postfix: new HarmonyMethod(typeof(Plugin), nameof(AfterAdd)));
                if (!missions.TryRegisterObserver("flight-trail-mission", MissionChanged, MissionChanged, null, out diagnostic))
                    throw new InvalidOperationException(diagnostic.Reason);
                enabledSetting.SettingChanged += (_, __) => api.Ui.TryPost(() => TrailRenderingEnabled = enabledSetting.Value);
                ready = true;
                SynchronizeMission();
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
            if (current == null) return;
            try
            {
                if (NativePatchGuard.Recheck() != 0)
                    throw new InvalidOperationException("Afterimage observer was overwritten.");
                if (current.IsEditor) return;
                SessionExtreme = EngineInterface.getMultiplayerStartingData().extreme_troops != 0;
                RefreshCapture();
                WriteInfo($"mission {session} extreme={SessionExtreme} enabled={renderingEnabled}");
            }
            catch (Exception ex) { Fail(ex.ToString()); }
        }

        private static void AfterAdd(GameMap __instance, int __0, int __1, int __2, int __3, int __4,
            float __5, int __6, int __7, int __9, int __10, int __11, Dictionary<int, Fly> ___flies)
        {
            if (!ready || failed || __0 <= 0 || __0 >= WideProjectileLimit) return;
            TraceVanilla(__0, __1, __2, __3, __4, __5, __6, __7, __9, __10, __11, ___flies);
            if (!SessionExtreme || !renderingEnabled || styleKnown ||
                (__11 != (int)ProjectileType.ArcherArrow && __11 != (int)ProjectileType.CrossbowBolt &&
                 __11 != (int)ProjectileType.UnkMissile24)) return;
            try
            {
                if (!___flies.TryGetValue(__0, out var fly) || fly.sprRenderer == null) return;
                var sprite = fly.sprRenderer;
                Matrix4x4 transform = sprite.transform.localToWorldMatrix;
                float flipX = sprite.flipX ? -1f : 1f;
                float flipY = sprite.flipY ? -1f : 1f;
                axisX = new Axis(transform.m00 * flipX, transform.m10 * flipX);
                axisY = new Axis(transform.m01 * flipY, transform.m11 * flipY);
                sortLayer = sprite.sortingLayerID;
                objectLayer = sprite.gameObject.layer;
                styleKnown = true;
            }
            catch (Exception ex) { Fail(ex.ToString()); }
        }

        private static void RestartCapture(bool active)
        {
            lastHead = native?.Head ?? 0;
            native?.Reset(active);
            history.Clear();
            clockKnown = false;
            HideMesh();
        }

        private static void RefreshCapture()
        {
            bool active = !failed && session != 0 && SessionExtreme && renderingEnabled;
            RestartCapture(active);
            frame?.SetContinuous(active);
        }

        private static void BeforeRender()
        {
            if (failed)
            {
                ResetView();
                return;
            }
            if (!SessionExtreme || !renderingEnabled || native == null) return;
            try
            {
                GameMap currentMap = GameMap.instance;
                if (currentMap == null || currentMap.pendingRotationtriggered)
                {
                    if (native.Generation != 0) RestartCapture(false);
                    map = null;
                    return;
                }

                var currentRotation = currentMap.CurrentRotation();
                if (map != currentMap || rotation != currentRotation)
                {
                    if (map != null || native.Generation == 0) RestartCapture(true);
                    map = currentMap;
                    rotation = currentRotation;
                }
                if (!styleKnown) return;

                long head = native.Head;
                if (head == lastHead && history.Count == 0) return;
                uint tick = native.Tick;
                if (clockKnown && unchecked(tick - lastTick) > int.MaxValue)
                {
                    RestartCapture(true);
                    return;
                }
                if (!clockKnown || lastTick != tick)
                {
                    meshes.SetTick(tick);
                    lastTick = tick;
                    clockKnown = true;
                }

                bool changed = history.Expire(tick);
                long oldest = Math.Max(lastHead + 1, head - NativeFlightSamples.Capacity + 1);
                int epoch = native.Generation;
                for (long sequence = oldest; sequence <= head; sequence++)
                {
                    if (!native.TryRead(sequence, epoch, out var sample))
                    {
                        lastHead = sequence;
                        continue;
                    }
                    uint age = unchecked(tick - sample.Tick);
                    if (age > int.MaxValue) break; // The next snapshot publishes this later tick.
                    lastHead = sequence;
                    if (age < TrailBuffer.Lifetime) changed |= AddEcho(sample);
                }

                Camera current = Camera.main;
                if (current == null)
                {
                    HideMesh();
                    return;
                }
                Matrix4x4 matrix = current.cullingMatrix;
                if (!changed && camera == current && cameraMatrix.Equals(matrix)) return;
                camera = current;
                cameraMatrix = matrix;

                long allocatedBefore = 0;
                SampleMeshAllocation(ref allocatedBefore);
                LastSegmentCount = meshes.Publish(history, current, axisX, axisY);
                RecordMeshAllocation(allocatedBefore);
                TracePublish(current);
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

        private static bool AddEcho(NativeFlightSamples.EntrySample sample)
        {
            const int unitsPerTile = 8;
            if (sample.X < 0 || sample.Y < 0 || sample.Frame == 0) return false;
            map.mapGameTileToTilemapCoord(sample.X / unitsPerTile, sample.Y / unitsPerTile, out int x, out int y);
            if (map.getMapTile(x, y) == null) return false;
            int appearance = meshes.GetAppearance(sample.File, sample.Frame);
            if (appearance < 0) return false;

            native.Subtile(sample.X, sample.Y, rotation, out int subX, out int subY);
            var position = map.getSpritePosVector(x, y, subX, subY, sample.Slot);
            position.y += sample.Height / NativeHeightScale;
            int row = map.getRow(x, y, ProjectileLayerDelay);
            var echo = new TrailBuffer.Sample
            {
                Appearance = appearance,
                SortKey = TrailBuffer.Key(sortLayer, -20000 + row * 49 + 15),
                ObjectLayer = objectLayer,
                Position = new Position(position.x, position.y, position.z),
                Tick = sample.Tick,
                SourceSlot = sample.Slot
            };
            RememberEchoDetails(ref echo, sample, x, y, subX, subY);
            history.Append(echo);
            return true;
        }

        private static void HideMesh()
        {
            meshes?.Hide();
            camera = null;
            LastSegmentCount = 0;
        }

        private static void ResetView()
        {
            RestartCapture(false);
            frame?.SetContinuous(false);
            map = null;
            styleKnown = false;
        }

        private static void Fail(string reason)
        {
            if (failed) return;
            failed = true;
            native?.Reset(false);
            frame?.Request();
            log.LogError($"[{DateTime.Now:HH:mm:ss.fff}] ProjectileTrailsTest disabled. {reason}");
        }

        internal static void WriteInfo(string message) =>
            log.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] ProjectileTrailsTest {message}");

        static partial void StartAutomation(ModApiClient api);
        static partial void InitializeAllocationCounters();
        static partial void SampleMeshAllocation(ref long allocated);
        static partial void RecordMeshAllocation(long before);
        static partial void TraceVanilla(int id, int x, int y, int subX, int subY, float height, int file,
            int image, int transparency, int layerDelay, int kind, Dictionary<int, Fly> flies);
        static partial void RememberEchoDetails(ref TrailBuffer.Sample echo, NativeFlightSamples.EntrySample sample, int x, int y, int subX, int subY);
        static partial void TracePublish(Camera current);
    }
}
