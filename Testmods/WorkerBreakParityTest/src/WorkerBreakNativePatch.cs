using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;
using System;
using System.Runtime.InteropServices;

namespace WorkerBreakParityTest
{
    internal sealed class WorkerBreakNativePatch
    {
        private readonly HookTransaction transaction;
        private readonly HookHandle<X64AssemblyPatch>[] handles;

        internal WorkerBreakNativePatch(ManualLogSource log, CrusaderLibraryLoadContext context)
        {
            if (context == null || context.ModuleHandle == IntPtr.Zero)
                throw new ArgumentException("Crusader native library is unavailable.", nameof(context));
            ulong imageBase = unchecked((ulong)context.ModuleHandle.ToInt64());
            handles = new HookHandle<X64AssemblyPatch>[WorkerBreakNativeContract.Sites.Length];

            // Validate both sites before any executable byte is changed.
            for (int i = 0; i < handles.Length; i++)
                WorkerBreakNativeContract.Resolve(context.Memory, imageBase,
                    WorkerBreakNativeContract.Sites[i]);

            HookTransaction pending = null;
            try
            {
                pending = new HookTransaction(context.Region,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions
                    {
                        FailureMode = TransactionFailureMode.RollbackAndThrow,
                        OwnsHooks = true
                    });
                for (int i = 0; i < handles.Length; i++)
                {
                    WorkerBreakPatchSite site = WorkerBreakNativeContract.Sites[i];
                    var handle = new HookHandle<X64AssemblyPatch>();
                    handles[i] = handle;
                    pending.AddAssemblyPatch(handle,
                        HookTarget.FromAddress(imageBase + (uint)site.Rva),
                        (assembler, _) => WorkerBreakNativeContract.Emit(assembler, site, imageBase),
                        maxByteCount: site.Expected.Length,
                        name: "WorkerBreakParityTest_" + site.Name);
                }
                CommitResult result = pending.Commit();
                if (!result.IsCompleteSuccess)
                    throw new InvalidOperationException("Both worker pause patches did not commit atomically.");
                for (int i = 0; i < handles.Length; i++)
                {
                    WorkerBreakPatchSite site = WorkerBreakNativeContract.Sites[i];
                    if (!handles[i].Success)
                        throw new InvalidOperationException(site.Name + " patch did not install.");
                    var actual = new byte[site.Replacement.Length];
                    Marshal.Copy(new IntPtr(unchecked((long)(imageBase + (uint)site.Rva))),
                        actual, 0, actual.Length);
                    if (!new ReadOnlySpan<byte>(actual).SequenceEqual(site.Replacement))
                        throw new InvalidOperationException(site.Name + " post-commit bytes differ.");
                }
                transaction = pending;
                pending = null;
            }
            catch
            {
                // The candidate has not been published; an incomplete transaction must roll back.
                if (pending != null)
                {
                    try { pending.Dispose(); } catch { }
                }
                throw;
            }

            Shared.DebugLogHelper.LogInfo(log,
                "WORKER_BREAK_PARITY_TEST_PATCH_INSTALLED: miller=0x1383F3->0x138467 (2 bytes), " +
                "baker=0x139596->0x139651 (6 bytes); both branch targets are Vanilla state-2 paths.");
        }
    }
}
