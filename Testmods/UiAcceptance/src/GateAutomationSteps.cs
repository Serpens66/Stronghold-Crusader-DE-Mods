using System;
using System.Collections.Generic;
using CrusaderDE;
using RedBird.Core.Memory;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace UiAcceptance
{
    // ExtraFeatures' "operate automatically" switch, an APIShared building panel section: on the local player's own gate
    // (Vanilla's selection, the local path) and on Spectator's test gate panel (an APIShared presenter whose clicks run as
    // building actions). ExtraFeatures is observed only through its button and the button's view model.
    internal static unsafe class GateAutomationSteps
    {
        private const string ButtonName = "ExtraFeaturesGatehouseAutomationButton";
        // The former HUD_Buildings patch: Margin 0,100,600,0, right/top, 57x57 inside Vanilla's 800x170 gate panel.
        private const double ExpectedLeft = 800 - 600 - 57, ExpectedTop = 100;
        private static readonly Dictionary<string, int> gates = new Dictionary<string, int>(StringComparer.Ordinal);

        internal static void Register()
        {
            Runner.Register("Gate.Place", Place);
            Runner.Register("Gate.Click", Click);
            Runner.Register("Gate.SpectatorOwner", SpectatorOwner);
            Runner.Register("Gate.ExpectButton", ExpectButton);
            Runner.Register("Gate.ExpectNoButton", ExpectNoButton);
            Runner.Register("Gate.ExpectManual", ExpectManual);
        }

        // args: label, owner ("local", "ai" for the first other player with a keep, or a player ID), mapper, struct, radius.
        private static IEnumerable<Wait> Place(Step step)
        {
            var mapper = (eMappers)Enum.Parse(typeof(eMappers), step.Arg(2));
            var type = (eStructs)Enum.Parse(typeof(eStructs), step.Arg(3));
            int id;
            lock (HuntSteps.Engine()) id = PlaceGate(Owner(step.Arg(1)), mapper, type, step.Int(4, 12));
            gates[step.Arg(0)] = id;
            yield return new Until("gate " + id + " alive", () => { lock (HuntSteps.Engine()) return IsAlive(id); });
        }

        private static int Owner(string value)
        {
            int local = HuntSteps.Local();
            if (value == "local") return local;
            if (value != "ai") return int.Parse(value);
            for (int player = 1; player <= 8; player++)
                if (player != local && GamePlayerManagerAPI.Instance.GetPlayerKeepDoorPosition(player).X > 0) return player;
            throw new StepFailure("no other player with a keep");
        }

        private static int PlaceGate(int owner, eMappers mapper, eStructs type, int radius)
        {
            var before = new HashSet<int>(GatesOf(owner, type));
            var door = GamePlayerManagerAPI.Instance.GetPlayerKeepDoorPosition(owner);
            Runner.Require(HuntSteps.Place(owner, door.X, door.Y, mapper, type, radius, out int x, out int y), "no free site for " + mapper);
            int id = 0;
            foreach (int candidate in GatesOf(owner, type)) if (!before.Contains(candidate)) id = candidate;
            Runner.Require(id > 0, "the placed " + type + " is not registered");
            AcceptLog.Info("GATE_FIXTURE id=" + id + " type=" + type + " owner=" + owner + " at=" + x + "," + y);
            return id;
        }

        private static List<int> GatesOf(int owner, eStructs type)
        {
            var found = new List<int>();
            SimpleNativeArray<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsArray();
            for (int index = 0; index < buildings.Length; index++)
            {
                GameBuilding* building = buildings.GetValuePointer(index);
                if (building->r_AliveState != AliveState.None && building->r_BuildingType == type && building->r_PlayerIdOwner == owner)
                    found.Add(index + 1); // Building IDs are slot index + 1.
            }
            return found;
        }

        private static bool IsAlive(int id) =>
            APIShared.BuildingAccess.TryGetById(id, out GameBuilding* building, out _) && building != null &&
            building->r_AliveState == AliveState.IsAlive;

        private static int OwnerOf(int id)
        {
            lock (HuntSteps.Engine())
            {
                Runner.Require(APIShared.BuildingAccess.TryGetById(id, out GameBuilding* building, out _) && building != null, "gate missing");
                return building->r_PlayerIdOwner;
            }
        }

        // args: label. A plain left click on the footprint tile nearest to the gate's middle, with the camera on the gate.
        private static IEnumerable<Wait> Click(Step step)
        {
            int id = Gate(step.Arg(0));
            GateTile(id, out int x, out int y);
            foreach (var wait in InputSteps.MoveNoesisMouseOverMap()) yield return wait;
            AcceptLog.Info("GATE_CLICK_CONTEXT id=" + id + " " + ClickContext(x, y));
            foreach (var wait in InputSteps.ClickTile(x, y)) yield return wait;
            AcceptLog.Info("GATE_CLICK id=" + id + " tile=" + x + "," + y + " after: " + ClickContext(x, y));
        }

        // What Vanilla's and Spectator's click handling look at: the main-controls action, UI hover, the tile's building
        // and Spectator's acting tools player (read by name; Spectator may be absent).
        private static string ClickContext(int x, int y)
        {
            var tiles = GameTileManagerAPI.Instance;
            object spectator = null;
            try
            {
                spectator = Type.GetType("Spectator.SpectatorRuntime, Spectator")?.GetField("humanSelectedOwner",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.GetValue(null);
            }
            catch (Exception error) { spectator = error.GetType().Name; }
            return "action=" + MainControls.instance?.CurrentAction + " overUI=" + EditorDirector.instance?.overUI() +
                " tileBuilding=" + tiles.GetTileBuildingId(tiles.GetTileId(x, y)) + " spectatorOwner=" + spectator;
        }

        private static void GateTile(int id, out int tileX, out int tileY)
        {
            lock (HuntSteps.Engine())
            {
                Runner.Require(APIShared.BuildingAccess.TryGetById(id, out GameBuilding* building, out _) && building != null, "gate missing");
                int size = Math.Max(1, (int)building->r_OccupyTileGridSize), x0 = building->r_TilePositionXBegin, y0 = building->r_TilePositionYBegin;
                var tiles = GameTileManagerAPI.Instance;
                int best = int.MaxValue;
                tileX = tileY = -1;
                for (int dx = 0; dx < size; dx++)
                    for (int dy = 0; dy < size; dy++)
                    {
                        int x = x0 + dx, y = y0 + dy;
                        if (tiles.GetTileBuildingId(tiles.GetTileId(x, y)) != id) continue;
                        int distance = Math.Abs(2 * dx - size + 1) + Math.Abs(2 * dy - size + 1);
                        if (distance < best) { best = distance; tileX = x; tileY = y; }
                    }
                Runner.Require(tileX >= 0, "no map tile names gate " + id);
                GamePlayerManagerAPI.Instance.SetScreenCenterToBuilding(id);
            }
        }

        // args: label. Spectator's tools act for the gate's owner (its owner button on the test toolbar).
        private static IEnumerable<Wait> SpectatorOwner(Step step)
        {
            string name = "SpectatorEditorHumanOwner" + OwnerOf(Gate(step.Arg(0)));
            Noesis.FrameworkElement element = null;
            yield return new Until("element " + name, () => (element = GameXAMLManagerAPI.Instance.FindGlobalElement(name)) != null && element.IsVisible);
            foreach (var wait in InputSteps.ClickElement(element, name)) yield return wait;
        }

        // args: label. The switch is visible on the gate's open panel, at the former patch's place inside Vanilla's gate panel.
        private static IEnumerable<Wait> ExpectButton(Step step)
        {
            int id = Gate(step.Arg(0));
            Noesis.FrameworkElement button = null;
            yield return new Until("visible " + ButtonName + " for gate " + id, () => (button = Button()) != null && button.IsVisible &&
                button.ActualWidth > 0);
            var panel = GameXAMLManagerAPI.Instance.FindGlobalElement("ShowGate");
            Runner.Require(panel != null && panel.ActualWidth > 0, "Vanilla's gate panel ShowGate is not laid out");
            Noesis.Point origin = panel.PointToScreen(new Noesis.Point(0, 0));
            Noesis.Point corner = panel.PointToScreen(new Noesis.Point((float)panel.ActualWidth, 0));
            Noesis.Point at = button.PointToScreen(new Noesis.Point(0, 0));
            double scale = (corner.X - origin.X) / panel.ActualWidth;
            Runner.Require(scale > 0, "invalid HUD scale");
            double left = (at.X - origin.X) / scale, top = (at.Y - origin.Y) / scale;
            AcceptLog.Info("GATE_BUTTON id=" + id + " left=" + left.ToString("0.0") + " top=" + top.ToString("0.0") +
                " size=" + button.ActualWidth + "x" + button.ActualHeight + " scale=" + scale.ToString("0.000"));
            Runner.Require(Math.Abs(left - ExpectedLeft) <= 1.5 && Math.Abs(top - ExpectedTop) <= 1.5,
                "the switch is at " + left.ToString("0.0") + "," + top.ToString("0.0") + " instead of " + ExpectedLeft + "," + ExpectedTop);
            Runner.Require(Math.Abs(button.ActualWidth - 57) <= 0.5 && Math.Abs(button.ActualHeight - 57) <= 0.5, "the switch is not 57x57");
        }

        // args: optional ms (default 1500). Negative proof for panels that must not offer the switch.
        private static IEnumerable<Wait> ExpectNoButton(Step step)
        {
            yield return new Delay(step.Int(0, 1500));
            var button = Button();
            Runner.Require(button == null || !button.IsVisible, ButtonName + " is visible");
        }

        // args: true for manual only (the switch's crossed-out indicator), false for automatic.
        private static IEnumerable<Wait> ExpectManual(Step step)
        {
            bool manual = step.Arg(0) == "true";
            string shown = null;
            yield return new Until(ButtonName + (manual ? " manual only" : " automatic"), () =>
            {
                object model = Button()?.DataContext;
                object value = model?.GetType().GetProperty("ManualIndicatorVisibility")?.GetValue(model, null);
                shown = value?.ToString();
                return value is Noesis.Visibility visibility && (visibility == Noesis.Visibility.Visible) == manual;
            });
            AcceptLog.Info("GATE_MANUAL expected=" + manual + " indicator=" + shown);
        }

        private static Noesis.FrameworkElement Button() => GameXAMLManagerAPI.Instance.FindGlobalElement(ButtonName);

        private static int Gate(string label) =>
            gates.TryGetValue(label, out int id) ? id : throw new StepFailure("no gate fixture named " + label);
    }
}
