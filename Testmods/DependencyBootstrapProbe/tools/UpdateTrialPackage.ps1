function Assert-LocalUpdateTargets([string]$Game) {
    foreach($relative in @('BepInEx/config/DependencyBootstrapProbe.local-update.json','_SE/DependencyBootstrapProbeLocalTrial.json')){
        $path=Join-Path $Game $relative;Assert-ContinuationUnlinked $path
        if(Test-Path -LiteralPath $path){throw "Preserve existing trial target: $relative"}
    }
    $stage=Join-Path $Game '_SE/.staging';Assert-ContinuationUnlinked $stage
    if((Test-Path -LiteralPath $stage) -and @(Get-ChildItem -LiteralPath $stage -Force).Count){throw 'Existing staging must be resolved before trial preparation.'}
}
function New-LocalUpdatePackage([string]$Probe,[string]$Game,[string]$MSBuild) {
    . (Join-Path $Probe 'package/Pair.ps1')
    $root=Join-Path $Probe 'update-trial'
    if(Test-Path -LiteralPath $root){throw 'Trial artifacts already exist; preserve them.'}
    [IO.Directory]::CreateDirectory($root)|Out-Null
    $plugin='BepInEx/plugins/DependencyBootstrapProbe_Serp'
    $payload=@($script:RuntimeRelative,$script:BootstrapRelative,"$plugin/info.json",$script:PairRelative,'BepInEx/config/DependencyBootstrapProbe.active','BepInEx/config/DependencyBootstrapProbe.correct')
    $builds=@()
    foreach($variant in @('A','B')){
        $target=Join-Path $root $variant
        if($variant -eq 'A'){
            foreach($rel in $payload[0..3]){$dest=Join-Path $target $rel;[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($dest))|Out-Null;Copy-Item -LiteralPath (Join-Path $Probe $rel) -Destination $dest}
        }else{
            foreach($project in @('DependencyBootstrapProbe.Patcher','DependencyBootstrapProbe')){
                $relative=if($project -like '*.Patcher'){'BepInEx/patchers'}else{$plugin}
                $output=Join-Path $target $relative
                & $MSBuild (Join-Path $Probe ($project+'.csproj')) /nologo /verbosity:minimal /p:Configuration=Release /p:DefineConstants=UPDATE_B "/p:OutputPath=$output/" "/p:IntermediateOutputPath=$root/obj-B/$project/" | Out-Host
                if($LASTEXITCODE -ne 0){throw 'B compilation failed'}
            }
            $metadata=Get-Content -LiteralPath (Join-Path $Probe 'info.json') -Raw|ConvertFrom-Json
            $metadata.Version='0.1.2'
            [IO.File]::WriteAllText((Join-Path $target "$plugin/info.json"),($metadata|ConvertTo-Json -Depth 8))
            $pair=[ordered]@{Schema=1;Guid=$metadata.GUID;Version=$metadata.Version;RuntimeSHA256=Get-ProbeHash (Join-Path $target $script:RuntimeRelative);BootstrapSHA256=Get-ProbeHash (Join-Path $target $script:BootstrapRelative)}
            [IO.File]::WriteAllText((Join-Path $target $script:PairRelative),($pair|ConvertTo-Json))
        }
        $pair=Read-ProbePair $target
        foreach($suffix in @('active','correct')){$dest=Join-Path $target ('BepInEx/config/DependencyBootstrapProbe.'+$suffix);[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($dest))|Out-Null;[IO.File]::WriteAllText($dest,$pair.BootstrapSHA256.ToUpperInvariant())}
        foreach($relative in @($script:RuntimeRelative,$script:BootstrapRelative)){
            $assembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $target $relative))
            try{
                if($assembly.Name.Version.ToString() -cne ($pair.Version+'.0')){throw 'Assembly version differs from pair.'}
                if($relative -ceq $script:RuntimeRelative){
                    $plugins=@($assembly.MainModule.Types|ForEach-Object {$_.CustomAttributes}|Where-Object {$_.AttributeType.FullName -eq 'BepInEx.BepInPlugin'})
                    if($plugins.Count -ne 1 -or $plugins[0].ConstructorArguments[0].Value -cne $pair.Guid -or $plugins[0].ConstructorArguments[2].Value -cne $pair.Version){throw 'Compiled plugin metadata differs.'}
                }
            }finally{$assembly.Dispose()}
        }
        $builds+=@{Version=$pair.Version;PatcherSHA256=$pair.BootstrapSHA256.ToUpperInvariant();RuntimeSHA256=$pair.RuntimeSHA256.ToUpperInvariant()}
        [IO.Directory]::CreateDirectory((Join-Path $target 'trial'))|Out-Null
        Copy-Item -LiteralPath (Join-Path $Probe 'trial/expectations.json') -Destination (Join-Path $target 'trial/expectations.json')
    }
    if([version]$builds[0].Version -ge [version]$builds[1].Version -or $builds[0].PatcherSHA256 -ceq $builds[1].PatcherSHA256 -or $builds[0].RuntimeSHA256 -ceq $builds[1].RuntimeSHA256){throw 'Update must replace both assemblies with a newer pair.'}
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $remote=Join-Path $root 'remote';[IO.Directory]::CreateDirectory($remote)|Out-Null
    $map=Join-Path $remote 'DependencyBootstrapProbeLocalTrial.map'
    $zip=[IO.Compression.ZipFile]::Open($map,[IO.Compression.ZipArchiveMode]::Create)
    try{
        foreach($relative in $payload){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path "$root/B" $relative),$relative)|Out-Null}
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path "$root/B" "$plugin/info.json"),'info.json')|Out-Null
    }finally{$zip.Dispose()}
    # Verify exact member set and bytes; never give general package paths write permission.
    $zip=[IO.Compression.ZipFile]::OpenRead($map)
    try{
        if((@($zip.Entries.FullName|Sort-Object)-join '|') -cne (@($payload+'info.json'|Sort-Object)-join '|')){throw 'Unexpected archive entries'}
        foreach($entry in $zip.Entries){$source=if($entry.FullName -eq 'info.json'){"$root/B/$plugin/info.json"}else{Join-Path "$root/B" $entry.FullName};$stream=$entry.Open();$hash=[Security.Cryptography.SHA256]::Create();try{if([BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-','').ToLowerInvariant() -cne (Get-ProbeHash $source)){throw 'ZIP byte mismatch'}}finally{$stream.Dispose();$hash.Dispose()}}
    }finally{$zip.Dispose()}
    $request=[ordered]@{Schema=1;TrialId=[guid]::NewGuid().ToString('N');MapSHA256=(Get-ProbeHash $map).ToUpperInvariant();Builds=$builds}
    [IO.File]::WriteAllText((Join-Path $root 'request.json'),($request|ConvertTo-Json -Depth 6))
    $updater='BepInEx/plugins/000shcdese/data/mod-updater.ps1'
    $source=Join-Path $Probe '../../shcde-script-extender/deps/data/mod-updater.ps1'
    if((Get-ProbeHash (Join-Path $Game $updater)) -cne (Get-ProbeHash $source)){throw 'Installed updater differs from audited source.'}
    $proof=[ordered]@{Schema=1;TrialId=$request.TrialId;MapSHA256=$request.MapSHA256;Payload=@(foreach($rel in $payload){@{Path=$rel;SHA256=(Get-ProbeHash (Join-Path "$root/B" $rel)).ToUpperInvariant()}});Builds=$builds;UpdaterSHA256=(Get-ProbeHash (Join-Path $Game $updater)).ToUpperInvariant();APISharedSHA256=(Get-ProbeHash (Join-Path $Game 'BepInEx/plugins/APIShared_Serp/APIShared.dll')).ToUpperInvariant();SyntheticReceipt='_SE/DependencyBootstrapProbeLocalTrial.json';BaselineReceiptSHA256=(Get-ProbeHash (Join-Path "$root/A" "$plugin/info.json")).ToUpperInvariant()}
    [IO.File]::WriteAllText((Join-Path $root 'proof.json'),($proof|ConvertTo-Json -Depth 8))
    & (Join-Path $Probe 'tests/bin/DependencyBootstrapProbe.Tests.exe') $Game update-package $root | Out-Host
    if($LASTEXITCODE -ne 0){throw 'Actual Extender package/version checks failed.'}
    Write-Host 'PASS: both executable pairs, exact six-file payload, ZIP hashes, updater identity and real Extender version decision.'
    return $root
}
function Install-LocalUpdateRequest([string]$Probe,[string]$Game,[string]$Root){
    if(Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue){throw 'Game running; trial setup refused.'}
    Assert-LocalUpdateTargets $Game
    $request=Join-Path $Game 'BepInEx/config/DependencyBootstrapProbe.local-update.json'
    $receipt=Join-Path $Game '_SE/DependencyBootstrapProbeLocalTrial.json'
    Copy-Item -LiteralPath (Join-Path $Root 'request.json') -Destination $request
    [IO.Directory]::CreateDirectory((Split-Path $receipt))|Out-Null
    Copy-Item -LiteralPath (Join-Path $Root 'A/BepInEx/plugins/DependencyBootstrapProbe_Serp/info.json') -Destination $receipt
    if((Get-ContinuationHash $request) -cne (Get-ContinuationHash (Join-Path $Root 'request.json')) -or (Get-ContinuationHash $receipt) -cne (Get-ContinuationHash (Join-Path $Root 'A/BepInEx/plugins/DependencyBootstrapProbe_Serp/info.json'))){throw 'Trial setup copy mismatch'}
    Write-Host 'Local update trial armed. Start once and WAIT at the original update dialog; do not accept before staging validation.'
}
