$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$current = [IO.File]::ReadAllText((Join-Path $root 'APIShared/src/UnitHudPresentationCapability.cs'))
$before = (git -C $root show HEAD:APIShared/src/UnitHudPresentationCapability.cs) -join "`r`n"
if ($LASTEXITCODE -ne 0) { throw 'Cannot read reference source.' }
function Block([string]$text, [string]$marker) {
    $start=$text.IndexOf($marker,[StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Missing $marker" }
    $open=$text.IndexOf('{',$start); $depth=1; $end=$open+1
    while ($depth -gt 0 -and $end -lt $text.Length) {
        if ($text[$end] -eq '{') { $depth++ }; if ($text[$end] -eq '}') { $depth-- }; $end++
    }
    $text.Substring($start,$end-$start)
}
$contracts=[IO.File]::ReadAllText((Join-Path $root 'APIShared/src/UnitHudContracts.cs'))
$code=@'
using System;
using System.Runtime.CompilerServices;
using Noesis;
namespace Noesis {
 public class ImageSource {}
 public struct Color { public uint Value; public static Color FromArgb(byte a,byte r,byte g,byte b) => new Color {Value=((uint)a<<24)|((uint)r<<16)|((uint)g<<8)|b}; }
 public class SolidColorBrush {public static int Created; public Color Color; public SolidColorBrush(Color c){Color=c;Created++;}}
 public class ImageBrush {public static int Created; public ImageSource Source; public ImageBrush(ImageSource s){Source=s;Created++;}}
 public class Border {public SolidColorBrush Background; public ImageBrush OpacityMask; public float Opacity; public bool IsHitTestVisible;}
}
'@
$code+=Block $contracts 'public sealed class UnitHudTint'
foreach($entry in @(@('Before',$before),@('After',$current))) {
    $code+='public class '+$entry[0]+" {`r`n"
    if ($entry[0] -eq 'After') {
        $code+='private static readonly ConditionalWeakTable<Border,TintCache> TintCaches = new ConditionalWeakTable<Border,TintCache>();'
        $code+=Block $current 'private sealed class TintCache'
    }
    $code+=Block $entry[1] 'private static void ApplyTint('
    $code+='public static void Apply(Border b,UnitHudTint t,ImageSource s) { ApplyTint(b,t,s); }}'
}
$code+=@'
public static class ResourceAudit {
 public static string Run() {
  var old=new Border(); var current=new Border(); var source=new ImageSource();
  var tint=new UnitHudTint(64,128,255,115);
  Before.Apply(old,tint,source); After.Apply(current,tint,source);
  long begin=GC.GetAllocatedBytesForCurrentThread();
  SolidColorBrush.Created=ImageBrush.Created=0;
  for(int i=0;i<10000;i++) Before.Apply(old,tint,source);
  long oldBytes=GC.GetAllocatedBytesForCurrentThread()-begin;
  int oldBrushes=SolidColorBrush.Created+ImageBrush.Created;
  begin=GC.GetAllocatedBytesForCurrentThread(); SolidColorBrush.Created=ImageBrush.Created=0;
  for(int i=0;i<10000;i++) After.Apply(current,tint,source);
  long newBytes=GC.GetAllocatedBytesForCurrentThread()-begin;
  int newBrushes=SolidColorBrush.Created+ImageBrush.Created;
  if(oldBytes<=newBytes || newBrushes!=0)throw new Exception("Resources not reused");
  for(int i=0;i<8;i++) {
   var t=new UnitHudTint((byte)(i*30),128,255,(byte)(i*20));
   ImageSource s=i%2==0 ? source : null;
   Before.Apply(old,t,s); After.Apply(current,t,s);
   if(old.Background.Color.Value!=current.Background.Color.Value || old.Opacity!=current.Opacity ||
      old.OpacityMask?.Source!=current.OpacityMask?.Source || old.IsHitTestVisible!=current.IsHitTestVisible)
      throw new Exception("Visual state differs");
  }
  current.Background=null; current.OpacityMask=null;
  After.Apply(current,tint,source); Before.Apply(old,tint,source);
  if(current.Background.Color.Value!=old.Background.Color.Value || current.OpacityMask.Source!=source)
    throw new Exception("External overwrite not repaired");
  return "PASS: 10000 stable tint updates: before="+oldBrushes+" brush allocations / "+oldBytes+" bytes; after="+newBrushes+" / "+newBytes+" bytes. Changed inputs, null images and external overwrites preserve results (managed stand-ins).";
 }
}
'@
Add-Type -TypeDefinition $code -Language CSharp
[ResourceAudit]::Run()
