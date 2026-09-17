$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$current = [IO.File]::ReadAllText((Join-Path $root 'APIShared/src/UnitHudPresentationCapability.cs'))
$before = (git -C $root show HEAD:APIShared/src/UnitHudPresentationCapability.cs) -join "`r`n"
if ($LASTEXITCODE -ne 0) { throw 'Cannot read reference source.' }
function RenderBlock([string]$source) {
    $start = $source.IndexOf('private void OnBeforeRender()', [StringComparison]::Ordinal)
    if ($start -lt 0) { throw 'Missing render method.' }
    $end = $source.IndexOf('{', $start) + 1; $depth = 1
    while ($depth -gt 0 -and $end -lt $source.Length) {
        if ($source[$end] -eq '{') { $depth++ }; if ($source[$end] -eq '}') { $depth-- }; $end++
    }
    $source.Substring($start, $end - $start)
}
$code = @'
using System;
[Flags] public enum UnitHudSurface { None=0, TroopSelection=1, ControlGroups=2, UnitHover=4, ArmyReport=8, Recruitment=16, UnitDetails=32, All=63 }
public static class Time {
 public static int Frame, Reads;
 public static int frameCount { get { Reads++; return Frame; } }
}
public class MainViewModel {
 public static bool viewModelLoaded=true;
 public static int Reads;
 private static readonly MainViewModel instance=new MainViewModel();
 public static MainViewModel Instance { get { Reads++; return instance; } }
 public object HUDmain=new object();
 public bool Show_HUD_Troops=true, Show_HUD_ControlGroups=true;
 public Panel HUDTroopPanel=new Panel(), HUDControlGroups=new Panel();
 public class Panel {public void SetupSelectedTroops(){} public void Update(){}}
}
public abstract class Fixture {
 protected UnitHudSurface activeSurfaces, restoreSurfaces;
 protected bool pendingPresentation, refreshRequested, restoreImages, activeImages;
 protected object recruitmentLease;
 protected int lastFrame=-1;
 protected readonly object sync=new object();
 protected bool hasSpriteContext, updateSpritesActive, lastSpriteArabic;
 protected int lastSpriteColour;
 public int Areas, Restores, Expiries;
 protected bool HasCategories(UnitHudSurface s) => (activeSurfaces&s)!=0;
 protected bool HasRenderedTroopSelectionChanged() => false;
 protected bool IsImageOverrideContextReady() => true;
 protected void updateSpritesOriginal(MainViewModel m,int c,bool a){}
 protected void ApplyImageOverrides(MainViewModel m,int c,bool a){}
 protected void TryApplyFrameArea(string name,Action action,Action cleanup=null){Areas++;action();}
 protected void ApplyHover(MainViewModel m){}
 protected void ApplyArmyReport(MainViewModel m){}
 protected void ApplyRecruitmentPresentation(MainViewModel m){}
 protected void ApplyUnitDetails(MainViewModel m){}
 protected void HideArmyHosts(){}
 protected void HideRecruitmentControls(){}
 protected void HideUnitDetailControls(){}
 protected void ExpireRecruitment(){if(recruitmentLease!=null){Expiries++;recruitmentLease=null;}}
 protected void RestorePresentation(MainViewModel m,UnitHudSurface s,bool images){if(s!=UnitHudSurface.None||images)Restores++;}
 public void Configure(bool active,bool pending=false,bool ticket=false,bool images=false){
  activeSurfaces=active?UnitHudSurface.All:UnitHudSurface.None;
  pendingPresentation=refreshRequested=pending;
  restoreSurfaces=pending?UnitHudSurface.All:UnitHudSurface.None;
  recruitmentLease=ticket?new object():null; activeImages=images;
 }
 public abstract void Frame();
}
'@
foreach ($entry in @(@('Before', $before), @('After', $current))) {
    $code += 'public sealed class ' + $entry[0] + ' : Fixture {'
    $code += RenderBlock $entry[1]
    $code += 'public override void Frame(){OnBeforeRender();}}'
}
$code += @'
public static class DispatchAudit {
 private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 private static string Measure(Fixture f,string label,bool active,bool image=false){
  f.Configure(active,images:image); Time.Reads=MainViewModel.Reads=0;
  for(int i=0;i<10000;i++){Time.Frame=i;f.Frame();}
  return label+": Unity frame reads="+Time.Reads+", ViewModel resolutions="+MainViewModel.Reads+", area dispatches="+f.Areas;
 }
 public static string Run(){
  string oldIdle=Measure(new Before(),"Before/no registrations",false);
  var idle=new After(); string newIdle=Measure(idle,"After/no registrations or passive owner",false);
  Check(Time.Reads==0 && MainViewModel.Reads==0 && idle.Areas==0,"idle work");
  string oldRegistered=Measure(new Before(),"Before/registered but unused category",true);
  string newInactive=Measure(new After(),"After/registered inactive category",false);
  var image=new After(); string imageOnly=Measure(image,"After/image-only, refresh completed",false,true);
  Check(Time.Reads==0 && MainViewModel.Reads==0 && image.Areas==0,"image-only work");
  var active=new After(); Measure(active,"active",true);
  Check(active.Areas==50000 && MainViewModel.Reads==10000,"active frame cadence retained");
  var restored=new After(); restored.Configure(false,true); Time.Frame=10001; restored.Frame();
  Check(restored.Restores==1,"queued restoration");
  Time.Reads=MainViewModel.Reads=0; int areas=restored.Areas;
  for(int i=0;i<10000;i++){Time.Frame++;restored.Frame();}
  Check(Time.Reads==0 && MainViewModel.Reads==0 && restored.Areas==areas,"idle after restoration");
  MainViewModel.viewModelLoaded=false;
  var delayed=new After(); delayed.Configure(false,true); Time.Frame++; delayed.Frame();
  Check(delayed.Restores==0,"restoration waits for ready HUD");
  MainViewModel.viewModelLoaded=true; Time.Frame++; delayed.Frame();
  Check(delayed.Restores==1,"delayed restoration completes");
  MainViewModel.viewModelLoaded=false;
  var ticket=new After(); ticket.Configure(false,ticket:true); Time.Frame++; ticket.Frame();
  Check(ticket.Expiries==1 && ticket.Areas==0,"inactive ticket maintenance before HUD readiness");
  MainViewModel.viewModelLoaded=true;
  return string.Join(Environment.NewLine,new[]{oldIdle,newIdle,oldRegistered,newInactive,imageOnly,
   "PASS: production render dispatch with managed stand-ins; active cadence, restoration, late readiness and ticket maintenance."});
 }
}
'@
Add-Type -TypeDefinition $code -Language CSharp
[DispatchAudit]::Run()
