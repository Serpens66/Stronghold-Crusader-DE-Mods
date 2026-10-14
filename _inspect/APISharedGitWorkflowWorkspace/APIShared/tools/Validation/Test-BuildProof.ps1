$ErrorActionPreference='Stop'
$temp=Join-Path ([IO.Path]::GetTempPath()) ('ApiSharedProof-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temp) | Out-Null
function Check([bool]$value,[string]$message) { if(-not$value){throw $message} }
function Write-Fixture([string]$Path,[string]$Text) { [IO.Directory]::CreateDirectory((Split-Path -Parent $Path)) | Out-Null; [IO.File]::WriteAllText($Path,$Text,[Text.UTF8Encoding]::new($false)) }
function Commit-Fixture([string]$Directory,[string]$Message) {
    & git -C $Directory add -A; if($LASTEXITCODE){throw 'Fixture staging failed'}
    & git -C $Directory -c user.name=Fixture -c user.email=fixture@example.invalid commit -m $Message | Out-Null; if($LASTEXITCODE){throw 'Fixture commit failed'}
}
try {
    $proofRoot=Join-Path $temp 'proof'
    foreach($dir in @('src','Properties','Patches','Workshop','Override/Workshop','tools')){[IO.Directory]::CreateDirectory((Join-Path $proofRoot $dir)) | Out-Null}
    Write-Fixture (Join-Path $proofRoot 'src/example.cs') 'actual input'
    Write-Fixture (Join-Path $proofRoot 'APIShared.csproj') '<Project />'
    Write-Fixture (Join-Path $proofRoot 'info.json') '{}'
    Write-Fixture (Join-Path $proofRoot '.gitignore') ".local/`n"
    Write-Fixture (Join-Path $proofRoot 'tools/Build.ps1') 'build fixture'
    foreach($name in @('WorkshopPackage.ps1','Prepare-WorkshopPackage.ps1','New-WorkshopBootstrap.ps1','PublisherPackage.ps1')) { Write-Fixture (Join-Path $proofRoot ('tools/' + $name)) 'packaging fixture' }
    Write-Fixture (Join-Path $proofRoot 'Workshop/workshop-updater.ps1') 'updater fixture'
    $viewSource=Join-Path $proofRoot 'tools/WorkshopUploader/UploadMod-linux.sh'
    $viewTarget=Join-Path $proofRoot 'BepInEx/tools/APISharedWorkshopPublisher/UploadMod-linux.sh'
    Write-Fixture $viewSource '<Grid />'
    Write-Fixture $viewTarget '<Grid />'
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../BuildProof.ps1') -Destination (Join-Path $proofRoot 'tools/BuildProof.ps1')
    $compiler=Join-Path $temp 'compiler'; Write-Fixture $compiler 'external compiler boundary'
    $dll=Join-Path $proofRoot 'BepInEx/plugins/APIShared_Serp/APIShared.dll'; Write-Fixture $dll 'external binary boundary'
    & git init --initial-branch=main $proofRoot | Out-Null; Commit-Fixture $proofRoot inputs
    . (Join-Path $proofRoot 'tools/BuildProof.ps1')
    $failed=$false; try{$null=Assert-ApiBuildProof $proofRoot}catch{$failed=$true}; Check $failed 'Missing evidence accepted'
    $state=Get-ApiInputState $proofRoot '' '' $compiler
    Write-ApiBuildProof $proofRoot '' '' $compiler $state
    $null=Assert-ApiBuildProof $proofRoot
    $null=Assert-ApiBuildProof $proofRoot -Release
    $recordedCommit=([IO.File]::ReadAllText((Join-Path $proofRoot '.local/build-proof.json'))|ConvertFrom-Json).Commit
    Write-Fixture (Join-Path $proofRoot 'tests/internal-notes.txt') 'test-only change'
    Commit-Fixture $proofRoot 'test-only commit after build'
    $accepted=Assert-ApiBuildProof $proofRoot -Release
    Check ($accepted.Commit -ceq $recordedCommit) 'Content-identical commit changed original build provenance'
    Write-Fixture (Join-Path $proofRoot 'src/example.cs') 'changed input'
    $failed=$false; try{$null=Assert-ApiBuildProof $proofRoot}catch{$failed=$true}; Check $failed 'Stale package accepted'
    Commit-Fixture $proofRoot 'runtime change after build'
    $failed=$false; try{$null=Assert-ApiBuildProof $proofRoot -Release}catch{$failed=$true}; Check $failed 'Committed runtime change accepted with old evidence'
    Write-Fixture (Join-Path $proofRoot 'src/example.cs') 'uncommitted runtime input'
    $state=Get-ApiInputState $proofRoot '' '' $compiler; Write-ApiBuildProof $proofRoot '' '' $compiler $state
    $null=Assert-ApiBuildProof $proofRoot
    $failed=$false; try{$null=Assert-ApiBuildProof $proofRoot -Release}catch{$failed=$true}; Check $failed 'Uncommitted source accepted for release'
    $crossScript=Join-Path $temp 'cross-shell.ps1'
    Write-Fixture $crossScript 'param($Root,$Compiler) . (Join-Path $Root "tools/BuildProof.ps1"); (Get-ApiInputState $Root "" "" $Compiler).Fingerprint'
    $cross=@(& powershell.exe -NoProfile -File $crossScript $proofRoot $compiler)
    Check ($LASTEXITCODE -eq 0 -and ($cross -join '').Trim() -eq $state.Fingerprint) 'Fingerprint differs across PowerShell versions'
    Write-Fixture $viewSource '<Grid Margin="1" />'
    $changedViewState=Get-ApiInputState $proofRoot '' '' $compiler
    Check ($changedViewState.Fingerprint -cne $state.Fingerprint) 'Publisher launcher change did not invalidate release evidence'
    $failed=$false; try{$null=Assert-ApiBuildProof $proofRoot}catch{$failed=$true}; Check $failed 'Changed launcher source accepted with old evidence'
    Write-Fixture $viewSource '<Grid />'
    [IO.File]::Delete($viewTarget)
    $failed=$false; try{Assert-ApiPublisherEvidence $proofRoot ([IO.File]::ReadAllText((Join-Path $proofRoot '.local/build-proof.json')) | ConvertFrom-Json)}catch{$failed=$true}; Check $failed 'Missing packaged Publisher launcher accepted'
    Write-Fixture $viewTarget '<Grid />'
    Write-Fixture $dll 'tampered binary'
    $failed=$false; try{$null=Assert-ApiBuildProof $proofRoot}catch{$failed=$true}; Check $failed 'Tampered package accepted'
    $previousGame=$env:SHCDE_GAME_DIR; $previousExtender=$env:SHCDESE_EXTENDER_DIR; $previousCompiler=$env:SHCDE_MSBUILD
    try {
        $localRoot=Join-Path $temp 'local paths with spaces'
        $game=Join-Path $localRoot ('Spiel mit Umlauten ' + [char]0x00e4)
        $extender=Join-Path $game 'BepInEx/plugins/000shcdese'
        Write-Fixture (Join-Path $game 'Stronghold Crusader Definitive Edition_Data/Managed/Assembly-CSharp.dll') 'fixture'
        Write-Fixture (Join-Path $extender 'SHCDESE.dll') 'fixture'
        $path=Join-Path $localRoot '.local/build-proof.json'
        Write-Fixture $path (@{SchemaVersion=1;GameDir=$game;ExtenderDir=$extender;MSBuild=$compiler}|ConvertTo-Json)
        $env:SHCDE_GAME_DIR='';$env:SHCDESE_EXTENDER_DIR='';$env:SHCDE_MSBUILD=''
        Initialize-ApiBuildEnvironment $localRoot
        Check ($env:SHCDE_GAME_DIR -ceq $game -and $env:SHCDESE_EXTENDER_DIR -ceq $extender -and $env:SHCDE_MSBUILD -ceq $compiler) 'Local paths were not restored for child builds'
        $childOutput=& powershell.exe -NoProfile -Command '[Console]::Write($env:SHCDE_GAME_DIR)'
        Check (($childOutput -join '') -ceq $game) 'Child process did not inherit game path'
        Write-Fixture $path 'invalid json'
        Initialize-ApiBuildEnvironment $localRoot
        Check ($env:SHCDE_GAME_DIR -ceq $game) 'Explicit environment must take precedence over unreadable evidence'
        $env:SHCDE_GAME_DIR=Join-Path $temp 'missing game'
        $failed=$false;try{Initialize-ApiBuildEnvironment $localRoot}catch{$failed=$_.Exception.Message -match 'Assembly-CSharp.dll'}
        Check $failed 'Explicit invalid game path silently fell back'
        $env:SHCDE_GAME_DIR='';$env:SHCDESE_EXTENDER_DIR=''
        $failed=$false;try{Initialize-ApiBuildEnvironment $localRoot}catch{$failed=$_.Exception.Message -match 'unreadable'}
        Check $failed 'Unreadable path evidence accepted without explicit environment'
        Remove-Item -LiteralPath $path
        $failed=$false;try{Initialize-ApiBuildEnvironment $localRoot}catch{$failed=$_.Exception.Message -match 'first build'}
        Check $failed 'Missing first-build configuration accepted'
        $env:SHCDE_GAME_DIR=$game;$env:SHCDESE_EXTENDER_DIR=Join-Path $temp 'missing extender'
        $failed=$false;try{Initialize-ApiBuildEnvironment $localRoot}catch{$failed=$_.Exception.Message -match 'Missing SHCDESE.dll'}
        Check $failed 'Invalid explicit Extender path accepted'
    } finally {
        $env:SHCDE_GAME_DIR=$previousGame;$env:SHCDESE_EXTENDER_DIR=$previousExtender;$env:SHCDE_MSBUILD=$previousCompiler
    }
    Write-Host 'PASS: content-identical commits, retained provenance, changed runtime rejection, package tampering, dirty release rejection, cross-shell fingerprint, local path recovery and child inheritance.'
} finally { Write-Host ("Build proof fixtures: " + $temp) }
