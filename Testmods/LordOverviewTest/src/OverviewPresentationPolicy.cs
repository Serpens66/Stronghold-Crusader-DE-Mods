namespace LordOverviewTest {
 internal static class OverviewPresentationPolicy {
  // FatControler.overNoesisGUI walks both logical and visual parents. These are Vanilla tags,
  // not Noesis hit-test flags: Ignore exempts a subtree, IgnoreSelf only a directly hit container.
  internal const string PassiveTag="Ignore",ContainerTag="IgnoreSelf";
  internal static string StatisticsTag(bool editing)=>editing?ContainerTag:PassiveTag;
  // WGT_Popularity uses GameSprites 0/1/2. Overall BookPopularityColour turns green at 50.
  internal static int PopularitySprite(decimal? permittedValue,bool dynamic)=>!dynamic?0:!permittedValue.HasValue?1:permittedValue.Value>=50?0:2;
  // Normal sprites from HUD_MissionOver, resolved from the existing atlas, never new image files.
  internal static string IconKey(Metric metric){switch(metric){
   case Metric.Gold:case Metric.GoldAcquired:return "UI-Buttons S048";
   case Metric.Army:return "UI-Buttons S051";
   case Metric.TroopsKilled:return "UI-Buttons S055";case Metric.TroopsLost:return "UI-Buttons S058";
   case Metric.FoodProduced:return "UI-Buttons S061";case Metric.WoodProduced:return "UI-Buttons S064";
   case Metric.StoneProduced:return "UI-Buttons S067";case Metric.IronProduced:return "UI-Buttons S070";
   case Metric.PitchProduced:return "UI-Buttons S073";case Metric.WeaponsProduced:return "UI-Buttons S076";
   case Metric.MaxPopulation:return "UI-Buttons S079";case Metric.Fear:return "UI-Buttons S082";
   case Metric.BuildingsDestroyed:return "UI-Buttons S085";case Metric.BuildingsLost:return "UI-Buttons S088";
   case Metric.TroopsProduced:return "UI-Buttons S091";
   case Metric.GoodsReceived:return "UI-Buttons S094";case Metric.GoodsSent:return "UI-Buttons S097";
   case Metric.LordsKilled:return "MP_Star";default:return null;
  }}
 }
}
