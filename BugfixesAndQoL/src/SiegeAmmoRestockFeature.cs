// Feature: Fair single- and multi-selection catapult/trebuchet ammunition restocking.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
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
using System.Reflection;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal sealed unsafe class SiegeAmmoRestockFeature : IDisposable
    {
        private delegate void RechargeRockDelegate(MainViewModel self, object parameter);
        private delegate void TroopPanelMouseDelegate(MainViewModel self, object parameter);

        private const int ProtocolVersion = 1;
        private const int MaximumBlobBytes = 1200;
        private const int MaximumRememberedOperations = 2048;
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly MultiplayerFeatureGate multiplayerFeatureGate;
        private readonly HashSet<long> processedOperations = new HashSet<long>();
        private readonly Queue<long> processedOperationOrder = new Queue<long>();
        private Hook buttonHook;
        private RechargeRockDelegate buttonTrampoline;
        private Hook mouseEnterHook;
        private Hook mouseLeaveHook;
        private TroopPanelMouseDelegate mouseEnterTrampoline;
        private TroopPanelMouseDelegate mouseLeaveTrampoline;
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
            if (packetHook == null)
            {
                packetHook = GameNetworkAPI.Instance.GetPacketEventFor<SiegeAmmoRestockPacket>();
                packetSubscription = packetHook.GetBaseHook().Observable.Subscribe(OnPacketReceived);
            }

            if (buttonHook != null)
                return;

            MethodInfo method = FindMainViewModelMethod(
                "ButtonUnitRechargeRock",
                typeof(object));
            MethodInfo enterMethod = FindMainViewModelMethod("ButtonTroopPanelMouseEnter", typeof(object));
            MethodInfo leaveMethod = FindMainViewModelMethod("ButtonTroopPanelMouseLeave", typeof(object));

            Hook installed = null;
            Hook installedEnter = null;
            Hook installedLeave = null;
            try
            {
                installed = new Hook(method, (RechargeRockDelegate)OnRechargeRock);
                RechargeRockDelegate trampoline = installed.GenerateTrampoline<RechargeRockDelegate>();
                installedEnter = new Hook(enterMethod, (TroopPanelMouseDelegate)OnTroopPanelMouseEnter);
                TroopPanelMouseDelegate enterTrampoline = installedEnter.GenerateTrampoline<TroopPanelMouseDelegate>();
                installedLeave = new Hook(leaveMethod, (TroopPanelMouseDelegate)OnTroopPanelMouseLeave);
                TroopPanelMouseDelegate leaveTrampoline = installedLeave.GenerateTrampoline<TroopPanelMouseDelegate>();
                buttonHook = installed;
                buttonTrampoline = trampoline;
                mouseEnterHook = installedEnter;
                mouseEnterTrampoline = enterTrampoline;
                mouseLeaveHook = installedLeave;
                mouseLeaveTrampoline = leaveTrampoline;
                // Key transitions are event-driven; no per-frame callback remains active in the neutral state.
                keyDownSubscription = InputR3EventHooks.OnKeyDown.Observable.Subscribe(OnModifierKeyChanged);
                keyUpSubscription = InputR3EventHooks.OnKeyUp.Observable.Subscribe(OnModifierKeyChanged);
                LogInfo($"fair siege-ammunition hook initialized: packetId={packetHook.GetPacketId()}, protocol={ProtocolVersion}.");
            }
            catch
            {
                keyUpSubscription?.Dispose();
                keyUpSubscription = null;
                keyDownSubscription?.Dispose();
                keyDownSubscription = null;
                mouseLeaveHook = null;
                mouseLeaveTrampoline = null;
                mouseEnterHook = null;
                mouseEnterTrampoline = null;
                buttonHook = null;
                buttonTrampoline = null;
                installedLeave?.Dispose();
                installedEnter?.Dispose();
                installed?.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            // The runtime is process-lived, but keep conventional cleanup safe for test hosts.
            keyUpSubscription?.Dispose();
            keyUpSubscription = null;
            keyDownSubscription?.Dispose();
            keyDownSubscription = null;
            mouseLeaveHook?.Undo();
            mouseLeaveHook?.Dispose();
            mouseLeaveHook = null;
            mouseLeaveTrampoline = null;
            mouseEnterHook?.Undo();
            mouseEnterHook?.Dispose();
            mouseEnterHook = null;
            mouseEnterTrampoline = null;
            buttonHook?.Undo();
            buttonHook?.Dispose();
            buttonHook = null;
            buttonTrampoline = null;
            packetSubscription?.Dispose();
            packetSubscription = null;
        }

        private static MethodInfo FindMainViewModelMethod(string name, params Type[] parameterTypes)
        {
            MethodInfo method = typeof(MainViewModel).GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);
            if (method == null)
                throw new MissingMethodException(typeof(MainViewModel).FullName, name);
            return method;
        }

        private void OnTroopPanelMouseEnter(MainViewModel self, object parameter)
        {
            mouseEnterTrampoline(self, parameter);
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
            mouseLeaveTrampoline(self, parameter);
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
            if (!settings.EnableMod || !settings.EnableFairSiegeAmmoRestock)
            {
                buttonTrampoline(self, parameter);
                return;
            }

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
            if (packetHook == null || !ChoreNetworkTransport.IsAvailable)
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
            byte[] body = GameNetworkAPI.Serialize(packet);
            byte[] blob = new byte[sizeof(short) + body.Length];
            BitConverter.GetBytes(packetHook.GetPacketId()).CopyTo(blob, 0);
            Buffer.BlockCopy(body, 0, blob, sizeof(short), body.Length);
            if (blob.Length >= MaximumBlobBytes)
            {
                LogError($"fair siege-ammunition restock exceeded the Chore payload limit: bytes={blob.Length}.");
                return false;
            }

            Func<byte[], bool> send = ChoreNetworkTransport.SendRawBlob;
            bool queued = send != null && send(blob);
            if (!queued)
            {
                LogError($"fair siege-ammunition Chore was not queued; no local mutation occurred: operationId={packet.OperationId}.");
                return false;
            }

            LogInfo($"fair siege-ammunition Chore queued: playerId={playerId}, operationId={packet.OperationId}, targets={globalUnitIds.Length}, modifier={modifier}, bytes={blob.Length}.");
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
            int[] selected = GamePlayerManagerAPI.Instance.GetSelectedChimps() ?? Array.Empty<int>();
            var ids = new List<int>();
            var unique = new HashSet<int>();
            for (int index = 0; index < selected.Length; index++)
            {
                int unitId = selected[index];
                if (unitId <= 0 ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
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
            int[] alive = GameUnitManagerAPI.Instance.GetAllAliveUnits();
            for (int index = 0; index < alive.Length && resolved.Count < requested.Count; index++)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(alive[index], out GameUnit* unit)) continue;
                int globalId = (int)unit->r_GlobalId;
                if (requested.Contains(globalId) && IsEligible(unit, playerId))
                    resolved.Add(new ResolvedTarget(globalId, unit, ReadAmmunition(unit)));
            }
            return resolved.Count == requested.Count;
        }

        private static bool IsEligible(GameUnit* unit, int playerId) =>
            unit != null && unit->r_AliveState == AliveState.IsAlive &&
            unit->r_ControllableForPlayerId == playerId &&
            (unit->r_UnitChimp == eChimps.CHIMP_TYPE_CATAPULT || unit->r_UnitChimp == eChimps.CHIMP_TYPE_TREBUCHET);

        private static ushort ReadAmmunition(GameUnit* unit) =>
            (ushort)(unit->r_StoneAmmoLeft | (unit->r_StoneAmmoStacksLeft << 8));

        private static void WriteAmmunition(GameUnit* unit, ushort value)
        {
            unit->r_StoneAmmoLeft = (byte)value;
            unit->r_StoneAmmoStacksLeft = (byte)(value >> 8);
        }

        private static int GetControlledPlayerId() => Shared.GameModeHelper.IsMapEditor()
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

        private void LogInfo(string message) => Shared.DebugLogHelper.LogInfo(log, message);
        private void LogWarning(string message) => Shared.DebugLogHelper.LogWarning(log, message);
        private void LogError(string message) => Shared.DebugLogHelper.LogError(log, message);

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
