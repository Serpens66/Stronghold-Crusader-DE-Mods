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
        public bool TryScaleConfirmedIncrement(uint before, uint after, int percent, uint cap, out uint adjusted)
        {
            adjusted = after;
            if (after == before) return true;
            if (before == uint.MaxValue || after != before + 1) return false;
            int desiredIncrease = ScaleDelta(1, percent);
            ulong value = (ulong)before + (uint)desiredIncrease;
            adjusted = value > cap ? cap : (uint)value;
            return true;
        }
        public void Reset() { remainder = 0; }
    }
}
