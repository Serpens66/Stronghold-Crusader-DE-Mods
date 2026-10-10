using APIShared.Presentation;
using APIShared.GameModes;
// Feature: Fair single- and multi-selection catapult/trebuchet ammunition restocking.
using BepInEx.Logging;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Input;
using SHCDESE.EventAPI.Network;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal sealed unsafe class SiegeAmmoRestockFeature : IDisposable
    {
        private const int ProtocolVersion = 1;
        private const int MaximumRememberedOperations = 2048;
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly MultiplayerFeatureGate multiplayerFeatureGate;
        private readonly HashSet<long> processedOperations = new HashSet<long>();
        private readonly Queue<long> processedOperationOrder = new Queue<long>();
        private bool eventsRegistered;
        private R3PacketEventHook<SiegeAmmoRestockPacket> packetHook;
        private IDisposable packetSubscription;
        private IDisposable keyDownSubscription;
        private IDisposable keyUpSubscription;
        private int nextOperationId;
        private int displayedTooltipCost = -1;
        private int displayedTooltipAmount = -1;
        private string vanillaReloadTooltip;
        private MainViewModel hoveredViewModel;

        internal SiegeAmmoRestockFeature(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            MultiplayerFeatureGate multiplayerFeatureGate)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.multiplayerFeatureGate = multiplayerFeatureGate ?? throw new ArgumentNullException(nameof(multiplayerFeatureGate));
        }

        internal void Initialize()
        {
            if (eventsRegistered) return;
            try
            {
                packetHook = GameNetworkAPI.Instance.GetPacketEventFor<SiegeAmmoRestockPacket>();
                packetSubscription = packetHook.GetBaseHook().Observable.Subscribe(OnPacketReceived);
                if (!PresentationEvents.TryRegister(PresentationOperation.RechargeSiegeAmmo, BugfixesAndQoLPlugin.PluginGuid,
                    "FairSiegeAmmo", args => {
                        if (!eventsRegistered || args.SkipOriginalFunction || !settings.EnableMod || !settings.EnableFairSiegeAmmoRestock) return;
                        args.Replacement = parameter => OnRechargeRock(args.ViewModel, parameter);
                    }, null, out string reason)) throw new InvalidOperationException(reason);
                if (!PresentationEvents.TryRegister(PresentationOperation.TroopPanelEnter, BugfixesAndQoLPlugin.PluginGuid,
                    "FairSiegeAmmo", null, args => { if (eventsRegistered && args.OriginalCompleted) OnTroopPanelMouseEnter(args.ViewModel, args.Parameter); }, out reason)) throw new InvalidOperationException(reason);
                if (!PresentationEvents.TryRegister(PresentationOperation.TroopPanelLeave, BugfixesAndQoLPlugin.PluginGuid,
                    "FairSiegeAmmo", null, args => { if (eventsRegistered && args.OriginalCompleted) OnTroopPanelMouseLeave(args.ViewModel, args.Parameter); }, out reason)) throw new InvalidOperationException(reason);
                keyDownSubscription = InputR3EventHooks.OnKeyDown.Observable.Subscribe(OnModifierKeyChanged);
                keyUpSubscription = InputR3EventHooks.OnKeyUp.Observable.Subscribe(OnModifierKeyChanged);
                eventsRegistered = true;
                LogInfo($"fair siege-ammunition shared events initialized: packetId={packetHook.GetPacketId()}, protocol={ProtocolVersion}.");
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            // Only an unpublished failed initialization may release its subscriptions.
            if (eventsRegistered) return;
            keyUpSubscription?.Dispose(); keyUpSubscription = null;
            keyDownSubscription?.Dispose(); keyDownSubscription = null;
            packetSubscription?.Dispose(); packetSubscription = null;
        }
        private void OnTroopPanelMouseEnter(MainViewModel self, object parameter)
        {
            if (!string.Equals(parameter as string, "UnitReload", StringComparison.Ordinal))
            {
                ClearReloadTooltipState();
                return;
            }

            hoveredViewModel = self;
            vanillaReloadTooltip = self?.TroopsPanelRollover;
            displayedTooltipCost = -1;
            displayedTooltipAmount = -1;
            RefreshReloadTooltip(force: true);
        }

        private void OnTroopPanelMouseLeave(MainViewModel self, object parameter)
        {
            ClearReloadTooltipState();
        }

        private void ClearReloadTooltipState()
        {
            hoveredViewModel = null;
            vanillaReloadTooltip = null;
            displayedTooltipCost = -1;
            displayedTooltipAmount = -1;
        }

        private void OnModifierKeyChanged(UnityInputEventArgs args)
        {
            if (args == null || args.Phase != EventHookPhase.Post || !IsModifierKey(args.Key))
                return;
            RefreshReloadTooltip(force: true);
        }

        private static bool IsModifierKey(KeyCode key) =>
            key == KeyCode.LeftShift || key == KeyCode.RightShift ||
            key == KeyCode.LeftControl || key == KeyCode.RightControl;

        private void RefreshReloadTooltip(bool force)
        {
            MainViewModel viewModel = hoveredViewModel;
            string template = vanillaReloadTooltip;
            if (viewModel == null || string.IsNullOrEmpty(template))
                return;

            if (!settings.EnableMod || !settings.EnableFairSiegeAmmoRestock ||
                !TryReadConfiguredPackage(out int baseCost, out int baseAmount) ||
                !SiegeAmmoRestockPolicy.TryCalculateRequestedPackage(
                    baseCost,
                    baseAmount,
                    CaptureModifier(),
                    out int displayedCost,
                    out int displayedAmount))
            {
                if (force || !string.Equals(viewModel.TroopsPanelRollover, template, StringComparison.Ordinal))
                    viewModel.TroopsPanelRollover = template;
                displayedTooltipCost = -1;
                displayedTooltipAmount = -1;
                return;
            }

            if (!force && displayedTooltipCost == displayedCost && displayedTooltipAmount == displayedAmount)
                return;

            viewModel.TroopsPanelRollover = SiegeAmmoRestockPolicy.ReplaceFirstTwoNumbers(
                template,
                displayedAmount,
                displayedCost);
            displayedTooltipCost = displayedCost;
            displayedTooltipAmount = displayedAmount;
        }

        private void OnRechargeRock(MainViewModel self, object parameter)
        {
            try
            {
                int playerId = GetControlledPlayerId();
                SiegeAmmoRestockModifier modifier = CaptureModifier();
                if (!TryReadConfiguredPackage(out int baseStoneCost, out int baseAmmunitionAmount) ||
                    !TryCaptureSelectedGlobalIds(playerId, out int[] globalUnitIds))
                {
                    LogWarning("fair siege-ammunition restock was rejected because its package or selected targets were invalid.");
                    return;
                }

                if (multiplayerFeatureGate.BlocksLocalStateChanges)
                {
                    TryQueueChore(playerId, modifier, baseStoneCost, baseAmmunitionAmount, globalUnitIds);
                    return;
                }

                ApplyValidated(playerId, modifier, baseStoneCost, baseAmmunitionAmount, globalUnitIds, "local click");
            }
            catch (Exception ex)
            {
                LogError($"fair siege-ammunition click failed closed: {ex}");
            }
        }

        private bool TryQueueChore(
            int playerId,
            SiegeAmmoRestockModifier modifier,
            int baseStoneCost,
            int baseAmmunitionAmount,
            int[] globalUnitIds)
        {
            if (!BugfixesAndQoLChoreSender.IsAvailable(
                    packetHook != null,
                    () => GameGlobalsManager.Instance.ChoreManagerVA))
            {
                LogError("fair siege-ammunition restock was rejected in multiplayer because Chore transport is unavailable.");
                return false;
            }

            var packet = new SiegeAmmoRestockPacket
            {
                ProtocolVersion = ProtocolVersion,
                PlayerId = playerId,
                OperationId = unchecked(++nextOperationId),
                Modifier = (int)modifier,
                BaseStoneCost = baseStoneCost,
                BaseAmmunitionAmount = baseAmmunitionAmount,
                GlobalUnitIds = globalUnitIds
            };
            short packetId = packetHook?.GetPacketId() ?? (short)0;
            if (!BugfixesAndQoLChoreSender.TrySend(
                    packet,
                    packetId,
                    packetHook != null,
                    value => GameNetworkAPI.Serialize(value),
                    () => GameGlobalsManager.Instance.ChoreManagerVA,
                    (value, id) => GameNetworkAPI.SendPacketToAllEx2(value, id, viaChore: true),
                    out byte[] body,
                    out string rejectionReason))
            {
                LogError($"fair siege-ammunition Chore was not queued; no local mutation occurred: operationId={packet.OperationId}, reason={rejectionReason}.");
                return false;
            }

            LogInfo($"fair siege-ammunition Chore queued: playerId={playerId}, operationId={packet.OperationId}, targets={globalUnitIds.Length}, modifier={modifier}, bytes={sizeof(short) + body.Length}.");
            return true;
        }

        private void OnPacketReceived(ReceiveCustomPacketEventArgs<SiegeAmmoRestockPacket> args)
        {
            SiegeAmmoRestockPacket packet = args?.Packet;
            if (!settings.EnableMod || !settings.EnableFairSiegeAmmoRestock || !IsValidPacket(packet))
            {
                LogWarning("rejected an invalid or disabled fair siege-ammunition Chore.");
                return;
            }

            long operationKey = ((long)packet.PlayerId << 32) | (uint)packet.OperationId;
            if (!RememberOperation(operationKey))
            {
                LogWarning($"rejected duplicate fair siege-ammunition operation: playerId={packet.PlayerId}, operationId={packet.OperationId}.");
                return;
            }

            if (!TryReadConfiguredPackage(out int currentCost, out int currentAmount) ||
                currentCost != packet.BaseStoneCost || currentAmount != packet.BaseAmmunitionAmount)
            {
                LogError($"rejected fair siege-ammunition Chore because configured package values differ: packet={packet.BaseAmmunitionAmount}/{packet.BaseStoneCost}, local={currentAmount}/{currentCost}.");
                return;
            }

            ApplyValidated(
                packet.PlayerId,
                (SiegeAmmoRestockModifier)packet.Modifier,
                packet.BaseStoneCost,
                packet.BaseAmmunitionAmount,
                packet.GlobalUnitIds,
                $"Chore {packet.OperationId}");
        }

        private bool IsValidPacket(SiegeAmmoRestockPacket packet)
        {
            if (packet == null || packet.ProtocolVersion != ProtocolVersion || packet.PlayerId < 1 || packet.PlayerId > 8 ||
                packet.OperationId == 0 || packet.BaseStoneCost <= 0 || packet.BaseStoneCost > ushort.MaxValue ||
                packet.BaseAmmunitionAmount <= 0 || packet.BaseAmmunitionAmount > ushort.MaxValue ||
                packet.GlobalUnitIds == null || packet.GlobalUnitIds.Length == 0 ||
                packet.GlobalUnitIds.Length > SiegeAmmoRestockPolicy.MaximumTargetCount ||
                !Enum.IsDefined(typeof(SiegeAmmoRestockModifier), packet.Modifier))
            {
                return false;
            }

            var ids = new HashSet<int>();
            for (int index = 0; index < packet.GlobalUnitIds.Length; index++)
                if (packet.GlobalUnitIds[index] <= 0 || !ids.Add(packet.GlobalUnitIds[index])) return false;
            return true;
        }

        private void ApplyValidated(
            int playerId,
            SiegeAmmoRestockModifier modifier,
            int baseStoneCost,
            int baseAmmunitionAmount,
            int[] globalUnitIds,
            string source)
        {
            if (!TryResolveTargets(playerId, globalUnitIds, out List<ResolvedTarget> resolved))
            {
                LogWarning($"fair siege-ammunition {source} was rejected because a target is no longer valid or owned.");
                return;
            }

            GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
            int availableStone = Math.Max(0, players.GetGoodAmount(playerId, eGoods.STORED_STONE_BLOCKS));
            var snapshots = new SiegeAmmoRestockTarget[resolved.Count];
            for (int index = 0; index < resolved.Count; index++)
                snapshots[index] = new SiegeAmmoRestockTarget(resolved[index].GlobalUnitId, resolved[index].Ammunition);

            if (!SiegeAmmoRestockPolicy.TryCreatePlan(
                    baseStoneCost, baseAmmunitionAmount, modifier, availableStone, snapshots, out SiegeAmmoRestockPlan plan))
            {
                return;
            }

            var finalById = new Dictionary<int, ushort>(plan.Targets.Length);
            for (int index = 0; index < plan.Targets.Length; index++)
                finalById.Add(plan.Targets[index].GlobalUnitId, plan.Targets[index].Ammunition);

            // Revalidate every pointer and value before the one resource mutation.
            for (int index = 0; index < resolved.Count; index++)
            {
                ResolvedTarget target = resolved[index];
                if (!IsEligible(target.Unit, playerId) || (int)target.Unit->r_GlobalId != target.GlobalUnitId ||
                    ReadAmmunition(target.Unit) != target.Ammunition || !finalById.ContainsKey(target.GlobalUnitId))
                {
                    LogWarning($"fair siege-ammunition {source} was aborted during final validation.");
                    return;
                }
            }
            if (players.GetGoodAmount(playerId, eGoods.STORED_STONE_BLOCKS) < plan.StoneCost)
                return;

            players.RemoveGood(playerId, eGoods.STORED_STONE_BLOCKS, plan.StoneCost);
            for (int index = 0; index < resolved.Count; index++)
                WriteAmmunition(resolved[index].Unit, finalById[resolved[index].GlobalUnitId]);

            LogInfo($"fair siege-ammunition {source} applied: playerId={playerId}, targets={resolved.Count}, ammunitionAdded={plan.AmmunitionAdded}, stoneUsed={plan.StoneCost}, modifier={modifier}.");
        }

        private bool TryCaptureSelectedGlobalIds(int playerId, out int[] globalIds)
        {
            globalIds = null;
            if (playerId < 1 || playerId > 8)
                return false;
            if (!LocalSelectionSnapshot.TryCapture(playerId, out APIShared.LocalSelectionSnapshot selected))
                return false;
            var ids = new List<int>();
            var unique = new HashSet<int>();
            for (int index = 0; index < selected.Count; index++)
            {
                int unitId = selected[index].UnitId;
                if (unitId <= 0 ||
                    !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                    !IsEligible(unit, playerId))
                {
                    continue;
                }

                int globalId = (int)unit->r_GlobalId;
                if (globalId > 0 && unique.Add(globalId)) ids.Add(globalId);
            }
            ids.Sort();
            if (ids.Count == 0 || ids.Count > SiegeAmmoRestockPolicy.MaximumTargetCount) return false;
            globalIds = ids.ToArray();
            return true;
        }

        private bool TryResolveTargets(int playerId, int[] globalIds, out List<ResolvedTarget> resolved)
        {
            resolved = new List<ResolvedTarget>(globalIds?.Length ?? 0);
            if (globalIds == null || globalIds.Length == 0 || globalIds.Length > SiegeAmmoRestockPolicy.MaximumTargetCount)
                return false;
            var requested = new HashSet<int>(globalIds);
            if (requested.Count != globalIds.Length || requested.Contains(0)) return false;
            int[] alive = APIShared.UnitAccess.GetAllReallyAliveUnits();
            for (int index = 0; index < alive.Length && resolved.Count < requested.Count; index++)
            {
                if (!APIShared.UnitAccess.TryGetById(alive[index], out GameUnit* unit, out _)) continue;
                int globalId = (int)unit->r_GlobalId;
                if (requested.Contains(globalId) && IsEligible(unit, playerId))
                    resolved.Add(new ResolvedTarget(globalId, unit, ReadAmmunition(unit)));
            }
            return resolved.Count == requested.Count;
        }

        private static bool IsEligible(GameUnit* unit, int playerId) =>
            unit != null && APIShared.UnitAccess.IsReallyAlive(unit) &&
            unit->r_ControllableForPlayerId == playerId &&
            (unit->r_UnitChimp == eChimps.CHIMP_TYPE_CATAPULT || unit->r_UnitChimp == eChimps.CHIMP_TYPE_TREBUCHET);

        private static ushort ReadAmmunition(GameUnit* unit) =>
            (ushort)(unit->r_StoneAmmoLeft | (unit->r_StoneAmmoStacksLeft << 8));

        private static void WriteAmmunition(GameUnit* unit, ushort value)
        {
            unit->r_StoneAmmoLeft = (byte)value;
            unit->r_StoneAmmoStacksLeft = (byte)(value >> 8);
        }

        private static int GetControlledPlayerId() => APIShared.GameModes.GameModeHelper.IsMapEditor()
            ? EditorDirector.instance?.ActivePlayerID ?? -1
            : GamePlayerManagerAPI.Instance.GetLocalPlayerId();

        private static SiegeAmmoRestockModifier CaptureModifier()
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            return shift && control ? SiegeAmmoRestockModifier.ShiftAndControl :
                shift ? SiegeAmmoRestockModifier.Shift :
                control ? SiegeAmmoRestockModifier.Control : SiegeAmmoRestockModifier.Normal;
        }

        private static bool TryReadConfiguredPackage(out int cost, out int amount)
        {
            cost = GameGlobalsManager.Instance.CatapultRestockStoneCost?.GetValue() ?? 0;
            amount = GameGlobalsManager.Instance.CatapultRestockStoneAmount?.GetValue() ?? 0;
            return cost > 0 && amount > 0;
        }

        private bool RememberOperation(long key)
        {
            if (!processedOperations.Add(key)) return false;
            processedOperationOrder.Enqueue(key);
            while (processedOperationOrder.Count > MaximumRememberedOperations)
                processedOperations.Remove(processedOperationOrder.Dequeue());
            return true;
        }

        private void LogInfo(string message) => Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(
            () => Shared.DebugLogHelper.LogDebug(log, message));
        private void LogWarning(string message) => Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(
            () => Shared.DebugLogHelper.LogWarning(log, message));
        private void LogError(string message) => Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(
            () => Shared.DebugLogHelper.LogError(log, message));

        private readonly struct ResolvedTarget
        {
            internal ResolvedTarget(int globalUnitId, GameUnit* unit, ushort ammunition)
            {
                GlobalUnitId = globalUnitId;
                Unit = unit;
                Ammunition = ammunition;
            }
            internal int GlobalUnitId { get; }
            internal GameUnit* Unit { get; }
            internal ushort Ammunition { get; }
        }
    }
}
