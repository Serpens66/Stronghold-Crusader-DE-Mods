using Noesis;

namespace BuildingCosts
{
    public sealed class BuildingCostTooltipEntry
    {
        public string Title { get; set; } = "";
        public bool IsTitle { get; set; }
        public string AmountRequired { get; set; } = "";
        public string AmountAvailable { get; set; } = "";
        public ImageSource Image { get; set; }
        public Visibility TitleVisibility => IsTitle ? Visibility.Visible : Visibility.Collapsed;
        public Visibility CostVisibility => IsTitle ? Visibility.Collapsed : Visibility.Visible;
    }
}
