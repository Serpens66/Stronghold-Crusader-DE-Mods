// Shared convention and layout for independently installed troop-action buttons.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace Shared
{
    public static class TroopActionButtonLayout
    {
        public const string HostNamePrefix = TroopActionButtonLayoutPolicy.HostNamePrefix;
        public const int ButtonWidth = 36;
        public const int ButtonHeight = 37;
        public const int SlotStep = 40;
        private const int MaximumCandidateSlots = 10;
        private const string WantsVisibilityProperty = "WantsVisibility";
        private const string LayoutAvailableProperty = "LayoutAvailable";
        private static readonly long DiagnosticIntervalTicks = Math.Max(1L, Stopwatch.Frequency * 2L);

        private static readonly HashSet<string> overflowLogged =
            new HashSet<string>(StringComparer.Ordinal);
        private static long nextDiagnosticTimestamp;

        public static void Reflow(HUD_Troops troopPanel, ManualLogSource log)
        {
            if (troopPanel == null)
                return;

            FrameworkElement controls = troopPanel.FindName("UnitControls") as FrameworkElement;
            if (controls == null)
                return;

            var hosts = new List<ActionHost>();
            CollectActionHosts(controls, hosts);
            if (hosts.Count == 0)
                return;

            hosts.Sort(ActionHostComparer.Instance);
            var occupied = new List<ScreenRectangle>();
            CollectOccupiedRectangles(controls, occupied);
            var assigned = new List<ScreenRectangle>();
            bool hasControlsBounds = TryGetScreenRectangle(controls, out ScreenRectangle controlsBounds);
            int wantedCount = 0;
            int testedSlots = 0;
            int invalidRectangles = 0;
            int boundsRejections = 0;
            int collisionRejections = 0;
            int unavailableCount = 0;

            for (int index = 0; index < hosts.Count; index++)
            {
                ActionHost host = hosts[index];
                if (!host.WantsVisibility)
                {
                    SetLayoutAvailable(host, true);
                    continue;
                }
                wantedCount++;

                bool placed = false;
                for (int slot = 0; slot < MaximumCandidateSlots; slot++)
                {
                    testedSlots++;
                    host.Element.RenderTransform = new TranslateTransform(-SlotStep * slot, 0f);
                    if (!TryGetScreenRectangle(host.Element, out ScreenRectangle candidate))
                    {
                        invalidRectangles++;
                        continue;
                    }
                    if (hasControlsBounds && !controlsBounds.Contains(candidate))
                    {
                        boundsRejections++;
                        continue;
                    }
                    if (IntersectsAny(candidate, occupied) || IntersectsAny(candidate, assigned))
                    {
                        collisionRejections++;
                        continue;
                    }

                    assigned.Add(candidate);
                    SetLayoutAvailable(host, true);
                    placed = true;
                    overflowLogged.Remove(host.ActionId);
                    break;
                }

                if (placed)
                    continue;

                SetLayoutAvailable(host, false);
                unavailableCount++;
                if (log != null && overflowLogged.Add(host.ActionId))
                {
                    DebugLogHelper.LogWarning(
                        log,
                        $"Troop action '{host.ActionId}' is hidden because no collision-free UnitControls slot is available; " +
                        $"effectiveOccupied={occupied.Count}, testedSlots={MaximumCandidateSlots}, " +
                        $"invalidRectangles={invalidRectangles}, boundsRejections={boundsRejections}, collisionRejections={collisionRejections}.");
                }
            }

            LogDiagnosticIfDue(
                log,
                hosts.Count,
                wantedCount,
                occupied.Count,
                assigned.Count,
                unavailableCount,
                testedSlots,
                invalidRectangles,
                boundsRejections,
                collisionRejections,
                hasControlsBounds);
        }

        private static void CollectActionHosts(DependencyObject parent, List<ActionHost> result)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int index = 0; index < childCount; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                if (child is FrameworkElement element &&
                    TroopActionButtonLayoutPolicy.TryParseHostName(element.Name, out int priority, out string actionId) &&
                    TryResolveVisibilityContract(element, out PropertyInfo wantsProperty, out PropertyInfo layoutProperty, out bool wantsVisibility))
                {
                    result.Add(new ActionHost(element, priority, actionId, wantsProperty, layoutProperty, wantsVisibility));
                }
                CollectActionHosts(child, result);
            }
        }

        private static void CollectOccupiedRectangles(DependencyObject parent, List<ScreenRectangle> result)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int index = 0; index < childCount; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                if (child is FrameworkElement element)
                {
                    if (TroopActionButtonLayoutPolicy.TryParseHostName(element.Name, out _, out _))
                        continue;

                    // IsVisible includes hidden ancestors; Visibility alone makes inactive
                    // Vanilla action groups look like occupied slots.
                    if (element is Button &&
                        TroopActionButtonLayoutPolicy.IsEffectivelyOccupied(element.IsVisible, element.IsHitTestVisible) &&
                        TryGetScreenRectangle(element, out ScreenRectangle rectangle))
                    {
                        result.Add(rectangle);
                    }
                }
                CollectOccupiedRectangles(child, result);
            }
        }

        private static bool TryResolveVisibilityContract(
            FrameworkElement element,
            out PropertyInfo wantsProperty,
            out PropertyInfo layoutProperty,
            out bool wantsVisibility)
        {
            wantsProperty = null;
            layoutProperty = null;
            wantsVisibility = false;
            object viewModel = element.DataContext;
            if (viewModel == null)
                return false;

            Type type = viewModel.GetType();
            wantsProperty = type.GetProperty(WantsVisibilityProperty, BindingFlags.Instance | BindingFlags.Public);
            layoutProperty = type.GetProperty(LayoutAvailableProperty, BindingFlags.Instance | BindingFlags.Public);
            if (wantsProperty?.PropertyType != typeof(bool) || !wantsProperty.CanRead ||
                layoutProperty?.PropertyType != typeof(bool) || !layoutProperty.CanWrite)
            {
                return false;
            }

            wantsVisibility = (bool)wantsProperty.GetValue(viewModel, null);
            return true;
        }

        private static void SetLayoutAvailable(ActionHost host, bool available)
        {
            if (host.Element == null || host.LayoutProperty == null)
                return;
            object viewModel = host.Element.DataContext;
            if (viewModel != null)
                host.LayoutProperty.SetValue(viewModel, available, null);
        }

        private static bool TryGetScreenRectangle(FrameworkElement element, out ScreenRectangle rectangle)
        {
            rectangle = default;
            float width = element.ActualWidth;
            float height = element.ActualHeight;
            if (!(width > 0f) || !(height > 0f) || float.IsNaN(width) || float.IsNaN(height))
                return false;

            try
            {
                Point topLeft = ((Visual)element).PointToScreen(new Point(0f, 0f));
                Point bottomRight = ((Visual)element).PointToScreen(new Point(width, height));
                rectangle = new ScreenRectangle(
                    Math.Min(topLeft.X, bottomRight.X),
                    Math.Min(topLeft.Y, bottomRight.Y),
                    Math.Max(topLeft.X, bottomRight.X),
                    Math.Max(topLeft.Y, bottomRight.Y));
                return rectangle.IsValid;
            }
            catch
            {
                return false;
            }
        }

        private static bool IntersectsAny(ScreenRectangle candidate, List<ScreenRectangle> rectangles)
        {
            for (int index = 0; index < rectangles.Count; index++)
            {
                if (candidate.Intersects(rectangles[index]))
                    return true;
            }
            return false;
        }

        private static void LogDiagnosticIfDue(
            ManualLogSource log,
            int hostCount,
            int wantedCount,
            int occupiedCount,
            int assignedCount,
            int unavailableCount,
            int testedSlots,
            int invalidRectangles,
            int boundsRejections,
            int collisionRejections,
            bool hasControlsBounds)
        {
            if (log == null || wantedCount == 0)
                return;

            long now = Stopwatch.GetTimestamp();
            if (now < nextDiagnosticTimestamp)
                return;
            nextDiagnosticTimestamp = now + DiagnosticIntervalTicks;
            DebugLogHelper.LogDebug(
                log,
                $"Troop action layout diagnostic: hosts={hostCount}, wanted={wantedCount}, " +
                $"effectiveOccupied={occupiedCount}, assigned={assignedCount}, unavailable={unavailableCount}, " +
                $"testedSlots={testedSlots}, invalidRectangles={invalidRectangles}, " +
                $"boundsRejections={boundsRejections}, collisionRejections={collisionRejections}, " +
                $"controlsBoundsAvailable={hasControlsBounds}.");
        }

        private sealed class ActionHostComparer : IComparer<ActionHost>
        {
            public static readonly ActionHostComparer Instance = new ActionHostComparer();

            public int Compare(ActionHost left, ActionHost right)
            {
                int priority = left.Priority.CompareTo(right.Priority);
                return priority != 0
                    ? priority
                    : string.Compare(left.ActionId, right.ActionId, StringComparison.Ordinal);
            }
        }

        private sealed class ActionHost
        {
            public ActionHost(
                FrameworkElement element,
                int priority,
                string actionId,
                PropertyInfo wantsProperty,
                PropertyInfo layoutProperty,
                bool wantsVisibility)
            {
                Element = element;
                Priority = priority;
                ActionId = actionId;
                WantsProperty = wantsProperty;
                LayoutProperty = layoutProperty;
                WantsVisibility = wantsVisibility;
            }

            public FrameworkElement Element { get; }
            public int Priority { get; }
            public string ActionId { get; }
            public PropertyInfo WantsProperty { get; }
            public PropertyInfo LayoutProperty { get; }
            public bool WantsVisibility { get; }
        }

        private struct ScreenRectangle
        {
            private const float CollisionTolerance = 0.5f;

            public ScreenRectangle(float left, float top, float right, float bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public float Left;
            public float Top;
            public float Right;
            public float Bottom;
            public bool IsValid => Right > Left && Bottom > Top;

            public bool Contains(ScreenRectangle other)
            {
                return other.Left >= Left - CollisionTolerance && other.Top >= Top - CollisionTolerance &&
                    other.Right <= Right + CollisionTolerance && other.Bottom <= Bottom + CollisionTolerance;
            }

            public bool Intersects(ScreenRectangle other)
            {
                return Left < other.Right - CollisionTolerance && Right > other.Left + CollisionTolerance &&
                    Top < other.Bottom - CollisionTolerance && Bottom > other.Top + CollisionTolerance;
            }
        }
    }

    public sealed class TroopActionHudCoordinator : IDisposable
    {
        private delegate void SetupTroopActionsDelegate(HUD_Troops self, bool fromInitialOpening);

        private readonly ManualLogSource log;
        private readonly List<Action<HUD_Troops>> refreshers = new List<Action<HUD_Troops>>();
        private Hook setupHook;
        private SetupTroopActionsDelegate trampoline;

        public TroopActionHudCoordinator(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public void Register(Action<HUD_Troops> refresher)
        {
            if (refresher == null)
                throw new ArgumentNullException(nameof(refresher));
            refreshers.Add(refresher);
        }

        public void Initialize()
        {
            if (setupHook != null)
                return;

            MethodInfo method = typeof(HUD_Troops).GetMethod(
                "SetuptroopActionsUI",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(bool) },
                null);
            if (method == null)
                throw new MissingMethodException(typeof(HUD_Troops).FullName, "SetuptroopActionsUI");

            Hook pending = null;
            try
            {
                pending = new Hook(method, (SetupTroopActionsDelegate)SetupTroopActions);
                SetupTroopActionsDelegate pendingTrampoline = pending.GenerateTrampoline<SetupTroopActionsDelegate>();
                trampoline = pendingTrampoline;
                setupHook = pending;
                pending = null;
            }
            finally
            {
                pending?.Undo();
                pending?.Dispose();
            }
        }

        public void Refresh()
        {
            if (!MainViewModel.viewModelLoaded)
                return;
            HUD_Troops panel = MainViewModel.Instance?.HUDTroopPanel;
            if (panel != null)
                Refresh(panel);
        }

        public void Dispose()
        {
            setupHook?.Undo();
            setupHook?.Dispose();
            setupHook = null;
            trampoline = null;
        }

        private void SetupTroopActions(HUD_Troops self, bool fromInitialOpening)
        {
            trampoline(self, fromInitialOpening);
            Refresh(self);
        }

        private void Refresh(HUD_Troops panel)
        {
            for (int index = 0; index < refreshers.Count; index++)
            {
                try
                {
                    refreshers[index](panel);
                }
                catch (Exception ex)
                {
                    DebugLogHelper.LogError(log, $"A troop-action visibility provider failed: {ex}");
                }
            }
            TroopActionButtonLayout.Reflow(panel, log);
        }
    }
}
