param(
    [Parameter(Mandatory = $true)][string]$MapPath,
    [Parameter(Mandatory = $true)][string]$AivDirectory
)

$ErrorActionPreference = 'Stop'
$package = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..\CastlePlanner\BepInEx\plugins\CastlePlanner_Serp')).Path
foreach ($name in @('MapParser.Core.dll', 'AIVParser.Core.dll', 'AIVPlacement.Core.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $package $name))
}

$document = [MapParser.Core.MapFileReader]::Parse($MapPath)
$slots = @($document.ReadKeepAnchors().Slots | Where-Object { $_.IsSelectable -and $_.Status.ToString() -eq 'Exact' })
$blueprints = @(
    foreach ($lord in @('sentinel', 'wolf', 'nizar', 'marshal', 'emir')) {
        foreach ($variant in 1..8) {
            $path = Join-Path $AivDirectory ($lord + $variant + '.aivjson')
            if (-not (Test-Path -LiteralPath $path)) { continue }
            $loaded = [AIVParser.Core.AivJsonFileLoader]::Load($path)
            $parsed = [AIVParser.Core.AivBlueprintParser]::new().Parse($loaded.Document, $path, $loaded.Diagnostics)
            if ($parsed.IsValid -and @($parsed.Blueprint.Frames | Where-Object { $_.Mapper.Value -eq 87 }).Count -gt 0) {
                [pscustomobject]@{ Lord = $lord; Variant = $variant; Blueprint = $parsed.Blueprint }
            }
        }
    }
)

$evaluator = [AIVPlacement.Core.AivPlacementEvaluator]::new()
$projector = [AIVPlacement.Core.AivCastleProjector]::new()
$results = @(
    foreach ($first in $slots) {
        foreach ($second in $slots) {
            if ($first.SlotIndex -eq $second.SlotIndex) { continue }
            $others = @($slots | Where-Object { $_.SlotIndex -ne $first.SlotIndex -and $_.SlotIndex -ne $second.SlotIndex })
            if ($others.Count -eq 0) { continue }
            $human = $others | Sort-Object {
                -([Math]::Pow($_.Coordinate.X - $first.Coordinate.X, 2) +
                  [Math]::Pow($_.Coordinate.Y - $first.Coordinate.Y, 2))
            } | Select-Object -First 1
            $state = [AIVPlacement.Core.AivPreplacementMapState]::Create($document, [int[]]@($human.SlotIndex))
            $initial = [AIVPlacement.Core.AivInitialRotationResolver]::ResolveMapFacing($first.Coordinate)

            $target = $second.Coordinate
            $cleanup = [Collections.Generic.HashSet[string]]::new()
            $regions = @(
                @{ X = $target.X; Y = $target.Y; Size = 7 },
                @{ X = $target.X; Y = $target.Y + 8; Size = 7 },
                @{ X = $target.X + 7; Y = $target.Y + 2; Size = 5 }
            )
            foreach ($region in $regions) {
                for ($y = $region.Y; $y -lt $region.Y + $region.Size; $y++) {
                    for ($x = $region.X; $x -lt $region.X + $region.Size; $x++) {
                        [void]$cleanup.Add("$x,$y")
                    }
                }
            }

            foreach ($item in $blueprints) {
                $selection = $evaluator.EvaluateAllRotations($state, $item.Blueprint, $first.Coordinate, $initial)
                if ($selection.Status -notin @(
                    [AIVPlacement.Core.AivPlacementStatus]::Complete,
                    [AIVPlacement.Core.AivPlacementStatus]::Partial)) { continue }
                $castle = $projector.Project($item.Blueprint, $first.Coordinate, $selection.BestVariant.Rotation)
                $linkTiles = @($castle.Elements |
                    Where-Object { $_.Mapper.Value -eq 87 } |
                    ForEach-Object { $_.OccupiedTiles })
                $overlap = @($linkTiles | Where-Object {
                    $cleanup.Contains("$($_.MapCoordinate.X),$($_.MapCoordinate.Y)")
                }).Count
                if ($overlap -eq 0) { continue }
                [pscustomobject]@{
                    Map = [IO.Path]::GetFileName($MapPath)
                    FirstSlot = $first.SlotIndex
                    SecondSlot = $second.SlotIndex
                    HumanSlot = $human.SlotIndex
                    Lord = $item.Lord
                    Variant = $item.Variant
                    Rotation = $selection.BestVariant.Rotation
                    FitPercent = $selection.BestVariant.Score.FitPercentage
                    FirstBlockedBuildStep = $selection.BestVariant.FirstBlockingBuildStep
                    LinkBuildSteps = (@($castle.Elements | Where-Object { $_.Mapper.Value -eq 87 } | ForEach-Object { $_.BuildIndex }) -join ',')
                    LinkedMapperOverlap = $overlap
                }
            }
        }
    }
)

$results | Sort-Object -Property @{ Expression = 'LinkedMapperOverlap'; Descending = $true }, Map, FirstSlot, SecondSlot, Lord, Variant |
    Select-Object Map, FirstSlot, SecondSlot, HumanSlot, Lord, Variant, Rotation, FitPercent, FirstBlockedBuildStep, LinkBuildSteps, LinkedMapperOverlap |
    Format-List
Write-Output "positive=$($results.Count)"
