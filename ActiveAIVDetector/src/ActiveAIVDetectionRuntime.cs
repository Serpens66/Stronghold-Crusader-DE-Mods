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
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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
        private readonly string vanillaAicDirectory;
        private readonly string vanillaAivDirectory;
        private readonly bool bundledVanillaAicMatchesInstalledGame;
        private readonly Dictionary<int, AivSelectionSnapshot> pendingSelections =
            new Dictionary<int, AivSelectionSnapshot>();
        private readonly Dictionary<int, LobbyAivSnapshot> lobbyAivSnapshots =
            new Dictionary<int, LobbyAivSnapshot>();
        private readonly HashSet<int> reportedPlayers = new HashSet<int>();
        private readonly List<IDisposable> lifecycleSubscriptions = new List<IDisposable>();
        private HookRef<X64ManagedFunctionDetourAOB<PrepareLayoutDelegate>> prepareLayoutHook =
            new HookRef<X64ManagedFunctionDetourAOB<PrepareLayoutDelegate>>();

        // Retaining the transaction keeps the native detour alive for the full process lifetime.
        private HookTransaction transaction;
        private Hook startSkirmishGameHook;
        private StartSkirmishGameDelegate startSkirmishGameTrampoline;
        private int detectionCount;
        private bool installed;
        private bool mapStartCompleted;
        private bool callbackFailureLogged;
        private bool lobbyCapturePending;
        private bool lobbySnapshotAppliesToCurrentMap;

        public ActiveAIVDetectionRuntime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
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

            transaction.Commit();

            if (!prepareLayoutHook.Success)
                throw new InvalidOperationException(
                    "The c_game_aiv_prepare_layout signature was not found.");

            SubscribeLifecycleHooks();
            installed = true;
            Shared.DebugLogHelper.LogInfo(
                log,
                "Native active-AIV detector installed at Info level; " +
                "lobby lord/AIV metadata will be joined with finalized selections after OnStartMap(Post).");
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
                .Subscribe(_ => ResetForMapTransition("map load")));

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
            reportedPlayers.Clear();
            callbackFailureLogged = false;

            // StartSkirmishGame captures metadata before the game's repeated unload/load callbacks.
            if (!lobbyCapturePending)
            {
                lobbyAivSnapshots.Clear();
                lobbySnapshotAppliesToCurrentMap = false;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Active AIV detector reset for {reason}; waiting for finalized AI selections. " +
                $"retainedPendingLobbyMetadata={lobbyCapturePending}.");
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
