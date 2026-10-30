extern alias BugfixesRuntime;
using System;
using BepInEx.Logging;
using BugfixesRuntime::BugfixesAndQoL.UnitCommands;
using SHCDESE.API.LowLevel;
namespace MoatMove
{
    internal sealed unsafe partial class FriendlyMoatTraversalProvider : UnitCommandTraversalProvider
    {
        private readonly UnitCommandPathRuntime runtime;
        private readonly MoatMoveOptions settings;
        private readonly ManualLogSource log;
        private volatile bool enabled;
        internal override bool Enabled => enabled;
        internal override bool RequiredOnly => settings.GetFriendlyMoatMovementMode() == FriendlyMoatMovementMode.RequiredOnly;
        internal bool RequiredOnlyMode => RequiredOnly;
        internal override IMoatSearchKernel CreateKernel(int width, int height, MoatSearchEdge edge) => new MoatSearchKernel(width, height, edge);
        internal FriendlyMoatTraversalProvider(ManualLogSource log, MoatMoveOptions options)
        {
            this.log = log; settings = options;
            runtime = UnitCommandPathAPI.Runtime ?? throw new InvalidOperationException("Main command runtime unavailable.");
            UnitCommandPathAPI.RegisterTraversal(this);
        }
        internal void Install(CrusaderLibraryLoadContext context)
        {
            try
            {
                if (settings.NativeFast) FastNativeKernel.Initialize(context.Memory.Slice(FastNativeKernel.SourceRva, FastNativeKernel.SourceLength).ToArray(), unchecked((ulong)context.ModuleHandle.ToInt64()));
                if (settings.NativeFast) FastNativeKernel.Get(800, 800);
                enabled = true;
                InstallFastCommandRuntime(context.Memory, unchecked((ulong)context.ModuleHandle.ToInt64()));
                InitializeAnchor();
                InstallGroundFailureProofs(context.Memory, unchecked((ulong)context.ModuleHandle.ToInt64()));
                InstallMoatFailureProofs(context.Memory, unchecked((ulong)context.ModuleHandle.ToInt64()));
                RegisterFireRouteRules(context.Memory, unchecked((ulong)context.ModuleHandle.ToInt64()));
            }
            catch { enabled = false; throw; }
        }

        // GroundFailureProofs replays F4930's ground failure path; these instruction bytes pin exactly the
        // audited sequence (FBCB9319; UpdateToNewDLL.md "Failed ground builds"). Any mismatch leaves
        // the feature off and every member keeps Vanilla's builder.
        private void InstallGroundFailureProofs(ReadOnlySpan<byte> memory, ulong libraryBase)
        {
            if (!anchorAvailable) return;
            try
            {
                // F4930: call F32B0; test eax,eax; jz; inc [rbx+0xA8] (direct success).
                UnitCommandPathRuntime.ValidateExactBytes(memory, 0xF4A8C, new byte[] { 0xE8,0x1F,0xE8,0xFF,0xFF,0x85,0xC0,0x74,0x81,0xFF,0x83,0xA8,0x00,0x00,0x00 }, "F4930 direct attempt");
                // F4930: inc [rbx+0xAC] (direct failure) and mov [rbx+0x7C],ebp; call 79C0.
                UnitCommandPathRuntime.ValidateExactBytes(memory, 0xF4A2B, new byte[] { 0xFF,0x83,0xAC,0x00,0x00,0x00 }, "F4930 direct failure counter");
                UnitCommandPathRuntime.ValidateExactBytes(memory, 0xF4A35, new byte[] { 0x89,0x6B,0x7C,0xE8,0x83,0x2F,0xF1,0xFF }, "F4930 search flag");
                // F4930: call DAAC0, call DA590 (both skipped for proven starts).
                UnitCommandPathRuntime.ValidateExactBytes(memory, 0xF4AF5, new byte[] { 0xE8,0xC6,0x5F,0xFE,0xFF }, "F4930 DAAC0 call");
                UnitCommandPathRuntime.ValidateExactBytes(memory, 0xF4ACF, new byte[] { 0xE8,0xBC,0x5A,0xFE,0xFF }, "F4930 DA590 call");
                UnitCommandPathRuntime.ValidateExactBytes(memory, 0xF4A78, new byte[] { 0xE8,0x13,0x5B,0xFE,0xFF }, "F4930 DA590 call (direct mode)");
                // F4930's region gate: mov edx,r10d (player); mov r9d,eax (target region); call E2F60; test; jz.
                UnitCommandPathRuntime.ValidateExactBytes(memory, 0xF49C2, new byte[] { 0x41,0x8B,0xD2,0x44,0x8B,0xC8,0xE8,0x93,0xE5,0xFE,0xFF,0x85,0xC0,0x0F,0x84 }, "F4930 region gate");
                // F4930: test esi,esi (profile); jz F32B0; call F3060; test eax,eax; jnz (success).
                UnitCommandPathRuntime.ValidateExactBytes(memory, 0xF4A09, new byte[] { 0x85,0xF6,0x74,0x7F,0xE8,0x4E,0xE6,0xFF,0xFF,0x85,0xC0,0x75,0x7F }, "F4930 profile branch");
                // F32B0/F3060 entries (called natively) and DA590's connection/stamp load (BugfixesAndQoL's cursor proof contract).
                UnitCommandPathRuntime.ValidateExactBytes(memory, 0xF32B0, new byte[] { 0x48,0x89,0x5C,0x24,0x08,0x48,0x89,0x6C,0x24,0x10 }, "F32B0 entry");
                UnitCommandPathRuntime.ValidateExactBytes(memory, 0xF3060, new byte[] { 0x48,0x89,0x5C,0x24,0x10,0x48,0x89,0x6C,0x24,0x18 }, "F3060 entry");
                UnitCommandPathRuntime.ValidateExactBytes(memory, 0xDA783, new byte[] { 0x44,0x0F,0xBE,0x9C,0x17,0xD0,0x90,0x18,0x05,0x66,0x44,0x39,0x84,0x7A,0x4E,0x25,0x2C,0x05 }, "DA590 connections/stamps");
                ushort* rows = SHCDESE.API.GameTileManagerAPI.Instance.MapColumnLookupTable;
                if (rows == null || runtime.nativeProbeGrid == null || runtime.nativeMovementMasks == null || runtime.nativeDirectionMasks == null || runtime.pathRegionGrid == null)
                    throw new InvalidOperationException("native tables unavailable");
                var directLarge = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<GroundFailureProofs.DirectAttemptDelegate>((IntPtr)(libraryBase + 0xF32B0));
                var directDefault = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<GroundFailureProofs.DirectAttemptDelegate>((IntPtr)(libraryBase + 0xF3060));
                // The same address F4930 calls (a hook on E2F60 runs as in Vanilla's call).
                var regionsConnected = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<GroundFailureProofs.RegionsConnectedDelegate>((IntPtr)(libraryBase + 0xE2F60));
                groundFailureProofs = new GroundFailureProofs((short*)(runtime.nativeProbeGrid + UnitCommandPathRuntime.NativeProbeGridBytes / 2),
                    runtime.nativeMovementMasks, runtime.nativeDirectionMasks, (int*)(libraryBase + 0x405EDB0), rows, runtime.pathRegionGrid,
                    SHCDESE.API.GameTileManagerAPI.Instance.GetTileId, directLarge, directDefault, regionsConnected);
                Shared.DebugLogHelper.LogInfo(log, $"MoatMove failed-ground-build proofs ready; method=reference-rva verify={GroundFailureVerify}.");
            }
            catch (Exception ex)
            {
                groundFailureProofs = null;
                Shared.DebugLogHelper.LogError(log, "MoatMove failed-ground-build proofs unavailable; Vanilla builds unchanged: " + ex.Message);
            }
        }

        // MoatFailureProofs replays F4930's moat-mode path up to DAAC0 and relies on DAFD0's exhaustive search
        // and its edge inputs; these windows pin exactly the audited code (FBCB9319; UpdateToNewDLL.md "Failed
        // moat-mode builds"). DAFD0's and DAAC0's direction-byte loads are left out: APIShared's gate filter
        // hooks them. Any mismatch leaves the feature off and every build keeps Vanilla's path.
        private static void ValidatePin(ReadOnlySpan<byte> memory, int rva, string hex, string name)
        {
            string[] parts = hex.Split(' ');
            var bytes = new byte[parts.Length];
            for (int i = 0; i < parts.Length; i++) bytes[i] = Convert.ToByte(parts[i], 16);
            UnitCommandPathRuntime.ValidateExactBytes(memory, rva, bytes, name);
        }

        private void InstallMoatFailureProofs(ReadOnlySpan<byte> memory, ulong libraryBase)
        {
            if (!anchorAvailable) return;
            try
            {
                ValidatePin(memory, 0xF49D5, "33 ED C7 43 7C 01 00 00 00 89 AB 68 5F 15 00 39 AB 84 00 00 00 75 2A", "F4930 moat-mode entry writes");
                ValidatePin(memory, 0xF4A16, "8B 43 14 48 8D 0D 30 55 3B 03 44 8B 4B 10 44 8B 43 0C 8B 53 08 FF 83 AC 00 00 00 89 44 24 20 89 6B 7C E8 83 2F F1 FF 8B 05 19 55 3B 03 83 C0 04 89 AB 68 5F 15 00 0F AF C0 39 AB 80 00 00 00 74 49", "F4930 failure writes, 79C0, budget");
                ValidatePin(memory, 0xF4AA0, "39 AB 88 00 00 00 74 2E", "F4930 +0x88 branch");
                ValidatePin(memory, 0xF4AD6, "44 8B 4B 10 03 C0 44 8B 43 0C 48 8B CB 8B 53 08 89 6C 24 30 89 44 24 28 8B 43 14 89 44 24 20 E8 C6 5F FE FF 85 C0 75 5C", "F4930 DAAC0 call");
                ValidatePin(memory, 0xF4AFE, "8B 43 14 44 8B 4B 10 44 8B 43 0C 8B 53 08 39 AB 88 00 00 00 74 1D 89 6C 24 30 48 8B CB C7 44 24 28 80 1A 06 00 89 44 24 20 E8 14 51 FE FF E9 4C FF FF FF 39 AB 84 00 00 00 0F 84 28 FF FF FF 89 6C 24 30 48 8B CB C7 44 24 28 80 1A 06 00 89 44 24 20 E8 7B 64 FE FF E9 23 FF FF FF", "F4930 DAFD0 dispatch");
                ValidatePin(memory, 0xF4A7D, "85 C0 0F 85 D5 00 00 00 33 C0 E9 A4 01 00 00", "F4930 failure return");
                ValidatePin(memory, 0xDB004, "48 69 D5 20 03 00 00 4C 8D 05 EE 4F F2 FF 48 03 D6 42 80 BC 02 A4 1E A1 03 00", "DAFD0 start validity");
                ValidatePin(memory, 0xDB070, "41 FF 47 04 41 81 7F 04 00 7D 00 00 7E 26", "DAFD0 generation step");
                ValidatePin(memory, 0xDB164, "3B 84 24 B8 00 00 00 0F 8D 9E 04 00 00 41 81 FD 1F E5 04 00", "DAFD0 ring-head budget compare");
                ValidatePin(memory, 0xDB519, "66 42 89 84 73 10 5B 22 05 41 0F B7 47 04 66 42 89 84 73 50 25 2C 05", "DAFD0 distance and stamp write");
                ValidatePin(memory, 0xDB293, "48 8D 0D A6 31 20 03 E8 C1 BE 02 00", "DAFD0 special-structure call");
                ValidatePin(memory, 0xDB442, "43 0F BF 94 70 50 AA B6 04 48 8D 0D 5E 17 3F 06 E8 69 53 FE FF", "DAFD0 height offset call");
                ValidatePin(memory, 0x107160, "48 63 C2 48 8D 15 A6 6E 9C 04 48 0F BF 14 42 66 85 D2 74 1B 48 69 D2 9C 00 00 00 0F B7 44 0A 6A 66 83 F8 05 7C 09 66 83 F8 0F 74 03 B0 01 C3", "107160 special-structure predicate");
                ValidatePin(memory, 0xC07C0, "48 63 C2 48 69 D0 2C 03 00 00 0F BF 84 0A 2E 01 00 00", "C07C0 type word");
                // 107160: lea rdx,[rip+0x049C6EA6] at +3 (7 bytes): tile -> special-structure index.
                const int SpecialTilesRva = 0x107160 + 10 + 0x049C6EA6;
                ushort* rows = SHCDESE.API.GameTileManagerAPI.Instance.MapColumnLookupTable;
                if (rows == null || runtime.nativeProbeGrid == null || runtime.movementTargetAvailability == null || runtime.nativeMovementMasks == null ||
                    runtime.tileFlags == null || runtime.nativeHeightLayer == null || runtime.nativeBuildingLayer == null || runtime.nativeRowLookup == null ||
                    runtime.nativeProbeRectangle == null || runtime.nativeSpecialStructureContext == IntPtr.Zero)
                    throw new InvalidOperationException("native tables unavailable");
                var distance = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<MoatFailureProofs.DistanceDelegate>((IntPtr)(libraryBase + 0x79C0));
                // The address F4930 calls (APIShared's gate filter inside DAAC0 applies as in Vanilla's call).
                var firstSearch = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<MoatFailureProofs.BreadthSearchDelegate>((IntPtr)(libraryBase + 0xDAAC0));
                moatFailureProofs = new MoatFailureProofs((short*)(runtime.nativeProbeGrid + UnitCommandPathRuntime.NativeProbeGridBytes / 2),
                    runtime.movementTargetAvailability, runtime.nativeMovementMasks, runtime.tileFlags, runtime.nativeHeightLayer, runtime.nativeBuildingLayer,
                    (byte*)(libraryBase + UnitCommandPathRuntime.NativeBuildingTypeBiasRva), (short*)(libraryBase + SpecialTilesRva),
                    (byte*)runtime.nativeSpecialStructureContext.ToPointer(), rows, runtime.nativeRowLookup, (int*)(libraryBase + 0x405EDB0),
                    (IntPtr)runtime.nativeProbeRectangle, distance, firstSearch);
#if SERP_DEVDIAG
                moatFailureProofs.VerifySearch = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<MoatFailureProofs.BreadthSearchDelegate>((IntPtr)(libraryBase + 0xDAFD0));
#endif
                Shared.DebugLogHelper.LogInfo(log, $"MoatMove failed-moat-build proofs ready; method=reference-rva verify={GroundFailureVerify}.");
            }
            catch (Exception ex)
            {
                moatFailureProofs = null;
                Shared.DebugLogHelper.LogError(log, "MoatMove failed-moat-build proofs unavailable; Vanilla builds unchanged: " + ex.Message);
            }
        }
    }
}
