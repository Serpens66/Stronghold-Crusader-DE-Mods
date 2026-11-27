using System;
using System.Globalization;
using System.Threading;
using APIShared;
using APIShared.GameModes;
using APIShared.Simulation;
using APIShared.Units;
using APIShared.WorldMutations;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;

namespace BugfixesAndQoL.TrappedUnitRescue
{
    // Hosted by the existing BQoL bootstrap. Published brokers retain this normal
    // managed object after Unity destroys the early plugin component.
    internal sealed class TrappedUnitRescueFeature
    {
        private static TrappedUnitRescueFeature processOwner;
        private readonly string ownerGuid;
        private readonly ManualLogSource log;
        private readonly Func<bool> configuredEnable;
        private MissionContext current;
        private long boundSession;
        private int attempted,ready,civilianRemovalTick;
        private NativeRelocation native;
        private RescueRuntime runtime;
        private GeneralRescueObserver generalObserver;
        private WorkerRecoveryService workers;
        private IdleEscapeOwner idleEscapes;
        private NativeIdleEscapeWorld idleEscapeWorld;
        private FixesIntegration fixes;
        private RescueOrderObserver orders;

        internal TrappedUnitRescueFeature(string ownerGuid,ManualLogSource log,Func<bool> configuredEnable)
        {
            if(string.IsNullOrWhiteSpace(ownerGuid))throw new ArgumentException("A stable owner GUID is required.",nameof(ownerGuid));
            this.ownerGuid=ownerGuid;this.log=log??throw new ArgumentNullException(nameof(log));
            this.configuredEnable=configuredEnable??throw new ArgumentNullException(nameof(configuredEnable));
        }
        internal bool Ready=>Volatile.Read(ref ready)!=0;
        internal bool Active=>Ready && generalObserver?.IsActive==true;
        internal GeneralRescueObserver GeneralObserver=>generalObserver;
        internal RescueRuntime Runtime=>runtime;
        internal WorkerRecoveryService Recovery=>workers;

        // The host must publish Lord.HomeTeleportGuard before this call. This
        // adapter only borrows that audited entry; it installs no relocation hook.
        internal void Initialize(CrusaderLibraryLoadContext context)
        {
            if(Interlocked.Exchange(ref attempted,1)!=0)return;
            if(Interlocked.CompareExchange(ref processOwner,this,null)!=null)
            {ReportError("UNAVAILABLE another process-lifetime rescue host already owns initialization");return;}
            try
            {
                if(context==null)throw new ArgumentNullException(nameof(context));
#if SERP_DEVDIAG
                RescueCaptureDiagnostics.Initialize();
#endif
                native=NativeRelocation.Create(ownerGuid);
                fixes=new FixesIntegration(ownerGuid,log,native);
                fixes.Initialize(context);
                if(!fixes.Ready)throw new InvalidOperationException("Relevant Fixes compatibility contracts are unavailable.");
                runtime=new RescueRuntime(native,()=>Ready && Configured() && generalObserver?.IsActive==true,Report);
                generalObserver=new GeneralRescueObserver(runtime,SetSuppression,Report);
                runtime.CanRemoveCivilian=()=>Ready && Volatile.Read(ref civilianRemovalTick)!=0 && generalObserver.IsRecovering;
                generalObserver.ReplanIntents=new NativeRescueReplanIntentWorld(runtime,new NativeWorkerResumeWorld(native));
                generalObserver.ReplanOwnerGuid=ownerGuid;
                generalObserver.OwnReplanScope=()=>Ready && (workers?.Supervisor.IsExecutingNativeCall==true ||
                    generalObserver.IsRecovering || idleEscapes?.IsExecutingNativeCall==true);
                workers=new WorkerRecoveryService(native,()=>generalObserver.IsRecovering || idleEscapes?.IsExecutingNativeCall==true,Report);
                idleEscapeWorld=new NativeIdleEscapeWorld(native,ownerGuid,IdleEscapeAuthority,native.TryReissue);
                idleEscapes=new IdleEscapeOwner(idleEscapeWorld);
                runtime.Workers=workers.Supervisor;runtime.WorkerTick=()=>workers.TickValue;runtime.WorkerReady=()=>Ready && workers.Ready;
                runtime.IdleEscapes=idleEscapes;
                runtime.CreateIdleEscapeRequest=(before,target)=>Ready && WorldMutationEvents.TryGetCurrentWorld(out long session,out long generation)?
                    new IdleEscapeRequest(session,generation,generalObserver.GetOrderSerial(before.Id),before,target,true):null;
                generalObserver.WorldMutated=workers.Supervisor.MarkWorldMutation;
                workers.CapturePending=generalObserver.TryCapturePending;workers.RestorePending=generalObserver.StagePending;
                workers.CaptureGeometry=generalObserver.TryCaptureGeometry;workers.RestoreGeometry=generalObserver.StageGeometry;

                if(!UnitMoveEvents.TryRegister(ownerGuid,"idle-escape-source-phase",idleEscapeWorld,out string idleReason,
                    enabled:()=>Ready && idleEscapeWorld.IsArmed))throw new InvalidOperationException("Idle source observation: "+idleReason);
                if(!WorldMutationEvents.TryRegister(ownerGuid,"rescue-world-mutations",generalObserver,out string mutationReason,
                    enabled:()=>Ready && generalObserver.IsActive))throw new InvalidOperationException("World mutation observation: "+mutationReason);
                if(!SimulationTickEvents.TryRegister(ownerGuid,"rescue-candidates",RecoveryTick,out string tickReason))
                    throw new InvalidOperationException("Recovery tick: "+tickReason);
                orders=new RescueOrderObserver(ownerGuid,()=>workers.Supervisor.IsExecutingNativeCall || generalObserver.IsRecovering || idleEscapes.IsExecutingNativeCall,
                    id=>{idleEscapes.Release(id);generalObserver.ObserveOrder(id,true);if(workers.Ready)workers.Supervisor.ObserveOrderPre(id);},
                    (id,result)=>{try{if(workers.Ready)workers.Supervisor.ObserveOrderPost(id,workers.TickValue,result);}
                        finally{generalObserver.EndExternalOrder(id);}},
                    id=>generalObserver.IsInterestedInOrder(id) || idleEscapes.Contains(id) || workers.Supervisor.Ledger.TryGetOwnedByUnit(id,out _),
                    id=>{try{workers.Supervisor.ObserveOrderAborted(id,workers.TickValue);}
                        finally{generalObserver.EndExternalOrder(id);}});
                if(!UnitMoveEvents.TryRegister(ownerGuid,"rescue-order-identity",orders,out string orderReason,
                    enabled:()=>Ready && (generalObserver.HasEvidence || idleEscapes.Count!=0 || workers.Supervisor.Ledger.Count!=0)))
                    throw new InvalidOperationException("Order identity observation: "+orderReason);
                if(!UnitMoveEvents.TryRegister(ownerGuid,"rescue-replan-proof",generalObserver,out string replanReason,order:100,
                    enabled:()=>Ready && generalObserver.HasEvidence))throw new InvalidOperationException("Replan observation: "+replanReason);
                // Last fallible registration: failed partial initialization must not leave
                // a live atomic save participant behind a permanently inactive host.
                if(!workers.Register(ownerGuid,out string saveReason))throw new InvalidOperationException("Rescue persistence: "+saveReason);
                Volatile.Write(ref ready,1);
                Report("READY event-driven rescue registered; activation awaits host session/settings");
            }
            catch(Exception error)
            {
                Volatile.Write(ref ready,0);fixes?.SetEnabled(false);generalObserver?.SetSession(0);idleEscapes?.Clear();
                ReportError("UNAVAILABLE "+error);
                // Published registrations/hooks remain rooted and logically inactive.
            }
        }

        internal void BeginSession(MissionContext context)
        {
            if(context==null || context.SessionId<=0)throw new ArgumentException("A current mission is required.",nameof(context));
            Volatile.Write(ref current,context);
            if(!Ready)return;
            if(boundSession==context.SessionId){RefreshSession();return;}
            idleEscapes.Clear();fixes.RecheckPublishedHooks();
            RefreshSession();
            workers.BeginSession(context.SessionId,LocalModeAllowed(context));
            boundSession=context.SessionId;
        }
        internal void EndSession()
        {
            Volatile.Write(ref current,null);boundSession=0;
            fixes?.SetEnabled(false);idleEscapes?.Clear();generalObserver?.SetSession(0);generalObserver?.ClearPending();workers?.EndSession();
            Volatile.Write(ref civilianRemovalTick,0);
        }
        internal void ApplySettings()
        {
            if(!Ready)return;
            var mission=Volatile.Read(ref current);
            if(mission!=null && boundSession!=mission.SessionId){BeginSession(mission);return;}
            RefreshSession();
        }
        private bool Configured()
        {
            try{return configuredEnable();}
            catch(Exception error){ReportError("SETTINGS_UNAVAILABLE "+error);return false;}
        }
        private static bool LocalModeAllowed(MissionContext mission)=>mission!=null && !mission.Mode.IsRealMultiplayer &&
            !mission.Mode.HasConflictingCustomizedOrigin && mission.Mode.Kind!=GameModeKind.Unknown;
        private void RefreshSession()
        {
            var mission=Volatile.Read(ref current);
            bool enabled=Ready && Configured() && fixes.Ready && LocalModeAllowed(mission);
            fixes?.SetEnabled(false);
            if(!enabled)idleEscapes?.Clear();
            generalObserver?.SetSession(enabled?mission.SessionId:0);
        }
        private bool IdleEscapeAuthority(IdleEscapeRequest request)
        {
            var mission=Volatile.Read(ref current);
            return Ready && request!=null && Configured() && LocalModeAllowed(mission) && mission.SessionId==request.Session &&
                generalObserver.IsActive && WorldMutationEvents.TryGetCurrentWorld(out long session,out long generation) &&
                session==request.Session && generation==request.Generation && generalObserver.GetOrderSerial(request.Before.Id)==request.OrderSerial;
        }
        private void SetSuppression(bool enabled)
        {
            if(!enabled){fixes?.SetEnabled(false);return;}
            var mission=Volatile.Read(ref current);
            if(!Ready || !generalObserver.IsActive || !fixes.Ready || !Configured() || !LocalModeAllowed(mission) ||
                !WorldMutationEvents.TryGetCurrentWorld(out long session,out _) || session!=mission.SessionId)
                throw new InvalidOperationException("Rescue suppression requires the active owned local session.");
            fixes.SetEnabled(true);
        }
        private void RecoveryTick(int tick)
        {
            if(!Ready || generalObserver.HasOpenExternalCalls)return;
            generalObserver.ExpireInactivePending();
            bool enabled=Configured();
            if(!enabled)idleEscapes.Clear();
            if((generalObserver.PendingCount==0 && workers.Supervisor.Ledger.Count==0 && idleEscapes.Count==0) || !workers.AdmitTick(tick))return;
            if(!UnitMoveEvents.IsAvailable)idleEscapes.Clear();
            workers.AdvanceLegacyTick(tick); // Historical owned civilian barriers, including OFF:32visits/4writes.
#if SERP_DEVDIAG
            long diagnosticStarted=0;
            Shared.DevDiag.BeginBatch();Shared.DevDiag.Start(ref diagnosticStarted);
            try
            {
#endif
                Volatile.Write(ref civilianRemovalTick,1);
                try{generalObserver.Tick(tick);}
                finally{Volatile.Write(ref civilianRemovalTick,0);}
                long completed=idleEscapes.CompletedCount;
                idleEscapes.Advance(unchecked((uint)workers.TickValue)); // Existing eight-receipt budget.
                if(idleEscapes.CompletedCount!=completed)Report("IDLE_ESCAPE_ARRIVED count="+(idleEscapes.CompletedCount-completed));
                if(workers.Supervisor.Ledger.Count==0 || !workers.Ready || !enabled || !Active)return;
                SetSuppression(true);
#if SERP_DEVDIAG
                long supervisorStarted=0;Shared.DevDiag.Start(ref supervisorStarted);
#endif
                try{workers.Supervisor.Tick(workers.TickValue,32,4);}
                finally
                {
#if SERP_DEVDIAG
                    Shared.DevDiag.StopSpike(ref supervisorStarted,"rescue.tick.supervisor",2000);
#endif
                    SetSuppression(false);
                }
#if SERP_DEVDIAG
            }
            finally
            {
                Shared.DevDiag.StopSpike(ref diagnosticStarted,"rescue.tick.total.inclusive",2000);
                Shared.DevDiag.EndBatch();
            }
#endif
        }
        private void Report(string message)
        {
            try{log.LogInfo("["+DateTime.Now.ToString("HH:mm:ss.fff",CultureInfo.InvariantCulture)+"] TRAPPED_UNIT_RESCUE "+message);}catch{}
        }
        private void ReportError(string message)
        {
            try{log.LogError("["+DateTime.Now.ToString("HH:mm:ss.fff",CultureInfo.InvariantCulture)+"] TRAPPED_UNIT_RESCUE "+message);}catch{}
        }
    }
}
