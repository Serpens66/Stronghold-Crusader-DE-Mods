$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../ApiSharedRepository/MainSource.Common.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$fixture=Join-Path $workspace ('.inspect/frozen-sdk-'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory((Join-Path $fixture 'tools'))
$checks=0
function Check($Ok,$Message){if(-not $Ok){throw $Message};$script:checks++}
function Import-Policy($Relative,$Name){
 $ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $workspace $Relative),[ref]$null,[ref]$null)
 $node=$ast.Find({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $Name},$true)
 if(-not $node){throw "Missing function $Name"}
 . ([scriptblock]::Create($node.Extent.Text.Replace('function '+$Name, 'function script:'+$Name)))
}
$savedPin=$env:SHCDE_API_SHARED_PUBLISHED_092;$savedSelection=$env:SHCDE_API_SHARED_MAIN_SELECTION;$savedTest=$env:SHCDE_WORKSHOP_TEST
try{
 $env:SHCDE_API_SHARED_PUBLISHED_092='1';$env:SHCDE_API_SHARED_MAIN_SELECTION='fixture';$env:SHCDE_WORKSHOP_TEST='1'
 [IO.File]::WriteAllText((Join-Path $fixture 'tools/BuildProof.ps1'), 'function Assert-ApiBuildProof($Root){if(-not [IO.File]::Exists((Join-Path $Root "valid-proof"))){throw "Missing or invalid proof"}}')
 [IO.File]::WriteAllText((Join-Path $fixture 'build.bat'), '@echo off'+"`r`n"+'echo unexpected>"%~dp0unexpected-build"'+"`r`n")
 function Assert-ApiMainSelection($Workspace,$Path){return @{Root=$fixture;Commit='c7b210f007035ff8edaf4d359a21f7bfbcbe479d'}}
 function Assert-PublishedApi092($Selection){$script:pinChecks++}
 $script:pinChecks=0
 $failed=$false;try{Ensure-ApiMainBuild $workspace}catch{$failed=$_.Exception.Message -match 'Missing or invalid proof'}
 Check $failed 'Pinned SDK did not reject missing proof'
 Check (-not [IO.File]::Exists((Join-Path $fixture 'unexpected-build'))) 'Missing proof triggered an APIShared build'
 [IO.File]::WriteAllText((Join-Path $fixture 'valid-proof'),'fixture')
 Ensure-ApiMainBuild $workspace
 Check ($pinChecks -eq 1 -and -not [IO.File]::Exists((Join-Path $fixture 'unexpected-build'))) 'Valid SDK was not reused'
 Import-Policy 'Shared/Tools/Release/Release.Common.ps1' 'Get-PublishedApiSharedRelease'
 function Get-WorkshopApiRoot($Workspace){return $fixture}
 function Invoke-CheckedCommand($FilePath,$Arguments){
  if($FilePath -eq 'git'){return @{Output=@('c7b210f007035ff8edaf4d359a21f7bfbcbe479d'+"`trefs/tags/APIShared/v0.9.2")}}
  Check ($Arguments[2] -ceq 'APIShared/v0.9.2') 'Private consumer requested a draft SDK'
  return @{ExitCode=0;Output=@('{"tagName":"APIShared/v0.9.2","isDraft":false,"url":"fixture","assets":[{"name":"APIShared-v0.9.2.zip"}]}')}
 }
 $result=Get-PublishedApiSharedRelease @{Root=$workspace;ApiShared=@{Project='APIShared';Repository='SHCDE-APIShared/APIShared'}} ([pscustomobject]@{Version='0.9.2';Commit='c7b210f007035ff8edaf4d359a21f7bfbcbe479d'})
 Check ($result.Tag -ceq 'APIShared/v0.9.2') 'Private consumer did not reuse published SDK'
 Write-Host "PASS: $checks frozen SDK/no-build/public-release checks. Fixtures: $fixture"
}finally{$env:SHCDE_API_SHARED_PUBLISHED_092=$savedPin;$env:SHCDE_API_SHARED_MAIN_SELECTION=$savedSelection;$env:SHCDE_WORKSHOP_TEST=$savedTest}
