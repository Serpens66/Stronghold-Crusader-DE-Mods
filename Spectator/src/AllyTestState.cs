namespace Spectator
{
    internal sealed class AllyTestState
    {
        internal bool Enabled { get; private set; }
        internal int Recipient { get; private set; }
        internal int Target { get; private set; }
        internal string Page { get; private set; } = "";
        internal void Toggle(int viewedPlayer)
        {
            Enabled = !Enabled; Page = "";
            if (Enabled && Recipient == 0) Recipient = viewedPlayer >= 1 && viewedPlayer <= 8 ? viewedPlayer : 0;
        }
        internal void Select(int player) { if (player >= 1 && player <= 8) { Recipient = player; Page = ""; } }
        internal void SelectTarget(int player) { if (player >= 1 && player <= 8) Target = player; }
        internal void Open(string page) { Page = page == "Goods" || page == "Orders" || page == "Protect" ? page : ""; }
        internal void Reset() { Enabled = false; Recipient = Target = 0; Page = ""; }
    }
}
