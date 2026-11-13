using static Pathfinding.UnitCommands.UnitCommandPathRuntime;
using System;
using System.Collections.Generic;
using System.IO;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace Pathfinding.Engine
{
    // Open movement cases in saves (APIShared VersionedSaveData, registered in FastMovementScheduler):
    // fire and plague escapes, solo resumes and replan waits, plus the replan watch state they depend on
    // (observed tribe orders, settled units, escape retry ticks). Escaping and solo soldiers stand in
    // one-man tribes that the game saves; without their cases an AI soldier would stay in a tribe the AI
    // never orders and a human one would not rejoin its group. Ticks are native map ticks, which the save
    // preserves. A loaded record is applied on the first simulation tick after the load, before the watch
    // and escape ticks, and every case is checked against the unit's and tribes' saved global ids: a case
    // that cannot continue (unit gone, tribe replaced, feature now disabled) ends at once, which rejoins
    // the soldier to its original group (RejoinSoldier). Simulation state only, encoded in key order:
    // every peer writes and restores the same bytes.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        internal const string CaseSaveIdentifier = "Pathfinding_Serp.MovementCases";
        // Version 1: the entries below. Raise it (and MinimumReader when an entry changes meaning) per APIShared's SAVE_DATA.md.
        internal const int CaseRecordVersion = 1, CaseRecordMinimumReader = 1;
        private const string CaseEscapes = "escapes", CaseEscapeRetry = "escapeRetry", CaseSolos = "solos", CaseWaits = "waits",
            CaseSettled = "settled", CaseOrders = "orders";
        internal static readonly string[] CaseEntries = { CaseEscapes, CaseEscapeRetry, CaseSolos, CaseWaits, CaseSettled, CaseOrders };
        // Policy of a saved wait: Pathfinding's fire or plague policy; another owner's policy is not restored.
        private const byte CasePolicyOther = 0, CasePolicyFire = 1, CasePolicyPlague = 2;
        // Encoded sizes of SoldierTribe and TribeOrder (WriteTribe, WriteOrder).
        private const int TribeBytes = 17, OrderBytes = 23;
        // Entries of a loaded record until the first simulation tick applies them (null: none).
        private IReadOnlyDictionary<string, byte[]> loadedCases;
        private long casesRestored, casesEnded, casesDropped, casesUnreadable;

        // Capture on the Extender's save thread, like the Fast queue (CaptureFastSave): a save of a just
        // loaded, still paused game writes the loaded entries back unchanged.
        private void CaptureMovementCases(Action<string, byte[]> write)
        {
            IReadOnlyDictionary<string, byte[]> pending = loadedCases;
            if (pending != null)
            {
                foreach (string name in CaseEntries)
                    if (pending.TryGetValue(name, out byte[] data)) write(name, data);
                return;
            }
            write(CaseEscapes, Encode(w =>
            {
                w.Write(fireEscapes.Count);
                foreach (KeyValuePair<int, FireEscapeEntry> pair in fireEscapes)
                {
                    FireEscapeEntry e = pair.Value;
                    w.Write(pair.Key); w.Write(e.Global); w.Write(e.Player); w.Write(e.MoveX); w.Write(e.MoveY); w.Write(e.ReturnX); w.Write(e.ReturnY);
                    w.Write(e.Until); w.Write(e.NextProbe); w.Write((byte)e.Kind); w.Write((byte)e.Hazard); w.Write((byte)((e.Safe ? 1 : 0) | (e.Returning ? 2 : 0)));
                    WriteTribe(w, e.Tribe);
                }
            }));
            write(CaseEscapeRetry, Encode(w =>
            {
                var ids = new List<int>(fireEscapeRetry.Keys); ids.Sort();
                w.Write(ids.Count);
                foreach (int id in ids) { w.Write(id); w.Write(fireEscapeRetry[id]); }
            }));
            write(CaseSolos, Encode(w =>
            {
                w.Write(replanSolos.Count);
                foreach (KeyValuePair<int, ReplanSolo> pair in replanSolos)
                { w.Write(pair.Key); w.Write(pair.Value.Global); w.Write(pair.Value.MoveX); w.Write(pair.Value.MoveY); WriteTribe(w, pair.Value.Tribe); }
            }));
            write(CaseWaits, Encode(w =>
            {
                w.Write(replanWaits.Count);
                foreach (KeyValuePair<int, ReplanWait> pair in replanWaits)
                {
                    ReplanWait r = pair.Value;
                    byte policy = r.Policy != null && ReferenceEquals(r.Policy, fireAbortPolicy) ? CasePolicyFire :
                        r.Policy != null && ReferenceEquals(r.Policy, plagueAbortPolicy) ? CasePolicyPlague : CasePolicyOther;
                    w.Write(pair.Key); w.Write(r.Global); w.Write(r.Player); w.Write(r.TargetX); w.Write(r.TargetY); w.Write(r.StopX); w.Write(r.StopY);
                    w.Write(r.Remaining); w.Write(r.Until); w.Write(r.NextProbe); w.Write(policy); w.Write(r.Tribe); WriteOrder(w, r.Order);
                }
            }));
            write(CaseSettled, Encode(w =>
            {
                var ids = new List<int>(replanSettled.Keys); ids.Sort();
                w.Write(ids.Count);
                foreach (int id in ids) { ReplanSettled s = replanSettled[id]; w.Write(id); w.Write(s.Global); w.Write(s.TargetX); w.Write(s.TargetY); }
            }));
            write(CaseOrders, Encode(w =>
            {
                var ids = new List<int>(tribeOrders.Keys); ids.Sort();
                w.Write(tribeOrderSequence); w.Write(ids.Count);
                foreach (int id in ids) { w.Write(id); WriteOrder(w, tribeOrders[id]); }
            }));
        }

        // Loaded callback of the record: entries of a restored record, or null for any other load result.
        private void StageMovementCases(IReadOnlyDictionary<string, byte[]> entries) => loadedCases = entries;

        private void ApplyLoadedMovementCases()
        {
            IReadOnlyDictionary<string, byte[]> entries = loadedCases;
            loadedCases = null;
            if (entries != null) RestoreMovementCases(entries);
        }

        // Orders first: restored waits compare their order with the tribe's last one. A solo case may
        // own the temporary tribe that a nested escape borrows (Split=false). Restore both layers:
        // ProcessReplanSolos defers its rejoin until that escape ends, exactly as without a load.
        private void RestoreMovementCases(IReadOnlyDictionary<string, byte[]> entries)
        {
            long restored = casesRestored, ended = casesEnded, dropped = casesDropped, unreadable = casesUnreadable;
            Decode(entries, CaseOrders, 4 + OrderBytes, r =>
            {
                int tribeId = r.ReadInt32(); TribeOrder order = ReadOrder(r);
                return () =>
                {
                    // An order observed since the load is newer; a reused slot has another global id.
                    if (!tribeOrders.ContainsKey(tribeId) && tribeId > 0 && tribeId < MaximumTribeCount &&
                        GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) && tribe != null && tribe->r_GlobalId == order.Global)
                        tribeOrders[tribeId] = order;
                };
            }, r => { int sequence = r.ReadInt32(); r.ReadInt32(); return () => tribeOrderSequence = Math.Max(tribeOrderSequence, sequence); }, 8);
            Decode(entries, CaseSettled, 16, r =>
            {
                int id = r.ReadInt32(); var settled = new ReplanSettled { Global = r.ReadUInt32(), TargetX = r.ReadInt32(), TargetY = r.ReadInt32() };
                return () => { if (TryGetSoldier(id, settled.Global, out _)) replanSettled[id] = settled; };
            });
            Decode(entries, CaseEscapeRetry, 8, r => { int id = r.ReadInt32(), retry = r.ReadInt32(); return () => fireEscapeRetry[id] = retry; });
            Decode(entries, CaseEscapes, 39 + TribeBytes, r =>
            {
                int id = r.ReadInt32();
                var e = new FireEscapeEntry { Global = r.ReadUInt32(), Player = r.ReadInt32(), MoveX = r.ReadInt32(), MoveY = r.ReadInt32(),
                    ReturnX = r.ReadInt32(), ReturnY = r.ReadInt32(), Until = r.ReadInt32(), NextProbe = r.ReadInt32() };
                byte kind = r.ReadByte(), hazard = r.ReadByte(), flags = r.ReadByte();
                e.Tribe = ReadTribe(r);
                if (kind > (byte)FireReturnKind.MoveTarget || hazard > (byte)EscapeHazard.Plague || flags > 3) throw new InvalidDataException("escape case values");
                e.Kind = (FireReturnKind)kind; e.Hazard = (EscapeHazard)hazard; e.Safe = (flags & 1) != 0; e.Returning = (flags & 2) != 0;
                return () =>
                {
                    bool enabled = AllowsAutomaticHazardControl(e.Player) &&
                        (e.Hazard == EscapeHazard.Plague ? PlagueEscapeEnabled : FireEscapeEnabled);
                    if (enabled && !fireEscapes.ContainsKey(id) && CanContinueInOwnTribe(id, e.Global, e.Player, e.Tribe)) { fireEscapes[id] = e; casesRestored++; }
                    else EndRestoredCase(id, e.Global, e.Tribe);
                };
            });
            // Solo walkers and waits are processed by the replan watch only (ReplanTick).
            bool replanning = settings.AnchorSingleMoves && anchorAvailable;
            Decode(entries, CaseSolos, 16 + TribeBytes, r =>
            {
                int id = r.ReadInt32();
                var solo = new ReplanSolo { Global = r.ReadUInt32(), MoveX = r.ReadInt32(), MoveY = r.ReadInt32(), Tribe = ReadTribe(r) };
                return () =>
                {
                    // Escaping does not require AnchorSingleMoves. If its outer solo watch
                    // is now disabled, hand that tribe's ownership to the borrowed escape:
                    // keeping an unprocessed solo or rejoining here would orphan/end it.
                    if (!replanning && solo.Tribe.Split && fireEscapes.TryGetValue(id, out FireEscapeEntry escape) &&
                        escape.Global == solo.Global && !escape.Tribe.Split && escape.Tribe.Own == solo.Tribe.Own &&
                        escape.Tribe.OwnGlobal == solo.Tribe.OwnGlobal &&
                        CanContinueInOwnTribe(id, solo.Global, escape.Player, solo.Tribe))
                    { escape.Tribe = solo.Tribe; casesRestored++; return; }
                    if (replanning && !replanSolos.ContainsKey(id) &&
                        TryGetSoldier(id, solo.Global, out GameUnit* unit) && CanContinueInOwnTribe(id, solo.Global, unit->r_ControllableForPlayerId, solo.Tribe))
                    { replanSolos[id] = solo; casesRestored++; }
                    else EndRestoredCase(id, solo.Global, solo.Tribe);
                };
            });
            Decode(entries, CaseWaits, 45 + OrderBytes, r =>
            {
                int id = r.ReadInt32();
                var wait = new ReplanWait { Global = r.ReadUInt32(), Player = r.ReadInt32(), TargetX = r.ReadInt32(), TargetY = r.ReadInt32(),
                    StopX = r.ReadInt32(), StopY = r.ReadInt32(), Remaining = r.ReadInt32(), Until = r.ReadInt32(), NextProbe = r.ReadInt32() };
                byte policy = r.ReadByte();
                wait.Tribe = r.ReadInt32(); wait.Order = ReadOrder(r);
                if (policy > CasePolicyPlague) throw new InvalidDataException("wait policy");
                return () =>
                {
                    wait.Policy = policy == CasePolicyFire ? fireAbortPolicy : policy == CasePolicyPlague ? plagueAbortPolicy : null;
                    // A dropped wait leaves the soldier stopped where it waited (like an aborted fire wait), in its own group.
                    if (replanning && wait.Policy != null && AllowsAutomaticHazardControl(wait.Player) &&
                        !fireEscapes.ContainsKey(id) && !replanWaits.ContainsKey(id) && TryGetSoldier(id, wait.Global, out _))
                    { replanWaits[id] = wait; casesRestored++; }
                    else casesDropped++;
                };
            });
            Shared.DebugLogHelper.LogInfo(log, $"Pathfinding movement cases loaded; restored={casesRestored - restored} ended={casesEnded - ended} dropped={casesDropped - dropped} " +
                $"unreadableEntries={casesUnreadable - unreadable} escapes={fireEscapes.Count} solos={replanSolos.Count} waits={replanWaits.Count} orders={tribeOrders.Count}.");
        }

        // The soldier is still in the own tribe of its case, and that tribe is still the one the case used.
        private bool CanContinueInOwnTribe(int id, uint global, int player, SoldierTribe tribe) =>
            TryGetSoldier(id, global, out GameUnit* unit) && unit->r_TribeId == tribe.Own && unit->r_ControllableForPlayerId == player &&
            TryGetLiveTribe(tribe.Own, tribe.OwnGlobal, player, out _);

        // As at the end of a running case: back into the original group if it still exists (an AI soldier
        // without one leaves its own tribe for Vanilla's repair); an emptied own tribe is deleted.
        private void EndRestoredCase(int id, uint global, SoldierTribe tribe)
        {
            if (TryGetSoldier(id, global, out _)) casesEnded++; else casesDropped++;
            RejoinSoldier(id, global, tribe);
        }

        private static void WriteTribe(BinaryWriter w, SoldierTribe tribe)
        { w.Write(tribe.Own); w.Write(tribe.OwnGlobal); w.Write(tribe.Original); w.Write(tribe.OriginalGlobal); w.Write(tribe.Split); }

        private static SoldierTribe ReadTribe(BinaryReader r) =>
            new SoldierTribe { Own = r.ReadInt32(), OwnGlobal = r.ReadUInt32(), Original = r.ReadInt32(), OriginalGlobal = r.ReadUInt32(), Split = r.ReadBoolean() };

        private static void WriteOrder(BinaryWriter w, TribeOrder order)
        {
            w.Write(order.Global); w.Write(order.Sequence); w.Write(order.X); w.Write(order.Y); w.Write(order.Patrol);
            w.Write((byte)((order.NewOrder ? 1 : 0) | (order.Move ? 2 : 0) | (order.Plain ? 4 : 0) | (order.Queued ? 8 : 0)));
            w.Write((int)order.MoveType);
        }

        private static TribeOrder ReadOrder(BinaryReader r)
        {
            var order = new TribeOrder { Global = r.ReadUInt32(), Sequence = r.ReadInt32(), X = r.ReadInt32(), Y = r.ReadInt32(), Patrol = r.ReadInt16() };
            byte flags = r.ReadByte();
            if (flags > 15) throw new InvalidDataException("order flags");
            order.NewOrder = (flags & 1) != 0; order.Move = (flags & 2) != 0; order.Plain = (flags & 4) != 0; order.Queued = (flags & 8) != 0;
            order.MoveType = (TribeMoveType)r.ReadInt32();
            return order;
        }

        // BinaryWriter writes little-endian on every platform.
        private static byte[] Encode(Action<BinaryWriter> body)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream)) body(writer);
                return stream.ToArray();
            }
        }

        // One entry: a header of headerBytes whose last four bytes are the record count, then the records of
        // recordBytes each. The whole entry is read before anything is applied: a damaged entry changes nothing.
        private void Decode(IReadOnlyDictionary<string, byte[]> entries, string name, int recordBytes, Func<BinaryReader, Action> record,
            Func<BinaryReader, Action> header = null, int headerBytes = 4)
        {
            if (!entries.TryGetValue(name, out byte[] data) || data == null) return;
            var apply = new List<Action>();
            try
            {
                if (data.Length < headerBytes) throw new InvalidDataException("short entry");
                int count = BitConverter.ToInt32(data, headerBytes - 4);
                if (count < 0 || (long)count * recordBytes != data.Length - headerBytes) throw new InvalidDataException("entry length");
                using (var reader = new BinaryReader(new MemoryStream(data, false)))
                {
                    if (header != null) apply.Add(header(reader)); else reader.ReadInt32();
                    for (int i = 0; i < count; i++) apply.Add(record(reader));
                }
            }
            catch (Exception ex) when (ex is InvalidDataException || ex is EndOfStreamException)
            {
                casesUnreadable++;
                Shared.DebugLogHelper.LogError(log, $"Pathfinding saved movement cases '{name}' unreadable ({ex.Message}); its records are not restored.");
                return;
            }
            foreach (Action step in apply) step();
        }

        private string FormatMovementCaseSave() =>
            $"savedCases(restored={casesRestored} ended={casesEnded} dropped={casesDropped} unreadable={casesUnreadable})";
    }
}
