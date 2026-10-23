from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent
BASE = ROOT/'_inspect/CrusaderDE-Native-Baseline'
facts = json.loads((OUT/'preflight.json').read_text())
destination = ROOT/'Shared/Tools/ScriptExtenderUpdate'
def text(path, content):
    path.write_bytes((content.replace('\r\n','\n').rstrip()+'\n').replace('\n','\r\n').encode())

plan = {
    'OldVersion':'2.14.1','NewVersion':'2.14.2','TargetCommit':facts['targetCommit'],
    'ImpactReview': {
        'BaselinePreviousCommit':facts['previousCommit'],
        'CrusaderNativeFiles':[], 'ExternalNativeFiles':[],
        'NonSemanticArtifacts':['CHANGELOG.md'],
        'ChangedPublicContracts':[
            {'Path':'src/SHCDESE.BepInEx/API/GameAssetManagerAPI.cs',
             'Symbol':'Music override playback behavior (internal implementation; no public signature change)',
             'Consumers':[],
             'Compatibility':'Decoded music samples are cached; each uncached music playback receives a fresh streaming clip. Resource replacement invalidates its music cache. Workspace mods contain no music overrides and do not call the internal loader.'},
            {'Path':'src/SHCDESE.BepInEx/Bootstrap/Plugin.cs',
             'Symbol':'Startup preloading after GameAssetModManager.RegisterAll',
             'Consumers':[],
             'Compatibility':'Adds synchronous preloading of indexed OGG/WAV music overrides. External music packages may increase startup work and retained RAM; workspace assets do not use this path.'},
            {'Path':'src/SHCDESE.BepInEx/ManagedHooks/MyAudioManager_Hooks.cs',
             'Symbol':'MyAudioManager_LoadClip_Hook documentation',
             'Consumers':['BugfixesAndQoL','APIShared','ArabianIdleSoundTest'],
             'Compatibility':'Comment-only change in this file. Hook signature and body unchanged. Speech and SFX retain their prior paths; APIShared loads private resources through GetModFileBinaryContent and AudioClipExtensions.'}
        ],'AffectedMods':[],'BuildMods':[]
    },'Mods':{},
    'CompatibilityNotes':[
        'Source-only review and semantic baseline refresh; no extender/mod build, installation, game launch or runtime acceptance.',
        'Local bin, mod_output and installed SHCDESE.dll remain byte-identical at 2.14.1.0; SHA256 '+facts['installedExtenderSha256']+'.',
        'Source target tree: '+facts['targetTree']+'. Native hash: '+facts['nativeHash']+'.',
        'No public C# signature, Lua, enum, interop layout, native hook, AOB, semantic header or packaged dependency change.',
        'Existing minimum versions remain valid. No workspace music overrides found; the ArabianIdleSoundTest WAV uses its separate private-resource path.',
        'The known startup crash-handler false alarm has no fix in this diff and no new runtime acceptance. Existing projectile-slot workarounds are unaffected.',
        'Online upstream verification was unavailable during review; local HEAD, v2.14.2, origin/main and upstream/main agreed. This is not a claim about future upstream releases.'
    ]
}
text(destination/'2.14.1-2.14.2.compatibility.json',json.dumps(plan,indent=2))
report=f'''# Script Extender 2.14.2: source compatibility and baseline refresh

Reviewed on 2026-10-17 (Europe/Berlin). No workspace mod requires an adaptation for this source diff. No mod version, minimum dependency version or changelog is changed.

## Identity and acceptance boundary

- Baseline predecessor / v2.14.1: `{facts['previousCommit']}`.
- Source target / v2.14.2: `{facts['targetCommit']}`; tree `{facts['targetTree']}`.
- Canonical CrusaderDE.dll SHA-256: `{facts['nativeHash']}`.
- Installed Assembly-CSharp.dll SHA-256: `{facts['managedHash']}`.
- Local bin, mod_output and installed SHCDESE.dll: **2.14.1.0**, SHA-256 `{facts['installedExtenderSha256']}`.
- The refreshed source provenance is not an installed 2.14.2 binary contract or runtime acceptance. No extender/mod build, install, game start, commit or release was performed.
- Local refs agreed at review time. Online upstream verification failed due to network access; no remote freshness claim is made.

## Complete release diff and consumers

The diff contains the music-preload fix `b94161b414c8af28c81316613334e5c1f70dd658` and the release commit. Four files change: GameAssetManagerAPI, Plugin startup, a comment in MyAudioManager_Hooks, and CHANGELOG.

Music overrides under Music paths are predecoded after provider registration. Samples remain cached, while each playback obtains a fresh streaming AudioClip. Replacing an indexed resource invalidates its cached samples. Preload errors retain the existing on-demand fallback. External music packages can increase startup work and retained memory; these effects were not measured in-game.

| Consumer or contract | Review result |
|---|---|
| APIShared SoundClips; ArabianIdleSoundTest | Private binary-resource loading and unchanged AudioClipExtensions decoder; not music override loading. |
| BugfixesAndQoL unit sounds and notification speech | SFX/speech paths unaffected by the new music condition. |
| ExtraFeatures, CastlePlanner, ExtendedData, localization, helper/testmod asset consumers | File, image, AIV, text and localization accessors unchanged. |
| Public signatures, Lua, enums, interop layouts, native hooks/AOBs, headers, dependencies | No changes in the complete diff. No new native semantic claims are introduced. |
| Workspace audio assets | No music overrides found; the test WAV uses Sounds/APISharedTriad.wav. |
| Manifest compatibility | 92 source/package manifests parsed in the initial review; none excludes 2.14.2. This count is evidence, not a test invariant. |

AffectedMods and BuildMods are empty. The existing projectile-slot workaround markers remain unaffected. The known startup minidump has neither a source fix in this release nor a new runtime acceptance test; its documentation remains in place.

## Baseline procedure and evidence

The existing UpdateForScriptExtender stage refreshes source knowledge, module-filtered AOB results, index and manifest, and runs fast validation. Its SemanticExtract utility build is authorized; it does not build the extender or mods. The optional RequireUnchangedGhidraInputs guard stops before any unexpected Ghidra work. No historical export regeneration is intended.

Evidence and recoverable pre-run backups are retained under `_inspect/ScriptExtender2142Audit`: preflight identities, complete upstream diff, protected-file hashes, original baseline files/database, driver logs and final checks. Existing user changes are preserved.

Status: prepared; baseline update and final acceptance pending.
'''
text(destination/'2.14.1-2.14.2.review.md',report)
current=BASE/'CURRENT.md'
link='- [Script Extender 2.14.2 source compatibility review](../../Shared/Tools/ScriptExtenderUpdate/2.14.1-2.14.2.review.md): music-only managed change; source baseline refresh, installed extender remains 2.14.1; runtime acceptance pending.'
body=current.read_text(encoding='utf-8-sig')
assert link not in body
text(current,body.replace('# Current CrusaderDE Native Baseline','# Current CrusaderDE Native Baseline\n\n'+link,1))
driver=BASE/'tools/semantic/Build-SemanticBaseline.ps1'
text(driver,driver.read_text(encoding='utf-8-sig'))
text(Path(__file__),Path(__file__).read_text())
text(OUT/'prepare.py',(OUT/'prepare.py').read_text())
print('Compatibility plan, review, CURRENT link and CRLF written.')
