// Explicit real-menu acceptance: these steps never create an API spawn scope themselves.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Input;
using APIShared;
using APIShared.Units.AiMilitary;
using CrusaderDE;
using Noesis;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.GameGlobals;

namespace UiAcceptance
{
    internal static class RuntimeAiUiSteps
    {
        private static int owner, x, y;
        private static bool editor;
        private static eMappers mapper;
        private static eChimps type;
        private static AiUnitRef placed;
        private static string wantedRole = "None";
        private static bool additional;
        private static long placementLog;
        private static object engine;
        private static MethodInfo editorTroopCommand;
        private static readonly HashSet<int> usedPlacementTiles = new HashSet<int>();

        internal static void Register()
        {
            Runner.Register("AiUi.Prepare", Prepare);
            Runner.Register("AiUi.Select", Select);
            Runner.Register("AiUi.Place", Place);
            Runner.Register("AiUi.Check", Check);
            Runner.Register("AiUi.EditorOwner", EditorOwner);
            Runner.Register("AiUi.Cancel", Cancel);
            Runner.Register("AiUi.DisabledEditor", DisabledEditor);
            Runner.Register("AiUi.Menu", Menu);
            Runner.Register("AiUi.NoAiPreviewOwner", NoAiPreviewOwner);
        }

        private static IAiMilitaryCapability Capability()
        {
            Runner.Require(ApiShared.ForMod(UiAcceptancePlugin.Guid).TryGetAiMilitary(out var api, out var diagnostic),
                "AI capability unavailable: " + diagnostic.Reason);
            ProbeSteps.RequireNativeReferenceHash();
            return api;
        }

        private static FrameworkElement Find(string name)
        {
            var pending = new Stack<DependencyObject>();
            if (MainViewModel.Instance?.IngameUI is DependencyObject root) pending.Push(root);
            int visited = 0;
            while (pending.Count > 0 && visited++ < 8192)
            {
                var node = pending.Pop();
                if (node is FrameworkElement element && element.Name == name) return element;
                for (int i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--)
                    pending.Push(VisualTreeHelper.GetChild(node, i));
            }
            return null;
        }

        private static bool Visible(FrameworkElement element) => element != null && element.IsVisible &&
            element.Visibility == Visibility.Visible && element.ActualWidth > 0 && element.ActualHeight > 0;
        private static object Model => Find("SpectatorAiPlacementPanel")?.DataContext;
        private static T Read<T>(object instance, string name) => (T)instance.GetType().GetProperty(name).GetValue(instance, null);

        private static void Click(FrameworkElement element)
        {
            Runner.Require(Visible(element) && element.IsEnabled, "AI control is hidden/disabled: " + element?.Name);
            var point = element.PointToScreen(new Point(element.ActualWidth / 2, element.ActualHeight / 2));
            Runner.Require(point.X >= 0 && point.Y >= 0 && point.X < UnityEngine.Screen.width && point.Y < UnityEngine.Screen.height,
                "AI control outside the actual viewport: " + element.Name + " at " + point);
            var view = FatControler.instance.NGview;
            bool moved = view.MouseMove((int)point.X, (int)point.Y);
            bool over = element.IsMouseOver;
            UIElement capturedElement = element.Mouse?.Captured;
            string captured = capturedElement is FrameworkElement capture ? capture.GetType().Name + "#" + capture.Name : capturedElement?.GetType().Name ?? "null";
            bool down = view.MouseButtonDown((int)point.X, (int)point.Y, MouseButton.Left);
            bool up = view.MouseButtonUp((int)point.X, (int)point.Y, MouseButton.Left);
            AcceptLog.Info("AI_UI_MOUSE_CLICK name=" + element.Name + " at=" + point.X + "/" + point.Y +
                " handled=" + moved + "/" + down + "/" + up + " mouseOverAfterMove=" + over + " capturedBeforeDown=" + captured);
        }

        // args: sp|editor, ai|playerId, optional mapper enum symbol (engineers by default).
        private static IEnumerable<Wait> Prepare(Step step)
        {
            editor = step.Arg(0) == "editor";
            Capability();
            usedPlacementTiles.Clear();
            Runner.Require(UiAcceptancePlugin.LastContext?.IsEditor == editor &&
                !UiAcceptancePlugin.LastContext.Mode.IsRealMultiplayer, "UI fixture mode differs");
            mapper = (eMappers)Enum.Parse(typeof(eMappers), step.Arg(2, "MAPPER_PEOPLE_ENGINEERS"));
            // The scenario only needs this audited primary unit; machines are exercised by options.
            type = mapper == eMappers.MAPPER_PEOPLE_ENGINEERS ? eChimps.CHIMP_TYPE_ENGINEER :
                mapper == eMappers.MAPPER_PEOPLE_ARCHERS ? eChimps.CHIMP_TYPE_ARCHER :
                throw new StepFailure("UI fixture supports engineer/archer mapper symbols");
            owner = step.Arg(1) == "ai" ? FindAiOwner() : step.Int(1);
            Runner.Require(owner >= 1 && owner <= GamePlayerManagerAPI.MAX_PLAYERS, "invalid UI fixture owner");
            if (engine == null)
            {
                var field = typeof(EngineInterface).GetField("threadLock", BindingFlags.NonPublic | BindingFlags.Static);
                Runner.Require(field != null && field.IsPrivate && field.FieldType == typeof(object), "native monitor contract changed");
                engine = field.GetValue(null);
                Runner.Require(engine != null, "native monitor missing");
            }
            if (editor)
            {
                EditorDirector.instance.SetEditorPlayerID(owner);
                Runner.Require(EditorDirector.instance.ActivePlayerID == owner, "native editor owner setter declined");
                StartEditorTroopMapper(mapper);
            }
            else
            {
                yield return new Until("Spectator tools", () => SpectatorSteps.Model() != null);
                object tools = SpectatorSteps.Model();
                // The real command refuses an unselected actor. Select via the actual owner button
                // before invoking Placement, then wait for its published view-model state.
                Click(GameXAMLManagerAPI.Instance.FindGlobalElement("SpectatorEditorHumanOwner" + owner));
                var runtime = GameSteps.FindType("Spectator.SpectatorRuntime");
                var selected = runtime?.GetField("humanSelectedOwner", BindingFlags.NonPublic | BindingFlags.Static);
                Runner.Require(selected != null && selected.IsPrivate && selected.FieldType == typeof(int),
                    "Spectator selection observation contract changed");
                yield return PrepareWait("selected owner", () => (int)selected.GetValue(null) == owner, selected);
                if (!Read<bool>(tools, "PlacementActive"))
                {
                    var command = Read<ICommand>(tools, "ActionCommand");
                    Runner.Require(command.CanExecute("Placement"), "Placement tool unavailable");
                    command.Execute("Placement");
                }
                yield return PrepareWait("active placement mode", () => Read<bool>(tools, "PlacementActive"), selected);
                EngineInterface.StartMapperItem((int)mapper);
                MainControls.instance.CurrentAction = 5; MainControls.instance.CurrentSubAction = (int)mapper;
            }
            Runner.Require(ObserveNativeMapper("after-start") == (int)mapper, "native mapper did not start with the selected unit");
            yield return PrepareWait("visible AI placement strip", () => Visible(Find("SpectatorAiPlacementPanel")));
            Runner.Require(Read<string>(Model, "Role") == "None" && !Read<bool>(Model, "Additional"), "mission default is not No AI");
            Runner.Require(!(Find("SpectatorAiAdditional")?.IsEnabled ?? true), "No AI unexpectedly enables quota mode");
            Runner.Require(Visible(Find("SpectatorAiEditorApply")) == editor, "editor preview visibility differs");
            if (editor) Runner.Require(!Visible(GameXAMLManagerAPI.Instance.FindGlobalElement("SpectatorEditorHumanPanel")), "editor exposed general toolbar");
            AcceptLog.Info("AI_UI_READY editor=" + editor + " owner=" + owner + " action=" + MainControls.instance.CurrentAction +
                " mapper=" + mapper + " defaultNone=True preview=" + editor);
        }

        private static Until PrepareWait(string phase, Func<bool> condition, FieldInfo selection = null)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew(); bool reported = false;
            return new Until(phase, () => {
                if (condition()) return true;
                if (!reported && clock.ElapsedMilliseconds >= 1000)
                {
                    reported = true;
                    object tools = SpectatorSteps.Model();
                    AcceptLog.Info("AI_UI_PREPARE_WAIT phase=" + phase + " requestedOwner=" + owner +
                        " selectedOwner=" + (selection?.GetValue(null) ?? "unobserved") + " placement=" +
                        (tools == null ? "no-model" : Read<bool>(tools, "PlacementActive").ToString()) +
                        " action=" + MainControls.instance.CurrentAction + " subAction=" + MainControls.instance.CurrentSubAction +
                        " hudMain=" + MainViewModel.Instance.Show_HUD_Main);
                }
                return false;
            });
        }

        private static int FindAiOwner()
        {
            var api = Capability();
            for (int player = 1; player <= GamePlayerManagerAPI.MAX_PLAYERS; player++)
                if (GamePlayerManagerAPI.Instance.IsAIPlayer(player) && GamePlayerManagerAPI.Instance.GetLordUnitGlobalId(player) != 0)
                    foreach (var option in api.GetOptions(player, eChimps.CHIMP_TYPE_ENGINEER, false))
                        if (option.Role == AiMilitaryRole.SiegeEngineers && option.IsAvailable) return player;
            throw new StepFailure("no initialized AI owner for the real UI fixture");
        }

        private static unsafe void SetPlacementTile()
        {
            var tiles = GameTileManagerAPI.Instance;
            lock (engine)
            {
                int centerX = GameTileManagerAPI.MAX_WIDTH / 2, centerY = GameTileManagerAPI.MAX_HEIGHT / 2;
                if (!editor)
                {
                    int id = GamePlayerManagerAPI.Instance.GetLordUnitId(owner);
                    Runner.Require(UnitAccess.TryGetById(id, out GameUnit* lord, out _), "AI lord no longer exists");
                    centerX = lord->r_CurrentTilePositionX; centerY = lord->r_CurrentTilePositionY;
                }
                // FBCB9319 93750 uses this exact 40501481 mask for engineer/archer placement.
                // Native count is fixed to one; brush size zero is the real managed unit argument.
                const TilePropertyFlag blocked = TilePropertyFlag.Sea | TilePropertyFlag.ImpassableEdge |
                    TilePropertyFlag.IsBuilding | TilePropertyFlag.IsTree | TilePropertyFlag.River |
                    TilePropertyFlag.CrenelatedHigh | TilePropertyFlag.IsMoat;
                int examined = 0;
                for (int radius = 0; radius <= 32; radius++)
                for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                    examined++;
                    int candidateX = centerX + dx, candidateY = centerY + dy;
                    if (!tiles.IsTileInsideMapBounds(candidateX, candidateY)) continue;
                    int tile = tiles.GetTileId(candidateX, candidateY);
                    if (!tiles.IsValidTileId(tile) || usedPlacementTiles.Contains(tile)) continue;
                    var flags = tiles.GetTilePropertyFlag(tile);
                    if ((flags & blocked) != 0 || tiles.GetTileBuildingId(tile) != 0 || tiles.GetTileUnitId(tile) != 0) continue;
                    x = candidateX; y = candidateY; usedPlacementTiles.Add(tile);
                    AcceptLog.Info("AI_UI_FREE_TILE editor=" + editor + " owner=" + owner + " tile=" + x + "," + y +
                        " packed=" + tile + " flags=0x" + ((uint)flags).ToString("X8") + " examined=" + examined +
                        " nativeSpawnMask=0x" + ((uint)blocked).ToString("X8"));
                    return;
                }
                throw new StepFailure("no native-mask-free unoccupied fixture tile within radius32");
            }
        }

        private static int ObserveNativeMapper(string phase)
        {
            lock (engine)
            {
                ulong address = GameGlobalsManager.Instance.CurrentContextMapperValueVA;
                Runner.Require(address != 0, "SDK current mapper address unavailable");
                int current = System.Runtime.InteropServices.Marshal.ReadInt32(new IntPtr(unchecked((long)address)));
                AcceptLog.Info("AI_UI_NATIVE_MAPPER phase=" + phase + " actual=" + current + " wanted=" + (int)mapper +
                    " action=" + MainControls.instance.CurrentAction + " subAction=" + MainControls.instance.CurrentSubAction);
                return current;
            }
        }

        // args: public role enum symbol, Regular|Additional, optional editor preview true|false.
        private static IEnumerable<Wait> Select(Step step)
        {
            wantedRole = step.Arg(0); additional = step.Arg(1, "Regular") == "Additional";
            var picker = Find("SpectatorAiRolePicker") as ComboBox;
            Runner.Require(picker != null && Model != null, "AI role picker absent");
            yield return SettledRoleTarget(picker, false);
            int mapperBefore = ObserveNativeMapper("before-dropdown");
            Runner.Require(mapperBefore == (int)mapper, "native mapper changed before dropdown interaction");
            AcceptLog.Info("AI_UI_PICKER_OPEN before " + ScreenBounds(picker) + " hit=" + HitSummary(picker));
            Click(picker);
            var openClock = System.Diagnostics.Stopwatch.StartNew();
            yield return new Until("real AI dropdown open", () => picker.IsDropDownOpen || openClock.ElapsedMilliseconds >= 2000);
            if (!picker.IsDropDownOpen)
            {
                AcceptLog.Info("AI_UI_PICKER_OPEN failed " + ScreenBounds(picker) + " hit=" + HitSummary(picker));
                foreach (var wait in TakeUiScreenshot("ai-ui-picker-failed-" + (editor ? "editor" : "sp"))) yield return wait;
                throw new StepFailure("settled real picker click did not open its dropdown");
            }
            // Opening is synchronous; generated containers and bindings settle in render/layout.
            yield return NextFrame.Instance;
            yield return NextFrame.Instance;
            var bindingClock = System.Diagnostics.Stopwatch.StartNew(); bool bindingReported = false;
            yield return new Until("materialized AI option availability bindings", () => {
                int materialized = 0, mismatches = 0, disabledBound = 0;
                foreach (object item in picker.ItemsSource)
                {
                    object choice = item is ComboBoxItem owned ? owned.DataContext : item;
                    var container = item as ComboBoxItem ?? picker.ItemContainerGenerator.ContainerFromItem(item) as ComboBoxItem;
                    if (container == null) continue;
                    materialized++;
                    bool available = Read<bool>(choice, "Available");
                    if (item is ComboBoxItem)
                        Runner.Require((string)container.Content == Read<string>(choice, "Name"), "explicit role row label differs");
                    if (!available && item is ComboBoxItem)
                        Runner.Require(ToolTipService.GetToolTip(container) is ToolTip tip && tip.Content is TextBlock text &&
                            text.Text == Read<string>(choice, "Help"), "disabled role row lacks its concrete reason tooltip");
                    if (container.IsEnabled == available)
                    { if (!available) disabledBound++; continue; }
                    mismatches++;
                    if (!bindingReported && mismatches <= 4)
                        AcceptLog.Info("AI_UI_OPTION_BINDING role=" + Read<string>(choice, "Role") + " available=" + available +
                            " containerEnabled=" + container.IsEnabled + " dataContext=" + (container.DataContext?.GetType().FullName ?? "null") +
                            " content=" + (container.Content?.GetType().FullName ?? "null") + " afterRenderedFrames=2");
                }
                if (mismatches > 0 && !bindingReported)
                {
                    bindingReported = true;
                    AcceptLog.Info("AI_UI_OPTION_BINDING_SUMMARY materialized=" + materialized + " mismatches=" + mismatches +
                        " disabledBound=" + disabledBound);
                }
                if (materialized > 0 && mismatches == 0 && disabledBound > 0) return true;
                Runner.Require(bindingClock.ElapsedMilliseconds < 5000,
                    "AI dropdown availability binding failed after rendered frames: materialized=" + materialized +
                    " mismatches=" + mismatches + " disabledBound=" + disabledBound);
                return false;
            });
            object wanted = null; int enabled = 0, disabled = 0, disabledRendered = 0;
            foreach (object item in picker.ItemsSource)
            {
                object choice = item is ComboBoxItem owned ? owned.DataContext : item;
                if (Read<string>(choice, "Role") == wantedRole) wanted = item;
                if (Read<bool>(choice, "Available")) enabled++; else disabled++;
                if ((item as ComboBoxItem ?? picker.ItemContainerGenerator.ContainerFromItem(item) as ComboBoxItem) is ComboBoxItem container)
                {
                    Runner.Require(container.IsEnabled == Read<bool>(choice, "Available"), "dropdown option availability binding differs");
                    if (!container.IsEnabled) disabledRendered++;
                }
            }
            Runner.Require(disabled > 0 && disabledRendered > 0, "fixture did not render any disabled role option");
            Runner.Require(wanted != null && Read<bool>(wanted is ComboBoxItem ownedWanted ? ownedWanted.DataContext : wanted, "Available"),
                "requested UI role unavailable: " + wantedRole);
            FrameworkElement target = null;
            yield return new Until("materialized role item", () => (target = wanted as FrameworkElement ?? picker.ItemContainerGenerator.ContainerFromItem(wanted) as FrameworkElement) != null);
            target.BringIntoView();
            yield return NextFrame.Instance;
            yield return SettledRoleTarget(target);
            LogRoleClick("before", picker, target);
            foreach (var wait in TakeUiScreenshot("ai-ui-dropdown-" + (editor ? "editor-" : "sp-") + wantedRole)) yield return wait;
            yield return SettledRoleTarget(target);
            LogRoleClick("before-click", picker, target);
            int observedSelections = 0;
            SelectionChangedEventHandler observer = (sender, args) => {
                if (observedSelections++ >= 4) return;
                AcceptLog.Info("AI_UI_SELECTION_EVENT added=" + DescribeItems(args.AddedItems) +
                    " removed=" + DescribeItems(args.RemovedItems) + " selected=" + DescribeChoice(picker.SelectedItem) +
                    " modelRole=" + Read<string>(Model, "Role") + " open=" + picker.IsDropDownOpen);
            };
            picker.SelectionChanged += observer;
            try { Click(target); }
            finally { picker.SelectionChanged -= observer; }
            LogRoleClick("after-click", picker, target);
            yield return NextFrame.Instance;
            LogRoleClick("after-render", picker, target);
            var selectionClock = System.Diagnostics.Stopwatch.StartNew();
            yield return new Until("real role selection", () => {
                if (Read<string>(Model, "Role") == wantedRole && !picker.IsDropDownOpen) return true;
                Runner.Require(selectionClock.ElapsedMilliseconds < 5000,
                    "real role click did not select " + wantedRole + ": selected=" + DescribeChoice(picker.SelectedItem) +
                    " modelRole=" + Read<string>(Model, "Role") + " open=" + picker.IsDropDownOpen);
                return false;
            });
            var extra = Find("SpectatorAiAdditional") as CheckBox;
            if (extra.IsChecked != additional) Click(extra);
            bool preview = bool.Parse(step.Arg(2, "false"));
            if (editor)
            {
                var apply = Find("SpectatorAiEditorApply") as CheckBox;
                Runner.Require(apply.IsEnabled, "deferred editor role disabled explicit preview");
                if (apply.IsChecked != preview) Click(apply);
            }
            yield return NextFrame.Instance;
            Runner.Require(Read<bool>(Model, "Additional") == additional && Read<bool>(Model, "ApplyInEditor") == preview,
                "UI selection did not publish complete placement intent");
            Runner.Require(ObserveNativeMapper("after-dropdown") == mapperBefore, "dropdown interaction cancelled/changed the native mapper");
            AcceptLog.Info("AI_UI_SELECTED role=" + wantedRole + " additional=" + additional + " preview=" + preview +
                " enabledOptions=" + enabled + " disabledOptions=" + disabled + " disabledRendered=" + disabledRendered + " realDropdown=True");
        }

        private static string DescribeChoice(object value)
        {
            if (value == null) return "null";
            object context = (value as FrameworkElement)?.DataContext;
            object role = value.GetType().GetProperty("Role")?.GetValue(value, null);
            object contextRole = context?.GetType().GetProperty("Role")?.GetValue(context, null);
            return value.GetType().FullName + "{role=" + (role ?? "null") + ",dc=" +
                (context?.GetType().FullName ?? "null") + ",dcRole=" + (contextRole ?? "null") + "}";
        }

        private static Until SettledRoleTarget(FrameworkElement target, bool requirePopupViewport = true)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            float left = 0, top = 0, right = 0, bottom = 0; long stableSince = 0; bool previous = false;
            return new Until("role click inside settled popup viewport", () => {
                Point a = target.PointToScreen(new Point(0, 0));
                Point b = target.PointToScreen(new Point(target.ActualWidth, target.ActualHeight));
                Point center = target.PointToScreen(new Point(target.ActualWidth/2, target.ActualHeight/2));
                bool visible = Visible(target) && target.IsEnabled && target.IsHitTestVisible && center.X >= 0 && center.Y >= 0 &&
                    center.X < UnityEngine.Screen.width && center.Y < UnityEngine.Screen.height &&
                    Math.Abs(b.X-a.X) > 1 && Math.Abs(b.Y-a.Y) > 1;
                bool clippedViewport = false; DependencyObject ancestor = target;
                for (int i = 0; i < 12 && (ancestor = VisualTreeHelper.GetParent(ancestor)) != null; i++)
                    if (ancestor is FrameworkElement element && (element is ScrollViewer || element.GetType().Name == "ScrollContentPresenter"))
                    {
                        clippedViewport = true;
                        Point clipA = element.PointToScreen(new Point(0, 0));
                        Point clipB = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
                        visible &= element.IsVisible && center.X > Math.Min(clipA.X,clipB.X) && center.X < Math.Max(clipA.X,clipB.X) &&
                            center.Y > Math.Min(clipA.Y,clipB.Y) && center.Y < Math.Max(clipA.Y,clipB.Y);
                    }
                bool stable = previous && Math.Abs(a.X-left) < 0.25 && Math.Abs(a.Y-top) < 0.25 &&
                    Math.Abs(b.X-right) < 0.25 && Math.Abs(b.Y-bottom) < 0.25;
                bool clipReady = clippedViewport || !requirePopupViewport;
                if (!stable || !visible || !clipReady) stableSince = clock.ElapsedMilliseconds;
                previous = true; left = a.X; top = a.Y; right = b.X; bottom = b.Y;
                if (visible && clipReady && clock.ElapsedMilliseconds-stableSince >= 100)
                { AcceptLog.Info("AI_UI_ROLE_SETTLED wanted=" + wantedRole + " elapsedMs=" + clock.ElapsedMilliseconds + " " + ScreenBounds(target)); return true; }
                Runner.Require(clock.ElapsedMilliseconds < 5000,
                    "role row did not settle inside its actual popup clip: " + ScreenBounds(target));
                return false;
            });
        }

        private static IEnumerable<Wait> TakeUiScreenshot(string label)
        {
            var screenshot = typeof(GameSteps).GetMethod("Screenshot", BindingFlags.NonPublic | BindingFlags.Static);
            Runner.Require(screenshot != null && screenshot.ReturnType == typeof(IEnumerable<Wait>), "existing Screenshot step changed");
            foreach (var wait in (IEnumerable<Wait>)screenshot.Invoke(null, new object[] { new Step { Args = new[] { label } } })) yield return wait;
        }

        private static string HitSummary(FrameworkElement target)
        {
            var root = FatControler.instance?.NGview?.Content;
            if (root == null) return "no-view-content";
            Point center = target.PointToScreen(new Point(target.ActualWidth/2, target.ActualHeight/2));
            DependencyObject current = VisualTreeHelper.HitTest(root, root.PointFromScreen(center)).VisualHit;
            string result = ""; bool reachesTarget = false;
            for (int i = 0; current != null && i < 12; i++, current = VisualTreeHelper.GetParent(current))
            {
                reachesTarget |= ReferenceEquals(current, target);
                result += (i == 0 ? "" : " <- ") + current.GetType().Name;
                if (current is FrameworkElement element)
                    result += "#" + element.Name + "(z=" + Panel.GetZIndex(element) + ",hit=" + element.IsHitTestVisible +
                        ",visible=" + element.IsVisible + ",enabled=" + element.IsEnabled + ")";
            }
            return "targetAncestor=" + reachesTarget + ",captured=" + (target.Mouse?.Captured?.GetType().Name ?? "null") + ":" + result;
        }

        private static string DescribeItems(System.Collections.IList items)
        {
            string result = "";
            for (int i = 0; i < Math.Min(items.Count, 2); i++) result += (i == 0 ? "" : ";") + DescribeChoice(items[i]);
            return "[" + result + "]";
        }

        private static string ScreenBounds(FrameworkElement element)
        {
            var topLeft = element.PointToScreen(new Point(0, 0));
            var bottomRight = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
            var center = element.PointToScreen(new Point(element.ActualWidth/2, element.ActualHeight/2));
            return "tl=" + topLeft.X + "/" + topLeft.Y + ",br=" + bottomRight.X + "/" + bottomRight.Y +
                ",center=" + center.X + "/" + center.Y + ",actual=" +
                element.ActualWidth + "x" + element.ActualHeight + ",visible=" + element.IsVisible + ",enabled=" + element.IsEnabled;
        }

        private static void LogRoleClick(string phase, ComboBox picker, FrameworkElement target)
        {
            string parents = ""; DependencyObject current = target;
            for (int i = 0; i < 8 && (current = VisualTreeHelper.GetParent(current)) != null; i++)
                if (current is ScrollViewer scroll)
                    parents += " ScrollViewer{" + ScreenBounds(scroll) + ",offset=" + scroll.VerticalOffset +
                        ",extent=" + scroll.ExtentHeight + ",viewport=" + scroll.ViewportHeight + "}";
                else if (current is FrameworkElement ancestor)
                    parents += " " + ancestor.GetType().Name + "{" + ScreenBounds(ancestor) + "}";
            AcceptLog.Info("AI_UI_ROLE_CLICK phase=" + phase + " wanted=" + wantedRole + " selected=" + DescribeChoice(picker.SelectedItem) +
                " modelRole=" + Read<string>(Model, "Role") + " open=" + picker.IsDropDownOpen +
                " target=" + DescribeChoice(target) + " bounds={" + ScreenBounds(target) + "} screen=" +
                UnityEngine.Screen.width + "x" + UnityEngine.Screen.height + " parents=" + parents);
        }

        private static unsafe Dictionary<int,uint> Snapshot()
        {
            var ids = new List<int>();
            GameUnitManagerAPI.Instance.QueryUnits().Where((in GameUnit unit) => unit.r_GlobalId != 0).ToIdList(ids);
            var result = new Dictionary<int,uint>();
            foreach (int id in ids)
            {
                Runner.Require(UnitAccess.TryGetById(id, out GameUnit* unit, out _), "snapshot unit disappeared under monitor");
                result[id] = unit->r_GlobalId;
            }
            return result;
        }

        private static unsafe AiUnitRef FindCreated(Dictionary<int,uint> before)
        {
            var ids = new List<int>();
            GameUnitManagerAPI.Instance.QueryUnits().Where((in GameUnit unit) => unit.r_GlobalId != 0).ToIdList(ids);
            AiUnitRef result = default; int count = 0;
            foreach (int id in ids)
            {
                Runner.Require(UnitAccess.TryGetById(id, out GameUnit* unit, out _), "created unit disappeared under monitor");
                if ((!before.TryGetValue(id, out uint old) || unit->r_GlobalId != old) &&
                    unit->r_ControllableForPlayerId == owner && unit->r_UnitChimp == type)
                { result = new AiUnitRef(id, unit->r_GlobalId); count++; }
            }
            Runner.Require(count == 1, "actual unit mapper created " + count + " matching primary identities");
            return result;
        }

        private static IEnumerable<Wait> Place(Step step)
        {
            Runner.Require(Visible(Find("SpectatorAiPlacementPanel")), "AI strip disappeared before placement");
            AiMilitaryAssignmentResult state;
            placementLog = AcceptLog.Position;
            // Vanilla's exact unit-release managed boundary (action 5/7). The existing synthetic
            // MapClick postfix runs after this call and paused EditorDirector suppresses its state.
            // Calling this boundary exercises the real Spectator prefix/native mapper/post/finalizer;
            // unlike RuntimeAiSteps, this harness owns no spawn scope and never invokes CreateUnitLocal.
            lock (engine)
            {
                Runner.Require(!GamePlayerManagerAPI.Instance.IsLocalPaused(), "Vanilla mapper refuses Game_Paused; unpause before native placement");
                SetPlacementTile();
                int nativeMapper = ObserveNativeMapper("before-placement");
                AcceptLog.Info("AI_UI_MAPPER_INPUT nativeMapper=" + nativeMapper + " requestedMapper=" + (int)mapper +
                    " size=0 constructingOnly=False mouseState=3 inGameNotEditor=" + !editor + " nativePaused=False");
                Runner.Require(nativeMapper == (int)mapper, "native active mapper changed after UI selection");
                var before = Snapshot();
                EngineInterface.PlaceMapperItem((int)mapper, x, y, 0, EditorDirector.instance.ActivePlayerID,
                    !editor, false, 3);
                placed = FindCreated(before);
                Runner.Require(Capability().TryGetState(placed, out state) && state.Assignment.Role.ToString() == wantedRole,
                    "Spectator mapper did not capture the new primary unit with its UI intent");
            }
            yield return NextFrame.Instance;
            AcceptLog.Info("AI_UI_MAPPER_CAPTURE editor=" + editor + " owner=" + owner + " unit=" + placed +
                " role=" + state.Assignment.Role + " status=" + state.Status +
                " apiHarnessScope=False path=native-mapper-with-real-UI-intent");
        }

        // args: Applied|Declared|Rejected (after unpausing SP when necessary).
        private static IEnumerable<Wait> Check(Step step)
        {
            var expected = (AiMilitaryAssignmentStatus)Enum.Parse(typeof(AiMilitaryAssignmentStatus), step.Arg(0));
            var api = Capability(); AiMilitaryAssignmentResult current = null;
            string mode = additional ? "Additional" : "Regular";
            var diagnostic = new Regex("SPECTATOR_AI_RESULT source=placement .*expectedOwner=" + owner +
                " .*unit=" + Regex.Escape(placed.ToString()) + " role=" + wantedRole + " mode=" + mode +
                " .*status=" + expected + " reason=" + step.Arg(1, expected == AiMilitaryAssignmentStatus.Rejected ? "NoAiContext" : "None") +
                " .*matches=True");
            if (expected == AiMilitaryAssignmentStatus.Rejected)
            {
                // A refused fresh placement is deleted. A slot can retain its historical result;
                // immutable completion plus the dead identity/cleared marker prove the outcome.
                yield return new Until("Spectator rejected-preview completion", () => AcceptLog.Count(placementLog, diagnostic) > 0);
                lock (engine)
                {
                    Runner.Require(RejectedIdentityGone(), "rejected fresh mapper identity remains alive");
                    Runner.Require(!api.IsExcludedFromRecruitmentQuota(placed), "rejected fresh mapper identity retains a quota marker");
                    if (api.TryGetState(placed, out var historical))
                        Runner.Require(historical.Unit.Equals(placed) && historical.Status == AiMilitaryAssignmentStatus.Rejected &&
                            historical.Assignment.Role.ToString() == wantedRole && historical.Assignment.RecruitmentMode.ToString() == mode &&
                            historical.Reason.ToString() == step.Arg(1, "NoAiContext"),
                            "rejected slot retained a mismatched historical terminal result");
                }
                AcceptLog.Info("AI_UI_VERIFIED editor=" + editor + " unit=" + placed + " status=Rejected freshIdentityRetired=True realConsumerDevDiag=True");
                yield break;
            }
            yield return new Until("actual mapper terminal result " + expected, () => api.TryGetState(placed, out current) && current.Status == expected);
            Runner.Require(current.Assignment.Role.ToString() == wantedRole &&
                current.Assignment.RecruitmentMode == (additional ? AiRecruitmentMode.Additional : AiRecruitmentMode.Regular), "mapper terminal intent differs");
            if (expected == AiMilitaryAssignmentStatus.Applied)
                Runner.Require(api.IsExcludedFromRecruitmentQuota(placed) == additional, "actual mapper quota marker differs");
            yield return new Until("Spectator DevDiag actual mapper result", () => AcceptLog.Count(placementLog, diagnostic) > 0);
            AcceptLog.Info("AI_UI_VERIFIED editor=" + editor + " unit=" + placed + " status=" + expected + " realConsumerDevDiag=True");
        }

        private static unsafe bool RejectedIdentityGone() => !UnitAccess.TryGetById(placed.UnitId, out GameUnit* unit, out _) ||
            unit->r_GlobalId != placed.GlobalId || !UnitAccess.IsReallyAlive(unit);

        // Make the negative-preview fixture explicit: an editor may retain an earlier owner's AIC.
        private static IEnumerable<Wait> NoAiPreviewOwner(Step step)
        {
            Runner.Require(editor && owner == EditorDirector.instance.ActivePlayerID, "negative preview requires the current native editor owner");
            var role = (AiMilitaryRole)Enum.Parse(typeof(AiMilitaryRole), step.Arg(0, "SiegeEngineers"));
            var api = Capability();
            bool unavailable(int candidate)
            {
                foreach (var option in api.GetOptions(candidate, type, false))
                    if (option.Role == role) return !option.IsAvailable && option.Reason == AiMilitaryReason.NoAiContext;
                return false;
            }
            if (!unavailable(owner))
            {
                int replacement = 0;
                for (int candidate = GamePlayerManagerAPI.MAX_PLAYERS; candidate >= 1; candidate--)
                    if (candidate != owner && !GamePlayerManagerAPI.Instance.IsAIPlayer(candidate) &&
                        GamePlayerManagerAPI.Instance.GetLordUnitGlobalId(candidate) == 0 && unavailable(candidate))
                    { replacement = candidate; break; }
                Runner.Require(replacement > 0, "no unused native owner has the required NoAiContext preview precondition");
                object previousChoices = Read<object>(Model, "Choices");
                EditorDirector.instance.SetEditorPlayerID(replacement);
                Runner.Require(EditorDirector.instance.ActivePlayerID == replacement, "negative preview editor owner change declined");
                owner = replacement;
                yield return new Until("AI strip refreshed for the unused preview owner", () =>
                    Model != null && !ReferenceEquals(previousChoices, Read<object>(Model, "Choices")));
            }
            Runner.Require(unavailable(owner), "NoAiContext preview precondition changed");
            Runner.Require(ObserveNativeMapper("negative-preview-owner") == (int)mapper,
                "native owner change altered the selected mapper");
            AcceptLog.Info("AI_UI_NEGATIVE_PREVIEW owner=" + owner + " role=" + role + " nativeOptionReason=NoAiContext");
        }

        // Verify 1 -> 0 -> 1 with unchanged mapper, including the wake from the idle owner-0 state.
        private static IEnumerable<Wait> EditorOwner(Step step)
        {
            Runner.Require(editor, "editor owner transition requires editor fixture");
            int wanted = step.Int(0);
            EditorDirector.instance.SetEditorPlayerID(wanted);
            Runner.Require(EditorDirector.instance.ActivePlayerID == wanted, "editor owner change declined (paused?)");
            owner = wanted;
            yield return new Until("AI strip follows native editor owner " + wanted,
                () => Visible(Find("SpectatorAiPlacementPanel")) == (wanted >= 1 && wanted <= 8));
            Runner.Require(MainControls.instance.CurrentAction == 7 && MainControls.instance.CurrentSubAction == (int)mapper,
                "AI owner transition changed the native editor mapper");
            AcceptLog.Info("AI_UI_EDITOR_OWNER owner=" + wanted + " visible=" + Visible(Find("SpectatorAiPlacementPanel")));
        }

        private static IEnumerable<Wait> Cancel(Step step)
        {
            MainControls.instance.StopAllPlacement();
            yield return new Until("AI placement strip retired", () => !Visible(Find("SpectatorAiPlacementPanel")));
            AcceptLog.Info("AI_UI_RETIRED editor=" + editor);
        }

        private static IEnumerable<Wait> DisabledEditor(Step step)
        {
            Runner.Require(UiAcceptancePlugin.LastContext?.IsEditor == true, "disabled check requires editor");
            EditorDirector.instance.SetEditorPlayerID(1);
            StartEditorTroopMapper(eMappers.MAPPER_PEOPLE_ENGINEERS);
            yield return new Delay(500);
            Runner.Require(!Visible(Find("SpectatorAiPlacementPanel")) &&
                !Visible(GameXAMLManagerAPI.Instance.FindGlobalElement("SpectatorEditorHumanPanel")),
                "disabled editor opt-in exposed a mod placement surface");
            MainControls.instance.StopAllPlacement();
            AcceptLog.Info("AI_UI_DISABLED_EDITOR nativeMapper=True aiStrip=False generalToolbar=False");
        }

        private static void StartEditorTroopMapper(eMappers selectedMapper)
        {
            if (editorTroopCommand == null)
            {
                var method = typeof(EditorDirector).GetMethod("placeEditorTroop", BindingFlags.Instance | BindingFlags.NonPublic);
                var parameters = method?.GetParameters();
                Runner.Require(method != null && method.IsPrivate && !method.IsStatic && method.DeclaringType == typeof(EditorDirector) &&
                    method.ReturnType == typeof(void) && parameters.Length == 1 && parameters[0].ParameterType.IsEnum &&
                    parameters[0].ParameterType.FullName == "Enums+eMappers", "actual private editor troop command contract changed");
                editorTroopCommand = method;
            }
            editorTroopCommand.Invoke(EditorDirector.instance,
                new[] { Enum.ToObject(editorTroopCommand.GetParameters()[0].ParameterType, (int)selectedMapper) });
            Runner.Require(MainControls.instance.CurrentAction == 7 && MainControls.instance.CurrentSubAction == (int)selectedMapper,
                "native editor troop command declined to start its unit mapper");
        }

        private static IEnumerable<Wait> Menu(Step step)
        {
            MainViewModel.Instance.HUDmain.InGameOptions(null, null);
            yield return new Until("native in-game menu", () => MainViewModel.Instance.Show_HUD_IngameMenu);
            yield return new Until("AI strip hidden by native menu", () => !Visible(Find("SpectatorAiPlacementPanel")));
            AcceptLog.Info("AI_UI_NATIVE_MENU stripHidden=True action=" + MainControls.instance.CurrentAction);
            // The scenario leaves through the existing QuitToMenu step, preserving Vanilla's pause/cancel semantics.
        }
    }
}
