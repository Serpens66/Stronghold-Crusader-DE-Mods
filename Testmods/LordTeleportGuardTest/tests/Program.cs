using LordTeleportGuardTest;
using Iced.Intel;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using static Iced.Intel.AssemblerRegisters;

internal static class Program
{
    [DllImport("kernel32", SetLastError = true)] private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocation, uint protection);
    [DllImport("kernel32", SetLastError = true)] private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint operation);
    [DllImport("kernel32", SetLastError = true)] private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);
    [DllImport("kernel32")] private static extern IntPtr GetCurrentProcess();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void RunCode();
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }
    private static ulong Address(IntPtr p) => unchecked((ulong)p.ToInt64());
    private static int Main(string[] args)
    {
        try
        {
            Check(IntPtr.Size == 8, "x64 test host");
            LordTeleportNative.ValidateLayouts();
            if (args.Length > 1) CheckArtifact(args[1]);
            byte[] file = File.ReadAllBytes(args[0]);
            using (var sha = SHA256.Create()) Check(BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "") == LordTeleportNative.NativeHash, "installed DLL identity");
            AuditImage(file);
            using (var f = new Fixture(file))
            {
                f.Reset(); f.Expect(true, "human Lord home", abi: true);
                f.Reset(); f.Ai(1, 4); f.Expect(false, "AI Lord home");
                f.Reset(); f.Ai(1, 1001); f.Expect(false, "custom AI Lord home");
                f.Reset(); f.UnitWord(0x6E6, (int)eChimps.CHIMP_TYPE_SWORDSMAN); f.Expect(false, "other unit");
                f.Reset(); f.TargetX++; f.Expect(false, "different X");
                f.Reset(); f.TargetY++; f.Expect(false, "different Y");
                f.Reset(); f.Active(false); f.Expect(false, "inactive guard");
                f.Reset(); f.UnitWord(0x6E4, (int)AliveState.NeedsInit); f.Expect(false, "initial creation preserved");
                f.Reset(); f.UnitWord(0x6E4, (int)AliveState.None); f.Expect(false, "empty unit slot");
                f.Reset(); f.UnitWord(0x6EE, 0); f.Expect(false, "neutral player");
                f.Reset(); f.UnitWord(0x6EE, 9); f.Expect(false, "invalid player");
                f.Reset(); f.UnitId = 0; f.Expect(false, "reserved slot");
                f.Reset(); f.UnitId = GameUnitManager.NativeUnitSlotCount; f.Expect(false, "past final unit ID");
                f.Reset(); f.SetId(GameUnitManager.LastLiveUnitId); f.Expect(true, "last allocated unit slot", abi: true);
                for (int player = 1; player <= 8; player++)
                {
                    f.Reset(); f.UnitWord(0x6EE, player); f.Expect(true, "human slot " + player, abi: true);
                    f.Ai(player, 7); f.Expect(false, "AI slot " + player);
                    f.Reset(); f.UnitWord(0x6EE, player); f.Expect(false, "human standing-tile rescue " + player, route: 2);
                }
                foreach (int route in new[] { 1, 2 })
                {
                    f.Reset(); f.Expect(route == 1, "native caller policy " + route, route);
                    f.Reset(); f.Ai(1, 4); f.Expect(false, "native caller AI " + route, route);
                    f.Reset(); f.Active(false); f.Expect(false, "native caller inactive " + route, route);
                }
                f.ExpectRescueTransparency();
                f.Reset();
                for (int i = 0; i < 500; i++)
                {
                    f.SetId(1); // Restore the exterior position without clearing the bounded trace.
                    f.Expect(i % 2 == 0, "repeated return/rescue request " + i, route: i % 2 + 1);
                }
                f.CheckTraceBound();
            }
            Console.WriteLine("PASS: native guard execution, actual RedBird installation, pristine caller blocks, ABI/flags/XMM, no-position-write and bounded diagnostic checks=" + checks);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void CheckArtifact(string path)
    {
        using (var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(path))
        {
            Check(assembly.Name.Version.ToString() == "0.1.0.0", "testmod assembly version");
            bool marker = assembly.CustomAttributes.Any(a => a.AttributeType.FullName == "System.Reflection.AssemblyMetadataAttribute" &&
                (string)a.ConstructorArguments[0].Value == "SerpDevDiagnostics" && (string)a.ConstructorArguments[1].Value == "true");
            bool runtime = assembly.MainModule.Types.Any(t => t.FullName == "Shared.DevDiagRuntime");
#if SERP_DEVDIAG
            Check(marker && runtime, "diagnostic artifact carries release-rejection markers");
#else
            Check(!marker && !runtime, "normal artifact contains no development runtime");
#endif
            var plugin = assembly.MainModule.Types.Single(t => t.BaseType?.FullName == "BepInEx.BaseUnityPlugin");
            var identity = plugin.CustomAttributes.Single(a => a.AttributeType.FullName == "BepInEx.BepInPlugin");
            Check((string)identity.ConstructorArguments[0].Value == "LordTeleportGuardTest_Serp" &&
                (string)identity.ConstructorArguments[2].Value == "0.1.0", "compiled plugin identity");
            Check(!plugin.Methods.Any(m => new[] { "Update", "LateUpdate", "FixedUpdate", "OnDestroy", "OnDisable" }.Contains(m.Name)), "plugin has no short-lived worker or teardown");
            Check(!assembly.MainModule.AssemblyReferences.Any(r => new[] { "Newtonsoft.Json", "System.Text.Json", "System.Web.Extensions", "BugfixesAndQoL" }.Contains(r.Name)), "standalone dependency boundary");
        }
    }

    private static int Raw(byte[] file, int rva)
    {
        int pe = BitConverter.ToInt32(file, 0x3C), section = pe + 24 + BitConverter.ToUInt16(file, pe + 20);
        for (int n = 0; n < BitConverter.ToUInt16(file, pe + 6); n++)
        {
            int h = section + n * 40, start = BitConverter.ToInt32(file, h + 12), length = BitConverter.ToInt32(file, h + 16);
            if (rva >= start && rva < start + length) return BitConverter.ToInt32(file, h + 20) + rva - start;
        }
        throw new InvalidOperationException("RVA has no raw bytes: " + rva.ToString("X"));
    }

    private static void AuditImage(byte[] file)
    {
        int at = Raw(file, LordTeleportNative.EntryRva);
        Check(file.Skip(at).Take(16).SequenceEqual(LordTeleportNative.EntryBytes), "reference prologue");
        int pe = BitConverter.ToInt32(file, 0x3C), opt = pe + 24, section = opt + BitConverter.ToUInt16(file, pe + 20), hits = 0;
        for (int n = 0; n < BitConverter.ToUInt16(file, pe + 6); n++)
        {
            int h = section + n * 40;
            if ((BitConverter.ToUInt32(file, h + 36) & 0x20000000) == 0) continue;
            int size = BitConverter.ToInt32(file, h + 16), raw = BitConverter.ToInt32(file, h + 20);
            for (int i = 0; i <= size - LordTeleportNative.EntrySignature.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < LordTeleportNative.EntrySignature.Length; j++) if (file[raw + i + j] != LordTeleportNative.EntrySignature[j]) { match = false; break; }
                if (match) { hits++; Check(BitConverter.ToInt32(file, h + 12) + i == LordTeleportNative.EntryRva, "signature RVA"); }
            }
        }
        Check(hits == 1, "unique signature in executable PE sections");
        // Check every pdata function's direct control-flow targets, not just the expected callers.
        int pdata = Raw(file, BitConverter.ToInt32(file, opt + 112 + 3 * 8));
        int pdataSize = BitConverter.ToInt32(file, opt + 116 + 3 * 8), functions = 0;
        for (int n = 0; n + 12 <= pdataSize; n += 12)
        {
            int begin = BitConverter.ToInt32(file, pdata + n), end = BitConverter.ToInt32(file, pdata + n + 4);
            if (begin == 0 || end <= begin) continue;
            byte[] body = new byte[end - begin]; Array.Copy(file, Raw(file, begin), body, 0, body.Length);
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(body)); decoder.IP = 0x180000000UL + (uint)begin;
            while (decoder.IP < 0x180000000UL + (uint)end)
            {
                Instruction i = decoder.Decode();
                if (i.Op0Kind == OpKind.NearBranch16 || i.Op0Kind == OpKind.NearBranch32 || i.Op0Kind == OpKind.NearBranch64)
                    Check(i.NearBranchTarget <= 0x18019BA40UL || i.NearBranchTarget >= 0x18019BA50UL, "no incoming edge into displaced span");
            }
            functions++;
        }
        Check(functions > 0, "pdata code coverage available");
        LordTeleportNative.ValidateStandingRescueCall(file.Skip(Raw(file, LordTeleportNative.StandingRescueCallRva)).Take(5).ToArray(), 0x180000000UL);
        byte[] alteredCall = file.Skip(Raw(file, LordTeleportNative.StandingRescueCallRva)).Take(5).ToArray();
        alteredCall[1] ^= 1;
        bool refused = false;
        try { LordTeleportNative.ValidateStandingRescueCall(alteredCall, 0x180000000UL); }
        catch (InvalidOperationException) { refused = true; }
        Check(refused, "changed rescue caller refused before publication");
        foreach (int call in new[] { 0x15ABD1, LordTeleportNative.StandingRescueCallRva })
        {
            var d = Decoder.Create(64, new ByteArrayCodeReader(file.Skip(Raw(file, call)).Take(5).ToArray())); d.IP = 0x180000000UL + (uint)call;
            Instruction i = d.Decode(); Check(i.Mnemonic == Mnemonic.Call && i.NearBranchTarget == 0x18019BA40UL, "original caller target");
        }
        // Human Skirmish idle is not a timed return. The building-attack target-loss branch is the repro trigger.
        Console.WriteLine("AUDIT: pdata functions=" + functions + "; two native home-relocation CALL sites confirmed; gameplay trigger is target loss after building attack, not elapsed gate-close time.");
    }

    private sealed class Fixture : IDisposable
    {
        private readonly IntPtr image, flag, trace, results;
        private readonly int imageSize;
        private readonly X64InlineHook hook;
        private readonly ulong units, lineup, cleanup;
        internal int UnitId = 1, TargetX = 20, TargetY = 20;
        private ulong Base => Address(image);
        private ulong Record => units + (uint)UnitId * (ulong)LordTeleportNative.Stride;
        internal Fixture(byte[] file)
        {
            int pe = BitConverter.ToInt32(file, 0x3C), opt = pe + 24;
            imageSize = BitConverter.ToInt32(file, opt + 56);
            image = VirtualAlloc(IntPtr.Zero, (UIntPtr)(uint)imageSize, 0x3000, 0x40);
            Check(image != IntPtr.Zero, "private mapped-image fixture");
            int section = opt + BitConverter.ToUInt16(file, pe + 20);
            for (int n = 0; n < BitConverter.ToUInt16(file, pe + 6); n++)
            {
                int h = section + n * 40, count = BitConverter.ToInt32(file, h + 16);
                if (count > 0) Marshal.Copy(file, BitConverter.ToInt32(file, h + 20), IntPtr.Add(image, BitConverter.ToInt32(file, h + 12)), count);
            }
            units = Base + LordTeleportNative.UnitsRva; lineup = Base + LordTeleportNative.AiLineUpRva; cleanup = Base + 0x2C00;
            flag = Marshal.AllocHGlobal(4); trace = Marshal.AllocHGlobal(LordTeleportNative.TraceBytes); results = Marshal.AllocHGlobal(256);
            // Populate only the map-validity/row tables consumed by the real teleport helper.
            byte[] valid = Enumerable.Repeat((byte)1, 800 * 800).ToArray(); Marshal.Copy(valid, 0, Ptr(Base + 0x3A11EA4), valid.Length);
            for (int y = 0; y < 800; y++) Marshal.WriteInt32(Ptr(Base + 0x402FF2C + (uint)y * 12), y * 400);
            Marshal.WriteByte(Ptr(Base + 0x4DDD350 + 20 * 400 + 20), 70);
            // Exact native position writer; isolate only its occupancy/presentation external boundaries.
            Marker(0x186AD0, 160); Marker(0x180230, 164); Marker(0x18BE50, 168);
            var a = new Assembler(64); a.xor(eax, eax); a.ret(); Write(Base + 0x196280, a); // authoritative path failure boundary
            a = new Assembler(64); a.mov(eax, 1); a.ret(); Write(Base + 0x188340, a); // path is finished/empty
            a = new Assembler(64); a.AddUnrestrictedJmp(cleanup); Write(Base + 0x15D3C5, a); Write(Base + 0x183041, a);
            hook = new X64InlineHook(Base + LordTeleportNative.EntryRva, 14);
            Check(hook.DisplacedByteCount == 16 && !hook.IsInstalled, "actual backend decode");
            hook.Generate((asm, original, resume) => {
                LordTeleportNative.ValidateInstructions(original, resume);
                LordTeleportNative.Emit(asm, original, Address(flag), units, lineup, Address(trace)); });
            hook.Enable(); Check(hook.IsInstalled, "actual backend installed on PRIVATE image only");
            byte[] patch = new byte[16]; Marshal.Copy(Ptr(Base + LordTeleportNative.EntryRva), patch, 0, patch.Length);
            Check(patch[0] == 0xFF && patch[1] == 0x25 && BitConverter.ToUInt64(patch, 6) == Address(hook.StubAddress) && patch[14] == 0x90 && patch[15] == 0x90, "actual patch shape and destination");
        }
        internal void Reset()
        {
            UnitId = 1; TargetX = TargetY = 20;
            Marshal.Copy(new byte[LordTeleportNative.TraceBytes], 0, trace, LordTeleportNative.TraceBytes);
            Marshal.Copy(new byte[256], 0, results, 256);
            Marshal.Copy(new byte[36], 0, Ptr(lineup), 36);
            Active(true); SetId(1);
        }
        internal void SetId(int id)
        {
            UnitId = id;
            Marshal.Copy(new byte[LordTeleportNative.Stride], 0, Ptr(Record + LordTeleportNative.RecordOrigin), LordTeleportNative.Stride);
            UnitWord(0x6E4, (int)AliveState.IsAlive); UnitWord(0x6E6, (int)eChimps.CHIMP_TYPE_LORD); UnitWord(0x6EE, 1);
            Marshal.WriteInt32(Ptr(Record + 0x6F0), 0x123456);
            UnitWord(0x71C, 100); UnitWord(0x71E, 120); UnitWord(0x70E, 804); UnitWord(0x710, 964); UnitWord(0x712, 3);
            UnitWord(0x992, 20); UnitWord(0x994, 20); UnitWord(0x918, 1); UnitWord(0x9CC, 1);
            Marshal.WriteInt32(Ptr(units), GameUnitManager.NativeUnitSlotCount);
            Marshal.WriteInt32(Ptr(Base + 0x9302C4), id);
        }
        internal void Active(bool value) => Marshal.WriteInt32(flag, value ? 1 : 0);
        internal void Ai(int player, int lord) => Marshal.WriteInt32(Ptr(lineup + (uint)player * 4), lord);
        internal void UnitWord(int offset, int value) => Marshal.WriteInt16(Ptr(Record + (uint)offset), unchecked((short)value));
        private byte[] Snapshot() { byte[] b = new byte[LordTeleportNative.Stride]; Marshal.Copy(Ptr(Record + LordTeleportNative.RecordOrigin), b, 0, b.Length); return b; }
        internal void Expect(bool blocked, string label, int route = 0, bool abi = false)
        {
            Marshal.Copy(new byte[256], 0, results, 256);
            byte[] before = Snapshot(); long traceBefore = Marshal.ReadInt64(trace, 8);
            BuildWrapper(route);
            Check(FlushInstructionCache(GetCurrentProcess(), image, (UIntPtr)(uint)imageSize), "fixture instruction-cache flush");
            ((RunCode)Marshal.GetDelegateForFunctionPointer(Ptr(Base + 0x2000), typeof(RunCode)))();
            Check(Marshal.ReadInt32(results, 160) == (blocked ? 0 : 1) && Marshal.ReadInt32(results, 164) == (blocked ? 0 : 1) && Marshal.ReadInt32(results, 168) == (blocked ? 0 : 1), "original occupancy boundaries exactly once or never: " + label);
            Check(Marshal.ReadInt16(Ptr(Record + 0x71C)) == (blocked ? (short)100 : (short)TargetX) && Marshal.ReadInt16(Ptr(Record + 0x71E)) == (blocked ? (short)120 : (short)TargetY), "position result: " + label);
            Check(Marshal.ReadInt16(Ptr(Record + 0x712)) == (blocked ? 3 : 70), "height result: " + label);
            Check(Marshal.ReadInt64(results, 112) == Marshal.ReadInt64(results, 200), "balanced stack: " + label);
            if (blocked && route == 0) Check(before.SequenceEqual(Snapshot()), "all unit bytes unchanged: " + label);
            if (route == 1) Check((ushort)Marshal.ReadInt16(Ptr(Record + 0x918)) == 0x6D && Marshal.ReadInt16(Ptr(Record + 0x91A)) == 2 && Marshal.ReadInt16(Ptr(Record + 0x9CC)) == 0, "first caller waiting transition unchanged: " + label);
            if (route == 2) Check(Marshal.ReadInt16(Ptr(Record + 0x918)) == 1 && Marshal.ReadInt16(Ptr(Record + 0x9CC)) == 0, "second caller return transition unchanged: " + label);
            if (abi)
            {
                long[] expected = { 0x789, unchecked((long)units), UnitId, TargetX, TargetY, 0x567, 0x678, 0x111, 0x222, 0x333, 0x444, 0x555, 0x666, 0x777 };
                for (int i = 0; i < expected.Length; i++) Check(Marshal.ReadInt64(results, i * 8) == expected[i], "register " + i + ": " + label);
                Check((Marshal.ReadInt64(results, 120) & 0x8C5) == 0x45, "incoming CF/PF/ZF/SF/OF preserved: " + label);
                for (int i = 128; i < 160; i += 8) Check(Marshal.ReadInt64(results, i) == 0x12345678, "XMM0/XMM5 unchanged: " + label);
                Check(Marshal.ReadInt64(results, 176) == 0x12345678 && Marshal.ReadInt64(results, 184) == 0x12345678, "XMM6 unchanged: " + label);
            }
#if SERP_DEVDIAG
            Check(Marshal.ReadInt64(trace, 8) == traceBefore + (blocked ? 1 : 0), "diagnostic decision count: " + label);
            if (blocked && route != 0 && Marshal.ReadInt32(trace) < LordTeleportNative.TraceCapacity)
            {
                int offset = LordTeleportNative.TraceHeader + (Marshal.ReadInt32(trace) - 1) * LordTeleportNative.TraceStride;
                Check(Marshal.ReadInt64(trace, offset + 24) == unchecked((long)(Base + (route == 1 ? 0x15ABD6UL : 0x183014UL))), "actual caller identity: " + label);
            }
#else
            Check(Marshal.ReadInt64(trace, 8) == 0, "normal build has no native diagnostic writes: " + label);
#endif
        }
        internal void ExpectRescueTransparency()
        {
            Reset(); Expect(false, "active standing rescue matches Vanilla", route: 2);
            byte[] activeUnit = Snapshot(), activeRegisters = new byte[256];
            Marshal.Copy(results, activeRegisters, 0, activeRegisters.Length);
            Reset(); Active(false); Expect(false, "inactive standing rescue reference", route: 2);
            byte[] inactiveRegisters = new byte[256]; Marshal.Copy(results, inactiveRegisters, 0, inactiveRegisters.Length);
            Check(activeUnit.SequenceEqual(Snapshot()), "rescue writes exactly match inactive guard for the whole unit record");
            // Absolute stack addresses depend on the managed entry; balance is checked separately by Expect.
            for (int i = 0; i < 112; i += 8)
                Check(BitConverter.ToInt64(activeRegisters, i) == BitConverter.ToInt64(inactiveRegisters, i), "rescue register transparency " + i);
            Check((BitConverter.ToInt64(activeRegisters, 120) & 0x8C5) == (BitConverter.ToInt64(inactiveRegisters, 120) & 0x8C5), "rescue flag transparency");
            foreach (int i in new[] { 128, 136, 144, 152, 176, 184 })
                Check(BitConverter.ToInt64(activeRegisters, i) == BitConverter.ToInt64(inactiveRegisters, i), "rescue XMM transparency " + i);
        }

        internal void CheckTraceBound()
        {
#if SERP_DEVDIAG
            Check(Marshal.ReadInt32(trace) == 32 && Marshal.ReadInt32(trace, 4) == 218 && Marshal.ReadInt64(trace, 8) == 250, "bounded trace invariant");
#else
            Check(Marshal.ReadInt32(trace) == 0, "normal trace absent");
#endif
        }
        private void BuildWrapper(int route)
        {
            var a = new Assembler(64);
            a.push(rbx); a.push(rbp); a.push(rsi); a.push(rdi); a.push(r12); a.push(r13); a.push(r14); a.push(r15); a.sub(rsp, 0x78);
            a.movdqu(__xmmword_ptr[rsp + 0x40], xmm6);
            a.mov(rax, Address(results)); a.mov(__qword_ptr[rax + 200], rsp);
            a.mov(rbx, 0x111UL); a.mov(rbp, 0x222UL); a.mov(rsi, 0x333UL); a.mov(r12, 0x444UL); a.mov(r13, 0x555UL); a.mov(r14, 0x666UL); a.mov(r15, 0x777UL);
            a.mov(rax, 0x12345678UL); a.movq(xmm0, rax); a.punpcklqdq(xmm0, xmm0); a.movdqa(xmm5, xmm0); a.movdqa(xmm6, xmm0);
            a.mov(rcx, units); a.mov(edx, UnitId); a.mov(r8d, TargetX); a.mov(r9d, TargetY); a.mov(r10, 0x567UL); a.mov(r11, 0x678UL);
            a.mov(__dword_ptr[rsp + 0x20], 70);
            if (route == 0)
            {
                a.xor(eax, eax); a.stc(); a.mov(rax, 0x789UL);
                a.call(Base + LordTeleportNative.EntryRva); a.jmp(cleanup);
            }
            else if (route == 1)
            {
                a.mov(r11, units); a.mov(rbx, (ulong)(UnitId * LordTeleportNative.Stride)); a.mov(edi, UnitId); a.mov(esi, 1); a.mov(r13d, 128); a.xor(r10d, r10d);
                a.jmp(Base + 0x15AB57); // unchanged route builder, failure branch, relocation CALL, and waiting transition
            }
            else
            {
                a.mov(r14, Base); a.mov(rbx, units); a.mov(rcx, (ulong)(UnitId * LordTeleportNative.Stride)); a.mov(r13d, 1); a.xor(ebp, ebp);
                a.jmp(Base + 0x182FDB); // unchanged invalid-tile rescue CALL and its state/flag writes
            }
            Write(Base + 0x2000, a);
            a = new Assembler(64); a.push(rax); a.mov(rax, Address(results));
            a.mov(__qword_ptr[rax + 8], rcx); a.mov(__qword_ptr[rax + 16], rdx); a.mov(__qword_ptr[rax + 24], r8); a.mov(__qword_ptr[rax + 32], r9);
            a.mov(__qword_ptr[rax + 40], r10); a.mov(__qword_ptr[rax + 48], r11); a.mov(__qword_ptr[rax + 56], rbx); a.mov(__qword_ptr[rax + 64], rbp);
            a.mov(__qword_ptr[rax + 72], rsi); a.mov(__qword_ptr[rax + 80], r12); a.mov(__qword_ptr[rax + 88], r13); a.mov(__qword_ptr[rax + 96], r14); a.mov(__qword_ptr[rax + 104], r15);
            a.lea(r10, __[rsp + 8]); a.mov(__qword_ptr[rax + 112], r10); a.pushfq(); a.pop(r10); a.mov(__qword_ptr[rax + 120], r10);
            a.movdqu(__xmmword_ptr[rax + 128], xmm0); a.movdqu(__xmmword_ptr[rax + 144], xmm5); a.movdqu(__xmmword_ptr[rax + 176], xmm6);
            a.pop(r10); a.mov(__qword_ptr[rax], r10);
            a.movdqu(xmm6, __xmmword_ptr[rsp + 0x40]); a.add(rsp, 0x78); a.pop(r15); a.pop(r14); a.pop(r13); a.pop(r12); a.pop(rdi); a.pop(rsi); a.pop(rbp); a.pop(rbx); a.ret();
            Write(cleanup, a);
        }
        private void Marker(int rva, int offset)
        {
            var a = new Assembler(64); a.mov(rax, Address(results)); a.inc(__dword_ptr[rax + offset]); a.ret(); Write(Base + (uint)rva, a);
        }
        private void Write(ulong address, Assembler a)
        {
            byte[] bytes = LordTeleportNative.Assemble(a, address); Marshal.Copy(bytes, 0, Ptr(address), bytes.Length);
        }
        private static IntPtr Ptr(ulong address) => new IntPtr(unchecked((long)address));
        public void Dispose()
        {
            // PRIVATE executable fixtures only. No running-game hook is ever uninstalled.
            hook.Dispose(); VirtualFree(image, UIntPtr.Zero, 0x8000);
            Marshal.FreeHGlobal(flag); Marshal.FreeHGlobal(trace); Marshal.FreeHGlobal(results);
        }
    }
}
