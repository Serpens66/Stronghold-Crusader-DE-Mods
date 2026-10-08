using APIShared;
using BepInEx;
using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace BugfixesAndQoL.GatehouseLivingCapture
{
    internal sealed unsafe class CaptureRuntime
    {
        private readonly ManualLogSource log;
        private readonly HookHandle<X64InlineHook> captureHook = new HookHandle<X64InlineHook>();
        private readonly CaptureCallback rootedCallback;
        private HookTransaction transaction;
        private bool published;
        private int active, confirmed, errorLogged;

        internal CaptureRuntime(ManualLogSource log)
        {
            this.log = log;
            rootedCallback = OnCandidate;
        }

        internal void Install(CrusaderLibraryLoadContext context, bool enableMod, bool enableFix)
        {
            NativeDefinition.ValidateLayout();
            string path = Path.Combine(Paths.GameRootPath,
                "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
            string hash;
            using (var sha = SHA256.Create())
            using (var input = File.OpenRead(path)) hash = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
            if (hash != NativeDefinition.Sha256)
                throw new InvalidOperationException("Unknown native field/layout contract: " + hash);
            if (context == null || context.ModuleHandle == IntPtr.Zero)
                throw new InvalidOperationException("Native library context is unavailable.");
            var resolved = Shared.NativePatternResolver.ResolveUnique(context.Memory, NativeDefinition.Pattern,
                NativeDefinition.HookRva, true, "gatehouse living capture filter", log);
            NativeDefinition.ValidateCode(context.Memory, resolved.Rva);
            ulong target = unchecked((ulong)context.ModuleHandle.ToInt64()) + (ulong)resolved.Rva;
            using (var probe = new X64InlineHook(target, NativeDefinition.HookLength))
                if (probe.DisplacedByteCount != NativeDefinition.HookLength)
                    throw new InvalidOperationException("Installed RedBird displaces an unexpected capture span.");
            ulong callback = unchecked((ulong)Marshal.GetFunctionPointerForDelegate(rootedCallback).ToInt64());
            CaptureAdapterEmitter.AssembleAndValidate(NativeDefinition.Original, target, callback, target + 0x2200000);
            try
            {
                transaction = new HookTransaction(context.Region, SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    // Ownership allows Dispose to restore only a failed unpublished candidate.
                    new HookTransactionOptions { FailureMode = TransactionFailureMode.RollbackAndThrow, OwnsHooks = true });
                transaction.AddInline(captureHook, HookTarget.FromAddress(target),
                    (a, original, ret) => CaptureAdapterEmitter.Emit(a, original, callback),
                    hookSize: NativeDefinition.HookLength);
                CommitResult result = transaction.Commit();
                if (!result.IsCompleteSuccess || !captureHook.Success ||
                    captureHook.Hook.DisplacedByteCount != NativeDefinition.HookLength ||
                    captureHook.Hook.TargetAddress != target)
                    throw new InvalidOperationException("Capture hook transaction/committed span failed: " + result);
                ValidateCommittedPatch(target);
                // Publication happens only after the exact committed hook has been validated.
                published = true;
                SetEnabled(enableMod, enableFix);
            }
            catch
            {
                if (!published) transaction?.Dispose(); // Roll back only an unpublished initialization candidate.
                throw;
            }
            try { log.LogDebug($"GATEHOUSE_LIVING_CAPTURE_READY: method={resolved.Method}, rva=0x{resolved.Rva:X}, " +
                $"end=0x{NativeDefinition.ContinueRva:X}, skip=0x{NativeDefinition.SkipRva:X}, displaced=18 (2+7+9), " +
                $"redBird={typeof(X64InlineHook).Assembly.GetName().Version}, sha256={hash}; " +
                "GameUnit size=0x490, alive=0x88, deathLowWord=0x29C, owner=0x92, health=0x3C4; original cadence/scoring retained."); }
            catch { /* Installation diagnostics cannot deactivate a published hook. */ }
        }

        private static void ValidateCommittedPatch(ulong target)
        {
            byte* code = (byte*)target;
            if (code[0] != 0xFF || code[1] != 0x25 || *(uint*)(code + 2) != 0 || *(ulong*)(code + 6) == 0)
                throw new InvalidOperationException("Committed RedBird absolute-indirect jump differs.");
            for (int i = 14; i < NativeDefinition.HookLength; i++)
                if (code[i] != 0x90) throw new InvalidOperationException("Committed capture padding differs.");
            if (code[18] != 0x74 || code[19] != 0x63)
                throw new InvalidOperationException("Committed hook changed the native JE continuation.");
        }

        private void OnCandidate(IntPtr pointer)
        {
            X64SmartCPUContext* context = (X64SmartCPUContext*)pointer;
            ulong originalBoolean = context->R11;
            if (Volatile.Read(ref active) == 0 || (originalBoolean & 0xFF) == 0) return;
            GameUnit* unit;
            int unitId;
            try
            {
                // EAX is Vanilla's signed, one-based tile-chain ID; CDQE has already run.
                unitId = checked((int)(long)context->RAX);
                if (context->RDI != checked((ulong)unitId * NativeDefinition.UnitSize) ||
                    !UnitAccess.TryGetById(unitId, out unit, out var failure))
                    throw new InvalidOperationException("Capture candidate ID/lookup contract failed: " + unitId);
                CaptureDecision.Apply(context, unit, true);
            }
            catch (Exception ex)
            {
                context->R11 = originalBoolean;
                if (Interlocked.Exchange(ref errorLogged, 1) == 0)
                    TryLogError("GATEHOUSE_LIVING_CAPTURE_CALLBACK_ERROR: Vanilla preserved; " + ex);
                return;
            }
            // Diagnostics cannot undo/prevent the correction.
            try
            {
                if (Interlocked.Exchange(ref confirmed, 1) == 0)
                    LogInfo("GATEHOUSE_LIVING_CAPTURE_CONFIRMED: capture hook executed after startup.");
            }
            catch { /* Logging is observational only. */ }
        }

        internal void SetEnabled(bool enableMod, bool enableFix) =>
            Volatile.Write(ref active, published && CaptureDecision.IsEnabled(enableMod, enableFix) ? 1 : 0);

        private void LogInfo(string message) => log.LogDebug($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
        private void TryLogError(string message)
        {
            try { log.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}"); }
            catch { }
        }
    }
}
