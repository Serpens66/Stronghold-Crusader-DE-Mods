from pathlib import Path
import re, json
root=Path.cwd()
def write(path,text):
    Path(path).write_bytes(text.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))
def change(path,old,new):
    p=Path(path); text=p.read_text(encoding='utf-8-sig')
    if old not in text: raise Exception(f'Missing replacement: {path}: {old[:80]}')
    write(p,text.replace(old,new))

# Batch drivers must pass the same local directory used by evaluated MSBuild references.
drivers=[]
for p in root.rglob('build.bat'):
    relative=p.relative_to(root)
    if any(x in relative.parts for x in ['APIShared','shcde-script-extender','_inspect','bin','obj','.git']): continue
    text=p.read_text(encoding='utf-8-sig')
    if 'API_SHARED_DIR=' not in text: continue
    workspace_relative=Path(*(['..']*(len(relative.parts)-1)))
    local='%~dp0'+str(workspace_relative)+'\\APIShared\\BepInEx\\plugins\\APIShared_Serp'
    lines=[]
    for line in text.splitlines():
        if line.startswith('set "API_SHARED_DIR='): line=f'set "API_SHARED_DIR={local}"'
        if 'if not exist "%API_SHARED_DIR%\\APIShared.dll" if exist "%LOCAL_API_SHARED_DIR%' in line: continue
        if 'set "API_SHARED_DIR=%PACK_PLUGIN_ROOT%' in line: continue
        lines.append(line)
    text='\n'.join(lines)+'\n'
    # Check an explicit override as well; external directories are rejected.
    validation=(f'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0{workspace_relative}\\Shared\\Tools\\ApiSharedRepository\\Test-ConsumerPackage.ps1" '
                f'-Workspace "%~dp0{workspace_relative}" -PackageDirectory "%API_SHARED_DIR%"\nif errorlevel 1 exit /b 1\n')
    anchor='if defined SHCDE_API_SHARED_DIR set "API_SHARED_DIR=%SHCDE_API_SHARED_DIR%"\n'
    if anchor in text: text=text.replace(anchor,anchor+validation)
    else:
        index=text.index('\n',text.index('set "API_SHARED_DIR='))+1
        text=text[:index]+validation+text[index:]
    write(p,text); drivers.append(relative.as_posix())
write(root/'_inspect/ApiSharedSubmodule/changed-drivers.json',json.dumps(drivers,indent=2)+'\n')

change(root/'Shared/Tools/Release/Release.Common.ps1',
       "    return [PSCustomObject]@{\n        Directory = $apiPackage",
       "    . (Join-Path $Config.Root 'Shared/Tools/ApiSharedRepository/ApiShared.Common.ps1')\n"
       "    $proof = Assert-ApiConsumerPackage $Config.Root $apiPackage\n"
       "    return [PSCustomObject]@{\n        Commit = $proof.Commit\n        Directory = $apiPackage")
change(root/'Shared/Tools/Release/Release.Common.ps1',
       "$ApiSharedDir = Join-Path $Metadata.Config.GameDir 'BepInEx\\plugins\\APIShared_Serp'",
       "$ApiSharedDir = Join-Path $Metadata.Config.Root 'APIShared\\BepInEx\\plugins\\APIShared_Serp'")
change(root/'Shared/Tools/Release/Release-Mod.ps1',
       "Use release.bat from the independent checkout after exporting the reviewed source commit.",
       "Use APIShared/release.bat from the shared Git checkout.")
change(root/'Shared/Tools/Release/Release-Mod.ps1',
       "    if ($ValidateOnly) {",
       "    if ($apiSharedConsumer) {\n"
       "        . (Join-Path $config.Root 'Shared/Tools/ApiSharedRepository/ApiShared.Common.ps1')\n"
       "        $null = Assert-ApiReleaseState $config.Root\n    }\n    if ($ValidateOnly) {")
change(root/'Shared/Tools/Release/Release-Mod.ps1',
       "        ValidatedVersion = [string]$apiSharedPackage.Version",
       "        Commit = [string]$apiSharedPackage.Commit\n        ValidatedVersion = [string]$apiSharedPackage.Version")
change(root/'Shared/Tools/Release/Write-LocalBuildManifest.ps1',
       "        Dependencies = @(Get-DependencyRecords",
       "        ApiSharedCommit = $(if (Test-Path -LiteralPath (Join-Path $metadata.Config.Root 'APIShared/.git')) { (& git -C (Join-Path $metadata.Config.Root 'APIShared') rev-parse HEAD) } else { $null })\n        Dependencies = @(Get-DependencyRecords")

# Historical root-tree reads remain supported alongside gitlinks.
change(root/'Shared/Tools/Release/ReleaseStatus.Common.ps1',
       '    $result = Invoke-StatusGit -Config $Config -Arguments @(\'show\', "${Revision}:$Path") -AllowFailure:$AllowMissing',
       '''    if ($Path.StartsWith('APIShared/', [StringComparison]::OrdinalIgnoreCase)) {
        $entry = Invoke-StatusGit -Config $Config -Arguments @('ls-tree', $Revision, '--', 'APIShared') -AllowFailure:$AllowMissing
        $row = $entry.Output -join ''
        if ($row -match '^160000 commit ([0-9a-f]{40})\\s+APIShared$') {
            $result = Invoke-CheckedCommand -FilePath 'git' -Arguments @('-C', (Join-Path $Config.Root 'APIShared'), 'show', ($Matches[1] + ':' + $Path.Substring(10))) -AllowFailure:$AllowMissing
            if ($result.ExitCode -ne 0) { return $null }
            return ($result.Output -join "`n")
        }
    }
    $result = Invoke-StatusGit -Config $Config -Arguments @('show', "${Revision}:$Path") -AllowFailure:$AllowMissing''')
change(root/'Shared/Tools/Release/ReleaseStatus.Common.ps1',
       '    return @($paths | Sort-Object)\n}',
       '''    if ($xml.SelectNodes('//*[local-name()="Reference"][@Include="APIShared"]').Count -gt 0) { [void]$paths.Add('APIShared') }
    return @($paths | Sort-Object)
}''')
change(root/'Shared/Tools/Release/ReleaseStatus.Common.ps1',
       "    if (-not $Path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { return $false }",
       "    if ($Path -eq 'APIShared' -and $ProjectDirectory -eq 'APIShared') { return $true }\n    if (-not $Path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { return $false }")

agents=root/'AGENTS.md'
text=agents.read_text(encoding='utf-8-sig')
text=text.replace('APIShared ist die kontrollierte Git-Subtree-Kopie des öffentlichen Repos', 'APIShared ist der einzige aktive Git-Submodule-Checkout des öffentlichen Repos')
text=text.replace('Community-Commits/Tags ausdrücklich auswählen, keine automatische Synchronisierung.', 'APIShared-Status.bat und APIShared-Aktualisieren.bat verwenden; keine automatische Aktualisierung. APIShared-Commits zuerst veröffentlichen, danach den geprüften Submodule-Zeiger im Mod-Repository committen. Verbraucher verwenden ausschließlich das frische lokale APIShared-Paket; veröffentlichte Mindestversionen bleiben unverändert.')
write(agents,text)
write(root/'Shared/Tools/ApiSharedRepository/WORKFLOW.md', '''# APIShared in the mod workspace

`APIShared/` is the single active checkout of https://github.com/SHCDE-APIShared/APIShared,
registered as a Git submodule. The mod repository records a reviewed APIShared commit.
There is no second working copy, subtree export, background update or source synchronization.

From any current directory, double-click the workspace helpers:

- `APIShared-Status.bat`: show commits, branch, open changes and package freshness.
- `APIShared-Status.bat /release`: additionally check clean repositories, the recorded commit and its public publication. This does not fetch or change refs.
- `APIShared-Aktualisieren.bat`: fetch and merge `origin/main` into the current APIShared branch.
- `APIShared-Aktualisieren.bat <commit-or-tag>`: merge that explicitly selected revision.

Both helpers pause on completion unless `/nopause` is supplied. Open APIShared changes and
unfinished Git operations block updates. Detached HEAD gets a new unused `codex/api-work`
branch, preserving the starting commit. Conflicts remain available for manual resolution;
the helper prints continue/abort commands. No push, root commit, build or release happens automatically.

For a fresh clone use `git clone --recurse-submodules <mod-repository-url>`.
For an existing clone initialize its recorded version with `git submodule update --init APIShared`.
The update helper can also initialize it before merging a selected newer version.

Edit and commit APIShared from inside its directory. Publish contributions using ordinary
branches and pull requests. After review and tests, record the chosen commit in the parent:
`git add APIShared` then commit in the mod repository. Publish APIShared commits first.
Do not commit a parent pointer to a commit other users cannot obtain.

Run APIShared's own elevated `build.bat /nopause` before consumer builds.
All workspace consumers use its local package; stale inputs or altered package files block builds.
Development edits are supported after rebuilding; releases require a clean published commit.
APIShared's `release.bat` works from this checkout and creates a draft by default.
The public repository remains independent of these workspace helpers.

An older reviewed commit is allowed. Updating to the latest main is an explicit choice,
not a prerequisite for releases. History before this migration remains in the mod Git history.
''')
for p in [root/'.gitmodules', root/'Directory.Build.targets', *root.glob('APIShared-*.bat'),
          * (root/'Shared/Tools/ApiSharedRepository').glob('*.ps1'),root/'APIShared/tools/BuildProof.ps1',root/'APIShared/tools/Build.ps1',root/'APIShared/tools/Release/Release.ps1']:
    write(p,p.read_text(encoding='utf-8-sig'))
print(f'Updated {len(drivers)} consumer build drivers; centralized MSBuild references cover the other consumers.')
