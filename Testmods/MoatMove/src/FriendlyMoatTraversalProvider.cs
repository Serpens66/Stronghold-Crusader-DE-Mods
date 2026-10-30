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
    }
}
