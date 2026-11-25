using System;
using System.IO;
using System.Threading;
using APIShared;
using APIShared.ModSettings;
using APIShared.WorldMutations;
using APIShared.Simulation;
using APIShared.Units;
using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;

namespace TrappedUnitRescueTest
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("000shcdese", "2.14.3")]
    [BepInDependency("APIShared_Serp", "0.74.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Pathfinding_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("BugfixesAndQoL_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "TrappedUnitRescueTest_Serp", Name = "Trapped Unit Rescue Test", Version = "0.1.0";
        private static ManualLogSource log;
        private static Settings settings;
        private static RescueRuntime runtime;
        private static GeneralRescueObserver generalObserver;
        private static WorkerRecoveryService workers;
        private static FixesIntegration fixes;
        private static Fixture fixture;
        private static IMissionLifecycleCapability missions;
        private static int active, fixtureActive, generalFixture;
        internal static GeneralRescueObserver GeneralObserver => generalObserver;
        private static bool initialized, libraryReady, settingsReady;
        private static Plugin instance;
        private static string configuredMap;
        private static int configuredPeaceTime;
        private static int startupThread;
        private static RescueOrderObserver orders;

        private void Awake()
        {
            if (initialized) return;
            initialized = true; instance = this; log = Logger; settings = new Settings();
            startupThread = Thread.CurrentThread.ManagedThreadId;
            configuredMap = Config.Bind("Fixture", "Map", "test AI.map", "Disposable local Skirmish map; suitability is checked before each scenario.").Value;
            configuredPeaceTime = Config.Bind("Fixture", "PeaceTimeMinutes", 0, "Optional ordinary lobby peace time; requires a lobby that offers it.").Value;
            // Static fields, LibraryLoaded and public mission/automation publishers retain all work
            // after SHCDE destroys early plugin components. There is deliberately no Unity teardown.
            CrusaderLibrary.Instance.LibraryLoaded += LibraryLoaded;
            ApiShared.ForMod(Guid).WhenReady(_ => {
                // Registration requires the still-live plugin during synchronous Chainloader startup.
                // Only starting automation is deferred; deferring registration would lose the component.
                if (Thread.CurrentThread.ManagedThreadId != startupThread) { Stamp("INIT_FAILED settings readiness left the startup Unity thread"); return; }
                InitializeSettings();
            });
        }

        private static void LibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (libraryReady) return;
            try
            {
                Shared.NativePatchGuard.Bind(context, log);
                var native = NativeRelocation.Create(Guid);
                fixes = new FixesIntegration(log, native);
                fixes.Initialize(context);
                runtime = new RescueRuntime(native, () => Volatile.Read(ref active) != 0, Stamp);
                var generalRuntime = new RescueRuntime(native, () => generalObserver != null && generalObserver.IsActive, Stamp);
                generalObserver = new GeneralRescueObserver(generalRuntime, SetGeneralSuppression, Stamp);
                workers=new WorkerRecoveryService(native,()=>generalObserver.IsRecovering,Stamp);
                if(!workers.Register(Guid,out string saveReason))throw new InvalidOperationException("Worker persistence unavailable: "+saveReason);
                foreach(var rescue in new[]{runtime,generalRuntime})
                {rescue.Workers=workers.Supervisor;rescue.WorkerTick=()=>workers.TickValue;rescue.WorkerReady=()=>workers.Ready;}
                generalObserver.WorldMutated=workers.Supervisor.MarkWorldMutation;
                workers.CapturePending=generalObserver.TryCapturePending;
                workers.RestorePending=generalObserver.StagePending;
                if (!WorldMutationEvents.TryRegister(Guid,"rescue-world-mutations",generalObserver,out string mutationReason,
                    enabled:()=>generalObserver.IsActive))
                    throw new InvalidOperationException("World-mutation observer unavailable: " + mutationReason);
                if(!SimulationTickEvents.TryRegister(Guid,"rescue-candidates",RecoveryTick,out string tickReason))
                    throw new InvalidOperationException("Recovery tick unavailable: " + tickReason);
                orders=new RescueOrderObserver(Guid,()=>workers.Supervisor.IsExecutingNativeCall || generalObserver.IsRecovering,
                    id=>{generalObserver.ObserveOrder(id,true);if(workers.Ready)workers.Supervisor.ObserveOrderPre(id);},
                    (id,result)=>{if(workers.Ready)workers.Supervisor.ObserveOrderPost(id,workers.TickValue,result);},
                    id=>generalObserver.IsInterestedInOrder(id) || workers.Supervisor.Ledger.TryGetOwnedByUnit(id,out _));
                if(!UnitMoveEvents.TryRegister(Guid,"rescue-order-identity",orders,out string orderReason,
                    enabled:()=>generalObserver.HasEvidence || workers.Supervisor.Ledger.Count!=0))
                    throw new InvalidOperationException("Final unit-move observations unavailable: "+orderReason);
                libraryReady = true;
                UpdateGeneralSession();
                Stamp("NATIVE_READY; experimental setting defaults off; gatehouse farmer fix retained");
                if (settingsReady) QueueRequestedFixture();
            }
            catch (Exception ex) { SetActive(false); generalObserver?.SetSession(0); Stamp("INIT_FAILED " + ex); }
        }

        private static void InitializeSettings()
        {
            try
            {
                LobbyModSettingsPresetRegistration.Register(instance, log, Name, settings,
                    "ScriptExtenderUI/TrappedUnitRescueTestSettings.xaml");
                if (!ApiShared.ForMod(Guid).TryGetMissionLifecycle(out missions, out var diagnostic))
                    throw new InvalidOperationException(diagnostic.Reason);
                if (!missions.TryRegisterObserver("rescue-session", n => {
                    fixes?.RecheckPublishedHooks();
                    SetActive(Volatile.Read(ref fixtureActive) != 0 && settings.EnableMod && settings.EnableExperimentalRescue);
                    UpdateGeneralSession();
                    workers?.BeginSession(n.Context.SessionId,LocalModeAllowed(n.Context));
                }, n => { generalObserver?.SetSession(0); generalObserver?.ClearPending(); workers?.EndSession(); Volatile.Write(ref generalFixture, 0); SetActive(false); fixture?.Stop(); }, null, out var observerDiagnostic))
                    throw new InvalidOperationException(observerDiagnostic.Reason);
                settingsReady = true;
                UpdateGeneralSession();
                Stamp("SETTINGS_READY; no request means no automatic scenario; event-driven rescue requires explicit setting");
                if (libraryReady) QueueRequestedFixture();
            }
            catch (Exception ex) { SetActive(false); Stamp("INIT_FAILED settings: " + ex); }
        }

        private static void QueueRequestedFixture()
        {
            if (!ApiShared.ForMod(Guid).Ui.TryPost(StartRequestedFixture)) Stamp("INIT_FAILED Unity dispatcher unavailable");
        }
        private static void RecoveryTick(int tick)
        {
            generalObserver?.ExpireInactivePending();
            if(workers==null || (generalObserver.PendingCount==0 && workers.Supervisor.Ledger.Count==0) || !workers.AdmitTick(tick))return;
            if(!UnitMoveEvents.IsAvailable){generalObserver.ClearPending();return;}
            generalObserver?.Tick(tick);
            if(workers.Supervisor.Ledger.Count==0 || !workers.Ready || !settings.EnableMod || !settings.EnableExperimentalRescue)return;
            bool fixtureScope=Volatile.Read(ref fixtureActive)!=0 && Volatile.Read(ref generalFixture)==0;
            if(fixtureScope)SetSuppression(true);else SetGeneralSuppression(true);
            try{workers.Supervisor.Tick(workers.TickValue,32,4);}
            finally{if(fixtureScope)SetSuppression(false);else SetGeneralSuppression(false);}
        }
        private static bool LocalModeAllowed(MissionContext current)=>current!=null && !current.Mode.IsRealMultiplayer &&
            !current.Mode.HasConflictingCustomizedOrigin && current.Mode.Kind!=APIShared.GameModes.GameModeKind.Unknown;

        private static void StartRequestedFixture()
        {
            if (fixture != null) return;
            string request = Path.Combine(Paths.ConfigPath, "SerpAutomation", Guid + ".run");
            if (!File.Exists(request)) return;
            string[] lines = File.ReadAllLines(request);
            if (lines.Length < 2 || !Fixture.Owns(lines[1])) return;
            if (!System.Guid.TryParseExact(lines[0], "N", out var runId) || runId == System.Guid.Empty)
            { Stamp("Invalid owned request; not consumed"); return; }
            fixture = new Fixture(configuredMap, lines[1], Stamp, runtime, enabled => {
                if (enabled && (fixes == null || !fixes.Ready))
                    throw new InvalidOperationException("Experimental rescue is unavailable; the validated Fixes integration did not become ready. Original behavior remains active.");
                settings.EnableMod = true; settings.EnableExperimentalRescue = enabled;
                if (!settings.EnableMod || settings.EnableExperimentalRescue != enabled)
                    throw new InvalidOperationException("Requested test settings were not accepted before mission start.");
                Interlocked.Exchange(ref fixtureActive, 1);
                generalObserver?.SetSession(0);
                SetActive(enabled);
            }, SetSuppression, () => {
                if (fixes == null || !fixes.Ready)
                    throw new InvalidOperationException("Moat-worker baseline requires a validated Fixes installation state.");
                return fixes.WorkerDeletionInstalled;
            }, configuredPeaceTime);
            File.Delete(request);
            ApiShared.ForMod(Guid).Automation.Start(runId, fixture.Steps(), log,
                result => {
                    generalObserver?.SetSession(0); Volatile.Write(ref generalFixture, 0);
                    SetActive(false); Interlocked.Exchange(ref fixtureActive, 0); fixture.Stop();
                    Stamp("finished status=" + result.Status + " detail=" + result.Detail);
                }, quitWhenDone: true);
        }

        private static void SetActive(bool enabled)
        {
            bool allow = enabled && runtime != null && fixes != null && fixes.Ready &&
                missions?.Current != null && Volatile.Read(ref fixtureActive) != 0;
            // Mission activation enables our explicit rescue calls, never a mission-wide
            // replacement of natural AI/Fixes behavior. Execute opens the narrower gate.
            fixes?.SetEnabled(false);
            Volatile.Write(ref active, allow ? 1 : 0);
        }
        private static void SetSuppression(bool enabled)
        {
            if (!enabled) { fixes?.SetEnabled(false); return; }
            if (Volatile.Read(ref active) == 0 || fixes == null || !fixes.Ready ||
                missions?.Current == null || Volatile.Read(ref fixtureActive) == 0)
                throw new InvalidOperationException("Fixes suppression requires the active, ready, explicit test mission.");
            fixes.SetEnabled(true);
        }
        private static void UpdateGeneralSession()
        {
            var current = missions?.Current;
            bool allow = libraryReady && settingsReady && settings.EnableMod && settings.EnableExperimentalRescue &&
                (Volatile.Read(ref fixtureActive) == 0 || Volatile.Read(ref generalFixture) != 0) && fixes != null && fixes.Ready && current != null &&
                !current.Mode.IsRealMultiplayer && !current.Mode.HasConflictingCustomizedOrigin &&
                current.Mode.Kind != APIShared.GameModes.GameModeKind.Unknown;
            generalObserver?.SetSession(allow ? current.SessionId : 0);
            if (allow) Stamp("GENERAL_PILOT_ACTIVE session=" + current.SessionId + " sources="+WorldMutationEvents.AvailableSources+"; recovery budgets active; original Fixes unchanged");
        }
        private static void SetGeneralSuppression(bool enabled)
        {
            if (!enabled) { fixes?.SetEnabled(false); return; }
            if (generalObserver == null || !generalObserver.IsActive ||
                (Volatile.Read(ref fixtureActive) != 0 && Volatile.Read(ref generalFixture) == 0) ||
                fixes == null || !fixes.Ready || missions?.Current == null)
                throw new InvalidOperationException("General rescue requires the active local rescue session.");
            fixes.SetEnabled(true);
        }
        // Explicit observer fixture opt-in; callers hold the public simulation monitor and
        // invoke the LIVE AIV entry without Fixture.Execute's construction suppression scope.
        internal static void SetGeneralFixtureSession(bool enabled)
        {
            if (Volatile.Read(ref fixtureActive) == 0 || missions?.Current == null)
                throw new InvalidOperationException("Observer fixture requires its current owned mission.");
            fixes?.SetEnabled(false);
            Volatile.Write(ref generalFixture, enabled ? 1 : 0);
            if (enabled) UpdateGeneralSession(); else generalObserver?.SetSession(0);
        }
        private static void Stamp(string message) => log.LogInfo("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] RESCUE_TEST " + message);
    }
}
