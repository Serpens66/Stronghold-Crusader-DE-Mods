$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$old=(& git -C $root show 7e048e6435a72b339dff35f3dcd368210f04ccdb:APIShared/src/UnitHudPresentationCapability.cs) -join "`r`n"
$new=[IO.File]::ReadAllText((Join-Path $root 'APIShared/src/UnitHudPresentationCapability.cs'))
function Slice([string]$text,[string]$first,[string]$next) { $a=$text.IndexOf($first); $b=$text.IndexOf($next,$a); if($a -lt 0 -or $b -le $a){throw "Missing $first -> $next"}; return $text.Substring($a,$b-$a) }
function Block([string]$text,[string]$marker) { $a=$text.IndexOf($marker); if($a -lt 0){throw "Missing $marker"}; $open=$text.IndexOf('{',$a); $depth=1; $b=$open+1; while($depth){if($text[$b] -eq '{'){$depth++}; if($text[$b] -eq '}'){$depth--}; $b++}; return $text.Substring($a,$b-$a) }
$contracts=[IO.File]::ReadAllText((Join-Path $root 'APIShared/src/UnitHudContracts.cs'))
$enumSource=[IO.File]::ReadAllText((Join-Path $root 'shcde-script-extender/src/SHCDESE.BepInEx/Interop/Enums.cs'))
$code=@'
using System;
using System.Collections.Generic;
using System.Linq;
using Noesis;
using SHCDESE.Interop;
namespace Noesis { public class ImageSource {} }
namespace APIShared { public class NativeCapabilityDiagnostic {} }
namespace SHCDESE.Interop {
'@
$code+=(Block $enumSource 'public enum eChimps')+"`n}`n"+$contracts.Substring($contracts.IndexOf('namespace APIShared'))
$code+=@'
namespace APIShared {
public static class EngineInterface { public class PlayState { public int numSelectedChimps; public int[] selectedChimps,selectedChimpTypes; } }
public class GameData { public static GameData Instance=new GameData(); public EngineInterface.PlayState lastGameState; }
public static class NativeApiLog { public static void Error(object log,string message) {} }
public static class UnitStore { public static Dictionary<int,UnitHudUnitSnapshot> Units=new Dictionary<int,UnitHudUnitSnapshot>(); }
'@
foreach($optimized in @($false,$true)) {
 $text=if($optimized){$new}else{$old}; $name=if($optimized){'NewHud'}else{'OldHud'}
 $code+="`npublic class $name {`n"
 $code+=Slice $text '        private readonly object sync' '        private readonly HashSet<string> loggedCategoryConflicts'
 $code+=@'
 private readonly HashSet<string> loggedCategoryConflicts=new HashSet<string>();
 private object log;
 private bool refreshRequested;
 public int MatcherErrors;
 private void LogCallbackFailure(string area,Exception ex) {MatcherErrors++;}
 private static bool TryCapture(int id,out UnitHudUnitSnapshot unit) {return UnitStore.Units.TryGetValue(id,out unit);}
 private bool Fail(string message,out NativeCapabilityDiagnostic diagnostic) {diagnostic=new NativeCapabilityDiagnostic(); return false;}
 private static NativeCapabilityDiagnostic Available(string message) {return new NativeCapabilityDiagnostic();}
 public bool Register(string owner,string id,UnitHudCategoryMatcher matcher) {return RegisterCategory(owner,new UnitHudCategoryDefinition(id,id,(int)eChimps.CHIMP_TYPE_ARCHER,UnitHudSurface.All),matcher,out _);}
 public IReadOnlyList<UnitHudCategorySnapshot> Capture() {return CaptureSelectedCategories();}
 public object Registrations() {return CategoryCopy();}
'@
 $code+=Slice $text '        private bool RegisterCategory(' '        private bool CompleteRecruitment('
 $code+=Slice $text '        private void SortRegistrations()' '        private bool Fail('
 $code+=Slice $text '        private List<DisplayEntry> BuildEntries(' '        private bool HasRenderedTroopSelectionChanged()'
 $code+=Slice $text '        private CategoryRegistration Classify(' '        private IReadOnlyList<UnitHudControlGroupSnapshot> CaptureControlGroups()'
 $code+=Slice $text '        private string ResolveText(' '        private void '
 if($optimized){ $code+=Slice $text '        private HudWorkBuffers RentHudBuffers()' '        private sealed class CategoryRegistration' }
 foreach($declaration in @('        private sealed class CategoryRegistration','        private sealed class InteractionRegistration','        private sealed class ImageRegistration','        private sealed class RecruitmentRegistration','        private sealed class DisplayEntry')) { $code+=(Block $text $declaration)+"`n" }
 if($optimized) {
  $code+=@'
 public string Entries(List<UnitHudUnitSnapshot> selected,int[] counts,bool complete) {
  var buffers=RentHudBuffers();
  try {return Format(BuildEntries(selected,counts,complete,UnitHudSurface.TroopSelection,buffers));}
  finally {buffers.Clear();lock(sync)hudBufferPool.Push(buffers);}
 }
 public void CheckPool() {
  foreach(var b in hudBufferPool) if(b.Selected.Count!=0 || b.Grouped.Count!=0 || b.Entries.Count!=0 || b.Slots.Count!=0 || b.Categories.Count!=0 || b.Seen.Count!=0 || b.ClaimedIds.Count!=0 || b.Reduction.Any(x=>x!=0) || b.EffectiveCounts.Any(x=>x!=0)) throw new Exception("Dirty HUD buffer");
 }
'@
 } else { $code+='public string Entries(List<UnitHudUnitSnapshot> selected,int[] counts,bool complete) {return Format(BuildEntries(selected,counts,complete,UnitHudSurface.TroopSelection));}' }
 $code+='private static string Format(List<DisplayEntry> entries) {return string.Join("|",entries.Select(x=>x.VanillaType+":"+x.Count+":"+(x.Category==null ? "vanilla" : x.Category.Key)+":"+(x.Units==null ? "" : string.Join(",",x.Units.Select(u=>u.GameId)))));}'
 $code+="`n}`n"
}
$code+=(Block $new '    internal static class UnitHudSelectionPolicy')
$code+=@'
public static class HudAudit {
 static string Describe(IReadOnlyList<UnitHudCategorySnapshot> rows) {return string.Join("|",rows.Select(x=>x.OwnerGuid+":"+x.CategoryId+":"+string.Join(",",x.Units.Select(u=>u.GameId))));}
 public static string Run() {
  long oldBytes=0,newBytes=0;
  for(int scenario=0;scenario<7;scenario++) {
   var old=new OldHud(); var current=new NewHud();
   bool nestedOld=false,nestedNew=false;
   UnitHudCategoryMatcher a=u=> {if(scenario==4) throw new Exception("expected"); if(scenario==5 && !nestedOld) {nestedOld=true;old.Capture();} if(scenario==6 && !nestedOld) {nestedOld=true;old.Register("other","later",x=>false);} return u.GameId%2==0;};
   UnitHudCategoryMatcher b=u=> {if(scenario==4) throw new Exception("expected"); if(scenario==5 && !nestedNew) {nestedNew=true;current.Capture();} if(scenario==6 && !nestedNew) {nestedNew=true;current.Register("other","later",x=>false);} return u.GameId%2==0;};
   old.Register("one","a",a); current.Register("one","a",b);
   old.Register("two","b",u=>u.GameId%2!=0); current.Register("two","b",u=>u.GameId%2!=0);
   var selected=new List<UnitHudUnitSnapshot>(); UnitStore.Units.Clear();
   for(int i=1;i<=20;i++) {var u=new UnitHudUnitSnapshot(i,(uint)(100+i),(int)eChimps.CHIMP_TYPE_ARCHER,1,true); selected.Add(u);UnitStore.Units.Add(i,u);}
   var state=new EngineInterface.PlayState {numSelectedChimps=20,selectedChimps=selected.Select(x=>x.GameId).ToArray(),selectedChimpTypes=selected.Select(x=>x.VanillaType).ToArray()};
   if(scenario==1) state.selectedChimps[4]=state.selectedChimps[0];
   if(scenario==2) state.selectedChimpTypes[4]++;
   if(scenario==3) state.selectedChimps=null;
   GameData.Instance.lastGameState=state;
   var before=old.Capture();var after=current.Capture(); string retained=Describe(after);
   if(Describe(before)!=retained || old.MatcherErrors!=current.MatcherErrors) throw new Exception("Capture mismatch "+scenario);
   var counts=new int[128]; counts[(int)eChimps.CHIMP_TYPE_ARCHER]=20;
   foreach(bool complete in new[]{false,true}) if(old.Entries(selected,counts,complete)!=current.Entries(selected,counts,complete)) throw new Exception("Entries mismatch "+scenario);
   current.Capture();current.CheckPool();if(Describe(after)!=retained)throw new Exception("Snapshot mutated");
   if(scenario==0) {
    object view=current.Registrations(); if(!ReferenceEquals(view,current.Registrations()))throw new Exception("Repeated registry copy");
    current.Register("third","c",u=>false);if(ReferenceEquals(view,current.Registrations()))throw new Exception("Registry not republished");
    old.Register("third","c",u=>false);
    for(int n=0;n<10;n++){old.Capture();current.Capture();}
    long start=GC.GetAllocatedBytesForCurrentThread();for(int n=0;n<1000;n++)old.Capture();oldBytes=GC.GetAllocatedBytesForCurrentThread()-start;
    start=GC.GetAllocatedBytesForCurrentThread();for(int n=0;n<1000;n++)current.Capture();newBytes=GC.GetAllocatedBytesForCurrentThread()-start;
   }
  }
  if(newBytes>=oldBytes)throw new Exception("No HUD allocation reduction");
  return "PASS: 7 scenarios, grouping/order/partial selection/matcher errors/reentrant capture/reentrant registration; public snapshots retained; pooled lists cleared. 1000 captures of 20 units: "+oldBytes+" -> "+newBytes+" bytes.";
 }
}
}
'@
Add-Type -TypeDefinition $code -IgnoreWarnings
[APIShared.HudAudit]::Run()
