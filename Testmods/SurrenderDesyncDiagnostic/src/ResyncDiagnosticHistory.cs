using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace SurrenderDesyncDiagnostic
{
    internal sealed class ResyncDiagnosticHistory
    {
        internal static readonly int[] CheckpointOffsets = { 0, 1, 2, 4, 8, 16, 32, 64, 96 };

        private const int BufferCapacity = 128;
        private const int PayloadPrefixLimit = 64;
        private readonly Queue<string> buffers = new Queue<string>(BufferCapacity);
        private readonly List<string> snapshots = new List<string>(CheckpointOffsets.Length);
        private long anchorGeneration = long.MinValue;
        private int anchorTick = -1;
        private int nextCheckpointIndex;
        private bool startDumpClaimed;

        internal void Reset()
        {
            buffers.Clear();
            snapshots.Clear();
            anchorGeneration = long.MinValue;
            anchorTick = -1;
            nextCheckpointIndex = 0;
            startDumpClaimed = false;
        }

        internal bool ObserveAnchor(long generation, int tick)
        {
            if (generation == anchorGeneration && tick == anchorTick)
                return false;

            anchorGeneration = generation;
            anchorTick = tick;
            nextCheckpointIndex = 0;
            snapshots.Clear();
            startDumpClaimed = false;
            return true;
        }

        internal int[] TakeDueCheckpointOffsets(int currentTick)
        {
            if (anchorTick < 0 || currentTick < anchorTick || nextCheckpointIndex >= CheckpointOffsets.Length)
                return Array.Empty<int>();

            var due = new List<int>();
            while (nextCheckpointIndex < CheckpointOffsets.Length &&
                   currentTick >= anchorTick + CheckpointOffsets[nextCheckpointIndex])
            {
                due.Add(CheckpointOffsets[nextCheckpointIndex]);
                nextCheckpointIndex++;
            }
            return due.ToArray();
        }

        internal void AddSnapshot(string snapshot)
        {
            if (!string.IsNullOrEmpty(snapshot))
                snapshots.Add(snapshot);
        }

        internal bool AddBuffer(byte[] choreBuffer, int tick, out bool containsStart, out bool containsEnd)
        {
            string description = DescribeBuffer(choreBuffer, tick, out containsStart, out containsEnd);
            if (buffers.Count == BufferCapacity)
                buffers.Dequeue();
            buffers.Enqueue(description);
            return description.IndexOf("malformed=", StringComparison.Ordinal) < 0;
        }

        internal bool TryClaimStartDump()
        {
            if (startDumpClaimed)
                return false;
            startDumpClaimed = true;
            return true;
        }

        internal string[] GetBuffers() => buffers.ToArray();

        internal string[] GetSnapshots() => snapshots.ToArray();

        internal static string DescribeBuffer(
            byte[] choreBuffer,
            int tick,
            out bool containsStart,
            out bool containsEnd)
        {
            containsStart = false;
            containsEnd = false;
            if (choreBuffer == null)
                return $"tick={tick},malformed=null";

            var records = new List<string>();
            int offset = 0;
            for (int recordIndex = 0; offset < choreBuffer.Length && recordIndex < 10000; recordIndex++)
            {
                if (choreBuffer.Length - offset >= 4 &&
                    BitConverter.ToInt32(choreBuffer, offset) == -1)
                    return choreBuffer.Length - offset == 4
                        ? $"tick={tick},records=[{string.Join(";", records)}],terminator=-1"
                        : $"tick={tick},records=[{string.Join(";", records)}],malformed=trailing-after-terminator@{offset}";

                if (choreBuffer.Length - offset < 5)
                    return $"tick={tick},records=[{string.Join(";", records)}],malformed=truncated-header@{offset}";

                int payloadLength = BitConverter.ToInt32(choreBuffer, offset);
                if (payloadLength < 1 || payloadLength > choreBuffer.Length - offset - 5)
                    return $"tick={tick},records=[{string.Join(";", records)}],malformed=length-{payloadLength}@{offset}";

                int payloadOffset = offset + 5;
                byte targetPlayerId = choreBuffer[offset + 4];
                byte opcode = choreBuffer[payloadOffset];
                containsStart |= opcode == 54;
                containsEnd |= opcode == 67;
                records.Add(
                    $"target={targetPlayerId},opcode={opcode},length={payloadLength},sha256={ComputeSha256(choreBuffer, payloadOffset, payloadLength)},payload={ToHexPrefix(choreBuffer, payloadOffset, payloadLength)}");
                offset += payloadLength + 5;
            }

            if (offset != choreBuffer.Length)
                return $"tick={tick},records=[{string.Join(";", records)}],malformed=record-limit@{offset}";
            return $"tick={tick},records=[{string.Join(";", records)}]";
        }

        internal static string ComputeSha256(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            using (SHA256 sha = SHA256.Create())
                return ToHex(sha.ComputeHash(bytes), 0, 32);
        }

        private static string ComputeSha256(byte[] bytes, int offset, int count)
        {
            using (SHA256 sha = SHA256.Create())
                return ToHex(sha.ComputeHash(bytes, offset, count), 0, 32);
        }

        private static string ToHexPrefix(byte[] bytes, int offset, int count)
        {
            int prefixLength = Math.Min(count, PayloadPrefixLimit);
            string suffix = count > prefixLength ? "..." : string.Empty;
            return ToHex(bytes, offset, prefixLength) + suffix;
        }

        private static string ToHex(byte[] bytes, int offset, int count)
        {
            var builder = new StringBuilder(count * 2);
            for (int index = 0; index < count; index++)
                builder.Append(bytes[offset + index].ToString("X2"));
            return builder.ToString();
        }
    }
}
