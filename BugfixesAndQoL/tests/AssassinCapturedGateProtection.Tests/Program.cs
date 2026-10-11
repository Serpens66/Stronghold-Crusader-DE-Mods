using System;
using System.Runtime.InteropServices;
using APIShared;
using BugfixesAndQoL;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
internal static unsafe class Program
{
    private static int assertions;
    private static void Check(bool value,string name) { assertions++; if(!value) throw new Exception(name); }
    private static int Main()
    {
        try { PolicyTests(); CompletionTests(); NativeTests(); Console.WriteLine("PASS captured-gate control: "+assertions+" assertions"); return 0; }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static GameUnit ReadyUnit() => new GameUnit {
        r_AliveState=AliveState.IsAlive, r_ControllableForPlayerId=2, r_UnitChimp=eChimps.CHIMP_TYPE_ARAB_ASSASIN,
        r_AIState=107,r_AI_ContextTargetBuildingTileId=45,r_CurrentTilePositionX=10,r_CurrentTilePositionY=12,
        r_ContextTargetTileX=10,r_ContextTargetTileY=12 };
    private static GameBuilding ReadyGate() => new GameBuilding {
        r_AliveState=AliveState.IsAlive,r_BuildingType=eStructs.STRUCT_GATE_MAIN,r_CapturedByPlayerId=2 };
    private static void PolicyTests()
    {
        GameUnit unit=ReadyUnit(); GameBuilding gate=ReadyGate();
        Check(AssassinCapturedGateProtectionPolicy.ShouldProtect(in unit,in gate,true,true,true,true),"Own captured gate protected");
        gate.r_CapturedByPlayerId=3;
        Check(AssassinCapturedGateProtectionPolicy.ShouldProtect(in unit,in gate,true,true,true,true),"Allied captured gate protected");
        Check(!AssassinCapturedGateProtectionPolicy.ShouldProtect(in unit,in gate,true,true,true,false),"Enemy capture retained");
        gate.r_CapturedByPlayerId=0;
        Check(!AssassinCapturedGateProtectionPolicy.ShouldProtect(in unit,in gate,true,true,true,true),"Uncaptured gate retained");
        gate=ReadyGate();
        Check(!AssassinCapturedGateProtectionPolicy.ShouldProtect(in unit,in gate,false,true,true,true),"Master switch off");
        Check(!AssassinCapturedGateProtectionPolicy.ShouldProtect(in unit,in gate,true,false,true,true),"Protection switch off");
        Check(!AssassinCapturedGateProtectionPolicy.ShouldProtect(in unit,in gate,true,true,false,true),"Human player excluded");
        unit.r_IsKilledByProjectile=1;
        Check(!AssassinCapturedGateProtectionPolicy.ShouldProtect(in unit,in gate,true,true,true,true),"Death-marked IsAlive corpse excluded");
        unit=ReadyUnit(); unit.r_AliveState=AliveState.MarkedForDeletion;
        Check(!AssassinCapturedGateProtectionPolicy.ShouldProtect(in unit,in gate,true,true,true,true),"Deleted unit excluded");
        unit=ReadyUnit(); unit.r_UnitChimp=eChimps.CHIMP_TYPE_ARCHER;
        Check(!AssassinCapturedGateProtectionPolicy.ShouldProtect(in unit,in gate,true,true,true,true),"Other unit type excluded");
        unit=ReadyUnit(); unit.r_ControllableForPlayerId=258;
        Check(!AssassinCapturedGateProtectionPolicy.ShouldProtect(in unit,in gate,true,true,true,true),"Full control WORD is not truncated");
        unit=ReadyUnit();
        for(ushort state=0;state<140;state++) {
            unit.r_AIState=state;
            Check(AssassinCapturedGateProtectionPolicy.ShouldProtect(in unit,in gate,true,true,true,true)==(state==101 || state==107),"Only obstacle states; climbing untouched");
        }
        unit=ReadyUnit();unit.r_AIState=101;unit.r_PathPlanStateBitFlags=2;
        Check(!AssassinCapturedGateProtectionPolicy.ShouldProtect(in unit,in gate,true,true,true,true),"Active path in transit retained");
        unit.r_PathPlanStateBitFlags=0;unit.r_ContextTargetTileX=11;
        Check(!AssassinCapturedGateProtectionPolicy.ShouldProtect(in unit,in gate,true,true,true,true),"Not arrived retained");
        unit.r_ContextTargetTileX=10;
        Check(AssassinCapturedGateProtectionPolicy.ShouldProtect(in unit,in gate,true,true,true,true),"Arrived state 101 intercepted before damage");
        gate.r_BuildingType=eStructs.STRUCT_GATE_INNER;
        Check(AssassinCapturedGateProtectionPolicy.ShouldProtect(in unit,in gate,true,true,true,true),"Small gatehouse protected");
        gate.r_BuildingType=eStructs.STRUCT_TOWER1;
        Check(!AssassinCapturedGateProtectionPolicy.ShouldProtect(in unit,in gate,true,true,true,true),"Tower remains Vanilla");
        gate=ReadyGate();gate.r_AliveState=AliveState.None;
        Check(!AssassinCapturedGateProtectionPolicy.ShouldProtect(in unit,in gate,true,true,true,true),"Destroyed gate ignored");
        gate=ReadyGate();unit.r_AI_ContextTargetBuildingTileId=320800;
        Check(!AssassinCapturedGateProtectionPolicy.ShouldProtect(in unit,in gate,true,true,true,true),"Native tile bounds");
    }
    private static void CompletionTests()
    {
        IntPtr storage=Marshal.AllocHGlobal(sizeof(GameUnit));
        GameUnit* unit=(GameUnit*)storage;
        try {
            *unit=ReadyUnit();unit->r_AnimationTimer=9;
            int calls=0;
            AssassinAttackNativeContract.RetargetDelegate selector=(manager,id,role)=> {
                calls++;
                Check(manager==new IntPtr(1234) && id==37,"Manager and one-based ID preserved");
                Check(role==(int)AITribeObjectiveRole.SiegeAssassins,"Second objective passed unchanged");
                Check(unit->r_AI_ContextTargetBuildingTileId==0,"Old context cleared before selection");
                unit->r_AIState=101;unit->r_AI_ContextTargetBuildingTileId=78;return 1;
            };
            unit->r_AIObjectiveRole=AITribeObjectiveRole.MoveToEnemyGatehouse;
            unit->r_AIObjectiveRole2=AITribeObjectiveRole.SiegeAssassins;
            AssassinObstacleCompletion.Complete(unit,37,new IntPtr(1234),true,selector);
            Check(calls==1 && unit->r_AIState==101 && unit->r_AI_ContextTargetBuildingTileId==78 && unit->r_AnimationTimer==0,"Successful native target retained");
            *unit=ReadyUnit();unit->r_AnimationTimer=9;
            AssassinObstacleCompletion.Complete(unit,37,new IntPtr(1234),true,(m,id,r)=>0);
            Check(unit->r_AIState==0 && unit->r_AI_ContextTargetBuildingTileId==0 && unit->r_AnimationTimer==0,"Failed selection idles safely");
            *unit=ReadyUnit();unit->r_AnimationTimer=9;
            AssassinObstacleCompletion.Complete(unit,37,new IntPtr(1234),false,selector);
            Check(calls==1 && unit->r_AIState==0 && unit->r_AI_ContextTargetBuildingTileId==0 && unit->r_AnimationTimer==0,"Legacy mode does not retarget");
            *unit=ReadyUnit();unit->r_AnimationTimer=9;
            bool failed=false;
            try { AssassinObstacleCompletion.Complete(unit,37,IntPtr.Zero,true,(m,id,r)=>{throw new InvalidOperationException();}); }
            catch(InvalidOperationException) { failed=true; }
            Check(failed && unit->r_AIState==0 && unit->r_AI_ContextTargetBuildingTileId==0 && unit->r_AnimationTimer==0,"Selection failure cannot retain damage state");
        } finally { Marshal.FreeHGlobal(storage); }
    }
    [DllImport("kernel32.dll")] private static extern IntPtr VirtualAlloc(IntPtr address,UIntPtr size,uint kind,uint protect);
    [DllImport("kernel32.dll")] private static extern bool VirtualFree(IntPtr address,UIntPtr size,uint kind);
    private static void NativeTests()
    {
        IntPtr code=VirtualAlloc(IntPtr.Zero,(UIntPtr)4096,0x3000,0x40);
        NativeDetour<AssassinAttackNativeContract.UpdateDelegate> candidate=null;
        AssassinAttackNativeContract.UpdateDelegate original=null;
        int calls=0;
        AssassinAttackNativeContract.UpdateDelegate callback=()=> { calls++; original(); };
        try
        {
            Check(code!=IntPtr.Zero,"Native allocation");
            byte[] bytes={0x48,0x89,0x5C,0x24,0x08,0x48,0x89,0x6C,0x24,0x10,0xC3};
            Marshal.Copy(bytes,0,code,bytes.Length);
            var request=new DetourRequest<AssassinAttackNativeContract.UpdateDelegate> { Name="Assassin update exact backend fixture",TargetAddress=(ulong)code.ToInt64(),Callback=callback };
            candidate=AssassinAttackNativeContract.Backend.CreateDetour(in request) as NativeDetour<AssassinAttackNativeContract.UpdateDelegate>;
            Check(candidate!=null && candidate.Scheme==DetourScheme.Indirect && candidate.DisplacedByteCount==10,"Production backend exact displacement");
            original=candidate.Original; candidate.Enable();
            AssassinAttackNativeContract.Validate(candidate,code); assertions++;
            Marshal.GetDelegateForFunctionPointer<AssassinAttackNativeContract.UpdateDelegate>(code)();
            Check(calls==1,"No-argument native callback/original ABI");
            original(); Check(calls==1,"Original trampoline does not recurse");
            // Exact Winapi delegate ABI: pointer base, 32-bit one-based ID, signed 32-bit role.
            byte[] probe={0x89,0x11,0x44,0x89,0x41,0x04,0xB8,0x01,0x00,0x00,0x00,0xC3};
            Marshal.Copy(probe,0,code+128,probe.Length);
            var select=Marshal.GetDelegateForFunctionPointer<AssassinAttackNativeContract.RetargetDelegate>(code+128);
            Check(select(code+256,37,-3)==1 && Marshal.ReadInt32(code,256)==37 && Marshal.ReadInt32(code,260)==-3,"Actual retarget pointer/ID/signed-role ABI");

        }
        finally { candidate?.Dispose(); VirtualFree(code,UIntPtr.Zero,0x8000); GC.KeepAlive(callback); }
    }
}
