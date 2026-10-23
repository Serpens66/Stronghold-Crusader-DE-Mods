[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
# Behavior test of the shared Vanilla close-range rule that lets ExtraFeatures skip
# gatehouse queries whose answer cannot close the gate (GATEHOUSE_QUERY_PASS.md).
$policy = [IO.File]::ReadAllText((Join-Path $PSScriptRoot '..\Shared\Runtime\Gameplay\GatehouseQueryUnitIdPolicy.cs'))
Add-Type -TypeDefinition ($policy.Replace('internal static class', 'public static class').Replace('internal static bool', 'public static bool').Replace('internal const', 'public const'))
$type = [Shared.GatehouseQueryUnitIdPolicy]
function Assert-Range([int]$gx, [int]$gy, [int]$ux, [int]$uy, [bool]$expected, [string]$case) {
    $actual = $type::IsWithinVanillaCloseRange([int16]$gx, [int16]$gy, [int16]$ux, [int16]$uy)
    if ($actual -ne $expected) { throw "Gatehouse close range failed: $case (expected $expected, got $actual)" }
}
# Gate tile 100,50 -> fine 800,400. Chebyshev distance below 200 closes, 200 never does.
Assert-Range 100 50 800 400 $true 'same position'
Assert-Range 100 50 999 400 $true 'dx 199'
Assert-Range 100 50 1000 400 $false 'dx 200'
Assert-Range 100 50 601 400 $true 'dx -199'
Assert-Range 100 50 600 400 $false 'dx -200'
Assert-Range 100 50 800 599 $true 'dy 199'
Assert-Range 100 50 800 200 $false 'dy -200'
Assert-Range 100 50 999 599 $true 'diagonal 199/199 (Chebyshev, not Euclid)'
Assert-Range 100 50 999 600 $false 'larger axis decides'
# Signed-word coordinates: a negative unit position must not wrap into range.
Assert-Range 0 0 -199 0 $true 'negative unit x inside'
Assert-Range 0 0 -200 0 $false 'negative unit x outside'
Assert-Range 4000 4000 32767 32767 $false 'large tile values without overflow'
if ($type::VanillaMaximumCloseDistance -ne 200) { throw 'Vanilla close distance must stay 200 (native baseline).' }
Write-Output 'PASS: gatehouse Vanilla close-range rule (boundary, sign and Chebyshev cases).'
