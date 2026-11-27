using System;
using System.IO;
using System.Linq;
using System.Threading;
using APIShared;
using BugfixesAndQoL;
using BugfixesAndQoL.TrappedUnitRescue;
using BepInEx.Bootstrap;
using System.Reflection;
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
    [BepInDependency("APIShared_Serp", "0.78.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Pathfinding_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("BugfixesAndQoL_Serp")]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string Guid = "TrappedUnitRescueTest_Serp", Name = "Trapped Unit Rescue Test", Version = "0.1.0";
        private static ManualLogSource log;
        private static BugfixesAndQoLViewModel settings;
        internal const string RescueOwnerGuid="BugfixesAndQoL_Serp";
        private static TrappedUnitRescueFeature host;
        private static RescueRuntime runtime;
        private static GeneralRescueObserver generalObserver;
        private static WorkerRecoveryService workers;
        private static FixesIntegration fixes;
        private static Fixture fixture;
        private static RescueScenarioSeries series;
        private static IMissionLifecycleCapability missions;
        private static int active, fixtureActive, generalFixture;
        internal static GeneralRescueObserver GeneralObserver => generalObserver;
        private static bool initialized, libraryReady, settingsReady, startupFailureRun;
        private static string startupFailure;
        private static Plugin instance;
        private static string configuredMap;
        private static string configuredScenarios;
        private static int configuredPeaceTime;
        private static int startupThread;

        private void Awake()
        {
            if (initialized) return;
            initialized = true; instance = this; log = Logger;
            Shared.DevDiag.Attach(log);
            startupThread = Thread.CurrentThread.ManagedThreadId;
            configuredMap = Config.Bind("Fixture", "Map", "4spielerkarte.map", "Disposable local Skirmish map; suitability is checked before each scenario.").Value;
            configuredPeaceTime = Config.Bind("Fixture", "PeaceTimeMinutes", 60, "Explicit peace time for the disposable test world.").Value;
            configuredScenarios = Config.Bind("Fixture", "Scenarios", "", "Ordered comma-separated cases for the owned rescue-series request.").Value;
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
            if(libraryReady)return;
            try
            {
                if(!Chainloader.PluginInfos.TryGetValue(RescueOwnerGuid,out var info) || ReferenceEquals(info.Instance,null))
                    throw new InvalidOperationException("Actual BQoL host is absent.");
                var plugin=info.Instance as BugfixesAndQoLPlugin;
                if(ReferenceEquals(plugin,null))throw new InvalidOperationException("BQoL plugin type differs from the referenced selected assembly.");
                var field=typeof(BugfixesAndQoLPlugin).GetField("runtime",BindingFlags.Instance|BindingFlags.NonPublic);
                var owner=field?.GetValue(plugin);
                var feature=typeof(BugfixesAndQoLRuntime).GetField("trappedUnitRescueFeature",BindingFlags.Instance|BindingFlags.NonPublic);
                host=feature?.GetValue(owner) as TrappedUnitRescueFeature;
                if(host==null || !host.Ready)throw new InvalidOperationException("Actual BQoL rescue feature did not initialize.");
                settings=plugin.Settings;runtime=host.Runtime;generalObserver=host.GeneralObserver;workers=host.Recovery;
                var compatibility=typeof(TrappedUnitRescueFeature).GetField("fixes",BindingFlags.Instance|BindingFlags.NonPublic);
                fixes=compatibility?.GetValue(host) as FixesIntegration;
                if(settings==null || runtime==null || generalObserver==null || workers==null || fixes==null)
                    throw new InvalidOperationException("Selected production host contract is incomplete.");
                ArmAcceptanceDrain();libraryReady=true;
                Stamp("PRODUCTION_HOST_READY assembly="+typeof(TrappedUnitRescueFeature).Assembly.Location+" mvid="+typeof(TrappedUnitRescueFeature).Module.ModuleVersionId+
                    " owner="+RescueOwnerGuid+" secondRuntime=false secondEarlyProvider=false");
                if(settingsReady)QueueRequestedFixture();
            }
            catch(Exception error)
            {
                startupFailure="Production acceptance binding: "+error;
                Stamp("INIT_FAILED "+startupFailure);
                if(settingsReady)QueueRequestedFixture();
            }
        }
        private static void InitializeSettings()
        {
            try
            {
                if(!ApiShared.ForMod(Guid).TryGetMissionLifecycle(out missions,out var diagnostic))throw new InvalidOperationException(diagnostic.Reason);
                if(!missions.TryRegisterObserver("acceptance-session",n=>{
                    series?.MissionStarted(n.Context.SessionId);
                    if(host!=null && libraryReady)Stamp("production-session-observed session="+n.Context.SessionId+" isSave="+n.Context.IsSave+
                        " activeBeforeAcceptance="+host.Active+" hostMaster="+settings.EnableMod+" hostOption="+settings.EnableTrappedUnitRescue+" clientMaster="+settings.EnableClientFeatures);
                    // Loaded production proofs are already staged by the main host. Never
                    // reset that bank while the owned save fixture is rebinding metadata.
                    if(Volatile.Read(ref fixtureActive)!=0 && !n.Context.IsSave)SetGeneralFixtureSession(false);
                },n=>{
                    RestoreAcceptanceDrain();
                    Volatile.Write(ref generalFixture,0);
                    if(series!=null)series.MissionEnded(n.Context.SessionId);else fixture?.Stop();
                },null,out var registered))throw new InvalidOperationException(registered.Reason);
                settingsReady=true;if(libraryReady || startupFailure!=null)QueueRequestedFixture();
            }
            catch(Exception error)
            {
                startupFailure="Acceptance lifecycle: "+error;settingsReady=true;
                Stamp("INIT_FAILED "+startupFailure);QueueRequestedFixture();
            }
        }

        private static void QueueRequestedFixture()
        {
            if (!ApiShared.ForMod(Guid).Ui.TryPost(StartRequestedFixture)) Stamp("INIT_FAILED Unity dispatcher unavailable");
        }
        private static void StartRequestedFixture()
        {
            if (fixture != null || series != null || startupFailureRun) return;
            string request = Path.Combine(Paths.ConfigPath, "SerpAutomation", Guid + ".run");
            if (!File.Exists(request)) return;
            string[] lines = File.ReadAllLines(request);
            if (lines.Length < 2 || !Fixture.Owns(lines[1]) && lines[1]!=RescueScenarioSelection.SeriesRequest) return;
            if (!System.Guid.TryParseExact(lines[0], "N", out var runId) || runId == System.Guid.Empty)
            { Stamp("Invalid owned request; not consumed"); return; }
            string[] selection;
            if(lines[1]==RescueScenarioSelection.SeriesRequest)
            {
                if(!RescueScenarioSelection.TryParse(configuredScenarios,Fixture.Owns,out selection))
                {Stamp("Invalid owned series selection; not consumed");return;}
            }
            else selection=new[]{lines[1]};
            if(!RescueScenarioSelection.TryExpand(selection,out var expandedSelection))
            {Stamp("Expanded series exceeds bounded capacity; request not consumed");return;}
            selection=expandedSelection;
            if(startupFailure!=null)
            {
                // A valid owned request must finish FAIL instead of waiting for the outer timeout.
                string failure=startupFailure;startupFailureRun=true;File.Delete(request);
                ApiShared.ForMod(Guid).Automation.Start(runId,new[]{new APIShared.Automation.AutomationStep(
                    "production-host-initialization",TimeSpan.FromSeconds(10),_=>throw new InvalidOperationException(failure))},log,
                    result=>Stamp("finished status="+result.Status+" detail="+result.Detail),quitWhenDone:true);
                return;
            }
            APIShared.Automation.AutomationStep[] steps;
            if(lines[1]==RescueScenarioSelection.SeriesRequest || selection.Length>1)
            {
                var cases=new RescueSeriesCase[selection.Length];
                for(int i=0;i<cases.Length;i++)
                {
                    var item=CreateFixture(selection[i]);
                    cases[i]=new RescueSeriesCase(selection[i],item.Steps(),()=>fixture=item,item.Stop);
                }
                series=new RescueScenarioSeries(cases,Stamp);steps=series.Steps().ToArray();
            }
            else {fixture=CreateFixture(selection[0]);steps=fixture.Steps().ToArray();}
            File.Delete(request);
            ApiShared.ForMod(Guid).Automation.Start(runId, steps, log,
                result => {
                    RestoreAcceptanceDrain(); generalObserver?.SetSession(0); Volatile.Write(ref generalFixture, 0);
                    SetActive(false); Interlocked.Exchange(ref fixtureActive, 0); series?.Stop();fixture?.Stop();
                    Stamp("finished status=" + result.Status + " detail=" + result.Detail);
                }, quitWhenDone: true);
        }

        private static Fixture CreateFixture(string scenario)=>new Fixture(configuredMap, scenario, Stamp, runtime, enabled => {
                if (enabled && (fixes == null || !fixes.Ready))
                    throw new InvalidOperationException("Experimental rescue is unavailable; the validated Fixes integration did not become ready. Original behavior remains active.");
                bool masterOff=scenario=="rescue-legacy-civilian-master-off" || scenario=="rescue-aivbridge-hostmaster-off";
                bool defaultProfile=scenario=="rescue-aivbridge-default-on";
                settings.EnableMod=!masterOff;
                settings.EnableClientFeatures=scenario!="rescue-aivbridge-clientoff-on";
                if(!defaultProfile)settings.EnableTrappedUnitRescue=masterOff || IsMountedScannerCase(scenario) || enabled;
                if(settings.EnableMod!=!masterOff || settings.EnableTrappedUnitRescue!=(masterOff || IsMountedScannerCase(scenario) || enabled))
                    throw new InvalidOperationException("Requested test settings were not accepted before mission start.");
                Interlocked.Exchange(ref fixtureActive, 1);
                generalObserver?.SetSession(0);
                SetActive(enabled);
            }, SetSuppression, () => {
                if (fixes == null || !fixes.Ready)
                    throw new InvalidOperationException("Moat-worker baseline requires a validated Fixes installation state.");
                return fixes.WorkerDeletionInstalled;
            }, configuredPeaceTime);

        internal static void AssertProductionProfile(string scenario,bool expected)
        {
            bool masterOff=scenario=="rescue-aivbridge-hostmaster-off";
            if(settings.EnableMod!=!masterOff || settings.EnableTrappedUnitRescue!=(masterOff || expected) ||
                settings.EnableClientFeatures!=(scenario!="rescue-aivbridge-clientoff-on") || host.Active!=expected)
                throw new InvalidOperationException("Actual production host/default/client activation differs from the selected profile.");
            int gate=ProductionScannerGate(expected);
            Stamp("production-profile scenario="+scenario+" hostMaster="+settings.EnableMod+" hostOption="+settings.EnableTrappedUnitRescue+
                " clientMaster="+settings.EnableClientFeatures+" active="+host.Active+" providerNativeGate="+gate+
                " optionExplicitlyWritten="+(scenario!="rescue-aivbridge-default-on")+" settingsChangedBeforeMissionOnly=true");
        }
        internal static int ProductionScannerGate(bool expected)
        {
            var type=typeof(MountedScannerEarlyProvider);var flags=BindingFlags.Static|BindingFlags.NonPublic;
            var requested=type.GetField("requested",flags);
            if(requested?.FieldType!=typeof(int) || (int)requested.GetValue(null)!=(expected?1:0))throw new InvalidOperationException("Production scanner logical setting gate differs.");
            var value=type.GetField("capture",flags)?.GetValue(null);if(value==null)return -1;
            var field=value.GetType().GetField("activation",BindingFlags.Instance|BindingFlags.NonPublic);
            if(field?.FieldType!=typeof(IntPtr))throw new InvalidOperationException("Production scanner activation contract changed.");
            var bytes=Shared.CommittedMemory.ReadUpTo((IntPtr)field.GetValue(value),4);
            if(bytes.Length!=4)throw new InvalidOperationException("Production scanner activation is unreadable.");
            int gate=BitConverter.ToInt32(bytes,0);
            if(!expected && gate!=0 || gate<0 || gate>1)throw new InvalidOperationException("Disabled production scanner has an active native gate.");
            return gate;
        }
        private static bool IsMountedScannerCase(string scenario)=>scenario.StartsWith("rescue-mountedkeep",StringComparison.Ordinal);
        private static void SetActive(bool enabled)
        {fixes?.SetEnabled(false);Volatile.Write(ref active,enabled?1:0);}
        private static void SetSuppression(bool enabled)
        {
            if(!enabled){fixes?.SetEnabled(false);return;}
            if(host==null || !host.Ready || fixes==null || !fixes.Ready || missions?.Current==null || Volatile.Read(ref fixtureActive)==0)
                throw new InvalidOperationException("Acceptance suppression requires its actual production host and owned mission.");
            fixes.SetEnabled(true);
        }
        internal static void SetGeneralFixtureSession(bool enabled)
        {
            if(host==null || !host.Ready || Volatile.Read(ref fixtureActive)==0 || missions?.Current==null)
                throw new InvalidOperationException("Acceptance observer control requires its owned production mission.");
            if(enabled && (!settings.EnableMod || !settings.EnableTrappedUnitRescue))
                throw new InvalidOperationException("Acceptance cannot enable a disabled production setting mid-mission.");
            fixes.SetEnabled(false);Volatile.Write(ref generalFixture,enabled?1:0);
            generalObserver.SetSession(enabled?missions.Current.SessionId:0);
        }
        private static void Stamp(string message)=>log.LogInfo("["+DateTime.Now.ToString("HH:mm:ss.fff")+"] RESCUE_TEST "+message);
    }
}
