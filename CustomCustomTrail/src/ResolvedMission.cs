using CustomCustomTrail.Core;
using CrusaderDE;
using System.Collections.Generic;

namespace CustomCustomTrail
{
    internal sealed class ResolvedMission
    {
        public LoadedMission Loaded { get; set; }
        public FileHeader Header { get; set; }
        public FRONT_Multiplayer.CoopMissionSetupData CoopData { get; set; }
        public Dictionary<int, FRONT_Multiplayer.MPAIVInfo> AiInfoByPlayerIndex { get; } = new Dictionary<int, FRONT_Multiplayer.MPAIVInfo>();
    }
}
