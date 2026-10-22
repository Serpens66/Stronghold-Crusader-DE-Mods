using System;
using System.Collections.Generic;
using System.Linq;
using Iced.Intel;

namespace BugfixesAndQoL
{
    // Hash-bound grouped spans from the complete MODSETTINGS_ACTIVATION audit. The literals
    // cover every byte copied to either side block, not just the old two-byte patches.
    internal static class NativeSwitchCatalog
    {
        private sealed class Definition
        {
            internal Definition(int rva, int physical, string hex, int entry, string following)
            { Rva=rva; Physical=physical; Bytes=Enumerable.Range(0,hex.Length/2).Select(i=>Convert.ToByte(hex.Substring(i*2,2),16)).ToArray(); Entry=entry; Following=Enumerable.Range(0,following.Length/2).Select(i=>Convert.ToByte(following.Substring(i*2,2),16)).ToArray(); }
            internal readonly int Rva, Physical, Entry;
            internal readonly byte[] Bytes, Following;
        }
        private static readonly Definition[] Definitions =
        {
            new Definition(0x105520, 7, "741F83F863741A", 0, "833D46A46E0300"),
            new Definition(0x119050, 16, "4053565741544883EC688B05C8CE5403", 1, "33DB"),
            new Definition(0x124BB4, 6, "0F859C020000", 0, "48895C2460"),
            new Definition(0x1256C3, 6, "0F85DE020000", 0, "48895C2460"),
            new Definition(0x1259F3, 6, "0F85F2020000", 0, "8B0529055403"),
            new Definition(0x156EAB, 6, "0F8599000000", 0, "664239BC2B2C0A0000"),
            new Definition(0x17F3E0, 6, "740485C07527", 0, "460FBF8C291E070000"),
            new Definition(0x1867A0, 18, "48895C24084863C2488BD94C69D890040000", 2, "4C03D9"),
            new Definition(0x1868B2, 6, "740485C075D6", 0, "6683E92D"),
            new Definition(0x186976, 6, "743585C07431", 0, "B801000000"),
            new Definition(0x18699A, 6, "8B05F0E13E08", 0, "83F863"),
            new Definition(0x8A141, 7, "44393D48AA4E08744E", 0, "744E"),
            new Definition(0x8D360, 6, "39352A784E08740F", 0, "740F"),
            new Definition(0x8D7BD, 7, "833DCC734E08007409", 0, "7409"),
            new Definition(0x8E05E, 7, "740585C00F45DF", 0, "448B44244C"),
            new Definition(0x8E0D7, 7, "833DB26A4E08007409", 0, "7409"),
            new Definition(0x8E5F8, 7, "833D91654E08007409", 0, "7409"),
            new Definition(0x8EA59, 7, "740585C00F45FB", 0, "4963C1"),
            new Definition(0x8EC9E, 7, "740585C00F45DD", 0, "4963C1"),
            new Definition(0xABC88, 6, "74204183FF63741A", 0, "741A"),
            new Definition(0xC70BA, 9, "740D803D19B27A0300", 0, "0F858A010000"),
            new Definition(0xCA904, 7, "0FAF15F5207208", 0, "8B0D47B65903"),
            new Definition(0xCDD81, 6, "740485C07545", 0, "458B8850390000"),
            new Definition(0xCE2FD, 7, "833D8C684A08007408", 0, "7408"),
            new Definition(0xFFD94, 6, "4183FA637431", 0, "833DC3FB6E0300"),
        };

        internal static NativeSwitchPlan[] CreatePlans(ReadOnlySpan<byte> memory, ulong imageBase, bool noDogs)
        {
            var result=new List<NativeSwitchPlan>();
            int patches=0;
            foreach (Definition d in Definitions)
            {
                if ((d.Rva==0xFFD94)!=noDogs) continue;
                if (d.Rva<0 || d.Rva>memory.Length-d.Bytes.Length ||
                    !memory.Slice(d.Rva,d.Bytes.Length).SequenceEqual(d.Bytes))
                    throw new InvalidOperationException("Native switch original block differs at RVA 0x"+d.Rva.ToString("X"));
                if (!memory.Slice(d.Rva+d.Physical,d.Following.Length).SequenceEqual(d.Following))
                    throw new InvalidOperationException("Native switch following instruction differs.");
                byte[] active=(byte[])d.Bytes.Clone();
                if (noDogs) Buffer.BlockCopy(NoDogsNativeContract.ReplacementBytes,0,active,NoDogsNativeContract.PatchRva-d.Rva,2);
                else foreach (VanillaPeaceTimePatchSite site in VanillaPeaceTimeNativeContract.PatchSites)
                {
                    if (site.Rva<d.Rva || site.Rva+site.ExpectedBytes.Length>d.Rva+active.Length) continue;
                    var a=new Assembler(64);VanillaPeaceTimeNativeContract.EmitPatch(a,site,imageBase);
                    byte[] replacement=PermanentNativeSwitch.Assemble(a,imageBase+(uint)site.Rva);
                    if (replacement.Length!=site.ExpectedBytes.Length) throw new InvalidOperationException("Native replacement size changed.");
                    Buffer.BlockCopy(replacement,0,active,site.Rva-d.Rva,replacement.Length);patches++;
                }
                result.Add(new NativeSwitchPlan(d.Rva,d.Physical,d.Bytes,active,d.Entry,d.Following));
            }
            if (result.Count!=(noDogs?1:24) || !noDogs && patches!=VanillaPeaceTimeNativeContract.PatchSites.Length)
                throw new InvalidOperationException("Incomplete native logical switch group.");
            return result.ToArray();
        }
    }
}
