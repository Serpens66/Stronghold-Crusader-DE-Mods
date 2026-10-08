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
    [TestClass]
    [DoNotParallelize]
    public partial class RuntimeTests
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

        private sealed class LoggingLevelListener : BepInEx.Logging.ILogListener
        {
            public BepInEx.Logging.LogLevel DisplayedLogLevel { get; set; }
            public void LogEvent(object sender, BepInEx.Logging.LogEventArgs args) { }
            public void Dispose() { }
        }

        private static bool CaptureSelectionState(int playerId, EngineInterface.PlayState state,
            out LocalSelectionSnapshot snapshot) =>
            CaptureSelectionState(playerId, () => state, out snapshot);

        private static bool CaptureSelectionState(int playerId, Func<EngineInterface.PlayState> readState,
            out LocalSelectionSnapshot snapshot)
        {
            MethodInfo capture = typeof(LocalSelectionAPI).GetMethod("TryCaptureState",
                BindingFlags.Static | BindingFlags.NonPublic);
            object[] args = { playerId, readState, null };
            bool success = (bool)capture.Invoke(null, args);
            snapshot = (LocalSelectionSnapshot)args[2];
            return success;
        }

        private static int FindUniqueReference(byte[] data, string pattern)
        {
            string[] tokens = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int found = -1;
            for (int offset = 0; offset <= data.Length - tokens.Length; offset++)
            {
                bool matches = true;
                for (int index = 0; index < tokens.Length; index++)
                {
                    if (tokens[index] != "?" && tokens[index] != "??" &&
                        data[offset + index] != Convert.ToByte(tokens[index], 16))
                    {
                        matches = false;
                        break;
                    }
                }
                if (!matches)
                    continue;
                if (found >= 0)
                    return -2;
                found = offset;
            }
            return found;
        }

        private static void StampPattern(byte[] data, int offset, string pattern, Random random)
        {
            string[] tokens = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < tokens.Length; index++)
                data[offset + index] = tokens[index] == "?" || tokens[index] == "??"
                    ? (byte)random.Next(0, 256)
                    : Convert.ToByte(tokens[index], 16);
        }

        private static byte[] MapPeImage(byte[] file)
        {
            int peOffset = BitConverter.ToInt32(file, 0x3C);
            int sectionCount = BitConverter.ToUInt16(file, peOffset + 6);
            int optionalHeaderSize = BitConverter.ToUInt16(file, peOffset + 20);
            int optionalHeader = peOffset + 24;
            int imageSize = BitConverter.ToInt32(file, optionalHeader + 56);
            int headersSize = BitConverter.ToInt32(file, optionalHeader + 60);
            var mapped = new byte[imageSize];
            Buffer.BlockCopy(file, 0, mapped, 0, Math.Min(headersSize, file.Length));
            int sectionTable = optionalHeader + optionalHeaderSize;
            for (int index = 0; index < sectionCount; index++)
            {
                int header = sectionTable + index * 40;
                int virtualAddress = BitConverter.ToInt32(file, header + 12);
                int rawSize = BitConverter.ToInt32(file, header + 16);
                int rawOffset = BitConverter.ToInt32(file, header + 20);
                if (rawSize > 0)
                    Buffer.BlockCopy(file, rawOffset, mapped, virtualAddress, rawSize);
            }
            return mapped;
        }

        private static bool ArraysEqual(int[] left, int[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }

        private static void AssertSafePublicType(Type type, string location)
        {
            while (type.IsByRef || type.IsArray)
                type = type.GetElementType();
            if (type.IsGenericType)
            {
                foreach (Type argument in type.GetGenericArguments())
                    AssertSafePublicType(argument, location + " generic argument");
            }
            string typeName = type.Name;
            bool forbidden = type.IsPointer || type == typeof(IntPtr) || type == typeof(UIntPtr) ||
                typeName.IndexOf("NativeDetour", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("MemoryWriter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("Pattern", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.StartsWith("Rva", StringComparison.OrdinalIgnoreCase) ||
                typeName.EndsWith("Rva", StringComparison.OrdinalIgnoreCase);
            Assert(!forbidden, $"forbidden native implementation type at {location}: {type.FullName}");
        }

        private static Instruction[] DecodeInstructions(byte[] bytes, ulong address)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = address;
            var result = new List<Instruction>();
            ulong end = address + unchecked((ulong)bytes.Length);
            while (decoder.IP < end)
            {
                Instruction instruction = decoder.Decode();
                Assert(!instruction.IsInvalid && instruction.NextIP <= end,
                    "gatehouse displaced Vanilla bytes decode without invalid instructions");
                result.Add(instruction);
            }
            return result.ToArray();
        }

        internal static byte[] AssembleAndDecode(Assembler assembler, ulong address)
        {
            using (var stream = new MemoryStream())
            {
                assembler.Assemble(new StreamCodeWriter(stream), address);
                byte[] bytes = stream.ToArray();
                int offset = 0;
                while (offset < bytes.Length)
                {
                    // RedBird's unrestricted CALL jumps over an eight-byte target literal.
                    if (offset <= bytes.Length - 16 && bytes[offset] == 0xEB && bytes[offset + 1] == 8 &&
                        bytes[offset + 10] == 0xFF && bytes[offset + 11] == 0x15 &&
                        BitConverter.ToInt32(bytes, offset + 12) == -14)
                    {
                        offset += 16;
                        continue;
                    }
                    if (offset <= bytes.Length - 14 &&
                        bytes[offset] == 0xFF && bytes[offset + 1] == 0x25 &&
                        bytes[offset + 2] == 0 && bytes[offset + 3] == 0 &&
                        bytes[offset + 4] == 0 && bytes[offset + 5] == 0)
                    {
                        offset += 14;
                        continue;
                    }
                    var remaining = new byte[bytes.Length - offset];
                    Buffer.BlockCopy(bytes, offset, remaining, 0, remaining.Length);
                    var decoder = Decoder.Create(64, new ByteArrayCodeReader(remaining));
                    decoder.IP = address + unchecked((ulong)offset);
                    Instruction instruction = decoder.Decode();
                    Assert(!instruction.IsInvalid && instruction.Length <= remaining.Length,
                        "generated gatehouse stub decodes completely");
                    offset += instruction.Length;
                }
                Assert(offset == bytes.Length, "generated gatehouse stub consumes its complete encoded span");
                return bytes;
            }
        }

        private static ApiSharedRuntime InitializeRuntime(
            byte[] image,
            GatehouseBuildTarget catalog,
            FakeMemory memory)
        {
            var runtime = new ApiSharedRuntime();
            runtime.Initialize(ModuleBase, image, catalog.BuildHash, memory, null, catalog, false);
            return runtime;
        }

        private static void AssertTimingValidationFailure(ApiSharedRuntime runtime, string message) =>
            Assert(!runtime.TryGetGatehouseTiming("owner", out _, out NativeCapabilityDiagnostic diagnostic) &&
                diagnostic.State == NativeCapabilityState.ValidationFailed, message);

        private static void AssertOriginValidationFailure(ApiSharedRuntime runtime, string message) =>
            Assert(!runtime.TryGetGatehouseDistanceOrigin("owner", out _, out NativeCapabilityDiagnostic diagnostic) &&
                diagnostic.State == NativeCapabilityState.ValidationFailed, message);

        private static void AssertBothGateValidationFailures(ApiSharedRuntime runtime, string message)
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
                ApiSharedRuntime.ComputeSha256(new ReadOnlySpan<byte>(image, FunctionRva, FunctionSize)),
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
            GatehousePermanentRuntimeState state,
            NativeOwnershipRegistry ownership,
            object mutationSync)
        {
            var target = new GatehouseDistanceOriginTarget(
                ModuleBase + 0xB7B70,
                VanillaDistanceBytes,
                CenteredDistanceBytes);
            return new GatehouseDistanceOriginService("hash", target, state, ownership, mutationSync, null);
        }

        private static GatehouseTimingService CreateGateService(
            GatehousePermanentRuntimeState state,
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
            return new GatehouseTimingService("hash", target, state, ownership, mutationSync, null);
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
            return new GatehouseTimingService(
                "hash", target, GatehousePermanentRuntimeState.CreateTestState(),
                new NativeOwnershipRegistry(), new object(), null);
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
        private static void Assert(bool condition, string message) =>
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(condition, message);

        private sealed class RecordingAivObserver : IAivBuildStepObserver
        {
            private readonly string name;
            private readonly List<string> calls;
            private readonly bool failBegin;
            private readonly bool failComplete;

            internal RecordingAivObserver(
                string name,
                List<string> calls,
                bool failBegin = false,
                bool failComplete = false)
            {
                this.name = name;
                this.calls = calls;
                this.failBegin = failBegin;
                this.failComplete = failComplete;
            }

            public IAivBuildStepInvocation TryBegin(AivBuildStepContext context)
            {
                calls.Add("begin:" + name);
                if (failBegin)
                    throw new InvalidOperationException("injected begin failure");
                return new RecordingAivInvocation(name, calls, failComplete);
            }
        }
        private static void AssertSequenceEqual(string[] actual, string[] expected, string message)
        {
            if (actual.Length != expected.Length)
            {
                Assert(false, message + $" (length {actual.Length}, expected {expected.Length})");
                return;
            }
            for (int index = 0; index < expected.Length; index++)
                if (!string.Equals(actual[index], expected[index], StringComparison.Ordinal))
                {
                    Assert(false, message + $" (mismatch at index {index}: '{actual[index]}', expected '{expected[index]}')");
                    return;
                }
        }

        private sealed class RecordingAivInvocation : IAivBuildStepInvocation
        {
            private readonly string name;
            private readonly List<string> calls;
            private readonly bool fail;

            internal RecordingAivInvocation(string name, List<string> calls, bool fail)
            {
                this.name = name;
                this.calls = calls;
                this.fail = fail;
            }

            public void Complete(AivBuildStepCompletion completion)
            {
                calls.Add($"complete:{name}:{completion.VanillaCompleted}:{completion.VanillaResult}");
                if (fail)
                    throw new InvalidOperationException("injected completion failure");
            }
        }

        private sealed class RecordingWorkingSourceProvider : IModSettingsWorkingSourceProvider
        {
            public event Action SourcesChanged;
            public List<string> Calls { get; } = new List<string>();
            public string PreferredId { get; set; } = ModSettingsWorkingSourceRegistry.TrailId;
            public string PreferenceContextId { get; set; } = "trail-a";
            public IReadOnlyList<ModSettingsWorkingSource> GetSources(string targetGuid) => new[]
            {
                new ModSettingsWorkingSource { Id = ModSettingsWorkingSourceRegistry.TrailId, Kind = ModSettingsWorkingSourceKind.Trail, DisplayName = "Trail", IsPreferred = PreferredId == ModSettingsWorkingSourceRegistry.TrailId, PreferenceContextId = PreferenceContextId },
                new ModSettingsWorkingSource { Id = ModSettingsWorkingSourceRegistry.MapId, Kind = ModSettingsWorkingSourceKind.Map, DisplayName = "Map", IsPreferred = PreferredId == ModSettingsWorkingSourceRegistry.MapId, PreferenceContextId = PreferenceContextId },
            };
            public void Apply(string targetGuid, string sourceId) => Calls.Add("one:" + targetGuid + ":" + sourceId);
            public void ApplyMany(IEnumerable<string> targetGuids, string sourceId) => Calls.Add("many:" + string.Join(",", targetGuids) + ":" + sourceId);
            public void RaiseChanged() => SourcesChanged?.Invoke();
        }

        private sealed class PresetSaveTestViewModel : PresetLobbyModSettingsViewModel
        {
            protected override string ResolveSettingsUiText(string key, string fallback) =>
                key == "Common.ClientOptions" ? "Translated client options" : key;
        }

        private sealed class DynamicDefaultTestViewModel : PresetLobbyModSettingsViewModel
        {
            [PresetLocal]
            public string[] DynamicValues { get; set; } = Array.Empty<string>();

            public void PublishDefault(string[] value) =>
                SetModDefaultValue(nameof(DynamicValues), value);

            public void PublishUnknownDefault() =>
                SetModDefaultValue("Missing", Array.Empty<string>());

            public void PublishWrongTypeDefault() =>
                SetModDefaultValue(nameof(DynamicValues), 42);
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
        [AssemblyInitialize]
        public static void InitializeHost(TestContext context)
        {
            typeof(BepInEx.Paths).GetProperty(nameof(BepInEx.Paths.ConfigPath))
                .GetSetMethod(true).Invoke(null, new object[] { Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-config") });
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                new AssemblyName(args.Name).Name == "Assembly-CSharp"
                    ? Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assembly-CSharp.dll")) : null;
        }


        private static int Count(string text, string value)
        {
            int count = 0;
            int offset = 0;
            while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += value.Length;
            }
            return count;
        }
        private static string GetGameDirectory()
        {
            string configuration = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameDirectory.txt");
            if (File.Exists(configuration)) return File.ReadAllText(configuration).Trim();
            string directory = Environment.GetEnvironmentVariable("SHCDE_GAME_DIR");
            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException("Set SHCDE_GAME_DIR or supply -p:GameDir when building the integration tests.");
            return directory;
        }
    }
}
