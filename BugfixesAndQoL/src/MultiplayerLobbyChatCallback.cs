using System;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal static class MultiplayerLobbyChatCallback
    {
        private const string FieldName = "LobbyChatDelegate";

        internal static Action<string, string, int> Capture(Platform_Multiplayer multiplayer)
        {
            if (multiplayer == null)
                throw new ArgumentNullException(nameof(multiplayer));

            FieldInfo field = typeof(Platform_Multiplayer).GetField(
                FieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || field.FieldType != typeof(Action<string, string, int>))
                throw new MissingFieldException(typeof(Platform_Multiplayer).FullName, FieldName);

            var callback = field.GetValue(multiplayer) as Action<string, string, int>;
            if (callback == null)
                throw new InvalidOperationException(
                    "The original multiplayer lobby has no chat callback; replacement was not created.");
            return callback;
        }

        internal static bool IsInstalled(Platform_Multiplayer multiplayer,
            Action<string, string, int> expected) =>
            expected != null && ReferenceEquals(Capture(multiplayer), expected);
    }
}
