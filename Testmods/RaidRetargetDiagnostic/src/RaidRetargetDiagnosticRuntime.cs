using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static RaidRetargetDiagnostic.RaidAttackFieldEvaluation;

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
        private const int NativeRaidCandidateCapacity = 100;
        private const int NativePlayerResourceStride = 0x583C;
        private const int NativeResourcePointerBias = 0x5C;
        private const int NativeRaidCandidateListRva = 0x379E38C;
        private const int NativeRaidCandidateCountRva = 0x379E454;
        private const int NativeRaidTargetPlayerRva = 0x379D9A4;
        private const int RaidCandidateListOffset = 0x3560 + NativeResourcePointerBias;
        private const int RaidCandidateCountOffset = 0x3628 + NativeResourcePointerBias;
        private const int NativePrioritySelectorRva = 0x856A6D2;
        private const int NativePriorityTable0Rva = 0x2C7F80;
        private const int NativePriorityTable1Rva = 0x2C7EC0;
        private const int NativePriorityTable2Rva = 0x2C7E00;
        private const int NativePriorityCount = 47;
        private const int MaximumRetryCommandsPerTick = 4;
        private const int RejectedTargetDurationTicks = 300;
        private const int SummaryIntervalTicks = 2000;

        private readonly ManualLogSource log;
        private readonly bool fixesLoaded;
        private readonly string[,] previousGroups = new string[MaxPlayers + 1, RaidGroupCount];
        private readonly string[,] previousTargetIdentities = new string[MaxPlayers + 1, RaidGroupCount];
        private readonly Dictionary<ulong, DamageSummary> targetDamage =
            new Dictionary<ulong, DamageSummary>();
        private readonly Dictionary<RaidGroupKey, MoveSummary> groupMoves =
            new Dictionary<RaidGroupKey, MoveSummary>();
        private readonly Dictionary<string, RepeatSummary> repeatedMessages =
            new Dictionary<string, RepeatSummary>();
        private readonly Dictionary<int, Stack<DeleteCapture>> pendingDeletes =
            new Dictionary<int, Stack<DeleteCapture>>();
        private readonly Dictionary<int, Stack<AttackCandidateCapture>> pendingAttackCandidates =
            new Dictionary<int, Stack<AttackCandidateCapture>>();
        private readonly Dictionary<RaidGroupKey, RaidRetry> pendingRaidRetries =
            new Dictionary<RaidGroupKey, RaidRetry>();
        private readonly Dictionary<RaidGroupKey, Dictionary<ulong, int>> rejectedRaidTargets =
            new Dictionary<RaidGroupKey, Dictionary<ulong, int>>();
        private bool issuingFallback;
        private int fallbackTribeId;
        private int fallbackBuildingId;
        private uint fallbackBuildingGlobalId;
        private AttackResult fallbackResult;
        private string fallbackDetails;
        private bool active;
        private bool firstTickLogged;
        private int lastTick = -1;
        private long sessionId;
        private int targetChangeCount;
        private int attackCommandCount;
        private int retryCount;
        private int suppressedMessageCount;
        private int emittedLineCount;
        private int emittedCharacterCount;

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
            Info($"RAID_DIAG_CODES: session={sessionId}, retryAttempts=id/global/type:result, " +
                "result=A(validMeleePoint),N(noMeleePoint),U(unknown), !i=commandNotIssued, !s=targetNotStored.");
        }

        internal void OnSessionEnded()
        {
            if (active)
            {
                FlushPendingRetries("sessionEnded");
                FlushDamageSummaries("sessionEnded");
                FlushMoveSummaries("sessionEnded");
                FlushRepeatSummaries();
                LogSummary("end");
                Info($"RAID_DIAG_END: session={sessionId}, tick={lastTick}.");
            }
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
                    FlushPendingRetries("tickReset");
                    FlushDamageSummaries("tickReset");
                    FlushMoveSummaries("tickReset");
                    FlushRepeatSummaries();
                    LogSummary("tickReset");
                    ClearObservations();
                    lastTick = -1;
                    Warn($"RAID_DIAG_TICK_RESET: session={sessionId}, previous={resetFrom}, current={tick}.");
                }
                lastTick = tick;
                PruneReplacedRaidGroups();
                var players = GamePlayerManagerAPI.Instance;
                var tribes = GameTribeManagerAPI.Instance;
                for (int playerId = 1; playerId <= MaxPlayers; playerId++)
                {
                    if (!players.IsAIPlayer(playerId) ||
                        !players.TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
                        continue;

                    for (int group = 0; group < RaidGroupCount; group++)
                    {
                        AITribeStorageRole16 role = (AITribeStorageRole16)((int)AITribeStorageRole16.HarassmentCombat0 + group);
                        bool hasRole = tribes.TryGetAITribeStorageRole(playerId, role,
                            out ushort tribeId, out uint tribeGlobalId);
                        GameTribe* tribe = null;
                        bool resolved = hasRole && tribeId != 0 && tribes.IsValidId(tribeId) &&
                            tribes.TryResolveAITribeStorageRole(playerId, role,
                                out tribe);
                        string identity = resolved && tribe != null
                            ? $"{tribeId}/{tribeGlobalId}" : "none";
                        string previousIdentity = previousGroups[playerId, group];
                        if (!String.Equals(previousIdentity, identity, StringComparison.Ordinal))
                        {
                            previousGroups[playerId, group] = identity;
                            previousTargetIdentities[playerId, group] = null;
                            if (identity != "none" || previousIdentity != null && previousIdentity != "none")
                            {
                                string eligibility = identity == "none" ? "none" :
                                    TryIsMeleeRaidGroup(tribeId, tribeGlobalId, out string reason)
                                        ? "eligibleMelee" : reason;
                                Info($"RAID_DIAG_GROUP: session={sessionId}, tick={tick}, " +
                                    $"player={playerId}, group={group}, role={(int)role}, " +
                                    $"previous={previousIdentity ?? "none"}, current={identity}, " +
                                    $"units={(tribe != null ? tribe->r_UnitsInGroup.ToString() : "0")}, " +
                                    $"eligibility={eligibility}.");
                            }
                        }
                        if (resolved && tribe != null)
                        {
                            byte* bytes = (byte*)tribe;
                            ushort targetId = *(ushort*)(bytes + TargetBuildingIdOffset);
                            uint targetGlobalId = *(uint*)(bytes + TargetGlobalIdOffset);
                            string targetIdentity = $"{targetId}/{targetGlobalId}";
                            if (!String.Equals(previousTargetIdentities[playerId, group], targetIdentity,
                                    StringComparison.Ordinal))
                            {
                                string previousTarget = previousTargetIdentities[playerId, group];
                                previousTargetIdentities[playerId, group] = targetIdentity;
                                if (targetId != 0 || previousTarget != null && previousTarget != "0/0")
                                {
                                    targetChangeCount++;
                                    short statusA = *(short*)(bytes + TribeStatusAOffset);
                                    short statusB = *(short*)(bytes + TribeStatusBOffset);
                                    short statusC = *(short*)(bytes + TribeStatusCOffset);
                                    Info($"RAID_DIAG_TARGET: session={sessionId}, tick={tick}, " +
                                        $"player={playerId}, group={group}, tribe={identity}, " +
                                        $"previous={previousTarget ?? "none"}, current={targetIdentity}, " +
                                        $"building={FormatBuilding(targetId, targetGlobalId)}, " +
                                        $"status={statusA}/{statusB}/{statusC}, " +
                                        $"retarget={resources->N00003F66},remaining100={resources->N00005341}.");
                                }
                            }
                        }
                    }
                }
                ProcessRaidRetries();
                if (tick % SummaryIntervalTicks == 0)
                {
                    LogSummary("periodic");
                }
            }
            catch (Exception ex)
            {
                LogRepeated($"tickError:{ex.GetType().Name}:{ex.Message}",
                    $"RAID_DIAG_TICK_ERROR: session={sessionId}, tick={tick}, error={ex}.", true);
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
                ulong key = BuildingIdentity(buildingId, buildingGlobalId.Value);
                if (!targetDamage.TryGetValue(key, out DamageSummary summary))
                {
                    summary = new DamageSummary(buildingId, buildingGlobalId.Value,
                        FormatBuilding(buildingId, buildingGlobalId.Value), groups, lastTick);
                    targetDamage.Add(key, summary);
                    Info($"RAID_DIAG_DAMAGE_START: session={sessionId}, tick={lastTick}, " +
                        $"building={buildingId}/{buildingGlobalId.Value}, {summary.Building}, " +
                        $"raidGroups={groups}, sourcePlayer={args.PlayerIdSource}.");
                }
                summary.Add(lastTick, args.Damage, args.PlayerIdSource);
            }
            catch (Exception ex)
            {
                LogRepeated($"damageError:{ex.GetType().Name}:{ex.Message}",
                    $"RAID_DIAG_DAMAGE_ERROR: session={sessionId}, tick={lastTick}, error={ex}.", true);
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
                    if (!globalId.HasValue || (groups == "none" &&
                        !targetDamage.ContainsKey(BuildingIdentity(args.BuildingId, globalId.Value)))) return;
                    var capture = new DeleteCapture(globalId, FormatBuilding(args.BuildingId, globalId), groups);
                    if (!pendingDeletes.TryGetValue(args.BuildingId, out Stack<DeleteCapture> stack))
                    {
                        stack = new Stack<DeleteCapture>();
                        pendingDeletes.Add(args.BuildingId, stack);
                    }
                    stack.Push(capture);
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
                    if (capture == null) return;
                    Info($"RAID_DIAG_DELETE: session={sessionId}, tick={lastTick}, " +
                        $"building={args.BuildingId}/{capture.GlobalId}, " +
                        $"preBuilding={capture.PreBuilding}, raidGroupsAtPre={capture.RaidGroups}, " +
                        $"postSlot={FormatBuilding(args.BuildingId, null)}.");
                    FlushDamageSummary(BuildingIdentity(args.BuildingId, capture.GlobalId.Value), "deleted");
                }
            }
            catch (Exception ex)
            {
                LogRepeated($"deleteError:{ex.GetType().Name}:{ex.Message}",
                    $"RAID_DIAG_DELETE_ERROR: session={sessionId}, tick={lastTick}, error={ex}.", true);
            }
        }

        internal void OnTribeOrder(TribeIssueOrderWithTargetEventArgs args)
        {
            if (!active) return;
            try
            {
                if ((args.AICommand == TribeAICommand.AttackBuilding ||
                    args.AICommand == TribeAICommand.ForceAttackBuilding) &&
                    TryIdentifyRaidGroup(args.TribeId, out _, out _, out _))
                    LogAttackCandidates(args);
            }
            catch (Exception ex)
            {
                LogRepeated($"orderError:{ex.GetType().Name}:{ex.Message}",
                    $"RAID_DIAG_TRIBE_ORDER_ERROR: session={sessionId}, tick={lastTick}, error={ex}.", true);
            }
        }

        private void LogAttackCandidates(TribeIssueOrderWithTargetEventArgs args)
        {
            var tribes = GameTribeManagerAPI.Instance;
            if (args.TribeId <= 0 || !tribes.IsValidId(args.TribeId) ||
                !tribes.TryGetTribeById(args.TribeId, out GameTribe* tribe) || tribe == null ||
                tribe->r_AliveState != AliveState.IsAlive) return;

            uint tribeGlobalId = tribe->r_GlobalId;
            bool raid = TryIdentifyRaidGroup(args.TribeId,
                out int raidPlayer, out int raidGroup, out uint raidGlobalId) &&
                raidGlobalId == tribeGlobalId;
            if (!raid) return;
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
                    LogRepeated($"unpaired:{args.TribeId}/{tribeGlobalId}",
                        $"RAID_DIAG_ATTACK_UNPAIRED: session={sessionId}, tick={lastTick}, " +
                        $"tribe={args.TribeId}/{tribeGlobalId}, discardedPreCalls={stack.Count}.", true);
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

            if (args.Phase != EventHookPhase.Post) return;
            var tiles = GameTileManagerAPI.Instance;
            AttackResult result = Evaluate(snapshot, pairMatches, freshness, args.ReturnValue,
                ReadBuildingGlobalId(args.TargetValue1) == targetGlobalId, args.TargetValue1,
                tiles.GetStructureLayer().Length, GetBuildingAtTile, IsCardinalPair,
                out string validation);
            if (issuingFallback)
            {
                if (args.TribeId == fallbackTribeId &&
                    args.TargetValue1 == fallbackBuildingId &&
                    targetGlobalId == fallbackBuildingGlobalId)
                {
                    fallbackResult = result;
                    fallbackDetails = $"freshness={freshness},prePostMatch={pairMatches}," +
                        $"validation={validation},return={args.ReturnValue},{snapshot.DescribeCompact()}";
                }
                return;
            }
            attackCommandCount++;
            Info($"RAID_DIAG_ATTACK: session={sessionId}, tick={lastTick}, " +
                $"player={raidPlayer}, role={raidGroup}, tribe={args.TribeId}/{tribeGlobalId}, " +
                $"command={args.AICommand}, target={args.TargetValue1}/{targetGlobalId}, " +
                $"building={FormatBuilding(args.TargetValue1, targetGlobalId)}, " +
                $"return={args.ReturnValue}, prePostMatch={pairMatches}, freshness={freshness}, " +
                $"validation={validation}, result={result}, {snapshot.DescribeCompact()}.");
            if (args.AICommand != TribeAICommand.AttackBuilding) return;
            RaidGroupKey raidKey = new RaidGroupKey(raidPlayer, raidGroup,
                args.TribeId, tribeGlobalId);
            string eligibilityReason = "notChecked";
            bool eligible = result == AttackResult.NoAttackPoint &&
                TryIsMeleeRaidGroup(args.TribeId, tribeGlobalId, out eligibilityReason);
            if (eligible &&
                GameBuildingManagerAPI.Instance.TryGetBuildingById(args.TargetValue1,
                    out GameBuilding* failedBuilding) && failedBuilding != null &&
                failedBuilding->r_GlobalId == targetGlobalId &&
                TryGetPriorityTable(out int priorityTableRva, out _))
            {
                RememberRejectedTarget(raidKey, args.TargetValue1, targetGlobalId);
                if (pendingRaidRetries.TryGetValue(raidKey, out RaidRetry prior))
                    LogRetry(prior, "superseded", 0, 0);
                pendingRaidRetries[raidKey] = new RaidRetry(raidPlayer, raidGroup,
                    args.TribeId, tribeGlobalId, failedBuilding->r_PlayerIdOwner,
                    args.TargetValue1, targetGlobalId, priorityTableRva, lastTick);
            }
            else if (result == AttackResult.Unknown)
                LogRepeated($"uncertain:{freshness}:{validation}:{raidPlayer}/{raidGroup}",
                    $"RAID_FIX_UNCERTAIN: session={sessionId}, tick={lastTick}, " +
                    $"tribe={args.TribeId}/{tribeGlobalId}, target={args.TargetValue1}/{targetGlobalId}, " +
                    $"freshness={freshness}, validation={validation}; vanillaPreserved=true.", false);
            else if (result == AttackResult.NoAttackPoint)
                LogRepeated($"uncertain:{eligibilityReason}:{raidPlayer}/{raidGroup}",
                    $"RAID_FIX_UNCERTAIN: session={sessionId}, tick={lastTick}, " +
                    $"tribe={args.TribeId}/{tribeGlobalId}, target={args.TargetValue1}/{targetGlobalId}, " +
                    $"reason={(eligible ? "priorityOrBuildingUnavailable" : eligibilityReason)}; vanillaPreserved=true.", true);
        }

        private static AttackCandidateSnapshot ReadAttackCandidateSnapshot()
        {
            IntPtr context = GamePathingManagerAPI.Instance.GetPathfindingContextView().Address;
            return Read(context, AttackCandidateListOffset);
        }

        private static bool IsCardinalPair(int approachTile, int buildingTile)
        {
            var tiles = GameTileManagerAPI.Instance;
            var stand = tiles.GetTileVectorFromId(approachTile);
            var target = tiles.GetTileVectorFromId(buildingTile);
            return Math.Abs((int)stand.X - target.X) + Math.Abs((int)stand.Y - target.Y) == 1 &&
                tiles.GetTileId(stand.X, stand.Y) == approachTile;
        }

        private static int GetBuildingAtTile(int tileId) =>
            GameTileManagerAPI.Instance.GetTileBuildingId(tileId);

        private static bool TryIsMeleeRaidGroup(int tribeId, uint tribeGlobalId,
            out string reason)
        {
            reason = "tribeUnavailable";
            var tribes = GameTribeManagerAPI.Instance;
            if (!tribes.TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null ||
                tribe->r_GlobalId != tribeGlobalId) return false;
            var unitIds = new List<int>();
            if (!tribes.GetUnits(tribeId, unitIds))
            {
                reason = "unitListUnavailable";
                return false;
            }
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
                    case eChimps.CHIMP_TYPE_TUNNELER:
                    case eChimps.CHIMP_TYPE_PIKEMAN:
                    case eChimps.CHIMP_TYPE_MACEMAN:
                    case eChimps.CHIMP_TYPE_SWORDSMAN:
                    case eChimps.CHIMP_TYPE_KNIGHT:
                    case eChimps.CHIMP_TYPE_MONK:
                    case eChimps.CHIMP_TYPE_LORD:
                    case eChimps.CHIMP_TYPE_ARAB_SLAVE:
                    case eChimps.CHIMP_TYPE_ARAB_ASSASIN:
                    case eChimps.CHIMP_TYPE_ARAB_SWORDSMAN:
                    case eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER:
                    case eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH:
                    case eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL:
                    case eChimps.CHIMP_TYPE_BEDOUIN_SAPPER:
                    case eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER:
                        meleeCount++;
                        break;
                    case eChimps.CHIMP_TYPE_ARCHER:
                    case eChimps.CHIMP_TYPE_XBOWMAN:
                    case eChimps.CHIMP_TYPE_ARAB_BOW:
                    case eChimps.CHIMP_TYPE_ARAB_SLINGER:
                    case eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER:
                        // Command 9 does not count these toward its melee approach search.
                        break;
                    case eChimps.CHIMP_TYPE_CATAPULT:
                    case eChimps.CHIMP_TYPE_TREBUCHET:
                    case eChimps.CHIMP_TYPE_MANGONEL:
                    case eChimps.CHIMP_TYPE_BALLISTA:
                    case eChimps.CHIMP_TYPE_ARAB_BALLISTA:
                    case eChimps.CHIMP_TYPE_BATTERING_RAM:
                        reason = $"buildingCapableSiegeUnit:{unit->r_UnitChimp}";
                        return false;
                    default:
                        reason = $"unknownOrSpecialUnit:{unit->r_UnitChimp}";
                        return false;
                }
            }
            reason = meleeCount != 0 ? "eligibleMelee" : "noMeleeAttackers";
            return meleeCount != 0;
        }

        private static ulong BuildingIdentity(int buildingId, uint globalId) =>
            ((ulong)globalId << 32) | (uint)buildingId;

        private void RememberRejectedTarget(RaidGroupKey groupKey, int buildingId, uint globalId)
        {
            if (!rejectedRaidTargets.TryGetValue(groupKey, out Dictionary<ulong, int> rejected))
            {
                rejected = new Dictionary<ulong, int>();
                rejectedRaidTargets.Add(groupKey, rejected);
            }
            rejected[BuildingIdentity(buildingId, globalId)] =
                lastTick + RejectedTargetDurationTicks;
        }

        private bool IsRejectedTarget(RaidGroupKey groupKey, int buildingId, uint globalId) =>
            rejectedRaidTargets.TryGetValue(groupKey, out Dictionary<ulong, int> rejected) &&
            rejected.TryGetValue(BuildingIdentity(buildingId, globalId), out int until) &&
            until > lastTick;

        private void PruneReplacedRaidGroups()
        {
            var keys = new HashSet<RaidGroupKey>(rejectedRaidTargets.Keys);
            keys.UnionWith(pendingRaidRetries.Keys);
            keys.UnionWith(groupMoves.Keys);
            foreach (RaidGroupKey key in keys)
            {
                if (IsCurrentRaidGroup(key)) continue;
                rejectedRaidTargets.Remove(key);
                if (pendingRaidRetries.TryGetValue(key, out RaidRetry replaced))
                {
                    LogRetry(replaced, "groupReplaced", 0, 0);
                    pendingRaidRetries.Remove(key);
                }
                if (groupMoves.TryGetValue(key, out MoveSummary oldMove))
                {
                    FlushMoveSummary(key, oldMove, "groupReplaced");
                    groupMoves.Remove(key);
                }
                Info($"RAID_FIX_GROUP_REPLACED: session={sessionId}, tick={lastTick}, " +
                    $"player={key.PlayerId}, group={key.Group}, " +
                    $"tribe={key.TribeId}/{key.TribeGlobalId}; cachedStateCleared=true.");
            }
        }

        private static bool IsCurrentRaidGroup(RaidGroupKey key)
        {
            if (key.PlayerId < 1 || key.PlayerId > MaxPlayers ||
                key.Group < 0 || key.Group >= RaidGroupCount ||
                !GamePlayerManagerAPI.Instance.IsAIPlayer(key.PlayerId)) return false;
            AITribeStorageRole16 role = (AITribeStorageRole16)
                ((int)AITribeStorageRole16.HarassmentCombat0 + key.Group);
            return GameTribeManagerAPI.Instance.TryGetAITribeStorageRole(key.PlayerId, role,
                out ushort tribeId, out uint globalId) &&
                tribeId == key.TribeId && globalId == key.TribeGlobalId &&
                GameTribeManagerAPI.Instance.TryResolveAITribeStorageRole(key.PlayerId, role,
                    out GameTribe* tribe) && tribe != null &&
                tribe->r_GlobalId == key.TribeGlobalId;
        }

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
                    !TryIsMeleeRaidGroup(retry.TribeId, retry.TribeGlobalId, out _))
                {
                    LogRetry(retry, "groupUnavailableOrIneligible", 0, 0);
                    continue;
                }

                byte* tribeBytes = (byte*)tribe;
                int currentId = *(ushort*)(tribeBytes + TargetBuildingIdOffset);
                uint currentGlobal = *(uint*)(tribeBytes + TargetGlobalIdOffset);
                if (currentId != retry.LastFailedId || currentGlobal != retry.LastFailedGlobalId)
                {
                    LogRetry(retry, $"targetChanged:{currentId}/{currentGlobal}", 0, 0);
                    continue;
                }

                if (retry.Candidates == null && !TryReadOrderedRaidCandidates(retry,
                        out retry.Candidates, out string reason))
                {
                    LogRetry(retry, $"candidateListUncertain:{reason};vanillaPreserved", 0, 0);
                    continue;
                }

                int issued = 0;
                bool finished = false;
                while (retry.NextCandidate < retry.Candidates.Count &&
                    issued < MaximumRetryCommandsPerTick)
                {
                    BuildingTarget candidate = retry.Candidates[retry.NextCandidate++];
                    if (IsRejectedTarget(retry.Key, candidate.Id, candidate.GlobalId) ||
                        !IsLiveBuildingIdentity(candidate.Id, candidate.GlobalId,
                            retry.TargetPlayerId)) continue;
                    fallbackResult = AttackResult.Unknown;
                    fallbackDetails = "freshness=missingPost,validation=notEvaluated," +
                        "entryCount=unavailable,terminatorAt=unavailable,firstPair=unavailable";
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
                    if (!commandIssued)
                    {
                        fallbackResult = AttackResult.Unknown;
                        fallbackDetails += ",commandNotIssued=true";
                    }
                    if (!storedCandidate)
                    {
                        fallbackResult = AttackResult.Unknown;
                        fallbackDetails += ",storedTargetMismatch=true";
                    }
                    issued++;
                    string code = fallbackResult == AttackResult.AttackPoint ? "A" :
                        fallbackResult == AttackResult.NoAttackPoint ? "N" : "U";
                    retry.Attempts.Add($"{candidate.Id}/{candidate.GlobalId}/{candidate.Type}:{code}" +
                        (commandIssued ? "" : "!i") + (storedCandidate ? "" : "!s") +
                        (fallbackResult == AttackResult.Unknown ? $"{{{fallbackDetails}}}" : ""));
                    if (!commandIssued || fallbackResult == AttackResult.Unknown)
                    {
                        LogRetry(retry, "uncertain;vanillaPreserved", 0, 0,
                            *(ushort*)(tribeBytes + TargetBuildingIdOffset),
                            *(uint*)(tribeBytes + TargetGlobalIdOffset));
                        finished = true; // Preserve the command outcome when the scratch result is uncertain.
                        break;
                    }
                    if (fallbackResult == AttackResult.AttackPoint)
                    {
                        LogRetry(retry, "selected", candidate.Id, candidate.GlobalId);
                        finished = true;
                        break;
                    }
                    RememberRejectedTarget(retry.Key, candidate.Id, candidate.GlobalId);
                    retry.LastFailedId = candidate.Id;
                    retry.LastFailedGlobalId = candidate.GlobalId;
                }
                if (finished) continue;
                if (retry.NextCandidate < retry.Candidates.Count)
                {
                    pendingRaidRetries[retry.Key] = retry;
                    continue;
                }
                if (*(ushort*)(tribeBytes + TargetBuildingIdOffset) == retry.LastFailedId &&
                    *(uint*)(tribeBytes + TargetGlobalIdOffset) == retry.LastFailedGlobalId)
                {
                    *(ushort*)(tribeBytes + TargetBuildingIdOffset) = 0;
                    *(uint*)(tribeBytes + TargetGlobalIdOffset) = 0;
                    LogRetry(retry, "noTarget;storedTargetCleared", 0, 0);
                }
                else LogRetry(retry, "noTarget;storedTargetChanged", 0, 0);
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
                retry.PlayerId < 1 || retry.PlayerId > MaxPlayers ||
                !GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(
                    retry.TargetPlayerId, out GamePlayerResources* resources) || resources == null)
                return false;
            if (!TryGetPriorityTable(out _, out byte* library))
            {
                reason = "nativeModuleUnavailable";
                return false;
            }
            byte* nativeIds = library + NativeRaidCandidateListRva +
                retry.TargetPlayerId * NativePlayerResourceStride;
            byte* nativeCount = library + NativeRaidCandidateCountRva +
                retry.TargetPlayerId * NativePlayerResourceStride;
            if ((byte*)resources + RaidCandidateListOffset != nativeIds ||
                (byte*)resources + RaidCandidateCountOffset != nativeCount)
            {
                reason = "extenderNativeCandidateAddressMismatch";
                return false;
            }
            int nativeTargetPlayer = *(int*)(library + NativeRaidTargetPlayerRva +
                retry.PlayerId * NativePlayerResourceStride);
            if (nativeTargetPlayer != retry.TargetPlayerId)
            {
                reason = $"nativeTargetPlayerMismatch:{nativeTargetPlayer}/{retry.TargetPlayerId}";
                return false;
            }
            int count = *(int*)nativeCount;
            if (count < 1 || count > NativeRaidCandidateCapacity)
            {
                reason = $"candidateCountContradictsLiveSelection:{count}";
                return false;
            }
            if (!IsLiveBuildingIdentity(retry.LastFailedId, retry.LastFailedGlobalId,
                    retry.TargetPlayerId))
            {
                reason = "selectedBuildingIdentityChanged";
                return false;
            }
            ushort* ids = (ushort*)nativeIds;
            bool selectedIdInList = false;
            for (int index = 0; index < count; index++)
                if (ids[index] == retry.LastFailedId) selectedIdInList = true;
            if (!selectedIdInList)
            {
                reason = $"selectedBuildingMissingFromCandidateList:{retry.LastFailedId}/{count}";
                return false;
            }
            int tableRva = retry.PriorityTableRva;
            int* priorities = (int*)(library + tableRva);
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
                        result.Add(new BuildingTarget(id, building->r_GlobalId,
                            (int)building->r_BuildingType));
                }
            }
            if (!seen.Contains(BuildingIdentity(retry.LastFailedId, retry.LastFailedGlobalId)))
            {
                result = null;
                reason = "selectedBuildingMissingFromPriorityOrder";
                return false;
            }
            reason = $"count={count},priorityTableRva=0x{tableRva:X}";
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
            if (!active || args.Phase != EventHookPhase.Post) return;
            try
            {
                if (!TryIdentifyRaidGroup(args.TribeId,
                        out int playerId, out int group, out uint tribeGlobalId)) return;
                var key = new RaidGroupKey(playerId, group, args.TribeId, tribeGlobalId);
                string destination = $"{args.TileX}/{args.TileY}";
                if (groupMoves.TryGetValue(key, out MoveSummary previous) &&
                    previous.Destination == destination)
                {
                    previous.Repeats++;
                    suppressedMessageCount++;
                    return;
                }
                if (previous != null) FlushMoveSummary(key, previous, "destinationChanged");
                groupMoves[key] = new MoveSummary(destination, lastTick);
                Info($"RAID_DIAG_MOVE_COMMAND: session={sessionId}, tick={lastTick}, " +
                    $"player={playerId}, role={group}, tribe={args.TribeId}/{tribeGlobalId}, " +
                    $"destination={destination}, moveType={args.MoveType}, return={args.ReturnValue}; " +
                    "scope=commandOnly.");
            }
            catch (Exception ex)
            {
                LogRepeated($"moveError:{ex.GetType().Name}:{ex.Message}",
                    $"RAID_DIAG_TRIBE_MOVE_ERROR: session={sessionId}, tick={lastTick}, error={ex}.", true);
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
                Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.N0000526D)).ToInt32() != RaidCandidateListOffset ||
                Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.N0000529F)).ToInt32() != RaidCandidateCountOffset)
                throw new InvalidOperationException("Installed Script Extender raid layout differs from audited native layout.");
        }

        private void LogRetry(RaidRetry retry, string outcome, int selectedId, uint selectedGlobalId,
            int storedId = 0, uint storedGlobalId = 0)
        {
            retryCount++;
            Info($"RAID_FIX_RETRY: session={sessionId}, startTick={retry.StartedTick}, endTick={lastTick}, " +
                $"elapsedTicks={lastTick - retry.StartedTick}, player={retry.PlayerId}, role={retry.Group}, " +
                $"tribe={retry.TribeId}/{retry.TribeGlobalId}, targetPlayer={retry.TargetPlayerId}, " +
                $"original={retry.OriginalFailedId}/{retry.OriginalFailedGlobalId}, " +
                $"candidateCount={retry.Candidates?.Count.ToString() ?? "unavailable"}, " +
                $"priorityTable=0x{retry.PriorityTableRva:X}, attempts=[{String.Join(";", retry.Attempts)}], " +
                $"selected={selectedId}/{selectedGlobalId}, " +
                (outcome.StartsWith("uncertain;", StringComparison.Ordinal)
                    ? $"storedTargetAtAbort={storedId}/{storedGlobalId}, " : "") +
                $"outcome={outcome}.");
        }

        private void FlushPendingRetries(string reason)
        {
            foreach (RaidRetry retry in pendingRaidRetries.Values)
                LogRetry(retry, reason, 0, 0);
            pendingRaidRetries.Clear();
        }

        private void FlushDamageSummary(ulong key, string reason)
        {
            if (!targetDamage.TryGetValue(key, out DamageSummary damage)) return;
            Info($"RAID_DIAG_DAMAGE_SUMMARY: session={sessionId}, firstTick={damage.FirstTick}, " +
                $"lastTick={damage.LastTick}, building={damage.BuildingId}/{damage.GlobalId}, " +
                $"{damage.Building}, raidGroupsAtFirst={damage.RaidGroups}, hits={damage.Hits}, " +
                $"damageTotal={damage.TotalDamage}, sources={String.Join(",", damage.Sources)}, reason={reason}.");
            targetDamage.Remove(key);
        }

        private void FlushDamageSummaries(string reason)
        {
            foreach (ulong key in new List<ulong>(targetDamage.Keys)) FlushDamageSummary(key, reason);
        }

        private void FlushMoveSummary(RaidGroupKey key, MoveSummary move, string reason)
        {
            if (move.Repeats == 0) return;
            Info($"RAID_DIAG_MOVE_REPEAT: session={sessionId}, tick={lastTick}, player={key.PlayerId}, " +
                $"role={key.Group}, tribe={key.TribeId}/{key.TribeGlobalId}, " +
                $"destination={move.Destination}, firstTick={move.FirstTick}, repeats={move.Repeats}, " +
                $"reason={reason}; scope=commandOnly.");
        }

        private void FlushMoveSummaries(string reason)
        {
            foreach (var entry in groupMoves) FlushMoveSummary(entry.Key, entry.Value, reason);
            groupMoves.Clear();
        }

        private void LogRepeated(string key, string message, bool warning)
        {
            if (!repeatedMessages.TryGetValue(key, out RepeatSummary repeat))
            {
                repeatedMessages.Add(key, new RepeatSummary(lastTick));
                if (warning) Warn(message); else Info(message);
            }
            else
            {
                repeat.Count++;
                repeat.LastTick = lastTick;
                suppressedMessageCount++;
            }
        }

        private void FlushRepeatSummaries()
        {
            foreach (var entry in repeatedMessages)
                if (entry.Value.Count != 0)
                    Info($"RAID_DIAG_REPEAT: session={sessionId}, firstTick={entry.Value.FirstTick}, " +
                        $"lastTick={entry.Value.LastTick}, key={entry.Key}, " +
                        $"suppressed={entry.Value.Count}.");
            repeatedMessages.Clear();
        }

        private void LogSummary(string reason)
        {
            int groups = 0, targets = 0;
            for (int player = 1; player <= MaxPlayers; player++)
                for (int role = 0; role < RaidGroupCount; role++)
                    if (previousGroups[player, role] != null && previousGroups[player, role] != "none")
                    {
                        groups++;
                        if (previousTargetIdentities[player, role] != null &&
                            previousTargetIdentities[player, role] != "0/0") targets++;
                    }
            Info($"RAID_DIAG_SUMMARY: session={sessionId}, tick={lastTick}, reason={reason}, " +
                $"activeGroups={groups}, assignedTargets={targets}, targetChanges={targetChangeCount}, " +
                $"attackCommands={attackCommandCount}, retries={retryCount}, " +
                $"pendingRetries={pendingRaidRetries.Count}, trackedDamage={targetDamage.Count}, " +
                $"suppressedRepeats={suppressedMessageCount}, emittedLines={emittedLineCount + 1}, " +
                $"payloadCharsBeforeSummary={emittedCharacterCount}.");
        }

        private void ClearObservations()
        {
            Array.Clear(previousGroups, 0, previousGroups.Length);
            Array.Clear(previousTargetIdentities, 0, previousTargetIdentities.Length);
            targetDamage.Clear();
            groupMoves.Clear();
            repeatedMessages.Clear();
            pendingDeletes.Clear();
            pendingAttackCandidates.Clear();
            pendingRaidRetries.Clear();
            rejectedRaidTargets.Clear();
            issuingFallback = false;
            fallbackTribeId = 0;
            fallbackBuildingId = 0;
            fallbackBuildingGlobalId = 0;
            fallbackResult = AttackResult.Unknown;
            fallbackDetails = null;
            targetChangeCount = 0;
            attackCommandCount = 0;
            retryCount = 0;
            suppressedMessageCount = 0;
            emittedLineCount = 0;
            emittedCharacterCount = 0;
        }

        private void Info(string value)
        {
            emittedLineCount++;
            emittedCharacterCount += value.Length;
            Shared.DebugLogHelper.LogInfo(log, value);
        }
        private void Warn(string value)
        {
            emittedLineCount++;
            emittedCharacterCount += value.Length;
            Shared.DebugLogHelper.LogWarning(log, value);
        }

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

        private readonly struct RaidGroupKey : IEquatable<RaidGroupKey>
        {
            internal readonly int PlayerId;
            internal readonly int Group;
            internal readonly int TribeId;
            internal readonly uint TribeGlobalId;

            internal RaidGroupKey(int playerId, int group, int tribeId, uint tribeGlobalId)
            {
                PlayerId = playerId;
                Group = group;
                TribeId = tribeId;
                TribeGlobalId = tribeGlobalId;
            }

            public bool Equals(RaidGroupKey other) =>
                PlayerId == other.PlayerId && Group == other.Group &&
                TribeId == other.TribeId && TribeGlobalId == other.TribeGlobalId;

            public override bool Equals(object obj) => obj is RaidGroupKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = PlayerId;
                    hash = (hash * 397) ^ Group;
                    hash = (hash * 397) ^ TribeId;
                    return (hash * 397) ^ (int)TribeGlobalId;
                }
            }
        }

        private sealed class RaidRetry
        {
            internal readonly int PlayerId;
            internal readonly int Group;
            internal readonly int TribeId;
            internal readonly uint TribeGlobalId;
            internal RaidGroupKey Key => new RaidGroupKey(PlayerId, Group, TribeId, TribeGlobalId);
            internal readonly int TargetPlayerId;
            internal readonly int PriorityTableRva;
            internal readonly int StartedTick;
            internal readonly int OriginalFailedId;
            internal readonly uint OriginalFailedGlobalId;
            internal readonly List<string> Attempts = new List<string>();
            internal int LastFailedId;
            internal uint LastFailedGlobalId;
            internal List<BuildingTarget> Candidates;
            internal int NextCandidate;

            internal RaidRetry(int playerId, int group, int tribeId, uint tribeGlobalId,
                int targetPlayerId, int failedId, uint failedGlobalId, int priorityTableRva,
                int startedTick)
            {
                PlayerId = playerId;
                Group = group;
                TribeId = tribeId;
                TribeGlobalId = tribeGlobalId;
                TargetPlayerId = targetPlayerId;
                PriorityTableRva = priorityTableRva;
                StartedTick = startedTick;
                OriginalFailedId = failedId;
                OriginalFailedGlobalId = failedGlobalId;
                LastFailedId = failedId;
                LastFailedGlobalId = failedGlobalId;
            }
        }

        private readonly struct BuildingTarget
        {
            internal readonly int Id;
            internal readonly uint GlobalId;
            internal readonly int Type;

            internal BuildingTarget(int id, uint globalId, int type)
            {
                Id = id;
                GlobalId = globalId;
                Type = type;
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

        private sealed class DamageSummary
        {
            internal readonly int BuildingId;
            internal readonly uint GlobalId;
            internal readonly string Building;
            internal readonly string RaidGroups;
            internal readonly int FirstTick;
            internal int LastTick;
            internal int Hits;
            internal long TotalDamage;
            internal readonly HashSet<int> Sources = new HashSet<int>();

            internal DamageSummary(int id, uint globalId, string building, string raidGroups, int tick)
            {
                BuildingId = id;
                GlobalId = globalId;
                Building = building;
                RaidGroups = raidGroups;
                FirstTick = tick;
                LastTick = tick;
            }

            internal void Add(int tick, int damage, int source)
            {
                LastTick = tick;
                Hits++;
                TotalDamage += damage;
                Sources.Add(source);
            }
        }

        private sealed class MoveSummary
        {
            internal readonly string Destination;
            internal readonly int FirstTick;
            internal int Repeats;

            internal MoveSummary(string destination, int tick)
            {
                Destination = destination;
                FirstTick = tick;
            }
        }

        private sealed class RepeatSummary
        {
            internal readonly int FirstTick;
            internal int LastTick;
            internal int Count;

            internal RepeatSummary(int tick)
            {
                FirstTick = tick;
                LastTick = tick;
            }
        }
    }
}
