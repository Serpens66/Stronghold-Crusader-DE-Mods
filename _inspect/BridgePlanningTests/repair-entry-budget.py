from pathlib import Path
root=Path('Testmods/EnemyBridgePathTest')
def change(path,old,new):
    p=Path(path);s=p.read_text(encoding='utf-8-sig')
    if new in s:return
    assert old in s,(str(p),old[:100])
    s=s.replace(old,new)
    p.write_bytes(s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))
p=root/'src/BridgePlanningCapture.cs'
change(p,'private string lastReason="awaiting-fresh-relevant-planning";', '''private string lastReason="awaiting-fresh-relevant-planning";
        private string terminalReason="none",entryReason="not-observed";
        private long entrySkipped;
        // The only phase-5 paths before this test retain phase 5 or leave for
        // 7/8, which cannot call 2D250. 3CF20/126020 and 2FC80 do not write
        // E78C. The signed increment wraps exactly as the native word INC.
        internal static bool Phase5PlanningDue(short before) => unchecked((short)(before+1))>3;
        private bool EntryMayPlan(int player)
        {
            int stride=player*0x583c,phase=Int(0x379D974+stride);
            if(!IsDecisionCapturePhase(phase)){entryReason="entry-phase-not-supported";return false;}
            if(Int(0x379D0D0+stride)==0){entryReason="entry-lord-disabled";return false;}
            if(phase==5&&!Phase5PlanningDue(BitConverter.ToInt16(Read(0x379E78C+stride,2),0))){entryReason="phase5-signed-timer-wait";return false;}
            // Phase 4 may reset its timer after the engineer scan: the old
            // timer is deliberately not treated as a proven early return.
            entryReason=phase==5?"phase5-timer-due-other-branches-observed-by-root":"phase4-potential-engineer-dependent";
            return true;
        }''')
change(p,'",planningCaptureReason="+lastReason+', '",planningCaptureReason="+lastReason+",planningTerminalReason="+terminalReason+",militaryEntrySkipped="+entrySkipped+",militaryEntrySelectionReason="+entryReason+')
change(p,'selected=false;attempts=entryAttempts=0;', 'selected=false;attempts=entryAttempts=0;entrySkipped=0;entryReason="not-observed";terminalReason="none";')
change(p,'{selected=false;lastReason=reason;emit("planning-artifact-retry"', '{selected=false;lastReason=terminalReason=reason;emit("planning-artifact-retry"')
change(p,'if(current==null)return;lastReason=reason;rejected++;','if(current==null)return;lastReason=terminalReason=reason;rejected++;')
change(p,'if(IsDecisionCapturePhase(Int(0x379D974+a*0x583c))&&Int(0x379D0D0+a*0x583c)!=0&&!string.IsNullOrEmpty(selectionReason=relevance==null?"fixture-unrestricted":relevance(a)))','if(!string.IsNullOrEmpty(selectionReason=relevance==null?"fixture-unrestricted":relevance(a))&&EntryMayPlan(a))')
change(p,'militaryInputs=copied;militaryEntryOp=op;\n                    }', 'militaryInputs=copied;militaryEntryOp=op;\n                    }\n                    else entrySkipped++;')
change(p,'lastReason="consumer-incomplete:"+error.Message;', 'lastReason=terminalReason="consumer-incomplete:"+error.Message;')
change(p,'catch(Exception error){emit("planning-target-incomplete"', 'catch(Exception error){lastReason=terminalReason="selection-incomplete:"+error.Message;emit("planning-target-incomplete"')
p=root/'src/BridgeStructureTrace.cs'
change(p,'int startX,int startY,int unitId=0)', 'int startX,int startY,int unitId=0,int unitUnknown=0)')
change(p,'// This is an actual building argument only for command38.', '''// Original event arguments are separate from the resolved gate anchor.
                    if(kind==3)
                    {
                        long[] original={session,op,unitId,unitGlobal,x,y,unitUnknown};
                        var raw=new byte[56];Buffer.BlockCopy(original,0,raw,0,56);
                        input.Sections.Add("unitCommandArguments7",raw);
                    }
                    // This is an actual building argument only for command38.''')
p=root/'src/BridgeDiagnostics.cs'
change(p,'frame.StartX,frame.StartY,kind==3?frame.Id:0);','frame.StartX,frame.StartY,kind==3?frame.Id:0,frame.Extra);')
p=root/'src/BridgePlanningImporter.cs'
change(p,'if(a.Sections.TryGetValue("structure/unitIdentity4",out byte[] actor))', '''if(a.Sections.TryGetValue("structure/unitCommandArguments7",out byte[] commandArgs))
                    {
                        if(commandArgs.Length!=56||!a.Sections.TryGetValue("structure/unitIdentity4",out byte[] commandActor)||commandActor.Length!=32||!a.Sections.TryGetValue("structure/observationOrigin",out byte[] commandOrigin)||System.Text.Encoding.UTF8.GetString(commandOrigin)!="unit-command-pre")throw new InvalidDataException("structure-original-command-extent-or-origin");
                        long[] args=new long[7];Buffer.BlockCopy(commandArgs,0,args,0,56);
                        if(args[0]!=own[0]||args[1]!=own[1]||args[2]!=BitConverter.ToInt64(commandActor,0)||args[3]!=BitConverter.ToInt64(commandActor,8)||(ulong)args[4]>=800||(ulong)args[5]>=800||args[6]<int.MinValue||args[6]>int.MaxValue||BitConverter.ToInt64(commandActor,24)!=0)throw new InvalidDataException("structure-original-command-binding");
                    }
                    if(a.Sections.TryGetValue("structure/unitIdentity4",out byte[] actor))''')
p=Path('_inspect/BridgePlanningTests/Program.cs')
change(p,'m.Int(0x379D974+8*0x583c,5);bounded.Begin(8);', '''m.Int(0x379D974+8*0x583c,5);bounded.Begin(8);
            foreach(short timer in new short[]{0,1,2,short.MaxValue,short.MinValue,-1}){m.Put(0x379E78C+8*0x583c,BitConverter.GetBytes(timer));bounded.Observe(0x3C2E0,false,8,605,590,8,8,0,0,0,true);}
            Check(fullReads==6&&bounded.Status.Contains("militaryEntryAttempts=0"),"phase5 wait cycles and signed overflow consume no full manager reads or budget");
            m.Put(0x379E78C+8*0x583c,BitConverter.GetBytes((short)3));''')
