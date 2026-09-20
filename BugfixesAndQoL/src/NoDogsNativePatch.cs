using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using System;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL
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
                pending = BugfixesHookInfrastructure.CreateOwnedTransaction(region);
                pending.AddAssemblyPatch(
                    patchHandle,
                    HookTarget.FromAddress(imageBase + NoDogsNativeContract.PatchRva),
                    (assembler, _) => NoDogsNativeContract.EmitReplacement(assembler),
                    maxByteCount: NoDogsNativeContract.ReplacementBytes.Length,
                    name: "BugfixesAndQoL_NoDogsMode99Gate");

                CommitResult commitResult = pending.Commit();
                if (!commitResult.IsCompleteSuccess || !patchHandle.Success)
                    throw new InvalidOperationException("No Dogs native patch transaction did not commit.");

                byte[] installed = new byte[NoDogsNativeContract.ReplacementBytes.Length];
                Marshal.Copy(
                    new IntPtr(unchecked((long)(imageBase + NoDogsNativeContract.PatchRva))),
                    installed,
                    0,
                    installed.Length);
                if (!new ReadOnlySpan<byte>(installed).SequenceEqual(NoDogsNativeContract.ReplacementBytes))
                    throw new InvalidOperationException("No Dogs native patch post-commit verification failed.");

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

            Shared.DebugLogHelper.LogDebug(
                log,
                $"BUGFIXES_AND_QOL_SKIRMISH_GAME_OPTIONS_NO_DOGS_PATCH_INSTALLED: sha256={NoDogsNativeContract.ReferenceSha256}, rva=0x{NoDogsNativeContract.PatchRva:X}, original=74-31, replacement=90-90.");
        }
    }
}
