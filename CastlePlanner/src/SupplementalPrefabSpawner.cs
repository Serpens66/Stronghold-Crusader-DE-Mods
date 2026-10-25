using System;
using System.Collections.Generic;
using System.Threading;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace CastlePlanner
{
    internal readonly struct SupplementalBuildingIdentity
    {
        internal readonly uint GlobalId;
        internal readonly int Owner, X, Y;
        internal readonly eStructs Type;
        internal readonly AliveState State;

        internal SupplementalBuildingIdentity(uint globalId, int owner, eStructs type, int x, int y, AliveState state)
        { GlobalId = globalId; Owner = owner; Type = type; X = x; Y = y; State = state; }

        internal bool Matches(int owner, eStructs type, int x, int y) => GlobalId != 0 &&
            Owner == owner && Type == type && X == x && Y == y &&
            (State == AliveState.NeedsInit || State == AliveState.IsAlive);
    }

    // Capture only within the synchronous CreatePrefab invocation. A nested call owns
    // its own frame; another thread cannot donate a spawn to this invocation.
    internal sealed class SupplementalPrefabSpawner
    {
        private readonly ThreadLocal<Stack<Capture>> frames =
            new ThreadLocal<Stack<Capture>>(() => new Stack<Capture>());

        internal void Observe(long result, int owner, eStructs type, int x, int y,
            Func<int, SupplementalBuildingIdentity?> lookup)
        {
            Stack<Capture> stack = frames.Value;
            if (stack.Count == 0) return;
            Capture capture = stack.Peek();
            if (capture.Owner != owner || capture.Type != type || capture.X != x || capture.Y != y) return;
            capture.Events++;
            if (result <= 0 || result > int.MaxValue) return;
            int id = (int)result;
            SupplementalBuildingIdentity? identity = lookup(id);
            if (!identity.HasValue || !identity.Value.Matches(owner, type, x, y)) return;
            capture.Id = id;
            capture.GlobalId = identity.Value.GlobalId;
        }

        internal int Create(int owner, eMappers mapper, int x, int y, Func<int, long> create,
            Func<int, SupplementalBuildingIdentity?> lookup, out string reason)
        {
            if (!SupplementalPrefabVisualPolicy.TryGetVariation(mapper, out int variation))
            {
                reason = $"unsupported-supplemental-visual(mapper={mapper})";
                return -1;
            }
            eStructs type = mapper.ConvertToEStructs();
            var capture = new Capture { Owner = owner, Type = type, X = x, Y = y };
            Stack<Capture> stack = frames.Value;
            stack.Push(capture);
            try { create(variation); } // The raw native return is a status/residue, never an ID.
            finally { stack.Pop(); }
            if (capture.Events != 1 || capture.Id <= 0)
            {
                reason = $"unconfirmed-spawn(events={capture.Events}, capturedId={capture.Id})";
                return -1;
            }
            SupplementalBuildingIdentity? current = lookup(capture.Id);
            if (!current.HasValue || current.Value.GlobalId != capture.GlobalId ||
                !current.Value.Matches(owner, type, x, y))
            {
                reason = $"changed-spawn-identity(id={capture.Id}, globalId={capture.GlobalId})";
                return -1;
            }
            reason = string.Empty;
            return capture.Id;
        }

        private sealed class Capture
        {
            internal int Owner, X, Y, Id, Events;
            internal uint GlobalId;
            internal eStructs Type;
        }
    }
}
