using System;
using System.Reflection;

namespace SpectatorEditorBuildTest
{
    // Managed provider entry only: it owns report invalidation and portrait selection.
    internal sealed class SpectatorSelectionAdapter
    {
        private readonly Func<bool> active;
        private readonly Action<int> select;
        private readonly Func<int> viewed;
        private readonly Action<string> warn;
        private bool faulted;

        internal SpectatorSelectionAdapter(Type provider, Func<int> readViewed, Action<string> warning)
        {
            viewed = readViewed;
            warn = warning;
            if (provider == null) return;
            try
            {
                active = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), Require(provider,
                    "IsActiveSpectator", typeof(bool), Type.EmptyTypes));
                select = (Action<int>)Delegate.CreateDelegate(typeof(Action<int>), Require(provider,
                    "SelectPlayer", typeof(void), new[] { typeof(int) }));
            }
            catch (Exception error) { Fail(error); }
        }

        private static MethodInfo Require(Type type, string name, Type result, Type[] parameters)
        {
            var method = type.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null, parameters, null);
            if (method == null || method.ReturnType != result || method.ContainsGenericParameters)
                throw new MissingMethodException(type.FullName, name);
            return method;
        }

        internal bool TryRead(out int player)
        {
            player = -1;
            if (faulted || active == null) return false;
            try
            {
                if (!active()) return false;
                player = viewed();
                return true; // An invalid viewpoint must not silently choose another AI.
            }
            catch (Exception error) { Fail(error); return false; }
        }

        internal bool TrySelect(int player)
        {
            if (player < 1 || player > 8 || !TryRead(out int previous)) return false;
            try
            {
                if (previous != player) select(player);
                return TryRead(out int confirmed) && confirmed == player;
            }
            catch (Exception error) { Fail(error); return false; }
        }

        private void Fail(Exception error)
        {
            if (faulted) return;
            faulted = true;
            warn("TEST_SPECTATOR_SELECTION_UNAVAILABLE: " + error);
        }
    }
}
