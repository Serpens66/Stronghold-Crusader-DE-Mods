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
  Check(!model.AllowEnemyDetails && model.EnableMod,"safe code defaults");
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
  model.System_ExitMissionPreset();model.FlushLocalColumns();Check(model.Columns=="Fear,Popularity","pending display choice persists through normal source after mission");
  Check(Prepare(file).Columns=="Fear,Popularity","deferred local choice survives restart");
  var reorder=new ColumnSelection();reorder.Load(model.Columns);Check(reorder.MoveTo(Metric.Popularity,0),"header drop changes selection by metric ID");
  model.SetDisplayColumns(Metrics.Serialize(reorder.Columns));Check(Prepare(file).Columns=="Popularity,Fear","header-drop order survives actual shared persistence restart");
  model.Columns="Gold,Army,Popularity,Fear,GoldAcquired,MaxPopulation";Check(model.Columns==Metrics.Serialize(Metrics.Defaults),"invalid six-column preference normalizes safely");
  Console.WriteLine("PASS: "+checks+" production LordOverview settings/persistence/host-source checks.");return 0;
 }catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
