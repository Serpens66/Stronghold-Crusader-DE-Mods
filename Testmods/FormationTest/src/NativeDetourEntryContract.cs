using Iced.Intel;
using System;
using System.Runtime.InteropServices;

namespace FormationTest
{
    internal static class NativeDetourEntryContract
    {
        internal const int SnapshotLength = 64;

        internal static byte[] Capture(ulong address)
        {
            if (address == 0)
                throw new ArgumentOutOfRangeException(nameof(address));
            var bytes = new byte[SnapshotLength];
            Marshal.Copy(unchecked((IntPtr)(long)address), bytes, 0, bytes.Length);
            return bytes;
        }

        internal static int Validate(
            byte[] entryBytes,
            byte[] vanillaPrefix,
            string scheme,
            int displacedByteCount,
            out bool chainedEntry)
        {
            if (entryBytes == null)
                throw new ArgumentNullException(nameof(entryBytes));
            if (vanillaPrefix == null)
                throw new ArgumentNullException(nameof(vanillaPrefix));
            if (entryBytes.Length == 0)
                throw new InvalidOperationException("The live detour entry snapshot is empty.");

            Decoder classifier = Decoder.Create(64, new ByteArrayCodeReader(entryBytes));
            Instruction first = classifier.Decode();
            if (classifier.LastError != DecoderError.None || first.IsInvalid)
                throw new InvalidOperationException("The live detour entry does not begin with a complete x64 instruction.");

            bool vanillaEntry = StartsWith(entryBytes, vanillaPrefix);
            chainedEntry = first.FlowControl == FlowControl.UnconditionalBranch ||
                first.FlowControl == FlowControl.IndirectBranch;
            if (!vanillaEntry && !chainedEntry)
                throw new InvalidOperationException(
                    "The live detour entry is neither the audited Vanilla prologue nor a supported hook-chain jump.");

            int minimumBytes = GetMinimumPatchSize(scheme);
            Decoder decoder = Decoder.Create(64, new ByteArrayCodeReader(entryBytes));
            int decodedBytes = 0;
            int instructionIndex = 0;
            while (decodedBytes < minimumBytes)
            {
                Instruction instruction = decoder.Decode();
                if (decoder.LastError != DecoderError.None || instruction.IsInvalid || instruction.Length <= 0)
                    throw new InvalidOperationException(
                        $"The live detour entry cannot provide {minimumBytes} complete bytes for {scheme}.");
                if (chainedEntry && instructionIndex > 0 &&
                    instruction.Mnemonic != Mnemonic.Nop && instruction.Code != Code.Int3)
                {
                    throw new InvalidOperationException(
                        "A chained detour would overwrite non-padding bytes beyond the existing entry jump.");
                }
                decodedBytes = checked(decodedBytes + instruction.Length);
                instructionIndex++;
            }

            if (displacedByteCount != decodedBytes)
            {
                throw new InvalidOperationException(
                    $"RedBird reported {displacedByteCount} displaced bytes for {scheme}; " +
                    $"the captured live entry requires exactly {decodedBytes} bytes.");
            }
            return decodedBytes;
        }

        private static int GetMinimumPatchSize(string scheme)
        {
            switch (scheme)
            {
                case "Indirect": return 6;
                case "Absolute": return 14;
                case "PushRet": return 12;
                case "PushRetRedZoneSafe": return 23;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported RedBird native detour scheme: {scheme ?? "<null>"}.");
            }
        }

        private static bool StartsWith(byte[] bytes, byte[] prefix)
        {
            if (prefix.Length == 0 || prefix.Length > bytes.Length)
                return false;
            for (int index = 0; index < prefix.Length; index++)
                if (bytes[index] != prefix[index])
                    return false;
            return true;
        }
    }
}
