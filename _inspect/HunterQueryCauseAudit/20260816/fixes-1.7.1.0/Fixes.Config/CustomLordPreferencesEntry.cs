namespace Fixes.Config;

public class CustomLordPreferencesEntry
{
	public bool? EnableHopsFarmFix { get; set; } = true;

	public int? SiegeSelectionMaxSearchRange { get; set; } = 110;

	public int? SiegeSelectionPreferredStandoffDistance { get; set; } = 90;

	public int? HovelBuildingLogicDontBuildWhenAboveHousingSpace { get; set; } = 12;

	public int? HovelBuildingLogicDontBuildWhenAboveOrEqualToCurrentIdlePeasants { get; set; } = 5;

	public int? HovelBuildingLogicDontBuildWhenAboveOrEqualToAverageIdlePeasants { get; set; } = 5;

	public int? HovelBuildingLogicDontBuildWhenBelowPopularity { get; set; } = 5000;
}
