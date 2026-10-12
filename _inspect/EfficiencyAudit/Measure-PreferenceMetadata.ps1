$ErrorActionPreference = 'Stop'
$workspace = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$before = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'PreferenceCodec.before.txt'))
$after = [IO.File]::ReadAllText((Join-Path $workspace 'ExtendedData/src/TypedPreferenceSnapshotCodec.cs'))
$code = 'using System; using System.IO; using System.Linq; using System.Collections.Generic; using System.Reflection; namespace PreferenceMetadataAudit {'
foreach ($sample in @(@{Name='Before';Text=$before}, @{Name='After';Text=$after})) {
    $start = $sample.Text.IndexOf('        private static PropertyInfo[] Properties(Type type)', [StringComparison]::Ordinal)
    $end = $sample.Text.IndexOf('        private static void ValidateType(', $start, [StringComparison]::Ordinal)
    if ($start -lt 0 -or $end -lt 0) { throw 'Missing production method boundary.' }
    $method = $sample.Text.Substring($start, $end - $start)
    $code += 'public static class ' + $sample.Name + 'Codec {'
    if ($sample.Name -eq 'After') {
        $code += 'private static readonly object PropertyCacheLock=new object(); private static readonly Dictionary<Type,PropertyInfo[]> PropertyCache=new Dictionary<Type,PropertyInfo[]>();'
    }
    $code += $method + 'public static PropertyInfo[] Read(Type type)=>Properties(type); }'
}
$code += 'public sealed class Preferences {'
for ($index = 0; $index -lt 20; $index++) { $code += 'public int Value' + $index + ' {get;set;} ' }
$code += @'
}
public static class Measurement {
 static long Measure(Func<Type,PropertyInfo[]> read) { Type type=typeof(Preferences); object last=null; for(int i=0;i<1000;i++) last=read(type); long start=GC.GetAllocatedBytesForCurrentThread(); for(int i=0;i<10000;i++) last=read(type); long bytes=GC.GetAllocatedBytesForCurrentThread()-start; GC.KeepAlive(last); return bytes; }
 public static string Run() { var before=BeforeCodec.Read(typeof(Preferences)); var after=AfterCodec.Read(typeof(Preferences)); if(!before.Select(x=>x.Name).SequenceEqual(after.Select(x=>x.Name))) throw new Exception("Canonical ordering changed"); long oldBytes=Measure(BeforeCodec.Read), newBytes=Measure(AfterCodec.Read); if(newBytes!=0 || oldBytes<=newBytes) throw new Exception("Metadata allocation regression"); return "properties=20, lookups=10000, metadataBytes="+oldBytes+" -> "+newBytes+"; canonical ordering identical"; }
}
}
'@
Add-Type -TypeDefinition $code
[PreferenceMetadataAudit.Measurement]::Run()
