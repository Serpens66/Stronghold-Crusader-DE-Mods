using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using DesyncTest;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using static Iced.Intel.AssemblerRegisters;

namespace DesyncTest { public static class DesyncTestPlugin { public const string Guid="DesyncTest_Serp"; } }

internal static unsafe class Program
{
    private static int assertions;
    static void Check(bool value,string message){assertions++;if(!value)throw new Exception(message);}
    [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr VirtualAlloc(IntPtr address,UIntPtr size,uint type,uint protect);
    [DllImport("kernel32.dll")]static extern bool VirtualFree(IntPtr address,UIntPtr size,uint type);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool VirtualProtect(IntPtr address,UIntPtr size,uint protection,out uint previous);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate uint Crc(IntPtr unused,int size,IntPtr bytes);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void Thunk(IntPtr target);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void FixtureHandler();
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void Forced(IntPtr text,int tick,int expected,int started,int now);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate int WorkerEntry(IntPtr manager,ushort role,int building,int player,int ordinal);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate int EntranceEntry(IntPtr manager,int building,int ordinal,int expansion);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate int TerrainEntry(IntPtr manager);
    private sealed class Bytes:CodeWriter{internal readonly List<byte> Data=new List<byte>();public override void WriteByte(byte value)=>Data.Add(value);}
    static IntPtr Code(byte[] bytes){var p=VirtualAlloc(IntPtr.Zero,(UIntPtr)Math.Max(4096,bytes.Length),0x3000,0x40);if(p==IntPtr.Zero)throw new Exception("allocation failed");Marshal.Copy(bytes,0,p,bytes.Length);return p;}
    static byte[] Assemble(Assembler a,ulong address){var w=new Bytes();a.Assemble(w,address);return w.Data.ToArray();}
    public static int Main(string[] args)
    {
        if(args.Length!=2)throw new ArgumentException("fixture directory and native-audit.json required");
        Directory.CreateDirectory(args[0]);Ring();Window();NativeHash(args[1]);InlineMachines();WorkerEntryMachines();RepairCapture();WorkerCapture(6);WorkerCapture(-1);ConcurrentSessions(Path.Combine(args[0],"concurrent"));Packages(Path.Combine(args[0],"packages"));BudgetRecovery(Path.Combine(args[0],"budget"));RollingCheckpoints(Path.Combine(args[0],"rotation"));Retention(Path.Combine(args[0],"retention"));WriteObservations();
        Console.WriteLine("PASS: "+assertions+" assertions; native checksum, six inline generators and worker and terrain entry detours executed.");return 0;
    }
    static void Ring()
    {
        var r=new CaptureRing(256);var e=new CaptureEvent{Kind=RecordKind.Dispatch,Session=1,Tick=17,Phase=0,Opcode=54,Command=123,Stage=4};
        for(int i=0;i<5;i++)r.Append(e,IntPtr.Zero,0,false);
        var b=new byte[256];Check(r.Drain(b)==0,"Light history must stay in RAM before trigger");r.Flush();int n=r.Drain(b);
        Check(n==240,"ring wraps whole records");Check(BitConverter.ToInt64(b,8)==3,"oldest retained sequence");
        Check(BitConverter.ToUInt16(b,4)==2,"binary format distinguishes revised phase/direction semantics");
        Check(BitConverter.ToInt32(b,40)==17&&BitConverter.ToInt32(b,72)==4,"wire header offsets");Check(r.HistoryOverwrites==2&&r.DroppedRecords==0,"expected rolling history distinguished from lost committed data");
        Check(r.Append(e,IntPtr.Zero,1000,true)==0,"oversized record refused without pointer read");
        var random=new Random(12);byte[] input=new byte[131];random.NextBytes(input);
        fixed(byte* p=input)for(int i=0;i<50;i++)
        {
            int size=i%132;r.Append(e,(IntPtr)p,size,true);n=r.Drain(b);
            Check(n==80+size,"variable frame size");Check(b.AsSpan(80,size).SequenceEqual(input.AsSpan(0,size)),"wrapped payload exact");
        }
        var bulk=new CaptureRing(2*1048576);byte[] payload=new byte[1000],scratch=new byte[1048576];
        fixed(byte* p=payload)for(int i=0;i<700;i++)bulk.Append(e,(IntPtr)p,payload.Length,true);
        int first=bulk.Drain(scratch),total=first;Check(first<=256*1024&&first>0,"large writer scratch does not cause a long exclusive ring copy");
        while((n=bulk.Drain(scratch))>0)total+=n;Check(total==700*1080,"bounded drain preserves all complete frames");
    }
    static void Window()
    {
        var w=new DeepWindow(CaptureMode.Deep,100,300);Check(!w.Expire(399),"Deep remains before deadline");Check(w.Expire(400),"deadline inclusive");Check(w.Mode==CaptureMode.Light&&!w.Expire(401),"Deep expiry once and Light continuation");
        var off=new DeepWindow(CaptureMode.Off,0,1);Check(!off.Expire(100)&&off.Mode==CaptureMode.Off,"Off does not activate");
        var disabled=new Recorder(new CaptureOptions{Mode=CaptureMode.Off},"unused",_=>{});disabled.Start(1,new{});
        Check(!disabled.Active&&disabled.Ring.Capacity==256,"Off does not reserve capture budgets or activate a session");
    }
    static void ConcurrentSessions(string root)
    {
        var recorder=new Recorder(new CaptureOptions{LightMiB=1,DeepMiB=2,QueueMiB=1},root,_=>{});
        var barrier=new Barrier(3);Exception failure=null;const int rounds=64;
        ThreadStart produce=()=>{
            for(int round=0;round<rounds;round++)
            {
                barrier.SignalAndWait();
                try{for(int i=0;i<500;i++){bool active=recorder.Active,deep=recorder.IsDeep;recorder.Record(new CaptureEvent{Kind=RecordKind.Fault,Tick=i},IntPtr.Zero,0);}}
                catch(Exception ex){Interlocked.CompareExchange(ref failure,ex,null);}
                barrier.SignalAndWait();
            }
        };
        var a=new Thread(produce);var b=new Thread(produce);a.Start();b.Start();byte[] bytes=new byte[1048576];
        for(int round=0;round<rounds;round++)
        {
            recorder.Start(round+1,new{fixture=true});barrier.SignalAndWait();Thread.Yield();recorder.End();barrier.SignalAndWait();
            bool end=false,valid=true;int size;
            while((size=recorder.Ring.Drain(bytes))>0)
                for(int at=0;at<size;at+=BitConverter.ToInt32(bytes,at))
                {valid&=!end&&BitConverter.ToInt64(bytes,at+16)==round+1;if((RecordKind)BitConverter.ToUInt16(bytes,at+6)==RecordKind.End)end=true;}
            Check(valid&&end,"concurrent capture never publishes a retired session after End");recorder.Pump();
        }
        a.Join();b.Join();barrier.Dispose();Check(failure==null,"Active/IsDeep remain safe during concurrent mission end");
        Check(recorder.RejectedSessions==0,"retired concurrent sessions are reclaimed");
    }
    static void BudgetRecovery(string root)
    {
        var options=new CaptureOptions{LightMiB=4,DeepMiB=4,QueueMiB=1,IncidentMiB=1,SessionBytes=8*1048576};
        var r=new Recorder(options,root,_=>{});r.Start(1,new{fixture=true});
        r.Record(new CaptureEvent{Kind=RecordKind.Dispatch,Phase=0,Opcode=54},IntPtr.Zero,0);
        byte[] fill=new byte[1048576-80];
        fixed(byte* p=fill)r.Record(new CaptureEvent{Kind=RecordKind.Fault},(IntPtr)p,fill.Length);
        Check(!r.Record(new CaptureEvent{Kind=RecordKind.Fault},IntPtr.Zero,0),"incident quota rejects excess repair detail");
        r.Record(new CaptureEvent{Kind=RecordKind.Dispatch,Phase=0,Opcode=67,Stage=1},IntPtr.Zero,0);
        r.Record(new CaptureEvent{Kind=RecordKind.ForcedRun},IntPtr.Zero,0);r.End();r.Pump();
        string folder=Directory.GetDirectories(root).Single();byte[] data=File.ReadAllBytes(Path.Combine(folder,"events.bin"));bool forced=false;
        for(int at=0;at<data.Length;at+=BitConverter.ToInt32(data,at))forced|=(RecordKind)BitConverter.ToUInt16(data,at+6)==RecordKind.ForcedRun;
        Check(forced&&new FileInfo(Path.Combine(folder,"markers.bin")).Length==240,"start and finish survive an exhausted incident budget and later Light events resume");
        var deepOptions=new CaptureOptions{Mode=CaptureMode.Deep,LightMiB=4,DeepMiB=4,QueueMiB=1,IncidentMiB=1,SessionBytes=8*1048576};
        var d=new Recorder(deepOptions,Path.Combine(root,"deep"),_=>{});d.Start(1,new{fixture=true});
        d.Current.Window=new DeepWindow(CaptureMode.Light,0,0); // Deliberately absent DeepEnded frame.
        d.Record(new CaptureEvent{Kind=RecordKind.Dispatch,Phase=0,Opcode=54},IntPtr.Zero,0);byte[] payload=new byte[400000];
        fixed(byte* p=payload)for(int i=0;i<3;i++)d.Record(new CaptureEvent{Kind=RecordKind.Fault},(IntPtr)p,payload.Length);
        var downgraded=d.Current;
        d.End();d.Pump();Check(downgraded.IncidentBudgetDropped>0,"Light incident limit survives a lost DeepEnded marker");
        var reserve=new Recorder(options,Path.Combine(root,"reserved"),_=>{});
        reserve.Start(1,new{fixture=true});
        byte[] ordinary=new byte[512*1024],repair=new byte[512*1024];
        fixed(byte* p=ordinary)for(int i=0;i<12;i++)
        {
            reserve.Record(new CaptureEvent{Kind=RecordKind.Fault},(IntPtr)p,ordinary.Length);
            reserve.Pump();
        }
        Check(reserve.DiskDroppedBytes>0,"ordinary detail reaches its own disk limit");
        reserve.Record(new CaptureEvent{Kind=RecordKind.Dispatch,Phase=0,Opcode=54},IntPtr.Zero,0);
        fixed(byte* p=repair)reserve.Record(new CaptureEvent{Kind=RecordKind.Field},(IntPtr)p,repair.Length);
        reserve.Record(new CaptureEvent{Kind=RecordKind.Dispatch,Phase=0,Opcode=67,Stage=1},IntPtr.Zero,0);
        reserve.End();reserve.Pump();
        byte[] saved=File.ReadAllBytes(Path.Combine(Directory.GetDirectories(Path.Combine(root,"reserved")).Single(),"snapshots.bin"));
        Check(saved.Length==80+repair.Length&&BitConverter.ToUInt16(saved,6)==(ushort)RecordKind.Field,
            "repair detail uses reserved disk space and exports an ordinary format-2 frame");
    }
    static void NativeHash(string auditPath)
    {
        var json=(Dictionary<string,object>)Shared.DependencyFreeJson.Parse(File.ReadAllText(auditPath));
        string hex=(string)json["checksumHelper"];byte[] code=Convert.FromHexString(hex);IntPtr memory=Code(code);
        try
        {
            var native=Marshal.GetDelegateForFunctionPointer<Crc>(memory);var random=new Random(1234);byte[] data=new byte[1168];
            for(int n=0;n<250;n++)
            {
                random.NextBytes(data);int count=n%data.Length;
                fixed(byte* p=data)Check(UnitChecksum.RotateAdd(p,0,count)==native(IntPtr.Zero,count,(IntPtr)p),"native signed byte / rotate semantics");
            }
            byte[] record=new byte[1168],path=new byte[1000];random.NextBytes(record);random.NextBytes(path);
            fixed(byte* p=record)fixed(byte* q=path)
            {
                uint value=UnitChecksum.Compute(p,q);ulong strong=UnitChecksum.Strong(p,q);
                for(int i=0x41c;i<0x424;i++)record[i]^=0xA5;
                Check(value==UnitChecksum.Compute(p,q)&&strong==UnitChecksum.Strong(p,q),"mask ignored without mutating source");
                record[0xB2]^=1;Check(strong!=UnitChecksum.Strong(p,q),"selected state within hashed inputs");
                byte[] original=(byte[])record.Clone();byte[] zeroed=(byte[])record.Clone();Array.Clear(zeroed,0x41c,8);
                fixed(byte* z=zeroed)
                {uint expected=unchecked(native(IntPtr.Zero,0x404,(IntPtr)(z+0x8c))+native(IntPtr.Zero,1000,(IntPtr)q)+*(uint*)(p+0x3c)+*(uint*)(p+0x40)+*(uint*)(p+0x44));Check(expected==UnitChecksum.Compute(p,q),"all exact native unit inputs");}
                Check(record.SequenceEqual(original),"diagnostic hashing never writes live input");
            }
        }
        finally{VirtualFree(memory,UIntPtr.Zero,0x8000);}
    }
    static void RollingCheckpoints(string root)
    {
        var r=new Recorder(new CaptureOptions{LightMiB=1,DeepMiB=2,QueueMiB=1,SessionBytes=2*1048576},root,_=>{});
        var row=new byte[39996];
        r.Start(1,new{fixture=true});
        fixed(byte* p=row)
        {
            for(int i=1;i<=16;i++)
            {
                r.Record(new CaptureEvent{Kind=RecordKind.UnitChecksumTable,Tick=i*200},(IntPtr)p,row.Length);
                if(i==2)
                {
                    r.Record(new CaptureEvent{Kind=RecordKind.Dispatch,Tick=401,Opcode=54},IntPtr.Zero,0);
                    r.Record(new CaptureEvent{Kind=RecordKind.Dispatch,Tick=402,Opcode=67,Stage=1},IntPtr.Zero,0);
                }
                r.Pump();
            }
        }
        string path=Directory.GetDirectories(root).Single();
        Check(File.Exists(Path.Combine(path,"checkpoints.previous.bin")),"rolling storage is written while the game is active");
        r.End();r.Pump();
        byte[] evidence=File.ReadAllBytes(Path.Combine(path,"evidence.bin"));
        Check(evidence.Length==2*(80+row.Length)&&BitConverter.ToInt32(evidence,40)==200,
            "early incident pins the before-state despite later checkpoint rotations");
        Check(Directory.GetFiles(path).Sum(f=>new FileInfo(f).Length)<=2*1048576,"complete package stays below the session budget");
    }
    static void Retention(string root)
    {
        Directory.CreateDirectory(root);
        var now=new DateTime(2026,11,20,14,0,0,DateTimeKind.Utc);
        string Make(int minute,bool ended,int incidents)
        {
            string path=Path.Combine(root,$"20261120-1300{minute:D2}-001-p123-s1");
            Directory.CreateDirectory(path);
            string manifest=Path.Combine(path,"manifest.json");
            File.WriteAllText(manifest,Shared.DependencyFreeJson.Serialize(new{plugin="DesyncTest_Serp",retentionSchema=1,session=1,incidents,ended}));
            File.SetLastWriteTimeUtc(manifest,now.AddHours(-1));
            return path;
        }
        string oldest=Make(1,true,0),control=Make(2,true,0),latest=Make(3,true,0);
        string incident=Make(4,true,1),active=Make(5,false,0),foreign=Make(6,true,0);
        File.WriteAllText(Path.Combine(foreign,"personal.txt"),"untouched");
        CaptureRetention.Prune(root,now,_=>{});
        Check(!Directory.Exists(oldest)&&!Directory.Exists(control)&&!Directory.Exists(latest),"all closed controls are removed");
        Check(Directory.Exists(incident)&&Directory.Exists(active)&&Directory.Exists(foreign),"incident, active and unrecognized packages are preserved");
    }
    static void InlineMachines()
    {
        // Real production emitter and real displaced instructions. Only external/native callees
        // and RIP data are replaced by owned fixtures. Integer nonvolatiles and stack are restored.
        IntPtr state=Marshal.AllocHGlobal(0x400000);new Span<byte>((void*)state,0x400000).Clear();
        IntPtr counter=Marshal.AllocHGlobal(16);new Span<byte>((void*)counter,16).Clear();
        var leaf=new Assembler(64);leaf.mov(rax,(ulong)counter.ToInt64());leaf.inc(__dword_ptr[rax]);leaf.ret();
        IntPtr original=Code(new byte[4096]);byte[] leafBytes=Assemble(leaf,(ulong)original.ToInt64());Marshal.Copy(leafBytes,0,original,leafBytes.Length);
        Marshal.WriteIntPtr(state,original);
        int calls=0;bool forcedCorrect=false;
        var capture=new NativeCapture(new Recorder(new CaptureOptions{Mode=CaptureMode.Off},"unused",_=>{}),IntPtr.Zero,0x88E5000);
        Thunk callback=p=>{Check(p==original,"original handler address preserved");calls++;capture.Dispatch(p);};
        Forced forced=(text,tick,expected,started,now)=>{calls++;forcedCorrect=tick==20&&expected==30&&started==40&&now==50;};
        try
        {
            foreach(var site in NativeHooks.Sites.Skip(NativeHooks.EntrySiteCount))
            {
                var decoder=Decoder.Create(64,new ByteArrayCodeReader(site.Bytes));decoder.IP=0x180000000UL+(uint)site.Rva;
                var instructions=new List<Instruction>();while(decoder.IP<0x180000000UL+(uint)site.Rva+(uint)site.Bytes.Length){decoder.Decode(out var i);instructions.Add(i);}
                for(int j=0;j<instructions.Count;j++)
                {
                    var i=instructions[j];
                    if(i.IsIPRelativeMemoryOperand)i.MemoryDisplacement64=(ulong)state.ToInt64()+0x100;
                    if(i.FlowControl==FlowControl.Call&&site.Rva!=0x1BFAD)i.NearBranch64=(ulong)original.ToInt64();
                    instructions[j]=i;
                }
                var a=new Assembler(64);
                a.push(rbx);a.push(rbp);a.push(rsi);a.push(rdi);a.push(r12);a.push(r13);a.push(r14);a.push(r15);a.sub(rsp,0x28);
                a.mov(rbx,(ulong)state.ToInt64());a.xor(ebp,ebp);a.xor(esi,esi);a.mov(rdi,site.Rva==0x1F88B?0UL:(ulong)state.ToInt64());
                a.mov(r12,(ulong)state.ToInt64());a.mov(r14,(ulong)state.ToInt64());a.mov(r15,site.Rva==0x23B85?0UL:(ulong)state.ToInt64());a.xor(eax,eax);
                a.mov(edx,20);a.mov(r8d,30);a.mov(r9d,40);a.mov(__dword_ptr[rsp+0x20],50);
                NativeHooks.EmitInline(a,instructions.ToArray(),site,(ulong)Marshal.GetFunctionPointerForDelegate(callback).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(forced).ToInt64());
                a.add(rsp,0x28);a.pop(r15);a.pop(r14);a.pop(r13);a.pop(r12);a.pop(rdi);a.pop(rsi);a.pop(rbp);a.pop(rbx);a.ret();
                IntPtr fixture=Code(new byte[4096]);byte[] bytes=Assemble(a,(ulong)fixture.ToInt64());Marshal.Copy(bytes,0,fixture,bytes.Length);
                int before=calls,old=Marshal.ReadInt32(counter);Marshal.GetDelegateForFunctionPointer<FixtureHandler>(fixture)();
                Check(calls==before+1,"one callback per displaced dispatch");
                Check(site.Rva==0x1BFAD?forcedCorrect:Marshal.ReadInt32(counter)>old,"original handler / subsequent callee executed");
                VirtualFree(fixture,UIntPtr.Zero,0x8000);
            }
            GC.KeepAlive(callback);GC.KeepAlive(forced);
        }
        finally{VirtualFree(original,UIntPtr.Zero,0x8000);Marshal.FreeHGlobal(counter);Marshal.FreeHGlobal(state);}
    }
    static void WorkerEntryMachines()
    {
        RunWorkerEntry<WorkerEntry>(NativeHooks.Sites[4],
            "0fb7c24103c04103c103442428c3",
            (callback, expected)=>callback(IntPtr.Zero,15,271,6,2)==expected,294);
        RunWorkerEntry<EntranceEntry>(NativeHooks.Sites[5],
            "8bc24103c04103c1c3",
            (callback, expected)=>callback(IntPtr.Zero,271,3,0)==expected,274);
        IntPtr tiles=VirtualAlloc(IntPtr.Zero,(UIntPtr)(NativeCapture.TerrainPending+sizeof(int)),0x3000,0x04);
        if(tiles==IntPtr.Zero)throw new InvalidOperationException("terrain entry fixture allocation failed");
        try
        {
            RunWorkerEntry<TerrainEntry>(NativeHooks.Sites[6],"b87b0000004881c4b00000005bc3",
                (callback,expected)=>callback(tiles)==expected,123);
        }
        finally{VirtualFree(tiles,UIntPtr.Zero,0x8000);}
    }

    // Copy the audited source spans into guarded executable pages. The synthetic tail
    // returns the sum of the actual ABI arguments without touching a game object.
    static void RunWorkerEntry<T>(NativeHooks.Site site,string tail,Func<T,int,bool> call,int expected) where T:Delegate
    {
        const int page=4096, lookahead=16;
        IntPtr region=VirtualAlloc(IntPtr.Zero,(UIntPtr)(2*page),0x3000,0x40);
        if(region==IntPtr.Zero)throw new InvalidOperationException("entry fixture allocation failed");
        Check(VirtualProtect(region+page,(UIntPtr)page,0x01,out _),"worker entry has a guard page");
        byte[] code=site.Bytes.Concat(Convert.FromHexString(tail)).ToArray();
        IntPtr entry=region+page-site.Bytes.Length-lookahead;
        Marshal.Copy(code,0,entry,code.Length);
        var backend=new NativeDetourBackend(new NativeDetourOptions{AllowedSchemes=DetourScheme.Absolute,FollowCalls=false,FollowJumps=false,
            Chaining=HookChainingPolicy.Refuse,FunctionScanLimit=site.Bytes.Length,SuspendThreadsDuringPatch=false});
        T direct=Marshal.GetDelegateForFunctionPointer<T>(entry);
        var request=new DetourRequest<T>{Name=site.Name,TargetAddress=(ulong)entry.ToInt64(),Callback=direct};
        var hook=(NativeDetour<T>)backend.CreateDetour(in request);
        Check(hook.Scheme==DetourScheme.Absolute&&hook.DisplacedByteCount==site.Bytes.Length&&
              hook.TrampolineAddress!=IntPtr.Zero&&!hook.IsInstalled,"worker entry span prepared with installed backend");
        Check(call(direct,expected)&&call(hook.Original,expected),"worker trampoline preserves register and stack arguments and return");
        hook.Dispose();
        GC.KeepAlive(direct);
        VirtualFree(region,UIntPtr.Zero,0x8000);
    }
    static void WriteObservations()
    {
        const int size=0x88E5000;
        IntPtr image=VirtualAlloc(IntPtr.Zero,(UIntPtr)size,0x3000,0x04);
        if(image==IntPtr.Zero)throw new InvalidOperationException("observation fixture allocation");
        try
        {
            var r=new Recorder(new CaptureOptions{LightMiB=1,DeepMiB=2,QueueMiB=1,WorkerPlayer=-1},"unused",_=>{});
            var capture=new NativeCapture(r,image,size);
            var b=(byte*)image;var tile=b+NativeCapture.TileManagerRva;
            var network=b+NativeCapture.Manager;
            var building=b+NativeCapture.BuildingRecords+11*NativeCapture.BuildingRecordSize;
            var units=b+0x67E8400;
            var unit=(byte*)capture.ReadWorker(1);
            *(int*)units=3;*(short*)(unit+0x88)=2;*(short*)(unit+0x8A)=1;*(short*)(unit+0x92)=6;
            Check(capture.FindWorkerCandidate((IntPtr)units,6)==1,"first eligible worker is found");
            *(short*)(unit+0x29C)=1;
            Check(capture.FindWorkerCandidate((IntPtr)units,6)==0,"killed unit is excluded");
            r.Start(1,new{fixture=true});
            *(int*)(tile+NativeCapture.TerrainPending)=1;
            *(int*)(tile+0x6400)=2;
            *(int*)(b+0x402FF28)=30;
            tile[0xA20D40+31]=1;
            Check(capture.BeginTerrain((IntPtr)tile),"native dirty terrain at entry captured");
            *(int*)(tile+0x898400+31*4)=0x8000;
            capture.EndTerrain((IntPtr)tile,true);
            *(ushort*)(building+0xD0)=2;*(int*)(building+0xD8)=1001;
            network[0xB0C00]=106;
            FixtureHandler command=()=>{*(ushort*)(building+0x192)=55;*(int*)(b+0x32DE440+0xA4)=4;};
            capture.Dispatch(Marshal.GetFunctionPointerForDelegate(command));
            GC.KeepAlive(command);
            r.End();
            var bytes=new byte[1048576];
            int sizeRead=r.Ring.Drain(bytes),deltas=0;
            for(int at=0;at<sizeRead;at+=BitConverter.ToInt32(bytes,at))
            {
                var kind=(RecordKind)BitConverter.ToUInt16(bytes,at+6);
                if(kind==RecordKind.TerrainDelta)
                {
                    Check(BitConverter.ToInt32(bytes,at+64)==31&&BitConverter.ToInt32(bytes,at+80)==0&&
                        BitConverter.ToInt32(bytes,at+84)==0x8000,"only the marked tile contains the observed transition");
                    deltas++;
                }
                if(kind==RecordKind.CommandBuildingDelta)
                {
                    int field=BitConverter.ToInt32(bytes,at+72);
                    Check(field==0?BitConverter.ToInt32(bytes,at+80+18*4)==55:
                        BitConverter.ToInt32(bytes,at+88)==4,"executed handler observed before and after with matching command context");
                    deltas++;
                }
            }
            Check(deltas==3,"terrain, building and vegetation deltas survive in Light before any resync");
        }
        finally{VirtualFree(image,UIntPtr.Zero,0x8000);}
    }

    static void Packages(string root)
    {
        var options=new CaptureOptions{LightMiB=1,DeepMiB=2,QueueMiB=1,SessionBytes=1048576};
        var r=new Recorder(options,root,_=>{});r.Start(1,new{nativeHash="fixture",managedHash="fixture",startKind="test",file="fixture",plugins=new object[0]});
        r.Record(new CaptureEvent{Kind=RecordKind.Dispatch,Tick=20,Phase=0,Opcode=54},IntPtr.Zero,0);
        r.Record(new CaptureEvent{Kind=RecordKind.Dispatch,Tick=20,Phase=0,Opcode=67,Stage=1},IntPtr.Zero,0);r.End();r.Pump();
        string folder=Directory.GetDirectories(root).Single();Check(File.Exists(Path.Combine(folder,"events.bin")),"packet written");
        var m=(Dictionary<string,object>)Shared.DependencyFreeJson.Parse(File.ReadAllText(Path.Combine(folder,"manifest.json")));
        Check((bool)m["ended"]&&(bool)m["complete"],"session completion after successful flush");
        Check(Convert.ToInt32(m["format"])==2,"manifest declares the same format as the binary frames");
        Check(File.ReadAllText(Path.Combine(folder,"summary.txt")).Contains("opcode=54"),"readable summary contains observed resync start, not just static instructions");
        r.Start(2,new{nativeHash="fixture"});
        r.Record(new CaptureEvent{Kind=RecordKind.Checksum,Tick=200},IntPtr.Zero,0);r.Pump();
        string control=r.Current.Folder;
        Check(File.Exists(Path.Combine(control,"manifest.json")),"a running control exposes live checkpoints");
        r.End();r.Pump();
        Check(!Directory.Exists(control)&&Directory.Exists(folder),"closing a control removes its files and keeps the incident");
        r.Start(4,new{nativeHash="fixture"});
        string shortControl=r.Current.Folder;
        r.End();r.Pump();
        Check(!Directory.Exists(shortControl),"a control ending before the first writer pass creates no files");
        var missed=new Recorder(options,Path.Combine(root,"missed-marker"),_=>{});
        missed.Start(1,new{nativeHash="fixture"});
        string missedPath=missed.Current.Folder;
        var gate=typeof(Recorder).GetField("captureGate",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(missed);
        bool markerWritten=true;
        lock(gate)
        {
            var producer=new Thread(()=>markerWritten=missed.Record(new CaptureEvent{Kind=RecordKind.Dispatch,Phase=0,Opcode=54},IntPtr.Zero,0));
            producer.Start();producer.Join();
        }
        missed.End();missed.Pump();
        Check(!markerWritten&&Directory.Exists(missedPath),"a detected resync retains evidence even if its marker was dropped by contention");
        // Disk exhaustion stays bounded and produces an incomplete package, without retrying gameplay.
        r.Start(3,new{nativeHash="fixture"});byte[] payload=new byte[400000];
        r.Record(new CaptureEvent{Kind=RecordKind.Dispatch,Tick=20,Phase=0,Opcode=54},IntPtr.Zero,0);
        fixed(byte* p=payload)
        {
            for(int i=0;i<4;i++){r.Record(new CaptureEvent{Kind=RecordKind.Fault},(IntPtr)p,payload.Length);r.Pump();}
        }
        r.End();r.Pump();
        string last=Directory.GetDirectories(root).Single(v=>v.EndsWith("-s3"));
        var limited=(Dictionary<string,object>)Shared.DependencyFreeJson.Parse(File.ReadAllText(Path.Combine(last,"manifest.json")));
        Check(!(bool)limited["complete"]&&r.DiskDroppedBytes>0,"disk limit explicitly incomplete");
        Check(new FileInfo(Path.Combine(last,"events.bin")).Length+new FileInfo(Path.Combine(last,"snapshots.bin")).Length<=options.SessionBytes,"data bytes remain bounded");
        string blocked=Path.Combine(root,"not-a-directory");File.WriteAllText(blocked,"fixture");
        var bad=new Recorder(options,blocked,_=>{});
        for(int id=1;id<=10;id++){bad.Start(id,new{});bad.Pump();bad.End();bad.Pump();}
        Check(bad.WriterFailures==10&&bad.RejectedSessions==0&&bad.DiskDroppedBytes>0,"unwritable output cannot strand sessions or suppress later draining");
        var deep=new Recorder(new CaptureOptions{Mode=CaptureMode.Deep,LightMiB=1,DeepMiB=2,QueueMiB=1},Path.Combine(root,"deep"),_=>{});
        deep.Start(1,new{fixture=true});deep.Current.Window=new DeepWindow(CaptureMode.Deep,0,0);deep.Pump();
        Check(!deep.IsDeep&&deep.Active,"Deep expires while simulation is idle, Light remains active");
        deep.Record(new CaptureEvent{Kind=RecordKind.Dispatch,Tick=123,Phase=0,Opcode=54},IntPtr.Zero,0);deep.End();deep.Pump();
        string deepFolder=Directory.GetDirectories(Path.Combine(root,"deep")).Single();
        byte[] frames=File.ReadAllBytes(Path.Combine(deepFolder,"events.bin")).Concat(File.ReadAllBytes(Path.Combine(deepFolder,"markers.bin"))).ToArray();int endings=0;bool continuation=false;
        for(int at=0;at<frames.Length;at+=BitConverter.ToInt32(frames,at))
        {if((RecordKind)BitConverter.ToUInt16(frames,at+6)==RecordKind.DeepEnded)endings++;if(BitConverter.ToInt32(frames,at+40)==123)continuation=true;}
        Check(endings==1&&continuation,"one Deep-end block and later Light evidence survive finalization");
    }
    static void WorkerCapture(int workerPlayer)
    {
        const int size=0x88E5000;
        IntPtr image=VirtualAlloc(IntPtr.Zero,(UIntPtr)size,0x3000,0x04);
        if(image==IntPtr.Zero)throw new Exception("fixture image allocation");
        try
        {
            var recorder=new Recorder(new CaptureOptions{LightMiB=1,DeepMiB=2,QueueMiB=1,WorkerPlayer=workerPlayer},"unused",_=>{});
            recorder.Start(1,new{fixture=true});
            var capture=new NativeCapture(recorder,image,size);
            byte* b=(byte*)image;
            *(uint*)(b+0x67E8400)=3;
            var unit=(SHCDESE.Interop.GameUnit*)(b+NativeCapture.WireUnits+4+UnitChecksum.RecordSize);
            unit->r_GlobalId=70;
            unit->r_ControllableForPlayerId=6;
            unit->r_AliveState=SHCDESE.Interop.Enums.AliveState.IsAlive;
            unit->r_UnitChimp=SHCDESE.Interop.eChimps.CHIMP_TYPE_PEASANT;
            unit[1]=*unit;
            unit[1].r_ControllableForPlayerId=5;
            unit[1].r_GlobalId=90;
            capture.Sample();
            unit->r_CurrentTilePositionX=21;
            *(int*)(b+NativeCapture.TickRva)=1;
            capture.Sample(); // Movement alone must not emit an assignment event.
            unit->r_TransformIntoUnitOfType=SHCDESE.Interop.eChimps.CHIMP_TYPE_MILLER;
            unit->r_LinkedProductionBuildingGlobalId=111;
            *(int*)(b+NativeCapture.TickRva)=2;
            capture.Sample();capture.Sample();
            unit->r_GlobalId=71;
            unit->r_UnitChimp=SHCDESE.Interop.eChimps.CHIMP_TYPE_MILLER;
            *(int*)(b+NativeCapture.TickRva)=3;
            capture.Sample();recorder.End();
            var buffer=new byte[1048576];int sizeRead=recorder.Ring.Drain(buffer);
            var records=new List<(int Tick,int Stage,int Global,int Target,long Tile)>();
            int otherPlayerRecords=0;
            for(int at=0;at<sizeRead;at+=BitConverter.ToInt32(buffer,at))
            {
                if((RecordKind)BitConverter.ToUInt16(buffer,at+6)!=RecordKind.WorkerAssignment)continue;
                if(BitConverter.ToInt32(buffer,at+64)==2)
                {
                    Check(workerPlayer==-1&&BitConverter.ToInt32(buffer,at+52)==5,"all-player trace preserves the actual player ID");
                    otherPlayerRecords++;
                    continue;
                }
                Check(BitConverter.ToInt32(buffer,at+64)==1&&BitConverter.ToInt32(buffer,at+76)==28,"worker player filter and record size");
                records.Add((BitConverter.ToInt32(buffer,at+40),BitConverter.ToInt32(buffer,at+72),
                    BitConverter.ToInt32(buffer,at+80),BitConverter.ToInt32(buffer,at+84),BitConverter.ToUInt16(buffer,at+100)));
            }
            Check(records.SequenceEqual(new (int,int,int,int,long)[]{(0,0,70,0,0),(2,1,70,111,21),(3,2,70,111,21),(3,0,71,111,21)}),
                "Light traces assignment changes and slot reuse without emitting movement or repeated tick events");
            Check(otherPlayerRecords==(workerPlayer==-1?1:0),"optional coverage is not tied to player 6");
        }
        finally{VirtualFree(image,UIntPtr.Zero,0x8000);}
    }
    static void RepairCapture()
    {
        int size=0x88E5000;IntPtr image=VirtualAlloc(IntPtr.Zero,(UIntPtr)size,0x3000,0x04);if(image==IntPtr.Zero)throw new Exception("fixture image allocation");
        try
        {
            byte* b=(byte*)image,m=b+NativeCapture.Manager;
            byte* unit=b+NativeCapture.WireUnits+13*1168;byte* path=b+NativeCapture.Paths+13*1000;
            *(int*)(b+NativeCapture.TickRva)=200;*(int*)(m+0x36FA4C)=2;*(int*)(m+0x36FA50)=1;*(int*)(m+0x84CC8)=0;*(int*)(m+0xB0C04)=123;*(int*)(m+0xDAC)=1;
            *(int*)(m+0xB0BF8)=200;*(sbyte*)(m+0xB0C00)=56;*(int*)(unit+0x98)=1001;*(ushort*)(unit+0x34)=77;
            *(int*)(m+0x84CD8)=13;
            for(int i=0;i<1168;i++)m[0x84CD8+4+i]=0;
            *(int*)(m+0x84CD8+4+0x98)=1002;
            *(ushort*)(m+0x84CD8+4+0x34)=5;
            for(int i=0;i<1000;i++)m[0x84CD8+4+1168+i]=9;
            NativeCapture capture=null;int calls=0;bool packing=false;
            FixtureHandler original=()=>{
                calls++;*(int*)(m+0x370BF8)=0;
                if(packing)
                {
                    *(int*)(m+0x84CCC)=0; // Actual Chore-56 pack path changes phase before fields.
                    capture.BeforeField((IntPtr)m,(IntPtr)(m+0x14D00C),4,0,0);return;
                }
                capture.BeforeField((IntPtr)m,(IntPtr)(m+0x14D00C),4,0,1);
                *(int*)(m+0x14D00C)=*(int*)(m+0x84CD8);
                ushort hover=*(ushort*)(unit+0x34);*(ushort*)(unit+0x34)=0;
                *(int*)(m+0x370BF8)=4;capture.BeforeField((IntPtr)m,(IntPtr)unit,1168,0,1);
                Buffer.MemoryCopy(m+0x84CD8+4,unit,1168,1168);
                *(int*)(m+0x370BF8)=1172;capture.BeforeField((IntPtr)m,(IntPtr)path,1000,0,1);
                Buffer.MemoryCopy(m+0x84CD8+1172,path,1000,1000);
                *(ushort*)(unit+0x34)=hover;
            };
            // Match the Extender's use of an independently typed managed thunk in the live table.
            *(long*)(b+0x2C7A30+106*8)=Marshal.GetFunctionPointerForDelegate(original).ToInt64();
            var recorder=new Recorder(new CaptureOptions{LightMiB=1,DeepMiB=2,QueueMiB=1},"unused",_=>{});
            recorder.Start(1,new{fixture=true});capture=new NativeCapture(recorder,image,size);
            byte* arrival=stackalloc byte[8];arrival[0]=36;arrival[1]=0x23;arrival[2]=1;arrival[3]=2;*(int*)(arrival+4)=991;
            capture.Arrival(2,(IntPtr)arrival,8);
            capture.Dispatch((IntPtr)(*(long*)(b+0x2C7A30+106*8)));
            uint* checksums=(uint*)(m+0x14F0CC)+10000;
            uint* buildingChecks=(uint*)(m+NativeCapture.BuildingChecksumsOffset)+4000;
            buildingChecks[0]=7;buildingChecks[2]=553;
            byte* building=b+NativeCapture.BuildingRecords+2*NativeCapture.BuildingRecordSize;
            *(uint*)(building+0xD8)=666;
            checksums[0]=99;checksums[1]=21;checksums[13]=42;checksums[9999]=84;
            capture.Checksum(true,0);capture.Checksum(true,1);capture.Checksum(false,0);
            checksums[13]=0; // The retained copy must survive Vanilla overwriting its table.
            Check(calls==1,"native handler invoked once during repair capture");Check(*(int*)(unit+0x98)==1002&&*(ushort*)(unit+0x34)==77,"original replacement and preserved local field");
            packing=true;*(int*)(m+0x84CCC)=1;*(int*)(m+0xB0BFC)=2;*(int*)(m+0x36FA4C)=7;
            capture.Dispatch((IntPtr)(*(long*)(b+0x2C7A30+106*8)));Check(calls==2,"pack original also runs exactly once");
            recorder.End();var data=new byte[1048576];var priority=new byte[1048576];
            int count=recorder.Ring.Drain(data),priorityCount=recorder.Checkpoints.Drain(priority);
            Buffer.BlockCopy(priority,0,data,count,priorityCount);count+=priorityCount;
            bool old=false,final=false,replacement=false,received=false,regularChecksum=false,resyncChecksum=false,packed=false;
            int tables=0,buildingTables=0,buildingRows=0;
            for(int at=0;at<count;at+=BitConverter.ToInt32(data,at))
            {
                if((RecordKind)BitConverter.ToUInt16(data,at+6)==RecordKind.Checksum)
                {regularChecksum|=BitConverter.ToInt32(data,at+44)==10;resyncChecksum|=BitConverter.ToInt32(data,at+44)==13;}
                if((RecordKind)BitConverter.ToUInt16(data,at+6)==RecordKind.UnitChecksumTable)
                {
                    tables++;
                    Check(BitConverter.ToInt32(data,at+76)==39996&&BitConverter.ToUInt32(data,at+80)==21&&
                        BitConverter.ToUInt32(data,at+80+12*4)==42&&BitConverter.ToUInt32(data,at+80+9998*4)==84,
                        "Light copies exactly the local table, excluding slot zero, without retaining a native pointer");
                }
                if((RecordKind)BitConverter.ToUInt16(data,at+6)==RecordKind.BuildingChecksumTable)
                {
                    buildingTables++;
                    Check(BitConverter.ToInt32(data,at+76)==15996&&BitConverter.ToUInt32(data,at+80)==0&&
                        BitConverter.ToUInt32(data,at+84)==553,"building tables retain the slot 2 checksum without the reserved slot");
                }
                if((RecordKind)BitConverter.ToUInt16(data,at+6)==RecordKind.BuildingState)
                {
                    buildingRows++;
                    Check(BitConverter.ToInt32(data,at+64)==2&&BitConverter.ToInt32(data,at+56)==553&&
                        BitConverter.ToUInt32(data,at+80+0xD8)==666,"building raw data is tagged with the same checkpoint, slot and checksum");
                }
                if((RecordKind)BitConverter.ToUInt16(data,at+6)==RecordKind.Arrival)
                {received=true;Check(BitConverter.ToInt32(data,at+40)==-1&&BitConverter.ToInt32(data,at+56)==991&&BitConverter.ToInt32(data,at+60)==0x20123,"arrival uses normal wire identity and uint24 planned tick, without simulation access");}
                if((RecordKind)BitConverter.ToUInt16(data,at+6)==RecordKind.Field&&BitConverter.ToInt32(data,at+72)==4)
                {replacement=true;Check(BitConverter.ToUInt16(data,at+80+0x34)==5,"incoming replacement preserved before copy and local field restoration");}
                if((RecordKind)BitConverter.ToUInt16(data,at+6)==RecordKind.Field&&BitConverter.ToInt32(data,at+44)==1)
                {packed=true;Check(BitConverter.ToInt32(data,at+52)==2&&BitConverter.ToInt32(data,at+68)==0,"field keeps pack entry source and explicit output direction after native phase mutation");}
                if((RecordKind)BitConverter.ToUInt16(data,at+6)!=RecordKind.Unit)continue;
                int stage=BitConverter.ToInt32(data,at+72);
                if(stage==0){old=true;Check(BitConverter.ToInt32(data,at+80+0x98)==1001&&BitConverter.ToUInt16(data,at+80+0x34)==77,"capture old slot after decoding wire ID, before any preservation writes");}
                if(stage==4){final=true;Check(BitConverter.ToInt32(data,at+80+0x98)==1002&&BitConverter.ToUInt16(data,at+80+0x34)==77,"capture final slot identity after preservation restoration");}
            }
            Check(old&&final&&replacement&&received,"old, incoming and final repair endpoints plus arrival present");Check(recorder.CallbackFailures==0,"valid fixture produces no callback failure");GC.KeepAlive(original);
            Check(regularChecksum&&resyncChecksum,"regular and resync checksums use distinct phases even at the same tick");
            Check(tables==2,"Light records both actual checksum calls and skips the guarded call");
            Check(buildingTables==2&&buildingRows==1,"Light stores building detail only at the regular checkpoint");
            Check(packed,"pack fields are not misreported as received execution fields");
        }
        finally{VirtualFree(image,UIntPtr.Zero,0x8000);}
    }
}
