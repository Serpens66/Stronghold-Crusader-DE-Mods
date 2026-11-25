using System;
using System.Runtime.InteropServices;
using System.Threading;
using APIShared.Multiplayer;
using APIShared.NativeSites;
using BepInEx.Logging;
using Iced.Intel;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using Shared;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL.AreaDemolition
{
    internal static unsafe class OccupiedWallDemolition
    {
        private const string Feature = "occupied-wall-demolition";
        private const int QueryRva = 0x79F84, QueryOriginal = 0x7A044, QuerySkipOccupancy = 0x7A058;
        private const int ExecuteRva = 0x63735, ExecuteBlocked = 0x63842;
        private const int WallOwnerGrid = 0xE1AFE0, LogicGrid = 0x898400, StructureGrid = 0xB0BCA0;

        private static readonly byte[] QueryBytes =
        {
            0x89, 0xB3, 0x6C, 0xE7, 0x04, 0x02,
            0x66, 0x85, 0x84, 0x4B, 0x40, 0x5F, 0xF0, 0x00,
            0x0F, 0x84, 0xAC, 0, 0, 0
        };
        private static readonly byte[] ExecuteBytes =
        {
            0x4B, 0x8D, 0x14, 0x76,
            0x41, 0x2B, 0xAC, 0x97, 0x2C, 0xFF, 0x02, 0x04,
            0x66, 0x21, 0x84, 0x79, 0x40, 0x5F, 0xF0, 0x00,
            0x66, 0x83, 0xBC, 0x79, 0, 0x6C, 0xBF, 0, 0,
            0x0F, 0x85, 0xEA, 0, 0, 0
        };

        private static HookTransaction retainedTransaction;
        private static IntPtr activeFlag;
        private static Func<bool> integrity;
        private static bool attempted, agreed;

        internal static void RegisterAgreement() =>
            SimulationAgreement.Register(BugfixesAndQoLPlugin.PluginGuid, Feature, false, IsReady, SetAgreement);

        private static bool IsReady() => Volatile.Read(ref integrity)?.Invoke() == true;

        // Assessment in the lobby is independent of the option. The common host setting is
        // read once after mission settings have been applied, and held for the active world.
        private static void SetAgreement(bool enabled) => Volatile.Write(ref agreed, enabled);

        internal static void Install(CrusaderLibraryLoadContext context, ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            if (attempted) return;
            attempted = true;
            HookTransaction candidate = null;
            IntPtr flag = IntPtr.Zero;
            bool claimed = false, publicationStarted = false;
            try
            {
                if (context == null || context.ModuleHandle == IntPtr.Zero || !DebugLogHelper.IsCurrentNativeLibraryVersion())
                    throw new InvalidOperationException("Requires the audited CrusaderDE.dll.");

                ValidateReference(context.Memory, QueryRva, QueryBytes);
                ValidateReference(context.Memory, QueryOriginal, new byte[] { 0x49, 0x63, 0xC6 });
                ValidateReference(context.Memory, QuerySkipOccupancy, new byte[] { 0x85, 0xED });
                ValidateReference(context.Memory, QueryRva + QueryBytes.Length, new byte[] { 0x89, 0xB3, 0xFC, 0xE6, 0x04, 0x02 });
                ValidateReference(context.Memory, ExecuteRva, ExecuteBytes);
                ValidateReference(context.Memory, ExecuteRva + ExecuteBytes.Length, new byte[] { 0x0F, 0xB6, 0x84, 0x0F, 0xC0, 0xCA, 0xDC, 0x00 });
                ValidateReference(context.Memory, ExecuteBlocked, new byte[] { 0x4C, 0x8D, 0x5C, 0x24, 0x60 });

                ulong image = unchecked((ulong)context.ModuleHandle.ToInt64());
                NativePatchGuard.RequireUnpatched(image + QueryRva, QueryBytes.Length, "wall permission", Feature);
                NativePatchGuard.RequireUnpatched(image + ExecuteRva, ExecuteBytes.Length, "wall execution", Feature);
                var registration = NativeSiteRegistry.Register(BugfixesAndQoLPlugin.PluginGuid, Feature, new[]
                {
                    new NativeSiteSpan(QueryRva, QueryBytes.Length, NativeSiteMode.Exclusive),
                    new NativeSiteSpan(ExecuteRva, ExecuteBytes.Length, NativeSiteMode.Exclusive)
                });
                if (!registration.Succeeded) throw new InvalidOperationException(registration.Reason);
                claimed = true;

                flag = Marshal.AllocHGlobal(sizeof(int));
                Marshal.WriteInt32(flag, 0);
                ulong state = unchecked((ulong)flag.ToInt64());
                candidate = BugfixesHookInfrastructure.CreateOwnedTransaction(context.Region, installOnCommit: false);
                var query = new HookHandle<X64InlineHook>();
                var execute = new HookHandle<X64InlineHook>();
                candidate.AddInline(query, HookTarget.FromAddress(image + QueryRva),
                    (asm, code, resume) => EmitQuery(asm, code, resume, image, state),
                    hookSize: QueryBytes.Length, name: "OccupiedWallPermission");
                candidate.AddInline(execute, HookTarget.FromAddress(image + ExecuteRva),
                    (asm, code, resume) => EmitExecute(asm, code, resume, image, state),
                    hookSize: ExecuteBytes.Length, name: "OccupiedWallExecution");
                var result = candidate.Commit();
                if (!result.IsCompleteSuccess)
                    throw new InvalidOperationException("Hook preparation failed: " + result);
                X64InlineHook[] hooks =
                {
                    Prepared(query, image + QueryRva, QueryBytes.Length),
                    Prepared(execute, image + ExecuteRva, ExecuteBytes.Length)
                };

                foreach (var hook in hooks) NativePatchGuard.RequireUnpatched(hook, Feature);
                publicationStarted = true;
                retainedTransaction = candidate;
                foreach (var hook in hooks) hook.Enable();
                foreach (var hook in hooks) VerifyPublished(hook);
                foreach (var hook in hooks) NativePatchGuard.Watch(hook, Feature);
                var check = NativePatchGuard.CaptureIntegrity(hooks, Feature);
                // Subscriptions and hooks survive the destruction of the early BepInEx component.
                MissionEvents.Ended.Subscribe(_ => Publish(false));
                activeFlag = flag;
                MissionEvents.Started.Subscribe(note =>
                {
                    bool multiplayer = note.Context.Mode.IsRealMultiplayer;
                    bool ready = check();
                    if (multiplayer && Volatile.Read(ref agreed) && !ready)
                        SimulationAgreement.ReportFault("The agreed occupied-wall demolition hooks changed during map start.");
                    Publish(ready && settings.EnableMod && settings.EnableOccupiedWallDemolition &&
                        (!multiplayer || Volatile.Read(ref agreed)));
                });
                Volatile.Write(ref integrity, check);
                DebugLogHelper.LogInfo(log, "OCCUPIED_WALLS_READY method=reference-rva query=0x79F84+20 (6/8/6) next=0x79F98 execution=0x63735+35 (4/8/8/9/6) next=0x63758");
            }
            catch (Exception error)
            {
                DebugLogHelper.LogHookInstallFailure(log, Feature, "wall permission and execution", error.ToString());
                if (!publicationStarted)
                {
                    candidate?.Dispose();
                    if (flag != IntPtr.Zero) Marshal.FreeHGlobal(flag);
                    if (claimed) NativeSiteRegistry.Unregister(BugfixesAndQoLPlugin.PluginGuid, Feature);
                }
            }
        }

        private static void Publish(bool enabled)
        {
            if (activeFlag != IntPtr.Zero)
                Volatile.Write(ref *(int*)activeFlag, enabled ? 1 : 0);
        }

        private static void EmitQuery(Assembler asm, ReadOnlySpan<Instruction> code, ulong resume, ulong image, ulong state)
        {
            if (resume != image + QueryRva + (uint)QueryBytes.Length || code.Length != 3 ||
                code[2].NearBranchTarget != image + QueryOriginal)
                throw new InvalidOperationException("Wall query branch differs.");
            Label pending = asm.CreateLabel();
            Label vanilla = asm.CreateLabel();
            asm.AddInstruction(code[0]);
            asm.AddInstruction(code[1]);
            asm.jne(pending);
            asm.push(rax);
            asm.mov(rax, state);
            asm.cmp(__dword_ptr[rax], 0);
            asm.je(vanilla);
            asm.pop(rax);
            asm.AddUnrestrictedJmp(image + QuerySkipOccupancy);
            asm.Label(ref vanilla);
            asm.pop(rax);
            asm.AddUnrestrictedJmp(image + QueryOriginal);
            asm.Label(ref pending);
            asm.nop(); // RedBird's continuation runs the native pending-removal rejection.
        }

        private static void EmitExecute(Assembler asm, ReadOnlySpan<Instruction> code, ulong resume, ulong image, ulong state)
        {
            if (resume != image + ExecuteRva + (uint)ExecuteBytes.Length || code.Length != 5 ||
                code[4].NearBranchTarget != image + ExecuteBlocked)
                throw new InvalidOperationException("Wall execution branch differs.");
            Label vanilla = asm.CreateLabel();
            Label remove = asm.CreateLabel();

            // EDX is still the original actor here. The two brush/cleanup callers pass zero.
            // Before entering the new route, check current ownership and the actual wall layer.
            asm.push(r11);
            asm.mov(r11, state);
            asm.cmp(__dword_ptr[r11], 0);
            asm.je(vanilla);
            asm.cmp(edx, 1);
            asm.jl(vanilla);
            asm.cmp(edx, 8);
            asm.jg(vanilla);
            asm.cmp(rdi, GameTileManagerView.NativePackedTileCapacity);
            asm.jae(vanilla);
            asm.test(__dword_ptr[rcx + rdi * 4 + LogicGrid], (int)TilePropertyFlag.IsWall);
            asm.je(vanilla);
            asm.cmp(__word_ptr[rcx + rdi * 2 + StructureGrid], 0);
            asm.jne(vanilla);
            asm.movzx(r11d, __byte_ptr[rcx + rdi + WallOwnerGrid]);
            asm.and(r11d, 7);
            asm.inc(r11d);
            asm.cmp(r11d, edx);
            asm.jne(vanilla);
            asm.pop(r11);
            asm.AddInstructions(code.Slice(0, 4).CloneInstructionsWithoutIP());
            asm.jmp(remove);

            asm.Label(ref vanilla);
            asm.pop(r11);
            asm.AddInstructions(code.Slice(0, 4).CloneInstructionsWithoutIP());
            asm.je(remove);
            asm.AddUnrestrictedJmp(image + ExecuteBlocked);
            asm.Label(ref remove);
            asm.nop();
        }

        private static X64InlineHook Prepared(HookHandle<X64InlineHook> handle, ulong address, int length)
        {
            var hook = handle.Hook;
            if (!handle.Success || hook == null || hook.IsInstalled || hook.TargetAddress != address ||
                hook.DisplacedByteCount != length || hook.StubAddress == IntPtr.Zero)
                throw new InvalidOperationException($"Prepared RedBird span at 0x{address:X} differs from {length} audited bytes.");
            return hook;
        }

        private static void VerifyPublished(X64InlineHook hook)
        {
            byte* site = (byte*)hook.TargetAddress;
            if (!hook.IsInstalled || site[0] != 0xFF || site[1] != 0x25 ||
                *(int*)(site + 2) != 0 || *(long*)(site + 6) != hook.StubAddress.ToInt64())
                throw new InvalidOperationException(hook.Name + " did not publish its audited indirect jump.");
            for (int i = 14; i < hook.DisplacedByteCount; i++)
                if (site[i] != 0x90) throw new InvalidOperationException(hook.Name + " has unexpected padding.");
        }

        private static void ValidateReference(ReadOnlySpan<byte> image, int rva, byte[] bytes)
        {
            if (!image.Slice(rva, bytes.Length).SequenceEqual(bytes))
                throw new InvalidOperationException($"Reference bytes differ at RVA 0x{rva:X}.");
        }
    }
}
