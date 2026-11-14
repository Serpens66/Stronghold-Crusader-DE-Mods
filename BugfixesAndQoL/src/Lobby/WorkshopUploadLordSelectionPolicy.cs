// Feature: Keep the selected Extended Lord after a successful Workshop upload.
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace BugfixesAndQoL
{
    // Pending uploads belong to the UI instance. Every invocation also shadows an outer
    // invocation's restore value, including unrelated/reentrant commands on the same thread.
    internal sealed class WorkshopUploadLordSelectionContext
    {
        private readonly object gate = new object();
        private ConditionalWeakTable<object, WorkshopUploadLordSelectionPolicy> pending =
            new ConditionalWeakTable<object, WorkshopUploadLordSelectionPolicy>();
        [ThreadStatic] private static Frame current;
        private int generation;

        internal WorkshopUploadLordRefresh Handle(object owner, bool enabled, string command,
            WorkshopUploadLordRefresh mode, int selected, out int restored)
        {
            lock (gate)
                return pending.GetValue(owner, _ => new WorkshopUploadLordSelectionPolicy())
                    .HandleCommand(enabled, command, mode, selected, out restored);
        }

        internal void Cancel(object owner)
        {
            lock (gate)
                if (pending.TryGetValue(owner, out var policy)) policy.CancelPending();
        }

        internal void CancelAll()
        {
            lock (gate)
            {
                pending = new ConditionalWeakTable<object, WorkshopUploadLordSelectionPolicy>();
                Interlocked.Increment(ref generation);
            }
        }

        internal IDisposable Enter(object owner, WorkshopUploadLordRefresh refresh, int lord)
        {
            var frame = new Frame(this, owner, refresh, lord, Volatile.Read(ref generation), current);
            current = frame;
            return frame;
        }

        internal int Resolve(object owner, WorkshopUploadLordRefresh refresh, int vanilla)
        {
            Frame frame = current;
            return frame != null && ReferenceEquals(frame.Context, this) && ReferenceEquals(frame.Owner, owner) &&
                frame.Generation == Volatile.Read(ref generation) &&
                frame.Refresh == refresh && frame.Lord >= WorkshopUploadLordSelectionPolicy.MinimumLordType &&
                frame.Lord <= WorkshopUploadLordSelectionPolicy.MaximumLordType ? frame.Lord : vanilla;
        }

        private sealed class Frame : IDisposable
        {
            internal readonly WorkshopUploadLordSelectionContext Context;
            internal readonly object Owner;
            internal readonly WorkshopUploadLordRefresh Refresh;
            internal readonly int Lord;
            internal readonly int Generation;
            private readonly Frame previous;
            internal Frame(WorkshopUploadLordSelectionContext context, object owner,
                WorkshopUploadLordRefresh refresh, int lord, int generation, Frame previous)
            { Context = context; Owner = owner; Refresh = refresh; Lord = lord; Generation = generation; this.previous = previous; }
            public void Dispose() { current = previous; }
        }
    }

    internal enum WorkshopUploadLordRefresh
    {
        None,
        Aiv,
        LordConfig
    }

    internal sealed class WorkshopUploadLordSelectionPolicy
    {
        internal const int MinimumLordType = 2;
        internal const int MaximumLordType = 29;

        private WorkshopUploadLordRefresh pendingRefresh;
        private int pendingLordType;

        internal WorkshopUploadLordRefresh HandleCommand(
            bool enabled,
            string command,
            WorkshopUploadLordRefresh currentMode,
            int selectedLordType,
            out int restoredLordType)
        {
            restoredLordType = 0;
            if (!enabled)
            {
                Clear();
                return WorkshopUploadLordRefresh.None;
            }

            WorkshopUploadLordRefresh requestedRefresh = GetRefreshCommand(command);
            if (requestedRefresh != WorkshopUploadLordRefresh.None &&
                requestedRefresh == pendingRefresh &&
                requestedRefresh == currentMode)
            {
                restoredLordType = pendingLordType;
                Clear();
                return requestedRefresh;
            }

            if (string.Equals(command, "DoUpload", StringComparison.Ordinal))
            {
                if (currentMode != WorkshopUploadLordRefresh.None &&
                    selectedLordType >= MinimumLordType &&
                    selectedLordType <= MaximumLordType)
                {
                    pendingRefresh = currentMode;
                    pendingLordType = selectedLordType;
                }
                else
                {
                    Clear();
                }

                return WorkshopUploadLordRefresh.None;
            }

            Clear();
            return WorkshopUploadLordRefresh.None;
        }

        internal void CancelPending()
        {
            Clear();
        }

        private static WorkshopUploadLordRefresh GetRefreshCommand(string command)
        {
            if (string.Equals(command, "UploadAIV", StringComparison.Ordinal))
                return WorkshopUploadLordRefresh.Aiv;
            if (string.Equals(command, "UploadLordConfig", StringComparison.Ordinal))
                return WorkshopUploadLordRefresh.LordConfig;
            return WorkshopUploadLordRefresh.None;
        }

        private void Clear()
        {
            pendingRefresh = WorkshopUploadLordRefresh.None;
            pendingLordType = 0;
        }
    }
}
