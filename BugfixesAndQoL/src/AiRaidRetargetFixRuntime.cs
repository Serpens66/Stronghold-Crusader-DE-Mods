using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.API.LowLevel;
using R3;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using static BugfixesAndQoL.RaidAttackFieldEvaluation;

namespace BugfixesAndQoL
{
    internal sealed unsafe class AiRaidRetargetFixRuntime
    {
        private const int MaxPlayers = 8;
        private const int RaidGroupCount = 6;
        private const int RetargetCounterOffset = 0x2B78;
        private const int RemainingCounterOffset = 0x390C;
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
        private const int RejectionCleanupIntervalTicks = 200;

        private readonly ManualLogSource log;
        private readonly RaidActivationState activation = new RaidActivationState();
        private readonly Dictionary<int, Stack<AttackCandidateCapture>> pendingAttackCandidates = new Dictionary<int, Stack<AttackCandidateCapture>>();
        private readonly object attackCaptureLock = new object();
        private readonly Dictionary<RaidGroupKey, RaidRetry> pendingRaidRetries = new Dictionary<RaidGroupKey, RaidRetry>();
        private readonly Dictionary<RaidGroupKey, Dictionary<ulong, int>> rejectedRaidTargets = new Dictionary<RaidGroupKey, Dictionary<ulong, int>>();
        private readonly HashSet<string> warningReasons = new HashSet<string>();
        private IDisposable sessionSubscription, orderSubscription;
        private long sessionId, attackSequence, observationEpoch;
        private long nextRejectionCleanupTick;
        private bool initialized, sessionStarted, firstTickLogged, searchConfirmed, firstRetryLogged, issuingFallback;
        private volatile bool active;
        private int lastTick = -1, fallbackTribeId, fallbackBuildingId;
        private uint fallbackBuildingGlobalId;
        private AttackResult fallbackResult;
        private string fallbackDetails, fallbackSummary;
        private int attackCommandCount, validCount, negativeCount, unknownCount, retryCount, selectedCount, abortedCount, suppressedMessageCount;

        internal AiRaidRetargetFixRuntime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            ValidateLayout();
        }

        internal void Initialize(CrusaderLibraryLoadContext context, bool enabled)
        {
            lock (attackCaptureLock) activation.Requested = enabled;
            RaidSearchObserver.Install(context, log, OnSearchObserved);
            bool available;
            lock (attackCaptureLock)
            {
                activation.Available = RaidSearchObserver.IsAvailable;
                available = activation.Available;
            }
            if (!available) { Info("AI_RAID_READY: available=False; Vanilla preserved."); return; }
            IDisposable pendingSession = null, pendingOrder = null;
            bool pendingTick = false;
            try
            {
                pendingOrder = TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable.Subscribe(OnTribeOrder);
                pendingSession = Shared.GameplaySessionLifecycle.SubscribeStarted(log, OnSessionStarted, OnSessionEnded);
                GameTimeManagerAPI.Instance.OnTick += OnTick;
                pendingTick = true;
                CompleteInitialization(pendingOrder, pendingSession);
            }
            catch (System.Exception ex)
            {
                // Only event candidates not yet published by this runtime are rolled back.
                FailInitialization();
                if (pendingTick) GameTimeManagerAPI.Instance.OnTick -= OnTick;
                pendingSession?.Dispose();
                pendingOrder?.Dispose();
                Warn("AI_RAID_INACTIVE: event initialization failed; Vanilla preserved: " + ex);
            }
            Info($"AI_RAID_READY: available={activation.Available && initialized}, requested={enabled}, publisher=GameTimeManagerAPI.OnTick,roles=6,tribeTarget=0x622/0x626,candidates=resource+0x35BC/0x3684,bias=-0x5C.");
        }

        private void CompleteInitialization(IDisposable pendingOrder, IDisposable pendingSession)
        {
            lock (attackCaptureLock)
            {
                orderSubscription = pendingOrder;
                sessionSubscription = pendingSession;
                initialized = true;
                // A cached APIShared start may already have supplied the session.
                active = activation.Active;
            }
        }

        private void FailInitialization()
        {
            lock (attackCaptureLock)
            {
                initialized = false;
                activation.Available = false;
                active = false;
                ClearObservations();
            }
        }

        internal void SetEnabled(bool enabled)
        {
            lock (attackCaptureLock)
            {
                bool wasActive = active;
                activation.Requested = enabled;
                active = initialized && activation.Active;
                if (wasActive != active) ClearObservations();
            }
        }

        internal void OnSessionStarted(Shared.GameplaySessionStartedContext session)
        {
            lock (attackCaptureLock)
            {
                ClearObservations();
                sessionId = session.SessionId;
                sessionStarted = true;
                // IsReplay is cached lifecycle delivery, not a game replay; it must not disable this persistent feature.
                activation.SessionAllowed = !session.IsEditor;
                active = initialized && activation.Active;
                warningReasons.Clear();
                firstRetryLogged = false;
                attackCommandCount = validCount = negativeCount = unknownCount = retryCount = selectedCount = abortedCount = suppressedMessageCount = 0;
            }
        }

        internal void OnSessionEnded()
        {
            lock (attackCaptureLock)
            {
                if (sessionStarted) LogSummary("end");
                sessionStarted = false;
                activation.SessionAllowed = false;
                active = false;
                ClearObservations();
            }
        }

        internal void OnTick(int tick)
        {
            if (!active) return;
            lock (attackCaptureLock)
            {
                if (!active) return;
                try
                {
                    if (!firstTickLogged)
                    {
                        firstTickLogged = true;
                        Info($"AI_RAID_RUNTIME_CONFIRMED: session={sessionId}, tick={tick}, publisher=GameTimeManagerAPI.OnTick,roles=6,tribeTarget=0x622/0x626,candidates=resource+0x35BC/0x3684,bias=-0x5C.");
                    }
                    if (lastTick >= 0 && tick < lastTick) ClearObservations();
                    lastTick = tick;
                    PruneExpiredRejectedTargets();
                    PruneReplacedRaidGroups();
                    ProcessRaidRetries();
                }
                catch (Exception ex)
                {
                    LogRepeated("tickError:" + ex.GetType().Name, "AI_RAID_TICK_ERROR: " + ex, true);
                }
            }
        }
        internal void OnTribeOrder(TribeIssueOrderWithTargetEventArgs args)
        {
            if (!active) return;
            try
            {
                // All target commands form nesting barriers. Only building
                // attack commands are evaluated or emitted below.
                lock (attackCaptureLock)
                {
                    if (!active) return;
                    LogAttackCandidates(args);
                }
            }
            catch (Exception ex)
            {
                lock (attackCaptureLock)
                {
                    if (!active) return;
                    pendingAttackCandidates.Remove(Thread.CurrentThread.ManagedThreadId);
                    LogRepeated($"orderError:{ex.GetType().Name}",
                        $"AI_RAID_TRIBE_ORDER_ERROR: session={sessionId}, tick={lastTick}, error={ex}.", true);
                }
            }
        }
        private void LogAttackCandidates(TribeIssueOrderWithTargetEventArgs args)
        {
            var tribes = GameTribeManagerAPI.Instance;
            GameTribe* tribe = null;
            bool tribeAlive = args.TribeId > 0 && tribes.IsValidId(args.TribeId) &&
                tribes.TryGetTribeById(args.TribeId, out tribe) && tribe != null &&
                tribe->r_AliveState == AliveState.IsAlive;
            // An invalid ID still needs a neutral frame: its Post must not
            // consume an outer command. No candidate buffer is read for it.
            uint tribeGlobalId = tribeAlive ? tribe->r_GlobalId : 0;
            bool raid = TryIdentifyRaidGroup(args.TribeId,
                out int raidPlayer, out int raidGroup, out uint raidGlobalId) &&
                raidGlobalId == tribeGlobalId;
            if (!raid) { raidPlayer = tribeAlive ? tribe->r_PlayerIdOwner : 0; raidGroup = -1; }
            int threadId = Thread.CurrentThread.ManagedThreadId;
            bool buildingCommand = args.AICommand == TribeAICommand.AttackBuilding ||
                args.AICommand == TribeAICommand.ForceAttackBuilding;
            uint targetGlobalId = unchecked((uint)args.TargetValue2);
            AttackCandidateSnapshot snapshot = buildingCommand && tribeAlive ? ReadAttackCandidateSnapshot() :
                new AttackCandidateSnapshot(false, -1, Array.Empty<CandidateRecord>());
            AttackCandidateCapture pre = null;
            bool pairMatches = false;
            string contentComparison = "preCallUnproven";

            if (args.Phase == EventHookPhase.Pre)
            {
                PushCommandFrame(threadId, args, tribeGlobalId, snapshot,
                    buildingCommand && tribeAlive ? GamePathingManagerAPI.Instance.GetPathfindingContextView().Address : IntPtr.Zero,
                    raidPlayer, raidGroup);
            }
            else if (args.Phase == EventHookPhase.Post &&
                pendingAttackCandidates.TryGetValue(threadId,
                    out Stack<AttackCandidateCapture> postStack) && postStack.Count != 0)
            {
                pre = TakePostFrame(postStack);
                if (postStack.Count == 0) pendingAttackCandidates.Remove(threadId);
                if (pre == null) return;
                pairMatches = pre.Evidence.Session == sessionId && pre.Evidence.Epoch == observationEpoch &&
                    pre.Evidence.TribeId == args.TribeId && pre.TribeGlobalId == tribeGlobalId &&
                    pre.Command == args.AICommand && pre.BuildingId == args.TargetValue1 &&
                    pre.BuildingGlobalId == targetGlobalId && pre.Evidence.ThreadId == threadId &&
                    pre.Evidence.PlayerId == raidPlayer && pre.Evidence.RaidRole == raidGroup &&
                    (!buildingCommand || pre.Evidence.Context ==
                        GamePathingManagerAPI.Instance.GetPathfindingContextView().Address);
                contentComparison = pre.Snapshot.SameRecords(snapshot) ? "identical" : "changed";
            }
            else if (args.Phase == EventHookPhase.Post)
            {
                contentComparison = "missingPre";
            }

            // All commands retain their Pre/Post nesting barriers, but only raid
            // groups are classified or logged after their frame is consumed.
            if (args.Phase != EventHookPhase.Post || !buildingCommand || !raid) return;
            var tiles = GameTileManagerAPI.Instance;
            string freshness = pre == null ? "missingPre" : pre.Evidence.GetFreshness(pairMatches, snapshot);
            // Both original and replacement commands use this one classifier.
            // No observed search means no correction, even if scratch content changed.
            AttackResult result = Evaluate(snapshot, pairMatches, freshness, args.ReturnValue,
                targetGlobalId != 0 && ReadBuildingGlobalId(args.TargetValue1) == targetGlobalId,
                args.TargetValue1, tiles.GetStructureLayer().Length, GetBuildingAtTile, IsCardinalPair,
                out string validation);
            string searchDetails = pre == null
                ? "searchObserved=False,searchAssociation=missingPre,decision=[unavailable]"
                : pre.Evidence.Describe(pairMatches, snapshot);
            // TEMP_GATE_ROUTE_ACCEPTANCE: already associated decision, never changes classification.
            if (APIShared.TemporaryGateRouteAcceptanceBridge.Current != null)
            {
                CandidateRecord diagnosticFirst = snapshot.Records.Length == 0 ? default : snapshot.Records[0];
                APIShared.TemporaryGateRouteAcceptanceBridge.ReportRaid(raidPlayer, raidGroup, args.TribeId, tribeGlobalId, args.TargetValue1, targetGlobalId,
                issuingFallback ? "raid-replacement-command" : "raid-command", result.ToString(),
                "command=" + args.AICommand + ",searchSequence=" + pre?.Evidence.Sequence + ",freshness=" + freshness +
                ",validation=" + validation + ",return=" + args.ReturnValue +
                ",candidateApproach=" + diagnosticFirst.ApproachTile + ",candidateBuildingTile=" + diagnosticFirst.BuildingTile +
                ",candidateIsNotRoute=true," + searchDetails);
            }
            if (raid && issuingFallback)
            {
                if (args.TribeId == fallbackTribeId &&
                    args.TargetValue1 == fallbackBuildingId &&
                    targetGlobalId == fallbackBuildingGlobalId)
                {
                    fallbackResult = result;
                    CandidateRecord first = snapshot.Records.Length == 0 ? default : snapshot.Records[0];
                    fallbackSummary = $"search={freshness},validation={validation},sequence={pre?.Evidence.Sequence}," +
                        $"content={contentComparison},first={first.ApproachTile}/{first.BuildingTile}/{first.Score}";
                    fallbackDetails = $"freshness={freshness},contentComparison={contentComparison},prePostMatch={pairMatches}," +
                        $"validation={validation},return={args.ReturnValue},{snapshot.DescribeCompact()}," +
                        $"{searchDetails},result={result}";
                }
                return;
            }
            attackCommandCount++;
            if (result == AttackResult.AttackPoint) validCount++;
            else if (result == AttackResult.NoAttackPoint) negativeCount++;
            else unknownCount++;
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
                    $"AI_RAID_UNCERTAIN: session={sessionId}, tick={lastTick}, " +
                    $"tribe={args.TribeId}/{tribeGlobalId}, target={args.TargetValue1}/{targetGlobalId}, " +
                    $"freshness={freshness}, validation={validation}; vanillaPreserved=true.", false);
            else if (result == AttackResult.NoAttackPoint)
                LogRepeated($"uncertain:{eligibilityReason}:{raidPlayer}/{raidGroup}",
                    $"AI_RAID_UNCERTAIN: session={sessionId}, tick={lastTick}, " +
                    $"tribe={args.TribeId}/{tribeGlobalId}, target={args.TargetValue1}/{targetGlobalId}, " +
                    $"reason={(eligible ? "priorityOrBuildingUnavailable" : eligibilityReason)}; vanillaPreserved=true.", true);
        }
        internal void OnSearchObserved(int tribeId, int buildingId, IntPtr context)
        {
            if (!active) return;
            lock (attackCaptureLock)
            {
                if (!active) return;
                int threadId = Thread.CurrentThread.ManagedThreadId;
                if (!pendingAttackCandidates.TryGetValue(threadId, out Stack<AttackCandidateCapture> stack) ||
                    PeekSearchFrame(stack) == null)
                {
                    LogRepeated("searchUnscoped", $"AI_RAID_SEARCH_UNSCOPED: session={sessionId}," +
                        $"tick={lastTick},thread={threadId},tribe={tribeId},target={buildingId};" +
                        "association=missingActiveCommand.", true);
                    return;
                }
                AttackCandidateCapture capture = stack.Peek();
                if (capture.Command != TribeAICommand.AttackBuilding &&
                    capture.Command != TribeAICommand.ForceAttackBuilding)
                {
                    return;
                }
                uint tribeGlobalId = 0;
                int playerId = 0, raidRole = -1;
                var tribes = GameTribeManagerAPI.Instance;
                if (tribes.IsValidId(tribeId) && tribes.TryGetTribeById(tribeId, out GameTribe* tribe) &&
                    tribe != null && tribe->r_AliveState == AliveState.IsAlive)
                {
                    tribeGlobalId = tribe->r_GlobalId;
                    playerId = tribe->r_PlayerIdOwner;
                    if (TryIdentifyRaidGroup(tribeId, out int raidPlayer, out int role, out uint global) &&
                        global == tribeGlobalId) { playerId = raidPlayer; raidRole = role; }
                }
                capture.Evidence.Observe(sessionId, observationEpoch, threadId, tribeId, tribeGlobalId,
                    buildingId, ReadBuildingGlobalId(buildingId) ?? 0, context,
                    Read(context, AttackCandidateListOffset), playerId, raidRole);
                if (!searchConfirmed && capture.Evidence.Association == "matchedNativeConsumerReturn")
                {
                    searchConfirmed = true;
                    Info($"AI_RAID_SEARCH_CONFIRMED: session={sessionId},tick={lastTick}," +
                        $"sequence={capture.Evidence.Sequence},thread={threadId},tribe={tribeId}/{tribeGlobalId}," +
                        $"target={buildingId}/{capture.BuildingGlobalId},rva=0x11FFA7;observer=readOnly,classification=authoritative.");
                }
            }
        }
        private void PushCommandFrame(int threadId, TribeIssueOrderWithTargetEventArgs args,
            uint tribeGlobalId, AttackCandidateSnapshot snapshot, IntPtr context,
            int raidPlayer, int raidGroup)
        {
            uint targetGlobalId = unchecked((uint)args.TargetValue2);
            if (!pendingAttackCandidates.TryGetValue(threadId,
                    out Stack<AttackCandidateCapture> stack))
            {
                stack = new Stack<AttackCandidateCapture>();
                pendingAttackCandidates.Add(threadId, stack);
            }
            if (stack.Count != 0 && stack.Peek().Tick != lastTick)
            {
                LogRepeated($"unpaired:{args.TribeId}/{tribeGlobalId}",
                    $"AI_RAID_ATTACK_UNPAIRED: session={sessionId}, tick={lastTick}, " +
                    $"tribe={args.TribeId}/{tribeGlobalId}, discardedPreCalls={stack.Count}.", true);
                stack.Clear();
            }
            stack.Push(new AttackCandidateCapture(args, lastTick, tribeGlobalId,
                args.AICommand, args.TargetValue1, targetGlobalId, snapshot,
                new RaidSearchEvidence(sessionId, observationEpoch,
                    Interlocked.Increment(ref attackSequence), threadId, args.TribeId,
                    tribeGlobalId, args.TargetValue1, targetGlobalId,
                    context,
                    raidPlayer, raidGroup)));
        }

        // The Extender emits no Post for skipped calls. Do not prune at Pre:
        // an outer subscriber may still change its skip flag afterwards.
        private static AttackCandidateCapture PeekSearchFrame(Stack<AttackCandidateCapture> stack)
        {
            while (stack.Count != 0 && stack.Peek().PreEvent.SkipOriginalFunction) stack.Pop();
            return stack.Count != 0 ? stack.Peek() : null;
        }

        private static AttackCandidateCapture TakePostFrame(Stack<AttackCandidateCapture> stack)
        {
            return PeekSearchFrame(stack) != null ? stack.Pop() : null;
        }

        private void PruneExpiredRejectedTargets()
        {
            if (lastTick < nextRejectionCleanupTick) return;
            nextRejectionCleanupTick = (long)lastTick + RejectionCleanupIntervalTicks;
            foreach (RaidGroupKey key in new List<RaidGroupKey>(rejectedRaidTargets.Keys))
            {
                Dictionary<ulong, int> rejected = rejectedRaidTargets[key];
                var expired = new List<ulong>();
                foreach (KeyValuePair<ulong, int> target in rejected)
                    if (target.Value <= lastTick) expired.Add(target.Key);
                foreach (ulong identity in expired) rejected.Remove(identity);
                if (rejected.Count == 0) rejectedRaidTargets.Remove(key);
            }
        }

        private void PruneReplacedRaidGroups()
        {
            var keys = new HashSet<RaidGroupKey>(rejectedRaidTargets.Keys);
            keys.UnionWith(pendingRaidRetries.Keys);
            foreach (RaidGroupKey key in keys)
            {
                if (IsCurrentRaidGroup(key)) continue;
                rejectedRaidTargets.Remove(key);
                if (pendingRaidRetries.TryGetValue(key, out RaidRetry replaced))
                {
                    LogRetry(replaced, "groupReplaced", 0, 0);
                    pendingRaidRetries.Remove(key);
                }
            }
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
                    fallbackSummary = null;
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
                        (fallbackResult == AttackResult.Unknown ? $"{{{fallbackDetails}}}" : $"{{{fallbackSummary}}}"));
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

        private static uint? ReadBuildingGlobalId(int buildingId)
        {
            var buildings = GameBuildingManagerAPI.Instance;
            if (!buildings.IsValidId(buildingId) ||
                !buildings.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                building == null || building->r_AliveState != AliveState.IsAlive)
                return null;
            return building->r_GlobalId;
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
                Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.r_BuildingIds)).ToInt32() != RaidCandidateListOffset ||
                Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.r_TrackedBuildingCount)).ToInt32() != RaidCandidateCountOffset)
                throw new InvalidOperationException("Installed Script Extender raid layout differs from audited native layout.");
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, EntryPoint = "GetModuleHandleW")]
        private static extern IntPtr GetModuleHandle(string moduleName);
        private void LogRetry(RaidRetry retry, string outcome, int selectedId, uint selectedGlobalId,
            int storedId = 0, uint storedGlobalId = 0)
        {
            // TEMP_GATE_ROUTE_ACCEPTANCE: counts every completion, independent of warning suppression.
            if (APIShared.TemporaryGateRouteAcceptanceBridge.Current != null)
                APIShared.TemporaryGateRouteAcceptanceBridge.ReportRaid(retry.PlayerId, retry.Group, retry.TribeId, retry.TribeGlobalId,
                selectedId, selectedGlobalId, "raid-retarget", outcome.Split(':')[0].Split(';')[0],
                "original=" + retry.OriginalFailedId + "/" + retry.OriginalFailedGlobalId + ",attempts=" + retry.Attempts.Count + ",reason=" + outcome);
            retryCount++;
            if (outcome == "selected")
            {
                selectedCount++;
                if (!firstRetryLogged)
                {
                    firstRetryLogged = true;
                    Info($"AI_RAID_RETRY_CONFIRMED: session={sessionId}, tick={lastTick}, player={retry.PlayerId}, role={retry.Group}, tribe={retry.TribeId}/{retry.TribeGlobalId}, " +
                        $"original={retry.OriginalFailedId}/{retry.OriginalFailedGlobalId}, attempts=[{String.Join(";", retry.Attempts)}], selected={selectedId}/{selectedGlobalId}.");
                }
            }
            else
            {
                abortedCount++;
                if (outcome.StartsWith("candidateListUncertain", StringComparison.Ordinal) || outcome.StartsWith("uncertain;", StringComparison.Ordinal))
                {
                    string reasonKey = outcome.StartsWith("candidateListUncertain:", StringComparison.Ordinal)
                        ? "candidateList:" + outcome.Split(':')[1].Split(';')[0]
                        : "retry:" + DetailReason(fallbackDetails, "freshness=") + ":" + DetailReason(fallbackDetails, "validation=");
                    LogRepeated(reasonKey,
                        $"AI_RAID_RETRY_ABORT: session={sessionId}, tick={lastTick}, player={retry.PlayerId}, role={retry.Group}, tribe={retry.TribeId}/{retry.TribeGlobalId}, " +
                        $"original={retry.OriginalFailedId}/{retry.OriginalFailedGlobalId}, stored={storedId}/{storedGlobalId}, attempts=[{String.Join(";", retry.Attempts)}], reason={outcome}.", true);
                }
            }
        }

        private static string DetailReason(string details, string prefix)
        {
            foreach (string part in (details ?? "").Split(','))
                if (part.StartsWith(prefix, StringComparison.Ordinal)) return part.Substring(prefix.Length);
            return "unavailable";
        }

        private void LogRepeated(string key, string message, bool warning)
        {
            // Once per reason/session, rather than per changing group or target ID.
            string[] parts = key.Split(':');
            key = parts[0] + (parts.Length > 1 ? ":" + parts[1] : "") + (parts.Length > 2 ? ":" + parts[2] : "");
            if (!warningReasons.Add(key)) { suppressedMessageCount++; return; }
            Warn(message);
        }

        private void LogSummary(string reason)
        {
            Info($"AI_RAID_SUMMARY: session={sessionId}, tick={lastTick}, reason={reason}, attacks={attackCommandCount}, valid={validCount}, negative={negativeCount}, unknown={unknownCount}, " +
                $"retries={retryCount}, selected={selectedCount}, aborted={abortedCount}, pending={pendingRaidRetries.Count}, suppressedWarnings={suppressedMessageCount}.");
        }

        private void ClearObservations()
        {
            // Caller owns the command lock. No published hook or subscription is removed.
            pendingAttackCandidates.Clear();
            observationEpoch++;
            pendingRaidRetries.Clear();
            rejectedRaidTargets.Clear();
            issuingFallback = false;
            fallbackTribeId = fallbackBuildingId = 0;
            fallbackBuildingGlobalId = 0;
            fallbackResult = AttackResult.Unknown;
            fallbackDetails = fallbackSummary = null;
            lastTick = -1;
            nextRejectionCleanupTick = 0;
        }

        private void Info(string value) { try { Shared.DebugLogHelper.LogInfo(log, value); } catch { } }
        private void Warn(string value) { try { Shared.DebugLogHelper.LogWarning(log, value); } catch { } }
        private sealed class AttackCandidateCapture
        {
            internal readonly TribeIssueOrderWithTargetEventArgs PreEvent;
            internal readonly int Tick;
            internal readonly uint TribeGlobalId;
            internal readonly TribeAICommand Command;
            internal readonly int BuildingId;
            internal readonly uint BuildingGlobalId;
            internal readonly AttackCandidateSnapshot Snapshot;
            internal readonly RaidSearchEvidence Evidence;

            internal AttackCandidateCapture(TribeIssueOrderWithTargetEventArgs preEvent,
                int tick, uint tribeGlobalId, TribeAICommand command,
                int buildingId, uint buildingGlobalId, AttackCandidateSnapshot snapshot, RaidSearchEvidence evidence)
            {
                PreEvent = preEvent;
                Tick = tick;
                TribeGlobalId = tribeGlobalId;
                Command = command;
                BuildingId = buildingId;
                BuildingGlobalId = buildingGlobalId;
                Snapshot = snapshot;
                Evidence = evidence;
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
    }
}
