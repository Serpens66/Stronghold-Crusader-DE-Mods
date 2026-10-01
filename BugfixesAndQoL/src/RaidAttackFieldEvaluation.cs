using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    // Pure list/decision contract shared by the runtime and the native-buffer regression test.
    internal static unsafe class RaidAttackFieldEvaluation
    {
        internal const int Capacity = 500;
        internal const int RecordSize = 12;
        internal const int UnreachableScore = 10000000;

        internal static AttackCandidateSnapshot Read(IntPtr context, int listOffset)
        {
            if (context == IntPtr.Zero)
                return new AttackCandidateSnapshot(false, -1, new CandidateRecord[0]);
            byte* firstRecord = (byte*)context.ToPointer() + listOffset;
            var records = new List<CandidateRecord>(Capacity);
            for (int index = 0; index < Capacity; index++)
            {
                int* record = (int*)(firstRecord + index * RecordSize);
                // 0x123090 clears only the approach field at the end. The other
                // fields and every record beyond it can still contain old data.
                if (record[0] == 0)
                    return new AttackCandidateSnapshot(true, index, records.ToArray());
                records.Add(new CandidateRecord(record[0], record[1], record[2]));
            }
            return new AttackCandidateSnapshot(true, -1, records.ToArray());
        }

        internal static AttackResult Evaluate(AttackCandidateSnapshot snapshot,
            bool pairMatches, string freshness, long commandReturn, bool targetIdentityValid,
            int buildingId, int tileCapacity, Func<int, int> getBuildingAtTile,
            Func<int, int, bool> isCardinalPair, out string validation)
        {
            validation = "contextUnavailable";
            if (!snapshot.Available) return AttackResult.Unknown;
            validation = "listEndNotFoundInFirst500";
            if (!snapshot.Complete) return AttackResult.Unknown;
            validation = "notEvaluatedWithoutFreshness";
            if (!pairMatches || freshness != "nativeSearchObserved") return AttackResult.Unknown;
            validation = "commandReturnNotSuccess";
            if (commandReturn != 1) return AttackResult.Unknown;
            validation = "targetIdentityChanged";
            if (!targetIdentityValid) return AttackResult.Unknown;
            if (snapshot.Records.Length == 0)
            {
                validation = "emptyList";
                return AttackResult.NoAttackPoint;
            }
            CandidateRecord first = snapshot.Records[0];
            validation = "approachTileOutOfRange";
            if (first.ApproachTile <= 0 || first.ApproachTile >= tileCapacity)
                return AttackResult.Unknown;
            if (first.BuildingTile == 0)
            {
                // The command's first building-field gate fails before the unit
                // pass; even later valid pairs do not cause Vanilla to move.
                validation = "nativeFirstBuildingGateFailed";
                return AttackResult.NoAttackPoint;
            }
            validation = "buildingTileOutOfRange";
            if (first.BuildingTile < 0 || first.BuildingTile >= tileCapacity)
                return AttackResult.Unknown;
            validation = "firstPairWrongBuilding";
            if (getBuildingAtTile(first.BuildingTile) != buildingId)
                return AttackResult.Unknown;
            validation = "firstScoreInvalid";
            if (first.Score >= UnreachableScore) return AttackResult.Unknown;
            validation = "firstPairNotCardinalOrTileMappingInvalid";
            if (!isCardinalPair(first.ApproachTile, first.BuildingTile))
                return AttackResult.Unknown;
            validation = "validFirstMeleePair";
            return AttackResult.AttackPoint;
        }

        internal sealed class AttackCandidateSnapshot
        {
            internal readonly bool Available;
            internal readonly int TerminatorAt;
            internal readonly CandidateRecord[] Records;
            internal bool Complete => Available && TerminatorAt >= 0;

            internal AttackCandidateSnapshot(bool available, int terminatorAt, CandidateRecord[] records)
            {
                Available = available;
                TerminatorAt = terminatorAt;
                Records = records;
            }

            internal bool SameRecords(AttackCandidateSnapshot other)
            {
                if (other == null || Available != other.Available ||
                    TerminatorAt != other.TerminatorAt || Records.Length != other.Records.Length)
                    return false;
                for (int i = 0; i < Records.Length; i++)
                    if (!Records[i].Equals(other.Records[i])) return false;
                return true;
            }

            internal string DescribeCompact()
            {
                CandidateRecord first = Records.Length != 0 ? Records[0] : default;
                int pairs = 0;
                foreach (CandidateRecord record in Records)
                    if (record.BuildingTile != 0) pairs++;
                return $"scratchAvailable={Available}, terminatorAt=" +
                    (TerminatorAt >= 0 ? TerminatorAt.ToString() : "notInFirst500") +
                    $", entryCount={Records.Length}, pairedEntries={pairs}, " +
                    $"firstPair={first.ApproachTile}/{first.BuildingTile}/{first.Score}";
            }
        }

        internal readonly struct CandidateRecord : IEquatable<CandidateRecord>
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
                ApproachTile == other.ApproachTile && BuildingTile == other.BuildingTile && Score == other.Score;
        }

        internal enum AttackResult { Unknown, NoAttackPoint, AttackPoint }
    }
}
