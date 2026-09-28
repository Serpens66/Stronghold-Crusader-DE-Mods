namespace SpectatorPerspectiveTest
{
    // ModSaveDataAPI names archive entries with a .msgpack suffix, but accepts arbitrary bytes.
    internal static class SpectatorSaveMarker
    {
        internal const string Identifier = "SpectatorPerspectiveTest-SaveIdentity";
        internal const string EntryName = "_SE_ModData_" + Identifier + ".msgpack";

        internal static byte[] Encode(int selectedCpuView)
        {
            if (selectedCpuView < 0 || selectedCpuView > 8)
                throw new System.ArgumentOutOfRangeException("selectedCpuView");
            return new byte[] { (byte)'S', (byte)'P', (byte)'V', (byte)'M', 1,
                (byte)(selectedCpuView == 0 ? 0 : 1), (byte)selectedCpuView };
        }

        internal static bool TryDecode(byte[] bytes, out int selectedCpuView)
        {
            selectedCpuView = 0;
            if (bytes == null || bytes.Length != 7 || bytes[0] != (byte)'S' ||
                bytes[1] != (byte)'P' || bytes[2] != (byte)'V' || bytes[3] != (byte)'M' ||
                bytes[4] != 1 || bytes[5] > 1 ||
                (bytes[5] == 0 && bytes[6] != 0) ||
                (bytes[5] == 1 && (bytes[6] < 1 || bytes[6] > 8))) return false;
            selectedCpuView = bytes[6];
            return true;
        }
    }
}
