[CmdletBinding()]
param([string]$Root, [string]$MSBuild)
if (-not $Root) { $Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')) }
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'WorkPaths.ps1')
if (-not $MSBuild) { $MSBuild = & (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe') -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild/**/Bin/MSBuild.exe' | Select-Object -First 1 }
if (-not $MSBuild) { throw 'MSBuild required.' }
$fixture = New-ShortWorkDirectory -Root $Root -Name paths -Transient
$paths=@()
try {
$source = @'
using System;
using System.IO;
class PathProbe {
    static void Main(string[] args) {
        if (args[0] == "fixture") {
            string fixture = TestWorkDirectory.New("path-cleanup", true);
            File.WriteAllText(args[1], fixture);
            File.WriteAllText(Path.Combine(fixture, "payload.txt"), "disposable");
            if (args[2] == "fail") Environment.Exit(7);
            return;
        }
        string root = args[0], path = root;
        while (path.Length < 330) path = Path.Combine(path, "Ordner mit Umlaut ä 0123456789");
        Directory.CreateDirectory(path);
        string a = Path.Combine(path,"datei.txt"), b=Path.Combine(path,"kopie.txt"), c=Path.Combine(path,"neu.txt");
        File.WriteAllText(a,"long path ä");
        if (File.ReadAllText(a)!="long path ä") throw new Exception("read");
        File.Copy(a,b); File.Move(b,c);
        if (Directory.GetFiles(path).Length!=2) throw new Exception("enumeration");
        File.Delete(a); File.Delete(c); Directory.Delete(root,true);
        Console.WriteLine("PASS: executable long-path create/write/read/list/copy/rename/delete ("+a.Length+")");
    }
}
'@
[IO.File]::WriteAllText((Join-Path $fixture 'Program.cs'), $source, [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $fixture 'App.config'), '<configuration><appSettings><add key="keep" value="yes"/></appSettings><runtime><loadFromRemoteSources enabled="true"/><AppContextSwitchOverrides value="Unrelated.Switch=true;Switch.System.IO.BlockLongPaths=true"/></runtime></configuration>')
$helperSource=[Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'TestWorkDirectory.cs'))
$classic = '<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003"><Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"/><PropertyGroup><OutputType>Exe</OutputType><TargetFrameworkVersion>v4.8.1</TargetFrameworkVersion><OutputPath>bin\</OutputPath><AssemblyName>ClassicProbe</AssemblyName></PropertyGroup><ItemGroup><Reference Include="System"/><Compile Include="Program.cs"/><Compile Include="'+$helperSource+'"/></ItemGroup><Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets"/></Project>'
[IO.File]::WriteAllText((Join-Path $fixture 'Classic.csproj'),$classic)
& $MSBuild (Join-Path $fixture 'Classic.csproj') /nologo /verbosity:minimal
if ($LASTEXITCODE) { throw 'Classic path probe failed to build.' }
& (Join-Path $fixture 'bin/ClassicProbe.exe') (Join-Path $fixture 'io')
if ($LASTEXITCODE) { throw 'Classic executable does not support long paths.' }
[xml]$config = [IO.File]::ReadAllText((Join-Path $fixture 'bin/ClassicProbe.exe.config'))
if ($config.configuration.appSettings.add.value -ne 'yes' -or $config.configuration.runtime.loadFromRemoteSources.enabled -ne 'true' -or $config.configuration.runtime.AppContextSwitchOverrides.value -notmatch 'Unrelated.Switch=true') { throw 'Existing app configuration was lost.' }
foreach($mode in @('success','fail')) {
    $result=Join-Path $fixture ('csharp-'+$mode+'.txt')
    & (Join-Path $fixture 'bin/ClassicProbe.exe') fixture $result $mode
    $expected=if($mode -eq 'fail'){7}else{0}
    if($LASTEXITCODE -ne $expected){throw 'C# cleanup probe exit code mismatch.'}
    $allocated=[IO.File]::ReadAllText($result)
    if([IO.Directory]::Exists($allocated) -or [IO.File]::Exists($allocated+'.run.json')){throw 'C# process left a fixture behind.'}
}
$sdk = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net481</TargetFramework><OutputType>Exe</OutputType><EnableDefaultCompileItems>false</EnableDefaultCompileItems><AssemblyName>SdkProbe</AssemblyName></PropertyGroup><ItemGroup><Compile Include="Program.cs"/><Compile Include="'+$helperSource+'"/></ItemGroup></Project>'
[IO.File]::WriteAllText((Join-Path $fixture 'Sdk.csproj'),$sdk)
& dotnet build (Join-Path $fixture 'Sdk.csproj') --nologo --verbosity quiet
if ($LASTEXITCODE) { throw 'SDK path probe failed to build.' }
& (Join-Path $fixture 'bin/Debug/net481/SdkProbe.exe') (Join-Path $fixture 'io')
if ($LASTEXITCODE) { throw 'SDK executable does not support long paths.' }
# PowerShell 5.1 must support the same IO in the actual BAT child environment.
$long = Join-Path $fixture 'shell'
while ($long.Length -lt 330) { $long = Join-Path $long 'Ordner mit Umlaut ä 0123456789' }
[void][IO.Directory]::CreateDirectory($long)
$one=Join-Path $long 'a.txt';$two=Join-Path $long 'b.txt';$three=Join-Path $long 'c.txt'
[IO.File]::WriteAllText($one,'shell');[IO.File]::Copy($one,$two);[IO.File]::Move($two,$three)
if ([IO.File]::ReadAllText($three) -ne 'shell' -or [IO.Directory]::GetFiles($long).Count -ne 2) { throw 'PowerShell long IO failed.' }
[IO.File]::Delete($one);[IO.File]::Delete($three);[IO.Directory]::Delete((Join-Path $fixture 'shell'),$true)
$jobs = @(1..4 | ForEach-Object { Start-Job -ArgumentList $PSScriptRoot,$Root -ScriptBlock { param($tools,$base); . (Join-Path $tools 'WorkPaths.ps1'); New-ShortWorkDirectory -Root $base -Name parallel } })
try {
    $paths = @($jobs | Wait-Job | Receive-Job -ErrorAction Stop)
    if (@($paths | Sort-Object -Unique).Count -ne 4) { throw 'Concurrent work allocation collided.' }
} finally { $jobs | Remove-Job -Force }
$rejected=$false;try { Assert-ToolPathBudget -Path $long } catch { $rejected=$true }
if (-not $rejected) { throw 'Path budget did not reject a long tool path.' }
$cleanupChild=Join-Path $fixture 'cleanup-child.ps1'
[IO.File]::WriteAllText($cleanupChild, @'
param([string]$Helper,[string]$Root,[string]$Result,[int]$Fail)
$ErrorActionPreference='Stop'
. $Helper
try {
$allocated=New-ShortWorkDirectory -Root $Root -Kind t -Name exit-fixture -Transient
[IO.File]::WriteAllText($Result,$allocated)
[IO.File]::WriteAllText((Join-Path $allocated 'payload.txt'),'disposable')
if($Fail){throw 'Intentional fixture failure'}
}finally{Complete-TransientWorkDirectories -Owner $PSCommandPath}
'@)
foreach($fail in @(0,1)) {
    $result=Join-Path $fixture ('powershell-'+$fail+'.txt')
    try {
        $ErrorActionPreference='Continue'
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $cleanupChild -Helper (Join-Path $PSScriptRoot 'WorkPaths.ps1') -Root $Root -Result $result -Fail $fail *> (Join-Path $fixture ('child-'+$fail+'.log'))
    }finally{$ErrorActionPreference='Stop'}
    if(($LASTEXITCODE -eq 0) -ne ($fail -eq 0)){throw 'PowerShell cleanup probe exit code mismatch.'}
    $allocated=[IO.File]::ReadAllText($result)
    if([IO.Directory]::Exists($allocated) -or [IO.File]::Exists($allocated+'.run.json')){throw 'PowerShell process left a fixture behind.'}
}
# The cleanup must not follow a junction or accept a forged ownership record.
$victim=New-ShortWorkDirectory -Root $Root -Name cleanup-safety
$external=Join-Path $fixture 'outside';[void][IO.Directory]::CreateDirectory($external)
[IO.File]::WriteAllText((Join-Path $external 'keep.txt'),'keep')
$null=New-Item -ItemType Junction -Path (Join-Path $victim 'link') -Target $external
Remove-ShortWorkDirectory $victim
if(-not [IO.File]::Exists((Join-Path $external 'keep.txt'))){throw 'Fixture cleanup followed a directory junction.'}
$victim=New-ShortWorkDirectory -Root $Root -Name cleanup-safety
$record=[IO.File]::ReadAllText($victim+'.run.json')
try {
    [IO.File]::WriteAllText($victim+'.run.json','{}')
    $rejected=$false;try {Remove-ShortWorkDirectory $victim}catch{$rejected=$true}
    if(-not $rejected -or -not [IO.Directory]::Exists($victim)){throw 'Cleanup accepted forged fixture ownership.'}
}finally{[IO.File]::WriteAllText($victim+'.run.json',$record);Remove-ShortWorkDirectory $victim}
$victim=New-ShortWorkDirectory -Root $Root -Name cleanup-git
$repo=Join-Path $victim 'repo';$tree=Join-Path $victim 'tree'
& git init --quiet $repo
& git -C $repo -c user.name=Fixture -c user.email=fixture@example.invalid -c commit.gpgsign=false commit --quiet --allow-empty -m fixture
if($LASTEXITCODE){throw 'Cannot create cleanup Git fixture.'}
& git -C $repo worktree add --quiet --detach $tree HEAD
if($LASTEXITCODE){throw 'Cannot create cleanup fixture worktree.'}
function Git {throw 'Cleanup must not call the test script Git wrapper.'}
try{Remove-ShortWorkDirectory $victim}finally{Remove-Item Function:Git}
if([IO.Directory]::Exists($victim)){throw 'Owned fixture Git worktree was not cleaned.'}
$victim=New-ShortWorkDirectory -Root $Root -Name cleanup-git
$repo=Join-Path $fixture 'external-repo';$tree=Join-Path $victim 'tree'
& git init --quiet $repo
& git -C $repo -c user.name=Fixture -c user.email=fixture@example.invalid -c commit.gpgsign=false commit --quiet --allow-empty -m fixture
& git -C $repo worktree add --quiet --detach $tree HEAD
if($LASTEXITCODE){throw 'Cannot create external Git pointer fixture.'}
try {
    $rejected=$false;try{Remove-ShortWorkDirectory $victim}catch{$rejected=$true}
    if(-not $rejected -or -not [IO.File]::Exists((Join-Path $tree '.git'))){throw 'Cleanup accepted external Git ownership.'}
}finally{& git -C $repo worktree remove --force $tree;Remove-ShortWorkDirectory $victim}
Write-Host 'PASS: paths, configuration, concurrent allocation, PowerShell/C# success/failure cleanup, ownership, Git and junction safety.'
} finally {
    foreach($allocated in $paths){Remove-ShortWorkDirectory $allocated}
    Remove-ShortWorkDirectory $fixture
    Complete-TransientWorkDirectories -Owner $PSCommandPath
}
