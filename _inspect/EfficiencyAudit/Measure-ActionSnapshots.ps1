$ErrorActionPreference = 'Stop'
$workspace = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$before = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ActionButtons.before.txt'))
$after = [IO.File]::ReadAllText((Join-Path $workspace 'APIShared/src/Presentation/UnitHud/UnitHudPresentationService.ActionButtons.cs'))
function Read-Range([string]$text, [string]$startMarker, [string]$endMarker) {
    $start = $text.IndexOf($startMarker, [StringComparison]::Ordinal)
    $end = $text.IndexOf($endMarker, $start, [StringComparison]::Ordinal)
    if ($start -lt 0 -or $end -lt 0) { throw "Missing source boundary: $startMarker" }
    return $text.Substring($start, $end - $start)
}
$code = @'
using System;
using System.Collections.Generic;
using System.Linq;
namespace EfficiencyAudit {
public interface IUnitHudActionButtonRegistration {}
public sealed class UnitHudActionButtonDefinition { public readonly string ButtonId, Tooltip; public UnitHudActionButtonDefinition(string id) { ButtonId=id; Tooltip="Initial"; } }
'@
foreach ($sample in @(@{Name='Before';Text=$before}, @{Name='After';Text=$after})) {
    $name = $sample.Name + 'Service'
    $methods = Read-Range $sample.Text '        private ActionButtonState[] CaptureActionButtons()' '        private bool TryGetActionContext('
    $marker = if ($sample.Name -eq 'After') { '        private sealed class ActionButtonSnapshot' } else { '        private sealed class ActionButtonState' }
    $classes = Read-Range $sample.Text $marker "`r`n    }`r`n}"
    $classes = $classes.Replace('UnitHudPresentationService', $name)
    $capture = if ($sample.Name -eq 'After') { 'return CaptureActionButtonSnapshot().Visible;' } else { 'return CaptureActionButtons().Where(x=>x.Visible).ToArray();' }
    $code += @"
public sealed class $name {
 private readonly object sync=new object();
 private readonly List<ActionButtonRegistration> actionButtons=new List<ActionButtonRegistration>();
 private bool pendingPresentation;
 private bool OwnerActive(string owner)=>true;
 public $name(int count, bool hidden) { for(int i=0;i<count;i++) { var item=new ActionButtonRegistration(this,"owner",new UnitHudActionButtonDefinition(i.ToString())); item.Visible=!hidden || (i%2==0); actionButtons.Add(item); } }
 public object Capture() { $capture }
"@
    if ($sample.Name -eq 'After') { $code += 'private ActionButtonSnapshot actionButtonSnapshot;' }
    $code += $methods + $classes + "`r`n}`r`n"
}
$code += @'
public static class SnapshotMeasurement {
 static long Measure(Func<object> capture) { object last=null; for(int i=0;i<1000;i++) last=capture(); long start=GC.GetAllocatedBytesForCurrentThread(); for(int i=0;i<10000;i++) last=capture(); long bytes=GC.GetAllocatedBytesForCurrentThread()-start; GC.KeepAlive(last); return bytes; }
 public static string Run() {
  var results=new List<string>();
  foreach(int count in new[]{0,1,10,50}) foreach(bool hidden in new[]{false,true}) {
   var before=new BeforeService(count,hidden); var after=new AfterService(count,hidden);
   long oldBytes=Measure(before.Capture), newBytes=Measure(after.Capture);
   if(newBytes!=0 || (count>0 && oldBytes<=newBytes)) throw new Exception("Snapshot allocation regression");
   results.Add("registrations="+count+", alternatingHidden="+hidden+", captures=10000, snapshotBytes="+oldBytes+" -> "+newBytes);
  }
  return string.Join(Environment.NewLine,results);
 }
}
}
'@
# Compile extracted production registry methods only. SDK/Noesis stand-ins do not
# model rendering or frame time; integration tests exercise the actual service.
Add-Type -TypeDefinition $code -IgnoreWarnings
[EfficiencyAudit.SnapshotMeasurement]::Run()
