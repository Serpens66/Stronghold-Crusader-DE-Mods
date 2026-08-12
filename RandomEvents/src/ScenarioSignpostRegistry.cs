using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using Shared;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RandomEvents
{
    internal sealed unsafe class ScenarioSignpostRegistry
    {
        private const int SlotCount = 8;
        private const int ReferenceSignpostIdsOffset = 0x18388C;
        private const int ExpectedLookupFunctionRva = 0xCB800;
        private const int CandidateValidationWindow = 0x240;
        private const int AttackPointDeltaFromSlots = 0x1B2C;
        private const int ScenarioPointCount = 4;

        // CrusaderDE.dll SHA-256 33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469.
        // RVA 0xCB800 reads eight building IDs at gPlayerManager+0x18388C and accepts STRUCT_SIGNPOST (52).
        // Mutable displacements and branch lengths are wildcarded so compatible game updates can still resolve it.
        private const string LookupPattern =
            "48 63 81 ?? ?? ?? ?? 4C 8D 1D ?? ?? ?? ?? 45 33 D2 4C 8B C9 45 8B C2 85 C0 7E ?? " +
            "48 69 D0 2C 03 00 00 B8 01 00 00 00 66 42 83 BC 1A 2E 01 00 00 34";
        private readonly ManualLogSource log;
        private IntPtr slotsAddress;
        private IntPtr attackPointsAddress;
        private readonly HashSet<int> eligibleBuildingIds = new HashSet<int>();
        private bool eligibilityConfigured;
        private string unavailableReason = "native compatibility resolution has not run.";
        private string targetingUnavailableReason = "native event targeting compatibility resolution has not run.";

        public ScenarioSignpostRegistry(ManualLogSource log)
        {
            this.log = log;
        }

        public bool IsAvailable => slotsAddress != IntPtr.Zero;
        public string UnavailableReason => unavailableReason;

        public void ResetMapState()
        {
            eligibleBuildingIds.Clear();
            eligibilityConfigured = false;
        }

        public void SetEligibleSignposts(IEnumerable<int> buildingIds)
        {
            eligibleBuildingIds.Clear();
            eligibilityConfigured = true;
            if (buildingIds == null)
                return;
            foreach (int buildingId in buildingIds)
            {
                if (buildingId > 0)
                    eligibleBuildingIds.Add(buildingId);
            }
        }

        public void InitializeNative(IntPtr libraryHandle, ReadOnlySpan<byte> memory, bool referenceHashMatches)
        {
            slotsAddress = IntPtr.Zero;
            attackPointsAddress = IntPtr.Zero;
            unavailableReason = "native compatibility resolution failed.";
            targetingUnavailableReason = "native event targeting compatibility resolution failed.";

            try
            {
                NativeLookupResolution resolution = ResolveLookup(memory, referenceHashMatches);

                ulong playerManager = GameGlobalsManager.Instance.GamePlayerManagerVA;
                if (playerManager == 0)
                    throw new InvalidOperationException("gPlayerManager is unavailable.");

                slotsAddress = new IntPtr(checked((long)playerManager + resolution.SignpostIdsOffset));
                unavailableReason = string.Empty;
                InitializeTargetingFields(resolution.SignpostIdsOffset, playerManager);
            }
            catch (Exception ex)
            {
                slotsAddress = IntPtr.Zero;
                attackPointsAddress = IntPtr.Zero;
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
                if (IsEligible(buildingId) && TryGetUsableSignpost(buildingId, out _))
                    return true;
            }
            return false;
        }

        public bool TryBeginTargetedEvent(
            int targetPlayerId,
            out IDisposable scope,
            out int signpostBuildingId,
            out double signpostDistance,
            out string failure)
        {
            scope = null;
            signpostBuildingId = -1;
            signpostDistance = double.MaxValue;
            failure = string.Empty;

            if (!IsAvailable || attackPointsAddress == IntPtr.Zero)
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
                if (!IsEligible(buildingId) || !seen.Add(buildingId) ||
                    !TryGetUsableSignpost(buildingId, out GameBuilding* signpost))
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
            try
            {
                WriteScenarioPoints(attackPointsAddress, CreateDisabledScenarioPoints());
                for (int slot = 0; slot < SlotCount; slot++)
                {
                    int buildingId = slot < usable.Count ? usable[slot].BuildingId : 0;
                    Marshal.WriteInt32(slotsAddress, slot * sizeof(int), buildingId);
                }
            }
            catch (Exception ex)
            {
                // Never leave Vanilla's scenario sources partially reordered after a failed native write.
                RestoreTargetingFields(originalSlots, originalAttackPoints);
                failure = $"temporary native source prioritization failed: {ex.Message}";
                LogError($"Native event targeting failed safely: {failure}");
                return false;
            }

            signpostBuildingId = usable[0].BuildingId;
            signpostDistance = usable[0].Distance;
            scope = new TargetingScope(
                slotsAddress,
                attackPointsAddress,
                originalSlots,
                originalAttackPoints);
            return true;
        }

        public bool TryGetClosestSignpostToPlayer(
            int targetPlayerId,
            out int signpostBuildingId,
            out int tileX,
            out int tileY,
            out double distance,
            out string distanceReference,
            out string failure)
        {
            signpostBuildingId = -1;
            tileX = 0;
            tileY = 0;
            distance = double.MaxValue;
            distanceReference = string.Empty;
            failure = string.Empty;

            if (!IsAvailable)
            {
                failure = unavailableReason;
                return false;
            }

            if (!TryGetPlayerAnchor(targetPlayerId, out double anchorX, out double anchorY, out distanceReference))
            {
                failure = $"target player {targetPlayerId} has neither a usable keep nor a living Lord anchor.";
                return false;
            }

            HashSet<int> seen = new HashSet<int>();
            foreach (int buildingId in ReadRegisteredBuildingIds())
            {
                if (!IsEligible(buildingId) || !seen.Add(buildingId) ||
                    !TryGetUsableSignpost(buildingId, out GameBuilding* signpost))
                    continue;

                double x = (signpost->r_TilePositionXBegin + signpost->r_TilePositionXEnd) / 2.0;
                double y = (signpost->r_TilePositionYBegin + signpost->r_TilePositionYEnd) / 2.0;
                double deltaX = x - anchorX;
                double deltaY = y - anchorY;
                double candidateDistance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                if (candidateDistance >= distance)
                    continue;

                signpostBuildingId = buildingId;
                tileX = (int)Math.Round(x);
                tileY = (int)Math.Round(y);
                distance = candidateDistance;
            }

            if (signpostBuildingId <= 0)
            {
                failure = "no alive registered Vanilla signpost exists.";
                return false;
            }

            return true;
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
                    return true;
                }
            }

            LogWarning($"Native signpost registration no-op: buildingId={buildingId}, reason=all eight Vanilla slots occupied.");
            return false;
        }

        public bool TryUnregister(int buildingId)
        {
            if (!IsAvailable || buildingId <= 0)
                return false;

            bool removed = false;
            for (int slot = 0; slot < SlotCount; slot++)
            {
                IntPtr slotAddress = IntPtr.Add(slotsAddress, slot * sizeof(int));
                if (Marshal.ReadInt32(slotAddress) != buildingId)
                    continue;
                Marshal.WriteInt32(slotAddress, 0);
                removed |= Marshal.ReadInt32(slotAddress) == 0;
            }
            return removed;
        }

        private bool IsEligible(int buildingId) =>
            !eligibilityConfigured || eligibleBuildingIds.Contains(buildingId);

        private void InitializeTargetingFields(int signpostIdsOffset, ulong playerManager)
        {
            int attackPointsOffset = signpostIdsOffset - AttackPointDeltaFromSlots;
            if (attackPointsOffset < 0x10000)
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
        }

        private static bool TryGetUsableSignpost(int buildingId, out GameBuilding* building)
        {
            building = null;
            return buildingId > 0 &&
                GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out building) &&
                building->r_BuildingType == eStructs.STRUCT_SIGNPOST &&
                (building->r_AliveState == AliveState.NeedsInit || building->r_AliveState == AliveState.IsAlive);
        }

        private static bool TryGetPlayerAnchor(
            int playerId,
            out double tileX,
            out double tileY,
            out string reference)
        {
            tileX = 0;
            tileY = 0;
            reference = string.Empty;

            int keepId = GamePlayerManagerAPI.Instance.GetPlayerKeepId(playerId);
            if (keepId > 0 &&
                GameBuildingManagerAPI.Instance.TryGetBuildingById(keepId, out GameBuilding* keep) &&
                (keep->r_AliveState == AliveState.NeedsInit || keep->r_AliveState == AliveState.IsAlive))
            {
                tileX = (keep->r_TilePositionXBegin + keep->r_TilePositionXEnd) / 2.0;
                tileY = (keep->r_TilePositionYBegin + keep->r_TilePositionYEnd) / 2.0;
                reference = "keep";
                return true;
            }

            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources) ||
                resources == null || resources->r_LordUnitId == 0 || resources->r_LordUnitId > int.MaxValue ||
                !GameUnitManagerAPI.Instance.TryGetUnitById((int)resources->r_LordUnitId, out GameUnit* lord) ||
                lord == null || lord->r_AliveState != AliveState.IsAlive ||
                lord->r_UnitChimp != eChimps.CHIMP_TYPE_LORD || lord->r_ControllableForPlayerId != playerId)
            {
                return false;
            }

            tileX = lord->r_CurrentTilePositionX;
            tileY = lord->r_CurrentTilePositionY;
            reference = "living-lord";
            return true;
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
            short[] originalAttackPoints)
        {
            for (int slot = 0; slot < SlotCount; slot++)
                Marshal.WriteInt32(slotsAddress, slot * sizeof(int), originalSlots[slot]);
            WriteScenarioPoints(attackPointsAddress, originalAttackPoints);
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

        private static void WriteScenarioPoints(IntPtr address, short[] values)
        {
            for (int index = 0; index < ScenarioPointCount * 2; index++)
                Marshal.WriteInt16(address, index * sizeof(short), values[index]);
        }

        private NativeLookupResolution ResolveLookup(ReadOnlySpan<byte> memory, bool referenceHashMatches)
        {
            List<string> failures = new List<string>();

            // Exact builds take the validated RVA fast path; changed builds use executable-section scans.
            if (referenceHashMatches)
            {
                if (TryValidateLookupCandidate(memory, ExpectedLookupFunctionRva, out int slotOffset, out string validationFailure))
                {
                    if (slotOffset != ReferenceSignpostIdsOffset)
                    {
                        throw new InvalidOperationException(
                            $"reference signpost-slot offset 0x{slotOffset:X} does not match audited offset " +
                            $"0x{ReferenceSignpostIdsOffset:X}.");
                    }

                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Native address resolved: name=signpost lookup, method=reference-rva, rva=0x{ExpectedLookupFunctionRva:X}.");
                    return new NativeLookupResolution(slotOffset);
                }

                throw new InvalidOperationException(
                    $"reference RVA 0x{ExpectedLookupFunctionRva:X} failed local semantic validation: {validationFailure}");
            }

            try
            {
                int match = NativePatternResolver.FindUniquePattern(memory, LookupPattern, "signpost lookup");
                if (TryValidateLookupCandidate(memory, match, out int slotOffset, out string validationFailure))
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Native address resolved: name=signpost lookup, method=signature-fallback, rva=0x{match:X}.");
                    return new NativeLookupResolution(slotOffset);
                }
                failures.Add($"semantic AOB candidate 0x{match:X} failed validation: {validationFailure}");
            }
            catch (Exception ex)
            {
                failures.Add($"semantic AOB: {ex.Message}");
            }

            if (TryFindUniqueStructuralCandidate(memory, out int structuralRva, out int structuralSlotOffset, out int candidateCount))
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Native address resolved: name=signpost lookup, method=structural-fallback, rva=0x{structuralRva:X}.");
                return new NativeLookupResolution(structuralSlotOffset);
            }
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

            foreach (NativeCodeRange range in NativePatternResolver.GetExecutableCodeRanges(memory))
            {
                int end = range.Offset + range.Length - 7;
                for (int offset = range.Offset; offset <= end; offset++)
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

            int baseOffset = NativePatternResolver.ReadInt32(memory, functionRva + 3);
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

                int displacement = NativePatternResolver.ReadInt32(memory, offset + 3);
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

        private void LogWarning(string message) => Shared.DebugLogHelper.LogWarning(log, message);
        private void LogError(string message) => Shared.DebugLogHelper.LogError(log, message);

        private readonly struct NativeLookupResolution
        {
            public NativeLookupResolution(int signpostIdsOffset)
            {
                SignpostIdsOffset = signpostIdsOffset;
            }

            public int SignpostIdsOffset { get; }
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

        private sealed class TargetingScope : IDisposable
        {
            private readonly IntPtr slots;
            private readonly IntPtr attackPoints;
            private readonly int[] originalSlots;
            private readonly short[] originalAttackPoints;
            private bool disposed;

            public TargetingScope(
                IntPtr slots,
                IntPtr attackPoints,
                int[] originalSlots,
                short[] originalAttackPoints)
            {
                this.slots = slots;
                this.attackPoints = attackPoints;
                this.originalSlots = originalSlots;
                this.originalAttackPoints = originalAttackPoints;
            }

            public void Dispose()
            {
                if (disposed)
                    return;
                for (int slot = 0; slot < SlotCount; slot++)
                    Marshal.WriteInt32(slots, slot * sizeof(int), originalSlots[slot]);
                WriteScenarioPoints(attackPoints, originalAttackPoints);
                disposed = true;
            }
        }
    }
}
