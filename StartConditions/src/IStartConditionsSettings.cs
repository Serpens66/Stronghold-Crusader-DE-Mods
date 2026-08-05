using System;

namespace StartConditions
{
    public interface IStartConditionsSettings
    {
        event Action<string> SettingChanged;
        bool EnableMod { get; }
        int SetStartGoldAI { get; }
        int SetStartGoldHuman { get; }
        int AddStartGoldAI { get; }
        int AddStartGoldHuman { get; }
        int MultiplyStartTroopsAI { get; }
        int MultiplyStartTroopsHuman { get; }
        string StartGoodsAI { get; }
        string StartGoodsHuman { get; }
        string AddStartTroopsAI { get; }
        string AddStartTroopsHuman { get; }
    }
}
