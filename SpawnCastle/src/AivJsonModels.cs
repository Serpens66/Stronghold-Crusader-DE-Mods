using System;
using System.Collections.Generic;

#pragma warning disable 0649

namespace SpawnCastle
{
    [Serializable]
    internal sealed class AivJsonDocument
    {
        public int pauseDelayAmount;
        public List<AivJsonFrame> frames;
        public List<AivJsonMiscItem> miscItems;
    }

    [Serializable]
    internal sealed class AivJsonFrame
    {
        public int itemType;
        public List<int> tilePositionOfsets;
        public bool shouldPause;
    }

    [Serializable]
    internal sealed class AivJsonMiscItem
    {
        public int positionOfset;
        public int itemType;
        public int number;
    }
}

#pragma warning restore 0649
