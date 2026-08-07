// Feature: Shared native/managed hook infrastructure for the two minimap improvements.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed partial class MinimapPlacementClickHook : IDisposable
    {
        private delegate void RadarScrollMapDelegate(FatControler self);

        private static readonly FieldInfo RadarClickDelayField = FindField("radarClickDelay");
        private static readonly FieldInfo RadarClickDelayTimeField = FindField("radarClickDelayTime");
        private static readonly FieldInfo RadarScrollTriggeredField = FindField("radarScrollTrigged");
        private static readonly FieldInfo NgMousePointField = FindField("NGMousePoint");
        private static readonly FieldInfo LastNgMousePointField = FindField("LastNGMousePoint");

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly Hook hook;
        private readonly RadarScrollMapDelegate trampoline;
        private bool disposed;

        public MinimapPlacementClickHook(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            hook = new Hook(FindRadarScrollMapMethod(), (RadarScrollMapDelegate)RadarScrollMapHook);
            trampoline = hook.GenerateTrampoline<RadarScrollMapDelegate>();
            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL minimap placement click hook installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            hook?.Undo();
            hook.Dispose();
            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL minimap placement click hook disposed.");
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
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL minimap placement click hook failed: {ex}");
            }
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
