namespace StartConditions
{
    internal sealed class StartConditionsMapSessionState
    {
        internal bool IsHandled { get; private set; }

        internal bool TryBeginNewMap()
        {
            if (IsHandled)
                return false;

            IsHandled = true;
            return true;
        }

        internal void MarkSaveLoaded()
        {
            IsHandled = true;
        }

        internal void Reset()
        {
            IsHandled = false;
        }
    }
}
