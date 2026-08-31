namespace ExtremePowers.API
{
    public sealed class RegenerationAccumulator
    {
        private long remainder;
        public int ScaleDelta(int vanillaDelta, int percent)
        {
            if (vanillaDelta < 0) throw new System.ArgumentOutOfRangeException(nameof(vanillaDelta));
            if (percent < 0 || percent > 1000) throw new System.ArgumentOutOfRangeException(nameof(percent));
            long scaled = (long)vanillaDelta * percent + remainder; int result = (int)(scaled / 100); remainder = scaled % 100; return result;
        }
        public void Reset() { remainder = 0; }
    }
}
