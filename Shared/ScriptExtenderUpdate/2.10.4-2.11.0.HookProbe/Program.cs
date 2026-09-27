using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Iced.Intel;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using static Iced.Intel.AssemblerRegisters;

const int hookRva = 0xFEF21;
const int displacedLength = 15;
const string nativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";

string gameDir = Environment.GetEnvironmentVariable("SHCDE_GAME_DIR") ??
    @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
string nativePath = Path.Combine(gameDir,
    @"Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll");
byte[] file = File.ReadAllBytes(nativePath);
Check(Convert.ToHexString(SHA256.HashData(file)) == nativeHash, "Native hash changed.");

using var peStream = new MemoryStream(file, writable: false);
using var pe = new PEReader(peStream);
var section = pe.PEHeaders.SectionHeaders.Single(s =>
    hookRva >= s.VirtualAddress && hookRva + 64 <= s.VirtualAddress + s.SizeOfRawData);
int fileOffset = section.PointerToRawData + hookRva - section.VirtualAddress;
byte[] native = file.AsSpan(fileOffset, 64).ToArray();
Check(native[0] == 0x48 && native[1] == 0x8D && native[5] == 0xE8 &&
      native[10] == 0x48 && native[11] == 0x8D,
    "Vanilla instruction prefix changed.");

// Relocate the copied CALL to a safe RET in this scratch buffer. The real
// target address is checked separately by the hash-bound native audit.
IntPtr copy = Marshal.AllocHGlobal(64);
IntPtr pathBuffer = Marshal.AllocHGlobal(256);
try
{
    Marshal.Copy(native, 0, copy, native.Length);
    Marshal.WriteByte(copy, 32, 0xC3);
    Marshal.WriteInt32(copy, 6, 32 - 10);
    ulong target = unchecked((ulong)copy.ToInt64());
    using var hook = new X64InlineHook(target, displacedLength);
    Check(hook.DisplacedByteCount == displacedLength,
        $"RedBird displaced {hook.DisplacedByteCount} bytes, expected 15.");

    int generatorCalls = 0;
    hook.Generate((asm, original, returnAddress) =>
    {
        generatorCalls++;
        Check(original.Length == 3 && original[0].Length == 5 &&
              original[1].Length == 5 && original[2].Length == 5,
            "RedBird did not decode exactly the three expected instructions.");
        Check(original[1].Mnemonic == Mnemonic.Call &&
              original[1].NearBranchTarget == target + 32,
            "The copied Vanilla call target is wrong.");
        Check(returnAddress == target + displacedLength,
            "The inline continuation address is wrong.");
        asm.mov(rcx, unchecked((ulong)pathBuffer.ToInt64()));
        asm.AddInstruction(original[1]);
        asm.mov(rdx, unchecked((ulong)pathBuffer.ToInt64()));
    });
    Check(generatorCalls == 1 && hook.StubAddress != IntPtr.Zero,
        "RedBird did not generate the replacement stub.");

    byte[] stub = new byte[64];
    Marshal.Copy(hook.StubAddress, stub, 0, stub.Length);
    var decoder = Decoder.Create(64, new ByteArrayCodeReader(stub),
        unchecked((ulong)hook.StubAddress.ToInt64()));
    Instruction first = decoder.Decode();
    Instruction second = decoder.Decode();
    Instruction third = decoder.Decode();
    Check(first.Mnemonic == Mnemonic.Mov && first.Op0Register == Register.RCX &&
          first.Immediate64 == unchecked((ulong)pathBuffer.ToInt64()),
        "Generated stub does not set the destination buffer in RCX.");
    Check(second.Mnemonic == Mnemonic.Call && second.NearBranchTarget == target + 32,
        "Generated stub does not call the relocated original target.");
    Check(third.Mnemonic == Mnemonic.Mov && third.Op0Register == Register.RDX &&
          third.Immediate64 == unchecked((ulong)pathBuffer.ToInt64()),
        "Generated stub does not pass the same buffer to the sound manager.");
    Console.WriteLine("PASS: installed RedBird decoded 15 bytes and assembled the copied CALL, buffer registers, and continuation.");
}
finally
{
    Marshal.FreeHGlobal(pathBuffer);
    Marshal.FreeHGlobal(copy);
}

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
