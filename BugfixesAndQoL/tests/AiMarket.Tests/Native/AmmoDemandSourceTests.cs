using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using BugfixesAndQoL.AI.Market;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

internal static unsafe class AmmoDemandSourceTests
{
    internal static void Run(byte[] dll,Action<bool,string>check)
    {
        const ulong module=0x180000000;
        int[] returns={0x153057,0x153156,0x1542C6,0x1543C7,0x18FD8A};
        var image=new byte[0x18FDA0];
        Copy(dll,image,0xD5710,10);Copy(dll,image,0xD572B,3);
        foreach(int ret in returns)Copy(dll,image,ret-5,5);
        MarketAmmoDemandSource.Initialize(image,module);
        foreach(string name in new[]{nameof(GameUnit.r_AliveState),nameof(GameUnit.r_ControllableForPlayerId),
            nameof(GameUnit.r_GlobalId),nameof(GameUnit.r_CreationTickStamp),nameof(GameUnit.r_IsKilledByProjectile),nameof(GameUnit.r_StoneAmmoLeft),nameof(GameUnit.r_StoneAmmoStacksLeft)})
            check(typeof(GameUnit).GetField(name,BindingFlags.Public|BindingFlags.Instance)!=null,"ammo: actual installed SDK member is public: "+name);
        GameUnit unit=default;unit.r_AliveState=AliveState.IsAlive;unit.r_ControllableForPlayerId=2;
        unit.r_GlobalId=81;unit.r_CreationTickStamp=104;unit.r_StoneAmmoLeft=0;unit.r_StoneAmmoStacksLeft=1;
        var state=MarketAmmoDemandSource.ReadUnit(&unit);
        check(state.Ammo==256,"ammo: production reader includes high SDK ammo byte, not only low byte");
        unit.r_StoneAmmoStacksLeft=0;state=MarketAmmoDemandSource.ReadUnit(&unit);
        check(state.Ammo==0&&state.Owner==2&&state.GlobalId==81&&state.CreationTick==104&&state.Alive==AliveState.IsAlive,
            "ammo: production reader preserves actual public unit identity and signed word");
        check(MarketAmmoDemandSource.TryCreate(2,7,9,state,out var origin),"ammo: validated empty-origin with nine stone");
        check(MarketAmmoDemandSource.Desired(origin,2,9,state,out int wanted)==MarketAmmoDemandSource.Result.Live&&wanted==1,
            "ammo: atomic ten-stone reload missing exactly one");
        state.Ammo=18;
        check(MarketAmmoDemandSource.Desired(origin,2,0,state,out wanted)==MarketAmmoDemandSource.Result.Ended&&wanted==0,
            "ammo: manual partial nine-stone refill cancels empty trigger instead of buying unusable one stone");
        state.Ammo=0;
        check(MarketAmmoDemandSource.Desired(origin,2,1,state,out wanted)==MarketAmmoDemandSource.Result.Live&&wanted==9,
            "ammo: newly empty native reload still requires atomic ten-stone stock");
        check(MarketAmmoDemandSource.Desired(origin,2,10,state,out wanted)==MarketAmmoDemandSource.Result.Live&&wanted==0,
            "ammo: full stock covers atomic refill");
        var next=origin;check(origin.SameIdentity(next),"ammo: origin identity compares without boxing");
        next.GlobalId++;check(!origin.SameIdentity(next),"ammo: recycled slot global ID changes origin");
        var altered=state;altered.GlobalId++;
        check(MarketAmmoDemandSource.Desired(origin,2,0,altered,out wanted)==MarketAmmoDemandSource.Result.Ended&&wanted==0,"ammo: reused slot ends original unit request");
        altered=state;altered.CreationTick++;
        check(MarketAmmoDemandSource.Desired(origin,2,0,altered,out wanted)==MarketAmmoDemandSource.Result.Ended,"ammo: creation epoch also detects reuse");
        altered=state;altered.Owner=3;
        check(MarketAmmoDemandSource.Desired(origin,2,0,altered,out wanted)==MarketAmmoDemandSource.Result.Ended,"ammo: changed unit owner ends old player's goal");
        altered=state;altered.Alive=AliveState.MarkedForDeletion;
        check(MarketAmmoDemandSource.Desired(origin,2,0,altered,out wanted)==MarketAmmoDemandSource.Result.Ended,"ammo: dead/deleting origin ends goal");
        altered=state;altered.DeathMarker=1;
        check(MarketAmmoDemandSource.Desired(origin,2,0,altered,out wanted)==MarketAmmoDemandSource.Result.Ended,"ammo: dying/corpse marker ends goal even while AliveState remains alive");
        check(MarketAmmoDemandSource.Desired(origin,2,-1,state,out wanted)==MarketAmmoDemandSource.Result.Unknown&&
            MarketAmmoDemandSource.Desired(origin,3,0,state,out wanted)==MarketAmmoDemandSource.Result.Unknown,
            "ammo: invalid stock or wrong request player authorizes no write");
        altered=state;altered.Owner=0;
        check(!MarketAmmoDemandSource.TryCreate(0,7,0,altered,out _)&&!MarketAmmoDemandSource.TryCreate(2,7,0,altered,out _),"ammo: neutral owner never becomes a source");
        altered=state;altered.Ammo=1;
        check(!MarketAmmoDemandSource.TryCreate(2,7,0,altered,out _),"ammo: nonempty/manual-like origin unsupported");
        ulong* frame=stackalloc ulong[32];for(int i=0;i<32;i++)frame[i]=0;
        frame[MarketDemandWriterHooks.Rbx/8]=7;
        foreach(int ret in returns)
        {
            frame[(MarketDemandWriterHooks.SnapshotBytes+0x38)/8]=module+(uint)ret;
            check(MarketAmmoDemandSource.TryReadFrame((IntPtr)frame,2,9,module,out int unitId)&&unitId==7,"ammo: actual automatic caller whitelist and original stack return "+ret.ToString("X"));
        }
        frame[(MarketDemandWriterHooks.SnapshotBytes+0x38)/8]=module+0x18BB5;
        check(!MarketAmmoDemandSource.TryReadFrame((IntPtr)frame,2,9,module,out _),"ammo: manual tail-jump location is not an automatic return");
        frame[(MarketDemandWriterHooks.SnapshotBytes+0x38)/8]=module+0x153057;
        frame[MarketDemandWriterHooks.Rbx/8]=0;
        check(!MarketAmmoDemandSource.TryReadFrame((IntPtr)frame,2,9,module,out _),"ammo: unit sentinel zero rejected before SDK lookup");
        frame[MarketDemandWriterHooks.Rbx/8]=7;
        check(!MarketAmmoDemandSource.TryReadFrame((IntPtr)frame,2,10,module,out _),"ammo: source stock must match native insufficient-stock branch");
        image[returns[0]-4]^=1;bool rejected=false;
        try{MarketAmmoDemandSource.Initialize(image,module);}catch(InvalidOperationException){rejected=true;}
        check(rejected&&!MarketAmmoDemandSource.TryReadFrame((IntPtr)frame,2,9,module,out _),"ammo: changed call target fails closed and disables capture");
        image[returns[0]-4]^=1;MarketAmmoDemandSource.Initialize(image,module);
    }
    private static void Copy(byte[]dll,byte[]image,int rva,int length)=>Buffer.BlockCopy(dll,FileOffset(dll,rva),image,rva,length);
    private static int FileOffset(byte[]dll,int rva)
    {int pe=BitConverter.ToInt32(dll,0x3C),sections=BitConverter.ToUInt16(dll,pe+6),table=pe+24+BitConverter.ToUInt16(dll,pe+20);for(int i=0;i<sections;i++){int s=table+i*40,va=BitConverter.ToInt32(dll,s+12),size=BitConverter.ToInt32(dll,s+16);if(rva>=va&&rva<va+size)return rva-va+BitConverter.ToInt32(dll,s+20);}throw new Exception("Ammo RVA is not in a file section");}
}
