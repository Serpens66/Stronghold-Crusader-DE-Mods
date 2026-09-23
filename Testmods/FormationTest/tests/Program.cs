using FormationTest;
using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

internal static class Program
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate long CommonGroupProbeDelegate(
        IntPtr manager, int tribeId, short x, short y, short patrol, int newOrder);

    private static int assertions;

    private static int Main()
    {
        try
        {
            TestFormationCycleAndDensity();
            TestAutomaticWidths();
            TestNativeVanillaCandidateRules();
            TestDirectionQuantization();
            TestShapesAndDensity();
            TestRoleAssignment();
            TestDirectionAndDensityMatrix();
            TestRoleEdgeCases();
            TestEffectivePreviewKeys();
            TestDefaultsMigration();
            TestReleaseStateModel();
            TestGroundMovePreviewEligibility();
            TestStatusTextAndDirectionVectors();
            TestPlanHash();
            TestMoveOrderMatching();
            TestPreviewMarkerNormalization();
            TestNativeDetourEntryContract();
            TestInstalledRedBirdMarkerSpan();
            TestSourceSafetyContracts();
            Console.WriteLine($"PASS: FormationTest ({assertions} assertions).");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception);
            return 1;
        }
    }

    private static void TestGroundMovePreviewEligibility()
    {
        GroundMovePreviewSnapshot ground = PreviewSnapshot();
        Check(GroundMovePreviewEligibility.EvaluateInitial(ground) ==
              GroundMovePreviewRejection.None,
            "clean pathable ground permits a formation preview");

        var rejected = new Dictionary<GroundMovePreviewRejection, GroundMovePreviewSnapshot>
        {
            [GroundMovePreviewRejection.OutsideMap] = PreviewSnapshot(insideMap: false),
            [GroundMovePreviewRejection.CursorOutsideGame] = PreviewSnapshot(cursorInGame: false),
            [GroundMovePreviewRejection.CursorSnapshotMismatch] = PreviewSnapshot(cursorMatches: false),
            [GroundMovePreviewRejection.UnderCursorUnit] = PreviewSnapshot(underCursor: 1),
            [GroundMovePreviewRejection.HoveredUnit] = PreviewSnapshot(hoveredUnit: 4),
            [GroundMovePreviewRejection.TileOccupiedByUnit] = PreviewSnapshot(tileUnit: 5),
            [GroundMovePreviewRejection.HoveredBuilding] = PreviewSnapshot(hoveredBuilding: 6),
            [GroundMovePreviewRejection.HoveredWall] = PreviewSnapshot(hoveringWall: true),
            [GroundMovePreviewRejection.TileOccupiedByBuilding] = PreviewSnapshot(tileBuilding: 7),
            [GroundMovePreviewRejection.TargetUnavailable] = PreviewSnapshot(targetAvailable: false),
            [GroundMovePreviewRejection.MissingPathComponent] = PreviewSnapshot(hasComponent: false)
        };
        foreach (KeyValuePair<GroundMovePreviewRejection, GroundMovePreviewSnapshot> item in rejected)
        {
            Check(GroundMovePreviewEligibility.EvaluateInitial(item.Value) == item.Key,
                $"{item.Key} suppresses the formation preview");
        }

        Check(GroundMovePreviewEligibility.EvaluateFixedTarget(
                  true, 0, 0, true, true) == GroundMovePreviewRejection.None &&
              GroundMovePreviewEligibility.EvaluateFixedTarget(
                  true, 9, 0, true, true) ==
                  GroundMovePreviewRejection.TileOccupiedByUnit &&
              GroundMovePreviewEligibility.EvaluateFixedTarget(
                  true, 0, 9, true, true) ==
                  GroundMovePreviewRejection.TileOccupiedByBuilding,
            "fixed-target revalidation catches later unit and building occupancy");
    }

    private static GroundMovePreviewSnapshot PreviewSnapshot(
        bool insideMap = true,
        bool cursorInGame = true,
        bool cursorMatches = true,
        int underCursor = 0,
        int hoveredUnit = 0,
        int tileUnit = 0,
        int hoveredBuilding = 0,
        bool hoveringWall = false,
        int tileBuilding = 0,
        bool targetAvailable = true,
        bool hasComponent = true) =>
        new GroundMovePreviewSnapshot(
            insideMap, cursorInGame, cursorMatches, underCursor,
            hoveredUnit, tileUnit, hoveredBuilding, hoveringWall,
            tileBuilding, targetAvailable, hasComponent);

    private static void TestFormationCycleAndDensity()
    {
        FormationKind current = FormationKind.Vanilla;
        var observed = new List<FormationKind>();
        for (int index = 0; index < 6; index++)
        {
            observed.Add(current);
            current = FormationModel.Next(current);
        }
        Check(observed.SequenceEqual(new[]
        {
            FormationKind.Vanilla,
            FormationKind.Block,
            FormationKind.Line,
            FormationKind.Column,
            FormationKind.Wedge,
            FormationKind.Circle
        }), "formation cycle order");
        Check(current == FormationKind.Vanilla, "formation cycle wraps");
        Check(FormationModel.ChangeDensity(2, 1) == 1, "wheel up tightens");
        Check(FormationModel.ChangeDensity(2, -1) == 3, "wheel down loosens");
        Check(FormationModel.ChangeDensity(1, 1) == 1, "density lower clamp");
        Check(FormationModel.ChangeDensity(4, -1) == 4, "density upper clamp");
    }

    private static void TestAutomaticWidths()
    {
        int block = FormationModel.ResolveAutomaticWidth(FormationKind.Block, 100);
        int line = FormationModel.ResolveAutomaticWidth(FormationKind.Line, 100);
        int column = FormationModel.ResolveAutomaticWidth(FormationKind.Column, 100);
        Check(line > block && block > column, "automatic aspect ordering");
        Check(FormationModel.ResolveAutomaticRows(FormationKind.Block, 20) == 5 &&
              FormationModel.ResolveAutomaticWidth(FormationKind.Block, 20) == 4,
            "twenty-unit Block starts as five ranks of four");
        Check(FormationModel.ResolveDraggedWidth(FormationKind.Block, 2, 20) == 4 &&
              FormationModel.ResolveDraggedWidth(FormationKind.Block, 4, 20) == 5 &&
              FormationModel.ResolveDraggedWidth(FormationKind.Block, 10, 20) == 20,
            "each two drag tiles reduces Block depth by one rank");
        Check(FormationModel.ResolveActualRows(FormationKind.Block, 20, 4) == 5 &&
              FormationModel.ResolveActualRows(FormationKind.Block, 20, 20) == 1,
            "resolved width produces the requested Block depth");
        int wedgeAutomatic = FormationModel.ResolveAutomaticWidth(FormationKind.Wedge, 100);
        int wedgeExpanded = FormationModel.ResolveDraggedWidth(FormationKind.Wedge, 50, 100);
        Check(wedgeExpanded >= wedgeAutomatic &&
              FormationModel.ResolveActualRows(FormationKind.Wedge, 100, wedgeExpanded) >= 10,
            "Wedge drag preserves its geometric minimum depth");
        foreach (FormationKind kind in new[]
        {
            FormationKind.Block, FormationKind.Line,
            FormationKind.Column, FormationKind.Wedge,
            FormationKind.Vanilla, FormationKind.Circle
        })
        {
            for (int count = 1; count <= 100; count++)
            {
                int previousRows = int.MaxValue;
                for (int distance = 2; distance <= 30; distance += 2)
                {
                    int width = FormationModel.ResolveDraggedWidth(kind, distance, count);
                    int rows = FormationModel.ResolveActualRows(kind, count, width);
                    Check(width >= 1 &&
                          (kind == FormationKind.Circle || width <= count) && rows >= 1 &&
                          rows <= previousRows,
                        $"drag depth is valid and monotonic kind={kind}, count={count}, distance={distance}");
                    previousRows = rows;
                }
            }
        }
        for (int density = 1; density <= 4; density++)
            Check(FormationModel.ResolveDraggedWidth(FormationKind.Block, 6, 20) == 7,
                "drag width is independent of density " + density);
    }

    private static void TestDirectionQuantization()
    {
        Check(FormationModel.QuantizeDirection(0, -4) == 0, "north sector");
        Check(FormationModel.QuantizeDirection(4, -4) == 1, "north-east sector");
        Check(FormationModel.QuantizeDirection(4, 0) == 2, "east sector");
        Check(FormationModel.QuantizeDirection(0, 4) == 4, "south sector");
        Check(FormationModel.QuantizeDirection(-4, 0) == 6, "west sector");
        Check(FormationModel.QuantizeDirection(0, 0, 7) == 7, "zero drag fallback");
    }

    private static void TestNativeVanillaCandidateRules()
    {
        for (int density = 1; density <= 4; density++)
        {
            for (int distance = 0; distance <= 16; distance++)
            {
                Check(FormationModel.IsNativeVanillaSlotCandidate(
                        distance, 0, 1, 0, density, assassinOnly: false) ==
                      (distance % density == 0),
                    $"Vanilla density modulo density={density}, distance={distance}");
            }
        }
        Check(!FormationModel.IsNativeVanillaSlotCandidate(
                0, 0, 0, 0, 1, assassinOnly: false) &&
              !FormationModel.IsNativeVanillaSlotCandidate(
                0, 0, 4000, 0, 1, assassinOnly: false) &&
              FormationModel.IsNativeVanillaSlotCandidate(
                0, 0, 3999, 0, 1, assassinOnly: false),
            "Vanilla path-distance bounds match the native selectors");
        Check(!FormationModel.IsNativeVanillaSlotCandidate(
                0, 0, 1, 0x10000100, 1, assassinOnly: true) &&
              FormationModel.IsNativeVanillaSlotCandidate(
                0, 0, 1, 0x10000100, 1, assassinOnly: false),
            "Assassin-only Vanilla slots apply the native logic-mask filter");
    }

    private static void TestPlanHash()
    {
        ulong first = FormationPlanHash.Begin(2, RangedPlacementMode.Rear);
        FormationPlanHash.AddEntry(
            ref first, 1, 1001, 400, 401, FormationRole.Front);
        FormationPlanHash.AddEntry(
            ref first, 2, 1002, 402, 403, FormationRole.Rear);

        ulong repeated = FormationPlanHash.Begin(2, RangedPlacementMode.Rear);
        FormationPlanHash.AddEntry(
            ref repeated, 1, 1001, 400, 401, FormationRole.Front);
        FormationPlanHash.AddEntry(
            ref repeated, 2, 1002, 402, 403, FormationRole.Rear);
        Check(first == repeated, "formation plan hash is deterministic");

        ulong reordered = FormationPlanHash.Begin(2, RangedPlacementMode.Rear);
        FormationPlanHash.AddEntry(
            ref reordered, 2, 1002, 402, 403, FormationRole.Rear);
        FormationPlanHash.AddEntry(
            ref reordered, 1, 1001, 400, 401, FormationRole.Front);
        Check(first != reordered, "formation plan hash includes canonical unit order");

        ulong changedTarget = FormationPlanHash.Begin(2, RangedPlacementMode.Rear);
        FormationPlanHash.AddEntry(
            ref changedTarget, 1, 1001, 400, 401, FormationRole.Front);
        FormationPlanHash.AddEntry(
            ref changedTarget, 2, 1002, 402, 404, FormationRole.Rear);
        Check(first != changedTarget, "formation plan hash includes destinations");
        Check(first != FormationPlanHash.Begin(2, RangedPlacementMode.Center),
            "formation plan hash includes ranged placement mode");
    }

    private static void TestReleaseStateModel()
    {
        var leftRelease = new FormationMouseState(3, false, false, false, true);
        var inputFirst = new FormationReleaseGate(commandButton: 0);
        Check(inputFirst.ObserveInputRelease() && inputFirst.ReleaseEventSeen &&
              !inputFirst.VanillaReleaseClaimed && !inputFirst.CanModify,
            "input release freezes the gesture while awaiting Vanilla");
        Check(inputFirst.TryClaimVanillaRelease(leftRelease) &&
              inputFirst.VanillaReleaseClaimed &&
              !inputFirst.TryClaimVanillaRelease(leftRelease),
            "input-first release is claimed exactly once");

        var rightRelease = new FormationMouseState(0, false, true, true, false);
        var nativeFirst = new FormationReleaseGate(commandButton: 1);
        Check(nativeFirst.TryClaimVanillaRelease(rightRelease) &&
              !nativeFirst.ReleaseEventSeen && !nativeFirst.CanModify,
            "authoritative native release works without an input-up event");
        Check(!nativeFirst.ObserveInputRelease() &&
              !nativeFirst.TryClaimVanillaRelease(rightRelease),
            "late input events and duplicate native releases are ignored");

        var wrongRelease = new FormationReleaseGate(commandButton: 1);
        Check(!wrongRelease.TryClaimVanillaRelease(leftRelease) &&
              wrongRelease.CanModify,
            "the auxiliary release cannot claim the active command drag");

        FormationMouseState consumed = FormationReleaseStateModel.Consume();
        Check(consumed.LeftState == 0 && !consumed.RightDown && !consumed.RightUp &&
              consumed.StateRead && !consumed.UpPending,
            "accepted dispatch permanently consumes both release mechanisms");
        Check(!FormationReleaseStateModel.HasCommandRelease(consumed, 0) &&
              !FormationReleaseStateModel.HasCommandRelease(consumed, 1),
            "consumed state cannot issue a follow-up command");
    }

    private static void TestStatusTextAndDirectionVectors()
    {
        var expected = new[]
        {
            new { X = 0, Y = -1 }, new { X = 1, Y = -1 },
            new { X = 1, Y = 0 }, new { X = 1, Y = 1 },
            new { X = 0, Y = 1 }, new { X = -1, Y = 1 },
            new { X = -1, Y = 0 }, new { X = -1, Y = -1 }
        };
        for (int sector = 0; sector < expected.Length; sector++)
        {
            FormationModel.GetForwardVector(sector, out int x, out int y);
            Check(x == expected[sector].X && y == expected[sector].Y,
                "direction indicator sector " + sector);
        }
    }

    private static void TestShapesAndDensity()
    {
        foreach (FormationKind kind in new[]
        {
            FormationKind.Block,
            FormationKind.Line,
            FormationKind.Column,
            FormationKind.Wedge,
            FormationKind.Circle
        })
        {
            List<FormationPoint> slots = FormationModel.BuildRelativeSlots(
                kind, 37, 9, 2, 2);
            Check(slots.Count == 37, kind + " slot count");
            Check(slots.Select(point => point.X + ":" + point.Y).Distinct().Count() == 37,
                kind + " unique slots");
            Check(Math.Abs(slots.Sum(point => point.X)) <= slots.Count,
                kind + " centered X");
            Check(Math.Abs(slots.Sum(point => point.Y)) <= slots.Count,
                kind + " centered Y");
        }

        List<FormationPoint> tight = FormationModel.BuildRelativeSlots(
            FormationKind.Block, 16, 4, 1, 0);
        List<FormationPoint> loose = FormationModel.BuildRelativeSlots(
            FormationKind.Block, 16, 4, 4, 0);
        int tightExtent = tight.Max(point => Math.Abs(point.X) + Math.Abs(point.Y));
        int looseExtent = loose.Max(point => Math.Abs(point.X) + Math.Abs(point.Y));
        Check(looseExtent > tightExtent, "density changes geometric extent");

        int vanillaAutomatic = FormationModel.ResolveAutomaticWidth(
            FormationKind.Vanilla, 40);
        int vanillaDragged = FormationModel.ResolveDraggedWidth(
            FormationKind.Vanilla, 10, 40);
        Check(vanillaDragged == vanillaAutomatic,
            "Vanilla drag never changes the native slot shape");
        bool rejectedSyntheticVanilla = false;
        try
        {
            FormationModel.BuildRelativeSlots(
                FormationKind.Vanilla, 40, vanillaAutomatic, 1, 0);
        }
        catch (InvalidOperationException)
        {
            rejectedSyntheticVanilla = true;
        }
        Check(rejectedSyntheticVanilla,
            "Vanilla cannot silently fall back to a synthetic blob");
        var nativeShape = new List<FormationPoint>
        {
            new FormationPoint(0, 0, 0, 0),
            new FormationPoint(-1, 0, 0, 0),
            new FormationPoint(1, 0, 0, 0),
            new FormationPoint(0, -1, 0, 0),
            new FormationPoint(0, 1, 0, 0)
        };
        List<FormationPoint> northFacing = FormationModel.OrientNativeSlots(
            nativeShape, 0);
        List<FormationPoint> eastFacing = FormationModel.OrientNativeSlots(
            nativeShape, 2);
        Check(northFacing.Select(point => point.X + ":" + point.Y)
                  .SequenceEqual(eastFacing.Select(point => point.X + ":" + point.Y)) &&
              !northFacing.Select(point => point.Rank + ":" + point.File)
                  .SequenceEqual(eastFacing.Select(point => point.Rank + ":" + point.File)),
            "Vanilla direction changes role metadata without changing native slots");

        List<FormationPoint> circle = FormationModel.BuildRelativeSlots(
            FormationKind.Circle, 41,
            FormationModel.ResolveAutomaticWidth(FormationKind.Circle, 41), 1, 0);
        Check(circle.Count == 41 &&
              circle.Select(point => point.X + ":" + point.Y).Distinct().Count() == 41,
            "filled circle has a complete unique deterministic slot set");
        Check(circle.Any(point => point.X == 0 && point.Y == 0),
            "odd filled circles include their center");
    }

    private static void TestRoleAssignment()
    {
        List<FormationPoint> slots = FormationModel.BuildRelativeSlots(
            FormationKind.Block, 12, 4, 1, 0);
        var units = new List<FormationUnit>
        {
            new FormationUnit(12, 0, FormationRole.Rear),
            new FormationUnit(11, 0, FormationRole.Protected),
            new FormationUnit(10, 0, FormationRole.Front),
            new FormationUnit(9, 0, FormationRole.Front),
            new FormationUnit(8, 0, FormationRole.Rear),
            new FormationUnit(7, 0, FormationRole.Front),
            new FormationUnit(6, 0, FormationRole.Protected),
            new FormationUnit(5, 0, FormationRole.Front),
            new FormationUnit(4, 0, FormationRole.Front),
            new FormationUnit(3, 0, FormationRole.Front),
            new FormationUnit(2, 0, FormationRole.Rear),
            new FormationUnit(1, 0, FormationRole.Neutral)
        };
        int[] assignment = FormationModel.AssignSlotsByRole(
            units, slots, RangedPlacementMode.Rear);
        int maximumFrontRank = units
            .Select((unit, index) => new { unit, index })
            .Where(value => value.unit.Role == FormationRole.Front)
            .Max(value => slots[assignment[value.index]].Rank);
        int minimumRearRank = units
            .Select((unit, index) => new { unit, index })
            .Where(value => value.unit.Role == FormationRole.Rear)
            .Min(value => slots[assignment[value.index]].Rank);
        Check(maximumFrontRank <= minimumRearRank, "melee precedes rear units");
        Check(assignment.Distinct().Count() == units.Count, "role assignment is bijective");

        int[] unchanged = FormationModel.AssignSlotsByRole(
            units, slots, RangedPlacementMode.Off);
        Check(unchanged.SequenceEqual(Enumerable.Range(0, units.Count)),
            "disabled role sorting preserves order");
        Check(!assignment.SequenceEqual(unchanged),
            "mixed canonical IDs visibly change when role sorting is enabled");

        var alreadyOrdered = new List<FormationUnit>
        {
            new FormationUnit(1, 0, FormationRole.Front),
            new FormationUnit(2, 0, FormationRole.Front),
            new FormationUnit(3, 0, FormationRole.Protected),
            new FormationUnit(4, 0, FormationRole.Neutral),
            new FormationUnit(5, 0, FormationRole.Rear)
        };
        List<FormationPoint> orderedSlots = FormationModel.BuildRelativeSlots(
            FormationKind.Column, alreadyOrdered.Count, 1, 1, 0);
        Check(FormationModel.AssignSlotsByRole(
                alreadyOrdered, orderedSlots, RangedPlacementMode.Rear)
                .SequenceEqual(Enumerable.Range(0, alreadyOrdered.Count)),
            "already role-ordered IDs remain stable when sorting is enabled");

        List<FormationPoint> centerSlots = FormationModel.BuildRelativeSlots(
            FormationKind.Block, 25, 5, 1, 0);
        var centerUnits = Enumerable.Range(1, 25)
            .Select(id => new FormationUnit(
                id, 0, id <= 16 ? FormationRole.Front :
                id <= 18 ? FormationRole.Neutral : FormationRole.Rear))
            .ToList();
        int[] centered = FormationModel.AssignSlotsByRole(
            centerUnits, centerSlots, RangedPlacementMode.Center);
        double meleeRadius = centerUnits.Select((unit, index) => new { unit, index })
            .Where(value => value.unit.Role == FormationRole.Front)
            .Average(value => SquaredRadius(centerSlots[centered[value.index]]));
        double coreRadius = centerUnits.Select((unit, index) => new { unit, index })
            .Where(value => value.unit.Role == FormationRole.Rear)
            .Average(value => SquaredRadius(centerSlots[centered[value.index]]));
        Check(meleeRadius > coreRadius && centered.Distinct().Count() == 25,
            "Center mode places melee outside a deterministic protected core");

        List<FormationPoint> circleSlots = FormationModel.BuildRelativeSlots(
            FormationKind.Circle, 25,
            FormationModel.ResolveAutomaticWidth(FormationKind.Circle, 25), 1, 3);
        int[] circleCentered = FormationModel.AssignSlotsByRole(
            centerUnits, circleSlots, RangedPlacementMode.Center, FormationKind.Circle);
        double circleMeleeRadius = centerUnits.Select((unit, index) => new { unit, index })
            .Where(value => value.unit.Role == FormationRole.Front)
            .Average(value => SquaredRadius(circleSlots[circleCentered[value.index]]));
        double circleCoreRadius = centerUnits.Select((unit, index) => new { unit, index })
            .Where(value => value.unit.Role == FormationRole.Rear)
            .Average(value => SquaredRadius(circleSlots[circleCentered[value.index]]));
        Check(circleMeleeRadius > circleCoreRadius &&
              circleCentered.Distinct().Count() == 25,
            "Circle Center mode places melee around its filled protected core");

        var vanillaNativeSlots = new List<FormationPoint>
        {
            new FormationPoint(0, 0, 2, 0),
            new FormationPoint(-1, 0, 2, -1),
            new FormationPoint(1, 0, 2, 1),
            new FormationPoint(0, -1, 3, 0),
            new FormationPoint(0, 1, 1, 0),
            new FormationPoint(-1, -1, 3, -1),
            new FormationPoint(1, -1, 3, 1),
            new FormationPoint(-1, 1, 1, -1),
            new FormationPoint(1, 1, 1, 1)
        };
        var vanillaUnits = Enumerable.Range(1, 9)
            .Select(id => new FormationUnit(
                id, 0, id <= 5 ? FormationRole.Front : FormationRole.Rear))
            .ToList();
        int[] vanillaCentered = FormationModel.AssignSlotsByRole(
            vanillaUnits, vanillaNativeSlots,
            RangedPlacementMode.Center, FormationKind.Vanilla);
        double vanillaMeleeRadius = vanillaUnits.Select((unit, index) => new { unit, index })
            .Where(value => value.unit.Role == FormationRole.Front)
            .Average(value => SquaredRadius(vanillaNativeSlots[vanillaCentered[value.index]]));
        double vanillaCoreRadius = vanillaUnits.Select((unit, index) => new { unit, index })
            .Where(value => value.unit.Role == FormationRole.Rear)
            .Average(value => SquaredRadius(vanillaNativeSlots[vanillaCentered[value.index]]));
        Check(vanillaMeleeRadius > vanillaCoreRadius,
            "Vanilla Center mode assigns melee to the outside of native slots");

        List<FormationPoint> lineSlots = FormationModel.BuildRelativeSlots(
            FormationKind.Line, 7, 7, 1, 0);
        var lineUnits = Enumerable.Range(1, 7)
            .Select(id => new FormationUnit(
                id, 0, id <= 4 ? FormationRole.Front : FormationRole.Rear))
            .ToList();
        int[] lineCentered = FormationModel.AssignSlotsByRole(
            lineUnits, lineSlots, RangedPlacementMode.Center);
        int maximumCoreDistance = lineUnits.Select((unit, index) => new { unit, index })
            .Where(value => value.unit.Role == FormationRole.Rear)
            .Max(value => Math.Abs(lineSlots[lineCentered[value.index]].File));
        int minimumMeleeDistance = lineUnits.Select((unit, index) => new { unit, index })
            .Where(value => value.unit.Role == FormationRole.Front)
            .Min(value => Math.Abs(lineSlots[lineCentered[value.index]].File));
        Check(maximumCoreDistance <= minimumMeleeDistance,
            "single-rank Center mode puts melee at both outer ends");
    }

    private static void TestDirectionAndDensityMatrix()
    {
        var vectors = new[]
        {
            new { X = 0, Y = -5, Sector = 0 },
            new { X = 5, Y = -5, Sector = 1 },
            new { X = 5, Y = 0, Sector = 2 },
            new { X = 5, Y = 5, Sector = 3 },
            new { X = 0, Y = 5, Sector = 4 },
            new { X = -5, Y = 5, Sector = 5 },
            new { X = -5, Y = 0, Sector = 6 },
            new { X = -5, Y = -5, Sector = 7 }
        };
        foreach (var vector in vectors)
        {
            Check(FormationModel.QuantizeDirection(vector.X, vector.Y) == vector.Sector,
                "eight-sector quantization " + vector.Sector);
            foreach (FormationKind kind in new[]
            {
                FormationKind.Block, FormationKind.Line,
                FormationKind.Column, FormationKind.Wedge, FormationKind.Circle
            })
            {
                int width = FormationModel.ResolveAutomaticWidth(kind, 31);
                List<FormationPoint> tightTopology = FormationModel.BuildRelativeSlots(
                    kind, 31, width, 1, vector.Sector);
                for (int density = 1; density <= 4; density++)
                {
                    List<FormationPoint> first = FormationModel.BuildRelativeSlots(
                        kind, 31, width, density, vector.Sector);
                    List<FormationPoint> second = FormationModel.BuildRelativeSlots(
                        kind, 31, width, density, vector.Sector);
                    Check(first.Select(point => point.X + ":" + point.Y)
                            .SequenceEqual(second.Select(point => point.X + ":" + point.Y)),
                        $"deterministic slots kind={kind}, sector={vector.Sector}, density={density}");
                    Check(first.Select(point => point.X + ":" + point.Y).Distinct().Count() == 31,
                        $"unique slots kind={kind}, sector={vector.Sector}, density={density}");
                    Check(first.Select(point => point.Rank + ":" + point.File)
                            .SequenceEqual(tightTopology.Select(point => point.Rank + ":" + point.File)),
                        $"density preserves topology kind={kind}, sector={vector.Sector}, density={density}");
                }
            }
        }

        Check(FormationModel.ResolveAutomaticWidth(FormationKind.Block, 1) == 1,
            "single-unit automatic width");
        Check(FormationModel.ResolveDraggedWidth(FormationKind.Block, 1, 20) == 4,
            "short drag retains automatic Block depth");
        Check(FormationModel.BuildRelativeSlots(
            FormationKind.Wedge, 1, 1, 4, 7).Single().Rank == 0,
            "single-unit wedge remains at the tip");
    }

    private static void TestRoleEdgeCases()
    {
        List<FormationPoint> slots = FormationModel.BuildRelativeSlots(
            FormationKind.Line, 10, 5, 1, 0);
        var withoutFront = Enumerable.Range(1, 10)
            .Select(id => new FormationUnit(
                id,
                0,
                id <= 6 ? FormationRole.Protected : FormationRole.Rear))
            .ToList();
        int[] first = FormationModel.AssignSlotsByRole(
            withoutFront, slots, RangedPlacementMode.Rear);
        int[] second = FormationModel.AssignSlotsByRole(
            withoutFront, slots, RangedPlacementMode.Rear);
        Check(first.SequenceEqual(second), "role sorting without melee is deterministic");
        Check(first.Distinct().Count() == withoutFront.Count,
            "protected overflow remains bijective");

        var tiny = new List<FormationUnit>
        {
            new FormationUnit(3, 0, FormationRole.Rear),
            new FormationUnit(2, 0, FormationRole.Protected),
            new FormationUnit(1, 0, FormationRole.Front)
        };
        List<FormationPoint> tinySlots = FormationModel.BuildRelativeSlots(
            FormationKind.Column, tiny.Count, 1, 2, 4);
        int[] tinyAssignment = FormationModel.AssignSlotsByRole(
            tiny, tinySlots, RangedPlacementMode.Rear);
        Check(tinySlots[tinyAssignment[2]].Rank <= tinySlots[tinyAssignment[0]].Rank,
            "tiny army keeps melee ahead of rear unit");

        int[] noMeleeCenter = FormationModel.AssignSlotsByRole(
            withoutFront, slots, RangedPlacementMode.Center);
        int[] noMeleeCenterAgain = FormationModel.AssignSlotsByRole(
            withoutFront, slots, RangedPlacementMode.Center);
        Check(noMeleeCenter.SequenceEqual(noMeleeCenterAgain) &&
              noMeleeCenter.Distinct().Count() == withoutFront.Count,
            "Center mode without melee remains deterministic and bijective");

        List<FormationPoint> wedgeSlots = FormationModel.BuildRelativeSlots(
            FormationKind.Wedge, 15, 7, 2, 3);
        var wedgeUnits = Enumerable.Range(1, 15)
            .Select(id => new FormationUnit(
                id, 0, id <= 4 ? FormationRole.Front :
                id <= 7 ? FormationRole.Neutral : FormationRole.Protected))
            .ToList();
        int[] wedgeCenter = FormationModel.AssignSlotsByRole(
            wedgeUnits, wedgeSlots, RangedPlacementMode.Center);
        Check(wedgeCenter.Distinct().Count() == wedgeUnits.Count,
            "Center mode handles protected overflow in a wedge");
    }

    private static void TestPreviewMarkerNormalization()
    {
        int[] normalized = FormationPreviewMarkerModel.NormalizeTileIds(new[]
        {
            FormationPreviewMarkerModel.NativeTileCount,
            12,
            -1,
            4,
            12,
            FormationPreviewMarkerModel.NativeTileCount - 1
        });
        Check(normalized.SequenceEqual(new[]
        {
            4,
            12,
            FormationPreviewMarkerModel.NativeTileCount - 1
        }), "preview tiles are valid, unique, and deterministic");

        int[] capped = FormationPreviewMarkerModel.NormalizeTileIds(
            Enumerable.Range(0, FormationPreviewMarkerModel.MaximumMarkers + 50).Reverse());
        Check(capped.Length == FormationPreviewMarkerModel.MaximumMarkers,
            "preview marker capacity is capped at 4000");
        Check(FormationPreviewMarkerModel.MaximumMarkers == 4000,
            "native mode-8 identity range exposes exactly 4000 preview markers");
        Check(capped[0] == 0 &&
              capped[capped.Length - 1] == FormationPreviewMarkerModel.MaximumMarkers - 1,
            "preview capacity selection is deterministic");
        Check(FormationPreviewMarkerModel.NormalizeTileIds(null).Length == 0,
            "null preview is empty");
    }

    private static void TestMoveOrderMatching()
    {
        Func<int, int, int, short, bool, int, bool> matches =
            (tribe, x, y, patrol, fresh, moveType) =>
                FormationOrderMatchModel.Matches(
                    42, 320, 240, 1, 3,
                    tribe, x, y, patrol, fresh, moveType);
        Check(matches(42, 320, 240, 0, true, 3),
            "exact pending move order matches Pre/Post identity");
        Check(!matches(41, 320, 240, 0, true, 3), "tribe mismatch rejected");
        Check(!matches(42, 319, 240, 0, true, 3), "X mismatch rejected");
        Check(!matches(42, 320, 241, 0, true, 3), "Y mismatch rejected");
        Check(!matches(42, 320, 240, 1, true, 3), "patrol mismatch rejected");
        Check(!matches(42, 320, 240, 0, false, 3), "new-order mismatch rejected");
        Check(!matches(42, 320, 240, 0, true, 2), "move-type mismatch rejected");
    }

    private static void TestEffectivePreviewKeys()
    {
        FormationPreviewKey vanilla = FormationPreviewKey.Create(
            FormationKind.Vanilla, 2, RangedPlacementMode.Off, 0, 4, 100, 200, 40);
        FormationPreviewKey vanillaDragged = FormationPreviewKey.Create(
            FormationKind.Vanilla, 2, RangedPlacementMode.Center, 7, 30, 100, 200, 40);
        Check(!vanilla.Equals(vanillaDragged),
            "managed Vanilla preview includes direction and placement");
        Check(vanilla.Equals(FormationPreviewKey.Create(
                FormationKind.Vanilla, 2, RangedPlacementMode.Off, 0, 99, 100, 200, 40)),
            "Vanilla preview ignores synthetic width changes");
        Check(!vanilla.Equals(FormationPreviewKey.Create(
                FormationKind.Vanilla, 2, RangedPlacementMode.Off,
                0, 4, 100, 200, 40, explicitDirection: true)),
            "Vanilla preview refreshes when the same sector becomes an explicit drag");
        FormationPreviewKey circle = FormationPreviewKey.Create(
            FormationKind.Circle, 2, RangedPlacementMode.Rear, 3, 5, 100, 200, 40);
        Check(circle.Equals(FormationPreviewKey.Create(
                FormationKind.Circle, 2, RangedPlacementMode.Rear, 3, 99, 100, 200, 40)),
            "Circle preview ignores synthetic width changes");
        Check(!vanilla.Equals(FormationPreviewKey.Create(
                FormationKind.Vanilla, 3, RangedPlacementMode.Off, 0, 4, 100, 200, 40)),
            "Vanilla preview changes for density");
        Check(!vanilla.Equals(FormationPreviewKey.Create(
                FormationKind.Vanilla, 2, RangedPlacementMode.Off, 0, 4, 101, 200, 40)),
            "Vanilla preview changes for target");

        FormationPreviewKey block = FormationPreviewKey.Create(
            FormationKind.Block, 2, RangedPlacementMode.Off, 3, 8, 100, 200, 40);
        Check(block.Equals(FormationPreviewKey.Create(
                FormationKind.Block, 2, RangedPlacementMode.Off, 3, 8, 100, 200, 40)),
            "same effective custom formation reuses preview");
        Check(!block.Equals(FormationPreviewKey.Create(
                FormationKind.Block, 2, RangedPlacementMode.Off, 4, 8, 100, 200, 40)),
            "custom preview changes for direction sector");
        Check(!block.Equals(FormationPreviewKey.Create(
                FormationKind.Block, 2, RangedPlacementMode.Off, 3, 9, 100, 200, 40)),
            "custom preview changes for width");
        Check(!block.Equals(FormationPreviewKey.Create(
                FormationKind.Block, 2, RangedPlacementMode.Center, 3, 8, 100, 200, 40)),
            "custom preview changes for ranged placement mode");
    }

    private static void TestDefaultsMigration()
    {
        FormationDefaultsMigration oldVanilla = FormationDefaultsMigration.Resolve(
            FormationKind.Vanilla,
            0);
        Check(oldVanilla.Kind == FormationKind.Block && oldVanilla.KindChanged &&
              oldVanilla.RevisionChanged &&
              oldVanilla.Revision == FormationDefaultsMigration.CurrentRevision,
            "old Vanilla default migrates once to Block");

        FormationDefaultsMigration oldCustom = FormationDefaultsMigration.Resolve(
            FormationKind.Line,
            0);
        Check(oldCustom.Kind == FormationKind.Line && !oldCustom.KindChanged &&
              oldCustom.RevisionChanged,
            "existing custom choice survives defaults migration");

        FormationDefaultsMigration deliberateVanilla = FormationDefaultsMigration.Resolve(
            FormationKind.Vanilla,
            FormationDefaultsMigration.CurrentRevision);
        Check(deliberateVanilla.Kind == FormationKind.Vanilla &&
              !deliberateVanilla.KindChanged && !deliberateVanilla.RevisionChanged,
            "deliberate Vanilla choice survives later starts");

        PlacementDefaultsMigration oldRear = PlacementDefaultsMigration.Resolve(
            1, legacyRearSorting: true, currentMode: RangedPlacementMode.Off);
        PlacementDefaultsMigration oldOff = PlacementDefaultsMigration.Resolve(
            1, legacyRearSorting: false, currentMode: RangedPlacementMode.Rear);
        PlacementDefaultsMigration migratedCenter = PlacementDefaultsMigration.Resolve(
            PlacementDefaultsMigration.CurrentRevision,
            legacyRearSorting: false,
            currentMode: RangedPlacementMode.Center);
        Check(oldRear.Mode == RangedPlacementMode.Rear && oldRear.Changed &&
              oldOff.Mode == RangedPlacementMode.Off && oldOff.Changed &&
              migratedCenter.Mode == RangedPlacementMode.Center && !migratedCenter.Changed,
            "legacy rear bool migrates once without overwriting later Center selection");
    }

    private static void TestInstalledRedBirdMarkerSpan()
    {
        string extender = Environment.GetEnvironmentVariable("SHCDESE_EXTENDER_DIR");
        if (string.IsNullOrWhiteSpace(extender))
        {
            extender =
                @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese";
        }
        ResolveEventHandler resolver = (sender, args) =>
        {
            string candidate = Path.Combine(
                extender,
                new AssemblyName(args.Name).Name + ".dll");
            return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
        };
        AppDomain.CurrentDomain.AssemblyResolve += resolver;
        try
        {
            foreach (string name in new[]
            {
                "Microsoft.Extensions.Logging.Abstractions",
                "Iced",
                "RedBird.Abstractions",
                "RedBird.Backends.NativeX64",
                "RedBird.Core",
                "RedBird.X64"
            })
                Assembly.LoadFrom(Path.Combine(extender, name + ".dll"));

            Assembly assembly = Assembly.LoadFrom(Path.Combine(extender, "RedBird.X64.dll"));
            Assembly nativeAssembly = Assembly.LoadFrom(
                Path.Combine(extender, "RedBird.Backends.NativeX64.dll"));
            Type type = assembly.GetType(
                "RedBird.X64.Hooks.X64InlineHook",
                throwOnError: true);
            byte[] bytes =
            {
                0x41, 0x0F, 0xB7, 0xBC, 0x59, 0x80, 0xEF, 0x75, 0x00,
                0x85, 0xFF,
                0x0F, 0x84, 0x81, 0x02, 0x00, 0x00
            };
            IntPtr memory = Marshal.AllocHGlobal(64);
            try
            {
                for (int index = 0; index < 64; index++)
                    Marshal.WriteByte(memory, index, 0x90);
                Marshal.Copy(bytes, 0, memory, bytes.Length);

                object candidate = Activator.CreateInstance(
                    type,
                    new object[]
                    {
                        unchecked((ulong)memory.ToInt64()),
                        14,
                        null,
                        "FormationTest marker span regression"
                    });
                try
                {
                    Check((int)type.GetProperty("DisplacedByteCount").GetValue(candidate) == 17,
                        "installed RedBird displaces the audited 17-byte marker span");
                    Check(!(bool)type.GetProperty("IsInstalled").GetValue(candidate),
                        "decode-only marker probe installs no hook");
                }
                finally
                {
                    ((IDisposable)candidate).Dispose();
                }

                var after = new byte[bytes.Length];
                Marshal.Copy(memory, after, 0, after.Length);
                Check(after.SequenceEqual(bytes),
                    "decode-only marker probe leaves fixture bytes unchanged");
            }
            finally
            {
                Marshal.FreeHGlobal(memory);
            }

            ProbeInstalledRedBirdSpan(
                type,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x20,
                    0x55, 0x56, 0x57, 0x41, 0x54,
                    0x41, 0x55, 0x41, 0x56,
                    0x41, 0x57
                },
                14,
                "terminal unit target");
            ProbeInstalledNativeDetourSpan(nativeAssembly);
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= resolver;
        }
    }

    private static void ProbeInstalledNativeDetourSpan(Assembly nativeAssembly)
    {
        byte[] bytes =
        {
            0x48, 0x89, 0x5C, 0x24, 0x08,
            0x48, 0x89, 0x6C, 0x24, 0x10,
            0x48, 0x89, 0x74, 0x24, 0x18,
            0x57, 0x41, 0x54, 0x41, 0x55
        };
        IntPtr memory = Marshal.AllocHGlobal(128);
        try
        {
            for (int index = 0; index < 128; index++)
                Marshal.WriteByte(memory, index, 0x90);
            Marshal.Copy(bytes, 0, memory, bytes.Length);

            Type backendType = nativeAssembly.GetType(
                "RedBird.Backends.NativeX64.NativeDetourBackend",
                throwOnError: true);
            Assembly abstractions = AppDomain.CurrentDomain.GetAssemblies().Single(
                candidate => candidate.GetName().Name == "RedBird.Abstractions");
            Type requestType = abstractions.GetType(
                "RedBird.Abstractions.Hooks.DetourRequest`1",
                throwOnError: true).MakeGenericType(typeof(CommonGroupProbeDelegate));
            object request = Activator.CreateInstance(requestType);
            CommonGroupProbeDelegate callback = CommonGroupProbe;
            requestType.GetProperty("Name").SetValue(request, "FormationTest common detour probe");
            requestType.GetProperty("TargetAddress").SetValue(
                request, unchecked((ulong)memory.ToInt64()));
            requestType.GetProperty("Callback").SetValue(request, callback);
            MethodInfo create = backendType.GetMethods()
                .Single(method => method.Name == "CreateDetour" &&
                    method.IsGenericMethodDefinition && method.GetParameters().Length == 1)
                .MakeGenericMethod(typeof(CommonGroupProbeDelegate));
            object candidate = create.Invoke(
                backendType.GetProperty("Instance").GetValue(null),
                new[] { request });
            GC.KeepAlive(callback);
            try
            {
                Type candidateType = candidate.GetType();
                Check((int)candidateType.GetProperty("DisplacedByteCount")
                        .GetValue(candidate) == 10,
                    "installed NativeDetour displaces the audited 10-byte common prologue");
                Check(!(bool)candidateType.GetProperty("IsInstalled").GetValue(candidate),
                    "native detour probe remains uninstalled");
                candidateType.GetMethod("Enable").Invoke(candidate, null);
                Check((bool)candidateType.GetProperty("IsInstalled").GetValue(candidate),
                    "first NativeDetour layer installs on the Vanilla prologue");

                requestType.GetProperty("Name").SetValue(
                    request, "FormationTest chained common detour probe");
                object chained = create.Invoke(
                    backendType.GetProperty("Instance").GetValue(null),
                    new[] { request });
                try
                {
                    Type chainedType = chained.GetType();
                    Check(chainedType.GetProperty("Scheme").GetValue(chained).ToString() ==
                            "Indirect" &&
                          (int)chainedType.GetProperty("DisplacedByteCount")
                            .GetValue(chained) == 6 &&
                          (int)chainedType.GetProperty("ChainDepth").GetValue(chained) == 2,
                        "second NativeDetour layer uses the supported indirect six-byte chain entry");
                    chainedType.GetMethod("Enable").Invoke(chained, null);
                    Check((bool)chainedType.GetProperty("IsInstalled").GetValue(chained),
                        "second NativeDetour layer installs");
                }
                finally
                {
                    ((IDisposable)chained).Dispose();
                }
                Check((bool)candidateType.GetProperty("IsInstalled").GetValue(candidate),
                    "disposing the upper layer restores the lower detour");
            }
            finally
            {
                ((IDisposable)candidate).Dispose();
            }
            var after = new byte[bytes.Length];
            Marshal.Copy(memory, after, 0, after.Length);
            Check(after.SequenceEqual(bytes),
                "native detour probe leaves common fixture bytes unchanged");
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    private static void TestNativeDetourEntryContract()
    {
        byte[] vanillaPrefix =
        {
            0x48, 0x89, 0x5C, 0x24, 0x08,
            0x48, 0x89, 0x6C, 0x24, 0x10,
            0x48, 0x89, 0x74, 0x24, 0x18
        };
        var vanilla = Enumerable.Repeat((byte)0x90,
            NativeDetourEntryContract.SnapshotLength).ToArray();
        Array.Copy(vanillaPrefix, vanilla, vanillaPrefix.Length);
        Check(NativeDetourEntryContract.Validate(
                vanilla, vanillaPrefix, "Indirect", 10, out bool vanillaChained) == 10 &&
              !vanillaChained,
            "Vanilla prologue rounds the indirect patch to ten complete bytes");

        var chained = Enumerable.Repeat((byte)0x90,
            NativeDetourEntryContract.SnapshotLength).ToArray();
        chained[0] = 0xFF;
        chained[1] = 0x25;
        Check(NativeDetourEntryContract.Validate(
                chained, vanillaPrefix, "Indirect", 6, out bool isChained) == 6 &&
              isChained,
            "existing indirect hook entry validates at six bytes");

        ExpectInvalidDetourContract(
            () => NativeDetourEntryContract.Validate(
                new byte[] { 0x48, 0x89, 0x5C, 0x24, 0x08, 0x48 },
                new byte[] { 0x48, 0x89, 0x5C, 0x24, 0x08, 0x48 },
                "Indirect", 6, out _),
            "truncated instruction is rejected");
        ExpectInvalidDetourContract(
            () => NativeDetourEntryContract.Validate(
                vanilla, vanillaPrefix, "Indirect", 6, out _),
            "backend span that splits the Vanilla prologue is rejected");
        ExpectInvalidDetourContract(
            () => NativeDetourEntryContract.Validate(
                vanilla, vanillaPrefix, "Unknown", 10, out _),
            "unknown RedBird detour scheme is rejected");
    }

    private static void ExpectInvalidDetourContract(Action action, string label)
    {
        bool rejected = false;
        try { action(); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, label);
    }

    private static long CommonGroupProbe(
        IntPtr manager, int tribeId, short x, short y, short patrol, int newOrder) => 0;

    private static void ProbeInstalledRedBirdSpan(
        Type hookType,
        byte[] bytes,
        int expectedSpan,
        string label)
    {
        IntPtr memory = Marshal.AllocHGlobal(64);
        try
        {
            for (int index = 0; index < 64; index++)
                Marshal.WriteByte(memory, index, 0x90);
            Marshal.Copy(bytes, 0, memory, bytes.Length);
            object candidate = Activator.CreateInstance(
                hookType,
                new object[]
                {
                    unchecked((ulong)memory.ToInt64()),
                    14,
                    null,
                    "FormationTest " + label + " span regression"
                });
            try
            {
                Check((int)hookType.GetProperty("DisplacedByteCount").GetValue(candidate) ==
                    expectedSpan, label + " uses the audited displaced span");
                Check(!(bool)hookType.GetProperty("IsInstalled").GetValue(candidate),
                    label + " decode-only probe installs no hook");
            }
            finally
            {
                ((IDisposable)candidate).Dispose();
            }
            var after = new byte[bytes.Length];
            Marshal.Copy(memory, after, 0, after.Length);
            Check(after.SequenceEqual(bytes),
                label + " decode-only probe leaves fixture bytes unchanged");
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    private static void TestSourceSafetyContracts()
    {
        string projectRoot = FindProjectRoot();
        string[] sourceFiles = Directory.GetFiles(
            Path.Combine(projectRoot, "src"), "*.cs", SearchOption.AllDirectories);
        string source = string.Join("\n", sourceFiles.Select(File.ReadAllText));
        string troopPatch = File.ReadAllText(Path.Combine(projectRoot,
            "Patches", "Assets", "GUI", "XAMLResources", "HUD_Troops.xaml"));
        string screenPatch = File.ReadAllText(Path.Combine(projectRoot,
            "Patches", "Assets", "GUI", "XAML", "IngameUIScreens.xaml"));
        foreach (string forbidden in new[]
        {
            "System.Text.Json", "Newtonsoft.Json", "JavaScriptSerializer",
            "System.Web.Extensions", "DataContractJsonSerializer", "JsonUtility",
            "OnDestroy(", "OnDisable(", "OnApplicationQuit("
        })
            Check(source.IndexOf(forbidden, StringComparison.Ordinal) < 0,
                "forbidden runtime pattern: " + forbidden);

        Check(source.Contains("StandardSelectorRva = 0xE1D30"),
            "standard selector RVA contract");
        Check(source.Contains("AssassinSelectorRva = 0xE0970"),
            "assassin selector RVA contract");
        Check(source.Contains("NativeDetourEntryContract.Capture") &&
              source.Contains("NativeDetourEntryContract.Validate") &&
              source.Contains("detour.Scheme.ToString()") &&
              source.Contains("detour.ChainDepth"),
            "native detours validate the live entry against the selected backend scheme");
        Check(!source.Contains("ExpectedSelectorDisplacedBytes") &&
              !source.Contains("ExpectedCommonGroupDisplacedBytes"),
            "native detour compatibility is not fixed to an obsolete backend span");
        Check(source.Contains("CommonGroupMoveRva = 0x118E00"),
            "common group path RVA contract");
        Check(source.Contains("UnitMoveTargetRva = 0x196280") &&
              source.Contains("ExpectedUnitMoveTargetAuditBytes = 14"),
            "terminal unit target RVA and native audit-span contract");
        Check(source.Contains("TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable") &&
              source.Contains("tribeMoveSubscription = pendingTribeMove"),
            "move-order Pre/Post subscription is process-rooted");
        Check(source.Contains("UnitR3EventHooks.OnUnitMoveHere.Observable") &&
              source.Contains("unitMoveSubscription = pendingUnitMove") &&
              !source.Contains("unitMoveTargetHandle"),
            "terminal target replacement uses the Extender-owned 0x196280 event");
        string applyPacket = ExtractMethodBody(source, "private bool ApplyPacket(");
        Check(applyPacket.Contains("pendingCommand = command") &&
              !applyPacket.Contains("BuildManagedDestinations("),
            "packet application only stages the pending command before Vanilla dispatch");
        string moveEvent = ExtractMethodBody(
            source, "private void OnTribeIssueOrderMoveHere(");
        Check(moveEvent.Contains("EventHookPhase.Pre") &&
              moveEvent.Contains("EventHookPhase.Post") &&
              moveEvent.Contains("finally") &&
              moveEvent.Contains("activeCommand = null"),
            "move-order context is activated in Pre and cleared in Post finally");
        string commonHook = ExtractMethodBody(source, "private long CommonGroupMoveHook(");
        Check(CountOccurrences(commonHook, "commonGroupMoveHandle.Original(") == 1,
            "common group detour calls its original exactly once");
        string unitHook = ExtractMethodBody(source, "private void OnUnitMoveHere(");
        Check(unitHook.Contains("TryGetUnitDestination(") &&
              unitHook.Contains("args.TileX = destination.X") &&
              unitHook.Contains("args.TileY = destination.Y") &&
              unitHook.Contains("args.Phase != EventHookPhase.Post") &&
              unitHook.Contains("args.ReturnValue > 0") &&
              unitHook.Contains("frame.PreArgs.SkipOriginalFunction") &&
              !unitHook.Contains("commonGroupCommand") &&
              !unitHook.Contains("Original("),
            "all managed paths substitute targets and confirm their LIFO frame in Post");
        Check(source.Contains("r_AttackMoveToTargetTileX = (ushort)destination.X") &&
              source.Contains("FinishUnitAssignmentFrame(frame, false)") &&
              source.Contains("UnitAssignmentFrame Parent"),
            "terminal path synchronizes and rolls back Vanilla's persistent attack target");
        Check(source.Contains("Dictionary<int, NativeDestination>") &&
              source.Contains("Invalid or duplicate native unit identity") &&
              source.Contains("unit->r_GlobalId == globalId"),
            "terminal mapping uses validated unit game IDs and global identities");
        Check(source.Contains("FORMATION_ORDER_FELL_BACK_TO_VANILLA") &&
              source.Contains("FORMATION_ORDER_PARTIAL_FALLBACK") &&
              source.Contains("formationSucceeded={successfulFormationTargets}") &&
              source.Contains("expected={completed.ExpectedCount}"),
            "completion distinguishes complete, partial, and Vanilla fallback");
        Check(unitHook.Contains("args.UnitId == frame.UnitId") &&
              unitHook.Contains("args.TileX == frame.OriginalX") &&
              unitHook.Contains("args.Unknown == frame.OriginalUnknown") &&
              source.Contains("RecordTerminalResult("),
            "Post matches original Extender arguments and records terminal results");
        Check(source.Contains("GameUnitManagerAPI.Instance.MoveToTile(") &&
              source.Contains("UnitFallbackAttempt") &&
              source.Contains("FORMATION_UNIT_FELL_BACK_TO_VANILLA"),
            "failed terminal targets retry the command anchor under a scoped guard");
        Check(source.Contains("ComputePlanHash(") &&
              source.Contains("RangedPlacementMode placementMode") &&
              source.Contains("Formation plan hash mismatch") &&
              source.Contains("ProtocolVersion = 5") &&
              source.Contains("private const int FieldCount = 14") &&
              source.Contains("[Key(9)] public byte PlacementMode") &&
              source.Contains("[Key(12)] public ushort UnitCount") &&
              source.Contains("[Key(13)] public ulong PlanHash"),
            "protocol 5 validates placement mode and reconstructed unit-to-slot plan");
        Check(source.Contains("result.Sort((left, right) => left.UnitId.CompareTo(right.UnitId))"),
            "dispatch units use canonical one-based unit ID order");
        Check(source.Contains("ExpectedVisibleTileDisplacedBytes = 17") &&
              source.Contains("VisibleTileHookRva = 0x436DE"),
            "native green marker hook span contract");
        Check(source.Contains("Placement = OverwrittenInstructionPlacement.AfterCallback") &&
              source.Contains("Registers = X64SmartCPUContextRegs.All"),
            "marker callback runs before TEST/JE with preserved general registers");
        Check(source.Contains("0x6B, 0x52 + frame") &&
              source.Contains("6 - GetTerrainHeight(registers, tileId)"),
            "animated green Vanilla marker and height contract");
        Check(source.Contains("MainViewModel.instance.IsMapEditorMode") &&
              source.Contains("if (!isMapEditor"),
            "map editor bypasses only normal ownership filtering");
        Check(!source.Contains("private void OnGUI()") &&
              source.Contains("FindGlobalElement(\"FormationTestPreviewCanvas\")") &&
              source.Contains("List<Ellipse>") && source.Contains("Line[] ArrowLines"),
            "preview uses pooled Noesis shapes instead of Unity IMGUI");
        Check(source.Contains("selected.Length > FormationPreviewMarkerModel.MaximumMarkers"),
            "commands cannot exceed the complete green preview capacity");
        Check(source.Contains("markerRenderer?.ClearPreviewMarkerTiles()") &&
              source.Contains("FormationPreviewOverlay.Clear()"),
            "native markers and role dots share the clear path");
        Check(source.Contains("ConfigSettings.Settings_SH1RTSControls ? 0 : 1") &&
              source.Contains("mouseButton == 0 ? KeyCode.Mouse0 : KeyCode.Mouse1"),
            "both Vanilla mouse-control schemes use the formation gesture");
        Check(source.Contains("!markerRenderer.ReplacementAvailable") &&
              source.Contains("FORMATION_PREVIEW_MARKER_FAIL_OPEN"),
            "an unavailable native preview leaves the drag disabled and fails open");
        Check(source.Contains("InputR3EventHooks.OnKey.Observable.Subscribe(OnKeyHeld)") &&
              source.Contains("keyHeldSubscription = pendingKeyHeld"),
            "held-input subscription is process-rooted");
        Check(source.Contains("RequireMainThread(\"input-down\")") &&
              source.Contains("RequireMainThread(\"input-held\")") &&
              source.Contains("RequireMainThread(\"input-up\")"),
            "all Unity input handlers enforce the captured main thread");
        string engineRun = ExtractMethodBody(source, "private int EngineRunHook(");
        Check(!engineRun.Contains("UpdateGesture(") &&
              !engineRun.Contains("Input.") &&
              !engineRun.Contains("TryCaptureTarget(") &&
              !engineRun.Contains("CalcMapTileFromMousePos") &&
              engineRun.Contains("EvaluateFixedGroundTarget(state.Target)") &&
              engineRun.IndexOf("EvaluateFixedGroundTarget(state.Target)",
                  StringComparison.Ordinal) <
              engineRun.IndexOf("TryClaimVanillaRelease(inputState)",
                  StringComparison.Ordinal),
            "simulation hook performs no Unity cursor or map query");
        string held = ExtractMethodBody(source, "private void OnKeyHeld(");
        Check(held.Contains("EvaluateFixedGroundTarget(state.Target)") &&
              held.Contains("AbortDrag(") && held.Contains("UpdateGesture(state)"),
            "held input revalidates the fixed ground target before live updates");
        Check(source.Contains("TryCaptureCommandTarget(") &&
              source.Contains("grabTroopsOnScreen(") &&
              source.Contains("r_HoverOverUnitId") &&
              source.Contains("r_HoverOverBuildingId") &&
              source.Contains("r_HoveringOverWall") &&
              source.Contains("TileUnitIdGrid") &&
              source.Contains("StructureGrid") &&
              source.Contains("movementTargetAvailability"),
            "formation drag starts only from a coherent object-free native ground target");
        string updateGesture = ExtractMethodBody(
            source, "private void UpdateGesture(");
        Check(updateGesture.Contains("lock (stateSync)") &&
              updateGesture.Contains("ReferenceEquals(drag, state)") &&
              updateGesture.Contains("PublishPreview(state, force: false)"),
            "gesture snapshots and previews are published atomically against release");
        string released = ExtractMethodBody(source, "private void OnKeyUp(");
        Check(released.IndexOf("UpdateGesture(state)", StringComparison.Ordinal) >= 0 &&
              released.IndexOf("UpdateGesture(state)", StringComparison.Ordinal) <
              released.IndexOf("ObserveInputRelease()", StringComparison.Ordinal) &&
              released.IndexOf("ObserveInputRelease()", StringComparison.Ordinal) <
              released.IndexOf("ClearPreview()", StringComparison.Ordinal),
            "release performs a final cursor update before publication");
        Check(source.Contains("FormationPreviewKey.Create(") &&
              !source.Contains("LastPreviewDeltaX") &&
              !source.Contains("LastPreviewDeltaY"),
            "preview cache uses effective parameters instead of raw mouse tiles");
        Check(source.Contains("CameraControls2D.instance") &&
              source.Contains("result != null ? result : Camera.main") &&
              source.Contains("host.ActualWidth / Screen.width") &&
              source.Contains("Canvas.SetLeft") &&
              source.Contains("FORMATION_OVERLAY_SUMMARY"),
            "Noesis role overlay uses the gameplay camera and canvas scale");
        Check(source.Contains("pointCount = showRoleMarkers ? current.Points.Length : 0") &&
              source.Contains("RenderArrow(camera, host, current.Direction)") &&
              source.Contains("default: return neutralBrush"),
            "role-marker visibility leaves the arrow active and unknown roles yellow");
        Check(source.Contains("FormationDirectionIndicator") &&
              source.Contains("BuildDirectionIndicator(") &&
              source.Contains("RenderArrow(camera, host") &&
              source.Contains("FormationDirectionIndicator.Hidden") &&
              source.Contains("lengthScale = direction.ExplicitDirection ? 0.5f : 0.2f") &&
              source.Contains("strokeThickness = direction.ExplicitDirection ? 2f : 1f") &&
              source.Contains("opacity = direction.ExplicitDirection ? 0.72f : 0.4f"),
            "all managed previews publish distinct subtle click and drag arrows");
        Check(source.Contains("CaptureVanillaDestinations(") &&
              source.Contains("MaximumVanillaSelectorCandidates = 4001") &&
              source.Contains("IsNativeVanillaSlotCandidate(") &&
              source.Contains("(logicFlags & 0x10000100) == 0") &&
              source.Contains("BuildVanillaSlotMetadata(") &&
              !source.Contains("BuildVanillaBlob"),
            "Vanilla uses native BFS ordering with density and Assassin filtering");
        string vanillaCapture = ExtractMethodBody(
            source, "private NativeDestination[] CaptureVanillaDestinations(");
        int vanillaLeft = vanillaCapture.IndexOf(
            "current.X - 1, current.Y, 0x40", StringComparison.Ordinal);
        int vanillaRight = vanillaCapture.IndexOf(
            "current.X + 1, current.Y, 0x04", StringComparison.Ordinal);
        int vanillaUp = vanillaCapture.IndexOf(
            "current.X, current.Y - 1, 0x01", StringComparison.Ordinal);
        int vanillaUpLeft = vanillaCapture.IndexOf(
            "current.X - 1, current.Y - 1, 0x80", StringComparison.Ordinal);
        int vanillaUpRight = vanillaCapture.IndexOf(
            "current.X + 1, current.Y - 1, 0x02", StringComparison.Ordinal);
        int vanillaDown = vanillaCapture.IndexOf(
            "current.X, current.Y + 1, 0x10", StringComparison.Ordinal);
        int vanillaDownLeft = vanillaCapture.IndexOf(
            "current.X - 1, current.Y + 1, 0x20", StringComparison.Ordinal);
        int vanillaDownRight = vanillaCapture.IndexOf(
            "current.X + 1, current.Y + 1, 0x08", StringComparison.Ordinal);
        Check(vanillaLeft >= 0 && vanillaLeft < vanillaRight &&
              vanillaRight < vanillaUp && vanillaUp < vanillaUpLeft &&
              vanillaUpLeft < vanillaUpRight && vanillaUpRight < vanillaDown &&
              vanillaDown < vanillaDownLeft && vanillaDownLeft < vanillaDownRight,
            "Vanilla BFS expands neighbors in the audited native order");
        Check(troopPatch.Contains("FormationTestButtonHost") &&
              troopPatch.Contains("FormationTestMenuHost") &&
              troopPatch.Contains("FormationTestRolloverHost") &&
              troopPatch.Contains("Margin=\"480,11,0,0\"") &&
              troopPatch.Contains("Margin=\"140,3,0,0\"") &&
              troopPatch.Contains("VerticalAlignment=\"Top\"") &&
              troopPatch.Contains("SelectFormationCommand") &&
              troopPatch.Contains("SelectDensityCommand") &&
              troopPatch.Contains("SelectPlacementCommand") &&
              troopPatch.Contains("SelectRoleMarkersCommand") &&
              troopPatch.Contains("CommandParameter=\"Circle\"") &&
              troopPatch.Contains("Width=\"40\" Height=\"40\"") &&
              !troopPatch.Contains("ToolTip=\"Formations\"") &&
              troopPatch.Contains("CommandParameter=\"Center\"") &&
              CountOccurrences(troopPatch, "x:Name=\"FormationTestRoleMarker") == 3 &&
              troopPatch.Contains("x:Name=\"FormationTestRoleMarkerFront\"") &&
              troopPatch.Contains("x:Name=\"FormationTestRoleMarkerProtected\"") &&
              troopPatch.Contains("x:Name=\"FormationTestRoleMarkerRear\"") &&
              CountOccurrences(troopPatch, "<Operation Type=\"Add\"") == 3 &&
              screenPatch.Contains("FormationTestPreviewCanvas") &&
              screenPatch.Contains("IsHitTestVisible=\"False\""),
            "compact formation menu and click-through preview host are patched into Noesis");
        string publishPreview = ExtractMethodBody(source, "private void PublishPreview(");
        Check(!publishPreview.Contains("? destinations[index].Role") &&
              publishPreview.Contains("destinations[index].Role"),
            "role colors are published independently of placement mode");
        Check(source.Contains("\"Formation\", \"Kind\", FormationKind.Block") &&
              source.Contains("FormationDefaultsMigration.Resolve(") &&
              source.Contains("PlacementDefaultsMigration.Resolve(") &&
              source.Contains("\"Formation\", \"RangedPlacement\"") &&
              source.Contains("\"Formation\", \"ShowRoleMarkers\"") &&
              source.Contains("SetRoleMarkersVisible"),
            "Block default, placement migration, and local role-marker setting are wired");
        Check(engineRun.Contains("TryClaimVanillaRelease(inputState)") &&
              !engineRun.Contains("!nativeRelease || !releaseObserved") &&
              source.Contains("FORMATION_RELEASE_CLAIMED") &&
              source.Contains("releaseEventSeen="),
            "native release is authoritative even without an input-up event");
        Check(source.Contains("stale-release-before-new-down") &&
              source.Contains("state.ReleaseGate.CanModify") &&
              source.Contains("FORMATION_RELEASE_EVENT"),
            "released drags cannot consume or modify later mouse input");
        string selectionMatches = ExtractMethodBody(
            source, "private static bool SelectionMatches(");
        Check(selectionMatches.Contains("UnitId") &&
              selectionMatches.Contains("GlobalId") &&
              selectionMatches.Contains("UnitType") &&
              !selectionMatches.Contains(".X") &&
              !selectionMatches.Contains(".Y"),
            "moving selected units do not invalidate a drag solely by position");
        Check(source.Contains("RequireEditorField(\"stateRead\", typeof(bool))") &&
              source.Contains("RequireEditorField(\"upPending\", typeof(bool))") &&
              source.Contains("RequireEditorField(\"rightDownForEngine\", typeof(bool))") &&
              !source.Contains("ConsumeAuxiliaryInput") &&
              !source.Contains("FORMATION_AUXILIARY_CONSUMED") &&
              !source.Contains("args.Result = false") &&
              !source.Contains("RunOriginalWithReleaseTemporarilyDeferred"),
            "auxiliary mouse input is never blocked or retained");
        Check(!source.Contains("RightClickOpenHook") &&
              !source.Contains("StartSelectionHook") &&
              !source.Contains("AuxiliaryMouseCapture") &&
              !source.Contains("KeyCode.Mouse2"),
            "obsolete auxiliary-button hooks, middle-click controls, and capture state are absent");
        Check(source.Contains("\"FormationTestButtonHost\", formationMenu") &&
              source.Contains("\"FormationTestMenuHost\", formationMenu") &&
              source.Contains("\"FormationTestRolloverHost\", formationMenu") &&
              source.Contains("main.Show_HUD_ControlGroups = false") &&
              source.Contains("Formation menu unavailable; movement runtime remains active"),
            "formation menu is bound, mutually exclusive, and fails independently of movement");
        string consumedRelease = ExtractMethodBody(
            source, "private int RunOriginalAfterReleaseConsumed(");
        Check(consumedRelease.Contains("clearMouseStateForEngine()") &&
              consumedRelease.Contains(
                  "ApplyMouseState(director, FormationReleaseStateModel.Consume())") &&
              !consumedRelease.Contains("ApplyMouseState(director, originalState)") &&
              consumedRelease.Contains("FORMATION_RELEASE_CONSUMED"),
            "accepted release is consumed permanently and diagnosed");
        Check(source.Contains("DispatchDisposition.Accepted") &&
              source.Contains("FORMATION_UNEXPECTED_SECOND_ORDER"),
            "dispatch acceptance and duplicate-order diagnostics are explicit");
        Check(source.Contains("TryIssuePacketVanillaFallback(") &&
              source.Contains("reason={reason}, issued={issued}") &&
              source.Contains("packet.PlacementMode > (byte)RangedPlacementMode.Center"),
            "unknown protocol or placement values use the packet's safe Vanilla fallback");
        Check(source.Contains("RunOriginalOnce(mpFrameSkip, ref originalEntered)") &&
              source.Contains("if (originalEntered)") &&
              source.Contains("throw;"),
            "the original engine run cannot be replayed after it was entered");
        Check(source.Contains("MovementTargetAvailabilityRva = 0x3A11EA4"),
            "walkable-target grid contract");
        Check(source.Contains("SendScriptExtenderChorePayload"),
            "Chore-only transport requested");
        Check(!source.Contains("SendPacketToAllEx2") &&
              !source.Contains("SendPacketToAll(packet"),
            "no unsynchronized Steam fallback");
        string packetReceiver = ExtractMethodBody(
            source, "private void OnPacketReceived(");
        Check(packetReceiver.Contains("LogErrorNoThrow") &&
              !packetReceiver.Contains("DebugLogHelper"),
            "Chore receiver logging is deferred away from the simulation callback");
        Check(source.Contains("UnityMainThreadDispatch.InitializeForCurrentThread()") &&
              source.Contains("UnityMainThreadDispatch.TryRunInlineOrEnqueue"),
            "FormationTest captures and uses the validated main-thread dispatcher");
        Check(source.Contains("BepInDependency(ScriptExtenderGuid, \"2.9.0\")"),
            "runtime requires Script Extender 2.9.0 selection semantics");
        string manifest = File.ReadAllText(Path.Combine(projectRoot, "info.json"));
        Check(manifest.Contains("\"MinimumScriptExtenderVersion\": \"2.9.0\""),
            "manifest requires Script Extender 2.9.0");
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "FormationTest.csproj")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("FormationTest project root not found.");
    }

    private static string ExtractMethodBody(string source, string signature)
    {
        int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        if (signatureIndex < 0)
            throw new InvalidOperationException("Method signature not found: " + signature);
        int openingBrace = source.IndexOf('{', signatureIndex);
        if (openingBrace < 0)
            throw new InvalidOperationException("Method body not found: " + signature);
        int depth = 0;
        for (int index = openingBrace; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}' && --depth == 0)
                return source.Substring(openingBrace, index - openingBrace + 1);
        }
        throw new InvalidOperationException("Method body is incomplete: " + signature);
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }

    private static long SquaredRadius(FormationPoint point) =>
        (long)point.X * point.X + (long)point.Y * point.Y;

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
