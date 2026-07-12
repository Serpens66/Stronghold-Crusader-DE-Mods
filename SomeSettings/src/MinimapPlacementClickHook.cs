using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;

namespace SomeSettings
{
    internal sealed class MinimapPlacementClickHook : IDisposable
    {
        private delegate void RadarScrollMapDelegate(FatControler self);

        private static readonly FieldInfo RadarClickDelayField = FindField("radarClickDelay");
        private static readonly FieldInfo RadarClickDelayTimeField = FindField("radarClickDelayTime");
        private static readonly FieldInfo RadarScrollTriggeredField = FindField("radarScrollTrigged");
        private static readonly FieldInfo NgMousePointField = FindField("NGMousePoint");
        private static readonly FieldInfo LastNgMousePointField = FindField("LastNGMousePoint");

        private readonly ManualLogSource log;
        private readonly SomeSettingsViewModel settings;
        private readonly Hook hook;
        private readonly RadarScrollMapDelegate trampoline;
        private bool disposed;

        public MinimapPlacementClickHook(ManualLogSource log, SomeSettingsViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            hook = new Hook(FindRadarScrollMapMethod(), (RadarScrollMapDelegate)RadarScrollMapHook);
            trampoline = hook.GenerateTrampoline<RadarScrollMapDelegate>();
            Shared.DebugLogHelper.LogDebug(log, "SomeSettings minimap placement click hook installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            hook?.Undo();
            hook.Dispose();
            Shared.DebugLogHelper.LogDebug(log, "SomeSettings minimap placement click hook disposed.");
        }

        private static MethodInfo FindRadarScrollMapMethod()
        {
            MethodInfo method = typeof(FatControler).GetMethod(
                "RadarScrollMap",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);

            if (method == null)
                throw new MissingMethodException(typeof(FatControler).FullName, "RadarScrollMap");

            return method;
        }

        private static FieldInfo FindField(string fieldName)
        {
            FieldInfo field = typeof(FatControler).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field == null)
                throw new MissingFieldException(typeof(FatControler).FullName, fieldName);

            return field;
        }

        private void RadarScrollMapHook(FatControler self)
        {
            trampoline(self);

            try
            {
                TryHandlePlacementMinimap(self);
                FollowMinimapCursor(self);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"SomeSettings minimap placement click hook failed: {ex}");
            }
        }

        private void TryHandlePlacementMinimap(FatControler self)
        {
            if (!settings.EnableMod || !settings.AllowMinimapWhilePlacingBuilding)
                return;

            if (self == null || MainControls.instance == null || MainControls.instance.CurrentAction != (int)Enums.editorActions.placingBuilding)
                return;

            if (!MainViewModel.viewModelLoaded || MainViewModel.Instance == null || MainViewModel.Instance.Show_HUD_Briefing)
                return;

            if (!MainControls.instance.IsUIVisible)
                return;

            if (GetBool(self, RadarClickDelayField) && DateTime.UtcNow < GetDateTime(self, RadarClickDelayTimeField))
                return;

            if (GameData.Instance == null || GameData.Instance.lastGameState == null)
                return;

            if (FatControler.currentScene != Enums.SceneIDS.ActualMainGame)
                return;

            if (!MainViewModel.Instance.RadarLoaded || !MainViewModel.Instance.MainUILoaded)
                return;

            if (!self.mouseIsDown)
            {
                SetBool(self, RadarScrollTriggeredField, false);
                return;
            }

            Noesis.Point mousePoint = GetPoint(self, NgMousePointField);
            if (FatControler.MouseIsDownStroke && IsOutsideRadar(self, mousePoint))
                return;

            if (FatControler.MouseIsDownStroke)
            {
                HandleClickStroke(self);
                return;
            }

            HandleDrag(self);
        }

        private void FollowMinimapCursor(FatControler self)
        {
            if (!settings.EnableMod || self == null || KeyManager.instance == null)
                return;

            if (!self.mouseIsDown || !GetBool(self, RadarScrollTriggeredField))
                return;

            if (FatControler.MouseIsDownStroke)
                return;

            Noesis.Point mousePoint = GetPoint(self, NgMousePointField);
            if (IsOutsideRadar(self, mousePoint))
            {
                KeyManager.instance.RadarHeldX = 0f;
                KeyManager.instance.RadarHeldY = 0f;
                return;
            }

            KeyManager.instance.RadarHeldX = 0f;
            KeyManager.instance.RadarHeldY = 0f;
            EngineInterface.GameAction(
                Enums.GameActionCommand.RadarClicked,
                (int)(mousePoint.X * self.SHRadarScalar),
                (int)(mousePoint.Y * self.SHRadarScalar));
        }

        private static void HandleClickStroke(FatControler self)
        {
            if (MainViewModel.Instance.HUDRoot.RefRadarME.Opacity != 0f)
            {
                MainViewModel.Instance.HUDRoot.RefRadarME.Opacity = 0f;
                FatControler.MouseIsDownStroke = false;
                SetBool(self, RadarScrollTriggeredField, false);
                return;
            }

            Noesis.Point mousePoint = GetPoint(self, NgMousePointField);
            SetBool(self, RadarScrollTriggeredField, true);
            SetPoint(self, LastNgMousePointField, mousePoint);
            EngineInterface.GameAction(
                Enums.GameActionCommand.RadarClicked,
                (int)(mousePoint.X * self.SHRadarScalar),
                (int)(mousePoint.Y * self.SHRadarScalar));
        }

        private static void HandleDrag(FatControler self)
        {
            if (!GetBool(self, RadarScrollTriggeredField) || KeyManager.instance == null)
                return;

            Noesis.Point mousePoint = GetPoint(self, NgMousePointField);
            Noesis.Point lastMousePoint = GetPoint(self, LastNgMousePointField);
            float deltaX = mousePoint.X - lastMousePoint.X;
            float deltaY = lastMousePoint.Y - mousePoint.Y;

            if (deltaX == 0f && deltaY != 0f)
            {
                KeyManager.instance.RadarHeldY = deltaY > 0f ? 1f : -1f;
                return;
            }

            if (deltaY == 0f && deltaX != 0f)
            {
                KeyManager.instance.RadarHeldX = deltaX > 0f ? 1f : -1f;
                return;
            }

            if (Math.Abs(deltaX) > Math.Abs(deltaY))
            {
                float ratioY = Math.Abs(deltaY / deltaX);
                KeyManager.instance.RadarHeldX = deltaX > 0f ? 1f : -1f;
                KeyManager.instance.RadarHeldY = deltaY > 0f ? ratioY : -ratioY;
            }
            else if (Math.Abs(deltaY) > 0f)
            {
                float ratioX = Math.Abs(deltaX / deltaY);
                KeyManager.instance.RadarHeldX = deltaX > 0f ? ratioX : -ratioX;
                KeyManager.instance.RadarHeldY = deltaY > 0f ? 1f : -1f;
            }
        }

        private static bool IsOutsideRadar(FatControler self, Noesis.Point mousePoint)
        {
            return mousePoint.X < 0f
                || mousePoint.X >= self.SHRadarRectSize
                || mousePoint.Y < 0f
                || mousePoint.Y >= self.SHRadarRectSize;
        }

        private static bool GetBool(FatControler self, FieldInfo field)
        {
            return (bool)field.GetValue(self);
        }

        private static void SetBool(FatControler self, FieldInfo field, bool value)
        {
            field.SetValue(self, value);
        }

        private static DateTime GetDateTime(FatControler self, FieldInfo field)
        {
            return (DateTime)field.GetValue(self);
        }

        private static Noesis.Point GetPoint(FatControler self, FieldInfo field)
        {
            return (Noesis.Point)field.GetValue(self);
        }

        private static void SetPoint(FatControler self, FieldInfo field, Noesis.Point value)
        {
            field.SetValue(self, value);
        }
    }
}
