using Iced.Intel;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using WorkerBreakParityTest;

internal static class Program
{
    private const ulong PreferredImageBase = 0x180000000;

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 1)
                throw new ArgumentException("Expected the installed CrusaderDE.dll path.");
            byte[] file = File.ReadAllBytes(args[0]);
            string hash;
            using (var sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
            Check(hash == WorkerBreakNativeContract.ReferenceSha256, "native hash");

            byte[] image = MapPeImage(file);
            foreach (WorkerBreakPatchSite site in WorkerBreakNativeContract.Sites)
            {
                Check(WorkerBreakNativeContract.Resolve(image, PreferredImageBase, site) == site.Rva,
                    site.Name + " RVA and branch target");
                int signatures = CountSignature(image, site.Signature);
                Check(signatures == 1, site.Name + " unique .text signature");

                var assembler = new Assembler(64);
                WorkerBreakNativeContract.Emit(assembler, site, PreferredImageBase);
                byte[] generated;
                using (var stream = new MemoryStream())
                {
                    if (!assembler.TryAssemble(new StreamCodeWriter(stream),
                        PreferredImageBase + (uint)site.Rva, out string error, out _,
                        BlockEncoderOptions.None))
                        throw new InvalidOperationException(site.Name + " assembly: " + error);
                    generated = stream.ToArray();
                }
                Check(generated.SequenceEqual(site.Replacement), site.Name + " replacement bytes");

                byte old = image[site.Rva];
                image[site.Rva] = 0xCC;
                ExpectFailure(() => WorkerBreakNativeContract.Resolve(image, PreferredImageBase, site),
                    site.Name + " changed bytes");
                image[site.Rva] = old;
            }
            Console.WriteLine("WorkerBreakParityTest native contracts passed.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static byte[] MapPeImage(byte[] file)
    {
        int pe = BitConverter.ToInt32(file, 0x3C);
        int sectionCount = BitConverter.ToUInt16(file, pe + 6);
        int sectionTable = pe + 24 + BitConverter.ToUInt16(file, pe + 20);
        var image = new byte[WorkerBreakNativeContract.TextEnd];
        for (int i = 0; i < sectionCount; i++)
        {
            int at = sectionTable + 40 * i;
            string name = System.Text.Encoding.ASCII.GetString(file, at, 8).TrimEnd('\0');
            if (name != ".text") continue;
            int rawSize = BitConverter.ToInt32(file, at + 16);
            int virtualAddress = BitConverter.ToInt32(file, at + 12);
            int rawPointer = BitConverter.ToInt32(file, at + 20);
            Check(virtualAddress == WorkerBreakNativeContract.TextStart &&
                virtualAddress + rawSize >= WorkerBreakNativeContract.TextEnd,
                ".text section layout");
            Buffer.BlockCopy(file, rawPointer, image, virtualAddress,
                WorkerBreakNativeContract.TextEnd - virtualAddress);
            return image;
        }
        throw new InvalidOperationException(".text was not found.");
    }

    private static int CountSignature(byte[] image, byte[] signature)
    {
        int count = 0;
        for (int rva = WorkerBreakNativeContract.TextStart;
            rva <= WorkerBreakNativeContract.TextEnd - signature.Length; rva++)
            if (new ReadOnlySpan<byte>(image, rva, signature.Length).SequenceEqual(signature)) count++;
        return count;
    }

    private static void ExpectFailure(Action action, string label)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException(label + " did not fail closed.");
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Failed: " + label);
    }
}
