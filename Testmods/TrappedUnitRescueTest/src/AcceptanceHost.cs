using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using APIShared.Simulation;
using APIShared.WorldMutations;
using BugfixesAndQoL.TrappedUnitRescue;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace TrappedUnitRescueTest
{
    public sealed partial class Plugin
    {
        private static FieldInfo acceptanceReady,acceptanceAttempted;
        private static bool acceptanceHolding;
        private static long acceptanceSession,acceptanceWorld;
        private static int acceptanceHeld,acceptanceRestored;
        private static void ArmAcceptanceDrain()
        {
            acceptanceReady=typeof(TrappedUnitRescueFeature).GetField("ready",BindingFlags.Instance|BindingFlags.NonPublic);
            acceptanceAttempted=typeof(TrappedUnitRescueFeature).GetField("attempted",BindingFlags.Instance|BindingFlags.NonPublic);
            if(acceptanceReady?.FieldType!=typeof(int) || acceptanceAttempted?.FieldType!=typeof(int))throw new InvalidOperationException("Production readiness contract changed.");
            using(var module=ModuleDefinition.ReadModule(typeof(TrappedUnitRescueFeature).Assembly.Location))
            {
                if(module.Mvid!=typeof(TrappedUnitRescueFeature).Module.ModuleVersionId)throw new InvalidOperationException("Loaded/disk host identity differs.");
                var type=module.GetType(typeof(TrappedUnitRescueFeature).FullName);
                foreach(var method in type.Methods)
                {
                    if(!method.HasBody || method.Name=="Initialize")continue;
                    foreach(var instruction in method.Body.Instructions)
                    {
                        if(!(instruction.Operand is FieldReference field) || field.Name!="ready" || field.DeclaringType.FullName!=type.FullName)continue;
                        if(instruction.OpCode.Code==Code.Stfld)throw new InvalidOperationException("Host acquired another readiness writer.");
                        if(instruction.OpCode.Code!=Code.Ldflda)continue;
                        var next=instruction.Next;
                        for(int i=0;i<6 && next!=null;i++,next=next.Next)
                            if(next.Operand is MethodReference called && called.DeclaringType.FullName=="System.Threading.Volatile" && called.Name=="Write")
                                throw new InvalidOperationException("Host acquired another readiness writer.");
                    }
                }
            }
            if(!SimulationTickEvents.TryRegister(Guid,"acceptance-production-drain-hold",HoldAcceptanceDrain,out string reason,order:-50) ||
                !SimulationTickEvents.TryRegister(Guid,"acceptance-production-drain-restore",RestoreAcceptanceTick,out reason,order:1))
                throw new InvalidOperationException(reason);
        }
        private static void HoldAcceptanceDrain(int tick)
        {
            if(fixture==null || !Fixture.HoldRecoveryTick(tick))return;
            if(acceptanceHolding || !host.Ready || (int)acceptanceAttempted.GetValue(host)!=1 ||
                !settings.EnableMod || !settings.EnableTrappedUnitRescue ||
                !WorldMutationEvents.TryGetCurrentWorld(out acceptanceSession,out acceptanceWorld) || missions.Current?.SessionId!=acceptanceSession)
                throw new InvalidOperationException("Acceptance hold lost its initialized exact host/world.");
            acceptanceReady.SetValue(host,0);acceptanceHolding=true;acceptanceHeld++;
            if(acceptanceHeld==1)Stamp("production-drain-hold session="+acceptanceSession+" world="+acceptanceWorld+" tick="+tick+" settingsUnchanged=true beforeOrder=0 restoreOrder=1");
        }
        private static void RestoreAcceptanceTick(int tick)=>RestoreAcceptanceDrain();
        private static void RestoreAcceptanceDrain()
        {
            if(!acceptanceHolding)return;
            if((int)acceptanceReady.GetValue(host)!=0 || (int)acceptanceAttempted.GetValue(host)!=1 ||
                !settings.EnableMod || !settings.EnableTrappedUnitRescue ||
                !WorldMutationEvents.TryGetCurrentWorld(out long session,out long world) || session!=acceptanceSession || world!=acceptanceWorld ||
                missions.Current?.SessionId!=session)
                throw new InvalidOperationException("Acceptance will not restore another/fail-closed production world.");
            acceptanceReady.SetValue(host,1);acceptanceHolding=false;acceptanceRestored++;
            if(acceptanceRestored==1 || acceptanceRestored%128==0)Stamp("production-drain-restored session="+session+" world="+world+" held="+acceptanceHeld+" restored="+acceptanceRestored+" beforeNativePass=true");
        }
    }
    internal sealed partial class Fixture
    {
        private static void CheckProductionScannerDisabled()
        {
            var type=typeof(MountedScannerEarlyProvider);
            var requested=type.GetField("requested",BindingFlags.Static|BindingFlags.NonPublic);
            var capture=type.GetField("capture",BindingFlags.Static|BindingFlags.NonPublic)?.GetValue(null);
            Require(requested?.FieldType==typeof(int) && (int)requested.GetValue(null)==0,"OFF production scanner remains requested");
            if(capture==null)return; // Absent/unknown upstream has no owned native correction cell.
            var field=capture.GetType().GetField("activation",BindingFlags.Instance|BindingFlags.NonPublic);
            Require(field?.FieldType==typeof(IntPtr),"Production scanner native gate contract changed");
            var address=(IntPtr)field.GetValue(capture);var bytes=Shared.CommittedMemory.ReadUpTo(address,4);
            Require(bytes.Length==4 && BitConverter.ToInt32(bytes,0)==0,"OFF production scanner has a live correction gate");
        }
        // This bounded scope belongs ONLY to direct acceptance regressions. The
        // production runtime contains neither this helper nor a full-unit census.
        private AcceptanceMutation BeginMutation(string label,int x,int y,int radius)=>new AcceptanceMutation(this,label,x,y,radius);
        private sealed class AcceptanceMutation : IDisposable
        {
            private readonly Fixture fixture;private readonly string label;private bool complete;
            internal readonly List<UnitSnapshot> Before=new List<UnitSnapshot>();
            internal unsafe AcceptanceMutation(Fixture fixture,string label,int x,int y,int radius)
            {
                if(radius<0 || radius>32)throw new ArgumentOutOfRangeException(nameof(radius));this.fixture=fixture;this.label=label;
                foreach(var actor in fixture.actors)
                    if(fixture.runtime.Native.TryUnit(actor.Id,out var unit) && unit->r_GlobalId==actor.Global &&
                        Math.Max(Math.Abs(unit->r_CurrentTilePositionX-x),Math.Abs(unit->r_CurrentTilePositionY-y))<=radius)
                    {var snapshot=fixture.runtime.SnapshotUnit(actor.Id);if(snapshot!=null)Before.Add(snapshot);}
            }
            internal RescueResult[] Complete(bool resume=false)
            {
                if(complete)throw new InvalidOperationException("Acceptance mutation already completed.");complete=true;
                bool prior=Plugin.GeneralObserver.IsActive;
                try
                {
                    if(fixture.expectedEnabled)Plugin.SetGeneralFixtureSession(true);
                    var results=new List<RescueResult>();
                    foreach(var snapshot in Before){fixture.runtime.TryRescue(snapshot,label,resume,out var result);results.Add(result);}
                    return results.ToArray();
                }
                finally{if(!prior && Plugin.GeneralObserver.IsActive)Plugin.SetGeneralFixtureSession(false);}
            }
            public void Dispose(){complete=true;}
        }
    }
}
