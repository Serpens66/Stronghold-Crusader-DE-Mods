$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$base=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\')
$workspace=[IO.Path]::GetFullPath((Join-Path $base '../..')).TrimEnd('\')
if($base -cne (Join-Path $workspace '_inspect/DependencyBootstrapProbe').Replace('/','\')){throw 'Unexpected evidence root.'}
$archiveRoot=Join-Path $base 'compacted'
[IO.Directory]::CreateDirectory($archiveRoot)|Out-Null
function Digest([string]$Path){$s=[IO.File]::OpenRead($Path);$h=[Security.Cryptography.SHA256]::Create();try{[BitConverter]::ToString($h.ComputeHash($s)).Replace('-','')}finally{$s.Dispose();$h.Dispose()}}
function Inventory([string]$Directory){
    foreach($entry in Get-ChildItem -LiteralPath $Directory -Force){
        if($entry.Attributes -band [IO.FileAttributes]::ReparsePoint){$entry}
        elseif($entry.PSIsContainer){Inventory $entry.FullName}
        else{$entry}
    }
}
foreach($name in @('package-lifecycle','lifecycle-implementation')){
    $root=[IO.Path]::GetFullPath((Join-Path $base $name))
    if(-not $root.StartsWith($base+'\',[StringComparison]::Ordinal) -or ((Get-Item -LiteralPath $root).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Unsafe fixture root.'}
    $zipPath=Join-Path $archiveRoot ($name+'-deduplicated.zip')
    if(Test-Path -LiteralPath $zipPath){throw 'Archive already exists; preserve it.'}
    $entries=@(Inventory $root)
    $records=@(foreach($entry in $entries){
        $relative=$entry.FullName.Substring($root.Length+1)
        $linked=($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
        [pscustomobject]@{Path=$relative;Link=$linked;Target=if($linked){@($entry.Target)}else{@()};SHA256=if($linked){''}else{Digest $entry.FullName};Length=if($linked){0}else{$entry.Length}}
    })
    $unique=@{}
    $zip=[IO.Compression.ZipFile]::Open($zipPath,[IO.Compression.ZipArchiveMode]::Create)
    try{
        foreach($row in $records|Where-Object {-not $_.Link}){
            if(-not $unique.ContainsKey($row.SHA256)){
                [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $root $row.Path),('objects/'+$row.SHA256),[IO.Compression.CompressionLevel]::Optimal)|Out-Null
                $unique[$row.SHA256]=$true
            }
        }
        $stream=$zip.CreateEntry('manifest.json').Open();$writer=New-Object IO.StreamWriter($stream)
        try{$writer.Write(($records|ConvertTo-Json -Depth 5))}finally{$writer.Dispose()}
    }finally{$zip.Dispose()}
    # Verify every stored object before removing any fixture, including data retained only once.
    $zip=[IO.Compression.ZipFile]::OpenRead($zipPath)
    try{
        $reader=New-Object IO.StreamReader($zip.GetEntry('manifest.json').Open())
        try{$stored=@($reader.ReadToEnd()|ConvertFrom-Json)}finally{$reader.Dispose()}
        if(($stored|ConvertTo-Json -Depth 5) -cne ($records|ConvertTo-Json -Depth 5)){throw 'Archive index verification failed.'}
        foreach($hash in $unique.Keys){$stream=$zip.GetEntry('objects/'+$hash).Open();$sha=[Security.Cryptography.SHA256]::Create();try{if([BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') -cne $hash){throw 'Archive verification failed.'}}finally{$stream.Dispose();$sha.Dispose()}}
        foreach($row in $records|Where-Object {-not $_.Link}){if((Digest (Join-Path $root $row.Path)) -cne $row.SHA256){throw 'Fixture changed while archiving.'}}
        if(@(Inventory $root).Count -ne $records.Count){throw 'Fixture inventory changed.'}
        # Delete links themselves, never recursively traverse their targets.
        foreach($row in $records|Where-Object Link){
            $path=[IO.Path]::GetFullPath((Join-Path $root $row.Path))
            if(-not $path.StartsWith($root+'\') -or -not ((Get-Item -LiteralPath $path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Changed link boundary.'}
            if((Get-Item -LiteralPath $path -Force).PSIsContainer){[IO.Directory]::Delete($path,$false)}else{Remove-Item -LiteralPath $path -Force}
        }
        if(@(Inventory $root|Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){throw 'Remaining links.'}
        if($root -cne (Join-Path $base $name)){throw 'Deletion target changed.'}
        Remove-Item -LiteralPath $root -Recurse -Force
        [IO.Directory]::CreateDirectory($root)|Out-Null
        # Keep useful reports directly readable at their previous paths; payloads and sources are in the archive.
        foreach($row in $records|Where-Object {-not $_.Link -and ([IO.Path]::GetExtension($_.Path) -in @('.md','.log') -or [IO.Path]::GetFileName($_.Path) -match '^(results|provenance|sources|installed-before|installed-after|probe-source-hashes)\.json$')}){
            $target=[IO.Path]::GetFullPath((Join-Path $root $row.Path))
            if(-not $target.StartsWith($root+'\')){throw 'Restore outside root.'}
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($zip.GetEntry('objects/'+$row.SHA256),$target)
        }
    }finally{$zip.Dispose()}
    $records|Export-Csv -LiteralPath (Join-Path $archiveRoot ($name+'-index.csv')) -NoTypeInformation -Encoding UTF8
    $summary=[ordered]@{FixtureRoot=$name;OriginalFiles=@($records|Where-Object {-not $_.Link}).Count;OriginalBytes=($records|Measure-Object Length -Sum).Sum;UniqueObjects=$unique.Count;ArchiveBytes=(Get-Item -LiteralPath $zipPath).Length;ArchiveSHA256=Digest $zipPath;LinksRecordedNotFollowed=@($records|Where-Object Link).Count}
    $summary|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $archiveRoot ($name+'-summary.json')) -Encoding UTF8
    $summary|ConvertTo-Json
}
