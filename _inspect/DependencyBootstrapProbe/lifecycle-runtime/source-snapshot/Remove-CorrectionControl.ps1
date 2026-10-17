[CmdletBinding(SupportsShouldProcess=$true)]
param([string]$GameDir='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition')
$ErrorActionPreference='Stop'
if(Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue){throw 'Close the game first.'}
$plugins=[IO.Path]::GetFullPath((Join-Path $GameDir 'BepInEx\plugins'))
$directory=[IO.Path]::GetFullPath((Join-Path $plugins 'DependencyBootstrapProbeControl_Serp'))
if(-not $directory.StartsWith($plugins+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Control path escaped plugin root.'}
if(-not (Test-Path -LiteralPath $directory)){Write-Host 'Control fixture already absent.';return}
if((Get-Item -LiteralPath $directory).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Control directory is a reparse point.'}
$children=@(Get-ChildItem -LiteralPath $directory -Force)
if($children.Count -ne 1 -or $children[0].Name -cne 'info.json' -or $children[0].PSIsContainer){throw 'Unexpected files in control directory; preserve everything.'}
$receipt=Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'trial\expectations.json')|ConvertFrom-Json
$stream=[IO.File]::OpenRead($children[0].FullName);$sha=[Security.Cryptography.SHA256]::Create()
try{$hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()}
if($hash -cne $receipt.ManifestSHA256){throw 'Control manifest changed; preserving it.'}
if($PSCmdlet.ShouldProcess($directory,'Remove only hash-confirmed control manifest and its empty directory')){
    Remove-Item -LiteralPath $children[0].FullName
    [IO.Directory]::Delete($directory,$false)
}
