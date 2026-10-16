using System;
using Iced.Intel;
namespace AIBuildDiagnoseTest
{
    internal sealed class EconomyHookContract
    {
        internal readonly int Rva, Length;
        internal readonly byte[] Prefix;
        private EconomyHookContract(int rva, int length, string hex)
        {
            Rva = rva; Length = length; Prefix = new byte[hex.Length / 2];
            for (int i = 0; i < Prefix.Length; i++) Prefix[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        internal static readonly EconomyHookContract[] All = {
            new EconomyHookContract(0x29650, 10, "48895C240848896C2410488974241848897C242041564883EC304863FA418BD9"),
            new EconomyHookContract(0x29700, 9, "4585C90F889600000048895C24104889742418574883EC304863FA418BD94889"),
            new EconomyHookContract(0x2d810, 6, "4055565741564883EC28418BF8BE01000000448B05D7F34906410FB6E9448BF2"),
            new EconomyHookContract(0x2dba0, 10, "48895C240848896C24104889742418574883EC204863C2488D2D52E663034869"),
            new EconomyHookContract(0x2e1e0, 10, "48895C240848896C24104889742418574883EC20833D75724D0300488BD97E6A"),
            new EconomyHookContract(0x3b360, 10, "48895C24084889742410574883EC204863FA488D05970E63034869CF3C580000"),
            new EconomyHookContract(0x3ec90, 6, "40564154415541574883EC284C63FA4C8D156AD562034969F73C5800004C8BE1"),
            new EconomyHookContract(0x3f270, 6, "4533C04863C24C69C83C5800004C8D158CCF62034789841128161300438B8411"),
            new EconomyHookContract(0x40740, 12, "40555641564881EC60010000488B05557C2F004833C448898424300100004C63"),
            new EconomyHookContract(0xcc420, 10, "48895C242044894424188954241048894C240855565741544155415641574883"),
            new EconomyHookContract(0xCCBB0, 10, "48895C240848896C2410488974241848897C242041564883EC20488BF1488D2D"),
        };
        internal static EconomyHookContract Find(int rva)
        {
            foreach (var c in All) if (c.Rva == rva) return c;
            throw new ArgumentOutOfRangeException(nameof(rva));
        }
        internal void ValidateBytes(ReadOnlySpan<byte> image)
        {
            if (Rva < 0 || Rva + Prefix.Length > image.Length) throw new InvalidOperationException("Native entry outside image");
            if (!image.Slice(Rva, Prefix.Length).SequenceEqual(Prefix))
            {
                // Same-hash executable-section fallback. Never follow an unknown detour.
                int pe = BitConverter.ToInt32(image.Slice(0x3C, 4).ToArray(), 0);
                int count = BitConverter.ToUInt16(image.Slice(pe + 6, 2).ToArray(), 0);
                int opt = BitConverter.ToUInt16(image.Slice(pe + 20, 2).ToArray(), 0);
                int match = -1;
                for (int i = 0; i < count; i++)
                {
                    int s = pe + 24 + opt + i * 40;
                    uint flags = BitConverter.ToUInt32(image.Slice(s + 36, 4).ToArray(), 0);
                    if ((flags & 0x20000000) == 0) continue;
                    int start = BitConverter.ToInt32(image.Slice(s + 12, 4).ToArray(), 0);
                    int size = BitConverter.ToInt32(image.Slice(s + 8, 4).ToArray(), 0);
                    for (int j = start; j <= start + size - Prefix.Length; j++)
                        if (image.Slice(j, Prefix.Length).SequenceEqual(Prefix))
                        { if (match != -1) throw new InvalidOperationException("Ambiguous entry pattern"); match = j; }
                }
                if (match != Rva) throw new InvalidOperationException("Entry occupied/changed; no validated same-image fallback");
            }
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(Prefix));
            decoder.IP = 0x180000000UL + (uint)Rva;
            int span = 0;
            while (span < 6)
            {
                var instruction = decoder.Decode();
                if (instruction.IsInvalid || decoder.LastError != DecoderError.None) throw new InvalidOperationException("Invalid entry instruction");
                span += instruction.Length;
            }
            if (span != Length) throw new InvalidOperationException("Audited indirect span changed");
        }
    }
}
