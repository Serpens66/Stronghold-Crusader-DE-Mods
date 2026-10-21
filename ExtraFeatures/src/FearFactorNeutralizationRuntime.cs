using BepInEx.Logging;
using CrusaderDE;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ExtraFeatures
{
    internal sealed class FearFactorNeutralizationRuntime
    {
        private readonly ManualLogSource log;
        private readonly CrusaderLibraryLoadContext context;
        private readonly HookHandle<X64InlineHook> damageHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> unitOverlayHook =
            new HookHandle<X64InlineHook>();
        private HookTransaction transaction;
        // Read by both native stubs; 1 neutralizes fear. Allocated once, never freed after publication.
        private IntPtr enabledFlag;
        private volatile bool enabled;
        private MainViewModel armyReportViewModel;

        internal FearFactorNeutralizationRuntime(
            ManualLogSource log,
            CrusaderLibraryLoadContext context)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal void Apply(bool referenceHashMatches)
        {
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("FearFactorNeutralizationTest_Serp"))
                throw new InvalidOperationException("Remove FearFactorNeutralizationTest and restart the game; overlapping hooks are forbidden.");
            if (!referenceHashMatches)
                throw new InvalidOperationException("The audited CrusaderDE.dll hash is required.");
            if (context.ModuleHandle == IntPtr.Zero || context.Memory.Length == 0 || context.Region == null)
                throw new InvalidOperationException("The Crusader native load context is incomplete.");
            if (!string.Equals(
                    FearFactorNativeDefinition.ReferenceSha256,
                    Shared.DebugLogHelper.CurrentNativeSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The mod and shared native hash contracts disagree.");
            }

            ReadOnlySpan<byte> memory = context.Memory;
            ulong imageBase = unchecked((ulong)context.ModuleHandle.ToInt64());

            Shared.NativeResolution damageResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                FearFactorNativeDefinition.DamageFunctionPattern,
                FearFactorNativeDefinition.DamageFunctionRva,
                referenceHashMatches,
                "fear-factor damage helper",
                log);
            Shared.NativeResolution uiResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                FearFactorNativeDefinition.UiFearLoadPattern,
                FearFactorNativeDefinition.UiPatternRva,
                referenceHashMatches,
                "fear-factor selected-unit overlay",
                log);
            int uiHookRva = uiResolution.Rva;
            if (damageResolution.Rva != FearFactorNativeDefinition.DamageFunctionRva ||
                uiHookRva != FearFactorNativeDefinition.UiHookRva)
            {
                throw new InvalidOperationException("A fear-factor hook resolved outside its audited RVA.");
            }

            FearFactorNativeDefinition.Validate(memory, imageBase);
            if (Shared.NativePatternResolver.FindUniquePattern(memory,
                    FearFactorNativeDefinition.DamageFunctionPattern, "fear damage") != damageResolution.Rva ||
                Shared.NativePatternResolver.FindUniquePattern(memory,
                    FearFactorNativeDefinition.UiFearLoadPattern, "fear overlay") != uiHookRva)
                throw new InvalidOperationException("Fear-factor patterns are not unique at the audited targets.");
            // Decode through the actual backend before writing either native patch.
            ProbeSpan(imageBase + (ulong)damageResolution.Rva, FearFactorNativeDefinition.DamageHookLength, "damage");
            ProbeSpan(imageBase + (ulong)uiHookRva, FearFactorNativeDefinition.UiHookLength, "UI");
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                Shared.DebugLogHelper.LogDebug(log,
                    $"FEAR_FACTOR_VALIDATED: sha256={FearFactorNativeDefinition.ReferenceSha256}, " +
                    "damageStart=0x180560, damageEnd=0x180571, instructionLengths=3,7,7, " +
                    "uiStart=0x1A19F2, uiEnd=0x1A1A00, instructionLengths=7,7, next=TEST_EAX_EAX.");

            HookTransaction pending = null;
            IntPtr flag = IntPtr.Zero;
            try
            {
                flag = Marshal.AllocHGlobal(1);
                Marshal.WriteByte(flag, 0);
                ulong flagAddress = unchecked((ulong)flag.ToInt64());
                ulong fearTableAddress = imageBase + (ulong)FearFactorNativeDefinition.FearTableRva;
                pending = new HookTransaction(
                    context.Region,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions
                    {
                        FailureMode = TransactionFailureMode.RollbackAndThrow,
                        OwnsHooks = true
                    });
                // Native stubs only: both sites are hot, so neither the enabled nor the
                // default disabled path pays a managed transition.
                pending.AddInline(
                    damageHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)damageResolution.Rva)),
                    (assembler, instructions, returnAddress) => FearFactorNativeStubs.GenerateDamage(
                        assembler, instructions, returnAddress, flagAddress, fearTableAddress),
                    hookSize: FearFactorNativeDefinition.DamageHookLength);
                pending.AddInline(
                    unitOverlayHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)uiHookRva)),
                    (assembler, instructions, returnAddress) => FearFactorNativeStubs.GenerateOverlay(
                        assembler, instructions, returnAddress, flagAddress),
                    hookSize: FearFactorNativeDefinition.UiHookLength);

                CommitResult result = pending.Commit();
                if (!result.IsCompleteSuccess || !damageHook.Success || !unitOverlayHook.Success)
                {
                    throw new InvalidOperationException(
                        $"The atomic fear-factor hook transaction was incomplete: {result}.");
                }
                if (damageHook.Hook.DisplacedByteCount != FearFactorNativeDefinition.DamageHookLength ||
                    unitOverlayHook.Hook.DisplacedByteCount != FearFactorNativeDefinition.UiHookLength)
                    throw new InvalidOperationException("Unexpected actual fear-factor overwrite length; rolling back both hooks.");

                transaction = pending;
                pending = null;
                enabledFlag = flag;
                flag = IntPtr.Zero;
            }
            catch
            {
                if (pending != null)
                {
                    try
                    {
                        pending.Dispose();
                    }
                    catch
                    {
                    }
                }
                // Only an unpublished candidate's flag is released; a published one lives
                // as long as its stubs.
                if (flag != IntPtr.Zero)
                    Marshal.FreeHGlobal(flag);
                throw;
            }

            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"FEAR_FACTOR_NEUTRALIZATION_READY: damageRva=0x{damageResolution.Rva:X}, " +
                    $"actualDamageHookLength={damageHook.Hook.DisplacedByteCount}, " +
                    $"uiHookRva=0x{uiHookRva:X}, actualUiHookLength={unitOverlayHook.Hook.DisplacedByteCount}, " +
                    "scope=allPlayersAndAI, initiallyEnabled=false, " +
                    "nativeStubs=true, editorEnabled=true, " +
                    "fearStateMutation=false, hooksInstalledAtomically=true.");
        }

        private static void ProbeSpan(ulong address, int expectedLength, string name)
        {
            using (var probe = new X64InlineHook(address, expectedLength))
            {
                if (probe.DisplacedByteCount != expectedLength)
                    throw new InvalidOperationException($"Unexpected RedBird {name} span before installation.");
            }
        }

        internal void SetEnabled(bool value)
        {
            // One shared switch makes both native callbacks obey the same host setting.
            bool next = value && transaction != null;
            if (enabled == next) return;
            enabled = next;
            Marshal.WriteByte(enabledFlag, next ? (byte)1 : (byte)0);
            if (next)
            {
                TryAttachArmyReportViewModel();
                RefreshArmyReportText();
            }
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                Shared.DebugLogHelper.LogDebugLimited(log, "ExtraFeatures/src/FearFactorNeutralizationRuntime.cs:195", $"FEAR_FACTOR_SETTING: enabled={enabled}, mode={Shared.GameplayModActivationGate.Snapshot.Kind}.");
        }

        internal void TryAttachArmyReportViewModel()
        {
            if (!MainViewModel.viewModelLoaded)
                return;

            MainViewModel viewModel = MainViewModel.Instance;
            if (ReferenceEquals(armyReportViewModel, viewModel))
                return;

            // The game's persistent view model publishes each Vanilla report rewrite.
            viewModel.PropertyChanged += OnArmyReportPropertyChanged;
            armyReportViewModel = viewModel;
            RefreshArmyReportText();
        }

        private void OnArmyReportPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args?.PropertyName == nameof(MainViewModel.ArmyReportFFBoostText) &&
                ReferenceEquals(sender, armyReportViewModel))
                RefreshArmyReportText();
        }

        private void RefreshArmyReportText()
        {
            MainViewModel viewModel = armyReportViewModel;
            if (!enabled || viewModel == null)
                return;

            string label = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_REPORT_BUTTONS, 31);
            string zeroText = FatControler.turkish || FatControler.arabic
                ? label + " %0"
                : label + " 0%";
            if (viewModel.ArmyReportFFBoostText != zeroText)
                viewModel.ArmyReportFFBoostText = zeroText;
        }

    }
}
