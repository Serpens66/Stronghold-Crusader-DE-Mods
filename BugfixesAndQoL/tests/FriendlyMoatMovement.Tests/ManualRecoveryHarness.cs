using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BugfixesAndQoL.UnitCommands;

namespace SHCDESE.Interop { internal enum eChimps { CHIMP_TYPE_ARAB_ASSASIN = 73 } }
namespace CommandFixture
{
    internal struct FormationUnit { internal int UnitId; internal uint GlobalId; }
    public unsafe partial class UnitCommandPathRuntime
    {
        private readonly List<string> diagnosticFailures = new();
        private Func<IntPtr,int,int,int> getGroupUnitId;
        private Func<IntPtr> manualThreadToken = () => (IntPtr)123;
        private Func<int,int,int,int,int> manualRegionQuery;
        private const uint CursorSpecialStructureTileFlagMask = 0x10000300;

        private void RecoveryTests()
        {
            int firstAssertion = assertions;
            GameUnit* units = (GameUnit*)NativeMemory.AllocZeroed(1001 * (nuint)sizeof(GameUnit));
            GameTribe* tribe = (GameTribe*)NativeMemory.AllocZeroed((nuint)sizeof(GameTribe));
            byte* manager = (byte*)NativeMemory.AllocZeroed(0x200);
            movementTargetAvailability = (byte*)NativeMemory.Alloc(800*800);
            tileFlags = (uint*)NativeMemory.AllocZeroed(800*800*sizeof(uint));
            int queries = 0, probes = 0;
            var keys = new HashSet<(int,int,int,int)>();
            try
            {
                APIShared.UnitAccess.Units = units; APIShared.UnitAccess.UnitCapacity = 1001;
                GameTribeManagerAPI.Instance.Tribe = tribe;
                nativePathManager = (IntPtr)manager; nativeTribeManager = (IntPtr)tribe; nativeUnitManager = (byte*)units;
                manualRegionQuery = QueryManualGroupRegion;
                getGroupUnitId = (m,t,i) => i+1;
                originalCentralMovementPlan = (m,id,x,y) => { probes++; throw new Exception("Forbidden full path probe"); };
                new Span<byte>(movementTargetAvailability,800*800).Fill(1);
                void Setup(int count, int outside)
                {
                    manualAdmissionReady = true; ManualCommandsEnabled = true; TraversalEnabled = false; disposed = false;
                    unitMoveFrame = null; activePlan = pendingPlan = null; activeAttackCommand = null;
                    activeDirectFillCommand = null; activeMoatWorkSelection = null; manualAdmission = null;
                    completedMoatTiles.Clear(); Array.Clear(pathRegionGrid);
                    tribe->r_PlayerIdOwner = 1; tribe->r_LeaderUnitId = 1; tribe->r_UnitsInGroup = count;
                    pathRegionGrid[16020] = 42;
                    for (int id=1; id<=count; id++)
                    {
                        units[id] = new GameUnit { r_AliveState=AliveState.IsAlive, r_GlobalId=(uint)(id+500),
                            r_TribeId=1, r_ControllableForPlayerId=1, r_CurrentTilePositionX=100+id%600,
                            r_CurrentTilePositionY=100+id/600, r_MovementSubstep=8, N000000DB=1 };
                        units[id].r_CurrentPositionTileId=(uint)(units[id].r_CurrentTilePositionY*800+units[id].r_CurrentTilePositionX);
                        pathRegionGrid[units[id].r_CurrentPositionTileId]=(ushort)(id<=outside?7:42);
                    }
                    queries=probes=0;keys.Clear();diagnosticFailures.Clear();
                    originalRegionPairReachability=(m,p,s,t,mode)=>{
                        queries++;keys.Add((p,s,t,mode));
                        *(int*)(manager+0x98)=99;*(int*)(manager+0xC0)=100;*(int*)(manager+0xC4)=101;
                        return 0;
                    };
                }
                foreach(int count in new[]{100,200,550,1000})
                foreach(int blocked in new[]{0,count/2,count})
                {
                    Setup(count,blocked);
                    var args=new TribeIssueOrderMoveHereEventArgs();
                    var parent=BeginManualGroupAdmission(args);
                    var batch=manualAdmission;
                    Check(batch.Supported && batch.Excluded==blocked && batch.Reachable==count-blocked,"all members classified before dispatch");
                    Check(args.SkipOriginalFunction==(blocked==count),"only completely excluded groups are consumed before native mutation");
                    Check(queries==(blocked==0?0:1) && probes==0,"100/200/550/1000: one query per region key, zero full path probes");
                    for(int id=1;id<=count;id++)
                        Check(batch.IsExcluded(id,units[id].r_GlobalId)==(id<=blocked),"stamped native decision matches each stable identity");
                    var selected=new FormationUnit[count];
                    for(int id=1;id<=count;id++) selected[id-1]=new FormationUnit{UnitId=id,GlobalId=units[id].r_GlobalId};
                    Check(AdmitFormationMembers(selected,1,20,20).Length==count-blocked,"formation gets only admitted identities");
                    EndManualGroupAdmission(parent);
                    Check(manualAdmission==null,"command context always closes");
                }

                Setup(4,4); units[2].r_PathConnectionMode=ushort.MaxValue; units[3].r_PathConnectionMode=1;
                for(int i=0;i<0x200;i++)manager[i]=(byte)(i^0x5A);
                byte[] before = new ReadOnlySpan<byte>(manager,0x200).ToArray();
                var outer=BeginManualGroupAdmission(new());
                Check(queries==3 && keys.Contains((1,7,42,-1)),"player, source, target and signed native mode form the key");
                Check(new ReadOnlySpan<byte>(manager,0x200).SequenceEqual(before),"all native query scratch bytes restored");
                EndManualGroupAdmission(outer);
                Setup(2,2); originalRegionPairReachability=(m,p,s,t,mode)=>{
                    *(int*)(manager+0x98)=700;*(int*)(manager+0xC0)=800;*(int*)(manager+0xC4)=900;
                    throw new Exception("native graph failure");
                };
                outer=BeginManualGroupAdmission(new());
                Check(!manualAdmission.Supported && diagnosticFailures.Count==1,"a query exception disables admission for this command");
                Check(new ReadOnlySpan<byte>(manager,0x200).SequenceEqual(before),"native scratch restores even through an exception");
                EndManualGroupAdmission(outer);

                Setup(2,2); units[1].r_MovementSubstep=4; units[1].r_NextTilePositionX2=20; units[1].r_NextTilePositionY2=20;
                outer=BeginManualGroupAdmission(new());
                Check(manualAdmission.Members[0].Decision==ManualRegionAdmission.Reachable,"moving unit uses Vanilla's next-tile start");
                EndManualGroupAdmission(outer);

                Setup(2,2);units[1].r_MovementSubstep=8;units[1].r_PathPlanStateBitFlags=2;
                units[1].r_NextTilePositionX2=20;units[1].r_NextTilePositionY2=20;
                outer=BeginManualGroupAdmission(new());
                Check(manualAdmission.Members[0].Decision==ManualRegionAdmission.Reachable,
                    "set path flags at substep8 still select the next tile, exactly like 196280");
                EndManualGroupAdmission(outer);

                Setup(2,1);units[1].r_CurrentPositionTileId++;
                outer=BeginManualGroupAdmission(new());
                Check(!manualAdmission.Supported && queries==0,"units requiring native position normalization remain native-owned");
                EndManualGroupAdmission(outer);

                foreach(int variant in new[]{0,1,2,3,4,5,6})
                {
                    Setup(2,1);
                    switch(variant){
                        case 0: activeAttackCommand=new();break;
                        case 1: activePlan=new();break;
                        case 2: activeMoatWorkSelection=new();break;
                        case 3: completedMoatTiles.Add((int)units[1].r_CurrentPositionTileId);break;
                        case 4: units[1].r_UnitChimp=73;break;
                        case 5: TraversalEnabled=true;units[1].Digger=true;break;
                        case 6: tileFlags[units[1].r_CurrentPositionTileId]=0x100;break;
                    }
                    outer=BeginManualGroupAdmission(new());
                    Check(manualAdmission==null || !manualAdmission.Supported,"special routes retain existing providers");
                    Check(queries==0 && probes==0,"special routes get no preparatory search");
                    EndManualGroupAdmission(outer); tileFlags[units[1].r_CurrentPositionTileId]=0;
                }
                Setup(2,1); outer=BeginManualGroupAdmission(new());var parentBatch=manualAdmission;
                activeAttackCommand=new();var nestedParent=BeginManualGroupAdmission(new());
                Check(manualAdmission==null,"irrelevant nested command cannot borrow its parent's mask");
                EndManualGroupAdmission(nestedParent);
                Check(ReferenceEquals(manualAdmission,parentBatch),"nested command restores parent admission");
                activeAttackCommand=null;EndManualGroupAdmission(outer);

                Setup(2,0);units[1].N000000DB=0;outer=BeginManualGroupAdmission(new());
                Check(manualAdmission.Excluded==1 && queries==0,"native immobility flag excludes stationary engines without a region query");
                Check(!manualAdmission.IsExcluded(1,units[1].r_GlobalId+1),"reused native ID cannot inherit an old exclusion");
                EndManualGroupAdmission(outer);
                Setup(2,2);queueDefersMove=a=>true;outer=BeginManualGroupAdmission(new());
                Check(manualAdmission==null && queries==0,"Shift insertion waits for execution-time admission");
                EndManualGroupAdmission(outer);queueDefersMove=null;
                Setup(2,1);var rewritten=new TribeIssueOrderMoveHereEventArgs();outer=BeginManualGroupAdmission(rewritten);
                rewritten.TileX++;
                Check(!IsMixedManualGroup(1,20,20),"a later subscriber's retarget cannot reuse the mask");
                EndManualGroupAdmission(outer);
                Setup(2,2);manualPlayerMove=new ManualPlayerMoveFrame{Valid=true,Tribe=1};
                outer=BeginManualGroupAdmission(new());
                Check(manualPlayerMove.Rejected,"fully rejected player move records its earlier native flag write for restoration");
                EndManualGroupAdmission(outer);
                Setup(2,2);manualPlayerMove=new ManualPlayerMoveFrame{Valid=true,Tribe=1,GroupSeen=true};
                outer=BeginManualGroupAdmission(new());
                Check(!manualPlayerMove.Rejected,"nested group call cannot mark the parent player's move rejected");
                EndManualGroupAdmission(outer);manualPlayerMove=default;
                Console.WriteLine($"PASS: {assertions-firstAssertion} production pre-dispatch admission checks; 100/200/550/1000, no full probes.");
            }
            finally
            {
                manualAdmission=null;manualAdmissionReady=false;activeAttackCommand=null;activePlan=null;pendingPlan=null;
                NativeMemory.Free(units);NativeMemory.Free(tribe);NativeMemory.Free(manager);
                NativeMemory.Free(movementTargetAvailability);NativeMemory.Free(tileFlags);
                APIShared.UnitAccess.Units=null;APIShared.UnitAccess.UnitCapacity=3;GameTribeManagerAPI.Instance.Tribe=null;
            }
        }
    }
}
