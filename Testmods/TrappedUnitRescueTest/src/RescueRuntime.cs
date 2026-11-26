using System;
using System.Collections.Generic;
using System.Threading;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal enum RescueStatus { Inactive, NotTrapped, Unsupported, NoCandidate, RelocationFailed, IdentityFailed, Rescued, ResumeFailed, EscapeInProgress }
    internal sealed class UnitSnapshot
    {
        internal int Id, Type, Owner, Tribe, X, Y, TileId, AIState, Flags, Substep, RequestedX, RequestedY,CrewCount;
        internal int NextX, NextY, NextTileId;
        internal bool OrdinaryStep, AffectedCurrent, AffectedNext, CurrentWasOrdinary, NextWasOrdinary;
        internal uint GroundFlags, NextGroundFlags;
        internal uint GlobalId, Health, TribeGlobalId;
        internal bool HasCapturedVegetation;
        internal uint CapturedVegetationGlobal;
        internal WorkerJourneySnapshot Job;
        internal CommandJourneySnapshot Command;
        internal RescueGeometryWitness GeometryWitness;
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
        internal WorkerResumeSupervisor Workers { get; set; }
        internal Func<long> WorkerTick { get; set; }
        internal Func<bool> WorkerReady { get; set; }
        internal IdleEscapeOwner IdleEscapes { get; set; }
        internal Func<UnitSnapshot,RescueCandidate,IdleEscapeRequest> CreateIdleEscapeRequest { get; set; }
        private static readonly int[] Dx = { 0,1,1,1,0,-1,-1,-1 }, Dy = { -1,-1,0,1,1,1,0,-1 };
        internal RescueRuntime(NativeRelocation native, Func<bool> isEnabled, Action<string> report)
        { this.native = native ?? throw new ArgumentNullException(nameof(native)); enabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled)); this.report = report; }
        internal UnitSnapshot SnapshotUnit(int id) => SnapshotUnit(id, false);
        internal UnitSnapshot SnapshotUnit(int id, bool includeOrdinaryStep,bool collectCandidates=true)
        {
            if (!native.TryUnit(id, out GameUnit* unit)) return null;
            var value = new UnitSnapshot { Id=id, GlobalId=unit->r_GlobalId, Type=(int)unit->r_UnitChimp, Owner=unit->r_ControllableForPlayerId,
                Tribe=unit->r_TribeId, Health=unit->r_CurrentHealth, X=unit->r_CurrentTilePositionX, Y=unit->r_CurrentTilePositionY,
                TileId=(int)unit->r_CurrentPositionTileId, AIState=unit->r_AIState, Flags=unit->r_PathPlanStateBitFlags, Substep=unit->r_MovementSubstep,
                NextX=unit->r_NextTilePositionX2,NextY=unit->r_NextTilePositionY2,NextTileId=(int)unit->r_NextPositionTileId2,
                RequestedX=unit->r_TargetTilePositionX2, RequestedY=unit->r_TargetTilePositionY2, CrewIdentity=CaptureCrew(unit),
                CrewCount=IsSiege((int)unit->r_UnitChimp)?native.Word(unit,0xA0C):0 };
            value.OrdinaryStep = HasOrdinaryStep(unit);
            value.Unsupported = ClassifyState(unit, includeOrdinaryStep);
            // 11D370 writes unit930/93C together;118660 validates that stored stamp
            // against the current tribe record. Never adopt a recycled group slot.
            if(value.Tribe!=0 && (!native.TryTribeGlobal(value.Tribe,out value.TribeGlobalId) || native.JobDword(unit,0x93C)!=value.TribeGlobalId))
                value.Unsupported="tribe-identity-unavailable";
            if(value.Unsupported==null && WorkerTravelPolicy.TryDescribe(unit->r_UnitChimp,unit->r_AIState,out var rule))
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
            else if(value.Unsupported==null)
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
            if (collectCandidates && value.Unsupported == null && native.OrdinaryGround(value.TileId)) CaptureCandidates(value);
            return value;
        }
        internal bool TryCaptureMutation(IList<RescueTile> changed,out List<UnitSnapshot> before,out string reason)
        {
            before=new List<UnitSnapshot>();
            if(!RescueDiscovery.TryCollect(changed,native.Tile,native.Head,ReadDiscoveryUnit,
                new RescueDiscoveryBudget(4096,16384,512,NativeRelocation.MaximumUnitId,NativeRelocation.TileCapacity),
                out var occupants,out reason))return false;
            var affected=new HashSet<int>();foreach(var tile in changed)affected.Add(tile.TileId);
            foreach(var occupant in occupants)
            {
                var snapshot=SnapshotUnit(occupant.UnitId,true);
                if(snapshot==null || snapshot.GlobalId!=occupant.GlobalId || snapshot.TileId!=occupant.CurrentTile ||
                    snapshot.OrdinaryStep && snapshot.NextTileId!=occupant.NextTile)
                {before.Clear();reason="occupant-changed-before-snapshot";return false;}
                snapshot.AffectedCurrent=affected.Contains(snapshot.TileId);
                snapshot.AffectedNext=snapshot.OrdinaryStep && affected.Contains(snapshot.NextTileId);
                // Cancelling a newly unsafe Next step may keep its still-legal Current tile.
                // This candidate was reachable before the mutation; never manufacture it later.
                if(snapshot.OrdinaryStep && native.OrdinaryGround(snapshot.TileId))
                    snapshot.Candidates.Insert(0,new RescueCandidate {X=snapshot.X,Y=snapshot.Y,Tile=snapshot.TileId,Distance=0});
                before.Add(snapshot);
            }
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
            if (!IsSiege((int)unit->r_UnitChimp)) return new uint[0];
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
        private bool HasExit(UnitSnapshot value)
        {
            for(int d=0;d<8;d++) if(Edge(value.TileId,value.X,value.Y,d)) return true;
            return false;
        }
        private void CaptureCandidates(UnitSnapshot value)
        { value.Candidates.AddRange(FindLocalCandidates(value.X,value.Y,native.Tile,native.OrdinaryGround,Edge)); }
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
        internal MutationScope BeginMutation(string label,int centerX,int centerY,int radius) => new MutationScope(this,label,centerX,centerY,radius);
        // Dedicated fixture transaction: generic completed-moat admission remains unsupported.
        // Capture BEFORE the real writer; only this single-use receipt can nominate its old banks.
        internal AppleMoatReceipt CaptureAppleMoatMutation(int id)
        {
            var before=SnapshotUnit(id);
            if(before==null || !native.TryUnit(id,out GameUnit* unit) || !AppleMoatBoundary(before,unit) ||
                !native.OrdinaryGround(before.TileId) || before.Candidates.Count==0)return null;
            return new AppleMoatReceipt(this,before,CaptureAppleJob(unit));
        }
        internal static bool AppleMoatBoundary(UnitSnapshot before,GameUnit* unit)
        {
            return before!=null && unit!=null && before.Unsupported==null &&
                before.Type==(int)eChimps.CHIMP_TYPE_FARMER_APPLE && (before.AIState==5 || before.AIState==7) &&
                (before.Flags==2 || before.Flags==4) && before.Substep==8 && ClassifyState(unit)==null &&
                unit->r_GlobalId==before.GlobalId && (int)unit->r_UnitChimp==before.Type &&
                unit->r_AIState==before.AIState && unit->r_PathPlanStateBitFlags==before.Flags &&
                unit->r_CurrentHealth==before.Health && unit->r_TribeId==before.Tribe && unit->r_ControllableForPlayerId==before.Owner &&
                unit->r_CurrentPositionTileId==(uint)before.TileId && unit->r_CurrentTilePositionX==before.X && unit->r_CurrentTilePositionY==before.Y &&
                unit->r_NextTilePositionX2==before.X && unit->r_NextTilePositionY2==before.Y &&
                unit->r_TargetTilePositionX2==before.RequestedX && unit->r_TargetTilePositionY2==before.RequestedY &&
                (before.X!=before.RequestedX || before.Y!=before.RequestedY);
        }
        // These are 135E30's employee/home/store identities, cargo and job counters. Neither
        //19BA40 nor failed196280 is authorized to change them in this synchronous transaction.
        internal static uint[] CaptureAppleJob(GameUnit* unit)
        {
            return new uint[] { unit->r_LinkedProductionBuildingId,unit->r_LinkedProductionBuildingGlobalId,
                unit->r_LinkedProductionBuildingTileEndX,unit->r_LinkedProductionBuildingTileEndY,
                unit->r_CarryGoodAmount,ReadStateWord(unit,0x98E),*(uint*)((byte*)unit+0x6F8-0x65C),
                ReadStateWord(unit,0x9A0),ReadStateWord(unit,0x9CA),ReadStateWord(unit,0x9CC),
                ReadStateWord(unit,0x9D0),ReadStateWord(unit,0x9EC),ReadStateWord(unit,0x9EE) };
        }
        internal static bool NewlyTrappedAppleMoat(uint oldFlags,uint newFlags,bool ordinaryExit) =>
            (oldFlags&(uint)TilePropertyFlag.IsMoat)==0 && (newFlags&(uint)TilePropertyFlag.IsMoat)!=0 && !ordinaryExit;

        // Synchronous fixture-only receipt: capture, brush, actual rejected native publication
        // and consumption are one Fixture.Execute action under the simulation monitor, without
        // yielding, ticks or mission transitions. This is not a general delayed recovery API.
        internal sealed class AppleMoatReceipt
        {
            private readonly RescueRuntime owner;
            private readonly UnitSnapshot before;
            private readonly uint[] job;
            private readonly uint oldFlags;
            private int consumed;
            internal AppleMoatReceipt(RescueRuntime owner,UnitSnapshot before,uint[] job)
            { this.owner=owner;this.before=before;this.job=job;oldFlags=owner.native.Flags(before.TileId); }
            internal bool TryRecoverRejectedPublication(bool publicationAccepted,out RescueResult result)
            {
                result=new RescueResult { UnitId=before.Id,GlobalId=before.GlobalId,BeforeX=before.X,BeforeY=before.Y,
                    AfterX=before.X,AfterY=before.Y,Detail="causal-apple-new-moat-after-rejected-original-goal" };
                if(Interlocked.Exchange(ref consumed,1)!=0)return owner.Finish(result,RescueStatus.Unsupported,"receipt-already-consumed");
                if(!owner.enabled()){result.Status=RescueStatus.Inactive;return false;}
                if(publicationAccepted)return owner.Finish(result,RescueStatus.NotTrapped,"original-goal-publication-succeeded");
                var native=owner.native;
                if(!native.TryUnit(before.Id,out GameUnit* unit) || !AppleMoatBoundary(before,unit) || !Same(job,CaptureAppleJob(unit)))
                    return owner.Finish(result,RescueStatus.Unsupported,"employee-job-identity-or-mode-changed");
                // Apple/class0 with marker0 has no Assassin/tunnel/moat fallback in DCE60.
                if(!NewlyTrappedAppleMoat(oldFlags,native.Flags(before.TileId),owner.HasExit(before)))
                    return owner.Finish(result,RescueStatus.NotTrapped,"not-a-new-zero-exit-mode0-moat");
                if(native.Building(before.TileId)!=0 || !native.HasOccupancyExactlyOnce(before.TileId,before.Id))
                    return owner.Finish(result,RescueStatus.Unsupported,"source-building-or-occupancy-changed");
                result.IdentityPreserved=true;
                bool recovered=owner.RelocateAndResume(before,true,result);
                if(!native.TryUnit(before.Id,out unit) || !Same(job,CaptureAppleJob(unit)) ||
                    unit->r_AIState!=before.AIState || unit->r_GlobalId!=before.GlobalId || unit->r_CurrentHealth!=before.Health ||
                    unit->r_ControllableForPlayerId!=before.Owner || unit->r_TribeId!=before.Tribe ||
                    unit->r_TargetTilePositionX2!=before.RequestedX || unit->r_TargetTilePositionY2!=before.RequestedY)
                {result.IdentityPreserved=false;return owner.Finish(result,RescueStatus.IdentityFailed,"employee-or-original-job-goal-not-preserved");}
                if(recovered)owner.report?.Invoke(result.ToString());
                return recovered;
            }
        }
        internal bool TryRescue(UnitSnapshot before,string cause,bool resume,out RescueResult result)
        {
            result=new RescueResult { UnitId=before?.Id??0, GlobalId=before?.GlobalId??0, BeforeX=before?.X??0,BeforeY=before?.Y??0,AfterX=before?.X??0,AfterY=before?.Y??0,Detail=cause };
            if(!enabled()) { result.Status=RescueStatus.Inactive; return false; }
            if(before==null || !native.TryUnit(before.Id,out GameUnit* unit) || unit->r_GlobalId!=before.GlobalId) return Finish(result,RescueStatus.Unsupported,"identity-disappeared");
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
            if(before.Candidates.Count==0)return Finish(result,RescueStatus.NoCandidate,"no-old-reachable-local-ground");
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
            IdleEscapes?.Release(before.Id); // A new proved mutation supersedes observation of an earlier escape.
            // A blocked accepted Next is not the planner's physical source yet.
            // Keep its still-legal Current via the already captured Distance0
            // candidate, normalize there under reservation, then reissue normally.
            bool probeCurrent=RequiresCurrentMoatProbe(completedMoat,native.Flags(sourceTile));
            bool recovered=RelocateAndResume(before,resume,result,sourceTile,probeCurrent);
            if(recovered)report?.Invoke(result.ToString());
            return recovered;
        }
        internal static bool RequiresCurrentMoatProbe(bool trappedIsMoat,uint actualCurrentFlags)=>
            trappedIsMoat && (actualCurrentFlags&(uint)TilePropertyFlag.IsMoat)!=0;
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
                if(!accepted)return Finish(result,RescueStatus.Unsupported,detail);
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
                if(!accepted)return Finish(result,outcome==WorkerRecoveryOutcome.OwnershipUncertain?RescueStatus.IdentityFailed:RescueStatus.Unsupported,reason);
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
        { result.Status=status;result.Detail+="; "+detail;report?.Invoke(result.ToString());return status==RescueStatus.Rescued; }
        internal sealed class MutationScope : IDisposable
        {
            private readonly RescueRuntime owner; private readonly string label; private bool completed;
            internal readonly List<UnitSnapshot> Before=new List<UnitSnapshot>();
            internal MutationScope(RescueRuntime owner,string label,int x,int y,int radius)
            {
                this.owner=owner;this.label=label;
                if(radius<0 || radius>32)throw new ArgumentOutOfRangeException(nameof(radius));
                for(int id=1;id<=NativeRelocation.MaximumUnitId;id++)
                    if(owner.native.TryUnit(id,out GameUnit* unit) && Math.Max(Math.Abs(unit->r_CurrentTilePositionX-x),Math.Abs(unit->r_CurrentTilePositionY-y))<=radius)
                        Before.Add(owner.SnapshotUnit(id));
            }
            internal RescueResult[] Complete(bool tryResumeActiveOrder=false)
            {
                if(completed)throw new InvalidOperationException("Mutation scope already completed.");completed=true;
                var results=new List<RescueResult>();foreach(var before in Before) { owner.TryRescue(before,label,tryResumeActiveOrder,out RescueResult result);results.Add(result); }
                return results.ToArray();
            }
            public void Dispose() { completed=true; } // Never implicitly mutate while unwinding a failed fixture.
        }
    }
}
