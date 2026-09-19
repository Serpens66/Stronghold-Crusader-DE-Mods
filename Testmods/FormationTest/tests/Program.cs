using FormationTest;
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
            TestDirectionQuantization();
            TestShapesAndDensity();
            TestRoleAssignment();
            TestDirectionAndDensityMatrix();
            TestRoleEdgeCases();
            TestEffectivePreviewKeys();
            TestDefaultsMigration();
            TestReleaseStateModel();
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

    private static void TestPlanHash()
    {
        ulong first = FormationPlanHash.Begin(2);
        FormationPlanHash.AddEntry(
            ref first, 1, 1001, 400, 401, FormationRole.Front);
        FormationPlanHash.AddEntry(
            ref first, 2, 1002, 402, 403, FormationRole.Rear);

        ulong repeated = FormationPlanHash.Begin(2);
        FormationPlanHash.AddEntry(
            ref repeated, 1, 1001, 400, 401, FormationRole.Front);
        FormationPlanHash.AddEntry(
            ref repeated, 2, 1002, 402, 403, FormationRole.Rear);
        Check(first == repeated, "formation plan hash is deterministic");

        ulong reordered = FormationPlanHash.Begin(2);
        FormationPlanHash.AddEntry(
            ref reordered, 2, 1002, 402, 403, FormationRole.Rear);
        FormationPlanHash.AddEntry(
            ref reordered, 1, 1001, 400, 401, FormationRole.Front);
        Check(first != reordered, "formation plan hash includes canonical unit order");

        ulong changedTarget = FormationPlanHash.Begin(2);
        FormationPlanHash.AddEntry(
            ref changedTarget, 1, 1001, 400, 401, FormationRole.Front);
        FormationPlanHash.AddEntry(
            ref changedTarget, 2, 1002, 402, 404, FormationRole.Rear);
        Check(first != changedTarget, "formation plan hash includes destinations");
    }

    private static void TestReleaseStateModel()
    {
        var leftCommandWithAuxiliaryRelease = new FormationMouseState(
            leftState: 2,
            rightUp: true,
            stateRead: false,
            upPending: true);
        FormationMouseState deferredLeft =
            FormationReleaseStateModel.SuppressAuxiliaryReleaseForOneRun(
                leftCommandWithAuxiliaryRelease, commandButton: 0);
        Check(deferredLeft.LeftState == 2 && !deferredLeft.RightUp,
            "left-command drag suppresses only the auxiliary right release");
        Check(!deferredLeft.StateRead && deferredLeft.UpPending,
            "temporary left suppression preserves the queued state machine");
        Check(leftCommandWithAuxiliaryRelease.RightUp &&
              leftCommandWithAuxiliaryRelease.UpPending,
            "temporary suppression does not mutate its snapshot");

        var rightCommandWithAuxiliaryRelease = new FormationMouseState(
            leftState: 3,
            rightUp: false,
            stateRead: true,
            upPending: false);
        FormationMouseState deferredRight =
            FormationReleaseStateModel.SuppressAuxiliaryReleaseForOneRun(
                rightCommandWithAuxiliaryRelease, commandButton: 1);
        Check(!deferredRight.RightUp && deferredRight.LeftState == 0,
            "right-command drag suppresses only the auxiliary left release");
        Check(deferredRight.StateRead && !deferredRight.UpPending,
            "temporary right suppression preserves the queued state machine");

        var leftRelease = new FormationMouseState(3, false, false, true);
        Check(FormationReleaseStateModel.SuppressAuxiliaryReleaseForOneRun(
                  leftRelease, commandButton: 0).LeftState == 3,
            "temporary deferral never hides the authoritative left release");
        var inputFirst = new FormationReleaseGate(commandButton: 0);
        Check(inputFirst.ObserveInputRelease() && inputFirst.ReleaseEventSeen &&
              !inputFirst.VanillaReleaseClaimed && !inputFirst.CanModify,
            "input release freezes the gesture while awaiting Vanilla");
        Check(inputFirst.TryClaimVanillaRelease(leftRelease) &&
              inputFirst.VanillaReleaseClaimed &&
              !inputFirst.TryClaimVanillaRelease(leftRelease),
            "input-first release is claimed exactly once");

        var rightRelease = new FormationMouseState(0, true, true, false);
        Check(FormationReleaseStateModel.SuppressAuxiliaryReleaseForOneRun(
                  rightRelease, commandButton: 1).RightUp,
            "temporary deferral never hides the authoritative right release");
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
        Check(consumed.LeftState == 0 && !consumed.RightUp &&
              consumed.StateRead && !consumed.UpPending,
            "accepted dispatch permanently consumes both release mechanisms");
        Check(!FormationReleaseStateModel.HasCommandRelease(consumed, 0) &&
              !FormationReleaseStateModel.HasCommandRelease(consumed, 1),
            "consumed state cannot issue a follow-up command");
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
            FormationKind.Vanilla, 2, false, 0, 4, 100, 200, 40);
        FormationPreviewKey vanillaDragged = FormationPreviewKey.Create(
            FormationKind.Vanilla, 2, true, 7, 30, 100, 200, 40);
        Check(vanilla.Equals(vanillaDragged),
            "Vanilla preview ignores direction, width, and rear sorting");
        Check(!vanilla.Equals(FormationPreviewKey.Create(
                FormationKind.Vanilla, 3, false, 0, 4, 100, 200, 40)),
            "Vanilla preview changes for density");
        Check(!vanilla.Equals(FormationPreviewKey.Create(
                FormationKind.Vanilla, 2, false, 0, 4, 101, 200, 40)),
            "Vanilla preview changes for target");

        FormationPreviewKey block = FormationPreviewKey.Create(
            FormationKind.Block, 2, false, 3, 8, 100, 200, 40);
        Check(block.Equals(FormationPreviewKey.Create(
                FormationKind.Block, 2, false, 3, 8, 100, 200, 40)),
            "same effective custom formation reuses preview");
        Check(!block.Equals(FormationPreviewKey.Create(
                FormationKind.Block, 2, false, 4, 8, 100, 200, 40)),
            "custom preview changes for direction sector");
        Check(!block.Equals(FormationPreviewKey.Create(
                FormationKind.Block, 2, false, 3, 9, 100, 200, 40)),
            "custom preview changes for width");
        Check(!block.Equals(FormationPreviewKey.Create(
                FormationKind.Block, 2, true, 3, 8, 100, 200, 40)),
            "custom preview changes for rear sorting");
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
        Check(source.Contains("ComputePlanHash(units, destinations)") &&
              source.Contains("Formation plan hash mismatch") &&
              source.Contains("ProtocolVersion = 2") &&
              source.Contains("private const int FieldCount = 14") &&
              source.Contains("[Key(12)] public ushort UnitCount") &&
              source.Contains("[Key(13)] public ulong PlanHash"),
            "protocol 2 validates the reconstructed unit-to-slot plan");
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
        Check(!source.Contains("GUI.Label("),
            "preview has no HUD text label");
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
              !engineRun.Contains("CalcMapTileFromMousePos"),
            "simulation hook performs no Unity cursor or map query");
        string held = ExtractMethodBody(source, "private void OnKeyHeld(");
        Check(held.Contains("UpdateGesture(state)"),
            "held input owns live gesture updates");
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
        Check(source.Contains("Event.current.type != EventType.Repaint") &&
              source.Contains("Camera camera = Camera.main") &&
              !source.Contains("screen.z < 0f") &&
              source.Contains("outlineTexture") &&
              source.Contains("FORMATION_OVERLAY_SUMMARY"),
            "role overlay renders only during repaint and caches the camera");
        string publishPreview = ExtractMethodBody(source, "private void PublishPreview(");
        Check(!publishPreview.Contains("? destinations[index].Role") &&
              publishPreview.Contains("destinations[index].Role"),
            "role colors are published independently of rear sorting");
        Check(source.Contains("\"Formation\", \"Kind\", FormationKind.Block") &&
              source.Contains("FormationDefaultsMigration.Resolve("),
            "Block default and one-time defaults migration are wired");
        Check(engineRun.Contains("TryClaimVanillaRelease(releaseState)") &&
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
              source.Contains("RunOriginalWithReleaseTemporarilyDeferred") &&
              source.Contains("ApplyMouseState(director, originalState)") &&
              source.Contains("SuppressAuxiliaryReleaseForOneRun") &&
              !source.Contains("suppressCommandRelease"),
            "deferred release restores the complete managed input state machine");
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
        Check(source.Contains("BepInDependency(ScriptExtenderGuid, \"2.8.0\")"),
            "runtime requires Script Extender 2.8.0 identity semantics");
        string manifest = File.ReadAllText(Path.Combine(projectRoot, "info.json"));
        Check(manifest.Contains("\"MinimumScriptExtenderVersion\": \"2.8.0\""),
            "manifest requires Script Extender 2.8.0");
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

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
