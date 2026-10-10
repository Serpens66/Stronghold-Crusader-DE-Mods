using System;
namespace EnemyBridgePathTest
{
    // Schema-2 continuation inputs. Historical missing tables stay Unknown;
    // there is no DLL-default substitution or current-game lookup here.
    internal static class CopiedCandidateInputs
    {
        internal static bool TryCreate(BridgePlanningCapture.Bundle bundle,VirtualPlanningState calculated,VirtualBridgeMap physical,byte[] macroRecords,int[] effectiveCounts,out VirtualCandidateBuilder builder,out string reason)
        {
            builder=null;reason="Unknown:missing-candidate-inputs";
            try
            {
                if(bundle==null||!bundle.Complete||calculated==null||!calculated.Complete||physical==null||physical.Session!=bundle.Session||physical.Revision!=bundle.Revision||bundle.Attacker<1||bundle.Attacker>8||bundle.Target<1||bundle.Target>8||bundle.Bank!=bundle.Target)throw new ArgumentException("candidate-context");
                const int n=320800;byte[] Get(string key)=>CopiedPlanningBundle.Resolve(bundle,key);
                var actual=new VirtualCandidateBuilder();
                byte[] Raw(Array values){var bytes=new byte[Buffer.ByteLength(values)];Buffer.BlockCopy(values,0,bytes,0,bytes.Length);return bytes;}
                void Put(int address,string key,int expected=-1){var bytes=Get(key);if(expected>=0&&bytes.Length!=expected)throw new ArgumentException("candidate-input-extent:"+key);actual.Put(address,bytes);}
                var expectedCounts=new int[1000];if(physical.Components.Length!=n||physical.Flags.Length!=n||physical.Edges.Length!=n||effectiveCounts==null||effectiveCounts.Length!=1000)throw new ArgumentException("candidate-map-extent");
                foreach(int region in physical.Components){if(region>=1000)throw new ArgumentException("candidate-component-bound");if(region!=0)expectedCounts[region]++;}
                for(int i=0;i<1000;i++)if(expectedCounts[i]!=effectiveCounts[i])throw new ArgumentException("candidate-effective-histogram");
                foreach(var table in new[]{Tuple.Create("nativeWorkDirections64",0x2d2e50,64),Tuple.Create("nativeDirectionMasks8",0x312620,8),Tuple.Create("nativeWorkBuildingClasses336",0x2e68d0,1344)})
                {
                    string key="consumer/pre/"+table.Item1;
                    if(!bundle.Sections.ContainsKey(key)&&!bundle.References.ContainsKey(key))throw new ArgumentException("missing-own-consumer-table:"+table.Item1+":rva="+table.Item2.ToString("X")+":bytes="+table.Item3);
                    Put(table.Item2,key,table.Item3);var pre=Get(key);var post=Get("consumer/post/"+table.Item1);
                    if(post.Length!=pre.Length)throw new ArgumentException("candidate-table-post-extent");for(int i=0;i<pre.Length;i++)if(pre[i]!=post[i])throw new ArgumentException("candidate-table-changed:"+table.Item1);
                }
                Put(VirtualCandidateConsumers.Root,"consumer/pre/candidates",VirtualCandidateConsumers.Bytes);
                Put(0x67e8400,"consumer/pre/unitManager");Put(0x4c559b0,"consumer/pre/unitTiles",n*2);
                actual.Put(0x48f71b0,Raw(physical.Flags));actual.Put(0x50ec690,Raw(physical.Components));actual.Put(0x51890d0,physical.Edges);
                Put(0x4ddd350,"consumer/pre/height",n);Put(0x4e2b870,"consumer/pre/baseHeight",n);Put(0x4e79d90,"consumer/pre/terrainOwner",n);Put(0x4b6aa50,"consumer/pre/buildingIds",n*2);
                Put(0x64ccbb0+0x32c,"consumer/pre/buildingRecords");Put(0x37edf3c,"consumer/pre/nativeAlliances9",36);
                Put(0x60ad660,"consumer/pre/workControls",0xe0);
                if(macroRecords==null||macroRecords.Length!=Get("consumer/pre/macroRecords").Length)throw new ArgumentException("candidate-macro-extent");actual.Put(0x60ad660+0x2228,macroRecords);
                Put(0x3a11ea4,"consumer/pre/packedValidity",640000);Put(0x8574bcc,"consumer/pre/combatClassMask"); // historical key, actually int player categories
                Put(0x2e6710,"consumer/pre/buildingTransitionClasses",336*4);Put(0x2e6c50,"consumer/pre/buildingSeedClasses",336*4);Put(0x8574b90,"consumer/pre/costBranch",1);
                Put(0x405edb0,"directionOffsets",800*32);Put(0x402ff2c,"rowRecords",800*12);Put(0x3aae2a4,"tileRows",n*2);
                if(calculated.Seeds.Length!=n||calculated.Distance.Length!=n)throw new ArgumentException("candidate-calculated-fields");
                actual.Put(0x535ef90,Raw(calculated.Seeds));actual.Put(0x53ad4b0,new byte[n]);actual.Put(0x5759230+bundle.Bank*n*2,Raw(calculated.Distance));
                Put(0x379ae00,"consumer/pre/players",9*0x583c);Put(0x37f1edc,"consumer/pre/workTargets",9*50*16);
                Put(0x60ad660+0x155f38,"consumer/pre/workQueueControls",0x34);Put(0x60ad660+0x155f6c,"consumer/pre/workQueue",n*4);Put(0x60ad660+0x28f3ec,"consumer/pre/workQueueRows",n*2);Put(0x60ad660+0x32be2c,"consumer/pre/workQueueX",n*2);Put(0x5225b10,"consumer/pre/workDistances",n*2);Put(0x52c2550,"consumer/pre/workVisits",n*2);
                actual.SelectAndUnitTargets(bundle.Attacker,bundle.Target,effectiveCounts);builder=actual;
                reason="complete-own-candidate-inputs;dirty="+bundle.Dirty+";historicalOnly=True;negativePolicyEligible=False";return true;
            }
            catch(Exception error){reason="Unknown:candidate-inputs:"+error.Message;return false;}
        }
    }
}
