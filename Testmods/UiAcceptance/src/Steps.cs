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
            AutoSendSteps.Register();
            RuntimeAiSteps.Register();
            RuntimeAiUiSteps.Register();
            RelaySteps.Register();
            HuntSteps.Register();
            HuntMpSteps.Register();
            TannerSteps.Register();
            MissionReadinessSteps.Register();
            WorkerProductionSteps.Register();
        }
    }
}
