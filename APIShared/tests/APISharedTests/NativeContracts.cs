using APIShared.ModSettings;
using APIShared;
using CrusaderDE;
using Iced.Intel;
using APIShared.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace APISharedTests
{
    public partial class RuntimeTests
    {
        [TestMethod]
        [TestCategory("NativeContracts")]
        public void VerifyPeValidation() => TestPeValidation();

        private static void TestPeValidation()
        {
            byte[] image = CreatePeImage(0x4000, true);
            NativePeImage pe = NativePeImage.Parse(image);
            Assert(pe.ImageSize == image.Length, "PE image size should parse");
            AssertThrowsState(NativeCapabilityState.ValidationFailed,
                () => pe.RequireExecutableRange(0x200, 1, "header"), "headers are not executable targets");

            byte[] nonExecutable = CreatePeImage(0x4000, false);
            GatehouseBuildTarget catalog = InstallTestGatehouse(nonExecutable);
            var runtime = InitializeRuntime(nonExecutable, catalog, SeedRuntimeMemory(nonExecutable, catalog));
            Assert(!runtime.TryGetGatehouseTiming("owner", out _, out NativeCapabilityDiagnostic diagnostic) &&
                diagnostic.State == NativeCapabilityState.ValidationFailed, "gatehouse function must be executable");
            Assert(!runtime.TryGetGatehouseDistanceOrigin("owner", out _, out diagnostic) &&
                diagnostic.State == NativeCapabilityState.ValidationFailed, "distance origin also requires an executable gatehouse function");
        }
        [TestMethod]
        [TestCategory("NativeContracts")]
        public void VerifyFixedCatalogValidation() => TestFixedCatalogValidation();

        private static void TestFixedCatalogValidation()
        {
            byte[] image = CreatePeImage(0x4000, true);
            GatehouseBuildTarget catalog = InstallTestGatehouse(image);
            FakeMemory memory = SeedRuntimeMemory(image, catalog);
            ApiSharedRuntime runtime = InitializeRuntime(image, catalog, memory);
            Assert(runtime.TryGetGatehouseTiming("owner", out _, out NativeCapabilityDiagnostic available) &&
                available.State == NativeCapabilityState.Available && available.Reason.Contains("function SHA-256"),
                "matching fixed catalog should validate with provenance");
            Assert(runtime.TryGetGatehouseDistanceOrigin("origin-owner", out _, out NativeCapabilityDiagnostic originAvailable) &&
                originAvailable.State == NativeCapabilityState.Available && originAvailable.Reason.Contains("function SHA-256"),
                "matching distance-origin catalog should validate with provenance");
            Assert(memory.WriteCount == 0,
                "initialization and capability acquisition must not activate either gatehouse gameplay change");

            FakeMemory preHookedMemory = SeedRuntimeMemory(image, catalog);
            preHookedMemory.SetByte(ModuleBase + catalog.HumanReopenDelayRva + 4, 0xFF);
            runtime = InitializeRuntime(image, catalog, preHookedMemory);
            Assert(runtime.TryGetGatehouseTiming("owner", out var coexistTiming, out _),
                "Fixes Farmer hook after the seven-byte human delay block must coexist before initialization");
            preHookedMemory.SetByte(ModuleBase + catalog.HumanDelayBlockRva + 7, 0xEE);
            Assert(runtime.TryGetGatehouseTiming("owner", out _, out _),
                "Fixes Farmer hook installed after initialization must remain independent");
            var invariants = (IReadOnlyList<NativeByteInvariant>)typeof(GatehouseCapabilityResolver)
                .GetMethod("CreateInstructionInvariants", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { ModuleBase, catalog });
            Assert(!invariants.Any(item => item.Address >= ModuleBase + catalog.HumanDelayBlockRva + 7 &&
                item.Address < ModuleBase + catalog.HumanDelayBlockRva + catalog.HumanDelayBlockBytes.Length),
                "timing invariants exclude the external Farmer block");
            preHookedMemory.SetByte(ModuleBase + catalog.HumanDelayBlockRva, 0x90);
            runtime = InitializeRuntime(image, catalog, preHookedMemory);
            AssertTimingValidationFailure(runtime,"changes inside our owned human block remain fail-closed");
            Assert(runtime.TryGetGatehouseDistanceOrigin("owner", out _, out _),
                "a timing-only live layout mismatch must not disable distance origin");

            byte[] wrongHashImage = (byte[])image.Clone();
            var wrongHashCatalog = CloneCatalog(catalog, functionHash: new string('0', 64));
            runtime = InitializeRuntime(wrongHashImage, wrongHashCatalog, SeedRuntimeMemory(wrongHashImage, wrongHashCatalog));
            AssertBothGateValidationFailures(runtime, "wrong function hash must fail both gatehouse capabilities");

            byte[] wrongOpcode = (byte[])image.Clone();
            wrongOpcode[DecisionRva] ^= 1;
            Copy(wrongOpcode, 0x1500, DecisionBytes); // A decoy must never be used as a fallback.
            GatehouseBuildTarget wrongOpcodeCatalog = CloneCatalog(catalog, functionHash: ApiSharedRuntime.ComputeSha256(
                new ReadOnlySpan<byte>(wrongOpcode, FunctionRva, FunctionSize)));
            runtime = InitializeRuntime(wrongOpcode, wrongOpcodeCatalog, SeedRuntimeMemory(wrongOpcode, wrongOpcodeCatalog));
            AssertTimingValidationFailure(runtime, "wrong timing opcode must fail without accepting a decoy");
            Assert(runtime.TryGetGatehouseDistanceOrigin("owner", out _, out _),
                "a timing-only opcode mismatch must not disable distance-origin capability");

            byte[] wrongImmediate = (byte[])image.Clone();
            WriteInt32(wrongImmediate, DecisionRva + 8, 201);
            GatehouseBuildTarget wrongImmediateCatalog = CloneCatalog(catalog, functionHash: ApiSharedRuntime.ComputeSha256(
                new ReadOnlySpan<byte>(wrongImmediate, FunctionRva, FunctionSize)));
            runtime = InitializeRuntime(wrongImmediate, wrongImmediateCatalog, SeedRuntimeMemory(wrongImmediate, wrongImmediateCatalog));
            AssertTimingValidationFailure(runtime, "wrong Vanilla immediate must fail timing");
            Assert(runtime.TryGetGatehouseDistanceOrigin("owner", out _, out _),
                "a timing immediate mismatch must not disable distance-origin capability");

            byte[] wrongDistance = (byte[])image.Clone();
            wrongDistance[DistanceRva] ^= 1;
            GatehouseBuildTarget wrongDistanceCatalog = CloneCatalog(catalog, functionHash: ApiSharedRuntime.ComputeSha256(
                new ReadOnlySpan<byte>(wrongDistance, FunctionRva, FunctionSize)));
            runtime = InitializeRuntime(wrongDistance, wrongDistanceCatalog, SeedRuntimeMemory(wrongDistance, wrongDistanceCatalog));
            AssertOriginValidationFailure(runtime, "wrong Vanilla distance block must fail distance origin");
            Assert(runtime.TryGetGatehouseTiming("owner", out _, out _),
                "a distance-origin mismatch must not disable gatehouse timing");

            GatehouseBuildTarget outside = CloneCatalog(catalog, aiCloseDistanceRva: FunctionRva - 4);
            runtime = InitializeRuntime(image, outside, SeedRuntimeMemory(image, catalog));
            AssertTimingValidationFailure(runtime, "catalogued immediate outside function must fail timing");
            Assert(runtime.TryGetGatehouseDistanceOrigin("owner", out _, out _),
                "an invalid timing address must not disable distance origin");
        }
        [TestMethod]
        [TestCategory("NativeContracts")]
        public void VerifyCenteredDistanceSemantics() => TestCenteredDistanceSemantics();

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
        [TestMethod]
        [TestCategory("NativeContracts")]
        public void VerifyGatehouseDistanceOriginTransaction() => TestGatehouseDistanceOriginTransaction();

        private static void TestGatehouseDistanceOriginTransaction()
        {
            var ownership = new NativeOwnershipRegistry();
            var mutationSync = new object();
            GatehousePermanentRuntimeState state = GatehousePermanentRuntimeState.CreateTestState();
            IGatehouseDistanceOriginCapability origin =
                CreateOriginService(state, ownership, mutationSync).Bind("BugfixesAndQoL_Serp");
            IGatehouseTimingCapability timing =
                CreateGateService(state, ownership, mutationSync).Bind("ExtraFeatures_Serp");

            Assert(!origin.TryApply((GatehouseDistanceOrigin)99, out NativeCapabilityDiagnostic invalid) &&
                invalid.State == NativeCapabilityState.ValidationFailed,
                "unknown distance-origin values fail before logical publication");
            Assert(origin.TryApply(GatehouseDistanceOrigin.BuildingBoundsCenter, out NativeCapabilityDiagnostic centered) &&
                centered.CapabilityId == NativeCapabilityIds.GatehouseDistanceOrigin &&
                state.Origin == GatehouseDistanceOrigin.BuildingBoundsCenter,
                "Bugfixes owner should apply the centered distance origin");
            state.ReadTiming(out int aiDistance, out int aiDelay, out int humanDistance, out int humanDelay);
            Assert(aiDistance == 200 && aiDelay == 1200 && humanDistance == 140 && humanDelay == 100,
                "distance-origin publication leaves all timing values Vanilla");
            Assert(origin.TryApply(GatehouseDistanceOrigin.BuildingBoundsCenter, out _) &&
                state.Origin == GatehouseDistanceOrigin.BuildingBoundsCenter,
                "identical distance-origin apply is idempotent");

            Assert(timing.TryApply(new GatehouseTimingSettings(true, 1, 5, 10, 15), out NativeCapabilityDiagnostic timingApplied) &&
                timingApplied.CapabilityId == NativeCapabilityIds.GatehouseTiming,
                "different owners can reserve the adjacent timing and origin intervals");
            Assert(state.Origin == GatehouseDistanceOrigin.BuildingBoundsCenter,
                "timing publication leaves the independently selected origin unchanged");

            Assert(origin.TryApply(GatehouseDistanceOrigin.VanillaBuildingBegin, out _),
                "distance-origin capability restores Vanilla on explicit request");
            Assert(state.Origin == GatehouseDistanceOrigin.VanillaBuildingBegin,
                "Vanilla distance-origin request changes only the logical gate");
            state.ReadTiming(out aiDistance, out aiDelay, out humanDistance, out humanDelay);
            Assert(aiDistance == 120 && aiDelay == 200 && humanDistance == 80 && humanDelay == 40,
                "restoring the origin does not change customized timing values");

            var conflictRegistry = new NativeOwnershipRegistry();
            GatehouseDistanceOriginService originService =
                CreateOriginService(GatehousePermanentRuntimeState.CreateTestState(), conflictRegistry, new object());
            Assert(originService.Bind("A").TryApply(GatehouseDistanceOrigin.BuildingBoundsCenter, out _),
                "first distance-origin owner applies");
            Assert(!originService.Bind("B").TryApply(GatehouseDistanceOrigin.VanillaBuildingBegin, out NativeCapabilityDiagnostic conflict) &&
                conflict.State == NativeCapabilityState.Conflict && conflict.ConflictOwnerGuid == "A",
                "second distance-origin owner receives conflict diagnostics");
        }
        [TestMethod]
        [TestCategory("NativeContracts")]
        public void VerifyGatehouseTransactionAndRounding() => TestGatehouseTransactionAndRounding();

        private static void TestGatehouseTransactionAndRounding()
        {
            GatehousePermanentRuntimeState state = GatehousePermanentRuntimeState.CreateTestState();
            IGatehouseTimingCapability capability = CreateGateService(
                state, new NativeOwnershipRegistry(), new object()).Bind("owner");
            Assert(!capability.TryApply(new GatehouseTimingSettings(true, double.NaN, 0, 5, 5), out NativeCapabilityDiagnostic invalid) &&
                invalid.State == NativeCapabilityState.ValidationFailed, "non-finite gatehouse input should fail");
            AssertThrows<ArgumentOutOfRangeException>(
                () => GatehouseTimingService.ConvertNativeUInt16(8192, 8, "value"), "native UInt16 overflow should fail");

            var rounded = new GatehouseTimingSettings(true, 0.0125, 0.0125, 5.0625, 5.0625);
            Assert(capability.TryApply(rounded, out NativeCapabilityDiagnostic applied) &&
                applied.Reason.Contains("41units") && applied.Reason.Contains("1ticks"),
                "AwayFromZero values and verified native units should be diagnosed");
            state.ReadTiming(out int aiDistance, out int aiDelay, out int humanDistance, out int humanDelay);
            Assert(aiDistance == 41 && aiDelay == 1 && humanDistance == 41 && humanDelay == 1,
                "all four rounded values should be atomically published");
            Assert(capability.TryApply(rounded, out _), "identical apply should be idempotent");
            Assert(capability.TryApply(new GatehouseTimingSettings(false, double.NaN, double.NaN, double.NaN, double.NaN), out _),
                "disabled settings restore Vanilla without validating unused values");
            state.ReadTiming(out aiDistance, out aiDelay, out humanDistance, out humanDelay);
            Assert(aiDistance == 200 && aiDelay == 1200 && humanDistance == 140 && humanDelay == 100,
                "disabled settings atomically publish all Vanilla values");
        }
        [TestMethod]
        [TestCategory("NativeContracts")]
        public void VerifyGatehouseRollbackAndPageCleanup() => TestGatehouseRollbackAndPageCleanup();

        private static void TestGatehouseRollbackAndPageCleanup()
        {
            var registry = new NativeOwnershipRegistry();
            GatehousePermanentRuntimeState state = GatehousePermanentRuntimeState.CreateTestState();
            GatehouseTimingService service = CreateGateService(state, registry, new object());
            Assert(service.Bind("A").TryApply(new GatehouseTimingSettings(true, 1, 5, 10, 15), out _), "first owner applies");
            Assert(!service.Bind("B").TryApply(new GatehouseTimingSettings(true, 2, 6, 11, 16), out NativeCapabilityDiagnostic conflict) &&
                conflict.State == NativeCapabilityState.Conflict && conflict.ConflictOwnerGuid == "A",
                "second owner receives conflict diagnostics");
            state.ReadTiming(out int aiDistance, out int aiDelay, out int humanDistance, out int humanDelay);
            Assert(aiDistance == 120 && aiDelay == 200 && humanDistance == 80 && humanDelay == 40,
                "conflicting owner cannot alter the atomically published timing snapshot");
        }
        [TestMethod]
        [TestCategory("NativeContracts")]
        public void VerifyGatehouseConcurrentPublication() => TestGatehouseConcurrentPublication();

        private static void TestGatehouseConcurrentPublication()
        {
            GatehousePermanentRuntimeState state = GatehousePermanentRuntimeState.CreateTestState();
            Exception writerFailure = null;
            var writer = new Thread(() =>
            {
                try
                {
                    for (int index = 0; index < 10000; index++)
                    {
                        if ((index & 1) == 0)
                            state.PublishTiming(11, 22, 33, 44);
                        else
                            state.PublishTiming(101, 202, 303, 404);
                    }
                }
                catch (Exception ex)
                {
                    writerFailure = ex;
                }
            });
            writer.Start();
            for (int index = 0; index < 10000; index++)
            {
                state.ReadTiming(out int aiDistance, out int aiDelay, out int humanDistance, out int humanDelay);
                bool vanilla = aiDistance == 200 && aiDelay == 1200 && humanDistance == 140 && humanDelay == 100;
                bool first = aiDistance == 11 && aiDelay == 22 && humanDistance == 33 && humanDelay == 44;
                bool second = aiDistance == 101 && aiDelay == 202 && humanDistance == 303 && humanDelay == 404;
                if (!vanilla && !first && !second)
                {
                    Assert(false, "parallel gatehouse readers must observe one complete immutable timing snapshot");
                    break;
                }
            }
            writer.Join();
            Assert(writerFailure == null, "parallel gatehouse publication must not fail");
        }
        [TestMethod]
        [TestCategory("NativeContracts")]
        public void VerifyGatehouseAssemblerContracts() => TestGatehouseAssemblerContracts();

        private static void TestGatehouseAssemblerContracts()
        {
            GateBridgeAutomationTests.Run(Assert, MapPeImage(File.ReadAllBytes(Path.Combine(
                GetGameDirectory(),
                "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll"))));
            const ulong moduleBase = 0x00007FF940F90000;
            const ulong stubAddress = 0x00007FF8D1144000;
            Instruction[] displaced = DecodeInstructions(
                GatehouseBuildTarget.Supported.VanillaDistanceBlockBytes,
                moduleBase + GatehousePermanentRuntimeState.DistanceHookRva);
            var distanceAssembler = new Assembler(64);
            GatehousePermanentRuntimeState.GenerateDistanceOrigin(
                distanceAssembler,
                displaced,
                0x000001A000001000);
            byte[] distanceStub = AssembleAndDecode(distanceAssembler, stubAddress);
            Assert(distanceStub.Length > displaced.Length,
                "gatehouse distance generator assembles its logical gate and relocated Vanilla fallback");

            var decisionAssembler = new Assembler(64);
            GatehousePermanentRuntimeState.GenerateDecision(
                decisionAssembler,
                new[] { Instruction.Create(Code.Nopd) },
                0x000001A000002000,
                moduleBase,
                moduleBase + GatehousePermanentRuntimeState.DecisionReturnRva,
                moduleBase + GatehousePermanentRuntimeState.ClosePathRva);
            byte[] decisionStub = AssembleAndDecode(decisionAssembler, stubAddress + 0x1000);
            Assert(decisionStub.Length > 0,
                "gatehouse timing generator assembles with one label per emitted instruction");
        }

    }
}
