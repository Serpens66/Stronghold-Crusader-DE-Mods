using Fixes.Components;
using MessagePack;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace ExtenderFixesIssueRepros
{
    internal sealed unsafe class AivSaveProbe
    {
        private const int EntriesPerSlot = 40000;
        private const int BytesPerSlot = EntriesPerSlot * sizeof(uint);
        private const int FormatVersion = 1;
        private const string SaveIdentifier = "serpens66-aiv-buffer-repro-v1";

        private readonly List<Snapshot> expected = new List<Snapshot>();
        private bool loadedProbeData;
        private bool compareOnNextTick;

        private sealed class Snapshot
        {
            internal int PlayerId;
            internal int Slot;
            internal byte[] Hash;
            internal bool DistinctFromSlotZero;
        }

        internal void Register()
        {
            if (!ModSaveDataAPI.Instance.RegisterModDataHandler(SaveIdentifier, Save, Load, ClearAfterMapUnload))
                IssueReprosPlugin.Log.LogError("[FIXES-AIV] Probe save handler registration failed.");
        }

        private void ClearAfterMapUnload()
        {
            expected.Clear();
            loadedProbeData = false;
            compareOnNextTick = false;
        }

        private byte[] Save(SHCDESE.API.Components.SaveData.SaveContext context)
        {
            if (!context.IsSaveFile)
                return null;
            if (!TryCapture(out List<Snapshot> snapshots, out string reason))
            {
                IssueReprosPlugin.Log.LogWarning("[FIXES-AIV] INCONCLUSIVE at save: " + reason);
                return null;
            }
            CompareFixesSerializedData(snapshots);

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(FormatVersion);
                writer.Write(snapshots.Count);
                foreach (Snapshot snapshot in snapshots)
                {
                    writer.Write(snapshot.PlayerId);
                    writer.Write(snapshot.Slot);
                    writer.Write(snapshot.DistinctFromSlotZero);
                    writer.Write(snapshot.Hash);
                    IssueReprosPlugin.Log.LogInfo($"[FIXES-AIV] save player={snapshot.PlayerId} physicalSlot={snapshot.Slot} hash={Hex(snapshot.Hash)} distinctFromSlot0={snapshot.DistinctFromSlotZero}");
                }
                if (!snapshots.Any(s => s.DistinctFromSlotZero))
                    IssueReprosPlugin.Log.LogWarning("[FIXES-AIV] INCONCLUSIVE: no active slot differs from slot zero; let AI build more before saving.");
                return stream.ToArray();
            }
        }

        private static void CompareFixesSerializedData(List<Snapshot> snapshots)
        {
            try
            {
                const string fixesFile = "_SE_ModData_extendedaivbuffer-savedata.msgpack";
                byte[] payload = GameMapArchiveManagerAPI.Instance.TryReadBinaryFile(fixesFile, true);
                if (payload == null || payload.Length == 0)
                {
                    IssueReprosPlugin.Log.LogWarning("[FIXES-AIV] INCONCLUSIVE: Fixes archive entry not yet available at probe save callback.");
                    return;
                }
                ExtendedAIVBufferContainer container = MessagePackSerializer.Deserialize<ExtendedAIVBufferContainer>(payload);
                if (container?.buffers == null)
                {
                    IssueReprosPlugin.Log.LogWarning("[FIXES-AIV] INCONCLUSIVE: decoded Fixes archive has no buffers.");
                    return;
                }
                if (!FixesInspection.TryGetAivBuffer(out IntPtr address, out string reason))
                {
                    IssueReprosPlugin.Log.LogWarning("[FIXES-AIV] INCONCLUSIVE: Fixes archive could not be compared: " + reason);
                    return;
                }
                byte[] zeroHash = HashSlot(address, 0);
                foreach (Snapshot snapshot in snapshots)
                {
                    if (!container.buffers.TryGetValue(snapshot.PlayerId, out ExtendedAIVBuffer saved) ||
                        saved?.buffer == null || saved.buffer.Length != EntriesPerSlot)
                    {
                        IssueReprosPlugin.Log.LogWarning($"[FIXES-AIV] player={snapshot.PlayerId} INCONCLUSIVE: serialized Fixes entry missing or wrong length.");
                        continue;
                    }
                    byte[] packed = new byte[BytesPerSlot];
                    Buffer.BlockCopy(saved.buffer, 0, packed, 0, packed.Length);
                    byte[] serializedHash;
                    using (SHA256 sha = SHA256.Create())
                        serializedHash = sha.ComputeHash(packed);
                    bool matchesZero = serializedHash.SequenceEqual(zeroHash);
                    bool matchesActive = serializedHash.SequenceEqual(snapshot.Hash);
                    string result = snapshot.DistinctFromSlotZero && matchesZero && !matchesActive
                        ? "REPRODUCED: Fixes serialized slot zero instead of the active slot"
                        : matchesActive ? "MATCH" : "INCONCLUSIVE: unexpected serialized data";
                    IssueReprosPlugin.Log.LogInfo($"[FIXES-AIV] archive player={snapshot.PlayerId} physicalSlot={snapshot.Slot} activeHash={Hex(snapshot.Hash)} slot0Hash={Hex(zeroHash)} serializedHash={Hex(serializedHash)} result={result}");
                }
            }
            catch (Exception ex)
            {
                IssueReprosPlugin.Log.LogWarning("[FIXES-AIV] INCONCLUSIVE: could not inspect Fixes archive entry: " + ex);
            }
        }

        private void Load(byte[] bytes, SHCDESE.API.Components.SaveData.LoadContext context)
        {
            expected.Clear();
            loadedProbeData = false;
            compareOnNextTick = false;
            if (!context.IsSaveFile)
                return;
            using (MemoryStream stream = new MemoryStream(bytes, false))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                if (reader.ReadInt32() != FormatVersion)
                    throw new InvalidDataException("Unexpected AIV probe format.");
                int count = reader.ReadInt32();
                if (count < 0 || count > 8)
                    throw new InvalidDataException("Invalid AIV probe record count.");
                for (int i = 0; i < count; i++)
                {
                    int playerId = reader.ReadInt32();
                    int slot = reader.ReadInt32();
                    bool distinct = reader.ReadBoolean();
                    byte[] hash = reader.ReadBytes(32);
                    if (playerId < 1 || playerId > 8 || slot < 1 || slot > 8 || hash.Length != 32)
                        throw new InvalidDataException("Invalid AIV probe record.");
                    expected.Add(new Snapshot { PlayerId = playerId, Slot = slot, DistinctFromSlotZero = distinct, Hash = hash });
                }
                if (stream.Position != stream.Length)
                    throw new InvalidDataException("Trailing AIV probe data.");
            }
            loadedProbeData = true;
            compareOnNextTick = true;
            IssueReprosPlugin.Log.LogInfo($"[FIXES-AIV] loaded {expected.Count} independent saved checksums; comparing now and on the first game tick after load.");
            CompareAfterLoad();
        }

        internal void CompareOnceAfterLoad()
        {
            if (!compareOnNextTick)
                return;
            compareOnNextTick = false;
            CompareAfterLoad();
        }

        private void CompareAfterLoad()
        {
            if (!loadedProbeData)
            {
                IssueReprosPlugin.Log.LogWarning("[FIXES-AIV] INCONCLUSIVE: no probe snapshot was loaded from this save.");
                return;
            }
            if (!TryCapture(out List<Snapshot> current, out string reason))
            {
                IssueReprosPlugin.Log.LogWarning("[FIXES-AIV] INCONCLUSIVE after load: " + reason);
                return;
            }
            foreach (Snapshot saved in expected)
            {
                Snapshot now = current.FirstOrDefault(s => s.PlayerId == saved.PlayerId);
                if (now == null || now.Slot != saved.Slot)
                {
                    IssueReprosPlugin.Log.LogWarning($"[FIXES-AIV] player={saved.PlayerId} INCONCLUSIVE: village slot changed or disappeared.");
                    continue;
                }
                bool match = saved.Hash.SequenceEqual(now.Hash);
                string result = match ? "MATCH" : saved.DistinctFromSlotZero ? "MISMATCH: check callback timing and archived buffer result" : "INCONCLUSIVE: original section matched slot zero";
                IssueReprosPlugin.Log.LogInfo($"[FIXES-AIV] player={saved.PlayerId} physicalSlot={saved.Slot} savedHash={Hex(saved.Hash)} loadedHash={Hex(now.Hash)} result={result}");
            }
        }

        private static bool TryCapture(out List<Snapshot> snapshots, out string reason)
        {
            snapshots = new List<Snapshot>();
            if (!FixesInspection.TryGetAivBuffer(out IntPtr address, out reason))
                return false;
            GameAIVManagerAPI api = GameAIVManagerAPI.Instance;
            if (!api.TryGetVillageBySlot(0, out AivVillageState* zero) || zero == null)
            {
                reason = "native village array unavailable";
                return false;
            }

            byte[] zeroHash = HashSlot(address, 0);
            for (int playerId = 1; playerId <= 8; playerId++)
            {
                if (!api.TryGetVillageByPlayerId(playerId, out AivVillageState* village) || village == null)
                    continue;
                long slot = village - zero;
                if (slot < 1 || slot > 8)
                {
                    reason = "player-to-village pointer was outside live slots";
                    snapshots.Clear();
                    return false;
                }
                byte[] hash = HashSlot(address, (int)slot);
                snapshots.Add(new Snapshot { PlayerId = playerId, Slot = (int)slot, Hash = hash, DistinctFromSlotZero = !hash.SequenceEqual(zeroHash) });
            }
            reason = string.Empty;
            return true;
        }

        private static byte[] HashSlot(IntPtr baseAddress, int slot)
        {
            byte[] bytes = new byte[BytesPerSlot];
            Marshal.Copy(IntPtr.Add(baseAddress, checked(slot * BytesPerSlot)), bytes, 0, bytes.Length);
            using (SHA256 sha = SHA256.Create())
                return sha.ComputeHash(bytes);
        }

        private static string Hex(byte[] hash)
        {
            return BitConverter.ToString(hash).Replace("-", string.Empty);
        }
    }
}
