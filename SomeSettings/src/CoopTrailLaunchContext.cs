using System;

namespace SomeSettings
{
    public static class CoopTrailLaunchContext
    {
        public static event Action Customized;
        private static readonly object Sync = new object();
        private static bool customizedLobby;
        private static int trailId = -1;
        private static int missionId = -1;

        public static bool IsCustomizedLobby
        {
            get { lock (Sync) return customizedLobby; }
        }

        public static void MarkCustomized(int newTrailId, int newMissionId)
        {
            lock (Sync)
            {
                customizedLobby = true;
                trailId = newTrailId;
                missionId = newMissionId;
            }

            Customized?.Invoke();
        }

        public static bool Matches(int expectedTrailId, int expectedMissionId)
        {
            lock (Sync)
                return customizedLobby && trailId == expectedTrailId && missionId == expectedMissionId;
        }

        public static void Clear()
        {
            lock (Sync)
            {
                customizedLobby = false;
                trailId = -1;
                missionId = -1;
            }
        }
    }
}
