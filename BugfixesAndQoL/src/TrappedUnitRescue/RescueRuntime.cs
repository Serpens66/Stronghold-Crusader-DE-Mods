using System;
using System.Collections.Generic;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace BugfixesAndQoL.TrappedUnitRescue
{
    internal enum RescueStatus { Inactive, NotTrapped, Unsupported, NoCandidate, RelocationFailed, IdentityFailed, Rescued, ResumeFailed, EscapeInProgress, RemovalRequested, RemovalDeferred, AwaitResources }
    internal sealed class UnitSnapshot
    {
        internal int Id, Type, Owner, Tribe, X, Y, TileId, AIState, Flags, Substep, RequestedX, RequestedY,CrewCount;
        internal int NextX, NextY, NextTileId;
        internal bool OrdinaryStep, AffectedCurrent, AffectedNext, CurrentWasOrdinary, NextWasOrdinary;
        internal bool CivilianRemoval, CurrentHadExit, NextHadExit;
        internal uint GroundFlags, NextGroundFlags;
        internal uint GlobalId, Health, TribeGlobalId;
        internal bool HasCapturedVegetation;
        internal uint CapturedVegetationGlobal;
        internal WorkerJourneySnapshot Job;
        internal CommandJourneySnapshot Command;
        internal RescueGeometryWitness GeometryWitness;
        internal RescueLocalField LocalField;
        internal byte Connections;
        internal string Unsupported;
        internal uint[] CrewIdentity;
        internal readonly List<RescueCandidate> Candidates = new List<RescueCandidate>();
        // Old geometry/intent remains immutable. Legitimate damage between capture and
        // the admitted recovery tick is not evidence of slot reuse and must not be healed.
        internal UnitSnapshot WithCurrentHealth(uint health)
        {var copy=(UnitSnapshot)MemberwiseClone();copy.Health=health;return copy;}
    }
    internal struct RescueCandidate { internal int X, Y, Tile, Distance; }
    internal sealed class RescueResult
    {
        internal int UnitId, BeforeX, BeforeY, AfterX, AfterY;
        internal uint GlobalId;
        internal RescueStatus Status;
        internal string Detail;
        internal bool IdentityPreserved, ResumeAttempted, ResumeSucceeded;
        public override string ToString() => $"unit={UnitId}/{GlobalId} result={Status} from={BeforeX},{BeforeY} to={AfterX},{AfterY} identity={IdentityPreserved} resume={ResumeAttempted}/{ResumeSucceeded} {Detail}";
    }
    internal sealed unsafe class RescueRuntime
    {
        private readonly NativeRelocation native;
        internal NativeRelocation Native => native;
        private readonly Func<bool> enabled;
        private readonly Action<string> report;
        private readonly Action<UnitSnapshot> candidateCapture;
        internal WorkerResumeSupervisor Workers { get; set; }
        internal Func<long> WorkerTick { get; set; }
        internal Func<bool> WorkerReady { get; set; }
        internal IdleEscapeOwner IdleEscapes { get; set; }
        internal Func<bool> CanRemoveCivilian { get; set; }
        internal Func<UnitSnapshot,RescueCandidate,IdleEscapeRequest> CreateIdleEscapeRequest { get; set; }
        private static readonly int[] Dx = { 0,1,1,1,0,-1,-1,-1 }, Dy = { -1,-1,0,1,1,1,0,-1 };
        internal RescueRuntime(NativeRelocation native, Func<bool> isEnabled, Action<string> report)
        { this.native = native ?? throw new ArgumentNullException(nameof(native)); enabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled)); this.report = report; candidateCapture=CaptureCandidates; }
        internal UnitSnapshot SnapshotUnit(int id) => SnapshotUnit(id, false);
        internal UnitSnapshot SnapshotUnit(int id, bool includeOrdinaryStep,bool collectCandidates=true,int sourceOwner=0)
        {
            if (!native.TryUnit(id, out GameUnit* unit)) return null;
            var value = new UnitSnapshot { Id=id, GlobalId=unit->r_GlobalId, Type=(int)unit->r_UnitChimp, Owner=unit->r_ControllableForPlayerId,
                Tribe=unit->r_TribeId, Health=unit->r_CurrentHealth, X=unit->r_CurrentTilePositionX, Y=unit->r_CurrentTilePositionY,
                TileId=(int)unit->r_CurrentPositionTileId, AIState=unit->r_AIState, Flags=unit->r_PathPlanStateBitFlags, Substep=unit->r_MovementSubstep,
                NextX=unit->r_NextTilePositionX2,NextY=unit->r_NextTilePositionY2,NextTileId=(int)unit->r_NextPositionTileId2,
                RequestedX=unit->r_TargetTilePositionX2, RequestedY=unit->r_TargetTilePositionY2, CrewIdentity=CaptureCrew(unit),
                CrewCount=IsSiege((int)unit->r_UnitChimp)?native.Word(unit,0xA0C):0 };
            value.OrdinaryStep = HasOrdinaryStep(unit);
            value.CivilianRemoval=CivilianRemovalPolicy.IsRemovableType(unit->r_UnitChimp);
            value.Unsupported = value.CivilianRemoval ? CivilianRemovalPolicy.GetExclusion(unit,includeOrdinaryStep) : ClassifyState(unit, includeOrdinaryStep);
            // 11D370 writes unit930/93C together;118660 validates that stored stamp
            // against the current tribe record. Never adopt a recycled group slot.
            if(value.Tribe!=0 && (!native.TryTribeGlobal(value.Tribe,out value.TribeGlobalId) || native.JobDword(unit,0x93C)!=value.TribeGlobalId))
                value.Unsupported="tribe-identity-unavailable";
            if(!value.CivilianRemoval && value.Unsupported==null && WorkerTravelPolicy.TryDescribe(unit->r_UnitChimp,unit->r_AIState,out var rule))
            {
                IntPtr pointer=(IntPtr)unit;
                if(!WorkerJourneySnapshot.TryCapture(rule,offset=>native.JobWord((GameUnit*)pointer,offset),
                    offset=>native.JobDword((GameUnit*)pointer,offset),out value.Job))value.Unsupported="worker-job-snapshot-unavailable";
                else if((rule.RequiredExternalBindings&WorkerExternalBindings.Vegetation)!=0)
                {
                    if(!value.Job.TryGetValue(0x9A0,out uint treeIndex) || !NativeVegetationAccess.TryGet((int)treeIndex,out var tree) ||
                        !NativeVegetationAccess.TryReadAliveGlobal(tree,out value.CapturedVegetationGlobal) ||
                        rule.VegetationSerialNativeOffset!=0 && (!value.Job.TryGetValue(rule.VegetationSerialNativeOffset,out uint serial) || serial!=value.CapturedVegetationGlobal))
                        value.Unsupported="worker-selected-vegetation-identity-unavailable";
                    else value.HasCapturedVegetation=true;
                }
            }
            else if(!value.CivilianRemoval && value.Unsupported==null)
            {
                IntPtr pointer=(IntPtr)unit;
                bool captured=value.Flags==0?
                    CommandJourneySnapshot.TryCaptureStationary(unit->r_UnitChimp,unit->r_AIState,offset=>native.JobWord((GameUnit*)pointer,offset),
                        offset=>native.JobDword((GameUnit*)pointer,offset),out value.Command):
                    CommandJourneySnapshot.TryCapture(unit->r_UnitChimp,unit->r_AIState,offset=>native.JobWord((GameUnit*)pointer,offset),
                        offset=>native.JobDword((GameUnit*)pointer,offset),out value.Command);
                if(!captured)value.Unsupported="command-state-needs-audited-intent";
            }
            if (native.Tile(value.X,value.Y) != value.TileId) value.Unsupported="incoherent-position";
            if(value.OrdinaryStep && native.Tile(value.NextX,value.NextY)!=value.NextTileId)value.Unsupported="incoherent-next-position";
            if (value.TileId < 0 || value.TileId >= NativeRelocation.TileCapacity) { value.Unsupported="invalid-tile"; return value; }
            value.Connections=native.Connections(value.TileId);
            value.GroundFlags=native.Flags(value.TileId);
            value.CurrentWasOrdinary=native.OrdinaryGround(value.TileId);
            value.AffectedCurrent=true; // Explicit fixture snapshots observe their current tile.
            if((uint)value.NextTileId<NativeRelocation.TileCapacity)
            { value.NextGroundFlags=native.Flags(value.NextTileId);value.NextWasOrdinary=native.OrdinaryGround(value.NextTileId); }
            if(value.CivilianRemoval && value.Unsupported==null)
            {
                value.CurrentHadExit=value.CurrentWasOrdinary && HasOrdinaryExit(value.X,value.Y,value.TileId);
                value.NextHadExit=value.OrdinaryStep && value.NextWasOrdinary && HasOrdinaryExit(value.NextX,value.NextY,value.NextTileId);
            }
            if (!value.CivilianRemoval && collectCandidates && value.Unsupported == null && native.OrdinaryGround(value.TileId))
                CaptureCandidatesForOwner(value,sourceOwner,candidateCapture);
            return value;
        }
        // The native source owner has already been validated. Zero means unfiltered;
        // snapshot/identity/chain validation still runs even for discarded owners.
        internal static void CaptureCandidatesForOwner(UnitSnapshot value,int sourceOwner,Action<UnitSnapshot> capture)
        {if(sourceOwner==0 || value.Owner==sourceOwner)capture(value);}
        internal bool TryCaptureMutation(IList<RescueTile> changed,out List<UnitSnapshot> before,out string reason,int sourceOwner=0,bool compactGeometry=false)
        {
            before=new List<UnitSnapshot>();
            List<RescueDiscoveryUnit> occupants;
#if SERP_DEVDIAG
            long discoveryStarted=0;
            Shared.DevDiag.Start(ref discoveryStarted);
            try
            {
#endif
            if(!RescueDiscovery.TryCollect(changed,native.Tile,native.Head,ReadDiscoveryUnit,
                // The halo contains at most9 cells per planned write. Each native
                // unit record is read once and revalidated once by discovery.
                compactGeometry?new RescueDiscoveryBudget(9*2048,2*NativeRelocation.MaximumUnitId,NativeRelocation.MaximumUnitId,
                    NativeRelocation.MaximumUnitId,NativeRelocation.TileCapacity):
                    new RescueDiscoveryBudget(4096,16384,512,NativeRelocation.MaximumUnitId,NativeRelocation.TileCapacity),
                out occupants,out reason))return false;
#if SERP_DEVDIAG
            }
            finally
            {
                Shared.DevDiag.StopSpike(ref discoveryStarted,"rescue.capture.discovery",2000);
            }
#endif
#if SERP_DEVDIAG
            long snapshotsStarted=0;
            Shared.DevDiag.Start(ref snapshotsStarted);
            try
            {
#endif
            var affected=new HashSet<int>();foreach(var tile in changed)affected.Add(tile.TileId);
            // Per invocation only: later child originals may already have changed geometry.
            var fields=compactGeometry?new Dictionary<int,RescueLocalField>():null;
            foreach(var occupant in occupants)
            {
                var snapshot=SnapshotUnit(occupant.UnitId,true,collectCandidates:!compactGeometry,sourceOwner:sourceOwner);
#if SERP_DEVDIAG
                if(snapshot!=null)RescueCaptureDiagnostics.Snapshot(snapshot.Owner,snapshot.Unsupported!=null);
#endif
                if(snapshot==null || snapshot.GlobalId!=occupant.GlobalId || snapshot.TileId!=occupant.CurrentTile ||
                    snapshot.OrdinaryStep && snapshot.NextTileId!=occupant.NextTile)
                {before.Clear();reason="occupant-changed-before-snapshot";return false;}
                snapshot.AffectedCurrent=affected.Contains(snapshot.TileId);
                snapshot.AffectedNext=snapshot.OrdinaryStep && affected.Contains(snapshot.NextTileId);
                if(compactGeometry && !snapshot.CivilianRemoval && snapshot.Unsupported==null && snapshot.CurrentWasOrdinary &&
                    (sourceOwner==0 || snapshot.Owner==sourceOwner))
                {
                    if(!fields.TryGetValue(snapshot.TileId,out var field))
                    {
                        if(!RescueLocalField.TryCapture(snapshot.X,snapshot.Y,snapshot.TileId,native.Tile,native.OrdinaryGround,
                            native.Connections,out field,out reason))
                        {before.Clear();return false;}
                        fields.Add(snapshot.TileId,field);
                    }
                    snapshot.LocalField=field;
                }
                // Cancelling a newly unsafe Next step may keep its still-legal Current tile.
                // This candidate was reachable before the mutation; never manufacture it later.
                if(!compactGeometry && !snapshot.CivilianRemoval && snapshot.OrdinaryStep && native.OrdinaryGround(snapshot.TileId))
                    snapshot.Candidates.Insert(0,new RescueCandidate {X=snapshot.X,Y=snapshot.Y,Tile=snapshot.TileId,Distance=0});
                before.Add(snapshot);
            }
#if SERP_DEVDIAG
            }
            finally
            {
                Shared.DevDiag.StopSpike(ref snapshotsStarted,"rescue.capture.snapshots",2000);
            }
#endif
            return true;
        }
        private bool ReadDiscoveryUnit(int id,out RescueDiscoveryUnit record)
        {
            record=default;if(!native.TryUnit(id,out var unit))return false;
            record=new RescueDiscoveryUnit(id,unit->r_GlobalId,(int)unit->r_CurrentPositionTileId,
                (int)unit->r_NextPositionTileId2,native.Next(unit),HasOrdinaryStep(unit));return true;
        }
        internal static bool HasOrdinaryStep(GameUnit* unit) => unit!=null &&
            (unit->r_PathPlanStateBitFlags==2 || unit->r_PathPlanStateBitFlags==4) && unit->r_MovementSubstep<8 &&
            ReadStateWord(unit,0x6EC)==0 && ReadStateWord(unit,0x9C8)==0 && ReadStateWord(unit,0x9B0)==0 &&
            ReadStateWord(unit,0x8EC)==0 && ReadStateWord(unit,0x8F6)==0 &&
            !(unit->r_AIState>=126 && unit->r_AIState<=129) &&
            (unit->r_NextTilePositionX2!=unit->r_CurrentTilePositionX || unit->r_NextTilePositionY2!=unit->r_CurrentTilePositionY) &&
            Math.Abs((int)unit->r_NextTilePositionX2-unit->r_CurrentTilePositionX)<=1 &&
            Math.Abs((int)unit->r_NextTilePositionY2-unit->r_CurrentTilePositionY)<=1;
        internal bool TryCaptureCurrentOccupants(int x,int y,int width,int height,out List<UnitSnapshot> before,out string reason)
        {
            before=new List<UnitSnapshot>();reason=null;
            if(width<1 || height<1 || width>32 || height>32){reason="invalid-footprint";return false;}
            var tiles=new List<int>();
            for(int dy=0;dy<height;dy++)for(int dx=0;dx<width;dx++)
            {
                long tx=(long)x+dx,ty=(long)y+dy;
                if(tx<0 || tx>=SHCDESE.API.GameTileManagerAPI.MAX_WIDTH || ty<0 || ty>=SHCDESE.API.GameTileManagerAPI.MAX_HEIGHT){reason="footprint-outside-map";return false;}
                int tile=native.Tile((int)tx,(int)ty);
                if(tile<0){reason="footprint-outside-map";return false;}
                tiles.Add(tile);
            }
            if(!TryCollectCurrentOccupants(tiles,native.Head,ReadOccupantRecord,out List<int> ids,out reason))return false;
            // Validate ALL chains before any potentially expensive reachable-field capture.
            foreach(int id in ids)
            {
                UnitSnapshot snapshot=SnapshotUnit(id);
                if(snapshot==null || !tiles.Contains(snapshot.TileId))
                {before.Clear();reason="occupant-changed-during-capture";return false;}
                before.Add(snapshot);
            }
            return true;
        }
        private bool ReadOccupantRecord(int id,out int current,out int next)
        {
            current=next=0;
            if(!native.TryUnit(id,out GameUnit* unit))return false;
            current=(int)unit->r_CurrentPositionTileId;next=native.Next(unit);return true;
        }
        internal delegate bool OccupantRecordReader(int id,out int current,out int next);
        internal static bool TryCollectCurrentOccupants(IEnumerable<int> tiles,Func<int,int> head,OccupantRecordReader record,
            out List<int> ids,out string reason)
        {
            ids=new List<int>();reason=null;var found=new HashSet<int>();int visits=0,cellCount=0;
            foreach(int tile in tiles)
            {
                if(++cellCount>1024){reason="occupancy-cell-budget";ids.Clear();return false;}
                if(tile<0 || tile>=NativeRelocation.TileCapacity){reason="invalid-occupancy-tile";ids.Clear();return false;}
                var chain=new HashSet<int>();int id=head(tile);
                while(id!=0)
                {
                    if(++visits>4096){reason="occupancy-walk-budget";ids.Clear();return false;}
                    if(id<1 || id>NativeRelocation.MaximumUnitId || !chain.Add(id))
                    {reason="invalid-or-cyclic-occupancy-chain";ids.Clear();return false;}
                    if(!record(id,out int current,out int next) || current<0 || current>=NativeRelocation.TileCapacity)
                    {reason="invalid-occupant-record";ids.Clear();return false;}
                    // Native traversal can also link the actor through an auxiliary tile.
                    // Only the actual current tile identifies an actor affected by this mutation.
                    if(current==tile && found.Add(id))
                    {
                        if(found.Count>512){reason="occupant-snapshot-budget";ids.Clear();return false;}
                        ids.Add(id);
                    }
                    id=next;
                }
            }
            ids.Sort();return true;
        }
        internal static string ClassifyState(GameUnit* unit) => ClassifyState(unit,false);
        internal static string ClassifyState(GameUnit* unit,bool includeOrdinaryStep)
            =>ClassifyState(unit,includeOrdinaryStep,false);
        private static string ClassifyState(GameUnit* unit,bool includeOrdinaryStep,bool allowCompletedCapturedStep,bool allowCapturedMoatMidpoint=false)
        {
            if(unit==null)return "missing-unit";
            if(CivilianRemovalPolicy.IsRemovableType(unit->r_UnitChimp))
                return CivilianRemovalPolicy.GetExclusion(unit,includeOrdinaryStep,allowCapturedMoatMidpoint);
            // DCE60 receives native+6EC as its movement-class argument. Mode1 bypasses ordinary
            // connection checks (tunnel movement); other nonzero modes also need their own proof.
            if(ReadStateWord(unit,0x6EC)!=0)return "nonordinary-movement-class";
            // The seventh DCE60 argument is the stored per-path moat permission, not
            // the current tile flag. It permits entering a moat without ordinary links.
            if(ReadStateWord(unit,0x9C8)!=0)return "stored-moat-route-needs-mode-aware-exit-proof";
            if (ReadStateWord(unit,0x9B0)!=0) return "mounted-or-hidden";
            if (ReadStateWord(unit,0x8EC)!=0 || ReadStateWord(unit,0x8F6)!=0) return "portal-or-transport";
            if (unit->r_MovementSubstep!=8 && (!includeOrdinaryStep || !HasOrdinaryStep(unit)) && !allowCapturedMoatMidpoint) return "step-in-progress";
            if (unit->r_AIState>=126 && unit->r_AIState<=129) return "assassin-transition";
            if (unit->r_IsKilledByProjectile!=0 || unit->r_CurrentHealth==0) return "dying";
            if (unit->r_PathPlanStateBitFlags!=0 && unit->r_PathPlanStateBitFlags!=2 && unit->r_PathPlanStateBitFlags!=4) return "special-path-state";
            if (unit->r_AIState>=108) return "special-ai-state";
            bool activeRoute=unit->r_PathPlanStateBitFlags==2 || unit->r_PathPlanStateBitFlags==4;
            // Preserve only separately audited travel segments. In particular,135E30's
            // apple state3 continues picking after relocation without a new distance check.
            if(WorkerTravelPolicy.TryDescribe(unit->r_UnitChimp,unit->r_AIState,out var journey))
                return activeRoute && journey.CanResumeOrdinary ? null : "worker-needs-audited-active-travel";
            // A9E is a type-derived group-order gate, not civilian locomotion ability.
            if(!CommandResumePolicy.IsKnownType(unit->r_UnitChimp))return "unproven-unit-type";
            if(unit->N000000DB==0)return "unclassified-zero-group-order-field";
            //143400 state0 selects2 when no idle travel target is found. State2
            //only animates idle, returns to0 and asks181890 for fresh positional recovery.
            bool stationarySpearmanIdle=unit->r_UnitChimp==eChimps.CHIMP_TYPE_SPEARMAN && unit->r_AIState==2 && unit->r_PathPlanStateBitFlags==0;
            bool stationaryArcherIdle=!activeRoute && CommandJourneySnapshot.IsArcherIdleAnimation(unit->r_UnitChimp,unit->r_AIState);
            if(!stationarySpearmanIdle && !stationaryArcherIdle && !CommandResumePolicy.IsCommandState(unit->r_UnitChimp,unit->r_AIState))
                return IsSiege((int)unit->r_UnitChimp)?"siege-special-state":"unproven-order-state";
            if(!activeRoute && !CommandJourneySnapshot.IsSupportedStationaryPhase(unit->r_UnitChimp,unit->r_AIState) &&
                !(allowCompletedCapturedStep && CommandJourneySnapshot.IsSupportedPhase(unit->r_UnitChimp,unit->r_AIState)))
                return "stationary-state-needs-native-owner";
            return null;
        }
        private static ushort ReadStateWord(GameUnit* unit,int nativeOffset)=>*(ushort*)((byte*)unit+nativeOffset-0x65C);
        internal static bool NeedsExplicitCivilianResume(int type,int flags,bool resume) => !resume && (flags==2 || flags==4) &&
            WorkerTravelPolicy.TryDescribe((eChimps)type,0,out _);
        private static bool IsSiege(int type) => type==(int)eChimps.CHIMP_TYPE_CATAPULT || type==(int)eChimps.CHIMP_TYPE_TREBUCHET ||
            type==(int)eChimps.CHIMP_TYPE_SIEGE_TOWER || type==(int)eChimps.CHIMP_TYPE_BATTERING_RAM || type==(int)eChimps.CHIMP_TYPE_PORTABLE_SHIELD || type==(int)eChimps.CHIMP_TYPE_ARAB_BALLISTA;
        private uint[] CaptureCrew(GameUnit* unit)
        {
            if (!IsSiege((int)unit->r_UnitChimp)) return Array.Empty<uint>();
            var pairs=new uint[8];
            for(int i=0;i<4;i++) { pairs[i*2]=native.Word(unit,0x96C+i*2); pairs[i*2+1]=*(uint*)((byte*)unit+0x974-0x65C+i*4); }
            return pairs;
        }
        private bool ValidCrew(UnitSnapshot before)
        {
            if(!HasCoherentCrewShape(before.CrewIdentity,before.CrewCount))return false;
            var seen=new HashSet<int>();
            for(int i=0;i<before.CrewIdentity.Length;i+=2)
            {
                int id=(int)before.CrewIdentity[i]; if(id==0) continue;
                if(!seen.Add(id) || !native.TryUnit(id,out GameUnit* crew) || crew->r_GlobalId!=before.CrewIdentity[i+1] ||
                    crew->r_UnitChimp!=eChimps.CHIMP_TYPE_ENGINEER || crew->r_AIState!=5 || native.Word(crew,0x9B0)==0 || crew->r_ControllableForPlayerId!=before.Owner) return false;
            }
            if(before.CrewIdentity.Length==0)return true;
            // No native reverse reference proves exclusivity. Check possible saved
            // aliases once per admission, not once for each of up to four engineers.
            for(int other=1;other<=NativeRelocation.MaximumUnitId;other++)
                if(other!=before.Id && native.TryUnit(other,out GameUnit* engine) && IsSiege((int)engine->r_UnitChimp))
                    for(int slot=0;slot<4;slot++)
                        if(HasCrewAlias(before.CrewIdentity,native.Word(engine,0x96C+slot*2),native.JobDword(engine,0x974+slot*4)))return false;
            return true;
        }
        internal static bool HasCrewAlias(uint[] identity,int id,uint global)
        {
            if(id==0 || identity==null)return false;
            for(int i=0;i+1<identity.Length;i+=2)if(identity[i]==id && identity[i+1]==global)return true;
            return false;
        }
        // Native 182B00's refresh pass visits the first Count slots, not every
        // nonzero slot. Trailing global IDs can be stale after an ID slot is cleared.
        internal static bool HasCoherentCrewShape(uint[] identity,int count)
        {
            if(identity==null || count<0 || count>4)return false;
            if(identity.Length==0)return count==0; // A non-siege snapshot has no crew view.
            if(identity.Length!=8)return false;
            for(int slot=0;slot<4;slot++)
                if((identity[slot*2]!=0)!=(slot<count))return false;
            return true;
        }
        private bool Edge(int source,int x,int y,int direction)
        {
            int target=native.Tile(x+Dx[direction],y+Dy[direction]);
            return target>=0 && ((native.Connections(source)&(1<<direction))!=0 || (native.Connections(target)&(1<<(direction^4)))!=0);
        }
        private bool HasOrdinaryExit(int x,int y,int tile)
        {
            for(int d=0;d<8;d++)
            {
                int target=native.Tile(x+Dx[d],y+Dy[d]);
                if(target>=0 && native.OrdinaryGround(target) && Edge(tile,x,y,d))return true;
            }
            return false;
        }
        private void CaptureCandidates(UnitSnapshot value)
        {
#if SERP_DEVDIAG
            var diagnosticCandidates=FindLocalCandidates(value.X,value.Y,native.Tile,native.OrdinaryGround,Edge);
            // Caller proved an ordinary origin: seen = origin plus exactly the returned neighbours.
            RescueCaptureDiagnostics.CandidateBfs(value.Owner,diagnosticCandidates.Count+1);
            value.Candidates.AddRange(diagnosticCandidates);
#else
            value.Candidates.AddRange(FindLocalCandidates(value.X,value.Y,native.Tile,native.OrdinaryGround,Edge));
#endif
        }
        // PURE_LOCAL_SEARCH_BEGIN: shared by runtime and its offline behavior fixture.
        internal static List<RescueCandidate> FindLocalCandidates(int originX,int originY,Func<int,int,int> tileAt,
            Func<int,bool> ordinaryGround,Func<int,int,int,int,bool> hasEdge)
        {
            var candidates=new List<RescueCandidate>();
            var pending=new Queue<RescueCandidate>(); var seen=new HashSet<int>();
            int origin=tileAt(originX,originY);if(origin<0 || !ordinaryGround(origin))return candidates;
            pending.Enqueue(new RescueCandidate { X=originX,Y=originY,Tile=origin }); seen.Add(origin);
            while(pending.Count!=0)
            {
                RescueCandidate current=pending.Dequeue();
                for(int d=0;d<8;d++)
                {
                    int x=current.X+Dx[d],y=current.Y+Dy[d], tile=tileAt(x,y);
                    if(tile<0 || Math.Max(Math.Abs(x-originX),Math.Abs(y-originY))>8 || seen.Contains(tile) ||
                        !ordinaryGround(tile) || !hasEdge(current.Tile,current.X,current.Y,d)) continue;
                    seen.Add(tile); var candidate=new RescueCandidate { X=x,Y=y,Tile=tile,Distance=current.Distance+1 };
                    candidates.Add(candidate); pending.Enqueue(candidate);
                }
            }
            return candidates;
        }
        internal static RescueCandidate? ChooseCandidate(List<RescueCandidate> oldReachable,Func<RescueCandidate,bool> freeNow)
        { foreach(var candidate in oldReachable)if(freeNow(candidate))return candidate;return null; }
        // PURE_LOCAL_SEARCH_END
        // Materialization is transient. Retained/save snapshots keep only immutable
        // before-geometry, never a second large candidate list.
        internal static bool TryMaterializeCandidates(UnitSnapshot before,Func<int,int,int> tileAt,out UnitSnapshot materialized,out string reason)
        {
            materialized=before;reason=null;if(before==null){reason="missing-snapshot";return false;}
            var field=before.LocalField;if(field==null)return true;
            var origin=before.GeometryWitness;
            int x=origin==null?before.X:origin.Current.X,y=origin==null?before.Y:origin.Current.Y,tile=origin==null?before.TileId:origin.Current.Tile;
            if(before.CivilianRemoval || before.Candidates.Count!=0 || field.OriginX!=x || field.OriginY!=y || field.OriginTile!=tile)
            {reason="local-field-origin-conflict";return false;}
            if(!field.TryFindCandidates(tileAt,out var candidates,out reason))return false;
            var copy=RescueReplanIntent.CopySnapshot(before);copy.LocalField=null;
            bool includeOrigin=(origin==null?before.OrdinaryStep:origin.HasAcceptedNext) && (origin==null?before.CurrentWasOrdinary:origin.Current.WasOrdinary);
            if(includeOrigin)copy.Candidates.Add(new RescueCandidate{X=x,Y=y,Tile=tile,Distance=0});
            copy.Candidates.AddRange(candidates);materialized=copy;return true;
        }
        internal bool TryRescue(UnitSnapshot before,string cause,bool resume,out RescueResult result)
        {
            result=new RescueResult { UnitId=before?.Id??0, GlobalId=before?.GlobalId??0, BeforeX=before?.X??0,BeforeY=before?.Y??0,AfterX=before?.X??0,AfterY=before?.Y??0,Detail=cause };
            if(!enabled()) { result.Status=RescueStatus.Inactive; return false; }
            if(before==null || !native.TryUnit(before.Id,out GameUnit* unit) || unit->r_GlobalId!=before.GlobalId) return Finish(result,RescueStatus.Unsupported,"identity-disappeared");
            if(before.CivilianRemoval)return TryRemoveCivilian(before,unit,result);
            if(CivilianRemovalPolicy.IsRemovableType(unit->r_UnitChimp))
                return Finish(result,RescueStatus.Unsupported,"legacy-worker-continuation-has-no-removal-proof");
            if(!APIShared.Units.UnitMoveEvents.IsAvailable)
                return Finish(result,RescueStatus.Unsupported,"movement-capability-unavailable");
            before=before.WithCurrentHealth(unit->r_CurrentHealth);
            result.IdentityPreserved=true;
            // After the native midpoint, Current already equals the captured Next
            // while the last half-step still runs. Only this prior, fresh-Next-moat
            // witness permits that exact phase; ordinary SnapshotUnit stays strict.
            bool capturedMoatMidpoint=before.TileId>=0 && before.TileId<NativeRelocation.TileCapacity &&
                before.NextTileId>=0 && before.NextTileId<NativeRelocation.TileCapacity &&
                NativeWorkerResumeWorld.IsCapturedMoatMidpoint(before,(int)unit->r_CurrentPositionTileId,unit->r_CurrentTilePositionX,unit->r_CurrentTilePositionY,
                    (int)unit->r_NextPositionTileId2,unit->r_NextTilePositionX2,unit->r_NextTilePositionY2,
                    unit->r_PathPlanStateBitFlags,unit->r_MovementSubstep,native.Flags(before.TileId),native.Flags(before.NextTileId),native.OrdinaryGround(before.TileId));
            if(before.Unsupported!=null || ClassifyState(unit,before.OrdinaryStep,before.OrdinaryStep,capturedMoatMidpoint)!=null || !ValidCrew(before))
                return Finish(result,RescueStatus.Unsupported,(before.Unsupported??ClassifyState(unit,before.OrdinaryStep,before.OrdinaryStep,capturedMoatMidpoint)??"crew-inconsistent")+
                    "; ai="+unit->r_AIState+" flags="+unit->r_PathPlanStateBitFlags+" substep="+unit->r_MovementSubstep);
            if(before.Command!=null)
            {
                IntPtr pointer=(IntPtr)unit;
                if(!before.Command.Matches(unit->r_UnitChimp,unit->r_AIState,offset=>native.JobWord((GameUnit*)pointer,offset),
                    offset=>native.JobDword((GameUnit*)pointer,offset)))return Finish(result,RescueStatus.Unsupported,"command-intent-changed-after-capture");
            }
            if(NeedsExplicitCivilianResume(before.Type,before.Flags,resume))
                return Finish(result,RescueStatus.Unsupported,"civilian-active-route-requires-explicit-reissue");
            if(unit->r_CurrentHealth!=before.Health || unit->r_TribeId!=before.Tribe || unit->r_AIState!=before.AIState ||
                (int)unit->r_UnitChimp!=before.Type || unit->r_ControllableForPlayerId!=before.Owner || !Same(CaptureCrew(unit),before.CrewIdentity) ||
                unit->r_TargetTilePositionX2!=before.RequestedX || unit->r_TargetTilePositionY2!=before.RequestedY ||
                !native.TryTribeGlobal(before.Tribe,out uint tribeGlobal) || tribeGlobal!=before.TribeGlobalId ||
                before.Tribe!=0 && native.JobDword(unit,0x93C)!=tribeGlobal ||
                (IsSiege(before.Type) && native.Word(unit,0xA0C)!=before.CrewCount))
                return Finish(result,RescueStatus.Unsupported,"unit-changed-during-mutation");
            if(!TryLocateChangedTrap(before,(int)unit->r_CurrentPositionTileId,unit->r_CurrentTilePositionX,unit->r_CurrentTilePositionY,
                (int)unit->r_NextPositionTileId2,HasOrdinaryStep(unit),native.OrdinaryGround,out RescueCandidate trapped))
                return Finish(result,RescueStatus.NotTrapped,"no-new-complete-ground-exit-loss");
            if(before.LocalField==null && before.Candidates.Count==0)return Finish(result,RescueStatus.NoCandidate,"no-old-reachable-local-ground");
            for(int d=0;d<8;d++)
            {
                int neighborTile=native.Tile(trapped.X+Dx[d],trapped.Y+Dy[d]);
                if(neighborTile>=0 && (native.Flags(neighborTile)&0x10000100u)!=0 && Edge(trapped.Tile,trapped.X,trapped.Y,d))
                    return Finish(result,RescueStatus.Unsupported,"raised-exit-needs-unit-surface-proof");
            }
            for(int d=0;d<8;d++)if(Edge(trapped.Tile,trapped.X,trapped.Y,d))
                return Finish(result,RescueStatus.NotTrapped,"ordinary-physical-exit-remains");
            bool completedMoat=(native.Flags(trapped.Tile)&(uint)TilePropertyFlag.IsMoat)!=0;
            // A failed196280 on a wall can enter199CD0's AI110 retirement path.
            // A mixed wall/moat source needs its surface owner, not this functional probe.
            if(completedMoat && (native.Flags((int)unit->r_CurrentPositionTileId)&(uint)TilePropertyFlag.IsWall)!=0)
                return Finish(result,RescueStatus.Unsupported,"mixed-wall-moat-source-needs-native-owner");
            int sourceTile=(int)unit->r_CurrentPositionTileId;
            if(!native.HasOccupancyExactlyOnce(sourceTile,before.Id)) return Finish(result,RescueStatus.Unsupported,"source-occupancy-invalid");
            // DCE60 fallback: wall at either endpoint, destination mask and optional tree exception.
            // A mere Elevated or drawbridge-support bit cannot establish a legal climb.
            if(before.Type==(int)eChimps.CHIMP_TYPE_ARAB_ASSASIN)
                for(int d=0;d<8;d++)
                {
                    int t=native.Tile(trapped.X+Dx[d],trapped.Y+Dy[d]);if(t<0)continue;
                    uint flags=native.Flags(t);
                    if(((native.Flags(trapped.Tile)|flags)&0x100)!=0 && ((flags&0x4A5014B1)==0 || (flags&0x1000)!=0))
                        return Finish(result,RescueStatus.Unsupported,"possible-native-assassin-climb-exit");
                }
            bool probeCurrent=RequiresCurrentMoatProbe(completedMoat,native.Flags(sourceTile));
            // Scarce owners are checked before allocating a bank list or making
            // any native change. Duplication/unknown ownership is not scarcity.
            if(resume && WorkerReady!=null && WorkerReady())
            {
                if(probeCurrent && before.Job==null && before.Flags==0 && IdleEscapes!=null && !IdleEscapes.HasCapacityFor(before.Id))
                    return Finish(result,RescueStatus.AwaitResources,"idle-escape-capacity");
                if((before.Job!=null || before.Command!=null && (before.Flags==2 || before.Flags==4)) && Workers!=null && !Workers.CanReserveSlot)
                    return Finish(result,RescueStatus.AwaitResources,"worker-receipt-capacity");
            }
            if(!TryMaterializeCandidates(before,native.Tile,out before,out string fieldReason))
                return Finish(result,RescueStatus.Unsupported,fieldReason);
            if(before.Candidates.Count==0)return Finish(result,RescueStatus.NoCandidate,"no-old-reachable-local-ground");
            IdleEscapes?.Release(before.Id); // A new proved mutation supersedes observation of an earlier escape.
            // Keep an already captured legal Current through the Distance0 candidate.

            bool recovered=RelocateAndResume(before,resume,result,sourceTile,probeCurrent);
            if(recovered)report?.Invoke(result.ToString());
            return recovered;
        }
        internal static bool RequiresCurrentMoatProbe(bool trappedIsMoat,uint actualCurrentFlags)=>
            trappedIsMoat && (actualCurrentFlags&(uint)TilePropertyFlag.IsMoat)!=0;
        private bool TryRemoveCivilian(UnitSnapshot before,GameUnit* unit,RescueResult result)
        {
            if(CanRemoveCivilian==null || !CanRemoveCivilian())return Finish(result,RescueStatus.Unsupported,"removal-outside-admitted-simulation-tick");
            if(before.Unsupported!=null || before.Job!=null || before.Command!=null || before.CrewIdentity==null || before.CrewIdentity.Length!=0 ||
                unit->r_UnitChimp!=(eChimps)before.Type || unit->r_ControllableForPlayerId!=before.Owner)
                return Finish(result,RescueStatus.Unsupported,"removal-identity-or-capture-changed");
            int tile=(int)unit->r_CurrentPositionTileId,x=unit->r_CurrentTilePositionX,y=unit->r_CurrentTilePositionY;
            bool oldCurrent=tile==before.TileId && x==before.X && y==before.Y;
            bool acceptedNext=before.OrdinaryStep && tile==before.NextTileId && x==before.NextX && y==before.NextY;
            if(!oldCurrent && !acceptedNext)return Finish(result,RescueStatus.NotTrapped,"removal-left-captured-step");
            bool midpoint=acceptedNext && before.AffectedNext && before.NextWasOrdinary && before.NextHadExit &&
                unit->r_NextTilePositionX2==x && unit->r_NextTilePositionY2==y && (int)unit->r_NextPositionTileId2==tile &&
                (unit->r_PathPlanStateBitFlags==2 || unit->r_PathPlanStateBitFlags==4) && unit->r_MovementSubstep<8 &&
                native.JobDword(unit,0x740)==0;
            string exclusion=CivilianRemovalPolicy.GetExclusion(unit,before.OrdinaryStep,midpoint);
            if(exclusion!=null)return Finish(result,RescueStatus.Unsupported,exclusion);
            if(!TryLocateChangedTrap(before,tile,x,y,(int)unit->r_NextPositionTileId2,HasOrdinaryStep(unit),native.OrdinaryGround,out var trapped))
                return Finish(result,RescueStatus.NotTrapped,"removal-no-new-unsafe-surface");
            // An accepted but newly blocked Next is not an unsafe physical Current yet.
            // Let normal execution/replanning win; retain only this bounded causal case.
            if(native.OrdinaryGround(tile))return Finish(result,RescueStatus.RemovalDeferred,"removal-current-still-ordinary");
            bool sourceHadExit=oldCurrent && before.AffectedCurrent && before.CurrentWasOrdinary && before.CurrentHadExit ||
                acceptedNext && before.AffectedNext && before.NextWasOrdinary && before.NextHadExit;
            if(!sourceHadExit || native.Tile(x,y)!=tile || !native.HasOccupancyExactlyOnce(tile,before.Id))
                return Finish(result,RescueStatus.Unsupported,"removal-lacks-original-exit-or-current-occupancy");
            for(int d=0;d<8;d++)if(Edge(trapped.Tile,trapped.X,trapped.Y,d))
                return Finish(result,RescueStatus.NotTrapped,"removal-physical-exit-remains");
            // DeleteSafe invokes no foreign callback: it only marks this checked live life.
            // The next ordinary unit pass owns cleanup, employee maintenance and recount.
            if(!SHCDESE.API.GameUnitManagerAPI.Instance.DeleteUnitSafe(before.Id) || unit->r_GlobalId!=before.GlobalId ||
                unit->r_AliveState!=AliveState.MarkedForDeletion)
                return Finish(result,RescueStatus.IdentityFailed,"removal-mark-not-confirmed");
            result.IdentityPreserved=false; // No completed-cleanup or replacement claim.
            return Finish(result,RescueStatus.RemovalRequested,"native-removal-requested-await-normal-cleanup");
        }
        // Accept at most the one ordinary step captured before this mutation. A different
        // subsequent location cannot inherit old local reachability or an old command.
        internal bool HasChangedTrapCandidate(UnitSnapshot before)
        {
            return before!=null && native.TryUnit(before.Id,out var unit) && unit->r_GlobalId==before.GlobalId &&
                TryLocateChangedTrap(before,(int)unit->r_CurrentPositionTileId,unit->r_CurrentTilePositionX,unit->r_CurrentTilePositionY,
                    (int)unit->r_NextPositionTileId2,HasOrdinaryStep(unit),native.OrdinaryGround,out _);
        }
        internal static bool TryLocateChangedTrap(UnitSnapshot before,int currentTile,int currentX,int currentY,
            int nextTile,bool ordinaryStep,Func<int,bool> ordinaryGround,out RescueCandidate trapped)
        {
            trapped=default;if(before==null || ordinaryGround==null)return false;
            bool oldCurrent=currentTile==before.TileId && currentX==before.X && currentY==before.Y;
            bool acceptedNext=before.OrdinaryStep && currentTile==before.NextTileId && currentX==before.NextX && currentY==before.NextY;
            if(!oldCurrent && !acceptedNext)return false;
            if((oldCurrent && before.AffectedCurrent && before.CurrentWasOrdinary || acceptedNext && before.AffectedNext && before.NextWasOrdinary) &&
                !ordinaryGround(currentTile))
            {trapped=new RescueCandidate{X=currentX,Y=currentY,Tile=currentTile};return true;}
            if(oldCurrent && before.OrdinaryStep && ordinaryStep && before.AffectedNext && before.NextWasOrdinary &&
                nextTile==before.NextTileId && ordinaryGround(currentTile) && !ordinaryGround(nextTile))
            {trapped=new RescueCandidate{X=before.NextX,Y=before.NextY,Tile=nextTile};return true;}
            return false;
        }
        private bool RelocateAndResume(UnitSnapshot before,bool resume,RescueResult result,int sourceTile=-1,bool probeOriginal=false)
        {
            if(sourceTile<0)sourceTile=before.TileId;
            RescueCandidate? chosen=ChooseCandidate(before.Candidates,candidate=>native.Tile(candidate.X,candidate.Y)==candidate.Tile && native.OrdinaryGround(candidate.Tile) &&
                (native.Head(candidate.Tile)==0 || candidate.Tile==sourceTile && native.HasOccupancyExactlyOnce(sourceTile,before.Id)) &&
                NativeIdleEscapeWorld.HasPhysicalExit(candidate,native.Tile,native.OrdinaryGround,native.Connections));
            if(!chosen.HasValue && !probeOriginal) return Finish(result,RescueStatus.NoCandidate,"no-old-reachable-free-local-ground");
            var target=chosen.GetValueOrDefault();
            if(probeOriginal && before.Job==null && before.Flags==0)
            {
                if(!chosen.HasValue)return Finish(result,RescueStatus.NoCandidate,"idle-moat-no-old-reachable-free-local-ground");
                if(!resume || IdleEscapes==null || CreateIdleEscapeRequest==null || WorkerTick==null || WorkerReady==null || !WorkerReady())
                    return Finish(result,RescueStatus.Unsupported,"idle-escape-owner-not-ready");
                var request=CreateIdleEscapeRequest(before,target);
                bool accepted=IdleEscapes.TryStart(request,unchecked((uint)WorkerTick()),out string detail);
                result.ResumeAttempted=true;result.ResumeSucceeded=accepted;
                if(!accepted)return Finish(result,IdleEscapes.LastState==IdleEscapeState.AwaitResources?RescueStatus.AwaitResources:RescueStatus.Unsupported,detail);
                if(IdleEscapes.LastState==IdleEscapeState.InProgress)
                    return Finish(result,RescueStatus.EscapeInProgress,detail);
                result.AfterX=target.X;result.AfterY=target.Y;
                return Finish(result,RescueStatus.Rescued,detail);
            }
            if(before.Job!=null || before.Command!=null && (before.Flags==2 || before.Flags==4))
            {
                if(!resume || Workers==null || WorkerTick==null || WorkerReady==null || !WorkerReady())
                    return Finish(result,RescueStatus.Unsupported,"worker-resume-owner-not-ready");
                int source=sourceTile;
                Func<bool> relocate=()=>chosen.HasValue && RelocateChecked(before,target,source,result);
                WorkerRecoveryOutcome outcome;string reason;
                bool accepted=probeOriginal ? Workers.TryProbeOrRecover(before,relocate,WorkerTick(),out outcome,out reason) :
                    Workers.TryRecover(before,relocate,WorkerTick(),out outcome,out reason);
                result.ResumeAttempted=accepted || outcome!=WorkerRecoveryOutcome.Rejected;
                result.ResumeSucceeded=accepted && outcome==WorkerRecoveryOutcome.Resumed;
                if(!accepted)return Finish(result,outcome==WorkerRecoveryOutcome.AwaitResources?RescueStatus.AwaitResources:outcome==WorkerRecoveryOutcome.OwnershipUncertain?RescueStatus.IdentityFailed:RescueStatus.Unsupported,reason);
                if(!result.ResumeSucceeded)return Finish(result,RescueStatus.ResumeFailed,reason);
                // A captured step may already have committed its original Next.
                // A successful functional probe need not enter RelocateChecked,
                // so report the actual live source rather than the old Before XY.
                if(native.TryUnit(before.Id,out var resumedUnit) && resumedUnit->r_GlobalId==before.GlobalId)
                {result.AfterX=resumedUnit->r_CurrentTilePositionX;result.AfterY=resumedUnit->r_CurrentTilePositionY;}
                return Finish(result,RescueStatus.Rescued,reason);
            }
            if(before.Flags==2 || before.Flags==4)return Finish(result,RescueStatus.Unsupported,"active-order-needs-owned-continuation");
            if(!RelocateChecked(before,target,sourceTile,result))return false;
            result.Status=RescueStatus.Rescued;result.Detail+="; local-relocation-confirmed; order continuation not implied";
            return true;
        }
        private bool RelocateChecked(UnitSnapshot before,RescueCandidate target,int sourceTile,RescueResult result)
        {
            if(!native.Relocate(before.Id,target.X,target.Y)) return Finish(result,RescueStatus.RelocationFailed,"live-relocation-veto-or-postcondition");
            result.AfterX=target.X;result.AfterY=target.Y;
            if(!native.TryUnit(before.Id,out GameUnit* unit)) return Finish(result,RescueStatus.IdentityFailed,"lost-after-relocation");
            IntPtr pointer=(IntPtr)unit;
            result.IdentityPreserved=unit->r_GlobalId==before.GlobalId && (int)unit->r_UnitChimp==before.Type && unit->r_CurrentHealth==before.Health &&
                unit->r_ControllableForPlayerId==before.Owner && unit->r_TribeId==before.Tribe && unit->r_AIState==before.AIState && Same(CaptureCrew(unit),before.CrewIdentity) &&
                native.TryTribeGlobal(before.Tribe,out uint tribeGlobal) && tribeGlobal==before.TribeGlobalId &&
                (before.Tribe==0 || native.JobDword(unit,0x93C)==tribeGlobal) &&
                (before.Command==null || before.Command.Matches(unit->r_UnitChimp,unit->r_AIState,
                    offset=>native.JobWord((GameUnit*)pointer,offset),offset=>native.JobDword((GameUnit*)pointer,offset))) &&
                (!IsSiege(before.Type) || native.Word(unit,0xA0C)==before.CrewCount);
            if(!result.IdentityPreserved || !native.HasOccupancyCount(sourceTile,before.Id,target.Tile==sourceTile?1:0)) return Finish(result,RescueStatus.IdentityFailed,"identity-or-source-occupancy");
            return true;
        }
        private static bool Same(uint[] a,uint[] b) { if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true; }
        private bool Finish(RescueResult result,RescueStatus status,string detail)
        { result.Status=status;result.Detail+="; "+detail;report?.Invoke(result.ToString());return status==RescueStatus.Rescued || status==RescueStatus.RemovalRequested; }
    }
}
