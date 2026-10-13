using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace EnemyGatePathfindingTest {
// SDK lookup/liveness and configuration calls are controlled fixture boundaries.
// The complete production Allows body and pure transition rule are compiled unchanged.
enum eChimps : short { CHIMP_TYPE_ARAB_ASSASIN=73 }
[StructLayout(LayoutKind.Explicit,Size=0x490)] unsafe struct GameUnit {
[FieldOffset(0x8A)] internal eChimps r_UnitChimp;
[FieldOffset(0x92)] internal ushort r_ControllableForPlayerId;
[FieldOffset(0x94)] internal uint r_GlobalId;
[FieldOffset(0xC2)] internal ushort r_CurrentTilePositionY;
[FieldOffset(0xD0)] internal uint r_CurrentPositionTileId;
[FieldOffset(0xF6)] internal ushort r_CurrentPathPlanIndex;
[FieldOffset(0xF8)] internal ushort r_PathPlanLength;
[FieldOffset(0x2B0)] internal short r_MovementDirection;
[FieldOffset(0x2BC)] internal ushort r_AIState;
}
struct GameBuilding { internal uint r_GlobalId; internal ushort r_PlayerIdOwner,r_CapturedByPlayerId; internal byte r_GateState; }
struct X64SmartCPUContext { internal ulong RBX,R14,RDI,R12,RBP,RDX,R9; }
sealed class RouteTilePolicySnapshot { internal Dictionary<int,Identity> GateIdentities=new(); internal struct Identity { internal uint Global; internal ushort Owner,Capturer; } }
static unsafe class UnitAccess { internal static GameUnit* Unit; internal static bool Alive=true, Lookup=true;
internal static bool TryGetById(int id,out GameUnit* u,out int x) { u=Unit;x=0;return Lookup; }
internal static bool IsReallyAlive(in GameUnit u)=>Alive; }
static class AssassinPathAPI { internal static bool DirectGatehouseClimbingEnabled=true, Endpoint=true;
internal static bool IsDirectGatehouseClimbEndpoint(int t)=>Endpoint; }
unsafe class GameBuildingManagerAPI { internal static GameBuildingManagerAPI Instance=new(); internal static GameBuilding* Building;
internal bool IsValidId(int id)=>id>0&&id<4000;
internal bool TryGetBuildingById(int id,out GameBuilding* b) { b=Building;return true; } }
unsafe class Program {
static int checks; static void Check(bool ok,string why) { checks++;if(!ok)throw new Exception(why); }
static int Main() { try { Run(); return 0; } catch(Exception e) { Console.Error.WriteLine(e); return 1; } }
static void Run() {
string root=System.IO.Directory.GetCurrentDirectory();
string runtime=System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Testmods/EnemyGatePathfindingTest/src/AssassinGateDescentRuntime.cs")).Replace("\r\n","\n");
string body=runtime.Substring(runtime.IndexOf("        private bool Allows(")); body=body.Substring(0,body.LastIndexOf("\n    }")).Replace("private bool Allows","internal bool Allows");
Check(System.IO.File.ReadAllText(System.IO.Path.Combine(root,"_inspect/AssassinDescentRuntimeTests/ProductionReader.cs")).Replace("\r\n","\n").Contains(body.Replace("\r\n","\n")),"fixture reader is exactly current production source");
byte* m=(byte*)NativeMemory.AllocZeroed(0x6800000); int active=1;
try {
var r=new Reader { module=(ulong)m, active=&active };
var snapshot=new RouteTilePolicySnapshot(); snapshot.GateIdentities.Add(1,new RouteTilePolicySnapshot.Identity{Global=100,Owner=1});
r.policy=()=>snapshot;
GameBuilding b=new(){r_GlobalId=100,r_PlayerIdOwner=1,r_GateState=2}; GameBuildingManagerAPI.Building=&b;
GameUnit* u=(GameUnit*)(m+0x67E8400+0x490+0x65C); UnitAccess.Unit=u;
*u=new GameUnit { r_UnitChimp=eChimps.CHIMP_TYPE_ARAB_ASSASIN,r_GlobalId=4,r_ControllableForPlayerId=2,r_CurrentPositionTileId=1,
r_CurrentPathPlanIndex=16,r_PathPlanLength=19,r_AIState=101 };
*(short*)(m+0x4B6AA50+2)=1; *(int*)(m+0x405EDB0)=1; m[0x312620]=1;m[0x312624]=16;
*(uint*)(m+0x48F71B0+4)=0x100;
X64SmartCPUContext* cp=stackalloc X64SmartCPUContext[1]; *cp=new(){RBX=1,RDI=(ulong)m+0x67E8400,R12=0x490};
Check(r.Allows(cp),"actual production reader authorizes verified descent");
void Reject(Action change,Action restore,string why) { change();Check(!r.Allows(cp),why);restore();Check(r.Allows(cp),why+" restored"); }
// No substitute player or fabricated identities.
Reject(()=>u->r_ControllableForPlayerId=0,()=>u->r_ControllableForPlayerId=2,"zero control");
Reject(()=>u->r_ControllableForPlayerId=0x102,()=>u->r_ControllableForPlayerId=2,"full control WORD");
Reject(()=>u->r_GlobalId=0,()=>u->r_GlobalId=4,"missing unit global");
Reject(()=>UnitAccess.Alive=false,()=>UnitAccess.Alive=true,"dead unit");
Reject(()=>UnitAccess.Lookup=false,()=>UnitAccess.Lookup=true,"missing unit");
Reject(()=>UnitAccess.Unit=u+1,()=>UnitAccess.Unit=u,"mismatched native unit address");
Reject(()=>u->r_UnitChimp=(eChimps)10,()=>u->r_UnitChimp=eChimps.CHIMP_TYPE_ARAB_ASSASIN,"ordinary unit");
Reject(()=>AssassinPathAPI.DirectGatehouseClimbingEnabled=false,()=>AssassinPathAPI.DirectGatehouseClimbingEnabled=true,"climb disabled");
Reject(()=>AssassinPathAPI.Endpoint=false,()=>AssassinPathAPI.Endpoint=true,"invalid gate endpoint");
Reject(()=>*(short*)((byte*)u+0x90)=1,()=>*(short*)((byte*)u+0x90)=0,"unit pathmode");
Reject(()=>*(short*)((byte*)u+0x36C)=1,()=>*(short*)((byte*)u+0x36C)=0,"alternate movement");
Reject(()=>u->r_AIState=106,()=>u->r_AIState=101,"combat state");
Reject(()=>m[0x51890D0+1]=1,()=>m[0x51890D0+1]=0,"ground connection");
Reject(()=>m[0x51890D0+2]=16,()=>m[0x51890D0+2]=0,"reverse ground connection");
// Stack/local pointer fixtures cannot be captured: context and building mutations directly.
var saved=*cp;
cp->RBP=1;Check(!r.Allows(cp),"speculative call never uses exception");*cp=saved;
cp->RBX=10001;Check(!r.Allows(cp),"invalid ID");*cp=saved;
cp->R12=0;Check(!r.Allows(cp),"stride mismatch");*cp=saved;
cp->RDX=1;Check(!r.Allows(cp),"register pathmode");*cp=saved;
cp->R9=1;Check(!r.Allows(cp),"special step");*cp=saved;
b.r_GlobalId++;Check(!r.Allows(cp),"reused gate ID");b.r_GlobalId--;
b.r_CapturedByPlayerId=2;Check(!r.Allows(cp),"capture before matching publication");
snapshot.GateIdentities[1]=new RouteTilePolicySnapshot.Identity{Global=100,Owner=1,Capturer=2};Check(r.Allows(cp),"confirmed captured gate");
b.r_PlayerIdOwner=2;snapshot.GateIdentities[1]=new RouteTilePolicySnapshot.Identity{Global=100,Owner=2,Capturer=2};Check(r.Allows(cp),"owned gate descent");
active=0;Check(!r.Allows(cp),"map ended");active=1;
int calls=0;r.policy=()=>++calls==1?snapshot:new RouteTilePolicySnapshot();Check(!r.Allows(cp),"publication changed within read");
calls=0;r.policy=()=> { if(++calls==2)u->r_GlobalId++;return snapshot; };Check(!r.Allows(cp),"unit reused during read");u->r_GlobalId--;
r.policy=()=>snapshot;
for(int d=0;d<8;d+=2) { cp->R14=(ulong)d;u->r_MovementDirection=(short)d;*(int*)(m+0x405EDB0+d*4)=1;m[0x312620+d]=(byte)(1<<d);m[0x312620+(d^4)]=(byte)(1<<(d^4));Check(r.Allows(cp),"orientation "+d); }
Console.WriteLine("PASS: "+checks+" production-reader assertions; native data, SDK fixture boundaries, identities/control/state, owned/captured, maps and publication changes.");
} finally { NativeMemory.Free(m); }
}
}}
