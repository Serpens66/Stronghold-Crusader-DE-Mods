[CmdletBinding()]
param([string]$Root, [string]$MSBuild)
if (-not $Root) { $Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')) }
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'WorkPaths.ps1')
if (-not $MSBuild) { $MSBuild = & (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe') -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild/**/Bin/MSBuild.exe' | Select-Object -First 1 }
if (-not $MSBuild) { throw 'MSBuild required.' }
$fixture = New-ShortWorkDirectory -Root $Root -Name paths
$source = @'
using System;
using System.IO;
class PathProbe {
    static void Main(string[] args) {
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
$classic = '<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003"><Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"/><PropertyGroup><OutputType>Exe</OutputType><TargetFrameworkVersion>v4.8.1</TargetFrameworkVersion><OutputPath>bin\</OutputPath><AssemblyName>ClassicProbe</AssemblyName></PropertyGroup><ItemGroup><Reference Include="System"/><Compile Include="Program.cs"/></ItemGroup><Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets"/></Project>'
[IO.File]::WriteAllText((Join-Path $fixture 'Classic.csproj'),$classic)
& $MSBuild (Join-Path $fixture 'Classic.csproj') /nologo /verbosity:minimal
if ($LASTEXITCODE) { throw 'Classic path probe failed to build.' }
& (Join-Path $fixture 'bin/ClassicProbe.exe') (Join-Path $fixture 'io')
if ($LASTEXITCODE) { throw 'Classic executable does not support long paths.' }
[xml]$config = [IO.File]::ReadAllText((Join-Path $fixture 'bin/ClassicProbe.exe.config'))
if ($config.configuration.appSettings.add.value -ne 'yes' -or $config.configuration.runtime.loadFromRemoteSources.enabled -ne 'true' -or $config.configuration.runtime.AppContextSwitchOverrides.value -notmatch 'Unrelated.Switch=true') { throw 'Existing app configuration was lost.' }
$sdk = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net481</TargetFramework><OutputType>Exe</OutputType><EnableDefaultCompileItems>false</EnableDefaultCompileItems><AssemblyName>SdkProbe</AssemblyName></PropertyGroup><ItemGroup><Compile Include="Program.cs"/></ItemGroup></Project>'
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
Write-Host "PASS: paths, config preservation, SDK/classic defaults, PowerShell IO and concurrent allocation; evidence $fixture"
