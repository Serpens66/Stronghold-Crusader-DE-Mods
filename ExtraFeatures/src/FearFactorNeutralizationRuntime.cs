using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace ExtraFeatures
{
    internal sealed unsafe class FearFactorNeutralizationRuntime
    {
        private const int MaximumDetailedDamageLogs = 12;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int FearDamageDelegate(IntPtr unitManager, int baseDamage, int playerIndex);

        private readonly ManualLogSource log;
        private readonly CrusaderLibraryLoadContext context;
        private readonly DetourHandle<FearDamageDelegate> damageHook =
            new DetourHandle<FearDamageDelegate>();
        private readonly HookHandle<X64InlineHook> unitOverlayHook =
            new HookHandle<X64InlineHook>();
        private HookTransaction transaction;
        private int detailedDamageLogs;
        private int vanillaDamageLogs;
        private int damageCalls;
        private int suppressedOverlayLoads;
        private int runtimeTickLogged;
        private int diagnosticFailureLogged;
        private volatile bool enabled;
        private int overlayMarkerPending;

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
            using (var probe = new X64InlineHook(imageBase + (ulong)uiHookRva,
                FearFactorNativeDefinition.UiHookLength))
            {
                if (probe.DisplacedByteCount != FearFactorNativeDefinition.UiHookLength)
                    throw new InvalidOperationException("Unexpected RedBird UI span before installation.");
            }
            Shared.DebugLogHelper.LogInfo(log,
                $"FEAR_FACTOR_VALIDATED: sha256={FearFactorNativeDefinition.ReferenceSha256}, " +
                "uiStart=0x1A19F2, uiEnd=0x1A1A00, instructionLengths=7,7, next=TEST_EAX_EAX.");

            HookTransaction pending = null;
            try
            {
                pending = new HookTransaction(
                    context.Region,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions
                    {
                        FailureMode = TransactionFailureMode.RollbackAndThrow,
                        OwnsHooks = true
                    });
                pending.AddDetour(
                    damageHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)damageResolution.Rva)),
                    NeutralizeFearDamage);
                pending.AddContextHook(
                    unitOverlayHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)uiHookRva)),
                    HideFearOverlay,
                    new ContextHookOptions
                    {
                        // The managed call clobbers volatile GPRs. In particular R11 carries
                        // the render-cache key across this load; saving only RAX corrupts it.
                        Registers = X64SmartCPUContextRegs.All,
                        // RedBird requires >=14 bytes. Include IMUL+MOV, leaving TEST/JE outside.
                        HookSize = FearFactorNativeDefinition.UiHookLength,
                        ErrorMode = CallbackErrorMode.LogAndContinue,
                        Placement = OverwrittenInstructionPlacement.BeforeCallback
                    });

                CommitResult result = pending.Commit();
                if (!result.IsCompleteSuccess || !damageHook.Success || !unitOverlayHook.Success)
                {
                    throw new InvalidOperationException(
                        $"The atomic fear-factor hook transaction was incomplete: {result}.");
                }
                if (unitOverlayHook.Hook.DisplacedByteCount != FearFactorNativeDefinition.UiHookLength)
                    throw new InvalidOperationException("Unexpected actual UI overwrite length; rolling back both hooks.");

                transaction = pending;
                pending = null;
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
                throw;
            }

            GameTimeManagerAPI.Instance.OnTick += OnGameTick;

            Shared.DebugLogHelper.LogInfo(
                log,
                $"FEAR_FACTOR_NEUTRALIZATION_READY: damageRva=0x{damageResolution.Rva:X}, " +
                $"damageDetourLength={FearFactorNativeDefinition.DamageDetourLength}, " +
                $"uiHookRva=0x{uiHookRva:X}, actualUiHookLength={unitOverlayHook.Hook.DisplacedByteCount}, " +
                "scope=allPlayersAndAI, initiallyEnabled=false, " +
                "registers=All, editorEnabled=true, " +
                "fearStateMutation=false, hooksInstalledAtomically=true.");
        }

        private int NeutralizeFearDamage(IntPtr unitManager, int baseDamage, int playerIndex)
        {
            if (!enabled)
            {
                int originalDamage = damageHook.Original(unitManager, baseDamage, playerIndex);
                // Diagnostics run after the one original call, never in a retry/fallback path.
                if (Interlocked.Increment(ref vanillaDamageLogs) <= MaximumDetailedDamageLogs)
                {
                    try
                    {
                        Shared.DebugLogHelper.LogInfo(log,
                            $"FEAR_FACTOR_DAMAGE_VANILLA: playerIndex={playerIndex}, baseDamage={baseDamage}, returnedDamage={originalDamage}.");
                    }
                    catch (Exception exception) { LogDiagnosticFailureOnce("Vanilla result", exception); }
                }
                return originalDamage;
            }
            int callNumber = Interlocked.Increment(ref damageCalls);
            if (Interlocked.Increment(ref detailedDamageLogs) <= MaximumDetailedDamageLogs)
            {
                try
                {
                    int vanillaDamage = damageHook.Original(unitManager, baseDamage, playerIndex);
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"FEAR_FACTOR_DAMAGE_NEUTRALIZED: call={callNumber}, playerIndex={playerIndex}, " +
                        $"baseDamage={baseDamage}, vanillaAdjustedDamage={vanillaDamage}, returnedDamage={baseDamage}.");
                }
                catch (Exception exception)
                {
                    LogDiagnosticFailureOnce("damage comparison", exception);
                }
            }

            return FearFactorNeutralizationPolicy.CalculateNeutralDamage(baseDamage);
        }

        private void HideFearOverlay(NativePointer<X64SmartCPUContext> contextPointer)
        {
            X64SmartCPUContext* registers = contextPointer.Pointer;
            if (registers == null)
                return;

            if (!enabled) return;
            if (unchecked((int)(uint)registers->RAX) != 0)
            {
                Interlocked.Increment(ref suppressedOverlayLoads);
                Interlocked.CompareExchange(ref overlayMarkerPending, 1, 0);
            }

            // The relocated Vanilla load has already run; only its result is neutralized.
            registers->RAX = 0;
        }

        internal void SetEnabled(bool value)
        {
            // One shared switch makes both native callbacks obey the same host setting.
            bool next = value && transaction != null;
            if (enabled == next) return;
            enabled = next;
            Interlocked.Exchange(ref runtimeTickLogged, 0);
            Shared.DebugLogHelper.LogInfo(log,
                $"FEAR_FACTOR_SETTING: enabled={enabled}, mode={Shared.GameplayModActivationGate.Snapshot.Kind}.");
        }

        private void OnGameTick(int tick)
        {
            // Never write logs from the render callback.
            if (Interlocked.CompareExchange(ref overlayMarkerPending, 2, 1) == 1)
                Shared.DebugLogHelper.LogInfo(log, "FEAR_FACTOR_OVERLAY_CONFIRMED: nonzero fear neutralized; healthbar retained.");

            if (Interlocked.Exchange(ref runtimeTickLogged, 1) != 0)
                return;

            Shared.DebugLogHelper.LogInfo(
                log,
                $"FEAR_FACTOR_NEUTRALIZATION_RUNTIME_ALIVE: tick={tick}, enabled={enabled}, " +
                $"damageCalls={Volatile.Read(ref damageCalls)}, " +
                $"suppressedOverlayLoads={Volatile.Read(ref suppressedOverlayLoads)}, " +
                $"transactionRooted={transaction != null}.");
        }

        private void LogDiagnosticFailureOnce(string operation, Exception exception)
        {
            if (Interlocked.Exchange(ref diagnosticFailureLogged, 1) != 0)
                return;
            try
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"FEAR_FACTOR_DIAGNOSTIC_FAILED: operation={operation}, gameplay neutralization remains active, " +
                    $"exception={exception}");
            }
            catch
            {
            }
        }
    }
}
