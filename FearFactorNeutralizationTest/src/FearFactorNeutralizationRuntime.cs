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

namespace FearFactorNeutralizationTest
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
        private int damageCalls;
        private int suppressedOverlayLoads;
        private int runtimeTickLogged;
        private int diagnosticFailureLogged;

        internal FearFactorNeutralizationRuntime(
            ManualLogSource log,
            CrusaderLibraryLoadContext context)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal void Apply(bool referenceHashMatches)
        {
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
            FearFactorNativeDefinition.Validate(memory, imageBase);

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
            int uiHookRva = checked(uiResolution.Rva + FearFactorNativeDefinition.UiFearLoadOffset);
            if (damageResolution.Rva != FearFactorNativeDefinition.DamageFunctionRva ||
                uiHookRva != FearFactorNativeDefinition.UiFearLoadRva)
            {
                throw new InvalidOperationException("A fear-factor hook resolved outside its audited RVA.");
            }

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
                        Registers = X64SmartCPUContextRegs.RAX,
                        HookSize = FearFactorNativeDefinition.UiFearLoadLength,
                        ErrorMode = CallbackErrorMode.LogAndContinue,
                        Placement = OverwrittenInstructionPlacement.BeforeCallback
                    });

                CommitResult result = pending.Commit();
                if (!result.IsCompleteSuccess || !damageHook.Success || !unitOverlayHook.Success)
                {
                    throw new InvalidOperationException(
                        $"The atomic fear-factor hook transaction was incomplete: {result}.");
                }

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
                $"uiLoadRva=0x{uiHookRva:X}, uiHookLength={FearFactorNativeDefinition.UiFearLoadLength}, " +
                "scope=allPlayersAndAI, damageMultiplier=100Percent, selectedUnitFearSymbolsHidden=true, " +
                "fearStateMutation=false, hooksInstalledAtomically=true.");
        }

        private int NeutralizeFearDamage(IntPtr unitManager, int baseDamage, int playerIndex)
        {
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

            int fearLevel = unchecked((int)(uint)registers->RAX);
            if (fearLevel != 0)
                Interlocked.Increment(ref suppressedOverlayLoads);

            // The relocated Vanilla load has already run; only its result is neutralized.
            registers->RAX = 0;
        }

        private void OnGameTick(int tick)
        {
            if (Interlocked.Exchange(ref runtimeTickLogged, 1) != 0)
                return;

            Shared.DebugLogHelper.LogInfo(
                log,
                $"FEAR_FACTOR_NEUTRALIZATION_RUNTIME_ALIVE: tick={tick}, " +
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
