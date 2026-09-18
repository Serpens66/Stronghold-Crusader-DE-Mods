namespace CastlePlanner
{
    internal sealed class MultiplayerLoadingWarningGate
    {
        private int generation;

        public int BeginLoading()
        {
            generation = unchecked(generation + 1);
            return generation;
        }

        public bool IsCurrent(int candidateGeneration, bool loadingBlackVisible) =>
            loadingBlackVisible && candidateGeneration == generation;
    }
}
