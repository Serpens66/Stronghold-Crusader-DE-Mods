namespace ExtremePowers.UI
{
    internal sealed class ExtremePowersUiModel
    {
        internal ExtremePowersUiModel(string backendStatus) { BackendStatus = backendStatus; }
        public string BackendStatus { get; }
    }
}
