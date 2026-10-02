using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;
using System;
using System.Runtime.InteropServices;

namespace AIBuildDiagnoseTest
{
    // Diagnostic only: these two searchers have no existing main-mod hook.
    // The owning runtime is rooted by the plugin for the entire process.
    internal sealed class GeneralSiteSearchHooks
    {
        private const int AivSearchRva = 0x583A0;
        private const int FineSearchRva = 0x58BE0;
        private const int DisplacedLength = 6;
        private static readonly byte[] AivEntry = { 0x41, 0x57, 0x48, 0x83, 0xEC, 0x20 };
        private static readonly byte[] FineEntry = { 0x89, 0x54, 0x24, 0x10, 0x53, 0x55 };

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void AivSearchDelegate(ulong state, uint coarseX, uint coarseY);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void FineSearchDelegate(ulong state, int playerId,
            uint coarseX, uint coarseY);

        private readonly DetourHandle<AivSearchDelegate> aivHook =
            new DetourHandle<AivSearchDelegate>();
        private readonly DetourHandle<FineSearchDelegate> fineHook =
            new DetourHandle<FineSearchDelegate>();
        private readonly Action<string, int, int, int, int, int> report;
        private static GeneralSiteSearchHooks published;
        private HookTransaction transaction;
        private bool observationEnabled;

        internal GeneralSiteSearchHooks(CrusaderLibraryLoadContext context,
            Action<string, int, int, int, int, int> report)
        {
            this.report = report ?? throw new ArgumentNullException(nameof(report));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.Memory.Length < FineSearchRva + FineEntry.Length ||
                !context.Memory.Slice(AivSearchRva, AivEntry.Length).SequenceEqual(AivEntry) ||
                !context.Memory.Slice(FineSearchRva, FineEntry.Length).SequenceEqual(FineEntry))
                throw new InvalidOperationException("General AI search entries differ from the audited native bytes.");
            ulong module = unchecked((ulong)context.ModuleHandle.ToInt64());
            ulong aivTarget = module + AivSearchRva;
            ulong fineTarget = module + FineSearchRva;
            var aivRequest = new DetourRequest<AivSearchDelegate>
            {
                Name = "AI build diagnostic AIV search preflight",
                TargetAddress = aivTarget,
                Callback = AivSearch
            };
            var fineRequest = new DetourRequest<FineSearchDelegate>
            {
                Name = "AI build diagnostic fine search preflight",
                TargetAddress = fineTarget,
                Callback = FineSearch
            };
            NativeDetour<AivSearchDelegate> aivProbe = null;
            NativeDetour<FineSearchDelegate> fineProbe = null;
            try
            {
                aivProbe = NativeDetourBackend.Instance.CreateDetour(in aivRequest)
                    as NativeDetour<AivSearchDelegate>;
                fineProbe = NativeDetourBackend.Instance.CreateDetour(in fineRequest)
                    as NativeDetour<FineSearchDelegate>;
                Validate(aivProbe, aivTarget);
                Validate(fineProbe, fineTarget);
            }
            finally
            {
                aivProbe?.Dispose(); // Unpublished preflight candidates only.
                fineProbe?.Dispose();
            }
            var pending = new HookTransaction(context.Region,
                SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                new HookTransactionOptions
                {
                    FailureMode = TransactionFailureMode.RollbackAndThrow,
                    OwnsHooks = false
                });
            try
            {
                // Root the callbacks before any executable patch is published.
                published = this;
                pending.AddDetour(aivHook, HookTarget.FromAddress(aivTarget), AivSearch);
                pending.AddDetour(fineHook, HookTarget.FromAddress(fineTarget), FineSearch);
                CommitResult result = pending.Commit();
                if (!result.IsCompleteSuccess || !aivHook.Success || !fineHook.Success)
                    throw new InvalidOperationException("General AI search observation did not commit completely.");
                // Keep committed hooks rooted even if the post-commit contract check fails.
                transaction = pending;
                Validate(aivHook.Hook as NativeDetour<AivSearchDelegate>, aivTarget);
                Validate(fineHook.Hook as NativeDetour<FineSearchDelegate>, fineTarget);
                ValidateInstalled(aivHook.Hook as NativeDetour<AivSearchDelegate>, aivTarget);
                ValidateInstalled(fineHook.Hook as NativeDetour<FineSearchDelegate>, fineTarget);
                observationEnabled = true;
            }
            catch
            {
                if (!aivHook.Success && !fineHook.Success) pending.Dispose();
                throw;
            }
        }

        private static void Validate<T>(NativeDetour<T> hook, ulong target) where T : Delegate
        {
            if (hook == null || hook.Scheme.ToString() != "Indirect" ||
                hook.DisplacedByteCount != DisplacedLength || hook.TargetAddress != target ||
                hook.PointerSlot == IntPtr.Zero || hook.HookEntryPointAddress == IntPtr.Zero)
                throw new InvalidOperationException("Installed RedBird search detour contract differs.");
        }

        private static void ValidateInstalled<T>(NativeDetour<T> hook, ulong target) where T : Delegate
        {
            byte[] patch = new byte[DisplacedLength];
            Marshal.Copy(new IntPtr(checked((long)target)), patch, 0, patch.Length);
            if (patch[0] != 0xFF || patch[1] != 0x25 ||
                checked((long)target + DisplacedLength + BitConverter.ToInt32(patch, 2)) !=
                    hook.PointerSlot.ToInt64() ||
                Marshal.ReadInt64(hook.PointerSlot) != hook.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException("Installed search detour patch or pointer slot differs.");
        }

        private void AivSearch(ulong state, uint coarseX, uint coarseY)
        {
            aivHook.Original(state, coarseX, coarseY);
            Report("aiv-neighborhood", 0, state, coarseX, coarseY);
        }

        private void FineSearch(ulong state, int playerId, uint coarseX, uint coarseY)
        {
            fineHook.Original(state, playerId, coarseX, coarseY);
            Report("fine-path", playerId, state, coarseX, coarseY);
        }

        private void Report(string kind, int playerId, ulong state, uint inputX, uint inputY)
        {
            if (!observationEnabled) return;
            try
            {
                int resultX = Marshal.ReadInt32(new IntPtr(checked((long)state + 0x1B983C)));
                int resultY = Marshal.ReadInt32(new IntPtr(checked((long)state + 0x1B9840)));
                report(kind, playerId, (int)inputX, (int)inputY, resultX, resultY);
            }
            catch
            {
                // Observation must never alter the native search result.
            }
        }
    }
}
