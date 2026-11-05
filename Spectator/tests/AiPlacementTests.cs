using System;
using Spectator;
using SHCDESE.Interop;

internal static class AiPlacementTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static int Main()
    {
        var model = new AiPlacementModel();
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
        model.SetChoices(new[] { none, defense }, "Unavailable", "Changed unit");
        Check(model.Role == "None" && model.CanPlace && !model.CanCount, "Default must be No AI.");
        Check(model.CanApplyInEditor, "No AI may explicitly preview its editor assignment.");
        model.Additional = true; Check(!model.Additional, "No AI has no recruitment mode.");
        model.Selected = defense; model.Additional = true;
        model.SetChoices(new[] { none }, "Unavailable", "Changed unit");
        Check(model.Role == "CastleDefense" && !model.CanPlace && model.Additional, "Type changes must preserve and reject incompatible intent.");
        model.SetChoices(new[] { none, new AiPlacementChoice("CastleDefense", "Deferred", true, true, "At map start") }, "Unavailable", "");
        Check(model.CanPlace && model.Selected.Deferred, "Editor declarations do not require a running AI.");
        Check(model.CanApplyInEditor, "Authored AIC deferral does not disable explicit current-editor preview.");
        var unavailable = new AiPlacementChoice("CastleDefense", "Defense", false, false, "AIC changed");
        model.SetChoices(new[] { none, unavailable }, "Unavailable", "Changed unit");
        Check(ReferenceEquals(model.Selected, unavailable) && !model.Selected.Available && !model.CanPlace && !model.CanApplyInEditor &&
            model.Role == "CastleDefense" && model.Additional,
            "A disabled current row retains exact semantic intent and reason while blocking placement/preview.");
        model.Selected = none; Check(!model.Additional, "Returning to No AI clears quota mode.");
        model.Selected = defense; model.ApplyInEditor = true; model.Reset();
        Check(model.Role == "None" && !model.ApplyInEditor && !model.Additional, "New missions reset local placement intent.");
        Console.WriteLine("PASS: AI placement defaults, incompatible intent, editor declarations and mission reset.");
        return 0;
    }
}
