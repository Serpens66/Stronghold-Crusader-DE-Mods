using BepInEx.Logging;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using static Iced.Intel.AssemblerRegisters;

namespace APIShared
{
    /// <summary>Optional extension of the timing capability for per-building manual control.</summary>
    public interface IGatehouseAutomationCapability
    {
        /// <summary>
        /// Publishes a simulation-thread query receiving a one-based building ID. True prevents
        /// automatic/linked commands to that building. Null logically disables the policy.
        /// The query must validate the current building identity and must not mutate simulation state.
        /// Hooks and callback storage remain rooted until process exit.
        /// </summary>
        bool TrySetManualOnlyResolver(Func<int, bool> resolver, out NativeCapabilityDiagnostic diagnostic);
    }

    // The close decision is routed by the existing APIShared hook, BEFORE Fixes' B7C39 hook.
    // None of these hooks owns the animation, occupancy or pathfinding update functions.
    internal sealed class GatehouseAutomationNativeState
    {
        internal const int CouplingRva = 0xC5300;
        internal const int HandlerExitRva = 0xB7CB4;
        internal const int ScanRva = 0xB7A71;
        internal const int CouplingExitRva = 0xC54E8;
        internal const int NextBridgeRva = 0xC54D8;
        internal const int ReopenTimerRva = 0x64CCECC; // signed 16-bit Vanilla timer, not a domain ID
        internal static readonly int[] HookRvas = { 0xB7A4E, 0xB7BF4, 0xC53D5, 0xC54A0, 0xD5835, 0xB79E2 };
        internal static readonly byte[][] HookBytes =
        {
            Hex("0F B7 84 1D C4 CE 4C 06 66 85 C0 0F 88 55 02 00 00"),
            Hex("66 83 BC 2B CC CE 4C 06 00 0F 85 7C 00 00 00"),
            Hex("48 98 48 69 C8 2C 03 00 00 42 0F B6 84 29 FE 02 00 00"),
            Hex("48 69 D1 2C 03 00 00 42 0F B6 8C 2A F0 02 00 00"),
            Hex("66 41 89 84 0A 1C 03 00 00 41 83 F8 0A 75 09"),
            Hex("66 89 84 2B CC CE 4C 06 44 39 B4 29 58 CE 79 03 75 11")
        };
        private static readonly byte[] CouplingEntry = Hex("48 89 6C 24 08 48 89 74 24 10 48 89 7C 24 18");
        private static readonly List<GatehouseAutomationNativeState> Published = new List<GatehouseAutomationNativeState>();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ManualQuery(int buildingId);
        private readonly ManualQuery query;
        private readonly ulong moduleBase;
        private readonly ScanRegion region;
        private readonly INativeMemory memory;
        private readonly ManualLogSource log;
        private readonly NativeSection section;
        private readonly HookHandle<X64InlineHook>[] hooks =
        {
            new HookHandle<X64InlineHook>(), new HookHandle<X64InlineHook>(),
            new HookHandle<X64InlineHook>(), new HookHandle<X64InlineHook>(),
            new HookHandle<X64InlineHook>(), new HookHandle<X64InlineHook>()
        };
        private HookTransaction transaction;
        private Func<int, bool> resolver;
        private bool installed;
        private string installationFailure;
        private int callbackFailureLogged;
        private int callbackConfirmed;
        private ulong timingPointerAddress;

        internal ulong QueryAddress { get; }

        internal void BindTimingPointer(ulong address)
        {
            if (installed || address == 0) throw new InvalidOperationException("Timing pointer must be bound before publication.");
            timingPointerAddress = address;
        }

        private GatehouseAutomationNativeState(ulong moduleBase, ScanRegion region,
            INativeMemory memory, ManualLogSource log, NativeSection section)
        {
            this.moduleBase = moduleBase;
            this.region = region;
            this.memory = memory;
            this.log = log;
            this.section = section;
            query = QueryManual;
            QueryAddress = unchecked((ulong)Marshal.GetFunctionPointerForDelegate(query).ToInt64());
        }

        internal static GatehouseAutomationNativeState Prepare(ulong moduleBase, ScanRegion region,
            ReadOnlySpan<byte> image, INativeMemory memory, ManualLogSource log)
        {
            try
            {
                NativeSection section = ValidateImage(image, moduleBase);
                return new GatehouseAutomationNativeState(moduleBase, region, memory, log, section);
            }
            catch (Exception ex)
            {
                NativeApiLog.Error(log, $"capability=gatehouse-automation, status=unavailable, error={ex}");
                return null;
            }
        }

        internal static NativeSection ValidateImage(ReadOnlySpan<byte> image, ulong moduleBase)
        {
            NativePeImage pe = NativePeImage.Parse(image);
            NativeSection section = pe.RequireExecutableRange(CouplingRva, 519, "gate/bridge coupling");
            if (ApiSharedRuntime.ComputeSha256(image.Slice(CouplingRva, 519)) !=
                "71652656B8970E86BBBAE5E3EC95C422489F9B5DE4B020ACEC4C01BA044612A1")
                throw new InvalidOperationException("Gate/bridge coupling function hash changed.");
            pe.RequireExecutableRange(0xD5810, 96, "direct gate command");
            if (ApiSharedRuntime.ComputeSha256(image.Slice(0xD5810, 96)) !=
                "93BFC31E3B3FF25DBECFCF9624EE26A1AAA1A1A169ADDAD0ED1233201B811504")
                throw new InvalidOperationException("Direct gate command function hash changed.");
            for (int site = 0; site < HookRvas.Length; site++)
            {
                pe.RequireExecutableRange(HookRvas[site], HookBytes[site].Length, "gate automation hook");
                if (!image.Slice(HookRvas[site], HookBytes[site].Length).SequenceEqual(HookBytes[site]))
                    throw new InvalidOperationException($"Gate automation bytes changed at 0x{HookRvas[site]:X}.");
                int start = site == 4 ? 0xD5810 : site < 2 || site == 5 ? 0xB73D0 : CouplingRva;
                int length = site == 4 ? 96 : site < 2 || site == 5 ? 2325 : 519;
                ValidateSpan(image.Slice(start, length).ToArray(), moduleBase + (ulong)start,
                    moduleBase + (ulong)HookRvas[site], HookBytes[site].Length);
            }
            return section;
        }

        internal static void ValidateSpan(byte[] function, ulong functionAddress, ulong hookAddress, int length)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(function));
            decoder.IP = functionAddress;
            ulong end = functionAddress + (ulong)function.Length;
            bool startFound = false, endFound = false;
            int decodedLength = 0;
            while (decoder.IP < end)
            {
                Instruction instruction = decoder.Decode();
                if (instruction.IsInvalid || instruction.NextIP > end)
                    throw new InvalidOperationException("Invalid instruction in gate automation function.");
                if (instruction.IP == hookAddress) startFound = true;
                if (instruction.IP >= hookAddress && instruction.IP < hookAddress + (ulong)length)
                    decodedLength += instruction.Length;
                if (instruction.NextIP == hookAddress + (ulong)length) endFound = true;
                if ((instruction.Op0Kind == OpKind.NearBranch16 || instruction.Op0Kind == OpKind.NearBranch32 ||
                     instruction.Op0Kind == OpKind.NearBranch64) &&
                    instruction.NearBranchTarget > hookAddress && instruction.NearBranchTarget < hookAddress + (ulong)length)
                    throw new InvalidOperationException("Branch enters the interior of a gate automation hook.");
            }
            if (!startFound || !endFound || decodedLength != length || length < 14)
                throw new InvalidOperationException("Gate automation span does not match whole instructions.");
        }

        internal bool TryPublish(Func<int, bool> value, out string failure)
        {
            failure = null;
            if (value != null && !installed)
            {
                if (installationFailure != null) { failure = installationFailure; return false; }
                try
                {
                    if (timingPointerAddress == 0) throw new InvalidOperationException("Gate timing snapshot is not bound.");
                    // All targets are checked before the first patch. A foreign hook is never overwritten.
                    for (int site = 0; site < HookRvas.Length; site++) RequireLive(HookRvas[site], HookBytes[site]);
                    RequireLive(CouplingRva, CouplingEntry);
                    transaction = new HookTransaction(region, SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                        new HookTransactionOptions { FailureMode = TransactionFailureMode.RollbackAndThrow, OwnsHooks = true });
                    for (int site = 0; site < HookRvas.Length; site++)
                    {
                        int capturedSite = site;
                        transaction.AddInline(hooks[site], HookTarget.FromAddress(moduleBase + (ulong)HookRvas[site]),
                            (asm, displaced, continuation) => GenerateHook(asm, displaced.ToArray(),
                                capturedSite, moduleBase, QueryAddress, continuation, timingPointerAddress), hookSize: HookBytes[site].Length);
                    }
                    CommitResult result = transaction.Commit();
                    if (!result.IsCompleteSuccess) throw new InvalidOperationException($"Incomplete gate automation transaction: {result}.");
                    for (int site = 0; site < HookRvas.Length; site++)
                    {
                        if (!hooks[site].Success || !hooks[site].IsInstalled ||
                            hooks[site].Hook.DisplacedByteCount != HookBytes[site].Length)
                            throw new InvalidOperationException($"Gate automation DisplacedByteCount mismatch at 0x{HookRvas[site]:X}.");
                    }
                }
                catch (Exception ex)
                {
                    // Only the unpublished initialization candidate can be rolled back.
                    transaction?.Dispose();
                    installationFailure = failure = ex.ToString();
                    NativeApiLog.Error(log, $"capability=gatehouse-automation, status=installation-failed, error={ex}");
                    return false;
                }
                lock (Published) Published.Add(this);
                installed = true;
                NativeApiLog.Info(log, "capability=gatehouse-automation, status=installed, spans=B7A4E/17,B7BF4/15,C53D5/18,C54A0/16,D5835/15,B79E2/18; permanent hooks published.");
            }
            Volatile.Write(ref resolver, value);
            return true;
        }

        private int QueryManual(int buildingId)
        {
            Func<int, bool> current = Volatile.Read(ref resolver);
            if (!installed || current == null || buildingId <= 0) return 0;
            try
            {
                bool manual = current(buildingId);
                if (Interlocked.Exchange(ref callbackConfirmed, 1) == 0)
                    NativeApiLog.Info(log, $"GATE_AUTOMATION_POST_STARTUP: hook confirmed, buildingId={buildingId}, manual={manual}.");
                return manual ? 1 : 0;
            }
            catch (Exception ex)
            {
                if (Interlocked.Exchange(ref callbackFailureLogged, 1) == 0)
                    NativeApiLog.Error(log, $"gatehouse-automation query failed: buildingId={buildingId}, error={ex}");
                return 0;
            }
        }

        private void RequireLive(int rva, byte[] expected)
        {
            bool matches = true;
            for (int index = 0; index < expected.Length; index++)
                matches &= memory.ReadByte((long)moduleBase + rva + index) == expected[index];
            if (!matches)
            {
                // Hash-bound continuations cannot be moved. Still try the executable-section
                // pattern fallback so a failed reference check never silently skips resolution.
                var bytes = new byte[section.Length];
                for (int index = 0; index < bytes.Length; index++)
                    bytes[index] = memory.ReadByte((long)moduleBase + section.Start + index);
                int match = CompiledBytePattern.Parse(BitConverter.ToString(expected).Replace('-', ' ')).FindUnique(bytes);
                if (match < 0 || section.Start + match != rva)
                    throw new InvalidOperationException($"Gate automation target 0x{rva:X} rejected: live reference and unique executable pattern failed (match={match}).");
            }
            NativeApiLog.Info(log, $"capability=gatehouse-automation, target=0x{rva:X}, method={(matches ? "reference-rva" : "pattern")}, bytes={expected.Length}.");
        }

        internal static void GenerateHook(Assembler asm, Instruction[] displaced, int site,
            ulong moduleBase, ulong queryAddress, ulong continuation, ulong timingPointerAddress = 0)
        {
            Label manual = asm.CreateLabel("manualDestination");
            Label vanilla = asm.CreateLabel("vanillaDestination");
            if (site == 4)
            {
                if (timingPointerAddress == 0) throw new ArgumentException("Direct close requires a timing snapshot.");
                asm.cmp(r8d, 10); // Audited native close command; other commands stay Vanilla.
                asm.jne(vanilla);
            }
            EmitQuery(asm, queryAddress, site, manual, vanilla);
            asm.Label(ref vanilla);
            foreach (Instruction instruction in displaced) asm.AddInstruction(instruction);
            asm.AddUnrestrictedJmp(continuation);
            asm.Label(ref manual);
            switch (site)
            {
                case 0: // The native manual timer must not suppress the bridge's enemy scan.
                    asm.AddUnrestrictedJmp(moduleBase + ScanRva);
                    break;
                case 1:
                    asm.cmp(__word_ptr[rbx + rbp + ReopenTimerRva], 0);
                    Label wait = asm.CreateLabel("bridgeReopenWait");
                    asm.jne(wait);
                    EmitCoupling(asm, moduleBase, true);
                    asm.Label(ref wait);
                    asm.AddUnrestrictedJmp(moduleBase + HandlerExitRva);
                    break;
                case 2: asm.AddUnrestrictedJmp(moduleBase + CouplingExitRva); break;
                case 3: asm.AddUnrestrictedJmp(moduleBase + NextBridgeRva); break;
                case 4:
                    EmitDirectCloseDelay(asm, moduleBase, timingPointerAddress);
                    asm.cmp(r8d, 10); // Preserve the displaced comparison's flags on the close continuation.
                    asm.AddUnrestrictedJmp(continuation);
                    break;
                case 5:
                    // AX was decremented by Vanilla immediately before this span. Keep that
                    // write, but do not zero an active manual-gate delay when the enemy list is empty.
                    asm.mov(__word_ptr[rbx + rbp + ReopenTimerRva], ax);
                    asm.AddUnrestrictedJmp(moduleBase + 0xB7A05);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(site));
            }
        }

        private static void EmitDirectCloseDelay(Assembler asm, ulong moduleBase, ulong timingPointerAddress)
        {
            // D5810 has already validated the Global-ID. RCX=manager, R10=building stride.
            // Reproduce B7AB2..B7AED's EXACT role predicate (including mission mode 99),
            // not the local player or a simplified human/AI heuristic. No enemy search here.
            asm.push(rax); asm.push(r9); asm.push(r11);
            asm.mov(r11, moduleBase);
            asm.mov(r9, timingPointerAddress);
            asm.mov(r9, __qword_ptr[r9]);
            Label ai = asm.CreateLabel("directCloseAiDelay");
            Label human = asm.CreateLabel("directCloseHumanDelay");
            Label store = asm.CreateLabel("directCloseStoreDelay");
            asm.cmp(__dword_ptr[r11 + 0x8574B90], 0);
            asm.je(ai);
            asm.movsx(rax, __word_ptr[rcx + r10 + 0x132]);
            asm.cmp(__dword_ptr[r11 + rax * 4 + 0x8574BCC], -1);
            asm.jne(ai);
            asm.cmp(__dword_ptr[r11 + rax * 4 + 0x8574C44], 0);
            asm.je(ai);
            asm.cmp(__dword_ptr[r11 + 0x8574B90], 99);
            asm.jne(human);
            asm.cmp(__dword_ptr[r11 + 0x3669040], 0);
            asm.jne(ai);
            asm.Label(ref human);
            asm.mov(eax, __dword_ptr[r9 + 12]);
            asm.jmp(store);
            asm.Label(ref ai);
            asm.mov(eax, __dword_ptr[r9 + 4]);
            asm.Label(ref store);
            asm.mov(__word_ptr[rcx + r10 + 0x31C], ax);
            asm.pop(r11); asm.pop(r9); asm.pop(rax);
        }

        internal static void GenerateCloseRouting(Assembler asm, ulong moduleBase, ulong queryAddress, ulong closePath)
        {
            if (queryAddress == 0) { asm.AddUnrestrictedJmp(closePath); return; }
            Label manual = asm.CreateLabel("manualGateClose");
            Label vanilla = asm.CreateLabel("vanillaGateClose");
            EmitQuery(asm, queryAddress, 0, manual, vanilla);
            asm.Label(ref vanilla);
            asm.AddUnrestrictedJmp(closePath);
            asm.Label(ref manual);
            // EAX is still the exact delay selected by APIShared; only the native timer changes.
            asm.mov(rbp, moduleBase);
            asm.mov(__word_ptr[rbx + rbp + ReopenTimerRva], ax);
            EmitCoupling(asm, moduleBase, false);
            asm.AddUnrestrictedJmp(moduleBase + HandlerExitRva);
        }

        private static void EmitQuery(Assembler asm, ulong address, int site, Label manual, Label vanilla)
        {
            asm.pushfq();
            asm.push(rax);
            asm.X64FastcallSafe(address, 1, a =>
            {
                if (site < 2 || site == 5) a.mov(ecx, r10d);
                else if (site == 2) a.mov(ecx, eax);
                else if (site == 4) a.mov(ecx, edx);
                // site 3 already has the recipient's one-based ID in RCX.
            }, preserveRAX: false, preserveXMM: true);
            // Recompute AFTER the wrapper; it changes flags while aligning/restoring RSP.
            asm.test(eax, eax);
            Label automatic = asm.CreateLabel("automaticQueryResult");
            asm.je(automatic);
            asm.pop(rax);
            asm.popfq();
            asm.jmp(manual);
            asm.Label(ref automatic);
            asm.pop(rax);
            asm.popfq();
            asm.jmp(vanilla);
        }

        private static void EmitCoupling(Assembler asm, ulong moduleBase, bool open)
        {
            asm.mov(rcx, r13);
            asm.mov(edx, r10d);
            asm.mov(r8d, open ? 1 : 0); // Native boolean open argument, not a command enum.
            asm.xor(r9d, r9d);
            asm.AddUnrestrictedCall(moduleBase + CouplingRva);
        }

        private static byte[] Hex(string value) => Array.ConvertAll(value.Split(' '), b => Convert.ToByte(b, 16));
    }
}
