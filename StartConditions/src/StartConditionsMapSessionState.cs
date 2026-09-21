namespace StartConditions
{
    internal sealed class StartConditionsMapSessionState
    {
        internal bool IsHandled { get; private set; }
        internal bool IsNewGame { get; private set; }

        internal bool TryBeginNewMap()
        {
            if (IsHandled)
                return false;

            IsHandled = true;
            IsNewGame = true;
            return true;
        }

        internal void MarkSaveLoaded()
        {
            IsHandled = true;
            IsNewGame = false;
        }

        internal void Reset()
        {
            IsHandled = false;
            IsNewGame = false;
        }
    }
}
