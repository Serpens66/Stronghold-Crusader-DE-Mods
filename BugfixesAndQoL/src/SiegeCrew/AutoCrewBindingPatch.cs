using Iced.Intel;
using RedBird.X64.Extensions;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL.SiegeCrew
{
    internal static class AutoCrewBindingPatch
    {
        internal static readonly NativeContract.Site[] Sites = {
            new NativeContract.Site("catapult crew candidate", 0x15258A, "664183BBEEFDFFFF1E0F85E8000000", "490FBF87EE060000", r11),
            new NativeContract.Site("trebuchet crew candidate", 0x153AFA, "664183BBEEFDFFFF1E0F85CD000000", "664183BBFC00000010", r11),
            new NativeContract.Site("mangonel crew candidate", 0x154DBC, "664383BC01E60600001E0F85C8000000", "664383BC01F409000010", r8),
            new NativeContract.Site("siege tower crew candidate", 0x15F278, "664183BB0E030000100F85C3000000", "664183BB1202000000", r11),
            new NativeContract.Site("battering ram crew candidate", 0x16082A, "664183BBEEFDFFFF1E0F85CE000000", "664183BBFC00000010", r11),
            new NativeContract.Site("portable shield crew candidate", 0x161549, "664183BBEEFDFFFF1E0F85C3000000", "664183BBFC00000010", r11),
            new NativeContract.Site("ballista crew candidate", 0x16221C, "664383BC01E60600001E0F85C9000000", "664383BC01F409000010", r8),
            new NativeContract.Site("fire ballista crew candidate", 0x17224A, "664183BBEEFDFFFF1E0F85EC000000", "490FBF86EE060000", r11)
        };
        internal static readonly int[] RejectRvas = { 0x152681, 0x153BD6, 0x154E94, 0x15F34A, 0x160907, 0x16161B, 0x1622F5, 0x172345 };

        internal static void Emit(Assembler a, NativeContract.Site site, ulong activation, ulong image, System.ReadOnlySpan<Instruction> original)
        {
            int index = System.Array.IndexOf(Sites, site);
            if (index < 0) throw new System.ArgumentException("Unknown automatic crew site");
            var resume = a.CreateLabel(); var reject = a.CreateLabel();
            // Preserve the fast original type/command rejection before paying
            // for the added guard. Most scanned units are not candidates.
            a.AddInstructions(original);
            a.pushfq(); a.push(rax);
            a.mov(rax, activation); a.cmp(__dword_ptr[rax], 0); a.je(resume);
            // R11 walks an interior field of each candidate; R8+R9 is the
            // original manager/slot pair in the two own-owner search loops.
            if (index == 2 || index == 6) a.lea(rax, __[r8 + r9]);
            else a.lea(rax, __[r11 - (index == 3 ? 0x6E6 : 0x8F8)]);
            a.cmp(__word_ptr[rax + 0x9CA], 0); a.jne(reject);
            a.cmp(__word_ptr[rax + 0x918], 5); a.je(reject);
            a.cmp(__word_ptr[rax + 0x918], 7); a.je(reject);
            a.cmp(__dword_ptr[rax + 0x918], 0x0005006D); a.je(reject);
            a.cmp(__dword_ptr[rax + 0x918], 0x0007006D); a.je(reject);
            // State8 may still be approaching a tent that has already completed;
            // Vanilla intentionally lets an empty device rescue that late worker.
            a.jmp(resume);
            a.Label(ref reject); a.pop(rax); a.popfq();
            a.AddUnrestrictedJmp(image + (uint)RejectRvas[index]);
            a.Label(ref resume); a.pop(rax); a.popfq();
        }
    }
}
