using System;

namespace TrappedUnitRescueTest
{
    internal interface IRescueReplanIntentWorld
    {
        bool TryCapture(int unitId,int x,int y,int extra,out RescueReplanIntent intent);
        bool IsCurrent(RescueReplanIntent intent);
        bool TryGetSnapshot(RescueReplanIntent intent,out UnitSnapshot exact);
    }
    // An unreserved, read-only intent receipt. It grants no geometry, order or
    // mutation authority; the coordinator owns world/serial/result/age gates.
    internal sealed class RescueReplanIntent
    {
        private readonly UnitSnapshot before;
        internal readonly WorkerResumeTicket Receipt;
        internal int UnitId=>Receipt.UnitId;
        internal uint GlobalId=>Receipt.UnitGlobalId;
        internal RescueReplanIntent(UnitSnapshot snapshot,WorkerResumeTicket receipt)
        {before=CopySnapshot(snapshot);before.Candidates.Clear();before.GeometryWitness=null;Receipt=receipt;}
        internal UnitSnapshot CopyBefore()=>CopySnapshot(before);
        internal static UnitSnapshot CopySnapshot(UnitSnapshot source)
        {
            if(source==null)return null;
            var copy=new UnitSnapshot{Id=source.Id,Type=source.Type,Owner=source.Owner,Tribe=source.Tribe,
                X=source.X,Y=source.Y,TileId=source.TileId,AIState=source.AIState,Flags=source.Flags,Substep=source.Substep,
                RequestedX=source.RequestedX,RequestedY=source.RequestedY,CrewCount=source.CrewCount,
                NextX=source.NextX,NextY=source.NextY,NextTileId=source.NextTileId,OrdinaryStep=source.OrdinaryStep,
                AffectedCurrent=source.AffectedCurrent,AffectedNext=source.AffectedNext,CurrentWasOrdinary=source.CurrentWasOrdinary,
                NextWasOrdinary=source.NextWasOrdinary,GroundFlags=source.GroundFlags,NextGroundFlags=source.NextGroundFlags,
                GlobalId=source.GlobalId,Health=source.Health,TribeGlobalId=source.TribeGlobalId,
                HasCapturedVegetation=source.HasCapturedVegetation,CapturedVegetationGlobal=source.CapturedVegetationGlobal,
                Job=source.Job,Command=source.Command,Connections=source.Connections,Unsupported=source.Unsupported,GeometryWitness=source.GeometryWitness,
                CrewIdentity=source.CrewIdentity==null?null:(uint[])source.CrewIdentity.Clone()};
            copy.Candidates.AddRange(source.Candidates);return copy;
        }
    }
    internal readonly struct RescueReplanPose
    {
        internal readonly uint Global,Health,PendingStep;
        internal readonly int X,Y,Tile,NextX,NextY,NextTile,Flags,Substep;
        internal readonly bool OrdinaryMode;
        internal RescueReplanPose(uint global,uint health,bool ordinaryMode,int x,int y,int tile,int nextX,int nextY,int nextTile,
            int flags,int substep,uint pendingStep)
        {Global=global;Health=health;OrdinaryMode=ordinaryMode;X=x;Y=y;Tile=tile;NextX=nextX;NextY=nextY;NextTile=nextTile;
            Flags=flags;Substep=substep;PendingStep=pendingStep;}
    }
    internal delegate bool RescueReplanPoseReader(int unitId,out RescueReplanPose pose);

    // Same pure orchestration is exercised offline with typed native-word snapshots.
    // These accessors must be synchronous/read-only under the simulation owner.
    internal sealed class RescueReplanIntentWorld : IRescueReplanIntentWorld
    {
        private readonly Func<int,UnitSnapshot> capture;
        private readonly RescueReplanPoseReader readPose;
        private readonly IWorkerResumeWorld world;
        private readonly Func<int,int,int> tileAt;
        internal RescueReplanIntentWorld(Func<int,UnitSnapshot> capture,RescueReplanPoseReader readPose,
            IWorkerResumeWorld world,Func<int,int,int> tileAt)
        {this.capture=capture??throw new ArgumentNullException(nameof(capture));this.readPose=readPose??throw new ArgumentNullException(nameof(readPose));
            this.world=world??throw new ArgumentNullException(nameof(world));this.tileAt=tileAt??throw new ArgumentNullException(nameof(tileAt));}
        public bool TryCapture(int unitId,int x,int y,int extra,out RescueReplanIntent intent)
        {
            intent=null;if(extra!=0 || unitId<1 || unitId>NativeRelocation.MaximumUnitId)return false;
            try
            {
                var before=capture(unitId);
                if(before==null || before.Id!=unitId || before.Unsupported!=null || before.RequestedX!=x || before.RequestedY!=y ||
                    tileAt(x,y)<0 || (before.Flags!=2 && before.Flags!=4) || !readPose(unitId,out var pose) ||
                    !SameSnapshotPose(before,pose) || !CoherentPose(pose) ||
                    before.OrdinaryStep!=(pose.Substep<8) || !world.TryCapture(before,out var receipt,out _) ||
                    !MatchesSnapshot(receipt,before))return false;
                var exact=RescueReplanIntent.CopySnapshot(before);exact.Job=receipt.Job;exact.Command=receipt.Command;
                intent=new RescueReplanIntent(exact,receipt);return true;
            }
            catch{return false;}
        }
        public bool IsCurrent(RescueReplanIntent intent)
        {try{return CanRefresh(intent,out _,out _);}catch{return false;}}
        public bool TryGetSnapshot(RescueReplanIntent intent,out UnitSnapshot exact)
        {
            exact=null;
            try
            {
                if(!CanRefresh(intent,out var before,out var pose))return false;
                // Preserve fresh PRE pose for GeometryJoin and admissible original-step
                // completion. Only health and audited harmless job timers are refreshed.
                before.Health=pose.Health;
                if(!world.TryCapture(before,out var current,out _) || !MatchesSnapshot(current,before) ||
                    !SameReceiptIdentity(intent.Receipt,current) || !SameBindings(intent.Receipt.Bindings,current.Bindings) ||
                    !SameCapturedIntent(intent.Receipt,current))return false;
                before.Job=current.Job;before.Command=current.Command;exact=before;return true;
            }
            catch{return false;}
        }
        private bool CanRefresh(RescueReplanIntent intent,out UnitSnapshot before,out RescueReplanPose pose)
        {
            before=null;pose=default;if(intent==null || intent.Receipt==null || !intent.Receipt.IsValid)return false;
            before=intent.CopyBefore();
            if(before.Unsupported!=null || before.Flags!=2 && before.Flags!=4 || !MatchesSnapshot(intent.Receipt,before) ||
                !readPose(intent.UnitId,out pose) || !CoherentPose(pose) || pose.Global!=intent.GlobalId ||
                !NativeWorkerResumeWorld.AdmissibleCapturePosition(before,pose.Tile,pose.X,pose.Y,pose.Flags,pose.Substep,
                    pose.NextTile,pose.NextX,pose.NextY) || pose.Substep<8 && pose.Substep<before.Substep)return false;
            var state=world.Inspect(intent.Receipt,true);
            return state==WorkerReceiptState.MatchingActive || state==WorkerReceiptState.MatchingStopped;
        }
        private bool CoherentPose(RescueReplanPose pose)
        {
            if(!pose.OrdinaryMode || pose.Health==0 || pose.Substep<0 || pose.Substep>8 ||
                pose.Flags!=0 && pose.Flags!=2 && pose.Flags!=4 || tileAt(pose.X,pose.Y)<0 ||
                tileAt(pose.X,pose.Y)!=pose.Tile || tileAt(pose.NextX,pose.NextY)<0 || tileAt(pose.NextX,pose.NextY)!=pose.NextTile)return false;
            if(pose.Substep==8)return pose.PendingStep==0 && pose.X==pose.NextX && pose.Y==pose.NextY && pose.Tile==pose.NextTile;
            return (pose.Flags==2 || pose.Flags==4) && (pose.X!=pose.NextX || pose.Y!=pose.NextY) &&
                Math.Abs(pose.X-pose.NextX)<=1 && Math.Abs(pose.Y-pose.NextY)<=1;
        }
        private static bool SameSnapshotPose(UnitSnapshot before,RescueReplanPose pose)=>before.GlobalId==pose.Global && before.Health==pose.Health &&
            before.X==pose.X && before.Y==pose.Y && before.TileId==pose.Tile && before.NextX==pose.NextX && before.NextY==pose.NextY &&
            before.NextTileId==pose.NextTile && before.Flags==pose.Flags && before.Substep==pose.Substep;
        private static bool MatchesSnapshot(WorkerResumeTicket ticket,UnitSnapshot snapshot)
        {
            if(ticket==null || !ticket.IsValid || snapshot==null || snapshot.GlobalId!=ticket.UnitGlobalId || snapshot.Id!=ticket.UnitId ||
                snapshot.Type!=(int)ticket.UnitType || snapshot.AIState!=ticket.AIState || snapshot.Owner!=ticket.Owner ||
                snapshot.Tribe!=ticket.TribeId || snapshot.TribeGlobalId!=ticket.TribeGlobalId ||
                snapshot.RequestedX!=ticket.RequestedX || snapshot.RequestedY!=ticket.RequestedY)return false;
            if(ticket.Kind==ResumeReceiptKind.Command)return snapshot.Job==null && snapshot.Command!=null;
            if(ticket.Kind!=ResumeReceiptKind.Worker || snapshot.Job==null || snapshot.Command!=null)return false;
            bool vegetation=(ticket.Bindings.Required&WorkerExternalBindings.Vegetation)!=0;
            return vegetation?snapshot.HasCapturedVegetation && snapshot.CapturedVegetationGlobal==ticket.Bindings.VegetationGlobal:
                !snapshot.HasCapturedVegetation;
        }
        private static bool SameReceiptIdentity(WorkerResumeTicket before,WorkerResumeTicket current)=>before.Kind==current.Kind &&
            before.UnitId==current.UnitId && before.UnitGlobalId==current.UnitGlobalId && before.UnitType==current.UnitType && before.AIState==current.AIState &&
            before.Owner==current.Owner && before.TribeId==current.TribeId && before.TribeGlobalId==current.TribeGlobalId &&
            before.RequestedX==current.RequestedX && before.RequestedY==current.RequestedY;
        private static bool SameBindings(WorkerBindingSnapshot first,WorkerBindingSnapshot second)=>first!=null && second!=null &&
            first.Required==second.Required && first.WorkplaceId==second.WorkplaceId && first.WorkplaceGlobal==second.WorkplaceGlobal &&
            first.StoreId==second.StoreId && first.StoreGlobal==second.StoreGlobal && first.VegetationId==second.VegetationId && first.VegetationGlobal==second.VegetationGlobal;
        private static bool SameCapturedIntent(WorkerResumeTicket before,WorkerResumeTicket current)
        {
            if(before.Kind==ResumeReceiptKind.Worker)
            {
                var first=before.Job.CopyValues();var second=current.Job.CopyValues();if(first.Length!=second.Length)return false;
                for(int i=0;i<first.Length;i++)
                    if(before.Job.Rule.Field(i).NativeOffset!=current.Job.Rule.Field(i).NativeOffset ||
                        before.Job.Rule.Field(i).Width!=current.Job.Rule.Field(i).Width ||
                        !before.Job.Rule.MayAdvanceWhileWaiting(i) && first[i]!=second[i])return false;
                return true;
            }
            var oldValues=before.Command.CopyValues();var newValues=current.Command.CopyValues();if(oldValues.Length!=newValues.Length)return false;
            for(int i=0;i<oldValues.Length;i++)if(before.Command.Field(i).NativeOffset!=current.Command.Field(i).NativeOffset ||
                before.Command.Field(i).Width!=current.Command.Field(i).Width || oldValues[i]!=newValues[i])return false;
            return true;
        }
    }
    internal sealed unsafe class NativeRescueReplanIntentWorld : IRescueReplanIntentWorld
    {
        private readonly NativeRelocation native;
        private readonly RescueReplanIntentWorld core;
        internal NativeRescueReplanIntentWorld(RescueRuntime runtime,NativeWorkerResumeWorld world,Func<int,UnitSnapshot> capture=null)
        {
            if(runtime==null)throw new ArgumentNullException(nameof(runtime));native=runtime.Native;
            core=new RescueReplanIntentWorld(capture??(id=>runtime.SnapshotUnit(id,true,false)),ReadPose,world,native.Tile);
        }
        private bool ReadPose(int id,out RescueReplanPose pose)
        {
            pose=default;if(!native.TryUnit(id,out var unit))return false;
            bool ordinary=native.Word(unit,0x6EC)==0 && native.Word(unit,0x9C8)==0 && native.Word(unit,0x9B0)==0 &&
                native.Word(unit,0x8EC)==0 && native.Word(unit,0x8F6)==0 && unit->r_IsKilledByProjectile==0;
            pose=new RescueReplanPose(unit->r_GlobalId,unit->r_CurrentHealth,ordinary,unit->r_CurrentTilePositionX,unit->r_CurrentTilePositionY,
                (int)unit->r_CurrentPositionTileId,unit->r_NextTilePositionX2,unit->r_NextTilePositionY2,(int)unit->r_NextPositionTileId2,
                unit->r_PathPlanStateBitFlags,unit->r_MovementSubstep,native.JobDword(unit,0x740));return true;
        }
        public bool TryCapture(int unitId,int x,int y,int extra,out RescueReplanIntent intent)=>core.TryCapture(unitId,x,y,extra,out intent);
        public bool IsCurrent(RescueReplanIntent intent)=>core.IsCurrent(intent);
        public bool TryGetSnapshot(RescueReplanIntent intent,out UnitSnapshot exact)=>core.TryGetSnapshot(intent,out exact);
    }
}
