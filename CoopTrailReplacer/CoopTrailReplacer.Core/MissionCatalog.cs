using System;
using System.Collections.Generic;
using System.IO;

namespace CoopTrailReplacer.Core
{
    public sealed class MissionCatalog
    {
        private readonly Dictionary<int, LoadedMission> missions = new Dictionary<int, LoadedMission>();

        public IReadOnlyDictionary<int, LoadedMission> Missions => missions;

        public static int ToKey(int trailNumber, int missionNumber) => (trailNumber * 100) + missionNumber;

        public void Load(string coopTrailsRoot, Action<string> info, Action<string> error)
        {
            missions.Clear();
            var loader = new MissionLoader();
            for (int trail = 1; trail <= 4; trail++)
            {
                string directory = Path.Combine(coopTrailsRoot, "Trail" + trail);
                for (int mission = 1; mission <= 10; mission++)
                {
                    string path = Path.Combine(directory, mission.ToString("00") + ".coopmission.json");
                    if (!File.Exists(path))
                        continue;
                    try
                    {
                        LoadedMission loaded = loader.Load(path, trail, mission);
                        missions[ToKey(trail, mission)] = loaded;
                        info?.Invoke("Loaded Trail" + trail + "/" + mission.ToString("00") + ": " + loaded.Definition.DisplayName);
                    }
                    catch (Exception ex)
                    {
                        // A broken replacement never removes the corresponding Vanilla mission.
                        error?.Invoke("Ignored Trail" + trail + "/" + mission.ToString("00") + ": " + ex.Message);
                    }
                }
            }
        }

        public bool TryGet(int zeroBasedTrailId, int oneBasedMissionId, out LoadedMission mission) =>
            missions.TryGetValue(ToKey(zeroBasedTrailId + 1, oneBasedMissionId), out mission);
    }
}
