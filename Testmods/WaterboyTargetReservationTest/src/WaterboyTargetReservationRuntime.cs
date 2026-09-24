using BepInEx.Logging;
using CrusaderDE;
using MessagePack;
using MonoMod.RuntimeDetour;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Network;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace WaterboyTargetReservationTest
{
    internal sealed unsafe class WaterboyTargetReservationRuntime
    {
        private const int MaximumDetailedLogs = 200;
        private const int ChoreProtocolVersion = 1;
        private const int MaximumChorePayloadBytes = 1200;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int FindNearestBurningBuildingDelegate(IntPtr buildingManager, int nativeUnitId);
        private delegate void SetUpInbuildingDelegate(MainViewModel self, int overridePanel, int overrideType);

        private readonly ManualLogSource log;
        private readonly WaterboySettings settings;
        private readonly ReservationLedger ledger = new ReservationLedger();
        private readonly List<FireReservation> reservationScratch = new List<FireReservation>();
        private readonly List<MaskedFire> maskedFireScratch = new List<MaskedFire>();
        private readonly DetourHandle<FindNearestBurningBuildingDelegate> targetSearchHook =
            new DetourHandle<FindNearestBurningBuildingDelegate>();
        private readonly HookTransaction transaction;
        private readonly IDisposable mapUnloadSubscription;
        private readonly R3PacketEventHook<WaterboyModePacket> modePacketHook;
        private readonly IDisposable modePacketSubscription;
        private readonly Hook setUpInbuildingHook;
        private readonly SetUpInbuildingDelegate setUpInbuildingTrampoline;
        private readonly bool[] effectiveModes = { true, true, true, true, true, true, true, true, true };
        private readonly bool[] observedSettingModes = { true, true, true, true, true, true, true, true, true };
        private readonly int[] lastOperationIds = new int[9];
        private readonly int targetSearchDisplacedByteCount;
        private readonly string targetSearchScheme;
        private readonly WaterboyButtonViewModel buttonViewModel;
        private volatile bool correctionAvailable = true;
        private bool buttonAvailable = true;
        private bool postStartupLivenessLogged;
        private bool initialMapSeedCompleted;
        private bool uiRefreshPending;
        private int nextOperationId;
        private int detailedLogCount;
        private bool detailLimitLogged;

        internal WaterboyTargetReservationRuntime(ManualLogSource log, WaterboySettings settings,
            CrusaderLibraryLoadContext context, bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (!referenceHashMatches || !string.Equals(WaterboyNativeDefinition.ReferenceSha256,
                Shared.DebugLogHelper.CurrentNativeSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The waterboy native contract is not validated for this CrusaderDE.dll.");

            ValidateRuntimeDependencies();
            ValidateManagedLayout();
            for (int playerId = 1; playerId <= 8; playerId++)
                effectiveModes[playerId] = observedSettingModes[playerId] = settings.IsEnabledForPlayer(playerId);
            buttonViewModel = new WaterboyButtonViewModel(OnToggleCommand);

            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                context.Memory, WaterboyNativeDefinition.FindNearestBurningBuildingPattern,
                WaterboyNativeDefinition.FindNearestBurningBuildingRva, referenceHashMatches,
                "waterboy nearest-burning-building search", log);
            if (resolution.Rva != WaterboyNativeDefinition.FindNearestBurningBuildingRva)
                throw new InvalidOperationException("The waterboy target search resolved outside its audited RVA.");

            HookTransaction pendingTransaction = null;
            Hook pendingManagedHook = null;
            IDisposable pendingMapUnload = null;
            IDisposable pendingPacketSubscription = null;
            bool pendingTick = false;
            try
            {
                R3PacketEventHook<WaterboyModePacket> pendingPacketHook =
                    GameNetworkAPI.Instance.GetPacketEventFor<WaterboyModePacket>();
                pendingPacketSubscription = pendingPacketHook.GetBaseHook().Observable.Subscribe(OnModePacketReceived);

                pendingManagedHook = new Hook(FindSetUpInbuildingMethod(),
                    (SetUpInbuildingDelegate)SetUpInbuildingHook);
                SetUpInbuildingDelegate pendingTrampoline =
                    pendingManagedHook.GenerateTrampoline<SetUpInbuildingDelegate>();

                pendingTransaction = new HookTransaction(context.Region,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions
                    {
                        FailureMode = TransactionFailureMode.RollbackAndThrow,
                        OwnsHooks = true
                    });
                ulong expectedTargetAddress = unchecked((ulong)context.ModuleHandle.ToInt64()) +
                    unchecked((ulong)resolution.Rva);
                pendingTransaction.AddDetour(targetSearchHook,
                    HookTarget.FromAddress(expectedTargetAddress), FindNearestEligibleFire);
                CommitResult result = pendingTransaction.Commit();
                NativeDetour<FindNearestBurningBuildingDelegate> committedDetour =
                    targetSearchHook.Hook as NativeDetour<FindNearestBurningBuildingDelegate>;
                if (!result.IsCompleteSuccess || !targetSearchHook.Success)
                    throw new InvalidOperationException($"The waterboy target-search detour was not installed: {result}.");
                ValidateCommittedDetour(committedDetour, expectedTargetAddress);

                GameTimeManagerAPI.Instance.GetFrameProvider().OnGameTick += OnGameTick;
                pendingTick = true;
                pendingMapUnload = MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(_ => OnMapUnload());

                transaction = pendingTransaction;
                setUpInbuildingHook = pendingManagedHook;
                setUpInbuildingTrampoline = pendingTrampoline;
                modePacketHook = pendingPacketHook;
                modePacketSubscription = pendingPacketSubscription;
                mapUnloadSubscription = pendingMapUnload;
                targetSearchDisplacedByteCount = committedDetour.DisplacedByteCount;
                targetSearchScheme = committedDetour.Scheme.ToString();
                pendingTransaction = null;
                pendingManagedHook = null;
                pendingPacketSubscription = null;
                pendingMapUnload = null;
                pendingTick = false;
            }
            catch
            {
                if (pendingTick)
                    GameTimeManagerAPI.Instance.GetFrameProvider().OnGameTick -= OnGameTick;
                pendingMapUnload?.Dispose();
                pendingPacketSubscription?.Dispose();
                pendingTransaction?.Dispose();
                pendingManagedHook?.Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogInfo(log,
                $"Waterboy target reservations active: method={resolution.Method}, rva=0x{resolution.Rva:X}, " +
                $"scheme={targetSearchScheme}, displaced={targetSearchDisplacedByteCount}, " +
                $"span=0x{resolution.Rva:X}-0x{resolution.Rva + targetSearchDisplacedByteCount:X}, " +
                $"stationaryTimeoutTicks={ReservationLedger.StationaryTimeoutTicks}, baseGameSpeed=40, " +
                "linkedCompounds=true, perPlayer=true, nearestTakeover=true, liveToggle=true.");
        }

        internal WaterboyButtonViewModel ButtonViewModel => buttonViewModel;

        internal void DisableButton(string reason, Exception exception)
        {
            buttonAvailable = false;
            buttonViewModel.Update(false, false, true, true);
            Shared.DebugLogHelper.LogError(log,
                $"Waterboy mode button disabled; target reservations remain active. reason={reason}; exception={exception}");
        }

        private int FindNearestEligibleFire(IntPtr buildingManager, int nativeUnitId)
        {
            if (!correctionAvailable)
                return targetSearchHook.Original(buildingManager, nativeUnitId);

            NativeIdentity requester;
            GameUnit* requesterUnit;
            int requesterPlayerId;
            try
            {
                requester = ResolveLivingFiremanIdentity(nativeUnitId);
                requesterUnit = ResolveLivingFireman(requester);
                requesterPlayerId = requesterUnit->r_ControllableForPlayerId;
                if (!IsValidPlayerId(requesterPlayerId) || !effectiveModes[requesterPlayerId])
                    return targetSearchHook.Original(buildingManager, nativeUnitId);
                PruneInvalidReservations(CurrentGameTick());
            }
            catch (Exception exception)
            {
                DisableCorrection("pre-search validation failed", exception);
                return targetSearchHook.Original(buildingManager, nativeUnitId);
            }

            List<MaskedFire> masked = maskedFireScratch;
            masked.Clear();
            FireReservation displacedOwner = null;
            int requesterDistance = 0;
            int previousOwnerDistance = 0;
            int selectedBuildingId = 0;
            bool selectionAccepted = false;
            Exception selectionFailure = null;
            try
            {
                MaskRequesterSuppression(requester, requesterUnit, masked);
                int attemptLimit = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan().Length + 1;
                for (int attempt = 0; attempt < attemptLimit; attempt++)
                {
                    selectedBuildingId = targetSearchHook.Original(buildingManager, nativeUnitId);
                    if (selectedBuildingId == 0)
                    {
                        ledger.Release(requester.GlobalId, clearSuppression: false);
                        selectionAccepted = true;
                        break;
                    }

                    GameBuilding* target = ResolveBurningBuilding(selectedBuildingId);
                    NativeIdentity targetIdentity = new NativeIdentity(selectedBuildingId, target->r_GlobalId);
                    uint compoundKey = ReadCompoundKey(target);
                    if (!ledger.TryGetCoveringReservation(requesterPlayerId, targetIdentity,
                        compoundKey, requester.GlobalId, out FireReservation conflict))
                    {
                        ReservationClaimResult claim = ledger.Claim(requesterPlayerId, requester,
                            targetIdentity, compoundKey, requesterUnit->r_CurrentTilePositionX,
                            requesterUnit->r_CurrentTilePositionY, CurrentGameTick());
                        if (claim == ReservationClaimResult.Conflict)
                            throw new InvalidOperationException("Reservation changed during target claim.");
                        if (claim == ReservationClaimResult.Suppressed)
                        {
                            MaskCoverage(targetIdentity, compoundKey, masked);
                            continue;
                        }
                        LogDetail($"waterboy reservation created: player={requesterPlayerId}, owner={requester}, target={targetIdentity}, compound={compoundKey}.");
                        selectionAccepted = true;
                        break;
                    }

                    if (TryResolveLivingFireman(conflict.Owner, out GameUnit* currentOwner) &&
                        currentOwner->r_ControllableForPlayerId == requesterPlayerId &&
                        TryResolveBurningBuilding(conflict.Target, out GameBuilding* ownerTarget))
                    {
                        requesterDistance = Distance(requesterUnit, target);
                        previousOwnerDistance = Distance(currentOwner, ownerTarget);
                        if (WaterboyTargetPolicy.CanTakeOver(requesterUnit->r_AIState,
                            currentOwner->r_AIState, requesterDistance, previousOwnerDistance) &&
                            ledger.TryTransfer(conflict, requesterPlayerId, requester, targetIdentity,
                                compoundKey, requesterUnit->r_CurrentTilePositionX,
                                requesterUnit->r_CurrentTilePositionY, CurrentGameTick()))
                        {
                            displacedOwner = conflict;
                            selectionAccepted = true;
                            break;
                        }
                    }

                    MaskCoverage(targetIdentity, compoundKey, masked);
                }
                if (!selectionAccepted)
                    throw new InvalidOperationException("The bounded target-selection loop exhausted all building candidates.");
            }
            catch (Exception exception)
            {
                selectionFailure = exception;
            }
            finally
            {
                RestoreMaskedFires(masked, disableOnFailure: true);
                masked.Clear();
            }

            if (selectionFailure != null)
            {
                DisableCorrection("target selection or reservation failed", selectionFailure);
                return targetSearchHook.Original(buildingManager, nativeUnitId);
            }

            if (correctionAvailable && displacedOwner != null)
            {
                InvalidateDisplacedOwner(displacedOwner);
                LogDetail($"waterboy reservation takeover: player={requesterPlayerId}, oldOwner={displacedOwner.Owner}, newOwner={requester}, target={selectedBuildingId}, oldDistance={previousOwnerDistance}, newDistance={requesterDistance}.");
            }
            return selectedBuildingId;
        }

        private void OnGameTick(int currentTick)
        {
            if (!correctionAvailable)
                return;
            try
            {
                if (!postStartupLivenessLogged)
                {
                    postStartupLivenessLogged = true;
                    Shared.DebugLogHelper.LogInfo(log,
                        "Waterboy target reservation runtime reached OnGameTick after startup cleanup; the permanent native and managed hooks remain active.");
                }
                ReconcileSettingModes(currentTick);
                if (!initialMapSeedCompleted)
                {
                    initialMapSeedCompleted = true;
                    for (int playerId = 1; playerId <= 8; playerId++)
                        if (effectiveModes[playerId])
                        {
                            ledger.ClearPlayer(playerId);
                            SeedPlayerReservations(playerId, currentTick);
                        }
                }
                PruneInvalidReservations(currentTick);
                if (uiRefreshPending)
                {
                    uiRefreshPending = false;
                    RefreshButtonVisibility();
                }
            }
            catch (Exception exception)
            {
                DisableCorrection("per-tick reservation validation failed", exception);
            }
        }

        private void ReconcileSettingModes(int currentTick)
        {
            for (int playerId = 1; playerId <= 8; playerId++)
            {
                bool settingMode = settings.IsEnabledForPlayer(playerId);
                if (settingMode == observedSettingModes[playerId])
                    continue;
                observedSettingModes[playerId] = settingMode;
                ApplyMode(playerId, settingMode, currentTick, "synchronized-setting");
            }
        }

        private void ApplyMode(int playerId, bool enabled, int currentTick, string source)
        {
            if (!IsValidPlayerId(playerId) || effectiveModes[playerId] == enabled)
                return;
            effectiveModes[playerId] = enabled;
            ledger.ClearPlayer(playerId);
            if (enabled)
                SeedPlayerReservations(playerId, currentTick);
            Shared.DebugLogHelper.LogInfo(log,
                $"Waterboy targeting mode changed: player={playerId}, optimized={enabled}, source={source}, tick={currentTick}.");
            uiRefreshPending = true;
        }

        private void SeedPlayerReservations(int playerId, int currentTick)
        {
            var candidates = new List<SeedCandidate>();
            int unitCount = GameUnitManagerAPI.Instance.GetUnitsAsSpan().Length;
            for (int unitId = 1; unitId <= unitCount; unitId++)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_UnitChimp != eChimps.CHIMP_TYPE_FIREMAN ||
                    unit->r_ControllableForPlayerId != playerId ||
                    (unit->r_AIState != ReservationLedger.WalkingState &&
                     unit->r_AIState != ReservationLedger.ExtinguishingState))
                    continue;
                NativeIdentity targetIdentity = ReadTargetIdentity(unit);
                if (!TryResolveBurningBuilding(targetIdentity, out GameBuilding* target))
                    continue;
                candidates.Add(new SeedCandidate(unitId, unit->r_GlobalId, unit->r_AIState,
                    targetIdentity, ReadCompoundKey(target), unit->r_CurrentTilePositionX,
                    unit->r_CurrentTilePositionY, Distance(unit, target)));
            }
            candidates.Sort(SeedCandidate.Compare);
            foreach (SeedCandidate candidate in candidates)
                ledger.Claim(playerId, candidate.Owner, candidate.Target, candidate.CompoundKey,
                    candidate.X, candidate.Y, currentTick);
        }

        private void PruneInvalidReservations(int currentTick)
        {
            ledger.CopyReservationsTo(reservationScratch);
            foreach (FireReservation reservation in reservationScratch)
            {
                bool ownerValid = TryResolveLivingFireman(reservation.Owner, out GameUnit* owner);
                int ownerState = ownerValid ? owner->r_AIState : 0;
                int ownerPlayerId = ownerValid ? owner->r_ControllableForPlayerId : 0;
                NativeIdentity currentTarget = ownerValid ? ReadTargetIdentity(owner) : default;
                ushort x = ownerValid ? owner->r_CurrentTilePositionX : (ushort)0;
                ushort y = ownerValid ? owner->r_CurrentTilePositionY : (ushort)0;
                bool targetBurning = TryResolveBurningBuilding(reservation.Target, out GameBuilding* target);
                uint compoundKey = targetBurning ? ReadCompoundKey(target) : 0;
                ReservationReconcileResult result = ledger.Reconcile(reservation, ownerValid,
                    ownerPlayerId, ownerState, currentTarget, targetBurning, compoundKey,
                    x, y, currentTick);
                if (result == ReservationReconcileResult.Released)
                    LogDetail($"waterboy reservation released as invalid: player={reservation.PlayerId}, owner={reservation.Owner}, target={reservation.Target}.");
                else if (result == ReservationReconcileResult.Stalled)
                    LogDetail($"waterboy reservation released after stationary timeout: player={reservation.PlayerId}, owner={reservation.Owner}, target={reservation.Target}.");
            }
        }

        private void MaskRequesterSuppression(NativeIdentity requester, GameUnit* requesterUnit,
            List<MaskedFire> masked)
        {
            if (!ledger.TryGetSuppressedCoverage(requester, requesterUnit->r_CurrentTilePositionX,
                requesterUnit->r_CurrentTilePositionY, out NativeIdentity target, out uint compound))
                return;
            if (!TryResolveBurningBuilding(target, out GameBuilding* building) ||
                ReadCompoundKey(building) != compound)
            {
                ledger.ClearSuppression(requester.GlobalId);
                return;
            }
            MaskCoverage(target, compound, masked);
        }

        private void MaskCoverage(NativeIdentity target, uint compoundKey, List<MaskedFire> masked)
        {
            int count = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan().Length;
            for (int buildingId = 1; buildingId <= count; buildingId++)
            {
                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                    building == null || building->r_AliveState != AliveState.IsAlive || building->r_OnFireTicks == 0)
                    continue;
                NativeIdentity identity = new NativeIdentity(buildingId, building->r_GlobalId);
                uint candidateCompound = ReadCompoundKey(building);
                if (!ReservationLedger.CoversSuppression(identity, candidateCompound, target, compoundKey))
                    continue;
                bool alreadyMasked = false;
                foreach (MaskedFire item in masked)
                    if (item.Identity.Equals(identity)) { alreadyMasked = true; break; }
                if (alreadyMasked)
                    continue;
                masked.Add(new MaskedFire(identity, building->r_OnFireTicks));
                building->r_OnFireTicks = 0;
            }
        }

        private void RestoreMaskedFires(List<MaskedFire> masked, bool disableOnFailure)
        {
            Exception firstFailure = null;
            foreach (MaskedFire item in masked)
            {
                try
                {
                    if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(item.Identity.Slot, out GameBuilding* building) ||
                        building == null || building->r_GlobalId != item.Identity.GlobalId)
                        throw new InvalidOperationException($"Masked building identity changed: {item.Identity}.");
                    if (building->r_OnFireTicks != 0)
                        throw new InvalidOperationException($"Masked fire counter changed: {item.Identity}.");
                    building->r_OnFireTicks = item.OriginalFireTicks;
                }
                catch (Exception exception) { if (firstFailure == null) firstFailure = exception; }
            }
            if (firstFailure == null)
                return;
            if (disableOnFailure)
                DisableCorrection("temporary fire counters could not be restored safely", firstFailure);
            else
                Shared.DebugLogHelper.LogError(log, $"Waterboy reservation mask rollback failed: {firstFailure}");
        }

        private void InvalidateDisplacedOwner(FireReservation displaced)
        {
            if (!TryResolveLivingFireman(displaced.Owner, out GameUnit* owner) ||
                owner->r_AIState != ReservationLedger.WalkingState ||
                !ReadTargetIdentity(owner).Equals(displaced.Target))
                return;
            *(uint*)((byte*)owner + WaterboyNativeDefinition.FiremanTargetGlobalIdOffset) = 0;
        }

        private void SetUpInbuildingHook(MainViewModel self, int overridePanel, int overrideType)
        {
            setUpInbuildingTrampoline(self, overridePanel, overrideType);
            RefreshButtonVisibility();
        }

        private void RefreshButtonVisibility()
        {
            try
            {
                if (!buttonAvailable)
                {
                    buttonViewModel.Update(false, false, true, true);
                    return;
                }
                int playerId = GetControlledPlayerId();
                int buildingId = GamePlayerManagerAPI.Instance.GetSelectedBuildingId();
                bool visible = TryGetOwnedWaterBuilding(buildingId, playerId, out _);
                bool multiplayer = Shared.GameModeHelper.IsRealMultiplayer();
                bool syncUnavailable = multiplayer && !IsChoreReady();
                buttonViewModel.Update(visible, visible && correctionAvailable && !syncUnavailable,
                    IsValidPlayerId(playerId) && effectiveModes[playerId], syncUnavailable);
            }
            catch (Exception exception)
            {
                DisableButton("visibility refresh failed", exception);
            }
        }

        private void OnToggleCommand()
        {
            try
            {
                if (!correctionAvailable)
                {
                    RefreshButtonVisibility();
                    return;
                }
                if (!buttonAvailable)
                    return;
                int playerId = GetControlledPlayerId();
                int buildingId = GamePlayerManagerAPI.Instance.GetSelectedBuildingId();
                if (!TryGetOwnedWaterBuilding(buildingId, playerId, out GameBuilding* building))
                {
                    RefreshButtonVisibility();
                    return;
                }
                bool enabled = !effectiveModes[playerId];
                int operationId = NextOperationId();
                if (Shared.GameModeHelper.IsRealMultiplayer())
                {
                    if (!TrySendModeChore(playerId, operationId, buildingId, building->r_GlobalId, enabled))
                        RefreshButtonVisibility();
                    return;
                }
                ApplyReceivedMode(playerId, operationId, buildingId, building->r_GlobalId,
                    enabled, "singleplayer-button");
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(log, $"Waterboy mode button click failed: {exception}");
                RefreshButtonVisibility();
            }
        }

        private bool TrySendModeChore(int playerId, int operationId, int buildingId,
            uint buildingGlobalId, bool enabled)
        {
            if (!IsChoreReady())
                return false;
            var packet = new WaterboyModePacket
            {
                ProtocolVersion = ChoreProtocolVersion,
                PlayerId = playerId,
                OperationId = operationId,
                SourceBuildingId = buildingId,
                SourceBuildingGlobalId = buildingGlobalId,
                Enabled = enabled
            };
            try
            {
                byte[] body = GameNetworkAPI.Serialize(packet);
                if (body == null || sizeof(short) + body.Length > MaximumChorePayloadBytes ||
                    GameGlobalsManager.Instance.ChoreManagerVA == 0)
                    return false;
                short packetId = modePacketHook.GetPacketId();
                GameNetworkAPI.SendPacketToAllEx2(packet, packetId, viaChore: true);
                Shared.DebugLogHelper.LogInfo(log,
                    $"Waterboy mode Chore queued: player={playerId}, operation={operationId}, optimized={enabled}, payloadBytes={sizeof(short) + body.Length}.");
                return true;
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(log,
                    $"Waterboy mode Chore was not queued; no local change was applied: operation={operationId}, exception={exception}");
                return false;
            }
        }

        private void OnModePacketReceived(ReceiveCustomPacketEventArgs<WaterboyModePacket> args)
        {
            // A transport sender identifies the asynchronous Steam fallback. This packet is
            // simulation-authoritative only when it arrived through the sender-less Chore path.
            if (args?.SenderSteamId != null)
                return;
            WaterboyModePacket packet = args?.Packet;
            if (packet == null || packet.ProtocolVersion != ChoreProtocolVersion ||
                !IsValidPlayerId(packet.PlayerId) || packet.OperationId <= 0)
                return;
            ApplyReceivedMode(packet.PlayerId, packet.OperationId, packet.SourceBuildingId,
                packet.SourceBuildingGlobalId, packet.Enabled, "multiplayer-chore");
        }

        private void ApplyReceivedMode(int playerId, int operationId, int buildingId,
            uint buildingGlobalId, bool enabled, string source)
        {
            if (!WaterboyModeOperationPolicy.TryAccept(lastOperationIds[playerId], operationId,
                    out int acceptedOperationId) ||
                !TryGetOwnedWaterBuilding(buildingId, playerId, out GameBuilding* building) ||
                building->r_GlobalId != buildingGlobalId)
                return;
            lastOperationIds[playerId] = acceptedOperationId;
            int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            settings.ApplyChoreValue(playerId, enabled, localPlayerId == playerId);
            observedSettingModes[playerId] = enabled;
            ApplyMode(playerId, enabled, CurrentGameTick(), source);
        }

        private bool IsChoreReady()
        {
            try { return modePacketHook != null && modePacketHook.GetPacketId() != 0 &&
                    GameGlobalsManager.Instance.ChoreManagerVA != 0; }
            catch { return false; }
        }

        private void OnMapUnload()
        {
            ledger.Clear();
            reservationScratch.Clear();
            maskedFireScratch.Clear();
            initialMapSeedCompleted = false;
            buttonViewModel.Update(false, false, true, false);
        }

        private static MethodInfo FindSetUpInbuildingMethod()
        {
            MethodInfo method = typeof(MainViewModel).GetMethod("setUpInbuilding",
                BindingFlags.Instance | BindingFlags.Public, null,
                new[] { typeof(int), typeof(int) }, null);
            if (method == null)
                throw new MissingMethodException(typeof(MainViewModel).FullName, "setUpInbuilding");
            return method;
        }

        private static bool TryGetOwnedWaterBuilding(int buildingId, int playerId,
            out GameBuilding* building)
        {
            building = null;
            return IsValidPlayerId(playerId) && buildingId > 0 &&
                GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out building) &&
                building != null && building->r_AliveState == AliveState.IsAlive &&
                building->r_PlayerIdOwner == playerId &&
                (building->r_BuildingType == eStructs.STRUCT_WELL ||
                 building->r_BuildingType == eStructs.STRUCT_WATERPOT);
        }

        private static int GetControlledPlayerId()
        {
            if (Shared.GameModeHelper.IsMapEditor())
                return EditorDirector.instance?.ActivePlayerID ?? -1;
            return GamePlayerManagerAPI.Instance.GetLocalPlayerId();
        }

        private int NextOperationId()
        {
            if (nextOperationId == int.MaxValue)
                throw new InvalidOperationException("The process-wide waterboy mode operation sequence is exhausted.");
            return ++nextOperationId;
        }

        private NativeIdentity ResolveLivingFiremanIdentity(int unitId)
        {
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                unit == null || unit->r_AliveState != AliveState.IsAlive ||
                unit->r_UnitChimp != eChimps.CHIMP_TYPE_FIREMAN || unit->r_GlobalId == 0)
                throw new InvalidOperationException($"Target search received an invalid fireman unit: {unitId}.");
            return new NativeIdentity(unitId, unit->r_GlobalId);
        }

        private static GameUnit* ResolveLivingFireman(NativeIdentity identity)
        {
            if (!TryResolveLivingFireman(identity, out GameUnit* unit))
                throw new InvalidOperationException($"Fireman identity is no longer valid: {identity}.");
            return unit;
        }

        private static bool TryResolveLivingFireman(NativeIdentity identity, out GameUnit* unit) =>
            GameUnitManagerAPI.Instance.TryGetUnitById(identity.Slot, out unit) && unit != null &&
            unit->r_GlobalId == identity.GlobalId && unit->r_AliveState == AliveState.IsAlive &&
            unit->r_UnitChimp == eChimps.CHIMP_TYPE_FIREMAN;

        private static GameBuilding* ResolveBurningBuilding(int buildingId)
        {
            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                building == null || building->r_AliveState != AliveState.IsAlive ||
                building->r_GlobalId == 0 || building->r_OnFireTicks == 0)
                throw new InvalidOperationException($"Vanilla returned a building that is no longer burning: {buildingId}.");
            return building;
        }

        private static bool TryResolveBurningBuilding(NativeIdentity identity, out GameBuilding* building) =>
            GameBuildingManagerAPI.Instance.TryGetBuildingById(identity.Slot, out building) &&
            building != null && building->r_GlobalId == identity.GlobalId &&
            building->r_AliveState == AliveState.IsAlive && building->r_OnFireTicks != 0;

        private static NativeIdentity ReadTargetIdentity(GameUnit* unit) => new NativeIdentity(
            *(ushort*)((byte*)unit + WaterboyNativeDefinition.FiremanTargetSlotOffset),
            *(uint*)((byte*)unit + WaterboyNativeDefinition.FiremanTargetGlobalIdOffset));

        private static uint ReadCompoundKey(GameBuilding* building) =>
            *(uint*)((byte*)building + WaterboyNativeDefinition.ManagedBuildingCompoundKeyOffset);

        private static int Distance(GameUnit* unit, GameBuilding* building) =>
            WaterboyTargetPolicy.ManhattanDistance(unit->r_CurrentTilePositionX,
                unit->r_CurrentTilePositionY, building->r_TilePositionXBegin,
                building->r_TilePositionYBegin);

        private static int CurrentGameTick() =>
            GameTimeManagerAPI.Instance.GetFrameProvider().CurrentGameTick;

        private static bool IsValidPlayerId(int playerId) => playerId >= 1 && playerId <= 8;

        private static void ValidateCommittedDetour(NativeDetour<FindNearestBurningBuildingDelegate> detour,
            ulong expectedTargetAddress)
        {
            if (detour == null || !detour.IsInstalled || detour.TargetAddress != expectedTargetAddress ||
                detour.Scheme != DetourScheme.Indirect ||
                detour.DisplacedByteCount != WaterboyNativeDefinition.FindNearestBurningBuildingDisplacedLength ||
                detour.TrampolineAddress == IntPtr.Zero || detour.TrampolineSize <= 0 ||
                detour.HookEntryPointAddress == IntPtr.Zero || detour.OriginalEntryPointAddress == IntPtr.Zero ||
                detour.PointerSlot == IntPtr.Zero || detour.ChainDepth != 1)
                throw new InvalidOperationException("The committed waterboy NativeDetour does not match the audited contract.");
            IntPtr target = new IntPtr(unchecked((long)expectedTargetAddress));
            int displacement = Marshal.ReadInt32(IntPtr.Add(target, 2));
            if (Marshal.ReadByte(target, 0) != 0xFF || Marshal.ReadByte(target, 1) != 0x25 ||
                Marshal.ReadByte(target, 6) != 0x90 || Marshal.ReadByte(target, 7) != 0x90 ||
                Marshal.ReadByte(target, 8) != 0x90 || Marshal.ReadByte(target, 9) != 0x90 ||
                IntPtr.Add(target, 6 + displacement) != detour.PointerSlot ||
                Marshal.ReadInt64(detour.PointerSlot) != detour.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException("The committed waterboy entry patch differs from the audited indirect form.");
        }

        private static void ValidateRuntimeDependencies()
        {
            string extender = typeof(GameTimeManagerAPI).Assembly.GetName().Version.ToString();
            string redBird = typeof(NativeDetour<>).Assembly.GetName().Version.ToString();
            if (extender != WaterboyNativeDefinition.AuditedScriptExtenderAssemblyVersion ||
                redBird != WaterboyNativeDefinition.AuditedRedBirdAssemblyVersion)
                throw new InvalidOperationException($"Unaudited dependencies: scriptExtender={extender}, redBird={redBird}.");
        }

        private static void ValidateManagedLayout()
        {
            if (Marshal.SizeOf(typeof(GameBuilding)) != 0x32C ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_AliveState)).ToInt32() != 0xD0 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_GlobalId)).ToInt32() != 0xD8 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_OnFireTicks)).ToInt32() != WaterboyNativeDefinition.ManagedBuildingFireTicksOffset ||
                Marshal.SizeOf(typeof(GameUnit)) != 0x490 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_ControllableForPlayerId)).ToInt32() != 0x92 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_GlobalId)).ToInt32() != 0x94 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_AIState)).ToInt32() != 0x2BC)
                throw new InvalidOperationException("The installed managed layouts differ from the audited waterboy contract.");
        }

        private void DisableCorrection(string reason, Exception exception)
        {
            if (!correctionAvailable)
                return;
            correctionAvailable = false;
            ledger.Clear();
            buttonViewModel.Update(false, false, true, false);
            Shared.DebugLogHelper.LogError(log,
                $"Waterboy target reservations disabled; Vanilla remains active. reason={reason}; exception={exception}");
        }

        private void LogDetail(string message)
        {
            if (detailedLogCount < MaximumDetailedLogs)
            {
                detailedLogCount++;
                Shared.DebugLogHelper.LogDebug(log, message);
                return;
            }
            if (detailLimitLogged)
                return;
            detailLimitLogged = true;
            Shared.DebugLogHelper.LogDebug(log,
                "Waterboy reservation detailed-log limit reached; repeated details are suppressed.");
        }

        private readonly struct MaskedFire
        {
            internal MaskedFire(NativeIdentity identity, ushort originalFireTicks)
            { Identity = identity; OriginalFireTicks = originalFireTicks; }
            internal NativeIdentity Identity { get; }
            internal ushort OriginalFireTicks { get; }
        }

        private readonly struct SeedCandidate
        {
            internal SeedCandidate(int unitId, uint globalId, int state, NativeIdentity target,
                uint compoundKey, ushort x, ushort y, int distance)
            {
                UnitId = unitId; Owner = new NativeIdentity(unitId, globalId); State = state;
                Target = target; CompoundKey = compoundKey; X = x; Y = y; Distance = distance;
            }
            internal int UnitId { get; }
            internal NativeIdentity Owner { get; }
            internal int State { get; }
            internal NativeIdentity Target { get; }
            internal uint CompoundKey { get; }
            internal ushort X { get; }
            internal ushort Y { get; }
            internal int Distance { get; }
            internal static int Compare(SeedCandidate left, SeedCandidate right)
            {
                int state = (left.State == ReservationLedger.ExtinguishingState ? 0 : 1)
                    .CompareTo(right.State == ReservationLedger.ExtinguishingState ? 0 : 1);
                if (state != 0) return state;
                int distance = left.Distance.CompareTo(right.Distance);
                return distance != 0 ? distance : left.UnitId.CompareTo(right.UnitId);
            }
        }
    }
}
