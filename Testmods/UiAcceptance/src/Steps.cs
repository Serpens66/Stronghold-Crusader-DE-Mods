namespace UiAcceptance
{
    internal static class Steps
    {
        internal static void RegisterAll()
        {
            GameSteps.Register();
            SpectatorSteps.Register();
            SpectatorPowerRegressionSteps.Register();
            InputSteps.Register();
            ModKeybindSteps.Register();
            ProbeSteps.Register();
            LobbySteps.Register();
            RuntimeAiSteps.Register();
            RuntimeAiUiSteps.Register();
            RelaySteps.Register();
            HuntSteps.Register();
            ArcherStartSteps.Register();
            HuntAiCastleSteps.Register();
            HuntMpSteps.Register();
            TannerSteps.Register();
            MissionReadinessSteps.Register();
            WorkerProductionSteps.Register();
            LocalAutosaveSteps.Register();
            LongMapSteps.Register();
        }
    }
}
