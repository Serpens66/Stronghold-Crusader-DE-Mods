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
        private const int SlotCount = ArcherSourceTargetingScope.SlotCount;
        private const int ReferenceSignpostIdsOffset = 0x18388C;
        private const int ExpectedLookupFunctionRva = 0xCB800;
        private const int CandidateValidationWindow = 0x240;
        private const int ExpectedArcherSourceLoadRva = 0x104E13;
        private const int ArcherSourceValidationWindow = 0x80;

        // CrusaderDE.dll SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
        // RVA 0xCB800 reads eight building IDs at gPlayerManager+0x18388C and accepts STRUCT_SIGNPOST (52).
        // Mutable displacements and branch lengths are wildcarded so compatible game updates can still resolve it.
        private const string LookupPattern =
            "48 63 81 ?? ?? ?? ?? 4C 8D 1D ?? ?? ?? ?? 45 33 D2 4C 8B C9 45 8B C2 85 C0 7E ?? " +
            "48 69 D0 2C 03 00 00 B8 01 00 00 00 66 42 83 BC 1A 2E 01 00 00 34";
        private const string ArcherSourcePattern =
            "48 63 C8 48 8D 1D ?? ?? ?? ?? 8B 05 ?? ?? ?? ?? 48 03 C9 48 89 44 24 30 BA 03 00 00 00 " +
            "8B 05 ?? ?? ?? ?? C7 44 24 28 16 00 00 00 49 8D 34 CE 44 89 44 24 20 " +
            "44 8B 8E ?? ?? ?? ?? 49 8D 3C CE 44 8B 87 ?? ?? ?? ??";
        private readonly ManualLogSource log;
        private IntPtr slotsAddress;
        private IntPtr archerSourceCoordinatesAddress;
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
            archerSourceCoordinatesAddress = IntPtr.Zero;
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
                InitializeArcherTargeting(memory, referenceHashMatches, resolution.SignpostIdsOffset, playerManager);
            }
            catch (Exception ex)
            {
                slotsAddress = IntPtr.Zero;
                archerSourceCoordinatesAddress = IntPtr.Zero;
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
            out SignpostTarget target,
            out string failure)
        {
            scope = null;
            target = default;
            failure = string.Empty;

            if (!IsAvailable || archerSourceCoordinatesAddress == IntPtr.Zero)
            {
                failure = string.IsNullOrEmpty(targetingUnavailableReason) ? unavailableReason : targetingUnavailableReason;
                return false;
            }

            if (!TryGetClosestSignpostToPlayer(targetPlayerId, out target, out failure))
            {
                return false;
            }
            if (!ArcherSourceTargetingScope.TryBegin(
                    slotsAddress,
                    archerSourceCoordinatesAddress,
                    target,
                    out scope,
                    out int originalSourceX,
                    out int originalSourceY,
                    out failure))
            {
                LogError($"Native event targeting failed safely: {failure}");
                return false;
            }

            LogDebug(
                $"Native event source isolated: targetPlayerId={targetPlayerId}, " +
                $"signpostBuildingId={target.BuildingId}, tile=({target.TileX},{target.TileY}), " +
                $"distanceReference={target.DistanceReference}, signpostDistance={target.Distance:0.00}, exposedSources=1, " +
                $"originalArcherSource=({originalSourceX},{originalSourceY}), " +
                $"injectedArcherSource=({target.TileX},{target.TileY}).");
            return true;
        }

        public bool TryGetClosestSignpostToPlayer(
            int targetPlayerId,
            out SignpostTarget target,
            out string failure)
        {
            target = default;
            failure = string.Empty;

            if (!IsAvailable)
            {
                failure = unavailableReason;
                return false;
            }

            if (!TryGetPlayerAnchor(targetPlayerId, out double anchorX, out double anchorY, out string distanceReference))
            {
                failure = $"target player {targetPlayerId} has neither a usable keep nor a living Lord anchor.";
                return false;
            }

            List<SignpostTarget> candidates = new List<SignpostTarget>();
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
                candidates.Add(new SignpostTarget(
                    buildingId,
                    (int)Math.Round(x),
                    (int)Math.Round(y),
                    Math.Sqrt(deltaX * deltaX + deltaY * deltaY),
                    distanceReference));
            }

            if (!SignpostTargetSelection.TrySelectClosest(candidates, out target))
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

        private void InitializeArcherTargeting(
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches,
            int signpostIdsOffset,
            ulong playerManager)
        {
            try
            {
                ArcherSourceResolution resolution = ResolveArcherSource(memory, referenceHashMatches, signpostIdsOffset);
                archerSourceCoordinatesAddress = new IntPtr(
                    checked((long)playerManager + resolution.SourceXOffset));
                targetingUnavailableReason = string.Empty;
            }
            catch (Exception ex)
            {
                archerSourceCoordinatesAddress = IntPtr.Zero;
                targetingUnavailableReason = ex.Message;
                LogError(
                    "Native archer targeting disabled while signpost selection for bandits and lions remains active: " +
                    targetingUnavailableReason);
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

        private ArcherSourceResolution ResolveArcherSource(
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches,
            int signpostIdsOffset)
        {
            if (referenceHashMatches)
            {
                if (!TryValidateArcherSourceCandidate(
                        memory,
                        ExpectedArcherSourceLoadRva,
                        signpostIdsOffset,
                        out int sourceXOffset,
                        out string validationFailure))
                {
                    throw new InvalidOperationException(
                        $"reference archer source RVA 0x{ExpectedArcherSourceLoadRva:X} failed local semantic validation: " +
                        validationFailure);
                }
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Native address resolved: name=archer signpost source, method=reference-rva, " +
                    $"rva=0x{ExpectedArcherSourceLoadRva:X}, sourceXOffset=0x{sourceXOffset:X}, " +
                    $"sourceYOffset=0x{sourceXOffset + sizeof(int):X}, slotStride=0x10.");
                return new ArcherSourceResolution(sourceXOffset);
            }

            int match = NativePatternResolver.FindUniquePattern(
                memory,
                ArcherSourcePattern,
                "archer signpost source");
            if (!TryValidateArcherSourceCandidate(
                    memory,
                    match,
                    signpostIdsOffset,
                    out int fallbackSourceXOffset,
                    out string fallbackFailure))
            {
                throw new InvalidOperationException(
                    $"archer source signature candidate 0x{match:X} failed validation: {fallbackFailure}");
            }
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Native address resolved: name=archer signpost source, method=signature-fallback, " +
                $"rva=0x{match:X}, sourceXOffset=0x{fallbackSourceXOffset:X}, " +
                $"sourceYOffset=0x{fallbackSourceXOffset + sizeof(int):X}, slotStride=0x10.");
            return new ArcherSourceResolution(fallbackSourceXOffset);
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

        private static bool TryValidateArcherSourceCandidate(
            ReadOnlySpan<byte> memory,
            int candidateRva,
            int signpostIdsOffset,
            out int sourceXOffset,
            out string failure)
        {
            sourceXOffset = -1;
            failure = string.Empty;
            if (candidateRva < 0 || candidateRva > memory.Length - 3 ||
                memory[candidateRva] != 0x48 || memory[candidateRva + 1] != 0x63 || memory[candidateRva + 2] != 0xC8)
            {
                failure = "candidate does not begin with the selected-slot sign extension.";
                return false;
            }

            int end = Math.Min(memory.Length - 7, candidateRva + ArcherSourceValidationWindow);
            List<int> xOffsets = new List<int>();
            List<int> yOffsets = new List<int>();
            for (int offset = candidateRva; offset <= end; offset++)
            {
                if (memory[offset] != 0x44 || memory[offset + 1] != 0x8B)
                    continue;
                if (memory[offset + 2] == 0x87)
                    xOffsets.Add(NativePatternResolver.ReadInt32(memory, offset + 3));
                else if (memory[offset + 2] == 0x8E)
                    yOffsets.Add(NativePatternResolver.ReadInt32(memory, offset + 3));
            }

            if (xOffsets.Count != 2 || yOffsets.Count != 2 ||
                xOffsets[0] != xOffsets[1] || yOffsets[0] != yOffsets[1])
            {
                failure = $"expected two matching X/Y loads but found X=[{string.Join(",", xOffsets)}], " +
                    $"Y=[{string.Join(",", yOffsets)}].";
                return false;
            }
            if (yOffsets[0] != xOffsets[0] + sizeof(int))
            {
                failure = $"source Y offset 0x{yOffsets[0]:X} does not immediately follow X offset 0x{xOffsets[0]:X}.";
                return false;
            }
            if (xOffsets[0] != signpostIdsOffset + 0x40)
            {
                failure = $"source X offset 0x{xOffsets[0]:X} is not signpost slots 0x{signpostIdsOffset:X} + 0x40.";
                return false;
            }
            if (!ContainsBytes(memory, candidateRva, end, new byte[] { 0x48, 0x03, 0xC9 }) ||
                !ContainsBytes(memory, candidateRva, end, new byte[] { 0x49, 0x8D, 0x34, 0xCE }) ||
                !ContainsBytes(memory, candidateRva, end, new byte[] { 0x49, 0x8D, 0x3C, 0xCE }))
            {
                failure = "selected-slot scaling does not prove the expected 0x10-byte coordinate-record stride.";
                return false;
            }

            sourceXOffset = xOffsets[0];
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

        private void LogDebug(string message) => Shared.DebugLogHelper.LogDebug(log, message);
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

        private readonly struct ArcherSourceResolution
        {
            public ArcherSourceResolution(int sourceXOffset)
            {
                SourceXOffset = sourceXOffset;
            }

            public int SourceXOffset { get; }
        }
    }
}
