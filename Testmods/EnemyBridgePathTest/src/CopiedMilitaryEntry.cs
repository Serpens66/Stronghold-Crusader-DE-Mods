using System;

namespace EnemyBridgePathTest
{
    // Private copied memory only. This is the 3CF20 -> 126020 side effect,
    // not a substitute for the whole military entry or any movement search.
    internal static class CopiedMilitaryEntry
    {
        internal static void Validate(BridgePlanningCapture.Bundle b)
        {
            byte[] Get(string key,int length)
            {var v=CopiedPlanningBundle.Resolve(b,"military/"+key);if(v.Length!=length)throw new ArgumentException("entry-extent:"+key);return v;}
            if(BitConverter.ToInt32(Get("entryInputVersion",4),0)!=1)throw new ArgumentException("entry-version");
            Get("entryUnitManagerFull",BridgePlanningCapture.EntryUnitBytes);
            Get("entryTribesFull",BridgePlanningCapture.EntryTribeBytes);
            var players=Get("entryPlayers9",9*0x583c);var own=Get("entryPlayer",0x583c);
            Get("entryQuota",4);Get("entryEngineerCounters2",8);
            bool Has(string key)=>b.Sections.ContainsKey("military/"+key)||b.References.ContainsKey("military/"+key);
            if(Has("entryLordConfigRow")||Has("entryPhase5Controls"))
            {
                var row=Get("entryLordConfigRow",0x5e4);var phase=Get("entryPhase5Controls",8);var config=Get("entryConfig5",20);
                if(BitConverter.ToInt32(row,0x20c)!=BitConverter.ToInt32(config,4)||BitConverter.ToInt32(row,0x2ec)!=BitConverter.ToInt32(config,8)||row[0x2f4]!=BitConverter.ToInt32(config,12)||BitConverter.ToInt32(phase,4)!=BitConverter.ToInt32(config,16))throw new ArgumentException("entry-lord-controls-disagree");
            }
            var identity=Get("entryIdentity4",32);
            if(b.Attacker<1||b.Attacker>8||BitConverter.ToInt64(identity,0)!=b.Session||
               BitConverter.ToInt64(identity,8)!=b.Root||BitConverter.ToInt64(identity,16)<=0||BitConverter.ToInt64(identity,16)>b.Clock||
               BitConverter.ToInt64(identity,24)!=b.Thread)throw new ArgumentException("entry-source-identity");
            for(int i=0;i<own.Length;i++)if(own[i]!=players[b.Attacker*0x583c+i])throw new ArgumentException("entry-own-player-mismatch");
        }
        internal static int Member(byte[] tribes,int tribe,int ordinal)
        {
            if(tribes.Length!=BridgePlanningCapture.EntryTribeBytes||tribe<1||tribe>=4500)throw new ArgumentException("tribe-extent");
            int at=tribe*0x688;
            if(BitConverter.ToInt16(tribes,at+0x40)!=2||ordinal<0||ordinal>=BitConverter.ToInt16(tribes,at+0x5c))return 0;
            int seen=0;
            for(int word=0;word<0x271;word++)
            {
                ushort bits=BitConverter.ToUInt16(tribes,at+0x60+word*2);
                for(int bit=0;bit<16;bit++)if((bits&(1<<bit))!=0&&seen++==ordinal)return word*16+bit;
            }
            return 0; // Matches the native sparse/count mismatch result.
        }
        internal static void ResetMembership(byte[] player,int[] classes,byte[] tribes,byte[] units)
        {
            if(player.Length!=0x583c||classes.Length!=22||tribes.Length!=BridgePlanningCapture.EntryTribeBytes||units.Length!=BridgePlanningCapture.EntryUnitBytes)throw new ArgumentException("membership-input-extent");
            if(BitConverter.ToInt32(player,0x22d0)==0)return;
            for(int row=0;row<11;row++)
            {
                int start=classes[row*2],count=classes[row*2+1];if(count<=0)continue;
                if(start<0||count>256||start>256-count)throw new ArgumentException("entry-class-bounds");
                for(int n=0;n<count;n++)
                {
                    int slot=start+n,id=BitConverter.ToInt16(player,0x4870+slot*2);if(id==0)continue;
                    if(id<0||id>=4500)throw new ArgumentException("entry-tribe-id");
                    if(BitConverter.ToInt32(tribes,id*0x688+0x34)!=BitConverter.ToInt32(player,0x4ac8+slot*4))continue;
                    int members=BitConverter.ToInt16(tribes,id*0x688+0x5c);
                    for(int member=0;member<members;member++)
                    {
                        int unit=Member(tribes,id,member),at=unit*0x490;
                        if(unit<0||unit>=10000)throw new ArgumentException("entry-member-id");
                        if(BitConverter.ToInt16(units,at+0x6e4)==2&&BitConverter.ToInt16(units,at+0x8f8)==0)
                        {units[at+0xa84]=0;units[at+0xa85]=0;}
                    }
                }
            }
        }
    }
}
