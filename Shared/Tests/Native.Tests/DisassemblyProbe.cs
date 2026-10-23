using Iced.Intel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;

[TestClass]
public sealed class DisassemblyProbe
{
    // Audit helper only: writes the disassembly of selected functions of the installed CrusaderDE.dll.
    // Set SERP_DISASM="rva:length;rva:length" and SERP_DISASM_OUT=<file>.
    [TestMethod]
    public void DumpRequestedFunctions()
    {
        string request = Environment.GetEnvironmentVariable("SERP_DISASM");
        string output = Environment.GetEnvironmentVariable("SERP_DISASM_OUT");
        if (string.IsNullOrEmpty(request) || string.IsNullOrEmpty(output)) { Assert.Inconclusive("No disassembly requested."); return; }
        byte[] image = MapInstalledDll();
        var text = new StringBuilder();
        foreach (string item in request.Split(';'))
        {
            string[] parts = item.Split(':');
            int rva = Convert.ToInt32(parts[0], 16), length = Convert.ToInt32(parts[1], 16);
            text.AppendLine("== 0x" + rva.ToString("X") + " length 0x" + length.ToString("X"));
            var decoder = Iced.Intel.Decoder.Create(64, new ByteArrayCodeReader(image, rva, length));
            decoder.IP = (ulong)rva;
            var formatter = new NasmFormatter();
            formatter.Options.RipRelativeAddresses = false;
            var sink = new StringOutput();
            while (decoder.IP < (ulong)(rva + length))
            {
                decoder.Decode(out Instruction instruction);
                formatter.Format(instruction, sink);
                string bytes = BitConverter.ToString(image, (int)instruction.IP, instruction.Length).Replace("-", " ");
                string rip = instruction.IsIPRelativeMemoryOperand ? "  ; rip->0x" + instruction.IPRelativeMemoryAddress.ToString("X") : "";
                string branch = instruction.IsCallNear || instruction.IsJmpNear || instruction.IsJccShortOrNear ? "  ; ->0x" + instruction.NearBranchTarget.ToString("X") : "";
                text.AppendLine(instruction.IP.ToString("X6") + "  " + bytes.PadRight(36) + sink.ToStringAndReset() + rip + branch);
            }
        }
        File.WriteAllText(output, text.ToString());
    }

    internal static byte[] MapInstalledDll()
    {
        string dll = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
        byte[] raw = File.ReadAllBytes(dll);
        int pe = BitConverter.ToInt32(raw, 0x3C);
        int count = BitConverter.ToUInt16(raw, pe + 6), optional = BitConverter.ToUInt16(raw, pe + 20);
        int sizeOfImage = BitConverter.ToInt32(raw, pe + 24 + 56), headers = BitConverter.ToInt32(raw, pe + 24 + 60);
        var image = new byte[sizeOfImage];
        Array.Copy(raw, image, headers);
        for (int i = 0; i < count; i++)
        {
            int h = pe + 24 + optional + i * 40;
            int va = BitConverter.ToInt32(raw, h + 12), size = BitConverter.ToInt32(raw, h + 16), ptr = BitConverter.ToInt32(raw, h + 20);
            Array.Copy(raw, ptr, image, va, Math.Min(size, image.Length - va));
        }
        return image;
    }
}
