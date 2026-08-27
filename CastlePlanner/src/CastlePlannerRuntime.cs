using BepInEx.Logging;
using BepInEx.Bootstrap;
using AIVParser.Core;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Units;
using SHCDESE.Extensions;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace CastlePlanner
{
    internal sealed unsafe class CastlePlannerRuntime
    {
        private const int AivSpecStride = 0x6D98;
        private const int PlayerAivStateStride = 0x583C;
        private const int ImportedCandidatesPerPlayer = 1000;
        private const int SpecCopiedPlayerAivValueOffset = 0x08;
        private const int SpecOrientationOffset = 0x0C;
        private const int SpecCandidateIdOffset = 0x10;
        private const int SpecPlacementStateOffset = 0x14;
        private const int SpecHighestFrameOffset = 0x24;

        private const string AllocateSpecPattern =
            "48 89 74 24 10 57 48 83 EC 20 BF 01 00 00 00 " +
            "48 8D 81 9C 6D 00 00";
        private const string SetPlacementPattern =
            "40 53 48 83 EC 30 48 63 C2 45 8B D1 48 69 D8 98 6D 00 00";
        private const string SelectBestFitPattern =
            "44 88 44 24 18 89 54 24 10 55 56 41 54 41 55 41 56 41 57 " +
            "48 83 EC 58";
        private const string TestSpecificCandidatePattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 " +
            "48 89 7C 24 20 41 56 48 83 EC 20 41 8B F0 48 63 EA " +
            "48 8B F9 4C 8D 89 44 98 1B 00";
        private const string PrepareLayoutPattern =
            "44 89 44 24 18 53 55 56 57 41 54 41 55 41 56 41 57 " +
            "48 83 EC 68";
        private const string ExecuteToPercentagePattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 " +
            "48 83 EC 30 48 63 F2 48 8D 05 ?? ?? ?? ?? " +
            "4C 69 CE 3C 58 00 00";
        private const string AivStateReferencePattern =
            "41 89 4E 04 48 8D 0D ?? ?? ?? ??";
        private const string PrebuiltPlayersReferencePattern =
            "8B 0D ?? ?? ?? ?? 8D 46 FF 0F AB C1 41 B8 64 00 00 00 " +
            "89 0D ?? ?? ?? ?? 8B D6 49 8B CD E8 ?? ?? ?? ??";
        private const string PreparedKeepCoordinatesReferencePattern =
            "42 8B 44 2F 0C 48 8D 0D ?? ?? ?? ?? " +
            "44 8B 0D ?? ?? ?? ?? 8B D6 44 8B 05 ?? ?? ?? ?? " +
            "C6 44 24 38 00 89 44 24 30 B8 3D 00 00 00";
        private const string HumanKeepCoordinateLoadPattern =
            "48 63 BC CD 54 0D 00 00 44 8B A4 CD 50 0D 00 00 " +
            "44 8B CF 45 8B C4 66 89 44 24 20";
        private const int AllocateSpecRva = 0x50680;
        private const int SetPlacementRva = 0x54EC0;
        private const int SelectBestFitRva = 0x54F60;
        private const int TestSpecificCandidateRva = 0x54DE0;
        private const int PrepareLayoutRva = 0x53D00;
        private const int ExecuteToPercentageRva = 0x55F50;
        private const int AivStateReferenceRva = 0x95C9F;
        private const int PrebuiltPlayersReferenceRva = 0x95FF8;
        private const int PreparedKeepCoordinatesReferenceRva = 0x95EA3;
        private const int HumanKeepCoordinateLoadRva = 0x95B3C;
        private const int DeferredCompoundPlacementMaxAttempts = 3;
        private const int DeferredCompoundPlacementTimeoutTicks = 600;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int AllocateSpecDelegate(IntPtr aivState, int playerId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SetPlacementDelegate(
            IntPtr aivState,
            int specIndex,
            int keepX,
            int keepY,
            int orientation);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SelectBestFitDelegate(
            IntPtr aivState,
            int specIndex,
            byte tryOtherRotations);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate uint TestSpecificCandidateDelegate(
            IntPtr aivState,
            int specIndex,
            int candidateId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PrepareLayoutDelegate(
            IntPtr aivState,
            int specIndex,
            int playerId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ExecuteToPercentageDelegate(
            IntPtr aivState,
            int playerId,
            int percentage);

        private readonly ManualLogSource log;
        private readonly CastlePlannerSettingsViewModel settings;
        private readonly FreeCastlePreviewRuntime preview;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();

        private AllocateSpecDelegate allocateSpec;
        private SetPlacementDelegate setPlacement;
        private SelectBestFitDelegate selectBestFit;
        private TestSpecificCandidateDelegate testSpecificCandidate;
        private PrepareLayoutDelegate prepareLayout;
        private ExecuteToPercentageDelegate executeToPercentage;
        private IntPtr aivState;
        private IntPtr playerAivStateBase;
        private IntPtr prebuiltPlayersBitField;
        private IntPtr preparedKeepX;
        private IntPtr preparedKeepY;
        private HookTransaction nativeHookTransaction;
        private HookRef<X64InlineHook> humanKeepCoordinateLoadHook =
            new HookRef<X64InlineHook>();
        private bool installed;
        private bool referenceHashMatches;
        private bool handledCurrentMap;
        private readonly Dictionary<int, PendingAivImport> pendingAivImports =
            new Dictionary<int, PendingAivImport>();
        private readonly Dictionary<int, PreparedAivCastle> preparedAivCastles =
            new Dictionary<int, PreparedAivCastle>();
        private readonly Dictionary<int, PreparedAivCastle> executedAivCastles =
            new Dictionary<int, PreparedAivCastle>();
        private readonly SortedDictionary<int, DeferredCompoundBuildingQueue> deferredCompoundBuildings =
            new SortedDictionary<int, DeferredCompoundBuildingQueue>();
        private readonly HashSet<int> expectedAivCastlePlayers = new HashSet<int>();
        private readonly HashSet<int> failedAivCastlePlayers = new HashSet<int>();
        private readonly Dictionary<int, int> earlyUnitDiagnosticCounts =
            new Dictionary<int, int>();
        private string spawnPlanFailure = string.Empty;
        private bool nativeCastleExecutionInProgress;
        private int nativeCastleExecutionPlayerId;
        private int nextHovelVisualStyle;
        private int correctedHovelVisualCount;
        private bool captureSupplementalBuilding;
        private int captureSupplementalPlayerId;
        private int captureSupplementalX;
        private int captureSupplementalY;
        private eStructs captureSupplementalStruct;
        private int capturedSupplementalBuildingId;

        public CastlePlannerRuntime(
            ManualLogSource log,
            CastlePlannerSettingsViewModel settings,
            FreeCastlePreviewRuntime preview)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.preview = preview ?? throw new ArgumentNullException(nameof(preview));
        }

        public void Install(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            if (installed)
                return;

            this.referenceHashMatches = referenceHashMatches;
            BindNativeFunctions(libraryHandle, memory);
            InstallHumanStartPreparationHook(libraryHandle, memory);

            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                .Subscribe(OnStartMap));
            subscriptions.Add(BuildingR3EventHooks.OnBuildStructure.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnBuildStructurePre));
            subscriptions.Add(BuildingR3EventHooks.OnBuildStructure.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnBuildStructurePost));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnBuildingSpawnPost));
            subscriptions.Add(UnitR3EventHooks.OnUnitCreate.Observable
                .Subscribe(OnUnitCreateDiagnostic));
            subscriptions.Add(MapLoaderR3EventHooks.OnLoadSave.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnLoadSave));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnUnloadMap));
            GameTimeManagerAPI.Instance.OnTick += OnGameTick;

            installed = true;
            Shared.DebugLogHelper.LogInfo(
                log,
                "Native AIV castle spawner installed; all private functions and globals resolved uniquely.");
        }

        private void OnLoadSave(LoadSaveGameEventArgs args)
        {
            handledCurrentMap = true;
            ClearDeferredCompoundPlacements("savegame-load");
            ClearMapSpawnState();
            Shared.DebugLogHelper.LogInfo(
                log,
                "Savegame load detected; native castle spawning is disabled for this map.");
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            handledCurrentMap = false;
            ClearDeferredCompoundPlacements("map-unload");
            ClearMapSpawnState();
            nativeCastleExecutionInProgress = false;
            nativeCastleExecutionPlayerId = 0;
            nextHovelVisualStyle = 0;
            correctedHovelVisualCount = 0;
            Shared.DebugLogHelper.LogInfo(
                log,
                "OnUnloadMap(Post) received; the next new map may spawn a native AIV castle.");
        }

        private void ClearMapSpawnState()
        {
            pendingAivImports.Clear();
            preparedAivCastles.Clear();
            executedAivCastles.Clear();
            expectedAivCastlePlayers.Clear();
            failedAivCastlePlayers.Clear();
            earlyUnitDiagnosticCounts.Clear();
            spawnPlanFailure = string.Empty;
        }

        private void OnStartMap(MapStartEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre)
            {
                OnStartMapPre(args);
                return;
            }

            if (args.Phase != EventHookPhase.Post)
                return;

            Shared.DebugLogHelper.LogInfo(
                log,
                $"OnStartMap(Post) received: handledCurrentMap={handledCurrentMap}, " +
                $"preImports={pendingAivImports.Count}, " +
                $"keepPreSpawns={preparedAivCastles.Count}, " +
                $"keepPostSpawns={executedAivCastles.Count}.");

            GameModeSnapshot gameMode = CaptureGameMode(args);
            LogGameModeDiagnostics(gameMode);

            if (handledCurrentMap)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "OnStartMap(Post) ignored because this map was already handled or is a loaded savegame.");
                return;
            }

            handledCurrentMap = true;
            if (!preview.IsSpawnMapPass)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Native castle spawning skipped because this is not the committed restart pass.");
                return;
            }

            try
            {
                EnsureSupportedGameMode(gameMode);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Game-mode guard accepted supported skirmish: " +
                    $"sharedSingleplayerSkirmish={gameMode.SharedSingleplayerSkirmish}, " +
                    $"sharedSingleplayerTrail={gameMode.SharedSingleplayerTrail}, " +
                    $"gameDataSkirmishGameType={gameMode.GameDataSkirmishGameType}, " +
                    $"lobbySkirmishMembers={gameMode.LobbySkirmishMemberCount}, " +
                    $"lobbySkirmishHumans={gameMode.LobbySkirmishHumanCount}, " +
                    $"lobbyNetworkHumans={gameMode.LobbyNetworkHumanCount}, " +
                    $"realNetworkGameMembers={gameMode.RealNetworkGameMemberCount}.");

                int pendingCount = pendingAivImports.Count;
                int preparedCount = preparedAivCastles.Count;
                int executedCount = executedAivCastles.Count;
                int[] expectedPlayers = expectedAivCastlePlayers.OrderBy(id => id).ToArray();
                int[] executedPlayers = executedAivCastles.Keys.OrderBy(id => id).ToArray();
                int[] failedPlayers = failedAivCastlePlayers.OrderBy(id => id).ToArray();
                if (!string.IsNullOrEmpty(spawnPlanFailure) ||
                    pendingCount != 0 || preparedCount != 0 ||
                    !expectedPlayers.Except(failedPlayers).SequenceEqual(executedPlayers))
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"One or more selected castles could not be prepared; their Vanilla Keeps remain unchanged: " +
                        $"expected=[{string.Join(",", expectedPlayers)}], " +
                        $"executed=[{string.Join(",", executedPlayers)}], " +
                        $"failed=[{string.Join(",", failedPlayers)}], pending={pendingCount}, " +
                        $"prepared={preparedCount}, executedCount={executedCount}, " +
                        $"preImportFailure='{spawnPlanFailure}'.");
                }

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Native multiplayer castle execution completed inside map start: " +
                    $"executedPlayers=[{string.Join(",", executedPlayers)}].");
            }
            catch (Exception ex)
            {
                // There is deliberately no managed placement fallback.
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Native AIV castle spawn failed; no fallback was attempted: {ex}");
            }
            finally
            {
                ClearMapSpawnState();
            }
        }

        private void OnStartMapPre(MapStartEventArgs args)
        {
            ClearDeferredCompoundPlacements("new-map-start");
            ClearMapSpawnState();
            Shared.DebugLogHelper.LogInfo(
                log,
                $"OnStartMap(Pre) received: handledCurrentMap={handledCurrentMap}.");

            if (handledCurrentMap || !preview.TryGetCommittedSelections(out List<FreeCastleSelection> requests))
                return;

            try
            {
                GameModeSnapshot gameMode = CaptureGameMode(args);
                EnsureSupportedGameMode(gameMode);
                int[] humanPlayerIds = CaptureHumanPlayerIds();

                // Parse and encode every file before the first native mutation. A single
                // malformed AIV therefore aborts the whole transaction without partial imports.
                List<PendingAivImport> preparedImports = requests
                    .Select(request =>
                    {
                        AivSpawnOptions options = settings.GetSpawnOptions(request.PlayerId);
                        // Decorations can affect gameplay, so only the synchronized
                        // host setting controls them on every peer.
                        options.SpawnBraziersAndFlags = settings.SpawnBraziersAndFlags;
                        ApplyFixesGoodsyardPolicy(request.PlayerId, options);
                        return PreparePlayerImport(request, options);
                    })
                    .ToList();
                foreach (FreeCastleSelection request in requests)
                    expectedAivCastlePlayers.Add(request.PlayerId);
                // Validate every native candidate slot before the first import so
                // an unavailable table cannot leave a partially imported set.
                foreach (FreeCastleSelection request in requests)
                    CaptureImportedCandidates(request.PlayerId - 1);
                for (int index = 0; index < requests.Count; index++)
                    ImportPlayerCastle(requests[index], preparedImports[index]);

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Native AIV pre-import transaction completed: " +
                    $"humanPlayers=[{string.Join(",", humanPlayerIds)}], " +
                    $"selectedPlayers=[{string.Join(",", requests.Select(request => request.PlayerId))}].");
            }
            catch (Exception ex)
            {
                pendingAivImports.Clear();
                preparedAivCastles.Clear();
                executedAivCastles.Clear();
                foreach (int playerId in expectedAivCastlePlayers)
                    failedAivCastlePlayers.Add(playerId);
                spawnPlanFailure = ex.Message;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Native AIV pre-import failed; Keep-preparation and Post execution will be skipped: {ex}");
            }
        }

        private static PendingAivImport PreparePlayerImport(
            FreeCastleSelection request,
            AivSpawnOptions options)
        {
            FreeCastleProtocol.ValidateSelection(request);
            AivJsonDocument decoded = AivSpawnPlan.Decode(request.RawData);
            AivJsonDocument filtered = AivSpawnPlan.Filter(decoded, options);
            short[] filteredRaw = AivRawDataEncoder.Encode(filtered);
            return new PendingAivImport(
                request.PlayerId,
                request.DisplayName,
                request.ContentHash,
                request.Rotation,
                request.FlagProjectileType,
                filteredRaw,
                decoded,
                filtered,
                options);
        }

        private void ImportPlayerCastle(
            FreeCastleSelection request,
            PendingAivImport prepared)
        {
            // Pre runs after Vanilla's import loop but before map start consumes the table.
            EngineInterface.ImportAIV(request.PlayerId - 1, 0, prepared.RawAiv, 1);
            ImportedCandidateSnapshot importedCandidates =
                CaptureImportedCandidates(request.PlayerId - 1);
            pendingAivImports.Add(request.PlayerId, prepared);

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Native AIV pre-import completed: phase=OnStartMap(Pre), " +
                $"playerId={request.PlayerId}, playerSlot={request.PlayerId - 1}, " +
                $"castle='{request.DisplayName}', sha256={request.ContentHash}, " +
                $"candidateId=0, custom=1, " +
                $"rawShorts={prepared.RawShortCount}, " +
                $"nativeCandidateCountAfterImport={importedCandidates.Count}, " +
                $"nativeCandidate0Pointer=0x{importedCandidates.FirstPointer.ToInt64():X}, " +
                $"nativeCandidateTable=0x{importedCandidates.TableAddress.ToInt64():X}.");
        }

        private void OnBuildStructurePre(BuildStructureEventArgs args)
        {
            if (TryCorrectNativeHovelVisualStyle(args))
                return;

            if (!IsKeepMapper(args.Mappers))
                return;

            if (pendingAivImports.Remove(args.PlayerId))
            {
                failedAivCastlePlayers.Add(args.PlayerId);
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Early Vanilla human-start preparation was not reached for playerId={args.PlayerId}; " +
                    "the unmodified Vanilla Keep will continue and no castle fallback will run.");
                return;
            }

            if (!preparedAivCastles.TryGetValue(args.PlayerId, out PreparedAivCastle castle))
                return;

            bool matchesPreparedStart =
                args.TileX == castle.PreparedKeepX &&
                args.TileY == castle.PreparedKeepY &&
                args.Mappers == eMappers.MAPPER_KEEP2 &&
                args.BuildingScaleUnknown == 7 &&
                args.Unknown1 == castle.Orientation &&
                !args.IsFree;
            if (!matchesPreparedStart)
            {
                preparedAivCastles.Remove(args.PlayerId);
                failedAivCastlePlayers.Add(args.PlayerId);
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Vanilla human-start arguments diverged after early preparation; " +
                    $"playerId={args.PlayerId}, expectedKeep=({castle.PreparedKeepX},{castle.PreparedKeepY}), " +
                    $"actualKeep=({args.TileX},{args.TileY}), expectedOrientation={castle.Orientation}, " +
                    $"actualOrientation={args.Unknown1}, mapper={args.Mappers}, " +
                    $"scale={args.BuildingScaleUnknown}, isFree={args.IsFree}. " +
                    "Native castle execution is disabled for this player.");
                return;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Vanilla human Keep reached BuildStructure with its final start data already active: " +
                $"playerId={args.PlayerId}, requestedKeep=({castle.RequestedKeepX},{castle.RequestedKeepY}), " +
                $"preparedKeep=({args.TileX},{args.TileY}), orientation={args.Unknown1}, " +
                $"mapper={args.Mappers}, scale={args.BuildingScaleUnknown}, isFree={args.IsFree}.");
        }

        private bool TryCorrectNativeHovelVisualStyle(BuildStructureEventArgs args)
        {
            if (!nativeCastleExecutionInProgress ||
                args.PlayerId != nativeCastleExecutionPlayerId ||
                args.Mappers != eMappers.MAPPER_HOVEL)
            {
                return false;
            }

            int originalVisualStyle = args.Unknown1;
            int correctedVisualStyle = nextHovelVisualStyle;
            nextHovelVisualStyle = (nextHovelVisualStyle + 1) % 7;
            correctedHovelVisualCount++;

            // The native AI path ignores AIV's value 15 and cycles Hovel styles 0..6.
            // The human path consumes that value directly, so mirror the AI cycle here.
            args.Unknown1 = correctedVisualStyle;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Native Hovel visual style corrected: playerId={args.PlayerId}, " +
                $"ordinal={correctedHovelVisualCount}, " +
                $"originalStyle={originalVisualStyle}, correctedStyle={correctedVisualStyle}.");
            return true;
        }

        private void OnBuildStructurePost(BuildStructureEventArgs args)
        {
            if (!preparedAivCastles.TryGetValue(args.PlayerId, out PreparedAivCastle castle) ||
                !IsKeepMapper(args.Mappers))
            {
                return;
            }

            // Version 0.7.7 proved that execution can be delayed: retain PreparedAivCastle
            // after Keep(Post), arm it on the Lord's UnitCreate(Post), execute on the next
            // GameTime tick, and keep map-spawn state past OnStartMap(Post). Keep the native
            // start-phase execution here unless a future compatibility issue needs that fallback.
            preparedAivCastles.Remove(args.PlayerId);
            try
            {
                LogVanillaStartState(castle, "KeepPostBeforeAiv");
                ExecutePreparedCastle(castle);
                executedAivCastles[args.PlayerId] = castle;
                LogVanillaStartState(castle, "KeepPostAfterAiv");
            }
            catch (Exception ex)
            {
                executedAivCastles.Remove(args.PlayerId);
                failedAivCastlePlayers.Add(args.PlayerId);
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Native AIV execution at human Keep BuildStructure(Post) failed; " +
                    $"no castle fallback will run: {ex}");
            }
        }

        private void OnUnitCreateDiagnostic(UnitCreateEventArgs args)
        {
            int playerId = args.PlayerOwnerId;
            if (!expectedAivCastlePlayers.Contains(playerId))
                return;

            earlyUnitDiagnosticCounts.TryGetValue(playerId, out int count);
            if (args.Phase == EventHookPhase.Pre)
            {
                count++;
                earlyUnitDiagnosticCounts[playerId] = count;
            }
            if (count > 32)
                return;

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Selected-player early UnitCreate diagnostic: phase={args.Phase}, " +
                $"ordinal={count}, playerId={playerId}, colorId={args.PlayerColorId}, " +
                $"type={args.UnitType}, world=({args.WorldTileX},{args.WorldTileY}), " +
                $"height={args.HeightElevation}, returnValue={args.ReturnValue}.");
        }

        private void LogVanillaStartState(PreparedAivCastle castle, string phase)
        {
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(
                    castle.PlayerId,
                    out GamePlayerResources* resources))
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Vanilla start-state diagnostic unavailable: phase={phase}, " +
                    $"playerId={castle.PlayerId}, reason=player-resources-unavailable.");
                return;
            }

            var components = new List<string>();
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int buildingId = 1; buildingId < buildings.Length; buildingId++)
            {
                GameBuilding building = buildings[buildingId];
                if (building.r_PlayerIdOwner != castle.PlayerId ||
                    (building.r_AliveState != AliveState.NeedsInit &&
                     building.r_AliveState != AliveState.IsAlive) ||
                    !IsVanillaStartDiagnosticStructure(building.r_BuildingType))
                {
                    continue;
                }

                components.Add(
                    $"{buildingId + 1}:{building.r_BuildingType}:" +
                    $"({building.r_TilePositionXBegin},{building.r_TilePositionYBegin})-" +
                    $"({building.r_TilePositionXEnd},{building.r_TilePositionYEnd}):" +
                    $"tile={building.r_TileIdBegin}:global={building.r_GlobalId}");
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Vanilla start-state diagnostic: phase={phase}, playerId={castle.PlayerId}, " +
                $"orientation={castle.Orientation}, requestedKeep=({castle.RequestedKeepX},{castle.RequestedKeepY}), " +
                $"preparedKeep=({castle.PreparedKeepX},{castle.PreparedKeepY}), " +
                $"resourceKeep={resources->r_KeepId}:({resources->r_KeepTilePositionX},{resources->r_KeepTilePositionY}):" +
                $"tile={resources->r_KeepTileId}, " +
                $"resourceDoor={resources->r_KeepDoorId}:({resources->r_KeepDoorTilePositionX},{resources->r_KeepDoorTilePositionY}):" +
                $"tile={resources->r_KeepDoorTileId}, components=[{string.Join("|", components)}].");
        }

        private static bool IsVanillaStartDiagnosticStructure(eStructs structure)
        {
            return structure == eStructs.STRUCT_KEEP_ONE ||
                   structure == eStructs.STRUCT_KEEP_TWO ||
                   structure == eStructs.STRUCT_KEEP_THREE ||
                   structure == eStructs.STRUCT_KEEP_FOUR ||
                   structure == eStructs.STRUCT_KEEP_FIVE ||
                   structure == eStructs.STRUCT_KEEPDOOR ||
                   structure == eStructs.STRUCT_KEEPDOOR_LEFT ||
                   structure == eStructs.STRUCT_KEEPDOOR_RIGHT ||
                   structure == eStructs.STRUCT_CAMPGROUND ||
                   structure == eStructs.STRUCT_GOODS_YARD;
        }

        private PreparedAivCastle PrepareSelectedCastle(
            PendingAivImport prepared,
            int keepX,
            int keepY)
        {
            int playerId = prepared.PlayerId;
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) ||
                GamePlayerManagerAPI.Instance.IsAIPlayer(playerId))
            {
                throw new InvalidOperationException(
                    $"The imported player is not a valid human player; playerId={playerId}.");
            }

            if (!GameTileManagerAPI.Instance.IsTileInsideMapBounds(
                    keepX,
                    keepY))
            {
                throw new InvalidOperationException(
                    $"Native keep reference is outside the map: " +
                    $"({keepX},{keepY}).");
            }

            int ownedBuildingsBefore = CountOwnedBuildings(playerId);
            ImportedCandidateSnapshot importedCandidates =
                CaptureImportedCandidates(playerId - 1);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Native castle spawn planned: playerId={playerId}, " +
                $"phase=VanillaHumanStart(PreCoordinateRead), keepReference=({keepX},{keepY}), " +
                $"castle={prepared.DisplayName}, sha256={prepared.ContentHash}, " +
                $"rawShorts={prepared.RawShortCount}, importedDuringPre=True, " +
                $"ownedBuildingsBefore={ownedBuildingsBefore}, " +
                $"nativeCandidateCountBeforePlacement={importedCandidates.Count}, " +
                $"nativeCandidate0Pointer=0x{importedCandidates.FirstPointer.ToInt64():X}.");

            int specIndex = allocateSpec(aivState, playerId);
            if (specIndex < 1 || specIndex > 8)
            {
                throw new InvalidOperationException(
                    $"Native AIV spec allocation failed; returned specIndex={specIndex}.");
            }

            // The player explicitly chose this orientation. Other rotations must never be tried.
            setPlacement(
                aivState,
                specIndex,
                keepX,
                keepY,
                prepared.Rotation);
            selectBestFit(aivState, specIndex, 0);

            IntPtr spec = IntPtr.Add(aivState, checked(specIndex * AivSpecStride));
            int copiedPlayerAivValue =
                Marshal.ReadInt32(spec, SpecCopiedPlayerAivValueOffset);
            int orientation = Marshal.ReadInt32(spec, SpecOrientationOffset);
            int candidateId = Marshal.ReadInt32(spec, SpecCandidateIdOffset);
            int placementState = Marshal.ReadInt32(spec, SpecPlacementStateOffset);
            if (placementState != 1 && placementState != 2)
            {
                uint explicitFit = testSpecificCandidate(aivState, specIndex, 0);
                placementState = Marshal.ReadInt32(spec, SpecPlacementStateOffset);
                candidateId = Marshal.ReadInt32(spec, SpecCandidateIdOffset);
                orientation = Marshal.ReadInt32(spec, SpecOrientationOffset);
                throw new InvalidOperationException(
                    $"Vanilla could not place the selected AIV: specIndex={specIndex}, " +
                    $"candidateId={candidateId}, orientation={orientation}, " +
                    $"placementState={placementState}, " +
                    $"explicitCandidateFitUnsigned={explicitFit}, " +
                    $"explicitCandidateFitSigned={unchecked((int)explicitFit)}, " +
                    $"nativeCandidateCount={importedCandidates.Count}, " +
                    $"copiedPlayerAivValue={copiedPlayerAivValue}, " +
                    $"failureClass={DescribePlacementFailure(importedCandidates.Count)}.");
            }
            if (candidateId != 0)
            {
                throw new InvalidOperationException(
                    $"Native candidate selection returned unexpected candidateId={candidateId}; " +
                    "only candidate zero was imported.");
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Native AIV placement selected: specIndex={specIndex}, candidateId={candidateId}, " +
                $"orientation={orientation} ({DescribeOrientation(orientation)}), " +
                $"placementState={placementState} ({DescribePlacementState(placementState)}).");

            prepareLayout(aivState, specIndex, playerId);
            int highestFrame = Marshal.ReadInt32(spec, SpecHighestFrameOffset);
            if (highestFrame < 0)
            {
                throw new InvalidOperationException(
                    $"Native layout preparation returned invalid highestFrame={highestFrame}.");
            }

            IntPtr activeSpecAddress = IntPtr.Add(
                playerAivStateBase,
                checked(playerId * PlayerAivStateStride));
            int previousActiveSpec = Marshal.ReadInt32(activeSpecAddress);
            Marshal.WriteInt32(activeSpecAddress, specIndex);
            int nativePreparedKeepX = Marshal.ReadInt32(preparedKeepX);
            int nativePreparedKeepY = Marshal.ReadInt32(preparedKeepY);
            if (!GameTileManagerAPI.Instance.IsTileInsideMapBounds(
                    nativePreparedKeepX,
                    nativePreparedKeepY))
            {
                throw new InvalidOperationException(
                    $"Native layout preparation produced an out-of-bounds Keep: " +
                    $"({nativePreparedKeepX},{nativePreparedKeepY}).");
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Native AIV layout prepared before Vanilla Keep: playerId={playerId}, " +
                $"specIndex={specIndex}, highestFrame={highestFrame}, " +
                $"requestedKeep=({keepX},{keepY}), " +
                $"preparedKeep=({nativePreparedKeepX},{nativePreparedKeepY}), " +
                $"orientation={orientation}, previousActiveSpec={previousActiveSpec}.");

            return new PreparedAivCastle(
                playerId,
                specIndex,
                highestFrame,
                orientation,
                keepX,
                keepY,
                nativePreparedKeepX,
                nativePreparedKeepY,
                ownedBuildingsBefore,
                prepared.SourceDocument,
                prepared.FilteredDocument,
                prepared.FlagProjectileType,
                prepared.Options);
        }

        private void ExecutePreparedCastle(PreparedAivCastle castle)
        {
            int ownedBuildingsBeforeExecution = CountOwnedBuildings(castle.PlayerId);
            SetPrebuiltPlayerBit(castle.PlayerId);
            nativeCastleExecutionInProgress = true;
            nativeCastleExecutionPlayerId = castle.PlayerId;
            nextHovelVisualStyle = 0;
            correctedHovelVisualCount = 0;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Native castle execution planned: playerId={castle.PlayerId}, " +
                $"specIndex={castle.SpecIndex}, highestFrame={castle.HighestFrame}, " +
                $"completion=100, ownedBuildingsAtKeepPre={castle.OwnedBuildingsAtPreparation}, " +
                $"ownedBuildingsBeforeExecution={ownedBuildingsBeforeExecution}.");

            try
            {
                executeToPercentage(aivState, castle.PlayerId, 100);
                SpawnSupplementalContents(castle);
                QueueDeferredCompoundBuildings(castle);
            }
            finally
            {
                nativeCastleExecutionInProgress = false;
                nativeCastleExecutionPlayerId = 0;
            }

            int ownedBuildingsAfter = CountOwnedBuildings(castle.PlayerId);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Native castle execution completed: playerId={castle.PlayerId}, " +
                $"specIndex={castle.SpecIndex}, highestFrame={castle.HighestFrame}, " +
                $"ownedBuildingsBefore={ownedBuildingsBeforeExecution}, " +
                $"ownedBuildingsAfter={ownedBuildingsAfter}, " +
                $"buildingDelta={ownedBuildingsAfter - ownedBuildingsBeforeExecution}, " +
                $"correctedHovelVisuals={correctedHovelVisualCount}.");
            LogSpecialBuildingDiagnostics(castle.PlayerId);
        }

        private void OnBuildingSpawnPost(BuildingSpawnEventArgs args)
        {
            if (!captureSupplementalBuilding ||
                args.PlayerId != captureSupplementalPlayerId ||
                args.TileX != captureSupplementalX ||
                args.TileY != captureSupplementalY ||
                (captureSupplementalStruct != eStructs.STRUCT_NULL &&
                 args.Building != captureSupplementalStruct))
            {
                return;
            }
            capturedSupplementalBuildingId = unchecked((int)args.ReturnValue);
        }

        private void SpawnSupplementalContents(PreparedAivCastle castle)
        {
            var digestRows = new List<string>();
            var queuedDecorations = new HashSet<string>(StringComparer.Ordinal);
            AivRotation rotation = ToAivRotation(castle.Orientation);
            int nativeReferenceX = castle.RequestedKeepX;
            int nativeReferenceY = castle.RequestedKeepY;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Supplemental castle anchor resolved: playerId={castle.PlayerId}, " +
                $"nativeReference=({nativeReferenceX},{nativeReferenceY}), " +
                $"liveKeep=({castle.PreparedKeepX},{castle.PreparedKeepY}), " +
                $"liveKeepOffset=({castle.PreparedKeepX - nativeReferenceX},{castle.PreparedKeepY - nativeReferenceY}), " +
                $"orientation={castle.Orientation}.");

            foreach (AivJsonFrame frame in castle.FilteredDocument.frames)
            {
                AivFrameSpawnCategory frameCategory = AivSpawnPlan.ClassifyFrame(frame.itemType);
                bool isFearFactor = frameCategory == AivFrameSpawnCategory.FearFactor;
                bool isStockpile = frame.itemType == (int)eMappers.MAPPER_STORES;
                if (!isFearFactor && !isStockpile)
                    continue;

                string objectKind = isStockpile ? "AIV Stockpile" : "Fearfactor object";
                string digestKind = isStockpile ? "stockpile" : "fear";
                foreach (int encodedPosition in frame.tilePositionOfsets)
                {
                    AivGridPoint rawAnchor = new AivGridPoint(encodedPosition);
                    AivWorldTile projectedAnchor = AivWorldTransform.ProjectNativeFit(
                        rawAnchor,
                        nativeReferenceX,
                        nativeReferenceY,
                        rotation);
                    eMappers mapper = (eMappers)frame.itemType;
                    int footprintSize = AivMapperCatalog.Resolve(frame.itemType)
                        .FootprintSize.GetValueOrDefault(1);
                    if (footprintSize < 1)
                        footprintSize = 1;
                    AivWorldTile buildOrigin = AivNativeBuildingPlacement.ResolveBuildStructureOrigin(
                        rawAnchor,
                        footprintSize,
                        nativeReferenceX,
                        nativeReferenceY,
                        rotation);
                    if (!CanPlaceSupplementalPrefab(
                            mapper,
                            encodedPosition,
                            nativeReferenceX,
                            nativeReferenceY,
                            rotation,
                            out string reason))
                    {
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            $"Supplemental {objectKind} skipped: playerId={castle.PlayerId}, mapper={mapper}, " +
                            $"aivAnchor=({projectedAnchor.X},{projectedAnchor.Y}), " +
                            $"buildOrigin=({buildOrigin.X},{buildOrigin.Y}), reason={reason}.");
                        continue;
                    }
                    int height = GameTileManagerAPI.Instance.GetTileHeight(
                        GameTileManagerAPI.Instance.GetTileId(buildOrigin.X, buildOrigin.Y));
                    int id;
                    try
                    {
                        id = CreateSupplementalPrefab(
                            castle.PlayerId,
                            buildOrigin.X,
                            buildOrigin.Y,
                            mapper);
                    }
                    catch (Exception ex)
                    {
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            $"Supplemental {objectKind} creation threw and was skipped: " +
                            $"playerId={castle.PlayerId}, mapper={mapper}, " +
                            $"aivAnchor=({projectedAnchor.X},{projectedAnchor.Y}), " +
                            $"buildOrigin=({buildOrigin.X},{buildOrigin.Y}), " +
                            $"error={ex.GetBaseException().Message}.");
                        continue;
                    }
                    if (id > 0)
                    {
                        digestRows.Add(
                            $"{digestKind}:{(int)mapper}:{castle.PlayerId}:" +
                            $"{buildOrigin.X}:{buildOrigin.Y}:{height}");
                    }
                }
            }

            for (int index = 0; index < castle.FilteredDocument.miscItems.Count; index++)
            {
                AivJsonMiscItem item = castle.FilteredDocument.miscItems[index];
                AivMiscSpawnCategory category = AivSpawnPlan.ClassifyMisc(item.itemType);
                AivWorldTile tile = AivWorldTransform.ProjectNativeFit(
                    new AivGridPoint(item.positionOfset),
                    nativeReferenceX,
                    nativeReferenceY,
                    rotation);
                if (!GameTileManagerAPI.Instance.IsTileInsideMapBounds(tile.X, tile.Y))
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Supplemental misc item skipped: playerId={castle.PlayerId}, sourceIndex={index}, itemType={item.itemType}, position=({tile.X},{tile.Y}), reason=out-of-bounds.");
                    continue;
                }

                int tileId = GameTileManagerAPI.Instance.GetTileId(tile.X, tile.Y);
                int height = GameTileManagerAPI.Instance.GetTileHeight(tileId);
                if (category == AivMiscSpawnCategory.SiegeEngine)
                {
                    if (!AivSpawnPlan.TryMapSiegeEngine(item.itemType, out eChimps chimp))
                    {
                        LogUnknownMisc(castle.PlayerId, index, item);
                        continue;
                    }
                    long id;
                    try
                    {
                        id = GameUnitManagerAPI.Instance.CreateUnitLocal(
                            castle.PlayerId,
                            castle.PlayerId,
                            tile.X,
                            tile.Y,
                            height,
                            chimp);
                    }
                    catch (Exception ex)
                    {
                        Shared.DebugLogHelper.LogWarning(log, $"Supplemental unit creation threw and was skipped: playerId={castle.PlayerId}, sourceIndex={index}, chimp={chimp}, position=({tile.X},{tile.Y}), error={ex.GetBaseException().Message}.");
                        continue;
                    }
                    if (id <= 0)
                    {
                        Shared.DebugLogHelper.LogWarning(log, $"Supplemental unit creation failed: playerId={castle.PlayerId}, sourceIndex={index}, chimp={chimp}, position=({tile.X},{tile.Y}), height={height}.");
                        continue;
                    }
                    digestRows.Add($"unit:{(int)chimp}:{castle.PlayerId}:{tile.X}:{tile.Y}:{height}:slot{item.number}");
                    continue;
                }

                if (category == AivMiscSpawnCategory.Decoration)
                {
                    if (!AivSpawnPlan.TryMapDecoration(
                            item.itemType,
                            castle.PlayerId,
                            castle.FlagProjectileType,
                            out eMappers mapper,
                            out ProjectileType projectileType))
                    {
                        LogUnknownMisc(castle.PlayerId, index, item);
                        continue;
                    }

                    string decorationKey = $"{(int)mapper}:{(ushort)projectileType}:{tile.X}:{tile.Y}";
                    if (!queuedDecorations.Add(decorationKey) ||
                        HasMatchingDecoration(
                            tileId,
                            castle.PlayerId,
                            projectileType))
                    {
                        Shared.DebugLogHelper.LogInfo(
                            log,
                            $"Supplemental decoration skipped because it already exists or was queued: playerId={castle.PlayerId}, sourceIndex={index}, mapper={mapper}, position=({tile.X},{tile.Y}).");
                        continue;
                    }

                    if (!TryCreateDecoration(
                            castle.PlayerId,
                            tile.X,
                            tile.Y,
                            height,
                            mapper,
                            projectileType,
                            out int projectileId))
                    {
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            $"Supplemental decoration creation failed: playerId={castle.PlayerId}, sourceIndex={index}, mapper={mapper}, position=({tile.X},{tile.Y}), height={height}.");
                        continue;
                    }
                    if (projectileType == ProjectileType.Disease)
                        ExtraFeaturesFlagBridge.TryRegisterDiseaseFlag(projectileId, log);
                    digestRows.Add($"decoration:{(int)mapper}:{(ushort)projectileType}:{castle.PlayerId}:{tile.X}:{tile.Y}:{height}");
                    continue;
                }

                LogUnknownMisc(castle.PlayerId, index, item);
            }

            string digestPayload = string.Join("|", digestRows);
            string digest;
            using (SHA256 sha = SHA256.Create())
                digest = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(digestPayload))).Replace("-", string.Empty).ToLowerInvariant();
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Supplemental castle spawn digest: playerId={castle.PlayerId}, objects={digestRows.Count}, sha256={digest}, entries=[{digestPayload}].");
        }

        private static bool HasMatchingDecoration(
            int tileId,
            int playerId,
            ProjectileType expectedType)
        {
            short projectileId = GameTileManagerAPI.Instance.TileManager.FlyGrid[tileId];
            if (projectileId <= 0 ||
                !GameProjectileManagerAPI.Instance.TryGetProjectileById(projectileId, out GameProjectile* projectile) ||
                (projectile->r_AliveState != AliveState.NeedsInit && projectile->r_AliveState != AliveState.IsAlive))
            {
                return false;
            }

            return projectile->r_PlayerSourceId == (uint)playerId &&
                projectile->r_ProjectileType == expectedType;
        }

        private bool TryCreateDecoration(
            int playerId,
            int worldX,
            int worldY,
            int height,
            eMappers mapper,
            ProjectileType projectileType,
            out int projectileId)
        {
            projectileId = 0;
            int projectileX;
            int projectileY;
            try
            {
                projectileX = AivProjectileTransform.ToProjectileCoordinate(worldX);
                projectileY = AivProjectileTransform.ToProjectileCoordinate(worldY);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }

            long result;
            try
            {
                // Vanilla represents both AIV decorations as stationary projectiles. For flags,
                // r_PlayerSourceId selects the owning player's colour. Projectile positions use
                // eighth-tile units even though AIV and FlyGrid positions use whole tiles.
                result = GameProjectileManagerAPI.Instance.CreateProjectile(
                    0,
                    playerId,
                    projectileX,
                    projectileY,
                    height,
                    projectileX,
                    projectileY,
                    height,
                    projectileType,
                    0);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Supplemental decoration creation threw: playerId={playerId}, mapper={mapper}, projectileType={projectileType}, position=({worldX},{worldY}), error={ex.GetBaseException().Message}.");
                return false;
            }

            if (result <= 0 || result > int.MaxValue)
                return false;

            projectileId = (int)result;
            if (!GameProjectileManagerAPI.Instance.TryGetProjectileById(projectileId, out GameProjectile* projectile) ||
                (projectile->r_AliveState != AliveState.NeedsInit && projectile->r_AliveState != AliveState.IsAlive) ||
                projectile->r_ProjectileType != projectileType ||
                projectile->r_PlayerSourceId != (uint)playerId ||
                projectile->r_SourceWorldTileX != projectileX ||
                projectile->r_SourceWorldTileY != projectileY ||
                projectile->r_SourceElevation != height ||
                projectile->r_TargetWorldTileX != projectileX ||
                projectile->r_TargetWorldTileY != projectileY ||
                projectile->r_TargetElevation != height)
            {
                string observed = projectile == null
                    ? "unavailable"
                    : $"state={projectile->r_AliveState}, type={projectile->r_ProjectileType}, owner={projectile->r_PlayerSourceId}, source=({projectile->r_SourceWorldTileX},{projectile->r_SourceWorldTileY},{projectile->r_SourceElevation}), target=({projectile->r_TargetWorldTileX},{projectile->r_TargetWorldTileY},{projectile->r_TargetElevation}), current=({projectile->r_CurrentTileX},{projectile->r_CurrentTileY})";
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Supplemental decoration verification failed: playerId={playerId}, projectileId={projectileId}, mapper={mapper}, projectileType={projectileType}, tile=({worldX},{worldY}), projectilePosition=({projectileX},{projectileY}), observed={observed}.");
                return false;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Supplemental decoration created: playerId={playerId}, projectileId={projectileId}, mapper={mapper}, projectileType={projectileType}, position=({worldX},{worldY}), height={height}.");
            return true;
        }

        private bool CanPlaceSupplementalPrefab(
            eMappers mapper,
            int encodedPosition,
            int keepX,
            int keepY,
            AivRotation rotation,
            out string reason)
        {
            AivMapperInfo info = AivMapperCatalog.Resolve((int)mapper);
            int size = info.FootprintSize.GetValueOrDefault(1);
            if (size < 1)
                size = 1;
            AivGridPoint anchor = new AivGridPoint(encodedPosition);
            for (int rowOffset = 0; rowOffset < size; rowOffset++)
            {
                for (int columnOffset = 0; columnOffset < size; columnOffset++)
                {
                    int row = anchor.Row - rowOffset;
                    int column = anchor.Column + columnOffset;
                    if (row < 0 || row >= AivGridPoint.GridSize ||
                        column < 0 || column >= AivGridPoint.GridSize)
                    {
                        reason = "source-footprint-out-of-bounds";
                        return false;
                    }
                    AivWorldTile footprintTile = AivWorldTransform.ProjectNativeFit(
                        new AivGridPoint(row, column),
                        keepX,
                        keepY,
                        rotation);
                    if (!GameTileManagerAPI.Instance.IsTileInsideMapBounds(footprintTile.X, footprintTile.Y))
                    {
                        reason = "footprint-out-of-bounds";
                        return false;
                    }
                    int tileId = GameTileManagerAPI.Instance.GetTileId(footprintTile.X, footprintTile.Y);
                    if (GameTileManagerAPI.Instance.GetTileBuildingId(tileId) > 0)
                    {
                        reason = "occupied-or-already-created";
                        return false;
                    }
                }
            }
            reason = string.Empty;
            return true;
        }

        private int CreateSupplementalPrefab(
            int playerId,
            int x,
            int y,
            eMappers mapper,
            bool bypassPlacementRules = true)
        {
            captureSupplementalBuilding = true;
            captureSupplementalPlayerId = playerId;
            captureSupplementalX = x;
            captureSupplementalY = y;
            captureSupplementalStruct = mapper.ConvertToEStructs();
            capturedSupplementalBuildingId = -1;
            bool previousBypassEnabled = GameTileManagerAPI.Instance.TileManager.UsePlacementBlockedOverride;
            bool previousBypassValue = GameTileManagerAPI.Instance.TileManager.PlacementBlockedOverrideValue;
            long result;
            try
            {
                result = GameBuildingManagerAPI.Instance.CreatePrefab(
                    playerId,
                    x,
                    y,
                    mapper,
                    BuildingScales.GetScale(mapper),
                    0,
                    true,
                    bypassPlacementRules);
            }
            finally
            {
                GameTileManagerAPI.Instance.TileManager.PlacementBlockedOverrideValue = previousBypassValue;
                GameTileManagerAPI.Instance.TileManager.UsePlacementBlockedOverride = previousBypassEnabled;
                captureSupplementalBuilding = false;
            }

            int id = capturedSupplementalBuildingId > 0
                ? capturedSupplementalBuildingId
                : unchecked((int)result);
            if (id <= 0 ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(id, out GameBuilding* building) ||
                building->r_PlayerIdOwner != playerId ||
                (captureSupplementalStruct != eStructs.STRUCT_NULL &&
                 building->r_BuildingType != captureSupplementalStruct) ||
                (building->r_AliveState != AliveState.NeedsInit && building->r_AliveState != AliveState.IsAlive))
            {
                Shared.DebugLogHelper.LogWarning(log, $"Supplemental prefab creation failed verification: playerId={playerId}, mapper={mapper}, position=({x},{y}), returnedId={id}.");
                return -1;
            }
            return id;
        }

        private void QueueDeferredCompoundBuildings(PreparedAivCastle castle)
        {
            List<AivCompoundBuildingPlacement> plan = AivCompoundBuildingPlan.Create(
                castle.FilteredDocument,
                castle.RequestedKeepX,
                castle.RequestedKeepY,
                ToAivRotation(castle.Orientation));
            var repeatedMappers = new HashSet<eMappers>(
                plan.GroupBy(item => item.Mapper)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key));
            List<AivCompoundBuildingPlacement> compoundPlan = plan
                .Where(item => repeatedMappers.Contains(item.Mapper))
                .ToList();
            if (compoundPlan.Count == 0)
                return;

            int existing = compoundPlan.Count(item =>
                TryFindCompoundBuilding(castle.PlayerId, item, out _, out _));
            if (existing == compoundPlan.Count)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Deferred compound-building queue not needed: playerId={castle.PlayerId}, " +
                    $"planned={compoundPlan.Count}, existing={existing}.");
                return;
            }

            deferredCompoundBuildings[castle.PlayerId] = new DeferredCompoundBuildingQueue(
                castle.PlayerId,
                castle.RequestedKeepX,
                castle.RequestedKeepY,
                ToAivRotation(castle.Orientation),
                compoundPlan);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Deferred compound-building queue armed: playerId={castle.PlayerId}, " +
                $"planned={compoundPlan.Count}, existing={existing}, " +
                $"entries=[{string.Join("|", compoundPlan.Select(item =>
                    $"{item.SourceOrdinal}:{item.Mapper}:{item.BuildOrigin.X}:{item.BuildOrigin.Y}"))}].");
        }

        private void OnGameTick(int tick)
        {
            if (deferredCompoundBuildings.Count == 0)
                return;

            foreach (int playerId in deferredCompoundBuildings.Keys.ToArray())
            {
                if (!deferredCompoundBuildings.TryGetValue(
                        playerId,
                        out DeferredCompoundBuildingQueue queue))
                {
                    continue;
                }

                try
                {
                    if (ProcessDeferredCompoundBuilding(queue, tick))
                        deferredCompoundBuildings.Remove(playerId);
                }
                catch (Exception ex)
                {
                    deferredCompoundBuildings.Remove(playerId);
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Deferred compound-building queue aborted after an exception: " +
                        $"playerId={playerId}, tick={tick}, error={ex}.");
                }
            }
        }

        private bool ProcessDeferredCompoundBuilding(
            DeferredCompoundBuildingQueue queue,
            int tick)
        {
            if (queue.FirstTick < 0)
                queue.FirstTick = tick;
            if (tick - queue.FirstTick > DeferredCompoundPlacementTimeoutTicks)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Deferred compound-building queue timed out: playerId={queue.PlayerId}, " +
                    $"tick={tick}, cursor={queue.Cursor}/{queue.Placements.Count}.");
                return true;
            }

            while (queue.Cursor < queue.Placements.Count)
            {
                AivCompoundBuildingPlacement placement = queue.Placements[queue.Cursor];
                if (TryFindCompoundBuilding(
                        queue.PlayerId,
                        placement,
                        out int existingId,
                        out AliveState existingState))
                {
                    if (existingState != AliveState.IsAlive)
                        return false;

                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Deferred compound-building prerequisite ready: playerId={queue.PlayerId}, " +
                        $"sourceOrdinal={placement.SourceOrdinal}, mapper={placement.Mapper}, " +
                        $"position=({placement.BuildOrigin.X},{placement.BuildOrigin.Y}), " +
                        $"buildingId={existingId}, tick={tick}.");
                    queue.Cursor++;
                    queue.Attempts = 0;
                    continue;
                }

                AivCompoundBuildingPlacement? predecessor = FindPreviousSameType(
                    queue.Placements,
                    queue.Cursor,
                    placement.Mapper);
                if (!predecessor.HasValue ||
                    !TryFindCompoundBuilding(
                        queue.PlayerId,
                        predecessor.Value,
                        out _,
                        out AliveState predecessorState) ||
                    predecessorState != AliveState.IsAlive)
                {
                    // The first piece of each complex belongs to Vanilla. Without it,
                    // creating later pieces would hide a different placement failure.
                    return false;
                }

                if (!CanPlaceSupplementalPrefab(
                        placement.Mapper,
                        placement.EncodedPosition,
                        queue.NativeReferenceX,
                        queue.NativeReferenceY,
                        queue.Rotation,
                        out string reason))
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Deferred compound-building placement aborted: playerId={queue.PlayerId}, " +
                        $"sourceOrdinal={placement.SourceOrdinal}, mapper={placement.Mapper}, " +
                        $"position=({placement.BuildOrigin.X},{placement.BuildOrigin.Y}), " +
                        $"reason={reason}, tick={tick}.");
                    return true;
                }

                queue.Attempts++;
                int buildingId = CreateSupplementalPrefab(
                    queue.PlayerId,
                    placement.BuildOrigin.X,
                    placement.BuildOrigin.Y,
                    placement.Mapper,
                    bypassPlacementRules: false);
                if (buildingId > 0)
                {
                    int height = GameTileManagerAPI.Instance.GetTileHeight(
                        GameTileManagerAPI.Instance.GetTileId(
                            placement.BuildOrigin.X,
                            placement.BuildOrigin.Y));
                    queue.DigestRows.Add(
                        $"building:{(int)placement.Mapper}:{queue.PlayerId}:" +
                        $"{placement.BuildOrigin.X}:{placement.BuildOrigin.Y}:{height}");
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Deferred compound-building placement accepted by Vanilla: " +
                        $"playerId={queue.PlayerId}, sourceOrdinal={placement.SourceOrdinal}, " +
                        $"mapper={placement.Mapper}, position=({placement.BuildOrigin.X},{placement.BuildOrigin.Y}), " +
                        $"buildingId={buildingId}, attempt={queue.Attempts}, tick={tick}.");
                    return false;
                }

                if (queue.Attempts >= DeferredCompoundPlacementMaxAttempts)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Deferred compound-building placement rejected repeatedly: " +
                        $"playerId={queue.PlayerId}, sourceOrdinal={placement.SourceOrdinal}, " +
                        $"mapper={placement.Mapper}, position=({placement.BuildOrigin.X},{placement.BuildOrigin.Y}), " +
                        $"attempts={queue.Attempts}, tick={tick}.");
                    return true;
                }
                return false;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Deferred compound-building queue completed: playerId={queue.PlayerId}, " +
                $"placements={queue.Placements.Count}, tick={tick}.");
            string digestPayload = string.Join("|", queue.DigestRows);
            string digest;
            using (SHA256 sha = SHA256.Create())
            {
                digest = BitConverter.ToString(
                        sha.ComputeHash(Encoding.UTF8.GetBytes(digestPayload)))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Deferred compound-building spawn digest: playerId={queue.PlayerId}, " +
                $"objects={queue.DigestRows.Count}, sha256={digest}, entries=[{digestPayload}].");
            LogSpecialBuildingDiagnostics(queue.PlayerId);
            return true;
        }

        private static AivCompoundBuildingPlacement? FindPreviousSameType(
            IReadOnlyList<AivCompoundBuildingPlacement> placements,
            int cursor,
            eMappers mapper)
        {
            for (int index = cursor - 1; index >= 0; index--)
            {
                if (placements[index].Mapper == mapper)
                    return placements[index];
            }
            return null;
        }

        private static bool TryFindCompoundBuilding(
            int playerId,
            AivCompoundBuildingPlacement placement,
            out int buildingId,
            out AliveState aliveState)
        {
            eStructs structure = placement.Mapper.ConvertToEStructs();
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int index = 0; index < buildings.Length; index++)
            {
                GameBuilding building = buildings[index];
                if (building.r_PlayerIdOwner == playerId &&
                    building.r_BuildingType == structure &&
                    building.r_TilePositionXBegin == placement.BuildOrigin.X &&
                    building.r_TilePositionYBegin == placement.BuildOrigin.Y &&
                    (building.r_AliveState == AliveState.NeedsInit ||
                     building.r_AliveState == AliveState.IsAlive))
                {
                    buildingId = index;
                    aliveState = building.r_AliveState;
                    return true;
                }
            }

            buildingId = -1;
            aliveState = default;
            return false;
        }

        private void ClearDeferredCompoundPlacements(string reason)
        {
            if (deferredCompoundBuildings.Count > 0)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Deferred compound-building queues cleared: reason={reason}, " +
                    $"players=[{string.Join(",", deferredCompoundBuildings.Keys)}].");
            }
            deferredCompoundBuildings.Clear();
        }

        private void LogUnknownMisc(int playerId, int index, AivJsonMiscItem item)
        {
            Shared.DebugLogHelper.LogWarning(
                log,
                $"Unknown supplemental misc item skipped: playerId={playerId}, sourceIndex={index}, itemType={item.itemType}, positionOffset={item.positionOfset}, number={item.number}.");
        }

        private static AivRotation ToAivRotation(int nativeOrientation)
        {
            switch (nativeOrientation)
            {
                case 0: return AivRotation.Degrees0;
                case 2: return AivRotation.Degrees90;
                case 4: return AivRotation.Degrees180;
                case 6: return AivRotation.Degrees270;
                default: throw new InvalidOperationException($"Unsupported native AIV orientation: {nativeOrientation}.");
            }
        }

        private void BindNativeFunctions(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory)
        {
            IntPtr allocateAddress = ResolveUniqueAddress(
                libraryHandle,
                memory,
                nameof(AllocateSpecDelegate),
                AllocateSpecPattern,
                AllocateSpecRva);
            allocateSpec = Marshal.GetDelegateForFunctionPointer<AllocateSpecDelegate>(
                allocateAddress);
            setPlacement = Bind<SetPlacementDelegate>(
                libraryHandle,
                memory,
                SetPlacementPattern,
                SetPlacementRva);
            selectBestFit = Bind<SelectBestFitDelegate>(
                libraryHandle,
                memory,
                SelectBestFitPattern,
                SelectBestFitRva);
            testSpecificCandidate = Bind<TestSpecificCandidateDelegate>(
                libraryHandle,
                memory,
                TestSpecificCandidatePattern,
                TestSpecificCandidateRva);
            prepareLayout = Bind<PrepareLayoutDelegate>(
                libraryHandle,
                memory,
                PrepareLayoutPattern,
                PrepareLayoutRva);
            executeToPercentage = Bind<ExecuteToPercentageDelegate>(
                libraryHandle,
                memory,
                ExecuteToPercentagePattern,
                ExecuteToPercentageRva);
            int stateReferenceOffset = ResolveReferenceRva(
                memory,
                nameof(aivState),
                AivStateReferencePattern,
                AivStateReferenceRva);
            IntPtr stateReferenceInstruction = IntPtr.Add(
                libraryHandle,
                stateReferenceOffset + 4);
            aivState = ResolveRipRelativeAddress(
                stateReferenceInstruction,
                displacementOffset: 3,
                instructionLength: 7);

            // AllocateSpec contains LEA RAX,[playerStateBase+4] at function offset 0x5F.
            IntPtr playerStateInstruction = IntPtr.Add(allocateAddress, 0x5F);
            RequireBytes(
                playerStateInstruction,
                "player AIV state reference",
                0x48,
                0x8D,
                0x05);
            playerAivStateBase = IntPtr.Subtract(
                ResolveRipRelativeAddress(
                    playerStateInstruction,
                    displacementOffset: 3,
                    instructionLength: 7),
                4);

            int prebuiltReferenceOffset = ResolveReferenceRva(
                memory,
                nameof(prebuiltPlayersBitField),
                PrebuiltPlayersReferencePattern,
                PrebuiltPlayersReferenceRva);
            IntPtr prebuiltReferenceInstruction = IntPtr.Add(
                libraryHandle,
                prebuiltReferenceOffset);
            prebuiltPlayersBitField = ResolveRipRelativeAddress(
                prebuiltReferenceInstruction,
                displacementOffset: 2,
                instructionLength: 6);

            int preparedKeepReferenceOffset = ResolveReferenceRva(
                memory,
                "prepared Keep coordinates",
                PreparedKeepCoordinatesReferencePattern,
                PreparedKeepCoordinatesReferenceRva);
            IntPtr preparedKeepReference = IntPtr.Add(
                libraryHandle,
                preparedKeepReferenceOffset);
            preparedKeepY = ResolveRipRelativeAddress(
                IntPtr.Add(preparedKeepReference, 12),
                displacementOffset: 3,
                instructionLength: 7);
            preparedKeepX = ResolveRipRelativeAddress(
                IntPtr.Add(preparedKeepReference, 21),
                displacementOffset: 3,
                instructionLength: 7);

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Native AIV bindings resolved: module=0x{libraryHandle.ToInt64():X}, " +
                $"aivState=0x{aivState.ToInt64():X}, " +
                $"playerAivStateBase=0x{playerAivStateBase.ToInt64():X}, " +
                $"prebuiltPlayers=0x{prebuiltPlayersBitField.ToInt64():X}, " +
                $"preparedKeepX=0x{preparedKeepX.ToInt64():X}, " +
                $"preparedKeepY=0x{preparedKeepY.ToInt64():X}.");
        }

        private void InstallHumanStartPreparationHook(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory)
        {
            int humanStartHookRva = ResolveReferenceRva(
                memory,
                "Vanilla human Keep coordinate load",
                HumanKeepCoordinateLoadPattern,
                HumanKeepCoordinateLoadRva);
            nativeHookTransaction = new HookTransaction(
                memory,
                unchecked((ulong)libraryHandle.ToInt64()),
                loggerFactory: null,
                failureMode: TransactionFailureMode.RollbackAndThrow);
            nativeHookTransaction.AddContextHook(
                ref humanKeepCoordinateLoadHook,
                unchecked((ulong)libraryHandle.ToInt64()) + unchecked((ulong)humanStartHookRva),
                PrepareVanillaHumanStart,
                regs: X64SmartCPUContextRegs.All,
                hookSize: 16,
                errorMode: CallbackErrorMode.LogAndContinue,
                placement: OverwrittenInstructionPlacement.AfterCallback);
            nativeHookTransaction.Commit();
            if (!humanKeepCoordinateLoadHook.Success)
                throw new InvalidOperationException("The Vanilla human Keep coordinate-load hook was not installed.");

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Early Vanilla human-start hooks installed: " +
                $"keepCoordinateRva=0x{humanStartHookRva:X}.");
        }

        private void PrepareVanillaHumanStart(
            NativePointer<X64SmartCPUContext> context)
        {
            X64SmartCPUContext* registers = context.Pointer;
            int playerId = unchecked((int)registers->RSI);
            if (!pendingAivImports.TryGetValue(playerId, out PendingAivImport imported) ||
                preparedAivCastles.ContainsKey(playerId))
            {
                return;
            }

            pendingAivImports.Remove(playerId);
            try
            {
                int startIndex = unchecked((int)registers->RCX);
                if (startIndex < 0 || startIndex >= 9)
                {
                    throw new InvalidOperationException(
                        $"Vanilla returned an invalid human start index {startIndex} for playerId={playerId}.");
                }

                byte* frame = (byte*)registers->RBP;
                int* keepX = (int*)(frame + 0xD50 + startIndex * 8);
                int* keepY = (int*)(frame + 0xD54 + startIndex * 8);
                int requestedKeepX = *keepX;
                int requestedKeepY = *keepY;
                PreparedAivCastle castle = PrepareSelectedCastle(
                    imported,
                    requestedKeepX,
                    requestedKeepY);

                // Canonical CrusaderDE.dll SHA-256
                // FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2:
                // RVA 0x95B3C is the first read of the human Keep coordinates. Updating
                // Vanilla's source values here makes the unmodified caller feed the same
                // anchor to the Keep, coupled start complex, flag and later unit setup.
                *keepX = castle.PreparedKeepX;
                *keepY = castle.PreparedKeepY;
                *(int*)(registers->RSP + 0x30) = castle.Orientation;
                preparedAivCastles[playerId] = castle;

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Vanilla human start prepared before its first Keep-coordinate read: " +
                    $"playerId={playerId}, startIndex={startIndex}, " +
                    $"requestedKeep=({requestedKeepX},{requestedKeepY}), " +
                    $"preparedKeep=({castle.PreparedKeepX},{castle.PreparedKeepY}), " +
                    $"orientation={castle.Orientation} ({DescribeOrientation(castle.Orientation)}).");
            }
            catch (Exception ex)
            {
                preparedAivCastles.Remove(playerId);
                failedAivCastlePlayers.Add(playerId);
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Early Vanilla human-start preparation failed for playerId={playerId}; " +
                    $"Vanilla retains its original start data and no castle fallback will run: {ex}");
            }
        }

        private void ApplyFixesGoodsyardPolicy(int playerId, AivSpawnOptions options)
        {
            if (!TryGetFixesPlaceGoodsyard(playerId, out bool placeGoodsyard))
                return;

            options.SpawnStockpile = placeGoodsyard;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Fixes goodsyard policy applied to AIV spawn plan: " +
                $"playerId={playerId}, placeGoodsyard={placeGoodsyard}.");
        }

        private bool TryGetFixesPlaceGoodsyard(int playerId, out bool placeGoodsyard)
        {
            placeGoodsyard = true;
            if (!Chainloader.PluginInfos.TryGetValue("fixes", out BepInEx.PluginInfo pluginInfo) ||
                pluginInfo == null ||
                ReferenceEquals(pluginInfo.Instance, null))
            {
                return false;
            }

            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
                Type pluginType = pluginInfo.Instance.GetType();
                object modularGoodsyardPlacement = pluginType
                    .GetField("ModularGoodsyardPlacement", flags)?
                    .GetValue(pluginInfo.Instance);
                object modularEnabledValue = modularGoodsyardPlacement?.GetType()
                    .GetProperty("Value", flags)?
                    .GetValue(modularGoodsyardPlacement, null);
                if (!(modularEnabledValue is bool modularEnabled) || !modularEnabled)
                    return false;

                object viewModel = pluginType
                    .GetProperty("LobbySettingsViewModel", flags)?
                    .GetValue(pluginInfo.Instance, null);
                bool[] data = viewModel?.GetType()
                    .GetProperty("PlaceGoodsyardData", flags)?
                    .GetValue(viewModel, null) as bool[];
                if (data == null || playerId < 1 || playerId >= data.Length)
                {
                    throw new InvalidOperationException(
                        $"Fixes PlaceGoodsyardData is unavailable for playerId={playerId}.");
                }

                placeGoodsyard = data[playerId];
                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "The installed Fixes mod exposes no compatible PlaceGoodsyardData setting.",
                    ex);
            }
        }

        private T Bind<T>(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            string pattern,
            int referenceRva)
            where T : Delegate
        {
            IntPtr address = ResolveUniqueAddress(
                libraryHandle,
                memory,
                typeof(T).Name,
                pattern,
                referenceRva);
            return Marshal.GetDelegateForFunctionPointer<T>(address);
        }

        private IntPtr ResolveUniqueAddress(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            string name,
            string pattern,
            int referenceRva)
        {
            return IntPtr.Add(
                libraryHandle,
                ResolveReferenceRva(memory, name, pattern, referenceRva));
        }

        private int ResolveReferenceRva(
            ReadOnlySpan<byte> memory,
            string name,
            string pattern,
            int referenceRva)
        {
            return Shared.NativePatternResolver.ResolveUnique(
                memory,
                pattern,
                referenceRva,
                referenceHashMatches,
                name,
                log).Rva;
        }

        private static IntPtr ResolveRipRelativeAddress(
            IntPtr instruction,
            int displacementOffset,
            int instructionLength)
        {
            int displacement = Marshal.ReadInt32(instruction, displacementOffset);
            return new IntPtr(
                checked(instruction.ToInt64() + instructionLength + displacement));
        }

        private static void RequireBytes(
            IntPtr address,
            string name,
            params byte[] expected)
        {
            for (int index = 0; index < expected.Length; index++)
            {
                byte actual = Marshal.ReadByte(address, index);
                if (actual != expected[index])
                {
                    throw new InvalidOperationException(
                        $"Native {name} opcode mismatch at +0x{index:X}: " +
                        $"expected=0x{expected[index]:X2}, actual=0x{actual:X2}.");
                }
            }
        }

        private void SetPrebuiltPlayerBit(int playerId)
        {
            int current = Marshal.ReadInt32(prebuiltPlayersBitField);
            int updated = current | (1 << (playerId - 1));
            Marshal.WriteInt32(prebuiltPlayersBitField, updated);
        }

        private static ImportedCandidateSnapshot CaptureImportedCandidates(
            int zeroBasedPlayerSlot)
        {
            if (zeroBasedPlayerSlot < 0 || zeroBasedPlayerSlot >= 8)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(zeroBasedPlayerSlot),
                    zeroBasedPlayerSlot,
                    "The native AIV candidate table only contains eight player slots.");
            }

            ulong tableVirtualAddress = GameGlobalsManager.Instance.AIVDataTableVA;
            if (tableVirtualAddress == 0)
            {
                throw new InvalidOperationException(
                    "The Script Extender did not resolve the native AIV candidate table.");
            }

            IntPtr tableAddress = new IntPtr(checked((long)tableVirtualAddress));
            IntPtr playerTableAddress = IntPtr.Add(
                tableAddress,
                checked(zeroBasedPlayerSlot * ImportedCandidatesPerPlayer * IntPtr.Size));
            IntPtr firstPointer = Marshal.ReadIntPtr(playerTableAddress);
            int count = 0;
            while (count < ImportedCandidatesPerPlayer &&
                   Marshal.ReadIntPtr(
                       playerTableAddress,
                       checked(count * IntPtr.Size)) != IntPtr.Zero)
            {
                count++;
            }

            return new ImportedCandidateSnapshot(
                tableAddress,
                firstPointer,
                count);
        }

        private static int[] CaptureHumanPlayerIds()
        {
            return Shared.ActivePlayerHelper.GetActivePlayerIds()
                .Where(playerId => !GamePlayerManagerAPI.Instance.IsAIPlayer(playerId))
                .ToArray();
        }

        private static string DescribePlacementFailure(int nativeCandidateCount)
        {
            // Native TestSpecificCandidate returns -2 both for an absent candidate
            // and for a candidate whose layout cannot be placed.
            return nativeCandidateCount == 0
                ? "candidate-missing-from-native-table"
                : "candidate-present-but-map-fit-rejected";
        }

        private static GameModeSnapshot CaptureGameMode(MapStartEventArgs args)
        {
            Shared.GameModeSnapshot sharedMode =
                Shared.GameModeHelper.Capture(args.bMultiplayerSave != 0);
            Director director = Director.instance;
            GameData gameData = GameData.Instance;
            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
            Platform_Multiplayer.MPLobby lobby = platform?.activeLobby;

            int lobbySkirmishMemberCount = 0;
            int lobbySkirmishHumanCount = 0;
            if (lobby?.members != null)
            {
                foreach (Platform_Multiplayer.MPLobbyMember member in lobby.members)
                {
                    if (member.SkirmishMember)
                    {
                        lobbySkirmishMemberCount++;
                        if (member.SkirmishHumanMember)
                            lobbySkirmishHumanCount++;
                    }
                }
            }

            var gameMemberDetails = new List<string>();
            if (platform?.gameMembers != null)
            {
                foreach (Platform_Multiplayer.MPGameMember member in platform.gameMembers)
                {
                    gameMemberDetails.Add(
                        $"{member.playerID}:self={member.isSelf}," +
                        $"host={member.isHost},ai={member.skirmishAI}," +
                        $"steam={(member.steamID > 1000 ? "real" : member.steamID.ToString())}," +
                        $"kicked={member.kicked}");
                }
            }

            var activePlayerDetails = new List<string>();
            foreach (int id in Shared.ActivePlayerHelper.GetActivePlayerIds())
            {
                activePlayerDetails.Add(
                    $"{id}:ai={GamePlayerManagerAPI.Instance.IsAIPlayer(id)}");
            }

            int networkActivePlayers = sharedMode.LowLevelNetworked
                ? GameNetworkAPI.GetNumActivePlayers()
                : -1;

            return new GameModeSnapshot
            {
                CampaignMapId = args.CampaignMapId,
                MultiplayerSave = args.bMultiplayerSave,
                Unknown1 = args.Unknown1,
                Unknown3 = args.Unknown3,
                SharedRealMultiplayer = sharedMode.IsRealMultiplayer,
                SharedSingleplayerSkirmish = sharedMode.IsSingleplayerSkirmish,
                SharedSingleplayerTrail = sharedMode.IsSingleplayerTrail,
                SharedModeDetails = sharedMode.ToDiagnosticString(),
                DirectorAvailable = sharedMode.DirectorAvailable,
                DirectorMultiplayerGame = sharedMode.DirectorMultiplayer,
                DirectorSkirmishModeGame = sharedMode.DirectorSkirmish,
                DirectorSimRunning = director != null && director.SimRunning,
                NetworkedEnvironment = sharedMode.LowLevelNetworked,
                NetworkActivePlayers = networkActivePlayers,
                NativeLocalPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId(),
                PlatformAvailable = platform != null,
                PlatformMpGameActive = sharedMode.PlatformMultiplayer,
                PlatformIsHost = platform != null && platform.IsHost,
                ActiveLobbyAvailable = lobby != null,
                LobbyReportedMemberCount = lobby?.numLobbyMembers ?? -1,
                LobbyMemberCount = sharedMode.LobbyMembers,
                LobbySkirmishMemberCount = lobbySkirmishMemberCount,
                LobbySkirmishHumanCount = lobbySkirmishHumanCount,
                LobbyNetworkHumanCount = sharedMode.RealLobbyMembers,
                GameMemberCount = sharedMode.GameMembers,
                RealNetworkGameMemberCount = sharedMode.RealNetworkGameMembers,
                GameMemberDetails = string.Join(" | ", gameMemberDetails),
                ActivePlayerDetails = string.Join(" | ", activePlayerDetails),
                GameDataMultiplayerMap = gameData != null && gameData.multiplayerMap,
                GameDataSkirmishGameType = sharedMode.SkirmishGameType,
                GameDataGameType = sharedMode.GameType
            };
        }

        private void LogGameModeDiagnostics(GameModeSnapshot mode)
        {
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Game-mode diagnostics: campaignMapId={mode.CampaignMapId}, " +
                $"bMultiplayerSave={mode.MultiplayerSave}, " +
                $"unknown1=0x{mode.Unknown1.ToInt64():X}, unknown3={mode.Unknown3}, " +
                $"sharedRealMultiplayer={mode.SharedRealMultiplayer}, " +
                $"sharedSingleplayerSkirmish={mode.SharedSingleplayerSkirmish}, " +
                $"sharedSingleplayerTrail={mode.SharedSingleplayerTrail}, " +
                $"directorAvailable={mode.DirectorAvailable}, " +
                $"directorMultiplayerGame={mode.DirectorMultiplayerGame}, " +
                $"directorSkirmishModeGame={mode.DirectorSkirmishModeGame}, " +
                $"directorSimRunning={mode.DirectorSimRunning}, " +
                $"networkedEnvironment={mode.NetworkedEnvironment}, " +
                $"networkActivePlayers={mode.NetworkActivePlayers}, " +
                $"nativeLocalPlayerId={mode.NativeLocalPlayerId}, " +
                $"platformAvailable={mode.PlatformAvailable}, " +
                $"platformMpGameActive={mode.PlatformMpGameActive}, " +
                $"platformIsHost={mode.PlatformIsHost}, " +
                $"activeLobbyAvailable={mode.ActiveLobbyAvailable}, " +
                $"lobbyReportedMembers={mode.LobbyReportedMemberCount}, " +
                $"lobbyMembers={mode.LobbyMemberCount}, " +
                $"lobbySkirmishMembers={mode.LobbySkirmishMemberCount}, " +
                $"lobbySkirmishHumans={mode.LobbySkirmishHumanCount}, " +
                $"lobbyNetworkHumans={mode.LobbyNetworkHumanCount}, " +
                $"gameMembers={mode.GameMemberCount}, " +
                $"realNetworkGameMembers={mode.RealNetworkGameMemberCount}, " +
                $"gameDataMultiplayerMap={mode.GameDataMultiplayerMap}, " +
                $"gameDataSkirmishGameType={mode.GameDataSkirmishGameType}, " +
                $"gameDataGameType={mode.GameDataGameType}.");

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Shared game-mode diagnostics: {mode.SharedModeDetails}.");

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Game-member diagnostics: [{mode.GameMemberDetails}].");
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Active-player diagnostics (not used for multiplayer detection): " +
                $"[{mode.ActivePlayerDetails}].");
        }

        private static void EnsureSupportedGameMode(GameModeSnapshot mode)
        {
            if (!mode.DirectorAvailable)
            {
                throw new InvalidOperationException(
                    "Director is unavailable during the map-start callback; game mode cannot be verified.");
            }

            if (!mode.SharedSingleplayerSkirmish &&
                !mode.SharedSingleplayerTrail &&
                !mode.SharedRealMultiplayer)
            {
                throw new NotSupportedException(
                    $"Native CastlePlanner requires a singleplayer or multiplayer skirmish: " +
                    $"{mode.SharedModeDetails}.");
            }
        }

        private static int CountOwnedBuildings(int playerId)
        {
            int count = 0;
            Span<GameBuilding> buildings =
                GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            foreach (GameBuilding building in buildings)
            {
                if (building.r_PlayerIdOwner == playerId &&
                    (building.r_AliveState == AliveState.NeedsInit ||
                     building.r_AliveState == AliveState.IsAlive))
                {
                    count++;
                }
            }

            return count;
        }

        private void LogSpecialBuildingDiagnostics(int playerId)
        {
            int granaryCount = 0;
            int hovelCount = 0;
            Span<GameBuilding> buildings =
                GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int buildingId = 0; buildingId < buildings.Length; buildingId++)
            {
                GameBuilding building = buildings[buildingId];
                if (building.r_PlayerIdOwner != playerId ||
                    (building.r_BuildingType != eStructs.STRUCT_GRANARY &&
                     building.r_BuildingType != eStructs.STRUCT_HOVEL))
                {
                    continue;
                }

                if (building.r_BuildingType == eStructs.STRUCT_GRANARY)
                    granaryCount++;
                else
                    hovelCount++;

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Native special-building diagnostics: playerId={playerId}, " +
                    $"buildingId={buildingId}, globalId={building.r_GlobalId}, " +
                    $"type={building.r_BuildingType}, aliveState={building.r_AliveState}, " +
                    $"tiles=({building.r_TilePositionXBegin},{building.r_TilePositionYBegin})-" +
                    $"({building.r_TilePositionXEnd},{building.r_TilePositionYEnd}), " +
                    $"gridSize={building.r_OccupyTileGridSize}, " +
                    $"height={building.r_HeightElevation}, " +
                    $"spritePlayerColorId={building.r_SpritePlayerColorId}, " +
                    $"spriteVariation={building.r_SpriteVariationIndex}, " +
                    $"hovelVisualStyle={building.r_BuildingVariation}, " +
                    $"material={building.r_GameMaterialIndex}, " +
                    $"health={building.r_CurrentHealth}/{building.r_MaxHealth}.");
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Native special-building summary: playerId={playerId}, " +
                $"granaries={granaryCount}, hovels={hovelCount}.");
        }

        private static bool IsKeepMapper(eMappers mapper)
        {
            return mapper == eMappers.MAPPER_KEEP1 ||
                   mapper == eMappers.MAPPER_KEEP2 ||
                   mapper == eMappers.MAPPER_KEEP3 ||
                   mapper == eMappers.MAPPER_KEEP4 ||
                   mapper == eMappers.MAPPER_KEEP5;
        }

        private static string DescribeOrientation(int orientation)
        {
            switch (orientation)
            {
                case 0:
                    return "0 degrees";
                case 2:
                    return "90 degrees";
                case 4:
                    return "180 degrees";
                case 6:
                    return "270 degrees";
                default:
                    return "unknown";
            }
        }

        private static string DescribePlacementState(int placementState)
        {
            switch (placementState)
            {
                case 1:
                    return "best partial fit";
                case 2:
                    return "complete fit";
                default:
                    return "not placed";
            }
        }

        private readonly struct ImportedCandidateSnapshot
        {
            public ImportedCandidateSnapshot(
                IntPtr tableAddress,
                IntPtr firstPointer,
                int count)
            {
                TableAddress = tableAddress;
                FirstPointer = firstPointer;
                Count = count;
            }

            public IntPtr TableAddress { get; }
            public IntPtr FirstPointer { get; }
            public int Count { get; }
        }


        private sealed class GameModeSnapshot
        {
            public int CampaignMapId { get; set; }
            public byte MultiplayerSave { get; set; }
            public IntPtr Unknown1 { get; set; }
            public ulong Unknown3 { get; set; }
            public bool SharedRealMultiplayer { get; set; }
            public bool SharedSingleplayerSkirmish { get; set; }
            public bool SharedSingleplayerTrail { get; set; }
            public string SharedModeDetails { get; set; }
            public bool DirectorAvailable { get; set; }
            public bool DirectorMultiplayerGame { get; set; }
            public bool DirectorSkirmishModeGame { get; set; }
            public bool DirectorSimRunning { get; set; }
            public bool NetworkedEnvironment { get; set; }
            public int NetworkActivePlayers { get; set; }
            public int NativeLocalPlayerId { get; set; }
            public bool PlatformAvailable { get; set; }
            public bool PlatformMpGameActive { get; set; }
            public bool PlatformIsHost { get; set; }
            public bool ActiveLobbyAvailable { get; set; }
            public int LobbyReportedMemberCount { get; set; }
            public int LobbyMemberCount { get; set; }
            public int LobbySkirmishMemberCount { get; set; }
            public int LobbySkirmishHumanCount { get; set; }
            public int LobbyNetworkHumanCount { get; set; }
            public int GameMemberCount { get; set; }
            public int RealNetworkGameMemberCount { get; set; }
            public string GameMemberDetails { get; set; }
            public string ActivePlayerDetails { get; set; }
            public bool GameDataMultiplayerMap { get; set; }
            public int GameDataSkirmishGameType { get; set; }
            public int GameDataGameType { get; set; }
        }

        private sealed class PendingAivImport
        {
            public PendingAivImport(
                int playerId,
                string displayName,
                string contentHash,
                int rotation,
                ushort flagProjectileType,
                short[] rawAiv,
                AivJsonDocument sourceDocument,
                AivJsonDocument filteredDocument,
                AivSpawnOptions options)
            {
                PlayerId = playerId;
                DisplayName = displayName ?? string.Empty;
                ContentHash = contentHash ?? string.Empty;
                Rotation = rotation;
                FlagProjectileType = flagProjectileType;
                RawAiv = rawAiv ?? throw new ArgumentNullException(nameof(rawAiv));
                SourceDocument = sourceDocument ?? throw new ArgumentNullException(nameof(sourceDocument));
                FilteredDocument = filteredDocument ?? throw new ArgumentNullException(nameof(filteredDocument));
                Options = options ?? throw new ArgumentNullException(nameof(options));
            }

            public int PlayerId { get; }
            public string DisplayName { get; }
            public string ContentHash { get; }
            public int Rotation { get; }
            public ushort FlagProjectileType { get; }
            public short[] RawAiv { get; }
            public AivJsonDocument SourceDocument { get; }
            public AivJsonDocument FilteredDocument { get; }
            public AivSpawnOptions Options { get; }
            public int RawShortCount => RawAiv.Length;
        }

        private sealed class PreparedAivCastle
        {
            public PreparedAivCastle(
                int playerId,
                int specIndex,
                int highestFrame,
                int orientation,
                int requestedKeepX,
                int requestedKeepY,
                int preparedKeepX,
                int preparedKeepY,
                int ownedBuildingsAtPreparation,
                AivJsonDocument sourceDocument,
                AivJsonDocument filteredDocument,
                ushort flagProjectileType,
                AivSpawnOptions options)
            {
                PlayerId = playerId;
                SpecIndex = specIndex;
                HighestFrame = highestFrame;
                Orientation = orientation;
                RequestedKeepX = requestedKeepX;
                RequestedKeepY = requestedKeepY;
                PreparedKeepX = preparedKeepX;
                PreparedKeepY = preparedKeepY;
                OwnedBuildingsAtPreparation = ownedBuildingsAtPreparation;
                SourceDocument = sourceDocument;
                FilteredDocument = filteredDocument;
                FlagProjectileType = flagProjectileType;
                Options = options;
            }

            public int PlayerId { get; }
            public int SpecIndex { get; }
            public int HighestFrame { get; }
            public int Orientation { get; }
            public int RequestedKeepX { get; }
            public int RequestedKeepY { get; }
            public int PreparedKeepX { get; }
            public int PreparedKeepY { get; }
            public int OwnedBuildingsAtPreparation { get; }
            public AivJsonDocument SourceDocument { get; }
            public AivJsonDocument FilteredDocument { get; }
            public ushort FlagProjectileType { get; }
            public AivSpawnOptions Options { get; }
        }

        private sealed class DeferredCompoundBuildingQueue
        {
            public DeferredCompoundBuildingQueue(
                int playerId,
                int nativeReferenceX,
                int nativeReferenceY,
                AivRotation rotation,
                List<AivCompoundBuildingPlacement> placements)
            {
                PlayerId = playerId;
                NativeReferenceX = nativeReferenceX;
                NativeReferenceY = nativeReferenceY;
                Rotation = rotation;
                Placements = placements ?? throw new ArgumentNullException(nameof(placements));
            }

            public int PlayerId { get; }
            public int NativeReferenceX { get; }
            public int NativeReferenceY { get; }
            public AivRotation Rotation { get; }
            public List<AivCompoundBuildingPlacement> Placements { get; }
            public List<string> DigestRows { get; } = new List<string>();
            public int Cursor { get; set; }
            public int Attempts { get; set; }
            public int FirstTick { get; set; } = -1;
        }

    }
}
