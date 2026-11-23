using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using APIShared;
using APIShared.GameModes;
using APIShared.Multiplayer;
using APIShared.Presentation;
using APIShared.Savegames;
using BepInEx.Logging;
using HarmonyLib;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop.Enums;
using Shared;
using UnityEngine;
using Position = System.Numerics.Vector3;
using Axis = System.Numerics.Vector2;

namespace BugfixesAndQoL.ProjectileTrails
{
    internal static class ProjectileTrailRuntime
    {
        private const string Owner = BugfixesAndQoLPlugin.PluginGuid;
        private const int WideProjectileLimit = TrailBuffer.WideProjectileLimit;
        private const int ProjectileLayerDelay = 1;
        private const float NativeHeightScale = 32f;
        private static ManualLogSource log;
        private static BugfixesAndQoLViewModel settings;
        private static int afterimageCount = TrailBuffer.Lifetime;
        private static int maximumVisible = TrailBuffer.MaximumVisibleCopies;
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
        private static bool renderingEnabled, styleKnown, firstVisible;
        private static volatile bool nativeReady, presentationReady, presentationFailed;
        private static long localPreparedSession;
        private static bool localReplacement;

        internal static bool SessionExtreme { get; private set; }
        private static bool replaceStandard;
        private static volatile bool multiplayerReplacement;
        private static bool UsesModTrails => SessionExtreme || replaceStandard;
        internal static int LastSegmentCount { get; private set; }
        private static bool CanDrawTrails => renderingEnabled && afterimageCount > 0 && maximumVisible > 0;

        internal static void Configure(ManualLogSource logger, BugfixesAndQoLViewModel model)
        {
            log = logger;
            settings = model;
            api = ApiShared.ForMod(Owner);
            SimulationAgreement.Register(Owner, "standard-projectile-trails", required: false,
                CanReplaceNative, ApplyMultiplayerReplacement);
        }

        // An unsupported renderer keeps Standard Vanilla at admission. A local failure after
        // admission must not alter the rule, and an intentional local display-off is supported.
        private static bool CanReplaceNative() =>
            nativeReady && presentationReady && !presentationFailed &&
            settings.EnableMod && settings.ReplaceStandardProjectileAfterimages && native.IsIntact;

        private static void ApplyMultiplayerReplacement(bool enabled)
        {
            multiplayerReplacement = enabled;
            native?.SuppressNativeAfterimages(enabled);
        }

        internal static void Install(CrusaderLibraryLoadContext context)
        {
            try
            {
                if (api == null) return;
                // Root the buffer and hook before publication, even if the backend later reports failure.
                native = new NativeFlightSamples(context, Owner, log);
                native.Install(context);
                api.WhenReady(_ =>
                {
                    if (!api.Ui.TryPost(Initialize)) FailPresentation("Unity dispatch unavailable");
                });
            }
            catch (Exception ex) { LogError("Native projectile trails are unavailable. " + ex); }
        }

        private static void Initialize()
        {
            try
            {
                if (!api.TryGetMissionLifecycle(out missions, out var diagnostic))
                    throw new InvalidOperationException(diagnostic.Reason);
                if (!NativeWorldRestoreEvents.TryRegister(Owner, "projectile-trails", OnWorldRestore, out string reason))
                    throw new InvalidOperationException(reason);
                if (!missions.TryRegisterObserver("projectile-trails", MissionStarted, MissionEnded, PrepareMission, out diagnostic))
                    throw new InvalidOperationException(diagnostic.Reason);
                nativeReady = true;
            }
            catch (Exception ex)
            {
                LogError("Projectile trails could not register mission boundaries. " + ex);
                return;
            }

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

                if (!api.Ui.TryRegisterBeforeRender("projectile-trails", BeforeRender, out frame, out string reason))
                    throw new InvalidOperationException(reason);
                meshes = new TrailMeshes(Path.Combine(
                    Path.GetDirectoryName(typeof(BugfixesAndQoLPlugin).Assembly.Location), TrailMeshes.BundleName));
                // APIShared's popularity-popup IL edit stays in the original body.
                new Harmony(Owner + ".ProjectileTrails").Patch(add,
                    postfix: new HarmonyMethod(typeof(ProjectileTrailRuntime), nameof(AfterAdd)));
                presentationReady = true;
                SynchronizeMission();
            }
            catch (Exception ex) { FailPresentation(ex.ToString()); }
        }

        private static void PrepareMission(MissionLifecycleNotification notification)
        {
            if (notification.Phase == MissionInitializationPhase.BeforeNativeStart && !notification.Context.IsSave)
                PrepareLocalReplacement(notification.Context);
        }

        private static bool OnWorldRestore(NativeWorldRestoreContext restore)
        {
            if (restore.Mission?.IsSave == true) PrepareLocalReplacement(restore.Mission);
            return true;
        }

        private static void PrepareLocalReplacement(MissionContext current)
        {
            var mode = current.Mode;
            if (!nativeReady || current.IsEditor || mode.IsRealMultiplayer || mode.MultiplayerSave ||
                mode.Kind == GameModeKind.Unknown || localPreparedSession == current.SessionId) return;
            localReplacement = CanReplaceNative();
            localPreparedSession = current.SessionId;
            native.SuppressNativeAfterimages(localReplacement);
        }

        private static void MissionStarted(MissionLifecycleNotification notification)
        {
            if (!nativeReady) return;
            if (!native.IsIntact)
            {
                if (multiplayerReplacement)
                    SimulationAgreement.ReportFault("Projectile afterimage hook lost its agreed native entry.");
                else
                    native.SuppressNativeAfterimages(false);
                FailPresentation("Afterimage observer was overwritten.");
                return;
            }
            // Some local mission entry points do not publish either early boundary.
            PrepareLocalReplacement(notification.Context);
            QueuePresentation();
        }

        private static void MissionEnded(MissionLifecycleNotification notification)
        {
            if (localPreparedSession == notification.Context.SessionId)
            {
                native.SuppressNativeAfterimages(false);
                localPreparedSession = 0;
                localReplacement = false;
            }
            native.Reset(false);
            QueuePresentation();
        }

        private static void QueuePresentation()
        {
            if (presentationReady && !api.Ui.TryPost(SynchronizeMission))
                FailPresentation("Mission notification could not reach Unity");
        }

        private static void SynchronizeMission()
        {
            if (presentationFailed) return;
            var current = missions.Current;
            long next = current?.SessionId ?? 0;
            if (session == next) return;
            ResetView();
            session = next;
            SessionExtreme = false;
            replaceStandard = false;
            firstVisible = false;
            if (current == null || current.IsEditor) return;
            afterimageCount = settings.ProjectileTrailLength;
            maximumVisible = settings.ProjectileTrailMaximum;
            renderingEnabled = settings.EnableClientFeatures && settings.EnableProjectileTrails;
            try
            {
                SessionExtreme = EngineInterface.getMultiplayerStartingData().extreme_troops != 0;
                replaceStandard = !SessionExtreme && (multiplayerReplacement ||
                    (localPreparedSession == next && localReplacement));
                RefreshCapture();
                WriteInfo($"mission {session} extreme={SessionExtreme} replaceStandard={replaceStandard} enabled={renderingEnabled}");
            }
            catch (Exception ex) { FailPresentation(ex.ToString()); }
        }

        private static void AfterAdd(int __0, int __11, Dictionary<int, Fly> ___flies)
        {
            if (!presentationReady || presentationFailed || styleKnown || !UsesModTrails || !CanDrawTrails ||
                __0 <= 0 || __0 >= WideProjectileLimit ||
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
            catch (Exception ex) { FailPresentation(ex.ToString()); }
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
            bool active = !presentationFailed && session != 0 && UsesModTrails && CanDrawTrails;
            RestartCapture(active);
            frame?.SetContinuous(active);
        }

        private static void BeforeRender()
        {
            if (presentationFailed || missions.Current?.SessionId != session)
            {
                ResetView();
                return;
            }
            if (!UsesModTrails || !CanDrawTrails || native == null) return;
            try
            {
                GameMap currentMap = GameMap.instance;
                if (currentMap == null || currentMap.pendingRotationtriggered)
                {
                    if (native.Generation != 0) RestartCapture(false);
                    styleKnown = false;
                    map = null;
                    return;
                }

                var currentRotation = currentMap.CurrentRotation();
                if (map != currentMap || rotation != currentRotation)
                {
                    if (map != null) styleKnown = false;
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

                bool changed = history.Expire(tick, afterimageCount);
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
                    if (age < (uint)afterimageCount) changed |= AddEcho(sample);
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

                LastSegmentCount = meshes.Publish(history, current, axisX, axisY, afterimageCount, maximumVisible);
                if (!firstVisible && LastSegmentCount > 0)
                {
                    firstVisible = true;
                    WriteInfo($"rendered first trails in mission {session}");
                }
            }
            catch (Exception ex)
            {
                ResetView();
                FailPresentation(ex.ToString());
            }
        }

        private static bool AddEcho(NativeFlightSamples.EntrySample sample)
        {
            const int unitsPerTile = 8;
            if (sample.X < 0 || sample.Y < 0 || sample.Frame == 0) return false;
            map.mapGameTileToTilemapCoord(sample.X / unitsPerTile, sample.Y / unitsPerTile, out int x, out int y);
            var tile = map.getMapTile(x, y);
            if (tile == null) return false;
            int appearance = meshes.GetAppearance(sample.File, sample.Frame);
            if (appearance < 0) return false;

            native.Subtile(sample.X, sample.Y, rotation, out int subX, out int subY);
            var position = map.getSpritePosVector(x, y, subX, subY, sample.Slot);
            position.y += sample.Height / NativeHeightScale;
            // Same clamp as GameMap.getRow, using the tile already read above.
            int row = Math.Min(tile.row + ProjectileLayerDelay, GameMap.tilemapSize);
            var echo = new TrailBuffer.Sample
            {
                Appearance = appearance,
                SortKey = TrailBuffer.Key(sortLayer, -20000 + row * 49 + 15),
                ObjectLayer = objectLayer,
                Position = new Position(position.x, position.y, position.z),
                Tick = sample.Tick,
                SourceSlot = sample.Slot
            };
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

        private static void FailPresentation(string reason)
        {
            if (presentationFailed) return;
            presentationFailed = true;
            native?.Reset(false);
            frame?.Request();
            LogError("Local drawing disabled. " + reason);
        }

        private static void WriteInfo(string message) =>
            DebugLogHelper.LogInfo(log, "Projectile trails: " + message);

        private static void LogError(string message) =>
            DebugLogHelper.LogError(log, "Projectile trails: " + message);
    }
}
