using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using System;

namespace APIShared
{
    /// <summary>An immutable copy of one local player's last completed selection.</summary>
    public sealed class LocalSelectionSnapshot
    {
        private readonly SelectedUnitInfo[] units;

        internal LocalSelectionSnapshot(int playerId, SelectedUnitInfo[] units)
        {
            PlayerId = playerId;
            this.units = units;
        }

        /// <summary>The one-based local player ID used to capture this selection.</summary>
        public int PlayerId { get; }

        /// <summary>The number of selected units.</summary>
        public int Count => units.Length;

        /// <summary>Gets the selected unit ID and type at an index without exposing the backing array.</summary>
        public SelectedUnitInfo this[int index] => units[index];
    }

    /// <summary>Shares a demand-driven selection snapshot between runtime mods.</summary>
    public static class LocalSelectionAPI
    {
        private const int MaximumSelectionCount = 10000;
        private static readonly object Sync = new object();
        private static EngineInterface.PlayState cachedState;
        private static LocalSelectionSnapshot cachedSelection;

        /// <summary>
        /// Copies the local selection from one completed Vanilla play state. An unchanged
        /// selection reuses its immutable snapshot, even when Vanilla publishes a new tick.
        /// </summary>
        public static bool TryCapture(int expectedPlayerId, out LocalSelectionSnapshot snapshot)
        {
            snapshot = null;
            if (expectedPlayerId < 1 || expectedPlayerId > 8)
                return false;

            int actualPlayerId = Shared.GameModeHelper.IsMapEditor()
                ? EditorDirector.instance?.ActivePlayerID ?? -1
                : GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? -1;
            if (actualPlayerId != expectedPlayerId)
                return false;

            EngineInterface.PlayState state = GameData.Instance?.lastGameState;
            return TryCaptureState(expectedPlayerId, state, out snapshot);
        }

        private static bool TryCaptureState(int expectedPlayerId, EngineInterface.PlayState state,
            out LocalSelectionSnapshot snapshot)
        {
            snapshot = null;
            lock (Sync)
            {
                if (state == null)
                {
                    cachedState = null;
                    cachedSelection = null;
                    return false;
                }

                int count = state.numSelectedChimps;
                int[] ids = state.selectedChimps;
                int[] types = state.selectedChimpTypes;
                if (count < 0 || count > MaximumSelectionCount || ids == null || types == null ||
                    ids.Length < count || types.Length < count)
                    return false;

                if (cachedSelection != null && cachedSelection.PlayerId == expectedPlayerId)
                {
                    if (ReferenceEquals(state, cachedState) || HasSameSelection(cachedSelection, ids, types, count))
                    {
                        cachedState = state;
                        snapshot = cachedSelection;
                        return true;
                    }
                }

                var copied = new SelectedUnitInfo[count];
                for (int index = 0; index < count; index++)
                    copied[index] = new SelectedUnitInfo { UnitId = ids[index], UnitType = types[index] };
                cachedState = state;
                cachedSelection = new LocalSelectionSnapshot(expectedPlayerId, copied);
                snapshot = cachedSelection;
                return true;
            }
        }

        private static bool HasSameSelection(LocalSelectionSnapshot selection, int[] ids, int[] types, int count)
        {
            if (selection.Count != count)
                return false;
            for (int index = 0; index < count; index++)
            {
                SelectedUnitInfo unit = selection[index];
                if (unit.UnitId != ids[index] || unit.UnitType != types[index])
                    return false;
            }
            return true;
        }
    }
}
