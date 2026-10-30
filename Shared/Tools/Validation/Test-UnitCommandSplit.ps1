[CmdletBinding()]
param([switch]$SkipCommonChecks, [switch]$SkipCompiler, [string]$ModDirectory, [switch]$SkipPermanentHooks)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$projects = @('APIShared\APIShared.csproj', 'BugfixesAndQoL\BugfixesAndQoL.csproj', 'Testmods\MoatMove\MoatMove.csproj')
if ($ModDirectory) {
    $modRoot = [IO.Path]::GetFullPath((Join-Path $workspace $ModDirectory)).TrimEnd('\','/')
    $selected = @($projects | Where-Object { [IO.Path]::GetDirectoryName((Join-Path $workspace $_)) -ieq $modRoot })
    if ($selected.Count -ne 1) { throw "Unsupported command-runtime scope: $ModDirectory" }
    $projects = @('APIShared\APIShared.csproj') + $selected
}
$jsonPattern = 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json'
$callbackPattern = '\b(?:public|private|protected|internal)\s+(?:(?:static|override|virtual|async)\s+)*(?:void|IEnumerator|Task)\s+(?:OnDestroy|OnDisable|OnApplicationQuit|Update|LateUpdate|FixedUpdate)\s*\(\s*\)|\bStartCoroutine\s*\('
$count = 0
foreach ($relative in $projects) {
    $project = Join-Path $workspace $relative
    $directory = Split-Path -Parent $project
    [xml]$xml = [IO.File]::ReadAllText($project)
    $paths = @($project) + @(foreach ($compile in $xml.SelectNodes('//*[local-name()="Compile"][@Include]')) {
        $include = [string]$compile.Include
        $target = Join-Path $directory $include
        if ($target.Contains('*')) {
            # .NET Framework rejects wildcards in GetFullPath; resolve only the directory.
            $base = [IO.Path]::GetFullPath($target.Substring(0, $target.IndexOf('*')).TrimEnd('\','/'))
            $option = if ($target.Contains('**')) { [IO.SearchOption]::AllDirectories } else { [IO.SearchOption]::TopDirectoryOnly }
            [IO.Directory]::GetFiles($base, [IO.Path]::GetFileName($target), $option)
        } else { [IO.Path]::GetFullPath($target) }
    })
    foreach ($path in $paths | Select-Object -Unique) {
        $text = [IO.File]::ReadAllText($path)
        if (-not $SkipCommonChecks -and $text -match $jsonPattern) { throw "Forbidden runtime JSON dependency: $path" }
        if ($text -match $callbackPattern) { throw "Runtime component callback or teardown: $path" }
        if ($path -match '\\UnitCommands\\|\\MoatMove\\src\\|\\BugfixesAndQoL\\src\\Formations\\') {
            if ($text -match '\b(?:CodePatch\.Write|VirtualProtect|FlushInstructionCache)\s*\(|\.Hook\.(?:Enable|Disable|Undo|Dispose)\s*\(') {
                throw "Executable hook mutation in shared command runtime: $path"
            }
            # The private FastNative kernel, the only executable publication here, was removed (2026-10-30).
            if ($text -match 'NativeMemoryManager\.WriteStub\s*\(') {
                throw "Unexpected executable publication in shared command runtime: $path"
            }
        }
        if (-not $SkipCommonChecks -and $text -match '(?<!\r)\n') { throw "Bare LF: $path" }
        $count++
    }
if (-not $SkipCommonChecks) {
    foreach ($file in Get-ChildItem -LiteralPath $directory -Recurse -File -Filter '*.xaml' | Where-Object { $_.FullName -notmatch '\\(?:bin|obj|BepInEx)\\' }) {
        [xml]$patch = [IO.File]::ReadAllText($file.FullName)
        foreach ($content in $patch.SelectNodes('//*[local-name()="Content"]')) {
            $elements = @($content.ChildNodes | Where-Object { $_.NodeType -eq [Xml.XmlNodeType]::Element })
            if ($elements.Count -ne 1) { throw "XAML Content requires exactly one root: $($file.FullName)" }
        }
    }
}
}
if (-not $SkipPermanentHooks) {
    & (Join-Path $workspace 'Shared\Tools\Validation\Test-PermanentNativeRuntimePatches.ps1') -ModDirectory $ModDirectory
}
if (-not $SkipCompiler) {
    . (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
    $checkRoot = New-ShortWorkDirectory -Root $workspace -Kind t -Name command-scope
    $projectList = Join-Path $checkRoot 'projects.txt'
    [IO.File]::WriteAllText($projectList, (($projects + '') -join "`r`n"), [Text.UTF8Encoding]::new($false))
    & dotnet run --project (Join-Path $workspace 'Shared\Tools\Validation\UnitCommandSourceChecks\UnitCommandSourceChecks.csproj') -- $workspace --real --projects-file $projectList
    if ($LASTEXITCODE -ne 0) { throw 'Real installed game assembly source check failed.' }
}
Write-Output "PASS: command preflight ($count project/source entries), JSON, callbacks, CRLF, XAML and permanent hooks; projects: $($projects -join ', ')."
