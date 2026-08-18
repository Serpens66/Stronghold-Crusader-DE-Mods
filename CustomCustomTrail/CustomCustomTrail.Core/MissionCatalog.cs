using System;
using System.Collections.Generic;
using System.IO;

namespace CustomCustomTrail.Core
{
    public sealed class MissionCatalog
    {
        private readonly Dictionary<int, LoadedMission> missions = new Dictionary<int, LoadedMission>();

        public IReadOnlyDictionary<int, LoadedMission> Missions => missions;

        public static int ToKey(int trailNumber, int missionNumber) => (trailNumber * 100) + missionNumber;

        public void Load(CoopTrailPackage package, Action<string> info, Action<string> error)
        {
            missions.Clear();
            if (package == null)
                return;
            foreach (LoadedMission loaded in package.Missions)
            {
                missions[ToKey(loaded.TrailNumber, loaded.MissionNumber)] = loaded;
                info?.Invoke("Loaded Trail" + loaded.TrailNumber + "/" + loaded.MissionNumber.ToString("00") + ": " + loaded.Definition.DisplayName);
            }
        }

        public bool TryGet(int zeroBasedTrailId, int oneBasedMissionId, out LoadedMission mission) =>
            missions.TryGetValue(ToKey(zeroBasedTrailId + 1, oneBasedMissionId), out mission);
    }
}
