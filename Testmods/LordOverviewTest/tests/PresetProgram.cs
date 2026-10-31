using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using APIShared;
using APIShared.ModSettings;
using MessagePack;
using SHCDESE.API;
using LordOverviewTest;
internal static class SerpLocalization {internal static string Get(string key)=>key;}
internal static class LordOverviewPresetTests {
 static int checks;
 static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
 static OverviewSettings Prepare(string file){var model=new OverviewSettings();model.PreparePresets(null,file,"LordOverviewPresets");model.ActivatePresets();return model;}
 public static int Main(string[] args){try{
  string root=args[0];Program.PresetFixtureInstallRoot=Path.Combine(root,"installation");Directory.CreateDirectory(Program.PresetFixtureInstallRoot);
  PersonalSettingsPaths.TestDocumentsDirectory=_=>Path.Combine(root,"documents");
  APIShared.Localization.UiText.Configure(()=>"en-US");GameNetworkAPI.LocalHost=true;GameNetworkAPI.Networked=true;GameNetworkAPI.MultiplayerGame=true;
  string file=Path.Combine(Program.PresetFixtureInstallRoot,"LordOverviewTest.dll");var model=Prepare(file);
  Check(!model.AllowEnemyDetails && model.EnableMod && model.ShowEliminated,"safe code defaults preserve existing eliminated rows");
  model.SetDisplayShowEliminated(false);Check(!Prepare(file).ShowEliminated,"local eliminated-player filter survives restart");
  model.SetDisplayShowEliminated(true);
  model.SetDisplayColumns("Fear,Gold,TroopsKilled");model.SetDisplayColumns("TroopsKilled,Gold,Fear");model.AllowEnemyDetails=true;
  var restart=Prepare(file);Check(restart.Columns=="TroopsKilled,Gold,Fear" && restart.AllowEnemyDetails,"actual shared storage survives restart and replacement");
  GameNetworkAPI.LocalHost=false;restart.System_RefreshSettingsAccess();restart.AllowEnemyDetails=false;Check(restart.AllowEnemyDetails,"client rejected host edit");
  restart.SetDisplayColumns("Gold,Popularity");Check(restart.Columns=="Gold,Popularity","client local columns remain editable");
  restart.ObserveLobby(new LobbyStateSnapshot(10,null,false,2,false,null,null));
  Check(!restart.EnemyDetailsReceived,"personal host preference is not host confirmation");
  GameXAMLManagerAPI.Instance.ApplyNetworkSync(restart,()=>restart.AllowEnemyDetails=true);
  Check(restart.EnemyDetailsReceived && restart.AllowEnemyDetails,"same-value authoritative update confirms permission");
  Check(restart.Columns=="Gold,Popularity","host update preserves local columns");
  restart.ObserveLobby(new LobbyStateSnapshot(null,null,false,2,true,null,null));Check(restart.EnemyDetailsReceived,"map transition retains confirmed lobby permission");
  restart.ObserveLobby(new LobbyStateSnapshot(11,null,false,2,false,null,null));Check(!restart.EnemyDetailsReceived,"new lobby discards old permission");
  GameXAMLManagerAPI.Instance.ApplyNetworkSync(restart,()=>restart.AllowEnemyDetails=true);
  restart.ObserveLobby(new LobbyStateSnapshot(11,null,true,0,false,"Unresolved authority",null));Check(!restart.EnemyDetailsReceived,"invalid lobby observation revokes confirmation");
  var disk=Prepare(file);Check(disk.AllowEnemyDetails,"received host state did not replace personal host preference");
  GameXAMLManagerAPI.Instance.ApplyNetworkSync(restart,()=>restart.AllowEnemyDetails=false);Check(!restart.AllowEnemyDetails,"host can revoke permission");
  Check(Prepare(file).AllowEnemyDetails,"host revocation not persisted into local preference");
  GameNetworkAPI.LocalHost=true;model.System_RefreshSettingsAccess();
  model.System_EnterMissionPreset(new Dictionary<string,byte[]>{{nameof(OverviewSettings.AllowEnemyDetails),MessagePackSerializer.Serialize(false)}},"Trail",false);
  model.SetDisplayColumns("Fear,Popularity");Check(model.DisplayColumns=="Fear,Popularity","locked mission still permits runtime display choice");
  model.SetDisplayShowEliminated(false);Check(!model.DisplayShowEliminated && model.ShowEliminated,"locked mission stages local player filter without changing mission source");
  model.System_ExitMissionPreset();model.FlushLocalPreferences();Check(model.Columns=="Fear,Popularity","pending display choice persists through normal source after mission");
  Check(Prepare(file).Columns=="Fear,Popularity","deferred local choice survives restart");
  Check(!Prepare(file).ShowEliminated,"deferred player filter persists after source exit");
  var reorder=new ColumnSelection();reorder.Load(model.Columns);Check(reorder.MoveTo(Metric.Popularity,0),"header drop changes selection by metric ID");
  model.SetDisplayColumns(Metrics.Serialize(reorder.Columns));Check(Prepare(file).Columns=="Popularity,Fear","header-drop order survives actual shared persistence restart");
  model.Columns="Gold,Army,Popularity,Fear,GoldAcquired,MaxPopulation";Check(model.Columns=="Gold,Army,Popularity,Fear,GoldAcquired,MaxPopulation","more than five columns persist without normalization");
  model.Columns="Gold,other.mod/missing,Army";Check(Prepare(file).Columns==model.Columns,"missing external provider survives actual preset restart");
    model.Design="Cards";model.LordGrid="Rows:3";model.ValueGrid="Columns:4";
  var layoutRestart=Prepare(file);Check(layoutRestart.Design=="Cards" && layoutRestart.LordGrid=="Rows:3" && layoutRestart.ValueGrid=="Columns:4","design and both grid preferences survive actual shared persistence");
  model.System_EnterMissionPreset(new Dictionary<string,byte[]>{{nameof(OverviewSettings.AllowEnemyDetails),MessagePackSerializer.Serialize(false)}},"Trail",false);
  model.SetDisplayDesign(OverviewDesign.SplitList);model.SetDisplayGrid(true,new GridFlow(1,8));model.SetDisplayGrid(false,new GridFlow(2,3));
  Check(model.DisplayDesign==OverviewDesign.SplitList && model.DisplayLordGrid.ToString()=="1x8" && model.DisplayValueGrid.ToString()=="2x3","locked mission permits all local runtime layout choices");
  model.System_ExitMissionPreset();model.FlushLocalPreferences();layoutRestart=Prepare(file);
  Check(layoutRestart.Design=="SplitList" && layoutRestart.LordGrid=="1x8" && layoutRestart.ValueGrid=="2x3","deferred layout choices survive source exit and restart");
  GameNetworkAPI.LocalHost=false;layoutRestart.System_RefreshSettingsAccess();layoutRestart.SetDisplayDesign(OverviewDesign.Cards);
  Check(layoutRestart.Design=="Cards","client can change its local design");
  GameXAMLManagerAPI.Instance.ApplyNetworkSync(layoutRestart,()=>layoutRestart.AllowEnemyDetails=false);
  Check(!layoutRestart.ShowEliminated,"host sync cannot replace a local player filter");
  Check(layoutRestart.Design=="Cards" && layoutRestart.LordGrid=="1x8","incoming host preferences preserve local layouts");
  layoutRestart.SetDisplayGrid(true,GridFlow.Parse("Rows:3",2).Normalize(8));
  layoutRestart.SetDisplayGrid(false,GridFlow.Parse("Columns:4",3).Normalize(7));
  var migrated=Prepare(file);Check(migrated.LordGrid=="3x3" && migrated.ValueGrid=="2x4","legacy preferences migrate and persist as exact grid pairs");
  migrated.SetDisplayGrid(false,migrated.DisplayValueGrid.Normalize(11));Check(Prepare(file).ValueGrid=="3x4","expanded grid persists without dropping new values");
  layoutRestart.Design="garbage";layoutRestart.LordGrid="Rows:-3";layoutRestart.ValueGrid="Columns:0";
  Check(layoutRestart.Design=="List" && layoutRestart.LordGrid=="Columns:2" && layoutRestart.ValueGrid=="Columns:3","invalid persisted preferences normalize safely");
  migrated.SetDisplayColumns("GreatestLord,Gold");Check(Prepare(file).Columns=="GreatestLord,Gold","new score selection and order survive local storage restart");
  migrated.System_EnterMissionPreset(new Dictionary<string,byte[]>{{nameof(OverviewSettings.AllowEnemyDetails),MessagePackSerializer.Serialize(false)}},"Trail",false);
  migrated.SetDisplayColumns("Gold,GreatestLord");Check(migrated.DisplayColumns=="Gold,GreatestLord","score selection stages under a locked mission source");
  migrated.System_ExitMissionPreset();migrated.FlushLocalPreferences();Check(Prepare(file).Columns=="Gold,GreatestLord","staged score selection persists after mission exit");
  Console.WriteLine("PASS: "+checks+" production LordOverview settings/persistence/host-source checks.");return 0;
 }catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
