namespace TestExtender
{
    public sealed class LordInfo
    {
        public string LocalizedDisplayName { get; set; } = string.Empty;
        public string Messages { get; set; } = string.Empty;
        public string FutureMetadata { get; set; } = string.Empty;
    }

    public enum AILordMessageType
    {
        DefeatedAgain = 16,
        AllyNotificationCongratulations = 17,
        FutureMessage = 34
    }
}
