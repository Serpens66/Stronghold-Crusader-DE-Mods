using BepInEx.Logging;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL
{
    internal static class RaidSearchObserver
    {
        internal const int SiteRva = 0x11FFA7, SpanLength = 17, ContextRva = 0x60AD660;
        internal static readonly byte[] ExpectedBytes = {
            0xE8,0xE4,0x30,0,0,0x39,0x35,0xF6,0x89,0xFA,0x05,0x0F,0x84,0x68,0x1B,0,0 };
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void ObserverDelegate(int tribeId, int buildingId, IntPtr context);
        private static readonly ObserverDelegate observer = Observe;
        private static readonly HookHandle<X64InlineHook> handle = new HookHandle<X64InlineHook>();
        private static HookTransaction transaction;
        private static Action<int, int, IntPtr> receiver;
        private static ManualLogSource logger;
        private static int enabled, callbackErrorReported;
        internal static bool IsAvailable => Volatile.Read(ref enabled) != 0;

        internal static void Install(CrusaderLibraryLoadContext context, ManualLogSource log,
            Action<int, int, IntPtr> callback)
        {
            if (transaction != null) return;
            HookTransaction pending = null;
            bool published = false;
            try
            {
                ulong module = unchecked((ulong)context.ModuleHandle.ToInt64());
                if (module == 0 || !Shared.DebugLogHelper.IsCurrentNativeLibraryVersion())
                    throw new InvalidOperationException("Native identity or module unavailable.");
                for (int i = 0; i < ExpectedBytes.Length; i++)
                    if (context.Memory[SiteRva + i] != ExpectedBytes[i] ||
                        Marshal.ReadByte(context.ModuleHandle, SiteRva + i) != ExpectedBytes[i])
                        throw new InvalidOperationException("Observation span differs or overlaps another hook.");
                logger = log;
                receiver = callback;
                ulong callbackAddress = unchecked((ulong)Marshal.GetFunctionPointerForDelegate(observer).ToInt64());
                pending = new HookTransaction(context.Region, options: new HookTransactionOptions {
                    OwnsHooks = true, FailureMode = TransactionFailureMode.RollbackAndThrow });
                pending.AddInline(handle, HookTarget.FromAddress(module + SiteRva),
                    (asm, original, continuation) => {
                        ValidateOriginal(original, module, continuation);
                        Emit(asm, original, callbackAddress, module + ContextRva);
                    }, hookSize: 14);
                var result = pending.Commit();
                if (!result.IsCompleteSuccess || !handle.Success || !handle.IsInstalled ||
                    handle.Failure != null || handle.ResolvedAddress != module + SiteRva ||
                    handle.Require().DisplacedByteCount != SpanLength)
                    throw new InvalidOperationException("Observation hook commit/span mismatch.");
                transaction = pending;
                published = true;
                Volatile.Write(ref enabled, 1);
                Shared.DebugLogHelper.LogDebug(log,
                    $"AI_RAID_HOOK: method=reference-rva,rva=0x{SiteRva:X}," +
                    $"address=0x{module + SiteRva:X},displaced={SpanLength},continuation=0x{module + SiteRva + SpanLength:X}," +
                    "backend=X64InlineHook,observer=readOnly,classification=authoritative.");
            }
            catch (Exception ex)
            {
                // Only an unpublished initialization candidate may be rolled back.
                if (!published)
                {
                    try { pending?.Dispose(); }
                    catch (Exception rollback) { SafeError(log, "AI_RAID_SEARCH_ROLLBACK_ERROR: " + rollback); }
                }
                SafeError(log, "AI_RAID_HOOK_UNAVAILABLE: " + ex);
            }
        }

        internal static void ValidateOriginal(ReadOnlySpan<Instruction> original, ulong module, ulong continuation)
        {
            if (continuation != module + SiteRva + SpanLength || original.Length != 3 ||
                original[0].Code != Code.Call_rel32_64 || original[0].NearBranch64 != module + 0x123090 ||
                original[1].Mnemonic != Mnemonic.Cmp || original[1].MemoryDisplacement64 != module + 0x60C89A8 ||
                original[1].Op1Register != Register.ESI || original[2].Mnemonic != Mnemonic.Je ||
                original[2].NearBranch64 != module + 0x121B20)
                throw new InvalidOperationException("Consumer call/first-field gate relocation contract changed.");
        }

        internal static void Emit(Assembler asm, ReadOnlySpan<Instruction> original, ulong callback, ulong context)
        {
            // Native RSP is aligned at the three-argument Win64 consumer call.
            // Execute the live filter (including other mods) once at the original RSP.
            asm.AddInstruction(original[0]);
            // LEA preserves flags. All volatile GPRs and XMM0..5 are saved;
            // Win64 callees preserve nonvolatile GPRs and XMM6..15.
            asm.lea(rsp, __qword_ptr[rsp - 0xD0]);
            asm.mov(__qword_ptr[rsp + 0x20], rax);
            asm.mov(__qword_ptr[rsp + 0x28], rcx); asm.mov(__qword_ptr[rsp + 0x30], rdx);
            asm.mov(__qword_ptr[rsp + 0x38], r8); asm.mov(__qword_ptr[rsp + 0x40], r9);
            asm.mov(__qword_ptr[rsp + 0x48], r10); asm.mov(__qword_ptr[rsp + 0x50], r11);
            asm.movdqu(__xmmword_ptr[rsp + 0x60], xmm0); asm.movdqu(__xmmword_ptr[rsp + 0x70], xmm1);
            asm.movdqu(__xmmword_ptr[rsp + 0x80], xmm2); asm.movdqu(__xmmword_ptr[rsp + 0x90], xmm3);
            asm.movdqu(__xmmword_ptr[rsp + 0xA0], xmm4); asm.movdqu(__xmmword_ptr[rsp + 0xB0], xmm5);
            asm.pushfq(); asm.pop(rax); asm.mov(__qword_ptr[rsp + 0xC0], rax);
            asm.mov(ecx, edi); asm.mov(edx, r14d); asm.mov(r8, context);
            asm.mov(rax, callback); asm.call(rax);
            asm.movdqu(xmm0, __xmmword_ptr[rsp + 0x60]); asm.movdqu(xmm1, __xmmword_ptr[rsp + 0x70]);
            asm.movdqu(xmm2, __xmmword_ptr[rsp + 0x80]); asm.movdqu(xmm3, __xmmword_ptr[rsp + 0x90]);
            asm.movdqu(xmm4, __xmmword_ptr[rsp + 0xA0]); asm.movdqu(xmm5, __xmmword_ptr[rsp + 0xB0]);
            asm.push(__qword_ptr[rsp + 0xC0]); asm.popfq();
            asm.mov(rax, __qword_ptr[rsp + 0x20]); asm.mov(rcx, __qword_ptr[rsp + 0x28]);
            asm.mov(rdx, __qword_ptr[rsp + 0x30]); asm.mov(r8, __qword_ptr[rsp + 0x38]);
            asm.mov(r9, __qword_ptr[rsp + 0x40]); asm.mov(r10, __qword_ptr[rsp + 0x48]);
            asm.mov(r11, __qword_ptr[rsp + 0x50]); asm.lea(rsp, __qword_ptr[rsp + 0xD0]);
            // CMP recomputes flags for JE; no ContextHook flag-wrapper is used.
            asm.AddInstruction(original[1]); asm.AddInstruction(original[2]);
        }

        private static void Observe(int tribeId, int buildingId, IntPtr context)
        {
            if (Volatile.Read(ref enabled) == 0) return;
            try { receiver?.Invoke(tribeId, buildingId, context); }
            catch (Exception ex)
            {
                if (Interlocked.Exchange(ref callbackErrorReported, 1) == 0)
                    SafeError(logger, "AI_RAID_SEARCH_CALLBACK_ERROR: " + ex);
            }
        }

        private static void SafeError(ManualLogSource log, string message)
        {
            // Neither an observation nor its error reporting may escape into Vanilla.
            try { Shared.DebugLogHelper.LogError(log, message); } catch { }
        }
    }
}
