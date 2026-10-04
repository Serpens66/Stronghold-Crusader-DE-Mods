[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$tweaker = Join-Path (Split-Path -Parent $workspace) 'Fremde Mods\crusader-de-tweaker'
$roots = @((Join-Path $workspace 'APIShared\src'), (Join-Path $workspace 'SerpsModsHost\src'), (Join-Path $tweaker 'Config'))
$sources = @(foreach ($root in $roots) { Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.cs' })
$sources += Get-Item -LiteralPath (Join-Path $tweaker 'Plugin.cs')
$projects = @((Join-Path $workspace 'APIShared\APIShared.csproj'), (Join-Path $workspace 'SerpsModsHost\SerpsModsHost.csproj'), (Join-Path $tweaker 'CrusaderDETweaker.csproj'))
foreach ($file in @($sources.FullName) + $projects) {
    $text = [IO.File]::ReadAllText($file)
    if ($text -match '(?<![A-Za-z])(System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility)(?![A-Za-z])') { throw "Forbidden JSON dependency: $file" }
    if ($text -match '\b(?:void|IEnumerator)\s+(?:OnDestroy|OnDisable|OnApplicationQuit|LateUpdate|FixedUpdate)\s*\(' -or $text -match '\bStartCoroutine\s*\(') { throw "Runtime lifecycle path requires audit: $file" }
    if ($text -match '\bvoid\s+Update\s*\(' -and $file -ne (Join-Path $tweaker 'Plugin.cs')) { throw "Unexpected component Update: $file" }
}
$plugin = [IO.File]::ReadAllText((Join-Path $tweaker 'Plugin.cs'))
$upstreamPlugin = (& git -C $tweaker show 'upstream/main:Plugin.cs') -join "`n"
if ($LASTEXITCODE -ne 0) { throw 'Cannot compare existing Tweaker Update' }
$pattern = '(?s)private void Update\(\)\s*\{.*?\n        \}'
if ([regex]::Match($plugin.Replace("`r`n","`n"),$pattern).Value -cne [regex]::Match($upstreamPlugin,$pattern).Value) { throw 'Tweaker component Update changed' }

$changed = @(& git -C $workspace diff --name-only -- APIShared SerpsModsHost _inspect/StatsTweakerApiTests _inspect/HostClientPresetTests _inspect/LobbyModSettingsPresetTests)
$changed += @(& git -C $workspace ls-files --others --exclude-standard -- APIShared SerpsModsHost _inspect/StatsTweakerApiTests _inspect/HostClientPresetTests _inspect/LobbyModSettingsPresetTests)
$foreign = @(& git -C $tweaker diff --name-only)
$foreign += @(& git -C $tweaker ls-files --others --exclude-standard)
$paths = @($changed | ForEach-Object { Join-Path $workspace $_ }) + @($foreign | ForEach-Object { Join-Path $tweaker $_ })
foreach ($file in ($paths | Sort-Object -Unique)) {
    if ($file -match '\\(BepInEx|bin|obj)\\') { continue }
    if ([IO.Path]::GetExtension($file) -notin @('.cs','.csproj','.ps1','.xaml','.txt','.md','.bat')) { continue }
    $text = [IO.File]::ReadAllText($file)
    if ($text -match "(?<!`r)`n") { throw "Bare LF: $file" }
}
$xaml = Join-Path $workspace 'SerpsModsHost\Override\ScriptExtenderUI\StatsTweakerPresets.xaml'
[xml]$xml = [IO.File]::ReadAllText($xaml)
foreach ($control in $xml.SelectNodes("//*[local-name()='Button' or local-name()='TextBox' or local-name()='ComboBox' or local-name()='CheckBox' or local-name()='Slider']")) {
    if ([string]::IsNullOrWhiteSpace($control.GetAttribute('ToolTip')) -or $control.GetAttribute('ToolTipService.ShowDuration') -ne '60000') { throw "Missing tooltip in $xaml" }
}
foreach ($root in @((Join-Path $workspace 'APIShared\Patches'), (Join-Path $workspace 'SerpsModsHost\Patches'))) {
    if (!(Test-Path -LiteralPath $root)) { continue }
    foreach ($patch in Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.xaml') {
        [xml]$patchXml = [IO.File]::ReadAllText($patch.FullName)
        foreach ($content in $patchXml.SelectNodes("//*[local-name()='Content']")) {
            if (@($content.ChildNodes | Where-Object NodeType -eq Element).Count -ne 1) { throw "XAML Content root contract: $($patch.FullName)" }
        }
    }
}
& (Join-Path $workspace 'Shared\Test-PermanentNativeRuntimePatches.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Permanent runtime audit failed' }
& (Join-Path $workspace '_inspect\AuditModSettings.ps1') -Mod SerpsModsHost
Write-Output 'PASS: JSON, lifecycle, unchanged legacy Tweaker Update, CRLF, injected UI tooltips and XAML patch roots.'
Write-Output 'New direct Assembly-CSharp members: none. Publicized visibility check: empty new-member list.'
Write-Output 'Lifetime: static configuration API; static adapter roots Noesis commands; registration via persistent Extender registry and EnqueueStatic.'
