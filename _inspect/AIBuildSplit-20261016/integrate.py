exec((__import__('pathlib').Path(__file__).parent/'split.py').read_text(encoding='utf-8').split('# Read-only evidence')[0])
# This script intentionally starts from the already split sources.
p=base/'src/AIBuildDiagnoseRuntime.cs'
s=read(p).replace('            nearbyCopySession = false;\n','')
write(p,s)

plugin='''using APIShared;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;
namespace AIBuildDiagnoseTest
{
    [BepInDependency("000shcdese", "2.11.0")]
    [BepInDependency("APIShared_Serp", "0.4.6")]
    [BepInDependency("BugfixesAndQoL_Serp", "1.0.181")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, Name, Version)]
    public sealed class AIBuildDiagnosePlugin : BaseUnityPlugin
    {
        public const string Guid = "AIBuildDiagnoseTest_Serp";
        public const string Name = "AI Build Diagnose Test";
        public const string Version = "0.1.1";
        private static ManualLogSource log;
        private static AIBuildDiagnoseRuntime runtime;
        private static IDisposable sessionSubscription;
        private static bool registered;
        private void Awake()
        {
            log = Logger;
            Shared.DebugLogHelper.LogInfo(log, Name + " " + Version + " loaded; general observation only; editor/replay excluded.");
            if (registered) return;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            registered = true;
        }
        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            try
            {
                runtime = new AIBuildDiagnoseRuntime(log, Chainloader.PluginInfos.ContainsKey("fixes"),
                    unchecked((ulong)context.ModuleHandle.ToInt64()));
                sessionSubscription = Shared.GameplaySessionLifecycle.SubscribeStarted(log, runtime.OnSessionStarted, runtime.OnSessionEnded);
                GameTimeManagerAPI.Instance.OnTick += OnTick;
                string hash;
                try { hash = NativeDiagnosticContracts.ComputeInstalledHash(); }
                catch (Exception ex) { hash = string.Empty; Shared.DebugLogHelper.LogError(log, "Could not hash diagnostic native module: " + ex); }
                AiBuildDiagnostic.Initialize(context.ModuleHandle.ToInt64(), hash, context.Region, log);
                if (!AiBuildDiagnostic.TryRegister(Guid, DeliverRecord, out string error))
                    Shared.DebugLogHelper.LogWarning(log, "AI_BUILD_NATIVE_OBSERVATION_INCOMPLETE: " + error);
                if (AiBuildDiagnostic.HasObserver && !BugfixesAndQoL.Diagnostics.AiBuildObservation.TryRegister(
                    AiBuildObservationSink.Instance, log, out string sinkError))
                    Shared.DebugLogHelper.LogWarning(log, "AI_BUILD_OBSERVATION_BRIDGE_UNAVAILABLE: " + sinkError);
                runtime.TryInstallGeneralSiteSearchHooks(context);
                Shared.DebugLogHelper.LogInfo(log, "AI_BUILD_DIAGNOSTIC_READY: publisher=GameTimeManagerAPI.OnTick; " +
                    "observer=" + AiBuildDiagnostic.HasObserver + ", schedulerHook=" + AiBuildDiagnostic.SchedulerReady +
                    ", routeHook=" + AiBuildDiagnostic.RouteReady + "; observationDoesNotGuaranteeCompleteCauseDetection=true.");
            }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI build diagnostic initialization failed: " + ex); }
        }
        private static void DeliverRecord(AiBuildDiagnosticRecord record)
        {
            // A general capture failure must not prevent an independently registered probe.
            try { runtime?.OnNativeRecord(record); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI_BUILD_CALLBACK_FAILED: " + ex); }
            AiBuildExperimentBridge.Deliver(record, log);
        }
        private static void OnTick(int tick) => runtime?.OnTick(tick);
    }
}
'''
write(base/'src/AIBuildDiagnosePlugin.cs',plugin)
write(base/'src/Diagnostics/AiBuildExperimentBridge.cs','''using System;
using System.Threading;
using BepInEx.Logging;
namespace AIBuildDiagnoseTest
{
    // Assembly-private integration with the optional companion, not an APIShared service.
    // Register after the general observer is ready. Callbacks run synchronously on the native
    // caller thread and remain rooted for process lifetime; ordinary sessions must stay inert.
    internal static class AiBuildExperimentBridge
    {
        private static readonly object sync = new object();
        private static Action<AiBuildDiagnosticRecord> observer;
        internal static bool TryRegister(string ownerGuid, Action<AiBuildDiagnosticRecord> callback, out string error)
        {
            error = null;
            if (ownerGuid != "CanarariWoodTest_Serp" || callback == null)
            { error = "Invalid companion observer owner or callback."; return false; }
            lock (sync)
            {
                if (observer != null)
                {
                    if (ReferenceEquals(observer, callback)) return true;
                    error = "Another companion observer is already registered."; return false;
                }
                if (!AiBuildDiagnostic.HasObserver)
                { error = "General observer is unavailable."; return false; }
                Volatile.Write(ref observer, callback);
                return true;
            }
        }
        internal static void Deliver(AiBuildDiagnosticRecord record, ManualLogSource log)
        {
            Action<AiBuildDiagnosticRecord> callback = Volatile.Read(ref observer);
            if (callback == null) return;
            try { callback(record); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI_BUILD_COMPANION_CALLBACK_FAILED: " + ex); }
        }
    }
}
''')
p=base/'src/Diagnostics/AiBuildDiagnostic.cs'
s=read(p)
# Only the companion may provide mutating gates. Observation ownership remains unchanged.
s=s.replace('ownerGuid != "AIBuildDiagnoseTest_Serp" || gate == null','ownerGuid != "CanarariWoodTest_Serp" || gate == null')
s=s.replace('ownerGuid != "AIBuildDiagnoseTest_Serp" || begin == null','ownerGuid != "CanarariWoodTest_Serp" || begin == null')
s=s.replace('copy-only wood-nearby data overlay owned by AIBuildDiagnoseTest','copy-only wood-nearby data overlay owned by CanarariWoodTest')
write(p,s)
p=base/'Properties/AssemblyInfo.cs'
write(p,read(p)+'\n[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("CanarariWoodTest")]\n')
p=base/'AIBuildDiagnoseTest.csproj'
s=read(p)
for name in ['FarmSiteContractProbe','CanariFarmSwapProbe','FixesFarmFilterOverride']:
    s=s.replace(f'    <Compile Include="src\\{name}.cs" />\n','')
write(p,s)

write(special/'src/CanarariWoodPlugin.cs','''using AIBuildDiagnoseTest;
using BepInEx;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI.Buildings;
using System;
namespace CanarariWoodTest
{
    [BepInDependency("000shcdese", "2.11.0")]
    [BepInDependency("APIShared_Serp", "0.4.6")]
    [BepInDependency("BugfixesAndQoL_Serp", "1.0.181")]
    [BepInDependency("AIBuildDiagnoseTest_Serp", "0.1.1")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, "CanarariWoodTest", "0.1.0")]
    public sealed class CanarariWoodPlugin : BaseUnityPlugin
    {
        public const string Guid = "CanarariWoodTest_Serp";
        private static ManualLogSource log;
        private static bool registered, placement, nearby, farms, swap;
        private static FixesFarmFilterOverride comparison;
        private static CanarariWoodRuntime runtime;
        private static IDisposable sessionSubscription, spawnSubscription, buildSubscription;
        private void Awake()
        {
            log = Logger;
            placement = Config.Bind("PlacementProbe", "Enabled", false, "One-time placement probe on test_canari_nowoodcutters_probe.sav only. Never save the result.").Value;
            nearby = Config.Bind("NearbyWoodTest", "Enabled", false, "Calibrated temporary candidate overlay on the named Canari save copy, at most twelve calls.").Value;
            farms = Config.Bind("FarmContractProbe", "Enabled", false, "Place four farms and test sites on rat_farm_site_contract_probe.sav only. Never save the result.").Value;
            swap = Config.Bind("CanariFarmSwap", "Enabled", false, "Replace the orchard on three byte-verified test_canari_farm_swap_*.sav copies only. Never save the result.").Value;
            bool suppress = Config.Bind("FixesFarmFilterOverride", "Enabled", false,
                "Before native load, suppress the external Fixes farmland filter for a Vanilla comparison. Process-wide until game exit; restart to change. The configuration file is preserved.").Value;
            if (registered) return;
            try { comparison = FixesFarmFilterOverride.Begin(log, suppress); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI_BUILD_FIXES_FILTER: startup check failed; probes disabled: " + ex); }
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            registered = true;
            Shared.DebugLogHelper.LogInfo(log, "CanarariWoodTest loaded; placement=" + placement + ", nearby=" + nearby +
                ", farms=" + farms + ", swap=" + swap + ", suppressFixesFilter=" + suppress + "; startup-rooted=true.");
        }
        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            bool ready = false;
            try { ready = comparison != null && comparison.Complete(context.ModuleHandle); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI_BUILD_FIXES_FILTER: compatibility check failed: " + ex); }
            if (!ready && (placement || nearby || farms || swap))
                Shared.DebugLogHelper.LogWarning(log, "AI_BUILD_SPECIAL_PROBES_DISABLED: Vanilla comparison/native contract unavailable; no active experiment armed.");
            try
            {
                runtime = new CanarariWoodRuntime(log, unchecked((ulong)context.ModuleHandle.ToInt64()),
                    ready && placement, ready && nearby, ready && farms, ready && swap);
                if (!AiBuildExperimentBridge.TryRegister(Guid, runtime.OnNativeRecord, out string error))
                { Shared.DebugLogHelper.LogError(log, "AI_BUILD_SPECIAL_PROBES_UNAVAILABLE: " + error); runtime = null; return; }
                if (!AiBuildDiagnostic.TryRegisterWoodBuildGate(Guid, runtime.ShouldDeferWoodBuild, out error) ||
                    !AiBuildDiagnostic.TryRegisterNearbyWoodOverlay(Guid, runtime.BeginNearbyWoodOverlay, out error))
                { Shared.DebugLogHelper.LogError(log, "AI_BUILD_SPECIAL_PROBES_UNAVAILABLE: " + error); return; }
                sessionSubscription = Shared.GameplaySessionLifecycle.SubscribeStarted(log, runtime.OnSessionStarted, runtime.OnSessionEnded);
                spawnSubscription = BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(runtime.OnBuildingSpawn);
                buildSubscription = BuildingR3EventHooks.OnBuildStructure.Observable.Subscribe(runtime.OnBuildStructure);
                GameTimeManagerAPI.Instance.OnTick += OnTick;
                Shared.DebugLogHelper.LogInfo(log, "AI_BUILD_SPECIAL_READY: publisher=GameTimeManagerAPI.OnTick; nativeHooksOwnedBy=AIBuildDiagnoseTest.");
            }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI_BUILD_SPECIAL_INITIALIZATION_FAILED: " + ex); }
        }
        private static bool firstTick;
        private static void OnTick(int tick)
        {
            if (!firstTick) { firstTick = true; Shared.DebugLogHelper.LogInfo(log, "AI_BUILD_SPECIAL_POST_STARTUP_TICK: " + tick); }
            try { runtime?.OnTick(tick); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI_BUILD_SPECIAL_TICK_FAILED: " + ex); }
        }
    }
}
''')
p=special/'src/FixesFarmFilterOverride.cs'
s=read(p).replace('Begin(ManualLogSource logger)','Begin(ManualLogSource logger, bool suppress)')
needle='''            bool original = setting.Value;
'''
s=s.replace(needle,needle+'''            if (!suppress && original)
                return new FixesFarmFilterOverride(logger, source, setting, true,
                    source.SaveOnConfigSet, null, false, "suppression-not-requested");
''')
s=s.replace('log.LogError(','Shared.DebugLogHelper.LogError(log, ').replace('log.LogInfo(','Shared.DebugLogHelper.LogInfo(log, ')
write(p,s)

# Build/package templates preserve the established consumer workflow and CRLF.
s=read(base/'AIBuildDiagnoseTest.csproj').replace('AIBuildDiagnoseTest','CanarariWoodTest')
s=s.replace('{B8BFF288-06CA-4225-AF4A-1B9F8F789E42}','{DFDD2077-66E6-4256-BC9F-E570B0BA6D85}')
start=s.index('    <Compile Include="src\\CanarariWoodTestPlugin.cs"') if 'src\\CanarariWoodTestPlugin.cs' in s else -1
# Replace all own-source entries, not the linked shared adapters.
s=re.sub(r'    <Compile Include="src\\[^\n]+\n','',s)
s=s.replace('    <Compile Include="Properties\\AssemblyInfo.cs" />','    <Compile Include="Properties\\AssemblyInfo.cs" />\n    <Compile Include="src\\*.cs" />')
s=s.replace('    <Reference Include="System" />','    <Reference Include="AIBuildDiagnoseTest"><HintPath>..\\AIBuildDiagnoseTest\\BepInEx\\plugins\\AIBuildDiagnoseTest_Serp\\AIBuildDiagnoseTest.dll</HintPath><Private>false</Private></Reference>\n    <Reference Include="System" />')
write(special/'CanarariWoodTest.csproj',s)
write(special/'Properties/AssemblyInfo.cs','''using System.Reflection;
[assembly: AssemblyTitle("CanarariWoodTest")]
[assembly: AssemblyProduct("CanarariWoodTest")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: AssemblyInformationalVersion("0.1.0")]
''')
import json
info=json.loads(read(base/'info.json'))
info['Description']='General read-only observations of AI construction, AIV steps, site searches, routes and spawns.'
write(base/'info.json',json.dumps(info,indent=2)+'\n')
info['GUID']='CanarariWoodTest_Serp';info['Name']='CanarariWoodTest';info['Version']='0.1.0'
info['Description']='Explicit disposable-save experiments for Canari wood construction and farm-site contracts.'
info['Dependencies'].append({'GUID':'AIBuildDiagnoseTest_Serp','MinimumVersion':'0.1.1'})
write(special/'info.json',json.dumps(info,indent=2)+'\n')
s=read(base/'build.bat').replace('AIBuildDiagnoseTest','CanarariWoodTest').replace('AI Build Diagnose Test','CanarariWoodTest')
start=s.index('echo [%date% %time%] Run tests')
end=s.index('echo [%date% %time%] PowerShell checks / build step',start)
s=s[:start]+s[end:]
write(special/'build.bat',s)
