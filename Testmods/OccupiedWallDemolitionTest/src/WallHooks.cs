using System;
using System.Linq;
using System.Runtime.InteropServices;
using APIShared.NativeSites;
using BepInEx.Logging;
using CrusaderDE;
using Iced.Intel;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using SHCDESE.API.LowLevel;
using Shared;
using static Iced.Intel.AssemblerRegisters;

namespace OccupiedWallDemolitionTest
{
    internal sealed unsafe class WallHooks
    {
        private const string Feature = "occupied-wall-demolition";
        private const int Query = 0x79F84;
        private const int QueryOriginal = 0x7A044;
        private const int QuerySkip = 0x7A058;
        private const int Execute = 0x63749;
        private const int ExecuteBlocked = 0x63842;
        private const int Cursor = 0x3A11DC0;
        private static readonly byte[] QueryBytes = { 0x89, 0xB3, 0x6C, 0xE7, 0x04, 0x02, 0x66, 0x85, 0x84, 0x4B, 0x40, 0x5F, 0xF0, 0x00, 0x0F, 0x84, 0xAC, 0, 0, 0 };
        private static readonly byte[] ExecuteBytes = { 0x66, 0x83, 0xBC, 0x79, 0, 0x6C, 0xBF, 0, 0, 0x0F, 0x85, 0xEA, 0, 0, 0 };

        private readonly IntPtr module;
        private readonly IntPtr activeTile;
        private readonly X64InlineHook queryHook;
        private readonly X64InlineHook executeHook;
        private readonly Func<bool> integrity;
        private bool missionAllowed;
        internal bool Ready { get; private set; }

        internal WallHooks(CrusaderLibraryLoadContext context, ManualLogSource log)
        {
            module = context.ModuleHandle;
            if (module == IntPtr.Zero || !DebugLogHelper.IsCurrentNativeLibraryVersion())
                throw new InvalidOperationException("Unsupported CrusaderDE.dll.");

            NativePatchGuard.Bind(context, log);
            RequireReference(context, Query, QueryBytes);
            RequireReference(context, Execute, ExecuteBytes);
            RequireReference(context, Query + QueryBytes.Length, new byte[] { 0x89, 0xB3, 0xFC, 0xE6, 0x04, 0x02 });
            RequireReference(context, QueryOriginal, new byte[] { 0x49, 0x63, 0xC6 });
            RequireReference(context, QuerySkip, new byte[] { 0x85, 0xED });
            RequireReference(context, Execute + ExecuteBytes.Length, new byte[] { 0x0F, 0xB6, 0x84, 0x0F, 0xC0, 0xCA, 0xDC, 0x00 });
            RequireReference(context, ExecuteBlocked, new byte[] { 0x4C, 0x8D, 0x5C, 0x24, 0x60 });

            activeTile = Marshal.AllocHGlobal(sizeof(int));
            Disarm();
            bool claimed = false;
            try
            {
                queryHook = Prepare(Query, QueryBytes.Length, "WallPermission", EmitQuery);
                executeHook = Prepare(Execute, ExecuteBytes.Length, "WallExecution", EmitExecute);
                var claim = NativeSiteRegistry.Register(Plugin.Guid, Feature, new[] {
                    new NativeSiteSpan(Query, QueryBytes.Length, NativeSiteMode.Exclusive),
                    new NativeSiteSpan(Execute, ExecuteBytes.Length, NativeSiteMode.Exclusive)
                });
                if (!claim.Succeeded)
                    throw new InvalidOperationException(claim.Reason);
                claimed = true;

                NativePatchGuard.RequireUnpatched(queryHook, Feature);
                NativePatchGuard.RequireUnpatched(executeHook, Feature);
                queryHook.Enable();
                executeHook.Enable();
                VerifyPublished(queryHook);
                VerifyPublished(executeHook);
                NativePatchGuard.Watch(queryHook, Feature);
                NativePatchGuard.Watch(executeHook, Feature);
                integrity = NativePatchGuard.CaptureIntegrity(new[] { queryHook, executeHook }, Feature);
                log.LogInfo(Plugin.Stamp("WALL_HOOKS_READY method=reference-rva query=0x79F84+20 next=0x79F98 execution=0x63749+15 next=0x63758"));
                Ready = true;
            }
            catch
            {
                executeHook?.Dispose();
                queryHook?.Dispose();
                if (claimed)
                    NativeSiteRegistry.Unregister(Plugin.Guid, Feature);
                Marshal.FreeHGlobal(activeTile);
                throw;
            }
        }

        internal void MissionChanged(bool allowed)
        {
            Disarm();
            NativePatchGuard.Recheck();
            Ready = integrity();
            missionAllowed = allowed;
        }

        internal void Arm(int tile)
        {
            if (!Ready || !missionAllowed || tile <= 0)
                throw new InvalidOperationException("Wall hooks have no validated local test mission or tile.");
            System.Threading.Volatile.Write(ref *(int*)activeTile, tile);
        }

        internal void Disarm() => System.Threading.Volatile.Write(ref *(int*)activeTile, -1);

        // The caller holds the engine monitor. Only reproduce the cursor input used by
        // an ordinary wall click and let Vanilla enqueue and dispatch the simulation command.
        internal void Delete(int player, int x, int y, int tile)
        {
            if (!missionAllowed)
                throw new InvalidOperationException("No local test mission for the demolition command.");
            Span<byte> cursor = new Span<byte>((byte*)module + Cursor, 0x80);
            Span<byte> previous = stackalloc byte[0x80];
            cursor.CopyTo(previous);
            try
            {
                cursor.Clear();
                int* input = (int*)((byte*)module + Cursor);
                input[0x08 / 4] = 1;
                input[0x0C / 4] = input[0x6C / 4] = x;
                input[0x10 / 4] = input[0x70 / 4] = y;
                input[0x14 / 4] = input[0x5C / 4] = input[0x68 / 4] = input[0x78 / 4] = tile;
                EngineInterface.DeleteBuilding(x, y, player, inGameNotEditor: true, mouseState: 1);
            }
            finally
            {
                previous.CopyTo(cursor);
            }
        }

        private delegate void Emit(Assembler assembler, ReadOnlySpan<Instruction> instructions, ulong resume);

        private X64InlineHook Prepare(int rva, int length, string name, Emit emit)
        {
            ulong address = Address(rva);
            NativePatchGuard.RequireUnpatched(address, length, name, Feature);
            var hook = new X64InlineHook(address, length, name: name);
            try
            {
                if (hook.DisplacedByteCount != length || hook.IsInstalled || hook.TargetAddress != address)
                    throw new InvalidOperationException($"{name} displacement mismatch: expected {length}, got {hook.DisplacedByteCount}.");
                hook.Generate((asm, original, resume) => {
                    if (resume != address + (uint)length)
                        throw new InvalidOperationException(name + " continuation mismatch.");
                    emit(asm, original, resume);
                });
                return hook;
            }
            catch
            {
                hook.Dispose();
                throw;
            }
        }

        private void EmitQuery(Assembler asm, ReadOnlySpan<Instruction> code, ulong resume)
        {
            if (code.Length != 3 || code[2].NearBranchTarget != Address(QueryOriginal))
                throw new InvalidOperationException("Wall query branch changed.");
            var pending = asm.CreateLabel();
            var bypass = asm.CreateLabel();
            asm.AddInstruction(code[0]);
            asm.AddInstruction(code[1]);
            asm.jne(pending);
            CompareActiveTile(asm, r14d);
            asm.je(bypass);
            RestoreRegisters(asm);
            asm.AddUnrestrictedJmp(Address(QueryOriginal));
            asm.Label(ref bypass);
            RestoreRegisters(asm);
            asm.AddUnrestrictedJmp(Address(QuerySkip));
            asm.Label(ref pending);
            asm.nop(); // Fall through to the native pending-removal rejection.
        }

        private void EmitExecute(Assembler asm, ReadOnlySpan<Instruction> code, ulong resume)
        {
            if (code.Length != 2 || code[1].NearBranchTarget != Address(ExecuteBlocked))
                throw new InvalidOperationException("Wall execution branch changed.");
            var proceed = asm.CreateLabel();
            var bypass = asm.CreateLabel();
            asm.AddInstruction(code[0]);
            asm.je(proceed);
            CompareActiveTile(asm, edi);
            asm.je(bypass);
            RestoreRegisters(asm);
            asm.AddUnrestrictedJmp(Address(ExecuteBlocked));
            asm.Label(ref bypass);
            RestoreRegisters(asm);
            asm.Label(ref proceed);
            asm.nop();
        }

        private void CompareActiveTile(Assembler asm, AssemblerRegister32 tile)
        {
            asm.pushfq();
            asm.push(rax);
            asm.mov(rax, unchecked((ulong)activeTile.ToInt64()));
            asm.cmp(__dword_ptr[rax], tile);
        }

        private static void RestoreRegisters(Assembler asm)
        {
            asm.pop(rax);
            asm.popfq();
        }

        private static void VerifyPublished(X64InlineHook hook)
        {
            var bytes = Enumerable.Repeat((byte)0x90, hook.DisplacedByteCount).ToArray();
            bytes[0] = 0xFF;
            bytes[1] = 0x25;
            Array.Clear(bytes, 2, 4);
            BitConverter.GetBytes(hook.StubAddress.ToInt64()).CopyTo(bytes, 6);
            var actual = new ReadOnlySpan<byte>((void*)hook.TargetAddress, bytes.Length);
            if (!hook.IsInstalled || !actual.SequenceEqual(bytes))
                throw new InvalidOperationException(hook.Name + " did not publish the audited inline jump.");
        }

        private static void RequireReference(CrusaderLibraryLoadContext context, int rva, byte[] bytes)
        {
            if (!context.Memory.Slice(rva, bytes.Length).SequenceEqual(bytes))
                throw new InvalidOperationException($"Reference bytes differ at RVA 0x{rva:X}.");
        }

        private ulong Address(int rva) => unchecked((ulong)module.ToInt64()) + (uint)rva;
    }
}
