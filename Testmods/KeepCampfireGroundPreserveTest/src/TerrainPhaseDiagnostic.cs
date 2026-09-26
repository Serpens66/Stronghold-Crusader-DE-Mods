using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;

namespace KeepCampfireGroundPreserveTest
{
    // Read-only probes around the two consecutive Vanilla terrain phases.
    // The detours remain installed after publication; only Armed changes.
    internal sealed class TerrainPhaseDiagnostic
    {
        internal const int RecalculateRva = 0x65830;
        internal const int FillRva = 0x650C0;
        private const int Side = 21;
        private const int Radius = 10;
        private const int MaxExamplesPerPhase = 72;
        private static readonly byte[] RecalculatePrefix = {
            0x4C, 0x8B, 0xDC, 0x53, 0x55, 0x48, 0x81, 0xEC, 0x88, 0, 0, 0,
            0x8B, 0x81, 0x30, 0xE6, 0x04, 0x02
        };
        private static readonly byte[] FillPrefix = {
            0x40, 0x53, 0x48, 0x81, 0xEC, 0x90, 0, 0, 0,
            0x8B, 0x81, 0x34, 0xE6, 0x04, 0x02
        };

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void TerrainFunction(IntPtr tileManager);

        private readonly HookHandle<NativeDetour<TerrainFunction>> recalculate =
            new HookHandle<NativeDetour<TerrainFunction>>();
        private readonly HookHandle<NativeDetour<TerrainFunction>> fill =
            new HookHandle<NativeDetour<TerrainFunction>>();
        private readonly object gate = new object();
        private readonly Action<string> log;
        private HookTransaction transaction;
        private Target target;
        private bool published;
        private int armed;
        private string failure;

        private TerrainPhaseDiagnostic(Action<string> log) { this.log = log; }

        internal static TerrainPhaseDiagnostic TryCreate(CrusaderLibraryLoadContext context,
            ManualLogSource logger, Action<string> log)
        {
            var candidate = new TerrainPhaseDiagnostic(log);
            try { candidate.Install(context); return candidate; }
            catch { candidate.RollbackUnpublished(); throw; }
        }

        internal void SetEnabled(bool enabled)
        {
            Interlocked.Exchange(ref armed, enabled ? 1 : 0);
            if (!enabled) lock (gate) target = null;
        }

        internal void ArmKeep(int playerId, int x, int y)
        {
            if (!published || Volatile.Read(ref armed) == 0 || playerId != 1) return;
            lock (gate)
            {
                if (target != null) return;
                var api = GameTileManagerAPI.Instance;
                int capacity = api.GetGfxLayer().Length;
                var next = new Target(playerId, x, y);
                for (int dy = -Radius; dy <= Radius; dy++)
                for (int dx = -Radius; dx <= Radius; dx++)
                {
                    int tx = x + dx, ty = y + dy;
                    if (!api.IsTileInsideMapBounds(tx, ty)) continue;
                    int id = api.GetTileId(tx, ty);
                    if (id < 0 || id >= capacity) continue;
                    next.Ids.Add(id);
                    next.X.Add(tx);
                    next.Y.Add(ty);
                }
                if (next.Ids.Count == 0) return;
                target = next;
                log("terrain-phase armed keep player=" + playerId + " origin=" + x + "," + y +
                    " tiles=" + next.Ids.Count + " baseline=first-observed-before-recalculation");
            }
        }

        internal void FlushCompleted()
        {
            Target ready;
            lock (gate)
            {
                ready = target;
                if (ready == null || (ready.Stage != 4 && failure == null)) return;
                target = null;
            }
            if (failure != null) { log("terrain-phase failed: " + failure); failure = null; return; }
            Report(ready, 0, 1, "recalculate-0x65830");
            Report(ready, 2, 3, "fill-0x650C0");
            int changed = 0;
            for (int i = 0; i < ready.Ids.Count; i++)
                if (ready.Frames[0][i].Gfx != ready.Frames[3][i].Gfx) changed++;
            log("terrain-phase complete keep=" + ready.X0 + "," + ready.Y0 +
                " finalGfxChangedFromFirstObserved=" + changed + " tiles=" + ready.Ids.Count);
        }

        private void Report(Target item, int from, int to, string phase)
        {
            int changed = 0, free = 0, zeroed = 0, examples = 0;
            for (int i = 0; i < item.Ids.Count; i++)
            {
                TileRecord before = item.Frames[from][i], after = item.Frames[to][i];
                if (before.Gfx == after.Gfx && before.Alpha == after.Alpha) continue;
                changed++;
                if (before.Structure == 0 && after.Structure == 0) free++;
                if (before.Gfx != 0 && after.Gfx == 0) zeroed++;
                if (examples++ >= MaxExamplesPerPhase ||
                    before.Structure != 0 || after.Structure != 0) continue;
                log("terrain-phase tile=" + item.X[i] + "," + item.Y[i] +
                    " phase=" + phase + " gfx=0x" + before.Gfx.ToString("X8") +
                    "->0x" + after.Gfx.ToString("X8") + " alpha=0x" +
                    before.Alpha.ToString("X8") + "->0x" + after.Alpha.ToString("X8") +
                    " structure=" + before.Structure + "->" + after.Structure +
                    " logic=0x" + before.Logic.ToString("X8") + "->0x" +
                    after.Logic.ToString("X8") + " logic2=0x" +
                    before.Logic2.ToString("X2") + "->0x" +
                    after.Logic2.ToString("X2") + " luminescence=" +
                    before.Luminescence + "->" + after.Luminescence + " macro=0x" +
                    before.Macro.ToString("X4") + "->0x" + after.Macro.ToString("X4") +
                    " changedGrid=" + before.Changed + "->" + after.Changed);
            }
            log("terrain-phase summary=" + phase + " graphicChanged=" + changed +
                " freeToFree=" + free + " nonzeroToZero=" + zeroed +
                " examples=" + Math.Min(examples, MaxExamplesPerPhase));
        }

        private void RecalculateHook(IntPtr manager)
        {
            CaptureIfExpected(manager, 0, 0);
            recalculate.Original(manager);
            CaptureIfExpected(manager, 1, 1);
        }

        private void FillHook(IntPtr manager)
        {
            CaptureIfExpected(manager, 2, 2);
            fill.Original(manager);
            CaptureIfExpected(manager, 3, 3);
        }

        private void CaptureIfExpected(IntPtr manager, int expectedStage, int frame)
        {
            if (Volatile.Read(ref armed) == 0 || manager == IntPtr.Zero) return;
            lock (gate)
            {
                Target item = target;
                if (item == null || item.Stage != expectedStage) return;
                try
                {
                    for (int i = 0; i < item.Ids.Count; i++)
                    {
                        int id = item.Ids[i];
                        item.Frames[frame][i] = new TileRecord {
                            Gfx = Marshal.ReadInt32(manager, 0x140900 + id * 4),
                            Alpha = Marshal.ReadInt32(manager, 0x279D80 + id * 4),
                            Logic = Marshal.ReadInt32(manager, 0x898400 + id * 4),
                            Structure = unchecked((ushort)Marshal.ReadInt16(manager, 0xB0BCA0 + id * 2)),
                            Logic2 = Marshal.ReadByte(manager, 0x9D2500 + id),
                            Luminescence = Marshal.ReadByte(manager, 0xE69500 + id),
                            Macro = unchecked((ushort)Marshal.ReadInt16(manager, 0xFF0EA0 + id * 2)),
                            Changed = Marshal.ReadByte(manager, 0xA20D40 + id)
                        };
                    }
                    item.Stage++;
                }
                catch (Exception ex) { failure = ex.GetType().Name + ": " + ex.Message; item.Stage = -1; }
            }
        }

        private void Install(CrusaderLibraryLoadContext context)
        {
            ulong image = unchecked((ulong)context.ModuleHandle.ToInt64());
            CheckPrefix(image, RecalculateRva, RecalculatePrefix);
            CheckPrefix(image, FillRva, FillPrefix);
            transaction = new HookTransaction(context.Region,
                SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                new HookTransactionOptions {
                    FailureMode = TransactionFailureMode.RollbackAndThrow, OwnsHooks = true
                });
            transaction.AddDetour(recalculate, HookTarget.FromAddress(image + RecalculateRva),
                (TerrainFunction)RecalculateHook);
            transaction.AddDetour(fill, HookTarget.FromAddress(image + FillRva),
                (TerrainFunction)FillHook);
            CommitResult result = transaction.Commit();
            if (!result.IsCompleteSuccess || !recalculate.Success || !fill.Success)
                throw new InvalidOperationException("Terrain-phase hook transaction failed: " + result);
            ValidateDetour(recalculate.Hook, image + RecalculateRva, 12, "recalculate");
            ValidateDetour(fill.Hook, image + FillRva, 9, "fill");
            published = true;
            log("terrain-phase hooks ready: RVA=0x65830/0x650C0 scheme=Indirect spans=12/9 " +
                "mode=read-only, disarmed until player-one Keep spawn");
        }

        private static void CheckPrefix(ulong image, int rva, byte[] expected)
        {
            byte[] actual = new byte[expected.Length];
            Marshal.Copy((IntPtr)(image + (uint)rva), actual, 0, actual.Length);
            for (int i = 0; i < expected.Length; i++)
                if (actual[i] != expected[i])
                    throw new InvalidOperationException("Native terrain entry differs at 0x" + rva.ToString("X"));
        }

        private static void ValidateDetour(NativeDetour<TerrainFunction> detour,
            ulong address, int displacement, string name)
        {
            if (detour == null || !detour.IsInstalled || detour.TargetAddress != address ||
                detour.Scheme.ToString() != "Indirect" ||
                detour.DisplacedByteCount != displacement || detour.PointerSlot == IntPtr.Zero ||
                detour.HookEntryPointAddress == IntPtr.Zero ||
                detour.OriginalEntryPointAddress == IntPtr.Zero ||
                detour.TrampolineAddress == IntPtr.Zero || detour.TrampolineSize <= 0)
                throw new InvalidOperationException(name + " NativeDetour contract differs.");
        }

        private void RollbackUnpublished()
        {
            if (published) throw new InvalidOperationException("Published terrain hooks must remain installed.");
            transaction?.Dispose();
            transaction = null;
        }

        private sealed class Target
        {
            internal readonly int X0, Y0;
            internal readonly List<int> Ids = new List<int>(Side * Side);
            internal readonly List<int> X = new List<int>(Side * Side);
            internal readonly List<int> Y = new List<int>(Side * Side);
            internal readonly TileRecord[][] Frames = {
                new TileRecord[Side * Side], new TileRecord[Side * Side],
                new TileRecord[Side * Side], new TileRecord[Side * Side]
            };
            internal int Stage;
            internal Target(int playerId, int x, int y) { X0 = x; Y0 = y; }
        }

        private struct TileRecord
        {
            internal int Gfx, Alpha, Logic;
            internal ushort Structure, Macro;
            internal byte Logic2, Luminescence, Changed;
        }
    }
}
