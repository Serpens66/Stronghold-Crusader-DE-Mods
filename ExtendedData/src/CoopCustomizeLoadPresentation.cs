using System;
using Expression = System.Linq.Expressions.Expression;
using System.Reflection;
using CrusaderDE;
using Noesis;

namespace ExtendedData
{
    internal static class CoopCustomizeLoadPresentation
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Func<FRONT_Multiplayer, Button> LoadButton = Getter<Button>("RefLoadButton");
        private static readonly Func<FRONT_Multiplayer, bool> Loading = Getter<bool>("MPGameLoading");

        // Customize replaces the Coop page with the general setup. That setup's load
        // button rejects AI members; the original Coop page already has the correct
        // Vanilla permission. Project its current result, never calculate readiness.
        internal static void Refresh(FRONT_Multiplayer lobby, MainViewModel view,
            Platform_Multiplayer.MPLobby activeLobby, bool customizedCoop)
        {
            if (!customizedCoop || lobby == null || view == null || !lobby.panelActive ||
                !FRONT_Multiplayer.coopGame || FRONT_Multiplayer.skirmishGame || lobby.singlePlayerCoop ||
                !view.Show_MultiplayerSetup || !view.Show_MPGameCreation)
                return;
            var room = lobby.currentLobby;
            if (room == null || !room.isHost || !room.coopTrailGame ||
                !ReferenceEquals(room, activeLobby) || Loading(lobby))
                return;
            Button original;
            switch (room.coopTrailID)
            {
                case 0: original = FRONT_CoopTrail1.Instance?.RefLoadButton; break;
                case 1: original = FRONT_CoopTrail2.Instance?.RefLoadButton; break;
                case 2: original = FRONT_CoopTrail3.Instance?.RefLoadButton; break;
                case 3: original = FRONT_CoopTrail4.Instance?.RefLoadButton; break;
                default: return;
            }
            var target = LoadButton(lobby);
            if (original != null && target != null && target.Visibility == Visibility.Visible &&
                target.IsEnabled != original.IsEnabled)
                target.IsEnabled = original.IsEnabled;
        }

        private static Func<FRONT_Multiplayer, T> Getter<T>(string name)
        {
            var field = typeof(FRONT_Multiplayer).GetField(name, PrivateInstance);
            if (field == null || field.FieldType != typeof(T))
                throw new MissingFieldException(typeof(FRONT_Multiplayer).FullName, name);
            var self = Expression.Parameter(typeof(FRONT_Multiplayer), "self");
            return Expression.Lambda<Func<FRONT_Multiplayer, T>>(Expression.Field(self, field), self).Compile();
        }
    }
}
