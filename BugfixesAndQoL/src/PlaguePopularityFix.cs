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
using System.Diagnostics;
using System.Runtime.InteropServices;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

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
        private readonly List<TrackedPlagueHerd> herds = new List<TrackedPlagueHerd>();
        private readonly HashSet<int> managedPlayerIds = new HashSet<int>();
        private readonly Dictionary<int, int> popularityCallbackCounts = new Dictionary<int, int>();
        private readonly Dictionary<int, int> correctedCallbackCounts = new Dictionary<int, int>();
        private readonly Dictionary<int, int> diagnosticRevisions = new Dictionary<int, int>();
        private readonly Dictionary<int, int> loggedDiagnosticRevisions = new Dictionary<int, int>();
        private readonly Dictionary<int, int> warnedDiagnosticRevisions = new Dictionary<int, int>();
        private readonly Dictionary<int, long> diagnosticStartedTimestamps = new Dictionary<int, long>();
        private HookTransaction transaction;
        private HookRef<X64ManagedFunctionDetourAOB<CreateHerdDelegate>> createHerdHook =
            new HookRef<X64ManagedFunctionDetourAOB<CreateHerdDelegate>>();
        private HookRef<X64InlineHook> popularityExitHook = new HookRef<X64InlineHook>();
        private HerdCapture currentCapture;
        private bool saveHandlerRegistered;
        private bool mapActive;
        private bool correctionAvailable = true;
        private bool callbackFailureLogged;
        private int invalidPopularityCallbackCount;
        private int lastInvalidPopularityCallbackPlayerId;
        private bool disposed;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void CreateHerdDelegate(IntPtr diseaseManager, int buildingId);

        public PlaguePopularityFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            int createHerdRva = PlagueNativePatternValidator.Resolve(
                log, memory, CreateHerdPattern, CreateHerdRva, referenceHashMatches, "plague herd creation");
            int popularityExitPatternRva = PlagueNativePatternValidator.Resolve(
                log, memory, PopularityExitPattern, PopularityExitPatternRva, referenceHashMatches, "plague popularity exit");

            try
            {
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddDetour(
                    ref createHerdHook,
                    libraryBase + unchecked((ulong)createHerdRva),
                    CreatePlagueHerd);
                transaction.AddContextHook(
                    ref popularityExitHook,
                    libraryBase + unchecked((ulong)(popularityExitPatternRva + PopularityExitHookOffset)),
                    CorrectPlaguePopularity,
                    regs: X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBP |
                        X64SmartCPUContextRegs.R12 | X64SmartCPUContextRegs.R14,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.Commit();

                if (!createHerdHook.Success || !popularityExitHook.Success)
                    throw new InvalidOperationException("The plague herd or popularity hook was not installed.");

                subscriptions.Add(ProjectileR3EventHooks.OnProjectileSpawn.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnProjectileSpawn));
                subscriptions.Add(ProjectileR3EventHooks.OnProjectileDelete.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnProjectileDelete));
                subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnStartMap));
                GameTimeManagerAPI.Instance.OnTick += OnGameTick;

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
            GameTimeManagerAPI.Instance.OnTick -= OnGameTick;
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            if (saveHandlerRegistered)
            {
                ModSaveDataAPI.Instance.UnregisterModDataHandler(SaveDataIdentifier);
                saveHandlerRegistered = false;
            }
            transaction?.Unload();
            transaction?.Dispose();
            transaction = null;
            ResetMapState();
        }

        private void CreatePlagueHerd(IntPtr diseaseManager, int buildingId)
        {
            if (!correctionAvailable)
            {
                createHerdHook.Value.Hook.Trampoline(diseaseManager, buildingId);
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
                createHerdHook.Value.Hook.Trampoline(diseaseManager, buildingId);
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

                herds.Add(new TrackedPlagueHerd(capture.PlayerId, capture.Members));
                managedPlayerIds.Add(capture.PlayerId);
                ArmPopularityDiagnostic(capture.PlayerId);
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Plague herd captured: sourceBuildingId={capture.BuildingId}, " +
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

        private void OnProjectileSpawn(ProjectileSpawnEventArgs args)
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

        private void OnProjectileDelete(ProjectileDeleteEventArgs args)
        {
            if (!correctionAvailable || herds.Count == 0)
                return;

            try
            {
                RemoveProjectileSlot(args.ProjectileId);
            }
            catch (Exception ex)
            {
                DisableCorrectionToVanilla("Disease projectile deletion tracking failed", ex);
            }
        }

        private void OnStartMap(MapStartEventArgs args)
        {
            mapActive = true;
            Shared.DebugLogHelper.LogDebug(
                log,
                $"Plague popularity diagnostics armed: modEnabled={settings.EnableMod}, " +
                $"fixEnabled={settings.EnablePlaguePopularityFix}, " +
                $"mode={Shared.GameModeHelper.Capture(args.bMultiplayerSave != 0).ToDiagnosticString()}.");
        }

        private void OnGameTick(int _)
        {
            if (!mapActive || !correctionAvailable)
                return;

            try
            {
                if (herds.Count != 0)
                    PruneInvalidProjectiles();
                ReportMissingPopularityCallbacks();
            }
            catch (Exception ex)
            {
                DisableCorrectionToVanilla("plague projectile reconciliation failed", ex);
            }
        }

        private void CorrectPlaguePopularity(NativePointer<X64SmartCPUContext> context)
        {
            if (!correctionAvailable || !mapActive)
                return;

            try
            {
                X64SmartCPUContext* registers = context.Pointer;
                int playerId = unchecked((int)(uint)registers->R14);
                if (playerId >= 0 && playerId <= PlaguePopularitySaveLimitPolicy.GetCurrent().MaximumManagedPlayers)
                    IncrementCount(popularityCallbackCounts, playerId);
                else
                {
                    invalidPopularityCallbackCount++;
                    lastInvalidPopularityCallbackPlayerId = playerId;
                }
                if (!managedPlayerIds.Contains(playerId))
                    return;

                int diagnosticRevision = GetCount(diagnosticRevisions, playerId);
                if (!settings.EnableMod || !settings.EnablePlaguePopularityFix)
                {
                    if (GetCount(loggedDiagnosticRevisions, playerId) != diagnosticRevision)
                    {
                        loggedDiagnosticRevisions[playerId] = diagnosticRevision;
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            $"Plague popularity callback skipped by settings: playerId={playerId}, " +
                            $"modEnabled={settings.EnableMod}, fixEnabled={settings.EnablePlaguePopularityFix}, " +
                            $"callbackCount={GetCount(popularityCallbackCounts, playerId)}, " +
                            $"activeHerds={CountHerds(playerId)}.");
                    }
                    return;
                }

                // Read-only native validation makes natural expiry effective in the
                // same popularity pass even if no delete event was emitted.
                PruneInvalidProjectiles();
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

                if (GetCount(loggedDiagnosticRevisions, playerId) != diagnosticRevision)
                {
                    loggedDiagnosticRevisions[playerId] = diagnosticRevision;
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        $"Plague popularity correction applied: playerId={playerId}, " +
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

        private byte[] SaveState(SaveContext context)
        {
            if (!context.IsSaveFile || !mapActive || !correctionAvailable || managedPlayerIds.Count == 0)
                return null;

            try
            {
                PruneInvalidProjectiles();
                int[] players = new int[managedPlayerIds.Count];
                managedPlayerIds.CopyTo(players);
                Array.Sort(players);

                PlagueHerdSaveRecord[] records = new PlagueHerdSaveRecord[herds.Count];
                for (int herdIndex = 0; herdIndex < herds.Count; herdIndex++)
                    records[herdIndex] = herds[herdIndex].ToSaveRecord();

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

        private void LoadState(byte[] bytes, LoadContext context)
        {
            if (!context.IsSaveFile || !correctionAvailable)
                return;

            try
            {
                PlaguePopularitySaveState state =
                    MessagePackSerializer.Deserialize<PlaguePopularitySaveState>(bytes);
                ValidateSaveState(state);

                herds.Clear();
                managedPlayerIds.Clear();
                foreach (int playerId in state.ManagedPlayerIds)
                    managedPlayerIds.Add(playerId);
                foreach (PlagueHerdSaveRecord record in state.Herds)
                {
                    TrackedPlagueHerd herd = TrackedPlagueHerd.FromSaveRecord(record);
                    herds.Add(herd);
                    managedPlayerIds.Add(herd.PlayerId);
                }
                foreach (int playerId in managedPlayerIds)
                    ArmPopularityDiagnostic(playerId);
            }
            catch (Exception ex)
            {
                herds.Clear();
                managedPlayerIds.Clear();
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Plague popularity state was rejected; this save keeps Vanilla plague behavior: {ex}");
            }
        }

        private void ResetMapState()
        {
            mapActive = false;
            currentCapture = null;
            herds.Clear();
            managedPlayerIds.Clear();
            popularityCallbackCounts.Clear();
            correctedCallbackCounts.Clear();
            diagnosticRevisions.Clear();
            loggedDiagnosticRevisions.Clear();
            warnedDiagnosticRevisions.Clear();
            diagnosticStartedTimestamps.Clear();
            invalidPopularityCallbackCount = 0;
            lastInvalidPopularityCallbackPlayerId = 0;
        }

        private void ArmPopularityDiagnostic(int playerId)
        {
            diagnosticRevisions[playerId] = GetCount(diagnosticRevisions, playerId) + 1;
            diagnosticStartedTimestamps[playerId] = Stopwatch.GetTimestamp();
        }

        private void ReportMissingPopularityCallbacks()
        {
            long now = Stopwatch.GetTimestamp();
            foreach (int playerId in managedPlayerIds)
            {
                int revision = GetCount(diagnosticRevisions, playerId);
                if (revision == 0 || GetCount(loggedDiagnosticRevisions, playerId) == revision ||
                    GetCount(warnedDiagnosticRevisions, playerId) == revision ||
                    !diagnosticStartedTimestamps.TryGetValue(playerId, out long started) ||
                    (now - started) * 1000L <
                        MissingPopularityCallbackWarningMilliseconds * Stopwatch.Frequency)
                {
                    continue;
                }

                warnedDiagnosticRevisions[playerId] = revision;
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"No plague popularity correction callback was observed within " +
                    $"{MissingPopularityCallbackWarningMilliseconds} ms after herd capture: " +
                    $"playerId={playerId}, diagnosticRevision={revision}, " +
                    $"activeHerds={CountHerds(playerId)}, livingProjectiles={CountProjectiles(playerId)}, " +
                    $"popularityCallbacksObserved={DescribeCallbackCounts()}, " +
                    $"modEnabled={settings.EnableMod}, fixEnabled={settings.EnablePlaguePopularityFix}, " +
                    $"mode={Shared.GameModeHelper.Capture().ToDiagnosticString()}.");
            }
        }

        private int CountProjectiles(int playerId)
        {
            int count = 0;
            for (int herdIndex = 0; herdIndex < herds.Count; herdIndex++)
            {
                if (herds[herdIndex].PlayerId == playerId)
                    count += herds[herdIndex].Members.Count;
            }
            return count;
        }

        private string DescribeCallbackCounts()
        {
            if (popularityCallbackCounts.Count == 0 && invalidPopularityCallbackCount == 0)
                return "[]";

            List<int> playerIds = new List<int>(popularityCallbackCounts.Keys);
            playerIds.Sort();
            List<string> descriptions = new List<string>(playerIds.Count);
            foreach (int playerId in playerIds)
                descriptions.Add($"P{playerId}={popularityCallbackCounts[playerId]}");
            if (invalidPopularityCallbackCount != 0)
            {
                descriptions.Add(
                    $"invalid={invalidPopularityCallbackCount}/lastRaw={lastInvalidPopularityCallbackPlayerId}");
            }
            return "[" + string.Join(",", descriptions) + "]";
        }

        private static string DescribeProjectiles(List<ProjectileIdentity> members)
        {
            List<string> descriptions = new List<string>(members.Count);
            foreach (ProjectileIdentity member in members)
                descriptions.Add($"{member.SlotId}/{member.GlobalId}");
            return "[" + string.Join(",", descriptions) + "]";
        }

        private static int GetCount(Dictionary<int, int> counts, int playerId) =>
            counts.TryGetValue(playerId, out int count) ? count : 0;

        private static void IncrementCount(Dictionary<int, int> counts, int playerId) =>
            counts[playerId] = GetCount(counts, playerId) + 1;

        private void PruneInvalidProjectiles()
        {
            for (int herdIndex = herds.Count - 1; herdIndex >= 0; herdIndex--)
            {
                TrackedPlagueHerd herd = herds[herdIndex];
                for (int memberIndex = herd.Members.Count - 1; memberIndex >= 0; memberIndex--)
                {
                    ProjectileIdentity member = herd.Members[memberIndex];
                    if (!IsLivingDiseaseProjectile(member))
                        herd.Members.RemoveAt(memberIndex);
                }

                if (herd.Members.Count == 0)
                {
                    bool lastHerdForPlayer = CountHerds(herd.PlayerId) == 1;
                    herds.RemoveAt(herdIndex);
                    if (lastHerdForPlayer)
                        LogHerdEnded(herd.PlayerId, "projectile reconciliation");
                }
            }
        }

        private bool IsLivingDiseaseProjectile(ProjectileIdentity member)
        {
            return GameProjectileManagerAPI.Instance.TryGetProjectileById(member.SlotId, out GameProjectile* projectile) &&
                projectile != null &&
                // Chore-created clouds remain NeedsInit until the surrounding native tick completes.
                (projectile->r_AliveState == AliveState.NeedsInit ||
                    projectile->r_AliveState == AliveState.IsAlive) &&
                projectile->r_ProjectileType == ProjectileType.Disease &&
                projectile->r_GlobalId == member.GlobalId;
        }

        private void RemoveProjectileSlot(int projectileId)
        {
            for (int herdIndex = herds.Count - 1; herdIndex >= 0; herdIndex--)
            {
                TrackedPlagueHerd herd = herds[herdIndex];
                List<ProjectileIdentity> members = herd.Members;
                for (int memberIndex = members.Count - 1; memberIndex >= 0; memberIndex--)
                {
                    if (members[memberIndex].SlotId == projectileId)
                    {
                        members.RemoveAt(memberIndex);
                        if (members.Count == 0)
                        {
                            bool lastHerdForPlayer = CountHerds(herd.PlayerId) == 1;
                            herds.RemoveAt(herdIndex);
                            if (lastHerdForPlayer)
                                LogHerdEnded(herd.PlayerId, $"projectile delete event for slot {projectileId}");
                        }
                        return;
                    }
                }
            }
        }

        private void LogHerdEnded(int playerId, string reason)
        {
            int revision = GetCount(diagnosticRevisions, playerId);
            bool correctionObserved = GetCount(loggedDiagnosticRevisions, playerId) == revision;
            string message =
                $"Plague herd ended: playerId={playerId}, reason={reason}, " +
                $"diagnosticRevision={revision}, correctionObserved={correctionObserved}, " +
                $"callbackCount={GetCount(popularityCallbackCounts, playerId)}, " +
                $"correctedCallbackCount={GetCount(correctedCallbackCounts, playerId)}, " +
                $"popularityCallbacksObserved={DescribeCallbackCounts()}.";
            if (correctionObserved)
                Shared.DebugLogHelper.LogDebug(log, message);
            else
                Shared.DebugLogHelper.LogWarning(log, message);
        }

        private int CountHerds(int playerId)
        {
            int count = 0;
            for (int index = 0; index < herds.Count; index++)
            {
                if (herds[index].PlayerId == playerId)
                    count++;
            }
            return count;
        }

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
                Members = new List<ProjectileIdentity>(
                    PlaguePopularitySaveLimitPolicy.GetCurrent().MaximumProjectilesPerHerd);
            }

            public int BuildingId { get; }
            public uint BuildingGlobalId { get; }
            public int PlayerId { get; }
            public ushort TileX { get; }
            public ushort TileY { get; }
            public List<ProjectileIdentity> Members { get; }

            public void Add(int slotId, uint globalId)
            {
                for (int index = 0; index < Members.Count; index++)
                {
                    if (Members[index].SlotId == slotId && Members[index].GlobalId == globalId)
                        return;
                }
                Members.Add(new ProjectileIdentity(slotId, globalId));
            }
        }

        private sealed class TrackedPlagueHerd
        {
            public TrackedPlagueHerd(int playerId, List<ProjectileIdentity> members)
            {
                PlayerId = playerId;
                Members = members ?? throw new ArgumentNullException(nameof(members));
            }

            public int PlayerId { get; }
            public List<ProjectileIdentity> Members { get; }

            public PlagueHerdSaveRecord ToSaveRecord()
            {
                int[] slots = new int[Members.Count];
                uint[] globals = new uint[Members.Count];
                for (int index = 0; index < Members.Count; index++)
                {
                    slots[index] = Members[index].SlotId;
                    globals[index] = Members[index].GlobalId;
                }
                return new PlagueHerdSaveRecord
                {
                    PlayerId = PlayerId,
                    ProjectileSlotIds = slots,
                    ProjectileGlobalIds = globals
                };
            }

            public static TrackedPlagueHerd FromSaveRecord(PlagueHerdSaveRecord record)
            {
                List<ProjectileIdentity> members = new List<ProjectileIdentity>(record.ProjectileSlotIds.Length);
                for (int index = 0; index < record.ProjectileSlotIds.Length; index++)
                    members.Add(new ProjectileIdentity(record.ProjectileSlotIds[index], record.ProjectileGlobalIds[index]));
                return new TrackedPlagueHerd(record.PlayerId, members);
            }
        }

        private readonly struct ProjectileIdentity
        {
            public ProjectileIdentity(int slotId, uint globalId)
            {
                SlotId = slotId;
                GlobalId = globalId;
            }

            public int SlotId { get; }
            public uint GlobalId { get; }
        }
    }
}
