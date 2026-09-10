namespace MapParser.Core
{
    internal static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        public static uint Compute(byte[] data)
        {
            uint crc = 0xffffffffu;
            for (int index = 0; index < data.Length; index++)
                crc = Table[(int)((crc ^ data[index]) & 0xff)] ^ (crc >> 8);
            return crc ^ 0xffffffffu;
        }

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint index = 0; index < table.Length; index++)
            {
                uint value = index;
                for (int bit = 0; bit < 8; bit++)
                    value = (value & 1) != 0
                        ? 0xedb88320u ^ (value >> 1)
                        : value >> 1;
                table[index] = value;
            }
            return table;
        }
    }
}
