using SerpNativeAPI;
using SHCDESE.EventAPI;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace SerpNativeAPITests
{
    internal static class Program
    {
        private const long ModuleBase = 0x10000000;
        private const int FunctionRva = 0x1100;
        private const int FunctionSize = 0x300;
        private const int DistanceRva = 0x1180;
        private const int DecisionRva = 0x1200;
        private const int HumanDelayRva = 0x1280;
        private static readonly byte[] VanillaDistanceBytes = Hex("01 02 03 04");
        private static readonly byte[] CenteredDistanceBytes = Hex("05 06 07 08");
        private static readonly byte[] SupportedCenteredDistanceBytes = Hex(
            "44 0F BF 84 2A 0E 8B 7E 06 0F BF 8C 2A 10 8B 7E 06 0F BF 84 2B 0A CD 4C 06 " +
            "44 01 F8 C1 E0 02 44 29 C0 99 31 D0 29 D0 41 89 C0 0F BF 84 2B 0C CD 4C 06 " +
            "44 01 E0 C1 E0 02 29 C8 99 31 D0 29 D0 41 39 C0 44 0F 4C C0 90 90 90 90 90");
        private static readonly byte[] DecisionBytes = Hex(
            "40 84 F6 75 10 41 81 F8 C8 00 00 00 7D 10 B8 B0 04 00 00 EB 69 41 81 F8 8C 00 00 00 7C 5B");
        private static readonly byte[] HumanDelayBytes = Hex("EB 50 B8 64 00 00 00 48 8D 2D C0 83 F4 FF");
        private static int failures;

        private static int Main()
        {
            TestPublicSurface();
            TestPeValidation();
            TestFixedCatalogValidation();
            TestReadinessAndIndependentCapabilities();
            TestOwnership();
            TestCenteredDistanceSemantics();
            TestGatehouseDistanceOriginTransaction();
            TestGatehouseTransactionAndRounding();
            TestGatehouseRollbackAndPageCleanup();
            TestSelectedBroker();
            TestSelectedEventService();
            TestMigrationContracts();
            if (failures == 0)
            {
                Console.WriteLine("PASS: SerpNativeAPI baseline-hardened tests passed.");
                return 0;
            }
            Console.Error.WriteLine($"FAIL: SerpNativeAPI tests reported {failures} failure(s).");
            return 1;
        }

        private static void TestMigrationContracts()
        {
            string workspace = FindWorkspaceRoot();
            string plugin = File.ReadAllText(Path.Combine(workspace, "SerpNativeAPI", "src", "SerpNativeAPIPlugin.cs"));
            string project = File.ReadAllText(Path.Combine(workspace, "SerpNativeAPI", "SerpNativeAPI.csproj"));
            string sourceManifest = File.ReadAllText(Path.Combine(workspace, "SerpNativeAPI", "info.json"));
            string packageManifest = File.ReadAllText(Path.Combine(workspace, "SerpNativeAPI", "BepInEx", "plugins", "SerpNativeAPI_Serp", "info.json"));

            Assert(plugin.Contains("[BepInDependency(ScriptExtenderGuid, \"2.0.2\")]"),
                "plugin requires SHCDESE 2.0.2");
            Assert(plugin.Contains("OnLibraryLoaded(CrusaderLibraryLoadContext context)"),
                "plugin consumes CrusaderLibraryLoadContext");
            Assert(plugin.Contains("context.ModuleHandle.ToInt64()") && plugin.Contains("context.Memory"),
                "plugin passes the 2.0.2 module and memory view");
            Assert(!plugin.Contains("IntPtr libraryHandle") && !plugin.Contains("ReadOnlySpan<byte> memory"),
                "old LibraryLoaded callback is absent");
            Assert(!project.Contains("Zhuqiaomon") && !project.Contains("PolyHook"),
                "project has no obsolete native dependency");
            Assert(sourceManifest.Contains("\"Version\": \"0.1.0\"") && sourceManifest.Contains("\"NetworkMode\": 1"),
                "source manifest preserves version and declares gameplay mode");
            Assert(packageManifest.Contains("\"Version\": \"0.1.0\"") && packageManifest.Contains("\"NetworkMode\": 1"),
                "package manifest preserves version and declares gameplay mode");
        }

        private static string FindWorkspaceRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "SerpNativeAPI")) &&
                    File.Exists(Path.Combine(directory.FullName, "UpdatePlan-SHCDESE-2.0.2.md")))
                {
                    return directory.FullName;
                }
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("Workspace root was not found.");
        }

        private static void TestPublicSurface()
        {
            var expected = new HashSet<string>(StringComparer.Ordinal)
            {
                "SerpNativeAPI.GatehouseDistanceOrigin",
                "SerpNativeAPI.GatehouseTimingSettings",
                "SerpNativeAPI.GatehouseTimingValues",
                "SerpNativeAPI.IGatehouseDistanceOriginCapability",
                "SerpNativeAPI.IGatehouseTimingCapability",
                "SerpNativeAPI.ISelectedUnitCommandCapability",
                "SerpNativeAPI.ISelectedUnitCommandRegistration",
                "SerpNativeAPI.ISerpNativeApi",
                "SerpNativeAPI.NativeApiState",
                "SerpNativeAPI.NativeCapabilityDiagnostic",
                "SerpNativeAPI.NativeCapabilityIds",
                "SerpNativeAPI.NativeCapabilityState",
                "SerpNativeAPI.SelectedUnitCommandContext",
                "SerpNativeAPI.SerpNativeApi",
                // BepInEx discovers the plugin type; it is public but is not a consumer service.
                "SerpNativeAPI.SerpNativeAPIPlugin"
            };

            Type[] exported = typeof(ISerpNativeApi).Assembly.GetExportedTypes();
            foreach (Type type in exported)
            {
                Assert(expected.Remove(type.FullName), $"unexpected exported API type: {type.FullName}");
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    AssertSafePublicType(method.ReturnType, $"{type.FullName}.{method.Name} return type");
                    foreach (ParameterInfo parameter in method.GetParameters())
                        AssertSafePublicType(parameter.ParameterType, $"{type.FullName}.{method.Name} parameter {parameter.Name}");
                }
                foreach (ConstructorInfo constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    foreach (ParameterInfo parameter in constructor.GetParameters())
                        AssertSafePublicType(parameter.ParameterType, $"{type.FullName} constructor parameter {parameter.Name}");
                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    AssertSafePublicType(property.PropertyType, $"{type.FullName}.{property.Name} property type");
                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    AssertSafePublicType(field.FieldType, $"{type.FullName}.{field.Name} field type");
            }

            foreach (string missing in expected)
                Assert(false, $"expected exported API type is missing: {missing}");

            var expectedAcquisitionMethods = new HashSet<string>(StringComparer.Ordinal)
            {
                "TryGetGatehouseDistanceOrigin",
                "TryGetGatehouseTiming",
                "TryGetSelectedUnitCommand"
            };
            foreach (MethodInfo method in typeof(ISerpNativeApi).GetMethods())
                expectedAcquisitionMethods.Remove(method.Name);
            foreach (string missing in expectedAcquisitionMethods)
                Assert(false, $"expected capability acquisition method is missing: ISerpNativeApi.{missing}");

            Assert(NativeCapabilityIds.GatehouseDistanceOrigin == "gatehouse-distance-origin",
                "distance-origin capability ID must remain stable");
            Assert(NativeCapabilityIds.GatehouseTiming == "gatehouse-timing",
                "gatehouse-timing capability ID must remain stable");
        }

        private static void AssertSafePublicType(Type type, string location)
        {
            while (type.IsByRef || type.IsArray)
                type = type.GetElementType();
            bool forbidden = type.IsPointer || type == typeof(IntPtr) || type == typeof(UIntPtr) ||
                type.FullName.IndexOf("NativeDetour", StringComparison.OrdinalIgnoreCase) >= 0 ||
                type.FullName.IndexOf("MemoryWriter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                type.FullName.IndexOf("Pattern", StringComparison.OrdinalIgnoreCase) >= 0 ||
                type.FullName.IndexOf("Rva", StringComparison.OrdinalIgnoreCase) >= 0;
            Assert(!forbidden, $"forbidden native implementation type at {location}: {type.FullName}");
        }

        private static void TestPeValidation()
        {
            byte[] image = CreatePeImage(0x4000, true);
            NativePeImage pe = NativePeImage.Parse(image);
            Assert(pe.ImageSize == image.Length, "PE image size should parse");
            AssertThrowsState(NativeCapabilityState.ValidationFailed,
                () => pe.RequireExecutableRange(0x200, 1, "header"), "headers are not executable targets");

            byte[] nonExecutable = CreatePeImage(0x4000, false);
            GatehouseBuildTarget catalog = InstallTestGatehouse(nonExecutable);
            var runtime = InitializeRuntime(nonExecutable, catalog, SeedRuntimeMemory(nonExecutable, catalog), new FakeEventSource());
            Assert(!runtime.TryGetGatehouseTiming("owner", out _, out NativeCapabilityDiagnostic diagnostic) &&
                diagnostic.State == NativeCapabilityState.ValidationFailed, "gatehouse function must be executable");
            Assert(!runtime.TryGetGatehouseDistanceOrigin("owner", out _, out diagnostic) &&
                diagnostic.State == NativeCapabilityState.ValidationFailed, "distance origin also requires an executable gatehouse function");
        }

        private static void TestFixedCatalogValidation()
        {
            byte[] image = CreatePeImage(0x4000, true);
            GatehouseBuildTarget catalog = InstallTestGatehouse(image);
            FakeMemory memory = SeedRuntimeMemory(image, catalog);
            SerpNativeApiRuntime runtime = InitializeRuntime(image, catalog, memory, new FakeEventSource());
            Assert(runtime.TryGetGatehouseTiming("owner", out _, out NativeCapabilityDiagnostic available) &&
                available.State == NativeCapabilityState.Available && available.Reason.Contains("function SHA-256"),
                "matching fixed catalog should validate with provenance");
            Assert(runtime.TryGetGatehouseDistanceOrigin("origin-owner", out _, out NativeCapabilityDiagnostic originAvailable) &&
                originAvailable.State == NativeCapabilityState.Available && originAvailable.Reason.Contains("function SHA-256"),
                "matching distance-origin catalog should validate with provenance");
            Assert(memory.WriteCount == 0,
                "initialization and capability acquisition must not activate either gatehouse gameplay change");

            byte[] wrongHashImage = (byte[])image.Clone();
            var wrongHashCatalog = CloneCatalog(catalog, functionHash: new string('0', 64));
            runtime = InitializeRuntime(wrongHashImage, wrongHashCatalog, SeedRuntimeMemory(wrongHashImage, wrongHashCatalog), new FakeEventSource());
            AssertBothGateValidationFailures(runtime, "wrong function hash must fail both gatehouse capabilities");

            byte[] wrongOpcode = (byte[])image.Clone();
            wrongOpcode[DecisionRva] ^= 1;
            Copy(wrongOpcode, 0x1500, DecisionBytes); // A decoy must never be used as a fallback.
            GatehouseBuildTarget wrongOpcodeCatalog = CloneCatalog(catalog, functionHash: SerpNativeApiRuntime.ComputeSha256(
                new ReadOnlySpan<byte>(wrongOpcode, FunctionRva, FunctionSize)));
            runtime = InitializeRuntime(wrongOpcode, wrongOpcodeCatalog, SeedRuntimeMemory(wrongOpcode, wrongOpcodeCatalog), new FakeEventSource());
            AssertTimingValidationFailure(runtime, "wrong timing opcode must fail without accepting a decoy");
            Assert(runtime.TryGetGatehouseDistanceOrigin("owner", out _, out _),
                "a timing-only opcode mismatch must not disable distance-origin capability");

            byte[] wrongImmediate = (byte[])image.Clone();
            WriteInt32(wrongImmediate, DecisionRva + 8, 201);
            GatehouseBuildTarget wrongImmediateCatalog = CloneCatalog(catalog, functionHash: SerpNativeApiRuntime.ComputeSha256(
                new ReadOnlySpan<byte>(wrongImmediate, FunctionRva, FunctionSize)));
            runtime = InitializeRuntime(wrongImmediate, wrongImmediateCatalog, SeedRuntimeMemory(wrongImmediate, wrongImmediateCatalog), new FakeEventSource());
            AssertTimingValidationFailure(runtime, "wrong Vanilla immediate must fail timing");
            Assert(runtime.TryGetGatehouseDistanceOrigin("owner", out _, out _),
                "a timing immediate mismatch must not disable distance-origin capability");

            byte[] wrongDistance = (byte[])image.Clone();
            wrongDistance[DistanceRva] ^= 1;
            GatehouseBuildTarget wrongDistanceCatalog = CloneCatalog(catalog, functionHash: SerpNativeApiRuntime.ComputeSha256(
                new ReadOnlySpan<byte>(wrongDistance, FunctionRva, FunctionSize)));
            runtime = InitializeRuntime(wrongDistance, wrongDistanceCatalog, SeedRuntimeMemory(wrongDistance, wrongDistanceCatalog), new FakeEventSource());
            AssertOriginValidationFailure(runtime, "wrong Vanilla distance block must fail distance origin");
            Assert(runtime.TryGetGatehouseTiming("owner", out _, out _),
                "a distance-origin mismatch must not disable gatehouse timing");

            GatehouseBuildTarget outside = CloneCatalog(catalog, aiCloseDistanceRva: FunctionRva - 4);
            runtime = InitializeRuntime(image, outside, SeedRuntimeMemory(image, catalog), new FakeEventSource());
            AssertTimingValidationFailure(runtime, "catalogued immediate outside function must fail timing");
            Assert(runtime.TryGetGatehouseDistanceOrigin("owner", out _, out _),
                "an invalid timing address must not disable distance origin");
        }

        private static void TestReadinessAndIndependentCapabilities()
        {
            byte[] image = CreatePeImage(0x4000, true);
            var runtime = new SerpNativeApiRuntime();
            Assert(!runtime.TryGetGatehouseDistanceOrigin("owner", out _, out NativeCapabilityDiagnostic originPending) &&
                originPending.State == NativeCapabilityState.Pending, "pre-initialization origin query should be Pending");
            Assert(!runtime.TryGetGatehouseTiming("owner", out _, out NativeCapabilityDiagnostic pending) &&
                pending.State == NativeCapabilityState.Pending, "pre-initialization query should be Pending");
            int readyBefore = 0;
            runtime.WhenReady(_ => readyBefore++);
            var memory = new FakeMemory();
            var events = new FakeEventSource();
            runtime.Initialize(ModuleBase, image, "UNKNOWN", memory, events, null);
            Assert(runtime.State == NativeApiState.Ready && readyBefore == 1, "unknown build should still publish Ready");
            Assert(!runtime.TryGetGatehouseTiming("owner", out _, out NativeCapabilityDiagnostic gate) &&
                gate.State == NativeCapabilityState.UnsupportedBuild, "unknown build should disable only gatehouse");
            Assert(!runtime.TryGetGatehouseDistanceOrigin("owner", out _, out NativeCapabilityDiagnostic origin) &&
                origin.State == NativeCapabilityState.UnsupportedBuild, "unknown build should disable distance origin without mutation");
            Assert(runtime.TryGetSelectedUnitCommand("owner", out ISelectedUnitCommandCapability selected, out NativeCapabilityDiagnostic selectedDiagnostic) &&
                selectedDiagnostic.State == NativeCapabilityState.Available, "event capability should support unknown native hashes");
            Assert(memory.OperationCount == 0 && events.SubscribeCount == 0, "unknown build performs no native operation or eager subscription");
            Assert(selected.TryRegisterBefore(_ => { }, out _, out _) && events.SubscribeCount == 1,
                "event capability should subscribe lazily on unknown hashes");
            int readyAfter = 0;
            runtime.WhenReady(_ => readyAfter++);
            Assert(readyAfter == 1, "post-initialization readiness callback should be synchronous");

            runtime = new SerpNativeApiRuntime();
            runtime.Initialize(0, ReadOnlySpan<byte>.Empty, string.Empty, new FakeMemory(), new FakeEventSource(), null);
            Assert(runtime.State == NativeApiState.Ready, "missing native module is a gate capability error, not a global failure");
            Assert(runtime.TryGetSelectedUnitCommand("owner", out _, out _), "selected event survives missing native module");
            Assert(!runtime.TryGetGatehouseTiming("owner", out _, out NativeCapabilityDiagnostic missingHash) &&
                missingHash.State == NativeCapabilityState.UnsupportedBuild, "missing hash is unsupported for gatehouse");
            Assert(!runtime.TryGetGatehouseDistanceOrigin("owner", out _, out NativeCapabilityDiagnostic missingOriginHash) &&
                missingOriginHash.State == NativeCapabilityState.UnsupportedBuild, "missing hash is unsupported for distance origin");
        }

        private static void TestOwnership()
        {
            var registry = new NativeOwnershipRegistry();
            var first = new[] { new NativeInterval(100, 110) };
            Assert(registry.TryReserve("A", "cap", NativeReservationMode.Exclusive, first, out _), "first reservation");
            Assert(registry.TryReserve("A", "cap", NativeReservationMode.Exclusive, first, out _), "same reservation is idempotent");
            Assert(registry.TryReserve("B", "other", NativeReservationMode.Exclusive,
                new[] { new NativeInterval(110, 120) }, out _), "adjacent half-open intervals do not overlap");
            Assert(!registry.TryReserve("C", "third", NativeReservationMode.Exclusive,
                new[] { new NativeInterval(109, 111) }, out string conflict) && conflict == "A",
                "exclusive overlap identifies the first owner");
        }

        private static void TestCenteredDistanceSemantics()
        {
            GatehouseBuildTarget supported = GatehouseBuildTarget.Supported;
            Assert(supported.DistanceBlockRva == 0xB7B70 &&
                supported.DistanceBlockRva + supported.VanillaDistanceBlockBytes.Length == 0xB7BBB,
                "distance patch must occupy exactly the post-hook Vanilla arithmetic block");
            Assert(supported.VanillaDistanceBlockBytes.Length == 75 &&
                supported.CenteredDistanceBlockBytes.Length == supported.VanillaDistanceBlockBytes.Length,
                "centered distance patch must preserve the 75-byte block size");
            AssertSequenceEqual(supported.CenteredDistanceBlockBytes, SupportedCenteredDistanceBytes,
                "supported centered distance bytes must match the reviewed crash-safe sequence");

            var originTarget = new GatehouseDistanceOriginTarget(
                ModuleBase + supported.DistanceBlockRva,
                supported.VanillaDistanceBlockBytes,
                supported.CenteredDistanceBlockBytes);
            var timingTarget = new GatehouseTimingTarget(
                ModuleBase + supported.AiCloseDistanceRva,
                ModuleBase + supported.AiReopenDelayRva,
                ModuleBase + supported.HumanCloseDistanceRva,
                ModuleBase + supported.HumanReopenDelayRva);
            Assert(originTarget.Intervals.Count == 1 &&
                originTarget.Intervals[0].Start == ModuleBase + 0xB7B70 &&
                originTarget.Intervals[0].End == ModuleBase + 0xB7BBB,
                "distance-origin capability must own exactly [0xB7B70, 0xB7BBB)");
            Assert(timingTarget.Intervals.Count == 4 &&
                timingTarget.Intervals[0].Start == ModuleBase + 0xB7BC3 && timingTarget.Intervals[0].End == ModuleBase + 0xB7BC7 &&
                timingTarget.Intervals[1].Start == ModuleBase + 0xB7BCA && timingTarget.Intervals[1].End == ModuleBase + 0xB7BCE &&
                timingTarget.Intervals[2].Start == ModuleBase + 0xB7BD3 && timingTarget.Intervals[2].End == ModuleBase + 0xB7BD7 &&
                timingTarget.Intervals[3].Start == ModuleBase + 0xB7C35 && timingTarget.Intervals[3].End == ModuleBase + 0xB7C39,
                "timing capability must own exactly the four immediate intervals");
            foreach (NativeInterval timingInterval in timingTarget.Intervals)
                Assert(!originTarget.Intervals[0].Overlaps(timingInterval),
                    "distance-origin and timing intervals must be disjoint");

            byte[] unitYLoad = Hex("0F BF 8C 2A 10 8B 7E 06");
            int unitYLoadOffset = IndexOfSequence(supported.CenteredDistanceBlockBytes, unitYLoad);
            int firstCdqOffset = Array.IndexOf(supported.CenteredDistanceBlockBytes, (byte)0x99);
            Assert(unitYLoadOffset == 9 && unitYLoadOffset + unitYLoad.Length <= firstCdqOffset,
                "unit Y must be loaded through the live RDX unit offset before CDQ overwrites RDX");

            Assert(GatehouseDistanceOriginService.ComputeCenteredDistanceNative(10, 20, 12, 24, 88, 176) == 0,
                "integer midpoint should map to zero native distance");
            Assert(GatehouseDistanceOriginService.ComputeCenteredDistanceNative(10, 20, 11, 23, 84, 172) == 0,
                "half-tile midpoint should remain exact in native coordinates");
            Assert(GatehouseDistanceOriginService.ComputeCenteredDistanceNative(12, 24, 10, 20, 88, 176) == 0,
                "reversed bounds should produce the same midpoint");
            Assert(GatehouseDistanceOriginService.ComputeCenteredDistanceNative(10, 20, 12, 24, 80, 176) == 8 &&
                GatehouseDistanceOriginService.ComputeCenteredDistanceNative(10, 20, 12, 24, 96, 176) == 8,
                "opposite horizontal approaches should have equal distance");
            Assert(GatehouseDistanceOriginService.ComputeCenteredDistanceNative(10, 20, 12, 24, 80, 168) == 8 &&
                GatehouseDistanceOriginService.ComputeCenteredDistanceNative(10, 20, 12, 24, 96, 184) == 8,
                "diagonal approaches should retain Vanilla Chebyshev distance");
        }

        private static void TestGatehouseDistanceOriginTransaction()
        {
            FakeMemory memory = SeedDirectGateMemory();
            var ownership = new NativeOwnershipRegistry();
            var mutationSync = new object();
            IGatehouseDistanceOriginCapability origin =
                CreateOriginService(memory, ownership, mutationSync).Bind("BugfixesAndQoL_Serp");
            IGatehouseTimingCapability timing =
                CreateGateService(memory, ownership, mutationSync).Bind("ExtraFeatures_Serp");

            Assert(!origin.TryApply((GatehouseDistanceOrigin)99, out NativeCapabilityDiagnostic invalid) &&
                invalid.State == NativeCapabilityState.ValidationFailed && memory.WriteCount == 0,
                "unknown distance-origin values fail before native mutation");
            Assert(origin.TryApply(GatehouseDistanceOrigin.BuildingBoundsCenter, out NativeCapabilityDiagnostic centered) &&
                centered.CapabilityId == NativeCapabilityIds.GatehouseDistanceOrigin,
                "Bugfixes owner should apply the centered distance origin");
            AssertBytes(memory, ModuleBase + 0xB7B70, CenteredDistanceBytes,
                "distance-origin capability writes only the centered block");
            Assert(memory.ReadRaw(ModuleBase + 0xB7BC3) == 200 && memory.ReadRaw(ModuleBase + 0xB7C35) == 100,
                "distance-origin mutation leaves all timing values Vanilla");
            int writes = memory.WriteCount;
            Assert(origin.TryApply(GatehouseDistanceOrigin.BuildingBoundsCenter, out _) && memory.WriteCount == writes,
                "identical distance-origin apply is idempotent");

            Assert(timing.TryApply(new GatehouseTimingSettings(true, 1, 5, 10, 15), out NativeCapabilityDiagnostic timingApplied) &&
                timingApplied.CapabilityId == NativeCapabilityIds.GatehouseTiming,
                "different owners can reserve the adjacent timing and origin intervals");
            AssertBytes(memory, ModuleBase + 0xB7B70, CenteredDistanceBytes,
                "timing mutation leaves the independently selected origin unchanged");

            Assert(origin.TryApply(GatehouseDistanceOrigin.VanillaBuildingBegin, out _),
                "distance-origin capability restores Vanilla on explicit request");
            AssertBytes(memory, ModuleBase + 0xB7B70, VanillaDistanceBytes,
                "Vanilla distance-origin request restores all original bytes");
            Assert(memory.ReadRaw(ModuleBase + 0xB7BC3) == 120 && memory.ReadRaw(ModuleBase + 0xB7C35) == 40,
                "restoring the origin does not change customized timing values");

            memory.SetByte(ModuleBase + 0xB7B70, 0x90);
            Assert(!origin.TryApply(GatehouseDistanceOrigin.BuildingBoundsCenter, out NativeCapabilityDiagnostic changed) &&
                changed.State == NativeCapabilityState.ValidationFailed,
                "external distance-block mutation fails closed");

            FakeMemory rollbackMemory = SeedDirectGateMemory();
            origin = CreateOriginService(rollbackMemory, new NativeOwnershipRegistry(), new object()).Bind("owner");
            rollbackMemory.FailNextWriteByteAddress = ModuleBase + 0xB7B72;
            Assert(!origin.TryApply(GatehouseDistanceOrigin.BuildingBoundsCenter, out _),
                "partial centered-block write fails");
            AssertBytes(rollbackMemory, ModuleBase + 0xB7B70, VanillaDistanceBytes,
                "partial centered-block write restores the complete Vanilla block");

            FakeMemory conflictMemory = SeedDirectGateMemory();
            var conflictRegistry = new NativeOwnershipRegistry();
            GatehouseDistanceOriginService originService =
                CreateOriginService(conflictMemory, conflictRegistry, new object());
            Assert(originService.Bind("A").TryApply(GatehouseDistanceOrigin.BuildingBoundsCenter, out _),
                "first distance-origin owner applies");
            Assert(!originService.Bind("B").TryApply(GatehouseDistanceOrigin.VanillaBuildingBegin, out NativeCapabilityDiagnostic conflict) &&
                conflict.State == NativeCapabilityState.Conflict && conflict.ConflictOwnerGuid == "A",
                "second distance-origin owner receives conflict diagnostics");
        }

        private static void TestGatehouseTransactionAndRounding()
        {
            FakeMemory memory = SeedDirectGateMemory();
            IGatehouseTimingCapability capability = CreateGateService(memory, new NativeOwnershipRegistry(), new object()).Bind("owner");
            Assert(!capability.TryApply(new GatehouseTimingSettings(true, double.NaN, 0, 5, 5), out NativeCapabilityDiagnostic invalid) &&
                invalid.State == NativeCapabilityState.ValidationFailed, "non-finite gatehouse input should fail");
            AssertThrows<ArgumentOutOfRangeException>(
                () => GatehouseTimingService.ConvertNativeUInt16(8192, 8, "value"), "native UInt16 overflow should fail");

            var rounded = new GatehouseTimingSettings(true, 0.0125, 0.0125, 5.0625, 5.0625);
            Assert(capability.TryApply(rounded, out NativeCapabilityDiagnostic applied) &&
                applied.Reason.Contains("41units") && applied.Reason.Contains("1ticks"),
                "AwayFromZero values and verified native units should be diagnosed");
            Assert(memory.ReadRaw(ModuleBase + 0xB7BC3) == 41 && memory.ReadRaw(ModuleBase + 0xB7BCA) == 1 &&
                memory.ReadRaw(ModuleBase + 0xB7BD3) == 41 && memory.ReadRaw(ModuleBase + 0xB7C35) == 1,
                "all four rounded values should be written");
            AssertBytes(memory, ModuleBase + 0xB7B70, VanillaDistanceBytes,
                "timing apply must not change the independently owned distance-origin block");
            int writes = memory.WriteCount;
            Assert(capability.TryApply(rounded, out _) && memory.WriteCount == writes, "identical apply should be idempotent");
            memory.Set(ModuleBase + 0xB7BC3, 42);
            Assert(!capability.TryApply(rounded, out NativeCapabilityDiagnostic identicalChanged) &&
                identicalChanged.State == NativeCapabilityState.ValidationFailed,
                "idempotent apply must still detect external memory changes");
            memory.Set(ModuleBase + 0xB7BC3, 41);
            Assert(capability.TryApply(new GatehouseTimingSettings(false, double.NaN, double.NaN, double.NaN, double.NaN), out _),
                "disabled settings restore Vanilla without validating unused values");
            Assert(memory.ReadRaw(ModuleBase + 0xB7BC3) == 200 && memory.ReadRaw(ModuleBase + 0xB7C35) == 100,
                "disabled settings restore all Vanilla values");
            AssertBytes(memory, ModuleBase + 0xB7B70, VanillaDistanceBytes,
                "disabled timing must not change the independently owned distance origin");

            memory.Set(ModuleBase + 0xB7BC3, 201);
            Assert(!capability.TryApply(rounded, out NativeCapabilityDiagnostic changed) &&
                changed.State == NativeCapabilityState.ValidationFailed, "external immediate mutation fails closed");
            memory.Set(ModuleBase + 0xB7BC3, 200);
            memory.SetByte(ModuleBase + 0xB7BC0, 0x90);
            Assert(!capability.TryApply(rounded, out changed) && changed.State == NativeCapabilityState.ValidationFailed,
                "external opcode mutation fails closed before writing");

            memory.SetByte(ModuleBase + 0xB7BC0, 0x41);
            memory.SetByte(ModuleBase + 0xB7B70, 0x90);
            Assert(capability.TryApply(rounded, out changed),
                "timing capability ignores mutations outside its owned intervals");
        }

        private static void TestGatehouseRollbackAndPageCleanup()
        {
            FakeMemory memory = SeedDirectGateMemory();
            IGatehouseTimingCapability capability = CreateGateService(memory, new NativeOwnershipRegistry(), new object()).Bind("owner");
            memory.FailNextWriteAddress = ModuleBase + 0xB7BCA;
            Assert(!capability.TryApply(new GatehouseTimingSettings(true, 1, 5, 10, 15), out _), "partial write should fail");
            Assert(memory.ReadRaw(ModuleBase + 0xB7BC3) == 200 && memory.ReadRaw(ModuleBase + 0xB7BCA) == 1200 &&
                memory.ReadRaw(ModuleBase + 0xB7BD3) == 140 && memory.ReadRaw(ModuleBase + 0xB7C35) == 100,
                "partial write should roll back all four values");
            AssertBytes(memory, ModuleBase + 0xB7B70, VanillaDistanceBytes,
                "timing rollback leaves the distance-origin block untouched");
            Assert(memory.WritablePages.Count == 1 && memory.RestoredProtections.Count == 1,
                "the four current RVAs share one 4 KiB page");

            FakeMemory changedDuringAcquire = SeedDirectGateMemory();
            capability = CreateGateService(changedDuringAcquire, new NativeOwnershipRegistry(), new object()).Bind("owner");
            changedDuringAcquire.MutateOnMakeWritableAddress = ModuleBase + 0xB7BC3;
            changedDuringAcquire.MutateOnMakeWritableValue = 202;
            Assert(!capability.TryApply(new GatehouseTimingSettings(true, 1, 5, 10, 15), out _) &&
                changedDuringAcquire.ReadRaw(ModuleBase + 0xB7BC3) == 202 && changedDuringAcquire.WriteCount == 0,
                "a change during protection acquisition fails closed without overwriting or rollback adoption");

            FakeMemory crossPageMemory = SeedCrossPageGateMemory();
            capability = CreateCrossPageGateService(crossPageMemory).Bind("owner");
            Assert(capability.TryApply(new GatehouseTimingSettings(true, 1, 5, 10, 15), out _),
                "generic gatehouse transaction should support targets crossing a page boundary");
            Assert(crossPageMemory.WritablePages.Count == 2 && crossPageMemory.RestoredProtections.Count == 2,
                "cross-page transaction should protect both pages separately");
            Assert(crossPageMemory.RestoredProtections[0].Protection != crossPageMemory.RestoredProtections[1].Protection,
                "each page should restore its own original protection");

            memory = SeedDirectGateMemory();
            capability = CreateGateService(memory, new NativeOwnershipRegistry(), new object()).Bind("owner");
            memory.FailNextWriteAddress = ModuleBase + 0xB7BCA;
            memory.FailRestore = true;
            memory.FailFlush = true;
            Assert(!capability.TryApply(new GatehouseTimingSettings(true, 1, 5, 10, 15), out NativeCapabilityDiagnostic combined) &&
                combined.Reason.Contains("transaction and cleanup"), "write, restore, and flush failures should remain combined");

            memory = SeedDirectGateMemory();
            var registry = new NativeOwnershipRegistry();
            GatehouseTimingService service = CreateGateService(memory, registry, new object());
            Assert(service.Bind("A").TryApply(new GatehouseTimingSettings(true, 1, 5, 10, 15), out _), "first owner applies");
            Assert(!service.Bind("B").TryApply(new GatehouseTimingSettings(true, 2, 6, 11, 16), out NativeCapabilityDiagnostic conflict) &&
                conflict.State == NativeCapabilityState.Conflict && conflict.ConflictOwnerGuid == "A",
                "second owner receives conflict diagnostics");
        }

        private static void TestSelectedBroker()
        {
            var broker = new SelectedUnitCommandBroker("hash", null);
            var order = new List<string>();
            ISelectedUnitCommandRegistration b = broker.Register("B", _ => order.Add("B"));
            ISelectedUnitCommandRegistration a = broker.Register("A", _ => order.Add("A"));
            broker.Register("C", _ => { order.Add("C"); throw new InvalidOperationException("expected"); });
            var context = new SelectedUnitCommandContext(2, TribeAICommand.UnitStop, 4, 5, 6);
            broker.Dispatch(context);
            Assert(string.Join(string.Empty, order) == "ABC", "callbacks use ordinal owner order and continue after errors");
            Assert(ReferenceEquals(b, broker.Register("B", _ => order.Add("replacement"))), "same owner registration is idempotent");
            order.Clear();
            a.Disable();
            broker.Dispatch(context);
            Assert(string.Join(string.Empty, order) == "BC", "disabled registration is omitted");
        }

        private static void TestSelectedEventService()
        {
            var source = new FakeEventSource();
            var service = new SelectedUnitCommandService("hash", source, null);
            ISelectedUnitCommandCapability capabilityA = service.Bind("A");
            Assert(source.SubscribeCount == 0, "selected event subscription is lazy");
            int callsA = 0;
            Assert(capabilityA.TryRegisterBefore(_ => callsA++, out ISelectedUnitCommandRegistration registrationA, out _),
                "first selected callback registration");
            Assert(capabilityA.TryRegisterBefore(_ => callsA += 100, out ISelectedUnitCommandRegistration repeated, out _) &&
                ReferenceEquals(registrationA, repeated), "same selected owner registration is idempotent");
            int callsB = 0;
            ISelectedUnitCommandRegistration registrationB = null;
            Assert(service.Bind("B").TryRegisterBefore(_ => callsB++, out registrationB, out _), "second selected owner registration");
            Assert(source.SubscribeCount == 1, "all selected callbacks share one event subscription");

            var context = new SelectedUnitCommandContext(7, TribeAICommand.UnitStop, 1, 2, 3);
            source.Publish(EventHookPhase.Post, context);
            Assert(callsA == 0 && callsB == 0, "Post events are not exposed as Before callbacks");
            source.Publish(EventHookPhase.Pre, context);
            Assert(callsA == 1 && callsB == 1, "Pre event reaches both registrations");

            registrationB.Disable();
            service.Bind("0").TryRegisterBefore(_ => registrationB.Enable(), out _, out _);
            source.Publish(EventHookPhase.Pre, context);
            Assert(callsB == 1, "reentrant enable affects the next callback snapshot");
            source.Publish(EventHookPhase.Pre, context);
            Assert(callsB == 2, "reentrantly enabled callback runs on the next event");
            registrationA.Dispose();
            Assert(source.SubscribeCount == 1, "disposing registrations preserves the process subscription");

            var failingSource = new FakeEventSource { FailNextSubscribe = true };
            var failingService = new SelectedUnitCommandService("hash", failingSource, null);
            ISelectedUnitCommandCapability retry = failingService.Bind("owner");
            Assert(!retry.TryRegisterBefore(_ => { }, out _, out NativeCapabilityDiagnostic failed) &&
                failed.State == NativeCapabilityState.Faulted, "subscription failure is reported");
            Assert(retry.TryRegisterBefore(_ => { }, out _, out _) && failingSource.SubscribeCount == 2,
                "failed first subscription leaves registration retryable");
        }

        private static SerpNativeApiRuntime InitializeRuntime(
            byte[] image,
            GatehouseBuildTarget catalog,
            FakeMemory memory,
            FakeEventSource events)
        {
            var runtime = new SerpNativeApiRuntime();
            runtime.Initialize(ModuleBase, image, catalog.BuildHash, memory, events, null, catalog);
            return runtime;
        }

        private static void AssertTimingValidationFailure(SerpNativeApiRuntime runtime, string message) =>
            Assert(!runtime.TryGetGatehouseTiming("owner", out _, out NativeCapabilityDiagnostic diagnostic) &&
                diagnostic.State == NativeCapabilityState.ValidationFailed, message);

        private static void AssertOriginValidationFailure(SerpNativeApiRuntime runtime, string message) =>
            Assert(!runtime.TryGetGatehouseDistanceOrigin("owner", out _, out NativeCapabilityDiagnostic diagnostic) &&
                diagnostic.State == NativeCapabilityState.ValidationFailed, message);

        private static void AssertBothGateValidationFailures(SerpNativeApiRuntime runtime, string message)
        {
            AssertTimingValidationFailure(runtime, message + " (timing)");
            AssertOriginValidationFailure(runtime, message + " (origin)");
        }

        private static GatehouseBuildTarget InstallTestGatehouse(byte[] image)
        {
            Copy(image, DistanceRva, VanillaDistanceBytes);
            Copy(image, DecisionRva, DecisionBytes);
            Copy(image, HumanDelayRva, HumanDelayBytes);
            return new GatehouseBuildTarget(
                "TESTHASH", FunctionRva, FunctionSize,
                SerpNativeApiRuntime.ComputeSha256(new ReadOnlySpan<byte>(image, FunctionRva, FunctionSize)),
                DistanceRva, VanillaDistanceBytes, CenteredDistanceBytes,
                DecisionRva, DecisionBytes, HumanDelayRva, HumanDelayBytes,
                DecisionRva + 8, DecisionRva + 15, DecisionRva + 24, HumanDelayRva + 3);
        }

        private static GatehouseBuildTarget CloneCatalog(
            GatehouseBuildTarget source,
            string functionHash = null,
            int? aiCloseDistanceRva = null) =>
            new GatehouseBuildTarget(
                source.BuildHash, source.FunctionRva, source.FunctionSize, functionHash ?? source.FunctionHash,
                source.DistanceBlockRva, source.VanillaDistanceBlockBytes, source.CenteredDistanceBlockBytes,
                source.DecisionBlockRva, source.DecisionBlockBytes, source.HumanDelayBlockRva, source.HumanDelayBlockBytes,
                aiCloseDistanceRva ?? source.AiCloseDistanceRva, source.AiReopenDelayRva,
                source.HumanCloseDistanceRva, source.HumanReopenDelayRva);

        private static FakeMemory SeedRuntimeMemory(byte[] image, GatehouseBuildTarget target)
        {
            var memory = new FakeMemory();
            for (int index = 0; index < target.VanillaDistanceBlockBytes.Length; index++)
                memory.SetByte(ModuleBase + target.DistanceBlockRva + index, image[target.DistanceBlockRva + index]);
            for (int index = 0; index < target.DecisionBlockBytes.Length; index++)
                memory.SetByte(ModuleBase + target.DecisionBlockRva + index, image[target.DecisionBlockRva + index]);
            for (int index = 0; index < target.HumanDelayBlockBytes.Length; index++)
                memory.SetByte(ModuleBase + target.HumanDelayBlockRva + index, image[target.HumanDelayBlockRva + index]);
            memory.Set(ModuleBase + target.AiCloseDistanceRva, 200);
            memory.Set(ModuleBase + target.AiReopenDelayRva, 1200);
            memory.Set(ModuleBase + target.HumanCloseDistanceRva, 140);
            memory.Set(ModuleBase + target.HumanReopenDelayRva, 100);
            return memory;
        }

        private static GatehouseDistanceOriginService CreateOriginService(
            FakeMemory memory,
            NativeOwnershipRegistry ownership,
            object mutationSync)
        {
            var target = new GatehouseDistanceOriginTarget(
                ModuleBase + 0xB7B70,
                VanillaDistanceBytes,
                CenteredDistanceBytes);
            return new GatehouseDistanceOriginService("hash", target, memory, ownership, mutationSync, null);
        }

        private static GatehouseTimingService CreateGateService(
            FakeMemory memory,
            NativeOwnershipRegistry ownership,
            object mutationSync)
        {
            var invariants = new[]
            {
                new NativeByteInvariant(ModuleBase + 0xB7BC0, 0x41),
                new NativeByteInvariant(ModuleBase + 0xB7BC1, 0x81),
                new NativeByteInvariant(ModuleBase + 0xB7BC2, 0xF8),
                new NativeByteInvariant(ModuleBase + 0xB7C34, 0xB8)
            };
            var target = new GatehouseTimingTarget(
                ModuleBase + 0xB7BC3, ModuleBase + 0xB7BCA,
                ModuleBase + 0xB7BD3, ModuleBase + 0xB7C35, invariants);
            return new GatehouseTimingService("hash", target, memory, ownership, mutationSync, null);
        }

        private static FakeMemory SeedDirectGateMemory()
        {
            var memory = new FakeMemory();
            memory.SetByte(ModuleBase + 0xB7BC0, 0x41);
            memory.SetByte(ModuleBase + 0xB7BC1, 0x81);
            memory.SetByte(ModuleBase + 0xB7BC2, 0xF8);
            memory.SetByte(ModuleBase + 0xB7C34, 0xB8);
            for (int index = 0; index < VanillaDistanceBytes.Length; index++)
                memory.SetByte(ModuleBase + 0xB7B70 + index, VanillaDistanceBytes[index]);
            memory.Set(ModuleBase + 0xB7BC3, 200);
            memory.Set(ModuleBase + 0xB7BCA, 1200);
            memory.Set(ModuleBase + 0xB7BD3, 140);
            memory.Set(ModuleBase + 0xB7C35, 100);
            return memory;
        }

        private static GatehouseTimingService CreateCrossPageGateService(FakeMemory memory)
        {
            var target = new GatehouseTimingTarget(
                ModuleBase + 0x1FF0, ModuleBase + 0x1FF4,
                ModuleBase + 0x1FF8, ModuleBase + 0x2004);
            return new GatehouseTimingService("hash", target, memory, new NativeOwnershipRegistry(), new object(), null);
        }

        private static FakeMemory SeedCrossPageGateMemory()
        {
            var memory = new FakeMemory();
            for (int index = 0; index < VanillaDistanceBytes.Length; index++)
                memory.SetByte(ModuleBase + 0x1FE0 + index, VanillaDistanceBytes[index]);
            memory.Set(ModuleBase + 0x1FF0, 200);
            memory.Set(ModuleBase + 0x1FF4, 1200);
            memory.Set(ModuleBase + 0x1FF8, 140);
            memory.Set(ModuleBase + 0x2004, 100);
            return memory;
        }

        private static byte[] CreatePeImage(int size, bool executable)
        {
            var image = new byte[size];
            image[0] = 0x4D; image[1] = 0x5A;
            WriteInt32(image, 0x3C, 0x80);
            WriteInt32(image, 0x80, 0x4550);
            WriteUInt16(image, 0x86, 1);
            WriteUInt16(image, 0x94, 0xF0);
            WriteUInt16(image, 0x98, 0x20B);
            WriteInt32(image, 0x80 + 24 + 56, size);
            int section = 0x80 + 24 + 0xF0;
            WriteInt32(image, section + 8, size - 0x1000);
            WriteInt32(image, section + 12, 0x1000);
            WriteInt32(image, section + 16, size - 0x1000);
            WriteInt32(image, section + 36, unchecked((int)(executable ? 0x60000020 : 0x40000040)));
            return image;
        }

        private static byte[] Hex(string text)
        {
            string[] tokens = text.Split(' ');
            var bytes = new byte[tokens.Length];
            for (int index = 0; index < tokens.Length; index++)
                bytes[index] = Convert.ToByte(tokens[index], 16);
            return bytes;
        }

        private static void Copy(byte[] target, int offset, byte[] source) => Array.Copy(source, 0, target, offset, source.Length);
        private static void AssertBytes(FakeMemory memory, long address, byte[] expected, string message)
        {
            for (int index = 0; index < expected.Length; index++)
                if (memory.ReadByte(address + index) != expected[index])
                {
                    Assert(false, message + $" (mismatch at +0x{index:X})");
                    return;
                }
        }
        private static void AssertSequenceEqual(byte[] actual, byte[] expected, string message)
        {
            if (actual.Length != expected.Length)
            {
                Assert(false, message + $" (length {actual.Length}, expected {expected.Length})");
                return;
            }
            for (int index = 0; index < expected.Length; index++)
                if (actual[index] != expected[index])
                {
                    Assert(false, message + $" (mismatch at +0x{index:X})");
                    return;
                }
        }
        private static int IndexOfSequence(byte[] source, byte[] sequence)
        {
            for (int start = 0; start <= source.Length - sequence.Length; start++)
            {
                int index = 0;
                while (index < sequence.Length && source[start + index] == sequence[index])
                    index++;
                if (index == sequence.Length)
                    return start;
            }
            return -1;
        }
        private static void WriteUInt16(byte[] data, int offset, int value) { data[offset] = (byte)value; data[offset + 1] = (byte)(value >> 8); }
        private static void WriteInt32(byte[] data, int offset, int value)
        {
            data[offset] = (byte)value; data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16); data[offset + 3] = (byte)(value >> 24);
        }

        private static void AssertThrowsState(NativeCapabilityState expected, Action action, string message)
        {
            try { action(); Assert(false, message + " did not throw"); }
            catch (NativeResolutionException ex) { Assert(ex.State == expected, message + $": expected {expected}, got {ex.State}"); }
            catch (Exception ex) { Assert(false, message + $" threw {ex.GetType().Name}"); }
        }

        private static void AssertThrows<T>(Action action, string message) where T : Exception
        {
            try { action(); Assert(false, message + " did not throw"); }
            catch (T) { }
            catch (Exception ex) { Assert(false, message + $" threw {ex.GetType().Name}"); }
        }

        private static void Assert(bool condition, string message)
        {
            if (condition) return;
            failures++;
            Console.Error.WriteLine("FAIL: " + message);
        }

        private sealed class FakeMemory : INativeMemory
        {
            private readonly Dictionary<long, int> values = new Dictionary<long, int>();
            private readonly Dictionary<long, byte> bytes = new Dictionary<long, byte>();
            public int PageSize => 0x1000;
            public long? FailNextWriteAddress { get; set; }
            public long? FailNextWriteByteAddress { get; set; }
            public bool FailRestore { get; set; }
            public bool FailFlush { get; set; }
            public long? MutateOnMakeWritableAddress { get; set; }
            public int MutateOnMakeWritableValue { get; set; }
            public int OperationCount { get; private set; }
            public int WriteCount { get; private set; }
            public List<long> WritablePages { get; } = new List<long>();
            public List<RestoredProtection> RestoredProtections { get; } = new List<RestoredProtection>();
            public void Set(long address, int value) => values[address] = value;
            public void SetByte(long address, byte value) => bytes[address] = value;
            public int ReadRaw(long address) => values[address];
            public byte ReadByte(long address) { OperationCount++; return bytes[address]; }
            public int ReadInt32(long address) { OperationCount++; return values[address]; }
            public void WriteByte(long address, byte value)
            {
                OperationCount++; WriteCount++;
                if (FailNextWriteByteAddress == address) { FailNextWriteByteAddress = null; throw new InvalidOperationException("injected byte write failure"); }
                bytes[address] = value;
            }
            public void WriteInt32(long address, int value)
            {
                OperationCount++; WriteCount++;
                if (FailNextWriteAddress == address) { FailNextWriteAddress = null; throw new InvalidOperationException("injected write failure"); }
                values[address] = value;
            }
            public uint MakeWritable(long address, int length)
            {
                OperationCount++;
                WritablePages.Add(address);
                if (MutateOnMakeWritableAddress.HasValue)
                {
                    values[MutateOnMakeWritableAddress.Value] = MutateOnMakeWritableValue;
                    MutateOnMakeWritableAddress = null;
                }
                return (uint)(0x20 + WritablePages.Count);
            }
            public void RestoreProtection(long address, int length, uint protection)
            {
                OperationCount++;
                RestoredProtections.Add(new RestoredProtection(address, protection));
                if (FailRestore) { FailRestore = false; throw new InvalidOperationException("injected restore failure"); }
            }
            public void Flush(long address, int length)
            {
                OperationCount++;
                if (FailFlush) { FailFlush = false; throw new InvalidOperationException("injected flush failure"); }
            }
        }

        private readonly struct RestoredProtection
        {
            public RestoredProtection(long address, uint protection) { Address = address; Protection = protection; }
            public long Address { get; }
            public uint Protection { get; }
        }

        private sealed class FakeEventSource : ISelectedUnitCommandEventSource
        {
            private Action<SelectedUnitCommandEventData> callback;
            public int SubscribeCount { get; private set; }
            public bool FailNextSubscribe { get; set; }
            public IDisposable Subscribe(Action<SelectedUnitCommandEventData> handler)
            {
                SubscribeCount++;
                if (FailNextSubscribe)
                {
                    FailNextSubscribe = false;
                    throw new InvalidOperationException("injected subscription failure");
                }
                callback = handler;
                return new FakeSubscription();
            }
            public void Publish(EventHookPhase phase, SelectedUnitCommandContext context) =>
                callback?.Invoke(new SelectedUnitCommandEventData(phase, context));
            private sealed class FakeSubscription : IDisposable { public void Dispose() { } }
        }
    }
}
