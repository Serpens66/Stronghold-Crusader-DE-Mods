using System;
using Spectator;
using SHCDESE.Interop;

internal static class AiPlacementTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static int Main()
    {
        PlacementGeometry();
        var model = new AiPlacementModel();
        foreach (var mapper in new[] { eMappers.MAPPER_PEOPLE_MACEMEN, eMappers.MAPPER_PEOPLE_ARCHERS, eMappers.MAPPER_PEOPLE_ENGINEERS })
        {
            Check(AiMapperTypes.IsUnitPlacement(7, mapper), "Normal troop selection must expose AI placement in SP and editor.");
            Check(!AiMapperTypes.IsUnitPlacement(0, mapper), "Cancellation must hide AI placement even with a retained subtype.");
            Check(!AiMapperTypes.IsUnitPlacement(5, mapper), "Building action must not expose a retained troop subtype.");
        }
        Check(!AiMapperTypes.IsUnitPlacement(7, eMappers.MAPPER_CATAPULT), "Non-unit mappers must not expose AI placement.");
        foreach (bool enabled in new[] { false, true })
        foreach (bool editor in new[] { false, true })
        foreach (bool multiplayer in new[] { false, true })
        foreach (int action in new[] { 0, 5, 7 })
        foreach (int owner in new[] { -1, 0, 1, 8, 9 })
        foreach (bool mapper in new[] { false, true })
            Check(AiEditorPlacementPolicy.Enabled(enabled, editor, multiplayer, action, owner, mapper) ==
                (enabled && editor && !multiplayer && action == 7 && owner >= 1 && owner <= 8 && mapper),
                "Independent editor AI capture respects opt-in, native unit action, ownership and multiplayer.");
        Check(AiMapperTypes.TryGet(eMappers.MAPPER_PEOPLE_ENGINEERS_POTS, out var pot) &&
            AiMapperTypes.TryGet(eMappers.MAPPER_PEOPLE_ENGINEERS, out var engineer) &&
            pot == engineer && pot == eChimps.CHIMP_TYPE_ENGINEER, "Oil carrier shares the engineer type; its bound job is handled separately.");
        Check(!AiMapperTypes.TryGet(eMappers.MAPPER_CATAPULT, out _), "A building/tent mapper is not a primary unit mapper.");
        Check(AiMapperTypes.NeedsCrew(eChimps.CHIMP_TYPE_PORTABLE_SHIELD) && !AiMapperTypes.NeedsCrew(eChimps.CHIMP_TYPE_ENGINEER),
            "Machine placement does not create a crew; engineers themselves are free units.");
        Check(AiMapperTypes.TryGet(eMappers.MAPPER_PEOPLE_BEDOUIN_DEMOLISHER, out var bedouin) &&
            bedouin == eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER, "Bedouin unit placement must retain its native type.");
        var none = new AiPlacementChoice("None", "None", true, false, "");
        var defense = new AiPlacementChoice("CastleDefense", "Defense", true, false, "");
        // Choice keys are unit types (mapper IDs as text in the runtime).
        model.SetChoices("archer", new[] { none, defense });
        Check(model.Role == "None" && model.CanPlace && !model.CanCount, "Default must be No AI.");
        Check(model.CanApplyInEditor, "No AI may explicitly preview its editor assignment.");
        model.Additional = false; Check(!model.Additional, "No AI has no recruitment mode.");
        model.Selected = defense;
        Check(model.Role == "CastleDefense" && model.Additional, "Placed troops are additional to the AI's planned army by default.");
        model.Additional = false;
        model.SetChoices("pikeman", new[] { none });
        Check(model.Role == "None" && model.CanPlace && !model.Additional, "A unit type without the selected role falls back to No AI.");
        model.SetChoices("archer", new[] { none, defense });
        Check(model.Role == "CastleDefense" && !model.Additional, "Each unit type remembers its last role and recruitment mode.");
        model.SetChoices("pikeman", new[] { none, defense });
        Check(model.Role == "None", "The fallback is not remembered as a choice of the other unit type.");
        model.SetChoices("archer", new[] { none, new AiPlacementChoice("CastleDefense", "Deferred", true, true, "At map start") });
        Check(model.CanPlace && model.Selected.Deferred, "Editor declarations do not require a running AI.");
        Check(model.CanApplyInEditor, "Authored AIC deferral does not disable explicit current-editor preview.");
        var unavailable = new AiPlacementChoice("CastleDefense", "Defense", false, false, "AIC changed");
        model.SetChoices("archer", new[] { none, unavailable });
        Check(model.Role == "None" && model.CanPlace, "A remembered role that is not selectable now shows No AI.");
        model.SetChoices("archer", new[] { none, defense });
        Check(model.Role == "CastleDefense", "The remembered role returns once it is selectable again.");
        var blocked = new AiPlacementChoice("None", "None", false, false, "Unavailable");
        model.SetChoices("archer", new[] { blocked });
        Check(model.Role == "None" && !model.CanPlace, "Without the AI service the unavailable None row blocks placement.");
        model.SetChoices("archer", new[] { none, defense });
        model.ApplyInEditor = true; model.Reset();
        Check(model.Role == "None" && !model.ApplyInEditor && !model.Additional, "New missions reset local placement intent.");
        model.SetChoices("archer", new[] { none, defense });
        Check(model.Role == "None", "New missions forget remembered roles.");
        Console.WriteLine("PASS: AI placement defaults, per-type memory, No AI fallback, editor declarations and mission reset.");
        return 0;
    }

    private static void PlacementGeometry()
    {
        var menu = new ToolbarRect(500, 800, 800, 200);
        var obstacles = new System.Collections.Generic.List<ToolbarRect>();
        Check(AiPlacementLayout.TryDock(menu, 430, 60, 1920, 1080, obstacles, out var dock) &&
            dock.Right == menu.Right && dock.Bottom == menu.Y, "Default box must touch the build menu's top-right corner.");
        obstacles.Add(new ToolbarRect(1150, 700, 140, 100));
        Check(AiPlacementLayout.TryDock(menu, 430, 60, 1920, 1080, obstacles, out var shifted) &&
            shifted.Bottom == menu.Y && !shifted.Intersects(obstacles[0]), "Collision avoidance must slide along the menu, not float into the map.");
        obstacles.Add(new ToolbarRect(0, 0, 1920, 800));
        Check(AiPlacementLayout.TryDock(menu, 430, 60, 1920, 1080, obstacles, out var side) &&
            (side.X == menu.Right || side.Right == menu.X || side.Y == menu.Bottom), "If its top is occupied, the box must use an adjacent menu edge.");
        Check(!AiPlacementLayout.TryDock(menu, 2000, 60, 1920, 1080, obstacles, out _), "An oversized box cannot claim a valid dock.");
        var crowded = new[] { new ToolbarRect(0, 0, 1450, 1080) };
        Check(AiPlacementLayout.TryDock(menu, 430, 60, 1920, 1080, crowded, out var edge) && edge.Right == 1920,
            "A free screen edge remains a valid fallback when every menu edge is occupied.");
        var position = new AiPlacementPosition();
        var initial = position.Arrange(dock, 430, 60, 1920, 1080);
        position.Begin(initial.X + 30, initial.Y + 8, initial);
        position.Move(230, 108);
        var preview = position.Arrange(dock, 430, 60, 1920, 1080);
        Check(preview.X == 200 && preview.Y == 100, "Drag must preserve the exact grab offset.");
        position.Cancel();
        Check(!position.Floating && position.Arrange(dock, 430, 60, 1920, 1080).X == dock.X,
            "Cancelling a docked drag restores the dock.");
        position.Begin(initial.X, initial.Y, initial); position.Move(200, 100);
        position.Drop(dock, 430, 60, 1920, 1080, 1, 1);
        Check(position.Floating && !position.Dragging, "Mouse release commits a floating position.");
        var committed = position.Arrange(dock, 430, 60, 1920, 1080);
        position.Begin(200, 100, committed); position.Move(500, 500); position.Cancel();
        var restored = position.Arrange(dock, 430, 60, 1920, 1080);
        Check(Math.Abs(restored.X - committed.X) < .001 && Math.Abs(restored.Y - committed.Y) < .001,
            "Interrupted dragging must restore the last committed position, not the draft.");
        var resized = position.Arrange(dock, 430, 60, 640, 480);
        Check(resized.X >= 0 && resized.Right <= 640 && resized.Bottom <= 480,
            "Relative floating positions must stay reachable after viewport changes.");
        position.Begin(committed.X, committed.Y, committed); position.Move(dock.X + 4, dock.Y + 4);
        position.Drop(dock, 430, 60, 1920, 1080, 2, 2);
        Check(!position.Floating, "Returning within the physical-pixel snap range must dock.");
        position.Begin(dock.X, dock.Y, dock); position.Move(dock.X + 9, dock.Y);
        position.Drop(dock, 430, 60, 1920, 1080, 2, 2);
        Check(position.Floating, "Snap distance must account for UI-to-screen scale.");
        position.Reset();
        Check(!position.Floating && !position.Dragging, "New missions reset both placement and active capture state.");
    }
}
