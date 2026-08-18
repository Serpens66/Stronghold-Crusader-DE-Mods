using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Projectiles;
using SHCDESE.EventAPI.Units;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Zhuqiaomon.Assembly.Stateful;
using Zhuqiaomon.Memory;

namespace ImprovedHunters
{
    internal sealed partial class ImprovedHuntersRuntime
    {
        // Per-animal compatibility required by the corresponding Hunt setting.
        private void InitializeRabbitDespawnPatch()
        {
            if (rabbitDespawnTicksInitialized)
                return;

            rabbitDespawnTickTime = GameGlobalsManager.Instance.RabbitDespawnTickTime;
            originalRabbitDespawnTicks = rabbitDespawnTickTime.GetValue();
            rabbitDespawnTicksInitialized = true;
        }

        private void InitializeExtraDespawnPatches(ReadOnlySpan<byte> memory, ulong imageBase)
        {
            if (extraDespawnTicksInitialized)
                return;

            camelDespawnTickTime = FindExtraDespawnImmediate(
                memory,
                imageBase,
                "camel despawn immediate",
                CamelDespawnTickTimePattern,
                CamelDespawnTickTimeRva);
            chickenDespawnTickTime = FindExtraDespawnImmediate(
                memory,
                imageBase,
                "chicken despawn immediate",
                ChickenDespawnTickTimePattern,
                ChickenDespawnTickTimeRva);

            if (camelDespawnTickTime != null)
                originalCamelDespawnTicks = camelDespawnTickTime.GetValue();

            if (chickenDespawnTickTime != null)
                originalChickenDespawnTicks = chickenDespawnTickTime.GetValue();

            extraDespawnTicksInitialized = true;
        }

        private ManagedAssemblyImmediate<short> FindExtraDespawnImmediate(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            string name,
            string pattern,
            int referenceRva)
        {
            try
            {
                int offset = Shared.NativePatternResolver.ResolveUnique(
                    memory,
                    pattern,
                    referenceRva,
                    referenceHashMatches,
                    name,
                    log).Rva;

                return new ManagedAssemblyImmediate<short>(
                    new IntPtr(unchecked((long)(imageBase + (ulong)offset + ExtraDespawnPatternImmediateOffset))),
                    // The matched instruction has more than one operand; operand 1
                    // is the immediate despawn threshold.
                    operand: 1);
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters failed to initialize {name}; this native patch remains inactive: {exception}");
                return null;
            }
        }

        private void ApplyDespawnPatches()
        {
            try
            {
                if (rabbitDespawnTickTime != null)
                {
                    short desired = settings.EnableMod && settings.HuntRabbit
                        ? RabbitCorpseDespawnTicks
                        : originalRabbitDespawnTicks;

                    if (rabbitDespawnTickTime.GetValue() != desired)
                        rabbitDespawnTickTime.SetValue(desired);

                    rabbitDespawnTicksPatched = desired != originalRabbitDespawnTicks;
                }

                ApplyExtraDespawnPatch(camelDespawnTickTime, originalCamelDespawnTicks, settings.EnableMod && settings.HuntCamel, ref camelDespawnTicksPatched);
                ApplyExtraDespawnPatch(chickenDespawnTickTime, originalChickenDespawnTicks, settings.EnableMod && settings.HuntChicken, ref chickenDespawnTicksPatched);
                LogDespawnPatchState();
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters failed to apply an animal despawn patch; the affected patch remains inactive: {exception}");
            }
        }

        private void LogDespawnPatchState()
        {
            if (despawnPatchStateLogged)
                return;

            despawnPatchStateLogged = true;
            log.LogInfo(
                $"Improved Hunters despawn patch state: " +
                $"rabbit={FormatDespawnPatchState(rabbitDespawnTickTime, originalRabbitDespawnTicks, settings.EnableMod && settings.HuntRabbit ? RabbitCorpseDespawnTicks : originalRabbitDespawnTicks, rabbitDespawnTicksPatched)}, " +
                $"camel={FormatDespawnPatchState(camelDespawnTickTime, originalCamelDespawnTicks, settings.EnableMod && settings.HuntCamel ? ExtraCorpseDespawnTicks : originalCamelDespawnTicks, camelDespawnTicksPatched)}, " +
                $"chicken={FormatDespawnPatchState(chickenDespawnTickTime, originalChickenDespawnTicks, settings.EnableMod && settings.HuntChicken ? ExtraCorpseDespawnTicks : originalChickenDespawnTicks, chickenDespawnTicksPatched)}.");
        }

        private static string FormatDespawnPatchState(
            ManagedAssemblyImmediate<short> immediate,
            short originalTicks,
            short desiredTicks,
            bool patched)
        {
            if (immediate == null)
                return "missing";

            return $"original={originalTicks}/desired={desiredTicks}/current={immediate.GetValue()}/patched={patched}";
        }

        private static void ApplyExtraDespawnPatch(
            ManagedAssemblyImmediate<short> immediate,
            short originalTicks,
            bool enabled,
            ref bool patched)
        {
            if (immediate == null)
                return;

            short desired = enabled ? ExtraCorpseDespawnTicks : originalTicks;
            if (immediate.GetValue() != desired)
                immediate.SetValue(desired);

            patched = desired != originalTicks;
        }

        private void ApplyCamelHealthPatch()
        {
            try
            {
                GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
                if (!camelHealthInitialized)
                {
                    originalCamelArrowDamage = unitApi.GetRangedArrowDamageTo(eChimps.CHIMP_TYPE_CAMEL);
                    originalCamelHealth = unitApi.GetDefaultHealth(eChimps.CHIMP_TYPE_CAMEL);
                    camelHealthInitialized = true;
                }

                uint desired = originalCamelHealth;
                if (settings.EnableMod && settings.HuntCamel)
                {
                    uint oneShotHealth = (uint)Math.Max(1, originalCamelArrowDamage - 1);
                    desired = Math.Min(originalCamelHealth, oneShotHealth);
                }

                if (unitApi.GetDefaultHealth(eChimps.CHIMP_TYPE_CAMEL) != desired)
                    unitApi.SetDefaultHealth(eChimps.CHIMP_TYPE_CAMEL, desired);

                desiredCamelHealth = desired;
                camelHealthPatched = desired != originalCamelHealth;
                LogCamelHealthPatch(0);
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters failed to apply the camel health patch; that patch remains inactive: {exception}");
            }
        }

        private unsafe bool TryClampLiveCamelHealth(int unitId, GameUnit* unit)
        {
            if (!settings.EnableMod ||
                !settings.HuntCamel ||
                !camelHealthInitialized ||
                desiredCamelHealth == 0 ||
                unit == null ||
                unit->r_UnitChimp != eChimps.CHIMP_TYPE_CAMEL ||
                unit->r_AliveState != AliveState.IsAlive)
            {
                return false;
            }

            bool changed = false;
            if (unit->r_MaxHealth > desiredCamelHealth)
            {
                unit->r_MaxHealth = desiredCamelHealth;
                changed = true;
            }

            if (unit->r_CurrentHealth > desiredCamelHealth)
            {
                unit->r_CurrentHealth = desiredCamelHealth;
                changed = true;
            }

            if (changed)
                UpdateUnitHealthDisplay(unit);

            return changed;
        }

        private unsafe void LogCamelHealthPatch(int adjustedLiveCamels)
        {
            if (!camelHealthInitialized)
                return;

            if (adjustedLiveCamels <= 0 && lastLoggedDesiredCamelHealth == desiredCamelHealth)
                return;

            lastLoggedDesiredCamelHealth = desiredCamelHealth;
            log.LogInfo(
                $"Improved Hunters camel health patch: originalHealth={originalCamelHealth}, desiredHealth={desiredCamelHealth}, " +
                $"originalArrowDamage={originalCamelArrowDamage}, enabled={settings.EnableMod && settings.HuntCamel}, " +
                $"adjustedLiveCamels={adjustedLiveCamels}.");
        }

    }
}
