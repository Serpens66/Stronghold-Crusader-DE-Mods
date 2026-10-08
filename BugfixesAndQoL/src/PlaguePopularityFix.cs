// Feature: Tie the plague popularity penalty to living Disease projectile herds.
using BepInEx.Logging;
using MessagePack;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.SaveData;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Projectiles;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;

namespace BugfixesAndQoL
{
    internal sealed unsafe class PlaguePopularityFix : IDisposable
    {
        private const string SaveDataIdentifier = "serp-plague-popularity-v1";
        private const int MinimumProjectilesPerHerd = 6;
        private const int PopularityPointsPerHerd = 25;
        private const int MissingPopularityCallbackWarningMilliseconds = 3000;
        private const ulong PopularityAccumulatorOffset = 0x12EC20UL;

        // c_game_disease_create_one_herd, reference RVA 0xD17D0.
        private const string CreateHerdPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 " +
            "48 83 EC 60 4C 8D 2D ?? ?? ?? ?? 48 63 C2 48 69 C8 2C 03 00 00";

        // Common exit of Vanilla's plague-popularity block. The hook starts at
        // the report-field write at reference RVA 0xCB57C (pattern + 32).
        private const string PopularityExitPattern =
            "B9 E7 FF FF FF 0F 4E C1 03 D0 41 89 94 2C 20 EC 12 00 EB 0C " +
            "45 89 AC 2C 84 0E 13 00 41 0F B7 C5 " +
            "66 41 89 84 2C 78 0E 13 00 41 0F B7 84 2C 6E 0E 13 00";
        private const int CreateHerdRva = 0xD17D0;
        private const int PopularityExitPatternRva = 0xCB55C;
        private const int PopularityExitHookOffset = 32;

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly PlagueHerdLedger herds = new PlagueHerdLedger();
        private readonly object stateGate = new object();
        private readonly Func<PlagueProjectileIdentity, bool> isLivingProjectile;
        private volatile bool correctionEnabled;
        private readonly HashSet<int> managedPlayerIds = new HashSet<int>();
        private readonly Dictionary<int, int> popularityCallbackCounts = new Dictionary<int, int>();
        private readonly Dictionary<int, int> correctedCallbackCounts = new Dictionary<int, int>();
        private readonly Dictionary<int, int> diagnosticRevisions = new Dictionary<int, int>();
        private readonly Dictionary<int, int> loggedDiagnosticRevisions = new Dictionary<int, int>();
        private long diagnosticGeneration;
        private readonly Dictionary<int, string> diagnosticTimers = new Dictionary<int, string>();
        private HookTransaction transaction;
        private readonly DetourHandle<CreateHerdDelegate> createHerdHook = new DetourHandle<CreateHerdDelegate>();
        private readonly HookHandle<X64InlineHook> popularityExitHook = new HookHandle<X64InlineHook>();
        private volatile HerdCapture currentCapture;
        private bool saveHandlerRegistered;
        private volatile bool mapActive;
        private volatile bool correctionAvailable = true;
        private bool callbackFailureLogged;
        private bool disposed;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void CreateHerdDelegate(IntPtr diseaseManager, int buildingId);

        public PlaguePopularityFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            isLivingProjectile = IsLivingDiseaseProjectile;
            correctionEnabled = settings.EnableMod && settings.EnablePlaguePopularityFix;

            int createHerdRva = PlagueNativePatternValidator.Resolve(
                log, memory, CreateHerdPattern, CreateHerdRva, referenceHashMatches, "plague herd creation");
            int popularityExitPatternRva = PlagueNativePatternValidator.Resolve(
                log, memory, PopularityExitPattern, PopularityExitPatternRva, referenceHashMatches, "plague popularity exit");

            try
            {
                transaction = BugfixesHookInfrastructure.CreateOwnedTransaction(region);
                transaction.AddDetour(
                    createHerdHook,
                    HookTarget.FromAddress(libraryBase + unchecked((ulong)createHerdRva)),
                    CreatePlagueHerd);
                BugfixesHookInfrastructure.AddContextHook(transaction, popularityExitHook,
                    libraryBase + unchecked((ulong)(popularityExitPatternRva + PopularityExitHookOffset)),
                    CorrectPlaguePopularity,
                    registers: X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBP |
                        X64SmartCPUContextRegs.R12 | X64SmartCPUContextRegs.R14,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                CommitResult commitResult = transaction.Commit();

                if (!commitResult.IsCompleteSuccess || !createHerdHook.Success || !popularityExitHook.Success)
                    throw new InvalidOperationException("The plague herd or popularity hook was not installed.");

                subscriptions.Add(ProjectileR3EventHooks.OnProjectileSpawn.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnProjectileSpawn));
                subscriptions.Add(ProjectileR3EventHooks.OnProjectileDelete.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnProjectileDelete));
                subscriptions.Add(Shared.GameplaySessionLifecycle.SubscribeStarted(log, OnSessionStarted, ResetMapState));
                settings.SettingChanged += OnSettingChanged;

                if (!ModSaveDataAPI.Instance.RegisterModDataHandler(
                        SaveDataIdentifier,
                        SaveState,
                        LoadState,
                        ResetMapState))
                {
                    throw new InvalidOperationException("Plague popularity save-data handler registration failed.");
                }
                saveHandlerRegistered = true;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            correctionAvailable = false;
            settings.SettingChanged -= OnSettingChanged;
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            if (saveHandlerRegistered)
            {
                ModSaveDataAPI.Instance.UnregisterModDataHandler(SaveDataIdentifier);
                saveHandlerRegistered = false;
            }
            ResetMapState();
        }

        private void CreatePlagueHerd(IntPtr diseaseManager, int buildingId)
        {
            lock (stateGate)
            {
                if (!correctionAvailable)
                {
                    createHerdHook.Original(diseaseManager, buildingId);
                    return;
                }

                HerdCapture capture = null;
                try
                {
                    if (GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) &&
                        building != null &&
                        IsValidPlayerId(building->r_PlayerIdOwner))
                    {
                        capture = new HerdCapture(
                            buildingId,
                            building->r_GlobalId,
                            building->r_PlayerIdOwner,
                            building->r_TilePositionXBegin,
                            building->r_TilePositionYBegin);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Vanilla selected an invalid plague source building: buildingId={buildingId}.");
                    }
                }
                catch (Exception ex)
                {
                    DisableCorrectionToVanilla("plague source-player detection failed", ex);
                }

                HerdCapture previousCapture = currentCapture;
                currentCapture = capture;
                try
                {
                    // Vanilla remains authoritative for all projectile creation.
                    createHerdHook.Original(diseaseManager, buildingId);
                }
                finally
                {
                    currentCapture = previousCapture;
                }

                if (capture == null || !correctionAvailable)
                    return;

                try
                {
                    if (capture.Members.Count < MinimumProjectilesPerHerd ||
                        capture.Members.Count > PlaguePopularitySaveLimitPolicy.GetCurrent().MaximumProjectilesPerHerd)
                    {
                        throw new InvalidOperationException(
                            $"Vanilla created an unexpected plague-herd size: " +
                            $"playerId={capture.PlayerId}, projectileCount={capture.Members.Count}.");
                    }

                    herds.Add(capture.PlayerId, capture.Members);
                    managedPlayerIds.Add(capture.PlayerId);
                    ArmPopularityDiagnostic(capture.PlayerId);
                    if (correctionEnabled) LogDebug(
                        () => $"Plague herd captured: sourceBuildingId={capture.BuildingId}, " +
                        $"sourceBuildingGlobalId={capture.BuildingGlobalId}, playerId={capture.PlayerId}, " +
                        $"sourceTile=({capture.TileX},{capture.TileY}), projectileCount={capture.Members.Count}, " +
                        $"projectiles={DescribeProjectiles(capture.Members)}, " +
                        $"activeHerdsForPlayer={CountHerds(capture.PlayerId)}, " +
                        $"popularityCallbacksObserved={DescribeCallbackCounts()}, " +
                        $"mode={Shared.GameModeHelper.Capture().ToDiagnosticString()}.");
                }
                catch (Exception ex)
                {
                    DisableCorrectionToVanilla("plague herd capture failed", ex);
                }
            }
        }

        private void OnProjectileSpawn(ProjectileSpawnEventArgs args)
        {
            if (!correctionAvailable || currentCapture == null || args.ProjectileType != ProjectileType.Disease) return;
            lock (stateGate)
            {
                HerdCapture capture = currentCapture;
                if (!correctionAvailable || capture == null || args.ProjectileType != ProjectileType.Disease)
                    return;

                try
                {
                    if (args.ReturnValue <= 0 || args.ReturnValue > int.MaxValue)
                        throw new InvalidOperationException($"Disease projectile returned an invalid slot ID: {args.ReturnValue}.");

                    int projectileId = checked((int)args.ReturnValue);
                    if (!GameProjectileManagerAPI.Instance.TryGetProjectileById(projectileId, out GameProjectile* projectile) ||
                        projectile == null ||
                        projectile->r_ProjectileType != ProjectileType.Disease ||
                        projectile->r_GlobalId == 0)
                    {
                        throw new InvalidOperationException(
                            $"Spawned Disease projectile could not be identified: projectileId={projectileId}.");
                    }

                    capture.Add(projectileId, projectile->r_GlobalId);
                }
                catch (Exception ex)
                {
                    DisableCorrectionToVanilla("Disease projectile capture failed", ex);
                }
            }
        }

        private void OnProjectileDelete(ProjectileDeleteEventArgs args)
        {
            if (!correctionAvailable || herds.Count == 0) return;
            lock (stateGate)
            {
                if (!correctionAvailable || herds.Count == 0)
                    return;

                try
                {
                    herds.ReconcileDeletedSlot(args.ProjectileId, isLivingProjectile);
                }
                catch (Exception ex)
                {
                    DisableCorrectionToVanilla("Disease projectile deletion tracking failed", ex);
                }
            }
        }

        private void OnSessionStarted(Shared.GameplaySessionStartedContext context)
        {
            lock (stateGate)
            {
                mapActive = true;
                LogDebug(
                    () => $"Plague popularity diagnostics armed: modEnabled={settings.EnableMod}, " +
                    $"fixEnabled={settings.EnablePlaguePopularityFix}, " +
                    $"source={context.Kind}, mode={context.Mode.ToDiagnosticString()}.");
            }
        }

        private void OnSettingChanged(string propertyName)
        {
            if (propertyName != nameof(BugfixesAndQoLViewModel.EnableMod) &&
                propertyName != nameof(BugfixesAndQoLViewModel.EnablePlaguePopularityFix)) return;
            bool next = settings.EnableMod && settings.EnablePlaguePopularityFix;
            lock (stateGate)
            {
                bool previous = correctionEnabled;
                correctionEnabled = next;
                if (!next) CancelAllDiagnostics();
                else if (!previous && mapActive)
                    foreach (int playerId in managedPlayerIds) ArmPopularityDiagnostic(playerId);
            }
        }

        private void CorrectPlaguePopularity(NativePointer<X64SmartCPUContext> context)
        {
            if (!correctionEnabled || !correctionAvailable || !mapActive) return;
            lock (stateGate)
            {
                if (!correctionEnabled || !correctionAvailable || !mapActive)
                    return;

                try
                {
                    X64SmartCPUContext* registers = context.Pointer;
                    int playerId = unchecked((int)(uint)registers->R14);
                    if (!managedPlayerIds.Contains(playerId)) return;
                    IncrementCount(popularityCallbackCounts, playerId);

                    int diagnosticRevision = GetCount(diagnosticRevisions, playerId);
                    // Read-only native validation makes natural expiry effective in the
                    // same popularity pass even if no delete event was emitted.
                    herds.ReconcilePlayer(playerId, isLivingProjectile);
                    CancelDiagnostic(playerId);
                    int herdCount = CountHerds(playerId);
                    int desiredModifier = checked(-PopularityPointsPerHerd * herdCount);
                    if (desiredModifier < short.MinValue)
                        throw new OverflowException($"Too many simultaneous plague herds for player {playerId}: {herdCount}.");

                    int vanillaModifier = (short)(ushort)registers->RAX;
                    int currentPopularity = unchecked((int)(uint)registers->RDX);
                    int correctedPopularity = checked(currentPopularity - vanillaModifier + desiredModifier);
                    if (registers->R12 == 0)
                        throw new InvalidOperationException("The native player-resource base register is null.");
                    int* popularityAccumulator =
                        (int*)(registers->R12 + registers->RBP + PopularityAccumulatorOffset);
                    int accumulatorBefore = *popularityAccumulator;

                    registers->RDX = unchecked((uint)correctedPopularity);
                    registers->RAX =
                        (registers->RAX & ~0xFFFFUL) |
                        unchecked((ushort)(short)desiredModifier);
                    // Vanilla stores each plague branch before the shared report write.
                    // Keep the authoritative accumulator aligned with the corrected register.
                    *popularityAccumulator = correctedPopularity;
                    IncrementCount(correctedCallbackCounts, playerId);

                    if (GetCount(correctedCallbackCounts, playerId) == 1 ||
                        GetCount(loggedDiagnosticRevisions, playerId) != diagnosticRevision)
                    {
                        loggedDiagnosticRevisions[playerId] = diagnosticRevision;
                        LogDebug(
                            () => $"Plague popularity correction applied: playerId={playerId}, " +
                            $"diagnosticRevision={diagnosticRevision}, herdCount={herdCount}, " +
                            $"livingProjectiles={CountProjectiles(playerId)}, vanillaModifier={vanillaModifier}, " +
                            $"desiredModifier={desiredModifier}, currentPopularity={currentPopularity}, " +
                            $"accumulatorBefore={accumulatorBefore}, correctedPopularity={correctedPopularity}, " +
                            $"callbackCount={GetCount(popularityCallbackCounts, playerId)}, " +
                            $"correctedCallbackCount={GetCount(correctedCallbackCounts, playerId)}.");
                    }
                }
                catch (Exception ex)
                {
                    DisableCorrectionToVanilla("plague popularity callback failed", ex);
                }
            }
        }

        private byte[] SaveState(SaveContext context)
        {
            lock (stateGate)
            {
                if (!context.IsSaveFile || !mapActive || !correctionAvailable || managedPlayerIds.Count == 0)
                    return null;

                try
                {
                    herds.ReconcileAll(isLivingProjectile);
                    int[] players = new int[managedPlayerIds.Count];
                    managedPlayerIds.CopyTo(players);
                    Array.Sort(players);

                    PlagueHerdSaveRecord[] records = herds.ToSaveRecords();

                    return MessagePackSerializer.Serialize(new PlaguePopularitySaveState
                    {
                        ManagedPlayerIds = players,
                        Herds = records
                    });
                }
                catch (Exception ex)
                {
                    DisableCorrectionToVanilla("plague state serialization failed", ex);
                    return null;
                }
            }
        }

        private void LoadState(byte[] bytes, LoadContext context)
        {
            lock (stateGate)
            {
                if (!context.IsSaveFile || !correctionAvailable)
                    return;

                try
                {
                    PlaguePopularitySaveState state =
                        MessagePackSerializer.Deserialize<PlaguePopularitySaveState>(bytes);
                    ValidateSaveState(state);

                    CancelAllDiagnostics();
                    herds.Load(state.Herds);
                    managedPlayerIds.Clear();
                    foreach (int playerId in state.ManagedPlayerIds)
                        managedPlayerIds.Add(playerId);
                    foreach (PlagueHerdSaveRecord record in state.Herds)
                        managedPlayerIds.Add(record.PlayerId);
                    foreach (int playerId in managedPlayerIds)
                        ArmPopularityDiagnostic(playerId);
                }
                catch (Exception ex)
                {
                    CancelAllDiagnostics();
                    herds.Clear();
                    managedPlayerIds.Clear();
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Plague popularity state was rejected; this save keeps Vanilla plague behavior: {ex}");
                }
            }
        }

        private void ResetMapState()
        {
            lock (stateGate)
            {
                mapActive = false;
                currentCapture = null;
                herds.Clear();
                managedPlayerIds.Clear();
                popularityCallbackCounts.Clear();
                correctedCallbackCounts.Clear();
                diagnosticRevisions.Clear();
                loggedDiagnosticRevisions.Clear();
                CancelAllDiagnostics();
            }
        }

        private void LogDebug(Func<string> messageFactory)
        {
            try { Shared.DebugLogHelper.LogDebug(log, messageFactory); }
            catch { /* Logging cannot invalidate native state or a successful correction. */ }
        }

        private void ArmPopularityDiagnostic(int playerId)
        {
            if (!correctionEnabled || !correctionAvailable) return;
            CancelDiagnostic(playerId);
            int revision = GetCount(diagnosticRevisions, playerId) + 1;
            diagnosticRevisions[playerId] = revision;
            long generation = diagnosticGeneration;
            try
            {
                diagnosticTimers[playerId] = GameTimeManagerAPI.Instance.GetTimerEngine().AddDelayedAction(
                    MissingPopularityCallbackWarningMilliseconds,
                    () => ReportMissingPopularityCallback(playerId, revision, generation), null);
            }
            catch (Exception ex)
            {
                // Diagnostics must never disable a working gameplay correction.
                Shared.DebugLogHelper.LogWarning(log, "Plague callback watchdog could not be armed: " + ex);
            }
        }

        private void CancelDiagnostic(int playerId)
        {
            if (!diagnosticTimers.TryGetValue(playerId, out string handle)) return;
            diagnosticTimers.Remove(playerId);
            GameTimeManagerAPI.Instance.GetTimerEngine().RemoveAction(handle);
        }

        private void CancelAllDiagnostics()
        {
            diagnosticGeneration++;
            foreach (string handle in diagnosticTimers.Values)
                GameTimeManagerAPI.Instance.GetTimerEngine().RemoveAction(handle);
            diagnosticTimers.Clear();
        }

        private void ReportMissingPopularityCallback(int playerId, int revision, long generation)
        {
            lock (stateGate)
            {
                if (!correctionEnabled || !correctionAvailable || !mapActive ||
                    diagnosticGeneration != generation || GetCount(diagnosticRevisions, playerId) != revision) return;
                diagnosticTimers.Remove(playerId);
                if (GetCount(loggedDiagnosticRevisions, playerId) == revision) return;
                Shared.DebugLogHelper.LogWarning(log,
                    $"No plague popularity correction callback was observed within {MissingPopularityCallbackWarningMilliseconds} ms of simulation time: " +
                    $"playerId={playerId}, diagnosticRevision={revision}, activeHerds={CountHerds(playerId)}, " +
                    $"livingProjectiles={CountProjectiles(playerId)}, popularityCallbacksObserved={DescribeCallbackCounts()}.");
            }
        }

        private int CountProjectiles(int playerId) => herds.CountProjectiles(playerId);

        private string DescribeCallbackCounts()
        {
            if (popularityCallbackCounts.Count == 0)
                return "[]";

            List<int> playerIds = new List<int>(popularityCallbackCounts.Keys);
            playerIds.Sort();
            List<string> descriptions = new List<string>(playerIds.Count);
            foreach (int playerId in playerIds)
                descriptions.Add($"P{playerId}={popularityCallbackCounts[playerId]}");
            return "[" + string.Join(",", descriptions) + "]";
        }

        private static string DescribeProjectiles(List<PlagueProjectileIdentity> members)
        {
            List<string> descriptions = new List<string>(members.Count);
            foreach (PlagueProjectileIdentity member in members)
                descriptions.Add($"{member.SlotId}/{member.GlobalId}");
            return "[" + string.Join(",", descriptions) + "]";
        }

        private static int GetCount(Dictionary<int, int> counts, int playerId) =>
            counts.TryGetValue(playerId, out int count) ? count : 0;

        private static void IncrementCount(Dictionary<int, int> counts, int playerId) =>
            counts[playerId] = GetCount(counts, playerId) + 1;

        private bool IsLivingDiseaseProjectile(PlagueProjectileIdentity member)
        {
            return GameProjectileManagerAPI.Instance.TryGetProjectileById(member.SlotId, out GameProjectile* projectile) &&
                projectile != null &&
                // Chore-created clouds remain NeedsInit until the surrounding native tick completes.
                (projectile->r_AliveState == AliveState.NeedsInit ||
                    projectile->r_AliveState == AliveState.IsAlive) &&
                projectile->r_ProjectileType == ProjectileType.Disease &&
                projectile->r_GlobalId == member.GlobalId;
        }

        private int CountHerds(int playerId) => herds.CountHerds(playerId);

        private static void ValidateSaveState(PlaguePopularitySaveState state)
        {
            PlaguePopularitySaveLimits limits = PlaguePopularitySaveLimitPolicy.GetCurrent();
            if (state == null || state.Version != PlaguePopularitySaveState.CurrentVersion ||
                state.ManagedPlayerIds == null || state.Herds == null ||
                state.ManagedPlayerIds.Length > limits.MaximumManagedPlayers ||
                state.Herds.Length > limits.MaximumHerds)
            {
                throw new InvalidOperationException("The plague save-data header is invalid.");
            }

            foreach (int playerId in state.ManagedPlayerIds)
            {
                if (!IsValidPlayerId(playerId, limits))
                    throw new InvalidOperationException($"Invalid managed plague player ID: {playerId}.");
            }

            foreach (PlagueHerdSaveRecord record in state.Herds)
            {
                if (record == null || !IsValidPlayerId(record.PlayerId, limits) ||
                    record.ProjectileSlotIds == null || record.ProjectileGlobalIds == null ||
                    record.ProjectileSlotIds.Length != record.ProjectileGlobalIds.Length ||
                    record.ProjectileSlotIds.Length < 1 ||
                    record.ProjectileSlotIds.Length > limits.MaximumProjectilesPerHerd)
                {
                    throw new InvalidOperationException("A saved plague herd is invalid.");
                }

                for (int index = 0; index < record.ProjectileSlotIds.Length; index++)
                {
                    if (record.ProjectileSlotIds[index] < 1 ||
                        record.ProjectileSlotIds[index] > limits.MaximumProjectileSlotId ||
                        record.ProjectileGlobalIds[index] == 0)
                    {
                        throw new InvalidOperationException("A saved plague projectile identity is invalid.");
                    }
                }
            }
        }

        private void DisableCorrectionToVanilla(string reason, Exception ex)
        {
            correctionAvailable = false;
            CancelAllDiagnostics();
            currentCapture = null;
            if (callbackFailureLogged)
                return;

            callbackFailureLogged = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"Plague popularity fix disabled for this process; Vanilla behavior restored because {reason}: {ex}");
        }

        private static bool IsValidPlayerId(int playerId) =>
            IsValidPlayerId(playerId, PlaguePopularitySaveLimitPolicy.GetCurrent());

        private static bool IsValidPlayerId(int playerId, PlaguePopularitySaveLimits limits) =>
            playerId >= 1 && playerId <= limits.MaximumManagedPlayers;

        private sealed class HerdCapture
        {
            public HerdCapture(
                int buildingId,
                uint buildingGlobalId,
                int playerId,
                ushort tileX,
                ushort tileY)
            {
                BuildingId = buildingId;
                BuildingGlobalId = buildingGlobalId;
                PlayerId = playerId;
                TileX = tileX;
                TileY = tileY;
                Members = new List<PlagueProjectileIdentity>(
                    PlaguePopularitySaveLimitPolicy.GetCurrent().MaximumProjectilesPerHerd);
            }

            public int BuildingId { get; }
            public uint BuildingGlobalId { get; }
            public int PlayerId { get; }
            public ushort TileX { get; }
            public ushort TileY { get; }
            public List<PlagueProjectileIdentity> Members { get; }

            public void Add(int slotId, uint globalId)
            {
                for (int index = 0; index < Members.Count; index++)
                {
                    if (Members[index].SlotId == slotId && Members[index].GlobalId == globalId)
                        return;
                }
                Members.Add(new PlagueProjectileIdentity(slotId, globalId));
            }
        }

    }
}
