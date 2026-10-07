using APIShared;
using BepInEx.Logging;
using EnemyGatePathfindingTest;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace EnemyBridgePathTest
{
    // Every frame and unit-state capture exists only between Pre and Post. Snapshots
    // contain buildings/tiles. The separate route observer retains validated unit identities.
    internal sealed unsafe class BridgeDiagnostics : IEnemyBridgePathObserver
    {
        private readonly ManualLogSource log;
        internal readonly BridgeDecisionTrace Trace;
        private BridgeNativeHooks nativeHooks;
        private int nativeInitializationAttempted;
        private long boundaryPosts;
        private int unresolvedSamples;
        private readonly AiGateDecisionAggregate totals = new AiGateDecisionAggregate();
        private BridgeSnapshot snapshot = BridgeSnapshot.Empty;
        private BridgeSnapshot CurrentSnapshot => Volatile.Read(ref snapshot);
        private volatile bool running;
        private bool marker, nativeVerified;
        private readonly Shared.DeferredSnapshotRefreshRequest captureRefresh = new Shared.DeferredSnapshotRefreshRequest();
        private long epoch, nextSnapshot, nextFlush, errors, preCount, postCount, observedSearches, suppressedCount, missingPostCount;
        [ThreadStatic] private static List<Frame> frames;
        private sealed class Frame
        {
            internal EventHookBase PreEvent;
            internal long Epoch, TraceId, ParentTrace;
            internal string Kind, Identity, UnitType = "unknown";
            internal int Id, Tribe, Player, Command, X, Y, SourcePcl, TargetPcl;
            internal long Regions, Searches, Failed, WorkMoves;
            internal uint Global;
            internal int StartX=-1,StartY=-1,RawUnitType;
            internal bool Detailed, RouteBound;
            internal Dictionary<int, UnitState> WorkBefore;
        }
        private readonly struct UnitState
        {
            internal readonly uint Global;
            internal readonly ushort Command, X, Y;
            internal readonly uint Task;
            internal UnitState(GameUnit* value)
            { Global = value->r_GlobalId; Command = value->r_AI_LastIssuedTribeCommand;
              X = value->r_ContextTargetTileX; Y = value->r_ContextTargetTileY; Task = value->r_MoatWorkTaskIndex; }
            public override string ToString() => Global + ":" + Command + ":" + X + "/" + Y + ":task=" + Task;
        }
        private sealed class Search
        {
            internal Frame Frame;
            internal string Source;
            internal int RawPlayer;
        }
        private sealed class Assassin
        {
            internal Frame Frame;
            internal BridgeSnapshot Snapshot;
            internal int X, Y;
            internal readonly Dictionary<string, long> Edges = new Dictionary<string, long>();
        }
        internal BridgeDiagnostics(ManualLogSource log) { this.log = log; Trace = new BridgeDecisionTrace(log); Trace.PromoteCommandChain=PromoteRouteFrames; }
        internal void InitializeNative(SHCDESE.API.LowLevel.CrusaderLibraryLoadContext context)
        {
            if(Interlocked.Exchange(ref nativeInitializationAttempted,1)!=0) return;
            string assemblyPath=typeof(BridgeDiagnostics).Assembly.Location;
            using(var sha=System.Security.Cryptography.SHA256.Create())
            using(var file=System.IO.File.OpenRead(assemblyPath))
                Shared.DebugLogHelper.LogInfo(log,"bridge loaded assembly: path="+assemblyPath+",sha256="+BitConverter.ToString(sha.ComputeHash(file)).Replace("-", ""));
            VerifyNative();
            if(!nativeVerified) return;
            // Passive data coverage remains bounded even when entry-hook preparation fails.
            Trace.SetNative(context.ModuleHandle,context.Memory.Length);
            nativeHooks = new BridgeNativeHooks(Trace);
            try { nativeHooks.Install(context,log); }
            catch(Exception error) { Trace.MarkNativeUnavailable(error.GetType().Name+":"+error.Message);Shared.DebugLogHelper.LogError(log,"Bridge native diagnosis unavailable; existing passive coverage only: "+error); }
            GameTimeManagerAPI.Instance.OnTick += Trace.Tick;
        }
        internal void VerifyNative() => nativeVerified = Shared.DebugLogHelper.CurrentNativeSha256 == BridgeNativeDefinition.NativeHash &&
            Shared.DebugLogHelper.ReportNativeLibraryVersion(log,
            "EnemyBridgePathTest", requireCurrentVersion: true);
        internal void BeginMap(Shared.GameplaySessionStartedContext context)
        {
            if (!nativeVerified)
            { Shared.DebugLogHelper.LogError(log, "Bridge diagnosis inactive: native layout hash not confirmed."); return; }
            if (running) End();
            totals.Reset(); captureRefresh.Reset(); epoch++; running = true; marker = false; Trace.Begin(context);
            preCount = postCount = errors = observedSearches = 0;
            suppressedCount = missingPostCount = 0;
            nextSnapshot = nextFlush = 0; Volatile.Write(ref snapshot, BridgeSnapshot.Empty);
            Shared.DebugLogHelper.LogInfo(log, "bridge map start epoch=" + epoch + ",editor=" + context.IsEditor);
        }
        internal void End()
        {
            if (!running) return;
            Flush(); running = false; captureRefresh.Reset(); Trace.End(); Volatile.Write(ref snapshot, BridgeSnapshot.Empty);
        }
        internal void BuildingCapture(BuildingCaptureEventArgs args)
        {
            if (args != null && args.BuildingId > 0)
                {
                if(running&&args.Phase==EventHookPhase.Post)Trace.VirtualShadow?.Invalidate();
                captureRefresh.Request(running, args.Phase == EventHookPhase.Post);
            }
        }
        internal void Deferred()
        {
            Trace.VirtualShadow?.Pump();Trace.VirtualShadow?.PumpArtifacts();Trace.Drain();
            if (!running || Trace.InsideNative) return;
            // Rendering is outside synchronous native dispatch. No command frame
            // may survive here, including suppressed calls without a Post event.
            RetireFrames();
            if (!marker)
            {
                marker = true;
                Trace.Observe("native-readiness",Trace.NativeReadiness);
                Shared.DebugLogHelper.LogInfo(log, "bridge runtime confirmed after startup cleanup, epoch=" + epoch +
                    "; region/builder coverage requires mainmod movement hooks; Assassin coverage requires its existing hook. " +
                    "Missing callbacks are missing coverage, never negative reachability evidence.");
            }
            long now = Stopwatch.GetTimestamp();
            if (captureRefresh.Consume())
            {
                Trace.InvalidateBuildings();
                nextSnapshot = 0;
                Shared.DebugLogHelper.LogInfo(log, "bridge capture Post refresh deferred, epoch=" + epoch);
            }
            if (now >= nextSnapshot)
            {
                nextSnapshot = now + Stopwatch.Frequency;
                Guard("snapshot", () =>
                {
                    var captured = BridgeSnapshot.Capture();
                    if (!captured.SameState(CurrentSnapshot)) Volatile.Write(ref snapshot, captured);
                });
            }
            if (now >= nextFlush) { nextFlush = now + Stopwatch.Frequency * 10; Flush(); }
        }
        private void RetireFrames()
        {
            Guard("unfinished-events", () =>
            {
                PruneSkipped();
                while (frames != null && frames.Count > 0)
                {
                    var unfinished = frames[frames.Count - 1];
                    frames.RemoveAt(frames.Count - 1);
                    if(unfinished.Epoch!=epoch) { boundaryPosts++; continue; }
                    Interlocked.Increment(ref errors);
                    missingPostCount++;
                    Record(unfinished, "diagnostic-error", "missing-post", unfinished.Identity);
                }
            });
        }
        private void Flush()
        {
            Trace.FlushRegions(); Trace.FlushCosts(); unresolvedSamples=0;
            var rows = totals.Drain(out var definitions);
            foreach (var definition in definitions) Trace.Observe("aggregate-state",definition.ToString());
            Trace.Aggregates(rows);
            Shared.DebugLogHelper.LogInfo(log, "bridge summary epoch=" + epoch + ",observations=" + totals.Observations +
                ",pre=" + preCount + ",post=" + postCount + ",searches=" + observedSearches + ",errors=" + errors +
                ",suppressed=" + suppressedCount + ",missingPost=" + missingPostCount +
                ",adapterFailures=" + EnemyBridgeDiagnosticBridge.FailureCount +
                ",lastAdapterFailure=" + (EnemyBridgeDiagnosticBridge.LastFailure ?? "none") +
                ",boundaryPosts="+boundaryPosts+",decisionTrace=["+Trace.Summary()+"],pendingCalls=" + (frames?.Count ?? 0) + ",bridgeCount=" + CurrentSnapshot.Bridges.Length);
        }
        private void Guard(string source, Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                Interlocked.Increment(ref errors);
                totals.Record(0, 0, "diagnostic-error", source + ":" + ex.GetType().Name, 0, 0, 0, 0, ex.Message);
            }
        }
        private void PruneSkipped()
        {
            while (frames != null && frames.Count > 0 && frames[frames.Count - 1].PreEvent?.SkipOriginalFunction == true)
            {
                var skipped = frames[frames.Count - 1];
                frames.RemoveAt(frames.Count - 1);
                if(skipped.Epoch!=epoch) { boundaryPosts++; continue; }
                suppressedCount++;
                Record(skipped, "event-suppressed", "original-skipped-no-post", skipped.Identity);
            }
        }
        private Frame Current
        {
            get { PruneSkipped(); return frames != null && frames.Count > 0 ? frames[frames.Count - 1] : null; }
        }
        private Frame SessionFrame { get {var value=Current;return value!=null&&value.Epoch==epoch?value:null;} }
        private Frame WorkParent(int tribe)
        {
            PruneSkipped();
            if (frames == null) return null;
            for (int i = frames.Count - 1; i >= 0; i--)
                if (frames[i].Epoch==epoch && frames[i].Kind == "target" && frames[i].Tribe == tribe && IsWork(frames[i].Command) &&
                    frames[i].PreEvent is TribeIssueOrderWithTargetEventArgs target &&
                    target.TribeId == tribe && (int)target.AICommand == frames[i].Command) return frames[i];
            return null;
        }
        private static bool IsWork(int command) => command == (int)TribeAICommand.DigMoatTileId || command == (int)TribeAICommand.Unknown7;
        private void Record(Frame frame, string stage, string result, string detail = null, long count = 0)
        {
            if(frame!=null&&!frame.Detailed&&stage!="diagnostic-error"&&stage!="event-suppressed") {Trace.CountBackground("summarized-frame-observation");return;}
            totals.Record(frame?.Player ?? 0, 0, stage, result + ",unitType=" + (frame?.UnitType ?? "unknown"), frame?.Command ?? 0, frame?.Tribe ?? 0,
                frame?.X ?? 0, frame?.Y ?? 0, detail, count);
        }
        private Frame TribeFrame(string kind, int tribeId, int command, int x, int y)
        {
            var frame = new Frame { Kind = kind, Id = tribeId, Tribe = tribeId, Command = command, X = x, Y = y,
                SourcePcl = -1, TargetPcl = -1, Identity = "tribe-unavailable" };
            if (GameTribeManagerAPI.Instance.IsValidId(tribeId) && GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) && tribe != null)
            {
                frame.Player = tribe->r_PlayerIdOwner; frame.Global=tribe->r_GlobalId;
                frame.Identity = "tribe=" + tribeId + "/g" + tribe->r_GlobalId + ",leader=" + tribe->r_LeaderUnitId;
                if (GameUnitManagerAPI.Instance.IsValidId(tribe->r_LeaderUnitId) && GameUnitManagerAPI.Instance.TryGetUnitById(tribe->r_LeaderUnitId, out GameUnit* leader) && leader != null)
                {
                    frame.StartX=leader->r_CurrentTilePositionX;frame.StartY=leader->r_CurrentTilePositionY;
                    frame.SourcePcl = Pcl(frame.StartX,frame.StartY);
                    frame.UnitType = leader->r_UnitChimp.ToString();
                    frame.Identity += "/g" + leader->r_GlobalId + ",type=" + leader->r_UnitChimp +
                        ",controlWord=" + (leader->r_ControllableForPlayerId | ((int)leader->N00000569 << 8)) +
                        ",start=" + leader->r_CurrentTilePositionX + "/" + leader->r_CurrentTilePositionY;
                }
            }
            if (kind == "move" || IsWork(command)) frame.TargetPcl = Pcl(x, y);
            return frame;
        }
        internal static int Pcl(int x, int y)
        {
            // GetTileId has no coordinate validation. Validate before its row lookup.
            if ((uint)x >= 800 || (uint)y >= 800) return -1;
            var api = GameTileManagerAPI.Instance;
            int tile = api.GetTileId(x, y);
            var pcl = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            return (uint)tile < (uint)pcl.Length ? pcl[tile] : -1;
        }
        private void Push(Frame frame)
        {
            if (frames == null) frames = new List<Frame>();
            frame.Epoch = running ? epoch : -1;
            frame.ParentTrace = frames.Count>0&&frames[frames.Count-1].Epoch==frame.Epoch ? frames[frames.Count-1].TraceId : 0;
            frame.TraceId = Trace.NewOperation(); frames.Add(frame);
            if(!running) return;
            Interlocked.Increment(ref preCount);Trace.CountCommand(true);
            if(frame.Kind=="unit")Trace.TaskCommand(frame.Id,frame.Global,frame.Tribe,frame.Player,frame.Command,frame.TraceId);
            int kind=frame.Kind=="target"?1:frame.Kind=="move"?2:3;
            bool resolved=frame.Global!=0&&frame.Player>=1&&frame.Player<=8;
            bool changed=Trace.CommandIsNew(kind,frame.Id,frame.Global,new BridgeDecisionTrace.Arguments(frame.Tribe,frame.Player,frame.Command,frame.X,frame.Y,0));
            bool near=Trace.NearBridge(frame.X,frame.Y)||Trace.NearBridge(frame.StartX,frame.StartY);
            frame.Detailed=Trace.ShouldDetailCommand(frame.Kind!="unit",near,changed);
            Trace.CountEvent(BridgeDecisionTrace.CommandCountData(kind,frame.Player,frame.Command,0,true,resolved));
            if(!resolved&&!frame.Detailed&&unresolvedSamples<8)
            {
                unresolvedSamples++;
                Trace.Observe("unresolved-command","op="+frame.TraceId+",commandKind="+kind+",id="+frame.Id+",global="+frame.Global+",playerRaw="+frame.Player+",commandRaw="+frame.Command+",input="+frame.X+"/"+frame.Y+",sampleLimitPerInterval=8,classification=unknown");
            }
            if(frame.Kind=="unit") frame.RouteBound=Trace.BindRoute(frame.Id,frame.Global,frame.Player,frame.Tribe,frame.TraceId,frame.ParentTrace,changed);
            if(!frame.Detailed)return;
            if(frame.Kind=="unit"&&frame.Global!=0)
            {
                frame.UnitType=((eChimps)frame.RawUnitType).ToString();
                frame.Identity="unit="+frame.Id+"/g"+frame.Global+",type="+frame.UnitType+",controlWord="+frame.Player+",start="+frame.StartX+"/"+frame.StartY;
            }
            if(frame.Kind=="target"&&IsWork(frame.Command))frame.WorkBefore=CaptureWork(frame.Tribe);
            Trace.Command("command-pre","op="+frame.TraceId+",parentEvent="+frame.ParentTrace+",commandKind="+frame.Kind+
                ",id="+frame.Id+",tribe="+frame.Tribe+",player="+frame.Player+",command="+frame.Command+
                ",input="+frame.X+"/"+frame.Y+",pcl="+frame.SourcePcl+"->"+frame.TargetPcl+",nearBridge="+near+",contextAttribution="+(Trace.DetailedNative?"native-decision":near?"bridge-proximity":"unscoped-bounded-example")+",routeAttribution=unproven,"+frame.Identity,frame.Player);
            if(IsWork(frame.Command)) Trace.Observe("terrain-work-command","eventOp="+frame.TraceId+",player="+frame.Player+",command="+frame.Command+",target="+frame.X+"/"+frame.Y+",nearBridge="+near+",execution=not-proven");
            Record(frame,"command-pre","called",frame.Identity);

        }
        private void PromoteRouteFrames(long unitOperation)
        {
            if(frames==null)return;
            // Unit and group frames are retained through the passive Post capture.
            // Emit frozen Pre fields only, never fabricate an entry capture.
            foreach(var frame in frames)
            {
                if(frame.Epoch==epoch&&frame.Kind!="unit")Trace.VirtualShadow?.BridgeGroup(frame.TraceId,frame.Player);
                if(frame.Epoch==epoch&&!frame.Detailed)
                {
                    frame.Detailed=true;
                    Trace.Command("command-pre","op="+frame.TraceId+",parentEvent="+frame.ParentTrace+",commandKind="+frame.Kind+",id="+frame.Id+",global="+frame.Global+",tribe="+frame.Tribe+",player="+frame.Player+",command="+frame.Command+",input="+frame.X+"/"+frame.Y+",start="+frame.StartX+"/"+frame.StartY+",pcl="+frame.SourcePcl+"->"+frame.TargetPcl+",promotion=bridge-route-observed,entryData=retained-pre-fields,liveStateTiming=promotion,followingUnitOp="+unitOperation,frame.Player);
                }
            }
        }
        private void Pop(string kind, int id, long result)
        {
            var frame = Current;
            if(frame!=null && frame.Kind==kind && frame.Id==id && (frame.Epoch!=epoch || !running))
            {
                frames.RemoveAt(frames.Count-1); boundaryPosts++;
                Trace.Observe("event-boundary","op="+frame.TraceId+",preEpoch="+frame.Epoch+",postEpoch="+epoch+",commandKind="+kind+",id="+id);
                return;
            }
            if(!running) return;
            Interlocked.Increment(ref postCount);Trace.CountCommand(false);
            if (frame == null || frame.Kind != kind || frame.Id != id)
            { Interlocked.Increment(ref errors); Record(frame, "diagnostic-error", "pre-post-mismatch", "post=" + kind + "/" + id); return; }
            if(kind=="unit")Trace.Routes.Observe(id,true,true,result,frame.TraceId,frame.RouteBound);
            frames.RemoveAt(frames.Count - 1);
            Trace.CountEvent(BridgeDecisionTrace.CommandCountData(kind=="target"?1:kind=="move"?2:3,frame.Player,frame.Command,result,false,frame.Global!=0&&frame.Player>=1&&frame.Player<=8));
            string stage = kind == "move" && frame.SourcePcl > 0 && frame.SourcePcl == frame.TargetPcl && frame.Regions == 0
                ? "same-pcl-with-no-region-call" : frame.Regions > 0 ? "region-query-executed" : "region-not-observed";
            if(frame.Detailed) Trace.Command("command-post","op="+frame.TraceId+",parentEvent="+frame.ParentTrace+",commandKind="+kind+
                ",id="+id+",return="+result+",retainedPre="+DescribePreArgs(frame.PreEvent)+",postInput=original,regions="+frame.Regions+",stage="+stage,frame.Player);
            // No region call for same PCL is distinct from an observed positive E2610.
            if(frame.Detailed) Record(frame, kind + "-post", "return=" + result + ",stage=" + stage,
                frame.Identity + ",regions=" + frame.Regions + ",searches=" + frame.Searches + ",failed=" + frame.Failed +
                ",retainedPreArgs=" + DescribePreArgs(frame.PreEvent) + ",postArgsSource=extender-original-inputs");
            if (frame.WorkBefore != null) CompareWork(frame);
            if (Current != null&&Current.Epoch==epoch) { Current.Regions += frame.Regions; Current.Searches += frame.Searches; Current.Failed += frame.Failed; }
        }
        private static string DescribePreArgs(EventHookBase args)
        {
            if (args is TribeIssueOrderWithTargetEventArgs target)
                return target.TribeId + ":" + (int)target.AICommand + ":" + target.TargetValue1 + "/" + target.TargetValue2 + ":a6=" + target.a6;
            if (args is TribeIssueOrderMoveHereEventArgs move)
                return move.TribeId + ":" + (int)move.MoveType + ":" + move.TileX + "/" + move.TileY;
            if (args is UnitMoveHereEventArgs unit) return unit.UnitId + ":" + unit.TileX + "/" + unit.TileY;
            return "unknown";
        }
        internal void TargetOrder(TribeIssueOrderWithTargetEventArgs args)
        {
            if (!running)
            {
                if(args.Phase==EventHookPhase.Pre) { PruneSkipped(); Push(new Frame { Kind="target", Id=args.TribeId, PreEvent=args }); }
                else if(args.Phase==EventHookPhase.Post) Pop("target",args.TribeId,args.ReturnValue);
                return;
            }
            Guard("target", () =>
            {
                if (args.Phase == EventHookPhase.Pre)
                {
                    var frame = TribeFrame("target", args.TribeId, (int)args.AICommand, args.TargetValue1, args.TargetValue2);
                    frame.PreEvent = args;
                    Push(frame);
                }
                else if (args.Phase == EventHookPhase.Post) Pop("target", args.TribeId, args.ReturnValue);
            });
        }
        internal void TribeMove(TribeIssueOrderMoveHereEventArgs args)
        {
            if (!running)
            {
                if(args.Phase==EventHookPhase.Pre) { PruneSkipped(); Push(new Frame { Kind="move", Id=args.TribeId, PreEvent=args }); }
                else if(args.Phase==EventHookPhase.Post) Pop("move",args.TribeId,args.ReturnValue);
                return;
            }
            Guard("move", () =>
            {
                if (args.Phase == EventHookPhase.Pre)
                {
                    var work = WorkParent(args.TribeId);
                    if (work != null)
                    {
                        work.WorkMoves++;
                        Record(work, "work-access", "nested-work-move-observed",
                            "movementTarget=" + args.TileX + "/" + args.TileY + ",selectorReturn=not-directly-observed,execution=not-proven");
                    }
                    var frame = TribeFrame("move", args.TribeId, (int)args.MoveType, args.TileX, args.TileY);
                    frame.PreEvent = args;
                    Push(frame);
                    Trace.CompareGroupShadow(frame.TraceId,frame.ParentTrace,frame.Player,frame.StartX,frame.StartY,frame.X,frame.Y,frame.Tribe,frame.Global);
                }
                else if (args.Phase == EventHookPhase.Post) Pop("move", args.TribeId, args.ReturnValue);
            });
        }
        internal void UnitMovement(UnitMovementEventArgs args)
        {
            if(!running||args.UnitId>int.MaxValue)return;
            Trace.Routes.Observe((int)args.UnitId,args.Phase==EventHookPhase.Post);
        }
        internal void UnitMove(UnitMoveHereEventArgs args)
        {
            if (!running)
            {
                if(args.Phase==EventHookPhase.Pre) { PruneSkipped(); Push(new Frame { Kind="unit", Id=args.UnitId, PreEvent=args }); }
                else if(args.Phase==EventHookPhase.Post) Pop("unit",args.UnitId,args.ReturnValue);
                return;
            }
            Guard("unit", () =>
            {
                if (args.Phase == EventHookPhase.Pre)
                {
                    var frame = new Frame { Kind = "unit", Id = args.UnitId, X = args.TileX, Y = args.TileY,
                        PreEvent = args,
                        SourcePcl = -1, TargetPcl = Pcl(args.TileX, args.TileY), Identity = "unit-unavailable" };
                    if (GameUnitManagerAPI.Instance.IsValidId(args.UnitId) && GameUnitManagerAPI.Instance.TryGetUnitById(args.UnitId, out GameUnit* unit) && unit != null)
                    {
                        frame.Global=unit->r_GlobalId;frame.StartX=unit->r_CurrentTilePositionX;frame.StartY=unit->r_CurrentTilePositionY;
                        frame.Tribe = unit->r_TribeId; frame.Command = unit->r_AI_LastIssuedTribeCommand;
                        frame.RawUnitType=(int)unit->r_UnitChimp;
                        frame.Player = unit->r_ControllableForPlayerId | ((int)unit->N00000569 << 8);
                        frame.SourcePcl = Pcl(unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY);

                    }
                    Push(frame);
                }
                else if (args.Phase == EventHookPhase.Post) Pop("unit", args.UnitId, args.ReturnValue);
            });
        }
        private static Dictionary<int, UnitState> CaptureWork(int tribe)
        {
            var result = new Dictionary<int, UnitState>();
            var units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
            {
                ref GameUnit unit = ref units[spanIndex];
                if (unit.r_TribeId != tribe || unit.r_GlobalId == 0) continue;
                fixed (GameUnit* pointer = &unit) result.Add(spanIndex + 1, new UnitState(pointer));
            }
            return result;
        }
        private void CompareWork(Frame frame)
        {
            var after = CaptureWork(frame.Tribe);
            long changed = 0, same = 0, missing = 0;
            var matched = new HashSet<int>();
            foreach (var pair in frame.WorkBefore)
            {
                if (!after.TryGetValue(pair.Key, out var value) || value.Global != pair.Value.Global)
                { missing++; continue; }
                matched.Add(pair.Key);
                bool change = pair.Value.Command != value.Command || pair.Value.X != value.X || pair.Value.Y != value.Y;
                if (change) changed++; else same++;
                Record(frame, "work-unit-fields", change ? "command-or-context-changed" : "unchanged",
                    "unit=" + pair.Key + ",before=" + pair.Value + ",after=" + value + ",execution=not-proven");
            }
            foreach (var pair in after)
                if (!matched.Contains(pair.Key)) Record(frame, "work-unit-fields", "new-or-reused-after-pre",
                    "unit=" + pair.Key + ",after=" + pair.Value + ",execution=not-proven");
            Record(frame, "work-summary", "nestedMoves=" + frame.WorkMoves,
                "before=" + frame.WorkBefore.Count + ",after=" + after.Count + ",changed=" + changed + ",unchanged=" + same +
                ",missingOrReused=" + missing + ",newOrReused=" + (after.Count - matched.Count) +
                ",beforeInvariant=" + (frame.WorkBefore.Count == changed + same + missing) +
                ",execution=not-proven",
                changed);
        }
        public object BeginSearch(string source, int rawPlayer) => !running ? null :
            new Search { Frame = SessionFrame, Source = source, RawPlayer = rawPlayer };
        public void EndSearch(object token, bool completed, int? nativeResult, int effectiveResult, long nativeCalls)
        {
            if (!(token is Search search) || !running) return;
            if(search.Frame?.Detailed==true||Trace.DetailedNative) Trace.SearchResult(search.Frame?.TraceId??0,search.Source,nativeResult,effectiveResult,nativeCalls,completed);
            Interlocked.Increment(ref observedSearches);
            Trace.CountEvent(new BridgeDecisionTrace.Arguments(4,search.RawPlayer,nativeResult??int.MinValue,effectiveResult,completed?1:0,0));
            bool route = search.Source == "builder" || search.Source == "reconstructed-builder";
            if (search.Frame != null) { search.Frame.Searches++; if (!completed || (route && effectiveResult <= 0)) search.Frame.Failed++; }
            if(search.Frame?.Detailed==true||Trace.DetailedNative) Record(search.Frame, "search-" + search.Source,
                "completed=" + completed + ",native=" + (nativeResult?.ToString() ?? "unobserved-or-void") + ",effective=" + (route ? effectiveResult.ToString() : "void"),
                "rawPlayer=" + search.RawPlayer + ",movementPlayer=" + (search.Frame?.Player ?? 0) + ",nativeCalls=" + nativeCalls);
        }
        public void ObserveRegion(int rawPlayer, int sourcePcl, int targetPcl, int mode, int nativeResult, int effectiveResult)
        {
            if (!running) return;
            Trace.Region(rawPlayer,sourcePcl,targetPcl,mode,nativeResult,effectiveResult);
            var frame = SessionFrame;
            if (frame != null) frame.Regions++;

        }
        public object BeginAssassinSearch(int startX, int startY, int targetX, int targetY, int maximumNodes, int continuation, string nativeState)
        {
            if (!running) return null;
            Assassin token = null;
            Guard("assassin-begin", () =>
            {
                var frame = SessionFrame;
                Record(frame, "assassin-entry", "called", "start=" + startX + "/" + startY + ",target=" + targetX + "/" + targetY +
                    ",continuation=" + continuation + ",limit=" + maximumNodes + "," + nativeState);
                token = new Assassin { Frame = frame, Snapshot = CurrentSnapshot, X = targetX, Y = targetY };
            });
            return token;
        }
        public void ObserveAssassinEdge(object token, int playerId, int fromTile, int toTile, int direction, bool climb)
        {
            if (!(token is Assassin search)) return;
            Guard("assassin-edge", () =>
            {
                string result = search.Snapshot.DescribeEdge(playerId, fromTile, direction, climb);
                search.Edges.TryGetValue(result, out long previous); search.Edges[result] = previous + 1;
            });
        }
        public void ObserveAssassinPolicyFiltering(object token, int playerId, long ground, long climb)
        { if (token is Assassin search) Guard("assassin-filter", () => Record(search.Frame, "assassin-active-policy-filter", "observed", "player=" + playerId, ground + climb)); }
        public void ObserveAssassinBuildingSearch(int tribeId, int buildingId, int sourceRegion, int rawSearchPlayer)
        { if (running) Guard("assassin-building", () => Record(Current, "assassin-building", "called", "tribe=" + tribeId + ",building=" + buildingId + ",pcl=" + sourceRegion + ",rawPlayer=" + rawSearchPlayer)); }
        public void EndAssassinSearch(object token, int playerId, int vanillaResult, int effectiveResult, string outcome, bool cacheHit, int routeLength)
        {
            if (!(token is Assassin search)) return;
            Guard("assassin-end", () =>
            {
                Record(search.Frame, "assassin-result", "native=" + vanillaResult + ",effective=" + effectiveResult + ",cache=" + cacheHit + ",outcome=" + outcome,
                    "player=" + playerId + ",length=" + routeLength + ",snapshotStable=" + ReferenceEquals(CurrentSnapshot, search.Snapshot));
                foreach (var pair in search.Edges) Record(search.Frame, "assassin-hypothetical-edge", pair.Key, null, pair.Value);
            });
        }
    }
}
