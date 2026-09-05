using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Iced.Intel;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.Interop;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Extensions;
using static Iced.Intel.AssemblerRegisters;

namespace MoveMoatTest
{
    internal sealed unsafe partial class MoveMoatPathTest
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void TileUpdateDelegate(IntPtr manager, int y, int tile);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate ulong MoatWriteDelegate(IntPtr manager, byte owner, uint x, uint y, int mode, byte replace);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void MoatDeleteDelegate(IntPtr manager, uint id);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void MaskRebuildDelegate(IntPtr manager);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int PreBuilderRecoveryDelegate(IntPtr manager, int id, int x, int y, int targetX, int targetY);
        private TileUpdateDelegate originalTileUpdate;
        private MoatWriteDelegate originalMoatWrite;
        private MoatDeleteDelegate originalMoatDelete;
        private MaskRebuildDelegate originalMaskRebuild;
        private readonly List<Delegate> connectivityDelegates = new List<Delegate>();
        private readonly List<NativeDetour> connectivityDetours = new List<NativeDetour>();
        private X64InlineHook preBuilderRecoveryHook;
        private long nativeModeEntries, preBuilderFailures, preBuilderRecovered;
        private int noBuilderDetails;
        private readonly Dictionary<string, long> preBuilderRejections = new Dictionary<string, long>();

        private void InstallConnectivityAndRecovery(ReadOnlySpan<byte> memory, ulong libraryBase)
        {
            try
            {
                nativePortalGateStates = (byte*)(libraryBase + 0x64CCED2);
                // Function entry detours retain the complete native ABI. No context-hook
                // generator is used at entries (the 1.42 generator assumes an aligned RSP).
                InstallConnectivityObserver(memory, libraryBase, 0xD90D0,
                    "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 48 83 EC 20 4C 63 F2 45 8B F8 41 8B D6 48 8B E9",
                    (TileUpdateDelegate)((manager, y, tile) => {
                        originalTileUpdate(manager, y, tile);
                        try { InvalidateMovementSearchData(); DirtyCursorTile(tile); } catch (Exception ex) { InvalidateConnectivity(ex); }
                    }), out originalTileUpdate);
                InstallConnectivityObserver(memory, libraryBase, 0x59210,
                    "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48 89 7C 24 20 41 56 48 83 EC 20 49 63 F8 8B EA 49 63 F1 48 8B D9 81 FF 1F",
                    (MoatWriteDelegate)((manager, owner, x, y, mode, replace) => {
                        ulong result = originalMoatWrite(manager, owner, x, y, mode, replace);
                        try { InvalidateMovementSearchData(); if (x < MapWidth && y < MapWidth) DirtyCursorTile(GameTileManagerAPI.Instance.GetTileId((int)x, (int)y)); }
                        catch (Exception ex) { InvalidateConnectivity(ex); }
                        return result;
                    }), out originalMoatWrite);
                InstallConnectivityObserver(memory, libraryBase, 0x61E70,
                    "81 FA FF F9 00 00 77 53 53 48 83 EC 20 48 8B D9 48 63 CA 48 8B C1 48 03 C0 80 BC C3 3C EE F3 01 00 7E 33 4C 8D 89 E3 3E",
                    (MoatDeleteDelegate)((manager, id) => {
                        int tile = id > 0 && id <= MaximumMoatRecordId ? *(int*)((byte*)manager + MoatRecordArrayOffset + id * MoatRecordSize) : -1;
                        originalMoatDelete(manager, id);
                        try { InvalidateMovementSearchData(); DirtyCursorTile(tile); } catch (Exception ex) { InvalidateConnectivity(ex); }
                    }), out originalMoatDelete);
                InstallConnectivityObserver(memory, libraryBase, 0xDAA50,
                    "48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 20 33 DB 48 8D 3D 3C 38 9D 03 89 99 34 5F 15 00 48 8B F1 0F BF 17 44 8B C3 48",
                    (MaskRebuildDelegate)(manager => {
                        originalMaskRebuild(manager);
                        try { InvalidateMovementSearchData(); cursorTopologies.Clear(); } catch (Exception ex) { InvalidateConnectivity(ex); }
                    }), out originalMaskRebuild);

                const int failureRva = 0x19664B;
                ValidateExactBytes(memory, failureRva, new byte[] {
                    0x33,0xC0,0x8B,0xD6,0x48,0x89,0x05,0x8E,0x70,0xF1,0x05,0x49,0x8B,0xCF }, "pre-builder failure block through 196659");
                ValidateExactBytes(memory, 0x196585, new byte[] { 0x4D,0x8D,0x8F,0x58,0x06,0,0,0xBA,0x90,0x04,0,0 }, "native unit-buffer initialization");
                PreBuilderRecoveryDelegate callback = TryRecoverBeforeBuilder;
                connectivityDelegates.Add(callback);
                ulong address = unchecked((ulong)Marshal.GetFunctionPointerForDelegate(callback).ToInt64());
                preBuilderRecoveryHook = new X64InlineHook(libraryBase + failureRva, 14);
                preBuilderRecoveryHook.Generate((asm, original, returnAddress) =>
                    EmitRecoveryAdapter(asm, original.ToArray(), address, libraryBase));
                preBuilderRecoveryHook.Enable();
                InstallPlacementAdapters(memory, libraryBase);
            }
            catch { DisposeConnectivityHooks(); throw; }
        }

        private static void EmitRecoveryAdapter(Assembler asm, Instruction[] original, ulong address, ulong libraryBase)
        {
            // At 19664B RSP is 16-byte aligned. RSI/RDI/RBP/R12-R15 are live
            // nonvolatile state; the managed ABI preserves them. All volatile GPRs
            // and flags are dead on BOTH continuations, and no XMM value is live.
            // Native start stack slots must be read before any overlapping write.
            asm.sub(rsp, 0x30);
            asm.mov(rcx, r15); asm.mov(edx, esi);
            asm.mov(r8d, __dword_ptr[rsp + 0xA0]); asm.mov(r9d, __dword_ptr[rsp + 0xA8]);
            asm.mov(__dword_ptr[rsp + 0x20], r14d); asm.mov(__dword_ptr[rsp + 0x28], ebp);
            asm.mov(rax, address); asm.call(rax); asm.add(rsp, 0x30);
            asm.test(eax, eax);
            var failed = asm.CreateLabel(); asm.je(failed);
            asm.jmp(libraryBase + 0x196585);
            asm.Label(ref failed);
            foreach (var instruction in original) asm.AddInstruction(instruction);
        }

        private void DisposeConnectivityHooks()
        {
            preBuilderRecoveryHook?.Dispose();
            for (int i = connectivityDetours.Count - 1; i >= 0; i--) connectivityDetours[i].Dispose();
            connectivityDetours.Clear();
        }

        private void InstallConnectivityObserver<T>(ReadOnlySpan<byte> memory, ulong libraryBase, int rva,
            string bytes, T callback, out T original) where T : Delegate
        {
            string[] parts = bytes.Split(' '); var expected = new byte[parts.Length];
            for (int i = 0; i < parts.Length; i++) expected[i] = Convert.ToByte(parts[i], 16);
            ValidateExactBytes(memory, rva, expected, "connectivity observer entry");
            if (Shared.NativePatternResolver.FindUniquePattern(memory, bytes, "connectivity observer") != rva)
                throw new InvalidOperationException("Connectivity observer does not match its validated function entry.");
            var detour = CreateDetour(libraryBase + (uint)rva, callback);
            original = detour.GenerateTrampoline<T>();
            connectivityDelegates.Add(callback); connectivityDetours.Add(detour); detour.Apply();
        }

        private void InvalidateMovementSearchData()
        {
            placementRevision++;
            weightedMoatRoutePlanner.SetSearchSession(null, -1, mapEpoch, CaptureCurrentGameTick());
            nativeGroundDecisions.Clear(); activeMoveCommand?.TargetedRouteDecisions.Clear();
            cacheMapEpoch = -1;
        }

        private void InvalidateConnectivity(Exception ex)
        {
            cursorTopologies.Clear();
            TryLogDiagnosticFailure("connectivity-invalidation", ex);
        }

        private int TryRecoverBeforeBuilder(IntPtr manager, int id, int x, int y, int targetX, int targetY)
        {
            try
            {
                preBuilderFailures++;
                UnitMoveFrame frame = GetCurrentUnitMoveFrame();
                if (disposed || manager != (IntPtr)nativeUnitManager || frame == null ||
                    frame.BuilderReached || frame.RecoveryAttempted || frame.Args.UnitId != id) return RejectPreBuilder(frame, "context-or-duplicate");
                frame.RecoveryAttempted = true;
                PlanScope plan = GetUnitMovePlan(frame, id);
                if (plan == null || !plan.ModeObserved || plan.TargetX != targetX || plan.TargetY != targetY ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(id, out GameUnit* unit) || unit == null) return RejectPreBuilder(frame, "identity-mode-or-target");
                GetNativeMovementStart(unit, out int actualX, out int actualY);
                if (x != actualX || y != actualY || (uint)targetX >= MapWidth || (uint)targetY >= MapWidth ||
                    movementTargetAvailability[targetY * MapWidth + targetX] == 0) return RejectPreBuilder(frame, "start-or-unavailable-target");
                int target = GameTileManagerAPI.Instance.GetTileId(targetX, targetY);
                if (!IsValidTileId(target) || HasDownstreamMovementBlockingFlags(tileFlags[target])) return RejectPreBuilder(frame, "blocked-target");
                PrepareMovementSearch(plan, plan.PlayerId);
                if (!weightedMoatRoutePlanner.TryBuildReachabilityEncoded(plan.PlayerId, x, y, targetX, targetY,
                    false, out WeightedMoatRouteSummary summary, out WeightedMoatEncodedRoute route) ||
                    !route.IsValid || summary.MoatEdges <= 0 || !ValidateRecoveryEdges(plan.PlayerId, x, y, targetX, targetY, route)) return RejectPreBuilder(frame, "no-audited-encodable-friendly-route");
                plan.QualifiedTerminalRoute = route; plan.QualifiedTerminalSummary = summary;
                plan.FriendlyRouteQualified = true; plan.ExactRouteEndpoints = true;
                plan.RouteStartX = x; plan.RouteStartY = y;
                // Match the direct-to-builder branch: destination PCL, and no failed
                // portal's zero intermediate in the unit record. No output is published here.
                frame.FailedDestinationRegion = *(short*)((byte*)unit + (0x900 - NativeUnitSlotDataOffset));
                frame.FailedPortalRegion = *(short*)((byte*)unit + (0x8EC - NativeUnitSlotDataOffset));
                frame.RecoveryApplied = true;
                *(short*)((byte*)unit + (0x900 - NativeUnitSlotDataOffset)) = pathRegionGrid[target]; // slot+900, GameUnit begins at slot+65C
                *(short*)((byte*)unit + (0x8EC - NativeUnitSlotDataOffset)) = frame.PrePortalRegion; // slot+8EC
                preBuilderRecovered++;
                return 1;
            }
            catch (Exception ex) { TryLogDiagnosticFailure("pre-builder-recovery", ex); return 0; }
        }

        private int RejectPreBuilder(UnitMoveFrame frame, string reason)
        {
            if (frame != null) frame.RecoveryRejection = reason;
            preBuilderRejections.TryGetValue(reason, out long count);
            preBuilderRejections[reason] = count + 1;
            return 0;
        }

        private string FormatRecoveryRejections()
        {
            var text = new System.Text.StringBuilder();
            foreach (var entry in preBuilderRejections)
            {
                if (text.Length != 0) text.Append(',');
                text.Append(entry.Key).Append(':').Append(entry.Value);
            }
            return text.Length == 0 ? "none" : text.ToString();
        }

        private bool ValidateRecoveryEdges(int player, int x, int y, int targetX, int targetY, WeightedMoatEncodedRoute route)
        {
            if (!route.IsValid || route.DirectionCount > 2000) return false;
            weightedMoatRoutePlanner.BeginReachabilityProbe();
            try
            {
                for (int i = 0; i < route.DirectionCount; i++)
                {
                    int direction = (route.Bytes[i >> 1] >> ((i & 1) * 4)) & 15;
                    if (direction >= 8) return false;
                    int nx = x + WeightedMoatRoutePlanner.DirectionX[direction], ny = y + WeightedMoatRoutePlanner.DirectionY[direction];
                    if ((uint)nx >= MapWidth || (uint)ny >= MapWidth || !weightedMoatRoutePlanner.TryGetTraversalEdge(player,
                        x, y, GameTileManagerAPI.Instance.GetTileId(x, y), nx, ny, GameTileManagerAPI.Instance.GetTileId(nx, ny),
                        direction, i == route.DirectionCount - 1, false, MoatTraversalPolicy.FriendlyOnly, out _, out _)) return false;
                    x = nx; y = ny;
                }
                return x == targetX && y == targetY;
            }
            finally { weightedMoatRoutePlanner.EndReachabilityProbe(); }
        }

        private void ObserveNativeModeEntry(UnitMoveFrame frame, int id)
        {
            nativeModeEntries++;
            if (frame == null || frame.Args.UnitId != id) return;
            frame.NativeModeReached = true;
            if (GameUnitManagerAPI.Instance.TryGetUnitById(id, out GameUnit* unit) && unit != null)
                frame.PrePortalRegion = *(short*)((byte*)unit + (0x8EC - NativeUnitSlotDataOffset));
        }

        private void RestoreFailedRecovery(UnitMoveFrame frame, long result)
        {
            if (result > 0 || !frame.RecoveryApplied || frame.Plan == null ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(frame.Plan.UnitId, out GameUnit* unit) || unit == null ||
                frame.Plan.UnitGlobalId != unit->r_GlobalId || frame.Plan.PlayerId != unit->r_ControllableForPlayerId) return;
            *(short*)((byte*)unit + (0x900 - NativeUnitSlotDataOffset)) = frame.FailedDestinationRegion;
            *(short*)((byte*)unit + (0x8EC - NativeUnitSlotDataOffset)) = frame.FailedPortalRegion;
        }

        private void LogUnitWithoutBuilder(UnitMoveFrame frame, long result)
        {
            if (result > 0 || noBuilderDetails++ >= 32) return;
            int x = frame.Args.TileX, y = frame.Args.TileY;
            int tile = (uint)x < MapWidth && (uint)y < MapWidth ? GameTileManagerAPI.Instance.GetTileId(x, y) : -1;
            Shared.DebugLogHelper.LogInfo(log, $"MoveMoat stage=unit-no-builder unit={frame.Args.UnitId} " +
                $"click=({frame.Command?.TargetX},{frame.Command?.TargetY}) unitTarget=({x},{y}) " +
                $"reason={frame.RecoveryRejection ?? (frame.RegionReached ? "native-after-region" : frame.NativeModeReached ? "native-after-mode" : "native-before-mode")} " +
                $"modeReached={frame.NativeModeReached} regionReached={frame.RegionReached} recoveryAttempted={frame.RecoveryAttempted} " +
                $"availability={(IsValidTileId(tile) ? movementTargetAvailability[y * MapWidth + x] : 0)} " +
                $"targetFlags={(IsValidTileId(tile) ? tileFlags[tile] : 0):X8} targetRegion={(IsValidTileId(tile) ? pathRegionGrid[tile] : 0)} result={result}.");
        }
    }
}
