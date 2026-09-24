param(
    [string]$MapDirectory = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\StreamingAssets\Maps',
    [string]$AivDirectory = (Join-Path $PSScriptRoot '..\..\CastlePlanner\BepInEx\plugins\CastlePlanner_Serp\VanillaAIV')
)

$ErrorActionPreference = 'Stop'
$package = (Resolve-Path (Join-Path $PSScriptRoot '..\..\CastlePlanner\BepInEx\plugins\CastlePlanner_Serp')).Path
foreach ($name in @('MapParser.Core.dll', 'AIVParser.Core.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $package $name))
}

$maps = @(
    [pscustomobject]@{
        Name = 'CrusadesCrossing.map'
        Hash = 'B5AB8BCC5C4C2783697EEF4BBE692AE829B2C7D59C4AFF540F79E140C49417FE'
        HumanSlot = 5
        Players = @(
            @{ LordType = 3; AivDefault = 7; KeepSlot = 0 },
            @{ LordType = 17; AivDefault = 2; KeepSlot = 1 },
            @{ LordType = 12; AivDefault = 6; KeepSlot = 2 },
            @{ LordType = 14; AivDefault = 7; KeepSlot = 3 },
            @{ LordType = 11; AivDefault = 1; KeepSlot = 4 },
            @{ LordType = 15; AivDefault = 2; KeepSlot = 6 },
            @{ LordType = 16; AivDefault = 5; KeepSlot = 7 }
        )
        Prefix = 'Crossing'
    },
    [pscustomobject]@{
        Name = 'Reed Sea.map'
        Hash = 'C6ABE1353E0EA1829B626408B4F3807E4473B428AE239FD15A52E9745FFF8116'
        HumanSlot = 1
        Players = @(
            @{ LordType = 3; AivDefault = 2; KeepSlot = 5 },
            @{ LordType = 17; AivDefault = 2; KeepSlot = 2 },
            @{ LordType = 12; AivDefault = 6; KeepSlot = 0 },
            @{ LordType = 14; AivDefault = 7; KeepSlot = 3 },
            @{ LordType = 11; AivDefault = 1; KeepSlot = 4 },
            @{ LordType = 15; AivDefault = 2; KeepSlot = 6 }
        )
        Prefix = 'Reed'
    }
)

$aivNames = @{
    3 = 'wolf'; 17 = 'sentinel'; 12 = 'nizar'; 14 = 'marshal'
    11 = 'emir'; 15 = 'abbot'; 16 = 'jewel'
}
$parser = [AIVParser.Core.AivBlueprintParser]::new()
$runs = [Collections.Generic.List[object]]::new()
foreach ($map in $maps) {
    $mapPath = Join-Path $MapDirectory $map.Name
    if (-not (Test-Path -LiteralPath $mapPath)) { throw "Map missing: $mapPath" }
    $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $mapPath).Hash
    if ($actualHash -cne $map.Hash) { throw "Map hash changed: $($map.Name) $actualHash" }
    $document = [MapParser.Core.MapFileReader]::Parse($mapPath)
    if ($document.Metadata.MaxPlayers -lt $map.Players.Count + 1) {
        throw "Map player cap is too small: $($map.Name)"
    }
    $slots = @($document.ReadKeepAnchors().Slots | Where-Object {
        $_.IsSelectable -and $_.Status.ToString() -eq 'Exact'
    })
    if ($slots.Count -ne $map.Players.Count + 1) {
        throw "Expected exact Keep slots for every player on $($map.Name)"
    }
    foreach ($ai in $map.Players) {
        $aivName = $aivNames[$ai.LordType] + $ai.AivDefault + '.aivjson'
        $aivPath = Join-Path $AivDirectory $aivName
        if (-not (Test-Path -LiteralPath $aivPath)) { throw "AIV missing: $aivPath" }
        $loaded = [AIVParser.Core.AivJsonFileLoader]::Load($aivPath)
        $parsed = $parser.Parse($loaded.Document, $aivPath, $loaded.Diagnostics)
        if (-not $parsed.IsValid) { throw "AIV invalid: $aivPath" }
    }
    foreach ($order in @('forward', 'reverse')) {
        $ordered = @($map.Players)
        if ($order -eq 'reverse') {
            $ordered[0], $ordered[1] = $ordered[1], $ordered[0]
        }
        foreach ($preBuild in @($false, $true)) {
            $players = [Collections.Generic.List[object]]::new()
            $assignments = @(@{ Human = $true; KeepSlot = $map.HumanSlot }) + $ordered
            for ($index = 0; $index -lt $assignments.Count; $index++) {
                $assignment = $assignments[$index]
                $anchor = @($slots | Where-Object SlotIndex -eq $assignment.KeepSlot)
                if ($anchor.Count -ne 1) { throw "Invalid Keep slot $($assignment.KeepSlot)" }
                $player = [ordered]@{
                    id = $index + 1
                    keepSlot = $assignment.KeepSlot
                    radarX = $anchor[0].RadarCoordinate.X
                    radarY = $anchor[0].RadarCoordinate.Y
                    keepX = $anchor[0].Coordinate.X
                    keepY = $anchor[0].Coordinate.Y
                }
                if ($index -eq 0) { $player.human = $true }
                else {
                    $player.lordType = $assignment.LordType
                    $player.aivDefault = $assignment.AivDefault
                }
                $players.Add($player)
            }
            $runId = '{0}-{1}-{2}' -f $map.Prefix, $order, $(if ($preBuild) { 'on' } else { 'off' })
            $runs.Add([ordered]@{
                id = $runId
                preBuild = $preBuild
                preset = [ordered]@{
                    enabled = $true
                    mapFileName = $map.Name
                    mapSha256 = $map.Hash
                    players = @($players)
                }
            })
        }
    }
}

$seriesId = 'aiv-natural-link-eight-20260924'
$series = [ordered]@{ enabled = $true; seriesId = $seriesId; runs = @($runs) }
$progress = [ordered]@{ seriesId = $seriesId; lastCompletedRunId = ''; nextIndex = 0 }
$encoding = [Text.UTF8Encoding]::new($false)
foreach ($output in @(
    @{ Name = 'AivLobbyNaturalLinkSeries.json'; Value = $series },
    @{ Name = 'AivLobbyNaturalLinkSeries.progress.json'; Value = $progress }
)) {
    $path = Join-Path $PSScriptRoot $output.Name
    $content = (($output.Value | ConvertTo-Json -Depth 20) -replace '\r?\n', [Environment]::NewLine) + [Environment]::NewLine
    [IO.File]::WriteAllText($path, $content, $encoding)
    if ([IO.File]::ReadAllText($path, $encoding) -cne $content) {
        throw "Written content differs: $path"
    }
    Write-Output "$($output.Name): $($content.Length) chars"
}
Write-Output "Validated $($runs.Count) starts on $($maps.Count) hash-checked maps."
