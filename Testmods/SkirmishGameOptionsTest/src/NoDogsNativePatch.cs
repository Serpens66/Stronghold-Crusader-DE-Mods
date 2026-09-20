using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using System;
using System.Runtime.InteropServices;

namespace SkirmishGameOptionsTest
{
    internal sealed class NoDogsNativePatch
    {
        private readonly HookHandle<X64AssemblyPatch> patchHandle =
            new HookHandle<X64AssemblyPatch>();
        private readonly HookTransaction transaction;

        internal NoDogsNativePatch(
            ManualLogSource log,
            IntPtr libraryHandle,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));
            if (libraryHandle == IntPtr.Zero || region == null || memory.Length == 0)
                throw new ArgumentException("The Crusader native library is unavailable.");

            ulong imageBase = unchecked((ulong)libraryHandle.ToInt64());
            NoDogsNativeContract.Validate(memory, imageBase, referenceHashMatches);

            HookTransaction pending = null;
            try
            {
                pending = new HookTransaction(
                    region,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions
                    {
                        FailureMode = TransactionFailureMode.RollbackAndThrow,
                        OwnsHooks = true
                    });
                pending.AddAssemblyPatch(
                    patchHandle,
                    HookTarget.FromAddress(imageBase + NoDogsNativeContract.PatchRva),
                    (assembler, _) => NoDogsNativeContract.EmitReplacement(assembler),
                    maxByteCount: NoDogsNativeContract.ReplacementBytes.Length,
                    name: "SkirmishGameOptionsTest_NoDogsMode99Gate");

                CommitResult result = pending.Commit();
                if (!result.IsCompleteSuccess || !patchHandle.Success)
                    throw new InvalidOperationException("No Dogs native patch transaction did not commit.");

                byte[] installedBytes = new byte[NoDogsNativeContract.ReplacementBytes.Length];
                Marshal.Copy(
                    new IntPtr(unchecked((long)(imageBase + NoDogsNativeContract.PatchRva))),
                    installedBytes,
                    0,
                    installedBytes.Length);
                for (int index = 0; index < installedBytes.Length; index++)
                {
                    if (installedBytes[index] != NoDogsNativeContract.ReplacementBytes[index])
                        throw new InvalidOperationException("No Dogs native patch post-commit verification failed.");
                }

                transaction = pending;
                pending = null;
            }
            catch
            {
                if (pending != null)
                {
                    try { pending.Dispose(); } catch { }
                }
                throw;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"SKIRMISH_GAME_OPTIONS_TEST_NO_DOGS_PATCH_INSTALLED: sha256={NoDogsNativeContract.ReferenceSha256}, rva=0x{NoDogsNativeContract.PatchRva:X}, original=74-31, replacement=90-90.");
        }
    }
}
