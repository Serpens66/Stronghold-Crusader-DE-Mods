using SerpNativeAPI;
using SHCDESE.EventAPI;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace SerpNativeAPITests
{
    internal static class Program
    {
        private const long ModuleBase = 0x10000000;
        private const int FunctionRva = 0x1100;
        private const int FunctionSize = 0x300;
        private const int DecisionRva = 0x1200;
        private const int HumanDelayRva = 0x1280;
        private static readonly byte[] DecisionBytes = Hex(
            "40 84 F6 75 10 41 81 F8 C8 00 00 00 7D 10 B8 B0 04 00 00 EB 69 41 81 F8 8C 00 00 00 7C 5B");
        private static readonly byte[] HumanDelayBytes = Hex("EB 50 B8 64 00 00 00 48 8D 2D C0 83 F4 FF");
        private static int failures;

        private static int Main()
        {
            TestPeValidation();
            TestFixedCatalogValidation();
            TestReadinessAndIndependentCapabilities();
            TestOwnership();
            TestGatehouseTransactionAndRounding();
            TestGatehouseRollbackAndPageCleanup();
            TestSelectedBroker();
            TestSelectedEventService();
            if (failures == 0)
            {
                Console.WriteLine("PASS: SerpNativeAPI baseline-hardened tests passed.");
                return 0;
            }
            Console.Error.WriteLine($"FAIL: SerpNativeAPI tests reported {failures} failure(s).");
            return 1;
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

            byte[] wrongHashImage = (byte[])image.Clone();
            var wrongHashCatalog = CloneCatalog(catalog, functionHash: new string('0', 64));
            runtime = InitializeRuntime(wrongHashImage, wrongHashCatalog, SeedRuntimeMemory(wrongHashImage, wrongHashCatalog), new FakeEventSource());
            AssertGateValidationFailure(runtime, "wrong function hash must fail");

            byte[] wrongOpcode = (byte[])image.Clone();
            wrongOpcode[DecisionRva] ^= 1;
            Copy(wrongOpcode, 0x1500, DecisionBytes); // A decoy must never be used as a fallback.
            GatehouseBuildTarget wrongOpcodeCatalog = CloneCatalog(catalog, functionHash: SerpNativeApiRuntime.ComputeSha256(
                new ReadOnlySpan<byte>(wrongOpcode, FunctionRva, FunctionSize)));
            runtime = InitializeRuntime(wrongOpcode, wrongOpcodeCatalog, SeedRuntimeMemory(wrongOpcode, wrongOpcodeCatalog), new FakeEventSource());
            AssertGateValidationFailure(runtime, "wrong opcode must fail without accepting a decoy");

            byte[] wrongImmediate = (byte[])image.Clone();
            WriteInt32(wrongImmediate, DecisionRva + 8, 201);
            GatehouseBuildTarget wrongImmediateCatalog = CloneCatalog(catalog, functionHash: SerpNativeApiRuntime.ComputeSha256(
                new ReadOnlySpan<byte>(wrongImmediate, FunctionRva, FunctionSize)));
            runtime = InitializeRuntime(wrongImmediate, wrongImmediateCatalog, SeedRuntimeMemory(wrongImmediate, wrongImmediateCatalog), new FakeEventSource());
            AssertGateValidationFailure(runtime, "wrong Vanilla immediate must fail");

            GatehouseBuildTarget outside = CloneCatalog(catalog, aiCloseDistanceRva: FunctionRva - 4);
            runtime = InitializeRuntime(image, outside, SeedRuntimeMemory(image, catalog), new FakeEventSource());
            AssertGateValidationFailure(runtime, "catalogued immediate outside function must fail");
        }

        private static void TestReadinessAndIndependentCapabilities()
        {
            byte[] image = CreatePeImage(0x4000, true);
            var runtime = new SerpNativeApiRuntime();
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

        private static void TestGatehouseTransactionAndRounding()
        {
            FakeMemory memory = SeedDirectGateMemory();
            IGatehouseTimingCapability capability = CreateGateService(memory, new NativeOwnershipRegistry()).Bind("owner");
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

            memory.Set(ModuleBase + 0xB7BC3, 201);
            Assert(!capability.TryApply(rounded, out NativeCapabilityDiagnostic changed) &&
                changed.State == NativeCapabilityState.ValidationFailed, "external immediate mutation fails closed");
            memory.Set(ModuleBase + 0xB7BC3, 200);
            memory.SetByte(ModuleBase + 0xB7BC0, 0x90);
            Assert(!capability.TryApply(rounded, out changed) && changed.State == NativeCapabilityState.ValidationFailed,
                "external opcode mutation fails closed before writing");
        }

        private static void TestGatehouseRollbackAndPageCleanup()
        {
            FakeMemory memory = SeedDirectGateMemory();
            IGatehouseTimingCapability capability = CreateGateService(memory, new NativeOwnershipRegistry()).Bind("owner");
            memory.FailNextWriteAddress = ModuleBase + 0xB7BCA;
            Assert(!capability.TryApply(new GatehouseTimingSettings(true, 1, 5, 10, 15), out _), "partial write should fail");
            Assert(memory.ReadRaw(ModuleBase + 0xB7BC3) == 200 && memory.ReadRaw(ModuleBase + 0xB7BCA) == 1200 &&
                memory.ReadRaw(ModuleBase + 0xB7BD3) == 140 && memory.ReadRaw(ModuleBase + 0xB7C35) == 100,
                "partial write should roll back all four values");
            Assert(memory.WritablePages.Count == 1 && memory.RestoredProtections.Count == 1,
                "the four current RVAs share one 4 KiB page");

            FakeMemory changedDuringAcquire = SeedDirectGateMemory();
            capability = CreateGateService(changedDuringAcquire, new NativeOwnershipRegistry()).Bind("owner");
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
            capability = CreateGateService(memory, new NativeOwnershipRegistry()).Bind("owner");
            memory.FailNextWriteAddress = ModuleBase + 0xB7BCA;
            memory.FailRestore = true;
            memory.FailFlush = true;
            Assert(!capability.TryApply(new GatehouseTimingSettings(true, 1, 5, 10, 15), out NativeCapabilityDiagnostic combined) &&
                combined.Reason.Contains("transaction and cleanup"), "write, restore, and flush failures should remain combined");

            memory = SeedDirectGateMemory();
            var registry = new NativeOwnershipRegistry();
            GatehouseTimingService service = CreateGateService(memory, registry);
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

        private static void AssertGateValidationFailure(SerpNativeApiRuntime runtime, string message) =>
            Assert(!runtime.TryGetGatehouseTiming("owner", out _, out NativeCapabilityDiagnostic diagnostic) &&
                diagnostic.State == NativeCapabilityState.ValidationFailed, message);

        private static GatehouseBuildTarget InstallTestGatehouse(byte[] image)
        {
            Copy(image, DecisionRva, DecisionBytes);
            Copy(image, HumanDelayRva, HumanDelayBytes);
            return new GatehouseBuildTarget(
                "TESTHASH", FunctionRva, FunctionSize,
                SerpNativeApiRuntime.ComputeSha256(new ReadOnlySpan<byte>(image, FunctionRva, FunctionSize)),
                DecisionRva, DecisionBytes, HumanDelayRva, HumanDelayBytes,
                DecisionRva + 8, DecisionRva + 15, DecisionRva + 24, HumanDelayRva + 3);
        }

        private static GatehouseBuildTarget CloneCatalog(
            GatehouseBuildTarget source,
            string functionHash = null,
            int? aiCloseDistanceRva = null) =>
            new GatehouseBuildTarget(
                source.BuildHash, source.FunctionRva, source.FunctionSize, functionHash ?? source.FunctionHash,
                source.DecisionBlockRva, source.DecisionBlockBytes, source.HumanDelayBlockRva, source.HumanDelayBlockBytes,
                aiCloseDistanceRva ?? source.AiCloseDistanceRva, source.AiReopenDelayRva,
                source.HumanCloseDistanceRva, source.HumanReopenDelayRva);

        private static FakeMemory SeedRuntimeMemory(byte[] image, GatehouseBuildTarget target)
        {
            var memory = new FakeMemory();
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

        private static GatehouseTimingService CreateGateService(FakeMemory memory, NativeOwnershipRegistry ownership)
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
            return new GatehouseTimingService("hash", target, memory, ownership, null);
        }

        private static FakeMemory SeedDirectGateMemory()
        {
            var memory = new FakeMemory();
            memory.SetByte(ModuleBase + 0xB7BC0, 0x41);
            memory.SetByte(ModuleBase + 0xB7BC1, 0x81);
            memory.SetByte(ModuleBase + 0xB7BC2, 0xF8);
            memory.SetByte(ModuleBase + 0xB7C34, 0xB8);
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
            return new GatehouseTimingService("hash", target, memory, new NativeOwnershipRegistry(), null);
        }

        private static FakeMemory SeedCrossPageGateMemory()
        {
            var memory = new FakeMemory();
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
