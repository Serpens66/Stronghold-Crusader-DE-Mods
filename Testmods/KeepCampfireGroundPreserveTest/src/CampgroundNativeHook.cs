using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using BepInEx;
using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;
using Shared;

namespace KeepCampfireGroundPreserveTest
{
    internal sealed unsafe class CampgroundNativeHook
    {
        private const string ExpectedNativeSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const string EntryPattern =
            "48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ?? 4C 8D 05";
        private const string StorePattern =
            "42 89 94 87 00 09 14 00 42 8B 8C 13 B4 CC 4C 06";

        private readonly HookHandle<X64InlineHook> graphicStore = new HookHandle<X64InlineHook>();
        private HookTransaction transaction;
        private IntPtr state;
        private bool published;

        internal bool IsPublished => published;
        internal long SuppressedStores => state == IntPtr.Zero ? 0 : Marshal.ReadInt64(state, 8);

        internal static CampgroundNativeHook TryCreate(CrusaderLibraryLoadContext context,
            ManualLogSource log, Action<string> write)
        {
            CampgroundNativeHook candidate = new CampgroundNativeHook();
            try
            {
                candidate.Install(context, log, write);
                return candidate;
            }
            catch
            {
                candidate.RollbackUnpublishedInitialization();
                throw;
            }
        }

        internal void SetEnabled(bool enabled)
        {
            if (!published || state == IntPtr.Zero) return;
            Interlocked.Exchange(ref *(int*)state.ToPointer(), enabled ? 1 : 0);
        }

        private void Install(CrusaderLibraryLoadContext context, ManualLogSource log,
            Action<string> write)
        {
            string nativePath = Path.Combine(Paths.GameRootPath,
                "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64",
                "CrusaderDE.dll");
            string actualHash;
            using (SHA256 sha = SHA256.Create())
            using (FileStream input = File.OpenRead(nativePath))
                actualHash = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");

            // Resolve first even on an unknown build, but the record-field and
            // loop-tail contract is bound to the audited binary hash.
            NativeResolution entry = NativePatternResolver.ResolveUnique(context.Memory,
                EntryPattern, CampgroundVisualGate.FunctionRva,
                actualHash == ExpectedNativeSha256, "campground visual function", log);
            NativeResolution store = NativePatternResolver.ResolveUnique(context.Memory,
                StorePattern, CampgroundVisualGate.HookRva,
                actualHash == ExpectedNativeSha256, "campground generic graphic store", log);
            write("native resolution: function=" + entry.Method + "/0x" +
                entry.Rva.ToString("X") + " store=" + store.Method + "/0x" +
                store.Rva.ToString("X") + " hash=" + actualHash);
            if (actualHash != ExpectedNativeSha256 ||
                entry.Rva != CampgroundVisualGate.FunctionRva ||
                store.Rva != CampgroundVisualGate.HookRva)
                throw new InvalidOperationException("Audited native build or branch layout differs.");
            if (context.Memory[CampgroundVisualGate.SkipRva] != 0x8B ||
                context.Memory[CampgroundVisualGate.SkipRva + 1] != 0x74 ||
                context.Memory[CampgroundVisualGate.SkipRva + 2] != 0x24 ||
                context.Memory[CampgroundVisualGate.SkipRva + 3] != 0x60)
                throw new InvalidOperationException("Campground loop-tail contract differs.");

            ulong image = unchecked((ulong)context.ModuleHandle.ToInt64());
            byte[] live = new byte[CampgroundVisualGate.DisplacedBytes];
            Marshal.Copy((IntPtr)(image + CampgroundVisualGate.HookRva), live, 0, live.Length);
            for (int index = 0; index < live.Length; index++)
                if (live[index] != CampgroundVisualGate.HookBytes[index])
                    throw new InvalidOperationException("Graphic store already changed by another hook.");

            using (var probe = new X64InlineHook(image + CampgroundVisualGate.HookRva,
                CampgroundVisualGate.DisplacedBytes))
                if (probe.DisplacedByteCount != CampgroundVisualGate.DisplacedBytes)
                    throw new InvalidOperationException("Installed RedBird displaced a different span.");

            state = Marshal.AllocHGlobal(16);
            Marshal.WriteInt64(state, 0, 0);
            Marshal.WriteInt64(state, 8, 0);
            transaction = new HookTransaction(context.Region,
                SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                new HookTransactionOptions {
                    FailureMode = TransactionFailureMode.RollbackAndThrow,
                    OwnsHooks = true
                });
            ulong enableAddress = unchecked((ulong)state.ToInt64());
            transaction.AddInline(graphicStore,
                HookTarget.FromAddress(image + CampgroundVisualGate.HookRva),
                (assembler, original, continuation) =>
                    CampgroundVisualGate.Generate(assembler, original, continuation,
                        enableAddress, image + CampgroundVisualGate.SkipRva),
                hookSize: CampgroundVisualGate.DisplacedBytes);
            CommitResult result = transaction.Commit();
            if (!result.IsCompleteSuccess || !graphicStore.Success ||
                !graphicStore.IsInstalled ||
                graphicStore.Hook.DisplacedByteCount != CampgroundVisualGate.DisplacedBytes)
                throw new InvalidOperationException("Graphic-store hook commit failed validation: " + result);
            published = true;
            write("HOOK READY: store RVA=0x6F0A0 span=16 continuation=0x6F0B0 " +
                "campground-skip=0x6F0D8; inactive until allowed mission; " +
                "RedBird=" + typeof(X64InlineHook).Assembly.GetName().Version);
        }

        // Only a candidate that was never published may release or undo a hook.
        private void RollbackUnpublishedInitialization()
        {
            if (published) throw new InvalidOperationException("Published hook must stay installed.");
            transaction?.Dispose();
            transaction = null;
            if (state != IntPtr.Zero) {
                Marshal.FreeHGlobal(state);
                state = IntPtr.Zero;
            }
        }
    }
}
