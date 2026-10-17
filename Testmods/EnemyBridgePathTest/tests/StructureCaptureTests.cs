using System;
using System.Collections.Generic;
using System.IO;
namespace EnemyBridgePathTest.Tests
{
    internal static class StructureCaptureTests
    {
        private static int checks;private static void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
        private static IEnumerable<byte[]> Write(BridgeStructureCapture.Input input,bool corrupt=false)
        {
            yield return BridgeInputArtifact.Header("nativeHash="+BridgeNativeDefinition.NativeHash+"\nbackendHash="+BridgeNativeDefinition.BackendHash+"\nsession="+input.Session+"\nop="+input.Op+"\nplayer="+input.Player,2);
            foreach(var pair in input.Sections){byte[] data=pair.Value;if(corrupt&&pair.Key=="pre/target"){data=(byte[])data.Clone();data[0x134]^=1;}foreach(var block in BridgeInputArtifact.Section("structure/"+pair.Key,data,1))yield return block;}
            foreach(var block in BridgeInputArtifact.Section("structure/postClock",BitConverter.GetBytes(input.PostClock),1))yield return block;
        }
        internal static int Run()
        {
            checks=0;
            foreach(int player in new[]{1,6})
            {
                int tribe=player*3,building=player*7,unit=player*11;uint tg=(uint)(player*81),bg=(uint)(player*83);object map=new object();var logs=new List<string>();bool reused=false,tableChanged=false,lineEnabled=false;
                byte[] Reader(int address,int length)
                {
                    var result=new byte[length];void I(int offset,int value){Buffer.BlockCopy(BitConverter.GetBytes(value),0,result,offset,4);}void S(int offset,short value){Buffer.BlockCopy(BitConverter.GetBytes(value),0,result,offset,2);}
                    if(address==0x60ad660&&length>=4)I(0,1);
                    if(address==NativePathfindingTableCopy.ProfileRva&&tableChanged)I(0,1);
                    if(address==0x2f7a690&&lineEnabled)I(0,1);
                    if(address==0x64ccbb0+0x50)I(0,building+1);
                    if(address==0x67e8400&&length==BridgePlanningCapture.EntryUnitBytes){I(unit*0x490+0x6f0,reused?32:31);I(unit*0x490+0xa00,17);}
                    if(address==0x7cc6720&&length==BridgePlanningCapture.EntryTribeBytes){I(tribe*0x688+0x2c,player);I(tribe*0x688+0x34,(int)tg);S(tribe*0x688+0x5a,(short)unit);}
                    if(address==0x7cc6720+tribe*0x688+0x34)I(0,(int)tg);
                    if(address==0x64ccbb0+building*0x32c+0x134)I(0,(int)bg);
                    if(address==0x7cc6720+tribe*0x688&&length==0x688){I(0x2c,player);I(0x34,(int)tg);S(0x5a,(short)unit);}
                    if(address==0x64ccbb0+building*0x32c&&length==0x32c)I(0x134,(int)bg);
                    if(address==0x64ccbb0+0x32c&&length==building*0x32c)I((building-1)*0x32c+0x134,(int)bg);
                    if(address==0x67e8400+unit*0x490&&length==0xb00){I(0x6f0,reused?32:31);I(0xa00,17);}
                    return result;
                }
                var capture=new BridgeStructureCapture(Reader,()=>map,(kind,detail)=>logs.Add(kind+":"+detail));capture.Begin(player);
                var input=capture.Capture(50,40,player,tribe,tg,38,building,bg,true,250+player,350+player,260+player,360+player);
                Check(input!=null&&!input.Complete,"valid own pre captured with renumbered identities");Check(input.Sections["pre/lineBlockGrid"].Length==320800&&BitConverter.ToInt32(input.Sections["pre/pathInputCoverage4"],0)==1,"mode-zero normal flood input is copied independently of optional line blocking");Check(capture.Capture(51,40,player,tribe,tg,38,building,bg,true,1,1,2,2)==null,"nested copy cannot replace entry");
                Check(capture.Complete(input,true)==input&&input.Complete&&capture.Available,"complete own post retains identity and one remaining verified-pair attempt");
                string folder=TestWorkDirectory.New("structure-capture");var publisher=new BridgeInputArtifact(folder,(_,kind,detail)=>logs.Add(kind+":"+detail));Check(publisher.Enqueue(player,50,Write(input),2),"bounded follow artifact accepted");int blocks=0;while(publisher.Pending(player)&&blocks++<3000)publisher.Pump();Check(!publisher.Pending(player),"bounded complete delivery");
                BridgePlanningImporter.Artifact a;string reason;Check(BridgePlanningImporter.TryRead(Path.Combine(folder,"bridge-"+player+"-50.bin"),out a,out reason)&&a.Sections.ContainsKey("structure/pre/ringOffsets"),"production writer/import retains own command fields");
                VirtualCandidateBuilder restored;Check(CopiedStructureInput.TryCreate(a,out restored,out reason)&&reason.Contains("postUsedOnlyForComparison=True"),"productive own-command adapter uses own bytes and never post substitutions");
                Check(publisher.Enqueue(player,51,Write(input,true),2),"corrupt-data fixture transport valid");while(publisher.Pending(player)&&blocks++<6000)publisher.Pump();Check(!BridgePlanningImporter.TryRead(Path.Combine(folder,"bridge-"+player+"-51.bin"),out a,out reason)&&reason.Contains("structure-command-identity"),"hash-valid conflicting structure identity is rejected");
                capture.Begin(player+10);input=capture.Capture(52,40,player,tribe,tg,38,building,bg,true,1,1,2,2);reused=true;Check(capture.Complete(input,true)==null,"reused member identity invalidates post");reused=false;input=capture.Capture(53,40,player,tribe,tg,38,building,bg,true,1,1,2,2);Check(input!=null&&capture.Complete(input,false)==null,"suppressed original does not acquire false post proof");Check(!capture.Available,"two failed attempts exhaust own budget");
                capture.Begin(player+20);input=capture.Capture(54,40,player,tribe,tg,38,building,bg,true,1,1,2,2);map=new object();Check(capture.Complete(input,true)==null,"map identity change invalidates command input");capture.End();
                capture.Begin(player+30);input=capture.Capture(60,0,player,tribe,tg,3,building,bg,false,1,1,2,2,unit,31,17,"ongoing-unit-state-snapshot","native-unit-context-building-tile-not-attack-execution-proof",true);
                Check(input!=null&&capture.Complete(input,false)==input&&input.SnapshotOnly&&!input.ExplicitBuilding,"ongoing state captured without claiming original command or selector");
                Check(capture.Capture(61,0,player,tribe,tg,3,building,bg,false,1,1,2,2,unit,31,17,"ongoing-unit-state-snapshot","native-unit-context-building-tile",true)==null,"same ongoing state cannot consume second attempt");
                input=capture.Capture(62,0,player,tribe,tg,3,building,bg,false,1,1,2,2,unit,31,17,"unit-command-pre","native-unit-context-building-tile",false);Check(input!=null&&capture.Complete(input,true)==input,"actual unit command can replace weaker ongoing state within two attempts");
                long[] original={input.Session,input.Op,unit,31,123+player,234+player,-17};var originalBytes=new byte[56];Buffer.BlockCopy(original,0,originalBytes,0,56);input.Sections.Add("unitCommandArguments7",originalBytes);
                input.Sections.Add("commandPostReturn8",BitConverter.GetBytes(5L));input.Sections.Add("commandReturnTiming",System.Text.Encoding.UTF8.GetBytes("mutable-post-event-at-bridge-subscriber;not-raw-native-or-final-publisher-result"));
                Check(input.Sections["post/pathBank1000"].Length==1000&&BitConverter.ToInt64(input.Sections["post/pathBankIdentity5"],16)==unit,"immediate copied path retains exact unit event identity");
                Check(publisher.Enqueue(player+30,62,Write(input),2),"unit actor artifact enqueued");int unitBlocks=0;while(publisher.Pending(player+30)&&unitBlocks++<3000)publisher.Pump();Check(BridgePlanningImporter.TryRead(Path.Combine(folder,"bridge-"+(player+30)+"-62.bin"),out a,out reason)&&a.Sections.ContainsKey("structure/unitIdentity4"),"productive importer validates native header-derived A00 context tile and unit identity");
                Check(CopiedStructureInput.TryCommandClosure(a,out reason)&&reason.Contains("fullCommandReplay=Unknown"),"new own profile/line inputs have bounded initial closure without a full replay claim");
                Check(BitConverter.ToInt64(a.Sections["structure/unitCommandArguments7"],32)==123+player&&BitConverter.ToInt64(a.Sections["structure/identity18"],112)==2,"original command coordinate differs from gate anchor and survives import");
                originalBytes[8]^=1;Check(publisher.Enqueue(player+30,63,Write(input),2),"conflicting original argument fixture written");while(publisher.Pending(player+30)&&unitBlocks++<6000)publisher.Pump();Check(!BridgePlanningImporter.TryRead(Path.Combine(folder,"bridge-"+(player+30)+"-63.bin"),out a,out reason)&&reason.Contains("original-command-binding"),"original args cannot inherit another event op");
                capture.Begin(player+35);var first=capture.Capture(70,70,player,tribe,tg,3,building,bg,false,1,1,2,2,unit,31,17,"unit-command-pre","native-unit-context-building-tile",false);Check(first!=null&&capture.Complete(first,true)==first,"independent same-rank fallback completed");
                Check(capture.Capture(71,71,player,tribe,tg,3,building,bg,false,1,1,2,2,unit,31,17,"unit-command-pre","native-unit-context-building-tile",false)==null,"equal rank unrelated command cannot consume remaining attempt");
                var replacement=capture.Capture(72,72,player,tribe,tg,3,building,bg,false,1,1,2,2,unit,31,17,"unit-command-pre","native-unit-context-building-tile",false,true);Check(replacement!=null&&capture.Complete(replacement,true)==replacement&&!capture.Available,"verified exact planning frame allows equal rank second attempt within budget");
                capture.Begin(player+40);input=capture.Capture(64,0,player,tribe,tg,3,building,bg,false,1,1,2,2,unit,31,17,"unit-command-pre","native-unit-context-building-tile",false);tableChanged=true;Check(input!=null&&capture.Complete(input,true)==null&&logs.Exists(v=>v.Contains("command-table-changed:profiles90")),"live command table mutation rejects historical replay inputs");tableChanged=false;
                capture.Begin(player+50);lineEnabled=true;input=capture.Capture(65,0,player,tribe,tg,3,building,bg,false,1,1,2,2,unit,31,17,"unit-command-pre","native-unit-context-building-tile",false);Check(input!=null&&input.Sections["pre/lineBlockGrid"].Length==320800&&capture.Complete(input,true)==input,"conditional native line-block layer copied once for own command");lineEnabled=false;
            }
            var approach=new CopiedMeleeApproaches(new[]{4,5},new[]{0,0},true,false);Check(approach.Evaluate(true,true,t=>t==5?VirtualReachability.Reachable:VirtualReachability.NoRoute)==VirtualReachability.Reachable,"alternative melee field survives denied deck");Check(approach.Evaluate(true,true,_=>VirtualReachability.NoRoute)==VirtualReachability.NoRoute,"complete deck-only case can reject offline");Check(approach.Evaluate(false,true,_=>VirtualReachability.NoRoute)==VirtualReachability.Unknown,"dirty/unproven topology cannot reject");Check(approach.Evaluate(true,false,_=>VirtualReachability.NoRoute)==VirtualReachability.Unknown,"missing permissions cannot reject");Check(new CopiedMeleeApproaches(new[]{4},new[]{0},false,true).Evaluate(true,true,_=>VirtualReachability.NoRoute)==VirtualReachability.Unknown,"capacity-truncated enumeration cannot prove global rejection");
            Check(BridgeDecisionTrace.HealthDecrease(100,90,true,false)==10,"same target health decrease observed");Check(BridgeDecisionTrace.HealthDecrease(100,90,false,false)==0&&BridgeDecisionTrace.HealthDecrease(100,90,true,true)==0,"reused and nested target cannot attribute health decrease");Check(BridgeDecisionTrace.HealthDecrease(90,100,true,false)==0,"repair is not attack evidence");
            int hp=100;var trace=new BridgeDecisionTrace(null,testCapture:()=>0,testRead:address=>address==0x4b6aa50+11*2?8:address==0x64ccbb0+8*0x32c+0x134?80:address==0x64ccbb0+8*0x32c+0x168?hp:0);trace.SetNative(new IntPtr(1),0x8903000);trace.MarkInstalled(30);trace.StartSession(90);
            var index=(BridgeBuildingIndex)typeof(BridgeDecisionTrace).GetField("buildingIndex",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(trace);index.Entries=new[]{new BridgeBuildingIndex.Entry{Id=7,Global=70,ParentId=8,ParentGlobal=80}};typeof(BridgeBuildingIndex).GetField("built",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(index,typeof(BridgeBuildingIndex).GetField("generation",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(index));typeof(BridgeBuildingIndex).GetField("nextCheck",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(index,long.MaxValue);
            trace.BuildingDamage(false,11,-5,0,10);hp=90;trace.BuildingDamage(true,11,-5,0,10);var status=typeof(BridgeDecisionTrace).GetProperty("DamageStatus",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Check(((string)status.GetValue(trace)).Contains("buildingHealthDecreases=1"),"production damage observer reads actual delta and preserves negative native source class without player lookup");
            trace.BuildingDamage(false,11,1,0,10);trace.BuildingDamage(false,12,1,0,10);trace.BuildingDamage(true,12,1,0,10);hp=80;trace.BuildingDamage(true,11,1,0,10);Check(((string)status.GetValue(trace)).Contains("buildingHealthDecreases=1"),"irrelevant nested event cannot pop outer frame or attribute its delta");
            var history=(BridgeLossHistory)typeof(BridgeDecisionTrace).GetField("lossHistory",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(trace);history.Retain(new[]{new BridgeBuildingIndex.Entry{Id=7,Global=70,ParentId=8,ParentGlobal=80,Occupied=new[]{11}}});index.Entries=Array.Empty<BridgeBuildingIndex.Entry>();trace.BuildingDamage(false,11,1,0,5);hp=75;trace.BuildingDamage(true,11,1,0,5);Check(((string)status.GetValue(trace)).Contains("buildingHealthDecreases=2"),"actual production damage observer retains gate after last live bridge leaves index");
            for(int i=0;i<18;i++)trace.BuildingDamage(false,11,1,0,1);hp=70;for(int i=0;i<18;i++)trace.BuildingDamage(true,11,1,0,1);string bounded=(string)status.GetValue(trace);Check(bounded.Contains("buildingDamageSkipped=2")&&bounded.Contains("buildingDamageCoverageComplete=False"),"damage depth cap preserves balanced stack and explicitly incomplete coverage");trace.End();
            Console.WriteLine("PASS own structure command capture/writer/import: renumbering, context, suppression, identities, budgets and conservative melee alternatives");return checks;
        }
    }
}
