using System;
using System.Collections.Generic;
using Spectator.LordOverview;

internal static partial class Program
{
    private sealed class PortraitSource : IPlayerPortraitSource<object>
    {
        internal readonly PortraitIdentity[] Identities = new PortraitIdentity[9];
        internal readonly Dictionary<ulong, object> SteamImages = new Dictionary<ulong, object>();
        internal object LocalImage, AiImage;
        internal int Reads, IdReads, LocalReads;
        internal Action DuringRead;
        public PortraitIdentity Identify(int player) { IdReads++; return Identities[player]; }
        public object Read(PortraitIdentity identity)
        {
            Reads++;
            var callback = DuringRead; DuringRead = null; callback?.Invoke();
            if (identity.Kind == PortraitKind.Local) { LocalReads++; return LocalImage; }
            if (identity.Kind == PortraitKind.Ai) return AiImage;
            return SteamImages.TryGetValue(identity.SteamId, out var image) ? image : null;
        }
    }

    private static void TestPortraits()
    {
        var steamPicture = new object(); var coatOfArms = new object();
        var source = new PortraitSource { LocalImage = steamPicture };
        var cache = new PlayerPortraits<object>(source);
        source.Identities[1] = PortraitIdentity.Choose(false, -1, true, false, 0);
        cache.SetPlayers(1 << 1);
        Check(ReferenceEquals(cache.Resolve(1, 0), steamPicture) && source.LocalReads == 1,
            "singleplayer without a multiplayer list uses Vanilla's local avatar");
        Check(!cache.HasPending, "available local portraits require no extra GUI work");
        source.LocalImage = coatOfArms;
        Check(ReferenceEquals(cache.Resolve(1, 1), coatOfArms), "Vanilla's selected coat of arms is respected without creating another texture");
        int reads = source.Reads;
        Check(ReferenceEquals(cache.Resolve(1, 1.2), coatOfArms) && source.Reads == reads, "same source is reused across repeated UI rebuilds");
        Check(cache.Poll(100) == 0 && source.Reads == reads, "settled portraits do not poll even while the GUI continues updating");

        // The same policy is used in the runtime for all known non-MP contexts, including editor.
        Check(PortraitIdentity.Choose(false, -1, true, false, 123).Kind == PortraitKind.Local, "stale multiplayer identity never overrides a local context");
        Check(PortraitIdentity.Choose(false, -1, false, false, 123).Kind == PortraitKind.Missing, "unknown mode never guesses a local identity");
        var ai = PortraitIdentity.Choose(true, 42, true, true, 123);
        Check(ai.Kind == PortraitKind.Ai && ai.Lord == 42, "AI/custom lord identity takes its existing lord ID, not a human avatar");

        cache.Reset(); cache.SetPlayers((1 << 1) | (1 << 4));
        source.Identities[1] = PortraitIdentity.Choose(false, -1, true, true, 101);
        source.Identities[4] = PortraitIdentity.Choose(false, -1, true, true, 404);
        source.SteamImages[101] = steamPicture; source.SteamImages[404] = coatOfArms;
        Check(ReferenceEquals(cache.Resolve(1, 0), steamPicture) && ReferenceEquals(cache.Resolve(4, 0), coatOfArms), "each MP slot resolves its own validated Steam identity");
        reads = source.LocalReads;
        source.Identities[4] = PortraitIdentity.Choose(false, -1, true, true, 0);
        Check(cache.Resolve(4, .1) == null && cache.Cached(4) == null && source.LocalReads == reads, "missing MP identity clears the previous source and cannot use the local avatar");
        int idReads = source.IdReads;
        Check(cache.Poll(.9) == 0 && source.IdReads == idReads, "pending lookup is gated before even resolving player identities");
        source.Identities[4] = PortraitIdentity.Choose(false, -1, true, true, 505);
        source.SteamImages[505] = new object();
        Check(cache.Poll(1.1) == (1 << 4) && ReferenceEquals(cache.Cached(4), source.SteamImages[505]), "late MP identity and source update the same realized portrait while paused");
        Check(!cache.HasPending && cache.Poll(2.2) == 0, "pending work stops as soon as all images are available");

        cache.Reset(); cache.SetPlayers(1 << 1);
        source.Identities[1] = PortraitIdentity.Choose(false, -1, true, true, 606);
        Check(cache.Resolve(1, 0) == null && cache.HasPending, "null cache result is not treated as permanently unavailable");
        reads = source.Reads;
        for (int i = 1; i < 10; i++) { cache.Resolve(1, i / 10.0); cache.Poll(i / 10.0); }
        Check(source.Reads == reads, "missing images are not re-read by menu activity within one second");
        source.SteamImages[606] = new object();
        Check(cache.Poll(1) == (1 << 1) && ReferenceEquals(cache.Cached(1), source.SteamImages[606]), "late cached image appears without a numeric statistics update");
        source.Identities[1] = PortraitIdentity.Choose(false, -1, true, true, 707);
        Check(cache.Resolve(1, 1.1) == null, "reused player number immediately drops the previous person's source");
        cache.SetPlayers(1 << 4);
        reads = source.Reads;
        Check(!cache.HasPending && cache.Cached(1) == null && cache.Poll(10) == 0 && source.Reads == reads, "filtering/removing a row releases its pending work and source");

        cache.Reset(); cache.SetPlayers(1 << 1);
        source.Identities[1] = ai;
        source.AiImage = null; cache.Resolve(1, 0);
        source.AiImage = coatOfArms;
        Check(cache.Poll(1) == (1 << 1) && ReferenceEquals(cache.Cached(1), coatOfArms), "late custom AI image retains the existing AI resolver path");
        cache.Reset(); cache.SetPlayers(1 << 1);
        source.DuringRead = cache.Reset;
        Check(cache.Resolve(1, 0) == null && cache.Cached(1) == null && !cache.HasPending, "closing/HUD/session reset retires an in-flight source read");
        reads = source.Reads;
        Check(cache.Resolve(1, 5) == null && cache.Poll(5) == 0 && source.Reads == reads, "closed overview performs no image reads");
        cache.SetPlayers(1 << 1);
        Check(ReferenceEquals(cache.Resolve(1, 5), coatOfArms), "new HUD/session can resolve again with fresh ownership");
        cache.Resolve(1, 5.2); cache.Poll(5.2);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) { cache.Resolve(1, 5.2); cache.Poll(5.2); }
        Check(GC.GetAllocatedBytesForCurrentThread() == allocated, "prepared portrait cache/idle refresh allocate no managed objects");
    }
}
