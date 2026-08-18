using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace RandomEvents
{
    internal sealed unsafe class ScenarioSignpostRegistry
    {
        private const int SlotCount = 8;
        private const int ReferenceSignpostIdsOffset = 0x18388C;
        private const int ExpectedLookupFunctionRva = 0xCB7B0;
        private const int CandidateValidationWindow = 0x240;
        private const int AttackPointDeltaFromSlots = 0x1B2C;
        private const int RabbitPointDeltaFromSlots = 0x19C;
        private const int ScenarioPointCount = 4;
        private const int ExpectedRabbitHandlerRva = 0x10487A;
        private const int ExpectedRabbitPredicateRva = 0x117700;
        private const int ExpectedRabbitSpawnerRva = 0x123A20;
        private const uint RabbitRejectedTileMask = 0x50501581;
        private const int RabbitSourceSearchRadius = 12;

        // CrusaderDE.dll SHA-256 1E6D4C2E10CC35A7B8082A7E2BCD8BB20680EBEDA803D9B943257B948145CB2B.
        // RVA 0xCB7B0 reads eight building IDs at gPlayerManager+0x18388C and accepts STRUCT_SIGNPOST (52).
        // Mutable displacements and branch lengths are wildcarded so compatible game updates can still resolve it.
        private const string LookupPattern =
            "48 63 81 ?? ?? ?? ?? 4C 8D 1D ?? ?? ?? ?? 45 33 D2 4C 8B C9 45 8B C2 85 C0 7E ?? " +
            "48 69 D0 2C 03 00 00 B8 01 00 00 00 66 42 83 BC 1A 2E 01 00 00 34";
        private const string RabbitHandlerPattern =
            "48 8D 1D ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ?? 85 C0 0F 84 ?? ?? ?? ?? " +
            "8B 15 ?? ?? ?? ?? 48 8D 3D ?? ?? ?? ?? 48 8B CF 41 B8 1F 00 00 00 E8 ?? ?? ?? ??";
        private const string RabbitPredicatePattern =
            "83 3D ?? ?? ?? ?? 00 75 ?? 81 3D ?? ?? ?? ?? A0 00 00 00 7D ?? 33 C0 " +
            "4C 8D 0D ?? ?? ?? ?? 41 BA 1F 03 00 00 48 83 F8 04 7D ?? 4D 0F BF 84 81 ?? ?? ?? ??";
        private const string RabbitSpawnerPattern =
            "48 8B C4 41 57 48 83 EC 50 83 3D ?? ?? ?? ?? 00 4C 8B F9 0F 85 ?? ?? ?? ?? " +
            "81 3D ?? ?? ?? ?? A0 00 00 00";
        private const string RabbitTileMaskPattern =
            "43 8B 84 B4 ?? ?? ?? ?? A9 81 15 50 50 74 ?? 0F BA E0 0C";

        private readonly ManualLogSource log;
        private IntPtr slotsAddress;
        private IntPtr attackPointsAddress;
        private IntPtr rabbitPointsAddress;
        private IntPtr rabbitGlobalGateAddress;
        private IntPtr rabbitCountAddress;
        private string unavailableReason = "native compatibility resolution has not run.";
        private string targetingUnavailableReason = "native event targeting compatibility resolution has not run.";

        public ScenarioSignpostRegistry(ManualLogSource log)
        {
            this.log = log;
        }

        public bool IsAvailable => slotsAddress != IntPtr.Zero;
        public string UnavailableReason => unavailableReason;

        public void InitializeNative(IntPtr libraryHandle, ReadOnlySpan<byte> memory, bool referenceHashMatches)
        {
            slotsAddress = IntPtr.Zero;
            attackPointsAddress = IntPtr.Zero;
            rabbitPointsAddress = IntPtr.Zero;
            rabbitGlobalGateAddress = IntPtr.Zero;
            rabbitCountAddress = IntPtr.Zero;
            unavailableReason = "native compatibility resolution failed.";
            targetingUnavailableReason = "native event targeting compatibility resolution failed.";

            try
            {
                LogInfo(
                    $"Native signpost compatibility scan started: referenceHashMatch={referenceHashMatches}, " +
                    "strategies=semantic AOB/reference RVA/structural scan.");
                NativeLookupResolution resolution = ResolveLookup(memory, referenceHashMatches);

                ulong playerManager = GameGlobalsManager.Instance.GamePlayerManagerVA;
                if (playerManager == 0)
                    throw new InvalidOperationException("gPlayerManager is unavailable.");

                slotsAddress = new IntPtr(checked((long)playerManager + resolution.SignpostIdsOffset));
                unavailableReason = string.Empty;
                LogInfo(
                    $"Native signpost registry ready: strategy={resolution.Strategy}, " +
                    $"lookupRva=0x{resolution.FunctionRva:X}, lookupVa=0x{libraryHandle.ToInt64() + resolution.FunctionRva:X16}, " +
                    $"slotOffset=0x{resolution.SignpostIdsOffset:X}, slots=0x{slotsAddress.ToInt64():X16}, count={SlotCount}.");

                if (!referenceHashMatches ||
                    resolution.FunctionRva != ExpectedLookupFunctionRva ||
                    resolution.SignpostIdsOffset != ReferenceSignpostIdsOffset)
                {
                    LogInfo(
                        "Native signpost compatibility fallback accepted a structurally compatible DLL: " +
                        $"referenceRva=0x{ExpectedLookupFunctionRva:X}, actualRva=0x{resolution.FunctionRva:X}, " +
                        $"referenceSlotOffset=0x{ReferenceSignpostIdsOffset:X}, actualSlotOffset=0x{resolution.SignpostIdsOffset:X}.");
                }

                InitializeTargetingFields(resolution.SignpostIdsOffset, playerManager);
                InitializeRabbitCompatibility(libraryHandle, memory, referenceHashMatches);
            }
            catch (Exception ex)
            {
                slotsAddress = IntPtr.Zero;
                attackPointsAddress = IntPtr.Zero;
                rabbitPointsAddress = IntPtr.Zero;
                rabbitGlobalGateAddress = IntPtr.Zero;
                rabbitCountAddress = IntPtr.Zero;
                unavailableReason = ex.Message;
                LogError(
                    "Native signpost compatibility validation failed. Automatic edge-signpost discovery, placement, and " +
                    "registration are disabled; random-event scheduling, direct Vanilla events, and timeline events remain active. " +
                    $"Vanilla can still use signposts already registered by the loaded scenario. Reason: {ex}");
            }
        }

        public bool HasUsableRegisteredSignpost()
        {
            foreach (int buildingId in ReadRegisteredBuildingIds())
            {
                if (TryGetUsableSignpost(buildingId, out _))
                    return true;
            }
            return false;
        }

        public bool TryBeginTargetedEvent(
            int targetPlayerId,
            bool rabbitEvent,
            out IDisposable scope,
            out int signpostBuildingId,
            out double signpostDistance,
            out string failure)
        {
            scope = null;
            signpostBuildingId = -1;
            signpostDistance = double.MaxValue;
            failure = string.Empty;

            if (!IsAvailable || attackPointsAddress == IntPtr.Zero ||
                (rabbitEvent &&
                    (rabbitPointsAddress == IntPtr.Zero ||
                     rabbitGlobalGateAddress == IntPtr.Zero ||
                     rabbitCountAddress == IntPtr.Zero)))
            {
                failure = string.IsNullOrEmpty(targetingUnavailableReason) ? unavailableReason : targetingUnavailableReason;
                return false;
            }

            int keepId = GamePlayerManagerAPI.Instance.GetPlayerKeepId(targetPlayerId);
            if (keepId <= 0 ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(keepId, out GameBuilding* keep) ||
                (keep->r_AliveState != AliveState.NeedsInit && keep->r_AliveState != AliveState.IsAlive))
            {
                failure = $"target player {targetPlayerId} has no usable keep.";
                return false;
            }

            double keepX = (keep->r_TilePositionXBegin + keep->r_TilePositionXEnd) / 2.0;
            double keepY = (keep->r_TilePositionYBegin + keep->r_TilePositionYEnd) / 2.0;
            int[] originalSlots = ReadRegisteredBuildingIds();
            List<SignpostDistance> usable = new List<SignpostDistance>();
            HashSet<int> seen = new HashSet<int>();
            foreach (int buildingId in originalSlots)
            {
                if (!seen.Add(buildingId) || !TryGetUsableSignpost(buildingId, out GameBuilding* signpost))
                    continue;

                double x = (signpost->r_TilePositionXBegin + signpost->r_TilePositionXEnd) / 2.0;
                double y = (signpost->r_TilePositionYBegin + signpost->r_TilePositionYEnd) / 2.0;
                double deltaX = x - keepX;
                double deltaY = y - keepY;
                usable.Add(new SignpostDistance(
                    buildingId,
                    Math.Sqrt(deltaX * deltaX + deltaY * deltaY),
                    (short)Math.Round(x),
                    (short)Math.Round(y)));
            }

            if (usable.Count == 0)
            {
                failure = "no alive registered Vanilla signpost exists.";
                return false;
            }

            usable.Sort((left, right) => left.Distance.CompareTo(right.Distance));
            short[] originalAttackPoints = ReadScenarioPoints(attackPointsAddress);
            short[] originalRabbitPoints = rabbitPointsAddress != IntPtr.Zero
                ? ReadScenarioPoints(rabbitPointsAddress)
                : null;
            short[] rabbitScenarioPoints = null;
            if (rabbitEvent &&
                !TryCreateRabbitScenarioPoints(usable[0], out rabbitScenarioPoints, out failure))
            {
                LogError(
                    $"Rabbit event source selection failed safely: signpostBuildingId={usable[0].BuildingId}, " +
                    $"reason={failure}");
                return false;
            }

            // Vanilla rabbits read cached source coordinates that are not initialized by dynamic ID registration.
            // Feed their native scenario-coordinate path with points taken directly from the selected signpost.
            try
            {
                WriteScenarioPoints(attackPointsAddress, CreateDisabledScenarioPoints());
                if (rabbitEvent)
                {
                    WriteScenarioPoints(rabbitPointsAddress, rabbitScenarioPoints);
                    for (int slot = 0; slot < SlotCount; slot++)
                        Marshal.WriteInt32(slotsAddress, slot * sizeof(int), 0);
                }
                else
                {
                    if (rabbitPointsAddress != IntPtr.Zero)
                        WriteScenarioPoints(rabbitPointsAddress, CreateDisabledScenarioPoints());
                    for (int slot = 0; slot < SlotCount; slot++)
                    {
                        int buildingId = slot < usable.Count ? usable[slot].BuildingId : 0;
                        Marshal.WriteInt32(slotsAddress, slot * sizeof(int), buildingId);
                    }
                }
            }
            catch (Exception ex)
            {
                // Never leave Vanilla's scenario sources partially reordered after a failed native write.
                RestoreTargetingFields(originalSlots, originalAttackPoints, originalRabbitPoints);
                failure = $"temporary native source prioritization failed: {ex.Message}";
                LogError($"Native event targeting failed safely: {failure}");
                return false;
            }

            signpostBuildingId = usable[0].BuildingId;
            signpostDistance = usable[0].Distance;
            scope = new TargetingScope(
                slotsAddress,
                attackPointsAddress,
                rabbitPointsAddress,
                originalSlots,
                originalAttackPoints,
                originalRabbitPoints);
            LogInfo(
                $"Vanilla spawn source prioritized for target: targetPlayerId={targetPlayerId}, " +
                $"signpostBuildingId={signpostBuildingId}, distanceToKeep={signpostDistance:0.00}, " +
                $"usableSignposts={usable.Count}, sourceMode={(rabbitEvent ? "rabbit-scenario-coordinates" : "registered-signpost-slots")}, " +
                $"sourceTile=({usable[0].TileX},{usable[0].TileY}).");
            return true;
        }

        public bool TryGetRabbitNativeState(out int globalGate, out int rabbitCount, out string failure)
        {
            globalGate = 0;
            rabbitCount = 0;
            failure = string.Empty;
            if (rabbitGlobalGateAddress == IntPtr.Zero || rabbitCountAddress == IntPtr.Zero)
            {
                failure = string.IsNullOrEmpty(targetingUnavailableReason)
                    ? "native rabbit compatibility is unavailable."
                    : targetingUnavailableReason;
                return false;
            }

            try
            {
                globalGate = Marshal.ReadInt32(rabbitGlobalGateAddress);
                rabbitCount = Marshal.ReadInt32(rabbitCountAddress);
                return true;
            }
            catch (Exception ex)
            {
                failure = $"native rabbit state read failed: {ex.Message}";
                return false;
            }
        }

        public int[] ReadRegisteredBuildingIds()
        {
            if (!IsAvailable)
                return Array.Empty<int>();

            int[] result = new int[SlotCount];
            for (int slot = 0; slot < SlotCount; slot++)
                result[slot] = Marshal.ReadInt32(slotsAddress, slot * sizeof(int));
            return result;
        }

        public bool HasFreeSlot()
        {
            if (!IsAvailable)
                return false;
            for (int slot = 0; slot < SlotCount; slot++)
            {
                if (Marshal.ReadInt32(slotsAddress, slot * sizeof(int)) <= 0)
                    return true;
            }
            return false;
        }

        public bool TryRegister(int buildingId, out int registeredSlot)
        {
            registeredSlot = -1;
            if (!IsAvailable)
            {
                LogWarning($"Native signpost registration no-op: buildingId={buildingId}, reason=registry unavailable.");
                return false;
            }

            if (buildingId <= 0 ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                building->r_BuildingType != eStructs.STRUCT_SIGNPOST ||
                (building->r_AliveState != AliveState.NeedsInit && building->r_AliveState != AliveState.IsAlive))
            {
                LogWarning($"Native signpost registration rejected invalid building: buildingId={buildingId}.");
                return false;
            }

            for (int slot = 0; slot < SlotCount; slot++)
            {
                if (Marshal.ReadInt32(slotsAddress, slot * sizeof(int)) == buildingId)
                {
                    registeredSlot = slot;
                    return true;
                }
            }

            for (int slot = 0; slot < SlotCount; slot++)
            {
                IntPtr slotAddress = IntPtr.Add(slotsAddress, slot * sizeof(int));
                if (Marshal.ReadInt32(slotAddress) > 0)
                    continue;

                Marshal.WriteInt32(slotAddress, buildingId);
                if (Marshal.ReadInt32(slotAddress) == buildingId)
                {
                    registeredSlot = slot;
                    LogInfo($"Registered Vanilla scenario signpost: buildingId={buildingId}, slot={slot}.");
                    return true;
                }
            }

            LogWarning($"Native signpost registration no-op: buildingId={buildingId}, reason=all eight Vanilla slots occupied.");
            return false;
        }

        private void InitializeTargetingFields(int signpostIdsOffset, ulong playerManager)
        {
            int attackPointsOffset = signpostIdsOffset - AttackPointDeltaFromSlots;
            int rabbitPointsOffset = signpostIdsOffset - RabbitPointDeltaFromSlots;
            if (attackPointsOffset < 0x10000 || rabbitPointsOffset < 0x10000)
            {
                targetingUnavailableReason = "derived scenario-point offsets are outside the guarded manager range.";
                LogError($"Native event targeting disabled: {targetingUnavailableReason}");
                return;
            }

            IntPtr attackCandidate = new IntPtr(checked((long)playerManager + attackPointsOffset));
            if (!AreScenarioPointsPlausible(attackCandidate))
            {
                targetingUnavailableReason = "derived attack scenario-point array failed coordinate validation.";
                LogError($"Native event targeting disabled: {targetingUnavailableReason}");
                return;
            }

            attackPointsAddress = attackCandidate;
            targetingUnavailableReason = string.Empty;
            LogInfo(
                $"Native event signpost targeting ready: strategy=validated-relative-manager-layout, " +
                $"attackPointsOffset=0x{attackPointsOffset:X}, referenceRabbitPointsOffset=0x{rabbitPointsOffset:X}. " +
                "Reference attack-source RVA 0x11A420; rabbit fields require their separate semantic validation.");
        }

        private void InitializeRabbitCompatibility(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            rabbitPointsAddress = IntPtr.Zero;
            rabbitGlobalGateAddress = IntPtr.Zero;
            rabbitCountAddress = IntPtr.Zero;

            try
            {
                int handlerRva = FindUniquePattern(memory, RabbitHandlerPattern);
                int predicateRva = FindUniquePattern(memory, RabbitPredicatePattern);
                int spawnerRva = FindUniquePattern(memory, RabbitSpawnerPattern);
                int tileMaskRva = FindUniquePattern(memory, RabbitTileMaskPattern);

                if (referenceHashMatches &&
                    (handlerRva != ExpectedRabbitHandlerRva ||
                     predicateRva != ExpectedRabbitPredicateRva ||
                     spawnerRva != ExpectedRabbitSpawnerRva))
                {
                    throw new InvalidOperationException(
                        $"reference DLL rabbit RVAs diverged: handler=0x{handlerRva:X}, " +
                        $"predicate=0x{predicateRva:X}, spawner=0x{spawnerRva:X}.");
                }

                int predicateCallTarget = ResolveRelativeCallTarget(memory, handlerRva + 10);
                if (predicateCallTarget != predicateRva ||
                    !ContainsRelativeCallTo(memory, handlerRva, handlerRva + 0x80, spawnerRva))
                {
                    throw new InvalidOperationException(
                        "rabbit handler does not call the validated predicate and spawner.");
                }
                if (tileMaskRva < spawnerRva || tileMaskRva > spawnerRva + 0x180)
                    throw new InvalidOperationException("rabbit tile-mask validation is outside the resolved spawner.");

                rabbitGlobalGateAddress = ResolveRipRelativeAddress(
                    libraryHandle,
                    memory,
                    predicateRva,
                    displacementOffset: 2,
                    instructionLength: 7);
                rabbitCountAddress = ResolveRipRelativeAddress(
                    libraryHandle,
                    memory,
                    predicateRva + 9,
                    displacementOffset: 2,
                    instructionLength: 10);

                int rabbitPointsRva = ReadInt32(memory, predicateRva + 47);
                if (rabbitPointsRva < 0x10000 || rabbitPointsRva > 0x10000000)
                    throw new InvalidOperationException($"rabbit source RVA 0x{rabbitPointsRva:X} is implausible.");
                rabbitPointsAddress = IntPtr.Add(libraryHandle, rabbitPointsRva);
                if (!AreScenarioPointsPlausible(rabbitPointsAddress))
                    throw new InvalidOperationException("resolved rabbit source array failed coordinate validation.");

                targetingUnavailableReason = string.Empty;
                LogInfo(
                    $"Native rabbit compatibility ready: strategy=semantic-aob, handlerRva=0x{handlerRva:X}, " +
                    $"predicateRva=0x{predicateRva:X}, spawnerRva=0x{spawnerRva:X}, " +
                    $"rabbitPointsRva=0x{rabbitPointsRva:X}, rejectedTileMask=0x{RabbitRejectedTileMask:X8}, " +
                    $"globalGate=0x{rabbitGlobalGateAddress.ToInt64():X16}, rabbitCount=0x{rabbitCountAddress.ToInt64():X16}.");
            }
            catch (Exception ex)
            {
                rabbitPointsAddress = IntPtr.Zero;
                rabbitGlobalGateAddress = IntPtr.Zero;
                rabbitCountAddress = IntPtr.Zero;
                targetingUnavailableReason = $"native rabbit compatibility validation failed: {ex.Message}";
                LogError(
                    $"Rabbit events are disabled while other signpost events remain active: {targetingUnavailableReason}");
            }
        }

        private static bool TryGetUsableSignpost(int buildingId, out GameBuilding* building)
        {
            building = null;
            return buildingId > 0 &&
                GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out building) &&
                building->r_BuildingType == eStructs.STRUCT_SIGNPOST &&
                (building->r_AliveState == AliveState.NeedsInit || building->r_AliveState == AliveState.IsAlive);
        }

        private static bool AreScenarioPointsPlausible(IntPtr address)
        {
            for (int index = 0; index < ScenarioPointCount * 2; index++)
            {
                short value = Marshal.ReadInt16(address, index * sizeof(short));
                // -1 is Vanilla's unused-coordinate sentinel; non-negative values are map tiles.
                if (value < -1 || value >= 800)
                    return false;
            }
            return true;
        }

        private void RestoreTargetingFields(
            int[] originalSlots,
            short[] originalAttackPoints,
            short[] originalRabbitPoints)
        {
            for (int slot = 0; slot < SlotCount; slot++)
                Marshal.WriteInt32(slotsAddress, slot * sizeof(int), originalSlots[slot]);
            WriteScenarioPoints(attackPointsAddress, originalAttackPoints);
            if (rabbitPointsAddress != IntPtr.Zero && originalRabbitPoints != null)
                WriteScenarioPoints(rabbitPointsAddress, originalRabbitPoints);
        }

        private static short[] ReadScenarioPoints(IntPtr address)
        {
            short[] result = new short[ScenarioPointCount * 2];
            for (int index = 0; index < result.Length; index++)
                result[index] = Marshal.ReadInt16(address, index * sizeof(short));
            return result;
        }

        private static short[] CreateDisabledScenarioPoints()
        {
            short[] result = new short[ScenarioPointCount * 2];
            for (int index = 0; index < result.Length; index++)
                result[index] = -1;
            return result;
        }

        private bool TryCreateRabbitScenarioPoints(
            SignpostDistance signpost,
            out short[] result,
            out string failure)
        {
            result = null;
            failure = string.Empty;
            List<RabbitSourceCandidate> candidates = new List<RabbitSourceCandidate>();
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            for (int y = signpost.TileY - RabbitSourceSearchRadius;
                 y <= signpost.TileY + RabbitSourceSearchRadius;
                 y++)
            {
                for (int x = signpost.TileX - RabbitSourceSearchRadius;
                     x <= signpost.TileX + RabbitSourceSearchRadius;
                     x++)
                {
                    int deltaX = x - signpost.TileX;
                    int deltaY = y - signpost.TileY;
                    int distanceSquared = deltaX * deltaX + deltaY * deltaY;
                    if (distanceSquared < 4 ||
                        distanceSquared > RabbitSourceSearchRadius * RabbitSourceSearchRadius ||
                        !tiles.IsTileInsideMapBounds(x, y))
                    {
                        continue;
                    }

                    int tileId = tiles.GetTileId(x, y);
                    if (!tiles.IsTileWalkableAndUnoccupied(tileId))
                        continue;

                    uint flags = (uint)tiles.GetTilePropertyFlag(tileId);
                    if ((flags & RabbitRejectedTileMask) != 0)
                        continue;

                    candidates.Add(new RabbitSourceCandidate(x, y, distanceSquared, flags));
                }
            }

            candidates.Sort((left, right) =>
            {
                int comparison = left.DistanceSquared.CompareTo(right.DistanceSquared);
                if (comparison != 0) return comparison;
                comparison = left.Y.CompareTo(right.Y);
                return comparison != 0 ? comparison : left.X.CompareTo(right.X);
            });
            if (candidates.Count == 0)
            {
                failure =
                    $"no Vanilla-compatible tile was found within radius {RabbitSourceSearchRadius} " +
                    $"of signpost {signpost.BuildingId}; requiredMaskResult=0.";
                return false;
            }

            result = new short[ScenarioPointCount * 2];
            for (int index = 0; index < ScenarioPointCount; index++)
            {
                RabbitSourceCandidate candidate = candidates[index % candidates.Count];
                result[index * 2] = (short)candidate.X;
                result[index * 2 + 1] = (short)candidate.Y;
                LogInfo(
                    $"Rabbit source accepted: slot={index}, tile=({candidate.X},{candidate.Y}), " +
                    $"distanceToSignpost={Math.Sqrt(candidate.DistanceSquared):0.00}, " +
                    $"tileFlags=0x{candidate.Flags:X8}, rejectedMaskResult=0x{candidate.Flags & RabbitRejectedTileMask:X8}.");
            }
            return true;
        }

        private static IntPtr ResolveRipRelativeAddress(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            int instructionRva,
            int displacementOffset,
            int instructionLength)
        {
            int displacement = ReadInt32(memory, instructionRva + displacementOffset);
            long address = checked(
                libraryHandle.ToInt64() + instructionRva + instructionLength + displacement);
            return new IntPtr(address);
        }

        private static int ResolveRelativeCallTarget(ReadOnlySpan<byte> memory, int callRva)
        {
            if (callRva < 0 || callRva > memory.Length - 5 || memory[callRva] != 0xE8)
                return -1;
            return checked(callRva + 5 + ReadInt32(memory, callRva + 1));
        }

        private static bool ContainsRelativeCallTo(
            ReadOnlySpan<byte> memory,
            int start,
            int end,
            int targetRva)
        {
            int guardedEnd = Math.Min(end, memory.Length - 5);
            for (int offset = Math.Max(0, start); offset <= guardedEnd; offset++)
            {
                if (memory[offset] == 0xE8 && ResolveRelativeCallTarget(memory, offset) == targetRva)
                    return true;
            }
            return false;
        }

        private static void WriteScenarioPoints(IntPtr address, short[] values)
        {
            for (int index = 0; index < ScenarioPointCount * 2; index++)
                Marshal.WriteInt16(address, index * sizeof(short), values[index]);
        }

        private static NativeLookupResolution ResolveLookup(ReadOnlySpan<byte> memory, bool referenceHashMatches)
        {
            List<string> failures = new List<string>();

            try
            {
                int match = FindUniquePattern(memory, LookupPattern);
                if (TryValidateLookupCandidate(memory, match, out int slotOffset, out string validationFailure))
                    return new NativeLookupResolution(match, slotOffset, "semantic-aob");
                failures.Add($"semantic AOB candidate 0x{match:X} failed validation: {validationFailure}");
            }
            catch (Exception ex)
            {
                failures.Add($"semantic AOB: {ex.Message}");
            }

            // The RVA is only a fallback for the exact reference build and is never trusted without validation.
            if (referenceHashMatches)
            {
                if (TryValidateLookupCandidate(memory, ExpectedLookupFunctionRva, out int slotOffset, out string validationFailure))
                    return new NativeLookupResolution(ExpectedLookupFunctionRva, slotOffset, "validated-reference-rva");
                failures.Add($"reference RVA 0x{ExpectedLookupFunctionRva:X}: {validationFailure}");
            }

            if (TryFindUniqueStructuralCandidate(memory, out int structuralRva, out int structuralSlotOffset, out int candidateCount))
                return new NativeLookupResolution(structuralRva, structuralSlotOffset, "structural-module-scan");
            failures.Add($"structural module scan found {candidateCount} validated candidates instead of one");

            throw new InvalidOperationException(string.Join(" | ", failures));
        }

        private static bool TryFindUniqueStructuralCandidate(
            ReadOnlySpan<byte> memory,
            out int functionRva,
            out int signpostIdsOffset,
            out int candidateCount)
        {
            functionRva = -1;
            signpostIdsOffset = -1;
            candidateCount = 0;

            for (int offset = 0; offset <= memory.Length - 7; offset++)
            {
                if (memory[offset] != 0x48 || memory[offset + 1] != 0x63 || memory[offset + 2] != 0x81)
                    continue;
                if (!TryValidateLookupCandidate(memory, offset, out int slotOffset, out _))
                    continue;

                functionRva = offset;
                signpostIdsOffset = slotOffset;
                candidateCount++;
                if (candidateCount > 1)
                    return false;
            }

            return candidateCount == 1;
        }

        private static bool TryValidateLookupCandidate(
            ReadOnlySpan<byte> memory,
            int functionRva,
            out int signpostIdsOffset,
            out string failure)
        {
            signpostIdsOffset = -1;
            failure = string.Empty;
            if (functionRva < 0 || functionRva > memory.Length - 7 ||
                memory[functionRva] != 0x48 || memory[functionRva + 1] != 0x63 || memory[functionRva + 2] != 0x81)
            {
                failure = "candidate does not begin with the expected gPlayerManager field load.";
                return false;
            }

            int baseOffset = ReadInt32(memory, functionRva + 3);
            if (baseOffset < 0x10000 || baseOffset > 0x400000)
            {
                failure = $"derived slot offset 0x{baseOffset:X} is outside the guarded manager range.";
                return false;
            }

            int semanticEnd = Math.Min(memory.Length, functionRva + 0x60);
            if (!ContainsBytes(memory, functionRva, semanticEnd, new byte[] { 0x2C, 0x03, 0x00, 0x00 }) ||
                !ContainsBytes(memory, functionRva, semanticEnd, new byte[] { 0x2E, 0x01, 0x00, 0x00, 0x34 }))
            {
                failure = "building stride/type semantics do not match STRUCT_SIGNPOST lookup behavior.";
                return false;
            }

            // Vanilla emits eight unrolled manager-field loads; deriving their base avoids a hard-coded layout offset.
            int expectedOffset = baseOffset;
            int validatedSlots = 0;
            int validationEnd = Math.Min(memory.Length - 7, functionRva + CandidateValidationWindow);
            for (int offset = functionRva; offset <= validationEnd && validatedSlots < SlotCount; offset++)
            {
                byte rex = memory[offset];
                byte modRm = memory[offset + 2];
                if ((rex != 0x48 && rex != 0x49) || memory[offset + 1] != 0x63 ||
                    (modRm != 0x81 && modRm != 0x89))
                {
                    continue;
                }

                int displacement = ReadInt32(memory, offset + 3);
                if (displacement != expectedOffset)
                    continue;
                validatedSlots++;
                expectedOffset += sizeof(int);
            }

            if (validatedSlots != SlotCount)
            {
                failure = $"only {validatedSlots} of {SlotCount} consecutive signpost-slot loads were validated.";
                return false;
            }

            signpostIdsOffset = baseOffset;
            return true;
        }

        private static int ReadInt32(ReadOnlySpan<byte> memory, int offset)
        {
            return memory[offset] |
                memory[offset + 1] << 8 |
                memory[offset + 2] << 16 |
                memory[offset + 3] << 24;
        }

        private static bool ContainsBytes(ReadOnlySpan<byte> memory, int start, int end, byte[] needle)
        {
            for (int offset = start; offset <= end - needle.Length; offset++)
            {
                int index = 0;
                while (index < needle.Length && memory[offset + index] == needle[index])
                    index++;
                if (index == needle.Length)
                    return true;
            }
            return false;
        }

        private static int FindUniquePattern(ReadOnlySpan<byte> memory, string pattern)
        {
            PatternByte[] bytes = ParsePattern(pattern);
            int found = -1;
            int count = 0;
            for (int offset = 0; offset <= memory.Length - bytes.Length; offset++)
            {
                bool matches = true;
                for (int index = 0; index < bytes.Length; index++)
                {
                    if (!bytes[index].Wildcard && memory[offset + index] != bytes[index].Value)
                    {
                        matches = false;
                        break;
                    }
                }
                if (!matches) continue;
                found = offset;
                if (++count > 1) break;
            }
            if (count != 1)
                throw new InvalidOperationException($"lookup AOB expected one match but found {count}.");
            return found;
        }

        private static PatternByte[] ParsePattern(string pattern)
        {
            string[] tokens = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            PatternByte[] result = new PatternByte[tokens.Length];
            for (int index = 0; index < tokens.Length; index++)
            {
                bool wildcard = tokens[index] == "?" || tokens[index] == "??";
                result[index] = new PatternByte(
                    wildcard ? (byte)0 : byte.Parse(tokens[index], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    wildcard);
            }
            return result;
        }

        private void LogInfo(string message) => Shared.DebugLogHelper.LogInfo(log, message);
        private void LogWarning(string message) => Shared.DebugLogHelper.LogWarning(log, message);
        private void LogError(string message) => Shared.DebugLogHelper.LogError(log, message);

        private readonly struct PatternByte
        {
            public PatternByte(byte value, bool wildcard) { Value = value; Wildcard = wildcard; }
            public byte Value { get; }
            public bool Wildcard { get; }
        }

        private readonly struct NativeLookupResolution
        {
            public NativeLookupResolution(int functionRva, int signpostIdsOffset, string strategy)
            {
                FunctionRva = functionRva;
                SignpostIdsOffset = signpostIdsOffset;
                Strategy = strategy;
            }

            public int FunctionRva { get; }
            public int SignpostIdsOffset { get; }
            public string Strategy { get; }
        }

        private readonly struct SignpostDistance
        {
            public SignpostDistance(int buildingId, double distance, short tileX, short tileY)
            {
                BuildingId = buildingId;
                Distance = distance;
                TileX = tileX;
                TileY = tileY;
            }

            public int BuildingId { get; }
            public double Distance { get; }
            public short TileX { get; }
            public short TileY { get; }
        }

        private readonly struct RabbitSourceCandidate
        {
            public RabbitSourceCandidate(int x, int y, int distanceSquared, uint flags)
            {
                X = x;
                Y = y;
                DistanceSquared = distanceSquared;
                Flags = flags;
            }

            public int X { get; }
            public int Y { get; }
            public int DistanceSquared { get; }
            public uint Flags { get; }
        }

        private sealed class TargetingScope : IDisposable
        {
            private readonly IntPtr slots;
            private readonly IntPtr attackPoints;
            private readonly IntPtr rabbitPoints;
            private readonly int[] originalSlots;
            private readonly short[] originalAttackPoints;
            private readonly short[] originalRabbitPoints;
            private bool disposed;

            public TargetingScope(
                IntPtr slots,
                IntPtr attackPoints,
                IntPtr rabbitPoints,
                int[] originalSlots,
                short[] originalAttackPoints,
                short[] originalRabbitPoints)
            {
                this.slots = slots;
                this.attackPoints = attackPoints;
                this.rabbitPoints = rabbitPoints;
                this.originalSlots = originalSlots;
                this.originalAttackPoints = originalAttackPoints;
                this.originalRabbitPoints = originalRabbitPoints;
            }

            public void Dispose()
            {
                if (disposed)
                    return;
                for (int slot = 0; slot < SlotCount; slot++)
                    Marshal.WriteInt32(slots, slot * sizeof(int), originalSlots[slot]);
                WriteScenarioPoints(attackPoints, originalAttackPoints);
                if (rabbitPoints != IntPtr.Zero && originalRabbitPoints != null)
                    WriteScenarioPoints(rabbitPoints, originalRabbitPoints);
                disposed = true;
            }
        }
    }
}
