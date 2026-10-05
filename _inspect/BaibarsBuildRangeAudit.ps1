$ErrorActionPreference = 'Stop'
$nativePath = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
$expectedHash = 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2'
if ((Get-FileHash -LiteralPath $nativePath).Hash -ne $expectedHash) { throw 'Native hash changed' }
$bytes = [IO.File]::ReadAllBytes($nativePath)
$pe = [BitConverter]::ToInt32($bytes, 60)
$count = [BitConverter]::ToUInt16($bytes, $pe + 6)
$start = $pe + 24 + [BitConverter]::ToUInt16($bytes, $pe + 20)
$sections = @(for ($i = 0; $i -lt $count; $i++) {
    $s = $start + 40 * $i
    [pscustomobject]@{ VA = [BitConverter]::ToUInt32($bytes,$s+12); Size = [BitConverter]::ToUInt32($bytes,$s+16); Raw = [BitConverter]::ToUInt32($bytes,$s+20) }
})
function Offset([int]$rva) {
    foreach ($s in $sections) { if ($rva -ge $s.VA -and $rva -lt $s.VA+$s.Size) { return [int]($s.Raw+$rva-$s.VA) } }
    throw "RVA outside raw sections: $rva"
}
# Native 0x6A190 switch returns, indexed by the audited byte dispatch table.
$sizeReturns = @(2,4,3,5,2,6,1,7,11,9,10,13,0,8)
function Scale([int]$mapper) {
    if ($mapper -eq 106 -or $mapper -lt 44 -or $mapper -ge 456) { return 1 }
    $case = $bytes[(Offset (0x6a250+$mapper-44))]
    if ($case -ge $sizeReturns.Count) { return 1 }
    return [math]::Max(1, $sizeReturns[$case])
}
$directory = 'C:\Users\Serpens66\AppData\LocalLow\Firefly Studios\Stronghold Crusader Definitive Edition\ExtendedLords\Baibars'
foreach ($name in @('Nimrod1.aivjson','Nimrodwest.aivjson','Nimrodwest2.aivjson')) {
    $path = Join-Path $directory $name
    $aiv = Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
    $grid = New-Object int[] 10000
    for ($f=0; $f -lt $aiv.frames.Count; $f++) {
        $frame = $aiv.frames[$f]
        $size = Scale $frame.itemType
        foreach ($position in $frame.tilePositionOfsets) {
            # 0x53CA0 flips the serialized Y before painting positive-X/Y squares.
            $x = $position % 100
            $y = 99 - [int][math]::Floor($position / 100)
            for ($dy=0; $dy -lt $size; $dy++) { for ($dx=0; $dx -lt $size; $dx++) {
                if ($x+$dx -ge 100 -or $y+$dy -ge 100) { throw "Footprint outside raster: $name frame $($f+1)" }
                $grid[($y+$dy)*100+$x+$dx] = $f+1
            } }
        }
    }
    # The Keep's associated reserved areas do not intersect these remote targets.
    $keep = $aiv.frames[0].tilePositionOfsets[0]
    $keepX = $keep % 100
    $keepY = 99 - [int][math]::Floor($keep / 100)
    Write-Output "$name SHA256=$((Get-FileHash -LiteralPath $path).Hash)"
    for ($f=0; $f -lt $aiv.frames.Count; $f++) {
        $frame = $aiv.frames[$f]
        if ($frame.itemType -notin @(113,147)) { continue }
        $position = $frame.tilePositionOfsets[0]
        if ([math]::Abs($position%100-$keepX) -lt 60) { continue }
        $size = Scale $frame.itemType
        $x = $position%100
        $y = 99-[int][math]::Floor($position/100)
        $surviving = 0
        for ($dy=0; $dy -lt $size; $dy++) { for ($dx=0; $dx -lt $size; $dx++) {
            if ($grid[($y+$dy)*100+$x+$dx] -eq $f+1) { $surviving++ }
        } }
        $case = $bytes[(Offset (0x79914+$frame.itemType-51))]
        $branch = [BitConverter]::ToUInt32($bytes,(Offset (0x798f4+4*$case)))
        if ($branch -ne 0x7833c) { throw 'Unexpected placement branch' }
        $distances = @(foreach ($rotation in @(0,2,4,6)) {
            # Rotate full square bounds; native Keep construction receives the same rotation.
            switch ($rotation) {
                0 { $rx=$x; $ry=$y; $kx=$keepX; $ky=$keepY; $ox=3; $oy=7 }
                2 { $rx=$y; $ry=100-$x-$size; $kx=$keepY; $ky=93-$keepX; $ox=-1; $oy=3 }
                4 { $rx=100-$x-$size; $ry=100-$y-$size; $kx=93-$keepX; $ky=93-$keepY; $ox=3; $oy=-1 }
                6 { $rx=100-$y-$size; $ry=$x; $kx=93-$keepY; $ky=$keepX; $ox=7; $oy=3 }
            }
            $maxDistance = 0
            for ($dy=0; $dy -lt $size; $dy++) { for ($dx=0; $dx -lt $size; $dx++) {
                $d=[math]::Max([math]::Abs($rx+$dx-$kx-$ox),[math]::Abs($ry+$dy-$ky-$oy))
                $maxDistance=[math]::Max($maxDistance,$d)
            } }
            $maxDistance
        })
        [pscustomobject]@{ Frame=$f+1; Mapper=$frame.itemType; Size=$size; SurvivingCells=$surviving; ExpectedCells=$size*$size; RequiredRangesByRotation=$distances; Default400Range=70; Default500Range=80 } | ConvertTo-Json -Compress
    }
}
