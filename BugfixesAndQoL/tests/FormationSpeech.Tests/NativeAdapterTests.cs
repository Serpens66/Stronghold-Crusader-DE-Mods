using BugfixesAndQoL.UnitCommands;
using Iced.Intel;
using SHCDESE.Interop;
using System.Runtime.InteropServices;
using static Iced.Intel.AssemblerRegisters;

// Private, unpublished executable fixtures only. No game DLL is loaded or modified.
// Original whole-instruction prefixes exercise the production delegate ABI; bounded
// fixture tails record arguments instead of touching real game or audio state.
internal static class NativeAdapterTests
{
    [DllImport("kernel32", SetLastError = true)]
    private static extern IntPtr VirtualAlloc(IntPtr address, nuint size, uint allocation, uint protection);
    [DllImport("kernel32", SetLastError = true)]
    private static extern bool VirtualProtect(IntPtr address, nuint size, uint protection, out uint previous);
    [DllImport("kernel32", SetLastError = true)]
    private static extern bool VirtualFree(IntPtr address, nuint size, uint type);
    [DllImport("kernel32", SetLastError = true)]
    private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, nuint size);

    internal static void Run(byte[] image, Action<bool, string> check)
    {
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8)
            throw new PlatformNotSupportedException("Native speech ABI fixtures require Windows x64.");
        IntPtr module = VirtualAlloc(IntPtr.Zero, (nuint)image.Length, 0x3000, 0x04);
        check(module != IntPtr.Zero, "Allocate private native fixture");
        try
        {
            IntPtr record = IntPtr.Add(module, 0x200000);
            ulong recordAddress = (ulong)record.ToInt64();
            for (int index = 0; index < FormationSpeechNative.ReferenceRvas.Length; index++)
            {
                int rva = FormationSpeechNative.ReferenceRvas[index];
                int prefixLength = FormationSpeechNative.Patterns[index].Split(' ').Length;
                var asm = new Assembler(64);
                if (index == 0)
                {
                    asm.mov(rax, recordAddress);
                    asm.mov(__qword_ptr[rax + 24], rcx); asm.mov(__dword_ptr[rax + 32], edx);
                    asm.mov(__qword_ptr[rax + 40], r8);
                    asm.mov(eax, (int)eChimps.CHIMP_TYPE_SWORDSMAN);
                    asm.add(rsp, 0x1A0); asm.pop(rbx);
                }
                else if (index == 1)
                {
                    asm.mov(rax, recordAddress);
                    asm.mov(__qword_ptr[rax + 64], rcx); asm.mov(__dword_ptr[rax + 72], edx);
                    asm.mov(rax, (ulong)IntPtr.Add(module, FormationSpeechNative.CurrentUnitRva).ToInt64());
                    asm.mov(__dword_ptr[rax], 999); asm.mov(eax, 2);
                    asm.add(rsp, 0x180); asm.pop(rbp);
                }
                else if (index == 2)
                {
                    asm.mov(rax, recordAddress);
                    asm.mov(__qword_ptr[rax], rcx); asm.mov(__dword_ptr[rax + 8], edx);
                    asm.mov(__dword_ptr[rax + 12], r8d); asm.mov(__dword_ptr[rax + 16], r9d);
                    asm.inc(__dword_ptr[rax + 20]);
                    asm.add(rsp, 0x78); asm.pop(rdi); asm.pop(rsi); asm.pop(rbp); asm.pop(rbx);
                }
                else
                {
                    asm.mov(rax, recordAddress);
                    asm.mov(__qword_ptr[rax + 48], rcx); asm.inc(__dword_ptr[rax + 56]);
                    asm.mov(eax, __dword_ptr[rax + 60]);
                    asm.add(rsp, 0x20); asm.pop(rbx);
                }
                asm.ret();
                using var stream = new MemoryStream();
                asm.Assemble(new StreamCodeWriter(stream), (ulong)module.ToInt64() + (uint)rva + (uint)prefixLength);
                byte[] body = stream.ToArray();
                IntPtr entry = IntPtr.Add(module, rva);
                Marshal.Copy(image, rva, entry, prefixLength);
                Marshal.Copy(body, 0, IntPtr.Add(entry, prefixLength), body.Length);
                check(VirtualProtect(entry, (nuint)(prefixLength + body.Length), 0x20, out _), "Protect private fixture RX");
                check(FlushInstructionCache(new IntPtr(-1), entry, (nuint)(prefixLength + body.Length)), "Flush private fixture code");
            }
            int resolutions = 0;
            var native = new FormationSpeechNative(module, image, _ => resolutions++);
            check(resolutions == 4, "All native delegates resolved");
            var tribeManager = IntPtr.Add(module, 0x7CC6720);
            check(native.CaptureVoice(tribeManager, 123) == (int)eChimps.CHIMP_TYPE_SWORDSMAN &&
                Marshal.ReadInt64(record, 24) == tribeManager.ToInt64() && Marshal.ReadInt32(record, 32) == 123 &&
                Marshal.ReadInt64(record, 40) == 0, "Voice delegate arguments and EAX return");
            IntPtr current = IntPtr.Add(module, FormationSpeechNative.CurrentUnitRva);
            Marshal.WriteInt32(current, 456);
            check(native.CaptureRepresentative() == 2 && Marshal.ReadInt32(current) == 456 &&
                Marshal.ReadInt64(record, 64) == IntPtr.Add(module, FormationSpeechNative.UnitManagerRva).ToInt64() &&
                Marshal.ReadInt32(record, 72) == 1, "Representative delegate and scratch restoration");
            var near = new FormationSpeechRequest(1, 2, 3, 4, eChimps.CHIMP_TYPE_SWORDSMAN, 0, 0, 10, 10, 1234);
            native.Emit(near);
            check(Marshal.ReadInt64(record) == IntPtr.Add(module, FormationSpeechNative.DialogueManagerRva).ToInt64() &&
                Marshal.ReadInt32(record, 8) == (int)near.VoiceType && Marshal.ReadInt32(record, 12) == 6 &&
                Marshal.ReadInt32(record, 16) == 1234 && Marshal.ReadInt32(record, 20) == 1,
                "Speech delegate RCX/EDX/R8D/R9D arguments");
            check(Marshal.ReadInt64(record, 48) == IntPtr.Add(module, FormationSpeechNative.SoundManagerRva).ToInt64(),
                "Busy delegate manager argument");
            Marshal.WriteInt32(record, 60, 1); native.Emit(near);
            check(Marshal.ReadInt32(record, 20) == 1, "Busy native return suppresses emission");
            Marshal.WriteInt32(record, 60, 0);
            native.Emit(new FormationSpeechRequest(1, 2, 3, 4, eChimps.CHIMP_TYPE_SWORDSMAN, 0, 0, 73, 0, 1234));
            check(Marshal.ReadInt32(record, 12) == 10 && Marshal.ReadInt32(record, 16) == 0 &&
                Marshal.ReadInt32(record, 20) == 2 && Marshal.ReadInt32(record, 56) == 3, "Distant native arguments and exact call counts");
        }
        finally { check(VirtualFree(module, 0, 0x8000), "Release unpublished fixture memory"); }
    }
}
