using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using LordOverviewTest;
internal static partial class Program {
 static void TestVanillaIconMappings(){
  var root=new DirectoryInfo(AppContext.BaseDirectory);
  while(root!=null && !File.Exists(Path.Combine(root.FullName,"_inspect/CrusaderDE-Native-Baseline/CURRENT.json")))root=root.Parent;
  if(root==null)throw new Exception("Vanilla icon evidence root unavailable.");
  using var current=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName,"_inspect/CrusaderDE-Native-Baseline/CURRENT.json")));
  string relative=current.RootElement.GetProperty("semanticDirectory").GetString();
  string path=Path.Combine(root.FullName,"_inspect/CrusaderDE-Native-Baseline",relative,"resources/xaml/Assets/GUI/XAMLResources/HUD_MissionOver.xaml");
  var x=XDocument.Load(path);
  var aliases=new Dictionary<string,Metric>{{"Troops",Metric.Army},{"EnemyKilled",Metric.TroopsKilled},{"YoursKilled",Metric.TroopsLost},{"BuildingsDestroyed",Metric.BuildingsDestroyed},{"BuildingsLost",Metric.BuildingsLost},{"TroopsMade",Metric.TroopsProduced},{"Gold",Metric.GoldAcquired},{"Food",Metric.FoodProduced},{"Wood",Metric.WoodProduced},{"Stone",Metric.StoneProduced},{"Iron",Metric.IronProduced},{"Pitch",Metric.PitchProduced},{"Weapons",Metric.WeaponsProduced},{"Peasants",Metric.MaxPopulation},{"TradeIn",Metric.GoodsReceived},{"TradeOut",Metric.GoodsSent}};
  int checkedMappings=0;
  foreach(var button in x.Descendants().Where(e=>e.Name.LocalName=="RadioButton")){
   string command=(string)button.Attribute("CommandParameter");
   if(command==null || !aliases.TryGetValue(command,out var metric))continue;
   string source=button.Attributes().Single(a=>a.Name.LocalName.EndsWith(".Sprite1",StringComparison.Ordinal)).Value;
   Check(source=="{StaticResource "+OverviewPresentationPolicy.IconKey(metric)+"}","production icon agrees with Vanilla statistic command "+command);checkedMappings++;
  }
  Check(checkedMappings==aliases.Count,"every mapped statistic command was checked against actual Vanilla XAML");
  var fear=x.Descendants().First(e=>(string)e.Attribute("CommandParameter")=="Fear").Ancestors().First(e=>e.Name.LocalName=="RadioButton");
  Check(fear.Attributes().Single(a=>a.Name.LocalName.EndsWith(".Sprite1",StringComparison.Ordinal)).Value=="{StaticResource "+OverviewPresentationPolicy.IconKey(Metric.Fear)+"}","fear uses the original statistics noose emblem");
  Check(OverviewPresentationPolicy.IconKey(Metric.Gold)==OverviewPresentationPolicy.IconKey(Metric.GoldAcquired) && OverviewPresentationPolicy.IconKey(Metric.LordsKilled)=="MP_Star","current gold shares the Vanilla gold emblem and lord kills use their actual star");
  var merit=XDocument.Load(Path.Combine(Path.GetDirectoryName(path),"HUD_MeritPanel.xaml"));
  var crown=merit.Descendants().Single(e=>(string)e.Attribute("Name")=="Merit_SortLordScore");
  Check(crown.Attributes().Single(a=>a.Name.LocalName.EndsWith(".Sprite1",StringComparison.Ordinal)).Value=="{StaticResource "+OverviewPresentationPolicy.IconKey(Metric.GreatestLord)+"}","score uses the actual Merit ranking crown");
  var atlas=XDocument.Load(Path.Combine(root.FullName,"_inspect/CrusaderDE-Native-Baseline",relative,"resources/xaml/Assets/GUI/Sprites/UI-MasterAtlas.xaml"));
  foreach(var metric in Metrics.Builtins){
   if(metric.Id==Metric.Popularity)continue;
   var source=atlas.Root.Elements().Single(e=>e.Attributes().Any(a=>a.Name.LocalName=="Key" && a.Value==OverviewPresentationPolicy.IconKey(metric.Id)));
   var rect=source.Attribute("SourceRect").Value.Split(',').Select(int.Parse).ToArray();var crop=OverviewPresentationPolicy.Crop(metric.Id);
   Check(crop.SourceWidth==rect[2] && crop.SourceHeight==rect[3],"icon source dimensions match the original Vanilla rectangle");
   Check(crop.X>=0 && crop.Y>=0 && crop.Width>0 && crop.Height>0 && crop.X+crop.Width<=rect[2] && crop.Y+crop.Height<=rect[3],"trimmed icon stays within its original source");
  }
  Check(OverviewPresentationPolicy.IconSize==28 && OverviewPresentationPolicy.IconCellSize==32 && OverviewPresentationPolicy.IconSize+4==OverviewPresentationPolicy.IconCellSize,"shared symbol dimensions fill a 32-unit cell with two-unit padding");
 }
}
