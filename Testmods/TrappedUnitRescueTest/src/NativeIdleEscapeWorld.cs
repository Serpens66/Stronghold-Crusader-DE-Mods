using System;
using APIShared.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal delegate bool IdleEscapeNativeMove(int id,int x,int y,Func<bool> authorityCurrent,out UnitMoveRunResult result);
    // Reads are valid only in the caller's synchronous simulation scope. Tickets
    // contain scalar evidence, never native pointers or reconstructed worker jobs.
    internal sealed unsafe class NativeIdleEscapeWorld : IIdleEscapeWorld,IUnitMoveObserver
    {
        private readonly NativeRelocation native;
        private readonly Func<IdleEscapeRequest,bool> authority;
        private readonly IdleEscapeNativeMove move;
        private readonly string ownerGuid;
        private ArmedMove armed;
        internal bool IsArmed=>armed!=null;
        private sealed class ArmedMove {internal Func<bool> Owned;internal IdleEscapeSourcePhase Phase;}
        private static readonly int[] Words={0x6E4,0x6E6,0x6E8,0x6EC,0x6EE,0x708,0x748,0x74A,0x8F6,0x8F8,0x8FC,
            0x918,0x91A,0x930,0x934,0x936,0x956,0x958,0x95A,0x98E,0x996,0x99C,0x9B0,0x9B8,
            0x9E0,0x9F4,0x9F6,0xA40,0xA42,0xA7E,0xA82,0xA86,0xA9E,0xAA4};
        private static readonly int[] Dwords={0x6F0,0x6F8,0x93C,0x998,0x9F8,0xA00};
        internal NativeIdleEscapeWorld(NativeRelocation native,string ownerGuid,Func<IdleEscapeRequest,bool> authorityCurrent,IdleEscapeNativeMove move)
        {
            this.native=native??throw new ArgumentNullException(nameof(native));
            if(string.IsNullOrWhiteSpace(ownerGuid))throw new ArgumentException(nameof(ownerGuid));this.ownerGuid=ownerGuid;
            authority=authorityCurrent??throw new ArgumentNullException(nameof(authorityCurrent));this.move=move??throw new ArgumentNullException(nameof(move));
        }
        private bool Authority(IdleEscapeTicket ticket)=>ticket!=null && authority(ticket.Request);
        private bool CurrentCoherent(GameUnit* u)=>native.Tile(u->r_CurrentTilePositionX,u->r_CurrentTilePositionY)==(int)u->r_CurrentPositionTileId;
        private bool Stationary(GameUnit* u)=>u->r_PathPlanStateBitFlags==0 && u->r_MovementSubstep==8 && CurrentCoherent(u) &&
            u->r_CurrentTilePositionX==u->r_NextTilePositionX2 && u->r_CurrentTilePositionY==u->r_NextTilePositionY2 &&
            u->r_CurrentPositionTileId==u->r_NextPositionTileId2 && native.JobDword(u,0x740)==0;
        private bool CandidateSafe(IdleEscapeRequest request,bool arrived=false)
        {
            var c=request.Candidate;
            return native.Tile(c.X,c.Y)==c.Tile && c.Tile!=request.Before.TileId && native.OrdinaryGround(c.Tile) && HasPhysicalExit(c) &&
                (arrived?native.HasOccupancyExactlyOnce(c.Tile,request.Before.Id):native.Head(c.Tile)==0);
        }
        private static readonly int[] Dx={0,1,1,1,0,-1,-1,-1},Dy={-1,-1,0,1,1,1,0,-1};
        private bool HasPhysicalExit(RescueCandidate c)=>HasPhysicalExit(c,native.Tile,native.OrdinaryGround,native.Connections);
        internal static bool HasPhysicalExit(RescueCandidate c,Func<int,int,int> tile,Func<int,bool> ordinary,Func<int,byte> connectionsAt)
        {
            int connections=connectionsAt(c.Tile);
            for(int d=0;d<8;d++)
            {
                int next=tile(c.X+Dx[d],c.Y+Dy[d]);
                if(next>=0 && ordinary(next) &&
                    ((connections&(1<<d))!=0 || (connectionsAt(next)&(1<<(d^4)))!=0))return true;
            }
            return false;
        }
        private uint[] Capture(GameUnit* u)
        {
            bool siege=CommandResumePolicy.IsSiege(u->r_UnitChimp);
            var result=new uint[Words.Length+Dwords.Length+(siege?12:0)];int at=0;
            foreach(int offset in Words)result[at++]=native.Word(u,offset);
            foreach(int offset in Dwords)result[at++]=native.JobDword(u,offset);
            if(siege)
            {
                for(int i=0;i<4;i++)result[at++]=native.Word(u,0x96C+i*2);
                for(int i=0;i<4;i++)result[at++]=native.JobDword(u,0x974+i*4);
                result[at++]=native.Word(u,0xA0C);result[at++]=native.JobDword(u,0xA10);
                result[at++]=native.Word(u,0x748);result[at++]=native.Word(u,0x74A);
            }
            return result;
        }
        private bool Match(IdleEscapeTicket ticket,GameUnit* u)
        {
            var b=ticket.Request.Before;
            if(!Authority(ticket) || u->r_GlobalId!=b.GlobalId || (int)u->r_UnitChimp!=b.Type || u->r_ControllableForPlayerId!=b.Owner ||
                u->r_TribeId!=b.Tribe || !native.TryTribeGlobal(b.Tribe,out uint global) || global!=b.TribeGlobalId ||
                b.Tribe!=0 && native.JobDword(u,0x93C)!=global || !CrewValid(ticket.Request,u))return false;
            var current=Capture(u);if(current.Length!=ticket.Semantic.Length)return false;
            for(int i=0;i<current.Length;i++)if(current[i]!=ticket.Semantic[i])return false;
            return u->r_CurrentHealth>0 && u->r_IsKilledByProjectile==0;
        }
        private bool CrewValid(IdleEscapeRequest r,GameUnit* u)
        {
            if(!CommandResumePolicy.IsSiege(u->r_UnitChimp))return true;
            int count=CommandJourneySnapshot.RequiredCrewCount(u->r_UnitChimp);
            if(!r.InitialCrewAliasProof || count==0 || r.Before.CrewCount!=count || native.Word(u,0xA0C)!=count || r.Before.CrewIdentity==null || r.Before.CrewIdentity.Length!=8)return false;
            for(int i=0;i<4;i++)
            {
                int id=native.Word(u,0x96C+i*2);uint global=native.JobDword(u,0x974+i*4);
                if(r.Before.CrewIdentity[i*2]!=id || r.Before.CrewIdentity[i*2+1]!=global || (id!=0)!=(i<count))return false;
                if(i<count && (!native.TryUnit(id,out var crew) || crew->r_GlobalId!=global || crew->r_UnitChimp!=eChimps.CHIMP_TYPE_ENGINEER ||
                    crew->r_AIState!=5 || native.Word(crew,0x9B0)==0 || crew->r_ControllableForPlayerId!=r.Before.Owner))return false;
                for(int j=0;j<i;j++)if(id!=0 && id==native.Word(u,0x96C+j*2))return false;
            }
            return true;
        }
        public bool TryCapture(IdleEscapeRequest r,out IdleEscapeTicket ticket,out string reason)
        {
            ticket=null;reason="idle-moat-entry-rejected";
            var b=r?.Before;
            if(b==null || r.Session<=0 || r.Generation<0 || r.OrderSerial<0 || !authority(r) || b.Unsupported!=null || b.Job!=null ||
                b.Flags!=0 || b.Substep!=8 || b.RequestedX!=b.X || b.RequestedY!=b.Y || !b.CurrentWasOrdinary || !b.AffectedCurrent ||
                (b.GroundFlags&0x40000000u)!=0 || !native.TryUnit(b.Id,out var u) || !Stationary(u) ||
                u->r_GlobalId!=b.GlobalId || (int)u->r_UnitChimp!=b.Type || u->r_AIState!=b.AIState || u->r_ControllableForPlayerId!=b.Owner ||
                u->r_TribeId!=b.Tribe || u->r_CurrentHealth!=b.Health || u->r_CurrentTilePositionX!=b.X || u->r_CurrentTilePositionY!=b.Y ||
                (int)u->r_CurrentPositionTileId!=b.TileId || u->r_TargetTilePositionX2!=b.X || u->r_TargetTilePositionY2!=b.Y ||
                !CommandResumePolicy.IsIndependentRescueType(u->r_UnitChimp) || u->r_UnitChimp==eChimps.CHIMP_TYPE_ENGINEER || native.Word(u,0xA9E)!=1 ||
                !(u->r_AIState==0 || u->r_UnitChimp==eChimps.CHIMP_TYPE_SPEARMAN && u->r_AIState==2) ||
                WorkerTravelPolicy.TryDescribe(u->r_UnitChimp,u->r_AIState,out _) || native.Word(u,0x6EC)!=0 ||
                native.Word(u,0x708)!=0 || native.Word(u,0x8F8)!=0 || native.Word(u,0x8EC)!=0 || native.Word(u,0x8F6)!=0 ||
                native.Word(u,0x9B0)!=0 || native.Word(u,0x9C8)!=0 ||
                u->r_UnitChimp==eChimps.CHIMP_TYPE_ENGINEER && (native.Word(u,0x9E0)!=0 || native.Word(u,0xAA4)!=0) ||
                native.Word(u,0x9F4)==(ushort)TribeAICommand.DigMoatTileId || native.Word(u,0x9F4)==(ushort)TribeAICommand.Unknown7 ||
                native.Word(u,0x9F6)!=0 || native.JobDword(u,0xA00)!=0 || u->r_IsKilledByProjectile!=0 ||
                !native.TryTribeGlobal(b.Tribe,out uint global) || global!=b.TribeGlobalId || b.Tribe!=0 && native.JobDword(u,0x93C)!=global ||
                (native.Flags(b.TileId)&0x40000100u)!=0x40000000u || native.Building(b.TileId)!=0 ||
                !native.HasOccupancyExactlyOnce(b.TileId,b.Id) || !CandidateSafe(r) || !CrewValid(r,u))return false;
            bool oldReachable=false;
            foreach(var c in b.Candidates)if(c.Tile==r.Candidate.Tile && c.X==r.Candidate.X && c.Y==r.Candidate.Y){oldReachable=true;break;}
            if(!oldReachable)return false;
            IntPtr pointer=(IntPtr)u;
            if(b.Command!=null && !b.Command.Matches(u->r_UnitChimp,u->r_AIState,offset=>native.JobWord((GameUnit*)pointer,offset),offset=>native.JobDword((GameUnit*)pointer,offset)))return false;
            ticket=new IdleEscapeTicket(r,Capture(u));reason=null;return true;
        }
        private bool CanIssue(IdleEscapeTicket t)
        {
            var b=t.Request.Before;
            return native.TryUnit(t.UnitId,out var u) && Match(t,u) && Stationary(u) &&
                (int)u->r_CurrentPositionTileId==b.TileId && u->r_TargetTilePositionX2==b.X && u->r_TargetTilePositionY2==b.Y &&
                native.Word(u,0x8EC)==0 && native.Word(u,0x9C8)==0 && (native.Flags(b.TileId)&0x40000100u)==0x40000000u &&
                native.Building(b.TileId)==0 && native.HasOccupancyExactlyOnce(b.TileId,t.UnitId) && CandidateSafe(t.Request);
        }
        public IdleEscapeMove Move(IdleEscapeTicket t,Func<bool> stillOwned)
        {
            if(armed!=null || !Authority(t) || stillOwned==null || !stillOwned() || !CanIssue(t))return IdleEscapeMove.Other;
            var c=t.Request.Candidate;
            var frame=new ArmedMove{Owned=stillOwned,Phase=new IdleEscapeSourcePhase(ownerGuid,t.Request)};armed=frame;
            try
            {
                // Broker calls this before Pre, after all final-original observers,
                // and after Post. Only its exact completion ends the source phase.
                move(t.UnitId,c.X,c.Y,()=>ReferenceEquals(armed,frame) && frame.Owned() && Authority(t) &&
                    (frame.Phase.Finished || CanIssue(t)),out var result);
                return ClassifyMove(result,stillOwned() && Authority(t));
            }
            finally {armed=null;}
        }
        public IUnitMoveInvocation TryBegin(UnitMoveContext context)=>armed?.Phase.TryBegin(context);
        internal static IdleEscapeMove ClassifyMove(UnitMoveRunResult result,bool current)
        {
            if(result==null || !current || !result.IsCurrentWorld || result.Failure!=null ||
                !result.NativeInvoked || !result.NativeReturned)return IdleEscapeMove.Other;
            return result.Status==UnitMoveRunStatus.NativeSucceeded && result.NativeResult==1?IdleEscapeMove.NativeSucceeded:
                result.Status==UnitMoveRunStatus.NativeRefused && result.NativeResult==0 && result.WrapperReturned && result.WrapperFailure==null?
                IdleEscapeMove.NativeRefused:IdleEscapeMove.Other;
        }
        private bool OwnGoal(IdleEscapeTicket t,GameUnit* u)=>u->r_TargetTilePositionX2==t.Request.Candidate.X && u->r_TargetTilePositionY2==t.Request.Candidate.Y;
        public bool HasPublishedRoute(IdleEscapeTicket t)=>native.TryUnit(t.UnitId,out var u) && Match(t,u) && OwnGoal(t,u) && CurrentCoherent(u) &&
            ((u->r_PathPlanLength>0 && (u->r_PathPlanStateBitFlags==2 || u->r_PathPlanStateBitFlags==4) && u->r_MovementSubstep<=8 &&
                native.Word(u,0x752)<=u->r_PathPlanLength && (u->r_MovementSubstep==8 ||
                native.Tile(u->r_NextTilePositionX2,u->r_NextTilePositionY2)==(int)u->r_NextPositionTileId2 &&
                Math.Abs(u->r_NextTilePositionX2-u->r_CurrentTilePositionX)<=1 && Math.Abs(u->r_NextTilePositionY2-u->r_CurrentTilePositionY)<=1)) ||
             Stationary(u) && (int)u->r_CurrentPositionTileId==t.Request.Candidate.Tile && CandidateSafe(t.Request,true));
        public bool CanFallback(IdleEscapeTicket t)
        {
            if(!native.TryUnit(t.UnitId,out var u) || !Match(t,u) || !Stationary(u) || !OwnGoal(t,u) ||
                (int)u->r_CurrentPositionTileId!=t.Request.Before.TileId || native.Word(u,0x8EC)!=0 || native.Word(u,0x9C8)!=0 ||
                native.Word(u,0xA4C)!=0 || (native.Flags(t.Request.Before.TileId)&0x40000100u)!=0x40000000u ||
                !native.HasOccupancyExactlyOnce(t.Request.Before.TileId,t.UnitId) || !CandidateSafe(t.Request))return false;
            //196280 may replace900/92A, A4C, requested and the inactive1000-byte
            // route slot. None is an old job/route here.199CD0's AI110 arm requires
            // WALL; the freshly completed Moat source explicitly excludes it.
            return true;
        }
        public bool RelocateAndNormalize(IdleEscapeTicket t)
        {
            if(!CanFallback(t))return false;
            var c=t.Request.Candidate;uint health;
            if(!native.TryUnit(t.UnitId,out var before))return false;health=before->r_CurrentHealth;
            return native.Relocate(t.UnitId,c.X,c.Y) && native.TryUnit(t.UnitId,out var u) && Match(t,u) && u->r_CurrentHealth==health &&
                Stationary(u) && OwnGoal(t,u) && native.Word(u,0x752)==0 && native.HasOccupancyCount(t.Request.Before.TileId,t.UnitId,0);
        }
        public IdleEscapeInspection Inspect(IdleEscapeTicket t)
        {
            if(!native.TryUnit(t.UnitId,out var u) || !Match(t,u) || !OwnGoal(t,u) || !CurrentCoherent(u))return IdleEscapeInspection.Changed;
            if(Stationary(u) && (int)u->r_CurrentPositionTileId==t.Request.Candidate.Tile && CandidateSafe(t.Request,true))return IdleEscapeInspection.Arrived;
            return HasPublishedRoute(t)?IdleEscapeInspection.Waiting:IdleEscapeInspection.Changed;
        }
    }
    // Pure invocation phase evidence. This grants no move/relocation authority;
    // actual outcome classification and post-state checks remain mandatory.
    internal sealed class IdleEscapeSourcePhase : IUnitMoveObserver
    {
        private readonly string owner;
        private readonly IdleEscapeRequest request;
        private UnitMoveContext begun;
        internal bool Finished {get;private set;}
        internal IdleEscapeSourcePhase(string owner,IdleEscapeRequest request){this.owner=owner;this.request=request;}
        public IUnitMoveInvocation TryBegin(UnitMoveContext c)
        {
            if(begun!=null || c==null || request==null || c.OriginOwnerGuid!=owner || c.UnitId!=request.Before.Id ||
                c.SessionId!=request.Session || c.WorldGeneration!=request.Generation || c.X!=request.Candidate.X || c.Y!=request.Candidate.Y || c.Extra!=0)return null;
            begun=c;return new Completion(this,c);
        }
        private sealed class Completion : IUnitMoveInvocation
        {
            private readonly IdleEscapeSourcePhase phase;
            private readonly UnitMoveContext context;
            internal Completion(IdleEscapeSourcePhase phase,UnitMoveContext context){this.phase=phase;this.context=context;}
            public void Complete(UnitMoveCompletion completion)
            {if(completion!=null && ReferenceEquals(completion.Context,context))phase.Finished=true;}
        }
    }
}
