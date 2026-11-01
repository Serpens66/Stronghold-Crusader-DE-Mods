using System;
using APIShared.ModSettings;
using SHCDESE.API.Components.Network;
using Spectator.LordOverview;

namespace Spectator
{
 public sealed partial class SpectatorViewModel
 {
  private bool enableLordOverview=true,overviewShowEliminated=true,allowEnemyDetails,overviewSortDescending;
  private string overviewColumns=Metrics.Serialize(Metrics.Defaults),overviewDesign="List",overviewLordGrid="Columns:2",overviewValueGrid="Columns:3",overviewSortBy="Player";
  internal OverviewSettings Overview {get;}
  [PresetLocal] public bool EnableLordOverview {get=>enableLordOverview;set=>Set(ref enableLordOverview,value,nameof(EnableLordOverview));}
  [PresetLocal] public bool OverviewShowEliminated {get=>overviewShowEliminated;set=>Set(ref overviewShowEliminated,value,nameof(OverviewShowEliminated));}
  [PresetLocal] public bool OverviewSortDescending {get=>overviewSortDescending;set=>Set(ref overviewSortDescending,value,nameof(OverviewSortDescending));}
  [SyncHostOnly] public bool AllowEnemyDetails {get=>allowEnemyDetails;set{
   if(!CanMutateSetting())return;
   // A same-value authoritative write still confirms this lobby's permission.
   Overview.ObservePermissionWrite();
   if(allowEnemyDetails==value)return;allowEnemyDetails=value;OnPropertyChanged(nameof(AllowEnemyDetails));SettingsChanged?.Invoke();
  }}
  [PresetLocal] public string OverviewColumns {get=>overviewColumns;set=>SetOverview(ref overviewColumns,Metrics.Serialize(Metrics.Parse(value)),nameof(OverviewColumns));}
  [PresetLocal] public string OverviewDesign {get=>overviewDesign;set=>SetOverview(ref overviewDesign,OverviewLayout.ParseDesign(value).ToString(),nameof(OverviewDesign));}
  [PresetLocal] public string OverviewLordGrid {get=>overviewLordGrid;set=>SetOverview(ref overviewLordGrid,GridFlow.Parse(value,2).ToString(),nameof(OverviewLordGrid));}
  [PresetLocal] public string OverviewValueGrid {get=>overviewValueGrid;set=>SetOverview(ref overviewValueGrid,GridFlow.Parse(value,3).ToString(),nameof(OverviewValueGrid));}
  [PresetLocal] public string OverviewSortBy {get=>overviewSortBy;set=>SetOverview(ref overviewSortBy,string.IsNullOrWhiteSpace(value)?"Player":value,nameof(OverviewSortBy));}
  public string[] OverviewDesignNames=>new[]{SerpLocalization.Get("Overview.DesignList"),SerpLocalization.Get("Overview.DesignSplitList"),SerpLocalization.Get("Overview.DesignCards")};
  public int OverviewDesignIndex {get=>(int)OverviewLayout.ParseDesign(OverviewDesign);set{if(value>=0 && value<=2)OverviewDesign=((LordOverview.OverviewDesign)value).ToString();}}
  private void SetOverview(ref string field,string value,string name){
   if(!CanMutateSetting(name) || field==value)return;field=value;OnPropertyChanged(name);
   if(name==nameof(OverviewDesign))OnPropertyChanged(nameof(OverviewDesignIndex));SettingsChanged?.Invoke();
  }
 }
}
