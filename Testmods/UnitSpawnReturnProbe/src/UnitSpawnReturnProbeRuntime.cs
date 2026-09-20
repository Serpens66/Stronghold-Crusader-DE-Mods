using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace UnitSpawnReturnProbe
{
    internal sealed unsafe class UnitSpawnReturnProbeRuntime
    {
        private const long ExpectedSpawnRva = 0x17FEF0;
        private const int PreferredPlayerId = 4;
        private const int MaximumPlayerId = 8;
        private const int ProbeDelaySeconds = 5;
        private const int KeepReadinessTimeoutSeconds = 30;
        private const eChimps ProbeUnitType = eChimps.CHIMP_TYPE_MACEMAN;

        private readonly ManualLogSource log;
        private readonly List<IDisposable> processLifetimeSubscriptions = new List<IDisposable>();
        private readonly List<UnitIdentity> spawnedIdentities = new List<UnitIdentity>();

        private bool applied;
        private bool mapActive;
        private bool probeStarted;
        private bool probeCompleted;
        private bool eventWindowActive;
        private long mapStartedTimestamp;
        private long sessionId;
        private int eventSequence;
        private int followupTick = -1;
        private string activeProbeStep = "none";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate long ScriptExtenderAbiSpawnDelegate(
            IntPtr unitManager,
            int playerOwnerId,
            int playerColorId,
            int worldTileX,
            int worldTileY,
            int heightElevation,
            eChimps unitType);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NativeAbiSpawnDelegate(
            IntPtr unitManager,
            int playerOwnerId,
            short playerColorId,
            short worldTileX,
            short worldTileY,
            ushort heightElevation,
            int unitType);

        internal UnitSpawnReturnProbeRuntime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        internal void Apply()
        {
            if (applied)
                return;

            processLifetimeSubscriptions.Add(Shared.MissionEvents.Started.Subscribe(OnMissionStarted));
            processLifetimeSubscriptions.Add(Shared.MissionEvents.Ended.Subscribe(_ => ResetMapState("mission-ended")));
            processLifetimeSubscriptions.Add(UnitR3EventHooks.OnUnitCreate.Observable.Subscribe(OnUnitCreate));
            GameTimeManagerAPI.Instance.OnTick += OnGameTick;

            applied = true;
            LogAssemblyProvenance();
            LogInfo(
                "UNIT_SPAWN_RETURN_PROBE_READY: delaySeconds=5, oncePerMap=true, " +
                "probeVariants=public-api|redbird-trampoline-se-abi|redbird-trampoline-native-abi, " +
                "nativeSpawnRva=0x17FEF0, directDetourInstallation=false, unitIdsAreOneBased=true.");
        }

        private void OnMissionStarted(APIShared.MissionLifecycleNotification notification)
        {
            ResetMapState("mission-started");

            if (notification.Context.IsSave)
            {
                LogInfo("UNIT_SPAWN_RETURN_PROBE_SKIPPED: reason=save-load.");
                return;
            }

            if (!notification.Context.Mode.IsSingleplayerSkirmish ||
                notification.Context.Mode.IsRealMultiplayer ||
                notification.Context.Mode.IsMapEditor)
            {
                LogInfo(
                    "UNIT_SPAWN_RETURN_PROBE_SKIPPED: reason=unsupported-mode, " +
                    notification.Context.Mode.ToDiagnosticString() + ".");
                return;
            }

            sessionId = notification.Context.SessionId;
            mapStartedTimestamp = Stopwatch.GetTimestamp();
            mapActive = true;
            LogInfo(
                $"UNIT_SPAWN_RETURN_PROBE_ARMED: session={sessionId}, delaySeconds={ProbeDelaySeconds}, " +
                "waitingForTargetKeep=true.");
        }

        private void ResetMapState(string reason)
        {
            mapActive = false;
            probeStarted = false;
            probeCompleted = false;
            eventWindowActive = false;
            mapStartedTimestamp = 0;
            sessionId = 0;
            eventSequence = 0;
            followupTick = -1;
            activeProbeStep = "none";
            spawnedIdentities.Clear();
            LogInfo($"UNIT_SPAWN_RETURN_PROBE_RESET: reason={reason}.");
        }

        private void OnGameTick(int tick)
        {
            if (!mapActive)
                return;

            if (followupTick >= 0 && tick >= followupTick)
            {
                followupTick = -1;
                LogFollowupSnapshots(tick);
            }

            if (probeStarted || probeCompleted)
                return;

            long elapsed = Stopwatch.GetTimestamp() - mapStartedTimestamp;
            long required = checked((long)ProbeDelaySeconds * Stopwatch.Frequency);
            if (elapsed < required)
                return;

            if (!TryResolveTarget(out ProbeTarget target, out string failure))
            {
                long timeout = checked((long)KeepReadinessTimeoutSeconds * Stopwatch.Frequency);
                if (elapsed >= timeout)
                {
                    probeCompleted = true;
                    LogError($"UNIT_SPAWN_RETURN_PROBE_ABORTED: reason=target-not-ready, details={failure}.");
                }
                return;
            }

            probeStarted = true;
            try
            {
                RunProbe(tick, target);
            }
            catch (Exception exception)
            {
                LogError($"UNIT_SPAWN_RETURN_PROBE_FAILED: session={sessionId}, exception={exception}");
            }
            finally
            {
                eventWindowActive = false;
                activeProbeStep = "none";
                probeCompleted = true;
                followupTick = tick + 1;
            }
        }

        private void RunProbe(int tick, ProbeTarget target)
        {
            if (!Shared.DebugLogHelper.IsCurrentNativeLibraryVersion())
                throw new InvalidOperationException("The installed native hash changed after initialization.");

            IntPtr manager = (IntPtr)GameUnitManagerAPI.Instance.GetUnitManager().Pointer;
            if (manager == IntPtr.Zero)
                throw new InvalidOperationException("GameUnitManager pointer is null.");

            HookAddresses hook = ResolveInstalledSpawnHook();
            ValidateHookAddresses(hook);

            int worldX = checked(target.LocalX * 8);
            int worldY = checked(target.LocalY * 8);
            if (worldX < short.MinValue || worldX > short.MaxValue ||
                worldY < short.MinValue || worldY > short.MaxValue ||
                target.PlayerId < short.MinValue || target.PlayerId > short.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Probe parameters do not fit the audited native ABI: player={target.PlayerId}, world={worldX},{worldY}.");
            }

            LogInfo(
                $"UNIT_SPAWN_RETURN_PROBE_BEGIN: session={sessionId}, tick={tick}, player={target.PlayerId}, " +
                $"localPlayer={target.LocalPlayerId}, activePlayers=[{target.ActivePlayers}], keepId={target.KeepId}, " +
                $"keepDoor={target.KeepDoorX},{target.KeepDoorY}, local={target.LocalX},{target.LocalY}, " +
                $"tileId={target.TileId}, tileBuildingId={target.TileBuildingId}, " +
                $"walkableAndUnoccupied={target.WalkableAndUnoccupied}, world={worldX},{worldY}, " +
                $"height={target.Height}, manager=0x{manager.ToInt64():X16}, target=0x{hook.TargetAddress:X16}, " +
                $"trampoline=0x{hook.TrampolineAddress.ToInt64():X16}, originalEntry=0x{hook.OriginalEntryPointAddress.ToInt64():X16}, " +
                $"displacedBytes={hook.DisplacedByteCount}, scheme={hook.Scheme}.");

            eventWindowActive = true;

            ProbeResult apiResult = ExecuteProbeStep(
                "public-api",
                () => GameUnitManagerAPI.Instance.CreateUnitLocal(
                    target.PlayerId,
                    target.PlayerId,
                    target.LocalX,
                    target.LocalY,
                    target.Height,
                    ProbeUnitType));

            ScriptExtenderAbiSpawnDelegate scriptExtenderAbi =
                Marshal.GetDelegateForFunctionPointer<ScriptExtenderAbiSpawnDelegate>(hook.OriginalEntryPointAddress);
            ProbeResult scriptExtenderAbiResult = ExecuteProbeStep(
                "redbird-trampoline-se-abi",
                () => scriptExtenderAbi(
                    manager,
                    target.PlayerId,
                    target.PlayerId,
                    worldX,
                    worldY,
                    target.Height,
                    ProbeUnitType));

            NativeAbiSpawnDelegate nativeAbi =
                Marshal.GetDelegateForFunctionPointer<NativeAbiSpawnDelegate>(hook.OriginalEntryPointAddress);
            ProbeResult nativeAbiResult = ExecuteProbeStep(
                "redbird-trampoline-native-abi",
                () => nativeAbi(
                    manager,
                    target.PlayerId,
                    checked((short)target.PlayerId),
                    checked((short)worldX),
                    checked((short)worldY),
                    target.Height,
                    (int)ProbeUnitType));

            LogConclusion(apiResult, scriptExtenderAbiResult, nativeAbiResult);
        }

        private ProbeResult ExecuteProbeStep(string step, Func<long> spawn)
        {
            activeProbeStep = step;
            Dictionary<int, UnitSnapshot> before = CaptureActiveUnits();
            long returnValue = spawn();
            Dictionary<int, UnitSnapshot> after = CaptureActiveUnits();
            List<UnitSnapshot> added = FindAddedUnits(before, after);

            foreach (UnitSnapshot unit in added)
                spawnedIdentities.Add(new UnitIdentity(unit.GameId, unit.GlobalId));

            string addedText = added.Count == 0
                ? "none"
                : string.Join(" | ", added.Select(unit => unit.ToString()));
            LogInfo(
                $"UNIT_SPAWN_RETURN_PROBE_STEP: step={step}, returnValue={returnValue}, " +
                $"activeBefore={before.Count}, activeAfter={after.Count}, addedCount={added.Count}, added=[{addedText}].");

            return new ProbeResult(step, returnValue, added.Count);
        }

        private void LogConclusion(ProbeResult api, ProbeResult scriptExtenderAbi, ProbeResult nativeAbi)
        {
            string classification;
            if (api.ReturnValue > 0 && scriptExtenderAbi.ReturnValue > 0 && nativeAbi.ReturnValue > 0 &&
                api.AddedCount == 1 && scriptExtenderAbi.AddedCount == 1 && nativeAbi.AddedCount == 1)
                classification = "ALL_SPAWN_PATHS_VALID";
            else if (api.ReturnValue == 0 && scriptExtenderAbi.ReturnValue == 0 && nativeAbi.ReturnValue > 0)
                classification = "SCRIPT_EXTENDER_DELEGATE_ABI_MISMATCH_CONFIRMED";
            else if (scriptExtenderAbi.ReturnValue == 0 && nativeAbi.ReturnValue == 0 &&
                     (scriptExtenderAbi.AddedCount > 0 || nativeAbi.AddedCount > 0))
                classification = "REDBIRD_TRAMPOLINE_RETURN_LOSS_CONFIRMED";
            else if (api.ReturnValue == 0 && scriptExtenderAbi.ReturnValue > 0 && nativeAbi.ReturnValue > 0)
                classification = "SCRIPT_EXTENDER_EVENT_OR_WRAPPER_RETURN_LOSS_CONFIRMED";
            else if (api.ReturnValue == 0 && api.AddedCount == 0)
                classification = "PUBLIC_API_NATIVE_SPAWN_FAILURE_OBSERVED";
            else
                classification = "MIXED_RESULT_REQUIRES_LOG_REVIEW";

            LogInfo(
                $"UNIT_SPAWN_RETURN_PROBE_CONCLUSION: classification={classification}, " +
                $"apiReturn={api.ReturnValue},apiAdded={api.AddedCount}, " +
                $"seAbiReturn={scriptExtenderAbi.ReturnValue},seAbiAdded={scriptExtenderAbi.AddedCount}, " +
                $"nativeAbiReturn={nativeAbi.ReturnValue},nativeAbiAdded={nativeAbi.AddedCount}.");
        }

        private void OnUnitCreate(UnitCreateEventArgs args)
        {
            if (!eventWindowActive)
                return;

            int sequence = ++eventSequence;
            LogInfo(
                $"UNIT_SPAWN_RETURN_PROBE_EVENT: sequence={sequence}, step={activeProbeStep}, phase={args.Phase}, " +
                $"returnValue={args.ReturnValue}, skipOriginal={args.SkipOriginalFunction}, owner={args.PlayerOwnerId}, " +
                $"color={args.PlayerColorId}, world={args.WorldTileX},{args.WorldTileY}, height={args.HeightElevation}, " +
                $"type={args.UnitType}.");
        }

        private void LogFollowupSnapshots(int tick)
        {
            if (spawnedIdentities.Count == 0)
            {
                LogInfo($"UNIT_SPAWN_RETURN_PROBE_FOLLOWUP: tick={tick}, trackedUnits=0.");
                return;
            }

            foreach (UnitIdentity identity in spawnedIdentities.Distinct())
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(identity.GameId, out GameUnit* unit) || unit == null)
                {
                    LogWarning(
                        $"UNIT_SPAWN_RETURN_PROBE_FOLLOWUP: tick={tick}, gameId={identity.GameId}, " +
                        $"expectedGlobalId={identity.GlobalId}, status=missing.");
                    continue;
                }

                UnitSnapshot snapshot = UnitSnapshot.Capture(identity.GameId, unit);
                string status = snapshot.GlobalId == identity.GlobalId ? "identity-match" : "identity-changed";
                LogInfo($"UNIT_SPAWN_RETURN_PROBE_FOLLOWUP: tick={tick}, status={status}, unit={snapshot}.");
            }
        }

        private static Dictionary<int, UnitSnapshot> CaptureActiveUnits()
        {
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            Dictionary<int, UnitSnapshot> result = new Dictionary<int, UnitSnapshot>();
            for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
            {
                fixed (GameUnit* unit = &units[spanIndex])
                {
                    if (!IsActive(unit->r_AliveState) || unit->r_GlobalId == 0)
                        continue;

                    int gameId = checked(spanIndex + 1);
                    result.Add(gameId, UnitSnapshot.Capture(gameId, unit));
                }
            }
            return result;
        }

        private static List<UnitSnapshot> FindAddedUnits(
            Dictionary<int, UnitSnapshot> before,
            Dictionary<int, UnitSnapshot> after)
        {
            List<UnitSnapshot> added = new List<UnitSnapshot>();
            foreach (KeyValuePair<int, UnitSnapshot> pair in after)
            {
                if (!before.TryGetValue(pair.Key, out UnitSnapshot old) || old.GlobalId != pair.Value.GlobalId)
                    added.Add(pair.Value);
            }
            added.Sort((left, right) => left.GameId.CompareTo(right.GameId));
            return added;
        }

        private bool TryResolveTarget(out ProbeTarget target, out string failure)
        {
            target = default;
            int[] activePlayerIds = Shared.ActivePlayerHelper.GetActivePlayerIds();
            if (activePlayerIds.Length == 0)
            {
                failure = "the synchronized active-player roster is unavailable or empty";
                return false;
            }

            int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            List<int> candidates = new List<int>();
            if (activePlayerIds.Contains(PreferredPlayerId))
                candidates.Add(PreferredPlayerId);

            foreach (int playerId in activePlayerIds)
            {
                if (playerId != PreferredPlayerId && playerId != localPlayerId && playerId != 1)
                    candidates.Add(playerId);
            }

            if (activePlayerIds.Contains(1) && !candidates.Contains(1))
                candidates.Add(1);

            List<string> rejections = new List<string>();
            string activePlayers = string.Join(",", activePlayerIds);
            foreach (int playerId in candidates)
            {
                if (!TryGetReadyKeep(playerId, out int keepId, out _))
                {
                    rejections.Add($"P{playerId}:keep-not-ready");
                    continue;
                }

                var door = GamePlayerManagerAPI.Instance.GetPlayerKeepDoorPosition(playerId);
                var position = GameTileManagerAPI.Instance.GetNearestUnoccupiedTile(door.X, door.Y, 12);
                int tileId = GameTileManagerAPI.Instance.GetTileId(position.X, position.Y);
                bool validTile = GameTileManagerAPI.Instance.IsValidTileId(tileId);
                bool walkableAndUnoccupied = validTile &&
                    GameTileManagerAPI.Instance.IsTileWalkableAndUnoccupied(tileId);
                int tileBuildingId = validTile
                    ? GameTileManagerAPI.Instance.GetTileBuildingId(tileId)
                    : -1;
                if (!validTile || !walkableAndUnoccupied)
                {
                    rejections.Add(
                        $"P{playerId}:invalid-spawn-tile door={door.X},{door.Y}, " +
                        $"nearest={position.X},{position.Y}, tileId={tileId}, valid={validTile}, " +
                        $"walkableAndUnoccupied={walkableAndUnoccupied}, tileBuildingId={tileBuildingId}");
                    continue;
                }

                byte height = GameTileManagerAPI.Instance.GetTileHeight(tileId);
                target = new ProbeTarget(
                    playerId,
                    localPlayerId,
                    activePlayers,
                    keepId,
                    door.X,
                    door.Y,
                    position.X,
                    position.Y,
                    tileId,
                    tileBuildingId,
                    walkableAndUnoccupied,
                    height);
                failure = string.Empty;
                return true;
            }

            failure =
                $"no valid StartConditions-style spawn tile was found; localPlayer={localPlayerId}, " +
                $"activePlayers=[{activePlayers}], candidates=[{string.Join(",", candidates)}], " +
                $"rejections=[{string.Join(" | ", rejections)}]";
            return false;
        }

        private static bool TryGetReadyKeep(int playerId, out int keepId, out GameBuilding* keep)
        {
            keepId = -1;
            keep = null;
            GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
            if (!players.IsPlayerIdValid(playerId))
                return false;

            keepId = players.GetPlayerKeepId(playerId);
            if (keepId <= 0 || !GameBuildingManagerAPI.Instance.TryGetBuildingById(keepId, out keep) || keep == null)
                return false;

            return keep->r_PlayerIdOwner == playerId &&
                   IsKeepType(keep->r_BuildingType) &&
                   (keep->r_AliveState == AliveState.NeedsInit || keep->r_AliveState == AliveState.IsAlive);
        }

        private static bool IsKeepType(eStructs buildingType)
        {
            return buildingType == eStructs.STRUCT_KEEP_ONE ||
                   buildingType == eStructs.STRUCT_KEEP_TWO ||
                   buildingType == eStructs.STRUCT_KEEP_THREE ||
                   buildingType == eStructs.STRUCT_KEEP_FOUR ||
                   buildingType == eStructs.STRUCT_KEEP_FIVE;
        }

        private HookAddresses ResolveInstalledSpawnHook()
        {
            Assembly scriptExtender = typeof(GameUnitManagerAPI).Assembly;
            Type bulkUnitDetours = scriptExtender.GetType("SHCDESE.Detours.BulkUnitDetours", throwOnError: true);
            FieldInfo handleField = bulkUnitDetours.GetField(
                "c_game_unit_spawn_ex_hook",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            object handle = handleField?.GetValue(null) ??
                throw new InvalidOperationException("The Script Extender spawn detour handle was not found.");

            object hook = ReadProperty(handle, "Hook") ??
                throw new InvalidOperationException("The Script Extender spawn detour is not installed.");
            ulong targetAddress = Convert.ToUInt64(ReadProperty(hook, "TargetAddress"));
            IntPtr trampolineAddress = (IntPtr)ReadProperty(hook, "TrampolineAddress");
            IntPtr originalEntryPointAddress = (IntPtr)ReadProperty(hook, "OriginalEntryPointAddress");
            int displacedByteCount = Convert.ToInt32(ReadProperty(hook, "DisplacedByteCount"));
            string scheme = Convert.ToString(ReadProperty(hook, "Scheme"));
            return new HookAddresses(
                targetAddress,
                trampolineAddress,
                originalEntryPointAddress,
                displacedByteCount,
                scheme);
        }

        private static object ReadProperty(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null)
                throw new MissingMemberException(instance.GetType().FullName, propertyName);
            return property.GetValue(instance);
        }

        private static void ValidateHookAddresses(HookAddresses hook)
        {
            if (hook.TargetAddress == 0 || hook.TrampolineAddress == IntPtr.Zero ||
                hook.OriginalEntryPointAddress == IntPtr.Zero || hook.DisplacedByteCount <= 0)
            {
                throw new InvalidOperationException("The installed spawn detour exposes invalid addresses or displacement length.");
            }

            ProcessModule module = Process.GetCurrentProcess().Modules
                .Cast<ProcessModule>()
                .FirstOrDefault(candidate => string.Equals(
                    candidate.ModuleName,
                    "CrusaderDE.dll",
                    StringComparison.OrdinalIgnoreCase));
            if (module == null)
                throw new InvalidOperationException("The loaded CrusaderDE.dll module was not found.");

            ulong expectedAddress = checked((ulong)module.BaseAddress.ToInt64() + (ulong)ExpectedSpawnRva);
            if (hook.TargetAddress != expectedAddress)
            {
                throw new InvalidOperationException(
                    $"Spawn hook target mismatch: expected=0x{expectedAddress:X16}, actual=0x{hook.TargetAddress:X16}.");
            }

            if (hook.OriginalEntryPointAddress != hook.TrampolineAddress)
            {
                throw new InvalidOperationException(
                    $"Unexpected RedBird intermediary: trampoline=0x{hook.TrampolineAddress.ToInt64():X16}, " +
                    $"originalEntry=0x{hook.OriginalEntryPointAddress.ToInt64():X16}.");
            }
        }

        private void LogAssemblyProvenance()
        {
            Assembly scriptExtender = typeof(GameUnitManagerAPI).Assembly;
            Assembly redBird = typeof(RedBird.Core.Memory.NativePointer<>).Assembly;
            LogInfo(
                $"UNIT_SPAWN_RETURN_PROBE_PROVENANCE: shcdeseVersion={scriptExtender.GetName().Version}, " +
                $"shcdeseSha256={ComputeSha256(scriptExtender.Location)}, redBirdCoreVersion={redBird.GetName().Version}, " +
                $"redBirdCoreSha256={ComputeSha256(redBird.Location)}, nativeSha256={Shared.DebugLogHelper.CurrentNativeSha256}.");
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha256 = SHA256.Create())
                return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private static bool IsActive(AliveState state) =>
            state == AliveState.NeedsInit || state == AliveState.IsAlive;

        private void LogInfo(string message) => Shared.DebugLogHelper.LogInfo(log, message);
        private void LogWarning(string message) => Shared.DebugLogHelper.LogWarning(log, message);
        private void LogError(string message) => Shared.DebugLogHelper.LogError(log, message);

        private readonly struct ProbeTarget
        {
            internal ProbeTarget(
                int playerId,
                int localPlayerId,
                string activePlayers,
                int keepId,
                int keepDoorX,
                int keepDoorY,
                int localX,
                int localY,
                int tileId,
                int tileBuildingId,
                bool walkableAndUnoccupied,
                byte height)
            {
                PlayerId = playerId;
                LocalPlayerId = localPlayerId;
                ActivePlayers = activePlayers ?? string.Empty;
                KeepId = keepId;
                KeepDoorX = keepDoorX;
                KeepDoorY = keepDoorY;
                LocalX = localX;
                LocalY = localY;
                TileId = tileId;
                TileBuildingId = tileBuildingId;
                WalkableAndUnoccupied = walkableAndUnoccupied;
                Height = height;
            }

            internal int PlayerId { get; }
            internal int LocalPlayerId { get; }
            internal string ActivePlayers { get; }
            internal int KeepId { get; }
            internal int KeepDoorX { get; }
            internal int KeepDoorY { get; }
            internal int LocalX { get; }
            internal int LocalY { get; }
            internal int TileId { get; }
            internal int TileBuildingId { get; }
            internal bool WalkableAndUnoccupied { get; }
            internal byte Height { get; }
        }

        private readonly struct HookAddresses
        {
            internal HookAddresses(
                ulong targetAddress,
                IntPtr trampolineAddress,
                IntPtr originalEntryPointAddress,
                int displacedByteCount,
                string scheme)
            {
                TargetAddress = targetAddress;
                TrampolineAddress = trampolineAddress;
                OriginalEntryPointAddress = originalEntryPointAddress;
                DisplacedByteCount = displacedByteCount;
                Scheme = scheme ?? string.Empty;
            }

            internal ulong TargetAddress { get; }
            internal IntPtr TrampolineAddress { get; }
            internal IntPtr OriginalEntryPointAddress { get; }
            internal int DisplacedByteCount { get; }
            internal string Scheme { get; }
        }

        private readonly struct ProbeResult
        {
            internal ProbeResult(string step, long returnValue, int addedCount)
            {
                Step = step;
                ReturnValue = returnValue;
                AddedCount = addedCount;
            }

            internal string Step { get; }
            internal long ReturnValue { get; }
            internal int AddedCount { get; }
        }

        private readonly struct UnitIdentity : IEquatable<UnitIdentity>
        {
            internal UnitIdentity(int gameId, uint globalId)
            {
                GameId = gameId;
                GlobalId = globalId;
            }

            internal int GameId { get; }
            internal uint GlobalId { get; }

            public bool Equals(UnitIdentity other) => GameId == other.GameId && GlobalId == other.GlobalId;
            public override bool Equals(object obj) => obj is UnitIdentity other && Equals(other);
            public override int GetHashCode() => (GameId * 397) ^ unchecked((int)GlobalId);
        }

        private readonly struct UnitSnapshot
        {
            private UnitSnapshot(
                int gameId,
                uint globalId,
                AliveState aliveState,
                eChimps unitType,
                int ownerPlayerId,
                int currentX,
                int currentY,
                int tribeId,
                ushort aiRole,
                ushort aiRoleRelated)
            {
                GameId = gameId;
                GlobalId = globalId;
                AliveState = aliveState;
                UnitType = unitType;
                OwnerPlayerId = ownerPlayerId;
                CurrentX = currentX;
                CurrentY = currentY;
                TribeId = tribeId;
                AIRole = aiRole;
                AIRoleRelated = aiRoleRelated;
            }

            internal int GameId { get; }
            internal uint GlobalId { get; }
            internal AliveState AliveState { get; }
            internal eChimps UnitType { get; }
            internal int OwnerPlayerId { get; }
            internal int CurrentX { get; }
            internal int CurrentY { get; }
            internal int TribeId { get; }
            internal ushort AIRole { get; }
            internal ushort AIRoleRelated { get; }

            internal static UnitSnapshot Capture(int gameId, GameUnit* unit)
            {
                return new UnitSnapshot(
                    gameId,
                    unit->r_GlobalId,
                    unit->r_AliveState,
                    unit->r_UnitChimp,
                    unit->r_ControllableForPlayerId,
                    unit->r_CurrentTilePositionX,
                    unit->r_CurrentTilePositionY,
                    unit->r_TribeId,
                    unit->r_AITribeRole,
                    unit->r_AITribeRoleRelatedUnknown);
            }

            public override string ToString()
            {
                return $"gameId={GameId},globalId={GlobalId},state={AliveState},type={UnitType},owner={OwnerPlayerId}," +
                       $"tile={CurrentX},{CurrentY},tribe={TribeId},aiRoleSigned={(short)AIRole}," +
                       $"aiRoleRaw={AIRole},aiRoleRelated={AIRoleRelated}";
            }
        }
    }
}
