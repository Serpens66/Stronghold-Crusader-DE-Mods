using CrusaderDE;
using ExtendedData;
using Noesis;

internal static class CoopCustomizeLoadTests
{
    internal static void Run()
    {
        // Run the production adapter against boundary doubles: source permissions
        // represent Vanilla's already completed Update, not a copied ready algorithm.
        for (int trail = 0; trail < 4; trail++)
        {
            var lobby = new FRONT_Multiplayer(new EngineInterface.MultiplayerSetupData(), new FileHeader(), true, new[] { 1, 1 });
            var room = lobby.currentLobby;
            room.coopTrailGame = true;
            room.coopTrailID = trail;
            var view = new MainViewModel { Show_MultiplayerSetup = true, Show_MPGameCreation = true };
            FRONT_Multiplayer.coopGame = true;
            FRONT_Multiplayer.skirmishGame = false;
            var sources = new[] { FRONT_CoopTrail1.Instance.RefLoadButton, FRONT_CoopTrail2.Instance.RefLoadButton,
                FRONT_CoopTrail3.Instance.RefLoadButton, FRONT_CoopTrail4.Instance.RefLoadButton };
            foreach (var source in sources) source.IsEnabled = false;
            sources[trail].IsEnabled = true;
            CoopCustomizeLoadPresentation.Refresh(lobby, view, room, true);
            Check(lobby.TestLoadButton.IsEnabled, "selected Coop page permission was not projected");
            sources[trail].IsEnabled = false;
            CoopCustomizeLoadPresentation.Refresh(lobby, view, room, true);
            Check(!lobby.TestLoadButton.IsEnabled, "a subsequent Vanilla refusal stayed enabled");
            sources[trail].IsEnabled = true;

            void Unchanged(Action change, Action restore, string context)
            {
                lobby.TestLoadButton.IsEnabled = false;
                change();
                CoopCustomizeLoadPresentation.Refresh(lobby, view, room, true);
                Check(!lobby.TestLoadButton.IsEnabled, context);
                restore();
            }
            Unchanged(() => room.isHost = false, () => room.isHost = true, "client modified");
            Unchanged(() => room.coopTrailGame = false, () => room.coopTrailGame = true, "ordinary lobby modified");
            Unchanged(() => FRONT_Multiplayer.coopGame = false, () => FRONT_Multiplayer.coopGame = true, "non-Coop modified");
            Unchanged(() => FRONT_Multiplayer.skirmishGame = true, () => FRONT_Multiplayer.skirmishGame = false, "skirmish modified");
            Unchanged(() => lobby.singlePlayerCoop = true, () => lobby.singlePlayerCoop = false, "solo Coop modified");
            Unchanged(() => lobby.panelActive = false, () => lobby.panelActive = true, "inactive panel modified");
            Unchanged(() => view.Show_MPGameCreation = false, () => view.Show_MPGameCreation = true, "hidden setup modified");
            Unchanged(() => view.Show_MultiplayerSetup = false, () => view.Show_MultiplayerSetup = true, "closed frontend modified");
            Unchanged(() => lobby.TestLoading = true, () => lobby.TestLoading = false, "native load modified");
            Unchanged(() => lobby.TestLoadButton.Visibility = Visibility.Collapsed, () => lobby.TestLoadButton.Visibility = Visibility.Visible, "hidden load button modified");
            Unchanged(() => room.coopTrailID = -1, () => room.coopTrailID = trail, "invalid Coop page selected");
            CoopCustomizeLoadPresentation.Refresh(lobby, view, new Platform_Multiplayer.MPLobby(), true);
            Check(!lobby.TestLoadButton.IsEnabled, "stale lobby modified");
            CoopCustomizeLoadPresentation.Refresh(lobby, view, room, false);
            Check(!lobby.TestLoadButton.IsEnabled, "direct Coop page modified");
            CoopCustomizeLoadPresentation.Refresh(lobby, null, room, true);
            CoopCustomizeLoadPresentation.Refresh(null, view, room, true);
        }
        FRONT_Multiplayer.coopGame = false;
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }
}

namespace Noesis
{
    public sealed class Button
    {
        public bool IsEnabled;
        public Visibility Visibility = Visibility.Visible;
    }
}

namespace CrusaderDE
{
    public sealed partial class FRONT_Multiplayer
    {
        private readonly Button RefLoadButton = new();
        private bool MPGameLoading;
        public static bool coopGame;
        public bool singlePlayerCoop;
        public Button TestLoadButton => RefLoadButton;
        public bool TestLoading { set => MPGameLoading = value; }
    }
    public sealed class FRONT_CoopTrail1 { public static readonly FRONT_CoopTrail1 Instance = new(); public Button RefLoadButton = new(); }
    public sealed class FRONT_CoopTrail2 { public static readonly FRONT_CoopTrail2 Instance = new(); public Button RefLoadButton = new(); }
    public sealed class FRONT_CoopTrail3 { public static readonly FRONT_CoopTrail3 Instance = new(); public Button RefLoadButton = new(); }
    public sealed class FRONT_CoopTrail4 { public static readonly FRONT_CoopTrail4 Instance = new(); public Button RefLoadButton = new(); }
}
