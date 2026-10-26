using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL.UnitCommands
{
    internal enum ManualRegionAdmission : byte { Unknown, Reachable, Excluded }

    // One authoritative command only. No tile-path probes and no cursor/tick cache.
    internal sealed unsafe class ManualRegionAdmissionBatch
    {
        internal const int Capacity = 10000;
        internal const int HeaderBytes = 32, EntryBytes = 16;
        internal struct Member
        {
            internal int Id, Source, Mode;
            internal uint GlobalId;
            internal ManualRegionAdmission Decision;
        }

        internal readonly Member[] Members = new Member[Capacity];
        private readonly Dictionary<ulong, ManualRegionAdmission> answers =
            new Dictionary<ulong, ManualRegionAdmission>();
        // Retained with the permanent hook owner; never freed while a stub can read it.
        internal readonly IntPtr NativeState = Marshal.AllocHGlobal(HeaderBytes + (Capacity + 1) * EntryBytes);
        internal int Count, Reachable, Excluded, Queries;
        internal bool Supported;
        internal long Generation;
        internal int Tribe, X, Y, Player, Epoch, Tick;
        internal long Revision;
        internal bool CommonEntered;
        internal object NativeArguments;
        internal ManualRegionAdmissionBatch Parent;
        internal bool Mixed => Supported && Reachable != 0 && Excluded != 0;
        internal bool RejectAll => Supported && Count != 0 && Excluded == Count;

        internal ManualRegionAdmissionBatch()
        {
            new Span<byte>((void*)NativeState, HeaderBytes + (Capacity + 1) * EntryBytes).Clear();
        }

        internal void Reset(long generation)
        {
            Count = Reachable = Excluded = Queries = 0;
            Supported = true; CommonEntered = false; Generation = generation;
            answers.Clear();
        }

        internal void Add(int id, uint globalId, int source, int mode, bool ordinary, bool movable)
        {
            if (Count == Capacity) { Supported = false; return; }
            Members[Count++] = new Member { Id = id, GlobalId = globalId, Source = source, Mode = mode,
                Decision = movable ? ManualRegionAdmission.Unknown : ManualRegionAdmission.Excluded };
            if (!ordinary || id <= 0 || id > Capacity || globalId == 0 || source <= 0)
                Supported = false;
        }

        internal void Evaluate(int player, int target, Func<int, int, int, int, int> query)
        {
            if (!Supported || Count == 0 || target <= 0) { Supported = false; return; }
            for (int i = 0; i < Count; i++)
            {
                Member member = Members[i];
                if (member.Decision != ManualRegionAdmission.Excluded)
                {
                    if (member.Source == target) member.Decision = ManualRegionAdmission.Reachable;
                    else
                    {
                        ulong key = unchecked((ushort)player | ((ulong)(ushort)member.Source << 16) |
                            ((ulong)(ushort)target << 32) | ((ulong)(ushort)member.Mode << 48));
                        if (!answers.TryGetValue(key, out member.Decision))
                        {
                            Queries++;
                            member.Decision = query(player, member.Source, target, member.Mode) != 0
                                ? ManualRegionAdmission.Reachable : ManualRegionAdmission.Excluded;
                            answers.Add(key, member.Decision);
                        }
                    }
                }
                Members[i] = member;
                if (member.Decision == ManualRegionAdmission.Excluded) Excluded++; else Reachable++;
            }
        }

        internal void Publish(IntPtr threadToken, IntPtr unitManager)
        {
            byte* state = (byte*)NativeState;
            *(IntPtr*)state = threadToken; *(IntPtr*)(state + 8) = unitManager;
            *(long*)(state + 16) = Tribe; *(long*)(state + 24) = Generation;
            for (int i = 0; i < Count; i++)
            {
                Member member = Members[i];
                if (member.Decision != ManualRegionAdmission.Excluded) continue;
                byte* entry = state + HeaderBytes + member.Id * EntryBytes;
                *(long*)entry = Generation; *(uint*)(entry + 8) = member.GlobalId;
                entry[12] = (byte)ManualRegionAdmission.Excluded;
            }
        }

        internal bool IsExcluded(int id, uint globalId)
        {
            if (!Supported || id <= 0 || id > Capacity) return false;
            // The managed consumer is not in a unit hot loop; use the same stamped table.
            byte* entry = (byte*)NativeState + HeaderBytes + id * EntryBytes;
            return *(long*)entry == Generation && *(uint*)(entry + 8) == globalId &&
                entry[12] == (byte)ManualRegionAdmission.Excluded;
        }
    }
}
