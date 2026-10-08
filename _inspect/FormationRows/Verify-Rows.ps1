param()
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$workspace = Split-Path -Parent (Split-Path -Parent $taskRoot)
$before = Join-Path $taskRoot 'before'
$runtimePath = 'APIShared\src\UnitCommands\FormationRuntime.cs'
$runtime = [IO.File]::ReadAllText((Join-Path $workspace $runtimePath))
$old = [IO.File]::ReadAllText((Join-Path $before $runtimePath))
$old = $old.Replace('FormationModel.ResolveActualRows((FormationKind)packet.Formation, packet.UnitCount, packet.Width)', 'packet.Rows')
function Get-Method([string]$source, [string]$signature) {
    $start = $source.IndexOf($signature, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Missing method $signature" }
    $open = $source.IndexOf('{', $start)
    $depth = 0
    for ($index = $open; $index -lt $source.Length; $index++) {
        if ($source[$index] -eq '{') { $depth++ }
        if ($source[$index] -eq '}') {
            $depth--
            if ($depth -eq 0) { return $source.Substring($start, $index - $start + 1) }
        }
    }
    throw "Incomplete method $signature"
}
foreach ($signature in @(
    'private NativeDestination[] CaptureVanillaDestinations(',
    'private static void TryEnqueueVanillaCandidate(',
    'private static void TryEnqueueCandidate(',
    'private int EngineRunHook(',
    'private int RunOriginalAfterReleaseConsumed(',
    'private void CameraUpdateHook(',
    'private void DisableAfterNativeFailure(',
    'private static void ValidateNativeContracts('
)) {
    if ((Get-Method $runtime $signature) -cne (Get-Method $old $signature)) {
        throw "Unexpected change to preserved contract: $signature"
    }
}
Write-Output 'PASS: Vanilla search, terrain/connection gates, release consumption, camera hook, failure and native validation unchanged from starting code.'
$xamlPath = 'BugfixesAndQoL\Patches\Assets\GUI\XAMLResources\HUD_Troops.xaml'
[xml]$patch = [IO.File]::ReadAllText((Join-Path $workspace $xamlPath))
[xml]$native = [IO.File]::ReadAllText((Join-Path $workspace '_inspect\CrusaderDE-Native-Baseline\sem\FBCB9319\resources\xaml\Assets\GUI\XAMLResources\HUD_Troops.xaml'))
$ns = [Xml.XmlNamespaceManager]::new($native.NameTable)
$ns.AddNamespace('n', 'http://schemas.microsoft.com/winfx/2006/xaml/presentation')
$templates = @($patch.SelectNodes('//Operation[@XPath="//n:UserControl.Resources"]/Content/DataTemplate'))
if ($templates.Count -ne 6 -or $native.SelectNodes('//n:UserControl.Resources', $ns).Count -ne 1) { throw 'Icon resource insertion mismatch' }
$text = $patch.OuterXml
foreach ($kind in @('Vanilla','Block','Line','Column','Wedge','Circle')) {
    $key = 'BugfixesFormationIcon' + $kind
    if (@($templates | Where-Object { $_.GetAttribute('Key','http://schemas.microsoft.com/winfx/2006/xaml') -eq $key }).Count -ne 1 -or
        [regex]::Matches($text, [regex]::Escape('{StaticResource ' + $key + '}')).Count -ne 2) { throw "Missing shared icon $kind" }
}
Write-Output 'PASS: six shared icon resources target the actual Vanilla resource node; menu and opener reference each template.'
foreach ($file in Get-ChildItem -LiteralPath $before -Recurse -File) {
    $relative = $file.FullName.Substring($before.Length + 1)
    $current = [IO.File]::ReadAllText((Join-Path $workspace $relative))
    if ($current -match '(?<!\r)\n') { throw "Bare LF: $relative" }
    if ($relative -match '\\Locales\\') {
        $pattern = '(?m)^BugfixesAndQoL\.EnableMoveFormationEnhancementsHelp=.*\r?\n'
        if ([regex]::Replace($current, $pattern, '') -cne [regex]::Replace([IO.File]::ReadAllText($file.FullName), $pattern, '')) {
            throw "Unrelated locale change: $relative"
        }
    }
}
Write-Output 'PASS: changed source texts are CRLF; every pre-existing locale entry except formation help preserved.'
