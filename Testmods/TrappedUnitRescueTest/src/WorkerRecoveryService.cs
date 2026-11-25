using System;
using System.Collections.Generic;
using System.Globalization;
using APIShared.Savegames;
using APIShared.WorldMutations;

namespace TrappedUnitRescueTest
{
    internal delegate bool WorkerWorldReader(out long session,out long generation);
    internal delegate bool RescuePendingCapture(out RescuePendingCandidate<UnitSnapshot>[] records);
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
        internal RescuePendingCapture CapturePending { get; set; }
        internal Action<RescuePendingCandidate<UnitSnapshot>[]> RestorePending { get; set; }
        private bool unusable,clockStarted,modeAdmitted;
        private long session,world,clock;
        private int lastNativeTick;
        internal long TickValue=>clock;
        internal bool Ready=>!unusable && session!=0 && modeAdmitted;
        internal WorkerRecoveryService(NativeRelocation native,Func<bool> isRecovering,Action<string> report)
            : this(new WorkerResumeSupervisor(new NativeWorkerResumeWorld(native)),isRecovering,report,WorldMutationEvents.TryGetCurrentWorld) {}
        internal WorkerRecoveryService(WorkerResumeSupervisor supervisor,Func<bool> isRecovering,Action<string> report,WorkerWorldReader readWorld)
        {Supervisor=supervisor??throw new ArgumentNullException(nameof(supervisor));this.isRecovering=isRecovering??throw new ArgumentNullException(nameof(isRecovering));
            this.report=report;this.readWorld=readWorld??throw new ArgumentNullException(nameof(readWorld));}
        internal bool Register(string ownerGuid,out string reason)
        {
            var registration=new VersionedSaveDataRegistration(ownerGuid,"TrappedUnitRescueOwnedWorkers",1,1,
                new[]{"owned-workers","pending/"},Capture,Load)
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
            if(pending==null || pending.Length>512)return false;
            var encoded=new byte[pending.Length][];var units=new HashSet<int>();
            for(int i=0;i<pending.Length;i++)
                if(pending[i].Snapshot==null || !units.Add(pending[i].Snapshot.Id) || !RescuePendingCodec.TryEncode(pending[i],out encoded[i]))return false;
            // Entries are written only after every pending proof has validated.
            var result=new SortedDictionary<string,byte[]>(StringComparer.Ordinal){{"owned-workers",bytes}};
            for(int i=0;i<encoded.Length;i++)result.Add("pending/"+i.ToString("D3",CultureInfo.InvariantCulture),encoded[i]);
            entries=result;return true;
        }
        internal bool TryCaptureOwnedRecord(out byte[] bytes)
        {
            bytes=null;
            return !unusable && !isRecovering() && !Supervisor.IsExecutingNativeCall && Supervisor.Ledger.TrySerialize(out bytes);
        }
        private void Load(VersionedSaveLoad load)
        {
            load.Entries.TryGetValue("owned-workers",out byte[] bytes);
            StageOwnedRecord(load.Status,bytes);
            StagePendingRecords(load.Status,load.Entries);
        }
        internal bool StagePendingRecords(VersionedSaveLoadStatus status,IReadOnlyDictionary<string,byte[]> entries)
        {
            loadedPending=new RescuePendingCandidate<UnitSnapshot>[0];
            if(status!=VersionedSaveLoadStatus.Restored)return true;
            var keys=new List<string>();
            if(entries!=null)foreach(string key in entries.Keys)if(key.StartsWith("pending/",StringComparison.Ordinal))keys.Add(key);
            if(keys.Count>512)return false;keys.Sort(StringComparer.Ordinal);
            var records=new RescuePendingCandidate<UnitSnapshot>[keys.Count];var units=new HashSet<int>();
            for(int i=0;i<keys.Count;i++)
            {
                if(keys[i]!="pending/"+i.ToString("D3",CultureInfo.InvariantCulture) ||
                    !RescuePendingCodec.TryDecode(entries[keys[i]],out records[i]) || !units.Add(records[i].Snapshot.Id))
                {report?.Invoke("pending-restore rejected invalid causal proof; no retrospective repair inferred");return false;}
            }
            loadedPending=records;return true;
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
            Supervisor.Reset();session=id;world=clock=0;clockStarted=false;modeAdmitted=admitMode;
            if(loaded!=null)
            {
                if(!Supervisor.Ledger.TryRestore(loaded,0)){unusable=true;report?.Invoke("worker-restore rejected invalid receipt data; automatic worker recovery unavailable");}
                loaded=null;
            }
            RestorePending?.Invoke(loadedPending);
            if(RestorePending!=null)loadedPending=new RescuePendingCandidate<UnitSnapshot>[0];
        }
        internal void EndSession()
        {Supervisor.Reset();session=world=clock=0;clockStarted=false;loaded=null;loadedPending=new RescuePendingCandidate<UnitSnapshot>[0];unusable=false;modeAdmitted=false;}
        // Called before any work on the admitted tick. Native counters may wrap; ledger
        // deadlines use the monotonic forward distance of this current world only.
        internal bool AdmitTick(int tick)
        {
            // A worker-record failure must not stop the independent ordinary-unit queue.
            if(session==0 || !readWorld(out long currentSession,out long generation) || currentSession!=session)return false;
            if(world!=0 && world!=generation){Supervisor.Reset();clock=0;clockStarted=false;}
            world=generation;
            if(!clockStarted){lastNativeTick=tick;clockStarted=true;return true;}
            uint distance=unchecked((uint)(tick-lastNativeTick));
            if(distance>int.MaxValue)return false;
            clock+=distance;lastNativeTick=tick;return true;
        }
    }
}
