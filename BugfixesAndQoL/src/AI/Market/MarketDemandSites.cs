using System;

namespace BugfixesAndQoL.AI.Market
{
    // Single MOV spans. Register slots refer to the relay's saved-register frame,
    // not Windows argument registers. Full source/edge audit: Findings/AllGoods*.
    internal sealed class MarketDemandSite
    {
        internal readonly int Rva, Base, Index, Scale, Displacement, Value;
        internal readonly byte[] Bytes;
        internal readonly bool Immediate;
        internal MarketDemandSite(int rva, string hex, int baseSlot, int indexSlot, int scale,
            int displacement, int value, bool immediate = false)
        {
            Rva = rva; Base = baseSlot; Index = indexSlot; Scale = scale;
            Displacement = displacement; Value = value; Immediate = immediate;
            Bytes = new byte[hex.Length / 2];
            for (int i = 0; i < Bytes.Length; i++) Bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        internal unsafe ulong Cell(IntPtr frame)
        {
            ulong* r = (ulong*)frame;
            return r[Base] + (Index < 0 ? 0 : r[Index] * (uint)Scale) + (uint)Displacement;
        }
        internal unsafe int Amount(IntPtr frame) => Immediate ? Value : unchecked((int)((ulong*)frame)[Value]);
    }

    internal static class MarketDemandSites
    {
        // r15..r8 slots0..7, rdi8,rsi9,rbp10,rbx11,rdx12,rcx13,rax14.
        internal static readonly MarketDemandSite[] All = {
            new MarketDemandSite(0x2A464,"4389840864161300",7,6,1,0x131664,14),
            new MarketDemandSite(0x2A488,"438984085C161300",7,6,1,0x13165C,14),
            new MarketDemandSite(0x2A4AC,"4389840858161300",7,6,1,0x131658,14),
            new MarketDemandSite(0x2A4D0,"4389840854161300",7,6,1,0x131654,14),
            new MarketDemandSite(0x2A4F4,"438984083C161300",7,6,1,0x13163C,14),
            new MarketDemandSite(0x403A7,"4189849530161300",2,12,4,0x131630,14),
            new MarketDemandSite(0x40597,"4189849530161300",2,12,4,0x131630,14),
            new MarketDemandSite(0x40D3C,"C7848240D8790305000000",12,14,4,0x379D840,5,true),
            new MarketDemandSite(0x40D62,"89848A40D87903",12,13,4,0x379D840,14),
            new MarketDemandSite(0xCC555,"4489A348161300",11,-1,1,0x131648,3),
            new MarketDemandSite(0xCC5C1,"4489AB4C161300",11,-1,1,0x13164C,2),
            new MarketDemandSite(0xCC657,"89BB38161300",11,-1,1,0x131638,8),
            new MarketDemandSite(0xCC692,"89BB38161300",11,-1,1,0x131638,8),
            new MarketDemandSite(0xCC6E4,"89B340161300",11,-1,1,0x131640,9),
            new MarketDemandSite(0x2ADB5,"42C784333816130005000000",11,1,1,0x131638,5,true),
            new MarketDemandSite(0x2ADD7,"42C784334816130002000000",11,1,1,0x131648,2,true),
            new MarketDemandSite(0x2ADF9,"42C784337016130002000000",11,1,1,0x131670,2,true),
            new MarketDemandSite(0x2AE1B,"42C784333C16130002000000",11,1,1,0x13163C,2,true),
            new MarketDemandSite(0x2EB9C,"43C784205CD8790304000000",7,3,1,0x379D85C,4,true),
            new MarketDemandSite(0x2F56E,"C784283816130014000000",14,10,1,0x131638,20,true),
            new MarketDemandSite(0x51ADB,"42C784114016130005000000",13,5,1,0x131640,5,true),
            new MarketDemandSite(0xD5791,"41C7843850D879030A000000",7,8,1,0x379D850,10,true)
        };
    }
}
