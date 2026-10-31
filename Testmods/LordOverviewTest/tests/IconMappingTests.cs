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
 }
}
