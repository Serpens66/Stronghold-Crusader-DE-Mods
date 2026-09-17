using FormationTest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal static class Program
{
    private static int assertions;

    private static int Main()
    {
        try
        {
            TestFormationCycleAndDensity();
            TestAutomaticWidths();
            TestDirectionQuantization();
            TestShapesAndDensity();
            TestRoleAssignment();
            TestDirectionAndDensityMatrix();
            TestRoleEdgeCases();
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

    private static void TestFormationCycleAndDensity()
    {
        FormationKind current = FormationKind.Vanilla;
        var observed = new List<FormationKind>();
        for (int index = 0; index < 5; index++)
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
            FormationKind.Wedge
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
        Check(FormationModel.ResolveDraggedWidth(20, 2, 100) == 10,
            "drag distance maps to width");
        Check(FormationModel.ResolveDraggedWidth(999, 1, 12) == 12,
            "drag width clamps to unit count");
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

    private static void TestShapesAndDensity()
    {
        foreach (FormationKind kind in new[]
        {
            FormationKind.Block,
            FormationKind.Line,
            FormationKind.Column,
            FormationKind.Wedge
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
        int[] assignment = FormationModel.AssignSlotsByRole(units, slots, true);
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

        int[] unchanged = FormationModel.AssignSlotsByRole(units, slots, false);
        Check(unchanged.SequenceEqual(Enumerable.Range(0, units.Count)),
            "disabled role sorting preserves order");
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
            for (int density = 1; density <= 4; density++)
            {
                List<FormationPoint> first = FormationModel.BuildRelativeSlots(
                    FormationKind.Block, 31, 7, density, vector.Sector);
                List<FormationPoint> second = FormationModel.BuildRelativeSlots(
                    FormationKind.Block, 31, 7, density, vector.Sector);
                Check(first.Select(point => point.X + ":" + point.Y)
                        .SequenceEqual(second.Select(point => point.X + ":" + point.Y)),
                    $"deterministic slots sector={vector.Sector}, density={density}");
                Check(first.Select(point => point.X + ":" + point.Y).Distinct().Count() == 31,
                    $"unique slots sector={vector.Sector}, density={density}");
            }
        }

        Check(FormationModel.ResolveAutomaticWidth(FormationKind.Block, 1) == 1,
            "single-unit automatic width");
        Check(FormationModel.ResolveDraggedWidth(1, 4, 20) == 1,
            "short drag clamps to one file");
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
        int[] first = FormationModel.AssignSlotsByRole(withoutFront, slots, true);
        int[] second = FormationModel.AssignSlotsByRole(withoutFront, slots, true);
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
        int[] tinyAssignment = FormationModel.AssignSlotsByRole(tiny, tinySlots, true);
        Check(tinySlots[tinyAssignment[2]].Rank <= tinySlots[tinyAssignment[0]].Rank,
            "tiny army keeps melee ahead of rear unit");
    }

    private static void TestSourceSafetyContracts()
    {
        string projectRoot = FindProjectRoot();
        string[] sourceFiles = Directory.GetFiles(
            Path.Combine(projectRoot, "src"), "*.cs", SearchOption.AllDirectories);
        string source = string.Join("\n", sourceFiles.Select(File.ReadAllText));
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
        Check(source.Contains("DisplacedByteCount != ExpectedSelectorDisplacedBytes"),
            "RedBird displaced-span check");
        Check(source.Contains("ReleaseObserved") &&
              source.Contains("suppressCommandRelease: nativeRelease"),
            "stale release cannot leak into Vanilla");
        Check(source.Contains("MovementTargetAvailabilityRva = 0x3A11EA4"),
            "walkable-target grid contract");
        Check(source.Contains("SendScriptExtenderChorePayload"),
            "Chore-only transport requested");
        Check(!source.Contains("SendPacketToAllEx2") &&
              !source.Contains("SendPacketToAll(packet"),
            "no unsynchronized Steam fallback");
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

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
