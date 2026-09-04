using System;
using System.Collections.Generic;

namespace QueueTest
{
    internal static class QueueNativeContract
    {
        public const int GameTribePointerAdjustment = 0x2A;
        public const int ManagerRelativeWaypointIndexOffset = 0x5DC;
        public const int ManagerRelativeWaypointCountOffset = 0x5DE;
        public const int ManagerRelativeMovementModeOffset = 0x582;
        public const int GameTribeWaypointBaseOffset = 0x58A;
        public const int GameTribeWaypointIndexOffset =
            ManagerRelativeWaypointIndexOffset - GameTribePointerAdjustment;
        public const int GameTribeWaypointCountOffset =
            ManagerRelativeWaypointCountOffset - GameTribePointerAdjustment;
        public const int GameTribeMovementModeOffset =
            ManagerRelativeMovementModeOffset - GameTribePointerAdjustment;
        public const int GameUnitSize = 0x490;
        public const int GameUnitGlobalIdOffset = 0x94;
        public const int GameUnitAttackMarkerOffset = 0x68;
        public const int GameBuildingSize = 0x32C;
        public const int GameBuildingGlobalIdOffset = 0xD6;
        public const int GameBuildingAttackMarkerOffset = 0xC0;

        // Chore 8 stores the public, one-based game tribe ID. It is not a span index.
        public static int WaypointChoreValueToTribeId(int serializedTribeId) => serializedTribeId;
    }

    internal enum QueueCommandKind
    {
        Move,
        AttackUnit,
        AttackBuilding,
        ForceAttackBuilding
    }

    internal static class QueueVisualContract
    {
        public const int MovementMarkerCategory = 0xAC;
        public const int MovementMarkerLayer = 0x12;
        public const int MovementMarkerVerticalOffset = -1;
        public const int MovementMarkerFlags = 0xA0022;
        public const int PatrolOnceFlagSpriteFirst = 0x138;
        public const int PatrolOnceFlagSpriteLast = 0x141;

        public static bool ShouldSuppressFlag(
            QueueCommandKind kind,
            int category,
            int spriteId,
            int layer,
            int verticalOffset,
            int flags) =>
            kind != QueueCommandKind.Move && IsPatrolOnceFlagSubmission(
                category,
                spriteId,
                layer,
                verticalOffset,
                flags);

        public static bool IsPatrolOnceFlagSubmission(
            int category,
            int spriteId,
            int layer,
            int verticalOffset,
            int flags) =>
            category == MovementMarkerCategory &&
            layer == MovementMarkerLayer &&
            verticalOffset == MovementMarkerVerticalOffset &&
            flags == MovementMarkerFlags &&
            spriteId >= PatrolOnceFlagSpriteFirst &&
            spriteId <= PatrolOnceFlagSpriteLast;
    }

    internal sealed class QueueCommand
    {
        public QueueCommand(QueueCommandKind kind, int argument1, int argument2, int argument3 = 0)
        {
            Kind = kind;
            Argument1 = argument1;
            Argument2 = argument2;
            Argument3 = argument3;
        }

        public QueueCommandKind Kind { get; }
        public int Argument1 { get; }
        public int Argument2 { get; }
        public int Argument3 { get; }

        public bool IsAttack => Kind != QueueCommandKind.Move;

        public bool HasSamePayload(QueueCommand other) =>
            other != null &&
            Kind == other.Kind &&
            Argument1 == other.Argument1 &&
            Argument2 == other.Argument2 &&
            Argument3 == other.Argument3;

        public override string ToString() =>
            $"{Kind}({Argument1},{Argument2},{Argument3})";
    }

    internal static class QueueCommandClassifier
    {
        public const int AttackUnitValue = 4;
        public const int AttackBuildingValue = 9;
        public const int ForceAttackBuildingValue = 36;

        public static bool TryClassifyTarget(int commandValue, out QueueCommandKind kind)
        {
            switch (commandValue)
            {
                case AttackUnitValue:
                    kind = QueueCommandKind.AttackUnit;
                    return true;
                case AttackBuildingValue:
                    kind = QueueCommandKind.AttackBuilding;
                    return true;
                case ForceAttackBuildingValue:
                    kind = QueueCommandKind.ForceAttackBuilding;
                    return true;
                default:
                    kind = default(QueueCommandKind);
                    return false;
            }
        }
    }

    internal sealed class TribeQueueState
    {
        private readonly Queue<QueueCommand> pending = new Queue<QueueCommand>();
        private readonly List<ExpectedMoveSignal> expectedMoveChores = new List<ExpectedMoveSignal>();
        private readonly List<ExpectedMoveSignal> expectedMoveEvents = new List<ExpectedMoveSignal>();
        private readonly List<QueueUnitIdentity> members;

        public TribeQueueState(
            uint tribeGlobalId,
            int maximumPendingCommands,
            IEnumerable<QueueUnitIdentity> members = null)
        {
            if (maximumPendingCommands <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumPendingCommands));

            TribeGlobalId = tribeGlobalId;
            MaximumPendingCommands = maximumPendingCommands;
            this.members = members == null
                ? new List<QueueUnitIdentity>()
                : new List<QueueUnitIdentity>(members);
        }

        public uint TribeGlobalId { get; private set; }
        public IReadOnlyList<QueueUnitIdentity> Members => members;
        public int MaximumPendingCommands { get; }
        public int PendingCount => pending.Count;
        public int ExpectedMoveChoreCount => expectedMoveChores.Count;
        public int ExpectedMoveEventCount => expectedMoveEvents.Count;
        public QueueCommand Active { get; private set; }
        public QueueCommand ExternalAttack { get; set; }
        public bool WaitForVanillaMovement { get; set; }
        public bool ActiveNeedsRedispatch { get; private set; }
        public int LastWaitDiagnosticTick { get; private set; } = -1;

        public bool MatchesTribe(uint globalId) => TribeGlobalId == globalId;

        public void RebindTribe(uint globalId)
        {
            TribeGlobalId = globalId;
            ActiveNeedsRedispatch = Active != null;
            LastWaitDiagnosticTick = -1;
        }

        public void MarkActiveRedispatched() => ActiveNeedsRedispatch = false;

        public bool SharesMemberWith(IEnumerable<QueueUnitIdentity> candidates)
        {
            if (candidates == null)
                return false;

            foreach (QueueUnitIdentity candidate in candidates)
            {
                for (int index = 0; index < members.Count; index++)
                {
                    if (members[index].Equals(candidate))
                        return true;
                }
            }
            return false;
        }

        public bool ContainsAttack
        {
            get
            {
                if ((Active != null && Active.IsAttack) || ExternalAttack != null)
                    return true;
                foreach (QueueCommand command in pending)
                {
                    if (command.IsAttack)
                        return true;
                }
                return false;
            }
        }

        public List<QueueCommand> GetPendingCommands(int maximum)
        {
            return GetPendingCommands(maximum, command => true);
        }

        public List<QueueCommand> GetPendingCommands(
            int maximum,
            Func<QueueCommand, bool> isVisible)
        {
            if (maximum < 0)
                throw new ArgumentOutOfRangeException(nameof(maximum));
            if (isVisible == null)
                throw new ArgumentNullException(nameof(isVisible));

            List<QueueCommand> commands = new List<QueueCommand>(maximum);
            if (maximum == 0)
                return commands;
            foreach (QueueCommand command in pending)
            {
                if (!isVisible(command))
                    continue;
                commands.Add(command);
                if (commands.Count == maximum)
                    break;
            }
            return commands;
        }

        public bool TryEnqueue(QueueCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (pending.Count >= MaximumPendingCommands)
                return false;

            pending.Enqueue(command);
            return true;
        }

        public void ExpectMoveChore(QueueCommand command, int expiresAfterTick)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (command.Kind != QueueCommandKind.Move)
                throw new ArgumentException("Only Move commands have waypoint chores.", nameof(command));

            expectedMoveChores.Add(new ExpectedMoveSignal(command, expiresAfterTick));
        }

        public bool TryConsumeExpectedMoveChore(QueueCommand command, int currentTick)
        {
            return TryConsumeExpectedMoveSignal(expectedMoveChores, command, currentTick);
        }

        public void ExpectMoveEvent(QueueCommand command, int expiresAfterTick)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (command.Kind != QueueCommandKind.Move)
                throw new ArgumentException("Only Move commands have move-order events.", nameof(command));

            expectedMoveEvents.Add(new ExpectedMoveSignal(command, expiresAfterTick));
        }

        public bool TryConsumeExpectedMoveEvent(QueueCommand command, int currentTick)
        {
            return TryConsumeExpectedMoveSignal(expectedMoveEvents, command, currentTick);
        }

        private static bool TryConsumeExpectedMoveSignal(
            List<ExpectedMoveSignal> expectedSignals,
            QueueCommand command,
            int currentTick)
        {
            for (int index = expectedSignals.Count - 1; index >= 0; index--)
            {
                if (expectedSignals[index].ExpiresAfterTick < currentTick)
                    expectedSignals.RemoveAt(index);
            }

            for (int index = 0; index < expectedSignals.Count; index++)
            {
                if (!expectedSignals[index].Command.HasSamePayload(command))
                    continue;

                expectedSignals.RemoveAt(index);
                return true;
            }

            return false;
        }

        public bool TryActivateNext(out QueueCommand command)
        {
            if (Active != null || pending.Count == 0)
            {
                command = null;
                return false;
            }

            Active = pending.Dequeue();
            command = Active;
            return true;
        }

        public void CompleteActive()
        {
            Active = null;
            ActiveNeedsRedispatch = false;
            LastWaitDiagnosticTick = -1;
        }

        public bool ShouldLogWaitDiagnostic(int tick, int interval)
        {
            if (LastWaitDiagnosticTick >= 0 && tick - LastWaitDiagnosticTick < interval)
                return false;

            LastWaitDiagnosticTick = tick;
            return true;
        }

        public void ResetWaitDiagnostic() => LastWaitDiagnosticTick = -1;

        public bool IsEmpty =>
            Active == null && pending.Count == 0 && ExternalAttack == null && !WaitForVanillaMovement;
    }

    internal sealed class ExpectedMoveSignal
    {
        public ExpectedMoveSignal(QueueCommand command, int expiresAfterTick)
        {
            Command = command;
            ExpiresAfterTick = expiresAfterTick;
        }

        public QueueCommand Command { get; }
        public int ExpiresAfterTick { get; }
    }

    internal readonly struct QueueUnitIdentity : IEquatable<QueueUnitIdentity>
    {
        public QueueUnitIdentity(int unitId, uint globalId)
        {
            UnitId = unitId;
            GlobalId = globalId;
        }

        public int UnitId { get; }
        public uint GlobalId { get; }

        public bool Equals(QueueUnitIdentity other) =>
            UnitId == other.UnitId && GlobalId == other.GlobalId;

        public override bool Equals(object obj) =>
            obj is QueueUnitIdentity other && Equals(other);

        public override int GetHashCode() => (UnitId * 397) ^ unchecked((int)GlobalId);

        public override string ToString() => $"{UnitId}/{GlobalId}";
    }
}
