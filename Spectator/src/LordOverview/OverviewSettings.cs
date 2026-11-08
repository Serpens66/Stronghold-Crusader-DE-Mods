using System;
using System.ComponentModel;
using APIShared;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
namespace Spectator.LordOverview {
 // Adapter to the one registered settings model; never owns a second preset/file.
 internal sealed class OverviewSettings : INotifyPropertyChanged {
  private readonly SpectatorViewModel owner;
  private ulong? lobby;
  internal bool EnemyDetailsReceived {get;private set;}
  internal event Action InformationRightsInvalidated;
  public event PropertyChangedEventHandler PropertyChanged;
  internal OverviewSettings(SpectatorViewModel owner){this.owner=owner;owner.PropertyChanged+=Changed;}
  internal bool EnableMod=>owner.EnableClientFeatures && owner.EnableLordOverview;
  internal bool AllowEnemyDetails=>owner.EnableMod && owner.AllowEnemyDetails;
  internal bool IsLocalSettingsHost=>owner.IsLocalSettingsHost;
  internal bool IsRealMultiplayerContext=>owner.IsRealMultiplayerContext;
  internal bool CanEditClientSettings=>owner.CanEditClientSettings;
  internal void System_RefreshSettingsAccess()=>owner.System_RefreshSettingsAccess();
  internal string Columns=>owner.OverviewColumns;
  internal string Design=>owner.OverviewDesign;
  internal string LordGrid=>owner.OverviewLordGrid;
  internal string ValueGrid=>owner.OverviewValueGrid;
  internal string SortBy=>owner.OverviewSortBy;
  internal bool SortDescending=>owner.OverviewSortDescending;
  internal bool ShowEliminated=>owner.OverviewShowEliminated;
  internal string DisplayColumns=>Columns;
  internal string DisplaySortBy=>SortBy;
  internal bool DisplaySortDescending=>SortDescending;
  internal bool DisplayShowEliminated=>ShowEliminated;
  internal bool ShowWhileHudHidden=>owner.OverviewShowWhileHudHidden;
  private string cachedDesignText,cachedLordText,cachedValueText;
  private OverviewDesign cachedDesign;
  private GridFlow cachedLord,cachedValue;
  internal OverviewDesign DisplayDesign {get{string text=Design;if(text!=cachedDesignText){cachedDesignText=text;cachedDesign=OverviewLayout.ParseDesign(text);}return cachedDesign;}}
  internal GridFlow DisplayLordGrid {get{string text=LordGrid;if(text!=cachedLordText){cachedLordText=text;cachedLord=GridFlow.Parse(text,2);}return cachedLord;}}
  internal GridFlow DisplayValueGrid {get{string text=ValueGrid;if(text!=cachedValueText){cachedValueText=text;cachedValue=GridFlow.Parse(text,3);}return cachedValue;}}
  internal void SetDisplayColumns(string value)=>owner.OverviewColumns=Metrics.Serialize(Metrics.Parse(value));
  internal void SetDisplayShowEliminated(bool value)=>owner.OverviewShowEliminated=value;
  internal void SetShowWhileHudHidden(bool value)=>owner.OverviewShowWhileHudHidden=value;
  internal void SetDisplayDesign(OverviewDesign value)=>owner.OverviewDesign=(Enum.IsDefined(typeof(OverviewDesign),value)?value:OverviewDesign.List).ToString();
  internal void SetDisplayGrid(bool lords,GridFlow value){if(lords)owner.OverviewLordGrid=value.ToString();else owner.OverviewValueGrid=value.ToString();}
  internal void SetDisplaySort(string key,bool descending){owner.OverviewSortBy=key;owner.OverviewSortDescending=descending;}
  internal void ObservePermissionWrite(){
   if(lobby.HasValue && GameXAMLManagerAPI.Instance.CurrentLobbyModSettingsChangeOrigin==LobbyModSettingsChangeOrigin.IncomingNetwork && !EnemyDetailsReceived){EnemyDetailsReceived=true;InformationRightsInvalidated?.Invoke();}
  }
  internal void ObserveLobby(LobbyStateSnapshot snapshot){
   if(snapshot.PreserveForMapTransition && !snapshot.LobbyId.HasValue)return;
   var next=string.IsNullOrEmpty(snapshot.Error)?snapshot.LobbyId:null;
   if(lobby!=next){lobby=next;EnemyDetailsReceived=false;InformationRightsInvalidated?.Invoke();}
  }
  private void Changed(object sender,PropertyChangedEventArgs args){
   string name=args.PropertyName;
   switch(name){
    case nameof(SpectatorViewModel.EnableClientFeatures):case nameof(SpectatorViewModel.EnableLordOverview):name=nameof(EnableMod);break;
    case nameof(SpectatorViewModel.EnableMod):case nameof(SpectatorViewModel.AllowEnemyDetails):InformationRightsInvalidated?.Invoke();name=nameof(AllowEnemyDetails);break;
    case nameof(SpectatorViewModel.OverviewColumns):name=nameof(Columns);break;
    case nameof(SpectatorViewModel.OverviewDesign):name=nameof(Design);break;
    case nameof(SpectatorViewModel.OverviewLordGrid):name=nameof(LordGrid);break;
    case nameof(SpectatorViewModel.OverviewValueGrid):name=nameof(ValueGrid);break;
    case nameof(SpectatorViewModel.OverviewShowEliminated):name=nameof(ShowEliminated);break;
    case nameof(SpectatorViewModel.OverviewShowWhileHudHidden):name=nameof(ShowWhileHudHidden);break;
    case nameof(SpectatorViewModel.OverviewSortBy):name=nameof(SortBy);break;
    case nameof(SpectatorViewModel.OverviewSortDescending):name=nameof(SortDescending);break;
    default:if(name!=nameof(IsLocalSettingsHost) && name!=nameof(IsRealMultiplayerContext) && name!=nameof(CanEditClientSettings))return;break;
   }
   PropertyChanged?.Invoke(this,name==args.PropertyName?args:new PropertyChangedEventArgs(name));
  }
 }
}
