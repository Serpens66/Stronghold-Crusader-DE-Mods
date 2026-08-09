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
using System.Globalization;
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
        private const int MaximumPlayerId = 8;
        private const int MinimumProjectilesPerHerd = 6;
        private const int MaximumProjectilesPerHerd = 10;
        private const int PopularityPointsPerHerd = 25;

        // c_game_disease_create_one_herd, reference RVA 0xD1780.
        private const string CreateHerdPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 " +
            "48 83 EC 60 4C 8D 2D ?? ?? ?? ?? 48 63 C2 48 69 C8 2C 03 00 00";

        // Common exit of Vanilla's plague-popularity block. The hook starts at
        // the report-field write at reference RVA 0xCB52C (pattern + 32).
        private const string PopularityExitPattern =
            "B9 E7 FF FF FF 0F 4E C1 03 D0 41 89 94 2C 20 EC 12 00 EB 0C " +
            "45 89 AC 2C 84 0E 13 00 41 0F B7 C5 " +
            "66 41 89 84 2C 78 0E 13 00 41 0F B7 84 2C 6E 0E 13 00";

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly List<TrackedPlagueHerd> herds = new List<TrackedPlagueHerd>();
        private readonly HashSet<int> managedPlayerIds = new HashSet<int>();
        private HookTransaction transaction;
        private HookRef<X64ManagedFunctionDetourAOB<CreateHerdDelegate>> createHerdHook =
            new HookRef<X64ManagedFunctionDetourAOB<CreateHerdDelegate>>();
        private HookRef<X64InlineHook> popularityExitHook = new HookRef<X64InlineHook>();
        private HerdCapture currentCapture;
        private bool saveHandlerRegistered;
        private bool mapActive;
        private bool loadedValidationPending;
        private bool correctionAvailable = true;
        private bool callbackFailureLogged;
        private bool disposed;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void CreateHerdDelegate(IntPtr diseaseManager, int buildingId);

        public PlaguePopularityFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ReadOnlySpan<byte> memory,
            ulong libraryBase)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            int createHerdOffset = RequireUniquePattern(memory, CreateHerdPattern, "plague herd creation");
            int popularityExitOffset = RequireUniquePattern(memory, PopularityExitPattern, "plague popularity exit") + 32;

            try
            {
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddDetour(ref createHerdHook, CreateHerdPattern, CreatePlagueHerd);
                transaction.AddContextHook(
                    ref popularityExitHook,
                    PopularityExitPattern,
                    CorrectPlaguePopularity,
                    regs: X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.R14,
                    patternOffset: 32,
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

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Plague popularity fix installed: createHerdRva=0x{createHerdOffset:X}, " +
                    $"popularityExitRva=0x{popularityExitOffset:X}.");
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
                    capture = new HerdCapture(building->r_PlayerIdOwner);
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
            bool completed = false;
            try
            {
                // Vanilla remains authoritative for all projectile creation.
                createHerdHook.Value.Hook.Trampoline(diseaseManager, buildingId);
                completed = true;
            }
            finally
            {
                currentCapture = previousCapture;
            }

            if (!completed || capture == null || !correctionAvailable)
                return;

            try
            {
                if (capture.Members.Count < MinimumProjectilesPerHerd ||
                    capture.Members.Count > MaximumProjectilesPerHerd)
                {
                    throw new InvalidOperationException(
                        $"Vanilla created an unexpected plague-herd size: " +
                        $"playerId={capture.PlayerId}, projectileCount={capture.Members.Count}.");
                }

                herds.Add(new TrackedPlagueHerd(capture.PlayerId, capture.Members));
                managedPlayerIds.Add(capture.PlayerId);
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Tracked plague herd: playerId={capture.PlayerId}, " +
                    $"projectileCount={capture.Members.Count}, activeHerds={CountHerds(capture.PlayerId)}.");
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
            if (!correctionAvailable)
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
        }

        private void OnGameTick(int tick)
        {
            if (!mapActive || !correctionAvailable)
                return;

            try
            {
                PruneInvalidProjectiles();
                if (loadedValidationPending)
                {
                    loadedValidationPending = false;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Validated loaded plague state: managedPlayers={managedPlayerIds.Count}, " +
                        $"activeHerds={herds.Count}.");
                }
            }
            catch (Exception ex)
            {
                DisableCorrectionToVanilla("plague projectile reconciliation failed", ex);
            }
        }

        private void CorrectPlaguePopularity(NativePointer<X64SmartCPUContext> context)
        {
            if (!correctionAvailable || !mapActive || !settings.EnableMod || !settings.EnablePlaguePopularityFix)
                return;

            try
            {
                X64SmartCPUContext* registers = context.Pointer;
                int playerId = unchecked((int)(uint)registers->R14);
                if (!managedPlayerIds.Contains(playerId))
                    return;

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

                registers->RDX = unchecked((uint)correctedPopularity);
                registers->RAX =
                    (registers->RAX & ~0xFFFFUL) |
                    unchecked((ushort)(short)desiredModifier);
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

                // Native projectile arrays are authoritative only after map data has loaded.
                loadedValidationPending = true;
            }
            catch (Exception ex)
            {
                herds.Clear();
                managedPlayerIds.Clear();
                loadedValidationPending = false;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Plague popularity state was rejected; this save keeps Vanilla plague behavior: {ex}");
            }
        }

        private void ResetMapState()
        {
            mapActive = false;
            loadedValidationPending = false;
            currentCapture = null;
            herds.Clear();
            managedPlayerIds.Clear();
        }

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
                    herds.RemoveAt(herdIndex);
            }
        }

        private bool IsLivingDiseaseProjectile(ProjectileIdentity member)
        {
            return GameProjectileManagerAPI.Instance.TryGetProjectileById(member.SlotId, out GameProjectile* projectile) &&
                projectile != null &&
                projectile->r_AliveState == AliveState.IsAlive &&
                projectile->r_ProjectileType == ProjectileType.Disease &&
                projectile->r_GlobalId == member.GlobalId;
        }

        private void RemoveProjectileSlot(int projectileId)
        {
            for (int herdIndex = herds.Count - 1; herdIndex >= 0; herdIndex--)
            {
                List<ProjectileIdentity> members = herds[herdIndex].Members;
                for (int memberIndex = members.Count - 1; memberIndex >= 0; memberIndex--)
                {
                    if (members[memberIndex].SlotId == projectileId)
                        members.RemoveAt(memberIndex);
                }
                if (members.Count == 0)
                    herds.RemoveAt(herdIndex);
            }
        }

        private int CountHerds(int playerId)
        {
            int count = 0;
            for (int index = 0; index < herds.Count; index++)
            {
                if (herds[index].PlayerId == playerId && herds[index].Members.Count > 0)
                    count++;
            }
            return count;
        }

        private static void ValidateSaveState(PlaguePopularitySaveState state)
        {
            if (state == null || state.Version != PlaguePopularitySaveState.CurrentVersion ||
                state.ManagedPlayerIds == null || state.Herds == null ||
                state.ManagedPlayerIds.Length > MaximumPlayerId || state.Herds.Length > 4096)
            {
                throw new InvalidOperationException("The plague save-data header is invalid.");
            }

            foreach (int playerId in state.ManagedPlayerIds)
            {
                if (!IsValidPlayerId(playerId))
                    throw new InvalidOperationException($"Invalid managed plague player ID: {playerId}.");
            }

            foreach (PlagueHerdSaveRecord record in state.Herds)
            {
                if (record == null || !IsValidPlayerId(record.PlayerId) ||
                    record.ProjectileSlotIds == null || record.ProjectileGlobalIds == null ||
                    record.ProjectileSlotIds.Length != record.ProjectileGlobalIds.Length ||
                    record.ProjectileSlotIds.Length < 1 ||
                    record.ProjectileSlotIds.Length > MaximumProjectilesPerHerd)
                {
                    throw new InvalidOperationException("A saved plague herd is invalid.");
                }

                for (int index = 0; index < record.ProjectileSlotIds.Length; index++)
                {
                    if (record.ProjectileSlotIds[index] < 1 || record.ProjectileSlotIds[index] > 10000 ||
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

        private static bool IsValidPlayerId(int playerId) => playerId >= 1 && playerId <= MaximumPlayerId;

        private static int RequireUniquePattern(ReadOnlySpan<byte> memory, string pattern, string name)
        {
            string[] tokens = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int[] expected = new int[tokens.Length];
            for (int index = 0; index < tokens.Length; index++)
            {
                if (tokens[index] == "??")
                {
                    expected[index] = -1;
                    continue;
                }

                if (!byte.TryParse(tokens[index], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte value))
                    throw new InvalidOperationException($"Invalid AOB token '{tokens[index]}' in {name}.");
                expected[index] = value;
            }

            int matchCount = 0;
            int matchOffset = -1;
            for (int offset = 0; offset <= memory.Length - expected.Length; offset++)
            {
                bool matches = true;
                for (int index = 0; index < expected.Length; index++)
                {
                    if (expected[index] >= 0 && memory[offset + index] != expected[index])
                    {
                        matches = false;
                        break;
                    }
                }

                if (!matches)
                    continue;
                matchOffset = offset;
                matchCount++;
                if (matchCount > 1)
                    break;
            }

            if (matchCount != 1)
                throw new InvalidOperationException($"The {name} signature matched {matchCount} times instead of exactly once.");
            return matchOffset;
        }

        private sealed class HerdCapture
        {
            public HerdCapture(int playerId)
            {
                PlayerId = playerId;
            }

            public int PlayerId { get; }
            public List<ProjectileIdentity> Members { get; } = new List<ProjectileIdentity>(MaximumProjectilesPerHerd);

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
                Members = new List<ProjectileIdentity>(members);
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
