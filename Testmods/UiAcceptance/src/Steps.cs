namespace UiAcceptance
{
    internal static class Steps
    {
        internal static void RegisterAll()
        {
            GameSteps.Register();
            SpectatorSteps.Register();
            InputSteps.Register();
            ProbeSteps.Register();
            LobbySteps.Register();
        }
    }
}
