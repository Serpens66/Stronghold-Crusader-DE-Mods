using Iced.Intel;
using RedBird.Backends.NativeX64;
using System;
using System.Runtime.InteropServices;

namespace Shared
{
    // Private memory image for copied-entry RedBird NativeX64 probes.
    // RedBird NativeX64 1.5.0.0 (PrologueAnalyzer.Disassemble) decodes a detour target through a
    // raw pointer up to Options.FunctionScanLimit bytes (16 KiB by default) until a function end
    // beyond its furthest forward branch. A probe image therefore spans that whole window: a short
    // heap copy let the analyser read neighbouring memory and fault when the copy ended in front of
    // an inaccessible page (Mono: NullReferenceException in MemoryCodeReader.ReadByte), which
    // disabled the audited hooks on random starts.
    internal static class CopiedProbeImage
    {
        internal const int SourceLength = 64;
        private const int MaximumScanLimit = 1 << 20;

        // options: the options of the backend that runs the probe (same as the real installation).
        internal static byte[] Create(ulong entry, NativeDetourOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            var source = new byte[SourceLength];
            Marshal.Copy(unchecked((IntPtr)(long)entry), source, 0, source.Length);
            return Build(source, options.FunctionScanLimit);
        }

        internal static byte[] Build(byte[] source, int scanLimit)
        {
            if (source == null || source.Length != SourceLength)
                throw new ArgumentException("Probe source must hold " + SourceLength + " bytes.", nameof(source));
            if (scanLimit < SourceLength || scanLimit > MaximumScanLimit)
                throw new InvalidOperationException("Unexpected RedBird function scan limit: " + scanLimit);
            // Keep only complete instructions so no truncated instruction merges with the padding.
            Decoder decoder = Decoder.Create(64, new ByteArrayCodeReader(source));
            int complete = 0;
            bool first = true;
            while (true)
            {
                Instruction instruction = decoder.Decode();
                if (decoder.LastError != DecoderError.None || instruction.IsInvalid) break;
                // RedBird follows an entry branch (FollowJumps) relative to the copy, outside the image.
                if (first && instruction.FlowControl != FlowControl.Next)
                    throw new InvalidOperationException("Probe entry must start with a non-branch instruction.");
                first = false;
                complete += instruction.Length;
            }
            if (complete == 0) throw new InvalidOperationException("Probe entry contains no decodable instruction.");
            var image = new byte[scanLimit];
            Buffer.BlockCopy(source, 0, image, 0, complete);
            for (int index = complete; index < image.Length; index++)
                image[index] = 0xCC; // int3 ends RedBird's scan deterministically inside the image.
            return image;
        }
    }
}
