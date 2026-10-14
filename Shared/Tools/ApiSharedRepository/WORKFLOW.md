# APIShared development and Workshop releases

`APIShared/` is an independent clone of
https://github.com/SHCDE-APIShared/APIShared. It stays on `dev/Serpens66`, tracking
`origin/dev/Serpens66`. The mod repository ignores this directory and stores no
APIShared source pointer. Both repositories have their own history and commits.

## GitHub Desktop: everyday use

Add the APIShared folder through **File > Add local repository** if it is not
already listed. Choose **APIShared** to commit/push APIShared work on
`dev/Serpens66`. Choose your **modpack repository** to commit modpack work.
APIShared no longer appears in the modpack's Changes list after the one-time
submodule-removal commit. No branch switch is needed for a modpack commit.

On a fresh workspace, explicitly run from its root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Shared\Tools\ApiSharedRepository\Initialize-ApiShared.ps1
```

This clones the canonical public repository on `dev/Serpens66`. Existing folders
are never replaced or updated. Use GitHub Desktop for subsequent fetch/pull/merge.
The minimum versions in consumer manifests describe compatibility requirements;
release provenance records the exact SDK commit and hashes used for a release.
An old modpack commit alone no longer selects an APIShared development snapshot.

## One-time migration of an existing submodule

`Convert-ToIndependent.ps1` preserves APIShared working files, refs, staging and
all registered worktrees. It retains recovery metadata under
`.inspect/APISharedIndependent-*` and the old inactive Git metadata. Do not use
the old metadata as a second active repository. It stages only removal of the
APIShared gitlink; commit that removal together with the reviewed workspace
configuration/tooling changes. No APIShared commit or push is needed for this
migration. Pending Git operations must finish before migration. A caught failure
restores original links, parent index and configuration; retained backups permit
manual recovery after a process or machine interruption. Do not switch the modpack
to historical submodule-era commits in this active workspace; use another checkout
when examining those versions.

## Maintainer rights and branch readiness

Serpens66 and Ensrick are equal APIShared maintainers. Either may integrate changes
and publish releases without approval from the other. PRs provide visibility and
discussion; reviews and any future CODEOWNERS requests are advisory. Consultation
before breaking changes is recommended, not mandatory. Compatibility assessment,
appropriate tests, impact documentation and migration guidance where needed remain
required. APIShared's public CONTRIBUTING.md is the authoritative community workflow;
this document covers its use from the Serpens66 workspace.

Personal `dev/Serpens66` and `dev/Ensrick` branches allow experiments and unfinished
work without mandatory passing tests. `develop` contains completed, test-ready work
with passing automated source validation and game-independent CLI tests. `main`
contains tested, release-ready work with passing CLI checks and, for runtime changes,
the relevant local build, integration and gameplay checks. CI alone does not prove
gameplay compatibility. Development contributions target `develop`.

## Daily work in GitHub Desktop

Select APIShared and `dev/Serpens66`, commit and Push origin. Personal branch
commits need no tests. Ordinary local consumer builds use the freshly built Dev
DLL; rebuild APIShared with its `build.bat` when its runtime inputs change.

`APIShared-PRCheck-develop.bat` opens an existing PR or the comparison from
`dev/Serpens66` to `develop`. It fetches both public branches, refuses unpublished
local branch commits, reports textual conflicts and opens the browser. It never
changes checked-out files, creates commits, pushes or merges. `-NoOpen -NoPause`
checks the comparison in the terminal without opening a browser.

A PR includes ALL source commits not yet integrated. Keep unrelated unfinished
experiments on separate feature branches. After CI passes, merge on GitHub using
**Create a merge commit** and keep your personal branch. `develop` is for joint
integration and gameplay tests, not automatically ready for release.

`APIShared-PRCheck-main.bat` opens `develop` to `main` without switching your checkout.
Merge only when every included change is release-ready, using **Create a merge
commit**. Persistent branch pairs must not be squash/rebase merged. Squash remains
available for disposable feature branches. The intended main protection policy
requires passing CI without mandatory approving reviews, while permitting direct
maintainer documentation corrections. This does not claim protection is configured.

## Direct documentation corrections

Either maintainer may commit straightforward documentation corrections directly to
`main`; no separate approval or gameplay test is required. Applicable automated
checks must pass. Use an isolated worktree when another chat is using the development
checkout. Do not switch, reset, stash or move its branch pointer.

Merge the correction from `main` back into `develop`, then synchronize personal
branches when their owners are ready. Never update a personal branch while another
chat is working on it. Its owner can merge the updated `develop`, or cherry-pick a
specific documentation commit if only that correction is needed. Avoid copying entire
files over branch-specific edits. Likewise, prepare workspace instruction changes
separately and defer their application until the active workspace chats are finished.

## Synchronization and conflicts

In Desktop, Fetch origin/Pull origin first. Stay on `dev/Serpens66` and choose
**Branch > Merge into current branch** to merge the fetched `develop` into it.
Resolve conflicts in the editor, review the combined behavior, commit the resolution
and Push origin. The helpers list conflicts but never choose an author's version.
For develop/main conflicts, prepare a separate resolution PR into `develop`;
never overwrite `main` with an untested conflict resolution or unfinished development work. The direct documentation exception above remains allowed.

The old `APIShared-Aktualisieren.bat` and `APIShared-Status.bat` are absent and are
not recreated. Their underlying PowerShell scripts remain explicitly callable;
there is no automatic update or background synchronization.

## Stable Workshop preparation

`CreateSteamModPack.bat` retains APIShared / SerpsMods / Both and `/test`,
`/validate`, `/nopause`. It resolves public `main` once and pins that commit for
all targets. An isolated detached worktree is created/reused under
`.release-output/APIShared/main/<commit>/source`. This is the explicitly approved
exception to the single DEVELOPMENT-checkout rule. Changed or unexpected worktrees
are preserved and rejected, never reset or overwritten.

Manifest, build, bootstrap helpers, SDK comparison and publisher come from main.
Missing builds use its `build.bat /nopause /noinstall`; only machine reference paths
may be read as data from the development build proof. Neither the installed game
APIShared nor the development package is overwritten.

Verified releases are reusable when release-relevant inputs match selected main.
Their original source commit/DLL hashes stay in provenance; the selected main commit
is recorded separately. Missing APIShared releases retain the existing explicit
publication confirmation. APIShared versions must already be correct in main.
Use `CreateSteamModPack.bat /apishared` for this workspace's APIShared releases;
it invokes the independent APIShared release driver from the pinned main worktree.

Consumers and host build against the selected published main SDK through the
inherited override. A mod needing an unreleased Dev API fails its minimum-version
check or compilation; it never silently falls back to the development DLL.
Open APIShared development files do not block this stable path. Other
dirty mod-repository inputs still block production preparation.

## Upload and test mode

`UploadWorkshop.bat` uploads exactly the preparation. It verifies package files,
SDK provenance, pinned main source and publisher input/output evidence. New main
commits do not silently change it. Preparations without main identity must be
prepared again. Both packages must share one main selection for a combined upload.

Tests also use main, retaining private separate IDs, drafts and isolated outputs.
Dev branches/test tags are not production publication proofs. No command switches
or stashes the development checkout or promotes develop to main.
Retained worktrees and test artifacts remain inspectable; do not manually delete
Git worktree directories.
