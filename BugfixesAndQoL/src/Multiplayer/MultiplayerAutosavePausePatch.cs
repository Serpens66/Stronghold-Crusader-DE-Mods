using System;
using System.Runtime.InteropServices;
using APIShared.NativeSites;
using BepInEx.Logging;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL.Multiplayer
{
    internal static class MultiplayerAutosavePausePatch
    {
        private const string Feature = "Multiplayer autosave deferral";
        private const int Rva = 0x1F76D;
        private const int ReturnRva = 0x1F7D0;
        private const int GamePausedRva = 0x366A0F8;
        private const int LastAutosaveTime = 0x279F9C;
        private const int FirstPendingSlot = 0x370BF0;
        private const int PendingSlots = 0xB0BF8;
        private const int SlotSize = 0x500;
        private const int SlotCount = 500;
        private const int OpcodeOffset = 8;
        private const int StateOffset = 9;
        private const int ExecutedState = 10;
        private const int SaveChore = 39;
        private static readonly byte[] Original = {
            0x89, 0x83, 0xFC, 0x0D, 0, 0,
            0x48, 0x8D, 0x0D, 0xA6, 0x4B, 0x55, 0x08,
            0x89, 0x83, 0x9C, 0x9F, 0x27, 0
        };
        private static HookTransaction published;

        internal static void Install(CrusaderLibraryLoadContext context, bool referenceHashMatches, ManualLogSource log)
        {
            if (published != null) return;
            if (!referenceHashMatches || !context.Memory.Slice(Rva, Original.Length).SequenceEqual(Original))
                throw new InvalidOperationException("The multiplayer autosave scheduler differs from the audited native layout.");

            ulong module = unchecked((ulong)context.ModuleHandle.ToInt64());
            ulong address = module + Rva;
            Shared.NativePatchGuard.RequireUnpatched(address, Original.Length, Feature, Feature);
            var claim = NativeSiteRegistry.Register(BugfixesAndQoLPlugin.PluginGuid, Feature,
                Rva, Original.Length, NativeSiteMode.Exclusive);
            if (!claim.Succeeded) throw new InvalidOperationException(claim.Reason);

            HookTransaction candidate = null;
            try
            {
                candidate = BugfixesHookInfrastructure.CreateOwnedTransaction(context.Region);
                var handle = new HookHandle<X64InlineHook>();
                candidate.AddInline(handle, HookTarget.FromAddress(address), (asm, instructions, _) =>
                    Emit(asm, instructions, module + GamePausedRva, module + ReturnRva),
                    hookSize: Original.Length, name: Feature);
                var result = candidate.Commit();
                var hook = handle.Hook;
                if (!result.IsCompleteSuccess || !handle.Success || hook == null || hook.IsInstalled ||
                    hook.TargetAddress != address || hook.DisplacedByteCount != Original.Length ||
                    hook.StubAddress == IntPtr.Zero)
                    throw new InvalidOperationException("The prepared autosave hook span differs from the audited instructions.");

                Shared.NativePatchGuard.RequireUnpatched(hook, Feature);
                published = candidate;
                hook.Enable();
                var site = context.ModuleHandle + Rva;
                if (!hook.IsInstalled || Marshal.ReadByte(site) != 0xFF || Marshal.ReadByte(site, 1) != 0x25 ||
                    Marshal.ReadInt32(site, 2) != 0 || Marshal.ReadIntPtr(site, 6) != hook.StubAddress)
                    throw new InvalidOperationException("The published autosave hook jump differs from the audited backend.");
                Shared.NativePatchGuard.Watch(hook, Feature);
                Shared.DebugLogHelper.LogInfo(log,
                    "MP_AUTOSAVE_DEFERRAL_READY method=reference-rva span=[0x1F76D,0x1F780) instructions=6+7+6");
            }
            catch
            {
                if (published == null)
                {
                    candidate?.Dispose();
                    NativeSiteRegistry.Unregister(BugfixesAndQoLPlugin.PluginGuid, Feature);
                }
                throw;
            }
        }

        private static void Emit(Assembler asm, ReadOnlySpan<Instruction> instructions, ulong pause, ulong exit)
        {
            var scan = asm.CreateLabel();
            var next = asm.CreateLabel();
            var resume = asm.CreateLabel();
            var coalesce = asm.CreateLabel();
            var defer = asm.CreateLabel();

            // Only reached after the Vanilla wall-clock deadline. EAX holds the
            // timestamp and RBX the network state. R10/R11 and flags are dead here.
            asm.mov(r10, pause);
            asm.cmp(__dword_ptr[r10], 0);
            asm.jne(defer);
            asm.mov(r11d, __dword_ptr[rbx + FirstPendingSlot]);
            asm.cmp(r11d, SlotCount);
            asm.jae(resume);
            asm.imul(r10d, r11d, SlotSize);
            asm.lea(r10, __qword_ptr[rbx + r10 + PendingSlots]);

            // A save queued before pausing may still be pending when the timer
            // becomes due again. Let the original queue finish it first.
            asm.Label(ref scan);
            asm.cmp(__byte_ptr[r10 + OpcodeOffset], SaveChore);
            asm.jne(next);
            asm.movzx(ecx, __byte_ptr[r10 + StateOffset]);
            asm.dec(ecx);
            asm.cmp(ecx, ExecutedState - 1);
            asm.jb(coalesce);
            asm.Label(ref next);
            asm.add(r10, SlotSize);
            asm.inc(r11d);
            asm.cmp(r11d, SlotCount);
            asm.jb(scan);
            asm.jmp(resume);

            // The pending save covers this deadline too. Start the next
            // interval here, without touching Vanilla's request timestamp.
            asm.Label(ref coalesce);
            asm.mov(__dword_ptr[rbx + LastAutosaveTime], eax);
            asm.Label(ref defer);
            asm.mov(r10, exit);
            asm.jmp(r10);
            asm.Label(ref resume);
            asm.AddInstructions(instructions);
        }
    }
}
