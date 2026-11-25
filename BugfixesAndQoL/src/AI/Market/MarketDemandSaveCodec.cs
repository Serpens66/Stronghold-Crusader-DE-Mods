using System;
using System.IO;

namespace BugfixesAndQoL.AI.Market
{
    // Explicit little-endian schema; only synchronized scalar state is persisted.
    // Schema1 pins the audited native player/goods IDs (8 players,25 goods,gold15,
    // stone4). Runtime ValidateInteropLayout checks these serialized identities.
    internal struct MarketDemandSaveRow
    {
        internal int Player, Good, SourceRva, PolicySourceRva, SourceStock, GoalAmount;
        internal MarketDemandTarget Target;
        internal int UnitId, UnitOwner;
        internal uint UnitGlobalId, UnitCreationTick;
    }

    internal static class MarketDemandSaveCodec
    {
        internal const int MaximumRows = 184, MaximumBytes = 32768;
        private const int Magic = 0x31444D41, RowBytes = 92;
        internal static byte[] Encode(MarketDemandSaveRow[] rows)
        {
            Validate(rows);
            using (var stream = new MemoryStream(8 + rows.Length * RowBytes))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Magic); writer.Write(rows.Length);
                foreach (var r in rows)
                {
                    var t = r.Target;
                    writer.Write(r.Player); writer.Write(r.Good); writer.Write(r.SourceRva); writer.Write(r.PolicySourceRva);
                    writer.Write(r.SourceStock); writer.Write(r.GoalAmount);
                    writer.Write((int)t.Policy); writer.Write((int)t.Family);
                    writer.Write(t.ExpectedPending); writer.Write(t.OriginalPackage); writer.Write(t.Credited);
                    writer.Write(t.LastObservedStock); writer.Write(t.AicId); writer.Write(t.MaterialCost); writer.Write(t.MaterialPadding);
                    writer.Write(t.Goal); writer.Write(t.PolicyToken);
                    writer.Write(r.UnitId); writer.Write(r.UnitOwner); writer.Write(r.UnitGlobalId); writer.Write(r.UnitCreationTick);
                }
                return stream.ToArray();
            }
        }
        internal static MarketDemandSaveRow[] Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 8 || bytes.Length > MaximumBytes) throw new InvalidDataException("Invalid demand ledger size.");
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadInt32() != Magic) throw new InvalidDataException("Unknown demand ledger schema.");
                int count = reader.ReadInt32();
                if (count < 0 || count > MaximumRows || bytes.Length != 8 + count * RowBytes) throw new InvalidDataException("Invalid demand ledger row count.");
                var rows = new MarketDemandSaveRow[count];
                for (int i = 0; i < count; i++)
                {
                    var r = new MarketDemandSaveRow { Player = reader.ReadInt32(), Good = reader.ReadInt32(), SourceRva = reader.ReadInt32(), PolicySourceRva = reader.ReadInt32() };
                    r.SourceStock = reader.ReadInt32(); r.GoalAmount = reader.ReadInt32();
                    r.Target = new MarketDemandTarget { Active = true, Policy = (MarketDemandPolicy)reader.ReadInt32(), Family = (MarketDemandSourceFamily)reader.ReadInt32(),
                        ExpectedPending = reader.ReadInt32(), OriginalPackage = reader.ReadInt32(), Credited = reader.ReadInt32(), LastObservedStock = reader.ReadInt32(),
                        AicId = reader.ReadInt32(), MaterialCost = reader.ReadInt32(), MaterialPadding = reader.ReadInt32(), Goal = reader.ReadInt64(), PolicyToken = reader.ReadInt64() };
                    r.UnitId = reader.ReadInt32(); r.UnitOwner = reader.ReadInt32(); r.UnitGlobalId = reader.ReadUInt32(); r.UnitCreationTick = reader.ReadUInt32();
                    rows[i] = r;
                }
                Validate(rows); return rows;
            }
        }
        private static void Validate(MarketDemandSaveRow[] rows)
        {
            if (rows == null || rows.Length > MaximumRows) throw new InvalidDataException("Too many demand ledger rows.");
            int previous = 0;
            foreach (var r in rows)
            {
                var t = r.Target;
                int key = r.Player * 25 + r.Good;
                if (r.Player < 1 || r.Player > 8 || r.Good < 1 || r.Good >= 25 || r.Good == 15 || key <= previous ||
                    r.SourceRva <= 0 || r.PolicySourceRva <= 0 || r.SourceStock < 0 || r.GoalAmount <= 0 || !t.Active || t.ExpectedPending <= 0 || t.OriginalPackage <= 0 ||
                    t.LastObservedStock < 0 || t.AicId <= 0 || t.Goal < 0 || t.Credited < 0 || t.Credited > t.OriginalPackage ||
                    (uint)t.Policy > (uint)MarketDemandPolicy.AutoAmmo || (uint)t.Family > (uint)MarketDemandSourceFamily.Fixes)
                    throw new InvalidDataException("Invalid demand ledger row.");
                if (t.Policy == MarketDemandPolicy.FixedBatch && (t.MaterialCost != 0 || t.MaterialPadding != 0 || t.ExpectedPending > t.OriginalPackage) ||
                    t.Policy == MarketDemandPolicy.MaterialStockGoal && (t.MaterialCost <= 0 || t.Credited != 0 || t.MaterialPadding != 0 && t.MaterialPadding != 2) ||
                    t.Policy == MarketDemandPolicy.AutoAmmo && (t.Family != MarketDemandSourceFamily.Ammo || t.MaterialCost != 10 || t.OriginalPackage != 10 || t.ExpectedPending > 10 ||
                        t.MaterialPadding != 0 || t.Credited != 0 || t.Goal != 10 || r.Good != 4 || r.SourceStock >= 10 || r.GoalAmount != 10 || r.UnitId <= 0 || r.UnitOwner != r.Player) ||
                    t.Policy != MarketDemandPolicy.AutoAmmo && (r.UnitId != 0 || r.UnitOwner != 0 || r.UnitGlobalId != 0 || r.UnitCreationTick != 0))
                    throw new InvalidDataException("Invalid source-specific demand state.");
                previous = key;
            }
        }
    }
}
