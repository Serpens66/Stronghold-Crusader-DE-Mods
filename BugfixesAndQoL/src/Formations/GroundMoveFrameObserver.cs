using System;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using SHCDESE.API.LowLevel;

namespace BugfixesAndQoL.UnitCommands
{
    // FBCB9319: DLL_RunTick calls 41D60 after 8C5F0 has selected the cursor.
    // The renderer clears cursor kind at 45702; ResetDrawList is already too late.
    // Entry has six integer/pointer ABI arguments, no live incoming flags or XMM arguments.
    internal sealed class GroundMoveFrameObserver
    {
        internal const int Rva = 0x41D60, DisplacedBytes = 15;
        internal static readonly byte[] EntryBytes = {
            0x48,0x89,0x5C,0x24,0x10, 0x48,0x89,0x6C,0x24,0x18, 0x48,0x89,0x74,0x24,0x20 };
        private readonly IntPtr demand = Marshal.AllocHGlobal(1);
        private X64InlineHook hook;
        private readonly Action observe;

        internal GroundMoveFrameObserver(Action observe)
        {
            this.observe = observe ?? throw new ArgumentNullException(nameof(observe));
            Marshal.WriteByte(demand, 0);
        }

        internal void Install(CrusaderLibraryLoadContext context, ManualLogSource log)
        {
            if (!Shared.DebugLogHelper.IsCurrentNativeLibraryVersion() ||
                !context.Memory.Slice(Rva, EntryBytes.Length).SequenceEqual(EntryBytes))
                throw new InvalidOperationException("Ground feedback render-entry contract changed.");
            IntPtr address = IntPtr.Add(context.ModuleHandle, Rva);
            var live = new byte[EntryBytes.Length];
            Marshal.Copy(address, live, 0, live.Length);
            if (!new ReadOnlySpan<byte>(live).SequenceEqual(EntryBytes))
                throw new InvalidOperationException("Another hook overlaps ground feedback render entry.");
            hook = BugfixesHookInfrastructure.CreateDemandGatedContextHook(
                unchecked((ulong)address.ToInt64()), 14, OnFrame,
                new ContextHookOptions { Registers = X64SmartCPUContextRegs.All, HookSize = 14,
                    Placement = OverwrittenInstructionPlacement.AfterCallback,
                    ErrorMode = CallbackErrorMode.LogAndContinue }, demand, "groundMoveFrame");
            if (hook.DisplacedByteCount != DisplacedBytes)
            {
                hook.Dispose(); hook = null; // Private, never enabled.
                throw new InvalidOperationException("Ground feedback render-entry span changed.");
            }
            hook.Enable(); // Permanent, rooted by FormationFeature before initialization.
            Shared.DebugLogHelper.LogInfo(log,
                "Ground move feedback installed: reference-rva=0x41D60 span=15 resume=0x41D6F; native demand gate.");
        }

        internal void SetActive(bool active) => Marshal.WriteByte(demand,
            active && hook != null && hook.IsInstalled ? (byte)1 : (byte)0);

        private void OnFrame(NativePointer<X64SmartCPUContext> context) => observe();
    }
}
