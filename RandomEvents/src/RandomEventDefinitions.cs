using System;
using System.Collections.Generic;

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
            bool direct,
            int actionId,
            RandomEventStrengthKind strengthKind,
            bool requiresSignpost)
        {
            Kind = kind;
            Name = name;
            TextId = textId;
            IsDirect = direct;
            TimelineActionId = actionId;
            StrengthKind = strengthKind;
            RequiresSignpost = requiresSignpost;
        }

        public RandomEventKind Kind { get; }
        public string Name { get; }
        public int TextId { get; }
        public bool IsDirect { get; }
        public int TimelineActionId { get; }
        public RandomEventStrengthKind StrengthKind { get; }
        public bool RequiresSignpost { get; }
    }

    internal static class RandomEventDefinitions
    {
        public static readonly RandomEventDefinition[] All =
        {
            new RandomEventDefinition(RandomEventKind.Fair, "Fair", 133, true, 5, RandomEventStrengthKind.None, false),
            new RandomEventDefinition(RandomEventKind.Plague, "Plague", 139, true, 11, RandomEventStrengthKind.Plague, false),
            new RandomEventDefinition(RandomEventKind.WheatInfestation, "Wheat infestation", 140, false, 12, RandomEventStrengthKind.None, false),
            new RandomEventDefinition(RandomEventKind.HopsBeetles, "Hops beetles", 141, false, 13, RandomEventStrengthKind.None, false),
            new RandomEventDefinition(RandomEventKind.AppleBlight, "Apple blight", 142, false, 14, RandomEventStrengthKind.None, false),
            new RandomEventDefinition(RandomEventKind.TreeBlight, "Tree blight", 143, true, 15, RandomEventStrengthKind.None, false),
            new RandomEventDefinition(RandomEventKind.Rabbits, "Rabbit infestation", 144, true, 16, RandomEventStrengthKind.None, true),
            new RandomEventDefinition(RandomEventKind.LionAttack, "Lion attack", 145, true, 17, RandomEventStrengthKind.LionAttack, true),
            new RandomEventDefinition(RandomEventKind.Bandits, "Bandits", 146, true, 18, RandomEventStrengthKind.Bandits, true),
            new RandomEventDefinition(RandomEventKind.MadCows, "Mad cows", 147, false, 19, RandomEventStrengthKind.None, false),
            new RandomEventDefinition(RandomEventKind.Archers, "Archers", 148, true, 20, RandomEventStrengthKind.Archers, true),
            new RandomEventDefinition(RandomEventKind.Marriage, "Marriage", 149, true, 21, RandomEventStrengthKind.None, false),
            new RandomEventDefinition(RandomEventKind.Bard, "Bard", 150, true, 22, RandomEventStrengthKind.None, false),
            new RandomEventDefinition(RandomEventKind.GranaryTheft, "Granary theft", 178, false, 29, RandomEventStrengthKind.GranaryTheft, false),
            new RandomEventDefinition(RandomEventKind.Fire, "Fire", 179, true, 30, RandomEventStrengthKind.Fire, false)
        };

        public static readonly RandomEventDefinition[] TimelineOnly = Array.FindAll(All, definition => !definition.IsDirect);

        public static RandomEventDefinition Get(RandomEventKind kind) => All[(int)kind];

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
