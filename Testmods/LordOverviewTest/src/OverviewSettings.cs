using System;
using APIShared.ModSettings;
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using APIShared;
namespace LordOverviewTest {
 public sealed class OverviewSettings : PresetLobbyModSettingsViewModel {
  private bool enabled=true,enemyDetails;
  private ulong? lobby;
  internal bool EnemyDetailsReceived {get;private set;}
  internal event Action InformationRightsInvalidated;
  private string columns=Metrics.Serialize(Metrics.Defaults),pendingColumns;
  public OverviewSettings(){ResetToDefaultCommand=new RelayCommand(()=>{if(CanEditHostSettings)AllowEnemyDetails=false;if(CanEditClientSettings){EnableMod=true;Columns=Metrics.Serialize(Metrics.Defaults);}});}
  [PresetLocal] public bool EnableMod {get=>enabled;set{if(!CanMutateSetting() || enabled==value)return;enabled=value;OnPropertyChanged(nameof(EnableMod));}}
  [SyncHostOnly] public bool AllowEnemyDetails {get=>enemyDetails;set{if(!CanMutateSetting())return;
   if(lobby.HasValue && GameXAMLManagerAPI.Instance.CurrentLobbyModSettingsChangeOrigin==LobbyModSettingsChangeOrigin.IncomingNetwork)EnemyDetailsReceived=true;
   if(enemyDetails==value)return;enemyDetails=value;if(!value)InformationRightsInvalidated?.Invoke();OnPropertyChanged(nameof(AllowEnemyDetails));}}
  internal void ObserveLobby(LobbyStateSnapshot snapshot){if(snapshot.PreserveForMapTransition && !snapshot.LobbyId.HasValue)return;
   var next=string.IsNullOrEmpty(snapshot.Error)?snapshot.LobbyId:null;
   if(lobby!=next){lobby=next;EnemyDetailsReceived=false;InformationRightsInvalidated?.Invoke();}}
  [PresetLocal] public string Columns {get=>columns;set{if(!CanMutateSetting())return;string next=Metrics.Serialize(Metrics.Parse(value));if(next==columns)return;columns=next;OnPropertyChanged(nameof(Columns));}}
  internal string DisplayColumns=>pendingColumns??Columns;
  internal void SetDisplayColumns(string value){pendingColumns=Metrics.Serialize(Metrics.Parse(value));FlushLocalColumns();}
  // A locked mission source does not prevent a local HUD choice. Persist it when the personal source is editable again.
  internal void FlushLocalColumns(){if(pendingColumns==null || !CanEditClientSettings)return;var value=pendingColumns;Columns=value;if(Columns==value)pendingColumns=null;}
  public RelayCommand ResetToDefaultCommand {get;}
  public string ResetText=>SerpLocalization.Get("Reset");
  public string EnemyDetailsText=>SerpLocalization.Get("EnemyDetails");
  public string EnemyDetailsHelp=>SerpLocalization.Get("EnemyDetailsHelp");
  public string ColumnsHelp=>SerpLocalization.Get("ColumnsHelp");
  protected override string ResolveSettingsUiText(string key,string fallback){string text=SerpLocalization.Get(key);return text==key?fallback:text;}
 }
}
