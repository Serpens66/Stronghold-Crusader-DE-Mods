using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Iced.Intel;
using RedBird.Core.Memory;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL.AI.Market
{
    // Attributes guaranteed future writes of the structurally verified Fixes source. No Fixes
    // assembly reference, settings changes, copied-entry probes or per-call reflection.
    internal static unsafe class MarketFixesDemandSource
    {
        internal const int SourceRva = 0x2A4D8;
        internal const int ObservationRva = 0x2A42A;
        private const int FunctionRva = 0x2A420, FunctionLength = 221;
        private const int SourceLength = 19;
        private static readonly byte[] sourceBytes =
        {
            0x8B, 0x84, 0x0A, 0xAC, 0xFA, 0xFF, 0xFF, 0x85, 0xC0,
            0x7E, 0x19, 0x43, 0x39, 0x84, 0x08, 0x9C, 0xF0, 0x12, 0x00
        };
        private static Channel wood, iron, ale;
        private static X64InlineHook owner;
        private static IntPtr sourceAddress;
        private static byte[] publishedPatch, expectedStub;
        private static IntPtr functionAddress;
        private static byte[] sealedBody;
        private static ManualLogSource logger;
        private static bool initialized, covered, failureReported, sourceExists, sealedAfterInstallation;

        private struct Channel
        {
            internal NativeFlagArray Flags;
            internal NativeStateArray<int> Threshold, Amount;
            internal ulong FlagsAddress, ThresholdAddress, AmountAddress;
            internal int Good;
        }

        internal static bool IsCovered => covered;
        // Unknown reflection/shape states count conservatively as an existing source.
        // A recognized, uninstalled HookHandle alone proves the optional source absent.
        internal static bool DoesSourceExist => sourceExists;

        internal static void Initialize(CrusaderLibraryLoadContext context, ManualLogSource log)
        {
            if (initialized) return;
            initialized = true;
            logger = log;
            if (!Chainloader.PluginInfos.TryGetValue("fixes", out var info))
            {
                Shared.DebugLogHelper.LogInfo(log,
                    "AI_MARKET_FIXES_SOURCE: covered=false reason=Fixes-not-installed; optional custom source absent.");
                return;
            }
            sourceExists = true;
            try
            {
                Type type = info.Instance.GetType().Assembly.GetType("Fixes.Detours.FixesAIDetours", true);
                object handle = RequiredField(type, "c_game_ai_food_buy_handler_hook").GetValue(null);
                PropertyInfo hookProperty = handle?.GetType().GetProperty("Hook", BindingFlags.Public | BindingFlags.Instance);
                if (hookProperty == null || hookProperty.PropertyType != typeof(X64InlineHook))
                    throw new InvalidOperationException("Custom-purchasing HookHandle shape differs.");
                owner = hookProperty.GetValue(handle, null) as X64InlineHook;
                if (owner == null || !owner.IsInstalled)
                {
                    sourceExists = false;
                    Shared.DebugLogHelper.LogInfo(log, "AI_MARKET_FIXES_SOURCE: covered=false reason=custom-source-not-installed.");
                    return;
                }
                wood = ReadChannel(type, "Wood", eGoods.STORED_WOOD_PLANKS);
                iron = ReadChannel(type, "Iron", eGoods.STORED_IRON_INGOTS);
                ale = ReadChannel(type, "Ale", eGoods.STORED_FOOD_ALE);
                ulong image = unchecked((ulong)context.ModuleHandle.ToInt64());
                if (owner == null || !owner.IsInstalled || owner.TargetAddress != image + SourceRva ||
                    owner.DisplacedByteCount != SourceLength || owner.StubAddress == IntPtr.Zero)
                    throw new InvalidOperationException("Custom-purchasing hook is absent or its published shape differs.");
                if (!context.Memory.Slice(SourceRva, SourceLength).SequenceEqual(sourceBytes))
                    throw new InvalidOperationException("Native custom-purchasing anchor differs.");

                sourceAddress = (IntPtr)unchecked((long)(image + SourceRva));
                expectedStub = CreateExpectedStub(image, unchecked((ulong)owner.StubAddress.ToInt64()));
                RequireReadable(owner.StubAddress, expectedStub.Length);
                if (!Matches(owner.StubAddress, expectedStub))
                    throw new InvalidOperationException("Published custom-purchasing native semantics are unrecognized.");
                publishedPatch = new byte[SourceLength];
                publishedPatch[0] = 0xFF; publishedPatch[1] = 0x25;
                Array.Copy(BitConverter.GetBytes(owner.StubAddress.ToInt64()), 0, publishedPatch, 6, 8);
                for (int index = 14; index < SourceLength; index++) publishedPatch[index] = 0x90;
                if (!Matches(sourceAddress, publishedPatch))
                    throw new InvalidOperationException("Published custom-purchasing entry bytes differ.");
                functionAddress = (IntPtr)unchecked((long)(image + FunctionRva));
                sealedBody = context.Memory.Slice(FunctionRva, FunctionLength).ToArray();
                // Initialize before our own source/observer relays are published.
                // The sole permitted pre-existing mutation is the verified Fixes span.
                Array.Copy(publishedPatch, 0, sealedBody, SourceRva - FunctionRva, SourceLength);
                if (!Matches(functionAddress, sealedBody))
                    throw new InvalidOperationException("Food-purchase body has an unrecognized existing patch.");
                covered = true;
                Shared.DebugLogHelper.LogInfo(log,
                    "AI_MARKET_FIXES_SOURCE: covered=true source=0x2A4D8 observation=0x2A42A goods=wood,iron,ale proof=guaranteed-native-write.");
            }
            catch (Exception error) { Disable(error.Message); }
        }

        // Call at 2A42A before IMUL R8,RAX,583C, with player from preserved RAX.
        // The complete recognized body has no calls/stock writes: AIC==0 alone
        // exits before Fixes, while every nonzero-AIC food branch joins 2A4D8.
        // Flags/thresholds cannot change inside that straight native invocation.
        // Each satisfied custom guard therefore guarantees its future assignment,
        // including same-value writes. Pending is intentionally not checked yet.
        // 2A4EB is NOT a complete observer: the replayed JLE bypasses it when the
        // hops threshold is nonpositive, AFTER custom writes have already executed.
        internal static void CapturePlanned(int player, Action<int, int, int, int, int> capture)
        {
            if (!covered || !sealedAfterInstallation || capture == null ||
                player <= 0 || player > GamePlayerManagerAPI.MAX_PLAYERS) return;
            if (!Matches(sourceAddress, publishedPatch))
            {
                Disable("Custom-purchasing live span was changed by another patch.");
                return;
            }
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(player, out GamePlayerResources* resources) ||
                resources == null || resources->r_AILordMinusOne == 0) return;
            int* stock = (int*)&resources->r_TotalGoodsNull;
            CaptureChannel(wood, player, stock, capture);
            CaptureChannel(iron, player, stock, capture);
            CaptureChannel(ale, player, stock, capture);
        }

        // The caller invokes this only after its independently audited, permanent
        // source and observation relays have been published and checked. This is
        // a byte snapshot, never an executable copied-entry/backend probe.
        internal static void SealBodyAfterInstallation()
        {
            if (!covered || sealedAfterInstallation) return;
            if (!Matches(sourceAddress, publishedPatch) || !Matches(owner.StubAddress, expectedStub))
            {
                Disable("Custom-purchasing source changed during observer installation.");
                return;
            }
            Marshal.Copy(functionAddress, sealedBody, 0, sealedBody.Length);
            sealedAfterInstallation = true;
        }

        internal static void Recheck()
        {
            if (covered && (!owner.IsInstalled || !Matches(sourceAddress, publishedPatch) ||
                !Matches(owner.StubAddress, expectedStub) || !Matches(functionAddress, sealedBody)))
                Disable("Custom-purchasing owner, native stub or food-purchase body changed since validation.");
        }

        // Relevant live policy only; the owner can compare threshold+amount
        // exactly without hashing unrelated AIC fields. Disabled flags are data,
        // whereas an unknown/unvalidated channel has no usable policy snapshot.
        internal static bool TryGetPolicySnapshot(int player, int good,
            out int enabledFlag, out int threshold, out int amount)
        {
            enabledFlag = threshold = amount = 0;
            if (!covered || !sealedAfterInstallation || player <= 0 ||
                player > GamePlayerManagerAPI.MAX_PLAYERS) return false;
            Channel channel;
            if (good == (int)eGoods.STORED_WOOD_PLANKS) channel = wood;
            else if (good == (int)eGoods.STORED_IRON_INGOTS) channel = iron;
            else if (good == (int)eGoods.STORED_FOOD_ALE) channel = ale;
            else return false;
            enabledFlag = *((byte*)channel.FlagsAddress + player);
            threshold = *((int*)channel.ThresholdAddress + player);
            amount = *((int*)channel.AmountAddress + player);
            return true;
        }

        private static void CaptureChannel(Channel channel, int player, int* stock,
            Action<int, int, int, int, int> capture)
        {
            if (*((byte*)channel.FlagsAddress + player) == 0) return;
            int current = stock[channel.Good];
            int threshold = *((int*)channel.ThresholdAddress + player);
            int amount = *((int*)channel.AmountAddress + player);
            // Fixes uses a signed stock<threshold comparison without requiring
            // threshold>0. Nonpositive emitted amounts are not correction candidates.
            if (current < threshold && amount > 0)
                capture(player, channel.Good, current, amount, SourceRva);
        }

        private static Channel ReadChannel(Type type, string name, eGoods good)
        {
            var channel = new Channel
            {
                Flags = RequiredField(type, "buy" + name + "AtFlagArray").GetValue(null) as NativeFlagArray,
                Threshold = RequiredField(type, "buy" + name + "AtArray").GetValue(null) as NativeStateArray<int>,
                Amount = RequiredField(type, "buy" + name + "AmountArray").GetValue(null) as NativeStateArray<int>,
                Good = (int)good
            };
            int expectedLength = GamePlayerManagerAPI.MAX_PLAYERS + 1;
            if (channel.Flags == null || channel.Threshold == null || channel.Amount == null ||
                channel.Flags.Length != expectedLength || channel.Threshold.Length != expectedLength ||
                channel.Amount.Length != expectedLength)
                throw new InvalidOperationException("Custom-purchasing arrays are unavailable or their bounds differ: " + name);
            channel.FlagsAddress = channel.Flags.Address;
            channel.ThresholdAddress = channel.Threshold.Address;
            channel.AmountAddress = channel.Amount.Address;
            RequireReadable((IntPtr)unchecked((long)channel.FlagsAddress), expectedLength);
            RequireReadable((IntPtr)unchecked((long)channel.ThresholdAddress), expectedLength * sizeof(int));
            RequireReadable((IntPtr)unchecked((long)channel.AmountAddress), expectedLength * sizeof(int));
            return channel;
        }

        private static FieldInfo RequiredField(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException("Missing custom-purchasing field: " + name);
            return field;
        }

        private static byte[] CreateExpectedStub(ulong image, ulong address)
        {
            var asm = new Assembler(64);
            asm.push(rax); asm.push(rdx); asm.push(r10); asm.push(r11);
            asm.mov(rax, 0x5CD7F862489FF00B); asm.mul(r8); asm.shr(rdx, 13);
            EmitChannel(asm, wood);
            EmitChannel(asm, iron);
            EmitChannel(asm, ale);
            asm.pop(r11); asm.pop(r10); asm.pop(rdx); asm.pop(rax);
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(sourceBytes));
            decoder.IP = image + SourceRva;
            while (decoder.IP < image + SourceRva + SourceLength)
            {
                decoder.Decode(out Instruction instruction);
                if (instruction.IsInvalid) throw new InvalidOperationException("Invalid source anchor instruction.");
                asm.AddInstruction(instruction);
            }
            // X64InlineHook appends this continuation after Fixes' generator.
            asm.AddUnrestrictedJmp(image + SourceRva + SourceLength);
            using (var output = new MemoryStream())
            {
                asm.Assemble(new StreamCodeWriter(output), address);
                return output.ToArray();
            }
        }

        private static void EmitChannel(Assembler asm, Channel channel)
        {
            Label done = asm.CreateLabel();
            // Installed RedBird TestNativeFlag(flags,RDX,R10) with its default
            // preserveScratch=true has exactly this push/mov/cmp/pop contract.
            asm.push(r10); asm.mov(r10, channel.FlagsAddress);
            asm.cmp(__byte_ptr[r10 + rdx], 0); asm.pop(r10); asm.jz(done);
            asm.mov(r10, channel.ThresholdAddress); asm.mov(r11d, __dword_ptr[r10 + rdx * 4]);
            asm.cmp(__dword_ptr[r8 + r9 + 0x12F090 + channel.Good * sizeof(int)], r11d);
            asm.jge(done);
            asm.mov(r10, channel.AmountAddress); asm.mov(eax, __dword_ptr[r10 + rdx * 4]);
            asm.mov(__dword_ptr[r8 + r9 + 0x131630 + channel.Good * sizeof(int)], eax);
            asm.Label(ref done);
        }

        private static bool Matches(IntPtr address, byte[] expected)
        {
            byte* pointer = (byte*)address.ToPointer();
            for (int index = 0; index < expected.Length; index++)
                if (pointer[index] != expected[index]) return false;
            return true;
        }

        private static void Disable(string reason)
        {
            covered = false;
            if (failureReported) return;
            failureReported = true;
            Shared.DebugLogHelper.LogError(logger,
                "AI_MARKET_FIXES_SOURCE: covered=false; unknown custom demand remains unchanged. " + reason);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryInformation
        {
            internal IntPtr BaseAddress, AllocationBase;
            internal uint AllocationProtect;
            internal UIntPtr RegionSize;
            internal uint State, Protect, Type;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern UIntPtr VirtualQuery(IntPtr address, out MemoryInformation information, UIntPtr size);

        private static void RequireReadable(IntPtr address, int length)
        {
            if (address == IntPtr.Zero || length <= 0 ||
                VirtualQuery(address, out MemoryInformation info, (UIntPtr)Marshal.SizeOf(typeof(MemoryInformation))) == UIntPtr.Zero ||
                info.State != 0x1000 || (info.Protect & (0x100 | 0x01)) != 0)
                throw new InvalidOperationException("Custom-purchasing memory is unavailable.");
            uint access = info.Protect & 0xFF;
            if (access != 0x02 && access != 0x04 && access != 0x08 &&
                access != 0x20 && access != 0x40 && access != 0x80)
                throw new InvalidOperationException("Custom-purchasing memory is not readable.");
            ulong offset = unchecked((ulong)(address.ToInt64() - info.BaseAddress.ToInt64()));
            if (offset > info.RegionSize.ToUInt64() || (ulong)length > info.RegionSize.ToUInt64() - offset)
                throw new InvalidOperationException("Custom-purchasing read exceeds its native memory region.");
        }
    }
}
