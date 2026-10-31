using System;
using System.IO;
using System.Runtime.InteropServices;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace BugfixesAndQoL.BuildPreview
{
    internal static class Program
    {
        private static int assertions, callbacks;
        private static NativeDetour<PreviewNativeContract.PreviewDelegate> probe;
        private static readonly PreviewNativeContract.PreviewDelegate callback = Callback;
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocation, uint protection);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint type);

        private static void Assert(bool value, string name)
        {
            assertions++;
            if (!value) throw new Exception(name);
        }

        private static int Main(string[] args)
        {
            try
            {
                PolicyTests();
                CaptureTests();
                StateTests();
                NativeTests(args[0]);
                Console.WriteLine("PASS: " + assertions + " assertions; terrain policy, native branches, exact backend and callable trampoline.");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }

        private static void StateTests()
        {
            var store = new BuildPreviewStateStore();
            foreach (bool client in new[] { false, true })
                foreach (bool setting in new[] { false, true })
                    Assert(BuildPreviewStateStore.Effective(client, setting) == (client && setting), "Client/local activation truth table");
            store.SetEnabled(true); store.SetSession(42);
            var mapper = eMappers.MAPPER_WHEATFARM;
            var counts = new GroundCounts(81, 47, 0);
            var capture = store.Read();
            store.Publish(capture, mapper, 10, 20, counts);
            var stable = store.Read();
            for (int i=0;i<1000;i++) store.Publish(stable, mapper, 10, 20, counts);
            Assert(ReferenceEquals(stable, store.Read()), "Unchanged publication reuses immutable state and snapshot");
            long allocation = GC.GetAllocatedBytesForCurrentThread();
            for (int i=0;i<10000;i++) store.Publish(store.Read(),mapper,10,20,counts);
            Assert(GC.GetAllocatedBytesForCurrentThread()==allocation, "Production unchanged snapshot publication allocates zero after warm-up");

            Assert(GroundTooltipModel.Matches(stable.Snapshot,42,mapper,10,20,false,true,stable.Generation), "Current generation match");
            store.SetEnabled(false);
            Assert(!store.Read().Enabled && store.Read().Snapshot == null, "Disable atomically retires actual counts");
            store.SetEnabled(true);
            store.Publish(capture, mapper, 10, 20, counts);
            Assert(store.Read().Snapshot == null, "Old in-flight callback cannot publish after off/on");
            Assert(!GroundTooltipModel.Matches(stable.Snapshot,42,mapper,10,20,false,true,store.Read().Generation), "Old generation rejected");
            var fresh = store.Read();
            store.Publish(fresh,mapper,10,20,counts);
            store.SetSession(43);
            store.Publish(fresh,mapper,10,20,counts);
            Assert(store.Read().Snapshot == null && store.Read().Session==43, "Map transition retires in-flight counts");
            for(int i=0;i<1000;i++)
            {
                var before=store.Read();store.SetEnabled(false);store.SetEnabled(true);
                store.Publish(before,mapper,10,20,counts);
                Assert(store.Read().Snapshot==null, "Fast toggle generation " + i);
            }
            var current=store.Read();store.Publish(current,mapper,10,20,counts);
            var retained=store.Read().Snapshot;
            store.Publish(store.Read(),mapper,11,20,new GroundCounts(80,46,0));
            Assert(retained.X==10 && retained.Counts.Coverage==81, "Retained snapshot remains immutable");
            store.Clear(current);
            Assert(store.Read().Snapshot==null && store.Read().Enabled && store.Read().Generation==current.Generation, "Abort discards counts without disabling the generation");
            store.Publish(store.Read(),mapper,12,20,counts);
            Assert(store.Read().Snapshot.X==12, "A complete sample publishes after clearing a skipped footprint");
            var producerCapture=store.Read();
            using (var captured=new System.Threading.ManualResetEvent(false))
            using (var resume=new System.Threading.ManualResetEvent(false))
            {
                var worker=new System.Threading.Thread(()=>
                {
                    captured.Set(); resume.WaitOne();
                    store.Publish(producerCapture,mapper,10,20,counts);
                });
                worker.Start();captured.WaitOne();store.SetEnabled(false);store.SetEnabled(true);
                resume.Set();worker.Join();
                Assert(store.Read().Snapshot==null,"Real overlapping producer cannot cross activation generation");
            }

        }

        private static void PolicyTests()
        {
            foreach (eMappers mapper in new[] { eMappers.MAPPER_WHEATFARM, eMappers.MAPPER_HOPSFARM,
                eMappers.MAPPER_APPLEFARM, eMappers.MAPPER_CATTLEFARM })
            {
                int count = FarmPreviewPolicy.Required(mapper);
                int[] logic = new int[count], preview = new int[count]; byte[] terrain = new byte[count];
                for (int delta = -1; delta <= 1; delta++)
                {
                    for (int i=0;i<count;i++) { terrain[i]=(byte)TileType.OasisGrass; preview[i]=62; }
                    terrain[0]=(byte)(delta < 0 ? TileType.NoneOrDirt : delta == 0 ? TileType.OasisGrass : TileType.OasisGrass | TileType.Foliage);
                    int changed=FarmPreviewPolicy.Mark(mapper,true,logic,terrain,preview,out GroundCounts c);
                    Assert(c.Coverage == count+delta && c.Green >= 50 && changed == (delta < 0 ? 1 : 0), "Coverage threshold " + mapper + delta);
                }
                for (int green=49;green<=51;green++)
                {
                    for (int i=0;i<count;i++) { terrain[i]=(byte)(i<green ? TileType.OasisGrass : TileType.Foliage); preview[i]=62; }
                    int changed=FarmPreviewPolicy.Mark(mapper,true,logic,terrain,preview,out GroundCounts c);
                    Assert(c.Coverage == count && c.Green == green && changed == (green < 50 ? count-green : 0), "Green threshold " + mapper + green);
                }
                // Both counters fail: foliage-only cells lack green; dirt lacks both.
                terrain[0]=(byte)TileType.NoneOrDirt;
                for(int i=1;i<count;i++) terrain[i]=(byte)(i<40 ? TileType.OasisGrass : TileType.Foliage);
                for(int i=0;i<count;i++) preview[i]=62;
                preview[1]=55; preview[2]=0x340010;
                Assert(FarmPreviewPolicy.Mark(mapper,true,logic,terrain,preview,out GroundCounts both) == count-39 && both.Coverage == count-1 && both.Green==39, "Union, preserving red obstacles and building sprites");
                for(int i=0;i<count;i++) preview[i]=62;
                Assert(FarmPreviewPolicy.Mark(mapper,false,logic,terrain,preview,out _) == 0, "Vanilla valid placement never recoloured");
                // Combined flags compensate a dirt tile: count+1 coverage, count green.
                for(int i=0;i<count;i++) terrain[i]=(byte)TileType.OasisGrass;
                terrain[0]=0; terrain[1]=(byte)(TileType.Foliage | TileType.OasisGrass | TileType.ThickFoliage);
                Assert(FarmPreviewPolicy.Mark(mapper,true,logic,terrain,preview,out GroundCounts combo) == 0 && combo.Coverage==count+1 && combo.Green==count, "Combined flags separately counted");
                logic[1]=(int)TilePropertyFlag.River;
                Assert(FarmPreviewPolicy.Mark(mapper,true,logic,terrain,preview,out combo)==2 && combo.Coverage==count-2 && combo.Green==count-2, "River excludes both counters");
            }
            for(int flags=0;flags<256;flags++)
            {
                int green=((flags&16)!=0 ? 1:0)+((flags&128)!=0 ? 1:0);
                Assert(FarmPreviewPolicy.Green(0,(byte)flags)==green && FarmPreviewPolicy.Coverage(0,(byte)flags)==green+((flags&1)!=0?1:0), "All combined flags "+flags);
                Assert(FarmPreviewPolicy.Green((int)TilePropertyFlag.River,(byte)flags)==0 && FarmPreviewPolicy.Coverage((int)TilePropertyFlag.River,(byte)flags)==0, "River combination "+flags);
            }
            foreach(var mapper in new[] { eMappers.MAPPER_QUARRY, eMappers.MAPPER_IRON_MINE, eMappers.MAPPER_PITCH_WORKINGS })
            {
                int count=FarmPreviewPolicy.Scale(mapper)*FarmPreviewPolicy.Scale(mapper), need=FarmPreviewPolicy.Required(mapper);
                for(int delta=-1;delta<=1;delta++)
                {
                    int[] logic=new int[count], preview=new int[count]; byte[] terrain=new byte[count];
                    for(int i=0;i<count;i++) { preview[i]=62; if(i<need+delta) logic[i]=unchecked((int)FarmPreviewPolicy.ResourceFlag(mapper)) | (int)TilePropertyFlag.River; }
                    int changed=FarmPreviewPolicy.Mark(mapper,true,logic,terrain,preview,out GroundCounts c);
                    Assert(c.Resource==need+delta && changed==(delta<0 ? count-(need+delta) : 0), "Resource threshold and River "+mapper+delta);
                    for(int i=0;i<count;i++) preview[i]=62;
                    preview[count-1]=55;preview[count-2]=0x340010;
                    changed=FarmPreviewPolicy.Mark(mapper,true,logic,terrain,preview,out c);
                    Assert(preview[count-1]==55 && preview[count-2]==0x340010 && changed==(delta<0 ? count-(need+delta)-2 : 0), "Resource preserves obstacle/building tokens");
                }
            }
            TooltipTests();
        }
        private static void TooltipTests()
        {
            var mapper=eMappers.MAPPER_WHEATFARM;
            var snapshot=new GroundSnapshot(42,mapper,10,20,new GroundCounts(81,47,0));
            Assert(GroundTooltipModel.Matches(snapshot,42,mapper,10,20,false,true), "Current valid and blocked snapshots available");
            Assert(!GroundTooltipModel.Matches(snapshot,43,mapper,10,20,false,true), "Map change retires counts");
            Assert(!GroundTooltipModel.Matches(snapshot,0,mapper,10,20,false,true), "No active session");
            Assert(!GroundTooltipModel.Matches(snapshot,42,eMappers.MAPPER_QUARRY,10,20,false,true), "Building switch");
            Assert(!GroundTooltipModel.Matches(snapshot,42,mapper,11,20,false,true), "Fast mouse movement");
            Assert(!GroundTooltipModel.Matches(snapshot,42,mapper,10,21,false,true), "Rotated cursor position");
            Assert(!GroundTooltipModel.Matches(snapshot,42,mapper,10,20,true,true), "Symbol hover shows minima");
            Assert(!GroundTooltipModel.Matches(snapshot,42,mapper,10,20,false,false), "Missing position/edge/pending rotation");
            Assert(!GroundTooltipModel.Matches(null,42,mapper,10,20,false,true), "Cancelled snapshot");
            Assert(GroundTooltipModel.Matches(snapshot,42,mapper,10,20,false,true), "Pause retains matching position; no wallclock expiry");
            Assert(GroundTooltipModel.Value("Bewuchs",81,81)=="Bewuchs: 81/81", "Short actual text");
            Assert(GroundTooltipModel.Value("Steinfelder",null,8)=="Steinfelder: \u22658", "Short minimum text");
            Assert(GroundTooltipModel.Value("x",3,4,true)=="x: \u20663/4\u2069", "RTL keeps numerator/denominator order");
        }

        private static unsafe void CaptureTests()
        {
            // Sparse committed test image uses the exact production layer/manager offsets.
            IntPtr memory = VirtualAlloc(IntPtr.Zero, (UIntPtr)0x6100000, 0x3000, 0x04);
            if (memory == IntPtr.Zero) throw new Exception("Native layout fixture allocation failed.");
            try
            {
                ulong image = (ulong)memory.ToInt64();
                IntPtr manager = (IntPtr)(long)(image + PreviewNativeContract.ManagerRva);
                var view = new GameTileManagerView((ulong)manager.ToInt64());
                foreach (var mapper in new[] { eMappers.MAPPER_APPLEFARM, eMappers.MAPPER_QUARRY,
                    eMappers.MAPPER_WHEATFARM, eMappers.MAPPER_HOPSFARM, eMappers.MAPPER_CATTLEFARM,
                    eMappers.MAPPER_IRON_MINE, eMappers.MAPPER_PITCH_WORKINGS })
                {
                    int scale = FarmPreviewPolicy.Scale(mapper), count = scale*scale;
                    for (int y = 0; y < scale; y++)
                    {
                        *(int*)(image + PreviewNativeContract.PackedRowRva + (ulong)y*12) = y*scale;
                        for (int x = 0; x < scale; x++)
                        {
                            *(byte*)(image + PreviewNativeContract.ValidMapMaskRva + (ulong)(y*800+x)) = 1;
                            view.LogicGrid[y*scale+x] = 0;
                            view.Logic2Grid[y*scale+x] = (byte)TileType.OasisGrass;
                            view.ConstructionGrid[y*scale+x] = FarmPreviewPolicy.DarkPreview;
                        }
                    }
                    view.Logic2Grid[0] = (byte)TileType.NoneOrDirt;
                    Assert(FootprintMarker.TryApply(image, manager, 0, 0, scale, out MarkResult result, mapper), "Complete footprint sampled: " + mapper);
                    int expected = FarmPreviewPolicy.IsFarm(mapper) ? 1 : count;
                    Assert(result.Changed == expected && result.Coverage == count-1 && view.ConstructionGrid[0] == FarmPreviewPolicy.RedPreview, "Production marking includes packed index zero: " + mapper);
                    for (int i = 0; i < count; i++) view.ConstructionGrid[i] = FarmPreviewPolicy.DarkPreview;
                    foreach (int missingCell in new[] { 0, count-1 })
                    {
                        ulong mask = image + PreviewNativeContract.ValidMapMaskRva + (ulong)((missingCell/scale)*800+missingCell%scale);
                        *(byte*)mask = 0;
                        Assert(!FootprintMarker.TryApply(image, manager, 0, 0, scale, out result, mapper) && result.Cells == 0 && result.Changed == 0 && result.Coverage == 0, "Map edge returns no partial result: " + mapper);
                        Assert(!FootprintMarker.TryApply(image, manager, 0, 0, scale, out result, mapper, false), "Map edge skipped independently of placement state: " + mapper);
                        for (int i = 0; i < count; i++) Assert(view.ConstructionGrid[i] == FarmPreviewPolicy.DarkPreview, "Map edge preserves every preview cell");
                        *(byte*)mask = 1;
                    }
                    foreach (int coordinate in new[] { -1, 800, int.MinValue, int.MaxValue, 800-scale+1 })
                    {
                        Assert(!FootprintMarker.TryApply(image, manager, coordinate, 0, scale, out result, mapper), "X boundary safely skipped: " + mapper);
                        Assert(!FootprintMarker.TryApply(image, manager, 0, coordinate, scale, out result, mapper), "Y boundary safely skipped: " + mapper);
                    }
                    for (int i = 0; i < count; i++) Assert(view.ConstructionGrid[i] == FarmPreviewPolicy.DarkPreview, "Coordinate rejection preserves every preview cell");
                    Assert(FootprintMarker.TryApply(image, manager, 0, 0, scale, out result, mapper) && result.Changed == expected, "Valid-edge-valid recovers without reinitialization: " + mapper);
                    for (int i = 0; i < count; i++) view.ConstructionGrid[i] = FarmPreviewPolicy.DarkPreview;
                    Assert(FootprintMarker.TryApply(image, manager, 0, 0, scale, out result, mapper, false) && result.Changed == 0 && result.Cells == count, "Valid placement sampled without recolouring: " + mapper);
                    *(int*)(image + PreviewNativeContract.PackedRowRva + (ulong)(scale-1)*12) = GameTileManagerView.NativePackedTileCapacity;
                    bool rejected = false;
                    try { FootprintMarker.TryApply(image, manager, 0, 0, scale, out result, mapper); } catch (InvalidOperationException) { rejected = true; }
                    Assert(rejected, "Bad packed row index remains a contract error: " + mapper);
                    for (int i = 0; i < count; i++) Assert(view.ConstructionGrid[i] == FarmPreviewPolicy.DarkPreview, "Corrupt row prevents all writes");
                    rejected = false;
                    try { FootprintMarker.TryApply(image, manager, 0, 0, scale+1, out result, mapper); } catch (ArgumentException) { rejected = true; }
                    Assert(rejected, "Unsupported scale remains a contract error: " + mapper);
                }
            }
            finally { Assert(VirtualFree(memory, UIntPtr.Zero, 0x8000), "Native layout fixture released"); }
        }

        private static byte[] ReadRva(byte[] pe, int rva, int count)
        {
            int nt = BitConverter.ToInt32(pe, 0x3C), sections = BitConverter.ToUInt16(pe, nt + 6);
            int table = nt + 24 + BitConverter.ToUInt16(pe, nt + 20);
            for (int i = 0; i < sections; i++)
            {
                int s = table + i * 40, start = BitConverter.ToInt32(pe, s + 12), size = BitConverter.ToInt32(pe, s + 16);
                if (rva < start || rva + count > start + size) continue;
                byte[] result = new byte[count];
                Buffer.BlockCopy(pe, BitConverter.ToInt32(pe, s + 20) + rva - start, result, 0, count);
                return result;
            }
            throw new Exception("RVA is not backed by file data.");
        }

        private static bool Contains(byte[] bytes, byte[] pattern)
        {
            for (int i = 0; i <= bytes.Length-pattern.Length; i++)
            {
                bool same = true;
                for (int j = 0; j < pattern.Length; j++) same &= bytes[i+j] == pattern[j];
                if (same) return true;
            }
            return false;
        }

        private static void NativeTests(string nativePath)
        {
            byte[] pe = File.ReadAllBytes(nativePath);
            byte[] body = ReadRva(pe, PreviewNativeContract.PreviewRva,
                PreviewNativeContract.PreviewEndRva - PreviewNativeContract.PreviewRva);
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(body));
            decoder.IP = (ulong)PreviewNativeContract.PreviewRva;
            Assert(decoder.Decode().Length == 5 && decoder.Decode().Length == 1 && decoder.Decode().Length == 1,
                "Entry instructions and continuation boundary");
            // Full reachable control flow and switch-table edges are verified by native-audit.py.
            Assert(Contains(body, new byte[] { 0xFC,0xE6,0x04,0x02 }) &&
                Contains(body, new byte[] { 0xEC,0xE7,0x04,0x02 }), "Audited native state offsets");

            IntPtr buffer = VirtualAlloc(IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x40);
            if (buffer == IntPtr.Zero) throw new Exception("Executable test allocation failed.");
            try
            {
                // Same real entry and ABI; a small executable body exposes argument preservation.
                byte[] synthetic = { 0x48,0x89,0x5C,0x24,0x20,0x55, 0x8B,0xC2,0x41,0x03,0xC0,
                    0x45,0x0F,0xBF,0xC9,0x41,0x03,0xC1,0x03,0x44,0x24,0x30,0x89,0x01,0x5D,0xC3 };
                Marshal.Copy(synthetic, 0, buffer, synthetic.Length);
                ulong target = (ulong)buffer.ToInt64();
                PreviewNativeContract.ValidateEntry(target);
                var request = new DetourRequest<PreviewNativeContract.PreviewDelegate> { Name = "Farm preview isolated ABI test", TargetAddress = target, Callback = callback };
                probe = (NativeDetour<PreviewNativeContract.PreviewDelegate>)PreviewNativeContract.Backend().CreateDetour(in request);
                PreviewNativeContract.Validate(probe, target, false);
                probe.Enable();
                PreviewNativeContract.Validate(probe, target, true);
                var entry = (PreviewNativeContract.PreviewDelegate)Marshal.GetDelegateForFunctionPointer(buffer, typeof(PreviewNativeContract.PreviewDelegate));
                IntPtr result = Marshal.AllocHGlobal(4);
                try
                {
                    foreach (short mapper in new short[] { (short)eMappers.MAPPER_WHEATFARM, (short)eMappers.MAPPER_CATTLEFARM, -1 })
                    {
                        int before = callbacks;
                        entry(result, -10, 400, mapper, 9);
                        Assert(Marshal.ReadInt32(result) == -10 + 400 + mapper + 9, "Trampoline ABI and signed mapper");
                        Assert(callbacks == before + 1, "Callback and Original exactly once");
                    }
                    PreviewNativeContract.Validate(probe, target, true);
                }
                finally { Marshal.FreeHGlobal(result); }
            }
            finally
            {
                // This isolated test candidate is never published to a game runtime.
                probe?.Dispose();
                probe = null;
                Assert(VirtualFree(buffer, UIntPtr.Zero, 0x8000), "Test allocation released");
            }
        }

        private static void Callback(IntPtr manager, int x, int y, short mapper, int scale)
        {
            callbacks++;
            probe.Original(manager, x, y, mapper, scale);
        }
    }
}
