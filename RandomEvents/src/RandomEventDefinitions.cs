using System;

namespace RandomEvents
{
    internal enum RandomEventKind
    {
        Fair = 0,
        Plague = 1,
        WheatInfestation = 2,
        HopsBeetles = 3,
        AppleBlight = 4,
        TreeBlight = 5,
        Rabbits = 6,
        LionAttack = 7,
        Bandits = 8,
        MadCows = 9,
        Archers = 10,
        Marriage = 11,
        Bard = 12,
        GranaryTheft = 13,
        Fire = 14
    }

    internal enum RandomEventStrengthKind
    {
        None,
        Plague,
        LionAttack,
        Bandits,
        Archers,
        GranaryTheft,
        Fire
    }

    internal enum RandomEventDispatchKind
    {
        GameAction,
        NativeWildlife,
        NativeVanilla,
        ManualBandits
    }

    public enum MultiplayerEventMode
    {
        SharedEvents = 0,
        IndividualRolls = 1
    }

    internal sealed class RandomEventDefinition
    {
        public RandomEventDefinition(
            RandomEventKind kind,
            string name,
            int textId,
            RandomEventDispatchKind dispatchKind,
            int actionId,
            RandomEventStrengthKind strengthKind,
            bool requiresSignpost)
        {
            Kind = kind;
            Name = name;
            TextId = textId;
            DispatchKind = dispatchKind;
            VanillaActionId = actionId;
            StrengthKind = strengthKind;
            RequiresSignpost = requiresSignpost;
        }

        public RandomEventKind Kind { get; }
        public string Name { get; }
        public int TextId { get; }
        public RandomEventDispatchKind DispatchKind { get; }
        public int VanillaActionId { get; }
        public RandomEventStrengthKind StrengthKind { get; }
        public bool RequiresSignpost { get; }
    }

    internal static class RandomEventDefinitions
    {
        public static readonly RandomEventDefinition[] All =
        {
            new RandomEventDefinition(RandomEventKind.Fair, "Fair", 133, RandomEventDispatchKind.GameAction, 5, RandomEventStrengthKind.None, false),
            new RandomEventDefinition(RandomEventKind.Plague, "Plague", 139, RandomEventDispatchKind.GameAction, 11, RandomEventStrengthKind.Plague, false),
            new RandomEventDefinition(RandomEventKind.WheatInfestation, "Wheat infestation", 140, RandomEventDispatchKind.NativeVanilla, 12, RandomEventStrengthKind.None, false),
            new RandomEventDefinition(RandomEventKind.HopsBeetles, "Hops beetles", 141, RandomEventDispatchKind.NativeVanilla, 13, RandomEventStrengthKind.None, false),
            new RandomEventDefinition(RandomEventKind.AppleBlight, "Apple blight", 142, RandomEventDispatchKind.NativeVanilla, 14, RandomEventStrengthKind.None, false),
            new RandomEventDefinition(RandomEventKind.TreeBlight, "Tree blight", 143, RandomEventDispatchKind.GameAction, 15, RandomEventStrengthKind.None, false),
            new RandomEventDefinition(RandomEventKind.Rabbits, "Rabbit infestation", 144, RandomEventDispatchKind.NativeWildlife, 16, RandomEventStrengthKind.None, false),
            new RandomEventDefinition(RandomEventKind.LionAttack, "Lion attack", 145, RandomEventDispatchKind.NativeWildlife, 17, RandomEventStrengthKind.LionAttack, true),
            new RandomEventDefinition(RandomEventKind.Bandits, "Bandits", 146, RandomEventDispatchKind.ManualBandits, 18, RandomEventStrengthKind.Bandits, true),
            new RandomEventDefinition(RandomEventKind.MadCows, "Mad cows", 147, RandomEventDispatchKind.NativeVanilla, 19, RandomEventStrengthKind.None, false),
            new RandomEventDefinition(RandomEventKind.Archers, "Archers", 148, RandomEventDispatchKind.GameAction, 20, RandomEventStrengthKind.Archers, true),
            new RandomEventDefinition(RandomEventKind.Marriage, "Marriage", 149, RandomEventDispatchKind.GameAction, 21, RandomEventStrengthKind.None, false),
            new RandomEventDefinition(RandomEventKind.Bard, "Bard", 150, RandomEventDispatchKind.GameAction, 22, RandomEventStrengthKind.None, false),
            new RandomEventDefinition(RandomEventKind.GranaryTheft, "Granary theft", 178, RandomEventDispatchKind.NativeVanilla, 29, RandomEventStrengthKind.GranaryTheft, false),
            new RandomEventDefinition(RandomEventKind.Fire, "Fire", 179, RandomEventDispatchKind.GameAction, 30, RandomEventStrengthKind.Fire, false)
        };

        public static RandomEventDefinition Get(RandomEventKind kind) => All[(int)kind];

        public static void GetEncodedStrengthLimits(
            RandomEventStrengthKind kind,
            out int minimum,
            out int maximum)
        {
            switch (kind)
            {
                case RandomEventStrengthKind.None: minimum = 0; maximum = 0; return;
                case RandomEventStrengthKind.Plague:
                case RandomEventStrengthKind.LionAttack:
                case RandomEventStrengthKind.Fire: minimum = 1; maximum = 10; return;
                case RandomEventStrengthKind.Bandits:
                case RandomEventStrengthKind.Archers: minimum = 1; maximum = 50; return;
                case RandomEventStrengthKind.GranaryTheft: minimum = 1; maximum = 100; return;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown event strength kind.");
            }
        }

        public static bool RequiresSignposts(int[] chances)
        {
            if (chances == null || chances.Length < All.Length)
                return false;

            foreach (RandomEventDefinition definition in All)
            {
                if (definition.RequiresSignpost && chances[(int)definition.Kind] > 0)
                    return true;
            }

            return false;
        }
    }
}
