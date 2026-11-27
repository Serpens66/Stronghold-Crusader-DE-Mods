using BugfixesAndQoL;
using BugfixesAndQoL.TrappedUnitRescue;
#if SERP_DEVDIAG
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using APIShared;
using APIShared.Automation;
using APIShared.GameModes;
using APIShared.WorldMutations;
using CrusaderDE;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private static NoJestersWorld noJestersWorld;
        private static bool noJestersRegistered;
        private static BepInEx.Logging.ManualLogSource noJestersErrors;
        private static void NoJestersError(Exception error)
        {
            if(noJestersErrors==null)noJestersErrors=BepInEx.Logging.Logger.CreateLogSource("RescueTest.NativeState");
            noJestersErrors.LogError("["+DateTime.Now.ToString("HH:mm:ss.fff")+"] NOJESTERS_TEST_WORLD_REJECTED "+error);
        }
        private bool IsNoJestersProfile()=>scenario=="rescue-diag-nojesters4000-on" || scenario=="rescue-perf-4000-nojesters-passive-on" || scenario=="rescue-perf-9871-nojesters-passive-on";
        private void ArmNoJestersWorld(AutomationRun run)
        {
            if(!IsNoJestersProfile())return;
            Require(noJestersWorld==null,"Another native-state test world is active");
            if(!noJestersRegistered)
            {
                Require(ApiShared.ForMod(Plugin.Guid).TryGetMissionLifecycle(out var lifecycle,out var available),
                    "Native-state lifecycle unavailable: "+available.Reason);
                Require(lifecycle.TryRegisterObserver("acceptance-performance-nojesters",e=>noJestersWorld?.Started(e),
                    e=>noJestersWorld?.Ended(e),e=>noJestersWorld?.Initialization(e),out var diagnostic),
                    "Native-state lifecycle observer rejected: "+diagnostic.Reason);
                noJestersRegistered=true;
            }
            // Before StartSkirmish; PerformanceProof does not yet exist.
            try{noJestersWorld=new NoJestersWorld(this,PersistenceSavePath(run));}
            catch(Exception error){NoJestersError(error);throw;}
        }
        private void RequireNoJestersFresh()
        {if(IsNoJestersProfile()){Require(noJestersWorld!=null,"Missing early native-state scope");noJestersWorld.RequireFresh(this);}}
        private void BindNoJestersPerformance(PerformanceProof proof)
        {if(IsNoJestersProfile()){RequireNoJestersFresh();noJestersWorld.Bind(proof);}}
        private void ExpectNoJestersLoad(PerformanceProof proof)
        {if(IsNoJestersProfile()){Require(noJestersWorld!=null,"Missing owned native-state load scope");noJestersWorld.ExpectLoad(proof);}}
        private void CheckNoJestersBoundary(PerformanceProof proof)
        {
            if(!IsNoJestersProfile())return;
            try{Require(noJestersWorld!=null,"Missing native-state boundary owner");noJestersWorld.Boundary(proof);}
            catch(Exception error){NoJestersError(error);throw;}
        }
        private void StopNoJestersWorld(bool completing=false)
        {
            var current=noJestersWorld;
            if(current==null || !ReferenceEquals(current.Fixture,this))return;
            // Fixture.Stop is also called while the existing performance owner
            // intentionally unloads for its exact next owned save.
            if(!completing && current.ExpectingLoad)return;
            try{current.Release();}
            catch(Exception error){NoJestersError(error);if(completing)throw;}
            finally{if(ReferenceEquals(noJestersWorld,current))noJestersWorld=null;}
        }
        private sealed class NoJestersWorld
        {
            internal readonly Fixture Fixture;
            private readonly string mapPath,savePath,runId;
            private readonly int launchThread;
            private readonly FieldInfo monitorField;
            private readonly object monitor;
            private readonly NativeJesterWord native;
            private readonly NoJestersState state;
            private readonly NativeWorldTokenReader tokens=new NativeWorldTokenReader();
            private readonly NoJestersReadyHandoff ready=new NoJestersReadyHandoff();
            private bool nativeWasSave;
            private bool fresh,expecting,retired;
            private long session,world;
            private string fault;
            private int boundaries,loads;
            internal bool ExpectingLoad=>expecting;
            internal NoJestersWorld(Fixture fixture,string ownedSave)
            {
                Fixture=fixture;mapPath=Path.GetFullPath(fixture.map);savePath=Path.GetFullPath(ownedSave);
                Require(Path.IsPathRooted(fixture.map) && File.Exists(mapPath),"Native-state world needs its exact full map path");
                string name=Path.GetFileNameWithoutExtension(savePath);const string prefix="SerpAcceptance-";
                Guid id=Guid.Empty;
                Require(name.StartsWith(prefix,StringComparison.Ordinal) && Guid.TryParseExact(name.Substring(prefix.Length),"N",out id) && id!=Guid.Empty,
                    "Native-state owner lacks the actual nonempty AutomationRun identity");runId=id.ToString("N");
                launchThread=Thread.CurrentThread.ManagedThreadId;
                monitorField=typeof(EngineInterface).GetField("threadLock",BindingFlags.NonPublic|BindingFlags.Static);
                Require(monitorField!=null && monitorField.FieldType==typeof(object),"Native-state engine monitor contract changed");
                monitor=monitorField.GetValue(null);Require(monitor!=null,"Native-state engine monitor missing");
                native=new NativeJesterWord(fixture.runtime.Native.ImageBase);state=new NoJestersState(native);
            }
            private bool ExactPath(string value,string expected)=>!string.IsNullOrEmpty(value) && Path.IsPathRooted(value) &&
                string.Equals(Path.GetFullPath(value),expected,StringComparison.OrdinalIgnoreCase);
            private void MonitorCurrent(){Require(ReferenceEquals(monitorField.GetValue(null),monitor),"Native-state engine monitor was replaced");}
            private void SameWorld()
            {
                Require(ready.IsBound,"Native-state ManagedReady handoff has not been bound");
                ready.RequireToken(session,tokens.Read(session));
                Require(!retired && WorldMutationEvents.TryGetCurrentWorld(out long sid,out long generation) &&
                    sid==session && generation==world,"Native-state fixture no longer owns this native world");
                native.RequireSkirmish();
            }
            internal void Initialization(MissionLifecycleNotification notification)
            {
                if(notification!=null && notification.IsBeforeInitialization && ready.HasPendingCapture)
                {
                    // A further initialization after our captured NativeLoaded
                    // is never merely a registry-generation refresh.
                    ready.Retire();fault="Further native initialization invalidated the captured world";
                }
                if(notification==null || notification.IsReplay || notification.Kind!=MissionLifecycleKind.Initialization ||
                    notification.Phase!=MissionInitializationPhase.NativeLoaded)return;
                try
                {
                    Require(fault==null,"Native-state scope already failed: "+fault);
                    var context=notification.Context;
                    Require(context!=null && context.SessionId>0 && !context.IsEditor && !context.IsRestart &&
                        context.Mode.Kind==GameModeKind.CustomGame && !context.Mode.IsRealMultiplayer &&
                        !context.Mode.HasConflictingCustomizedOrigin,"Native-state setup requires the exact ordinary singleplayer Skirmish context");
                    Require(Thread.CurrentThread.ManagedThreadId==launchThread,"NativeLoaded left the audited synchronous launch thread");
                    bool loading=context.IsSave;
                    Require(loading ? fresh && expecting && ExactPath(context.FilePath,savePath) :
                        !fresh && context.StartKind==MissionStartKind.NewGame && ExactPath(context.FilePath,mapPath),
                        "Foreign/repeated native load is outside the exact map/run/save scope");
                    MonitorCurrent();
                    lock(monitor)
                    {
                        native.RequireSkirmish();
                        Require(WorldMutationEvents.TryGetCurrentWorld(out long sid,out long generation) && sid==context.SessionId && generation>0,
                            "NativeLoaded lacks its newly identified native world");
                        Require(!loading || sid!=session && generation!=world,"Owned save reused its old mission/world identity");
                        ready.Capture(sid,generation,tokens.Read(sid));
                        if(loading)state.AdoptLoaded(sid,generation);else state.ApplyFresh(sid,generation);
                        session=sid;world=generation;retired=false;nativeWasSave=loading;
                        if(loading){expecting=false;loads++;}else fresh=true;
                        Log(loading?"load":"fresh",loading?0:1);
                    }
                }
                catch(Exception error){fault=error.ToString();NoJestersError(error);Fixture.log("performance-nojesters-fault run="+runId+" profile="+Fixture.scenario+" error="+error);}
            }
            internal void Started(MissionLifecycleNotification notification)
            {
                if(notification==null || notification.IsReplay)return;
                try
                {
                    var context=notification.Context;
                    Require(fault==null && !retired && notification.Kind==MissionLifecycleKind.Start &&
                        notification.Phase==MissionInitializationPhase.ManagedReady && notification.HasStarted &&
                        context!=null && context.SessionId==session && context.IsSave==nativeWasSave &&
                        context.StartKind==(nativeWasSave?MissionStartKind.LoadedSave:MissionStartKind.NewGame) &&
                        !context.IsEditor && !context.IsRestart && ExactPath(context.FilePath,nativeWasSave?savePath:mapPath) &&
                        context.Mode.Kind==GameModeKind.CustomGame && !context.Mode.IsRealMultiplayer &&
                        !context.Mode.HasConflictingCustomizedOrigin && Thread.CurrentThread.ManagedThreadId==launchThread,
                        "ManagedReady is not the exact captured native-load continuation");
                    MonitorCurrent();lock(monitor)
                    {native.RequireSkirmish();ready.Started(session,tokens.Read(session));}
                    // Do not read/bind the public registry generation here:
                    // its independent OnStart invalidation may run after us.
                }
                catch(Exception error){fault=error.ToString();NoJestersError(error);}
            }
            private void BindReadyAtPausedBoundary()
            {
                Require(Monitor.IsEntered(monitor) && fault==null && !retired,"Ready binding lacks its held live world");
                if(ready.IsBound)return;
                Require(WorldMutationEvents.TryGetCurrentWorld(out long sid,out long generation) && sid==session && generation>0,
                    "ManagedReady world is unavailable at its final paused boundary");
                long early=ready.EarlyWorld;
                ready.Bind(sid,generation,tokens.Read(sid),state);world=generation;
                Fixture.log("performance-nojesters-ready schema=1 run="+runId+" profile="+Fixture.scenario+
                    " session="+session+" earlyWorld="+early+" world="+world+" flag="+native.ReadJester()+" lady="+native.ReadLady()+
                    " allEightPairsEmpty=true wordWrites=0 tokenSame=true loadCount="+loads+
                    " native-state-fixture=true newSharedBaseline=true generalPerformanceClaim=false");
            }
            internal void Ended(MissionLifecycleNotification notification)
            {
                if(notification?.Context==null || notification.Context.SessionId!=session)return;
                // The publisher invalidates its native-world token BEFORE End.
                // No restoration from this callback can prove the old world.
                state.Retire();ready.Retire();retired=true;
            }
            internal void RequireFresh(Fixture fixture)
            {Require(ReferenceEquals(Fixture,fixture) && fresh && fault==null,"Early native-state setup failed or was not observed: "+fault);}
            internal void Bind(PerformanceProof proof)
            {
                RequireFresh(proof.Fixture);
                Require(proof.RunId==runId && ExactPath(proof.Path,savePath) && proof.Population==0 && !proof.ObserveArmyMoves,
                    "Native-state performance proof does not match its early run/save owner");
                MonitorCurrent();lock(monitor)
                {
                    Require(proof.PreviousSession==session && Fixture.session==session,"Native-state proof belongs to another prepared session");
                    BindReadyAtPausedBoundary();
                    SameWorld();state.Verify(session,world);
                }
                Fixture.log("performance-nojesters-baseline schema=1 run="+runId+" profile="+Fixture.scenario+
                    " native-state-fixture=true newSharedBaseline=true generalPerformanceClaim=false defaultProfilesUnchanged=true");
            }
            internal void ExpectLoad(PerformanceProof proof)
            {
                RequireFresh(proof.Fixture);
                Require(proof.RunId==runId && ExactPath(proof.Path,savePath) && File.Exists(savePath) && !expecting,
                    "Native-state load is not the exact saved run-owned artifact");
                MonitorCurrent();lock(monitor)
                {
                    Require(proof.PreviousSession==session,"Outgoing native-state world differs from the performance owner");
                    SameWorld();state.Verify(session,world);
                    Require(++boundaries<=64,"Unexpected native-state boundary volume");Log("boundary",0);
                }
                expecting=true;
            }
            internal void Boundary(PerformanceProof proof)
            {
                RequireFresh(proof.Fixture);MonitorCurrent();Require(Monitor.IsEntered(monitor),"Native-state boundary requires the existing paused engine monitor");
                Require(proof.RunId==runId && ExactPath(proof.Path,savePath) && !expecting,"Native-state boundary crossed an unsettled load");
                BindReadyAtPausedBoundary();
                SameWorld();state.Verify(session,world);Require(++boundaries<=64,"Unexpected native-state boundary volume");Log("boundary",0);
            }
            private void Log(string phase,int writes)
            {
                Fixture.log("performance-nojesters-world schema=1 run="+runId+" profile="+Fixture.scenario+" phase="+phase+
                    " session="+session+" world="+world+" flag="+native.ReadJester()+" lady="+native.ReadLady()+
                    " allEightPairsEmpty=true wordWrites="+writes+" loadCount="+loads+" boundary="+boundaries+
                    " native-state-fixture=true newSharedBaseline=true generalPerformanceClaim=false");
            }
            internal void Release()
            {
                bool restored=false;
                try
                {
                    MonitorCurrent();lock(monitor)
                    {
                        if(!retired && ready.IsBound && WorldMutationEvents.TryGetCurrentWorld(out long sid,out long generation) && sid==session && generation==world)
                        {ready.RequireToken(sid,tokens.Read(sid));native.RequireSkirmish();state.Verify(sid,generation);restored=state.Restore(sid,generation);}
                    }
                }
                finally
                {
                    retired=true;expecting=false;state.Retire();ready.Retire();
                    Fixture.log("performance-nojesters-cleanup run="+runId+" profile="+Fixture.scenario+
                        " originalWordRestored="+restored+" sameWorldRequired=true foreignOrLoadedWorldNeverRepaired=true native-state-fixture=true");
                }
            }
        }
        // Test-only SDK-private identity adapter. Public registry generations
        // deliberately change at OnStart; this immutable underlying object does
        // not. Validate getter IL/readonly shape AND deliberately pin the exact
        // audited SDK snapshot whose private producers were inspected. A new
        // SDK rebuild needs this test-only audit again; never infer a +2 delta.
        private sealed class NativeWorldTokenReader
        {
            private readonly MethodInfo getter;
            private readonly Type worldType;
            private readonly FieldInfo mission,sequence;
            internal NativeWorldTokenReader()
            {
                var assembly=typeof(ApiShared).Assembly;
                // Verified merged SDK 0.78 full build, 27 Nov 2026: NativeWorld 17 / NativeClock 22 IL;
                // readonly World/Mission/Sequence and lifecycle match the 0.74 / 0.77 snapshots.
                // Private producer semantics are not a public API guarantee.
                Require(assembly.ManifestModule.ModuleVersionId==new Guid("79234223-ffd6-4a3b-a1ab-6eb0d6aa39f2"),
                    "Unknown SDK private-world snapshot; only the artificial native-state profile is unavailable");
                var service=assembly.GetType("APIShared.MissionLifecycleService",true);
                const BindingFlags all=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance;
                getter=service.GetProperty("NativeWorld",all)?.GetGetMethod(true);
                var clock=service.GetProperty("NativeClock",all)?.GetGetMethod(true);
                worldType=assembly.GetType("APIShared.MissionLifecycleState+NativeWorldState",true);
                var clockType=assembly.GetType("APIShared.MissionLifecycleState+NativeClockState",true);
                var worldField=clockType.GetField("World",all);
                mission=worldType.GetField("Mission",all);sequence=worldType.GetField("Sequence",all);
                Require(getter!=null && getter.IsStatic && getter.GetParameters().Length==0 && getter.ReturnType==worldType &&
                    clock!=null && clock.IsStatic && clock.ReturnType==clockType && getter.Module==typeof(ApiShared).Module &&
                    worldType.IsClass && worldField!=null && !worldField.IsStatic && worldField.IsInitOnly && worldField.FieldType==worldType &&
                    mission!=null && !mission.IsStatic && mission.IsInitOnly && mission.FieldType==typeof(long) &&
                    sequence!=null && !sequence.IsStatic && sequence.IsInitOnly && sequence.FieldType==typeof(long),
                    "SDK native-world identity shape differs from the audited test contract");
                byte[] il=getter.GetMethodBody()?.GetILAsByteArray();
                Require(il!=null && il.Length==17 && il[0]==0x28 && il[5]==0x25 && il[6]==0x2D && il[7]==3 &&
                    il[8]==0x26 && il[9]==0x14 && il[10]==0x2A && il[11]==0x7B && il[16]==0x2A &&
                    getter.Module.ResolveMethod(BitConverter.ToInt32(il,1)).Equals(clock) &&
                    getter.Module.ResolveField(BitConverter.ToInt32(il,12)).Equals(worldField),
                    "SDK NativeWorld getter IL changed; only this artificial profile is unavailable");
            }
            internal object Read(long expectedSession)
            {
                object value=getter.Invoke(null,null);
                Require(value!=null && value.GetType()==worldType && (long)mission.GetValue(value)==expectedSession &&
                    (long)sequence.GetValue(value)>0,"Missing/foreign immutable native-world identity");
                return value;
            }
        }
        private sealed class NativeJesterWord : INoJestersWord
        {
            private const int Flag=0x37EDF82,Lady=0x37EDF80,Pairs=0x379CFD8,Stride=0x583C;
            private readonly IntPtr module;
            private readonly int moduleSize;
            internal NativeJesterWord(IntPtr expectedModule)
            {
                Require(IntPtr.Size==8 && BitConverter.IsLittleEndian,"Native-state fixture requires the audited64-bit little-endian process");
                ProcessModule found=null;using(var process=Process.GetCurrentProcess())foreach(ProcessModule candidate in process.Modules)
                    if(string.Equals(candidate.ModuleName,"CrusaderDE.dll",StringComparison.OrdinalIgnoreCase)){Require(found==null,"Ambiguous native modules");found=candidate;}
                Require(found!=null && found.BaseAddress==expectedModule && expectedModule!=IntPtr.Zero,"Native-state module identity differs");
                module=found.BaseAddress;moduleSize=found.ModuleMemorySize;
                using(var input=File.OpenRead(found.FileName))using(var sha=SHA256.Create())
                    Require(BitConverter.ToString(sha.ComputeHash(input)).Replace("-","")==NativeRelocation.NativeHash,"Native-state DLL hash is unsupported");
                CheckBytes(0xC29AB,new byte[]{0x66,0x39,0x2D,0xD0,0xB5,0x72,0x03});
                CheckBytes(0x951C1,new byte[]{0xC7,0x05,0xB5,0x8D,0x75,0x03,0x00,0x00,0x01,0x00});
                Address(Lady,4,true);for(int id=1;id<=8;id++)Address(Pairs+id*Stride,8,false);
                // Independent live directory/layout corroboration, including width and original save owner.
                var entry=Address(0x337120,24,false);
                Require(Marshal.ReadInt64(entry)==module.ToInt64()+0x37ED180 && Marshal.ReadInt32(entry,8)==0 &&
                    Marshal.ReadInt32(entry,12)==0x82474 && Marshal.ReadInt16(entry,18)==1023,"Native-state save section contract changed");
            }
            private IntPtr Address(int rva,int count,bool writable)
            {
                Require(rva>=0 && count>0 && (long)rva+count<=moduleSize,"Native-state range escaped the selected module");
                IntPtr pointer=new IntPtr(checked(module.ToInt64()+rva));
                Require(VirtualQuery(pointer,out var region,(UIntPtr)Marshal.SizeOf(typeof(Region)))!=UIntPtr.Zero &&
                    region.State==0x1000 && region.Type==0x1000000 && region.AllocationBase==module &&
                    (region.Protect&0x101)==0 && ((region.Protect&0xFF)==2 || (region.Protect&0xFF)==4 || (region.Protect&0xFF)==8 ||
                        (region.Protect&0xFF)==0x20 || (region.Protect&0xFF)==0x40 || (region.Protect&0xFF)==0x80) &&
                    pointer.ToInt64()>=region.Base.ToInt64() && checked(pointer.ToInt64()+count)<=checked(region.Base.ToInt64()+(long)region.Size.ToUInt64()) &&
                    (!writable || (region.Protect&0xFF)==4 || (region.Protect&0xFF)==8),"Native-state memory is not the required committed image data");
                return pointer;
            }
            private void CheckBytes(int rva,byte[] expected)
            {var pointer=Address(rva,expected.Length,false);for(int i=0;i<expected.Length;i++)Require(Marshal.ReadByte(pointer,i)==expected[i],"Foreign native patch at0x"+rva.ToString("X"));}
            internal void RequireSkirmish()
            {
                CheckBytes(0xC29AB,new byte[]{0x66,0x39,0x2D,0xD0,0xB5,0x72,0x03});
                CheckBytes(0x951C1,new byte[]{0xC7,0x05,0xB5,0x8D,0x75,0x03,0x00,0x00,0x01,0x00});
                Require(Marshal.ReadInt32(Address(0x3665F28,4,false))==3 && Marshal.ReadInt32(Address(0x8574B90,4,false))==99,"Native-state world is no longer a singleplayer Skirmish");
            }
            public ushort ReadJester()=>unchecked((ushort)Marshal.ReadInt16(Address(Flag,2,false)));
            public ushort ReadLady()=>unchecked((ushort)Marshal.ReadInt16(Address(Lady,2,false)));
            public bool AllEightOriginalPairsEmpty()
            {
                for(int playerId=1;playerId<=8;playerId++)
                {var pair=Address(Pairs+playerId*Stride,8,false);if(Marshal.ReadInt32(pair)!=0 || Marshal.ReadInt32(pair,4)!=0)return false;}
                return true;
            }
            public void WriteJesterWord(ushort value)=>Marshal.WriteInt16(Address(Flag,2,true),unchecked((short)value));
            [StructLayout(LayoutKind.Sequential)]private struct Region
            {internal IntPtr Base,AllocationBase;internal uint AllocationProtect;internal ushort PartitionId;internal UIntPtr Size;internal uint State,Protect,Type;}
            [DllImport("kernel32.dll")]private static extern UIntPtr VirtualQuery(IntPtr address,out Region region,UIntPtr size);
        }
    }
}
#endif
