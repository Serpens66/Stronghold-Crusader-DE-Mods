using SerpNativeAPI;
using System;
using System.Collections.Generic;

namespace SerpNativeAPITests
{
    internal static class Program
    {
        private const int GateDecisionRva = 0xB7BBB;
        private const int GateHumanRva = 0xB7C32;
        private const int SelectedRva = 0x199C70;
        private const int FullImageSize = 0x7CC8000;
        private const long ModuleBase = 0x10000000;
        private static readonly byte[] GateDecision = PatternBytes(
            "40 84 F6 75 10 41 81 F8 C8 00 00 00 7D 10 B8 B0 04 00 00 EB 69 " +
            "41 81 F8 8C 00 00 00 7C 5B 48 8D 2D 00 00 00 00 49 FF C6 49 83 C3 02");
        private static readonly byte[] GateHuman = PatternBytes(
            "EB 50 B8 64 00 00 00 48 8D 2D 00 00 00 00 66 89 84 2B 00 00 00 00 " +
            "80 BC 2B 00 00 00 00 00");
        private static readonly byte[] Selected = PatternBytes("48 8D 0D A9 CA B2 07 E9 E4 4C F8 FF");
        private static int failures;

        private static int Main()
        {
            TestPeAndPatternResolution();
            TestPatternMissingAndAmbiguous();
            TestRelativeTargetValidation();
            TestOwnership();
            TestReadinessAndUnknownBuild();
            TestIndependentCapabilities();
            TestKnownBuildCapabilities();
            TestGatehouseTransaction();
            TestGatehouseRollbackAndCleanup();
            TestSelectedBroker();
            TestSelectedServiceInstallsOnce();
            if (failures == 0)
            {
                Console.WriteLine("PASS: SerpNativeAPI focused tests passed.");
                return 0;
            }
            Console.Error.WriteLine($"FAIL: SerpNativeAPI tests reported {failures} failure(s).");
            return 1;
        }

        private static void TestPeAndPatternResolution()
        {
            byte[] image = CreatePeImage(0x3000);
            Copy(image, 0x1200, new byte[] { 0xAA, 0xBB, 0xCC });
            NativePeImage pe = NativePeImage.Parse(image);
            Assert(pe.ImageSize == image.Length, "PE image size should parse");
            AssertThrowsState(NativeCapabilityState.ValidationFailed,
                () => pe.RequireExecutableRange(0x200, 1, "header target"),
                "target outside executable sections");
            Assert(NativePattern.ResolveKnownBuild(image, pe, "AA BB CC", 0x1200, "test", true) == 0x1200,
                "known RVA should resolve");
            Copy(image, 0x1200, new byte[] { 0, 0, 0 });
            Copy(image, 0x1400, new byte[] { 0xAA, 0xBB, 0xCC });
            Assert(NativePattern.ResolveKnownBuild(image, pe, "AA BB CC", 0x1200, "test", true) == 0x1400,
                "unique known-build fallback should resolve");
        }

        private static void TestPatternMissingAndAmbiguous()
        {
            byte[] image = CreatePeImage(0x3000);
            NativePeImage pe = NativePeImage.Parse(image);
            AssertThrowsState(NativeCapabilityState.PatternMissing,
                () => NativePattern.ResolveKnownBuild(image, pe, "AA BB", 0x1200, "missing", true),
                "missing pattern state");
            Copy(image, 0x1300, new byte[] { 0xAA, 0xBB });
            Copy(image, 0x1400, new byte[] { 0xAA, 0xBB });
            AssertThrowsState(NativeCapabilityState.Ambiguous,
                () => NativePattern.ResolveKnownBuild(image, pe, "AA BB", 0x1200, "ambiguous", true),
                "ambiguous pattern state");
        }

        private static void TestRelativeTargetValidation()
        {
            byte[] image = CreatePeImage(0x3000);
            NativePeImage pe = NativePeImage.Parse(image);
            WriteInt32(image, 0x1100, 0x20);
            Assert(NativePattern.ResolveRelativeTarget(image, pe, 0x1100, 0x1104, "relative") == 0x1124,
                "valid relative target");
            WriteInt32(image, 0x1100, int.MaxValue);
            AssertThrowsState(NativeCapabilityState.ValidationFailed,
                () => NativePattern.ResolveRelativeTarget(image, pe, 0x1100, 0x1104, "relative"),
                "relative target outside image");
        }

        private static void TestOwnership()
        {
            var registry = new NativeOwnershipRegistry();
            var first = new[] { new NativeInterval(100, 110) };
            Assert(registry.TryReserve("A", "cap", NativeReservationMode.Exclusive, first, out _), "first reservation");
            Assert(registry.TryReserve("A", "cap", NativeReservationMode.Exclusive, first, out _), "same reservation idempotent");
            Assert(registry.TryReserve("B", "other", NativeReservationMode.Exclusive, new[] { new NativeInterval(110, 120) }, out _),
                "adjacent interval should not overlap");
            Assert(!registry.TryReserve("C", "third", NativeReservationMode.Exclusive, new[] { new NativeInterval(109, 111) }, out string conflict) && conflict == "A",
                "overlapping exclusive reservation should identify owner");
            var hooks = new NativeOwnershipRegistry();
            Assert(hooks.TryReserve("B", "hook", NativeReservationMode.SharedHook, first, out _), "first hook reservation");
            Assert(hooks.TryReserve("A", "hook", NativeReservationMode.SharedHook, first, out _), "shared hook reservation");
        }

        private static void TestReadinessAndUnknownBuild()
        {
            var runtime = new SerpNativeApiRuntime();
            Assert(!runtime.TryGetGatehouseTiming("test", out _, out NativeCapabilityDiagnostic pending) &&
                pending.State == NativeCapabilityState.Pending, "query before initialization should remain Pending");
            int before = 0;
            runtime.WhenReady(_ => before++);
            var memory = new FakeMemory();
            var hooks = new FakeHookFactory();
            byte[] image = CreatePeImage(0x3000);
            runtime.Initialize(ModuleBase, image, "UNKNOWN", memory, hooks, null);
            Assert(before == 1 && runtime.State == NativeApiState.Ready, "pre-init readiness callback");
            int after = 0;
            runtime.WhenReady(_ => after++);
            Assert(after == 1, "post-init readiness callback should be synchronous");
            Assert(!runtime.TryGetGatehouseTiming("test", out _, out NativeCapabilityDiagnostic gate) && gate.State == NativeCapabilityState.UnsupportedBuild,
                "unknown gatehouse build");
            Assert(!runtime.TryGetSelectedUnitCommand("test", out _, out NativeCapabilityDiagnostic selected) && selected.State == NativeCapabilityState.UnsupportedBuild,
                "unknown selected-command build");
            Assert(memory.OperationCount == 0 && hooks.InstallCount == 0, "unknown build must not mutate or hook");
            Assert(!runtime.TryGetGatehouseTiming("", out _, out NativeCapabilityDiagnostic invalid) && invalid.State == NativeCapabilityState.ValidationFailed,
                "empty owner should fail validation");

            var missingHash = new SerpNativeApiRuntime();
            missingHash.Initialize(ModuleBase, image, string.Empty, new FakeMemory(), new FakeHookFactory(), null);
            Assert(missingHash.State == NativeApiState.Unavailable, "missing module hash should make the API unavailable");
        }

        private static void TestIndependentCapabilities()
        {
            byte[] image = CreatePeImage(0x200000);
            InstallGatePatterns(image);
            Copy(image, SelectedRva, Selected);
            var memory = SeedGateMemory();
            var runtime = new SerpNativeApiRuntime();
            runtime.Initialize(ModuleBase, image, SerpNativeApiRuntime.SupportedHash, memory, new FakeHookFactory(), null);
            Assert(runtime.TryGetGatehouseTiming("test", out _, out NativeCapabilityDiagnostic gate) && gate.State == NativeCapabilityState.Available,
                "gatehouse should survive selected-target failure");
            Assert(!runtime.TryGetSelectedUnitCommand("test", out _, out NativeCapabilityDiagnostic selected) && selected.State == NativeCapabilityState.ValidationFailed,
                "selected target outside small image should fail independently");
        }

        private static void TestKnownBuildCapabilities()
        {
            byte[] image = CreatePeImage(FullImageSize);
            InstallGatePatterns(image);
            Copy(image, SelectedRva, Selected);
            var memory = SeedGateMemory();
            var hooks = new FakeHookFactory();
            var runtime = new SerpNativeApiRuntime();
            runtime.Initialize(ModuleBase, image, SerpNativeApiRuntime.SupportedHash, memory, hooks, null);
            Assert(runtime.State == NativeApiState.Ready, "known build reaches Ready");
            Assert(runtime.TryGetGatehouseTiming("test", out _, out NativeCapabilityDiagnostic gate) && gate.State == NativeCapabilityState.Available,
                "known build gatehouse target validates");
            Assert(runtime.TryGetSelectedUnitCommand("test", out ISelectedUnitCommandCapability selected, out NativeCapabilityDiagnostic selectedDiagnostic) &&
                selectedDiagnostic.State == NativeCapabilityState.Available, "known build selected target validates");
            Assert(hooks.InstallCount == 0, "known selected target remains lazy after resolution");
            Assert(selected.TryRegisterBefore(_ => { }, out _, out _) && hooks.InstallCount == 1,
                "known selected target installs through broker");
        }

        private static void TestGatehouseTransaction()
        {
            FakeMemory memory = SeedGateMemory();
            GatehouseTimingService service = CreateGateService(memory, new NativeOwnershipRegistry());
            IGatehouseTimingCapability capability = service.Bind("owner");
            Assert(!capability.TryApply(new GatehouseTimingSettings(true, double.NaN, 0, 5, 5), out NativeCapabilityDiagnostic invalid) &&
                invalid.State == NativeCapabilityState.ValidationFailed, "active non-finite gatehouse value should fail");
            Assert(!capability.TryApply(new GatehouseTimingSettings(true, 31, 0, 5, 5), out invalid) &&
                invalid.State == NativeCapabilityState.ValidationFailed, "active out-of-range gatehouse value should fail");
            var active = new GatehouseTimingSettings(true, 0, 0, 5, 5);
            Assert(capability.TryApply(active, out NativeCapabilityDiagnostic applied) && applied.State == NativeCapabilityState.Available,
                "gatehouse active values apply");
            Assert(memory.ReadRaw(ModuleBase + GateDecisionRva + 8) == 40 &&
                memory.ReadRaw(ModuleBase + GateDecisionRva + 15) == 0 &&
                memory.ReadRaw(ModuleBase + GateDecisionRva + 24) == 40 &&
                memory.ReadRaw(ModuleBase + GateHumanRva + 3) == 0,
                "all four converted values written");
            int writes = memory.WriteCount;
            Assert(capability.TryApply(active, out _ ) && memory.WriteCount == writes, "identical gatehouse apply is idempotent");
            Assert(capability.TryApply(new GatehouseTimingSettings(false, double.NaN, double.NaN, double.NaN, double.NaN), out _),
                "disabled settings restore Vanilla without validating unused fields");
            Assert(memory.ReadRaw(ModuleBase + GateDecisionRva + 8) == 200 && memory.ReadRaw(ModuleBase + GateHumanRva + 3) == 100,
                "gatehouse disable restores Vanilla");
            memory.Set(ModuleBase + GateDecisionRva + 8, 201);
            Assert(!capability.TryApply(active, out NativeCapabilityDiagnostic changed) && changed.State == NativeCapabilityState.ValidationFailed,
                "external change must fail closed");
        }

        private static void TestGatehouseRollbackAndCleanup()
        {
            FakeMemory memory = SeedGateMemory();
            IGatehouseTimingCapability capability = CreateGateService(memory, new NativeOwnershipRegistry()).Bind("owner");
            memory.FailNextWriteAddress = ModuleBase + GateDecisionRva + 15;
            Assert(!capability.TryApply(new GatehouseTimingSettings(true, 1, 5, 10, 15), out _), "partial write should fail");
            Assert(memory.ReadRaw(ModuleBase + GateDecisionRva + 8) == 200 &&
                memory.ReadRaw(ModuleBase + GateDecisionRva + 15) == 1200 &&
                memory.ReadRaw(ModuleBase + GateDecisionRva + 24) == 140 &&
                memory.ReadRaw(ModuleBase + GateHumanRva + 3) == 100,
                "partial write should roll back all values");

            memory = SeedGateMemory();
            capability = CreateGateService(memory, new NativeOwnershipRegistry()).Bind("owner");
            memory.FailNextWriteAddress = ModuleBase + GateDecisionRva + 15;
            memory.FailRestore = true;
            Assert(!capability.TryApply(new GatehouseTimingSettings(true, 1, 5, 10, 15), out NativeCapabilityDiagnostic combined) &&
                combined.Reason.Contains("transaction and cleanup"), "write and cleanup failures should remain combined");

            memory = SeedGateMemory();
            var registry = new NativeOwnershipRegistry();
            GatehouseTimingService sharedService = CreateGateService(memory, registry);
            capability = sharedService.Bind("ownerA");
            Assert(capability.TryApply(new GatehouseTimingSettings(true, 1, 5, 10, 15), out _), "owner A applies");
            IGatehouseTimingCapability other = sharedService.Bind("ownerB");
            Assert(!other.TryApply(new GatehouseTimingSettings(true, 2, 6, 11, 16), out NativeCapabilityDiagnostic conflict) &&
                conflict.State == NativeCapabilityState.Conflict && conflict.ConflictOwnerGuid == "ownerA", "gatehouse ownership conflict");
        }

        private static void TestSelectedBroker()
        {
            var broker = new SelectedUnitCommandBroker("hash", null);
            var order = new List<string>();
            ISelectedUnitCommandRegistration b = broker.Register("B", _ => order.Add("B"));
            ISelectedUnitCommandRegistration a = broker.Register("A", _ => order.Add("A"));
            broker.Register("C", _ => { order.Add("C"); throw new InvalidOperationException("expected"); });
            int originals = 0;
            int result = broker.Dispatch(new NativeSelectedUnitCommandArguments(IntPtr.Zero, 2, 3, 4, 5, 6), _ => { originals++; return 77; });
            Assert(string.Join(string.Empty, order) == "ABC", "callbacks should use ordinal owner order");
            Assert(originals == 1 && result == 77, "throwing callback must not prevent exact Vanilla result");
            Assert(ReferenceEquals(b, broker.Register("B", _ => order.Add("replacement"))), "same owner registration is idempotent");
            order.Clear();
            a.Disable();
            broker.Dispatch(new NativeSelectedUnitCommandArguments(), _ => 0);
            Assert(string.Join(string.Empty, order) == "BC", "disabled registration omitted");

            var reentrant = new SelectedUnitCommandBroker("hash", null);
            ISelectedUnitCommandRegistration self = null;
            self = reentrant.Register("A", _ => self.Disable());
            int calls = 0;
            reentrant.Register("B", _ => calls++);
            reentrant.Dispatch(new NativeSelectedUnitCommandArguments(), _ => 0);
            reentrant.Dispatch(new NativeSelectedUnitCommandArguments(), _ => 0);
            Assert(calls == 2 && !self.IsEnabled, "reentrant disable applies on the next snapshot");
        }

        private static void TestSelectedServiceInstallsOnce()
        {
            var factory = new FakeHookFactory();
            var service = new SelectedUnitCommandService("hash", 1000, 12, new NativeOwnershipRegistry(), factory, null);
            ISelectedUnitCommandCapability a = service.Bind("A");
            Assert(factory.InstallCount == 0, "selected detour is lazy");
            Assert(a.TryRegisterBefore(_ => { }, out ISelectedUnitCommandRegistration first, out _), "first selected callback registration");
            Assert(a.TryRegisterBefore(_ => { }, out ISelectedUnitCommandRegistration repeated, out _) && ReferenceEquals(first, repeated),
                "same selected owner registration idempotent");
            Assert(service.Bind("B").TryRegisterBefore(_ => { }, out _, out _), "second selected owner registration");
            Assert(factory.InstallCount == 1, "selected target detoured exactly once");
            first.Dispose();
            Assert(factory.InstallCount == 1, "disposing registration preserves detour");
            Assert(a.TryRegisterBefore(_ => { }, out ISelectedUnitCommandRegistration recreated, out _) &&
                !ReferenceEquals(first, recreated), "disposed owner registration can be recreated");
        }

        private static GatehouseTimingService CreateGateService(FakeMemory memory, NativeOwnershipRegistry ownership)
        {
            var target = new GatehouseTimingTarget(
                ModuleBase + GateDecisionRva + 8,
                ModuleBase + GateDecisionRva + 15,
                ModuleBase + GateDecisionRva + 24,
                ModuleBase + GateHumanRva + 3);
            return new GatehouseTimingService("hash", target, memory, ownership, null);
        }

        private static FakeMemory SeedGateMemory()
        {
            var memory = new FakeMemory();
            memory.Set(ModuleBase + GateDecisionRva + 8, 200);
            memory.Set(ModuleBase + GateDecisionRva + 15, 1200);
            memory.Set(ModuleBase + GateDecisionRva + 24, 140);
            memory.Set(ModuleBase + GateHumanRva + 3, 100);
            return memory;
        }

        private static byte[] CreatePeImage(int size)
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
            WriteInt32(image, section + 36, unchecked((int)0x60000020));
            return image;
        }

        private static void InstallGatePatterns(byte[] image)
        {
            Copy(image, GateDecisionRva, GateDecision);
            Copy(image, GateHumanRva, GateHuman);
        }

        private static byte[] PatternBytes(string value)
        {
            string[] tokens = value.Split(' ');
            var bytes = new byte[tokens.Length];
            for (int index = 0; index < tokens.Length; index++)
                bytes[index] = Convert.ToByte(tokens[index], 16);
            return bytes;
        }

        private static void Copy(byte[] target, int offset, byte[] source) => Array.Copy(source, 0, target, offset, source.Length);
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

        private static void Assert(bool condition, string message)
        {
            if (condition) return;
            failures++;
            Console.Error.WriteLine("FAIL: " + message);
        }

        private sealed class FakeMemory : INativeMemory
        {
            private readonly Dictionary<long, int> values = new Dictionary<long, int>();
            public long? FailNextWriteAddress { get; set; }
            public bool FailRestore { get; set; }
            public int OperationCount { get; private set; }
            public int WriteCount { get; private set; }
            public void Set(long address, int value) => values[address] = value;
            public int ReadRaw(long address) => values[address];
            public int ReadInt32(long address) { OperationCount++; return values[address]; }
            public void WriteInt32(long address, int value)
            {
                OperationCount++; WriteCount++;
                if (FailNextWriteAddress == address) { FailNextWriteAddress = null; throw new InvalidOperationException("injected write failure"); }
                values[address] = value;
            }
            public uint MakeWritable(long address, int length) { OperationCount++; return 0x20; }
            public void RestoreProtection(long address, int length, uint protection)
            {
                OperationCount++;
                if (FailRestore) { FailRestore = false; throw new InvalidOperationException("injected restore failure"); }
            }
            public void Flush(long address, int length) { OperationCount++; }
        }

        private sealed class FakeHookFactory : ISelectedUnitCommandHookFactory
        {
            public int InstallCount { get; private set; }
            public ISelectedUnitCommandHook Install(long targetAddress, SelectedUnitCommandBroker broker)
            {
                InstallCount++;
                return new FakeHook();
            }
            private sealed class FakeHook : ISelectedUnitCommandHook { }
        }
    }
}
