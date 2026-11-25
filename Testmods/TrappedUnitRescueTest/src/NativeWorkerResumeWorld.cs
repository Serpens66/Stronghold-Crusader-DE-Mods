using System;
using APIShared;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    // NativeRelocation can only be constructed after the pinned image/layout audit.
    // No pointer survives an API call or enters a saved receipt.
    internal sealed unsafe class NativeWorkerResumeWorld : IWorkerResumeWorld,IWorkerResumeAuthorityAware,IWorkerResumeBarrierView
    {
        private readonly NativeRelocation native;
        private Func<WorkerResumeTicket,bool> authority;
        private Action<WorkerResumeTicket> invalidateSource;
        public void BindResumeAuthority(Func<WorkerResumeTicket,bool> isCurrent,Action<WorkerResumeTicket> onSourceLost)
        {if(authority!=null)throw new InvalidOperationException("Resume world already has an owner.");authority=isCurrent??throw new ArgumentNullException(nameof(isCurrent));
            invalidateSource=onSourceLost??throw new ArgumentNullException(nameof(onSourceLost));}
        internal NativeWorkerResumeWorld(NativeRelocation native){this.native=native??throw new ArgumentNullException(nameof(native));}
        public bool TryCapture(UnitSnapshot before,out WorkerResumeTicket ticket,out string reason)
        {
            if(before!=null && before.Job==null && before.Command!=null)return TryCaptureCommand(before,out ticket,out reason);
            ticket=null;reason="worker-live-capture-rejected";
            if(before==null || before.Job==null || before.Unsupported!=null || !native.TryUnit(before.Id,out var unit) ||
                !WorkerTravelPolicy.IsActiveTravel(unit->r_UnitChimp,unit->r_AIState,before.Flags) ||
                unit->r_GlobalId!=before.GlobalId || unit->r_CurrentHealth!=before.Health ||
                unit->r_ControllableForPlayerId!=before.Owner || unit->r_TribeId!=before.Tribe ||
                !AdmissibleCapturePosition(before,(int)unit->r_CurrentPositionTileId,unit->r_CurrentTilePositionX,unit->r_CurrentTilePositionY,
                unit->r_PathPlanStateBitFlags,unit->r_MovementSubstep,(int)unit->r_NextPositionTileId2,unit->r_NextTilePositionX2,unit->r_NextTilePositionY2) ||
                !CurrentCoherent(unit) ||
                (unit->r_MovementSubstep<8 && !NextCoherent(unit)) ||
                unit->r_TargetTilePositionX2!=before.RequestedX || unit->r_TargetTilePositionY2!=before.RequestedY ||
                !SafeMode(unit) || !Match(before.Job,unit,true) || !native.TryTribeGlobal(before.Tribe,out uint tribeGlobal) ||
                tribeGlobal!=before.TribeGlobalId || (before.Tribe!=0 && native.JobDword(unit,0x93C)!=tribeGlobal) ||
                native.Tile(before.RequestedX,before.RequestedY)<0)return false;
            // Refresh harmless timers now; subsequent synchronous native mutations still
            // use a freshly exact snapshot, not the delayed pre-mutation timer values.
            IntPtr pointer=(IntPtr)unit;
            if(!WorkerJourneySnapshot.TryCapture(before.Job.Rule,offset=>native.JobWord((GameUnit*)pointer,offset),
                offset=>native.JobDword((GameUnit*)pointer,offset),out var job))return false;
            var required=job.Rule.RequiredExternalBindings;
            int home=0,store=0,tree=0;uint homeGlobal=0,storeGlobal=0,treeGlobal=0;
            if((required&WorkerExternalBindings.Workplace)!=0)
            {home=native.JobWord(unit,0x990);homeGlobal=native.JobDword(unit,0x9C0);if(!BuildingAlive(home,homeGlobal))return false;}
            if((required&WorkerExternalBindings.Store)!=0)
            {store=native.JobWord(unit,0x98E);storeGlobal=native.JobDword(unit,0x6F8);if(!BuildingAlive(store,storeGlobal))return false;}
            if((required&WorkerExternalBindings.Vegetation)!=0)
            {
                tree=native.JobWord(unit,0x9A0);
                if(!NativeVegetationAccess.TryGet(tree,out var vegetation) ||
                    !NativeVegetationAccess.TryReadCapturedAliveGlobal(vegetation,before.HasCapturedVegetation,before.CapturedVegetationGlobal,out treeGlobal))return false;
                if(job.Rule.VegetationSerialNativeOffset!=0 && treeGlobal!=native.JobDword(unit,job.Rule.VegetationSerialNativeOffset))return false;
            }
            var bindings=new WorkerBindingSnapshot(required,home,homeGlobal,store,storeGlobal,tree,treeGlobal);
            ticket=new WorkerResumeTicket(before.Id,before.GlobalId,(eChimps)before.Type,before.AIState,(ushort)before.Owner,
                before.Tribe,before.TribeGlobalId,(ushort)before.RequestedX,(ushort)before.RequestedY,job,bindings);
            if(!ticket.IsValid){ticket=null;return false;}reason=null;return true;
        }
        private bool TryCaptureCommand(UnitSnapshot before,out WorkerResumeTicket ticket,out string reason)
        {
            ticket=null;reason="command-live-capture-rejected";
            if((before.Flags!=2 && before.Flags!=4) || before.Unsupported!=null || !native.TryUnit(before.Id,out var unit) || unit->r_GlobalId!=before.GlobalId ||
                unit->r_CurrentHealth!=before.Health || unit->r_ControllableForPlayerId!=before.Owner || unit->r_TribeId!=before.Tribe ||
                unit->r_TargetTilePositionX2!=before.RequestedX || unit->r_TargetTilePositionY2!=before.RequestedY || !SafeMode(unit) ||
                !AdmissibleCapturePosition(before,(int)unit->r_CurrentPositionTileId,unit->r_CurrentTilePositionX,unit->r_CurrentTilePositionY,
                    unit->r_PathPlanStateBitFlags,unit->r_MovementSubstep,(int)unit->r_NextPositionTileId2,unit->r_NextTilePositionX2,unit->r_NextTilePositionY2) ||
                !CurrentCoherent(unit) ||
                (unit->r_MovementSubstep<8 && !NextCoherent(unit)) ||
                !native.TryTribeGlobal(before.Tribe,out uint tribeGlobal) || tribeGlobal!=before.TribeGlobalId ||
                (before.Tribe!=0 && native.JobDword(unit,0x93C)!=tribeGlobal) || native.Tile(before.RequestedX,before.RequestedY)<0)return false;
            IntPtr pointer=(IntPtr)unit;
            if(!before.Command.Matches(unit->r_UnitChimp,unit->r_AIState,offset=>native.JobWord((GameUnit*)pointer,offset),
                offset=>native.JobDword((GameUnit*)pointer,offset)) || !CommandCrewAlive(before.Command,(ushort)before.Owner))return false;
            ticket=WorkerResumeTicket.ForCommand(before.Id,before.GlobalId,(eChimps)before.Type,before.AIState,(ushort)before.Owner,
                before.Tribe,before.TribeGlobalId,(ushort)before.RequestedX,(ushort)before.RequestedY,before.Command);
            if(!ticket.IsValid){ticket=null;return false;}reason=null;return true;
        }
        private bool CommandCrewAlive(CommandJourneySnapshot command,ushort owner)
        {
            if(command==null)return false;
            if(!CommandResumePolicy.IsSiege(command.UnitType))return true;
            for(int i=0;i<CommandJourneySnapshot.RequiredCrewCount(command.UnitType);i++)
            {
                if(!command.TryGetValue(0x96C+i*2,out uint id) || !command.TryGetValue(0x974+i*4,out uint global) ||
                    !native.TryUnit((int)id,out var crew) || crew->r_GlobalId!=global || crew->r_UnitChimp!=eChimps.CHIMP_TYPE_ENGINEER ||
                    crew->r_AIState!=5 || native.Word(crew,0x9B0)==0 || crew->r_ControllableForPlayerId!=owner)return false;
            }
            return true;
        }
        private bool Match(WorkerResumeTicket ticket,GameUnit* unit,bool continuation)
        {
            if(ticket.Kind==ResumeReceiptKind.Worker)return Match(ticket.Job,unit,continuation);
            if(ticket.Kind!=ResumeReceiptKind.Command || ticket.Command==null || !CommandCrewAlive(ticket.Command,ticket.Owner))return false;
            IntPtr pointer=(IntPtr)unit;
            return ticket.Command.Matches(unit->r_UnitChimp,unit->r_AIState,
                offset=>native.JobWord((GameUnit*)pointer,offset),offset=>native.JobDword((GameUnit*)pointer,offset));
        }
        internal static bool AdmissibleCapturePosition(UnitSnapshot before,int tile,int x,int y,int flags,int substep,int nextTile,int nextX,int nextY)
        {
            if(before==null || substep<0 || substep>8)return false;
            bool oldCurrent=tile==before.TileId && x==before.X && y==before.Y;
            bool acceptedNext=before.OrdinaryStep && (before.NextX!=before.X || before.NextY!=before.Y) &&
                tile==before.NextTileId && x==before.NextX && y==before.NextY;
            if(flags==0)return (before.Flags==2 || before.Flags==4) && acceptedNext && substep==8 &&
                nextTile==tile && nextX==x && nextY==y;
            if(flags!=2 && flags!=4)return false;
            if(!oldCurrent && !acceptedNext)return false;
            if(substep<8)
            {
                if(nextX==x && nextY==y || Math.Abs(nextX-x)>1 || Math.Abs(nextY-y)>1)return false;
                if(oldCurrent && (!before.OrdinaryStep || nextTile!=before.NextTileId || nextX!=before.NextX || nextY!=before.NextY))return false;
            }
            return true;
        }
        private static bool BuildingAlive(int id,uint global)
        {
            return id>0 && BuildingAccess.TryGetById(id,out var building,out _) && building!=null &&
                building->r_AliveState==AliveState.IsAlive && building->r_GlobalId==global;
        }
        private static bool TryVegetation(int id,out uint global)
        {
            global=0;
            return NativeVegetationAccess.TryGet(id,out var vegetation) && NativeVegetationAccess.TryReadAliveGlobal(vegetation,out global);
        }
        private static bool BindingsAlive(WorkerBindingSnapshot bindings)
        {
            if(bindings==null)return false;
            if((bindings.Required&WorkerExternalBindings.Workplace)!=0 && !BuildingAlive(bindings.WorkplaceId,bindings.WorkplaceGlobal))return false;
            if((bindings.Required&WorkerExternalBindings.Store)!=0 && !BuildingAlive(bindings.StoreId,bindings.StoreGlobal))return false;
            return (bindings.Required&WorkerExternalBindings.Vegetation)==0 ||
                TryVegetation(bindings.VegetationId,out uint global) && global==bindings.VegetationGlobal;
        }
        private bool Match(WorkerJourneySnapshot job,GameUnit* unit,bool continuation)
        {
            IntPtr pointer=(IntPtr)unit;
            return continuation ? job.MatchesContinuation(unit->r_UnitChimp,unit->r_AIState,
                offset=>native.JobWord((GameUnit*)pointer,offset),offset=>native.JobDword((GameUnit*)pointer,offset)) :
                job.Matches(unit->r_UnitChimp,unit->r_AIState,offset=>native.JobWord((GameUnit*)pointer,offset),offset=>native.JobDword((GameUnit*)pointer,offset));
        }
        private bool SafeMode(GameUnit* unit,bool allowFreshMoat=false)=>unit->r_CurrentHealth>0 && native.Word(unit,0x6EC)==0 &&
            (allowFreshMoat ? native.Word(unit,0x9C8)<=1 : native.Word(unit,0x9C8)==0) &&
            native.Word(unit,0x9B0)==0 && native.Word(unit,0x8EC)==0 && native.Word(unit,0x8F6)==0 &&
            unit->r_MovementSubstep<=8;
        private bool CurrentCoherent(GameUnit* unit)
        {int tile=native.Tile(unit->r_CurrentTilePositionX,unit->r_CurrentTilePositionY);return tile>=0 && unit->r_CurrentPositionTileId==(uint)tile;}
        private bool NextCoherent(GameUnit* unit)
        {int tile=native.Tile(unit->r_NextTilePositionX2,unit->r_NextTilePositionY2);return tile>=0 && unit->r_NextPositionTileId2==(uint)tile;}
        private bool ParkablePose(GameUnit* unit)=>unit->r_MovementSubstep==8 && CurrentCoherent(unit) &&
            unit->r_CurrentPositionTileId==unit->r_NextPositionTileId2 && unit->r_CurrentTilePositionX==unit->r_NextTilePositionX2 &&
            unit->r_CurrentTilePositionY==unit->r_NextTilePositionY2 && native.Word(unit,0x740)==0 && native.Word(unit,0x742)==0;
        public WorkerReceiptState Inspect(WorkerResumeTicket ticket,bool continuation)
        {
            if(ticket==null || !ticket.IsValid)return WorkerReceiptState.Unknown;
            if(!native.TryUnit(ticket.UnitId,out var unit) || unit->r_GlobalId!=ticket.UnitGlobalId)return WorkerReceiptState.ReplacedOrGone;
            if(unit->r_UnitChimp!=ticket.UnitType || unit->r_AIState!=ticket.AIState || unit->r_ControllableForPlayerId!=ticket.Owner ||
                unit->r_TribeId!=ticket.TribeId || !native.TryTribeGlobal(ticket.TribeId,out uint tribeGlobal) || tribeGlobal!=ticket.TribeGlobalId)
                return WorkerReceiptState.ReplacedOrGone;
            // A changed active route belongs to native/new-order processing, never to us.
            if(unit->r_PathPlanStateBitFlags==2 || unit->r_PathPlanStateBitFlags==4)
            {
                if(!Match(ticket,unit,continuation) || unit->r_TargetTilePositionX2!=ticket.RequestedX || unit->r_TargetTilePositionY2!=ticket.RequestedY)
                    return WorkerReceiptState.Unknown;
                return SafeMode(unit) && BindingsAlive(ticket.Bindings)?WorkerReceiptState.MatchingActive:WorkerReceiptState.Unknown;
            }
            if(!SafeMode(unit,unit->r_PathPlanStateBitFlags==5) || !BindingsAlive(ticket.Bindings) || !Match(ticket,unit,continuation) ||
                unit->r_TargetTilePositionX2!=ticket.RequestedX || unit->r_TargetTilePositionY2!=ticket.RequestedY)return WorkerReceiptState.Unknown;
            if(unit->r_PathPlanStateBitFlags==5)return ParkablePose(unit)?WorkerReceiptState.MatchingParked:WorkerReceiptState.Unknown;
            return unit->r_PathPlanStateBitFlags==0?WorkerReceiptState.MatchingStopped:WorkerReceiptState.Unknown;
        }
        public bool TryPark(WorkerResumeTicket ticket,bool continuation)
        {
            // Losing an external target forbids reissue, but must not expose false
            // arrival after our own route mutation. Park only the unchanged admitted intent.
            if(ticket==null || !ticket.IsValid || !native.TryUnit(ticket.UnitId,out var unit) || unit->r_GlobalId!=ticket.UnitGlobalId ||
                unit->r_UnitChimp!=ticket.UnitType || unit->r_AIState!=ticket.AIState || unit->r_ControllableForPlayerId!=ticket.Owner ||
                unit->r_TribeId!=ticket.TribeId || !native.TryTribeGlobal(ticket.TribeId,out uint tribeGlobal) || tribeGlobal!=ticket.TribeGlobalId ||
                !SafeMode(unit,true) || !Match(ticket,unit,continuation) || unit->r_TargetTilePositionX2!=ticket.RequestedX ||
                unit->r_TargetTilePositionY2!=ticket.RequestedY ||
                !ParkablePose(unit) ||
                (unit->r_PathPlanStateBitFlags!=0 && unit->r_PathPlanStateBitFlags!=2 && unit->r_PathPlanStateBitFlags!=4 && unit->r_PathPlanStateBitFlags!=5))return false;
            //193B80 also writes91E. Deliberately change only the audited arrival barrier74E.
            if(unit->r_PathPlanStateBitFlags!=5)unit->r_PathPlanStateBitFlags=5;
            return unit->r_PathPlanStateBitFlags==5;
        }
        public bool HasOwnedArrivalBarrier(WorkerResumeTicket ticket)
        {
            return ticket!=null && ticket.IsValid && native.TryUnit(ticket.UnitId,out var unit) && unit->r_GlobalId==ticket.UnitGlobalId &&
                unit->r_UnitChimp==ticket.UnitType && unit->r_ControllableForPlayerId==ticket.Owner && unit->r_TribeId==ticket.TribeId &&
                native.TryTribeGlobal(ticket.TribeId,out uint global) && global==ticket.TribeGlobalId && SafeMode(unit,true) &&
                unit->r_PathPlanStateBitFlags==5 && ParkablePose(unit);
        }
        public bool Reissue(WorkerResumeTicket ticket)
        {
            if(Inspect(ticket,true)!=WorkerReceiptState.MatchingParked)return false;
            if(!native.TryUnit(ticket.UnitId,out var initial))return false;
            int x=initial->r_CurrentTilePositionX,y=initial->r_CurrentTilePositionY;uint tile=initial->r_CurrentPositionTileId;
            bool Current()
            {
                if(native.TryUnit(ticket.UnitId,out var live) && live->r_GlobalId==ticket.UnitGlobalId &&
                    (!ParkablePose(live) || live->r_CurrentTilePositionX!=x || live->r_CurrentTilePositionY!=y || live->r_CurrentPositionTileId!=tile))
                {invalidateSource?.Invoke(ticket);return false;}
                return IntentAuthorityCurrent(ticket);
            }
            return native.Reissue(ticket.UnitId,ticket.RequestedX,ticket.RequestedY,Current);
        }
        private bool IntentAuthorityCurrent(WorkerResumeTicket ticket)
        {
            // Runs inside the checked final-original slot as well as after its wrapper.
            // Role/adversary changes without another Move event also defeat old authority.
            return authority!=null && authority(ticket) && native.TryUnit(ticket.UnitId,out var unit) &&
                unit->r_GlobalId==ticket.UnitGlobalId && unit->r_UnitChimp==ticket.UnitType && unit->r_AIState==ticket.AIState &&
                unit->r_ControllableForPlayerId==ticket.Owner && unit->r_TribeId==ticket.TribeId &&
                native.TryTribeGlobal(ticket.TribeId,out uint global) && global==ticket.TribeGlobalId &&
                SafeMode(unit,true) && CurrentCoherent(unit) && Match(ticket,unit,true) &&
                unit->r_TargetTilePositionX2==ticket.RequestedX && unit->r_TargetTilePositionY2==ticket.RequestedY;
        }
        public bool CanProbeOriginal(WorkerResumeTicket ticket)
        {
            var state=Inspect(ticket,false);
            if((state!=WorkerReceiptState.MatchingActive && state!=WorkerReceiptState.MatchingStopped && state!=WorkerReceiptState.MatchingParked) ||
                !native.TryUnit(ticket.UnitId,out var unit))return false;
            return ParkablePose(unit);
        }
        public bool ProbeOriginal(WorkerResumeTicket ticket)
        {
            return CanProbeOriginal(ticket) && Inspect(ticket,false)==WorkerReceiptState.MatchingParked && Reissue(ticket);
        }
        public bool IsSuccessfulPublication(WorkerResumeTicket ticket)
        {
            if(ticket==null || !ticket.IsValid || !native.TryUnit(ticket.UnitId,out var unit) || unit->r_GlobalId!=ticket.UnitGlobalId ||
                unit->r_UnitChimp!=ticket.UnitType || unit->r_AIState!=ticket.AIState || unit->r_ControllableForPlayerId!=ticket.Owner ||
                unit->r_TribeId!=ticket.TribeId || !native.TryTribeGlobal(ticket.TribeId,out uint tribeGlobal) || tribeGlobal!=ticket.TribeGlobalId ||
                !SafeMode(unit,true) || !Match(ticket,unit,true) || !BindingsAlive(ticket.Bindings) ||
                unit->r_TargetTilePositionX2!=ticket.RequestedX || unit->r_TargetTilePositionY2!=ticket.RequestedY ||
                !CurrentCoherent(unit))return false;
            return ((unit->r_PathPlanStateBitFlags==2 || unit->r_PathPlanStateBitFlags==4) && unit->r_PathPlanLength>0) ||
                (unit->r_PathPlanStateBitFlags==0 && unit->r_CurrentTilePositionX==ticket.RequestedX && unit->r_CurrentTilePositionY==ticket.RequestedY);
        }
        public WorkerOrderObservation ObserveOrderPost(WorkerResumeTicket ticket,long nativeResult,out WorkerResumeTicket replacement)
        {
            replacement=null;
            if(ticket==null || !ticket.IsValid)return WorkerOrderObservation.Keep;
            if(!native.TryUnit(ticket.UnitId,out var unit) || unit->r_GlobalId!=ticket.UnitGlobalId)return WorkerOrderObservation.Release;
            // An active route is owned by the just completed native order, even when
            // its job or target differs. Never restore the obsolete receipt's goal.
            if(nativeResult>0 && (unit->r_PathPlanStateBitFlags==2 || unit->r_PathPlanStateBitFlags==4) &&
                unit->r_PathPlanLength>0 && CurrentCoherent(unit))
                return WorkerOrderObservation.Release;
            if(nativeResult>0 && unit->r_PathPlanStateBitFlags==0 && unit->r_CurrentTilePositionX==unit->r_TargetTilePositionX2 &&
                unit->r_CurrentTilePositionY==unit->r_TargetTilePositionY2)return WorkerOrderObservation.Release;
            if(!native.TryTribeGlobal(ticket.TribeId,out uint tribeGlobal))return WorkerOrderObservation.Keep;
            IntPtr pointer=(IntPtr)unit;
            if(!TryPrepareFailedOrderHandoff(ticket,nativeResult,offset=>native.JobWord((GameUnit*)pointer,offset),
                offset=>native.JobDword((GameUnit*)pointer,offset),native.Tile,tribeGlobal,out replacement) ||
                replacement.Command!=null && !CommandCrewAlive(replacement.Command,ticket.Owner))return WorkerOrderObservation.Keep;
            // This is the supervisor's safe boundary after the entire wrapper. A
            // failed foreign order may retain flags2 after clearing the path bytes.
            // Park the fresh live goal, never the obsolete receipt's target.
            return TryPark(replacement,true)?WorkerOrderObservation.Rebind:WorkerOrderObservation.Keep;
        }
        // The real adapter and offline fixture use this same reader-only admission.
        // All machine fields retain NativeRelocation's pinned image/layout contract.
        internal static bool TryPrepareFailedOrderHandoff(WorkerResumeTicket ticket,long nativeResult,
            Func<int,ushort> word,Func<int,uint> dword,Func<int,int,int> tileAt,uint tribeGlobal,out WorkerResumeTicket replacement)
        {
            replacement=null;
            if(ticket==null || !ticket.IsValid || nativeResult!=0 || word==null || dword==null || tileAt==null)return false;
            try
            {
                int flags=word(0x74E),ai=word(0x918),x=word(0x71C),y=word(0x71E);
                if(flags!=0 && flags!=2 && flags!=4 && flags!=5)return false;
                // Existing flags5 already owns its barrier. Adopting stale active or
                // stopped output additionally requires the previously qualified AI.
                if(flags!=5 && ai!=ticket.AIState || dword(0x6F0)!=ticket.UnitGlobalId ||
                    word(0x6E4)!=(ushort)AliveState.IsAlive || word(0x6E6)!=(ushort)ticket.UnitType ||
                    word(0x6EE)!=ticket.Owner || word(0x930)!=ticket.TribeId || tribeGlobal!=ticket.TribeGlobalId ||
                    ticket.TribeId!=0 && dword(0x93C)!=tribeGlobal || dword(0xA20)==0 || word(0x8F8)!=0 ||
                    word(0x6EC)!=0 || word(0x9C8)>1 || word(0x9B0)!=0 || word(0x8EC)!=0 || word(0x8F6)!=0 ||
                    word(0x750)!=8 || dword(0x740)!=0 || tileAt(x,y)<0 || dword(0x72C)!=(uint)tileAt(x,y) ||
                    word(0x738)!=x || word(0x73A)!=y || dword(0x73C)!=dword(0x72C))return false;
                ushort goalX=word(0x744),goalY=word(0x746);
                if(tileAt(goalX,goalY)<0)return false;
                if(ticket.Kind==ResumeReceiptKind.Command)
                {
                    if(!CommandJourneySnapshot.TryCapture(ticket.UnitType,ai,word,dword,out var command) || !SameCrew(ticket.Command,command))return false;
                    replacement=WorkerResumeTicket.ForCommand(ticket.UnitId,ticket.UnitGlobalId,ticket.UnitType,ai,ticket.Owner,
                        ticket.TribeId,tribeGlobal,goalX,goalY,command);
                }
                else
                {
                    if(ai!=ticket.AIState || !ticket.Job.MatchesContinuation(ticket.UnitType,ai,word,dword) ||
                        !WorkerJourneySnapshot.TryCapture(ticket.Job.Rule,word,dword,out var job))return false;
                    replacement=new WorkerResumeTicket(ticket.UnitId,ticket.UnitGlobalId,ticket.UnitType,ai,ticket.Owner,
                        ticket.TribeId,tribeGlobal,goalX,goalY,job,ticket.Bindings);
                }
                if(replacement.IsValid)return true;replacement=null;return false;
            }
            catch{replacement=null;return false;}
        }
        private static bool SameCrew(CommandJourneySnapshot first,CommandJourneySnapshot second)
        {
            if(first==null || second==null || first.UnitType!=second.UnitType)return false;
            if(!CommandResumePolicy.IsSiege(first.UnitType))return true;
            for(int i=0;i<4;i++)
            {
                if(!first.TryGetValue(0x96C+i*2,out var id) || !second.TryGetValue(0x96C+i*2,out var otherId) || id!=otherId ||
                    !first.TryGetValue(0x974+i*4,out var global) || !second.TryGetValue(0x974+i*4,out var otherGlobal) || global!=otherGlobal)return false;
            }
            return true;
        }
    }
}
