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
  private bool showEliminated=true;
  private bool? pendingShowEliminated;
  private ulong? lobby;
  internal bool EnemyDetailsReceived {get;private set;}
  internal event Action InformationRightsInvalidated;
  private string columns=Metrics.Serialize(Metrics.Defaults),pendingColumns;
  private string design="List",lordGrid="Columns:2",valueGrid="Columns:3",pendingDesign,pendingLordGrid,pendingValueGrid;
  public OverviewSettings(){ResetToDefaultCommand=new RelayCommand(()=>{if(CanEditHostSettings)AllowEnemyDetails=false;if(CanEditClientSettings){
   pendingColumns=pendingDesign=pendingLordGrid=pendingValueGrid=null;
   pendingShowEliminated=null;
   EnableMod=true;ShowEliminated=true;Columns=Metrics.Serialize(Metrics.Defaults);Design="List";LordGrid="Columns:2";ValueGrid="Columns:3";
  }});}
  [PresetLocal] public bool EnableMod {get=>enabled;set{if(!CanMutateSetting() || enabled==value)return;enabled=value;OnPropertyChanged(nameof(EnableMod));}}
  [PresetLocal] public bool ShowEliminated {get=>showEliminated;set{if(!CanMutateSetting() || showEliminated==value)return;showEliminated=value;OnPropertyChanged(nameof(ShowEliminated));}}
  internal bool DisplayShowEliminated=>pendingShowEliminated??ShowEliminated;
  internal void SetDisplayShowEliminated(bool value){pendingShowEliminated=value;FlushLocalPreferences();}
  [SyncHostOnly] public bool AllowEnemyDetails {get=>enemyDetails;set{if(!CanMutateSetting())return;
   if(lobby.HasValue && GameXAMLManagerAPI.Instance.CurrentLobbyModSettingsChangeOrigin==LobbyModSettingsChangeOrigin.IncomingNetwork)EnemyDetailsReceived=true;
   if(enemyDetails==value)return;enemyDetails=value;if(!value)InformationRightsInvalidated?.Invoke();OnPropertyChanged(nameof(AllowEnemyDetails));}}
  internal void ObserveLobby(LobbyStateSnapshot snapshot){if(snapshot.PreserveForMapTransition && !snapshot.LobbyId.HasValue)return;
   var next=string.IsNullOrEmpty(snapshot.Error)?snapshot.LobbyId:null;
   if(lobby!=next){lobby=next;EnemyDetailsReceived=false;InformationRightsInvalidated?.Invoke();}}
  [PresetLocal] public string Columns {get=>columns;set{if(!CanMutateSetting())return;string next=Metrics.Serialize(Metrics.Parse(value));if(next==columns)return;columns=next;OnPropertyChanged(nameof(Columns));}}
  internal string DisplayColumns=>pendingColumns??Columns;
  internal void SetDisplayColumns(string value){pendingColumns=Metrics.Serialize(Metrics.Parse(value));FlushLocalPreferences();}
  [PresetLocal] public string Design {get=>design;set{if(!CanMutateSetting())return;string next=OverviewLayout.ParseDesign(value).ToString();if(design==next)return;design=next;OnPropertyChanged(nameof(Design));}}
  [PresetLocal] public string LordGrid {get=>lordGrid;set{if(!CanMutateSetting())return;string next=GridFlow.Parse(value,2).ToString();if(lordGrid==next)return;lordGrid=next;OnPropertyChanged(nameof(LordGrid));}}
  [PresetLocal] public string ValueGrid {get=>valueGrid;set{if(!CanMutateSetting())return;string next=GridFlow.Parse(value,3).ToString();if(valueGrid==next)return;valueGrid=next;OnPropertyChanged(nameof(ValueGrid));}}
  private string cachedDesignText,cachedLordText,cachedValueText;
  private OverviewDesign cachedDesign;
  private GridFlow cachedLord,cachedValue;
  internal OverviewDesign DisplayDesign {get {string text=pendingDesign??Design;if(text!=cachedDesignText){cachedDesignText=text;cachedDesign=OverviewLayout.ParseDesign(text);}return cachedDesign;}}
  internal GridFlow DisplayLordGrid {get {string text=pendingLordGrid??LordGrid;if(text!=cachedLordText){cachedLordText=text;cachedLord=GridFlow.Parse(text,2);}return cachedLord;}}
  internal GridFlow DisplayValueGrid {get {string text=pendingValueGrid??ValueGrid;if(text!=cachedValueText){cachedValueText=text;cachedValue=GridFlow.Parse(text,3);}return cachedValue;}}
  internal void SetDisplayDesign(OverviewDesign value){pendingDesign=(Enum.IsDefined(typeof(OverviewDesign),value)?value:OverviewDesign.List).ToString();FlushLocalPreferences();}
  internal void SetDisplayGrid(bool lords,GridFlow value){if(lords)pendingLordGrid=value.ToString();else pendingValueGrid=value.ToString();FlushLocalPreferences();}
  // A locked mission source does not prevent a local HUD choice. Persist it when the personal source is editable again.
  internal void FlushLocalPreferences(){if(!CanEditClientSettings)return;
   if(pendingShowEliminated.HasValue){bool value=pendingShowEliminated.Value;ShowEliminated=value;if(ShowEliminated==value)pendingShowEliminated=null;}
   if(pendingColumns!=null){var value=pendingColumns;Columns=value;if(Columns==value)pendingColumns=null;}
   if(pendingDesign!=null){var value=pendingDesign;Design=value;if(Design==value)pendingDesign=null;}
   if(pendingLordGrid!=null){var value=pendingLordGrid;LordGrid=value;if(LordGrid==value)pendingLordGrid=null;}
   if(pendingValueGrid!=null){var value=pendingValueGrid;ValueGrid=value;if(ValueGrid==value)pendingValueGrid=null;}
  }
  public RelayCommand ResetToDefaultCommand {get;}
  public string ResetText=>SerpLocalization.Get("Reset");
  public string EnemyDetailsText=>SerpLocalization.Get("EnemyDetails");
  public string EnemyDetailsHelp=>SerpLocalization.Get("EnemyDetailsHelp");
  public string ColumnsHelp=>SerpLocalization.Get("ColumnsHelp");
  protected override string ResolveSettingsUiText(string key,string fallback){string text=SerpLocalization.Get(key);return text==key?fallback:text;}
 }
}
