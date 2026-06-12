using Noesis;

namespace BuildingCosts
{
    public sealed class BuildingCostTooltipEntry
    {
        public string AmountRequired { get; set; } = "";
        public string AmountAvailable { get; set; } = "";
        public ImageSource Image { get; set; }
    }
}
