using System;
using System.Collections.Generic;

namespace AIVParser.Core
{
    /// <summary>
    /// Mirrors the game's AIVLoader.SaveData shape. Public fields deliberately keep
    /// Unity JsonUtility compatibility for the future in-game adapter.
    /// </summary>
    [Serializable]
    public sealed class AivJsonDocument
    {
        public int pauseDelayAmount;
        public List<AivJsonFrame> frames;
        public List<AivJsonMiscItem> miscItems;
    }

    [Serializable]
    public sealed class AivJsonFrame
    {
        public int itemType;
        public List<int> tilePositionOfsets;
        public bool shouldPause;
    }

    [Serializable]
    public sealed class AivJsonMiscItem
    {
        public int positionOfset;
        public int itemType;
        public int number;
    }
}
