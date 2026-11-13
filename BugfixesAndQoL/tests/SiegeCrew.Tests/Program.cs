using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Iced.Intel;
using RedBird.X64.Extensions;
using static BugfixesAndQoL.SiegeCrew.NativeContract;
using BugfixesAndQoL.SiegeCrew;
using SHCDESE.Interop;
using static Iced.Intel.AssemblerRegisters;

internal static partial class Program
{
    private const int UnitStride = 0x490;
    private const int StateOffset = 0x918;
    private const int VisualOffset = 0x986;
    private const int FixtureSize = 0x1500;
    private const ulong RaxSentinel = 0x1122334455667788;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate ulong RunStub(IntPtr manager, int slotDisplacement, IntPtr raxResult);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocation, uint protection);
    [DllImport("kernel32.dll")]
    private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint freeType);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);

    private static int checks;

    public static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Pass the installed CrusaderDE.dll path.");
            return 2;
        }

        try
        {
            byte[] image = File.ReadAllBytes(args[0]);
            using (SHA256 sha = SHA256.Create())
                Check(BitConverter.ToString(sha.ComputeHash(image)).Replace("-", "") == NativeHash, "canonical DLL hash");

            IntPtr activation = Marshal.AllocHGlobal(sizeof(int));
            try
            {
                foreach (Site site in TimeoutSites)
                {
                    Instruction[] original = VerifySite(image, site);
                    for (int flags = 0; flags < 2; flags++)
                    {
                        ulong baseline = Execute(site, original, activation, flags != 0, useStub: false, expectedCounter: 121);
                        Marshal.WriteInt32(activation, 0);
                        Check(baseline == Execute(site, original, activation, flags != 0, useStub: true, expectedCounter: 121),
                            site.Name + " preserves flags when dormant");
                        Publish(activation, () => Check(Marshal.ReadInt32(activation) == 0, "gate dormant during publication"), enabled: true);
                        Check(baseline == Execute(site, original, activation, flags != 0, useStub: true, expectedCounter: 0),
                            site.Name + " preserves flags when active");
                    }
                }

                VerifyFirePatch(image, activation);
                VerifySettingsCombinations(image, activation);
                VerifyAiReadiness(image, activation);
                VerifyAutoBinding(image, activation);

                // Model a backend that can fail after a site was enabled, including
                // the last one. Every stub still executes its dormant native path.
                for (int failedSite = 0; failedSite < TimeoutSites.Length; failedSite++)
                {
                    Marshal.WriteInt32(activation, 0);
                    var failure = new InvalidOperationException("fixture publication failure");
                    try
                    {
                        Publish(activation, () =>
                        {
                            for (int siteIndex = 0; siteIndex < TimeoutSites.Length; siteIndex++)
                            {
                                Site site = TimeoutSites[siteIndex];
                                Execute(site, VerifySite(image, site), activation, zeroFlag: true, useStub: true, expectedCounter: 121);
                                if (siteIndex == failedSite)
                                    throw failure;
                            }
                        }, enabled: true);
                        throw new Exception("Expected publication failure.");
                    }
                    catch (InvalidOperationException error) when (ReferenceEquals(error, failure))
                    {
                        Check(Marshal.ReadInt32(activation) == 0, "partial publication keeps all sites dormant");
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(activation);
            }

            Console.WriteLine($"PASS: {checks} siege crew byte, execution and publication checks.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void VerifySettingsCombinations(byte[] image, IntPtr timeout)
    {
        IntPtr fire = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            Site timeoutSite = TimeoutSites[2];
            Instruction[] timeoutInstructions = VerifySite(image, timeoutSite);
            Site fireSite = SiegeCrewFirePatch.FireSite;
            int offset = CodeOffset(image, fireSite.Rva, fireSite.Original.Length);
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(image.AsSpan(offset, fireSite.Original.Length).ToArray()),
                0x180000000UL + (uint)fireSite.Rva);
            Instruction[] fireInstructions = { decoder.Decode(), decoder.Decode(), decoder.Decode() };
            foreach (bool global in new[] { true, false })
            foreach (bool timeoutEnabled in new[] { true, false })
            foreach (bool fireEnabled in new[] { true, false })
            {
                SetActivation(timeout, global, timeoutEnabled);
                SetActivation(fire, global, fireEnabled);
                Execute(timeoutSite, timeoutInstructions, timeout, zeroFlag: true, useStub: true,
                    expectedCounter: global && timeoutEnabled ? 0 : 121);
                ExecuteFire(eChimps.CHIMP_TYPE_TREBUCHET, 3, fire, fireInstructions, null,
                    global && fireEnabled ? new[] { 2, 3, 4 } : Array.Empty<int>());
            }
        }
        finally
        {
            Marshal.FreeHGlobal(fire);
        }
    }

    private static Instruction[] VerifySite(byte[] image, Site site)
    {
        int offset = CodeOffset(image, site.Rva, site.Original.Length + site.Continuation.Length);
        byte[] span = image.AsSpan(offset, site.Original.Length).ToArray();
        byte[] continuation = image.AsSpan(offset + span.Length, site.Continuation.Length).ToArray();
        site.Verify(span, continuation, span);
        byte[] changed = (byte[])span.Clone();
        changed[0] ^= 1;
        Reject(() => site.Verify(changed, continuation, span), site.Name + " rejects changed load image");
        Reject(() => site.Verify(span, continuation, changed), site.Name + " rejects occupied live bytes");
        byte[] wrongContinuation = (byte[])continuation.Clone();
        wrongContinuation[0] ^= 1;
        Reject(() => site.Verify(span, wrongContinuation, span), site.Name + " rejects changed continuation");

        var decoder = Decoder.Create(64, new ByteArrayCodeReader(span));
        Instruction first = decoder.Decode();
        Instruction second = decoder.Decode();
        Check(first.Mnemonic == Mnemonic.Mov && first.MemoryDisplacement64 == StateOffset && first.Length == 9,
            site.Name + " main-state write");
        Check(second.Mnemonic == Mnemonic.Mov && second.MemoryDisplacement64 == VisualOffset &&
            second.Length + first.Length == span.Length, site.Name + " complete visual write");
        Check(first.MemoryBase == Register.RCX && second.MemoryBase == Register.RCX &&
            first.MemoryIndex == second.MemoryIndex &&
            first.MemoryIndex == (site.Name == "Trebuchet" ? Register.R14 : Register.R13),
            site.Name + " both writes address the selected record");
        return new[] { first, second };
    }

    private static ulong Execute(Site site, Instruction[] original, IntPtr activation, bool zeroFlag, bool useStub, int expectedCounter)
    {
        IntPtr data = Marshal.AllocHGlobal(FixtureSize);
        IntPtr output = Marshal.AllocHGlobal(sizeof(long));
        IntPtr code = VirtualAlloc(IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x40);
        try
        {
            Check(code != IntPtr.Zero, "fixture allocation");
            byte[] expected = Enumerable.Repeat((byte)0xA5, FixtureSize).ToArray();
            int timeout = UnitStride + CrewTimeoutOffset;
            Array.Copy(BitConverter.GetBytes(121), 0, expected, timeout, sizeof(int));
            Marshal.Copy(expected, 0, data, expected.Length);

            var a = new Assembler(64);
            a.push(r13);
            a.push(r14);
            a.mov(site.Manager, rcx);
            a.mov(ecx, edx);
            a.mov(r9d, 5);
            a.push(rsi);
            a.xor(esi, esi);
            if (site.Manager.Equals(r13))
                a.mov(r14d, 5);
            if (!zeroFlag)
                a.cmp(r9d, 6);
            a.mov(rax, RaxSentinel);

            if (useStub)
                EmitTimeout(a, site, unchecked((ulong)activation.ToInt64()), original);
            else
                a.AddInstructions(original);

            a.mov(__qword_ptr[r8], rax);
            a.pushfq();
            a.pop(rax);
            a.pop(rsi);
            a.pop(r14);
            a.pop(r13);
            a.ret();

            using (var stream = new MemoryStream())
            {
                a.Assemble(new StreamCodeWriter(stream), unchecked((ulong)code.ToInt64()));
                byte[] payload = stream.ToArray();
                Marshal.Copy(payload, 0, code, payload.Length);
                Check(FlushInstructionCache(GetCurrentProcess(), code, (UIntPtr)payload.Length), "fixture instruction cache");
            }

            ulong flags = Marshal.GetDelegateForFunctionPointer<RunStub>(code)(data, UnitStride, output);
            Check(unchecked((ulong)Marshal.ReadInt64(output)) == RaxSentinel, site.Name + " RAX preserved");
            Check(((flags & 0x40) != 0) == zeroFlag, site.Name + " continuation condition preserved");
            Array.Copy(BitConverter.GetBytes(expectedCounter), 0, expected, timeout, sizeof(int));
            expected[UnitStride + StateOffset] = 5;
            expected[UnitStride + StateOffset + 1] = 0;
            expected[UnitStride + VisualOffset] = 0;
            var actual = new byte[FixtureSize];
            Marshal.Copy(data, actual, 0, actual.Length);
            Check(expected.SequenceEqual(actual), site.Name + " only expected bytes changed");
            return flags;
        }
        finally
        {
            if (code != IntPtr.Zero)
                VirtualFree(code, UIntPtr.Zero, 0x8000);
            Marshal.FreeHGlobal(output);
            Marshal.FreeHGlobal(data);
        }
    }

    private static int CodeOffset(byte[] image, int rva, int length)
    {
        int pe = BitConverter.ToInt32(image, 0x3C);
        int sections = BitConverter.ToUInt16(image, pe + 6);
        int section = pe + 24 + BitConverter.ToUInt16(image, pe + 20);
        for (int index = 0; index < sections; index++, section += 40)
        {
            int delta = rva - BitConverter.ToInt32(image, section + 12);
            int rawSize = BitConverter.ToInt32(image, section + 16);
            uint flags = BitConverter.ToUInt32(image, section + 36);
            if ((flags & 0x20000000) != 0 && delta >= 0 && delta <= rawSize - length)
                return BitConverter.ToInt32(image, section + 20) + delta;
        }
        throw new InvalidOperationException($"RVA 0x{rva:X} is outside the executable PE sections.");
    }

    private static void Reject(Action action, string label)
    {
        try { action(); }
        catch (InvalidOperationException)
        {
            checks++;
            return;
        }
        throw new Exception(label);
    }

    private static void Check(bool condition, string label)
    {
        if (!condition)
            throw new Exception(label);
        checks++;
    }
}
