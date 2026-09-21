using ExtendedData.Core;
using CrusaderDE;
using System.Collections.Generic;

namespace ExtendedData
{
    internal sealed class ResolvedMission
    {
        public LoadedMission Loaded { get; set; }
        public FileHeader Header { get; set; }
        public FRONT_Multiplayer.CoopMissionSetupData CoopData { get; set; }
        public Dictionary<int, FRONT_Multiplayer.MPAIVInfo> AiInfoByPlayerIndex { get; } = new Dictionary<int, FRONT_Multiplayer.MPAIVInfo>();
        public Dictionary<int, int> PreferredAivByPlayerIndex { get; } = new Dictionary<int, int>();
    }
}
