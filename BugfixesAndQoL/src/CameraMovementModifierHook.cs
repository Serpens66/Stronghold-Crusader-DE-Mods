// Feature: Keep keyboard and edge camera movement active while Ctrl or Alt is held.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal sealed class CameraMovementModifierHook : IDisposable
    {
        private delegate float AxisDelegate(KeyManager self);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private Hook horizontalHook;
        private Hook verticalHook;
        private AxisDelegate horizontalTrampoline;
        private AxisDelegate verticalTrampoline;
        private bool disposed;

        public CameraMovementModifierHook(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            try
            {
                horizontalHook = new Hook(FindAxisMethod(nameof(KeyManager.HorizontalAxis)), (AxisDelegate)HorizontalAxisHook);
                horizontalTrampoline = horizontalHook.GenerateTrampoline<AxisDelegate>();
                verticalHook = new Hook(FindAxisMethod(nameof(KeyManager.VerticalAxis)), (AxisDelegate)VerticalAxisHook);
                verticalTrampoline = verticalHook.GenerateTrampoline<AxisDelegate>();
            }
            catch
            {
                Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL camera movement modifier hooks installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            verticalHook?.Undo();
            verticalHook?.Dispose();
            verticalHook = null;
            horizontalHook?.Undo();
            horizontalHook?.Dispose();
            horizontalHook = null;
            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL camera movement modifier hooks disposed.");
        }

        private static MethodInfo FindAxisMethod(string methodName)
        {
            MethodInfo method = typeof(KeyManager).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);

            if (method == null || method.ReturnType != typeof(float))
                throw new MissingMethodException(typeof(KeyManager).FullName, methodName);

            return method;
        }

        private float HorizontalAxisHook(KeyManager self)
        {
            if (!settings.EnableClientFeatures || !settings.AllowCameraMovementWithModifiers)
                return horizontalTrampoline(self);

            float radarHeldX = self.RadarHeldX;
            self.RadarHeldX = 0f;
            if (!Director.instance.SimRunning && CameraControls2D.instance.isMapLocked())
                return 0f;
            if (MainViewModel.Instance.Show_HUD_LoadSaveRequester || Director.instance.Paused)
                return 0f;

            // Mirror Vanilla after its Ctrl/Alt early return so all other movement rules stay intact.
            bool left = self.IsActionHeldDown(Enums.KeyFunctions.Left, ignoreModifiers: true);
            bool right = self.IsActionHeldDown(Enums.KeyFunctions.Right, ignoreModifiers: true);
            float speed = ConfigSettings.GetScrollSpeed();
            if (self.isShiftDown())
                speed *= 2f;
            if (left && right)
                return 0f;
            if (left)
                return -speed;
            if (right)
                return speed;
            if (ConfigSettings.Settings_PushMapScrolling)
            {
                if (Input.mousePosition.x <= 0f)
                    return -speed;
                if (Input.mousePosition.x >= Screen.width - 1)
                    return speed;
            }

            return radarHeldX;
        }

        private float VerticalAxisHook(KeyManager self)
        {
            if (!settings.EnableClientFeatures || !settings.AllowCameraMovementWithModifiers)
                return verticalTrampoline(self);

            float radarHeldY = self.RadarHeldY;
            self.RadarHeldY = 0f;
            if (!Director.instance.SimRunning && CameraControls2D.instance.isMapLocked())
                return 0f;
            if (MainViewModel.Instance.Show_HUD_LoadSaveRequester || Director.instance.Paused)
                return 0f;

            // Mirror Vanilla after its Ctrl/Alt early return so all other movement rules stay intact.
            bool up = self.IsActionHeldDown(Enums.KeyFunctions.Up, ignoreModifiers: true);
            bool down = self.IsActionHeldDown(Enums.KeyFunctions.Down, ignoreModifiers: true);
            float speed = ConfigSettings.GetScrollSpeed();
            if (self.isShiftDown())
                speed *= 2f;
            if (up && down)
                return 0f;
            if (up)
                return speed;
            if (down)
                return -speed;
            if (ConfigSettings.Settings_PushMapScrolling)
            {
                if (Input.mousePosition.y <= 0f)
                    return -speed;
                if (Input.mousePosition.y >= Screen.height - 1)
                    return speed;
            }

            return radarHeldY;
        }
    }
}
