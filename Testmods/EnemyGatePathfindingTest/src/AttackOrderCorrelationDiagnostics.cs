using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Text;
using System.Threading;

namespace EnemyGatePathfindingTest
{
    // Bounded, observational correlation around Vanilla's existing 11E960 order call.
    // No route, PCL, or target search is started here. The only per-order work is a
    // handful of validated native-structure reads and counter updates.
    internal sealed unsafe class AttackOrderCorrelationDiagnostics
    {
        private const int MaximumNestedOrders = 8;
        private const int MaximumSamples = 32;
        private const int CommandCount = 6;

        [ThreadStatic] private static AttackFrame[] threadFrames;
        [ThreadStatic] private static int threadDepth;

        private readonly ManualLogSource log;
        private readonly GateTopologySnapshotProvider topology;
        private readonly object sampleGate = new object();
        private readonly AttackSample[] samples = new AttackSample[MaximumSamples];
        private readonly long[] commandCounts = new long[CommandCount];
        private readonly long[] lastCommandCounts = new long[CommandCount];
        private int sampleCount;
        private int publishedSamples;
        private long orders, completed, skipped, invalidContexts, nestingOverflows,
            postMismatches, builderCalls, builderSuccesses, builderNoRoutes,
            builderDetours, rejectedEdges;
        private long lastOrders, lastBuilderCalls, lastBuilderSuccesses,
            lastBuilderNoRoutes, lastBuilderDetours, lastRejectedEdges;

        internal AttackOrderCorrelationDiagnostics(
            ManualLogSource log,
            GateTopologySnapshotProvider topology)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.topology = topology ?? throw new ArgumentNullException(nameof(topology));
        }

        internal void ObserveOrder(TribeIssueOrderWithTargetEventArgs args)
        {
            if (args == null || !TryGetCommandIndex(args.AICommand, out int commandIndex))
                return;

            if (args.Phase == EventHookPhase.Pre)
            {
                AttackFrame frame = CaptureFrame(args, commandIndex);
                if (!frame.Active)
                    return;
                Interlocked.Increment(ref orders);
                Interlocked.Increment(ref commandCounts[commandIndex]);
                if (threadFrames == null)
                    threadFrames = new AttackFrame[MaximumNestedOrders];
                if (threadDepth >= threadFrames.Length)
                {
                    Interlocked.Increment(ref nestingOverflows);
                    return;
                }
                threadFrames[threadDepth++] = frame;
                if (args.SkipOriginalFunction)
                {
                    Interlocked.Increment(ref skipped);
                    PublishAndPop(frame, threadDepth - 1);
                }
                return;
            }

            if (args.Phase != EventHookPhase.Post || threadDepth <= 0 || threadFrames == null)
                return;

            if (!GameTribeManagerAPI.Instance.TryGetTribeById(
                    args.TribeId, out GameTribe* postTribe) || postTribe == null ||
                postTribe->r_PlayerIdOwner <= 0 || postTribe->r_PlayerIdOwner > 8 ||
                !GamePlayerManagerAPI.Instance.IsAIPlayer(postTribe->r_PlayerIdOwner))
                return;

            int index = threadDepth - 1;
            AttackFrame pending = threadFrames[index];
            if (pending.TribeId != args.TribeId || pending.Command != args.AICommand)
                Interlocked.Increment(ref postMismatches);
            PublishAndPop(pending, index);
        }

        internal void ObserveBuilder(
            int player,
            bool completedNormally,
            bool success,
            long policyEdges,
            ulong policyFingerprint)
        {
            if (threadDepth <= 0 || threadFrames == null)
                return;
            int index = threadDepth - 1;
            AttackFrame frame = threadFrames[index];
            if (!frame.Active || frame.PlayerId != player)
                return;
            frame.BuilderCalls++;
            if (completedNormally)
            {
                if (success) frame.BuilderSuccesses++;
                else frame.BuilderNoRoutes++;
                if (success && policyEdges > 0) frame.BuilderDetours++;
            }
            frame.RejectedEdges += policyEdges;
            if (frame.PolicyFingerprint == 0)
                frame.PolicyFingerprint = policyFingerprint;
            threadFrames[index] = frame;
        }

        internal void Reset()
        {
            Reset(ref orders); Reset(ref completed); Reset(ref skipped);
            Reset(ref invalidContexts); Reset(ref nestingOverflows); Reset(ref postMismatches);
            Reset(ref builderCalls); Reset(ref builderSuccesses); Reset(ref builderNoRoutes);
            Reset(ref builderDetours); Reset(ref rejectedEdges);
            Array.Clear(commandCounts, 0, commandCounts.Length);
            Array.Clear(lastCommandCounts, 0, lastCommandCounts.Length);
            lock (sampleGate)
            {
                Array.Clear(samples, 0, samples.Length);
                sampleCount = 0;
                publishedSamples = 0;
            }
            lastOrders = lastBuilderCalls = lastBuilderSuccesses = 0;
            lastBuilderNoRoutes = lastBuilderDetours = lastRejectedEdges = 0;
            threadDepth = 0;
        }

        internal string DescribeCheckpoint()
        {
            long currentOrders = Read(ref orders);
            long currentBuilders = Read(ref builderCalls);
            long currentSuccesses = Read(ref builderSuccesses);
            long currentNoRoutes = Read(ref builderNoRoutes);
            long currentDetours = Read(ref builderDetours);
            long currentEdges = Read(ref rejectedEdges);
            var text = new StringBuilder();
            text.Append("orders=").Append(currentOrders).Append("(+")
                .Append(currentOrders - lastOrders).Append("),commands=[");
            for (int index = 0; index < CommandCount; index++)
            {
                if (index != 0) text.Append(',');
                long count = Read(ref commandCounts[index]);
                text.Append(CommandName(index)).Append('=').Append(count).Append("(+")
                    .Append(count - lastCommandCounts[index]).Append(')');
                lastCommandCounts[index] = count;
            }
            text.Append("],completed=").Append(Read(ref completed))
                .Append(",skipped=").Append(Read(ref skipped))
                .Append(",builderCalls=").Append(currentBuilders).Append("(+")
                .Append(currentBuilders - lastBuilderCalls).Append(')')
                .Append(",builderSuccess=").Append(currentSuccesses).Append("(+")
                .Append(currentSuccesses - lastBuilderSuccesses).Append(')')
                .Append(",builderDetours=").Append(currentDetours).Append("(+")
                .Append(currentDetours - lastBuilderDetours).Append(')')
                .Append(",builderNoRoute=").Append(currentNoRoutes).Append("(+")
                .Append(currentNoRoutes - lastBuilderNoRoutes).Append(')')
                .Append(",policyEdges=").Append(currentEdges).Append("(+")
                .Append(currentEdges - lastRejectedEdges).Append(')')
                .Append(",invalidContext=").Append(Read(ref invalidContexts))
                .Append(",nestingOverflow=").Append(Read(ref nestingOverflows))
                .Append(",postMismatch=").Append(Read(ref postMismatches))
                .Append(",samples=").Append(Volatile.Read(ref sampleCount));
            lastOrders = currentOrders;
            lastBuilderCalls = currentBuilders;
            lastBuilderSuccesses = currentSuccesses;
            lastBuilderNoRoutes = currentNoRoutes;
            lastBuilderDetours = currentDetours;
            lastRejectedEdges = currentEdges;
            return text.ToString();
        }

        internal void ProcessDeferred()
        {
            lock (sampleGate)
            {
                while (publishedSamples < sampleCount)
                {
                    AttackSample sample = samples[publishedSamples++];
                    Shared.DebugLogHelper.LogInfo(log,
                        "Enemy-gate AI order correlation sample: " + sample.Format());
                }
            }
        }

        private AttackFrame CaptureFrame(
            TribeIssueOrderWithTargetEventArgs args,
            int commandIndex)
        {
            if (!GameTribeManagerAPI.Instance.TryGetTribeById(
                    args.TribeId, out GameTribe* tribe) || tribe == null)
            {
                Interlocked.Increment(ref invalidContexts);
                return default;
            }
            int player = tribe->r_PlayerIdOwner;
            if (player <= 0 || player > 8 || !GamePlayerManagerAPI.Instance.IsAIPlayer(player))
                return default;

            int sourceUnitId = tribe->r_LeaderUnitId;
            int sourceGlobal = 0, sourceX = -1, sourceY = -1, sourcePcl = 0;
            if (sourceUnitId > 0 && GameUnitManagerAPI.Instance.TryGetUnitById(
                    sourceUnitId, out GameUnit* source) && source != null &&
                source->r_TribeId == args.TribeId)
            {
                sourceGlobal = unchecked((int)source->r_GlobalId);
                sourceX = source->r_CurrentTilePositionX;
                sourceY = source->r_CurrentTilePositionY;
                sourcePcl = ReadPcl(sourceX, sourceY);
            }

            ResolveTarget(args.AICommand, args.TargetValue1, args.TargetValue2,
                out int targetId, out int targetGlobal, out int targetX,
                out int targetY, out int targetTile, out int targetPcl);
            AttackGateDiagnostic gate = topology.CaptureAttackGateDiagnostic(
                player, sourcePcl, targetPcl);
            return new AttackFrame
            {
                Active = true,
                CommandIndex = commandIndex,
                Command = args.AICommand,
                TribeId = args.TribeId,
                PlayerId = player,
                TargetValue1 = args.TargetValue1,
                TargetValue2 = args.TargetValue2,
                TargetId = targetId,
                TargetGlobal = targetGlobal,
                TargetX = targetX,
                TargetY = targetY,
                TargetTile = targetTile,
                TargetPcl = targetPcl,
                SourceUnitId = sourceUnitId,
                SourceGlobal = sourceGlobal,
                SourceX = sourceX,
                SourceY = sourceY,
                SourcePcl = sourcePcl,
                PolicyFingerprint = gate.PolicyFingerprint,
                Gate = gate
            };
        }

        private void ResolveTarget(
            TribeAICommand command,
            int value1,
            int value2,
            out int targetId,
            out int targetGlobal,
            out int x,
            out int y,
            out int tile,
            out int pcl)
        {
            targetId = 0; targetGlobal = 0; x = y = -1; tile = -1; pcl = 0;
            if (command == TribeAICommand.AttackUnit || command == TribeAICommand.Unknown32)
            {
                targetId = value1; targetGlobal = value2;
                if (targetId > 0 && GameUnitManagerAPI.Instance.TryGetUnitById(
                        targetId, out GameUnit* unit) && unit != null &&
                    (targetGlobal == 0 || unchecked((int)unit->r_GlobalId) == targetGlobal))
                {
                    x = unit->r_CurrentTilePositionX; y = unit->r_CurrentTilePositionY;
                    targetGlobal = unchecked((int)unit->r_GlobalId);
                }
            }
            else if (command == TribeAICommand.AttackBuilding)
            {
                targetId = value1; targetGlobal = value2;
                if (targetId > 0 && GameBuildingManagerAPI.Instance.TryGetBuildingById(
                        targetId, out GameBuilding* building) && building != null &&
                    (targetGlobal == 0 || unchecked((int)building->r_GlobalId) == targetGlobal))
                {
                    x = building->r_TilePositionXBegin; y = building->r_TilePositionYBegin;
                    targetGlobal = unchecked((int)building->r_GlobalId);
                }
            }
            else if (command == TribeAICommand.AttackWallTileId)
            {
                tile = value1;
                if ((uint)tile < EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive)
                {
                    var position = GameTileManagerAPI.Instance.GetTileVectorFromId(tile);
                    x = position.X; y = position.Y;
                }
            }
            else
            {
                x = value1; y = value2;
            }

            if (tile < 0 && x >= 0 && y >= 0)
                tile = GameTileManagerAPI.Instance.GetTileId(x, y);
            if ((uint)tile < EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive)
                pcl = GamePathingManagerAPI.Instance.GetPathComponentIdByTileId(tile);
        }

        private static int ReadPcl(int x, int y) => x >= 0 && y >= 0
            ? GamePathingManagerAPI.Instance.GetPathComponentId(x, y) : 0;

        private void PublishAndPop(AttackFrame frame, int index)
        {
            threadFrames[index] = default;
            threadDepth = index;
            Interlocked.Increment(ref completed);
            Interlocked.Add(ref builderCalls, frame.BuilderCalls);
            Interlocked.Add(ref builderSuccesses, frame.BuilderSuccesses);
            Interlocked.Add(ref builderNoRoutes, frame.BuilderNoRoutes);
            Interlocked.Add(ref builderDetours, frame.BuilderDetours);
            Interlocked.Add(ref rejectedEdges, frame.RejectedEdges);
            lock (sampleGate)
            {
                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    if (samples[sampleIndex].Matches(frame))
                        return;
                }
                if (sampleCount >= samples.Length)
                    return;
                samples[sampleCount++] = new AttackSample(frame);
            }
        }

        private static bool TryGetCommandIndex(TribeAICommand command, out int index)
        {
            switch (command)
            {
                case TribeAICommand.AttackUnit: index = 0; return true;
                case TribeAICommand.Unknown32: index = 1; return true;
                case TribeAICommand.AttackBuilding: index = 2; return true;
                case TribeAICommand.AttackWallTileId: index = 3; return true;
                case TribeAICommand.DigMoatTileId: index = 4; return true;
                case TribeAICommand.AttackTilePosition: index = 5; return true;
                default: index = -1; return false;
            }
        }

        private static string CommandName(int index)
        {
            switch (index)
            {
                case 0: return "AttackUnit";
                case 1: return "Unknown32";
                case 2: return "AttackBuilding";
                case 3: return "AttackWallTileId";
                case 4: return "DigMoatTileId";
                case 5: return "AttackTilePosition";
                default: return "Unknown";
            }
        }

        private static long Read(ref long value) => Interlocked.Read(ref value);
        private static void Reset(ref long value) => Interlocked.Exchange(ref value, 0);

        private struct AttackFrame
        {
            internal bool Active;
            internal int CommandIndex, TribeId, PlayerId, TargetValue1, TargetValue2;
            internal TribeAICommand Command;
            internal int TargetId, TargetGlobal, TargetX, TargetY, TargetTile, TargetPcl;
            internal int SourceUnitId, SourceGlobal, SourceX, SourceY, SourcePcl;
            internal ulong PolicyFingerprint;
            internal AttackGateDiagnostic Gate;
            internal long BuilderCalls, BuilderSuccesses, BuilderNoRoutes,
                BuilderDetours, RejectedEdges;
        }

        private readonly struct AttackSample
        {
            private readonly AttackFrame frame;
            internal AttackSample(AttackFrame frame) { this.frame = frame; }
            internal bool Matches(AttackFrame candidate) =>
                frame.TribeId == candidate.TribeId && frame.Command == candidate.Command &&
                frame.TargetValue1 == candidate.TargetValue1 &&
                frame.TargetValue2 == candidate.TargetValue2;
            internal string Format() =>
                "tribe=" + frame.TribeId + ",player=" + frame.PlayerId +
                ",command=" + frame.Command + ",targetRaw=" + frame.TargetValue1 + "/" +
                frame.TargetValue2 + ",targetId=" + frame.TargetId + ",targetGlobal=" +
                frame.TargetGlobal + ",targetXY=" + frame.TargetX + "/" + frame.TargetY +
                ",targetTile=" + frame.TargetTile + ",targetPcl=" + frame.TargetPcl +
                ",sourceUnit=" + frame.SourceUnitId + "/g" + frame.SourceGlobal +
                ",sourceXY=" + frame.SourceX + "/" + frame.SourceY +
                ",sourcePcl=" + frame.SourcePcl + ",policyFingerprint=0x" +
                frame.PolicyFingerprint.ToString("X16") + ",builders=" + frame.BuilderCalls +
                ",success=" + frame.BuilderSuccesses + ",detours=" + frame.BuilderDetours +
                ",noRoute=" + frame.BuilderNoRoutes + ",policyEdges=" + frame.RejectedEdges +
                ",gate=[" + frame.Gate.Format() + "]";
        }
    }
}
