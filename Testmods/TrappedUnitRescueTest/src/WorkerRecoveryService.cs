using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using APIShared.Savegames;
using APIShared.WorldMutations;
using SHCDESE.Interop;
using SHCDESE.API;

namespace TrappedUnitRescueTest
{
    internal delegate bool WorkerWorldReader(out long session,out long generation);
    internal delegate bool RescuePendingCapture(out RescuePendingCandidate<UnitSnapshot>[] records);
    internal delegate bool RescueGeometryCapture(out RescuePendingGeometry[] records);
    // Holds only our arrival-barrier ownership. Old blocked units without a receipt
    // are never inferred from a save or recovered by scanning all units.
    internal sealed class WorkerRecoveryService
    {
        internal readonly WorkerResumeSupervisor Supervisor;
        private readonly Func<bool> isRecovering;
        private readonly Action<string> report;
        private readonly WorkerWorldReader readWorld;
        private byte[] loaded;
        private RescuePendingCandidate<UnitSnapshot>[] loadedPending=new RescuePendingCandidate<UnitSnapshot>[0];
        private RescuePendingGeometry[] loadedGeometry=Array.Empty<RescuePendingGeometry>();
        internal RescuePendingCapture CapturePending { get; set; }
        internal Action<RescuePendingCandidate<UnitSnapshot>[]> RestorePending { get; set; }
        internal RescueGeometryCapture CaptureGeometry {get;set;}
        internal Action<RescuePendingGeometry[]> RestoreGeometry {get;set;}
        private readonly Dictionary<int,string> legacyDiagnostics=new Dictionary<int,string>();
        private int legacyReports;
        private IWorkerLegacyRetirementWorld legacyWorld;
        private bool legacyDrained;private int legacyTick;
        private bool unusable,clockStarted,modeAdmitted;
        private long session,world,clock;
        private int lastNativeTick;
        internal long TickValue=>clock;
        internal bool Ready=>!unusable && session!=0 && modeAdmitted;
        internal WorkerRecoveryService(NativeRelocation native,Func<bool> isRecovering,Action<string> report)
            : this(new WorkerResumeSupervisor(new NativeWorkerResumeWorld(native)),isRecovering,report,WorldMutationEvents.TryGetCurrentWorld)
        {EnableLegacyRetirement(new NativeWorkerLegacyRetirementWorld(native));}
        internal WorkerRecoveryService(WorkerResumeSupervisor supervisor,Func<bool> isRecovering,Action<string> report,WorkerWorldReader readWorld)
        {Supervisor=supervisor??throw new ArgumentNullException(nameof(supervisor));this.isRecovering=isRecovering??throw new ArgumentNullException(nameof(isRecovering));
            this.report=report;this.readWorld=readWorld??throw new ArgumentNullException(nameof(readWorld));}
        internal void EnableLegacyRetirement(IWorkerLegacyRetirementWorld world)
        {legacyWorld=world??throw new ArgumentNullException(nameof(world));Supervisor.EnableLegacyRetirement(world);}
        // Qualified caller: normal simulation pre-pass, after AdmitTick and before
        // gameplay gates/suppression. It resolves our historical barriers even OFF.
        internal int AdvanceLegacyTick(int tick)
        {
            if(legacyWorld==null || Supervisor.Ledger.LegacyCivilianCount==0 || !Ready || !clockStarted || lastNativeTick!=tick || isRecovering() ||
                legacyDrained && legacyTick==tick || !CurrentLegacyWorld())return 0;
            legacyDrained=true;legacyTick=tick;
            return Supervisor.RetireLegacyCivilianParks(32,4,legacyWorld,CurrentLegacyWorld,ReportLegacy);
        }
        private void ReportLegacy(WorkerResumeTicket ticket,WorkerLegacyRetirement state,string reason)
        {
            if(state!=WorkerLegacyRetirement.Deferred){legacyDiagnostics.Remove(ticket.UnitId);return;}
            string key=ticket.UnitGlobalId+":"+(reason??"barrier-unqualified");
            if(legacyDiagnostics.TryGetValue(ticket.UnitId,out string previous) && previous==key)return;
            legacyDiagnostics[ticket.UnitId]=key;
            if(legacyReports>=32)return;legacyReports++;
            try{report?.Invoke("legacy-worker-retirement deferred unit="+ticket.UnitId+"/"+ticket.UnitGlobalId+" reason="+(reason??"barrier-unqualified"));}catch{}
        }
        private bool CurrentLegacyWorld()=>session!=0 && world!=0 && readWorld(out long actualSession,out long actualWorld) &&
            actualSession==session && actualWorld==world;
        internal bool Register(string ownerGuid,out string reason)
        {
            var registration=new VersionedSaveDataRegistration(ownerGuid,"TrappedUnitRescueOwnedWorkers",1,1,
                new[]{"owned-workers","pending/","geometry/","compact/"},Capture,Load)
            {RequireAtomicWrite=true,SimulationRelevant=true,Scope=VersionedSaveScope.SaveGamesAndMaps};
            return VersionedSaveData.TryRegister(registration,out _,out reason);
        }
        private bool Capture(VersionedSaveWriter writer)
        {
            if(!TryCaptureArchive(out var entries))return false;
            foreach(var entry in entries)writer.Write(entry.Key,entry.Value);
            return true;
        }
        internal bool TryCaptureArchive(out SortedDictionary<string,byte[]> entries)
        {
            entries=null;
            if(!TryCaptureOwnedRecord(out byte[] bytes))return false;
            RescuePendingCandidate<UnitSnapshot>[] pending=loadedPending;
            if(CapturePending!=null && !CapturePending(out pending))return false;
            if(pending==null || pending.Length>GeneralRescueObserver.MaximumPending)return false;
            RescuePendingGeometry[] geometry=loadedGeometry;
            if(CaptureGeometry!=null && !CaptureGeometry(out geometry))return false;
            if(geometry==null || pending.Length+geometry.Length>GeneralRescueObserver.MaximumPending)return false;
            bool compact=pending.Length+geometry.Length>512;
            foreach(var record in pending)compact|=record.LifetimeMode==RescueQueueLifetimeMode.ActiveWindow || record.Snapshot?.LocalField!=null;
            foreach(var record in geometry)compact|=record.LifetimeMode==RescueQueueLifetimeMode.ActiveWindow || record.Witness?.LocalField!=null;
            if(compact)
            {
                if(!RescueCompactArchiveCodec.TryEncode(pending,geometry,bytes,out var complete,out string reason))
                {report?.Invoke("compact-pending-save rejected: "+reason);return false;}
                entries=new SortedDictionary<string,byte[]>(complete,StringComparer.Ordinal);return true;
            }
            var encoded=new byte[pending.Length][];var units=new HashSet<int>();
            for(int i=0;i<pending.Length;i++)
                if(pending[i].Snapshot==null || !units.Add(pending[i].Snapshot.Id) || !RescuePendingCodec.TryEncode(pending[i],out encoded[i]))return false;
            var geometryBytes=new byte[geometry.Length][];
            for(int i=0;i<geometry.Length;i++)
                if(geometry[i].Witness==null || !units.Add(geometry[i].Witness.UnitId) || !RescueGeometryCodec.TryEncode(geometry[i],out geometryBytes[i]))return false;
            // Entries are written only after every pending proof has validated.
            var result=new SortedDictionary<string,byte[]>(StringComparer.Ordinal){{"owned-workers",bytes}};
            for(int i=0;i<encoded.Length;i++)result.Add("pending/"+i.ToString("D3",CultureInfo.InvariantCulture),encoded[i]);
            for(int i=0;i<geometryBytes.Length;i++)result.Add("geometry/"+i.ToString("D3",CultureInfo.InvariantCulture),geometryBytes[i]);
            entries=result;return true;
        }
        internal bool TryCaptureOwnedRecord(out byte[] bytes)
        {
            bytes=null;
            return !unusable && !isRecovering() && !Supervisor.IsExecutingNativeCall && !Supervisor.HasPendingExternalOrders && Supervisor.Ledger.TrySerialize(out bytes);
        }
        private void Load(VersionedSaveLoad load)
        {
            load.Entries.TryGetValue("owned-workers",out byte[] bytes);
            StageOwnedRecord(load.Status,bytes);
            // A restored record is one atomic ownership/causal archive. Returning
            // normally after a rejected namespace would let the SDK replace it.
            if(load.Status==VersionedSaveLoadStatus.Restored &&
                (bytes==null || !new WorkerResumeLedger().TryRestore(bytes,0)))RejectLoadedArchive();
            if(!StagePendingRecords(load.Status,load.Entries))RejectLoadedArchive();
        }
        private void RejectLoadedArchive()
        {
            loaded=null;loadedPending=Array.Empty<RescuePendingCandidate<UnitSnapshot>>();
            loadedGeometry=Array.Empty<RescuePendingGeometry>();unusable=true;
            // VersionedSaveDataRegistry catches this callback exception and sets
            // KeepLoaded=true for the ENTIRE original restored record.
            throw new InvalidDataException("Rejected complete rescue archive; original saved bytes must be retained.");
        }
        internal bool StagePendingRecords(VersionedSaveLoadStatus status,IReadOnlyDictionary<string,byte[]> entries)
        {
            loadedPending=new RescuePendingCandidate<UnitSnapshot>[0];
            loadedGeometry=Array.Empty<RescuePendingGeometry>();
            if(status!=VersionedSaveLoadStatus.Restored)return true;
            bool compact=false;
            if(entries!=null)foreach(string key in entries.Keys)if(key.StartsWith("compact/",StringComparison.Ordinal)){compact=true;break;}
            if(compact)
            {
                var owned=new Dictionary<string,byte[]>(StringComparer.Ordinal);
                foreach(var item in entries)
                {
                    if(item.Key.StartsWith("pending/",StringComparison.Ordinal) || item.Key.StartsWith("geometry/",StringComparison.Ordinal))return false;
                    if(item.Key=="owned-workers" || item.Key.StartsWith("compact/",StringComparison.Ordinal))owned.Add(item.Key,item.Value);
                }
                if(!RescueCompactArchiveCodec.TryDecode(owned,out var compactPending,out var compactGeometry,out string reason))
                {report?.Invoke("compact-pending-restore rejected: "+reason);return false;}
                loadedPending=compactPending;loadedGeometry=compactGeometry;return true;
            }
            var keys=new List<string>();
            if(entries!=null)foreach(string key in entries.Keys)if(key.StartsWith("pending/",StringComparison.Ordinal))keys.Add(key);
            var geometryKeys=new List<string>();
            if(entries!=null)foreach(string key in entries.Keys)if(key.StartsWith("geometry/",StringComparison.Ordinal))geometryKeys.Add(key);
            if(keys.Count+geometryKeys.Count>512)return false;keys.Sort(StringComparer.Ordinal);geometryKeys.Sort(StringComparer.Ordinal);
            var records=new RescuePendingCandidate<UnitSnapshot>[keys.Count];var units=new HashSet<int>();
            for(int i=0;i<keys.Count;i++)
            {
                if(keys[i]!="pending/"+i.ToString("D3",CultureInfo.InvariantCulture) ||
                    !RescuePendingCodec.TryDecode(entries[keys[i]],out records[i]) || !units.Add(records[i].Snapshot.Id))
                {report?.Invoke("pending-restore rejected invalid causal proof; no retrospective repair inferred");return false;}
            }
            var geometry=new RescuePendingGeometry[geometryKeys.Count];
            for(int i=0;i<geometry.Length;i++)
                if(geometryKeys[i]!="geometry/"+i.ToString("D3",CultureInfo.InvariantCulture) ||
                    !RescueGeometryCodec.TryDecode(entries[geometryKeys[i]],out geometry[i]) || !units.Add(geometry[i].Witness.UnitId))
                {report?.Invoke("geometry-restore rejected invalid or overlapping spatial proof");return false;}
            loadedPending=records;loadedGeometry=geometry;return true;
        }
        internal void StageOwnedRecord(VersionedSaveLoadStatus status,byte[] bytes)
        {
            loaded=null;unusable=false;
            if(status==VersionedSaveLoadStatus.Restored)
            {
                if(bytes!=null)loaded=(byte[])bytes.Clone();
                else unusable=true;
            }
            else if(status!=VersionedSaveLoadStatus.NoRecord && status!=VersionedSaveLoadStatus.OutOfScope)unusable=true;
        }
        internal void BeginSession(long id,bool admitMode)
        {
            Supervisor.Reset();legacyDiagnostics.Clear();legacyReports=0;session=id;world=clock=0;clockStarted=false;legacyDrained=false;modeAdmitted=admitMode;
            if(loaded!=null)
            {
                if(!Supervisor.Ledger.TryRestore(loaded,0)){unusable=true;report?.Invoke("worker-restore rejected invalid receipt data; automatic worker recovery unavailable");}
                loaded=null;
            }
            RestorePending?.Invoke(loadedPending);
            if(RestorePending!=null)loadedPending=new RescuePendingCandidate<UnitSnapshot>[0];
            RestoreGeometry?.Invoke(loadedGeometry);
            if(RestoreGeometry!=null)loadedGeometry=Array.Empty<RescuePendingGeometry>();
        }
        internal void EndSession()
        {Supervisor.Reset();legacyDiagnostics.Clear();legacyReports=0;session=world=clock=0;clockStarted=false;loaded=null;loadedPending=new RescuePendingCandidate<UnitSnapshot>[0];loadedGeometry=Array.Empty<RescuePendingGeometry>();unusable=false;modeAdmitted=false;legacyDrained=false;}
        // Called before any work on the admitted tick. Native counters may wrap; ledger
        // deadlines use the monotonic forward distance of this current world only.
        internal bool AdmitTick(int tick)
        {
            // A worker-record failure must not stop the independent ordinary-unit queue.
            if(session==0 || !readWorld(out long currentSession,out long generation) || currentSession!=session)return false;
            if(world!=0 && world!=generation){Supervisor.Reset();legacyDiagnostics.Clear();legacyReports=0;clock=0;clockStarted=false;legacyDrained=false;}
            world=generation;
            if(!clockStarted){lastNativeTick=tick;clockStarted=true;return true;}
            uint distance=unchecked((uint)(tick-lastNativeTick));
            if(distance>int.MaxValue)return false;
            clock+=distance;lastNativeTick=tick;return true;
        }
    }
    internal enum WorkerLegacyRetirement { Deferred, Released, Marked, ReadyToMark }
    internal interface IWorkerLegacyRetirementWorld
    {
        WorkerLegacyRetirement Retire(WorkerResumeTicket ticket,Func<bool> authorityCurrent);
        bool IsPublishedOrder(WorkerResumeTicket ticket,long nativeResult);
    }

    // Uses the already hash/layout-checked NativeRelocation reader. DeleteUnitSafe
    // only marks Life3; normal unit processing owns actual destruction and recount.
    internal sealed unsafe class NativeWorkerLegacyRetirementWorld : IWorkerLegacyRetirementWorld
    {
        private readonly NativeRelocation native;
        internal NativeWorkerLegacyRetirementWorld(NativeRelocation native)
        {this.native=native??throw new ArgumentNullException(nameof(native));}
        public bool IsPublishedOrder(WorkerResumeTicket ticket,long nativeResult)
        {
            if(ticket==null || nativeResult!=1 || !APIShared.UnitAccess.TryGetById(ticket.UnitId,out var unit,out _) || unit==null ||
                !native.TryTribeGlobal(ticket.TribeId,out uint tribeGlobal))return false;
            return IsPublishedOrder(ticket,unit,nativeResult,tribeGlobal,native.Tile(unit->r_CurrentTilePositionX,unit->r_CurrentTilePositionY));
        }
        internal static bool IsPublishedOrder(WorkerResumeTicket ticket,GameUnit* unit,long nativeResult,uint tribeGlobal,int tile)
        {
            if(ticket==null || !ticket.IsValid || !WorkerResumeLedger.IsLegacyCivilian(ticket) || nativeResult!=1 || unit==null ||
                unit->r_GlobalId!=ticket.UnitGlobalId || unit->r_UnitChimp!=ticket.UnitType || unit->r_ControllableForPlayerId!=ticket.Owner ||
                unit->r_AliveState!=SHCDESE.Interop.Enums.AliveState.IsAlive || unit->r_TribeId!=ticket.TribeId || tribeGlobal!=ticket.TribeGlobalId ||
                ticket.TribeId!=0 && Dword(unit,0x93C)!=tribeGlobal || tile<0 || unit->r_CurrentPositionTileId!=(uint)tile)return false;
            int flags=unit->r_PathPlanStateBitFlags;
            return (flags==2 || flags==4) && unit->r_PathPlanLength>0 || flags==0 &&
                unit->r_CurrentTilePositionX==unit->r_TargetTilePositionX2 && unit->r_CurrentTilePositionY==unit->r_TargetTilePositionY2;
        }
        public WorkerLegacyRetirement Retire(WorkerResumeTicket ticket,Func<bool> authorityCurrent)=>Run(ticket,authorityCurrent,
            ()=>Inspect(ticket),()=>APIShared.UnitAccess.TryGetById(ticket.UnitId,out _,out _) && GameUnitManagerAPI.Instance.DeleteUnitSafe(ticket.UnitId),()=>
                APIShared.UnitAccess.TryGetById(ticket.UnitId,out var unit,out _) && unit!=null && unit->r_GlobalId==ticket.UnitGlobalId &&
                unit->r_UnitChimp==ticket.UnitType && unit->r_ControllableForPlayerId==ticket.Owner && unit->r_AliveState==SHCDESE.Interop.Enums.AliveState.MarkedForDeletion);
        private WorkerLegacyRetirement Inspect(WorkerResumeTicket ticket)
        {
            if(!APIShared.UnitAccess.TryGetById(ticket.UnitId,out var unit,out _) || unit==null)return WorkerLegacyRetirement.Deferred;
            if(unit->r_GlobalId!=ticket.UnitGlobalId || unit->r_AliveState!=SHCDESE.Interop.Enums.AliveState.IsAlive ||
                unit->r_UnitChimp!=ticket.UnitType || unit->r_ControllableForPlayerId!=ticket.Owner || unit->r_TribeId!=ticket.TribeId)
                return WorkerLegacyRetirement.Released;
            int tile=native.Tile(unit->r_CurrentTilePositionX,unit->r_CurrentTilePositionY);
            if(!native.TryTribeGlobal(ticket.TribeId,out uint tribeGlobal))return WorkerLegacyRetirement.Deferred;
            return InspectBarrier(ticket,unit,tribeGlobal,tile,tile>=0 && native.HasOccupancyExactlyOnce(tile,ticket.UnitId));
        }
        // The production wrapper and offline fixture execute this same transaction.
        internal static WorkerLegacyRetirement Run(WorkerResumeTicket ticket,Func<bool> authorityCurrent,
            Func<WorkerLegacyRetirement> inspect,Func<bool> mark,Func<bool> marked)
        {
            if(ticket==null || !ticket.IsValid || !WorkerResumeLedger.IsLegacyCivilian(ticket) || authorityCurrent==null ||
                !authorityCurrent())return WorkerLegacyRetirement.Deferred;
            var state=inspect();if(state!=WorkerLegacyRetirement.ReadyToMark)return state;
            if(!authorityCurrent())return WorkerLegacyRetirement.Deferred;
            state=inspect();if(state!=WorkerLegacyRetirement.ReadyToMark)return state;
            if(!authorityCurrent() || !mark())return WorkerLegacyRetirement.Deferred;
            return authorityCurrent() && marked()?WorkerLegacyRetirement.Marked:WorkerLegacyRetirement.Deferred;
        }
        internal static WorkerLegacyRetirement InspectBarrier(WorkerResumeTicket ticket,GameUnit* unit,uint tribeGlobal,int tile,bool occupancy)
        {
            if(ticket==null || !ticket.IsValid || !WorkerResumeLedger.IsLegacyCivilian(ticket))return WorkerLegacyRetirement.Deferred;
            if(unit==null || unit->r_GlobalId!=ticket.UnitGlobalId || unit->r_UnitChimp!=ticket.UnitType ||
                unit->r_ControllableForPlayerId!=ticket.Owner || unit->r_AliveState!=SHCDESE.Interop.Enums.AliveState.IsAlive ||
                unit->r_TribeId!=ticket.TribeId || tribeGlobal!=ticket.TribeGlobalId ||
                ticket.TribeId!=0 && Dword(unit,0x93C)!=tribeGlobal || unit->r_CurrentHealth<=0 || Word(unit,0x8F8)!=0 ||
                unit->r_AIState>=108)return WorkerLegacyRetirement.Released;
            if(unit->r_PathPlanStateBitFlags!=5)return WorkerLegacyRetirement.Released;
            if(Word(unit,0x6EC)!=0 || Word(unit,0x9C8)>1 || Word(unit,0x9B0)!=0 || Word(unit,0x8EC)!=0 || Word(unit,0x8F6)!=0 ||
                !NativeRelocation.IsNormalizedPose(unit,tile,Dword(unit,0x740)) || !occupancy)return WorkerLegacyRetirement.Deferred;
            return WorkerLegacyRetirement.ReadyToMark;
        }
        private static ushort Word(GameUnit* unit,int offset)=>*(ushort*)((byte*)unit+offset-0x65C);
        private static uint Dword(GameUnit* unit,int offset)=>*(uint*)((byte*)unit+offset-0x65C);
    }

}
