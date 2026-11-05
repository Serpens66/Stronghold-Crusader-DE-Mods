using SHCDESE.Interop;

namespace BugfixesAndQoL
{
    internal static class ForeignTroopPortraits
    {
        private static readonly int[] PortraitCodes = CreatePortraitCodes();

        internal static string GetResourceKey(int type, GM material)
        {
            // Use the unit's rendered material, independently of the local player's culture.
            if (type == (int)eChimps.CHIMP_TYPE_MONK && material == GM.GM_BODY_TEMPLE_GUARD)
                return "UI-Buttons K011a";
            int code = type >= 0 && type < PortraitCodes.Length ? PortraitCodes[type] : 0;
            return code == 0 ? null : "UI-Buttons K" + code.ToString("000");
        }

        private static int[] CreatePortraitCodes()
        {
            int[] codes = new int[89];
            int[] pairs = {
                22,23, 24,1, 26,3, 23,5, 25,25, 27,7, 28,27, 30,9, 29,29, 5,31,
                39,13, 40,15, 59,17, 58,19, 60,33, 41,21, 61,35, 70,37, 71,39, 72,41,
                73,43, 74,45, 75,47, 76,49, 77,51, 78,53, 79,55, 80,57, 81,59, 82,61,
                83,63, 84,65, 85,67
            };
            for (int i = 0; i < pairs.Length; i += 2) codes[pairs[i]] = pairs[i + 1];
            codes[(int)eChimps.CHIMP_TYPE_MONK] = 11;
            return codes;
        }
    }
}
