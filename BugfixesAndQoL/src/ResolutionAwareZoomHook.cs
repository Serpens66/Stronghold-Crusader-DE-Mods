// Feature: Resolution-normalized zoom and two additional close camera steps.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal sealed class ResolutionAwareZoomHook
    {
        private delegate void AdjustZoomDelegate(PerfectPixelWithZoom self, float adjustment, bool loop);
        private delegate void ZoomDelegate(PerfectPixelWithZoom self, float zoomTo);
        private delegate void SetZoomImmediateDelegate(PerfectPixelWithZoom self, float scale);
        private delegate void UpdateDelegate(PerfectPixelWithZoom self);

        private static readonly FieldInfo ZoomPositionField = FindFloatField("zoomPos");
        private static readonly FieldInfo PixelsPerUnitScaleField = FindFloatField("pixelsPerUnitScale");
        private static readonly FieldInfo ZoomCurrentValueField = FindFloatField("zoomCurrentValue");
        private static readonly FieldInfo ZoomNextValueField = FindFloatField("zoomNextValue");

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private Hook adjustZoomHook;
        private Hook zoomHook;
        private Hook setZoomImmediateHook;
        private Hook updateHook;
        private AdjustZoomDelegate adjustZoomOriginal;
        private ZoomDelegate zoomOriginal;
        private SetZoomImmediateDelegate setZoomImmediateOriginal;
        private UpdateDelegate updateOriginal;
        private PerfectPixelWithZoom trackedInstance;
        private float appliedResolutionScale = 1f;
        private bool appliedEnabled;

        internal ResolutionAwareZoomHook(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            try
            {
                zoomHook = new Hook(FindMethod("Zoom", typeof(float)), (ZoomDelegate)ZoomHook);
                zoomOriginal = zoomHook.GenerateTrampoline<ZoomDelegate>();
                setZoomImmediateHook = new Hook(
                    FindMethod(nameof(PerfectPixelWithZoom.SetZoomImmediate), typeof(float)),
                    (SetZoomImmediateDelegate)SetZoomImmediateHook);
                setZoomImmediateOriginal =
                    setZoomImmediateHook.GenerateTrampoline<SetZoomImmediateDelegate>();
                adjustZoomHook = new Hook(
                    FindMethod(nameof(PerfectPixelWithZoom.adjustZoom), typeof(float), typeof(bool)),
                    (AdjustZoomDelegate)AdjustZoomHook);
                adjustZoomOriginal = adjustZoomHook.GenerateTrampoline<AdjustZoomDelegate>();
                updateHook = new Hook(FindMethod("Update"), (UpdateDelegate)UpdateHook);
                updateOriginal = updateHook.GenerateTrampoline<UpdateDelegate>();
            }
            catch
            {
                RollbackFailedInitialization();
                throw;
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                "Bugfixes and QoL resolution-aware extended zoom hooks installed for the process lifetime.");
        }

        private bool Enabled =>
            settings.EnableClientFeatures && settings.EnableResolutionAwareExtendedZoom;

        private void AdjustZoomHook(PerfectPixelWithZoom self, float adjustment, bool loop)
        {
            if (!Enabled)
            {
                adjustZoomOriginal(self, adjustment, loop);
                return;
            }

            ReconcileState(self);
            if (GameData.Instance.game_type == 4)
                EngineInterface.TutorialAction(3);

            float position = ResolutionAwareZoomPolicy.ResolvePosition(
                GetFloat(ZoomPositionField, self),
                adjustment,
                CameraControls2D.instance.isMapLocked(),
                self.CanUserExtraZoom(),
                MainViewModel.Instance.IsMapEditorMode,
                loop);
            SetFloat(ZoomPositionField, self, position);
            ApplyZoomTarget(self, position);
        }

        private void ZoomHook(PerfectPixelWithZoom self, float zoomTo)
        {
            ReconcileState(self);
            zoomOriginal(self, zoomTo);
            if (Enabled)
                ScaleZoomTarget(self, appliedResolutionScale);
        }

        private void SetZoomImmediateHook(PerfectPixelWithZoom self, float scale)
        {
            bool enabled = Enabled;
            float resolutionScale = enabled
                ? ResolutionAwareZoomPolicy.GetResolutionScale(Screen.height)
                : 1f;
            // Let Vanilla apply its base min/max and map-size cap first, then normalize the
            // resulting supported scale. This avoids applying the 0.5 minimum to an already
            // resolution-scaled value and also keeps the normalization valid above 4K.
            setZoomImmediateOriginal(self, scale);
            if (enabled && Math.Abs(resolutionScale - 1f) > 0.0001f)
            {
                ScaleField(PixelsPerUnitScaleField, self, resolutionScale);
                self.UpdateCameraScale();
            }
            TrackState(self, enabled, resolutionScale);
        }

        private void UpdateHook(PerfectPixelWithZoom self)
        {
            updateOriginal(self);
            ReconcileState(self);
        }

        private void ApplyZoomTarget(PerfectPixelWithZoom self, float position)
        {
            zoomOriginal(self, position);
            ScaleZoomTarget(self, appliedResolutionScale);
        }

        private static void ScaleZoomTarget(PerfectPixelWithZoom self, float resolutionScale)
        {
            SetFloat(
                ZoomNextValueField,
                self,
                GetFloat(ZoomNextValueField, self) * resolutionScale);
        }

        private void ReconcileState(PerfectPixelWithZoom self)
        {
            if (self == null)
                return;

            if (trackedInstance != self)
                TrackState(self, enabled: false, resolutionScale: 1f);

            bool enabled = Enabled;
            float targetScale = enabled
                ? ResolutionAwareZoomPolicy.GetResolutionScale(Screen.height)
                : 1f;
            bool scaleChanged = Math.Abs(targetScale - appliedResolutionScale) > 0.0001f;
            bool enabledChanged = enabled != appliedEnabled;
            if (!scaleChanged && !enabledChanged)
                return;

            if (scaleChanged)
            {
                float ratio = targetScale / appliedResolutionScale;
                ScaleField(PixelsPerUnitScaleField, self, ratio);
                ScaleField(ZoomCurrentValueField, self, ratio);
                ScaleField(ZoomNextValueField, self, ratio);
            }

            if (!enabled && GetFloat(ZoomPositionField, self) >
                ResolutionAwareZoomPolicy.VanillaLockedMaximumPosition)
            {
                SetFloat(
                    ZoomPositionField,
                    self,
                    ResolutionAwareZoomPolicy.VanillaLockedMaximumPosition);
                zoomOriginal(self, ResolutionAwareZoomPolicy.VanillaLockedMaximumPosition);
            }

            TrackState(self, enabled, targetScale);
            self.UpdateCameraScale();
            Shared.DebugLogHelper.LogDebug(
                log,
                $"Resolution-aware zoom reconciled: enabled={enabled}, screenHeight={Screen.height}, factor={targetScale:0.###}.");
        }

        private void TrackState(
            PerfectPixelWithZoom self,
            bool enabled,
            float resolutionScale)
        {
            trackedInstance = self;
            appliedEnabled = enabled;
            appliedResolutionScale = resolutionScale;
        }

        private static void ScaleField(FieldInfo field, PerfectPixelWithZoom self, float ratio) =>
            SetFloat(field, self, GetFloat(field, self) * ratio);

        private static float GetFloat(FieldInfo field, PerfectPixelWithZoom self) =>
            (float)field.GetValue(self);

        private static void SetFloat(FieldInfo field, PerfectPixelWithZoom self, float value) =>
            field.SetValue(self, value);

        private static FieldInfo FindFloatField(string name)
        {
            FieldInfo field = typeof(PerfectPixelWithZoom).GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || field.FieldType != typeof(float))
                throw new MissingFieldException(typeof(PerfectPixelWithZoom).FullName, name);
            return field;
        }

        private static MethodInfo FindMethod(string name, params Type[] parameters)
        {
            MethodInfo method = typeof(PerfectPixelWithZoom).GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                parameters,
                null);
            if (method == null || method.ReturnType != typeof(void))
                throw new MissingMethodException(typeof(PerfectPixelWithZoom).FullName, name);
            return method;
        }

        private void RollbackFailedInitialization()
        {
            RollbackHook(ref updateHook);
            RollbackHook(ref adjustZoomHook);
            RollbackHook(ref setZoomImmediateHook);
            RollbackHook(ref zoomHook);
        }

        private static void RollbackHook(ref Hook hook)
        {
            if (hook == null)
                return;

            hook.Undo();
            hook.Dispose();
            hook = null;
        }
    }
}
