$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$target = Join-Path $workspace 'Testmods\MoatMove'
$sourceDir = Join-Path $workspace 'BugfixesAndQoL\src'
$testDir = Join-Path $workspace 'BugfixesAndQoL\tests\FriendlyMoatMovement.Tests'
$program = [IO.File]::ReadAllText((Join-Path $testDir 'Program.cs'))
$list = [regex]::Match($program, '(?s)string\[\] runtimeSourceNames =\s*\{(.*?)\};').Groups[1].Value
$names = @([regex]::Matches($list, '"([^"]+\.cs)"') | ForEach-Object { $_.Groups[1].Value })
if ($names.Count -ne 22) { throw 'Unexpected source closure; review before copying.' }
function Write-Crlf([string]$path, [string]$text) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path)) | Out-Null
    $expected = [regex]::Replace($text, '\r?\n', "`r`n")
    [IO.File]::WriteAllText($path, $expected, [Text.UTF8Encoding]::new($false))
    $actual = [IO.File]::ReadAllText($path)
    if (-not [string]::Equals($expected, $actual, [StringComparison]::Ordinal) -or [regex]::IsMatch($actual, '(?<!\r)\n')) { throw "Text verification failed: $path" }
}
$records = @(foreach ($name in $names) {
    $source = Join-Path $sourceDir $name
    $destination = Join-Path $target ('src\' + $name)
    if (Test-Path -LiteralPath $destination) { throw "Refusing to overwrite: $destination" }
    $text = [IO.File]::ReadAllText($source).Replace('BugfixesAndQoLViewModel', 'MoatMoveOptions').Replace('BugfixesAndQoL', 'MoatMove').Replace('Bugfixes and QoL', 'MoatMove')
    Write-Crlf $destination $text
    [ordered]@{ file = $name; sourceSha256 = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash; copySha256 = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash }
})
$provenance = [ordered]@{ schemaVersion = 1; sourceDirectory = 'BugfixesAndQoL/src'; nativeSha256 = 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2'; transformations = @('BugfixesAndQoLViewModel -> MoatMoveOptions', 'BugfixesAndQoL -> MoatMove', 'Bugfixes and QoL -> MoatMove', 'UTF-8 without BOM, CRLF'); files = $records }
Write-Crlf (Join-Path $target 'SOURCE_PROVENANCE.json') (($provenance | ConvertTo-Json -Depth 6) + "`r`n")
foreach ($name in @('CursorTests.cs','FillFormationTests.cs','PlacementTests.cs','RuntimeHarness.cs','SearchKernelTests.cs','NativeContracts.py','Validate-PlacementContracts.py','FriendlyMoatMovement.Tests.csproj')) {
    $text = [IO.File]::ReadAllText((Join-Path $testDir $name)).Replace('BugfixesAndQoLViewModel','MoatMoveOptions').Replace('BugfixesAndQoL','MoatMove')
    $text = $text.Replace('Path.Combine(root,"MoatMove","tests","FriendlyMoatMovement.Tests",','Path.Combine(root,"Testmods","MoatMove","tests",')
    $text = $text.Replace("MoatMove/src/", "Testmods/MoatMove/src/").Replace('MoatMove/tests/FriendlyMoatMovement.Tests/', 'Testmods/MoatMove/tests/')
    if ($name -eq 'NativeContracts.py') { $text = $text.Replace('InstallConnectivityObserver\(memory, libraryBase,', 'InstallConnectivityObserver\(pendingTransaction, memory, libraryBase,') }
    if ($name -eq 'FriendlyMoatMovement.Tests.csproj') { $name = 'MoatMove.Tests.csproj' }
    Write-Crlf (Join-Path $target ('tests\' + $name)) $text
}
$program = $program.Replace('BugfixesAndQoLViewModel','MoatMoveOptions').Replace('BugfixesAndQoL','MoatMove')
$program = $program.Replace('Path.Combine(root, "MoatMove", "src")', 'Path.Combine(root, "Testmods", "MoatMove", "src")').Replace('Path.Combine(root, "MoatMove", "tests", "FriendlyMoatMovement.Tests")','Path.Combine(root, "Testmods", "MoatMove", "tests")').Replace('Path.Combine(root, "MoatMove",','Path.Combine(root, "Testmods", "MoatMove",')
# Retain all behavioral fixtures and transaction checks. Replace only parent-mod
# registration/settings assertions; MoatMove has no parent orchestrator or UI.
$start = $program.IndexOf('    string plugin =', $program.IndexOf('void ValidateScriptExtenderIntegration()'))
$end = $program.IndexOf('    foreach (string forbidden', $start)
$replacement = @'
    string plugin = File.ReadAllText(Path.Combine(sourceDir, "MoatMovePlugin.cs"));
    string runtime = string.Join("\n", trees.Select(tree => tree.ToString()));
    string project = File.ReadAllText(Path.Combine(root, "Testmods", "MoatMove", "MoatMove.csproj"));
    if (!plugin.Contains("private static FriendlyMoatMovementRuntime runtime;", StringComparison.Ordinal) ||
        !plugin.Contains("MoatMoveConflictPolicy.FindConflict", StringComparison.Ordinal) ||
        plugin.Contains("runtime.Dispose(", StringComparison.Ordinal))
        throw new Exception("Standalone ownership/conflict contract missing.");
'@
$program = $program.Substring(0,$start) + $replacement + "`r`n" + $program.Substring($end)
$program = $program.Replace(', "RedBird.Backends.NativeX64.dll"','')
$start = $program.IndexOf('    var settingsStub =', $program.IndexOf('void ValidateRuntimeSources()'))
$end = $program.IndexOf('    var check=', $start)
$replacement = @'
    var sources = Directory.GetFiles(sourceDir, "*.cs")
        .Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file))
        .Concat(new[]{"DebugLogHelper.cs", "NativePatternResolver.cs"}.Select(file =>
            CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"Shared",file)),path:file))).ToArray();
'@
$program = $program.Substring(0,$start) + $replacement + "`r`n" + $program.Substring($end)
$start = $program.IndexOf('void ValidateModeSettings()')
$program = $program.Substring(0,$start) + @'
void ValidateModeSettings()
{
    StandaloneContracts.Validate(root, sourceDir);
}
'@
Write-Crlf (Join-Path $target 'tests\Program.cs') $program
[xml]$originalProject = [IO.File]::ReadAllText((Join-Path $workspace 'BugfixesAndQoL\BugfixesAndQoL.csproj'))
$refs = @($originalProject.Project.ItemGroup.Reference | Where-Object { $_.Include -notin @('APIShared','MonoMod.RuntimeDetour') } | ForEach-Object { $_.OuterXml }) -join "`r`n    "
$compile = @($names | ForEach-Object { '    <Compile Include="src\' + $_ + '" />' }) -join "`r`n"
$project = @'
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
  <PropertyGroup>
    <Configuration Condition="'$(Configuration)' == ''">Debug</Configuration>
    <Platform Condition="'$(Platform)' == ''">AnyCPU</Platform>
    <ProjectGuid>{C1652144-F832-4C7A-81E6-E5851A0D240D}</ProjectGuid>
    <OutputType>Library</OutputType><RootNamespace>MoatMove</RootNamespace><AssemblyName>MoatMove</AssemblyName>
    <TargetFrameworkVersion>v4.8.1</TargetFrameworkVersion><LangVersion>latest</LangVersion><AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <OutputPath>BepInEx\plugins\MoatMove_Serp\</OutputPath><DebugSymbols>true</DebugSymbols><DebugType>portable</DebugType><WarningLevel>4</WarningLevel>
    <GameDir Condition="'$(GameDir)' == ''">E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition</GameDir>
    <ExtenderDir Condition="'$(ExtenderDir)' == ''">$(GameDir)\BepInEx\plugins\000shcdese</ExtenderDir>
  </PropertyGroup>
  <ItemGroup>
    __REFERENCES__
  </ItemGroup>
  <ItemGroup>
__SOURCES__
    <Compile Include="src\MoatMovePlugin.cs" /><Compile Include="src\MoatMoveOptions.cs" /><Compile Include="src\MoatMoveConflictPolicy.cs" />
    <Compile Include="..\..\Shared\DebugLogHelper.cs"><Link>Shared\DebugLogHelper.cs</Link></Compile>
    <Compile Include="..\..\Shared\NativePatternResolver.cs"><Link>Shared\NativePatternResolver.cs</Link></Compile>
  </ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
  <Target Name="ValidateMoatMove" BeforeTargets="BeforeBuild">
    <Exec Command="powershell.exe -NoProfile -ExecutionPolicy Bypass -File &quot;$(MSBuildProjectDirectory)\tests\Test-Preflight.ps1&quot;" />
    <Error Condition="!Exists('$(ExtenderDir)\SHCDESE.dll')" Text="Installed Script Extender required; set ExtenderDir explicitly to override." />
  </Target>
</Project>
'@
Write-Crlf (Join-Path $target 'MoatMove.csproj') ($project.Replace('__REFERENCES__',$refs).Replace('__SOURCES__',$compile))
Write-Output 'Copied 22 runtime files, behavioral fixtures, standalone test runner, project and provenance.'
