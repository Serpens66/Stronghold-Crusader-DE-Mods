namespace ExtendedData.Core
{
    public static class CoopTrailMissionRange
    {
        public static bool Contains(int missionCount, int zeroBasedTrail, int oneBasedMission)
        {
            return missionCount >= 1 && missionCount <= 40 &&
                zeroBasedTrail >= 0 && zeroBasedTrail < 4 &&
                oneBasedMission >= 1 && oneBasedMission <= 10 &&
                zeroBasedTrail * 10 + oneBasedMission <= missionCount;
        }

        public static int CountOnPage(int missionCount, int zeroBasedTrail)
        {
            if (missionCount < 1 || missionCount > 40 || zeroBasedTrail < 0 || zeroBasedTrail >= 4)
                return 0;
            int remaining = missionCount - zeroBasedTrail * 10;
            return remaining <= 0 ? 0 : remaining >= 10 ? 10 : remaining;
        }

        public static bool IsFinalPage(int missionCount, int zeroBasedTrail)
        {
            return CountOnPage(missionCount, zeroBasedTrail) > 0 &&
                missionCount <= (zeroBasedTrail + 1) * 10;
        }
    }
}
