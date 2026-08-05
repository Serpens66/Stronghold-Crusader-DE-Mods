using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;

namespace ActiveAIVDetector
{
    internal sealed unsafe class ActiveAIVDetectionRuntime
    {
        // c_game_aiv_prepare_layout function start in game version 2.7.0.1.
        // This routine is called after the engine has selected the best-fitting AIV candidate.
        private const string PrepareLayoutPattern =
            "44 89 44 24 18 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 68";

        private const int AivSpecStride = 0x6D98;
        private const int MaxAivSpecIndex = 8;
        private const int OrientationOffset = 0x0C;
        private const int CandidateIdOffset = 0x10;
        private const int PlacementStateOffset = 0x14;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PrepareLayoutDelegate(ulong aivStateAddress, int aivSpecIndex, int playerId);

        private delegate void StartSkirmishGameDelegate(
            FRONT_Multiplayer self,
            HUD_IngameMenu.RestartSkirmishMapInfo restartInfo);

        private readonly ManualLogSource log;
        private readonly OracleCellTraceOptions cellTraceOptions;
        private readonly OraclePrebuildTraceOptions prebuildTraceOptions;
        private readonly string vanillaAicDirectory;
        private readonly string vanillaAivDirectory;
        private readonly bool bundledVanillaAicMatchesInstalledGame;
        private readonly Dictionary<int, AivSelectionSnapshot> pendingSelections =
            new Dictionary<int, AivSelectionSnapshot>();
        private readonly Dictionary<int, LobbyAivSnapshot> lobbyAivSnapshots =
            new Dictionary<int, LobbyAivSnapshot>();
        private readonly List<OracleSelectionSnapshot> pendingOracleSelections =
            new List<OracleSelectionSnapshot>();
        private readonly List<OraclePrebuildFrameTraceSnapshot> pendingPrebuildFrames =
            new List<OraclePrebuildFrameTraceSnapshot>();
        private readonly HashSet<int> reportedPlayers = new HashSet<int>();
        private readonly HashSet<long> reportedOracleSelections = new HashSet<long>();
        private readonly List<IDisposable> lifecycleSubscriptions = new List<IDisposable>();
        private HookRef<X64ManagedFunctionDetourAOB<PrepareLayoutDelegate>> prepareLayoutHook =
            new HookRef<X64ManagedFunctionDetourAOB<PrepareLayoutDelegate>>();

        // Retaining the transaction keeps the native detour alive for the full process lifetime.
        private HookTransaction transaction;
        private Hook startSkirmishGameHook;
        private StartSkirmishGameDelegate startSkirmishGameTrampoline;
        private AivPlacementOracle placementOracle;
        private int detectionCount;
        private bool installed;
        private bool mapStartCompleted;
        private bool callbackFailureLogged;
        private bool lobbyCapturePending;
        private bool lobbySnapshotAppliesToCurrentMap;
        private bool preBuildCapturePending;
        private bool preBuildSettingAppliesToCurrentMap;
        private int capturedPreBuildSetting = -1;
        private int mapLoadSequence;
        private string currentMapFileName = "<unknown>";
        private string currentMapName = "<unknown>";
        private string currentMapFileSha256 = "<not-available>";

        public ActiveAIVDetectionRuntime(
            ManualLogSource log,
            OracleCellTraceOptions cellTraceOptions,
            OraclePrebuildTraceOptions prebuildTraceOptions)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.cellTraceOptions = cellTraceOptions ??
                throw new ArgumentNullException(nameof(cellTraceOptions));
            this.prebuildTraceOptions = prebuildTraceOptions ??
                throw new ArgumentNullException(nameof(prebuildTraceOptions));
            string pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            vanillaAicDirectory = Path.Combine(pluginDirectory, "VanillaAIC");
            // The game uses embedded AIV bytes; these editor files make that selection inspectable.
            vanillaAivDirectory = Path.Combine(pluginDirectory, "VanillaAIV");
            bundledVanillaAicMatchesInstalledGame = VerifyBundledVanillaAicVersion();
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Bundled vanilla AIC verification: " +
                $"matchesInstalledGame={bundledVanillaAicMatchesInstalledGame}, " +
                $"directory={vanillaAicDirectory}.");
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Bundled vanilla AIV inventory: files={CountBundledVanillaAivFiles()}, " +
                $"directory={vanillaAivDirectory}.");
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Oracle cell trace configuration: enabled={cellTraceOptions.Enabled}, " +
                $"playerId={cellTraceOptions.PlayerId}, candidateId={cellTraceOptions.CandidateId}, " +
                $"orientation={cellTraceOptions.Orientation}, " +
                $"keep=({cellTraceOptions.KeepX},{cellTraceOptions.KeepY}), " +
                $"maximumCaptures={cellTraceOptions.MaximumCaptureCount}.");
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Oracle prebuild trace configuration: enabled={prebuildTraceOptions.Enabled}, " +
                $"playerId={prebuildTraceOptions.PlayerId}, " +
                $"maximumCaptures={prebuildTraceOptions.MaximumCaptureCount}.");
        }

        public void Install(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (installed)
                return;

            InstallLobbyCaptureHook();

            transaction = new HookTransaction(
                memory,
                unchecked((ulong)libraryHandle.ToInt64()),
                loggerFactory: null,
                failureMode: TransactionFailureMode.RollbackAndThrow);

            transaction.AddDetour(
                ref prepareLayoutHook,
                PrepareLayoutPattern,
                PrepareLayout);

            placementOracle = new AivPlacementOracle(
                log,
                OnOracleSelectionCompleted,
                cellTraceOptions,
                OnPrebuildFrameCaptured,
                prebuildTraceOptions,
                libraryHandle,
                memory);
            placementOracle.RegisterHooks(transaction);

            transaction.Commit();

            if (!prepareLayoutHook.Success)
                throw new InvalidOperationException(
                    "The c_game_aiv_prepare_layout signature was not found.");
            placementOracle.ValidateHooks();

            SubscribeLifecycleHooks();
            installed = true;
            Shared.DebugLogHelper.LogInfo(
                log,
                "Native active-AIV detector and passive placement oracle installed at Info level; " +
                "Vanilla candidate tests will be joined with lobby metadata after OnStartMap(Post).");
        }

        private void InstallLobbyCaptureHook()
        {
            MethodInfo method = typeof(FRONT_Multiplayer).GetMethod(
                "StartSkirmishGame",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(HUD_IngameMenu.RestartSkirmishMapInfo) },
                null);
            if (method == null)
                throw new MissingMethodException(typeof(FRONT_Multiplayer).FullName, "StartSkirmishGame");

            startSkirmishGameHook =
                new Hook(method, (StartSkirmishGameDelegate)StartSkirmishGameHook);
            startSkirmishGameTrampoline =
                startSkirmishGameHook.GenerateTrampoline<StartSkirmishGameDelegate>();
        }

        private void StartSkirmishGameHook(
            FRONT_Multiplayer self,
            HUD_IngameMenu.RestartSkirmishMapInfo restartInfo)
        {
            try
            {
                CaptureSkirmishOptions(self);
            }
            catch (Exception ex)
            {
                capturedPreBuildSetting = -1;
                preBuildCapturePending = false;
                preBuildSettingAppliesToCurrentMap = false;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Could not capture advopt_pre_build before game start: {ex}");
            }

            try
            {
                CaptureLobbyAivMetadata(self);
            }
            catch (Exception ex)
            {
                lobbyAivSnapshots.Clear();
                lobbyCapturePending = false;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Could not capture lobby lord/AIV metadata before game start: {ex}");
            }

            startSkirmishGameTrampoline(self, restartInfo);
        }

        private void CaptureSkirmishOptions(FRONT_Multiplayer frontend)
        {
            if (ReferenceEquals(frontend, null))
                throw new ArgumentNullException(nameof(frontend));

            // Reflection avoids a compile-time dependency on the Noesis base type
            // of FRONT_Multiplayer while still reading Vanilla's real setup object.
            FieldInfo setupField = typeof(FRONT_Multiplayer).GetField(
                "MPsetupData",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (setupField == null)
                throw new MissingFieldException(typeof(FRONT_Multiplayer).FullName, "MPsetupData");
            object setup = setupField.GetValue(frontend);
            if (setup == null)
                throw new InvalidOperationException("MPsetupData is null before StartSkirmishGame.");
            FieldInfo preBuildField = setup.GetType().GetField(
                "advopt_pre_build",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (preBuildField == null)
                throw new MissingFieldException(setup.GetType().FullName, "advopt_pre_build");

            capturedPreBuildSetting = Convert.ToInt32(
                preBuildField.GetValue(setup),
                CultureInfo.InvariantCulture);
            preBuildCapturePending = capturedPreBuildSetting >= 0;
            preBuildSettingAppliesToCurrentMap = false;

            // Capture before Vanilla transfers the lobby structure into the native globals.
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Captured skirmish option before StartSkirmishGame: " +
                $"advopt_pre_build={capturedPreBuildSetting}, " +
                $"mode={DescribePreBuildSetting(capturedPreBuildSetting)}.");
        }

        private void CaptureLobbyAivMetadata(FRONT_Multiplayer frontend)
        {
            lobbyAivSnapshots.Clear();
            lobbyCapturePending = false;

            if (frontend?.currentLobby?.members == null || frontend.AIVs == null)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "No lobby lord/AIV metadata was available before StartSkirmishGame.");
                return;
            }

            foreach (Platform_Multiplayer.MPLobbyMember member in frontend.currentLobby.members)
            {
                if (member == null || !member.SkirmishMember || member.SkirmishHumanMember)
                    continue;

                int playerId =
                    frontend.currentLobby.getThisPlayerFromSteamID(member.GetSteamID());
                if (playerId < 1 || playerId > frontend.AIVs.Length)
                    continue;

                FRONT_Multiplayer.MPAIVInfo info = frontend.AIVs[playerId - 1];
                lobbyAivSnapshots[playerId] = LobbyAivSnapshot.Create(
                    playerId,
                    member.GetLordType(),
                    member.customLordName,
                    info);
            }

            lobbyCapturePending = lobbyAivSnapshots.Count > 0;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Captured lord/AIV metadata for {lobbyAivSnapshots.Count} AI lobby slots " +
                "before StartSkirmishGame.");
        }

        private void PrepareLayout(ulong aivStateAddress, int aivSpecIndex, int playerId)
        {
            // Vanilla consumes this AIVSpec only after a successful placement result.
            prepareLayoutHook.Value.Hook.Trampoline(
                aivStateAddress,
                aivSpecIndex,
                playerId);

            try
            {
                if (aivStateAddress == 0)
                    throw new InvalidOperationException("The native AIV state pointer is null.");

                if (aivSpecIndex < 0 || aivSpecIndex > MaxAivSpecIndex)
                    throw new InvalidOperationException(
                        $"The native AIVSpec index {aivSpecIndex} is outside 0..{MaxAivSpecIndex}.");

                byte* aivSpec = (byte*)aivStateAddress + (aivSpecIndex * AivSpecStride);
                int orientation = *(int*)(aivSpec + OrientationOffset);
                int candidateId = *(int*)(aivSpec + CandidateIdOffset);
                int placementState = *(int*)(aivSpec + PlacementStateOffset);

                if (!IsKnownOrientation(orientation))
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Active AIV candidate for playerId={playerId} returned " +
                        $"unexpected orientation={orientation}; it will not be reported as final.");
                    return;
                }

                if (placementState != 1 && placementState != 2)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Active AIV candidate for playerId={playerId} has non-final " +
                        $"placementState={placementState}; waiting for a finalized selection.");
                    return;
                }

                AivSelectionSnapshot snapshot = new AivSelectionSnapshot(
                    playerId,
                    aivSpecIndex,
                    orientation,
                    candidateId,
                    placementState);

                // Repeated native callbacks before map start replace the earlier candidate.
                pendingSelections[playerId] = snapshot;

                if (mapStartCompleted)
                    ReportSelectionIfActiveAI(snapshot, "native callback after OnStartMap(Post)");
            }
            catch (Exception ex)
            {
                if (callbackFailureLogged)
                    return;

                callbackFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Active AIV native callback failed; further identical errors are suppressed: {ex}");
            }
        }

        private void SubscribeLifecycleHooks()
        {
            lifecycleSubscriptions.Add(MapLoaderR3EventHooks.OnLoadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnMapLoadStarted));

            lifecycleSubscriptions.Add(MapLoaderR3EventHooks.OnLoadSave.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(_ => ResetForMapTransition("save load")));

            lifecycleSubscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => ResetForMapTransition("map unload")));

            lifecycleSubscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => OnMapStarted()));
        }

        private void ResetForMapTransition(string reason)
        {
            mapStartCompleted = false;
            pendingSelections.Clear();
            pendingOracleSelections.Clear();
            pendingPrebuildFrames.Clear();
            reportedPlayers.Clear();
            reportedOracleSelections.Clear();
            callbackFailureLogged = false;

            // StartSkirmishGame captures metadata before the game's repeated unload/load callbacks.
            if (!lobbyCapturePending)
            {
                lobbyAivSnapshots.Clear();
                lobbySnapshotAppliesToCurrentMap = false;
            }
            if (!preBuildCapturePending)
            {
                capturedPreBuildSetting = -1;
                preBuildSettingAppliesToCurrentMap = false;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Active AIV detector reset for {reason}; waiting for finalized AI selections. " +
                $"retainedPendingLobbyMetadata={lobbyCapturePending}, " +
                $"retainedPendingPreBuildSetting={preBuildCapturePending}.");
        }

        private void OnMapLoadStarted(MapLoadEventArgs args)
        {
            ResetForMapTransition("map load");
            mapLoadSequence++;
            currentMapFileName = string.IsNullOrEmpty(args.FileName)
                ? "<unknown>"
                : args.FileName;
            currentMapName = string.IsNullOrEmpty(args.MapName)
                ? "<unknown>"
                : args.MapName;
            // Hash once per load so every Oracle row identifies the exact same map bytes.
            currentMapFileSha256 = ComputeFileSha256(currentMapFileName);
        }

        private void OnOracleSelectionCompleted(OracleSelectionSnapshot snapshot)
        {
            pendingOracleSelections.Add(snapshot);
            if (mapStartCompleted)
                ReportOracleSelection(snapshot, "native callback after OnStartMap(Post)");
        }

        private void OnPrebuildFrameCaptured(OraclePrebuildFrameTraceSnapshot snapshot)
        {
            // ExecuteBuildStep runs inside map start; defer file I/O until Vanilla returns.
            pendingPrebuildFrames.Add(snapshot);
        }

        private void OnMapStarted()
        {
            // OnStartMap(Post) runs after the game's complete native map-start routine.
            mapStartCompleted = true;
            if (lobbyCapturePending)
            {
                lobbySnapshotAppliesToCurrentMap = true;
                lobbyCapturePending = false;
            }
            if (preBuildCapturePending)
            {
                preBuildSettingAppliesToCurrentMap = true;
                preBuildCapturePending = false;
            }

            WritePendingOraclePrebuildTraces();

            pendingOracleSelections.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
            foreach (OracleSelectionSnapshot oracleSelection in pendingOracleSelections)
                ReportOracleSelection(oracleSelection, "OnStartMap(Post)");

            List<int> playerIds = new List<int>(pendingSelections.Keys);
            playerIds.Sort();

            int activeAiCount = 0;
            int reportedAiCount = 0;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;

            for (int playerId = 1; playerId <= GamePlayerManagerAPI.MAX_PLAYERS; playerId++)
            {
                if (!playerApi.IsAIPlayer(playerId))
                    continue;

                activeAiCount++;
                if (pendingSelections.TryGetValue(playerId, out AivSelectionSnapshot snapshot) &&
                    ReportSelectionIfActiveAI(snapshot, "OnStartMap(Post)"))
                {
                    reportedAiCount++;
                }
                else
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"No finalized AIV selection was captured for active AI playerId={playerId}.");
                }
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Active AIV finalization completed after OnStartMap(Post): " +
                $"activeAIs={activeAiCount}, reportedAIVs={reportedAiCount}, " +
                $"capturedSelections={playerIds.Count}.");
        }

        private void ReportOracleSelection(
            OracleSelectionSnapshot snapshot,
            string confirmationPoint)
        {
            if (!reportedOracleSelections.Add(snapshot.Sequence))
                return;

            LobbyAivSnapshot lobbySnapshot = null;
            if (lobbySnapshotAppliesToCurrentMap)
                lobbyAivSnapshots.TryGetValue(snapshot.PlayerId, out lobbySnapshot);

            ResolvedAivSource finalSource = ResolveOracleSource(
                lobbySnapshot,
                snapshot.PlayerId,
                snapshot.FinalCandidateId);
            string directReturn = snapshot.DirectReturnSigned.HasValue
                ? snapshot.DirectReturnSigned.Value.ToString()
                : "<not-applicable>";

            Shared.DebugLogHelper.LogInfo(
                log,
                $"AIV placement oracle selection #{snapshot.Sequence}: " +
                $"mapName={currentMapName}, mapFile={currentMapFileName}, " +
                $"mapFileSha256={currentMapFileSha256}, " +
                $"preBuildSetting={CurrentPreBuildSetting}, " +
                $"playerId={snapshot.PlayerId}, method={snapshot.Method}, " +
                $"tryOtherRotations={snapshot.TryOtherRotations}, " +
                $"aivSpecIndex={snapshot.AivSpecIndex}, attempts={snapshot.Attempts.Count}, " +
                $"finalCandidateId={snapshot.FinalCandidateId}, finalAivName={finalSource.Name}, " +
                $"finalAivJson={finalSource.JsonPath}, " +
                $"finalAivJsonSha256={ComputeFileSha256(finalSource.JsonPath)}, " +
                $"finalOrientation={snapshot.FinalOrientation} " +
                $"({DescribeOrientation(snapshot.FinalOrientation)}), " +
                $"placementState={snapshot.PlacementState} " +
                $"({DescribePlacementState(snapshot.PlacementState)}), " +
                $"directReturnSigned={directReturn}, confirmationPoint={confirmationPoint}.");

            foreach (OracleAttemptSnapshot attempt in snapshot.Attempts)
            {
                ResolvedAivSource source = ResolveOracleSource(
                    lobbySnapshot,
                    snapshot.PlayerId,
                    attempt.CandidateId);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"AIV placement oracle attempt #{snapshot.Sequence}.{attempt.AttemptNumber}: " +
                    $"mapName={currentMapName}, mapFile={currentMapFileName}, " +
                    $"mapFileSha256={currentMapFileSha256}, " +
                    $"preBuildSetting={CurrentPreBuildSetting}, " +
                    $"playerId={snapshot.PlayerId}, method={snapshot.Method}, " +
                    $"candidateId={attempt.CandidateId}, aivName={source.Name}, " +
                    $"aivJson={source.JsonPath}, " +
                    $"aivJsonSha256={ComputeFileSha256(source.JsonPath)}, " +
                    $"orientation={attempt.Orientation} " +
                    $"({DescribeOrientation(attempt.Orientation)}), " +
                    $"result={attempt.ResultKind}, rawFitScore={attempt.RawFitScore}, " +
                    $"fitPercent={attempt.FitPercent}, evaluatedCells={attempt.EvaluatedCells}, " +
                    $"blockedCells={attempt.BlockedCells}, " +
                    $"origin=({attempt.OriginX},{attempt.OriginY}), " +
                    $"keepReference=({attempt.KeepX},{attempt.KeepY}).");

                if (attempt.CellTrace != null)
                {
                    WriteOracleCellTrace(snapshot, attempt, source);
                    if (attempt.CellTrace.LiveBuildingTiles.Count != 0)
                        WriteOracleLiveBuildingGrid(snapshot, attempt);
                }
            }
        }

        private void WriteOracleCellTrace(
            OracleSelectionSnapshot selection,
            OracleAttemptSnapshot attempt,
            ResolvedAivSource source)
        {
            try
            {
                OracleCellTraceSnapshot trace = attempt.CellTrace;
                Directory.CreateDirectory(cellTraceOptions.OutputDirectory);
                string fileName = string.Format(
                    CultureInfo.InvariantCulture,
                    "oracle-cell-trace-{0}-p{1}-c{2}-r{3}-keep{4}-{5}.tsv",
                    trace.CapturedAtLocal.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture),
                    selection.PlayerId,
                    attempt.CandidateId,
                    attempt.Orientation,
                    attempt.KeepX,
                    attempt.KeepY);
                string path = Path.Combine(cellTraceOptions.OutputDirectory, fileName);

                using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
                {
                    writer.NewLine = "\r\n";
                    writer.WriteLine($"# capturedAtLocal={trace.CapturedAtLocal:O}");
                    writer.WriteLine($"# mapName={currentMapName}");
                    writer.WriteLine($"# mapFile={currentMapFileName}");
                    writer.WriteLine($"# mapFileSha256={currentMapFileSha256}");
                    writer.WriteLine($"# preBuildSetting={CurrentPreBuildSetting}");
                    writer.WriteLine($"# playerId={selection.PlayerId}");
                    writer.WriteLine($"# candidateId={attempt.CandidateId}");
                    writer.WriteLine($"# aivName={source.Name}");
                    writer.WriteLine($"# aivJson={source.JsonPath}");
                    writer.WriteLine($"# aivJsonSha256={ComputeFileSha256(source.JsonPath)}");
                    writer.WriteLine($"# orientation={attempt.Orientation}");
                    writer.WriteLine($"# originX={attempt.OriginX}");
                    writer.WriteLine($"# originY={attempt.OriginY}");
                    writer.WriteLine($"# keepX={attempt.KeepX}");
                    writer.WriteLine($"# keepY={attempt.KeepY}");
                    writer.WriteLine($"# evaluatedCells={trace.EvaluatedCells}");
                    writer.WriteLine($"# nativeBlockedCells={trace.NativeBlockedCells}");
                    writer.WriteLine($"# resultGridBlockedCells={trace.ResultGridBlockedCells}");
                    int validatorBlockedCells = 0;
                    foreach (OracleValidatorCallEntry call in trace.ValidatorCalls)
                    {
                        if (call.Result != 0)
                            validatorBlockedCells++;
                    }
                    writer.WriteLine($"# validatorCalls={trace.ValidatorCalls.Count}");
                    writer.WriteLine($"# validatorBlockedCells={validatorBlockedCells}");
                    writer.WriteLine(
                        "gridRow\tgridColumn\tworldX\tworldY\trawMapper\t" +
                        "effectiveMapper\tscoreGridValue\tresultGridValue\tblocked");

                    foreach (OracleCellTraceEntry cell in trace.Cells)
                    {
                        writer.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}",
                            cell.GridRow,
                            cell.GridColumn,
                            cell.WorldX,
                            cell.WorldY,
                            cell.RawMapper,
                            cell.EffectiveMapper,
                            cell.ScoreGridValue,
                            cell.ResultGridValue,
                            cell.Blocked));
                    }

                    writer.WriteLine();
                    writer.WriteLine("# validator calls captured only inside the filtered fit window");
                    writer.WriteLine(
                        "validatorCallIndex\ttileId\tplayerId\tmapperValue\tmode\tresult\tblocked\t" +
                        "nativeTerrainFlags\tnativeHeight\tnativeDefaultHeight\t" +
                        "nativeOrganismId\tnativeOrganismClass\tnativeBuildingId\t" +
                        "nativeEntityId\tnativeOwnerId\tnativeGameMode");
                    foreach (OracleValidatorCallEntry call in trace.ValidatorCalls)
                    {
                        writer.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t" +
                            "{10}\t{11}\t{12}\t{13}\t{14}\t{15}",
                            call.CallIndex,
                            call.TileId,
                            call.PlayerId,
                            call.MapperValue,
                            call.Mode,
                            call.Result,
                            call.Result != 0,
                            call.NativeTerrainFlags,
                            call.NativeHeight,
                            call.NativeDefaultHeight,
                            call.NativeOrganismId,
                            call.NativeOrganismClass,
                            call.NativeBuildingId,
                            call.NativeEntityId,
                            call.NativeOwnerId,
                            call.NativeGameMode));
                    }
                }

                int tracedValidatorBlockedCells = 0;
                foreach (OracleValidatorCallEntry call in trace.ValidatorCalls)
                {
                    if (call.Result != 0)
                        tracedValidatorBlockedCells++;
                }
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Wrote opt-in AIV cell trace: path={path}, " +
                    $"rows={trace.Cells.Count}, nativeBlocked={trace.NativeBlockedCells}, " +
                    $"resultGridBlocked={trace.ResultGridBlockedCells}, " +
                    $"validatorCalls={trace.ValidatorCalls.Count}, " +
                    $"validatorBlocked={tracedValidatorBlockedCells}.");
                if (trace.Cells.Count != trace.EvaluatedCells ||
                    tracedValidatorBlockedCells != trace.NativeBlockedCells)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"AIV cell trace grid validation differs from native counters: " +
                        $"rows={trace.Cells.Count}/{trace.EvaluatedCells}, " +
                        $"validatorBlocked={tracedValidatorBlockedCells}/" +
                        $"{trace.NativeBlockedCells}.");
                }
            }
            catch (Exception ex)
            {
                // Diagnostics must never disturb Vanilla's already completed candidate test.
                Shared.DebugLogHelper.LogError(log, $"Writing the opt-in AIV cell trace failed: {ex}");
            }
        }

        private void WriteOracleLiveBuildingGrid(
            OracleSelectionSnapshot selection,
            OracleAttemptSnapshot attempt)
        {
            try
            {
                OracleCellTraceSnapshot trace = attempt.CellTrace;
                Directory.CreateDirectory(cellTraceOptions.OutputDirectory);
                string fileName = string.Format(
                    CultureInfo.InvariantCulture,
                    "oracle-live-building-grid-{0}-p{1}-r{2}-keep{3}-{4}.tsv",
                    trace.CapturedAtLocal.ToString(
                        "yyyyMMdd-HHmmss-fff",
                        CultureInfo.InvariantCulture),
                    selection.PlayerId,
                    attempt.Orientation,
                    attempt.KeepX,
                    attempt.KeepY);
                string path = Path.Combine(cellTraceOptions.OutputDirectory, fileName);

                using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
                {
                    writer.NewLine = "\r\n";
                    writer.WriteLine($"# capturedAtLocal={trace.CapturedAtLocal:O}");
                    writer.WriteLine($"# mapName={currentMapName}");
                    writer.WriteLine($"# mapFile={currentMapFileName}");
                    writer.WriteLine($"# mapFileSha256={currentMapFileSha256}");
                    writer.WriteLine($"# preBuildSetting={CurrentPreBuildSetting}");
                    writer.WriteLine($"# playerId={selection.PlayerId}");
                    writer.WriteLine($"# orientation={attempt.Orientation}");
                    writer.WriteLine($"# keepX={attempt.KeepX}");
                    writer.WriteLine($"# keepY={attempt.KeepY}");
                    writer.WriteLine($"# occupiedCells={trace.LiveBuildingTiles.Count}");
                    writer.WriteLine("tileId\tbuildingId\townerId\tterrainFlags");
                    foreach (OracleLiveBuildingTileEntry tile in trace.LiveBuildingTiles)
                    {
                        writer.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "{0}\t{1}\t{2}\t{3}",
                            tile.TileId,
                            tile.BuildingId,
                            tile.OwnerId,
                            tile.TerrainFlags));
                    }
                }

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Wrote opt-in live building grid: path={path}, " +
                    $"occupiedCells={trace.LiveBuildingTiles.Count}.");
            }
            catch (Exception ex)
            {
                // The snapshot is diagnostic only and must not affect Vanilla selection.
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Writing the opt-in live building grid failed: {ex}");
            }
        }

        private void WritePendingOraclePrebuildTraces()
        {
            if (pendingPrebuildFrames.Count == 0)
                return;

            var framesByCapture = new Dictionary<int, List<OraclePrebuildFrameTraceSnapshot>>();
            foreach (OraclePrebuildFrameTraceSnapshot frame in pendingPrebuildFrames)
            {
                if (!framesByCapture.TryGetValue(
                        frame.CaptureSequence,
                        out List<OraclePrebuildFrameTraceSnapshot> frames))
                {
                    frames = new List<OraclePrebuildFrameTraceSnapshot>();
                    framesByCapture.Add(frame.CaptureSequence, frames);
                }
                frames.Add(frame);
            }

            var captureSequences = new List<int>(framesByCapture.Keys);
            captureSequences.Sort();
            foreach (int captureSequence in captureSequences)
            {
                List<OraclePrebuildFrameTraceSnapshot> frames =
                    framesByCapture[captureSequence];
                frames.Sort((left, right) =>
                    left.CaptureFrameNumber.CompareTo(right.CaptureFrameNumber));
                WriteOraclePrebuildTrace(captureSequence, frames);
            }
        }

        private void WriteOraclePrebuildTrace(
            int captureSequence,
            IReadOnlyList<OraclePrebuildFrameTraceSnapshot> frames)
        {
            try
            {
                if (frames.Count == 0)
                    return;

                OraclePrebuildFrameTraceSnapshot first = frames[0];
                Directory.CreateDirectory(prebuildTraceOptions.OutputDirectory);
                string fileName = string.Format(
                    CultureInfo.InvariantCulture,
                    "oracle-prebuild-trace-{0}-session{1:D3}-p{2}-capture{3:D2}.tsv",
                    first.StartedAtLocal.ToString(
                        "yyyyMMdd-HHmmss-fff",
                        CultureInfo.InvariantCulture),
                    mapLoadSequence,
                    first.PlayerId,
                    captureSequence);
                string path = Path.Combine(prebuildTraceOptions.OutputDirectory, fileName);

                int totalAdded = 0;
                int totalRemoved = 0;
                int totalReplaced = 0;
                int pointerProblemFrames = 0;
                int errorFrames = 0;
                int highlightedFrames = 0;
                foreach (OraclePrebuildFrameTraceSnapshot frame in frames)
                {
                    totalAdded += frame.AddedCount;
                    totalRemoved += frame.RemovedCount;
                    totalReplaced += frame.ReplacedCount;
                    if (!frame.PlacementStatePointerConsistent)
                        pointerProblemFrames++;
                    if (!string.IsNullOrEmpty(frame.CaptureError))
                        errorFrames++;
                    if (frame.IsHighlightedMapper)
                        highlightedFrames++;
                }

                using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
                {
                    writer.NewLine = "\r\n";
                    writer.WriteLine($"# capturedAtLocal={first.StartedAtLocal:O}");
                    writer.WriteLine($"# mapLoadSequence={mapLoadSequence}");
                    writer.WriteLine($"# mapName={currentMapName}");
                    writer.WriteLine($"# mapFile={currentMapFileName}");
                    writer.WriteLine($"# mapFileSha256={currentMapFileSha256}");
                    writer.WriteLine($"# preBuildSetting={CurrentPreBuildSetting}");
                    writer.WriteLine($"# captureSequence={captureSequence}");
                    writer.WriteLine($"# playerId={first.PlayerId}");
                    writer.WriteLine($"# frameCount={frames.Count}");
                    writer.WriteLine($"# pointerProblemFrames={pointerProblemFrames}");
                    writer.WriteLine($"# captureErrorFrames={errorFrames}");
                    writer.WriteLine($"# highlightedMapperFrames={highlightedFrames}");
                    writer.WriteLine(
                        "captureFrameNumber\tstartedAtLocal\tcompletedAtLocal\t" +
                        "selectionSequence\tframeIndex\tactiveLayoutIndex\tmapper\t" +
                        "highlightedMapper\tstatus\thelper\tpositionCount\t" +
                        "firstPositionIndex\trestrictedMode\tfreeOrForced\treturnValue\t" +
                        "placementStateAddress\tpointerConsistent\tadded\tremoved\t" +
                        "replaced\tchanged\tcaptureError");

                    foreach (OraclePrebuildFrameTraceSnapshot frame in frames)
                    {
                        writer.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "{0}\t{1:O}\t{2:O}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t" +
                            "{10}\t{11}\t{12}\t{13}\t{14}\t0x{15:X}\t{16}\t{17}\t" +
                            "{18}\t{19}\t{20}\t{21}",
                            frame.CaptureFrameNumber,
                            frame.StartedAtLocal,
                            frame.CompletedAtLocal,
                            frame.SelectionSequence,
                            frame.FrameIndex,
                            frame.ActiveLayoutIndex,
                            frame.Mapper,
                            frame.IsHighlightedMapper,
                            frame.Status,
                            frame.Helper,
                            frame.PositionCount,
                            frame.FirstPositionIndex,
                            frame.RestrictedMode,
                            frame.FreeOrForced,
                            frame.ReturnValue,
                            frame.PlacementStateAddress,
                            frame.PlacementStatePointerConsistent,
                            frame.AddedCount,
                            frame.RemovedCount,
                            frame.ReplacedCount,
                            frame.Changes.Count,
                            SanitizeTsv(frame.CaptureError)));
                    }

                    writer.WriteLine();
                    writer.WriteLine("# synchronous BuildingId-grid changes per ExecuteBuildStep frame");
                    writer.WriteLine(
                        "captureFrameNumber\tframeIndex\tmapper\ttileId\tbeforeId\tafterId\tchangeKind");
                    foreach (OraclePrebuildFrameTraceSnapshot frame in frames)
                    {
                        foreach (OraclePrebuildBuildingGridChange change in frame.Changes)
                        {
                            writer.WriteLine(string.Format(
                                CultureInfo.InvariantCulture,
                                "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}",
                                frame.CaptureFrameNumber,
                                frame.FrameIndex,
                                frame.Mapper,
                                change.TileId,
                                change.BeforeId,
                                change.AfterId,
                                change.Kind));
                        }
                    }
                }

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Wrote opt-in Oracle prebuild trace: path={path}, " +
                    $"mapLoadSequence={mapLoadSequence}, playerId={first.PlayerId}, " +
                    $"frames={frames.Count}, added={totalAdded}, removed={totalRemoved}, " +
                    $"replaced={totalReplaced}, pointerProblemFrames={pointerProblemFrames}, " +
                    $"captureErrorFrames={errorFrames}.");
                foreach (OraclePrebuildFrameTraceSnapshot frame in frames)
                {
                    if (!frame.IsHighlightedMapper)
                        continue;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Oracle prebuild highlighted mapper frame: playerId={frame.PlayerId}, " +
                        $"frameIndex={frame.FrameIndex}, mapper={frame.Mapper}, " +
                        $"returnValue={frame.ReturnValue}, added={frame.AddedCount}, " +
                        $"removed={frame.RemovedCount}, replaced={frame.ReplacedCount}, " +
                        $"pointerConsistent={frame.PlacementStatePointerConsistent}.");
                }
            }
            catch (Exception ex)
            {
                // File output is deferred, but it remains diagnostic-only.
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Writing the opt-in Oracle prebuild trace failed: {ex}");
            }
        }

        private static string SanitizeTsv(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }

        private ResolvedAivSource ResolveOracleSource(
            LobbyAivSnapshot lobbySnapshot,
            int playerId,
            int candidateId)
        {
            try
            {
                Enums.AILords lord = GamePlayerManagerAPI.Instance.GetAILord(playerId);
                return ResolveAivSource(lobbySnapshot, lord, candidateId);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Oracle AIV source resolution failed for playerId={playerId}, " +
                    $"candidateId={candidateId}: {ex.Message}");
                return ResolvedAivSource.Unknown("oracle source resolution failed");
            }
        }

        private bool ReportSelectionIfActiveAI(AivSelectionSnapshot snapshot, string confirmationPoint)
        {
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            if (!playerApi.IsPlayerIdValid(snapshot.PlayerId) ||
                !playerApi.IsAIPlayer(snapshot.PlayerId) ||
                !reportedPlayers.Add(snapshot.PlayerId))
            {
                return false;
            }

            int currentDetection = ++detectionCount;
            Enums.AILords lord = playerApi.GetAILord(snapshot.PlayerId);
            LobbyAivSnapshot lobbySnapshot = null;
            if (lobbySnapshotAppliesToCurrentMap)
                lobbyAivSnapshots.TryGetValue(snapshot.PlayerId, out lobbySnapshot);

            ResolvedAivSource source;
            try
            {
                source = ResolveAivSource(lobbySnapshot, lord, snapshot.CandidateId);
            }
            catch (Exception ex)
            {
                source = ResolvedAivSource.Unknown("source resolution failed");
                Shared.DebugLogHelper.LogError(
                    log,
                    $"AIV source resolution failed for playerId={snapshot.PlayerId}, " +
                    $"lord={lord}, candidateId={snapshot.CandidateId}; " +
                    $"the finalized native selection will still be reported: {ex}");
            }
            Enums.AILords baseLord = lobbySnapshot == null
                ? lord
                : ToLordEnum(lobbySnapshot.LordType, lord);
            string lordName =
                lobbySnapshot != null && !string.IsNullOrEmpty(lobbySnapshot.CustomLordName)
                    ? lobbySnapshot.CustomLordName
                    : DescribeLord(baseLord);
            ResolvedLordConfig lordConfig = ResolveLordConfig(lobbySnapshot, baseLord);
            string lordConfigChecksum =
                lobbySnapshot == null || lobbySnapshot.LordConfigChecksum == 0
                    ? "<not-applicable>"
                    : lobbySnapshot.LordConfigChecksum.ToString();

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Active AIV confirmed #{currentDetection}: " +
                $"playerId={snapshot.PlayerId}, lord={lordName}, " +
                $"baseLordEnum={baseLord}, runtimeLordEnum={lord}, " +
                $"lordJson={lordConfig.JsonPath}, lordConfigChecksum={lordConfigChecksum}, " +
                $"lordJsonSha256={lordConfig.FileSha256}, " +
                $"lordConfigEffectiveSource={lordConfig.EffectiveSource}, " +
                $"aivMode={source.Mode}, candidateId={snapshot.CandidateId}, " +
                $"aivName={source.Name}, aivJson={source.JsonPath}, " +
                $"aivJsonSha256={ComputeFileSha256(source.JsonPath)}, " +
                $"checksum={source.Checksum}, effectiveSource={source.EffectiveSource}, " +
                $"aivSpecIndex={snapshot.AivSpecIndex}, " +
                $"orientation={snapshot.Orientation} " +
                $"({DescribeOrientation(snapshot.Orientation)}), " +
                $"placementState={snapshot.PlacementState} " +
                $"({DescribePlacementState(snapshot.PlacementState)}), " +
                $"confirmationPoint={confirmationPoint}.");
            return true;
        }

        private ResolvedLordConfig ResolveLordConfig(
            LobbyAivSnapshot lobby,
            Enums.AILords baseLord)
        {
            if (lobby == null)
                return ResolvedLordConfig.Unknown("lobby metadata unavailable");

            if (!lobby.BuiltInLord)
            {
                string customPath = lobby.GetCustomLordConfigJsonPath();
                return new ResolvedLordConfig(
                    customPath,
                    ComputeFileSha256(customPath),
                    "custom lordjson file selected by the lobby");
            }

            string fileName = GetLordJsonFileName(baseLord);
            string vanillaPath = Path.Combine(vanillaAicDirectory, fileName);
            if (!File.Exists(vanillaPath))
            {
                return new ResolvedLordConfig(
                    fileName,
                    "<not-available>",
                    "embedded game data; lordJson is the equivalent export filename, but the file is not bundled");
            }

            return new ResolvedLordConfig(
                vanillaPath,
                ComputeFileSha256(vanillaPath),
                bundledVanillaAicMatchesInstalledGame
                    ? "embedded game data; bundled lordJson is a verified equivalent export and is not read by the game"
                    : "embedded game data; bundled lordJson exists but its manifest does not match the installed CrusaderDE.dll");
        }

        private bool VerifyBundledVanillaAicVersion()
        {
            try
            {
                string manifestPath = Path.Combine(vanillaAicDirectory, "manifest.json");
                string nativeLibraryPath = Path.Combine(
                    BepInEx.Paths.GameRootPath,
                    "Stronghold Crusader Definitive Edition_Data",
                    "Plugins",
                    "x86_64",
                    "CrusaderDE.dll");
                if (!File.Exists(manifestPath) || !File.Exists(nativeLibraryPath))
                    return false;

                Match match = Regex.Match(
                    File.ReadAllText(manifestPath),
                    "\"nativeLibrarySha256\"\\s*:\\s*\"(?<hash>[0-9a-fA-F]{64})\"");
                if (!match.Success)
                    return false;

                return string.Equals(
                    match.Groups["hash"].Value,
                    ComputeFileSha256(nativeLibraryPath),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Could not verify bundled vanilla lordjson files against the installed game: {ex.Message}");
                return false;
            }
        }

        private static string ComputeFileSha256(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return "<not-available>";

            try
            {
                using (FileStream stream = File.OpenRead(path))
                using (SHA256 sha256 = SHA256.Create())
                {
                    return BitConverter.ToString(sha256.ComputeHash(stream))
                        .Replace("-", string.Empty)
                        .ToLowerInvariant();
                }
            }
            catch
            {
                return "<not-available>";
            }
        }

        private ResolvedAivSource ResolveAivSource(
            LobbyAivSnapshot lobby,
            Enums.AILords runtimeLord,
            int candidateId)
        {
            if (lobby == null)
                return ResolvedAivSource.Unknown("lobby metadata unavailable");

            if (lobby.Mode == AivMode.Custom)
            {
                if (candidateId < 0 || candidateId >= lobby.Candidates.Count)
                {
                    return ResolvedAivSource.Unknown(
                        $"custom candidate outside captured list 0..{lobby.Candidates.Count - 1}");
                }

                LobbyAivCandidate candidate = lobby.Candidates[candidateId];
                if (candidate.BuiltIn &&
                    TryDecodeBuiltInChecksum(
                        candidate.Checksum,
                        out AivMode builtInMode,
                        out int sourceIndex))
                {
                    Enums.AILords sourceLord =
                        ToLordEnum(candidate.LordType, runtimeLord);
                    return ResolveBuiltInSource(
                        builtInMode,
                        sourceLord,
                        sourceIndex,
                        candidate.Name,
                        candidate.Checksum);
                }

                string fileName = EnsureAivJsonExtension(candidate.Name);
                string path = string.IsNullOrEmpty(candidate.Path)
                    ? fileName
                    : Path.Combine(candidate.Path, fileName);
                return new ResolvedAivSource(
                    AivMode.Custom,
                    candidate.Name,
                    path,
                    candidate.Checksum.ToString(),
                    string.IsNullOrEmpty(candidate.Path)
                        ? "custom AIV transmitted without local path"
                        : "custom AIV file");
            }

            Enums.AILords lobbyLord = ToLordEnum(lobby.LordType, runtimeLord);
            return ResolveBuiltInSource(
                lobby.Mode,
                lobbyLord,
                candidateId,
                string.Empty,
                0);
        }

        private ResolvedAivSource ResolveBuiltInSource(
            AivMode mode,
            Enums.AILords lord,
            int sourceIndex,
            string capturedName,
            ulong checksum)
        {
            string overrideAsset = $"AIV/{lord}_{sourceIndex}.aivjson";
            if (HasAssetOverride(overrideAsset))
            {
                return new ResolvedAivSource(
                    mode,
                    string.IsNullOrEmpty(capturedName) ? overrideAsset : capturedName,
                    overrideAsset,
                    checksum == 0 ? string.Empty : checksum.ToString(),
                    "Script Extender asset override");
            }

            string stem = GetAivFileStem(lord);
            string fileName;
            switch (mode)
            {
                case AivMode.Community:
                    fileName = $"Community_{stem}{sourceIndex + 1}.aivjson";
                    break;
                case AivMode.Historical:
                    fileName = $"Community_Historical_{stem}.aivjson";
                    break;
                default:
                    fileName = $"{stem}{sourceIndex + 1}.aivjson";
                    break;
            }

            return new ResolvedAivSource(
                mode,
                string.IsNullOrEmpty(capturedName)
                    ? Path.GetFileNameWithoutExtension(fileName)
                    : capturedName,
                ResolveBundledVanillaAivPath(fileName),
                checksum == 0 ? string.Empty : checksum.ToString(),
                File.Exists(Path.Combine(vanillaAivDirectory, fileName))
                    ? "embedded game data; bundled aivJson is the equivalent official editor file and is not read by the game"
                    : "embedded game data; aivJson is the equivalent editor filename, but the file is not bundled");
        }

        private string ResolveBundledVanillaAivPath(string fileName)
        {
            string path = Path.Combine(vanillaAivDirectory, fileName);
            return File.Exists(path) ? path : fileName;
        }

        private int CountBundledVanillaAivFiles()
        {
            try
            {
                return Directory.Exists(vanillaAivDirectory)
                    ? Directory.GetFiles(vanillaAivDirectory, "*.aivjson").Length
                    : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static bool TryDecodeBuiltInChecksum(
            ulong checksum,
            out AivMode mode,
            out int sourceIndex)
        {
            if (checksum >= 1 && checksum <= 8)
            {
                mode = AivMode.Default;
                sourceIndex = (int)checksum - 1;
                return true;
            }

            if (checksum >= 51 && checksum <= 58)
            {
                mode = AivMode.Community;
                sourceIndex = (int)checksum - 51;
                return true;
            }

            if (checksum == 61)
            {
                mode = AivMode.Historical;
                sourceIndex = 0;
                return true;
            }

            mode = AivMode.Unknown;
            sourceIndex = -1;
            return false;
        }

        private static Enums.AILords ToLordEnum(
            int zeroBasedLordType,
            Enums.AILords fallback)
        {
            int enumValue = zeroBasedLordType + 1;
            return Enum.IsDefined(typeof(Enums.AILords), enumValue)
                ? (Enums.AILords)enumValue
                : fallback;
        }

        private static string DescribeLord(Enums.AILords lord)
        {
            string name = lord.ToString();
            return name.StartsWith("SK_", StringComparison.Ordinal)
                ? name.Substring(3)
                : name;
        }

        private static string GetAivFileStem(Enums.AILords lord)
        {
            switch (lord)
            {
                case Enums.AILords.SK_PHILLIP:
                    return "philip";
                case Enums.AILords.SK_KAHIN:
                    return "kahinah";
                default:
                    return DescribeLord(lord).ToLowerInvariant();
            }
        }

        private static string GetLordJsonFileName(Enums.AILords lord)
        {
            switch (lord)
            {
                case Enums.AILords.SK_PHILLIP:
                    return "Philip.lordjson";
                case Enums.AILords.SK_KAHIN:
                    return "Kahinah.lordjson";
                default:
                    string name = DescribeLord(lord).ToLowerInvariant();
                    return char.ToUpperInvariant(name[0]) + name.Substring(1) + ".lordjson";
            }
        }

        private static string EnsureAivJsonExtension(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "<unknown>.aivjson";
            return name.EndsWith(".aivjson", StringComparison.OrdinalIgnoreCase)
                ? name
                : name + ".aivjson";
        }

        private static bool HasAssetOverride(string assetPath)
        {
            try
            {
                return GameAssetManagerAPI.Instance != null &&
                       GameAssetManagerAPI.Instance.GetModifiedFileTextContent(
                           assetPath,
                           out _);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsKnownOrientation(int orientation)
        {
            return orientation == 0 ||
                   orientation == 2 ||
                   orientation == 4 ||
                   orientation == 6;
        }

        private int CurrentPreBuildSetting =>
            preBuildSettingAppliesToCurrentMap ? capturedPreBuildSetting : -1;

        private static string DescribePreBuildSetting(int setting)
        {
            switch (setting)
            {
                case 0:
                    return "disabled";
                case 1:
                    return "enabled";
                default:
                    return "unknown";
            }
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
                case 0:
                    return "no accepted placement";
                default:
                    return "unknown";
            }
        }

        private readonly struct AivSelectionSnapshot
        {
            public AivSelectionSnapshot(
                int playerId,
                int aivSpecIndex,
                int orientation,
                int candidateId,
                int placementState)
            {
                PlayerId = playerId;
                AivSpecIndex = aivSpecIndex;
                Orientation = orientation;
                CandidateId = candidateId;
                PlacementState = placementState;
            }

            public int PlayerId { get; }
            public int AivSpecIndex { get; }
            public int Orientation { get; }
            public int CandidateId { get; }
            public int PlacementState { get; }
        }

        private enum AivMode
        {
            Unknown,
            Default,
            Community,
            Historical,
            Custom
        }

        private sealed class LobbyAivSnapshot
        {
            private LobbyAivSnapshot()
            {
            }

            public int LordType { get; private set; }
            public string CustomLordName { get; private set; }
            public bool BuiltInLord { get; private set; }
            public string LordConfigName { get; private set; }
            public string LordConfigPath { get; private set; }
            public ulong LordConfigChecksum { get; private set; }
            public AivMode Mode { get; private set; }
            public List<LobbyAivCandidate> Candidates { get; private set; }

            public static LobbyAivSnapshot Create(
                int playerId,
                int lordType,
                string customLordName,
                FRONT_Multiplayer.MPAIVInfo info)
            {
                LobbyAivSnapshot snapshot = new LobbyAivSnapshot
                {
                    LordType = lordType,
                    CustomLordName = customLordName ?? string.Empty,
                    BuiltInLord = info == null || info.builtInLord,
                    LordConfigName = info?.lordConfig?.name ?? string.Empty,
                    LordConfigPath = info?.lordConfig?.path ?? string.Empty,
                    LordConfigChecksum = info?.lordConfig?.checksum ?? 0,
                    Mode = GetMode(info),
                    Candidates = new List<LobbyAivCandidate>()
                };

                if (info?.aivs != null)
                {
                    foreach (CustomisationFileManager.CustomAIV candidate in info.aivs)
                    {
                        if (candidate != null)
                            snapshot.Candidates.Add(LobbyAivCandidate.Create(candidate));
                    }
                }

                return snapshot;
            }

            public string GetCustomLordConfigJsonPath()
            {
                if (string.IsNullOrEmpty(LordConfigName))
                    return "<custom lord config metadata unavailable>";

                string fileName = LordConfigName.EndsWith(
                    ".lordjson",
                    StringComparison.OrdinalIgnoreCase)
                    ? LordConfigName
                    : LordConfigName + ".lordjson";
                return string.IsNullOrEmpty(LordConfigPath)
                    ? fileName
                    : Path.Combine(LordConfigPath, fileName);
            }

            private static AivMode GetMode(FRONT_Multiplayer.MPAIVInfo info)
            {
                if (info == null || info.builtIn || info.aivs == null || info.aivs.Count == 0)
                    return AivMode.Default;
                if (info.community)
                    return AivMode.Community;
                if (info.historical)
                    return AivMode.Historical;
                return AivMode.Custom;
            }
        }

        private readonly struct ResolvedLordConfig
        {
            public ResolvedLordConfig(
                string jsonPath,
                string fileSha256,
                string effectiveSource)
            {
                JsonPath = string.IsNullOrEmpty(jsonPath) ? "<unknown>" : jsonPath;
                FileSha256 = string.IsNullOrEmpty(fileSha256) ? "<not-available>" : fileSha256;
                EffectiveSource = string.IsNullOrEmpty(effectiveSource) ? "<unknown>" : effectiveSource;
            }

            public string JsonPath { get; }
            public string FileSha256 { get; }
            public string EffectiveSource { get; }

            public static ResolvedLordConfig Unknown(string reason)
            {
                return new ResolvedLordConfig(
                    "<unknown>",
                    "<not-available>",
                    string.IsNullOrEmpty(reason) ? "unknown" : reason);
            }
        }

        private sealed class LobbyAivCandidate
        {
            private LobbyAivCandidate()
            {
            }

            public int LordType { get; private set; }
            public string Name { get; private set; }
            public string Path { get; private set; }
            public ulong Checksum { get; private set; }
            public bool BuiltIn { get; private set; }

            public static LobbyAivCandidate Create(
                CustomisationFileManager.CustomAIV candidate)
            {
                return new LobbyAivCandidate
                {
                    LordType = candidate.lordType,
                    Name = candidate.AIVName ?? string.Empty,
                    Path = candidate.path ?? string.Empty,
                    Checksum = candidate.checksum,
                    BuiltIn = candidate.builtIn
                };
            }
        }

        private readonly struct ResolvedAivSource
        {
            public ResolvedAivSource(
                AivMode mode,
                string name,
                string jsonPath,
                string checksum,
                string effectiveSource)
            {
                Mode = mode;
                Name = string.IsNullOrEmpty(name) ? "<unknown>" : name;
                JsonPath = string.IsNullOrEmpty(jsonPath) ? "<unknown>" : jsonPath;
                Checksum = string.IsNullOrEmpty(checksum) ? "<not-applicable>" : checksum;
                EffectiveSource = effectiveSource;
            }

            public AivMode Mode { get; }
            public string Name { get; }
            public string JsonPath { get; }
            public string Checksum { get; }
            public string EffectiveSource { get; }

            public static ResolvedAivSource Unknown(string reason)
            {
                return new ResolvedAivSource(
                    AivMode.Unknown,
                    "<unknown>",
                    "<unknown>",
                    string.Empty,
                    reason);
            }
        }
    }
}
