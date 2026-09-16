param([switch]$NormalizeChangedText)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $workspace
$changed = @(& git diff --name-only) + @(& git ls-files --others --exclude-standard)
$textTargets = @($changed | Where-Object { $_ -match '\.(cs|csproj|ps1|md|json|jsonl|bat)$' } | Sort-Object -Unique)
foreach ($relative in $textTargets) {
    $path = [IO.Path]::GetFullPath((Join-Path $workspace $relative))
    if (-not $path.StartsWith($workspace + '\', [StringComparison]::OrdinalIgnoreCase)) { throw $path }
    $text = [IO.File]::ReadAllText($path)
    if ($NormalizeChangedText) {
        $expected = [regex]::Replace($text, '\r?\n', "`r`n")
        if (-not [string]::Equals($text, $expected, [StringComparison]::Ordinal)) {
            [IO.File]::WriteAllText($path, $expected, [Text.UTF8Encoding]::new($false))
            $text = [IO.File]::ReadAllText($path)
            if (-not [string]::Equals($text, $expected, [StringComparison]::Ordinal)) { throw "Ordinal readback mismatch: $relative" }
        }
    }
    $bareLf = [regex]::Matches($text, '(?<!\r)\n').Count
    if ($bareLf) { throw "Bare LF in $relative : $bareLf" }
    Write-Output ("TEXT {0}: CRLF={1}, bareLF={2}, first={3}" -f $relative,
        [regex]::Matches($text, '\r\n').Count, $bareLf, ($text -split "`r`n")[0])
}
$projects = @(& rg -l 'Shared\\GameplaySessionLifecycle.cs' --glob '*.csproj' --glob '!_inspect/**')
$projects += 'APIShared\APIShared.csproj'
foreach ($relative in $projects) {
    $path = Join-Path $workspace $relative
    [xml]$project = [IO.File]::ReadAllText($path)
    foreach ($node in $project.SelectNodes('//*[local-name()="PropertyGroup" or local-name()="ItemGroup" or local-name()="Target"]')) {
        if ($node.ParentNode -ne $project.DocumentElement -and $node.ParentNode.LocalName -ne 'Target' -and $node.ParentNode.LocalName -ne 'When' -and $node.ParentNode.LocalName -ne 'Otherwise') {
            throw "Invalid nested MSBuild element: $relative $($node.LocalName)"
        }
    }
    $root = Split-Path -Parent $path
    $sources = @($project.SelectNodes('//*[local-name()="Compile"]') | ForEach-Object {
        $include = $_.GetAttribute('Include')
        if ($include -notmatch '[$*]') { [IO.Path]::GetFullPath((Join-Path $root $include)) }
    })
    foreach ($file in @($path) + $sources) {
        $text = [IO.File]::ReadAllText($file)
        if ($text -match 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility') {
            throw "Forbidden runtime JSON dependency: $file"
        }
        foreach ($m in [regex]::Matches($text, '\bvoid\s+(OnDestroy|OnDisable|OnApplicationQuit)\s*\([^)]*\)\s*\{')) {
            $depth = 1; $cursor = $m.Index + $m.Length; $begin = $cursor
            while ($cursor -lt $text.Length -and $depth -gt 0) {
                if ($text[$cursor] -eq '{') { $depth++ }
                if ($text[$cursor] -eq '}') { $depth-- }
                $cursor++
            }
            $body = $text.Substring($begin, $cursor - $begin)
            if ($body -match '\.Dispose\(|-=|\.Undo\(|Uninstall\(') { throw "Lifecycle teardown: $file $($m.Groups[1].Value)" }
        }
        if ($text -match ':\s*BaseUnityPlugin' -and $text -match '\bvoid\s+(Update|LateUpdate|FixedUpdate)\s*\(|StartCoroutine\(') {
            throw "Plugin frame/coroutine lifecycle: $file"
        }
    }
    if ($relative -ne 'APIShared\APIShared.csproj') {
        if (-not $project.SelectSingleNode('//*[local-name()="Reference" and @Include="APIShared"]')) { throw "Missing APIShared reference: $relative" }
        $pluginText = (@($sources | Where-Object { $_ -match 'Plugin\.cs$' } | ForEach-Object { [IO.File]::ReadAllText($_) }) -join "`n")
        if ($pluginText -notmatch 'BepInDependency\((?:"APIShared_Serp"|ApiSharedGuid),\s*(?:"0\.3\.6"|ApiSharedVersion)\)') {
            throw "Missing mandatory APIShared dependency: $relative"
        }
    }
    Write-Output "RUNTIME PASS: $relative"
}
$inventory = Get-Content 'Shared\ScriptExtenderUpdate\mods.json' -Raw | ConvertFrom-Json
$release = Get-Content 'Shared\Release\release-projects.json' -Raw | ConvertFrom-Json
foreach ($projectRelative in $projects) {
    $matches = @($inventory | Where-Object { $_.Project -eq $projectRelative })
    if ($matches.Count -ne 1) { throw "Missing or duplicate inventory project: $projectRelative" }
    if ($matches[0].Name -ne 'APIShared' -and $release.ApiShared.Consumers.($matches[0].Name) -ne '0.3.6') {
        throw "Missing release dependency: $projectRelative"
    }
}
foreach ($item in $inventory) {
    if ($projects -contains $item.Project -and $item.Name -ne 'APIShared') {
        if (@($item.DependsOn) -notcontains 'APIShared' -or $item.BuildOrder -le 10) { throw "Wrong build dependency: $($item.Name)" }
    }
}
$obsolete = & rg -n 'EnsureEditorMapState|BeginEditorMapIfApplicable|initial-current-editor' Shared CastlePlanner\src ExtraFeatures\src BugfixesAndQoL\src --glob '*.cs'
if ($obsolete) { throw ($obsolete -join "`n") }
Write-Output 'PASS: editor lifecycle preflight, dependencies, CRLF and replacement checks.'
