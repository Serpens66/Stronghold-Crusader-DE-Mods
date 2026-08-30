using SHCDESE.API;
using System.Collections.Generic;

namespace RandomEvents
{
    internal static class RandomEventsBatchValidator
    {
        public static int MaximumActionCount => checked(
            RandomEventDefinitions.All.Length * GamePlayerManagerAPI.MAX_PLAYERS);

        public static bool Validate(
            RandomEventsBatchChorePacket packet,
            int protocolVersion,
            RandomEventsRuntimeState state,
            out string failure)
        {
            failure = string.Empty;
            if (packet == null || packet.ProtocolVersion != protocolVersion || packet.OperationId <= 0 ||
                packet.DueAbsoluteMonth < 0 || (packet.PrngState0 | packet.PrngState1) == 0)
            {
                failure = "packet header is invalid";
                return false;
            }
            if (state == null ||
                (state.MultiplayerMode != (int)MultiplayerEventMode.SharedEvents &&
                 state.MultiplayerMode != (int)MultiplayerEventMode.IndividualRolls))
            {
                failure = "runtime state or distribution mode is invalid";
                return false;
            }

            int count = packet.EventKinds?.Length ?? -1;
            if (count < 0 || count > MaximumActionCount ||
                packet.EventStrengths?.Length != count || packet.TargetPlayerIds?.Length != count)
            {
                failure = $"action arrays have invalid lengths (count={count}, maximum={MaximumActionCount})";
                return false;
            }

            var seenActions = new HashSet<int>();
            var sharedStrengths = new Dictionary<int, int>();
            for (int index = 0; index < count; index++)
            {
                int kindIndex = packet.EventKinds[index];
                int targetPlayerId = packet.TargetPlayerIds[index];
                if (kindIndex < 0 || kindIndex >= RandomEventDefinitions.All.Length ||
                    targetPlayerId < 1 || targetPlayerId > GamePlayerManagerAPI.MAX_PLAYERS)
                {
                    failure = $"action {index} contains an invalid event kind or target player";
                    return false;
                }

                RandomEventDefinition definition = RandomEventDefinitions.All[kindIndex];
                if ((int)definition.Kind != kindIndex)
                {
                    failure = $"event catalog entry {kindIndex} is not indexed by its event kind";
                    return false;
                }

                int actionKey = checked(kindIndex * (GamePlayerManagerAPI.MAX_PLAYERS + 1) + targetPlayerId);
                if (!seenActions.Add(actionKey))
                {
                    failure = $"event kind {kindIndex} targets player {targetPlayerId} more than once";
                    return false;
                }

                int strength = packet.EventStrengths[index];
                if (!ValidateStrength(definition, strength, out failure))
                {
                    failure = $"action {index}: {failure}";
                    return false;
                }

                if (state.MultiplayerMode == (int)MultiplayerEventMode.SharedEvents)
                {
                    if (sharedStrengths.TryGetValue(kindIndex, out int sharedStrength) && sharedStrength != strength)
                    {
                        failure = $"shared event kind {kindIndex} contains different strengths";
                        return false;
                    }
                    sharedStrengths[kindIndex] = strength;
                }
            }

            return true;
        }

        private static bool ValidateStrength(
            RandomEventDefinition definition,
            int strength,
            out string failure)
        {
            failure = string.Empty;
            RandomEventDefinitions.GetEncodedStrengthLimits(definition.StrengthKind, out int minimum, out int maximum);
            if (strength < minimum || strength > maximum)
            {
                failure = $"strength {strength} is outside the safe encoded range {minimum}..{maximum}";
                return false;
            }
            return true;
        }
    }
}
