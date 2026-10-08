using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace APISharedTests
{
    public partial class RuntimeTests
    {
        [TestMethod] public void MissionLifecycleTransitions() => MissionLifecycleTests.Run(Assert);
        [TestMethod] public void SavegameSettingsRoundTrip() => SavegameModSettingsTests.Run(Assert);
        [TestMethod] public void PlayerDefeatTransitions() => PlayerDefeatTests.Run();
        [TestMethod] public void MarkedSelectionHarmonyContract() => MarkedSelectionHarmonyTests.Run(Assert);
    }
}
