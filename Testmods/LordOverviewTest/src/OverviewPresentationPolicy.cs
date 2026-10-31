namespace LordOverviewTest {
 internal readonly struct IconCrop {
  internal readonly int X,Y,Width,Height,SourceWidth,SourceHeight;
  internal IconCrop(int x,int y,int width,int height,int sourceWidth=129,int sourceHeight=200){X=x;Y=y;Width=width;Height=height;SourceWidth=sourceWidth;SourceHeight=sourceHeight;}
 }
 internal static class OverviewPresentationPolicy {
  internal const int FontSize=14,IconSize=28,IconCellSize=32;
  // FatControler.overNoesisGUI walks both logical and visual parents. These are Vanilla tags,
  // not Noesis hit-test flags: Ignore exempts a subtree, IgnoreSelf only a directly hit container.
  internal const string PassiveTag="Ignore",ContainerTag="IgnoreSelf";
  internal static string StatisticsTag(bool editing)=>editing?ContainerTag:PassiveTag;
  // WGT_Popularity uses GameSprites 0/1/2. Overall BookPopularityColour turns green at 50.
  internal static int PopularitySprite(decimal? permittedValue,bool dynamic)=>!dynamic?0:!permittedValue.HasValue?1:permittedValue.Value>=50?0:2;
  // Normal sprites from HUD_MissionOver, resolved from the existing atlas, never new image files.
  internal static string IconKey(Metric metric){switch(metric){
   case Metric.GreatestLord:return "UI-Buttons S047"; // Vanilla's selected gold crown.
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
  // Alpha bounds relative to the original Vanilla CroppedBitmap, audited against UI-MasterAtlas.
  // Preserve the arrow/sword protrusions; the full 129x200 button rectangle has large empty margins.
  internal static IconCrop Crop(Metric metric){switch(metric){
   case Metric.GreatestLord:return new IconCrop(308,25,383,263,1000,288);
   case Metric.LordsKilled:return new IconCrop(26,46,40,39,90,128);
   case Metric.TroopsKilled:return new IconCrop(3,29,124,133);
   case Metric.TroopsLost:return new IconCrop(2,17,125,145);
   case Metric.StoneProduced:return new IconCrop(3,38,125,124);
   case Metric.WeaponsProduced:return new IconCrop(3,31,125,131);
   case Metric.BuildingsLost:return new IconCrop(3,37,124,125);
   case Metric.TroopsProduced:return new IconCrop(3,37,125,125);
   case Metric.GoodsReceived:return new IconCrop(3,26,124,136);
   case Metric.GoodsSent:return new IconCrop(3,25,124,137);
   case Metric.Popularity:return default(IconCrop); // GameSprites already provide the face masks.
   default:return new IconCrop(3,38,124,124);
  }}
 }
}
