using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    // Tokens describe the audited Vanilla inline sequence; no UI is changed until
    // the complete history has passed validation.
    internal enum CompactChatToken { Unknown, Shield, Name, Break, Message, End, System }
    internal enum CompactChatAction { Keep, NameRun, MessageRun }

    internal struct CompactChatOperation
    {
        internal readonly int Index;
        internal readonly CompactChatAction Action;
        internal CompactChatOperation(int index, CompactChatAction action)
        { Index = index; Action = action; }
    }

    internal static class CompactLobbyChatLayout
    {
        internal static bool TryPlan(CompactChatToken[] tokens, out CompactChatOperation[] result)
        {
            result = null;
            if (tokens == null) return false;
            var plan = new List<CompactChatOperation>(tokens.Length);
            for (int i = 0; i < tokens.Length;)
            {
                if (tokens[i] == CompactChatToken.System && i + 1 < tokens.Length &&
                    tokens[i + 1] == CompactChatToken.Break)
                {
                    plan.Add(new CompactChatOperation(i++, CompactChatAction.Keep));
                    plan.Add(new CompactChatOperation(i++, CompactChatAction.Keep));
                }
                else if (i + 4 < tokens.Length && tokens[i] == CompactChatToken.Shield &&
                    tokens[i + 1] == CompactChatToken.Name && tokens[i + 2] == CompactChatToken.Break &&
                    tokens[i + 3] == CompactChatToken.Message && tokens[i + 4] == CompactChatToken.End)
                {
                    plan.Add(new CompactChatOperation(i, CompactChatAction.Keep));
                    plan.Add(new CompactChatOperation(i + 1, CompactChatAction.NameRun));
                    plan.Add(new CompactChatOperation(i + 3, CompactChatAction.MessageRun));
                    plan.Add(new CompactChatOperation(i + 4, CompactChatAction.Keep));
                    i += 5;
                }
                else return false;
            }
            result = plan.ToArray();
            return true;
        }

        internal static bool Matches<T>(IList<T> current, T[] expected) where T : class
        {
            if (expected == null || current.Count != expected.Length) return false;
            for (int i = 0; i < expected.Length; i++)
                if (!ReferenceEquals(current[i], expected[i])) return false;
            return true;
        }
    }
}
