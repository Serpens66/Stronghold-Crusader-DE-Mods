using BepInEx.Logging;
using CrusaderDE;
using MessagePack;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.SaveData;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace RandomEvents
{
    internal sealed class RandomEventsRuntime : IDisposable
    {
        private const string SaveDataIdentifier = "serp-randomevents-state-v2";
        private const int VanillaMonthsPerYear = 12;
        private const int RabbitSpawnRadius = 12;
        private const int LionSpawnRadius = 12;

        private readonly ManualLogSource log;
        private readonly RandomEventsSettingsViewModel settings;
        private readonly ScenarioSignpostRegistry signpostRegistry;
        private readonly SignpostPlacementService signpostPlacement;
        private readonly NativeVanillaEventDispatcher nativeEventDispatcher;
        private readonly NativeWildlifeEventDispatcher nativeWildlifeDispatcher;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private bool initialized;
        private bool disposed;
        private bool mapStartPending;
        private bool mapActive;
        private bool loadedStateAvailable;
        private bool multiplayerDisableLogged;
        private bool mapStartedFromMultiplayerSave;
        private int lastSignpostAttemptTick = int.MinValue;
        private bool calendarApiChecked;
        private bool waitingForLivingHumanTargetLogged;
        private RandomEventsSaveStateV2 state;

        public RandomEventsRuntime(ManualLogSource log, RandomEventsSettingsViewModel settings)
        {
            this.log = log;
            this.settings = settings;
            signpostRegistry = new ScenarioSignpostRegistry(log);
            signpostPlacement = new SignpostPlacementService(log, signpostRegistry);
            nativeEventDispatcher = new NativeVanillaEventDispatcher(log);
            nativeWildlifeDispatcher = new NativeWildlifeEventDispatcher(log, nativeEventDispatcher);
        }

        public void InitializeNative(IntPtr libraryHandle, ReadOnlySpan<byte> memory, bool referenceHashMatches)
        {
            signpostRegistry.InitializeNative(libraryHandle, memory, referenceHashMatches);
            nativeEventDispatcher.InitializeNative(libraryHandle, memory, referenceHashMatches);
            nativeWildlifeDispatcher.InitializeNative(libraryHandle, memory, referenceHashMatches);
        }

        public void Initialize()
        {
            if (initialized)
                return;

            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnStartMap));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnUnloadMap));
            GameTimeManagerAPI.Instance.OnTick += OnGameTick;

            if (!ModSaveDataAPI.Instance.RegisterModDataHandler(
                    SaveDataIdentifier,
                    SaveState,
                    LoadState,
                    ResetMapState))
            {
                throw new InvalidOperationException("Random Events save-data handler registration failed.");
            }

            initialized = true;
            LogInfo("Runtime hooks and versioned save-data handler registered.");
        }

        public void Dispose()
        {
            if (disposed) return;
            GameTimeManagerAPI.Instance.OnTick -= OnGameTick;
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            signpostPlacement.Dispose();
            ModSaveDataAPI.Instance.UnregisterModDataHandler(SaveDataIdentifier);
            disposed = true;
        }

        private void OnStartMap(MapStartEventArgs args)
        {
            mapStartPending = true;
            mapActive = false;
            multiplayerDisableLogged = false;
            mapStartedFromMultiplayerSave = args.bMultiplayerSave != 0;
            lastSignpostAttemptTick = int.MinValue;
            calendarApiChecked = false;
            waitingForLivingHumanTargetLogged = false;
            LogInfo(
                $"Map start received; initialization deferred to persistent game tick: " +
                $"loadedState={loadedStateAvailable}, multiplayerSave={mapStartedFromMultiplayerSave}.");
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            LogInfo("Map unload received; in-memory Random Events state cleared.");
            ResetMapState();
        }

        private void ResetMapState()
        {
            mapStartPending = false;
            mapActive = false;
            loadedStateAvailable = false;
            multiplayerDisableLogged = false;
            mapStartedFromMultiplayerSave = false;
            calendarApiChecked = false;
            waitingForLivingHumanTargetLogged = false;
            signpostPlacement.ResetMapState();
            state = null;
        }

        private void OnGameTick(int tick)
        {
            try
            {
                if (mapStartPending)
                {
                    if (GameTimeManagerAPI.Instance.GetElapsedMapTicks() <= 0)
                        return;
                    InitializeCurrentMap();
                }

                if (!mapActive || state == null)
                    return;

                if (Shared.GameModeHelper.IsRealMultiplayer(mapStartedFromMultiplayerSave))
                {
                    Shared.GameModeSnapshot networkMode = Shared.GameModeHelper.Capture(mapStartedFromMultiplayerSave);
                    DisableForNetwork("real network game became active during the match", networkMode.ToDiagnosticString());
                    return;
                }

                int currentAbsoluteMonth = GetCurrentAbsoluteMonth();
                if (state.BatchPrepared && currentAbsoluteMonth >= state.NextDueAbsoluteMonth)
                {
                    ExecuteDueBatch(currentAbsoluteMonth);
                    return;
                }

                if (!state.BatchPrepared)
                    PrepareBatch();

                if (!state.SignpostsInitialized && RandomEventDefinitions.RequiresSignposts(state.Chances) &&
                    (lastSignpostAttemptTick == int.MinValue || tick - lastSignpostAttemptTick >= 30))
                {
                    lastSignpostAttemptTick = tick;
                    signpostPlacement.TryInitialize(state);
                }
            }
            catch (Exception ex)
            {
                LogError($"Persistent game-tick processing failed: {ex}");
            }
        }

        private void InitializeCurrentMap()
        {
            mapStartPending = false;

            Shared.GameModeSnapshot gameMode = Shared.GameModeHelper.Capture(mapStartedFromMultiplayerSave);
            string gameModeDetails = gameMode.ToDiagnosticString();
            LogInfo($"Game-mode diagnostics: {gameModeDetails}.");
            if (gameMode.IsRealMultiplayer)
            {
                DisableForNetwork("map started as a real network game", gameModeDetails);
                return;
            }

            if (gameMode.IsMapEditor)
            {
                LogInfo("Random Events disabled for map editor session.");
                state = null;
                return;
            }

            if (!gameMode.IsSingleplayerSkirmishMode)
            {
                LogInfo("Random Events disabled because the map is neither a singleplayer skirmish nor a singleplayer Trail mission.");
                state = null;
                return;
            }

            if (loadedStateAvailable && ValidateLoadedState(state))
            {
                mapActive = state.EffectiveEnabled;
                LogInfo(
                    $"Restored Random Events state: enabled={state.EffectiveEnabled}, interval={state.IntervalMonths}, " +
                    $"nextDueAbsoluteMonth={state.NextDueAbsoluteMonth}, batchPrepared={state.BatchPrepared}, " +
                    $"signpostsInitialized={state.SignpostsInitialized}.");
                return;
            }

            loadedStateAvailable = false;
            if (!settings.EnableMod)
            {
                LogInfo("Random Events disabled by the effective map setting.");
                state = null;
                return;
            }

            state = CreateFreshState();
            mapActive = true;
            PrepareBatch();
            LogInfo(
                $"Fresh Random Events state initialized: interval={state.IntervalMonths}, " +
                $"firstDueAbsoluteMonth={state.NextDueAbsoluteMonth}, multiplayerMode={(MultiplayerEventMode)state.MultiplayerMode} (reserved)." );
        }

        private RandomEventsSaveStateV2 CreateFreshState()
        {
            byte[] seed = new byte[16];
            using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
                generator.GetBytes(seed);

            int[] minimums = new int[6];
            int[] maximums = new int[6];
            for (int index = 0; index < 6; index++)
            {
                settings.GetStrengthRange((RandomEventStrengthKind)(index + 1), out minimums[index], out maximums[index]);
            }

            int interval = Math.Max(1, Math.Min(90, settings.IntervalMonths));
            int[] chances = settings.SnapshotChances();
            return new RandomEventsSaveStateV2
            {
                EffectiveEnabled = true,
                IntervalMonths = interval,
                MultiplayerMode = Math.Max(0, Math.Min(1, settings.MultiplayerEventModeIndex)),
                Chances = chances,
                StrengthMinimums = minimums,
                StrengthMaximums = maximums,
                PrngState0 = BitConverter.ToUInt64(seed, 0),
                PrngState1 = BitConverter.ToUInt64(seed, 8),
                NextDueAbsoluteMonth = checked(GetCurrentAbsoluteMonth() + interval),
                SignpostsInitialized = !RandomEventDefinitions.RequiresSignposts(chances)
            };
        }

        private bool PrepareBatch()
        {
            int[] humanTargetPlayerIds = GetLivingHumanPlayerIds();
            if (humanTargetPlayerIds.Length == 0)
            {
                if (!waitingForLivingHumanTargetLogged)
                {
                    waitingForLivingHumanTargetLogged = true;
                    LogInfo(
                        $"Event batch preparation deferred: dueAbsoluteMonth={state.NextDueAbsoluteMonth}, " +
                        "reason=no active human player has a living Lord yet. The configured first interval remains unchanged.");
                }
                return false;
            }

            waitingForLivingHumanTargetLogged = false;
            SavedPrng prng = new SavedPrng(state.PrngState0, state.PrngState1);
            List<int> directKinds = new List<int>();
            List<int> directStrengths = new List<int>();
            List<int> directTargetPlayerIds = new List<int>();

            foreach (RandomEventDefinition definition in RandomEventDefinitions.All)
            {
                int chance = state.Chances[(int)definition.Kind];
                int roll = prng.Next(100);
                bool success = roll < chance;
                int strength = success ? RollStrength(definition.StrengthKind, ref prng) : 0;
                LogDebug(
                    $"Event roll: event={definition.Name}, kind={definition.Kind}, roll={roll}, chance={chance}, " +
                    $"success={success}, strength={strength}, dueAbsoluteMonth={state.NextDueAbsoluteMonth}.");

                if (!success)
                    continue;
                directKinds.Add((int)definition.Kind);
                directStrengths.Add(strength);
                // Store an explicit non-AI target so the saved schedule remains deterministic
                // when synchronized multiplayer support is enabled later.
                directTargetPlayerIds.Add(humanTargetPlayerIds[prng.Next(humanTargetPlayerIds.Length)]);
            }

            state.PrngState0 = prng.State0;
            state.PrngState1 = prng.State1;
            state.PreparedDirectKinds = directKinds.ToArray();
            state.PreparedDirectStrengths = directStrengths.ToArray();
            state.PreparedDirectTargetPlayerIds = directTargetPlayerIds.ToArray();
            state.BatchPrepared = true;
            LogInfo(
                $"Event batch prepared: dueAbsoluteMonth={state.NextDueAbsoluteMonth}, " +
                $"preparedHits={directKinds.Count}, livingHumanTargets=[{string.Join(",", humanTargetPlayerIds)}].");
            return true;
        }

        private int RollStrength(RandomEventStrengthKind kind, ref SavedPrng prng)
        {
            if (kind == RandomEventStrengthKind.None)
                return 0;
            int index = (int)kind - 1;
            return prng.NextInclusive(state.StrengthMinimums[index], state.StrengthMaximums[index]);
        }

        private void ExecuteDueBatch(int currentAbsoluteMonth)
        {
            int due = state.NextDueAbsoluteMonth;
            int[] directKinds = state.PreparedDirectKinds ?? Array.Empty<int>();
            int[] strengths = state.PreparedDirectStrengths ?? Array.Empty<int>();
            int[] targetPlayerIds = state.PreparedDirectTargetPlayerIds ?? Array.Empty<int>();

            // Clear first so a save callback during a Vanilla action cannot persist an executable duplicate.
            state.BatchPrepared = false;
            state.PreparedDirectKinds = Array.Empty<int>();
            state.PreparedDirectStrengths = Array.Empty<int>();
            state.PreparedDirectTargetPlayerIds = Array.Empty<int>();

            LogInfo($"Event date reached: dueAbsoluteMonth={due}, currentAbsoluteMonth={currentAbsoluteMonth}, directCount={directKinds.Length}.");
            for (int index = 0; index < directKinds.Length; index++)
            {
                RandomEventDefinition definition = RandomEventDefinitions.Get((RandomEventKind)directKinds[index]);
                int strength = index < strengths.Length ? strengths[index] : 0;
                int targetPlayerId = index < targetPlayerIds.Length
                    ? targetPlayerIds[index]
                    : -1;
                DispatchDirectEvent(definition, strength, targetPlayerId);
            }

            state.NextDueAbsoluteMonth = checked(due + state.IntervalMonths);
            LogInfo($"Next event batch will be prepared on the following game tick: nextDueAbsoluteMonth={state.NextDueAbsoluteMonth}.");
        }

        private byte[] SaveState(SaveContext context)
        {
            if (!context.IsSaveFile || !mapActive || state == null ||
                Shared.GameModeHelper.IsRealMultiplayer(mapStartedFromMultiplayerSave))
                return null;
            byte[] bytes = MessagePackSerializer.Serialize(state);
            LogInfo($"Saved Random Events state: bytes={bytes.Length}, nextDueAbsoluteMonth={state.NextDueAbsoluteMonth}, batchPrepared={state.BatchPrepared}.");
            return bytes;
        }

        private void LoadState(byte[] bytes, LoadContext context)
        {
            if (!context.IsSaveFile)
                return;
            try
            {
                state = MessagePackSerializer.Deserialize<RandomEventsSaveStateV2>(bytes);
                loadedStateAvailable = state != null;
                mapStartPending = true;
                LogInfo($"Loaded Random Events state bytes: bytes={bytes.Length}, version={state?.Version}.");
            }
            catch (Exception ex)
            {
                state = null;
                loadedStateAvailable = false;
                LogError($"Random Events state could not be deserialized and will be initialized fresh: {ex}");
            }
        }

        private bool ValidateLoadedState(RandomEventsSaveStateV2 loaded)
        {
            bool valid = loaded != null && loaded.Version == RandomEventsSaveStateV2.CurrentVersion &&
                loaded.IntervalMonths >= 1 && loaded.IntervalMonths <= 90 &&
                loaded.Chances?.Length == RandomEventDefinitions.All.Length &&
                loaded.StrengthMinimums?.Length == 6 && loaded.StrengthMaximums?.Length == 6 &&
                loaded.PreparedDirectKinds != null && loaded.PreparedDirectStrengths != null &&
                loaded.PreparedDirectKinds.Length == loaded.PreparedDirectStrengths.Length &&
                loaded.PreparedDirectTargetPlayerIds != null &&
                loaded.PreparedDirectKinds.Length == loaded.PreparedDirectTargetPlayerIds.Length &&
                (loaded.PrngState0 | loaded.PrngState1) != 0;
            if (valid)
            {
                int currentAbsoluteMonth = GetCurrentAbsoluteMonth();
                // Reject states written with an incompatible calendar basis instead of waiting centuries.
                valid = loaded.NextDueAbsoluteMonth >= currentAbsoluteMonth &&
                    loaded.NextDueAbsoluteMonth <= checked(currentAbsoluteMonth + loaded.IntervalMonths);
                if (!valid)
                {
                    LogWarning(
                        $"Loaded Random Events state uses an implausible event date and will be initialized fresh: " +
                        $"currentAbsoluteMonth={currentAbsoluteMonth}, loadedNextDueAbsoluteMonth={loaded.NextDueAbsoluteMonth}, " +
                        $"interval={loaded.IntervalMonths}, effectiveMonthsPerYear={VanillaMonthsPerYear}.");
                }
            }
            if (!valid)
                LogWarning("Loaded Random Events V2 state failed validation and will not be used.");
            return valid;
        }

        private void DispatchDirectEvent(
            RandomEventDefinition definition,
            int strength,
            int targetPlayerId)
        {
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(targetPlayerId))
            {
                LogError(
                    $"Vanilla direct event skipped: event={definition.Name}, targetPlayerId={targetPlayerId}, " +
                    "reason=invalid target player.");
                return;
            }

            if (GamePlayerManagerAPI.Instance.IsAIPlayer(targetPlayerId))
            {
                LogInfo(
                    $"Random event skipped: event={definition.Name}, targetPlayerId={targetPlayerId}, " +
                    "reason=target player is controlled by AI.");
                return;
            }

            if (!TryGetLivingLord(targetPlayerId, out string lordFailure))
            {
                LogInfo(
                    $"Random event skipped: event={definition.Name}, targetPlayerId={targetPlayerId}, " +
                    $"reason=target player has no living Lord ({lordFailure}).");
                return;
            }

            if (definition.DispatchKind == RandomEventDispatchKind.NativeWildlife)
            {
                if (definition.Kind == RandomEventKind.Rabbits)
                    SpawnRabbitInfestation(targetPlayerId);
                else if (definition.Kind == RandomEventKind.LionAttack)
                    SpawnLionAttack(targetPlayerId, strength);
                else
                    LogError($"Native wildlife event skipped: unsupported event kind {definition.Kind}.");
                return;
            }

            if (definition.DispatchKind == RandomEventDispatchKind.NativeVanilla)
            {
                try
                {
                    NativeEventDispatchStatus status = nativeEventDispatcher.Dispatch(
                        definition.Kind,
                        strength,
                        targetPlayerId,
                        out string detail);
                    if (status == NativeEventDispatchStatus.Applied)
                    {
                        LogInfo(
                            $"Native Vanilla event dispatched: event={definition.Name}, actionId={definition.VanillaActionId}, " +
                            $"strength={strength}, targetPlayerId={targetPlayerId}, detail={detail}");
                    }
                    else if (status == NativeEventDispatchStatus.PrerequisiteNotMet)
                    {
                        LogInfo(
                            $"Native Vanilla event had no effect: event={definition.Name}, actionId={definition.VanillaActionId}, " +
                            $"targetPlayerId={targetPlayerId}, detail={detail}");
                    }
                    else
                    {
                        LogError(
                            $"Native Vanilla event skipped: event={definition.Name}, actionId={definition.VanillaActionId}, " +
                            $"targetPlayerId={targetPlayerId}, reason={detail}");
                    }
                }
                catch (Exception ex)
                {
                    LogError(
                        $"Native Vanilla event failed: event={definition.Name}, actionId={definition.VanillaActionId}, " +
                        $"targetPlayerId={targetPlayerId}, error={ex}");
                }
                return;
            }

            IDisposable signpostScope = null;
            int signpostBuildingId = -1;
            double signpostDistance = 0;
            if (definition.RequiresSignpost &&
                !signpostRegistry.TryBeginTargetedEvent(
                    targetPlayerId,
                    out signpostScope,
                    out signpostBuildingId,
                    out signpostDistance,
                    out string signpostFailure))
            {
                LogError(
                    $"Vanilla direct event skipped: event={definition.Name}, targetPlayerId={targetPlayerId}, " +
                    $"reason=required signpost unavailable or could not be prioritized ({signpostFailure}).");
                return;
            }

            int originalLocalPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            IntPtr localPlayerAddress = new IntPtr(unchecked((long)GameGlobalsManager.Instance.LocalPlayerIdVA));
            bool playerChanged = targetPlayerId != originalLocalPlayerId;
            try
            {
                if (playerChanged)
                {
                    if (localPlayerAddress == IntPtr.Zero)
                    {
                        LogError(
                            $"Vanilla direct event skipped: event={definition.Name}, targetPlayerId={targetPlayerId}, " +
                            "reason=native local-player address unavailable for explicit targeting.");
                        return;
                    }

                    Marshal.WriteInt32(localPlayerAddress, targetPlayerId);
                    if (Marshal.ReadInt32(localPlayerAddress) != targetPlayerId)
                    {
                        LogError(
                            $"Vanilla direct event skipped: event={definition.Name}, targetPlayerId={targetPlayerId}, " +
                            "reason=native target-player switch verification failed.");
                        return;
                    }
                }

                EngineInterface.GameAction(Enums.GameActionCommand.FreeBuild_Event, definition.TextId, strength);
                LogInfo(
                    $"Vanilla direct event dispatched: event={definition.Name}, textId={definition.TextId}, " +
                    $"strength={strength}, targetPlayerId={targetPlayerId}, signpostBuildingId={signpostBuildingId}, " +
                    $"signpostDistanceToTargetKeep={signpostDistance:0.00}. " +
                    "Vanilla assigns its own event-specific movement, attack, or aggression behavior; " +
                    "the managed wrapper cannot distinguish a visible effect " +
                    "from a prerequisite-driven native no-op.");
            }
            finally
            {
                if (playerChanged && localPlayerAddress != IntPtr.Zero)
                    Marshal.WriteInt32(localPlayerAddress, originalLocalPlayerId);
                signpostScope?.Dispose();
            }
        }

        private int[] GetLivingHumanPlayerIds()
        {
            List<int> result = new List<int>();
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            foreach (int playerId in Shared.ActivePlayerHelper.GetActivePlayerIds())
            {
                if (!playerApi.IsPlayerIdValid(playerId) ||
                    playerApi.IsAIPlayer(playerId) ||
                    !TryGetLivingLord(playerId, out _))
                {
                    continue;
                }
                result.Add(playerId);
            }
            return result.ToArray();
        }

        private void SpawnRabbitInfestation(int targetPlayerId)
        {
            if (!nativeWildlifeDispatcher.TryGetRabbitTileMask(out uint rabbitTileMask, out string compatibilityFailure))
            {
                LogError(
                    $"Native rabbit event skipped: targetPlayerId={targetPlayerId}, " +
                    $"reason={compatibilityFailure}");
                return;
            }

            List<RabbitFarm> farms = new List<RabbitFarm>();
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int buildingId = 0; buildingId < buildings.Length; buildingId++)
            {
                ref GameBuilding building = ref buildings[buildingId];
                if (building.r_PlayerIdOwner != targetPlayerId ||
                    building.r_AliveState != AliveState.IsAlive ||
                    (building.r_BuildingType != eStructs.STRUCT_WHEATFARM &&
                     building.r_BuildingType != eStructs.STRUCT_HOPSFARM))
                {
                    continue;
                }

                farms.Add(new RabbitFarm(
                    buildingId,
                    building.r_BuildingType,
                    (building.r_TilePositionXBegin + building.r_TilePositionXEnd) / 2,
                    (building.r_TilePositionYBegin + building.r_TilePositionYEnd) / 2));
            }

            if (farms.Count == 0)
            {
                LogInfo(
                    $"Rabbit infestation skipped: targetPlayerId={targetPlayerId}, " +
                    "reason=no alive wheat or hops farm owned by the target player.");
                return;
            }

            SavedPrng prng = new SavedPrng(state.PrngState0, state.PrngState1);
            RabbitFarm farm = farms[prng.Next(farms.Count)];
            List<RabbitSpawnTile> spawnTiles = FindWildlifeSpawnTiles(
                farm.TileX,
                farm.TileY,
                RabbitSpawnRadius,
                rabbitTileMask);
            if (spawnTiles.Count == 0)
            {
                state.PrngState0 = prng.State0;
                state.PrngState1 = prng.State1;
                LogWarning(
                    $"Rabbit infestation skipped: targetPlayerId={targetPlayerId}, farmBuildingId={farm.BuildingId}, " +
                    $"farmType={farm.BuildingType}, farmTile=({farm.TileX},{farm.TileY}), " +
                    $"reason=no Vanilla-compatible tile exists within radius {RabbitSpawnRadius}.");
                return;
            }

            RabbitSpawnTile spawnTile = spawnTiles[prng.Next(spawnTiles.Count)];
            state.PrngState0 = prng.State0;
            state.PrngState1 = prng.State1;
            NativeEventDispatchStatus status = nativeWildlifeDispatcher.DispatchRabbits(
                spawnTile.X, spawnTile.Y, spawnTile.Height, out string detail);
            LogWildlifeDispatchResult(
                "Rabbit infestation",
                status,
                targetPlayerId,
                $"farmBuildingId={farm.BuildingId}, farmType={farm.BuildingType}, " +
                $"farmTile=({farm.TileX},{farm.TileY}), radius={RabbitSpawnRadius}, " +
                $"spawnTile=({spawnTile.X},{spawnTile.Y}), candidateTiles={spawnTiles.Count}, detail={detail}");
        }

        private void SpawnLionAttack(int targetPlayerId, int strength)
        {
            if (!nativeWildlifeDispatcher.TryGetLionTileMask(out uint lionTileMask, out string compatibilityFailure))
            {
                LogError(
                    $"Native lion event skipped: targetPlayerId={targetPlayerId}, " +
                    $"reason={compatibilityFailure}");
                return;
            }

            if (!signpostRegistry.TryGetClosestSignpostToPlayer(
                    targetPlayerId,
                    out int signpostBuildingId,
                    out int signpostX,
                    out int signpostY,
                    out double signpostDistance,
                    out string distanceReference,
                    out string signpostFailure))
            {
                LogError(
                    $"Native lion event skipped: targetPlayerId={targetPlayerId}, " +
                    $"reason=closest registered signpost unavailable ({signpostFailure}).");
                return;
            }

            List<RabbitSpawnTile> spawnTiles = FindWildlifeSpawnTiles(
                signpostX,
                signpostY,
                LionSpawnRadius,
                lionTileMask);
            if (spawnTiles.Count == 0)
            {
                LogError(
                    $"Native lion event skipped: targetPlayerId={targetPlayerId}, signpostBuildingId={signpostBuildingId}, " +
                    $"signpostTile=({signpostX},{signpostY}), reason=no Vanilla-compatible tile exists within radius {LionSpawnRadius}.");
                return;
            }

            // The first candidate is the nearest valid tile to the selected signpost.
            RabbitSpawnTile spawnTile = spawnTiles[0];
            NativeEventDispatchStatus status = nativeWildlifeDispatcher.DispatchLions(
                spawnTile.X, spawnTile.Y, spawnTile.Height, strength, out string detail);
            LogWildlifeDispatchResult(
                "Lion attack",
                status,
                targetPlayerId,
                $"strength={strength}, signpostBuildingId={signpostBuildingId}, " +
                $"signpostTile=({signpostX},{signpostY}), distanceReference={distanceReference}, " +
                $"signpostDistance={signpostDistance:0.00}, " +
                $"spawnTile=({spawnTile.X},{spawnTile.Y}), spawnRadius={LionSpawnRadius}, detail={detail}");
        }

        private static List<RabbitSpawnTile> FindWildlifeSpawnTiles(
            int centerX,
            int centerY,
            int radius,
            uint rejectedTileMask)
        {
            List<RabbitSpawnTile> result = new List<RabbitSpawnTile>();
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            if (rejectedTileMask == 0)
                return result;

            int radiusSquared = radius * radius;
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    int deltaX = x - centerX;
                    int deltaY = y - centerY;
                    if (deltaX * deltaX + deltaY * deltaY > radiusSquared ||
                        !tiles.IsTileInsideMapBounds(x, y))
                    {
                        continue;
                    }

                    int tileId = tiles.GetTileId(x, y);
                    uint flags = unchecked((uint)tiles.GetTilePropertyFlag(tileId));
                    if ((flags & rejectedTileMask) == 0)
                        result.Add(new RabbitSpawnTile(x, y, tiles.GetTileHeight(tileId), deltaX * deltaX + deltaY * deltaY));
                }
            }
            result.Sort((left, right) => left.DistanceSquared.CompareTo(right.DistanceSquared));
            return result;
        }

        private void LogWildlifeDispatchResult(
            string eventName,
            NativeEventDispatchStatus status,
            int targetPlayerId,
            string details)
        {
            if (status == NativeEventDispatchStatus.Applied)
            {
                LogInfo($"Native Vanilla wildlife event dispatched: event={eventName}, targetPlayerId={targetPlayerId}, {details}.");
                return;
            }
            if (status == NativeEventDispatchStatus.PrerequisiteNotMet)
            {
                LogInfo($"Native Vanilla wildlife event had no effect: event={eventName}, targetPlayerId={targetPlayerId}, {details}.");
                return;
            }
            LogError($"Native Vanilla wildlife event disabled or failed: event={eventName}, targetPlayerId={targetPlayerId}, {details}.");
        }

        private static unsafe bool TryGetLivingLord(int playerId, out string failure)
        {
            failure = string.Empty;
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(
                    playerId,
                    out GamePlayerResources* resources) ||
                resources == null)
            {
                failure = "player resources unavailable";
                return false;
            }

            uint lordUnitId = resources->r_LordUnitId;
            if (lordUnitId == 0 || lordUnitId > int.MaxValue)
            {
                failure = "no valid Lord unit is registered";
                return false;
            }
            if (!GameUnitManagerAPI.Instance.TryGetUnitById((int)lordUnitId, out GameUnit* lord) || lord == null)
            {
                failure = "registered Lord unit cannot be resolved";
                return false;
            }
            if (lord->r_AliveState != AliveState.IsAlive)
            {
                failure = $"registered Lord unit state is {lord->r_AliveState}";
                return false;
            }
            if (lord->r_UnitChimp != eChimps.CHIMP_TYPE_LORD || lord->r_ControllableForPlayerId != playerId)
            {
                failure = "registered unit is not the target player's Lord";
                return false;
            }

            return true;
        }

        private int GetCurrentAbsoluteMonth()
        {
            int currentYear = GameTimeManagerAPI.Instance.GetCurrentYear();
            int currentMonth = GameTimeManagerAPI.Instance.GetCurrentMonth();
            ValidateCalendarApi(currentYear, currentMonth);
            return checked(currentYear * VanillaMonthsPerYear + currentMonth);
        }

        private void ValidateCalendarApi(int currentYear, int currentMonth)
        {
            if (currentYear < 0 || currentMonth < 0 || currentMonth >= VanillaMonthsPerYear)
            {
                mapActive = false;
                state = null;
                throw new InvalidOperationException(
                    $"Unsupported Vanilla calendar values year={currentYear}, month={currentMonth}; " +
                    "Random Events was disabled for this map to prevent incorrectly dated events.");
            }

            if (calendarApiChecked)
                return;

            calendarApiChecked = true;
            int apiMonthsPerYear = GameTimeManagerAPI.Instance.GetMonthsInYear();
            if (apiMonthsPerYear == VanillaMonthsPerYear)
                return;

            LogInfo(
                $"GameTimeManagerAPI.GetMonthsInYear() returned {apiMonthsPerYear}; " +
                $"Random Events uses the validated Vanilla calendar constant {VanillaMonthsPerYear} instead.");
        }

        private void DisableForNetwork(string reason, string details)
        {
            mapStartPending = false;
            mapActive = false;
            state = null;
            if (multiplayerDisableLogged) return;
            multiplayerDisableLogged = true;
            LogInfo(
                $"Random Events fully disabled: {reason}; no rolls, events, save data, or signposts will be created. " +
                $"Network details: {details}.");
        }

        private void LogDebug(string message) => Shared.DebugLogHelper.LogDebug(log, message);
        private void LogInfo(string message) => Shared.DebugLogHelper.LogInfo(log, message);
        private void LogWarning(string message) => Shared.DebugLogHelper.LogWarning(log, message);
        private void LogError(string message) => Shared.DebugLogHelper.LogError(log, message);

        private readonly struct RabbitFarm
        {
            public RabbitFarm(int buildingId, eStructs buildingType, int tileX, int tileY)
            {
                BuildingId = buildingId;
                BuildingType = buildingType;
                TileX = tileX;
                TileY = tileY;
            }

            public int BuildingId { get; }
            public eStructs BuildingType { get; }
            public int TileX { get; }
            public int TileY { get; }
        }

        private readonly struct RabbitSpawnTile
        {
            public RabbitSpawnTile(int x, int y, int height, int distanceSquared)
            {
                X = x;
                Y = y;
                Height = height;
                DistanceSquared = distanceSquared;
            }

            public int X { get; }
            public int Y { get; }
            public int Height { get; }
            public int DistanceSquared { get; }
        }
    }
}
