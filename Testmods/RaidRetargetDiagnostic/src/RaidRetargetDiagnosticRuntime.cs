using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RaidRetargetDiagnostic
{
    internal sealed unsafe class RaidRetargetDiagnosticRuntime
    {
        private const int MaxPlayers = 8;
        private const int RaidGroupCount = 6;
        private const int RetargetCounterOffset = 0x2B78;
        private const int RemainingCounterOffset = 0x390C;
        private const int TribeStatusAOffset = 0x5EA;
        private const int TribeStatusBOffset = 0x5EC;
        private const int TribeStatusCOffset = 0x5F0;
        private const int TargetBuildingIdOffset = 0x622;
        private const int TargetGlobalIdOffset = 0x626;
        private const int AttackCandidateListOffset = 0x1B344;
        private const int AttackCandidateRecordSize = 12;
        private const int MaxAttackCandidateSnapshotEntries = 32;
        private const int NativeAttackCandidateCapacity = 500;
        private const int NativeUnreachableCandidateScore = 10000000;
        private const int NativeRaidCandidateCapacity = 100;
        private const int RaidCandidateListOffset = 0x3560;
        private const int RaidCandidateCountOffset = 0x3628;
        private const int NativePrioritySelectorRva = 0x856A6D2;
        private const int NativePriorityTable0Rva = 0x2C7F80;
        private const int NativePriorityTable1Rva = 0x2C7EC0;
        private const int NativePriorityTable2Rva = 0x2C7E00;
        private const int NativePriorityCount = 47;
        private const int MaximumRetryCommandsPerTick = 4;
        private const int RejectedTargetDurationTicks = 300;
        private const int StationarySummaryInterval = 50;

        private readonly ManualLogSource log;
        private readonly bool fixesLoaded;
        private readonly string[,] previousGroups = new string[MaxPlayers + 1, RaidGroupCount];
        private readonly string[] previousCounters = new string[MaxPlayers + 1];
        private readonly string[,] previousTargetIdentities = new string[MaxPlayers + 1, RaidGroupCount];
        private readonly Dictionary<string, string> previousUnits = new Dictionary<string, string>();
        private readonly Dictionary<string, string> previousPositions = new Dictionary<string, string>();
        private readonly Dictionary<string, int> lastPositionChanges = new Dictionary<string, int>();
        private readonly Dictionary<int, Stack<DeleteCapture>> pendingDeletes =
            new Dictionary<int, Stack<DeleteCapture>>();
        private readonly Dictionary<int, Stack<AttackCandidateCapture>> pendingAttackCandidates =
            new Dictionary<int, Stack<AttackCandidateCapture>>();
        private readonly Dictionary<int, RaidRetry> pendingRaidRetries = new Dictionary<int, RaidRetry>();
        private readonly Dictionary<int, Dictionary<ulong, int>> rejectedRaidTargets =
            new Dictionary<int, Dictionary<ulong, int>>();
        private bool issuingFallback;
        private int fallbackTribeId;
        private int fallbackBuildingId;
        private uint fallbackBuildingGlobalId;
        private AttackResult fallbackResult;
        private bool active;
        private bool firstTickLogged;
        private int lastTick = -1;
        private long sessionId;

        internal RaidRetargetDiagnosticRuntime(ManualLogSource log, bool fixesLoaded)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.fixesLoaded = fixesLoaded;
            ValidateLayout();
        }

        internal void OnSessionStarted(Shared.GameplaySessionStartedContext session)
        {
            ClearObservations();
            sessionId = session.SessionId;
            active = true;
            firstTickLogged = false;
            lastTick = -1;
            Info($"RAID_DIAG_SESSION: session={sessionId}, kind={session.Kind}, loadedSave={session.IsLoadedSave}, " +
                $"save={session.SaveFileName}, editor={session.IsEditor}, replay={session.IsReplay}, fixesLoaded={fixesLoaded}.");
        }

        internal void OnSessionEnded()
        {
            if (active) Info($"RAID_DIAG_END: session={sessionId}, tick={lastTick}.");
            active = false;
            ClearObservations();
        }

        internal void OnTick(int tick)
        {
            if (!active) return;
            try
            {
                if (!firstTickLogged)
                {
                    firstTickLogged = true;
                    Info($"RAID_DIAG_POST_STARTUP_TICK: session={sessionId}, tick={tick}, publisher=GameTimeManagerAPI.OnTick.");
                }
                if (lastTick >= 0 && tick < lastTick)
                {
                    int resetFrom = lastTick;
                    ClearObservations();
                    lastTick = -1;
                    Warn($"RAID_DIAG_TICK_RESET: session={sessionId}, previous={resetFrom}, current={tick}.");
                }
                int previousPreTick = lastTick;
                lastTick = tick;
                var players = GamePlayerManagerAPI.Instance;
                var tribes = GameTribeManagerAPI.Instance;
                for (int playerId = 1; playerId <= MaxPlayers; playerId++)
                {
                    if (!players.IsAIPlayer(playerId) ||
                        !players.TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
                        continue;

                    string counters = $"retarget={resources->N00003F66},remaining100={resources->N00005341}";
                    if (!String.Equals(previousCounters[playerId], counters, StringComparison.Ordinal))
                    {
                        string changeWindow = previousCounters[playerId] == null || previousPreTick < 0 ? "initialSample" :
                            $"afterPreTick:{previousPreTick}/beforePreTick:{tick}";
                        Info($"RAID_DIAG_COUNTER: session={sessionId}, tick={tick}, player={playerId}, " +
                            $"previous={previousCounters[playerId] ?? "unseen"}, current={counters}, " +
                            $"changeWindow={changeWindow}.");
                        previousCounters[playerId] = counters;
                    }

                    for (int group = 0; group < RaidGroupCount; group++)
                    {
                        AITribeStorageRole16 role = (AITribeStorageRole16)((int)AITribeStorageRole16.HarassmentCombat0 + group);
                        bool hasRole = tribes.TryGetAITribeStorageRole(playerId, role,
                            out ushort tribeId, out uint tribeGlobalId);
                        GameTribe* tribe = null;
                        bool resolved = hasRole && tribeId != 0 && tribes.IsValidId(tribeId) &&
                            tribes.TryResolveAITribeStorageRole(playerId, role,
                                out tribe);
                        string state = FormatGroup(tribeId, tribeGlobalId, resolved ? tribe : null);
                        if (resolved && tribe != null && tribe->r_UnitsInGroup != 0)
                        {
                            byte* bytes = (byte*)tribe;
                            ushort targetId = *(ushort*)(bytes + TargetBuildingIdOffset);
                            uint targetGlobalId = *(uint*)(bytes + TargetGlobalIdOffset);
                            string targetIdentity = $"{tribeId}/{tribeGlobalId}:{targetId}/{targetGlobalId}";
                            if (!String.Equals(previousTargetIdentities[playerId, group], targetIdentity,
                                    StringComparison.Ordinal))
                            {
                                previousTargetIdentities[playerId, group] = targetIdentity;
                                if (targetId != 0)
                                    LogTargetAccess(playerId, group, tribeId, tribeGlobalId,
                                        targetId, targetGlobalId);
                            }
                            if (targetId != 0)
                                LogGroupUnits(playerId, group, tribeId, tribeGlobalId);
                        }
                        if (String.Equals(previousGroups[playerId, group], state, StringComparison.Ordinal))
                            continue;
                        Info($"RAID_DIAG_GROUP: session={sessionId}, tick={tick}, player={playerId}, group={group}, " +
                            $"role={(int)role}, previous={previousGroups[playerId, group] ?? "unseen"}, current={state}, {counters}.");
                        previousGroups[playerId, group] = state;
                    }
                }
                ProcessRaidRetries();
            }
            catch (Exception ex)
            {
                Warn($"RAID_DIAG_TICK_ERROR: session={sessionId}, tick={tick}, error={ex}.");
            }
        }

        internal void OnBuildingDamage(BuildingTileTakeDamageEventArgs args)
        {
            if (!active || args.Phase != EventHookPhase.Pre) return;
            try
            {
                var tiles = GameTileManagerAPI.Instance;
                if ((uint)args.TileId >= (uint)tiles.GetStructureLayer().Length) return;
                int buildingId = tiles.GetTileBuildingId(args.TileId);
                uint? buildingGlobalId = ReadBuildingGlobalId(buildingId);
                if (!buildingGlobalId.HasValue) return;
                string groups = MatchingTargetGroups(buildingId, buildingGlobalId.Value);
                if (groups == "none") return;
                Info($"RAID_DIAG_DAMAGE: session={sessionId}, tick=afterPreTick:{lastTick}, phase={args.Phase}, " +
                    $"buildingId={buildingId}, buildingGlobalId={buildingGlobalId.Value}, " +
                    $"building={FormatBuilding(buildingId, null)}, raidGroups={groups}, " +
                    $"tile={args.TileId}, xy={args.TileX}/{args.TileY}, damage={args.Damage}, sourcePlayer={args.PlayerIdSource}.");
            }
            catch (Exception ex)
            {
                Warn($"RAID_DIAG_DAMAGE_ERROR: session={sessionId}, tick={lastTick}, error={ex}.");
            }
        }

        internal void OnBuildingDelete(BuildingDeleteEventArgs args)
        {
            if (!active) return;
            try
            {
                if (args.Phase == EventHookPhase.Pre)
                {
                    uint? globalId = ReadBuildingGlobalId(args.BuildingId);
                    string groups = globalId.HasValue
                        ? MatchingTargetGroups(args.BuildingId, globalId.Value) : "none";
                    var capture = new DeleteCapture(globalId, FormatBuilding(args.BuildingId, null), groups);
                    if (!pendingDeletes.TryGetValue(args.BuildingId, out Stack<DeleteCapture> stack))
                    {
                        stack = new Stack<DeleteCapture>();
                        pendingDeletes.Add(args.BuildingId, stack);
                    }
                    stack.Push(capture);
                    // All building deletions are logged, regardless of building type or raid target.
                    Info($"RAID_DIAG_DELETE: session={sessionId}, tick=afterPreTick:{lastTick}, phase=Pre, " +
                        $"buildingId={args.BuildingId}, preGlobalId={globalId?.ToString() ?? "none"}, " +
                        $"preBuilding={capture.PreBuilding}, raidGroups={groups}.");
                }
                else if (args.Phase == EventHookPhase.Post)
                {
                    DeleteCapture capture = null;
                    if (pendingDeletes.TryGetValue(args.BuildingId, out Stack<DeleteCapture> stack) &&
                        stack.Count != 0)
                    {
                        capture = stack.Pop();
                        if (stack.Count == 0) pendingDeletes.Remove(args.BuildingId);
                    }
                    Info($"RAID_DIAG_DELETE: session={sessionId}, tick=afterPreTick:{lastTick}, phase=Post, " +
                        $"buildingId={args.BuildingId}, preGlobalId={capture?.GlobalId?.ToString() ?? "missing"}, " +
                        $"preBuilding={capture?.PreBuilding ?? "missing"}, " +
                        $"raidGroupsAtPre={capture?.RaidGroups ?? "missing"}, " +
                        $"postBuilding={FormatBuilding(args.BuildingId, null)}.");
                }
            }
            catch (Exception ex)
            {
                Warn($"RAID_DIAG_DELETE_ERROR: session={sessionId}, tick={lastTick}, error={ex}.");
            }
        }

        internal void OnTribeOrder(TribeIssueOrderWithTargetEventArgs args)
        {
            if (!active) return;
            try
            {
                if (args.AICommand == TribeAICommand.AttackBuilding ||
                    args.AICommand == TribeAICommand.ForceAttackBuilding)
                {
                    try { LogAttackCandidates(args); }
                    catch (Exception ex)
                    {
                        Warn($"RAID_DIAG_ATTACK_CANDIDATES_ERROR: session={sessionId}, " +
                            $"tick={lastTick}, tribe={args.TribeId}, error={ex}.");
                    }
                }
                if (!TryIdentifyRaidGroup(args.TribeId,
                        out int playerId, out int group, out uint tribeGlobalId)) return;
                string target = args.AICommand == TribeAICommand.AttackBuilding ||
                    args.AICommand == TribeAICommand.ForceAttackBuilding
                    ? FormatBuilding(args.TargetValue1, unchecked((uint)args.TargetValue2))
                    : "notBuildingCommand";
                Info($"RAID_DIAG_TRIBE_ORDER: session={sessionId}, tick=afterPreTick:{lastTick}, " +
                    $"phase={args.Phase}, player={playerId}, group={group}, " +
                    $"tribe={args.TribeId}/{tribeGlobalId}, command={args.AICommand}/{(int)args.AICommand}, " +
                    $"eventTarget1={args.TargetValue1}, eventTarget2={args.TargetValue2}, a6={args.a6}, " +
                    $"return={args.ReturnValue}, targetBuilding={target}.");
            }
            catch (Exception ex)
            {
                Warn($"RAID_DIAG_TRIBE_ORDER_ERROR: session={sessionId}, tick={lastTick}, error={ex}.");
            }
        }

        private void LogAttackCandidates(TribeIssueOrderWithTargetEventArgs args)
        {
            var tribes = GameTribeManagerAPI.Instance;
            if (args.TribeId <= 0 || !tribes.IsValidId(args.TribeId) ||
                !tribes.TryGetTribeById(args.TribeId, out GameTribe* tribe) || tribe == null ||
                tribe->r_AliveState != AliveState.IsAlive) return;

            uint tribeGlobalId = tribe->r_GlobalId;
            int owner = tribe->r_PlayerIdOwner;
            bool raid = TryIdentifyRaidGroup(args.TribeId,
                out int raidPlayer, out int raidGroup, out uint raidGlobalId) &&
                raidGlobalId == tribeGlobalId;
            uint targetGlobalId = unchecked((uint)args.TargetValue2);
            AttackCandidateSnapshot snapshot = ReadAttackCandidateSnapshot();
            AttackCandidateCapture pre = null;
            bool pairMatches = false;
            string freshness = "preCallUnproven";

            if (args.Phase == EventHookPhase.Pre)
            {
                if (!pendingAttackCandidates.TryGetValue(args.TribeId,
                        out Stack<AttackCandidateCapture> stack))
                {
                    stack = new Stack<AttackCandidateCapture>();
                    pendingAttackCandidates.Add(args.TribeId, stack);
                }
                if (stack.Count != 0 && stack.Peek().Tick != lastTick)
                {
                    Warn($"RAID_DIAG_ATTACK_UNPAIRED: session={sessionId}, tick={lastTick}, " +
                        $"tribe={args.TribeId}/{tribeGlobalId}, discardedPreCalls={stack.Count}.");
                    stack.Clear();
                }
                stack.Push(new AttackCandidateCapture(lastTick, tribeGlobalId,
                    args.AICommand, args.TargetValue1, targetGlobalId, snapshot));
            }
            else if (args.Phase == EventHookPhase.Post &&
                pendingAttackCandidates.TryGetValue(args.TribeId,
                    out Stack<AttackCandidateCapture> postStack) && postStack.Count != 0)
            {
                pre = postStack.Pop();
                if (postStack.Count == 0) pendingAttackCandidates.Remove(args.TribeId);
                pairMatches = pre.TribeGlobalId == tribeGlobalId &&
                    pre.Command == args.AICommand && pre.BuildingId == args.TargetValue1 &&
                    pre.BuildingGlobalId == targetGlobalId;
                freshness = !pairMatches ? "unmatchedPrePost" :
                    pre.Snapshot.SameRecords(snapshot) ? "unchangedPossiblyStale" : "changedFromPre";
            }
            else if (args.Phase == EventHookPhase.Post)
            {
                freshness = "missingPre";
            }

            string raidLabel = raid ? $"p{raidPlayer}g{raidGroup}" : "none";
            bool possiblyOld = args.Phase != EventHookPhase.Post ||
                freshness != "changedFromPre" || !snapshot.Available;
            AttackResult result = args.Phase == EventHookPhase.Post &&
                !possiblyOld && args.ReturnValue == 1
                ? ClassifyAttackResult(snapshot, args.TargetValue1, targetGlobalId)
                : AttackResult.Unknown;
            Info($"RAID_DIAG_ATTACK_CANDIDATES: session={sessionId}, tick=afterPreTick:{lastTick}, " +
                $"phase={args.Phase}, tribe={args.TribeId}/{tribeGlobalId}, owner={owner}, " +
                $"raid={raidLabel}, " +
                $"command={args.AICommand}/{(int)args.AICommand}, " +
                $"eventTarget={args.TargetValue1}/{targetGlobalId}, " +
                $"targetBuilding={FormatBuilding(args.TargetValue1, targetGlobalId)}, " +
                $"return={args.ReturnValue}, pairMatches={pairMatches}, freshness={freshness}, " +
                $"postScratchMayBeOld={possiblyOld}, meleeAttackResult={result}, " +
                $"{snapshot.Describe()}.");
            if (args.Phase != EventHookPhase.Post || args.AICommand != TribeAICommand.AttackBuilding ||
                !raid) return;
            if (issuingFallback)
            {
                if (args.TribeId == fallbackTribeId &&
                    args.TargetValue1 == fallbackBuildingId &&
                    targetGlobalId == fallbackBuildingGlobalId)
                    fallbackResult = result;
                return;
            }
            if (result == AttackResult.NoAttackPoint &&
                TryIsMeleeRaidGroup(args.TribeId, tribeGlobalId) &&
                GameBuildingManagerAPI.Instance.TryGetBuildingById(args.TargetValue1,
                    out GameBuilding* failedBuilding) && failedBuilding != null &&
                failedBuilding->r_GlobalId == targetGlobalId &&
                TryGetPriorityTable(out int priorityTableRva, out _))
            {
                RememberRejectedTarget(args.TribeId, args.TargetValue1, targetGlobalId);
                pendingRaidRetries[args.TribeId] = new RaidRetry(raidPlayer, raidGroup,
                    args.TribeId, tribeGlobalId, failedBuilding->r_PlayerIdOwner,
                    args.TargetValue1, targetGlobalId, priorityTableRva);
                Info($"RAID_FIX_RETRY_QUEUED: session={sessionId}, tick={lastTick}, " +
                    $"player={raidPlayer}, group={raidGroup}, tribe={args.TribeId}/{tribeGlobalId}, " +
                    $"rejected={args.TargetValue1}/{targetGlobalId}.");
            }
            else if (result == AttackResult.Unknown)
                Info($"RAID_FIX_UNCERTAIN: session={sessionId}, tick={lastTick}, " +
                    $"tribe={args.TribeId}/{tribeGlobalId}, target={args.TargetValue1}/{targetGlobalId}, " +
                    $"reason={freshness}; vanillaPreserved=true.");
            else if (result == AttackResult.NoAttackPoint)
                Warn($"RAID_FIX_UNCERTAIN: session={sessionId}, tick={lastTick}, " +
                    $"tribe={args.TribeId}/{tribeGlobalId}, target={args.TargetValue1}/{targetGlobalId}, " +
                    "reason=raidEligibilityOrPriorityUnavailable; vanillaPreserved=true.");
        }

        private static AttackCandidateSnapshot ReadAttackCandidateSnapshot()
        {
            IntPtr context = GamePathingManagerAPI.Instance.GetPathfindingContextView().Address;
            if (context == IntPtr.Zero)
                return new AttackCandidateSnapshot(false, -1, new CandidateRecord[0]);

            byte* firstRecord = (byte*)context.ToPointer() + AttackCandidateListOffset;
            var entries = new List<CandidateRecord>(NativeAttackCandidateCapacity);
            int terminatorAt = -1;
            for (int index = 0; index < NativeAttackCandidateCapacity; index++)
            {
                int* record = (int*)(firstRecord + index * AttackCandidateRecordSize);
                if (record[0] == 0 && record[1] == 0)
                {
                    terminatorAt = index;
                    break;
                }
                entries.Add(new CandidateRecord(record[0], record[1], record[2]));
            }
            return new AttackCandidateSnapshot(true, terminatorAt, entries.ToArray());
        }

        private static AttackResult ClassifyAttackResult(AttackCandidateSnapshot snapshot,
            int buildingId, uint buildingGlobalId)
        {
            if (!snapshot.Available || !snapshot.Complete ||
                ReadBuildingGlobalId(buildingId) != buildingGlobalId)
                return AttackResult.Unknown;
            if (snapshot.Records.Length == 0) return AttackResult.NoAttackPoint;
            CandidateRecord first = snapshot.Records[0];
            var tiles = GameTileManagerAPI.Instance;
            int capacity = tiles.GetStructureLayer().Length;
            if (first.ApproachTile <= 0 || first.ApproachTile >= capacity)
                return AttackResult.Unknown;
            if (first.BuildingTile == 0)
            {
                // Vanilla consumes only the paired prefix. A later approach-only entry
                // does not rescue a zero first building tile.
                for (int i = 1; i < snapshot.Records.Length; i++)
                    if (snapshot.Records[i].BuildingTile != 0)
                        return AttackResult.Unknown;
                return AttackResult.NoAttackPoint;
            }
            if (first.BuildingTile < 0 || first.BuildingTile >= capacity ||
                tiles.GetTileBuildingId(first.BuildingTile) != buildingId)
                return AttackResult.Unknown;
            if (first.Score >= NativeUnreachableCandidateScore)
                return AttackResult.Unknown;
            var stand = tiles.GetTileVectorFromId(first.ApproachTile);
            var target = tiles.GetTileVectorFromId(first.BuildingTile);
            if (Math.Abs((int)stand.X - target.X) +
                Math.Abs((int)stand.Y - target.Y) != 1 ||
                tiles.GetTileId(stand.X, stand.Y) != first.ApproachTile)
                return AttackResult.Unknown;
            return AttackResult.AttackPoint;
        }

        private static bool TryIsMeleeRaidGroup(int tribeId, uint tribeGlobalId)
        {
            var tribes = GameTribeManagerAPI.Instance;
            if (!tribes.TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null ||
                tribe->r_GlobalId != tribeGlobalId) return false;
            var unitIds = new List<int>();
            if (!tribes.GetUnits(tribeId, unitIds)) return false;
            int meleeCount = 0;
            var units = GameUnitManagerAPI.Instance;
            foreach (int unitId in unitIds)
            {
                if (!units.IsValidId(unitId) || !units.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_TribeId != tribeId) continue;
                switch (unit->r_UnitChimp)
                {
                    case eChimps.CHIMP_TYPE_SPEARMAN:
                    case eChimps.CHIMP_TYPE_PIKEMAN:
                    case eChimps.CHIMP_TYPE_MACEMAN:
                    case eChimps.CHIMP_TYPE_SWORDSMAN:
                    case eChimps.CHIMP_TYPE_KNIGHT:
                    case eChimps.CHIMP_TYPE_MONK:
                    case eChimps.CHIMP_TYPE_ARAB_SLAVE:
                    case eChimps.CHIMP_TYPE_ARAB_ASSASIN:
                    case eChimps.CHIMP_TYPE_ARAB_SWORDSMAN:
                    case eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER:
                    case eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH:
                    case eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL:
                        meleeCount++;
                        break;
                    default:
                        return false; // Mixed or unknown combat groups retain Vanilla.
                }
            }
            return meleeCount != 0;
        }

        private static ulong BuildingIdentity(int buildingId, uint globalId) =>
            ((ulong)globalId << 32) | (uint)buildingId;

        private void RememberRejectedTarget(int tribeId, int buildingId, uint globalId)
        {
            if (!rejectedRaidTargets.TryGetValue(tribeId, out Dictionary<ulong, int> rejected))
            {
                rejected = new Dictionary<ulong, int>();
                rejectedRaidTargets.Add(tribeId, rejected);
            }
            rejected[BuildingIdentity(buildingId, globalId)] =
                lastTick + RejectedTargetDurationTicks;
        }

        private bool IsRejectedTarget(int tribeId, int buildingId, uint globalId) =>
            rejectedRaidTargets.TryGetValue(tribeId, out Dictionary<ulong, int> rejected) &&
            rejected.TryGetValue(BuildingIdentity(buildingId, globalId), out int until) &&
            until > lastTick;

        private void ProcessRaidRetries()
        {
            if (pendingRaidRetries.Count == 0) return;
            var queued = new List<RaidRetry>(pendingRaidRetries.Values);
            pendingRaidRetries.Clear();
            foreach (RaidRetry retry in queued)
            {
                if (!TryIdentifyRaidGroup(retry.TribeId, out int player, out int group,
                        out uint currentGlobalId) || player != retry.PlayerId ||
                    group != retry.Group || currentGlobalId != retry.TribeGlobalId ||
                    !GameTribeManagerAPI.Instance.TryGetTribeById(retry.TribeId,
                        out GameTribe* tribe) || tribe == null ||
                    tribe->r_AliveState != AliveState.IsAlive ||
                    !TryIsMeleeRaidGroup(retry.TribeId, retry.TribeGlobalId)) continue;

                byte* tribeBytes = (byte*)tribe;
                int currentId = *(ushort*)(tribeBytes + TargetBuildingIdOffset);
                uint currentGlobal = *(uint*)(tribeBytes + TargetGlobalIdOffset);
                if (currentId != retry.LastFailedId || currentGlobal != retry.LastFailedGlobalId)
                {
                    Info($"RAID_FIX_RETRY_CANCELLED: session={sessionId}, tick={lastTick}, " +
                        $"tribe={retry.TribeId}/{retry.TribeGlobalId}, " +
                        $"expected={retry.LastFailedId}/{retry.LastFailedGlobalId}, " +
                        $"current={currentId}/{currentGlobal}.");
                    continue;
                }

                if (retry.Candidates == null && !TryReadOrderedRaidCandidates(retry,
                        out retry.Candidates, out string reason))
                {
                    Warn($"RAID_FIX_RETRY_UNCERTAIN: session={sessionId}, tick={lastTick}, " +
                        $"tribe={retry.TribeId}/{retry.TribeGlobalId}, reason={reason}; vanillaPreserved=true.");
                    continue;
                }

                int issued = 0;
                bool finished = false;
                while (retry.NextCandidate < retry.Candidates.Count &&
                    issued < MaximumRetryCommandsPerTick)
                {
                    BuildingTarget candidate = retry.Candidates[retry.NextCandidate++];
                    if (IsRejectedTarget(retry.TribeId, candidate.Id, candidate.GlobalId) ||
                        !IsLiveBuildingIdentity(candidate.Id, candidate.GlobalId,
                            retry.TargetPlayerId)) continue;
                    fallbackResult = AttackResult.Unknown;
                    issuingFallback = true;
                    fallbackTribeId = retry.TribeId;
                    fallbackBuildingId = candidate.Id;
                    fallbackBuildingGlobalId = candidate.GlobalId;
                    bool commandIssued;
                    try
                    {
                        commandIssued = GameTribeManagerAPI.Instance.AttackBuildingEx(
                            retry.TribeId, candidate.Id, unchecked((int)candidate.GlobalId));
                    }
                    finally
                    {
                        issuingFallback = false;
                        fallbackTribeId = 0;
                        fallbackBuildingId = 0;
                        fallbackBuildingGlobalId = 0;
                    }
                    bool storedCandidate = *(ushort*)(tribeBytes + TargetBuildingIdOffset) == candidate.Id &&
                        *(uint*)(tribeBytes + TargetGlobalIdOffset) == candidate.GlobalId;
                    if (!storedCandidate) fallbackResult = AttackResult.Unknown;
                    issued++;
                    Info($"RAID_FIX_RETRY_RESULT: session={sessionId}, tick={lastTick}, " +
                        $"player={player}, group={group}, tribe={retry.TribeId}/{retry.TribeGlobalId}, " +
                        $"candidate={candidate.Id}/{candidate.GlobalId}, issued={commandIssued}, " +
                        $"storedCandidate={storedCandidate}, meleeAttackResult={fallbackResult}, " +
                        $"attempt={retry.NextCandidate}/{retry.Candidates.Count}.");
                    if (!commandIssued || fallbackResult == AttackResult.Unknown)
                    {
                        finished = true; // Preserve the command outcome when the scratch result is uncertain.
                        break;
                    }
                    if (fallbackResult == AttackResult.AttackPoint)
                    {
                        finished = true;
                        break;
                    }
                    RememberRejectedTarget(retry.TribeId, candidate.Id, candidate.GlobalId);
                    retry.LastFailedId = candidate.Id;
                    retry.LastFailedGlobalId = candidate.GlobalId;
                }
                if (finished) continue;
                if (retry.NextCandidate < retry.Candidates.Count)
                {
                    pendingRaidRetries[retry.TribeId] = retry;
                    continue;
                }
                if (*(ushort*)(tribeBytes + TargetBuildingIdOffset) == retry.LastFailedId &&
                    *(uint*)(tribeBytes + TargetGlobalIdOffset) == retry.LastFailedGlobalId)
                {
                    *(ushort*)(tribeBytes + TargetBuildingIdOffset) = 0;
                    *(uint*)(tribeBytes + TargetGlobalIdOffset) = 0;
                    Info($"RAID_FIX_NO_TARGET: session={sessionId}, tick={lastTick}, " +
                        $"player={player}, group={group}, tribe={retry.TribeId}/{retry.TribeGlobalId}, " +
                        $"rejected={retry.LastFailedId}/{retry.LastFailedGlobalId}, " +
                        $"candidateCount={retry.Candidates.Count}; storedTargetCleared=true.");
                }
            }
        }

        private static bool IsLiveBuildingIdentity(int id, uint globalId, int owner)
        {
            var buildings = GameBuildingManagerAPI.Instance;
            return buildings.IsValidId(id) &&
                buildings.TryGetBuildingById(id, out GameBuilding* building) &&
                building != null && building->r_AliveState == AliveState.IsAlive &&
                building->r_GlobalId == globalId && building->r_PlayerIdOwner == owner;
        }

        private bool TryReadOrderedRaidCandidates(RaidRetry retry,
            out List<BuildingTarget> result, out string reason)
        {
            result = null;
            reason = "unavailable";
            if (retry.TargetPlayerId < 1 || retry.TargetPlayerId > MaxPlayers ||
                !GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(
                    retry.TargetPlayerId, out GamePlayerResources* resources) || resources == null)
                return false;
            int count = *(int*)((byte*)resources + RaidCandidateCountOffset);
            if (count < 0 || count > NativeRaidCandidateCapacity)
            {
                reason = $"candidateCountOutOfRange:{count}";
                return false;
            }
            if (!TryGetPriorityTable(out _, out byte* library))
            {
                reason = "nativeModuleUnavailable";
                return false;
            }
            int tableRva = retry.PriorityTableRva;
            int* priorities = (int*)(library + tableRva);
            ushort* ids = (ushort*)((byte*)resources + RaidCandidateListOffset);
            result = new List<BuildingTarget>(count);
            var seen = new HashSet<ulong>();
            for (int rank = 0; rank < NativePriorityCount; rank++)
            {
                // 0x2C620 replaces its selection on equal rank, so the last
                // candidate in the native list wins each priority tie.
                for (int index = count - 1; index >= 0; index--)
                {
                    int id = ids[index];
                    var buildings = GameBuildingManagerAPI.Instance;
                    if (id <= 0 || !buildings.IsValidId(id) ||
                        !buildings.TryGetBuildingById(id, out GameBuilding* building) ||
                        building == null || building->r_AliveState != AliveState.IsAlive ||
                        building->r_PlayerIdOwner != retry.TargetPlayerId ||
                        (int)building->r_BuildingType != priorities[rank]) continue;
                    ulong identity = BuildingIdentity(id, building->r_GlobalId);
                    if (seen.Add(identity))
                        result.Add(new BuildingTarget(id, building->r_GlobalId));
                }
            }
            reason = $"count={count},priorityTableRva=0x{tableRva:X}";
            Info($"RAID_FIX_CANDIDATES: session={sessionId}, tick={lastTick}, " +
                $"tribe={retry.TribeId}/{retry.TribeGlobalId}, targetPlayer={retry.TargetPlayerId}, " +
                $"{reason}, orderedCount={result.Count}.");
            return true;
        }

        private static bool TryGetPriorityTable(out int tableRva, out byte* library)
        {
            tableRva = 0;
            IntPtr module = GetModuleHandle("CrusaderDE.dll");
            library = (byte*)module.ToPointer();
            if (library == null) return false;
            int selector = (*(ushort*)(library + NativePrioritySelectorRva)) & 7;
            tableRva = selector < 2 ? NativePriorityTable0Rva :
                selector < 4 ? NativePriorityTable1Rva : NativePriorityTable2Rva;
            return true;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true,
            EntryPoint = "GetModuleHandleW")]
        private static extern IntPtr GetModuleHandle(string moduleName);

        internal void OnTribeMove(TribeIssueOrderMoveHereEventArgs args)
        {
            if (!active) return;
            try
            {
                if (!TryIdentifyRaidGroup(args.TribeId,
                        out int playerId, out int group, out uint tribeGlobalId)) return;
                Info($"RAID_DIAG_TRIBE_MOVE: session={sessionId}, tick=afterPreTick:{lastTick}, " +
                    $"phase={args.Phase}, player={playerId}, group={group}, " +
                    $"tribe={args.TribeId}/{tribeGlobalId}, destination={args.TileX}/{args.TileY}, " +
                    $"patrol={args.IsPatrolPath}, newOrder={args.IsNewOrder}, " +
                    $"moveType={args.MoveType}, return={args.ReturnValue}.");
            }
            catch (Exception ex)
            {
                Warn($"RAID_DIAG_TRIBE_MOVE_ERROR: session={sessionId}, tick={lastTick}, error={ex}.");
            }
        }

        internal void OnUnitMove(UnitMoveHereEventArgs args)
        {
            if (!active || args.UnitId <= 0) return;
            try
            {
                var units = GameUnitManagerAPI.Instance;
                if (!units.IsValidId(args.UnitId) ||
                    !units.TryGetUnitById(args.UnitId, out GameUnit* unit) || unit == null ||
                    unit->r_AliveState != AliveState.IsAlive ||
                    !TryIdentifyRaidGroup(unit->r_TribeId,
                        out int playerId, out int group, out uint tribeGlobalId)) return;
                Info($"RAID_DIAG_UNIT_MOVE: session={sessionId}, tick=afterPreTick:{lastTick}, " +
                    $"phase={args.Phase}, player={playerId}, group={group}, " +
                    $"tribe={unit->r_TribeId}/{tribeGlobalId}, unit={args.UnitId}/{unit->r_GlobalId}, " +
                    $"destination={args.TileX}/{args.TileY}, unknown={args.Unknown}, " +
                    $"return={args.ReturnValue}, current={unit->r_CurrentTilePositionX}/{unit->r_CurrentTilePositionY}.");
            }
            catch (Exception ex)
            {
                Warn($"RAID_DIAG_UNIT_MOVE_ERROR: session={sessionId}, tick={lastTick}, error={ex}.");
            }
        }

        private static bool TryIdentifyRaidGroup(int tribeId, out int playerId,
            out int group, out uint tribeGlobalId)
        {
            playerId = 0;
            group = -1;
            tribeGlobalId = 0;
            var tribes = GameTribeManagerAPI.Instance;
            if (tribeId <= 0 || !tribes.IsValidId(tribeId)) return false;
            for (int candidatePlayer = 1; candidatePlayer <= MaxPlayers; candidatePlayer++)
            {
                if (!GamePlayerManagerAPI.Instance.IsAIPlayer(candidatePlayer)) continue;
                for (int candidateGroup = 0; candidateGroup < RaidGroupCount; candidateGroup++)
                {
                    AITribeStorageRole16 role = (AITribeStorageRole16)
                        ((int)AITribeStorageRole16.HarassmentCombat0 + candidateGroup);
                    if (!tribes.TryGetAITribeStorageRole(candidatePlayer, role,
                            out ushort storedId, out uint storedGlobalId) ||
                        storedId != tribeId ||
                        !tribes.TryResolveAITribeStorageRole(candidatePlayer, role,
                            out GameTribe* tribe) || tribe == null ||
                        tribe->r_GlobalId != storedGlobalId) continue;
                    playerId = candidatePlayer;
                    group = candidateGroup;
                    tribeGlobalId = storedGlobalId;
                    return true;
                }
            }
            return false;
        }

        private void LogGroupUnits(int playerId, int group, int tribeId, uint tribeGlobalId)
        {
            var unitIds = new List<int>();
            if (!GameTribeManagerAPI.Instance.GetUnits(tribeId, unitIds)) return;
            var units = GameUnitManagerAPI.Instance;
            foreach (int unitId in unitIds)
            {
                if (!units.IsValidId(unitId) ||
                    !units.TryGetUnitById(unitId, out GameUnit* unit) || unit == null ||
                    unit->r_AliveState != AliveState.IsAlive || unit->r_TribeId != tribeId)
                    continue;
                string key = $"{playerId}/{group}/{tribeGlobalId}/{unit->r_GlobalId}";
                string position = $"{unit->r_CurrentTilePositionX}/{unit->r_CurrentTilePositionY}";
                string state = $"unit={unitId}/{unit->r_GlobalId},type={unit->r_UnitChimp}," +
                    $"position={position}," +
                    $"destination={unit->r_TargetTilePositionX}/{unit->r_TargetTilePositionY}," +
                    $"currentTile={unit->r_CurrentPositionTileId},targetTile={unit->r_TargetPositionTileId}," +
                    $"pathFlags={unit->r_PathPlanStateBitFlags},pathIndex={unit->r_CurrentPathPlanIndex}," +
                    $"pathLength={unit->r_PathPlanLength},aiState={unit->r_AIState}," +
                    $"attackMoveTarget={unit->r_AttackMoveToTargetTileX}/{unit->r_AttackMoveToTargetTileY}," +
                    $"contextUnit={unit->r_AI_ContextTargetUnitId}/{unit->r_AI_ContextTargetUnitGlobalId}," +
                    $"contextBuildingTile={unit->r_AI_ContextTargetBuildingTileId}";
                if (!previousUnits.TryGetValue(key, out string previous) ||
                    !String.Equals(previous, state, StringComparison.Ordinal))
                {
                    Info($"RAID_DIAG_UNIT_STATE: session={sessionId}, tick={lastTick}, " +
                        $"player={playerId}, group={group}, tribe={tribeId}/{tribeGlobalId}, " +
                        $"previous={previous ?? "unseen"}, current={state}.");
                    previousUnits[key] = state;
                }
                if (!previousPositions.TryGetValue(key, out string previousPosition) ||
                    !String.Equals(previousPosition, position, StringComparison.Ordinal))
                {
                    previousPositions[key] = position;
                    lastPositionChanges[key] = lastTick;
                }
                else if (lastTick % StationarySummaryInterval == 0 &&
                    lastPositionChanges.TryGetValue(key, out int changedAt) &&
                    lastTick - changedAt >= StationarySummaryInterval)
                {
                    Info($"RAID_DIAG_UNIT_STILL: session={sessionId}, tick={lastTick}, " +
                        $"player={playerId}, group={group}, tribe={tribeId}/{tribeGlobalId}, " +
                        $"positionUnchangedSince={changedAt}, {state}.");
                }
            }
        }

        private void LogTargetAccess(int playerId, int group, int tribeId,
            uint tribeGlobalId, int buildingId, uint targetGlobalId)
        {
            var buildings = GameBuildingManagerAPI.Instance;
            if (!buildings.IsValidId(buildingId) ||
                !buildings.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                building == null || building->r_GlobalId != targetGlobalId ||
                building->r_AliveState != AliveState.IsAlive)
            {
                Info($"RAID_DIAG_ACCESS: session={sessionId}, tick={lastTick}, " +
                    $"player={playerId}, group={group}, tribe={tribeId}/{tribeGlobalId}, " +
                    $"building={buildingId}/{targetGlobalId}, targetIdentityValid=false.");
                return;
            }

            var pathing = GamePathingManagerAPI.Instance;
            var tiles = GameTileManagerAPI.Instance;
            var unitIds = new List<int>();
            var sourceComponents = new HashSet<ushort>();
            var sourceUnits = new List<string>();
            if (GameTribeManagerAPI.Instance.GetUnits(tribeId, unitIds))
            {
                foreach (int unitId in unitIds)
                {
                    var units = GameUnitManagerAPI.Instance;
                    if (!units.IsValidId(unitId) ||
                        !units.TryGetUnitById(unitId, out GameUnit* unit) || unit == null ||
                        unit->r_AliveState != AliveState.IsAlive || unit->r_TribeId != tribeId)
                        continue;
                    bool componentValid = pathing.TryGetPathComponentId(unit->r_CurrentTilePositionX,
                        unit->r_CurrentTilePositionY, out ushort component);
                    sourceUnits.Add($"{unitId}/{unit->r_GlobalId}@" +
                        $"{unit->r_CurrentTilePositionX}/{unit->r_CurrentTilePositionY}:" +
                        (componentValid ? component.ToString() : "invalidTile"));
                    if (componentValid && component != 0)
                        sourceComponents.Add(component);
                }
            }

            int beginX = building->r_TilePositionXBegin;
            int beginY = building->r_TilePositionYBegin;
            int size = (int)building->r_OccupyTileGridSize;
            bool accessComponentValid = pathing.TryGetPathComponentId(
                building->r_TileAccessPositionX, building->r_TileAccessPositionY,
                out ushort accessComponent);
            Info($"RAID_DIAG_ACCESS: session={sessionId}, tick={lastTick}, " +
                $"player={playerId}, group={group}, tribe={tribeId}/{tribeGlobalId}, " +
                $"building={buildingId}/{targetGlobalId}, targetIdentityValid=true, " +
                $"type={building->r_BuildingType}, owner={building->r_PlayerIdOwner}, " +
                $"footprintBegin={beginX}/{beginY}, footprintGridSize={size}, " +
                $"accessPoint={building->r_TileAccessPositionX}/{building->r_TileAccessPositionY}, " +
                $"accessComponent={(accessComponentValid ? accessComponent.ToString() : "invalidTile")}, " +
                $"sourceUnits={String.Join(",", sourceUnits)}, " +
                $"sourceComponents={String.Join(",", sourceComponents)}.");

            // This bounding ring is a read-only clue, not Vanilla's complete attack-tile test.
            if (size < 1 || size > 32) return;
            for (int y = beginY - 1; y <= beginY + size; y++)
            {
                for (int x = beginX - 1; x <= beginX + size; x++)
                {
                    if (x != beginX - 1 && x != beginX + size &&
                        y != beginY - 1 && y != beginY + size) continue;
                    if (!pathing.TryGetPathComponentId(x, y, out ushort destinationComponent))
                        continue;
                    int tileId = tiles.GetTileId(x, y);
                    if ((uint)tileId >= (uint)tiles.GetStructureLayer().Length) continue;
                    int connectedComponents = 0;
                    if (destinationComponent != 0)
                    {
                        foreach (ushort sourceComponent in sourceComponents)
                        {
                            if (pathing.ArePathComponentsConnected(playerId, sourceComponent,
                                    destinationComponent, PathConnectionQueryMode.ExcludeLadderClimb))
                                connectedComponents++;
                        }
                    }
                    Info($"RAID_DIAG_ACCESS_TILE: session={sessionId}, tick={lastTick}, " +
                        $"player={playerId}, group={group}, tribe={tribeId}/{tribeGlobalId}, " +
                        $"building={buildingId}/{targetGlobalId}, xy={x}/{y}, tile={tileId}, " +
                        $"component={destinationComponent}, connectedComponents={connectedComponents}/{sourceComponents.Count}, " +
                        $"walkableNoBuilding={tiles.IsTileWalkableAndUnoccupied(tileId)}, " +
                        $"occupyingBuilding={tiles.GetTileBuildingId(tileId)}, height={tiles.GetTileHeight(tileId)}, " +
                        $"scope=boundingRingClue.");
                }
            }
        }

        private static string FormatGroup(ushort tribeId, uint tribeGlobalId, GameTribe* tribe)
        {
            if (tribe == null)
                return $"tribe={tribeId}/{tribeGlobalId},resolved=false";

            byte* bytes = (byte*)tribe;
            ushort targetId = *(ushort*)(bytes + TargetBuildingIdOffset);
            uint targetGlobalId = *(uint*)(bytes + TargetGlobalIdOffset);
            short statusA = *(short*)(bytes + TribeStatusAOffset);
            short statusB = *(short*)(bytes + TribeStatusBOffset);
            short statusC = *(short*)(bytes + TribeStatusCOffset);
            return $"tribe={tribeId}/{tribeGlobalId},resolved=true,units={tribe->r_UnitsInGroup}," +
                $"stance={tribe->r_TribeStance},status={statusA}/{statusB}/{statusC}," +
                $"target={targetId}/{targetGlobalId},building={FormatBuilding(targetId, targetGlobalId)}";
        }

        private static string FormatBuilding(int buildingId, uint? expectedGlobalId)
        {
            if (buildingId <= 0) return "none";
            var buildings = GameBuildingManagerAPI.Instance;
            if (!buildings.IsValidId(buildingId) ||
                !buildings.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                building == null)
                return "slotInvalid";
            bool live = building->r_AliveState == AliveState.IsAlive;
            string slot = $"type={building->r_BuildingType},owner={building->r_PlayerIdOwner}," +
                $"global={building->r_GlobalId},alive={building->r_AliveState},slotAlive={live}";
            if (!expectedGlobalId.HasValue) return slot;
            bool matches = building->r_GlobalId == expectedGlobalId.Value;
            return $"{slot},globalMatchesTarget={matches},targetIdentityValid={matches && live}";
        }

        private static uint? ReadBuildingGlobalId(int buildingId)
        {
            var buildings = GameBuildingManagerAPI.Instance;
            if (!buildings.IsValidId(buildingId) ||
                !buildings.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                building == null || building->r_AliveState != AliveState.IsAlive)
                return null;
            return building->r_GlobalId;
        }

        private static string MatchingTargetGroups(int buildingId, uint buildingGlobalId)
        {
            var matches = new List<string>();
            var players = GamePlayerManagerAPI.Instance;
            var tribes = GameTribeManagerAPI.Instance;
            for (int playerId = 1; playerId <= MaxPlayers; playerId++)
            {
                if (!players.IsAIPlayer(playerId)) continue;
                for (int group = 0; group < RaidGroupCount; group++)
                {
                    AITribeStorageRole16 role = (AITribeStorageRole16)((int)AITribeStorageRole16.HarassmentCombat0 + group);
                    if (!tribes.TryGetAITribeStorageRole(playerId, role,
                            out ushort tribeId, out uint tribeGlobalId) ||
                        tribeId == 0 || !tribes.IsValidId(tribeId) ||
                        !tribes.TryResolveAITribeStorageRole(playerId, role,
                        out GameTribe* tribe)) continue;
                    byte* bytes = (byte*)tribe;
                    if (*(ushort*)(bytes + TargetBuildingIdOffset) == buildingId &&
                        *(uint*)(bytes + TargetGlobalIdOffset) == buildingGlobalId)
                        matches.Add($"p{playerId}g{group}t{tribeId}/{tribeGlobalId}");
                }
            }
            return matches.Count == 0 ? "none" : String.Join(",", matches);
        }

        private static void ValidateLayout()
        {
            if ((int)AITribeStorageRole16.HarassmentCombat0 != 180 ||
                (int)AITribeStorageRole16.HarassmentCombat5 != 185 ||
                Marshal.SizeOf(typeof(GameTribe)) != 0x688 ||
                Marshal.OffsetOf(typeof(GameTribe), nameof(GameTribe.r_UnitsInGroup)).ToInt32() != 0x32 ||
                Marshal.OffsetOf(typeof(GameTribe), nameof(GameTribe.r_TribeStance)).ToInt32() != 0x60A ||
                Marshal.OffsetOf(typeof(GameTribe), nameof(GameTribe.N00000580)).ToInt32() != 0x620 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_CurrentTilePositionX)).ToInt32() != 0xC0 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_TargetTilePositionX)).ToInt32() != 0xC4 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_PathPlanStateBitFlags)).ToInt32() != 0xF2 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_AIState)).ToInt32() != 0x2BC ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_TribeId)).ToInt32() != 0x2D4 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_AI_ContextTargetBuildingTileId)).ToInt32() != 0x3A4 ||
                Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.N00003F66)).ToInt32() != RetargetCounterOffset ||
                Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.N00005341)).ToInt32() != RemainingCounterOffset ||
                Marshal.SizeOf(typeof(GamePlayerResources)) != 0x583C ||
                Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.N000040A3)).ToInt32() != RaidCandidateListOffset ||
                Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.N000040BC)).ToInt32() != RaidCandidateCountOffset)
                throw new InvalidOperationException("Installed Script Extender raid layout differs from audited native layout.");
        }

        private void ClearObservations()
        {
            Array.Clear(previousGroups, 0, previousGroups.Length);
            Array.Clear(previousCounters, 0, previousCounters.Length);
            Array.Clear(previousTargetIdentities, 0, previousTargetIdentities.Length);
            previousUnits.Clear();
            previousPositions.Clear();
            lastPositionChanges.Clear();
            pendingDeletes.Clear();
            pendingAttackCandidates.Clear();
            pendingRaidRetries.Clear();
            rejectedRaidTargets.Clear();
            issuingFallback = false;
            fallbackTribeId = 0;
            fallbackBuildingId = 0;
            fallbackBuildingGlobalId = 0;
            fallbackResult = AttackResult.Unknown;
        }

        private void Info(string value) => Shared.DebugLogHelper.LogInfo(log, value);
        private void Warn(string value) => Shared.DebugLogHelper.LogWarning(log, value);

        private sealed class AttackCandidateCapture
        {
            internal readonly int Tick;
            internal readonly uint TribeGlobalId;
            internal readonly TribeAICommand Command;
            internal readonly int BuildingId;
            internal readonly uint BuildingGlobalId;
            internal readonly AttackCandidateSnapshot Snapshot;

            internal AttackCandidateCapture(int tick, uint tribeGlobalId, TribeAICommand command,
                int buildingId, uint buildingGlobalId, AttackCandidateSnapshot snapshot)
            {
                Tick = tick;
                TribeGlobalId = tribeGlobalId;
                Command = command;
                BuildingId = buildingId;
                BuildingGlobalId = buildingGlobalId;
                Snapshot = snapshot;
            }
        }

        private sealed class AttackCandidateSnapshot
        {
            private readonly bool available;
            private readonly int terminatorAt;
            internal readonly CandidateRecord[] Records;

            internal AttackCandidateSnapshot(bool available, int terminatorAt, CandidateRecord[] records)
            {
                this.available = available;
                this.terminatorAt = terminatorAt;
                Records = records;
            }

            internal bool Available => available;
            internal bool Complete => available && terminatorAt >= 0;

            internal bool SameRecords(AttackCandidateSnapshot other)
            {
                if (other == null || available != other.available ||
                    terminatorAt != other.terminatorAt || Records.Length != other.Records.Length)
                    return false;
                for (int i = 0; i < Records.Length; i++)
                    if (!Records[i].Equals(other.Records[i])) return false;
                return true;
            }

            internal string Describe()
            {
                CandidateRecord first = Records.Length != 0 ? Records[0] : default;
                var preview = new List<string>();
                for (int i = 0; i < Records.Length && i < MaxAttackCandidateSnapshotEntries; i++)
                    preview.Add($"{i}:{Records[i].ApproachTile}/{Records[i].BuildingTile}/{Records[i].Score}");
                int pairs = 0;
                foreach (CandidateRecord record in Records)
                    if (record.BuildingTile != 0) pairs++;
                return $"scratchAvailable={available}, firstPosition={first.ApproachTile}, " +
                    $"firstAttackTile={first.BuildingTile}, nativeFirstGatePass={available && first.BuildingTile != 0}, " +
                    $"terminatorAt={(terminatorAt >= 0 ? terminatorAt.ToString() : "notInFirst500")}, " +
                    $"entryCount={Records.Length}, pairedEntries={pairs}, " +
                    $"entries={String.Join(",", preview)}";
            }
        }

        private readonly struct CandidateRecord : IEquatable<CandidateRecord>
        {
            internal readonly int ApproachTile;
            internal readonly int BuildingTile;
            internal readonly int Score;

            internal CandidateRecord(int approachTile, int buildingTile, int score)
            {
                ApproachTile = approachTile;
                BuildingTile = buildingTile;
                Score = score;
            }

            public bool Equals(CandidateRecord other) =>
                ApproachTile == other.ApproachTile && BuildingTile == other.BuildingTile &&
                Score == other.Score;
        }

        private enum AttackResult { Unknown, NoAttackPoint, AttackPoint }

        private sealed class RaidRetry
        {
            internal readonly int PlayerId;
            internal readonly int Group;
            internal readonly int TribeId;
            internal readonly uint TribeGlobalId;
            internal readonly int TargetPlayerId;
            internal readonly int PriorityTableRva;
            internal int LastFailedId;
            internal uint LastFailedGlobalId;
            internal List<BuildingTarget> Candidates;
            internal int NextCandidate;

            internal RaidRetry(int playerId, int group, int tribeId, uint tribeGlobalId,
                int targetPlayerId, int failedId, uint failedGlobalId, int priorityTableRva)
            {
                PlayerId = playerId;
                Group = group;
                TribeId = tribeId;
                TribeGlobalId = tribeGlobalId;
                TargetPlayerId = targetPlayerId;
                PriorityTableRva = priorityTableRva;
                LastFailedId = failedId;
                LastFailedGlobalId = failedGlobalId;
            }
        }

        private readonly struct BuildingTarget
        {
            internal readonly int Id;
            internal readonly uint GlobalId;

            internal BuildingTarget(int id, uint globalId)
            {
                Id = id;
                GlobalId = globalId;
            }
        }

        private sealed class DeleteCapture
        {
            internal readonly uint? GlobalId;
            internal readonly string PreBuilding;
            internal readonly string RaidGroups;

            internal DeleteCapture(uint? globalId, string preBuilding, string raidGroups)
            {
                GlobalId = globalId;
                PreBuilding = preBuilding;
                RaidGroups = raidGroups;
            }
        }
    }
}
