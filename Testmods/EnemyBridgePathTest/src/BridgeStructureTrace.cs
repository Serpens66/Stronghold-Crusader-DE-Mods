using System;
using System.Runtime.InteropServices;
namespace EnemyBridgePathTest
{
    internal sealed unsafe partial class BridgeDecisionTrace
    {
        private BridgeStructureCapture structureCapture;
        private bool ValidateStructurePair(BridgeStructureCapture.Input input)
        {
            if(input.Session!=session||!ReferenceEquals(input.Token,planningMapIdentity)||input.Thread!=System.Threading.Thread.CurrentThread.ManagedThreadId||input.Tribe<1||input.Tribe>=4500||input.Building<1||input.Building>4000||input.TribeGlobal==0||input.BuildingGlobal==0)return false;
            if(unchecked((uint)Read(0x7cc6720+input.Tribe*0x688+0x34))!=input.TribeGlobal||unchecked((uint)Read(0x64ccbb0+input.Building*0x32c+0x134))!=input.BuildingGlobal||(Read(0x64ccbb0+input.Building*0x32c+0x12c)&65535)!=2)return false;
            if(input.Unit==0)return input.ExplicitBuilding;
            if(input.Unit<1||input.Unit>9999||input.UnitGlobal==0||!input.Sections.TryGetValue("unitIdentity4",out byte[] actor)||actor.Length!=32)return false;
            int at=0x67e8400+input.Unit*0x490;
            return unchecked((uint)Read(at+0x6f0))==input.UnitGlobal&&(Read(at+0x6e4)&65535)==2&&unchecked((uint)Read(at+0xa00))==BitConverter.ToUInt64(actor,16);
        }
        private readonly BridgeLossHistory lossHistory;
        private long damagePre,damagePost,damageChanges,damageSkippedEvents;private int damageSamples,structureSamples;
        [ThreadStatic] private static System.Collections.Generic.List<DamageFrame> damageFrames;
        [ThreadStatic] private static long damageSession;
        [ThreadStatic] private static int damageSkipped;
        private sealed class DamageFrame{internal long Session;internal object Map;internal int Tile,Building,Hp,Source,Unit,Damage;internal uint Global,UnitGlobal;internal bool Nested;}
        internal static int HealthDecrease(int before,int after,bool sameIdentity,bool nested) => sameIdentity&&!nested&&before>after?before-after:0;
        private void BeginDamage(){damagePre=damagePost=damageChanges=damageSkippedEvents=0;damageSamples=structureSamples=0;damageFrames?.Clear();damageSkipped=0;damageSession=session;}
        private string DamageStatus=>"buildingDamagePre="+damagePre+",buildingDamagePost="+damagePost+",buildingHealthDecreases="+damageChanges+",buildingDamageSkipped="+damageSkippedEvents+",buildingDamageCoverageComplete="+(damagePre==damagePost&&damageSkippedEvents==0);
        internal void BuildingDamage(bool post,int tile,int source,int unit,int amount)
        {
            if(native==IntPtr.Zero||installed!=BridgeNativeDefinition.OwnedCount||session==0||(uint)tile>=320800)return;
            try
            {
                if(damageSession!=session){damageFrames?.Clear();damageSkipped=0;damageSession=session;}
                if(damageSkipped>0){if(!post)System.Threading.Interlocked.Increment(ref damageSkippedEvents);damageSkipped+=post?-1:1;return;}
                if(!post&&damageFrames!=null&&damageFrames.Count>=16){if(damageFrames[damageFrames.Count-1]!=null)damageFrames[damageFrames.Count-1].Nested=true;System.Threading.Interlocked.Increment(ref damageSkippedEvents);damageSkipped=1;CountBackground("building-damage-nesting-limit");return;}
                if(!post)
                {
                    if(damageFrames==null)damageFrames=new System.Collections.Generic.List<DamageFrame>();
                    if(damageFrames.Count>0&&damageFrames[damageFrames.Count-1]!=null)damageFrames[damageFrames.Count-1].Nested=true;damageFrames.Add(null);
                    int id=Read(0x4b6aa50+tile*2)&65535;if(id<1||id>4000)return;int record=0x64ccbb0+id*0x32c;uint global=unchecked((uint)Read(record+0x134));if(global==0)return;bool relevant=false;
                    lock(captureGate){buildingIndex.Refresh();lossHistory.Retain(buildingIndex.Entries);relevant=lossHistory.KnowsGate(id,global);foreach(var bridge in buildingIndex.Entries)if(bridge.ParentId==id&&bridge.ParentGlobal==global){relevant=true;break;}}
                    if(!relevant)return;
                    lock(captureGate)lossHistory.Sample("damage-pre",id,global);
                    damageFrames[damageFrames.Count-1]=new DamageFrame{Session=session,Map=planningMapIdentity,Tile=tile,Building=id,Global=global,Hp=(short)Read(record+0x168),Source=source,Unit=unit,UnitGlobal=unit>=1&&unit<=9999?unchecked((uint)Read(0x67e8400+unit*0x490+0x6f0)):0,Damage=amount};System.Threading.Interlocked.Increment(ref damagePre);return;
                }
                if(damageFrames==null||damageFrames.Count==0)return;var frame=damageFrames[damageFrames.Count-1];damageFrames.RemoveAt(damageFrames.Count-1);if(frame==null)return;if(frame.Tile!=tile||frame.Source!=source||frame.Unit!=unit){CountBackground("building-damage-event-pair-mismatch");return;}System.Threading.Interlocked.Increment(ref damagePost);
                if(frame.Session!=session||!ReferenceEquals(frame.Map,planningMapIdentity)){System.Threading.Interlocked.Increment(ref damageSkippedEvents);CountBackground("building-damage-session-gap");return;}
                int address=0x64ccbb0+frame.Building*0x32c;bool same=unchecked((uint)Read(address+0x134))==frame.Global;int after=(short)Read(address+0x168);lock(captureGate)lossHistory.Sample("damage-post",frame.Building,frame.Global);int delta=HealthDecrease(frame.Hp,after,same,frame.Nested);if(delta>0)System.Threading.Interlocked.Increment(ref damageChanges);
                long command=0,decision=0;bool binding=false;if(unit>=1&&unit<=9999){uint global=unchecked((uint)Read(0x67e8400+unit*0x490+0x6f0));if(global!=0&&global==frame.UnitGlobal)binding=Routes.TryDamageBinding(unit,global,out command,out decision);}
                if(System.Threading.Interlocked.Increment(ref damageSamples)<=16)Observe("building-damage-observed","building="+frame.Building+"/g"+frame.Global+",sourceRaw="+source+",sourcePlayerKnown="+(source>=1&&source<=8)+",unitArgument="+unit+",unitCommandBound="+binding+",commandOp="+command+",decisionState="+decision+",hp="+frame.Hp+"/"+after+",healthDecrease="+delta+",sameIdentity="+same+",nested="+frame.Nested+",amountPreObserved="+frame.Damage+",amountPostOriginal="+amount+",argumentTiming=sdk-pre-observed-not-final-effective-damage,damageKind=shared-native-path-melee-not-proven,postReturn=not-exposed-by-sdk");
            }
            catch(Exception error){if(System.Threading.Interlocked.Increment(ref damageSamples)<=16)Observe("building-damage-unresolved","reason="+error.Message);}
        }
        private void StructureUnresolved(long op,string reason){if(System.Threading.Interlocked.Increment(ref structureSamples)<=8)Observe("structure-command-unresolved","op="+op+",reason="+reason);else CountBackground("structure-command-unresolved-repeat");}
        private byte[] CopyStructureBytes(int rva,int length)
        {if(native==IntPtr.Zero||rva<0||length<0||(long)rva+length>nativeLength)throw new InvalidOperationException("structure-copy-outside-module");var bytes=new byte[length];Marshal.Copy(IntPtr.Add(native,rva),bytes,0,length);return bytes;}
        internal BridgeStructureCapture.Input BeginStructureCommand(long op,long parent,int kind,int player,int tribe,uint global,int command,int x,int y,int startX,int startY,int unitId=0,int unitUnknown=0)
        {
            if(player<1||player>8||tribe<1||tribe>=4500||global==0||VirtualShadow?.HasPublishedGroup==true||structureCapture==null||!structureCapture.Available||installed!=BridgeNativeDefinition.OwnedCount||!(kind==1&&command==38||kind==2||kind==3||kind==4))return null;
            try
            {
                if(Read(0x379ae64+player*0x583c)==0)return null; // Native non-AI player rows cannot consume the follow capture.
                int building=0,tx=x,ty=y;uint targetGlobal=0,unitTargetTile=0,unitGlobal=0;bool explicitTarget=kind==1;string targetEvidence=explicitTarget?"explicit-building-command":"coordinate-candidate-not-selector-proof";
                if(kind==3||kind==4)
                {
                    if(unitId<1||unitId>9999||!APIShared.UnitAccess.TryGetById(unitId,out SHCDESE.Interop.GameUnit* unit,out _)||unit==null||unit->r_GlobalId!=global||unit->r_TribeId!=tribe||unit->r_ControllableForPlayerId!=player)return null;
                    unitGlobal=global;unitTargetTile=unit->r_AI_ContextTargetBuildingTileId;
                    if(unitTargetTile!=0&&unitTargetTile<320800){building=Read(0x4b6aa50+(int)unitTargetTile*2)&65535;if(building>0&&building<=4000)targetGlobal=unchecked((uint)Read(0x64ccbb0+building*0x32c+0x134));targetEvidence="native-unit-context-building-tile-not-attack-execution-proof";}
                    if(building==0){if(!Routes.TryRemainingGate(unitId,unitGlobal,out building,out targetGlobal))return null;targetEvidence="remaining-deck-path-coupling-candidate-not-building-target-proof";}
                    global=unchecked((uint)Read(0x7cc6720+tribe*0x688+0x34));
                    lock(captureGate){buildingIndex.Refresh();lossHistory.Retain(buildingIndex.Entries);if(!lossHistory.KnowsGate(building,targetGlobal))return null;}
                    int at=0x64ccbb0+building*0x32c;
                    if(unchecked((uint)Read(at+0x134))!=targetGlobal||(Read(at+0x12c)&65535)!=2)return null;
                    if(!BridgeSnapshot.IsGate((SHCDESE.Interop.eStructs)(Read(at+0x12e)&65535))){StructureUnresolved(op,"historical-gate-current-type-changed");return null;}
                    tx=(short)Read(at+0x2bc);ty=(short)Read(at+0x2be);
                }
                lock(captureGate)
                {
                    buildingIndex.Refresh();
                    if(kind==1||kind==2)foreach(var bridge in buildingIndex.Entries)
                    {
                        if(bridge.Id<1||bridge.Id>4000||unchecked((uint)Read(0x64ccbb0+bridge.Id*0x32c+0x134))!=bridge.Global)continue;
                        int id=bridge.ParentId;if(id<1||id>4000||bridge.ParentGlobal==0)continue;
                        int record=0x64ccbb0+id*0x32c;if(unchecked((uint)Read(record+0x134))!=bridge.ParentGlobal)continue;
                        int bx=(short)Read(record+0x2bc),by=(short)Read(record+0x2be);
                        if(explicitTarget?id!=x||unchecked((uint)y)!=bridge.ParentGlobal:bx!=x||by!=y)continue;
                        if(building!=0&&building!=id){StructureUnresolved(op,"ambiguous-coordinate-target");return null;}
                        building=id;targetGlobal=bridge.ParentGlobal;tx=bx;ty=by;
                    }
                }
                if(building==0)return null;
                Scope root=CurrentScope;while(root!=null&&root.Site.Rva!=0x3C2E0)root=root.Parent;long rootId=root!=null&&root.Session==session&&root.PlanningPlayer==player?root.Id:0;
                var input=structureCapture.Capture(op,rootId,player,tribe,global,command,building,targetGlobal,explicitTarget,startX,startY,tx,ty,unitId,unitGlobal,unitTargetTile,kind==4?"ongoing-unit-state-snapshot":kind==3?"unit-command-pre":"group-command-pre",targetEvidence,kind==4);
                if(input!=null)
                {
                    // Original event arguments are separate from the resolved gate anchor.
                    if(kind==3)
                    {
                        long[] original={session,op,unitId,unitGlobal,x,y,unitUnknown};
                        var raw=new byte[56];Buffer.BlockCopy(original,0,raw,0,56);
                        input.Sections.Add("unitCommandArguments7",raw);
                    }
                    // This is an actual building argument only for command38.
                    if(explicitTarget)lock(captureGate){structureTargets[player]=building;structureTargetGlobals[player]=targetGlobal;}
                    var caller=new long[]{CurrentId,CurrentScope?.Site.Rva??0};var callerBytes=new byte[16];Buffer.BlockCopy(caller,0,callerBytes,0,16);input.Sections.Add("nativeCaller2",callerBytes);
                    CompareGroupShadow(op,parent,player,startX,startY,kind==3||kind==4?x:tx,kind==3||kind==4?y:ty,tribe,global,kind==4?"group-formation-ongoing-unit-state-hypothesis":kind==3?"group-formation-unit-command-hypothesis":"group-formation");
                    VirtualShadow?.RetainStructureCommand(input,buildingIndex);
                }
                return input;
            }
            catch(Exception error){StructureUnresolved(op,error.Message);return null;}
        }
        internal void ObserveUnitStructure(int unitId)
        {
            // Existing movement publisher: this snapshot is an old task observation,
            // never a fabricated new command, selector result or fresh decision.
            if(structureCapture==null||VirtualShadow?.HasPublishedGroup==true||!structureCapture.CanCaptureRank(2)||unitId<1||unitId>9999||!APIShared.UnitAccess.TryGetById(unitId,out SHCDESE.Interop.GameUnit* unit,out _)||unit==null)return;
            if(unit->r_AI_ContextTargetBuildingTileId==0&&!structureCapture.CanCaptureRank(1))return;
            if(unit->r_AI_ContextTargetBuildingTileId==0&&!Routes.HasRemainingDeck(unitId))return;
            var input=BeginStructureCommand(NewOperation(),0,4,unit->r_ControllableForPlayerId,unit->r_TribeId,unit->r_GlobalId,unit->r_AI_LastIssuedTribeCommand,unit->r_ContextTargetTileX,unit->r_ContextTargetTileY,unit->r_CurrentTilePositionX,unit->r_CurrentTilePositionY,unitId);
            if(input!=null)EndStructureCommand(input,false);
        }
        internal void EndStructureCommand(BridgeStructureCapture.Input input,bool originalCalled)
        {
            if(input==null)return;
            try{var complete=structureCapture?.Complete(input,originalCalled);if(complete!=null)VirtualShadow?.CompleteStructureCommand(complete);}
            catch(Exception error){Observe("structure-command-incomplete","op="+input.Op+",reason="+error.Message);}
        }
    }
}
