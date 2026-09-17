$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
function Method([string]$source,[string]$marker) {
    $start=$source.IndexOf($marker); if($start -lt 0) { throw "Missing $marker" }
    $open=$source.IndexOf('{',$start); $depth=1; $end=$open+1
    while($depth -gt 0) { if($source[$end] -eq '{') {$depth++}; if($source[$end] -eq '}') {$depth--}; $end++ }
    return $source.Substring($start,$end-$start)
}
$results = @()
foreach($mod in @('UnitCosts','UnitLimit')) {
    $relative=if($mod -eq 'UnitCosts') {'UnitCosts/src/UnitCostsRuntime.cs'} else {'UnitLimit/src/UnitLimitRuntime.RecruitmentAvailability.cs'}
    $old=(& git -C $root show ('7e048e6435a72b339dff35f3dcd368210f04ccdb:'+$relative)) -join "`r`n"
    $new=[IO.File]::ReadAllText((Join-Path $root $relative))
    $oldMap=[regex]::Matches($old,'eChimps\.(\w+), panel\.(RefRecruit\w+)\)')
    $newMap=[regex]::Matches($new,'new RecruitmentButtonRule\(eChimps\.(\w+), panel => panel\.(\w+)\)')
    $a=@($oldMap | ForEach-Object { $_.Groups[1].Value+':'+$_.Groups[2].Value })
    $b=@($newMap | ForEach-Object { $_.Groups[1].Value+':'+$_.Groups[2].Value })
    if(($a -join ',') -cne ($b -join ',') -or $a.Count -ne 26) {throw "Button mapping changed: $mod"}
    $enums=@($oldMap | ForEach-Object {$_.Groups[1].Value})
    $fields=@($oldMap | ForEach-Object {'public Noesis.UIElement '+$_.Groups[2].Value+' = new Noesis.UIElement();'}) -join "`r`n"
    $code=@'
using System;
using System.Collections.Generic;
using System.Linq;
'@
    $code+="`nnamespace ${mod}Audit {`nnamespace Noesis { public class UIElement { public bool IsEnabled=true; } }`npublic enum eGoods { None }`npublic enum eChimps { CHIMP_NUM_TYPES, " + ($enums -join ',') + " }`npublic class HUD_Buildings { $fields }`n"
    $code+=@'
public class MainViewModel { public static MainViewModel Instance = new MainViewModel(); public HUD_Buildings HUDBuildingPanel; }
public class UnitExtraCostValues { public bool Configured, Affordable; public bool HasAnyCost() {return Configured;} }
public class Stub {
 public bool EffectsEnabled=true, Allowed=true;
 public int PlayerId=1, ExpiryCalls, WorkerCalls, HelperCalls;
 public readonly Dictionary<eChimps,UnitExtraCostValues> humanExtraCosts=new Dictionary<eChimps,UnitExtraCostValues>();
 public readonly Dictionary<eChimps,int> activeUnitLimits=new Dictionary<eChimps,int>();
 protected bool IsUnitCostModeAllowed() {return Allowed;}
 protected bool IsUnitLimitModeAllowed() {return Allowed;}
 protected int GetLocalHumanPlayerId() {return PlayerId;}
 protected eChimps GetLastTroopBuildChimp(MainViewModel vm) {return eChimps.CHIMP_NUM_TYPES;}
 protected int GetLastTroopsAmountToMake(MainViewModel vm) {return 1;}
 protected bool TryGetHumanExtraCosts(eChimps type,out UnitExtraCostValues costs) {return humanExtraCosts.TryGetValue(type,out costs) && costs.HasAnyCost();}
 protected bool HasEnoughExtraCosts(int player,UnitExtraCostValues costs,int amount,out eGoods good,out bool horse,out int req,out int avail) {WorkerCalls++; good=default; horse=false; req=avail=0; return costs.Affordable;}
 protected void RemoveExpiredPendingRecruitments() {ExpiryCalls++;}
 protected int CountAliveUnits(int player,eChimps type) {WorkerCalls++; return 3;}
 protected int GetPendingRecruitmentCount(int player,eChimps type) {return 0;}
}
'@
    foreach($optimized in @($false,$true)) {
        $text=if($optimized){$new}else{$old}; $name=if($optimized){'After'}else{'Before'}
        $code+="`npublic class $name : Stub {`n"
        if($optimized) {
            $s=$text.IndexOf('        private readonly System.Collections.Generic.List<RecruitmentButtonRule>')
            $e=$text.IndexOf('        internal void RefreshRecruitmentButtonAvailability()', $s)
            $code+=$text.Substring($s,$e-$s)
            $code+='public void Rebuild() { RebuildConfiguredRecruitmentButtons(); }'
        } else {$code+='public void Rebuild() {}'}
        $code+=Method $text '        internal void RefreshRecruitmentButtonAvailability()'
        $helper=if($mod -eq 'UnitCosts') {'        private void DisableRecruitmentButtonIfMissingExtraCosts('} else {'        private void DisableRecruitmentButtonIfLimitReached('}
        $method=Method $text $helper; $open=$method.IndexOf('{')
        $code+=$method.Insert($open+1,' HelperCalls++; ')
        $code+="`n}`n"
    }
    $code+=@'
public static class Test {
 public static string Run() {
  int checks=0; long oldHelpers=0,newHelpers=0;
  var random=new Random(1729);
  for(int scenario=0;scenario<300;scenario++) {
   var a=new Before(); var b=new After();
   a.Allowed=b.Allowed=scenario%7!=0; a.EffectsEnabled=b.EffectsEnabled=scenario%9!=0;
   a.PlayerId=b.PlayerId=scenario%11==0 ? 0 : 1;
   foreach(eChimps type in Enum.GetValues(typeof(eChimps))) {
    if(type==eChimps.CHIMP_NUM_TYPES || random.Next(3)!=0) continue;
    var costs=new UnitExtraCostValues {Configured=random.Next(2)==0,Affordable=random.Next(2)==0};
    a.humanExtraCosts[type]=costs; b.humanExtraCosts[type]=costs;
    int limit=random.Next(-1,6); a.activeUnitLimits[type]=limit; b.activeUnitLimits[type]=limit;
   }
   for(int pass=0;pass<2;pass++) {
    a.Rebuild(); b.Rebuild();
    var p=new HUD_Buildings(); var q=new HUD_Buildings();
    foreach(var field in typeof(HUD_Buildings).GetFields()) {
     bool enabled=random.Next(3)!=0;
     ((Noesis.UIElement)field.GetValue(p)).IsEnabled=enabled; ((Noesis.UIElement)field.GetValue(q)).IsEnabled=enabled;
    }
    MainViewModel.Instance.HUDBuildingPanel=p; a.RefreshRecruitmentButtonAvailability();
    MainViewModel.Instance.HUDBuildingPanel=q; b.RefreshRecruitmentButtonAvailability();
    foreach(var field in typeof(HUD_Buildings).GetFields()) {
     if(((Noesis.UIElement)field.GetValue(p)).IsEnabled!=((Noesis.UIElement)field.GetValue(q)).IsEnabled) throw new Exception("Button mismatch "+scenario+"/"+pass+"/"+field.Name);
     checks++;
    }
    if(a.WorkerCalls!=b.WorkerCalls || a.ExpiryCalls!=b.ExpiryCalls) throw new Exception("Gameplay query mismatch");
    a.humanExtraCosts.Clear(); b.humanExtraCosts.Clear(); a.activeUnitLimits.Clear(); b.activeUnitLimits.Clear();
   }
   oldHelpers+=a.HelperCalls; newHelpers+=b.HelperCalls;
  }
  if(newHelpers>=oldHelpers) throw new Exception("No helper-call reduction");
  return "PASS: "+checks+" button states, configuration removal/panel replacement/disabled buttons/gates, identical gameplay query counts; helper calls "+oldHelpers+" -> "+newHelpers;
 }
}
}
'@
    Add-Type -TypeDefinition $code
    $type=($mod+'Audit.Test') -as [type]
    $results += $mod + ': ' + $type::Run()
}
$results
