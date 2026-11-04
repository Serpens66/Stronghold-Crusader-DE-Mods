using System;
using System.Collections.Generic;
using SHCDESE.Interop;

namespace Shared
{
    internal readonly struct StoragePlacement
    {
        internal StoragePlacement(int sourceIndex, eMappers mapper, int x, int y, int size)
        { SourceIndex = sourceIndex; Mapper = mapper; X = x; Y = y; Size = size; }
        internal int SourceIndex { get; }
        internal eMappers Mapper { get; }
        internal int X { get; }
        internal int Y { get; }
        internal int Size { get; }
        internal static bool IsStorage(eMappers mapper) => mapper == eMappers.MAPPER_STORES ||
            mapper == eMappers.MAPPER_GRANARY || mapper == eMappers.MAPPER_ARMOURY;
    }

    internal enum StorageSectionState { Missing, NeedsInit, IsAlive, Lost }
    internal enum StorageQueueResult { Waiting, Completed, Aborted, TimedOut }

    internal interface IDeferredStorageBackend
    {
        bool IsCurrent(StoragePlacement placement, out string reason);
        StorageSectionState Inspect(StoragePlacement placement);
        bool HasInitializedSeed(eMappers mapper);
        bool CanPlace(StoragePlacement placement, out string reason);
        bool TryCreate(StoragePlacement placement, out string reason);
        void OnReady(StoragePlacement placement, bool created);
    }

    // Mod-owned source link: both castle-start consumers use this state machine.
    // No wall-clock scheduling, artificial entity initialization or placement bypass.
    internal sealed class DeferredStorageQueue
    {
        internal const int TimeoutTicks = 600;
        private readonly List<StoragePlacement> placements = new List<StoragePlacement>();
        private readonly HashSet<int> created = new HashSet<int>();
        private int? firstTick, lastTick;
        private bool processing, ended;
        private StorageQueueResult finalResult;

        internal DeferredStorageQueue(int playerId, IEnumerable<StoragePlacement> source)
        {
            if (playerId < 1 || playerId > 8) throw new ArgumentOutOfRangeException(nameof(playerId));
            if (source == null) throw new ArgumentNullException(nameof(source));
            PlayerId = playerId;
            var unique = new HashSet<Tuple<eMappers, int, int>>();
            foreach (StoragePlacement placement in source)
            {
                if (!StoragePlacement.IsStorage(placement.Mapper) || placement.Size < 1)
                    throw new ArgumentException("Only complete storage placements are supported.", nameof(source));
                if (unique.Add(Tuple.Create(placement.Mapper, placement.X, placement.Y))) placements.Add(placement);
            }
        }

        internal int PlayerId { get; }
        internal int Count => placements.Count;
        internal int Cursor { get; private set; }

        internal StorageQueueResult Process(int nativeTick, IDeferredStorageBackend backend, out string reason)
        {
            reason = string.Empty;
            if (ended) return finalResult;
            if (processing || lastTick == nativeTick) return StorageQueueResult.Waiting;
            if (backend == null) throw new ArgumentNullException(nameof(backend));
            if (!firstTick.HasValue) firstTick = nativeTick;
            // Forward Int32 half-range, including a signed wrap, within one mission.
            uint elapsed = unchecked((uint)(nativeTick - firstTick.Value));
            if (elapsed >= TimeoutTicks)
            { reason = "startup-timeout"; return Finish(StorageQueueResult.TimedOut); }
            lastTick = nativeTick;
            processing = true;
            try
            {
                while (Cursor < placements.Count)
                {
                    StoragePlacement placement = placements[Cursor];
                    if (!backend.IsCurrent(placement, out reason)) return Finish(StorageQueueResult.Aborted);
                    StorageSectionState state = backend.Inspect(placement);
                    if (state == StorageSectionState.Lost)
                    { reason = "observed-section-lost"; return Finish(StorageQueueResult.Aborted); }
                    if (state == StorageSectionState.NeedsInit) return StorageQueueResult.Waiting;
                    if (state == StorageSectionState.IsAlive)
                    {
                        backend.OnReady(placement, created.Contains(Cursor));
                        Cursor++;
                        continue;
                    }
                    // A previously created section must never become a rebuild request.
                    if (created.Contains(Cursor))
                    { reason = "created-section-lost"; return Finish(StorageQueueResult.Aborted); }
                    if (!backend.HasInitializedSeed(placement.Mapper)) return StorageQueueResult.Waiting;
                    if (!backend.CanPlace(placement, out reason)) return Finish(StorageQueueResult.Aborted);
                    // Claim the attempt before invoking native code, even if it throws/reenters.
                    created.Add(Cursor);
                    if (!backend.TryCreate(placement, out reason)) return Finish(StorageQueueResult.Aborted);
                    return StorageQueueResult.Waiting;
                }
                return Finish(StorageQueueResult.Completed);
            }
            catch
            {
                Finish(StorageQueueResult.Aborted);
                throw;
            }
            finally { processing = false; }
        }

        private StorageQueueResult Finish(StorageQueueResult result)
        { ended = true; finalResult = result; return result; }
    }
}
