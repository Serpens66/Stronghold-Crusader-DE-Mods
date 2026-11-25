using System;
using System.Collections.Generic;

namespace TrappedUnitRescueTest
{
    internal readonly struct RescueGeometryEndpoint
    {
        internal readonly int X,Y,Tile;
        internal readonly bool Affected,WasOrdinary;
        internal readonly uint GroundFlags;
        internal RescueGeometryEndpoint(int x,int y,int tile,bool affected,bool wasOrdinary,uint groundFlags)
        {X=x;Y=y;Tile=tile;Affected=affected;WasOrdinary=wasOrdinary;GroundFlags=groundFlags;}
        internal bool Matches(int x,int y,int tile)=>X==x && Y==y && Tile==tile;
    }

    // Historical spatial proof only. Lifetime/session/world/age/attempt ownership
    // belongs to the caller; no former AI, job, group, goal or crew is retained.
    internal sealed class RescueGeometryWitness
    {
        // CaptureCandidates searches the17x17 local square and may include Current.
        internal const int MaximumCandidates=289;
        internal readonly int UnitId,Type,Owner;
        internal readonly uint GlobalId;
        internal readonly RescueGeometryEndpoint Current,Next;
        internal readonly bool HasAcceptedNext;
        private readonly RescueCandidate[] candidates;
        internal int CandidateCount=>candidates.Length;
        internal RescueCandidate[] CopyCandidates()=>(RescueCandidate[])candidates.Clone();
        private RescueGeometryWitness(int id,uint global,int type,int owner,RescueGeometryEndpoint current,
            RescueGeometryEndpoint next,bool hasAcceptedNext,RescueCandidate[] candidates)
        {UnitId=id;GlobalId=global;Type=type;Owner=owner;Current=current;Next=next;HasAcceptedNext=hasAcceptedNext;this.candidates=candidates;}

        internal static bool TryCapture(UnitSnapshot original,out RescueGeometryWitness witness)
        {
            witness=null;if(original==null)return false;
            bool accepted=original.OrdinaryStep;
            if(accepted && ((original.Flags!=2 && original.Flags!=4) || original.Substep<0 || original.Substep>=8))return false;
            var current=new RescueGeometryEndpoint(original.X,original.Y,original.TileId,original.AffectedCurrent,original.CurrentWasOrdinary,original.GroundFlags);
            var next=new RescueGeometryEndpoint(original.NextX,original.NextY,original.NextTileId,original.AffectedNext,original.NextWasOrdinary,original.NextGroundFlags);
            return TryCreate(original.Id,original.GlobalId,original.Type,original.Owner,current,next,accepted,original.Candidates,out witness);
        }
        // The scalar form supports a geometry-only save record. The caller must
        // still establish its original source/world/lifetime/age contract.
        internal static bool TryCreate(int unitId,uint globalId,int type,int owner,RescueGeometryEndpoint current,
            RescueGeometryEndpoint next,bool hasAcceptedNext,IList<RescueCandidate> oldCandidates,out RescueGeometryWitness witness)
        {
            witness=null;
            if(unitId<1 || unitId>NativeRelocation.MaximumUnitId || globalId==0 || !Valid(current) || oldCandidates==null || oldCandidates.Count>MaximumCandidates ||
                hasAcceptedNext && (!Valid(next) || !Adjacent(current,next)) ||
                !(current.Affected && current.WasOrdinary || hasAcceptedNext && next.Affected && next.WasOrdinary))return false;
            var copy=new RescueCandidate[oldCandidates.Count];var seen=new HashSet<int>();
            for(int i=0;i<copy.Length;i++)
            {
                var c=oldCandidates[i];
                if(!XY(c.X,c.Y) || !Tile(c.Tile) || Math.Abs(c.X-current.X)>8 || Math.Abs(c.Y-current.Y)>8 ||
                    c.Distance<0 || c.Distance>MaximumCandidates || !seen.Add(c.Tile) ||
                    c.Distance==0 && !current.Matches(c.X,c.Y,c.Tile))return false;
                copy[i]=c;
            }
            witness=new RescueGeometryWitness(unitId,globalId,type,owner,current,hasAcceptedNext?next:current,hasAcceptedNext,copy);return true;
        }
        internal static bool TryJoin(RescueGeometryWitness witness,UnitSnapshot freshPre,out UnitSnapshot joined)
        {
            joined=null;
            if(witness==null || freshPre==null || freshPre.Id!=witness.UnitId || freshPre.GlobalId!=witness.GlobalId ||
                freshPre.Type!=witness.Type || freshPre.Owner!=witness.Owner ||
                (freshPre.Flags!=2 && freshPre.Flags!=4) || freshPre.Substep<0 || freshPre.Substep>8)return false;
            bool atCurrent=witness.Current.Matches(freshPre.X,freshPre.Y,freshPre.TileId);
            bool atNext=witness.HasAcceptedNext && witness.Next.Matches(freshPre.X,freshPre.Y,freshPre.TileId);
            if(!atCurrent && !atNext)return false;
            RescueGeometryEndpoint current=atCurrent?witness.Current:witness.Next,next;
            if(freshPre.Substep==8)
            {
                if(freshPre.OrdinaryStep || freshPre.NextX!=freshPre.X || freshPre.NextY!=freshPre.Y || freshPre.NextTileId!=freshPre.TileId)return false;
                next=current;
            }
            else
            {
                // Only the originally accepted edge carries historical authority.
                // Being already at its end cannot authorize another successor.
                if(!freshPre.OrdinaryStep || !atCurrent || !witness.HasAcceptedNext ||
                    !witness.Next.Matches(freshPre.NextX,freshPre.NextY,freshPre.NextTileId))return false;
                next=witness.Next;
            }
            joined=new UnitSnapshot
            {
                Id=freshPre.Id,GlobalId=freshPre.GlobalId,Type=freshPre.Type,Owner=freshPre.Owner,
                Tribe=freshPre.Tribe,TribeGlobalId=freshPre.TribeGlobalId,Health=freshPre.Health,AIState=freshPre.AIState,
                X=freshPre.X,Y=freshPre.Y,TileId=freshPre.TileId,NextX=freshPre.NextX,NextY=freshPre.NextY,NextTileId=freshPre.NextTileId,
                Flags=freshPre.Flags,Substep=freshPre.Substep,OrdinaryStep=freshPre.OrdinaryStep,
                RequestedX=freshPre.RequestedX,RequestedY=freshPre.RequestedY,Job=freshPre.Job,Command=freshPre.Command,
                HasCapturedVegetation=freshPre.HasCapturedVegetation,CapturedVegetationGlobal=freshPre.CapturedVegetationGlobal,
                GeometryWitness=witness,
                CrewCount=freshPre.CrewCount,CrewIdentity=freshPre.CrewIdentity==null?null:(uint[])freshPre.CrewIdentity.Clone(),
                Connections=freshPre.Connections,Unsupported=freshPre.Unsupported,
                AffectedCurrent=current.Affected,CurrentWasOrdinary=current.WasOrdinary,GroundFlags=current.GroundFlags,
                AffectedNext=freshPre.OrdinaryStep && next.Affected,NextWasOrdinary=next.WasOrdinary,NextGroundFlags=next.GroundFlags
            };
            joined.Candidates.AddRange(witness.candidates);return true;
        }
        private static bool XY(int x,int y)=>(uint)x<800 && (uint)y<800;
        private static bool Tile(int tile)=>(uint)tile<NativeRelocation.TileCapacity;
        private static bool Valid(RescueGeometryEndpoint e)=>XY(e.X,e.Y) && Tile(e.Tile);
        private static bool Adjacent(RescueGeometryEndpoint a,RescueGeometryEndpoint b)=>a.Tile!=b.Tile &&
            (a.X!=b.X || a.Y!=b.Y) && Math.Abs(a.X-b.X)<=1 && Math.Abs(a.Y-b.Y)<=1;
    }
}
