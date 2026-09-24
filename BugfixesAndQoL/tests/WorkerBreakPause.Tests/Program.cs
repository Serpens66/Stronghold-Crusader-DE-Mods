using BugfixesAndQoL;
using Iced.Intel;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

internal static class Program
{
    private const ulong ImageBase = 0x180000000;
    private const string ExpectedHash =
        "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 1) throw new ArgumentException("Expected installed CrusaderDE.dll path.");
            byte[] file = File.ReadAllBytes(args[0]);
            using (var sha = SHA256.Create())
                Check(BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "") == ExpectedHash,
                    "installed native hash");
            int pe = BitConverter.ToInt32(file, 0x3C);
            int table = pe + 24 + BitConverter.ToUInt16(file, pe + 20);
            int raw = -1;
            for (int i = 0; i < BitConverter.ToUInt16(file, pe + 6); i++)
            {
                int at = table + 40 * i;
                if (System.Text.Encoding.ASCII.GetString(file, at, 8).TrimEnd('\0') != ".text")
                    continue;
                Check(BitConverter.ToInt32(file, at + 12) == 0x1000, "native .text RVA");
                raw = BitConverter.ToInt32(file, at + 20);
                break;
            }
            Check(raw >= 0, "native .text exists");
            CheckSite(file, raw, 0x1383F3, 0x138467,
                "74-72-8B-15-C9-7E-7F-00-49-8B-CF-48-69-DF-2C-03-00-00");
            CheckSite(file, raw, 0x139596, 0x139651,
                "0F-84-B5-00-00-00-48-69-FD-2C-03-00-00-44-89-64-24-20");
            Console.WriteLine("Worker-break native and conditional replay contracts passed.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void CheckSite(byte[] file, int textRaw, int rva, int targetRva,
        string expectedHex)
    {
        byte[] expected = expectedHex.Split('-').Select(x => Convert.ToByte(x, 16)).ToArray();
        Check(expected.Length == 18, "hook span length");
        int offset = textRaw + rva - 0x1000;
        Check(BitConverter.ToString(file, offset, expected.Length) == expectedHex,
            $"original bytes at 0x{rva:X}");

        var decoder = Decoder.Create(64, new ByteArrayCodeReader(expected));
        decoder.IP = ImageBase + (uint)rva;
        var original = new System.Collections.Generic.List<Instruction>();
        int decoded = 0;
        while (decoded < expected.Length)
        {
            Instruction instruction = decoder.Decode();
            Check(!instruction.IsInvalid, "valid original instruction");
            original.Add(instruction);
            decoded += instruction.Length;
        }
        Check(decoded == 18 && original[0].FlowControl == FlowControl.ConditionalBranch &&
            original[0].NearBranchTarget == ImageBase + (uint)targetRva,
            "complete Vanilla branch and return span");

        var assembler = new Assembler(64);
        NativeInstructionReplayEmitter.EmitConditionalWorkerBreak(
            assembler, original.ToArray(), 0x1234567887654321, ImageBase + (uint)targetRva);
        byte[] generated;
        using (var stream = new MemoryStream())
        {
            Check(assembler.TryAssemble(new StreamCodeWriter(stream), ImageBase + (uint)rva,
                out string error, out _, BlockEncoderOptions.None), "generated assembly: " + error);
            generated = stream.ToArray();
        }
        var replayDecoder = Decoder.Create(64, new ByteArrayCodeReader(generated));
        replayDecoder.IP = ImageBase + (uint)rva;
        var emitted = new System.Collections.Generic.List<Instruction>();
        while (replayDecoder.IP < ImageBase + (uint)rva + (uint)generated.Length)
        {
            Instruction instruction = replayDecoder.Decode();
            Check(!instruction.IsInvalid, "valid generated instruction");
            emitted.Add(instruction);
        }
        Check(emitted.Count(x => x.FlowControl == FlowControl.UnconditionalBranch &&
            x.NearBranchTarget == ImageBase + (uint)targetRva) == 1,
            "enabled path jumps to Vanilla pause route");
        Check(emitted.Count(x => x.FlowControl == FlowControl.ConditionalBranch &&
            x.NearBranchTarget == ImageBase + (uint)targetRva) == 1,
            "disabled path retains original conditional branch");
        Check(emitted.Count(x => x.Mnemonic == Mnemonic.Cmp) == 1 &&
            emitted.Any(x => x.Mnemonic == Mnemonic.Je &&
                x.NearBranchTarget >= ImageBase + (uint)rva &&
                x.NearBranchTarget < ImageBase + (uint)rva + (uint)generated.Length),
            "zero flag selects the in-gateway Vanilla replay");
        Check(emitted.Any(x => x.Mnemonic == Mnemonic.Pushfq) &&
            emitted.Count(x => x.Mnemonic == Mnemonic.Popfq) == 2,
            "both paths restore original flags");
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Failed: " + label);
    }
}
