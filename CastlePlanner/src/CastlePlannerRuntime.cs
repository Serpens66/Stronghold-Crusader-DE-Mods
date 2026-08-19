using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

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

        private const int AllocateSpecRva = 0x50680;
        private const int SetPlacementRva = 0x54EC0;
        private const int SelectBestFitRva = 0x54F60;
        private const int TestSpecificCandidateRva = 0x54DE0;
        private const int PrepareLayoutRva = 0x53D00;
        private const int ExecuteToPercentageRva = 0x55F50;
        private const int AivStateReferenceRva = 0x95C9F;
        private const int PrebuiltPlayersReferenceRva = 0x95FF8;
        private const int PreparedKeepCoordinatesReferenceRva = 0x95EA3;

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
        private bool installed;
        private bool handledCurrentMap;
        private bool aivImportedForCurrentMap;
        private PendingAivImport pendingAivImport;
        private PreparedAivCastle preparedAivCastle;
        private PreparedAivCastle executedAivCastle;
        private bool nativeCastleExecutionInProgress;
        private int nativeCastleExecutionPlayerId;
        private int nextHovelVisualStyle;
        private int correctedHovelVisualCount;

        public CastlePlannerRuntime(
            ManualLogSource log,
            CastlePlannerSettingsViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Install(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory)
        {
            if (installed)
                return;

            BindNativeFunctions(libraryHandle, memory);

            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                .Subscribe(OnStartMap));
            subscriptions.Add(BuildingR3EventHooks.OnBuildStructure.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnBuildStructurePre));
            subscriptions.Add(BuildingR3EventHooks.OnBuildStructure.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnBuildStructurePost));
            subscriptions.Add(MapLoaderR3EventHooks.OnLoadSave.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnLoadSave));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnUnloadMap));

            installed = true;
            Shared.DebugLogHelper.LogInfo(
                log,
                "Native AIV castle spawner installed; all private functions and globals resolved uniquely.");
        }

        private void OnLoadSave(LoadSaveGameEventArgs args)
        {
            handledCurrentMap = true;
            aivImportedForCurrentMap = false;
            pendingAivImport = null;
            preparedAivCastle = null;
            executedAivCastle = null;
            Shared.DebugLogHelper.LogInfo(
                log,
                "Savegame load detected; native castle spawning is disabled for this map.");
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            handledCurrentMap = false;
            aivImportedForCurrentMap = false;
            pendingAivImport = null;
            preparedAivCastle = null;
            executedAivCastle = null;
            nativeCastleExecutionInProgress = false;
            nativeCastleExecutionPlayerId = 0;
            nextHovelVisualStyle = 0;
            correctedHovelVisualCount = 0;
            Shared.DebugLogHelper.LogInfo(
                log,
                "OnUnloadMap(Post) received; the next new map may spawn a native AIV castle.");
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
                $"selection='{settings.SelectedCastle}', " +
                $"preImportReady={pendingAivImport != null}, " +
                $"keepPreSpawnPrepared={preparedAivCastle != null}, " +
                $"keepPostSpawnExecuted={executedAivCastle != null}.");

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
            if (!settings.IsSpawnMode)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Native castle spawning skipped because mode is '{settings.Mode}'.");
                return;
            }

            try
            {
                EnsureSupportedGameMode(gameMode);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Game-mode guard accepted local singleplayer skirmish: " +
                    $"sharedSingleplayerSkirmish={gameMode.SharedSingleplayerSkirmish}, " +
                    $"gameDataSkirmishGameType={gameMode.GameDataSkirmishGameType}, " +
                    $"lobbySkirmishMembers={gameMode.LobbySkirmishMemberCount}, " +
                    $"lobbySkirmishHumans={gameMode.LobbySkirmishHumanCount}, " +
                    $"lobbyNetworkHumans={gameMode.LobbyNetworkHumanCount}, " +
                    $"realNetworkGameMembers={gameMode.RealNetworkGameMemberCount}.");

                bool imported = aivImportedForCurrentMap;
                aivImportedForCurrentMap = false;
                pendingAivImport = null;
                PreparedAivCastle castle = executedAivCastle;
                preparedAivCastle = null;
                executedAivCastle = null;
                if (castle == null)
                {
                    throw new InvalidOperationException(
                        !imported
                            ? "No AIV was imported during OnStartMap(Pre); native execution could not run."
                            : "The selected AIV was imported, but the local Keep BuildStructure(Post) event did not execute it.");
                }

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Native castle execution already completed inside the native map start: " +
                    $"playerId={castle.PlayerId}, specIndex={castle.SpecIndex}, " +
                    $"highestFrame={castle.HighestFrame}.");
            }
            catch (Exception ex)
            {
                // There is deliberately no managed placement fallback.
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Native AIV castle spawn failed; no fallback was attempted: {ex}");
            }
        }

        private void OnStartMapPre(MapStartEventArgs args)
        {
            aivImportedForCurrentMap = false;
            pendingAivImport = null;
            preparedAivCastle = null;
            executedAivCastle = null;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"OnStartMap(Pre) received: handledCurrentMap={handledCurrentMap}, " +
                $"selection='{settings.SelectedCastle}'.");

            if (handledCurrentMap || !settings.IsSpawnMode)
                return;

            try
            {
                GameModeSnapshot gameMode = CaptureGameMode(args);
                EnsureSupportedGameMode(gameMode);

                if (!settings.TryResolveSelectedFile(out string filePath))
                {
                    throw new FileNotFoundException(
                        $"Selected AIVJSON file is unavailable: '{settings.SelectedCastle}'.");
                }

                int nativePlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
                if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(nativePlayerId) ||
                    GamePlayerManagerAPI.Instance.IsAIPlayer(nativePlayerId))
                {
                    throw new InvalidOperationException(
                        $"No valid native local human player was found during pre-import; " +
                        $"nativePlayerId={nativePlayerId}.");
                }

                string json = File.ReadAllText(filePath);
                AivJsonDocument document = AivJsonReader.Parse(json);
                short[] rawAiv = AivRawDataEncoder.Encode(document);

                // Pre runs after Vanilla's InitAIVLoading/import loop but before the native
                // start consumes the candidate table. Importing in Post is too late.
                EngineInterface.ImportAIV(nativePlayerId - 1, 0, rawAiv, 1);
                ImportedCandidateSnapshot importedCandidates =
                    CaptureImportedCandidates(nativePlayerId - 1);
                pendingAivImport = new PendingAivImport(
                    nativePlayerId,
                    filePath,
                    new FileInfo(filePath).Length,
                    document,
                    rawAiv.Length);
                aivImportedForCurrentMap = true;

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Native AIV pre-import completed: phase=OnStartMap(Pre), " +
                    $"playerId={nativePlayerId}, playerSlot={nativePlayerId - 1}, " +
                    $"candidateId=0, custom=1, file={filePath}, " +
                    $"frames={document.frames.Count}, miscItems={document.miscItems.Count}, " +
                    $"rawShorts={rawAiv.Length}, " +
                    $"nativeCandidateCountAfterImport={importedCandidates.Count}, " +
                    $"nativeCandidate0Pointer=0x{importedCandidates.FirstPointer.ToInt64():X}, " +
                    $"nativeCandidateTable=0x{importedCandidates.TableAddress.ToInt64():X}.");
            }
            catch (Exception ex)
            {
                aivImportedForCurrentMap = false;
                pendingAivImport = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Native AIV pre-import failed; Keep-preparation and Post execution will be skipped: {ex}");
            }
        }

        private void OnBuildStructurePre(BuildStructureEventArgs args)
        {
            if (TryCorrectNativeHovelVisualStyle(args))
                return;

            PendingAivImport imported = pendingAivImport;
            if (imported == null ||
                preparedAivCastle != null ||
                args.PlayerId != imported.PlayerId ||
                !IsKeepMapper(args.Mappers))
            {
                return;
            }

            // Vanilla reaches this event immediately before placing the human Keep.
            // Preparing here lets the native fit test see an empty Keep footprint.
            pendingAivImport = null;
            try
            {
                PreparedAivCastle castle = PrepareSelectedCastle(
                    imported,
                    args.TileX,
                    args.TileY);

                args.TileX = castle.PreparedKeepX;
                args.TileY = castle.PreparedKeepY;
                args.Mappers = eMappers.MAPPER_KEEP2;
                args.BuildingScaleUnknown = 7;
                args.Unknown1 = castle.Orientation;
                args.IsFree = false;
                preparedAivCastle = castle;

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Vanilla local Keep intercepted after AIV preparation: " +
                    $"playerId={args.PlayerId}, " +
                    $"originalKeep=({castle.RequestedKeepX},{castle.RequestedKeepY}), " +
                    $"preparedKeep=({castle.PreparedKeepX},{castle.PreparedKeepY}), " +
                    $"mapper={args.Mappers}, scale={args.BuildingScaleUnknown}, " +
                    $"orientation={args.Unknown1}, isFree={args.IsFree}.");
            }
            catch (Exception ex)
            {
                preparedAivCastle = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Native AIV preparation at local Keep BuildStructure(Pre) failed; " +
                    $"the Vanilla Keep will remain unchanged and no castle fallback will run: {ex}");
            }
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
            PreparedAivCastle castle = preparedAivCastle;
            if (castle == null ||
                args.PlayerId != castle.PlayerId ||
                !IsKeepMapper(args.Mappers))
            {
                return;
            }

            // Execute while the native skirmish-start function is still running.
            // Vanilla performs its completed-AI-castle execution at this stage too,
            // before the outer map-start finalizes building tiles and visuals.
            preparedAivCastle = null;
            try
            {
                ExecutePreparedCastle(castle);
                executedAivCastle = castle;
            }
            catch (Exception ex)
            {
                executedAivCastle = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Native AIV execution at local Keep BuildStructure(Post) failed; " +
                    $"no castle fallback will run: {ex}");
            }
        }

        private PreparedAivCastle PrepareSelectedCastle(
            PendingAivImport prepared,
            int keepX,
            int keepY)
        {
            int playerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) ||
                GamePlayerManagerAPI.Instance.IsAIPlayer(playerId))
            {
                throw new InvalidOperationException(
                    $"No valid local human player was found; playerId={playerId}.");
            }
            if (playerId != prepared.PlayerId)
            {
                throw new InvalidOperationException(
                    $"Local player changed between AIV import and placement: " +
                    $"prePlayerId={prepared.PlayerId}, postPlayerId={playerId}.");
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
                $"phase=OnBuildStructure(Pre), keepReference=({keepX},{keepY}), " +
                $"file={prepared.FilePath}, jsonBytes={prepared.JsonBytes}, " +
                $"frames={prepared.Document.frames.Count}, " +
                $"miscItems={prepared.Document.miscItems.Count}, " +
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

            // Start at zero degrees and let Vanilla try the other rotations only when needed.
            setPlacement(
                aivState,
                specIndex,
                keepX,
                keepY,
                0);
            selectBestFit(aivState, specIndex, 1);

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
                ownedBuildingsBefore);
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
                referenceHashMatches: true,
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

            if (mode.SharedRealMultiplayer)
            {
                throw new NotSupportedException(
                    $"Native CastlePlanner is disabled for real multiplayer: " +
                    $"{mode.SharedModeDetails}.");
            }

            if (!mode.SharedSingleplayerSkirmish)
            {
                throw new NotSupportedException(
                    $"Native CastlePlanner requires a singleplayer skirmish: " +
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
                string filePath,
                long jsonBytes,
                AivJsonDocument document,
                int rawShortCount)
            {
                PlayerId = playerId;
                FilePath = filePath;
                JsonBytes = jsonBytes;
                Document = document;
                RawShortCount = rawShortCount;
            }

            public int PlayerId { get; }
            public string FilePath { get; }
            public long JsonBytes { get; }
            public AivJsonDocument Document { get; }
            public int RawShortCount { get; }
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
                int ownedBuildingsAtPreparation)
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
        }

    }
}
