# APIShared in the mod workspace

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
Configure `SHCDE_GAME_DIR` for your installation first, as described in
`APIShared/CONTRIBUTING.md`; the public build contains no machine-specific path.
All workspace consumers use its local package; stale inputs or altered package files block builds.
Development edits are supported after rebuilding; releases require a clean published commit.
APIShared's `release.bat` works from this checkout and creates a draft by default.
The public repository remains independent of these workspace helpers.

An older reviewed commit is allowed. Updating to the latest main is an explicit choice,
not a prerequisite for releases. History before this migration remains in the mod Git history.
