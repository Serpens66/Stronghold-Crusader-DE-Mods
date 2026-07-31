using System.Collections.Generic;

namespace SpawnCastle
{
    internal sealed class AivJsonDocument
    {
        public int pauseDelayAmount;
        public List<AivJsonFrame> frames;
        public List<AivJsonMiscItem> miscItems;
    }

    internal sealed class AivJsonFrame
    {
        public int itemType;
        public List<int> tilePositionOfsets;
        public bool shouldPause;
    }

    internal sealed class AivJsonMiscItem
    {
        public int positionOfset;
        public int itemType;
        public int number;
    }
}
