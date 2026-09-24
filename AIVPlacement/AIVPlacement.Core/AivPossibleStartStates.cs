using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace AIVPlacement.Core
{
    /// <summary>
    /// Possible native start outcomes before a later AI player is evaluated.
    /// A failed start has no rebuilt buildings; the serialized start for that
    /// slot was already removed by Vanilla's player preparation.
    /// </summary>
    public sealed class AivPossibleStartStates
    {
        public const int MaximumScenarios = 128;

        private readonly IReadOnlyList<AivStartScenario> scenarios;

        private AivPossibleStartStates(IReadOnlyList<AivStartScenario> scenarios)
        {
            this.scenarios = scenarios;
        }

        public static AivPossibleStartStates Initial { get; } =
            new AivPossibleStartStates(new ReadOnlyCollection<AivStartScenario>(
                new[] { new AivStartScenario(new Dictionary<int, AivStartRebuildState>(), 0) }));

        public IReadOnlyList<AivStartScenario> Scenarios => scenarios;

        public bool TryAppend(
            int keepSlotIndex,
            IEnumerable<AivStartRebuildState> successfulStarts,
            out AivPossibleStartStates next)
        {
            if (successfulStarts == null)
                throw new ArgumentNullException(nameof(successfulStarts));
            AivStartRebuildState[] distinctStarts = successfulStarts.Distinct().ToArray();
            var byScenario = new Dictionary<string, IReadOnlyList<AivStartRebuildState>>();
            foreach (AivStartScenario scenario in scenarios)
                byScenario.Add(scenario.Key, distinctStarts);
            return TryAppend(keepSlotIndex, byScenario, out next);
        }

        public bool TryAppend(
            int keepSlotIndex,
            IReadOnlyDictionary<string, IReadOnlyList<AivStartRebuildState>>
                successfulStartsByScenario,
            out AivPossibleStartStates next)
        {
            if (keepSlotIndex < 0 || keepSlotIndex >= 8)
                throw new ArgumentOutOfRangeException(nameof(keepSlotIndex));
            if (successfulStartsByScenario == null)
                throw new ArgumentNullException(nameof(successfulStartsByScenario));
            var unique = new SortedDictionary<string, AivStartScenario>(StringComparer.Ordinal);
            foreach (AivStartScenario scenario in scenarios)
            {
                if (!successfulStartsByScenario.TryGetValue(
                        scenario.Key, out IReadOnlyList<AivStartRebuildState> starts) ||
                    starts == null)
                {
                    next = null;
                    return false;
                }
                // 0x6D580 returns before construction if 0x77E60 sets its
                // failure flag. Until all validator branches are proven,
                // retain this outcome even after a complete AIV fit.
                Add(new AivStartScenario(scenario.RebuiltStartsBySlot,
                    scenario.AbsentStartSlotMask | (1 << keepSlotIndex)));
                foreach (AivStartRebuildState start in starts.Distinct())
                {
                    var rebuilt = scenario.RebuiltStartsBySlot.ToDictionary(
                        entry => entry.Key, entry => entry.Value);
                    rebuilt[keepSlotIndex] = start;
                    Add(new AivStartScenario(rebuilt,
                        scenario.AbsentStartSlotMask & ~(1 << keepSlotIndex)));
                }
            }

            if (unique.Count > MaximumScenarios)
            {
                next = null;
                return false;
            }
            next = new AivPossibleStartStates(
                new ReadOnlyCollection<AivStartScenario>(unique.Values.ToArray()));
            return true;

            void Add(AivStartScenario value)
            {
                unique[value.Key] = value;
            }
        }
    }

    public sealed class AivStartScenario
    {
        internal AivStartScenario(
            IReadOnlyDictionary<int, AivStartRebuildState> rebuiltStartsBySlot,
            int absentStartSlotMask)
        {
            RebuiltStartsBySlot = new ReadOnlyDictionary<int, AivStartRebuildState>(
                rebuiltStartsBySlot.ToDictionary(entry => entry.Key, entry => entry.Value));
            AbsentStartSlotMask = absentStartSlotMask;
            var key = new StringBuilder().Append(absentStartSlotMask).Append('|');
            foreach (KeyValuePair<int, AivStartRebuildState> entry in
                RebuiltStartsBySlot.OrderBy(value => value.Key))
            {
                key.Append(entry.Key).Append(':')
                    .Append((int)entry.Value.Rotation).Append(':')
                    .Append(entry.Value.Marker.EncodedOffset).Append(';');
            }
            Key = key.ToString();
        }

        public IReadOnlyDictionary<int, AivStartRebuildState> RebuiltStartsBySlot { get; }
        public int AbsentStartSlotMask { get; }
        public string Key { get; }
    }
}
