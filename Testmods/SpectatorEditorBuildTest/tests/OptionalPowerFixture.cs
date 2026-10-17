namespace ExtremePowers.API
{
    public enum ExtremePowerId { Arrow, Heal, Spearmen, Engineers, Macemen, Gold, Knights, Rock }
    public enum TargetKind { None, MapPoint, Unit }
    public struct Target
    {
        public int Tile;
        public static Target None { get { return new Target { Tile = -1 }; } }
        public static Target MapPoint(int tile) { return new Target { Tile = tile }; }
    }
    public struct Context
    {
        public ExtremePowerId Power;
        public int Player, Tick;
        public Target Target;
        public ulong Operation;
        public Context(ExtremePowerId power, int player, Target target, ulong operation, int tick)
        { Power = power; Player = player; Target = target; Operation = operation; Tick = tick; }
    }
    public sealed class Replacement
    {
        public string Name { get { return "Configured spawn"; } }
        public string Tooltip { get { return "Existing mod callback"; } }
        public string Sprite { get { return "custom spawn"; } }
        public TargetKind TargetKind { get; set; }
    }
    public sealed class Tuning { public int[] Costs { get; set; } }
    public sealed class Readiness { public bool Ready { get; set; } }
    public sealed class FixtureApi
    {
        public bool NativeBackendAvailable { get; set; }
        public Tuning Current { get; set; }
        public Replacement Replacement;
        public bool Reject;
        public bool SessionReady = true;
        internal Readiness GetSessionReadiness() => new Readiness { Ready = SessionReady };
        public int Calls;
        public Context Last;
        public bool TryGetReplacement(ExtremePowerId id, out Replacement replacement)
        { replacement = id == ExtremePowerId.Gold ? Replacement : null; return replacement != null; }
        public bool TryExecuteReplacement(Context context, out string reason)
        { Calls++; Last = context; reason = Reject ? "fixture rejection" : null; return !Reject; }
    }
    public static class ExtremePowersBootstrap
    {
        public static FixtureApi Instance { get; } = new FixtureApi {
            NativeBackendAvailable = true,
            Current = new Tuning { Costs = new int[] { 0, 1272, 1908, 2544, 3180, 20000, 4452, 5088 } }
        };
    }
}
