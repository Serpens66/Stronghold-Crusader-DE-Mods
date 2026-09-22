using BepInEx.Logging;
using CrusaderDE;
using RedBird.Core.Memory;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace AIAttackTest
{
    internal sealed class AIAttackTestRuntime
    {
        private const int InternalAicSize = 0x5E4;
        private const int SiegeTriggerLevelOffset = 0x1F8;
        private const int SiegeMaxTroopsOffset = 0x454;
        private const int SiegeNormalWaveMultiplierOffset = 0x458;
        private const int SiegeHighGoldWaveMultiplierOffset = 0x45C;

        private readonly ManualLogSource log;
        private readonly AIAttackTestSettings settings;
        private readonly AIAttackPermanentNativeOverrides nativeOverrides;
        private readonly Dictionary<int, AicOverrideState> aicOverrides =
            new Dictionary<int, AicOverrideState>();
        private readonly Dictionary<int, AttackDiagnosticState> diagnostics =
            new Dictionary<int, AttackDiagnosticState>();
        private readonly List<int> activeAiPlayerIds = new List<int>();

        private bool mapActive;

        internal AIAttackTestRuntime(
            ManualLogSource log,
            AIAttackTestSettings settings,
            CrusaderLibraryLoadContext context,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (!referenceHashMatches)
                throw new InvalidOperationException("The audited CrusaderDE.dll hash is required.");

            ValidateManagedLayout();
            AIAttackNativeContract.ValidateLayout();

            int recruitContext = Shared.NativePatternResolver.FindUniquePattern(
                context.Memory,
                AIAttackNativeContract.RecruitContextPattern,
                "AI initial defense-only recruitment comparison");
            int lordContext = Shared.NativePatternResolver.FindUniquePattern(
                context.Memory,
                AIAttackNativeContract.LordContextPattern,
                "AI post-breach lord command limiter");
            if (recruitContext != AIAttackNativeContract.RecruitContextRva ||
                lordContext != AIAttackNativeContract.LordContextRva)
            {
                throw new InvalidOperationException(
                    $"Native AI contexts resolved at unexpected RVAs: " +
                    $"recruit=0x{recruitContext:X}, lord=0x{lordContext:X}.");
            }

            int recruitRva = checked(recruitContext + AIAttackNativeContract.RecruitImmediateOffset);
            int lordRva = checked(lordContext + AIAttackNativeContract.LordBranchOffset);
            ValidateLoadedBytes(
                context.Memory,
                recruitRva,
                AIAttackNativeContract.VanillaRecruitTicks,
                "initial recruitment comparison immediate");
            ValidateLoadedBytes(
                context.Memory,
                lordRva,
                AIAttackNativeContract.VanillaLordBranch,
                "post-breach lord limiter branch");

            ulong moduleBase = unchecked((ulong)context.ModuleHandle.ToInt64());
            nativeOverrides = new AIAttackPermanentNativeOverrides(
                context.Region,
                context.Memory,
                moduleBase);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"AI attack native patches resolved: recruitImmediateRva=0x{recruitRva:X}, " +
                $"lordBranchRva=0x{lordRva:X}, wallTargetingFixRva=0x{AIAttackNativeContract.AiWallTargetingFixRva:X} (separate).");
        }

        internal void BeginMap(Shared.GameplaySessionStartedContext context)
        {
            EndMap("session replacement");
            if (!settings.EnableMod)
            {
                Shared.DebugLogHelper.LogInfo(log, "AI Attack Test is disabled; Vanilla settings remain active.");
                return;
            }

            try
            {
                ApplyNativeSettings();
                ResolveActiveAiPlayers();
                if (settings.AttackScalingMode == AIAttackPolicy.RelativeMode)
                    ApplyRelativeAicOverrides();
                InitializeDiagnostics();
                mapActive = true;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"AI Attack Test session applied: kind={context.Kind}, activeAiPlayers=[{string.Join(",", activeAiPlayerIds)}], " +
                    $"scaling={settings.AttackScalingMode}, percent={settings.RelativeAttackGrowthPercent}, " +
                    $"attackLord={settings.AttackLordAfterBreach}, defenseMonths={settings.InitialDefenseOnlyMonths}.");
            }
            catch (Exception applyError)
            {
                try
                {
                    EndMap("failed map application");
                }
                catch (Exception rollbackError)
                {
                    throw new AggregateException(
                        "AI Attack Test map application and rollback both failed.",
                        applyError,
                        rollbackError);
                }
                throw;
            }
        }

        internal void EndMap(string reason)
        {
            Exception aicError = null;
            Exception nativeError = null;
            try
            {
                RestoreAicOverrides(reason);
            }
            catch (Exception ex)
            {
                aicError = ex;
            }
            try
            {
                RestoreNativeOverrides();
            }
            catch (Exception ex)
            {
                nativeError = ex;
            }
            finally
            {
                diagnostics.Clear();
                activeAiPlayerIds.Clear();
                mapActive = false;
            }

            if (aicError != null && nativeError != null)
                throw new AggregateException("AIC and native patch restoration both failed.", aicError, nativeError);
            if (aicError != null)
                throw new InvalidOperationException("AIC restoration failed.", aicError);
            if (nativeError != null)
                throw new InvalidOperationException("Native patch restoration failed.", nativeError);
        }

        internal void RollbackUnpublishedInitialization() =>
            nativeOverrides.RollbackUnpublished();

        internal void MarkPublished()
        {
            nativeOverrides.MarkPublished();
        }

        internal unsafe void OnGameTick(int tick)
        {
            if (!mapActive)
                return;

            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            var aics = GameAIManagerAPI.Instance.GetAICArray();
            foreach (int playerId in activeAiPlayerIds)
            {
                if (!diagnostics.TryGetValue(playerId, out AttackDiagnosticState diagnostic) ||
                    diagnostic.ChangesLogged >= 4 ||
                    !playerApi.TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources) ||
                    resources == null)
                {
                    continue;
                }

                uint target = resources->r_AITargetAttackForceSize;
                if (target == diagnostic.LastTarget)
                    continue;

                diagnostic.LastTarget = target;
                diagnostics[playerId] = diagnostic;
                if (target == 0)
                    continue;

                diagnostic.ChangesLogged++;
                diagnostics[playerId] = diagnostic;
                Enums.AILords lord = playerApi.GetAILord(playerId);
                int aicIndex = (int)lord;
                if (aicIndex <= 0 || aicIndex >= aics.Length)
                    continue;
                InternalAIC aic = aics.GetValue(aicIndex);
                int gold = playerApi.GetPlayerGold(playerId);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"AI_ATTACK_FORCE_OBSERVED: tick={tick}, sample={diagnostic.ChangesLogged}/4, " +
                    $"player={playerId}, lord={lord}, target={target}, gold={gold}, " +
                    $"goldPath={(gold < 10001 ? "normal" : "high")}, trigger={aic.siege_trigger_level}, " +
                    $"normalStep={aic.siege_normal_wave_multiplier}, highStep={aic.siege_high_gold_wave_multiplier}, " +
                    $"lordCap={aic.siege_max_troops}.");
            }
        }

        private void ApplyNativeSettings()
        {
            nativeOverrides.Apply(
                AIAttackPolicy.CalculateInitialDefenseTicks(settings.InitialDefenseOnlyMonths),
                settings.AttackLordAfterBreach);
        }

        private void ResolveActiveAiPlayers()
        {
            activeAiPlayerIds.Clear();
            for (int playerId = 1; playerId <= 8; playerId++)
            {
                if (GamePlayerManagerAPI.Instance.GetAILord(playerId) != Enums.AILords.SK_NULL)
                    activeAiPlayerIds.Add(playerId);
            }
        }

        private unsafe void ApplyRelativeAicOverrides()
        {
            var aics = GameAIManagerAPI.Instance.GetAICArray();
            int[] indices = AIAttackPolicy.ResolveUniqueAicIndices(
                activeAiPlayerIds.Select(playerId =>
                    (int)GamePlayerManagerAPI.Instance.GetAILord(playerId)),
                aics.Length);
            foreach (int aicIndex in indices)
            {
                InternalAIC current = aics.GetValue(aicIndex);
                InternalAIC updated = current;
                updated.siege_normal_wave_multiplier =
                    AIAttackPolicy.CalculateNormalWaveMultiplier(
                        current.siege_trigger_level,
                        settings.RelativeAttackGrowthPercent);
                updated.siege_high_gold_wave_multiplier =
                    AIAttackPolicy.CalculateHighGoldWaveMultiplier(
                        updated.siege_normal_wave_multiplier);

                SetFullAic(aicIndex, updated);
                aicOverrides.Add(
                    aicIndex,
                    new AicOverrideState(
                        current.siege_normal_wave_multiplier,
                        current.siege_high_gold_wave_multiplier,
                        updated.siege_normal_wave_multiplier,
                        updated.siege_high_gold_wave_multiplier));
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"AI AIC relative scaling applied: aic={aicIndex}/{(Enums.AILords)aicIndex}, " +
                    $"trigger={current.siege_trigger_level}, normal={current.siege_normal_wave_multiplier}->{updated.siege_normal_wave_multiplier}, " +
                    $"high={current.siege_high_gold_wave_multiplier}->{updated.siege_high_gold_wave_multiplier}, " +
                    $"lordCap={current.siege_max_troops} (preserved).");
            }
        }

        private unsafe void RestoreAicOverrides(string reason)
        {
            if (aicOverrides.Count == 0)
                return;

            var aics = GameAIManagerAPI.Instance.GetAICArray();
            foreach (KeyValuePair<int, AicOverrideState> pair in aicOverrides.ToArray())
            {
                if (pair.Key <= 0 || pair.Key >= aics.Length)
                    continue;

                InternalAIC current = aics.GetValue(pair.Key);
                bool restoreNormal = current.siege_normal_wave_multiplier == pair.Value.WrittenNormal;
                bool restoreHigh = current.siege_high_gold_wave_multiplier == pair.Value.WrittenHigh;
                if (!restoreNormal || !restoreHigh)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"AI AIC cooperative restore detected a foreign change: aic={pair.Key}, reason={reason}, " +
                        $"normalCurrent={current.siege_normal_wave_multiplier}, normalWritten={pair.Value.WrittenNormal}, " +
                        $"highCurrent={current.siege_high_gold_wave_multiplier}, highWritten={pair.Value.WrittenHigh}. " +
                        "Only unchanged owned fields will be restored.");
                }

                if (!restoreNormal && !restoreHigh)
                    continue;
                if (restoreNormal)
                    current.siege_normal_wave_multiplier = pair.Value.OriginalNormal;
                if (restoreHigh)
                    current.siege_high_gold_wave_multiplier = pair.Value.OriginalHigh;
                SetFullAic(pair.Key, current);
            }
            aicOverrides.Clear();
        }

        private unsafe void SetFullAic(int aicIndex, InternalAIC value)
        {
            if (sizeof(InternalAIC) != InternalAicSize)
                throw new InvalidOperationException("InternalAIC changed before SetAICFromBytes.");
            var aics = GameAIManagerAPI.Instance.GetAICArray();
            if (aics.GetArrayAddress() == IntPtr.Zero || aicIndex <= 0 || aicIndex >= aics.Length)
                throw new InvalidOperationException($"AIC index {aicIndex} is not writable.");
            ReadOnlySpan<byte> bytes = new ReadOnlySpan<byte>(&value, sizeof(InternalAIC));
            if (bytes.Length != InternalAicSize)
                throw new InvalidOperationException("InternalAIC byte span has an unexpected length.");
            GameAIManagerAPI.Instance.SetAICFromBytes((Enums.AILords)aicIndex, bytes);
            InternalAIC verified = aics.GetValue(aicIndex);
            ReadOnlySpan<byte> verifiedBytes = new ReadOnlySpan<byte>(&verified, sizeof(InternalAIC));
            if (!verifiedBytes.SequenceEqual(bytes))
                throw new InvalidOperationException($"Full AIC write verification failed for index {aicIndex}.");
        }

        private unsafe void InitializeDiagnostics()
        {
            diagnostics.Clear();
            foreach (int playerId in activeAiPlayerIds)
            {
                uint current = 0;
                if (GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(
                        playerId,
                        out GamePlayerResources* resources) && resources != null)
                {
                    current = resources->r_AITargetAttackForceSize;
                }
                diagnostics[playerId] = new AttackDiagnosticState(current, 0);
            }
        }

        private void RestoreNativeOverrides()
        {
            nativeOverrides.RestoreVanilla();
        }

        private static void ValidateLoadedBytes(
            ReadOnlySpan<byte> memory,
            int rva,
            byte[] expected,
            string label)
        {
            if (rva < 0 || rva > memory.Length - expected.Length ||
                !memory.Slice(rva, expected.Length).SequenceEqual(expected))
            {
                throw new InvalidOperationException($"{label} does not match audited Vanilla bytes.");
            }
        }

        private static void ValidateManagedLayout()
        {
            ValidateSize(typeof(InternalAIC), InternalAicSize);
            ValidateOffset(typeof(InternalAIC), nameof(InternalAIC.siege_trigger_level), SiegeTriggerLevelOffset);
            ValidateOffset(typeof(InternalAIC), nameof(InternalAIC.siege_max_troops), SiegeMaxTroopsOffset);
            ValidateOffset(typeof(InternalAIC), nameof(InternalAIC.siege_normal_wave_multiplier), SiegeNormalWaveMultiplierOffset);
            ValidateOffset(typeof(InternalAIC), nameof(InternalAIC.siege_high_gold_wave_multiplier), SiegeHighGoldWaveMultiplierOffset);
            ValidateOffset(typeof(GamePlayerResources), nameof(GamePlayerResources.r_AITargetAttackForceSize), 0x38A0);
        }

        private static void ValidateSize(Type type, int expected)
        {
            int actual = Marshal.SizeOf(type);
            if (actual != expected)
                throw new InvalidOperationException(
                    $"Managed layout mismatch: sizeof({type.Name})=0x{actual:X}, expected=0x{expected:X}.");
        }

        private static void ValidateOffset(Type type, string fieldName, int expected)
        {
            int actual = Marshal.OffsetOf(type, fieldName).ToInt32();
            if (actual != expected)
                throw new InvalidOperationException(
                    $"Managed layout mismatch: {type.Name}.{fieldName}=0x{actual:X}, expected=0x{expected:X}.");
        }

        private static string ToHex(byte[] bytes) =>
            BitConverter.ToString(bytes).Replace('-', ' ');

        private readonly struct AicOverrideState
        {
            internal AicOverrideState(int originalNormal, int originalHigh, int writtenNormal, int writtenHigh)
            {
                OriginalNormal = originalNormal;
                OriginalHigh = originalHigh;
                WrittenNormal = writtenNormal;
                WrittenHigh = writtenHigh;
            }

            internal int OriginalNormal { get; }
            internal int OriginalHigh { get; }
            internal int WrittenNormal { get; }
            internal int WrittenHigh { get; }
        }

        private struct AttackDiagnosticState
        {
            internal AttackDiagnosticState(uint lastTarget, int changesLogged)
            {
                LastTarget = lastTarget;
                ChangesLogged = changesLogged;
            }

            internal uint LastTarget;
            internal int ChangesLogged;
        }
    }
}
