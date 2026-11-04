using System;

namespace BugfixesAndQoL.UnitCommands
{
    // 11B520's member check after its admission flood (0x124740), answered in one pass. 0x124740 fetches
    // member i through 0x119F90, which scans the tribe's member bitset (625 words from +0x60) from its start
    // every time, so one check is quadratic in the group size; 11B520 repeats it after every widened flood
    // (budget +5,000 up to 320,800; Vanilla audit 30.10.2026, MANUAL_GROUP_ADMISSION.md). Its result depends
    // only on the bitset, the tribe kind (+0x40 == 2, otherwise every member is unit slot 0), the group size
    // (+0x5C, signed) and three unit fields against the flood stamps; this pass reads exactly those in the
    // same member order and returns at the same first failing member. FBCB9319 only: both bodies are
    // hash-bound by the caller.
    internal static unsafe class TribeFloodMembership
    {
        internal const int MembershipRva = 0x124740, MembershipBodyLength = 0xAC, GroupUnitRva = 0x119F90, GroupUnitBodyLength = 0x8C;
        internal const string MembershipBodySha256 = "FFB294BD9F96BE36B85A9D7D3031746B843F7D1D1F98148E169003E449C9817C",
            GroupUnitBodySha256 = "CD5DAE0B455BA6CBFC20D0193849591A8250C710C8B77BBDCFFF6908FBD1544F";
        internal const int TribeStride = 0x688, UnitStride = 0x490;

        internal static bool BodyMatches(ReadOnlySpan<byte> memory, int rva, int length, string sha256)
        {
            if (rva < 0 || length <= 0 || rva > memory.Length - length) return false;
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(memory.Slice(rva, length).ToArray())).Replace("-", "") == sha256;
        }

        // tribe: the tribe record (manager + id * 0x688); units: the unit manager; stamps: 0x52C2550.
        internal static int Check(byte* tribe, byte* units, short* stamps, int stamp)
        {
            int count = *(short*)(tribe + 0x5C);
            if (count <= 0) return 1;
            int member = 0;
            if (*(short*)(tribe + 0x40) == 2)
            {
                short* words = (short*)(tribe + 0x60);
                for (int word = 0; word < 0x271 && member < count; word++)
                {
                    int bits = words[word];
                    if (bits == 0) continue;
                    for (int bit = 0; bit < 16 && member < count; bit++)
                    {
                        if ((bits >> bit & 1) == 0) continue;
                        if (!Reached(units, stamps, word * 16 + bit, stamp)) return 0;
                        member++;
                    }
                }
            }
            // 0x119F90 answers unit slot 0 beyond the set bits and for other tribe kinds.
            for (; member < count; member++)
                if (!Reached(units, stamps, 0, stamp)) return 0;
            return 1;
        }

        // 0x124740's member test: an active (+0x6E4 == 2), non-excluded (+0x8F8 == 0) member must stand on a
        // tile the flood stamped (tile id at +0x72C, signed stamp words).
        private static bool Reached(byte* units, short* stamps, int unit, int stamp)
        {
            byte* record = units + (long)unit * UnitStride;
            return *(short*)(record + 0x6E4) != 2 || *(short*)(record + 0x8F8) != 0 || stamps[*(int*)(record + 0x72C)] == stamp;
        }
    }
}
