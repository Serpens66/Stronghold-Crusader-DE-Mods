using Iced.Intel;
using SHCDESE.API.LowLevel;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NativeOrderCall(int attackTarget, int orderedAi, int requester, int order);
        private static NativeOrderCall nativeOrder;
        private const int OrderRva = 0xD7580;
        private const int OrderLength = 587;

        private static void InitializeNativeOrders(CrusaderLibraryLoadContext context, byte[] image)
        {
            if (nativeOrder != null) return;
            try
            {
                int pe = BitConverter.ToInt32(image, 60);
                int table = pe + 24 + BitConverter.ToUInt16(image, pe + 20);
                int count = BitConverter.ToUInt16(image, pe + 6);
                int sectionRva = 0, raw = 0, size = 0;
                for (int index = 0; index < count; index++)
                {
                    int header = table + index * 40;
                    int rva = BitConverter.ToInt32(image, header + 12);
                    int length = BitConverter.ToInt32(image, header + 16);
                    if (OrderRva >= rva && OrderRva + OrderLength <= (long)rva + length &&
                        (BitConverter.ToUInt32(image, header + 36) & 0x20000000) != 0)
                    { sectionRva = rva; raw = BitConverter.ToInt32(image, header + 20); size = length; break; }
                }
                if (size <= 0 || raw < 0 || (long)raw + size > image.Length) throw new InvalidDataException("Executable section missing.");
                var expected = new byte[OrderLength];
                Buffer.BlockCopy(image, raw + OrderRva - sectionRva, expected, 0, OrderLength);
                // Decode the complete reference function. No code bytes are ever patched here.
                var decoder = Decoder.Create(64, new ByteArrayCodeReader(expected));
                int decoded = 0;
                while (decoded < expected.Length)
                {
                    Instruction instruction = decoder.Decode();
                    if (instruction.IsInvalid || decoder.LastError != DecoderError.None) throw new InvalidDataException("Invalid order instruction.");
                    decoded += instruction.Length;
                }
                if (decoded != OrderLength) throw new InvalidDataException("Order function boundary changed.");
                var address = new IntPtr(context.ModuleHandle.ToInt64() + OrderRva);
                var live = new byte[OrderLength];
                Marshal.Copy(address, live, 0, live.Length);
                if (!Matches(live, 0, expected))
                    throw new InvalidDataException("Live native order function differs at the audited RVA.");
                nativeOrder = (NativeOrderCall)Marshal.GetDelegateForFunctionPointer(address, typeof(NativeOrderCall));
                log.LogInfo($"TEST_AI_ORDERS_READY method=reference-rva rva=0x{OrderRva:X} bytes={OrderLength}");
            }
            catch (Exception error) { log.LogError("TEST_AI_ORDERS_DISABLED: " + error); }
        }

        private static bool Matches(byte[] source, int offset, byte[] expected)
        {
            if (offset < 0 || offset > source.Length - expected.Length) return false;
            for (int index = 0; index < expected.Length; index++) if (source[offset + index] != expected[index]) return false;
            return true;
        }
    }
}
