using BepInEx.Logging;
using SHCDESE.Interop;
using System;
using System.Runtime.InteropServices;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace TroopMovementFix
{
    internal sealed unsafe class SelectionDiagnosticsHook : IDisposable
    {
        // Internal helper called by DLL_TroopSelection for world/mouse selection.
        private const string MouseSelectionChangedPattern =
            "48 89 5C 24 08 48 89 7C 24 10 BF 01 00 00 00 48 63 DA 8B C7 " +
            "45 33 C9 89 05 ?? ?? ?? ?? 4C 8B D1";

        // Internal helper called by DLL_TroopSelectionChanged. The managed UI uses
        // this separate path after bottom-bar type filtering and similar changes.
        private const string UiSelectionChangedPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48 89 7C 24 20 " +
            "41 56 48 83 EC 20 49 63 D8 4C 8D 49 74";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MouseSelectionChangedDelegate(
            NativePointer<GameUnitManager> unitManager,
            int selectedUnitCount,
            IntPtr selectedUnitIds);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void UiSelectionChangedDelegate(
            NativePointer<GameUnitManager> unitManager,
            IntPtr selectedUnitIds,
            int selectedUnitCount);

        internal delegate void SelectionChangedDelegate(
            string source,
            int selectedUnitCount);

        private readonly ManualLogSource log;
        private readonly SelectionChangedDelegate selectionChanged;
        private readonly HookTransaction transaction;
        private HookRef<X64ManagedFunctionDetourAOB<MouseSelectionChangedDelegate>> mouseHook =
            new HookRef<X64ManagedFunctionDetourAOB<MouseSelectionChangedDelegate>>();
        private HookRef<X64ManagedFunctionDetourAOB<UiSelectionChangedDelegate>> uiHook =
            new HookRef<X64ManagedFunctionDetourAOB<UiSelectionChangedDelegate>>();

        private bool callbackFailureLogged;
        private bool disposed;

        public SelectionDiagnosticsHook(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            SelectionChangedDelegate selectionChanged)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.selectionChanged =
                selectionChanged ?? throw new ArgumentNullException(nameof(selectionChanged));

            transaction = new HookTransaction(
                memory,
                libraryBase,
                loggerFactory: null,
                failureMode: TransactionFailureMode.RollbackAndThrow);

            transaction.AddDetour(
                ref mouseHook,
                MouseSelectionChangedPattern,
                OnMouseSelectionChanged);

            transaction.AddDetour(
                ref uiHook,
                UiSelectionChangedPattern,
                OnUiSelectionChanged);

            transaction.Commit();

            if (!mouseHook.Success || !uiHook.Success)
            {
                throw new InvalidOperationException(
                    "One or more native selection diagnostic helpers were not found.");
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                "Native mouse-selection and UI-selection diagnostic hooks installed successfully.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            transaction?.Dispose();
        }

        private void OnMouseSelectionChanged(
            NativePointer<GameUnitManager> unitManager,
            int selectedUnitCount,
            IntPtr selectedUnitIds)
        {
            mouseHook.Value.Hook.Trampoline(
                unitManager,
                selectedUnitCount,
                selectedUnitIds);
            NotifySelectionChanged("MouseSelection", selectedUnitCount);
        }

        private void OnUiSelectionChanged(
            NativePointer<GameUnitManager> unitManager,
            IntPtr selectedUnitIds,
            int selectedUnitCount)
        {
            uiHook.Value.Hook.Trampoline(
                unitManager,
                selectedUnitIds,
                selectedUnitCount);
            NotifySelectionChanged("UiSelectionChanged", selectedUnitCount);
        }

        private void NotifySelectionChanged(string source, int selectedUnitCount)
        {
            try
            {
                selectionChanged(source, selectedUnitCount);
            }
            catch (Exception ex)
            {
                if (callbackFailureLogged)
                    return;

                callbackFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Selection diagnostics failed; Vanilla selection remains unchanged: {ex}");
            }
        }
    }
}
