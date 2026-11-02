using APIShared;
using APIShared.NativeSites;
using BepInEx.Logging;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace PitchStairsTest
{
    // Owns the permanent native stubs of variant B as one atomic unit. Published code is never
    // restored. The enabled word is finalized during installation and remains constant thereafter.
    internal sealed unsafe partial class PitchStairsRuntime
    {
        private const string Registration = "PitchStairsVariantB";
        private static readonly (string Name, int Offset)[] Counters =
        {
            ("skippedLowerings", NativeCode.SkippedLoweringsOffset),
            ("skippedAivRestores", NativeCode.SkippedAivRestoresOffset),
            ("skippedManualRestores", NativeCode.SkippedManualRestoresOffset),
            ("loadPitchTiles", NativeCode.LoadPitchTilesOffset),
            ("loadRaisedTiles", NativeCode.LoadRaisedTilesOffset),
            ("fireSourceAdjusted", NativeCode.FireSourceAdjustedOffset),
            ("fireNeighborAdjusted", NativeCode.FireNeighborAdjustedOffset),
        };

        private readonly ManualLogSource log;
        private readonly HookHandle<X64InlineHook>[] handles;
        private readonly long[] reported = new long[Counters.Length];
        private HookTransaction transaction;
        private IntPtr state;
        private ulong image;
        private bool published, ready;

        internal PitchStairsRuntime(ManualLogSource log)
        {
            this.log = log;
            handles = new HookHandle<X64InlineHook>[NativeCode.Sites.Length];
            for (int i = 0; i < handles.Length; i++) handles[i] = new HookHandle<X64InlineHook>();
        }

        internal bool Published => published;

        internal void Install(CrusaderLibraryLoadContext context, bool referenceHashMatches)
        {
            // No pattern fallback for another DLL on purpose: the tile offsets, the screen-height table and
            // the stub spans are proven only for this hash. A different build leaves Vanilla untouched.
            if (context == null || context.ModuleHandle == IntPtr.Zero || !referenceHashMatches ||
                !string.Equals(Shared.DebugLogHelper.InstalledNativeSha256(), NativeCode.Hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Pitch stairs requires the audited native image: " + NativeCode.Hash);
            image = unchecked((ulong)context.ModuleHandle.ToInt64());
            ValidateDataLayout(context.Memory);
            // Validate the independently resolved manager before any hook can change map data.
            // A later mission-start disable cannot restore a map already loaded under B rules.
            ulong extenderTiles = GameGlobalsManager.Instance.GameTileManagerVA;
            if (extenderTiles != image + NativeCode.TileManagerRva)
                throw new InvalidOperationException($"Extender tile manager 0x{extenderTiles:X} differs from " +
                    $"the audited manager 0x{image + NativeCode.TileManagerRva:X}.");

            var spans = new NativeSiteSpan[NativeCode.Sites.Length];
            for (int i = 0; i < NativeCode.Sites.Length; i++)
            {
                NativeCode.Site site = NativeCode.Sites[i];
                var resolution = Shared.NativePatternResolver.ResolveUnique(context.Memory, site.Bytes, site.Rva, true,
                    "pitch stairs " + site.Name, log, Shared.NativePatternSearchScope.ExecutableSections);
                if (resolution.Rva != site.Rva || !context.Memory.Slice(site.Rva, site.Length).SequenceEqual(site.Bytes))
                    throw new InvalidOperationException("Pitch-stairs site moved outside its audited layout: " + site.Name);
                if (!context.Memory.Slice(site.ContinuationRva, site.Following.Length).SequenceEqual(site.Following))
                    throw new InvalidOperationException("Pitch-stairs continuation changed: " + site.Name);
                NativeCode.Decode(site.Bytes, image + (uint)site.Rva, site.InstructionLengths);
                byte[] live = new byte[site.Length];
                Marshal.Copy(new IntPtr(unchecked((long)(image + (uint)site.Rva))), live, 0, live.Length);
                if (!live.AsSpan().SequenceEqual(site.Bytes))
                    throw new InvalidOperationException("Occupied pitch-stairs site: " + site.Name + " 0x" + site.Rva.ToString("X"));
                using (var probe = new X64InlineHook(image + (uint)site.Rva, site.Length))
                    if (probe.DisplacedByteCount != site.Length || probe.IsInstalled)
                        throw new InvalidOperationException("Pitch-stairs RedBird span differs: " + site.Name);
                using (var sha = SHA256.Create())
                    spans[i] = new NativeSiteSpan(site.Rva, site.Length, NativeSiteMode.Exclusive,
                        BitConverter.ToString(sha.ComputeHash(site.Bytes)).Replace("-", ""));
                Shared.DebugLogHelper.LogInfo(log, $"PITCH_STAIRS_SITE name={site.Name}; method={resolution.Method}; " +
                    $"rva=0x{site.Rva:X}; end=0x{site.ContinuationRva:X}; span={site.Length}; " +
                    $"instructionLengths={string.Join(",", site.InstructionLengths)}.");
            }

            // All sites form one rule: a partial set would mix lowered and unlowered pitch and cut
            // stairs off again. That is why there is a single exclusive claim and a single transaction.
            var claim = NativeSiteRegistry.Register(PitchStairsTestPlugin.Guid, Registration, spans);
            if (!claim.Succeeded) throw new InvalidOperationException("Native site claim refused: " + claim.Reason);
            try
            {
                state = Marshal.AllocHGlobal(NativeCode.StateSize);
                Marshal.Copy(new byte[NativeCode.StateSize], 0, state, NativeCode.StateSize);
                transaction = new HookTransaction(context.Region, SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions { FailureMode = TransactionFailureMode.RollbackAndThrow, OwnsHooks = true });
                ulong stateAddress = unchecked((ulong)state.ToInt64());
                bool diagCounters = false;
                DiagFireCounters(ref diagCounters);
                for (int i = 0; i < NativeCode.Sites.Length; i++)
                {
                    NativeCode.Site site = NativeCode.Sites[i];
                    ulong address = image + (uint)site.Rva, continuation = image + (uint)site.ContinuationRva;
                    // Assemble every stub once before committing any site.
                    Instruction[] expected = NativeCode.Decode(site.Bytes, address, site.InstructionLengths);
                    var a = new Assembler(64);
                    NativeCode.Emit(a, site, expected, stateAddress, continuation, diagCounters);
                    NativeCode.Assemble(a, address);
                    transaction.AddInline(handles[i], HookTarget.FromAddress(address), (assembler, original, _) =>
                    {
                        // A mismatch throws inside Commit, which rolls the whole transaction back.
                        NativeCode.RequireSameInstructions(original, expected, site.Name);
                        NativeCode.Emit(assembler, site, original, stateAddress, continuation, diagCounters);
                    }, hookSize: site.Length, name: "PitchStairsTest_" + site.Name);
                }
                CommitResult result = transaction.Commit();
                if (!result.IsCompleteSuccess) throw new InvalidOperationException("Pitch-stairs transaction failed: " + result);
                published = true;
                for (int i = 0; i < NativeCode.Sites.Length; i++) ValidateCommitted(NativeCode.Sites[i], handles[i]);
                ready = true;
                // Enabled only after the post-commit check. LibraryLoaded precedes every map load, so no
                // tile is ever stored with mixed rules.
                Volatile.Write(ref *(int*)state, 1);
            }
            catch
            {
                ready = false;
                if (state != IntPtr.Zero) Volatile.Write(ref *(int*)state, 0);
                if (!published)
                {
                    transaction?.Dispose(); // Unpublished initialization candidate only.
                    if (state != IntPtr.Zero) Marshal.FreeHGlobal(state);
                    state = IntPtr.Zero;
                    NativeSiteRegistry.Unregister(PitchStairsTestPlugin.Guid, Registration);
                }
                throw;
            }
            // Diagnostic failure must never switch a successfully activated simulation rule off.
            try
            {
                Shared.DebugLogHelper.LogInfo(log, $"PITCH_STAIRS_READY: {NativeCode.Sites.Length} permanent native stubs; " +
                    "pitch keeps ground height (placement, AIV/manual overbuild, start/load, render, placement cap); " +
                    "fire spread keeps Vanilla heights; no managed per-tile callback, save extension or RNG call.");
            }
            catch { /* Hooks and their enabled state already belong to the process. */ }
        }

        private static void ValidateDataLayout(ReadOnlySpan<byte> memory)
        {
            if (!memory.Slice(NativeCode.TileManagerAnchorRva, 3).SequenceEqual(NativeCode.TileManagerAnchorPrefix))
                throw new InvalidOperationException("Tile-manager anchor instruction changed.");
            int displacement = MemoryMarshal.Read<int>(memory.Slice(NativeCode.TileManagerAnchorRva + 3, 4));
            if (NativeCode.TileManagerAnchorRva + 7 + displacement != NativeCode.TileManagerRva)
                throw new InvalidOperationException("Tile-manager anchor resolves elsewhere.");
            RequireDisplacement(memory, NativeCode.StructureAnchorRva, 9, NativeCode.StructureOffset, Code.Cmp_rm16_imm8);
            RequireDisplacement(memory, NativeCode.DefaultHeightAnchorRva, 8, NativeCode.DefaultHeightOffset, Code.Movzx_r32_rm8);
            RequireDisplacement(memory, NativeCode.LoadStructureAnchorRva, 8, NativeCode.MiscDisplayToStructureDelta, Code.Movsx_r64_rm16);
            RequireDisplacement(memory, NativeCode.PitchLimitAnchorRva, 7, NativeCode.PitchLimitOffset, Code.Movsxd_r64_rm32);
            RequireDisplacement(memory, NativeCode.PitchRecordsAnchorRva, 7,
                NativeCode.PitchRecordsOffset + NativeCode.PitchRecordStride, Code.Lea_r64_m);
            RequireDisplacement(memory, NativeCode.PitchOwnerAnchorRva, 5, NativeCode.PitchOwnerOffset, Code.Cmp_rm16_imm8);
            RequireDisplacement(memory, NativeCode.PitchStateAnchorRva, 8,
                NativeCode.PitchRecordsOffset + NativeCode.PitchStateOffset, Code.Cmp_rm16_imm8);

            // The displaced blocks themselves carry the remaining offsets and targets.
            Instruction[] Site(int index) => NativeCode.Decode(NativeCode.Sites[index].Bytes, (ulong)NativeCode.Sites[index].Rva,
                NativeCode.Sites[index].InstructionLengths);
            Instruction[] lowering = Site(0), aiv = Site(1), load = Site(3), render = Site(4), placement = Site(5), fire = Site(6);
            bool ok = (long)lowering[3].MemoryDisplacement64 == NativeCode.HeightOffset &&
                (long)aiv[0].MemoryDisplacement64 == NativeCode.HeightOffset && (long)aiv[1].MemoryDisplacement64 == NativeCode.LogicOffset &&
                (long)load[0].MemoryDisplacement64 == NativeCode.DefaultHeightOffset - NativeCode.HeightOffset &&
                (long)render[0].MemoryDisplacement64 == NativeCode.HeightOffset &&
                (long)render[1].MemoryDisplacement64 == NativeCode.VanillaPitchHeightTableOffset &&
                NativeCode.VanillaPitchHeightTableOffset - NativeCode.HeightTableOffset == NativeCode.PitchHeightDrop * sizeof(int) &&
                (long)placement[1].MemoryDisplacement64 == NativeCode.TileManagerRva + NativeCode.MiscDisplayOffset &&
                placement[0].Immediate32 == NativeCode.PitchVisibleBit &&
                (long)fire[0].MemoryDisplacement64 == NativeCode.HeightOffset;
            for (int i = 0; i < NativeCode.Sites.Length; i++)
            {
                NativeCode.Site site = NativeCode.Sites[i];
                Instruction[] block = Site(i);
                if (site.Kind == NativeCode.SiteKind.FireNeighbor)
                    ok &= block[site.CallIndex].NearBranchTarget == NativeCode.NeighborHeightRva;
                else if (site.Kind == NativeCode.SiteKind.LoadBootstrap)
                    ok &= block[1].IPRelativeMemoryAddress == NativeCode.TileManagerRva + NativeCode.HeightOffset &&
                        block[2].Immediate32 == NativeCode.TileCapacity;
                else if (site.Kind == NativeCode.SiteKind.BurnoutRestore)
                    ok &= (long)block[0].MemoryDisplacement64 == NativeCode.TileManagerRva + NativeCode.DefaultHeightOffset &&
                        (long)block[1].MemoryDisplacement64 == NativeCode.TileManagerRva + NativeCode.HeightOffset;
                else if (site.Kind == NativeCode.SiteKind.PitchChain)
                    ok &= (long)block[0].MemoryDisplacement64 == NativeCode.TileManagerRva + NativeCode.HeightOffset;
            }
            if (!ok) throw new InvalidOperationException("Pitch-stairs tile, table or call layout differs.");
            if (GameTileManagerView.NativePackedTileCapacity != NativeCode.TileCapacity ||
                GameTileManagerView.PitchSlotCapacity != NativeCode.PitchSlotCapacity)
                throw new InvalidOperationException("Extender packed tile or pitch capacity changed.");
        }

        private static void RequireDisplacement(ReadOnlySpan<byte> memory, int rva, int length, int displacement, Code code)
        {
            Instruction instruction = NativeCode.Decode(memory.Slice(rva, length).ToArray(), (ulong)rva, new[] { length })[0];
            if (instruction.Code != code || (long)instruction.MemoryDisplacement64 != displacement)
                throw new InvalidOperationException($"Layout anchor 0x{rva:X} differs.");
        }

        private static void ValidateCommitted(NativeCode.Site site, HookHandle<X64InlineHook> handle)
        {
            if (!handle.Success || handle.Hook.DisplacedByteCount != site.Length)
                throw new InvalidOperationException("Committed pitch-stairs span differs: " + site.Name);
            byte* live = (byte*)handle.Hook.TargetAddress;
            if (live[0] != 0xFF || live[1] != 0x25 || *(uint*)(live + 2) != 0 ||
                *(ulong*)(live + 6) != unchecked((ulong)handle.Hook.StubAddress.ToInt64()))
                throw new InvalidOperationException("Committed pitch-stairs jump differs: " + site.Name);
            for (int i = 14; i < site.Length; i++)
                if (live[i] != 0x90) throw new InvalidOperationException("Committed pitch-stairs padding differs: " + site.Name);
        }

        internal long Read(int offset) => state == IntPtr.Zero ? 0 : Volatile.Read(ref *(long*)((byte*)state + offset));
        internal bool Enabled => state != IntPtr.Zero && Volatile.Read(ref *(int*)state) != 0;

        internal void OnMissionStarted(MissionLifecycleNotification notification)
        {
            if (!published) return;
            MissionContext context = notification.Context;
            // Editor map loads also run 0x804C0 (DLL_LoadSaveGame); only a new, pitch-free editor map skips it.
            Shared.DebugLogHelper.LogInfo(log, $"PITCH_STAIRS_SESSION session={context?.SessionId}; start={context?.StartKind}; " +
                $"enabled={Enabled}; ready={ready}; delta/total: {CounterReport()}; mode={context?.Mode.ToDiagnosticString()}.");
            DiagMissionStarted(notification);
        }

        // Closes the counting window of a mission, so activity after its start report is not lost.
        internal void OnMissionEnded(MissionLifecycleNotification notification)
        {
            if (!published) return;
            MissionContext context = notification.Context;
            Shared.DebugLogHelper.LogInfo(log, $"PITCH_STAIRS_SESSION_END session={context?.SessionId}; start={context?.StartKind}; " +
                $"reason={notification.EndReason}; enabled={Enabled}; delta/total: {CounterReport()}.");
            DiagMissionEnded(notification);
        }

        // Delta since the previous start or end report, then the process-lifetime total.
        private string CounterReport()
        {
            var parts = new string[Counters.Length];
            for (int i = 0; i < Counters.Length; i++)
            {
                long value = Read(Counters[i].Offset);
                parts[i] = $"{Counters[i].Name}={value - reported[i]}/{value}";
                reported[i] = value;
            }
            return string.Join("; ", parts);
        }

        partial void DiagMissionStarted(MissionLifecycleNotification notification);
        partial void DiagMissionEnded(MissionLifecycleNotification notification);
        partial void DiagFireCounters(ref bool enabled);
    }
}
