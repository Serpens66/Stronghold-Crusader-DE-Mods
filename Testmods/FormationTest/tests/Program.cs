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
            TestMoveOrderMatching();
            TestPreviewMarkerNormalization();
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
        Check(source.Contains("DisplacedByteCount != ExpectedSelectorDisplacedBytes"),
            "RedBird displaced-span check");
        Check(source.Contains("ExpectedSelectorDisplacedBytes = 10"),
            "selector detours use the observed 10-byte span");
        Check(source.Contains("CommonGroupMoveRva = 0x118E00") &&
              source.Contains("ExpectedCommonGroupDisplacedBytes = 10"),
            "common group path RVA and displaced-span contract");
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
        string applyPacket = ExtractMethodBody(source, "private void ApplyPacket(");
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
              !unitHook.Contains("Original("),
            "common path substitutes targets and confirms its LIFO frame in Post");
        Check(source.Contains("r_AttackMoveToTargetTileX = (ushort)destination.X") &&
              source.Contains("FinishUnitAssignmentFrame(frame, false)") &&
              source.Contains("UnitAssignmentFrame Parent"),
            "common path synchronizes and rolls back Vanilla's persistent attack target");
        Check(source.Contains("Dictionary<int, NativeDestination>") &&
              source.Contains("Invalid or duplicate native unit identity") &&
              source.Contains("unit->r_GlobalId == globalId"),
            "common path mapping uses validated unit game IDs and global identities");
        Check(source.Contains("FORMATION_ORDER_FELL_BACK_TO_VANILLA") &&
              source.Contains("assigned={completed.AssignedCount}") &&
              source.Contains("expected={completed.ExpectedCount}"),
            "completion distinguishes observed native assignments from Vanilla fallback");
        Check(source.Contains("r_TargetTilePositionX == pair.Value.X") &&
              source.Contains("r_TargetTilePositionY == pair.Value.Y") &&
              source.Contains("FORMATION_TARGET_VERIFICATION_MISMATCH"),
            "Post verifies the stored native unit targets against assigned slots");
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
        string released = ExtractMethodBody(source, "private void OnKeyUp(");
        Check(released.IndexOf("UpdateGesture(state)", StringComparison.Ordinal) >= 0 &&
              released.IndexOf("UpdateGesture(state)", StringComparison.Ordinal) <
              released.IndexOf("ReleaseObserved = true", StringComparison.Ordinal),
            "release performs a final cursor update before publication");
        Check(source.Contains("FormationPreviewKey.Create(") &&
              !source.Contains("LastPreviewDeltaX") &&
              !source.Contains("LastPreviewDeltaY"),
            "preview cache uses effective parameters instead of raw mouse tiles");
        Check(source.Contains("Event.current.type != EventType.Repaint") &&
              source.Contains("Camera camera = Camera.main"),
            "role overlay renders only during repaint and caches the camera");
        Check(source.Contains("\"Formation\", \"Kind\", FormationKind.Block") &&
              source.Contains("FormationDefaultsMigration.Resolve("),
            "Block default and one-time defaults migration are wired");
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
        string packetReceiver = ExtractMethodBody(
            source, "private void OnPacketReceived(");
        Check(packetReceiver.Contains("LogErrorNoThrow") &&
              !packetReceiver.Contains("DebugLogHelper"),
            "Chore receiver logging is deferred away from the simulation callback");
        Check(source.Contains("UnityMainThreadDispatch.InitializeForCurrentThread()") &&
              source.Contains("UnityMainThreadDispatch.TryRunInlineOrEnqueue"),
            "FormationTest captures and uses the validated main-thread dispatcher");
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
