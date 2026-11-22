using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using AIMarketOverbuyDiagnostic;
using BepInEx.Logging;
using RedBird.Backends.NativeX64;
using RedBird.Core.Memory;
using Shared;
using Iced.Intel;

internal static class WriterRelayTests
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void NativeCall();
    private static readonly List<object> roots = new List<object>();
    // Snapshot register order from the production relay (architectural register IDs).
    private static readonly int[] Order = { 15,14,13,12,11,10,9,8,7,6,5,3,2,1,0 };

    internal static void Run(byte[] nativeDll, ManualLogSource log, Action<bool,string> check)
    {
        byte[][] invalidSources={
            new byte[]{0x89,0x84,0x24,0,0,0,0}, // stack-relative store
            new byte[]{0x89,0x05,0,0,0,0}, // RIP-relative store
            new byte[]{0x8B,0x83,0,0,0,0}, // load instead of store
            new byte[]{0x89,0xA3,0,0,0,0}, // ESP source
            new byte[]{0x89,0x83,0,0,0,0,0x90} // more than one instruction
        };
        foreach(byte[] invalid in invalidSources)
        {
            bool rejected=false;try{MarketDemandWriterHooks.GenerateSource(1,2,3,4,5,6,7,invalid);}
            catch(ArgumentException){rejected=true;}
            check(rejected,"paired source rejects a non-single/non-plain/RSP/RIP dword store");
        }
        for (int site=0; site<MarketDemandSites.All.Length; site++) RunSite(nativeDll, log, check, site);
        for (int site=0; site<MarketDemandSites.All.Length; site++) RunSite(nativeDll, log, check, site,true);
        RunConditionalBoundary(nativeDll,log,check);
        RunMultiplyBoundary(nativeDll,log,check);
        RunRecountMarker(nativeDll,log,check,false);
        RunRecountMarker(nativeDll,log,check,true);
        RunRecountMarker(nativeDll,log,check,false,0xB8122);
    }

    private static void RunSite(byte[] dll, ManualLogSource log, Action<bool,string> check, int site,bool pairedSource=false)
    {
        MarketDemandSite map=MarketDemandSites.All[site];byte[] store=map.Bytes;int rva=map.Rva;
        VerifyMap(map,check);
        byte[] actual=new byte[store.Length];Buffer.BlockCopy(dll,FileOffset(dll,rva),actual,0,actual.Length);
        check(actual.SequenceEqual(store),"writer "+site+": real native store bytes match");
        int scan=NativeDetourBackend.Instance.Options.FunctionScanLimit+16;
        int pages=(scan+4095)/4096;
        IntPtr region=Alloc((pages+1)*4096),copy=region+pages*4096-scan;
        if(!VirtualProtect(region+pages*4096,(UIntPtr)4096,1,out _))throw new Exception("Writer guard page failed.");
        byte[] fixture=new byte[scan];Buffer.BlockCopy(dll,FileOffset(dll,rva),fixture,0,scan);
        // Continuation increments exactly once, preserving the state being tested.
        IntPtr count=Alloc(4096),target=count+16,gate=count+32,correctionGate=count+36;
        var continuation=new List<byte>{0x9C,0x50,0x48,0xB8};continuation.AddRange(BitConverter.GetBytes(count.ToInt64()));
        continuation.AddRange(new byte[]{0xFF,0x00,0x58,0x9D,0xC3});
        Buffer.BlockCopy(continuation.ToArray(),0,fixture,store.Length,continuation.Count);Marshal.Copy(fixture,0,copy,scan);
        ulong module=unchecked((ulong)(copy.ToInt64()-rva));var image=new byte[rva+scan];
        Buffer.BlockCopy(fixture,0,image,rva,scan);
        NativePatchGuard.Bind(ScanRegion.FromMemory(image,module,"writer private load image"),log);
        ulong[] expected=new ulong[16];for(int reg=0;reg<16;reg++)expected[reg]=0x1122334400000000UL+(uint)reg;
        if(map.Index>=0)expected[Order[map.Index]]=0;
        expected[Order[map.Base]]=unchecked((ulong)target.ToInt64()-(uint)map.Displacement);
        if(!map.Immediate)expected[Order[map.Value]]=0x12345678;
        int callbacks=0,postCallbacks=0,expectedPreCell=0,sourceMode=0;
        int emitted=map.Immediate?map.Value:0x12345678;
        IntPtr clobber=Alloc(4096);var clobberBytes=new List<byte>();
        for(int xmm=0;xmm<6;xmm++)clobberBytes.AddRange(new byte[]{0x66,0x0F,0xEF,(byte)(0xC0+xmm*9)});
        clobberBytes.AddRange(new byte[]{0xF9,0x48,0x31,0xC0,0xC3});Write(clobber,clobberBytes);
        NativeCall disturb=Marshal.GetDelegateForFunctionPointer<NativeCall>(clobber);roots.Add(disturb);
        MarketDemandWriterHooks.Observer observer=(snapshot,index)=>{
            // Deliberately perturb native volatile SSE/flags after checking input.
            try {
                bool after=pairedSource&&index==site+256;
                if(after)postCallbacks++;else callbacks++;
                check(index==(after?site+256:site),"writer "+site+": site index");
                if(pairedSource)
                {
                    check(Marshal.ReadInt32(target)==(after?emitted:expectedPreCell),"paired source "+site+": "+(after?"after sees original emitted quantity":"before sees untouched old pending"));
                    if(after)Marshal.WriteInt32(target,emitted-1);
                    else if(sourceMode==3)Marshal.WriteInt32(correctionGate,0); // Owner can disable correction in its pre observer.
                }
                for(int i=0;i<Order.Length;i++)check(unchecked((ulong)Marshal.ReadInt64(snapshot,i*8))==expected[Order[i]],"writer "+site+": observed GPR "+Order[i]);
                disturb();}
            catch(Exception error){check(false,"Writer observer test failed: "+error);}
        };
        byte[] changed=(byte[])store.Clone();changed[changed.Length-1]^=1;
        bool missingRawRejected=false;
        try { AuditedEntryDetour.PrepareNativeTarget(image,module,rva,store,(NativeCall)(()=>{}),IntPtr.Zero,"writer-null-native-target"); }
        catch(ArgumentNullException){missingRawRejected=true;}
        check(missingRawRejected,"writer "+site+": absent raw target cannot fall back to a managed placeholder");
        bool rejected=false;try{MarketDemandWriterHooks.Prepare(image,module,rva,changed,site,gate,observer);}
        catch(InvalidOperationException error){rejected=error.Message.Contains("load image differs");}
        check(rejected,"writer "+site+": raw intermediary preparation rejects changed pristine bytes");
        var occupied=(byte[])store.Clone();occupied[1]^=1;Marshal.Copy(occupied,0,copy,occupied.Length);
        rejected=false;try{MarketDemandWriterHooks.Prepare(image,module,rva,store,site,gate,observer);}
        catch(InvalidOperationException error){rejected=error.Message.Contains("NATIVE_FOREIGN_PATCH:");}
        var unchanged=new byte[occupied.Length];Marshal.Copy(copy,unchanged,0,unchanged.Length);
        check(rejected&&unchanged.SequenceEqual(occupied),"writer "+site+": raw intermediary preparation refuses foreign patch without overwriting it");
        Marshal.Copy(store,0,copy,store.Length);
        var hook=pairedSource?MarketDemandWriterHooks.PrepareSource(image,module,rva,store,site,gate,observer,correctionGate):
            MarketDemandWriterHooks.Prepare(image,module,rva,store,site,gate,observer);
        check(hook.detour.HookEntryPointAddress==hook.page&&hook.detour.OriginalEntryPointAddress==hook.detour.TrampolineAddress&&
            Marshal.GetFunctionPointerForDelegate(hook.relay)!=hook.page,
            "writer "+site+": public intermediary explicitly selects native relay, bypassing managed placeholder address");
        roots.Add(hook);hook.Publish();check(hook.IsPublished&&hook.IntegrityReady,"writer "+site+": real backend published and watched");
        for(int alignment=0;alignment<2;alignment++)
        {
            IntPtr result=Alloc(4096),hostFx=Alloc(4096),sse=Alloc(4096),harness=Alloc(4096);
            byte[] sseBytes=Enumerable.Range(0,256).Select(i=>(byte)(i*37+site)).ToArray();Marshal.Copy(sseBytes,0,sse,256);
            Write(harness,Harness(unchecked((ulong)harness.ToInt64()),copy,result,hostFx,sse,expected,alignment));
            NativeCall call=Marshal.GetDelegateForFunctionPointer<NativeCall>(harness);roots.Add(call);
            for(int mode=0;mode<(pairedSource?4:2);mode++)
            {
                int active=mode==0?0:1,post=pairedSource&&mode==2?1:0;sourceMode=mode;
                callbacks=postCallbacks=0;expectedPreCell=0x23456789;
                Marshal.WriteInt32(count,0);Marshal.WriteInt32(target,expectedPreCell);Marshal.WriteInt32(gate,active);
                Marshal.WriteInt32(correctionGate,mode>=2?1:0);call();
                string label=(pairedSource?"paired source ":"writer ")+site+" mode="+mode+" alignment="+alignment;
                check(callbacks==active&&postCallbacks==post&&Marshal.ReadInt32(count)==1,label+": callback gating and original continuation once");
                check(Marshal.ReadInt32(target)==emitted-post,label+": original MOV once; optional post correction survives continuation");
                for(int i=0;i<Order.Length;i++)check(unchecked((ulong)Marshal.ReadInt64(result,i*8))==expected[Order[i]],label+": preserved GPR "+Order[i]);
                // Arithmetic status bits deliberately include CF,PF,AF,ZF,SF,OF.
                check((Marshal.ReadInt64(result,120)&0x8D5)==(0xAD7&0x8D5),label+": preserved native arithmetic flags");
                byte[] observedSse=new byte[256];Marshal.Copy(result+128+160,observedSse,0,256);
                check(observedSse.SequenceEqual(sseBytes),label+": all16 SSE registers preserved despite observer clobber");
                expectedPreCell=Marshal.ReadInt32(target);call();
                check(callbacks==active*2&&postCallbacks==post*2&&Marshal.ReadInt32(count)==2,label+": repeated renewal retains before/store/after ordering");
            }
        }
        Marshal.WriteInt32(gate,0); // Published code remains intact and dormant.
    }

    private static byte[] Harness(ulong start,IntPtr site,IntPtr result,IntPtr hostFx,IntPtr sse,ulong[] values,int alignment,int flags=0xAD7,bool seedOriginalStack=false)
    {
        var b=new List<byte>();PushAll(b);Mov(b,0,unchecked((ulong)hostFx.ToInt64()));b.AddRange(new byte[]{0x48,0x0F,0xAE,0x00});
        Mov(b,0,unchecked((ulong)sse.ToInt64()));
        for(int xmm=0;xmm<16;xmm++) {b.Add(0xF3);if(xmm>=8)b.Add(0x44);b.AddRange(new byte[]{0x0F,0x6F,(byte)(0x80+((xmm&7)<<3))});b.AddRange(BitConverter.GetBytes(xmm*16));}
        foreach(int reg in Order)Mov(b,reg,values[reg]);
        if(alignment!=0)b.AddRange(new byte[]{0x48,0x83,0xEC,0x08});
        if(seedOriginalStack)
        {
            // Reserve scratch above the future site's return address. Never alter
            // host nonvolatile-register saves when seeding [original RSP+28h].
            b.AddRange(new byte[]{0x48,0x81,0xEC,0x80,0,0,0,0x50});
            Mov(b,0,0xA1B2C3D4E5F60718UL);
            b.AddRange(new byte[]{0x48,0x89,0x44,0x24,0x28,0x58});
        }
        b.Add(0x68);b.AddRange(BitConverter.GetBytes(flags));b.Add(0x9D);
        b.AddRange(new byte[]{0xFF,0x15});b.AddRange(BitConverter.GetBytes(checked((int)(0xE00-b.Count-4))));
        PushAll(b); // RSI points to the just-pushed image after native registers are safe.
        b.AddRange(new byte[]{0x48,0x89,0xE6});Mov(b,7,unchecked((ulong)result.ToInt64()));
        // At this point RSP is the after-call snapshot start. The site's native
        // RSP was 120 bytes above it (128-byte image, minus pushed return).
        b.AddRange(new byte[]{0x48,0x89,0xE0,0x48,0x83,0xC0,0x78,0x48,0x89,0x87,0x00,0x03,0x00,0x00});
        Mov(b,1,16);
        b.AddRange(new byte[]{0xFC,0xF3,0x48,0xA5,0x48,0x81,0xC4,0x80,0,0,0});
        Mov(b,0,unchecked((ulong)(result+128).ToInt64()));b.AddRange(new byte[]{0x48,0x0F,0xAE,0x00});
        Mov(b,0,unchecked((ulong)hostFx.ToInt64()));b.AddRange(new byte[]{0x48,0x0F,0xAE,0x08});
        if(seedOriginalStack)b.AddRange(new byte[]{0x48,0x81,0xC4,0x80,0,0,0});
        if(alignment!=0)b.AddRange(new byte[]{0x48,0x83,0xC4,0x08});
        PopAll(b);b.Add(0xC3);
        while(b.Count<0xE00)b.Add(0xCC);b.AddRange(BitConverter.GetBytes(site.ToInt64()));return b.ToArray();
    }
    private static void RunRecountMarker(byte[] dll,ManualLogSource log,Action<bool,string>check,bool stackMarker,int overrideRva=0)
    {
        bool lossMarker=overrideRva==0xB8122;
        int rva=overrideRva!=0?overrideRva:stackMarker?0xB9A30:0xC1F4A;
        byte[] entry=lossMarker?new byte[]{0x45,0x89,0x94,0x8E,0xA0,0xB2,0x79,0x03}:
            stackMarker?new byte[]{0x48,0x8B,0x4C,0x24,0x28,0x48,0x33,0xCC}:
            new byte[]{0x42,0x89,0xAC,0x36,0xCC,0xF0,0x12,0x00};
        int scan=NativeDetourBackend.Instance.Options.FunctionScanLimit+16,pages=(scan+4095)/4096;
        IntPtr region=Alloc((pages+1)*4096),copy=region+pages*4096-scan;
        if(!VirtualProtect(region+pages*4096,(UIntPtr)4096,1,out _))throw new Exception("Recount marker guard page failed");
        var bytes=new byte[scan];Buffer.BlockCopy(dll,FileOffset(dll,rva),bytes,0,scan);
        string name="recount marker "+rva.ToString("X");check(bytes.Take(entry.Length).SequenceEqual(entry),name+": audited real bytes");
        IntPtr data=Alloc(4096),count=data,target=data+16,gate=data+32;
        var suffix=new List<byte>{0x9C,0x50,0x48,0xB8};suffix.AddRange(BitConverter.GetBytes(count.ToInt64()));
        suffix.AddRange(new byte[]{0xFF,0x00,0x58,0x9D,0xC3});Buffer.BlockCopy(suffix.ToArray(),0,bytes,entry.Length,suffix.Count);
        Marshal.Copy(bytes,0,copy,bytes.Length);ulong module=unchecked((ulong)(copy.ToInt64()-rva));var image=new byte[rva+scan];Buffer.BlockCopy(bytes,0,image,rva,scan);
        NativePatchGuard.Bind(ScanRegion.FromMemory(image,module,name+" private image"),log);
        ulong[] values=new ulong[16];for(int reg=0;reg<16;reg++)values[reg]=0x1122334400000000UL+(uint)reg;
        if(lossMarker){values[14]=unchecked((ulong)target.ToInt64()-0x379B2A0UL);values[1]=0;values[10]=0;}
        else if(!stackMarker){values[6]=unchecked((ulong)target.ToInt64()-0x12F0CCUL);values[14]=0;values[5]=0x12345678;}
        int callbacks=0;MarketDemandWriterHooks.Observer observer=(snapshot,index)=>{
            callbacks++;check(index==(stackMarker?1003:1002),name+": site index");
            for(int i=0;i<Order.Length;i++)check(unchecked((ulong)Marshal.ReadInt64(snapshot,i*8))==values[Order[i]],name+": original GPR input "+Order[i]);
            if(lossMarker)check(Marshal.ReadInt32(target)==23&&Marshal.ReadInt64(snapshot,MarketDemandWriterHooks.R10)==0,
                name+": observer sees old stock23 and guaranteed planned zero before actual fallback store");
            if(stackMarker)check(unchecked((ulong)Marshal.ReadInt64(snapshot,MarketDemandWriterHooks.SnapshotBytes+0x28))==0xA1B2C3D4E5F60718UL,
                name+": snapshot+128 identifies original RSP and its seeded stack operand");};
        var hook=MarketDemandWriterHooks.Prepare(image,module,rva,entry,stackMarker?1003:1002,gate,observer);roots.Add(hook);hook.Publish();
        for(int active=0;active<2;active++)for(int alignment=0;alignment<2;alignment++)
        {
            IntPtr result=Alloc(4096),hostFx=Alloc(4096),sse=Alloc(4096),harness=Alloc(4096);
            byte[] sseBytes=Enumerable.Range(0,256).Select(i=>(byte)(i*29)).ToArray();Marshal.Copy(sseBytes,0,sse,256);
            Write(harness,Harness(unchecked((ulong)harness.ToInt64()),copy,result,hostFx,sse,values,alignment,0xAD7,stackMarker));
            NativeCall call=Marshal.GetDelegateForFunctionPointer<NativeCall>(harness);roots.Add(call);
            callbacks=0;Marshal.WriteInt32(gate,active);Marshal.WriteInt32(count,0);Marshal.WriteInt32(target,23);call();string label=name+" active="+active+" alignment="+alignment;
            check(callbacks==active&&Marshal.ReadInt32(count)==1,label+": gate and original continuation once");
            ulong originalRsp=unchecked((ulong)Marshal.ReadInt64(result,768)),xorResult=0xA1B2C3D4E5F60718UL^originalRsp;
            for(int i=0;i<Order.Length;i++)check(unchecked((ulong)Marshal.ReadInt64(result,i*8))==
                (stackMarker&&Order[i]==1?xorResult:values[Order[i]]),label+": exact replay GPR "+Order[i]);
            if(stackMarker)
            {
                int parity=0;for(int bit=0;bit<8;bit++)parity+=(int)((xorResult>>bit)&1);
                ulong flags=((parity&1)==0?4UL:0)|(xorResult==0?0x40UL:0)|((xorResult>>63)!=0?0x80UL:0);
                check((unchecked((ulong)Marshal.ReadInt64(result,120))&0x8C5)==flags,label+": XOR flags match original stack-derived result");
            }
            else check(Marshal.ReadInt32(target)==(lossMarker?0:0x12345678)&&(Marshal.ReadInt64(result,120)&0x8D5)==(0xAD7&0x8D5),label+": original count store and flags");
            var actualSse=new byte[256];Marshal.Copy(result+128+160,actualSse,0,256);check(actualSse.SequenceEqual(sseBytes),label+": SSE preserved");
        }
        Marshal.WriteInt32(gate,0);
    }
    private static void RunConditionalBoundary(byte[] dll,ManualLogSource log,Action<bool,string>check)
    {
        const int rva=0x2A4EB;byte[] entry={0x7D,0x0F,0x8B,0x84,0x0A,0xB0,0xFA,0xFF,0xFF};
        int scan=NativeDetourBackend.Instance.Options.FunctionScanLimit+16,pages=(scan+4095)/4096;
        IntPtr region=Alloc((pages+1)*4096),copy=region+pages*4096-scan;
        if(!VirtualProtect(region+pages*4096,(UIntPtr)4096,1,out _))throw new Exception("Boundary guard page failed");
        var bytes=new byte[scan];Buffer.BlockCopy(dll,FileOffset(dll,rva),bytes,0,scan);
        check(bytes.Take(entry.Length).SequenceEqual(entry),"Fixes boundary: audited real bytes");
        bytes[9]=0xE9;Buffer.BlockCopy(BitConverter.GetBytes(64-14),0,bytes,10,4);
        bytes[17]=0xE9;Buffer.BlockCopy(BitConverter.GetBytes(128-22),0,bytes,18,4);
        IntPtr counts=Alloc(4096),gate=counts+32,data=counts+64;Marshal.WriteInt32(data,0x76543210);
        for(int branch=0;branch<2;branch++)
        {
            var suffix=new List<byte>{0x9C,0x50,0x48,0xB8};suffix.AddRange(BitConverter.GetBytes((counts+branch*4).ToInt64()));
            suffix.AddRange(new byte[]{0xFF,0x00,0x58,0x9D,0xC3});Buffer.BlockCopy(suffix.ToArray(),0,bytes,branch==0?64:128,suffix.Count);
        }
        Marshal.Copy(bytes,0,copy,bytes.Length);ulong module=unchecked((ulong)(copy.ToInt64()-rva));var image=new byte[rva+scan];Buffer.BlockCopy(bytes,0,image,rva,scan);
        NativePatchGuard.Bind(ScanRegion.FromMemory(image,module,"Fixes boundary private image"),log);
        int callbacks=0;MarketDemandWriterHooks.Observer observer=(snapshot,index)=>{callbacks++;check(index==1000,"Fixes boundary: observer site index");};
        var hook=MarketDemandWriterHooks.Prepare(image,module,rva,entry,1000,gate,observer);roots.Add(hook);hook.Publish();
        ulong[] values=new ulong[16];for(int r=0;r<16;r++)values[r]=0x1122334400000000UL+(uint)r;
        values[1]=unchecked((ulong)data.ToInt64()+0x550UL);values[2]=0;
        for(int branch=0;branch<2;branch++)for(int active=0;active<2;active++)for(int alignment=0;alignment<2;alignment++)
        {
            int flags=branch==0?0x2D7:0xAD7;IntPtr result=Alloc(4096),hostFx=Alloc(4096),sse=Alloc(4096),harness=Alloc(4096);
            byte[] sseBytes=Enumerable.Range(0,256).Select(i=>(byte)(i*19)).ToArray();Marshal.Copy(sseBytes,0,sse,256);
            Write(harness,Harness(unchecked((ulong)harness.ToInt64()),copy,result,hostFx,sse,values,alignment,flags));
            NativeCall call=Marshal.GetDelegateForFunctionPointer<NativeCall>(harness);roots.Add(call);callbacks=0;Marshal.WriteInt32(counts,0);Marshal.WriteInt32(counts+4,0);Marshal.WriteInt32(gate,active);call();
            string label="Fixes boundary branch="+branch+" active="+active+" alignment="+alignment;
            check(callbacks==active&&Marshal.ReadInt32(counts+branch*4)==1&&Marshal.ReadInt32(counts+(1-branch)*4)==0,label+": exactly original chosen branch");
            for(int i=0;i<Order.Length;i++)check(unchecked((ulong)Marshal.ReadInt64(result,i*8))==
                (Order[i]==0&&branch==0?0x76543210UL:values[Order[i]]),label+": GPR "+Order[i]);
            check((Marshal.ReadInt64(result,120)&0x8D5)==(flags&0x8D5),label+": flags preserved through conditional trampoline");
            var actualSse=new byte[256];Marshal.Copy(result+128+160,actualSse,0,256);check(actualSse.SequenceEqual(sseBytes),label+": SSE preserved");
        }
        Marshal.WriteInt32(gate,0);
    }
    private static void RunMultiplyBoundary(byte[] dll,ManualLogSource log,Action<bool,string>check)
    {
        const int rva=0x2A42A;byte[] entry={0x4C,0x69,0xC0,0x3C,0x58,0x00,0x00};
        int scan=NativeDetourBackend.Instance.Options.FunctionScanLimit+16,pages=(scan+4095)/4096;
        IntPtr region=Alloc((pages+1)*4096),copy=region+pages*4096-scan;
        if(!VirtualProtect(region+pages*4096,(UIntPtr)4096,1,out _))throw new Exception("Multiply boundary guard page failed");
        var bytes=new byte[scan];Buffer.BlockCopy(dll,FileOffset(dll,rva),bytes,0,scan);
        check(bytes.Take(entry.Length).SequenceEqual(entry),"Fixes multiply boundary: audited real bytes");bytes[entry.Length]=0xC3;
        Marshal.Copy(bytes,0,copy,bytes.Length);ulong module=unchecked((ulong)(copy.ToInt64()-rva));var image=new byte[rva+scan];Buffer.BlockCopy(bytes,0,image,rva,scan);
        NativePatchGuard.Bind(ScanRegion.FromMemory(image,module,"Fixes multiply private image"),log);
        IntPtr gate=Alloc(4096);int callbacks=0;
        MarketDemandWriterHooks.Observer observer=(snapshot,index)=>{
            callbacks++;check(index==1001&&Marshal.ReadInt64(snapshot,MarketDemandWriterHooks.Rax)==7,
                "Fixes multiply boundary: snapshot precedes original IMUL and keeps player in RAX");};
        var hook=MarketDemandWriterHooks.Prepare(image,module,rva,entry,1001,gate,observer);roots.Add(hook);hook.Publish();
        ulong[] values=new ulong[16];for(int reg=0;reg<16;reg++)values[reg]=0x1122334400000000UL+(uint)reg;values[0]=7;
        for(int active=0;active<2;active++)for(int alignment=0;alignment<2;alignment++)
        {
            IntPtr result=Alloc(4096),hostFx=Alloc(4096),sse=Alloc(4096),harness=Alloc(4096);
            byte[] sseBytes=Enumerable.Range(0,256).Select(i=>(byte)(i*23)).ToArray();Marshal.Copy(sseBytes,0,sse,256);
            Write(harness,Harness(unchecked((ulong)harness.ToInt64()),copy,result,hostFx,sse,values,alignment));
            NativeCall call=Marshal.GetDelegateForFunctionPointer<NativeCall>(harness);roots.Add(call);
            callbacks=0;Marshal.WriteInt32(gate,active);call();string label="Fixes multiply active="+active+" alignment="+alignment;
            check(callbacks==active,label+": gate controls observer");
            for(int i=0;i<Order.Length;i++)check(unchecked((ulong)Marshal.ReadInt64(result,i*8))==
                (Order[i]==8?7UL*0x583C:values[Order[i]]),label+": original IMUL result and GPR "+Order[i]);
            check((Marshal.ReadInt64(result,120)&0x801)==0,label+": original IMUL clears overflow/carry for valid player");
            var actualSse=new byte[256];Marshal.Copy(result+128+160,actualSse,0,256);check(actualSse.SequenceEqual(sseBytes),label+": SSE preserved");
        }
        Marshal.WriteInt32(gate,0);
    }
    private static void VerifyMap(MarketDemandSite map,Action<bool,string> check)
    {
        var decoder=Decoder.Create(64,new ByteArrayCodeReader(map.Bytes),(ulong)map.Rva);Instruction ins=decoder.Decode();
        string label="writer mapping "+map.Rva.ToString("X");
        check(!ins.IsInvalid&&ins.Mnemonic==Mnemonic.Mov&&ins.FlowControl==FlowControl.Next&&
            !ins.IsIPRelativeMemoryOperand&&decoder.IP==(ulong)(map.Rva+map.Bytes.Length)&&ins.Op0Kind==OpKind.Memory,
            label+": complete single non-RIP MOV store");
        check(Slot(ins.MemoryBase)==map.Base&&Slot(ins.MemoryIndex)==map.Index&&
            ins.MemoryIndexScale==map.Scale&&ins.MemoryDisplacement64==(uint)map.Displacement,
            label+": base/index/scale/displacement match independently decoded operands");
        check(map.Immediate?ins.Op1Kind==OpKind.Immediate32&&ins.Immediate32==(uint)map.Value:
            ins.Op1Kind==OpKind.Register&&Slot(ins.Op1Register)==map.Value,
            label+": amount source matches independently decoded operand");
    }
    private static int Slot(Register reg)
    {
        if(reg==Register.None)return -1;string name=reg.ToString();if(name.StartsWith("E",StringComparison.Ordinal))name="R"+name.Substring(1);
        else if(name.EndsWith("D",StringComparison.Ordinal))name=name.Substring(0,name.Length-1);
        string[] names={"R15","R14","R13","R12","R11","R10","R9","R8","RDI","RSI","RBP","RBX","RDX","RCX","RAX"};
        return Array.IndexOf(names,name);
    }
    private static void Mov(List<byte>b,int reg,ulong value){b.Add((byte)(reg>=8?0x49:0x48));b.Add((byte)(0xB8+(reg&7)));b.AddRange(BitConverter.GetBytes(value));}
    private static void PushAll(List<byte>b){b.Add(0x9C);b.Add(0x50);b.AddRange(new byte[]{0x51,0x52,0x53,0x55,0x56,0x57,0x41,0x50,0x41,0x51,0x41,0x52,0x41,0x53,0x41,0x54,0x41,0x55,0x41,0x56,0x41,0x57});}
    private static void PopAll(List<byte>b){b.AddRange(new byte[]{0x41,0x5F,0x41,0x5E,0x41,0x5D,0x41,0x5C,0x41,0x5B,0x41,0x5A,0x41,0x59,0x41,0x58,0x5F,0x5E,0x5D,0x5B,0x5A,0x59,0x58,0x9D});}
    private static void Write(IntPtr p,IEnumerable<byte>b){byte[]a=b.ToArray();Marshal.Copy(a,0,p,a.Length);}
    private static IntPtr Alloc(int bytes){IntPtr p=VirtualAlloc(IntPtr.Zero,(UIntPtr)bytes,0x3000,0x40);if(p==IntPtr.Zero)throw new Exception("Writer test allocation failed");return p;}
    private static int FileOffset(byte[] dll,int rva){int pe=BitConverter.ToInt32(dll,0x3C),sections=BitConverter.ToUInt16(dll,pe+6),opt=BitConverter.ToUInt16(dll,pe+20),table=pe+24+opt;for(int i=0;i<sections;i++){int s=table+i*40,va=BitConverter.ToInt32(dll,s+12),size=BitConverter.ToInt32(dll,s+16);if(rva>=va&&rva<va+size)return rva-va+BitConverter.ToInt32(dll,s+20);}throw new Exception("RVA has no file section");}
    [DllImport("kernel32.dll")]private static extern IntPtr VirtualAlloc(IntPtr a,UIntPtr s,uint t,uint p);
    [DllImport("kernel32.dll")]private static extern bool VirtualProtect(IntPtr a,UIntPtr s,uint p,out uint old);
}
