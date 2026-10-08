// Shared convention and layout for independently installed troop-action buttons.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Shared
{
    public static class TroopActionButtonLayout
    {
        public const string HostNamePrefix = TroopActionButtonLayoutPolicy.HostNamePrefix;
        public const int ButtonWidth = 36;
        public const int ButtonHeight = 37;
        private const string LayoutVersionProperty = "TroopActionLayoutVersion";
        private const string ActionIdProperty = "TroopActionId";
        private const string PriorityProperty = "TroopActionPriority";
        private const string WantsVisibilityProperty = "WantsVisibility";
        private const string LayoutAvailableProperty = "LayoutAvailable";
        private static readonly Thickness BottomRightMargin = new Thickness(80, 40, 0, 3);
        private static readonly Thickness BottomMiddleMargin = new Thickness(1, 40, 2, 3);
        private static readonly HashSet<string> invalidLogged = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> duplicateLogged = new HashSet<string>(StringComparer.Ordinal);

        public static void Reflow(HUD_Troops troopPanel, ManualLogSource log)
        {
            if (troopPanel == null)
                return;

            FrameworkElement controls = troopPanel.FindName("UnitControls") as FrameworkElement;
            if (controls == null)
                return;

            var hosts = new List<ActionHost>();
            var invalidHosts = new List<string>();
            CollectActionHosts(controls, hosts, invalidHosts);
            if (hosts.Count == 0 && invalidHosts.Count == 0)
                return;

            // Hidden hosts can retain stale screen transforms in Noesis. The working Knight
            // layout uses only its XAML margin, so every shared host returns to that baseline.
            for (int index = 0; index < hosts.Count; index++)
                hosts[index].Element.RenderTransform = new TranslateTransform(0f, 0f);

            var occupied = new List<ScreenRectangle>();
            CollectOccupiedRectangles(controls, occupied);
            bool rightGeometryAvailable = TryGetSlotRectangle(controls, BottomRightMargin, out ScreenRectangle rightRectangle);
            bool middleGeometryAvailable = TryGetSlotRectangle(controls, BottomMiddleMargin, out ScreenRectangle middleRectangle);
            bool rightOccupied = !rightGeometryAvailable || IntersectsAny(rightRectangle, occupied);
            bool middleOccupied = !middleGeometryAvailable || IntersectsAny(middleRectangle, occupied);

            var requests = new List<TroopActionRequest>(hosts.Count);
            for (int index = 0; index < hosts.Count; index++)
            {
                ActionHost host = hosts[index];
                requests.Add(new TroopActionRequest(host.ActionId, host.Priority, host.WantsVisibility));
                if (host.WantsVisibility)
                    SetLayoutAvailable(host, false);
                else
                    SetLayoutAvailable(host, true);
            }

            TroopActionLayoutDecision decision = TroopActionButtonLayoutPolicy.CreateDecision(
                requests,
                rightOccupied,
                middleOccupied);
            var assignmentsById = new Dictionary<string, TroopActionSlot>(StringComparer.Ordinal);
            for (int index = 0; index < decision.Assignments.Count; index++)
            {
                TroopActionSlotAssignment assignment = decision.Assignments[index];
                assignmentsById[assignment.ActionId] = assignment.Slot;
            }

            for (int index = 0; index < hosts.Count; index++)
            {
                ActionHost host = hosts[index];
                if (!host.WantsVisibility || !assignmentsById.TryGetValue(host.ActionId, out TroopActionSlot slot))
                    continue;

                host.Element.Margin = slot == TroopActionSlot.BottomRight
                    ? BottomRightMargin
                    : BottomMiddleMargin;
                host.Element.RenderTransform = new TranslateTransform(0f, 0f);
                SetLayoutAvailable(host, true);
            }

            LogInvalidHosts(log, invalidHosts);
            LogDuplicateActions(log, decision.DuplicateActionIds);
        }

        private static void CollectActionHosts(
            DependencyObject parent,
            List<ActionHost> result,
            List<string> invalidHosts)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int index = 0; index < childCount; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                if (child is FrameworkElement element &&
                    TroopActionButtonLayoutPolicy.IsStandardHostName(element.Name))
                {
                    try
                    {
                        if (TryResolveActionHost(element, out ActionHost host))
                            result.Add(host);
                        else
                            invalidHosts.Add(element.Name ?? "<unnamed>");
                    }
                    catch
                    {
                        invalidHosts.Add(element.Name ?? "<unnamed>");
                    }
                    continue;
                }
                CollectActionHosts(child, result, invalidHosts);
            }
        }

        private static bool TryResolveActionHost(FrameworkElement element, out ActionHost host)
        {
            host = null;
            object viewModel = element.DataContext;
            if (viewModel == null)
                return false;

            Type type = viewModel.GetType();
            PropertyInfo wantsProperty = type.GetProperty(WantsVisibilityProperty, BindingFlags.Instance | BindingFlags.Public);
            PropertyInfo layoutProperty = type.GetProperty(LayoutAvailableProperty, BindingFlags.Instance | BindingFlags.Public);
            if (wantsProperty?.PropertyType != typeof(bool) || !wantsProperty.CanRead ||
                layoutProperty?.PropertyType != typeof(bool) || !layoutProperty.CanWrite)
            {
                return false;
            }

            if (!TryReadOptionalInt(viewModel, type, LayoutVersionProperty, out int? layoutVersion) ||
                !TryReadOptionalString(viewModel, type, ActionIdProperty, out string actionId) ||
                !TryReadOptionalInt(viewModel, type, PriorityProperty, out int? priority) ||
                !TroopActionButtonLayoutPolicy.TryResolveIdentity(
                    element.Name,
                    layoutVersion,
                    actionId,
                    priority,
                    out int resolvedPriority,
                    out string resolvedActionId))
            {
                layoutProperty.SetValue(viewModel, false, null);
                return false;
            }

            host = new ActionHost(
                element,
                resolvedPriority,
                resolvedActionId,
                layoutProperty,
                (bool)wantsProperty.GetValue(viewModel, null));
            return true;
        }

        private static bool TryReadOptionalInt(
            object viewModel,
            Type type,
            string propertyName,
            out int? value)
        {
            value = null;
            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
                return true;
            if (property.PropertyType != typeof(int) || !property.CanRead)
                return false;
            value = (int)property.GetValue(viewModel, null);
            return true;
        }

        private static bool TryReadOptionalString(
            object viewModel,
            Type type,
            string propertyName,
            out string value)
        {
            value = null;
            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
                return true;
            if (property.PropertyType != typeof(string) || !property.CanRead)
                return false;
            value = (string)property.GetValue(viewModel, null);
            return true;
        }

        private static void CollectOccupiedRectangles(DependencyObject parent, List<ScreenRectangle> result)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int index = 0; index < childCount; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                if (child is FrameworkElement element)
                {
                    if (TroopActionButtonLayoutPolicy.IsStandardHostName(element.Name))
                        continue;

                    // Only effectively interactive Vanilla or foreign buttons own a slot.
                    if (element is Button &&
                        TroopActionButtonLayoutPolicy.IsEffectivelyOccupied(
                            element.IsVisible,
                            element.IsHitTestVisible) &&
                        TryGetScreenRectangle(element, out ScreenRectangle rectangle))
                    {
                        result.Add(rectangle);
                    }
                }
                CollectOccupiedRectangles(child, result);
            }
        }

        private static bool TryGetSlotRectangle(
            FrameworkElement controls,
            Thickness margin,
            out ScreenRectangle rectangle)
        {
            rectangle = default;
            float availableWidth = controls.ActualWidth - margin.Left - margin.Right;
            float availableHeight = controls.ActualHeight - margin.Top - margin.Bottom;
            if (availableWidth < ButtonWidth || availableHeight < ButtonHeight)
                return false;

            float left = margin.Left + (availableWidth - ButtonWidth) * 0.5f;
            float top = margin.Top + (availableHeight - ButtonHeight) * 0.5f;
            try
            {
                Point topLeft = ((Visual)controls).PointToScreen(new Point(left, top));
                Point bottomRight = ((Visual)controls).PointToScreen(
                    new Point(left + ButtonWidth, top + ButtonHeight));
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

        private static void SetLayoutAvailable(ActionHost host, bool available)
        {
            object viewModel = host.Element?.DataContext;
            if (viewModel != null)
                host.LayoutProperty.SetValue(viewModel, available, null);
        }

        private static void LogInvalidHosts(ManualLogSource log, IReadOnlyList<string> invalidHosts)
        {
            if (log == null)
                return;
            for (int index = 0; index < invalidHosts.Count; index++)
            {
                string hostName = invalidHosts[index];
                if (invalidLogged.Add(hostName))
                    DebugLogHelper.LogWarning(log, $"Troop action host '{hostName}' has invalid or incomplete shared layout metadata and remains hidden.");
            }
        }

        private static void LogDuplicateActions(ManualLogSource log, IReadOnlyList<string> duplicateActionIds)
        {
            if (log == null)
                return;
            for (int index = 0; index < duplicateActionIds.Count; index++)
            {
                string actionId = duplicateActionIds[index];
                if (duplicateLogged.Add(actionId))
                    DebugLogHelper.LogWarning(log, $"Troop action id '{actionId}' is registered more than once; every duplicate remains hidden.");
            }
        }

        private sealed class ActionHost
        {
            public ActionHost(
                FrameworkElement element,
                int priority,
                string actionId,
                PropertyInfo layoutProperty,
                bool wantsVisibility)
            {
                Element = element;
                Priority = priority;
                ActionId = actionId;
                LayoutProperty = layoutProperty;
                WantsVisibility = wantsVisibility;
            }

            public FrameworkElement Element { get; }
            public int Priority { get; }
            public string ActionId { get; }
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
            // A direct editor launch can build the HUD before BepInEx installs this hook.
            Refresh();
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
